#Requires -Version 5.1
<#
.SYNOPSIS
  Read-only identity and capability check for an *already installed* official RFiDGear.
.DESCRIPTION
  Never installs, starts or modifies RFiDGear. No reader scan, no project load,
  no key registration, no run, no automatic mode and no card writes.
  Optionally invokes only the official sample client's "status" command when
  its executable is supplied explicitly. Emits a key-free JSON report.
  A green status is NOT a productive bridge, vault or hardware acceptance.
.EXAMPLE
  .\tools\Test-OfficialRFiDGearBridgeReadiness.ps1 -RFiDGearDirectory 'C:\Program Files\RFiDGear'
.EXAMPLE
  .\tools\Test-OfficialRFiDGearBridgeReadiness.ps1 -RFiDGearDirectory 'C:\Program Files\RFiDGear' -SampleClientExePath 'C:\Lab\RFiDGear.Bridge.SampleClient.exe' -LiveStatus
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $RFiDGearDirectory,

    [string] $SampleClientExePath = '',

    [switch] $LiveStatus,

    [string] $ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FileEvidence {
    param([Parameter(Mandatory = $true)][string] $Path)
    if (-not [IO.File]::Exists($Path)) {
        return $null
    }

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash
    return [ordered]@{
        file = $item.Name
        bytes = [long]$item.Length
        sha256 = $hash
        fileVersion = $item.VersionInfo.FileVersion
    }
}

. (Join-Path $PSScriptRoot 'BridgeReadinessStatusParser.ps1')

$root = [IO.Path]::GetFullPath($RFiDGearDirectory)
$exe = Join-Path $root 'RFiDGear.exe'
$contract = Join-Path $root 'RFiDGear.Bridge.Contracts.dll'

$engineEvidence = Get-FileEvidence -Path $exe
$contractEvidence = Get-FileEvidence -Path $contract

if ($null -eq $engineEvidence -or $null -eq $contractEvidence) {
    throw 'Die offizielle RFiDGear.exe oder RFiDGear.Bridge.Contracts.dll fehlt im angegebenen Verzeichnis. Keine Bridge-Freigabe.'
}

$live = $null
if ($LiveStatus) {
    if ([string]::IsNullOrWhiteSpace($SampleClientExePath)) {
        throw 'Fuer -LiveStatus muss -SampleClientExePath auf eine bereits gebaute offizielle SampleClient-EXE zeigen.'
    }
    $sample = [IO.Path]::GetFullPath($SampleClientExePath)
    if (-not [IO.File]::Exists($sample)) {
        throw 'Die angegebene SampleClient-EXE wurde nicht gefunden.'
    }

    # "status" is read-only; never call run, auto, keys, scanReader or loadProject.
    $output = @(& $sample status 2>&1 | ForEach-Object { [string] $_ })
    $exitCode = $LASTEXITCODE
    $live = Get-BridgeReadinessFromStatus -Lines $output -ExitCode $exitCode
}

$signatureStatus = (Get-AuthenticodeSignature -FilePath $exe -ErrorAction Stop).Status.ToString()
$report = [ordered]@{
    schemaVersion = 1
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    scope = 'read-only-engine-and-optional-status-probe'
    engine = $engineEvidence
    contract = $contractEvidence
    executableSignatureStatus = $signatureStatus
    liveStatus = $live
    caveats = @(
        'A file version or digest does not prove that a bridge capability works.'
        'Published v2.1.0 does not contain the later master-only PRs #396 through #399. Always inspect actual capabilities instead of inferring them from a version label.'
        'Capability observations do not verify the Check UID task gate, ownRunsOnly enforcement or a verified card readback.'
        'A project revision is valid only within its RFiDGear process; external payloads need separate SHA-256 checks.'
        'Vault authorization, card approval, real reader behavior and hardware writing are NOT tested by this script.'
        'Unknown start or run result means inspect and read back the card; never blindly repeat a write.'
    )
    productiveBridgeApproved = $false
}

$json = $report | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $absolute = [IO.Path]::GetFullPath($ReportPath)
    if ([IO.File]::Exists($absolute)) {
        throw 'Der Bericht existiert bereits. Kein automatisches Ueberschreiben.'
    }
    $parent = [IO.Path]::GetDirectoryName($absolute)
    if (-not [IO.Directory]::Exists($parent)) {
        throw 'Der Zielordner fuer den Bericht existiert nicht.'
    }
    $json | Set-Content -LiteralPath $absolute -Encoding UTF8 -NoNewline
}

Write-Output $json

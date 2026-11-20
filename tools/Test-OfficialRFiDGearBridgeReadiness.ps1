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

function Test-SafeCommandOutput {
    param([string[]] $Lines, [int] $ExitCode)
    $found = New-Object 'System.Collections.Generic.List[string]'
    $startCompleted = $null
    $uiResponsive = $null
    foreach ($line in $Lines) {
        # Parse only the documented status output. Never save its unfiltered lines:
        # a future sample client may include a project path or sensitive values.
        $text = [string] $line
        if ($text -match '^offers:\s*(.*)$') {
            foreach ($entry in ($Matches[1] -split '\s*,\s*')) {
                $cap = $entry.Trim()
                if ($cap -match '^[a-zA-Z0-9._-]{1,80}$' -and -not $found.Contains($cap)) {
                    [void] $found.Add($cap)
                }
            }
        }
        elseif ($text -match '^start-up complete:\s*(True|False)(?:,|$)') {
            $startCompleted = ($Matches[1] -eq 'True')
        }
        elseif ($text -match '^main window:\s*(answers|DOES NOT ANSWER)') {
            $uiResponsive = ($Matches[1] -eq 'answers')
        }
    }

    return [ordered]@{
        statusCommandSucceeded = ($ExitCode -eq 0)
        startupComplete = $startCompleted
        uiResponsive = $uiResponsive
        capabilities = @($found.ToArray())
        executionRunOffered = $found.Contains('execution.run')
        projectRevisionOffered = $found.Contains('project.revision')
        keyProviderOffered = $found.Contains('keys.provider')
        # No productive readiness claim is possible from "status" alone.
        productiveWritingApproved = $false
    }
}

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
    $live = Test-SafeCommandOutput -Lines $output -ExitCode $exitCode
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
        'The official v2.1.0 release does not contain upstream master PR #396 project.revision or #397 Check UID with variable.'
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

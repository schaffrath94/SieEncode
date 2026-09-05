[CmdletBinding()]
param(
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$EvidenceDirectory = '',
    [switch]$RunVisibleUiSmoke
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = Join-Path $PSScriptRoot 'Evidenz' }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null

function Get-Hash([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-','')) }
    finally { $sha.Dispose() }
}

function Test-DirectoryWrite([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return [pscustomobject]@{ Exists=$false; Writable=$false; IsUnc=$false; PathHash=$null; Error='Pfad ist leer.' } }
    $full = [IO.Path]::GetFullPath($Path)
    try {
        if (-not (Test-Path -LiteralPath $full -PathType Container)) { return [pscustomobject]@{ Exists=$false; Writable=$false; IsUnc=$full.StartsWith('\\'); PathHash=(Get-Hash $full); Error='Ordner fehlt.' } }
        $probe = Join-Path $full ('.sieencode-pilot-probe-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($probe, 'SieEncode pilot write probe', (New-Object Text.UTF8Encoding($false)))
        Remove-Item -LiteralPath $probe -Force
        return [pscustomobject]@{ Exists=$true; Writable=$true; IsUnc=$full.StartsWith('\\'); PathHash=(Get-Hash $full); Error=$null }
    }
    catch { return [pscustomobject]@{ Exists=(Test-Path -LiteralPath $full); Writable=$false; IsUnc=$full.StartsWith('\\'); PathHash=(Get-Hash $full); Error=$_.Exception.Message } }
}

function Signature-Status([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return [pscustomobject]@{ Exists=$false; Status='Missing'; Sha256=$null; Signer=$null } }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    return [pscustomobject]@{
        Exists=$true; Status=[string]$signature.Status; Sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
        Signer=if($signature.SignerCertificate){$signature.SignerCertificate.Subject}else{$null}
    }
}

if (-not (Test-Path -LiteralPath $InstalledExe)) { throw "SieEncode ist nicht installiert: $InstalledExe" }
$settingsPath = Join-Path $env:APPDATA 'SieEncode\studio.settings.json'
if (-not (Test-Path -LiteralPath $settingsPath)) { throw "SieEncode-Einstellungen fehlen: $settingsPath" }
$settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
$projects = [string]$settings.Projects.Directory
$profiles = if ($settings.Profiles -and -not [string]::IsNullOrWhiteSpace([string]$settings.Profiles.Directory)) { [string]$settings.Profiles.Directory } else { Join-Path (Split-Path -Parent $settingsPath) 'profiles' }

if ($RunVisibleUiSmoke) {
    $process = Start-Process -FilePath $InstalledExe -ArgumentList '--ui-smoke-test' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Sichtbarer UI-Smoke-Test fehlgeschlagen: $($process.ExitCode)" }
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SieEncodePilotDpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
}
'@
Add-Type -AssemblyName System.Windows.Forms
[void][SieEncodePilotDpi]::SetProcessDpiAwarenessContext([IntPtr](-4))
$dpi = [int][SieEncodePilotDpi]::GetDpiForSystem()
$scale = [int][Math]::Round(($dpi / 96.0) * 100)
$screen = [Windows.Forms.Screen]::PrimaryScreen.Bounds
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$sidHash = Get-Hash $identity.User.Value
$machineHash = Get-Hash $env:COMPUTERNAME
$uiReportPath = Join-Path $env:TEMP 'SieEncode\ui-smoke\ui-smoke-result.json'
$uiReport = if (Test-Path -LiteralPath $uiReportPath) { Get-Content -Raw -LiteralPath $uiReportPath | ConvertFrom-Json } else { $null }
$uiFresh = $uiReport -and $uiReport.status -eq 'OK' -and ([DateTimeOffset]$uiReport.generatedAt) -gt [DateTimeOffset]::Now.AddMinutes(-30)
$installRoot = Split-Path -Parent (Split-Path -Parent $InstalledExe)
$runtimeExe = Join-Path $installRoot 'Runtime\SieEncode.Runtime.exe'
$packageManifest = Join-Path $PSScriptRoot 'PACKAGE-SHA256.txt'
$packageHeadline = if (Test-Path -LiteralPath $packageManifest) {
    [IO.File]::ReadAllLines($packageManifest, (New-Object Text.UTF8Encoding($false))) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
} else { 'SieEncode – internes Releasepaket' }
$evidenceHeadline = $packageHeadline -replace 'interne Releasepakete', 'interne Abnahmeumgebung'
$msi = Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'SieEncode_*_internal_x64.msi' -File -ErrorAction SilentlyContinue | Select-Object -First 1

$result = [ordered]@{
    schemaVersion='1.0'; containsKeys=$false; generatedAt=[DateTimeOffset]::Now.ToString('O')
    product=@{ version=(Get-Item $InstalledExe).VersionInfo.ProductVersion; studio=(Signature-Status $InstalledExe); runtime=(Signature-Status $runtimeExe); installer=if($msi){Signature-Status $msi.FullName}else{$null} }
    environment=@{ sidHash=$sidHash; machineHash=$machineHash; dpi=$dpi; scalePercent=$scale; screenWidth=$screen.Width; screenHeight=$screen.Height }
    sharedPaths=@{ projects=(Test-DirectoryWrite $projects); profiles=(Test-DirectoryWrite $profiles) }
    ui=@{ executed=[bool]$RunVisibleUiSmoke; fresh=[bool]$uiFresh; status=if($uiReport){$uiReport.status}else{'Missing'}; version=if($uiReport){$uiReport.version}else{$null}; checks=if($uiReport){@($uiReport.checks)}else{@()} }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$baseName = "pilot-$($sidHash.Substring(0,12))-$scale-$stamp"
$jsonPath = Join-Path $EvidenceDirectory ($baseName + '.json')
$textPath = Join-Path $EvidenceDirectory ($baseName + '.txt')
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$status = if($uiFresh -and $result.sharedPaths.projects.Writable -and $result.sharedPaths.profiles.Writable){'BESTANDEN'}else{'BEFUND'}
@(
    $evidenceHeadline,
    ('Status: ' + $status),
    ('Erzeugt: ' + $result.generatedAt),
    ('Version: ' + $result.product.version),
    ('Windows-Identität: ' + $sidHash.Substring(0,16) + '… (Hash)'),
    ('Rechner: ' + $machineHash.Substring(0,16) + '… (Hash)'),
    ('DPI: ' + $dpi + ' / ' + $scale + ' %'),
    ('Bildschirm: ' + $screen.Width + ' × ' + $screen.Height),
    ('Projektpfad schreibbar: ' + $result.sharedPaths.projects.Writable),
    ('Profilpfad schreibbar: ' + $result.sharedPaths.profiles.Writable),
    ('UI-Smoke-Test: ' + $result.ui.status),
    ('Studio-Signatur: ' + $result.product.studio.Status),
    ('Runtime-Signatur: ' + $result.product.runtime.Status),
    '',
    'Der Bericht enthält keine Schlüssel, Vault-Inhalte, Projektdefinitionen oder Kartennutzdaten.'
) | Set-Content -LiteralPath $textPath -Encoding UTF8
Write-Host "Pilotprüfung: $status"
Write-Host $jsonPath
Write-Host $textPath
if($status -ne 'BESTANDEN'){exit 2}

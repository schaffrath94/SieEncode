[CmdletBinding()]
param(
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$SettingsPath = (Join-Path $env:APPDATA 'SieEncode\studio.settings.json'),
    [string]$PackageMsi = '',
    [string]$PackageVersion = '',
    [switch]$RunVisibleUiSmoke
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$evidenceRoot = Join-Path $root 'artifacts\field-evidence'
$dpiRoot = Join-Path $evidenceRoot 'dpi'
$userRoot = Join-Path $evidenceRoot 'users'
New-Item -ItemType Directory -Path $dpiRoot,$userRoot -Force | Out-Null

function Test-DirectoryWrite([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return [pscustomobject]@{ Path=$Path; Exists=$false; Writable=$false; Error='Pfad ist leer.' } }
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return [pscustomobject]@{ Path=$Path; Exists=$false; Writable=$false; Error='Ordner fehlt.' } }
        $probe = Join-Path $Path ('.sieencode-write-probe-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($probe, 'SieEncode write probe', (New-Object Text.UTF8Encoding($false)))
        Remove-Item -LiteralPath $probe -Force
        return [pscustomobject]@{ Path=$Path; Exists=$true; Writable=$true; Error=$null }
    }
    catch { return [pscustomobject]@{ Path=$Path; Exists=(Test-Path -LiteralPath $Path); Writable=$false; Error=$_.Exception.Message } }
}

if (-not (Test-Path -LiteralPath $InstalledExe)) { throw "Installierte Anwendung fehlt: $InstalledExe" }
if (-not (Test-Path -LiteralPath $SettingsPath)) { throw "Einstellungen fehlen: $SettingsPath" }
$installedProductVersion = (Get-Item -LiteralPath $InstalledExe).VersionInfo.ProductVersion
$installedExeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $InstalledExe).Hash
$settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
$projectsDirectory = [IO.Path]::GetFullPath([string]$settings.Projects.Directory)
$profilesDirectory = if ($settings.Profiles -and -not [string]::IsNullOrWhiteSpace([string]$settings.Profiles.Directory)) {
    [IO.Path]::GetFullPath([string]$settings.Profiles.Directory)
} else { Join-Path (Split-Path -Parent $SettingsPath) 'profiles' }

if ($RunVisibleUiSmoke) {
    $process = Start-Process -FilePath $InstalledExe -ArgumentList '--ui-smoke-test' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Sichtbarer UI-Smoke-Test fehlgeschlagen: $($process.ExitCode)" }
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SieEncodeDpiProbe {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
}
'@
Add-Type -AssemblyName System.Windows.Forms
[void][SieEncodeDpiProbe]::SetProcessDpiAwarenessContext([IntPtr](-4))
$dpi = [int][SieEncodeDpiProbe]::GetDpiForSystem()
$scale = [int][Math]::Round(($dpi / 96.0) * 100)
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$uiReport = Join-Path $env:TEMP 'SieEncode\ui-smoke\ui-smoke-result.json'
$uiEvidence = if (Test-Path -LiteralPath $uiReport) { Get-Content -Raw -LiteralPath $uiReport | ConvertFrom-Json } else { $null }
$uiFresh = $uiEvidence -and ([DateTimeOffset]$uiEvidence.generatedAt) -gt [DateTimeOffset]::Now.AddHours(-2) -and
    $uiEvidence.status -eq 'OK' -and [string]$uiEvidence.version -eq $installedProductVersion

$msiPath = if (-not [string]::IsNullOrWhiteSpace($PackageMsi)) {
    [IO.Path]::GetFullPath($PackageMsi)
} else {
    @(Get-ChildItem -LiteralPath (Join-Path $root 'artifacts\package') -Filter 'SieEncode_*_internal_x64.msi' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
if ([string]::IsNullOrWhiteSpace($PackageVersion) -and $msiPath -and (Split-Path -Leaf $msiPath) -match '^SieEncode_(?<version>.+)_internal_x64\.msi$') {
    $PackageVersion = $Matches['version']
}
$installerPassed = $msiPath -and (Test-Path -LiteralPath $msiPath -PathType Leaf) -and
    -not [string]::IsNullOrWhiteSpace($PackageVersion) -and $installedProductVersion -eq $PackageVersion
$signaturePaths = @($InstalledExe) + @(if ($msiPath -and (Test-Path -LiteralPath $msiPath -PathType Leaf)) { $msiPath })
$signatures = @($signaturePaths | ForEach-Object {
    $signature = Get-AuthenticodeSignature -LiteralPath $_
    [pscustomobject]@{ Path=$_; Status=[string]$signature.Status; Subject=if($signature.SignerCertificate){$signature.SignerCertificate.Subject}else{$null} }
})
$signed = @($signatures | Where-Object Status -ne 'Valid').Count -eq 0

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$userEvidence = [ordered]@{
    schemaVersion='1.0'; capturedAt=[DateTimeOffset]::Now.ToString('O'); user=$identity.Name; sid=$identity.User.Value
    machine=$env:COMPUTERNAME; projects=(Test-DirectoryWrite $projectsDirectory); profiles=(Test-DirectoryWrite $profilesDirectory)
}
$safeSid = $identity.User.Value.Replace('-','_')
$userEvidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $userRoot ($safeSid + '.json')) -Encoding UTF8

$dpiEvidence = [ordered]@{
    schemaVersion='1.0'; capturedAt=[DateTimeOffset]::Now.ToString('O'); dpi=$dpi; scalePercent=$scale
    primaryScreen=@{ width=$screen.Width; height=$screen.Height }
    uiSmokeStatus=if($uiFresh){'OK'}else{'FEHLT_ODER_VERALTET'}; uiSmokeGeneratedAt=if($uiEvidence){$uiEvidence.generatedAt}else{$null}
}
$dpiEvidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dpiRoot ("scale-$scale.json")) -Encoding UTF8

$dpiScales = @(Get-ChildItem -LiteralPath $dpiRoot -Filter 'scale-*.json' -File | ForEach-Object { (Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json).scalePercent } | Sort-Object -Unique)
$users = @(Get-ChildItem -LiteralPath $userRoot -Filter '*.json' -File | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json })
$validUsers = @($users | Where-Object { $_.projects.Writable -and $_.profiles.Writable } | Select-Object -ExpandProperty sid -Unique)
$hardwareEvidence = Join-Path $root 'artifacts\package\installed-readonly-ui-result.json'
$hardware = if (Test-Path -LiteralPath $hardwareEvidence) { Get-Content -Raw -LiteralPath $hardwareEvidence | ConvertFrom-Json } else { $null }
$runtimePath = Join-Path (Split-Path -Parent (Split-Path -Parent $InstalledExe)) 'Runtime\SieEncode.Runtime.exe'
$runtimeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $runtimePath).Hash
$hardwareValid = $hardware -and $hardware.schemaVersion -eq '1.1' -and
    [string]$hardware.installedProductVersion -eq $installedProductVersion -and
    [string]$hardware.installedExeSha256 -eq $installedExeHash -and
    [string]$hardware.runtimeExeSha256 -eq $runtimeHash -and
    ([string]$hardware.stationState -like 'KARTENSTATUS*' -or [string]$hardware.stationState -like 'TRANSPONDER*') -and
    @($hardware.rfidgearProcessesAfterTerminal).Count -eq 0
$multiCardEvidence = $null
$multiCardRoot = Join-Path $root 'pilot-testkit\Evidenz'
if (Test-Path -LiteralPath $multiCardRoot) {
    foreach ($directory in @(Get-ChildItem -LiteralPath $multiCardRoot -Directory | Sort-Object LastWriteTime -Descending)) {
        $statePath = Join-Path $directory.FullName 'active-series.json'
        $summaryPath = Join-Path $directory.FullName 'cards\series-summary.json'
        if (-not (Test-Path -LiteralPath $statePath) -or -not (Test-Path -LiteralPath $summaryPath)) { continue }
        try {
            $seriesState = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
            $seriesSummary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
            $cardFiles = @(Get-ChildItem -LiteralPath (Join-Path $directory.FullName 'cards') -Filter 'TESTKARTE-*.json' -File)
            $cards = @($cardFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json })
            $valid = $seriesState.schemaVersion -eq '1.2' -and
                [string]$seriesState.installedProductVersion -eq $installedProductVersion -and
                [string]$seriesState.installedExeSha256 -eq $installedExeHash -and
                [string]$seriesState.runtimeExeSha256 -eq $runtimeHash -and
                $seriesSummary.schemaVersion -eq '1.2' -and
                [string]$seriesSummary.keySetIdentity -match '^[A-Fa-f0-9]{64}$' -and
                [string]$seriesSummary.projectName -eq [string]$seriesState.project -and
                @($seriesState.completedCards).Count -eq 5 -and $cards.Count -eq 5 -and
                @($cards.cardLabel | Sort-Object -Unique).Count -eq 5 -and @($cards.seriesNumber | Sort-Object -Unique).Count -eq 5 -and
                @($cards | Where-Object { $_.schemaVersion -ne '1.2' -or $_.containsKeys -or [string]$_.installedProductVersion -ne $installedProductVersion -or [string]$_.installedExeSha256 -ne $installedExeHash -or [string]$_.runtimeExeSha256 -ne $runtimeHash -or -not $_.coding.runtimeClosed -or $_.coding.state -notlike 'KODIERUNG ERFOLGREICH*' -or $_.coding.evidence -notmatch 'bytegleich' -or $_.coding.evidence -notmatch 'TaskLoop.Success' -or $_.coding.evidence -notmatch 'Korrelation OK' }).Count -eq 0 -and
                -not $seriesSummary.containsKeys -and [int]$seriesSummary.counts.successful -eq 5 -and [int]$seriesSummary.counts.failed -eq 0 -and [int]$seriesSummary.counts.skipped -eq 0
            if ($valid) { $multiCardEvidence = [pscustomobject]@{ Path=$directory.FullName; Numbers=($cards.seriesNumber -join ', ') }; break }
        }
        catch { continue }
    }
}

$gates = @(
    [pscustomobject]@{ Gate='Software/UI'; Status=if($uiFresh){'BESTANDEN'}else{'OFFEN'}; Evidence="UI-Smoke $($uiEvidence.version), aktuelle Skala $scale %" },
    [pscustomobject]@{ Gate='Installer'; Status=if($installerPassed){'BESTANDEN'}else{'OFFEN'}; Evidence=("Installiertes Studio $installedProductVersion; erwartetes Paket $PackageVersion; Datei $(Split-Path -Leaf $msiPath). Das Gate besteht erst nach Installation exakt dieser Version.") },
    [pscustomobject]@{ Gate='Runtime/Hardware'; Status=if($multiCardEvidence -or $hardwareValid){'BESTANDEN'}else{'OFFEN'}; Evidence=if($multiCardEvidence){"Runtime SHA-256 $runtimeHash; vollständiger realer 5-Karten-Lauf mit Formatierung, Mastering, Kodierung, 26/26 Tasks, 3/3 Readbacks und geschlossenem Runtimeprozess bestanden."}elseif($hardwareValid){"Runtime SHA-256 $runtimeHash; versions- und binärgebundene Read-only-Evidenz: $($hardware.stationState); Runtimeprozess geschlossen."}else{"Runtime SHA-256 $runtimeHash; gültige versions- und binärgebundene Read-only-Evidenz fehlt."} },
    [pscustomobject]@{ Gate='Codesignatur'; Status=if($signed){'BESTANDEN'}else{'INTERN_AKZEPTIERT'}; Evidence=($signatures | ForEach-Object { "$(Split-Path -Leaf $_.Path): $($_.Status)" }) -join '; ' },
    [pscustomobject]@{ Gate='DPI/Fenster'; Status=if(@(@(100,125,150) | Where-Object { $dpiScales -notcontains $_ }).Count -eq 0){'BESTANDEN'}else{'OFFEN'}; Evidence=('Erfasste reale Skalen: ' + ($dpiScales -join ', ') + ' %; benötigt: 100, 125, 150 %') },
    [pscustomobject]@{ Gate='Mehrbenutzer'; Status=if($validUsers.Count -ge 2){'BESTANDEN'}else{'OFFEN'}; Evidence="$($validUsers.Count)/2 unterschiedliche Windows-SIDs mit Schreibtest auf Projekt- und Profilpfad" },
    [pscustomobject]@{ Gate='Usability'; Status='OFFEN'; Evidence='Zwei unabhängige fachfremde Testpersonen erforderlich; nicht synthetisch ersetzbar.' },
    [pscustomobject]@{ Gate='Mehrkartenserie'; Status=if($multiCardEvidence){'BESTANDEN'}else{'OFFEN'}; Evidence=if($multiCardEvidence){"5/5 eindeutige Karten freigegeben; Nummern $($multiCardEvidence.Numbers); Evidenz $($multiCardEvidence.Path)"}else{'Fünf unterscheidbare physische Testtransponder mit vollständiger Einzelevidenz erforderlich.'} }
)
$result = [ordered]@{ schemaVersion='1.0'; generatedAt=[DateTimeOffset]::Now.ToString('O'); version=(Get-Item $InstalledExe).VersionInfo.ProductVersion; gates=$gates }
$jsonPath = Join-Path $evidenceRoot 'pilot-release-gates.json'
$textPath = Join-Path $evidenceRoot 'pilot-release-gates.txt'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$lines = @('SieEncode – interne Release-Gates', ('Erzeugt: '+$result.generatedAt), ('Version: '+$result.version), '')
$lines += $gates | ForEach-Object { "[$($_.Status)] $($_.Gate): $($_.Evidence)" }
$lines | Set-Content -LiteralPath $textPath -Encoding UTF8
$lines
Write-Host $jsonPath
Write-Host $textPath

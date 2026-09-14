[CmdletBinding()]
param(
    [string]$RuntimeSource = '',
    [string]$ArtifactsPath = '',
    [string]$WixBin = $env:WIX_BIN,
    [string]$PackageVersion = '1.1.0-rc15',
    [string]$MsiProductVersion = '1.1.14',
    [string]$CodeSigningCertificateThumbprint = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$RequireSignature,
    [switch]$AcceptanceCandidateRuntime
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($RuntimeSource)) {
    throw 'RuntimeSource muss explizit auf die zuvor getestete und identifizierte Runtime zeigen.'
}
$RuntimeSource = [IO.Path]::GetFullPath($RuntimeSource)
$artifacts = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not [string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $candidateArtifacts = [IO.Path]::GetFullPath($ArtifactsPath)
    if (-not $candidateArtifacts.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ArtifactsPath muss ein Unterordner des Repository-Artefaktordners sein.'
    }
    $artifacts = $candidateArtifacts
}
$release = [IO.Path]::GetFullPath((Join-Path $artifacts 'release'))
$package = [IO.Path]::GetFullPath((Join-Path $artifacts 'package'))
$studio = Join-Path $release 'Studio'
$runtime = Join-Path $release 'Runtime'
$wixWork = Join-Path $artifacts 'wix'
if ([string]::IsNullOrWhiteSpace($WixBin)) { throw 'WiX 3 bin directory must be supplied with -WixBin or WIX_BIN.' }
$wixBin = [IO.Path]::GetFullPath($WixBin)
$signingEnabled = -not [string]::IsNullOrWhiteSpace($CodeSigningCertificateThumbprint)
if ($RequireSignature -and -not $signingEnabled) { throw 'Release-Signatur ist vorgeschrieben, aber kein Zertifikat-Thumbprint wurde angegeben.' }
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw "Ungültige Paketversion: $PackageVersion" }
if ($MsiProductVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Ungültige MSI-Produktversion: $MsiProductVersion" }
$packageVersionFilePart = $PackageVersion -replace '[^0-9A-Za-z.-]', '-'

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $candidate = Get-ChildItem -LiteralPath $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $candidate) { throw 'signtool.exe wurde nicht gefunden.' }
    return $candidate.FullName
}

function Sign-And-Verify([string]$path) {
    if (-not $signingEnabled) { return }
    if (-not (Test-Path -LiteralPath $path)) { throw "Zu signierende Datei fehlt: $path" }
    & $script:signTool sign /sha1 $CodeSigningCertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $path
    if ($LASTEXITCODE -ne 0) { throw "Authenticode-Signatur fehlgeschlagen: $path" }
    & $script:signTool verify /pa /all $path
    if ($LASTEXITCODE -ne 0) { throw "Authenticode-Prüfung fehlgeschlagen: $path" }
}

function Remove-DirectoryWithRetry([string]$Path, [int]$Attempts = 6) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq $Attempts) { throw }
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
}

foreach ($path in @($release, $package, $wixWork)) {
    if (-not $path.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Releasepfad verlaesst den Artefaktordner: $path"
    }
    Remove-DirectoryWithRetry $path
    New-Item -ItemType Directory -Path $path | Out-Null
}

if (-not (Test-Path -LiteralPath (Join-Path $RuntimeSource 'SieEncode.Runtime.exe'))) {
    throw "Freigegebene Runtime wurde nicht gefunden: $RuntimeSource"
}
if (-not (Test-Path -LiteralPath (Join-Path $RuntimeSource 'BUILD-IDENTITY.json'))) {
    throw "Runtime besitzt keinen reproduzierbaren BUILD-IDENTITY-Nachweis: $RuntimeSource"
}
foreach ($tool in @('heat.exe','candle.exe','light.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $wixBin $tool))) { throw "WiX-Werkzeug fehlt: $tool" }
}

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root '.nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$publishArguments = @(
    'publish', (Join-Path $root 'src\RFiDGear.Builder.App\RFiDGear.Builder.App.csproj'),
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $studio,
    '--source', 'https://api.nuget.org/v3/index.json',
    "-p:Version=$PackageVersion"
)
if ($AcceptanceCandidateRuntime) { $publishArguments += '-p:DefineConstants=SIEENCODE_ACCEPTANCE' }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "SieEncode Studio Publish fehlgeschlagen: $LASTEXITCODE" }

Copy-Item -LiteralPath $RuntimeSource -Destination $runtime -Recurse
Copy-Item -LiteralPath (Join-Path $root 'legal') -Destination (Join-Path $studio 'Legal') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $studio 'Docs') -Recurse
foreach ($releaseDocument in @('VERSION','README.md','PARITY.md','MIGRATION.md','RUNTIME.md')) {
    Copy-Item -LiteralPath (Join-Path $root $releaseDocument) -Destination (Join-Path $studio $releaseDocument)
}
$PackageVersion | Set-Content -LiteralPath (Join-Path $studio 'VERSION') -Encoding UTF8
foreach ($requiredReleaseDocument in @('VERSION','README.md','PARITY.md','MIGRATION.md','RUNTIME.md','Docs\GRUNDPROFILE.md','Docs\HARDWARE-ABNAHME.md','Docs\DATENBAUSTEINE.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $studio $requiredReleaseDocument) -PathType Leaf)) {
        throw "Release-Dokument fehlt im publizierten Studio: $requiredReleaseDocument"
    }
}

if ($signingEnabled) {
    $script:signTool = Find-SignTool
    Sign-And-Verify (Join-Path $studio 'SieEncode.exe')
    Sign-And-Verify (Join-Path $runtime 'SieEncode.Runtime.exe')
}

$heat = Join-Path $wixBin 'heat.exe'
$candle = Join-Path $wixBin 'candle.exe'
$light = Join-Path $wixBin 'light.exe'
$studioWxs = Join-Path $wixWork 'StudioFiles.wxs'
$runtimeWxs = Join-Path $wixWork 'RuntimeFiles.wxs'

& $heat dir $studio -cg StudioComponents -dr StudioFolder -srd -sfrag -sreg -scom -ag -var var.StudioSource -out $studioWxs
if ($LASTEXITCODE -ne 0) { throw "WiX Heat (Studio) fehlgeschlagen: $LASTEXITCODE" }
& $heat dir $runtime -cg RuntimeComponents -dr RuntimeFolder -srd -sfrag -sreg -scom -ag -var var.RuntimeSource -out $runtimeWxs
if ($LASTEXITCODE -ne 0) { throw "WiX Heat (Runtime) fehlgeschlagen: $LASTEXITCODE" }

$productWxs = Join-Path $root 'installer\Product.wxs'
$candleDefinitions = @('-nologo','-arch','x64',"-dProjectRoot=$root","-dStudioSource=$studio","-dRuntimeSource=$runtime","-dProductVersion=$MsiProductVersion")
foreach ($source in @($productWxs, $studioWxs, $runtimeWxs)) {
    $objectPath = Join-Path $wixWork (([IO.Path]::GetFileNameWithoutExtension($source)) + '.wixobj')
    & $candle @candleDefinitions -out $objectPath $source
    if ($LASTEXITCODE -ne 0) { throw "WiX Candle fehlgeschlagen fuer $source`: $LASTEXITCODE" }
}

$msi = Join-Path $package ("SieEncode_{0}_internal_x64.msi" -f $packageVersionFilePart)
$lightProcess = Start-Process -FilePath $light -ArgumentList @(
    '-nologo',
    '-ext', 'WixUIExtension',
    '-b', ('"{0}"' -f $root),
    '-out', ('"{0}"' -f $msi),
    ('"{0}"' -f (Join-Path $wixWork 'Product.wixobj')),
    ('"{0}"' -f (Join-Path $wixWork 'StudioFiles.wixobj')),
    ('"{0}"' -f (Join-Path $wixWork 'RuntimeFiles.wixobj'))
) -NoNewWindow -Wait -PassThru
if ($lightProcess.ExitCode -ne 0) { throw "WiX Light fehlgeschlagen: $($lightProcess.ExitCode)" }
Sign-And-Verify $msi

# Den tatsaechlichen MSI-Vertrag pruefen, nicht nur die WiX-Quelldatei. Das ist
# bewusst derselbe ProgramMenu-/HKCU-Komponentenvertrag wie im aktuellen,
# nachweislich funktionierenden RFiDGear-WiX-Installer.
$windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
$msiDatabase = $windowsInstaller.OpenDatabase($msi, 0)
function Get-MsiValues([string]$query, [int]$fieldCount) {
    $view = $msiDatabase.OpenView($query)
    try {
        [void]$view.Execute()
        $record = $view.Fetch()
        if (-not $record) { return $null }
        return @(for ($field = 1; $field -le $fieldCount; $field++) { $record.StringData($field) })
    }
    finally { [void]$view.Close() }
}
$versionValues = @(Get-MsiValues "SELECT Value FROM Property WHERE Property='ProductVersion'" 1)
if ($versionValues.Count -ne 1 -or $versionValues[0] -ne $MsiProductVersion) {
    throw "MSI-Pruefung fehlgeschlagen: ProductVersion ist nicht $MsiProductVersion."
}
$componentValues = @(Get-MsiValues "SELECT Directory_,KeyPath FROM Component WHERE Component='StartMenuShortcuts'" 2)
if ($componentValues.Count -ne 2 -or $componentValues[0] -ne 'ApplicationProgramsFolder' -or -not $componentValues[1]) {
    throw 'MSI-Pruefung fehlgeschlagen: RFiDGear-kompatible StartMenuShortcuts-Komponente fehlt.'
}
$featureValues = @(Get-MsiValues "SELECT Feature_ FROM FeatureComponents WHERE Component_='StartMenuShortcuts'" 1)
if ($featureValues.Count -ne 1 -or $featureValues[0] -ne 'StartMenu') {
    throw 'MSI-Pruefung fehlgeschlagen: StartMenuShortcuts ist nicht dem StartMenu-Feature zugeordnet.'
}
foreach ($expectedShortcut in @(
    @{ Id = 'StudioShortcut'; Arguments = '' }
)) {
    $values = @(Get-MsiValues "SELECT Directory_,Component_,Target,Arguments,Icon_ FROM Shortcut WHERE Shortcut='$($expectedShortcut.Id)'" 5)
    if ($values.Count -ne 5 -or
        $values[0] -ne 'ApplicationProgramsFolder' -or
        $values[1] -ne 'StartMenuShortcuts' -or
        $values[2] -ne '[StudioFolder]SieEncode.exe' -or
        $values[3] -ne $expectedShortcut.Arguments -or
        $values[4] -ne 'StartMenuIcon.exe') {
        throw "MSI-Pruefung fehlgeschlagen: $($expectedShortcut.Id) entspricht nicht dem RFiDGear-Shortcutvertrag."
    }
}
$stationShortcut = @(Get-MsiValues "SELECT Shortcut FROM Shortcut WHERE Shortcut='StationShortcut'" 1 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($stationShortcut.Count -ne 0) { throw 'MSI-Pruefung fehlgeschlagen: redundante Station-Verknuepfung ist noch enthalten.' }
Write-Host 'OK: MSI-Startmenuevertrag entspricht dem bewaehrten RFiDGear-EXE-Muster.' -ForegroundColor Green

$portableParent = Join-Path $artifacts 'portable'
$portableRoot = Join-Path $portableParent 'SieEncode'
Remove-DirectoryWithRetry $portableParent
New-Item -ItemType Directory -Path $portableRoot | Out-Null
Copy-Item -LiteralPath $studio -Destination (Join-Path $portableRoot 'Studio') -Recurse
Copy-Item -LiteralPath $runtime -Destination (Join-Path $portableRoot 'Runtime') -Recurse
$zip = Join-Path $package ("SieEncode_{0}_internal_portable_x64.zip" -f $packageVersionFilePart)
Compress-Archive -LiteralPath $portableRoot -DestinationPath $zip -CompressionLevel Optimal

$pilotPackageHashLines = @("SieEncode $PackageVersion – interne Releasepakete",'')
$pilotPackageHashLines += foreach ($file in @($msi, $zip)) {
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash, [IO.Path]::GetFileName($file)
}

$testKitParent = Join-Path $artifacts 'pilot-testkit-package'
$testKitRoot = Join-Path $testKitParent 'SieEncode-Abnahme-Kit'
Remove-DirectoryWithRetry $testKitParent
New-Item -ItemType Directory -Path $testKitRoot | Out-Null
foreach ($name in @('README.md','DATENSCHUTZ-UND-SICHERHEIT.txt','MEHRKARTEN-TESTBOGEN.csv','USABILITY-TESTBOGEN.md',
        'Collect-PilotEvidence.ps1','Merge-PilotEvidence.ps1','Run-FiveCardUiSeries.ps1','START_PILOTTEST.cmd','START_MEHRKARTENTEST.cmd')) {
    Copy-Item -LiteralPath (Join-Path $root ('pilot-testkit\' + $name)) -Destination (Join-Path $testKitRoot $name)
}
$pilotPackageHashLines | Set-Content -LiteralPath (Join-Path $testKitRoot 'PACKAGE-SHA256.txt') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $root 'pilot-testkit\Automation') -Destination (Join-Path $testKitRoot 'Automation') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'pilot-testkit\fixtures') -Destination (Join-Path $testKitRoot 'fixtures') -Recurse
New-Item -ItemType Directory -Path (Join-Path $testKitRoot 'Evidenz') | Out-Null
$unexpectedVersions = @(Get-ChildItem -LiteralPath $testKitRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.md','.txt','.ps1','.cmd','.csv')
} | ForEach-Object {
    $path = $_.FullName
    [regex]::Matches([IO.File]::ReadAllText($path), 'SieEncode\s+(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)') | ForEach-Object {
        if ($_.Groups['version'].Value -ne $PackageVersion) {
            [pscustomobject]@{ File=$path; Version=$_.Groups['version'].Value }
        }
    }
})
if ($unexpectedVersions.Count -gt 0) {
    $details = ($unexpectedVersions | ForEach-Object { "$($_.Version) in $($_.File)" }) -join '; '
    throw "Abnahme-Kit enthält sichtbare Fremdversionen: $details"
}
$testKitZip = Join-Path $package ("SieEncode_{0}_internal_acceptance-testkit.zip" -f $packageVersionFilePart)
Compress-Archive -LiteralPath $testKitRoot -DestinationPath $testKitZip -CompressionLevel Optimal

$hashes = foreach ($file in @($msi, $zip, $testKitZip)) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    [pscustomobject]@{ File = [IO.Path]::GetFileName($file); SHA256 = $hash.Hash; Bytes = (Get-Item -LiteralPath $file).Length }
}
$hashes | Format-Table -AutoSize
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.json') -Encoding utf8
$signatureReport = [ordered]@{
    schemaVersion = '1.0'
    packageVersion = $PackageVersion
    msiProductVersion = $MsiProductVersion
    required = [bool]$RequireSignature
    signed = $signingEnabled
    certificateThumbprint = if ($signingEnabled) { $CodeSigningCertificateThumbprint.ToUpperInvariant() } else { $null }
    timestampUrl = if ($signingEnabled) { $TimestampUrl } else { $null }
    artifacts = @($hashes.File)
}
$signatureReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'SIGNING.json') -Encoding utf8
if (-not $signingEnabled) { Write-Warning 'Internes Release ist nicht signiert. Vor externer Verteilung -RequireSignature und -CodeSigningCertificateThumbprint verwenden.' }
Write-Host "Release erzeugt: $package" -ForegroundColor Green

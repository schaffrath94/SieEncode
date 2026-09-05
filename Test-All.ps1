[CmdletBinding()]
param(
    [string]$PowerShellBuilderPath = ''
)

$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$originalLocation = Get-Location
Set-Location -LiteralPath $base
$PowerShellBuilderPath = if ([string]::IsNullOrWhiteSpace($PowerShellBuilderPath)) { Join-Path (Split-Path -Parent $base) 'RFiDGear_Projektbuilder_v10.39' } else { $PowerShellBuilderPath }
$localRoot = [IO.Path]::GetFullPath($base)
$env:DOTNET_CLI_HOME = Join-Path $localRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $localRoot '.nuget-packages'
$env:APPDATA = Join-Path $localRoot '.appdata'
$env:LOCALAPPDATA = Join-Path $localRoot '.localappdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Invoke-DotNetChecked([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
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

Write-Host 'SieEncode Studio - automated tests' -ForegroundColor Cyan
Invoke-DotNetChecked @('build','tests\RFiDGear.Builder.Tests\RFiDGear.Builder.Tests.csproj','--configuration','Release','--configfile','NuGet.Config')
$profileDirectory = Join-Path $PowerShellBuilderPath 'profiles'
$projectTemplate = Join-Path $PowerShellBuilderPath 'projekt.config.template.json'
$taskShellDirectory = Join-Path $PowerShellBuilderPath 'task-shells'
$bundledProfileDirectory = Join-Path $base 'assets\profiles'
$bundledTaskShellDirectory = Join-Path $base 'assets\task-shells'
$assetRootPairs = @()
foreach ($assetRoot in $assetRootPairs) {
    $referenceRoot=[IO.Path]::GetFullPath([string]$assetRoot.Reference);$candidateRoot=[IO.Path]::GetFullPath([string]$assetRoot.Candidate)
    $referenceFiles=@(Get-ChildItem -LiteralPath $referenceRoot -Recurse -File)
    $candidateFiles=@(Get-ChildItem -LiteralPath $candidateRoot -Recurse -File)
    if($referenceFiles.Count -ne $candidateFiles.Count){throw "Bundled asset count differs: $candidateRoot"}
    foreach($referenceFile in $referenceFiles){
        $relative=$referenceFile.FullName.Substring($referenceRoot.Length+1);$candidate=Join-Path $candidateRoot $relative
        if(-not(Test-Path -LiteralPath $candidate)){throw "Bundled asset missing: $relative"}
        if((Get-FileHash -Algorithm SHA256 -LiteralPath $referenceFile.FullName).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash){throw "Bundled asset differs: $relative"}
    }
}
$legacyTaskShells=@(Get-ChildItem -LiteralPath $taskShellDirectory -File)
$bundledTaskShells=@(Get-ChildItem -LiteralPath $bundledTaskShellDirectory -File)
if($bundledTaskShells.Count -ne $legacyTaskShells.Count + 1){throw 'Bundled task-shell count must equal v10.39 plus CheckFileSettings.'}
foreach($referenceFile in $legacyTaskShells){
    $candidate=Join-Path $bundledTaskShellDirectory $referenceFile.Name
    if(-not(Test-Path -LiteralPath $candidate)){throw "Bundled legacy task shell missing: $($referenceFile.Name)"}
    if((Get-FileHash -Algorithm SHA256 -LiteralPath $referenceFile.FullName).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash){throw "Bundled legacy task shell differs: $($referenceFile.Name)"}
}
if(-not(Test-Path -LiteralPath (Join-Path $bundledTaskShellDirectory 'CheckFileSettings.xml'))){throw 'Bundled CheckFileSettings task shell is missing.'}
$bundledProjectTemplate = Join-Path $base 'assets\templates\projekt.config.template.json'
if (-not (Test-Path -LiteralPath $bundledProjectTemplate) -or
    (Get-FileHash -Algorithm SHA256 -LiteralPath $projectTemplate).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $bundledProjectTemplate).Hash) {
    throw 'Bundled project template differs from v10.39.'
}
Write-Host 'OK: internal profile fixtures remain test-only; legacy task shells and project fixture match v10.39' -ForegroundColor Green
Invoke-DotNetChecked @('run','--project','tests\RFiDGear.Builder.Tests\RFiDGear.Builder.Tests.csproj','--configuration','Release','--no-build','--no-restore','--',$profileDirectory,$projectTemplate,$bundledTaskShellDirectory)
Invoke-DotNetChecked @('build','src\RFiDGear.Builder.Cli\RFiDGear.Builder.Cli.csproj','--configuration','Release','--configfile','NuGet.Config')
$publishedCliDirectory = [IO.Path]::GetFullPath((Join-Path $base 'artifacts\cli'))
if (-not $publishedCliDirectory.StartsWith($localRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "CLI publish directory leaves the release root: $publishedCliDirectory"
}
Remove-DirectoryWithRetry $publishedCliDirectory
Invoke-DotNetChecked @('publish','src\RFiDGear.Builder.Cli\RFiDGear.Builder.Cli.csproj','--configuration','Release','--no-restore','--output',$publishedCliDirectory)
Invoke-DotNetChecked @('build','src\RFiDGear.Builder.App\RFiDGear.Builder.App.csproj','--configuration','Release','--configfile','NuGet.Config')
$appDll = Join-Path $base 'src\RFiDGear.Builder.App\bin\Release\net8.0-windows\SieEncode.dll'
Invoke-DotNetChecked @($appDll,'--self-test')
Invoke-DotNetChecked @($appDll,'--ui-smoke-test')
Write-Host 'OK: WPF navigation, all automation surfaces, isolated project flows and bundled runtime assets initialize successfully' -ForegroundColor Green
$publishedDirectory = Join-Path $base 'artifacts\app'
$publishedDirectoryFull = [IO.Path]::GetFullPath($publishedDirectory)
if (-not $publishedDirectoryFull.StartsWith($localRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish directory leaves the release root: $publishedDirectoryFull"
}
Remove-DirectoryWithRetry $publishedDirectoryFull
Invoke-DotNetChecked @('publish','src\RFiDGear.Builder.App\RFiDGear.Builder.App.csproj','--configuration','Release','--no-restore','--output',$publishedDirectoryFull)
$publishedApp = Join-Path $base 'artifacts\app\SieEncode.exe'
$launcher = Join-Path $base 'START_SieEncode.cmd'
if (-not (Test-Path -LiteralPath $publishedApp)) { throw "Published WPF application is missing: $publishedApp" }
if (Test-Path -LiteralPath (Join-Path $publishedDirectory 'assets\profiles')) {
    $shippedProfiles = @(Get-ChildItem -LiteralPath (Join-Path $publishedDirectory 'assets\profiles') -Filter '*.json' -Recurse -File)
    if ($shippedProfiles.Count -gt 0) { throw 'Published application contains forbidden fachliche Grund- oder Kundenprofile.' }
}
if (Test-Path -LiteralPath (Join-Path $publishedDirectory 'assets\templates\projekt.config.template.json')) {
    throw 'Published application contains the old Grundprofil-bound project template.'
}
$publishedProductVersion = (Get-Item -LiteralPath $publishedApp).VersionInfo.ProductVersion
[xml]$appProject = Get-Content -LiteralPath (Join-Path $base 'src\RFiDGear.Builder.App\RFiDGear.Builder.App.csproj') -Raw
$expectedProductVersion = [string]($appProject.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($expectedProductVersion) -or $publishedProductVersion -notlike ($expectedProductVersion + '*')) {
    throw "Published WPF application has the wrong product version: $publishedProductVersion (expected $expectedProductVersion)"
}
if (-not (Test-Path -LiteralPath $launcher)) { throw "Application launcher is missing: $launcher" }
$launcherBytes = [IO.File]::ReadAllBytes($launcher)
if ($launcherBytes.Length -ge 3 -and $launcherBytes[0] -eq 0xEF -and $launcherBytes[1] -eq 0xBB -and $launcherBytes[2] -eq 0xBF) { throw 'Application launcher contains a UTF-8 BOM.' }
if (-not ([IO.File]::ReadAllText($launcher).Contains('artifacts\app\SieEncode.exe'))) { throw 'Application launcher does not target the published WPF application.' }
Write-Host 'OK: published WPF application and BOM-free launcher are present' -ForegroundColor Green

$cli = Join-Path $base 'src\RFiDGear.Builder.Cli\bin\Release\net8.0\RFiDGear.Builder.Cli.dll'
$testHost = Join-Path $base 'tests\RFiDGear.Builder.Tests\bin\Release\net8.0\RFiDGear.Builder.Tests.dll'
$codingSpecificationProfile = Join-Path $profileDirectory 'customer\DEVK.json'
$legacyCodingSpecification = Join-Path $env:TEMP ('rfidgear-coding-spec-legacy-' + [Guid]::NewGuid().ToString('N') + '.txt')
$csharpCodingSpecification = Join-Path $env:TEMP ('rfidgear-coding-spec-csharp-' + [Guid]::NewGuid().ToString('N') + '.txt')
try {
    & 'powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PowerShellBuilderPath 'Export-Kodiervorschrift.ps1') -ProfileFile $codingSpecificationProfile -OutputPath $legacyCodingSpecification
    if ($LASTEXITCODE -ne 0) { throw 'v10.39 coding specification export failed.' }
    Invoke-DotNetChecked @($cli,'export-coding-specification',$codingSpecificationProfile,$csharpCodingSpecification)
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $legacyCodingSpecification).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $csharpCodingSpecification).Hash) {
        throw 'C# coding specification differs byte-for-byte from v10.39.'
    }
    $codingSpecificationText = Get-Content -Raw -Encoding UTF8 -LiteralPath $csharpCodingSpecification
    if ($codingSpecificationText -match '(?i)(productivePicc|applicationKeys|keyValue|ciphertext)') { throw 'Coding specification contains a secret-bearing field.' }
    Write-Host 'OK: C# coding specification export is byte-identical to v10.39 and contains no secret-bearing fields' -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $legacyCodingSpecification -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $csharpCodingSpecification -Force -ErrorAction SilentlyContinue
}
$template = $projectTemplate
if (-not (Test-Path $template)) { throw "v10.39 reference template not found: $template" }
Invoke-DotNetChecked @($cli,'validate-project',$template)
Write-Host 'OK: v10.39 project JSON is accepted by the C# core' -ForegroundColor Green

$helper = Join-Path $PowerShellBuilderPath 'RFiDGear-KeyVault.ps1'
if (-not (Test-Path $helper)) { throw "v10.39 vault helper not found: $helper" }
. $helper

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('rfidgear-v11-compat-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null
$payloadPath = Join-Path $tempRoot 'payload.json'
$psVaultPath = Join-Path $tempRoot 'powershell.keys.vault.json'
$csVaultPath = Join-Path $tempRoot 'csharp.keys.vault.json'
$fromPsPath = Join-Path $tempRoot 'from-powershell.json'
$multiUserPayloadPath = Join-Path $tempRoot 'multi-user-payload.json'
$multiUserVaultPath = Join-Path $tempRoot 'multi-user.keys.vault.json'
$multiUserAuditAPath = Join-Path $tempRoot 'multi-user-audit-a.json'
$multiUserAuditBPath = Join-Path $tempRoot 'multi-user-audit-b.json'
$passphrase = 'Cross-Runtime-Portable-Test'
$payloadJson = '{"productivePicc":{"type":"DF_KEY_AES","value":"102132435465768798A9BACBDCEDFE0F"},"piccKeySettings":"0F","applicationKeys":{"F482D0":["00112233445566778899AABBCCDDEEFF"]}}'

try {
    [IO.File]::WriteAllText($payloadPath, $payloadJson, (New-Object Text.UTF8Encoding($false)))

    # Keep the cross-runtime cryptography check isolated from the legacy project
    # generator scripts executed below. Both directions still exercise the real
    # v10.39 helper and the freshly built C# CLI.
    if ($passphrase -cne 'Cross-Runtime-Portable-Test') { throw 'Synthetic vault passphrase was unexpectedly modified by the test run.' }
    $psPayload = $payloadJson | ConvertFrom-Json
    $psVault = Protect-RFiDGearKeyVault -Payload $psPayload -Passphrase $passphrase
    $psSelfCheck = Unprotect-RFiDGearKeyVault -Vault $psVault -Passphrase $passphrase
    if ([string]$psSelfCheck.productivePicc.value -ne '102132435465768798A9BACBDCEDFE0F') { throw 'PowerShell v10.39 vault self-check failed.' }
    [IO.File]::WriteAllText($psVaultPath, ($psVault | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
    Invoke-DotNetChecked @($testHost,'vault-unprotect-contract',$psVaultPath,$fromPsPath)
    $fromPs = Get-Content -Raw -Encoding UTF8 $fromPsPath | ConvertFrom-Json
    if ([string]$fromPs.productivePicc.value -ne '102132435465768798A9BACBDCEDFE0F') { throw 'PowerShell -> C# payload mismatch.' }
    Write-Host 'OK: PowerShell v10.39 vault -> C# 11.0' -ForegroundColor Green

    Invoke-DotNetChecked @($testHost,'vault-protect-contract',$payloadPath,$csVaultPath)
    $csVault = Get-Content -Raw -Encoding UTF8 $csVaultPath | ConvertFrom-Json
    $fromCs = Unprotect-RFiDGearKeyVault -Vault $csVault -Passphrase $passphrase
    if ([string]$fromCs.applicationKeys.F482D0[0] -ne '00112233445566778899AABBCCDDEEFF') { throw 'C# -> PowerShell payload mismatch.' }
    Write-Host 'OK: C# 11.0 vault -> PowerShell v10.39' -ForegroundColor Green

    $multiUserPayloadJson = '{"productivePicc":{"type":"DF_KEY_AES","value":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"},"piccKeySettings":"0F","applicationKeys":{"F482D0":["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"],"F51773":["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"],"F52348":["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB","BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"]}}'
    [IO.File]::WriteAllText($multiUserPayloadPath, $multiUserPayloadJson, (New-Object Text.UTF8Encoding($false)))
    $passphrase | & dotnet $cli 'vault-protect' $multiUserPayloadPath $multiUserVaultPath
    if ($LASTEXITCODE -ne 0) { throw 'Portabler Test-Vault konnte nicht erzeugt werden.' }
    $passphrase | & dotnet $cli 'audit-project-vault' $projectTemplate $profileDirectory $multiUserVaultPath $multiUserAuditAPath
    if ($LASTEXITCODE -ne 0) { throw 'Erste unabhängige Vault-Prüfung ist fehlgeschlagen.' }
    $passphrase | & dotnet $cli 'audit-project-vault' $projectTemplate $profileDirectory $multiUserVaultPath $multiUserAuditBPath
    if ($LASTEXITCODE -ne 0) { throw 'Zweite unabhängige Vault-Prüfung ist fehlgeschlagen.' }
    $auditA = Get-Content -Raw -Encoding UTF8 -LiteralPath $multiUserAuditAPath | ConvertFrom-Json
    $auditB = Get-Content -Raw -Encoding UTF8 -LiteralPath $multiUserAuditBPath | ConvertFrom-Json
    if ([string]$auditA.status -ne 'OK' -or -not [bool]$auditA.portableAcrossUsers -or [string]$auditA.fileFingerprint -ne [string]$auditB.fileFingerprint -or [int]$auditA.keyCount -ne 10) {
        throw 'Zwei unabhängige Prozesse bestätigen den gemeinsamen portablen Vault nicht eindeutig.'
    }
    $auditCorpus = (Get-Content -Raw -Encoding UTF8 -LiteralPath $multiUserAuditAPath) + (Get-Content -Raw -Encoding UTF8 -LiteralPath $multiUserAuditBPath)
    if ($auditCorpus.Contains('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') -or $auditCorpus.Contains('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')) { throw 'Mehrbenutzer-Vault-Nachweis enthält Schlüsselwerte.' }
    Write-Host 'OK: derselbe Vault ist in zwei unabhängigen Prozessen portabel, vollständig und keyfrei geprüft' -ForegroundColor Green

    # Offline runtime diagnosis: only synthetic log files in the temporary test
    # directory are read. No installed RFiDGear process, reader or card is touched.
    $runtimeLogDirectory = Join-Path $tempRoot 'synthetic-rfidgear-log'
    [IO.Directory]::CreateDirectory($runtimeLogDirectory) | Out-Null
    $runtimeLogPath = Join-Path $runtimeLogDirectory 'log-synthetic.txt'
    $runtimeSnapshotPath = Join-Path $tempRoot 'runtime-log.snapshot.json'
    $runtimeDiagnosisPath = Join-Path $tempRoot 'runtime-log.diagnosis.json'
    [IO.File]::WriteAllText($runtimeLogPath, "baseline`r`n", (New-Object Text.UTF8Encoding($false)))
    Invoke-DotNetChecked @($cli,'snapshot-rfidgear-log',$runtimeSnapshotPath,$runtimeLogDirectory)
    [IO.File]::AppendAllText($runtimeLogPath,'[ERR] {"Stage":"TaskLoop.Failure","Exception":{"Message":"AuthFailure 00112233445566778899AABBCCDDEEFF"},"Details":{"CurrentTaskIndex":4}}'+"`r`n",(New-Object Text.UTF8Encoding($false)))
    & dotnet $cli 'diagnose-rfidgear-log' $runtimeSnapshotPath $runtimeDiagnosisPath
    $runtimeDiagnosisExit = $LASTEXITCODE
    $runtimeDiagnosis = Get-Content -Raw -Encoding UTF8 -LiteralPath $runtimeDiagnosisPath | ConvertFrom-Json
    $runtimeDiagnosisText = Get-Content -Raw -Encoding UTF8 -LiteralPath $runtimeDiagnosisPath
    if ($runtimeDiagnosisExit -ne 2 -or -not [bool]$runtimeDiagnosis.signal.strongFailure -or [string]$runtimeDiagnosis.signal.category -ne 'Authentifizierung / Key') { throw 'Offline RFiDGear runtime diagnosis did not identify the synthetic strong AuthFailure.' }
    if ($runtimeDiagnosisText.Contains('00112233445566778899AABBCCDDEEFF') -or -not $runtimeDiagnosisText.Contains('REDACTED_HEX_16B')) { throw 'Offline RFiDGear runtime diagnosis did not sanitize synthetic key material.' }
    Write-Host 'OK: C# runtime snapshot, strong-failure parsing and key sanitizing work offline' -ForegroundColor Green

    # Byte-for-byte payload parity against the productive v10.39 generator and
    # its copied Start-Kodierung.ps1. This creates files only; no RFiDGear process,
    # reader or card is accessed.
    $parityConfigPath = Join-Path $tempRoot 'PayloadParity.project.json'
    $parityValuesPath = Join-Path $tempRoot 'PayloadParity.values.json'
    $csharpPayloadDirectory = Join-Path $tempRoot 'csharp-payloads'
    $parityConfig = Get-Content -Raw -Encoding UTF8 $projectTemplate | ConvertFrom-Json
    $parityConfig.Project.Name = 'PayloadParity'
    $parityConfig.Project.OutputDirectory = $tempRoot
    $parityConfig.Project.Build = @('Mastering', 'Coding')
    $parityConfig.Profiles.Customers = @('DEVK')
    $parityConfig.Productive.PiccKey.value = 'A' * 32
    $parityConfig.Productive.ApplicationKeys.F482D0 = @(('B' * 32), ('B' * 32), ('B' * 32), ('B' * 32))
    $parityConfig.Productive.ApplicationKeys.F51773 = @(('B' * 32), ('B' * 32))
    $parityConfig.Productive.ApplicationKeys.F52348 = @(('B' * 32), ('B' * 32), ('B' * 32))
    $parityConfig.Productive.ApplicationKeys | Add-Member -NotePropertyName F482D5 -NotePropertyValue @(('B' * 32), ('B' * 32))
    $parityConfig.Values.firmCode = '1234567890'
    [IO.File]::WriteAllText($parityConfigPath, ($parityConfig | ConvertTo-Json -Depth 50), (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText($parityValuesPath, '{"personalNumber":"123456","version":"01","devkCardNumber":"789"}', (New-Object Text.UTF8Encoding($false)))
    & 'powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PowerShellBuilderPath 'New-RFiDGear-Projekte.ps1') -ConfigFile $parityConfigPath
    if ($LASTEXITCODE -ne 0) { throw 'v10.39 payload reference project could not be generated.' }
    $parityProjectDirectory = Join-Path $tempRoot 'PayloadParity'
    Invoke-DotNetChecked @($cli,'inspect-rfprj',(Join-Path $parityProjectDirectory 'PayloadParity_Kodierung.rfPrj'))
    $csharpSupportDirectory = Join-Path $tempRoot 'csharp-support-projects'
    Invoke-DotNetChecked @($cli,'build-support-projects',$profileDirectory,$bundledTaskShellDirectory,$parityConfigPath,$csharpSupportDirectory)
    foreach ($supportName in @(
        'PayloadParity_CardPreflight_Transport.rfPrj',
        'PayloadParity_CardPreflight_Productive.rfPrj',
        'PayloadParity_Recovery_Format_TransportPICC.rfPrj',
        'PayloadParity_Recovery_Format_ProductivePICC_ToTransport.rfPrj'
    )) {
        Invoke-DotNetChecked @($cli,'compare-rfprj',(Join-Path $parityProjectDirectory $supportName),(Join-Path $csharpSupportDirectory $supportName))
    }
    Write-Host 'OK: C# recovery and read-only card-preflight rfPrj files are semantically identical to v10.39' -ForegroundColor Green
    $csharpMainDirectory = Join-Path $tempRoot 'csharp-main-projects'
    Invoke-DotNetChecked @($cli,'build-main-projects',$profileDirectory,$bundledTaskShellDirectory,$parityConfigPath,$csharpMainDirectory)
    foreach ($mainName in @('PayloadParity_Mastering.rfPrj','PayloadParity_Kodierung.rfPrj')) {
        Invoke-DotNetChecked @($cli,'compare-rfprj',(Join-Path $parityProjectDirectory $mainName),(Join-Path $csharpMainDirectory $mainName))
    }
    Write-Host 'OK: C# mastering and coding rfPrj files are semantically identical to v10.39' -ForegroundColor Green
    $csharpContractDirectory = Join-Path $tempRoot 'csharp-contract-artifacts'
    Invoke-DotNetChecked @($cli,'build-contract-artifacts',$profileDirectory,$parityConfigPath,'1.0.1',$csharpContractDirectory)
    function ConvertTo-NormalizedContractJson([string]$Path) {
        $node = Get-Content -Raw -Encoding UTF8 -LiteralPath $Path | ConvertFrom-Json
        function Normalize-Node($Value) {
            if ($null -eq $Value) { return $null }
            if ($Value -is [pscustomobject]) {
                $ordered = [ordered]@{}
                foreach ($property in @($Value.PSObject.Properties | Sort-Object Name)) {
                    if ($property.Name -in @('generatedAt','builderVersion','recommendedRFiDGearVersion','applicationAcceptanceProject','applicationAcceptanceEvidence','fileSettingsCheckMode','cardRecognitionReadOnly')) { continue }
                    $normalizedValue = Normalize-Node $property.Value
                    if ($property.Name -eq 'sourceConfigPath') { $normalizedValue = [IO.Path]::GetFullPath([string]$normalizedValue).ToLowerInvariant() }
                    $ordered[$property.Name] = $normalizedValue
                }
                return [pscustomobject]$ordered
            }
            if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
                return @($Value | ForEach-Object { Normalize-Node $_ })
            }
            return $Value
        }
        return (Normalize-Node $node | ConvertTo-Json -Depth 100 -Compress)
    }
    $hostContractPath = Join-Path $csharpContractDirectory 'rfidgear.host-contract.json'
    if (-not (Test-Path -LiteralPath $hostContractPath)) { throw 'Versioned RFiDGear host contract is missing.' }
    $hostContract = Get-Content -Raw -Encoding UTF8 -LiteralPath $hostContractPath | ConvertFrom-Json
    $approvedBuilds = @($hostContract.safety.approvedKeyChangeBuilds)
    $legacyApproved = @($approvedBuilds | Where-Object { [string]$_.productVersion -eq '1.0.0+2d0429f2a26985a2d52a83caa493e619dc1c1cfc' })
    $rc2Approved = @($approvedBuilds | Where-Object { [string]$_.productVersion -eq '1.0.0-rc2+rfidgear.2.0.10.1' })
    $rc4Approved = @($approvedBuilds | Where-Object { [string]$_.productVersion -eq '1.0.0-rc4+rfidgear.2.0.10.3' })
    $rc15Approved = @($approvedBuilds | Where-Object { [string]$_.productVersion -eq '1.0.0-rc15+rfidgear.2.0.10.10' })
    $rc16Approved = @($approvedBuilds | Where-Object { [string]$_.productVersion -eq '1.0.0-rc16+rfidgear.2.0.10.11' })
    if ([string]$hostContract.schemaVersion -ne '1.1' -or [string]$hostContract.contractId -ne 'de.rfidgear.host.v1' -or
        [bool]$hostContract.privacy.requestContainsKeys -or [bool]$hostContract.privacy.resultContainsKeys -or
        [bool]$hostContract.privacy.requestContainsCardData -or [bool]$hostContract.privacy.resultContainsCardData -or
        -not [bool]$hostContract.safety.keyChangeRequiresPostAuthentication -or
        [string]$hostContract.safety.keyChangeReleasePolicy -notlike '*exact hardware-approved*' -or
        $approvedBuilds.Count -ne 5 -or $legacyApproved.Count -ne 1 -or $rc2Approved.Count -ne 1 -or $rc4Approved.Count -ne 1 -or $rc15Approved.Count -ne 1 -or $rc16Approved.Count -ne 1 -or
        [string]$legacyApproved[0].evidenceSha256 -ne '0208509E43AE8ADF93583948FC2660CE3D287558E86DA9093CB3CE962C496582' -or
        [string]$rc2Approved[0].evidenceSha256 -ne '627A96B8C67916B15BC02E0AC072CFBFD6797996A3096E5FF4E8A336B88E3907' -or
        [string]$rc4Approved[0].evidenceSha256 -ne '85590C40D9F3A9E64CE60F0699CBF8B60C413314517745BA38130677000A7669' -or
        [string]$rc15Approved[0].evidenceSha256 -ne '1F4085B22D56D6554BE70C4295B11F42221CDA981E3BC0B0029F92105152F4C9' -or
        [string]$rc16Approved[0].evidenceSha256 -ne 'ABB7CF36D36DDEF75EED4E33BD7BCBCEDB98EA3D51F480F1ED8DBEAFEEA05D20' -or
        -not [bool]$hostContract.resultSemantics.terminalEngineResultImmutable) {
        throw 'Versioned RFiDGear host contract violates schema, privacy or key-change safety.'
    }
    foreach ($schemaName in @('de.rfidgear.host.v1.request.schema.json','de.rfidgear.host.v1.result.schema.json')) {
        $schemaPath = Join-Path (Join-Path $base 'docs\contracts') $schemaName
        if (-not (Test-Path -LiteralPath $schemaPath)) { throw "Formal host JSON schema is missing: $schemaName" }
        $schema = Get-Content -Raw -Encoding UTF8 -LiteralPath $schemaPath | ConvertFrom-Json
        if ([string]$schema.'$schema' -ne 'https://json-schema.org/draft/2020-12/schema' -or -not ([string]$schema.'$id').EndsWith('/1.1')) {
            throw "Formal host JSON schema has the wrong draft or version: $schemaName"
        }
    }
    $acceptancePlan = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $csharpContractDirectory 'acceptance.plan.json') | ConvertFrom-Json
    if ([string]$acceptancePlan.applicationAcceptanceProject -ne 'PayloadParity_Acceptance_Application_Productive.rfPrj' -or
        [string]$acceptancePlan.applicationAcceptanceEvidence -ne 'acceptance/application-settings.result.json') {
        throw 'Application acceptance contract is missing or not project-local.'
    }
    foreach ($contractName in @('recovery.plan.json','card-preflight.plan.json','project.definition.json','builder.manifest.json','acceptance.plan.json')) {
        $referenceContract = Join-Path $parityProjectDirectory $contractName
        $candidateContract = Join-Path $csharpContractDirectory $contractName
        if (-not (Test-Path -LiteralPath $candidateContract)) { throw "C# contract artifact is missing: $contractName" }
        if ((ConvertTo-NormalizedContractJson $referenceContract) -cne (ConvertTo-NormalizedContractJson $candidateContract)) {
            throw "Contract artifact mismatch: $contractName"
        }
    }
    $referenceCodingSchema = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $parityProjectDirectory 'coding.schema.json') | ConvertFrom-Json
    $candidateCodingSchema = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $csharpContractDirectory 'coding.schema.json') | ConvertFrom-Json
    if ([string]$candidateCodingSchema.schemaVersion -ne '1.2' -or [string]$candidateCodingSchema.keySetIdentity -notmatch '^[0-9A-F]{64}$') {
        throw 'Additive coding schema does not contain a valid key-free keySetIdentity.'
    }
    $candidateCodingSchema.PSObject.Properties.Remove('keySetIdentity')
    $candidateCodingSchema.schemaVersion = '1.1'
    if (($referenceCodingSchema | ConvertTo-Json -Depth 100 -Compress) -cne ($candidateCodingSchema | ConvertTo-Json -Depth 100 -Compress)) {
        throw 'Coding schema differs from v10.39 beyond the additive keySetIdentity contract.'
    }
    $candidateManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $csharpContractDirectory 'builder.manifest.json') | ConvertFrom-Json
    $requiredVersion = [string]$candidateManifest.requiredRFiDGearVersion
    if ([string]$candidateManifest.recommendedRFiDGearVersion -ne '2.0.10.0' -or
        (-not [string]::IsNullOrWhiteSpace($requiredVersion) -and $requiredVersion -ne '2.0.6.0')) {
        throw 'RFiDGear version policy in builder.manifest.json is incorrect.'
    }
    $contractCorpus = @(Get-ChildItem -LiteralPath $csharpContractDirectory -Filter '*.json' -File | ForEach-Object { Get-Content -Raw -Encoding UTF8 -LiteralPath $_.FullName }) -join "`n"
    if ($contractCorpus.Contains(('A' * 32)) -or $contractCorpus.Contains(('B' * 32))) { throw 'C# contract artifacts contain productive test keys.' }
    Write-Host 'OK: five legacy C# contract artifacts remain identical; coding schema adds only the key-free Keyset-ID, Host V1.1 and card recognition' -ForegroundColor Green
    $csharpCompleteDirectory = Join-Path $tempRoot 'csharp-complete-project'
    Invoke-DotNetChecked @($cli,'build-support-projects',$profileDirectory,$bundledTaskShellDirectory,$parityConfigPath,$csharpCompleteDirectory)
    Invoke-DotNetChecked @($cli,'build-main-projects',$profileDirectory,$bundledTaskShellDirectory,$parityConfigPath,$csharpCompleteDirectory)
    Invoke-DotNetChecked @($cli,'build-contract-artifacts',$profileDirectory,$parityConfigPath,'1.0.1',$csharpCompleteDirectory)
    $preflightReport = Join-Path $tempRoot 'preflight.report.json'
    & dotnet $cli 'preflight-operational' $profileDirectory $parityConfigPath $csharpCompleteDirectory $preflightReport
    $preflightExitCode = $LASTEXITCODE
    $preflight = Get-Content -Raw -Encoding UTF8 -LiteralPath $preflightReport | ConvertFrom-Json
    if ($preflightExitCode -ne 0 -or [string]$preflight.status -ne 'OK' -or @($preflight.projects).Count -ne 8 -or @($preflight.issues).Count -ne 0) {
        @($preflight.issues) | ForEach-Object { Write-Host ("PREFLIGHT {0} {1}: {2}" -f $_.code,$_.file,$_.message) -ForegroundColor Red }
        throw 'C# preflight did not accept the complete compatible project.'
    }
    $taskMapPath = Join-Path $tempRoot 'task-map.json'
    Invoke-DotNetChecked @($cli,'export-task-map',$csharpCompleteDirectory,$taskMapPath)
    $taskMap = Get-Content -Raw -Encoding UTF8 -LiteralPath $taskMapPath | ConvertFrom-Json
    if (@($taskMap.projects).Count -ne 8) { throw 'Task map does not contain the six legacy rfPrj files plus application acceptance and card recognition.' }
    $taskMapText = Get-Content -Raw -Encoding UTF8 -LiteralPath $taskMapPath
    if ($taskMapText.Contains(('A' * 32)) -or $taskMapText.Contains(('B' * 32))) { throw 'Task map contains productive test keys.' }
    if (@($preflight.limitations | Where-Object { $_ -like "Unsicheres Altprojekt*" }).Count -ne 8) {
        throw 'Compatibility preflight did not identify all eight legacy plaintext rfPrj files.'
    }
    Write-Host 'OK: operational compatibility preflight accepts and explicitly flags all eight legacy plaintext projects; task map remains key-free' -ForegroundColor Green
    & 'powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $parityProjectDirectory 'Start-Kodierung.ps1') -ValuesFile $parityValuesPath -PayloadOnly -Quiet
    if ($LASTEXITCODE -ne 0) { throw 'v10.39 reference payloads could not be generated.' }
    Invoke-DotNetChecked @($cli,'build-payloads',$profileDirectory,$parityConfigPath,$parityValuesPath,$csharpPayloadDirectory)
    $referencePayloads = @(Get-ChildItem (Join-Path $parityProjectDirectory 'payloads') -Filter '*.hex' -File)
    if ($referencePayloads.Count -ne 4) { throw "Unexpected v10.39 payload count: $($referencePayloads.Count)" }
    foreach ($referencePayload in $referencePayloads) {
        $candidate = Join-Path $csharpPayloadDirectory $referencePayload.Name
        if (-not (Test-Path $candidate)) { throw "C# payload is missing: $($referencePayload.Name)" }
        $expectedText = (Get-Content -Raw -Encoding UTF8 $referencePayload.FullName).Trim()
        $actualText = (Get-Content -Raw -Encoding UTF8 $candidate).Trim()
        if ($actualText -cne $expectedText) { throw "Payload mismatch: $($referencePayload.Name)" }
    }
    Write-Host 'OK: all C# payloads are byte-for-byte identical to v10.39' -ForegroundColor Green

    # Card-preflight parity uses synthetic readback files only. Binary zero
    # content is intentionally included to prove that readability is based on
    # file existence and exact byte length, never on a non-zero heuristic.
    $cardPlan = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $parityProjectDirectory 'card-preflight.plan.json') | ConvertFrom-Json
    foreach ($file in @($cardPlan.files)) {
        $transportPath = Join-Path $parityProjectDirectory ([string]$file.transportOutput)
        $productivePath = Join-Path $parityProjectDirectory ([string]$file.productiveOutput)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $transportPath)) | Out-Null
        [IO.Directory]::CreateDirectory((Split-Path -Parent $productivePath)) | Out-Null
        [IO.File]::WriteAllBytes($transportPath, (New-Object byte[] ([int]$file.size)))
        if (-not [string]::IsNullOrWhiteSpace([string]$file.codingPayload)) {
            Copy-Item -LiteralPath (Join-Path $parityProjectDirectory ([string]$file.codingPayload)) -Destination $productivePath -Force
        } else {
            [IO.File]::WriteAllBytes($productivePath, (New-Object byte[] ([int]$file.size)))
        }
    }
    $probeMarker = '{"completedAt":"2026-08-13T12:00:00","outcome":"RFiDGearSuccess"}'
    [IO.File]::WriteAllText((Join-Path (Join-Path $parityProjectDirectory ([string]$cardPlan.transportDirectory)) '_probe.completed.json'),$probeMarker,(New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText((Join-Path (Join-Path $parityProjectDirectory ([string]$cardPlan.productiveDirectory)) '_probe.completed.json'),$probeMarker,(New-Object Text.UTF8Encoding($false)))
    $legacyCardResultPath = Join-Path $tempRoot 'legacy-card-preflight.result.json'
    & 'powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PowerShellBuilderPath 'Card-Preflight.ps1') -ProjectDirectory $parityProjectDirectory -ResultFile $legacyCardResultPath -Quiet
    if ($LASTEXITCODE -ne 0) { throw 'v10.39 card-preflight reference evaluation failed.' }
    Invoke-DotNetChecked @($cli,'card-preflight-evaluate',$parityProjectDirectory)
    $legacyCard = Get-Content -Raw -Encoding UTF8 -LiteralPath $legacyCardResultPath | ConvertFrom-Json
    $csharpCard = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $parityProjectDirectory 'card_preflight\card-preflight.result.json') | ConvertFrom-Json
    foreach ($probe in @($csharpCard.transport,$csharpCard.productive)) {
        foreach ($file in @($probe.files)) {
            $file.PSObject.Properties.Remove('hexValue')
            $file.PSObject.Properties.Remove('asciiValue')
        }
    }
    function Normalize-CardPreflight($Report) {
        return [pscustomobject][ordered]@{
            schemaVersion=[string]$Report.schemaVersion;projectName=[string]$Report.projectName;readOnly=[bool]$Report.readOnly
            expectedPayloadPreparedAt=[string]$Report.expectedPayloadPreparedAt
            classification=$Report.classification;recommendation=$Report.recommendation
            transport=$Report.transport;productive=$Report.productive;limitations=@($Report.limitations)
        } | ConvertTo-Json -Depth 20 -Compress
    }
    $legacyCardNormalized = Normalize-CardPreflight $legacyCard
    $csharpCardNormalized = Normalize-CardPreflight $csharpCard
    if ($legacyCardNormalized -cne $csharpCardNormalized) {
        Write-Host ('LEGACY CARD: '+$legacyCardNormalized) -ForegroundColor Yellow
        Write-Host ('CSHARP CARD: '+$csharpCardNormalized) -ForegroundColor Yellow
        throw 'C# card-preflight classification differs from v10.39.'
    }
    if ([int]$csharpCard.transport.readableCount -ne @($cardPlan.files).Count) { throw 'Binary zero readbacks were not counted solely by their exact byte length.' }
    Write-Host 'OK: C# card-preflight classification and reports are semantically identical to v10.39; zero-filled files are length-validated' -ForegroundColor Green

    # Acceptance parity uses only the payload and final verification files just
    # produced above. It does not start RFiDGear or access hardware.
    $verificationDirectory = Join-Path $parityProjectDirectory 'verification'
    [IO.Directory]::CreateDirectory($verificationDirectory) | Out-Null
    foreach ($referencePayload in $referencePayloads) {
        Copy-Item -LiteralPath $referencePayload.FullName -Destination (Join-Path $verificationDirectory $referencePayload.Name) -Force
    }
    $legacyAcceptanceDirectory = Join-Path $tempRoot 'legacy-acceptance'
    $csharpAcceptanceDirectory = Join-Path $tempRoot 'csharp-acceptance'
    $legacyAcceptanceResult = Join-Path $tempRoot 'legacy-acceptance-result.json'
    & 'powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PowerShellBuilderPath 'Pruefbericht.ps1') -ProjectDirectory $parityProjectDirectory -OutputDirectory $legacyAcceptanceDirectory -ReportPrefix 'Parity' -ResultFile $legacyAcceptanceResult -Quiet
    if ($LASTEXITCODE -ne 0) { throw 'v10.39 acceptance reference report could not be generated.' }
    Invoke-DotNetChecked @($cli,'acceptance-report',$parityProjectDirectory,$csharpAcceptanceDirectory)
    $legacyAcceptance = (Get-Content -Raw -Encoding UTF8 -LiteralPath $legacyAcceptanceResult | ConvertFrom-Json).report
    $csharpAcceptanceFile = @(Get-ChildItem -LiteralPath $csharpAcceptanceDirectory -Filter '*.json' -File | Sort-Object LastWriteTime -Descending)[0]
    $csharpAcceptance = Get-Content -Raw -Encoding UTF8 -LiteralPath $csharpAcceptanceFile.FullName | ConvertFrom-Json
    if ([string]$legacyAcceptance.result -ne [string]$csharpAcceptance.result -or
        [int]$legacyAcceptance.summary.ok -ne [int]$csharpAcceptance.summary.ok -or
        [int]$legacyAcceptance.summary.deviation -ne [int]$csharpAcceptance.summary.deviation -or
        [int]$legacyAcceptance.summary.notVerifiable -ne [int]$csharpAcceptance.summary.notVerifiable -or
        [int]$legacyAcceptance.summary.total -ne [int]$csharpAcceptance.summary.total) {
        throw 'C# acceptance summary differs from v10.39.'
    }
    function Normalize-AcceptanceChecks($Checks) {
        return @($Checks | Where-Object {
            [string]$_.area -ne 'Application'
        } | ForEach-Object { [pscustomobject][ordered]@{
            status=[string]$_.status;area=[string]$_.area;subject=[string]$_.subject;expected=[string]$_.expected;actual=[string]$_.actual;detail=[string]$_.detail;evidence=[string]$_.evidence
        } }) | ConvertTo-Json -Depth 6 -Compress
    }
    if ((Normalize-AcceptanceChecks $legacyAcceptance.checks) -cne (Normalize-AcceptanceChecks $csharpAcceptance.checks)) {
        throw 'C# acceptance checks differ from v10.39.'
    }
    $applicationContractChecks = @($csharpAcceptance.checks | Where-Object {
        [string]$_.area -eq 'Application' -and
        (([string]$_.subject).EndsWith('Keytyp') -or ([string]$_.subject).EndsWith('Keyanzahl') -or ([string]$_.subject).EndsWith('KeySettings'))
    })
    $applicationChecks = @($csharpAcceptance.checks | Where-Object { [string]$_.area -eq 'Application' })
    $badApplicationContractChecks = @($applicationContractChecks | Where-Object {
        [string]$_.status -eq 'OK' -or -not [string]::IsNullOrWhiteSpace([string]$_.actual) -or
        [string]$_.evidenceLevel -eq 'Operativ' -or [string]$_.evidenceLevel -eq 'Bytegenau'
    })
    if ($applicationChecks.Count -ne (@($acceptancePlan.applications).Count * 4) -or
        $applicationContractChecks.Count -ne (@($acceptancePlan.applications).Count * 3) -or
        $badApplicationContractChecks.Count -ne 0) {
        throw ("C# application acceptance checks are not fail-closed without current correlated RFiDGear evidence. applications={0}, contract={1}, expectedApplications={2}, bad={3}, subjects={4}" -f $applicationChecks.Count,$applicationContractChecks.Count,(@($acceptancePlan.applications).Count * 4),$badApplicationContractChecks.Count,(@($applicationChecks | ForEach-Object { ([string]$_.subject)+'/'+([string]$_.status)+'/'+([string]$_.evidenceLevel) }) -join '; '))
    }
    $csharpAcceptanceCorpus = (Get-ChildItem -LiteralPath $csharpAcceptanceDirectory -File | ForEach-Object { Get-Content -Raw -Encoding UTF8 -LiteralPath $_.FullName }) -join "`n"
    if ($csharpAcceptanceCorpus.Contains(('A' * 32)) -or $csharpAcceptanceCorpus.Contains(('B' * 32))) { throw 'C# acceptance report contains productive test keys.' }
    Write-Host 'OK: legacy acceptance checks remain semantically identical to v10.39; new Application checks are key-free and fail-closed without correlated evidence' -ForegroundColor Green

}
finally {
    Remove-DirectoryWithRetry $tempRoot
}

Write-Host "All automated SieEncode $expectedProductVersion tests passed. No card or reader was accessed." -ForegroundColor Green
Set-Location -LiteralPath $originalLocation

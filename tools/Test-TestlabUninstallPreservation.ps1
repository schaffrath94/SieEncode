# Run separately in the real, loaded profile of labadmin, testerA and testerB.
# This verifier never installs/uninstalls software or exports private keys.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][ValidateSet('Capture','VerifyRemoved','VerifyRestored','BeforeUpgrade','AfterUpgrade')][string]$Phase)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:COMPUTERNAME -ne 'SIEENCODE-LAB' -or $env:USERNAME -notin @('labadmin','testerA','testerB')) {
    throw 'Only the isolated SIEENCODE-LAB test profiles are allowed.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ($identity.Name.Split('\')[-1] -ne $env:USERNAME) { throw 'Token and profile do not match.' }
if (Get-Process -Name SieEncode -ErrorAction SilentlyContinue) { throw 'Close SieEncode before capturing or comparing data.' }
$expectedDll = '81F79933C67CC007B63A0A8C14F4E7AB939FBA0EDF369C4BADE07D807EBB0F38'
$installedDll = if ($Phase -eq 'AfterUpgrade') { 'C84C1772A6E3D6FD9E73A7DEB18A2069DAED4487A35B6BE0CEBAD71CA4661B39' } else { $expectedDll }
$installRoot = 'C:\Program Files\SieEncode'
$dll = Join-Path $installRoot 'Studio\SieEncode.dll'
$products = @(Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' |
    Where-Object { $_.PSObject.Properties['DisplayName'] -and $_.DisplayName -eq 'SieEncode' })
if ($Phase -eq 'VerifyRemoved') {
    if ($products.Count -ne 0 -or (Test-Path -LiteralPath $dll) -or
        (Test-Path -LiteralPath (Join-Path $installRoot 'Studio\SieEncode.exe')) -or
        (Test-Path -LiteralPath (Join-Path $installRoot 'Runtime\SieEncode.Runtime.exe'))) {
        throw 'Application removal is not complete.'
    }
} else {
    if ($products.Count -ne 1 -or $products[0].DisplayVersion -ne '1.3.0' -or
        (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $installedDll) {
        throw 'Expected installed test candidate identity is missing.'
    }
}
$evidence = Join-Path $env:LOCALAPPDATA 'SieEncodeLab\UninstallEvidence'
$baselinePath = Join-Path $evidence 'before.json'
if ($Phase -eq 'Capture' -and (Test-Path -LiteralPath $baselinePath)) { throw 'Existing baseline must not be overwritten.' }
if ($Phase -ne 'Capture' -and -not (Test-Path -LiteralPath $baselinePath)) { throw 'Capture must run first in this profile.' }
function Assert-RemovalEvidence($Removal, [string]$UserSid, [string]$CandidateHash, [string]$BaselineHash) {
    if ($Removal.Phase -ne 'VerifyRemoved' -or $Removal.UserSid -ne $UserSid -or
        $Removal.CandidateDllSHA256 -ne $CandidateHash -or $Removal.BaselineSHA256 -ne $BaselineHash) {
        throw 'Removal evidence does not match this profile, candidate and baseline.'
    }
}
$baselineHash = if ($Phase -ne 'Capture') { (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash } else { $null }
if ($Phase -eq 'AfterUpgrade') {
    $upgradeBefore = Get-Content -LiteralPath (Join-Path $evidence 'BeforeUpgrade.json') -Raw | ConvertFrom-Json
    if ($upgradeBefore.Phase -ne 'BeforeUpgrade' -or $upgradeBefore.UserSid -ne $identity.User.Value -or
        $upgradeBefore.CandidateDllSHA256 -ne $expectedDll -or $upgradeBefore.InstalledDllSHA256 -ne $expectedDll -or
        $upgradeBefore.BaselineSHA256 -ne $baselineHash) { throw 'Matching pre-upgrade evidence required.' }
}
if ($Phase -eq 'VerifyRestored') {
    $removedPath = Join-Path $evidence 'VerifyRemoved.json'
    if (-not (Test-Path -LiteralPath $removedPath)) {
        throw 'Successful removal verification is required before restoration verification.'
    }
    $removed = Get-Content -LiteralPath $removedPath -Raw | ConvertFrom-Json
    Assert-RemovalEvidence $removed $identity.User.Value $expectedDll $baselineHash
}
$roots = @(
    (Join-Path $env:LOCALAPPDATA 'SieEncode'),
    (Join-Path $env:APPDATA 'SieEncode'),
    (Join-Path $env:LOCALAPPDATA 'SieEncodeLab\VaultProbe1'),
    (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SieEncode-Projekte')
)
# The shared fixture ACL grants testerA/testerB and elevated administrators.
# Guest-control labadmin runs with a filtered token. Do not change that ACL or
# silently skip access failures: both ordinary users must verify the shared root.
if ($env:USERNAME -ne 'labadmin') { $roots += 'C:\SieEncodeLab\Shared\VaultProbe1' }
$files = @{}
foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse -Force) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected linked fixture file.' }
        $files[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}
if ($files.Count -eq 0) { throw 'No preservation fixtures found; empty evidence cannot pass.' }
$certificates = @()
$store = New-Object Security.Cryptography.X509Certificates.X509Store('My','CurrentUser')
try {
    $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    foreach ($certificate in $store.Certificates) {
        if (-not $certificate.HasPrivateKey) { continue }
        $private = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
        if ($null -eq $private) { continue }
        $public = $null
        try {
            $public = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
            $challenge = New-Object byte[] 32
            $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
            try { $rng.GetBytes($challenge) } finally { $rng.Dispose() }
            $encrypted = $public.Encrypt($challenge, [Security.Cryptography.RSAEncryptionPadding]::OaepSHA256)
            $decrypted = $private.Decrypt($encrypted, [Security.Cryptography.RSAEncryptionPadding]::OaepSHA256)
            if ([Convert]::ToBase64String($challenge) -cne [Convert]::ToBase64String($decrypted)) { throw 'Private key roundtrip failed.' }
            $certificates += $certificate.Thumbprint
        } finally {
            $private.Dispose()
            if ($null -ne $public) { $public.Dispose() }
        }
    }
} finally { $store.Close() }
if ($certificates.Count -eq 0) { throw 'No usable RSA identity found in this Windows profile.' }
$record = [ordered]@{ Phase=$Phase; UserSid=$identity.User.Value; CandidateDllSHA256=$expectedDll;
    InstalledDllSHA256=$installedDll; BaselineSHA256=$baselineHash; ScopeRoots=$roots; Files=$files; Certificates=@($certificates | Sort-Object); RecordedUtc=[DateTime]::UtcNow.ToString('o') }
if ($Phase -ne 'Capture') {
    $before = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($before.UserSid -ne $identity.User.Value -or $before.CandidateDllSHA256 -ne $expectedDll) { throw 'Baseline belongs to a different identity or candidate.' }
    if (@($before.Files.PSObject.Properties).Count -ne $files.Count) { throw 'Fixture file count changed.' }
    foreach ($entry in $before.Files.PSObject.Properties) {
        if (-not $files.ContainsKey($entry.Name) -or $files[$entry.Name] -ne $entry.Value) { throw 'Preserved fixture is missing or changed.' }
    }
    if (Compare-Object @($before.Certificates) @($certificates | Sort-Object)) { throw 'Private certificate inventory changed.' }
}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$destination = if ($Phase -eq 'Capture') { $baselinePath } else { Join-Path $evidence ($Phase + '.json') }
if (Test-Path -LiteralPath $destination) { throw 'Existing phase evidence must not be overwritten.' }
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $destination -Encoding UTF8
Write-Output "$Phase PASS: $($files.Count) fixture files and $($certificates.Count) usable RSA private identities; no full native or hardware acceptance."

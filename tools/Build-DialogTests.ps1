[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$target=Join-Path $repo 'artifacts/dialog-test-build'
Push-Location $repo
try {
    & dotnet publish src/RFiDGear.Builder.App/RFiDGear.Builder.App.csproj -c Release --no-restore -p:DialogTests=true -o $target
    if($LASTEXITCODE -ne 0){throw 'Dialog test build failed'}
    [ordered]@{schemaVersion=1; syntheticOnly=$true; dllSha256=(Get-FileHash -LiteralPath (Join-Path $target 'SieEncode.DialogTests.dll')).Hash} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'DIALOG-TEST-BUILD.json') -Encoding UTF8
} finally {Pop-Location}

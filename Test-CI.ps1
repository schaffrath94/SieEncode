[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:APPDATA = Join-Path $PSScriptRoot 'tmp/ci/appdata'
    $env:LOCALAPPDATA = Join-Path $PSScriptRoot 'tmp/ci/localappdata'
    $tests = 'tests/RFiDGear.Builder.Tests/RFiDGear.Builder.Tests.csproj'
    & dotnet build $tests -c Release --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    # This is an executable test harness; dotnet test alone does not execute it.
    & dotnet run --project $tests -c Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    foreach ($project in @('src/RFiDGear.Builder.Cli/RFiDGear.Builder.Cli.csproj', 'src/RFiDGear.Builder.App/RFiDGear.Builder.App.csproj', 'src/RFiDGear.Builder.SyncServer/RFiDGear.Builder.SyncServer.csproj')) {
        & dotnet build $project -c Release --configfile NuGet.Config
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
} finally { Pop-Location }

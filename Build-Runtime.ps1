[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputPath = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$builderRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$workspaceRoot = Split-Path -Parent $builderRoot
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $workspaceRoot 'SieEncode.Runtime'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $builderRoot 'artifacts/runtime'
}
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$project = Join-Path $SourceRoot 'RFiDGear\RFiDGear.csproj'
$tests = Join-Path $SourceRoot 'RFiDGear.Tests\RFiDGear.Tests.csproj'

if (-not (Test-Path -LiteralPath $project)) { throw "Runtime-Projekt fehlt: $project" }
if (-not (Test-Path -LiteralPath $tests)) { throw "Runtime-Tests fehlen: $tests" }
if (-not $OutputPath.StartsWith($workspaceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Runtime-Ausgabe muss innerhalb des Arbeitsbereichs liegen: $OutputPath"
}
if ($OutputPath.Equals($SourceRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Runtime-Ausgabe darf nicht das Quellverzeichnis sein.'
}

$env:DOTNET_CLI_HOME = Join-Path $builderRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $builderRoot '.nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

if (-not $SkipTests) {
    & dotnet test $tests --configuration Release --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw "Runtime-Tests fehlgeschlagen: $LASTEXITCODE" }
}

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath | Out-Null

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputPath `
    --source 'https://api.nuget.org/v3/index.json' `
    -p:AssemblyName=SieEncode.Runtime
if ($LASTEXITCODE -ne 0) { throw "Runtime-Publish fehlgeschlagen: $LASTEXITCODE" }

foreach ($name in @('LICENSE','runtime-defaults.sample.json')) {
    $source = Join-Path $SourceRoot $name
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $OutputPath $name)
    }
}

$exe = Join-Path $OutputPath 'SieEncode.Runtime.exe'
$dll = Join-Path $OutputPath 'SieEncode.Runtime.dll'
if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $dll)) {
    throw 'Runtime-Publish enthält nicht die erwartete SieEncode.Runtime.exe und DLL.'
}

$sourceCommit = & git -c "safe.directory=$SourceRoot" -C $SourceRoot rev-parse HEAD 2>$null
$sourceDirty = $false
if ($LASTEXITCODE -eq 0) {
    $sourceDirty = [bool](& git -c "safe.directory=$SourceRoot" -C $SourceRoot status --porcelain)
}
$identity = [ordered]@{
    schemaVersion = '1.0'
    createdUtc = [DateTime]::UtcNow.ToString('o')
    sourceRoot = $SourceRoot
    sourceCommit = if ($sourceCommit) { "$sourceCommit".Trim() } else { $null }
    sourceHadUncommittedChanges = $sourceDirty
    testsExecuted = -not [bool]$SkipTests
    executable = [ordered]@{
        file = 'SieEncode.Runtime.exe'
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash
        bytes = (Get-Item -LiteralPath $exe).Length
    }
    assembly = [ordered]@{
        file = 'SieEncode.Runtime.dll'
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
        bytes = (Get-Item -LiteralPath $dll).Length
    }
}
$identity | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputPath 'BUILD-IDENTITY.json') -Encoding UTF8

Write-Host "Runtime reproduzierbar erzeugt: $OutputPath" -ForegroundColor Green
$identity | ConvertTo-Json -Depth 5

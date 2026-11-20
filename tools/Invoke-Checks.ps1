[CmdletBinding()]
param(
    [ValidateSet('Core','RuntimeStartup','AppSelf','AppSmoke','AppDialogs')]
    [string[]]$Suite = @('Core','RuntimeStartup'),
    [string]$Executable = '',
    [switch]$Plan,
    [switch]$Force,
    [ValidateRange(1,3600)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 required for bounded process-tree cleanup.' }
$repo = Split-Path -Parent $PSScriptRoot
if ($Executable) { $Executable = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Executable) }
$runtime = Join-Path (Split-Path -Parent $repo) 'SieEncode.Runtime'
$evidence = Join-Path $repo 'artifacts/checks'
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
$runRoot = Join-Path $evidence $runId
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$shell = (Get-Process -Id $PID).Path
$runnerHash = (Get-FileHash -LiteralPath $PSCommandPath).Hash
$sdk = (& dotnet --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'dotnet SDK unavailable' }
$results = [Collections.Generic.List[object]]::new()
$runLock = [IO.File]::Open((Join-Path $evidence 'runner.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)

function Get-InputHash([string]$Name) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("$Name|$runnerHash|$sdk|$env:COMPUTERNAME|$env:USERNAME|$($PSVersionTable.PSVersion)|$([Environment]::OSVersion)")
    if ($Name -like 'App*') {
        if ($Name -eq 'AppDialogs') { $lines.Add((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Test-NativeDialogs.ps1')).Hash) }
        if (-not $Executable -or -not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'App suites require -Executable with an explicit SieEncode.exe path.' }
        $appRoot = Split-Path -Parent ([IO.Path]::GetFullPath($Executable))
        $lines.Add($appRoot)
        $files = @(Get-ChildItem -LiteralPath $appRoot -Recurse -File | Sort-Object FullName)
        foreach ($file in $files) { $lines.Add($file.FullName.Substring($appRoot.Length) + '|' + (Get-FileHash -LiteralPath $file.FullName).Hash) }
    } else {
        $inputRoot = if ($Name -eq 'Core') { $repo } else { $runtime }
        # Tracked source + untracked code; gitignored build outputs/data are excluded.
        $paths = @(& git -c "safe.directory=$inputRoot" -C $inputRoot ls-files --cached --others --exclude-standard)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source inputs' }
        foreach ($relative in ($paths | Sort-Object -Unique)) {
            if ($relative -match '^(docs|legal)/' -or $relative -match '\.(md|log)$') { continue }
            $path = Join-Path $inputRoot $relative
            $hash = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path).Hash } else { 'DELETED' }
            $lines.Add($relative + '|' + $hash)
        }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-','') }
    finally { $sha.Dispose() }
}

foreach ($name in ($Suite | Select-Object -Unique)) {
    $started = [DateTime]::UtcNow
    $result = [ordered]@{ suite=$name; status='FAILED'; startedUtc=$started.ToString('o'); inputHash=$null; exitCode=$null; log=$null; reusedFrom=$null; error=$null }
    try {
        $fingerprint = Get-InputHash $name
        $result.inputHash = $fingerprint
        $cacheFile = Join-Path $evidence ("passed-$name.json")
        $cached = if (Test-Path -LiteralPath $cacheFile) { Get-Content -LiteralPath $cacheFile -Raw | ConvertFrom-Json } else { $null }
        if ($Plan) { $result.status='PLANNED' }
        elseif (-not $Force -and $cached -and $cached.inputHash -eq $fingerprint -and $cached.status -eq 'PASSED' -and (Test-Path -LiteralPath $cached.log)) {
            $result.status='REUSED'; $result.reusedFrom=$cached.startedUtc; $result.log=$cached.log
        } else {
            $stage = Join-Path $runRoot $name
            New-Item -ItemType Directory -Path $stage | Out-Null
            $psi = [Diagnostics.ProcessStartInfo]::new()
            $psi.UseShellExecute=$false
            $psi.CreateNoWindow=$true
            $psi.RedirectStandardOutput=$true
            $psi.RedirectStandardError=$true
            $psi.WorkingDirectory=$repo
            foreach ($variable in @('TEMP','TMP','APPDATA','LOCALAPPDATA')) {
                $directory = Join-Path $stage $variable
                New-Item -ItemType Directory -Path $directory | Out-Null
                $psi.EnvironmentVariables[$variable]=$directory
            }
            $psi.EnvironmentVariables['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
            $psi.EnvironmentVariables['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1'
            $psi.EnvironmentVariables['DOTNET_CLI_HOME']=Join-Path $repo '.dotnet-home'
            if ($name -eq 'Core') {
                $psi.FileName=$shell
                $psi.Arguments='-NoProfile -NonInteractive -File "' + (Join-Path $repo 'Test-CI.ps1') + '"'
            } elseif ($name -eq 'RuntimeStartup') {
                $psi.FileName=(Get-Command dotnet).Source
                $psi.WorkingDirectory=$runtime
                $psi.Arguments='test RFiDGear.Tests/RFiDGear.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~UpdaterTests|FullyQualifiedName~StartupArgumentProcessorTests" --logger "console;verbosity=normal"'
            } else {
                # Never manipulate an existing user window; the application's test switches seed isolated fixtures.
                if (Get-Process -Name SieEncode -ErrorAction SilentlyContinue) { throw 'Close SieEncode before AppSelf/AppSmoke; no running user instance will be controlled.' }
                $psi.FileName=[IO.Path]::GetFullPath($Executable)
                $psi.Arguments=if ($name -eq 'AppSelf') {'--self-test'} else {'--ui-smoke-test'}
                if ($name -eq 'AppDialogs') {
                    $psi.FileName=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
                    $psi.EnvironmentVariables['PSModulePath']=(Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules') + ';' + (Join-Path $env:ProgramFiles 'WindowsPowerShell/Modules')
                    $psi.Arguments='-NoProfile -NonInteractive -File "' + (Join-Path $PSScriptRoot 'Test-NativeDialogs.ps1') + '" -Executable "' + [IO.Path]::GetFullPath($Executable) + '"'
                }
            }
            $process=[Diagnostics.Process]::new()
            $process.StartInfo=$psi
            try {
                if (-not $process.Start()) { throw 'Process start failed' }
                $out=$process.StandardOutput.ReadToEndAsync()
                $err=$process.StandardError.ReadToEndAsync()
                $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
                if ($timedOut) {
                    # Only the process tree created by this runner, never an existing application.
                    if ($PSVersionTable.PSVersion.Major -ge 7) { $process.Kill($true) } else { $process.Kill() }
                    $process.WaitForExit()
                }
                $result.exitCode=$process.ExitCode
                $result.log=Join-Path $stage 'process.log'
                [IO.File]::WriteAllText($result.log, $out.GetAwaiter().GetResult() + "`n" + $err.GetAwaiter().GetResult())
                if ($timedOut) { throw "Timeout after $TimeoutSeconds seconds" }
                if ($process.ExitCode -ne 0) { throw "Process exit $($process.ExitCode); see log" }
                if ($name -eq 'AppSmoke') {
                    $smokePath=Join-Path $stage 'TMP/SieEncode/ui-smoke/ui-smoke-result.json'
                    if (-not (Test-Path -LiteralPath $smokePath)) { throw 'Fresh UI smoke report missing' }
                    $smoke=Get-Content -LiteralPath $smokePath -Raw | ConvertFrom-Json
                    if ($smoke.status -ne 'OK') { throw 'UI smoke report did not pass' }
                }
                if ($name -eq 'AppDialogs') {
                    $dialogReport=Get-Content -LiteralPath (Join-Path $stage 'TMP/native-dialog-result.json') -Raw | ConvertFrom-Json
                    if ($dialogReport.status -ne 'PASSED' -or -not $dialogReport.actualExternalUiAutomation) { throw 'Native dialog report did not pass' }
                }
                # If sources changed during execution, don't create a reusable pass.
                if ((Get-InputHash $name) -ne $fingerprint) { throw 'Inputs changed during execution; result not reusable' }
                $result.status='PASSED'
                $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $cacheFile -Encoding UTF8
            } finally { $process.Dispose() }
        }
    } catch { $result.error=$_.Exception.Message }
    $result.elapsedSeconds=[Math]::Round(([DateTime]::UtcNow-$started).TotalSeconds,1)
    $results.Add([pscustomobject]$result)
    Write-Output ("{0}: {1}" -f $name,$result.status)
}
$report=[ordered]@{ schemaVersion=1; runId=$runId; results=@($results.ToArray()); excluded=@('Hardware/card actions','MSI install/upgrade','Cross-user and second-machine acceptance','Initial enrollment and delegation-grant dialogs'); }
$reportPath=Join-Path $runRoot 'summary.json'
$report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Output "Report: $reportPath"
$runLock.Dispose()
if (@($results | Where-Object status -eq 'FAILED').Count) { exit 1 }

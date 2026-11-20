#Requires -Version 5.1
<#
  Synthetic, read-only regression test for BridgeReadinessStatusParser.ps1.
  No RFiDGear installation, process, hardware, pipe, cards or credentials needed.
  Run manually on Windows: powershell -NoProfile -File .\tools\Test-BridgeReadinessStatusParser.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'BridgeReadinessStatusParser.ps1')

function Assert-True {
    param([bool] $Condition, [string] $Explanation)
    if (-not $Condition) { throw ('FAIL: ' + $Explanation) }
}

$required = 'session, status, project.load, project.revision, execution.run, execution.resultLookup, execution.journal, reader.scan, keys.provider, keys.context, execution.verificationDetails'
$goodLines = @(
    'RFiDGear 2.1.1.0, protocol 1.0, instance synthetic-instance',
    ('offers: ' + $required),
    'start-up complete: True, running: False, project: 2 tasks',
    'main window: answers',
    'reader: noReader, no card, latest result: 0'
)

$good = Get-BridgeReadinessFromStatus -Lines $goodLines -ExitCode 0
Assert-True $good.statusCommandSucceeded 'A successful status call is acknowledged'
Assert-True $good.readOnlyPreflightObserved 'All required capabilities, startup and UI must be observed'
Assert-True ($good.missingCapabilities.Count -eq 0) 'No missing capabilities on complete sample'
Assert-True ($good.keyContextOffered -and $good.verificationDetailsOffered -and $good.executionJournalOffered) 'Three recent guarantees were captured'
Assert-True (-not $good.productiveWritingApproved) 'Even complete status does not authorize card writing'

$legacyLines = @(
    'offers: session, status, project.load, execution.run, execution.resultLookup, reader.scan, keys.provider',
    'start-up complete: True, running: False, project: 1 tasks',
    'main window: answers'
)
$legacy = Get-BridgeReadinessFromStatus -Lines $legacyLines -ExitCode 0
Assert-True (-not $legacy.readOnlyPreflightObserved) 'Older bridge cannot pass new preflight'
foreach ($missing in @('project.revision','execution.journal','keys.context','execution.verificationDetails')) {
    Assert-True ($legacy.missingCapabilities -ccontains $missing) ('Missing required capability is reported: ' + $missing)
}

$failed = Get-BridgeReadinessFromStatus -Lines $goodLines -ExitCode 1
Assert-True (-not $failed.statusCommandSucceeded -and -not $failed.readOnlyPreflightObserved) 'A failing status command cannot give a green result'

$notStarted = Get-BridgeReadinessFromStatus -Lines (@($goodLines | Where-Object { $_ -notmatch '^start-up complete:' }) + @('start-up complete: False, running: False')) -ExitCode 0
Assert-True (-not $notStarted.readOnlyPreflightObserved) 'Host still starting is not ready'

$unresponsive = Get-BridgeReadinessFromStatus -Lines (@($goodLines | Where-Object { $_ -notmatch '^main window:' }) + @('main window: DOES NOT ANSWER, stale: True')) -ExitCode 0
Assert-True (-not $unresponsive.readOnlyPreflightObserved) 'Unresponsive host cannot pass'

$missingStatus = Get-BridgeReadinessFromStatus -Lines @('offers: ' + $required) -ExitCode 0
Assert-True (-not $missingStatus.readOnlyPreflightObserved) 'Missing status fields cannot pass'

$missingOffers = Get-BridgeReadinessFromStatus -Lines @('start-up complete: True', 'main window: answers') -ExitCode 0
Assert-True (-not $missingOffers.requiredCapabilitiesObserved) 'Missing offers list cannot pass'

$noisy = Get-BridgeReadinessFromStatus -Lines @(
    'offers: keys.context.extra, execution.verificationDetails2, secret://synthetic-test, C:\private\path, keys.context',
    'start-up complete: True',
    'main window: answers'
) -ExitCode 0
Assert-True (-not $noisy.verificationDetailsOffered) 'Substring match must not substitute for exact capability'
Assert-True ($noisy.capabilities -cnotcontains 'secret://synthetic-test') 'Secret-like strings are never returned'
Assert-True ($noisy.capabilities -cnotcontains 'C:\private\path') 'Paths are never returned'

$duplicates = Get-BridgeReadinessFromStatus -Lines @(
    'offers: session, session, status, status',
    'start-up complete: True',
    'main window: answers'
) -ExitCode 0
Assert-True ($duplicates.capabilities.Count -eq 2) 'Duplicate tokens are ignored'
Assert-True (-not $duplicates.productiveWritingApproved) 'No synthetic test can give writing approval'

Write-Output 'PASS: 9 synthetic bridge status scenarios; no RFiDGear process, card or reader used.'

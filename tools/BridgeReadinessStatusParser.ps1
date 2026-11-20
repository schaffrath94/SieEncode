#Requires -Version 5.1
<#
.SYNOPSIS
  Parse the official RFiDGear SampleClient "status" output without any host or card access.
.DESCRIPTION
  Pure function. Input is transient: never log or return raw lines, paths, card data or keys.
  A capability list is an observation, not proof of authorization or a hardware acceptance.
#>
function Get-BridgeReadinessFromStatus {
    [CmdletBinding()]
    param(
        [AllowEmptyCollection()][string[]] $Lines = @(),
        [int] $ExitCode
    )

    $found = New-Object 'System.Collections.Generic.List[string]'
    $startupComplete = $null
    $uiResponsive = $null
    foreach ($line in @($Lines)) {
        $lineText = [string] $line
        if ($lineText -cmatch '^offers:\s*(.*)$') {
            foreach ($entry in ($Matches[1] -split '\s*,\s*')) {
                $cap = $entry.Trim()
                if ($cap -cmatch '^[a-zA-Z0-9._-]{1,80}$' -and -not $found.Contains($cap)) {
                    [void] $found.Add($cap)
                }
            }
        }
        elseif ($lineText -cmatch '^start-up complete:\s*(True|False)(?:,|$)') {
            $startupComplete = ($Matches[1] -ceq 'True')
        }
        elseif ($lineText -cmatch '^main window:\s*(answers|DOES NOT ANSWER)') {
            $uiResponsive = ($Matches[1] -ceq 'answers')
        }
    }

    # Names verified against the upstream BridgeCapabilities contract on 2026-11-20.
    # Not a substitute for checking the exact supported task types and host build.
    $required = @(
        'session', 'status', 'project.load', 'project.revision',
        'execution.run', 'execution.resultLookup', 'execution.journal',
        'reader.scan', 'keys.provider', 'keys.context', 'execution.verificationDetails'
    )
    $missing = @($required | Where-Object { -not $found.Contains($_) })
    $commandSucceeded = ($ExitCode -eq 0)
    $capabilitiesObserved = ($commandSucceeded -and $missing.Count -eq 0)
    $statusUsable = ($commandSucceeded -and $startupComplete -eq $true -and $uiResponsive -eq $true)

    return [ordered]@{
        statusCommandSucceeded = $commandSucceeded
        startupComplete = $startupComplete
        uiResponsive = $uiResponsive
        capabilities = @($found.ToArray())
        executionRunOffered = $found.Contains('execution.run')
        projectRevisionOffered = $found.Contains('project.revision')
        keyProviderOffered = $found.Contains('keys.provider')
        keyContextOffered = $found.Contains('keys.context')
        verificationDetailsOffered = $found.Contains('execution.verificationDetails')
        executionJournalOffered = $found.Contains('execution.journal')
        requiredCapabilities = @($required)
        missingCapabilities = @($missing)
        requiredCapabilitiesObserved = $capabilitiesObserved
        # Only a software/status observation, never a release or card-writing approval.
        readOnlyPreflightObserved = ($statusUsable -and $capabilitiesObserved)
        productiveWritingApproved = $false
    }
}

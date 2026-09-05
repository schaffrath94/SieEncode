[CmdletBinding()]
param(
    [string]$Executable = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$ProjectName = 'RC6-Hardwaretest-ReadOnly',
    [ValidateSet('Recognition','Transport','Productive')]
    [string]$Mode = 'Recognition',
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-Element {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId = '',
        [string]$Name = '',
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$TimeoutMilliseconds = 15000
    )
    $conditions = New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
    if ($AutomationId) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId))) }
    if ($Name) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name))) }
    if ($ControlType) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType))) }
    $condition = if ($conditions.Count -eq 1) { $conditions[0] } else { New-Object System.Windows.Automation.AndCondition($conditions.ToArray()) }
    $until = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $until)
    throw "UI-Element nicht gefunden: AutomationId='$AutomationId', Name='$Name'."
}

function Invoke-Element([System.Windows.Automation.AutomationElement]$Element) {
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Read-Name([System.Windows.Automation.AutomationElement]$Root, [string]$AutomationId) {
    return (Find-Element -Root $Root -AutomationId $AutomationId -TimeoutMilliseconds 3000).Current.Name
}

if (-not (Test-Path -LiteralPath $Executable)) { throw "Installierte Anwendung fehlt: $Executable" }
$runtimeExecutable = Join-Path (Split-Path -Parent (Split-Path -Parent $Executable)) 'Runtime\SieEncode.Runtime.exe'
if (-not (Test-Path -LiteralPath $runtimeExecutable)) { throw "Installierte Runtime fehlt: $runtimeExecutable" }
$startedAt = [DateTimeOffset]::Now
$process = Start-Process -FilePath $Executable -ArgumentList '--station' -PassThru
$main = $null
try {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $processCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    $until = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $main = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children, $processCondition)
        if ($main) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    if (-not $main) { throw 'SieEncode-Hauptfenster wurde nicht sichtbar.' }

    # Der technische Detailstatus ist im bewusst reduzierten Normalmodus
    # ausgeblendet. Für den reproduzierbaren Abnahmelauf schalten wir ihn
    # sichtbar über dasselbe UI-Bedienelement ein, das auch ein Fachanwender
    # verwendet. So bleibt der Test vollständig auf dem installierten UI-Pfad.
    $expertToggle = Find-Element -Root $main -AutomationId 'Window.ExpertModeToggle'
    $togglePattern = $expertToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($togglePattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
        $togglePattern.Toggle()
    }

    Invoke-Element (Find-Element -Root $main -Name 'Projekt wählen' -ControlType ([System.Windows.Automation.ControlType]::Button))
    try { $grid = Find-Element -Root $main -AutomationId 'Page.Projects.ProjectsGrid' -TimeoutMilliseconds 3000 }
    catch { $grid = Find-Element -Root $main -ControlType ([System.Windows.Automation.ControlType]::DataGrid) -TimeoutMilliseconds 12000 }
    $rowCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
    $rows = $grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, $rowCondition)
    $row = @($rows) | Where-Object { $_.Current.Name -like "*$ProjectName*" } | Select-Object -First 1
    if (-not $row) { throw "Projekt '$ProjectName' ist in der sichtbaren Projektbibliothek nicht vorhanden." }
    $selection = $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selection.Select()

    Invoke-Element (Find-Element -Root $main -AutomationId 'Page.Projects.OpenStationButton' -ControlType ([System.Windows.Automation.ControlType]::Button))
    Find-Element -Root $main -AutomationId 'Page.Station.StationStateDetail' -TimeoutMilliseconds 15000 | Out-Null
    Invoke-Element (Find-Element -Root $main -AutomationId 'Page.Station.StationPreflightButton')
    $preflightText = Read-Name $main 'Page.Station.StationStateDetail'

    try {
        if ($Mode -eq 'Recognition') {
            $buttonId = 'Page.Station.StationRecognitionButton'
        }
        else {
            try {
                $details = Find-Element -Root $main -AutomationId 'Page.Station.StationDetailedPreflight' -ControlType ([System.Windows.Automation.ControlType]::Group) -TimeoutMilliseconds 3000
            }
            catch {
                $details = Find-Element -Root $main -Name 'Vertiefte Projektprüfung · Experten' -TimeoutMilliseconds 5000
            }
            try {
                $expand = $details.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
                if ($expand.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded) { $expand.Expand() }
            }
            catch { throw 'Die vertiefte Kartenprüfung konnte über die sichtbare Oberfläche nicht aufgeklappt werden.' }
            $buttonId = if ($Mode -eq 'Productive') { 'Page.Station.StationPreflightProductiveButton' } else { 'Page.Station.StationPreflightTransportButton' }
        }
        $cardButton = Find-Element -Root $main -AutomationId $buttonId -TimeoutMilliseconds 5000
    }
    catch {
        $guidance = try { Read-Name $main 'Page.Station.StationGuidanceNextAction' } catch { 'Keine Bedienempfehlung sichtbar.' }
        throw "Der lesende Kartenbereich wurde nach der Projektprüfung nicht freigegeben. Projektprüfung: $preflightText Nächster Schritt: $guidance"
    }
    $capabilityDeadline = [DateTime]::UtcNow.AddSeconds(90)
    while (-not $cardButton.Current.IsEnabled -and [DateTime]::UtcNow -lt $capabilityDeadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not $cardButton.Current.IsEnabled) {
        $operationInfo = try { Read-Name $main 'Page.Station.StationOperationInfo' } catch { 'Kein technischer Betriebsstatus sichtbar.' }
        throw "Der lesende Modus $Mode ist trotz bestandener Projektwahl nicht freigegeben. Betriebsstatus: $operationInfo"
    }
    Invoke-Element $cardButton
    # Der initiale Capability-Scan kann auf Pilot-PCs durch Virenscanner verzögert werden.
    $yes = Find-Element -Root $desktop -Name 'Ja' -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutMilliseconds 90000
    if ($yes.Current.ProcessId -ne $process.Id) { throw 'Der Bestätigungsdialog gehört nicht zum gestarteten SieEncode-Prozess.' }
    Invoke-Element $yes

    $terminal = $false
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $state = Read-Name $main 'Page.Station.StationState'
        if ($state -like 'KARTENSTATUS*' -or $state -like 'TRANSPONDER*' -or $state -like '*FEHLER*' -or $state -like '*TIMEOUT*') { $terminal = $true; break }
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $terminal) { throw "Der sichtbare Kartenlauf erreichte innerhalb von $TimeoutSeconds Sekunden keinen terminalen Zustand." }

    $result = [ordered]@{
        schemaVersion = '1.1'
        startedAt = $startedAt.ToString('O')
        completedAt = [DateTimeOffset]::Now.ToString('O')
        executable = $Executable
        installedProductVersion = (Get-Item -LiteralPath $Executable).VersionInfo.ProductVersion
        installedExeSha256 = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
        runtimeExeSha256 = (Get-FileHash -LiteralPath $runtimeExecutable -Algorithm SHA256).Hash
        project = $ProjectName
        action = if ($Mode -eq 'Recognition') { 'card-recognition-read-only' } else { 'card-preflight-' + $Mode.ToLowerInvariant() + '-read-only' }
        projectPreflight = $preflightText
        stationState = Read-Name $main 'Page.Station.StationState'
        stationDetail = Read-Name $main 'Page.Station.StationStateDetail'
        operationInfo = Read-Name $main 'Page.Station.StationOperationInfo'
        guidanceTitle = Read-Name $main 'Page.Station.StationGuidanceTitle'
        guidanceEvidence = Read-Name $main 'Page.Station.StationGuidanceEvidence'
        rfidgearProcessesAfterTerminal = @((Get-Process -Name 'SieEncode.Runtime','RFiDGear' -ErrorAction SilentlyContinue) | ForEach-Object { $_.Id })
    }
    $output = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\package\installed-readonly-ui-result.json'
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding UTF8
    $result | ConvertTo-Json -Depth 6
    if ($Mode -eq 'Recognition' -and ($result.stationState -notlike 'TRANSPONDER*' -or $result.operationInfo -notlike 'Applications *')) {
        throw 'Die UI zeigt nach der Erkennung keinen vollständigen Transponderstatus mit Application-Nachweis.'
    }
    if ($Mode -ne 'Recognition' -and $result.operationInfo -notlike '*automatisch beendet*') { throw 'Die UI meldet nach dem terminalen Lauf kein automatisch beendetes RFiDGear.' }
    if ($result.rfidgearProcessesAfterTerminal.Count -ne 0) { throw 'Nach dem terminalen UI-Lauf ist noch ein RFiDGear-Prozess aktiv.' }
}
finally {
    if ($main) {
        try { $main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch { }
    }
    if (-not $process.HasExited) { $process.WaitForExit(5000) | Out-Null }
}

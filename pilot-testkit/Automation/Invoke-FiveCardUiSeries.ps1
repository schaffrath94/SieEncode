[CmdletBinding()]
param(
    [ValidateSet('Start','ProcessCard','ProcessBlankCard','ResumeCard','FinalizeObservedCard','Finish')][string]$Action,
    [int]$CardIndex = 0,
    [string]$CardLabel = '',
    [string]$ProjectName = 'UI-Abnahme-RC56-20260826',
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$StartNumber = '910001',
    [int]$TimeoutSeconds = 240,
    [string]$EvidenceRoot = '',
    [string]$CardEvidenceRoot = '',
    [string]$CardCsvPath = '',
    [switch]$Authorized,
    [switch]$BlankCardConfirmed
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if (-not ('NativeDialogAutomation' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class NativeDialogAutomation
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hDlg, int id);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    public static IntPtr FindYesButton(int expectedProcessId)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr ignored) {
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId != (uint)expectedProcessId || !IsWindowVisible(window)) return true;
            var className = new StringBuilder(64);
            GetClassName(window, className, className.Capacity);
            if (!String.Equals(className.ToString(), "#32770", StringComparison.Ordinal)) return true;
            result = GetDlgItem(window, 6); // Win32 IDYES
            return result == IntPtr.Zero;
        }, IntPtr.Zero);
        return result;
    }

    public static void Click(IntPtr button)
    {
        if (button == IntPtr.Zero) throw new InvalidOperationException("Ja-Schaltfläche fehlt.");
        SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK
    }
}
'@
}
$workspace = Split-Path -Parent $PSScriptRoot
$evidenceRoot = if ($EvidenceRoot) { $EvidenceRoot } else { Join-Path $workspace 'artifacts\field-evidence\multicard' }
$statePath = Join-Path $evidenceRoot 'active-series.json'
$cardEvidenceRoot = if ($CardEvidenceRoot) { $CardEvidenceRoot } else { Join-Path $workspace 'pilot-testkit\Evidenz\cards' }
$cardCsvPath = if ($CardCsvPath) { $CardCsvPath } else { Join-Path $workspace 'pilot-testkit\MEHRKARTEN-TESTBOGEN.csv' }
New-Item -ItemType Directory -Path $evidenceRoot,$cardEvidenceRoot -Force | Out-Null

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $sha.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Find-Element {
    param([System.Windows.Automation.AutomationElement]$Root,[string]$AutomationId='',[string]$Name='',[System.Windows.Automation.ControlType]$ControlType,[int]$TimeoutMilliseconds=15000)
    $conditions=New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
    if($AutomationId){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$AutomationId)))}
    if($Name){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)))}
    if($ControlType){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,$ControlType)))}
    $condition=if($conditions.Count-eq1){$conditions[0]}else{New-Object System.Windows.Automation.AndCondition($conditions.ToArray())}
    $until=[DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do{$element=$Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($element){return $element};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow-lt$until)
    throw "UI-Element nicht gefunden: id='$AutomationId', name='$Name'."
}
function Invoke-Element([System.Windows.Automation.AutomationElement]$Element){$Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
function Read-Name($Root,[string]$Id){(Find-Element -Root $Root -AutomationId $Id -TimeoutMilliseconds 4000).Current.Name}
function Set-Value($Root,[string]$Id,[string]$Value){$e=Find-Element -Root $Root -AutomationId $Id;$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)}
function Select-Combo($Root,[string]$Id,[string]$ItemName){
    $combo=Find-Element -Root $Root -AutomationId $Id
    try{$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()}catch{}
    $item=Find-Element -Root $combo -Name $ItemName -TimeoutMilliseconds 7000
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    try{$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()}catch{}
}
function Confirm-Yes($Desktop,[int]$ProcessId,[int]$Count){
    $processCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
    for($i=0;$i-lt$Count;$i++){
        # Der erste Capability-Scan kann auf realen Pilot-PCs durch Virenscanner deutlich länger dauern.
        $until=[DateTime]::UtcNow.AddSeconds(90);$yes=$null;$observed=@()
        do{
            $nativeYes=[NativeDialogAutomation]::FindYesButton($ProcessId)
            if($nativeYes -ne [IntPtr]::Zero){[NativeDialogAutomation]::Click($nativeYes);$yes=$true;break}
            $elements=@($Desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants,$processCondition))
            $buttons=@($elements|Where-Object{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.IsEnabled})
            $observed=@($buttons|ForEach-Object{$_.Current.Name}|Where-Object{$_}|Select-Object -Unique)
            $yes=$buttons|Where-Object{(($_.Current.Name -replace '[&_]','').Trim() -match '^(Ja|Yes|Oui|Si)$')}|Select-Object -First 1
            if(-not$yes){
                $dialogButtons=@($buttons|Where-Object{$_.Current.Name -match 'Ja|Yes|Nein|No'})
                if($dialogButtons.Count-eq2){$yes=$dialogButtons|Sort-Object{$_.Current.BoundingRectangle.Left}|Select-Object -First 1}
            }
            if(-not$yes){Start-Sleep -Milliseconds 150}
        }while(-not$yes-and[DateTime]::UtcNow-lt$until)
        if(-not$yes){throw "Prozessgebundener Ja/Yes-Button im SieEncode-Bestätigungsdialog wurde nicht gefunden. Beobachtete Buttons: $($observed -join ' | ')"}
        if($yes -is [System.Windows.Automation.AutomationElement]){Invoke-Element $yes}
        Start-Sleep -Milliseconds 350
    }
}
function Get-Context {
    if(-not(Test-Path $statePath)){throw 'Aktive Fünf-Karten-Sitzung fehlt. Zuerst -Action Start ausführen.'}
    $state=Get-Content -Raw $statePath|ConvertFrom-Json
    $process=Get-Process -Id $state.processId -ErrorAction Stop
    if ([IO.Path]::GetFullPath($process.Path) -ne [IO.Path]::GetFullPath($state.installedExe)) { throw 'Aktive Anwendung ist nicht die beim Serienstart geprüfte Installation.' }
    if ((Get-Sha256 $process.Path) -ne $state.installedExeSha256) { throw 'Installierte SieEncode-Datei wurde seit dem Serienstart verändert.' }
    $desktop=[System.Windows.Automation.AutomationElement]::RootElement
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $main=$desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
    if(-not$main){throw 'SieEncode-Hauptfenster der aktiven Serie ist nicht sichtbar.'}
    [pscustomobject]@{State=$state;Process=$process;Desktop=$desktop;Main=$main}
}
function Expand-Advanced($Main){
    try{Find-Element -Root $Main -AutomationId 'Page.Station.StationMasteringAutoButton' -TimeoutMilliseconds 1000|Out-Null;return}catch{}
    $expander=Find-Element -Root $Main -Name 'Erweiterte Kartenaktionen · Mastering, Abnahme und Recovery' -TimeoutMilliseconds 5000
    $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Find-Element -Root $Main -AutomationId 'Page.Station.StationMasteringAutoButton' -TimeoutMilliseconds 5000|Out-Null
}
function Expand-DetailedPreflight($Main){
    try{Find-Element -Root $Main -AutomationId 'Page.Station.StationPreflightProductiveButton' -TimeoutMilliseconds 1000|Out-Null;return}catch{}
    $expander=Find-Element -Root $Main -Name 'Vertiefte Projektprüfung · Experten' -TimeoutMilliseconds 5000
    $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Find-Element -Root $Main -AutomationId 'Page.Station.StationPreflightProductiveButton' -TimeoutMilliseconds 5000|Out-Null
}
function Expand-Series($Main){
    try{Find-Element -Root $Main -AutomationId 'Page.Station.StationMode' -TimeoutMilliseconds 1000|Out-Null;return}catch{}
    $expander=Find-Element -Root $Main -Name '3 · Serienmodus (optional)' -TimeoutMilliseconds 5000
    $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Find-Element -Root $Main -AutomationId 'Page.Station.StationMode' -TimeoutMilliseconds 5000|Out-Null
}
function Expand-Completion($Main){
    try{Find-Element -Root $Main -AutomationId 'Page.Station.StationExportButton' -TimeoutMilliseconds 1000|Out-Null;return}catch{}
    $expander=Find-Element -Root $Main -Name '5 · Abschluss und Sitzungsverlauf' -TimeoutMilliseconds 5000
    $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Find-Element -Root $Main -AutomationId 'Page.Station.StationExportButton' -TimeoutMilliseconds 5000|Out-Null
}
function Wait-NoRuntime([int]$Seconds=20){
    $until=[DateTime]::UtcNow.AddSeconds($Seconds);do{if(@(Get-Process -Name 'SieEncode.Runtime','RFiDGear' -ErrorAction SilentlyContinue).Count-eq0){return};Start-Sleep -Milliseconds 300}while([DateTime]::UtcNow-lt$until);throw 'RFiDGear/Runtime blieb nach terminalem Lauf geöffnet.'
}
function Run-Operation($Context,[string]$ButtonId,[int]$Confirmations,[ValidateSet('Preflight','Operation')][string]$Kind){
    $main=$Context.Main;$before=Read-Name $main 'Page.Station.StationState';$beforeDetail=Read-Name $main 'Page.Station.StationStateDetail';$beforeInfo=Read-Name $main 'Page.Station.StationOperationInfo';$button=Find-Element -Root $main -AutomationId $ButtonId
    if(-not$button.Current.IsEnabled){throw "Aktion ist gesperrt: $ButtonId"}
    Invoke-Element $button
    if($Confirmations-gt0){Confirm-Yes $Context.Desktop $Context.Process.Id $Confirmations}
    $until=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds);$started=$false;$invokedAt=[DateTime]::UtcNow
    do{
        Start-Sleep -Milliseconds 500;$state=Read-Name $main 'Page.Station.StationState';$detail=Read-Name $main 'Page.Station.StationStateDetail';$info=Read-Name $main 'Page.Station.StationOperationInfo'
        $terminal=if($Kind-eq'Preflight'){$state-like'KARTENSTATUS*'}else{$state-like'RFIDGEAR-LAUF ERFOLGREICH*'-or$state-like'RFIDGEAR-LAUF FEHLER*'-or$state-like'RFIDGEAR-LAUF TIMEOUT*'}
        if(-not$terminal){$started=$true}
        if($terminal-and($started-or$state-ne$before-or$detail-ne$beforeDetail-or$info-ne$beforeInfo-or([DateTime]::UtcNow-$invokedAt).TotalSeconds-ge2)){Wait-NoRuntime;return [pscustomobject]@{State=$state;Detail=$detail;Info=$info}}
    }while([DateTime]::UtcNow-lt$until)
    throw "Aktion erreichte keinen terminalen Zustand: $ButtonId"
}
function Resolve-TransportPreflightButtonId($Main) {
    try {
        $legacy = Find-Element -Root $Main -AutomationId 'Page.Station.StationLegacyTransportButton' -TimeoutMilliseconds 800
        if ($legacy.Current.IsEnabled) { return 'Page.Station.StationLegacyTransportButton' }
    }
    catch { }
    return 'Page.Station.StationPreflightTransportButton'
}
function Write-CardCsv([string]$Label,[string]$Number,[string]$EvidenceRelative){
    $path=$cardCsvPath;$rows=@(Import-Csv -LiteralPath $path)
    $row=$rows | Where-Object { $_.Kartenlabel -eq $Label } | Select-Object -First 1
    if(-not$row){$row=$rows | Where-Object { $_.Freigabe_bestaetigt -eq 'Nein' } | Select-Object -First 1}
    if(-not$row){throw 'Keine freie Zeile im Mehrkarten-Testbogen.'}
    $row.Kartenlabel=$Label;$row.Freigabe_bestaetigt='Ja';$row.Serienwert=$Number;$row.Startzeit=[DateTimeOffset]::Now.ToString('O')
    $row.SieEncode_Ergebnis='KODIERUNG ERFOLGREICH BESTÄTIGT';$row.Kartenfreigabe='Ja';$row.Runtime_automatisch_beendet='Ja';$row.Evidenzdatei=$EvidenceRelative;$row.Bemerkung='Sichtbarer UI-Lauf: Recovery, Mastering, Kodierung und finale Verifikation bestanden.'
    $csv=$rows|ConvertTo-Csv -NoTypeInformation;$encoding=New-Object Text.UTF8Encoding($true);[IO.File]::WriteAllLines($path,$csv,$encoding)
}
function Save-CompletedCard($Context,[int]$Index,[string]$Label,[string]$Expected,$Checkpoint,[string]$CodingState,[string]$CodingDetail,[string]$CodingEvidence){
    $evidence=[ordered]@{schemaVersion='1.2';containsKeys=$false;cardLabel=$Label;seriesNumber=$Expected;completedAt=[DateTimeOffset]::Now.ToString('O');project=$Context.State.project;installedProductVersion=$Context.State.installedProductVersion;installedExeSha256=$Context.State.installedExeSha256;runtimeExeSha256=$Context.State.runtimeExeSha256;preflightClassification=$Checkpoint.preflightClassification;recovery=$Checkpoint.recovery;mastering=$Checkpoint.mastering;coding=@{state=$CodingState;detail=$CodingDetail;evidence=$CodingEvidence;runtimeClosed=$true}}
    $safe=($Label-replace'[^A-Za-z0-9_.-]','_');$evidenceFile=Join-Path $cardEvidenceRoot ($safe+'.json');$evidence|ConvertTo-Json -Depth 12|Set-Content -Encoding UTF8 $evidenceFile
    $relative='cards\'+$safe+'.json';Write-CardCsv $Label $Expected $relative
    $state=Get-Content -Raw $statePath|ConvertFrom-Json;$completed=@($state.completedCards)+@([pscustomobject]@{index=$Index;label=$Label;number=$Expected;evidence=$evidenceFile});$state.completedCards=$completed;$state|ConvertTo-Json -Depth 12|Set-Content -Encoding UTF8 $statePath
    $checkpointPath=Join-Path $evidenceRoot ("card-$Index-checkpoint.json");if(Test-Path $checkpointPath){Remove-Item -LiteralPath $checkpointPath -Force}
}
function Complete-Card($Context,[int]$Index,[string]$Label,[string]$Expected,$Checkpoint){
    Set-Value $Context.Main 'StationField.version' '01'
    Invoke-Element(Find-Element -Root $Context.Main -AutomationId 'Page.Station.StationPrepareButton')
    $until=[DateTime]::UtcNow.AddSeconds(120);$duplicateConfirmed=$false
    do{
        Start-Sleep -Milliseconds 250
        $prepared=Read-Name $Context.Main 'Page.Station.StationState'
        if($prepared-eq'SITZUNG BEREIT'){break}
        if(-not$duplicateConfirmed){
            $processCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$Context.Process.Id)
            $elements=@($Context.Desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants,$processCondition))
            $duplicate=$elements|Where-Object{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $_.Current.Name -eq 'Doppelte Nummer'}|Select-Object -First 1
            if($duplicate){
                $yes=$elements|Where-Object{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.AutomationId -eq '6' -and $_.Current.Name -match '^(Ja|Yes)$'}|Select-Object -First 1
                if(-not$yes){throw 'Doppelte Nummer erkannt, aber eindeutiger Ja-Button fehlt.'}
                Invoke-Element $yes;$duplicateConfirmed=$true
            }
        }
    }while([DateTime]::UtcNow-lt$until)
    if($prepared-ne'SITZUNG BEREIT'){throw "Datensatz wurde nicht vorbereitet. Letzter Zustand: $prepared"}
    Invoke-Element(Find-Element -Root $Context.Main -AutomationId 'Page.Station.StationStartAutoModeButton')
    $until=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do{Start-Sleep -Milliseconds 500;$codingState=Read-Name $Context.Main 'Page.Station.StationState';$guidanceTitle=Read-Name $Context.Main 'Page.Station.StationGuidanceTitle';if($codingState-like'KODIERUNG ERFOLGREICH BESTÄTIGT*'-or$guidanceTitle-like'KODIERUNG ERFOLGREICH BESTÄTIGT*'-or$codingState-like'KODIERUNG NICHT FREIGEGEBEN*'-or$codingState-like'NICHT FREIGEBEN*'-or$guidanceTitle-like'KODIERUNG NICHT FREIGEGEBEN*'-or$guidanceTitle-like'NICHT FREIGEBEN*'){break}}while([DateTime]::UtcNow-lt$until)
    Wait-NoRuntime
    if($guidanceTitle-notlike'KODIERUNG ERFOLGREICH BESTÄTIGT*'){throw "Kodierung nicht freigegeben: $codingState · $(Read-Name $Context.Main 'Page.Station.StationStateDetail')"}
    Save-CompletedCard $Context $Index $Label $Expected $Checkpoint $guidanceTitle (Read-Name $Context.Main 'Page.Station.StationGuidanceExplanation') (Read-Name $Context.Main 'Page.Station.StationGuidanceEvidence')
}

if($Action-eq'Start'){
    if(Test-Path $statePath){$old=Get-Content -Raw $statePath|ConvertFrom-Json;if(Get-Process -Id $old.processId -ErrorAction SilentlyContinue){throw 'Es läuft bereits eine Fünf-Karten-Sitzung.'}}
    if(@(Get-Process -Name 'SieEncode','SieEncode.Runtime','RFiDGear' -ErrorAction SilentlyContinue).Count-gt0){throw 'Vor dem Serienstart müssen SieEncode und RFiDGear geschlossen sein.'}
    if (-not (Test-Path -LiteralPath $InstalledExe -PathType Leaf)) { throw "Installiertes SieEncode fehlt: $InstalledExe" }
    $installedItem = Get-Item -LiteralPath $InstalledExe
    if ($installedItem.VersionInfo.ProductName -notlike 'SieEncode*') { throw "Unerwartetes installiertes Produkt: $($installedItem.VersionInfo.ProductName)" }
    if (-not (Test-Path -LiteralPath $cardCsvPath)) {
        @('Kartenlabel,Freigabe_bestaetigt,Serienwert,Startzeit,SieEncode_Ergebnis,Kartenfreigabe,Runtime_automatisch_beendet,Evidenzdatei,Bemerkung') +
            (1..5 | ForEach-Object { "TESTKARTE-{0:D2},Nein,,,,Nein,Nein,," -f $_ }) |
            Set-Content -LiteralPath $cardCsvPath -Encoding UTF8
    }
    $process=Start-Process -FilePath $InstalledExe -ArgumentList '--station' -PassThru
    $desktop=[System.Windows.Automation.AutomationElement]::RootElement;$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $until=[DateTime]::UtcNow.AddSeconds(30);do{$main=$desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition);if($main){break};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow-lt$until);if(-not$main){throw 'SieEncode-Hauptfenster fehlt.'}
    Invoke-Element(Find-Element -Root $main -Name 'Projekt wählen' -ControlType ([System.Windows.Automation.ControlType]::Button))
    $grid=Find-Element -Root $main -AutomationId 'Page.Projects.ProjectsGrid';$rowCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::DataItem)
    $row=@($grid.FindAll([System.Windows.Automation.TreeScope]::Descendants,$rowCondition))|Where-Object{$_.Current.Name-like"*$ProjectName*"}|Select-Object -First 1;if(-not$row){throw "Projekt fehlt: $ProjectName"};$row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Element(Find-Element -Root $main -AutomationId 'Page.Projects.OpenStationButton' -ControlType ([System.Windows.Automation.ControlType]::Button))
    Invoke-Element(Find-Element -Root $main -AutomationId 'Page.Station.StationPreflightButton')
    $preflightUntil=[DateTime]::UtcNow.AddSeconds(20)
    do{Start-Sleep -Milliseconds 200;$projectState=Read-Name $main 'Page.Station.StationState';if($projectState-eq'PROJEKT OK'-or$projectState-like'*FEHLER*'){break}}while([DateTime]::UtcNow-lt$preflightUntil)
    if($projectState-ne'PROJEKT OK'){throw "Projektprüfung ist nicht OK: $projectState"}
    Expand-Series $main
    Select-Combo $main 'Page.Station.StationMode' 'Fortlaufend'
    Find-Element -Root $main -AutomationId 'StationField.personalNumber' -TimeoutMilliseconds 5000 | Out-Null
    Set-Value $main 'Page.Station.StationSequenceValue' $StartNumber;Set-Value $main 'Page.Station.StationSequenceStep' '1';Set-Value $main 'StationField.version' '01'
    Expand-Advanced $main
    $runtimeExe=Join-Path (Split-Path -Parent (Split-Path -Parent $InstalledExe)) 'Runtime\SieEncode.Runtime.exe'
    if(-not(Test-Path -LiteralPath $runtimeExe -PathType Leaf)){throw "Installierte Runtime fehlt: $runtimeExe"}
    [ordered]@{schemaVersion='1.2';containsKeys=$false;processId=$process.Id;installedExe=$installedItem.FullName;installedProductVersion=$installedItem.VersionInfo.ProductVersion;installedExeSha256=(Get-Sha256 $InstalledExe);runtimeExeSha256=(Get-Sha256 $runtimeExe);project=$ProjectName;startNumber=$StartNumber;startedAt=[DateTimeOffset]::Now.ToString('O');completedCards=@()}|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 $statePath
    Write-Host "READY: SieEncode $($installedItem.VersionInfo.ProductVersion), PID $($process.Id), Projekt $ProjectName, nächste Nummer $StartNumber"
    return
}

$context=Get-Context
if($Action-eq'ProcessCard'){
    if (-not $Authorized) { throw 'Schreiblauf nicht autorisiert. Nur freigegebene Testtransponder mit -Authorized verarbeiten.' }
    if($CardIndex-lt1-or$CardIndex-gt5){throw 'CardIndex muss 1 bis 5 sein.'};if([string]::IsNullOrWhiteSpace($CardLabel)){throw 'CardLabel fehlt.'}
    if (@($context.State.completedCards | Where-Object { $_.index -eq $CardIndex -or $_.label -eq $CardLabel }).Count -gt 0) { throw "Karte/Index wurde in dieser Sitzung bereits abgeschlossen: $CardLabel / $CardIndex" }
    $seriesStart = [string]$context.State.startNumber
    $expected=([int]$seriesStart+$CardIndex-1).ToString().PadLeft($seriesStart.Length,'0')
    $actual=(Find-Element -Root $context.Main -AutomationId 'Page.Station.StationSequenceValue').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($actual -ne $expected) { throw "Erwartete Seriennummer $expected, UI zeigt $actual." }
    Set-Value $context.Main 'StationField.version' '01'
    Expand-DetailedPreflight $context.Main
    $transport=Run-Operation $context (Resolve-TransportPreflightButtonId $context.Main) 1 'Preflight'
    $productive=Run-Operation $context 'Page.Station.StationPreflightProductiveButton' 1 'Preflight'
    $classification=$productive.State
    $classificationCode = ($classification -replace '^KARTENSTATUS\s*·\s*','').Trim()
    $recoveryId = switch ($classificationCode) {
        'PRODUKTIV_OK' { 'Page.Station.StationRecoveryProductiveButton' }
        'PRODUKTIV_DATEN_ABWEICHUNG' { 'Page.Station.StationRecoveryProductiveButton' }
        'TRANSPORT_MASTERING_OK' { 'Page.Station.StationRecoveryTransportButton' }
        'UNBEKANNT_KEIN_ZUGRIFF' { 'Page.Station.StationRecoveryTransportButton' }
        default { throw "Kartenstatus ist für einen automatischen Schreiblauf nicht eindeutig: $classificationCode. Keine Recovery und keine Kodierung ausgeführt." }
    }
    $recovery=Run-Operation $context $recoveryId 2 'Operation'
    if($recovery.State-notlike'*ERFOLGREICH*'){throw "Recovery fehlgeschlagen: $($recovery.Detail)"}
    $mastering=Run-Operation $context 'Page.Station.StationMasteringAutoButton' 1 'Operation';if($mastering.State-notlike'*ERFOLGREICH*'){throw "Mastering fehlgeschlagen: $($mastering.Detail)"}
    $checkpoint=[ordered]@{schemaVersion='1.0';containsKeys=$false;cardLabel=$CardLabel;seriesNumber=$expected;preflightClassification=$classification;recovery=@{state=$recovery.State;detail=$recovery.Detail;project=$recoveryId};mastering=@{state=$mastering.State;detail=$mastering.Detail}}
    $checkpointPath=Join-Path $evidenceRoot ("card-$CardIndex-checkpoint.json");$checkpoint|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 $checkpointPath
    Complete-Card $context $CardIndex $CardLabel $expected $checkpoint
    Write-Host "SUCCESS: $CardLabel · $expected · Recovery/Mastering/Kodierung/Verifikation · Runtime geschlossen"
    return
}

if($Action-eq'ProcessBlankCard'){
    if (-not $Authorized -or -not $BlankCardConfirmed) { throw 'Blankokarten-Initialisierung verlangt Schreibfreigabe und ausdrückliche Bestätigung eines neuen leeren Transponders.' }
    if($CardIndex-lt1-or$CardIndex-gt5){throw 'CardIndex muss 1 bis 5 sein.'};if([string]::IsNullOrWhiteSpace($CardLabel)){throw 'CardLabel fehlt.'}
    if (@($context.State.completedCards | Where-Object { $_.index -eq $CardIndex -or $_.label -eq $CardLabel }).Count -gt 0) { throw "Karte/Index wurde in dieser Sitzung bereits abgeschlossen: $CardLabel / $CardIndex" }
    $seriesStart=[string]$context.State.startNumber;$expected=([int64]$seriesStart+$CardIndex-1).ToString().PadLeft($seriesStart.Length,'0')
    $actual=(Find-Element -Root $context.Main -AutomationId 'Page.Station.StationSequenceValue').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if($actual-ne$expected){throw "Erwartete Seriennummer $expected, UI zeigt $actual."}
    Set-Value $context.Main 'StationField.version' '01'
    Expand-DetailedPreflight $context.Main
    $probeStarted=[DateTimeOffset]::Now
    $transport=Run-Operation $context (Resolve-TransportPreflightButtonId $context.Main) 1 'Preflight'
    $productive=Run-Operation $context 'Page.Station.StationPreflightProductiveButton' 1 'Preflight'
    if ($transport.State -notlike 'KARTENSTATUS*PREFLIGHT_LAUFZEITFEHLER*' -or $productive.State -notlike 'KARTENSTATUS*PREFLIGHT_LAUFZEITFEHLER*') {
        throw "Blankokarten-Signatur ist nicht eindeutig: Transport '$($transport.State)', Produktiv '$($productive.State)'."
    }
    $projectPath=Read-Name $context.Main 'Page.Station.StationProjectPath';$runs=Join-Path $projectPath 'integration\host-v1\runs'
    $probeEvidence=@()
    foreach($operation in @('card-preflight-transport','card-preflight-productive')){
        $request=Get-ChildItem -LiteralPath $runs -Filter '*.request.json' | Where-Object { $_.LastWriteTimeUtc -ge $probeStarted.UtcDateTime } | Sort-Object LastWriteTime -Descending | Where-Object { (Get-Content -Raw $_.FullName | ConvertFrom-Json).operation -eq $operation } | Select-Object -First 1
        if(-not$request){throw "Blankokarten-Nachweis fehlt: $operation wurde nicht dieser Aktion zugeordnet."}
        $requestJson=Get-Content -Raw $request.FullName|ConvertFrom-Json;$resultPath=Join-Path $runs (($request.BaseName-replace'\.request$','.result')+'.json');if(-not(Test-Path $resultPath)){throw "Blankokarten-Nachweis fehlt: Resultat für $operation."};$result=Get-Content -Raw $resultPath|ConvertFrom-Json
        $outcomes=@($result.taskOutcomes);$invalid=@($outcomes | Where-Object { $_.type -ne 'ReadData' -or $_.decision -ne 'Executed' -or $_.outcome -ne 'Failed' -or $_.errorCode -ne 'TransportError' })
        if ($result.state -ne 'failed' -or $result.terminalEvent -ne 'TaskLoop.Failure' -or $outcomes.Count -ne [int]$requestJson.expectedTaskCount -or $outcomes.Count -ne 6 -or $invalid.Count -ne 0) {
            throw "$operation entspricht nicht der engen Blankokarten-Signatur aus 6 ausgeführten ReadData/TransportError-Tasks."
        }
        $probeEvidence+=@([pscustomobject]@{operation=$operation;request=$request.FullName;result=$resultPath;taskPattern='6/6 ReadData · Executed · Failed · TransportError'})
    }
    $recovery=Run-Operation $context 'Page.Station.StationRecoveryTransportButton' 2 'Operation'
    if ($recovery.State -notlike '*ERFOLGREICH*') { throw "Blankokarten-Formatierung fehlgeschlagen: $($recovery.Detail)" }
    $mastering=Run-Operation $context 'Page.Station.StationMasteringAutoButton' 1 'Operation';if ($mastering.State -notlike '*ERFOLGREICH*') { throw "Mastering fehlgeschlagen: $($mastering.Detail)" }
    $checkpoint=[ordered]@{schemaVersion='1.1';containsKeys=$false;cardLabel=$CardLabel;seriesNumber=$expected;preflightClassification='BLANKOKARTE_BESTAETIGT · Reader/Chip erkannt · Projekt-AIDs unter beiden Keysets nicht vorhanden';blankCardProbe=$probeEvidence;recovery=@{state=$recovery.State;detail=$recovery.Detail;project='Page.Station.StationRecoveryTransportButton'};mastering=@{state=$mastering.State;detail=$mastering.Detail}}
    $checkpointPath=Join-Path $evidenceRoot ("card-$CardIndex-checkpoint.json");$checkpoint|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 $checkpointPath
    Complete-Card $context $CardIndex $CardLabel $expected $checkpoint
    Write-Host "SUCCESS: $CardLabel · $expected · Blankokarte/Formatierung/Mastering/Kodierung/Verifikation · Runtime geschlossen"
    return
}

if($Action-eq'ResumeCard'){
    if(-not$Authorized){throw 'Wiederaufnahme nicht autorisiert.'}
    if($CardIndex-lt1-or$CardIndex-gt5){throw 'CardIndex muss 1 bis 5 sein.'};if([string]::IsNullOrWhiteSpace($CardLabel)){throw 'CardLabel fehlt.'}
    $seriesStart=[string]$context.State.startNumber;$expected=([int64]$seriesStart+$CardIndex-1).ToString().PadLeft($seriesStart.Length,'0')
    $actual=(Find-Element -Root $context.Main -AutomationId 'Page.Station.StationSequenceValue').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if($actual-ne$expected){throw "Erwartete Seriennummer $expected, UI zeigt $actual."}
    $checkpointPath=Join-Path $evidenceRoot ("card-$CardIndex-checkpoint.json")
    if(Test-Path $checkpointPath){$checkpoint=Get-Content -Raw $checkpointPath|ConvertFrom-Json}
    else{
        if((Read-Name $context.Main 'Page.Station.StationState')-ne'RFIDGEAR-LAUF ERFOLGREICH'){throw 'Wiederaufnahme ohne Checkpoint nur direkt nach erfolgreichem Mastering erlaubt.'}
        $projectPath=Read-Name $context.Main 'Page.Station.StationProjectPath';$runs=Join-Path $projectPath 'integration\host-v1\runs'
        $request=Get-ChildItem $runs -Filter '*.request.json'|Sort-Object LastWriteTime -Descending|Where-Object{(Get-Content -Raw $_.FullName|ConvertFrom-Json).operation -eq 'mastering'}|Select-Object -First 1
        if(-not$request){throw 'Mastering-Hostvertrag für Wiederaufnahme fehlt.'}
        $resultPath=Join-Path $runs (($request.BaseName -replace '\.request$','.result')+'.json');$result=Get-Content -Raw $resultPath|ConvertFrom-Json
        if($result.state -ne 'successful' -or $result.terminalEvent -ne 'TaskLoop.Success'){throw 'Letzter Mastering-Hostvertrag ist nicht erfolgreich.'}
        $checkpoint=[pscustomobject]@{preflightClassification='siehe card-preflight.result.json';recovery=@{state='RFIDGEAR-LAUF ERFOLGREICH';detail='Vor Wiederaufnahme erfolgreich abgeschlossen';project='siehe Hostvertrag'};mastering=@{state='RFIDGEAR-LAUF ERFOLGREICH';detail=$result.summary;request=$request.FullName;result=$resultPath}}
    }
    Complete-Card $context $CardIndex $CardLabel $expected $checkpoint
    Write-Host "SUCCESS: $CardLabel · $expected · Wiederaufnahme nach nachgewiesenem Mastering/Kodierung/Verifikation · Runtime geschlossen"
    return
}

if($Action-eq'FinalizeObservedCard'){
    if(-not$Authorized){throw 'Nachträgliche Evidenzerfassung nicht autorisiert.'}
    if($CardIndex-lt1-or$CardIndex-gt5){throw 'CardIndex muss 1 bis 5 sein.'};if([string]::IsNullOrWhiteSpace($CardLabel)){throw 'CardLabel fehlt.'}
    if (@($context.State.completedCards | Where-Object { $_.index -eq $CardIndex -or $_.label -eq $CardLabel }).Count -gt 0) { throw "Karte/Index wurde in dieser Sitzung bereits abgeschlossen: $CardLabel / $CardIndex" }
    $seriesStart=[string]$context.State.startNumber;$expected=([int64]$seriesStart+$CardIndex-1).ToString().PadLeft($seriesStart.Length,'0')
    $nextExpected=([int64]$expected+1).ToString().PadLeft($expected.Length,'0')
    $actual=(Find-Element -Root $context.Main -AutomationId 'Page.Station.StationSequenceValue').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($actual -ne $nextExpected) { throw "Nach einem erfolgreichen Lauf wurde die nächste Seriennummer $nextExpected erwartet, die UI zeigt $actual." }
    Wait-NoRuntime -Seconds 1
    $guidanceTitle=Read-Name $context.Main 'Page.Station.StationGuidanceTitle';$guidanceEvidence=Read-Name $context.Main 'Page.Station.StationGuidanceEvidence'
    if ($guidanceTitle -notlike 'KODIERUNG ERFOLGREICH BESTÄTIGT*' -or $guidanceEvidence -notmatch '3/3 bytegleich' -or $guidanceEvidence -notmatch '26/26 Tasks' -or $guidanceEvidence -notmatch 'TaskLoop.Success' -or $guidanceEvidence -notmatch 'Korrelation OK' -or $guidanceEvidence -notmatch 'Runtime beendet') {
        throw 'Die sichtbare UI enthält keinen vollständigen Kartenfreigabenachweis für den unmittelbar vorherigen Lauf.'
    }
    $checkpointPath=Join-Path $evidenceRoot ("card-$CardIndex-checkpoint.json");if(-not(Test-Path $checkpointPath)){throw 'Checkpoint vor dem Kodierstart fehlt.'}
    $checkpoint=Get-Content -Raw $checkpointPath|ConvertFrom-Json
    if ([string]$checkpoint.cardLabel -ne $CardLabel -or [string]$checkpoint.seriesNumber -ne $expected) { throw 'Checkpoint passt nicht eindeutig zu Karte und Seriennummer.' }
    $projectPath=Read-Name $context.Main 'Page.Station.StationProjectPath';$runs=Join-Path $projectPath 'integration\host-v1\runs';$startedAt=[DateTimeOffset]$context.State.startedAt
    $request=Get-ChildItem -LiteralPath $runs -Filter '*.request.json'|Where-Object{$_.LastWriteTimeUtc-ge$startedAt.UtcDateTime}|Sort-Object LastWriteTime -Descending|Where-Object{(Get-Content -Raw $_.FullName|ConvertFrom-Json).operation -eq 'coding'}|Select-Object -First 1
    if(-not$request){throw 'Korrelierter Kodier-Hostvertrag dieser sichtbaren Sitzung fehlt.'}
    $requestJson=Get-Content -Raw $request.FullName|ConvertFrom-Json;$resultPath=Join-Path $runs (($request.BaseName-replace'\.request$','.result')+'.json');if(-not(Test-Path $resultPath)){throw 'Kodier-Hostresultat fehlt.'};$result=Get-Content -Raw $resultPath|ConvertFrom-Json
    if ($result.state -ne 'successful' -or $result.terminalEvent -ne 'TaskLoop.Success' -or @($result.taskOutcomes).Count -ne [int]$requestJson.expectedTaskCount -or @($result.taskOutcomes | Where-Object { $_.outcome -ne 'Successful' }).Count -ne 0 -or [string]$result.runIdHash -notmatch '^[0-9A-F]{64}$') {
        throw 'Kodier-Hostresultat ist nicht vollständig, erfolgreich und korreliert.'
    }
    Save-CompletedCard $context $CardIndex $CardLabel $expected $checkpoint $guidanceTitle (Read-Name $context.Main 'Page.Station.StationGuidanceExplanation') $guidanceEvidence
    Write-Host "SUCCESS: $CardLabel · $expected · sichtbarer UI-Erfolg und Hostvertrag nachträglich fail-closed erfasst"
    return
}

if($Action-eq'Finish'){
    if(@($context.State.completedCards).Count-ne5){throw "Erst $(@($context.State.completedCards).Count)/5 Karten abgeschlossen."}
    Expand-Completion $context.Main
    $button=Find-Element -Root $context.Main -AutomationId 'Page.Station.StationExportButton';if(-not$button.Current.IsEnabled){throw 'Sitzungsabschluss ist nicht freigegeben.'};Invoke-Element $button;Start-Sleep -Milliseconds 500
    if((Read-Name $context.Main 'Page.Station.StationState')-ne'SITZUNG GESCHLOSSEN'){throw 'Sitzung wurde nicht geschlossen.'}
    $context.Main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close();$context.Process.WaitForExit(5000)|Out-Null
    $summary=Get-ChildItem 'D:\RFiDGear-Projekte\UI-Abnahme-RC56-20260826\sessions' -Filter '*_summary.json'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
    if(-not$summary){throw 'Serienzusammenfassung fehlt.'};Copy-Item -LiteralPath $summary.FullName -Destination (Join-Path $cardEvidenceRoot 'series-summary.json') -Force
    Write-Host "FINISHED: 5/5 Karten; Zusammenfassung $($summary.FullName)"
}

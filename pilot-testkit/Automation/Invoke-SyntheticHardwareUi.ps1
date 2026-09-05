[CmdletBinding()]
param(
    [ValidateSet('Setup','Run')][string]$Action,
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$ProjectName = 'SYNTHETIC-MODULAR-HARDWARE-RC8',
    [string]$CardValue = '731245',
    [string]$StatePath = '',
    [switch]$Authorized
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$kitRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($StatePath)) { $StatePath = Join-Path $kitRoot 'artifacts\synthetic-hardware\active.json' }
$evidenceRoot = Split-Path -Parent $StatePath
$profileSource = Join-Path (Split-Path -Parent $PSScriptRoot) 'fixtures\Synthetic-AES-Mixed.base.json'
$profileTarget = Join-Path $env:APPDATA 'SieEncode\profiles\base\Synthetic-AES-Mixed.base.json'
$backupDirectory = Join-Path $kitRoot 'artifacts\synthetic-hardware\vault-backup'
$projectPath = "D:\RFiDGear-Projekte\$ProjectName\$ProjectName.project.json"
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null

function Find-Element {
    param($Root,[string]$Name='',[string]$AutomationId='',[System.Windows.Automation.ControlType]$ControlType,[int]$TimeoutMilliseconds=20000)
    $until=[DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $conditions=New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
        if($Name){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)))}
        if($AutomationId){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$AutomationId)))}
        if($ControlType){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,$ControlType)))}
        $condition=if($conditions.Count-eq1){$conditions[0]}else{New-Object System.Windows.Automation.AndCondition($conditions.ToArray())}
        $element=$Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if($element){return $element}
        Start-Sleep -Milliseconds 150
    } while([DateTime]::UtcNow-lt$until)
    throw "UI-Element nicht gefunden: Name='$Name', AutomationId='$AutomationId'."
}
function Invoke-Element($Element){$Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
function Set-Value($Root,[string]$Id,[string]$Value){$element=Find-Element $Root -AutomationId $Id;$element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)}
function Read-Name($Root,[string]$Id){(Find-Element $Root -AutomationId $Id -TimeoutMilliseconds 5000).Current.Name}
function Expand-Element($Root,[string]$Id){$element=Find-Element $Root -AutomationId $Id;$pattern=$element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern);if($pattern.Current.ExpandCollapseState-eq[System.Windows.Automation.ExpandCollapseState]::Collapsed){$pattern.Expand()}}
function Expand-Named($Root,[string]$Name){$element=Find-Element $Root -Name $Name;$pattern=$element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern);if($pattern.Current.ExpandCollapseState-eq[System.Windows.Automation.ExpandCollapseState]::Collapsed){$pattern.Expand()}}
function Select-Combo($Root,[string]$Id,[string]$Contains){
    $combo=Find-Element $Root -AutomationId $Id
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
    $until=[DateTime]::UtcNow.AddSeconds(8);$item=$null
    do{$items=@($combo.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition));$item=$items|Where-Object{$_.Current.Name-like"*$Contains*"}|Select-Object -First 1;if(-not$item){Start-Sleep -Milliseconds 150}}while(-not$item-and[DateTime]::UtcNow-lt$until)
    if(-not$item){throw "Auswahl '$Contains' fehlt in $Id."}
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    try{$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()}catch{}
}
function Get-Main([int]$ProcessId){
    $desktop=[System.Windows.Automation.AutomationElement]::RootElement
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
    $until=[DateTime]::UtcNow.AddSeconds(30);$main=$null
    do{$main=$desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition);if(-not$main){Start-Sleep -Milliseconds 200}}while(-not$main-and[DateTime]::UtcNow-lt$until)
    if(-not$main){throw 'Sichtbares SieEncode-Hauptfenster fehlt.'}
    [pscustomobject]@{Desktop=$desktop;Main=$main;Condition=$condition}
}
function Get-ProcessWindows($Context){@($Context.Desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants,$Context.Condition))|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Window}}
function Find-Dialog($Context,[string]$Title,[int]$Seconds=20){
    $until=[DateTime]::UtcNow.AddSeconds($Seconds);$dialog=$null
    do{$dialog=Get-ProcessWindows $Context|Where-Object{$_.Current.Name-eq$Title}|Select-Object -First 1;if(-not$dialog){Start-Sleep -Milliseconds 150}}while(-not$dialog-and[DateTime]::UtcNow-lt$until)
    if(-not$dialog){throw "Dialog '$Title' wurde nicht geöffnet."};$dialog
}
function Confirm-Button($Context,[string]$Name,[int]$Seconds=30){
    $until=[DateTime]::UtcNow.AddSeconds($Seconds);$button=$null
    do{$button=@($Context.Desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants,$Context.Condition))|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Button-and$_.Current.Name-eq$Name-and$_.Current.IsEnabled}|Select-Object -First 1;if(-not$button){Start-Sleep -Milliseconds 150}}while(-not$button-and[DateTime]::UtcNow-lt$until)
    if(-not$button){throw "Bestätigung '$Name' fehlt."};Invoke-Element $button;Start-Sleep -Milliseconds 300
}
function Wait-State($Main,[string]$Pattern,[int]$Seconds=240){$until=[DateTime]::UtcNow.AddSeconds($Seconds);do{Start-Sleep -Milliseconds 400;$state=Read-Name $Main 'Page.Station.StationState';if($state-like$Pattern){return $state};$guidance=Read-Name $Main 'Page.Station.StationGuidanceTitle';if($guidance-like$Pattern){return $guidance};if($state-like'*NICHT FREIGEGEBEN*'-or$guidance-like'*NICHT FREIGEGEBEN*'){throw "$state · $guidance · $(Read-Name $Main 'Page.Station.StationStateDetail')"}}while([DateTime]::UtcNow-lt$until);throw "Zeitüberschreitung. Letzter Zustand: $state · $guidance"}
function Invoke-Operation($Context,[string]$Id,[int]$Confirmations,[string]$SuccessPattern='RFIDGEAR-LAUF ERFOLGREICH*'){
    Invoke-Element (Find-Element $Context.Main -AutomationId $Id)
    for($i=0;$i-lt$Confirmations;$i++){Confirm-Button $Context 'Ja' 90}
    $state=Wait-State $Context.Main $SuccessPattern 300
    if(Get-Process 'SieEncode.Runtime','RFiDGear' -ErrorAction SilentlyContinue){throw "Runtime blieb nach $Id aktiv."}
    $state
}

if($Action-eq'Setup'){
    if(Get-Process SieEncode -ErrorAction SilentlyContinue){throw 'Vor Setup muss SieEncode geschlossen sein.'}
    if(-not(Test-Path $InstalledExe)){throw 'Installierte SieEncode-Anwendung fehlt.'}
    if(Test-Path $projectPath){throw "Synthetisches Testprojekt existiert bereits: $projectPath"}
    New-Item -ItemType Directory -Path (Split-Path -Parent $profileTarget),$backupDirectory -Force|Out-Null
    Copy-Item -LiteralPath $profileSource -Destination $profileTarget
    $process=Start-Process -FilePath $InstalledExe -PassThru
    $context=Get-Main $process.Id
    $mode=Find-Element $context.Main -AutomationId 'Window.ExpertModeToggle'
    $toggle=$mode.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern);if($toggle.Current.ToggleState-ne[System.Windows.Automation.ToggleState]::On){$toggle.Toggle();Start-Sleep -Milliseconds 300}
    Invoke-Element (Find-Element $context.Main -Name 'Einstellungen' -ControlType ([System.Windows.Automation.ControlType]::Button))
    Set-Value $context.Main 'Page.Settings.VaultBackupDirectoryText' $backupDirectory
    Invoke-Element (Find-Element $context.Main -Name 'Gebündelte SieEncode Runtime verwenden')
    Invoke-Element (Find-Element $context.Main -Name 'Einstellungen speichern')
    Start-Sleep -Milliseconds 500
    Invoke-Element (Find-Element $context.Main -Name 'Übersicht' -ControlType ([System.Windows.Automation.ControlType]::Button))
    Invoke-Element (Find-Element $context.Main -Name 'Neues Projekt' -ControlType ([System.Windows.Automation.ControlType]::Button))
    Select-Combo $context.Main 'Page.Editor.EditorBaseProfile' 'SYNTHETIC-MODULAR-2.0'
    Set-Value $context.Main 'Page.Editor.EditorProjectName' $ProjectName
    Set-Value $context.Main 'EditorProjectValue.testSite' '0042'
    Set-Value $context.Main 'EditorProjectValue.testHex' 'A1B2C3'
    Set-Value $context.Main 'EditorProjectValue.testLabel' 'OSS1'
    Set-Value $context.Main 'EditorProjectValue.testUnicode' 'ÄÖ'
    # WPF bindings are updated asynchronously after UI Automation SetValue.
    # Give the visible editor time to commit the values before generating keys/saving.
    Start-Sleep -Milliseconds 500
    Expand-Named $context.Main 'Transportkeys · Experteneinstellung'
    Select-Combo $context.Main 'Page.Editor.EditorTransportPiccType' 'DF_KEY_AES'
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorGenerateKeysButton')
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorSaveButton')
    $saveUntil=[DateTime]::UtcNow.AddSeconds(15)
    do { if(Test-Path $projectPath){break}; Start-Sleep -Milliseconds 200 } while([DateTime]::UtcNow-lt$saveUntil)
    if(-not(Test-Path $projectPath)){throw 'Projekt wurde über die Oberfläche nicht am erwarteten Pfad gespeichert.'}
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorManageVaultAccessButton')
    $vault=Find-Dialog $context 'Vault-Zugriff für mehrere Benutzer'
    $identityCombo=Find-Element $vault -ControlType ([System.Windows.Automation.ControlType]::ComboBox)
    $identityCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $listCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
    $identities=@($identityCombo.FindAll([System.Windows.Automation.TreeScope]::Descendants,$listCondition))
    try{$identityCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()}catch{}
    if($identities.Count-eq0){
        Invoke-Element (Find-Element $vault -Name 'Neue Identität')
        $identityDialog=Find-Dialog $context 'Neue SieEncode-Identität'
        $input=Find-Element $identityDialog -ControlType ([System.Windows.Automation.ControlType]::Edit)
        $input.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Interne Teststation')
        Invoke-Element (Find-Element $identityDialog -Name 'OK')
        $vault=Find-Dialog $context 'Vault-Zugriff für mehrere Benutzer'
    } else {
        Invoke-Element (Find-Element $vault -Name 'Zur Empfängerliste hinzufügen')
    }
    $password=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))+'aA1!'
    $passwordBoxes=@($vault.FindAll([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)))|Where-Object{$_.Current.IsPassword})
    if($passwordBoxes.Count-ne2){throw "Recovery-Passwortfelder: $($passwordBoxes.Count) statt 2."}
    foreach($box in $passwordBoxes){$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($password)}
    Invoke-Element (Find-Element $vault -Name 'Vault sicher freigeben')
    $password=$null
    $until=[DateTime]::UtcNow.AddSeconds(40);do{Start-Sleep -Milliseconds 250;$status=Read-Name $context.Main 'Page.Editor.EditorVaultStatus'}while($status-notlike'Mehrbenutzer-Vault sicher gespeichert*'-and[DateTime]::UtcNow-lt$until)
    if($status-notlike'Mehrbenutzer-Vault sicher gespeichert*'){throw "Vault nicht bestätigt: $status"}
    $bindProfiles=Find-Element $context.Main -Name 'Aktuelle Profilstände verbindlich übernehmen' -ControlType ([System.Windows.Automation.ControlType]::Button)
    if($bindProfiles.Current.IsEnabled){Invoke-Element $bindProfiles;Confirm-Button $context 'Ja' 30;Start-Sleep -Milliseconds 500}
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorBuildButton')
    $success=Find-Dialog $context 'Sicherer Projektbuild' 120
    Invoke-Element (Find-Element $success -Name 'OK')
    $state=[ordered]@{schemaVersion='1.0';processId=$process.Id;project=$ProjectName;projectPath=$projectPath;installedVersion=(Get-Item $InstalledExe).VersionInfo.ProductVersion;installedSha256=(Get-FileHash $InstalledExe -Algorithm SHA256).Hash;runtimeSha256=(Get-FileHash 'C:\Program Files\SieEncode\Runtime\SieEncode.Runtime.exe' -Algorithm SHA256).Hash;createdAt=[DateTimeOffset]::Now.ToString('O')}
    $state|ConvertTo-Json|Set-Content -LiteralPath $StatePath -Encoding UTF8
    Write-Host "SETUP_OK: $ProjectName · Profil A1B2C3 · 12 Datenbausteine · Mehrbenutzer-Vault · sicherer UI-Build · PID $($process.Id)"
    return
}

if(-not$Authorized){throw 'Der reale synthetische Kartenlauf benötigt -Authorized.'}
$state=Get-Content -LiteralPath $StatePath -Raw|ConvertFrom-Json
$process=Get-Process -Id $state.processId -ErrorAction Stop
if((Get-FileHash $InstalledExe -Algorithm SHA256).Hash-ne$state.installedSha256){throw 'Installierte Anwendung änderte sich seit Setup.'}
$context=Get-Main $process.Id
Invoke-Element (Find-Element $context.Main -Name 'Projekte' -ControlType ([System.Windows.Automation.ControlType]::Button))
$grid=Find-Element $context.Main -AutomationId 'Page.Projects.ProjectsGrid'
$rowCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::DataItem)
$row=@($grid.FindAll([System.Windows.Automation.TreeScope]::Descendants,$rowCondition))|Where-Object{$_.Current.Name-like"*$ProjectName*"}|Select-Object -First 1
if(-not$row){throw 'Synthetisches Projekt fehlt in der sichtbaren Projektbibliothek.'};$row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Projects.OpenStationButton')
Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Station.StationPreflightButton')
Wait-State $context.Main 'PROJEKT OK' 30|Out-Null
Expand-Named $context.Main 'Vertiefte Projektprüfung · Experten'
Expand-Named $context.Main 'Erweiterte Kartenaktionen · Mastering, Abnahme und Recovery'
$recognition=Invoke-Operation $context 'Page.Station.StationRecognitionButton' 1 'TRANSPONDER-ERKENNUNG*'
$recovery=Invoke-Operation $context 'Page.Station.StationRecoveryTransportButton' 2
$mastering=Invoke-Operation $context 'Page.Station.StationMasteringAutoButton' 1 'MASTERING ERFOLGREICH*'
Set-Value $context.Main 'StationField.testSerial' $CardValue
Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Station.StationPrepareButton')
Wait-State $context.Main 'SITZUNG BEREIT' 30|Out-Null
Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Station.StationStartAutoModeButton')
$final=Wait-State $context.Main 'KODIERUNG ERFOLGREICH BESTÄTIGT*' 300
if(Get-Process 'SieEncode.Runtime','RFiDGear' -ErrorAction SilentlyContinue){throw 'Runtime blieb nach synthetischer Kodierung aktiv.'}
$evidence=Read-Name $context.Main 'Page.Station.StationGuidanceEvidence'
if($evidence-notmatch'3/3 bytegleich'-or$evidence-notmatch'16/16 Tasks'-or$evidence-notmatch'TaskLoop.Success'-or$evidence-notmatch'Korrelation OK'){throw "Unvollständige Kodierfreigabe: $evidence"}
$result=[ordered]@{schemaVersion='1.0';containsKeys=$false;project=$ProjectName;profile='SYNTHETIC-MODULAR-2.0';aid='A1B2C3';cardValue=$CardValue;completedAt=[DateTimeOffset]::Now.ToString('O');installedVersion=$state.installedVersion;installedSha256=$state.installedSha256;runtimeSha256=$state.runtimeSha256;recognition=$recognition;recovery=$recovery;mastering=$mastering;coding=$final;evidence=$evidence;runtimeClosed=$true}
$resultPath=Join-Path $evidenceRoot 'synthetic-hardware-result.json';$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding UTF8
$context.Main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close();$process.WaitForExit(10000)|Out-Null
Write-Host "SUCCESS: erfundene AID A1B2C3 · Plain/MAC/Encrypt · 3/3 bytegleich · 16/16 Tasks · Runtime geschlossen · $resultPath"

[CmdletBinding()]
param(
    [ValidateSet('OpenEditor','Inspect','GenerateKeysInspect','RepairLegacyTransportPicc','EnableCodingBuild')][string]$Action = 'OpenEditor',
    [string]$ProjectName = 'Pilotprojekt',
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe',
    [string]$StatePath = '',
    [string]$ProjectPath = '',
    [string]$LegacyMasteringProject = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'artifacts\security-ui-state.json'
}

function Find-Element {
    param($Root,[string]$Name='',[string]$AutomationId='',[int]$TimeoutMilliseconds=15000)
    $until=[DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $conditions=New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
        if($Name){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)))}
        if($AutomationId){$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$AutomationId)))}
        if($conditions.Count-eq0){throw 'Name oder AutomationId fehlt.'}
        $condition=if($conditions.Count-eq1){$conditions[0]}else{New-Object System.Windows.Automation.AndCondition($conditions.ToArray())}
        $element=$Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if($element){return $element}
        Start-Sleep -Milliseconds 150
    } while([DateTime]::UtcNow-lt$until)
    throw "UI-Element nicht gefunden: Name='$Name', AutomationId='$AutomationId'."
}

function Invoke-Element($Element) {
    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Get-Context {
    if(-not(Test-Path -LiteralPath $StatePath)){throw 'UI-Abnahmestatus fehlt. Zuerst OpenEditor ausführen.'}
    $state=Get-Content -LiteralPath $StatePath -Raw|ConvertFrom-Json
    $process=Get-Process -Id $state.processId -ErrorAction Stop
    if([IO.Path]::GetFullPath($process.Path)-ne[IO.Path]::GetFullPath($InstalledExe)){throw 'UI-Prozess gehört nicht zur erwarteten Installation.'}
    $desktop=[System.Windows.Automation.AutomationElement]::RootElement
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $main=$desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
    if(-not$main){throw 'Sichtbares SieEncode-Hauptfenster fehlt.'}
    [pscustomobject]@{State=$state;Process=$process;Desktop=$desktop;Main=$main}
}

function Show-VisibleContract($Main) {
    $types=@(
        [System.Windows.Automation.ControlType]::Button,
        [System.Windows.Automation.ControlType]::Text,
        [System.Windows.Automation.ControlType]::Edit,
        [System.Windows.Automation.ControlType]::ComboBox,
        [System.Windows.Automation.ControlType]::CheckBox
    )
    @($Main.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)) |
        Where-Object { $_.Current.ControlType -in $types -and ($_.Current.Name -or $_.Current.AutomationId) } |
        ForEach-Object { [pscustomobject]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled} } |
        Format-Table -Wrap -AutoSize
}

if($Action-eq'OpenEditor'){
    if(Get-Process SieEncode -ErrorAction SilentlyContinue){throw 'Vor OpenEditor muss SieEncode geschlossen sein.'}
    if(-not(Test-Path -LiteralPath $InstalledExe)){throw "Installierte App fehlt: $InstalledExe"}
    $process=Start-Process -FilePath $InstalledExe -PassThru
    $desktop=[System.Windows.Automation.AutomationElement]::RootElement
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $until=[DateTime]::UtcNow.AddSeconds(30)
    do{$main=$desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition);if($main){break};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow-lt$until)
    if(-not$main){throw 'SieEncode-Hauptfenster fehlt.'}
    Invoke-Element (Find-Element $main -Name 'Projekte')
    $grid=Find-Element $main -AutomationId 'Page.Projects.ProjectsGrid'
    $dataItemCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::DataItem)
    $rows=@($grid.FindAll([System.Windows.Automation.TreeScope]::Descendants,$dataItemCondition))
    $row=$rows|Where-Object{$_.Current.Name-like("*"+$ProjectName+"*")}|Select-Object -First 1
    if(-not$row){throw "Projekt fehlt: $ProjectName"}
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Element (Find-Element $main -Name 'Bearbeiten')
    Find-Element $main -AutomationId 'Page.Editor.EditorGenerateKeysButton' -TimeoutMilliseconds 15000|Out-Null
    $state=[ordered]@{schemaVersion='1.0';processId=$process.Id;installedExe=$InstalledExe;project=$ProjectName;startedAt=[DateTimeOffset]::Now.ToString('O')}
    $state|ConvertTo-Json|Set-Content -LiteralPath $StatePath -Encoding UTF8
    Write-Host "OPEN_EDITOR_OK: $($main.Current.Name) · $ProjectName · PID $($process.Id)"
    Show-VisibleContract $main
    return
}

$context=Get-Context
if($Action-eq'GenerateKeysInspect'){
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorGenerateKeysButton')
    Start-Sleep -Milliseconds 800
    $processCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$context.Process.Id)
    @($context.Desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants,$processCondition)) |
        Where-Object {$_.Current.ControlType -in @([System.Windows.Automation.ControlType]::Window,[System.Windows.Automation.ControlType]::Text,[System.Windows.Automation.ControlType]::Button) -and ($_.Current.Name-or$_.Current.AutomationId)} |
        ForEach-Object {[pscustomobject]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled}} |
        Format-Table -Wrap -AutoSize
    return
}
if($Action-eq'RepairLegacyTransportPicc'){
    if(-not(Test-Path -LiteralPath $ProjectPath)){throw "Projektdefinition fehlt: $ProjectPath"}
    if(-not(Test-Path -LiteralPath $LegacyMasteringProject)){throw "Altes Masteringprojekt fehlt: $LegacyMasteringProject"}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[System.IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($LegacyMasteringProject))
    try{
        $entry=$archive.GetEntry('taskdatabase.xml');if(-not$entry){throw 'taskdatabase.xml fehlt im alten Masteringprojekt.'}
        $reader=New-Object IO.StreamReader($entry.Open())
        try{[xml]$xml=$reader.ReadToEnd()}finally{$reader.Dispose()}
    }finally{$archive.Dispose()}
    $transportKeys=@($xml.SelectNodes('//*[local-name()="DesfireMasterKeyCurrent"]')|ForEach-Object{$_.InnerText.Trim()}|Where-Object{$_-match'^[0-9A-Fa-f]{32}$'}|Sort-Object -Unique)
    if($transportKeys.Count-ne1){throw "Alter Masteringbuild enthält keinen eindeutigen Transport-PICC-Key (Varianten: $($transportKeys.Count))."}
    $transportKey=$transportKeys[0]

    $vaultPath=[IO.Path]::Combine([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ProjectPath)),[IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($ProjectPath))+'.keys.vault.json')
    $before=Get-Content -LiteralPath $vaultPath -Raw|ConvertFrom-Json
    $beforeRecovery=@($before.recoveryNonce,$before.recoveryCiphertext,$before.recoveryTag)-join'|'
    $beforeRecipients=ConvertTo-Json @($before.recipients|ForEach-Object{[ordered]@{id=$_.id;wrappedKey=$_.wrappedKey}}) -Compress

    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorUnlockVaultButton')
    $transportSettings=Find-Element $context.Main -Name 'Transportkeys · Experteneinstellung'
    $transportExpand=$transportSettings.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if($transportExpand.Current.ExpandCollapseState-eq[System.Windows.Automation.ExpandCollapseState]::Collapsed){$transportExpand.Expand()}
    $transportPicc=Find-Element $context.Main -AutomationId 'Page.Editor.EditorTransportPiccKey'
    $transportPicc.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($transportKey)
    $transportKey='';$transportKeys=@()
    $save=Find-Element $context.Main -AutomationId 'Page.Editor.EditorSaveVaultButton'
    if($save.Current.Name-ne'Vault-Änderungen sicher speichern'){throw "Mehrbenutzer-Vault würde nicht im sicheren Aktualisierungsmodus gespeichert: $($save.Current.Name)"}
    Invoke-Element $save
    $status=Find-Element $context.Main -AutomationId 'Page.Editor.EditorVaultStatus'
    $until=[DateTime]::UtcNow.AddSeconds(30)
    do{Start-Sleep -Milliseconds 200;$statusText=$status.Current.Name}while($statusText-notlike'Mehrbenutzer-Vault mit der persönlichen Windows-Identität aktualisiert*'-and[DateTime]::UtcNow-lt$until)
    if($statusText-notlike'Mehrbenutzer-Vault mit der persönlichen Windows-Identität aktualisiert*'){throw "Vault-Aktualisierung wurde nicht bestätigt: $statusText"}
    $after=Get-Content -LiteralPath $vaultPath -Raw|ConvertFrom-Json
    $afterRecovery=@($after.recoveryNonce,$after.recoveryCiphertext,$after.recoveryTag)-join'|'
    $afterRecipients=ConvertTo-Json @($after.recipients|ForEach-Object{[ordered]@{id=$_.id;wrappedKey=$_.wrappedKey}}) -Compress
    if($beforeRecovery-ne$afterRecovery-or$beforeRecipients-ne$afterRecipients){throw 'Empfänger- oder Recovery-Umschläge wurden bei der Vault-Aktualisierung verändert.'}
    Write-Host 'REPAIR_LEGACY_TRANSPORT_PICC_OK: eindeutiger Altwert übernommen · Schlüsselwert nicht ausgegeben · Empfänger und Recovery unverändert'
    return
}
if($Action-eq'EnableCodingBuild'){
    Invoke-Element (Find-Element $context.Main -AutomationId 'Page.Editor.EditorUnlockVaultButton')
    $vaultStatus=Find-Element $context.Main -AutomationId 'Page.Editor.EditorVaultStatus'
    $until=[DateTime]::UtcNow.AddSeconds(30)
    do{Start-Sleep -Milliseconds 200;$vaultText=$vaultStatus.Current.Name}while($vaultText-notlike'*persönlichen Windows-Identität entsperrt*'-and[DateTime]::UtcNow-lt$until)
    if($vaultText-notlike'*persönlichen Windows-Identität entsperrt*'){throw "Vault wurde nicht automatisch über die Windows-Identität entsperrt: $vaultText"}

    $transportSettings=Find-Element $context.Main -Name 'Transportkeys · Experteneinstellung'
    $transportExpand=$transportSettings.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if($transportExpand.Current.ExpandCollapseState-eq[System.Windows.Automation.ExpandCollapseState]::Collapsed){$transportExpand.Expand()}
    $transportPicc=Find-Element $context.Main -AutomationId 'Page.Editor.EditorTransportPiccKey'
    $transportValue=$transportPicc.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if($transportValue.Length-ne32){throw "Transport-PICC-Key wurde beim Entsperren nicht in die UI-Sitzung übernommen (Länge $($transportValue.Length))."}

    $buildSettings=Find-Element $context.Main -Name 'Technische Build-Einstellungen · Experten'
    $expand=$buildSettings.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if($expand.Current.ExpandCollapseState-eq[System.Windows.Automation.ExpandCollapseState]::Collapsed){$expand.Expand()}
    $coding=Find-Element $context.Main -AutomationId 'Page.Editor.EditorBuildCoding'
    $toggle=$coding.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if($toggle.Current.ToggleState-ne[System.Windows.Automation.ToggleState]::On){$toggle.Toggle()}

    $build=Find-Element $context.Main -AutomationId 'Page.Editor.EditorBuildButton'
    $until=[DateTime]::UtcNow.AddSeconds(20)
    do{Start-Sleep -Milliseconds 200;$enabled=$build.Current.IsEnabled}while(-not$enabled-and[DateTime]::UtcNow-lt$until)
    if(-not$enabled){
        $validation=(Find-Element $context.Main -AutomationId 'Page.Editor.EditorValidation').Current.Name
        throw "Build blieb nach Vault-Entsperrung und Aktivierung der Kodierung gesperrt: $validation"
    }
    Invoke-Element $build

    $processCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$context.Process.Id)
    $until=[DateTime]::UtcNow.AddSeconds(120)
    do{
        Start-Sleep -Milliseconds 250
        $elements=@($context.Main.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))
        $legacyText=$elements|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Text-and$_.Current.Name-like'Der sichere Sitzungsbuild wurde vollständig geprüft*'}|Select-Object -First 1
        if($legacyText){Invoke-Element ($elements|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Button-and$_.Current.Name-eq'Nein'}|Select-Object -First 1)}
        $successText=$elements|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Text-and$_.Current.Name-like'Projekt erfolgreich und sicher geprüft*'}|Select-Object -First 1
        if($successText){Invoke-Element ($elements|Where-Object{$_.Current.ControlType-eq[System.Windows.Automation.ControlType]::Button-and$_.Current.Name-eq'OK'}|Select-Object -First 1);$success=$true;break}
    }while([DateTime]::UtcNow-lt$until)
    if(-not$success){throw 'Bestätigung für den sicheren Projektbuild wurde nicht angezeigt.'}
    $validation=(Find-Element $context.Main -AutomationId 'Page.Editor.EditorValidation').Current.Name
    if($validation-notlike'SICHERER BUILD OK*'){throw "Sicherer Build meldet keinen Erfolg: $validation"}
    Write-Host "ENABLE_CODING_BUILD_OK: Vault per Windows-Identität · Transport-PICC-Key Länge 32 · $validation"
    return
}
Show-VisibleContract $context.Main

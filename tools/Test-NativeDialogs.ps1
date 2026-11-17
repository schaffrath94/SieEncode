[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -ReferencedAssemblies UIAutomationClient,UIAutomationTypes -TypeDefinition @'
using System;
using System.Threading;
using System.Windows.Automation;
public static class DialogInvoke {
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, string value);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    public static void Save(int edit, int dialog, string path) {
        if (edit == 0 || dialog == 0) throw new InvalidOperationException("Missing test dialog handle");
        SendMessage((IntPtr)edit, 0x000C, IntPtr.Zero, path);
        if (!PostMessage((IntPtr)dialog, 0x0111, (IntPtr)1, IntPtr.Zero)) throw new InvalidOperationException("Save command failed");
    }
    public static void Invoke(AutomationElement element) {
        var pattern = (InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern);
        var thread = new Thread(() => { try { pattern.Invoke(); } catch {} });
        thread.IsBackground = true; thread.Start();
    }
}
'@
$Executable=[IO.Path]::GetFullPath($Executable)
if((Split-Path -Leaf $Executable) -ne 'SieEncode.DialogTests.exe'){throw 'Separate dialog-test build required; never use installed SieEncode.exe.'}
$identityPath=Join-Path (Split-Path -Parent $Executable) 'DIALOG-TEST-BUILD.json'
$identity=Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
if((Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $Executable) 'SieEncode.DialogTests.dll')).Hash -ne $identity.dllSha256){throw 'Test-build identity mismatch'}
if(Get-Process -Name SieEncode,SieEncode.DialogTests -ErrorAction SilentlyContinue){throw 'Close SieEncode and other test instances first.'}
$root=Join-Path $env:TMP ('dialog-test-'+[guid]::NewGuid().ToString('N'))
$script:app=$null
$script:steps=New-Object 'System.Collections.Generic.List[string]'
$script:actions=0

function Wait-Until([scriptblock]$Read,[string]$Label) {
    $until=[DateTime]::UtcNow.AddSeconds(20)
    do {
        $value=& $Read
        if($value){return $value}
        Start-Sleep -Milliseconds 150
    } while([DateTime]::UtcNow -lt $until)
    throw "Timeout: $Label"
}
function Elements($Parent,$Type) {
    $condition=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,$Type)
    return @($Parent.FindAll([Windows.Automation.TreeScope]::Descendants,$condition))
}
function Find-Id($Parent,[string]$Id) {
    Wait-Until { $Parent.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))) } $Id
}
function Find-Name($Parent,[string]$Name) {
    Wait-Until { $Parent.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name))) } $Name
}
function Windows {
    $condition=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty,$script:app.Id)
    foreach ($top in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition)) {
        $top
        foreach ($owned in (Elements $top ([Windows.Automation.ControlType]::Window))) {
            if ($owned.Current.ProcessId -eq $script:app.Id) { $owned }
        }
    }
}
function Window([string]$Title) { Wait-Until { @(Windows | Where-Object {$_.Current.Name -eq $Title}) | Select-Object -First 1 } $Title }
function Select-Item($Element) { ([Windows.Automation.SelectionItemPattern]$Element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select() }
function Click($Element) {
    if(-not $Element.Current.IsEnabled){throw ('Disabled: '+$Element.Current.Name)}
    [DialogInvoke]::Invoke($Element)
    $script:actions++
}
function Expand-Item($Element) { ([Windows.Automation.ExpandCollapsePattern]$Element.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand() }
function Set-Value($Element,[string]$Value) { ([Windows.Automation.ValuePattern]$Element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue($Value) }
function Row($Grid,[int]$Index) {
    $rows=Elements $Grid ([Windows.Automation.ControlType]::DataItem)
    if($rows.Count -le $Index){throw "Missing grid row $Index"}
    return $rows[$Index]
}
function Cell($Grid,[int]$Row,[int]$Column) {
    return ([Windows.Automation.GridPattern]$Grid.GetCurrentPattern([Windows.Automation.GridPattern]::Pattern)).GetItem($Row,$Column)
}
function Confirm-Yes {
    $button=Wait-Until {
        foreach($window in (Windows)) {
            $candidate=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty,'6')))
            if($candidate){return $candidate}
        }
    } 'owned confirmation Yes'
    Click $button
}
function Start-Phase([string]$Phase,[switch]$Headless) {
    if($script:app){throw 'Previous test process still tracked'}
    $psi=New-Object Diagnostics.ProcessStartInfo
    $psi.FileName=$Executable; $psi.Arguments='--dialog-tests "'+$root+'" '+$Phase
    $psi.UseShellExecute=$false; $psi.RedirectStandardError=$true; $psi.RedirectStandardOutput=$true
    $script:app=New-Object Diagnostics.Process; $script:app.StartInfo=$psi
    [void]$script:app.Start()
    $script:stderr=$script:app.StandardError.ReadToEndAsync(); $script:stdout=$script:app.StandardOutput.ReadToEndAsync()
    if($Headless){
        if(-not $script:app.WaitForExit(30000)){throw "Fixture phase timed out: $Phase"}
        $errorText=$script:stderr.GetAwaiter().GetResult()
        if($script:app.ExitCode -ne 0){throw "Fixture phase failed: $Phase; $errorText"}
        $script:app.Dispose(); $script:app=$null
    } else { [void](Window 'SieEncode · SYNTHETIC DIALOG TEST') }
}
function Stop-Phase {
    if($script:app -and -not $script:app.HasExited){
        # Test-owned process only. Persisted state is checked in a new process afterwards.
        $script:app.Kill(); $script:app.WaitForExit()
    }
    if($script:app){$script:app.Dispose();$script:app=$null}
}
function Save-NativeFile([string]$Path) {
    $save=Wait-Until { @(Windows | Where-Object { $_.Current.ClassName -eq '#32770' -and $_.Current.Name -notlike '*Entwicklung*' }) | Select-Object -First 1 } 'save dialog'
    $input=Find-Id $save '1001'
    if($input.Current.ControlType -ne [Windows.Automation.ControlType]::Edit){$input=(Elements $input ([Windows.Automation.ControlType]::Edit))[0]}
    [DialogInvoke]::Save($input.Current.NativeWindowHandle,$save.Current.NativeWindowHandle,$Path)
    try { [void](Wait-Until {Test-Path -LiteralPath $Path} 'exported file') }
    catch { throw ('Export missing. Test windows: ' + (@(Windows | ForEach-Object { $_.Current.Name + ' ' + (@(Elements $_ ([Windows.Automation.ControlType]::Text) | ForEach-Object {$_.Current.Name} | Select-Object -Last 4) -join ' ') }) -join ' | ')) }
}

$status='FAILED';$failure=$null
try {
    Start-Phase 'seed' -Headless
    Start-Phase 'copy'
    $main=Window 'SieEncode · SYNTHETIC DIALOG TEST'
    Expand-Item (Find-Name $main 'Weitere Editoraktionen · Experten')
    Click (Find-Name $main 'Speichern unter')
    $prompt=Window 'Gruppenprojekt vollständig kopieren'
    Set-Value (Find-Id $prompt 'TextEntryPrompt.TextBox') 'Dialog Copy'
    Click (Find-Id $prompt 'TextEntryPrompt.Button.OK')
    [void](Wait-Until { @(Elements $main ([Windows.Automation.ControlType]::Text) | Where-Object {$_.Current.Name -like 'Dialog Copy ·*'}).Count -gt 0 } 'copy selected in header')
    Stop-Phase; Start-Phase 'verify-copy' -Headless
    Start-Phase 'copy-reopen'
    $main=Window 'SieEncode · SYNTHETIC DIALOG TEST'
    $copy=Find-Name $main 'Dialog Copy'
    # A text cell belongs to its selectable DataGrid row.
    $walker=[Windows.Automation.TreeWalker]::ControlViewWalker
    while($copy.Current.ControlType -ne [Windows.Automation.ControlType]::DataItem){$copy=$walker.GetParent($copy);if(-not $copy){throw 'Copy row unavailable'}}
    Select-Item $copy
    Click (Find-Name $main 'Bearbeiten')
    Click (Find-Name $main 'Vault entsperren')
    [void](Wait-Until { @(Elements $main ([Windows.Automation.ControlType]::Text) | Where-Object {$_.Current.Name -like 'Dialog Copy ·*'}).Count -gt 0 } 'reopened copy header')
    Stop-Phase; Start-Phase 'verify-copy' -Headless
    $steps.Add('Copy via real name dialog, selected header, process restart and UI reopen; payload/source hashes verified.')

    Start-Phase 'admin'
    $admin=Window 'Offline-Gruppen verwalten · Entwicklung'
    $groups=Find-Id $admin 'GroupAdministration.GroupsGrid'; Select-Item (Row $groups 0)
    $members=Find-Id $admin 'GroupAdministration.MembersList'
    $bob=Wait-Until { @(Elements $members ([Windows.Automation.ControlType]::ListItem) | Where-Object {$_.Current.Name -like '*Bob*'}) | Select-Object -First 1 } 'Bob member'
    Select-Item $bob; Click (Find-Name $admin 'Mitglied aus Gruppe entfernen'); Confirm-Yes
    $people=Find-Id $admin 'GroupAdministration.PeopleGrid'; Select-Item (Row $people 1)
    $devices=Find-Id $admin 'GroupAdministration.DevicesGrid'
    $secondRow = @(Elements $devices ([Windows.Automation.ControlType]::DataItem) | Where-Object {
        @(Elements $_ ([Windows.Automation.ControlType]::Text) | Where-Object { $_.Current.Name -eq 'Bob second device' }).Count -gt 0
    })
    if ($secondRow.Count -ne 1) { throw 'Cannot identify Bob second device uniquely' }
    Select-Item $secondRow[0]
    $cell = @(Elements $secondRow[0] ([Windows.Automation.ControlType]::Custom) | Where-Object { @(Elements $_ ([Windows.Automation.ControlType]::CheckBox)).Count -eq 1 })[0]
    ([Windows.Automation.InvokePattern]$cell.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke()
    $checkbox=(Elements $cell ([Windows.Automation.ControlType]::CheckBox))[0]
    if(-not $checkbox){throw 'Second device checkbox missing'}
    ([Windows.Automation.TogglePattern]$checkbox.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Toggle()
    Click (Find-Name $admin 'Gruppen signiert speichern')
    [void](Wait-Until { @(Elements $admin ([Windows.Automation.ControlType]::Text) | Where-Object {$_.Current.Name -like '*signiert gespeichert*'}).Count -gt 0 } 'signed save')
    Stop-Phase; Start-Phase 'verify-admin' -Headless
    $steps.Add('Member removal and second-device deactivation through administration controls; signed persisted policy verified.')

    Start-Phase 'delegate'
    $delegate=Window 'Mein Gruppenverwaltungsplatz · Entwicklung'
    $grid=Find-Id $delegate 'GroupDelegate.GroupsGrid'
    if((Elements $grid ([Windows.Automation.ControlType]::DataItem)).Count -ne 1){throw 'Delegate scope not limited to one group'}
    Set-Value (Cell $grid 0 0) 'Lokal UI edited'
    Click (Find-Name $delegate 'Änderung signieren und exportieren')
    Save-NativeFile (Join-Path $root 'change.group-change.json')
    Stop-Phase; Start-Phase 'verify-delegate' -Headless
    Start-Phase 'revoke'
    $admin=Window 'Offline-Gruppen verwalten · Entwicklung'
    Expand-Item (Find-Name $admin 'Gruppenverwalter und Vollmachten')
    $delegations=Find-Id $admin 'GroupAdministration.DelegationsList'
    Select-Item ((Elements $delegations ([Windows.Automation.ControlType]::ListItem))[0])
    Click (Find-Name $admin 'Ausgewählte Vollmacht entziehen'); Confirm-Yes
    [void](Wait-Until { @(Elements $delegations ([Windows.Automation.ControlType]::ListItem) | Where-Object {$_.Current.Name -like '*entzogen*'}).Count -gt 0 } 'revoked grant')
    Stop-Phase; Start-Phase 'verify-revoked' -Headless
    Start-Phase 'delegate-revoked'
    $delegate=Window 'Mein Gruppenverwaltungsplatz · Entwicklung'
    if((Elements (Find-Id $delegate 'GroupDelegate.GroupsGrid') ([Windows.Automation.ControlType]::DataItem)).Count -ne 0){throw 'Revoked delegate still has editable groups'}
    $steps.Add('Delegate scope/edit/native export, revocation in admin dialog and empty scope after process restart verified.')
    $status='PASSED'
} catch { $failure=$_.Exception.Message }
finally {
    Stop-Phase
    $report=[ordered]@{status=$status; checks=@($steps.ToArray()); failure=$failure; fixture=$root; dllSha256=$identity.dllSha256; actualExternalUiAutomation=($script:actions -gt 0); multiUserAcceptance=$false; hardwareAcceptance=$false; notes='Pre-seeded invitation/grant and identities. No claim of initial enrollment/grant dialogs or cross-user revocation. Test-owned processes terminated between phases.'}
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $env:TMP 'native-dialog-result.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 5
}
if($status -ne 'PASSED'){exit 1}

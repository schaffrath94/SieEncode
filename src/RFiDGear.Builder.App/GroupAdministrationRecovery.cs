using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    private sealed record RecoveryLabel(string Fingerprint, bool Enabled, string Label);
    private readonly ListBox recoveryList = new() { MinHeight = 60, MaxHeight = 150, DisplayMemberPath = nameof(RecoveryLabel.Label) };
    private void AddRecoveryControls(Panel root)
    {
        var panel = new StackPanel();
        panel.Children.Add(Note("Recovery-Zugänge gelten für alle Gruppen dieser Bibliothek. Verwenden Sie eigene Identitäten mit getrennten Schlüsseln. Gruppenverwalter können sie nicht ändern. Vor der Verwendung muss der private Recovery-Schlüssel unabhängig gesichert und wiederhergestellt geprüft werden; die öffentliche Einladung ist keine private Sicherung."));
        panel.Children.Add(recoveryList);
        panel.Children.Add(AsyncPolicyAction("Separate Recovery-Einladung freigeben", ImportRecoveryAsync));
        panel.Children.Add(AsyncPolicyAction("Recovery-Zugang aktivieren / entziehen", ToggleRecoveryAsync));
        root.Children.Add(new Expander { Header = "Separate Recovery-Zugänge", Content = panel });
    }
    private void RefreshRecovery(OfflineGroupPolicy policy)
        => recoveryList.ItemsSource = policy.RecoveryDevices.Select(d => new RecoveryLabel(d.Fingerprint, d.Enabled,
            $"{d.Name} · {(d.Enabled ? "aktiv" : "entzogen")} · {d.Fingerprint}")).ToArray();

    internal void GrantRecoveryInvitation(VaultRecipientInvitation invitation, string fingerprint)
    {
        RequireSavedAdministration();
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary!.Value);
        store!.GrantRecovery(invitation, fingerprint, checkpoint!);
        LoadAdministration(currentLibrary!.Value);
    }
    internal void SetRecoveryEnabled(string fingerprint, bool enabled)
    {
        RequireSavedAdministration();
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary!.Value);
        var current = GroupEnrollmentPackages.Validate(store!.ExportPublic());
        if (!current.Policy.RecoveryDevices.Any(d => d.Fingerprint == fingerprint)) throw new InvalidOperationException("Recovery-Zugang fehlt.");
        store.UpdateRecoveryDevices(current.Policy.RecoveryDevices.Select(d => d.Fingerprint == fingerprint ? d with { Enabled = enabled } : d).ToArray(), checkpoint!);
        LoadAdministration(currentLibrary!.Value);
    }
    private async Task ImportRecoveryAsync()
    {
        RequireSavedAdministration();
        var dialog = new OpenFileDialog { Title = "Öffentliche Einladung der separaten Recovery-Identität", Filter = "Öffentliche Identität (*.recipient.json)|*.recipient.json" };
        if (dialog.ShowDialog(this) != true) return;
        var bytes = GroupDelegationTransport.ReadFile(dialog.FileName);
        var invitation = JsonSerializer.Deserialize<VaultRecipientInvitation>(bytes, ProjectJson.Options) ?? throw new InvalidDataException("Recovery-Einladung fehlt.");
        var fingerprint = TextEntryPrompt.Show(this, "Bibliotheksweiten Recovery-Zugang bestätigen",
            "Den unabhängig erhaltenen SHA-256-Fingerabdruck der separaten Recovery-Identität eingeben. Sie erhält Zugriff auf alle Gruppen-Vaults dieser Bibliothek nach deren Erstellung oder Abgleich.", "");
        if (fingerprint is null) return;
        var confirmed = fingerprint.Trim().ToUpperInvariant();
        await ApplySpecialPolicyAsync(
            current => OfflineGroupAdministrationStore.ProposeRecoveryGrant(current, invitation, confirmed),
            () => GrantRecoveryInvitation(invitation, confirmed),
            "Separaten bibliotheksweiten Recovery-Zugang freigeben. Privaten Recovery-Schlüssel zuvor unabhängig sichern und Wiederherstellung prüfen.");
    }
    private async Task ToggleRecoveryAsync()
    {
        RequireSavedAdministration();
        if (recoveryList.SelectedItem is not RecoveryLabel selected) throw new InvalidOperationException("Bitte einen Recovery-Zugang auswählen.");
        var action = selected.Enabled ? "entziehen" : "aktivieren";
        if (MessageBox.Show(this, $"{selected.Label}\n\nDiesen bibliotheksweiten Recovery-Zugang {action}? Neue Vaults und Rotation benötigen mindestens einen aktiven Recovery-Zugang. Den neuen Stand verteilen und betroffene Vaults abgleichen; alte Kopien bleiben zugänglich.", Title,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await ApplySpecialPolicyAsync(
            current => OfflineGroupAdministrationStore.ProposeRecoveryEnabled(current, selected.Fingerprint, !selected.Enabled),
            () => SetRecoveryEnabled(selected.Fingerprint, !selected.Enabled),
            "Recovery-Zugang " + action + ". Mindestens ein gültiger separater Recovery-Zugang muss für die Neuverschlüsselung erhalten bleiben.");
    }
}

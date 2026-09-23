using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupPolicyUpdateWindow : Window
{
    private readonly OfflineGroupTrustStore store;
    private readonly GroupEnrollmentPackage candidate;
    private readonly GroupPolicyCheckpoint checkpoint;
    private readonly CheckBox confirmation = new() { Content = "Den geprüften neuen Stand für diesen Windows-Benutzer übernehmen.", Margin = new Thickness(0, 10, 0, 10) };
    internal GroupPolicyUpdateWindow(Window owner, GroupEnrollmentPackage package, string localDirectory)
    {
        Owner = owner; Title = "Registrierte Gruppen aktualisieren"; Width = 800; Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        candidate = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(package));
        store = new OfflineGroupTrustStore(Path.Combine(localDirectory, candidate.LibraryId.ToString("N") + ".trust.bin"),
            candidate.LibraryId, new WindowsGroupTrustProtection(candidate.LibraryId));
        var current = store.LoadOffline(); checkpoint = current.Checkpoint;
        var next = store.PreviewPackageUpdate(candidate, checkpoint);
        if (next.Checkpoint == checkpoint) throw new InvalidOperationException("Dieser Gruppenstand ist bereits registriert.");
        var root = new DockPanel { Margin = new Thickness(20) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom);
        var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        footer.Children.Add(confirmation); footer.Children.Add(error);
        var actions = new WrapPanel();
        var accept = new Button { Content = "Geprüften Stand übernehmen" };
        accept.Click += (_, _) =>
        {
            try { Accept(confirmation.IsChecked == true); DialogResult = true; }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        actions.Children.Add(accept); actions.Children.Add(cancel); footer.Children.Add(actions);
        root.Children.Add(footer);
        var detail = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = Describe(current.Policy, next.Policy) };
        root.Children.Add(detail); Content = root;
        MainWindow.InitializeAutomationContract(this, "GroupPolicyUpdate");
    }

    internal void Accept(bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("Bitte die Übernahme des geprüften Standes bestätigen.");
        store.AcceptPackageUpdate(candidate, checkpoint);
    }

    private static string Describe(OfflineGroupPolicy before, OfflineGroupPolicy after)
    {
        var lines = new List<string> { $"Bibliothek: {after.LibraryId:D}", $"Revision {before.Revision} → {after.Revision}",
            "Signatur gegen den bereits bestätigten Verwaltungsschlüssel geprüft.\n",
            "Der neue Stand ersetzt die folgenden lokalen Gruppen- und Personeninformationen. Vault-Dateien werden dadurch noch nicht neu verschlüsselt; alte Kopien bleiben zugänglich.\n" };
        void AddPolicy(string title, OfflineGroupPolicy policy)
        {
            lines.Add(title);
            foreach (var group in policy.Groups)
                lines.Add($"Gruppe {group.Name} ({group.Id}): " + string.Join(", ", group.Members.Select(id => policy.People.FirstOrDefault(p => p.Id == id)?.Name + " (" + id + ")")));
            foreach (var person in policy.People)
            {
                lines.Add($"Person {person.Name} ({person.Id}): {(person.Enabled ? "aktiv" : "inaktiv")}");
                foreach (var device in person.Devices) lines.Add($"  Gerät {device.Name}: {(device.Enabled ? "aktiv" : "inaktiv")} · {device.Fingerprint}");
            }
            foreach (var grant in policy.Delegations) lines.Add($"Vollmacht {grant.Id}: {grant.PersonId} · {(grant.Enabled ? "aktiv" : "entzogen")} · {string.Join(", ", grant.GroupIds)} · Schlüssel {OfflineGroupTrustStore.AdministratorFingerprint(grant.SigningPublicKey)}");
            foreach (var device in policy.RecoveryDevices) lines.Add($"Separater Recovery-Zugang {device.Name}: {(device.Enabled ? "aktiv" : "inaktiv")} · {device.Fingerprint}");
        }
        AddPolicy("BISHER", before); lines.Add(""); AddPolicy("NEUER STAND", after);
        return string.Join(Environment.NewLine, lines);
    }
}

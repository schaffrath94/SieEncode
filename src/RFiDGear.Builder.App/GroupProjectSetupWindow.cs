using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupProjectSetupWindow : Window
{
    private sealed record GroupChoice(string Id, string Label);
    private readonly string directory;
    private readonly ComboBox libraries = new();
    private readonly ComboBox groups = new() { DisplayMemberPath = nameof(GroupChoice.Label) };
    private readonly TextBox name = new();
    private readonly PasswordBox legacyPassphrase = new();
    private readonly CheckBox confirmed = new() { Content = new TextBlock { Text = "Neue Projektkopie mit eigener Projekt-ID und Gruppenfreigabe erstellen. Originale und alte Vaultkopien bleiben erhalten und weiter entschlüsselbar.", TextWrapping = TextWrapping.Wrap } };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Func<Guid, string, GroupPolicyCheckpoint, string, string?, GroupProjectCreationResult> create;
    private VerifiedGroupPolicy? selected;
    internal GroupProjectCreationResult? Result { get; private set; }

    internal GroupProjectSetupWindow(Window owner, string localDirectory, string proposedName, bool migration,
        Func<Guid, string, GroupPolicyCheckpoint, string, string?, GroupProjectCreationResult> create)
    {
        Owner = owner; directory = localDirectory; this.create = create;
        Title = migration ? "Vorhandenen Vault als Gruppenprojekt kopieren" : "Neues Gruppenprojekt erstellen";
        Width = 760; SizeToContent = SizeToContent.Height; MaxHeight = 800; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("Registrierte Bibliothek und Gruppe wählen. Die zentrale Richtlinie bestimmt alle normalen Empfänger und separaten Recovery-Zugänge. Eigener normaler Gruppenzugang und mindestens ein aktiver Recovery-Zugang sind erforderlich."));
        root.Children.Add(libraries); root.Children.Add(groups);
        root.Children.Add(Note("Neuer eindeutiger Projektname (zugleich Ordnername)")); name.Text = proposedName; root.Children.Add(name);
        if (migration)
        {
            root.Children.Add(Note("Passphrase des alten Vaults, falls benötigt. Bei persönlicher Zertifikatsfreigabe leer lassen. Verwendet wird der gespeicherte Originalstand; ungespeicherte Änderungen vorher speichern oder verwerfen."));
            root.Children.Add(legacyPassphrase);
        }
        root.Children.Add(confirmed);
        var execute = new Button { Content = migration ? "Gespeicherten Vault geprüft in neue Gruppenprojektkopie übernehmen" : "Projekt und Gruppen-Vault gemeinsam erstellen" };
        execute.Click += (_, _) =>
        {
            try { CreateSelected(name.Text, legacyPassphrase.Password, confirmed.IsChecked == true); DialogResult = true; }
            catch (Exception e) { status.Foreground = Brushes.Firebrick; status.Text = e.Message; }
            finally { legacyPassphrase.Clear(); }
        };
        root.Children.Add(execute); root.Children.Add(status);
        var cancel = new Button { Content = "Abbrechen" }; cancel.Click += (_, _) => Close(); root.Children.Add(cancel);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        libraries.SelectionChanged += (_, _) =>
        {
            selected = null; groups.ItemsSource = null;
            if (libraries.SelectedItem is not Guid id) return;
            try { LoadLibrary(id); } catch (Exception e) { status.Foreground = Brushes.Firebrick; status.Text = e.Message; }
        };
        libraries.ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [];
        libraries.SelectedIndex = libraries.Items.Count == 1 ? 0 : -1;
        Closed += (_, _) => legacyPassphrase.Clear();
        MainWindow.InitializeAutomationContract(this, "GroupProjectSetup");
    }

    internal void LoadLibrary(Guid id)
    {
        selected = null; groups.ItemsSource = null;
        var trust = new OfflineGroupTrustStore(Path.Combine(directory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
        var current = trust.LoadOffline();
        if (current.Policy.Version != 2) throw new InvalidOperationException("Registriertes Personenverzeichnis erforderlich.");
        selected = current;
        groups.ItemsSource = current.Policy.Groups.Select(g => new GroupChoice(g.Id, $"{g.Name} · {g.Members.Length} zugeordnete Personen · {g.Id}")).ToArray();
        groups.SelectedIndex = groups.Items.Count == 1 ? 0 : -1;
        status.Foreground = Brushes.Black;
        status.Text = $"Bibliothek {id:D} · registrierte Revision {current.Policy.Revision} · {current.Policy.RecoveryDevices.Count(d => d.Enabled)} aktive Recovery-Zugänge. Mitglieder können mehreren Gruppen angehören.";
    }

    internal GroupProjectCreationResult CreateSelected(string projectName, string? phrase, bool confirmation)
    {
        if (!confirmation) throw new InvalidDataException("Erstellung der neuen Gruppenprojektkopie muss bestätigt werden.");
        if (Result is not null) throw new InvalidOperationException("Projekt wurde bereits erstellt.");
        if (selected is null || groups.SelectedItem is not GroupChoice group) throw new InvalidOperationException("Bitte registrierte Bibliothek und Gruppe auswählen.");
        return Result = create(selected.Policy.LibraryId, group.Id, selected.Checkpoint, projectName, string.IsNullOrEmpty(phrase) ? null : phrase);
    }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

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
    private readonly TextBlock nextAction = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private Button? executeAction;
    private readonly Func<Guid, string, GroupPolicyCheckpoint, string, string?, GroupProjectCreationResult> create;
    private VerifiedGroupPolicy? selected;
    internal GroupProjectCreationResult? Result { get; private set; }

    internal GroupProjectSetupWindow(Window owner, string localDirectory, string proposedName, bool migration,
        Func<Guid, string, GroupPolicyCheckpoint, string, string?, GroupProjectCreationResult> create)
    {
        Owner = owner; directory = localDirectory; this.create = create;
        Title = migration ? "Neues Gruppenprojekt aus gespeichertem Entwurf" : "Neues Gruppenprojekt erstellen";
        Width = 760; SizeToContent = SizeToContent.Height; MaxHeight = 800; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note(migration
            ? "Dieser technische Kopierweg betrifft einen bereits gespeicherten Entwicklungsentwurf. Er ist für die normale Neuanlage von SieEncode 1.0 nicht erforderlich."
            : "Neues Teamprojekt: eine signiert registrierte Bibliothek und genau eine Gruppe auswählen. Kartenschlüssel werden ausschließlich für dieses neue Projekt verschlüsselt gespeichert. Der eigene Gruppenzugang und ein separater Recovery-Zugang müssen freigegeben sein."));
        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 246, 247)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(183, 204, 207)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8),
            Child = nextAction
        });
        root.Children.Add(Note("1 · Registrierte Bibliothek wählen"));
        root.Children.Add(libraries);
        root.Children.Add(Note("2 · Für dieses Projekt freigegebene Gruppe auswählen"));
        root.Children.Add(groups);
        root.Children.Add(Note("3 · Neuer eindeutiger Projektname (zugleich Ordnername)")); name.Text = proposedName; root.Children.Add(name);
        confirmed.Content = new TextBlock
        {
            Text = migration
                ? "Aus dem gespeicherten Entwicklungsentwurf eine unabhängige neue Projektkopie mit eigener ID und Gruppenfreigabe erstellen. Ursprungsentwurf und seine Sicherungen bleiben unverändert."
                : "Das neue Gruppenprojekt mit eigener Projekt-ID und dem ausgewählten Gruppen-Vault ausdrücklich anlegen. Noch keine Karte kodieren.",
            TextWrapping = TextWrapping.Wrap
        };
        if (migration)
        {
            root.Children.Add(Note("Passphrase des gespeicherten persönlichen Projekt-Vaults, falls benötigt. Bei Windows-Zertifikatsfreigabe leer lassen. Der Entwurf muss zuvor vollständig gespeichert worden sein."));
            root.Children.Add(legacyPassphrase);
        }
        root.Children.Add(confirmed);
        var execute = new Button
        {
            Content = migration ? "Gespeicherten Vault in neue Gruppenprojektkopie übernehmen" : "Gruppenprojekt sicher erstellen",
            IsEnabled = false
        };
        executeAction = execute;
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
            RefreshSelection();
            if (libraries.SelectedItem is not Guid id) return;
            try { LoadLibrary(id); }
            catch (Exception e) { status.Foreground = Brushes.Firebrick; status.Text = e.Message; RefreshSelection(); }
        };
        groups.SelectionChanged += (_, _) => RefreshSelection();
        libraries.ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [];
        libraries.SelectedIndex = libraries.Items.Count == 1 ? 0 : -1;
        RefreshSelection();
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
        groups.ItemsSource = current.Policy.Groups.Select(g => new GroupChoice(g.Id, $"{g.Name} · {g.Members.Length} zugeordnete Personen")).ToArray();
        groups.SelectedIndex = groups.Items.Count == 1 ? 0 : -1;
        status.Foreground = Brushes.Black;
        status.Text = $"Registrierter Gruppenstand: Revision {current.Policy.Revision} · {current.Policy.RecoveryDevices.Count(d => d.Enabled)} aktive Recovery-Zugänge. Bibliothek: {id:D}. Ein gespeicherter Gruppenstand allein erteilt keine Zugriffsrechte.";
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (selected is null)
        {
            nextAction.Text = libraries.Items.Count == 0
                ? "Noch keine Gruppenbibliothek registriert. Zuerst unter Einstellungen den signierten Gruppenstand der Hauptverwaltung übernehmen."
                : "Bitte die richtige registrierte Bibliothek wählen. Bei mehreren Bibliotheken keine Vorauswahl.";
        }
        else if (groups.SelectedItem is not GroupChoice group)
        {
            nextAction.Text = "Bibliothek geladen. Bitte die Gruppe auswählen, deren Mitglieder dieses Projekt nutzen dürfen. Keine Gruppe wird automatisch zugeteilt.";
        }
        else
        {
            var membership = selected.Policy.Groups.FirstOrDefault(g => g.Id == group.Id);
            var hasMember = membership is { Members.Length: > 0 };
            var hasRecovery = selected.Policy.RecoveryDevices.Any(device => device.Enabled);
            nextAction.Text = !hasMember
                ? "In dieser Gruppe ist noch keine freigegebene Person eingetragen. Die Hauptverwaltung muss zuerst Mitgliedschaft und berechtigtes Gerät signiert freigeben; danach Gruppenstand aktualisieren."
                : !hasRecovery
                    ? "Es fehlt ein separater aktiver Recovery-Zugang. Die Hauptverwaltung muss ihn getrennt einrichten, freigeben und den neuen signierten Gruppenstand verteilen."
                    : $"Gruppe: {group.Label}. Jetzt den Projektnamen prüfen und die Erstellung ausdrücklich bestätigen. Der tatsächliche Zugriff mit dem privaten Windows-Schlüssel wird erst beim Erstellen geprüft.";
        }
        if (executeAction is not null)
        {
            var chosen = groups.SelectedItem as GroupChoice;
            executeAction.IsEnabled = selected is not null && chosen is not null &&
                selected.Policy.Groups.Any(g => g.Id == chosen.Id && g.Members.Length > 0) &&
                selected.Policy.RecoveryDevices.Any(d => d.Enabled);
        }
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

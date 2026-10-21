using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupAdministratorTransitionWindow : Window
{
    private readonly SignedAdministratorTransition candidate;
    private readonly string directory;
    private OfflineGroupTrustStore? store;
    private GroupPolicyCheckpoint? checkpoint;
    private readonly TextBox fingerprint = new();
    private readonly CheckBox confirmation = new() { Content = new TextBlock
    {
        Text = "Bibliotheks-ID und neuen Fingerabdruck unabhängig bestätigt; vorhandene Vollmachten sollen unverändert bleiben.",
        TextWrapping = TextWrapping.Wrap
    }};
    private readonly TextBox detail = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 170 };
    private readonly TextBlock error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly Button accept = new() { Content = "Bestätigten Verwaltungswechsel übernehmen", IsEnabled = false };

    internal GroupAdministratorTransitionWindow(Window owner, SignedAdministratorTransition transition, string localDirectory)
    {
        candidate = GroupAdministratorTransition.Read(GroupAdministratorTransition.Write(transition));
        directory = Path.GetFullPath(localDirectory);
        Owner = owner; Title = "Verwaltungsschlüssel wechseln"; Width = 860; Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("Nur einen angekündigten Wechsel übernehmen. Die beiden Signaturen werden gegen die vorhandene lokale Registrierung geprüft. Der angezeigte neue Fingerabdruck muss zusätzlich unabhängig vom Dateiübertragungsweg bestätigt werden."));
        root.Children.Add(Note("Bereits registrierte Bibliothek auswählen (Bibliotheks-ID):"));
        var libraries = new ComboBox();
        libraries.ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [];
        root.Children.Add(libraries); root.Children.Add(detail);
        root.Children.Add(Note("Unabhängig erhaltenen neuen SHA-256-Fingerabdruck eingeben (64 Hex-Zeichen):"));
        root.Children.Add(fingerprint); root.Children.Add(confirmation);
        root.Children.Add(Note("Nur die lokale Registrierung dieses Windows-Benutzers wird geändert. Andere Arbeitsplätze müssen den Wechsel selbst importieren. Alte Vaultkopien bleiben zugänglich. Private Verwaltungsschlüssel werden hier weder übertragen noch ersetzt; für den Hauptverwaltungsplatz ist zusätzlich die private Schlüsselübergabe erforderlich."));
        root.Children.Add(error);
        var actions = new WrapPanel();
        accept.Click += (_, _) =>
        {
            try { Accept(fingerprint.Text, confirmation.IsChecked == true); DialogResult = true; }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        actions.Children.Add(accept);
        var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        actions.Children.Add(cancel); root.Children.Add(actions);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        libraries.SelectionChanged += (_, _) =>
        {
            if (libraries.SelectedItem is not Guid id) return;
            try { SelectLibrary(id); } catch (Exception exception) { error.Text = exception.Message; }
        };
        if (libraries.Items.Count == 1) libraries.SelectedIndex = 0;
        else if (libraries.Items.Count == 0) error.Text = "Keine lokale Registrierung vorhanden. Dialog schließen und zuerst die bisherige Gruppenverwaltung registrieren. Ein Wechsel ist keine Erstregistrierung.";
        MainWindow.InitializeAutomationContract(this, "GroupAdministratorTransition");
    }

    internal void SelectLibrary(Guid library)
    {
        // A failed new selection cannot retain the previously eligible store or confirmation.
        store = null; checkpoint = null; accept.IsEnabled = false;
        confirmation.IsChecked = false; fingerprint.Clear(); detail.Clear(); error.Text = "";
        var selected = new OfflineGroupTrustStore(Path.Combine(directory, library.ToString("N") + ".trust.bin"),
            library, new WindowsGroupTrustProtection(library));
        var current = selected.ExportPublic();
        var previous = GroupEnrollmentPackages.Validate(current);
        var next = GroupAdministratorTransition.Inspect(candidate, current);
        var after = GroupEnrollmentPackages.Validate(next);
        detail.Text = $"Bibliothek: {library:D}\nRevision: {previous.Policy.Revision} → {after.Policy.Revision}\n\nBisheriger Fingerabdruck:\n{OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey)}\n\nNeuer Fingerabdruck (unabhängig bestätigen):\n{OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey)}\n\nBeide Signaturen gültig. Personen, Geräte, Gruppen, Recovery-Zugänge und Vollmachten bleiben unverändert.";
        store = selected; checkpoint = previous.Checkpoint; accept.IsEnabled = true;
    }

    internal void Accept(string independentlyConfirmedFingerprint, bool confirmed)
    {
        if (store is null || checkpoint is null) throw new InvalidOperationException("Zuerst eine passende registrierte Bibliothek auswählen.");
        var normalized = string.Concat(independentlyConfirmedFingerprint.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        store.AcceptAdministratorTransition(candidate, checkpoint, normalized, confirmed);
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

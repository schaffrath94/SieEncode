using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupSuccessorSetupWindow : Window
{
    private readonly string directory;
    private Guid? selected;
    private OfflineGroupSuccessorStore? preparation;
    private bool backupCompleted;
    private readonly TextBox fingerprintDisplay = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };

    internal GroupSuccessorSetupWindow(Window owner, string trustDirectory)
    {
        Owner = owner; Title = "Nachfolge der Hauptverwaltung vorbereiten"; Width = 800; Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        directory = Path.GetFullPath(trustDirectory);
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("Nur für einen vereinbarten Wechsel der Hauptverwaltung. Zuerst die bisherige Bibliothek registrieren und ihren aktuellen Stand übernehmen. Diese Vorbereitung verleiht noch keine Verwaltungsrechte."));
        root.Children.Add(Note("Bereits registrierte Bibliothek (Bibliotheks-ID):"));
        var libraries = new ComboBox { ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [] };
        root.Children.Add(libraries);
        libraries.SelectionChanged += (_, _) => { if (libraries.SelectedItem is Guid id) Run(() => SelectLibrary(id)); };
        root.Children.Add(Action("1. Neuen Nachfolgeschlüssel vorbereiten", () =>
        {
            if (MessageBox.Show(this, "Einen neuen privaten Nachfolgeschlüssel für die ausgewählte Bibliothek vorbereiten? Vorhandene Vorbereitungen werden nicht ersetzt.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes) Prepare();
        }));
        root.Children.Add(Action("2. Private Sicherung erstellen", () => CreateBackupWindow().ShowDialog()));
        root.Children.Add(Action("3. Öffentliche Anfrage exportieren", () =>
        {
            _ = RequestForExport();
            var dialog = new SaveFileDialog { Title = "Öffentliche Nachfolgeranfrage als neue Datei speichern", Filter = "Nachfolgeranfrage (*.group-successor.json)|*.group-successor.json", FileName = selected!.Value.ToString("N") + ".group-successor.json" };
            if (dialog.ShowDialog(this) == true) ExportRequest(dialog.FileName);
        }));
        root.Children.Add(Action("4. Nach importierter Übergabe lokal aktivieren", ShowActivation));
        root.Children.Add(Note("Neuer SHA-256-Fingerabdruck (separat bestätigen):"));
        root.Children.Add(fingerprintDisplay); root.Children.Add(status); root.Children.Add(error);
        root.Children.Add(Note("Die Vorbereitung bleibt nach dem Schließen erhalten. Für einen erneuten Anfrageexport in einer neuen Sitzung zuerst eine neue private Sicherung speichern. Private Sicherung getrennt aufbewahren; nur die öffentliche Anfrage an die bisherige Hauptverwaltung geben und den neuen Fingerabdruck separat bestätigen."));
        root.Children.Add(Note("Danach autorisiert die bisherige Hauptverwaltung die Anfrage. Die zurückgegebene öffentliche Übergabe muss ausdrücklich importiert werden. Die Aktivierung des privaten Schlüssels ist ein zusätzlicher Schritt; vorhandene Verwaltungsdateien nicht löschen oder überschreiben. Alte Offline-Kopien werden nicht gesperrt."));
        root.Children.Add(Action("Schließen", Close));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (libraries.Items.Count == 1) libraries.SelectedIndex = 0;
        if (libraries.Items.Count == 0) error.Text = "Keine Bibliothek registriert. Zuerst die bisherige Gruppenverwaltung registrieren.";
        MainWindow.InitializeAutomationContract(this, "GroupSuccessorSetup");
    }

    private string PendingPath(Guid id) => Path.Combine(directory, "Successors", id.ToString("N") + ".successor.bin");
    private OfflineGroupTrustStore Trust(Guid id) => new(Path.Combine(directory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));

    internal void SelectLibrary(Guid id)
    {
        selected = null; preparation = null; backupCompleted = false; status.Text = ""; error.Text = ""; fingerprintDisplay.Clear();
        _ = Trust(id).LoadOffline();
        selected = id;
        if (File.Exists(PendingPath(id)))
        {
            preparation = new(PendingPath(id), id, new WindowsGroupSuccessorProtection(id));
            ShowFingerprint();
        }
        else status.Text = "Noch keine Vorbereitung. Mit Schritt 1 beginnen.";
    }

    internal void Prepare()
    {
        if (selected is not Guid id) throw new InvalidOperationException("Zuerst eine registrierte Bibliothek auswählen.");
        var trust = Trust(id);
        var pending = new OfflineGroupSuccessorStore(PendingPath(id), id, new WindowsGroupSuccessorProtection(id));
        pending.Create(trust, trust.LoadOffline().Checkpoint);
        preparation = pending; backupCompleted = false; ShowFingerprint();
    }

    private void ShowFingerprint()
    {
        var current = Trust(selected!.Value).ExportPublic();
        var fingerprint = preparation!.GetFingerprint();
        fingerprintDisplay.Text = fingerprint;
        if (fingerprint == OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey))
        {
            status.Text = "Der vorbereitete Schlüssel ist bereits als neue Verwaltung registriert. Jetzt Schritt 4 ausführen bzw. eine abgeschlossene Aktivierung bestätigen. Die Vorbereitung bleibt erhalten.";
            return;
        }
        var next = GroupAdministratorTransition.InspectRequest(preparation!.ExportRequest(Trust(selected.Value)), current);
        fingerprintDisplay.Text = OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey);
        status.Text = "Vorbereitung vorhanden. Noch keine aktive Verwaltung. Als Nächstes private Sicherung erstellen.";
    }

    internal GroupPrivateBackupWindow CreateBackupWindow()
    {
        var pending = preparation ?? throw new InvalidOperationException("Zuerst einen Nachfolgeschlüssel vorbereiten oder eine vorhandene Vorbereitung auswählen.");
        var id = selected;
        var window = new GroupPrivateBackupWindow(this, [new($"Nachfolge · {id:D}", pending.ExportPrivateBackup, null)], false, "Nachfolgeschlüssel");
        window.Closed += (_, _) =>
        {
            if (selected == id && ReferenceEquals(preparation, pending) && window.ExportCompleted)
            { backupCompleted = true; status.Text = "Private Sicherung gespeichert und geprüft. Jetzt die öffentliche Anfrage exportieren."; }
        };
        return window;
    }

    private AdministratorTransitionRequest RequestForExport()
    {
        if (!backupCompleted || preparation is null || selected is not Guid id)
            throw new InvalidOperationException("Zuerst die private Sicherung in dieser Sitzung erfolgreich speichern und den Sicherungsdialog schließen.");
        return preparation.ExportRequest(Trust(id));
    }

    internal void ExportRequest(string destination)
    {
        _ = RequestForExport();
        ConcurrentFileWriter.Write(destination, FileRevision.Missing, temporary =>
        {
            File.WriteAllBytes(temporary, GroupAdministratorTransition.WriteRequest(RequestForExport()));
        });
        status.Text = "Öffentliche Anfrage exportiert. An die bisherige Hauptverwaltung geben und den neuen Fingerabdruck unabhängig bestätigen. Private Sicherung bleibt bei Ihnen.";
    }

    private void ShowActivation()
    {
        if (selected is not Guid id || preparation is null) throw new InvalidOperationException("Zuerst die vorbereitete Bibliothek auswählen.");
        var current = Trust(id).ExportPublic();
        var fingerprint = preparation.GetFingerprint();
        if (fingerprint != OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey))
            throw new InvalidOperationException("Zuerst den angekündigten Verwaltungswechsel in den Einstellungen importieren. Danach diesen Dialog erneut öffnen.");
        var checkpoint = GroupEnrollmentPackages.Validate(current).Checkpoint;
        if (MessageBox.Show(this, $"Bibliothek: {id:D}\nNeuer Fingerabdruck: {fingerprint}\n\nDen vorbereiteten Schlüssel auf diesem Windows-Benutzer als Hauptverwaltung aktivieren? Eine vorhandene passende Vorgängerverwaltung wird vorher geschützt archiviert. Eine fremde Verwaltung wird nicht ersetzt. Archiv und Vorbereitung bleiben erhalten; alte Schlüsselkopien werden nicht gesperrt.", "Nachfolge lokal aktivieren", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Activate(checkpoint, true);
    }

    internal void Activate(GroupPolicyCheckpoint expected, bool confirmed)
    {
        if (selected is not Guid id || preparation is null) throw new InvalidOperationException("Zuerst die vorbereitete Bibliothek auswählen.");
        var path = Path.Combine(directory, "Administrations", id.ToString("N") + ".admin.bin");
        var active = new OfflineGroupAdministrationStore(path, id, new WindowsGroupTrustProtection(id, true));
        active.ActivateSuccessor(preparation, Trust(id), expected, confirmed);
        status.Text = "Neue Hauptverwaltung lokal aktiv. Diesen Dialog schließen und die Gruppenverwaltung öffnen. Vorbereitung und gegebenenfalls geschütztes Vorgängerarchiv bleiben erhalten. Andere Arbeitsplätze müssen die öffentliche Übergabe weiterhin selbst importieren.";
    }

    private void Run(Action action) { try { error.Text = ""; action(); } catch (Exception exception) { error.Text = exception.Message; } }
    private Button Action(string label, Action action) { var button = new Button { Content = label }; button.Click += (_, _) => Run(action); return button; }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

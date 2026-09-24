using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupAdministrationDraft
{
    public string Id { get; set; } = "gruppe-" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Neue Gruppe";
    public ObservableCollection<string> Members { get; } = [];
}

internal sealed partial class GroupAdministrationWindow : Window
{
    private readonly string localDirectory;
    private readonly ComboBox libraries = new() { MinWidth = 360 };
    private readonly ObservableCollection<GroupAdministrationDraft> groups = [];
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 140, MaxHeight = 220 };
    private readonly ListBox members = new() { MinHeight = 80, MaxHeight = 160 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private OfflineGroupAdministrationStore? store;
    private GroupPolicyCheckpoint? checkpoint;
    private bool dirty;
    private bool loading;
    private Guid? currentLibrary;

    public GroupAdministrationWindow(Window owner, string localTrustDirectory)
    {
        Owner = owner; Title = "Offline-Gruppen verwalten · Entwicklung"; Width = 880; MaxHeight = 850; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        localDirectory = Path.Combine(Path.GetFullPath(localTrustDirectory), "Administrations");
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("Hier verwalten Sie Bibliotheken, deren privater Verwaltungsschlüssel auf diesem Windows-Benutzer liegt. Registrierung allein verleiht keine Verwaltungsrechte. Sichern Sie neue Verwaltungsschlüssel separat über ‚Privaten Schlüssel sichern‘."));
        root.Children.Add(libraries);
        libraries.SelectionChanged += (_, _) =>
        {
            if (loading || libraries.SelectedItem is not Guid id) return;
            if (!ConfirmDiscard()) { loading = true; libraries.SelectedItem = currentLibrary; loading = false; return; }
            try { error.Text = ""; LoadAdministration(id); }
            catch (Exception exception)
            {
                error.Text = exception.Message;
                loading = true; libraries.SelectedItem = currentLibrary; loading = false;
            }
        };
        root.Children.Add(Action("Neue Verwaltung erstellen", () =>
        {
            if (ConfirmDiscard() && MessageBox.Show(this, "Eine neue unabhängige Bibliothek mit eigenem Verwaltungsschlüssel erstellen? Für bestehende Teams deren Registrierung verwenden. Den neuen Schlüssel anschließend über ‚Privaten Schlüssel sichern‘ separat sichern.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                CreateAdministration();
        }));
        root.Children.Add(Action("Privaten Schlüssel sichern", ShowPrivateBackup));
        root.Children.Add(Action("Privaten Schlüssel wiederherstellen", ShowPrivateRestore));
        AddPeopleControls(root);
        grid.Columns.Add(new DataGridTextColumn { Header = "Gruppe", Binding = new Binding(nameof(GroupAdministrationDraft.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Stabile Gruppen-ID", Binding = new Binding(nameof(GroupAdministrationDraft.Id)), IsReadOnly = true, Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.ItemsSource = groups;
        grid.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Commit) dirty = true; };
        grid.SelectionChanged += (_, _) => RefreshMemberLabels();
        root.Children.Add(grid);
        root.Children.Add(Action("Gruppe hinzufügen", () => { RequireStore(); var group = new GroupAdministrationDraft(); groups.Add(group); grid.SelectedItem = group; dirty = true; }));
        root.Children.Add(Note("Mitglieder der ausgewählten Gruppe. Eine Person darf mehreren Gruppen angehören; ihre Geräte werden zentral bei der Person gepflegt."));
        root.Children.Add(members);
        var memberActions = new WrapPanel();
        memberActions.Children.Add(Action("Ausgewählte Person zur Gruppe hinzufügen", AddSelectedPerson));
        memberActions.Children.Add(Action("Öffentliche Einladung hinzufügen", ImportMember));
        memberActions.Children.Add(Action("Mitglied aus Gruppe entfernen", () =>
        {
            if (grid.SelectedItem is GroupAdministrationDraft group && members.SelectedItem is GroupMemberLabel selected &&
                MessageBox.Show(this, "Diesen Zugang aus dem Gruppenentwurf entfernen? Alte Vaultkopien bleiben zugänglich.", Title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { group.Members.Remove(selected.Id); dirty = true; RefreshMemberLabels(); }
        }));
        root.Children.Add(memberActions);
        AddDelegationControls(root);
        AddRecoveryControls(root);
        var actions = new WrapPanel();
        actions.Children.Add(Action("Gruppen signiert speichern", SaveGroups));
        actions.Children.Add(Action("Gespeicherten Stand öffentlich exportieren", ExportPublic));
        root.Children.Add(actions); root.Children.Add(status); root.Children.Add(error);
        root.Children.Add(Action("Schließen", Close));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        RefreshLibraries(); MainWindow.InitializeAutomationContract(this, "GroupAdministration");
    }

    internal Guid CreateAdministration()
    {
        var id = Guid.NewGuid();
        Store(id).Create();
        dirty = false; RefreshLibraries(); LoadAdministration(id);
        return id;
    }

    internal void LoadAdministration(Guid id)
    {
        var candidate = Store(id);
        var package = candidate.ExportPublic();
        var verified = GroupEnrollmentPackages.Validate(package);
        LoadPeople(verified.Policy);
        groups.Clear();
        foreach (var group in verified.Policy.Groups)
        {
            var draft = new GroupAdministrationDraft { Id = group.Id, Name = group.Name };
            foreach (var member in group.Members) draft.Members.Add(member);
            groups.Add(draft);
        }
        store = candidate; checkpoint = verified.Checkpoint; currentLibrary = id;
        loading = true; libraries.SelectedItem = id; loading = false; dirty = false;
        RefreshMemberLabels();
        RefreshDelegations(verified.Policy);
        RefreshRecovery(verified.Policy);
        status.Text = $"Bibliothek: {id:D}\nGespeicherte Revision: {checkpoint.Revision}\nVerwaltungsfingerabdruck: {OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey)}";
    }

    internal void SaveGroups()
    {
        RequireStore(); CommitEdits();
        var groupRows = groups.Select(g => new OfflineVaultGroup(g.Id, g.Name, g.Members.ToArray())).ToArray();
        if (!personMode && people.Count > 0) throw new InvalidOperationException("Bitte zuerst die bisherigen Gruppen auf Personen umstellen.");
        var package = personMode ? store!.UpdateDirectory(PersonRows(), groupRows, checkpoint!) : store!.UpdateGroups(groupRows, checkpoint!);
        checkpoint = GroupEnrollmentPackages.Validate(package).Checkpoint;
        dirty = false; status.Text = $"Bibliothek: {package.LibraryId:D} · Revision {checkpoint.Revision} signiert gespeichert. Zum Weitergeben den gespeicherten Stand exportieren.";
        RefreshMemberLabels();
    }

    private void ExportPublic()
    {
        RequireStore();
        CommitEdits();
        if (dirty) throw new InvalidOperationException("Bitte Änderungen zuerst signiert speichern oder den Dialog neu laden.");
        var bytes = GroupEnrollmentPackages.Write(store!.ExportPublic());
        var dialog = new SaveFileDialog { Title = "Nur öffentliche Verwaltungsdaten exportieren", Filter = "Gruppenregistrierung (*.group-enrollment.json)|*.group-enrollment.json", FileName = currentLibrary!.Value.ToString("N") + ".group-enrollment.json" };
        if (dialog.ShowDialog(this) != true) return;
        var revision = ConcurrentFileWriter.Capture(dialog.FileName);
        ConcurrentFileWriter.Write(dialog.FileName, revision, temporary => File.WriteAllBytes(temporary, bytes));
        status.Text = "Öffentliche Registrierung exportiert. Bibliotheks-ID und Verwaltungsfingerabdruck den Kollegen unabhängig vom Dateiübertragungsweg bestätigen. Kein privater Schlüssel exportiert.";
    }

    private void ImportMember()
    {
        if (personMode) { ImportPersonDevice(); return; }
        if (grid.SelectedItem is not GroupAdministrationDraft group) throw new InvalidOperationException("Bitte zuerst eine Gruppe auswählen.");
        var dialog = new OpenFileDialog { Filter = "Empfängereinladung (*.recipient.json)|*.recipient.json" };
        if (dialog.ShowDialog(this) != true) return;
        var invitation = WindowsVaultIdentityService.ImportInvitation(dialog.FileName);
        using var certificate = invitation.Certificate;
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        if (MessageBox.Show(this, $"{invitation.DisplayName}\n{fingerprint}\n\nFingerabdruck unabhängig mit der Person abgeglichen?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (!group.Members.Contains(fingerprint)) { group.Members.Add(fingerprint); dirty = true; RefreshMemberLabels(); }
    }

    private OfflineGroupAdministrationStore Store(Guid id) => new(Path.Combine(localDirectory, id.ToString("N") + ".admin.bin"), id, new WindowsGroupTrustProtection(id, signingAuthority: true));
    private void RefreshLibraries()
    {
        loading = true;
        libraries.ItemsSource = Directory.Exists(localDirectory) ? Directory.EnumerateFiles(localDirectory, "*.admin.bin")
            .Select(path => Path.GetFileName(path).Replace(".admin.bin", "", StringComparison.Ordinal)).Where(name => Guid.TryParseExact(name, "N", out _)).Select(Guid.Parse).ToArray() : [];
        loading = false;
    }
    private void RequireStore() { if (store is null || checkpoint is null) throw new InvalidOperationException("Bitte eine Verwaltung auswählen oder erstellen."); }
    private bool ConfirmDiscard() => !dirty || MessageBox.Show(this, "Ungespeicherte Gruppenänderungen verwerfen?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private void Run(Action action) { try { error.Text = ""; action(); } catch (Exception e) { error.Text = e.Message; } }
    private Button Action(string label, Action action) { var button = new Button { Content = label }; button.Click += (_, _) => Run(action); return button; }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

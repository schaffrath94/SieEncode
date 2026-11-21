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
    public required string Name { get; set; }
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
        grid.Name = "GroupsGrid"; members.Name = "MembersList";
        peopleGrid.Name = "PeopleGrid"; deviceGrid.Name = "DevicesGrid";
        delegationList.Name = "DelegationsList";
        Owner = owner; Title = "Hauptverwaltung · Personen und Gruppen"; Width = 880; MaxHeight = 850; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        localDirectory = Path.Combine(Path.GetFullPath(localTrustDirectory), "Administrations");
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("Hauptverwaltung: Personen und Gruppen freigeben, Änderungen signiert speichern und den neuen öffentlichen Stand an Kollegen weitergeben. Die Verwaltung ist nur mit dem privaten Schlüssel dieses Windows-Benutzers möglich."));
        root.Children.Add(Note("Ablauf: 1 · Bestehende Bibliothek wählen  →  2 · Kollegen oder Gruppen zuordnen  →  3 · Änderung signiert speichern  →  4 · neuen öffentlichen Stand an die Kollegen geben. Jeder Kollege muss ihn ausdrücklich importieren. Ein privater Verwaltungsschlüssel wird nie mitgegeben."));
        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 246, 247)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(183, 204, 207)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12), Margin = new Thickness(0, 5, 0, 12),
            Child = status
        });
        status.Text = "Bitte eine bestehende Verwaltung wählen. Eine neue unabhängige Bibliothek nur anlegen, wenn noch keine für dieses Team vorhanden ist.";
        root.Children.Add(Note("1 · Meine verwaltete Bibliothek auswählen (bei genau einer automatisch geladen)"));
        root.Children.Add(libraries);
        libraries.SelectionChanged += (_, _) =>
        {
            if (loading || libraries.SelectedItem is not Guid id) return;
            if (!ConfirmDiscard()) { loading = true; libraries.SelectedItem = currentLibrary; loading = false; return; }
            try { error.Text = ""; LoadAdministration(id); }
            catch (Exception exception)
            {
                ShowError(exception);
                loading = true; libraries.SelectedItem = currentLibrary; loading = false;
            }
        };
        var newAdministration = new StackPanel { Margin = new Thickness(8, 5, 8, 5) };
        newAdministration.Children.Add(Note("Nur für ein tatsächlich neues, unabhängiges Team. Für bestehende Teams deren ursprünglichen Verwaltungsschlüssel laden oder wiederherstellen. Ein neuer Schlüssel ersetzt alte Freigaben nicht."));
        newAdministration.Children.Add(Action("Neue unabhängige Hauptverwaltung erstellen", () =>
        {
            if (ConfirmDiscard() && MessageBox.Show(this, "Eine neue unabhängige Bibliothek mit eigenem Verwaltungsschlüssel erstellen und auf diesem Windows-Benutzer registrieren? Für bestehende Teams deren Registrierung verwenden. Den neuen Schlüssel anschließend über ‚Privaten Schlüssel sichern‘ separat sichern.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                CreateAdministration();
        }));
        root.Children.Add(new Expander { Header = "Nur bei Neugründung · eigene Hauptverwaltung anlegen", Content = newAdministration, IsExpanded = false });
        var advancedAdministration = new StackPanel();
        advancedAdministration.Children.Add(Action("Privaten Schlüssel sichern", ShowPrivateBackup));
        advancedAdministration.Children.Add(Action("Privaten Schlüssel wiederherstellen", ShowPrivateRestore));
        advancedAdministration.Children.Add(Action("Eigene Verwaltung lokal registrieren", ShowOwnRegistration));
        advancedAdministration.Children.Add(Action("Nachfolgeranfrage prüfen und autorisieren", ShowSuccessorAuthorization));
        root.Children.Add(new Expander { Header = "Erweitert · Schlüssel sichern, Wiederherstellung und Nachfolge", Content = advancedAdministration, IsExpanded = false });
        AddPeopleControls(root);
        grid.Columns.Add(new DataGridTextColumn { Header = "Gruppe", Binding = new Binding(nameof(GroupAdministrationDraft.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Interne Gruppen-ID", Binding = new Binding(nameof(GroupAdministrationDraft.Id)), IsReadOnly = true, Width = new DataGridLength(2, DataGridLengthUnitType.Star), Visibility = Visibility.Collapsed });
        grid.ItemsSource = groups;
        grid.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Commit) dirty = true; };
        grid.SelectionChanged += (_, _) => RefreshMemberLabels();
        root.Children.Add(Note("2 · Gruppen und Personen verwalten"));
        root.Children.Add(grid);
        root.Children.Add(Action("Gruppe hinzufügen", AddGroup));
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
        actions.Children.Add(Action("Änderungen signiert speichern", SaveGroups));
        actions.Children.Add(Action("Aktuellen Gruppenstand für Kollegen exportieren", ExportPublic));
        root.Children.Add(actions);
        root.Children.Add(Action("Schließen", Close));
        var layout = new DockPanel();
        DockPanel.SetDock(error, Dock.Bottom);
        error.Margin = new Thickness(20, 0, 20, 0);
        layout.Children.Add(error);
        layout.Children.Add(new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        RefreshLibraries();
        // Only the single pre-existing administration is loaded; never create a new key implicitly.
        if (libraries.Items.Count == 1 && libraries.Items[0] is Guid onlyLibrary)
        {
            try { LoadAdministration(onlyLibrary, synchronizeOwnRegistration: false); }
            catch (Exception exception) { ShowError(exception); }
        }
        MainWindow.InitializeAutomationContract(this, "GroupAdministration");
    }

    internal Guid CreateAdministration()
    {
        var id = Guid.NewGuid();
        var package = Store(id).Create();
        try { SynchronizeOwnRegistration(package, allowInitialEnrollment: true); }
        catch (Exception exception) { throw new InvalidOperationException("Verwaltung wurde erstellt, lokale Registrierung aber nicht abgeschlossen. Vorhandene Verwaltung laden und lokal registrieren; keinen Ersatzschlüssel erzeugen.", exception); }
        dirty = false; RefreshLibraries(); LoadAdministration(id);
        return id;
    }

    internal void LoadAdministration(Guid id, bool synchronizeOwnRegistration = true)
    {
        var candidate = Store(id);
        var package = candidate.ExportPublic();
        var verified = GroupEnrollmentPackages.Validate(package);
        // Merely opening this window must never silently replace the local trust registration.
        var registrationStatus = synchronizeOwnRegistration
            ? SynchronizeOwnRegistration(package)
            : "Lokale Registrierung beim automatischen Öffnen nicht verändert; Änderungen erst ausdrücklich signiert speichern.";
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
        status.Text = $"Gruppenstand {checkpoint.Revision} geladen.\nAls Nächstes: Personen und Gruppen prüfen bzw. bearbeiten. Änderungen signiert speichern und danach den öffentlichen Stand exportieren.\nBibliothek: {id:D}\nVerwaltungsfingerabdruck: {OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey)}\n{registrationStatus}";
    }

    private void AddGroup()
    {
        RequireStore();
        CommitEdits();
        var input = TextEntryPrompt.Show(this, "Gruppe hinzufügen", "Wie heißt die Gruppe?", "");
        if (input is not null) AddNamedGroup(input);
    }

    internal void AddNamedGroup(string input)
    {
        RequireStore();
        var name = input.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Bitte einen Gruppennamen eingeben.");
        if (groups.Any(g => string.Equals(g.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Eine Gruppe mit diesem Namen ist bereits vorhanden. Bitte einen anderen Namen wählen.");
        var group = new GroupAdministrationDraft { Name = name };
        groups.Add(group);
        grid.SelectedItem = group;
        dirty = true;
    }

    internal void SaveGroups()
    {
        RequireStore(); CommitEdits();
        var groupRows = groups.Select(g => new OfflineVaultGroup(g.Id, g.Name, g.Members.ToArray())).ToArray();
        if (!personMode && people.Count > 0) throw new InvalidOperationException("Bitte zuerst die bisherigen Gruppen auf Personen umstellen.");
        var package = personMode ? store!.UpdateDirectory(PersonRows(), groupRows, checkpoint!) : store!.UpdateGroups(groupRows, checkpoint!);
        checkpoint = GroupEnrollmentPackages.Validate(package).Checkpoint;
        dirty = false;
        try { status.Text = $"Gruppenstand {checkpoint.Revision} signiert gespeichert.\nAls Nächstes: Aktuellen Gruppenstand für Kollegen exportieren. Andere Windows-Benutzer übernehmen ihn erst nach eigenem bestätigtem Import.\n{SynchronizeOwnRegistration(package)}"; }
        catch (Exception exception) { throw new InvalidOperationException("Signierte Änderung gespeichert, lokale Registrierung aber nicht aktualisiert. Verwaltung neu laden; vorhandenes Vertrauen wird nicht ersetzt.", exception); }
        RefreshMemberLabels();
    }

    private void ExportPublic()
    {
        RequireStore();
        CommitEdits();
        if (dirty) throw new InvalidOperationException("Bitte Änderungen zuerst signiert speichern oder den Dialog neu laden.");
        var package = store!.ExportPublic();
        var bytes = GroupEnrollmentPackages.Write(package);
        var dialog = new SaveFileDialog { Title = "Nur öffentliche Verwaltungsdaten exportieren", Filter = "Gruppenregistrierung (*.group-enrollment.json)|*.group-enrollment.json", FileName = currentLibrary!.Value.ToString("N") + ".group-enrollment.json" };
        if (dialog.ShowDialog(this) != true) return;
        var revision = ConcurrentFileWriter.Capture(dialog.FileName);
        ConcurrentFileWriter.Write(dialog.FileName, revision, temporary => File.WriteAllBytes(temporary, bytes));
        status.Text = $"Öffentliche Registrierung exportiert.\nBibliotheks-ID: {package.LibraryId:D}\nVerwaltungsfingerabdruck: {OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey)}\nDiese Angaben den Kollegen unabhängig vom Dateiübertragungsweg bestätigen. Kein privater Schlüssel exportiert.";
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
    private void ShowError(Exception exception)
    {
        error.Text = exception.Message;
    }

    private void Run(Action action) { try { error.Text = ""; action(); } catch (Exception e) { ShowError(e); } }
    private Button Action(string label, Action action) { var button = new Button { Content = label }; button.Click += (_, _) => Run(action); return button; }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupDelegateWindow : Window
{
    private sealed record PersonLabel(string Id, string Label);
    private readonly string directory;
    private readonly ComboBox libraries = new();
    private readonly ComboBox people = new() { DisplayMemberPath = nameof(PersonLabel.Label) };
    private readonly ObservableCollection<GroupAdministrationDraft> groups = [];
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 100, MaxHeight = 220 };
    private readonly ListBox members = new() { SelectionMode = SelectionMode.Multiple, DisplayMemberPath = nameof(PersonLabel.Label), MinHeight = 100, MaxHeight = 220 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private VerifiedGroupPolicy? loaded;
    private OfflineGroupTrustStore? trust;
    private string? personId;
    private string? grantId;
    private bool dirty;
    private bool selectingMembers;

    internal GroupDelegateWindow(Window owner, string localDirectory)
    {
        grid.Name = "GroupsGrid"; members.Name = "MembersList";
        libraries.Name = "Libraries"; people.Name = "People"; status.Name = "DelegateStatus";
        Owner = owner; Title = "Mein Gruppenverwaltungsplatz · Entwicklung";
        Width = 900; Height = 800; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        directory = Path.GetFullPath(localDirectory);
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("1. Registrierte Bibliothek laden. 2. Eigene Person wählen und Arbeitsplatz laden. 3. Verwaltungseinladung exportieren und den Fingerabdruck separat bestätigen. Nach Freigabe den neuen Gruppenstand in Einstellungen importieren und hier neu laden."));
        root.Children.Add(libraries);
        libraries.ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(Path.GetFileName).Select(n => n!.Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [];
        root.Children.Add(Action("Bibliothek laden", () =>
        {
            if (libraries.SelectedItem is not Guid id) throw new InvalidOperationException("Bitte eine registrierte Bibliothek auswählen.");
            if (ConfirmDiscard()) LoadLibrary(id);
        }));
        root.Children.Add(people);
        root.Children.Add(Action("Person und Arbeitsplatz laden", () =>
        {
            if (people.SelectedItem is not PersonLabel selected) throw new InvalidOperationException("Bitte die eigene Person auswählen.");
            if (ConfirmDiscard()) LoadPerson(selected.Id);
        }));
        root.Children.Add(Action("Öffentliche Verwaltungseinladung exportieren", ExportInvitation));
        root.Children.Add(Action("Privaten Verwaltungsschlüssel sichern", () => ShowPrivateBackup(false)));
        root.Children.Add(Action("Privaten Verwaltungsschlüssel wiederherstellen", () => ShowPrivateBackup(true)));
        root.Children.Add(status);
        root.Children.Add(Note("Bearbeitbar sind nur Ihre freigegebenen Gruppen. Personen und Geräte pflegt die Hauptverwaltung. Mitgliedschaften durch Auswahl umschalten; mehrere Personen sind möglich."));
        grid.Columns.Add(new DataGridTextColumn { Header = "Meine freigegebene Gruppe", Binding = new Binding(nameof(GroupAdministrationDraft.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Gruppen-ID", Binding = new Binding(nameof(GroupAdministrationDraft.Id)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.ItemsSource = groups;
        grid.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Commit) dirty = true; };
        grid.SelectionChanged += (_, _) => SelectMembers();
        members.SelectionChanged += (_, _) =>
        {
            if (selectingMembers || grid.SelectedItem is not GroupAdministrationDraft group) return;
            group.Members.Clear();
            foreach (var person in members.SelectedItems.Cast<PersonLabel>()) group.Members.Add(person.Id);
            dirty = true;
        };
        root.Children.Add(grid); root.Children.Add(members);
        root.Children.Add(Action("Änderung signieren und exportieren", ExportChange));
        root.Children.Add(Note("Exportierte Änderungen an die Hauptverwaltung weitergeben. Sie gelten erst nach deren Übernahme und Verteilung des neuen signierten Standes. Lokale Offline-Kopien können einen inzwischen erfolgten Entzug noch nicht kennen."));
        root.Children.Add(error); root.Children.Add(Action("Schließen", Close));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        MainWindow.InitializeAutomationContract(this, "GroupDelegate");
    }

    internal void LoadLibrary(Guid id)
    {
        var candidate = new OfflineGroupTrustStore(Path.Combine(directory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
        var verified = candidate.LoadOffline();
        if (verified.Policy.Version != 2) throw new InvalidOperationException("Die Hauptverwaltung muss zuerst ein Personenverzeichnis bereitstellen.");
        trust = candidate; loaded = verified; personId = null; grantId = null; groups.Clear(); members.ItemsSource = null;
        people.ItemsSource = verified.Policy.People.Where(p => p.Enabled).Select(p => new PersonLabel(p.Id, p.Name + " (" + p.Id + ")")).ToArray();
        libraries.SelectedItem = id; dirty = false;
        status.Text = $"Bibliothek {id:D} · lokal registrierte Revision {verified.Policy.Revision}. Bitte eigene Person wählen.";
    }

    internal void LoadPerson(string id)
    {
        if (trust is null) throw new InvalidOperationException("Bitte zuerst eine Bibliothek laden.");
        RequireSelectedLibrary();
        var current = trust.LoadOffline();
        var person = current.Policy.People.SingleOrDefault(p => p.Id == id && p.Enabled)
            ?? throw new InvalidOperationException("Aktive Person fehlt im registrierten Stand.");
        OfflineGroupDelegation? grant = null;
        if (File.Exists(KeyPath(current.Policy.LibraryId, id)))
        {
            var invitation = KeyStore(current.Policy.LibraryId, id).ExportPublic();
            grant = current.Policy.Delegations.SingleOrDefault(g => g.PersonId == id && g.Enabled && g.SigningPublicKey.SequenceEqual(invitation.SigningPublicKey));
        }
        loaded = current; personId = id; grantId = grant?.Id;
        people.SelectedItem = people.Items.Cast<PersonLabel>().Single(p => p.Id == id);
        groups.Clear();
        foreach (var group in current.Policy.Groups.Where(g => grant?.GroupIds.Contains(g.Id, StringComparer.Ordinal) == true))
        {
            var draft = new GroupAdministrationDraft { Id = group.Id, Name = group.Name };
            foreach (var member in group.Members) draft.Members.Add(member);
            groups.Add(draft);
        }
        dirty = false;
        status.Text = $"{person.Name} · Revision {current.Policy.Revision}\n" + (grant is null
            ? "Keine aktive Vollmacht für diesen lokalen Verwaltungsschlüssel. Einladung exportieren bzw. Freigabe und aktuellen Gruppenstand anfordern."
            : $"Aktive Vollmacht: {grant.Id}. {groups.Count} freigegebene Gruppen.");
        grid.SelectedItem = groups.FirstOrDefault(); SelectMembers();
    }

    internal GroupDelegateInvitation GetInvitation(bool allowCreate)
    {
        RequirePerson();
        var latest = trust!.LoadOffline();
        if (!latest.Policy.People.Any(p => p.Id == personId && p.Enabled)) throw new InvalidOperationException("Die Person ist nicht mehr aktiv.");
        var keyStore = KeyStore(loaded!.Policy.LibraryId, personId!);
        if (File.Exists(KeyPath(loaded.Policy.LibraryId, personId!))) return keyStore.ExportPublic();
        if (!allowCreate) throw new InvalidOperationException("Noch kein lokaler Verwaltungsschlüssel vorhanden.");
        return keyStore.Create();
    }

    internal SignedGroupChange CreateChange()
    {
        RequirePerson();
        if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidOperationException("Bitte Tabellenbearbeitung abschließen.");
        if (grantId is null) throw new InvalidOperationException("Keine aktive Verwaltungsvollmacht geladen.");
        var changes = groups.Where(g =>
        {
            var previous = loaded!.Policy.Groups.Single(p => p.Id == g.Id);
            return g.Name != previous.Name || !g.Members.Order(StringComparer.Ordinal).SequenceEqual(previous.Members.Order(StringComparer.Ordinal));
        }).Select(g => new OfflineVaultGroup(g.Id, g.Name, g.Members.ToArray())).ToArray();
        if (changes.Length == 0) throw new InvalidOperationException("Keine Gruppenänderung zum Exportieren vorhanden.");
        return KeyStore(loaded!.Policy.LibraryId, personId!).SignChange(trust!, grantId, loaded.Checkpoint, changes);
    }

    private void ExportInvitation()
    {
        RequirePerson();
        var exists = File.Exists(KeyPath(loaded!.Policy.LibraryId, personId!));
        if (!exists && MessageBox.Show(this, "Einen separaten lokalen Verwaltungsschlüssel für diese Person erzeugen? Die Hauptverwaltung muss die Einladung danach unabhängig bestätigen; allein entsteht keine Berechtigung.", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var invitation = GetInvitation(allowCreate: !exists);
        if (SavePublic(GroupDelegationTransport.WriteInvitation(invitation), personId + ".group-delegate.json", "Verwaltungseinladung (*.group-delegate.json)|*.group-delegate.json"))
            status.Text = "Einladung exportiert. Diesen Fingerabdruck unabhängig an die Hauptverwaltung übermitteln:\n" + GroupDelegationTransport.InvitationFingerprint(invitation);
    }

    private void ExportChange()
    {
        var change = CreateChange();
        if (SavePublic(GroupDelegationTransport.WriteChange(change), "gruppenänderung.group-change.json", "Gruppenänderung (*.group-change.json)|*.group-change.json"))
        { dirty = false; status.Text = "Signierte Änderung exportiert. Zur Übernahme an die Hauptverwaltung geben; der registrierte Stand bleibt bis zum nächsten Import unverändert."; }
    }

    private bool SavePublic(byte[] bytes, string fileName, string filter)
    {
        var dialog = new SaveFileDialog { FileName = fileName, Filter = filter };
        if (dialog.ShowDialog(this) != true) return false;
        var expected = ConcurrentFileWriter.Capture(dialog.FileName);
        ConcurrentFileWriter.Write(dialog.FileName, expected, temporary => File.WriteAllBytes(temporary, bytes));
        return true;
    }

    private void SelectMembers()
    {
        selectingMembers = true;
        try
        {
            members.ItemsSource = loaded?.Policy.People.Select(p => new PersonLabel(p.Id, p.Name + (p.Enabled ? "" : " · inaktiv") + " (" + p.Id + ")")).ToArray();
            if (grid.SelectedItem is GroupAdministrationDraft group)
                foreach (var person in members.Items.Cast<PersonLabel>().Where(p => group.Members.Contains(p.Id))) members.SelectedItems.Add(person);
        }
        finally { selectingMembers = false; }
    }
    private string KeyPath(Guid libraryId, string id) => Path.Combine(directory, "Delegates", libraryId.ToString("N"), id + ".delegate.bin");
    private void ShowPrivateBackup(bool restore)
    {
        RequirePerson();
        if (!ConfirmDiscard()) return;
        var id = personId!;
        var current = trust!.LoadOffline();
        var selectedStore = KeyStore(current.Policy.LibraryId, id);
        GroupPrivateBackupTarget[] choices = restore
            ? current.Policy.Delegations.Where(g => g.Enabled && g.PersonId == id).Select(g =>
                new GroupPrivateBackupTarget($"{id} · Vollmacht {g.Id} · {current.Policy.LibraryId:D}", null,
                    (bytes, password) => { selectedStore.RestorePrivateBackup(bytes, password, trust, g.Id); LoadPerson(id); })).ToArray()
            : [new($"{id} · {current.Policy.LibraryId:D}", selectedStore.ExportPrivateBackup, null)];
        if (choices.Length == 0) throw new InvalidOperationException("Keine aktive registrierte Vollmacht für diese Person. Zuerst den aktuellen Gruppenstand importieren.");
        new GroupPrivateBackupWindow(this, choices, restore).ShowDialog();
    }
    private OfflineGroupDelegateStore KeyStore(Guid libraryId, string id) => new(KeyPath(libraryId, id), libraryId, id, new WindowsGroupDelegateProtection(libraryId, id));
    private void RequireSelectedLibrary()
    {
        if (libraries.SelectedItem is not Guid selected || selected != loaded?.Policy.LibraryId)
            throw new InvalidOperationException("Bitte zuerst die ausgewählte Bibliothek laden.");
    }

    private void RequirePerson()
    {
        RequireSelectedLibrary();
        if (people.SelectedItem is not PersonLabel selected || selected.Id != personId)
            throw new InvalidOperationException("Bitte für die ausgewählte Person zuerst „Person und Arbeitsplatz laden“ wählen.");
    }
    private bool ConfirmDiscard() => !dirty || MessageBox.Show(this, "Nicht exportierte Gruppenänderungen verwerfen?", Title, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
    private Button Action(string text, Action action) { var button = new Button { Content = text }; button.Click += (_, _) => { try { error.Text = ""; action(); } catch (Exception e) { error.Text = e.Message; } }; return button; }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

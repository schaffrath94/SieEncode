using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class PersonAdministrationDraft
{
    public string Id { get; init; } = "person-" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Neue Person";
    public bool Enabled { get; set; } = true;
    public ObservableCollection<DeviceAdministrationDraft> Devices { get; } = [];
}
internal sealed class DeviceAdministrationDraft
{
    public string Name { get; set; } = "Rechner";
    public string Fingerprint { get; init; } = "";
    public string Certificate { get; init; } = "";
    public bool Enabled { get; set; } = true;
}
internal sealed record GroupMemberLabel(string Id, string Label);

internal sealed partial class GroupAdministrationWindow
{
    private readonly ObservableCollection<PersonAdministrationDraft> people = [];
    private readonly DataGrid peopleGrid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 90, MaxHeight = 160 };
    private readonly DataGrid deviceGrid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 70, MaxHeight = 140 };
    private bool personMode;

    private void AddPeopleControls(StackPanel root)
    {
        var panel = new StackPanel();
        panel.Children.Add(Note("Person auswählen, Geräte-Einladungen hinzufügen und darunter Gruppen zuordnen. Deaktivierung wirkt auf neue Freigaben; alte Vaultkopien bleiben zugänglich."));
        peopleGrid.Columns.Add(new DataGridTextColumn { Header = "Person", Binding = new Binding(nameof(PersonAdministrationDraft.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        peopleGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aktiv", Binding = new Binding(nameof(PersonAdministrationDraft.Enabled)) });
        peopleGrid.ItemsSource = people;
        peopleGrid.SelectionChanged += (_, _) => deviceGrid.ItemsSource = (peopleGrid.SelectedItem as PersonAdministrationDraft)?.Devices;
        peopleGrid.CellEditEnding += (_, e) => { if (!loading && e.EditAction == DataGridEditAction.Commit) dirty = true; };
        deviceGrid.Columns.Add(new DataGridTextColumn { Header = "Gerät", Binding = new Binding(nameof(DeviceAdministrationDraft.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        deviceGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aktiv", Binding = new Binding(nameof(DeviceAdministrationDraft.Enabled)) });
        deviceGrid.Columns.Add(new DataGridTextColumn { Header = "Fingerabdruck", Binding = new Binding(nameof(DeviceAdministrationDraft.Fingerprint)), IsReadOnly = true, Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        deviceGrid.CellEditEnding += (_, e) => { if (!loading && e.EditAction == DataGridEditAction.Commit) dirty = true; };
        panel.Children.Add(peopleGrid);
        panel.Children.Add(Action("Person hinzufügen", () =>
        {
            RequireStore();
            var name = TextEntryPrompt.Show(this, "Person hinzufügen", "Anzeigename der Person", "");
            if (name is not null) peopleGrid.SelectedItem = AddPerson(name);
        }));
        panel.Children.Add(deviceGrid);
        panel.Children.Add(Action("Geräteeinladung zur Person hinzufügen", ImportPersonDevice));
        panel.Children.Add(Action("Bisherige Gruppen auf Personen umstellen", () =>
        {
            RequireStore();
            if (personMode) return;
            if (MessageBox.Show(this, "Alle bisherigen Geräte müssen zuvor Personen zugeordnet sein. Die Gruppen werden anschließend im Entwurf auf Personen umgestellt; alle aktiven Geräte dieser Personen erhalten bei künftigen Freigaben Zugriff. Fortfahren?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) ConvertToPeople();
        }));
        root.Children.Add(new Expander { Header = "Personen und Geräte", IsExpanded = true, Content = panel });
        members.DisplayMemberPath = nameof(GroupMemberLabel.Label);
    }

    internal PersonAdministrationDraft AddPerson(string name)
    {
        RequireStore();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Personenname fehlt.");
        var person = new PersonAdministrationDraft { Name = name.Trim() }; people.Add(person); dirty = true; return person;
    }

    internal void AddDevice(string personId, VaultRecipientInvitation invitation)
    {
        RequireStore();
        var person = people.Single(p => p.Id == personId);
        var imported = CertificateVaultService.ImportInvitation(invitation);
        using var certificate = imported.Certificate;
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        if (people.SelectMany(p => p.Devices).Any(d => d.Fingerprint == fingerprint)) throw new InvalidOperationException("Dieses Gerät ist bereits einer Person zugeordnet.");
        using var incomingKey = certificate.GetRSAPublicKey()!;
        var incomingPublicKey = incomingKey.ExportSubjectPublicKeyInfo();
        foreach (var assignedPerson in people)
        foreach (var device in assignedPerson.Devices)
        {
            using var assignedCertificate = new X509Certificate2(Convert.FromBase64String(device.Certificate));
            using var assignedKey = assignedCertificate.GetRSAPublicKey()!;
            if (incomingPublicKey.SequenceEqual(assignedKey.ExportSubjectPublicKeyInfo()))
                throw new InvalidOperationException($"Dieser Schlüssel gehört bereits zu {assignedPerson.Name} · {device.Name}, auch wenn der Zugang deaktiviert ist. Auf dem neuen Gerät eine eigene Identität erzeugen und deren öffentliche Einladung verwenden.");
        }
        person.Devices.Add(new DeviceAdministrationDraft { Name = imported.DisplayName, Fingerprint = fingerprint, Certificate = Convert.ToBase64String(certificate.RawData) });
        dirty = true;
    }

    private void ImportPersonDevice()
    {
        if (peopleGrid.SelectedItem is not PersonAdministrationDraft person) throw new InvalidOperationException("Bitte zuerst eine Person auswählen.");
        var dialog = new OpenFileDialog { Filter = "Empfängereinladung (*.recipient.json)|*.recipient.json" };
        if (dialog.ShowDialog(this) != true) return;
        var imported = WindowsVaultIdentityService.ImportInvitation(dialog.FileName);
        using var certificate = imported.Certificate;
        var invitation = CertificateVaultService.ExportInvitation(certificate, imported.DisplayName);
        if (MessageBox.Show(this, $"Gerät: {invitation.DisplayName}\nPerson: {person.Name}\n{invitation.Fingerprint}\n\nFingerabdruck unabhängig bestätigt?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) AddDevice(person.Id, invitation);
    }

    private void AddSelectedPerson()
    {
        if (!personMode) throw new InvalidOperationException("Bitte bisherige Gruppen zuerst auf Personen umstellen.");
        if (peopleGrid.SelectedItem is not PersonAdministrationDraft person || grid.SelectedItem is not GroupAdministrationDraft group) throw new InvalidOperationException("Bitte Person und Gruppe auswählen.");
        if (!group.Members.Contains(person.Id)) { group.Members.Add(person.Id); dirty = true; RefreshMemberLabels(); }
    }

    internal void ConvertToPeople()
    {
        RequireStore();
        if (personMode) return;
        var map = people.SelectMany(p => p.Devices.Select(d => (d.Fingerprint, p.Id))).ToDictionary(x => x.Fingerprint, x => x.Id, StringComparer.Ordinal);
        var replacements = groups.Select(g => g.Members.Select(m => map.TryGetValue(m, out var id) ? id : throw new InvalidOperationException("Für mindestens ein bisheriges Gerät fehlt die Personenzuordnung.")).Distinct(StringComparer.Ordinal).ToArray()).ToArray();
        for (var i = 0; i < groups.Count; i++) { groups[i].Members.Clear(); foreach (var id in replacements[i]) groups[i].Members.Add(id); }
        personMode = true; dirty = true; RefreshMemberLabels();
    }

    private void LoadPeople(OfflineGroupPolicy policy)
    {
        people.Clear(); personMode = policy.Version == 2 || policy.Groups.All(g => g.Members.Length == 0);
        foreach (var person in policy.People)
        {
            var row = new PersonAdministrationDraft { Id = person.Id, Name = person.Name, Enabled = person.Enabled };
            foreach (var device in person.Devices) row.Devices.Add(new DeviceAdministrationDraft { Name = device.Name, Fingerprint = device.Fingerprint, Certificate = device.Certificate, Enabled = device.Enabled });
            people.Add(row);
        }
    }
    private OfflineVaultPerson[] PersonRows() => people.Select(p => new OfflineVaultPerson(p.Id, p.Name, p.Enabled, p.Devices.Select(d => new OfflineVaultDevice(d.Name, d.Fingerprint, d.Certificate, d.Enabled)).ToArray())).ToArray();
    private void RefreshMemberLabels() => members.ItemsSource = (grid.SelectedItem as GroupAdministrationDraft)?.Members.Select(id => new GroupMemberLabel(id, people.FirstOrDefault(p => p.Id == id) is { } p ? p.Name + (p.Enabled ? "" : " · inaktiv") + " · " + p.Id : id)).ToArray();
    private void CommitEdits()
    {
        foreach (var table in new[] { grid, peopleGrid, deviceGrid })
            if (!table.CommitEdit(DataGridEditingUnit.Cell, true) || !table.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidOperationException("Bitte ungültige Eingaben in der Tabelle korrigieren.");
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RFiDGear.Builder.Core;
using Choice = RFiDGear.Builder.App.KeyLibraryAssignmentWindow.VaultChoice;

namespace RFiDGear.Builder.App;

/// <summary>Project-independent read-only view. Only metadata is retained in
/// the list; explicit reveal and copy reopen the authenticated local vault.</summary>
internal sealed class KeyLibraryBrowseWindow : Window
{
    private sealed record Row(KeyLibraryEntry Entry, string Customer)
    {
        public string Name => Entry.Name;
        public string Type => Entry.KeyType;
        public int Version => Entry.Version;
        public string State => Entry.Status switch
        {
            KeyLibraryContracts.Active => "Aktiv",
            KeyLibraryContracts.Retired => "Abgelöst",
            _ => "Kompromittiert"
        };
        public string Purpose => Entry.Purpose;
        public string Host => Entry.HostSystem;
    }

    private readonly string trustDirectory;
    private readonly ComboBox sources = new() { DisplayMemberPath = nameof(Choice.Label), MinWidth = 350 };
    private readonly TextBox search = new() { MinWidth = 300 };
    private readonly DataGrid keys = new() { AutoGenerateColumns = false, IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 260 };
    private readonly TextBlock syncStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button reveal = new() { Content = "Schlüsselwert bewusst anzeigen", IsEnabled = false };
    private readonly Button refresh = new() { Content = "Aktualisieren" };
    private KeyLibrarySnapshot? catalog;
    private GroupPolicyCheckpoint? checkpoint;
    private FileRevision? file;
    private KeyLibraryDisclosureAccess? access;
    private bool busy;

    internal KeyLibraryBrowseWindow(Window owner, string localTrustDirectory)
    {
        Owner = owner; trustDirectory = Path.GetFullPath(localTrustDirectory);
        Title = "SieEncode · Schlüsselbibliothek"; Width = 990; Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = "Schlüsselbibliothek", FontSize = 22, FontWeight = FontWeights.SemiBold });
        top.Children.Add(Note("Projektunabhängig suchen und historische Versionen prüfen. Klartextanzeige und Kopieren benötigen getrennt signierte Freigaben. Keine Bearbeitung in dieser Ansicht."));
        var sourceBar = new WrapPanel(); sourceBar.Children.Add(sources); sourceBar.Children.Add(refresh);
        top.Children.Add(sourceBar); top.Children.Add(syncStatus);
        top.Children.Add(Note("Suchen · Kunde, Schlüsselname, Typ, Zweck, Host (z. B. SIPORT), ID oder Version"));
        top.Children.Add(search); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = new StackPanel();
        bottom.Children.Add(status);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(reveal);
        var close = new Button { Content = "Schließen" }; close.Click += (_, _) => Close();
        actions.Children.Add(close); bottom.Children.Add(actions);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        foreach (var column in new (string Title, string Property, double Weight)[]
        {
            ("Kunde", nameof(Row.Customer), 1.4), ("Schlüssel", nameof(Row.Name), 1.6),
            ("Typ", nameof(Row.Type), 1.0), ("Version", nameof(Row.Version), 0.6),
            ("Status", nameof(Row.State), 1.0), ("Zweck", nameof(Row.Purpose), 1.0),
            ("Host", nameof(Row.Host), 1.0)
        })
            keys.Columns.Add(new DataGridTextColumn { Header = column.Title,
                Binding = new Binding(column.Property), Width = new DataGridLength(column.Weight, DataGridLengthUnitType.Star) });
        root.Children.Add(keys); Content = root;
        search.TextChanged += (_, _) => Filter();
        keys.SelectionChanged += (_, _) => UpdateActions();
        sources.SelectionChanged += async (_, _) => await LoadAsync();
        refresh.Click += async (_, _) => await LoadAsync();
        reveal.Click += (_, _) => Reveal();
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        sources.ItemsSource = KeyLibraryAssignmentWindow.DiscoverAvailableVaults(trustDirectory);
        if (sources.Items.Count == 1) sources.SelectedIndex = 0;
        else syncStatus.Text = sources.Items.Count == 0
            ? "Keine registrierte lokale oder zentral erreichbare Bibliothek. Zuerst Gruppenregistrierung und persönliche Gerätefreigabe einrichten."
            : "Bitte die freigegebene Bibliothek auswählen.";
    }

    private static TextBlock Note(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 6) };
    private OfflineGroupTrustStore Trust(Choice choice) => new(
        Path.Combine(trustDirectory, choice.LibraryId.ToString("N") + ".trust.bin"),
        choice.LibraryId, new WindowsGroupTrustProtection(choice.LibraryId));

    private async Task LoadAsync()
    {
        if (busy) return;
        catalog = null; checkpoint = null; file = null; access = null;
        Filter();
        if (sources.SelectedItem is not Choice choice) return;
        busy = true; sources.IsEnabled = false; refresh.IsEnabled = false;
        syncStatus.Text = "Bibliothek wird geprüft …";
        try
        {
            using var lease = CentralGroupPolicyWorkflow.Acquire(choice.LibraryId);
            var trust = Trust(choice);
            var identities = WindowsVaultIdentityService.List();
            try
            {
                syncStatus.Text = await CentralGroupPolicyWorkflow.RefreshMemberAsync(choice.LibraryId,
                    choice.GroupId, trust, identities.Select(i => i.Certificate),
                    allowPolicyAdoption: !File.Exists(Path.Combine(trustDirectory, "Administrations",
                        choice.LibraryId.ToString("N") + ".admin.bin")));
                CentralGroupPolicyWorkflow.RequireNoPending(choice.LibraryId);
                var policy = trust.LoadOffline();
                var candidates = new List<(X509Certificate2 Certificate, KeyLibraryDisclosureAccess Access)>();
                foreach (var identity in identities)
                {
                    try { candidates.Add((identity.Certificate, KeyLibraryDisclosure.RequireMember(
                        policy, choice.GroupId, identity.Certificate, DateTimeOffset.UtcNow))); }
                    catch (CryptographicException) { }
                }
                if (candidates.Count == 0) throw new CryptographicException("Kein aktuelles persönliches Gruppen-Zertifikat.");
                // Do not combine permissions from different persons or devices.
                var selected = candidates.OrderByDescending(c => c.Access.CanCopy)
                    .ThenByDescending(c => c.Access.CanView).First();
                var captured = ConcurrentFileWriter.Capture(choice.Path);
                var opened = KeyLibraryVaultStore.Load(choice.Path, trust, choice.GroupId, [selected.Certificate]);
                if (ConcurrentFileWriter.Capture(choice.Path) != captured || trust.LoadOffline().Checkpoint != policy.Checkpoint)
                    throw new ConcurrentFileChangeException(choice.Path);
                catalog = opened.Catalog; checkpoint = policy.Checkpoint;
                file = captured; access = selected.Access;
            }
            finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
        }
        catch (Exception ex)
        {
            catalog = null; checkpoint = null; file = null; access = null;
            syncStatus.Text = "Bibliothek nicht freigegeben: " + ex.Message;
        }
        finally { busy = false; sources.IsEnabled = true; refresh.IsEnabled = true; Filter(); }
    }

    private void Filter()
    {
        var selected = (keys.SelectedItem as Row)?.Entry;
        if (catalog is null) { keys.ItemsSource = Array.Empty<Row>(); UpdateActions(); return; }
        var names = catalog.Customers.ToDictionary(c => c.CustomerId, c => c.Name);
        var query = search.Text.Trim();
        var rows = catalog.Keys.Select(k => new Row(k, names.GetValueOrDefault(k.CustomerId, k.CustomerId.ToString("D"))))
            .Where(row => query.Length == 0 || new[] { row.Customer, row.Name, row.Type,
                row.Purpose, row.Host, row.State, row.Entry.KeyId.ToString("D"), row.Version.ToString() }
                .Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(row => row.Customer, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(row => row.Version).ToArray();
        keys.ItemsSource = rows;
        keys.SelectedItem = rows.FirstOrDefault(row => row.Entry == selected);
        UpdateActions();
        status.Text = $"{rows.Length} / {catalog.Keys.Length} Versionen · Katalogrevision {catalog.Revision} · " +
            (access?.CanView == true ? access.CanCopy ? "Anzeigen und Kopieren freigegeben" : "Nur Anzeigen freigegeben" : "Nur Metadaten");
    }

    private void UpdateActions() => reveal.IsEnabled = !busy && access?.CanView == true && keys.SelectedItem is Row;

    private void Reveal()
    {
        if (busy || sources.SelectedItem is not Choice choice || keys.SelectedItem is not Row row ||
            catalog is null || checkpoint is null || file is null || access?.CanView != true) return;
        try
        {
            var selected = row.Entry;
            var revision = catalog.Revision;
            var expectedPolicy = checkpoint;
            var expectedFile = file;
            var actor = access;
            var historical = selected.Status != KeyLibraryContracts.Active
                ? "\n\nACHTUNG: Historische oder kompromittierte Version; nicht für neue produktive Konfigurationen verwenden." : "";
            if (MessageBox.Show(this, $"{row.Customer} · {selected.Name} · v{selected.Version}\n{selected.KeyType} · {row.State}\n\nSchlüssel bewusst im Klartext anzeigen? Lokaler Nachweis wird erfasst. Kopien sind nicht durch späteren Rechteentzug rückrufbar." + historical,
                Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

            using var lease = CentralGroupPolicyWorkflow.Acquire(choice.LibraryId);
            CentralGroupPolicyWorkflow.RequireNoPending(choice.LibraryId);
            var trust = Trust(choice);
            var identities = WindowsVaultIdentityService.List();
            try
            {
                var identity = identities.SingleOrDefault(i => i.Fingerprint == actor.DeviceFingerprint)?.Certificate
                    ?? throw new CryptographicException("Freigegebenes persönliches Gerät fehlt.");
                string ReadAndRecord(bool copying)
                {
                    CentralGroupPolicyWorkflow.RequireNoPending(choice.LibraryId);
                    var now = DateTimeOffset.UtcNow;
                    var current = KeyLibraryDisclosure.RequireMember(trust.LoadOffline(), choice.GroupId, identity, now);
                    if (current.PersonId != actor.PersonId || current.DeviceFingerprint != actor.DeviceFingerprint)
                        throw new CryptographicException("Persönlicher Zugang wurde geändert.");
                    var secret = KeyLibraryDisclosure.ReadSelected(choice.Path, trust, choice.GroupId,
                        identity, expectedPolicy, revision, expectedFile, selected, copying, now);
                    KeyLibraryDisclosureAudit.Record(Path.Combine(Path.GetDirectoryName(choice.Path)!, "DisclosureAudit"),
                        new WindowsGroupTrustProtection(choice.LibraryId, disclosureAudit: true),
                        new(Guid.NewGuid(), now, choice.LibraryId, choice.GroupId, current.PersonId,
                            current.DeviceFingerprint, selected.CustomerId, selected.KeyId, selected.Version,
                            revision, expectedPolicy, copying ? "copy-requested" : "view-requested"));
                    return secret;
                }
                var value = ReadAndRecord(false);
                new KeyLibrarySecretViewWindow(this, selected, value, actor.CanCopy,
                    () => ReadAndRecord(true)).ShowDialog();
            }
            finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
        }
        catch (Exception ex) { status.Text = "Klartext nicht freigegeben: " + ex.Message; }
    }
}

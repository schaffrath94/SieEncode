using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class KeyLibraryAuditReviewWindow : Window
{
    private sealed record Row(KeyLibraryDisclosureEvent Entry, string Hash, string Source,
        DateTimeOffset? AcceptedAt, string Person, string Customer, string Key)
    {
        public string Time => Entry.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string Accepted => AcceptedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
        public string Operation => Entry.Operation == "view-requested" ? "Anzeige angefordert" : "Kopie angefordert";
        public int Version => Entry.Version;
        public string Device => Entry.DeviceFingerprint;
        public string EventId => Entry.EventId.ToString("D");
    }

    private readonly Guid libraryId;
    private readonly OfflineGroupAdministrationStore administration;
    private readonly OfflineGroupTrustStore trust;
    private readonly TextBox search = new() { MinWidth = 310 };
    private readonly ComboBox source = new() { ItemsSource = new[] { "Alle", "Zentral", "Lokal" }, SelectedIndex = 0 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock counts = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button refresh = new() { Content = "Aktualisieren" };
    private readonly Button more = new() { Content = "Weitere zentrale Ereignisse laden", IsEnabled = false };
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, IsReadOnly = true,
        CanUserAddRows = false, CanUserDeleteRows = false, ClipboardCopyMode = DataGridClipboardCopyMode.None };
    private readonly List<Row> local = [];
    private readonly Dictionary<Guid, Row> remote = [];
    private readonly Dictionary<Guid, string> customers = [];
    private readonly Dictionary<KeyLibraryAddress, KeyLibraryEntry> keyNames = [];
    private VerifiedGroupPolicy? policy;
    private string? next;
    private string? group;
    private bool busy;
    private const int MaximumLoadedCentral = 4096;

    internal KeyLibraryAuditReviewWindow(Window owner, Guid id,
        OfflineGroupAdministrationStore administration, OfflineGroupTrustStore trust)
    {
        Owner = owner; libraryId = id; this.administration = administration; this.trust = trust;
        administration.RequireLocalAuthority(trust);
        Title = "SieEncode · Aktivitätsprotokoll"; Width = 1180; Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = "Aktivitätsprotokoll", FontSize = 22, FontWeight = FontWeights.SemiBold });
        top.Children.Add(Note("Gerätesignierte Anforderungen zur Klartextanzeige und Kopie. Keine Schlüsselwerte. Zeit der Anforderung stammt vom Arbeitsplatz; der zentrale Empfangszeitpunkt ist getrennt."));
        top.Children.Add(Note("Suche in geladenen Ereignissen · Person, Kunde, Schlüssel, Version, Gerät oder Ereignis-ID. Namen sind aktuelle Bezeichnungen, keine historischen Stammdaten."));
        var bar = new WrapPanel(); bar.Children.Add(search); bar.Children.Add(source);
        bar.Children.Add(refresh); bar.Children.Add(more); top.Children.Add(bar);
        top.Children.Add(status); top.Children.Add(counts);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = Note("Zentral bestätigt = im authentifizierten Serverabruf gefunden. Lokales Archiv = früher lokal bestätigte Übertragung, hier noch nicht erneut zentral geprüft. Nachweis einer Anforderung, nicht einer tatsächlich erfolgten Anzeige/Kopie.");
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        foreach (var column in new (string Title, string Property, double Width)[]
        {
            ("Anforderung", nameof(Row.Time), 145), ("Zentraler Empfang", nameof(Row.Accepted), 145),
            ("Person", nameof(Row.Person), 150), ("Kunde", nameof(Row.Customer), 150),
            ("Schlüssel", nameof(Row.Key), 160), ("Version", nameof(Row.Version), 65),
            ("Aktion", nameof(Row.Operation), 155), ("Nachweis", nameof(Row.Source), 220),
            ("Gerät", nameof(Row.Device), 180), ("Ereignis-ID", nameof(Row.EventId), 180)
        })
            grid.Columns.Add(new DataGridTextColumn { Header = column.Title, Binding = new Binding(column.Property), Width = column.Width });
        root.Children.Add(grid); Content = root;
        search.TextChanged += (_, _) => ApplyFilter();
        source.SelectionChanged += (_, _) => ApplyFilter();
        refresh.Click += async (_, _) => await LoadAsync(reset: true);
        more.Click += async (_, _) => await LoadAsync(reset: false);
        Loaded += async (_, _) => await LoadAsync(reset: true);
        Closing += (_, e) => { if (busy) e.Cancel = true; };
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };

    private Row MakeRow(KeyLibraryDisclosureEvent entry, string hash, string label, DateTimeOffset? accepted)
    {
        var person = policy?.Policy.People.SingleOrDefault(p => p.Id == entry.PersonId)?.Name ?? entry.PersonId;
        var customer = customers.GetValueOrDefault(entry.CustomerId, entry.CustomerId.ToString("D"));
        var address = new KeyLibraryAddress(entry.KeyId, entry.Version);
        var key = keyNames.TryGetValue(address, out var metadata) && metadata.CustomerId == entry.CustomerId
            ? metadata.Name : entry.KeyId.ToString("D");
        return new(entry, hash, label, accepted, person, customer, key);
    }

    private void ApplyFilter()
    {
        // A lost reply can leave a pending local report and an accepted server
        // record. Merge only identical payloads; conflicting IDs remain visible.
        var rows = new List<Row>(remote.Values);
        foreach (var item in local)
        {
            if (remote.TryGetValue(item.Entry.EventId, out var confirmed))
            {
                if (confirmed.Hash == item.Hash) continue;
                rows.Add(item with { Source = "Lokal · ID-Konflikt!" });
            }
            else if (!rows.Any(r => r.Entry.EventId == item.Entry.EventId && r.Hash == item.Hash && r.Source == item.Source))
                rows.Add(item);
        }
        var query = search.Text.Trim();
        var choice = source.SelectedIndex;
        var shown = rows.Where(r => (choice == 0 || (choice == 1 ? r.AcceptedAt is not null : r.AcceptedAt is null)) &&
            (query.Length == 0 || new[] { r.Person, r.Entry.PersonId, r.Customer, r.Entry.CustomerId.ToString("D"),
                r.Key, r.Entry.KeyId.ToString("D"), r.Version.ToString(), r.Device, r.EventId, r.Operation, r.Source }
                .Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(r => r.Entry.Time).ThenBy(r => r.EventId, StringComparer.Ordinal).ToArray();
        grid.ItemsSource = shown;
        counts.Text = $"{shown.Length} sichtbar · {remote.Count} zentral geladen · {local.Count} lokale Dateinachweise" +
            (next is not null ? " · weitere zentrale Seiten vorhanden" : "");
    }

    private async Task LoadAsync(bool reset)
    {
        if (busy || !reset && next is null) return;
        busy = true; refresh.IsEnabled = false; more.IsEnabled = false;
        status.Text = "Audit-Bestand wird geprüft …";
        var identities = Array.Empty<WindowsVaultIdentity>();
        var localNotice = "";
        try
        {
            // The owning main-administration window holds the library lease.
            CentralGroupPolicyWorkflow.RequireNoPending(libraryId);
            var current = trust.LoadOffline();
            if (administration.RequireLocalAuthority(trust) != current.Checkpoint)
                throw new CryptographicException("Verwaltungsstand wurde geändert.");
            if (!reset && policy?.Checkpoint != current.Checkpoint)
            {
                local.Clear(); remote.Clear(); next = null;
                throw new InvalidOperationException("Gruppenstand geändert. Bitte vollständig aktualisieren.");
            }
            policy = current;
            identities = WindowsVaultIdentityService.List().ToArray();
            var cursor = new CentralKeyLibraryCursorStore(CentralKeyLibraryCursorStore.DefaultPath(libraryId),
                libraryId, new WindowsGroupTrustProtection(libraryId, syncState: true));
            if (reset)
            {
                local.Clear(); remote.Clear(); customers.Clear(); keyNames.Clear(); next = null;
                group = cursor.ReadReconciledHead()?.GroupId;
                // Use current catalog labels only when independently decryptable.
                foreach (var g in current.Policy.Groups)
                {
                    var path = KeyLibraryVaultStore.DefaultPath(libraryId, g.Id);
                    if (!File.Exists(path)) continue;
                    try
                    {
                        var catalog = KeyLibraryVaultStore.Load(path, trust, g.Id,
                            identities.Select(i => i.Certificate)).Catalog;
                        foreach (var c in catalog.Customers) customers[c.CustomerId] = c.Name;
                        foreach (var k in catalog.Keys) keyNames[new(k.KeyId, k.Version)] = k;
                    }
                    catch (Exception ex) when (ex is IOException or CryptographicException or InvalidDataException)
                    { localNotice = " · Aktuelle Katalognamen teilweise nicht verfügbar"; }
                }
                var directory = Path.Combine(Path.GetDirectoryName(KeyLibraryVaultStore.DefaultPath(libraryId, "keys"))!, "DisclosureAudit");
                var review = KeyLibraryDisclosureAudit.ReadLocal(directory,
                    new WindowsGroupTrustProtection(libraryId, disclosureAudit: true), current);
                foreach (var record in review.Records)
                    local.Add(MakeRow(record.Entry, record.PayloadSha256,
                        (record.Archived ? "Lokales Archiv" : "Lokal ausstehend") +
                        (record.SignatureVerified ? "" : " · Signatur nicht prüfbar"), null));
                if (review.InvalidCount > 0 || review.Truncated)
                    localNotice += $" · {review.InvalidCount} ungültige lokale Dateien" +
                        (review.Truncated ? " · lokale Anzeige auf 1000 Dateien begrenzt" : "");
                ApplyFilter();
            }

            var endpoint = CentralGroupPolicyWorkflow.Endpoint(libraryId);
            var selectedGroup = group;
            if (endpoint is null || string.IsNullOrEmpty(selectedGroup))
            {
                status.Text = "Nur lokale Nachweise · Zentralverbindung oder abgeglichener Gruppenstand fehlt" + localNotice;
                return;
            }
            X509Certificate2? identity = null;
            foreach (var item in identities)
            {
                try { _ = KeyLibraryDisclosure.RequireMember(current, selectedGroup, item.Certificate, DateTimeOffset.UtcNow); identity = item.Certificate; break; }
                catch (CryptographicException) { }
            }
            if (identity is null) throw new CryptographicException("Aktiver persönlicher Zentralzugang fehlt.");
            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin), libraryId,
                endpoint.ServerCertificateSha256, identity);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var signed = administration.SignAuditQuery(trust, reset ? "" : next!,
                Math.Min(KeyLibraryAuditReview.MaximumPageSize, MaximumLoadedCentral - remote.Count),
                identity, DateTimeOffset.UtcNow);
            var query = KeyLibrarySyncWire.Read<KeyLibraryAuditQuery>(signed.Payload, KeyLibraryAuditReview.MaximumQueryBytes);
            var page = await transport.ReadAuditPageAsync(signed, deadline.Token);
            KeyLibraryAuditReview.ValidatePage(query, page);
            if (administration.RequireLocalAuthority(trust) != current.Checkpoint)
                throw new CryptographicException("Lokaler Gruppenstand wurde geändert.");
            foreach (var record in page.Records)
            {
                var entry = KeyLibraryAuditReview.ValidateRecord(record, libraryId);
                remote.Add(entry.EventId, MakeRow(entry, record.Receipt.PayloadSha256, "Zentral bestätigt", record.Receipt.AcceptedAt));
            }
            next = page.NextAfterId;
            status.Text = "Zentrale Audit-Seite bestätigt" + localNotice +
                (remote.Count >= MaximumLoadedCentral ? " · Anzeigegrenze erreicht (4096 zentrale Ereignisse)" : "");
        }
        catch (Exception ex)
        {
            status.Text = "Audit-Abfrage nicht vollständig bestätigt: " + ex.Message + localNotice;
        }
        finally
        {
            busy = false; refresh.IsEnabled = true;
            more.IsEnabled = next is not null && remote.Count < MaximumLoadedCentral;
            ApplyFilter();
        }
    }
}

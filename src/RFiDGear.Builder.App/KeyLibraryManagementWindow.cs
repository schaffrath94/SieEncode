using System.IO;
using Microsoft.Win32;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>
/// Key-library editing inside SieEncode's existing signed main administration.
/// Each edit requires both the local signing authority and current group-vault access.
/// This is an offline development workflow; server acceptance is not implied.
/// </summary>
internal sealed class KeyLibraryManagementWindow : Window
{
    private sealed record GroupChoice(string Id, string Name);
    private sealed record KeyRow(KeyLibraryEntry Entry, string CustomerName)
    {
        public string Name => Entry.Name;
        public string KeyType => Entry.KeyType;
        public int Version => Entry.Version;
        public string Status => Entry.Status;
        public string Purpose => Entry.Purpose;
        public string HostSystem => Entry.HostSystem;
    }
    private readonly OfflineGroupAdministrationStore administration;
    private readonly OfflineGroupTrustStore trust;
    private readonly Guid libraryId;
    private readonly KeyLibrarySyncEndpointStore endpointStore;
    private readonly CentralKeyLibraryCursorStore cursorStore;
    private readonly TextBox serverOrigin = new() { MinWidth = 310 };
    private readonly TextBox serverPin = new() { MinWidth = 420, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBlock centralStatus = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateGray };
    private bool syncing;
    private readonly Button resumeMerge = ActionButton("Zusammenführung fortsetzen");
    private readonly Button reviewMerge = ActionButton("Verschlüsselte Bestände vergleichen");
    private readonly ComboBox groupSelection = new() { DisplayMemberPath = nameof(GroupChoice.Name), MinWidth = 350 };
    private readonly ComboBox customerSelection = new() { DisplayMemberPath = nameof(KeyLibraryCustomer.Name), MinWidth = 260 };
    private readonly DataGrid keys = new() { AutoGenerateColumns = false, IsReadOnly = true,
        CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 240, MaxHeight = 430 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
    private readonly Button create = ActionButton("Verschlüsselte Bibliothek erstmalig anlegen");
    private readonly Button addCustomer = ActionButton("Kunde anlegen");
    private readonly Button generate = ActionButton("Schlüssel erzeugen");
    private readonly Button import = ActionButton("Vorhandenen Schlüssel importieren");
    private readonly Button versionGenerate = ActionButton("Neue Version erzeugen");
    private readonly Button versionImport = ActionButton("Neue Version importieren");
    private readonly Button retire = ActionButton("Version ablösen");
    private readonly Button compromise = ActionButton("Version als kompromittiert sperren");
    private readonly Button reveal = ActionButton("Schlüsselwert bewusst anzeigen");

    internal KeyLibraryManagementWindow(Window owner, OfflineGroupAdministrationStore administration,
        OfflineGroupTrustStore trust, Guid libraryId)
    {
        Owner = owner;
        this.administration = administration;
        this.trust = trust;
        this.libraryId = libraryId;
        endpointStore = new KeyLibrarySyncEndpointStore(
            KeyLibrarySyncEndpointStore.DefaultPath(libraryId), libraryId,
            new WindowsGroupTrustProtection(libraryId, syncState: true));
        cursorStore = new CentralKeyLibraryCursorStore(
            CentralKeyLibraryCursorStore.DefaultPath(libraryId), libraryId,
            new WindowsGroupTrustProtection(libraryId, syncState: true));
        Title = "SieEncode · Zentrale Schlüsselverwaltung (lokaler Entwicklungsstand)";
        Width = 990;
        MaxHeight = 870;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CentralGroupPolicyWorkflow.RequireNoPolicyPending(libraryId);
        administration.RequireLocalAuthority(trust);
        Closing += (_, e) => { if (syncing) e.Cancel = true; };

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(Note("Eine verschlüsselte Offline-Bibliothek für alle Kunden. Lokale Änderungen gelten erst nach bestätigt erfolgreichem Abgleich als zentral übernommen."));
        root.Children.Add(Note("Die signierte Hauptverwaltung und ein separat berechtigtes persönliches Gruppen-Zertifikat sind erforderlich. Alle Versionswechsel sind ausdrücklich; vorhandene Karten, Leser und SIPORT bleiben unverändert."));
        root.Children.Add(Note("Berechtigte Schlüsselgruppe"));
        root.Children.Add(groupSelection);
        root.Children.Add(create);
        root.Children.Add(Note("Kunden"));
        var customerBar = new WrapPanel();
        customerBar.Children.Add(customerSelection);
        customerBar.Children.Add(addCustomer);
        root.Children.Add(customerBar);
        root.Children.Add(Note("Schlüssel und historische Versionen"));
        keys.Columns.Add(new DataGridTextColumn { Header = "Kunde", Binding = new Binding(nameof(KeyRow.CustomerName)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Bezeichnung", Binding = new Binding(nameof(KeyRow.Name)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Typ", Binding = new Binding(nameof(KeyRow.KeyType)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Version", Binding = new Binding(nameof(KeyRow.Version)), Width = new DataGridLength(0.55, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding(nameof(KeyRow.Status)), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Verwendung", Binding = new Binding(nameof(KeyRow.Purpose)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        keys.Columns.Add(new DataGridTextColumn { Header = "Host", Binding = new Binding(nameof(KeyRow.HostSystem)), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });
        root.Children.Add(keys);

        var addActions = new WrapPanel();
        addActions.Children.Add(generate); addActions.Children.Add(import);
        addActions.Children.Add(versionGenerate); addActions.Children.Add(versionImport);
        root.Children.Add(addActions);
        var sensitiveActions = new WrapPanel();
        sensitiveActions.Children.Add(retire); sensitiveActions.Children.Add(compromise); sensitiveActions.Children.Add(reveal);
        root.Children.Add(sensitiveActions);
        var syncSettings = new StackPanel();
        syncSettings.Children.Add(Note("Einmalige Verbindung der bestehenden Hauptverwaltung. HTTPS und den SHA-256-Fingerabdruck des Serverzertifikats unabhängig prüfen. Keine automatische Vertrauensübernahme!"));
        syncSettings.Children.Add(Note("HTTPS-Zentrale (z. B. https://server:7443/)"));
        syncSettings.Children.Add(serverOrigin);
        syncSettings.Children.Add(Note("Unabhängig bestätigter SHA-256-Fingerabdruck des TLS-Serverzertifikats"));
        syncSettings.Children.Add(serverPin);
        var storeConnection = ActionButton("Bestätigte Zentralverbindung speichern");
        storeConnection.Click += async (_, _) =>
        {
            if (syncing) return;
            try
            {
                var proposed = KeyLibrarySyncEndpointStore.Validate(
                    new KeyLibrarySyncEndpoint(libraryId, serverOrigin.Text.Trim(),
                        serverPin.Text.Trim().ToUpperInvariant()));
                if (MessageBox.Show(this,
                    $"Bitte den folgenden vollständigen Fingerabdruck über einen unabhängigen vertrauenswürdigen Kanal prüfen:\n\n{proposed.ServerCertificateSha256}\n\nIst er unabhängig bestätigt und ist die HTTPS-Adresse die richtige Zentrale?",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes) return;
                CentralGroupPolicyWorkflow.RequireNoPending(libraryId);
                endpointStore.Save(proposed);
                centralStatus.Text = "HTTPS-Zentrale lokal hinterlegt. Kryptografischer Abgleich wird versucht.";
                await SynchronizeCurrentAsync();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        syncSettings.Children.Add(storeConnection);
        root.Children.Add(new Expander { Header = "Zentralverbindung · HTTPS und Zertifikat",
            Content = syncSettings, IsExpanded = false, Margin = new Thickness(0, 8, 0, 4) });
        root.Children.Add(status);
        root.Children.Add(centralStatus);
        resumeMerge.Visibility = Visibility.Collapsed;
        resumeMerge.Click += async (_, _) => await SynchronizeCurrentAsync();
        root.Children.Add(resumeMerge);
        reviewMerge.Click += async (_, _) => await ReviewMergeAsync();
        root.Children.Add(new Expander { Header = "Erweitert · Bibliotheksbestände abgleichen",
            Content = reviewMerge, IsExpanded = false, Margin = new Thickness(0, 6, 0, 4) });
        root.Children.Add(error);
        var close = ActionButton("Schließen");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        root.Children.Add(close);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        groupSelection.SelectionChanged += async (_, _) =>
        {
            if (Execute(Refresh)) await SynchronizeCurrentAsync();
        };
        customerSelection.SelectionChanged += (_, _) => Execute(RefreshKeys);
        keys.SelectionChanged += (_, _) => RefreshActions();
        create.Click += async (_, _) => await ExecuteAndSyncAsync(CreateEmpty);
        addCustomer.Click += async (_, _) => await ExecuteAndSyncAsync(AddCustomer);
        generate.Click += async (_, _) => await ExecuteAndSyncAsync(() => AddKey(generateValue: true));
        import.Click += async (_, _) => await ExecuteAndSyncAsync(() => AddKey(generateValue: false));
        versionGenerate.Click += async (_, _) => await ExecuteAndSyncAsync(() => AddVersion(generateValue: true));
        versionImport.Click += async (_, _) => await ExecuteAndSyncAsync(() => AddVersion(generateValue: false));
        retire.Click += async (_, _) => await ExecuteAndSyncAsync(() => SetStatus(KeyLibraryContracts.Retired));
        compromise.Click += async (_, _) => await ExecuteAndSyncAsync(() => SetStatus(KeyLibraryContracts.Compromised));
        reveal.Click += async (_, _) => await RevealAsync();
        try
        {
            var savedEndpoint = endpointStore.Load();
            if (savedEndpoint is not null)
            {
                serverOrigin.Text = savedEndpoint.HttpsOrigin;
                serverPin.Text = savedEndpoint.ServerCertificateSha256;
                centralStatus.Text = "Zentralverbindung konfiguriert; wird bei Gruppenauswahl geprüft.";
            }
            else centralStatus.Text = "Offline-Betrieb. Keine bestätigte Zentralverbindung hinterlegt.";
        }
        catch (Exception exception)
        {
            // Corrupted optional connection settings must not make the entire
            // offline key-management window unusable.
            centralStatus.Text = "Zentralverbindung ungültig; kein automatischer Netzabruf. " +
                exception.Message;
        }
        var policy = trust.LoadOffline();
        groupSelection.ItemsSource = policy.Policy.Groups.Select(g => new GroupChoice(g.Id, g.Name)).ToArray();
        var pendingGroup = CentralGroupPolicyWorkflow.PendingMerge(libraryId)?.PendingExpected?.GroupId;
        if (pendingGroup is not null)
            groupSelection.SelectedItem = groupSelection.Items.OfType<GroupChoice>().SingleOrDefault(g => g.Id == pendingGroup);
        else if (groupSelection.Items.Count == 1) groupSelection.SelectedIndex = 0;
        RefreshActions();
    }

    private static Button ActionButton(string label) => new() { Content = label, Margin = new Thickness(3) };
    private static TextBlock Note(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 6) };
    private GroupChoice CurrentGroup() => groupSelection.SelectedItem as GroupChoice
        ?? throw new InvalidOperationException("Bitte zuerst die freigegebene Schlüsselgruppe wählen.");
    private string CurrentPath() => KeyLibraryVaultStore.DefaultPath(libraryId, CurrentGroup().Id);

    private void RequireAuthority()
    {
        CentralGroupPolicyWorkflow.RequireNoPending(libraryId);
        administration.RequireLocalAuthority(trust);
    }

    private T WithIdentities<T>(Func<System.Security.Cryptography.X509Certificates.X509Certificate2[], T> operation)
    {
        var identities = WindowsVaultIdentityService.List();
        try { return operation(identities.Select(i => i.Certificate).ToArray()); }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    private KeyLibraryVaultPayload OpenCurrent()
    {
        CentralGroupPolicyWorkflow.RequireNoPolicyPending(libraryId);
        administration.RequireLocalAuthority(trust);
        var path = CurrentPath();
        return WithIdentities(ids => KeyLibraryVaultStore.Load(path, trust, CurrentGroup().Id, ids, requireFull: true));
    }

    private void Refresh()
    {
        error.Text = "";
        keys.ItemsSource = null;
        customerSelection.ItemsSource = null;
        var selected = groupSelection.SelectedItem as GroupChoice;
        if (selected is null) { status.Text = "Bitte eine Schlüsselgruppe wählen."; RefreshActions(); return; }
        if (!File.Exists(CurrentPath()))
        {
            status.Text = "Noch keine lokale verschlüsselte Schlüsselbibliothek für diese Gruppe vorhanden. Nur eine zentrale Bibliothek pro Verwaltung zulässig.";
            RefreshActions();
            return;
        }
        var state = OpenCurrent();
        KeyLibraryVault.Validate(state);
        var customerIds = state.Catalog.Keys.Select(k => k.CustomerId)
            .Except(state.Catalog.Customers.Select(c => c.CustomerId)).Distinct();
        var customers = state.Catalog.Customers
            .Concat(customerIds.Select(id => new KeyLibraryCustomer(id, "Unbenannter Kunde " + id.ToString("N")[..8])))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var prior = (customerSelection.SelectedItem as KeyLibraryCustomer)?.CustomerId;
        customerSelection.ItemsSource = customers;
        customerSelection.SelectedItem = customers.FirstOrDefault(c => c.CustomerId == prior) ??
            (customers.Length == 1 ? customers[0] : null);
        status.Text = $"Lokaler Vault geöffnet · Revision {state.Catalog.Revision} · {state.Catalog.Keys.Length} Schlüsselversionen · Zentralstatus separat darunter. Geheimwerte werden nicht in der Übersicht ausgegeben.";
        RefreshKeys();
    }

    private void RefreshKeys()
    {
        var selection = (customerSelection.SelectedItem as KeyLibraryCustomer)?.CustomerId;
        var current = groupSelection.SelectedItem is GroupChoice && File.Exists(CurrentPath())
            ? OpenCurrent() : null;
        var names = current?.Catalog.Customers.ToDictionary(c => c.CustomerId, c => c.Name)
            ?? new Dictionary<Guid, string>();
        keys.ItemsSource = current?.Catalog.Keys
            .Where(k => selection is null || k.CustomerId == selection)
            .OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(k => k.KeyId).ThenBy(k => k.Version)
            .Select(k => new KeyRow(k, names.GetValueOrDefault(k.CustomerId, "Unbenannter Kunde " + k.CustomerId.ToString("N")[..8])))
            .ToArray();
        RefreshActions();
    }

    private void RefreshActions()
    {
        var group = groupSelection.SelectedItem is GroupChoice;
        var exists = group && File.Exists(CurrentPath());
        var entry = (keys.SelectedItem as KeyRow)?.Entry;
        bool pending;
        try { pending = CentralGroupPolicyWorkflow.PendingMerge(libraryId) is not null; }
        catch (Exception exception) { pending = true; error.Text = exception.Message; }
        resumeMerge.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        resumeMerge.IsEnabled = pending && !syncing && group;
        var editable = !syncing && !pending;
        reviewMerge.IsEnabled = editable && exists;
        create.IsEnabled = editable && group && !exists;
        addCustomer.IsEnabled = editable && exists;
        generate.IsEnabled = import.IsEnabled = editable && exists &&
            customerSelection.SelectedItem is KeyLibraryCustomer;
        versionGenerate.IsEnabled = versionImport.IsEnabled = editable && exists && entry is not null;
        retire.IsEnabled = editable && exists && entry is { Status: KeyLibraryContracts.Active };
        compromise.IsEnabled = editable && exists && entry is not null &&
            entry.Status != KeyLibraryContracts.Compromised;
        reveal.IsEnabled = editable && exists && entry is not null;
    }

    private void CreateEmpty()
    {
        RequireAuthority();
        var group = CurrentGroup();
        var path = CurrentPath();
        var directory = Path.GetDirectoryName(path)!;
        if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.key-library.vault.json").Any())
            throw new InvalidOperationException("Für diese zentrale Verwaltung besteht bereits ein verschlüsselter Bibliotheks-Vault. Keine zweite, unabhängige Datenwahrheit erstellen.");
        if (MessageBox.Show(this,
            "Einen leeren, verschlüsselten lokalen Schlüsselbestand anlegen? Dies ist noch keine zentral synchronisierte Bibliothek. Gruppenmitglieder und ein getrennter Recovery-Zugang müssen signiert freigegeben sein.",
            Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var empty = new KeyLibraryVaultPayload(new KeyLibrarySnapshot(libraryId, 1, []), []);
        var authorization = administration.SignKeyLibraryMutation(trust, "create",
            group.Id, FileRevision.Missing, empty);
        WithIdentities(ids => KeyLibraryVaultStore.Create(path, empty, trust,
            group.Id, ids, DateTimeOffset.UtcNow, authorization, administration));
        Refresh();
    }

    private void Commit(Func<KeyLibraryVaultPayload, KeyLibraryVaultPayload> edit)
    {
        RequireAuthority();
        var group = CurrentGroup();
        var path = CurrentPath();
        var expected = ConcurrentFileWriter.Capture(path);
        if (!expected.Exists) throw new FileNotFoundException("Schlüsselbibliothek fehlt.", path);
        WithIdentities(ids =>
        {
            var original = KeyLibraryVaultStore.Load(path, trust, group.Id, ids, requireFull: true);
            var next = edit(original);
            var authorization = administration.SignKeyLibraryMutation(trust,
                "update", group.Id, expected, next);
            KeyLibraryVaultStore.Update(path, next, trust, group.Id, ids,
                expected, DateTimeOffset.UtcNow, authorization, administration);
            var verified = KeyLibraryVaultStore.Load(path, trust, group.Id, ids, requireFull: true);
            if (verified.Catalog.Revision != next.Catalog.Revision)
                throw new InvalidDataException("Gespeicherte Bibliotheksrevision stimmt nicht mit der Änderung überein.");
            return true;
        });
        Refresh();
    }

    private void AddCustomer()
    {
        var name = TextEntryPrompt.Show(this, "Kunden anlegen", "Kundenbezeichnung (unveränderliche Kunden-ID wird automatisch erstellt)", "");
        if (name is null) return;
        Commit(current => KeyLibraryEditing.AddCustomer(current, name).Next);
    }

    private void AddKey(bool generateValue)
    {
        if (customerSelection.SelectedItem is not KeyLibraryCustomer customer)
            throw new InvalidOperationException("Bitte zuerst einen Kunden wählen.");
        var input = KeyLibraryEntryPrompt.Show(this, generateValue, null);
        if (input is null) return;
        Commit(current => generateValue
            ? KeyLibraryEditing.GenerateNew(current, customer.CustomerId, input.Name, input.KeyType, input.Purpose, input.HostSystem).Next
            : KeyLibraryEditing.ImportNew(current, customer.CustomerId, input.Name, input.KeyType, input.Purpose, input.HostSystem, input.Secret).Next);
    }

    private KeyLibraryEntry SelectedKey() => (keys.SelectedItem as KeyRow)?.Entry
        ?? throw new InvalidOperationException("Bitte zuerst einen Schlüssel auswählen.");

    private void AddVersion(bool generateValue)
    {
        var selected = SelectedKey();
        var input = KeyLibraryEntryPrompt.Show(this, generateValue, selected);
        if (input is null) return;
        Commit(current => KeyLibraryEditing.AddVersion(current, selected.KeyId,
            generateValue ? KeyVaultService.GenerateKey(selected.KeyType) : input.Secret).Next);
    }

    private void SetStatus(string nextStatus)
    {
        var selected = SelectedKey();
        if (MessageBox.Show(this,
            $"Schlüssel {selected.Name}, v{selected.Version} wirklich auf '{nextStatus}' setzen? Das ändert keine bestehenden Karten, Leser oder Hostsysteme. Eine kompromittierte Version kann nicht reaktiviert werden.",
            Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Commit(current => KeyLibraryEditing.SetStatus(current,
            new KeyLibraryAddress(selected.KeyId, selected.Version), nextStatus));
    }

    private async Task RevealAsync()
    {
        if (syncing) return;
        syncing = true; groupSelection.IsEnabled = false; RefreshActions();
        var identities = Array.Empty<WindowsVaultIdentity>();
        try
        {
            RequireAuthority();
            var selected = SelectedKey();
            var group = CurrentGroup().Id;
            var path = CurrentPath();
            var policy = trust.LoadOffline();
            var file = ConcurrentFileWriter.Capture(path);
            identities = WindowsVaultIdentityService.List().ToArray();
            X509Certificate2? identity = null;
            KeyLibraryDisclosureAccess? actor = null;
            KeyLibraryVaultPayload? current = null;
            foreach (var candidate in identities)
            {
                try
                {
                    var access = KeyLibraryDisclosure.RequireMember(policy, group, candidate.Certificate, DateTimeOffset.UtcNow);
                    current = KeyLibraryVaultStore.Load(path, trust, group, [candidate.Certificate], requireFull: true);
                    identity = candidate.Certificate; actor = access; break;
                }
                catch (CryptographicException) { }
            }
            if (identity is null || actor is null || current is null)
                throw new CryptographicException("Aktiver persönlicher Zugang zur Hauptverwaltung fehlt.");
            if (ConcurrentFileWriter.Capture(path) != file || trust.LoadOffline().Checkpoint != policy.Checkpoint ||
                current.Catalog.Keys.SingleOrDefault(k => k.KeyId == selected.KeyId && k.Version == selected.Version) != selected)
                throw new ConcurrentFileChangeException(path);
            var revision = current.Catalog.Revision;
            current = null;
            var warning = selected.Status == KeyLibraryContracts.Active ? "" :
                "\n\nHistorische oder kompromittierte Version: nicht für neue produktive Konfigurationen verwenden.";
            if (MessageBox.Show(this,
                $"Schlüsselwert für {selected.Name} v{selected.Version} bewusst im Klartext anzeigen? Die Anforderung wird lokal protokolliert. Kopien bleiben außerhalb des Vaults verfügbar." + warning,
                Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            string ReadAndRecord(bool copying)
            {
                // Main authority must remain locally available in addition to
                // the exact enrolled personal decryption identity.
                RequireAuthority();
                var now = DateTimeOffset.UtcNow;
                var verified = trust.LoadOffline();
                var active = KeyLibraryDisclosure.RequireMember(verified, group, identity, now);
                if (active.PersonId != actor.PersonId || active.DeviceFingerprint != actor.DeviceFingerprint ||
                    verified.Checkpoint != policy.Checkpoint || ConcurrentFileWriter.Capture(path) != file)
                    throw new ConcurrentFileChangeException(path);
                var opened = KeyLibraryVaultStore.Load(path, trust, group, [identity], requireFull: true);
                if (opened.Catalog.Revision != revision ||
                    opened.Catalog.Keys.SingleOrDefault(k => k.KeyId == selected.KeyId && k.Version == selected.Version) != selected ||
                    trust.LoadOffline().Checkpoint != policy.Checkpoint || ConcurrentFileWriter.Capture(path) != file)
                    throw new ConcurrentFileChangeException(path);
                RequireAuthority();
                KeyLibraryAuditWorkflow.Record(libraryId,
                    new(Guid.NewGuid(), now, libraryId, group, active.PersonId,
                        active.DeviceFingerprint, selected.CustomerId, selected.KeyId, selected.Version,
                        revision, policy.Checkpoint, copying ? "copy-requested" : "view-requested"), identity);
                return opened.Secrets.Single(k => k.Key.KeyId == selected.KeyId && k.Key.Version == selected.Version).HexValue;
            }
            var value = ReadAndRecord(false);
            new KeyLibrarySecretViewWindow(this, selected, value, true, () => ReadAndRecord(true)).ShowDialog();
            status.Text = await KeyLibraryAuditWorkflow.TryFlushAsync(libraryId, identity);
        }
        catch (Exception exception) { error.Text = "Klartextanforderung nicht abgeschlossen: " + exception.Message; }
        finally
        {
            foreach (var item in identities) item.Certificate.Dispose();
            syncing = false; groupSelection.IsEnabled = true; RefreshActions();
        }
    }

    // Parent GroupAdministrationWindow holds the shared library lease for
    // this modal window. Never acquire a second lease or run a normal sync
    // during the preview, confirmation or staging operation.
    private async Task ReviewMergeAsync()
    {
        if (syncing) return;
        syncing = true;
        groupSelection.IsEnabled = false;
        RefreshActions();
        WindowsVaultIdentity[] identities = [];
        string? temporary = null;
        var staged = false;
        try
        {
            RequireAuthority();
            var group = CurrentGroup().Id;
            var localPath = CurrentPath();
            var endpoint = endpointStore.Load() ?? throw new InvalidOperationException("Bestätigte HTTPS-Zentrale fehlt.");
            var basis = cursorStore.ReadReconciledHead() ?? throw new InvalidOperationException("Kein bestätigter gemeinsamer Ausgangsstand vorhanden.");
            var enrollment = trust.ExportPublic();
            var policy = GroupEnrollmentPackages.Validate(enrollment);
            if (basis.GroupId != group || basis.Policy != policy.Checkpoint || !policy.Policy.CustomerScopesEnabled)
                throw new InvalidOperationException("Zusammenführung benötigt den aktuellen abgeglichenen Kundenbereich.");
            identities = WindowsVaultIdentityService.List().ToArray();
            X509Certificate2? member = null;
            foreach (var candidate in identities)
            {
                try
                {
                    var access = KeyLibraryDisclosure.RequireMember(policy, group, candidate.Certificate, DateTimeOffset.UtcNow);
                    if (!policy.Policy.People.Single(p => p.Id == access.PersonId).AllKeyLibraryCustomers) continue;
                    _ = KeyLibraryVaultStore.Load(localPath, trust, group, [candidate.Certificate], requireFull: true);
                    member = candidate.Certificate; break;
                }
                catch (CryptographicException) { }
            }
            if (member is null) throw new CryptographicException("Vollständiger persönlicher Bibliothekszugang fehlt.");

            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin), libraryId,
                endpoint.ServerCertificateSha256, member);
            var head = await transport.ReadHeadAsync();
            if (head != basis || !cursorStore.HasUploadBase(basis))
                throw new InvalidOperationException("Zentrale weicht vom zuletzt abgeglichenen Stand ab. " +
                    "Diese Mehrplatz-Konfliktart ist noch nicht zur Zusammenführung freigegeben. Keine Daten verändert.");
            var extra = MessageBox.Show(this, "Weitere verschlüsselte Offline-Bibliotheken hinzufügen?\n" +
                "Ja: Dateien wählen. Nein: nur Zentralstand und lokalen Bestand vergleichen.",
                Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (extra == MessageBoxResult.Cancel) return;
            string[] paths = [];
            if (extra == MessageBoxResult.Yes)
            {
                var open = new OpenFileDialog { Title = "Verschlüsselte Bibliothekskopien auswählen",
                    Filter = "Verschlüsselte Bibliothek (*.json)|*.json", Multiselect = true, CheckFileExists = true };
                if (open.ShowDialog(this) != true) return;
                paths = open.FileNames.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (paths.Length > 6 || paths.Any(path => string.Equals(path, localPath, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Maximal sechs weitere, vom aktiven Vault verschiedene Dateien auswählen.");
            }
            var transfer = await transport.DownloadAsync(KeyLibraryDownloadAuthorization.Sign(head, member, DateTimeOffset.UtcNow));
            var claim = KeyLibrarySyncTransport.Verify(transfer, enrollment);
            if (claim.LibraryId != head.LibraryId || claim.Policy != head.Policy || claim.GroupId != group ||
                claim.ExpectedCentralRevision + 1 != head.CentralRevision ||
                claim.CatalogRevision != head.CatalogRevision || claim.NextCiphertextSha256 != head.CiphertextSha256 ||
                await transport.ReadHeadAsync() != head)
                throw new CryptographicException("Heruntergeladener Ausgangsstand stimmt nicht mit der bestätigten Zentrale überein.");
            temporary = Path.Combine(Path.GetDirectoryName(localPath)!, ".merge-review-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            var basePath = Path.Combine(temporary, "central-base.json");
            using (var file = new FileStream(basePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(transfer.EncryptedVault);
                file.Flush(true);
            }
            var baseId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var sources = new List<KeyLibraryDisasterSource>
            {
                new(baseId, basePath, enrollment, policy.Checkpoint),
                new(localId, localPath, enrollment, policy.Checkpoint)
            };
            var names = new Dictionary<Guid, string> { [baseId] = "Zentralstand", [localId] = "Lokaler Bestand" };
            foreach (var path in paths)
            {
                var id = Guid.NewGuid();
                names.Add(id, $"Quelle {names.Count - 1} · {Path.GetFileName(path)}");
                sources.Add(new(id, path, enrollment, policy.Checkpoint));
            }
            var review = KeyLibraryDisasterAnalysis.ReviewMerge(trust, administration, group, member,
                DateTimeOffset.UtcNow, sources, head, baseId);
            var dialog = new KeyLibraryMergeReviewWindow(this, review, names);
            if (dialog.ShowDialog() != true) return;
            var resolved = KeyLibraryDisasterAnalysis.PreviewMerge(trust, group, member, false,
                DateTimeOffset.UtcNow, sources, head, baseId, dialog.Decisions);
            if (!resolved.Inventory.Sources.SequenceEqual(review.Preview.Inventory.Sources))
                throw new ConcurrentFileChangeException("Quellbestände der Zusammenführung");
            if (!resolved.CanPrepareCandidate)
            {
                centralStatus.Text = resolved.Issues.Length > 0
                    ? "Konflikte verbleiben. Keine Zusammenführung gespeichert."
                    : "Keine Änderungen zusammenzuführen.";
                return;
            }
            var summary = $"Neue Katalogrevision: {resolved.ProposedRevision}\n" +
                $"Ergänzungen: {resolved.AddedCustomers} Kunden, {resolved.AddedKeyVersions} Schlüsselversionen\n" +
                $"Ausdrückliche Konfliktentscheidungen: {resolved.Decisions.Length}\n\n" +
                "Zusammenführung verbindlich bestätigen? Der signierte verschlüsselte Auftrag wird dauerhaft gespeichert und zentral abgeglichen.";
            if (MessageBox.Show(this, summary, Title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes) return;
            if (endpointStore.Load() != endpoint || await transport.ReadHeadAsync() != head)
                throw new ConcurrentFileChangeException("Zentralstand der Zusammenführung");
            CentralGroupPolicyWorkflow.StageMergeUnderLease(libraryId, trust, administration, member,
                sources, head, baseId, resolved.ApprovalSha256(head), true, resolved.Decisions);
            staged = true;
            centralStatus.Text = "Zusammenführung vorbereitet. Zentraler Abgleich ausstehend.";
        }
        catch (Exception exception)
        {
            error.Text = "Zusammenführung nicht abgeschlossen: " + exception.Message;
        }
        finally
        {
            foreach (var identity in identities) identity.Certificate.Dispose();
            if (temporary is not null)
            {
                try { Directory.Delete(temporary, recursive: true); }
                catch (IOException) { error.Text += " Verschlüsselter Prüfbestand konnte nicht entfernt werden."; }
                catch (UnauthorizedAccessException) { error.Text += " Verschlüsselter Prüfbestand konnte nicht entfernt werden."; }
            }
            syncing = false;
            groupSelection.IsEnabled = true;
            RefreshActions();
        }
        if (staged) await SynchronizeCurrentAsync();
    }

    private async Task ExecuteAndSyncAsync(Action action)
    {
        if (syncing)
        {
            error.Text = "Zentraler Abgleich läuft. Bitte zuerst den aktuellen Revisionsstand abwarten.";
            return;
        }
        if (Execute(action)) await SynchronizeCurrentAsync();
    }

    private async Task SynchronizeCurrentAsync()
    {
        if (syncing || groupSelection.SelectedItem is not GroupChoice group) return;
        bool mergePending;
        try
        {
            CentralGroupPolicyWorkflow.RequireNoPolicyPending(libraryId);
            mergePending = CentralGroupPolicyWorkflow.PendingMerge(libraryId) is not null;
        }
        catch (Exception exception) { centralStatus.Text = exception.Message; return; }
        KeyLibrarySyncEndpoint? endpoint;
        try { endpoint = endpointStore.Load(); }
        catch (Exception exception)
        {
            centralStatus.Text = "Zentralverbindung nicht vertrauenswürdig: " + exception.Message;
            return;
        }
        if (endpoint is null)
        {
            centralStatus.Text = mergePending
                ? "Zusammenführung ausstehend. Bestätigte HTTPS-Zentrale erforderlich; keine weitere Änderung."
                : "Offline-Betrieb: Keine bestätigte HTTPS-Zentrale eingerichtet.";
            return;
        }

        syncing = true;
        groupSelection.IsEnabled = false;
        RefreshActions();
        WindowsVaultIdentity[] identities = [];
        try
        {
            identities = WindowsVaultIdentityService.List().ToArray();
            if (mergePending)
            {
                centralStatus.Text = "Ausstehende Zusammenführung wird geprüft und fortgesetzt.";
                var head = await CentralGroupPolicyWorkflow.ResumeMergeUnderLeaseAsync(libraryId,
                    group.Id, trust, administration, identities.Select(i => i.Certificate));
                centralStatus.Text = $"Zusammenführung zentral bestätigt · Katalogrevision {head.CatalogRevision}, Serverrevision {head.CentralRevision}";
                Refresh();
                return; // Do not run normal upload/download in the same pass.
            }
            CentralGroupPolicyWorkflow.RequireNoPending(libraryId);
            var policy = trust.LoadOffline().Policy;
            var allowedGroup = policy.Groups.SingleOrDefault(g => g.Id == group.Id)
                ?? throw new CryptographicException("Signierte Schlüsselgruppe fehlt.");
            var permitted = policy.People.Where(p => p.Enabled &&
                    (!policy.CustomerScopesEnabled || p.AllKeyLibraryCustomers) &&
                    allowedGroup.Members.Contains(p.Id, StringComparer.Ordinal))
                .SelectMany(p => p.Devices).Where(d => d.Enabled)
                .Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
            var certificate = identities.Select(i => i.Certificate)
                .FirstOrDefault(c => c.HasPrivateKey &&
                    permitted.Contains(CertificateVaultService.Fingerprint(c)));
            if (certificate is null)
            {
                centralStatus.Text = "Zentralabgleich nicht möglich: Kein aktives persönliches Gruppen-Zertifikat auf diesem Windows-Benutzer.";
                return;
            }
            using var transport = new KeyLibraryHttpsTransport(
                new Uri(endpoint.HttpsOrigin), libraryId,
                endpoint.ServerCertificateSha256, certificate);
            var result = await KeyLibraryAutomaticSync.SynchronizeOnceAsync(
                transport, CurrentPath(), trust, cursorStore, group.Id, certificate,
                administration);
            centralStatus.Text = result.Outcome switch
            {
                KeyLibrarySyncOutcome.InSync =>
                    $"Zentrale bestätigt · Katalogrevision {result.Central.CatalogRevision}, Serverrevision {result.Central.CentralRevision}",
                KeyLibrarySyncOutcome.Downloaded =>
                    $"Aktueller zentraler Bestand offline übernommen · Katalogrevision {result.Central.CatalogRevision}",
                KeyLibrarySyncOutcome.Uploaded =>
                    $"Lokale Änderung zentral bestätigt · Katalogrevision {result.Central.CatalogRevision}, Serverrevision {result.Central.CentralRevision}",
                KeyLibrarySyncOutcome.Empty =>
                    "Zentrale erreichbar, aber noch keine Bibliothek vorhanden.",
                KeyLibrarySyncOutcome.AdministrationRequired =>
                    "Lokaler Stand noch nicht zentral bestätigt: gültige Verwaltungsfreigabe erforderlich.",
                KeyLibrarySyncOutcome.Conflict =>
                    "Zentraler Konflikt: Unterschiedliche Bibliotheksstände. Kein Schlüssel wurde automatisch überschrieben.",
                _ => "Zentraler Abgleich liefert keinen eindeutigen Status."
            };
            centralStatus.Text += await KeyLibraryAuditWorkflow.FlushAsync(libraryId, transport, certificate);
            if (result.Outcome == KeyLibrarySyncOutcome.Downloaded) Refresh();
        }
        catch (HttpRequestException)
        {
            centralStatus.Text = mergePending
                ? "Zusammenführung weiterhin ausstehend. Zentralen Stand erneut prüfen; keine weitere Änderung freigegeben."
                : "Zentrale nicht erreichbar oder HTTPS-Vertrauen fehlgeschlagen. Lokale Schlüsselbibliothek bleibt offline verfügbar; kein Upload bestätigt.";
        }
        catch (TaskCanceledException)
        {
            centralStatus.Text = "Zentraler Abgleich nicht abgeschlossen (Timeout). Zentralen Stand erneut prüfen; lokaler Bestand bleibt erhalten.";
        }
        catch (ConcurrentFileChangeException)
        {
            centralStatus.Text = "Zentraler oder lokaler Revisionskonflikt. Kein automatisches Überschreiben; aktuellen Stand erneut prüfen.";
        }
        catch (CryptographicException)
        {
            centralStatus.Text = "Zentralabgleich wegen Zertifikat-, Signatur- oder Berechtigungsfehler abgewiesen. Keine zentrale Bestätigung.";
        }
        catch (Exception exception)
        {
            centralStatus.Text = "Zentralabgleich fehlgeschlagen: " + exception.Message +
                " (lokalen und zentralen Revisionsstand prüfen).";
        }
        finally
        {
            foreach (var identity in identities) identity.Certificate.Dispose();
            syncing = false;
            groupSelection.IsEnabled = true;
            RefreshActions();
        }
    }

    private bool Execute(Action operation)
    {
        try { error.Text = ""; operation(); return true; }
        catch (Exception ex)
        {
            error.Text = ex.Message;
            status.Text = "Vorgang nicht vollständig bestätigt. Bei Speicher-/Prüffehlern den aktuellen Bibliotheksstand erneut öffnen und die Revision kontrollieren; es kann bereits eine Änderung geschrieben worden sein.";
            RefreshActions();
            return false;
        }
    }
}

internal sealed record KeyLibraryEntryInput(string Name, string KeyType, string Purpose, string HostSystem, string Secret);

internal sealed class KeyLibraryEntryPrompt : Window
{
    private readonly TextBox name = new() { MinWidth = 400 };
    private readonly ComboBox type = new() { ItemsSource = new[] { "DF_KEY_AES", "DF_KEY_DES", "DF_KEY_3K3DES" } };
    private readonly TextBox purpose = new();
    private readonly TextBox host = new();
    private readonly PasswordBox secret = new();
    private KeyLibraryEntryInput? result;
    private KeyLibraryEntryPrompt(Window owner, bool generateValue, KeyLibraryEntry? previous)
    {
        Owner = owner;
        Title = previous is null ? generateValue ? "Neuen Schlüssel erzeugen" : "Vorhandenen Schlüssel importieren"
            : generateValue ? "Neue Schlüsselversion erzeugen" : "Neue Schlüsselversion importieren";
        Width = 580; MaxHeight = 650; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        if (previous is null)
        {
            panel.Children.Add(Note("Eindeutige Schlüsselbezeichnung")); panel.Children.Add(name);
            panel.Children.Add(Note("DESFire-Schlüsseltyp")); type.SelectedIndex = 0; panel.Children.Add(type);
            panel.Children.Add(Note("Verwendungszweck (z. B. PICC oder Application)")); panel.Children.Add(purpose);
            panel.Children.Add(Note("Hostsystem (z. B. SIPORT)")); panel.Children.Add(host);
        }
        else panel.Children.Add(Note($"Bestehende Schlüssel-ID {previous.KeyId:D}, Typ {previous.KeyType}, derzeit v{previous.Version}. Eine neue Version ändert keine Projektbindung automatisch."));
        if (!generateValue)
        {
            panel.Children.Add(Note("Hex-Schlüsselwert (verdeckt; wird ausschließlich verschlüsselt gespeichert)"));
            panel.Children.Add(secret);
        }
        var warning = Note("Der neue Stand wird nur lokal gespeichert und ist noch nicht zentral bestätigt. Niemals echte Kundenschlüssel in einem Entwicklungs-/Testlauf verwenden.");
        panel.Children.Add(warning);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Übernehmen", IsDefault = true };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions);
        save.Click += (_, _) =>
        {
            var chosenType = previous?.KeyType ?? type.SelectedItem as string ?? "";
            var chosenName = previous?.Name ?? name.Text.Trim();
            if (string.IsNullOrWhiteSpace(chosenName) || chosenType.Length == 0 || !generateValue && string.IsNullOrWhiteSpace(secret.Password))
            {
                MessageBox.Show(this, "Bitte alle erforderlichen Felder ausfüllen.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            result = new KeyLibraryEntryInput(chosenName, chosenType, purpose.Text.Trim(),
                host.Text.Trim(), generateValue ? "" : secret.Password.Trim());
            secret.Clear();
            DialogResult = true;
        };
        cancel.Click += (_, _) => DialogResult = false;
        Closed += (_, _) => secret.Clear();
        Content = panel;
    }

    internal static KeyLibraryEntryInput? Show(Window owner, bool generateValue, KeyLibraryEntry? previous)
    {
        var dialog = new KeyLibraryEntryPrompt(owner, generateValue, previous);
        return dialog.ShowDialog() == true ? dialog.result : null;
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 5) };
}

internal sealed class KeyLibrarySecretViewWindow : Window
{
    internal KeyLibrarySecretViewWindow(Window owner, KeyLibraryEntry key, string value,
        bool allowCopy = true, Func<string>? verifyCopy = null)
    {
        Owner = owner;
        Title = $"Schlüsselwert · {key.Name} · v{key.Version}";
        Width = 580; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Sichtbarer Geheimwert – nur für berechtigte Personen. Eine Kopie in der Zwischenablage bleibt möglicherweise auch nach dem Schließen erhalten. Keine automatische Änderung an Karten, Lesern oder SIPORT.", TextWrapping = TextWrapping.Wrap });
        var display = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
        panel.Children.Add(display);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var copy = new Button { Content = "Für Hostkonfiguration kopieren" };
        copy.IsEnabled = allowCopy;
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(verifyCopy is null ? value : verifyCopy()); }
            catch (Exception exception)
            {
                MessageBox.Show(this, "Kopieren nicht abgeschlossen: " + exception.Message,
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        var close = new Button { Content = "Schließen" };
        close.Click += (_, _) => Close();
        actions.Children.Add(copy); actions.Children.Add(close); panel.Children.Add(actions);
        Closed += (_, _) => display.Text = "";
        Content = panel;
    }
}

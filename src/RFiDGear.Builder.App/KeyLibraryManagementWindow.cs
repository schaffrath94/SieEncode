using System.IO;
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
    private readonly TextBox serverOrigin = new() { MinWidth = 310 };
    private readonly TextBox serverPin = new() { MinWidth = 420, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBlock centralStatus = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateGray };
    private bool syncing;
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
            new WindowsGroupTrustProtection(libraryId));
        Title = "SieEncode · Zentrale Schlüsselverwaltung (lokaler Entwicklungsstand)";
        Width = 990;
        MaxHeight = 870;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        administration.RequireLocalAuthority(trust);

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
            try
            {
                var proposed = KeyLibrarySyncEndpointStore.Validate(
                    new KeyLibrarySyncEndpoint(libraryId, serverOrigin.Text.Trim(),
                        serverPin.Text.Trim().ToUpperInvariant()));
                if (MessageBox.Show(this,
                    $"Bitte den folgenden vollständigen Fingerabdruck über einen unabhängigen vertrauenswürdigen Kanal prüfen:\\n\\n{proposed.ServerCertificateSha256}\\n\\nIst er unabhängig bestätigt und ist die HTTPS-Adresse die richtige Zentrale?",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes) return;
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
        reveal.Click += (_, _) => Execute(Reveal);
        var savedEndpoint = endpointStore.Load();
        if (savedEndpoint is not null)
        {
            serverOrigin.Text = savedEndpoint.HttpsOrigin;
            serverPin.Text = savedEndpoint.ServerCertificateSha256;
            centralStatus.Text = "Zentralverbindung konfiguriert; wird bei Gruppenauswahl geprüft.";
        }
        else centralStatus.Text = "Offline-Betrieb. Keine bestätigte Zentralverbindung hinterlegt.";
        var policy = trust.LoadOffline();
        groupSelection.ItemsSource = policy.Policy.Groups.Select(g => new GroupChoice(g.Id, g.Name)).ToArray();
        if (groupSelection.Items.Count == 1) groupSelection.SelectedIndex = 0;
        RefreshActions();
    }

    private static Button ActionButton(string label) => new() { Content = label, Margin = new Thickness(3) };
    private static TextBlock Note(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 6) };
    private GroupChoice CurrentGroup() => groupSelection.SelectedItem as GroupChoice
        ?? throw new InvalidOperationException("Bitte zuerst die freigegebene Schlüsselgruppe wählen.");
    private string CurrentPath() => KeyLibraryVaultStore.DefaultPath(libraryId, CurrentGroup().Id);

    private void RequireAuthority()
    {
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
        RequireAuthority();
        var path = CurrentPath();
        return WithIdentities(ids => KeyLibraryVaultStore.Load(path, trust, CurrentGroup().Id, ids));
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
        status.Text = $"Lokaler Vault geöffnet · Revision {state.Catalog.Revision} · {state.Catalog.Keys.Length} Schlüsselversionen · keine zentrale Bestätigung. Geheime Werte werden nicht in der Übersicht ausgegeben.";
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
        create.IsEnabled = group && !exists;
        addCustomer.IsEnabled = exists;
        generate.IsEnabled = import.IsEnabled = exists && customerSelection.SelectedItem is KeyLibraryCustomer;
        versionGenerate.IsEnabled = versionImport.IsEnabled = exists && entry is not null;
        retire.IsEnabled = exists && entry is { Status: KeyLibraryContracts.Active };
        compromise.IsEnabled = exists && entry is not null &&
            entry.Status != KeyLibraryContracts.Compromised;
        reveal.IsEnabled = exists && entry is not null;
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
            group.Id, ids, DateTimeOffset.UtcNow, authorization));
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
            var original = KeyLibraryVaultStore.Load(path, trust, group.Id, ids);
            var next = edit(original);
            var authorization = administration.SignKeyLibraryMutation(trust,
                "update", group.Id, expected, next);
            KeyLibraryVaultStore.Update(path, next, trust, group.Id, ids,
                expected, DateTimeOffset.UtcNow, authorization);
            var verified = KeyLibraryVaultStore.Load(path, trust, group.Id, ids);
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

    private void Reveal()
    {
        var selected = SelectedKey();
        if (MessageBox.Show(this,
            $"Schlüsselwert für {selected.Name} v{selected.Version} bewusst im Klartext anzeigen? Nur die angemeldete Hauptverwaltung mit persönlicher Gruppenzulassung erhält Zugriff. Die Anzeige und Zwischenablage sind sensible Bereiche.",
            Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var current = OpenCurrent();
        var metadata = current.Catalog.Keys.Single(k => k.KeyId == selected.KeyId && k.Version == selected.Version);
        if (metadata.CustomerId != selected.CustomerId || metadata.KeyType != selected.KeyType)
            throw new InvalidDataException("Schlüsselmetadaten haben sich geändert.");
        var value = current.Secrets.Single(k => k.Key.KeyId == selected.KeyId && k.Key.Version == selected.Version).HexValue;
        var view = new KeyLibrarySecretViewWindow(this, metadata, value);
        view.ShowDialog();
    }

    private void Execute(Action operation)
    {
        try { error.Text = ""; operation(); }
        catch (Exception ex)
        {
            error.Text = ex.Message;
            status.Text = "Vorgang nicht vollständig bestätigt. Bei Speicher-/Prüffehlern den aktuellen Bibliotheksstand erneut öffnen und die Revision kontrollieren; es kann bereits eine Änderung geschrieben worden sein.";
            RefreshActions();
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
    internal KeyLibrarySecretViewWindow(Window owner, KeyLibraryEntry key, string value)
    {
        Owner = owner;
        Title = $"Schlüsselwert · {key.Name} · v{key.Version}";
        Width = 580; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Sichtbarer Geheimwert – nur für berechtigte Verwaltung. Eine Kopie in der Zwischenablage bleibt möglicherweise auch nach dem Schließen erhalten.", TextWrapping = TextWrapping.Wrap });
        var display = new TextBox { IsReadOnly = true, Text = value, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 12, 0, 12) };
        panel.Children.Add(display);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var copy = new Button { Content = "Für Hostkonfiguration kopieren" };
        copy.Click += (_, _) => Clipboard.SetText(value);
        var close = new Button { Content = "Schließen" };
        close.Click += (_, _) => Close();
        actions.Children.Add(copy); actions.Children.Add(close); panel.Children.Add(actions);
        Closed += (_, _) => display.Clear();
        Content = panel;
    }
}

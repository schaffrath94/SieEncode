using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>
/// One Studio editor dialog for exact, key-free assignments. It opens an already
/// authorized local encrypted library on demand, and retains metadata only.
/// No user-entered secret or decrypted library payload is stored in the project.
/// </summary>
internal sealed class KeyLibraryAssignmentWindow : Window
{
    private const string VaultSuffix = ".key-library.vault.json";
    private sealed record VaultChoice(Guid LibraryId, string GroupId, string Path)
    {
        public string Label => $"{LibraryId:D} · Gruppe {GroupId}";
    }
    private sealed record KeyChoice(KeyLibraryEntry Entry)
    {
        public KeyLibraryAddress Address => new(Entry.KeyId, Entry.Version);
        public string Label => $"{Entry.Name} · v{Entry.Version} · {Entry.KeyType} · Kunde {Entry.CustomerId:D} · ID {Entry.KeyId.ToString("N")[..8]}";
    }
    private sealed record SlotPicker(string Slot, string Type, ComboBox Selection);

    private readonly ProjectDefinition project;
    private readonly ProfileLibrary profiles;
    private readonly string trustDirectory;
    private readonly ComboBox vaults = new() { DisplayMemberPath = nameof(VaultChoice.Label) };
    private readonly StackPanel slotsPanel = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    private readonly Button accept = new() { Content = "Schlüsselzuordnung übernehmen", IsEnabled = false };
    private readonly List<SlotPicker> pickers = [];
    private KeyLibrarySnapshot? catalog;

    internal KeyLibrarySnapshot? SelectedCatalog { get; private set; }
    internal Guid SelectedCustomerId { get; private set; }
    internal ProjectKeyLibraryBinding[]? SelectedBindings { get; private set; }

    internal KeyLibraryAssignmentWindow(Window owner, ProjectDefinition draft,
        ProfileLibrary library, string localTrustDirectory)
    {
        Owner = owner;
        project = draft;
        profiles = library;
        trustDirectory = localTrustDirectory;
        Title = "Schlüssel aus zentraler Bibliothek zuordnen";
        Width = 900;
        MaxHeight = 800;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(Note("Projekt-Schlüssel werden über feste Schlüssel-ID und Version zugeordnet. Die verschlüsselte Offline-Bibliothek wird nur nach erfolgreicher Windows-/Gruppenfreigabe geöffnet. In der Projektdatei stehen keine Schlüsselwerte."));
        root.Children.Add(Note("1 · Bereits bereitgestellte, berechtigte Offline-Bibliothek wählen"));
        root.Children.Add(vaults);
        root.Children.Add(Note("2 · Für jeden benötigten Karten-Schlüsselplatz eine ausdrückliche Zuordnung wählen"));
        root.Children.Add(slotsPanel);
        root.Children.Add(status);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Abbrechen", MinWidth = 100 };
        cancel.Click += (_, _) => DialogResult = false;
        accept.Click += (_, _) =>
        {
            try { CommitAssignment(); }
            catch (Exception ex) { ShowError(ex); }
        };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        vaults.SelectionChanged += (_, _) => LoadSelectedVault();
        vaults.ItemsSource = DiscoverLocalVaults();
        if (vaults.Items.Count == 1) vaults.SelectedIndex = 0;
        else status.Text = vaults.Items.Count == 0
            ? "Keine vorbereitete verschlüsselte Schlüsselbibliothek auf diesem Arbeitsplatz. Die Bibliothek muss zunächst von einer berechtigten SieEncode-Verwaltung eingerichtet und bereitgestellt werden."
            : "Bitte die richtige Bibliothek und deren freigegebene Gruppe auswählen.";
    }

    internal static bool HasLocalVaults() => DiscoverLocalVaults().Length > 0;

    private static VaultChoice[] DiscoverLocalVaults()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SieEncode", "KeyLibrary");
        if (!Directory.Exists(root)) return [];
        var choices = new List<VaultChoice>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var libraryId))
                continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*" + VaultSuffix, SearchOption.TopDirectoryOnly))
            {
                var filename = Path.GetFileName(path);
                var groupId = filename[..^VaultSuffix.Length];
                try
                {
                    if (string.Equals(Path.GetFullPath(path), KeyLibraryVaultStore.DefaultPath(libraryId, groupId),
                        StringComparison.OrdinalIgnoreCase))
                        choices.Add(new VaultChoice(libraryId, groupId, path));
                }
                catch (ArgumentException) { /* unrelated files are not library vaults */ }
            }
        }
        return choices.OrderBy(choice => choice.LibraryId).ThenBy(choice => choice.GroupId, StringComparer.Ordinal).ToArray();
    }

    private void LoadSelectedVault()
    {
        catalog = null;
        pickers.Clear();
        slotsPanel.Children.Clear();
        accept.IsEnabled = false;
        if (vaults.SelectedItem is not VaultChoice choice) return;
        try
        {
            var trust = new OfflineGroupTrustStore(
                Path.Combine(trustDirectory, choice.LibraryId.ToString("N") + ".trust.bin"),
                choice.LibraryId, new WindowsGroupTrustProtection(choice.LibraryId));
            var identities = WindowsVaultIdentityService.List();
            try
            {
                // Decrypt only on explicit selection. Do not retain the secret payload
                // in this dialog or pass it back to the editor.
                var unlocked = KeyLibraryVaultStore.Load(choice.Path, trust, choice.GroupId,
                    identities.Select(identity => identity.Certificate));
                KeyLibraryVault.Validate(unlocked);
                catalog = unlocked.Catalog;
            }
            finally
            {
                foreach (var identity in identities) identity.Certificate.Dispose();
            }

            var required = ProjectKeyLibraryResolution.RequiredSlots(project, profiles);
            foreach (var (slot, type) in required)
            {
                var picker = new ComboBox
                {
                    MinWidth = 340,
                    DisplayMemberPath = nameof(KeyChoice.Label),
                    SelectedValuePath = nameof(KeyChoice.Address),
                    ItemsSource = catalog.Keys
                        .Where(key => key.KeyType == type && key.Status == KeyLibraryContracts.Active)
                        .OrderBy(key => key.CustomerId).ThenBy(key => key.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(key => key.KeyId).ThenBy(key => key.Version)
                        .Select(key => new KeyChoice(key)).ToArray()
                };
                var existing = project.Project.KeyLibrary;
                if (existing is not null && existing.LibraryId == catalog.LibraryId)
                {
                    var pinned = existing.Bindings.FirstOrDefault(binding => binding.Slot == slot);
                    if (pinned is not null)
                        picker.SelectedValue = pinned.Key;
                }
                picker.SelectionChanged += (_, _) => RefreshReadyState();
                var row = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
                row.Children.Add(Note(slot + " · " + type));
                row.Children.Add(picker);
                slotsPanel.Children.Add(row);
                pickers.Add(new SlotPicker(slot, type, picker));
            }
            RefreshReadyState();
        }
        catch (Exception ex)
        {
            catalog = null;
            pickers.Clear();
            slotsPanel.Children.Clear();
            ShowError(ex);
        }
    }

    private void RefreshReadyState()
    {
        var missing = pickers.Count(picker => picker.Selection.SelectedItem is not KeyChoice);
        accept.IsEnabled = catalog is not null && pickers.Count > 0 && missing == 0;
        status.Foreground = Brushes.DimGray;
        status.Text = catalog is null ? "Bibliothek nicht freigegeben."
            : missing > 0 ? $"{missing} Schlüsselplatz/-plätze müssen noch ausdrücklich zugeordnet werden."
            : $"Alle {pickers.Count} Plätze sind belegt. Es werden nur exakte Bibliotheks-IDs und Versionen gespeichert; keine Klartextschlüssel.";
    }

    private void CommitAssignment()
    {
        if (catalog is null || pickers.Count == 0 ||
            pickers.Any(item => item.Selection.SelectedItem is not KeyChoice))
            throw new InvalidOperationException("Bitte alle Schlüsselplätze ausdrücklich zuordnen.");
        var choices = pickers.Select(item => (item.Slot, Key: (KeyChoice)item.Selection.SelectedItem)).ToArray();
        var defaultCustomer = choices[0].Key.Entry.CustomerId;
        var bindings = choices.Select(item => new ProjectKeyLibraryBinding(item.Slot, item.Key.Address,
            item.Key.Entry.CustomerId == defaultCustomer ? null : item.Key.Entry.CustomerId)).ToArray();

        // The same strict core check used at build time rejects type, version,
        // status, customer ownership and incomplete or ambiguous slot maps.
        _ = ProjectKeyLibraryResolution.Bind(project, profiles, catalog, defaultCustomer, bindings);
        SelectedCatalog = catalog;
        SelectedCustomerId = defaultCustomer;
        SelectedBindings = bindings;
        DialogResult = true;
    }

    private void ShowError(Exception error)
    {
        accept.IsEnabled = false;
        status.Foreground = Brushes.Firebrick;
        status.Text = "Keine Schlüssel freigegeben: " + error.Message;
    }

    private static TextBlock Note(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5)
    };
}

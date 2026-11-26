using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    private void RefreshCustomerScopeStatus()
    {
        customerScopeColumn.Visibility = customerScopeDraft ? Visibility.Visible : Visibility.Collapsed;
        if (prepareCustomerScopesButton is not null)
        {
            prepareCustomerScopesButton.Visibility = customerScopeDraft ? Visibility.Collapsed : Visibility.Visible;
            prepareCustomerScopesButton.IsEnabled = !peopleGrid.IsReadOnly;
        }
        if (editCustomerScopeButton is not null)
            editCustomerScopeButton.IsEnabled = customerScopeDraft && !peopleGrid.IsReadOnly;
        customerScopeStatus.Text = customerScopeDraft
            ? "Kundenfreigaben im Entwurf · Alle (auch neue), ausgewählte Kunden oder keine. Gruppenmitgliedschaft und Gerätefreigabe bleiben zusätzlich erforderlich."
            : "Bisher gemeinsame Bibliothek ohne kryptografische Kundentrennung. Die Umstellung benötigt einen vollständig abgeglichenen Zentralbestand.";
        peopleGrid.Items.Refresh();
    }

    private (CentralKeyLibraryHead Head, KeyLibrarySnapshot Catalog, OfflineGroupPolicy Policy) ReadScopeBasis()
    {
        RequireStore();
        CommitEdits();
        var id = currentLibrary!.Value;
        using var lease = CentralGroupPolicyWorkflow.Acquire(id);
        CentralGroupPolicyWorkflow.RequireNoPending(id);
        if (CentralGroupPolicyWorkflow.Endpoint(id) is null)
            throw new InvalidOperationException("Zuerst die Zentralverbindung in der Schlüsselbibliothek einrichten und vollständig abgleichen.");
        var trust = OwnTrust(id);
        var current = trust.LoadOffline();
        if (store!.RequireLocalAuthority(trust) != current.Checkpoint || current.Checkpoint != checkpoint ||
            current.Policy.Version != 2)
            throw new CryptographicException("Aktueller personenbasierter Verwaltungsstand erforderlich.");
        var cursor = new CentralKeyLibraryCursorStore(CentralKeyLibraryCursorStore.DefaultPath(id), id,
            new WindowsGroupTrustProtection(id, syncState: true));
        var head = cursor.ReadReconciledHead() ??
            throw new InvalidOperationException("Bitte Schlüsselbibliothek zuerst zentral abgleichen.");
        var path = KeyLibraryVaultStore.DefaultPath(id, head.GroupId);
        var file = ConcurrentFileWriter.Capture(path);
        if (!file.Exists || file.Sha256 != head.CiphertextSha256 || head.Policy != current.Checkpoint ||
            !cursor.HasUploadBase(head) || !cursor.HasUnchangedLocalBase(file))
            throw new InvalidOperationException("Lokaler Bestand ist nicht exakt zentral abgeglichen.");
        var identities = WindowsVaultIdentityService.List();
        try
        {
            var catalog = KeyLibraryVaultStore.Load(path, trust, head.GroupId,
                identities.Select(i => i.Certificate), requireFull: true).Catalog;
            if (catalog.Revision != head.CatalogRevision || ConcurrentFileWriter.Capture(path) != file ||
                trust.LoadOffline().Checkpoint != current.Checkpoint)
                throw new ConcurrentFileChangeException(path);
            return (head, catalog, current.Policy);
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    private void PrepareCustomerScopes()
    {
        RequireStore();
        CommitEdits();
        if (customerScopeDraft) return;
        if (dirty) throw new InvalidOperationException("Personen-/Gruppenänderungen vor der erstmaligen Umstellung speichern oder verwerfen.");
        var basis = ReadScopeBasis();
        var group = basis.Policy.Groups.Single(g => g.Id == basis.Head.GroupId);
        var previouslyAuthorized = basis.Policy.People.Where(p => p.Enabled &&
            group.Members.Contains(p.Id, StringComparer.Ordinal) && p.Devices.Any(d => d.Enabled))
            .Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (MessageBox.Show(this,
            $"Kryptografische Kundentrennung für {basis.Catalog.Customers.Length} Kunden vorbereiten?\n\n" +
            $"Als Entwurf erhalten die {previouslyAuthorized.Count} bisher aktiven Personen mit Gerätefreigabe in der Bibliotheksgruppe zunächst weiterhin alle Kunden, auch zukünftige. Andere Personen erhalten keine Freigabe. Prüfen und ändern Sie diese Zuordnung vor dem Speichern.\n\n" +
            "Erst ‚Änderungen signiert speichern‘ verschlüsselt den Gesamtbestand zentral neu. Ein Rückwechsel zur gemeinsamen Entschlüsselung ist nicht vorgesehen. Alte Kopien und bekannte Kartenschlüssel bleiben außerhalb dieses Schutzes.",
            Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        foreach (var person in people)
        {
            person.AllKeyLibraryCustomers = previouslyAuthorized.Contains(person.Id);
            person.KeyLibraryCustomers = [];
        }
        customerScopeDraft = true;
        dirty = true;
        RefreshCustomerScopeStatus();
    }

    private void EditSelectedCustomerScope()
    {
        RequireStore();
        CommitEdits();
        if (!customerScopeDraft)
            throw new InvalidOperationException("Zuerst Kundentrennung vorbereiten.");
        if (peopleGrid.SelectedItem is not PersonAdministrationDraft person)
            throw new InvalidOperationException("Bitte zuerst eine Person auswählen.");
        var basis = ReadScopeBasis();
        var dialog = new CustomerScopeSelectionWindow(this, person, basis.Catalog.Customers);
        if (dialog.ShowDialog() != true) return;
        person.AllKeyLibraryCustomers = dialog.AllCustomers;
        person.KeyLibraryCustomers = dialog.SelectedCustomers;
        dirty = true;
        RefreshCustomerScopeStatus();
    }
}

internal sealed class CustomerScopeSelectionWindow : Window
{
    private sealed record CustomerChoice(Guid Id, string Label);
    private readonly RadioButton all = new() { Content = "Alle Kunden, einschließlich zukünftiger Kunden", GroupName = "scope" };
    private readonly RadioButton selected = new() { Content = "Nur ausgewählte Kunden", GroupName = "scope" };
    private readonly RadioButton none = new() { Content = "Keine Kundenfreigabe", GroupName = "scope" };
    private readonly ListBox customers = new() { SelectionMode = SelectionMode.Multiple, DisplayMemberPath = nameof(CustomerChoice.Label), MinHeight = 140 };
    private readonly TextBlock count = new() { TextWrapping = TextWrapping.Wrap };
    internal bool AllCustomers { get; private set; }
    internal Guid[] SelectedCustomers { get; private set; } = [];

    internal CustomerScopeSelectionWindow(Window owner, PersonAdministrationDraft person,
        IEnumerable<KeyLibraryCustomer> catalog)
    {
        Owner = owner; Title = "Kundenfreigabe · " + person.Name;
        Width = 610; Height = 510; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var choices = catalog.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.CustomerId).Select(c => new CustomerChoice(c.CustomerId,
                c.Name + " · " + c.CustomerId.ToString("D"))).ToArray();
        if (!person.KeyLibraryCustomers.All(id => choices.Any(c => c.Id == id)))
            throw new InvalidDataException("Vorhandene Kundenfreigabe fehlt im vollständigen Katalog. Keine automatische Änderung.");
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = person.Name, FontSize = 20, FontWeight = FontWeights.SemiBold });
        top.Children.Add(new TextBlock { Text = "Die Auswahl gilt für alle aktiven Geräte dieser Person. Keine zusätzliche Gruppen- oder Klartextfreigabe.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        top.Children.Add(all); top.Children.Add(selected); top.Children.Add(none);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = new StackPanel(); bottom.Children.Add(count);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var accept = new Button { Content = "In Entwurf übernehmen", IsDefault = true, Margin = new Thickness(5) };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true, Margin = new Thickness(5) };
        buttons.Children.Add(accept); buttons.Children.Add(cancel); bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        customers.ItemsSource = choices; root.Children.Add(customers); Content = root;
        foreach (var choice in choices.Where(c => person.KeyLibraryCustomers.Contains(c.Id)))
            customers.SelectedItems.Add(choice);
        all.IsChecked = person.AllKeyLibraryCustomers;
        selected.IsChecked = !person.AllKeyLibraryCustomers && person.KeyLibraryCustomers.Length > 0;
        none.IsChecked = !person.AllKeyLibraryCustomers && person.KeyLibraryCustomers.Length == 0;
        void Refresh()
        {
            customers.IsEnabled = selected.IsChecked == true;
            count.Text = all.IsChecked == true ? "Alle bestehenden und zukünftigen Kunden." :
                none.IsChecked == true ? "Kein entschlüsselbarer Kundenbereich." :
                $"{customers.SelectedItems.Count} Kunden ausgewählt.";
            accept.IsEnabled = selected.IsChecked != true || customers.SelectedItems.Count > 0;
        }
        all.Checked += (_, _) => Refresh(); selected.Checked += (_, _) => Refresh();
        none.Checked += (_, _) => Refresh(); customers.SelectionChanged += (_, _) => Refresh();
        accept.Click += (_, _) =>
        {
            AllCustomers = all.IsChecked == true;
            SelectedCustomers = selected.IsChecked == true
                ? customers.SelectedItems.Cast<CustomerChoice>().Select(c => c.Id).OrderBy(id => id).ToArray() : [];
            DialogResult = true;
        };
        Refresh();
    }
}

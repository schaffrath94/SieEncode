using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class VaultRecipientChoice : IDisposable
{
    public VaultRecipientChoice(X509Certificate2 certificate, string displayName)
    { Certificate = certificate; DisplayName = displayName; Fingerprint = CertificateVaultService.Fingerprint(certificate); }
    public X509Certificate2 Certificate { get; }
    public string DisplayName { get; }
    public string Fingerprint { get; }
    public string Label => $"{DisplayName} · {Fingerprint[..12]}";
    public void Dispose() => Certificate.Dispose();
}

internal sealed class VaultAccessWindow : Window
{
    private readonly Func<IReadOnlyList<WindowsVaultIdentity>> loadIdentities;
    private readonly ObservableCollection<VaultRecipientChoice> recipients = [];
    private readonly ObservableCollection<WindowsVaultIdentity> identities = [];
    private readonly ListBox recipientList = new() { MinHeight = 150, DisplayMemberPath = nameof(VaultRecipientChoice.Label) };
    private readonly ComboBox identityList = new() { MinWidth = 360, DisplayMemberPath = nameof(WindowsVaultIdentity.DisplayName) };
    private readonly PasswordBox recovery = new() { MinWidth = 360 };
    private readonly PasswordBox confirmation = new() { MinWidth = 360 };
    private IReadOnlyList<(X509Certificate2 Certificate, string DisplayName)> acceptedRecipients = [];
    private readonly TextBlock accessGuidance = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private Button? createIdentityButton;
    private Button? addIdentityButton;
    private Button? exportIdentityButton;

    public VaultAccessWindow(Window owner, KeyVaultDocument? existing = null, IEnumerable<VaultRecipientInvitation>? teamRecipients = null, Func<IReadOnlyList<WindowsVaultIdentity>>? identityProvider = null)
    {
        loadIdentities = identityProvider ?? WindowsVaultIdentityService.List;
        Owner = owner; Title = "Vault-Zugriff für mehrere Benutzer"; Width = 680; MaxHeight = 760; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResizeWithGrip;
        recipientList.ItemsSource = recipients; identityList.ItemsSource = identities;
        identityList.SelectionChanged += (_, _) => UpdateIdentityGuidance();
        recipients.CollectionChanged += (_, _) => UpdateIdentityGuidance();
        ReloadIdentities();
        if (existing is { Version: KeyVaultService.CertificateVersion })
            foreach (var row in existing.Recipients.Where(row => !string.IsNullOrWhiteSpace(row.Certificate)))
                AddRecipient(new X509Certificate2(Convert.FromBase64String(row.Certificate)), row.DisplayName);
        // Team defaults seed a new certificate vault; they must not re-add removed recipients.
        if (teamRecipients is not null && existing is not { Version: KeyVaultService.CertificateVersion })
            foreach (var invitation in teamRecipients)
            {
                var imported = CertificateVaultService.ImportInvitation(invitation);
                AddRecipient(imported.Certificate, imported.DisplayName);
            }

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = "Mehrbenutzerzugriff", FontSize = 22, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = existing is { Version: KeyVaultService.CertificateVersion }
            ? "Die Empfänger dieses Vaults sind übernommen. Weitere Benutzer können Sie ausdrücklich hinzufügen oder importieren. Änderungen der zentralen Teamliste ändern diesen Vault nicht automatisch."
            : "Persönliches 1.0-Projekt: hier ausdrücklich mindestens einen berechtigten Empfänger auswählen und eine getrennte Recovery-Passphrase festlegen. Der eigene Windows-Zugang ist nicht automatisch eine bereits gespeicherte Projektfreigabe. Es werden keine früheren Teamlisten übernommen.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 16), Foreground = System.Windows.Media.Brushes.DimGray });
        root.Children.Add(new Border
        {
            Background = System.Windows.Media.Brushes.AliceBlue,
            BorderBrush = System.Windows.Media.Brushes.LightSteelBlue,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 9),
            Child = accessGuidance
        });
        root.Children.Add(new TextBlock { Text = "Mein persönlicher Zugang", FontWeight = FontWeights.SemiBold });
        root.Children.Add(identityList);
        root.Children.Add(new TextBlock { Text = "Vorhandene Identität auswählen und bei Bedarf zur Empfängerliste hinzufügen. Für Updates oder weitere Projekte keinen neuen Zugang anlegen. Diese Freigabe gilt nur für den aktuellen Vault.", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 5, 0, 5) });
        var identityButtons = new WrapPanel();
        addIdentityButton = Button("Zur Empfängerliste hinzufügen", (_, _) => AddSelectedIdentity());
        exportIdentityButton = Button("Eigene Einladung exportieren", (_, _) => ExportIdentity());
        identityButtons.Children.Add(addIdentityButton);
        identityButtons.Children.Add(exportIdentityButton);
        root.Children.Add(identityButtons);
        createIdentityButton = Button("Neue Identität", (_, _) => CreateIdentity());
        root.Children.Add(new Expander
        {
            Header = "Nur falls kein persönlicher Zugang vorhanden ist · neue Identität anlegen",
            IsExpanded = identities.Count == 0,
            Margin = new Thickness(0, 5, 0, 0),
            Content = createIdentityButton
        });
        root.Children.Add(new TextBlock { Text = "Freigegebene Empfänger", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4) });
        root.Children.Add(recipientList);
        var recipientButtons = new WrapPanel();
        recipientButtons.Children.Add(Button("Einladung importieren", (_, _) => ImportInvitation()));
        recipientButtons.Children.Add(Button("Ausgewählten Empfänger entfernen", (_, _) => RemoveRecipient()));
        root.Children.Add(recipientButtons);
        root.Children.Add(new TextBlock { Text = "Recovery-Passphrase aus KeePass", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4) });
        root.Children.Add(recovery);
        root.Children.Add(new TextBlock { Text = "Recovery-Passphrase wiederholen", Margin = new Thickness(0, 8, 0, 4) });
        root.Children.Add(confirmation);
        root.Children.Add(new TextBlock { Text = $"Mindestens {KeyVaultService.MinimumNewPassphraseLength} Zeichen. Dieser Zugang ist nur für Wiederherstellung und Änderungen an der Empfängerliste gedacht.", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 6, 0, 12) });
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        footer.Children.Add(Button("Abbrechen", (_, _) => { DialogResult = false; }));
        var save = Button("Vault sicher freigeben", (_, _) => Accept()); save.Style = (Style)FindResource("PrimaryButton"); save.IsDefault = true; footer.Children.Add(save);
        root.Children.Add(footer);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        MainWindow.InitializeAutomationContract(this, "VaultAccess");
        UpdateIdentityGuidance();
    }

    public string RecoveryPassphrase => recovery.Password;
    public IReadOnlyList<(X509Certificate2 Certificate, string DisplayName)> TakeRecipients()
        => acceptedRecipients;

    private static Button Button(string text, RoutedEventHandler click) { var button = new Button { Content = text, IsCancel = text == "Abbrechen" }; button.Click += click; return button; }
    internal void ReloadIdentities()
    {
        // Keep an explicit choice by fingerprint. Multiple certificates (even when
        // identically named) must never silently use the first private identity.
        var previousFingerprint = (identityList.SelectedItem as WindowsVaultIdentity)?.Fingerprint;
        identityList.SelectedItem = null;
        identityList.ItemsSource = null;
        var previous = identities.ToArray();
        identities.Clear();
        foreach (var row in previous) row.Certificate.Dispose();
        foreach (var row in loadIdentities()) identities.Add(row);
        identityList.ItemsSource = identities;
        var previousChoice = previousFingerprint is null ? null : identities.FirstOrDefault(row =>
            string.Equals(row.Fingerprint, previousFingerprint, StringComparison.OrdinalIgnoreCase));
        identityList.SelectedItem = previousChoice ?? (identities.Count == 1 ? identities[0] : null);
        UpdateIdentityGuidance();
    }
    private void UpdateIdentityGuidance()
    {
        var chosen = identityList.SelectedItem as WindowsVaultIdentity;
        accessGuidance.Text = identities.Count == 0
            ? "Kein persönlicher Zugang vorhanden. Einmalig einen neuen anlegen; bestehende Empfänger bleiben unverändert."
            : chosen is null
                ? "Mehrere persönliche Zugänge gefunden. Den richtigen anhand des Fingerabdrucks ausdrücklich auswählen. Keinen neuen Schlüssel nur für diesen Vault erzeugen."
                : $"Ausgewählt: {chosen.DisplayName} · {chosen.Fingerprint[..12]}…\n{recipients.Count} Empfänger in diesem Vault-Entwurf. Nur mit „Vault sicher freigeben“ werden Änderungen nach Prüfung und Bestätigung der Recovery-Passphrase übernommen.";
        if (createIdentityButton is not null) createIdentityButton.IsEnabled = identities.Count == 0;
        if (addIdentityButton is not null) addIdentityButton.IsEnabled = chosen is not null;
        if (exportIdentityButton is not null) exportIdentityButton.IsEnabled = chosen is not null;
    }

    private void CreateIdentity()
    {
        var name = TextEntryPrompt.Show(this, "Neue SieEncode-Identität", "Anzeigename, zum Beispiel Vorname Nachname oder Kodierstation 01", Environment.UserName);
        if (name is null) return;
        if (identities.Count > 0)
            throw new InvalidOperationException("Ein vorhandener persönlicher Zugang sollte weiterverwendet werden. Keine neue Identität nur für diesen Projekt-Vault erzeugen.");
        using var created = WindowsVaultIdentityService.Create(name).Certificate;
        var createdFingerprint = CertificateVaultService.Fingerprint(created);
        ReloadIdentities();
        identityList.SelectedItem = identities.SingleOrDefault(row =>
            string.Equals(row.Fingerprint, createdFingerprint, StringComparison.OrdinalIgnoreCase));
        AddSelectedIdentity();
    }
    private void AddSelectedIdentity()
    {
        if (identityList.SelectedItem is not WindowsVaultIdentity identity) { MessageBox.Show(this, "Bitte zuerst eine eigene Identität anlegen oder auswählen.", Title); return; }
        AddRecipient(new X509Certificate2(identity.Certificate), identity.DisplayName);
    }
    private void ExportIdentity()
    {
        if (identityList.SelectedItem is not WindowsVaultIdentity identity) { MessageBox.Show(this, "Bitte zuerst eine Identität auswählen.", Title); return; }
        var dialog = new SaveFileDialog { Title = "Öffentliche SieEncode-Einladung exportieren", Filter = "SieEncode-Empfängereinladung (*.recipient.json)|*.recipient.json", FileName = MakeSafe(identity.DisplayName) + ".recipient.json", AddExtension = true };
        if (dialog.ShowDialog(this) == true)
        {
            WindowsVaultIdentityService.ExportInvitation(identity, dialog.FileName);
            MessageBox.Show(this,
                $"Die Datei enthält ausschließlich den öffentlichen Schlüssel.\n\nSHA-256-Fingerabdruck:\n{FormatFingerprint(identity.Fingerprint)}\n\nDen Fingerabdruck über einen zweiten, unabhängigen Kanal an die freigebende Person übermitteln.",
                "Öffentliche Einladung exportiert", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    private void ImportInvitation()
    {
        var dialog = new OpenFileDialog { Title = "Öffentliche SieEncode-Einladung importieren", Filter = "SieEncode-Empfängereinladung (*.recipient.json)|*.recipient.json", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var path in dialog.FileNames)
        {
            var row = WindowsVaultIdentityService.ImportInvitation(path);
            var fingerprint = CertificateVaultService.Fingerprint(row.Certificate);
            if (MessageBox.Show(this,
                $"Empfänger: {row.DisplayName}\n\nSHA-256-Fingerabdruck:\n{FormatFingerprint(fingerprint)}\n\nHaben Sie diesen Fingerabdruck über einen unabhängigen Kanal mit der vorgesehenen Person abgeglichen?",
                "Empfängeridentität prüfen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                AddRecipient(row.Certificate, row.DisplayName);
            else row.Certificate.Dispose();
        }
    }
    private void AddRecipient(X509Certificate2 certificate, string displayName)
    {
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        if (recipients.Any(row => row.Fingerprint == fingerprint)) { certificate.Dispose(); return; }
        recipients.Add(new VaultRecipientChoice(certificate, displayName));
        UpdateIdentityGuidance();
    }
    private void RemoveRecipient() { if (recipientList.SelectedItem is VaultRecipientChoice row) { recipients.Remove(row); row.Dispose(); UpdateIdentityGuidance(); } }
    private void Accept()
    {
        if (recipients.Count == 0) { MessageBox.Show(this, "Mindestens ein Empfänger ist erforderlich.", Title); return; }
        if (recovery.Password != confirmation.Password) { MessageBox.Show(this, "Die Recovery-Passphrasen stimmen nicht überein.", Title); return; }
        try { KeyVaultService.ValidateNewPassphrase(recovery.Password); }
        catch (ArgumentException exception) { MessageBox.Show(this, exception.Message, Title); return; }
        acceptedRecipients = recipients.Select(row => (new X509Certificate2(row.Certificate), row.DisplayName)).ToArray();
        DialogResult = true;
    }
    protected override void OnClosed(EventArgs e) { foreach (var row in identities) row.Certificate.Dispose(); foreach (var row in recipients) row.Dispose(); base.OnClosed(e); }
    private static string MakeSafe(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    private static string FormatFingerprint(string value) => string.Join(" ", Enumerable.Range(0, value.Length / 4).Select(index => value.Substring(index * 4, 4)));
}

internal static class TextEntryPrompt
{
    public static string? Show(Window owner, string title, string prompt, string initial)
    {
        var (dialog, input) = Create(owner, title, prompt, initial);
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    internal static (Window Dialog, TextBox Input) Create(Window owner, string title, string prompt, string initial)
    {
        var input = new TextBox { Text = initial, MinWidth = 380 };
        var ok = new Button { Content = "OK", IsDefault = true }; var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap }); panel.Children.Add(input); panel.Children.Add(error); panel.Children.Add(buttons);
        var dialog = new Window { Owner = owner, Title = title, Width = 480, MaxHeight = 600, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.CanResizeWithGrip };
        ok.Click += (_, _) =>
        {
            error.Text = "";
            if (string.IsNullOrWhiteSpace(input.Text)) { error.Text = "Bitte einen Namen eingeben."; input.Focus(); return; }
            dialog.DialogResult = true;
        };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        MainWindow.InitializeAutomationContract(dialog, "TextEntryPrompt");
        return (dialog, input);
    }
}

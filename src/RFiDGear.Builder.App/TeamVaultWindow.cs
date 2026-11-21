using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class TeamVaultWindow : Window
{
    private readonly TeamVaultRecipientStore store;
    private readonly bool identityOnly;
    private readonly TextBlock identityExportStatus = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateGray };
    private readonly Func<IReadOnlyList<WindowsVaultIdentity>> loadIdentities;
    private readonly ObservableCollection<VaultRecipientChoice> recipients = [];
    private readonly ObservableCollection<WindowsVaultIdentity> identities = [];
    private readonly ListBox recipientList = new() { MinHeight = 135, DisplayMemberPath = nameof(VaultRecipientChoice.Label) };
    private readonly ComboBox identityList = new() { MinWidth = 390, DisplayMemberPath = nameof(WindowsVaultIdentity.DisplayName) };
    private readonly TextBlock setupStatus = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock personalNextStep = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private Button? createIdentityAction;
    private Button? exportIdentityAction;
    private string? exportedIdentityFingerprint;
    private readonly TextBlock actionError = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
    private readonly TextBlock identityFingerprint = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontFamily = new FontFamily("Consolas"), FontSize = 11 };

    public TeamVaultWindow(Window owner, string projectsDirectory, bool initialSetup = false, Func<IReadOnlyList<WindowsVaultIdentity>>? identityProvider = null)
    {
        Owner = owner;
        identityOnly = initialSetup;
        loadIdentities = identityProvider ?? WindowsVaultIdentityService.List;
        store = new TeamVaultRecipientStore(projectsDirectory);
        Title = initialSetup ? "Meinen persönlichen Vault-Zugang einrichten" : "Bisherige Team-Vaults verwalten";
        Width = 780; MaxHeight = 760; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResizeWithGrip;
        recipientList.ItemsSource = recipients; identityList.ItemsSource = identities;
        identityList.SelectionChanged += (_, _) => UpdateStatus();
        recipientList.SelectionChanged += (_, _) => UpdateStatus();
        ReloadIdentities();
        if (!identityOnly)
        {
            foreach (var invitation in store.Load().Recipients)
            {
                var imported = CertificateVaultService.ImportInvitation(invitation);
                AddRecipient(imported.Certificate, imported.DisplayName);
            }
        }

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = initialSetup ? "Mein persönlicher Zugang" : "Bisherige Team-Vaults", FontSize = 23, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(26, 91, 98)) });
        root.Children.Add(new TextBlock { Text = initialSetup
            ? "Ein vorhandener Zugang kann für weitere Projekte und Tests wiederverwendet werden. Nur wenn noch keiner vorhanden ist, einmalig einen neuen anlegen. Für Teamprojekte bei Bedarf eine öffentliche Einladung weitergeben – die Gruppenrechte vergibt anschließend die Hauptverwaltung."
            : "Nur für bisherige Team-Vaults: lokale Empfängerliste für neue Vaults verwalten. Offline-Gruppen und deren Vollmachten werden separat verwaltet. Bestehende Vaults bleiben unverändert.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 14) });
        if (!initialSetup) root.Children.Add(new Expander
        {
            Header = "Erweitert: bisherige Team-Vaults und Empfängerliste",
            Margin = new Thickness(0, 0, 0, 12),
            Content = new TextBlock
            {
                Text = "NUR ICH: In Schritt 1 eine vorhandene Identität auswählen und zum Team hinzufügen. Nur wenn noch keine existiert, eine neue anlegen. Danach Schritt 4 speichern. Export und Import können Sie überspringen.\n\n" +
                    "MIT KOLLEGEN: Jeder Kollege wählt auf seinem Windows-Benutzer eine eigene Identität oder legt dort einmalig eine an. In Schritt 2 exportiert er seine öffentliche Einladung und gibt Ihnen die Datei. Sie importieren die Einladungen in Schritt 3 und speichern die Liste. Alle verwenden dieselbe gemeinsame Projektbibliothek. Private Schlüssel werden nicht weitergegeben.\n\n" +
                    "BESTEHENDES PROJEKT: Projekt bearbeiten, Vault mit einem bereits berechtigten Zugang oder der Recovery-Passphrase entsperren. Im Projekt den Mehrbenutzerzugriff öffnen, Empfänger hinzufügen und den Vault speichern. Die Teamliste hier allein erteilt keinen Zugriff auf bestehende Vaults.\n\n" +
                    "ZWEI IDENTITÄTEN: Das sind zwei separat angelegte Zugänge. Eine vorhandene Identität weiterverwenden; für Updates und neue Projekte ist keine neue nötig. Der Fingerabdruck unterscheidet auch gleichnamige Einträge. Welche Identität einen alten Vault öffnet, bestimmt dessen Empfängerliste.\n\n" +
                    "ENTFERNEN: Schritt 3 entfernt nur einen Eintrag aus der Teamliste. Das lokale Zertifikat bleibt erhalten. Zertifikate lassen sich in Windows unter Benutzerzertifikate verwalten → Eigene Zertifikate → Zertifikate löschen. Vorher auch ältere Vaults und Sicherungen auf benötigte Zugänge prüfen; das Löschen kann den Zugang verlieren lassen.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8), Foreground = Brushes.DimGray
            }
        });

        if (initialSetup)
            root.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 246, 247)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(183, 204, 207)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10),
                Child = personalNextStep
            });

        var identityContent = new StackPanel();
        identityContent.Children.Add(new TextBlock { Text = "Mein Zugang auf diesem Windows-Benutzer", FontWeight = FontWeights.SemiBold });
        identityContent.Children.Add(identityList); identityContent.Children.Add(identityFingerprint);
        var own = new WrapPanel();
        createIdentityAction = ActionButton("Neue persönliche Identität anlegen", CreateIdentity, identities.Count == 0);
        own.Children.Add(createIdentityAction);
        if (!initialSetup) own.Children.Add(ActionButton("Eigene Identität zum Team hinzufügen", AddSelectedIdentity));
        identityContent.Children.Add(own);
        root.Children.Add(Step("1", "Meinen vorhandenen Zugang auswählen", initialSetup
            ? "Bei genau einem Zugang wird dieser vorausgewählt. Bei mehreren Zugängen bewusst auswählen und den Fingerabdruck abgleichen. Nicht für jeden Test einen neuen Schlüssel erzeugen."
            : "Vorhandenen persönlichen Zugang für bisherige Team-Vaults in die Teamliste aufnehmen. Gruppenmitgliedschaften vergibt separat die Hauptverwaltung.", identityContent));

        var exportContent = new StackPanel();
        exportContent.Children.Add(new TextBlock { Text = "Exportieren Sie die öffentliche Einladung und geben Sie sie an den Hauptverwalter Ihrer Gruppen bzw. den Verantwortlichen der bisherigen Teamliste. Fingerabdruck unabhängig abgleichen. Die Datei enthält keinen privaten Schlüssel.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray });
        exportIdentityAction = ActionButton("Öffentliche Einladung exportieren", ExportIdentity);
        exportContent.Children.Add(exportIdentityAction);
        exportContent.Children.Add(identityExportStatus);
        root.Children.Add(Step("2", "Nur bei Teamzugriff: Einladung weitergeben",
            "Nur wenn die Hauptverwaltung Ihren Zugang noch nicht kennt. Das Exportieren vergibt keine Rechte und überträgt keinen privaten Schlüssel.",
            exportContent));

        var importContent = new StackPanel(); importContent.Children.Add(recipientList);
        var team = new WrapPanel();
        team.Children.Add(ActionButton("Einladungen mehrerer Nutzer importieren", ImportInvitation));
        team.Children.Add(ActionButton("Ausgewähltes Mitglied entfernen", RemoveRecipient));
        importContent.Children.Add(team);
        if (!initialSetup) root.Children.Add(Step("3", "Verantwortlicher: Einladungen einsammeln", "Nur für bisherige Team-Vaults. Die Liste ist kein Nachweis für Zugriff auf ein bestehendes Projekt.", importContent));

        var statusBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(240, 246, 247)), BorderBrush = new SolidColorBrush(Color.FromRgb(183, 204, 207)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Child = setupStatus };
        var saveContent = new StackPanel(); saveContent.Children.Add(statusBorder);
        saveContent.Children.Add(new TextBlock { Text = "Speichern ändert nur die Teamliste. Bestehende Vaults separat im Projekt freigeben. Entfernte Empfänger können alte Vaultkopien weiterhin öffnen – auch nach einer Änderung der aktuellen Empfängerliste.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, 7, 0, 0) });
        if (!initialSetup) root.Children.Add(Step("4", "Gemeinsame Liste speichern", $"Speicherort: {store.Path}", saveContent));

        root.Children.Add(actionError);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        if (initialSetup) footer.Children.Add(ActionButton("Schließen", (_, _) => DialogResult = false));
        else
        {
            footer.Children.Add(ActionButton("Abbrechen", (_, _) => DialogResult = false));
            footer.Children.Add(ActionButton("Einrichtung speichern", Save, true));
        }
        root.Children.Add(footer);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
        MainWindow.InitializeAutomationContract(this, "TeamVault");
        UpdateStatus();
    }

    private static Border Step(string number, string title, string description, UIElement content)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = new SolidColorBrush(Color.FromRgb(26, 91, 98)), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = number, Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 7) }); body.Children.Add(content);
        Grid.SetColumn(body, 1); grid.Children.Add(body);
        return new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(207, 218, 220)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 9), Child = grid };
    }

    private Button ActionButton(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button { Content = text, IsCancel = text == "Abbrechen" };
        button.Click += (sender, e) =>
        {
            actionError.Text = "";
            try { click(sender, e); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException)
            {
                actionError.Text = exception is ConcurrentFileChangeException
                    ? "Die Teamliste wurde zwischenzeitlich geändert und nicht überschrieben. Bitte diesen Dialog schließen, erneut öffnen und die aktuelle Liste prüfen."
                    : $"Aktion nicht abgeschlossen: {exception.Message}";
                actionError.BringIntoView();
            }
        };
        if (primary && Application.Current.TryFindResource("PrimaryButton") is Style style) button.Style = style;
        return button;
    }

    internal void ReloadIdentities()
    {
        // Keep a previously selected key across reload. Never silently pick the first
        // of several local certificates: identical names are not interchangeable keys.
        var fingerprint = (identityList.SelectedItem as WindowsVaultIdentity)?.Fingerprint;
        identityList.SelectedItem = null;
        identityList.ItemsSource = null;
        var previous = identities.ToArray();
        identities.Clear();
        foreach (var row in previous) row.Certificate.Dispose();
        foreach (var row in loadIdentities()) identities.Add(row);
        identityList.ItemsSource = identities;
        var existingChoice = fingerprint is null ? null : identities.FirstOrDefault(row =>
            string.Equals(row.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
        identityList.SelectedItem = existingChoice ?? (identities.Count == 1 ? identities[0] : null);
        UpdateStatus();
    }

    private void CreateIdentity(object sender, RoutedEventArgs e)
    {
        var name = TextEntryPrompt.Show(this, "Neue SieEncode-Identität", "Eindeutiger Anzeigename, zum Beispiel Vorname Nachname oder Kodierstation 01", Environment.UserName);
        if (name is null) return;
        if (identityOnly && identities.Count > 0)
            throw new InvalidOperationException("Ein persönlicher Zugang ist bereits vorhanden. Diesen weiterverwenden; bei mehreren Zugängen den richtigen bewusst auswählen.");
        using var created = WindowsVaultIdentityService.Create(name).Certificate;
        ReloadIdentities(); identityList.SelectedItem = identities.FirstOrDefault(row => row.DisplayName == name);
        if (!identityOnly) AddSelectedIdentity(sender, e);
    }

    private void AddSelectedIdentity(object sender, RoutedEventArgs e)
    {
        if (identityList.SelectedItem is not WindowsVaultIdentity identity) { MessageBox.Show(this, "Bitte zuerst eine eigene Identität anlegen oder auswählen.", Title); return; }
        AddRecipient(new X509Certificate2(identity.Certificate), identity.DisplayName);
    }

    private void ExportIdentity(object sender, RoutedEventArgs e)
    {
        if (identityList.SelectedItem is not WindowsVaultIdentity identity) { MessageBox.Show(this, "Bitte zuerst eine eigene Identität anlegen oder auswählen.", Title); return; }
        var dialog = new SaveFileDialog { Title = "Öffentliche SieEncode-Einladung exportieren", Filter = "SieEncode-Empfängereinladung (*.recipient.json)|*.recipient.json", FileName = SafeFileName(identity.DisplayName) + ".recipient.json", AddExtension = true };
        if (dialog.ShowDialog(this) == true)
        {
            WindowsVaultIdentityService.ExportInvitation(identity, dialog.FileName);
            exportedIdentityFingerprint = identity.Fingerprint;
            identityExportStatus.Text = "Öffentliche Einladung gespeichert. Datei an die Hauptverwaltung übergeben und Fingerabdruck unabhängig, etwa telefonisch, bestätigen. Kein privater Schlüssel übertragen und noch keine Gruppenrechte erteilt.";
            UpdateStatus();
        }
    }

    private void ImportInvitation(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Öffentliche SieEncode-Einladungen importieren", Filter = "SieEncode-Empfängereinladung (*.recipient.json)|*.recipient.json", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var path in dialog.FileNames) { var imported = WindowsVaultIdentityService.ImportInvitation(path); AddRecipient(imported.Certificate, imported.DisplayName); }
    }

    private void AddRecipient(X509Certificate2 certificate, string name)
    {
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        if (recipients.Any(row => string.Equals(row.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))) { certificate.Dispose(); return; }
        recipients.Add(new VaultRecipientChoice(certificate, name)); UpdateStatus();
    }

    private void RemoveRecipient(object sender, RoutedEventArgs e)
    {
        if (recipientList.SelectedItem is not VaultRecipientChoice row) return;
        recipients.Remove(row); row.Dispose(); UpdateStatus();
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (identities.Count == 0) { MessageBox.Show(this, "Auf diesem Arbeitsplatz fehlt noch eine eigene Windows-Identität.", Title); return; }
        if (!recipients.Any(row => identities.Any(identity => string.Equals(identity.Fingerprint, row.Fingerprint, StringComparison.OrdinalIgnoreCase))))
        { MessageBox.Show(this, "Die eigene Windows-Identität muss in der Teamliste enthalten sein.", Title); return; }
        store.Save(recipients.Select(row => CertificateVaultService.ExportInvitation(row.Certificate, row.DisplayName))); DialogResult = true;
    }

    private void UpdateStatus()
    {
        var selected = identityList.SelectedItem as WindowsVaultIdentity;
        var selectedInTeam = selected is not null && recipients.Any(row =>
            string.Equals(row.Fingerprint, selected.Fingerprint, StringComparison.OrdinalIgnoreCase));
        if (identityOnly && exportedIdentityFingerprint is not null &&
            !string.Equals(selected?.Fingerprint, exportedIdentityFingerprint, StringComparison.OrdinalIgnoreCase))
            identityExportStatus.Text = "";

        identityFingerprint.Text = selected is null
            ? identities.Count > 1
                ? "Mehrere persönliche Zugänge vorhanden. Bitte bewusst auswählen; keiner wurde automatisch übernommen."
                : "Noch keine lokale Identität vorhanden."
            : identityOnly
                ? $"Ausgewählt: {selected.DisplayName}\nFingerabdruck zum unabhängigen Abgleich: {selected.Fingerprint}"
                : $"Fingerabdruck: {selected.Fingerprint}\n" + (selectedInTeam
                    ? "Ausgewählter Zugang ist in dieser bisherigen Teamliste enthalten (Änderungen noch speichern)."
                    : "Ausgewählter Zugang fehlt in dieser bisherigen Teamliste. Bei Bedarf unten hinzufügen.");

        if (createIdentityAction is not null)
            createIdentityAction.IsEnabled = identities.Count == 0;
        if (exportIdentityAction is not null)
            exportIdentityAction.IsEnabled = selected is not null;

        if (identityOnly)
            personalNextStep.Text = identities.Count == 0
                ? "Schritt 1 · Persönlichen Zugang einmalig anlegen. Erst danach kann eine öffentliche Einladung exportiert werden."
                : selected is null
                    ? "Schritt 1 · Mehrere bestehende Zugänge: den richtigen ausdrücklich auswählen. Kein neuer Schlüssel notwendig."
                    : string.Equals(exportedIdentityFingerprint, selected.Fingerprint, StringComparison.OrdinalIgnoreCase)
                        ? "Einladung exportiert. Bei Teamzugriff: Datei übergeben und Fingerabdruck separat bestätigen lassen. Anschließend den freigegebenen Gruppenstand ausdrücklich importieren. Noch kein Gruppenzugriff erteilt."
                        : "Persönlicher Zugang vorhanden. Ohne Teamfreigabe hier fertig. Für Gruppenprojekte nur bei Bedarf öffentliche Einladung an die Hauptverwaltung weitergeben.";

        var own = recipients.Any(row => identities.Any(identity =>
            string.Equals(identity.Fingerprint, row.Fingerprint, StringComparison.OrdinalIgnoreCase)));
        setupStatus.Text = identityOnly ? ""
            : identities.Count == 0 ? "Offen: eigene Windows-Identität anlegen."
            : !own ? $"Offen: eigene Identität zur bisherigen Teamliste hinzufügen. Aktuell {recipients.Count} Empfänger."
            : recipients.Count < 2 ? "Bisherige Teamliste mit einem eigenen Zugang vorbereitet. Jetzt speichern; weitere Kollegen sind optional."
            : $"Bisherige Teamliste mit {recipients.Count} Zugängen vorbereitet, eigener Zugang enthalten. Jetzt speichern. Mehrere lokale Identitäten sind nicht automatisch mehrere Benutzer.";
    }

    private static string SafeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    protected override void OnClosed(EventArgs e)
    {
        foreach (var row in identities) row.Certificate.Dispose(); foreach (var row in recipients) row.Dispose(); base.OnClosed(e);
    }
}

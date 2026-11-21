using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyVaultDialogContract(List<string> checks)
    {
        VerifyPasswordPrompt(checks);
        VerifyTextEntryPrompt(checks);
        VerifyVaultAccessRoundTrip(checks);
        var fixture = Path.Combine(Path.GetTempPath(), "SieEncode-VaultUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=Synthetic UI Recipient", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var invitation = CertificateVaultService.ExportInvitation(certificate, "Synthetic UI Recipient");
            var store = new TeamVaultRecipientStore(fixture);
            store.Save([invitation]);
            var original = File.ReadAllBytes(store.Path);
            // The group-vault identity setup is intentionally independent of legacy team-list storage.
            // It must not offer legacy save/remove actions or mutate the recipient list when opened/closed.
            var identitySetup = new TeamVaultWindow(this, fixture, initialSetup: true, identityProvider: () => []);
            try
            {
                VerifyAutomationContract(identitySetup);
                var buttons = DescendantsAndSelf(identitySetup).OfType<Button>()
                    .Select(button => button.Content?.ToString()).ToArray();
                if (!buttons.Contains("Öffentliche Einladung exportieren") || !buttons.Contains("Schließen") ||
                    buttons.Contains("Einrichtung speichern") || buttons.Contains("Eigene Identität zum Team hinzufügen") ||
                    DescendantsAndSelf(identitySetup).OfType<ListBox>().Any())
                    throw new InvalidOperationException("Persönlicher Vault-Einstieg zeigt Aktionen der bisherigen Teamliste.");
                if (!File.ReadAllBytes(store.Path).SequenceEqual(original))
                    throw new InvalidOperationException("Öffnen der persönlichen Identität verändert die bisherige Teamliste.");
            }
            finally { identitySetup.Close(); }
            if (!File.ReadAllBytes(store.Path).SequenceEqual(original))
                throw new InvalidOperationException("Schließen der persönlichen Identität verändert die bisherige Teamliste.");
            checks.Add("Persönlicher Vault-Einstieg: nur Identität und öffentliche Einladung, keine bisherige Teamlistenpflege oder Speicherung beim Öffnen/Schließen.");
            // Empty provider keeps the Windows certificate store entirely outside this smoke test.
            foreach (var dialog in new Window[] {
                new TeamVaultWindow(this, fixture, identityProvider: () => []),
                new VaultAccessWindow(this, teamRecipients: [invitation], identityProvider: () => []) })
            {
                try
                {
                    VerifyAutomationContract(dialog);
                    var elements = DescendantsAndSelf(dialog).OfType<FrameworkElement>().ToArray();
                    var recipients = elements.OfType<ListBox>().Single();
                    if (recipients.Items.Count != 1) throw new InvalidOperationException("Synthetische Einladung fehlt im Vault-Dialog.");
                    recipients.SelectedIndex = 0;
                    var remove = elements.OfType<Button>().Single(button => button.Content.ToString()!.EndsWith("entfernen", StringComparison.Ordinal));
                    remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (recipients.Items.Count != 0) throw new InvalidOperationException("Empfängerentfernung ist nicht verbunden.");
                    if (!File.ReadAllBytes(store.Path).SequenceEqual(original)) throw new InvalidOperationException("Dialogentwurf schreibt ohne Speichern die Teamliste.");
                    if (dialog is VaultAccessWindow access && access.TakeRecipients().Count != 0)
                        throw new InvalidOperationException("Unbestätigter Vault-Dialog gibt Empfänger frei.");
                    var scroll = (ScrollViewer)dialog.Content;
                    scroll.Measure(new Size(640, 220)); scroll.Arrange(new Rect(0, 0, 640, 220)); scroll.UpdateLayout();
                    scroll.ScrollToEnd(); scroll.UpdateLayout();
                    var save = elements.OfType<Button>().Single(button => button.Content.ToString() is "Einrichtung speichern" or "Vault sicher freigeben");
                    var bottom = save.TranslatePoint(new Point(0, save.ActualHeight), scroll).Y;
                    if (scroll.ScrollableHeight <= 0 || bottom <= 0 || bottom > scroll.ActualHeight + 1)
                        throw new InvalidOperationException("Vault-Aktionsleiste ist im kurzen Fenster nicht erreichbar.");
                    VerifyCancelClick(dialog);
                    if (!File.ReadAllBytes(store.Path).SequenceEqual(original) ||
                        dialog is VaultAccessWindow cancelled && cancelled.TakeRecipients().Count != 0)
                        throw new InvalidOperationException("Abbrechen speichert oder bestätigt den Vault-Entwurf.");
                }
                finally { dialog.Close(); }
            }
            using var ownCertificate = new X509Certificate2(certificate);
            IReadOnlyList<WindowsVaultIdentity> OwnIdentity() => [new("Synthetic UI Recipient", CertificateVaultService.Fingerprint(ownCertificate), new X509Certificate2(ownCertificate))];
            var conflicting = new TeamVaultWindow(this, fixture, identityProvider: OwnIdentity);
            try
            {
                var add = DescendantsAndSelf(conflicting).OfType<Button>().Single(button => button.Content.ToString() == "Eigene Identität zum Team hinzufügen");
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    conflicting.ReloadIdentities();
                    add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                var identity = (WindowsVaultIdentity)DescendantsAndSelf(conflicting).OfType<ComboBox>().Single().SelectedItem;
                if (identity.Certificate.RawData.Length == 0 || DescendantsAndSelf(conflicting).OfType<ListBox>().Single().Items.Count != 1 ||
                    DescendantsAndSelf(conflicting).OfType<TextBlock>().Any(text => text.Text.Contains("Aktion nicht abgeschlossen", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Wiederholtes Hinzufügen beschädigt die ausgewählte Identität.");
                store.Save([CertificateVaultService.ExportInvitation(certificate, "Changed by another user")]);
                var changedBytes = File.ReadAllBytes(store.Path);
                var save = DescendantsAndSelf(conflicting).OfType<Button>().Single(button => button.Content.ToString() == "Einrichtung speichern");
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (conflicting.DialogResult is not null || !File.ReadAllBytes(store.Path).SequenceEqual(changedBytes) ||
                    !DescendantsAndSelf(conflicting).OfType<TextBlock>().Any(text => text.Text.Contains("nicht überschrieben", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Team-Speicherkonflikt wird nicht ohne Überschreiben im Dialog angezeigt.");
            }
            finally { conflicting.Close(); }
            var accessReload = new VaultAccessWindow(this, teamRecipients: [invitation], identityProvider: OwnIdentity);
            try
            {
                var add = DescendantsAndSelf(accessReload).OfType<Button>().Single(button => button.Content.ToString() == "Zur Empfängerliste hinzufügen");
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    accessReload.ReloadIdentities();
                    add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var selected = (WindowsVaultIdentity)DescendantsAndSelf(accessReload).OfType<ComboBox>().Single().SelectedItem;
                    if (selected.Certificate.RawData.Length == 0 || DescendantsAndSelf(accessReload).OfType<ListBox>().Single().Items.Count != 1)
                        throw new InvalidOperationException("Projekt-Vault verliert Identitätsauswahl beim Neuladen.");
                }
            }
            finally { accessReload.Close(); }
            var secondRequest = new CertificateRequest("CN=Synthetic Removed Recipient", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var secondCertificate = secondRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var removedInvitation = CertificateVaultService.ExportInvitation(secondCertificate, "Synthetic Removed Recipient");
            var existingVault = new KeyVaultDocument { Version = KeyVaultService.CertificateVersion,
                Recipients = [new KeyVaultRecipient { Certificate = invitation.Certificate, DisplayName = invitation.DisplayName }] };
            foreach (var state in new[] { "existing", "new", "legacy" })
            {
                var existing = state == "existing" ? existingVault : state == "legacy" ? new KeyVaultDocument { Version = 2 } : null;
                var selection = new VaultAccessWindow(this, existing, [invitation, removedInvitation], () => []);
                try
                {
                    var rows = DescendantsAndSelf(selection).OfType<ListBox>().Single().Items.Cast<VaultRecipientChoice>().ToArray();
                    if (rows.Length != (state == "existing" ? 1 : 2) ||
                        state == "existing" && rows[0].Fingerprint != invitation.Fingerprint)
                        throw new InvalidOperationException("Teamvorgaben überschreiben bestehende Vault-Empfänger oder fehlen bei Neuanlage.");
                    VerifyCancelClick(selection);
                    if (selection.TakeRecipients().Count != 0 || existingVault.Recipients.Count != 1)
                        throw new InvalidOperationException("Empfängervorschau verändert oder bestätigt den bestehenden Vault.");
                }
                finally { selection.Close(); }
            }
            checks.Add("Vault-Empfänger: bestehende Freigaben bleiben beim Öffnen unverändert; Teamvorgaben nur für Neuanlage/v2-Umstellung, Abbruch ohne Übernahme.");
            var reopened = new TeamVaultWindow(this, fixture, identityProvider: OwnIdentity);
            try
            {
                reopened.Opacity = 0; reopened.ShowInTaskbar = false; reopened.ShowActivated = false;
                var timedOut = false;
                var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timeout.Tick += (_, _) => { timedOut = true; reopened.Close(); };
                reopened.Loaded += (_, _) => reopened.Dispatcher.BeginInvoke(new Action(() =>
                    DescendantsAndSelf(reopened).OfType<Button>().Single(button => button.Content.ToString() == "Einrichtung speichern")
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                timeout.Start();
                try
                {
                    if (reopened.ShowDialog() != true || timedOut || new TeamVaultRecipientStore(fixture).Load().Recipients.Count != 1)
                        throw new InvalidOperationException("Erneut geöffnete Teamliste lässt sich nicht sicher speichern.");
                }
                finally { timeout.Stop(); }
            }
            finally { reopened.Close(); }
            checks.Add("Team-Speicherung: konkurrierende Änderung bleibt erhalten, Fehler im Dialog sichtbar; erneutes Öffnen und Speichern synthetisch erfolgreich.");
            checks.Add("Vault-Identitätsauswahl: dreifaches Neuladen/Hinzufügen in beiden Dialogen erhält gültige Zertifikatshandles ohne doppelte Empfänger.");
            checks.Add("Team-/Vaultdialoge: synthetische Empfänger per Click entfernt, Entwurf nicht gespeichert/freigegeben, Aktionsleiste scrollbar; Windows-Zertifikatsspeicher nicht verwendet.");
        }
        finally { Directory.Delete(fixture, true); }
    }

    private void VerifyVaultAccessRoundTrip(List<string> checks)
    {
        var root = Path.Combine(Path.GetTempPath(), "SieEncode-VaultAccess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var ownerKey = RSA.Create(3072);
        using var colleagueKey = RSA.Create(3072);
        using var owner = new CertificateRequest("CN=Synthetic UI Owner", ownerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var colleague = new CertificateRequest("CN=Synthetic UI Colleague", colleagueKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var team = new[] { CertificateVaultService.ExportInvitation(owner, "Owner"), CertificateVaultService.ExportInvitation(colleague, "Colleague") };
        var vaultPath = Path.Combine(root, "Synthetic.keys.vault.json");
        var payload = new ProjectKeyVaultPayload { ProductivePicc = new KeyReference { Type = "DF_KEY_AES", Value = new string('A', 32) } };
        try
        {
            foreach (var changeRecipients in new[] { false, true })
            {
                var existing = changeRecipients ? System.Text.Json.JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(vaultPath), ProjectJson.Options) : null;
                var dialog = new VaultAccessWindow(this, existing, team, () => []);
                var passphrase = changeRecipients ? "Synthetic-UI-Recovery-Changed-2026!" : "Synthetic-UI-Recovery-Initial-2026!";
                Exception? failure = null;
                var timedOut = false;
                dialog.Opacity = 0; dialog.ShowInTaskbar = false; dialog.ShowActivated = false;
                var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timeout.Tick += (_, _) => { timedOut = true; dialog.Close(); };
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var elements = DescendantsAndSelf(dialog).OfType<FrameworkElement>().ToArray();
                        var list = elements.OfType<ListBox>().Single();
                        if (list.Items.Count != 2) throw new InvalidOperationException("Vault-Neuanlage oder erneutes Öffnen verliert Empfänger.");
                        if (changeRecipients)
                        {
                            list.SelectedItem = list.Items.Cast<VaultRecipientChoice>().Single(row => row.Fingerprint == team[1].Fingerprint);
                            elements.OfType<Button>().Single(button => button.Content.ToString() == "Ausgewählten Empfänger entfernen")
                                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        foreach (var box in elements.OfType<PasswordBox>()) box.Password = passphrase;
                        elements.OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception exception) { failure = exception; dialog.Close(); }
                }));
                try
                {
                    timeout.Start();
                    if (dialog.ShowDialog() != true || timedOut || failure is not null)
                        throw new InvalidOperationException("Vault-Freigabe über den positiven Dialogablauf fehlgeschlagen.", failure);
                    var accepted = dialog.TakeRecipients();
                    try
                    {
                        // OnClosed already disposed dialog-owned certificates. Returned copies must remain usable.
                        if (accepted.Count != (changeRecipients ? 1 : 2) || accepted.Any(row => row.Certificate.RawData.Length == 0))
                            throw new InvalidOperationException("Geschlossener Freigabedialog liefert ungültige Empfängerhandles.");
                        ProjectKeyVault.SaveForRecipients(vaultPath, payload, dialog.RecoveryPassphrase, accepted, ConcurrentFileWriter.Capture(vaultPath));
                    }
                    finally { foreach (var recipient in accepted) recipient.Certificate.Dispose(); }
                    if (ProjectKeyVault.LoadWithCertificates(vaultPath, [owner]).ProductivePicc.Value != payload.ProductivePicc.Value ||
                        ProjectKeyVault.Load(vaultPath, passphrase).ProductivePicc.Value != payload.ProductivePicc.Value)
                        throw new InvalidOperationException("Nach Dialogfreigabe gespeicherter Vault verliert Identitäts- oder Recovery-Zugang.");
                    if (!changeRecipients && ProjectKeyVault.LoadWithCertificates(vaultPath, [colleague]).ProductivePicc.Value != payload.ProductivePicc.Value)
                        throw new InvalidOperationException("Zweiter freigegebener Empfänger kann den gespeicherten Vault nicht öffnen.");
                }
                finally { timeout.Stop(); dialog.Close(); }
            }
            var removedCanOpen = false;
            try { ProjectKeyVault.LoadWithCertificates(vaultPath, [colleague]); removedCanOpen = true; }
            catch (CryptographicException) { }
            if (removedCanOpen) throw new InvalidOperationException("Im Dialog entfernter Empfänger kann den neuen Vault weiter öffnen.");
            var oldRecoveryWorks = false;
            try { ProjectKeyVault.Load(vaultPath, "Synthetic-UI-Recovery-Initial-2026!"); oldRecoveryWorks = true; }
            catch (CryptographicException) { }
            if (oldRecoveryWorks) throw new InvalidOperationException("Alte Recovery-Passphrase bleibt nach Dialogänderung gültig.");
            checks.Add("Vault-Freigabe: Dialog mit zwei synthetischen Empfängern bestätigt, nach Schließen gespeichert und geöffnet; Empfänger im erneut geöffneten Dialog entfernt, Recovery geändert und alte Zugänge am neuen Vault abgelehnt. Kein Windows-Mehrbenutzernachweis.");
        }
        finally { Directory.Delete(root, true); }
    }

    private void VerifyEditorVaultButtons(List<string> checks)
    {
        var root = Path.Combine(Path.GetTempPath(), "SieEncode-EditorVault-" + Guid.NewGuid().ToString("N"));
        var oldProjects = projectsDirectory;
        var oldBackup = settings.VaultBackup.Directory;
        var oldIdentities = loadEditorVaultIdentities;
        const string passphrase = "Synthetic-Editor-Recovery-2026!";
        try
        {
            projectsDirectory = Path.Combine(root, "projects");
            settings.VaultBackup.Directory = Path.Combine(root, "backup");
            Directory.CreateDirectory(projectsDirectory);
            var library = LoadProfileLibrary();
            var project = ProjectDraftFactory.Create(library, "Synthetic-EditorVault");
            project.Productive.PiccKey.Value = KeyVaultService.GenerateKey(project.Productive.PiccKey.Type);
            foreach (var app in library.Compose(project.Profiles.Base, project.Profiles.Customers).Applications)
                project.Productive.ApplicationKeys[ProfileValidator.NormalizeAid(app.Id)] = Enumerable.Range(0, app.KeyCount)
                    .Select(_ => KeyVaultService.GenerateKey(app.KeyType)).ToList();
            var path = Path.Combine(projectsDirectory, "Synthetic-EditorVault.project.json");
            ProjectJson.Save(path, ProjectJson.WithoutAllSecrets(project));
            LoadEditor(project, path);
            Navigate("Editor", false);
            void ClickWithPassphrase(Button button, string title, bool cancel = false)
            {
                var answered = false;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    var prompt = OwnedWindows.Cast<Window>().FirstOrDefault(window => window.Title == title);
                    if (prompt is null) return;
                    timer.Stop();
                    foreach (var box in DescendantsAndSelf(prompt).OfType<PasswordBox>()) box.Password = passphrase;
                    answered = true;
                    DescendantsAndSelf(prompt).OfType<Button>().Single(candidate => cancel ? candidate.IsCancel : candidate.IsDefault)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                try
                {
                    if (!button.IsEnabled) throw new InvalidOperationException("Vault-Editoraktion ist unerwartet gesperrt: " + button.Name);
                    timer.Start();
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (!answered) throw new InvalidOperationException("Vault-Editoraktion öffnet den erwarteten Passwortdialog nicht.");
                }
                finally { timer.Stop(); }
            }
            ClickWithPassphrase(EditorSaveVaultButton, "Keys verschlüsselt speichern");
            var vault = ProjectKeyVault.GetPath(path);
            if (!File.Exists(vault) || !editorVaultVerified || !editorKeysPersisted || editorVaultVersion != 2)
                throw new InvalidOperationException("Vault-Speichern per Editor-Click schließt die Verifikation nicht ab.");
            var backups = Directory.GetFiles(settings.VaultBackup.Directory, "*.keys.vault.json", SearchOption.AllDirectories);
            if (backups.Length != 1 || KeySetIdentityService.Compute(ProjectKeyVault.Load(backups[0], passphrase)) != KeySetIdentityService.Compute(ProjectKeyVault.FromProject(project)))
                throw new InvalidOperationException("Editor-Speichern erstellt keine unabhängig wieder öffnungsfähige Vault-Sicherung.");
            LoadEditor(ProjectJson.Load(path), path);
            if (editorVaultVerified || !string.IsNullOrEmpty(editorProject!.Productive.PiccKey.Value))
                throw new InvalidOperationException("Erneutes Laden übernimmt unerwartet entschlüsselte Schlüssel.");
            void VerifyLockedVaultGuidance()
            {
                if (EditorWorkflowValues.Text != "✓ Pflichtfelder" || EditorWorkflowKeys.Text != "! Vault entsperren" ||
                    !EditorNextStep.Text.Contains("entsperren", StringComparison.Ordinal) ||
                    EditorBuildButton.IsEnabled || !EditorUnlockVaultButton.IsEnabled)
                    throw new InvalidOperationException("Gesperrter Vault zeigt falsche Pflichtfeld-/Entsperrhinweise oder erlaubt Neubau.");
            }
            VerifyLockedVaultGuidance();
            var beforeUnlockCancel = File.ReadAllBytes(vault);
            ClickWithPassphrase(EditorUnlockVaultButton, "Vault entsperren", cancel: true);
            VerifyLockedVaultGuidance();
            if (editorVaultVerified || !File.ReadAllBytes(vault).SequenceEqual(beforeUnlockCancel))
                throw new InvalidOperationException("Abgebrochenes Entsperren verändert oder verifiziert den Vault.");
            ClickWithPassphrase(EditorUnlockVaultButton, "Vault entsperren");
            if (!editorVaultVerified || !editorKeysPersisted || KeySetIdentityService.Compute(ProjectKeyVault.FromProject(editorProject!)) != KeySetIdentityService.Compute(ProjectKeyVault.FromProject(project)))
                throw new InvalidOperationException("Vault-Entsperren per Editor-Click stellt den gespeicherten Schlüsselsatz nicht wieder her.");
            var initialKey = EditorTransportPiccKey.Text;
            EditorTransportPiccKey.Text = "00";
            if (EditorWorkflowValues.Text != "✓ Pflichtfelder" ||
                !EditorNextStep.Text.Contains("Grundschlüssel prüfen", StringComparison.Ordinal) || EditorBuildButton.IsEnabled)
                throw new InvalidOperationException("Ungültiger geladener Grundschlüssel wird nicht angezeigt oder erlaubt Neubau.");
            EditorTransportPiccKey.Text = initialKey;
            ClickWithPassphrase(EditorSaveVaultButton, "Keys verschlüsselt speichern");
            checks.Add("Vault-Anleitung: gesperrter Vault und Entsperrabbruch erhalten Bausperre/Datei; ungültiger Grundschlüssel separat angezeigt und gesperrt.");
            checks.Add("Editor-Vault v2: Speichern und Entsperren über tatsächliche Editor-Clicks/Passwortdialoge; automatische unabhängige Sicherung wieder geöffnet, Sitzung nach erneutem Projektladen erst durch Entsperren freigegeben. Synthetische Daten.");
            var stationEntry = new ProjectCatalog().Scan(projectsDirectory).Single(item => item.SourcePath == Path.GetFullPath(path));
            var savedSource = File.ReadAllBytes(path);
            var savedVault = File.ReadAllBytes(vault);
            loadEditorVaultIdentities = () => throw new InvalidOperationException("Passphrasen-Vault darf keine persönliche Identität benötigen.");
            try
            {
                var materialized = MaterializeSecureStationEntry(stationEntry, () => passphrase);
                if (secureOperationalSession is null || projectSecretServer is null ||
                    secureOperationalSession.Build.SessionSecrets.Count == 0 ||
                    materialized.ProjectDirectory == stationEntry.ProjectDirectory ||
                    !Directory.EnumerateFiles(materialized.ProjectDirectory, "*.rfPrj", SearchOption.AllDirectories)
                        .Any(file => RFiDGear.Builder.RfPrj.RfPrjArchive.Load(file).Tasks.Any(task => task.ToString().Contains("secret://", StringComparison.Ordinal))))
                    throw new InvalidOperationException("Passphrasen-Station stellt keine geschützte Sitzung mit Secretverweisen bereit.");
                projectSecretServer.AuthorizeClient(Environment.ProcessId);
                using (var pipe = new System.IO.Pipes.NamedPipeClientStream(".", projectSecretServer.Descriptor.PipeName, System.IO.Pipes.PipeDirection.InOut))
                {
                    pipe.Connect(5000);
                    using var reader = new StreamReader(pipe, leaveOpen: true);
                    using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                    var secret = secureOperationalSession.Build.SessionSecrets.First();
                    writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Reference = secret.Key }));
                    var response = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    using var json = System.Text.Json.JsonDocument.Parse(response!);
                    if (!json.RootElement.GetProperty("Success").GetBoolean() || json.RootElement.GetProperty("Secret").GetString() != secret.Value)
                        throw new InvalidOperationException("Stations-Secretserver liefert den freigegebenen synthetischen Schlüssel nicht.");
                }
                try { MaterializeSecureStationEntry(stationEntry, () => "Incorrect-Synthetic-Passphrase!"); throw new InvalidOperationException("Falsche Passphrase akzeptiert."); }
                catch (CryptographicException) { }
                if (projectSecretServer is not null || secureOperationalSession is not null)
                    throw new InvalidOperationException("Fehlgeschlagenes Entsperren behält eine alte Schlüsselsitzung.");
                try { MaterializeSecureStationEntry(stationEntry, () => null); throw new InvalidOperationException("Abbruch ignoriert."); }
                catch (OperationCanceledException) { }
                if (projectSecretServer is not null || secureOperationalSession is not null ||
                    !File.ReadAllBytes(path).SequenceEqual(savedSource) || !File.ReadAllBytes(vault).SequenceEqual(savedVault))
                    throw new InvalidOperationException("Stationsentsperren verändert Quelle/Vault oder hinterlässt nach Abbruch Schlüssel.");
                checks.Add("Passphrasen-Station v2: geschützte Sitzung/Secretserver ohne Identität, falsche Passphrase und Abbruch ohne alte Sitzung; Quelle/Vault bytegleich.");
            }
            finally
            {
                projectSecretServer?.Dispose(); projectSecretServer = null;
                secureOperationalSession?.Dispose(); secureOperationalSession = null;
                loadEditorVaultIdentities = oldIdentities;
            }
            using var identityKey = RSA.Create(3072);
            using var identityCertificate = new CertificateRequest("CN=Synthetic Editor Identity", identityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            loadEditorVaultIdentities = () => [new("Synthetic Editor Identity", CertificateVaultService.Fingerprint(identityCertificate), new X509Certificate2(identityCertificate))];
            new TeamVaultRecipientStore(projectsDirectory).Save([CertificateVaultService.ExportInvitation(identityCertificate, "Synthetic Editor Identity")]);
            ClickWithPassphrase(EditorManageVaultAccessButton, "Vault-Zugriff für mehrere Benutzer");
            if (editorVaultVersion != KeyVaultService.CertificateVersion || !editorVaultVerified ||
                ProjectKeyVault.LoadWithCertificates(vault, [identityCertificate]).ProductivePicc.Value != project.Productive.PiccKey.Value)
                throw new InvalidOperationException("Editor-Freigabe migriert den v2-Vault nicht verlustfrei zum Identitätszugriff.");
            LoadEditor(ProjectJson.Load(path), path);
            EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!editorVaultVerified || !EditorVaultStatus.Text.Contains("Windows-Identität", StringComparison.Ordinal))
                throw new InvalidOperationException("Editor entsperrt den v3-Vault nicht automatisch mit der freigegebenen Identität.");
            var updatedKey = KeyVaultService.GenerateKey("DF_KEY_AES");
            EditorProductivePiccKey.Text = updatedKey;
            if (editorVaultVerified || !EditorSaveVaultButton.IsEnabled)
                throw new InvalidOperationException("Geänderter Schlüssel setzt den Vault-Nachweis nicht zurück oder lässt sich nicht speichern.");
            EditorSaveVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!editorVaultVerified || ProjectKeyVault.LoadWithCertificates(vault, [identityCertificate]).ProductivePicc.Value != updatedKey ||
                ProjectKeyVault.Load(vault, passphrase).ProductivePicc.Value != updatedKey)
                throw new InvalidOperationException("Editor-Speichern mit Identität verliert neue Schlüssel oder Recovery-Zugang.");
            loadEditorVaultIdentities = () => [];
            LoadEditor(ProjectJson.Load(path), path);
            ClickWithPassphrase(EditorUnlockVaultButton, "Vault wiederherstellen");
            if (!editorVaultVerified || editorProject!.Productive.PiccKey.Value != updatedKey ||
                !EditorVaultStatus.Text.Contains("Recovery-Zugang", StringComparison.Ordinal))
                throw new InvalidOperationException("Editor stellt den v3-Vault ohne lokale Identität nicht über Recovery wieder her.");
            checks.Add("Editor-Vault v3: Freigabe/migrieren, automatisch per synthetischer Identität entsperren, Schlüsseländerung mit Identität speichern und ohne Identität per Recovery entsperren über Editor-Clicks geprüft; Windows-Zertifikatsspeicher nicht verändert.");
            var recoveryUpdatedKey = KeyVaultService.GenerateKey("DF_KEY_AES");
            EditorProductivePiccKey.Text = recoveryUpdatedKey;
            var beforeRecoverySave = File.ReadAllBytes(vault);
            ClickWithPassphrase(EditorSaveVaultButton, "Vault-Zugriff für mehrere Benutzer", cancel: true);
            if (!File.ReadAllBytes(vault).SequenceEqual(beforeRecoverySave) || editorVaultVerified)
                throw new InvalidOperationException("Abgebrochene Recovery-Freigabe überschreibt den Vault oder bestätigt ungespeicherte Schlüssel.");
            ClickWithPassphrase(EditorSaveVaultButton, "Vault-Zugriff für mehrere Benutzer");
            var recoverySaved = System.Text.Json.JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(vault), ProjectJson.Options)!;
            if (!editorVaultVerified || editorVaultVersion != KeyVaultService.CertificateVersion || recoverySaved.Recipients.Count != 1 ||
                recoverySaved.Recipients[0].Id != CertificateVaultService.Fingerprint(identityCertificate) ||
                ProjectKeyVault.LoadWithCertificates(vault, [identityCertificate]).ProductivePicc.Value != recoveryUpdatedKey ||
                ProjectKeyVault.Load(vault, passphrase).ProductivePicc.Value != recoveryUpdatedKey)
                throw new InvalidOperationException("Speichern nach Recovery verliert Schlüssel, Empfänger oder Recovery-Zugang.");
            checks.Add("Recovery-Sitzung ohne Identität: Editor-Speichern öffnet vorhandene Freigabe, erhält Empfänger und speichert den geänderten Schlüssel nach Passphrasebestätigung; Identitäts-/Recovery-Zugang danach erfolgreich.");
            VerifyGroupVaultEditor(project, identityCertificate, checks);
        }
        finally
        {
            projectsDirectory = oldProjects;
            settings.VaultBackup.Directory = oldBackup;
            loadEditorVaultIdentities = oldIdentities;
            // Only synthetic editor state remains here; do not prompt to save it when the test window closes.
            editorDirty = false;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private void VerifyExpertNavigationClicks(List<string> checks)
    {
        var originalMode = settings.ExpertMode;
        var originalPage = currentPage;
        try
        {
            settings.ExpertMode = true;
            ApplyOperatingMode();
            foreach (var button in new[] { NavProfilesButton, WorkspaceAnalysisButton, WorkspaceDiffButton, WorkspaceAcceptanceButton })
            {
                Navigate(button == WorkspaceAcceptanceButton ? "TestStation" : "Projects", false);
                for (DependencyObject? ancestor = button; ancestor is not null; ancestor = LogicalTreeHelper.GetParent(ancestor))
                    if (ancestor is FrameworkElement element && element.Visibility != Visibility.Visible)
                        throw new InvalidOperationException("Expertennavigation durch ausgeblendeten Vorfahren blockiert: " + button.Name);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (currentPage != (string)button.Tag || pages[currentPage].Visibility != Visibility.Visible)
                    throw new InvalidOperationException("Expertennavigation erreicht ihr Ziel nicht: " + button.Name);
            }
            checks.Add("Expertennavigation: Profile, Taskplan, Vergleich und Abnahme über tatsächliche Button-Clicks und sichtbare Vorfahren geprüft.");
        }
        finally { settings.ExpertMode = originalMode; ApplyOperatingMode(); Navigate(originalPage, false); }
    }

    private void VerifyTextEntryPrompt(List<string> checks)
    {
        var (dialog, input) = TextEntryPrompt.Create(this, "Synthetic name", "Name", "Synthetic default");
        try
        {
            VerifyAutomationContract(dialog);
            if (input.Text != "Synthetic default") throw new InvalidOperationException("Namensvorbelegung fehlt.");
            input.Text = "   ";
            DescendantsAndSelf(dialog).OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (dialog.DialogResult is not null || !DescendantsAndSelf(dialog).OfType<TextBlock>().Any(text => text.Text == "Bitte einen Namen eingeben."))
                throw new InvalidOperationException("Leere Namenseingabe bleibt ohne sichtbare Validierung.");
            VerifyCancelClick(dialog);
        }
        finally { dialog.Close(); }
        var (accepted, value) = TextEntryPrompt.Create(this, "Synthetic name", "Name", "  Synthetic Operator  ");
        try
        {
            accepted.Opacity = 0; accepted.ShowInTaskbar = false; accepted.ShowActivated = false;
            var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timeout.Tick += (_, _) => accepted.Close();
            accepted.Loaded += (_, _) => accepted.Dispatcher.BeginInvoke(new Action(() =>
                DescendantsAndSelf(accepted).OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            timeout.Start();
            try { if (accepted.ShowDialog() != true || value.Text.Trim() != "Synthetic Operator") throw new InvalidOperationException("Gültige Namenseingabe wird nicht bestätigt."); }
            finally { timeout.Stop(); }
        }
        finally { accepted.Close(); }
        checks.Add("Namensdialog: Vorbelegung erhalten, Leerzeichen abgelehnt mit Hinweis, Abbrechen ohne Freigabe, gültiger Name bestätigt; keine Identität erzeugt.");
    }

    private void VerifyPasswordPrompt(List<string> checks)
    {
        foreach (var confirm in new[] { false, true })
        {
            var (dialog, password) = PasswordPrompt.Create(this, "Synthetic passphrase", "Synthetic input", confirm);
            try
            {
                VerifyAutomationContract(dialog);
                var elements = DescendantsAndSelf(dialog).OfType<FrameworkElement>().ToArray();
                var ok = elements.OfType<Button>().Single(button => button.IsDefault);
                void Reject(string expected)
                {
                    ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (dialog.DialogResult is not null || !elements.OfType<TextBlock>().Any(text => text.Text.Contains(expected, StringComparison.Ordinal)))
                        throw new InvalidOperationException("Passphraseprüfung lehnt ungültige Eingabe nicht im Dialog ab.");
                }
                Reject("Bitte eine Passphrase");
                if (confirm)
                {
                    password.Password = "synthetic";
                    Reject("nicht überein");
                    elements.OfType<PasswordBox>().Last().Password = password.Password;
                    Reject("Zeichen");
                    password.Password = new string('x', KeyVaultService.MinimumNewPassphraseLength);
                    elements.OfType<PasswordBox>().Last().Password = password.Password;
                    Reject("gleichförmig");
                }
                VerifyCancelClick(dialog);
            }
            finally { dialog.Close(); }
            var (accepted, value) = PasswordPrompt.Create(this, "Synthetic acceptance", "Synthetic input", confirm);
            try
            {
                // Legacy unlock must continue accepting short existing passphrases.
                var expected = confirm ? "Synthetic-Only-7x!A9-bC3-kL5" : "old";
                foreach (var field in DescendantsAndSelf(accepted).OfType<PasswordBox>()) field.Password = expected;
                accepted.Opacity = 0; accepted.ShowInTaskbar = false; accepted.ShowActivated = false;
                var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timeout.Tick += (_, _) => accepted.Close();
                accepted.Loaded += (_, _) => accepted.Dispatcher.BeginInvoke(new Action(() =>
                    DescendantsAndSelf(accepted).OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                timeout.Start();
                try { if (accepted.ShowDialog() != true || value.Password != expected) throw new InvalidOperationException("Gültige Passphrase wird nicht unverändert übernommen."); }
                finally { timeout.Stop(); }
            }
            finally { accepted.Close(); }
        }
        checks.Add("Passphrase: leer/abweichend/zu kurz abgelehnt, Abbrechen ohne Freigabe, gültige neue und kurze bestehende Passphrase übernommen.");
    }

    private static void VerifyCancelClick(Window dialog)
    {
        var cancel = DescendantsAndSelf(dialog).OfType<Button>().Single(button => button.Content.ToString() == "Abbrechen");
        if (!cancel.IsCancel) throw new InvalidOperationException("Escape-Zuordnung fehlt: " + dialog.Title);
        dialog.Opacity = 0; dialog.ShowInTaskbar = false; dialog.ShowActivated = false;
        var timedOut = false;
        var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => { timedOut = true; dialog.Close(); };
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() => cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        timeout.Start();
        try
        {
            if (dialog.ShowDialog() != false || timedOut)
                throw new InvalidOperationException("Abbrechen schließt den Dialog nicht ohne Freigabe: " + dialog.Title);
        }
        finally { timeout.Stop(); }
    }

    private void VerifyPiccFlagBindings(List<string> checks)
    {
        var controls = new[] { editorPiccChangeMasterKey, editorPiccListApplications, editorPiccCreateDelete, editorPiccConfigurationChange };
        var original = Convert.ToInt32(editorProject!.Productive.PiccKeySettings, 16);
        for (var index = 0; index < controls.Length; index++)
        {
            var control = controls[index];
            var initial = control.IsChecked == true;
            foreach (var value in new[] { !initial, initial })
            {
                editorKeysPersisted = true; editorVaultVerified = true; editorDirty = false;
                control.IsChecked = value;
                var expected = value ? original | (1 << index) : original & ~(1 << index);
                if (Convert.ToInt32(editorProject.Productive.PiccKeySettings, 16) != expected ||
                    !editorDirty || editorKeysPersisted || editorVaultVerified)
                    throw new InvalidOperationException("Dynamische PICC-Rechteänderung verliert Modell- oder Vault-Prüfstatus.");
            }
        }
        checks.Add("PICC-Editor: alle vier Checked-/Unchecked-Bindungen aktualisieren Modell, Änderungsmarkierung und Vault-Prüfstatus.");
    }
}

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

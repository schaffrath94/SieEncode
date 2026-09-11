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

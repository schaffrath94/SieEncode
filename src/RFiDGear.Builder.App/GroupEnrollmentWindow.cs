using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupEnrollmentWindow : Window
{
    private readonly GroupEnrollmentPackage package;
    private readonly string localPath;
    private readonly TextBox centralOrigin = new() { MinWidth = 350 };
    private readonly TextBox centralPin = new() { MinWidth = 350, CharacterCasing = CharacterCasing.Upper };
    private readonly CheckBox centralConfirmed = new() { Content = "HTTPS-Adresse und Serverzertifikat unabhängig bestätigt." };
    private readonly TextBox fingerprint = new() { MinWidth = 420 };
    private readonly CheckBox confirmed = new() { Content = "Bibliotheks-ID und Verwaltungsschlüssel unabhängig mit dem Verantwortlichen abgeglichen." };
    private readonly TextBlock error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public GroupEnrollmentWindow(Window owner, GroupEnrollmentPackage candidate, string localTrustDirectory)
    {
        // Keep a private validated snapshot. File changes after opening cannot replace the confirmed key.
        package = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(candidate));
        var verified = GroupEnrollmentPackages.Validate(package);
        localPath = Path.Combine(Path.GetFullPath(localTrustDirectory), package.LibraryId.ToString("N") + ".trust.bin");
        Owner = owner; Title = "Offline-Gruppenverwaltung registrieren"; Width = 780; MaxHeight = 760;
        SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = "Verwaltung einmalig bestätigen", FontSize = 22, FontWeight = FontWeights.SemiBold });
        root.Children.Add(Note("Die Datei enthält öffentliche Verwaltungsdaten. Eine gültige Signatur allein beweist nicht, dass diese Verwaltung zu Ihrem Team gehört. Lassen Sie sich Bibliotheks-ID und Fingerabdruck unabhängig vom Dateiübertragungsweg bestätigen."));
        root.Children.Add(Note($"Bibliotheks-ID: {package.LibraryId:D}\nRichtlinienstand: {verified.Policy.Revision}\nGruppen: {verified.Policy.Groups.Length}"));
        root.Children.Add(Note("Unabhängig erhaltenen SHA-256-Fingerabdruck hier eingeben (64 Hex-Zeichen):"));
        root.Children.Add(fingerprint);
        root.Children.Add(confirmed);
        root.Children.Add(Note("Der Vertrauensstand wird nur für diesen Windows-Benutzer lokal geschützt gespeichert. Vorhandene Registrierungen werden nicht überschrieben. Dies erteilt noch keinen Zugriff auf Projekt-Vaults. Gruppenbasierter Projektzugriff befindet sich in Entwicklung."));
        var central = new StackPanel();
        central.Children.Add(Note("Optional · für zentrale Schlüsselbibliothek. Nur bei tatsächlich eingerichteter Zentrale ausfüllen; sonst bleibt die Registrierung offline."));
        central.Children.Add(Note("HTTPS-Adresse"));
        central.Children.Add(centralOrigin);
        central.Children.Add(Note("SHA-256-Fingerabdruck des Serverzertifikats"));
        central.Children.Add(centralPin);
        central.Children.Add(centralConfirmed);
        root.Children.Add(new Expander { Header = "Zentrale Schlüsselbibliothek · einmalige Verbindung",
            Content = central, IsExpanded = false });
        root.Children.Add(error);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Verwaltung registrieren" };
        save.Click += (_, _) =>
        {
            try
            {
                var endpoint = ReadOptionalEndpoint();
                using var lease = CentralGroupPolicyWorkflow.Acquire(package.LibraryId);
                CentralGroupPolicyWorkflow.RequireNoPending(package.LibraryId);
                Register(fingerprint.Text, confirmed.IsChecked == true);
                if (endpoint is not null)
                {
                    try
                    {
                        new KeyLibrarySyncEndpointStore(KeyLibrarySyncEndpointStore.DefaultPath(package.LibraryId),
                            package.LibraryId, new WindowsGroupTrustProtection(package.LibraryId, syncState: true)).Save(endpoint);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(this, "Die öffentliche Registrierung ist gespeichert. Zentralverbindung konnte nicht gespeichert werden: " +
                            exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                DialogResult = true;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or CryptographicException or ArgumentException or UnauthorizedAccessException)
            { error.Text = exception.Message; }
        };
        footer.Children.Add(cancel); footer.Children.Add(save); root.Children.Add(footer);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        MainWindow.InitializeAutomationContract(this, "GroupEnrollment");
    }

    private KeyLibrarySyncEndpoint? ReadOptionalEndpoint()
    {
        if (string.IsNullOrWhiteSpace(centralOrigin.Text) && string.IsNullOrWhiteSpace(centralPin.Text))
            return null;
        if (centralConfirmed.IsChecked != true)
            throw new InvalidDataException("HTTPS-Adresse und Serverzertifikat unabhängig bestätigen.");
        return KeyLibrarySyncEndpointStore.Validate(new(package.LibraryId, centralOrigin.Text.Trim(),
            string.Concat(centralPin.Text.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant()));
    }

    internal void Register(string independentlyConfirmedFingerprint, bool identityConfirmed)
    {
        if (!identityConfirmed) throw new InvalidDataException("Bitte Bibliotheks-ID und Schlüssel zuerst unabhängig bestätigen.");
        var normalized = string.Concat(independentlyConfirmedFingerprint.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        new OfflineGroupTrustStore(localPath, package.LibraryId, new WindowsGroupTrustProtection(package.LibraryId))
            .Enroll(package.AdministratorPublicKey, normalized, package.Policy);
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

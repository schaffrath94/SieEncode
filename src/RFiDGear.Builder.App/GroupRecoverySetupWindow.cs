using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class GroupRecoverySetupWindow : Window
{
    private readonly string directory;
    private readonly Action<X509Certificate2> install;
    private readonly ComboBox libraries = new();
    private readonly TextBox name = new() { Text = "Separate Team-Recovery" };
    private readonly PasswordBox password = new();
    private readonly PasswordBox repeat = new();
    private readonly CheckBox confirmed = new() { Content = new TextBlock { Text = "Private Sicherung und Passphrase getrennt verwahren; nur die öffentliche Einladung weitergeben.", TextWrapping = TextWrapping.Wrap } };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private VaultRecipientInvitation? invitation;
    private Guid? invitationLibrary;
    private bool exported;

    internal GroupRecoverySetupWindow(Window owner, string localDirectory, Action<X509Certificate2>? installIdentity = null)
    {
        Owner = owner; directory = Path.GetFullPath(localDirectory); install = installIdentity ?? WindowsVaultIdentityService.InstallRecovery;
        Title = "Separate Recovery-Identität einrichten"; Width = 780; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note("1. Registrierte Bibliothek wählen. 2. Neuen separaten Schlüssel verschlüsselt sichern. 3. Nur die öffentliche Einladung an die Hauptverwaltung geben und den Fingerabdruck unabhängig bestätigen. Erst nach Freigabe und Import des neuen Gruppenstandes ist eine Wiederherstellung möglich."));
        libraries.ItemsSource = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(Guid.Parse).ToArray() : [];
        libraries.SelectedIndex = libraries.Items.Count == 1 ? 0 : -1;
        root.Children.Add(Note("Registrierte Bibliothek (Bibliotheks-ID)"));
        var setupHint = Note(libraries.Items.Count == 0
            ? "Noch keine Gruppenverwaltung auf diesem Windows-Benutzer registriert. Diesen Dialog schließen und in Einstellungen → Gruppenverwaltung registrieren die öffentliche Datei des Hauptverwalters übernehmen. Als Hauptverwalter eines neuen Teams zuerst unter Offline-Gruppen verwalten eine Verwaltung erstellen. Danach diesen Dialog erneut öffnen."
            : "Bitte die Bibliothek auswählen, für die Sie einen separaten Recovery-Zugang einrichten oder wiederherstellen möchten.");
        setupHint.Foreground = Brushes.DarkGoldenrod;
        root.Children.Add(setupHint);
        root.Children.Add(libraries); root.Children.Add(Note("Anzeigename für die öffentliche Einladung")); root.Children.Add(name);
        root.Children.Add(Note("Neue Sicherungspassphrase (mindestens 20 Zeichen)")); root.Children.Add(password);
        root.Children.Add(Note("Passphrase wiederholen")); root.Children.Add(repeat); root.Children.Add(confirmed);
        var createBackup = Button("Neuen Recovery-Schlüssel erzeugen und privat sichern", () =>
        {
            try
            {
                if (invitation is not null && !exported) throw new InvalidOperationException("Zuerst die bereits erzeugte öffentliche Einladung exportieren.");
                var library = SelectedLibrary();
                Trust(library).LoadOffline();
                var dialog = new SaveFileDialog { Filter = "Private Recovery-Sicherung (*.group-private.json)|*.group-private.json", FileName = "recovery.group-private.json" };
                if (dialog.ShowDialog(this) == true) CreateBackup(library, name.Text, dialog.FileName, password.Password, repeat.Password, confirmed.IsChecked == true);
            }
            finally { password.Clear(); repeat.Clear(); }
        });
        root.Children.Add(createBackup);
        root.Children.Add(Button("Öffentliche Recovery-Einladung exportieren", () =>
        {
            if (invitation is null) throw new InvalidOperationException("Zuerst die private Sicherung erzeugen.");
            var dialog = new SaveFileDialog { Filter = "Öffentliche Einladung (*.recipient.json)|*.recipient.json", FileName = "recovery.recipient.json" };
            if (dialog.ShowDialog(this) == true) ExportInvitation(dialog.FileName);
        }));
        root.Children.Add(Note("Wiederherstellen: Nur bei Bedarf auf dem vertrauenswürdigen Recovery-Arbeitsplatz. Damit liegt ein privater Entschlüsselungsschlüssel im aktuellen Windows-Benutzer. Recovery-Zugänge gelten bibliotheksweit. Der lokale Stand kann spätere Offline-Entzüge noch nicht kennen; alte Kopien bleiben zugänglich."));
        var restoreIdentity = Button("Freigegebene Recovery-Identität wiederherstellen", () =>
        {
            var library = SelectedLibrary();
            var policy = Trust(library).LoadOffline();
            var choices = policy.Policy.RecoveryDevices.Where(d => d.Enabled).Select(d =>
                new GroupPrivateBackupTarget($"{d.Name} · {d.Fingerprint}", null, (bytes, phrase) => RestoreIdentity(library, d.Fingerprint, bytes, phrase))).ToArray();
            if (choices.Length == 0) throw new InvalidOperationException("Noch kein aktiver Recovery-Zugang registriert. Einladung zentral freigeben lassen und den neuen Gruppenstand importieren.");
            new GroupPrivateBackupWindow(this, choices, true, "Recovery-Schlüssel").ShowDialog();
        });
        root.Children.Add(restoreIdentity);
        void RefreshAvailability()
        {
            var selected = libraries.SelectedItem is Guid;
            createBackup.IsEnabled = selected; restoreIdentity.IsEnabled = selected;
            setupHint.Visibility = selected ? Visibility.Collapsed : Visibility.Visible;
        }
        libraries.SelectionChanged += (_, _) => RefreshAvailability();
        RefreshAvailability();
        root.Children.Add(status); root.Children.Add(Button("Schließen", Close));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Closing += (_, e) =>
        {
            if (IsVisible && invitation is not null && !exported && MessageBox.Show(this, "Die private Sicherung ist gespeichert, die öffentliche Einladung aber noch nicht exportiert. Trotzdem schließen?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
        };
        Closed += (_, _) => { password.Clear(); repeat.Clear(); };
        MainWindow.InitializeAutomationContract(this, "GroupRecoverySetup");
    }

    internal void CreateBackup(Guid library, string displayName, string path, string phrase, string repeated, bool confirmation)
    {
        if (!confirmation || phrase != repeated) throw new InvalidDataException("Bestätigung fehlt oder Passphrasen stimmen nicht überein.");
        Trust(library).LoadOffline();
        if (ConcurrentFileWriter.Capture(path) != FileRevision.Missing) throw new IOException("Sicherungsdatei existiert bereits; neuen Dateinamen wählen.");
        var created = GroupRecoveryIdentity.Create(library, displayName, phrase);
        ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary => File.WriteAllBytes(temporary, created.PrivateBackup));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != created.PrivateBackup.Length) throw new IOException("Sicherungsdatei konnte nicht geprüft werden.");
        var bytes = new byte[created.PrivateBackup.Length]; stream.ReadExactly(bytes);
        if (!bytes.SequenceEqual(created.PrivateBackup)) throw new IOException("Sicherungsdatei wurde verändert.");
        using var restored = GroupPrivateKeyBackups.Restore(bytes, phrase, new(library, GroupPrivateKeyBackups.Recovery), created.Invitation.Fingerprint);
        invitation = created.Invitation; invitationLibrary = library; exported = false;
        status.Text = $"Private Sicherung gespeichert und wiederhergestellt geprüft. Noch kein Windows-Schlüssel installiert und keine Freigabe erteilt.\nBibliothek: {library:D}\nFingerabdruck: {invitation.Fingerprint}\nJetzt öffentliche Einladung exportieren.";
    }

    internal void ExportInvitation(string path)
    {
        if (invitation is null) throw new InvalidOperationException("Noch keine gesicherte Recovery-Identität vorhanden.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(invitation, ProjectJson.Options);
        ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary => File.WriteAllBytes(temporary, bytes));
        exported = true;
        status.Text = $"Nur diese öffentliche Einladung an die Hauptverwaltung geben. Bibliothek {invitationLibrary:D} und Fingerabdruck unabhängig bestätigen:\n{invitation.Fingerprint}\nDanach den freigegebenen Gruppenstand importieren. Private Sicherung und Passphrase getrennt verwahren.";
    }

    internal void RestoreIdentity(Guid library, string fingerprint, byte[] backup, string phrase)
    {
        var trust = Trust(library);
        var checkpoint = trust.LoadOffline().Checkpoint;
        using var identity = GroupRecoveryIdentity.Restore(backup, phrase, trust, fingerprint);
        if (trust.LoadOffline().Checkpoint != checkpoint) throw new CryptographicException("Gruppenstand wurde geändert. Bitte neu laden.");
        install(identity);
    }
    private OfflineGroupTrustStore Trust(Guid id) => new(Path.Combine(directory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
    private Guid SelectedLibrary() => libraries.SelectedItem is Guid id ? id : throw new InvalidOperationException("Bitte eine registrierte Bibliothek auswählen.");
    private Button Button(string text, Action action)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => { try { status.Foreground = Brushes.Black; action(); } catch (Exception e) { status.Foreground = Brushes.Firebrick; status.Text = e.Message; } };
        return button;
    }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

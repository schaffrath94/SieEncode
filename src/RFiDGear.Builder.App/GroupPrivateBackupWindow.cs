using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed record GroupPrivateBackupTarget(string Label, Func<string, byte[]>? Export, Action<byte[], string>? Restore);

internal sealed class GroupPrivateBackupWindow : Window
{
    private readonly ComboBox targets = new() { DisplayMemberPath = nameof(GroupPrivateBackupTarget.Label) };
    private readonly PasswordBox password = new();
    private readonly PasswordBox repeat = new();
    private readonly CheckBox confirmation = new() { Content = new TextBlock { Text = "Ich verwahre Datei und Passphrase getrennt und gebe diese private Sicherung nicht als Einladung weiter.", TextWrapping = TextWrapping.Wrap } };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly bool restore;
    internal bool ExportCompleted { get; private set; }
    private const string Filter = "Private Schlüsselsicherung (*.group-private.json)|*.group-private.json";

    internal GroupPrivateBackupWindow(Window owner, IEnumerable<GroupPrivateBackupTarget> choices, bool restore, string keyLabel = "Verwaltungsschlüssel")
    {
        Owner = owner; this.restore = restore;
        Title = restore ? $"Privaten {keyLabel} wiederherstellen" : $"Privaten {keyLabel} sichern";
        Width = 720; SizeToContent = SizeToContent.Height; MaxHeight = 780; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Note(restore
            ? "Wählen Sie die bereits registrierte Bibliothek bzw. Vollmacht. Die Sicherung muss genau dazu passen. Vorhandene lokale Schlüssel bleiben erhalten. Für eine neue Windows-Umgebung zuerst die öffentliche Registrierung mit unabhängig bestätigtem Fingerabdruck einrichten."
            : "Diese Datei enthält Ihren privaten Verwaltungsschlüssel, verschlüsselt mit einer eigenen Passphrase. Sie ist keine öffentliche Einladung. Außerhalb des gemeinsamen Projektordners aufbewahren; die Passphrase getrennt, beispielsweise in einem Passwortmanager."));
        targets.ItemsSource = choices.ToArray(); targets.SelectedIndex = targets.Items.Count == 1 ? 0 : -1;
        root.Children.Add(targets);
        root.Children.Add(Note("Passphrase")); root.Children.Add(password);
        if (!restore) { root.Children.Add(Note("Passphrase wiederholen (mindestens 20 Zeichen, nicht gleichförmig)")); root.Children.Add(repeat); }
        else confirmation.Content = new TextBlock { Text = "Ich möchte den privaten Schlüssel für die ausgewählte registrierte Bibliothek/Vollmacht auf diesem Windows-Benutzer wiederherstellen.", TextWrapping = TextWrapping.Wrap };
        root.Children.Add(confirmation);
        var execute = new Button { Content = restore ? "Sicherungsdatei wählen und wiederherstellen" : "Verschlüsselte Sicherung speichern" };
        execute.Click += (_, _) =>
        {
            try
            {
                status.Foreground = Brushes.Black;
                if (restore)
                {
                    var dialog = new OpenFileDialog { Filter = Filter, Title = Title };
                    if (dialog.ShowDialog(this) == true) RestoreFrom(dialog.FileName, password.Password, confirmation.IsChecked == true);
                }
                else
                {
                    var dialog = new SaveFileDialog { Filter = Filter, FileName = "verwaltung.group-private.json", Title = Title };
                    if (dialog.ShowDialog(this) == true) ExportTo(dialog.FileName, password.Password, repeat.Password, confirmation.IsChecked == true);
                }
            }
            catch (Exception e) { status.Foreground = Brushes.Firebrick; status.Text = e.Message; }
            finally { password.Clear(); repeat.Clear(); }
        };
        root.Children.Add(execute); root.Children.Add(status);
        var close = new Button { Content = "Schließen" }; close.Click += (_, _) => Close(); root.Children.Add(close);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Closed += (_, _) => { password.Clear(); repeat.Clear(); };
        MainWindow.InitializeAutomationContract(this, "GroupPrivateBackup");
    }

    internal void ExportTo(string path, string passphrase, string repeated, bool confirmed)
    {
        ExportCompleted = false;
        if (restore || targets.SelectedItem is not GroupPrivateBackupTarget { Export: not null } target)
            throw new InvalidOperationException("Bitte einen lokalen Verwaltungsschlüssel auswählen.");
        if (!confirmed || passphrase != repeated) throw new InvalidDataException("Bestätigung fehlt oder Passphrasen stimmen nicht überein.");
        var bytes = target.Export(passphrase);
        ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary => File.WriteAllBytes(temporary, bytes));
        if (!ReadBackup(path).SequenceEqual(bytes)) throw new IOException("Die gespeicherte Sicherung konnte nicht unverändert zurückgelesen werden.");
        ExportCompleted = true;
        status.Text = "Private Sicherung gespeichert und unverändert zurückgelesen. Die Schlüsselfunktion wurde beim Export nach Entschlüsselung geprüft. Datei und Passphrase getrennt verwahren.";
    }

    internal void RestoreFrom(string path, string passphrase, bool confirmed)
    {
        if (!restore || targets.SelectedItem is not GroupPrivateBackupTarget { Restore: not null } target)
            throw new InvalidOperationException("Bitte eine registrierte Bibliothek/Vollmacht auswählen.");
        if (!confirmed) throw new InvalidDataException("Wiederherstellung muss ausdrücklich bestätigt werden.");
        target.Restore(ReadBackup(path), passphrase);
        status.Text = "Privater Schlüssel passend zum registrierten Stand auf diesem Windows-Benutzer wiederhergestellt. Die Sicherungsdatei bleibt erhalten. Sie können den Dialog schließen.";
    }

    private static byte[] ReadBackup(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GroupPrivateKeyBackups.MaximumBytes) throw new InvalidDataException("Private Sicherungsdatei ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}

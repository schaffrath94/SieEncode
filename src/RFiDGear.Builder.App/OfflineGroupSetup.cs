using System.IO;
using System.Windows;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private readonly string localGroupTrustDirectory;

    private void RegisterOfflineGroupAdministration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog { Title = "Öffentliche Gruppen-Registrierungsdatei wählen", Filter = "Gruppenregistrierung (*.group-enrollment.json)|*.group-enrollment.json" };
            if (dialog.ShowDialog(this) != true) return;
            using var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > GroupEnrollmentPackages.MaximumBytes) throw new InvalidDataException("Registrierungsdatei ist leer oder zu groß.");
            var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
            var package = GroupEnrollmentPackages.Read(bytes);
            if (new GroupEnrollmentWindow(this, package, localGroupTrustDirectory).ShowDialog() == true)
                SetStatus("Gruppenverwaltung für diesen Windows-Benutzer registriert. Projekt-Vaults wurden nicht verändert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

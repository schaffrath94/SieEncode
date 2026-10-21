using System.IO;
using System.Windows;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private readonly string localGroupTrustDirectory;

    private void ImportAdministratorTransition_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog { Title = "Öffentlichen Verwaltungswechsel wählen", Filter = "Verwaltungswechsel (*.group-transition.json)|*.group-transition.json" };
            if (dialog.ShowDialog(this) != true) return;
            using var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > GroupAdministratorTransition.MaximumBytes) throw new InvalidDataException("Verwaltungswechseldatei ist leer oder zu groß.");
            var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
            var transition = GroupAdministratorTransition.Read(bytes);
            if (new GroupAdministratorTransitionWindow(this, transition, localGroupTrustDirectory).ShowDialog() == true)
                SetStatus("Neuer Verwaltungsschlüssel lokal übernommen. Andere Arbeitsplätze und private Verwaltungsschlüssel wurden nicht geändert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenGroupRecoverySetup_Click(object sender, RoutedEventArgs e)
    {
        try { new GroupRecoverySetupWindow(this, localGroupTrustDirectory).ShowDialog(); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenGroupDelegate_Click(object sender, RoutedEventArgs e)
    {
        try { new GroupDelegateWindow(this, localGroupTrustDirectory).ShowDialog(); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void UpdateOfflineGroups_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog { Title = "Neuen signierten Gruppenstand wählen", Filter = "Gruppenregistrierung (*.group-enrollment.json)|*.group-enrollment.json" };
            if (dialog.ShowDialog(this) != true) return;
            var package = GroupEnrollmentPackages.Read(GroupDelegationTransport.ReadFile(dialog.FileName));
            if (new GroupPolicyUpdateWindow(this, package, localGroupTrustDirectory).ShowDialog() == true)
                SetStatus("Registrierter Gruppenstand aktualisiert. Bestehende Vault-Dateien wurden nicht verändert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ManageOfflineGroups_Click(object sender, RoutedEventArgs e)
    {
        try { new GroupAdministrationWindow(this, localGroupTrustDirectory).ShowDialog(); }
        catch (Exception exception) { ShowError(exception); }
    }

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

using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    private string OwnTrustPath(Guid id) => Path.Combine(Directory.GetParent(localDirectory)!.FullName, id.ToString("N") + ".trust.bin");
    private OfflineGroupTrustStore OwnTrust(Guid id) => new(OwnTrustPath(id), id, new WindowsGroupTrustProtection(id));

    private string SynchronizeOwnRegistration(GroupEnrollmentPackage package, bool allowInitialEnrollment = false)
    {
        var trust = OwnTrust(package.LibraryId);
        if (!File.Exists(OwnTrustPath(package.LibraryId)))
        {
            if (!allowInitialEnrollment) return "Auf diesem Windows-Benutzer noch nicht registriert. ‚Eigene Verwaltung lokal registrieren‘ verwenden.";
            // This package comes from the locally held signing authority, never from an imported public file.
            trust.Enroll(package.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey), package.Policy);
            return "Eigene Verwaltung auf diesem Windows-Benutzer registriert.";
        }
        var current = trust.LoadOffline();
        var next = trust.PreviewPackageUpdate(package, current.Checkpoint); // Checks pinned key even for an unchanged revision.
        if (next.Checkpoint != current.Checkpoint) trust.AcceptPackageUpdate(package, current.Checkpoint);
        return "Eigener signierter Stand ist auf diesem Windows-Benutzer aktiv. Änderungen für andere Benutzer weiterhin öffentlich exportieren und dort importieren.";
    }

    internal void RegisterOwnAdministration(bool confirmed)
    {
        if (!confirmed) throw new InvalidDataException("Lokale Registrierung muss bestätigt werden.");
        RequireSavedAdministration();
        status.Text = SynchronizeOwnRegistration(store!.ExportPublic(), allowInitialEnrollment: true);
    }

    private void ShowOwnRegistration()
    {
        RequireSavedAdministration();
        var package = store!.ExportPublic();
        if (MessageBox.Show(this, $"Die hier privat verwaltete Bibliothek {package.LibraryId:D} auf diesem Windows-Benutzer registrieren?\nVerwaltungsfingerabdruck: {OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey)}\n\nBestehendes fremdes Vertrauen wird nicht ersetzt. Registrierung vergibt keine Gruppenmitgliedschaft.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            RegisterOwnAdministration(true);
    }
}

using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyPrivateBackupDialog(string directory, Guid library, OfflineGroupAdministrationStore source, ICollection<string> checks)
    {
        const string password = "Synthetic UI private backup password 2026";
        var file = Path.Combine(directory, "ui.group-private.json");
        var export = new GroupPrivateBackupWindow(this, [new("Synthetic administration", source.ExportPrivateBackup, null)], false);
        try
        {
            VerifyAutomationContract(export);
            Reject(() => export.ExportTo(file, password, password, false));
            Reject(() => export.ExportTo(file, password, "mismatch", true));
            if (File.Exists(file)) throw new Exception("Unconfirmed private backup written.");
            export.ExportTo(file, password, password, true);
            var original = File.ReadAllBytes(file);
            Reject(() => export.ExportTo(file, password, password, true));
            if (!original.SequenceEqual(File.ReadAllBytes(file))) throw new Exception("Existing backup overwritten.");
        }
        finally { export.Close(); }
        var package = source.ExportPublic();
        var trust = new OfflineGroupTrustStore(Path.Combine(directory, "restore-ui.trust.bin"), library, new WindowsGroupTrustProtection(library));
        var current = trust.Enroll(package.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey), package.Policy);
        var targetPath = Path.Combine(directory, "restore-ui.admin.bin");
        var target = new OfflineGroupAdministrationStore(targetPath, library, new WindowsGroupTrustProtection(library, signingAuthority: true));
        var restore = new GroupPrivateBackupWindow(this, [new("Synthetic registered administration", null, (bytes, phrase) => target.RestorePrivateBackup(bytes, phrase, trust))], true);
        try
        {
            VerifyAutomationContract(restore);
            Reject(() => restore.RestoreFrom(file, password, false));
            Reject(() => restore.RestoreFrom(file, "wrong password", true));
            var invitationFile = Path.Combine(directory, "public.group-enrollment.json");
            File.WriteAllBytes(invitationFile, GroupEnrollmentPackages.Write(package));
            Reject(() => restore.RestoreFrom(invitationFile, password, true));
            if (File.Exists(targetPath)) throw new Exception("Rejected restore created private key.");
            restore.RestoreFrom(file, password, true);
            if (GroupEnrollmentPackages.Validate(target.ExportPublic()).Checkpoint != current.Checkpoint) throw new Exception("Restored stale administration.");
            var original = File.ReadAllBytes(targetPath);
            Reject(() => restore.RestoreFrom(file, password, true));
            if (!original.SequenceEqual(File.ReadAllBytes(targetPath))) throw new Exception("Existing administration overwritten.");
        }
        finally { restore.Close(); }
        checks.Add("Privater Sicherungsdialog: Bestätigung und Passphrasenvergleich, Dateirücklesen, falsches Passwort/öffentliche Registrierung abgewiesen, DPAPI-Wiederherstellung zum registrierten Stand und Überschreibschutz. Native Dateidialoge nicht automatisiert bedient.");
        static void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or System.Text.Json.JsonException) { return; }
            throw new Exception("Invalid private backup dialog operation succeeded.");
        }
    }
}

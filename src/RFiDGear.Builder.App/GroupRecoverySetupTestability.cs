using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyRecoverySetupDialog(string root, Guid library, OfflineGroupAdministrationStore administration, ICollection<string> checks)
    {
        const string phrase = "Synthetic recovery setup passphrase 2026";
        var directory = Path.Combine(root, "recovery-setup");
        var trust = new OfflineGroupTrustStore(Path.Combine(directory, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library));
        var package = administration.ExportPublic();
        trust.Enroll(package.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey), package.Policy);
        var installations = 0;
        var window = new GroupRecoverySetupWindow(this, directory, certificate =>
        {
            using var key = certificate.GetRSAPrivateKey()!;
            var challenge = RandomNumberGenerator.GetBytes(32);
            if (!key.Decrypt(key.Encrypt(challenge, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256).SequenceEqual(challenge)) throw new Exception("Restored recovery cannot decrypt.");
            installations++;
        });
        try
        {
            VerifyAutomationContract(window);
            var backup = Path.Combine(directory, "recovery.group-private.json");
            var invitationPath = Path.Combine(directory, "recovery.recipient.json");
            Reject(() => window.CreateBackup(library, "Synthetic recovery", backup, phrase, phrase, false));
            Reject(() => window.CreateBackup(library, "Synthetic recovery", backup, phrase, "wrong", true));
            if (File.Exists(backup)) throw new Exception("Unconfirmed recovery backup written.");
            window.CreateBackup(library, "Synthetic recovery", backup, phrase, phrase, true);
            var bytes = File.ReadAllBytes(backup);
            Reject(() => window.CreateBackup(library, "Synthetic recovery", backup, phrase, phrase, true));
            if (!bytes.SequenceEqual(File.ReadAllBytes(backup))) throw new Exception("Existing recovery backup overwritten.");
            window.ExportInvitation(invitationPath);
            var invitation = JsonSerializer.Deserialize<VaultRecipientInvitation>(File.ReadAllBytes(invitationPath), ProjectJson.Options)!;
            if (installations != 0) throw new Exception("Recovery generation installed a key.");
            Reject(() => window.RestoreIdentity(library, invitation.Fingerprint, bytes, phrase));
            var granted = administration.GrantRecovery(invitation, invitation.Fingerprint, trust.LoadOffline().Checkpoint);
            trust.AcceptUpdate(granted.Policy);
            Reject(() => window.RestoreIdentity(library, invitation.Fingerprint, bytes, "wrong passphrase"));
            if (installations != 0) throw new Exception("Rejected recovery restoration reached installer.");
            window.RestoreIdentity(library, invitation.Fingerprint, bytes, phrase);
            if (installations != 1) throw new Exception("Approved recovery restoration missing.");
            var policy = trust.LoadOffline();
            var revoked = administration.UpdateRecoveryDevices(policy.Policy.RecoveryDevices.Select(d => d with { Enabled = false }).ToArray(), policy.Checkpoint);
            trust.AcceptUpdate(revoked.Policy);
            Reject(() => window.RestoreIdentity(library, invitation.Fingerprint, bytes, phrase));
            if (installations != 1) throw new Exception("Revoked recovery reached installer.");
        }
        finally { window.Close(); }
        checks.Add("Recovery-Einrichtung: private Sicherung vor öffentlicher Einladung, kein Installieren beim Erzeugen; fehlende zentrale Freigabe, falsche Passphrase und Entzug sperren Wiederherstellung. Freigegebener Schlüssel entschlüsselt erfolgreich; Installer ersetzt, kein Zertifikatsspeichereingriff.");
        static void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException) { return; }
            throw new Exception("Invalid recovery setup operation succeeded.");
        }
    }
}

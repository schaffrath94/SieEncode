using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record GroupRecoveryIdentityBackup(byte[] PrivateBackup, VaultRecipientInvitation Invitation);

public static class GroupRecoveryIdentity
{
    public static GroupRecoveryIdentityBackup Create(Guid libraryId, string displayName, string passphrase)
    {
        if (libraryId == Guid.Empty) throw new ArgumentException("Bibliothek fehlt.");
        var name = displayName?.Trim();
        if (name is not { Length: >= 3 and <= 120 } || name.Any(char.IsControl)) throw new ArgumentException("Recovery-Name muss 3 bis 120 Zeichen lang sein.");
        KeyVaultService.ValidateNewPassphrase(passphrase);
        using var key = RSA.Create(3072);
        // User-supplied names are display metadata, never distinguished-name syntax.
        var request = new CertificateRequest("CN=SieEncode Separate Group Recovery", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
        var backup = GroupPrivateKeyBackups.Protect(key, new(libraryId, GroupPrivateKeyBackups.Recovery), passphrase, certificate);
        // Prove the recovery operation with a key restored from the encrypted backup.
        using var restored = GroupPrivateKeyBackups.Restore(backup, passphrase, new(libraryId, GroupPrivateKeyBackups.Recovery), CertificateVaultService.Fingerprint(certificate));
        VerifyDecryption(restored.Key);
        return new(backup, CertificateVaultService.ExportInvitation(certificate, name));
    }

    public static X509Certificate2 Restore(byte[] backup, string passphrase, OfflineGroupTrustStore trust, string fingerprint)
    {
        var current = trust.LoadOffline();
        if (!current.Policy.RecoveryDevices.Any(d => d.Enabled && d.Fingerprint == fingerprint))
            throw new CryptographicException("Dieser Recovery-Zugang ist im registrierten Stand nicht aktiv freigegeben.");
        using var restored = GroupPrivateKeyBackups.Restore(backup, passphrase, new(current.Policy.LibraryId, GroupPrivateKeyBackups.Recovery), fingerprint);
        VerifyDecryption(restored.Key);
        if (trust.LoadOffline().Checkpoint != current.Checkpoint) throw new CryptographicException("Registrierter Stand wurde während der Wiederherstellung geändert.");
        return restored.Certificate!.CopyWithPrivateKey(restored.Key);
    }

    private static void VerifyDecryption(RSA key)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        if (!key.Decrypt(key.Encrypt(challenge, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256).SequenceEqual(challenge))
            throw new CryptographicException("Recovery-Wiederherstellungsprüfung fehlgeschlagen.");
    }
}

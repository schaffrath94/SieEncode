using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record GroupPrivateKeyContext(Guid LibraryId, string Purpose, string PersonId = "");
public sealed class RestoredGroupPrivateKey(RSA key, X509Certificate2? certificate) : IDisposable
{
    public RSA Key { get; } = key;
    public X509Certificate2? Certificate { get; } = certificate;
    public void Dispose() { Certificate?.Dispose(); Key.Dispose(); }
}

/// <summary>Portable authenticated backup. Returned bytes contain encrypted private material, never public invitations.</summary>
public static class GroupPrivateKeyBackups
{
    public const string Administrator = "group-administrator";
    public const string Delegate = "group-delegate";
    public const string Recovery = "group-recovery";
    public const int MaximumBytes = 128 * 1024;
    private const string Format = "SieEncode-Private-Group-Key-Backup";
    private sealed record Envelope(string Format, int Version, KeyVaultDocument Encrypted);
    private sealed record Payload(string Format, int Version, GroupPrivateKeyContext Context, byte[] PrivateKey, byte[]? Certificate);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12 };

    public static byte[] Protect(RSA key, GroupPrivateKeyContext context, string passphrase, X509Certificate2? certificate = null)
    {
        ValidateContext(context);
        if (key.KeySize is < 3072 or > 16384) throw new CryptographicException("Ungültige private Schlüssellänge.");
        var publicCertificate = certificate?.RawData;
        var fingerprint = Fingerprint(key, context, publicCertificate);
        var privateKey = key.ExportPkcs8PrivateKey();
        try
        {
            var payload = JsonSerializer.Serialize(new Payload(Format, 1, context, privateKey, publicCertificate), Options);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, 1, KeyVaultService.Protect(payload, passphrase)), Options);
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Private Schlüsselsicherung ist zu groß.");
            // Check actual private-key operation after decrypting the backup, not merely its public metadata.
            using var restored = Restore(bytes, passphrase, context, fingerprint);
            var challenge = RandomNumberGenerator.GetBytes(32);
            var signature = restored.Key.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            if (!key.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new CryptographicException("Wiederherstellungsprüfung des privaten Schlüssels fehlgeschlagen.");
            return bytes;
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
    }

    public static RestoredGroupPrivateKey Restore(byte[] bytes, string passphrase, GroupPrivateKeyContext expected, string expectedFingerprint)
    {
        ValidateContext(expected);
        if (expectedFingerprint is not { Length: 64 } || !expectedFingerprint.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new CryptographicException("Unabhängig bestätigter Schlüsselfingerabdruck fehlt.");
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Private Schlüsselsicherung ist leer oder zu groß.");
        var snapshot = bytes.ToArray();
        using (var json = JsonDocument.Parse(snapshot, new JsonDocumentOptions { MaxDepth = 12 })) OfflineGroupPolicyService.RejectDuplicateProperties(json.RootElement);
        var envelope = JsonSerializer.Deserialize<Envelope>(snapshot, Options);
        if (envelope is null || envelope.Format != Format || envelope.Version != 1 || envelope.Encrypted is not { } vault ||
            vault.Version != KeyVaultService.CurrentVersion || vault.Kdf != "PBKDF2-SHA256" || vault.ProtectionMode != "passphrase" ||
            vault.Iterations < KeyVaultService.DefaultIterations || !KeyVaultService.IsStructurallyValid(vault) ||
            vault.Recipients is null || vault.Recipients.Count != 0 || vault.RecoveryCiphertext != "" || vault.RecoveryNonce != "" || vault.RecoveryTag != "")
            throw new InvalidDataException("Unbekanntes oder unzulässiges privates Sicherungsformat.");
        var plaintext = KeyVaultService.Unprotect(vault, passphrase);
        Payload? payload = null; RSA? key = null; X509Certificate2? certificate = null;
        try
        {
            using (var json = JsonDocument.Parse(plaintext, new JsonDocumentOptions { MaxDepth = 12 })) OfflineGroupPolicyService.RejectDuplicateProperties(json.RootElement);
            payload = JsonSerializer.Deserialize<Payload>(plaintext, Options);
            if (payload is null || payload.Format != Format || payload.Version != 1 || payload.Context != expected ||
                payload.PrivateKey is null || payload.PrivateKey.Length is 0 or > 16384)
                throw new CryptographicException("Private Sicherung passt nicht zu Bibliothek, Person oder Schlüsselzweck.");
            key = RSA.Create(); key.ImportPkcs8PrivateKey(payload.PrivateKey, out var read);
            if (read != payload.PrivateKey.Length || key.KeySize is < 3072 or > 16384 || Fingerprint(key, expected, payload.Certificate) != expectedFingerprint)
                throw new CryptographicException("Privater Schlüssel stimmt nicht mit der bestätigten Identität überein.");
            if (payload.Certificate is not null) certificate = OfflineGroupDirectory.ReadPublicCertificate(Convert.ToBase64String(payload.Certificate));
            var result = new RestoredGroupPrivateKey(key, certificate); key = null; certificate = null; return result;
        }
        finally
        {
            if (payload?.PrivateKey is not null) CryptographicOperations.ZeroMemory(payload.PrivateKey);
            certificate?.Dispose(); key?.Dispose();
        }
    }

    private static string Fingerprint(RSA key, GroupPrivateKeyContext context, byte[]? certificateBytes)
    {
        if (context.Purpose != Recovery)
        {
            if (certificateBytes is not null) throw new InvalidDataException("Signiersicherung darf kein Entschlüsselungszertifikat enthalten.");
            return OfflineGroupTrustStore.AdministratorFingerprint(key.ExportSubjectPublicKeyInfo());
        }
        if (certificateBytes is null || certificateBytes.Length is 0 or > 24576) throw new InvalidDataException("Öffentliches Recovery-Zertifikat fehlt.");
        using var certificate = OfflineGroupDirectory.ReadPublicCertificate(Convert.ToBase64String(certificateBytes));
        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey is null || !publicKey.ExportSubjectPublicKeyInfo().SequenceEqual(key.ExportSubjectPublicKeyInfo()))
            throw new CryptographicException("Recovery-Zertifikat passt nicht zum privaten Schlüssel.");
        return CertificateVaultService.Fingerprint(certificate);
    }

    private static void ValidateContext(GroupPrivateKeyContext context)
    {
        if (context is null || context.LibraryId == Guid.Empty || context.Purpose is not (Administrator or Delegate or Recovery) ||
            (context.Purpose == Delegate ? context.PersonId is not { Length: > 0 and <= 128 } || !context.PersonId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') : context.PersonId != ""))
            throw new InvalidDataException("Ungültiger privater Schlüsselkontext.");
    }
}

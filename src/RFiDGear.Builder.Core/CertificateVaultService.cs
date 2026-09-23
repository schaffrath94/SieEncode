using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class VaultRecipientInvitation
{
    public string Format { get; set; } = "SieEncode-Vault-Recipient";
    public int Version { get; set; } = 1;
    public string DisplayName { get; set; } = "";
    public string Certificate { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public static class CertificateVaultService
{
    public static KeyVaultDocument Protect(string payloadJson, string recoveryPassphrase, IEnumerable<(X509Certificate2 Certificate, string DisplayName)> recipients, int iterations = KeyVaultService.DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        KeyVaultService.ValidateNewPassphrase(recoveryPassphrase);
        using (JsonDocument.Parse(payloadJson)) { }
        var recipientList = recipients.ToArray();
        if (recipientList.Length == 0) throw new ArgumentException("Mindestens ein Zertifikatsempfänger ist erforderlich.", nameof(recipients));
        var ids = recipientList.Select(row => Fingerprint(row.Certificate)).ToArray();
        if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length) throw new ArgumentException("Ein Zertifikatsempfänger ist doppelt vorhanden.", nameof(recipients));

        var contentKey = RandomNumberGenerator.GetBytes(32);
        var payloadNonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(payloadJson);
        var cipher = new byte[plain.Length];
        var payloadTag = new byte[16];
        var salt = RandomNumberGenerator.GetBytes(16);
        var recoveryNonce = RandomNumberGenerator.GetBytes(12);
        var recoveryKey = Rfc2898DeriveBytes.Pbkdf2(recoveryPassphrase, salt, iterations, HashAlgorithmName.SHA256, 32);
        var recoveryCipher = new byte[contentKey.Length];
        var recoveryTag = new byte[16];
        try
        {
            using (var aes = new AesGcm(contentKey, payloadTag.Length))
                aes.Encrypt(payloadNonce, plain, cipher, payloadTag, PayloadAad());
            using (var aes = new AesGcm(recoveryKey, recoveryTag.Length))
                aes.Encrypt(recoveryNonce, contentKey, recoveryCipher, recoveryTag, RecoveryAad(iterations, salt));
            var wrapped = recipientList.Select((row, index) =>
            {
                ValidateRecipientCertificate(row.Certificate);
                using var rsa = row.Certificate.GetRSAPublicKey()!;
                return new KeyVaultRecipient
                {
                    Id = ids[index], DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Certificate.GetNameInfo(X509NameType.SimpleName, false) : row.DisplayName.Trim(),
                    Subject = row.Certificate.Subject, Certificate = Convert.ToBase64String(row.Certificate.Export(X509ContentType.Cert)), WrappedKey = Convert.ToBase64String(rsa.Encrypt(contentKey, RSAEncryptionPadding.OaepSHA256))
                };
            }).ToList();
            return new KeyVaultDocument
            {
                Version = KeyVaultService.CertificateVersion, ProtectionMode = "certificate-recipients+recovery", Kdf = "PBKDF2-SHA256", Iterations = iterations,
                Salt = Convert.ToBase64String(salt), InitializationVector = Convert.ToBase64String(payloadNonce), Ciphertext = Convert.ToBase64String(cipher), Mac = Convert.ToBase64String(payloadTag),
                RecoveryNonce = Convert.ToBase64String(recoveryNonce), RecoveryCiphertext = Convert.ToBase64String(recoveryCipher), RecoveryTag = Convert.ToBase64String(recoveryTag), Recipients = wrapped
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey); CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(cipher); CryptographicOperations.ZeroMemory(payloadTag);
            CryptographicOperations.ZeroMemory(recoveryKey); CryptographicOperations.ZeroMemory(recoveryCipher); CryptographicOperations.ZeroMemory(recoveryTag);
        }
    }

    public static string UnprotectWithCertificate(KeyVaultDocument vault, IEnumerable<X509Certificate2> certificates)
    {
        ValidateDocument(vault);
        foreach (var certificate in certificates.Where(certificate => certificate.HasPrivateKey))
        {
            var recipient = vault.Recipients.FirstOrDefault(row => string.Equals(row.Id, Fingerprint(certificate), StringComparison.OrdinalIgnoreCase));
            if (recipient is null) continue;
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is null) continue;
            byte[]? contentKey = null;
            try
            {
                contentKey = rsa.Decrypt(Convert.FromBase64String(recipient.WrappedKey), RSAEncryptionPadding.OaepSHA256);
                return DecryptPayload(vault, contentKey);
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
            { throw new CryptographicException("Die Windows-Identität passt zum Vault, konnte ihn aber nicht entschlüsseln.", exception); }
            finally { if (contentKey is not null) CryptographicOperations.ZeroMemory(contentKey); }
        }
        throw new CryptographicException("Für den angemeldeten Benutzer wurde kein freigegebenes Zertifikat mit privatem Schlüssel gefunden.");
    }

    public static string UnprotectWithRecovery(KeyVaultDocument vault, string passphrase)
    {
        ValidateDocument(vault); ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        var salt = Decode(vault.Salt); var nonce = Decode(vault.RecoveryNonce); var cipher = Decode(vault.RecoveryCiphertext); var tag = Decode(vault.RecoveryTag);
        if (salt.Length != 16 || nonce.Length != 12 || cipher.Length != 32 || tag.Length != 16) throw new CryptographicException("Recovery-Struktur des Vaults ist ungültig.");
        var recoveryKey = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, vault.Iterations, HashAlgorithmName.SHA256, 32);
        var contentKey = new byte[32];
        try
        {
            using (var aes = new AesGcm(recoveryKey, tag.Length)) aes.Decrypt(nonce, cipher, tag, contentKey, RecoveryAad(vault.Iterations, salt));
            return DecryptPayload(vault, contentKey);
        }
        catch (CryptographicException exception) { throw new CryptographicException("Recovery-Passphrase ist falsch oder der Vault wurde beschädigt.", exception); }
        finally { CryptographicOperations.ZeroMemory(recoveryKey); CryptographicOperations.ZeroMemory(contentKey); CryptographicOperations.ZeroMemory(cipher); CryptographicOperations.ZeroMemory(tag); }
    }

    public static KeyVaultDocument ReprotectWithCertificate(KeyVaultDocument vault, string payloadJson, IEnumerable<X509Certificate2> certificates)
    {
        ValidateDocument(vault); ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        using (JsonDocument.Parse(payloadJson)) { }
        foreach (var certificate in certificates.Where(certificate => certificate.HasPrivateKey))
        {
            var recipient = vault.Recipients.FirstOrDefault(row => string.Equals(row.Id, Fingerprint(certificate), StringComparison.OrdinalIgnoreCase));
            if (recipient is null) continue;
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is null) continue;
            byte[]? contentKey = null; byte[]? plain = null; byte[]? cipher = null; byte[]? tag = null;
            try
            {
                contentKey = rsa.Decrypt(Convert.FromBase64String(recipient.WrappedKey), RSAEncryptionPadding.OaepSHA256);
                _ = DecryptPayload(vault, contentKey); // authenticate the existing payload before preserving its recovery/recipient envelopes
                plain = Encoding.UTF8.GetBytes(payloadJson); cipher = new byte[plain.Length]; tag = new byte[16];
                var nonce = RandomNumberGenerator.GetBytes(12);
                using (var aes = new AesGcm(contentKey, tag.Length)) aes.Encrypt(nonce, plain, cipher, tag, PayloadAad());
                var updated = JsonSerializer.Deserialize<KeyVaultDocument>(JsonSerializer.Serialize(vault))
                    ?? throw new CryptographicException("Vault konnte nicht sicher aktualisiert werden.");
                updated.InitializationVector = Convert.ToBase64String(nonce);
                updated.Ciphertext = Convert.ToBase64String(cipher);
                updated.Mac = Convert.ToBase64String(tag);
                return updated;
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
            { throw new CryptographicException("Die Windows-Identität passt zum Vault, konnte ihn aber nicht sicher aktualisieren.", exception); }
            finally
            {
                if (contentKey is not null) CryptographicOperations.ZeroMemory(contentKey);
                if (plain is not null) CryptographicOperations.ZeroMemory(plain);
                if (cipher is not null) CryptographicOperations.ZeroMemory(cipher);
                if (tag is not null) CryptographicOperations.ZeroMemory(tag);
            }
        }
        throw new CryptographicException("Für den angemeldeten Benutzer wurde keine zum Vault passende Windows-Identität gefunden.");
    }

    public static VaultRecipientInvitation ExportInvitation(X509Certificate2 certificate, string displayName)
    {
        ValidateRecipientCertificate(certificate);
        return new VaultRecipientInvitation { DisplayName = displayName.Trim(), Certificate = Convert.ToBase64String(certificate.Export(X509ContentType.Cert)), Fingerprint = Fingerprint(certificate) };
    }

    public static (X509Certificate2 Certificate, string DisplayName) ImportInvitation(VaultRecipientInvitation invitation)
    {
        if (invitation.Format != "SieEncode-Vault-Recipient" || invitation.Version != 1) throw new InvalidDataException("Unbekanntes Empfängerformat.");
        if (string.IsNullOrWhiteSpace(invitation.Certificate) || invitation.Certificate.Length > 32768)
            throw new InvalidDataException("Empfängerzertifikat fehlt oder ist zu groß.");
        var bytes = Convert.FromBase64String(invitation.Certificate);
        if (X509Certificate2.GetCertContentType(bytes) != X509ContentType.Cert)
            throw new CryptographicException("Einladungen dürfen nur öffentliche Zertifikate enthalten, keine Schlüsselarchive.");
        var certificate = new X509Certificate2(bytes);
        try
        {
            ValidateRecipientCertificate(certificate);
            if (!certificate.RawData.SequenceEqual(bytes) || certificate.HasPrivateKey)
                throw new CryptographicException("Einladung enthält kein eindeutiges öffentliches DER-Zertifikat.");
            if (!string.Equals(Fingerprint(certificate), invitation.Fingerprint, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("Der Fingerabdruck der Empfängerdatei stimmt nicht.");
            return (certificate, invitation.DisplayName);
        }
        catch { certificate.Dispose(); throw; }
    }

    public static string Fingerprint(X509Certificate2 certificate) => Convert.ToHexString(SHA256.HashData(certificate.RawData));

    private static string DecryptPayload(KeyVaultDocument vault, byte[] contentKey)
    {
        var nonce = Decode(vault.InitializationVector); var cipher = Decode(vault.Ciphertext); var tag = Decode(vault.Mac);
        if (contentKey.Length != 32 || nonce.Length != 12 || cipher.Length == 0 || tag.Length != 16) throw new CryptographicException("Vault-Struktur ist ungültig.");
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(contentKey, tag.Length); aes.Decrypt(nonce, cipher, tag, plain, PayloadAad());
            var json = Encoding.UTF8.GetString(plain); using (JsonDocument.Parse(json)) { } return json;
        }
        finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(cipher); CryptographicOperations.ZeroMemory(tag); }
    }

    private static void ValidateDocument(KeyVaultDocument vault)
    {
        if (vault.Format != KeyVaultService.Format || vault.Version != KeyVaultService.CertificateVersion || vault.ProtectionMode != "certificate-recipients+recovery" || vault.Kdf != "PBKDF2-SHA256" || vault.Recipients.Count == 0)
            throw new CryptographicException("Unbekanntes Zertifikats-Vault-Format.");
    }

    private static void ValidateRecipientCertificate(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize < 3072) throw new CryptographicException("Empfängerzertifikat benötigt einen RSA-Schlüssel mit mindestens 3072 Bit.");
        if (certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) throw new CryptographicException("Empfängerzertifikat ist abgelaufen.");
    }

    private static byte[] PayloadAad() => Encoding.UTF8.GetBytes($"{KeyVaultService.Format}|3|certificate-recipients+recovery|AES-256-GCM");
    private static byte[] RecoveryAad(int iterations, byte[] salt) => Encoding.UTF8.GetBytes($"{KeyVaultService.Format}|3|recovery|PBKDF2-SHA256|{iterations}|{Convert.ToBase64String(salt)}");
    private static byte[] Decode(string value) { try { return Convert.FromBase64String(value); } catch (FormatException exception) { throw new CryptographicException("Vault-Encoding ist ungültig.", exception); } }
}

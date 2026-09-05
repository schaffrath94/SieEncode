using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed class KeyVaultDocument
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = KeyVaultService.Format;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("kdf")]
    public string Kdf { get; set; } = "PBKDF2";

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("iv")]
    public string InitializationVector { get; set; } = "";

    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; set; } = "";

    [JsonPropertyName("mac")]
    public string Mac { get; set; } = "";

    [JsonPropertyName("protectionMode")]
    public string ProtectionMode { get; set; } = "passphrase";

    [JsonPropertyName("recoveryNonce")]
    public string RecoveryNonce { get; set; } = "";

    [JsonPropertyName("recoveryCiphertext")]
    public string RecoveryCiphertext { get; set; } = "";

    [JsonPropertyName("recoveryTag")]
    public string RecoveryTag { get; set; } = "";

    [JsonPropertyName("recipients")]
    public List<KeyVaultRecipient> Recipients { get; set; } = [];
}

public sealed class KeyVaultRecipient
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "RSA-OAEP-SHA256";
    [JsonPropertyName("wrappedKey")]
    public string WrappedKey { get; set; } = "";
    [JsonPropertyName("certificate")]
    public string Certificate { get; set; } = "";
}

public static class KeyVaultService
{
    public const string Format = "RFiDGear-Projektbuilder-KeyVault";
    public const int CurrentVersion = 2;
    public const int CertificateVersion = 3;
    public const int DefaultIterations = 600_000;
    public const int MinimumNewPassphraseLength = 20;
    private const int MinimumIterations = 100_000;
    private const int MaximumIterations = 5_000_000;

    public static string GenerateKey(string keyType)
    {
        var byteCount = keyType switch
        {
            "DF_KEY_DES" or "DF_KEY_AES" => 16,
            "DF_KEY_3K3DES" => 24,
            _ => throw new ArgumentOutOfRangeException(nameof(keyType), keyType, "Unbekannter Keytyp.")
        };
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount));
    }

    public static KeyVaultDocument Protect(string payloadJson, string passphrase, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ValidateNewPassphrase(passphrase);
        ValidateIterations(iterations);
        using (JsonDocument.Parse(payloadJson)) { }

        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(payloadJson);
        var encryptionKey = Derive(passphrase, salt, iterations, HashAlgorithmName.SHA256, 32);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(encryptionKey, tag.Length);
            aes.Encrypt(nonce, plain, cipher, tag, AssociatedData(CurrentVersion, iterations, salt));
            return new KeyVaultDocument
            {
                Version = CurrentVersion,
                Kdf = "PBKDF2-SHA256",
                Iterations = iterations,
                Salt = Convert.ToBase64String(salt),
                InitializationVector = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(cipher),
                Mac = Convert.ToBase64String(tag)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(cipher);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    // Ausschließlich für den automatischen v10.39-Kompatibilitätsnachweis.
    // Neue Anwendungs-Vaults dürfen dieses Format nicht mehr verwenden.
    public static KeyVaultDocument ProtectLegacyV1(string payloadJson, string passphrase, int iterations = 250_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        ValidateIterations(iterations);
        using (JsonDocument.Parse(payloadJson)) { }
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);
        var plain = Encoding.UTF8.GetBytes(payloadJson);
        var material = Derive(passphrase, salt, iterations, HashAlgorithmName.SHA1, 64);
        var encryptionKey = material[..32];
        var macKey = material[32..];
        byte[]? cipher = null;
        byte[]? mac = null;
        try
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using (var encryptor = aes.CreateEncryptor()) cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            mac = ComputeLegacyMac(macKey, salt, iv, cipher);
            return new KeyVaultDocument { Version = 1, Kdf = "PBKDF2", Iterations = iterations, Salt = Convert.ToBase64String(salt), InitializationVector = Convert.ToBase64String(iv), Ciphertext = Convert.ToBase64String(cipher), Mac = Convert.ToBase64String(mac) };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(material); CryptographicOperations.ZeroMemory(encryptionKey); CryptographicOperations.ZeroMemory(macKey);
            if (cipher is not null) CryptographicOperations.ZeroMemory(cipher); if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }

    public static string Unprotect(KeyVaultDocument vault, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(vault);
        if (vault.Format != Format)
            throw new CryptographicException("Unbekanntes Key-Vault-Format.");
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        ValidateIterations(vault.Iterations);
        return vault.Version switch
        {
            1 when vault.Kdf == "PBKDF2" => UnprotectLegacyV1(vault, passphrase),
            CurrentVersion when vault.Kdf == "PBKDF2-SHA256" => UnprotectV2(vault, passphrase),
            CertificateVersion when vault.Kdf == "PBKDF2-SHA256" && vault.ProtectionMode == "certificate-recipients+recovery" => CertificateVaultService.UnprotectWithRecovery(vault, passphrase),
            _ => throw new CryptographicException("Unbekannte Key-Vault-Version oder Schlüsselableitung.")
        };
    }

    public static void ValidateNewPassphrase(string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        if (passphrase.Length < MinimumNewPassphraseLength)
            throw new ArgumentException($"Die Vault-Passphrase muss mindestens {MinimumNewPassphraseLength} Zeichen lang sein.", nameof(passphrase));
        if (passphrase.Distinct().Count() < 8)
            throw new ArgumentException("Die Vault-Passphrase ist zu gleichförmig. Bitte eine zufällige KeePass-Passphrase oder mehrere unabhängige Wörter verwenden.", nameof(passphrase));
    }

    public static bool IsStructurallyValid(KeyVaultDocument vault)
    {
        if (vault.Format != Format || vault.Iterations is < MinimumIterations or > MaximumIterations || string.IsNullOrWhiteSpace(vault.Salt) || string.IsNullOrWhiteSpace(vault.InitializationVector) || string.IsNullOrWhiteSpace(vault.Ciphertext) || string.IsNullOrWhiteSpace(vault.Mac)) return false;
        return (vault.Version == 1 && vault.Kdf == "PBKDF2") || (vault.Version == CurrentVersion && vault.Kdf == "PBKDF2-SHA256") ||
               (vault.Version == CertificateVersion && vault.Kdf == "PBKDF2-SHA256" && vault.ProtectionMode == "certificate-recipients+recovery" && vault.Recipients.Count > 0 &&
                !string.IsNullOrWhiteSpace(vault.RecoveryNonce) && !string.IsNullOrWhiteSpace(vault.RecoveryCiphertext) && !string.IsNullOrWhiteSpace(vault.RecoveryTag));
    }

    private static string UnprotectV2(KeyVaultDocument vault, string passphrase)
    {
        byte[] salt; byte[] nonce; byte[] cipher; byte[] tag;
        try
        {
            salt = Convert.FromBase64String(vault.Salt); nonce = Convert.FromBase64String(vault.InitializationVector);
            cipher = Convert.FromBase64String(vault.Ciphertext); tag = Convert.FromBase64String(vault.Mac);
        }
        catch (FormatException exception) { throw new CryptographicException("Key-Vault-Encoding ist ungültig.", exception); }
        if (salt.Length != 16 || nonce.Length != 12 || cipher.Length == 0 || tag.Length != 16) throw new CryptographicException("Key-Vault-Struktur ist ungültig.");
        var key = Derive(passphrase, salt, vault.Iterations, HashAlgorithmName.SHA256, 32);
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(vault.Version, vault.Iterations, salt));
            var json = Encoding.UTF8.GetString(plain); using (JsonDocument.Parse(json)) { } return json;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        { throw new CryptographicException("Passphrase ist falsch oder der Key-Vault wurde beschädigt.", exception); }
        finally
        { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(cipher); CryptographicOperations.ZeroMemory(tag); }
    }

    private static string UnprotectLegacyV1(KeyVaultDocument vault, string passphrase)
    {

        byte[] salt;
        byte[] initializationVector;
        byte[] cipher;
        byte[] expectedMac;
        try
        {
            salt = Convert.FromBase64String(vault.Salt);
            initializationVector = Convert.FromBase64String(vault.InitializationVector);
            cipher = Convert.FromBase64String(vault.Ciphertext);
            expectedMac = Convert.FromBase64String(vault.Mac);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Key-Vault-Encoding ist ungültig.", exception);
        }

        if (salt.Length != 16 || initializationVector.Length != 16 || cipher.Length < 16 || cipher.Length % 16 != 0 || expectedMac.Length != 32)
            throw new CryptographicException("Key-Vault-Struktur ist ungültig.");

        var material = Derive(passphrase, salt, vault.Iterations, HashAlgorithmName.SHA1, 64);
        var encryptionKey = material[..32];
        var macKey = material[32..];
        byte[]? actualMac = null;
        byte[]? plain = null;
        try
        {
            actualMac = ComputeLegacyMac(macKey, salt, initializationVector, cipher);
            if (!CryptographicOperations.FixedTimeEquals(actualMac, expectedMac))
                throw new CryptographicException("Passphrase ist falsch oder der Key-Vault wurde beschädigt.");

            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.IV = initializationVector;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            try
            {
                using var decryptor = aes.CreateDecryptor();
                plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                var json = Encoding.UTF8.GetString(plain);
                using (JsonDocument.Parse(json)) { }
                return json;
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                throw new CryptographicException("Passphrase ist falsch oder der Key-Vault wurde beschädigt.", exception);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(macKey);
            CryptographicOperations.ZeroMemory(cipher);
            CryptographicOperations.ZeroMemory(expectedMac);
            if (actualMac is not null) CryptographicOperations.ZeroMemory(actualMac);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static byte[] Derive(string passphrase, byte[] salt, int iterations, HashAlgorithmName hash, int length)
        => Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, hash, length);

    private static byte[] ComputeLegacyMac(byte[] macKey, byte[] salt, byte[] initializationVector, byte[] cipher)
    {
        var prefix = Encoding.UTF8.GetBytes("1|");
        var authenticated = new byte[prefix.Length + salt.Length + initializationVector.Length + cipher.Length];
        try
        {
            var offset = 0;
            Buffer.BlockCopy(prefix, 0, authenticated, offset, prefix.Length); offset += prefix.Length;
            Buffer.BlockCopy(salt, 0, authenticated, offset, salt.Length); offset += salt.Length;
            Buffer.BlockCopy(initializationVector, 0, authenticated, offset, initializationVector.Length); offset += initializationVector.Length;
            Buffer.BlockCopy(cipher, 0, authenticated, offset, cipher.Length);
            return HMACSHA256.HashData(macKey, authenticated);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prefix);
            CryptographicOperations.ZeroMemory(authenticated);
        }
    }

    private static byte[] AssociatedData(int version, int iterations, byte[] salt)
        => Encoding.UTF8.GetBytes($"{Format}|{version}|PBKDF2-SHA256|{iterations}|{Convert.ToBase64String(salt)}");

    private static void ValidateIterations(int iterations)
    {
        if (iterations is < MinimumIterations or > MaximumIterations)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Vault-Iterationszahl liegt außerhalb des unterstützten Bereichs.");
    }
}

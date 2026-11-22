using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Operator-confirmed network endpoint for one library, protected at rest by
/// the existing Windows-user trust protector. This is local configuration,
/// not the central authority and not an automatically trusted server.
/// </summary>
public sealed record KeyLibrarySyncEndpoint(
    Guid LibraryId, string HttpsOrigin, string ServerCertificateSha256);

public sealed class KeyLibrarySyncEndpointStore(
    string filePath, Guid libraryId, ILocalGroupTrustProtection protection)
{
    private readonly string path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
    private const int MaxBytes = 8192;

    public static string DefaultPath(Guid libraryId)
    {
        if (libraryId == Guid.Empty)
            throw new InvalidDataException("Bibliotheks-ID fehlt.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            throw new InvalidOperationException("Lokales Windows-Benutzerverzeichnis nicht verfügbar.");
        return Path.Combine(local, "SieEncode", "KeyLibrary",
            libraryId.ToString("N"), "sync-endpoint.bin");
    }

    public static KeyLibrarySyncEndpoint Validate(KeyLibrarySyncEndpoint settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.LibraryId == Guid.Empty ||
            settings.ServerCertificateSha256 is not { Length: 64 } ||
            !settings.ServerCertificateSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
            !Uri.TryCreate(settings.HttpsOrigin, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/" ||
            uri.Port is < 1 or > 65535)
            throw new InvalidDataException("Ungültige HTTPS-Adresse oder nicht bestätigter SHA-256-Fingerabdruck.");
        return settings;
    }

    public KeyLibrarySyncEndpoint? Load()
    {
        if (!File.Exists(path)) return null;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Geschützte Zentralverbindung hat ungültige Dateigröße.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        var plaintext = protection.Unprotect(bytes);
        try
        {
            using (var document = JsonDocument.Parse(plaintext,
                new JsonDocumentOptions { MaxDepth = 8 }))
                OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
            var result = JsonSerializer.Deserialize<KeyLibrarySyncEndpoint>(plaintext, Options)
                ?? throw new InvalidDataException("Geschützte Zentralverbindung fehlt.");
            Validate(result);
            if (result.LibraryId != libraryId)
                throw new CryptographicException("Zentrale ist für eine andere Bibliothek registriert.");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    /// <summary>Caller must independently confirm the server certificate first.</summary>
    public FileRevision Save(KeyLibrarySyncEndpoint settings)
    {
        Validate(settings);
        if (settings.LibraryId != libraryId)
            throw new CryptographicException("Zentralverbindung gehört nicht zu dieser Bibliothek.");
        var expected = ConcurrentFileWriter.Capture(path);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
        try
        {
            var protectedBytes = protection.Protect(plaintext);
            if (protectedBytes.Length is <= 0 or > MaxBytes)
                throw new InvalidDataException("Geschützte Zentralverbindung ist zu groß.");
            return ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var file = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                file.Write(protectedBytes);
                file.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}

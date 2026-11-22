using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// User-profile-bound observation of the last accepted central revision.
/// Protects against accidental server backup rollback between sync runs.
/// Not a substitute for an independently anchored, server-signed receipt.
/// </summary>
public sealed class CentralKeyLibraryCursorStore(
    string filePath, Guid libraryId, ILocalGroupTrustProtection protection)
{
    private sealed record Cursor(string Format, int Version, Guid LibraryId,
        CentralKeyLibraryHead Head, DateTimeOffset ObservedAt);
    private const string Format = "SieEncode-Client-Sync-Cursor";
    private const int MaximumBytes = 8192;
    private readonly string path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static string DefaultPath(Guid libraryId)
    {
        if (libraryId == Guid.Empty) throw new InvalidDataException("Bibliotheks-ID fehlt.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            throw new InvalidOperationException("Geschütztes Windows-Benutzerverzeichnis fehlt.");
        return Path.Combine(local, "SieEncode", "KeyLibrary",
            libraryId.ToString("N"), "sync-cursor.bin");
    }

    public void RequireNotRolledBack(CentralKeyLibraryHead observed)
    {
        Validate(observed);
        var stored = Read().State;
        if (stored is not null) CheckTransition(stored.Head, observed);
    }

    public void Remember(CentralKeyLibraryHead confirmed)
    {
        Validate(confirmed);
        var (old, expected) = Read();
        if (old is not null) CheckTransition(old.Head, confirmed);
        var updated = new Cursor(Format, 1, libraryId, confirmed,
            DateTimeOffset.UtcNow);
        var raw = JsonSerializer.SerializeToUtf8Bytes(updated, Options);
        try
        {
            var encrypted = protection.Protect(raw);
            if (encrypted.Length is <= 0 or > MaximumBytes)
                throw new InvalidDataException("Geschützter Zentralmerker ist zu groß.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var file = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                file.Write(encrypted);
                file.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private (Cursor? State, FileRevision Revision) Read()
    {
        if (!File.Exists(path)) return (null, FileRevision.Missing);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        if (file.Length is <= 0 or > MaximumBytes)
            throw new InvalidDataException("Zentralmerker ist leer oder zu groß.");
        var encrypted = new byte[(int)file.Length];
        file.ReadExactly(encrypted);
        var revision = new FileRevision(true,
            Convert.ToHexString(SHA256.HashData(encrypted)));
        var raw = protection.Unprotect(encrypted);
        try
        {
            using (var parsed = JsonDocument.Parse(raw,
                new JsonDocumentOptions { MaxDepth = 12 }))
                OfflineGroupPolicyService.RejectDuplicateProperties(parsed.RootElement);
            var value = JsonSerializer.Deserialize<Cursor>(raw, Options)
                ?? throw new InvalidDataException("Zentralmerker fehlt.");
            if (value.Format != Format || value.Version != 1 ||
                value.LibraryId != libraryId || value.Head is null)
                throw new CryptographicException("Zentralmerker gehört zu einer anderen Bibliothek.");
            Validate(value.Head);
            return (value, revision);
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private void Validate(CentralKeyLibraryHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (libraryId == Guid.Empty || head.LibraryId != libraryId ||
            head.Policy is null || head.Policy.Revision < 1 ||
            !IsSha(head.Policy.PayloadSha256) || head.CentralRevision < 0 ||
            head.CatalogRevision < 0 ||
            (head.CentralRevision == 0 &&
                (head.CatalogRevision != 0 || head.GroupId != "" ||
                    head.CiphertextSha256 != "")) ||
            (head.CentralRevision > 0 &&
                (head.CatalogRevision < 1 ||
                    head.GroupId is not { Length: > 0 and <= 128 } ||
                    !head.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
                    !IsSha(head.CiphertextSha256))))
            throw new InvalidDataException("Ungültige zentrale Bibliotheksrevision.");
    }

    private static void CheckTransition(CentralKeyLibraryHead old,
        CentralKeyLibraryHead observed)
    {
        if (old.LibraryId != observed.LibraryId ||
            old.Policy != observed.Policy ||
            observed.CentralRevision < old.CentralRevision ||
            observed.CatalogRevision < old.CatalogRevision ||
            observed.CentralRevision > old.CentralRevision &&
                observed.CatalogRevision <= old.CatalogRevision ||
            old.CentralRevision > 0 && old.GroupId != observed.GroupId ||
            observed.CentralRevision == old.CentralRevision &&
                (observed.CatalogRevision != old.CatalogRevision ||
                    observed.CiphertextSha256 != old.CiphertextSha256 ||
                    observed.GroupId != old.GroupId))
            throw new CryptographicException(
                "Zentrale liefert einen älteren oder widersprüchlichen signierten Revisionsstand. Kein automatisches Zurücksetzen.");
    }

    private static bool IsSha(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

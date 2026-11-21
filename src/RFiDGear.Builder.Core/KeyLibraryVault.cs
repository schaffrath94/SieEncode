using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>A real, explicit DESFire key value, held only inside an encrypted library payload.</summary>
public sealed record KeyLibrarySecret(KeyLibraryAddress Key, string HexValue)
{
    // Prevent accidental secret disclosure through record interpolation and diagnostic output.
    public override string ToString() => $"KeyLibrarySecret({Key.KeyId:N}, v{Key.Version}, [REDACTED])";
}

/// <summary>
/// Single authoritative local snapshot: complete key metadata and its secret values.
/// This class must never be serialized to logs, project definitions, reports or Git.
/// </summary>
public sealed record KeyLibraryVaultPayload(KeyLibrarySnapshot Catalog, KeyLibrarySecret[] Secrets);

/// <summary>
/// A separate, purpose-bound document format using the same authenticated group
/// AES-GCM/certificate/recovery protection as existing project Vaults.
/// No independent cryptographic implementation and no project file masquerading.
/// </summary>
public static class KeyLibraryVault
{
    public const string Format = "SieEncode-Key-Library-Payload";
    private const int Version = 1;
    private sealed record Envelope(string Format, int Version, KeyLibraryVaultPayload Library);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static void Validate(KeyLibraryVaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Catalog is null || payload.Secrets is null)
            throw new InvalidDataException("Vollständige Schlüsselbibliothek oder Schlüsselwerte fehlen.");
        var index = KeyLibraryContracts.Validate(payload.Catalog);
        if (index.Count != payload.Secrets.Length)
            throw new InvalidDataException("Vollständige Bibliothek benötigt für jede Schlüsselversion genau einen geschützten Wert.");

        var seen = new HashSet<KeyLibraryAddress>();
        foreach (var secret in payload.Secrets)
        {
            if (secret is null || secret.Key is null || !seen.Add(secret.Key) ||
                !index.TryGetValue(secret.Key, out var metadata))
                throw new InvalidDataException("Doppelter oder unbekannter Geheimwert in der Schlüsselbibliothek.");

            var length = metadata.KeyType switch
            {
                "DF_KEY_AES" or "DF_KEY_DES" => 32,
                "DF_KEY_3K3DES" => 48,
                _ => throw new InvalidDataException("Schlüsseltyp ist nicht unterstützt.")
            };
            if (secret.HexValue is null || secret.HexValue.Length != length ||
                !secret.HexValue.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f'))
                throw new InvalidDataException("Schlüsselwert hat einen ungültigen Typ, Umfang oder Hex-Inhalt.");
        }
    }

    public static GroupVaultDocument Protect(KeyLibraryVaultPayload payload, VerifiedGroupPolicy trusted,
        string groupId, DateTimeOffset now)
    {
        Validate(payload);
        ArgumentNullException.ThrowIfNull(trusted);
        if (payload.Catalog.LibraryId != trusted.Policy.LibraryId)
            throw new CryptographicException("Schlüsselbibliothek gehört nicht zur signierten Gruppenbibliothek.");

        var json = JsonSerializer.Serialize(new Envelope(Format, Version, payload), Options);
        // The Group Vault header itself is authenticated AAD. KeyLibraryFormat
        // is a distinct authenticated domain, so a project Vault never opens here.
        return GroupVaultService.Protect(json, trusted, payload.Catalog.LibraryId, groupId, now,
            GroupVaultPurpose.KeyLibrary);
    }

    public static KeyLibraryVaultPayload Unprotect(GroupVaultDocument document, VerifiedGroupPolicy trusted,
        string groupId, IEnumerable<X509Certificate2> identities, bool recovery = false)
    {
        ArgumentNullException.ThrowIfNull(trusted);
        var json = GroupVaultService.Unprotect(document, trusted, trusted.Policy.LibraryId, groupId,
            identities, recovery, GroupVaultPurpose.KeyLibrary);
        using (var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(parsed.RootElement);
        var envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
        if (envelope is null || envelope.Format != Format || envelope.Version != Version || envelope.Library is null ||
            envelope.Library.Catalog is null || envelope.Library.Catalog.LibraryId != trusted.Policy.LibraryId)
            throw new CryptographicException("Falscher Schlüsselbibliotheks-Typ oder Bibliotheks-ID.");
        Validate(envelope.Library);
        return envelope.Library;
    }
}

/// <summary>
/// Explicit exact-version resolver for the existing project's KeyReference type.
/// The caller must already have legitimately unlocked the offline library Vault.
/// No implicit "latest" behavior; no cross-customer reuse and no status bypass.
/// </summary>
public static class KeyLibraryKeyResolver
{
    public static KeyReference ResolveActive(KeyLibraryVaultPayload library, Guid customerId,
        KeyLibraryAddress address, string requiredKeyType)
    {
        KeyLibraryVault.Validate(library);
        if (customerId == Guid.Empty || address is null ||
            string.IsNullOrWhiteSpace(requiredKeyType))
            throw new InvalidDataException("Kunde, stabile Schlüsselversion und erwarteter Typ sind erforderlich.");

        KeyLibraryContracts.ValidateForNewOperation(library.Catalog,
            [new ProjectKeyLibraryBinding("resolved-key", address)]);
        var metadata = KeyLibraryContracts.Validate(library.Catalog)[address];
        if (metadata.CustomerId != customerId)
            throw new CryptographicException("Schlüsselversion gehört zu einem anderen Kunden.");
        if (!string.Equals(metadata.KeyType, requiredKeyType, StringComparison.Ordinal))
            throw new InvalidDataException("Schlüsseltyp passt nicht zum vorgesehenen Kartenvorgang.");
        var secret = library.Secrets.Single(item => item.Key == address);
        return new KeyReference { Type = metadata.KeyType, Value = secret.HexValue };
    }
}

/// <summary>
/// Safe local encrypted snapshot storage. The central service and sync protocol
/// will be attached later; this intentionally provides no untrusted key export.
/// </summary>
public static class KeyLibraryVaultStore
{
    public static string DefaultPath(Guid libraryId, string groupId)
    {
        if (libraryId == Guid.Empty || string.IsNullOrWhiteSpace(groupId) ||
            groupId.Length > 128 || !groupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new ArgumentException("Ungültige Bibliotheks- oder Gruppenkennung.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            throw new InvalidOperationException("Geschützter lokaler Vault-Speicherort ist nicht verfügbar.");
        return Path.Combine(local, "SieEncode", "KeyLibrary", libraryId.ToString("N"),
            groupId + ".key-library.vault.json");
    }

    public static FileRevision Create(string path, KeyLibraryVaultPayload payload,
        OfflineGroupTrustStore trust, string groupId, IEnumerable<X509Certificate2> identities,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trust);
        var current = trust.LoadOffline();
        var document = KeyLibraryVault.Protect(payload, current, groupId, now);
        var identitiesArray = identities.ToArray();
        _ = KeyLibraryVault.Unprotect(document, current, groupId, identitiesArray);
        var bytes = GroupVaultService.Write(document);
        return Write(path, FileRevision.Missing, bytes, () =>
        {
            if (trust.LoadOffline().Checkpoint != current.Checkpoint)
                throw new CryptographicException("Signierter Gruppenstand hat sich während der Erstellung geändert.");
        });
    }

    public static KeyLibraryVaultPayload Load(string path, OfflineGroupTrustStore trust, string groupId,
        IEnumerable<X509Certificate2> identities, bool recovery = false)
    {
        ArgumentNullException.ThrowIfNull(trust);
        var document = ReadDocument(path);
        return KeyLibraryVault.Unprotect(document, trust.LoadOffline(), groupId, identities, recovery);
    }

    public static FileRevision Update(string path, KeyLibraryVaultPayload next, OfflineGroupTrustStore trust,
        string groupId, IEnumerable<X509Certificate2> identities, FileRevision expected,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(expected);
        if (!expected.Exists || ConcurrentFileWriter.Capture(path) != expected)
            throw new ConcurrentFileChangeException(Path.GetFullPath(path));

        var current = trust.LoadOffline();
        var previousDocument = ReadDocument(path);
        // A changed signed group policy needs a separate, explicit recipient
        // reconciliation, never an implicit rewrap during routine data updates.
        if (previousDocument.Header.Policy != current.Checkpoint)
            throw new CryptographicException("Gruppenempfänger haben sich geändert. Bibliotheks-Vault erst kontrolliert abgleichen.");
        var identitiesArray = identities.ToArray();
        var previous = KeyLibraryVault.Unprotect(previousDocument, current, groupId, identitiesArray);
        KeyLibraryVault.Validate(next);
        if (next.Catalog.LibraryId != previous.Catalog.LibraryId ||
            previous.Catalog.Revision == long.MaxValue ||
            next.Catalog.Revision != previous.Catalog.Revision + 1)
            throw new InvalidDataException("Schlüsselbibliothek muss dieselbe ID und die direkt folgende Revision besitzen.");
        ValidateImmutableHistory(previous, next);

        var document = KeyLibraryVault.Protect(next, current, groupId, now);
        _ = KeyLibraryVault.Unprotect(document, current, groupId, identitiesArray);
        var bytes = GroupVaultService.Write(document);
        return Write(path, expected, bytes, () =>
        {
            if (trust.LoadOffline().Checkpoint != current.Checkpoint)
                throw new CryptographicException("Gruppenstand während der Bibliotheksänderung gewechselt.");
        });
    }

    private static void ValidateImmutableHistory(KeyLibraryVaultPayload previous, KeyLibraryVaultPayload next)
    {
        var currentEntries = KeyLibraryContracts.Validate(next.Catalog);
        var newValues = next.Secrets.ToDictionary(item => item.Key, item => item.HexValue);
        var oldValues = previous.Secrets.ToDictionary(item => item.Key, item => item.HexValue);
        foreach (var old in previous.Catalog.Keys)
        {
            var address = new KeyLibraryAddress(old.KeyId, old.Version);
            if (!currentEntries.TryGetValue(address, out var current) ||
                !newValues.TryGetValue(address, out var value) ||
                !oldValues.TryGetValue(address, out var oldValue))
                throw new InvalidDataException("Bestehende Schlüsselversionen dürfen beim Abgleich nicht gelöscht werden.");
            if (current.CustomerId != old.CustomerId || current.KeyType != old.KeyType)
                throw new InvalidDataException("Kunde und Schlüsseltyp einer vorhandenen Schlüssel-ID dürfen nicht verändert werden.");
            if (!string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ein vorhandener Schlüsselwert darf nicht unter derselben Version überschrieben werden.");
            if (old.Status == KeyLibraryContracts.Compromised && current.Status != KeyLibraryContracts.Compromised ||
                old.Status == KeyLibraryContracts.Retired && current.Status == KeyLibraryContracts.Active)
                throw new InvalidDataException("Eine abgelöste oder kompromittierte Schlüsselgeneration darf nicht still reaktiviert werden.");
        }
    }

    private static GroupVaultDocument ReadDocument(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GroupVaultService.MaximumBytes)
            throw new InvalidDataException("Bibliotheks-Vault ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return GroupVaultService.Read(bytes);
    }

    private static FileRevision Write(string path, FileRevision expected, byte[] bytes, Action checkPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            checkPolicy();
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes); stream.Flush(true);
            checkPolicy();
        });
    }
}

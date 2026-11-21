using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// A stable identity for a managed key version. Names, card projects and customer
/// display labels are never used as secret-key identities.
/// </summary>
public sealed record KeyLibraryAddress(Guid KeyId, int Version);

/// <summary>
/// Key metadata only. Actual secret material belongs in the authenticated,
/// encrypted library Vault; neither project JSON nor this index contains it.
/// </summary>
public sealed record KeyLibraryEntry(
    Guid CustomerId,
    Guid KeyId,
    int Version,
    string Name,
    string KeyType,
    string Purpose,
    string HostSystem,
    string Status);

/// <summary>
/// Central-authority metadata snapshot. A full authorized offline working set
/// comprises this metadata AND a separately protected secret container.
/// The catalog is not an authorization token and does not decrypt keys.
/// </summary>
public sealed record KeyLibrarySnapshot(
    Guid LibraryId,
    long Revision,
    KeyLibraryEntry[] Keys);

/// <summary>
/// Key-free, version-pinned project assignment. Slot syntax will be fixed at
/// the editor integration boundary; no runtime lookup is enabled yet.
/// </summary>
// A slot may override the project's default customer, for combined cards that
// legitimately contain applications owned by several different customers.
public sealed record ProjectKeyLibraryBinding(string Slot, KeyLibraryAddress Key, Guid? CustomerId = null);

public static class KeyLibraryContracts
{
    public const string Active = "active";
    public const string Retired = "retired";
    public const string Compromised = "compromised";

    private static readonly HashSet<string> SupportedKeyTypes = new(StringComparer.Ordinal)
    {
        "DF_KEY_AES", "DF_KEY_DES", "DF_KEY_3K3DES"
    };

    public static IReadOnlyDictionary<KeyLibraryAddress, KeyLibraryEntry> Validate(KeyLibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.LibraryId == Guid.Empty || snapshot.Revision < 1 || snapshot.Keys is null ||
            snapshot.Keys.Length > 100_000)
            throw new InvalidDataException("Schlüsselbibliothek benötigt eine stabile Bibliotheks-ID, gültige Revision und begrenzten Katalog.");

        var index = new Dictionary<KeyLibraryAddress, KeyLibraryEntry>();
        var identities = new Dictionary<Guid, (Guid CustomerId, string KeyType)>();
        foreach (var key in snapshot.Keys)
        {
            if (key is null || key.CustomerId == Guid.Empty || key.KeyId == Guid.Empty || key.Version < 1 ||
                string.IsNullOrWhiteSpace(key.Name) || key.Name.Length > 200 ||
                !SupportedKeyTypes.Contains(key.KeyType ?? "") ||
                key.Purpose is null || key.Purpose.Length > 200 ||
                key.HostSystem is null || key.HostSystem.Length > 200 ||
                key.Status is not (Active or Retired or Compromised))
                throw new InvalidDataException("Ungültige Schlüsselmetadaten: Kunde, stabile ID, Version, Keytyp oder Status fehlen.");

            var address = new KeyLibraryAddress(key.KeyId, key.Version);
            if (!index.TryAdd(address, key))
                throw new InvalidDataException("Die Kombination aus Schlüssel-ID und Version muss eindeutig sein.");
            if (identities.TryGetValue(key.KeyId, out var established) &&
                (established.CustomerId != key.CustomerId || established.KeyType != key.KeyType))
                throw new InvalidDataException("Ein Schlüssel darf über Versionen hinweg weder Kunde noch Schlüsseltyp wechseln.");
            identities[key.KeyId] = (key.CustomerId, key.KeyType);
        }
        return index;
    }

    /// <summary>
    /// Validate metadata bindings only; never materialize or log key values.
    /// Missing/retired/compromised keys cannot silently become usable again.
    /// </summary>
    public static void ValidateForNewOperation(KeyLibrarySnapshot snapshot,
        IEnumerable<ProjectKeyLibraryBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var index = Validate(snapshot);
        var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            if (binding is null || binding.Key is null || binding.Key.KeyId == Guid.Empty ||
                binding.Key.Version < 1 || string.IsNullOrWhiteSpace(binding.Slot) ||
                binding.Slot.Length > 160 || binding.Slot.Any(char.IsWhiteSpace) ||
                !slots.Add(binding.Slot))
                throw new InvalidDataException("Projekt-Schlüsselzuordnung ist leer, ungültig oder doppelt.");

            if (!index.TryGetValue(binding.Key, out var key))
                throw new InvalidDataException("Projekt verweist auf eine unbekannte Schlüssel-ID oder Version.");
            if (key.Status != Active)
                throw new InvalidOperationException("Abgelöste oder kompromittierte Schlüssel dürfen nicht für neue produktive Vorgänge freigegeben werden.");
        }
    }
}

namespace RFiDGear.Builder.Core;

/// <summary>One intentional in-memory library mutation; persistence and authority confirmation are separate.</summary>
public sealed record KeyLibraryEditResult(KeyLibraryVaultPayload Next, KeyLibraryAddress Key);

/// <summary>
/// The single application's domain editing operations. No web-server UI,
/// no independent secret store and no implicit change to card/reader keys.
/// The caller must first unlock the existing encrypted library legitimately.
/// </summary>
public static class KeyLibraryEditing
{
    public static KeyLibraryEditResult ImportNew(KeyLibraryVaultPayload current, Guid customerId,
        string name, string keyType, string purpose, string hostSystem, string hexValue)
    {
        ValidateCurrent(current);
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Kunde und Schlüsselbezeichnung sind erforderlich.");

        var address = new KeyLibraryAddress(Guid.NewGuid(), 1);
        var entry = new KeyLibraryEntry(customerId, address.KeyId, address.Version, name.Trim(),
            keyType, purpose ?? "", hostSystem ?? "", KeyLibraryContracts.Active);
        return Append(current, entry, new KeyLibrarySecret(address, hexValue));
    }

    public static KeyLibraryEditResult GenerateNew(KeyLibraryVaultPayload current, Guid customerId,
        string name, string keyType, string purpose, string hostSystem)
        => ImportNew(current, customerId, name, keyType, purpose, hostSystem,
            KeyVaultService.GenerateKey(keyType));

    /// <summary>
    /// Explicitly add the next generation. Existing active versions remain
    /// untouched: the host-system/card migration is a separate, verified action.
    /// </summary>
    public static KeyLibraryEditResult AddVersion(KeyLibraryVaultPayload current, Guid keyId, string newHexValue)
    {
        ValidateCurrent(current);
        if (keyId == Guid.Empty) throw new InvalidDataException("Stabile Schlüssel-ID fehlt.");
        var generations = current.Catalog.Keys.Where(item => item.KeyId == keyId).ToArray();
        if (generations.Length == 0) throw new InvalidDataException("Schlüssel-ID ist unbekannt.");
        var latest = generations.MaxBy(item => item.Version)!;
        if (latest.Version == int.MaxValue) throw new InvalidDataException("Schlüsselversion kann nicht weiter erhöht werden.");
        var nextVersion = latest.Version + 1;
        if (current.Secrets.Where(item => item.Key.KeyId == keyId)
            .Any(item => string.Equals(item.HexValue, newHexValue, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Neue Schlüsselversion darf keinen bereits vorhandenen Schlüsselwert erneut verwenden.");
        var address = new KeyLibraryAddress(keyId, nextVersion);
        var entry = latest with { Version = nextVersion, Status = KeyLibraryContracts.Active };
        return Append(current, entry, new KeyLibrarySecret(address, newHexValue));
    }

    /// <summary>
    /// A state change is explicit; no card or reader is touched here.
    /// A retired or compromised generation must not be silently resurrected.
    /// </summary>
    public static KeyLibraryVaultPayload SetStatus(KeyLibraryVaultPayload current,
        KeyLibraryAddress address, string status)
    {
        ValidateCurrent(current);
        if (status is not (KeyLibraryContracts.Active or KeyLibraryContracts.Retired or KeyLibraryContracts.Compromised))
            throw new InvalidDataException("Unbekannter Schlüsselstatus.");
        var index = Array.FindIndex(current.Catalog.Keys,
            item => item.KeyId == address.KeyId && item.Version == address.Version);
        if (index < 0) throw new InvalidDataException("Schlüsselversion ist unbekannt.");
        var old = current.Catalog.Keys[index].Status;
        if (old == KeyLibraryContracts.Compromised && status != KeyLibraryContracts.Compromised ||
            old == KeyLibraryContracts.Retired && status == KeyLibraryContracts.Active)
            throw new InvalidOperationException("Abgelöste oder kompromittierte Schlüssel dürfen nicht reaktiviert werden.");
        if (old == status) throw new InvalidOperationException("Kein Schlüsselstatus wurde geändert.");
        var entries = current.Catalog.Keys.ToArray();
        entries[index] = entries[index] with { Status = status };
        var next = current with { Catalog = current.Catalog with
            { Revision = NextRevision(current), Keys = entries } };
        KeyLibraryVault.Validate(next);
        return next;
    }

    private static KeyLibraryEditResult Append(KeyLibraryVaultPayload current,
        KeyLibraryEntry entry, KeyLibrarySecret secret)
    {
        var next = new KeyLibraryVaultPayload(current.Catalog with
            {
                Revision = NextRevision(current),
                Keys = [.. current.Catalog.Keys, entry]
            },
            [.. current.Secrets, secret]);
        KeyLibraryVault.Validate(next);
        return new(next, secret.Key);
    }

    private static long NextRevision(KeyLibraryVaultPayload current)
    {
        if (current.Catalog.Revision == long.MaxValue)
            throw new InvalidDataException("Schlüsselbibliotheksrevision ist erschöpft.");
        return current.Catalog.Revision + 1;
    }

    private static void ValidateCurrent(KeyLibraryVaultPayload current) =>
        KeyLibraryVault.Validate(current);
}

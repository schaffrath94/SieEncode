using RFiDGear.Builder.Core;

internal static class KeyLibraryEditingTests
{
    public static void Verify()
    {
        var libraryId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var empty = new KeyLibraryVaultPayload(new KeyLibrarySnapshot(libraryId, 1, []), []);
        KeyLibraryVault.Validate(empty);

        const string first = "0123456789ABCDEF0123456789ABCDEF";
        const string second = "AABBCCDDEEFF00112233445566778899";
        var imported = KeyLibraryEditing.ImportNew(empty, customerId, "SIPORT PICC",
            "DF_KEY_AES", "PICC", "SIPORT", first);
        if (imported.Next.Catalog.Revision != 2 || imported.Next.Catalog.Keys.Length != 1 ||
            imported.Next.Secrets.Single().HexValue != first || empty.Catalog.Keys.Length != 0)
            throw new Exception("Import must add one encrypted-library key record without changing the old snapshot.");
        var newVersion = KeyLibraryEditing.AddVersion(imported.Next, imported.Key.KeyId, second);
        if (newVersion.Next.Catalog.Revision != 3 ||
            newVersion.Key.Version != 2 ||
            newVersion.Next.Secrets.Length != 2 ||
            newVersion.Next.Catalog.Keys[0].Status != KeyLibraryContracts.Active)
            throw new Exception("Adding a key version must not silently retire earlier card/reader generations.");

        Reject<InvalidDataException>(() => KeyLibraryEditing.AddVersion(newVersion.Next, imported.Key.KeyId, first));
        Reject<InvalidDataException>(() => KeyLibraryEditing.AddVersion(newVersion.Next, imported.Key.KeyId, first.ToLowerInvariant()));
        Reject<InvalidDataException>(() => KeyLibraryEditing.ImportNew(empty, Guid.Empty, "bad", "DF_KEY_AES", "", "", first));
        Reject<InvalidDataException>(() => KeyLibraryEditing.ImportNew(empty, customerId, "bad", "DF_KEY_AES", "", "", "1234"));
        Reject<ArgumentOutOfRangeException>(() => KeyLibraryEditing.GenerateNew(empty, customerId, "bad", "INVALID", "", ""));
        Reject<InvalidDataException>(() => KeyLibraryEditing.SetStatus(newVersion.Next,
            new KeyLibraryAddress(Guid.NewGuid(), 1), KeyLibraryContracts.Retired));

        var retired = KeyLibraryEditing.SetStatus(newVersion.Next, imported.Key, KeyLibraryContracts.Retired);
        if (retired.Catalog.Revision != 4 ||
            retired.Catalog.Keys.Single(item => item.Version == 1).Status != KeyLibraryContracts.Retired ||
            retired.Catalog.Keys.Single(item => item.Version == 2).Status != KeyLibraryContracts.Active)
            throw new Exception("Retirement must affect only the explicitly named key generation.");
        Reject<InvalidOperationException>(() => KeyLibraryEditing.SetStatus(retired, imported.Key, KeyLibraryContracts.Active));
        Reject<InvalidOperationException>(() => KeyLibraryEditing.SetStatus(retired, imported.Key, KeyLibraryContracts.Retired));

        var compromised = KeyLibraryEditing.SetStatus(retired, newVersion.Key, KeyLibraryContracts.Compromised);
        Reject<InvalidOperationException>(() => KeyLibraryEditing.SetStatus(compromised,
            newVersion.Key, KeyLibraryContracts.Retired));
        Reject<InvalidOperationException>(() => KeyLibraryEditing.SetStatus(compromised,
            newVersion.Key, KeyLibraryContracts.Active));

        var generated = KeyLibraryEditing.GenerateNew(compromised, customerId, "New random AES",
            "DF_KEY_AES", "PICC", "SIPORT");
        if (generated.Next.Catalog.Revision != 6 ||
            generated.Next.Secrets.Single(item => item.Key == generated.Key).HexValue.Length != 32 ||
            generated.Next.Catalog.Keys.Count(item => item.CustomerId == customerId) != 3)
            throw new Exception("New generated key was not added as an independent library entry.");
        if (compromised.Catalog.Keys.Length != 2 ||
            empty.Catalog.Revision != 1)
            throw new Exception("Editing mutated an earlier immutable library snapshot.");
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Invalid key library edit succeeded: " + typeof(T).Name);
    }
}

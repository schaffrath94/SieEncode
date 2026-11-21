using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class KeyLibraryContractTests
{
    public static void Verify()
    {
        var libraryId = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();
        var a1 = new KeyLibraryEntry(customerA, keyA, 1, "PICC", "DF_KEY_AES", "PICC", "SIPORT", KeyLibraryContracts.Retired);
        var a2 = a1 with { Version = 2, Status = KeyLibraryContracts.Active };
        var b1 = new KeyLibraryEntry(customerB, keyB, 1, "App 01", "DF_KEY_AES", "Application", "SIPORT", KeyLibraryContracts.Active);
        var snapshot = new KeyLibrarySnapshot(libraryId, 5, [a1, a2, b1]);
        var index = KeyLibraryContracts.Validate(snapshot);
        if (index.Count != 3 || index[new KeyLibraryAddress(keyA, 2)].CustomerId != customerA)
            throw new Exception("Versionierte Bibliotheksidentität oder getrennte Kundenzuordnung verloren.");

        var boundA = new ProjectKeyLibraryBinding("productive/picc", new KeyLibraryAddress(keyA, 2));
        var boundB = new ProjectKeyLibraryBinding("productive/application/A1B2C3/0", new KeyLibraryAddress(keyB, 1));
        KeyLibraryContracts.ValidateForNewOperation(snapshot, [boundA, boundB]);

        Reject<InvalidDataException>(() => KeyLibraryContracts.Validate(snapshot with { Keys = [a1, a2, a2] }));
        Reject<InvalidDataException>(() => KeyLibraryContracts.Validate(snapshot with { Keys = [a1, a2 with { CustomerId = customerB }, b1] }));
        Reject<InvalidDataException>(() => KeyLibraryContracts.Validate(snapshot with { Keys = [a1, a2 with { KeyType = "DF_KEY_3K3DES" }, b1] }));
        Reject<InvalidDataException>(() => KeyLibraryContracts.Validate(snapshot with { Revision = 0 }));
        Reject<InvalidDataException>(() => KeyLibraryContracts.ValidateForNewOperation(snapshot,
            [boundA, boundA]));
        Reject<InvalidDataException>(() => KeyLibraryContracts.ValidateForNewOperation(snapshot,
            [boundA, boundA with { Key = new KeyLibraryAddress(keyB, 1) }]));
        Reject<InvalidDataException>(() => KeyLibraryContracts.ValidateForNewOperation(snapshot,
            [boundA with { Key = new KeyLibraryAddress(keyA, 999) }]));
        Reject<InvalidOperationException>(() => KeyLibraryContracts.ValidateForNewOperation(snapshot,
            [boundA with { Key = new KeyLibraryAddress(keyA, 1) }]));
        Reject<InvalidDataException>(() => KeyLibraryContracts.ValidateForNewOperation(snapshot,
            [boundA with { Slot = "productive/ picc" }]));
        Reject<InvalidOperationException>(() => KeyLibraryContracts.ValidateForNewOperation(
            snapshot with { Keys = [a1, a2 with { Status = KeyLibraryContracts.Compromised }, b1] }, [boundA]));

        // Pure metadata output: no raw key in this schema, no customer passwords,
        // no new secret storage format and no side effects on project JSON.
        var json = JsonSerializer.Serialize(snapshot);
        if (json.Contains("Value", StringComparison.Ordinal) ||
            json.Contains("Secret", StringComparison.Ordinal) ||
            json.Contains("Ciphertext", StringComparison.Ordinal) ||
            JsonSerializer.Deserialize<KeyLibrarySnapshot>(json)?.Keys.Length != 3)
            throw new Exception("Die Schlüsselmetadaten enthalten ein Geheimwertfeld oder können nicht gelesen werden.");
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Ungültige Bibliotheks-/Schlüsselbindung wurde ohne Fehler akzeptiert: " + typeof(T).Name);
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class KeyLibraryVaultTests
{
    public static void Verify()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "SieEncode-library-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        using var administrator = RSA.Create(3072);
        using var aliceKey = RSA.Create(3072);
        using var bobKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        X509Certificate2 Certificate(string name, RSA key) => new CertificateRequest("CN=" + name, key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        using var alice = Certificate("Synthetic library Alice", aliceKey);
        using var bob = Certificate("Synthetic library Bob", bobKey);
        using var recovery = Certificate("Synthetic library Recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject,
            CertificateVaultService.Fingerprint(cert), Convert.ToBase64String(cert.RawData), true);

        var libraryId = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();
        const string aes1 = "11223344556677889900AABBCCDDEEFF";
        const string aes2 = "0123456789ABCDEF0123456789ABCDEF";
        const string aes3 = "A1B2C3D4E5F60718293A4B5C6D7E8F9";
        const string threeDes = "11223344556677889900AABBCCDDEEFF0011223344556677";
        var snapshot = new KeyLibrarySnapshot(libraryId, 1,
        [
            new(customerA, keyA, 1, "Kunden-Schlüssel 1", "DF_KEY_AES", "PICC", "SIPORT", KeyLibraryContracts.Active),
            new(customerB, keyB, 1, "Kunden-Schlüssel 2", "DF_KEY_3K3DES", "Application", "Kundensystem", KeyLibraryContracts.Active)
        ]);
        var keys = new KeyLibraryVaultPayload(snapshot,
        [
            new(new KeyLibraryAddress(keyA, 1), aes1),
            new(new KeyLibraryAddress(keyB, 1), threeDes)
        ]);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, libraryId, 1,
            [new("keys", "Zentrale Schlüsselverwaltung", ["alice"]), new("other", "Other", ["bob"])])
        {
            People =
            [
                new("alice", "Alice", true, [Device(alice)]),
                new("bob", "Bob", true, [Device(bob)])
            ],
            RecoveryDevices = [Device(recovery)]
        };
        try
        {
            var trust = new OfflineGroupTrustStore(Path.Combine(rootDir, "trust.bin"), libraryId,
                new OfflineGroupTrustTests.TestProtection());
            var authority = administrator.ExportSubjectPublicKeyInfo();
            trust.Enroll(authority, OfflineGroupTrustStore.AdministratorFingerprint(authority),
                OfflineGroupPolicyService.Sign(policy, administrator));
            var path = Path.Combine(rootDir, "full.key-library.vault.json");
            if (KeyLibraryVaultStore.DefaultPath(libraryId, "keys") == path ||
                !KeyLibraryVaultStore.DefaultPath(libraryId, "keys").Contains(libraryId.ToString("N")))
                throw new Exception("Vorgesehener Bibliotheksspeicherort muss unabhängig vom Projektverzeichnis sein.");

            KeyLibraryVault.Validate(keys);
            if (keys.Secrets[0].ToString()!.Contains(aes1, StringComparison.Ordinal))
                throw new Exception("Secret-Diagnose könnte den Rohwert offenlegen.");

            var saved = KeyLibraryVaultStore.Create(path, keys, trust, "keys", [alice], now);
            if (!saved.Exists || saved != ConcurrentFileWriter.Capture(path))
                throw new Exception("Vollständiger verschlüsselter Schlüsselbestand wurde nicht atomar gespeichert.");
            var raw = File.ReadAllText(path);
            if (raw.Contains(aes1, StringComparison.OrdinalIgnoreCase) ||
                raw.Contains(aes2, StringComparison.OrdinalIgnoreCase) ||
                raw.Contains(threeDes, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Geheimwerte liegen offen auf der Festplatte.");

            var document = GroupVaultService.Read(File.ReadAllBytes(path));
            if (document.Header.Format != GroupVaultService.KeyLibraryFormat ||
                ProjectGroupVault.IsGroupVaultFile(path))
                throw new Exception("Kryptografische Domänentrennung zwischen Projekt und Schlüsselbibliothek fehlt.");
            Reject<CryptographicException>(() => GroupVaultService.Unprotect(document,
                trust.LoadOffline(), libraryId, "keys", [alice]));
            Reject<CryptographicException>(() => GroupVaultService.Unprotect(document,
                trust.LoadOffline(), libraryId, "other", [bob], purpose: GroupVaultPurpose.KeyLibrary));

            var loaded = KeyLibraryVaultStore.Load(path, trust, "keys", [alice]);
            if (loaded.Catalog.Keys.Length != 2 || loaded.Secrets.Single(s => s.Key.KeyId == keyA).HexValue != aes1 ||
                loaded.Secrets.Single(s => s.Key.KeyId == keyB).HexValue != threeDes)
                throw new Exception("Vollständiger Bibliotheks-Roundtrip hat Schlüsselwerte oder Kunden verloren.");
            var resolved = KeyLibraryKeyResolver.ResolveActive(loaded, customerA,
                new KeyLibraryAddress(keyA, 1), "DF_KEY_AES");
            if (resolved.Type != "DF_KEY_AES" || resolved.Value != aes1)
                throw new Exception("Bestehende Projekt-KeyReference erhält nicht den exakt gebundenen Bibliothekswert.");
            Reject<CryptographicException>(() => KeyLibraryKeyResolver.ResolveActive(loaded, customerB,
                new KeyLibraryAddress(keyA, 1), "DF_KEY_AES"));
            Reject<InvalidDataException>(() => KeyLibraryKeyResolver.ResolveActive(loaded, customerA,
                new KeyLibraryAddress(keyA, 1), "DF_KEY_DES"));
            Reject<InvalidDataException>(() => KeyLibraryKeyResolver.ResolveActive(loaded, customerA,
                new KeyLibraryAddress(keyA, 999), "DF_KEY_AES"));

            var rescue = KeyLibraryVaultStore.Load(path, trust, "keys", [recovery], recovery: true);
            if (rescue.Secrets[0].HexValue != aes1) throw new Exception("Unabhängiger Recovery-Zugang fehlt.");
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Load(path, trust, "keys", [bob]));
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Load(path, trust, "keys", [recovery]));
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Load(path, trust, "keys", [alice], recovery: true));
            Reject<ConcurrentFileChangeException>(() => KeyLibraryVaultStore.Create(path, keys, trust, "keys", [alice], now));
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Create(Path.Combine(rootDir, "denied.json"),
                keys, trust, "keys", [bob], now));

            // A library document cannot be opened as a project and vice versa,
            // even with a valid group identity and the same library identifier.
            var asProject = GroupVaultService.Protect("{\"project\":true}", trust.LoadOffline(), libraryId, "keys", now);
            Reject<CryptographicException>(() => KeyLibraryVault.Unprotect(asProject, trust.LoadOffline(), "keys", [alice]));
            var corrupted = document.Ciphertext.ToArray(); corrupted[0] ^= 0x01;
            Reject<CryptographicException>(() => KeyLibraryVault.Unprotect(document with { Ciphertext = corrupted },
                trust.LoadOffline(), "keys", [alice]));
            Reject<InvalidDataException>(() => KeyLibraryVault.Validate(keys with { Secrets = [keys.Secrets[0]] }));
            Reject<InvalidDataException>(() => KeyLibraryVault.Validate(keys with
            { Secrets = [keys.Secrets[0], keys.Secrets[0]] }));
            Reject<InvalidDataException>(() => KeyLibraryVault.Validate(keys with
            { Secrets = [keys.Secrets[0] with { HexValue = "AB" }, keys.Secrets[1]] }));
            Reject<CryptographicException>(() => KeyLibraryVault.Protect(keys with
            { Catalog = snapshot with { LibraryId = Guid.NewGuid() } }, trust.LoadOffline(), "keys", now));

            var versionTwo = new KeyLibraryEntry(customerA, keyA, 2, "Kunden-Schlüssel 1", "DF_KEY_AES",
                "PICC", "SIPORT", KeyLibraryContracts.Active);
            var revision2 = keys with
            {
                Catalog = snapshot with
                {
                    Revision = 2,
                    Keys = [snapshot.Keys[0] with { Status = KeyLibraryContracts.Retired }, snapshot.Keys[1], versionTwo]
                },
                Secrets = [keys.Secrets[0], keys.Secrets[1], new(new KeyLibraryAddress(keyA, 2), aes2)]
            };
            var beforeFailures = File.ReadAllBytes(path);
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision2 with
            { Catalog = revision2.Catalog with { Revision = 1 } }, trust, "keys", [alice], saved, now));
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision2 with
            { Secrets = [new(keys.Secrets[0].Key, aes3), keys.Secrets[1], revision2.Secrets[2]] },
                trust, "keys", [alice], saved, now));
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision2 with
            { Catalog = revision2.Catalog with { Keys = [snapshot.Keys[1], versionTwo] },
              Secrets = [keys.Secrets[1], revision2.Secrets[2]] }, trust, "keys", [alice], saved, now));
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision2 with
            { Catalog = revision2.Catalog with { Keys =
                [revision2.Catalog.Keys[0], revision2.Catalog.Keys[1] with { CustomerId = customerA }, versionTwo] } },
                trust, "keys", [alice], saved, now));
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision2 with
            { Catalog = revision2.Catalog with { Keys =
                [revision2.Catalog.Keys[0], revision2.Catalog.Keys[1] with { KeyType = "DF_KEY_AES" }, versionTwo] } },
                trust, "keys", [alice], saved, now));
            if (!File.ReadAllBytes(path).SequenceEqual(beforeFailures))
                throw new Exception("Verworfene Schlüsseländerung hat den Original-Vault verändert.");

            Reject<CryptographicException>(() => KeyLibraryVaultStore.Update(path, revision2, trust, "keys", [bob], saved, now));
            var saved2 = KeyLibraryVaultStore.Update(path, revision2, trust, "keys", [alice], saved, now);
            if (saved2 == saved || KeyLibraryVaultStore.Load(path, trust, "keys", [alice]).Catalog.Revision != 2 ||
                KeyLibraryVaultStore.Load(path, trust, "keys", [alice]).Secrets.Length != 3)
                throw new Exception("Kontrolliertes Bibliotheksupdate oder Geheimwert-Historie fehlgeschlagen.");
            var updatedLibrary = KeyLibraryVaultStore.Load(path, trust, "keys", [alice]);
            Reject<InvalidOperationException>(() => KeyLibraryKeyResolver.ResolveActive(updatedLibrary, customerA,
                new KeyLibraryAddress(keyA, 1), "DF_KEY_AES"));
            if (KeyLibraryKeyResolver.ResolveActive(updatedLibrary, customerA,
                    new KeyLibraryAddress(keyA, 2), "DF_KEY_AES").Value != aes2)
                throw new Exception("Projektauflösung wechselte nicht ausdrücklich auf eine neue Schlüsselversion.");
            Reject<ConcurrentFileChangeException>(() => KeyLibraryVaultStore.Update(path, revision2, trust,
                "keys", [alice], saved, now));

            var revision3 = revision2 with
            {
                Catalog = revision2.Catalog with { Revision = 3, Keys =
                [revision2.Catalog.Keys[0] with { Status = KeyLibraryContracts.Active },
                 revision2.Catalog.Keys[1], revision2.Catalog.Keys[2]] }
            };
            Reject<InvalidDataException>(() => KeyLibraryVaultStore.Update(path, revision3, trust,
                "keys", [alice], saved2, now)); // Retired versions cannot be silently resurrected.

            // Signed policy changes require an explicit recipient reconciliation.
            trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 2 }, administrator));
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Update(path, revision3, trust,
                "keys", [alice], saved2, now));
            if (KeyLibraryVaultStore.Load(path, trust, "keys", [alice]).Secrets.Length != 3)
                throw new Exception("Existing authorized encrypted library unexpectedly became unreadable.");
        }
        finally
        {
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, recursive: true);
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Ungültiger Bibliotheks-Vorgang wurde erlaubt: " + typeof(T).Name);
    }
}

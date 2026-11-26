using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class ScopedKeyLibraryVaultTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-scoped-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        var customerA = Guid.NewGuid(); var customerB = Guid.NewGuid(); var customerC = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var authority = RSA.Create(3072);
        using var fullKey = RSA.Create(3072);
        using var limitedKey = RSA.Create(3072);
        using var noneKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        X509Certificate2 Make(string name, RSA key) => new CertificateRequest("CN=" + name, key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        using var fullCert = Make("Synthetic all", fullKey);
        using var limitedCert = Make("Synthetic limited", limitedKey);
        using var noneCert = Make("Synthetic no customers", noneKey);
        using var recoveryCert = Make("Synthetic recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject,
            CertificateVaultService.Fingerprint(cert), Convert.ToBase64String(cert.RawData), true);
        var fullPerson = new OfflineVaultPerson("full", "All", true, [Device(fullCert)]) { AllKeyLibraryCustomers = true };
        var limited = new OfflineVaultPerson("limited", "Limited", true, [Device(limitedCert)]) { KeyLibraryCustomers = [customerA] };
        var none = new OfflineVaultPerson("none", "None", true, [Device(noneCert)]);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, id, 1,
            [new("keys", "Keys", ["full", "limited", "none"])])
        {
            CustomerScopesEnabled = true,
            People = [fullPerson, limited, none], RecoveryDevices = [Device(recoveryCert)]
        };
        var publicKey = authority.ExportSubjectPublicKeyInfo();
        GroupEnrollmentPackage Enrollment(OfflineGroupPolicy p) => new(GroupEnrollmentPackages.Format, 1, id,
            publicKey, OfflineGroupPolicyService.Sign(p, authority));
        try
        {
            Reject<InvalidDataException>(() => Enrollment(policy with
                { People = [fullPerson with { KeyLibraryCustomers = [customerA] }, limited, none] }));
            Reject<InvalidDataException>(() => Enrollment(policy with
                { People = [fullPerson, limited with { KeyLibraryCustomers = [customerA, customerA] }, none] }));
            var enrolled = Enrollment(policy);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), id,
                new OfflineGroupTrustTests.TestProtection());
            trust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), enrolled.Policy);
            var verified = trust.LoadOffline();
            var keyA = new KeyLibraryEntry(customerA, Guid.NewGuid(), 1, "Synthetic A", "DF_KEY_AES", "PICC", "Test", KeyLibraryContracts.Active);
            var keyB = new KeyLibraryEntry(customerB, Guid.NewGuid(), 1, "Synthetic B", "DF_KEY_AES", "PICC", "Test", KeyLibraryContracts.Active);
            var payload = new KeyLibraryVaultPayload(new(id, 1, [keyA, keyB])
            { Customers = [new(customerA, "Synthetic A"), new(customerB, "Synthetic B"), new(customerC, "Empty customer")] },
                [new(new(keyA.KeyId, 1), new string('A', 32)), new(new(keyB.KeyId, 1), new string('B', 32))]);
            Reject<CryptographicException>(() => KeyLibraryVault.Protect(payload, verified, "keys", now));
            var signed = ScopedKeyLibraryVault.Protect(payload, enrolled, authority, "keys", now);
            var bundle = ScopedKeyLibraryVault.Verify(signed, enrolled, "keys");
            var a = bundle.Customers.Single(c => c.CustomerId == customerA);
            var b = bundle.Customers.Single(c => c.CustomerId == customerB);
            if (a.Vault.Header.KeyGeneration == b.Vault.Header.KeyGeneration ||
                b.Vault.Header.Recipients.Any(r => r.Fingerprint == CertificateVaultService.Fingerprint(limitedCert)) ||
                bundle.Customers.Any(c => c.Vault.Header.Recipients.Any(r => r.Fingerprint == CertificateVaultService.Fingerprint(noneCert))))
                throw new Exception("Unauthorized device received a content-key wrap.");
            var all = ScopedKeyLibraryVault.Open(signed, trust, "keys", fullCert, now, requireFull: true);
            var subset = ScopedKeyLibraryVault.Open(signed, trust, "keys", limitedCert, now);
            var empty = ScopedKeyLibraryVault.Open(signed, trust, "keys", noneCert, now);
            var recovered = ScopedKeyLibraryVault.Open(signed, trust, "keys", recoveryCert, now, recovery: true);
            if (all.Catalog.Customers.Length != 3 || all.Secrets.Length != 2 ||
                subset.Catalog.Customers.Single().CustomerId != customerA ||
                subset.Secrets.Single().Key.KeyId != keyA.KeyId ||
                empty.Catalog.Customers.Length != 0 || empty.Secrets.Length != 0 ||
                recovered.Secrets.Length != 2)
                throw new Exception("Customer projection did not match cryptographic scope.");
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Open(signed, trust, "keys", limitedCert, now, requireFull: true));
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Open(signed, trust, "keys", recoveryCert, now));
            Reject<CryptographicException>(() => GroupVaultService.Unprotect(b.Vault, verified, customerB,
                "keys", [limitedCert], purpose: GroupVaultPurpose.CustomerKeyLibrary));
            Reject<CryptographicException>(() => GroupVaultService.Unprotect(a.Vault, verified, customerB,
                "keys", [fullCert], purpose: GroupVaultPurpose.CustomerKeyLibrary));
            Reject<CryptographicException>(() => GroupVaultService.Unprotect(a.Vault, verified, customerA,
                "keys", [fullCert], purpose: GroupVaultPurpose.Project));
            var unsignedChange = KeyLibrarySyncWire.Write(bundle with { Customers = [a] });
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Verify(signed with { Payload = unsignedChange }, enrolled, "keys"));
            var swapped = KeyLibrarySyncWire.Write(bundle with { Customers = [a with { CustomerId = customerB }] });
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Verify(new(swapped,
                authority.SignData(swapped, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)), enrolled, "keys"));
            var next = Enrollment(policy with { Revision = 2, People = [fullPerson, limited with { KeyLibraryCustomers = [customerB] }, none] });
            var rotated = ScopedKeyLibraryVault.Protect(payload, next, authority, "keys", now);
            trust.AcceptPackageUpdate(next, verified.Checkpoint);
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Open(signed, trust, "keys", limitedCert, now));
            if (ScopedKeyLibraryVault.Open(rotated, trust, "keys", limitedCert, now).Secrets.Single().Key.KeyId != keyB.KeyId)
                throw new Exception("Customer scope rotation failed.");
            // Offline revocation is not retroactive: the original signed
            // policy and original ciphertext can still open the old grant.
            _ = GroupVaultService.Unprotect(a.Vault, verified, customerA, "keys", [limitedCert],
                purpose: GroupVaultPurpose.CustomerKeyLibrary);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Unexpected scoped access: " + typeof(T).Name);
    }
}

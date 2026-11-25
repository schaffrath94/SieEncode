using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class KeyLibraryPolicyTransitionTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-policy-transition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var retainedKey = RSA.Create(3072);
        using var removedKey = RSA.Create(3072);
        using var addedKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        using var wrongRoot = RSA.Create(3072);
        X509Certificate2 MakeCert(string name, RSA key) =>
            new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        using var retained = MakeCert("Retained synthetic member", retainedKey);
        using var removed = MakeCert("Removed synthetic member", removedKey);
        using var added = MakeCert("Added synthetic member", addedKey);
        using var recovery = MakeCert("Synthetic recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject,
            CertificateVaultService.Fingerprint(cert), Convert.ToBase64String(cert.RawData), true);
        OfflineVaultPerson Person(string name, X509Certificate2 cert) =>
            new(name, name, true, [Device(cert)]);
        var member = Person("retained", retained);
        var removedPerson = Person("removed", removed);
        var addedPerson = Person("added", added);

        try
        {
            var administration = new OfflineGroupAdministrationStore(Path.Combine(directory, "admin.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            var initial = administration.Create();
            var configured = administration.UpdateDirectory([member, removedPerson],
                [new OfflineVaultGroup("keys", "Keys", ["retained", "removed"])],
                GroupEnrollmentPackages.Validate(initial).Checkpoint);
            var old = administration.UpdateRecoveryDevices([Device(recovery)],
                GroupEnrollmentPackages.Validate(configured).Checkpoint);
            var oldPolicy = GroupEnrollmentPackages.Validate(old);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(old.AdministratorPublicKey);
            trust.Enroll(old.AdministratorPublicKey, fingerprint, old.Policy);
            var localPath = Path.Combine(directory, "local.json");
            var customer = Guid.NewGuid();
            var key = Guid.NewGuid();
            // All test data below is synthetic.
            var payload = new KeyLibraryVaultPayload(new KeyLibrarySnapshot(id, 1,
                [new KeyLibraryEntry(customer, key, 1, "Test key", "DF_KEY_AES",
                    "PICC", "Test", KeyLibraryContracts.Active)])
                { Customers = [new(customer, "Test customer")] },
                [new(new(key, 1), new string('A', 32))]);
            var authorization = administration.SignKeyLibraryMutation(trust,
                "create", "keys", FileRevision.Missing, payload);
            var originalFile = KeyLibraryVaultStore.Create(localPath, payload, trust, "keys",
                [retained], now, authorization);
            var centralPath = Path.Combine(directory, "central.json");
            var center = new CentralKeyLibraryStore(centralPath, id);
            var empty = center.Register(old, fingerprint);
            var first = KeyLibrarySyncClient.PrepareUpload(localPath, trust,
                administration, "keys", empty, [retained]);
            var before = center.Accept(first);
            var next = administration.UpdateDirectory([member, addedPerson],
                [new OfflineVaultGroup("keys", "Keys", ["retained", "added"])],
                oldPolicy.Checkpoint);
            var nextPolicy = GroupEnrollmentPackages.Validate(next);

            Reject<InvalidOperationException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, retained, now, false));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, removed, now, true));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, added, now, true));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before with { CiphertextSha256 = new string('B', 64) }, first, retained, now, true));
            var transition = administration.PrepareKeyLibraryPolicyTransition(trust, before,
                first, retained, now, true);
            if (trust.LoadOffline().Checkpoint != oldPolicy.Checkpoint ||
                ConcurrentFileWriter.Capture(localPath) != originalFile ||
                center.ReadHead() != before)
                throw new Exception("Preparation changed trusted local or central data before acceptance.");
            var claim = KeyLibraryPolicyTransition.Verify(transition, old, before);
            if (claim.CatalogRevision != before.CatalogRevision ||
                claim.Policy != nextPolicy.Checkpoint || claim.NextCiphertextSha256 == before.CiphertextSha256)
                throw new Exception("Policy-only transition changed logical revision or reused old encryption.");

            Reject<CryptographicException>(() => center.Accept(transition.Rewrapped));
            Reject<CryptographicException>(() => center.AcceptPolicyTransition(transition with
            {
                Enrollment = next with
                {
                    AdministratorPublicKey = wrongRoot.ExportSubjectPublicKeyInfo(),
                    Policy = OfflineGroupPolicyService.Sign(nextPolicy.Policy, wrongRoot)
                }
            }));
            Reject<CryptographicException>(() => center.AcceptPolicyTransition(transition with
            {
                Rewrapped = transition.Rewrapped with { Signature = new byte[384] }
            }));
            Reject<ConcurrentFileChangeException>(() => KeyLibraryPolicyTransition.Verify(
                transition, old, before with { CentralRevision = before.CentralRevision + 1 }));
            Reject<ConcurrentFileChangeException>(() => KeyLibraryPolicyTransition.Verify(
                transition, old, before with { CatalogRevision = before.CatalogRevision + 1 }));
            if (center.ReadHead() != before || !center.IsAuthorizedTlsMember(removed, now) ||
                center.IsAuthorizedTlsMember(added, now))
                throw new Exception("Rejected transition changed membership or central revision.");

            var after = center.AcceptPolicyTransition(transition);
            var restarted = new CentralKeyLibraryStore(centralPath, id);
            if (after.CentralRevision != before.CentralRevision + 1 ||
                after.CatalogRevision != before.CatalogRevision ||
                after.Policy != nextPolicy.Checkpoint || restarted.ReadHead() != after ||
                !restarted.IsAuthorizedTlsMember(retained, now) ||
                !restarted.IsAuthorizedTlsMember(added, now) ||
                restarted.IsAuthorizedTlsMember(removed, now))
                throw new Exception("Central policy and recipient transition was not durable and atomic.");
            Reject<CryptographicException>(() => restarted.AcceptPolicyTransition(transition));
            Reject<CryptographicException>(() => restarted.ReadEncryptedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(after, removed, now), now));
            Reject<CryptographicException>(() => restarted.ReadEncryptedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(before, retained, now), now));
            var downloaded = restarted.ReadEncryptedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(after, added, now), now);
            var rewrapped = GroupVaultService.Read(downloaded.EncryptedVault);
            var recovered = KeyLibraryVault.Unprotect(rewrapped, nextPolicy, "keys", [added]);
            if (recovered.Catalog.Revision != payload.Catalog.Revision ||
                recovered.Catalog.Customers.Single() != payload.Catalog.Customers.Single() ||
                recovered.Catalog.Keys.Single() != payload.Catalog.Keys.Single() ||
                recovered.Secrets.Single() != payload.Secrets.Single())
                throw new Exception("Re-encryption did not preserve the complete catalog.");
            if (rewrapped.Header.KeyGeneration == GroupVaultService.Read(first.EncryptedVault).Header.KeyGeneration)
                throw new Exception("Policy change reused the previous encryption generation.");
            _ = KeyLibraryVault.Unprotect(rewrapped, nextPolicy, "keys", [recovery], recovery: true);
            Reject<CryptographicException>(() => KeyLibraryVault.Unprotect(rewrapped, nextPolicy, "keys", [removed]));
            // Document the unavoidable offline limitation: an already retained
            // historical ciphertext and matching old policy are still usable.
            _ = KeyLibraryVault.Unprotect(GroupVaultService.Read(first.EncryptedVault), oldPolicy, "keys", [removed]);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T) { return; }
        throw new Exception("Invalid policy transition was accepted: " + typeof(T).Name);
    }
}

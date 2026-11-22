using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using RFiDGear.Builder.Core;

internal static class CentralKeyLibrarySyncTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "SieEncode-headless-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var admin = RSA.Create(3072);
        using var stranger = RSA.Create(3072);
        using var memberKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        X509Certificate2 MakeCert(string name, RSA key) => new CertificateRequest(
            "CN=" + name, key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-2), now.AddDays(2));
        using var member = MakeCert("Synthetic central member", memberKey);
        using var recovery = MakeCert("Synthetic central recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject,
            CertificateVaultService.Fingerprint(cert),
            Convert.ToBase64String(cert.RawData), true);

        var library = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var key = Guid.NewGuid();
        const string secret = "1234567890ABCDEF0123456789ABCDEF";
        var catalog = new KeyLibraryVaultPayload(new KeyLibrarySnapshot(library, 1,
            [new KeyLibraryEntry(customer, key, 1, "SIPORT Test",
                "DF_KEY_AES", "PICC", "SIPORT", KeyLibraryContracts.Active)]),
            [new(new KeyLibraryAddress(key, 1), secret)]);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2,
            library, 1, [new OfflineVaultGroup("keys", "Keys", ["member"])])
        {
            People = [new("member", "Synthetic Member", true, [Device(member)])],
            RecoveryDevices = [Device(recovery)]
        };
        var enrolled = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1,
            library, admin.ExportSubjectPublicKeyInfo(),
            OfflineGroupPolicyService.Sign(policy, admin));
        var checkedPolicy = GroupEnrollmentPackages.Validate(enrolled);
        var root = OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey);
        var centralPath = Path.Combine(directory, "central.json");
        var center = new CentralKeyLibraryStore(centralPath, library);

        try
        {
            Reject<CryptographicException>(() => center.Register(enrolled, new string('A', 64)));
            if (File.Exists(centralPath))
                throw new Exception("Unconfirmed central administrator enrolled.");
            var before = center.Register(enrolled, root);
            if (before.CentralRevision != 0 || before.CatalogRevision != 0 ||
                before.CiphertextSha256 != "" || before.GroupId != "")
                throw new Exception("Headless store did not start at an empty authoritative revision.");
            Reject<ConcurrentFileChangeException>(() => center.Register(enrolled, root));
            Reject<FileNotFoundException>(() => center.ReadEncryptedSnapshot(
                new SignedKeyLibraryDownload([], []), now));

            var encrypted = GroupVaultService.Write(
                KeyLibraryVault.Protect(catalog, checkedPolicy, "keys", now));
            if (Encoding.UTF8.GetString(encrypted).Contains(secret, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Central transport leaked a raw key.");
            var claim = KeyLibrarySyncTransport.Prepare(enrolled, "keys",
                before.CentralRevision, before.CiphertextSha256, catalog.Catalog.Revision, encrypted);
            var signed = KeyLibrarySyncTransport.Sign(claim, encrypted, admin);
            if (Encoding.UTF8.GetString(signed.Claim).Contains(secret, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Signed transport claim included the card secret.");
            if (KeyLibrarySyncTransport.Verify(signed, enrolled) != claim)
                throw new Exception("Canonical encrypted transport signature does not round-trip.");

            var tamperedDocument = GroupVaultService.Read(encrypted);
            var tamperedPayload = tamperedDocument.Ciphertext.ToArray();
            tamperedPayload[0] ^= 0x01;
            var changed = GroupVaultService.Write(tamperedDocument with { Ciphertext = tamperedPayload });
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Verify(signed with { EncryptedVault = changed }, enrolled));
            var changedSignature = signed.Signature.ToArray();
            changedSignature[0] ^= 0x01;
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Verify(signed with { Signature = changedSignature }, enrolled));
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Verify(
                    KeyLibrarySyncTransport.Sign(claim, encrypted, stranger), enrolled));
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Verify(
                    KeyLibrarySyncTransport.Sign(claim with { GroupId = "another" },
                        encrypted, admin), enrolled));
            var projectVault = GroupVaultService.Write(
                GroupVaultService.Protect("{\"not-key-library\":true}",
                    checkedPolicy, library, "keys", now));
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", 0, "", 1, projectVault));

            var envelope = GroupVaultService.Read(encrypted);
            var altered = GroupVaultService.Write(envelope with
            {
                Header = envelope.Header with
                {
                    Recipients = [envelope.Header.Recipients[0] with
                        { Fingerprint = new string('B', 64) },
                        envelope.Header.Recipients[1]]
                }
            });
            // Even the genuine administrator must not upload a recipient list
            // contradicting the separately signed group policy.
            Reject<CryptographicException>(() =>
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", 0, "", 1, altered));

            var written = center.Accept(signed);
            if (written.CentralRevision != 1 || written.CatalogRevision != 1 ||
                written.CiphertextSha256 != KeyLibrarySyncTransport.CiphertextSha256(encrypted))
                throw new Exception("Central store failed to accept authenticated encrypted Vault.");
            if (File.ReadAllText(centralPath).Contains(secret, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Headless central state persisted a plaintext card secret.");
            Reject<ConcurrentFileChangeException>(() => center.Accept(signed));
            var unchangedRevisionUpload = KeyLibrarySyncTransport.Sign(
                KeyLibrarySyncTransport.Prepare(enrolled, "keys",
                    written.CentralRevision, written.CiphertextSha256,
                    catalog.Catalog.Revision, encrypted), encrypted, admin);
            Reject<ConcurrentFileChangeException>(() => center.Accept(unchangedRevisionUpload));

            var persisted = new CentralKeyLibraryStore(centralPath, library);
            if (!persisted.IsAuthorizedTlsMember(member, now) ||
                persisted.IsAuthorizedTlsMember(recovery, now))
                throw new Exception("TLS certificate authorization did not enforce signed group membership.");
            var memberRead = KeyLibraryDownloadAuthorization.Sign(written, member, now);
            var memberDownload = persisted.ReadEncryptedSnapshot(memberRead, now);
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(memberRead, now));
            var persistedAfterRead = new CentralKeyLibraryStore(centralPath, library);
            Reject<CryptographicException>(() => persistedAfterRead.ReadEncryptedSnapshot(memberRead, now));
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "member-trust.bin"),
                library, new OfflineGroupTrustTests.TestProtection());
            trust.Enroll(enrolled.AdministratorPublicKey, root, enrolled.Policy);
            var offlineCopyPath = Path.Combine(directory, "offline-key-library.json");
            var offlineRevision = KeyLibrarySyncClient.InstallDownloadedSnapshot(
                offlineCopyPath, trust, "keys", written, memberDownload,
                [member], FileRevision.Missing);
            if (KeyLibraryVaultStore.Load(offlineCopyPath, trust, "keys", [member])
                .Secrets.Single().HexValue != secret)
                throw new Exception("First signed central download did not become a usable offline library.");
            if (persisted.ReadHead() != written ||
                !memberDownload.EncryptedVault.SequenceEqual(encrypted))
                throw new Exception("Headless central state lost authenticated revision across restart.");
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(
                new SignedKeyLibraryDownload(memberRead.Claim, new byte[384]), now));
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(
                memberRead, now.AddHours(1)));
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(written, recovery, now), now));
            using var unknownIdentity = MakeCert("Unregistered key", stranger);
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(written, unknownIdentity, now), now));

            var second = KeyLibraryEditing.AddCustomer(catalog, "Synthetic customer").Next;
            var encrypted2 = GroupVaultService.Write(
                KeyLibraryVault.Protect(second, checkedPolicy, "keys", now));
            var next = KeyLibrarySyncTransport.Sign(
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", written.CentralRevision,
                    written.CiphertextSha256, second.Catalog.Revision, encrypted2), encrypted2, admin);
            var competing = KeyLibrarySyncTransport.Sign(
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", written.CentralRevision,
                    written.CiphertextSha256, catalog.Catalog.Revision, encrypted), encrypted, admin);
            var written2 = persisted.Accept(next);
            if (written2.CentralRevision != 2 || written2.CatalogRevision != 2 ||
                written2.CiphertextSha256 != KeyLibrarySyncTransport.CiphertextSha256(encrypted2))
                throw new Exception("Second central revision did not advance.");
            Reject<ConcurrentFileChangeException>(() => center.Accept(competing));
            Reject<ConcurrentFileChangeException>(() => persisted.Accept(next));
            Reject<CryptographicException>(() => persisted.ReadEncryptedSnapshot(memberRead, now));
            var currentMemberRead = KeyLibraryDownloadAuthorization.Sign(written2, member, now);
            var actual = persisted.ReadEncryptedSnapshot(currentMemberRead, now);
            Reject<InvalidDataException>(() => KeyLibrarySyncClient.InstallDownloadedSnapshot(
                offlineCopyPath, trust, "keys", written, memberDownload,
                [member], offlineRevision));
            var nextOfflineRevision = KeyLibrarySyncClient.InstallDownloadedSnapshot(
                offlineCopyPath, trust, "keys", written2, actual, [member], offlineRevision);
            if (nextOfflineRevision == offlineRevision ||
                KeyLibraryVaultStore.Load(offlineCopyPath, trust, "keys", [member])
                    .Catalog.Customers.Single().Name != "Synthetic customer")
                throw new Exception("Second signed central download did not preserve offline customer and key history.");
            var recovered = KeyLibraryVault.Unprotect(
                GroupVaultService.Read(actual.EncryptedVault), checkedPolicy, "keys", [member]);
            if (recovered.Catalog.Customers.Single().Name != "Synthetic customer" ||
                recovered.Secrets.Single().HexValue != secret)
                throw new Exception("Authorized offline receiver could not recover entire latest encrypted history.");
            if (persisted.ReadHead() != written2)
                throw new Exception("Rejected competing writes changed the central head.");
            VerifyStudioSigningBridge();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void VerifyStudioSigningBridge()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "SieEncode-signing-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var memberKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        X509Certificate2 MakeCert(string subject, RSA key) =>
            new CertificateRequest("CN=" + subject, key,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        using var member = MakeCert("Bridge member", memberKey);
        using var recovery = MakeCert("Bridge recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject,
            CertificateVaultService.Fingerprint(cert),
            Convert.ToBase64String(cert.RawData), true);

        try
        {
            var administration = new OfflineGroupAdministrationStore(
                Path.Combine(directory, "admin.bin"), id,
                new OfflineGroupTrustTests.TestProtection());
            var initial = administration.Create();
            var configured = administration.UpdateDirectory(
                [new OfflineVaultPerson("member", "Bridge Member", true,
                    [Device(member)])],
                [new OfflineVaultGroup("keys", "Bridge Keys", ["member"])],
                GroupEnrollmentPackages.Validate(initial).Checkpoint);
            var ready = administration.UpdateRecoveryDevices(
                [Device(recovery)], GroupEnrollmentPackages.Validate(configured).Checkpoint);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            trust.Enroll(ready.AdministratorPublicKey,
                OfflineGroupTrustStore.AdministratorFingerprint(ready.AdministratorPublicKey),
                ready.Policy);

            var localVault = Path.Combine(directory, "local-library.json");
            var payload = new KeyLibraryVaultPayload(new KeyLibrarySnapshot(id, 1, []), []);
            var localAuthorization = administration.SignKeyLibraryMutation(trust,
                "create", "keys", FileRevision.Missing, payload);
            KeyLibraryVaultStore.Create(localVault, payload, trust, "keys",
                [member], now, localAuthorization);
            var center = new CentralKeyLibraryStore(
                Path.Combine(directory, "central.json"), id);
            var confirmed = OfflineGroupTrustStore.AdministratorFingerprint(
                ready.AdministratorPublicKey);
            var first = center.Register(ready, confirmed);
            Reject<CryptographicException>(() => KeyLibrarySyncClient.PrepareUpload(
                localVault, trust, administration, "keys", first, [recovery]));
            var upload = KeyLibrarySyncClient.PrepareUpload(
                localVault, trust, administration, "keys", first, [member]);
            var approved = center.Accept(upload);
            if (approved.CentralRevision != 1 ||
                KeyLibrarySyncTransport.Verify(upload, ready).LibraryId != id)
                throw new Exception("Existing Studio administrator did not authorize an exact encrypted central upload.");
            Reject<ConcurrentFileChangeException>(() => center.Accept(upload));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Invalid headless encrypted sync was accepted: " + typeof(T).Name);
    }
}

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
            var proposed = oldPolicy.Policy with
            {
                Revision = checked(oldPolicy.Policy.Revision + 1),
                People = [member, addedPerson],
                Groups = [new OfflineVaultGroup("keys", "Keys", ["retained", "added"])]
            };

            Reject<InvalidOperationException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, retained, now, false, proposed));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, removed, now, true, proposed));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before, first, added, now, true, proposed));
            Reject<CryptographicException>(() => administration.PrepareKeyLibraryPolicyTransition(
                trust, before with { CiphertextSha256 = new string('B', 64) }, first, retained, now, true, proposed));
            var transition = administration.PrepareKeyLibraryPolicyTransition(trust, before,
                first, retained, now, true, proposed);
            var next = transition.Enrollment;
            var nextPolicy = GroupEnrollmentPackages.Validate(next);
            if (GroupEnrollmentPackages.Validate(administration.ExportPublic()).Checkpoint != oldPolicy.Checkpoint)
                throw new Exception("Staging prematurely changed administrator state.");
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

            Reject<CryptographicException>(() => center.AcceptPolicyTransition(transition, removed, now));
            Reject<CryptographicException>(() => center.AcceptPolicyTransition(transition, added, now));
            var cursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            cursor.RememberReconciled(before, originalFile);
            var journalPath = Path.Combine(directory, "transition.bin");
            var journalProtection = new OfflineGroupTrustTests.TestProtection();
            var journal = new KeyLibraryPolicyTransitionJournal(journalPath, id, localPath, journalProtection);
            Reject<InvalidOperationException>(() => journal.Stage(trust, cursor, before,
                transition, retained, false));
            journal.Stage(trust, cursor, before, transition, retained, true);
            if (!journal.HasPending || center.ReadHead() != before ||
                ConcurrentFileWriter.Capture(localPath) != originalFile)
                throw new Exception("Staged intent modified live data before central acknowledgement.");
            Reject<InvalidOperationException>(() => journal.Stage(trust, cursor, before,
                transition, retained, true));
            Reject<CryptographicException>(() => _ = new KeyLibraryPolicyTransitionJournal(
                journalPath, id, localPath, new OfflineGroupTrustTests.TestProtection()).HasPending);
            var transport = new LostReplyTransport(center, retained, now);
            transport.HeadOverride = before with { CentralRevision = before.CentralRevision + 1,
                CatalogRevision = before.CatalogRevision + 1 };
            Reject<ConcurrentFileChangeException>(() => journal.ResumeAsync(transport, trust, cursor, retained, administration: administration)
                .GetAwaiter().GetResult());
            if (!journal.HasPending || transport.Submissions != 0)
                throw new Exception("Competing central head caused a new submission.");
            transport.HeadOverride = null;
            var originalBytes = File.ReadAllBytes(localPath);
            File.WriteAllBytes(localPath, [1, 2, 3]);
            Reject<ConcurrentFileChangeException>(() => journal.ResumeAsync(transport, trust, cursor, retained, administration: administration)
                .GetAwaiter().GetResult());
            if (!journal.HasPending || transport.Submissions != 0)
                throw new Exception("Independent local edit was overwritten or uploaded.");
            File.WriteAllBytes(localPath, originalBytes);
            Reject<IOException>(() => journal.ResumeAsync(transport, trust, cursor, retained, administration: administration)
                .GetAwaiter().GetResult());
            if (!journal.HasPending || trust.LoadOffline().Checkpoint != oldPolicy.Checkpoint ||
                ConcurrentFileWriter.Capture(localPath) != originalFile || transport.Submissions != 1)
                throw new Exception("Lost response discarded pending intent or changed local state.");
            // Simulate a second interruption after local trust installation
            // but before the vault/cursor updates, then reconstruct the journal.
            trust.AcceptPackageUpdate(next, oldPolicy.Checkpoint);
            var reopened = new KeyLibraryPolicyTransitionJournal(journalPath, id, localPath, journalProtection);
            var after = reopened.ResumeAsync(transport, trust, cursor, retained, administration: administration).GetAwaiter().GetResult();
            if (reopened.HasPending || transport.Submissions != 1 ||
                administration.RequireLocalAuthority(trust) != nextPolicy.Checkpoint ||
                trust.LoadOffline().Checkpoint != nextPolicy.Checkpoint ||
                ConcurrentFileWriter.Capture(localPath).Sha256 != after.CiphertextSha256 ||
                !cursor.HasUploadBase(after) ||
                !cursor.HasUnchangedLocalBase(ConcurrentFileWriter.Capture(localPath)))
                throw new Exception("Restart did not complete the exact prepared operation once.");
            Reject<CryptographicException>(() => cursor.RequireNotRolledBack(before));
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
            // A second workstation has only a personal certificate and pinned
            // public trust. No administrator private store is used.
            var memberTrust = new OfflineGroupTrustStore(Path.Combine(directory, "member-trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            memberTrust.Enroll(old.AdministratorPublicKey, fingerprint, old.Policy);
            var memberPath = Path.Combine(directory, "member-vault.json");
            var memberFile = KeyLibrarySyncClient.InstallDownloadedSnapshot(memberPath,
                memberTrust, "keys", before, first, [retained], FileRevision.Missing);
            var memberCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "member-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            memberCursor.RememberReconciled(before, memberFile);
            var memberProof = KeyLibraryDownloadAuthorization.Sign(after, retained, now);
            Reject<CryptographicException>(() => restarted.ReadAuthorizedSnapshot(memberProof, added, now));
            Reject<CryptographicException>(() => restarted.ReadAuthorizedSnapshot(
                KeyLibraryDownloadAuthorization.Sign(after, removed, now), removed, now));
            var snapshot = restarted.ReadAuthorizedSnapshot(memberProof, retained, now);
            Reject<CryptographicException>(() => restarted.ReadAuthorizedSnapshot(memberProof, retained, now));
            var memberJournalPath = Path.Combine(directory, "member-transition.bin");
            var memberProtection = new OfflineGroupTrustTests.TestProtection();
            var memberJournal = new KeyLibraryPolicyTransitionJournal(memberJournalPath, id, memberPath, memberProtection);
            Reject<CryptographicException>(() => memberJournal.StageReceived(memberTrust, memberCursor,
                before, snapshot with { Head = after with { CatalogRevision = after.CatalogRevision + 1 } }, retained));
            Reject<CryptographicException>(() => memberJournal.StageReceived(memberTrust, memberCursor,
                before, snapshot, removed));
            memberJournal.StageReceived(memberTrust, memberCursor, before, snapshot, retained);
            if (!memberJournal.HasPending || !memberJournal.IsReceiveOnly ||
                memberTrust.LoadOffline().Checkpoint != oldPolicy.Checkpoint)
                throw new Exception("Member adoption was not staged read-only.");
            var memberTransport = new LostReplyTransport(restarted, retained, now);
            memberTransport.HeadOverride = before;
            Reject<ConcurrentFileChangeException>(() => memberJournal.ResumeAsync(memberTransport,
                memberTrust, memberCursor, retained).GetAwaiter().GetResult());
            if (memberTransport.Submissions != 0)
                throw new Exception("Receive-only member tried to submit a policy write.");
            memberTransport.HeadOverride = null;
            memberTrust.AcceptPackageUpdate(next, oldPolicy.Checkpoint);
            var reopenedMember = new KeyLibraryPolicyTransitionJournal(memberJournalPath, id,
                memberPath, memberProtection);
            var memberResult = reopenedMember.ResumeAsync(memberTransport, memberTrust, memberCursor,
                retained).GetAwaiter().GetResult();
            if (memberResult != after || memberTransport.Submissions != 0 ||
                reopenedMember.HasPending || !memberCursor.HasUploadBase(after) ||
                KeyLibraryVaultStore.Load(memberPath, memberTrust, "keys", [retained]).Secrets.Single() != payload.Secrets.Single())
                throw new Exception("Member without private administrator could not resume signed read-only policy adoption.");
            Reject<CryptographicException>(() => memberCursor.RequireNotRolledBack(before));

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
            // A workstation may miss the policy change AND subsequent catalog
            // changes. The latest snapshot must retain the original history.
            var laggingTrust = new OfflineGroupTrustStore(Path.Combine(directory, "lagging-trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            laggingTrust.Enroll(old.AdministratorPublicKey, fingerprint, old.Policy);
            var laggingPath = Path.Combine(directory, "lagging-vault.json");
            var laggingFile = KeyLibrarySyncClient.InstallDownloadedSnapshot(laggingPath,
                laggingTrust, "keys", before, first, [retained], FileRevision.Missing);
            var laggingCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "lagging-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            laggingCursor.RememberReconciled(before, laggingFile);
            var newerCatalog = KeyLibraryEditing.AddCustomer(payload, "Another synthetic customer").Next;
            var encryptedNewer = GroupVaultService.Write(KeyLibraryVault.Protect(newerCatalog, nextPolicy, "keys", now));
            var newerTransfer = administration.SignEncryptedCentralSync(trust, "keys", after,
                newerCatalog.Catalog.Revision, encryptedNewer);
            var newest = restarted.Accept(newerTransfer);
            var latestSnapshot = restarted.ReadAuthorizedSnapshot(KeyLibraryDownloadAuthorization.Sign(
                newest, retained, now), retained, now);
            var laggingJournal = new KeyLibraryPolicyTransitionJournal(Path.Combine(directory, "lagging-transition.bin"),
                id, laggingPath, new OfflineGroupTrustTests.TestProtection());
            laggingJournal.StageReceived(laggingTrust, laggingCursor, before, latestSnapshot, retained);
            var laggingTransport = new LostReplyTransport(restarted, retained, now);
            if (laggingJournal.ResumeAsync(laggingTransport, laggingTrust, laggingCursor, retained)
                    .GetAwaiter().GetResult() != newest || laggingTransport.Submissions != 0 ||
                KeyLibraryVaultStore.Load(laggingPath, laggingTrust, "keys", [retained]).Catalog.Customers.Length != 2)
                throw new Exception("Lagging member did not receive the latest signed catalog without writing centrally.");
            // Document the unavoidable offline limitation: an already retained
            // historical ciphertext and matching old policy are still usable.
            _ = KeyLibraryVault.Unprotect(GroupVaultService.Read(first.EncryptedVault), oldPolicy, "keys", [removed]);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class LostReplyTransport(CentralKeyLibraryStore center,
        X509Certificate2 member, DateTimeOffset now) : IKeyLibraryPolicyTransitionTransport
    {
        public int Submissions { get; private set; }
        public CentralKeyLibraryHead? HeadOverride { get; set; }
        public Task<CentralKeyLibraryHead> ReadHeadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(HeadOverride ?? center.ReadHead());

        public Task<CentralKeyLibraryHead> ChangePolicyAsync(SignedKeyLibraryPolicyTransition transition,
            CancellationToken cancellationToken = default)
        {
            Submissions++;
            _ = center.AcceptPolicyTransition(transition, member, now);
            throw new IOException("Synthetic connection interruption after durable central commit.");
        }
    }

    private static void Reject<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T) { return; }
        throw new Exception("Invalid policy transition was accepted: " + typeof(T).Name);
    }
}

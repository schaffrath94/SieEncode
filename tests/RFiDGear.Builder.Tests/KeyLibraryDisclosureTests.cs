using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class KeyLibraryDisclosureTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-disclosure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var authority = RSA.Create(3072);
        using var viewKey = RSA.Create(3072);
        using var copyKey = RSA.Create(3072);
        using var ordinaryKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        X509Certificate2 Certificate(string name, RSA key) => new CertificateRequest("CN=" + name, key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var view = Certificate("Synthetic viewer", viewKey);
        using var copy = Certificate("Synthetic copier", copyKey);
        using var ordinary = Certificate("Synthetic member", ordinaryKey);
        using var recovery = Certificate("Synthetic recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 c) => new(c.Subject,
            CertificateVaultService.Fingerprint(c), Convert.ToBase64String(c.RawData), true);
        var viewer = new OfflineVaultPerson("viewer", "Viewer", true, [Device(view)]) { AllowKeyLibraryView = true };
        var copier = new OfflineVaultPerson("copier", "Copier", true, [Device(copy)])
            { AllowKeyLibraryView = true, AllowKeyLibraryCopy = true };
        var plain = new OfflineVaultPerson("member", "Member", true, [Device(ordinary)]);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, id, 1,
            [new("keys", "Keys", ["viewer", "copier", "member"])])
            { People = [viewer, copier, plain], RecoveryDevices = [Device(recovery)] };
        try
        {
            Reject<InvalidDataException>(() => OfflineGroupPolicyService.Sign(
                policy with { People = [viewer with { AllowKeyLibraryView = false, AllowKeyLibraryCopy = true }, copier, plain] },
                authority));
            var signed = OfflineGroupPolicyService.Sign(policy, authority);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), id,
                new OfflineGroupTrustTests.TestProtection());
            var publicKey = authority.ExportSubjectPublicKeyInfo();
            trust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), signed);
            var verified = trust.LoadOffline();
            var readAccess = KeyLibraryDisclosure.RequireMember(verified, "keys", view, now);
            var copyAccess = KeyLibraryDisclosure.RequireMember(verified, "keys", copy, now);
            var memberAccess = KeyLibraryDisclosure.RequireMember(verified, "keys", ordinary, now);
            if (!readAccess.CanView || readAccess.CanCopy || !copyAccess.CanView || !copyAccess.CanCopy ||
                memberAccess.CanView || memberAccess.CanCopy || copyAccess.PersonId != copier.Id)
                throw new Exception("Signed disclosure permissions did not remain separate.");
            Reject<CryptographicException>(() => KeyLibraryDisclosure.RequireMember(verified, "keys", recovery, now));
            Reject<CryptographicException>(() => KeyLibraryDisclosure.RequireMember(verified, "keys", view, now.AddDays(2)));
            Reject<CryptographicException>(() => KeyLibraryDisclosure.RequireMember(verified, "missing", copy, now));
            var entry = new KeyLibraryEntry(customer, keyId, 1, "Synthetic key", "DF_KEY_AES", "PICC", "SIPORT", KeyLibraryContracts.Active);
            // Only synthetic test data; not a real card or customer key.
            var secret = new string('E', 32);
            var payload = new KeyLibraryVaultPayload(new(id, 1, [entry])
                { Customers = [new(customer, "Synthetic customer")] }, [new(new(keyId, 1), secret)]);
            var path = Path.Combine(directory, "vault.json");
            var authorization = KeyLibraryMutationAuthorization.Sign(
                KeyLibraryMutationAuthorization.Prepare("create", id, "keys", verified.Checkpoint,
                    FileRevision.Missing, payload), authority);
            var file = KeyLibraryVaultStore.Create(path, payload, trust, "keys", [view], now, authorization);
            string Read(X509Certificate2 cert, bool copying, KeyLibraryEntry? selected = null,
                long revision = 1, FileRevision? expected = null) =>
                KeyLibraryDisclosure.ReadSelected(path, trust, "keys", cert, verified.Checkpoint,
                    revision, expected ?? file, selected ?? entry, copying, now);
            if (Read(view, false) != secret || Read(copy, true) != secret)
                throw new Exception("Authorized exact-version human disclosure failed.");
            Reject<CryptographicException>(() => Read(view, true));
            Reject<CryptographicException>(() => Read(ordinary, false));
            Reject<ConcurrentFileChangeException>(() => Read(copy, false, entry with { CustomerId = Guid.NewGuid() }));
            Reject<ConcurrentFileChangeException>(() => Read(copy, false, entry with { Status = KeyLibraryContracts.Retired }));
            Reject<ConcurrentFileChangeException>(() => Read(copy, false, revision: 2));
            Reject<ConcurrentFileChangeException>(() => Read(copy, false, expected: FileRevision.Missing));
            // Metadata-only local record, protected; no key material argument.
            var auditDirectory = Path.Combine(directory, "audit");
            var protector = new OfflineGroupTrustTests.TestProtection();
            var audit = new KeyLibraryDisclosureEvent(Guid.NewGuid(), now, id, "keys", copyAccess.PersonId,
                copyAccess.DeviceFingerprint, customer, keyId, 1, 1, verified.Checkpoint, "copy-requested");
            KeyLibraryDisclosureAudit.Record(auditDirectory, protector, audit);
            var bytes = File.ReadAllBytes(Path.Combine(auditDirectory, audit.EventId.ToString("N") + ".audit.bin"));
            var raw = protector.Unprotect(bytes);
            if (KeyLibrarySyncWire.Read<KeyLibraryDisclosureEvent>(raw, 64 * 1024) != audit ||
                System.Text.Encoding.UTF8.GetString(raw).Contains(secret, StringComparison.Ordinal))
                throw new Exception("Disclosure evidence contained secret data or wrong attribution.");
            Reject<ConcurrentFileChangeException>(() => KeyLibraryDisclosureAudit.Record(auditDirectory, protector, audit));
            Reject<InvalidDataException>(() => KeyLibraryDisclosureAudit.Record(auditDirectory, protector,
                audit with { EventId = Guid.NewGuid(), Operation = "unknown" }));
            // Signed outbox and central storage: a lost response must not
            // duplicate events or discard the pending report.
            var outbox = Path.Combine(directory, "outbox");
            var central = new CentralKeyLibraryAuditStore(Path.Combine(directory, "central-audit"));
            var signedAudit = KeyLibraryAuditSync.Sign(audit, copy);
            Reject<CryptographicException>(() => KeyLibraryAuditSync.Verify(signedAudit, view));
            Reject<CryptographicException>(() => central.Accept(signedAudit, view, id, "keys", verified, now));
            var damaged = signedAudit with { Signature = new byte[signedAudit.Signature.Length] };
            Reject<CryptographicException>(() => central.Accept(damaged, copy, id, "keys", verified, now));
            var receipt = central.Accept(signedAudit, copy, id, "keys", verified, now);
            var restarted = new CentralKeyLibraryAuditStore(Path.Combine(directory, "central-audit"));
            if (restarted.Accept(signedAudit, copy, id, "keys", verified, now.AddSeconds(1)) != receipt)
                throw new Exception("Duplicate event did not return original durable receipt.");
            Reject<ConcurrentFileChangeException>(() => central.Accept(
                KeyLibraryAuditSync.Sign(audit with { Operation = "view-requested" }, copy),
                copy, id, "keys", verified, now));
            Reject<CryptographicException>(() => central.Accept(
                KeyLibraryAuditSync.Sign(audit with { EventId = Guid.NewGuid(), PersonId = viewer.Id }, copy),
                copy, id, "keys", verified, now));
            var queued = audit with { EventId = Guid.NewGuid() };
            KeyLibraryDisclosureAudit.RecordSigned(outbox, protector, queued, copy);
            var pendingPath = Path.Combine(outbox, queued.EventId.ToString("N") + ".signed-audit.bin");
            if (KeyLibraryDisclosureAudit.FlushAsync(outbox, protector, view,
                (_, _) => throw new Exception("Other device sent audit")).GetAwaiter().GetResult() != 0)
                throw new Exception("Wrong device drained audit.");
            Reject<IOException>(() => KeyLibraryDisclosureAudit.FlushAsync(outbox, protector, copy,
                (report, token) => { _ = central.Accept(report, copy, id, "keys", verified, now);
                    throw new IOException("Synthetic lost reply"); }).GetAwaiter().GetResult());
            if (!File.Exists(pendingPath)) throw new Exception("Lost response removed pending report.");
            Reject<CryptographicException>(() => KeyLibraryDisclosureAudit.FlushAsync(outbox, protector, copy,
                (_, _) => Task.FromResult(receipt)).GetAwaiter().GetResult());
            if (!File.Exists(pendingPath)) throw new Exception("Wrong acknowledgement removed pending report.");
            var sent = KeyLibraryDisclosureAudit.FlushAsync(outbox, protector, copy,
                (report, _) => Task.FromResult(restarted.Accept(report, copy, id, "keys", verified, now)))
                .GetAwaiter().GetResult();
            if (sent != 1 || File.Exists(pendingPath) ||
                !File.Exists(Path.Combine(outbox, "Sent", Path.GetFileName(pendingPath))) ||
                Directory.GetFiles(Path.Combine(directory, "central-audit"), "*.json").Length != 2)
                throw new Exception("Retry did not archive exactly once after central confirmation.");
            // A signed permission reduction must prevent further application reads.
            var next = policy with { Revision = 2, People = [viewer, copier with { AllowKeyLibraryCopy = false }, plain] };
            var updated = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, id,
                publicKey, OfflineGroupPolicyService.Sign(next, authority));
            trust.AcceptPackageUpdate(updated, verified.Checkpoint);
            Reject<CryptographicException>(() => Read(copy, true));
            Reject<ConcurrentFileChangeException>(() => Read(copy, false));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T) { return; }
        throw new Exception("Unexpectedly accepted disclosure request: " + typeof(T).Name);
    }
}

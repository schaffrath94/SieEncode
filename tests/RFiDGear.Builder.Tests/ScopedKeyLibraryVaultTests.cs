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
            // Same file path and same central CAS protocol, without a
            // separate customer database or server-side decryption.
            var encrypted = KeyLibrarySyncWire.Write(signed);
            var central = new CentralKeyLibraryStore(Path.Combine(directory, "central.json"), id);
            var head0 = central.Register(enrolled, OfflineGroupTrustStore.AdministratorFingerprint(publicKey));
            var transfer = KeyLibrarySyncTransport.Sign(KeyLibrarySyncTransport.Prepare(enrolled,
                "keys", head0.CentralRevision, head0.CiphertextSha256, 1, encrypted), encrypted, authority);
            var head1 = central.Accept(transfer);
            var received = central.ReadAuthorizedSnapshot(KeyLibraryDownloadAuthorization.Sign(
                head1, limitedCert, now), limitedCert, now);
            var localPath = Path.Combine(directory, "library.json");
            var file1 = KeyLibrarySyncClient.InstallDownloadedSnapshot(localPath, trust, "keys", head1,
                received.Transfer, [limitedCert], FileRevision.Missing);
            if (file1.Sha256 != head1.CiphertextSha256 ||
                KeyLibraryVaultStore.Load(localPath, trust, "keys", [limitedCert]).Secrets.Single().Key.KeyId != keyA.KeyId)
                throw new Exception("Scoped download did not preserve exact signed bytes or access scope.");
            Reject<CryptographicException>(() => KeyLibraryVaultStore.Load(localPath, trust,
                "keys", [limitedCert], requireFull: true));
            Reject<CryptographicException>(() => KeyLibrarySyncTransport.Prepare(enrolled, "keys",
                head1.CentralRevision, head1.CiphertextSha256, 2, encrypted));
            var nextPayload = KeyLibraryEditing.AddCustomer(payload, "New synthetic customer").Next;
            var bytes2 = KeyLibrarySyncWire.Write(ScopedKeyLibraryVault.Protect(nextPayload, enrolled, authority, "keys", now));
            var transfer2 = KeyLibrarySyncTransport.Sign(KeyLibrarySyncTransport.Prepare(enrolled,
                "keys", head1.CentralRevision, head1.CiphertextSha256, nextPayload.Catalog.Revision, bytes2), bytes2, authority);
            var head2 = central.Accept(transfer2);
            var file2 = KeyLibrarySyncClient.InstallDownloadedSnapshot(localPath, trust, "keys", head2,
                transfer2, [limitedCert], file1);
            if (file2.Sha256 != head2.CiphertextSha256 ||
                KeyLibraryVaultStore.Load(localPath, trust, "keys", [fullCert], requireFull: true).Catalog.Customers.Length != 4 ||
                KeyLibraryVaultStore.Load(localPath, trust, "keys", [limitedCert]).Catalog.Customers.Length != 1)
                throw new Exception("Full and partial offline scopes diverged after same-policy sync.");
            Reject<ConcurrentFileChangeException>(() => central.Accept(transfer2));
            // A signed newer catalog must not silently drop a hidden customer.
            // The member cannot inspect that customer's secrets, but can check
            // the signed complete list of stable customer IDs.
            var missingHidden = new KeyLibraryVaultPayload(nextPayload.Catalog with
            {
                Revision = nextPayload.Catalog.Revision + 1,
                Customers = nextPayload.Catalog.Customers.Where(c => c.CustomerId != customerB).ToArray(),
                Keys = nextPayload.Catalog.Keys.Where(k => k.CustomerId != customerB).ToArray()
            }, nextPayload.Secrets.Where(s => s.Key.KeyId != keyB.KeyId).ToArray());
            var badBytes = KeyLibrarySyncWire.Write(ScopedKeyLibraryVault.Protect(missingHidden, enrolled, authority, "keys", now));
            var badTransfer = KeyLibrarySyncTransport.Sign(KeyLibrarySyncTransport.Prepare(enrolled, "keys",
                head2.CentralRevision, head2.CiphertextSha256, missingHidden.Catalog.Revision, badBytes), badBytes, authority);
            var badHead = new CentralKeyLibraryHead(id, head2.CentralRevision + 1,
                KeyLibrarySyncTransport.CiphertextSha256(badBytes), "keys", verified.Checkpoint, missingHidden.Catalog.Revision);
            Reject<CryptographicException>(() => KeyLibrarySyncClient.InstallDownloadedSnapshot(localPath,
                trust, "keys", badHead, badTransfer, [limitedCert], file2));
            if (ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Rejected hidden-customer deletion changed the local file.");

            // Replacement workstation: the current signed enrollment is independently
            // installed, and a separate trusted observation supplies the exact head.
            var replacementPath = Path.Combine(directory, "replacement.json");
            var replacementTrust = new OfflineGroupTrustStore(Path.Combine(directory, "replacement-trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            replacementTrust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), enrolled.Policy);
            var replacementCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "replacement-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            var replacementJournal = new KeyLibraryPolicyTransitionJournal(Path.Combine(directory, "replacement-journal.bin"),
                id, replacementPath, new OfflineGroupTrustTests.TestProtection());
            var transport = new ReplacementTransport(central, limitedCert);
            KeyLibrarySyncResult Replace(CentralKeyLibraryHead confirmed, X509Certificate2 device) =>
                KeyLibraryAutomaticSync.RestoreMissingWorkstationAsync(transport, replacementPath,
                    replacementTrust, replacementCursor, replacementJournal, "keys", device, confirmed)
                    .GetAwaiter().GetResult();
            Reject<CryptographicException>(() => Replace(head1, limitedCert));
            Reject<CryptographicException>(() => Replace(head2, recoveryCert));
            transport.HeadAfterDownload = head1;
            Reject<ConcurrentFileChangeException>(() => Replace(head2, limitedCert));
            if (File.Exists(replacementPath) || replacementCursor.ReadReconciledHead() is not null)
                throw new Exception("Rejected workstation recovery installed or reconciled a Vault.");
            transport.HeadAfterDownload = null;
            transport.Downloaded = false;
            if (Replace(head2, limitedCert).Central != head2 ||
                replacementCursor.ReadReconciledHead() != head2 ||
                ConcurrentFileWriter.Capture(replacementPath).Sha256 != head2.CiphertextSha256 ||
                KeyLibraryVaultStore.Load(replacementPath, replacementTrust, "keys", [limitedCert]).Secrets.Single().Key.KeyId != keyA.KeyId)
                throw new Exception("Replacement workstation did not receive the exact current authorized scope.");
            Reject<ConcurrentFileChangeException>(() => Replace(head2, limitedCert));
            if (central.ReadHead() != head2 || ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Replacement workstation changed existing operational library.");

            // Independent encrypted archive: full member creates; separate recovery
            // identity verifies every customer, including an empty customer.
            var backupPath = Path.Combine(directory, "library.backup.json");
            var reference = KeyLibraryBackupService.Create(localPath, backupPath, trust, "keys", fullCert, now);
            var result = KeyLibraryBackupService.VerifyRecovery(backupPath, reference, recoveryCert, now);
            if (result.LibraryId != id || result.CatalogRevision != nextPayload.Catalog.Revision ||
                result.CustomerCount != 4 || result.KeyVersionCount != 2)
                throw new Exception("Independent library recovery did not cover the full snapshot.");
            Reject<CryptographicException>(() => KeyLibraryBackupService.Create(localPath,
                Path.Combine(directory, "partial.backup.json"), trust, "keys", limitedCert, now));
            Reject<CryptographicException>(() => KeyLibraryBackupService.VerifyRecovery(backupPath, reference, fullCert, now));
            Reject<CryptographicException>(() => KeyLibraryBackupService.VerifyRecovery(backupPath,
                reference with { CatalogRevision = reference.CatalogRevision + 1 }, recoveryCert, now));
            Reject<CryptographicException>(() => KeyLibraryBackupService.VerifyRecovery(backupPath,
                reference with { AdministratorFingerprint = new string('0', 64) }, recoveryCert, now));
            var damaged = File.ReadAllBytes(backupPath);
            damaged[damaged.Length / 2] ^= 1;
            var damagedPath = Path.Combine(directory, "damaged.backup.json");
            File.WriteAllBytes(damagedPath, damaged);
            Reject<CryptographicException>(() => KeyLibraryBackupService.VerifyRecovery(damagedPath, reference, recoveryCert, now));
            if (ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Backup or recovery verification changed the active vault.");

            // Separate workspace, no installed registration, member key or central server
            // is needed for the recovery exercise. Exercise a historical key version too.
            var historical = KeyLibraryEditing.AddVersion(nextPayload, keyA.KeyId, new string('C', 32)).Next;
            var historyPath = Path.Combine(directory, "history-source.json");
            File.WriteAllBytes(historyPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(historical, enrolled, authority, "keys", now)));
            var historyBackup = Path.Combine(directory, "history-backup.json");
            var historyReference = KeyLibraryBackupService.Create(historyPath, historyBackup, trust,
                "keys", fullCert, now);
            var expectedHistoryBytes = File.ReadAllBytes(historyPath);
            File.Delete(historyPath); // Simulate loss of the original encrypted library.
            var workspace = Path.Combine(directory, "isolated");
            Directory.CreateDirectory(workspace);
            var trustBefore = trust.LoadOffline().Checkpoint;
            var centralBefore = central.ReadHead();
            Reject<CryptographicException>(() => KeyLibraryBackupService.RestoreIsolated(damagedPath,
                reference, recoveryCert, now, workspace));
            Reject<CryptographicException>(() => KeyLibraryBackupService.RestoreIsolated(historyBackup,
                historyReference, limitedCert, now, workspace));
            if (Directory.EnumerateFileSystemEntries(workspace).Any())
                throw new Exception("Rejected recovery left a published or staged result.");
            var isolated = KeyLibraryBackupService.RestoreIsolated(historyBackup, historyReference,
                recoveryCert, now, workspace);
            if (!File.ReadAllBytes(isolated.EncryptedVaultPath).SequenceEqual(expectedHistoryBytes) ||
                isolated.Verification.KeyVersionCount != 3 || isolated.Verification.CustomerCount != 4 ||
                !File.Exists(Path.Combine(isolated.Directory, "NOT-ACTIVATED.json")))
                throw new Exception("Isolated recovery did not reconstruct the complete historical snapshot.");
            var restoredEnrollment = GroupEnrollmentPackages.Read(File.ReadAllBytes(isolated.PublicEnrollmentPath));
            var isolatedTrust = new OfflineGroupTrustStore(Path.Combine(workspace, "synthetic-check-trust.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            isolatedTrust.Enroll(restoredEnrollment.AdministratorPublicKey, historyReference.AdministratorFingerprint,
                restoredEnrollment.Policy);
            var restoredPayload = KeyLibraryVaultStore.Load(isolated.EncryptedVaultPath, isolatedTrust,
                "keys", [recoveryCert], recovery: true, requireFull: true);
            if (restoredPayload.Catalog.Revision != historical.Catalog.Revision ||
                !restoredPayload.Secrets.Any(s => s.Key.KeyId == keyA.KeyId && s.Key.Version == 1 && s.HexValue == new string('A', 32)) ||
                !restoredPayload.Secrets.Any(s => s.Key.KeyId == keyA.KeyId && s.Key.Version == 2 && s.HexValue == new string('C', 32)))
                throw new Exception("Recovered historical versions or values differ.");
            var again = KeyLibraryBackupService.RestoreIsolated(historyBackup, historyReference,
                recoveryCert, now, workspace);
            if (again.Directory == isolated.Directory || ConcurrentFileWriter.Capture(localPath) != file2 ||
                trust.LoadOffline().Checkpoint != trustBefore || central.ReadHead() != centralBefore)
                throw new Exception("Recovery reused an output or modified operational state.");

            var next = Enrollment(policy with { Revision = 2, People = [fullPerson, limited with { KeyLibraryCustomers = [customerB] }, none] });
            var rotated = ScopedKeyLibraryVault.Protect(payload, next, authority, "keys", now);
            trust.AcceptPackageUpdate(next, verified.Checkpoint);
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Open(signed, trust, "keys", limitedCert, now));
            if (ScopedKeyLibraryVault.Open(rotated, trust, "keys", limitedCert, now).Secrets.Single().Key.KeyId != keyB.KeyId)
                throw new Exception("Customer scope rotation failed.");
            // An archived policy can still be verified offline, but that does
            // not enroll or roll back the operational trust checkpoint.
            var oldRecovery = KeyLibraryBackupService.RestoreIsolated(historyBackup, historyReference,
                recoveryCert, now, workspace);
            if (oldRecovery.Verification.CatalogRevision != historical.Catalog.Revision ||
                trust.LoadOffline().Checkpoint == trustBefore)
                throw new Exception("Historical recovery changed the operational policy.");
            // Offline revocation is not retroactive: the original signed
            // policy and original ciphertext can still open the old grant.
            _ = GroupVaultService.Unprotect(a.Vault, verified, customerA, "keys", [limitedCert],
                purpose: GroupVaultPurpose.CustomerKeyLibrary);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class ReplacementTransport(CentralKeyLibraryStore central,
        X509Certificate2 member) : IKeyLibrarySyncTransport
    {
        public CentralKeyLibraryHead? HeadAfterDownload { get; set; }
        public bool Downloaded { get; set; }

        public Task<CentralKeyLibraryHead> ReadHeadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Downloaded && HeadAfterDownload is not null ? HeadAfterDownload! : central.ReadHead());

        public Task<SignedKeyLibrarySync> DownloadAsync(SignedKeyLibraryDownload proof,
            CancellationToken cancellationToken = default)
        {
            var result = central.ReadAuthorizedSnapshot(proof, member, DateTimeOffset.UtcNow).Transfer;
            Downloaded = true;
            return Task.FromResult(result);
        }

        public Task<CentralKeyLibrarySnapshot> DownloadSnapshotAsync(SignedKeyLibraryDownload proof,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CentralKeyLibraryHead> UploadAsync(SignedKeyLibrarySync transfer,
            CancellationToken cancellationToken = default) => throw new Exception("Recovery attempted an upload.");
        public Task<CentralKeyLibraryHead> ChangePolicyAsync(SignedKeyLibraryPolicyTransition transition,
            CancellationToken cancellationToken = default) => throw new Exception("Recovery attempted a policy change.");
        public Task<KeyLibraryAuditReceipt> SubmitAuditAsync(SignedKeyLibraryAudit report,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<KeyLibraryAuditPage> ReadAuditPageAsync(SignedKeyLibraryAuditQuery query,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Unexpected scoped access: " + typeof(T).Name);
    }
}

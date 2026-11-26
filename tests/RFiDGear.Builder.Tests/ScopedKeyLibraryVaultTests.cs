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

            // The independent library archive and central cold backup must
            // describe exactly the same encrypted bytes, not just revision numbers.
            var pairWorkspace = Path.Combine(directory, "pair-cold");
            Directory.CreateDirectory(pairWorkspace);
            var pairCentralPath = Path.Combine(directory, "central.json");
            var pairCentralRevision = ConcurrentFileWriter.Capture(pairCentralPath);
            var rootPin = OfflineGroupTrustStore.AdministratorFingerprint(publicKey);
            var pairCold = CentralKeyLibraryBackupService.CreateCold(pairCentralPath, pairWorkspace, head2, rootPin);
            var pairArchiveRevision = ConcurrentFileWriter.Capture(backupPath);
            var pair = KeyLibraryRecoveryReadiness.VerifyCurrentPair(pairCold.Directory, pairCold.Reference,
                backupPath, reference, head2, rootPin, recoveryCert, now);
            if (pair.ConfirmedHead != head2 || pair.CustomerCount != 4 ||
                pair.KeyVersionCount != 2 || pair.ArchivedAuditCount != pairCold.AuditCount ||
                pair.VerifiedAt != now)
                throw new Exception("Recovery pair did not verify the exact complete central library.");
            Reject<CryptographicException>(() => KeyLibraryRecoveryReadiness.VerifyCurrentPair(pairCold.Directory,
                pairCold.Reference, backupPath, reference, head2 with { CentralRevision = head2.CentralRevision + 1 },
                rootPin, recoveryCert, now));
            Reject<CryptographicException>(() => KeyLibraryRecoveryReadiness.VerifyCurrentPair(pairCold.Directory,
                pairCold.Reference, backupPath, reference, head2, new string('F', 64), recoveryCert, now));
            Reject<CryptographicException>(() => KeyLibraryRecoveryReadiness.VerifyCurrentPair(pairCold.Directory,
                pairCold.Reference, backupPath, reference, head2, rootPin, limitedCert, now));
            var reencryptedPath = Path.Combine(directory, "pair-reencrypted.json");
            File.WriteAllBytes(reencryptedPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(nextPayload, enrolled, authority, "keys", now)));
            var reencryptedArchive = Path.Combine(directory, "pair-reencrypted-backup.json");
            var reencryptedReference = KeyLibraryBackupService.Create(reencryptedPath, reencryptedArchive,
                trust, "keys", fullCert, now);
            // Authentically encrypted, same logical catalog/revision, but not
            // the central ciphertext. It must not pass the matched-pair check.
            Reject<CryptographicException>(() => KeyLibraryRecoveryReadiness.VerifyCurrentPair(pairCold.Directory,
                pairCold.Reference, reencryptedArchive, reencryptedReference, head2, rootPin, recoveryCert, now));
            if (ConcurrentFileWriter.Capture(pairCentralPath) != pairCentralRevision ||
                ConcurrentFileWriter.Capture(backupPath) != pairArchiveRevision ||
                ConcurrentFileWriter.Capture(localPath) != file2 ||
                Directory.GetDirectories(pairWorkspace).Length != 1)
                throw new Exception("Read-only pair verification changed operational state or published a restore.");

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

            // Disaster inventory uses complete authenticated sources, not
            // central revision numbers or the union of partial projections.
            var branchPath = Path.Combine(directory, "competing-offline.json");
            var competing = KeyLibraryEditing.AddVersion(nextPayload, keyA.KeyId, new string('D', 32)).Next;
            competing = competing with { Catalog = competing.Catalog with
            {
                Keys = competing.Catalog.Keys.Select(k => k.KeyId == keyB.KeyId
                    ? k with { Status = KeyLibraryContracts.Compromised } : k).ToArray()
            } };
            File.WriteAllBytes(branchPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(competing, enrolled, authority, "keys", now)));
            var idCentral = Guid.NewGuid(); var idHistory = Guid.NewGuid(); var idBranch = Guid.NewGuid();
            KeyLibraryDisasterSource[] candidates =
            [
                new(idCentral, localPath, enrolled, verified.Checkpoint),
                new(idHistory, isolated.EncryptedVaultPath, enrolled, verified.Checkpoint),
                new(idBranch, branchPath, enrolled, verified.Checkpoint)
            ];
            var inventory = KeyLibraryDisasterAnalysis.Analyze(trust, "keys", fullCert, false, now, candidates, head2);
            var baselineComparison = inventory.Comparisons.Single(c => c.LeftSourceId == idCentral && c.RightSourceId == idHistory);
            var forkComparison = inventory.Comparisons.Single(c => c.LeftSourceId == idHistory && c.RightSourceId == idBranch);
            if (inventory.Sources[0].CentralRelation != KeyLibraryDisasterCentralRelation.ExactKnownCiphertext ||
                inventory.Sources[1].CentralRelation != KeyLibraryDisasterCentralRelation.NewerCatalogUnconfirmed ||
                baselineComparison.VersionsOnlyRight != 1 || !baselineComparison.RightPreservesLeftHistory ||
                baselineComparison.LeftPreservesRightHistory || forkComparison.SecretValueConflicts != 1 ||
                forkComparison.StatusDifferences != 1 || forkComparison.SameLogicalCatalog ||
                forkComparison.LeftPreservesRightHistory || forkComparison.RightPreservesLeftHistory)
                throw new Exception("Disaster inventory incorrectly treated diverging branches as compatible.");
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.Analyze(trust, "keys",
                limitedCert, false, now, candidates, head2));
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.Analyze(trust, "keys",
                fullCert, false, now, [candidates[0] with { ConfirmedPolicy = verified.Checkpoint with { Revision = 2 } }]));
            // The same verified-source boundary feeds a conservative merge
            // preview. A matching central base is mandatory and no file is written.
            var blockedMerge = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert,
                false, now, candidates, head2, idCentral);
            if (blockedMerge.CanPrepareCandidate || blockedMerge.ProposedRevision is not null ||
                !blockedMerge.Issues.Any(i => i.Kind == KeyLibraryMergeIssueKind.SecretValue &&
                    i.KeyId == keyA.KeyId && i.Version == 2) ||
                !blockedMerge.Issues.Any(i => i.Kind == KeyLibraryMergeIssueKind.Status && i.KeyId == keyB.KeyId))
                throw new Exception("Merge preview failed to block immutable or status conflicts.");
            var independent = KeyLibraryEditing.AddVersion(nextPayload, keyB.KeyId, new string('E', 32)).Next;
            var independentPath = Path.Combine(directory, "independent-offline.json");
            File.WriteAllBytes(independentPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(independent, enrolled, authority, "keys", now)));
            KeyLibraryDisasterSource[] compatible =
                [candidates[0], candidates[1], new(Guid.NewGuid(), independentPath, enrolled, verified.Checkpoint)];
            var preview = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert,
                false, now, compatible, head2, idCentral);
            if (!preview.CanPrepareCandidate || preview.Issues.Length != 0 ||
                preview.AddedCustomers != 0 || preview.AddedKeyVersions != 2 ||
                preview.ProposedRevision != historical.Catalog.Revision + 1)
                throw new Exception("Independent additions did not form one safe candidate preview.");
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust,
                "keys", fullCert, false, now, compatible, head2, idHistory));
            // Restore the synthetic test authority into the normal protected
            // administration store, so preparation uses the production signer.
            var mergeAdministration = new OfflineGroupAdministrationStore(Path.Combine(directory, "merge-admin.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            const string syntheticPassphrase = "Synthetic merge fixture password 2026!";
            var syntheticBackup = GroupPrivateKeyBackups.Protect(authority,
                new GroupPrivateKeyContext(id, GroupPrivateKeyBackups.Administrator), syntheticPassphrase);
            mergeAdministration.RestorePrivateBackup(syntheticBackup, syntheticPassphrase, trust);
            // No surviving server is used for disaster candidate preparation.
            // A complete superset of every supplied source is required.
            KeyLibraryDisasterSource[] disasterSources = [candidates[0], candidates[1]];
            var disaster = KeyLibraryDisasterAnalysis.PreviewDisasterCandidate(trust, "keys",
                recoveryCert, now, disasterSources, idHistory, head2);
            if (!disaster.CanPrepare || disaster.ProposedCatalogRevision != historical.Catalog.Revision + 1 ||
                disaster.LastKnownHead != head2)
                throw new Exception("Complete disaster superset was not recognized.");
            if (KeyLibraryDisasterAnalysis.PreviewDisasterCandidate(trust, "keys", recoveryCert,
                    now, disasterSources, idCentral, head2).CanPrepare ||
                KeyLibraryDisasterAnalysis.PreviewDisasterCandidate(trust, "keys", recoveryCert,
                    now, candidates, idHistory, head2).CanPrepare ||
                KeyLibraryDisasterAnalysis.PreviewDisasterCandidate(trust, "keys", recoveryCert,
                    now, [candidates[1]], idHistory, head2).CanPrepare)
                throw new Exception("Incomplete or conflicting disaster history was accepted.");
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PreviewDisasterCandidate(
                trust, "keys", fullCert, now, disasterSources, idHistory, head2));
            KeyLibraryIsolatedDisasterCandidate PrepareDisaster(string hash, bool approved, bool loss,
                X509Certificate2 device) => KeyLibraryDisasterAnalysis.PrepareDisasterCandidate(trust,
                    mergeAdministration, "keys", recoveryCert, device, now, disasterSources, idHistory,
                    head2, hash, approved, loss);
            var disasterHash = disaster.ApprovalSha256();
            Reject<InvalidOperationException>(() => PrepareDisaster(disasterHash, true, false, fullCert));
            Reject<InvalidOperationException>(() => PrepareDisaster(disasterHash, false, true, fullCert));
            Reject<CryptographicException>(() => PrepareDisaster(disasterHash, true, true, limitedCert));
            Reject<CryptographicException>(() => PrepareDisaster(new string('F', 64), true, true, fullCert));
            var isolatedDisaster = PrepareDisaster(disasterHash, true, true, fullCert);
            var disasterOpened = ScopedKeyLibraryVault.OpenVerified(
                KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(isolatedDisaster.EncryptedVault,
                    GroupVaultService.MaximumBytes), isolatedDisaster.Enrollment, "keys", recoveryCert, now,
                requireFull: true, recovery: true);
            if (isolatedDisaster.ApprovedPreviewSha256 != disasterHash ||
                isolatedDisaster.CiphertextSha256 != KeyLibrarySyncTransport.CiphertextSha256(isolatedDisaster.EncryptedVault) ||
                disasterOpened.Catalog.Revision != disaster.ProposedCatalogRevision ||
                disasterOpened.Secrets.Length != 3 ||
                !disasterOpened.Secrets.Any(s => s.Key.KeyId == keyA.KeyId && s.Key.Version == 2 &&
                    s.HexValue == new string('C', 32)) ||
                central.ReadHead() != head2 || ConcurrentFileWriter.Capture(localPath) != file2 ||
                trust.LoadOffline().Checkpoint != verified.Checkpoint)
                throw new Exception("Disaster reconstruction did not remain isolated or lost historical keys.");

            var previewHash = preview.ApprovalSha256(head2);
            KeyLibraryPreparedMerge Prepare(IEnumerable<KeyLibraryDisasterSource> input, string hash, bool approved,
                X509Certificate2 device) => KeyLibraryDisasterAnalysis.PrepareApprovedMerge(trust,
                    mergeAdministration, "keys", device, now, input, head2, idCentral, hash, approved);
            Reject<InvalidOperationException>(() => Prepare(compatible, previewHash, false, fullCert));
            Reject<CryptographicException>(() => Prepare(compatible, new string('0', 64), true, fullCert));
            Reject<CryptographicException>(() => Prepare(compatible, previewHash, true, limitedCert));
            Reject<CryptographicException>(() => Prepare(compatible, previewHash, true, recoveryCert));
            Reject<CryptographicException>(() => Prepare(candidates, blockedMerge.ApprovalSha256(head2), true, fullCert));
            var originalIndependentBytes = File.ReadAllBytes(independentPath);
            // Even logically identical re-encryption is a different approved source.
            File.WriteAllBytes(independentPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(independent, enrolled, authority, "keys", now)));
            Reject<CryptographicException>(() => Prepare(compatible, previewHash, true, fullCert));
            File.WriteAllBytes(independentPath, originalIndependentBytes);
            var prepared = Prepare(compatible, previewHash, true, fullCert);
            var mergedClaim = KeyLibrarySyncTransport.Verify(prepared.Transfer, enrolled);
            var merged = ScopedKeyLibraryVault.Open(KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(
                prepared.Transfer.EncryptedVault, GroupVaultService.MaximumBytes), trust, "keys", fullCert, now, requireFull: true);
            if (prepared.PreviewSha256 != previewHash || prepared.ExpectedHead != head2 ||
                mergedClaim.ExpectedCentralRevision != head2.CentralRevision ||
                mergedClaim.ExpectedCentralCiphertextSha256 != head2.CiphertextSha256 ||
                merged.Catalog.Revision != preview.ProposedRevision || merged.Secrets.Length != 4 ||
                !merged.Secrets.Any(s => s.Key.KeyId == keyA.KeyId && s.Key.Version == 2 && s.HexValue == new string('C', 32)) ||
                !merged.Secrets.Any(s => s.Key.KeyId == keyB.KeyId && s.Key.Version == 2 && s.HexValue == new string('E', 32)) ||
                central.ReadHead() != head2 || ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Approved merge did not produce the exact encrypted candidate, or changed operational state.");

            var noChanges = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert, false, now,
                [candidates[0], candidates[0] with { SourceId = Guid.NewGuid() }], head2, idCentral);
            if (noChanges.CanPrepareCandidate || noChanges.Issues.Length != 0 || noChanges.ProposedRevision is not null)
                throw new Exception("Identical sources unnecessarily created a new revision.");
            var missingPath = Path.Combine(directory, "missing-base-offline.json");
            File.WriteAllBytes(missingPath, badBytes);
            var missing = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert, false, now,
                [candidates[0], new(Guid.NewGuid(), missingPath, enrolled, verified.Checkpoint)], head2, idCentral);
            if (missing.CanPrepareCandidate ||
                !missing.Issues.Any(i => i.Kind == KeyLibraryMergeIssueKind.MissingBaseCustomer) ||
                !missing.Issues.Any(i => i.Kind == KeyLibraryMergeIssueKind.MissingBaseVersion))
                throw new Exception("Merge preview accepted a branch missing base history.");

            var recoveryInventory = KeyLibraryDisasterAnalysis.Analyze(trust, "keys", recoveryCert,
                true, now, candidates);
            if (recoveryInventory.Comparisons.Length != 3 ||
                recoveryInventory.Sources.Any(s => s.CentralRelation != KeyLibraryDisasterCentralRelation.Unknown) ||
                ConcurrentFileWriter.Capture(localPath) != file2 || central.ReadHead() != centralBefore)
                throw new Exception("Read-only disaster inventory changed operational data.");

            // Manual decisions choose existing authenticated metadata and
            // the strictest status; no secret override, even by administration.
            var metadataPath = Path.Combine(directory, "merge-metadata.json");
            var changedMetadata = nextPayload with { Catalog = nextPayload.Catalog with
            {
                Revision = nextPayload.Catalog.Revision + 1,
                Customers = nextPayload.Catalog.Customers.Select(c => c.CustomerId == customerA
                    ? c with { Name = "Synthetic renamed customer" } : c).ToArray(),
                Keys = nextPayload.Catalog.Keys.Select(k => k.KeyId == keyA.KeyId
                    ? k with { Name = "Synthetic renamed key", Status = KeyLibraryContracts.Compromised } : k).ToArray()
            } };
            File.WriteAllBytes(metadataPath, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(changedMetadata, enrolled, authority, "keys", now)));
            var metadataId = Guid.NewGuid();
            KeyLibraryDisasterSource[] metadataSources =
                [candidates[0], new(metadataId, metadataPath, enrolled, verified.Checkpoint)];
            KeyLibraryMergeDecision[] decisions =
            [
                new(KeyLibraryMergeIssueKind.CustomerMetadata, customerA, null, null, metadataId),
                new(KeyLibraryMergeIssueKind.KeyMetadata, null, keyA.KeyId, 1, metadataId),
                new(KeyLibraryMergeIssueKind.Status, null, keyA.KeyId, 1, metadataId)
            ];
            var metadataReview = KeyLibraryDisasterAnalysis.ReviewMerge(trust, mergeAdministration,
                "keys", fullCert, now, metadataSources, head2, idCentral);
            var statusReview = metadataReview.Details.Single(i => i.Issue.Kind == KeyLibraryMergeIssueKind.Status);
            if (metadataReview.Details.Length != 3 ||
                statusReview.Options.Single(o => o.SourceId == idCentral).Allowed ||
                !statusReview.Options.Single(o => o.SourceId == metadataId).Allowed ||
                metadataReview.Details.Single(i => i.Issue.Kind == KeyLibraryMergeIssueKind.CustomerMetadata)
                    .Options.Single(o => o.SourceId == metadataId).Label != "Synthetic renamed customer")
                throw new Exception("Protected review did not present the exact selectable metadata and status.");
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.ReviewMerge(trust,
                mergeAdministration, "keys", limitedCert, now, metadataSources, head2, idCentral));
            var hardReview = KeyLibraryDisasterAnalysis.ReviewMerge(trust, mergeAdministration,
                "keys", fullCert, now, candidates, head2, idCentral);
            if (hardReview.Details.Single(i => i.Issue.Kind == KeyLibraryMergeIssueKind.SecretValue).Options.Length != 0)
                throw new Exception("Hard secret-value conflict presented a choice.");
            var unresolved = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert, false, now,
                metadataSources, head2, idCentral);
            var resolved = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert, false, now,
                metadataSources, head2, idCentral, decisions);
            if (unresolved.CanPrepareCandidate || unresolved.Issues.Length != 3 ||
                !resolved.CanPrepareCandidate || resolved.Decisions.Length != 3 ||
                resolved.AddedCustomers != 0 || resolved.AddedKeyVersions != 0 ||
                resolved.ProposedRevision != changedMetadata.Catalog.Revision + 1)
                throw new Exception("Explicit metadata/status decisions did not produce the expected candidate.");
            Reject<InvalidDataException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, metadataSources, head2, idCentral,
                [decisions[0], decisions[1], decisions[2] with { SourceId = idCentral }]));
            Reject<InvalidDataException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, metadataSources, head2, idCentral, [.. decisions, decisions[0]]));
            Reject<InvalidDataException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, candidates, head2, idCentral,
                [new(KeyLibraryMergeIssueKind.SecretValue, null, keyA.KeyId, 2, idHistory)]));
            var selectedHash = resolved.ApprovalSha256(head2);
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PrepareApprovedMerge(trust,
                mergeAdministration, "keys", fullCert, now, metadataSources, head2, idCentral, selectedHash,
                true, [decisions[0] with { SourceId = idCentral }, decisions[1], decisions[2]]));
            var selectedMerge = KeyLibraryDisasterAnalysis.PrepareApprovedMerge(trust, mergeAdministration,
                "keys", fullCert, now, metadataSources, head2, idCentral, selectedHash, true, decisions);
            var selectedPayload = ScopedKeyLibraryVault.Open(KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(
                selectedMerge.Transfer.EncryptedVault, GroupVaultService.MaximumBytes), trust,
                "keys", fullCert, now, requireFull: true);
            if (selectedPayload.Catalog.Customers.Single(c => c.CustomerId == customerA).Name != "Synthetic renamed customer" ||
                selectedPayload.Catalog.Keys.Single(k => k.KeyId == keyA.KeyId).Name != "Synthetic renamed key" ||
                selectedPayload.Catalog.Keys.Single(k => k.KeyId == keyA.KeyId).Status != KeyLibraryContracts.Compromised ||
                !selectedPayload.Secrets.OrderBy(s => s.Key.KeyId).SequenceEqual(nextPayload.Secrets.OrderBy(s => s.Key.KeyId)))
                throw new Exception("Explicit decisions changed immutable secret values or ignored selected metadata.");

            // Durable merge on a separate synthetic server and local offline branch.
            var mergeServerPath = Path.Combine(directory, "merge-server.json");
            var mergeServer = new CentralKeyLibraryStore(mergeServerPath, id);
            var mergeEmpty = mergeServer.Register(enrolled, OfflineGroupTrustStore.AdministratorFingerprint(publicKey));
            var mergeFirstBytes = KeyLibrarySyncWire.Write(signed);
            var mergeFirst = mergeServer.Accept(KeyLibrarySyncTransport.Sign(
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", mergeEmpty.CentralRevision,
                    mergeEmpty.CiphertextSha256, payload.Catalog.Revision, mergeFirstBytes), mergeFirstBytes, authority));
            var mergeBaseBytes = File.ReadAllBytes(localPath);
            var mergeBase = mergeServer.Accept(KeyLibrarySyncTransport.Sign(
                KeyLibrarySyncTransport.Prepare(enrolled, "keys", mergeFirst.CentralRevision,
                    mergeFirst.CiphertextSha256, nextPayload.Catalog.Revision, mergeBaseBytes), mergeBaseBytes, authority));
            if (mergeBase != head2) throw new Exception("Synthetic merge base differs.");
            var mergeLocal = Path.Combine(directory, "merge-local.json");
            File.Copy(independentPath, mergeLocal);
            var mergeCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "merge-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            mergeCursor.RememberReconciled(head2, file2);
            var mergeProtection = new OfflineGroupTrustTests.TestProtection();
            var journalPath = Path.Combine(directory, "merge-intent.bin");
            var mergeJournal = new KeyLibraryMergeJournal(journalPath, id, mergeLocal, mergeProtection);
            var mergePolicyJournal = new KeyLibraryPolicyTransitionJournal(Path.Combine(directory, "merge-policy.bin"),
                id, mergeLocal, mergeProtection);
            void StageMerge() => mergeJournal.Stage(trust, mergeAdministration, mergeCursor, mergePolicyJournal,
                "keys", fullCert, now, compatible, head2, idCentral, previewHash, true);
            StageMerge();
            Reject<InvalidOperationException>(StageMerge);
            var mergeTransport = new ReplacementTransport(mergeServer, fullCert) { AllowUpload = true, LoseUploadReply = true };
            CentralKeyLibraryHead ResumeMerge(KeyLibraryMergeJournal j) => j.ResumeAsync(mergeTransport,
                trust, mergeAdministration, mergeCursor, mergePolicyJournal, fullCert).GetAwaiter().GetResult();
            File.WriteAllBytes(mergeLocal, File.ReadAllBytes(branchPath));
            Reject<ConcurrentFileChangeException>(() => ResumeMerge(mergeJournal));
            if (mergeTransport.UploadCount != 0 || !mergeJournal.HasPending)
                throw new Exception("Changed local branch allowed merge upload.");
            File.WriteAllBytes(mergeLocal, originalIndependentBytes);
            Reject<IOException>(() => ResumeMerge(mergeJournal));
            if (!mergeJournal.HasPending || mergeTransport.UploadCount != 1 ||
                mergeServer.ReadHead().CentralRevision != head2.CentralRevision + 1)
                throw new Exception("Lost reply did not retain the durable merge intent.");
            var restartedMerge = new KeyLibraryMergeJournal(journalPath, id, mergeLocal, mergeProtection);
            var mergedHead = ResumeMerge(restartedMerge);
            if (mergeTransport.UploadCount != 1 || restartedMerge.HasPending ||
                restartedMerge.CompletedHead != mergedHead || mergeCursor.ReadReconciledHead() != mergedHead ||
                ConcurrentFileWriter.Capture(mergeLocal).Sha256 != mergedHead.CiphertextSha256 ||
                mergeServer.ReadHead() != mergedHead ||
                central.ReadHead() != head2 || ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Restarted merge did not reconcile exactly once.");

            // Actual multi-workstation conflict: the server has advanced,
            // but another local branch still uses the exact old shared base.
            var multiLocal = Path.Combine(directory, "multi-local.json");
            var multiPayload = KeyLibraryEditing.AddCustomer(nextPayload, "Synthetic independent customer").Next;
            File.WriteAllBytes(multiLocal, KeyLibrarySyncWire.Write(
                ScopedKeyLibraryVault.Protect(multiPayload, enrolled, authority, "keys", now)));
            var multiLocalId = Guid.NewGuid();
            var multiRemoteId = Guid.NewGuid();
            KeyLibraryDisasterSource[] multiSources =
            [
                candidates[0],
                new(multiRemoteId, mergeLocal, enrolled, verified.Checkpoint),
                new(multiLocalId, multiLocal, enrolled, verified.Checkpoint)
            ];
            var multiCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "multi-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            multiCursor.RememberReconciled(head2, file2);
            multiCursor.Remember(mergedHead); // ordinary sync saw the conflict
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, multiSources, mergedHead, idCentral));
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, multiSources, mergedHead, idCentral,
                historicalBase: head2 with { CiphertextSha256 = new string('F', 64) }));
            Reject<CryptographicException>(() => KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys",
                fullCert, false, now, [multiSources[0], multiSources[2]], mergedHead, idCentral,
                historicalBase: head2));
            var multiPreview = KeyLibraryDisasterAnalysis.PreviewMerge(trust, "keys", fullCert, false, now,
                multiSources, mergedHead, idCentral, historicalBase: head2);
            if (!multiPreview.CanPrepareCandidate || multiPreview.HistoricalBase != head2 ||
                multiPreview.AddedCustomers != 1 || multiPreview.AddedKeyVersions != 2 ||
                multiCursor.ReadReconciledHead() != head2)
                throw new Exception("Advanced central head was not treated as a distinct branch.");
            var multiJournal = new KeyLibraryMergeJournal(Path.Combine(directory, "multi-merge.bin"),
                id, multiLocal, mergeProtection);
            var multiPolicy = new KeyLibraryPolicyTransitionJournal(Path.Combine(directory, "multi-policy.bin"),
                id, multiLocal, mergeProtection);
            multiJournal.Stage(trust, mergeAdministration, multiCursor, multiPolicy, "keys", fullCert, now,
                multiSources, mergedHead, idCentral, multiPreview.ApprovalSha256(mergedHead), true, historicalBase: head2);
            var multiTransport = new ReplacementTransport(mergeServer, fullCert) { AllowUpload = true };
            var multiResult = multiJournal.ResumeAsync(multiTransport, trust, mergeAdministration,
                multiCursor, multiPolicy, fullCert).GetAwaiter().GetResult();
            var multiInstalled = KeyLibraryVaultStore.Load(multiLocal, trust, "keys", [fullCert], requireFull: true);
            if (multiTransport.UploadCount != 1 || multiJournal.HasPending ||
                multiResult.CentralRevision != mergedHead.CentralRevision + 1 ||
                multiCursor.ReadReconciledHead() != multiResult || multiInstalled.Secrets.Length != 4 ||
                !multiInstalled.Catalog.Customers.Any(c => c.Name == "Synthetic independent customer") ||
                central.ReadHead() != head2 || ConcurrentFileWriter.Capture(localPath) != file2)
                throw new Exception("Advanced-head merge did not preserve both branches.");

            // The common encrypted reference is selected only by the durable
            // reconciled head, not an observed conflict or a newer local edit.
            var refPath = KeyLibraryMergeReferenceStore.Find(multiLocal, multiResult)
                ?? throw new Exception("Merge did not preserve the new reference.");
            if (KeyLibraryMergeReferenceStore.Find(multiLocal, head2) is not null ||
                ConcurrentFileWriter.Capture(refPath).Sha256 != multiResult.CiphertextSha256)
                throw new Exception("Completed merge left the wrong reference.");
            var referenceLocal = Path.Combine(directory, "reference-local.json");
            File.Copy(localPath, referenceLocal);
            var refCursor = new CentralKeyLibraryCursorStore(Path.Combine(directory, "reference-cursor.bin"),
                id, new OfflineGroupTrustTests.TestProtection());
            refCursor.RememberReconciled(head2, file2);
            KeyLibraryMergeReferenceStore.PreserveExistingBase(referenceLocal, refCursor, head2, file2);
            var oldReference = KeyLibraryMergeReferenceStore.Find(referenceLocal, head2)
                ?? throw new Exception("Existing exact base was not preserved.");
            File.WriteAllBytes(referenceLocal, File.ReadAllBytes(independentPath));
            KeyLibraryMergeReferenceStore.PreserveExistingBase(referenceLocal, refCursor, head2,
                ConcurrentFileWriter.Capture(referenceLocal));
            if (ConcurrentFileWriter.Capture(oldReference).Sha256 != head2.CiphertextSha256)
                throw new Exception("Offline edit replaced the reconciled reference.");
            var newReferenceBytes = File.ReadAllBytes(multiLocal);
            Reject<CryptographicException>(() => KeyLibraryMergeReferenceStore.Stage(referenceLocal,
                refCursor, multiResult, File.ReadAllBytes(referenceLocal)));
            KeyLibraryMergeReferenceStore.Stage(referenceLocal, refCursor, multiResult, newReferenceBytes);
            Reject<CryptographicException>(() => KeyLibraryMergeReferenceStore.Finish(referenceLocal, refCursor, multiResult));
            if (KeyLibraryMergeReferenceStore.Find(referenceLocal, head2) != oldReference ||
                KeyLibraryMergeReferenceStore.Find(referenceLocal, multiResult) is null)
                throw new Exception("Staging lost old base before cursor commit.");
            refCursor.Remember(multiResult);
            if (refCursor.ReadReconciledHead() != head2)
                throw new Exception("Head observation replaced the reconciled base.");
            KeyLibraryMergeReferenceStore.Stage(referenceLocal, refCursor, multiResult, newReferenceBytes);
            refCursor.RememberReconciled(multiResult, ConcurrentFileWriter.Capture(multiLocal));
            KeyLibraryMergeReferenceStore.Finish(referenceLocal, refCursor, multiResult);
            if (KeyLibraryMergeReferenceStore.Find(referenceLocal, head2) is not null ||
                KeyLibraryMergeReferenceStore.Find(referenceLocal, multiResult) is null)
                throw new Exception("Reference cleanup did not retain only the committed base.");

            var next = Enrollment(policy with { Revision = 2, People = [fullPerson, limited with { KeyLibraryCustomers = [customerB] }, none] });
            var rotated = ScopedKeyLibraryVault.Protect(payload, next, authority, "keys", now);
            trust.AcceptPackageUpdate(next, verified.Checkpoint);
            Reject<CryptographicException>(() => ScopedKeyLibraryVault.Open(signed, trust, "keys", limitedCert, now));
            if (ScopedKeyLibraryVault.Open(rotated, trust, "keys", limitedCert, now).Secrets.Single().Key.KeyId != keyB.KeyId)
                throw new Exception("Customer scope rotation failed.");
            var historicalInventory = KeyLibraryDisasterAnalysis.Analyze(trust, "keys",
                recoveryCert, true, now, candidates, head2);
            if (historicalInventory.Sources.Any(s => s.CurrentPolicy))
                throw new Exception("Historical policy was classified as current.");
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
        public bool AllowUpload { get; set; }
        public bool LoseUploadReply { get; set; }
        public int UploadCount { get; private set; }
        public Task<CentralKeyLibraryHead> UploadAsync(SignedKeyLibrarySync transfer,
            CancellationToken cancellationToken = default)
        {
            if (!AllowUpload) throw new Exception("Recovery attempted an upload.");
            UploadCount++;
            var result = central.Accept(transfer);
            if (LoseUploadReply)
            {
                LoseUploadReply = false;
                throw new IOException("Synthetic lost merge reply.");
            }
            return Task.FromResult(result);
        }
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

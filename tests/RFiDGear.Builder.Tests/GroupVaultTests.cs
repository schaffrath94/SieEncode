using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class GroupVaultTests
{
    public static void Verify()
    {
        using var root = RSA.Create(3072);
        using var aliceKey = RSA.Create(3072);
        using var bobKey = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        X509Certificate2 Cert(string name, RSA key) => new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var alice = Cert("Synthetic Alice", aliceKey);
        using var bob = Cert("Synthetic Bob", bobKey);
        using var recovery = Cert("Synthetic Recovery", recoveryKey);
        OfflineVaultDevice Device(X509Certificate2 certificate) => new(certificate.Subject, CertificateVaultService.Fingerprint(certificate), Convert.ToBase64String(certificate.RawData), true);
        var library = Guid.NewGuid(); var project = Guid.NewGuid();
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, library, 1,
            [new("lokal", "Lokal", ["alice"]), new("other", "Other", ["alice", "bob"])])
        { People = [new("alice", "Alice", true, [Device(alice)]), new("bob", "Bob", true, [Device(bob)])], RecoveryDevices = [Device(recovery)] };
        VerifiedGroupPolicy Verify(OfflineGroupPolicy p) => OfflineGroupPolicyService.Verify(OfflineGroupPolicyService.Sign(p, root), root, library);
        var trusted = Verify(policy);
        const string payload = "{\"test\":\"synthetic secret\"}";
        var vault = GroupVaultService.Read(GroupVaultService.Write(GroupVaultService.Protect(payload, trusted, project, "lokal", now)));
        string Open(GroupVaultDocument v, X509Certificate2 identity, bool isRecovery = false) => GroupVaultService.Unprotect(v, trusted, project, "lokal", [identity], isRecovery);
        if (Open(vault, alice) != payload || Open(vault, recovery, true) != payload) throw new Exception("Group/recovery roundtrip failed.");
        Reject(() => Open(vault, bob));
        Reject(() => Open(vault, recovery));
        Reject(() => Open(vault, alice, true));
        Reject(() => GroupVaultService.Unprotect(vault, trusted, Guid.NewGuid(), "lokal", [alice]));
        Reject(() => GroupVaultService.Unprotect(vault, trusted, project, "other", [alice]));
        Reject(() => GroupVaultService.Unprotect(vault with { Header = vault.Header with { GroupId = "other" } }, trusted, project, "other", [alice]));
        var forgedProject = Guid.NewGuid();
        Reject(() => GroupVaultService.Unprotect(vault with { Header = vault.Header with { ProjectId = forgedProject } }, trusted, forgedProject, "lokal", [alice]));
        Reject(() => Open(vault with { Header = vault.Header with { ProjectId = Guid.NewGuid() } }, alice));
        Reject(() => Open(vault with { Header = vault.Header with { KeyGeneration = Guid.NewGuid() } }, alice));
        Reject(() => Open(vault with { Header = vault.Header with { LibraryId = Guid.NewGuid() } }, alice));
        var recipient = vault.Header.Recipients[0];
        var wrapped = recipient.WrappedKey.ToArray(); wrapped[0] ^= 1;
        Reject(() => Open(vault with { Header = vault.Header with { Recipients = [recipient with { WrappedKey = wrapped }, vault.Header.Recipients[1]] } }, alice));
        var cipher = vault.Ciphertext.ToArray(); cipher[0] ^= 1;
        Reject(() => Open(vault with { Ciphertext = cipher }, alice));
        Reject(() => Open(vault with { Header = vault.Header with { Recipients = [recipient] } }, alice));
        var serialized = Encoding.UTF8.GetString(GroupVaultService.Write(vault));
        Reject(() => GroupVaultService.Read(Encoding.UTF8.GetBytes(serialized.Insert(1, "\"Nonce\":\"\","))));
        Reject(() => GroupVaultService.Read(Encoding.UTF8.GetBytes(serialized.Insert(1, "\"Unexpected\":true,"))));
        Reject(() => GroupVaultService.Protect(payload, Verify(policy with { RecoveryDevices = [] }), project, "lokal", now));
        Reject(() => Verify(policy with { RecoveryDevices = [Device(alice)] }));
        using var alias = Cert("Same private key different certificate", aliceKey);
        Reject(() => Verify(policy with { RecoveryDevices = [Device(alias)] }));
        using var rootAlias = Cert("Main authority cannot recover", root);
        Reject(() => Verify(policy with { RecoveryDevices = [Device(rootAlias)] }));
        Reject(() => Verify(policy with { Delegations = [new("root-alias", "alice", root.ExportSubjectPublicKeyInfo(), ["lokal"], true)] }));
        Reject(() => GroupVaultService.Protect(payload, trusted, project, "lokal", now.AddDays(2)));

        var nextPolicy = policy with { Revision = 2, Groups = [new("lokal", "Lokal", ["bob"]), policy.Groups[1]] };
        var next = Verify(nextPolicy);
        var orphaned = Verify(policy with { Revision = 2, Groups = [policy.Groups[1]] });
        if (GroupVaultService.Unprotect(vault, orphaned, project, "lokal", [recovery], true) != payload)
            throw new Exception("Separate recovery lost access after group removal.");
        Reject(() => GroupVaultService.Unprotect(vault, orphaned, project, "lokal", [alice]));
        var rotated = GroupVaultService.Protect(payload, next, project, "lokal", now);
        if (rotated.Header.KeyGeneration == vault.Header.KeyGeneration ||
            GroupVaultService.Unprotect(rotated, next, project, "lokal", [bob]) != payload ||
            GroupVaultService.Unprotect(rotated, next, project, "lokal", [recovery], true) != payload)
            throw new Exception("Rotated group/recovery access failed.");
        Reject(() => GroupVaultService.Unprotect(rotated, next, project, "lokal", [alice]));
        Reject(() => GroupVaultService.Unprotect(vault, next, project, "lokal", [alice]));
        Reject(() => GroupVaultService.Unprotect(rotated, trusted, project, "lokal", [bob]));
        // A copied old vault with its old policy remains decryptable: offline revocation cannot recall it.
        if (Open(vault, alice) != payload) throw new Exception("Historical fixture unexpectedly changed.");
        var oldKey = aliceKey.Decrypt(recipient.WrappedKey, RSAEncryptionPadding.OaepSHA256);
        var newKey = bobKey.Decrypt(rotated.Header.Recipients.Single(r => r.Role == "member").WrappedKey, RSAEncryptionPadding.OaepSHA256);
        try { if (oldKey.SequenceEqual(newKey)) throw new Exception("Rotation reused the content key."); }
        finally { CryptographicOperations.ZeroMemory(oldKey); CryptographicOperations.ZeroMemory(newKey); }
        VerifyReconciliation(vault, policy, nextPolicy, root, project, alice, bob, recovery, now, payload);
        VerifyProjectBinding(policy, root, alice, bob, recovery, now);
        VerifyProjectCreation(policy, root, alice, bob, recovery, now);
    }

    private static void VerifyProjectCreation(OfflineGroupPolicy policy, RSA root, X509Certificate2 alice, X509Certificate2 bob, X509Certificate2 recovery, DateTimeOffset now)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-group-creation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), policy.LibraryId, new OfflineGroupTrustTests.TestProtection());
            var publicKey = root.ExportSubjectPublicKeyInfo();
            var current = trust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), OfflineGroupPolicyService.Sign(policy, root));
            var source = new ProjectDefinition(); source.Project.Name = "Original"; source.Project.Id = Guid.NewGuid(); source.Profiles.Base = "synthetic";
            source.Productive.PiccKey = new() { Type = "DF_KEY_AES", Value = new string('9', 32) };
            var keys = ProjectKeyVault.FromProject(source);
            var before = JsonSerializer.Serialize(source, ProjectJson.Options);
            var created = GroupProjectCreation.Create(directory, "New Group", source, keys, trust, "lokal", current.Checkpoint, [alice], now);
            var definition = ProjectJson.Load(created.ProjectPath);
            if (created.ProjectId == source.Project.Id || created.ProjectId != definition.Project.Id || definition.Productive.PiccKey.Value != "" ||
                JsonSerializer.Serialize(source, ProjectJson.Options) != before) throw new Exception("Group creation changed source or leaked secrets/reused ID.");
            if (ProjectGroupVault.Load(created.ProjectPath, definition, trust, [alice]).ProductivePicc.Value != keys.ProductivePicc.Value ||
                ProjectGroupVault.Load(created.ProjectPath, definition, trust, [recovery], true).ProductivePicc.Value != keys.ProductivePicc.Value)
                throw new Exception("Published group project cannot reopen.");
            var copied = GroupProjectCreation.CopyGroup(directory, "Group Copy", created.ProjectPath, created.ProjectRevision, created.VaultRevision,
                trust, current.Checkpoint, [alice], now);
            var copyDefinition = ProjectJson.Load(copied.ProjectPath);
            if (copied.ProjectId == created.ProjectId || copyDefinition.Project.GroupVault != definition.Project.GroupVault ||
                ProjectGroupVault.Load(copied.ProjectPath, copyDefinition, trust, [alice]).ProductivePicc.Value != keys.ProductivePicc.Value)
                throw new Exception("Complete group copy lost identity separation, binding or keys.");
            Reject(() => GroupProjectCreation.CopyGroup(directory, "Denied Copy", created.ProjectPath, created.ProjectRevision, created.VaultRevision, trust, current.Checkpoint, [bob], now));
            Reject(() => GroupProjectCreation.CopyGroup(directory, "Recovery Copy", created.ProjectPath, created.ProjectRevision, created.VaultRevision, trust, current.Checkpoint, [recovery], now));
            Reject(() => GroupProjectCreation.CopyGroup(directory, "Stale Copy", created.ProjectPath, FileRevision.Missing, created.VaultRevision, trust, current.Checkpoint, [alice], now));
            var wrongDraft = ProjectJson.WithoutAllSecrets(definition); wrongDraft.Project.GroupVault = new(policy.LibraryId, "other");
            Reject(() => GroupProjectCreation.CopyGroup(directory, "Rebound Copy", created.ProjectPath, created.ProjectRevision, created.VaultRevision,
                trust, current.Checkpoint, [alice], now, draft: wrongDraft));
            wrongDraft.Project.GroupVault = definition.Project.GroupVault; wrongDraft.Project.Id = Guid.NewGuid();
            Reject(() => GroupProjectCreation.CopyGroup(directory, "Wrong ID Copy", created.ProjectPath, created.ProjectRevision, created.VaultRevision,
                trust, current.Checkpoint, [alice], now, draft: wrongDraft));
            if (ConcurrentFileWriter.Capture(created.ProjectPath) != created.ProjectRevision || ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(created.ProjectPath)) != created.VaultRevision)
                throw new Exception("Group copy modified source files.");
            Reject(() => GroupProjectCreation.Create(directory, "New Group", source, keys, trust, "lokal", current.Checkpoint, [alice], now));
            Reject(() => GroupProjectCreation.Create(directory, "Denied", source, keys, trust, "lokal", current.Checkpoint, [bob], now));
            if (new ProjectCatalog().Scan(directory).Any(e => e.Name == "Denied")) throw new Exception("Failed project visible in catalog.");
            Reject(() => GroupProjectCreation.Create(directory, "../escape", source, keys, trust, "lokal", current.Checkpoint, [alice], now));
            var legacyPath = Path.Combine(directory, "Original", "original.project.json");
            ProjectJson.Save(legacyPath, ProjectJson.WithoutAllSecrets(source));
            var legacyVault = ProjectKeyVault.GetPath(legacyPath);
            const string phrase = "Synthetic legacy migration password 2026";
            File.WriteAllText(legacyVault, JsonSerializer.Serialize(KeyVaultService.Protect(JsonSerializer.Serialize(keys), phrase), ProjectJson.Options));
            var projectRevision = ConcurrentFileWriter.Capture(legacyPath); var vaultRevision = ConcurrentFileWriter.Capture(legacyVault);
            Reject(() => GroupProjectCreation.MigrateLegacy(directory, "Wrong Password", legacyPath, projectRevision, vaultRevision, trust, "lokal", current.Checkpoint, [alice], now, "wrong"));
            Reject(() => GroupProjectCreation.MigrateLegacy(directory, "Stale", legacyPath, FileRevision.Missing, vaultRevision, trust, "lokal", current.Checkpoint, [alice], now, phrase));
            var migrated = GroupProjectCreation.MigrateLegacy(directory, "Migrated", legacyPath, projectRevision, vaultRevision, trust, "lokal", current.Checkpoint, [alice], now, phrase);
            if (ConcurrentFileWriter.Capture(legacyPath) != projectRevision || ConcurrentFileWriter.Capture(legacyVault) != vaultRevision ||
                ProjectGroupVault.Load(migrated.ProjectPath, ProjectJson.Load(migrated.ProjectPath), trust, [alice]).ProductivePicc.Value != keys.ProductivePicc.Value)
                throw new Exception("Migration changed originals or lost keys.");
            IEnumerable<X509Certificate2> ChangePolicyDuringCreation()
            {
                trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = policy.Revision + 1 }, root));
                yield return alice;
            }
            Reject(() => GroupProjectCreation.Create(directory, "Changed Policy", source, keys, trust, "lokal", current.Checkpoint, ChangePolicyDuringCreation(), now));
            if (new ProjectCatalog().Scan(directory).Any(e => e.Name is "Changed Policy" or "Wrong Password" or "Stale"))
                throw new Exception("Failed creation/migration appeared in catalog.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void VerifyProjectBinding(OfflineGroupPolicy policy, RSA root, X509Certificate2 alice, X509Certificate2 bob,
        X509Certificate2 recovery, DateTimeOffset now)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-project-group-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), policy.LibraryId, new OfflineGroupTrustTests.TestProtection());
            var publicKey = root.ExportSubjectPublicKeyInfo();
            trust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), OfflineGroupPolicyService.Sign(policy, root));
            var legacy = new ProjectDefinition(); legacy.Project.Name = "Synthetic project";
            legacy.Productive.PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('1', 32) };
            var before = JsonSerializer.Serialize(legacy, ProjectJson.Options);
            var project = ProjectGroupVault.Bind(legacy, trust, "lokal");
            if (legacy.Project.Id is not null || legacy.Project.GroupVault is not null || JsonSerializer.Serialize(legacy, ProjectJson.Options) != before ||
                project.Project.Id is null || project.Project.Id == Guid.Empty)
                throw new Exception("Explicit group binding mutated legacy source or omitted identity.");
            var rebound = ProjectGroupVault.Bind(project, trust, "lokal");
            if (rebound.Project.Id != project.Project.Id) throw new Exception("Rebinding same group changed project identity.");
            Reject(() => ProjectGroupVault.Bind(project, trust, "other"));
            var path = Path.Combine(directory, "sample.project.json");
            ProjectJson.Save(path, ProjectJson.WithoutAllSecrets(project));
            var reloaded = ProjectJson.Load(path);
            if (reloaded.Project.Id != project.Project.Id || reloaded.Project.GroupVault != project.Project.GroupVault || reloaded.Productive.PiccKey.Value != "")
                throw new Exception("Key-free project did not retain stable group identity.");
            var keys = ProjectKeyVault.FromProject(project);
            Reject(() => ProjectGroupVault.Create(path, project, keys, trust, [bob], now));
            if (File.Exists(ProjectKeyVault.GetPath(path))) throw new Exception("Inaccessible new vault persisted.");
            var revision = ProjectGroupVault.Create(path, project, keys, trust, [alice], now);
            Reject(() => ProjectGroupVault.Create(path, project, keys, trust, [alice], now));
            var loaded = ProjectGroupVault.Load(path, reloaded, trust, [alice]);
            var recovered = ProjectGroupVault.Load(path, reloaded, trust, [recovery], recovery: true);
            if (loaded.ProductivePicc.Value != keys.ProductivePicc.Value || recovered.ProductivePicc.Value != keys.ProductivePicc.Value ||
                ProjectKeyVault.Apply(reloaded, loaded).Productive.PiccKey.Value != keys.ProductivePicc.Value)
                throw new Exception("Typed project group vault roundtrip failed.");
            reloaded.Project.Id = Guid.NewGuid();
            Reject(() => ProjectGroupVault.Load(path, reloaded, trust, [alice]));
            reloaded.Project.Id = project.Project.Id;
            reloaded.Project.GroupVault = new(policy.LibraryId, "other");
            Reject(() => ProjectGroupVault.Load(path, reloaded, trust, [alice]));
            reloaded.Project.GroupVault = project.Project.GroupVault;
            keys.ProductivePicc.Value = new string('2', 32);
            var updated = ProjectGroupVault.Update(path, reloaded, keys, trust, [alice], revision, now);
            if (ProjectGroupVault.Load(path, reloaded, trust, [alice]).ProductivePicc.Value != keys.ProductivePicc.Value ||
                !File.Exists(updated.EncryptedBackupPath)) throw new Exception("Typed project update or backup failed.");
            Reject(() => ProjectGroupVault.Update(path, reloaded, keys, trust, [alice], revision, now));
            var newer = policy with { Revision = 2, Groups = [new("lokal", "Lokal", ["alice", "bob"]), policy.Groups[1]] };
            trust.AcceptUpdate(OfflineGroupPolicyService.Sign(newer, root));
            Reject(() => ProjectGroupVault.Update(path, reloaded, keys, trust, [alice], updated.Revision, now));
            if (ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(path)) != updated.Revision)
                throw new Exception("Ordinary save silently changed recipients.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void VerifyReconciliation(GroupVaultDocument original, OfflineGroupPolicy oldPolicy, OfflineGroupPolicy targetPolicy,
        RSA root, Guid project, X509Certificate2 alice, X509Certificate2 bob, X509Certificate2 recovery, DateTimeOffset now, string payload)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-group-rotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "project.group-vault.json");
            var source = GroupVaultService.Write(original); File.WriteAllBytes(path, source);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), targetPolicy.LibraryId, new OfflineGroupTrustTests.TestProtection());
            var publicKey = root.ExportSubjectPublicKeyInfo();
            trust.Enroll(publicKey, OfflineGroupTrustStore.AdministratorFingerprint(publicKey), OfflineGroupPolicyService.Sign(targetPolicy, root));
            Reject(() => GroupVaultReconciliation.Preview(path, trust, project, "lokal", [alice], now));
            Reject(() => GroupVaultReconciliation.Preview(path, trust, project, "lokal", [bob], now)); // New member cannot decrypt the old vault.
            var plan = GroupVaultReconciliation.Preview(path, trust, project, "lokal", [recovery], now, recovery: true);
            if (plan.Added.Single() != new GroupVaultAccess("member", CertificateVaultService.Fingerprint(bob)) ||
                plan.Removed.Single() != new GroupVaultAccess("member", CertificateVaultService.Fingerprint(alice)) ||
                !File.ReadAllBytes(path).SequenceEqual(source) || Directory.Exists(Path.Combine(directory, "GroupVaultBackups")))
                throw new Exception("Reconciliation preview changed a file or misreported recipient changes.");
            File.WriteAllBytes(path, source.Concat(new byte[] { 32 }).ToArray());
            Reject(() => GroupVaultReconciliation.Apply(plan, trust, [recovery], now));
            File.WriteAllBytes(path, source);
            trust.AcceptUpdate(OfflineGroupPolicyService.Sign(targetPolicy with { Revision = targetPolicy.Revision + 1 }, root));
            Reject(() => GroupVaultReconciliation.Apply(plan, trust, [recovery], now));
            if (!File.ReadAllBytes(path).SequenceEqual(source)) throw new Exception("Stale preview overwrote source.");
            plan = GroupVaultReconciliation.Preview(path, trust, project, "lokal", [recovery], now, recovery: true);
            var result = GroupVaultReconciliation.Apply(plan, trust, [recovery], now);
            var rotated = GroupVaultService.Read(File.ReadAllBytes(path));
            if (!File.ReadAllBytes(result.EncryptedBackupPath).SequenceEqual(source) || result.KeyGeneration == original.Header.KeyGeneration ||
                result.Revision != ConcurrentFileWriter.Capture(path) || result.AppliedPolicy != trust.LoadOffline().Checkpoint ||
                GroupVaultService.Unprotect(rotated, trust.LoadOffline(), project, "lokal", [bob]) != payload ||
                GroupVaultService.Unprotect(rotated, trust.LoadOffline(), project, "lokal", [recovery], recovery: true) != payload)
                throw new Exception("Atomic recipient reconciliation or encrypted backup failed.");
            Reject(() => GroupVaultReconciliation.Apply(plan, trust, [recovery], now));
            var oldTrusted = OfflineGroupPolicyService.Verify(OfflineGroupPolicyService.Sign(oldPolicy, root), root, oldPolicy.LibraryId);
            if (GroupVaultService.Unprotect(GroupVaultService.Read(File.ReadAllBytes(result.EncryptedBackupPath)), oldTrusted, project, "lokal", [alice]) != payload)
                throw new Exception("Encrypted backup did not retain historical access.");
            var external = source.Concat(new byte[] { 32, 32 }).ToArray();
            Reject(() => ConcurrentFileWriter.Write(path, result.Revision, temporary =>
            {
                File.WriteAllBytes(temporary, source);
                File.WriteAllBytes(path, external); // Simulate a writer not participating in the application lock.
            }));
            if (!File.ReadAllBytes(path).SequenceEqual(external)) throw new Exception("Late external update overwritten.");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or InvalidDataException or InvalidOperationException or JsonException or IOException) { return; }
        throw new Exception("Invalid group vault operation accepted.");
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RFiDGear.Builder.Core;

internal static class GroupPrivateKeyBackupTests
{
    private const string Password = "Synthetic private backup password 2026";
    public static void Verify()
    {
        using var key = RSA.Create(3072);
        var library = Guid.NewGuid();
        var context = new GroupPrivateKeyContext(library, GroupPrivateKeyBackups.Administrator);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(key.ExportSubjectPublicKeyInfo());
        var backup = GroupPrivateKeyBackups.Protect(key, context, Password);
        using (var restored = GroupPrivateKeyBackups.Restore(backup, Password, context, fingerprint))
            Assert(restored.Key.ExportSubjectPublicKeyInfo().SequenceEqual(key.ExportSubjectPublicKeyInfo()), "Private backup changed key.");
        Reject(() => GroupPrivateKeyBackups.Restore(backup, "wrong password", context, fingerprint));
        Reject(() => GroupPrivateKeyBackups.Restore(backup, Password, context with { LibraryId = Guid.NewGuid() }, fingerprint));
        Reject(() => GroupPrivateKeyBackups.Restore(backup, Password, new(library, GroupPrivateKeyBackups.Delegate, "alice"), fingerprint));
        Reject(() => GroupPrivateKeyBackups.Restore(backup, Password, context, new string('0', 64)));
        Reject(() => GroupPrivateKeyBackups.Protect(key, context, "short"));
        Reject(() => GroupPrivateKeyBackups.Restore(new byte[GroupPrivateKeyBackups.MaximumBytes + 1], Password, context, fingerprint));
        var text = Encoding.UTF8.GetString(backup);
        Assert(!text.Contains(Convert.ToBase64String(key.ExportPkcs8PrivateKey())), "Plain private key in backup.");
        Reject(() => GroupPrivateKeyBackups.Restore(Encoding.UTF8.GetBytes(text.Insert(1, "\"Version\":1,")), Password, context, fingerprint));
        Reject(() => GroupPrivateKeyBackups.Restore(Encoding.UTF8.GetBytes(text.Insert(1, "\"Unknown\":1,")), Password, context, fingerprint));
        var node = JsonNode.Parse(backup)!;
        var cipher = Convert.FromBase64String(node["Encrypted"]!["ciphertext"]!.GetValue<string>());
        cipher[0] ^= 1; node["Encrypted"]!["ciphertext"] = Convert.ToBase64String(cipher);
        Reject(() => GroupPrivateKeyBackups.Restore(Encoding.UTF8.GetBytes(node.ToJsonString()), Password, context, fingerprint));

        using var certificate = new CertificateRequest("CN=Synthetic Recovery", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var recoveryContext = new GroupPrivateKeyContext(library, GroupPrivateKeyBackups.Recovery);
        var recovery = GroupPrivateKeyBackups.Protect(key, recoveryContext, Password, certificate);
        using (var restored = GroupPrivateKeyBackups.Restore(recovery, Password, recoveryContext, CertificateVaultService.Fingerprint(certificate)))
        {
            using var identity = restored.Certificate!.CopyWithPrivateKey(restored.Key);
            using var privateKey = identity.GetRSAPrivateKey();
            var challenge = RandomNumberGenerator.GetBytes(32);
            Assert(privateKey!.Decrypt(key.Encrypt(challenge, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256).SequenceEqual(challenge), "Recovery key cannot decrypt.");
        }
        using (var other = RSA.Create(3072)) Reject(() => GroupPrivateKeyBackups.Protect(other, recoveryContext, Password, certificate));
        Reject(() => GroupPrivateKeyBackups.Protect(key, context, Password, certificate));
        Reject(() => GroupPrivateKeyBackups.Protect(key, recoveryContext, Password));
        VerifyStores();
    }

    private static void VerifyStores()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-private-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var library = Guid.NewGuid();
            var admin = new OfflineGroupAdministrationStore(Path.Combine(directory, "admin.bin"), library, new OfflineGroupTrustTests.TestProtection());
            var initial = admin.Create();
            var backup = admin.ExportPrivateBackup(Password);
            var signer = new OfflineGroupDelegateStore(Path.Combine(directory, "delegate.bin"), library, "alice", new OfflineGroupTrustTests.TestProtection());
            var invitation = signer.Create();
            var delegateBackup = signer.ExportPrivateBackup(Password);
            var people = admin.UpdateDirectory([new("alice", "Alice", true, [])], [new("lokal", "Lokal", ["alice"])], GroupEnrollmentPackages.Validate(initial).Checkpoint);
            var granted = admin.GrantDelegation(invitation, GroupDelegationTransport.InvitationFingerprint(invitation), "grant", ["lokal"], GroupEnrollmentPackages.Validate(people).Checkpoint);
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), library, new OfflineGroupTrustTests.TestProtection());
            var current = trust.Enroll(granted.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(granted.AdministratorPublicKey), granted.Policy);
            var restoredAdmin = new OfflineGroupAdministrationStore(Path.Combine(directory, "restored-admin.bin"), library, new OfflineGroupTrustTests.TestProtection());
            var restored = restoredAdmin.RestorePrivateBackup(backup, Password, trust);
            Assert(GroupEnrollmentPackages.Validate(restored).Checkpoint == current.Checkpoint, "Old backup rolled back policy.");
            Reject(() => restoredAdmin.RestorePrivateBackup(backup, Password, trust));
            var restoredSigner = new OfflineGroupDelegateStore(Path.Combine(directory, "restored-delegate.bin"), library, "alice", new OfflineGroupTrustTests.TestProtection());
            restoredSigner.RestorePrivateBackup(delegateBackup, Password, trust, "grant");
            var signed = restoredSigner.SignChange(trust, "grant", current.Checkpoint, [new("lokal", "Restored signer", ["alice"])]);
            var updated = restoredAdmin.ApplyDelegatedChange(signed, current.Checkpoint);
            Assert(GroupEnrollmentPackages.Validate(updated).Policy.Groups[0].Name == "Restored signer", "Restored private keys cannot administer.");
            Reject(() => restoredSigner.RestorePrivateBackup(delegateBackup, Password, trust, "grant"));
            var wrongPerson = new OfflineGroupDelegateStore(Path.Combine(directory, "wrong-person.bin"), library, "bob", new OfflineGroupTrustTests.TestProtection());
            Reject(() => wrongPerson.RestorePrivateBackup(delegateBackup, Password, trust, "grant"));
            var revoked = restoredAdmin.UpdateDelegations([], GroupEnrollmentPackages.Validate(updated).Checkpoint);
            trust.AcceptUpdate(revoked.Policy);
            var revokedSigner = new OfflineGroupDelegateStore(Path.Combine(directory, "revoked.bin"), library, "alice", new OfflineGroupTrustTests.TestProtection());
            Reject(() => revokedSigner.RestorePrivateBackup(delegateBackup, Password, trust, "grant"));
            Assert(!File.Exists(Path.Combine(directory, "revoked.bin")), "Revoked key persisted.");
            var concurrentProtection = new OfflineGroupTrustTests.TestProtection();
            concurrentProtection.BeforeProtect = () =>
            {
                concurrentProtection.BeforeProtect = null;
                var next = restoredAdmin.UpdateGroups([new("lokal", "Newer", [])], GroupEnrollmentPackages.Validate(revoked).Checkpoint);
                trust.AcceptUpdate(next.Policy);
            };
            var concurrentPath = Path.Combine(directory, "concurrent.bin");
            Reject(() => new OfflineGroupAdministrationStore(concurrentPath, library, concurrentProtection).RestorePrivateBackup(backup, Password, trust));
            Assert(!File.Exists(concurrentPath), "Concurrent trust update ignored during restoration.");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or JsonException or ArgumentException) { return; }
        throw new Exception("Unsafe private-key backup operation succeeded.");
    }
}

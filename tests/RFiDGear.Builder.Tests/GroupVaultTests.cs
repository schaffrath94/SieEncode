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
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or InvalidDataException or InvalidOperationException or JsonException) { return; }
        throw new Exception("Invalid group vault operation accepted.");
    }
}

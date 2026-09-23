using System.Security.Cryptography;
using RFiDGear.Builder.Core;

internal static class OfflineGroupDelegateStoreTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-delegate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var library = Guid.NewGuid();
            var path = Path.Combine(directory, "delegate.bin");
            var protection = new OfflineGroupTrustTests.TestProtection();
            var signer = new OfflineGroupDelegateStore(path, library, "alice", protection);
            var invitation = signer.Create();
            var invitationBytes = GroupDelegationTransport.WriteInvitation(invitation);
            invitation = GroupDelegationTransport.ReadInvitation(invitationBytes);
            var json = System.Text.Encoding.UTF8.GetString(invitationBytes);
            Reject(() => GroupDelegationTransport.ReadInvitation(System.Text.Encoding.UTF8.GetBytes(json.Insert(1, "\"Version\":1,"))));
            Reject(() => GroupDelegationTransport.ReadInvitation(System.Text.Encoding.UTF8.GetBytes(json.Insert(1, "\"PrivateKey\":\"bad\","))));
            Reject(() => GroupDelegationTransport.ReadInvitation(new byte[GroupDelegationTransport.MaximumBytes + 1]));
            using (var privateCandidate = RSA.Create(3072))
                Reject(() => GroupDelegationTransport.WriteInvitation(invitation with { SigningPublicKey = privateCandidate.ExportPkcs8PrivateKey() }));
            var before = File.ReadAllBytes(path);
            Reject(() => signer.Create());
            if (!before.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Existing signing identity overwritten.");
            signer = new OfflineGroupDelegateStore(path, library, "alice", protection);
            if (!signer.ExportPublic().SigningPublicKey.SequenceEqual(invitation.SigningPublicKey)) throw new Exception("Restart lost delegate key.");
            Reject(() => new OfflineGroupDelegateStore(path, library, "bob", protection).ExportPublic());
            Reject(() => new OfflineGroupDelegateStore(path, Guid.NewGuid(), "alice", protection).ExportPublic());
            Reject(() => new OfflineGroupDelegateStore(path, library, "alice", new OfflineGroupTrustTests.TestProtection()).ExportPublic());
            using var root = RSA.Create(3072);
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, library, 1,
                [new("lokal", "Lokal", ["alice"]), new("other", "Other", [])])
            {
                People = [new("alice", "Alice", true, [])],
                Delegations = [new("grant", "alice", invitation.SigningPublicKey, ["lokal"], true)]
            };
            var trust = new OfflineGroupTrustStore(Path.Combine(directory, "trust.bin"), library, protection);
            var key = root.ExportSubjectPublicKeyInfo();
            var current = trust.Enroll(key, OfflineGroupTrustStore.AdministratorFingerprint(key), OfflineGroupPolicyService.Sign(policy, root));
            var signed = signer.SignChange(trust, "grant", current.Checkpoint, [new("lokal", "Updated", [])]);
            signed = GroupDelegationTransport.ReadChange(GroupDelegationTransport.WriteChange(signed));
            Reject(() => GroupDelegationTransport.ReadChange(invitationBytes));
            if (OfflineGroupDelegationService.Apply(signed, current).Groups[0].Name != "Updated") throw new Exception("Delegate signature unusable.");
            Reject(() => signer.SignChange(trust, "grant", current.Checkpoint, [new("other", "Escalation", [])]));
            Reject(() => signer.SignChange(trust, "unknown", current.Checkpoint, [new("lokal", "Updated", [])]));
            var other = new OfflineGroupDelegateStore(Path.Combine(directory, "other.bin"), library, "alice", protection);
            other.Create();
            Reject(() => other.SignChange(trust, "grant", current.Checkpoint, [new("lokal", "Updated", [])]));
            var revoked = trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 2, Delegations = [] }, root));
            Reject(() => signer.SignChange(trust, "grant", current.Checkpoint, [new("lokal", "Updated", [])]));
            Reject(() => signer.SignChange(trust, "grant", revoked.Checkpoint, [new("lokal", "Updated", [])]));
            if (!before.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Signing changed protected identity.");
            var corrupt = before.ToArray(); corrupt[^1] ^= 1; File.WriteAllBytes(path, corrupt);
            Reject(() => signer.ExportPublic());
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or System.Text.Json.JsonException) { return; }
        throw new Exception("Invalid delegate key operation succeeded.");
    }
}

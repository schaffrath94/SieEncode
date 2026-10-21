using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RFiDGear.Builder.Core;

internal static class GroupAdministratorTransitionTests
{
    public static void Verify()
    {
        using var oldKey = RSA.Create(3072);
        using var newKey = RSA.Create(3072);
        using var stranger = RSA.Create(3072);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, Guid.NewGuid(), 4,
            [new("lokal", "Lokal", [new string('A', 64)]), new("lokal-2", "Lokal 2", [new string('A', 64)])]);
        GroupEnrollmentPackage Package(OfflineGroupPolicy p, RSA key) => new(GroupEnrollmentPackages.Format, 1,
            p.LibraryId, key.ExportSubjectPublicKeyInfo(), OfflineGroupPolicyService.Sign(p, key));
        var current = Package(policy, oldKey);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(newKey.ExportSubjectPublicKeyInfo());
        var request = GroupAdministratorTransition.Prepare(current, newKey);
        var publicRequest = GroupAdministratorTransition.WriteRequest(request);
        var received = GroupAdministratorTransition.ReadRequest(publicRequest);
        var authorized = GroupAdministratorTransition.Authorize(received, current, oldKey, fingerprint, true);
        var authorizedNext = GroupAdministratorTransition.Verify(authorized, current, fingerprint);
        if (!authorizedNext.AdministratorPublicKey.SequenceEqual(newKey.ExportSubjectPublicKeyInfo()))
            throw new Exception("Separated successor request authorized the wrong key.");
        Reject(() => GroupAdministratorTransition.Authorize(received, current, oldKey, fingerprint, false));
        Reject(() => GroupAdministratorTransition.Authorize(received, current, oldKey, "", true));
        Reject(() => GroupAdministratorTransition.Authorize(received, current, stranger, fingerprint, true));
        Reject(() => GroupAdministratorTransition.Authorize(received, Package(policy with { Revision = 5 }, oldKey), oldKey, fingerprint, true));
        Reject(() => GroupAdministratorTransition.Authorize(received, Package(policy with { Groups = [] }, oldKey), oldKey, fingerprint, true));
        var invalidRequestSignature = received.NextSignature.ToArray(); invalidRequestSignature[0] ^= 1;
        Reject(() => GroupAdministratorTransition.Authorize(received with { NextSignature = invalidRequestSignature }, current, oldKey, fingerprint, true));
        var invalidRequestPayload = received.Payload.ToArray(); invalidRequestPayload[0] ^= 1;
        Reject(() => GroupAdministratorTransition.Authorize(received with { Payload = invalidRequestPayload }, current, oldKey, fingerprint, true));
        Reject(() => GroupAdministratorTransition.ReadRequest(GroupAdministratorTransition.Write(authorized)));
        Reject(() => GroupAdministratorTransition.Read(publicRequest));
        Reject(() => GroupAdministratorTransition.ReadRequest(new byte[GroupAdministratorTransition.MaximumBytes + 1]));
        Reject(() => GroupAdministratorTransition.ReadRequest(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(publicRequest).Insert(1, "\"Version\":1,"))));
        Reject(() => GroupAdministratorTransition.WriteRequest(received with { Version = 2 }));
        Reject(() => GroupAdministratorTransition.WriteRequest(received with { Format = "unrelated" }));
        var requestJson = JsonNode.Parse(publicRequest)!; requestJson["PrivateKey"] = "not-allowed";
        Reject(() => GroupAdministratorTransition.ReadRequest(Encoding.UTF8.GetBytes(requestJson.ToJsonString())));
        // Caller mutation after authorization must not alter the produced public transition.
        received.Payload[0] ^= 1; received.NextSignature[0] ^= 1;
        _ = GroupAdministratorTransition.Verify(authorized, current, fingerprint);
        var transition = GroupAdministratorTransition.Read(GroupAdministratorTransition.Write(
            GroupAdministratorTransition.Create(current, oldKey, newKey)));
        var next = GroupAdministratorTransition.Verify(transition, current, fingerprint);
        var verified = GroupEnrollmentPackages.Validate(next);
        if (verified.Policy.Revision != 5 || verified.Policy.LibraryId != policy.LibraryId ||
            verified.Policy.Groups.Length != 2 || verified.Policy.Groups[1].Members.Single() != new string('A', 64))
            throw new Exception("Transition lost policy contents or monotonic revision.");
        Reject(() => GroupAdministratorTransition.Create(current, stranger, newKey));
        Reject(() => GroupAdministratorTransition.Create(current, oldKey, oldKey));
        Reject(() => GroupAdministratorTransition.Verify(transition, current, ""));
        Reject(() => GroupAdministratorTransition.Verify(transition, current, new string('0', 64)));
        Reject(() => GroupAdministratorTransition.Verify(transition, Package(policy, stranger), fingerprint));
        Reject(() => GroupAdministratorTransition.Verify(transition, Package(policy with { LibraryId = Guid.NewGuid() }, oldKey), fingerprint));
        Reject(() => GroupAdministratorTransition.Verify(transition, Package(policy with { Revision = 5 }, oldKey), fingerprint));
        Reject(() => GroupAdministratorTransition.Verify(transition, Package(policy with { Groups = [] }, oldKey), fingerprint));
        Reject(() => GroupAdministratorTransition.Verify(transition, next, fingerprint));
        var changed = transition.Payload.ToArray(); changed[^2] ^= 1;
        Reject(() => GroupAdministratorTransition.Verify(transition with { Payload = changed }, current, fingerprint));
        changed = transition.PreviousSignature.ToArray(); changed[0] ^= 1;
        Reject(() => GroupAdministratorTransition.Verify(transition with { PreviousSignature = changed }, current, fingerprint));
        changed = transition.NextSignature.ToArray(); changed[0] ^= 1;
        Reject(() => GroupAdministratorTransition.Verify(transition with { NextSignature = changed }, current, fingerprint));
        Reject(() => GroupAdministratorTransition.Verify(transition with { PreviousSignature = transition.NextSignature, NextSignature = transition.PreviousSignature }, current, fingerprint));

        // Even correctly signed malicious transitions cannot alter rights or skip revisions.
        SignedAdministratorTransition Resign(byte[] payload) => new(payload,
            oldKey.SignData(Encoding.UTF8.GetBytes("SieEncode-Administrator-Transition/v1/previous\0").Concat(payload).ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            newKey.SignData(Encoding.UTF8.GetBytes("SieEncode-Administrator-Transition/v1/next\0").Concat(payload).ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        foreach (var invalid in new[] { policy with { Revision = 5, Groups = [] }, policy with { Revision = 4 }, policy with { Revision = 6 } })
        {
            var payload = JsonNode.Parse(transition.Payload)!;
            payload["Next"] = JsonSerializer.SerializeToNode(Package(invalid, newKey));
            Reject(() => GroupAdministratorTransition.Verify(Resign(Encoding.UTF8.GetBytes(payload.ToJsonString())), current, fingerprint));
        }
        var duplicatePayload = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(transition.Payload).Insert(1, "\"Version\":1,"));
        Reject(() => GroupAdministratorTransition.Verify(Resign(duplicatePayload), current, fingerprint));
        var unknown = JsonNode.Parse(transition.Payload)!; unknown["Unexpected"] = true;
        Reject(() => GroupAdministratorTransition.Verify(Resign(Encoding.UTF8.GetBytes(unknown.ToJsonString())), current, fingerprint));
        var envelope = GroupAdministratorTransition.Write(transition);
        Reject(() => GroupAdministratorTransition.Read(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(envelope).Insert(1, "\"Payload\":\"AA==\","))));
        Reject(() => GroupAdministratorTransition.Read(new byte[GroupAdministratorTransition.MaximumBytes + 1]));
        Reject(() => GroupAdministratorTransition.Verify(transition with { NextSignature = [] }, current, fingerprint));

        // A version-2 directory and an existing vault retain normal and separate recovery access.
        using var recoveryKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        using var member = new CertificateRequest("CN=Synthetic transition member", stranger, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var recovery = new CertificateRequest("CN=Synthetic transition recovery", recoveryKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        OfflineVaultDevice Device(X509Certificate2 cert) => new(cert.Subject, CertificateVaultService.Fingerprint(cert),
            Convert.ToBase64String(cert.RawData), true);
        var directoryPolicy = policy with
        {
            Version = 2, Groups = [new("lokal", "Lokal", ["person"])],
            People = [new("person", "Synthetic member", true, [Device(member)])], RecoveryDevices = [Device(recovery)]
        };
        var directoryPackage = Package(directoryPolicy, oldKey);
        var project = Guid.NewGuid();
        var vault = GroupVaultService.Protect("{\"synthetic\":true}", GroupEnrollmentPackages.Validate(directoryPackage), project, "lokal", now);
        var directoryNext = GroupAdministratorTransition.Verify(
            GroupAdministratorTransition.Create(directoryPackage, oldKey, newKey), directoryPackage, fingerprint);
        var directoryVerified = GroupEnrollmentPackages.Validate(directoryNext);
        if (GroupVaultService.Unprotect(vault, directoryVerified, project, "lokal", [member]) != "{\"synthetic\":true}" ||
            GroupVaultService.Unprotect(vault, directoryVerified, project, "lokal", [recovery], true) != "{\"synthetic\":true}")
            throw new Exception("Administrator transition broke existing normal or recovery access.");
        Reject(() => GroupAdministratorTransition.Create(directoryPackage, oldKey, stranger));
        Reject(() => GroupAdministratorTransition.Create(directoryPackage, oldKey, recoveryKey));

        // An ordinary policy update must never acquire the transition capability implicitly.
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-transition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "trust.bin");
            var protector = new OfflineGroupTrustTests.TestProtection();
            var trust = new OfflineGroupTrustStore(path, policy.LibraryId, protector);
            var enrolled = trust.Enroll(current.AdministratorPublicKey,
                OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey), current.Policy);
            var before = File.ReadAllBytes(path);
            VerifySuccessorPreparation(directory, current, oldKey);
            VerifyStoredAuthorization(directory);
            Reject(() => trust.AcceptPackageUpdate(next, enrolled.Checkpoint));
            if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Ordinary update replaced trust.");
            var preview = trust.PreviewAdministratorTransition(transition, enrolled.Checkpoint, fingerprint);
            if (!preview.AdministratorPublicKey.SequenceEqual(newKey.ExportSubjectPublicKeyInfo()) ||
                !File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Transition preview changed trust.");
            Reject(() => trust.AcceptAdministratorTransition(transition, enrolled.Checkpoint, fingerprint, false));
            Reject(() => trust.AcceptAdministratorTransition(transition, enrolled.Checkpoint, "", true));
            Reject(() => trust.AcceptAdministratorTransition(transition, new(99, enrolled.Checkpoint.PayloadSha256), fingerprint, true));
            if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Rejected transition changed trust.");

            protector.BeforeProtect = () => throw new IOException("Synthetic protection failure");
            try { Reject(() => trust.AcceptAdministratorTransition(transition, enrolled.Checkpoint, fingerprint, true)); }
            finally { protector.BeforeProtect = null; }
            if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Failed protection lost old trust.");

            // A concurrent ordinary update must not be overwritten by the transition.
            protector.BeforeProtect = () =>
            {
                protector.BeforeProtect = null;
                trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 5 }, oldKey));
            };
            Reject(() => trust.AcceptAdministratorTransition(transition, enrolled.Checkpoint, fingerprint, true));
            if (trust.LoadOffline().Policy.Revision != 5 || !trust.ExportPublic().AdministratorPublicKey.SequenceEqual(current.AdministratorPublicKey))
                throw new Exception("Concurrent ordinary update overwritten.");
            var latest = trust.ExportPublic();
            var checkpoint = trust.LoadOffline().Checkpoint;
            var refreshedTransition = GroupAdministratorTransition.Create(latest, oldKey, newKey);
            var accepted = trust.AcceptAdministratorTransition(refreshedTransition, checkpoint, fingerprint, true);
            var restarted = new OfflineGroupTrustStore(path, policy.LibraryId, protector);
            if (restarted.LoadOffline().Checkpoint != accepted.Checkpoint ||
                !restarted.ExportPublic().AdministratorPublicKey.SequenceEqual(newKey.ExportSubjectPublicKeyInfo()))
                throw new Exception("Transition not durable across store restart.");
            var acceptedBytes = File.ReadAllBytes(path);
            Reject(() => restarted.AcceptAdministratorTransition(refreshedTransition, checkpoint, fingerprint, true));
            Reject(() => restarted.AcceptAdministratorTransition(refreshedTransition, accepted.Checkpoint, fingerprint, true));
            Reject(() => restarted.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 100 }, oldKey)));
            Reject(() => restarted.AcceptPackageUpdate(Package(policy with { Revision = 100 }, oldKey), accepted.Checkpoint));
            if (!File.ReadAllBytes(path).SequenceEqual(acceptedBytes)) throw new Exception("Old authority or replay changed transitioned trust.");
            restarted.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 7 }, newKey));
            if (restarted.LoadOffline().Policy.Revision != 7) throw new Exception("New authority cannot update after restart.");

            // A different explicit transition wins over an in-flight competing transition.
            latest = restarted.ExportPublic(); checkpoint = restarted.LoadOffline().Checkpoint;
            var toOld = GroupAdministratorTransition.Create(latest, newKey, oldKey);
            var toOther = GroupAdministratorTransition.Create(latest, newKey, stranger);
            var otherFingerprint = OfflineGroupTrustStore.AdministratorFingerprint(stranger.ExportSubjectPublicKeyInfo());
            protector.BeforeProtect = () =>
            {
                protector.BeforeProtect = null;
                restarted.AcceptAdministratorTransition(toOther, checkpoint, otherFingerprint, true);
            };
            Reject(() => restarted.AcceptAdministratorTransition(toOld, checkpoint,
                OfflineGroupTrustStore.AdministratorFingerprint(oldKey.ExportSubjectPublicKeyInfo()), true));
            if (!restarted.ExportPublic().AdministratorPublicKey.SequenceEqual(stranger.ExportSubjectPublicKeyInfo()))
                throw new Exception("Competing transition overwritten.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void VerifySuccessorPreparation(string directory, GroupEnrollmentPackage current, RSA oldKey)
    {
        var id = current.LibraryId;
        var trust = new OfflineGroupTrustStore(Path.Combine(directory, "successor-trust.bin"), id, new OfflineGroupTrustTests.TestProtection());
        var checkpoint = trust.Enroll(current.AdministratorPublicKey,
            OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey), current.Policy).Checkpoint;
        var path = Path.Combine(directory, "pending.bin");
        var protection = new OfflineGroupTrustTests.TestProtection();
        var pending = new OfflineGroupSuccessorStore(path, id, protection);
        Reject(() => pending.Create(trust, new(999, checkpoint.PayloadSha256)));
        if (File.Exists(path)) throw new Exception("Stale preparation created a key file.");
        var request = pending.Create(trust, checkpoint);
        var before = File.ReadAllBytes(path);
        var restarted = new OfflineGroupSuccessorStore(path, id, protection);
        if (!GroupAdministratorTransition.WriteRequest(request).SequenceEqual(
            GroupAdministratorTransition.WriteRequest(restarted.ExportRequest(trust))))
            throw new Exception("Pending successor did not survive restart.");
        Reject(() => restarted.Create(trust, checkpoint));
        Reject(() => new OfflineGroupSuccessorStore(path, Guid.NewGuid(), protection).ExportRequest(trust));
        Reject(() => new OfflineGroupSuccessorStore(path, id, new OfflineGroupTrustTests.TestProtection()).ExportRequest(trust));
        const string phrase = "Synthetic successor backup phrase 2026!";
        var backup = restarted.ExportPrivateBackup(phrase);
        var activePath = Path.Combine(directory, "successor-active.bin");
        var active = new OfflineGroupAdministrationStore(activePath, id, new OfflineGroupTrustTests.TestProtection());
        Reject(() => active.RestorePrivateBackup(backup, phrase, trust));
        if (File.Exists(activePath) || !trust.ExportPublic().AdministratorPublicKey.SequenceEqual(current.AdministratorPublicKey))
            throw new Exception("Preparation activated an unauthorized authority.");
        var next = GroupAdministratorTransition.InspectRequest(request, current);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey);
        var transition = GroupAdministratorTransition.Authorize(request, current, oldKey, fingerprint, true);
        trust.AcceptAdministratorTransition(transition, checkpoint, fingerprint, true);
        Reject(() => restarted.ExportRequest(trust));
        backup = restarted.ExportPrivateBackup(phrase);
        active.RestorePrivateBackup(backup, phrase, trust);
        Reject(() => active.RestorePrivateBackup(backup, phrase, trust));
        var updated = active.UpdateGroups(GroupEnrollmentPackages.Validate(next).Policy.Groups, trust.LoadOffline().Checkpoint);
        trust.AcceptPackageUpdate(updated, trust.LoadOffline().Checkpoint);
        if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Preparation was overwritten during handover.");

        var conflictPath = Path.Combine(directory, "pending-conflict.bin");
        protection.BeforeProtect = () =>
        {
            protection.BeforeProtect = null;
            var later = active.UpdateGroups(GroupEnrollmentPackages.Validate(updated).Policy.Groups, trust.LoadOffline().Checkpoint);
            trust.AcceptPackageUpdate(later, trust.LoadOffline().Checkpoint);
        };
        Reject(() => new OfflineGroupSuccessorStore(conflictPath, id, protection).Create(trust, trust.LoadOffline().Checkpoint));
        if (File.Exists(conflictPath)) throw new Exception("Concurrent policy change published stale preparation.");
        var corrupt = before.ToArray(); corrupt[^1] ^= 1; File.WriteAllBytes(path, corrupt);
        Reject(() => restarted.ExportPrivateBackup(phrase));
    }

    private static void VerifyStoredAuthorization(string directory)
    {
        var id = Guid.NewGuid();
        var adminPath = Path.Combine(directory, "authorizing-admin.bin");
        var trustPath = Path.Combine(directory, "authorizing-trust.bin");
        var admin = new OfflineGroupAdministrationStore(adminPath, id, new OfflineGroupTrustTests.TestProtection());
        var original = admin.Create();
        var trust = new OfflineGroupTrustStore(trustPath, id, new OfflineGroupTrustTests.TestProtection());
        var checkpoint = trust.Enroll(original.AdministratorPublicKey,
            OfflineGroupTrustStore.AdministratorFingerprint(original.AdministratorPublicKey), original.Policy).Checkpoint;
        using var successor = RSA.Create(3072);
        var request = GroupAdministratorTransition.Prepare(original, successor);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(successor.ExportSubjectPublicKeyInfo());
        var adminBefore = File.ReadAllBytes(adminPath);
        var trustBefore = File.ReadAllBytes(trustPath);
        Reject(() => admin.AuthorizeSuccessor(request, trust, checkpoint, fingerprint, false));
        Reject(() => admin.AuthorizeSuccessor(request, trust, checkpoint, "", true));
        Reject(() => admin.AuthorizeSuccessor(request, trust, new(999, checkpoint.PayloadSha256), fingerprint, true));
        var bad = request with { NextSignature = new byte[384] };
        Reject(() => admin.AuthorizeSuccessor(bad, trust, checkpoint, fingerprint, true));
        var approved = admin.AuthorizeSuccessor(request, trust, checkpoint, fingerprint, true);
        _ = GroupAdministratorTransition.Verify(approved, original, fingerprint);
        if (!File.ReadAllBytes(adminPath).SequenceEqual(adminBefore) || !File.ReadAllBytes(trustPath).SequenceEqual(trustBefore))
            throw new Exception("Public authorization changed local authority or trust.");
        var differentAdmin = new OfflineGroupAdministrationStore(Path.Combine(directory, "other-authority.bin"), id, new OfflineGroupTrustTests.TestProtection());
        differentAdmin.Create();
        Reject(() => differentAdmin.AuthorizeSuccessor(request, trust, checkpoint, fingerprint, true));
        var newer = admin.UpdateGroups([], checkpoint);
        Reject(() => admin.AuthorizeSuccessor(request, trust, checkpoint, fingerprint, true));
        trust.AcceptPackageUpdate(newer, checkpoint);
        Reject(() => admin.AuthorizeSuccessor(request, trust, trust.LoadOffline().Checkpoint, fingerprint, true));
        var fresh = GroupAdministratorTransition.Prepare(newer, successor);
        var final = admin.AuthorizeSuccessor(fresh, trust, trust.LoadOffline().Checkpoint, fingerprint, true);
        trust.AcceptAdministratorTransition(final, trust.LoadOffline().Checkpoint, fingerprint, true);
        Reject(() => admin.AuthorizeSuccessor(fresh, trust, trust.LoadOffline().Checkpoint, fingerprint, true));
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or JsonException) { return; }
        throw new Exception("Invalid administrator transition accepted.");
    }
}

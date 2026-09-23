using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class OfflineGroupDelegationTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-delegation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var library = Guid.NewGuid();
            var path = Path.Combine(directory, "authority.bin");
            var protection = new OfflineGroupTrustTests.TestProtection();
            var store = new OfflineGroupAdministrationStore(path, library, protection);
            var package = store.Create();
            using var root = RSA.Create();
            root.ImportSubjectPublicKeyInfo(package.AdministratorPublicKey, out _);
            using var manager = RSA.Create(3072);
            using var stranger = RSA.Create(3072);
            VerifiedGroupPolicy Current() => OfflineGroupPolicyService.Verify(store.ExportPublic().Policy, root, library);
            var people = new[] { new OfflineVaultPerson("alice", "Alice", true, []), new OfflineVaultPerson("bob", "Bob", true, []) };
            store.UpdateDirectory(people, [new("lokal", "Lokal", ["alice"]), new("lokal-2", "Lokal 2", ["bob"])], Current().Checkpoint);
            var grant = new OfflineGroupDelegation("manager-one", "alice", manager.ExportSubjectPublicKeyInfo(), ["lokal"], true);
            store.UpdateDelegations([grant], Current().Checkpoint);
            var current = Current();
            var change = new OfflineGroupChange(OfflineGroupDelegationService.Format, 1, library, grant.Id, current.Checkpoint,
                [new("lokal", "Lokal neu", ["alice", "bob"])]);
            var signed = OfflineGroupDelegationService.Sign(change, manager);
            var before = File.ReadAllBytes(path);
            void Denied(SignedGroupChange candidate)
            {
                Reject(() => store.ApplyDelegatedChange(candidate, current.Checkpoint));
                if (!before.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Rejected delegation changed authority file.");
            }
            Denied(OfflineGroupDelegationService.Sign(change, stranger));
            Denied(OfflineGroupDelegationService.Sign(change with { LibraryId = Guid.NewGuid() }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { DelegationId = "unknown" }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { BasePolicy = current.Checkpoint with { PayloadSha256 = new string('0', 64) } }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { Groups = [new("lokal-2", "Escalation", ["alice"])] }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { Groups = [new("new-group", "Escalation", ["alice"])] }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { Groups = [change.Groups[0], change.Groups[0]] }, manager));
            Denied(OfflineGroupDelegationService.Sign(change with { Groups = [new("lokal", "Invalid", ["missing-person"])] }, manager));
            var tampered = signed.Payload.ToArray(); tampered[^2] ^= 1;
            Denied(signed with { Payload = tampered });
            SignedGroupChange Raw(string json)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                return new(bytes, manager.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
            }
            var text = Encoding.UTF8.GetString(signed.Payload);
            Denied(Raw(text.Insert(1, "\"People\":[],")));
            Denied(Raw(text.Insert(1, "\"Version\":1,")));
            Denied(Raw(text.Insert(1, "\"Delegations\":[],")));
            Reject(() => store.UpdateDelegations([grant with { GroupIds = ["missing"] }], current.Checkpoint));
            Reject(() => store.UpdateDelegations([grant, grant with { Id = "second" }], current.Checkpoint));
            Reject(() => store.UpdateDelegations([grant with { SigningPublicKey = manager.ExportPkcs8PrivateKey() }], current.Checkpoint));
            using (var certificate = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic forbidden key reuse", manager,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)))
            {
                var reused = people[0] with { Devices = [new("Reused key", CertificateVaultService.Fingerprint(certificate),
                    Convert.ToBase64String(certificate.RawData), true)] };
                Reject(() => store.UpdateDirectory([reused, people[1]], current.Policy.Groups, current.Checkpoint));
            }
            if (!before.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Invalid grant persisted.");

            store.ApplyDelegatedChange(signed, current.Checkpoint);
            var updated = Current();
            if (updated.Policy.Revision != current.Policy.Revision + 1 || updated.Policy.Groups[0].Members.Length != 2 ||
                updated.Policy.Groups[1].Members.Single() != "bob" ||
                JsonSerializer.Serialize(updated.Policy.People) != JsonSerializer.Serialize(current.Policy.People) ||
                JsonSerializer.Serialize(updated.Policy.Delegations) != JsonSerializer.Serialize(current.Policy.Delegations))
                throw new Exception("Delegation changed data outside the assigned group.");
            Reject(() => store.ApplyDelegatedChange(signed, updated.Checkpoint)); // Replay even with a fresh UI checkpoint.
            store.UpdateDelegations([grant with { Enabled = false }], updated.Checkpoint);
            var revoked = Current();
            Reject(() => store.ApplyDelegatedChange(OfflineGroupDelegationService.Sign(change with { BasePolicy = revoked.Checkpoint }, manager), revoked.Checkpoint));
            store.UpdateDelegations([grant], revoked.Checkpoint);
            store.UpdateDirectory([people[0] with { Enabled = false }, people[1]], Current().Policy.Groups, Current().Checkpoint);
            var disabled = Current();
            Reject(() => store.ApplyDelegatedChange(OfflineGroupDelegationService.Sign(change with { BasePolicy = disabled.Checkpoint }, manager), disabled.Checkpoint));
            store.UpdateDirectory(people, disabled.Policy.Groups, disabled.Checkpoint);
            current = Current();
            var pending = OfflineGroupDelegationService.Sign(change with { BasePolicy = current.Checkpoint }, manager);
            protection.BeforeProtect = () =>
            {
                protection.BeforeProtect = null;
                store.UpdateDelegations([], current.Checkpoint);
            };
            Reject(() => store.ApplyDelegatedChange(pending, current.Checkpoint));
            if (Current().Policy.Delegations.Length != 0) throw new Exception("Concurrent revocation overwritten.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or JsonException or FormatException) { return; }
        throw new Exception("Unauthorized delegated operation accepted.");
    }
}

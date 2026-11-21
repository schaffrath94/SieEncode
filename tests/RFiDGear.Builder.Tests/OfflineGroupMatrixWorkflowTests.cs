using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class OfflineGroupMatrixWorkflowTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-matrix-" + Guid.NewGuid().ToString("N"));
        var privateBackupRoot = directory + "-private-history";
        Directory.CreateDirectory(directory);
        using var administrator = RSA.Create(3072);
        var keys = new List<RSA>();
        var certificates = new List<X509Certificate2>();
        try
        {
            var now = DateTimeOffset.UtcNow;
            // Eight independent normal keys plus a separate recovery key. No certificate-store writes.
            for (var i = 0; i < 9; i++)
            {
                var key = RSA.Create(3072); keys.Add(key);
                certificates.Add(new CertificateRequest("CN=Synthetic matrix " + i, key, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1)));
            }
            OfflineVaultDevice Device(int i) => new("Device " + i, CertificateVaultService.Fingerprint(certificates[i]),
                Convert.ToBase64String(certificates[i].RawData), true);
            OfflineVaultGroup Group(string id, params int[] people) => new(id, id, people.Select(p => "person-" + p).ToArray());
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, Guid.NewGuid(), 1,
                [Group("lokal", 1, 2, 3, 4, 5, 6, 7, 8), Group("lokal-2", 3, 4, 5, 8), Group("lokal-3", 1, 2, 3, 6, 7, 8)])
            {
                People = Enumerable.Range(1, 8).Select(p => new OfflineVaultPerson("person-" + p, "Person " + p, true, [Device(p - 1)])).ToArray(),
                RecoveryDevices = [Device(8)]
            };
            var publicKey = administrator.ExportSubjectPublicKeyInfo();
            GroupEnrollmentPackage Transport(OfflineGroupPolicy p) => GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(
                new(GroupEnrollmentPackages.Format, 1, p.LibraryId, publicKey, OfflineGroupPolicyService.Sign(p, administrator))));
            var initial = Transport(policy);
            var protectors = Enumerable.Range(0, 9).Select(_ => new OfflineGroupTrustTests.TestProtection()).ToArray();
            OfflineGroupTrustStore Store(int i) => new(Path.Combine(directory, "workstation-" + i, "trust.bin"), policy.LibraryId, protectors[i]);
            for (var i = 0; i < 9; i++) Store(i).Enroll(initial.AdministratorPublicKey,
                OfflineGroupTrustStore.AdministratorFingerprint(publicKey), initial.Policy);

            string Payload(int group) => "{\"synthetic\":" + group + "}";
            var projects = policy.Groups.Select(_ => Guid.NewGuid()).ToArray();
            var vaults = policy.Groups.Select((g, i) => GroupVaultService.Protect(Payload(i), Store(0).LoadOffline(), projects[i], g.Id, now)).ToArray();
            var paths = policy.Groups.Select(g => Path.Combine(directory, g.Id + ".vault.json")).ToArray();
            for (var g = 0; g < vaults.Length; g++) File.WriteAllBytes(paths[g], GroupVaultService.Write(vaults[g]));
            var originals = paths.Select(File.ReadAllBytes).ToArray();
            string Open(int person, int group, bool recovery = false) => GroupVaultService.Unprotect(
                GroupVaultService.Read(File.ReadAllBytes(paths[group])), Store(person).LoadOffline(), projects[group], policy.Groups[group].Id, [certificates[person]], recovery);
            for (var g = 0; g < 3; g++)
            {
                for (var p = 0; p < 8; p++)
                {
                    if (policy.Groups[g].Members.Contains("person-" + (p + 1)))
                    { if (Open(p, g) != Payload(g)) throw new Exception("Expected matrix member cannot decrypt."); }
                    else Reject(() => Open(p, g));
                    Reject(() => Open(p, g, true));
                }
                if (Open(8, g, true) != Payload(g)) throw new Exception("Separate recovery failed.");
                Reject(() => Open(8, g));
            }

            // Replace person 3 with person 1 only in Lokal 2; person 3 stays deliberately offline first.
            var changedGroups = policy.Groups.ToArray(); changedGroups[1] = Group("lokal-2", 1, 4, 5, 8);
            var updated = Transport(policy with { Revision = 2, Groups = changedGroups });
            for (var p = 0; p < 9; p++)
                if (p != 2) Store(p).AcceptPackageUpdate(updated, Store(p).LoadOffline().Checkpoint);
            Reject(() => Open(0, 1)); // Policy alone does not wrap the vault key for a newly added member.
            if (Open(2, 1) != Payload(1)) throw new Exception("Offline historical access unexpectedly disappeared.");
            var plan = GroupVaultReconciliation.Preview(paths[1], Store(3), projects[1], "lokal-2", [certificates[3]], now);
            var result = GroupVaultReconciliation.Apply(plan, Store(3), [certificates[3]], now, privateBackupRoot);
            if (result.KeyGeneration == vaults[1].Header.KeyGeneration ||
                !File.ReadAllBytes(result.EncryptedBackupPath).SequenceEqual(originals[1]) ||
                !Path.GetFullPath(result.EncryptedBackupPath).StartsWith(
                    Path.GetFullPath(privateBackupRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(Path.Combine(directory, "GroupVaultBackups")))
                throw new Exception("Reconciliation rotated without private history storage outside the shared project tree.");
            Reject(() => Open(2, 1)); // Stale policy must not accept a newer vault.
            Store(2).AcceptPackageUpdate(updated, Store(2).LoadOffline().Checkpoint);
            for (var p = 0; p < 8; p++)
            {
                if (new[] { 0, 3, 4, 7 }.Contains(p))
                { if (Open(p, 1) != Payload(1)) throw new Exception("Updated matrix access failed."); }
                else Reject(() => Open(p, 1));
            }
            if (Open(8, 1, true) != Payload(1) || Open(2, 0) != Payload(0) || Open(2, 2) != Payload(2))
                throw new Exception("Scoped membership change broke other groups or recovery.");
            foreach (var g in new[] { 0, 2 })
                if (!File.ReadAllBytes(paths[g]).SequenceEqual(originals[g])) throw new Exception("Unrelated vault changed.");
            for (var p = 0; p < 9; p++)
                Reject(() => Store(p).AcceptPackageUpdate(initial, Store(p).LoadOffline().Checkpoint));
        }
        finally
        {
            foreach (var certificate in certificates) certificate.Dispose();
            foreach (var key in keys) key.Dispose();
            Directory.Delete(directory, true);
            if (Directory.Exists(privateBackupRoot)) Directory.Delete(privateBackupRoot, true);
        }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or InvalidDataException) { return; }
        throw new Exception("Unauthorized matrix operation succeeded.");
    }
}

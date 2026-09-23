using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class OfflineGroupDirectoryTests
{
    public static void Verify()
    {
        using var administrator = RSA.Create(3072);
        using var deviceKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        // Distinct test certificates share a synthetic RSA key to keep test setup inexpensive.
        var certificates = Enumerable.Range(1, 9).Select(i => new CertificateRequest("CN=Synthetic device " + i,
            deviceKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(now.AddDays(-1), now.AddDays(1))).ToArray();
        try
        {
            OfflineVaultDevice Device(int i) => new("Rechner " + i, CertificateVaultService.Fingerprint(certificates[i - 1]), Convert.ToBase64String(certificates[i - 1].RawData), true);
            var people = Enumerable.Range(1, 8).Select(i => new OfflineVaultPerson("person-" + i, "Person " + i, true,
                i == 3 ? [Device(i), Device(9)] : [Device(i)])).ToArray();
            OfflineVaultGroup Group(string id, params int[] members) => new(id, id, members.Select(i => "person-" + i).ToArray());
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, Guid.NewGuid(), 1,
                [Group("lokal", 1, 2, 3, 4, 5, 6, 7, 8), Group("lokal-2", 3, 4, 5, 8), Group("lokal-3", 1, 2, 3, 6, 7, 8)]) { People = people };
            var signed = OfflineGroupPolicyService.Sign(policy, administrator);
            var verified = OfflineGroupPolicyService.Verify(signed, administrator, policy.LibraryId).Policy;
            foreach (var (id, expected) in new[] { ("lokal", 9), ("lokal-2", 5), ("lokal-3", 7) })
                if (OfflineGroupDirectory.ResolveRecipients(verified, id, now).Length != expected) throw new Exception("Wrong overlapping access matrix.");
            var disabled = people.ToArray(); disabled[2] = people[2] with { Enabled = false };
            if (OfflineGroupDirectory.ResolveRecipients(policy with { People = disabled }, "lokal-2", now).Length != 3) throw new Exception("Disabled person still included.");
            disabled = people.ToArray(); disabled[2] = people[2] with { Devices = [Device(3), Device(9) with { Enabled = false }] };
            if (OfflineGroupDirectory.ResolveRecipients(policy with { People = disabled }, "lokal-2", now).Length != 4) throw new Exception("Disabled device still included.");
            Reject(() => OfflineGroupPolicyService.Sign(policy with { People = [people[0], people[0]] }, administrator));
            Reject(() => OfflineGroupPolicyService.Sign(policy with { People = [people[0], people[1] with { Devices = people[0].Devices }] }, administrator));
            Reject(() => OfflineGroupPolicyService.Sign(policy with { Groups = [Group("unknown", 10)] }, administrator));
            Reject(() => OfflineGroupPolicyService.Sign(policy with { Version = 1 }, administrator));
            Reject(() => OfflineGroupDirectory.ResolveRecipients(policy, "does-not-exist", now));
            Reject(() => OfflineGroupDirectory.ResolveRecipients(policy, "lokal", now.AddDays(2)));
            Reject(() => OfflineGroupDirectory.ResolveRecipients(policy, "lokal", now.AddDays(-2)));
            Reject(() => OfflineGroupDirectory.ResolveRecipients(policy with { People = people.Select(p => p with { Enabled = false }).ToArray() }, "lokal", now));
            var forged = people.ToArray(); forged[0] = people[0] with { Devices = [Device(1) with { Fingerprint = new string('0', 64) }] };
            Reject(() => OfflineGroupPolicyService.Sign(policy with { People = forged }, administrator));
            var pfx = people.ToArray(); pfx[0] = people[0] with { Devices = [Device(1) with { Certificate = Convert.ToBase64String(certificates[0].Export(X509ContentType.Pfx)) }] };
            Reject(() => OfflineGroupPolicyService.Sign(policy with { People = pfx }, administrator));
            Reject(() => CertificateVaultService.ImportInvitation(new VaultRecipientInvitation
            {
                Certificate = pfx[0].Devices[0].Certificate,
                Fingerprint = CertificateVaultService.Fingerprint(certificates[0]), DisplayName = "Private archive must not be imported"
            }));
        }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or InvalidDataException or FormatException) { return; }
        throw new Exception("Invalid person/device assignment accepted.");
    }
}

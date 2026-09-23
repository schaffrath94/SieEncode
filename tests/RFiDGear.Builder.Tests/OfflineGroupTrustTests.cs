using System.Security.Cryptography;
using RFiDGear.Builder.Core;

internal static class OfflineGroupTrustTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-Trust-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var admin = RSA.Create(3072);
            using var other = RSA.Create(3072);
            var library = Guid.NewGuid();
            var path = Path.Combine(directory, "local-trust.bin");
            var protection = new TestProtection();
            var store = new OfflineGroupTrustStore(path, library, protection);
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1, []);
            var signed = OfflineGroupPolicyService.Sign(policy, admin);
            var key = admin.ExportSubjectPublicKeyInfo();
            var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(key);
            Reject(() => store.LoadOffline());
            Reject(() => store.Enroll(key, new string('0', 64), signed));
            if (File.Exists(path)) throw new Exception("Unconfirmed trust persisted.");
            store.Enroll(key, fingerprint, signed);
            Reject(() => store.Enroll(key, fingerprint, signed));
            var restarted = new OfflineGroupTrustStore(path, library, protection);
            if (restarted.LoadOffline().Policy.Revision != 1) throw new Exception("Offline restart lost policy.");
            var updated = OfflineGroupPolicyService.Sign(policy with { Revision = 2 }, admin);
            restarted.AcceptUpdate(updated);
            var before = File.ReadAllBytes(path);
            Reject(() => restarted.AcceptUpdate(signed));
            Reject(() => restarted.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 3 }, other)));
            Reject(() => new OfflineGroupTrustStore(path, Guid.NewGuid(), protection).LoadOffline());
            Reject(() => new OfflineGroupTrustStore(path, library, new TestProtection()).LoadOffline());
            if (!before.SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Rejected operation changed trust.");
            // A concurrent valid update must win over a stale writer.
            protection.BeforeProtect = () =>
            {
                protection.BeforeProtect = null;
                new OfflineGroupTrustStore(path, library, protection).AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 4 }, admin));
            };
            Reject(() => restarted.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 3 }, admin)));
            if (restarted.LoadOffline().Policy.Revision != 4) throw new Exception("Concurrent update overwritten.");
            var corrupted = File.ReadAllBytes(path); corrupted[^1] ^= 1; File.WriteAllBytes(path, corrupted);
            Reject(() => restarted.LoadOffline());
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException) { return; }
        throw new Exception("Invalid trust operation succeeded.");
    }

    // Synthetic authenticated protector only; not a production OS-user protection implementation.
    internal sealed class TestProtection : ILocalGroupTrustProtection
    {
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public Action? BeforeProtect { get; set; }
        public byte[] Protect(byte[] plaintext)
        {
            BeforeProtect?.Invoke();
            var output = new byte[28 + plaintext.Length];
            RandomNumberGenerator.Fill(output.AsSpan(0, 12));
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(output.AsSpan(0, 12), plaintext, output.AsSpan(28), output.AsSpan(12, 16));
            return output;
        }
        public byte[] Unprotect(byte[] state)
        {
            var output = new byte[state.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(state.AsSpan(0, 12), state.AsSpan(28), state.AsSpan(12, 16), output);
            return output;
        }
    }
}

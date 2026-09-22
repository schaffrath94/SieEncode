using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private static void VerifyWindowsGroupTrust(List<string> checks)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-WindowsTrust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var administrator = RSA.Create(3072);
            var library = Guid.NewGuid();
            var key = administrator.ExportSubjectPublicKeyInfo();
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1, []);
            var signed = OfflineGroupPolicyService.Sign(policy, administrator);
            var path = Path.Combine(directory, "trust.bin");
            new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library))
                .Enroll(key, OfflineGroupTrustStore.AdministratorFingerprint(key), signed);
            var reloaded = new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library)).LoadOffline();
            if (reloaded.Policy.LibraryId != library || reloaded.Policy.Revision != 1)
                throw new InvalidOperationException("Windows-Vertrauensspeicher nicht wieder lesbar.");
            var protectedBytes = File.ReadAllBytes(path);
            Reject(() => new WindowsGroupTrustProtection(Guid.NewGuid()).Unprotect(protectedBytes));
            protectedBytes[^1] ^= 1;
            File.WriteAllBytes(path, protectedBytes);
            Reject(() => new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library)).LoadOffline());
            checks.Add("Offline-Gruppen: echter Windows-DPAPI-Roundtrip im aktuellen Benutzer; falsche Bibliotheksbindung und manipulierte Datei abgewiesen. Keine Zweitbenutzerabnahme.");
        }
        finally { Directory.Delete(directory, true); }

        static void Reject(Action action)
        {
            try { action(); }
            catch (CryptographicException) { return; }
            throw new InvalidOperationException("Ungültiger Windows-Vertrauensspeicher wurde akzeptiert.");
        }
    }
}

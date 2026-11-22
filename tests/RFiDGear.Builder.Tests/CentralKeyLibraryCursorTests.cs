using System.Security.Cryptography;
using RFiDGear.Builder.Core;

internal static class CentralKeyLibraryCursorTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "SieEncode-central-cursor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var lib = Guid.NewGuid();
        var file = Path.Combine(directory, "cursor.bin");
        var protector = new OfflineGroupTrustTests.TestProtection();
        var cursor = new CentralKeyLibraryCursorStore(file, lib, protector);
        var policy = new GroupPolicyCheckpoint(1, new string('A', 64));
        var initial = new CentralKeyLibraryHead(lib, 0, "", "", policy, 0);
        var version1 = new CentralKeyLibraryHead(lib, 1,
            new string('B', 64), "keys", policy, 1);
        var version2 = new CentralKeyLibraryHead(lib, 2,
            new string('C', 64), "keys", policy, 2);
        try
        {
            cursor.RequireNotRolledBack(initial);
            cursor.Remember(initial);
            cursor.Remember(version1);
            var reloaded = new CentralKeyLibraryCursorStore(file, lib, protector);
            reloaded.RequireNotRolledBack(version1);
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(initial));
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(
                version1 with { CiphertextSha256 = new string('D', 64) }));
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(
                version1 with { CatalogRevision = 2 }));
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(
                version2 with { CatalogRevision = 1 }));
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(
                version2 with { Policy = policy with { Revision = 2 } }));
            Reject<CryptographicException>(() => reloaded.RequireNotRolledBack(
                version2 with { GroupId = "other" }));
            reloaded.Remember(version2);
            if (File.ReadAllText(file).Contains(version2.CiphertextSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new Exception("Central cursor was written unprotected.");
            Reject<CryptographicException>(() => cursor.Remember(version1));
            Reject<CryptographicException>(() =>
                new CentralKeyLibraryCursorStore(file, lib,
                    new OfflineGroupTrustTests.TestProtection()).RequireNotRolledBack(version2));
            Reject<CryptographicException>(() =>
                new CentralKeyLibraryCursorStore(file, Guid.NewGuid(), protector)
                    .RequireNotRolledBack(version2));
            new CentralKeyLibraryCursorStore(file, lib, protector)
                .RequireNotRolledBack(version2);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Central anti-rollback or identity check was bypassed: " +
            typeof(T).Name);
    }
}

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
            var local1 = new FileRevision(true, new string('D', 64));
            var localEdited = new FileRevision(true, new string('E', 64));
            if (cursor.HasUploadBase(version1) || cursor.HasUnchangedLocalBase(local1))
                throw new Exception("An observed head was incorrectly treated as a successful reconciliation.");
            cursor.RememberReconciled(version1, local1);
            var reloaded = new CentralKeyLibraryCursorStore(file, lib, protector);
            reloaded.RequireNotRolledBack(version1);
            if (!reloaded.HasUploadBase(version1) ||
                !reloaded.HasUnchangedLocalBase(local1) ||
                reloaded.HasUnchangedLocalBase(localEdited))
                throw new Exception("Reconciled base did not distinguish an offline edit.");
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
            reloaded.Remember(version2); // conflict/observation, NOT reconciliation
            if (reloaded.HasUploadBase(version2) ||
                !reloaded.HasUnchangedLocalBase(local1) ||
                reloaded.HasUnchangedLocalBase(localEdited))
                throw new Exception("Conflict observation incorrectly replaced the last shared base.");
            var afterConflict = new CentralKeyLibraryCursorStore(file, lib, protector);
            if (afterConflict.HasUploadBase(version2))
                throw new Exception("Restart turned a conflict into an eligible automatic upload.");
            Reject<InvalidDataException>(() => afterConflict.RememberReconciled(version2, FileRevision.Missing));
            afterConflict.RememberReconciled(version2, localEdited);
            if (!afterConflict.HasUploadBase(version2) ||
                !afterConflict.HasUnchangedLocalBase(localEdited) ||
                afterConflict.HasUnchangedLocalBase(local1))
                throw new Exception("A successful reconciliation did not replace the base.");
            if (File.ReadAllText(file).Contains(version2.CiphertextSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new Exception("Central cursor was written unprotected.");
            Reject<CryptographicException>(() => cursor.Remember(version1));
            Reject<CryptographicException>(() =>
                new CentralKeyLibraryCursorStore(file, lib,
                    new OfflineGroupTrustTests.TestProtection()).RequireNotRolledBack(version2));
            Reject<InvalidDataException>(() =>
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

using System.Security.Cryptography;
using RFiDGear.Builder.Core;

internal static class KeyLibraryAdministrationTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "SieEncode-key-admin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var libraryId = Guid.NewGuid();
            var ownProtection = new OfflineGroupTrustTests.TestProtection();
            var registeredProtection = new OfflineGroupTrustTests.TestProtection();
            var administration = new OfflineGroupAdministrationStore(
                Path.Combine(directory, "admin.bin"), libraryId, ownProtection);
            var package = administration.Create();
            var trust = new OfflineGroupTrustStore(
                Path.Combine(directory, "trust.bin"), libraryId, registeredProtection);
            trust.Enroll(package.AdministratorPublicKey,
                OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey),
                package.Policy);
            var first = GroupEnrollmentPackages.Validate(package).Checkpoint;
            if (administration.RequireLocalAuthority(trust) != first)
                throw new Exception("Confirmed signing authority did not match local trust.");

            var stranger = new OfflineGroupAdministrationStore(
                Path.Combine(directory, "stranger.bin"), libraryId,
                new OfflineGroupTrustTests.TestProtection());
            stranger.Create();
            Reject<CryptographicException>(() => stranger.RequireLocalAuthority(trust));

            var changed = administration.UpdateGroups([], first);
            Reject<CryptographicException>(() => administration.RequireLocalAuthority(trust));
            trust.AcceptUpdate(changed.Policy);
            if (administration.RequireLocalAuthority(trust) !=
                GroupEnrollmentPackages.Validate(changed).Checkpoint)
                throw new Exception("Current signed checkpoint did not authorize administration.");

            var otherLibrary = new OfflineGroupTrustStore(
                Path.Combine(directory, "other.bin"), Guid.NewGuid(),
                new OfflineGroupTrustTests.TestProtection());
            Reject<Exception>(() => administration.RequireLocalAuthority(otherLibrary));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Invalid key-management signing authority was accepted.");
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>Human disclosure permission for the signed person and exact
/// personal device. Not a cryptographic sub-scope of the decrypted catalog.</summary>
public sealed record KeyLibraryDisclosureAccess(string PersonId, string DeviceFingerprint,
    bool CanView, bool CanCopy);

public static class KeyLibraryDisclosure
{
    public static KeyLibraryDisclosureAccess RequireMember(VerifiedGroupPolicy verified,
        string groupId, X509Certificate2 identity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(verified);
        ArgumentNullException.ThrowIfNull(identity);
        var policy = verified.Policy;
        var group = policy.Groups.SingleOrDefault(g => g.Id == groupId);
        if (policy.Version != 2 || group is null || !identity.HasPrivateKey ||
            now.UtcDateTime < identity.NotBefore.ToUniversalTime() ||
            now.UtcDateTime >= identity.NotAfter.ToUniversalTime())
            throw new CryptographicException("Aktiver persönlicher Gruppen-Zugang erforderlich.");
        var fingerprint = CertificateVaultService.Fingerprint(identity);
        var encoded = Convert.ToBase64String(identity.RawData);
        var person = policy.People.SingleOrDefault(p => p.Enabled &&
            group.Members.Contains(p.Id, StringComparer.Ordinal) &&
            p.Devices.Any(d => d.Enabled && d.Fingerprint == fingerprint && d.Certificate == encoded));
        if (person is null)
            throw new CryptographicException("Persönliches Gerät ist in dieser Gruppe nicht freigegeben.");
        return new(person.Id, fingerprint, person.AllowKeyLibraryView,
            person.AllowKeyLibraryView && person.AllowKeyLibraryCopy);
    }

    /// <summary>Reopen the exact selected revision under the currently pinned
    /// trust and matching device. Never silently substitute a later version,
    /// customer or changed status. Return only the selected secret.</summary>
    public static string ReadSelected(string path, OfflineGroupTrustStore trust,
        string groupId, X509Certificate2 identity, GroupPolicyCheckpoint expectedPolicy,
        long expectedCatalogRevision, FileRevision expectedFile, KeyLibraryEntry selected,
        bool forCopy, DateTimeOffset now)
    {
        var policy = trust.LoadOffline();
        var access = RequireMember(policy, groupId, identity, now);
        if (!access.CanView || forCopy && !access.CanCopy)
            throw new CryptographicException("Ausdrückliche Klartextfreigabe fehlt.");
        if (policy.Checkpoint != expectedPolicy ||
            ConcurrentFileWriter.Capture(path) != expectedFile)
            throw new ConcurrentFileChangeException(path);
        var payload = KeyLibraryVaultStore.Load(path, trust, groupId, [identity]);
        var entry = payload.Catalog.Keys.SingleOrDefault(k => k.KeyId == selected.KeyId &&
            k.Version == selected.Version);
        if (payload.Catalog.Revision != expectedCatalogRevision || entry != selected ||
            trust.LoadOffline().Checkpoint != expectedPolicy ||
            ConcurrentFileWriter.Capture(path) != expectedFile)
            throw new ConcurrentFileChangeException(path);
        return payload.Secrets.Single(s => s.Key.KeyId == selected.KeyId &&
            s.Key.Version == selected.Version).HexValue;
    }
}

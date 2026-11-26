using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>
/// A signed new public policy and the complete re-encrypted catalog for that
/// policy. The inner upload signature binds the exact previous central CAS
/// base, the new policy checkpoint, catalog revision and encrypted bytes.
/// No private signing key or plaintext catalog leaves Studio.
/// </summary>
public sealed record SignedKeyLibraryPolicyTransition(
    GroupEnrollmentPackage Enrollment, SignedKeyLibrarySync Rewrapped);

/// <summary>Current directory and encrypted snapshot read from one authorized
/// server state. The existing download proof binds the requested head.</summary>
public sealed record CentralKeyLibrarySnapshot(CentralKeyLibraryHead Head,
    GroupEnrollmentPackage Enrollment, SignedKeyLibrarySync Transfer);

public static class KeyLibraryPolicyTransition
{
    /// <summary>
    /// Compare against an already independently pinned previous enrollment.
    /// This operation cannot replace the administrator root or library/group.
    /// </summary>
    public static VerifiedGroupPolicy VerifyNextPolicy(GroupEnrollmentPackage previous,
        GroupEnrollmentPackage next, CentralKeyLibraryHead expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var old = GroupEnrollmentPackages.Validate(previous);
        var candidate = GroupEnrollmentPackages.Validate(next);
        if (old.Policy.CustomerScopesEnabled && !candidate.Policy.CustomerScopesEnabled)
            throw new CryptographicException("Kryptografische Kundentrennung darf nicht auf eine Gesamtfreigabe zurückgesetzt werden.");
        if (expected.CentralRevision < 1 || expected.CentralRevision == long.MaxValue ||
            expected.CatalogRevision < 1 || string.IsNullOrEmpty(expected.GroupId) ||
            expected.LibraryId != previous.LibraryId ||
            previous.LibraryId != next.LibraryId ||
            old.Policy.Version != 2 || candidate.Policy.Version != 2 ||
            old.Checkpoint != expected.Policy ||
            candidate.Checkpoint.Revision <= old.Checkpoint.Revision ||
            !previous.AdministratorPublicKey.SequenceEqual(next.AdministratorPublicKey))
            throw new CryptographicException("Richtlinienwechsel benötigt denselben bestätigten Verwalter, eine neuere Richtlinie und einen vorhandenen zentralen Bibliotheksstand.");
        return candidate;
    }

    public static KeyLibrarySyncClaim Verify(SignedKeyLibraryPolicyTransition transition,
        GroupEnrollmentPackage previous, CentralKeyLibraryHead expected)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _ = VerifyNextPolicy(previous, transition.Enrollment, expected);
        var claim = KeyLibrarySyncTransport.Verify(transition.Rewrapped, transition.Enrollment);
        if (claim.ExpectedCentralRevision != expected.CentralRevision ||
            claim.ExpectedCentralCiphertextSha256 != expected.CiphertextSha256 ||
            claim.GroupId != expected.GroupId ||
            claim.CatalogRevision != expected.CatalogRevision)
            throw new ConcurrentFileChangeException("Zentraler Richtlinienwechsel");
        // A rewrap alone never edits the logical key catalog. The server
        // cannot independently inspect plaintext; the signing Studio must
        // verify the unchanged payload before constructing this operation.
        if (claim.NextCiphertextSha256 == expected.CiphertextSha256)
            throw new CryptographicException("Richtlinienwechsel benötigt neu verschlüsselte Vault-Daten.");
        return claim;
    }
    /// <summary>Read-only catch-up may skip intermediate epochs. The pinned
    /// server supplies the current head; the original administrator must sign
    /// both the current directory and exact encrypted catalog. This is not
    /// an independently anchored proof of every intermediate revision.</summary>
    public static KeyLibrarySyncClaim VerifyReceivedSnapshot(GroupEnrollmentPackage previous,
        CentralKeyLibraryHead baseline, CentralKeyLibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var head = snapshot.Head;
        var next = VerifyNextPolicy(previous, snapshot.Enrollment, baseline);
        var claim = KeyLibrarySyncTransport.Verify(snapshot.Transfer, snapshot.Enrollment);
        if (head.LibraryId != baseline.LibraryId || head.GroupId != baseline.GroupId ||
            head.Policy != next.Checkpoint || head.CentralRevision <= baseline.CentralRevision ||
            head.CatalogRevision < baseline.CatalogRevision ||
            head.CentralRevision != claim.ExpectedCentralRevision + 1 ||
            head.CatalogRevision != claim.CatalogRevision ||
            head.GroupId != claim.GroupId || head.CiphertextSha256 != claim.NextCiphertextSha256 ||
            head.CiphertextSha256 == baseline.CiphertextSha256 ||
            (head.CentralRevision == baseline.CentralRevision + 1 &&
                (claim.ExpectedCentralCiphertextSha256 != baseline.CiphertextSha256 ||
                 head.CatalogRevision != baseline.CatalogRevision)))
            throw new CryptographicException("Empfangener Richtlinienstand passt nicht zum abgeglichenen Vorgänger oder signierten aktuellen Vault.");
        return claim;
    }

    // Both enrollments must already have been validated against the same pinned
    // root and the exact central transition. Never trust a root from the Vault.
    internal static KeyLibraryVaultPayload OpenEpoch(byte[] bytes, GroupEnrollmentPackage enrollment,
        string group, X509Certificate2 identity, DateTimeOffset now, bool requireFull)
    {
        var verified = GroupEnrollmentPackages.Validate(enrollment);
        _ = KeyLibraryDisclosure.RequireMember(verified, group, identity, now);
        if (!verified.Policy.CustomerScopesEnabled)
            return KeyLibraryVault.Unprotect(GroupVaultService.Read(bytes), verified, group, [identity]);
        return ScopedKeyLibraryVault.OpenVerified(
            KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(bytes, GroupVaultService.MaximumBytes),
            enrollment, group, identity, now, requireFull);
    }

    internal static void ValidateReceivedHistory(byte[] oldBytes, byte[] newBytes,
        GroupEnrollmentPackage oldEnrollment, GroupEnrollmentPackage newEnrollment,
        string group, X509Certificate2 identity, KeyLibraryVaultPayload oldView,
        KeyLibraryVaultPayload newView)
    {
        var old = GroupEnrollmentPackages.Validate(oldEnrollment).Policy;
        var next = GroupEnrollmentPackages.Validate(newEnrollment).Policy;
        var sameRevision = oldView.Catalog.Revision == newView.Catalog.Revision;
        // Signed bundle exposes stable customer IDs, even to a partial client.
        // It cannot prove immutable secrets for customers it cannot decrypt.
        Guid[] Ids(byte[] bytes, GroupEnrollmentPackage enrollment, OfflineGroupPolicy policy,
            KeyLibraryVaultPayload view) => policy.CustomerScopesEnabled
            ? ScopedKeyLibraryVault.Verify(KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(
                bytes, GroupVaultService.MaximumBytes), enrollment, group).Customers.Select(c => c.CustomerId).ToArray()
            : view.Catalog.Customers.Select(c => c.CustomerId).ToArray();
        var previousIds = Ids(oldBytes, oldEnrollment, old, oldView).ToHashSet();
        var nextIds = Ids(newBytes, newEnrollment, next, newView).ToHashSet();
        if (newView.Catalog.Revision < oldView.Catalog.Revision ||
            !previousIds.IsSubsetOf(nextIds) || sameRevision && !previousIds.SetEquals(nextIds))
            throw new CryptographicException("Kundenhistorie oder Katalogrevision wurde unzulässig verändert.");

        var fingerprint = CertificateVaultService.Fingerprint(identity);
        var newPerson = next.People.Single(p => p.Devices.Any(d => d.Fingerprint == fingerprint));
        // Explicitly determine retained customers from the new signed grant,
        // not by intersecting only whatever happens to exist in the payload.
        var retained = oldView.Catalog.Customers.Where(c => !next.CustomerScopesEnabled ||
            OfflineGroupDirectory.CanDecryptCustomer(next, newPerson, c.CustomerId))
            .Select(c => c.CustomerId).ToHashSet();
        if (!retained.IsSubsetOf(newView.Catalog.Customers.Select(c => c.CustomerId)))
            throw new CryptographicException("Weiterhin berechtigter Kunde fehlt im empfangenen Bestand.");
        KeyLibraryVaultPayload Project(KeyLibraryVaultPayload view)
        {
            var keys = view.Catalog.Keys.Where(k => retained.Contains(k.CustomerId)).ToArray();
            var addresses = keys.Select(k => new KeyLibraryAddress(k.KeyId, k.Version)).ToHashSet();
            return new(view.Catalog with
                { Customers = view.Catalog.Customers.Where(c => retained.Contains(c.CustomerId)).ToArray(), Keys = keys },
                view.Secrets.Where(s => addresses.Contains(s.Key)).ToArray());
        }
        var before = Project(oldView);
        var after = Project(newView);
        KeyLibraryVaultStore.ValidateImmutableHistory(before, after);
        if (sameRevision && !KeyLibraryAutomaticSync.LogicallyEquivalent(before, after))
            throw new CryptographicException("Weiterhin berechtigter Katalog wurde ohne neue Revision verändert.");
    }
}

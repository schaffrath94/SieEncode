using System.Security.Cryptography;

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
}

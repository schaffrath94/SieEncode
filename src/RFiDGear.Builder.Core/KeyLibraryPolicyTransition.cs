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
}

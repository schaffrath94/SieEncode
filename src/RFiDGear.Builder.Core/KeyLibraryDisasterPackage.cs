using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record KeyLibraryDisasterPackageReference(Guid PackageId, Guid LibraryId,
    string GroupId, string AdministratorFingerprint, GroupPolicyCheckpoint Policy,
    long CatalogRevision, string PackageSha256);

public sealed record KeyLibraryDisasterPackageVerification(KeyLibraryDisasterPackageReference Reference,
    string CiphertextSha256, string ApprovedPreviewSha256, int SourceCount,
    int CustomerCount, int KeyVersionCount, CentralKeyLibraryHead? LastKnownHead,
    DateTimeOffset PreparedAt);

internal sealed record KeyLibraryDisasterPackageClaim(string Format, Guid PackageId,
    KeyLibraryDisasterCandidatePreview Preview, string ApprovedPreviewSha256,
    string CiphertextSha256, string EnrollmentSha256, bool LostServerHistoryAcknowledged,
    DateTimeOffset PreparedAt);

/// <summary>Portable, signed, encrypted, non-operational recovery evidence.
/// No server state, central revision, private identity, or activation grant.</summary>
public static class KeyLibraryDisasterPackage
{
    internal const string Format = "SieEncode-Isolated-Disaster-Package-v1";
    private const int MaximumBytes = KeyLibrarySyncWire.MaximumEncryptedBytes;
    private const int MaximumClaimBytes = 256 * 1024;
    private sealed record Archive(string Format, byte[] Claim, byte[] Signature,
        byte[] Enrollment, byte[] EncryptedVault);

    /// <summary>Reconstruct from the approved sources inside this operation;
    /// do not accept caller-constructed candidates as provenance. The caller
    /// holds the common administration lease and selects protected storage.</summary>
    public static KeyLibraryDisasterPackageReference Create(string destination,
        OfflineGroupTrustStore trust, OfflineGroupAdministrationStore administration,
        string groupId, X509Certificate2 recoveryIdentity, X509Certificate2 fullMember,
        DateTimeOffset now, IEnumerable<KeyLibraryDisasterSource> sources,
        Guid selectedSourceId, CentralKeyLibraryHead? lastKnownHead, string approvedPreviewSha256,
        bool explicitlyApproved, bool acknowledgedLostServerHistory)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var input = sources.Take(9).ToArray();
        var target = Path.GetFullPath(destination);
        if (input.Any(s => s is null || string.Equals(Path.GetFullPath(s.VaultPath), target,
            StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Rekonstruktionspaket benötigt einen separaten neuen Dateipfad.");
        var candidate = KeyLibraryDisasterAnalysis.PrepareDisasterCandidate(trust, administration,
            groupId, recoveryIdentity, fullMember, now, input, selectedSourceId, lastKnownHead,
            approvedPreviewSha256, explicitlyApproved, acknowledgedLostServerHistory);
        var enrollment = GroupEnrollmentPackages.Write(candidate.Enrollment);
        var claim = new KeyLibraryDisasterPackageClaim(Format, Guid.NewGuid(), candidate.Preview,
            approvedPreviewSha256, candidate.CiphertextSha256, Hash(enrollment),
            acknowledgedLostServerHistory, now);
        var claimBytes = KeyLibrarySyncWire.Write(claim);
        if (claimBytes.Length > MaximumClaimBytes)
            throw new InvalidDataException("Rekonstruktionsnachweis ist zu groß.");
        var signature = administration.SignDisasterPackageClaim(trust, claim, claimBytes);
        var archive = new Archive(Format, claimBytes, signature, enrollment, candidate.EncryptedVault);
        var bytes = KeyLibrarySyncWire.Write(archive);
        if (bytes.Length > MaximumBytes)
            throw new InvalidDataException("Rekonstruktionspaket ist zu groß.");
        var reference = new KeyLibraryDisasterPackageReference(claim.PackageId,
            candidate.Preview.Inventory.LibraryId, groupId, candidate.Preview.AdministratorFingerprint,
            candidate.Preview.Inventory.CurrentPolicy, candidate.Preview.ProposedCatalogRevision!.Value,
            Hash(bytes));

        void CheckSources()
        {
            if (administration.RequireLocalAuthority(trust) != reference.Policy ||
                OfflineGroupTrustStore.AdministratorFingerprint(trust.ExportPublic().AdministratorPublicKey)
                    != reference.AdministratorFingerprint)
                throw new CryptographicException("Verwaltung während der Rekonstruktion geändert.");
            foreach (var source in input)
            {
                var verified = candidate.Preview.Inventory.Sources.Single(s => s.SourceId == source.SourceId);
                if (ConcurrentFileWriter.Capture(source.VaultPath) != new FileRevision(true, verified.CiphertextSha256))
                    throw new ConcurrentFileChangeException(source.VaultPath);
            }
        }

        CheckSources();
        _ = VerifyBytes(bytes, reference, recoveryIdentity, now);
        var written = ConcurrentFileWriter.Write(target, FileRevision.Missing, temporary =>
        {
            CheckSources();
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
            CheckSources();
        });
        if (written.Sha256 != reference.PackageSha256)
            throw new ConcurrentFileChangeException(target);
        _ = Verify(target, reference, recoveryIdentity, now);
        CheckSources();
        return reference;
    }

    /// <summary>Verify a separately retained reference and recovery identity.
    /// No installed trust or live server needed. This attests the signed
    /// recorded provenance; it cannot re-prove absent original sources.</summary>
    public static KeyLibraryDisasterPackageVerification Verify(string packagePath,
        KeyLibraryDisasterPackageReference independentlyConfirmed, X509Certificate2 recoveryIdentity,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(independentlyConfirmed);
        using var stream = new FileStream(Path.GetFullPath(packagePath), FileMode.Open,
            FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes)
            throw new InvalidDataException("Rekonstruktionspaket ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return VerifyBytes(bytes, independentlyConfirmed, recoveryIdentity, now);
    }

    private static KeyLibraryDisasterPackageVerification VerifyBytes(byte[] bytes,
        KeyLibraryDisasterPackageReference reference, X509Certificate2 recoveryIdentity, DateTimeOffset now)
    {
        if (Hash(bytes) != reference.PackageSha256)
            throw new CryptographicException("Rekonstruktionspaket entspricht nicht der unabhängig bestätigten Referenz.");
        var archive = KeyLibrarySyncWire.Read<Archive>(bytes, MaximumBytes);
        if (archive.Format != Format || archive.Claim is not { Length: > 0 and <= MaximumClaimBytes } ||
            archive.Signature is not { Length: >= 384 and <= 2048 } ||
            archive.Enrollment is not { Length: > 0 and <= GroupEnrollmentPackages.MaximumBytes } ||
            archive.EncryptedVault is not { Length: > 0 and <= GroupVaultService.MaximumBytes })
            throw new InvalidDataException("Ungültiges Rekonstruktionspaket.");
        var enrolled = GroupEnrollmentPackages.Read(archive.Enrollment);
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        using var authority = OfflineGroupDelegationService.OpenPublicKey(enrolled.AdministratorPublicKey);
        if (enrolled.LibraryId != reference.LibraryId || policy.Checkpoint != reference.Policy ||
            !policy.Policy.CustomerScopesEnabled ||
            OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey) != reference.AdministratorFingerprint ||
            !authority.VerifyData(archive.Claim, archive.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Rekonstruktionsnachweis besitzt keine bestätigte Verwaltungssignatur.");
        var claim = KeyLibrarySyncWire.Read<KeyLibraryDisasterPackageClaim>(archive.Claim, MaximumClaimBytes);
        if (!archive.Claim.SequenceEqual(KeyLibrarySyncWire.Write(claim)) || claim.Format != Format ||
            claim.PackageId == Guid.Empty || claim.PackageId != reference.PackageId ||
            !claim.LostServerHistoryAcknowledged || claim.PreparedAt == default ||
            claim.PreparedAt > now.AddMinutes(2) || claim.Preview is null ||
            !claim.Preview.CanPrepare || claim.Preview.ApprovalSha256() != claim.ApprovedPreviewSha256 ||
            claim.Preview.AdministratorFingerprint != reference.AdministratorFingerprint ||
            claim.Preview.Inventory.LibraryId != reference.LibraryId ||
            claim.Preview.Inventory.GroupId != reference.GroupId ||
            claim.Preview.Inventory.CurrentPolicy != reference.Policy ||
            claim.Preview.ProposedCatalogRevision != reference.CatalogRevision ||
            claim.EnrollmentSha256 != Hash(archive.Enrollment) ||
            claim.CiphertextSha256 != KeyLibrarySyncTransport.CiphertextSha256(archive.EncryptedVault))
            throw new CryptographicException("Rekonstruktionsnachweis und verschlüsselter Bestand stimmen nicht überein.");
        var inventory = claim.Preview.Inventory.Sources;
        if (inventory is not { Length: > 0 and <= 8 } ||
            inventory.Any(s => s is null || !s.CurrentPolicy || s.Policy != reference.Policy ||
                s.CatalogRevision <= 0 || s.CatalogRevision >= reference.CatalogRevision) ||
            inventory.Select(s => s.SourceId).Distinct().Count() != inventory.Length)
            throw new InvalidDataException("Ungültige Quellinventur im Rekonstruktionsnachweis.");
        var selected = inventory.SingleOrDefault(s => s.SourceId == claim.Preview.SelectedSourceId)
            ?? throw new InvalidDataException("Ausgewählte Rekonstruktionsquelle fehlt.");
        var known = claim.Preview.LastKnownHead;
        if (known is not null && (known.LibraryId != reference.LibraryId ||
            known.GroupId != reference.GroupId || known.Policy != reference.Policy ||
            known.CentralRevision <= 0 || known.CatalogRevision >= reference.CatalogRevision ||
            !inventory.Any(s => s.CiphertextSha256 == known.CiphertextSha256 &&
                s.CatalogRevision == known.CatalogRevision)))
            throw new CryptographicException("Bekannter alter Zentralstand ist nicht im Nachweis gebunden.");
        var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(archive.EncryptedVault, GroupVaultService.MaximumBytes);
        var restored = ScopedKeyLibraryVault.OpenVerified(signed, enrolled, reference.GroupId,
            recoveryIdentity, now, requireFull: true, recovery: true);
        if (restored.Catalog.Revision != reference.CatalogRevision ||
            restored.Catalog.Customers.Length != selected.CustomerCount ||
            restored.Catalog.Keys.Length != selected.KeyVersionCount)
            throw new CryptographicException("Entschlüsselter Rekonstruktionsbestand entspricht nicht dem Nachweis.");
        return new(reference, claim.CiphertextSha256, claim.ApprovedPreviewSha256,
            inventory.Length, restored.Catalog.Customers.Length, restored.Catalog.Keys.Length,
            known, claim.PreparedAt);
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}

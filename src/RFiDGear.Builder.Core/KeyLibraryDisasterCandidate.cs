using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public enum KeyLibraryDisasterCandidateBlock
{
    HistoricalPolicy, MissingKnownCentralSnapshot, MissingOrConflictingHistory,
    MetadataOrStatusConflict, RevisionExhausted
}

// No claim of completeness beyond the supplied verified sources.
public sealed record KeyLibraryDisasterCandidatePreview(KeyLibraryDisasterReport Inventory,
    Guid SelectedSourceId, string AdministratorFingerprint, CentralKeyLibraryHead? LastKnownHead,
    KeyLibraryDisasterCandidateBlock[] Blocks, long? ProposedCatalogRevision)
{
    public bool CanPrepare => Blocks.Length == 0 && ProposedCatalogRevision.HasValue;

    public string ApprovalSha256() => Convert.ToHexString(SHA256.HashData(KeyLibrarySyncWire.Write(new
    {
        Format = "SieEncode-Isolated-Disaster-Candidate-v1",
        Preview = this
    })));
}

// Deliberately not a central sync transfer. No central revision, acceptance
// receipt, activation grant or replacement server state is manufactured.
public sealed record KeyLibraryIsolatedDisasterCandidate(KeyLibraryDisasterCandidatePreview Preview,
    string ApprovedPreviewSha256, string CiphertextSha256, byte[] EncryptedVault,
    GroupEnrollmentPackage Enrollment);

public static partial class KeyLibraryDisasterAnalysis
{
    /// <summary>Conservative disaster reconstruction without a working server.
    /// Only a single selected complete superset is supported for now.
    /// No union of divergent histories and no implied proof of freshness.</summary>
    public static KeyLibraryDisasterCandidatePreview PreviewDisasterCandidate(
        OfflineGroupTrustStore trust, string groupId, X509Certificate2 recoveryIdentity,
        DateTimeOffset now, IEnumerable<KeyLibraryDisasterSource> sources,
        Guid selectedSourceId, CentralKeyLibraryHead? lastKnownHead = null) =>
        WithVerifiedSources(trust, groupId, recoveryIdentity, true, now, sources, lastKnownHead,
            (report, opened) => BuildDisasterCandidatePreview(trust, report, opened, selectedSourceId, lastKnownHead));

    /// <summary>Produce only a newly signed encrypted isolated candidate.
    /// Requires the distinct recovery and full personal identities and the
    /// protected current administration authority. No writes or server calls.</summary>
    public static KeyLibraryIsolatedDisasterCandidate PrepareDisasterCandidate(
        OfflineGroupTrustStore trust, OfflineGroupAdministrationStore administration,
        string groupId, X509Certificate2 recoveryIdentity, X509Certificate2 fullMember,
        DateTimeOffset now, IEnumerable<KeyLibraryDisasterSource> sources,
        Guid selectedSourceId, CentralKeyLibraryHead? lastKnownHead,
        string approvedPreviewSha256, bool explicitlyApproved, bool acknowledgedLostServerHistory)
    {
        if (!explicitlyApproved || !acknowledgedLostServerHistory)
            throw new InvalidOperationException("Isolierte Rekonstruktion und Verlust zentraler Historie ausdrücklich bestätigen.");
        var checkpoint = administration.RequireLocalAuthority(trust);
        var current = trust.ExportPublic();
        var policy = GroupEnrollmentPackages.Validate(current);
        var member = KeyLibraryDisclosure.RequireMember(policy, groupId, fullMember, now);
        if (!policy.Policy.People.Single(p => p.Id == member.PersonId).AllKeyLibraryCustomers ||
            CertificateVaultService.Fingerprint(fullMember) == CertificateVaultService.Fingerprint(recoveryIdentity) ||
            policy.Checkpoint != checkpoint)
            throw new CryptographicException("Separater vollständiger persönlicher Zugang und aktuelle Hauptverwaltung erforderlich.");

        return WithVerifiedSources(trust, groupId, recoveryIdentity, true, now, sources, lastKnownHead,
            (report, opened) =>
            {
                var preview = BuildDisasterCandidatePreview(trust, report, opened, selectedSourceId, lastKnownHead);
                if (!preview.CanPrepare || preview.ApprovalSha256() != approvedPreviewSha256 ||
                    preview.Inventory.CurrentPolicy != checkpoint)
                    throw new CryptographicException("Rekonstruktion ist nicht freigegeben oder entspricht nicht der bestätigten Vorschau.");
                var selected = opened.Single(o => o.Inventory.SourceId == selectedSourceId).Payload;
                var proposed = selected with { Catalog = selected.Catalog with
                    { Revision = preview.ProposedCatalogRevision!.Value } };
                KeyLibraryVault.Validate(proposed);
                foreach (var source in opened)
                    KeyLibraryVaultStore.ValidateImmutableHistory(source.Payload, proposed);
                var bytes = administration.ProtectScopedCatalog(trust, groupId, proposed, now);
                var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(bytes, GroupVaultService.MaximumBytes);
                var enrolled = trust.ExportPublic();
                var recovered = ScopedKeyLibraryVault.OpenVerified(signed, enrolled, groupId,
                    recoveryIdentity, now, requireFull: true, recovery: true);
                var personal = ScopedKeyLibraryVault.OpenVerified(signed, enrolled, groupId,
                    fullMember, now, requireFull: true);
                if (!KeyLibraryAutomaticSync.LogicallyEquivalent(proposed, recovered) ||
                    !KeyLibraryAutomaticSync.LogicallyEquivalent(proposed, personal) ||
                    administration.RequireLocalAuthority(trust) != checkpoint)
                    throw new CryptographicException("Isolierter Kandidat entspricht nicht der geprüften vollständigen Historie.");
                return new KeyLibraryIsolatedDisasterCandidate(preview, approvedPreviewSha256,
                    KeyLibrarySyncTransport.CiphertextSha256(bytes), bytes, enrolled);
            });
    }

    private static KeyLibraryDisasterCandidatePreview BuildDisasterCandidatePreview(
        OfflineGroupTrustStore trust, KeyLibraryDisasterReport report, List<Opened> opened,
        Guid selectedSourceId, CentralKeyLibraryHead? lastKnownHead)
    {
        var selected = opened.SingleOrDefault(o => o.Inventory.SourceId == selectedSourceId)
            ?? throw new InvalidDataException("Ausgewählte Wiederherstellungsquelle fehlt.");
        var blocks = new HashSet<KeyLibraryDisasterCandidateBlock>();
        if (opened.Any(o => !o.Inventory.CurrentPolicy))
            blocks.Add(KeyLibraryDisasterCandidateBlock.HistoricalPolicy);
        if (lastKnownHead is not null && !opened.Any(o =>
            o.Inventory.CentralRelation == KeyLibraryDisasterCentralRelation.ExactKnownCiphertext))
            blocks.Add(KeyLibraryDisasterCandidateBlock.MissingKnownCentralSnapshot);
        var selectedCustomers = selected.Payload.Catalog.Customers.ToDictionary(c => c.CustomerId);
        var selectedKeys = KeyLibraryContracts.Validate(selected.Payload.Catalog);
        foreach (var source in opened)
        {
            if (!Preserves(source.Payload, selected.Payload))
                blocks.Add(KeyLibraryDisasterCandidateBlock.MissingOrConflictingHistory);
            if (source.Payload.Catalog.Customers.Any(c =>
                    selectedCustomers.TryGetValue(c.CustomerId, out var other) && other != c) ||
                source.Payload.Catalog.Keys.Any(k =>
                    selectedKeys.TryGetValue(new(k.KeyId, k.Version), out var other) && other != k))
                blocks.Add(KeyLibraryDisasterCandidateBlock.MetadataOrStatusConflict);
        }
        var maximum = Math.Max(lastKnownHead?.CatalogRevision ?? 0,
            opened.Max(o => o.Inventory.CatalogRevision));
        if (maximum == long.MaxValue) blocks.Add(KeyLibraryDisasterCandidateBlock.RevisionExhausted);
        return new(report, selectedSourceId,
            OfflineGroupTrustStore.AdministratorFingerprint(trust.ExportPublic().AdministratorPublicKey),
            lastKnownHead, blocks.OrderBy(b => b).ToArray(),
            blocks.Count == 0 ? checked(maximum + 1) : null);
    }
}

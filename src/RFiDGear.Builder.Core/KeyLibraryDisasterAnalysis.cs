using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

// The historical checkpoint is supplied independently, not extracted from
// the candidate. Paths and certificate objects never appear in the report.
public sealed record KeyLibraryDisasterSource(Guid SourceId, string VaultPath,
    GroupEnrollmentPackage Enrollment, GroupPolicyCheckpoint ConfirmedPolicy);

public enum KeyLibraryDisasterCentralRelation
{
    Unknown, ExactKnownCiphertext, OlderCatalog, NewerCatalogUnconfirmed,
    SameCatalogDifferentCiphertext, DifferentPolicy
}

public sealed record KeyLibraryDisasterInventory(Guid SourceId, long CatalogRevision,
    GroupPolicyCheckpoint Policy, bool CurrentPolicy, string CiphertextSha256,
    int CustomerCount, int KeyVersionCount, KeyLibraryDisasterCentralRelation CentralRelation);

public sealed record KeyLibraryDisasterComparison(Guid LeftSourceId, Guid RightSourceId,
    int CustomersOnlyLeft, int CustomersOnlyRight, int CustomerMetadataDifferences,
    int VersionsOnlyLeft, int VersionsOnlyRight, int ImmutableIdentityConflicts,
    int SecretValueConflicts, int KeyMetadataDifferences, int StatusDifferences,
    bool LeftPreservesRightHistory, bool RightPreservesLeftHistory,
    bool SameLogicalCatalog);

public sealed record KeyLibraryDisasterReport(Guid LibraryId, string GroupId,
    GroupPolicyCheckpoint CurrentPolicy, KeyLibraryDisasterInventory[] Sources,
    KeyLibraryDisasterComparison[] Comparisons);

/// <summary>Read-only disaster inventory. Full currently authorized personal
/// or recovery identity is mandatory. No union of partial employee views,
/// no key export, no choice of winner, no new revision or server activation.</summary>
public static class KeyLibraryDisasterAnalysis
{
    private const int MaximumSources = 8;
    private sealed record Opened(KeyLibraryDisasterInventory Inventory, KeyLibraryVaultPayload Payload,
        string Path, FileRevision File);

    public static KeyLibraryDisasterReport Analyze(OfflineGroupTrustStore trust,
        string groupId, X509Certificate2 identity, bool recovery, DateTimeOffset now,
        IEnumerable<KeyLibraryDisasterSource> sources, CentralKeyLibraryHead? independentlyKnownHead = null) =>
        WithVerifiedSources(trust, groupId, identity, recovery, now, sources,
            independentlyKnownHead, (report, _) => report);

    /// <summary>Key-free preview of an additions-only union with an exact
    /// independently known common base. Does not persist or authorize changes.</summary>
    public static KeyLibraryMergePreview PreviewMerge(OfflineGroupTrustStore trust,
        string groupId, X509Certificate2 identity, bool recovery, DateTimeOffset now,
        IEnumerable<KeyLibraryDisasterSource> sources, CentralKeyLibraryHead independentlyKnownHead,
        Guid baseSourceId)
    {
        ArgumentNullException.ThrowIfNull(independentlyKnownHead);
        return WithVerifiedSources(trust, groupId, identity, recovery, now, sources,
            independentlyKnownHead, (report, opened) =>
            {
                if (opened.Count < 2 || opened.Any(s => !s.Inventory.CurrentPolicy) ||
                    opened.SingleOrDefault(s => s.Inventory.SourceId == baseSourceId)?.Inventory.CentralRelation
                        != KeyLibraryDisasterCentralRelation.ExactKnownCiphertext)
                    throw new CryptographicException("Zusammenführung erfordert einen exakt bestätigten gemeinsamen Ausgangsstand und aktuelle Richtlinien.");
                return KeyLibraryMergePlanner.Build(report, baseSourceId,
                    opened.ToDictionary(s => s.Inventory.SourceId, s => s.Payload)).Preview;
            });
    }

    private static T WithVerifiedSources<T>(OfflineGroupTrustStore trust,
        string groupId, X509Certificate2 identity, bool recovery, DateTimeOffset now,
        IEnumerable<KeyLibraryDisasterSource> sources, CentralKeyLibraryHead? independentlyKnownHead,
        Func<KeyLibraryDisasterReport, List<Opened>, T> project)
    {
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(sources);
        var pinned = trust.ExportPublic();
        var current = GroupEnrollmentPackages.Validate(pinned);
        if (!current.Policy.CustomerScopesEnabled ||
            !current.Policy.Groups.Any(g => g.Id == groupId))
            throw new CryptographicException("Bestätigte Kundenbibliothek erforderlich.");
        if (recovery)
        {
            var fingerprint = CertificateVaultService.Fingerprint(identity);
            if (!identity.HasPrivateKey || now.UtcDateTime < identity.NotBefore.ToUniversalTime() ||
                now.UtcDateTime >= identity.NotAfter.ToUniversalTime() ||
                !current.Policy.RecoveryDevices.Any(d => d.Enabled && d.Fingerprint == fingerprint &&
                    d.Certificate == Convert.ToBase64String(identity.RawData)))
                throw new CryptographicException("Aktuell bestätigter Recovery-Zugang erforderlich.");
        }
        else
        {
            var member = KeyLibraryDisclosure.RequireMember(current, groupId, identity, now);
            if (!current.Policy.People.Single(p => p.Id == member.PersonId).AllKeyLibraryCustomers)
                throw new CryptographicException("Vollständige aktuelle Kundenfreigabe erforderlich.");
        }

        if (independentlyKnownHead is not null &&
            (independentlyKnownHead.LibraryId != pinned.LibraryId ||
             independentlyKnownHead.GroupId != groupId || independentlyKnownHead.CentralRevision <= 0 ||
             independentlyKnownHead.CatalogRevision <= 0 ||
             independentlyKnownHead.Policy is null ||
             independentlyKnownHead.Policy.Revision > current.Checkpoint.Revision ||
             independentlyKnownHead.Policy.Revision == current.Checkpoint.Revision &&
                independentlyKnownHead.Policy != current.Checkpoint))
            throw new CryptographicException("Unabhängiger Zentralstand passt nicht zur bestätigten Bibliothek.");
        var candidates = sources.Take(MaximumSources + 1).ToArray();
        if (candidates.Length is 0 or > MaximumSources || candidates.Any(s => s is null || s.SourceId == Guid.Empty) ||
            candidates.Select(s => s.SourceId).Distinct().Count() != candidates.Length)
            throw new InvalidDataException("Eindeutige Wiederherstellungsquellen erforderlich (maximal acht).");

        var opened = new List<Opened>();
        foreach (var source in candidates)
        {
            var enrollment = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(source.Enrollment));
            var policy = GroupEnrollmentPackages.Validate(enrollment);
            if (enrollment.LibraryId != pinned.LibraryId ||
                !enrollment.AdministratorPublicKey.SequenceEqual(pinned.AdministratorPublicKey) ||
                !policy.Policy.CustomerScopesEnabled || policy.Checkpoint != source.ConfirmedPolicy ||
                policy.Checkpoint.Revision > current.Checkpoint.Revision ||
                policy.Checkpoint.Revision == current.Checkpoint.Revision && policy.Checkpoint != current.Checkpoint)
                throw new CryptographicException("Quellrichtlinie ist nicht unabhängig bestätigt oder gehört zu einer anderen Verwaltung.");
            var path = Path.GetFullPath(source.VaultPath);
            var bytes = KeyLibraryVaultStore.ReadBytes(path);
            var hash = KeyLibrarySyncTransport.CiphertextSha256(bytes);
            var file = new FileRevision(true, hash);
            var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(bytes, GroupVaultService.MaximumBytes);
            // Authenticate every complete candidate under its own pinned policy.
            // The same identity must have full access in each historical epoch.
            var payload = ScopedKeyLibraryVault.OpenVerified(signed, enrollment, groupId, identity, now,
                requireFull: true, recovery: recovery);
            if (ConcurrentFileWriter.Capture(path) != file)
                throw new ConcurrentFileChangeException(path);
            var relation = Relation(independentlyKnownHead, policy.Checkpoint, payload.Catalog.Revision, hash);
            opened.Add(new(new(source.SourceId, payload.Catalog.Revision, policy.Checkpoint,
                policy.Checkpoint == current.Checkpoint, hash, payload.Catalog.Customers.Length,
                payload.Catalog.Keys.Length, relation), payload, path, file));
        }

        var comparisons = new List<KeyLibraryDisasterComparison>();
        for (var i = 0; i < opened.Count; i++)
            for (var j = i + 1; j < opened.Count; j++)
                comparisons.Add(Compare(opened[i], opened[j]));
        var result = project(new(pinned.LibraryId, groupId, current.Checkpoint,
            opened.Select(s => s.Inventory).ToArray(), comparisons.ToArray()), opened);
        foreach (var candidate in opened)
            if (ConcurrentFileWriter.Capture(candidate.Path) != candidate.File)
                throw new ConcurrentFileChangeException(candidate.Path);
        if (trust.LoadOffline().Checkpoint != current.Checkpoint ||
            !trust.ExportPublic().AdministratorPublicKey.SequenceEqual(pinned.AdministratorPublicKey))
            throw new CryptographicException("Vertrauensstand wurde während der Bestandsanalyse geändert.");
        return result;
    }

    private static KeyLibraryDisasterCentralRelation Relation(CentralKeyLibraryHead? head,
        GroupPolicyCheckpoint policy, long revision, string hash)
    {
        if (head is null) return KeyLibraryDisasterCentralRelation.Unknown;
        if (head.Policy != policy) return KeyLibraryDisasterCentralRelation.DifferentPolicy;
        if (revision < head.CatalogRevision) return KeyLibraryDisasterCentralRelation.OlderCatalog;
        if (revision > head.CatalogRevision) return KeyLibraryDisasterCentralRelation.NewerCatalogUnconfirmed;
        return hash == head.CiphertextSha256 ? KeyLibraryDisasterCentralRelation.ExactKnownCiphertext :
            KeyLibraryDisasterCentralRelation.SameCatalogDifferentCiphertext;
    }

    private static KeyLibraryDisasterComparison Compare(Opened left, Opened right)
    {
        var l = left.Payload;
        var r = right.Payload;
        var lc = l.Catalog.Customers.ToDictionary(c => c.CustomerId);
        var rc = r.Catalog.Customers.ToDictionary(c => c.CustomerId);
        var lk = KeyLibraryContracts.Validate(l.Catalog);
        var rk = KeyLibraryContracts.Validate(r.Catalog);
        var ls = l.Secrets.ToDictionary(s => s.Key, s => s.HexValue);
        var rs = r.Secrets.ToDictionary(s => s.Key, s => s.HexValue);
        var li = l.Catalog.Keys.GroupBy(k => k.KeyId).ToDictionary(g => g.Key,
            g => (g.First().CustomerId, g.First().KeyType));
        var ri = r.Catalog.Keys.GroupBy(k => k.KeyId).ToDictionary(g => g.Key,
            g => (g.First().CustomerId, g.First().KeyType));
        var common = lk.Keys.Intersect(rk.Keys).ToArray();
        var identities = li.Keys.Intersect(ri.Keys).Count(id => li[id] != ri[id]);
        var secrets = common.Count(k => !string.Equals(ls[k], rs[k], StringComparison.OrdinalIgnoreCase));
        var status = common.Count(k => lk[k].Status != rk[k].Status);
        var metadata = common.Count(k => (lk[k].Name, lk[k].Purpose, lk[k].HostSystem) !=
            (rk[k].Name, rk[k].Purpose, rk[k].HostSystem));
        var customerMetadata = lc.Keys.Intersect(rc.Keys).Count(id => lc[id] != rc[id]);
        var onlyLC = lc.Keys.Except(rc.Keys).Count();
        var onlyRC = rc.Keys.Except(lc.Keys).Count();
        var onlyLK = lk.Keys.Except(rk.Keys).Count();
        var onlyRK = rk.Keys.Except(lk.Keys).Count();
        return new(left.Inventory.SourceId, right.Inventory.SourceId, onlyLC, onlyRC,
            customerMetadata, onlyLK, onlyRK, identities, secrets, metadata, status,
            Preserves(r, l), Preserves(l, r),
            onlyLC == 0 && onlyRC == 0 && onlyLK == 0 && onlyRK == 0 && identities == 0 &&
            secrets == 0 && status == 0 && metadata == 0 && customerMetadata == 0);
    }

    // A history-compatible superset is NOT proof of ancestry, central acceptance
    // or a safe automatic merge; mutable metadata may have diverged.
    private static bool Preserves(KeyLibraryVaultPayload previous, KeyLibraryVaultPayload next)
    {
        try
        {
            KeyLibraryVaultStore.ValidateImmutableHistory(previous, next);
            return true;
        }
        catch (InvalidDataException) { return false; }
    }
}

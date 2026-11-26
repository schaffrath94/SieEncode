using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>One protected, durable merge intent. Use under the same exclusive
/// Studio administration/workspace lease as normal editing and policy changes.
/// No auto-upload integration yet. A failed operation retains its evidence.</summary>
public sealed class KeyLibraryMergeJournal(string journalPath, Guid libraryId,
    string localVaultPath, ILocalGroupTrustProtection protection)
{
    private const string Format = "SieEncode-KeyLibrary-Merge-Journal";
    private const int MaximumProtectedBytes = KeyLibrarySyncWire.MaximumEncryptedBytes + 1024 * 1024;
    private readonly string path = Path.GetFullPath(journalPath);
    private readonly string vault = Path.GetFullPath(localVaultPath);
    private sealed record Pending(KeyLibraryPreparedMerge Merge, FileRevision Local,
        string AdministratorFingerprint, string DeviceFingerprint, KeyLibrarySyncEndpoint? Endpoint);
    private sealed record State(string Format, int Version, Guid LibraryId, string VaultPath,
        Pending? Intent, CentralKeyLibraryHead? Completed, string PreviewSha256);

    public bool HasPending => Read().State?.Intent is not null;
    public CentralKeyLibraryHead? CompletedHead => Read().State?.Completed;
    public CentralKeyLibraryHead? PendingExpected => Read().State?.Intent?.Merge.ExpectedHead;
    public string? PendingDeviceFingerprint => Read().State?.Intent?.DeviceFingerprint;

    // Prepare inside Stage, rather than accepting an arbitrary supplied transfer
    // whose provenance is not independently bound to the approved preview.
    public void Stage(OfflineGroupTrustStore trust, OfflineGroupAdministrationStore administration,
        CentralKeyLibraryCursorStore cursor, KeyLibraryPolicyTransitionJournal policyJournal,
        string groupId, X509Certificate2 fullMember, DateTimeOffset now,
        IEnumerable<KeyLibraryDisasterSource> sources, CentralKeyLibraryHead expected,
        Guid baseSourceId, string approvedPreviewSha256, bool explicitlyApproved,
        KeyLibrarySyncEndpoint? endpoint = null, IEnumerable<KeyLibraryMergeDecision>? decisions = null,
        CentralKeyLibraryHead? historicalBase = null)
    {
        if (policyJournal.HasPending) throw new InvalidOperationException("Richtlinienauftrag ist noch offen.");
        var (old, revision) = Read();
        if (old?.Intent is not null) throw new InvalidOperationException("Zusammenführung ist bereits vorbereitet.");
        var local = ConcurrentFileWriter.Capture(vault);
        if (!local.Exists) throw new FileNotFoundException("Lokale Bibliothek fehlt.", vault);
        var merge = KeyLibraryDisasterAnalysis.PrepareApprovedMerge(trust, administration,
            groupId, fullMember, now, sources, expected, baseSourceId, approvedPreviewSha256, explicitlyApproved, decisions, historicalBase);
        var root = OfflineGroupTrustStore.AdministratorFingerprint(trust.ExportPublic().AdministratorPublicKey);
        if (endpoint is not null && (endpoint.LibraryId != libraryId || KeyLibrarySyncEndpointStore.Validate(endpoint) != endpoint))
            throw new CryptographicException("Zentralverbindung passt nicht zur Zusammenführung.");
        var pending = new Pending(merge, local, root, CertificateVaultService.Fingerprint(fullMember), endpoint);
        var target = Validate(pending, trust, administration, fullMember);
        cursor.RequireMergeBase(expected, target, allowCompleted: false, historicalBase: merge.Preview.HistoricalBase);
        RequireLocal(pending, target, trust, fullMember, allowTarget: false);
        if (policyJournal.HasPending) throw new InvalidOperationException("Richtlinienauftrag ist noch offen.");
        Save(new(Format, 1, libraryId, vault, pending, null, merge.PreviewSha256), revision);
    }

    public async Task<CentralKeyLibraryHead> ResumeAsync(IKeyLibrarySyncTransport transport,
        OfflineGroupTrustStore trust, OfflineGroupAdministrationStore administration,
        CentralKeyLibraryCursorStore cursor, KeyLibraryPolicyTransitionJournal policyJournal,
        X509Certificate2 fullMember, CancellationToken cancellationToken = default,
        KeyLibrarySyncEndpoint? endpoint = null)
    {
        var (state, revision) = Read();
        var pending = state?.Intent ?? throw new InvalidOperationException("Kein offener Zusammenführungsauftrag.");
        if (pending.Endpoint != endpoint)
            throw new CryptographicException("Bestätigte Zentralverbindung wurde seit der Zusammenführung geändert.");
        var expected = pending.Merge.ExpectedHead;
        var target = Validate(pending, trust, administration, fullMember);
        void Check()
        {
            if (ConcurrentFileWriter.Capture(path) != revision)
                throw new ConcurrentFileChangeException(path);
            if (policyJournal.HasPending)
                throw new InvalidOperationException("Richtlinienauftrag ist noch offen.");
            if (administration.RequireLocalAuthority(trust) != expected.Policy)
                throw new CryptographicException("Verwaltung hat sich seit der Freigabe geändert.");
        }

        Check();
        cursor.RequireMergeBase(expected, target, allowCompleted: true, historicalBase: pending.Merge.Preview.HistoricalBase);
        var local = RequireLocal(pending, target, trust, fullMember, allowTarget: true);
        var remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (remote == expected)
        {
            if (local != pending.Local) throw new CryptographicException("Zentrale ist auf den Ausgangsstand zurückgefallen.");
            cursor.RequireMergeBase(expected, target, allowCompleted: false, historicalBase: pending.Merge.Preview.HistoricalBase);
            Check();
            RequireLocal(pending, target, trust, fullMember, allowTarget: false);
            cancellationToken.ThrowIfCancellationRequested();
            // The signed transfer is frozen. A lost reply leaves the intent;
            // subsequent calls read the head before deciding whether to submit.
            var accepted = await transport.UploadAsync(KeyLibrarySyncTransport.Capture(pending.Merge.Transfer),
                cancellationToken).ConfigureAwait(false);
            if (accepted != target) throw new CryptographicException("Zentrale hat die Zusammenführung nicht exakt bestätigt.");
            remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        }
        if (remote != target) throw new ConcurrentFileChangeException("Zentrale Zusammenführung");
        cancellationToken.ThrowIfCancellationRequested();
        Check();
        cursor.RequireMergeBase(expected, target, allowCompleted: true, historicalBase: pending.Merge.Preview.HistoricalBase);
        local = RequireLocal(pending, target, trust, fullMember, allowTarget: true);
        // Remember the accepted head before installing bytes; never permit a
        // later retry against an older remote head after this observation.
        cursor.Remember(target);
        if (local.Sha256 != target.CiphertextSha256)
        {
            Check();
            local = KeyLibrarySyncClient.InstallDownloadedSnapshot(vault, trust,
                expected.GroupId, target, pending.Merge.Transfer, [fullMember], local);
        }
        Check();
        if (RequireLocal(pending, target, trust, fullMember, allowTarget: true) != local ||
            local.Sha256 != target.CiphertextSha256)
            throw new ConcurrentFileChangeException(vault);
        cursor.RememberMerged(expected, target, local, pending.Merge.Preview.HistoricalBase);
        Save(new(Format, 1, libraryId, vault, null, target, pending.Merge.PreviewSha256), revision);
        return target;
    }

    private CentralKeyLibraryHead Validate(Pending pending, OfflineGroupTrustStore trust,
        OfflineGroupAdministrationStore administration, X509Certificate2 identity)
    {
        var merge = pending.Merge;
        var expected = merge.ExpectedHead;
        var enrolled = trust.ExportPublic();
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        var access = KeyLibraryDisclosure.RequireMember(policy, expected.GroupId, identity, DateTimeOffset.UtcNow);
        if (expected.LibraryId != libraryId || libraryId == Guid.Empty || expected.CentralRevision <= 0 ||
            !policy.Policy.CustomerScopesEnabled || policy.Checkpoint != expected.Policy ||
            administration.RequireLocalAuthority(trust) != expected.Policy ||
            OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey) != pending.AdministratorFingerprint ||
            access.DeviceFingerprint != pending.DeviceFingerprint ||
            !policy.Policy.People.Single(p => p.Id == access.PersonId).AllKeyLibraryCustomers ||
            !merge.Preview.CanPrepareCandidate || merge.Preview.ApprovalSha256(expected) != merge.PreviewSha256 ||
            merge.Preview.Inventory.LibraryId != libraryId ||
            merge.Preview.Inventory.GroupId != expected.GroupId ||
            merge.Preview.Inventory.CurrentPolicy != expected.Policy ||
            !pending.Local.Exists ||
            !merge.Preview.Inventory.Sources.Any(s => s.CurrentPolicy && s.CiphertextSha256 == pending.Local.Sha256))
            throw new CryptographicException("Zusammenführungsauftrag passt nicht zum bestätigten lokalen Bestand oder zur Verwaltung.");
        var baseline = merge.Preview.HistoricalBase ?? expected;
        var baseInventory = merge.Preview.Inventory.Sources.SingleOrDefault(s => s.SourceId == merge.Preview.BaseSourceId);
        if (baseline.LibraryId != libraryId || baseline.GroupId != expected.GroupId || baseline.Policy != expected.Policy ||
            baseline.CentralRevision <= 0 || baseline.CatalogRevision <= 0 ||
            merge.Preview.HistoricalBase is not null && (baseline.CentralRevision >= expected.CentralRevision ||
                baseline.CatalogRevision >= expected.CatalogRevision) ||
            baseInventory is null || !baseInventory.CurrentPolicy ||
            baseInventory.CatalogRevision != baseline.CatalogRevision ||
            baseInventory.CiphertextSha256 != baseline.CiphertextSha256 ||
            !merge.Preview.Inventory.Sources.Any(s => s.CurrentPolicy &&
                s.CatalogRevision == expected.CatalogRevision && s.CiphertextSha256 == expected.CiphertextSha256))
            throw new CryptographicException("Zusammenführungsauftrag enthält keine exakte gemeinsame Basis und keinen aktuellen Zentralstand.");
        var claim = KeyLibrarySyncTransport.Verify(merge.Transfer, enrolled);
        var next = KeyLibraryVaultStore.OpenEncrypted(merge.Transfer.EncryptedVault, trust,
            expected.GroupId, [identity], requireFull: true);
        if (claim.ExpectedCentralRevision != expected.CentralRevision ||
            claim.ExpectedCentralCiphertextSha256 != expected.CiphertextSha256 ||
            claim.GroupId != expected.GroupId || claim.Policy != expected.Policy ||
            claim.LibraryId != libraryId || claim.CatalogRevision != merge.Preview.ProposedRevision ||
            claim.CatalogRevision <= expected.CatalogRevision || next.Catalog.Revision != claim.CatalogRevision)
            throw new CryptographicException("Signierter Zusammenführungsauftrag besitzt eine falsche Basis.");
        return new(libraryId, checked(expected.CentralRevision + 1), claim.NextCiphertextSha256,
            expected.GroupId, expected.Policy, claim.CatalogRevision);
    }

    private FileRevision RequireLocal(Pending pending, CentralKeyLibraryHead target,
        OfflineGroupTrustStore trust, X509Certificate2 identity, bool allowTarget)
    {
        var local = ConcurrentFileWriter.Capture(vault);
        if (!local.Exists || local != pending.Local && (!allowTarget || local.Sha256 != target.CiphertextSha256))
            throw new ConcurrentFileChangeException(vault);
        var current = KeyLibraryVaultStore.Load(vault, trust, target.GroupId, [identity], requireFull: true);
        if (local.Sha256 != target.CiphertextSha256)
        {
            var next = KeyLibraryVaultStore.OpenEncrypted(pending.Merge.Transfer.EncryptedVault,
                trust, target.GroupId, [identity], requireFull: true);
            KeyLibraryVaultStore.ValidateImmutableHistory(current, next);
            if (next.Catalog.Revision <= current.Catalog.Revision)
                throw new InvalidDataException("Zusammenführung muss neuer als die lokale Bibliothek sein.");
        }
        else if (current.Catalog.Revision != target.CatalogRevision)
            throw new CryptographicException("Lokale Zielrevision stimmt nicht überein.");
        if (ConcurrentFileWriter.Capture(vault) != local || trust.LoadOffline().Checkpoint != target.Policy)
            throw new ConcurrentFileChangeException(vault);
        return local;
    }

    private (State? State, FileRevision Revision) Read()
    {
        if (string.Equals(path, vault, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Zusammenführungsjournal darf nicht der Vault sein.");
        if (!File.Exists(path)) return (null, FileRevision.Missing);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > MaximumProtectedBytes)
            throw new InvalidDataException("Geschütztes Zusammenführungsjournal ist ungültig.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        var revision = new FileRevision(true, Convert.ToHexString(SHA256.HashData(bytes)));
        var raw = protection.Unprotect(bytes);
        try
        {
            var state = KeyLibrarySyncWire.Read<State>(raw, KeyLibrarySyncWire.MaximumEncryptedBytes);
            if (state.Format != Format || state.Version != 1 || state.LibraryId != libraryId ||
                !string.Equals(state.VaultPath, vault, StringComparison.OrdinalIgnoreCase) ||
                (state.Intent is null) == (state.Completed is null) ||
                state.Intent is not null && state.Intent.Merge.PreviewSha256 != state.PreviewSha256 ||
                state.Completed is not null && state.Completed.LibraryId != libraryId)
                throw new CryptographicException("Zusammenführungsjournal passt nicht zur lokalen Bibliothek.");
            return (state, revision);
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private void Save(State state, FileRevision expected)
    {
        var raw = KeyLibrarySyncWire.Write(state);
        try
        {
            if (raw.Length > KeyLibrarySyncWire.MaximumEncryptedBytes)
                throw new InvalidDataException("Zusammenführungsjournal ist zu groß.");
            var bytes = protection.Protect(raw);
            if (bytes.Length is <= 0 or > MaximumProtectedBytes)
                throw new InvalidDataException("Geschütztes Zusammenführungsjournal ist zu groß.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Write(bytes);
                file.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
}

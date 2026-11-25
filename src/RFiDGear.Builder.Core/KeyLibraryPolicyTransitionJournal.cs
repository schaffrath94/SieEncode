using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>Transport must authenticate the pinned central endpoint. The
/// interface also permits deterministic recovery tests without a live server.</summary>
public interface IKeyLibraryPolicyTransitionTransport
{
    Task<CentralKeyLibraryHead> ReadHeadAsync(CancellationToken cancellationToken = default);
    Task<CentralKeyLibraryHead> ChangePolicyAsync(SignedKeyLibraryPolicyTransition transition,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Durable, user-protected intent for one policy-only rewrap. Never stores
/// plaintext keys or a second copy of the previous encrypted catalog.
/// A completed intent is replaced by a small protected completion marker.
/// The caller must serialize ordinary library editing and group management
/// against this workflow; UI integration is a separate release gate.
/// </summary>
public sealed class KeyLibraryPolicyTransitionJournal(string journalPath, Guid libraryId,
    string localVaultPath, ILocalGroupTrustProtection protection)
{
    private const string Format = "SieEncode-KeyLibrary-Policy-Transition-Journal";
    private const int MaximumProtectedBytes = KeyLibrarySyncWire.MaximumEncryptedBytes + 1024 * 1024;
    private readonly string path = Path.GetFullPath(journalPath);
    private readonly string vaultPath = Path.GetFullPath(localVaultPath);

    private sealed record Pending(GroupEnrollmentPackage Previous,
        CentralKeyLibraryHead Expected, SignedKeyLibraryPolicyTransition Transition)
    {
        public CentralKeyLibraryHead? ReceivedHead { get; init; }
    }
    private sealed record State(string Format, int Version, Guid LibraryId, string VaultPath,
        Pending? Intent, CentralKeyLibraryHead? Completed);

    public bool HasPending => Read().State?.Intent is not null;
    public bool IsReceiveOnly => Read().State?.Intent?.ReceivedHead is not null;

    // Only public enrollment data is returned; private identity selection
    // must still prove access to both recipient sets.
    public GroupEnrollmentPackage? PendingEnrollment => Read().State?.Intent?.Transition.Enrollment;
    public GroupEnrollmentPackage? PendingPreviousEnrollment => Read().State?.Intent?.Previous;
    public CentralKeyLibraryHead? PendingExpected => Read().State?.Intent?.Expected;

    public void Stage(OfflineGroupTrustStore trust, CentralKeyLibraryCursorStore cursor,
        CentralKeyLibraryHead expected, SignedKeyLibraryPolicyTransition transition,
        X509Certificate2 retainedIdentity, bool confirmed)
    {
        if (!confirmed)
            throw new InvalidOperationException("Richtlinienwechsel muss ausdrücklich bestätigt werden.");
        var (old, revision) = Read();
        if (old?.Intent is not null)
            throw new InvalidOperationException("Ein vorbereiteter Richtlinienwechsel muss zuerst abgeschlossen werden.");
        var previous = trust.ExportPublic();
        // Freeze exactly the signed bytes that will later be submitted.
        var frozen = KeyLibrarySyncWire.Read<SignedKeyLibraryPolicyTransition>(
            KeyLibrarySyncWire.Write(transition), KeyLibrarySyncWire.MaximumEncryptedBytes);
        var pending = new Pending(previous, expected, frozen);
        var target = ExpectedResult(pending);
        cursor.RequirePolicyTransitionBase(expected, target);
        var local = ConcurrentFileWriter.Capture(vaultPath);
        if (!local.Exists || local.Sha256 != expected.CiphertextSha256 ||
            !cursor.HasUploadBase(expected) || !cursor.HasUnchangedLocalBase(local))
            throw new ConcurrentFileChangeException(vaultPath);
        var current = KeyLibraryVaultStore.Load(vaultPath, trust, expected.GroupId, [retainedIdentity]);
        var next = OpenTarget(pending, retainedIdentity);
        if (current.Catalog.Revision != expected.CatalogRevision ||
            !KeyLibraryAutomaticSync.LogicallyEquivalent(current, next) ||
            ConcurrentFileWriter.Capture(vaultPath) != local ||
            trust.LoadOffline().Checkpoint != expected.Policy)
            throw new CryptographicException("Lokaler und zentraler Katalog sind nicht exakt abgeglichen.");
        Save(new(Format, 1, libraryId, vaultPath, pending, null), revision);
    }

    /// <summary>Stage a signed snapshot already accepted by the central
    /// service. This journal can never send a policy change on this path.</summary>
    public void StageReceived(OfflineGroupTrustStore trust, CentralKeyLibraryCursorStore cursor,
        CentralKeyLibraryHead baseline, CentralKeyLibrarySnapshot snapshot,
        X509Certificate2 personalIdentity)
    {
        var (old, revision) = Read();
        if (old?.Intent is not null)
            throw new InvalidOperationException("Ein ausstehender Richtlinienwechsel muss zuerst abgeschlossen werden.");
        var previous = trust.ExportPublic();
        var frozen = KeyLibrarySyncWire.Read<CentralKeyLibrarySnapshot>(
            KeyLibrarySyncWire.Write(snapshot), KeyLibrarySyncWire.MaximumEncryptedBytes);
        var pending = new Pending(previous, baseline, new(frozen.Enrollment, frozen.Transfer))
            { ReceivedHead = frozen.Head };
        var target = ExpectedResult(pending);
        cursor.RequirePolicyTransitionBase(baseline, target);
        var local = ConcurrentFileWriter.Capture(vaultPath);
        if (!local.Exists || local.Sha256 != baseline.CiphertextSha256 ||
            !cursor.HasUploadBase(baseline) || !cursor.HasUnchangedLocalBase(local))
            throw new ConcurrentFileChangeException(vaultPath);
        var current = KeyLibraryVaultStore.Load(vaultPath, trust, baseline.GroupId, [personalIdentity]);
        var next = OpenTarget(pending, personalIdentity);
        if (current.Catalog.Revision != baseline.CatalogRevision ||
            ConcurrentFileWriter.Capture(vaultPath) != local ||
            trust.LoadOffline().Checkpoint != baseline.Policy)
            throw new CryptographicException("Lokaler Bestand ist nicht exakt abgeglichen.");
        KeyLibraryVaultStore.ValidateImmutableHistory(current, next);
        if (next.Catalog.Revision == current.Catalog.Revision &&
            !KeyLibraryAutomaticSync.LogicallyEquivalent(current, next))
            throw new CryptographicException("Empfangener Richtlinienwechsel verändert den Katalog ohne neue Revision.");
        Save(new(Format, 1, libraryId, vaultPath, pending, null), revision);
    }

    /// <summary>
    /// First inspect the remote head. Submit only while it is exactly the
    /// original base; after an ambiguous network failure, resume from the
    /// persisted intent instead of creating another encrypted generation.
    /// </summary>
    public async Task<CentralKeyLibraryHead> ResumeAsync(
        IKeyLibraryPolicyTransitionTransport transport, OfflineGroupTrustStore trust,
        CentralKeyLibraryCursorStore cursor, X509Certificate2 retainedIdentity,
        CancellationToken cancellationToken = default,
        OfflineGroupAdministrationStore? administration = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var (state, revision) = Read();
        var pending = state?.Intent ??
            throw new InvalidOperationException("Kein vorbereiteter Richtlinienwechsel vorhanden.");
        var target = ExpectedResult(pending);
        if (administration is not null)
        {
            var own = administration.ExportPublic();
            var ownPolicy = GroupEnrollmentPackages.Validate(own).Checkpoint;
            if (!own.AdministratorPublicKey.SequenceEqual(pending.Previous.AdministratorPublicKey) ||
                (ownPolicy != pending.Expected.Policy && ownPolicy != target.Policy))
                throw new CryptographicException("Lokale Hauptverwaltung passt nicht zum vorbereiteten Wechsel.");
        }
        _ = OpenTarget(pending, retainedIdentity);
        cursor.RequirePolicyTransitionBase(pending.Expected, target);
        var local = RequireLocalState(pending, target, trust);
        var remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (remote == pending.Expected && pending.ReceivedHead is null)
        {
            // A partial local installation is only legal AFTER the remote commit.
            if (local.Sha256 != pending.Expected.CiphertextSha256 ||
                trust.LoadOffline().Checkpoint != pending.Expected.Policy ||
                !cursor.HasUploadBase(pending.Expected))
                throw new CryptographicException("Zentrale ist auf den vorherigen Stand zurückgefallen.");
            RequireUnchangedJournal(revision);
            _ = RequireLocalState(pending, target, trust);
            var accepted = await transport.ChangePolicyAsync(pending.Transition, cancellationToken)
                .ConfigureAwait(false);
            if (accepted != target)
                throw new CryptographicException("Zentrale hat nicht den exakt vorbereiteten Richtlinienwechsel bestätigt.");
            remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        }
        if (remote != target)
            throw new ConcurrentFileChangeException("Zentraler Richtlinienwechsel");
        cancellationToken.ThrowIfCancellationRequested();
        RequireUnchangedJournal(revision);
        cursor.RequirePolicyTransitionBase(pending.Expected, target);
        local = RequireLocalState(pending, target, trust);

        // The intent is durable throughout these separate atomic writes. A
        // restart can recognize the exact old/new trust and ciphertext states.
        // Never overwrite any third (concurrently edited) local ciphertext.
        if (trust.LoadOffline().Checkpoint == pending.Expected.Policy)
            trust.AcceptPackageUpdate(pending.Transition.Enrollment, pending.Expected.Policy);
        if (local.Sha256 != target.CiphertextSha256)
        {
            var installed = ConcurrentFileWriter.Write(vaultPath, local, temporary =>
            {
                RequireUnchangedJournal(revision);
                if (trust.LoadOffline().Checkpoint != target.Policy)
                    throw new CryptographicException("Lokale Richtlinie hat sich während der Übernahme geändert.");
                using var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Write(pending.Transition.Rewrapped.EncryptedVault);
                file.Flush(true);
                if (trust.LoadOffline().Checkpoint != target.Policy)
                    throw new CryptographicException("Lokale Richtlinie hat sich während der Übernahme geändert.");
            });
            local = installed;
        }
        var verified = RequireLocalState(pending, target, trust);
        if (verified != local || verified.Sha256 != target.CiphertextSha256 ||
            trust.LoadOffline().Checkpoint != target.Policy)
            throw new CryptographicException("Lokaler Vault und Richtlinie wurden nicht vollständig übernommen.");
        administration?.AcceptCommittedPolicyTransition(pending.Previous,
            pending.Transition.Enrollment, trust);
        if (pending.ReceivedHead is not null)
            cursor.RememberReceivedPolicy(pending.Previous, pending.Expected,
                new(target, pending.Transition.Enrollment, pending.Transition.Rewrapped), verified);
        else
            cursor.RememberPolicyTransition(pending.Previous, pending.Expected,
                pending.Transition, target, verified);
        // Drop the bulky intent only after all three local stores agree.
        Save(new(Format, 1, libraryId, vaultPath, null, target), revision);
        return target;
    }

    private CentralKeyLibraryHead ExpectedResult(Pending pending)
    {
        if (pending.Expected.LibraryId != libraryId || libraryId == Guid.Empty)
            throw new CryptographicException("Richtlinienwechsel gehört zu einer anderen Bibliothek.");
        if (pending.ReceivedHead is { } received)
        {
            _ = KeyLibraryPolicyTransition.VerifyReceivedSnapshot(pending.Previous,
                pending.Expected, new(received, pending.Transition.Enrollment, pending.Transition.Rewrapped));
            return received;
        }
        var claim = KeyLibraryPolicyTransition.Verify(pending.Transition, pending.Previous, pending.Expected);
        return new(libraryId, checked(pending.Expected.CentralRevision + 1),
            claim.NextCiphertextSha256, pending.Expected.GroupId, claim.Policy, pending.Expected.CatalogRevision);
    }

    private static KeyLibraryVaultPayload OpenTarget(Pending pending, X509Certificate2 identity)
    {
        var payload = KeyLibraryVault.Unprotect(GroupVaultService.Read(pending.Transition.Rewrapped.EncryptedVault),
            GroupEnrollmentPackages.Validate(pending.Transition.Enrollment), pending.Expected.GroupId, [identity]);
        if (payload.Catalog.Revision != (pending.ReceivedHead ?? pending.Expected).CatalogRevision)
            throw new CryptographicException("Neu verschlüsselter Katalog besitzt eine falsche Revision.");
        return payload;
    }

    private FileRevision RequireLocalState(Pending pending, CentralKeyLibraryHead target, OfflineGroupTrustStore trust)
    {
        var enrollment = trust.ExportPublic();
        var policy = GroupEnrollmentPackages.Validate(enrollment).Checkpoint;
        if (!enrollment.AdministratorPublicKey.SequenceEqual(pending.Previous.AdministratorPublicKey) ||
            (policy != pending.Expected.Policy && policy != target.Policy))
            throw new CryptographicException("Lokale Vertrauenswurzel oder Richtlinie passt nicht zum vorbereiteten Wechsel.");
        var local = ConcurrentFileWriter.Capture(vaultPath);
        if (!local.Exists ||
            (local.Sha256 != pending.Expected.CiphertextSha256 && local.Sha256 != target.CiphertextSha256) ||
            (policy == pending.Expected.Policy && local.Sha256 == target.CiphertextSha256))
            throw new ConcurrentFileChangeException(vaultPath);
        return local;
    }

    private void RequireUnchangedJournal(FileRevision revision)
    {
        if (ConcurrentFileWriter.Capture(path) != revision)
            throw new ConcurrentFileChangeException(path);
    }

    private (State? State, FileRevision Revision) Read()
    {
        if (string.Equals(path, vaultPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Richtlinienjournal darf nicht der Bibliotheks-Vault sein.");
        if (!File.Exists(path)) return (null, FileRevision.Missing);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > MaximumProtectedBytes)
            throw new InvalidDataException("Geschütztes Richtlinienjournal ist ungültig.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        var revision = new FileRevision(true, Convert.ToHexString(SHA256.HashData(bytes)));
        var raw = protection.Unprotect(bytes);
        try
        {
            var state = KeyLibrarySyncWire.Read<State>(raw, KeyLibrarySyncWire.MaximumEncryptedBytes);
            if (state.Format != Format || state.Version != 1 || state.LibraryId != libraryId ||
                !string.Equals(state.VaultPath, vaultPath, StringComparison.OrdinalIgnoreCase) ||
                (state.Intent is null) == (state.Completed is null))
                throw new CryptographicException("Richtlinienjournal passt nicht zur lokalen Bibliothek.");
            if (state.Intent is not null) _ = ExpectedResult(state.Intent);
            if (state.Completed is not null && state.Completed.LibraryId != libraryId)
                throw new CryptographicException("Abschlussmerker gehört zu einer anderen Bibliothek.");
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
                throw new InvalidDataException("Richtlinienjournal ist zu groß.");
            var bytes = protection.Protect(raw);
            if (bytes.Length is <= 0 or > MaximumProtectedBytes)
                throw new InvalidDataException("Geschütztes Richtlinienjournal ist zu groß.");
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

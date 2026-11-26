using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// User-profile-bound observation of the last accepted central revision.
/// Protects against accidental server backup rollback between sync runs.
/// Not a substitute for an independently anchored, server-signed receipt.
/// </summary>
public sealed class CentralKeyLibraryCursorStore(
    string filePath, Guid libraryId, ILocalGroupTrustProtection protection)
{
    private sealed record Cursor(string Format, int Version, Guid LibraryId,
        CentralKeyLibraryHead Head, DateTimeOffset ObservedAt)
    {
        public CentralKeyLibraryHead? ReconciledHead { get; init; }
        public string? ReconciledLocalSha256 { get; init; }
    }
    private const string Format = "SieEncode-Client-Sync-Cursor";
    private const int MaximumBytes = 8192;
    private readonly string path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static string DefaultPath(Guid libraryId)
    {
        if (libraryId == Guid.Empty) throw new InvalidDataException("Bibliotheks-ID fehlt.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            throw new InvalidOperationException("Geschütztes Windows-Benutzerverzeichnis fehlt.");
        return Path.Combine(local, "SieEncode", "KeyLibrary",
            libraryId.ToString("N"), "sync-cursor.bin");
    }

    public void RequireNotRolledBack(CentralKeyLibraryHead observed)
    {
        Validate(observed);
        var stored = Read().State;
        if (stored is not null) CheckTransition(stored.Head, observed);
    }

    // Observed central heads and successfully reconciled local snapshots are
    // deliberately separate. Observing a conflict must never establish a new base.
    public CentralKeyLibraryHead? ReadReconciledHead() => Read().State?.ReconciledHead;

    public bool HasUploadBase(CentralKeyLibraryHead remote)
    {
        RequireNotRolledBack(remote);
        return Read().State?.ReconciledHead == remote;
    }

    public bool HasUnchangedLocalBase(FileRevision local)
    {
        ArgumentNullException.ThrowIfNull(local);
        var state = Read().State;
        return local.Exists && state?.ReconciledHead is not null &&
            local.Sha256 == state.ReconciledLocalSha256;
    }

    public void Remember(CentralKeyLibraryHead confirmed) =>
        SaveObservation(confirmed, null);

    public void RememberReconciled(CentralKeyLibraryHead confirmed, FileRevision local)
    {
        ArgumentNullException.ThrowIfNull(local);
        if (confirmed.CentralRevision <= 0 || !local.Exists || !IsSha(local.Sha256))
            throw new InvalidDataException("Bestätigter gemeinsamer Bibliotheksstand fehlt.");
        SaveObservation(confirmed, local.Sha256);
    }

    // A merge can start from an explicitly approved local offline branch.
    // The durable reconciled base must still be the exact central ciphertext.
    internal void RequireMergeBase(CentralKeyLibraryHead expected, CentralKeyLibraryHead target,
        bool allowCompleted, CentralKeyLibraryHead? historicalBase = null)
    {
        var baseline = historicalBase ?? expected;
        Validate(baseline);
        Validate(expected);
        Validate(target);
        CheckTransition(baseline, expected);
        CheckTransition(expected, target);
        var old = Read().State;
        if (old is null) throw new CryptographicException("Gemeinsamer Abgleichmerker fehlt.");
        if (allowCompleted && old.Head == target && old.ReconciledHead == target &&
            old.ReconciledLocalSha256 == target.CiphertextSha256) return;
        if (old.ReconciledHead != baseline || old.ReconciledLocalSha256 != baseline.CiphertextSha256)
            throw new CryptographicException("Abgleichmerker passt nicht zur bestätigten Zusammenführung.");
        // Observation is not reconciliation. Permit a monotonically observed
        // head between the old shared base and the exact proposed CAS base.
        if (!(allowCompleted && old.Head == target))
            CheckTransition(old.Head, expected);
    }

    internal void RememberMerged(CentralKeyLibraryHead expected, CentralKeyLibraryHead target, FileRevision local,
        CentralKeyLibraryHead? historicalBase = null)
    {
        var baseline = historicalBase ?? expected;
        Validate(baseline);
        Validate(expected);
        Validate(target);
        CheckTransition(baseline, expected);
        CheckTransition(expected, target);
        var (old, revision) = Read();
        if (!local.Exists || local.Sha256 != target.CiphertextSha256 || old?.Head != target ||
            !(old.ReconciledHead == baseline && old.ReconciledLocalSha256 == baseline.CiphertextSha256 ||
              old.ReconciledHead == target && old.ReconciledLocalSha256 == target.CiphertextSha256))
            throw new CryptographicException("Bestätigte Zusammenführung besitzt keine passende Abgleichbasis.");
        if (old.ReconciledHead == target) return;
        SaveCursor(new Cursor(Format, 1, libraryId, target, DateTimeOffset.UtcNow)
        {
            ReconciledHead = target,
            ReconciledLocalSha256 = local.Sha256
        }, revision);
    }

    internal void RequirePolicyTransitionBase(CentralKeyLibraryHead expected,
        CentralKeyLibraryHead confirmed)
    {
        Validate(expected);
        Validate(confirmed);
        var state = Read().State;
        if (state is null ||
            !(state.Head == expected && state.ReconciledHead == expected &&
                state.ReconciledLocalSha256 == expected.CiphertextSha256 ||
              state.Head == confirmed && state.ReconciledHead == confirmed &&
                state.ReconciledLocalSha256 == confirmed.CiphertextSha256))
            throw new CryptographicException("Lokaler Abgleichmerker passt nicht zum vorbereiteten Richtlinienwechsel.");
    }

    /// <summary>Advance across a policy epoch only for the exact signed
    /// transition and installed ciphertext. Normal observations remain strict.</summary>
    internal void RememberPolicyTransition(GroupEnrollmentPackage previous,
        CentralKeyLibraryHead expected, SignedKeyLibraryPolicyTransition transition,
        CentralKeyLibraryHead confirmed, FileRevision installed)
    {
        Validate(expected);
        Validate(confirmed);
        var claim = KeyLibraryPolicyTransition.Verify(transition, previous, expected);
        if (confirmed != new CentralKeyLibraryHead(libraryId, expected.CentralRevision + 1,
                claim.NextCiphertextSha256, expected.GroupId, claim.Policy, expected.CatalogRevision) ||
            !installed.Exists || installed.Sha256 != confirmed.CiphertextSha256)
            throw new CryptographicException("Installierter Richtlinienwechsel stimmt nicht mit der bestätigten Zentrale überein.");

        SavePolicyEpoch(expected, confirmed, installed);
    }

    internal void RememberReceivedPolicy(GroupEnrollmentPackage previous,
        CentralKeyLibraryHead expected, CentralKeyLibrarySnapshot snapshot, FileRevision installed)
    {
        Validate(expected);
        Validate(snapshot.Head);
        _ = KeyLibraryPolicyTransition.VerifyReceivedSnapshot(previous, expected, snapshot);
        if (!installed.Exists || installed.Sha256 != snapshot.Head.CiphertextSha256)
            throw new CryptographicException("Empfangener Vault stimmt nicht mit dem installierten Bestand überein.");
        SavePolicyEpoch(expected, snapshot.Head, installed);
    }

    private void SavePolicyEpoch(CentralKeyLibraryHead expected,
        CentralKeyLibraryHead confirmed, FileRevision installed)
    {
        var (old, revision) = Read();
        if (old?.Head == confirmed && old.ReconciledHead == confirmed &&
            old.ReconciledLocalSha256 == installed.Sha256) return; // restart after cursor commit
        if (old?.Head != expected || old.ReconciledHead != expected ||
            old.ReconciledLocalSha256 != expected.CiphertextSha256)
            throw new CryptographicException("Richtlinienwechsel besitzt keine exakt abgeglichene Vorgängerrevision.");
        SaveCursor(new Cursor(Format, 1, libraryId, confirmed, DateTimeOffset.UtcNow)
        {
            ReconciledHead = confirmed,
            ReconciledLocalSha256 = installed.Sha256
        }, revision);
    }

    private void SaveObservation(CentralKeyLibraryHead confirmed, string? reconciledLocal)
    {
        Validate(confirmed);
        var (old, expected) = Read();
        if (old is not null) CheckTransition(old.Head, confirmed);
        var updated = new Cursor(Format, 1, libraryId, confirmed,
            DateTimeOffset.UtcNow)
        {
            ReconciledHead = reconciledLocal is null ? old?.ReconciledHead : confirmed,
            ReconciledLocalSha256 = reconciledLocal ?? old?.ReconciledLocalSha256
        };
        SaveCursor(updated, expected);
    }

    private void SaveCursor(Cursor updated, FileRevision expected)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(updated, Options);
        try
        {
            var encrypted = protection.Protect(raw);
            if (encrypted.Length is <= 0 or > MaximumBytes)
                throw new InvalidDataException("Geschützter Zentralmerker ist zu groß.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var file = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                file.Write(encrypted);
                file.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private (Cursor? State, FileRevision Revision) Read()
    {
        if (!File.Exists(path)) return (null, FileRevision.Missing);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        if (file.Length is <= 0 or > MaximumBytes)
            throw new InvalidDataException("Zentralmerker ist leer oder zu groß.");
        var encrypted = new byte[(int)file.Length];
        file.ReadExactly(encrypted);
        var revision = new FileRevision(true,
            Convert.ToHexString(SHA256.HashData(encrypted)));
        var raw = protection.Unprotect(encrypted);
        try
        {
            using (var parsed = JsonDocument.Parse(raw,
                new JsonDocumentOptions { MaxDepth = 12 }))
                OfflineGroupPolicyService.RejectDuplicateProperties(parsed.RootElement);
            var value = JsonSerializer.Deserialize<Cursor>(raw, Options)
                ?? throw new InvalidDataException("Zentralmerker fehlt.");
            if (value.Format != Format || value.Version != 1 ||
                value.LibraryId != libraryId || value.Head is null)
                throw new CryptographicException("Zentralmerker gehört zu einer anderen Bibliothek.");
            Validate(value.Head);
            if (value.ReconciledHead is not null)
            {
                Validate(value.ReconciledHead);
                if (value.ReconciledHead.CentralRevision <= 0 || !IsSha(value.ReconciledLocalSha256))
                    throw new InvalidDataException("Gemeinsamer Abgleichstand ist ungültig.");
                CheckTransition(value.ReconciledHead, value.Head);
            }
            else if (value.ReconciledLocalSha256 is not null)
                throw new InvalidDataException("Lokaler Abgleichhash ohne bestätigten zentralen Stand.");
            return (value, revision);
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private void Validate(CentralKeyLibraryHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (libraryId == Guid.Empty || head.LibraryId != libraryId ||
            head.Policy is null || head.Policy.Revision < 1 ||
            !IsSha(head.Policy.PayloadSha256) || head.CentralRevision < 0 ||
            head.CatalogRevision < 0 ||
            (head.CentralRevision == 0 &&
                (head.CatalogRevision != 0 || head.GroupId != "" ||
                    head.CiphertextSha256 != "")) ||
            (head.CentralRevision > 0 &&
                (head.CatalogRevision < 1 ||
                    head.GroupId is not { Length: > 0 and <= 128 } ||
                    !head.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
                    !IsSha(head.CiphertextSha256))))
            throw new InvalidDataException("Ungültige zentrale Bibliotheksrevision.");
    }

    private static void CheckTransition(CentralKeyLibraryHead old,
        CentralKeyLibraryHead observed)
    {
        if (old.LibraryId != observed.LibraryId ||
            old.Policy != observed.Policy ||
            observed.CentralRevision < old.CentralRevision ||
            observed.CatalogRevision < old.CatalogRevision ||
            observed.CentralRevision > old.CentralRevision &&
                observed.CatalogRevision <= old.CatalogRevision ||
            old.CentralRevision > 0 && old.GroupId != observed.GroupId ||
            observed.CentralRevision == old.CentralRevision &&
                (observed.CatalogRevision != old.CatalogRevision ||
                    observed.CiphertextSha256 != old.CiphertextSha256 ||
                    observed.GroupId != old.GroupId))
            throw new CryptographicException(
                "Zentrale liefert einen älteren oder widersprüchlichen signierten Revisionsstand. Kein automatisches Zurücksetzen.");
    }

    private static bool IsSha(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Server-side acknowledgement and conflict cursor. This is NOT a signed
/// receipt or proof that a remote client is authorized to DOWNLOAD secrets.
/// </summary>
public sealed record CentralKeyLibraryHead(Guid LibraryId, long CentralRevision,
    string CiphertextSha256, string GroupId, GroupPolicyCheckpoint Policy,
    long CatalogRevision);

/// <summary>
/// Headless encrypted-only acceptance engine for one pinned independent library.
/// The central service must expose this through authenticated TLS and authenticated
/// downloads later. It neither decrypts keys nor has a user/admin interface.
/// </summary>
public sealed class CentralKeyLibraryStore(string statePath, Guid expectedLibraryId)
{
    private sealed record State(string Format, int Version, Guid LibraryId,
        string AdministratorFingerprint, GroupEnrollmentPackage Enrollment,
        long CentralRevision, string GroupId, SignedKeyLibrarySync? Accepted)
    {
        public ReadNonce[] RecentReadNonces { get; init; } = [];
    }
    private sealed record ReadNonce(Guid RequestId, DateTimeOffset AcceptedAt);
    private const int MaximumRecentReads = 8192;
    private static readonly TimeSpan ReadNonceRetention = TimeSpan.FromMinutes(6);

    private const string Format = "SieEncode-Headless-Central-KeyLibrary";
    private const int MaximumStateBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 22
    };
    private readonly string path = Path.GetFullPath(statePath);

    /// <summary>
    /// Explicit provisioning with independently confirmed administrator fingerprint.
    /// A received upload MUST NOT be allowed to provision its own authority.
    /// </summary>
    public CentralKeyLibraryHead Register(GroupEnrollmentPackage enrollment,
        string independentlyConfirmedAdministratorFingerprint)
    {
        if (expectedLibraryId == Guid.Empty)
            throw new InvalidDataException("Zentrale Bibliotheks-ID fehlt.");
        var vetted = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(enrollment));
        if (vetted.LibraryId != expectedLibraryId)
            throw new CryptographicException("Registrierung gehört zu einer anderen Bibliothek.");
        var checkedPolicy = GroupEnrollmentPackages.Validate(vetted);
        if (checkedPolicy.Policy.Version != 2 ||
            !string.Equals(OfflineGroupTrustStore.AdministratorFingerprint(
                vetted.AdministratorPublicKey), independentlyConfirmedAdministratorFingerprint,
                StringComparison.Ordinal))
            throw new CryptographicException("Unbestätigte zentrale Hauptverwaltung oder nicht unterstützter Gruppenstand.");

        var state = new State(Format, 1, expectedLibraryId,
            independentlyConfirmedAdministratorFingerprint, vetted, 0, "", null);
        Save(state, FileRevision.Missing);
        return Head(state);
    }

    /// <summary>Return only the current version cursor; does not reveal raw card keys.</summary>
    public CentralKeyLibraryHead ReadHead() => Head(Read().State);

    /// <summary>
    /// Authenticate a TLS client certificate against the active signed directory.
    /// Certificates need not have a public CA chain: the exact DER is pinned by
    /// the signed group registration, and TLS verifies private-key possession.
    /// Neither recovery devices nor disabled group members are TLS operators.
    /// </summary>
    public bool IsAuthorizedTlsMember(X509Certificate2 certificate, DateTimeOffset now)
    {
        if (certificate is null) return false;
        return IsAuthorizedTlsMember(Read().State, certificate, now);
    }

    private static bool IsAuthorizedTlsMember(State state, X509Certificate2 certificate,
        DateTimeOffset now)
    {
        var policy = GroupEnrollmentPackages.Validate(state.Enrollment).Policy;
        if (policy.Version != 2 || policy.People is null) return false;
        var groupIds = state.CentralRevision == 0
            ? policy.Groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>([state.GroupId], StringComparer.Ordinal);
        var activeIds = policy.Groups.Where(g => groupIds.Contains(g.Id))
            .SelectMany(g => g.Members).ToHashSet(StringComparer.Ordinal);
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        return policy.People.Where(p => p.Enabled && activeIds.Contains(p.Id))
            .SelectMany(p => p.Devices)
            .Any(d => d.Enabled && d.Fingerprint == fingerprint &&
                d.Certificate == Convert.ToBase64String(certificate.RawData) &&
                now.UtcDateTime >= certificate.NotBefore.ToUniversalTime() &&
                now.UtcDateTime < certificate.NotAfter.ToUniversalTime());
    }

    /// <summary>
    /// Accept one authenticated encrypted snapshot if and only if its signed
    /// expected server revision+hash match the persisted state. No silent merges.
    /// </summary>
    public CentralKeyLibraryHead Accept(SignedKeyLibrarySync transfer)
    {
        var (state, captured) = Read();
        var claim = KeyLibrarySyncTransport.Verify(transfer, state.Enrollment);
        var current = Head(state);
        if (claim.ExpectedCentralRevision != state.CentralRevision ||
            claim.ExpectedCentralCiphertextSha256 != current.CiphertextSha256 ||
            state.CentralRevision == long.MaxValue ||
            state.CentralRevision > 0 && claim.GroupId != state.GroupId ||
            claim.CatalogRevision <= current.CatalogRevision)
            throw new ConcurrentFileChangeException(path);

        // Detach arrays owned by the transport caller before durable acceptance.
        var frozen = new SignedKeyLibrarySync(transfer.Claim.ToArray(),
            transfer.Signature.ToArray(), transfer.EncryptedVault.ToArray());
        var updated = state with
        {
            CentralRevision = state.CentralRevision + 1,
            GroupId = claim.GroupId,
            Accepted = frozen
        };
        Save(updated, captured);
        return Head(updated);
    }

    /// <summary>
    /// Commit a newer signed directory and its freshly encrypted library in
    /// one atomic CAS. There is never an intermediate state in which the
    /// accepted ciphertext and authorized recipient policy disagree.
    /// Ordinary uploads cannot advance a policy or reuse a catalog revision.
    /// </summary>
    public CentralKeyLibraryHead AcceptPolicyTransition(SignedKeyLibraryPolicyTransition transition) =>
        AcceptPolicyTransitionCore(transition, null, default);

    // The network caller must retain membership across the exact CAS; the
    // middleware check alone is not sufficient if the directory changes.
    public CentralKeyLibraryHead AcceptPolicyTransition(SignedKeyLibraryPolicyTransition transition,
        X509Certificate2 authenticatedMember, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authenticatedMember);
        return AcceptPolicyTransitionCore(transition, authenticatedMember, now);
    }

    private CentralKeyLibraryHead AcceptPolicyTransitionCore(SignedKeyLibraryPolicyTransition transition,
        X509Certificate2? authenticatedMember, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(transition);
        var (state, captured) = Read();
        var current = Head(state);
        var enrollment = GroupEnrollmentPackages.Read(
            GroupEnrollmentPackages.Write(transition.Enrollment));
        var transfer = transition.Rewrapped;
        ArgumentNullException.ThrowIfNull(transfer);
        var frozen = new SignedKeyLibrarySync(transfer.Claim.ToArray(),
            transfer.Signature.ToArray(), transfer.EncryptedVault.ToArray());
        _ = KeyLibraryPolicyTransition.Verify(new(enrollment, frozen),
            state.Enrollment, current);
        var updated = state with
        {
            Enrollment = enrollment,
            CentralRevision = checked(state.CentralRevision + 1),
            Accepted = frozen
        };
        if (authenticatedMember is not null &&
            (!IsAuthorizedTlsMember(state, authenticatedMember, now) ||
             !IsAuthorizedTlsMember(updated, authenticatedMember, now)))
            throw new CryptographicException("Der authentifizierte Arbeitsplatz muss in beiden Richtlinien berechtigt sein.");
        Save(updated, captured);
        return Head(updated);
    }

    /// <summary>
    /// Return an encrypted snapshot only after proving private member-key
    /// possession against the pinned active group directory. A network server
    /// must additionally require authenticated TLS and replay protection.
    /// </summary>
    public SignedKeyLibrarySync ReadEncryptedSnapshot(
        SignedKeyLibraryDownload request, DateTimeOffset now) =>
        ReadSnapshotCore(request, now, null).Transfer;

    public CentralKeyLibrarySnapshot ReadAuthorizedSnapshot(SignedKeyLibraryDownload request,
        X509Certificate2 authenticatedMember, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authenticatedMember);
        return ReadSnapshotCore(request, now, authenticatedMember);
    }

    private CentralKeyLibrarySnapshot ReadSnapshotCore(SignedKeyLibraryDownload request,
        DateTimeOffset now, X509Certificate2? authenticatedMember)
    {
        var (state, fileRevision) = Read();
        if (state.Accepted is null)
            throw new FileNotFoundException("Noch kein zentral freigegebener verschlüsselter Bibliotheksstand.");
        var head = Head(state);
        var claim = KeyLibraryDownloadAuthorization.Verify(request, head, state.Enrollment, now);
        if (authenticatedMember is not null &&
            (!IsAuthorizedTlsMember(state, authenticatedMember, now) ||
             claim.DeviceFingerprint != CertificateVaultService.Fingerprint(authenticatedMember)))
            throw new CryptographicException("Download-Signatur und TLS-Mitglied müssen denselben aktuell berechtigten Zugang verwenden.");
        var active = state.RecentReadNonces
            .Where(n => n.AcceptedAt > now - ReadNonceRetention &&
                n.AcceptedAt <= now + ReadNonceRetention).ToArray();
        if (active.Any(n => n.RequestId == claim.RequestId))
            throw new CryptographicException("Signierter Bibliotheksabruf wurde bereits verwendet.");
        if (active.Length >= MaximumRecentReads)
            throw new CryptographicException("Zu viele Download-Autorisierungen im aktuellen Prüfzeitraum.");
        // Persist accepted request IDs BEFORE returning any ciphertext. Failed
        // concurrent writes fail closed; callers must issue a fresh signed request.
        Save(state with { RecentReadNonces = [.. active, new(claim.RequestId, now)] },
            fileRevision);
        var accepted = state.Accepted;
        return new(head, state.Enrollment, new(accepted.Claim.ToArray(), accepted.Signature.ToArray(),
            accepted.EncryptedVault.ToArray()));
    }

    public KeyLibraryAuditReceipt AcceptAudit(SignedKeyLibraryAudit report,
        X509Certificate2 authenticatedMember, DateTimeOffset now)
    {
        var state = Read().State;
        if (state.Accepted is null || !IsAuthorizedTlsMember(state, authenticatedMember, now))
            throw new CryptographicException("Audit-Anmeldung nicht bestätigt.");
        return new CentralKeyLibraryAuditStore(path + ".audit").Accept(report,
            authenticatedMember, expectedLibraryId, state.GroupId,
            GroupEnrollmentPackages.Validate(state.Enrollment), now);
    }

    private (State State, FileRevision Revision) Read()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumStateBytes)
            throw new InvalidDataException("Zentraler Bibliotheksstand hat ungültige Dateigröße.");
        var raw = new byte[(int)stream.Length];
        stream.ReadExactly(raw);
        using (var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 22 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(doc.RootElement);
        var state = JsonSerializer.Deserialize<State>(raw, Options)
            ?? throw new InvalidDataException("Zentraler Bibliotheksstand fehlt.");
        Validate(state);
        return (state, new FileRevision(true, Convert.ToHexString(SHA256.HashData(raw))));
    }

    private static CentralKeyLibraryHead Head(State state) =>
        new(state.LibraryId, state.CentralRevision,
            state.Accepted is null ? "" :
                KeyLibrarySyncTransport.CiphertextSha256(state.Accepted.EncryptedVault),
            state.GroupId, GroupEnrollmentPackages.Validate(state.Enrollment).Checkpoint,
            state.Accepted is null ? 0 :
                KeyLibrarySyncTransport.Verify(state.Accepted, state.Enrollment).CatalogRevision);

    private void Validate(State state)
    {
        if (state is null || state.Format != Format || state.Version != 1 ||
            state.LibraryId != expectedLibraryId || state.Enrollment is null ||
            state.AdministratorFingerprint is not { Length: 64 } ||
            state.CentralRevision < 0 || state.GroupId is null ||
            state.RecentReadNonces is null || state.RecentReadNonces.Length > MaximumRecentReads ||
            state.RecentReadNonces.Any(n => n is null ||
                n.RequestId == Guid.Empty) ||
            state.RecentReadNonces.Select(n => n.RequestId).Distinct().Count() != state.RecentReadNonces.Length)
            throw new InvalidDataException("Zentrale Bibliothekszustandsdaten sind ungültig.");
        var validated = GroupEnrollmentPackages.Validate(state.Enrollment);
        if (state.Enrollment.LibraryId != expectedLibraryId ||
            validated.Policy.Version != 2 ||
            OfflineGroupTrustStore.AdministratorFingerprint(
                state.Enrollment.AdministratorPublicKey) != state.AdministratorFingerprint)
            throw new CryptographicException("Zentrale Vertrauenswurzel passt nicht zum registrierten Hauptverwalter.");

        if (state.CentralRevision == 0)
        {
            if (state.Accepted is not null || state.GroupId != "")
                throw new InvalidDataException("Uninitialisierter Server hat unerwartet Vault-Daten.");
            return;
        }

        if (state.Accepted is null)
            throw new InvalidDataException("Zentrale Revision ohne verschlüsselten Bibliotheksstand.");
        var claim = KeyLibrarySyncTransport.Verify(state.Accepted, state.Enrollment);
        if (claim.ExpectedCentralRevision != state.CentralRevision - 1 ||
            claim.CatalogRevision < 1 || claim.GroupId != state.GroupId)
            throw new CryptographicException("Zentrale Dateirevision stimmt nicht mit dem zuletzt signierten Auftrag überein.");
    }

    private void Save(State state, FileRevision expected)
    {
        Validate(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        if (bytes.Length is <= 0 or > MaximumStateBytes)
            throw new InvalidDataException("Verschlüsselter zentraler Bibliotheksstand ist zu groß.");
        ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        });
    }
}

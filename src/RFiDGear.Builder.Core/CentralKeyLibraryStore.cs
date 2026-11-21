using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Server-side acknowledgement and conflict cursor. This is NOT a signed
/// receipt or proof that a remote client is authorized to DOWNLOAD secrets.
/// </summary>
public sealed record CentralKeyLibraryHead(Guid LibraryId, long CentralRevision,
    string CiphertextSha256, string GroupId, GroupPolicyCheckpoint Policy);

/// <summary>
/// Headless encrypted-only acceptance engine for one pinned independent library.
/// The central service must expose this through authenticated TLS and authenticated
/// downloads later. It neither decrypts keys nor has a user/admin interface.
/// </summary>
public sealed class CentralKeyLibraryStore(string statePath, Guid expectedLibraryId)
{
    private sealed record State(string Format, int Version, Guid LibraryId,
        string AdministratorFingerprint, GroupEnrollmentPackage Enrollment,
        long CentralRevision, string GroupId, SignedKeyLibrarySync? Accepted);

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
            state.CentralRevision > 0 && claim.GroupId != state.GroupId)
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
    /// Encrypted latest copy for a future authenticated sync endpoint. Caller
    /// MUST authenticate/authorize the recipient; this core method is not an API.
    /// </summary>
    public SignedKeyLibrarySync? ReadEncryptedSnapshotForAuthorizedTransport()
    {
        var accepted = Read().State.Accepted;
        return accepted is null ? null : new(
            accepted.Claim.ToArray(), accepted.Signature.ToArray(),
            accepted.EncryptedVault.ToArray());
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
            state.GroupId, GroupEnrollmentPackages.Validate(state.Enrollment).Checkpoint);

    private void Validate(State state)
    {
        if (state is null || state.Format != Format || state.Version != 1 ||
            state.LibraryId != expectedLibraryId || state.Enrollment is null ||
            state.AdministratorFingerprint is not { Length: 64 } ||
            state.CentralRevision < 0 || state.GroupId is null)
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
            claim.GroupId != state.GroupId)
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

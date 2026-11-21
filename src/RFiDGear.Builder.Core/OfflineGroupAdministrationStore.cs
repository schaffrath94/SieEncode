using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>Local signing authority, separate from recipient identities and the public shared directory.</summary>
public sealed class OfflineGroupAdministrationStore(string localPath, Guid libraryId, ILocalGroupTrustProtection protection)
{
    private sealed record State(int Version, Guid LibraryId, byte[] PrivateKey, SignedGroupPolicy Policy);
    private readonly string path = Path.GetFullPath(localPath);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 20 };
    private const int MaximumBytes = 3 * 1024 * 1024;

    public GroupEnrollmentPackage Create()
    {
        if (libraryId == Guid.Empty) throw new ArgumentException("Bibliotheks-ID fehlt.");
        using var administrator = RSA.Create(3072);
        var privateKey = administrator.ExportPkcs8PrivateKey();
        try
        {
            var initial = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, libraryId, 1, []);
            var signed = OfflineGroupPolicyService.Sign(initial, administrator);
            Save(new(1, libraryId, privateKey, signed), FileRevision.Missing);
            return PublicPackage(administrator, signed);
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
    }

    /// <summary>
    /// Require possession of the private signing authority AND an exact match
    /// with this Windows user's independently registered public trust checkpoint.
    /// A member certificate capable of decrypting a Vault is not administration.
    /// </summary>
    public GroupPolicyCheckpoint RequireLocalAuthority(OfflineGroupTrustStore trust)
    {
        ArgumentNullException.ThrowIfNull(trust);
        var own = ExportPublic(); // Proves protected private signing-key possession.
        var registered = trust.ExportPublic();
        var signed = GroupEnrollmentPackages.Validate(own);
        var pinned = GroupEnrollmentPackages.Validate(registered);
        if (registered.LibraryId != libraryId ||
            !own.AdministratorPublicKey.SequenceEqual(registered.AdministratorPublicKey) ||
            signed.Checkpoint != pinned.Checkpoint)
            throw new CryptographicException(
                "Lokaler signierter Verwaltungsstand und bestätigte Gruppenregistrierung stimmen nicht überein.");
        return signed.Checkpoint;
    }

    /// <summary>
    /// Explicitly sign a single encrypted-catalog change. The signature binds
    /// the expected Vault file revision, exact next catalog contents and current
    /// trusted signed group policy. No raw key or private authority leaves here.
    /// </summary>
    public SignedKeyLibraryMutation SignKeyLibraryMutation(OfflineGroupTrustStore trust,
        string operation, string groupId, FileRevision previousVault,
        KeyLibraryVaultPayload next)
    {
        var checkpoint = RequireLocalAuthority(trust);
        var claim = KeyLibraryMutationAuthorization.Prepare(operation, libraryId,
            groupId, checkpoint, previousVault, next);
        var (state, revision) = Load();
        try
        {
            using var administrator = OpenKey(state);
            var verified = OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId);
            if (verified.Checkpoint != checkpoint ||
                trust.LoadOffline().Checkpoint != checkpoint ||
                ConcurrentFileWriter.Capture(path) != revision)
                throw new ConcurrentFileChangeException(path);
            var signed = KeyLibraryMutationAuthorization.Sign(claim, administrator);
            if (ConcurrentFileWriter.Capture(path) != revision ||
                trust.LoadOffline().Checkpoint != checkpoint)
                throw new ConcurrentFileChangeException(path);
            return signed;
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    /// <summary>
    /// Sign the precise encrypted Vault bytes for an offline or future HTTPS
    /// central transfer. This does not upload or accept anything centrally.
    /// </summary>
    public SignedKeyLibrarySync SignEncryptedCentralSync(OfflineGroupTrustStore trust,
        string groupId, CentralKeyLibraryHead expected, byte[] encryptedVault)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.LibraryId != libraryId)
            throw new CryptographicException("Zentrale Revisionsantwort gehört zu einer anderen Bibliothek.");
        var checkpoint = RequireLocalAuthority(trust);
        if (expected.Policy != checkpoint)
            throw new CryptographicException("Zentrale und lokale Gruppenrichtlinie stimmen nicht überein.");
        var claim = KeyLibrarySyncTransport.Prepare(trust.ExportPublic(), groupId,
            expected.CentralRevision, expected.CiphertextSha256, encryptedVault);
        var (state, revision) = Load();
        try
        {
            using var administrator = OpenKey(state);
            if (OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId).Checkpoint != checkpoint ||
                ConcurrentFileWriter.Capture(path) != revision ||
                trust.LoadOffline().Checkpoint != checkpoint)
                throw new ConcurrentFileChangeException(path);
            var signed = KeyLibrarySyncTransport.Sign(claim, encryptedVault, administrator);
            if (ConcurrentFileWriter.Capture(path) != revision ||
                trust.LoadOffline().Checkpoint != checkpoint)
                throw new ConcurrentFileChangeException(path);
            return signed;
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    public GroupEnrollmentPackage ExportPublic()
    {
        var (state, _) = Load();
        try
        {
            using var administrator = OpenKey(state);
            OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId);
            return PublicPackage(administrator, state.Policy);
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    public byte[] ExportPrivateBackup(string passphrase)
    {
        var (state, _) = Load();
        try
        {
            using var administrator = OpenKey(state);
            OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId);
            return GroupPrivateKeyBackups.Protect(administrator, new(libraryId, GroupPrivateKeyBackups.Administrator), passphrase);
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    public GroupEnrollmentPackage RestorePrivateBackup(byte[] backup, string passphrase, OfflineGroupTrustStore trust)
    {
        var package = trust.ExportPublic();
        if (package.LibraryId != libraryId) throw new CryptographicException("Registrierte Bibliothek passt nicht zur Wiederherstellung.");
        var current = GroupEnrollmentPackages.Validate(package);
        using var restored = GroupPrivateKeyBackups.Restore(backup, passphrase, new(libraryId, GroupPrivateKeyBackups.Administrator),
            OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey));
        var privateKey = restored.Key.ExportPkcs8PrivateKey();
        try
        {
            if (trust.LoadOffline().Checkpoint != current.Checkpoint) throw new CryptographicException("Registrierter Stand wurde während der Wiederherstellung geändert.");
            Save(new(1, libraryId, privateKey, package.Policy), FileRevision.Missing, () =>
            {
                if (trust.LoadOffline().Checkpoint != current.Checkpoint) throw new CryptographicException("Registrierter Stand wurde während der Wiederherstellung geändert.");
            });
            return PublicPackage(restored.Key, package.Policy);
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
    }

    public GroupEnrollmentPackage UpdateGroups(OfflineVaultGroup[] groups, GroupPolicyCheckpoint expectedPolicy)
        => Update(groups, null, expectedPolicy);

    public GroupEnrollmentPackage ActivateSuccessor(OfflineGroupSuccessorStore preparation, OfflineGroupTrustStore trust,
        GroupPolicyCheckpoint expectedPolicy, bool confirmed)
    {
        if (!confirmed) throw new InvalidDataException("Aktivierung muss ausdrücklich bestätigt werden.");
        var (key, previous) = preparation.OpenForActivation(libraryId);
        using (key)
        {
            var package = trust.ExportPublic();
            var current = GroupEnrollmentPackages.Validate(package);
            if (package.LibraryId != libraryId || current.Checkpoint != expectedPolicy ||
                !key.ExportSubjectPublicKeyInfo().SequenceEqual(package.AdministratorPublicKey) ||
                current.Policy.Revision <= GroupEnrollmentPackages.Validate(previous).Policy.Revision)
                throw new CryptographicException("Vorbereitung passt nicht zum bestätigten neuen Verwaltungsstand.");
            void CheckTrust()
            {
                var latest = trust.ExportPublic();
                if (!latest.AdministratorPublicKey.SequenceEqual(package.AdministratorPublicKey) ||
                    GroupEnrollmentPackages.Validate(latest).Checkpoint != expectedPolicy)
                    throw new ConcurrentFileChangeException(path);
            }
            var expectedFile = FileRevision.Missing;
            if (File.Exists(path))
            {
                var (old, revision) = Load();
                try
                {
                    using var oldKey = OpenKey(old);
                    var oldPolicy = OfflineGroupPolicyService.Verify(old.Policy, oldKey, libraryId);
                    if (oldKey.ExportSubjectPublicKeyInfo().SequenceEqual(package.AdministratorPublicKey))
                    {
                        if (oldPolicy.Checkpoint != expectedPolicy) throw new ConcurrentFileChangeException(path);
                        CheckTrust();
                        if (ConcurrentFileWriter.Capture(path) != revision) throw new ConcurrentFileChangeException(path);
                        return package; // A completed activation can be resumed without rewriting keys.
                    }
                    if (!oldKey.ExportSubjectPublicKeyInfo().SequenceEqual(previous.AdministratorPublicKey) ||
                        oldPolicy.Checkpoint != GroupEnrollmentPackages.Validate(previous).Checkpoint)
                        throw new CryptographicException("Vorhandene Verwaltung ist nicht der vorbereitete Vorgänger. Datei bleibt erhalten.");
                    byte[] protectedOld;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Ungültiger Verwaltungsstand.");
                        protectedOld = new byte[(int)stream.Length]; stream.ReadExactly(protectedOld);
                    }
                    if (Convert.ToHexString(SHA256.HashData(protectedOld)) != revision.Sha256) throw new ConcurrentFileChangeException(path);
                    CheckTrust();
                    var archive = path + ".retired-" + revision.Sha256 + ".bin";
                    if (!File.Exists(archive)) ConcurrentFileWriter.Write(archive, FileRevision.Missing, temporary => File.WriteAllBytes(temporary, protectedOld));
                    if (ConcurrentFileWriter.Capture(archive) != revision) throw new CryptographicException("Archivprüfung fehlgeschlagen; bisherige Verwaltung bleibt erhalten.");
                    expectedFile = revision;
                }
                finally { CryptographicOperations.ZeroMemory(old.PrivateKey); }
            }
            var privateKey = key.ExportPkcs8PrivateKey();
            try
            {
                Save(new(1, libraryId, privateKey, package.Policy), expectedFile, CheckTrust);
                return package;
            }
            finally { CryptographicOperations.ZeroMemory(privateKey); }
        }
    }

    public GroupEnrollmentPackage UpdateDirectory(OfflineVaultPerson[] people, OfflineVaultGroup[] groups, GroupPolicyCheckpoint expectedPolicy)
        => Update(groups, people ?? throw new ArgumentNullException(nameof(people)), expectedPolicy);

    public GroupEnrollmentPackage UpdateDelegations(OfflineGroupDelegation[] delegations, GroupPolicyCheckpoint expectedPolicy)
        => Transform(expectedPolicy, current => current.Policy with { Revision = checked(current.Policy.Revision + 1),
            Delegations = delegations ?? throw new ArgumentNullException(nameof(delegations)) });

    public GroupEnrollmentPackage UpdateRecoveryDevices(OfflineVaultDevice[] devices, GroupPolicyCheckpoint expectedPolicy)
        => Transform(expectedPolicy, current => current.Policy with { Revision = checked(current.Policy.Revision + 1),
            RecoveryDevices = devices ?? throw new ArgumentNullException(nameof(devices)) });

    public GroupEnrollmentPackage GrantRecovery(VaultRecipientInvitation invitation, string confirmedFingerprint, GroupPolicyCheckpoint expectedPolicy)
    {
        var imported = CertificateVaultService.ImportInvitation(invitation);
        using var certificate = imported.Certificate;
        var fingerprint = CertificateVaultService.Fingerprint(certificate);
        if (fingerprint != confirmedFingerprint) throw new CryptographicException("Recovery-Fingerabdruck stimmt nicht mit der unabhängigen Bestätigung überein.");
        var device = new OfflineVaultDevice(imported.DisplayName, fingerprint, Convert.ToBase64String(certificate.RawData), true);
        return Transform(expectedPolicy, current =>
        {
            if (current.Policy.Version != 2) throw new InvalidOperationException("Bitte zuerst das Personen-/Gruppenverzeichnis signiert speichern.");
            if (current.Policy.RecoveryDevices.Any(d => d.Fingerprint == fingerprint))
                throw new InvalidOperationException("Recovery-Zugang ist bereits vorhanden. Den vorhandenen Eintrag verwenden.");
            return current.Policy with { Revision = checked(current.Policy.Revision + 1), RecoveryDevices = [.. current.Policy.RecoveryDevices, device] };
        });
    }

    /// <summary>Produces only a public transition. Does not activate the successor or alter local trust.</summary>
    public SignedAdministratorTransition AuthorizeSuccessor(AdministratorTransitionRequest request,
        OfflineGroupTrustStore trust, GroupPolicyCheckpoint expectedPolicy, string confirmedFingerprint, bool confirmed)
    {
        var candidate = GroupAdministratorTransition.ReadRequest(GroupAdministratorTransition.WriteRequest(request));
        var enrolled = trust.ExportPublic();
        var current = GroupEnrollmentPackages.Validate(enrolled);
        if (enrolled.LibraryId != libraryId || current.Checkpoint != expectedPolicy)
            throw new CryptographicException("Registrierter Ausgangsstand passt nicht zur Übergabe.");
        var (state, revision) = Load();
        try
        {
            using var administrator = OpenKey(state);
            var local = OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId);
            if (local.Checkpoint != expectedPolicy || !administrator.ExportSubjectPublicKeyInfo().SequenceEqual(enrolled.AdministratorPublicKey))
                throw new CryptographicException("Lokale Verwaltung und Registrierung stimmen nicht überein.");
            var transition = GroupAdministratorTransition.Authorize(candidate, enrolled, administrator, confirmedFingerprint, confirmed);
            var latest = trust.ExportPublic();
            if (ConcurrentFileWriter.Capture(path) != revision ||
                !latest.AdministratorPublicKey.SequenceEqual(enrolled.AdministratorPublicKey) ||
                GroupEnrollmentPackages.Validate(latest).Checkpoint != expectedPolicy)
                throw new ConcurrentFileChangeException(path);
            return transition;
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    public GroupEnrollmentPackage ApplyDelegatedChange(SignedGroupChange change, GroupPolicyCheckpoint expectedPolicy)
        => Transform(expectedPolicy, current => OfflineGroupDelegationService.Apply(change, current));

    public GroupEnrollmentPackage GrantDelegation(GroupDelegateInvitation invitation, string confirmedFingerprint,
        string delegationId, string[] groupIds, GroupPolicyCheckpoint expectedPolicy)
    {
        // Freeze the candidate before the trust check and the eventual policy write.
        var candidate = GroupDelegationTransport.ReadInvitation(GroupDelegationTransport.WriteInvitation(invitation));
        if (candidate.LibraryId != libraryId || GroupDelegationTransport.InvitationFingerprint(candidate) != confirmedFingerprint)
            throw new CryptographicException("Einladung passt nicht zur Bibliothek oder zum unabhängig bestätigten Fingerabdruck.");
        var scope = groupIds.ToArray();
        return Transform(expectedPolicy, current =>
        {
            if (!current.Policy.People.Any(p => p.Id == candidate.PersonId && p.Enabled))
                throw new CryptographicException("Aktive Person für die Vollmacht fehlt.");
            if (current.Policy.Delegations.Any(g => g.Id == delegationId))
                throw new InvalidDataException("Vorhandene Vollmacht darf durch einen Einladungsimport nicht ersetzt werden.");
            return current.Policy with { Revision = checked(current.Policy.Revision + 1), Delegations =
                [.. current.Policy.Delegations, new(delegationId, candidate.PersonId, candidate.SigningPublicKey, scope, true)] };
        });
    }

    private GroupEnrollmentPackage Transform(GroupPolicyCheckpoint expectedPolicy, Func<VerifiedGroupPolicy, OfflineGroupPolicy> transform)
    {
        ArgumentNullException.ThrowIfNull(expectedPolicy);
        var (state, revision) = Load();
        try
        {
            using var administrator = OpenKey(state);
            var current = OfflineGroupPolicyService.Verify(state.Policy, administrator, libraryId);
            if (current.Checkpoint != expectedPolicy) throw new ConcurrentFileChangeException(path);
            var signed = OfflineGroupPolicyService.Sign(transform(current), administrator);
            Save(state with { Policy = signed }, revision);
            return PublicPackage(administrator, signed);
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    private GroupEnrollmentPackage Update(OfflineVaultGroup[] groups, OfflineVaultPerson[]? people, GroupPolicyCheckpoint expectedPolicy)
        => Transform(expectedPolicy, current => current.Policy with { Revision = checked(current.Policy.Revision + 1), Groups = groups,
            Version = people is null ? current.Policy.Version : 2, People = people ?? current.Policy.People });

    private GroupEnrollmentPackage PublicPackage(RSA administrator, SignedGroupPolicy policy)
        => new(GroupEnrollmentPackages.Format, 1, libraryId, administrator.ExportSubjectPublicKeyInfo(), policy);

    private RSA OpenKey(State state)
    {
        if (libraryId == Guid.Empty || state.Version != 1 || state.LibraryId != libraryId ||
            state.PrivateKey is null || state.PrivateKey.Length is 0 or > 16384 || state.Policy is null)
            throw new InvalidDataException("Ungültiger lokaler Verwaltungsstand.");
        var rsa = RSA.Create();
        try
        {
            rsa.ImportPkcs8PrivateKey(state.PrivateKey, out var read);
            if (read != state.PrivateKey.Length || rsa.KeySize < 3072) throw new CryptographicException("Ungültiger Verwaltungsschlüssel.");
            return rsa;
        }
        catch { rsa.Dispose(); throw; }
    }

    private (State, FileRevision) Load()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Ungültiger Verwaltungsstand.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var revision = new FileRevision(true, Convert.ToHexString(SHA256.HashData(bytes)));
        var plaintext = protection.Unprotect(bytes);
        try
        {
            if (plaintext.Length > MaximumBytes) throw new InvalidDataException("Verwaltungsstand ist zu groß.");
            var state = JsonSerializer.Deserialize<State>(plaintext, Options) ?? throw new InvalidDataException("Verwaltungsstand fehlt.");
            return (state, revision);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private void Save(State state, FileRevision expected, Action? validateCurrent = null)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        try
        {
            var bytes = protection.Protect(plaintext);
            if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Verwaltungsstand ist zu groß.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                validateCurrent?.Invoke();
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}

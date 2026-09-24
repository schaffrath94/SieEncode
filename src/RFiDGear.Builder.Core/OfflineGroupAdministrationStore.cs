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

    public GroupEnrollmentPackage UpdateGroups(OfflineVaultGroup[] groups, GroupPolicyCheckpoint expectedPolicy)
        => Update(groups, null, expectedPolicy);

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

    private void Save(State state, FileRevision expected)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        try
        {
            var bytes = protection.Protect(plaintext);
            if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Verwaltungsstand ist zu groß.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}

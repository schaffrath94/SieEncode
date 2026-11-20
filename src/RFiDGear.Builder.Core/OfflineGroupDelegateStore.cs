using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record GroupDelegateInvitation(Guid LibraryId, string PersonId, byte[] SigningPublicKey);

/// <summary>Local protected signing key only. Grants come exclusively from the enrolled policy.</summary>
public sealed class OfflineGroupDelegateStore
{
    private sealed record State(int Version, Guid LibraryId, string PersonId, byte[] PrivateKey);
    private readonly string path;
    private readonly Guid libraryId;
    private readonly string personId;
    private readonly ILocalGroupTrustProtection protection;
    private const int MaximumBytes = 32768;
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };

    public OfflineGroupDelegateStore(string localPath, Guid libraryId, string personId, ILocalGroupTrustProtection protection)
    {
        if (libraryId == Guid.Empty || personId is not { Length: > 0 and <= 128 } ||
            !personId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new ArgumentException("Bibliothek oder Personen-ID fehlt.");
        path = Path.GetFullPath(localPath);
        this.libraryId = libraryId;
        this.personId = personId;
        this.protection = protection ?? throw new ArgumentNullException(nameof(protection));
    }

    public GroupDelegateInvitation Create()
    {
        using var rsa = RSA.Create(3072);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        try
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(new State(1, libraryId, personId, privateKey), Options);
            try
            {
                var bytes = protection.Protect(plaintext);
                if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Geschützter Verwaltungsschlüssel ist ungültig.");
                ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
                {
                    using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.Write(bytes); stream.Flush(true);
                });
                return new(libraryId, personId, rsa.ExportSubjectPublicKeyInfo());
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
    }

    public GroupDelegateInvitation ExportPublic()
    {
        using var rsa = LoadKey();
        return new(libraryId, personId, rsa.ExportSubjectPublicKeyInfo());
    }

    public byte[] ExportPrivateBackup(string passphrase)
    {
        using var rsa = LoadKey();
        return GroupPrivateKeyBackups.Protect(rsa, new(libraryId, GroupPrivateKeyBackups.Delegate, personId), passphrase);
    }

    public GroupDelegateInvitation RestorePrivateBackup(byte[] backup, string passphrase, OfflineGroupTrustStore trust, string delegationId)
    {
        var current = trust.LoadOffline();
        var grant = current.Policy.Delegations.SingleOrDefault(g => g.Id == delegationId && g.Enabled && g.PersonId == personId);
        if (current.Policy.LibraryId != libraryId || grant is null || !current.Policy.People.Any(p => p.Id == personId && p.Enabled))
            throw new CryptographicException("Aktive registrierte Verwaltungsvollmacht fehlt für die Wiederherstellung.");
        using var restored = GroupPrivateKeyBackups.Restore(backup, passphrase, new(libraryId, GroupPrivateKeyBackups.Delegate, personId),
            OfflineGroupTrustStore.AdministratorFingerprint(grant.SigningPublicKey));
        var privateKey = restored.Key.ExportPkcs8PrivateKey();
        byte[]? plaintext = null;
        try
        {
            plaintext = JsonSerializer.SerializeToUtf8Bytes(new State(1, libraryId, personId, privateKey), Options);
            var bytes = protection.Protect(plaintext);
            if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Geschützter Verwaltungsschlüssel ist ungültig.");
            ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
            {
                if (trust.LoadOffline().Checkpoint != current.Checkpoint) throw new CryptographicException("Registrierter Stand wurde während der Wiederherstellung geändert.");
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
            return new(libraryId, personId, restored.Key.ExportSubjectPublicKeyInfo());
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    public SignedGroupChange SignChange(OfflineGroupTrustStore trust, string delegationId,
        GroupPolicyCheckpoint expectedPolicy, OfflineVaultGroup[] groups)
    {
        ArgumentNullException.ThrowIfNull(trust);
        var current = trust.LoadOffline();
        if (current.Policy.LibraryId != libraryId || current.Checkpoint != expectedPolicy)
            throw new CryptographicException("Verwaltungsentwurf passt nicht zum registrierten Bibliotheksstand.");
        using var rsa = LoadKey();
        var grant = current.Policy.Delegations.SingleOrDefault(g => g.Id == delegationId);
        if (grant is null || grant.PersonId != personId || !grant.SigningPublicKey.SequenceEqual(rsa.ExportSubjectPublicKeyInfo()))
            throw new CryptographicException("Dieser lokale Schlüssel hat keine passende Verwaltungsvollmacht.");
        var signed = OfflineGroupDelegationService.Sign(new(OfflineGroupDelegationService.Format, 1, libraryId,
            delegationId, current.Checkpoint, groups), rsa);
        // Validate the exact signed bytes, including active status and scope, before releasing them.
        OfflineGroupDelegationService.Apply(signed, current);
        if (trust.LoadOffline().Checkpoint != current.Checkpoint)
            throw new CryptographicException("Gruppenstand wurde während der Bearbeitung geändert. Bitte neu laden.");
        return signed;
    }

    private RSA LoadKey()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Lokaler Verwaltungsschlüssel ist ungültig.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var plaintext = protection.Unprotect(bytes);
        State? state = null;
        try
        {
            if (plaintext.Length is 0 or > MaximumBytes) throw new InvalidDataException("Lokaler Verwaltungsschlüssel ist ungültig.");
            using (var document = JsonDocument.Parse(plaintext, new JsonDocumentOptions { MaxDepth = 8 }))
                OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
            state = JsonSerializer.Deserialize<State>(plaintext, Options);
            if (state is null || state.Version != 1 || state.LibraryId != libraryId || state.PersonId != personId ||
                state.PrivateKey is null || state.PrivateKey.Length is 0 or > 16384)
                throw new CryptographicException("Lokaler Verwaltungsschlüssel gehört nicht zu dieser Person/Bibliothek.");
            var rsa = RSA.Create();
            try
            {
                rsa.ImportPkcs8PrivateKey(state.PrivateKey, out var read);
                if (read != state.PrivateKey.Length || rsa.KeySize < 3072) throw new CryptographicException("Ungültiger Verwaltungsschlüssel.");
                return rsa;
            }
            catch { rsa.Dispose(); throw; }
        }
        finally
        {
            if (state?.PrivateKey is not null) CryptographicOperations.ZeroMemory(state.PrivateKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}

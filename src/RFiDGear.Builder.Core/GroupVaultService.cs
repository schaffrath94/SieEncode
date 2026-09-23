using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record GroupVaultRecipient(string Role, string Fingerprint, byte[] WrappedKey);
public sealed record GroupVaultHeader(string Format, int Version, Guid LibraryId, Guid ProjectId, string GroupId,
    GroupPolicyCheckpoint Policy, Guid KeyGeneration, GroupVaultRecipient[] Recipients);
public sealed record GroupVaultDocument(GroupVaultHeader Header, byte[] Nonce, byte[] Ciphertext, byte[] Tag);

/// <summary>New group format; callers supply enrolled policy and the independently selected project/group identity.</summary>
public static class GroupVaultService
{
    public const string Format = "SieEncode-Group-Vault";
    public const int MaximumBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };

    public static GroupVaultDocument Protect(string payloadJson, VerifiedGroupPolicy trusted, Guid projectId, string groupId, DateTimeOffset now)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Stabile Projekt-ID fehlt.");
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        if (Encoding.UTF8.GetByteCount(payloadJson) > MaximumBytes / 2) throw new InvalidDataException("Vault-Nutzdaten sind zu groß.");
        using (JsonDocument.Parse(payloadJson)) { }
        var policy = SnapshotPolicy(trusted);
        var members = OfflineGroupDirectory.ResolveRecipients(policy, groupId, now);
        var recovery = policy.RecoveryDevices.Where(d => d.Enabled).ToArray();
        if (recovery.Length == 0) throw new InvalidOperationException("Mindestens ein separater Recovery-Zugang muss zentral freigegeben sein.");
        var key = RandomNumberGenerator.GetBytes(32);
        var plain = Encoding.UTF8.GetBytes(payloadJson);
        try
        {
            GroupVaultRecipient Wrap(string role, string fingerprint, string publicCertificate)
            {
                using var certificate = OfflineGroupDirectory.ReadPublicCertificate(publicCertificate);
                if (now.UtcDateTime < certificate.NotBefore.ToUniversalTime() || now.UtcDateTime >= certificate.NotAfter.ToUniversalTime())
                    throw new CryptographicException("Aktiver Vault-Zugang ist noch nicht gültig oder abgelaufen.");
                using var rsa = certificate.GetRSAPublicKey()!;
                return new(role, fingerprint, rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256));
            }
            var recipients = members.Select(d => Wrap("member", d.Fingerprint, d.Certificate))
                .Concat(recovery.Select(d => Wrap("recovery", d.Fingerprint, d.Certificate))).ToArray();
            var header = new GroupVaultHeader(Format, 1, policy.LibraryId, projectId, groupId, trusted.Checkpoint, Guid.NewGuid(), recipients);
            var nonce = RandomNumberGenerator.GetBytes(12); var ciphertext = new byte[plain.Length]; var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, ciphertext, tag, JsonSerializer.SerializeToUtf8Bytes(header, Options));
            var result = new GroupVaultDocument(header, nonce, ciphertext, tag);
            Validate(result); return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    public static string Unprotect(GroupVaultDocument document, VerifiedGroupPolicy trusted, Guid projectId, string groupId,
        IEnumerable<X509Certificate2> identities, bool recovery = false)
    {
        var vault = Read(Write(document));
        var policy = SnapshotPolicy(trusted);
        var header = vault.Header;
        if (projectId == Guid.Empty || header.ProjectId != projectId || header.LibraryId != policy.LibraryId || header.GroupId != groupId ||
            header.Policy.Revision > policy.Revision || header.Policy.Revision == policy.Revision && header.Policy != trusted.Checkpoint)
            throw new CryptographicException("Vault passt nicht zu Projekt, Gruppe oder registriertem Richtlinienstand.");
        var group = policy.Groups.SingleOrDefault(g => g.Id == groupId);
        if (!recovery && group is null) throw new CryptographicException("Projektgruppe fehlt im registrierten Stand.");
        var allowed = (recovery ? policy.RecoveryDevices.Where(d => d.Enabled) : policy.People.Where(p => p.Enabled && group!.Members.Contains(p.Id, StringComparer.Ordinal))
            .SelectMany(p => p.Devices).Where(d => d.Enabled)).Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
        foreach (var identity in identities.Where(c => c.HasPrivateKey))
        {
            var fingerprint = CertificateVaultService.Fingerprint(identity);
            if (!allowed.Contains(fingerprint)) continue;
            var recipient = header.Recipients.SingleOrDefault(r => r.Fingerprint == fingerprint && r.Role == (recovery ? "recovery" : "member"));
            if (recipient is null) continue;
            using var rsa = identity.GetRSAPrivateKey();
            if (rsa is null) continue;
            var key = rsa.Decrypt(recipient.WrappedKey, RSAEncryptionPadding.OaepSHA256);
            var plain = new byte[vault.Ciphertext.Length];
            try
            {
                if (key.Length != 32) throw new CryptographicException("Ungültiger Vault-Inhaltsschlüssel.");
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(vault.Nonce, vault.Ciphertext, vault.Tag, plain, JsonSerializer.SerializeToUtf8Bytes(header, Options));
                var result = Encoding.UTF8.GetString(plain); using (JsonDocument.Parse(result)) { } return result;
            }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        }
        throw new CryptographicException("Kein aktuell freigegebener privater Schlüssel für diesen Vault-Zugang vorhanden.");
    }

    public static byte[] Write(GroupVaultDocument document)
    {
        Validate(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Gruppen-Vault ist zu groß.");
        return bytes;
    }

    public static GroupVaultDocument Read(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Gruppen-Vault ist leer oder zu groß.");
        var snapshot = bytes.ToArray();
        using (var document = JsonDocument.Parse(snapshot, new JsonDocumentOptions { MaxDepth = 16 })) OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        var vault = JsonSerializer.Deserialize<GroupVaultDocument>(snapshot, Options) ?? throw new InvalidDataException("Gruppen-Vault fehlt.");
        Validate(vault); return vault;
    }

    private static OfflineGroupPolicy SnapshotPolicy(VerifiedGroupPolicy trusted)
    {
        var policy = JsonSerializer.Deserialize<OfflineGroupPolicy>(JsonSerializer.SerializeToUtf8Bytes(trusted.Policy, Options), Options)!;
        OfflineGroupPolicyService.Validate(policy);
        if (policy.Version != 2 || trusted.Checkpoint.Revision != policy.Revision || !Fingerprint(trusted.Checkpoint.PayloadSha256))
            throw new InvalidDataException("Verifizierter Personen-/Gruppenstand erforderlich.");
        return policy;
    }

    private static bool Fingerprint(string? value) => value is { Length: 64 } && value.All(c => c is >= 'A' and <= 'F' or >= '0' and <= '9');
    private static void Validate(GroupVaultDocument document)
    {
        var h = document?.Header;
        if (h is null || h.Format != Format || h.Version != 1 || h.LibraryId == Guid.Empty || h.ProjectId == Guid.Empty || h.KeyGeneration == Guid.Empty ||
            h.GroupId is not { Length: > 0 and <= 128 } || !h.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            h.Policy is null || h.Policy.Revision < 1 || !Fingerprint(h.Policy.PayloadSha256) || h.Recipients is null || h.Recipients.Length is 0 or > 10000 ||
            document!.Nonce is not { Length: 12 } || document.Tag is not { Length: 16 } || document.Ciphertext is null || document.Ciphertext.Length is 0 or > MaximumBytes / 2)
            throw new InvalidDataException("Ungültiges Gruppen-Vault-Format.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in h.Recipients)
            if (r is null || r.Role is not ("member" or "recovery") || !Fingerprint(r.Fingerprint) || !ids.Add(r.Fingerprint) || r.WrappedKey is null || r.WrappedKey.Length is < 384 or > 2048)
                throw new InvalidDataException("Ungültige oder doppelte Vault-Empfänger.");
        if (!h.Recipients.Any(r => r.Role == "member") || !h.Recipients.Any(r => r.Role == "recovery")) throw new InvalidDataException("Gruppen- oder Recovery-Zugang fehlt.");
    }
}

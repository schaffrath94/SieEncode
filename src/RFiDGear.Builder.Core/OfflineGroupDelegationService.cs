using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record OfflineGroupDelegation(string Id, string PersonId, byte[] SigningPublicKey, string[] GroupIds, bool Enabled);
public sealed record OfflineGroupChange(string Format, int Version, Guid LibraryId, string DelegationId,
    GroupPolicyCheckpoint BasePolicy, OfflineVaultGroup[] Groups);
public sealed record SignedGroupChange(byte[] Payload, byte[] Signature);

/// <summary>Membership/name changes only. The caller supplies a policy verified against its enrolled trust root.</summary>
public static class OfflineGroupDelegationService
{
    public const string Format = "SieEncode-Offline-Group-Change";
    private const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };

    internal static void ValidateDelegations(OfflineGroupPolicy policy)
    {
        if (policy.Delegations is null || policy.Delegations.Length > 1000 || policy.Version == 1 && policy.Delegations.Length != 0)
            throw new InvalidDataException("Ungültige Gruppenverwaltungsvollmachten.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in policy.Delegations)
        {
            if (grant is null || !Id(grant.Id) || !ids.Add(grant.Id) ||
                !policy.People.Any(p => p.Id == grant.PersonId) || grant.GroupIds is null || grant.GroupIds.Length is 0 or > 1000 ||
                grant.GroupIds.Distinct(StringComparer.Ordinal).Count() != grant.GroupIds.Length ||
                grant.GroupIds.Any(id => !policy.Groups.Any(g => g.Id == id)))
                throw new InvalidDataException("Vollmacht enthält unbekannte Personen/Gruppen oder doppelte Einträge.");
            using var key = OpenPublicKey(grant.SigningPublicKey);
            if (!keys.Add(Convert.ToHexString(SHA256.HashData(grant.SigningPublicKey))))
                throw new InvalidDataException("Ein Verwaltungsschlüssel darf nur einer Vollmacht zugeordnet sein.");
            foreach (var device in policy.People.SelectMany(p => p.Devices))
            {
                using var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String(device.Certificate));
                using var recipientKey = System.Security.Cryptography.X509Certificates.RSACertificateExtensions.GetRSAPublicKey(certificate);
                if (recipientKey is not null && recipientKey.ExportSubjectPublicKeyInfo().SequenceEqual(grant.SigningPublicKey))
                    throw new CryptographicException("Verwaltungs- und Entschlüsselungsschlüssel müssen getrennt sein.");
            }
        }
    }

    public static SignedGroupChange Sign(OfflineGroupChange change, RSA signer)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (signer.KeySize < 3072) throw new CryptographicException("Verwaltungsschlüssel benötigt mindestens 3072 Bit RSA.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(change, Options);
        if (payload.Length > MaximumBytes) throw new InvalidDataException("Gruppenänderung ist zu groß.");
        return new(payload, signer.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static OfflineGroupPolicy Apply(SignedGroupChange signed, VerifiedGroupPolicy trustedCurrent)
    {
        ArgumentNullException.ThrowIfNull(signed);
        ArgumentNullException.ThrowIfNull(trustedCurrent);
        // Snapshot the trusted state too: returned policy never shares mutable arrays with a caller.
        var current = JsonSerializer.Deserialize<OfflineGroupPolicy>(JsonSerializer.SerializeToUtf8Bytes(trustedCurrent.Policy, Options), Options)!;
        OfflineGroupPolicyService.Validate(current);
        if (current.Version != 2 || signed.Payload is null || signed.Payload.Length is 0 or > MaximumBytes ||
            signed.Signature is null || signed.Signature.Length is 0 or > 2048)
            throw new InvalidDataException("Ungültige delegierte Gruppenänderung.");
        var payload = signed.Payload.ToArray();
        var signature = signed.Signature.ToArray();
        using (var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        var change = JsonSerializer.Deserialize<OfflineGroupChange>(payload, Options)
            ?? throw new InvalidDataException("Gruppenänderung fehlt.");
        if (change.Format != Format || change.Version != 1 || change.LibraryId != current.LibraryId ||
            change.BasePolicy != trustedCurrent.Checkpoint || change.BasePolicy.Revision != current.Revision ||
            change.Groups is null || change.Groups.Length is 0 or > 1000)
            throw new CryptographicException("Gruppenänderung passt nicht zum aktuellen Bibliotheksstand.");
        var grant = current.Delegations.SingleOrDefault(g => g.Id == change.DelegationId);
        if (grant is null || !grant.Enabled || !current.People.Single(p => p.Id == grant.PersonId).Enabled)
            throw new CryptographicException("Gruppenverwaltung ist nicht freigegeben.");
        using var key = OpenPublicKey(grant.SigningPublicKey);
        if (signature.Length != key.KeySize / 8 || !key.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Gruppenänderung stammt nicht vom freigegebenen Gruppenverwalter.");
        var replacements = new Dictionary<string, OfflineVaultGroup>(StringComparer.Ordinal);
        foreach (var group in change.Groups)
            if (group is null || group.Id is null || !grant.GroupIds.Contains(group.Id, StringComparer.Ordinal) || !replacements.TryAdd(group.Id, group))
                throw new CryptographicException("Gruppenänderung überschreitet die Vollmacht oder enthält doppelte Gruppen.");
        var next = current with { Revision = checked(current.Revision + 1),
            Groups = current.Groups.Select(g => replacements.GetValueOrDefault(g.Id, g)).ToArray() };
        OfflineGroupPolicyService.Validate(next);
        return next;
    }

    private static RSA OpenPublicKey(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > 4096) throw new InvalidDataException("Öffentlicher Verwaltungsschlüssel fehlt.");
        var rsa = RSA.Create();
        try
        {
            rsa.ImportSubjectPublicKeyInfo(bytes, out var read);
            if (read != bytes.Length || rsa.KeySize is < 3072 or > 16384 || !rsa.ExportSubjectPublicKeyInfo().SequenceEqual(bytes))
                throw new CryptographicException("Ungültiger öffentlicher Verwaltungsschlüssel.");
            return rsa;
        }
        catch { rsa.Dispose(); throw; }
    }

    private static bool Id(string? id) => id is { Length: > 0 and <= 128 } && id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}

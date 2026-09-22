using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record OfflineVaultGroup(string Id, string Name, string[] Members);
public sealed record OfflineGroupPolicy(string Format, int Version, Guid LibraryId, long Revision, OfflineVaultGroup[] Groups);
public sealed record SignedGroupPolicy(byte[] Payload, byte[] Signature);
public sealed record GroupPolicyCheckpoint(long Revision, string PayloadSha256);
public sealed record VerifiedGroupPolicy(OfflineGroupPolicy Policy, GroupPolicyCheckpoint Checkpoint);

/// <summary>Policy verification only. Trust enrollment, persistence and vault access are separate layers.</summary>
public static class OfflineGroupPolicyService
{
    public const string Format = "SieEncode-Offline-Group-Policy";
    private const int MaximumPayloadBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    public static SignedGroupPolicy Sign(OfflineGroupPolicy policy, RSA administrator)
    {
        Validate(policy);
        RequireKey(administrator);
        var payload = JsonSerializer.SerializeToUtf8Bytes(policy, Options);
        if (payload.Length > MaximumPayloadBytes) throw new InvalidDataException("Gruppenrichtlinie ist zu groß.");
        return new(payload, administrator.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static VerifiedGroupPolicy Verify(SignedGroupPolicy signed, RSA trustedAdministrator,
        Guid expectedLibraryId, GroupPolicyCheckpoint? previous = null)
    {
        ArgumentNullException.ThrowIfNull(signed);
        RequireKey(trustedAdministrator);
        if (expectedLibraryId == Guid.Empty) throw new ArgumentException("Bestätigte Bibliotheks-ID fehlt.", nameof(expectedLibraryId));
        if (previous is not null && (previous.Revision < 1 || !IsFingerprint(previous.PayloadSha256)))
            throw new InvalidDataException("Lokaler Richtlinienstand ist ungültig.");
        if (signed.Payload is null || signed.Payload.Length is 0 or > MaximumPayloadBytes ||
            signed.Signature is null || signed.Signature.Length != trustedAdministrator.KeySize / 8)
            throw new InvalidDataException("Ungültige signierte Gruppenrichtlinie.");
        // Work on a private snapshot: caller-owned arrays cannot change between verification and parsing.
        var payload = signed.Payload.ToArray();
        var signature = signed.Signature.ToArray();
        if (!trustedAdministrator.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Gruppenfreigabe stammt nicht vom bestätigten Verwalter oder wurde verändert.");
        using (var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 }))
            RejectDuplicateProperties(document.RootElement);
        var policy = JsonSerializer.Deserialize<OfflineGroupPolicy>(payload, Options)
            ?? throw new InvalidDataException("Gruppenrichtlinie fehlt.");
        Validate(policy);
        if (policy.LibraryId != expectedLibraryId) throw new CryptographicException("Gruppenfreigabe gehört zu einer anderen Bibliothek.");
        var digest = Convert.ToHexString(SHA256.HashData(payload));
        if (previous is not null && (policy.Revision < previous.Revision ||
            policy.Revision == previous.Revision && !string.Equals(digest, previous.PayloadSha256, StringComparison.Ordinal)))
            throw new CryptographicException("Veraltete oder widersprüchliche Gruppenfreigabe.");
        return new(policy, new(policy.Revision, digest));
    }

    private static void RequireKey(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.KeySize < 3072) throw new CryptographicException("Verwaltungsschlüssel benötigt mindestens 3072 Bit RSA.");
    }

    private static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void Validate(OfflineGroupPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Format != Format || policy.Version != 1 || policy.LibraryId == Guid.Empty || policy.Revision < 1 || policy.Groups is null || policy.Groups.Length > 1000)
            throw new InvalidDataException("Gruppenrichtlinie hat ein ungültiges Format.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in policy.Groups)
        {
            if (group is null || string.IsNullOrWhiteSpace(group.Id) || group.Id.Length > 128 ||
                !group.Id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') || !ids.Add(group.Id) ||
                string.IsNullOrWhiteSpace(group.Name) || group.Name.Length > 200 || group.Name.Any(char.IsControl) ||
                group.Members is null || group.Members.Length > 10000)
                throw new InvalidDataException("Gruppe ist ungültig oder doppelt vorhanden.");
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in group.Members)
                if (!IsFingerprint(member) || !members.Add(member)) throw new InvalidDataException("Gruppenmitglied ist ungültig oder doppelt vorhanden.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Doppeltes Feld in Gruppenrichtlinie.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
}

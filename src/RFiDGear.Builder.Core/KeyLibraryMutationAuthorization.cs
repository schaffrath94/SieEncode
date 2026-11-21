using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Signed, secret-free intent for one exact local catalog transition.
/// The private signing key never leaves the established administration store.
/// </summary>
public sealed record KeyLibraryMutationClaim(
    string Format, int Version, string Operation, Guid LibraryId, string GroupId,
    GroupPolicyCheckpoint Policy, FileRevision PreviousVault, long NextRevision,
    string NextPayloadSha256, Guid Nonce);

public sealed record SignedKeyLibraryMutation(byte[] Payload, byte[] Signature);

/// <summary>
/// Cryptographic write authorization is checked at the Vault store boundary,
/// not only in the Studio dialog. This does NOT constitute central acceptance,
/// tamper-proof offline audit or revocation of previously copied offline keys.
/// </summary>
public static class KeyLibraryMutationAuthorization
{
    public const string Format = "SieEncode-KeyLibrary-Mutation";
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };
    private const int MaximumClaimBytes = 4096;

    public static KeyLibraryMutationClaim Prepare(string operation, Guid libraryId,
        string groupId, GroupPolicyCheckpoint policy, FileRevision previousVault,
        KeyLibraryVaultPayload next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(previousVault);
        if (operation is not ("create" or "update") || libraryId == Guid.Empty ||
            next.Catalog.LibraryId != libraryId || next.Catalog.Revision < 1 ||
            groupId is not { Length: > 0 and <= 128 } ||
            !groupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            policy is null || policy.Revision < 1 ||
            policy.PayloadSha256 is not { Length: 64 } ||
            !policy.PayloadSha256.All(Uri.IsHexDigit) ||
            operation == "create" && previousVault.Exists ||
            operation == "update" && (!previousVault.Exists ||
                previousVault.Sha256 is not { Length: 64 } ||
                !previousVault.Sha256.All(Uri.IsHexDigit)))
            throw new InvalidDataException("Unvollständiger Bibliotheksänderungs-Kontext.");
        return new(Format, 1, operation, libraryId, groupId, policy, previousVault,
            next.Catalog.Revision, PayloadFingerprint(next), Guid.NewGuid());
    }

    /// <summary>Used only with a private signing authority, never member-decryption certificates.</summary>
    public static SignedKeyLibraryMutation Sign(KeyLibraryMutationClaim claim, RSA administrator)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        if (administrator.KeySize < 3072) throw new CryptographicException("Verwaltungssignatur benötigt RSA-3072.");
        ValidateClaim(claim);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(claim, Options);
        if (bytes.Length > MaximumClaimBytes) throw new InvalidDataException("Signaturauftrag ist zu groß.");
        return new(bytes, administrator.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static void Verify(SignedKeyLibraryMutation signed,
        OfflineGroupTrustStore trust, string operation, Guid libraryId,
        string groupId, FileRevision previousVault, KeyLibraryVaultPayload next,
        GroupPolicyCheckpoint policy)
    {
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(signed);
        if (signed.Payload is not { Length: > 0 and <= MaximumClaimBytes } ||
            signed.Signature is not { Length: >= 384 and <= 1024 })
            throw new CryptographicException("Fehlende oder ungültige Bibliotheks-Verwaltungssignatur.");

        var registered = trust.ExportPublic();
        var pinned = GroupEnrollmentPackages.Validate(registered);
        if (pinned.Checkpoint != policy || registered.LibraryId != libraryId)
            throw new CryptographicException("Signaturverwaltung und lokal bestätigter Gruppenstand weichen ab.");
        using var authority = RSA.Create();
        authority.ImportSubjectPublicKeyInfo(registered.AdministratorPublicKey, out var consumed);
        if (consumed != registered.AdministratorPublicKey.Length || authority.KeySize < 3072 ||
            signed.Signature.Length != authority.KeySize / 8 ||
            !authority.VerifyData(signed.Payload, signed.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Bibliotheksänderung nicht von der bestätigten Hauptverwaltung signiert.");

        using (var doc = JsonDocument.Parse(signed.Payload, new JsonDocumentOptions { MaxDepth = 12 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(doc.RootElement);
        var claim = JsonSerializer.Deserialize<KeyLibraryMutationClaim>(signed.Payload, Options)
            ?? throw new InvalidDataException("Bibliotheksänderungsauftrag ist leer.");
        ValidateClaim(claim);
        if (!signed.Payload.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(claim, Options)))
            throw new CryptographicException("Nicht kanonisch codierte Änderungsfreigabe.");
        if (claim.Operation != operation || claim.LibraryId != libraryId ||
            claim.GroupId != groupId || claim.Policy != policy ||
            claim.PreviousVault != previousVault ||
            claim.NextRevision != next.Catalog.Revision ||
            claim.NextPayloadSha256 != PayloadFingerprint(next))
            throw new CryptographicException("Signierter Auftrag passt nicht zu Gruppe, Dateirevision oder neuem Bibliotheksinhalt.");
    }

    private static string PayloadFingerprint(KeyLibraryVaultPayload next)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(next, ProjectJson.Options)));

    private static void ValidateClaim(KeyLibraryMutationClaim claim)
    {
        if (claim is null || claim.Format != Format || claim.Version != 1 ||
            claim.Operation is not ("create" or "update") ||
            claim.LibraryId == Guid.Empty || claim.NextRevision < 1 || claim.Nonce == Guid.Empty ||
            claim.Policy is null || claim.Policy.Revision < 1 ||
            claim.Policy.PayloadSha256 is not { Length: 64 } ||
            !claim.Policy.PayloadSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
            claim.PreviousVault is null ||
            claim.Operation == "create" && claim.PreviousVault.Exists ||
            claim.Operation == "update" && (!claim.PreviousVault.Exists ||
                claim.PreviousVault.Sha256 is not { Length: 64 } ||
                !claim.PreviousVault.Sha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) ||
            claim.GroupId is not { Length: > 0 and <= 128 } ||
            !claim.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            claim.NextPayloadSha256 is not { Length: 64 } ||
            !claim.NextPayloadSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new InvalidDataException("Ungültiger signierter Bibliotheks-Änderungsauftrag.");
    }
}

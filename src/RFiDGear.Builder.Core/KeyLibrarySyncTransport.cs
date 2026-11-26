using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Secret-free central CAS request. The SHA-256 covers the EXACT encrypted
/// GroupVaultService.Write bytes; no decrypted library schema reaches the server.
/// </summary>
public sealed record KeyLibrarySyncClaim(
    string Format, int Version, Guid LibraryId, string GroupId,
    GroupPolicyCheckpoint Policy, long ExpectedCentralRevision,
    string ExpectedCentralCiphertextSha256, string NextCiphertextSha256,
    long CatalogRevision, Guid TransferId);

public sealed record SignedKeyLibrarySync(
    byte[] Claim, byte[] Signature, byte[] EncryptedVault);

/// <summary>
/// Offline-capable authenticated transport, independent of any HTTP/UI transport.
/// Only the enrolled main administrator may issue write requests. A read-only
/// synchronized Vault may later be distributed to authorized certificate holders.
/// </summary>
public static class KeyLibrarySyncTransport
{
    public const string Format = "SieEncode-Central-Encrypted-Library-Sync";
    private const int MaximumClaimBytes = 4096;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    internal static SignedKeyLibrarySync Capture(SignedKeyLibrarySync transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.Claim is not { Length: > 0 and <= MaximumClaimBytes } ||
            transfer.Signature is not { Length: >= 384 and <= 1024 } ||
            transfer.EncryptedVault is not { Length: > 0 and <= GroupVaultService.MaximumBytes })
            throw new CryptographicException("Ungültige signierte Bibliotheksübertragung.");
        return new(transfer.Claim.ToArray(), transfer.Signature.ToArray(), transfer.EncryptedVault.ToArray());
    }

    public static string CiphertextSha256(byte[] encrypted)
    {
        if (encrypted is null || encrypted.Length is <= 0 or > GroupVaultService.MaximumBytes)
            throw new InvalidDataException("Verschlüsselte Bibliotheksdatei fehlt oder ist zu groß.");
        return Convert.ToHexString(SHA256.HashData(encrypted));
    }

    public static KeyLibrarySyncClaim Prepare(GroupEnrollmentPackage enrolled,
        string groupId, long expectedCentralRevision, string expectedCentralCiphertextSha256,
        long catalogRevision, byte[] encrypted)
    {
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        if (catalogRevision < 1 || expectedCentralRevision < 0 ||
            expectedCentralRevision == long.MaxValue ||
            expectedCentralRevision == 0 && expectedCentralCiphertextSha256 != "" ||
            expectedCentralRevision > 0 &&
                !IsSha(expectedCentralCiphertextSha256))
            throw new InvalidDataException("Fehlende oder ungültige vorherige zentrale Revision.");
        ValidateEnvelope(enrolled, policy, groupId, encrypted, catalogRevision);
        return new(Format, 1, enrolled.LibraryId, groupId, policy.Checkpoint,
            expectedCentralRevision, expectedCentralCiphertextSha256,
            CiphertextSha256(encrypted), catalogRevision, Guid.NewGuid());
    }

    public static SignedKeyLibrarySync Sign(KeyLibrarySyncClaim claim,
        byte[] encrypted, RSA signingAuthority)
    {
        ValidateClaim(claim);
        ArgumentNullException.ThrowIfNull(signingAuthority);
        if (signingAuthority.KeySize < 3072 ||
            claim.NextCiphertextSha256 != CiphertextSha256(encrypted))
            throw new CryptographicException("Signatur passt nicht zur verschlüsselten Bibliothek.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(claim, Options);
        if (bytes.Length > MaximumClaimBytes)
            throw new InvalidDataException("Übertragungsauftrag ist zu groß.");
        return new(bytes, signingAuthority.SignData(bytes, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss), encrypted.ToArray());
    }

    public static KeyLibrarySyncClaim Verify(SignedKeyLibrarySync transfer,
        GroupEnrollmentPackage enrolled)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        if (transfer.Claim is not { Length: > 0 and <= MaximumClaimBytes } ||
            transfer.Signature is not { Length: >= 384 and <= 1024 })
            throw new CryptographicException("Ungültige signierte Bibliotheksübertragung.");
        using var authority = RSA.Create();
        authority.ImportSubjectPublicKeyInfo(enrolled.AdministratorPublicKey, out var used);
        if (used != enrolled.AdministratorPublicKey.Length ||
            transfer.Signature.Length != authority.KeySize / 8 ||
            !authority.VerifyData(transfer.Claim, transfer.Signature, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss))
            throw new CryptographicException("Bibliotheksübertragung wurde nicht vom registrierten Hauptverwalter signiert.");

        using (var document = JsonDocument.Parse(transfer.Claim, new JsonDocumentOptions { MaxDepth = 12 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        var claim = JsonSerializer.Deserialize<KeyLibrarySyncClaim>(transfer.Claim, Options)
            ?? throw new InvalidDataException("Signierter Bibliotheksauftrag fehlt.");
        ValidateClaim(claim);
        if (!transfer.Claim.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(claim, Options)) ||
            claim.LibraryId != enrolled.LibraryId || claim.Policy != policy.Checkpoint)
            throw new CryptographicException("Nicht kanonischer Auftrag oder anderer signierter Gruppenstand.");
        ValidateEnvelope(enrolled, policy, claim.GroupId, transfer.EncryptedVault, claim.CatalogRevision);
        if (claim.NextCiphertextSha256 != CiphertextSha256(transfer.EncryptedVault))
            throw new CryptographicException("Übertragene verschlüsselte Datei stimmt nicht mit ihrer Signatur überein.");
        return claim;
    }

    private static void ValidateClaim(KeyLibrarySyncClaim claim)
    {
        if (claim is null || claim.Format != Format || claim.Version != 1 ||
            claim.LibraryId == Guid.Empty || claim.TransferId == Guid.Empty ||
            claim.CatalogRevision < 1 ||
            claim.GroupId is not { Length: > 0 and <= 128 } ||
            !claim.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            claim.Policy is null || claim.Policy.Revision < 1 ||
            !IsSha(claim.Policy.PayloadSha256) ||
            claim.ExpectedCentralRevision < 0 || claim.ExpectedCentralRevision == long.MaxValue ||
            claim.ExpectedCentralRevision == 0 && claim.ExpectedCentralCiphertextSha256 != "" ||
            claim.ExpectedCentralRevision > 0 && !IsSha(claim.ExpectedCentralCiphertextSha256) ||
            !IsSha(claim.NextCiphertextSha256))
            throw new InvalidDataException("Ungültiger zentraler Revisions- oder Signaturauftrag.");
    }

    private static bool IsSha(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>
    /// The server validates the recipient list against the SIGNED person/group
    /// directory, but never calls KeyLibraryVault.Unprotect.
    /// </summary>
    private static void ValidateEnvelope(GroupEnrollmentPackage enrolled,
        VerifiedGroupPolicy signedPolicy, string groupId, byte[] encrypted, long catalogRevision)
    {
        if (signedPolicy.Policy.CustomerScopesEnabled)
        {
            var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(encrypted, GroupVaultService.MaximumBytes);
            var bundle = ScopedKeyLibraryVault.Verify(signed, enrolled, groupId);
            if (bundle.CatalogRevision != catalogRevision)
                throw new CryptographicException("Signierte Kundenbibliothek hat eine abweichende Katalogrevision.");
            return;
        }
        var document = GroupVaultService.Read(encrypted);
        var header = document.Header;
        if (signedPolicy.Policy.Version != 2 ||
            header.Format != GroupVaultService.KeyLibraryFormat ||
            header.LibraryId != enrolled.LibraryId ||
            header.ProjectId != enrolled.LibraryId ||
            header.GroupId != groupId || header.Policy != signedPolicy.Checkpoint)
            throw new CryptographicException("Verschlüsselter Vault gehört nicht zur signierten Schlüsselbibliothek/Gruppe.");

        var policy = signedPolicy.Policy;
        var group = policy.Groups.SingleOrDefault(g => g.Id == groupId)
            ?? throw new CryptographicException("Bibliotheksgruppe ist nicht registriert.");
        var allowed = policy.People.Where(p => p.Enabled &&
                group.Members.Contains(p.Id, StringComparer.Ordinal))
            .SelectMany(p => p.Devices).Where(d => d.Enabled)
            .Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
        var recoveries = policy.RecoveryDevices.Where(d => d.Enabled)
            .Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
        var memberRecipients = header.Recipients.Where(r => r.Role == "member")
            .Select(r => r.Fingerprint).ToHashSet(StringComparer.Ordinal);
        var recoveryRecipients = header.Recipients.Where(r => r.Role == "recovery")
            .Select(r => r.Fingerprint).ToHashSet(StringComparer.Ordinal);
        if (allowed.Count == 0 || recoveries.Count == 0 ||
            !allowed.SetEquals(memberRecipients) || !recoveries.SetEquals(recoveryRecipients))
            throw new CryptographicException("Verschlüsselter Empfängerkreis stimmt nicht mit der signierten Gruppenrichtlinie überein.");
    }
}

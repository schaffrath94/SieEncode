using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Short-lived proof of member-key possession for downloading an encrypted
/// central snapshot. It NEVER grants authority to upload/edit a key library.
/// </summary>
public sealed record KeyLibraryDownloadClaim(
    string Format, int Version, Guid LibraryId, string GroupId,
    long CentralRevision, string CiphertextSha256,
    string DeviceFingerprint, DateTimeOffset IssuedAt, Guid RequestId);

public sealed record SignedKeyLibraryDownload(byte[] Claim, byte[] Signature);

public static class KeyLibraryDownloadAuthorization
{
    public const string Format = "SieEncode-Central-KeyLibrary-Read";
    private const int MaximumBytes = 4096;
    private static readonly TimeSpan ClockWindow = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 10
    };

    public static SignedKeyLibraryDownload Sign(CentralKeyLibraryHead head,
        X509Certificate2 memberIdentity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(memberIdentity);
        if (head.CentralRevision <= 0 || head.LibraryId == Guid.Empty ||
            !memberIdentity.HasPrivateKey)
            throw new CryptographicException("Gültiger zentraler Stand und privater persönlicher Zugang erforderlich.");
        using var rsa = memberIdentity.GetRSAPrivateKey();
        if (rsa is null || rsa.KeySize < 3072)
            throw new CryptographicException("Persönliche Identität benötigt einen privaten RSA-Zugang.");
        var request = new KeyLibraryDownloadClaim(Format, 1, head.LibraryId, head.GroupId,
            head.CentralRevision, head.CiphertextSha256,
            CertificateVaultService.Fingerprint(memberIdentity), now, Guid.NewGuid());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, Options);
        if (bytes.Length is 0 or > MaximumBytes)
            throw new InvalidDataException("Download-Freigabe hat ungültige Größe.");
        return new(bytes, rsa.SignData(bytes, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));
    }

    public static void Verify(SignedKeyLibraryDownload signed, CentralKeyLibraryHead head,
        GroupEnrollmentPackage enrolled, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signed);
        ArgumentNullException.ThrowIfNull(head);
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        if (signed.Claim is not { Length: > 0 and <= MaximumBytes } ||
            signed.Signature is not { Length: >= 384 and <= 1024 } ||
            head.CentralRevision < 1 || head.LibraryId != enrolled.LibraryId ||
            head.Policy != policy.Checkpoint)
            throw new CryptographicException("Ungültige oder veraltete zentrale Download-Autorisierung.");
        using (var document = JsonDocument.Parse(signed.Claim, new JsonDocumentOptions { MaxDepth = 10 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        var request = JsonSerializer.Deserialize<KeyLibraryDownloadClaim>(signed.Claim, Options)
            ?? throw new InvalidDataException("Download-Anfrage fehlt.");
        if (request.Format != Format || request.Version != 1 ||
            request.RequestId == Guid.Empty || request.LibraryId != head.LibraryId ||
            request.GroupId != head.GroupId ||
            request.CentralRevision != head.CentralRevision ||
            request.CiphertextSha256 != head.CiphertextSha256 ||
            request.DeviceFingerprint is not { Length: 64 } ||
            !request.DeviceFingerprint.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
            request.IssuedAt < now - ClockWindow || request.IssuedAt > now + ClockWindow ||
            !signed.Claim.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(request, Options)))
            throw new CryptographicException("Download-Anfrage passt nicht zum aktuellen zentralen Bibliotheksstand.");

        var group = policy.Policy.Groups.SingleOrDefault(g => g.Id == head.GroupId)
            ?? throw new CryptographicException("Zentrale Download-Gruppe ist nicht freigegeben.");
        var member = policy.Policy.People.Where(p => p.Enabled &&
                group.Members.Contains(p.Id, StringComparer.Ordinal))
            .SelectMany(p => p.Devices).SingleOrDefault(d => d.Enabled &&
                d.Fingerprint == request.DeviceFingerprint)
            ?? throw new CryptographicException("Persönliches Gerät ist nicht als aktiver Gruppenempfänger registriert.");
        using var publicCertificate = OfflineGroupDirectory.ReadPublicCertificate(member.Certificate);
        if (now.UtcDateTime < publicCertificate.NotBefore.ToUniversalTime() ||
            now.UtcDateTime >= publicCertificate.NotAfter.ToUniversalTime())
            throw new CryptographicException("Persönliches Zertifikat ist nicht mehr gültig.");
        using var rsa = publicCertificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize < 3072 ||
            signed.Signature.Length != rsa.KeySize / 8 ||
            !rsa.VerifyData(signed.Claim, signed.Signature, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss))
            throw new CryptographicException("Download-Anfrage besitzt keine gültige persönliche Signatur.");
    }
}

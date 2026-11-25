using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record KeyLibraryAuditQuery(string Format, Guid LibraryId,
    GroupPolicyCheckpoint Policy, string DeviceFingerprint, string AfterId, int Limit,
    DateTimeOffset IssuedAt);
public sealed record SignedKeyLibraryAuditQuery(byte[] Payload, byte[] Signature);
public sealed record KeyLibraryAuditPage(AcceptedKeyLibraryAudit[] Records, string? NextAfterId);

/// <summary>Separate administrator authorization for reading the complete
/// audit log. An ordinary member certificate is not sufficient.</summary>
public static class KeyLibraryAuditReview
{
    public const string Format = "SieEncode-Central-Audit-Review-v1";
    public const int MaximumPageSize = 32;
    public const int MaximumPageBytes = 4 * 1024 * 1024;
    public const int MaximumQueryBytes = 16 * 1024;

    public static SignedKeyLibraryAuditQuery Sign(KeyLibraryAuditQuery query, RSA authority)
    {
        Validate(query);
        if (authority.KeySize < 3072) throw new CryptographicException("Verwaltungsschlüssel erforderlich.");
        var payload = KeyLibrarySyncWire.Write(query);
        return new(payload, authority.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static KeyLibraryAuditQuery Verify(SignedKeyLibraryAuditQuery signed,
        GroupEnrollmentPackage enrolled, X509Certificate2 tlsIdentity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signed);
        if (signed.Signature is not { Length: >= 384 and <= 2048 })
            throw new CryptographicException("Ungültige Audit-Lesefreigabe.");
        var query = KeyLibrarySyncWire.Read<KeyLibraryAuditQuery>(signed.Payload, MaximumQueryBytes);
        Validate(query);
        var verified = GroupEnrollmentPackages.Validate(enrolled);
        using var authority = OfflineGroupDelegationService.OpenPublicKey(enrolled.AdministratorPublicKey);
        if (query.LibraryId != enrolled.LibraryId || query.Policy != verified.Checkpoint ||
            query.DeviceFingerprint != CertificateVaultService.Fingerprint(tlsIdentity) ||
            query.IssuedAt < now.AddMinutes(-2) || query.IssuedAt > now.AddMinutes(2) ||
            !signed.Payload.SequenceEqual(KeyLibrarySyncWire.Write(query)) ||
            signed.Signature.Length != authority.KeySize / 8 ||
            !authority.VerifyData(signed.Payload, signed.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Audit-Lesefreigabe ist nicht aktuell oder stammt nicht von der bestätigten Hauptverwaltung.");
        return query;
    }

    internal static void Validate(KeyLibraryAuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Format != Format || query.LibraryId == Guid.Empty ||
            query.Policy is null || query.Policy.Revision < 1 ||
            !IsHash(query.Policy.PayloadSha256) || !IsHash(query.DeviceFingerprint) ||
            query.AfterId is null || query.AfterId.Length != 0 && !IsCursor(query.AfterId) ||
            query.Limit is < 1 or > MaximumPageSize || query.IssuedAt == default)
            throw new InvalidDataException("Ungültige Audit-Seitenabfrage.");
    }

    internal static bool IsCursor(string? value) => value is { Length: 32 } &&
        Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty && id.ToString("N") == value;
    private static bool IsHash(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    public static KeyLibraryDisclosureEvent ValidateRecord(AcceptedKeyLibraryAudit record, Guid libraryId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.DeviceCertificate is not { Length: > 0 and <= 32768 } ||
            record.Receipt is null || record.PolicyAtAcceptance is null)
            throw new InvalidDataException("Ungültiger zentraler Audit-Datensatz.");
        using var cert = OfflineGroupDirectory.ReadPublicCertificate(Convert.ToBase64String(record.DeviceCertificate));
        var entry = KeyLibraryAuditSync.Verify(record.Report, cert);
        if (entry.LibraryId != libraryId || record.Receipt.EventId != entry.EventId ||
            record.Receipt.PayloadSha256 != KeyLibraryAuditSync.Digest(record.Report) ||
            record.Receipt.AcceptedAt == default || record.PolicyAtAcceptance.Revision < entry.Policy.Revision ||
            !IsHash(record.PolicyAtAcceptance.PayloadSha256) ||
            record.PolicyAtAcceptance.Revision == entry.Policy.Revision && record.PolicyAtAcceptance != entry.Policy)
            throw new CryptographicException("Audit-Datensatz und Empfangsbestätigung stimmen nicht überein.");
        return entry;
    }

    public static void ValidatePage(KeyLibraryAuditQuery query, KeyLibraryAuditPage page)
    {
        Validate(query);
        if (page.Records is null || page.Records.Length > query.Limit)
            throw new InvalidDataException("Audit-Seite ist zu groß.");
        var previous = query.AfterId;
        foreach (var record in page.Records)
        {
            var entry = ValidateRecord(record, query.LibraryId);
            var id = entry.EventId.ToString("N");
            if (string.CompareOrdinal(id, previous) <= 0)
                throw new InvalidDataException("Audit-Seite enthält doppelte oder ungeordnete Ereignisse.");
            previous = id;
        }
        if (page.NextAfterId is not null &&
            (page.Records.Length != query.Limit || page.NextAfterId != previous))
            throw new InvalidDataException("Ungültiger Audit-Fortsetzungsmerker.");
    }
}

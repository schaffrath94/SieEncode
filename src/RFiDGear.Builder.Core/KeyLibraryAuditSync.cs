using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record SignedKeyLibraryAudit(byte[] Payload, byte[] Signature);
public sealed record KeyLibraryAuditReceipt(Guid EventId, string PayloadSha256, DateTimeOffset AcceptedAt);
public sealed record AcceptedKeyLibraryAudit(KeyLibraryAuditReceipt Receipt,
    SignedKeyLibraryAudit Report, byte[] DeviceCertificate, GroupPolicyCheckpoint PolicyAtAcceptance);

/// <summary>Device-signed disclosure reports, not proof that the reported
/// historical action actually occurred. No secret fields are defined.</summary>
public static class KeyLibraryAuditSync
{
    public const int MaximumBytes = 16 * 1024;
    public static SignedKeyLibraryAudit Sign(KeyLibraryDisclosureEvent entry, X509Certificate2 identity)
    {
        Validate(entry);
        if (entry.DeviceFingerprint != CertificateVaultService.Fingerprint(identity))
            throw new CryptographicException("Audit-Gerät stimmt nicht überein.");
        using var key = identity.GetRSAPrivateKey();
        if (key is null || key.KeySize < 3072) throw new CryptographicException("Privater Audit-Zugang fehlt.");
        var payload = KeyLibrarySyncWire.Write(entry);
        return new(payload, key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static KeyLibraryDisclosureEvent Parse(SignedKeyLibraryAudit report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Signature is not { Length: >= 384 and <= 2048 })
            throw new CryptographicException("Ungültige Audit-Signatur.");
        var entry = KeyLibrarySyncWire.Read<KeyLibraryDisclosureEvent>(report.Payload, MaximumBytes);
        Validate(entry);
        if (!report.Payload.SequenceEqual(KeyLibrarySyncWire.Write(entry)))
            throw new InvalidDataException("Nicht kanonischer Audit-Nachweis.");
        return entry;
    }

    public static KeyLibraryDisclosureEvent Verify(SignedKeyLibraryAudit report, X509Certificate2 identity)
    {
        var entry = Parse(report);
        using var key = identity.GetRSAPublicKey();
        if (entry.DeviceFingerprint != CertificateVaultService.Fingerprint(identity) ||
            key is null || key.KeySize < 3072 || report.Signature.Length != key.KeySize / 8 ||
            !key.VerifyData(report.Payload, report.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Audit-Signatur oder Geräteidentität ungültig.");
        return entry;
    }

    public static string Digest(SignedKeyLibraryAudit report) =>
        Convert.ToHexString(SHA256.HashData(report.Payload));

    private static void Validate(KeyLibraryDisclosureEvent entry)
    {
        if (entry is null || entry.EventId == Guid.Empty || entry.LibraryId == Guid.Empty ||
            entry.CustomerId == Guid.Empty || entry.KeyId == Guid.Empty || entry.Version < 1 ||
            entry.CatalogRevision < 1 || entry.Policy is null || entry.Policy.Revision < 1 ||
            !Sha(entry.Policy.PayloadSha256) || !Sha(entry.DeviceFingerprint) ||
            !Id(entry.PersonId) || !Id(entry.GroupId) || entry.Time == default ||
            entry.Operation is not ("view-requested" or "copy-requested"))
            throw new InvalidDataException("Ungültiger Audit-Nachweis.");
    }

    private static bool Sha(string? s) => s is { Length: 64 } &&
        s.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool Id(string? s) => s is { Length: > 0 and <= 128 } &&
        s.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}

/// <summary>Independent, create-once records avoid rewriting the encrypted
/// catalog for each event. Same event ID and payload is an idempotent retry.
/// Server filesystem administration is trusted; no external audit anchor.</summary>
public sealed class CentralKeyLibraryAuditStore(string directory)
{
    public KeyLibraryAuditReceipt Accept(SignedKeyLibraryAudit report, X509Certificate2 identity,
        Guid libraryId, string groupId, VerifiedGroupPolicy current, DateTimeOffset now)
    {
        var frozen = KeyLibrarySyncWire.Read<SignedKeyLibraryAudit>(KeyLibrarySyncWire.Write(report),
            KeyLibraryAuditSync.MaximumBytes);
        var entry = KeyLibraryAuditSync.Verify(frozen, identity);
        var policy = current.Policy;
        var group = policy.Groups.SingleOrDefault(g => g.Id == groupId);
        var encoded = Convert.ToBase64String(identity.RawData);
        if (entry.LibraryId != libraryId || policy.LibraryId != libraryId ||
            entry.GroupId != groupId || policy.Version != 2 || group is null ||
            !policy.People.Any(p => p.Id == entry.PersonId && p.Enabled &&
                group.Members.Contains(p.Id, StringComparer.Ordinal) && p.Devices.Any(d =>
                    d.Enabled && d.Fingerprint == entry.DeviceFingerprint && d.Certificate == encoded)) ||
            now.UtcDateTime < identity.NotBefore.ToUniversalTime() ||
            now.UtcDateTime >= identity.NotAfter.ToUniversalTime() ||
            entry.Policy.Revision > current.Checkpoint.Revision ||
            entry.Policy.Revision == current.Checkpoint.Revision && entry.Policy != current.Checkpoint ||
            entry.Time > now.AddMinutes(5))
            throw new CryptographicException("Audit-Meldung passt nicht zum aktuell authentifizierten Mitglied.");
        var hash = KeyLibraryAuditSync.Digest(frozen);
        var path = Path.Combine(Path.GetFullPath(directory), entry.EventId.ToString("N") + ".json");
        var receipt = new KeyLibraryAuditReceipt(entry.EventId, hash, now);
        var accepted = new AcceptedKeyLibraryAudit(receipt, frozen, identity.RawData, current.Checkpoint);
        var bytes = KeyLibrarySyncWire.Write(accepted);
        if (bytes.Length > 64 * 1024) throw new InvalidDataException("Audit-Datensatz zu groß.");
        try
        {
            ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
            return receipt;
        }
        catch (ConcurrentFileChangeException)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var old = KeyLibrarySyncWire.ReadAsync<AcceptedKeyLibraryAudit>(stream, 64 * 1024).GetAwaiter().GetResult();
            if (old.Receipt.EventId != entry.EventId || old.Receipt.PayloadSha256 != hash ||
                !old.Report.Payload.SequenceEqual(frozen.Payload) ||
                !old.DeviceCertificate.SequenceEqual(identity.RawData))
                throw;
            _ = KeyLibraryAuditSync.Verify(old.Report, identity);
            return old.Receipt;
        }
    }
}

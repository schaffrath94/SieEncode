using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>Local evidence of an explicit disclosure attempt. Never receives
/// a secret value. The user-controlled offline system is not a tamper-proof log.</summary>
public sealed record KeyLibraryDisclosureEvent(Guid EventId, DateTimeOffset Time,
    Guid LibraryId, string GroupId, string PersonId, string DeviceFingerprint,
    Guid CustomerId, Guid KeyId, int Version, long CatalogRevision,
    GroupPolicyCheckpoint Policy, string Operation);

public static class KeyLibraryDisclosureAudit
{
    public static void Record(string directory, ILocalGroupTrustProtection protection,
        KeyLibraryDisclosureEvent entry)
    {
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.EventId == Guid.Empty || entry.LibraryId == Guid.Empty ||
            entry.CustomerId == Guid.Empty || entry.KeyId == Guid.Empty ||
            entry.Version < 1 || entry.CatalogRevision < 1 ||
            entry.Policy is null || entry.Policy.Revision < 1 ||
            string.IsNullOrWhiteSpace(entry.PersonId) || string.IsNullOrWhiteSpace(entry.GroupId) ||
            entry.DeviceFingerprint is not { Length: 64 } ||
            entry.Operation is not ("view-requested" or "copy-requested"))
            throw new InvalidDataException("Ungültiger Nachweis der Klartextanforderung.");
        var path = Path.Combine(Path.GetFullPath(directory), entry.EventId.ToString("N") + ".audit.bin");
        var raw = KeyLibrarySyncWire.Write(entry);
        try
        {
            var bytes = protection.Protect(raw);
            if (bytes.Length is <= 0 or > 64 * 1024)
                throw new InvalidDataException("Lokaler Nachweis ist ungültig.");
            ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes);
                stream.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    public static void RecordSigned(string directory, ILocalGroupTrustProtection protection,
        KeyLibraryDisclosureEvent entry, X509Certificate2 identity)
    {
        var signed = KeyLibraryAuditSync.Sign(entry, identity);
        var raw = KeyLibrarySyncWire.Write(signed);
        try
        {
            var bytes = protection.Protect(raw);
            if (bytes.Length is <= 0 or > 64 * 1024)
                throw new InvalidDataException("Geschützter Audit-Nachweis ist zu groß.");
            var path = Path.Combine(Path.GetFullPath(directory), entry.EventId.ToString("N") + ".signed-audit.bin");
            ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    /// <summary>Drain a bounded number of this device's pending reports. Keep
    /// the original protected report until a matching acknowledgement arrives.
    /// Caller serializes with the existing library lease.</summary>
    public static async Task<int> FlushAsync(string directory, ILocalGroupTrustProtection protection,
        X509Certificate2 identity, Func<SignedKeyLibraryAudit, CancellationToken, Task<KeyLibraryAuditReceipt>> send,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory)) return 0;
        var count = 0;
        var fingerprint = CertificateVaultService.Fingerprint(identity);
        foreach (var path in Directory.EnumerateFiles(directory, "*.signed-audit.bin").OrderBy(p => p, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (count >= 20) break;
            var expected = ConcurrentFileWriter.Capture(path);
            byte[] protectedBytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length is <= 0 or > 64 * 1024)
                    throw new InvalidDataException("Lokaler Audit-Nachweis hat ungültige Größe.");
                protectedBytes = new byte[(int)stream.Length];
                stream.ReadExactly(protectedBytes);
            }
            var raw = protection.Unprotect(protectedBytes);
            SignedKeyLibraryAudit report;
            try { report = KeyLibrarySyncWire.Read<SignedKeyLibraryAudit>(raw, KeyLibraryAuditSync.MaximumBytes); }
            finally { CryptographicOperations.ZeroMemory(raw); }
            var entry = KeyLibraryAuditSync.Parse(report);
            if (entry.DeviceFingerprint != fingerprint) continue;
            _ = KeyLibraryAuditSync.Verify(report, identity);
            if (Path.GetFileName(path) != entry.EventId.ToString("N") + ".signed-audit.bin" ||
                ConcurrentFileWriter.Capture(path) != expected)
                throw new ConcurrentFileChangeException(path);
            var receipt = await send(report, cancellationToken).ConfigureAwait(false);
            if (receipt.EventId != entry.EventId || receipt.PayloadSha256 != KeyLibraryAuditSync.Digest(report) ||
                receipt.AcceptedAt == default)
                throw new CryptographicException("Audit-Empfangsbestätigung passt nicht zur Meldung.");
            // Keep a protected local copy; a lost reply is retried with exactly
            // the same signed report. No server acknowledgement, no archive.
            var archived = Path.Combine(directory, "Sent", Path.GetFileName(path));
            var archivedRevision = ConcurrentFileWriter.Capture(archived);
            if (!archivedRevision.Exists)
                ConcurrentFileWriter.Write(archived, FileRevision.Missing, temporary =>
                {
                    using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.Write(protectedBytes); stream.Flush(true);
                });
            else if (archivedRevision != expected)
                throw new ConcurrentFileChangeException(archived);
            ConcurrentFileWriter.Delete(path, expected);
            count++;
        }
        return count;
    }

    public sealed record LocalRow(KeyLibraryDisclosureEvent Entry, string PayloadSha256,
        bool Archived, bool SignatureVerified);
    public sealed record LocalReview(LocalRow[] Records, int InvalidCount, bool Truncated);

    /// <summary>Read only local signed reports, never decrypt a key Vault.
    /// A removed certificate may no longer be present in the current directory;
    /// such a report is explicitly marked unverified rather than discarded.</summary>
    public static LocalReview ReadLocal(string directory, ILocalGroupTrustProtection protection,
        VerifiedGroupPolicy policy, int limit = 1000)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        var rows = new List<LocalRow>();
        var invalid = 0;
        var examined = 0;
        foreach (var (folder, archived) in new[] { (directory, false), (Path.Combine(directory, "Sent"), true) })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var path in Directory.EnumerateFiles(folder, "*.signed-audit.bin"))
            {
                if (examined++ >= limit) return new(rows.ToArray(), invalid, true);
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length is <= 0 or > 64 * 1024) throw new InvalidDataException("Audit-Dateigröße.");
                    var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
                    var raw = protection.Unprotect(bytes);
                    try
                    {
                        var report = KeyLibrarySyncWire.Read<SignedKeyLibraryAudit>(raw, KeyLibraryAuditSync.MaximumBytes);
                        var entry = KeyLibraryAuditSync.Parse(report);
                        if (entry.LibraryId != policy.Policy.LibraryId ||
                            Path.GetFileName(path) != entry.EventId.ToString("N") + ".signed-audit.bin")
                            throw new InvalidDataException("Audit-Datei gehört nicht zur Bibliothek.");
                        var device = policy.Policy.People.SelectMany(p => p.Devices)
                            .SingleOrDefault(d => d.Fingerprint == entry.DeviceFingerprint);
                        if (device is not null)
                        {
                            using var cert = OfflineGroupDirectory.ReadPublicCertificate(device.Certificate);
                            _ = KeyLibraryAuditSync.Verify(report, cert);
                        }
                        rows.Add(new(entry, KeyLibraryAuditSync.Digest(report), archived, device is not null));
                    }
                    finally { CryptographicOperations.ZeroMemory(raw); }
                }
                catch (Exception ex) when (ex is IOException or CryptographicException or
                    System.Text.Json.JsonException or ArgumentException or FormatException or UnauthorizedAccessException)
                { invalid++; }
            }
        }
        return new(rows.ToArray(), invalid, false);
    }
}

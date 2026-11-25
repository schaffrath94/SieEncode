using System.Security.Cryptography;

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
}

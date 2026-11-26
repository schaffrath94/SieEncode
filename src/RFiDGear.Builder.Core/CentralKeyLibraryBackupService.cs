using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

public sealed record CentralKeyLibraryBackupReference(CentralKeyLibraryHead Head,
    string AdministratorFingerprint, string ManifestSha256);

public sealed record CentralKeyLibraryBackupResult(string Directory,
    CentralKeyLibraryBackupReference Reference, int AuditCount);

/// <summary>Cold backup of an existing headless server, and isolated restore.
/// Stop all server processes/writers before capturing. No network endpoint,
/// trust enrollment, new revision or activation is performed here.</summary>
public static class CentralKeyLibraryBackupService
{
    private const string Format = "SieEncode-Central-Cold-Backup";
    private const string StateName = "central-state.json";
    private const string ManifestName = "CENTRAL-BACKUP.json";
    private const int MaximumStateBytes = 32 * 1024 * 1024;
    private const int MaximumAuditBytes = 64 * 1024;
    private const int MaximumAuditRecords = 100000;
    private const int MaximumManifestBytes = 16 * 1024 * 1024;
    private sealed record AuditFile(string Id, string Sha256);
    private sealed record Manifest(string Format, int Version, CentralKeyLibraryHead Head,
        string AdministratorFingerprint, string StateSha256, AuditFile[] Audit);

    public static CentralKeyLibraryBackupResult CreateCold(string stoppedStatePath,
        string backupWorkspace, CentralKeyLibraryHead independentlyConfirmedHead,
        string independentlyConfirmedAdministratorFingerprint)
    {
        ArgumentNullException.ThrowIfNull(independentlyConfirmedHead);
        var source = Path.GetFullPath(stoppedStatePath);
        var center = new CentralKeyLibraryStore(source, independentlyConfirmedHead.LibraryId);
        center.VerifyBackupState(independentlyConfirmedHead, independentlyConfirmedAdministratorFingerprint);
        var sourceRevision = ConcurrentFileWriter.Capture(source);
        var originalAudit = CaptureAudit(source + ".audit");
        return Publish(backupWorkspace, "central-backup-", staging =>
        {
            var state = Path.Combine(staging, StateName);
            var hash = CopyFile(source, state, MaximumStateBytes);
            if (hash != sourceRevision.Sha256)
                throw new ConcurrentFileChangeException(source);
            var auditDirectory = state + ".audit";
            Directory.CreateDirectory(auditDirectory);
            foreach (var id in originalAudit)
                CopyFile(Path.Combine(source + ".audit", id + ".json"),
                    Path.Combine(auditDirectory, id + ".json"), MaximumAuditBytes);
            var manifest = Inspect(staging, independentlyConfirmedHead,
                independentlyConfirmedAdministratorFingerprint);
            // Detect ordinary concurrent changes, but this is NOT a replacement
            // for an externally enforced maintenance window.
            if (ConcurrentFileWriter.Capture(source) != sourceRevision ||
                !CaptureAudit(source + ".audit").SequenceEqual(originalAudit) ||
                manifest.Audit.Any(a => ConcurrentFileWriter.Capture(
                    Path.Combine(source + ".audit", a.Id + ".json")).Sha256 != a.Sha256))
                throw new ConcurrentFileChangeException(source);
            var bytes = KeyLibrarySyncWire.Write(manifest);
            if (bytes.Length > MaximumManifestBytes)
                throw new InvalidDataException("Zentrales Sicherungsverzeichnis ist zu groß.");
            WriteNew(Path.Combine(staging, ManifestName), bytes);
            var reference = new CentralKeyLibraryBackupReference(independentlyConfirmedHead,
                independentlyConfirmedAdministratorFingerprint, Hash(bytes));
            return (reference, manifest.Audit.Length);
        });
    }

    // A reference from the archive itself is not sufficient. A separately
    // confirmed CURRENT head is mandatory even if the old archive is authentic.
    public static CentralKeyLibraryBackupResult RestoreIsolated(string backupDirectory,
        string recoveryWorkspace, CentralKeyLibraryBackupReference independentlyConfirmed,
        CentralKeyLibraryHead independentlyConfirmedCurrentHead)
    {
        ArgumentNullException.ThrowIfNull(independentlyConfirmed);
        if (independentlyConfirmed.Head != independentlyConfirmedCurrentHead)
            throw new CryptographicException("Historischer Zentralstand darf nicht automatisch wieder in Betrieb gehen.");
        var source = Path.GetFullPath(backupDirectory);
        RequireDirectory(source);
        var bytes = ReadBounded(Path.Combine(source, ManifestName), MaximumManifestBytes);
        if (Hash(bytes) != independentlyConfirmed.ManifestSha256)
            throw new CryptographicException("Zentrales Sicherungsverzeichnis wurde verändert.");
        var manifest = KeyLibrarySyncWire.Read<Manifest>(bytes, MaximumManifestBytes);
        if (manifest.Format != Format || manifest.Version != 1 ||
            manifest.Head != independentlyConfirmed.Head ||
            manifest.AdministratorFingerprint != independentlyConfirmed.AdministratorFingerprint ||
            manifest.Audit is null || manifest.Audit.Length > MaximumAuditRecords ||
            manifest.Audit.Any(a => a is null || !KeyLibraryAuditReview.IsCursor(a.Id)))
            throw new InvalidDataException("Ungültiges zentrales Sicherungsverzeichnis.");
        RequireManifest(Inspect(source, manifest.Head, manifest.AdministratorFingerprint), manifest);
        return Publish(recoveryWorkspace, "central-recovery-not-activated-", staging =>
        {
            var state = Path.Combine(staging, StateName);
            if (CopyFile(Path.Combine(source, StateName), state, MaximumStateBytes) != manifest.StateSha256)
                throw new CryptographicException("Zentraler Sicherungsbestand wurde während der Wiederherstellung verändert.");
            Directory.CreateDirectory(state + ".audit");
            foreach (var audit in manifest.Audit)
                if (CopyFile(Path.Combine(source, StateName + ".audit", audit.Id + ".json"),
                    Path.Combine(state + ".audit", audit.Id + ".json"), MaximumAuditBytes) != audit.Sha256)
                    throw new CryptographicException("Audit-Sicherung wurde während der Wiederherstellung verändert.");
            RequireManifest(Inspect(staging, manifest.Head, manifest.AdministratorFingerprint), manifest);
            WriteNew(Path.Combine(staging, ManifestName), bytes);
            WriteNew(Path.Combine(staging, "NOT-ACTIVATED.json"), KeyLibrarySyncWire.Write(new
            {
                Status = "CentralColdRecoveryNotActivated",
                Reference = independentlyConfirmed,
                AuditCount = manifest.Audit.Length
            }));
            return (independentlyConfirmed, manifest.Audit.Length);
        });
    }

    private static Manifest Inspect(string root, CentralKeyLibraryHead head, string administrator)
    {
        RequireDirectory(root);
        var state = Path.Combine(root, StateName);
        RequireRegularFile(state);
        var revision = ConcurrentFileWriter.Capture(state);
        var center = new CentralKeyLibraryStore(state, head.LibraryId);
        center.VerifyBackupState(head, administrator);
        var audits = CaptureAudit(state + ".audit").Select(id =>
        {
            var bytes = ReadBounded(Path.Combine(state + ".audit", id + ".json"), MaximumAuditBytes);
            var record = KeyLibrarySyncWire.Read<AcceptedKeyLibraryAudit>(bytes, MaximumAuditBytes);
            var entry = KeyLibraryAuditReview.ValidateRecord(record, head.LibraryId);
            if (entry.EventId.ToString("N") != id || entry.GroupId != head.GroupId ||
                record.PolicyAtAcceptance.Revision > head.Policy.Revision ||
                record.PolicyAtAcceptance.Revision == head.Policy.Revision && record.PolicyAtAcceptance != head.Policy)
                throw new CryptographicException("Audit-Datensatz passt nicht zum zentralen Sicherungsstand.");
            return new AuditFile(id, Hash(bytes));
        }).ToArray();
        if (ConcurrentFileWriter.Capture(state) != revision)
            throw new ConcurrentFileChangeException(state);
        return new(Format, 1, head, administrator, revision.Sha256, audits);
    }

    private static void RequireManifest(Manifest actual, Manifest expected)
    {
        if (actual.Head != expected.Head || actual.AdministratorFingerprint != expected.AdministratorFingerprint ||
            actual.StateSha256 != expected.StateSha256 || !actual.Audit.SequenceEqual(expected.Audit))
            throw new CryptographicException("Zentrale Sicherung oder Audit-Bestand ist unvollständig oder verändert.");
    }

    private static string[] CaptureAudit(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        RequireDirectory(directory);
        var ids = new List<string>();
        // Persistent writer lock files are not audit records. Reject unknown JSON
        // names rather than silently dropping a potential accepted event.
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!KeyLibraryAuditReview.IsCursor(id) || ids.Count >= MaximumAuditRecords)
                throw new InvalidDataException("Audit-Dateiname oder Anzahl ungültig.");
            RequireRegularFile(file);
            ids.Add(id);
        }
        return ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static CentralKeyLibraryBackupResult Publish(string workspace, string prefix,
        Func<string, (CentralKeyLibraryBackupReference Reference, int Count)> prepare)
    {
        var root = Path.GetFullPath(workspace);
        RequireDirectory(root);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, ".central-pending-" + id);
        var destination = Path.Combine(root, prefix + id);
        Directory.CreateDirectory(staging);
        try
        {
            var result = prepare(staging);
            Directory.Move(staging, destination);
            return new(destination, result.Reference, result.Count);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void RequireDirectory(string path)
    {
        if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Geschützter, nicht umgeleiteter Sicherungsordner erforderlich.");
    }

    private static void RequireRegularFile(string path)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reguläre Sicherungsdatei erforderlich.");
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        RequireRegularFile(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximum)
            throw new InvalidDataException("Zentrale Sicherungsdatei hat ungültige Größe.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string CopyFile(string source, string target, int maximum)
    {
        var bytes = ReadBounded(source, maximum);
        WriteNew(target, bytes);
        var hash = Hash(bytes);
        if (Hash(ReadBounded(target, maximum)) != hash)
            throw new CryptographicException("Kopierte zentrale Sicherungsdatei stimmt nicht überein.");
        return hash;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

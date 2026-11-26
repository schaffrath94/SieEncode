using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

/// <summary>Cooperative exclusive volume session for one service or one
/// maintenance process. The lease lives outside the replaceable live directory.
/// A durable library marker forbids re-bootstrap after loss of live state.
/// This is not a distributed fencing token: the host must prove lock behavior
/// and stop every old writer before maintenance, including on other volumes.</summary>
public sealed class CentralKeyLibraryVolumeSession : IDisposable
{
    private const string MarkerFormat = "SieEncode-Central-Volume-v1";
    private sealed record VolumeMarker(string Format, Guid LibraryId);
    private readonly string root;
    private readonly Guid libraryId;
    private readonly FileStream lease;
    private bool disposed;

    private string MarkerPath => Path.Combine(root, "CENTRAL-VOLUME.json");
    public string StatePath => Path.Combine(root, "live", "central-state.json");

    private CentralKeyLibraryVolumeSession(string root, Guid libraryId, FileStream lease)
    {
        this.root = root;
        this.libraryId = libraryId;
        this.lease = lease;
    }

    public static CentralKeyLibraryVolumeSession Acquire(string volumeRoot, Guid libraryId)
    {
        if (libraryId == Guid.Empty) throw new InvalidDataException("Bibliotheks-ID fehlt.");
        var root = Path.GetFullPath(volumeRoot);
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Vorhandenes, nicht umgeleitetes Zentralvolume erforderlich.");
        var lockPath = Path.Combine(root, ".central-volume.lock");
        RequireRegularIfPresent(lockPath);
        var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return new(root, libraryId, lease);
    }

    public CentralKeyLibraryStore OpenExisting()
    {
        EnsureActive();
        RequireMarker();
        return CentralKeyLibraryStartup.OpenExisting(StatePath, libraryId);
    }

    public void Bootstrap(GroupEnrollmentPackage enrollment, string confirmedAdministratorFingerprint,
        bool explicitlyConfirmedFirstInstallation)
    {
        EnsureActive();
        if (!explicitlyConfirmedFirstInstallation)
            throw new InvalidOperationException("Erstinstallation muss ausdrücklich bestätigt werden.");
        RequireRegularIfPresent(MarkerPath);
        if (File.Exists(MarkerPath))
            throw new InvalidOperationException("Volume wurde bereits provisioniert oder für eine Rekonstruktion reserviert. Kein erneuter Bootstrap.");
        RequireAbsentLive();
        var copy = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(enrollment));
        var verified = GroupEnrollmentPackages.Validate(copy);
        if (copy.LibraryId != libraryId || verified.Policy.Version != 2 ||
            OfflineGroupTrustStore.AdministratorFingerprint(copy.AdministratorPublicKey) != confirmedAdministratorFingerprint)
            throw new CryptographicException("Unabhängig bestätigte Hauptverwaltung und Bibliotheks-ID erforderlich.");
        // Reserve before creating live state. An interrupted bootstrap must
        // never be retried as if the volume were genuinely new.
        WriteMarker();
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        using var stateLease = CentralKeyLibraryStartup.AcquireLease(StatePath);
        _ = CentralKeyLibraryStartup.BootstrapNew(StatePath, libraryId, copy, confirmedAdministratorFingerprint);
        _ = OpenExisting();
    }

    public void Restore(string isolatedDirectory, SignedCentralKeyLibraryActivation authorization,
        string confirmedAdministratorFingerprint, string tlsCertificateSha256, DateTimeOffset now)
    {
        EnsureActive();
        RequireAbsentLive();
        ArgumentNullException.ThrowIfNull(authorization);
        if (authorization.Payload is not { Length: > 0 and <= CentralKeyLibraryActivation.MaximumBytes } ||
            authorization.Signature is not { Length: >= 384 and <= 2048 })
            throw new InvalidDataException("Ungültige Wiederherstellungsfreigabe.");
        var captured = new SignedCentralKeyLibraryActivation(authorization.Payload.ToArray(), authorization.Signature.ToArray());
        var claim = CentralKeyLibraryActivation.ReadClaim(captured);
        if (claim.Reference.Head.LibraryId != libraryId)
            throw new CryptographicException("Wiederherstellungsauftrag gehört zu einer anderen Bibliothek.");
        // Verify the exact destination and signed grant before reserving a new
        // volume. ActivateRecovered independently repeats archive validation.
        var source = Path.GetFullPath(isolatedDirectory);
        var center = new CentralKeyLibraryStore(Path.Combine(source, "central-state.json"), libraryId);
        _ = CentralKeyLibraryActivation.Verify(captured, center.ReadBackupEnrollment(),
            confirmedAdministratorFingerprint, StatePath, tlsCertificateSha256, now);
        _ = CentralKeyLibraryBackupService.VerifyCold(source, claim.Reference);
        RequireRegularIfPresent(MarkerPath);
        if (File.Exists(MarkerPath)) RequireMarker();
        else WriteMarker();
        CentralKeyLibraryBackupService.ActivateRecovered(source, StatePath, captured,
            confirmedAdministratorFingerprint, tlsCertificateSha256, now);
        using var stateLease = CentralKeyLibraryStartup.AcquireLease(StatePath);
        if (OpenExisting().ReadHead() != claim.Reference.Head)
            throw new CryptographicException("Wiederhergestellter Zentralstand ist nicht exakt bestätigt.");
    }

    /// <summary>Cold export to a fixed protected staging workspace. No TLS
    /// identity needed. The caller must fence old writers externally; this
    /// same-volume copy is NOT an independent disaster backup.</summary>
    public CentralKeyLibraryBackupResult CreateColdBackup(CentralKeyLibraryHead independentlyConfirmedHead,
        string independentlyConfirmedAdministratorFingerprint)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(independentlyConfirmedHead);
        if (independentlyConfirmedHead.LibraryId != libraryId)
            throw new CryptographicException("Sicherungsauftrag gehört zu einer anderen Bibliothek.");
        var store = OpenExisting();
        var workspace = Path.Combine(root, "backups");
        if (!Directory.Exists(workspace) || (File.GetAttributes(workspace) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Vorbereiteter geschützter Sicherungsordner fehlt.");
        using var stateLease = CentralKeyLibraryStartup.AcquireLease(StatePath);
        store.VerifyBackupState(independentlyConfirmedHead, independentlyConfirmedAdministratorFingerprint);
        var backup = CentralKeyLibraryBackupService.CreateCold(StatePath, workspace,
            independentlyConfirmedHead, independentlyConfirmedAdministratorFingerprint);
        if (CentralKeyLibraryBackupService.VerifyCold(backup.Directory, backup.Reference) != backup.AuditCount)
            throw new CryptographicException("Zentrale Sicherung konnte nicht vollständig zurückgeprüft werden.");
        // The sibling reference is only a staging receipt. Export it to a
        // separately controlled location before relying on the backup.
        var receipt = backup.Directory + ".reference.json";
        var bytes = KeyLibrarySyncWire.Write(backup.Reference);
        ConcurrentFileWriter.Write(receipt, FileRevision.Missing, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        });
        store.VerifyBackupState(independentlyConfirmedHead, independentlyConfirmedAdministratorFingerprint);
        return backup;
    }

    private void RequireAbsentLive()
    {
        var live = Path.GetDirectoryName(StatePath)!;
        if (Directory.Exists(live) || File.Exists(live))
            throw new IOException("Bestehendes Live-Verzeichnis bleibt unverändert. Kein Überschreiben.");
    }

    private void RequireMarker()
    {
        RequireRegularIfPresent(MarkerPath);
        using var stream = new FileStream(MarkerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 4096)
            throw new InvalidDataException("Zentralvolume-Marker ist ungültig.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        var marker = KeyLibrarySyncWire.Read<VolumeMarker>(bytes, 4096);
        if (marker.Format != MarkerFormat || marker.LibraryId != libraryId)
            throw new CryptographicException("Zentralvolume gehört zu einer anderen Bibliothek.");
    }

    private void WriteMarker()
    {
        var bytes = KeyLibrarySyncWire.Write(new VolumeMarker(MarkerFormat, libraryId));
        ConcurrentFileWriter.Write(MarkerPath, FileRevision.Missing, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        });
        RequireMarker();
    }

    private static void RequireRegularIfPresent(string path)
    {
        if (Directory.Exists(path) || File.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reguläre Volume-Datei erforderlich.");
    }

    private void EnsureActive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(CentralKeyLibraryVolumeSession));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lease.Dispose();
    }
}

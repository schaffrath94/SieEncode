using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

// Store this reference independently of the backup. A checksum supplied by the
// backup itself cannot establish freshness or independently confirm its authority.
public sealed record KeyLibraryBackupReference(Guid LibraryId, string GroupId,
    string AdministratorFingerprint, GroupPolicyCheckpoint Policy, long CatalogRevision,
    string ArchiveSha256);

public sealed record KeyLibraryBackupVerification(Guid LibraryId, long CatalogRevision,
    int CustomerCount, int KeyVersionCount);

// These files are evidence for a recovery exercise, not an installed library.
public sealed record KeyLibraryIsolatedRestore(string Directory, string EncryptedVaultPath,
    string PublicEnrollmentPath, KeyLibraryBackupVerification Verification);

/// <summary>Encrypted scoped-library backup and offline recovery verification.
/// Does not install a vault, enroll trust, change a cursor or authorize card writes.</summary>
public static class KeyLibraryBackupService
{
    private const string Format = "SieEncode-Scoped-Library-Backup";
    private sealed record Archive(string Format, int Version, byte[] Enrollment, byte[] EncryptedVault);

    public static KeyLibraryBackupReference Create(string sourcePath, string backupPath,
        OfflineGroupTrustStore trust, string groupId, X509Certificate2 fullMember, DateTimeOffset now)
    {
        var enrolled = trust.ExportPublic();
        var verified = GroupEnrollmentPackages.Validate(enrolled);
        if (!verified.Policy.CustomerScopesEnabled)
            throw new CryptographicException("Bibliothekssicherung benötigt eine signierte Kundenbibliothek.");
        var encrypted = KeyLibraryVaultStore.ReadBytes(sourcePath);
        var sourceRevision = new FileRevision(true, Hash(encrypted));
        var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(encrypted, GroupVaultService.MaximumBytes);
        var opened = ScopedKeyLibraryVault.OpenVerified(signed, enrolled, groupId, fullMember, now, requireFull: true);
        var bytes = KeyLibrarySyncWire.Write(new Archive(Format, 1, GroupEnrollmentPackages.Write(enrolled), encrypted));
        if (bytes.Length > KeyLibrarySyncWire.MaximumEncryptedBytes)
            throw new InvalidDataException("Bibliothekssicherung ist zu groß.");
        var reference = new KeyLibraryBackupReference(enrolled.LibraryId, groupId,
            OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey),
            verified.Checkpoint, opened.Catalog.Revision, Hash(bytes));
        void CheckSource()
        {
            if (trust.LoadOffline().Checkpoint != verified.Checkpoint ||
                ConcurrentFileWriter.Capture(sourcePath) != sourceRevision)
                throw new ConcurrentFileChangeException(Path.GetFullPath(sourcePath));
        }
        CheckSource();
        ConcurrentFileWriter.Write(backupPath, FileRevision.Missing, temporary =>
        {
            CheckSource();
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
            CheckSource();
        });
        if (Hash(ReadArchive(backupPath)) != reference.ArchiveSha256)
            throw new CryptographicException("Gesicherte Bibliothek stimmt nicht mit der Quelle überein.");
        CheckSource();
        return reference;
    }

    // No existing local trust or member certificate is needed: use a separately
    // recorded exact archive reference and a separately stored recovery identity.
    public static KeyLibraryBackupVerification VerifyRecovery(string backupPath,
        KeyLibraryBackupReference independentlyConfirmed, X509Certificate2 recovery, DateTimeOffset now)
    {
        return ReadVerified(backupPath, independentlyConfirmed, recovery, now).Verification;
    }

    /// <summary>Independently decrypt the archived customer library and bind
    /// it to the exact ciphertext of the separately confirmed central head.
    /// A matching catalog revision alone is insufficient.</summary>
    public static KeyLibraryBackupVerification VerifyRecoveryAgainstHead(string backupPath,
        KeyLibraryBackupReference independentlyConfirmed, CentralKeyLibraryHead independentlyConfirmedHead,
        string independentlyConfirmedAdministratorFingerprint, X509Certificate2 recovery, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(independentlyConfirmed);
        ArgumentNullException.ThrowIfNull(independentlyConfirmedHead);
        if (independentlyConfirmedHead.CentralRevision <= 0 ||
            independentlyConfirmed.LibraryId != independentlyConfirmedHead.LibraryId ||
            independentlyConfirmed.GroupId != independentlyConfirmedHead.GroupId ||
            independentlyConfirmed.Policy != independentlyConfirmedHead.Policy ||
            independentlyConfirmed.CatalogRevision != independentlyConfirmedHead.CatalogRevision ||
            independentlyConfirmed.AdministratorFingerprint != independentlyConfirmedAdministratorFingerprint)
            throw new CryptographicException("Bibliothekssicherung entspricht nicht dem unabhängig bestätigten Zentralstand.");
        var (archive, verification) = ReadVerified(backupPath, independentlyConfirmed, recovery, now);
        if (KeyLibrarySyncTransport.CiphertextSha256(archive.EncryptedVault) != independentlyConfirmedHead.CiphertextSha256)
            throw new CryptographicException("Bibliothekssicherung besitzt nicht den exakten zentral bestätigten Ciphertext.");
        return verification;
    }

    /// <summary>Reconstruct the exact encrypted vault and public registration in
    /// a new, non-operational directory. Never enroll or replace a live store.
    /// The caller selects an existing, access-controlled recovery workspace.</summary>
    public static KeyLibraryIsolatedRestore RestoreIsolated(string backupPath,
        KeyLibraryBackupReference independentlyConfirmed, X509Certificate2 recovery,
        DateTimeOffset now, string recoveryWorkspace)
    {
        var (archive, verification) = ReadVerified(backupPath, independentlyConfirmed, recovery, now);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryWorkspace);
        var root = Path.GetFullPath(recoveryWorkspace);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Separater Wiederherstellungsordner fehlt.");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Wiederherstellungsordner darf keine direkte Umleitung sein.");

        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, ".recovery-pending-" + id);
        var destination = Path.Combine(root, "recovery-not-activated-" + id);
        const string vaultName = "recovered-encrypted-library.json";
        const string enrollmentName = "recovered-public-enrollment.json";
        const string noteName = "NOT-ACTIVATED.json";
        Directory.CreateDirectory(staging);
        try
        {
            var vaultPath = Path.Combine(staging, vaultName);
            var enrollmentPath = Path.Combine(staging, enrollmentName);
            WriteNew(vaultPath, archive.EncryptedVault);
            WriteNew(enrollmentPath, archive.Enrollment);
            var vaultBytes = KeyLibraryVaultStore.ReadBytes(vaultPath);
            var enrollmentBytes = ReadBounded(enrollmentPath, GroupEnrollmentPackages.MaximumBytes);
            if (!vaultBytes.AsSpan().SequenceEqual(archive.EncryptedVault) ||
                !enrollmentBytes.AsSpan().SequenceEqual(archive.Enrollment))
                throw new CryptographicException("Rekonstruierte Dateien stimmen nicht mit der Sicherung überein.");
            // Reopen the persisted files with the independently pinned recovery
            // context. Never use a normal member identity or live trust store.
            var diskVerification = VerifyArchive(new Archive(Format, 1, enrollmentBytes, vaultBytes),
                independentlyConfirmed, recovery, now);
            if (diskVerification != verification)
                throw new CryptographicException("Wiederhergestellter Bibliotheksstand stimmt nicht überein.");
            WriteNew(Path.Combine(staging, noteName), KeyLibrarySyncWire.Write(new
            {
                Status = "IsolatedNotActivated",
                Reference = independentlyConfirmed,
                Verification = diskVerification,
                VerifiedAt = now
            }));
            // Publish only the verified directory. No overwrite, no active
            // filenames, no registration or central cursor writes.
            Directory.Move(staging, destination);
            return new(destination, Path.Combine(destination, vaultName),
                Path.Combine(destination, enrollmentName), diskVerification);
        }
        finally
        {
            // The staging directory never contains plaintext secrets.
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static (Archive Archive, KeyLibraryBackupVerification Verification) ReadVerified(
        string backupPath, KeyLibraryBackupReference independentlyConfirmed,
        X509Certificate2 recovery, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(independentlyConfirmed);
        var bytes = ReadArchive(backupPath);
        if (Hash(bytes) != independentlyConfirmed.ArchiveSha256)
            throw new CryptographicException("Bibliothekssicherung stimmt nicht mit dem unabhängig bestätigten Stand überein.");
        var archive = KeyLibrarySyncWire.Read<Archive>(bytes, KeyLibrarySyncWire.MaximumEncryptedBytes);
        return (archive, VerifyArchive(archive, independentlyConfirmed, recovery, now));
    }

    private static KeyLibraryBackupVerification VerifyArchive(Archive archive,
        KeyLibraryBackupReference independentlyConfirmed, X509Certificate2 recovery, DateTimeOffset now)
    {
        if (archive.Format != Format || archive.Version != 1 || archive.Enrollment is null ||
            archive.EncryptedVault is null || archive.EncryptedVault.Length > GroupVaultService.MaximumBytes)
            throw new InvalidDataException("Ungültiges Bibliothekssicherungsformat.");
        var enrolled = GroupEnrollmentPackages.Read(archive.Enrollment);
        var verified = GroupEnrollmentPackages.Validate(enrolled);
        if (!verified.Policy.CustomerScopesEnabled || enrolled.LibraryId != independentlyConfirmed.LibraryId ||
            verified.Checkpoint != independentlyConfirmed.Policy ||
            OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey) != independentlyConfirmed.AdministratorFingerprint)
            throw new CryptographicException("Bibliothekssicherung gehört nicht zur bestätigten Verwaltung.");
        var signed = KeyLibrarySyncWire.Read<SignedScopedKeyLibraryBundle>(archive.EncryptedVault, GroupVaultService.MaximumBytes);
        var restored = ScopedKeyLibraryVault.OpenVerified(signed, enrolled, independentlyConfirmed.GroupId,
            recovery, now, requireFull: true, recovery: true);
        if (restored.Catalog.Revision != independentlyConfirmed.CatalogRevision)
            throw new CryptographicException("Bibliotheksrevision stimmt nicht mit dem bestätigten Sicherungsstand überein.");
        return new(restored.Catalog.LibraryId, restored.Catalog.Revision,
            restored.Catalog.Customers.Length, restored.Catalog.Keys.Length);
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] ReadArchive(string path) =>
        ReadBounded(path, KeyLibrarySyncWire.MaximumEncryptedBytes);

    private static byte[] ReadBounded(string path, int maximumBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximumBytes)
            throw new InvalidDataException("Bibliothekssicherung ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

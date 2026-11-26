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
        ArgumentNullException.ThrowIfNull(independentlyConfirmed);
        var bytes = ReadArchive(backupPath);
        if (Hash(bytes) != independentlyConfirmed.ArchiveSha256)
            throw new CryptographicException("Bibliothekssicherung stimmt nicht mit dem unabhängig bestätigten Stand überein.");
        var archive = KeyLibrarySyncWire.Read<Archive>(bytes, KeyLibrarySyncWire.MaximumEncryptedBytes);
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

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] ReadArchive(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > KeyLibrarySyncWire.MaximumEncryptedBytes)
            throw new InvalidDataException("Bibliothekssicherung ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>Evidence for a recovery exercise, not authorization to start a
/// service. The independently supplied head is not proven fresh by this result.</summary>
public sealed record KeyLibraryRecoveryPairVerification(CentralKeyLibraryHead ConfirmedHead,
    int CustomerCount, int KeyVersionCount, int ArchivedAuditCount, DateTimeOffset VerifiedAt);

/// <summary>Verify the two complementary recovery artifacts together:
/// the exact central state (including audit and replay records) and an
/// independently decryptable complete customer library. No output files,
/// network calls, trust registration, cursor reset or service activation.</summary>
public static class KeyLibraryRecoveryReadiness
{
    public static KeyLibraryRecoveryPairVerification VerifyCurrentPair(
        string centralBackupDirectory, CentralKeyLibraryBackupReference centralReference,
        string libraryBackupPath, KeyLibraryBackupReference libraryReference,
        CentralKeyLibraryHead independentlyConfirmedCurrentHead,
        string independentlyConfirmedAdministratorFingerprint,
        X509Certificate2 recovery, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(centralReference);
        ArgumentNullException.ThrowIfNull(libraryReference);
        ArgumentNullException.ThrowIfNull(independentlyConfirmedCurrentHead);
        ArgumentNullException.ThrowIfNull(recovery);
        if (centralReference.Head != independentlyConfirmedCurrentHead ||
            centralReference.AdministratorFingerprint != independentlyConfirmedAdministratorFingerprint ||
            libraryReference.AdministratorFingerprint != independentlyConfirmedAdministratorFingerprint)
            throw new CryptographicException("Sicherungen gehören nicht zum unabhängig bestätigten aktuellen Verwaltungsstand.");

        var initial = ConcurrentFileWriter.Capture(libraryBackupPath);
        if (!initial.Exists || initial.Sha256 != libraryReference.ArchiveSha256)
            throw new CryptographicException("Unabhängig bestätigte Bibliothekssicherung fehlt oder wurde verändert.");
        var library = KeyLibraryBackupService.VerifyRecoveryAgainstHead(libraryBackupPath, libraryReference,
            independentlyConfirmedCurrentHead, independentlyConfirmedAdministratorFingerprint, recovery, now);
        var auditCount = CentralKeyLibraryBackupService.VerifyCold(centralBackupDirectory, centralReference);
        if (ConcurrentFileWriter.Capture(libraryBackupPath) != initial)
            throw new ConcurrentFileChangeException(libraryBackupPath);
        return new(independentlyConfirmedCurrentHead, library.CustomerCount, library.KeyVersionCount, auditCount, now);
    }
}

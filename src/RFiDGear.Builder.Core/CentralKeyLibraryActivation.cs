using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

public sealed record CentralKeyLibraryActivationClaim(string Format, Guid RequestId,
    CentralKeyLibraryBackupReference Reference, string TargetStatePath,
    string TlsCertificateSha256, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
public sealed record SignedCentralKeyLibraryActivation(byte[] Payload, byte[] Signature);

/// <summary>One explicit main-administration authorization for a cold server
/// recovery. Signing attests that the former server was fenced externally.
/// No distributed fencing or proof of freshness is created by this signature.</summary>
public static class CentralKeyLibraryActivation
{
    public const string Format = "SieEncode-Central-Recovery-Activation-v1";
    public const int MaximumBytes = 32 * 1024;

    public static SignedCentralKeyLibraryActivation Sign(CentralKeyLibraryActivationClaim claim, RSA authority)
    {
        Validate(claim);
        if (authority.KeySize < 3072) throw new CryptographicException("Hauptverwaltungsschlüssel erforderlich.");
        var payload = KeyLibrarySyncWire.Write(claim);
        return new(payload, authority.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    internal static CentralKeyLibraryActivationClaim ReadClaim(SignedCentralKeyLibraryActivation signed)
    {
        ArgumentNullException.ThrowIfNull(signed);
        var claim = KeyLibrarySyncWire.Read<CentralKeyLibraryActivationClaim>(signed.Payload, MaximumBytes);
        Validate(claim);
        if (!signed.Payload.SequenceEqual(KeyLibrarySyncWire.Write(claim)) ||
            signed.Signature is not { Length: >= 384 and <= 2048 })
            throw new CryptographicException("Ungültiger Aktivierungsauftrag.");
        return claim;
    }

    internal static CentralKeyLibraryActivationClaim Verify(SignedCentralKeyLibraryActivation signed,
        GroupEnrollmentPackage enrolled, string independentlyConfirmedAdministratorFingerprint,
        string targetStatePath, string tlsCertificateSha256, DateTimeOffset now)
    {
        var claim = ReadClaim(signed);
        var policy = GroupEnrollmentPackages.Validate(enrolled);
        using var authority = OfflineGroupDelegationService.OpenPublicKey(enrolled.AdministratorPublicKey);
        if (claim.Reference.Head.LibraryId != enrolled.LibraryId ||
            claim.Reference.Head.Policy != policy.Checkpoint ||
            claim.Reference.AdministratorFingerprint != independentlyConfirmedAdministratorFingerprint ||
            OfflineGroupTrustStore.AdministratorFingerprint(enrolled.AdministratorPublicKey) != independentlyConfirmedAdministratorFingerprint ||
            claim.TargetStatePath != targetStatePath || claim.TlsCertificateSha256 != tlsCertificateSha256 ||
            now < claim.IssuedAt.AddMinutes(-2) || now >= claim.ExpiresAt ||
            signed.Signature.Length != authority.KeySize / 8 ||
            !authority.VerifyData(signed.Payload, signed.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Aktivierungsauftrag passt nicht zur bestätigten Verwaltung, zum Ziel oder zum aktuellen Zeitfenster.");
        return claim;
    }

    private static void Validate(CentralKeyLibraryActivationClaim claim)
    {
        if (claim.Format != Format || claim.RequestId == Guid.Empty || claim.Reference is null ||
            claim.Reference.Head is null || claim.Reference.Head.LibraryId == Guid.Empty ||
            claim.Reference.Head.CentralRevision <= 0 || claim.Reference.Head.CatalogRevision <= 0 ||
            claim.Reference.Head.Policy is null || !IsHash(claim.Reference.Head.CiphertextSha256) ||
            !IsHash(claim.Reference.AdministratorFingerprint) || !IsHash(claim.Reference.ManifestSha256) ||
            !IsHash(claim.TlsCertificateSha256) ||
            string.IsNullOrWhiteSpace(claim.TargetStatePath) || claim.TargetStatePath.Length > 4096 ||
            claim.IssuedAt == default || claim.ExpiresAt <= claim.IssuedAt ||
            claim.ExpiresAt - claim.IssuedAt > TimeSpan.FromMinutes(30))
            throw new InvalidDataException("Ungültige Freigabe zur zentralen Wiederinbetriebnahme.");
    }

    private static bool IsHash(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

/// <summary>Local server startup boundary. The lease only serializes this
/// exact state path on the same filesystem, not different machines.</summary>
public static class CentralKeyLibraryStartup
{
    public static void RequireOperationalPath(string statePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(statePath))!;
        if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Geschütztes Dienstverzeichnis fehlt oder ist umgeleitet.");
        var name = Path.GetFileName(directory);
        if (name.StartsWith("central-backup-", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("central-recovery-not-activated-", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(".central-pending-", StringComparison.OrdinalIgnoreCase) ||
            File.Exists(Path.Combine(directory, "CENTRAL-BACKUP.json")) ||
            File.Exists(Path.Combine(directory, "NOT-ACTIVATED.json")))
            throw new InvalidOperationException("Sicherungs- oder Prüfverzeichnis darf nicht als produktive Zentrale starten.");
        if (File.Exists(statePath) && (File.GetAttributes(statePath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Zentraler Zustand darf keine direkte Umleitung sein.");
    }

    public static IDisposable AcquireLease(string statePath)
    {
        RequireOperationalPath(statePath);
        return new FileStream(Path.GetFullPath(statePath) + ".server-instance.lock",
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public static CentralKeyLibraryStore OpenExisting(string statePath, Guid libraryId)
    {
        RequireOperationalPath(statePath);
        if (!File.Exists(statePath))
            throw new FileNotFoundException("Zentraler Zustand fehlt. Kein automatischer Bootstrap.", statePath);
        var store = new CentralKeyLibraryStore(statePath, libraryId);
        _ = store.ReadHead();
        return store;
    }

    public static CentralKeyLibraryStore BootstrapNew(string statePath, Guid libraryId,
        GroupEnrollmentPackage enrollment, string confirmedFingerprint)
    {
        RequireOperationalPath(statePath);
        if (File.Exists(statePath) || Directory.Exists(statePath + ".audit") ||
            File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(statePath))!, "CENTRAL-ACTIVATION.json")))
            throw new IOException("Erstinstallation ist nur ohne bestehenden Zustand, Audit oder Wiederherstellungsfreigabe zulässig.");
        var store = new CentralKeyLibraryStore(statePath, libraryId);
        store.Register(enrollment, confirmedFingerprint);
        return store;
    }
}

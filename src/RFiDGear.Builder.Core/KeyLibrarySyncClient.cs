using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Integration bridge for the existing local encrypted Vault and the headless
/// central transfer protocol. No UI, no HTTP, no second secret database.
/// </summary>
public static class KeyLibrarySyncClient
{
    public static SignedKeyLibrarySync PrepareUpload(string localVaultPath,
        OfflineGroupTrustStore trust, OfflineGroupAdministrationStore administration,
        string groupId, CentralKeyLibraryHead remoteHead,
        IEnumerable<X509Certificate2> ownIdentities)
    {
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(remoteHead);
        var path = Path.GetFullPath(localVaultPath);
        var expected = ConcurrentFileWriter.Capture(path);
        if (!expected.Exists)
            throw new FileNotFoundException("Verschlüsselter lokaler Schlüsselbestand fehlt.", path);
        var encrypted = ReadVaultBytes(path);
        if (ConcurrentFileWriter.Capture(path) != expected ||
            expected.Sha256 != KeyLibrarySyncTransport.CiphertextSha256(encrypted))
            throw new ConcurrentFileChangeException(path);
        var localPolicy = trust.LoadOffline();
        if (localPolicy.Policy.LibraryId != remoteHead.LibraryId ||
            localPolicy.Checkpoint != remoteHead.Policy)
            throw new CryptographicException("Lokale und zentral signierte Gruppenstände passen nicht zusammen.");

        // Successful authenticated local decryption is a prerequisite for upload.
        // This is deliberately the same library decoder as the project editor.
        var library = KeyLibraryVault.Unprotect(GroupVaultService.Read(encrypted),
            localPolicy, groupId, ownIdentities);
        if (library.Catalog.LibraryId != remoteHead.LibraryId)
            throw new CryptographicException("Lokaler Bibliotheksinhalt hat eine falsche Identität.");
        var signed = administration.SignEncryptedCentralSync(trust, groupId,
            remoteHead, encrypted);
        if (ConcurrentFileWriter.Capture(path) != expected)
            throw new ConcurrentFileChangeException(path);
        return signed;
    }

    /// <summary>
    /// Install only a server-verified signed encrypted snapshot. Authenticated
    /// remote head retrieval is mandatory at the future HTTPS transport layer.
    /// The logical versioned key history is checked before replacing local data.
    /// </summary>
    public static FileRevision InstallDownloadedSnapshot(string localVaultPath,
        OfflineGroupTrustStore trust, string groupId, CentralKeyLibraryHead verifiedRemoteHead,
        SignedKeyLibrarySync transfer, IEnumerable<X509Certificate2> ownIdentities,
        FileRevision expectedLocalFile)
    {
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(verifiedRemoteHead);
        ArgumentNullException.ThrowIfNull(expectedLocalFile);
        var registered = trust.ExportPublic();
        var claim = KeyLibrarySyncTransport.Verify(transfer, registered);
        var policy = trust.LoadOffline();
        if (verifiedRemoteHead.LibraryId != registered.LibraryId ||
            verifiedRemoteHead.Policy != policy.Checkpoint ||
            claim.Policy != policy.Checkpoint ||
            claim.GroupId != groupId ||
            claim.NextCiphertextSha256 != verifiedRemoteHead.CiphertextSha256 ||
            claim.ExpectedCentralRevision + 1 != verifiedRemoteHead.CentralRevision ||
            verifiedRemoteHead.GroupId != groupId)
            throw new CryptographicException("Heruntergeladener Schlüsselstand stimmt nicht mit der bestätigten zentralen Revision überein.");

        var next = KeyLibraryVault.Unprotect(GroupVaultService.Read(transfer.EncryptedVault),
            policy, groupId, ownIdentities);
        var path = Path.GetFullPath(localVaultPath);
        if (ConcurrentFileWriter.Capture(path) != expectedLocalFile)
            throw new ConcurrentFileChangeException(path);
        if (expectedLocalFile.Exists)
        {
            var previous = KeyLibraryVault.Unprotect(
                GroupVaultService.Read(ReadVaultBytes(path)), policy, groupId,
                ownIdentities);
            if (next.Catalog.LibraryId != previous.Catalog.LibraryId ||
                next.Catalog.Revision <= previous.Catalog.Revision)
                throw new InvalidDataException("Die zentrale Bibliothek ist nicht neuer als die bereits vorhandene lokale Schlüsselhistorie.");
            KeyLibraryVaultStore.ValidateImmutableHistory(previous, next);
        }

        // The download never edits the logical keys; only the exact, validated
        // encrypted bytes are atomically placed in the established local path.
        return ConcurrentFileWriter.Write(path, expectedLocalFile, temporary =>
        {
            if (trust.LoadOffline().Checkpoint != policy.Checkpoint)
                throw new CryptographicException("Gruppenberechtigung hat während des Downloads gewechselt.");
            using var target = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None);
            target.Write(transfer.EncryptedVault);
            target.Flush(true);
            if (trust.LoadOffline().Checkpoint != policy.Checkpoint)
                throw new CryptographicException("Gruppenberechtigung hat während des Downloads gewechselt.");
        });
    }

    private static byte[] ReadVaultBytes(string localVaultPath)
    {
        using var input = new FileStream(localVaultPath, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        if (input.Length is <= 0 or > GroupVaultService.MaximumBytes)
            throw new InvalidDataException("Lokaler verschlüsselter Schlüsselbestand ist leer oder zu groß.");
        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        return bytes;
    }
}

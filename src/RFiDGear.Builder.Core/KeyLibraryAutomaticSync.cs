using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public enum KeyLibrarySyncOutcome
{
    InSync,
    Downloaded,
    Uploaded,
    Empty,
    AdministrationRequired,
    Conflict
}

public sealed record KeyLibrarySyncResult(KeyLibrarySyncOutcome Outcome,
    CentralKeyLibraryHead Central);

/// <summary>
/// Single pass, no background worker and no second store. Studio can call this
/// from its existing workflow once server URL and pin are explicitly configured.
/// Local-only access remains available when the server is unreachable.
/// </summary>
public static class KeyLibraryAutomaticSync
{
    public static async Task<KeyLibrarySyncResult> SynchronizeOnceAsync(
        KeyLibraryHttpsTransport transport, string localVaultPath,
        OfflineGroupTrustStore trust, CentralKeyLibraryCursorStore cursor,
        string groupId,
        X509Certificate2 personalIdentity,
        OfflineGroupAdministrationStore? administration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(personalIdentity);
        KeyLibrarySyncResult Record(KeyLibrarySyncOutcome outcome,
            CentralKeyLibraryHead head)
        {
            cursor.Remember(head);
            return new(outcome, head);
        }
        KeyLibrarySyncResult Reconciled(KeyLibrarySyncOutcome outcome,
            CentralKeyLibraryHead head, FileRevision file)
        {
            cursor.RememberReconciled(head, file);
            return new(outcome, head);
        }
        var policy = trust.LoadOffline();
        var remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (remote.LibraryId != policy.Policy.LibraryId ||
            remote.Policy != policy.Checkpoint ||
            remote.CentralRevision > 0 && remote.GroupId != groupId)
            throw new CryptographicException("Zentrale und lokal bestätigte Gruppenbibliothek stimmen nicht überein.");
        cursor.RequireNotRolledBack(remote);

        var expectedLocal = ConcurrentFileWriter.Capture(localVaultPath);
        KeyLibraryVaultPayload? local = expectedLocal.Exists
            ? KeyLibraryVaultStore.Load(localVaultPath, trust, groupId, [personalIdentity])
            : null;
        if (ConcurrentFileWriter.Capture(localVaultPath) != expectedLocal)
            throw new ConcurrentFileChangeException(localVaultPath);
        if (remote.CentralRevision == 0 && local is null)
            return Record(KeyLibrarySyncOutcome.Empty, remote);

        if (remote.CentralRevision > 0 &&
            (local is null || local.Catalog.Revision < remote.CatalogRevision))
        {
            var transfer = await DownloadVerifiedAsync(transport, trust, remote,
                personalIdentity, groupId, cancellationToken).ConfigureAwait(false);
            // An observed remote revision does not prove that the existing
            // offline branch has no unmerged changes. Never overwrite it unless
            // its encrypted bytes still match the last successfully shared base.
            if (expectedLocal.Exists && !cursor.HasUnchangedLocalBase(expectedLocal))
                return Record(KeyLibrarySyncOutcome.Conflict, remote);
            // Pin the already verified latest central head before touching the
            // local Vault. If local replacement fails, a later run can safely
            // retry without accepting an older central backup.
            cursor.Remember(remote);
            try
            {
                var installed = KeyLibrarySyncClient.InstallDownloadedSnapshot(localVaultPath, trust,
                    groupId, remote, transfer, [personalIdentity], expectedLocal);
                return Reconciled(KeyLibrarySyncOutcome.Downloaded, remote, installed);
            }
            catch (InvalidDataException)
            {
                // An unmerged offline branch must never be silently overwritten.
                return Record(KeyLibrarySyncOutcome.Conflict, remote);
            }
        }

        if (local is null)
            throw new InvalidDataException("Lokale Bibliothek ist nicht vorhanden.");

        if (remote.CentralRevision > 0 &&
            local.Catalog.Revision == remote.CatalogRevision)
        {
            if (expectedLocal.Sha256 == remote.CiphertextSha256)
                return Reconciled(KeyLibrarySyncOutcome.InSync, remote, expectedLocal);

            var transfer = await DownloadVerifiedAsync(transport, trust, remote,
                personalIdentity, groupId, cancellationToken).ConfigureAwait(false);
            var central = KeyLibraryVault.Unprotect(GroupVaultService.Read(
                transfer.EncryptedVault), policy, groupId, [personalIdentity]);
            if (ConcurrentFileWriter.Capture(localVaultPath) != expectedLocal)
                throw new ConcurrentFileChangeException(localVaultPath);
            return LogicallyEquivalent(local, central)
                ? Reconciled(KeyLibrarySyncOutcome.InSync, remote, expectedLocal)
                : Record(KeyLibrarySyncOutcome.Conflict, remote);
        }

        if (local.Catalog.Revision <= remote.CatalogRevision)
            return Record(KeyLibrarySyncOutcome.Conflict, remote);
        if (administration is null)
            return Record(KeyLibrarySyncOutcome.AdministrationRequired, remote);

        // A newer catalog revision is not proof of ancestry. A competing
        // offline branch can retain all keys yet overwrite metadata or status.
        // Only an unchanged last reconciled central head permits auto-upload.
        if (remote.CentralRevision > 0 && !cursor.HasUploadBase(remote))
            return Record(KeyLibrarySyncOutcome.Conflict, remote);

        // Before uploading a newer offline branch, prove it retained every
        // previously centrally accepted key generation and secret.
        if (remote.CentralRevision > 0)
        {
            var transfer = await DownloadVerifiedAsync(transport, trust, remote,
                personalIdentity, groupId, cancellationToken).ConfigureAwait(false);
            var central = KeyLibraryVault.Unprotect(GroupVaultService.Read(
                transfer.EncryptedVault), policy, groupId, [personalIdentity]);
            try { KeyLibraryVaultStore.ValidateImmutableHistory(central, local); }
            catch (InvalidDataException)
            {
                return Record(KeyLibrarySyncOutcome.Conflict, remote);
            }
        }

        var signed = KeyLibrarySyncClient.PrepareUpload(localVaultPath, trust,
            administration, groupId, remote, [personalIdentity]);
        var claim = KeyLibrarySyncTransport.Verify(signed, trust.ExportPublic());
        if (claim.NextCiphertextSha256 != expectedLocal.Sha256 ||
            claim.CatalogRevision != local.Catalog.Revision ||
            ConcurrentFileWriter.Capture(localVaultPath) != expectedLocal)
            throw new ConcurrentFileChangeException(localVaultPath);
        var acknowledged = await transport.UploadAsync(signed,
            cancellationToken).ConfigureAwait(false);
        if (acknowledged.LibraryId != remote.LibraryId ||
            acknowledged.CentralRevision != remote.CentralRevision + 1 ||
            acknowledged.CatalogRevision != local.Catalog.Revision ||
            acknowledged.CiphertextSha256 != claim.NextCiphertextSha256 ||
            acknowledged.Policy != remote.Policy || acknowledged.GroupId != groupId)
            throw new CryptographicException("Zentrale hat die signierte Bibliotheksänderung nicht eindeutig bestätigt.");
        // Record the exact submitted file, not a newer local edit made while
        // the request was in flight. Such an edit remains an offline change.
        return Reconciled(KeyLibrarySyncOutcome.Uploaded, acknowledged, expectedLocal);
    }

    private static async Task<SignedKeyLibrarySync> DownloadVerifiedAsync(
        KeyLibraryHttpsTransport transport, OfflineGroupTrustStore trust,
        CentralKeyLibraryHead head, X509Certificate2 identity, string groupId,
        CancellationToken cancellationToken)
    {
        var proof = KeyLibraryDownloadAuthorization.Sign(head, identity,
            DateTimeOffset.UtcNow);
        var transfer = await transport.DownloadAsync(proof, cancellationToken)
            .ConfigureAwait(false);
        var claim = KeyLibrarySyncTransport.Verify(transfer, trust.ExportPublic());
        if (claim.GroupId != groupId ||
            claim.LibraryId != head.LibraryId ||
            claim.Policy != head.Policy ||
            claim.NextCiphertextSha256 != head.CiphertextSha256 ||
            claim.CatalogRevision != head.CatalogRevision ||
            claim.ExpectedCentralRevision + 1 != head.CentralRevision)
            throw new CryptographicException("Heruntergeladene Bibliothek gehört nicht zur zentral bestätigten Revision.");
        return transfer;
    }

    private static bool LogicallyEquivalent(
        KeyLibraryVaultPayload local, KeyLibraryVaultPayload remote)
    {
        KeyLibraryVault.Validate(local);
        KeyLibraryVault.Validate(remote);
        var first = JsonSerializer.SerializeToUtf8Bytes(local);
        var second = JsonSerializer.SerializeToUtf8Bytes(remote);
        try
        {
            var one = SHA256.HashData(first);
            var two = SHA256.HashData(second);
            return CryptographicOperations.FixedTimeEquals(one, two);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }
}

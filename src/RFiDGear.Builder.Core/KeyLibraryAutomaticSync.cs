using System.Net.Http;
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
    Conflict,
    PolicyDownloaded
}

public sealed record KeyLibrarySyncResult(KeyLibrarySyncOutcome Outcome,
    CentralKeyLibraryHead Central);

// Only an unreachable initial head allows the member offline fallback.
public sealed class KeyLibraryCentralUnavailableException(Exception inner)
    : IOException("Zentraler Ausgangsstand konnte nicht abgerufen werden.", inner);

/// <summary>
/// Single pass, no background worker and no second store. Studio can call this
/// from its existing workflow once server URL and pin are explicitly configured.
/// Local-only access remains available when the server is unreachable.
/// </summary>
public static class KeyLibraryAutomaticSync
{
    public static async Task<KeyLibrarySyncResult> SynchronizeOnceAsync(
        IKeyLibrarySyncTransport transport, string localVaultPath,
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
            ? KeyLibraryVaultStore.Load(localVaultPath, trust, groupId, [personalIdentity], requireFull: administration is not null)
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
            var central = KeyLibraryVaultStore.OpenEncrypted(transfer.EncryptedVault, trust, groupId, [personalIdentity], requireFull: administration is not null);
            if (ConcurrentFileWriter.Capture(localVaultPath) != expectedLocal)
                throw new ConcurrentFileChangeException(localVaultPath);
            if (policy.Policy.CustomerScopesEnabled)
                return Record(KeyLibrarySyncOutcome.Conflict, remote); // partial equality cannot prove identical complete history
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
            var central = KeyLibraryVaultStore.OpenEncrypted(transfer.EncryptedVault, trust, groupId, [personalIdentity], requireFull: true);
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

    /// <summary>Read-only workstation sync. The member never submits a
    /// central write, even when an administrator has a pending journal.</summary>
    public static async Task<KeyLibrarySyncResult> SynchronizeMemberAsync(
        IKeyLibrarySyncTransport transport, string localVaultPath,
        OfflineGroupTrustStore trust, CentralKeyLibraryCursorStore cursor,
        KeyLibraryPolicyTransitionJournal journal, string groupId,
        X509Certificate2 personalIdentity, CancellationToken cancellationToken = default,
        bool allowPolicyAdoption = true)
    {
        if (journal.HasPending)
        {
            if (!allowPolicyAdoption || !journal.IsReceiveOnly || journal.PendingExpected?.GroupId != groupId)
                throw new InvalidOperationException("Ausstehenden Verwaltungsauftrag zuerst in der Hauptverwaltung abschließen.");
            var resumed = await journal.ResumeAsync(transport, trust, cursor, personalIdentity,
                cancellationToken).ConfigureAwait(false);
            return new(KeyLibrarySyncOutcome.PolicyDownloaded, resumed);
        }

        var policy = trust.LoadOffline();
        CentralKeyLibraryHead remote;
        try { remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false); }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        { throw new KeyLibraryCentralUnavailableException(ex); }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        { throw new KeyLibraryCentralUnavailableException(ex); }
        // Once a head was observed, any later download failure is a failed
        // reconciliation, not permission to silently use an older snapshot.
        if (remote.LibraryId != policy.Policy.LibraryId ||
            remote.CentralRevision > 0 && remote.GroupId != groupId)
            throw new CryptographicException("Zentrale gehört zu einer anderen Bibliothek oder Gruppe.");
        if (remote.Policy == policy.Checkpoint)
            return await SynchronizeOnceAsync(transport, localVaultPath, trust, cursor, groupId,
                personalIdentity, null, cancellationToken).ConfigureAwait(false);

        if (!allowPolicyAdoption)
            throw new InvalidOperationException("Dieser Arbeitsplatz besitzt eine Hauptverwaltung. Richtlinienwechsel dort kontrolliert übernehmen.");
        var baseline = cursor.ReadReconciledHead() ??
            throw new InvalidOperationException("Kein bestätigter lokaler Ausgangsstand für automatische Richtlinienübernahme.");
        if (baseline.GroupId != groupId || baseline.Policy != policy.Checkpoint ||
            remote.Policy.Revision <= policy.Checkpoint.Revision ||
            remote.CentralRevision <= baseline.CentralRevision ||
            remote.CatalogRevision < baseline.CatalogRevision)
            throw new CryptographicException("Zentrale meldet einen widersprüchlichen oder älteren Richtlinienstand.");
        var proof = KeyLibraryDownloadAuthorization.Sign(remote, personalIdentity, DateTimeOffset.UtcNow);
        var snapshot = await transport.DownloadSnapshotAsync(proof, cancellationToken).ConfigureAwait(false);
        if (snapshot.Head != remote)
            throw new ConcurrentFileChangeException("Zentrale Schlüsselbibliothek");
        journal.StageReceived(trust, cursor, baseline, snapshot, personalIdentity);
        var confirmed = await journal.ResumeAsync(transport, trust, cursor, personalIdentity,
            cancellationToken).ConfigureAwait(false);
        return new(KeyLibrarySyncOutcome.PolicyDownloaded, confirmed);
    }

    /// <summary>Explicit workstation replacement when the central service is
    /// intact. The caller must have independently provisioned the CURRENT
    /// signed enrollment and separately confirmed the exact current central
    /// head. A response from the same transport is not independent evidence.
    /// Never import a historical backup, upload or overwrite an existing Vault.</summary>
    public static async Task<KeyLibrarySyncResult> RestoreMissingWorkstationAsync(
        IKeyLibrarySyncTransport transport, string localVaultPath,
        OfflineGroupTrustStore trust, CentralKeyLibraryCursorStore cursor,
        KeyLibraryPolicyTransitionJournal journal, string groupId,
        X509Certificate2 personalIdentity, CentralKeyLibraryHead independentlyConfirmedHead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(independentlyConfirmedHead);
        var policy = trust.LoadOffline();
        _ = KeyLibraryDisclosure.RequireMember(policy, groupId, personalIdentity, DateTimeOffset.UtcNow);
        if (!policy.Policy.CustomerScopesEnabled || independentlyConfirmedHead.CentralRevision <= 0 ||
            independentlyConfirmedHead.LibraryId != policy.Policy.LibraryId ||
            independentlyConfirmedHead.Policy != policy.Checkpoint ||
            independentlyConfirmedHead.GroupId != groupId)
            throw new CryptographicException("Aktueller unabhängig bestätigter Kundenbibliotheksstand erforderlich.");

        void RequireMissingAndIdle()
        {
            if (journal.HasPending)
                throw new InvalidOperationException("Offenen Richtlinienauftrag zuerst kontrolliert klären.");
            if (ConcurrentFileWriter.Capture(localVaultPath) != FileRevision.Missing)
                throw new ConcurrentFileChangeException(localVaultPath);
            if (trust.LoadOffline().Checkpoint != policy.Checkpoint)
                throw new CryptographicException("Richtlinie während der Arbeitsplatzwiederherstellung geändert.");
        }

        RequireMissingAndIdle();
        cursor.RequireNotRolledBack(independentlyConfirmedHead);
        var remote = await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (remote != independentlyConfirmedHead)
            throw new CryptographicException("Zentrale weicht vom unabhängig bestätigten Stand ab.");
        var transfer = await DownloadVerifiedAsync(transport, trust, remote,
            personalIdentity, groupId, cancellationToken).ConfigureAwait(false);
        // Authenticate and decrypt using the newly approved personal device,
        // never a recovery identity. A backup cannot authorize its own use.
        var opened = KeyLibraryVaultStore.OpenEncrypted(transfer.EncryptedVault, trust,
            groupId, [personalIdentity]);
        if (opened.Catalog.LibraryId != remote.LibraryId || opened.Catalog.Revision != remote.CatalogRevision)
            throw new CryptographicException("Empfangener Katalog passt nicht zum bestätigten Wiederanlauf.");
        if (await transport.ReadHeadAsync(cancellationToken).ConfigureAwait(false) != remote)
            throw new ConcurrentFileChangeException("Zentrale Schlüsselbibliothek");
        cancellationToken.ThrowIfCancellationRequested();
        RequireMissingAndIdle();

        // Retain the observed head even if the local write is interrupted.
        // No successful result until the exact installed bytes are reconciled.
        cursor.Remember(remote);
        var installed = KeyLibrarySyncClient.InstallDownloadedSnapshot(localVaultPath,
            trust, groupId, remote, transfer, [personalIdentity], FileRevision.Missing);
        if (installed.Sha256 != remote.CiphertextSha256 ||
            ConcurrentFileWriter.Capture(localVaultPath) != installed ||
            trust.LoadOffline().Checkpoint != policy.Checkpoint || journal.HasPending)
            throw new ConcurrentFileChangeException(localVaultPath);
        cursor.RememberReconciled(remote, installed);
        return new(KeyLibrarySyncOutcome.Downloaded, remote);
    }

    private static async Task<SignedKeyLibrarySync> DownloadVerifiedAsync(
        IKeyLibrarySyncTransport transport, OfflineGroupTrustStore trust,
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

    internal static bool LogicallyEquivalent(
        KeyLibraryVaultPayload local, KeyLibraryVaultPayload remote)
    {
        KeyLibraryVault.Validate(local);
        KeyLibraryVault.Validate(remote);
        // Customer blocks have canonical ordering; compare logical records independently of array order.
        KeyLibraryVaultPayload Canonical(KeyLibraryVaultPayload value) => new(
            value.Catalog with { Customers = value.Catalog.Customers.OrderBy(c => c.CustomerId).ToArray(),
                Keys = value.Catalog.Keys.OrderBy(k => k.KeyId).ThenBy(k => k.Version).ToArray() },
            value.Secrets.OrderBy(s => s.Key.KeyId).ThenBy(s => s.Key.Version).ToArray());
        var first = JsonSerializer.SerializeToUtf8Bytes(Canonical(local));
        var second = JsonSerializer.SerializeToUtf8Bytes(Canonical(remote));
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

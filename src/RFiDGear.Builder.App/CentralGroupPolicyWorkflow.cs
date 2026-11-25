using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>Single Studio-side coordination boundary for a configured central
/// library. No server administration UI; no automatic policy publication.</summary>
internal static class CentralGroupPolicyWorkflow
{
    private const string JournalSuffix = ".policy-transition.bin";
    private static string DirectoryFor(Guid id) =>
        Path.GetDirectoryName(KeyLibraryVaultStore.DefaultPath(id, "keys"))!;
    internal static WorkspaceLease Acquire(Guid id) =>
        WorkspaceLease.Acquire(DirectoryFor(id), "Schlüsselbibliothek und Gruppenverwaltung");

    private static WindowsGroupTrustProtection Protection(Guid id) => new(id, syncState: true);

    internal static KeyLibrarySyncEndpoint? Endpoint(Guid id) =>
        new KeyLibrarySyncEndpointStore(KeyLibrarySyncEndpointStore.DefaultPath(id), id,
            Protection(id)).Load();

    private static CentralKeyLibraryCursorStore Cursor(Guid id) =>
        new(CentralKeyLibraryCursorStore.DefaultPath(id), id, Protection(id));

    private static KeyLibraryPolicyTransitionJournal Journal(Guid id, string group) =>
        new(Path.Combine(DirectoryFor(id), group + JournalSuffix), id,
            KeyLibraryVaultStore.DefaultPath(id, group),
            new WindowsGroupTrustProtection(id, policyTransition: true));

    internal static KeyLibraryPolicyTransitionJournal? Pending(Guid id)
    {
        var directory = DirectoryFor(id);
        if (!Directory.Exists(directory)) return null;
        KeyLibraryPolicyTransitionJournal? pending = null;
        foreach (var path in Directory.EnumerateFiles(directory, "*" + JournalSuffix))
        {
            var group = Path.GetFileName(path)[..^JournalSuffix.Length];
            var journal = Journal(id, group);
            if (!journal.HasPending) continue;
            if (pending is not null)
                throw new InvalidDataException("Mehrere ausstehende Richtlinienwechsel; keine automatische Übernahme.");
            pending = journal;
        }
        return pending;
    }

    internal static void RequireNoPending(Guid id)
    {
        if (Pending(id) is not null)
            throw new InvalidOperationException("Ein zentraler Berechtigungswechsel ist ausstehend. Bitte in der Hauptverwaltung zuerst fortsetzen.");
    }

    // Other policy mutation paths must not silently bypass central authority
    // while they have not been integrated into the staged operation.
    internal static void RequireOfflinePolicyEdit(Guid id)
    {
        RequireNoPending(id);
        if (Endpoint(id) is not null)
            throw new InvalidOperationException("Bei eingerichteter Zentrale ist diese spezielle Änderung noch nicht freigegeben. Personen und Gruppen über ‚Änderungen signiert speichern‘ zentral ändern.");
    }

    // Caller holds the library lease through catalog loading. Member sync
    // must not use the private administrator store or submit policy changes.
    internal static async Task<string> RefreshMemberAsync(Guid id, string group,
        OfflineGroupTrustStore trust, IEnumerable<X509Certificate2> identities,
        bool allowPolicyAdoption = true, CancellationToken cancellationToken = default)
    {
        var pending = Pending(id);
        if (pending is not null && (!pending.IsReceiveOnly ||
            pending.PendingExpected?.GroupId != group))
            throw new InvalidOperationException("Ausstehenden Verwaltungsauftrag zuerst in der Hauptverwaltung abschließen.");
        var endpoint = Endpoint(id);
        if (endpoint is null)
        {
            RequireNoPending(id);
            return "Offline · keine Zentrale konfiguriert";
        }
        var current = trust.LoadOffline().Policy;
        var certificate = pending is null
            ? SelectRetained(identities, current, current, group)
            : SelectRetained(identities,
                GroupEnrollmentPackages.Validate(pending.PendingPreviousEnrollment!).Policy,
                GroupEnrollmentPackages.Validate(pending.PendingEnrollment!).Policy, group);
        try
        {
            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin), id,
                endpoint.ServerCertificateSha256, certificate);
            var result = await KeyLibraryAutomaticSync.SynchronizeMemberAsync(transport,
                KeyLibraryVaultStore.DefaultPath(id, group), trust, Cursor(id),
                Journal(id, group), group, certificate, cancellationToken, allowPolicyAdoption);
            if (result.Outcome is not (KeyLibrarySyncOutcome.InSync or KeyLibrarySyncOutcome.Downloaded or
                KeyLibrarySyncOutcome.PolicyDownloaded))
                throw new InvalidOperationException("Zentraler Schlüsselbestand nicht abgeglichen: " + result.Outcome);
            return $"Zentral bestätigt · Katalog {result.Central.CatalogRevision} · Gruppenstand {result.Central.Policy.Revision}";
        }
        catch (HttpRequestException)
        {
            RequireNoPending(id);
            return "Offline · Verbindung oder TLS-Anmeldung nicht bestätigt; nur bisheriger Offline-Stand";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RequireNoPending(id);
            return "Offline · zentraler Abgleich nicht abgeschlossen; nur bisheriger Offline-Stand";
        }
    }

    internal static async Task<CentralKeyLibraryHead> SaveAsync(Guid id,
        OfflineGroupAdministrationStore administration, OfflineGroupTrustStore trust,
        OfflineGroupPolicy proposed, KeyLibrarySyncEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        using var lease = Acquire(id);
        RequireNoPending(id);
        if (Endpoint(id) != endpoint)
            throw new InvalidOperationException("Zentralverbindung wurde geändert.");
        var current = trust.LoadOffline();
        administration.RequireLocalAuthority(trust);
        if (current.Policy.Version != 2 || proposed.Version != 2 ||
            proposed.LibraryId != id || proposed.Revision != checked(current.Policy.Revision + 1))
            throw new InvalidOperationException("Zentraler Wechsel benötigt einen aktuellen personenbasierten Gruppenstand.");
        var identities = WindowsVaultIdentityService.List().ToArray();
        try
        {
            var cursor = Cursor(id);
            var baseline = cursor.ReadReconciledHead() ??
                throw new InvalidOperationException("Bitte die Schlüsselbibliothek zuerst vollständig zentral abgleichen.");
            var identity = SelectRetained(identities.Select(i => i.Certificate), current.Policy,
                proposed, baseline.GroupId);
            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin), id,
                endpoint.ServerCertificateSha256, identity);
            var head = await transport.ReadHeadAsync(cancellationToken);
            if (head != baseline || head.LibraryId != id || head.Policy != current.Checkpoint ||
                head.CentralRevision < 1)
                throw new InvalidOperationException("Zentrale hat nicht den zuletzt abgeglichenen Gruppenstand. Bitte zuerst die Schlüsselbibliothek abgleichen.");
            var vaultPath = KeyLibraryVaultStore.DefaultPath(id, head.GroupId);
            if (Directory.Exists(DirectoryFor(id)) &&
                Directory.EnumerateFiles(DirectoryFor(id), "*.key-library.vault.json")
                    .Any(p => !string.Equals(Path.GetFullPath(p), vaultPath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Weitere lokale Gruppenbibliotheken vorhanden. Ein gemeinsamer Empfängerwechsel für mehrere Vaults ist noch nicht freigegeben.");
            var local = ConcurrentFileWriter.Capture(vaultPath);
            if (!cursor.HasUploadBase(head) || !cursor.HasUnchangedLocalBase(local) ||
                local.Sha256 != head.CiphertextSha256)
                throw new InvalidOperationException("Bitte Schlüsselbibliothek zuerst mit der Zentrale vollständig abgleichen. Keine Offline-Änderung wird überschrieben.");
            var prior = await transport.DownloadAsync(KeyLibraryDownloadAuthorization.Sign(
                head, identity, DateTimeOffset.UtcNow), cancellationToken);
            var transition = administration.PrepareKeyLibraryPolicyTransition(trust, head,
                prior, identity, DateTimeOffset.UtcNow, true, proposed);
            var journal = Journal(id, head.GroupId);
            journal.Stage(trust, cursor, head, transition, identity, true);
            return await journal.ResumeAsync(transport, trust, cursor, identity,
                cancellationToken, administration);
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    internal static async Task<CentralKeyLibraryHead> ResumeAsync(Guid id,
        OfflineGroupAdministrationStore administration, OfflineGroupTrustStore trust,
        CancellationToken cancellationToken = default)
    {
        using var lease = Acquire(id);
        var journal = Pending(id) ?? throw new InvalidOperationException("Kein ausstehender Berechtigungswechsel.");
        var endpoint = Endpoint(id) ?? throw new InvalidOperationException("Bestätigte Zentralverbindung fehlt.");
        var previous = GroupEnrollmentPackages.Validate(journal.PendingPreviousEnrollment!).Policy;
        var next = GroupEnrollmentPackages.Validate(journal.PendingEnrollment!).Policy;
        var group = journal.PendingExpected!.GroupId;
        var identities = WindowsVaultIdentityService.List().ToArray();
        try
        {
            var identity = SelectRetained(identities.Select(i => i.Certificate), previous, next, group);
            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin), id,
                endpoint.ServerCertificateSha256, identity);
            return await journal.ResumeAsync(transport, trust, Cursor(id), identity,
                cancellationToken, administration);
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    private static X509Certificate2 SelectRetained(IEnumerable<X509Certificate2> identities,
        OfflineGroupPolicy previous, OfflineGroupPolicy next, string group) =>
        identities.FirstOrDefault(c => c.HasPrivateKey && IsMember(previous, group, c) &&
            IsMember(next, group, c)) ??
        throw new CryptographicException("Ein auf diesem Arbeitsplatz verfügbarer persönlicher Zugang muss in beiden Gruppenständen berechtigt bleiben.");

    private static bool IsMember(OfflineGroupPolicy policy, string group, X509Certificate2 cert)
    {
        var selected = policy.Groups.SingleOrDefault(g => g.Id == group);
        if (selected is null || DateTime.UtcNow < cert.NotBefore.ToUniversalTime() ||
            DateTime.UtcNow >= cert.NotAfter.ToUniversalTime()) return false;
        var fingerprint = CertificateVaultService.Fingerprint(cert);
        return policy.People.Where(p => p.Enabled && selected.Members.Contains(p.Id, StringComparer.Ordinal))
            .SelectMany(p => p.Devices).Any(d => d.Enabled && d.Fingerprint == fingerprint &&
                d.Certificate == Convert.ToBase64String(cert.RawData));
    }
}

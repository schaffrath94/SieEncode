using System.IO;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal static class KeyLibraryAuditWorkflow
{
    private static string DirectoryFor(Guid id) =>
        Path.Combine(Path.GetDirectoryName(KeyLibraryVaultStore.DefaultPath(id, "keys"))!, "DisclosureAudit");

    internal static void Record(Guid id, KeyLibraryDisclosureEvent entry, X509Certificate2 identity)
    {
        if (entry.LibraryId != id) throw new InvalidDataException("Audit-Bibliothek stimmt nicht überein.");
        KeyLibraryDisclosureAudit.RecordSigned(DirectoryFor(id),
            new WindowsGroupTrustProtection(id, disclosureAudit: true), entry, identity);
    }

    // Caller holds the existing library lease or otherwise serializes local
    // audit writes. This is a best-effort follow-up, never a prerequisite for
    // the offline disclosure itself once the local event was persisted.
    internal static async Task<string> FlushAsync(Guid id, IKeyLibrarySyncTransport transport,
        X509Certificate2 identity, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var sent = await KeyLibraryDisclosureAudit.FlushAsync(DirectoryFor(id),
                new WindowsGroupTrustProtection(id, disclosureAudit: true),
                identity, transport.SubmitAuditAsync, deadline.Token);
            return sent == 0 ? "" : $" · Audit: {sent} Meldung(en) bestätigt";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return " · Audit-Nachmeldung nicht vollständig bestätigt; lokale Nachweise bleiben erhalten";
        }
    }

    internal static async Task<string> TryFlushAsync(Guid id, X509Certificate2 identity)
    {
        try
        {
            var endpoint = CentralGroupPolicyWorkflow.Endpoint(id);
            if (endpoint is null) return "Audit lokal gespeichert · Offline";
            using var transport = new KeyLibraryHttpsTransport(new Uri(endpoint.HttpsOrigin),
                id, endpoint.ServerCertificateSha256, identity);
            var result = await FlushAsync(id, transport, identity);
            return result.Length == 0 ? "Audit: keine neuen Meldungen für dieses Gerät" : result.TrimStart(' ', '·');
        }
        catch (Exception)
        {
            return "Audit-Nachmeldung nicht bestätigt; lokale Nachweise bleiben erhalten";
        }
    }
}

using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed record LibraryStationBasis(string SourcePath, FileRevision SourceFile,
    string VaultPath, ProjectKeyLibrarySessionBasis Library, string SyncStatus);

public partial class MainWindow
{
    private LibraryStationBasis? libraryStationBasis;

    private OfflineGroupTrustStore ProjectLibraryTrust(Guid id) => new(
        Path.Combine(localGroupTrustDirectory, id.ToString("N") + ".trust.bin"),
        id, new WindowsGroupTrustProtection(id));

    // Caller holds the library lease through temporary materialization.
    private (ProjectDefinition Hydrated, LibraryStationBasis Basis, string SyncStatus)
        ResolveLibraryProject(string sourcePath, ProfileLibrary profiles, Guid expectedLibrary)
    {
        var sourceFile = ConcurrentFileWriter.Capture(sourcePath);
        var project = ProjectJson.Load(sourcePath);
        if (project.Project.KeyLibrary?.LibraryId != expectedLibrary ||
            ConcurrentFileWriter.Capture(sourcePath) != sourceFile)
            throw new ConcurrentFileChangeException(sourcePath);
        var trust = ProjectLibraryTrust(expectedLibrary);
        var choices = KeyLibraryAssignmentWindow.DiscoverAvailableVaults(localGroupTrustDirectory)
            .Where(c => c.LibraryId == expectedLibrary).ToArray();
        var endpoint = CentralGroupPolicyWorkflow.Endpoint(expectedLibrary);
        if (endpoint is not null)
        {
            var cursor = new CentralKeyLibraryCursorStore(CentralKeyLibraryCursorStore.DefaultPath(expectedLibrary),
                expectedLibrary, new WindowsGroupTrustProtection(expectedLibrary, syncState: true));
            var group = cursor.ReadReconciledHead()?.GroupId;
            if (!string.IsNullOrEmpty(group)) choices = choices.Where(c => c.GroupId == group).ToArray();
        }
        if (choices.Length != 1)
            throw new InvalidOperationException("Genau eine eindeutig zugeordnete Bibliotheksgruppe erforderlich. Keine Zusammenführung unterschiedlicher Vaults.");
        var choice = choices[0];
        var identities = WindowsVaultIdentityService.List();
        try
        {
            // The existing station entry points are synchronous. Run the
            // existing async transport off the dispatcher, with bounded wait.
            // Local offline operation remains possible on connectivity failure.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            string syncStatus;
            try
            {
                syncStatus = Task.Run(() => CentralGroupPolicyWorkflow.RefreshMemberAsync(expectedLibrary,
                    choice.GroupId, trust, identities.Select(i => i.Certificate),
                    allowPolicyAdoption: !File.Exists(Path.Combine(localGroupTrustDirectory, "Administrations",
                        expectedLibrary.ToString("N") + ".admin.bin")),
                    cancellationToken: deadline.Token)).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                throw new InvalidOperationException("Zentralabgleich nicht rechtzeitig abgeschlossen. Keine neue Sitzung aus einem möglicherweise veralteten Bibliotheksstand; bitte erneut öffnen.");
            }
            CentralGroupPolicyWorkflow.RequireNoPending(expectedLibrary);
            foreach (var identity in identities)
            {
                try
                {
                    var resolved = ProjectKeyLibrarySession.Resolve(project, profiles, choice.Path,
                        trust, choice.GroupId, identity.Certificate, DateTimeOffset.UtcNow);
                    if (ConcurrentFileWriter.Capture(sourcePath) != sourceFile)
                        throw new ConcurrentFileChangeException(sourcePath);
                    return (resolved.Hydrated, new(Path.GetFullPath(sourcePath), sourceFile,
                        choice.Path, resolved.Basis, syncStatus), syncStatus);
                }
                catch (CryptographicException) { }
            }
            throw new CryptographicException("Kein einzelnes persönliches Gerät kann sämtliche benötigten Kundenbereiche öffnen.");
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    // Every new launch must still refer to the exact authenticated local
    // session basis. No forced remote availability and no historical fallback.
    private WorkspaceLease? RequireCurrentLibraryStation()
    {
        var entry = selectedProject ?? throw new InvalidOperationException("Kein Projekt ausgewählt.");
        if (entry.ArtifactOnly)
        {
            if (libraryStationBasis is not null) throw new InvalidOperationException("Projekt gewechselt. Kodierstation erneut öffnen.");
            return null;
        }
        var source = ProjectJson.Load(entry.SourcePath);
        if (source.Project.KeyLibrary is null && !entry.HasLibraryBinding)
        {
            if (libraryStationBasis is not null)
                throw new InvalidOperationException("Projekt gewechselt. Kodierstation erneut öffnen.");
            return null;
        }
        var basis = libraryStationBasis ?? throw new InvalidOperationException("Bibliotheksprojekt zuerst in einer geschützten lokalen Sitzung öffnen.");
        if (source.Project.KeyLibrary?.LibraryId != basis.Library.LibraryId ||
            !string.Equals(Path.GetFullPath(entry.SourcePath), basis.SourcePath, StringComparison.OrdinalIgnoreCase) ||
            secureOperationalSession is null ||
            !string.Equals(Path.GetFullPath(entry.ProjectDirectory), Path.GetFullPath(secureOperationalSession.ProjectDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bibliotheksprojekt oder Sitzung wurde gewechselt. Kodierstation erneut öffnen.");
        var lease = CentralGroupPolicyWorkflow.Acquire(basis.Library.LibraryId);
        try
        {
            CentralGroupPolicyWorkflow.RequireNoPending(basis.Library.LibraryId);
            if (ConcurrentFileWriter.Capture(basis.SourcePath) != basis.SourceFile)
                throw new ConcurrentFileChangeException(basis.SourcePath);
            var identities = WindowsVaultIdentityService.List();
            try
            {
                var identity = identities.SingleOrDefault(i => i.Fingerprint == basis.Library.DeviceFingerprint)
                    ?? throw new CryptographicException("Persönlicher Sitzungszugang fehlt.");
                ProjectKeyLibrarySession.RequireCurrent(basis.Library, basis.VaultPath,
                    ProjectLibraryTrust(basis.Library.LibraryId), identity.Certificate, DateTimeOffset.UtcNow);
            }
            finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
}

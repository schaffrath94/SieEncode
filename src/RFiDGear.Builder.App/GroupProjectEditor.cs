using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void CreateGroupProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null) throw new InvalidOperationException("Bitte zuerst einen Projektentwurf öffnen.");
            if (IsEditorGroupVault) throw new InvalidOperationException("Dieses Projekt ist bereits einer Gruppe zugeordnet. Für Empfängeränderungen den Gruppenabgleich verwenden.");
            var migration = !string.IsNullOrWhiteSpace(editorSourcePath) && editorVaultRevision.Exists;
            if (migration && (editorDirty || editorVaultPayload is not null && !editorKeysPersisted)) throw new InvalidOperationException("Vor der Migration Projekt- und Schlüsseländerungen speichern oder verwerfen.");
            var dialog = new GroupProjectSetupWindow(this, localGroupTrustDirectory, editorProject.Project.Name + " Gruppe", migration, CreateEditorGroupProject);
            if (dialog.ShowDialog() != true || dialog.Result is null) return;
            LoadEditor(ProjectJson.Load(dialog.Result.ProjectPath), dialog.Result.ProjectPath);
            RefreshProjects();
            SetStatus("Neue Gruppenprojektkopie vollständig gespeichert und geprüft. Zum Arbeiten mit normalem Gruppenzugang entsperren. Originale bleiben erhalten.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private GroupProjectCreationResult CreateEditorGroupProject(Guid library, string group, GroupPolicyCheckpoint policy, string name, string? phrase)
    {
        if (editorProject is null || editorLibrary is null || IsEditorGroupVault) throw new InvalidOperationException("Neuer Entwurf oder bisheriges Einzel-/Mehrbenutzerprojekt erforderlich.");
        var migration = !string.IsNullOrWhiteSpace(editorSourcePath) && editorVaultRevision.Exists;
        if (migration && (editorDirty || editorVaultPayload is not null && !editorKeysPersisted)) throw new InvalidOperationException("Ungespeicherte Änderungen zuerst speichern oder verwerfen.");
        CaptureEditorControls();
        var trust = new OfflineGroupTrustStore(Path.Combine(localGroupTrustDirectory, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library));
        var identities = loadEditorVaultIdentities();
        try
        {
            var certificates = identities.Select(i => i.Certificate).ToArray();
            return migration
                ? GroupProjectCreation.MigrateLegacy(projectsDirectory, name, editorSourcePath, editorProjectRevision, editorVaultRevision, trust, group, policy, certificates, DateTimeOffset.UtcNow, phrase, editorLibrary)
                : GroupProjectCreation.Create(projectsDirectory, name, editorProject, CreateVaultPayload(), trust, group, policy, certificates, DateTimeOffset.UtcNow, editorLibrary);
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }
}

using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private bool SaveEditorGroupCopy()
    {
        if (editorProject is null || string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Gespeichertes Gruppenprojekt erforderlich.");
        if (editorGroupRecovery) throw new InvalidOperationException("Zum Kopieren mit normalem Gruppenzugang entsperren. Recovery-Sitzungen erlauben keine operative Projektkopie.");
        var name = TextEntryPrompt.Show(this, "Gruppenprojekt vollständig kopieren",
            "Neuen eindeutigen Projekt-/Ordnernamen eingeben. Projekt und Vault werden gemeinsam mit neuer Projekt-ID kopiert. Bibliothek und Gruppe bleiben gleich; aktuelle zentrale Empfänger gelten. Offene Editor- und geladene Schlüsseländerungen werden in die Kopie übernommen. Originaldateien bleiben unverändert.", editorProject.Project.Name + " Kopie");
        if (name is null) return false;
        var result = CopyEditorGroupProject(name);
        LoadCreatedGroupProject(result);
        SetStatus("Vollständige Gruppenprojektkopie mit neuer ID gespeichert. Zum Arbeiten erneut entsperren; Originaldateien bleiben unverändert.");
        return true;
    }

    private GroupProjectCreationResult CopyEditorGroupProject(string name)
    {
        if (editorProject is null || editorLibrary is null || !IsEditorGroupVault || string.IsNullOrWhiteSpace(editorSourcePath) || editorGroupRecovery)
            throw new InvalidOperationException("Gespeichertes Gruppenprojekt und normaler Gruppenzugang erforderlich.");
        CaptureEditorControls();
        var trust = GroupTrust(editorProject);
        var current = trust.LoadOffline();
        var identities = loadEditorVaultIdentities();
        try
        {
            return GroupProjectCreation.CopyGroup(projectsDirectory, name, editorSourcePath, editorProjectRevision, editorVaultRevision,
                trust, current.Checkpoint, identities.Select(i => i.Certificate), DateTimeOffset.UtcNow, editorLibrary,
                editorProject, editorVaultPayload is null ? null : CreateVaultPayload());
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    private void CreateGroupProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null) throw new InvalidOperationException("Bitte zuerst einen Projektentwurf öffnen.");
            if (editorProject.Project.KeyLibrary is not null)
                throw new InvalidOperationException("Ein Bibliotheksprojekt erhält keinen zusätzlichen unabhängigen Gruppen-Vault.");
            if (IsEditorGroupVault) throw new InvalidOperationException("Dieses Projekt ist bereits einer Gruppe zugeordnet. Für Empfängeränderungen den Gruppenabgleich verwenden.");
            var migration = !string.IsNullOrWhiteSpace(editorSourcePath) && editorVaultRevision.Exists;
            if (migration && (editorDirty || editorVaultPayload is not null && !editorKeysPersisted)) throw new InvalidOperationException("Vor der Migration Projekt- und Schlüsseländerungen speichern oder verwerfen.");
            var dialog = new GroupProjectSetupWindow(this, localGroupTrustDirectory, editorProject.Project.Name + " Gruppe", migration, CreateEditorGroupProject);
            if (dialog.ShowDialog() != true || dialog.Result is null) return;
            LoadCreatedGroupProject(dialog.Result);
            SetStatus("Neue Gruppenprojektkopie vollständig gespeichert und geprüft. Zum Arbeiten mit normalem Gruppenzugang entsperren. Originale bleiben erhalten.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void LoadCreatedGroupProject(GroupProjectCreationResult result)
    {
        LoadEditor(ProjectJson.Load(result.ProjectPath), result.ProjectPath);
        RefreshProjects();
        var entry = projects.FirstOrDefault(item => string.Equals(item.SourcePath, editorSourcePath, StringComparison.OrdinalIgnoreCase));
        if (entry is not null) { SelectProject(entry); ProjectsGrid.SelectedItem = entry; }
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

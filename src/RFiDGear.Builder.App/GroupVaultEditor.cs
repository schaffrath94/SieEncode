using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private bool IsEditorGroupVault => editorProject?.Project.GroupVault is not null || editorVaultVersion == ProjectGroupVault.ProtectionLevel;
    private OfflineGroupTrustStore GroupTrust(ProjectDefinition project)
    {
        var binding = project.Project.GroupVault ?? throw new InvalidDataException("Ursprüngliche Projekt-/Gruppenzuordnung fehlt.");
        return new(Path.Combine(localGroupTrustDirectory, binding.LibraryId.ToString("N") + ".trust.bin"), binding.LibraryId, new WindowsGroupTrustProtection(binding.LibraryId));
    }
    private ProjectKeyVaultPayload LoadEditorGroupKeys()
    {
        if (editorProject is null || string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Gespeichertes Gruppenprojekt fehlt.");
        var identities = loadEditorVaultIdentities();
        try { return ProjectGroupVault.Load(editorSourcePath, editorProject, GroupTrust(editorProject), identities.Select(i => i.Certificate)); }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }
    private void SaveEditorGroupKeys()
    {
        if (editorProject is null || string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Gespeichertes Gruppenprojekt fehlt.");
        var identities = loadEditorVaultIdentities();
        try
        {
            var certificates = identities.Select(i => i.Certificate).ToArray();
            var trust = GroupTrust(editorProject);
            var result = ProjectGroupVault.Update(editorSourcePath, editorProject, CreateVaultPayload(), trust, certificates, editorVaultRevision, DateTimeOffset.UtcNow);
            editorVaultRevision = result.Revision;
            VerifyLoadedVaultPayload(ProjectGroupVault.Load(editorSourcePath, editorProject, trust, certificates));
            editorVaultVersion = ProjectGroupVault.ProtectionLevel;
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }
    private void ReconcileEditorGroupVault()
    {
        if (editorProject?.Project.GroupVault is null || editorProject.Project.Id is null || string.IsNullOrWhiteSpace(editorSourcePath))
            throw new InvalidOperationException("Gespeichertes Gruppenprojekt mit stabiler Zuordnung erforderlich.");
        var identities = loadEditorVaultIdentities();
        try
        {
            var certificates = identities.Select(i => i.Certificate).ToArray();
            var trust = GroupTrust(editorProject);
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            var plan = GroupVaultReconciliation.Preview(path, trust, editorProject.Project.Id.Value, editorProject.Project.GroupVault.GroupId, certificates, DateTimeOffset.UtcNow);
            if (plan.VaultRevision != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            var details = "Hinzufügen:\n" + string.Join("\n", plan.Added.Select(a => a.Role + " · " + a.Fingerprint)) +
                "\n\nEntfernen:\n" + string.Join("\n", plan.Removed.Select(a => a.Role + " · " + a.Fingerprint));
            if (MessageBox.Show(this, details + "\n\nMit frischem Inhaltsschlüssel neu verschlüsseln? Alte Sicherungen bleiben zugänglich. Offene Schlüsseländerungen werden nicht übernommen.", "Gruppenempfänger abgleichen", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var result = GroupVaultReconciliation.Apply(plan, trust, certificates, DateTimeOffset.UtcNow);
            editorVaultRevision = result.Revision;
            EditorVaultStatus.Text = "Gruppenempfänger abgeglichen. Verschlüsselte Altsicherung: " + result.EncryptedBackupPath;
            SetStatus("Gruppen-Vault neu verschlüsselt. Offene Schlüsseländerungen separat speichern.");
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }
    private void AuditEditorGroupVault()
    {
        var path = ProjectKeyVault.GetPath(editorSourcePath);
        var before = ConcurrentFileWriter.Capture(path);
        if (before != editorVaultRevision) throw new ConcurrentFileChangeException(path);
        var payload = LoadEditorGroupKeys();
        if (ConcurrentFileWriter.Capture(path) != before) throw new ConcurrentFileChangeException(path);
        if (editorKeysPersisted) { VerifyLoadedVaultPayload(payload); editorVaultVerified = true; }
        CreateVerifiedVaultBackupIfConfigured(payload);
        EditorVaultStatus.Text = "Gruppen-Vault: Projektbindung, normaler Zugang und Integrität geprüft. Separater Recovery-Zugang wurde nicht geprüft.";
        RefreshEditorWorkflow();
    }
    private void VerifyEditorGroupBuildAccess()
    {
        try
        {
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            VerifyLoadedVaultPayload(LoadEditorGroupKeys());
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
        }
        catch { editorVaultVerified = false; RefreshEditorWorkflow(); throw; }
    }
}

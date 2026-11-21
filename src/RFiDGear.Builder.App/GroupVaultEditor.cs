using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private bool editorGroupRecovery;
    private bool requestGroupRecovery;
    private void UnlockGroupRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (!IsEditorGroupVault) return;
        requestGroupRecovery = true;
        try { UnlockVault_Click(sender, e); }
        finally { requestGroupRecovery = false; }
    }
    private bool IsEditorGroupVault => editorProject?.Project.GroupVault is not null || editorVaultVersion == ProjectGroupVault.ProtectionLevel;
    private OfflineGroupTrustStore GroupTrust(ProjectDefinition project)
    {
        var binding = project.Project.GroupVault ?? throw new InvalidDataException("Ursprüngliche Projekt-/Gruppenzuordnung fehlt.");
        return new(Path.Combine(localGroupTrustDirectory, binding.LibraryId.ToString("N") + ".trust.bin"), binding.LibraryId, new WindowsGroupTrustProtection(binding.LibraryId));
    }
    private ProjectKeyVaultPayload LoadEditorGroupKeys(bool? recovery = null)
    {
        if (editorProject is null || string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Gespeichertes Gruppenprojekt fehlt.");
        var identities = loadEditorVaultIdentities();
        try { return ProjectGroupVault.Load(editorSourcePath, editorProject, GroupTrust(editorProject), identities.Select(i => i.Certificate), recovery ?? editorGroupRecovery); }
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
            var result = ProjectGroupVault.Update(editorSourcePath, editorProject, CreateVaultPayload(), trust, certificates, editorVaultRevision, DateTimeOffset.UtcNow, editorGroupRecovery);
            editorVaultRevision = result.Revision;
            VerifyLoadedVaultPayload(ProjectGroupVault.Load(editorSourcePath, editorProject, trust, certificates, editorGroupRecovery));
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
            var plan = GroupVaultReconciliation.Preview(path, trust, editorProject.Project.Id.Value, editorProject.Project.GroupVault.GroupId, certificates, DateTimeOffset.UtcNow, editorGroupRecovery);
            if (plan.VaultRevision != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            var details = "Hinzufügen:\n" + string.Join("\n", plan.Added.Select(a => a.Role + " · " + a.Fingerprint)) +
                "\n\nEntfernen:\n" + string.Join("\n", plan.Removed.Select(a => a.Role + " · " + a.Fingerprint));
            var memberRevocation = plan.Removed.Any(access => access.Role == "member");
            var revocationWarning = memberRevocation
                ? "\n\nSICHERHEIT: Dieser Vorgang entfernt alte Mitglieder NUR aus der aktuellen verschlüsselten Vault-Datei. Historische Offline-Kopien können mit alten privaten Schlüsseln weiterhin entschlüsselt werden. Die DESFire-Kartenschlüssel bleiben gleich. Für einen echten Entzug von bereits bekannten Kartenschlüsseln ist eine kontrollierte Schlüsselrotation auf den betroffenen Karten und Lesern/SAMs nötig."
                : "";
            if (MessageBox.Show(this,
                    details + "\n\nDen aktuellen Projekt-Vault mit neuem zufälligem Inhaltsschlüssel und aktueller Empfängerliste verschlüsseln?" +
                    revocationWarning +
                    "\n\nEin historischer Stand wird getrennt im lokalen Benutzerprofil gesichert, nicht im gemeinsamen Projektordner. Dieser Sicherungsort muss geschützt und bei Bedarf separat verwahrt werden. Offene Schlüsseländerungen werden nicht übernommen.",
                    memberRevocation ? "Gruppenzugang entfernen – kein rückwirkender Offline-Entzug" : "Gruppenempfänger abgleichen",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var result = GroupVaultReconciliation.Apply(plan, trust, certificates, DateTimeOffset.UtcNow);
            editorVaultRevision = result.Revision;
            EditorVaultStatus.Text = memberRevocation
                ? "Aktueller Gruppen-Vault neu verschlüsselt. Frühere Offline-Kopien können weiter zugänglich sein; Kartenschlüssel nicht rotiert. Separat geschützte historische Sicherung: " + result.EncryptedBackupPath
                : "Gruppenempfänger im aktuellen Vault neu verschlüsselt. Historische Sicherung im lokalen Profil: " + result.EncryptedBackupPath;
            SetStatus(memberRevocation
                ? "Vault-Empfänger aktualisiert – KEIN rückwirkender Offline- oder Kartenschlüsselentzug. Karten-/Leserschlüsselrotation gesondert beurteilen."
                : "Aktueller Gruppen-Vault neu verschlüsselt; Kartenschlüssel unverändert.");
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
        EditorVaultStatus.Text = editorGroupRecovery
            ? "Gruppen-Vault: separater Recovery-Zugang, Projektbindung und Integrität geprüft. Keine normale Gruppenfreigabe."
            : "Gruppen-Vault: Projektbindung, normaler Zugang und Integrität geprüft. Separater Recovery-Zugang wurde nicht geprüft.";
        RefreshEditorWorkflow();
    }
    private void VerifyEditorGroupBuildAccess()
    {
        try
        {
            if (editorGroupRecovery) throw new InvalidOperationException("Recovery-Sitzung: Vor einem Build mit normalem Gruppen-Zugang entsperren.");
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            VerifyLoadedVaultPayload(LoadEditorGroupKeys());
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
        }
        catch { editorVaultVerified = false; RefreshEditorWorkflow(); throw; }
    }
}

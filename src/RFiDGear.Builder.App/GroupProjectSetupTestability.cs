using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyGroupProjectSetupDialog(ProjectDefinition fixture, Guid library, X509Certificate2 identity, ICollection<string> checks)
    {
        loadEditorVaultIdentities = () => [new("Synthetic creation", CertificateVaultService.Fingerprint(identity), new X509Certificate2(identity))];
        LoadEditor(fixture, "");
        var create = new GroupProjectSetupWindow(this, localGroupTrustDirectory, "UI Group New", false, CreateEditorGroupProject);
        try
        {
            VerifyAutomationContract(create); create.LoadLibrary(library);
            try { create.CreateSelected("UI Group New", null, false); throw new Exception("Unconfirmed group creation accepted."); }
            catch (InvalidDataException) { }
            var result = create.CreateSelected("UI Group New", null, true);
            LoadCreatedGroupProject(result);
            if (editorVaultVerified || editorVaultVersion != ProjectGroupVault.ProtectionLevel) throw new Exception("New group project not locked/classified.");
            var lockedCopy = CopyEditorGroupProject("UI Group Locked Copy");
            if (lockedCopy.ProjectId == result.ProjectId) throw new Exception("Copy reused project identity.");
            EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!editorVaultVerified || editorProject!.Productive.PiccKey.Value != fixture.Productive.PiccKey.Value) throw new Exception("Created group project did not reopen with preserved keys.");
            var changed = KeyVaultService.GenerateKey("DF_KEY_AES");
            EditorProductivePiccKey.Text = changed;
            var editedCopy = CopyEditorGroupProject("UI Group Edited Copy");
            var copied = ProjectJson.Load(editedCopy.ProjectPath);
            if (copied.Project.GroupVault != editorProject.Project.GroupVault || editedCopy.ProjectId == result.ProjectId ||
                ProjectGroupVault.Load(editedCopy.ProjectPath, copied, GroupTrust(copied), [identity]).ProductivePicc.Value != changed ||
                ConcurrentFileWriter.Capture(result.ProjectPath) != result.ProjectRevision ||
                ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(result.ProjectPath)) != result.VaultRevision)
                throw new Exception("Editor copy lost edits, changed binding or modified original files.");
            LoadCreatedGroupProject(editedCopy);
            if (selectedProject?.SourcePath != editedCopy.ProjectPath ||
                !CurrentProject.Text.StartsWith("UI Group Edited Copy ·", StringComparison.Ordinal) ||
                AcceptancePath.Text != Path.GetDirectoryName(editedCopy.ProjectPath) || editorVaultVerified)
                throw new Exception("Group copy left the original selected in the header or downstream project actions.");
        }
        finally { create.Close(); }
        var legacy = Path.Combine(projectsDirectory, "UI Legacy Source", "source.project.json");
        ProjectJson.Save(legacy, ProjectJson.WithoutAllSecrets(fixture));
        var vault = ProjectKeyVault.GetPath(legacy);
        const string phrase = "Synthetic UI migration passphrase 2026";
        File.WriteAllText(vault, JsonSerializer.Serialize(KeyVaultService.Protect(JsonSerializer.Serialize(ProjectKeyVault.FromProject(fixture)), phrase), ProjectJson.Options));
        var originalProject = ConcurrentFileWriter.Capture(legacy); var originalVault = ConcurrentFileWriter.Capture(vault);
        LoadEditor(ProjectJson.Load(legacy), legacy);
        var migration = new GroupProjectSetupWindow(this, localGroupTrustDirectory, "UI Group Migrated", true, CreateEditorGroupProject);
        try
        {
            VerifyAutomationContract(migration); migration.LoadLibrary(library);
            var result = migration.CreateSelected("UI Group Migrated", phrase, true);
            if (ConcurrentFileWriter.Capture(legacy) != originalProject || ConcurrentFileWriter.Capture(vault) != originalVault) throw new Exception("UI migration changed source.");
            LoadEditor(ProjectJson.Load(result.ProjectPath), result.ProjectPath);
            EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!editorVaultVerified || editorProject!.Productive.PiccKey.Value != fixture.Productive.PiccKey.Value) throw new Exception("UI migrated keys lost.");
        }
        finally { migration.Close(); }
        checks.Add("Gruppenprojekt-Assistent: bestätigte Neuanlage und passphrasengeschützte Altvault-Migration über Editorpfad, eigene IDs, gesperrtes Neuladen und Entsperren mit erhaltenen Schlüsseln; Originaldateien unverändert. Synthetisch.");
        checks.Add("Gruppenprojektkopie: gesperrte Quelle mit erneuter Zugriffsprüfung und offene Schlüsseländerung als vollständige Kopie mit neuer ID übernommen; Gruppe und Originalhashes erhalten. Namensdialog separat visuell abzunehmen.");
    }
}

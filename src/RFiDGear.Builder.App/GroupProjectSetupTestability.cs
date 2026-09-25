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
            LoadEditor(ProjectJson.Load(result.ProjectPath), result.ProjectPath);
            if (editorVaultVerified || editorVaultVersion != ProjectGroupVault.ProtectionLevel) throw new Exception("New group project not locked/classified.");
            try { SaveEditorProject(saveAs: true); throw new Exception("Definition-only group Save As was allowed."); }
            catch (InvalidOperationException) { }
            EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!editorVaultVerified || editorProject!.Productive.PiccKey.Value != fixture.Productive.PiccKey.Value) throw new Exception("Created group project did not reopen with preserved keys.");
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
    }
}

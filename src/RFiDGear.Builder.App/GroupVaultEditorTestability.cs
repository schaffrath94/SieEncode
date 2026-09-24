using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyGroupVaultEditor(ProjectDefinition fixture, X509Certificate2 identity, List<string> checks)
    {
        using var root = RSA.Create(3072);
        using var recoveryKey = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        using var recovery = new CertificateRequest("CN=Synthetic group editor recovery", recoveryKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
        OfflineVaultDevice Device(X509Certificate2 c) => new(c.Subject, CertificateVaultService.Fingerprint(c), Convert.ToBase64String(c.RawData), true);
        var library = Guid.NewGuid();
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 2, library, 1, [new("local", "Local", ["owner"])])
        { People = [new("owner", "Owner", true, [Device(identity)])], RecoveryDevices = [Device(recovery)] };
        var trust = new OfflineGroupTrustStore(Path.Combine(localGroupTrustDirectory, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library));
        var key = root.ExportSubjectPublicKeyInfo();
        trust.Enroll(key, OfflineGroupTrustStore.AdministratorFingerprint(key), OfflineGroupPolicyService.Sign(policy, root));
        var project = ProjectGroupVault.Bind(fixture, trust, "local");
        project.Project.Name = "Synthetic Group Editor";
        var path = Path.Combine(projectsDirectory, "group-editor.project.json");
        ProjectJson.Save(path, ProjectJson.WithoutAllSecrets(project));
        ProjectGroupVault.Create(path, project, ProjectKeyVault.FromProject(project), trust, [identity], now);
        loadEditorVaultIdentities = () => [new("Synthetic group editor", CertificateVaultService.Fingerprint(identity), new X509Certificate2(identity))];
        LoadEditor(ProjectJson.Load(path), path);
        if (editorVaultVerified || editorVaultVersion != ProjectGroupVault.ProtectionLevel || EditorBuildButton.IsEnabled)
            throw new Exception("Group editor not locked after load.");
        EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!editorVaultVerified || editorProject!.Productive.PiccKey.Value != project.Productive.PiccKey.Value)
            throw new Exception("Group editor unlock failed.");
        var changed = KeyVaultService.GenerateKey("DF_KEY_AES");
        EditorProductivePiccKey.Text = changed;
        EditorSaveVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!editorVaultVerified || ProjectGroupVault.Load(path, ProjectJson.Load(path), trust, [identity]).ProductivePicc.Value != changed)
            throw new Exception("Group editor save failed.");
        var entry = new ProjectCatalog().Scan(projectsDirectory).Single(e => e.SourcePath == Path.GetFullPath(path));
        if (!entry.SecureOnDemand || entry.VaultVersion != ProjectGroupVault.ProtectionLevel) throw new Exception("Group catalog classification lost.");
        try
        {
            var station = MaterializeSecureStationEntry(entry);
            if (!station.HasOperationalBuild || secureOperationalSession is null) throw new Exception("Group station materialization failed.");
        }
        finally
        {
            projectSecretServer?.Dispose(); projectSecretServer = null;
            secureOperationalSession?.Dispose(); secureOperationalSession = null;
        }
        trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 2, People = [policy.People[0] with { Enabled = false }] }, root));
        try { VerifyEditorGroupBuildAccess(); throw new Exception("Revoked group session retained build permission."); }
        catch (CryptographicException) { }
        if (editorVaultVerified || EditorBuildButton.IsEnabled) throw new Exception("Revocation did not relock editor build.");
        var vaultPath = ProjectKeyVault.GetPath(path);
        var intact = File.ReadAllBytes(vaultPath);
        try
        {
            File.WriteAllText(vaultPath, "{");
            var damaged = new ProjectCatalog().Scan(projectsDirectory).Single(e => e.SourcePath == Path.GetFullPath(path));
            if (!damaged.SecureOnDemand) throw new Exception("Damaged group vault enabled legacy build fallback.");
        }
        finally { File.WriteAllBytes(vaultPath, intact); }
        checks.Add("Gruppen-Vault im Editor: gesperrtes Neuladen, Entsperren/Speichern per Buttons, Katalog/gesicherte Stationsbereitstellung und erneute Bausperre nach lokal importiertem Entzug geprüft. Synthetisch, keine Hardwarefreigabe.");
    }
}

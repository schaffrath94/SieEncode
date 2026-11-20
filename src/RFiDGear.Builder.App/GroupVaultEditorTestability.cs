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
        VerifyGroupProjectSetupDialog(fixture, library, identity, checks);
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
        loadEditorVaultIdentities = () => [new("Synthetic separate recovery", CertificateVaultService.Fingerprint(recovery), new X509Certificate2(recovery))];
        LoadEditor(ProjectJson.Load(path), path);
        try { LoadEditorGroupKeys(false); throw new Exception("Recovery identity received normal group access."); }
        catch (CryptographicException) { }
        EditorGroupRecoveryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!editorGroupRecovery || !editorVaultVerified || EditorBuildButton.IsEnabled || !EditorNextStep.Text.Contains("Recovery-Sitzung", StringComparison.Ordinal))
            throw new Exception("Separate recovery session not clearly separated from operational access.");
        var restoredKey = KeyVaultService.GenerateKey("DF_KEY_AES");
        EditorProductivePiccKey.Text = restoredKey;
        EditorSaveVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!editorGroupRecovery || !editorVaultVerified || EditorBuildButton.IsEnabled ||
            ProjectGroupVault.Load(path, ProjectJson.Load(path), trust, [identity]).ProductivePicc.Value != restoredKey ||
            ProjectGroupVault.Load(path, ProjectJson.Load(path), trust, [recovery], recovery: true).ProductivePicc.Value != restoredKey)
            throw new Exception("Recovery editor update lost access or enabled normal builds.");
        EditorAuditVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!EditorVaultStatus.Text.Contains("separater Recovery-Zugang", StringComparison.Ordinal))
            throw new Exception("Recovery audit reported normal access.");
        loadEditorVaultIdentities = () => [new("Synthetic group editor", CertificateVaultService.Fingerprint(identity), new X509Certificate2(identity))];
        EditorUnlockVaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (editorGroupRecovery || !editorVaultVerified) throw new Exception("Explicit normal unlock did not end recovery mode.");
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
        try { CopyEditorGroupProject("UI Revoked Copy"); throw new Exception("Revoked session could copy group project."); }
        catch (CryptographicException) { }
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
        checks.Add("Separater Recovery-Editorweg: normaler Zugriff mit Recovery-Identität abgewiesen, Recovery-Button/Schlüsselupdate/Audit erfolgreich; Builds gesperrt bis explizitem normalem Entsperren. Kein Zertifikatsspeichereingriff.");
    }
}

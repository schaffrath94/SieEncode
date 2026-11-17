#if SIEENCODE_DIALOG_TESTS
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>Fixture setup and persisted-state assertions, compiled only into the separate dialog test build.
/// Actions under test are performed by an external UI Automation client, never by these helpers.</summary>
internal static class DialogTestWorkspace
{
    internal sealed record Fixture(Guid Library, string SourceHash, string VaultHash);
    internal const string CopyName = "Dialog Copy";
    internal static string Root = "";
    internal static string TrustDirectory => Path.Combine(Root, "trust");
    internal static Fixture State => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(Root, "fixture.json")))!;
    internal static string Source => Path.Combine(Root, "projects", "Dialog Source", "source.project.json");
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static OfflineGroupAdministrationStore Admin(Guid id) => new(Path.Combine(TrustDirectory, "Administrations", id.ToString("N") + ".admin.bin"), id, new WindowsGroupTrustProtection(id, true));
    internal static OfflineGroupTrustStore Trust(Guid id) => new(Path.Combine(TrustDirectory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
    internal static X509Certificate2 Identity(string name) => new(File.ReadAllBytes(Path.Combine(Root, name + ".pfx")), "Synthetic-dialog-fixture-only", X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

    internal static void Start(string path, string phase)
    {
        Root = Path.GetFullPath(path);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(Root).StartsWith("dialog-test-", StringComparison.Ordinal))
            throw new InvalidOperationException("Dialog fixtures must be inside the isolated TEMP directory.");
        if (phase == "seed")
        {
            if (Directory.Exists(Root)) throw new InvalidOperationException("Fixture already exists; refusing replacement.");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, ".synthetic-dialog-test"), "SieEncode synthetic dialog test v1");
        }
        if (File.ReadAllText(Path.Combine(Root, ".synthetic-dialog-test")) != "SieEncode synthetic dialog test v1") throw new InvalidDataException("Fixture marker mismatch.");
        var store = new BuilderSettingsStore(Path.Combine(Root, "settings.json"));
        // Explicit paths on every launch; no migration or user-default settings.
        var settings = new BuilderSettings();
        settings.Projects.Directory = Path.Combine(Root, "projects");
        settings.Profiles.Directory = Path.Combine(Root, "profiles");
        settings.VaultBackup.Directory = Path.Combine(Root, "backups");
        store.Save(settings);
        var bases = Path.Combine(settings.Profiles.Directory, "base");
        Directory.CreateDirectory(bases);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets/test-fixtures/Synthetic-AES-Mixed.base.json"), Path.Combine(bases, "Synthetic-AES-Mixed.base.json"), true);
        var owner = new MainWindow("Home", store, TrustDirectory);
        owner.Title = "SieEncode · SYNTHETIC DIALOG TEST";
        if (phase == "seed") { owner.Show(); Seed(owner); owner.Close(); Application.Current.Shutdown(0); return; }
        if (phase.StartsWith("verify-", StringComparison.Ordinal))
        {
            Verify(phase);
            File.WriteAllText(Path.Combine(Root, phase + ".json"), JsonSerializer.Serialize(new { phase, status = "PASSED", sourceUnchanged = true }));
            owner.Close(); Application.Current.Shutdown(0); return;
        }
        owner.Show();
        if (phase == "copy" || phase == "copy-reopen") { owner.OpenDialogCopyFixture(phase == "copy-reopen"); return; }
        Window dialog;
        if (phase == "admin" || phase == "revoke")
        {
            var admin = new GroupAdministrationWindow(owner, TrustDirectory);
            admin.LoadAdministration(State.Library); dialog = admin;
        }
        else if (phase == "delegate" || phase == "delegate-revoked")
        {
            var delegated = new GroupDelegateWindow(owner, TrustDirectory);
            delegated.LoadLibrary(State.Library); delegated.LoadPerson("bob"); dialog = delegated;
        }
        else throw new ArgumentException("Unknown test phase.");
        dialog.Show();
    }

    private static void Seed(MainWindow owner)
    {
        foreach (var name in new[] { "alice", "bob", "bob2", "recovery" })
        {
            using var rsa = RSA.Create(3072);
            using var cert = new CertificateRequest("CN=Synthetic Dialog " + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
            File.WriteAllBytes(Path.Combine(Root, name + ".pfx"), cert.Export(X509ContentType.Pfx, "Synthetic-dialog-fixture-only"));
        }
        using var alice = Identity("alice"); using var bob = Identity("bob"); using var bob2 = Identity("bob2"); using var recovery = Identity("recovery");
        OfflineVaultDevice Device(string label, X509Certificate2 cert) => new(label, CertificateVaultService.Fingerprint(cert), Convert.ToBase64String(cert.RawData), true);
        var id = Guid.NewGuid(); var admin = Admin(id);
        var package = admin.Create();
        package = admin.UpdateDirectory([new("alice", "Alice", true, [Device("Alice device", alice)]), new("bob", "Bob", true, [Device("Bob device", bob), Device("Bob second device", bob2)])],
            [new("local", "Lokal", ["alice", "bob"]), new("other", "Lokal 2", ["alice"])], GroupEnrollmentPackages.Validate(package).Checkpoint);
        package = admin.UpdateRecoveryDevices([Device("Recovery", recovery)], GroupEnrollmentPackages.Validate(package).Checkpoint);
        var trust = Trust(id); trust.Enroll(package.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey), package.Policy);
        var delegated = new GroupDelegateWindow(owner, TrustDirectory);
        delegated.LoadLibrary(id); delegated.LoadPerson("bob");
        var invitation = delegated.GetInvitation(true);
        package = admin.GrantDelegation(invitation, GroupDelegationTransport.InvitationFingerprint(invitation), "test-grant", ["local"], GroupEnrollmentPackages.Validate(package).Checkpoint);
        trust.AcceptPackageUpdate(package, trust.LoadOffline().Checkpoint); delegated.Close();
        owner.SeedDialogCopyFixture(id, alice);
        File.WriteAllText(Path.Combine(Root, "fixture.json"), JsonSerializer.Serialize(new Fixture(id, Hash(Source), Hash(ProjectKeyVault.GetPath(Source)))));
    }

    private static void Verify(string phase)
    {
        var state = State;
        if (Hash(Source) != state.SourceHash || Hash(ProjectKeyVault.GetPath(Source)) != state.VaultHash) throw new Exception("Source project/vault changed.");
        var policy = Trust(state.Library).LoadOffline().Policy;
        if (phase == "verify-copy")
        {
            var entries = new ProjectCatalog().Scan(Path.Combine(Root, "projects"));
            var copy = entries.Single(e => ProjectJson.Load(e.SourcePath).Project.Name == CopyName);
            var original = ProjectJson.Load(Source); var copied = ProjectJson.Load(copy.SourcePath);
            if (copied.Project.Id == original.Project.Id) throw new Exception("Copy reused source ID.");
            using var identity = Identity("alice"); var trust = Trust(state.Library);
            var first = JsonSerializer.Serialize(ProjectGroupVault.Load(Source, original, trust, [identity]));
            var second = JsonSerializer.Serialize(ProjectGroupVault.Load(copy.SourcePath, copied, trust, [identity]));
            if (first != second) throw new Exception("Copied payload changed.");
        }
        else if (phase == "verify-admin")
        {
            if (policy.Groups.Single(g => g.Id == "local").Members.Contains("bob") || !policy.Groups.Single(g => g.Id == "local").Members.Contains("alice")) throw new Exception("Member removal not saved.");
            var devices = policy.People.Single(p => p.Id == "bob").Devices;
            if (!devices.Single(d => d.Name == "Bob device").Enabled || devices.Single(d => d.Name == "Bob second device").Enabled) throw new Exception("Wrong device state saved.");
            if (!policy.Groups.Single(g => g.Id == "other").Members.SequenceEqual(new[] { "alice" })) throw new Exception("Unrelated group changed.");
        }
        else if (phase == "verify-delegate")
        {
            var change = GroupDelegationTransport.ReadChange(File.ReadAllBytes(Path.Combine(Root, "change.group-change.json")));
            var current = GroupEnrollmentPackages.Validate(Admin(state.Library).ExportPublic());
            var proposed = OfflineGroupDelegationService.Apply(change, current);
            if (proposed.Groups.Single(g => g.Id == "local").Name != "Lokal UI edited" || proposed.Groups.Single(g => g.Id == "other").Name != "Lokal 2") throw new Exception("Delegation export lost edits or scope.");
        }
        else if (phase == "verify-revoked")
        {
            if (policy.Delegations.Any(d => d.Enabled)) throw new Exception("Delegation still enabled.");
        }
        else throw new ArgumentException("Unknown verification.");
    }
}

public partial class MainWindow
{
    internal void SeedDialogCopyFixture(Guid id, X509Certificate2 identity)
    {
        var library = LoadProfileLibrary();
        var project = ProjectDraftFactory.Create(library, "Dialog Source");
        project.Productive.PiccKey.Value = KeyVaultService.GenerateKey(project.Productive.PiccKey.Type);
        foreach (var app in library.Compose(project.Profiles.Base, project.Profiles.Customers).Applications)
            project.Productive.ApplicationKeys[ProfileValidator.NormalizeAid(app.Id)] = Enumerable.Range(0, app.KeyCount).Select(_ => KeyVaultService.GenerateKey(app.KeyType)).ToList();
        var trust = DialogTestWorkspace.Trust(id);
        project = ProjectGroupVault.Bind(project, trust, "local");
        ProjectJson.Save(DialogTestWorkspace.Source, ProjectJson.WithoutAllSecrets(project));
        ProjectGroupVault.Create(DialogTestWorkspace.Source, project, ProjectKeyVault.FromProject(project), trust, [identity], DateTimeOffset.UtcNow);
    }

    internal void OpenDialogCopyFixture(bool reopen)
    {
        loadEditorVaultIdentities = () => { var cert = DialogTestWorkspace.Identity("alice"); return [new("Synthetic Alice", CertificateVaultService.Fingerprint(cert), cert)]; };
        RefreshProjects();
        if (reopen) { Navigate("Projects", false); return; }
        LoadEditor(ProjectJson.Load(DialogTestWorkspace.Source), DialogTestWorkspace.Source);
        Navigate("Editor", false);
    }
}
#endif

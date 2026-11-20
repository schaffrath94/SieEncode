using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed record GroupProjectCreationResult(string ProjectPath, Guid ProjectId, FileRevision ProjectRevision, FileRevision VaultRevision);

/// <summary>Publishes a complete new project copy. Existing projects and vaults are never replaced.</summary>
public static class GroupProjectCreation
{
    public static GroupProjectCreationResult Create(string projectsDirectory, string name, ProjectDefinition source,
        ProjectKeyVaultPayload keys, OfflineGroupTrustStore trust, string groupId, GroupPolicyCheckpoint expectedPolicy,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now, ProfileLibrary? profiles = null)
    {
        ValidateName(name);
        if (source.Project.GroupVault is not null) throw new InvalidOperationException("Ein bestehendes Gruppenprojekt benötigt eine gesonderte kontrollierte Kopie/Umzuordnung.");
        var current = trust.LoadOffline();
        if (current.Checkpoint != expectedPolicy) throw new CryptographicException("Gruppenstand wurde seit der Auswahl geändert. Bitte neu laden.");
        var project = ProjectJson.WithoutAllSecrets(source);
        project.Project.Id = null; // A copy has its own authenticated identity; source is unchanged.
        project.Project.Name = name;
        project.Project.OutputDirectory = ".";
        project = ProjectGroupVault.Bind(project, trust, groupId);
        if (profiles is not null)
        {
            var issues = ProjectContractValidator.Validate(ProjectKeyVault.Apply(project, keys), profiles, requireProductiveSecrets: true);
            if (issues.Count != 0) throw new InvalidDataException("Entschlüsselter Projektstand ist nicht vollständig:\n" + string.Join("\n", issues.Select(i => i.Message)));
        }
        var root = Path.GetFullPath(projectsDirectory);
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, name);
        using var reservation = new FileStream(Path.Combine(root, "." + name + ".group-create.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Zielordner existiert bereits. Bitte einen neuen eindeutigen Projektnamen wählen.");
        // A flat or custom-layout definition may already reserve this name/output without a built directory yet.
        var conflict = new ProjectCatalog().Scan(root).FirstOrDefault(entry =>
            string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(entry.ProjectDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
        if (conflict is not null) throw new IOException("Projektname oder Ausgabeziel ist bereits durch ein vorhandenes Projekt belegt. Bitte einen anderen Namen wählen.");
        var staging = Path.Combine(root, ".sieencode-group-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        // No editable *.json is written in staging, so even a concurrent catalog scan cannot list a partial project.
        var filename = name + ".project.json";
        var stagedProjectPath = Path.Combine(staging, filename);
        var pending = Path.Combine(staging, "definition.pending");
        var certificates = identities.ToArray();
        ProjectGroupVault.Create(stagedProjectPath, project, keys, trust, certificates, now);
        ProjectJson.Save(pending, project, FileRevision.Missing);
        var persisted = ProjectJson.Load(pending);
        _ = ProjectGroupVault.Load(stagedProjectPath, persisted, trust, certificates);
        if (trust.LoadOffline().Checkpoint != expectedPolicy) throw new CryptographicException("Gruppenstand während der Erstellung geändert.");
        Directory.Move(staging, target); // Fails if another writer created the destination; never merges directories.
        var projectPath = Path.Combine(target, filename);
        if (trust.LoadOffline().Checkpoint != expectedPolicy) throw new CryptographicException("Gruppenstand vor Projektfreigabe geändert. Noch kein Projekt veröffentlicht.");
        File.Move(Path.Combine(target, "definition.pending"), projectPath, overwrite: false); // Publication point.
        return new(projectPath, persisted.Project.Id!.Value, ConcurrentFileWriter.Capture(projectPath), ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(projectPath)));
        // A failure/crash may leave only encrypted vault + key-free *.pending in staging/target. Never delete other writers' files.
    }

    public static GroupProjectCreationResult MigrateLegacy(string projectsDirectory, string name, string sourcePath,
        FileRevision expectedProject, FileRevision expectedVault, OfflineGroupTrustStore trust, string groupId,
        GroupPolicyCheckpoint expectedPolicy, IEnumerable<X509Certificate2> identities, DateTimeOffset now, string? legacyPassphrase = null, ProfileLibrary? profiles = null)
    {
        var path = Path.GetFullPath(sourcePath);
        var vaultPath = ProjectKeyVault.GetPath(path);
        // Keep original files read-locked throughout decryption and publication on Windows.
        using var projectStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var vaultStream = new FileStream(vaultPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var projectBytes = ReadSnapshot(projectStream, expectedProject, path);
        var vaultBytes = ReadSnapshot(vaultStream, expectedVault, vaultPath);
        var source = JsonSerializer.Deserialize<ProjectDefinition>(projectBytes, ProjectJson.Options) ?? throw new InvalidDataException("Projektdefinition fehlt.");
        if (source.Project.GroupVault is not null || ProjectGroupVault.IsGroupVaultFile(vaultPath))
            throw new InvalidOperationException("Dies ist bereits ein Gruppenprojekt. Keine implizite Umzuordnung erlaubt.");
        var vault = JsonSerializer.Deserialize<KeyVaultDocument>(vaultBytes, ProjectJson.Options) ?? throw new InvalidDataException("Alter Vault fehlt.");
        var certificates = identities.ToArray();
        var plaintext = legacyPassphrase is null ? CertificateVaultService.UnprotectWithCertificate(vault, certificates) : KeyVaultService.Unprotect(vault, legacyPassphrase);
        var keys = JsonSerializer.Deserialize<ProjectKeyVaultPayload>(plaintext, ProjectJson.Options) ?? throw new InvalidDataException("Alter Vault enthält keine Projektschlüssel.");
        ProjectKeyVault.Normalize(keys);
        return Create(projectsDirectory, name, source, keys, trust, groupId, expectedPolicy, certificates, now, profiles);
    }

    public static GroupProjectCreationResult CopyGroup(string projectsDirectory, string name, string sourcePath,
        FileRevision expectedProject, FileRevision expectedVault, OfflineGroupTrustStore trust, GroupPolicyCheckpoint expectedPolicy,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now, ProfileLibrary? profiles = null,
        ProjectDefinition? draft = null, ProjectKeyVaultPayload? editedKeys = null)
    {
        var path = Path.GetFullPath(sourcePath);
        var vaultPath = ProjectKeyVault.GetPath(path);
        using var projectStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var vaultStream = new FileStream(vaultPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var source = JsonSerializer.Deserialize<ProjectDefinition>(ReadSnapshot(projectStream, expectedProject, path), ProjectJson.Options)
            ?? throw new InvalidDataException("Projektdefinition fehlt.");
        _ = ReadSnapshot(vaultStream, expectedVault, vaultPath);
        var binding = source.Project.GroupVault ?? throw new InvalidDataException("Quelle ist kein Gruppenprojekt.");
        var certificates = identities.ToArray();
        // Always authenticate the original using current normal membership, even when editor changes are supplied.
        var originalKeys = ProjectGroupVault.Load(path, source, trust, certificates);
        if (draft is not null && (draft.Project.Id != source.Project.Id || draft.Project.GroupVault != binding))
            throw new CryptographicException("Editorentwurf passt nicht zur authentisierten Quelle. Keine stille Umzuordnung erlaubt.");
        var copy = ProjectJson.WithoutAllSecrets(draft ?? source);
        copy.Project.GroupVault = null;
        return Create(projectsDirectory, name, copy, editedKeys ?? originalKeys, trust, binding.GroupId, expectedPolicy, certificates, now, profiles);
    }

    private static byte[] ReadSnapshot(FileStream stream, FileRevision expected, string path)
    {
        if (stream.Length is <= 0 or > GroupVaultService.MaximumBytes) throw new InvalidDataException("Quelldatei ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        if (new FileRevision(true, Convert.ToHexString(SHA256.HashData(bytes))) != expected) throw new ConcurrentFileChangeException(path);
        return bytes;
    }

    private static void ValidateName(string name)
    {
        if (name is not { Length: >= 1 and <= 120 } || name != name.Trim() || name.EndsWith('.') ||
            name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) || name is "." or "..")
            throw new InvalidDataException("Projektname muss ein gültiger einfacher Ordnername sein (1 bis 120 Zeichen).");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || Enumerable.Range(1, 9).Any(n => stem == "COM" + n || stem == "LPT" + n))
            throw new InvalidDataException("Dieser Projektname ist unter Windows reserviert.");
    }
}

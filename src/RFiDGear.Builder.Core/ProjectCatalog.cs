using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed record ProjectCatalogEntry(
    string Name,
    string Build,
    string BaseProfile,
    string Customers,
    string Folder,
    DateTime Modified,
    string SourcePath,
    string ProjectDirectory,
    bool ArtifactOnly,
    bool HasVault = false,
    bool HasOperationalBuild = false,
    bool HasRecognition = false,
    int VaultVersion = 0,
    bool HasCoding = false)
{
    public bool SecureOnDemand => !ArtifactOnly && VaultVersion >= KeyVaultService.CertificateVersion;
    public bool CanOpenStation => HasCoding && (SecureOnDemand || HasOperationalBuild);
    public string Kind => ArtifactOnly ? "Nur gebaut" : SecureOnDemand ? "Sicher bereitstellbar" : !HasOperationalBuild ? "Neu bauen" : HasRecognition ? "Einsatzbereit" : "Altbuild nutzbar";
    public string KeyState => ArtifactOnly ? "Schlüssel im Build" : SecureOnDemand ? "Persönlich freigegebener Vault" : HasVault ? "Neubau mit Vault" : "Neubau gesperrt";
}

public sealed class ProjectCatalog
{
    public IReadOnlyList<ProjectCatalogEntry> Scan(string projectsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsDirectory);
        var root = Path.GetFullPath(projectsDirectory);
        if (!Directory.Exists(root)) return [];
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var entries = new List<ProjectCatalogEntry>();
        var editableDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(root, "*.json", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Path.GetFileName(path).EndsWith(".template.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "project.definition.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (!LooksLikeEditableProject(path)) continue;
                var project = ProjectJson.Load(path);
                if (string.IsNullOrWhiteSpace(project.Project.Name) || string.IsNullOrWhiteSpace(project.Profiles.Base)) continue;
                var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
                editableDirectories.Add(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar));
                var builtDirectory = ProjectPaths.ResolveBuiltProject(project, root);
                editableDirectories.Add(builtDirectory.TrimEnd(Path.DirectorySeparatorChar));
                // A bound group project must never fall back to an old plaintext build when its vault is missing or damaged.
                var vaultVersion = project.Project.GroupVault is not null ? ProjectGroupVault.ProtectionLevel : GetVaultVersion(path);
                entries.Add(new ProjectCatalogEntry(
                    project.Project.Name,
                    string.Join(" + ", project.Project.Build),
                    project.Profiles.Base,
                    project.Profiles.Customers.Count > 0 ? string.Join(", ", project.Profiles.Customers) : "–",
                    RelativeFolder(root, sourceDirectory),
                    File.GetLastWriteTime(path),
                    Path.GetFullPath(path),
                    builtDirectory,
                    false,
                    vaultVersion > 0,
                    HasCoreOperationalBuild(project, builtDirectory),
                    HasRecognitionBuild(project.Project.Name, builtDirectory),
                    vaultVersion,
                    project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { }
        }
        foreach (var path in Directory.EnumerateFiles(root, "project.definition.json", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            if (editableDirectories.Contains(directory.TrimEnd(Path.DirectorySeparatorChar))) continue;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var projectNode = GetProperty(document.RootElement, "project");
                var name = GetProperty(projectNode, "name").GetString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                var baseProfile = "";
                var customers = new List<string>();
                if (TryGetProperty(document.RootElement, "profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
                    foreach (var profile in profiles.EnumerateArray())
                    {
                        var id = TryGetString(profile, "id");
                        var kind = TryGetString(profile, "kind");
                        if (kind == "base") baseProfile = id;
                        else if (kind == "customer" && id.Length > 0) customers.Add(id);
                    }
                var hasCoding = File.Exists(Path.Combine(directory, "coding.schema.json"));
                entries.Add(new ProjectCatalogEntry(name, "Gebautes Projekt", baseProfile, customers.Count > 0 ? string.Join(", ", customers) : "–",
                    RelativeFolder(root, directory), File.GetLastWriteTime(path), Path.GetFullPath(path), directory, true, false,
                    Directory.EnumerateFiles(directory, "*.rfPrj", SearchOption.TopDirectoryOnly).Any(), HasRecognitionBuild(name, directory), 0, hasCoding));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        }
        return entries.OrderByDescending(entry => entry.Modified).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool LooksLikeEditableProject(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return TryGetProperty(document.RootElement, "Project", out var project) && project.ValueKind == JsonValueKind.Object &&
               TryGetProperty(document.RootElement, "Profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Object &&
               TryGetProperty(project, "Name", out _) && TryGetProperty(profiles, "Base", out _);
    }

    private static int GetVaultVersion(string projectPath)
    {
        var path = ProjectKeyVault.GetPath(projectPath);
        if (!File.Exists(path)) return 0;
        try
        {
            if (ProjectGroupVault.IsGroupVaultFile(path)) return ProjectGroupVault.ProtectionLevel;
            var vault = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options);
            return vault is not null && KeyVaultService.IsStructurallyValid(vault) ? vault.Version : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or InvalidOperationException) { return 0; }
    }

    private static bool HasCoreOperationalBuild(ProjectDefinition project, string directory)
    {
        if (!Directory.Exists(directory)) return false;
        var name = project.Project.Name;
        if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase) &&
            (!File.Exists(Path.Combine(directory, name + "_Kodierung.rfPrj")) || !File.Exists(Path.Combine(directory, "coding.schema.json")))) return false;
        if (project.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase) &&
            !File.Exists(Path.Combine(directory, name + "_Mastering.rfPrj"))) return false;
        return File.Exists(Path.Combine(directory, "project.definition.json")) && File.Exists(Path.Combine(directory, "builder.manifest.json"));
    }

    private static bool HasRecognitionBuild(string projectName, string directory)
        => File.Exists(Path.Combine(directory, "card-recognition.plan.json")) &&
           File.Exists(Path.Combine(directory, projectName + "_CardRecognition_ReadOnly.rfPrj"));

    private static string RelativeFolder(string root, string directory)
    {
        var relative = Path.GetRelativePath(root, directory);
        return string.IsNullOrWhiteSpace(relative) ? "." : relative;
    }

    private static JsonElement GetProperty(JsonElement element, string name)
        => TryGetProperty(element, name, out var value) ? value : throw new InvalidDataException($"JSON-Feld fehlt: {name}");

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }

    private static string TryGetString(JsonElement element, string name)
        => TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}

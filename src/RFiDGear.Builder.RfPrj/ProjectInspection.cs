using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record InspectionIssue(string Code, string Message, string? File = null);
public sealed record InspectedFile(string Name, string Kind, bool Exists, long? Size = null);
public sealed record InspectedProject(string Name, string ManifestVersion, int TaskCount, bool DependencyGraphValid, IReadOnlyDictionary<string, int> TaskTypes);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectPreflightScope
{
    Build,
    Operational,
    Artifact
}

public sealed class ProjectPreflightReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string ProjectName { get; init; } = "";
    public string GeneratedAt { get; init; } = "";
    public ProjectPreflightScope Scope { get; init; }
    public string Status => Issues.Count == 0 ? "OK" : "ERROR";
    public List<InspectionIssue> Issues { get; init; } = [];
    public List<InspectedFile> Files { get; init; } = [];
    public List<InspectedProject> Projects { get; init; } = [];
    public List<string> Limitations { get; init; } = [];
}

public static class ProjectPreflight
{
    private static readonly string[] ContractFiles =
    [
        "recovery.plan.json", "card-preflight.plan.json", "card-recognition.plan.json", "project.definition.json", "builder.manifest.json", "acceptance.plan.json",
        "rfidgear.host-contract.json"
    ];

    public static ProjectPreflightReport Inspect(ProjectDefinition project, ProfileLibrary library, string configPath, string projectDirectory,
        ProjectPreflightScope scope = ProjectPreflightScope.Build)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(library);
        var root = Path.GetFullPath(projectDirectory);
        var report = new ProjectPreflightReport
        {
            ProjectName = project.Project.Name,
            GeneratedAt = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            Scope = scope
        };
        var requireProductiveSecrets = scope == ProjectPreflightScope.Build &&
            project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        foreach (var issue in ProjectContractValidator.Validate(project, library, requireProductiveSecrets))
            report.Issues.Add(new InspectionIssue(issue.Code, issue.Message));

        if (scope == ProjectPreflightScope.Operational)
            report.Limitations.Add("Produktive Schlüsselwerte werden im Betriebscheck absichtlich nicht geladen. Vollständigkeit und Format werden beim Vault-Speichern und beim Projektbuild geprüft.");

        try { _ = library.Compose(project.Profiles.Base, project.Profiles.Customers); }
        catch (Exception exception) { report.Issues.Add(new InspectionIssue("PROFILE_COMPOSITION", exception.Message)); }

        var expectedContracts = ContractFiles.ToList();
        if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase)) expectedContracts.Add("coding.schema.json");
        foreach (var name in expectedContracts) InspectContract(project, configPath, root, name, report);

        foreach (var name in ExpectedProjects(project)) InspectRfPrj(root, name, report);
        if (scope == ProjectPreflightScope.Operational)
        {
            var additiveRecognitionFiles = report.Issues
                .Where(IsMissingAdditiveRecognitionArtifact)
                .ToArray();
            foreach (var issue in additiveRecognitionFiles) report.Issues.Remove(issue);
            if (additiveRecognitionFiles.Length > 0)
                report.Limitations.Add("Kompatibler Altbuild: Das strikt lesende RC15-Erkennungsmodul fehlt. Vorhandene Kartenprüf-, Mastering- und Kodierartefakte bleiben verwendbar; 'Transponder erkennen' ist für dieses Projekt nicht verfügbar.");
        }
        return report;
    }

    public static ProjectPreflightReport InspectArtifacts(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Projektordner nicht gefunden: {root}");

        var definitionPath = Path.Combine(root, "project.definition.json");
        var projectName = Path.GetFileName(root);
        if (File.Exists(definitionPath))
        {
            try
            {
                using var definition = JsonDocument.Parse(File.ReadAllText(definitionPath));
                if (definition.RootElement.TryGetProperty("project", out var project) &&
                    project.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                    projectName = name.GetString()!;
            }
            catch (JsonException) { }
        }

        var report = new ProjectPreflightReport
        {
            ProjectName = projectName,
            GeneratedAt = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            Scope = ProjectPreflightScope.Artifact,
            Limitations =
            {
                "Die Quelldefinition fehlt. Profilbindung und Aktualität gegenüber einer *.project.json können deshalb nicht geprüft werden.",
                "Produktive Schlüsselwerte werden für die strukturelle Artefaktprüfung nicht geladen."
            }
        };

        foreach (var name in new[] { "project.definition.json", "builder.manifest.json", "recovery.plan.json", "card-preflight.plan.json", "acceptance.plan.json", "coding.schema.json" })
            InspectArtifactContract(root, projectName, name, required: true, report);
        InspectArtifactContract(root, projectName, "card-recognition.plan.json", required: false, report);
        InspectArtifactContract(root, projectName, "rfidgear.host-contract.json", required: false, report);

        var rfPrjFiles = Directory.EnumerateFiles(root, "*.rfPrj", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (rfPrjFiles.Length == 0)
            report.Issues.Add(new InspectionIssue("RFPRJ_MISSING", "Der Projektordner enthält kein RFiDGear-Projekt."));
        foreach (var path in rfPrjFiles) InspectRfPrj(root, Path.GetFileName(path), report);
        return report;
    }

    private static void InspectArtifactContract(string root, string projectName, string name, bool required, ProjectPreflightReport report)
    {
        var path = Path.Combine(root, name);
        var exists = File.Exists(path);
        report.Files.Add(new InspectedFile(name, "contract", exists, exists ? new FileInfo(path).Length : null));
        if (!exists)
        {
            if (required) report.Issues.Add(new InspectionIssue("FILE_MISSING", "Erwartete Vertragsdatei fehlt.", name));
            else report.Limitations.Add($"Optionaler Vertrag '{name}' ist in diesem älteren Build nicht vorhanden.");
            return;
        }

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException("JSON-Wurzel ist kein Objekt.");
            var artifactProjectName = node["projectName"]?.GetValue<string>() ?? node["project"]?["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(artifactProjectName) && !string.Equals(artifactProjectName, projectName, StringComparison.Ordinal))
                report.Issues.Add(new InspectionIssue("PROJECT_NAME_MISMATCH", $"Projektname '{artifactProjectName}' passt nicht zu '{projectName}'.", name));
            if (name == "builder.manifest.json" && node["rfPrjManifestVersion"]?.GetValue<string>() != "2.0.6")
                report.Issues.Add(new InspectionIssue("RFPRJ_VERSION", "Erwartete rfPrj-Manifestversion 2.0.6 fehlt.", name));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or InvalidOperationException)
        {
            report.Issues.Add(new InspectionIssue("JSON_INVALID", exception.Message, name));
        }
    }

    private static void InspectContract(ProjectDefinition project, string configPath, string root, string name, ProjectPreflightReport report)
    {
        var path = Path.Combine(root, name);
        var exists = File.Exists(path);
        report.Files.Add(new InspectedFile(name, "contract", exists, exists ? new FileInfo(path).Length : null));
        if (!exists) { report.Issues.Add(new InspectionIssue("FILE_MISSING", "Erwartete Vertragsdatei fehlt.", name)); return; }
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException("JSON-Wurzel ist kein Objekt.");
            var artifactProjectName = node["projectName"]?.GetValue<string>() ?? node["project"]?["name"]?.GetValue<string>();
            if (!string.Equals(artifactProjectName, project.Project.Name, StringComparison.Ordinal))
                report.Issues.Add(new InspectionIssue("PROJECT_NAME_MISMATCH", $"Projektname '{artifactProjectName}' passt nicht zu '{project.Project.Name}'.", name));
            if (name == "builder.manifest.json")
            {
                var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(configPath)));
                var actualHash = node["configSha256"]?.GetValue<string>();
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    report.Issues.Add(new InspectionIssue("PROJECT_STALE", "Builder-Manifest gehört nicht zum aktuellen Stand der Projektkonfiguration.", name));
                if (node["rfPrjManifestVersion"]?.GetValue<string>() != "2.0.6")
                    report.Issues.Add(new InspectionIssue("RFPRJ_VERSION", "Erwartete rfPrj-Manifestversion 2.0.6 fehlt.", name));
            }
            var text = node.ToJsonString();
            foreach (var secret in ProductiveSecrets(project))
                if (text.Contains(secret, StringComparison.Ordinal))
                {
                    report.Issues.Add(new InspectionIssue("SECRET_IN_CONTRACT", "Vertragsdatei enthält einen produktiven Schlüsselwert.", name));
                    break;
                }
        }
        catch (Exception exception) { report.Issues.Add(new InspectionIssue("JSON_INVALID", exception.Message, name)); }
    }

    private static void InspectRfPrj(string root, string name, ProjectPreflightReport report)
    {
        var path = Path.Combine(root, name);
        var exists = File.Exists(path);
        report.Files.Add(new InspectedFile(name, "rfPrj", exists, exists ? new FileInfo(path).Length : null));
        if (!exists) { report.Issues.Add(new InspectionIssue("FILE_MISSING", "Erwartetes RFiDGear-Projekt fehlt.", name)); return; }
        try
        {
            var project = RfPrjArchive.Load(path);
            var plaintextSecrets = RfPrjSecretProtection.FindPlaintextSecretElements(project);
            if (plaintextSecrets.Count > 0)
            {
                var message = $"RFiDGear-Projekt enthält {plaintextSecrets.Count} Klartext-Schlüsselfeld(er) und muss in das Referenzformat migriert werden.";
                if (report.Scope == ProjectPreflightScope.Build)
                    report.Issues.Add(new InspectionIssue("RFPRJ_PLAINTEXT_SECRET", message, name));
                else
                    report.Limitations.Add($"Unsicheres Altprojekt '{name}': {message}");
            }
            var tasks = project.Summarize();
            var graphIssues = ValidateDependencies(tasks);
            foreach (var issue in graphIssues) report.Issues.Add(issue with { File = name });
            var masteringInputsRequired = name.EndsWith("_Mastering.rfPrj", StringComparison.OrdinalIgnoreCase);
            foreach (var issue in ValidateIoPaths(tasks, root, masteringInputsRequired)) report.Issues.Add(issue with { File = name });
            if (project.ManifestVersion != "2.0.6") report.Issues.Add(new InspectionIssue("RFPRJ_VERSION", $"rfPrj verwendet Manifestversion '{project.ManifestVersion}' statt 2.0.6.", name));
            if (tasks.Count == 0) report.Issues.Add(new InspectionIssue("TASKS_EMPTY", "RFiDGear-Projekt enthält keine Tasks.", name));
            report.Projects.Add(new InspectedProject(name, project.ManifestVersion, tasks.Count, graphIssues.Count == 0,
                tasks.GroupBy(task => task.Type, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)));
        }
        catch (Exception exception) { report.Issues.Add(new InspectionIssue("RFPRJ_INVALID", exception.Message, name)); }
    }

    public static List<InspectionIssue> ValidateDependencies(IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        var issues = new List<InspectionIssue>();
        var prior = new HashSet<string>(StringComparer.Ordinal);
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Index) || !all.Add(task.Index))
                issues.Add(new InspectionIssue("TASK_INDEX", $"Taskposition {task.Position} besitzt keinen eindeutigen Index."));
            if (string.IsNullOrWhiteSpace(task.ConditionIndex))
            {
                if (task.ConditionLevel != "Empty")
                    issues.Add(new InspectionIssue("TASK_ROOT_CONDITION", $"Unabhängiger Task {task.Index} verwendet nicht den Bedingungslevel Empty."));
            }
            else if (!prior.Contains(task.ConditionIndex))
                issues.Add(new InspectionIssue("TASK_DEPENDENCY", $"Task {task.Index} verweist nicht auf einen vorherigen Task."));
            if (!string.IsNullOrWhiteSpace(task.Index)) prior.Add(task.Index);
        }
        return issues;
    }

    public static List<InspectionIssue> ValidateIoPaths(IReadOnlyList<RfPrjTaskSummary> tasks, string projectDirectory, bool requireInputFiles = false)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var issues = new List<InspectionIssue>();
        var root = Path.GetFullPath(projectDirectory);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var task in tasks)
        {
            Check(task, "Eingabedatei", task.InputPath, validateInput: requireInputFiles);
            Check(task, "Readback-Ziel", task.OutputPath);
        }
        return issues;

        void Check(RfPrjTaskSummary task, string label, string value, bool validateInput = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            try
            {
                var resolved = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(root, value));
                if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new InspectionIssue("TASK_IO_PATH_OUTSIDE",
                        $"Task {task.Index} ({task.Type}) verwendet {label} außerhalb des Projektordners: {resolved}"));
                    return;
                }
                if (!validateInput) return;
                if (!File.Exists(resolved))
                {
                    issues.Add(new InspectionIssue("TASK_INPUT_MISSING", $"Task {task.Index} ({task.Type}) benötigt die fehlende Eingabedatei: {resolved}"));
                    return;
                }
                try
                {
                    var compact = string.Concat(File.ReadAllText(resolved).Where(character => !char.IsWhiteSpace(character)));
                    var bytes = Convert.FromHexString(compact);
                    if (int.TryParse(task.FileSize, out var expected) && bytes.Length != expected)
                        issues.Add(new InspectionIssue("TASK_INPUT_SIZE", $"Task {task.Index} ({task.Type}) erwartet {expected} Byte, Eingabedatei besitzt aber {bytes.Length} Byte: {resolved}"));
                }
                catch (FormatException exception)
                {
                    issues.Add(new InspectionIssue("TASK_INPUT_INVALID", $"Task {task.Index} ({task.Type}) besitzt keine gültige Hex-Eingabedatei: {resolved} ({exception.Message})"));
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                issues.Add(new InspectionIssue("TASK_IO_PATH_INVALID",
                    $"Task {task.Index} ({task.Type}) besitzt einen ungültigen Pfad für {label}: {exception.Message}"));
            }
        }
    }

    private static IEnumerable<string> ExpectedProjects(ProjectDefinition project)
    {
        var name = project.Project.Name;
        if (project.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase)) yield return name + "_Mastering.rfPrj";
        if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase)) yield return name + "_Kodierung.rfPrj";
        yield return name + "_CardPreflight_Transport.rfPrj";
        yield return name + "_CardRecognition_ReadOnly.rfPrj";
        if (!CardOperationMode.IsExtension(project)) yield return name + "_Recovery_Format_TransportPICC.rfPrj";
        else yield return name + "_Erweiterung_Rueckbau.rfPrj";
        if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase))
        {
            yield return name + "_Acceptance_Application_Productive.rfPrj";
            yield return name + "_CardPreflight_Productive.rfPrj";
            if (!CardOperationMode.IsExtension(project)) yield return name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj";
        }
    }

    private static IEnumerable<string> ProductiveSecrets(ProjectDefinition project)
    {
        if (project.Productive.PiccKey.Value.Length >= 16) yield return project.Productive.PiccKey.Value;
        foreach (var key in project.Productive.ApplicationKeys.Values.SelectMany(keys => keys).Where(key => key.Length >= 16).Distinct(StringComparer.Ordinal))
            yield return key;
    }

    private static bool IsMissingAdditiveRecognitionArtifact(InspectionIssue issue)
        => issue.Code == "FILE_MISSING" &&
           (string.Equals(issue.File, "card-recognition.plan.json", StringComparison.OrdinalIgnoreCase) ||
            issue.File?.EndsWith("_CardRecognition_ReadOnly.rfPrj", StringComparison.OrdinalIgnoreCase) == true);
}

public sealed record TaskMapProject(string Name, string ManifestVersion, IReadOnlyList<RfPrjTaskSummary> Tasks);
public sealed record TaskMapDocument(string SchemaVersion, string GeneratedAt, IReadOnlyList<TaskMapProject> Projects);

public static class TaskMapExporter
{
    public static TaskMapDocument InspectDirectory(string projectDirectory)
    {
        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Projektordner nicht gefunden: {root}");
        var projects = Directory.EnumerateFiles(root, "*.rfPrj", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var project = RfPrjArchive.Load(path);
                return new TaskMapProject(Path.GetFileName(path), project.ManifestVersion, project.Summarize());
            }).ToArray();
        return new TaskMapDocument("1.0", DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture), projects);
    }
}

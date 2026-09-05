using System.Text.Json;
using System.Text.RegularExpressions;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public enum RFiDGearOperationKind
{
    MasteringManual,
    MasteringAutorun,
    CardPreflightTransport,
    CardPreflightProductive,
    ApplicationAcceptanceProductive,
    RecoveryTransport,
    RecoveryProductive,
    ExtensionRollback,
    CardRecognitionReadOnly,
    CardFormatStandalone,
    ApplicationInventoryReadOnly
}

public enum RFiDGearOperationState { Waiting, Successful, Failed, TimedOut }

public sealed record RFiDGearOperationArtifact(
    RFiDGearOperationKind Kind,
    string Label,
    string ProjectPath,
    bool ReadOnly,
    bool Destructive,
    bool Autorun,
    IReadOnlyList<RfPrjTaskSummary> Tasks);

public sealed record RFiDGearOperationCatalog(string ProjectDirectory, IReadOnlyList<RFiDGearOperationArtifact> Operations)
{
    public RFiDGearOperationArtifact? Find(RFiDGearOperationKind kind) => Operations.FirstOrDefault(item => item.Kind == kind);
}

public sealed record RFiDGearOperationSession(
    RFiDGearOperationArtifact Operation,
    RFiDGearLaunchResult Launch,
    RFiDGearLogSnapshot LogSnapshot,
    DateTimeOffset StartedAt,
    TimeSpan Timeout);

public sealed record RFiDGearOperationEvaluation(
    RFiDGearOperationState State,
    string Summary,
    RFiDGearDiagnosticSignal Diagnostic,
    string SanitizedLog,
    RFiDGearStructuredRunEvidence? StructuredRun = null);

public sealed class RFiDGearOperationService
{
    private readonly IRFiDGearLauncher launcher;
    private readonly RFiDGearLogMonitor logMonitor;

    public RFiDGearOperationService(IRFiDGearLauncher? launcher = null, RFiDGearLogMonitor? logMonitor = null)
    {
        this.launcher = launcher ?? new RFiDGearLauncher();
        this.logMonitor = logMonitor ?? new RFiDGearLogMonitor();
    }

    public RFiDGearOperationCatalog Inspect(string projectDirectory)
    {
        var root = RequireDirectory(projectDirectory);
        var operations = new List<RFiDGearOperationArtifact>();
        var mastering = Directory.EnumerateFiles(root, "*_Mastering.rfPrj", SearchOption.TopDirectoryOnly).SingleOrDefault();
        if (mastering is not null)
        {
            var tasks = LoadAndValidate(mastering, task => true, "Mastering");
            operations.Add(new(RFiDGearOperationKind.MasteringManual, "Mastering manuell öffnen", mastering, false, true, false, tasks));
            operations.Add(new(RFiDGearOperationKind.MasteringAutorun, "Mastering einmal ausführen", mastering, false, true, true, tasks));
        }
        AddCardPreflight(root, operations);
        AddCardRecognition(root, operations);
        AddApplicationAcceptance(root, operations);
        AddRecovery(root, operations);
        return new RFiDGearOperationCatalog(root, operations);
    }

    public async Task<RFiDGearOperationSession> StartAsync(
        string executablePath,
        RFiDGearOperationArtifact operation,
        string? logDirectory = null,
        TimeSpan? timeout = null,
        ProjectSecretPipeServer? secretServer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var snapshot = logMonitor.Snapshot(logDirectory);
        var launch = await launcher.LaunchAsync(new RFiDGearLaunchRequest(
            executablePath, operation.ProjectPath, Autorun: operation.Autorun, AutoMode: false, SecretServer: secretServer), cancellationToken).ConfigureAwait(false);
        return new(operation, launch, snapshot, DateTimeOffset.Now, timeout ?? TimeSpan.FromMinutes(2));
    }

    public RFiDGearOperationEvaluation Evaluate(RFiDGearOperationSession session, DateTimeOffset? now = null, IEnumerable<string>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var delta = logMonitor.ReadDelta(session.LogSnapshot);
        return EvaluateText(session, delta.Text, now ?? DateTimeOffset.Now, knownSecrets);
    }

    public static RFiDGearOperationEvaluation EvaluateText(RFiDGearOperationSession session, string? logText, DateTimeOffset now, IEnumerable<string>? knownSecrets = null)
    {
        var safe = RFiDGearDiagnosticParser.Sanitize(logText, knownSecrets);
        var diagnostic = RFiDGearDiagnosticParser.Parse(logText, knownSecrets);
        var structured = RFiDGearStructuredRunParser.Parse(logText, session.Operation.Tasks);
        var terminals = Regex.Matches(safe, @"(?i)TaskLoop\.(Success|Failure)")
            .Select(match => match.Groups[1].Value).ToArray();
        if (terminals.Length > 0)
        {
            var last = terminals[^1];
            if (last.Equals("Success", StringComparison.OrdinalIgnoreCase))
            {
                if (!structured.Correlated || structured.TaskOutcomes.Count != session.Operation.Tasks.Count)
                    return new(RFiDGearOperationState.Failed,
                        "RFiDGear meldete Erfolg, aber der angeforderte Taskplan wurde nicht vollständig ausgeführt: " + structured.CorrelationError,
                        diagnostic, safe, structured);
                return new(RFiDGearOperationState.Successful, "RFiDGear hat den vollständigen Tasklauf als erfolgreich abgeschlossen.", diagnostic, safe, structured);
            }
            return new(RFiDGearOperationState.Failed,
                diagnostic.HasSignal ? diagnostic.Summary : "RFiDGear hat den Tasklauf als fehlgeschlagen abgeschlossen.", diagnostic, safe, structured);
        }
        if (diagnostic.StrongFailure)
            return new(RFiDGearOperationState.Failed, diagnostic.Summary, diagnostic, safe, structured);
        if (now - session.StartedAt >= session.Timeout)
            return new(RFiDGearOperationState.TimedOut, "Innerhalb des Zeitlimits wurde kein abschließender RFiDGear-Laufstatus protokolliert.", diagnostic, safe, structured);
        return new(RFiDGearOperationState.Waiting,
            session.Operation.Autorun ? "RFiDGear läuft; noch kein abschließender TaskLoop-Status vorhanden." : "RFiDGear wurde zur manuellen Bedienung geöffnet.", diagnostic, safe, structured);
    }

    private static void AddCardPreflight(string root, List<RFiDGearOperationArtifact> operations)
    {
        var planPath = Path.Combine(root, "card-preflight.plan.json");
        if (!File.Exists(planPath)) return;
        using var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        AddPlanned(root, plan.RootElement, "transportProject", RFiDGearOperationKind.CardPreflightTransport,
            "Kartenprüfung · Transportkeys", true, false, tasks => tasks.All(task => task.Type == "ReadData"), operations);
        if (!plan.RootElement.TryGetProperty("productiveProbeAvailable", out var available) || available.ValueKind != JsonValueKind.False)
            AddPlanned(root, plan.RootElement, "productiveProject", RFiDGearOperationKind.CardPreflightProductive,
                "Kartenprüfung · Produktivkeys", true, false, tasks => tasks.All(task => task.Type == "ReadData"), operations);
    }

    private static void AddCardRecognition(string root, List<RFiDGearOperationArtifact> operations)
    {
        var planPath = Path.Combine(root, "card-recognition.plan.json");
        if (!File.Exists(planPath)) return;
        using var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        AddPlanned(root, plan.RootElement, "project", RFiDGearOperationKind.CardRecognitionReadOnly,
            "Transponder erkennen · nur lesend", true, false,
            tasks => tasks.Count >= 4 && tasks[0].Type == "PICCApplicationInventory" &&
                tasks.Skip(1).All(task => task.Type == "PICCAuthCheck"), operations);
    }

    private static void AddRecovery(string root, List<RFiDGearOperationArtifact> operations)
    {
        var planPath = Path.Combine(root, "recovery.plan.json");
        if (!File.Exists(planPath)) return;
        using var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        AddPlanned(root, plan.RootElement, "transportProject", RFiDGearOperationKind.RecoveryTransport,
            "Recovery · Transport-PICC", false, true,
            tasks => tasks.Count == 1 && tasks[0].Type == "FormatDesfireCard", operations);
        AddPlanned(root, plan.RootElement, "productiveProject", RFiDGearOperationKind.RecoveryProductive,
            "Recovery · Produktiv-PICC → Transport", false, true,
            tasks => tasks.Count == 2 && tasks[0].Type == "FormatDesfireCard" &&
                tasks[1].Type == "PICCMasterKeyChangeover" && tasks[1].ConditionIndex == tasks[0].Index && tasks[1].ConditionLevel == "NoError", operations);
        AddPlanned(root, plan.RootElement, "extensionRollbackProject", RFiDGearOperationKind.ExtensionRollback,
            "Erweiterung gezielt zurücknehmen", false, true,
            tasks => tasks.Count > 0 && tasks.All(task => task.Type == "DeleteApplication"), operations);
    }

    private static void AddApplicationAcceptance(string root, List<RFiDGearOperationArtifact> operations)
    {
        var planPath = Path.Combine(root, "acceptance.plan.json");
        if (!File.Exists(planPath)) return;
        using var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        AddPlanned(root, plan.RootElement, "applicationAcceptanceProject", RFiDGearOperationKind.ApplicationAcceptanceProductive,
            "Kartenstruktur-Abnahme · Produktivkeys", true, false,
            IsProductiveAcceptance, operations);
    }

    private static bool IsProductiveAcceptance(IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        if (tasks.Count == 0) return false;
        var position = 0;
        while (position < tasks.Count)
        {
            var settings = tasks[position++];
            if (settings.Type != "ReadAppSettings" || string.IsNullOrWhiteSpace(settings.Aid)) return false;
            if (position >= tasks.Count) return false;
            var count = tasks[position++];
            if (count.Type != "CheckAppKeyCount" || count.Aid != settings.Aid) return false;

            var fileIds = new HashSet<string>(StringComparer.Ordinal);
            while (position < tasks.Count && tasks[position].Type == "CheckFileSettings" && tasks[position].Aid == settings.Aid)
            {
                if (string.IsNullOrWhiteSpace(tasks[position].FileId) || !fileIds.Add(tasks[position].FileId)) return false;
                position++;
            }
            // RFiDGear 2.0.10 cannot deserialize CheckFileSettings.  A compatible
            // acceptance therefore consists of the two application checks only;
            // newer runtimes may append one or more exact file checks.
        }
        return true;
    }

    private static void AddPlanned(string root, JsonElement plan, string property, RFiDGearOperationKind kind,
        string label, bool readOnly, bool destructive, Func<IReadOnlyList<RfPrjTaskSummary>, bool> contract,
        List<RFiDGearOperationArtifact> operations)
    {
        if (!plan.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) return;
        var path = ResolveWithin(root, value.GetString()!);
        var tasks = LoadAndValidate(path, contract, label);
        operations.Add(new(kind, label, path, readOnly, destructive, true, tasks));
    }

    private static IReadOnlyList<RfPrjTaskSummary> LoadAndValidate(string path, Func<IReadOnlyList<RfPrjTaskSummary>, bool> contract, string label)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"{label}: RFiDGear-Projekt fehlt.", path);
        var document = RfPrjArchive.Load(path);
        var tasks = document.Summarize();
        if (tasks.Count == 0) throw new InvalidDataException($"{label}: RFiDGear-Projekt enthält keine Tasks.");
        if (!contract(tasks)) throw new InvalidDataException($"{label}: Taskfolge verletzt den Sicherheitsvertrag.");
        var dependencies = ProjectPreflight.ValidateDependencies(tasks);
        if (dependencies.Count > 0) throw new InvalidDataException($"{label}: Taskabhängigkeiten sind ungültig: {dependencies[0].Message}");
        var paths = ProjectPreflight.ValidateIoPaths(tasks, Path.GetDirectoryName(path)!, requireInputFiles: true);
        if (paths.Count > 0) throw new InvalidDataException($"{label}: Datei-/Readbackpfade sind nicht projektlokal: {paths[0].Message}");
        return tasks;
    }

    private static string ResolveWithin(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("Plan darf keinen absoluten Projektpfad enthalten.");
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Planpfad verlässt den Projektordner.");
        return path;
    }

    private static string RequireDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("Projektordner nicht gefunden: " + full);
        return full;
    }
}

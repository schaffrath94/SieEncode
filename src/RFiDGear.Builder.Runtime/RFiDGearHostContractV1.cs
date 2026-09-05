using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public enum RFiDGearHostModeV1 { Open, Autorun, Automode }
public enum RFiDGearHostStateV1 { Requested, Running, Successful, Failed, TimedOut, Cancelled }

public sealed record RFiDGearHostTaskV1(
    int Position, string Index, string Type, string ConditionIndex, string ConditionLevel,
    string Aid, string FileId, string Input, string Output, string FileSize);

public sealed record RFiDGearHostTaskOutcomeV1(
    int Position, string Index, string Type, string Decision, string SkipReason,
    string Outcome, string ErrorCode, string Timestamp);

public sealed record RFiDGearHostRequestV1(
    string SchemaVersion, string ContractId, string RequestId, string CreatedAt,
    string Operation, string ProjectName, string ProjectFile, string ProjectDirectory,
    string ProjectFileSha256, string Mode, string TaskPlanSha256, int ExpectedTaskCount,
    IReadOnlyList<RFiDGearHostTaskV1> Tasks,
    IReadOnlyList<string> RequiredCapabilities, IReadOnlyList<string> AvailableCapabilities,
    string EngineVersion, bool ContainsKeys, bool ContainsCardData, bool ReferencedProjectMayContainKeys);

public sealed record RFiDGearHostResultV1(
    string SchemaVersion, string ContractId, string RequestId, string CreatedAt,
    string State, string AcceptanceState, bool Accepted, int? ProcessId,
    string RunIdHash, string TerminalEvent, int TaskOutcomeEvents,
    int FailedOutcomes, int SkippedOutcomes, IReadOnlyList<RFiDGearHostTaskOutcomeV1> TaskOutcomes,
    string FailureCategory, int? FailedTaskExecutionIndex, string FailedTaskType,
    string Summary, bool ContainsKeys, bool ContainsCardData);

public sealed record RFiDGearHostRunV1(
    string ProjectDirectory, string ProjectPath, string RequestPath, string ResultPath,
    RFiDGearHostRequestV1 Request, DateTimeOffset StartedAt);

public sealed class RFiDGearHostContractServiceV1
{
    public const string SchemaVersion = "1.1";
    public const string ContractId = "de.rfidgear.host.v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "coding", "mastering", "card-preflight-transport", "card-preflight-productive",
        "application-acceptance-productive", "recovery-transport", "recovery-productive"
        , "card-recognition-readonly", "card-format-standalone", "application-inventory-readonly"
    };
    private static readonly HashSet<string> AcceptanceStates = new(StringComparer.Ordinal)
        { "waiting", "successful", "failed", "not-evaluated", "not-applicable" };

    public RFiDGearHostRunV1 Begin(
        string projectDirectory,
        string projectPath,
        string projectName,
        RFiDGearHostModeV1 mode,
        IReadOnlyList<RfPrjTaskSummary> tasks,
        RFiDGearCapabilityReport capabilities,
        DateTimeOffset? now = null,
        string? requestId = null,
        string operation = "coding")
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(capabilities);
        var root = RequireDirectory(projectDirectory);
        var project = RequireChildFile(root, projectPath, "RFiDGear-Projekt");
        if (string.IsNullOrWhiteSpace(projectName)) throw new InvalidDataException("Projektname fehlt im Host-Vertrag.");
        if (!Operations.Contains(operation)) throw new InvalidDataException("Unbekannte Host-Operation: " + operation);
        var id = requestId ?? Guid.NewGuid().ToString("D");
        if (!Guid.TryParseExact(id, "D", out _)) throw new InvalidDataException("requestId muss eine GUID im D-Format sein.");
        var stamp = now ?? DateTimeOffset.Now;
        var normalizedTasks = tasks.Select(task => NormalizeTask(root, task)).ToArray();
        if (normalizedTasks.Length == 0) throw new InvalidDataException("Host-Vertrag benötigt mindestens einen Task.");
        var required = RequiredCapabilities(mode, tasks).Select(value => value.ToString()).ToArray();
        var available = capabilities.Capabilities.Where(value => value.State == RFiDGearCapabilityState.Available)
            .Select(value => value.Id.ToString()).Order(StringComparer.Ordinal).ToArray();
        var request = new RFiDGearHostRequestV1(
            SchemaVersion, ContractId, id, stamp.ToString("O"), operation, projectName, Path.GetFileName(project), ".",
            FileHash(project), mode.ToString().ToLowerInvariant(), TaskHash(normalizedTasks), normalizedTasks.Length, normalizedTasks,
            required, available, capabilities.Version.Version?.ToString() ?? capabilities.Version.ProductVersion ?? "unknown",
            false, false, true);
        Validate(request);
        var directory = Path.Combine(root, "integration", "host-v1", "runs");
        Directory.CreateDirectory(directory);
        var requestPath = Path.Combine(directory, id + ".request.json");
        var resultPath = Path.Combine(directory, id + ".result.json");
        if (File.Exists(requestPath) || File.Exists(resultPath)) throw new InvalidOperationException("requestId wurde im Projekt bereits verwendet: " + id);
        WriteAtomic(requestPath, request);
        return new(root, project, requestPath, resultPath, request, stamp);
    }

    public RFiDGearHostResultV1 MarkRunning(RFiDGearHostRunV1 run, RFiDGearLaunchResult launch, DateTimeOffset? now = null)
        => WriteResult(run, launch.ProcessId, RFiDGearHostStateV1.Running, "waiting", "", "", [], "", "RFiDGear-Prozess gestartet; fachliches Ergebnis ausstehend.", now);

    public RFiDGearHostResultV1 Complete(
        RFiDGearHostRunV1 run,
        RFiDGearLaunchResult? launch,
        CodingRunEvaluation evaluation,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var structured = evaluation.StructuredRun ?? RFiDGearStructuredRunEvidence.Empty;
        var engine = EngineState(structured, evaluation.State == CodingRunState.Failed, timedOut: false);
        var acceptance = evaluation.State switch
        {
            CodingRunState.Successful => "successful",
            CodingRunState.Failed => "failed",
            _ => "waiting"
        };
        var category = structured.Correlated || engine == RFiDGearHostStateV1.Running
            ? evaluation.FailureCategory : "Host-Korrelation";
        return WriteResult(run, launch?.ProcessId, engine, acceptance, structured.RunIdHash,
            structured.TerminalEvent, Outcomes(structured), category,
            evaluation.State == CodingRunState.Successful
                ? "RFiDGear-Lauf und finale projektlokale Readbacks sind erfolgreich."
                : evaluation.State == CodingRunState.Failed
                    ? "RFiDGear-Lauf oder finale projektlokale Readback-Abnahme ist fehlgeschlagen."
                    : "Finale projektlokale Readback-Abnahme ist noch nicht vollständig.", now);
    }

    public RFiDGearHostResultV1 CompleteOperation(
        RFiDGearHostRunV1 run,
        RFiDGearOperationSession session,
        RFiDGearOperationEvaluation evaluation,
        string acceptanceState = "not-evaluated",
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(evaluation);
        if (!AcceptanceStates.Contains(acceptanceState)) throw new InvalidDataException("Unbekannter Consumer-Abnahmestatus.");
        var structured = evaluation.StructuredRun ?? RFiDGearStructuredRunEvidence.Empty;
        var engine = evaluation.State switch
        {
            RFiDGearOperationState.Successful => EngineState(structured, failed: false, timedOut: false),
            RFiDGearOperationState.Failed => EngineState(structured, failed: true, timedOut: false),
            RFiDGearOperationState.TimedOut => RFiDGearHostStateV1.TimedOut,
            _ => RFiDGearHostStateV1.Running
        };
        var category = structured.Correlated || engine is RFiDGearHostStateV1.Running or RFiDGearHostStateV1.TimedOut
            ? evaluation.Diagnostic.Category : "Host-Korrelation";
        return WriteResult(run, session.Launch.ProcessId, engine, acceptanceState, structured.RunIdHash,
            structured.TerminalEvent, Outcomes(structured), category, evaluation.Summary, now);
    }

    public RFiDGearHostResultV1 FailStart(RFiDGearHostRunV1 run, string category, DateTimeOffset? now = null)
        => WriteResult(run, null, RFiDGearHostStateV1.Failed, "not-applicable", "", "", [], category, "RFiDGear-Prozessstart fehlgeschlagen.", now);

    public RFiDGearHostResultV1 Cancel(RFiDGearHostRunV1 run, int? processId, string summary, DateTimeOffset? now = null)
        => WriteResult(run, processId, RFiDGearHostStateV1.Cancelled, "not-applicable", "", "", [], "", summary, now);

    public static void Validate(RFiDGearHostRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != SchemaVersion || request.ContractId != ContractId) throw new InvalidDataException("Unbekannte Host-Vertragsversion.");
        if (!Guid.TryParseExact(request.RequestId, "D", out _)) throw new InvalidDataException("Ungültige requestId.");
        if (!Operations.Contains(request.Operation)) throw new InvalidDataException("Unbekannte Host-Operation.");
        if (Path.GetFileName(request.ProjectFile) != request.ProjectFile || request.ProjectDirectory != ".") throw new InvalidDataException("Host-Projektpfad ist nicht portabel/projektlokal.");
        if (!HexHash(request.ProjectFileSha256) || !HexHash(request.TaskPlanSha256)) throw new InvalidDataException("Host-Projekt- oder Taskplanfingerprint ist ungültig.");
        if (request.ContainsKeys || request.ContainsCardData || !request.ReferencedProjectMayContainKeys) throw new InvalidDataException("Host-Request verletzt den Datenschutzvertrag.");
        if (request.ExpectedTaskCount != request.Tasks.Count || request.Tasks.Count == 0) throw new InvalidDataException("Taskanzahl im Host-Request ist inkonsistent.");
        if (request.Mode is not ("open" or "autorun" or "automode")) throw new InvalidDataException("Unbekannter Host-Ausführungsmodus.");
        if (request.Tasks.Select(task => task.Position).Distinct().Count() != request.Tasks.Count || request.Tasks.Any(task => !PortableRelative(task.Input) || !PortableRelative(task.Output)))
            throw new InvalidDataException("Host-Taskplan enthält doppelte Positionen oder einen nicht portablen I/O-Pfad.");
        if (!string.Equals(request.TaskPlanSha256, TaskHash(request.Tasks), StringComparison.Ordinal)) throw new InvalidDataException("Taskplan-Hash im Host-Request ist ungültig.");
        RequireCapability(request, RFiDGearCapabilityId.CustomProjectFile);
        if (request.Mode == "autorun") RequireCapability(request, RFiDGearCapabilityId.AutorunOnce);
        if (request.Mode == "automode") RequireCapability(request, RFiDGearCapabilityId.ContinuousAutoMode);
        if (request.Mode != "open") RequireCapability(request, RFiDGearCapabilityId.StructuredTaskLogging);
        if (request.Tasks.Any(task => task.Type == "ReadAppSettings")) RequireCapability(request, RFiDGearCapabilityId.ExactAppSettingsCheck);
        if (request.Tasks.Any(task => task.Type == "CheckAppKeyCount")) RequireCapability(request, RFiDGearCapabilityId.AppKeyCountCheck);
        if (request.Tasks.Any(task => task.Type == "CheckFileSettings")) RequireCapability(request, RFiDGearCapabilityId.FileSettingsCheck);
        if (request.Tasks.Any(task => task.Type == "ApplicationFileInventory")) RequireCapability(request, RFiDGearCapabilityId.ApplicationFileInventory);
        if (request.Tasks.Any(task => task.Type is "ApplicationKeyChangeover" or "ApplicationKeySettingsChangeover" or "PICCMasterKeyChangeover" or "PICCMasterKeySettingsChangeover"))
            RequireCapability(request, RFiDGearCapabilityId.VerifiedApplicationKeyChange);
        if (request.RequiredCapabilities.Distinct(StringComparer.Ordinal).Count() != request.RequiredCapabilities.Count ||
            request.AvailableCapabilities.Distinct(StringComparer.Ordinal).Count() != request.AvailableCapabilities.Count)
            throw new InvalidDataException("Host-Capability-Liste enthält Duplikate.");
        if (request.RequiredCapabilities.Concat(request.AvailableCapabilities).Any(value => !Enum.TryParse<RFiDGearCapabilityId>(value, out _)))
            throw new InvalidDataException("Host-Capability-Liste enthält unbekannte Werte.");
        if (request.RequiredCapabilities.Any(required => !request.AvailableCapabilities.Contains(required, StringComparer.Ordinal)))
            throw new InvalidDataException("Host-Request verlangt eine nicht nachgewiesene Fähigkeit.");
    }

    public static void Validate(RFiDGearHostResultV1 result, RFiDGearHostRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);
        if (result.SchemaVersion != SchemaVersion || result.ContractId != ContractId || result.RequestId != request.RequestId)
            throw new InvalidDataException("Host-Resultat ist nicht mit dem Request korreliert.");
        if (result.ContainsKeys || result.ContainsCardData) throw new InvalidDataException("Host-Resultat verletzt den Datenschutzvertrag.");
        if (!Enum.TryParse<RFiDGearHostStateV1>(result.State, true, out _)) throw new InvalidDataException("Host-Resultat enthält einen unbekannten Status.");
        if (!AcceptanceStates.Contains(result.AcceptanceState)) throw new InvalidDataException("Host-Resultat enthält einen unbekannten Abnahmestatus.");
        if (result.Accepted != (result.State == "successful" && result.AcceptanceState == "successful"))
            throw new InvalidDataException("Host-Gesamtannahme ist nicht aus Engine- und Consumerstatus abgeleitet.");
        if (result.TaskOutcomeEvents != result.TaskOutcomes.Count || result.FailedOutcomes < 0 || result.SkippedOutcomes < 0 ||
            result.FailedOutcomes != result.TaskOutcomes.Count(item => item.Outcome == "Failed") ||
            result.SkippedOutcomes != result.TaskOutcomes.Count(item => item.Decision == "Skipped" || item.Outcome == "Skipped"))
            throw new InvalidDataException("Host-Resultat enthält inkonsistente Taskzähler.");
        if (result.TaskOutcomes.Select(item => item.Position).Distinct().Count() != result.TaskOutcomes.Count)
            throw new InvalidDataException("Host-Resultat enthält doppelte Taskpositionen.");
        foreach (var outcome in result.TaskOutcomes)
        {
            var expected = request.Tasks.SingleOrDefault(task => task.Position == outcome.Position)
                ?? throw new InvalidDataException("Host-Resultat enthält eine unbekannte Taskposition.");
            if (outcome.Index != expected.Index || outcome.Type != expected.Type || !SafeToken(outcome.Decision) ||
                !SafeToken(outcome.SkipReason) || !SafeToken(outcome.Outcome) || !SafeToken(outcome.ErrorCode))
                throw new InvalidDataException("Host-Taskresultat passt nicht zum Request oder enthält unzulässige Freitexte.");
        }
        if ((result.TaskOutcomes.Count > 0 || result.TerminalEvent.Length > 0) && !HexHash(result.RunIdHash))
            throw new InvalidDataException("Host-Resultat besitzt keine sichere RunId-Korrelation.");
        if (result.State == "successful" && (result.TerminalEvent != "TaskLoop.Success" || result.TaskOutcomes.Count != request.ExpectedTaskCount))
            throw new InvalidDataException("RFiDGear-Erfolg besitzt kein vollständiges korreliertes Terminal-/Taskresultat.");
    }

    private RFiDGearHostResultV1 WriteResult(RFiDGearHostRunV1 run, int? processId, RFiDGearHostStateV1 state,
        string acceptance, string runIdHash, string terminal, IReadOnlyList<RFiDGearHostTaskOutcomeV1> outcomes,
        string category, string summary, DateTimeOffset? now)
    {
        ArgumentNullException.ThrowIfNull(run);
        EnsureRequestAndProjectUnchanged(run);
        var failedTasks = outcomes.Where(item => item.Outcome == "Failed").ToArray();
        var rootFailure = state == RFiDGearHostStateV1.Failed
            ? failedTasks.FirstOrDefault(item => !IsAcceptedControlFlowFailure(run.Request, item))
            : null;
        var result = new RFiDGearHostResultV1(SchemaVersion, ContractId, run.Request.RequestId,
            (now ?? DateTimeOffset.Now).ToString("O"), state.ToString().ToLowerInvariant(), acceptance,
            state == RFiDGearHostStateV1.Successful && acceptance == "successful", processId,
            runIdHash, terminal, outcomes.Count, failedTasks.Length,
            outcomes.Count(item => item.Decision == "Skipped" || item.Outcome == "Skipped"), outcomes,
            category ?? "", rootFailure?.Position, rootFailure?.Type ?? "",
            summary, false, false);
        Validate(result, run.Request);
        ValidateTransition(run.ResultPath, result);
        WriteAtomic(run.ResultPath, result);
        return result;
    }

    private static bool IsAcceptedControlFlowFailure(RFiDGearHostRequestV1 request, RFiDGearHostTaskOutcomeV1 outcome)
        => request.Tasks.Any(task =>
            string.Equals(task.ConditionIndex, outcome.Index, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(task.ConditionLevel, outcome.ErrorCode, StringComparison.OrdinalIgnoreCase));

    private static void ValidateTransition(string resultPath, RFiDGearHostResultV1 next)
    {
        if (!File.Exists(resultPath)) return;
        var current = JsonSerializer.Deserialize<RFiDGearHostResultV1>(File.ReadAllText(resultPath), JsonOptions)
            ?? throw new InvalidDataException("Persistiertes Host-Resultat ist leer.");
        if (current.RequestId != next.RequestId) throw new InvalidDataException("Persistiertes Host-Resultat gehört zu einem anderen Request.");
        var terminal = current.State is "successful" or "failed" or "timedout" or "cancelled";
        if (!terminal) return;
        if (current.State != next.State || current.RunIdHash != next.RunIdHash || current.TerminalEvent != next.TerminalEvent ||
            !current.TaskOutcomes.SequenceEqual(next.TaskOutcomes))
            throw new InvalidOperationException("Ein terminales Host-Resultat darf nicht auf einen anderen Engine-Lauf zurückgesetzt oder umgeschrieben werden.");
        if ((current.AcceptanceState is "successful" or "failed" or "not-applicable") && current.AcceptanceState != next.AcceptanceState)
            throw new InvalidOperationException("Ein finaler Consumer-Abnahmestatus darf nicht nachträglich geändert werden.");
    }

    private static RFiDGearHostStateV1 EngineState(RFiDGearStructuredRunEvidence structured, bool failed, bool timedOut)
    {
        if (timedOut) return RFiDGearHostStateV1.TimedOut;
        if (!structured.Correlated && structured.TerminalEvent.Length > 0) return RFiDGearHostStateV1.Failed;
        if (structured.TerminalEvent == "TaskLoop.Success") return RFiDGearHostStateV1.Successful;
        if (structured.TerminalEvent == "TaskLoop.Failure" || failed) return RFiDGearHostStateV1.Failed;
        return RFiDGearHostStateV1.Running;
    }

    private static IReadOnlyList<RFiDGearHostTaskOutcomeV1> Outcomes(RFiDGearStructuredRunEvidence structured)
        => structured.TaskOutcomes.Select(item => new RFiDGearHostTaskOutcomeV1(item.Position, item.Index, item.Type,
            item.Decision, item.SkipReason, item.Outcome, item.ErrorCode, item.Timestamp)).ToArray();

    private static void EnsureRequestAndProjectUnchanged(RFiDGearHostRunV1 run)
    {
        if (!File.Exists(run.RequestPath) || File.ReadAllText(run.RequestPath) != JsonSerializer.Serialize(run.Request, JsonOptions))
            throw new InvalidDataException("Persistierter Host-Request wurde nach Annahme verändert.");
        if (!File.Exists(run.ProjectPath) || FileHash(run.ProjectPath) != run.Request.ProjectFileSha256)
            throw new InvalidDataException("Referenziertes rfPrj wurde nach Annahme des Host-Requests verändert.");
    }

    private static void RequireCapability(RFiDGearHostRequestV1 request, RFiDGearCapabilityId capability)
    {
        if (!request.RequiredCapabilities.Contains(capability.ToString(), StringComparer.Ordinal))
            throw new InvalidDataException($"Pflichtfähigkeit {capability} fehlt im Host-Request.");
    }

    private static RFiDGearHostTaskV1 NormalizeTask(string root, RfPrjTaskSummary task)
        => new(task.Position, task.Index, task.Type, task.ConditionIndex, task.ConditionLevel,
            task.Aid, task.FileId, RelativePath(root, task.InputPath), RelativePath(root, task.OutputPath), task.FileSize);

    private static string RelativePath(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var full = Path.GetFullPath(path);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Task-I/O-Pfad verlässt den Projektordner: " + full);
        return Path.GetRelativePath(root, full).Replace('\\', '/');
    }

    private static IReadOnlyList<RFiDGearCapabilityId> RequiredCapabilities(RFiDGearHostModeV1 mode, IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        var result = new List<RFiDGearCapabilityId> { RFiDGearCapabilityId.CustomProjectFile };
        if (mode == RFiDGearHostModeV1.Autorun) result.Add(RFiDGearCapabilityId.AutorunOnce);
        if (mode == RFiDGearHostModeV1.Automode) result.Add(RFiDGearCapabilityId.ContinuousAutoMode);
        if (mode != RFiDGearHostModeV1.Open) result.Add(RFiDGearCapabilityId.StructuredTaskLogging);
        if (tasks.Any(task => task.Type == "ReadAppSettings")) result.Add(RFiDGearCapabilityId.ExactAppSettingsCheck);
        if (tasks.Any(task => task.Type == "CheckAppKeyCount")) result.Add(RFiDGearCapabilityId.AppKeyCountCheck);
        if (tasks.Any(task => task.Type == "CheckFileSettings")) result.Add(RFiDGearCapabilityId.FileSettingsCheck);
        if (tasks.Any(task => task.Type == "ApplicationFileInventory")) result.Add(RFiDGearCapabilityId.ApplicationFileInventory);
        if (RFiDGearKeyChangeSafety.RequiresVerifiedKeyChange(tasks)) result.Add(RFiDGearCapabilityId.VerifiedApplicationKeyChange);
        return result.Distinct().ToArray();
    }

    private static bool PortableRelative(string path)
    {
        if (string.IsNullOrEmpty(path)) return true;
        if (Path.IsPathRooted(path) || path.Contains('\\')) return false;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(part => part is not "." and not "..");
    }

    private static bool SafeToken(string value) => string.IsNullOrEmpty(value) || Regex.IsMatch(value, "^[A-Za-z0-9_.-]{1,80}$", RegexOptions.CultureInvariant);
    private static bool HexHash(string value) => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string TaskHash(IReadOnlyList<RFiDGearHostTaskV1> tasks)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(tasks, JsonOptions)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string RequireDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("Projektordner fehlt: " + full);
        return full;
    }

    private static string RequireChildFile(string root, string path, string label)
    {
        var full = Path.GetFullPath(path);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new FileNotFoundException(label + " fehlt oder verlässt den Projektordner.", full);
        return full;
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, value, JsonOptions);
            stream.Flush(true);
            stream.Close();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

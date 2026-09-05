using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearStructuredTaskOutcome(
    int Position, string Index, string Type, string Decision, string SkipReason,
    string Outcome, string ErrorCode, string Timestamp)
{
    public IReadOnlyList<string> PiccApplicationIds { get; init; } = [];
    public bool? PiccAuthMatched { get; init; }
    public string PiccAuthResult { get; init; } = "";
    public int? PiccKeySettings { get; init; }
    public int? PiccFreeMemoryBytes { get; init; }
    public bool? PiccCreateDeleteWithoutMasterKey { get; init; }
}

public sealed record RFiDGearStructuredRunEvidence(
    string RunIdHash, string TerminalEvent,
    IReadOnlyList<RFiDGearStructuredTaskOutcome> TaskOutcomes,
    bool Correlated, string CorrelationError)
{
    public static RFiDGearStructuredRunEvidence Empty { get; } = new("", "", [], false, "Keine strukturierten RFiDGear-Laufereignisse vorhanden.");
}

public static class RFiDGearStructuredRunParser
{
    private sealed record ParsedEvent(string Stage, string RunId, JsonElement Details);

    public static RFiDGearStructuredRunEvidence Parse(string? rawLog, IReadOnlyList<RfPrjTaskSummary> taskMap)
    {
        ArgumentNullException.ThrowIfNull(taskMap);
        var events = ParseEvents(rawLog).ToArray();
        if (events.Length == 0) return RFiDGearStructuredRunEvidence.Empty;

        var terminalIndex = Array.FindLastIndex(events, item => item.Stage is "TaskLoop.Success" or "TaskLoop.Failure");
        var terminal = terminalIndex >= 0 ? events[terminalIndex] : null;
        var priorTerminalIndex = terminalIndex > 0
            ? Array.FindLastIndex(events, terminalIndex - 1, terminalIndex, item => item.Stage is "TaskLoop.Success" or "TaskLoop.Failure")
            : -1;
        var segmentEnd = terminalIndex >= 0 ? terminalIndex : events.Length - 1;
        var segment = events.Skip(priorTerminalIndex + 1).Take(segmentEnd - priorTerminalIndex).ToArray();
        var selectedRunId = terminal?.RunId;
        if (string.IsNullOrWhiteSpace(selectedRunId))
            selectedRunId = segment.LastOrDefault(item => item.Stage == "Task.Outcome" && !string.IsNullOrWhiteSpace(item.RunId))?.RunId;
        if (string.IsNullOrWhiteSpace(selectedRunId))
            return new("", terminal?.Stage ?? "", [], false, "Strukturierte RFiDGear-Ereignisse enthalten keine RunId.");
        if (segment.Any(item => string.IsNullOrWhiteSpace(item.RunId) || item.RunId != selectedRunId))
            return new(Hash(selectedRunId), terminal?.Stage ?? "", [], false, "Task- und Terminalereignisse sind nicht auf genau eine RunId korreliert.");

        var outcomes = new List<RFiDGearStructuredTaskOutcome>();
        var positions = new HashSet<int>();
        foreach (var item in segment.Where(item => item.Stage == "Task.Outcome"))
        {
            var position = Int(item.Details, "TaskPosition") ?? Int(item.Details, "TaskExecutionIndex") ?? Int(item.Details, "ExecutionIndex");
            if (position is not int execution || execution < 0 || execution >= taskMap.Count)
                return Failure(selectedRunId, terminal, outcomes, "Task.Outcome enthält keine gültige TaskPosition.");
            if (!positions.Add(execution))
                return Failure(selectedRunId, terminal, outcomes, $"TaskPosition {execution} ist im Lauf doppelt enthalten.");
            var expected = taskMap[execution];
            var index = Text(item.Details, "TaskId");
            if (index.Length == 0) index = Text(item.Details, "CurrentTaskIndex");
            if (index.Length == 0) index = expected.Index;
            var type = Text(item.Details, "TaskType");
            if (type.Length == 0) type = expected.Type;
            if (index != expected.Index || type != expected.Type)
                return Failure(selectedRunId, terminal, outcomes, $"Task.Outcome an Position {execution} passt nicht zum angeforderten Taskplan.");
            var parsedOutcome = new RFiDGearStructuredTaskOutcome(execution, index, type, Text(item.Details, "Decision"),
                Text(item.Details, "SkipReason"), Text(item.Details, "Outcome"),
                Text(item.Details, "ErrorCode"), Text(item.Details, "Timestamp"));
            if (item.Details.TryGetProperty("IO", out var io) && io.ValueKind == JsonValueKind.Object)
            {
                parsedOutcome = parsedOutcome with
                {
                    PiccApplicationIds = StringArray(io, "PICCApplicationIds"),
                    PiccAuthMatched = Boolean(io, "PICCAuthMatched"),
                    PiccAuthResult = Text(io, "PICCAuthResult"),
                    PiccKeySettings = Int(io, "PICCKeySettings"),
                    PiccFreeMemoryBytes = Int(io, "PICCFreeMemoryBytes"),
                    PiccCreateDeleteWithoutMasterKey = Boolean(io, "PICCCreateDeleteWithoutMasterKey")
                };
            }
            outcomes.Add(parsedOutcome);
        }

        var summary = segment.LastOrDefault(item => item.Stage == "TaskLoop.Summary");
        if (summary is not null)
        {
            var total = Int(summary.Details, "TotalTasks");
            var executed = Int(summary.Details, "Executed");
            if (total != taskMap.Count)
                return Failure(selectedRunId, terminal, outcomes, $"TaskLoop.Summary meldet {total?.ToString(CultureInfo.InvariantCulture) ?? "keine"} statt {taskMap.Count} erwarteter Tasks.");
            if (executed is int executedCount && executedCount != outcomes.Count)
                return Failure(selectedRunId, terminal, outcomes, $"TaskLoop.Summary meldet {executedCount} ausgeführte Tasks, aber {outcomes.Count} korrelierte Taskresultate.");
        }
        if (terminal is not null && outcomes.Count != taskMap.Count)
            return Failure(selectedRunId, terminal, outcomes, $"Terminaler Lauf enthält {outcomes.Count} statt {taskMap.Count} erwarteter Taskresultate.");

        var correlated = segment.Length > 0 && segment.All(item => item.RunId == selectedRunId);
        return new(Hash(selectedRunId), terminal?.Stage ?? "", outcomes.OrderBy(item => item.Position).ToArray(),
            correlated, correlated ? "" : "Task- und Terminalereignisse sind nicht auf eine RunId korreliert.");
    }

    private static RFiDGearStructuredRunEvidence Failure(string runId, ParsedEvent? terminal,
        IReadOnlyList<RFiDGearStructuredTaskOutcome> outcomes, string error)
        => new(Hash(runId), terminal?.Stage ?? "", outcomes, false, error);

    private static IEnumerable<ParsedEvent> ParseEvents(string? rawLog)
    {
        foreach (var line in (rawLog ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var start = line.IndexOf('{');
            if (start < 0) continue;
            var parsed = TryParseEvent(line[start..]);
            if (parsed is not null) yield return parsed;
        }
    }

    private static ParsedEvent? TryParseEvent(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var stage = Text(root, "Stage");
            if (stage is not ("Task.Outcome" or "TaskLoop.Summary" or "TaskLoop.Success" or "TaskLoop.Failure")) return null;
            var details = root.TryGetProperty("Details", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : root;
            return new(stage, Text(details, "RunId"), details.Clone());
        }
        catch (JsonException) { return null; }
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("de.rfidgear.host.v1/run/" + value)));

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static int? Int(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static bool? Boolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> StringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
            .Where(item => item.Length > 0).ToArray();
    }
}

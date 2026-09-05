using System.Text;
using System.Text.RegularExpressions;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearLogSnapshot(DateTimeOffset CreatedAt, string LogDirectory, IReadOnlyDictionary<string, long> Files);
public sealed record RFiDGearLogDelta(string Text, IReadOnlyList<string> Paths)
{
    public bool HasContent => !string.IsNullOrWhiteSpace(Text);
}

public sealed record RFiDGearDiagnosticSignal(
    bool HasSignal,
    bool StrongFailure,
    string Category,
    string Confidence,
    string Summary,
    int? TaskExecutionIndex,
    string Stage,
    string SanitizedText);

public sealed class RFiDGearLogMonitor
{
    public static string DefaultLogDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "log");

    public RFiDGearLogSnapshot Snapshot(string? logDirectory = null)
    {
        var directory = Path.GetFullPath(logDirectory ?? DefaultLogDirectory);
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(directory))
            foreach (var path in Directory.EnumerateFiles(directory, "log-*.txt", SearchOption.TopDirectoryOnly))
                try { files[Path.GetFullPath(path)] = new FileInfo(path).Length; } catch (IOException) { }
        return new RFiDGearLogSnapshot(DateTimeOffset.Now, directory, files);
    }

    public RFiDGearLogDelta ReadDelta(RFiDGearLogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var parts = new List<string>();
        var paths = new List<string>();
        if (!Directory.Exists(snapshot.LogDirectory)) return new RFiDGearLogDelta("", paths);
        foreach (var path in Directory.EnumerateFiles(snapshot.LogDirectory, "log-*.txt", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            snapshot.Files.TryGetValue(Path.GetFullPath(path), out var offset);
            var text = ReadTail(path, offset);
            if (string.IsNullOrWhiteSpace(text)) continue;
            parts.Add(text);
            paths.Add(Path.GetFullPath(path));
        }
        return new RFiDGearLogDelta(string.Join(Environment.NewLine, parts), paths);
    }

    private static string ReadTail(string path, long offset)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (offset < 0 || offset > stream.Length) offset = 0;
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: false);
            return reader.ReadToEnd();
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }
}

public static class RFiDGearDiagnosticParser
{
    private static readonly Regex Hex24Separated = new(@"(?i)(?<![0-9A-F])(?:[0-9A-F]{2}[\s:-]+){23}[0-9A-F]{2}(?![0-9A-F])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Hex16Separated = new(@"(?i)(?<![0-9A-F])(?:[0-9A-F]{2}[\s:-]+){15}[0-9A-F]{2}(?![0-9A-F])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Hex24 = new(@"(?i)(?<![0-9A-F])[0-9A-F]{48}(?![0-9A-F])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Hex16 = new(@"(?i)(?<![0-9A-F])[0-9A-F]{32}(?![0-9A-F])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex StageRegex = new("(?i)\"Stage\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex TaskIndexRegex = new("(?i)\"CurrentTaskIndex\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string Sanitize(string? text, IEnumerable<string>? knownSecrets = null)
    {
        var safe = text ?? "";
        foreach (var secret in knownSecrets ?? [])
        {
            if (string.IsNullOrWhiteSpace(secret)) continue;
            var compact = new string(secret.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (compact.Length < 16) continue;
            safe = Regex.Replace(safe, Regex.Escape(secret), "[REDACTED_SECRET]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            safe = Regex.Replace(safe, Regex.Escape(compact), "[REDACTED_SECRET]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        safe = Hex24Separated.Replace(safe, "[REDACTED_HEX_24B]");
        safe = Hex16Separated.Replace(safe, "[REDACTED_HEX_16B]");
        safe = Hex24.Replace(safe, "[REDACTED_HEX_24B]");
        return Hex16.Replace(safe, "[REDACTED_HEX_16B]");
    }

    public static RFiDGearDiagnosticSignal Parse(string? text, IEnumerable<string>? knownSecrets = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Empty("");
        var safe = Sanitize(text, knownSecrets);
        var lines = Regex.Split(safe, "\\r?\\n");
        var candidate = FindLast(lines, line => Regex.IsMatch(line, "(?i)\"Stage\"\\s*:\\s*\"(?:TaskLoop|DeviceDiscovery|ChipHydration|SelectionSync)\\.Failure\"", RegexOptions.CultureInvariant), out var strong);
        if (candidate is null)
        {
            candidate = FindLast(lines, line =>
                Regex.IsMatch(line, @"(?i)Unhandled (?:dispatcher|domain) exception|Unobserved task exception|\[FTL\]", RegexOptions.CultureInvariant) ||
                Regex.IsMatch(line, "(?i)\\[ERR\\].*\"Stage\"\\s*:\\s*\"ExecuteOnceAsync\"", RegexOptions.CultureInvariant) ||
                Regex.IsMatch(line, @"(?i)\[ERR\].*(AuthFailure|TransportError|PermissionDenied|ProtocolConstraint)", RegexOptions.CultureInvariant), out strong);
        }
        if (candidate is null)
        {
            candidate = FindLast(lines, line => Regex.IsMatch(line,
                @"(?i)AuthFailure|AuthenticationError|TransportError|PermissionDenied|ProtocolConstraint|TaskLoop\.Failure|ExecuteOnceAsync|Unhandled .*exception|\[FTL\]|\[ERR\].*(DESFire|Mifare|Reader|Elatec|LibLogicalAccess|TaskExecution|TaskLoop|ExecuteOnceAsync)", RegexOptions.CultureInvariant), out _);
            strong = false;
        }
        if (candidate is null) return Empty(safe);
        var category = Classify(candidate);
        var stage = StageRegex.Match(candidate) is { Success: true } stageMatch ? stageMatch.Groups[1].Value : "";
        int? taskIndex = TaskIndexRegex.Match(candidate) is { Success: true } indexMatch ? int.Parse(indexMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
        var summary = strong ? "RFiDGear meldet einen eindeutigen Ausführungsfehler." : "RFiDGear-Log enthält einen möglichen Fehlerhinweis.";
        if (stage.Length > 0) summary += $" Stage: {stage}.";
        if (taskIndex is not null) summary += $" Ausführungsindex: {taskIndex}.";
        summary += $" Kategorie: {category}.";
        return new RFiDGearDiagnosticSignal(true, strong, category, strong ? "high" : "medium", summary, taskIndex, stage, safe);
    }

    public static string Classify(string? text)
    {
        var value = text ?? "";
        if (Regex.IsMatch(value, @"(?i)AuthFailure|AuthenticationError|authentication\s+(?:failed|failure)|authenticate\w*\s+(?:failed|failure)|wrong\s+key|invalid\s+key|key\s+authentication")) return "Authentifizierung / Key";
        if (Regex.IsMatch(value, @"(?i)card\s+(?:removed|changed|swapped|lost)|chip\s+(?:changed|swapped)|different\s+card")) return "Karte entfernt / gewechselt";
        if (Regex.IsMatch(value, @"(?i)TransportError|No reader device|reader\s+(?:lost|disconnect|not found|unavailable)|communication\s+(?:failed|failure|error)|device\s+(?:timeout|disconnect)|connection\s+(?:failed|failure|lost)")) return "Reader / Transport";
        if (Regex.IsMatch(value, @"(?i)PermissionDenied|ProtocolConstraint|permission\s+denied|protocol\s+constraint")) return "Berechtigung / Protokoll";
        if (Regex.IsMatch(value, @"(?i)readback|verification|verify|mismatch|does not match")) return "Readback / Verifikation";
        if (Regex.IsMatch(value, @"(?i)TaskLoop\.Failure|ExecuteOnceAsync|Unhandled .*exception|ExceptionType|NullReferenceException|InvalidOperationException")) return "RFiDGear-Ausnahme";
        return "RFiDGear-Lauf / unbekannt";
    }

    public static RfPrjTaskSummary? CorrelateTask(RFiDGearDiagnosticSignal signal, IReadOnlyList<RfPrjTaskSummary> taskMap)
        => signal.TaskExecutionIndex is int index && index >= 0 && index < taskMap.Count ? taskMap[index] : null;

    private static string? FindLast(string[] lines, Func<string, bool> predicate, out bool found)
    {
        for (var index = lines.Length - 1; index >= 0; index--)
            if (predicate(lines[index])) { found = true; return lines[index]; }
        found = false;
        return null;
    }

    private static RFiDGearDiagnosticSignal Empty(string safe) => new(false, false, "", "none", "", null, "", safe);
}

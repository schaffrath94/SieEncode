using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace RFiDGear.Builder.Runtime;

public enum CodingSeriesMode { Single, Sequential, List }
public enum CodingSeriesResult { Successful, Failed, Skipped }

public sealed record CodingSeriesRecord(
    DateTimeOffset Timestamp, string Number, CodingSeriesResult Result, string FailureCategory,
    string Detail, IReadOnlyDictionary<string, string> Values, string RFiDGearDiagnosis = "",
    int? RFiDGearTaskExecutionIndex = null, string RFiDGearTaskType = "",
    string RFiDGearTaskDescription = "", string RFiDGearLogExcerpt = "",
    string OperatorSummary = "", string RecommendedAction = "", string EvidenceSummary = "",
    bool CardReleased = false);

public sealed record CodingSeriesSummaryFiles(string JsonPath, string TextPath, int Successful, int Failed, int Skipped);

public sealed class CodingSeriesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly List<CodingSeriesRecord> records = [];

    public CodingSeriesService(string projectDirectory, string projectName, CodingSeriesMode mode, DateTimeOffset? startedAt = null, string keySetIdentity = "")
    {
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        ProjectName = projectName;
        Mode = mode;
        KeySetIdentity = keySetIdentity;
        if (KeySetIdentity.Length > 0 && (KeySetIdentity.Length != 64 || KeySetIdentity.Any(character => !Uri.IsHexDigit(character))))
            throw new InvalidDataException("Keyset-ID der Kodierserie ist ungültig.");
        StartedAt = startedAt ?? DateTimeOffset.Now;
        var directory = Path.Combine(ProjectDirectory, "sessions");
        Directory.CreateDirectory(directory);
        SessionLogPath = Path.Combine(directory, $"coding_{StartedAt:yyyyMMdd_HHmmss}.jsonl");
        KnownSuccessfulNumbers = LoadKnownSuccessfulNumbers(ProjectDirectory);
    }

    public string ProjectDirectory { get; }
    public string ProjectName { get; }
    public CodingSeriesMode Mode { get; }
    public DateTimeOffset StartedAt { get; }
    public string KeySetIdentity { get; }
    public string SessionLogPath { get; }
    public IReadOnlyList<CodingSeriesRecord> Records => records;
    public HashSet<string> KnownSuccessfulNumbers { get; }

    public bool IsKnownSuccessful(string? number) => !string.IsNullOrWhiteSpace(number) && KnownSuccessfulNumbers.Contains(number.Trim());

    public void Append(CodingSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var safe = new
        {
            timestamp = record.Timestamp.ToString("s", CultureInfo.InvariantCulture),
            number = record.Number,
            result = ResultText(record.Result),
            failureCategory = record.FailureCategory,
            detail = record.Detail,
            values = record.Values,
            rfidgearDiagnosis = record.RFiDGearDiagnosis,
            rfidgearTaskExecutionIndex = record.RFiDGearTaskExecutionIndex,
            rfidgearTaskType = record.RFiDGearTaskType,
            rfidgearTaskDescription = record.RFiDGearTaskDescription,
            rfidgearLogExcerpt = record.RFiDGearLogExcerpt,
            operatorSummary = record.OperatorSummary,
            recommendedAction = record.RecommendedAction,
            evidenceSummary = record.EvidenceSummary,
            cardReleased = record.CardReleased,
            keySetIdentity = KeySetIdentity
        };
        File.AppendAllText(SessionLogPath, JsonSerializer.Serialize(safe, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + Environment.NewLine, new UTF8Encoding(false));
        records.Add(record);
        if (record.Result == CodingSeriesResult.Successful && !string.IsNullOrWhiteSpace(record.Number)) KnownSuccessfulNumbers.Add(record.Number);
    }

    public CodingSeriesSummaryFiles ExportSummary(DateTimeOffset? exportedAt = null)
    {
        if (records.Count == 0) throw new InvalidOperationException("Noch keine abgeschlossenen Datensätze in dieser Session.");
        var at = exportedAt ?? DateTimeOffset.Now;
        var successful = records.Count(record => record.Result == CodingSeriesResult.Successful);
        var failed = records.Count(record => record.Result == CodingSeriesResult.Failed);
        var skipped = records.Count(record => record.Result == CodingSeriesResult.Skipped);
        var directory = Path.Combine(ProjectDirectory, "sessions");
        var stem = $"coding_{at:yyyyMMdd_HHmmss}_summary";
        var jsonPath = Path.Combine(directory, stem + ".json");
        var textPath = Path.Combine(directory, stem + ".txt");
        var summary = new
        {
            schemaVersion = "1.2", projectName = ProjectName, mode = ModeText(Mode), keySetIdentity = KeySetIdentity,
            startedAt = StartedAt.ToString("s", CultureInfo.InvariantCulture),
            exportedAt = at.ToString("s", CultureInfo.InvariantCulture), sourceLog = SessionLogPath,
            containsCardData = true, containsKeys = false,
            counts = new { successful, failed, skipped, total = records.Count },
            records = records.Select(record => new
            {
                timestamp = record.Timestamp.ToString("s", CultureInfo.InvariantCulture), number = record.Number,
                result = ResultText(record.Result), failureCategory = record.FailureCategory, record.Detail, record.Values,
                record.RFiDGearDiagnosis, record.RFiDGearTaskExecutionIndex, record.RFiDGearTaskType,
                record.RFiDGearTaskDescription, record.RFiDGearLogExcerpt, record.OperatorSummary,
                record.RecommendedAction, record.EvidenceSummary, record.CardReleased
            })
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(summary, JsonOptions), new UTF8Encoding(false));
        var lines = new List<string>
        {
            $"RFiDGear Kodiersession – {ProjectName}", new('=', 72), $"Start: {summary.startedAt}",
            $"Export: {summary.exportedAt}", $"Modus: {summary.mode}",
            $"Keyset-ID: {(string.IsNullOrWhiteSpace(KeySetIdentity) ? "nicht verfügbar (Altprojekt)" : KeySetIdentity)}",
            $"Erfolgreich: {successful} · Fehler: {failed} · Übersprungen: {skipped} · Gesamt: {records.Count}",
            $"Session-Log: {SessionLogPath}", "", "Hinweis: Dieser Bericht enthält Kartennummern/Kartendaten, aber keine Schlüsselwerte.", "", "DATENSÄTZE", "---------"
        };
        foreach (var record in records)
        {
            lines.Add($"{record.Timestamp:s} | {record.Number} | {ResultText(record.Result)} | {(string.IsNullOrWhiteSpace(record.FailureCategory) ? "-" : record.FailureCategory)} | {record.Detail}");
            if (!string.IsNullOrWhiteSpace(record.OperatorSummary)) lines.Add("  Bedienentscheidung: " + record.OperatorSummary);
            if (!string.IsNullOrWhiteSpace(record.RecommendedAction)) lines.Add("  Nächste Aktion: " + record.RecommendedAction);
            if (!string.IsNullOrWhiteSpace(record.EvidenceSummary)) lines.Add("  Nachweis: " + record.EvidenceSummary.Replace(Environment.NewLine, " · "));
        }
        File.WriteAllLines(textPath, lines, new UTF8Encoding(false));
        return new CodingSeriesSummaryFiles(jsonPath, textPath, successful, failed, skipped);
    }

    public static string NextSequential(string current, string step)
    {
        if (string.IsNullOrWhiteSpace(current) || current.Any(character => !char.IsAsciiDigit(character))) throw new InvalidDataException("Aktuelle Nummer muss numerisch sein.");
        if (!BigInteger.TryParse(step, NumberStyles.None, CultureInfo.InvariantCulture, out var increment) || increment < 1) throw new InvalidDataException("Schrittweite muss eine positive ganze Zahl sein.");
        return (BigInteger.Parse(current, CultureInfo.InvariantCulture) + increment).ToString(CultureInfo.InvariantCulture).PadLeft(current.Length, '0');
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> LoadList(string path, IReadOnlyCollection<string> fieldIds, string seriesFieldId)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (lines.Length == 0) throw new InvalidDataException("Liste ist leer.");
        var delimiter = lines[0].Contains(';') ? ';' : lines[0].Contains(',') ? ',' : '\0';
        var first = delimiter == '\0' ? [lines[0].Trim()] : ParseDelimited(lines[0], delimiter);
        var hasHeader = delimiter != '\0' && first.Any(header => fieldIds.Contains(header, StringComparer.OrdinalIgnoreCase));
        var rows = new List<IReadOnlyDictionary<string, string>>();
        if (!hasHeader)
        {
            foreach (var line in lines) rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [seriesFieldId] = line.Trim() });
            return rows;
        }
        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseDelimited(lines[index], delimiter);
            if (values.Length != first.Length) throw new InvalidDataException($"Liste Zeile {index + 1}: Spaltenanzahl weicht vom Header ab.");
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < first.Length; column++)
                if (fieldIds.Contains(first[column], StringComparer.OrdinalIgnoreCase)) row[first[column]] = values[column];
            rows.Add(row);
        }
        if (rows.Count == 0) throw new InvalidDataException("Keine Datensätze erkannt.");
        return rows;
    }

    public static HashSet<string> LoadKnownSuccessfulNumbers(string projectDirectory)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var directory = Path.Combine(Path.GetFullPath(projectDirectory), "sessions");
        if (!Directory.Exists(directory)) return result;
        foreach (var path in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.TopDirectoryOnly))
        foreach (var line in File.ReadLines(path))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var status = root.TryGetProperty("result", out var resultNode) ? resultNode.GetString() : null;
                var number = root.TryGetProperty("number", out var numberNode) ? numberNode.GetString() : null;
                if ((string.Equals(status, "Erfolgreich", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Successful", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(number)) result.Add(number);
            }
            catch (JsonException) { }
        }
        return result;
    }

    private static string[] ParseDelimited(string line, char delimiter)
    {
        var values = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
            else if (character == '"') quoted = !quoted;
            else if (character == delimiter && !quoted) { values.Add(value.ToString().Trim()); value.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new InvalidDataException("Nicht geschlossenes Anführungszeichen in Liste.");
        values.Add(value.ToString().Trim());
        return values.ToArray();
    }

    private static string ResultText(CodingSeriesResult result) => result switch { CodingSeriesResult.Successful => "Erfolgreich", CodingSeriesResult.Failed => "Fehler", _ => "Übersprungen" };
    private static string ModeText(CodingSeriesMode mode) => mode switch { CodingSeriesMode.Sequential => "Fortlaufend", CodingSeriesMode.List => "Liste", _ => "Einzel" };
}

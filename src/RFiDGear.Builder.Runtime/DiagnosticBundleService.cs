using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record DiagnosticBundleRequest(
    string ProjectDirectory,
    string BuilderVersion,
    string RFiDGearVersion,
    RFiDGearLogSnapshot? LogSnapshot = null,
    string? SessionLogPath = null,
    CodingSeriesRecord? CurrentRecord = null,
    string? CodingProjectPath = null,
    IEnumerable<string>? KnownSecrets = null);

public sealed record DiagnosticBundleResult(string Path, bool ContainsKeys, bool ContainsCardData, IReadOnlyList<string> Entries);

public sealed class DiagnosticBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly string[] RootContracts =
    [
        "builder.manifest.json", "acceptance.plan.json", "coding.schema.json", "card-preflight.plan.json",
        "rfidgear.host-contract.json"
    ];
    private readonly RFiDGearLogMonitor logMonitor = new();

    public DiagnosticBundleResult Create(DiagnosticBundleRequest request, DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = Path.GetFullPath(request.ProjectDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Projektordner nicht gefunden: " + root);
        var at = createdAt ?? DateTimeOffset.Now;
        var diagnosticDirectory = Path.Combine(root, "diagnostics");
        Directory.CreateDirectory(diagnosticDirectory);
        var outputPath = UniqueOutputPath(diagnosticDirectory, at);
        var staging = Path.Combine(Path.GetTempPath(), "rfidgear-diagnostic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            WriteJson(Path.Combine(staging, "diagnostic.metadata.json"), new
            {
                schemaVersion = "1.0", createdAt = at.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
                builderVersion = request.BuilderVersion, projectFolderName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                rfidgearVersion = request.RFiDGearVersion, containsKeys = false, containsCardData = true,
                note = "Diagnosepaket enthält bewusst keine Projektdatei und keine Projektkonfiguration, da diese Schlüssel enthalten können."
            });

            foreach (var name in RootContracts) CopyIfPresent(Path.Combine(root, name), Path.Combine(staging, name));
            if (!string.IsNullOrWhiteSpace(request.SessionLogPath)) CopyIfSafeExternalFile(root, request.SessionLogPath, Path.Combine(staging, "session.jsonl"), request.KnownSecrets);
            if (request.CurrentRecord is not null) WriteCurrentRecord(Path.Combine(staging, "current-card.json"), request.CurrentRecord, request.KnownSecrets);
            CopyLatestSummary(root, staging, request.KnownSecrets);
            WriteRuntimeEvidence(staging, request);
            WriteTaskMap(staging, root, request.CodingProjectPath);
            CopyCardPreflight(root, staging, request.KnownSecrets);
            CopyReadbacks(root, staging);
            AssertStagingSafe(staging);

            var temporaryZip = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                ZipFile.CreateFromDirectory(staging, temporaryZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                File.Move(temporaryZip, outputPath);
            }
            finally { if (File.Exists(temporaryZip)) File.Delete(temporaryZip); }
            using var archive = ZipFile.OpenRead(outputPath);
            var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
            return new DiagnosticBundleResult(outputPath, false, true, entries);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    private void WriteRuntimeEvidence(string staging, DiagnosticBundleRequest request)
    {
        var delta = request.LogSnapshot is null ? new RFiDGearLogDelta("", []) : logMonitor.ReadDelta(request.LogSnapshot);
        var safe = RFiDGearDiagnosticParser.Sanitize(delta.Text, request.KnownSecrets);
        File.WriteAllText(Path.Combine(staging, "rfidgear.log.sanitized.txt"), safe, new UTF8Encoding(false));
        var signal = RFiDGearDiagnosticParser.Parse(delta.Text, request.KnownSecrets);
        WriteJson(Path.Combine(staging, "rfidgear.signal.json"), new
        {
            signal.HasSignal, signal.StrongFailure, signal.Category, signal.Confidence,
            signal.Summary, signal.TaskExecutionIndex, signal.Stage
        });
    }

    private static void WriteTaskMap(string staging, string root, string? configuredPath)
    {
        var projectPath = ResolveCodingProject(root, configuredPath);
        if (projectPath is null) return;
        var rows = RfPrjArchive.Load(projectPath).Summarize().Select((task, executionIndex) => new
        {
            executionIndex, taskIndex = task.Index, taskType = task.Type, task.Description,
            appNumberCurrent = task.Aid, appNumberNew = "", appNumberTarget = "",
            fileNumber = task.FileId, cryptoMode = "", accessRead = "", accessWrite = "", accessReadWrite = "", accessChange = ""
        }).ToArray();
        WriteJson(Path.Combine(staging, "task-map.json"), rows);
    }

    private static string? ResolveCodingProject(string root, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var full = Path.GetFullPath(configuredPath);
            if (IsChild(root, full) && File.Exists(full)) return full;
        }
        var schemaPath = Path.Combine(root, "coding.schema.json");
        if (!File.Exists(schemaPath)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        if (!document.RootElement.TryGetProperty("codingProject", out var property) || property.ValueKind != JsonValueKind.String) return null;
        var relative = property.GetString();
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return null;
        var candidate = Path.GetFullPath(Path.Combine(root, relative));
        return IsChild(root, candidate) && File.Exists(candidate) ? candidate : null;
    }

    private static void WriteCurrentRecord(string path, CodingSeriesRecord record, IEnumerable<string>? knownSecrets)
    {
        var value = new
        {
            timestamp = record.Timestamp.ToString("s", System.Globalization.CultureInfo.InvariantCulture), number = record.Number,
            result = record.Result switch { CodingSeriesResult.Successful => "Erfolgreich", CodingSeriesResult.Failed => "Fehler", _ => "Übersprungen" },
            record.FailureCategory, record.Detail, record.Values, record.RFiDGearDiagnosis,
            record.RFiDGearTaskExecutionIndex, record.RFiDGearTaskType, record.RFiDGearTaskDescription, record.RFiDGearLogExcerpt
        };
        var json = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(path, RFiDGearDiagnosticParser.Sanitize(json, knownSecrets), new UTF8Encoding(false));
    }

    private static void CopyLatestSummary(string root, string staging, IEnumerable<string>? knownSecrets)
    {
        var sessions = Path.Combine(root, "sessions");
        if (!Directory.Exists(sessions)) return;
        var latest = Directory.EnumerateFiles(sessions, "*_summary.json", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
        if (latest is not null) CopySanitizedText(latest.FullName, Path.Combine(staging, "session.summary.json"), knownSecrets);
    }

    private static void CopyCardPreflight(string root, string staging, IEnumerable<string>? knownSecrets)
    {
        var preflight = Path.Combine(root, "card_preflight");
        CopySanitizedText(Path.Combine(preflight, "expected-card.json"), Path.Combine(staging, "card-preflight.expected-card.json"), knownSecrets);
        CopySanitizedText(Path.Combine(preflight, "card-preflight.result.json"), Path.Combine(staging, "card-preflight.result.json"), knownSecrets);
        CopySanitizedText(Path.Combine(preflight, "card-preflight.result.txt"), Path.Combine(staging, "card-preflight.result.txt"), knownSecrets);
        foreach (var mode in new[] { "transport", "productive" })
        {
            var source = Path.Combine(preflight, mode, "rfidgear.probe.log.sanitized.txt");
            if (!File.Exists(source)) continue;
            var safe = RFiDGearDiagnosticParser.Sanitize(File.ReadAllText(source), knownSecrets);
            File.WriteAllText(Path.Combine(staging, $"card-preflight.{mode}.rfidgear.log.sanitized.txt"), safe, new UTF8Encoding(false));
        }
    }

    private static void CopyReadbacks(string root, string staging)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "verification", "readback" };
        var schemaPath = Path.Combine(root, "coding.schema.json");
        if (File.Exists(schemaPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
            if (document.RootElement.TryGetProperty("finalVerificationDirectory", out var property) && property.ValueKind == JsonValueKind.String && property.GetString() is { Length: > 0 } name && !Path.IsPathRooted(name)) names.Add(name);
        }
        foreach (var name in names)
        {
            var sourceDirectory = Path.GetFullPath(Path.Combine(root, name));
            if (!IsChild(root, sourceDirectory) || !Directory.Exists(sourceDirectory)) continue;
            var targetDirectory = Path.Combine(staging, name);
            Directory.CreateDirectory(targetDirectory);
            foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)) File.Copy(source, Path.Combine(targetDirectory, Path.GetFileName(source)), true);
        }
    }

    private static void AssertStagingSafe(string staging)
    {
        var allowedTaskMapFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "executionIndex", "taskIndex", "taskType", "description", "appNumberCurrent", "appNumberNew", "appNumberTarget", "fileNumber", "cryptoMode", "accessRead", "accessWrite", "accessReadWrite", "accessChange" };
        foreach (var path in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".rfPrj", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".project.json", StringComparison.OrdinalIgnoreCase) || name.Contains("vault", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Verbotene Datei im Diagnosepaket: " + name);
        }
        var taskMap = Path.Combine(staging, "task-map.json");
        if (!File.Exists(taskMap)) return;
        var taskMapText = File.ReadAllText(taskMap);
        if (!string.Equals(taskMapText, RFiDGearDiagnosticParser.Sanitize(taskMapText), StringComparison.Ordinal))
            throw new InvalidDataException("Diagnose-Task-Map enthält einen möglichen 16-/24-Byte-Schlüsselwert.");
        using var document = JsonDocument.Parse(taskMapText);
        foreach (var row in document.RootElement.EnumerateArray())
        foreach (var property in row.EnumerateObject())
            if (!allowedTaskMapFields.Contains(property.Name)) throw new InvalidDataException("Potenziell sensibles Feld in Diagnose-Task-Map: " + property.Name);
    }

    private static void CopyIfSafeExternalFile(string root, string source, string target, IEnumerable<string>? knownSecrets)
    {
        var full = Path.GetFullPath(source);
        if (!IsChild(root, full)) throw new InvalidDataException("Sessionlog liegt außerhalb des Projektordners.");
        CopySanitizedText(full, target, knownSecrets);
    }

    private static void CopyIfPresent(string source, string target)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, true);
    }

    private static void CopySanitizedText(string source, string target, IEnumerable<string>? knownSecrets)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, RFiDGearDiagnosticParser.Sanitize(File.ReadAllText(source), knownSecrets), new UTF8Encoding(false));
    }

    private static void WriteJson(string path, object value)
        => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));

    private static bool IsChild(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueOutputPath(string directory, DateTimeOffset at)
    {
        var stem = $"diagnostic_{at:yyyyMMdd_HHmmss}";
        var path = Path.Combine(directory, stem + ".zip");
        for (var suffix = 2; File.Exists(path); suffix++) path = Path.Combine(directory, $"{stem}_{suffix}.zip");
        return path;
    }
}

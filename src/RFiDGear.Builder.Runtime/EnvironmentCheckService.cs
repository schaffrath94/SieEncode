using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record EnvironmentCheckRequest(
    string DistributionRoot,
    string SettingsPath,
    string ProjectsDirectory,
    string RFiDGearExecutable,
    string ProfileDirectory,
    string TaskShellDirectory,
    string LauncherPath,
    IReadOnlyList<string> DocumentationPaths);

public sealed record EnvironmentCheckItem(string Status, string Area, string Subject, string Detail, string Evidence);
public sealed record EnvironmentCheckSummary(int Ok, int Warnings, int Errors, int Total);

public sealed record EnvironmentCheckReport(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Result,
    EnvironmentCheckSummary Summary,
    IReadOnlyList<EnvironmentCheckItem> Checks,
    string Note);

public sealed record EnvironmentCheckFiles(EnvironmentCheckReport Report, string JsonPath, string TextPath);

public sealed class EnvironmentCheckService
{
    private const string Ok = "OK";
    private const string Warning = "Warnung";
    private const string Error = "Fehler";

    public EnvironmentCheckReport Inspect(EnvironmentCheckRequest request, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var checks = new List<EnvironmentCheckItem>();
        CheckDistribution(request, checks);
        CheckProfiles(request.ProfileDirectory, checks);
        CheckTaskShells(request.TaskShellDirectory, checks);
        CheckSettings(request, checks);
        CheckProjects(request.ProjectsDirectory, checks);
        CheckRFiDGear(request.RFiDGearExecutable, checks);
        CheckReaderAndLogs(checks);

        var summary = new EnvironmentCheckSummary(
            checks.Count(item => item.Status == Ok),
            checks.Count(item => item.Status == Warning),
            checks.Count(item => item.Status == Error),
            checks.Count);
        var result = summary.Errors > 0 ? Error : summary.Warnings > 0 ? "Mit Warnungen" : Ok;
        return new EnvironmentCheckReport("1.0", now ?? DateTimeOffset.Now, result, summary, checks,
            "Rein lesender Umgebungstest. RFiDGear, Reader und Karte wurden nicht gestartet oder verändert; Schlüsselwerte und Dateiinhalte werden nicht ausgegeben.");
    }

    public EnvironmentCheckFiles CreateReport(EnvironmentCheckRequest request, string outputDirectory, DateTimeOffset? now = null)
    {
        var report = Inspect(request, now);
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        var stamp = report.GeneratedAt.ToString("yyyyMMdd-HHmmss");
        var jsonPath = Path.Combine(directory, $"Umgebungstest-{stamp}.json");
        var textPath = Path.Combine(directory, $"Umgebungstest-{stamp}.txt");
        WriteAtomic(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        WriteAtomic(textPath, RenderText(report));
        return new EnvironmentCheckFiles(report, jsonPath, textPath);
    }

    private static void CheckDistribution(EnvironmentCheckRequest request, List<EnvironmentCheckItem> checks)
    {
        var root = Full(request.DistributionRoot);
        Add(checks, Directory.Exists(root) ? Ok : Error, "Builder", "Release-Verzeichnis",
            Directory.Exists(root) ? "Verzeichnis ist lesbar." : "Verzeichnis fehlt.", root);
        CheckFile(Path.Combine(root, "VERSION"), "Builder", "Versionsdatei", checks);
        CheckFile(request.LauncherPath, "Builder", "Startdatei", checks);
        if (File.Exists(request.LauncherPath))
        {
            var bytes = File.ReadAllBytes(request.LauncherPath);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = Encoding.UTF8.GetString(bytes);
            Add(checks, !hasBom && text.Contains("artifacts\\app\\SieEncode.exe", StringComparison.OrdinalIgnoreCase) ? Ok : Error,
                "Builder", "Startdatei-Vertrag", !hasBom ? "Startziel wurde geprüft." : "CMD-Datei enthält eine UTF-8-BOM.", request.LauncherPath);
        }
        foreach (var document in request.DocumentationPaths) CheckFile(document, "Dokumentation", Path.GetFileName(document), checks);
    }

    private static void CheckProfiles(string directory, List<EnvironmentCheckItem> checks)
    {
        try
        {
            var library = ProfileLibrary.Load(directory);
            var issues = library.Validate();
            Add(checks, issues.Count == 0 ? Ok : Error, "Profile", "Profilbibliothek",
                issues.Count == 0 ? $"{library.Profiles.Count} Profile gültig." : $"{issues.Count} Validierungsfehler.", Full(directory));
        }
        catch (Exception exception) { Add(checks, Error, "Profile", "Profilbibliothek", exception.Message, Full(directory)); }
    }

    private static void CheckTaskShells(string directory, List<EnvironmentCheckItem> checks)
    {
        try
        {
            var library = TaskShellLibrary.Load(directory);
            Add(checks, Ok, "Build", "Task-Shells", $"{library.TaskTypes.Count} Task-Shells gültig.", Full(directory));
        }
        catch (Exception exception) { Add(checks, Error, "Build", "Task-Shells", exception.Message, Full(directory)); }
    }

    private static void CheckSettings(EnvironmentCheckRequest request, List<EnvironmentCheckItem> checks)
    {
        var path = Full(request.SettingsPath);
        if (!File.Exists(path)) Add(checks, Warning, "Einstellungen", "Benutzereinstellungen", "Datei wird beim ersten Speichern angelegt.", path);
        else
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                Add(checks, document.RootElement.ValueKind == JsonValueKind.Object ? Ok : Error, "Einstellungen", "Benutzereinstellungen",
                    document.RootElement.ValueKind == JsonValueKind.Object ? "Gültiges JSON-Objekt; Inhalte werden nicht ausgegeben." : "Wurzelelement ist kein JSON-Objekt.", path);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                Add(checks, Error, "Einstellungen", "Benutzereinstellungen", "Datei ist nicht lesbar oder ungültig: " + exception.Message, path);
            }
        }
        var root = Full(request.DistributionRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Add(checks, !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? Ok : Error, "Einstellungen", "Versionsunabhängiger Speicherort",
            !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? "Liegt außerhalb des Release-Verzeichnisses." : "Liegt im Release-Verzeichnis und könnte beim Versionswechsel verloren gehen.", path);
    }

    private static void CheckProjects(string directory, List<EnvironmentCheckItem> checks)
    {
        var path = Full(directory);
        if (!Directory.Exists(path)) { Add(checks, Error, "Projekte", "Projektbibliothek", "Übergeordneter Projektordner fehlt.", path); return; }
        try
        {
            var definitions = Directory.EnumerateFiles(path, "*.project.json", SearchOption.AllDirectories).Count();
            var built = Directory.EnumerateFiles(path, "*.rfPrj", SearchOption.AllDirectories).Count();
            Add(checks, Ok, "Projekte", "Projektbibliothek", $"Lesbar: {definitions} Definition(en), {built} gebautes/bebaute rfPrj-Projekt(e).", path);
        }
        catch (Exception exception) { Add(checks, Error, "Projekte", "Projektbibliothek", exception.Message, path); }
    }

    private static void CheckRFiDGear(string executable, List<EnvironmentCheckItem> checks)
    {
        var path = Full(executable);
        if (!File.Exists(path)) { Add(checks, Error, "RFiDGear", "Programmdatei", "RFiDGear.exe fehlt oder ist nicht konfiguriert.", path); return; }
        var report = new RFiDGearCapabilityInspector().Inspect(path);
        Add(checks, string.IsNullOrWhiteSpace(report.Error) ? Ok : Error, "RFiDGear", "Programmdatei",
            string.IsNullOrWhiteSpace(report.Error) ? $"Binär lesbar; Version {report.Version.ProductVersion ?? "unbekannt"}." : report.Error!, path);
        foreach (var capability in report.Capabilities)
        {
            var required = capability.Id == RFiDGearCapabilityId.CustomProjectFile;
            var status = capability.State == RFiDGearCapabilityState.Available ? Ok : required ? Error : Warning;
            Add(checks, status, "RFiDGear-Fähigkeit", capability.Name, capability.State.ToString(), capability.Evidence);
        }
    }

    private static void CheckReaderAndLogs(List<EnvironmentCheckItem> checks)
    {
        var reader = RFiDGearSettingsInspection.Inspect();
        Add(checks, reader.IsConfigured ? Ok : Warning, "RFiDGear", "Reader-Konfiguration",
            reader.IsConfigured ? $"Provider {reader.Provider}; Port {reader.Port}; Reader {reader.ReaderName}." : reader.Error ?? "Kein Reader konfiguriert.", reader.SettingsPath);
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "log");
        if (!Directory.Exists(logDirectory)) Add(checks, Warning, "RFiDGear", "Logverzeichnis", "Noch kein Logverzeichnis vorhanden.", logDirectory);
        else
        {
            try { Add(checks, Ok, "RFiDGear", "Logverzeichnis", $"Lesbar; {Directory.EnumerateFiles(logDirectory).Count()} Datei(en).", logDirectory); }
            catch (Exception exception) { Add(checks, Warning, "RFiDGear", "Logverzeichnis", exception.Message, logDirectory); }
        }
    }

    private static void CheckFile(string path, string area, string subject, List<EnvironmentCheckItem> checks)
        => Add(checks, File.Exists(path) ? Ok : Error, area, subject, File.Exists(path) ? "Datei vorhanden." : "Datei fehlt.", Full(path));

    private static void Add(List<EnvironmentCheckItem> checks, string status, string area, string subject, string detail, string evidence)
        => checks.Add(new(status, area, subject, detail, evidence));

    private static string Full(string? path)
        => string.IsNullOrWhiteSpace(path) ? "(nicht angegeben)" : Path.GetFullPath(path);

    private static string RenderText(EnvironmentCheckReport report)
    {
        var lines = new List<string>
        {
            "SieEncode – Umgebungstest", $"Erzeugt: {report.GeneratedAt:O}",
            $"Ergebnis: {report.Result}", $"OK: {report.Summary.Ok} | Warnungen: {report.Summary.Warnings} | Fehler: {report.Summary.Errors}", "", report.Note, ""
        };
        foreach (var item in report.Checks) lines.Add($"[{item.Status}] {item.Area} · {item.Subject}\r\n  {item.Detail}\r\n  Nachweis: {item.Evidence}");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }
}

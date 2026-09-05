using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record ProjectAnalysisIssue(string Severity, string Area, string Message);
public sealed record ProjectAnalysisFile(int Id, string Name, string Type, int Size, string Communication, string Access, string? CreationSettings, string Initialization, string CodingOutput);
public sealed record ProjectAnalysisApplication(string Aid, string Name, string KeyType, int KeyCount, string TransportKeySettings, string ProductiveKeySettings, string ChangeKeyMode, IReadOnlyList<ProjectAnalysisFile> Files);
public sealed record ProjectAnalysisTask(int Position, string Index, string Type, string Description, string ConditionIndex, string ConditionLevel, string Aid, string FileId, string KeyReference, string Settings, string FileContract);

public sealed class ProjectAnalysisReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string Project { get; init; } = "";
    public string GeneratedAt { get; init; } = "";
    public List<string> Profiles { get; init; } = [];
    public List<ProjectAnalysisApplication> Applications { get; init; } = [];
    public List<ProjectAnalysisTask> MasteringTasks { get; init; } = [];
    public List<ProjectAnalysisTask> CodingTasks { get; init; } = [];
    public List<ProjectAnalysisIssue> Issues { get; init; } = [];
    public int Errors => Issues.Count(issue => issue.Severity == "ERROR");
    public int Warnings => Issues.Count(issue => issue.Severity == "WARN");
    public bool ContainsKeys => false;
}

public sealed record ProjectAnalysisFiles(ProjectAnalysisReport Report, string TextPath, string JsonPath);

public sealed class ProjectAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public ProjectAnalysisReport Analyze(ProjectDefinition project, ProfileLibrary library, TaskShellLibrary shells, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(shells);
        var report = new ProjectAnalysisReport { Project = project.Project.Name, GeneratedAt = (now ?? DateTime.Now).ToString("s", CultureInfo.InvariantCulture) };
        var requiresProductive = project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        foreach (var issue in ProjectContractValidator.Validate(project, library, requireProductiveSecrets: false))
            report.Issues.Add(new ProjectAnalysisIssue("ERROR", "Projektvertrag", $"{issue.Code}: {issue.Message}"));

        ComposedProfiles composition;
        try { composition = library.Compose(project.Profiles.Base, project.Profiles.Customers); }
        catch (Exception exception) { report.Issues.Add(new("ERROR", "Profilkomposition", exception.Message)); return report; }
        report.Profiles.AddRange(composition.Profiles.Select(profile => $"{profile.Profile.Id} · {profile.Profile.Version} · {profile.Profile.Kind}"));
        report.Applications.AddRange(composition.Applications.Select(application => new ProjectAnalysisApplication(
            ProfileValidator.NormalizeAid(application.Id), application.Name, application.KeyType, application.KeyCount,
            application.TransportKeySettings, application.ProductiveKeySettings, application.ChangeKeyMode,
            application.Files.OrderBy(file => file.Id).Select(file => new ProjectAnalysisFile(
                file.Id, file.Name, file.Type, file.Size, file.Communication, Access(file.Access),
                file.CreationSettings is null ? null : $"{file.CreationSettings.Communication} {Access(file.CreationSettings.Access)}",
                file.MasteringInitialization?.Type ?? "zeroFill (Standard)", file.Coding?.Output ?? "")).ToArray())));
        if (report.Errors > 0) return report;

        // Der Dry-Run prüft Ablauf und Credential-Rollen mit synthetischen, nur im
        // Speicher vorhandenen Tokens. Produktivkeys werden dafür weder benötigt
        // noch aus einem Vault gelesen oder in einen Bericht übernommen.
        var analysisProject = WithSymbolicCredentials(project, composition);

        var factory = new MainProjectFactory(shells);
        if (project.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var mastering = factory.BuildMastering(composition, analysisProject, Path.Combine(Path.GetTempPath(), "rfidgear-analysis-contract"));
                report.MasteringTasks.AddRange(ToSafeTasks(mastering));
                ValidateDependencies(mastering, report, "Mastering");
                ValidateExpectedPlan(mastering, ExpectedMastering(composition, project), report, "Mastering");
                ValidateMasteringCredentials(mastering, composition, analysisProject, report);
            }
            catch (Exception exception) { report.Issues.Add(new("ERROR", "Mastering", exception.Message)); }
        }
        if (requiresProductive)
        {
            try
            {
                var coding = factory.BuildCoding(composition, analysisProject, Path.Combine(Path.GetTempPath(), "rfidgear-analysis-contract"));
                report.CodingTasks.AddRange(ToSafeTasks(coding));
                ValidateDependencies(coding, report, "Kodierung");
                ValidateExpectedPlan(coding, ExpectedCoding(composition, project), report, "Kodierung");
                SimulateCoding(coding, composition, analysisProject, report);
            }
            catch (Exception exception) { report.Issues.Add(new("ERROR", "Kodierung", exception.Message)); }
        }
        return report;
    }

    public ProjectAnalysisFiles Save(ProjectAnalysisReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var textPath = Path.Combine(root, "Projektanalyse.txt");
        var jsonPath = Path.Combine(root, "Projektanalyse.json");
        AtomicWrite(textPath, FormatText(report));
        AtomicWrite(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        AssertKeyFree(textPath, jsonPath);
        return new ProjectAnalysisFiles(report, textPath, jsonPath);
    }

    public ProjectAnalysisFiles AnalyzeAndSave(ProjectDefinition project, ProfileLibrary library, TaskShellLibrary shells, string directory, DateTime? now = null)
        => Save(Analyze(project, library, shells, now), directory);

    private static ProjectDefinition WithSymbolicCredentials(ProjectDefinition source, ComposedProfiles composition)
    {
        var project = JsonSerializer.Deserialize<ProjectDefinition>(JsonSerializer.Serialize(source, ProjectJson.Options), ProjectJson.Options)!;
        project.Transport.PiccKey.Value = Token("transport-picc", project.Transport.PiccKey.Type);
        project.Productive.PiccKey.Value = Token("productive-picc", project.Productive.PiccKey.Type);
        foreach (var type in composition.Applications.Select(application => application.KeyType).Distinct(StringComparer.OrdinalIgnoreCase))
            project.Transport.ApplicationKeys[type] = Token("transport-" + type, type);
        project.Productive.ApplicationKeys = composition.Applications.ToDictionary(application => ProfileValidator.NormalizeAid(application.Id), application =>
            Enumerable.Range(0, application.KeyCount).Select(index => Token($"productive-{application.Id}-{index}", application.KeyType)).ToList(), StringComparer.OrdinalIgnoreCase);
        return project;
    }

    private static string Token(string seed, string keyType)
    {
        var length = keyType == "DF_KEY_3K3DES" ? 48 : 32;
        var first = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("RFiDGear-Analyse:" + seed)));
        return first[..length];
    }

    private static IEnumerable<ExpectedTask> ExpectedMastering(ComposedProfiles composition, ProjectDefinition project)
    {
        foreach (var app in CardOperationMode.SelectApplications(composition, project))
        {
            var aid = ProfileValidator.NormalizeAid(app.Id);
            yield return new("AppExistCheck", aid, null, null, false);
            yield return new("CreateApplication", aid, null, null, false);
            foreach (var file in app.Files) yield return new("CreateFile", aid, file.Id, file, true);
            foreach (var file in app.Files.Where(RequiresZeroFill))
            {
                yield return new("WriteData", aid, file.Id, file, true);
                yield return new("ReadData", aid, file.Id, file, true);
            }
            foreach (var file in app.Files.Where(file => file.CreationSettings is not null)) yield return new("ChangeFileSettings", aid, file.Id, file, false);
            yield return new("AppExistCheck", aid, null, null, false);
        }
    }

    private static IEnumerable<ExpectedTask> ExpectedCoding(ComposedProfiles composition, ProjectDefinition project)
    {
        var applications = CardOperationMode.SelectApplications(composition, project);
        foreach (var app in applications) yield return new("AppExistCheck", ProfileValidator.NormalizeAid(app.Id), null, null, false);
        foreach (var app in applications)
        {
            var aid = ProfileValidator.NormalizeAid(app.Id);
            foreach (var file in app.Files.Where(file => file.Coding is not null))
            {
                yield return new("WriteData", aid, file.Id, file, false);
                yield return new("ReadData", aid, file.Id, file, false);
            }
            foreach (var number in Enumerable.Range(1, app.KeyCount - 1).Append(0)) yield return new("ApplicationKeyChangeover", aid, null, null, false, number);
            yield return new("ApplicationKeySettingsChangeover", aid, null, null, false);
        }
        if (!CardOperationMode.IsExtension(project))
        {
            yield return new("PICCMasterKeyChangeover", "0", null, null, false);
            yield return new("PICCMasterKeySettingsChangeover", "0", null, null, false);
        }
        foreach (var app in applications)
        foreach (var file in app.Files.Where(file => file.Coding is not null)) yield return new("ReadData", ProfileValidator.NormalizeAid(app.Id), file.Id, file, false);
    }

    private static void ValidateExpectedPlan(RfPrjDocument document, IEnumerable<ExpectedTask> expectedTasks, ProjectAnalysisReport report, string area)
    {
        var expected = expectedTasks.ToArray();
        if (document.Tasks.Count != expected.Length)
        {
            report.Issues.Add(new("ERROR", area, $"Taskanzahl {document.Tasks.Count} statt erwartet {expected.Length}."));
            return;
        }
        for (var position = 0; position < expected.Length; position++)
        {
            var task = document.Tasks[position]; var expectation = expected[position];
            var type = TaskXml.Get(task, "SelectedTaskType"); var index = TaskXml.Get(task, "CurrentTaskIndex"); var aid = Aid(task);
            if (type != expectation.Type) report.Issues.Add(new("ERROR", area, $"Position {position + 1}/Task {index}: {type} statt {expectation.Type}."));
            if (!string.Equals(aid, expectation.Aid, StringComparison.OrdinalIgnoreCase)) report.Issues.Add(new("ERROR", area, $"Task {index}: AID {aid} statt {expectation.Aid}."));
            if (expectation.FileId is { } fileId && Int(task, "FileNumberCurrent") != fileId) report.Issues.Add(new("ERROR", area, $"Task {index}: FileID stimmt nicht mit dem Profilplan überein."));
            if (expectation.KeyNumber is { } keyNo && Int(task, "SelectedDesfireAppKeyNumberCurrent") != keyNo) report.Issues.Add(new("ERROR", area, $"Task {index}: Keynummer stimmt nicht mit der sicheren Wechselreihenfolge überein."));
            if (expectation.File is not null) ValidateFileContract(task, expectation.File, expectation.CreationSettings, report, area);
        }
    }

    private static void ValidateMasteringCredentials(RfPrjDocument document, ComposedProfiles composition, ProjectDefinition project, ProjectAnalysisReport report)
    {
        var apps = composition.Applications.ToDictionary(app => ProfileValidator.NormalizeAid(app.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var task in document.Tasks)
        {
            var index = TaskXml.Get(task, "CurrentTaskIndex"); var type = TaskXml.Get(task, "SelectedTaskType");
            if (type is "ApplicationKeyChangeover" or "ApplicationKeySettingsChangeover" or "PICCMasterKeyChangeover" or "PICCMasterKeySettingsChangeover")
                report.Issues.Add(new("ERROR", "Mastering", $"Task {index}: produktiver Security-Task {type} ist im Mastering unzulässig."));
            var aid = Aid(task); if (!apps.TryGetValue(aid, out var app)) continue;
            var transport = project.Transport.ApplicationKeys.GetValueOrDefault(app.KeyType, "");
            var credentialFields = type switch
            {
                "WriteData" => new[] { "DesfireWriteKeyCurrent" },
                "ReadData" => new[] { "DesfireReadKeyCurrent" },
                _ => new[] { "DesfireAppKeyCurrent", "DesfireAppKeyCurrentOld" }
            };
            foreach (var field in credentialFields)
            {
                var value = TaskXml.Get(task, field);
                if (value.Length > 0 && !string.Equals(value, transport, StringComparison.OrdinalIgnoreCase)) report.Issues.Add(new("ERROR", "Mastering", $"Task {index}: {field} ist nicht transportneutral."));
            }
            if (type == "CreateApplication" && Settings(task) != app.TransportKeySettings) report.Issues.Add(new("ERROR", "Mastering", $"Task {index}: Transport-KeySettings weichen ab."));
        }
    }

    private static void SimulateCoding(RfPrjDocument document, ComposedProfiles composition, ProjectDefinition project, ProjectAnalysisReport report)
    {
        var applications = CardOperationMode.SelectApplications(composition, project)
            .ToDictionary(app => ProfileValidator.NormalizeAid(app.Id), StringComparer.OrdinalIgnoreCase);
        var states = applications.ToDictionary(pair => pair.Key, pair => new ApplicationState(
            Enumerable.Repeat(project.Transport.ApplicationKeys.GetValueOrDefault(pair.Value.KeyType, ""), pair.Value.KeyCount).ToArray(), pair.Value.TransportKeySettings), StringComparer.OrdinalIgnoreCase);
        var piccKey = project.Transport.PiccKey.Value;
        var piccSettings = "transport";
        foreach (var task in document.Tasks)
        {
            var type = TaskXml.Get(task, "SelectedTaskType"); var index = TaskXml.Get(task, "CurrentTaskIndex"); var aid = Aid(task);
            applications.TryGetValue(aid, out var app); states.TryGetValue(aid, out var state);
            switch (type)
            {
                case "AppExistCheck": if (app is null) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: unbekannte AID {aid}.")); break;
                case "WriteData": case "ReadData":
                    if (app is null || state is null) { report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: {type} auf unbekannter AID {aid}.")); break; }
                    var file = app.Files.SingleOrDefault(candidate => candidate.Id == Int(task, "FileNumberCurrent"));
                    if (file is null) { report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: unbekannte FileID.")); break; }
                    var keyNo = type == "WriteData" ? Int(task, "SelectedDesfireWriteKeyNumber") : Int(task, "SelectedDesfireReadKeyNumber");
                    var expectedNo = type == "WriteData" ? file.Access.Write : file.Access.Read;
                    var keyField = type == "WriteData" ? "DesfireWriteKeyCurrent" : "DesfireReadKeyCurrent";
                    if (keyNo != expectedNo) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: {type}-Keynummer {keyNo} statt Key{expectedNo}."));
                    if (keyNo < 0 || keyNo >= state.Keys.Length || !Equal(TaskXml.Get(task, keyField), state.Keys[keyNo])) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: {type}-Credential passt nicht zum simulierten Kartenstand."));
                    break;
                case "ApplicationKeyChangeover":
                    if (app is null || state is null) { report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: KeyChange auf unbekannter AID {aid}.")); break; }
                    var number = Int(task, "SelectedDesfireAppKeyNumberCurrent");
                    if (number < 0 || number >= state.Keys.Length) { report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: Ziel-Key{number} außerhalb der Keyanzahl.")); break; }
                    var auth = app.ChangeKeyMode == "ChangeKeyUsingMK" && number != 0 ? state.Keys[0] : state.Keys[number];
                    if (!Equal(TaskXml.Get(task, "DesfireAppKeyCurrent"), auth)) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: Auth-Key für Key{number} passt nicht zu {app.ChangeKeyMode}."));
                    if (!Equal(TaskXml.Get(task, "DesfireAppKeyCurrentOld"), state.Keys[number])) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: alter Zielschlüssel stimmt nicht mit dem simulierten Zustand überein."));
                    var productive = project.Productive.ApplicationKeys[aid];
                    if (!Equal(TaskXml.Get(task, "DesfireAppKeyTarget"), productive[number])) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: Zielwert von Key{number} weicht von der Sitzungskonfiguration ab."));
                    state.Keys[number] = TaskXml.Get(task, "DesfireAppKeyTarget");
                    break;
                case "ApplicationKeySettingsChangeover":
                    if (app is null || state is null) { report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: KeySettings auf unbekannter AID {aid}.")); break; }
                    if (!Equal(TaskXml.Get(task, "DesfireAppKeyCurrent"), state.Keys[0])) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: KeySettings werden nicht mit aktuellem Key0 autorisiert."));
                    if (Settings(task) != app.ProductiveKeySettings) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: produktive KeySettings weichen ab."));
                    state.Settings = Settings(task);
                    break;
                case "PICCMasterKeyChangeover":
                    if (!Equal(TaskXml.Get(task, "DesfireMasterKeyCurrent"), piccKey)) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: aktueller PICC-Key passt nicht zum simulierten Zustand."));
                    if (!Equal(TaskXml.Get(task, "DesfireMasterKeyTarget"), project.Productive.PiccKey.Value)) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: PICC-Zielkey weicht von der Sitzungskonfiguration ab."));
                    piccKey = TaskXml.Get(task, "DesfireMasterKeyTarget");
                    break;
                case "PICCMasterKeySettingsChangeover":
                    if (!Equal(TaskXml.Get(task, "DesfireMasterKeyCurrent"), piccKey)) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: PICC-KeySettings verwenden nicht den aktuellen Key."));
                    if (Settings(task) != project.Productive.PiccKeySettings) report.Issues.Add(new("ERROR", "Coding-DryRun", $"Task {index}: PICC-KeySettings weichen ab."));
                    piccSettings = Settings(task);
                    break;
            }
        }
        foreach (var (aid, app) in applications)
        {
            var expected = project.Productive.ApplicationKeys[aid]; var state = states[aid];
            if (!state.Keys.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) report.Issues.Add(new("ERROR", "Coding-Endzustand", $"AID {aid}: nicht alle Keyslots sind produktiv."));
            if (state.Settings != app.ProductiveKeySettings) report.Issues.Add(new("ERROR", "Coding-Endzustand", $"AID {aid}: KeySettings sind nicht produktiv."));
        }
        if (!CardOperationMode.IsExtension(project))
        {
            if (!Equal(piccKey, project.Productive.PiccKey.Value)) report.Issues.Add(new("ERROR", "Coding-Endzustand", "PICC-Key ist nicht produktiv."));
            if (piccSettings != project.Productive.PiccKeySettings) report.Issues.Add(new("ERROR", "Coding-Endzustand", "PICC-KeySettings sind nicht produktiv."));
        }
    }

    private static void ValidateFileContract(XElement task, FileDefinition file, bool creation, ProjectAnalysisReport report, string area)
    {
        var settings = creation && file.CreationSettings is not null ? file.CreationSettings : new FileSettingsDefinition { Communication = file.Communication, Access = file.Access };
        var index = TaskXml.Get(task, "CurrentTaskIndex");
        if (Int(task, "FileSizeCurrent") != file.Size) report.Issues.Add(new("ERROR", area, $"Task {index}: Filegröße weicht ab."));
        if (TaskXml.Get(task, "SelectedDesfireFileType") != file.Type) report.Issues.Add(new("ERROR", area, $"Task {index}: Filetyp weicht ab."));
        if (TaskXml.Get(task, "SelectedDesfireFileCryptoMode") != settings.Communication) report.Issues.Add(new("ERROR", area, $"Task {index}: Communication Mode weicht ab."));
        foreach (var (field, expected) in new[] { ("SelectedDesfireFileAccessRightRead", settings.Access.Read), ("SelectedDesfireFileAccessRightWrite", settings.Access.Write), ("SelectedDesfireFileAccessRightReadWrite", settings.Access.ReadWrite), ("SelectedDesfireFileAccessRightChange", settings.Access.Change) })
            if (TaskXml.Get(task, field) != "AR_KEY" + expected) report.Issues.Add(new("ERROR", area, $"Task {index}: {field} weicht ab."));
    }

    private static void ValidateDependencies(RfPrjDocument document, ProjectAnalysisReport report, string area)
    {
        foreach (var issue in ProjectPreflight.ValidateDependencies(document.Summarize())) report.Issues.Add(new("ERROR", area, issue.Message));
    }

    private static IReadOnlyList<ProjectAnalysisTask> ToSafeTasks(RfPrjDocument document) => document.Tasks.Select((task, position) =>
    {
        var appKey = TaskXml.Get(task, "SelectedDesfireAppKeyNumberCurrent"); var read = TaskXml.Get(task, "SelectedDesfireReadKeyNumber"); var write = TaskXml.Get(task, "SelectedDesfireWriteKeyNumber");
        var keyReference = string.Join(" · ", new[] { appKey.Length > 0 ? "App Key" + appKey : "", read.Length > 0 ? "R Key" + read : "", write.Length > 0 ? "W Key" + write : "" }.Where(value => value.Length > 0));
        var size = TaskXml.Get(task, "FileSizeCurrent"); var communication = TaskXml.Get(task, "SelectedDesfireFileCryptoMode");
        return new ProjectAnalysisTask(position + 1, TaskXml.Get(task, "CurrentTaskIndex"), TaskXml.Get(task, "SelectedTaskType"), TaskXml.Get(task, "SelectedTaskDescription"),
            TaskXml.Get(task, "SelectedExecuteConditionTaskIndex"), TaskXml.Get(task, "SelectedExecuteConditionErrorLevel"), Aid(task), TaskXml.Get(task, "FileNumberCurrent"), keyReference,
            Settings(task), string.Join(" · ", new[] { size.Length > 0 ? size + " B" : "", communication }.Where(value => value.Length > 0)));
    }).ToArray();

    private static string FormatText(ProjectAnalysisReport report)
    {
        var lines = new List<string> { "RFiDGear Projektanalyse / Dry-Run", new('=', 38), "Projekt: " + report.Project, "Profile: " + string.Join(" + ", report.Profiles), "", "KARTEN-VORSCHAU", "---------------" };
        foreach (var app in report.Applications)
        {
            lines.Add($"AID {app.Aid} | {app.Name} | {app.KeyType} | {app.KeyCount} Keys | Transport {app.TransportKeySettings} -> Produktiv {app.ProductiveKeySettings} | {app.ChangeKeyMode}");
            foreach (var file in app.Files) lines.Add($"  File {file.Id:D2} | {file.Name} | {file.Type} | {file.Size} B | {file.Communication} | {file.Access}" + (file.CreationSettings is null ? "" : " | temporär " + file.CreationSettings + " -> ChangeFileSettings") + (file.CodingOutput.Length == 0 ? "" : " | Kodierdaten " + file.CodingOutput));
        }
        AddTasks(lines, "MASTERING-BUILDPLAN", report.MasteringTasks); AddTasks(lines, "KODIER-BUILDPLAN", report.CodingTasks);
        lines.Add(""); lines.Add("DRY-RUN / VALIDIERUNG"); lines.Add("--------------------");
        if (report.Issues.Count == 0) lines.Add("OK: keine Inkonsistenzen im erzeugten Taskplan gefunden.");
        else lines.AddRange(report.Issues.Select(issue => $"[{issue.Severity}] {issue.Area}: {issue.Message}"));
        lines.Add(""); lines.Add($"Ergebnis: {report.Errors} Fehler, {report.Warnings} Warnungen"); lines.Add("Schlüsselwerte: nicht enthalten");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void AddTasks(List<string> lines, string title, IReadOnlyList<ProjectAnalysisTask> tasks)
    {
        if (tasks.Count == 0) return; lines.Add(""); lines.Add(title); lines.Add(new('-', title.Length));
        foreach (var task in tasks) lines.Add($"{task.Index,4}  {task.Type,-36} {task.Description}" + (task.ConditionIndex.Length == 0 ? "" : $" [wenn Task {task.ConditionIndex} = {task.ConditionLevel}]"));
    }

    private static void AtomicWrite(string path, string text)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { var bytes = new UTF8Encoding(false).GetBytes(text); stream.Write(bytes); stream.Flush(true); } File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void AssertKeyFree(params string[] paths)
    {
        foreach (var path in paths)
        {
            var text = File.ReadAllText(path);
            if (System.Text.RegularExpressions.Regex.IsMatch(text, "(?i)(key|secret|cipher|picc)[^\\r\\n]{0,40}[=:]?[ \"]*[0-9A-F]{32,48}")) throw new InvalidDataException("Projektanalyse enthält mögliches Schlüsselmaterial: " + path);
        }
    }

    private static string Aid(XElement task)
    {
        var value = TaskXml.Get(task, "AppNumberCurrent"); if (value.Length == 0 || value == "0") value = TaskXml.Get(task, "AppNumberNew");
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..]; return value.ToUpperInvariant();
    }
    private static int Int(XElement task, string field) => int.TryParse(TaskXml.Get(task, field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    private static string Settings(XElement task)
    {
        var value = (Bool(task, "IsAllowChangeMKChecked") ? 1 : 0) | (Bool(task, "IsAllowListingWithoutMKChecked") ? 2 : 0) | (Bool(task, "IsAllowCreateDelWithoutMKChecked") ? 4 : 0) | (Bool(task, "IsAllowConfigChangableChecked") ? 8 : 0);
        return value.ToString("X2", CultureInfo.InvariantCulture);
    }
    private static bool Bool(XElement task, string field) => string.Equals(TaskXml.Get(task, field), "true", StringComparison.OrdinalIgnoreCase);
    private static bool Equal(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static string Access(AccessDefinition access) => $"R{access.Read}/W{access.Write}/RW{access.ReadWrite}/C{access.Change}";
    private static bool RequiresZeroFill(FileDefinition file) => file.MasteringInitialization?.Type switch { "none" => false, "zeroFill" or null => file.Type is "StdDataFile" or "BackupFile", _ => false };

    private sealed record ExpectedTask(string Type, string Aid, int? FileId, FileDefinition? File, bool CreationSettings, int? KeyNumber = null);
    private sealed class ApplicationState(string[] keys, string settings) { public string[] Keys { get; } = keys; public string Settings { get; set; } = settings; }
}

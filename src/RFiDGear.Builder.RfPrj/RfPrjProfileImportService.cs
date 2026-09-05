using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record RfPrjImportIssue(string Severity, string Code, string Message, string? Aid = null, int? FileId = null, string? TaskIndex = null);

public sealed class RfPrjImportedApplication
{
    public ApplicationDefinition Definition { get; init; } = new();
    public bool SuggestedInclude { get; init; }
    public string ExistingProfile { get; init; } = "";
    public string Confidence { get; init; } = "";
    public int SourceTaskCount { get; init; }
}

public sealed class RfPrjProfileImportReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string SourcePath { get; init; } = "";
    public string ManifestVersion { get; init; } = "";
    public int TaskCount { get; init; }
    public List<RfPrjImportedApplication> Applications { get; init; } = [];
    public List<RfPrjImportIssue> Issues { get; init; } = [];
    public bool ContainsKeys => false;
    public bool CanCreateProposal => Applications.Count > 0 && Issues.All(issue => issue.Severity != "ERROR");
    public int Errors => Issues.Count(issue => issue.Severity == "ERROR");
    public int Warnings => Issues.Count(issue => issue.Severity == "WARNUNG");
}

public sealed record RfPrjProfileImportFiles(RfPrjProfileImportReport Report, string TextPath, string JsonPath);

public sealed partial class RfPrjProfileImportService
{
    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.Ordinal)
    {
        "AppExistCheck", "CreateApplication", "CreateFile", "WriteData", "ReadData", "CheckFileSettings", "ChangeFileSettings",
        "ApplicationKeyChangeover", "ApplicationKeySettingsChangeover", "PICCMasterKeyChangeover", "PICCMasterKeySettingsChangeover"
    };
    private static readonly HashSet<string> AppTaskTypes = new(StringComparer.Ordinal)
    {
        "AppExistCheck", "CreateApplication", "CreateFile", "WriteData", "ReadData", "CheckFileSettings", "ChangeFileSettings",
        "ApplicationKeyChangeover", "ApplicationKeySettingsChangeover"
    };
    private static readonly HashSet<string> FileTaskTypes = new(StringComparer.Ordinal) { "CreateFile", "WriteData", "ReadData", "CheckFileSettings", "ChangeFileSettings" };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public RfPrjProfileImportReport Analyze(string path, ProfileLibrary? library = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var document = RfPrjArchive.Load(fullPath);
        var report = new RfPrjProfileImportReport
        {
            SourcePath = Sanitize(fullPath),
            ManifestVersion = document.ManifestVersion,
            TaskCount = document.Tasks.Count
        };
        if (document.ManifestVersion != "2.0.6")
            report.Issues.Add(new("WARNUNG", "MANIFEST_VERSION", $"rfPrj-Manifest '{document.ManifestVersion}' ist nicht der verifizierte Stand 2.0.6."));
        foreach (var issue in ProjectPreflight.ValidateDependencies(document.Summarize()))
            report.Issues.Add(new("ERROR", issue.Code, issue.Message));

        var snapshots = document.Tasks.Select((task, position) => Snapshot(task, position, report)).ToArray();
        foreach (var unsupported in snapshots.Where(task => !string.IsNullOrWhiteSpace(task.Type) && !SupportedTaskTypes.Contains(task.Type)).Select(task => task.Type).Distinct(StringComparer.Ordinal))
            report.Issues.Add(new("WARNUNG", "TASK_UNSUPPORTED", $"Tasktyp '{Sanitize(unsupported)}' wird nicht in das Profilmodell übernommen."));

        foreach (var group in snapshots.Where(task => AppTaskTypes.Contains(task.Type) && task.Aid is not null).GroupBy(task => task.Aid!, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var application = ImportApplication(group.Key, group.ToArray(), report);
            var matches = library?.Profiles.Select(profile => (Profile: profile, App: profile.Applications.SingleOrDefault(candidate => string.Equals(ProfileValidator.NormalizeAid(candidate.Id), group.Key, StringComparison.OrdinalIgnoreCase))))
                .Where(item => item.App is not null).ToArray() ?? [];
            var exact = matches.FirstOrDefault(item => StructurallyEquivalent(application, item.App!));
            var existing = exact.Profile is not null ? $"Entspricht {exact.Profile.Profile.Id} {exact.Profile.Profile.Version}"
                : matches.Length > 0 ? "AID existiert mit abweichender Struktur in " + string.Join(", ", matches.Select(item => item.Profile.Profile.Id)) : "Neu";
            report.Applications.Add(new RfPrjImportedApplication
            {
                Definition = application,
                SuggestedInclude = exact.Profile is null,
                ExistingProfile = existing,
                Confidence = Confidence(group.Key, report),
                SourceTaskCount = group.Count()
            });
        }
        if (report.Applications.Count == 0) report.Issues.Add(new("ERROR", "NO_APPLICATIONS", "Keine unterstützte DESFire-Application konnte aus der rfPrj-Datei ermittelt werden."));
        if (snapshots.Any(task => task.Type == "PICCMasterKeyChangeover"))
            report.Issues.Add(new("WARNUNG", "PICC_NOT_PROFILED", "PICC-Keywechsel wurde erkannt. Schlüsselwerte werden absichtlich weder gelesen noch in den Profilvorschlag übernommen."));
        return report;
    }

    public ProfileDefinition CreateProposal(RfPrjProfileImportReport report, IEnumerable<string> selectedAids,
        string id, string code, string name, string version, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.CanCreateProposal)
            throw new InvalidDataException("Profilvorschlag ist wegen Importfehlern gesperrt. Bitte zuerst die ausgewiesenen Widersprüche klären.");
        var selected = selectedAids.Select(ProfileValidator.NormalizeAid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0) throw new InvalidDataException("Mindestens eine importierte Application muss ausgewählt werden.");
        var missing = selected.Except(report.Applications.Select(application => ProfileValidator.NormalizeAid(application.Definition.Id)), StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0) throw new InvalidDataException("Nicht im Import enthaltene AID-Auswahl: " + string.Join(", ", missing));
        var proposal = new ProfileDefinition
        {
            Profile = new ProfileMetadata
            {
                Id = id.Trim(), Code = code.Trim(), Name = name.Trim(), Version = version.Trim(), Kind = "customer",
                Description = string.IsNullOrWhiteSpace(description) ? "Geprüfter Profilvorschlag aus " + Path.GetFileName(report.SourcePath) : description.Trim()
            },
            Applications = report.Applications.Where(application => selected.Contains(ProfileValidator.NormalizeAid(application.Definition.Id)))
                .Select(application => application.Definition).ToList()
        };
        proposal = new ProfileEditingService().Clone(proposal);
        var issues = ProfileValidator.Validate(proposal);
        if (issues.Count > 0) throw new InvalidDataException("Erzeugter Profilvorschlag ist noch nicht valide:\n" + string.Join("\n", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        return proposal;
    }

    public RfPrjProfileImportFiles SaveReport(RfPrjProfileImportReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var textPath = Path.Combine(root, "rfprj-import.txt");
        var jsonPath = Path.Combine(root, "rfprj-import.json");
        AtomicWrite(textPath, FormatText(report));
        AtomicWrite(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        return new(report, textPath, jsonPath);
    }

    private static ApplicationDefinition ImportApplication(string aid, TaskSnapshot[] tasks, RfPrjProfileImportReport report)
    {
        var create = tasks.Where(task => task.Type == "CreateApplication").ToArray();
        var primary = create.Length > 0 ? create : tasks;
        var name = ResolveName(aid, tasks, report);
        var keyType = Resolve(primary.Select(task => task.Value("SelectedDesfireAppKeyEncryptionTypeCreateNewApp"))
            .Concat(tasks.Select(task => task.Value("SelectedDesfireAppKeyEncryptionTypeCurrent"))), "DF_KEY_AES", "KEY_TYPE", aid, null, report,
            value => value is "DF_KEY_DES" or "DF_KEY_AES" or "DF_KEY_3K3DES");
        var declaredCounts = primary.Select(task => ParseInt(task.Value("SelectedDesfireAppMaxNumberOfKeys"))).Where(value => value is > 0 and <= 14).Select(value => value!.Value).Distinct().ToArray();
        if (declaredCounts.Length > 1) report.Issues.Add(new("ERROR", "KEY_COUNT_CONFLICT", $"AID {aid}: widersprüchliche Keyanzahlen {string.Join("/", declaredCounts)}.", aid));
        var keyCount = declaredCounts.FirstOrDefault();
        var observedKey = tasks.SelectMany(task => new[]
        {
            ParseInt(task.Value("SelectedDesfireAppKeyNumberCurrent")), ParseAccess(task.Value("SelectedDesfireFileAccessRightRead")),
            ParseAccess(task.Value("SelectedDesfireFileAccessRightWrite")), ParseAccess(task.Value("SelectedDesfireFileAccessRightReadWrite")), ParseAccess(task.Value("SelectedDesfireFileAccessRightChange"))
        }).Where(value => value is >= 0 and <= 13).Select(value => value!.Value).DefaultIfEmpty(0).Max() + 1;
        if (keyCount == 0) { keyCount = observedKey; report.Issues.Add(new("WARNUNG", "KEY_COUNT_INFERRED", $"AID {aid}: Keyanzahl {keyCount} wurde aus verwendeten Keynummern abgeleitet.", aid)); }
        else if (observedKey > keyCount) report.Issues.Add(new("ERROR", "KEY_COUNT_TOO_SMALL", $"AID {aid}: deklarierte Keyanzahl {keyCount}, aber Key {observedKey - 1} wird referenziert.", aid));

        var transport = create.Length > 0 ? ReadKeySettings(create[0]) : "0F";
        if (create.Length == 0) report.Issues.Add(new("WARNUNG", "CREATE_APPLICATION_MISSING", $"AID {aid}: CreateApplication fehlt; Transport-KeySettings wurden mit 0F vorbelegt.", aid));
        var finalSettingsTasks = tasks.Where(task => task.Type == "ApplicationKeySettingsChangeover").ToArray();
        var productive = finalSettingsTasks.Length > 0 ? ReadKeySettings(finalSettingsTasks[^1]) : transport;
        if (finalSettingsTasks.Length == 0) report.Issues.Add(new("WARNUNG", "PRODUCTIVE_SETTINGS_UNKNOWN", $"AID {aid}: kein ApplicationKeySettingsChangeover; produktive KeySettings müssen geprüft werden.", aid));
        var changeMode = Resolve(primary.Select(task => task.Value("SelectedDesfireAppKeySettingsCreateNewApp")), "ChangeKeyUsingMK", "CHANGE_MODE", aid, null, report,
            value => value is "ChangeKeyUsingMK" or "ChangeKeyUsingKeyNo");

        var files = new List<FileDefinition>();
        foreach (var fileGroup in tasks.Where(task => FileTaskTypes.Contains(task.Type) && task.FileId is not null)
                     .GroupBy(task => task.FileId!.Value).OrderBy(group => group.Key))
            files.Add(ImportFile(aid, fileGroup.Key, fileGroup.ToArray(), keyCount, report));
        if (files.Count == 0) report.Issues.Add(new("WARNUNG", "NO_FILES", $"AID {aid}: keine unterstützten File-Tasks gefunden.", aid));
        return new ApplicationDefinition
        {
            Id = aid, Name = name, KeyType = keyType, KeyCount = Math.Clamp(keyCount, 1, 14),
            TransportKeySettings = transport, ProductiveKeySettings = productive, ChangeKeyMode = changeMode, Files = files
        };
    }

    private static FileDefinition ImportFile(string aid, int fileId, TaskSnapshot[] tasks, int keyCount, RfPrjProfileImportReport report)
    {
        var creates = tasks.Where(task => task.Type == "CreateFile").ToArray();
        var changes = tasks.Where(task => task.Type == "ChangeFileSettings").ToArray();
        var fallback = tasks.Where(task => task.Type is "ReadData" or "WriteData").ToArray();
        var initialSource = creates.Length > 0 ? creates : fallback;
        var finalSource = changes.Length > 0 ? changes : initialSource;
        if (creates.Length == 0) report.Issues.Add(new("WARNUNG", "CREATE_FILE_MISSING", $"AID {aid} File {fileId}: CreateFile fehlt; Struktur wurde aus Lese-/Schreibaufgaben abgeleitet.", aid, fileId));
        var initial = ReadFileSettings(initialSource, aid, fileId, keyCount, report);
        var final = ReadFileSettings(finalSource, aid, fileId, keyCount, report);
        var type = Resolve(finalSource.Select(task => task.Value("SelectedDesfireFileType")), "StdDataFile", "FILE_TYPE", aid, fileId, report, value => value is "StdDataFile" or "BackupFile");
        var sizes = finalSource.Select(task => ParseInt(task.Value("FileSizeCurrent"))).Where(value => value is > 0 and <= 65535).Select(value => value!.Value).Distinct().ToArray();
        if (sizes.Length != 1) report.Issues.Add(new("ERROR", "FILE_SIZE", $"AID {aid} File {fileId}: Größe fehlt oder ist widersprüchlich.", aid, fileId));
        var size = sizes.FirstOrDefault();
        var name = ResolveFileName(fileId, creates);
        var writeTasks = tasks.Where(task => task.Type == "WriteData").ToArray();
        var zeroFill = writeTasks.Any(task => IsExplicitZeroInitialization(task, size));
        if (writeTasks.Any() && !zeroFill)
            report.Issues.Add(new("WARNUNG", "CODING_RECIPE_UNKNOWN", $"AID {aid} File {fileId}: Schreibdaten erkannt; ursprüngliches Variablen-/Kodierrezept ist aus rfPrj nicht sicher rekonstruierbar und bleibt leer.", aid, fileId));
        return new FileDefinition
        {
            Id = fileId, Name = name, Type = type, Size = Math.Max(1, size), Communication = final.Communication, Access = final.Access,
            CreationSettings = changes.Length > 0 ? initial : null,
            MasteringInitialization = zeroFill ? null : new MasteringInitialization { Type = "none" },
            Coding = null
        };
    }

    private static FileSettingsDefinition ReadFileSettings(TaskSnapshot[] tasks, string aid, int fileId, int keyCount, RfPrjProfileImportReport report)
    {
        var communication = Resolve(tasks.Select(task => task.Value("SelectedDesfireFileCryptoMode")), "CM_PLAIN", "FILE_COMMUNICATION", aid, fileId, report,
            value => value is "CM_PLAIN" or "CM_MAC" or "CM_ENCRYPT");
        int Access(string field)
        {
            var values = tasks.Select(task => ParseAccess(task.Value(field))).Where(value => value is not null).Select(value => value!.Value).Distinct().ToArray();
            if (values.Length != 1 || values[0] < 0 || values[0] >= keyCount)
            {
                report.Issues.Add(new("ERROR", "FILE_ACCESS", $"AID {aid} File {fileId}: {field} fehlt, widerspricht sich oder liegt außerhalb der Keyanzahl.", aid, fileId));
                return 0;
            }
            return values[0];
        }
        return new FileSettingsDefinition
        {
            Communication = communication,
            Access = new AccessDefinition
            {
                Read = Access("SelectedDesfireFileAccessRightRead"), Write = Access("SelectedDesfireFileAccessRightWrite"),
                ReadWrite = Access("SelectedDesfireFileAccessRightReadWrite"), Change = Access("SelectedDesfireFileAccessRightChange")
            }
        };
    }

    private static TaskSnapshot Snapshot(XElement task, int position, RfPrjProfileImportReport report)
    {
        var type = TaskXml.Get(task, "SelectedTaskType");
        var index = TaskXml.Get(task, "CurrentTaskIndex");
        var fields = task.Elements().Where(element => element.Name.LocalName is
            "SelectedDesfireAppKeyEncryptionTypeCreateNewApp" or "SelectedDesfireAppKeyEncryptionTypeCurrent" or
            "SelectedDesfireAppMaxNumberOfKeys" or "SelectedDesfireAppKeySettingsCreateNewApp" or
            "IsAllowChangeMKChecked" or "IsAllowListingWithoutMKChecked" or "IsAllowCreateDelWithoutMKChecked" or "IsAllowConfigChangableChecked" or
            "SelectedDesfireAppKeyNumberCurrent" or "SelectedDesfireFileType" or "SelectedDesfireFileCryptoMode" or
            "SelectedDesfireFileAccessRightRead" or "SelectedDesfireFileAccessRightWrite" or "SelectedDesfireFileAccessRightReadWrite" or "SelectedDesfireFileAccessRightChange" or
            "FileSizeCurrent").ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);
        var aidCandidates = (type is "CreateApplication" or "AppExistCheck"
                ? new[] { TaskXml.Get(task, "AppNumberNew"), TaskXml.Get(task, "AppNumberCurrent"), TaskXml.Get(task, "AppNumberTarget") }
                : new[] { TaskXml.Get(task, "AppNumberCurrent"), TaskXml.Get(task, "AppNumberNew"), TaskXml.Get(task, "AppNumberTarget") })
            .Select(TryAid).Where(value => value is not null).Select(value => value!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (aidCandidates.Length > 1) report.Issues.Add(new("ERROR", "TASK_AID_CONFLICT", $"Task {index} enthält widersprüchliche AIDs.", TaskIndex: index));
        var fileId = ParseInt(TaskXml.Get(task, "FileNumberCurrent"));
        if (fileId is < 0 or > 31) fileId = null;
        return new TaskSnapshot(position, index, type, Sanitize(TaskXml.Get(task, "SelectedTaskDescription")), aidCandidates.FirstOrDefault(), fileId, fields, task);
    }

    private static string Resolve(IEnumerable<string> source, string fallback, string code, string aid, int? fileId, RfPrjProfileImportReport report, Func<string, bool> valid)
    {
        var values = source.Where(value => !string.IsNullOrWhiteSpace(value) && valid(value)).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 1) return values[0];
        report.Issues.Add(new(values.Length == 0 ? "WARNUNG" : "ERROR", code,
            values.Length == 0 ? $"AID {aid}{FileLabel(fileId)}: Wert fehlt; '{fallback}' ist nur ein zu prüfender Platzhalter." : $"AID {aid}{FileLabel(fileId)}: widersprüchliche Werte {string.Join("/", values)}.", aid, fileId));
        return values.FirstOrDefault() ?? fallback;
    }

    private static string ReadKeySettings(TaskSnapshot task) => ProfileEditingService.KeySettingsFromFlags(
        ParseBool(task.Value("IsAllowChangeMKChecked")), ParseBool(task.Value("IsAllowListingWithoutMKChecked")),
        ParseBool(task.Value("IsAllowCreateDelWithoutMKChecked")), ParseBool(task.Value("IsAllowConfigChangableChecked")));

    private static string ResolveName(string aid, TaskSnapshot[] tasks, RfPrjProfileImportReport report)
    {
        var names = tasks.Select(task => task.Description.Split(':', 2)[0].Trim()).Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 100)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length == 1) return names[0];
        if (names.Length > 1) report.Issues.Add(new("WARNUNG", "APPLICATION_NAME", $"AID {aid}: mehrere Bezeichnungen erkannt; neutraler Importname wird verwendet.", aid));
        return "Import AID " + aid;
    }

    private static string ResolveFileName(int fileId, TaskSnapshot[] createTasks)
    {
        foreach (var task in createTasks)
        {
            var match = FileNameRegex().Match(task.Description);
            if (match.Success) return Sanitize(match.Groups[1].Value.Trim());
        }
        return $"File {fileId:D2}";
    }

    private static bool IsExplicitZeroInitialization(TaskSnapshot task, int size)
    {
        if (!(task.Description.Contains("initialis", StringComparison.OrdinalIgnoreCase) || task.Description.Contains("zero-fill", StringComparison.OrdinalIgnoreCase))) return false;
        var hex = task.Xml.Descendants("DataAsHexString").Select(element => element.Value.Trim()).FirstOrDefault(value => value.Length > 0);
        return size > 0 && hex is not null && hex.Length == size * 2 && hex.All(character => character == '0');
    }

    private static bool StructurallyEquivalent(ApplicationDefinition left, ApplicationDefinition right)
    {
        if (left.KeyType != right.KeyType || left.KeyCount != right.KeyCount || left.ProductiveKeySettings != right.ProductiveKeySettings || left.ChangeKeyMode != right.ChangeKeyMode) return false;
        var a = left.Files.OrderBy(file => file.Id).ToArray();
        var b = right.Files.OrderBy(file => file.Id).ToArray();
        return a.Length == b.Length && a.Zip(b).All(pair => pair.First.Id == pair.Second.Id && pair.First.Type == pair.Second.Type && pair.First.Size == pair.Second.Size &&
            pair.First.Communication == pair.Second.Communication && AccessEqual(pair.First.Access, pair.Second.Access));
    }

    private static bool AccessEqual(AccessDefinition a, AccessDefinition b) => a.Read == b.Read && a.Write == b.Write && a.ReadWrite == b.ReadWrite && a.Change == b.Change;
    private static string Confidence(string aid, RfPrjProfileImportReport report) => report.Issues.Any(issue => issue.Aid == aid && issue.Severity == "ERROR") ? "Widerspruch" : report.Issues.Any(issue => issue.Aid == aid && issue.Severity == "WARNUNG") ? "Prüfung nötig" : "Hoch";
    private static int? ParseInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static int? ParseAccess(string value) { var match = AccessRegex().Match(value ?? ""); return match.Success && int.TryParse(match.Groups[1].Value, out var result) ? result : null; }
    private static bool ParseBool(string value) => bool.TryParse(value, out var result) && result;
    private static string? TryAid(string value) { if (string.IsNullOrWhiteSpace(value) || value.Trim() is "0" or "0x0") return null; try { return ProfileValidator.NormalizeAid(value); } catch (InvalidDataException) { return null; } }
    private static string FileLabel(int? fileId) => fileId is null ? "" : $" File {fileId}";
    private static string Sanitize(string value) => LongHexRegex().Replace(new(value.Where(character => !char.IsControl(character)).Take(160).ToArray()), "[HEX REDACTED]");

    private static string FormatText(RfPrjProfileImportReport report)
    {
        var lines = new List<string> { "RFiDGear rfPrj-Profilimport", new('=', 40), "Quelle: " + report.SourcePath, $"Manifest: {report.ManifestVersion} · Tasks: {report.TaskCount}", "" };
        foreach (var application in report.Applications)
        {
            var value = application.Definition;
            lines.Add($"AID {value.Id} · {value.Name} · {value.KeyType}/{value.KeyCount} Keys · {application.Confidence}");
            lines.Add($"  Empfehlung: {(application.SuggestedInclude ? "übernehmen" : "nicht übernehmen")} · {application.ExistingProfile}");
            foreach (var file in value.Files) lines.Add($"  File {file.Id:D2} · {file.Name} · {file.Type} · {file.Size} B · {file.Communication} · R{file.Access.Read}/W{file.Access.Write}/RW{file.Access.ReadWrite}/C{file.Access.Change}");
        }
        lines.Add("");
        foreach (var issue in report.Issues) lines.Add($"{issue.Severity} {issue.Code}: {issue.Message}");
        lines.Add(""); lines.Add($"Ergebnis: {report.Errors} Fehler · {report.Warnings} Warnungen · Profilvorschlag {(report.CanCreateProposal ? "möglich" : "gesperrt")}");
        lines.Add("Schlüsselwerte: nicht gelesen / nicht enthalten");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void AtomicWrite(string path, string text)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = new UTF8Encoding(false).GetBytes(text); stream.Write(bytes); stream.Flush(true);
            stream.Close(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record TaskSnapshot(int Position, string Index, string Type, string Description, string? Aid, int? FileId,
        IReadOnlyDictionary<string, string> Fields, XElement Xml)
    {
        public string Value(string name) => Fields.GetValueOrDefault(name, "");
    }

    [GeneratedRegex("AR_KEY([0-9]{1,2})$", RegexOptions.CultureInvariant)] private static partial Regex AccessRegex();
    [GeneratedRegex("File\\s+\\d+\\s+'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex FileNameRegex();
    [GeneratedRegex("(?i)[0-9a-f]{32,}", RegexOptions.CultureInvariant)] private static partial Regex LongHexRegex();
}

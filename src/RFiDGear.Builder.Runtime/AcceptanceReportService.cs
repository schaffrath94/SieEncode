using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Runtime;

public sealed record AcceptanceCheck(
    string Status,
    string Area,
    string Subject,
    string Expected,
    string Actual,
    string Detail,
    string EvidenceLevel,
    string Evidence);

public sealed record AcceptanceSummary(int Ok, int Deviation, int NotVerifiable, int Total);
public sealed record AcceptanceCoverage(int ByteExact, int Operational, int WithoutActualValue, int Total);

public sealed class AcceptanceReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string ReportId { get; init; } = Guid.NewGuid().ToString("N");
    public string GeneratedAt { get; init; } = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
    public string ProjectName { get; init; } = "";
    public string ProjectDirectory { get; init; } = "";
    public string PlanFile { get; init; } = "";
    public string? LastCodingFile { get; init; }
    public string Result { get; init; } = "";
    public AcceptanceSummary Summary { get; init; } = new(0, 0, 0, 0);
    public AcceptanceCoverage Coverage { get; init; } = new(0, 0, 0, 0);
    public IReadOnlyList<AcceptanceCheck> Checks { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } =
    [
        "Application-KeySettings und Keyanzahl werden nur bei aktueller, SHA-256-korrelierter RFiDGear-Task-Evidenz als geprüft bewertet.",
        "Filetyp, vollständige Filegröße, Communication Mode und alle vier Access Rights werden nur bei aktueller, SHA-256-korrelierter CheckFileSettings-Exact-Match-Evidenz als geprüft bewertet."
    ];
    public string Note { get; init; } = "Der Bericht enthält keine Schlüsselwerte. Nicht prüfbare Punkte werden nicht stillschweigend als erfolgreich behandelt.";
}

public sealed record AcceptanceReportResult(AcceptanceReport Report, string JsonPath, string TextPath);

public sealed class AcceptanceReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public AcceptanceReportResult Create(
        string projectDirectory,
        string? lastCodingFile = null,
        string? outputDirectory = null,
        string reportPrefix = "Pruefbericht",
        DateTime? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        var planPath = SafeChild(root, "acceptance.plan.json", "Abnahmeplan");
        if (!File.Exists(planPath)) throw new FileNotFoundException("acceptance.plan.json fehlt.", planPath);
        using var planDocument = JsonDocument.Parse(File.ReadAllText(planPath));
        var plan = planDocument.RootElement;
        if (RequiredString(plan, "schemaVersion") != "1.0")
            throw new InvalidDataException("Nicht unterstützte acceptance.plan.json-Version: " + RequiredString(plan, "schemaVersion"));

        lastCodingFile = string.IsNullOrWhiteSpace(lastCodingFile) ? SafeChild(root, "Letzte_Kodierung.json", "Letzte Kodierung") : Path.GetFullPath(lastCodingFile);
        JsonDocument? lastDocument = File.Exists(lastCodingFile) ? JsonDocument.Parse(File.ReadAllText(lastCodingFile)) : null;
        try
        {
            var last = lastDocument?.RootElement;
            var lastTimestamp = ParseTimestamp(last);
            var verificationDirectory = OptionalString(plan, "verificationDirectory") ?? "verification";
            _ = SafeChild(root, verificationDirectory, "Verification-Ordner");
            var payloads = ReadPayloadEvidence(last);
            var checks = new List<AcceptanceCheck>();
            var appReadEvidence = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var applicationEvidence = LoadApplicationEvidence(root, plan, checks);

            foreach (var app in RequiredArray(plan, "applications"))
            {
                var aid = RequiredString(app, "aid").ToUpperInvariant();
                appReadEvidence[aid] = false;
                foreach (var file in RequiredArray(app, "files"))
                {
                    var fileId = RequiredInt(file, "id");
                    var output = OptionalString(file, "codingOutput");
                    if (string.IsNullOrWhiteSpace(output)) continue;
                    var key = aid + ":" + fileId;
                    if (!payloads.TryGetValue(key, out var payload))
                    {
                        checks.Add(Check("Nicht prüfbar", "Daten", $"{aid} / File {fileId} – Nutzdaten", "Payload gemäß aktuellem Kartenwert", "", "Keine passende Letzte_Kodierung.json-Payload vorhanden.", "Kein Istwert"));
                        continue;
                    }

                    var verificationPath = string.IsNullOrWhiteSpace(payload.VerificationPath)
                        ? SafeChild(root, Path.Combine(verificationDirectory, output), "Verification-Datei")
                        : Path.GetFullPath(payload.VerificationPath);
                    EnsureWithin(root, verificationPath, "Verification-Datei");
                    if (!File.Exists(verificationPath))
                    {
                        checks.Add(Check("Nicht prüfbar", "Daten", $"{aid} / File {fileId} – Nutzdaten", payload.ExpectedHex, "", "Finale Verifikationsdatei fehlt. Der vollständige RFiDGear-Kodierlauf wurde möglicherweise noch nicht ausgeführt.", "Kein Istwert", verificationPath));
                        continue;
                    }
                    if (lastTimestamp is not null && File.GetLastWriteTimeUtc(verificationPath) < lastTimestamp.Value.UtcDateTime)
                    {
                        checks.Add(Check("Nicht prüfbar", "Daten", $"{aid} / File {fileId} – Nutzdaten", payload.ExpectedHex, "", "Verifikationsdatei ist älter als die letzte Payload-Bereitstellung und wird deshalb nicht als aktueller Nachweis gewertet.", "Kein Istwert", verificationPath));
                        continue;
                    }

                    var actualHex = Convert.ToHexString(ReadDataBytes(verificationPath));
                    var expectedHex = NormalizeExpectedHex(payload.ExpectedHex);
                    if (string.Equals(actualHex, expectedHex, StringComparison.Ordinal))
                    {
                        checks.Add(Check("OK", "Daten", $"{aid} / File {fileId} – Nutzdaten", expectedHex, actualHex, "Finaler ReadData-Nachweis stimmt bytegenau mit dem Soll überein.", "Bytegenau", verificationPath));
                        var access = RequiredProperty(file, "access");
                        checks.Add(Check("OK", "Datei", $"{aid} / File {fileId} – erreichbar", "File mit produktivem Read-Zugriff lesbar", "Lesen erfolgreich", $"ReadData mit Keyslot {RequiredInt(access, "read")} / {RequiredString(app, "keyType")} war erfolgreich. Schlüsselwert wird nicht protokolliert.", "Operativ", verificationPath));
                        appReadEvidence[aid] = true;
                    }
                    else
                        checks.Add(Check("Abweichung", "Daten", $"{aid} / File {fileId} – Nutzdaten", expectedHex, actualHex, "Finaler ReadData-Nachweis weicht vom Soll ab.", "Bytegenau", verificationPath));
                }
            }

            foreach (var app in RequiredArray(plan, "applications")) AddMetadataChecks(app, appReadEvidence, applicationEvidence, checks);
            checks.Add(Check("Nicht prüfbar", "PICC", "PICC – produktiver MasterKey/KeySettings", "Projektdefinierter Produktivzustand", "", "Keywerte werden bewusst nicht protokolliert. Für PICC-Key/Settings existiert im aktuellen Abnahmemodell noch kein verifizierter finaler Readback.", "Kein Istwert"));

            var summary = new AcceptanceSummary(
                checks.Count(check => check.Status == "OK"),
                checks.Count(check => check.Status == "Abweichung"),
                checks.Count(check => check.Status == "Nicht prüfbar"),
                checks.Count);
            var coverage = new AcceptanceCoverage(
                checks.Count(check => check.EvidenceLevel == "Bytegenau"),
                checks.Count(check => check.EvidenceLevel == "Operativ"),
                checks.Count(check => check.EvidenceLevel == "Kein Istwert"),
                checks.Count);
            var result = summary.Deviation > 0 ? "Abweichung" : summary.Ok > 0 ? "OK im automatisch prüfbaren Umfang" : "Nicht prüfbar";
            var generated = now ?? DateTime.Now;
            var report = new AcceptanceReport
            {
                GeneratedAt = generated.ToString("s", CultureInfo.InvariantCulture),
                ProjectName = RequiredString(plan, "projectName"),
                ProjectDirectory = root,
                PlanFile = planPath,
                LastCodingFile = File.Exists(lastCodingFile) ? Path.GetFullPath(lastCodingFile) : null,
                Result = result,
                Summary = summary,
                Coverage = coverage,
                Checks = checks
            };

            var reportRoot = string.IsNullOrWhiteSpace(outputDirectory) ? SafeChild(root, "reports", "Berichtsordner")
                : Path.IsPathRooted(outputDirectory) ? Path.GetFullPath(outputDirectory) : SafeChild(root, outputDirectory, "Berichtsordner");
            Directory.CreateDirectory(reportRoot);
            var safePrefix = Regex.Replace(reportPrefix ?? "", "[^A-Za-z0-9_.-]", "_");
            if (string.IsNullOrWhiteSpace(safePrefix)) safePrefix = "Pruefbericht";
            var stamp = generated.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var jsonPath = Path.Combine(reportRoot, $"{safePrefix}_{stamp}.json");
            var textPath = Path.Combine(reportRoot, $"{safePrefix}_{stamp}.txt");
            AtomicWrite(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
            AtomicWrite(textPath, BuildText(report));
            return new AcceptanceReportResult(report, jsonPath, textPath);
        }
        finally { lastDocument?.Dispose(); }
    }

    private static void AddMetadataChecks(JsonElement app, IReadOnlyDictionary<string, bool> appReadEvidence,
        IReadOnlyDictionary<string, ApplicationAcceptanceTaskEvidence> applicationEvidence, List<AcceptanceCheck> checks)
    {
        var aid = RequiredString(app, "aid").ToUpperInvariant();
        var settings = applicationEvidence.GetValueOrDefault(aid + ":ReadAppSettings");
        var count = applicationEvidence.GetValueOrDefault(aid + ":CheckAppKeyCount");
        checks.Add(appReadEvidence.GetValueOrDefault(aid) || settings?.Outcome == "Successful" || count?.Outcome == "Successful"
            ? Check("OK", "Application", $"{aid} – vorhanden", "Application erreichbar", "Durch finalen File-Read nachgewiesen", "Mindestens ein File der Application wurde nach Abschluss aller produktiven Security-Tasks erfolgreich gelesen.", "Operativ")
            : Check("Nicht prüfbar", "Application", $"{aid} – vorhanden", "Application vorhanden", "", "Mit den aktuell verifizierten Abnahme-Shells liegt kein eigenständiger finaler AppExistCheck-Nachweis vor.", "Kein Istwert"));
        checks.Add(settings?.Outcome == "Successful"
            ? Check("OK", "Application", $"{aid} – Keytyp", RequiredString(app, "keyType"), "Authentifizierung erfolgreich", "ReadAppSettings authentifizierte Keyslot 0 mit dem konfigurierten produktiven Keytyp; Schlüsselwert wird nicht protokolliert.", "Operativ")
            : Check("Nicht prüfbar", "Application", $"{aid} – Keytyp", RequiredString(app, "keyType"), settings?.ErrorCode ?? "", "Ohne erfolgreichen authentifizierten ReadAppSettings-Lauf liegt kein operativer Keytypnachweis vor.", "Kein Istwert"));
        checks.Add(TaskCheck(aid, count, "Keyanzahl", RequiredInt(app, "keyCount").ToString(CultureInfo.InvariantCulture), "CheckAppKeyCount verglich die auf der Karte gelesene maximale Keyanzahl exakt."));
        checks.Add(TaskCheck(aid, settings, "KeySettings", RequiredString(app, "keySettings"), $"ReadAppSettings verwendete den Exact-Match-Modus; Change-Key-Modus: {RequiredString(app, "changeKeyMode")}."));
        foreach (var file in RequiredArray(app, "files"))
        {
            var fileId = RequiredInt(file, "id");
            var fileEvidence = applicationEvidence.GetValueOrDefault(aid + ":CheckFileSettings:" + fileId.ToString(CultureInfo.InvariantCulture));
            AddFileMetadataChecks(aid, file, fileEvidence, checks);
        }
    }

    private static void AddFileMetadataChecks(string aid, JsonElement file, ApplicationAcceptanceTaskEvidence? evidence, List<AcceptanceCheck> checks)
    {
        var fileId = RequiredInt(file, "id");
        var subject = $"{aid} / File {fileId}";
        if (evidence?.Outcome == "Successful")
        {
            const string actual = "RFiDGear Exact-Match erfolgreich";
            const string detail = "CheckFileSettings hat den auf der Karte gelesenen Wert mit dem projektierten Soll verglichen; Schlüsselwerte werden nicht protokolliert.";
            checks.Add(Check("OK", "File-Metadaten", subject + " – Typ", RequiredString(file, "type"), actual, detail, "Operativ"));
            checks.Add(Check("OK", "File-Metadaten", subject + " – Größe", RequiredInt(file, "size") + " Byte", actual, detail, "Operativ"));
            checks.Add(Check("OK", "File-Metadaten", subject + " – Communication", RequiredString(file, "communication"), actual, detail, "Operativ"));
            checks.Add(Check("OK", "File-Metadaten", subject + " – Access Rights", RequiredString(file, "accessText"), actual, detail, "Operativ"));
            return;
        }
        if (evidence?.ErrorCode == "IsNotTrue")
        {
            checks.Add(Check("Abweichung", "File-Metadaten", subject + " – vollständiger Settingsvergleich",
                $"Typ {RequiredString(file, "type")}; Größe {RequiredInt(file, "size")} Byte; Communication {RequiredString(file, "communication")}; Access {RequiredString(file, "accessText")}",
                "RFiDGear: IsNotTrue", "Mindestens einer der vollständig verglichenen FileSettings-Werte weicht ab; das schlüsselfreie Host-Ergebnis behauptet bewusst nicht, welcher Einzelwert betroffen ist.", "Operativ"));
            return;
        }

        checks.Add(Check("Nicht prüfbar", "File-Metadaten", subject + " – Typ", RequiredString(file, "type"), evidence?.ErrorCode ?? "", "Der Filetyp ist im Soll dokumentiert, wird im aktuellen Abnahmelauf nicht separat ausgelesen.", "Kein Istwert"));
        checks.Add(Check("Nicht prüfbar", "File-Metadaten", subject + " – Größe", RequiredInt(file, "size") + " Byte", evidence?.ErrorCode ?? "", "Die Sollgröße ist dokumentiert. Ein erfolgreicher Payload-Read beweist den Dateninhalt, aber nicht sicher die vollständige konfigurierte Filegröße.", "Kein Istwert"));
        checks.Add(Check("Nicht prüfbar", "File-Metadaten", subject + " – Communication", RequiredString(file, "communication"), evidence?.ErrorCode ?? "", "Der Communication Mode ist im Soll dokumentiert; aktuell kein separater finaler GetFileSettings-Abnahmenachweis.", "Kein Istwert"));
        checks.Add(Check("Nicht prüfbar", "File-Metadaten", subject + " – Access Rights", RequiredString(file, "accessText"), evidence?.ErrorCode ?? "", "Die vier Sollrechte bleiben sichtbar. Ein erfolgreicher Read bestätigt nur den verwendeten Leseweg, nicht alle vier Access Rights.", "Kein Istwert"));
    }

    private static AcceptanceCheck TaskCheck(string aid, ApplicationAcceptanceTaskEvidence? evidence, string subject, string expected, string successDetail)
    {
        if (evidence?.Outcome == "Successful") return Check("OK", "Application", $"{aid} – {subject}", expected, "Exakter RFiDGear-Vergleich erfolgreich", successDetail, "Operativ");
        if (evidence?.ErrorCode == "IsNotTrue") return Check("Abweichung", "Application", $"{aid} – {subject}", expected, "RFiDGear: IsNotTrue", "Der strikt lesende Vergleich meldet eine Soll-/Ist-Abweichung.", "Operativ");
        return Check("Nicht prüfbar", "Application", $"{aid} – {subject}", expected, evidence?.ErrorCode ?? "", "Keine erfolgreiche aktuelle autoritative RFiDGear-Task-Evidenz vorhanden.", "Kein Istwert");
    }

    private static IReadOnlyDictionary<string, ApplicationAcceptanceTaskEvidence> LoadApplicationEvidence(string root, JsonElement plan, List<AcceptanceCheck> checks)
    {
        var result = new Dictionary<string, ApplicationAcceptanceTaskEvidence>(StringComparer.OrdinalIgnoreCase);
        var relative = OptionalString(plan, "applicationAcceptanceEvidence");
        if (string.IsNullOrWhiteSpace(relative)) return result;
        var path = SafeChild(root, relative, "Application-Abnahmeevidenz");
        if (!File.Exists(path)) return result;
        try
        {
            var evidence = ApplicationAcceptanceEvidenceService.LoadAndValidate(root, path);
            foreach (var task in evidence.Tasks)
            {
                var key = task.Aid.Replace("0x", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant() + ":" + task.TaskType;
                if (task.TaskType == "CheckFileSettings") key += ":" + task.FileId;
                result[key] = task;
            }
        }
        catch (Exception exception)
        {
            checks.Add(Check("Nicht prüfbar", "Application", "Application-Abnahmeevidenz", "Aktuelles korreliertes rfPrj-Ergebnis", "Abgewiesen", exception.Message, "Kein Istwert", path));
        }
        return result;
    }

    private static Dictionary<string, PayloadEvidence> ReadPayloadEvidence(JsonElement? last)
    {
        var result = new Dictionary<string, PayloadEvidence>(StringComparer.OrdinalIgnoreCase);
        if (last is null || !last.Value.TryGetProperty("payloads", out var payloads) || payloads.ValueKind != JsonValueKind.Array) return result;
        foreach (var payload in payloads.EnumerateArray())
        {
            var aid = RequiredString(payload, "aid").ToUpperInvariant();
            var fileId = RequiredInt(payload, "fileId");
            result[aid + ":" + fileId] = new PayloadEvidence(RequiredString(payload, "expectedHex"), OptionalString(payload, "verificationPath") ?? "");
        }
        return result;
    }

    private static DateTimeOffset? ParseTimestamp(JsonElement? last)
    {
        if (last is null || OptionalString(last.Value, "timestamp") is not string timestamp) return null;
        return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ? parsed : null;
    }

    private static byte[] ReadDataBytes(string path)
    {
        var raw = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(raw).Trim().Trim('\uFEFF');
        if (Regex.IsMatch(text, "^[0-9A-Fa-f\\s,;:\\-]*$"))
        {
            var clean = Regex.Replace(text, "[^0-9A-Fa-f]", "");
            if (clean.Length > 0 && clean.Length % 2 == 0) return Convert.FromHexString(clean);
        }
        return raw;
    }

    private static string NormalizeExpectedHex(string value)
    {
        var clean = Regex.Replace(value ?? "", "[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (clean.Length == 0 || clean.Length % 2 != 0) throw new InvalidDataException("Letzte_Kodierung.json enthält ungültiges expectedHex.");
        return clean;
    }

    private static string BuildText(AcceptanceReport report)
    {
        var lines = new List<string>
        {
            "SieEncode – Prüf- und Abnahmebericht", "=======================================", "",
            "Projekt: " + report.ProjectName, "Erstellt: " + report.GeneratedAt, "Gesamtergebnis: " + report.Result,
            $"OK: {report.Summary.Ok} | Abweichung: {report.Summary.Deviation} | Nicht prüfbar: {report.Summary.NotVerifiable}",
            $"Nachweisabdeckung: bytegenau {report.Coverage.ByteExact} | operativ {report.Coverage.Operational} | ohne Istwert {report.Coverage.WithoutActualValue}",
            "Schlüsselwerte: nicht enthalten", ""
        };
        lines.Add("Einschränkungen");
        lines.Add("---------------");
        lines.AddRange(report.Limitations.Select(value => "- " + value));
        lines.Add("");
        foreach (var row in report.Checks)
        {
            lines.Add($"[{row.Status}] {row.Area} – {row.Subject}");
            if (row.Expected.Length > 0) lines.Add("  Soll: " + row.Expected);
            if (row.Actual.Length > 0) lines.Add("  Ist:  " + row.Actual);
            if (row.Detail.Length > 0) lines.Add("  Hinweis: " + row.Detail);
            lines.Add("  Nachweisart: " + row.EvidenceLevel);
            if (row.Evidence.Length > 0) lines.Add("  Nachweis: " + row.Evidence);
            lines.Add("");
        }
        lines.AddRange(["Bewertung", "---------", "OK bedeutet: Der Punkt wurde mit dem aktuell verfügbaren sicheren Nachweis bestätigt.", "Abweichung bedeutet: Ein gelesener Ist-Wert widerspricht dem Soll.", "Nicht prüfbar bedeutet: Das Soll ist dokumentiert, aber der aktuelle RFiDGear-Abnahmepfad liefert dafür noch keinen verifizierten Ist-Wert."]);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static AcceptanceCheck Check(string status, string area, string subject, string expected, string actual, string detail, string evidenceLevel, string evidence = "")
        => new(status, area, subject, expected, actual, detail, evidenceLevel, evidence);

    private static void AtomicWrite(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) { writer.Write(content); writer.Flush(); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string SafeChild(string root, string relative, string label)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException($"{label} muss relativ zum Projektordner sein: {relative}");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        EnsureWithin(root, path, label);
        return path;
    }

    private static void EnsureWithin(string root, string path, string label)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{label} verlässt den Projektordner: {path}");
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : throw new InvalidDataException("JSON-Feld fehlt: " + name);
    private static IEnumerable<JsonElement> RequiredArray(JsonElement element, string name)
    { var value = RequiredProperty(element, name); return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : throw new InvalidDataException("JSON-Feld ist kein Array: " + name); }
    private static string RequiredString(JsonElement element, string name)
        => OptionalString(element, name) is { Length: > 0 } value ? value : throw new InvalidDataException("JSON-Textfeld fehlt: " + name);
    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int RequiredInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : throw new InvalidDataException("JSON-Zahlenfeld fehlt: " + name);

    private sealed record PayloadEvidence(string ExpectedHex, string VerificationPath);
}

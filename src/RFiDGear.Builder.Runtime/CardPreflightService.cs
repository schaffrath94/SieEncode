using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.Runtime;

public enum CardPreflightMode { Transport, Productive }

public sealed record CardPreflightFileResult(
    string Aid, int FileId, string Name, int Size, string Output, bool Exists,
    int? ActualLength, bool Readable, string PayloadState, string HexValue, string AsciiValue);

public sealed record CardPreflightProbeResult(
    string Mode, bool Completed, string CompletedAt,
    [property: JsonPropertyName("rfidgearOutcome")] string RFiDGearOutcome,
    int ExpectedCount, int ReadableCount, int MissingCount, int WrongSizeCount,
    int PayloadComparedCount, int PayloadMatchCount, int PayloadMismatchCount,
    IReadOnlyList<CardPreflightFileResult> Files);

public sealed record CardPreflightClassification(string Code, string Level, string Title, string Detail);
public sealed record CardPreflightRecommendation(
    string Code, string Level, string Title, string Detail, string WritePolicy,
    bool MasteringRecommended, bool CodingRecommended);

public sealed record CardPreflightReport(
    string SchemaVersion, string ProjectName, string EvaluatedAt, bool ReadOnly,
    string ExpectedPayloadPreparedAt, CardPreflightClassification Classification,
    CardPreflightRecommendation Recommendation, CardPreflightProbeResult Transport,
    CardPreflightProbeResult Productive, IReadOnlyList<string> Limitations);

public sealed record CardPreflightReportFiles(CardPreflightReport Report, string JsonPath, string TextPath);

public sealed class CardPreflightService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public void Prepare(string projectDirectory, CardPreflightMode mode)
    {
        using var plan = LoadPlan(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        var probeDirectory = ProbeDirectory(root, plan.RootElement, mode);
        Directory.CreateDirectory(probeDirectory);
        foreach (var file in RequiredArray(plan.RootElement, "files"))
        {
            var property = mode == CardPreflightMode.Productive ? "productiveOutput" : "transportOutput";
            var output = RequiredString(file, property);
            var path = SafeChild(root, output, "Preflight-Ausgabe");
            if (File.Exists(path)) File.Delete(path);
        }
        foreach (var name in new[] { "_probe.completed.json", "rfidgear.log.sanitized.txt" })
        {
            var path = Path.Combine(probeDirectory, name);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public CardPreflightReportFiles CompleteAndEvaluate(
        string projectDirectory, CardPreflightMode mode, RFiDGearOperationEvaluation operation,
        DateTimeOffset? completedAt = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var plan = LoadPlan(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        var probeDirectory = ProbeDirectory(root, plan.RootElement, mode);
        Directory.CreateDirectory(probeDirectory);
        var timestamp = completedAt ?? DateTimeOffset.Now;
        var marker = new
        {
            completedAt = timestamp.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            outcome = operation.State == RFiDGearOperationState.Successful ? "RFiDGearSuccess" : "RFiDGearFailure",
            state = operation.State.ToString()
        };
        AtomicWrite(Path.Combine(probeDirectory, "_probe.completed.json"), JsonSerializer.Serialize(marker, JsonOptions));
        AtomicWrite(Path.Combine(probeDirectory, "rfidgear.log.sanitized.txt"), operation.SanitizedLog);
        return Evaluate(projectDirectory, timestamp);
    }

    public CardPreflightReportFiles Evaluate(string projectDirectory, DateTimeOffset? evaluatedAt = null)
    {
        var root = Path.GetFullPath(projectDirectory);
        using var plan = LoadPlan(root);
        var transport = ProbeSummary(root, plan.RootElement, CardPreflightMode.Transport);
        var productive = ProbeSummary(root, plan.RootElement, CardPreflightMode.Productive);
        var classification = Classify(plan.RootElement, transport, productive);
        var recommendation = Recommend(classification);
        var expectedPayloadPreparedAt = ReadExpectedPayloadTimestamp(root);
        var report = new CardPreflightReport(
            "1.0", OptionalString(plan.RootElement, "projectName") ?? "", (evaluatedAt ?? DateTimeOffset.Now).ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            true, expectedPayloadPreparedAt, classification, recommendation, transport, productive,
            [
                "Diese Phase prüft erwartete Files ausschließlich über nicht-destruktive ReadData-Proben.",
                "Keine sichere Aussage über UID, vollständige AID-Liste, FileSettings oder fremde/ungekannte Applications.",
                "UNBEKANNT_KEIN_ZUGRIFF bedeutet nicht automatisch, dass die Karte leer ist."
            ]);
        var outputDirectory = Path.Combine(root, "card_preflight");
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "card-preflight.result.json");
        var textPath = Path.Combine(outputDirectory, "card-preflight.result.txt");
        AtomicWrite(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        AtomicWrite(textPath, ToText(report));
        return new(report, jsonPath, textPath);
    }

    private static CardPreflightProbeResult ProbeSummary(string root, JsonElement plan, CardPreflightMode mode)
    {
        var productive = mode == CardPreflightMode.Productive;
        var expectedHashes = productive ? ReadExpectedPayloadHashes(root) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var outputProperty = productive ? "productiveOutput" : "transportOutput";
        var markerPath = Path.Combine(ProbeDirectory(root, plan, mode), "_probe.completed.json");
        var completed = File.Exists(markerPath);
        var completedAt = "";
        var outcome = "";
        if (completed)
        {
            try
            {
                using var marker = JsonDocument.Parse(File.ReadAllText(markerPath));
                completedAt = OptionalString(marker.RootElement, "completedAt") ?? "";
                outcome = OptionalString(marker.RootElement, "outcome") ?? "";
            }
            catch (JsonException) { completed = false; }
        }
        var rows = new List<CardPreflightFileResult>();
        var readable = 0; var missing = 0; var wrongSize = 0;
        var compared = 0; var matches = 0; var mismatches = 0;
        foreach (var file in RequiredArray(plan, "files"))
        {
            var relative = RequiredString(file, outputProperty);
            var actualPath = SafeChild(root, relative, "Preflight-Ausgabe");
            var exists = File.Exists(actualPath);
            int? actualLength = null;
            var size = RequiredInt(file, "size");
            var sizeOk = false;
            var payloadState = "not-applicable";
            var hexValue = "";
            var asciiValue = "";
            if (exists)
            {
                try
                {
                    var actual = ReadBytes(actualPath);
                    actualLength = actual.Length;
                    var preview = actual.AsSpan(0, Math.Min(actual.Length, 256));
                    var suffix = actual.Length > preview.Length ? "…" : "";
                    hexValue = Convert.ToHexString(preview) + suffix;
                    asciiValue = new string(preview.ToArray().Select(value => value is >= 32 and <= 126 ? (char)value : '.').ToArray()) + suffix;
                    sizeOk = actual.Length == size;
                    if (sizeOk) readable++; else wrongSize++;
                    var payload = OptionalString(file, "codingPayload");
                    if (productive && !string.IsNullOrWhiteSpace(payload))
                    {
                        compared++;
                        var expectedPath = SafeChild(root, payload, "Coding-Payload");
                        if (File.Exists(expectedPath))
                        {
                            var expected = ReadBytes(expectedPath);
                            if (sizeOk && actual.AsSpan().SequenceEqual(expected)) { payloadState = "match"; matches++; }
                            else { payloadState = "mismatch"; mismatches++; }
                        }
                        else if (expectedHashes.TryGetValue(NormalizePayloadKey(payload), out var expectedHash))
                        {
                            var actualHash = Convert.ToHexString(SHA256.HashData(actual));
                            if (sizeOk && string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)) { payloadState = "match"; matches++; }
                            else { payloadState = "mismatch"; mismatches++; }
                        }
                        else { payloadState = "expected-missing"; mismatches++; }
                    }
                }
                catch (IOException) { wrongSize++; }
                catch (UnauthorizedAccessException) { wrongSize++; }
            }
            else missing++;
            rows.Add(new(RequiredString(file, "aid"), RequiredInt(file, "fileId"), OptionalString(file, "name") ?? "", size,
                relative, exists, actualLength, exists && sizeOk, payloadState, hexValue, asciiValue));
        }
        return new(mode.ToString(), completed, completedAt, outcome, rows.Count, readable, missing, wrongSize,
            compared, matches, mismatches, rows);
    }

    private static CardPreflightClassification Classify(JsonElement plan, CardPreflightProbeResult transport, CardPreflightProbeResult productive)
    {
        var total = RequiredArray(plan, "files").Count();
        if (!transport.Completed && !productive.Completed)
            return Classification("NICHT_AUSGEFUEHRT", "info", "Noch kein Karten-Preflight ausgeführt", "Transport- und Produktiv-Probe wurden noch nicht abgeschlossen.");
        var transportSuccessful = transport.Completed && transport.RFiDGearOutcome == "RFiDGearSuccess";
        var productiveSuccessful = productive.Completed && productive.RFiDGearOutcome == "RFiDGearSuccess";
        var transportReadable = transportSuccessful ? transport.ReadableCount : 0;
        var productiveReadable = productiveSuccessful ? productive.ReadableCount : 0;
        var allTransport = transportSuccessful && total > 0 && transportReadable == total;
        var allProductive = productiveSuccessful && total > 0 && productiveReadable == total;
        var payloadOk = productive.PayloadMismatchCount == 0;
        var anyReadable = transportReadable + productiveReadable > 0;
        var equivalent = OptionalBoolean(plan, "credentialSetsEquivalent");
        var runtimeFailure = transport.RFiDGearOutcome == "RFiDGearFailure" || productive.RFiDGearOutcome == "RFiDGearFailure";
        if (allProductive && !payloadOk)
            return Classification("PRODUKTIV_DATEN_ABWEICHUNG", "error", "Produktiv lesbar, aber Nutzdaten weichen ab", "Alle erwarteten Files sind mit Produktivkeys lesbar, mindestens ein kodiertes File stimmt jedoch nicht mit dem aktuellen SOLL-Payload überein.");
        if (allProductive && payloadOk)
        {
            if (allTransport)
                return Classification("LESBAR_MIT_BEIDEN_SCHLUESSELSETS", "warning", "Mit beiden Schlüsselsätzen vollständig lesbar",
                    equivalent ? "Die im Projekt hinterlegten Transport- und Produktivschlüssel sind nicht ausreichend unterscheidbar. Der Kartenstatus kann über die Read-Proben allein nicht eindeutig getrennt werden; vor Schreiboperationen prüfen."
                               : "Obwohl die hinterlegten Transport- und Produktivschlüssel unterschiedlich sind, waren alle erwarteten Files mit beiden Sätzen lesbar. Der Kartenstatus kann über die Read-Proben allein nicht eindeutig getrennt werden; vor Schreiboperationen prüfen.");
            if (transportReadable > 0)
                return Classification("PRODUKTIV_OK_MIT_TRANSPORT_RESTZUGRIFF", "warning", "Produktiver Zustand plausibel, Transportzugriff teilweise möglich", "Alle erwarteten Files sind produktiv lesbar und die kodierten Payloads stimmen. Zusätzlich waren einzelne Files noch mit Transportkeys lesbar; das sollte vor weiteren Schreiboperationen geprüft werden.");
            return Classification("PRODUKTIV_OK", "success", "Produktiver Zustand plausibel", "Alle erwarteten Files sind mit Produktivkeys lesbar; vorhandene kodierte Payloads stimmen mit dem aktuellen SOLL überein.");
        }
        if (allTransport)
        {
            if (productiveReadable > 0)
                return Classification("TRANSPORT_OK_MIT_PRODUKTIV_TEILZUGRIFF", "warning", "Transportzustand plausibel, Produktivzugriff teilweise möglich", "Alle erwarteten Files sind mit Transportkeys lesbar, einzelne zusätzlich mit Produktivkeys. Das kann auf gleiche Teilkeys oder einen Mischzustand hinweisen und sollte vor Mastering/Kodierung geklärt werden.");
            return Classification("TRANSPORT_MASTERING_OK", "success", "Mastering-/Transportzustand plausibel", "Alle erwarteten Files sind mit Transportkeys lesbar. Der Transponder wirkt vollständig gemastert, aber noch nicht eindeutig als produktiv bestätigt.");
        }
        if (runtimeFailure)
            return Classification("PREFLIGHT_LAUFZEITFEHLER", "error", "RFiDGear-Fehler während der Kartenprobe", "Mindestens eine read-only Probe endete mit einem strukturierten RFiDGear-Laufzeitfehler und keine erfolgreich abgeschlossene Probe belegt einen eindeutigen Zustand. Ausgabedateien eines fehlgeschlagenen Laufs gelten nicht als Lesbarkeitsnachweis.");
        if (transport.Completed && productive.Completed && !anyReadable)
            return Classification("UNBEKANNT_KEIN_ZUGRIFF", "warning", "Mit bekannten Schlüsseln kein erwartetes File lesbar", "Das kann ein leerer Transponder, ein fremder Transponder oder ein Transponder mit unbekannten Schlüsseln sein. Ohne AID-/UID-Inventur ist keine sichere Unterscheidung möglich.");
        if (anyReadable)
            return Classification("TEILWEISE_INKONSISTENT", "error", "Teilweiser oder inkonsistenter Kartenstatus", "Nur ein Teil der erwarteten Files ist mit den getesteten Schlüsselsätzen lesbar. Vor Schreiboperationen sollte dieser Zustand geklärt werden.");
        return Classification("NOCH_UNVOLLSTAENDIG", "info", "Karten-Preflight noch unvollständig", "Mindestens eine Probe wurde ausgeführt, für eine belastbare Klassifikation fehlt aber noch der zweite Schlüsselsatz oder ein lesbares Ergebnis.");
    }

    private static CardPreflightRecommendation Recommend(CardPreflightClassification classification) => classification.Code switch
    {
        "TRANSPORT_MASTERING_OK" => new("READY_FOR_CODING", "success", "Bereit für Kodierung – Mastering nicht wiederholen", "Der erwartete Mastering-/Transportzustand ist vollständig lesbar. Wenn diese Karte absichtlich zu diesem Projekt gehört, als nächsten Schritt die Kodierung verwenden; kein erneutes Mastering starten.", "CODING_ALLOWED_AFTER_OPERATOR_CHECK", false, true),
        "PRODUKTIV_OK" => new("ALREADY_PRODUCTIVE_MATCH", "success", "Keine Schreiboperation erforderlich", "Der Transponder ist mit Produktivkeys vollständig lesbar und die geprüften Nutzdaten entsprechen dem aktuellen SOLL. Weder Mastering noch erneute Kodierung wird empfohlen.", "NO_WRITE_NEEDED", false, false),
        "NICHT_AUSGEFUEHRT" => new("RUN_BOTH_PROBES", "info", "Zuerst Karten-Preflight ausführen", "Transport- und – sofern verfügbar – Produktivprobe auf demselben Transponder ausführen. Bis dahin keine Schreibentscheidung aus dem Preflight ableiten.", "BLOCK_UNTIL_PREFLIGHT", false, false),
        "NOCH_UNVOLLSTAENDIG" => new("RUN_MISSING_PROBE", "info", "Fehlende Probe nachholen", "Die Zustandsklassifikation ist noch unvollständig. Fehlende Probe auf demselben Transponder ausführen; bis dahin keine Schreiboperation aus dem Preflight ableiten.", "BLOCK_UNTIL_PREFLIGHT", false, false),
        _ => new("DO_NOT_WRITE_REVIEW_REQUIRED", classification.Level == "error" ? "error" : "warning", "Nicht schreiben – Kartenstatus zuerst klären", "Der Kartenstatus ist nicht eindeutig sicher für Mastering oder Kodierung. Probe-Ergebnis, RFiDGear-Log und bei Bedarf Diagnosepaket prüfen; keine automatische Reparatur oder Schreiboperation starten.", "BLOCK_WRITE", false, false)
    };

    private static string ToText(CardPreflightReport report) => string.Join(Environment.NewLine,
        $"RFiDGear Karten-Preflight – {report.ProjectName}",
        $"Auswertung: {report.EvaluatedAt}",
        "Modus: ausschließlich lesend (ReadData)", "",
        $"ERGEBNIS: {report.Classification.Title}", $"Code: {report.Classification.Code}", report.Classification.Detail, "",
        $"EMPFEHLUNG: {report.Recommendation.Title}", $"Policy: {report.Recommendation.WritePolicy}", report.Recommendation.Detail, "",
        $"Transport-Probe: abgeschlossen={report.Transport.Completed} · lesbar={report.Transport.ReadableCount}/{report.Transport.ExpectedCount} · falsche Größe={report.Transport.WrongSizeCount}",
        $"Produktiv-Probe: abgeschlossen={report.Productive.Completed} · lesbar={report.Productive.ReadableCount}/{report.Productive.ExpectedCount} · Payload-Matches={report.Productive.PayloadMatchCount}/{report.Productive.PayloadComparedCount} · Abweichungen={report.Productive.PayloadMismatchCount}", "",
        "Gelesene Inhalte (maximal 256 Byte je File):",
        string.Join(Environment.NewLine, report.Transport.Files.Concat(report.Productive.Files).Where(file => file.Readable).Select(file =>
            $" - {file.Aid} / File {file.FileId} / {file.Name}: HEX {file.HexValue} · ASCII {file.AsciiValue}")), "",
        "Grenzen:", " - keine sichere UID-/AID-Inventur", " - keine automatische FileSettings-/AccessRights-Inventur", " - keine Schreib-, Create-, Delete- oder ChangeKey-Tasks");

    private static JsonDocument LoadPlan(string projectDirectory)
    {
        var root = Path.GetFullPath(projectDirectory);
        var path = Path.Combine(root, "card-preflight.plan.json");
        if (!File.Exists(path)) throw new FileNotFoundException("card-preflight.plan.json fehlt.", path);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ProbeDirectory(string root, JsonElement plan, CardPreflightMode mode)
    {
        var property = mode == CardPreflightMode.Productive ? "productiveDirectory" : "transportDirectory";
        return SafeChild(root, RequiredString(plan, property), "Preflight-Ordner");
    }

    private static string ReadExpectedPayloadTimestamp(string root)
    {
        var path = Path.Combine(root, "card_preflight", "expected-card.json");
        if (!File.Exists(path)) return "";
        try { using var document = JsonDocument.Parse(File.ReadAllText(path)); return OptionalString(document.RootElement, "preparedAt") ?? ""; }
        catch (JsonException) { return ""; }
    }

    private static IReadOnlyDictionary<string, string> ReadExpectedPayloadHashes(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(root, "card_preflight", "expected-card.json");
        if (!File.Exists(path)) return result;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("payloads", out var payloads) && payloads.ValueKind == JsonValueKind.Array)
            {
                foreach (var payload in payloads.EnumerateArray())
                {
                    var output = OptionalString(payload, "output");
                    var sha256 = OptionalString(payload, "sha256");
                    if (!string.IsNullOrWhiteSpace(output) && sha256?.Length == 64) result[NormalizePayloadKey(output)] = sha256;
                }
            }
            if (result.Count == 0 && document.RootElement.TryGetProperty("values", out var valuesNode) && valuesNode.ValueKind == JsonValueKind.Object)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in valuesNode.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.String) values[property.Name] = property.Value.GetString() ?? "";
                var definition = new CodingSessionService().Load(root);
                foreach (var payload in PayloadEngine.BuildAll(definition.Profiles, definition.ProjectValues, values))
                    result[NormalizePayloadKey(payload.Output)] = Convert.ToHexString(SHA256.HashData(payload.Bytes));
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (InvalidDataException) { }
        return result;
    }

    private static string NormalizePayloadKey(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? path : parts[^1];
    }

    private static byte[] ReadBytes(string path)
    {
        var raw = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(raw).Trim().Trim('\uFEFF');
        if (Regex.IsMatch(text, @"^[0-9A-Fa-f\s,;:\-]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var clean = Regex.Replace(text, "[^0-9A-Fa-f]", "", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (clean.Length > 0 && clean.Length % 2 == 0) return Convert.FromHexString(clean);
        }
        return raw;
    }

    private static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            for (var attempt = 1; ; attempt++)
            {
                try { File.Move(temporary, path, true); break; }
                catch (Exception exception) when (attempt < 5 && exception is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(attempt * 20);
                }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string SafeChild(string root, string relative, string label)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException($"{label} muss relativ zum Projektordner sein.");
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(prefix, relative));
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{label} verlässt den Projektordner.");
        return path;
    }

    private static CardPreflightClassification Classification(string code, string level, string title, string detail) => new(code, level, title, detail);
    private static IEnumerable<JsonElement> RequiredArray(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : throw new InvalidDataException($"JSON-Array fehlt: {name}");
    private static string RequiredString(JsonElement element, string name)
        => OptionalString(element, name) is { Length: > 0 } value ? value : throw new InvalidDataException($"JSON-Textfeld fehlt: {name}");
    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int RequiredInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : throw new InvalidDataException($"JSON-Zahlenfeld fehlt: {name}");
    private static bool OptionalBoolean(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
}

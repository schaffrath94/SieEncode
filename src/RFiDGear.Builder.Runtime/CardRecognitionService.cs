using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record CardRecognitionReport(
    string State, string Structure, string PiccState, IReadOnlyList<string> ApplicationIds,
    IReadOnlyList<string> ExpectedApplicationIds, IReadOnlyList<string> MatchingCredentials,
    string Summary, string Recommendation, bool ReadOnly, string CreatedAt)
{
    public string CardMode { get; init; } = CardOperationMode.FullCard;
    public int? PiccKeySettings { get; init; }
    public int? PiccFreeMemoryBytes { get; init; }
    public bool? CreateDeleteWithoutMasterKey { get; init; }
    public IReadOnlyList<string> ExtensionTargetAids { get; init; } = [];
    public bool ExtensionEligible { get; init; }
}

public sealed record CardRecognitionFiles(CardRecognitionReport Report, string TextPath, string JsonPath);

public sealed class CardRecognitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CardRecognitionFiles Create(string projectDirectory, RFiDGearOperationArtifact operation,
        RFiDGearOperationEvaluation evaluation, DateTimeOffset? now = null)
    {
        if (operation.Kind != RFiDGearOperationKind.CardRecognitionReadOnly)
            throw new InvalidOperationException("Erkennungsbericht benötigt die strikt lesende Erkennungsoperation.");

        var root = Path.GetFullPath(projectDirectory);
        var plan = ReadPlan(root);
        var expected = ReadExpectedApplicationIds(root);
        var outcomes = evaluation.StructuredRun?.TaskOutcomes ?? [];
        var inventory = outcomes.FirstOrDefault(item => item.Type == "PICCApplicationInventory");
        var inventoryValid = evaluation.State == RFiDGearOperationState.Successful &&
            inventory is { Outcome: "Successful", ErrorCode: "NoError" };
        var observed = inventoryValid
            ? inventory!.PiccApplicationIds.Select(NormalizeAid).Distinct(StringComparer.Ordinal).Order().ToArray()
            : [];
        var cardMode = plan.CardMode;
        var extensionTargets = plan.ExtensionTargetAids;
        var settings = inventoryValid ? inventory!.PiccKeySettings : null;
        var createDeleteWithoutMasterKey = inventoryValid ? inventory!.PiccCreateDeleteWithoutMasterKey : null;
        var targetAlreadyPresent = observed.Intersect(extensionTargets, StringComparer.Ordinal).Order().ToArray();
        var extensionEligible = cardMode == CardOperationMode.ExtendExisting && inventoryValid &&
            createDeleteWithoutMasterKey == true && extensionTargets.Count > 0 && targetAlreadyPresent.Length == 0;

        var matches = new List<string>();
        foreach (var task in operation.Tasks.Where(task => task.Type == "PICCAuthCheck"))
        {
            var outcome = outcomes.FirstOrDefault(item => item.Position == task.Position);
            if (outcome?.PiccAuthMatched == true)
                matches.Add(CredentialName(task.Description));
        }

        var structure = !inventoryValid ? "UNBEKANNT" : ClassifyStructure(observed, expected);
        var picc = !inventoryValid && outcomes.Count == 0 ? "UNBEKANNT" : ClassifyPicc(matches);
        var state = !inventoryValid ? "UNKNOWN" : "RECOGNIZED";
        var summary = !inventoryValid
            ? "Der Transponder konnte nicht vollständig und autoritativ inventarisiert werden. Er wird nicht als leer eingestuft."
            : structure == "LEER"
                ? $"Die vollständige AID-Inventur war erfolgreich und enthält 0 Applications. PICC-Schlüsselstatus: {picc}."
                : $"Die vollständige AID-Inventur enthält {observed.Length} Application(s): {string.Join(", ", observed)}. PICC-Schlüsselstatus: {picc}.";
        var recommendation = cardMode == CardOperationMode.ExtendExisting
            ? !inventoryValid
                ? "Erweiterung gesperrt: Die vollständige AID-Inventur ist nicht autoritativ gelungen."
                : extensionTargets.Count == 0
                    ? "Erweiterung gesperrt: Das Projekt enthält keine neue Application gegenüber dem Grundprofil."
                    : targetAlreadyPresent.Length > 0
                        ? $"Erweiterung gesperrt: Ziel-AID(s) bereits vorhanden: {string.Join(", ", targetAlreadyPresent)}. Bestehende Applications werden nicht überschrieben."
                        : createDeleteWithoutMasterKey != true
                            ? "Erweiterung gesperrt: Die PICC-Einstellung erlaubt das Anlegen neuer Applications ohne bekannten PICC-Masterkey nicht oder konnte nicht sicher gelesen werden."
                            : "Sichere Erweiterung möglich: Bestehende Applications bleiben erhalten; ausschließlich die fehlenden Ziel-AIDs dürfen hinzugefügt werden."
            : structure switch
        {
            "LEER" when picc.Contains("STANDARD", StringComparison.Ordinal) => "Leerer Transponder mit passendem Standardzugang erkannt; Mastering kann bewusst gestartet werden.",
            "LEER" => "Struktur ist leer, der PICC-Schlüsselstatus ist aber nicht eindeutig Standard. Vor einem Schreiblauf Schlüsselzustand klären.",
            "ERWARTET_VOLLSTAENDIG" => "Die erwarteten Projekt-AIDs sind vollständig vorhanden. Für Details anschließend Produktivprüfung/Abnahme verwenden.",
            "ERWARTET_TEILWEISE" or "GEMISCHT" => "Nicht überschreiben. Teil- oder Fremdbelegung zuerst fachlich prüfen.",
            "FREMD" => "Nicht überschreiben. Der Transponder enthält ausschließlich nicht zum Projekt gehörende Applications.",
            "BELEGT" => "Projektlose Analyse abgeschlossen. Die aufgeführten Applications werden nur inventarisiert und nicht bewertet.",
            _ => "Keine Schreibaktion freigeben; Reader, Karte und Diagnosebericht prüfen."
        };

        var report = new CardRecognitionReport(state, structure, picc, observed, expected, matches,
            summary, recommendation, true, (now ?? DateTimeOffset.Now).ToString("O"))
        {
            CardMode = cardMode,
            PiccKeySettings = settings,
            PiccFreeMemoryBytes = inventoryValid ? inventory!.PiccFreeMemoryBytes : null,
            CreateDeleteWithoutMasterKey = createDeleteWithoutMasterKey,
            ExtensionTargetAids = extensionTargets,
            ExtensionEligible = extensionEligible
        };
        var directory = Path.Combine(root, "recognition");
        Directory.CreateDirectory(directory);
        var jsonPath = Path.Combine(directory, "card-recognition.result.json");
        var textPath = Path.Combine(directory, "card-recognition.result.txt");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(textPath, Render(report));
        return new(report, textPath, jsonPath);
    }

    private sealed record RecognitionPlan(string CardMode, IReadOnlyList<string> ExtensionTargetAids);

    private static RecognitionPlan ReadPlan(string root)
    {
        var path = Path.Combine(root, "card-recognition.plan.json");
        if (!File.Exists(path)) return new(CardOperationMode.FullCard, []);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var mode = document.RootElement.TryGetProperty("cardMode", out var modeValue) ? modeValue.GetString() ?? CardOperationMode.FullCard : CardOperationMode.FullCard;
        var targets = document.RootElement.TryGetProperty("extensionTargetAids", out var aids) && aids.ValueKind == JsonValueKind.Array
            ? aids.EnumerateArray().Select(item => NormalizeAid(item.ToString())).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray()
            : [];
        return new(mode, targets);
    }

    private static string[] ReadExpectedApplicationIds(string root)
    {
        var path = Path.Combine(root, "project.definition.json");
        if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("applications", out var applications) || applications.ValueKind != JsonValueKind.Array) return [];
        return applications.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var id) ? NormalizeAid(id.ToString()) : "")
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray();
    }

    private static string ClassifyStructure(IReadOnlyCollection<string> observed, IReadOnlyCollection<string> expected)
    {
        if (observed.Count == 0) return "LEER";
        var observedSet = observed.ToHashSet(StringComparer.Ordinal);
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        if (expectedSet.Count == 0) return "BELEGT";
        if (expectedSet.Count > 0 && observedSet.SetEquals(expectedSet)) return "ERWARTET_VOLLSTAENDIG";
        var expectedPresent = observedSet.Count(expectedSet.Contains);
        if (expectedPresent == 0) return "FREMD";
        if (observedSet.All(expectedSet.Contains)) return "ERWARTET_TEILWEISE";
        return "GEMISCHT";
    }

    private static string ClassifyPicc(IReadOnlyList<string> matches) => matches.Count switch
    {
        0 => "UNBEKANNT",
        1 => matches[0],
        _ => "MEHRDEUTIG (" + string.Join(" + ", matches) + ")"
    };

    private static string CredentialName(string description)
    {
        foreach (var value in new[] { "STANDARD_DES", "STANDARD_AES", "TRANSPORT", "PRODUCTIVE" })
            if (description.Contains(value, StringComparison.Ordinal)) return value;
        return "UNBENANNT";
    }

    private static string NormalizeAid(string value)
    {
        var clean = value.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id.ToString("X6", System.Globalization.CultureInfo.InvariantCulture)
            : "";
    }

    private static string Render(CardRecognitionReport report) =>
        "SieEncode Transponder-Erkennung (nur lesend)\r\n" +
        $"Zeit: {report.CreatedAt}\r\nStatus: {report.State}\r\nStruktur: {report.Structure}\r\n" +
        $"PICC-Schlüsselstatus: {report.PiccState}\r\nApplications: {(report.ApplicationIds.Count == 0 ? "keine" : string.Join(", ", report.ApplicationIds))}\r\n" +
        $"Erwartet: {(report.ExpectedApplicationIds.Count == 0 ? "keine Projekt-AIDs" : string.Join(", ", report.ExpectedApplicationIds))}\r\n\r\n" +
        $"PICC-KeySettings: {(report.PiccKeySettings is int settings ? settings.ToString("X2") : "nicht sicher gelesen")}\r\n" +
        $"Freier PICC-Speicher: {(report.PiccFreeMemoryBytes is int freeMemory ? freeMemory + " Byte" : "nicht sicher gelesen")}\r\n" +
        (report.PiccKeySettings is int flags
            ? $"  Masterkey änderbar: {YesNo((flags & 1) != 0)}\r\n  Applications ohne Masterkey auflisten: {YesNo((flags & 2) != 0)}\r\n  Applications ohne Masterkey erstellen/löschen: {YesNo((flags & 4) != 0)}\r\n  Konfiguration änderbar: {YesNo((flags & 8) != 0)}\r\n"
            : "") +
        $"Create/Delete ohne PICC-Masterkey: {(report.CreateDeleteWithoutMasterKey is bool allowed ? allowed ? "ja" : "nein" : "unbekannt")}\r\n" +
        (report.CardMode == CardOperationMode.ExtendExisting
            ? $"Erweiterungsziel: {(report.ExtensionTargetAids.Count == 0 ? "keine" : string.Join(", ", report.ExtensionTargetAids))}\r\nFreigabe: {(report.ExtensionEligible ? "JA" : "NEIN")}\r\n"
            : "") + "\r\n" +
        report.Summary + "\r\nEmpfehlung: " + report.Recommendation + "\r\n\r\n" +
        "Sicherheitsregel: Nur eine erfolgreiche vollständige Inventur mit 0 Applications gilt als leer. Lesefehler oder fehlgeschlagene Schlüsselproben reichen dafür nicht aus.\r\n";

    private static string YesNo(bool value) => value ? "ja" : "nein";
}

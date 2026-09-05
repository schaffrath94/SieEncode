using System.Text;
using System.Text.Json;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record HardwareAcceptancePlanRequest(
    string BuilderVersion,
    string ProjectName,
    string ProjectDirectory,
    string SourcePath,
    bool ArtifactOnly,
    string CodingProjectPath,
    ProjectPreflightReport Preflight,
    RFiDGearOperationCatalog Operations,
    RFiDGearCapabilityReport Capabilities,
    RFiDGearReaderSettingsInfo ReaderSettings);

public sealed record HardwareReadinessItem(string Status, string Area, string Requirement, string Evidence);
public sealed record HardwareAcceptanceStep(
    int Number,
    string Phase,
    string Action,
    string Risk,
    string ExpectedEvidence,
    bool RequiresFreshTestChipApproval,
    bool Optional);

public sealed record HardwareAcceptancePlan(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string BuilderVersion,
    string ProjectName,
    string ProjectDirectory,
    string Status,
    IReadOnlyList<HardwareReadinessItem> Readiness,
    IReadOnlyList<HardwareAcceptanceStep> Steps,
    IReadOnlyList<string> EvidenceGaps,
    string SafetyContract);

public sealed record HardwareAcceptancePlanFiles(HardwareAcceptancePlan Plan, string JsonPath, string TextPath);

public sealed class HardwareAcceptancePlanService
{
    public HardwareAcceptancePlan Inspect(HardwareAcceptancePlanRequest request, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var readiness = new List<HardwareReadinessItem>();
        Add(readiness, request.ArtifactOnly ? "Blockiert" : "OK", "Projekt", "Bearbeitbare Projektdefinition",
            request.ArtifactOnly ? "Nur Bauartefakt; vollständiger Stale-/Profilnachweis fehlt." : request.SourcePath);
        Add(readiness, request.Preflight.Status == "OK" ? "OK" : "Blockiert", "Projekt", "Projekt-Preflight",
            request.Preflight.Status == "OK" ? $"{request.Preflight.Projects.Count} rfPrj-Projekte ohne Befund." : $"{request.Preflight.Issues.Count} Befund(e)." );
        Add(readiness, File.Exists(request.CodingProjectPath) ? "OK" : "Blockiert", "Projekt", "Kodierprojekt",
            string.IsNullOrWhiteSpace(request.CodingProjectPath) ? "Nicht angegeben." : request.CodingProjectPath);
        RequireOperation(readiness, request.Operations, RFiDGearOperationKind.MasteringAutorun, "Mastering-Einmallauf");
        RequireOperation(readiness, request.Operations, RFiDGearOperationKind.CardPreflightTransport, "Read-only Transportprobe");
        RequireOperation(readiness, request.Operations, RFiDGearOperationKind.CardPreflightProductive, "Read-only Produktivprobe");
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.CustomProjectFile, true);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.AutorunOnce, true);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.ContinuousAutoMode, false);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.StructuredTaskLogging, false);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.AuthoritativeTaskLoopTerminalStatus, false);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.ExactAppSettingsCheck, false);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.AppKeyCountCheck, false);
        RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.FileSettingsCheck, false);
        if (request.Preflight.Projects.SelectMany(project => project.TaskTypes).Any(item =>
                (item.Key is "ApplicationKeyChangeover" or "ApplicationKeySettingsChangeover" or "PICCMasterKeyChangeover" or "PICCMasterKeySettingsChangeover") && item.Value > 0))
            RequireCapability(readiness, request.Capabilities, RFiDGearCapabilityId.VerifiedApplicationKeyChange, true);
        Add(readiness, request.ReaderSettings.IsConfigured ? "OK" : "Blockiert", "RFiDGear", "Reader-Provider ausgewählt",
            request.ReaderSettings.IsConfigured ? $"{request.ReaderSettings.Provider} · {request.ReaderSettings.Port} · {request.ReaderSettings.ReaderName}" : request.ReaderSettings.Error ?? "Nicht konfiguriert.");

        var blocked = readiness.Count(item => item.Status == "Blockiert");
        var status = blocked == 0 ? "Softwareseitig bereit – frische Testchipfreigabe erforderlich" : $"Nicht bereit – {blocked} Blocker";
        var steps = BuildSteps();
        return new HardwareAcceptancePlan(
            "1.0", now ?? DateTimeOffset.Now, request.BuilderVersion, request.ProjectName, Path.GetFullPath(request.ProjectDirectory), status,
            readiness, steps,
            [
                request.Capabilities.Has(RFiDGearCapabilityId.VerifiedApplicationKeyChange)
                    ? "RFiDGear weist die Nachverifikation geänderter Application-Keys binär nach."
                    : "RFiDGear #178 ist mit 2.0.10/ELATEC reproduziert: positiver Keywechsel-Taskstatus bei unverändertem Transport-Key; Kodierung bleibt gesperrt.",
                request.Capabilities.Has(RFiDGearCapabilityId.ExactAppSettingsCheck) && request.Capabilities.Has(RFiDGearCapabilityId.AppKeyCountCheck)
                    ? "RFiDGear stellt exakten ReadAppSettings- und CheckAppKeyCount-Nachweis bereit; der Builder integriert beide als getrennten read-only Lauf mit Ergebnis- und rfPrj-Hash-Korrelation."
                    : "Application-KeySettings und Keyanzahl benötigen die ab RFiDGear 2.0.8 bereitgestellten Prüfaufgaben (#201).",
                request.Capabilities.Has(RFiDGearCapabilityId.FileSettingsCheck)
                    ? "RFiDGear stellt den hardwarebewiesenen CheckFileSettings-Task bereit; der Builder erzeugt dafür je Datei eine strikt lesende Exact-Match-Abnahmeaufgabe."
                    : "CheckFileSettings ist im installierten RFiDGear 2.0.10 nicht als rfPrj-Tasktyp ladbar; Filetyp, Communication Mode und alle Access Rights bleiben daher nicht automatisiert bitgenau belegbar.",
                request.Capabilities.Has(RFiDGearCapabilityId.AuthoritativeTaskLoopTerminalStatus)
                    ? "RFiDGear leitet TaskLoop.Success/Failure autoritativ aus Taskoutcomes, Bedingungszweigen und ErrorRouting ab."
                    : "RFiDGear #207 ist nicht binär behoben; der Builder wertet deshalb weiterhin jedes korrelierte Task.Outcome zusätzlich fail-closed aus."
            ],
            "Dieser Plan startet nichts. Jede Reader-/Kartenaktion benötigt eine neue ausdrückliche Testchipfreigabe. Recovery/Formatieren ist optional, destruktiv und benötigt eine zusätzliche ausdrückliche Freigabe. Produktivmedien und Produktivkeys sind ausgeschlossen.");
    }

    public HardwareAcceptancePlanFiles CreateReport(HardwareAcceptancePlanRequest request, string outputDirectory, DateTimeOffset? now = null)
    {
        var plan = Inspect(request, now);
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        var stamp = plan.GeneratedAt.ToString("yyyyMMdd-HHmmss");
        var jsonPath = Path.Combine(directory, $"Hardware-Abnahmeplan-{stamp}.json");
        var textPath = Path.Combine(directory, $"Hardware-Abnahmeplan-{stamp}.txt");
        WriteAtomic(jsonPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        WriteAtomic(textPath, RenderText(plan));
        return new HardwareAcceptancePlanFiles(plan, jsonPath, textPath);
    }

    private static IReadOnlyList<HardwareAcceptanceStep> BuildSteps() =>
    [
        new(1, "Vorbereitung", "Projekt-Preflight und Bereitschaftsbericht prüfen; Blocker zuerst beheben.", "Offline / read-only", "Preflight OK; keine Keywerte im Bericht.", false, false),
        new(2, "Ausgangslage", "Transport- und Produktiv-Kartenprobe nacheinander ausführen; Karte dabei nicht wechseln.", "Karte / read-only", "Plausibler, verstandener Ausgangszustand; unbekannt gilt nie als leer.", true, false),
        new(3, "Mastering", "Neutralen Testtransponder genau einmal mastern und terminalen RFiDGear-Status abwarten.", "Karte / schreibend", "Alle Create/Zero-Fill/Readbacks erfolgreich; kein stiller Teilerfolg.", true, false),
        new(4, "Mastering-Nachweis", "Transportprobe erneut ausführen.", "Karte / read-only", "Alle erwarteten Files in korrekter Länge mit Transportkeys lesbar.", true, false),
        new(5, "Kodierung", "Testwerte bereitstellen und den vollständigen Kodierlauf ausführen.", "Karte / schreibend", "Write/Read, Keywechsel, PICC-Wechsel und finale Readbacks terminal erfolgreich.", true, false),
        new(6, "Abnahme", "Finale Readbacks und Prüfbericht auswerten.", "Karte / read-only", "Aktuelle Nutzdaten bytegenau OK; unbelegbare Settings bleiben Nicht prüfbar.", true, false),
        new(7, "Serie", "Mindestens fünf unterschiedliche Testnummern in Einzel-, Fortlaufend- und optional Listenmodus verarbeiten.", "Karten / schreibend", "Erfolg je Datensatz; keine alte UID/Payload; klare nächste Nummer.", true, false),
        new(8, "Fehlerfall", "Nur bei ungefährlichem Read-only-Task einen kontrollierten Fehler erzeugen und denselben Datensatz wiederholen.", "Karte / read-only Fehlerprobe", "Kein stiller Erfolg, keine automatische Wiederholung einer Schreibaufgabe, Diagnosepaket vorhanden.", true, false),
        new(9, "Serienfehler", "Fehlernummer bewusst überspringen und einen früher erfolgreichen Wert erneut eingeben.", "Bedienung", "Übersprungen zählt nicht als Erfolg; persistente Duplikatwarnung erscheint.", true, false),
        new(10, "Recovery optional", "Transport- und Produktiv-Recovery nur als getrennten Test auf dafür freigegebenem Wegwerfmedium ausführen.", "DESTRUKTIV / FormatPICC", "Formatierung, bedingte PICC-Rücksetzung und anschließender Preflight nachvollziehbar.", true, true)
    ];

    private static void RequireOperation(List<HardwareReadinessItem> items, RFiDGearOperationCatalog catalog, RFiDGearOperationKind kind, string label)
    {
        var operation = catalog.Find(kind);
        Add(items, operation is null ? "Blockiert" : "OK", "Projektartefakt", label, operation?.ProjectPath ?? "Fehlt.");
    }

    private static void RequireCapability(List<HardwareReadinessItem> items, RFiDGearCapabilityReport report, RFiDGearCapabilityId id, bool required)
    {
        var capability = report.Capabilities.First(item => item.Id == id);
        var status = capability.State == RFiDGearCapabilityState.Available ? "OK" : required ? "Blockiert" : "Warnung";
        Add(items, status, "RFiDGear-Fähigkeit", capability.Name, capability.Evidence);
    }

    private static void Add(List<HardwareReadinessItem> items, string status, string area, string requirement, string evidence)
        => items.Add(new(status, area, requirement, evidence));

    private static string RenderText(HardwareAcceptancePlan plan)
    {
        var lines = new List<string>
        {
            "SieEncode – Hardware-Abnahmeplan", $"Erzeugt: {plan.GeneratedAt:O}", $"Studio: {plan.BuilderVersion}",
            $"Projekt: {plan.ProjectName}", $"Projektordner: {plan.ProjectDirectory}", $"Status: {plan.Status}", "", plan.SafetyContract, "", "BEREITSCHAFT"
        };
        foreach (var item in plan.Readiness) lines.Add($"[{item.Status}] {item.Area} · {item.Requirement}\r\n  {item.Evidence}");
        lines.Add(""); lines.Add("SCHRITTE");
        foreach (var step in plan.Steps) lines.Add($"{step.Number}. {step.Phase} · {step.Risk}{(step.Optional ? " · OPTIONAL" : "")}\r\n  {step.Action}\r\n  Erwarteter Nachweis: {step.ExpectedEvidence}\r\n  Frische Testchipfreigabe: {(step.RequiresFreshTestChipApproval ? "JA" : "NEIN")}");
        lines.Add(""); lines.Add("BEWUSSTE EVIDENZGRENZEN");
        lines.AddRange(plan.EvidenceGaps.Select(gap => "- " + gap));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

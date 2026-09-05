using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        if (args.Length == 0) return Usage();
        switch (args[0].ToLowerInvariant())
        {
            case "validate-project" when args.Length == 2:
            {
                var project = ProjectJson.Load(args[1]);
                var issues = ProjectValidator.Validate(project);
                foreach (var issue in issues) Console.Error.WriteLine($"{issue.Code}: {issue.Message}");
                if (issues.Count > 0) return 2;
                Console.WriteLine("Projektdefinition ist gültig.");
                return 0;
            }
            case "sanitize-project" when args.Length == 3:
                ProjectJson.Save(args[2], ProjectJson.WithoutAllSecrets(ProjectJson.Load(args[1])));
                return 0;
            case "generate-key" when args.Length == 2:
                Console.WriteLine(KeyVaultService.GenerateKey(args[1]));
                return 0;
            case "inspect-rfprj" when args.Length == 2:
            {
                var project = RfPrjArchive.Load(args[1]);
                Console.WriteLine(JsonSerializer.Serialize(new { manifestVersion = project.ManifestVersion, tasks = project.Summarize() }, ProjectJson.Options));
                return 0;
            }
            case "migrate-acceptance-project" when args.Length is 2 or 3:
            {
                var source = Path.GetFullPath(args[1]);
                var target = Path.GetFullPath(args.Length == 3 ? args[2] : args[1]);
                var project = RfPrjArchive.Load(source);
                var removed = ApplicationAcceptanceCompatibilityService.RemoveUnsupportedFileSettingsTasks(project);
                RfPrjArchive.Save(target, project);
                Console.WriteLine($"Abnahmeprojekt kompatibilisiert: {removed} nicht ladbare CheckFileSettings-Task(s) entfernt; {project.Tasks.Count} ausführbare Task(s).");
                return 0;
            }
            case "build-support-projects" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var shells = TaskShellLibrary.Load(args[2]);
                var project = ProjectJson.Load(args[3]);
                var composed = library.Compose(project.Profiles.Base, project.Profiles.Customers);
                var outputDirectory = Path.GetFullPath(args[4]);
                Directory.CreateDirectory(outputDirectory);
                var factory = new SupportProjectFactory(shells);
                var name = project.Project.Name;
                RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_CardRecognition_ReadOnly.rfPrj"),
                    factory.BuildCardRecognition(project, project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase)));
                RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_CardPreflight_Transport.rfPrj"), factory.BuildCardPreflight(composed, project, outputDirectory, productive: false));
                if (!CardOperationMode.IsExtension(project))
                    RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_Recovery_Format_TransportPICC.rfPrj"), factory.BuildRecovery(project, productive: false));
                else
                    RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_Erweiterung_Rueckbau.rfPrj"), factory.BuildExtensionRollback(composed, project));
                if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase))
                {
                    RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_CardPreflight_Productive.rfPrj"), factory.BuildCardPreflight(composed, project, outputDirectory, productive: true));
                    RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_Acceptance_Application_Productive.rfPrj"), factory.BuildApplicationAcceptance(composed, project));
                    if (!CardOperationMode.IsExtension(project))
                        RfPrjArchive.Save(Path.Combine(outputDirectory, name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj"), factory.BuildRecovery(project, productive: true));
                }
                return 0;
            }
            case "build-main-projects" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var shells = TaskShellLibrary.Load(args[2]);
                var project = ProjectJson.Load(args[3]);
                var composed = library.Compose(project.Profiles.Base, project.Profiles.Customers);
                var outputDirectory = Path.GetFullPath(args[4]);
                Directory.CreateDirectory(outputDirectory);
                Directory.CreateDirectory(Path.Combine(outputDirectory, "payloads"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "verification"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "readback"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "readback_mastering"));
                MasteringInitializationService.Prepare(composed, outputDirectory);
                var factory = new MainProjectFactory(shells);
                if (project.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase))
                    RfPrjArchive.Save(Path.Combine(outputDirectory, project.Project.Name + "_Mastering.rfPrj"), factory.BuildMastering(composed, project, outputDirectory));
                if (project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase))
                    RfPrjArchive.Save(Path.Combine(outputDirectory, project.Project.Name + "_Kodierung.rfPrj"), factory.BuildCoding(composed, project, outputDirectory));
                return 0;
            }
            case "build-contract-artifacts" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var project = ProjectJson.Load(args[2]);
                var composed = library.Compose(project.Profiles.Base, project.Profiles.Customers);
                var context = new ContractArtifactContext(project, composed, Path.GetFullPath(args[2]), args[3], DateTime.Now);
                ContractArtifactFactory.SaveAll(args[4], ContractArtifactFactory.Build(context));
                return 0;
            }
            case "preflight-output" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var project = ProjectJson.Load(args[2]);
                var report = ProjectPreflight.Inspect(project, library, args[2], args[3]);
                await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
                Console.WriteLine($"Preflight: {report.Status}, {report.Issues.Count} Befund(e), {report.Projects.Count} rfPrj geprüft.");
                return report.Issues.Count == 0 ? 0 : 2;
            }
            case "preflight-operational" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var project = ProjectJson.Load(args[2]);
                var report = ProjectPreflight.Inspect(project, library, args[2], args[3], ProjectPreflightScope.Operational);
                await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
                Console.WriteLine($"Betriebsprüfung: {report.Status}, {report.Issues.Count} Befund(e), {report.Projects.Count} rfPrj geprüft.");
                return report.Issues.Count == 0 ? 0 : 2;
            }
            case "preflight-artifacts" when args.Length == 3:
            {
                var report = ProjectPreflight.InspectArtifacts(args[1]);
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
                Console.WriteLine($"Artefaktprüfung: {report.Status}, {report.Issues.Count} Befund(e), {report.Projects.Count} rfPrj geprüft.");
                return report.Issues.Count == 0 ? 0 : 2;
            }
            case "export-task-map" when args.Length == 3:
            {
                var map = TaskMapExporter.InspectDirectory(args[1]);
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(map, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
                Console.WriteLine($"Schlüsselfreie Taskübersicht: {map.Projects.Count} Projekt(e).");
                return 0;
            }
            case "export-coding-specification" when args.Length == 3:
            {
                var path = new CodingSpecificationService().ExportFile(args[1], args[2]);
                Console.WriteLine("Kodiervorschrift erzeugt: " + path);
                return 0;
            }
            case "export-card-report-pdf" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var shells = TaskShellLibrary.Load(args[2]);
                var project = ProjectJson.Load(args[3]);
                var analysis = new ProjectAnalysisService().Analyze(project, library, shells);
                var report = new CardReportFactory().FromProject(project, analysis, Path.GetFullPath(args[3]), library);
                new CardReportPdfService().Save(report, args[4]);
                Console.WriteLine("Schlüsselwertfreier PDF-Kartenreport erzeugt: " + Path.GetFullPath(args[4]));
                return analysis.Errors == 0 ? 0 : 2;
            }
            case "inspect-rfidgear-version" when args.Length == 2:
            {
                var version = RFiDGearLauncher.ReadVersion(args[1]);
                Console.WriteLine(JsonSerializer.Serialize(version, RuntimeJsonOptions()));
                return version.Version is null ? 2 : 0;
            }
            case "inspect-rfidgear-capabilities" when args.Length == 2:
            {
                var report = new RFiDGearCapabilityInspector().Inspect(args[1]);
                Console.WriteLine(JsonSerializer.Serialize(report, RuntimeJsonOptions()));
                return string.IsNullOrWhiteSpace(report.Error) ? 0 : 2;
            }
            case "snapshot-rfidgear-log" when args.Length is 2 or 3:
            {
                var snapshot = new RFiDGearLogMonitor().Snapshot(args.Length == 3 ? args[2] : null);
                await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(snapshot, RuntimeJsonOptions()));
                Console.WriteLine($"RFiDGear-Logsnapshot: {snapshot.Files.Count} Datei(en).");
                return 0;
            }
            case "diagnose-rfidgear-log" when args.Length == 3:
            {
                var snapshot = JsonSerializer.Deserialize<RFiDGearLogSnapshot>(await File.ReadAllTextAsync(args[1]), RuntimeJsonOptions())
                    ?? throw new InvalidDataException("RFiDGear-Logsnapshot ist leer.");
                var delta = new RFiDGearLogMonitor().ReadDelta(snapshot);
                var signal = RFiDGearDiagnosticParser.Parse(delta.Text);
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new { delta.Paths, delta.HasContent, Signal = signal }, RuntimeJsonOptions()));
                Console.WriteLine(signal.HasSignal ? signal.Summary : "Kein RFiDGear-Fehlersignal im neuen Logabschnitt.");
                return signal.StrongFailure ? 2 : 0;
            }
            case "compare-rfprj" when args.Length == 3:
            {
                var comparison = RfPrjSemanticComparer.Compare(RfPrjArchive.Load(args[1]), RfPrjArchive.Load(args[2]));
                if (!comparison.Equivalent)
                {
                    Console.Error.WriteLine($"rfPrj weicht ab: Taskposition {comparison.TaskPosition}, Feld {comparison.DifferingElement}.");
                    return 3;
                }
                Console.WriteLine("rfPrj ist semantisch identisch.");
                return 0;
            }
            case "acceptance-report" when args.Length is 2 or 3:
            {
                var result = new AcceptanceReportService().Create(args[1], outputDirectory: args.Length == 3 ? args[2] : null);
                Console.WriteLine($"Prüfbericht: {result.Report.Result} · OK {result.Report.Summary.Ok} · Abweichung {result.Report.Summary.Deviation} · Nicht prüfbar {result.Report.Summary.NotVerifiable}");
                Console.WriteLine(result.TextPath);
                Console.WriteLine(result.JsonPath);
                return result.Report.Summary.Deviation > 0 ? 2 : 0;
            }
            case "card-preflight-evaluate" when args.Length == 2:
            {
                var result = new CardPreflightService().Evaluate(args[1]);
                Console.WriteLine($"Karten-Preflight: {result.Report.Classification.Code} · {result.Report.Classification.Title}");
                Console.WriteLine(result.TextPath);
                Console.WriteLine(result.JsonPath);
                return result.Report.Classification.Level == "error" ? 2 : 0;
            }
            case "test-compatibility-matrix" when args.Length == 3:
            {
                var shells = TaskShellLibrary.Load(args[1]);
                var result = new CompatibilityMatrixService().Run(shells, args[2], "cli-compatibility-matrix");
                Console.WriteLine($"Kompatibilitätsmatrix: {(result.Report.Successful ? "BESTANDEN" : "FEHLER")} · {result.Report.Scenarios.Count(item => item.Status == "OK")}/{result.Report.Scenarios.Count} Szenarien");
                Console.WriteLine(result.TextPath);
                Console.WriteLine(result.JsonPath);
                return result.Report.Successful ? 0 : 2;
            }
            case "build-payloads" when args.Length == 5:
            {
                var library = ProfileLibrary.Load(args[1]);
                var project = ProjectJson.Load(args[2]);
                var cardValues = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(args[3]), ProjectJson.Options)
                    ?? throw new InvalidDataException("Kartendaten sind leer.");
                var composed = library.Compose(project.Profiles.Base, project.Profiles.Customers);
                var artifacts = PayloadEngine.BuildAll(composed, project.Values, cardValues);
                var outputRoot = Path.GetFullPath(args[4]);
                Directory.CreateDirectory(outputRoot);
                foreach (var artifact in artifacts)
                {
                    var path = Path.GetFullPath(Path.Combine(outputRoot, artifact.Output));
                    if (!path.StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Payload-Ausgabepfad verlässt den Zielordner: {artifact.Output}");
                    await File.WriteAllTextAsync(path, PayloadEngine.ToHexFileText(artifact.Bytes));
                    Console.WriteLine($"{artifact.Aid}:{artifact.FileId} {artifact.Output} {artifact.Bytes.Length} Byte");
                }
                return 0;
            }
            case "vault-protect" when args.Length == 3:
            {
                var passphrase = ReadPassphrase();
                var vault = KeyVaultService.Protect(await File.ReadAllTextAsync(args[1]), passphrase);
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(vault, ProjectJson.Options));
                return 0;
            }
            case "vault-unprotect" when args.Length == 3:
            {
                var passphrase = ReadPassphrase();
                var vault = JsonSerializer.Deserialize<KeyVaultDocument>(await File.ReadAllTextAsync(args[1]), ProjectJson.Options)
                    ?? throw new InvalidDataException("Key-Vault ist leer.");
                await File.WriteAllTextAsync(args[2], KeyVaultService.Unprotect(vault, passphrase));
                return 0;
            }
            case "audit-project-vault" when args.Length == 5:
            {
                var passphrase = ReadPassphrase();
                var project = ProjectJson.Load(args[1]);
                var library = ProfileLibrary.Load(args[2]);
                var audit = ProjectKeyVault.Audit(args[3], passphrase, project, library);
                await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(audit, ProjectJson.Options));
                Console.WriteLine($"Vault-Prüfung: {audit.Status} · portabel={audit.PortableAcrossUsers} · {audit.ApplicationCount} Applications · {audit.KeyCount} Keys · Fingerprint {audit.FileFingerprint}");
                return audit.Status == "OK" ? 0 : 2;
            }
            default:
                return Usage();
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static string ReadPassphrase()
{
    var passphrase = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(passphrase)) throw new InvalidOperationException("Passphrase über Standardeingabe fehlt.");
    return passphrase;
}

static int Usage()
{
    Console.Error.WriteLine("RFiDGear.Builder.Cli: validate-project | sanitize-project | generate-key | build-payloads | inspect-rfprj | migrate-acceptance-project | build-support-projects | build-main-projects | build-contract-artifacts | preflight-output | preflight-operational | preflight-artifacts | export-task-map | export-coding-specification | export-card-report-pdf | inspect-rfidgear-version | inspect-rfidgear-capabilities | snapshot-rfidgear-log | diagnose-rfidgear-log | compare-rfprj | acceptance-report | card-preflight-evaluate | test-compatibility-matrix | vault-protect | vault-unprotect | audit-project-vault");
    return 64;
}

static JsonSerializerOptions RuntimeJsonOptions() => new()
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

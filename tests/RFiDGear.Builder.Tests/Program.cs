using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text.Json;
using System.Xml.Linq;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

const string ContractPassphrase = "Cross-Runtime-Portable-Test";
if (args.Length == 3 && args[0] == "vault-unprotect-contract")
{
    var document = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(args[1])) ?? throw new InvalidDataException("Vault test document is empty.");
    File.WriteAllText(args[2], KeyVaultService.Unprotect(document, ContractPassphrase));
    return 0;
}
if (args.Length == 3 && args[0] == "vault-protect-contract")
{
    var document = KeyVaultService.ProtectLegacyV1(File.ReadAllText(args[1]), ContractPassphrase);
    File.WriteAllText(args[2], JsonSerializer.Serialize(document));
    return 0;
}
if (args.Length == 2 && args[0] == "hardware-extension-fixture")
{
    BuildHardwareExtensionFixture(args[1]);
    return 0;
}
if (args.Length == 2 && args[0] == "hardware-extension-upgrade-rollback")
{
    UpgradeHardwareExtensionRollback(args[1]);
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("Workspace navigation retains context and history", TestWorkspaceNavigation),
    ("Complete expert file rights and recipe snapshot", TestFileExpertDescription),
    ("Inventory evidence distinguishes missing, denied and partial reads", TestInventoryEvidence),
    ("Observed inventory PDF preserves evidence and payload", TestInventoryPdf),
    ("Payload comparison requires complete unambiguous reads", TestInventoryComparison),
    ("Project comparison uses full scoped production payloads", TestProjectInventoryComparison),
    ("File settings comparison respects metadata evidence and final state", TestInventoryFileSettings),
    ("Key generation", TestKeyGeneration),
    ("Vault roundtrip and tamper rejection", TestVault),
    ("Certificate recipient vault and recovery", TestCertificateVault),
    ("Project vault payload and path compatibility", TestProjectVault),
    ("Verified independent vault recovery backup", TestVaultRecoveryBackup),
    ("Project validation and secret sanitizing", TestProject),
    ("Key-free multipage PDF card report", TestCardReportPdf),
    ("Full target report preserves long payloads and fields", TestFullTargetReport),
    ("Project draft profile synchronization", TestProjectDraft),
    ("Managed multi-base profile library", TestManagedBaseProfiles),
    ("Pinned profile identity and version history", TestProfileBindingAndHistory),
    ("Project paths", TestPaths),
    ("Persistent settings and project catalog", TestSettingsAndCatalog),
    ("Safe project-definition deletion", TestProjectDeletion),
    ("Safe legacy and built-project migration", TestLegacyMigration),
    ("Atomic JSON persistence", TestPersistence),
    ("Concurrent project and vault change protection", TestConcurrentPersistence),
    ("Cross-process workspace lease and recovery", TestWorkspaceLease),
    ("Profile validation and composition conflicts", TestProfileComposition),
    ("Persistent profile editing and reference safety", TestProfileEditing),
    ("Key-free coding specification and profile duplication", TestCodingSpecificationAndDuplication),
    ("Key-free semantic project and profile diff", TestProfileDiff),
    ("Understandable two-stage file settings", TestFileSettingsExplanation),
    ("Payload recipe operations", TestPayloadRecipes),
    ("Synthetic encoding compatibility matrix", TestSyntheticCompatibilityMatrix),
    ("rfPrj dependency graph validation", TestDependencyGraph),
    ("Key-free rfPrj profile import safety", TestRfPrjProfileImportSafety),
    ("Evidence-based RFiDGear capability detection", TestRFiDGearCapabilities),
    ("RFiDGear launcher contract", TestLauncher),
    ("Process-bound in-memory project secret channel", TestProjectSecretPipe),
    ("Versioned key-free RFiDGear host request/result contract", TestHostContractV1),
    ("Fail-closed one-step coding-session start", TestCodingQuickStart),
    ("RFiDGear reader settings inspection", TestReaderSettingsInspection),
    ("RFiDGear log monitoring and diagnosis", TestRuntimeDiagnostics),
    ("RFiDGear operation contracts and terminal log status", TestOperations),
    ("Key-free authoritative application acceptance evidence", TestApplicationAcceptanceEvidence),
    ("Fail-closed RFiDGear run and shutdown safety", TestRunSafety),
    ("Project-local rfPrj I/O paths and controlled process completion", TestIoPathsAndProcessCompletion),
    ("Read-only card preflight classification and reports", TestCardPreflight),
    ("Read-only card recognition and fail-closed empty classification", TestCardRecognition),
    ("Projectless teststation safety and application inventory reports", TestTestStationSafety),
    ("Safe additive coding of existing cards", TestExistingCardExtension),
    ("Coding run completion evidence", TestCodingRunEvaluation),
    ("Coding series queue, history and duplicate protection", TestCodingSeries),
    ("Key-free diagnostic bundle allowlist", TestDiagnosticBundle),
    ("Schema-based coding session", TestCodingSession),
    ("Acceptance report evidence model", TestAcceptanceReport),
    ("Read-only key-free environment report", TestEnvironmentCheck),
    ("Key-free hardware acceptance planning", TestHardwareAcceptancePlan)
};

var failures = 0;
var filter = Environment.GetEnvironmentVariable("SIEENCODE_TEST_FILTER");
var selectedTests = string.IsNullOrWhiteSpace(filter)
    ? tests
    : tests.Where(test => test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
var total = selectedTests.Length;
foreach (var test in selectedTests)
{
    try { test.Run(); Console.WriteLine($"OK: {test.Name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL: {test.Name}: {exception}"); }
}
if (args.Length == 3 && string.IsNullOrWhiteSpace(filter))
{
    total++;
    try { TestRealAssets(args[0], args[1], args[2]); Console.WriteLine("OK: v10.39 profile and task-shell compatibility"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL: v10.39 profile library compatibility: {exception.Message}"); }
}
Console.WriteLine($"{total - failures}/{total} Tests erfolgreich.");
return failures == 0 ? 0 : 1;

static void TestApplicationAcceptanceEvidence()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-app-acceptance-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var document = RfPrjDocument.Create();
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "10"), new XElement("SelectedTaskType", "ReadAppSettings"), new XElement("SelectedTaskDescription", "settings"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF482D0")));
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "20"), new XElement("SelectedTaskType", "CheckAppKeyCount"), new XElement("SelectedTaskDescription", "count"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF482D0")));
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "30"), new XElement("SelectedTaskType", "CheckFileSettings"), new XElement("SelectedTaskDescription", "file settings"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF482D0"), new XElement("FileNumberCurrent", "0")));
        var projectPath = Path.Combine(root, "Demo_Acceptance_Application_Productive.rfPrj");
        RfPrjArchive.Save(projectPath, document);
        var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.ApplicationAcceptanceProductive, "Abnahme", projectPath, true, false, true, document.Summarize());
        var log = """
          [INF] {"Stage":"Task.Outcome","RunId":"demo","TaskExecutionIndex":0,"Outcome":"Successful","ErrorCode":"NoError"}
          [INF] {"Stage":"Task.Outcome","RunId":"demo","TaskExecutionIndex":1,"Outcome":"Successful","ErrorCode":"NoError"}
          [INF] {"Stage":"Task.Outcome","RunId":"demo","TaskExecutionIndex":2,"Outcome":"Successful","ErrorCode":"NoError"}
          [INF] {"Stage":"TaskLoop.Success","RunId":"demo"}
          """;
        var evaluation = new RFiDGearOperationEvaluation(RFiDGearOperationState.Successful, "OK", RFiDGearDiagnosticParser.Parse(log), log);
        var service = new ApplicationAcceptanceEvidenceService();
        var completed = CompleteApplicationHost(root, operation, log, "successful");
        var evidence = service.Create(root, operation, completed.Run, completed.Result, new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        Assert(evidence.State == "successful" && evidence.SchemaVersion == "1.2" && evidence.RunIdHash.Length == 64 && evidence.Tasks.Count == 3 && evidence.Tasks.All(task => task.Outcome == "Successful") && evidence.Tasks.Single(task => task.TaskType == "CheckFileSettings").FileId == "0", "Vollständige autoritative Kartenstruktur-Abnahme wird nicht als Erfolg persistiert.");
        var evidencePath = Path.Combine(root, "acceptance", "application-settings.result.json");
        var text = File.ReadAllText(evidencePath);
        Assert(!text.Contains("00000000000000000000000000000000", StringComparison.Ordinal) && text.Contains("\"containsKeys\": false", StringComparison.Ordinal), "Application-Abnahmeevidenz verletzt den Keyausschluss.");
        Assert(ApplicationAcceptanceEvidenceService.LoadAndValidate(root, evidencePath).ProjectSha256 == evidence.ProjectSha256, "Application-Abnahmeevidenz kann nicht korreliert geladen werden.");
        var mixedRunLog = log.Replace("\"RunId\":\"demo\",\"TaskExecutionIndex\":1", "\"RunId\":\"other\",\"TaskExecutionIndex\":1", StringComparison.Ordinal);
        var mixedCompleted = CompleteApplicationHost(root, operation, mixedRunLog, "failed");
        var mixedRun = service.Create(root, operation, mixedCompleted.Run, mixedCompleted.Result);
        Assert(mixedRun.State == "failed", "Vermischte RFiDGear-Läufe dürfen keine erfolgreiche Application-Abnahmeevidenz erzeugen.");
        File.AppendAllText(projectPath, "tamper");
        AssertThrows<InvalidDataException>(() => ApplicationAcceptanceEvidenceService.LoadAndValidate(root, evidencePath));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static (RFiDGearHostRunV1 Run, RFiDGearHostResultV1 Result) CompleteApplicationHost(
    string root, RFiDGearOperationArtifact operation, string log, string acceptanceState)
{
    var version = new RFiDGearVersionInfo("RFiDGear.exe", "test", new Version(2, 0, 10), true);
    var capabilities = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", version, [], token => token is
        "CUSTOMPROJECTFILE" or "AUTORUN" or "ReadAppSettings" or "IsAppSettingsExactMatch" or "CheckAppKeyCount" or "CheckFileSettings" or "Task.Outcome" or "TaskLoop.Summary" or "RunId",
        verifiedFileSettingsReleaseApproved: true);
    var host = new RFiDGearHostContractServiceV1();
    var run = host.Begin(root, operation.ProjectPath, "Demo", RFiDGearHostModeV1.Autorun, operation.Tasks, capabilities,
        operation: "application-acceptance-productive");
    var launch = new RFiDGearLaunchResult(123, "RFiDGear.exe", operation.ProjectPath, true, false, "AUTORUN=1");
    var started = DateTimeOffset.Now.AddSeconds(-1);
    var session = new RFiDGearOperationSession(operation, launch, new RFiDGearLogSnapshot(started, root, new Dictionary<string, long>()), started, TimeSpan.FromMinutes(2));
    var evaluation = RFiDGearOperationService.EvaluateText(session, log, DateTimeOffset.Now);
    return (run, host.CompleteOperation(run, session, evaluation, acceptanceState));
}

static void TestWorkspaceNavigation()
{
    var state = new WorkspaceNavigation();
    state.Navigate("Editor");
    state.Navigate("Station");
    Assert(state.LastPageFor("Projects") == "Editor", "Editor subpage must survive workspace switch.");
    state.Navigate(state.LastPageFor("Projects"));
    Assert(state.CurrentPage == "Editor", "Return to last workspace page.");
    Assert(state.GoBack() && state.CurrentPage == "Station", "Back must follow actual navigation.");
    state.Navigate("Profiles");
    state.Navigate("Profiles");
    Assert(state.GoBack() && state.CurrentPage == "Station", "Selecting current page must not duplicate history.");
    state.Navigate("Acceptance");
    Assert(WorkspaceNavigation.WorkspaceFor(state.CurrentPage) == "TestStation", "Acceptance belongs to inspection.");
    AssertThrows<ArgumentException>(() => state.Navigate("Unknown"));
    Assert(state.CurrentPage == "Acceptance", "Invalid navigation must not change state.");
    foreach (var page in new[] { "Home", "Projects", "Editor", "Profiles", "Analysis", "Diff", "Station", "TestStation", "Acceptance", "Settings", "Help" })
    {
        state.Navigate(page);
        Assert(state.LastPageFor(WorkspaceNavigation.WorkspaceFor(page)) == page, "Every RC15 page must retain its workspace route.");
    }
}

static void TestInventoryFileSettings()
{
    var expected = new FileDefinition { Id = 1, Type = "StdDataFile", Size = 32, Communication = "CM_ENCRYPT",
        Access = new AccessDefinition { Read = 14, Write = 15, ReadWrite = 2, Change = 0 },
        CreationSettings = new FileSettingsDefinition { Communication = "CM_PLAIN" } };
    IReadOnlyList<CardReportSettingComparison> Compare(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new InventoryFileSettingsComparisonService().Compare(expected, doc.RootElement.EnumerateArray().ToArray());
    }
    const string matching = """
        [{"fileId":1,"metadataResult":"NoError","contentResult":"PermissionDenied","fileType":"StdDataFile","declaredSize":32,"communicationMode":"CM_ENCRYPT","access":{"read":14,"write":15,"readWrite":2,"change":0}}]
        """;
    Assert(Compare(matching).All(row => row.State == ReportComparisonState.Equal), "Readable metadata can match despite denied content; compare final settings, not temporary ones.");
    Assert(Compare(matching.Replace("NoError", "PermissionDenied")).All(row => row.State == ReportComparisonState.NotComparable), "Denied metadata cannot establish matches from stale values.");
    var changed = Compare(matching.Replace("CM_ENCRYPT", "CM_PLAIN").Replace("\"read\":14", "\"read\":15"));
    Assert(changed.Count(row => row.State == ReportComparisonState.Different) == 2 && changed.Single(row => row.Name == "Leserecht").Expected.Contains("frei"), "Communication and special rights differences must be independent.");
    Assert(Compare("[]").All(row => row.State == ReportComparisonState.NotComparable), "Missing metadata is not evidence of absent settings.");
    Assert(Compare("[{\"fileId\":1},{\"fileId\":1}]").All(row => row.State == ReportComparisonState.NotComparable), "Duplicate IDs must not pick an arbitrary result.");
    Assert(Compare(matching.Replace("CM_ENCRYPT", "CM_UNKNOWN")).Single(row => row.Name == "Kommunikation").State == ReportComparisonState.NotComparable, "Unknown enum is not a differing known mode.");
    Assert(Compare(matching.Replace("StdDataFile", "ValueFile")).Single(row => row.Name == "Dateigröße (Byte)").State == ReportComparisonState.NotComparable, "Value-file union size is not a byte-size observation.");
    Assert(Compare(matching.Replace("\"read\":14", "\"read\":99")).Single(row => row.Name == "Leserecht").State == ReportComparisonState.NotComparable, "Invalid rights are not trustworthy differences.");
}

static void TestProjectInventoryComparison()
{
    var file = new FileDefinition { Id = 1, Size = 512, Coding = new CodingDefinition
    {
        Output = "test.hex", Recipe = [new RecipeStep { Op = "fixedHex", Value = new string('A', 1022) + "FF" }]
    } };
    var profiles = new ComposedProfiles([], [new ApplicationDefinition { Id = "A1B2C3", Files = [file] }], []);
    using var inventory = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        readOnly = true, applicationId = "A1B2C3", listing = "NoError", fileCount = 1,
        files = new[] { new { fileId = 1, declaredSize = 512, contentResult = "NoError", hex = new string('A', 1022) + "00" } }
    }));
    var service = new ProjectInventoryComparisonService();
    var result = service.CreateReport(profiles, new Dictionary<string, string>(), new Dictionary<string, string>(), inventory.RootElement, "synthetic");
    Assert(result.Findings.Any(f => f.Severity == "ERROR" && f.Area.Contains("Inhaltsvergleich")), "Difference after byte 256 must not disappear.");
    Assert(result.Kind == "application-comparison" && result.Applications[0].Files[0].ExpectedHexDump == new string('A', 1022) + "FF", "Comparison needs its own kind and complete expected bytes.");
    var review = Environment.GetEnvironmentVariable("SIEENCODE_COMPARISON_PDF_REVIEW");
    var pdfPath = string.IsNullOrWhiteSpace(review) ? Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf") : Path.GetFullPath(review);
    try
    {
        new CardReportPdfService().Save(result, pdfPath);
        var content = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
        Assert(content.Contains("Soll-HEX") && content.Contains("Abweichung") && content.Contains("0.70 0.10 0.10 rg"), "Comparison PDF must expose expected bytes and emphasize difference.");
        var compactPath = Path.ChangeExtension(pdfPath, ".compact.pdf");
        try
        {
            new CardReportPdfService().Save(result, compactPath, CardReportDetail.Compact);
            var compact = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(compactPath));
            Assert(!compact.Contains("Soll-HEX") && !compact.Contains("AAFF") && compact.Contains("Abweichung") &&
                compact.Contains("Nicht vergleichbar") && compact.Contains("Dateieinstellungen") && compact.Contains("Rohdaten ausgelassen"),
                "Compact report must omit raw payloads explicitly, while retaining differences and unknown requirements.");
            Assert(new FileInfo(compactPath).Length < new FileInfo(pdfPath).Length, "Compact output should reduce payload volume.");
        }
        finally { if (string.IsNullOrWhiteSpace(review)) File.Delete(compactPath); }
    }
    finally { if (string.IsNullOrWhiteSpace(review)) File.Delete(pdfPath); }
    file.Coding.Recipe = [new RecipeStep { Op = "asciiDigits", Source = "serial", Width = 1 }, new RecipeStep { Op = "zeroPadToFileSize" }];
    profiles = profiles with { Variables = [new VariableDefinition { Id = "serial", Scope = "card", Type = "digits", Length = 1, Required = true }] };
    result = service.CreateReport(profiles, new Dictionary<string, string> { ["serial"] = "7" }, new Dictionary<string, string>(), inventory.RootElement, "synthetic");
    Assert(result.Findings.Any(f => f.Area.Contains("Inhaltsvergleich") && f.Severity == "WARN" && f.Message.Contains("Sollinhalt nicht erzeugbar")), "Project values must not silently substitute missing card values.");
    result = service.CreateReport(profiles, new Dictionary<string, string>(), new Dictionary<string, string> { ["serial"] = "7" }, inventory.RootElement, "synthetic");
    Assert(result.Findings.Any(f => f.Area.Contains("Inhaltsvergleich") && f.Severity == "ERROR"), "Explicit card value should permit comparison.");
}

static void TestInventoryComparison()
{
    var service = new InventoryPayloadComparisonService();
    var expected = new Dictionary<int, byte[]> { [1] = [0, 255] };
    InventoryPayloadComparison Compare(string files)
    {
        using var document = JsonDocument.Parse("{\"readOnly\":true,\"applicationId\":\"A1B2C3\",\"files\":" + files + "}");
        return service.Compare("a1b2c3", expected, document.RootElement).Single();
    }
    Assert(Compare("[{\"fileId\":1,\"contentResult\":\"NoError\",\"declaredSize\":2,\"hex\":\"00ff\"}]").State == PayloadComparisonState.Equal, "Equivalent full bytes should match.");
    Assert(Compare("[{\"fileId\":1,\"contentResult\":\"NoError\",\"declaredSize\":2,\"hex\":\"0000\"}]").State == PayloadComparisonState.Different, "Full observed byte difference must be reported.");
    foreach (var files in new[] {
        "[]", "[{\"fileId\":1},{\"fileId\":1}]",
        "[{\"fileId\":1,\"contentResult\":\"PermissionDenied\"}]",
        "[{\"fileId\":1,\"contentResult\":\"NoError\",\"declaredSize\":4,\"hex\":\"00FF\"}]",
        "[{\"fileId\":1,\"contentResult\":\"NoError\",\"declaredSize\":2,\"hex\":\"00FF\",\"truncated\":true}]" })
        Assert(Compare(files).State == PayloadComparisonState.NotComparable, "Missing, denied, duplicate or partial reads must never establish equality or difference.");
    using var wrong = JsonDocument.Parse("{\"readOnly\":true,\"applicationId\":\"FFFFFF\"}");
    AssertThrows<InvalidDataException>(() => service.Compare("A1B2C3", expected, wrong.RootElement));
}

static void TestInventoryPdf()
{
    var hex = new string('A', 1400) + "DEADBEEF";
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        readOnly = true, applicationId = "A1B2C3", authentication = "NoError", listing = "PermissionDenied",
        createdUtc = "2026-09-09T10:00:00Z", secret = "DO-NOT-EXPORT-CREDENTIAL",
        files = new object[] {
            new { fileId = 1, declaredSize = 8192, fileType = "StdDataFile", communicationMode = "CM_ENCRYPT", metadataResult = "NoError", contentResult = "NoError", truncated = true, hex, ascii = new string('Q', 704) },
            new { fileId = 2, contentResult = "PermissionDenied" } }
    }));
    var report = new InventoryCardReportFactory().Create(json.RootElement, "synthetische Inventur");
    Assert(report.Applications[0].KeyCount is null && report.Applications[0].Files[1].Size is null, "Unknown metadata must not become zero.");
    Assert(report.Applications[0].Files[0].HexDump == hex && report.Applications[0].Files[0].DataDefinition.Contains("Teilweise"), "Payload and partial evidence must survive mapping.");
    Assert(report.Findings.Any(f => f.Message.Contains("Dateiliste nicht vollständig")), "Denied listing must stay incomplete.");
    var output = Environment.GetEnvironmentVariable("SIEENCODE_INVENTORY_PDF_REVIEW");
    var path = string.IsNullOrWhiteSpace(output) ? Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf") : Path.GetFullPath(output);
    try
    {
        new CardReportPdfService().Save(report, path);
        var pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert(pdf.Contains("DEADBEEF") && !pdf.Contains("DO-NOT-EXPORT-CREDENTIAL"), "Observed PDF loses trailing bytes or exports unapproved fields.");
        Assert(System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type /Page\b").Count >= 2, "Long observed payload must paginate.");
    }
    finally { if (string.IsNullOrWhiteSpace(output)) File.Delete(path); }
}

static void TestInventoryEvidence()
{
    FileInventoryEvidence Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return FileInventoryEvidence.FromRuntime(document.RootElement);
    }
    foreach (var status in new[] { "PermissionDenied", "AuthFailure", "AuthenticationError" })
        Assert(Read(JsonSerializer.Serialize(new { contentResult = status })).State == InventoryReadState.NoAccess, "Denied reads are not empty or different.");
    foreach (var status in new[] { "NotRequested", "NotAttempted", "EmptyOrUnknownSize" })
        Assert(Read(JsonSerializer.Serialize(new { contentResult = status })).State == InventoryReadState.NotChecked, "Unperformed reads stay unverified.");
    Assert(Read("{\"contentResult\":\"DeferredFileType\"}").State == InventoryReadState.Unsupported, "Deferred file types must be explicit.");
    Assert(Read("{\"contentResult\":\"IOError\"}").State == InventoryReadState.Failed, "Read errors must not imply denied access.");
    Assert(Read("{}").State == InventoryReadState.Unknown, "Missing result is not success.");
    Assert(Read("{\"contentResult\":\"NoError\",\"declaredSize\":0}").State == InventoryReadState.Unknown, "NoError without bytes is not an empty read.");
    Assert(Read("{\"contentResult\":\"NoError\",\"declaredSize\":0,\"hex\":\"\"}").State == InventoryReadState.Complete, "Explicit successful empty read remains representable.");
    Assert(Read("{\"contentResult\":\"NoError\",\"declaredSize\":2,\"hex\":\"00FF\"}").State == InventoryReadState.Complete, "Exact bytes prove a complete read.");
    Assert(Read("{\"contentResult\":\"NoError\",\"declaredSize\":2,\"hex\":\"00\"}").State == InventoryReadState.Partial, "Short bytes cannot prove completeness.");
    foreach (var hex in new[] { "Z0", "0", "000000" })
        Assert(Read(JsonSerializer.Serialize(new { contentResult = "NoError", declaredSize = 2, hex })).State == InventoryReadState.Unknown, "Malformed or excessive bytes cannot prove completeness.");
    var limited = Read(JsonSerializer.Serialize(new { contentResult = "NoError", declaredSize = 8192, truncated = true, hex = new string('A', 8192) }));
    Assert(limited.State == InventoryReadState.Partial && limited.BytesRead == 4096 && limited.Description.Contains("8192"), "Runtime cap must remain visible.");
    Assert(Read("{\"contentResult\":\"NoError\",\"declaredSize\":1,\"hex\":\"00\",\"truncated\":true}").State == InventoryReadState.Partial, "Explicit truncation prevents a complete verdict.");
}

static void TestFileExpertDescription()
{
    var file = new FileDefinition {
        Access = new AccessDefinition { Read = 14, Write = 15, ReadWrite = 2, Change = 0 },
        CreationSettings = new FileSettingsDefinition { Communication = "CM_PLAIN", Access = new AccessDefinition() },
        Coding = new CodingDefinition { Recipe = [new RecipeStep { Op = "fixedHex", Value = new string('A', 600) },
            new RecipeStep { Op = "asciiDigits", Source = "serial", Width = 9, PadLeft = "0", PadRight = " " }] }
    };
    var detail = FileExpertDescription.FromFile(file);
    Assert(detail.FinalRights.Contains("frei (0xE)") && detail.FinalRights.Contains("gesperrt (0xF)") &&
        detail.FinalRights.Contains("Schlüssel 2") && detail.FinalRights.Contains("Rechte ändern: Schlüssel 0"), "All four rights need correct special values.");
    Assert(detail.CreationRights.Contains("CM_PLAIN") && !detail.CreationRights.Contains("gesperrt"), "Creation settings must remain distinct.");
    using var recipe = JsonDocument.Parse(detail.Recipe);
    Assert(recipe.RootElement[0].GetProperty("value").GetString()!.Length == 600, "Recipe literals must not truncate.");
    Assert(recipe.RootElement[1].GetProperty("source").GetString() == "serial" && recipe.RootElement[1].GetProperty("padRight").GetString() == " ", "All parameters and order preserved.");
    file.Coding.Recipe.Clear();
    using var snapshot = JsonDocument.Parse(detail.Recipe);
    Assert(snapshot.RootElement.GetArrayLength() == 2, "Details must be a snapshot.");
    Assert(FileExpertDescription.FromFile(new FileDefinition()).Recipe.Contains("Kein"), "Missing recipe explicitly described.");
}

static void TestKeyGeneration()
{
    var keys = Enumerable.Range(0, 32).Select(_ => KeyVaultService.GenerateKey("DF_KEY_AES")).ToArray();
    Assert(keys.Distinct(StringComparer.Ordinal).Count() == keys.Length, "Keys sind nicht eindeutig.");
    Assert(keys.All(key => key.Length == 32 && key.All(Uri.IsHexDigit)), "AES-Keyformat ist ungültig.");
    Assert(KeyVaultService.GenerateKey("DF_KEY_3K3DES").Length == 48, "3K3DES-Keylänge ist ungültig.");
}

static void TestProjectSecretPipe()
{
    const string reference = "secret://rfidgear/v1/artifact/demo/task/10/DesfireAppKeyCurrent";
    const string secret = "00112233445566778899AABBCCDDEEFF";
    using var server = ProjectSecretPipeServer.Start(new Dictionary<string, string> { [reference] = secret });
    server.AuthorizeClient(Environment.ProcessId);

    var response = Resolve(reference);
    Assert(response.GetProperty("Success").GetBoolean() && response.GetProperty("Secret").GetString() == secret,
        "Prozessgebundener Secret-Kanal löst die freigegebene Referenz nicht auf.");
    var rejected = Resolve("secret://rfidgear/v1/artifact/demo/task/10/unknown");
    Assert(!rejected.GetProperty("Success").GetBoolean() && rejected.GetProperty("Secret").ValueKind == JsonValueKind.Null,
        "Secret-Kanal gibt eine nicht freigegebene Referenz zurück.");
    var commandLine = RFiDGearLauncher.BuildArguments(Path.Combine(Path.GetTempPath(), "demo.rfPrj"), false, false, server.Descriptor);
    Assert(commandLine.Contains("PROJECTSECRETPIPE=SieEncode.Secrets.v1.", StringComparison.Ordinal) &&
           commandLine.Contains("PROJECTSECRETSERVERPID=", StringComparison.Ordinal) &&
           !commandLine.Contains(secret, StringComparison.Ordinal),
        "RFiDGear-Startparameter enthalten nicht nur schlüsselfreie Sitzungsmetadaten.");

    JsonElement Resolve(string requestedReference)
    {
        using var pipe = new NamedPipeClientStream(".", server.Descriptor.PipeName, PipeDirection.InOut);
        pipe.Connect(5000);
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        writer.WriteLine(JsonSerializer.Serialize(new { Reference = requestedReference }));
        using var document = JsonDocument.Parse(reader.ReadLine() ?? throw new InvalidDataException("Secret-Kanal antwortete nicht."));
        return document.RootElement.Clone();
    }
}

static void TestProjectDeletion()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-project-delete-tests-" + Guid.NewGuid().ToString("N"));
    var outside = Path.Combine(Path.GetTempPath(), "rfidgear-project-delete-outside-" + Guid.NewGuid().ToString("N") + ".project.json");
    try
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "Demo.project.json");
        var build = Path.Combine(root, "Demo-build");
        Directory.CreateDirectory(build);
        File.WriteAllText(Path.Combine(build, "Demo_Mastering.rfPrj"), "artifact");
        ProjectJson.Save(source, ValidProject());
        var vault = ProjectKeyVault.GetPath(source);
        File.WriteAllText(vault, "encrypted-vault-placeholder");
        var entry = new ProjectCatalogEntry("Test", "Mastering", "Grundprofil-9.1", "–", ".", DateTime.Now, source, build, false);
        var service = new ProjectDeletionService();
        var preview = service.Prepare(entry, root);
        Assert(preview.VaultExists && preview.BuildDirectoryExists && preview.Contract.Contains("ausschließlich", StringComparison.Ordinal), "Löschvorschau weist erhaltene Daten nicht eindeutig aus.");

        var changed = ProjectJson.Load(source); changed.Project.Build = ["Coding"];
        ProjectJson.Save(source, changed);
        AssertThrows<ConcurrentFileChangeException>(() => service.Delete(preview));
        Assert(File.Exists(source) && File.Exists(vault) && Directory.Exists(build), "Konfliktprüfung hat Dateien verändert.");

        var current = service.Prepare(new ProjectCatalogEntry("Test", "Coding", "Grundprofil-9.1", "–", ".", DateTime.Now, source, build, false), root);
        var result = service.Delete(current);
        Assert(result.SourceDeleted && !File.Exists(source), "Projektdefinition wurde nicht gelöscht.");
        Assert(result.VaultPreserved && File.Exists(vault) && result.BuildDirectoryPreserved && File.Exists(Path.Combine(build, "Demo_Mastering.rfPrj")), "Vault oder Buildartefakte wurden mitgelöscht.");

        ProjectJson.Save(outside, ValidProject());
        var outsideEntry = new ProjectCatalogEntry("Test", "Mastering", "Grundprofil-9.1", "–", ".", DateTime.Now, outside, build, false);
        AssertThrows<InvalidOperationException>(() => service.Prepare(outsideEntry, root));
        var artifactEntry = outsideEntry with { SourcePath = Path.Combine(build, "project.definition.json"), ProjectDirectory = build, ArtifactOnly = true };
        AssertThrows<InvalidOperationException>(() => service.Prepare(artifactEntry, root));
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        if (File.Exists(outside)) File.Delete(outside);
        if (File.Exists(outside + ".rfidgear-builder.lock")) File.Delete(outside + ".rfidgear-builder.lock");
    }
}

static void TestVault()
{
    const string payload = "{\"productivePicc\":{\"type\":\"DF_KEY_AES\",\"value\":\"102132435465768798A9BACBDCEDFE0F\"}}";
    var vault = KeyVaultService.Protect(payload, "Portable-Test-Passphrase");
    Assert(vault.Version == 2 && vault.Kdf == "PBKDF2-SHA256" && vault.Iterations == 600_000, "Neue Vaults verwenden nicht den gehärteten v2-Vertrag.");
    var serialized = JsonSerializer.Serialize(vault, ProjectJson.Options);
    Assert(!serialized.Contains("102132435465768798A9BACBDCEDFE0F", StringComparison.Ordinal), "Vault enthält Klartext-Key.");
    Assert(JsonEquivalent(payload, KeyVaultService.Unprotect(vault, "Portable-Test-Passphrase")), "Vault-Roundtrip weicht ab.");
    AssertThrows<CryptographicException>(() => KeyVaultService.Unprotect(vault, "Wrong-Passphrase"));
    var bytes = Convert.FromBase64String(vault.Ciphertext); bytes[0] ^= 1; vault.Ciphertext = Convert.ToBase64String(bytes);
    AssertThrows<CryptographicException>(() => KeyVaultService.Unprotect(vault, "Portable-Test-Passphrase"));
    var legacy = KeyVaultService.ProtectLegacyV1(payload, "Portable-Test-Passphrase");
    Assert(JsonEquivalent(payload, KeyVaultService.Unprotect(legacy, "Portable-Test-Passphrase")), "Bestehender Vault v1 ist nicht mehr lesbar.");
    AssertThrows<ArgumentException>(() => KeyVaultService.Protect(payload, "zu-kurz"));
}

static void TestProjectVault()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-project-vault-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var projectPath = Path.Combine(directory, "Demo.project.json");
        var project = ValidProject();
        project.Productive.PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('A', 32) };
        project.Productive.ApplicationKeys["F482D0"] = [new string('B', 32)];
        var payload = ProjectKeyVault.FromProject(project);
        var vaultPath = ProjectKeyVault.GetPath(projectPath);
        Assert(vaultPath == Path.Combine(directory, "Demo.keys.vault.json"), "Vault-Pfad ist nicht zu v10.39 kompatibel.");
        ProjectKeyVault.Save(vaultPath, payload, "Project-Vault-Passphrase");
        var text = File.ReadAllText(vaultPath);
        Assert(!text.Contains(new string('A', 32), StringComparison.Ordinal) && !text.Contains(new string('B', 32), StringComparison.Ordinal), "Projekt-Vault enthält Klartext-Keys.");
        var loaded = ProjectKeyVault.Load(vaultPath, "Project-Vault-Passphrase");
        Assert(loaded.InitialKeySets.Count == 1 && loaded.InitialKeySets[0].Name == "Standard / Blankokarte",
            "Neuer Vault enthält keinen benannten Ausgangsschlüsselsatz.");
        var applied = ProjectKeyVault.Apply(ProjectJson.WithoutProductiveSecrets(project), loaded);
        Assert(applied.Productive.PiccKey.Value == new string('A', 32) && applied.Productive.ApplicationKeys["F482D0"][0] == new string('B', 32), "Projekt-Vault wurde nicht vollständig auf die Sitzung angewendet.");
        var vaultVariable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
        var library = ProfileLibrary.FromProfiles([MinimalProfile("Grundprofil-9.1", "base", "F482D0", vaultVariable)]);
        var audit = ProjectKeyVault.Audit(vaultPath, "Project-Vault-Passphrase", ProjectJson.WithoutProductiveSecrets(project), library);
        Assert(audit.Status == "OK" && audit.PortableAcrossUsers && audit.KeyCount == 6 && audit.ApplicationCount == 1 && audit.FileFingerprint.Length == 12,
            "Portabler, keyfreier Vault-Nachweis ist unvollständig.");
        Assert(!JsonSerializer.Serialize(audit).Contains(new string('A', 32), StringComparison.Ordinal) && !JsonSerializer.Serialize(audit).Contains(new string('B', 32), StringComparison.Ordinal),
            "Vault-Nachweis enthält Schlüsselwerte.");
        AssertThrows<CryptographicException>(() => ProjectKeyVault.Load(vaultPath, "Wrong-Passphrase"));

        var personalized = new InitialKeySet
        {
            Id = "bestand-2024",
            Name = "Personalisierter Bestand 2024",
            PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('C', 32) },
            ApplicationKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DF_KEY_AES"] = new string('D', 32) }
        };
        loaded.InitialKeySets.Add(personalized);
        loaded.SelectedInitialKeySetId = personalized.Id;
        project.Transport.KeySetId = personalized.Id;
        ProjectKeyVault.Save(vaultPath, loaded, "Project-Vault-Passphrase");
        var multi = ProjectKeyVault.Load(vaultPath, "Project-Vault-Passphrase");
        var selected = ProjectKeyVault.Apply(ProjectJson.WithoutAllSecrets(project), multi);
        Assert(multi.InitialKeySets.Count == 2 && selected.Transport.KeySetId == personalized.Id &&
               selected.Transport.PiccKey.Value == new string('C', 32) && selected.Transport.ApplicationKeys["DF_KEY_AES"] == new string('D', 32),
            "Mehrere verschlüsselte Ausgangsschlüsselsätze werden nicht eindeutig ausgewählt.");
        var keyFreeText = JsonSerializer.Serialize(ProjectJson.WithoutAllSecrets(selected), ProjectJson.Options);
        Assert(keyFreeText.Contains(personalized.Id, StringComparison.Ordinal) && !keyFreeText.Contains(personalized.Name, StringComparison.Ordinal) &&
               !keyFreeText.Contains(new string('C', 32), StringComparison.Ordinal) && !keyFreeText.Contains(new string('D', 32), StringComparison.Ordinal),
            "Projektdefinition enthält Name oder Werte des Ausgangsschlüsselsatzes.");

        var legacyPayload = new ProjectKeyVaultPayload
        {
            TransportPicc = new KeyReference { Type = "DF_KEY_DES", Value = new string('0', 32) },
            TransportApplicationKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DF_KEY_AES"] = new string('0', 32) },
            ProductivePicc = loaded.ProductivePicc,
            PiccKeySettings = loaded.PiccKeySettings,
            ApplicationKeys = loaded.ApplicationKeys
        };
        var legacyApplied = ProjectKeyVault.Apply(ProjectJson.WithoutAllSecrets(project), legacyPayload);
        Assert(legacyPayload.InitialKeySets.Count == 1 && legacyPayload.InitialKeySets[0].Name == "Bisheriger Ausgangsschlüsselsatz" &&
               legacyApplied.Transport.PiccKey.Value == new string('0', 32), "Alter Vault wird nicht als Ausgangsschlüsselsatz migriert.");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Vault-Datei blieb zurück.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestVaultRecoveryBackup()
{
    var root = Path.Combine(Path.GetTempPath(), "sieencode-vault-backup-tests-" + Guid.NewGuid().ToString("N"));
    var projectDirectory = Path.Combine(root, "projects", "Demo");
    var backupDirectory = Path.Combine(root, "independent-backup");
    Directory.CreateDirectory(projectDirectory);
    try
    {
        var project = ValidProject();
        project.Project.Name = "Demo";
        project.Productive.PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('A', 32) };
        project.Productive.ApplicationKeys["F482D0"] = Enumerable.Range(0, 5).Select(index => new string((char)('B' + index), 32)).ToList();
        var projectPath = Path.Combine(projectDirectory, "Demo.project.json");
        ProjectJson.Save(projectPath, ProjectJson.WithoutAllSecrets(project));
        var vaultPath = ProjectKeyVault.GetPath(projectPath);
        const string passphrase = "Recovery-Backup-Test-2026!";
        ProjectKeyVault.Save(vaultPath, ProjectKeyVault.FromProject(project), passphrase);
        var verified = ProjectKeyVault.Load(vaultPath, passphrase);
        var result = new VaultBackupService().Create(projectPath, vaultPath, backupDirectory, verified);

        Assert(File.Exists(result.ProjectPath) && File.Exists(result.VaultPath) && File.Exists(result.ManifestPath), "Recovery-Paket ist unvollständig.");
        Assert(ProjectKeyVault.Load(result.VaultPath, passphrase).ApplicationKeys["F482D0"].Count == 5, "Gesicherter Vault kann nicht vollständig wieder geöffnet werden.");
        var corpus = string.Join("\n", Directory.EnumerateFiles(result.Directory).Select(File.ReadAllText));
        var manifest = JsonSerializer.Deserialize<VaultBackupManifest>(File.ReadAllText(result.ManifestPath), ProjectJson.Options);
        Assert(manifest is { ContainsKeys: false } && manifest.KeySetIdentity == result.KeySetIdentity, "Recovery-Manifest ist nicht ausdrücklich keyfrei oder ohne Keyset-Identität.");
        Assert(!corpus.Contains(new string('A', 32), StringComparison.Ordinal) && !corpus.Contains(new string('B', 32), StringComparison.Ordinal), "Recovery-Paket enthält Klartextschlüssel außerhalb des verschlüsselten Vaults.");
        Assert(result.KeySetIdentity == KeySetIdentityService.Compute(ProjectKeyVault.FromProject(project)), "Keyset-Identität ist für denselben Schlüsselsatz nicht stabil.");
        project.Productive.ApplicationKeys["F482D0"][0] = new string('F', 32);
        Assert(result.KeySetIdentity != KeySetIdentityService.Compute(ProjectKeyVault.FromProject(project)), "Keyset-Identität erkennt einen geänderten Schlüssel nicht.");

        ProjectJson.Save(projectPath, project);
        AssertThrows<InvalidDataException>(() => new VaultBackupService().Create(projectPath, vaultPath, backupDirectory, verified));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestCertificateVault()
{
    using var aliceKey = RSA.Create(3072); using var bobKey = RSA.Create(3072); using var eveKey = RSA.Create(3072);
    using var alice = SelfSigned("Alice", aliceKey); using var bob = SelfSigned("Bob", bobKey); using var eve = SelfSigned("Eve", eveKey);
    const string payload = "{\"productivePicc\":{\"type\":\"DF_KEY_AES\",\"value\":\"102132435465768798A9BACBDCEDFE0F\"}}";
    const string recovery = "Recovery-aus-KeePass-2026!";
    var vault = CertificateVaultService.Protect(payload, recovery, [(alice, "Alice"), (bob, "Bob")]);
    Assert(vault.Version == 3 && vault.Recipients.Count == 2 && vault.Recipients.All(row => !string.IsNullOrWhiteSpace(row.WrappedKey)), "Zertifikats-Vault enthält keine eindeutigen Empfängerumschläge.");
    Assert(JsonEquivalent(payload, CertificateVaultService.UnprotectWithCertificate(vault, [alice])), "Alice kann den freigegebenen Vault nicht öffnen.");
    Assert(JsonEquivalent(payload, CertificateVaultService.UnprotectWithCertificate(vault, [bob])), "Bob kann den freigegebenen Vault nicht öffnen.");
    AssertThrows<CryptographicException>(() => CertificateVaultService.UnprotectWithCertificate(vault, [eve]));
    Assert(JsonEquivalent(payload, KeyVaultService.Unprotect(vault, recovery)), "Recovery-Zugang des Zertifikats-Vaults ist nicht funktionsfähig.");
    AssertThrows<CryptographicException>(() => KeyVaultService.Unprotect(vault, "Falsche-Recovery-Passphrase-2026!"));
    const string updatedPayload = "{\"productivePicc\":{\"type\":\"DF_KEY_AES\",\"value\":\"00112233445566778899AABBCCDDEEFF\"}}";
    var recoveryEnvelope = (vault.RecoveryNonce, vault.RecoveryCiphertext, vault.RecoveryTag);
    var recipientEnvelopes = vault.Recipients.Select(row => (row.Id, row.WrappedKey)).ToArray();
    var updatedVault = CertificateVaultService.ReprotectWithCertificate(vault, updatedPayload, [alice]);
    Assert(JsonEquivalent(updatedPayload, CertificateVaultService.UnprotectWithCertificate(updatedVault, [bob])) &&
           JsonEquivalent(updatedPayload, KeyVaultService.Unprotect(updatedVault, recovery)),
        "Vault-Aktualisierung über eine freigegebene Windows-Identität erhält Empfänger- oder Recovery-Zugang nicht.");
    Assert(recoveryEnvelope == (updatedVault.RecoveryNonce, updatedVault.RecoveryCiphertext, updatedVault.RecoveryTag) &&
           recipientEnvelopes.SequenceEqual(updatedVault.Recipients.Select(row => (row.Id, row.WrappedKey))) &&
           vault.InitializationVector != updatedVault.InitializationVector,
        "Vault-Aktualisierung verändert Zugriffsumschläge oder verwendet Payload-Nonce erneut.");
    AssertThrows<CryptographicException>(() => CertificateVaultService.ReprotectWithCertificate(vault, updatedPayload, [eve]));
    var serialized = JsonSerializer.Serialize(vault, ProjectJson.Options);
    Assert(!serialized.Contains("102132435465768798A9BACBDCEDFE0F", StringComparison.Ordinal), "Zertifikats-Vault enthält den Klartext-Key.");
    var invitation = CertificateVaultService.ExportInvitation(alice, "Alice");
    var imported = CertificateVaultService.ImportInvitation(invitation);
    using (imported.Certificate) Assert(imported.DisplayName == "Alice" && CertificateVaultService.Fingerprint(imported.Certificate) == invitation.Fingerprint, "Öffentliche Empfängereinladung ist nicht stabil.");
    invitation.Fingerprint = new string('0', 64);
    AssertThrows<CryptographicException>(() => CertificateVaultService.ImportInvitation(invitation));

    var teamRoot = Path.Combine(Path.GetTempPath(), "sieencode-team-recipients-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new TeamVaultRecipientStore(teamRoot);
        var aliceInvitation = CertificateVaultService.ExportInvitation(alice, "Alice");
        var bobInvitation = CertificateVaultService.ExportInvitation(bob, "Bob");
        store.Save([aliceInvitation, bobInvitation]);
        var stored = store.Load();
        var teamText = File.ReadAllText(store.Path);
        Assert(stored.Recipients.Count == 2 && stored.Recipients.Select(row => row.Fingerprint).ToHashSet().SetEquals([aliceInvitation.Fingerprint, bobInvitation.Fingerprint]),
            "Zentrale Team-Empfängerliste ist nicht vollständig portabel.");
        Assert(!teamText.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) && !teamText.Contains("wrappedKey", StringComparison.OrdinalIgnoreCase),
            "Team-Empfängerliste enthält private oder projektspezifisch umschlossene Schlüssel.");
        AssertThrows<InvalidDataException>(() => store.Save([aliceInvitation, aliceInvitation]));
    }
    finally { if (Directory.Exists(teamRoot)) Directory.Delete(teamRoot, true); }
}

static X509Certificate2 SelfSigned(string name, RSA key)
{
    var request = new CertificateRequest($"CN=SieEncode Test {name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(1));
}

static void TestFullTargetReport()
{
    var payload = new string('4', 1022) + "FF";
    var name = string.Join(" ", Enumerable.Repeat("Langer Dateiname", 8)) + " DATEIENDE";
    var definition = new ProfileDefinition { Profile = new ProfileMetadata { Id = "SYNTH-PDF", Kind = "base" },
        Applications = [new ApplicationDefinition { Id = "A1B2C3", Files = [new FileDefinition { Id = 1, Size = 512,
            Coding = new CodingDefinition { Output = "test.hex", Recipe = [new RecipeStep { Op = "fixedHex", Value = payload }] } }] }] };
    var project = new ProjectDefinition { Project = new ProjectSection { Name = "Synthetische Sollreport-Abnahme" }, Profiles = new ProfileSelection { Base = "SYNTH-PDF" },
        Values = new Dictionary<string, string> { ["Notiz"] = new string('W', 100) + " FELDENDE" } };
    var analysis = new ProjectAnalysisReport { Applications = [new ProjectAnalysisApplication("A1B2C3", name, "AES", 14, "0B", "0F", name, [new ProjectAnalysisFile(1, name, "StdDataFile", 512, "CM_PLAIN", "R0/W0/RW0/C0", name, name, name)])] };
    var report = new CardReportFactory().FromProject(project, analysis, "synthetisch", ProfileLibrary.FromProfiles([definition]));
    Assert(report.Applications[0].Files[0].HexDump.Replace(" ", "") == payload && report.Applications[0].Files[0].AsciiDump.Length == 512, "Sollreport must retain every payload byte.");
    var requested = Environment.GetEnvironmentVariable("SIEENCODE_FULL_TARGET_PDF_REVIEW");
    var path = string.IsNullOrWhiteSpace(requested) ? Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf") : Path.GetFullPath(requested);
    try
    {
        new CardReportPdfService().Save(report, path);
        var content = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert(content.Contains("DATEIENDE") && content.Contains("FELDENDE") && content.Contains("13: nicht"), "Names, fields and key-version list must not be clipped.");
        Assert(content.Contains("44 FF") && content.Split("/Type /Page ").Length > 2, "Full payload tail must survive multipage layout.");
    }
    finally { if (string.IsNullOrWhiteSpace(requested)) File.Delete(path); }
}

static void TestCardReportPdf()
{
    var root = Path.Combine(Path.GetTempPath(), "sieencode-pdf-report-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var project = new ProjectDefinition
        {
            Project = new ProjectSection { Name = "PDF Abnahme", CardMode = CardOperationMode.FullCard, Build = ["Mastering", "Coding"] },
            Profiles = new ProfileSelection { Base = "Basis-1", Customers = ["Kunde-A"] },
            Productive = new ProductiveConfiguration { PiccKeySettings = "0F" },
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CardNumber"] = "47110001",
                ["SecretKey"] = "DEADBEEFCAFEBABE0011223344556677"
            }
        };
        var files = Enumerable.Range(0, 14).Select(index => new ProjectAnalysisFile(
            index, "Datei " + index, index % 2 == 0 ? "StandardDataFile" : "BackupDataFile",
            32 + index, index % 2 == 0 ? "Plain" : "Enciphered", "R1/W2/RW1/C0",
            index % 3 == 0 ? "Plain R0/W0/RW0/C0" : null, "zeroFill", "modulares BCD/HEX/ASCII-Rezept")).ToArray();
        var analysis = new ProjectAnalysisReport
        {
            Project = project.Project.Name,
            GeneratedAt = "2026-09-05T12:00:00+02:00",
            Applications =
            [
                new ProjectAnalysisApplication("F482D0", "Zutritt", "AES", 5, "0B", "0F", "key0", files),
                new ProjectAnalysisApplication("F51773", "Info", "AES", 3, "0B", "0F", "key0", files.Take(5).ToArray())
            ],
            Issues = [new ProjectAnalysisIssue("WARN", "Test", "Nicht verfuegbare Istwerte bleiben sichtbar.")]
        };
        var report = new CardReportFactory().FromProject(project, analysis, @"D:\Projekte\PDF-Abnahme\projekt.project.json");
        var path = Path.Combine(root, "Kartenreport.pdf");
        new CardReportPdfService().Save(report, path);
        var bytes = File.ReadAllBytes(path);
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert(bytes.Length > 3000 && text.StartsWith("%PDF-1.4", StringComparison.Ordinal), "PDF-Kartenreport wurde nicht als gueltiges PDF erzeugt.");
        Assert(text.Split("/Type /Page ", StringSplitOptions.None).Length - 1 >= 2, "Langer Kartenreport wurde nicht sauber auf mehrere Seiten umgebrochen.");
        Assert(text.Contains("F482D0", StringComparison.Ordinal) && text.Contains("StandardDataFile", StringComparison.Ordinal) && text.Contains("CardNumber", StringComparison.Ordinal), "PDF enthaelt nicht alle relevanten Projekt-, Applikations- und Dateiinformationen.");
        Assert(!text.Contains("DEADBEEFCAFEBABE0011223344556677", StringComparison.Ordinal) && !report.ProjectFields.Any(field => field.Name == "SecretKey"), "PDF-Kartenreport hat einen Schluesselwert uebernommen.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestProject()
{
    var project = ValidProject();
    Assert(ProjectValidator.Validate(project).Count == 0, "Gültiges Mastering-Projekt wurde abgelehnt.");
    project.Project.Name = "..\\Ausbruch";
    Assert(ProjectValidator.Validate(project).Any(issue => issue.Code == "PROJECT_NAME"), "Pfadzeichen im Projektnamen wurden nicht abgelehnt.");
    project.Project.Name = "Test";
    project.Project.Build = ["Coding"];
    Assert(ProjectValidator.Validate(project, requireProductiveSecrets: true).Any(issue => issue.Code == "PRODUCTIVE_PICC"), "Coding ohne Produktivkey wurde nicht erkannt.");
    project.Productive.PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = "A1B2C3D4E5F60718293A4B5C6D7E8F90" };
    project.Productive.ApplicationKeys["F482D0"] = ["00112233445566778899AABBCCDDEEFF"];
    var clean = ProjectJson.WithoutProductiveSecrets(project);
    Assert(clean.Productive.PiccKey.Value == "" && clean.Productive.ApplicationKeys["F482D0"].Count == 0, "Sanitizing ließ Produktivkeys zurück.");
    Assert(project.Productive.PiccKey.Value.Length == 32, "Sanitizing veränderte das Quellobjekt.");
    var fullyClean = ProjectJson.WithoutAllSecrets(project);
    Assert(fullyClean.Transport.PiccKey.Value == "" && fullyClean.Transport.ApplicationKeys.Values.All(string.IsNullOrEmpty) &&
           fullyClean.Productive.PiccKey.Value == "", "Vollständiges Sanitizing ließ Transport- oder Produktivkeys zurück.");
}

static void TestProjectDraft()
{
    var projectVariable = new VariableDefinition { Id = "firmCode", Label = "Firmencode", Scope = "project", Type = "digits", Length = 4, Default = "1234", Required = true };
    var baseProfile = MinimalProfile("Base", "base", "ABCDEF", projectVariable);
    var customer = MinimalProfile("Customer", "customer", "123456", projectVariable);
    var library = ProfileLibrary.FromProfiles([baseProfile, customer]);
    var draft = ProjectDraftFactory.Create(library, "Neues Projekt");
    Assert(draft.Profiles.Base == "Base" && draft.Values["firmCode"] == "1234", "Neuer Projektentwurf übernimmt Masterprofil/Projektvariable nicht.");
    Assert(ProfileBindingService.Evaluate(draft, library).All(status => status.IsMatch), "Neuer Projektentwurf bindet den aktuellen Masterprofilstand nicht.");
    Assert(draft.Productive.ApplicationKeys["ABCDEF"].Count == 1, "Produktiv-Keyslots des Masterprofils fehlen.");
    draft.Profiles.Customers = ["Customer"];
    ProjectDraftFactory.Synchronize(draft, library);
    Assert(draft.Productive.ApplicationKeys.Keys.Order().SequenceEqual(new[] { "123456", "ABCDEF" }), "Additive Profilwahl synchronisiert Produktiv-Keyslots nicht.");
    Assert(ProfileBindingService.Evaluate(draft, library).Single(status => status.ProfileId == "Customer").State == ProfileBindingState.Missing, "Neu gewähltes Kundenprofil wurde unbemerkt gebunden.");
    draft.Profiles.Customers = [];
    ProjectDraftFactory.Synchronize(draft, library);
    Assert(!draft.Productive.ApplicationKeys.ContainsKey("123456"), "Abgewähltes Kundenprofil blieb im Projekt-Keymodell zurück.");
}

static void TestManagedBaseProfiles()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-multi-base-tests-" + Guid.NewGuid().ToString("N"));
    var bundled = Path.Combine(root, "bundled");
    var customer = Path.Combine(root, "profiles", "customer");
    var bases = Path.Combine(root, "profiles", "base");
    Directory.CreateDirectory(bundled);
    try
    {
        var variable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
        var primary = MinimalProfile("Grundprofil-9.1", "base", "ABCDEF", variable);
        File.WriteAllText(Path.Combine(bundled, "base.json"), JsonSerializer.Serialize(primary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var store = new ProfileStore(bundled, customer, baseDirectory: bases);
        var secondary = MinimalProfile("Grundprofil-Zutritt", "base", "123456", variable);
        var saved = store.SaveBase(secondary);
        var library = store.Load();
        Assert(File.Exists(saved) && library.Validate().Count == 0 && library.Profiles.Count(profile => profile.Profile.Kind == "base") == 2,
            "Mehrere verwaltete Grundprofile werden nicht gemeinsam geladen.");
        Assert(ProjectDraftFactory.Create(library).Profiles.Base == "Grundprofil-9.1", "Das bewährte Standardgrundprofil wird bei mehreren Grundprofilen nicht deterministisch vorausgewählt.");
        Assert(library.Compose("Grundprofil-Zutritt", []).Applications.Single().Id == "123456", "Ein alternatives Grundprofil kann nicht projektweise gewählt werden.");

        var changed = new ProfileEditingService().Clone(secondary);
        changed.Profile.Name = "Geänderter Zutritt";
        AssertThrows<InvalidDataException>(() => store.SaveBase(changed));
        changed.Profile.Version = "2.0";
        store.SaveBase(changed);
        Assert(store.Load().Get("Grundprofil-Zutritt").Profile.PreviousVersion == "1.0" && store.ListHistory("Grundprofil-Zutritt").Count == 1,
            "Grundprofiländerungen werden nicht versioniert und archiviert.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestProfileBindingAndHistory()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-profile-history-tests-" + Guid.NewGuid().ToString("N"));
    var bundled = Path.Combine(root, "bundled");
    var user = Path.Combine(root, "profiles", "customer");
    Directory.CreateDirectory(bundled);
    try
    {
        var original = MinimalProfile("Customer", "customer", "123456", new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true });
        var baseProfile = MinimalProfile("Base", "base", "ABCDEF", new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true });
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        File.WriteAllText(Path.Combine(bundled, "base.json"), JsonSerializer.Serialize(baseProfile, options));
        File.WriteAllText(Path.Combine(bundled, "customer.json"), JsonSerializer.Serialize(original, options));
        var instants = new Queue<DateTimeOffset>([new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero), new(2026, 8, 13, 11, 0, 0, TimeSpan.Zero), new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)]);
        var store = new ProfileStore(bundled, user, () => instants.Dequeue());
        var changed = new ProfileEditingService().Clone(original);
        changed.Profile.Name = "Geändertes Kundenprofil";
        AssertThrows<InvalidDataException>(() => store.SaveCustomer(changed));
        changed.Profile.Version = "1.1";
        store.SaveCustomer(changed);
        var history = store.ListHistory("Customer");
        Assert(history.Count == 1 && Path.GetFileName(history[0]).StartsWith("1.0_", StringComparison.Ordinal), "Vorgängerprofil wurde nicht eindeutig archiviert.");
        var current = store.Load().Get("Customer");
        Assert(current.Profile.PreviousVersion == "1.0" && current.Profile.CreatedAt is not null && current.Profile.ModifiedAt is not null, "Versionsmetadaten wurden nicht vollständig gespeichert.");
        Assert(ProfileIdentityService.ComputeDigest(original) != ProfileIdentityService.ComputeDigest(current), "Inhaltsfingerabdruck erkennt die Profiländerung nicht.");
        var reusedVersion = new ProfileEditingService().Clone(current);
        reusedVersion.Profile.Name = "Unzulässige Wiederverwendung";
        reusedVersion.Profile.Version = "1.0";
        AssertThrows<InvalidDataException>(() => store.SaveCustomer(reusedVersion));
        var historyService = new ProfileHistoryService(store);
        var historyEntries = historyService.List("Customer");
        Assert(historyEntries.Count == 2 && historyEntries.All(entry => entry.IntegrityValid) && historyEntries.Count(entry => entry.IsCurrent) == 1, "Historienindex enthält nicht exakt aktuellen und archivierten Stand.");
        var archived = historyEntries.Single(entry => !entry.IsCurrent);
        var historyDiff = historyService.CompareWithCurrent(archived, new DateTime(2026, 8, 13, 12, 0, 0));
        Assert(historyDiff.Items.Any(item => item.Property == "Version") && historyDiff.Items.Any(item => item.Property == "Name"), "Historienvergleich zeigt die fachlichen Änderungen nicht.");
        var forged = archived with { Path = Path.Combine(root, "outside.json") };
        AssertThrows<InvalidDataException>(() => historyService.Load(forged));

        var library = ProfileLibrary.FromProfiles([baseProfile, current]);
        var project = ProjectDraftFactory.Create(library);
        project.Profiles.Customers = ["Customer"];
        ProjectDraftFactory.Synchronize(project, library);
        AssertThrows<InvalidDataException>(() => ProfileBindingService.EnsureBuildCompatible(project, library));
        ProfileBindingService.BindCurrent(project, library);
        ProfileBindingService.EnsureBuildCompatible(project, library);
        current.Applications[0].Files[0].Size++;
        Assert(ProfileBindingService.Evaluate(project, library).Any(status => status.State == ProfileBindingState.DigestMismatch), "Verändertes Profil mit gleicher Version wird nicht als Bindungsbruch erkannt.");

        AssertThrows<InvalidDataException>(() => historyService.RestoreAsNewVersion(archived, "1.1"));
        var restored = historyService.RestoreAsNewVersion(archived, "2.0");
        var restoredProfile = store.Load().Get("Customer");
        Assert(restored.Version == "2.0" && restored.PreviousVersion == "1.1" && restoredProfile.Profile.Name == original.Profile.Name && restoredProfile.Profile.PreviousVersion == "1.1", "Historienstand wurde nicht sicher als neue Version wiederhergestellt.");
        Assert(store.ListHistory("Customer").Count == 2, "Der vor der Wiederherstellung aktuelle Stand wurde nicht zusätzlich archiviert.");
        var restoredLibrary = ProfileLibrary.FromProfiles([baseProfile, restoredProfile]);
        Assert(ProfileBindingService.Evaluate(project, restoredLibrary).Any(status => status.State == ProfileBindingState.VersionMismatch), "Wiederherstellung umgeht die bestehende Projektbindung.");

        var archivedCurrentPath = store.ListHistory("Customer").Single(path => Path.GetFileName(path).StartsWith("1.1_", StringComparison.Ordinal));
        var tampered = JsonSerializer.Deserialize<ProfileDefinition>(File.ReadAllText(archivedCurrentPath), options)!;
        tampered.Profile.Name += " manipuliert";
        File.WriteAllText(archivedCurrentPath, JsonSerializer.Serialize(tampered, options));
        var invalidEntry = historyService.List("Customer").Single(entry => string.Equals(entry.Path, archivedCurrentPath, StringComparison.OrdinalIgnoreCase));
        Assert(!invalidEntry.IntegrityValid && invalidEntry.Issue.Contains("Integritätsprüfung", StringComparison.Ordinal), "Manipulierter Historienstand wird nicht als Integritätsfehler markiert.");
        AssertThrows<InvalidDataException>(() => historyService.Load(invalidEntry));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestPaths()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-path-root");
    var output = ProjectPaths.ResolveOutput("Kunden", root);
    Assert(output == Path.GetFullPath(Path.Combine(root, "Kunden")), "Relativer Ausgabeordner verwendet nicht die Projektwurzel.");
    var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rfidgear-absolute"));
    Assert(ProjectPaths.ResolveOutput(absolute, root) == absolute, "Absoluter Ausgabeordner wurde verändert.");
}

static void TestSettingsAndCatalog()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-catalog-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var settingsPath = Path.Combine(root, "appdata", "builder.settings.json");
        var projectsRoot = Path.Combine(root, "projects");
        var profilesRoot = Path.Combine(root, "shared-profiles");
        var store = new BuilderSettingsStore(settingsPath);
        var vaultBackupRoot = Path.Combine(root, "vault-backup");
        var settings = new BuilderSettings { ExpertMode = true, Projects = new ProjectsSettings { Directory = projectsRoot }, Profiles = new ProfilesSettings { Directory = profilesRoot }, VaultBackup = new VaultBackupSettings { Directory = vaultBackupRoot } };
        store.Save(settings);
        var loaded = store.Load();
        Assert(loaded.ExpertMode && loaded.Projects.Directory == Path.GetFullPath(projectsRoot) && Directory.Exists(projectsRoot) &&
               loaded.Profiles.Directory == Path.GetFullPath(profilesRoot) && Directory.Exists(profilesRoot) && store.ResolveProfilesDirectory(loaded) == Path.GetFullPath(profilesRoot) &&
               loaded.VaultBackup.Directory == Path.GetFullPath(vaultBackupRoot) && Directory.Exists(vaultBackupRoot),
            "Externe Projekt- oder Profilbibliothek wurde nicht dauerhaft gespeichert/angelegt.");
        Assert(!Directory.EnumerateFiles(Path.GetDirectoryName(settingsPath)!, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Einstellungsdatei blieb zurück.");

        var legacySettingsPath = Path.Combine(root, "legacy", "builder.settings.json");
        var migratedSettingsPath = Path.Combine(root, "migrated-appdata", "builder.settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacySettingsPath)!);
        File.WriteAllText(legacySettingsPath, "{\"Projects\":{\"Directory\":\"" + projectsRoot.Replace("\\", "\\\\") + "\"}}");
        var migrated = new BuilderSettingsStore(migratedSettingsPath, legacySettingsPath).Load();
        Assert(migrated.Projects.Directory == projectsRoot && File.Exists(migratedSettingsPath), "Bestehende Builder-Einstellungen wurden nicht in den versionsunabhÃ¤ngigen Speicher migriert.");

        var editableDirectory = Path.Combine(projectsRoot, "Editable"); Directory.CreateDirectory(editableDirectory);
        var editable = ValidProject(); editable.Project.Name = "Editable"; editable.Project.OutputDirectory = "Built"; editable.Project.Build = ["Mastering", "Coding"];
        var editablePath = Path.Combine(editableDirectory, "Editable.project.json");
        ProjectJson.Save(editablePath, editable);
        var editableBuildDirectory = Path.Combine(projectsRoot, "Built", "Editable"); Directory.CreateDirectory(editableBuildDirectory);
        File.WriteAllText(Path.Combine(editableBuildDirectory, "project.definition.json"), "{\"project\":{\"name\":\"EditableBuild\"},\"profiles\":[]}");
        var artifactDirectory = Path.Combine(projectsRoot, "LegacyBuilt"); Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(Path.Combine(artifactDirectory, "project.definition.json"), "{\"project\":{\"name\":\"LegacyBuilt\",\"generatedAt\":\"2026-01-01T00:00:00\"},\"profiles\":[{\"id\":\"Grundprofil-9.1\",\"kind\":\"base\"},{\"id\":\"DEVK\",\"kind\":\"customer\"}],\"applications\":[]}");
        File.WriteAllText(Path.Combine(projectsRoot, "ignore.template.json"), "{}");
        File.WriteAllText(Path.Combine(projectsRoot, "broken.json"), "not json");
        var catalog = new ProjectCatalog().Scan(projectsRoot);
        Assert(catalog.Count == 2, "Projektkatalog erkennt bearbeitbares und gebautes Altprojekt nicht eindeutig.");
        var editableEntry = catalog.Single(entry => entry.Name == "Editable");
        Assert(!editableEntry.ArtifactOnly && !editableEntry.HasVault && editableEntry.Kind == "Neu bauen" && editableEntry.KeyState == "Neubau gesperrt" &&
               editableEntry.ProjectDirectory == Path.GetFullPath(editableBuildDirectory) && editableEntry.Build == "Mastering + Coding" &&
               editableEntry.HasCoding && !editableEntry.CanOpenStation,
            "Projekt ohne vollständigen Build oder gültigen Vault wird falsch katalogisiert.");
        var artifactEntry = catalog.Single(entry => entry.Name == "LegacyBuilt");
        Assert(artifactEntry.ArtifactOnly && artifactEntry.BaseProfile == "Grundprofil-9.1" && artifactEntry.Customers == "DEVK" &&
               !artifactEntry.HasCoding && !artifactEntry.CanOpenStation, "Gebautes Altprojekt wurde falsch katalogisiert.");

        File.WriteAllText(ProjectKeyVault.GetPath(editablePath), "{broken vault");
        Assert(!new ProjectCatalog().Scan(projectsRoot).Single(entry => entry.Name == "Editable").HasVault, "Beschädigter Vault wird als gültig ausgewiesen.");
        ProjectKeyVault.Save(ProjectKeyVault.GetPath(editablePath), ProjectKeyVault.FromProject(editable), "Catalog-Test-Passphrase");
        var securedEntry = new ProjectCatalog().Scan(projectsRoot).Single(entry => entry.Name == "Editable");
        Assert(securedEntry.HasVault && securedEntry.Kind == "Neu bauen" && securedEntry.KeyState == "Neubau mit Vault" && !securedEntry.CanOpenStation,
            "Passphrasen-Altvault wird im Katalog falsch ausgewiesen oder unsicher für die Kodierstation freigegeben.");

        using var stationKey = RSA.Create(3072);
        using var stationCertificate = SelfSigned("Catalog Station", stationKey);
        ProjectKeyVault.SaveForRecipients(ProjectKeyVault.GetPath(editablePath), ProjectKeyVault.FromProject(editable),
            "Catalog-Recovery-Passphrase", [(stationCertificate, "Catalog Station")], ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(editablePath)));
        var recipientSecuredEntry = new ProjectCatalog().Scan(projectsRoot).Single(entry => entry.Name == "Editable");
        Assert(recipientSecuredEntry.SecureOnDemand && recipientSecuredEntry.CanOpenStation,
            "Empfängergebundener Projekt-Vault wird nicht sicher für die Kodierstation angeboten.");

        editable.Project.Build = ["Mastering"];
        ProjectJson.Save(editablePath, editable);
        var masteringOnlyEntry = new ProjectCatalog().Scan(projectsRoot).Single(entry => entry.Name == "Editable");
        Assert(!masteringOnlyEntry.HasCoding && !masteringOnlyEntry.CanOpenStation,
            "Reines Mastering-Projekt darf die Kodierstation nicht anbieten.");

        File.WriteAllText(Path.Combine(artifactDirectory, "coding.schema.json"), "{}");
        File.WriteAllText(Path.Combine(artifactDirectory, "LegacyBuilt_Kodierung.rfPrj"), "test artifact");
        var codingArtifactEntry = new ProjectCatalog().Scan(projectsRoot).Single(entry => entry.Name == "LegacyBuilt");
        Assert(codingArtifactEntry.HasCoding && codingArtifactEntry.CanOpenStation,
            "Gebautes Altprojekt mit Kodierschema wird nicht für die Kodierstation angeboten.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestCodingSession()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-session-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var projectPath = Path.Combine(root, "Demo_Kodierung.rfPrj");
        RfPrjArchive.Save(projectPath, RfPrjDocument.Create());
        File.WriteAllText(Path.Combine(root, "coding.schema.json"), """
        {
          "schemaVersion":"1.1","projectName":"Demo","codingProject":"Demo_Kodierung.rfPrj",
          "finalVerificationDirectory":"verification",
          "variables":[
            {"id":"firmCode","label":"Firmencode","scope":"project","type":"digits","length":4,"required":true,"value":"1234"},
            {"id":"cardNumber","label":"Kartennummer","scope":"card","type":"digits","allowedLengths":[4,6],"required":true}
          ],
          "payloads":[{"aid":"F00001","appName":"Test","fileId":0,"fileName":"Data","size":10,"output":"card.hex",
            "recipe":[{"op":"asciiDigits","source":"firmCode","width":4,"padLeft":"0"},{"op":"asciiDigits","source":"cardNumber","width":6,"padLeft":"0"}]}]
        }
        """);
        var service = new CodingSessionService();
        var definition = service.Load(root);
        Assert(definition.ProjectName == "Demo" && definition.CardFields.Count == 1 && definition.PayloadCount == 1, "Kodierschema wurde nicht vollständig als Sitzung geladen.");
        var logDirectory = Path.Combine(root, "logs");
        AssertThrows<InvalidDataException>(() => service.Prepare(definition, new Dictionary<string, string> { ["cardNumber"] = "12A4" }, logDirectory));
        var invalidStatePath = Path.Combine(root, "payloads", PayloadPreparationStateService.StateFileName);
        Assert(File.Exists(invalidStatePath) && File.ReadAllText(invalidStatePath).Contains("\"State\": \"NotPrepared\"", StringComparison.Ordinal) &&
            !File.Exists(Path.Combine(root, "payloads", "card.hex")), "Ungültige Sitzungswerte sperren vorherige Payloads nicht sicher.");
        using var session = service.Prepare(definition, new Dictionary<string, string> { ["cardNumber"] = "5678" }, logDirectory);
        AssertThrows<IOException>(() => service.Prepare(definition, new Dictionary<string, string> { ["cardNumber"] = "9999" }, logDirectory));
        Assert(Directory.Exists(Path.Combine(root, "verification")), "Sitzungsbereitung legt den konfigurierten Zielordner für finale RFiDGear-Readbacks nicht an.");
        var payloadPath = Path.Combine(root, "payloads", "card.hex");
        Assert(File.Exists(payloadPath) && string.Concat(File.ReadAllText(payloadPath).Where(Uri.IsHexDigit)) == "31323334303035363738", "Sitzungs-Payload ist nicht bytegleich zum Schema-Rezept.");
        var statePath = Path.Combine(root, "payloads", PayloadPreparationStateService.StateFileName);
        var preparedState = File.ReadAllText(statePath);
        Assert(preparedState.Contains("\"State\": \"Prepared\"", StringComparison.Ordinal) && preparedState.Contains("\"ContainsCardData\": true", StringComparison.Ordinal), "Erfolgreiche Enter-Vorbereitung setzt keinen Prepared-Sicherheitsstatus.");
        File.WriteAllText(statePath, preparedState.Replace("\"State\": \"Prepared\"", "\"State\": \"NotPrepared\"", StringComparison.Ordinal));
        AssertThrows<InvalidDataException>(() => service.EnsurePreparedPayloads(session));
        File.WriteAllText(statePath, preparedState);
        var preparedPayloadText = File.ReadAllText(payloadPath);
        File.WriteAllText(payloadPath, "00");
        AssertThrows<InvalidDataException>(() => service.EnsurePreparedPayloads(session));
        File.WriteAllText(payloadPath, preparedPayloadText);
        service.EnsurePreparedPayloads(session);
        var lastCodingPath = Path.Combine(root, "Letzte_Kodierung.json");
        Assert(File.Exists(lastCodingPath) && File.ReadAllText(lastCodingPath).Contains("31323334303035363738", StringComparison.Ordinal), "C#-Kodiersitzung erzeugt keinen v10.39-kompatiblen Abnahmenachweis.");
        var expectedCardPath = Path.Combine(root, "card_preflight", "expected-card.json");
        Assert(File.Exists(expectedCardPath) && File.ReadAllText(expectedCardPath).Contains("\"containsKeys\": false", StringComparison.Ordinal) &&
               File.ReadAllText(expectedCardPath).Contains("\"cardNumber\": \"5678\"", StringComparison.Ordinal) &&
               File.ReadAllText(expectedCardPath).Contains("\"sha256\":", StringComparison.Ordinal),
            "C#-Kodiersitzung erzeugt keinen keyfreien, nach Payload-Invaliderung weiter nutzbaren Sollnachweis für die Produktivprobe.");
        Assert(service.Evaluate(session).State == CodingRunState.Waiting, "Sitzung meldet ohne neue Readbacks fälschlich Erfolg.");
        var verificationPath = Path.Combine(root, "verification", "card.hex");
        Directory.CreateDirectory(Path.GetDirectoryName(verificationPath)!);
        File.WriteAllText(verificationPath, File.ReadAllText(payloadPath));
        File.SetLastWriteTimeUtc(verificationPath, DateTime.UtcNow.AddSeconds(2));
        Assert(service.Evaluate(session).State == CodingRunState.Successful, "Bytegleicher neuer Readback schließt die Sitzung nicht erfolgreich ab.");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "log-session.txt");
        File.WriteAllText(logPath, "[ERR] {\"Stage\":\"TaskLoop.Failure\",\"Exception\":{\"Message\":\"AuthFailure\"},\"Details\":{\"CurrentTaskIndex\":1}}");
        var logFailure = service.Evaluate(session);
        Assert(logFailure.State == CodingRunState.Failed && logFailure.FailureCategory == "Authentifizierung / Key", "Starker neuer RFiDGear-Logfehler hat keinen Vorrang vor bytegleichen Readbacks.");
        File.Delete(logPath);
        File.WriteAllText(verificationPath, "00 00 00 00 00 00 00 00 00 00");
        File.SetLastWriteTimeUtc(verificationPath, DateTime.UtcNow.AddSeconds(3));
        Assert(service.Evaluate(session).State == CodingRunState.Failed, "Abweichender Readback wurde nicht als Fehler bewertet.");
        var invalidated = service.InvalidatePreparedPayloads(session, "Testabschluss");
        Assert(invalidated.RemovedPayloads == 1 && !File.Exists(payloadPath) && invalidated.State.State == "NotPrepared" && !invalidated.State.ContainsCardData,
            "Abgeschlossene Sitzung entfernt oder sperrt die Kartendaten nicht.");
        Assert(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Temporäre Sitzungsdatei blieb zurück.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestAcceptanceReport()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-acceptance-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "verification"));
    try
    {
        File.WriteAllText(Path.Combine(root, "acceptance.plan.json"), """
        {
          "schemaVersion":"1.0","projectName":"AcceptanceDemo","verificationDirectory":"verification",
          "applicationAcceptanceProject":"AcceptanceDemo_Acceptance_Application_Productive.rfPrj","applicationAcceptanceEvidence":"acceptance/application-settings.result.json",
          "applications":[{
            "aid":"F00001","name":"Test","keyType":"DF_KEY_AES","keyCount":1,"keySettings":"0B","changeKeyMode":"ChangeKeyUsingMK",
            "files":[{"id":0,"name":"Data","type":"StdDataFile","size":4,"communication":"CM_ENCRYPT","access":{"read":0,"write":0,"readWrite":0,"change":0},"accessText":"R0/W0/RW0/C0","codingOutput":"data.hex"}]
          }]
        }
        """);
        var timestamp = DateTime.Now.AddMinutes(-1);
        var verificationPath = Path.Combine(root, "verification", "data.hex");
        File.WriteAllText(verificationPath, "01 23 45 67");
        File.SetLastWriteTime(verificationPath, DateTime.Now);
        File.WriteAllText(Path.Combine(root, "Letzte_Kodierung.json"), JsonSerializer.Serialize(new
        {
            timestamp = timestamp.ToString("s"),
            payloads = new[] { new { aid = "F00001", fileId = 0, expectedHex = "01234567", verificationPath } }
        }, ProjectJson.Options));
        var service = new AcceptanceReportService();
        var accepted = service.Create(root, now: new DateTime(2026, 8, 13, 12, 0, 0));
        Assert(accepted.Report.Result == "OK im automatisch prüfbaren Umfang" && accepted.Report.Summary.Ok == 3 && accepted.Report.Summary.Deviation == 0 && accepted.Report.Summary.NotVerifiable == 8, "Bytegleicher Abnahmenachweis wird falsch bewertet.");
        Assert(accepted.Report.Coverage == new AcceptanceCoverage(1, 2, 8, 11), "Bytegenaue, operative und fehlende Istnachweise werden nicht korrekt getrennt.");
        Assert(accepted.Report.Limitations.Any(value => value.Contains("SHA-256", StringComparison.Ordinal)), "Korrelation der Application-Abnahmeevidenz wird nicht sichtbar erklärt.");
        Assert(File.Exists(accepted.JsonPath) && File.Exists(accepted.TextPath), "TXT-/JSON-Abnahmebericht wurde nicht erzeugt.");
        Assert(File.ReadAllText(accepted.TextPath).Contains("Nachweisabdeckung: bytegenau 1 | operativ 2 | ohne Istwert 8", StringComparison.Ordinal) && File.ReadAllText(accepted.JsonPath).Contains("\"evidenceLevel\": \"Operativ\"", StringComparison.Ordinal), "Abnahmeberichte enthalten nicht die erwartete Nachweisabdeckung.");

        var appProject = RfPrjDocument.Create();
        appProject.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "10"), new XElement("SelectedTaskType", "ReadAppSettings"), new XElement("SelectedTaskDescription", "settings"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF00001")));
        appProject.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "20"), new XElement("SelectedTaskType", "CheckAppKeyCount"), new XElement("SelectedTaskDescription", "count"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF00001")));
        appProject.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "30"), new XElement("SelectedTaskType", "CheckFileSettings"), new XElement("SelectedTaskDescription", "file settings"), new XElement("SelectedExecuteConditionErrorLevel", "Empty"), new XElement("AppNumberCurrent", "0xF00001"), new XElement("FileNumberCurrent", "0"), new XElement("FileSizeCurrent", "4")));
        var appProjectPath = Path.Combine(root, "AcceptanceDemo_Acceptance_Application_Productive.rfPrj");
        RfPrjArchive.Save(appProjectPath, appProject);
        var appOperation = new RFiDGearOperationArtifact(RFiDGearOperationKind.ApplicationAcceptanceProductive, "Abnahme", appProjectPath, true, false, true, appProject.Summarize());
        var appLog = "[INF] {\"Stage\":\"Task.Outcome\",\"RunId\":\"acceptance-demo\",\"TaskExecutionIndex\":0,\"Outcome\":\"Successful\",\"ErrorCode\":\"NoError\"}\n[INF] {\"Stage\":\"Task.Outcome\",\"RunId\":\"acceptance-demo\",\"TaskExecutionIndex\":1,\"Outcome\":\"Successful\",\"ErrorCode\":\"NoError\"}\n[INF] {\"Stage\":\"Task.Outcome\",\"RunId\":\"acceptance-demo\",\"TaskExecutionIndex\":2,\"Outcome\":\"Successful\",\"ErrorCode\":\"NoError\"}\n[INF] {\"Stage\":\"TaskLoop.Success\",\"RunId\":\"acceptance-demo\"}";
        var appCompleted = CompleteApplicationHost(root, appOperation, appLog, "successful");
        new ApplicationAcceptanceEvidenceService().Create(root, appOperation, appCompleted.Run, appCompleted.Result);
        var withApplicationEvidence = service.Create(root, reportPrefix: "WithAppEvidence", now: new DateTime(2026, 8, 13, 12, 0, 0));
        Assert(withApplicationEvidence.Report.Summary.Ok == 10 && withApplicationEvidence.Report.Summary.NotVerifiable == 1 &&
               withApplicationEvidence.Report.Checks.Any(check => check.Subject == "F00001 – KeySettings" && check.Status == "OK") &&
               withApplicationEvidence.Report.Checks.Any(check => check.Subject == "F00001 – Keyanzahl" && check.Status == "OK") &&
               withApplicationEvidence.Report.Checks.Count(check => check.Area == "File-Metadaten" && check.Status == "OK") == 4,
            "Autoritative RFiDGear-Application-/FileSettings-Evidenz wird im Abnahmebericht nicht ausgewertet.");
        Directory.Delete(Path.Combine(root, "acceptance"), true);

        File.WriteAllText(verificationPath, "89 AB CD EF");
        File.SetLastWriteTime(verificationPath, DateTime.Now.AddSeconds(1));
        var deviation = service.Create(root, reportPrefix: "Deviation", now: new DateTime(2026, 8, 13, 12, 0, 1));
        Assert(deviation.Report.Result == "Abweichung" && deviation.Report.Summary.Deviation == 1 && deviation.Report.Summary.Ok == 0, "Abweichender finaler Readback wird nicht als Abweichung bewertet.");

        File.SetLastWriteTime(verificationPath, timestamp.AddMinutes(-1));
        var stale = service.Create(root, reportPrefix: "Stale", now: new DateTime(2026, 8, 13, 12, 0, 2));
        Assert(stale.Report.Result == "Nicht prüfbar" && stale.Report.Checks.Any(check => check.Detail.Contains("älter", StringComparison.Ordinal)), "Alter Readback wird fälschlich als aktueller Nachweis verwendet.");
        Assert(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Temporäre Abnahmeberichtdatei blieb zurück.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-core-tests-" + Guid.NewGuid().ToString("N"));
    var path = Path.Combine(directory, "Test.project.json");
    try
    {
        ProjectJson.Save(path, ValidProject());
        var loaded = ProjectJson.Load(path);
        Assert(loaded.Project.Name == "Test", "Gespeichertes Projekt konnte nicht gelesen werden.");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Speicherdatei blieb zurück.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void TestConcurrentPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-concurrency-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var projectPath = Path.Combine(directory, "Shared.project.json");
        ProjectJson.Save(projectPath, ValidProject());
        var opened = ConcurrentFileWriter.Capture(projectPath);
        var foreign = ValidProject(); foreign.Project.Name = "Fremdänderung";
        ProjectJson.Save(projectPath, foreign);
        var local = ValidProject(); local.Project.Name = "Lokale Änderung";
        AssertThrows<ConcurrentFileChangeException>(() => ProjectJson.Save(projectPath, local, opened));
        Assert(ProjectJson.Load(projectPath).Project.Name == "Fremdänderung", "Fremde Projektfassung wurde trotz Konflikt überschrieben.");

        var fresh = ConcurrentFileWriter.Capture(projectPath);
        var saved = ProjectJson.Save(projectPath, local, fresh);
        Assert(saved == ConcurrentFileWriter.Capture(projectPath) && ProjectJson.Load(projectPath).Project.Name == "Lokale Änderung", "Konfliktfreies Projektspeichern scheiterte.");

        var vaultPath = ProjectKeyVault.GetPath(projectPath);
        var payload = ProjectKeyVault.FromProject(ValidProject());
        var vaultRevision = ProjectKeyVault.Save(vaultPath, payload, "Passphrase-1-mit-20-Zeichen", FileRevision.Missing);
        ProjectKeyVault.Save(vaultPath, payload, "Passphrase-2-mit-20-Zeichen");
        AssertThrows<ConcurrentFileChangeException>(() => ProjectKeyVault.Save(vaultPath, payload, "Passphrase-3-mit-20-Zeichen", vaultRevision));
        _ = ProjectKeyVault.Load(vaultPath, "Passphrase-2-mit-20-Zeichen");
        Assert(Directory.EnumerateFiles(directory, "*.rfidgear-builder.lock").Count() == 2, "Rechnerübergreifende Lockdateien fehlen.");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Konfliktdatei blieb zurück.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestWorkspaceLease()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-workspace-lease-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        using (var first = WorkspaceLease.Acquire(directory, "Testlauf"))
        {
            Assert(first.IsHeld && File.Exists(Path.Combine(directory, WorkspaceLease.FileName)), "Arbeitsbereichs-Lease oder persistente Lockdatei fehlt.");
            AssertThrows<IOException>(() => WorkspaceLease.Acquire(directory, "Parallellauf"));
        }
        using var recovered = WorkspaceLease.Acquire(directory, "Wiederanlauf");
        Assert(recovered.IsHeld, "Arbeitsbereich blieb nach Freigabe der Prozesssperre blockiert.");
        recovered.Dispose();
        Assert(File.Exists(Path.Combine(directory, WorkspaceLease.FileName)) && new FileInfo(Path.Combine(directory, WorkspaceLease.FileName)).Length == 0,
            "Datenfreie persistente Lockdatei fehlt oder enthält Daten.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestLegacyMigration()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-legacy-migration-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var cardVariable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
        var baseProfile = MinimalProfile("Base", "base", "ABCDEF", cardVariable);
        var customer = MinimalProfile("Customer", "customer", "123456", new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true });
        customer.Variables.Add(new VariableDefinition { Id = "firmCode", Label = "Firmencode", Scope = "project", Type = "digits", Length = 4, Required = true });
        var library = ProfileLibrary.FromProfiles([baseProfile, customer]);
        var project = ProjectDraftFactory.Create(library, "LegacyBuilt");
        project.Profiles.Customers = ["Customer"];
        project.Project.Build = ["Mastering", "Coding"];
        project.Values["firmCode"] = "1234";
        ProjectDraftFactory.Synchronize(project, library);
        var sourcePath = Path.Combine(root, "LegacyBuilt.project.json");
        ProjectJson.Save(sourcePath, project);
        var composition = library.Compose("Base", ["Customer"]);
        ContractArtifactFactory.SaveAll(root, ContractArtifactFactory.Build(new ContractArtifactContext(project, composition, sourcePath, "test", new DateTime(2026, 8, 13, 12, 0, 0))));
        var definitionPath = Path.Combine(root, "project.definition.json");
        var beforeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(definitionPath)));

        var service = new LegacyProjectMigrationService();
        var migrated = service.Import(definitionPath, library);
        Assert(migrated.SourceKind == "Gebautes Projekt" && migrated.Project.Project.Name == "LegacyBuilt_migriert", "Gebautes Projekt wird nicht eindeutig als neuer Migrationsentwurf benannt.");
        Assert(migrated.Project.Project.Build.SequenceEqual(["Mastering"]) && migrated.Project.Project.OutputDirectory == ".", "Migration ist nicht auf einen sicheren neuen Mastering-Entwurf begrenzt.");
        Assert(migrated.Project.Profiles.Base == "Base" && migrated.Project.Profiles.Customers.SequenceEqual(["Customer"]), "Profilkomposition wurde aus dem Bauartefakt falsch übernommen.");
        Assert(migrated.Project.Values["firmCode"] == "1234" && migrated.ImportedValues.Contains("Firmencode"), "Keyfreier Projektwert aus coding.schema.json wurde nicht übernommen.");
        Assert(string.IsNullOrEmpty(migrated.Project.Productive.PiccKey.Value) && migrated.Project.Productive.ApplicationKeys.Values.SelectMany(keys => keys).All(string.IsNullOrEmpty), "Migration hat produktive Schlüsselwerte materialisiert.");
        Assert(beforeHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(definitionPath))), "Migration hat das bestehende Bauartefakt verändert.");

        var changedCustomer = new ProfileEditingService().Clone(customer);
        changedCustomer.Applications[0].Files[0].Size = 7;
        var changedLibrary = ProfileLibrary.FromProfiles([baseProfile, changedCustomer]);
        AssertThrows<InvalidDataException>(() => service.Import(definitionPath, changedLibrary));
        var wrongBase = MinimalProfile("OtherBase", "base", "FEDCBA", cardVariable);
        AssertThrows<InvalidDataException>(() => service.Import(definitionPath, ProfileLibrary.FromProfiles([wrongBase, customer])));

        var oldPath = Path.Combine(root, "old-format.json");
        File.WriteAllText(oldPath, """
        { "ProjectName":"Alt Kunde", "Customer":"Customer", "DevkFirmCode":"4321", "ProductiveKey":"00112233445566778899AABBCCDDEEFF" }
        """);
        var old = service.Import(oldPath, library);
        Assert(old.SourceKind == "Alte JSON-Konfiguration" && old.Project.Project.Name == "Alt_Kunde_migriert", "Altes JSON-Format wird nicht als neuer Entwurf importiert.");
        Assert(old.Project.Values["firmCode"] == "4321" && old.Project.Productive.ApplicationKeys.Values.SelectMany(keys => keys).All(string.IsNullOrEmpty), "Legacy-Fachwert oder Key-Ausschluss ist falsch.");
        AssertThrows<InvalidDataException>(() => service.Import(sourcePath, library));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestProfileComposition()
{
    var variable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
    var baseProfile = MinimalProfile("Base", "base", "ABCDEF", variable);
    var customer = MinimalProfile("Customer", "customer", "123456", variable);
    var library = ProfileLibrary.FromProfiles([baseProfile, customer]);
    Assert(library.Validate().Count == 0, "Gültige synthetische Profile wurden abgelehnt.");
    var composed = library.Compose("Base", ["Customer"]);
    Assert(composed.Profiles.Count == 2 && composed.Applications.Count == 2 && composed.Variables.Count == 1, "Additive Komposition ist falsch.");

    customer.Applications[0].Id = "ABCDEF";
    AssertThrows<InvalidDataException>(() => library.Compose("Base", ["Customer"]));
    customer.Applications[0].Id = "123456";
    customer.Variables[0] = new VariableDefinition { Id = "cardNumber", Label = "Andere Definition", Scope = "card", Type = "digits", Length = 8, Required = true };
    AssertThrows<InvalidDataException>(() => library.Compose("Base", ["Customer"]));
}

static void TestProfileEditing()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-profile-edit-tests-" + Guid.NewGuid().ToString("N"));
    var bundled = Path.Combine(root, "bundled");
    var user = Path.Combine(root, "user");
    Directory.CreateDirectory(bundled);
    try
    {
        var variable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
        var baseProfile = MinimalProfile("Base", "base", "ABCDEF", variable);
        var customer = MinimalProfile("Customer", "customer", "123456", new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true });
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        File.WriteAllText(Path.Combine(bundled, "base.json"), JsonSerializer.Serialize(baseProfile, jsonOptions));
        File.WriteAllText(Path.Combine(bundled, "customer.json"), JsonSerializer.Serialize(customer, jsonOptions));

        var store = new ProfileStore(bundled, user);
        var editing = new ProfileEditingService();
        var working = editing.Clone(store.Load().Get("Customer"));
        editing.RenameVariable(working, "cardNumber", "badgeNumber");
        Assert(working.Applications[0].Files[0].Coding!.Recipe[0].Source == "badgeNumber", "Variablenumbenennung aktualisiert Rezeptreferenz nicht.");
        AssertThrows<InvalidDataException>(() => editing.DeleteVariable(working, "badgeNumber"));
        working.Variables[0].Label = "Ausweisnummer";
        working.Applications[0].Files[0].Coding!.Recipe.Clear();
        working.Applications[0].Files[0].Coding = new CodingDefinition { Output = "constant.hex", Recipe = [new RecipeStep { Op = "fixedHex", Value = "000000000000" }] };
        working.Profile.Version = "1.1";
        var saved = store.SaveCustomer(working);
        Assert(File.Exists(saved) && store.Load().Get("Customer").Variables[0].Id == "badgeNumber", "Dauerhaftes Kundenprofil wurde nicht als Benutzer-Override geladen.");
        Assert(!Directory.EnumerateFiles(user, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Profildatei blieb zurück.");

        var openedRevision = ConcurrentFileWriter.Capture(saved);
        var staleEditor = editing.Clone(store.Load().Get("Customer"));
        var foreignEditor = editing.Clone(store.Load().Get("Customer"));
        foreignEditor.Profile.Description = "Fremdänderung"; foreignEditor.Profile.Version = "1.2";
        store.SaveCustomer(foreignEditor);
        var historyBeforeConflict = store.ListHistory("Customer").Count;
        staleEditor.Profile.Description = "Lokale Änderung"; staleEditor.Profile.Version = "1.3";
        AssertThrows<ConcurrentFileChangeException>(() => store.SaveCustomer(staleEditor, openedRevision));
        var preserved = store.Load().Get("Customer");
        Assert(preserved.Profile.Version == "1.2" && preserved.Profile.Description == "Fremdänderung", "Fremder Profilstand wurde trotz Konflikt überschrieben.");
        Assert(store.ListHistory("Customer").Count == historyBeforeConflict, "Der abgewiesene Profilkonflikt hat die Historienkette verändert.");

        working.Applications[0].Files[0].Access.Change = 1;
        AssertThrows<InvalidDataException>(() => editing.ChangeKeyCount(working.Applications[0], 1));
        Assert(ProfileEditingService.KeySettingsFromFlags(true, true, false, true) == "0B", "KeySettings-Bitmaske ist falsch.");
        var flags = ProfileEditingService.KeySettingsFlags("0B");
        Assert(flags.ChangeMasterKey && flags.ListApplications && !flags.CreateDelete && flags.ConfigurationChange, "KeySettings-Bitmaske wird falsch gelesen.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestCodingSpecificationAndDuplication()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-coding-spec-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var profile = MinimalProfile("Customer", "customer", "123456", new VariableDefinition
        {
            Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true
        });
        profile.Profile.Description = "Keywertfreie Prüfvorschrift";
        profile.Picc = new PiccProfileSettings { ProductiveKeySettings = "0B" };
        profile.Applications[0].TransportKeySettings = "0F";
        profile.Applications[0].ProductiveKeySettings = "0B";
        profile.Applications[0].Files[0].CreationSettings = new FileSettingsDefinition
        {
            Communication = "CM_PLAIN",
            Access = new AccessDefinition()
        };
        profile.Applications[0].Files[0].MasteringInitialization = new MasteringInitialization { Type = "zeroFill" };

        var service = new CodingSpecificationService();
        var text = service.BuildText(profile);
        Assert(text.Contains("PICC Produktiv-KeySettings: 0B", StringComparison.Ordinal) &&
               text.Contains("Mastering temporär = CM_PLAIN", StringComparison.Ordinal) &&
               text.Contains("ASCII(cardNumber, Breite 6, links '0')", StringComparison.Ordinal),
            "Kodiervorschrift verliert Security-, Mastering- oder Rezeptinformationen.");
        Assert(!text.Contains("productivePicc", StringComparison.OrdinalIgnoreCase) && !text.Contains("keyValue", StringComparison.OrdinalIgnoreCase),
            "Kodiervorschrift enthält ein Schlüsselwertfeld.");
        var output = service.Export(profile, Path.Combine(root, "Customer_Kodiervorschrift.txt"));
        var bytes = File.ReadAllBytes(output);
        Assert(bytes.Length > 3 && !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "Kodiervorschrift enthält einen UTF-8-BOM.");
        Assert(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Exportdatei blieb zurück.");

        var editing = new ProfileEditingService();
        var clone = editing.CreateCloneDraft(profile, ["Customer", "Customer-Kopie"]);
        Assert(clone.Profile.Id == "Customer-Kopie-2" && clone.Profile.Code == "Customer-KOPIE" && clone.Profile.Version == "1.0" &&
               clone.Profile.CreatedAt is null && clone.Profile.ModifiedAt is null && clone.Profile.PreviousVersion is null,
            "Duplikat besitzt keine neue Identität oder übernimmt Historienmetadaten.");
        clone.Applications[0].Files[0].Name = "Geändert";
        Assert(profile.Applications[0].Files[0].Name == "Data", "Duplikat teilt veränderliche Profildaten mit dem Original.");
        AssertThrows<InvalidDataException>(() => editing.CreateCloneDraft(MinimalProfile("Base", "base", "ABCDEF", profile.Variables[0]), []));
    }
    finally { Directory.Delete(root, true); }
}

static void TestProfileDiff()
{
    var variable = new VariableDefinition { Id = "cardNumber", Label = "Kartennummer", Scope = "card", Type = "digits", Length = 6, Required = true };
    var leftProfile = MinimalProfile("Base", "base", "ABCDEF", variable);
    var rightProfile = new ProfileEditingService().Clone(leftProfile);
    rightProfile.Profile.Version = "2.0";
    rightProfile.Applications[0].Files[0].Size = 8;
    rightProfile.Applications[0].Files[0].Access.Write = 1;
    rightProfile.Applications[0].Files[0].Coding!.Recipe[0].Width = 8;
    var left = new ComposedProfiles([leftProfile], leftProfile.Applications, leftProfile.Variables);
    var right = new ComposedProfiles([rightProfile], rightProfile.Applications, rightProfile.Variables);
    var service = new ProfileDiffService();
    var report = service.Compare(left, right, "Links", "Rechts", new DateTime(2026, 8, 13, 13, 0, 0));
    Assert(report.Changed == 4 && report.Added == 0 && report.Removed == 0, "Profilvergleich erkennt Version, Größe, Rechte und Rezept nicht exakt.");
    Assert(report.Items.Any(item => item.Property == "Access Rights" && item.Left.Contains("W0") && item.Right.Contains("W1")), "Access-Rights-Diff ist nicht verständlich formatiert.");
    Assert(report.Items.Any(item => item.Property == "Kodierrezept" && item.Impact == "Kodierdaten"), "Kodierrezeptänderung fehlt oder ist falsch klassifiziert.");
    Assert(service.Compare(left, left, "A", "B").Identical, "Identische Profilkomposition wird als abweichend gemeldet.");

    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-profile-diff-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var files = service.Save(report, directory);
        var corpus = File.ReadAllText(files.TextPath) + File.ReadAllText(files.JsonPath);
        Assert(corpus.Contains("\"containsKeys\": false", StringComparison.Ordinal) && corpus.Contains("RFiDGear Projekt-/Profilvergleich", StringComparison.Ordinal), "Vergleichsberichte sind unvollständig oder nicht explizit keyfrei.");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp-*", SearchOption.AllDirectories).Any(), "Temporäre Profilvergleichsdatei blieb zurück.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void TestFileSettingsExplanation()
{
    var file = new FileDefinition
    {
        Id = 2, Name = "Protected", Type = "StdDataFile", Size = 32, Communication = "CM_ENCRYPT",
        Access = new AccessDefinition { Read = 1, Write = 3, ReadWrite = 3, Change = 0 },
        CreationSettings = new FileSettingsDefinition { Communication = "CM_PLAIN", Access = new AccessDefinition { Read = 0, Write = 0, ReadWrite = 0, Change = 0 } }
    };
    var explanation = FileSettingsExplanationService.Describe(file);
    Assert(explanation.IsTwoStage && !explanation.IsRedundant, "Zweistufige FileSettings werden nicht erkannt.");
    Assert(explanation.InitialTechnical == "CM_PLAIN · R0 / W0 / RW0 / C0" && explanation.FinalTechnical == "CM_ENCRYPT · R1 / W3 / RW3 / C0", "Technische FileSettings-Zusammenfassung ist falsch.");
    Assert(explanation.Transition.Contains("Communication CM_PLAIN → CM_ENCRYPT", StringComparison.Ordinal) && explanation.Transition.Contains("Schreiben Key 0 → Key 3", StringComparison.Ordinal), "FileSettings-Übergang erklärt nicht die konkreten Änderungen.");
    Assert(explanation.MasteringSequence.Contains("Zero-Fill + Readback", StringComparison.Ordinal) && explanation.MasteringSequence.Contains("ChangeFileSettings", StringComparison.Ordinal), "Mastering-Reihenfolge ist unvollständig.");
    Assert(FileSettingsExplanationService.UsesZeroFill(file), "Fehlende masteringInitialization muss als Standard-Zero-Fill gelten.");
    FileSettingsExplanationService.SetZeroFill(file, false);
    Assert(!FileSettingsExplanationService.UsesZeroFill(file) && file.MasteringInitialization?.Type == "none", "Abgewählter Zero-Fill wird nicht explizit als none gespeichert.");
    FileSettingsExplanationService.SetZeroFill(file, true);
    Assert(FileSettingsExplanationService.UsesZeroFill(file) && file.MasteringInitialization is null, "Aktivierter Standard-Zero-Fill wird nicht kanonisch gespeichert.");

    file.CreationSettings = null;
    var direct = FileSettingsExplanationService.Describe(file);
    Assert(!direct.IsTwoStage && !direct.IsRedundant && direct.MasteringSequence.Contains("kein ChangeFileSettings", StringComparison.Ordinal), "Direkter Endzustand wird falsch erklärt.");
    file.CreationSettings = new FileSettingsDefinition { Communication = file.Communication, Access = new AccessDefinition { Read = 1, Write = 3, ReadWrite = 3, Change = 0 } };
    Assert(FileSettingsExplanationService.Describe(file).IsRedundant, "Redundante Creation Settings werden nicht sichtbar markiert.");
}

static void TestPayloadRecipes()
{
    var file = new FileDefinition
    {
        Id = 0, Size = 16,
        Coding = new CodingDefinition
        {
            Output = "payload.hex",
            Recipe =
            [
                new RecipeStep { Op = "fixedHex", Value = "01 02" },
                new RecipeStep { Op = "asciiDigits", Source = "number", Width = 6, PadLeft = "0" },
                new RecipeStep { Op = "bcdDigits", Source = "code", Width = 4, PadLeft = "0" },
                new RecipeStep { Op = "zeroPadToFileSize" }
            ]
        }
    };
    var payload = PayloadEngine.Build(file, new Dictionary<string, string> { ["number"] = "123", ["code"] = "45" }, "ABCDEF");
    Assert(Convert.ToHexString(payload) == "01023030303132330045000000000000", "Payload-Rezept liefert falsche Bytes.");
    Assert(PayloadEngine.ToHexFileText(payload) == "01 02 30 30 30 31 32 33 00 45 00 00 00 00 00 00", "Hex-Dateiformat weicht ab.");
    AssertThrows<InvalidDataException>(() => PayloadEngine.Build(file, new Dictionary<string, string> { ["number"] = "1234567", ["code"] = "45" }, "ABCDEF"));

    var modular = new FileDefinition
    {
        Id = 1, Size = 32,
        Coding = new CodingDefinition
        {
            Output = "modular.hex",
            Recipe =
            [
                new RecipeStep { Op = "fixedAscii", Value = "OSS" },
                new RecipeStep { Op = "fixedUtf8", Value = "Ä" },
                new RecipeStep { Op = "hexBytes", Source = "raw", Width = 4, PadLeft = "00" },
                new RecipeStep { Op = "asciiText", Source = "label", Width = 6, PadRight = "20" },
                new RecipeStep { Op = "utf8Text", Source = "unicode", Width = 5, PadRight = "00" },
                new RecipeStep { Op = "uintBigEndian", Source = "number", Width = 2 },
                new RecipeStep { Op = "uintLittleEndian", Source = "number", Width = 4 },
                new RecipeStep { Op = "padToFileSize", Value = "FF" }
            ]
        }
    };
    var modularBytes = PayloadEngine.Build(modular, new Dictionary<string, string>
        { ["raw"] = "A1B2C3", ["label"] = "AB", ["unicode"] = "ÄÖ", ["number"] = "513" }, "ABCDEF");
    Assert(Convert.ToHexString(modularBytes) == "4F5353C38400A1B2C3414220202020C384C39600020101020000FFFFFFFFFFFF", "Modulares Mischformat liefert nicht die erwartete Bytefolge.");
    AssertThrows<InvalidDataException>(() => PayloadEngine.Build(modular, new Dictionary<string, string>
        { ["raw"] = "123", ["label"] = "AB", ["unicode"] = "ÄÖ", ["number"] = "513" }, "ABCDEF"));
}

static void TestSyntheticCompatibilityMatrix()
{
    var directory = Path.Combine(Path.GetTempPath(), "sieencode-matrix-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var shellDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "task-shells"));
        var result = new CompatibilityMatrixService().Run(TaskShellLibrary.Load(shellDirectory), directory, "test-matrix");
        Assert(result.Report.Successful && result.Report.Scenarios.Count == 5, "Synthetische Kompatibilitätsmatrix ist nicht vollständig erfolgreich: " + string.Join(" | ", result.Report.Scenarios.Where(item => item.Status != "OK").Select(item => item.Id + ": " + item.Error)));
        var dimensions = result.Report.Scenarios.SelectMany(item => item.KeyTypes).Distinct().ToArray();
        Assert(dimensions.Contains("DF_KEY_DES") && dimensions.Contains("DF_KEY_AES") && dimensions.Contains("DF_KEY_3K3DES"), "Kompatibilitätsmatrix deckt nicht alle unterstützten Schlüsseltypen ab.");
        Assert(result.Report.Scenarios.SelectMany(item => item.FileTypes).Distinct().Count() == 2 && result.Report.Scenarios.SelectMany(item => item.Communications).Distinct().Count() == 3,
            "Kompatibilitätsmatrix deckt Datei- oder Kommunikationsarten nicht vollständig ab.");
        Assert(result.Report.Scenarios.SelectMany(item => item.RecipeOperations).Distinct().Count() == 12,
            "Kompatibilitätsmatrix deckt Payloadoperationen nicht vollständig ab.");
        Assert(File.Exists(result.JsonPath) && File.Exists(result.TextPath) && !File.ReadAllText(result.TextPath).Contains("Hardwarestatus: bestanden", StringComparison.OrdinalIgnoreCase),
            "Kompatibilitätsbericht fehlt oder behauptet unzulässige Hardwareevidenz.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void TestDependencyGraph()
{
    var valid = new[]
    {
        new RfPrjTaskSummary(0, "10", "ReadData", "Root 1", "", "Empty", "0", "0"),
        new RfPrjTaskSummary(1, "20", "ReadData", "Root 2", "", "Empty", "0", "0"),
        new RfPrjTaskSummary(2, "30", "ReadData", "Child", "10", "NoError", "0", "0")
    };
    Assert(ProjectPreflight.ValidateDependencies(valid).Count == 0, "Mehrere gültige Taskwurzeln wurden abgelehnt.");
    var invalid = valid.Append(new RfPrjTaskSummary(3, "40", "ReadData", "Broken", "99", "NoError", "0", "0")).ToArray();
    Assert(ProjectPreflight.ValidateDependencies(invalid).Any(issue => issue.Code == "TASK_DEPENDENCY"), "Vorwärts-/Fremdverweis wurde nicht erkannt.");
}

static void TestRfPrjProfileImportSafety()
{
    const string secret = "00112233445566778899AABBCCDDEEFF";
    var document = RfPrjDocument.Create();
    var createApplication = new XElement("anyType");
    TaskXml.Set(createApplication, "SelectedTaskType", "CreateApplication");
    TaskXml.SetMetadata(createApplication, 10, "Importkunde: Application 123456 erstellen " + secret);
    TaskXml.Set(createApplication, "AppNumberNew", "0x123456");
    TaskXml.Set(createApplication, "SelectedDesfireAppKeyEncryptionTypeCreateNewApp", "DF_KEY_AES");
    TaskXml.Set(createApplication, "SelectedDesfireAppKeyEncryptionTypeCurrent", "DF_KEY_AES");
    TaskXml.Set(createApplication, "SelectedDesfireAppMaxNumberOfKeys", "2");
    TaskXml.Set(createApplication, "SelectedDesfireAppKeySettingsCreateNewApp", "ChangeKeyUsingMK");
    TaskXml.Set(createApplication, "IsAllowChangeMKChecked", "true");
    TaskXml.Set(createApplication, "IsAllowListingWithoutMKChecked", "true");
    TaskXml.Set(createApplication, "IsAllowCreateDelWithoutMKChecked", "false");
    TaskXml.Set(createApplication, "IsAllowConfigChangableChecked", "true");
    TaskXml.Set(createApplication, "DesfireAppKeyCurrent", secret);
    document.Append(createApplication);
    foreach (var (index, size) in new[] { (20, 6), (30, 7) })
    {
        var file = new XElement("anyType");
        TaskXml.Set(file, "SelectedTaskType", "CreateFile");
        TaskXml.SetMetadata(file, index, $"Importkunde: File 0 'Daten' erstellen");
        TaskXml.Set(file, "AppNumberCurrent", "0x123456");
        TaskXml.Set(file, "FileNumberCurrent", "0");
        TaskXml.Set(file, "FileSizeCurrent", size);
        TaskXml.Set(file, "SelectedDesfireFileType", "StdDataFile");
        TaskXml.Set(file, "SelectedDesfireFileCryptoMode", "CM_ENCRYPT");
        TaskXml.Set(file, "SelectedDesfireFileAccessRightRead", "AR_KEY0");
        TaskXml.Set(file, "SelectedDesfireFileAccessRightWrite", "AR_KEY1");
        TaskXml.Set(file, "SelectedDesfireFileAccessRightReadWrite", "AR_KEY1");
        TaskXml.Set(file, "SelectedDesfireFileAccessRightChange", "AR_KEY0");
        TaskXml.Set(file, "SelectedDesfireAppKeyEncryptionTypeCurrent", "DF_KEY_AES");
        document.Append(file);
    }
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-rfprj-import-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var path = Path.Combine(root, "conflict.rfPrj");
        RfPrjArchive.Save(path, document);
        var service = new RfPrjProfileImportService();
        var report = service.Analyze(path);
        Assert(report.Errors > 0 && !report.CanCreateProposal && report.Issues.Any(issue => issue.Code == "FILE_SIZE"), "Widersprüchliche Filegrößen sperren den Profilvorschlag nicht.");
        AssertThrows<InvalidDataException>(() => service.CreateProposal(report, ["123456"], "Import", "IMP", "Import", "1.0"));
        var files = service.SaveReport(report, Path.Combine(root, "report"));
        var corpus = File.ReadAllText(files.TextPath) + File.ReadAllText(files.JsonPath);
        Assert(!corpus.Contains(secret, StringComparison.Ordinal) && corpus.Contains("\"containsKeys\": false", StringComparison.Ordinal), "rfPrj-Importbericht enthält Schlüsselmaterial oder kennzeichnet sich nicht als keyfrei.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void TestLauncher()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-launch-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var executable = Path.Combine(directory, "RFiDGear.exe");
        var project = Path.Combine(directory, "Projekt mit Leerzeichen.rfPrj");
        File.WriteAllText(executable, "CUSTOMPROJECTFILE AUTORUN AUTOMODE"); File.WriteAllBytes(project, [0]);
        var starter = new FakeProcessStarter(new FakeRunningProcess(42, false, null));
        var launcher = new RFiDGearLauncher(starter, new ImmediateDelay());
        var result = launcher.LaunchAsync(new RFiDGearLaunchRequest(executable, project, true)).GetAwaiter().GetResult();
        Assert(result.ProcessId == 42 && result.Arguments == $"CUSTOMPROJECTFILE=\"{Path.GetFullPath(project)}\" AUTORUN=1" && result.Autorun && !result.AutoMode, "AUTORUN-Launcherargumente weichen vom Vertrag ab.");
        Assert(starter.StartInfo?.WorkingDirectory == directory && starter.StartInfo.UseShellExecute, "Launcher-Arbeitsverzeichnis oder Shellmodus ist falsch.");
        Assert(RFiDGearLauncher.BuildArguments(project, autorun: false, autoMode: true) == $"CUSTOMPROJECTFILE=\"{Path.GetFullPath(project)}\" AUTOMODE=1", "AUTOMODE-Launcherargumente weichen vom RFiDGear-2.0.7-Vertrag ab.");
        Assert(RFiDGearLauncher.RecommendedVersion == new Version(2, 0, 10, 0), "Empfohlene RFiDGear-Version ist nicht 2.0.10.0.");
        AssertThrows<ArgumentException>(() => RFiDGearLauncher.BuildArguments(project, autorun: true, autoMode: true));
        var failedLauncher = new RFiDGearLauncher(new FakeProcessStarter(new FakeRunningProcess(43, true, 7)), new ImmediateDelay());
        AssertThrows<InvalidOperationException>(() => failedLauncher.LaunchAsync(new RFiDGearLaunchRequest(executable, project)).GetAwaiter().GetResult());
        AssertThrows<ArgumentException>(() => RFiDGearLauncher.BuildArguments("bad\"path.rfPrj", false));
    }
    finally { Directory.Delete(directory, true); }
}

static void TestCodingQuickStart()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-quick-start-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var executable = Path.Combine(directory, "RFiDGear.exe");
        var project = Path.Combine(directory, "Demo_Kodierung.rfPrj");
        File.WriteAllText(executable, "CUSTOMPROJECTFILE AUTORUN AUTOMODE");
        File.WriteAllBytes(project, [0]);
        var profile = MinimalProfile("Base", "base", "ABCDEF", new VariableDefinition { Id = "number", Label = "Number", Scope = "card", Type = "digits", Length = 6, Required = true });
        var definition = new CodingSessionDefinition("Demo", directory, project, "verification", [], 1,
            new ComposedProfiles([profile], profile.Applications, profile.Variables), new Dictionary<string, string>());
        var payloadPath = Path.Combine(directory, "payloads", "ABCDEF.hex");
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        File.WriteAllText(payloadPath, "DE AD BE EF");
        var starter = new FakeProcessStarter(new FakeRunningProcess(77, false, null));
        var service = new CodingStationQuickStartService(new RFiDGearLauncher(starter, new ImmediateDelay()));
        var result = service.StartAsync(new CodingStationQuickStartRequest(executable, definition, "OK", false)).GetAwaiter().GetResult();
        Assert(result.Launch.ProcessId == 77 && !result.Launch.Autorun && !result.Launch.AutoMode,
            "Ein-Schritt-Kodiersession aktiviert einen automatischen Kartenlauf.");
        Assert(result.Launch.Arguments == $"CUSTOMPROJECTFILE=\"{Path.GetFullPath(project)}\"" && !result.Launch.Arguments.Contains("AUTO", StringComparison.Ordinal),
            "Quick-Start übergibt mehr als das konkrete Kodierprojekt.");
        Assert(result.SafetyState.Contains("noch keine neue Payload", StringComparison.OrdinalIgnoreCase) && result.SafetyState.Contains("kein Kartenlauf", StringComparison.Ordinal),
            "Quick-Start kennzeichnet den sicheren Zwischenzustand nicht.");
        Assert(result.Invalidation.RemovedPayloads == 1 && !File.Exists(payloadPath) && result.Invalidation.State.State == "NotPrepared" && !result.Invalidation.State.ContainsCardData,
            "Quick-Start hat eine alte Payloaddatei nicht fail-closed gesperrt.");
        Assert(File.Exists(result.Invalidation.StatePath) && !File.ReadAllText(result.Invalidation.StatePath).Contains("DEADBEEF", StringComparison.OrdinalIgnoreCase),
            "Payload-Sperrstatus fehlt oder enthält alte Kartendaten.");

        var blockedStarter = new FakeProcessStarter(new FakeRunningProcess(78, false, null));
        var blockedService = new CodingStationQuickStartService(new RFiDGearLauncher(blockedStarter, new ImmediateDelay()));
        AssertThrows<InvalidOperationException>(() => blockedService.StartAsync(new CodingStationQuickStartRequest(executable, definition, "ERROR", false)).GetAwaiter().GetResult());
        Assert(blockedStarter.StartInfo is null, "Fehlerhafter Preflight hat dennoch einen Prozess gestartet.");
        AssertThrows<InvalidOperationException>(() => blockedService.StartAsync(new CodingStationQuickStartRequest(executable, definition, "OK", true)).GetAwaiter().GetResult());
        Assert(blockedStarter.StartInfo is null, "Artifact-only Quick-Start hat dennoch einen Prozess gestartet.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestRFiDGearCapabilities()
{
    var highVersion = new RFiDGearVersionInfo("RFiDGear.exe", "99.0.0", new Version(99, 0), true);
    var highWithoutMarkers = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", highVersion, ["RFiDGear.dll"],
        token => token == "CUSTOMPROJECTFILE");
    Assert(highWithoutMarkers.Has(RFiDGearCapabilityId.CustomProjectFile), "CUSTOMPROJECTFILE-Marker wurde nicht erkannt.");
    Assert(!highWithoutMarkers.Has(RFiDGearCapabilityId.ContinuousAutoMode) && !highWithoutMarkers.Has(RFiDGearCapabilityId.StructuredTaskLogging),
        "Eine hohe Versionsnummer hat Fähigkeiten ohne Binärnachweis freigegeben.");

    var lowVersion = new RFiDGearVersionInfo("RFiDGear.exe", "1.0.0", new Version(1, 0), false);
    var evidence = new HashSet<string>(["CUSTOMPROJECTFILE", "AUTORUN", "AUTOMODE", "ChangeFileSettings", "ReadAppSettings", "IsAppSettingsExactMatch", "CheckAppKeyCount", "ApplicationFileInventory", "ApplicationInventoryOutputPath", "VerifyChangedApplicationKey", "VerifyMifareDesfireKeyChange", "ChangeKey reported", "Task.Outcome", "TaskLoop.Summary", "RunId", "AcceptedFailures", "UnhandledFailures", "InvalidSkips", "TerminalStatus"], StringComparer.Ordinal);
    var lowWithMarkers = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", lowVersion, ["RFiDGear.dll"], evidence.Contains);
    Assert(lowWithMarkers.Has(RFiDGearCapabilityId.AutorunOnce) && lowWithMarkers.Has(RFiDGearCapabilityId.ContinuousAutoMode) &&
           lowWithMarkers.Has(RFiDGearCapabilityId.ChangeFileSettings) && lowWithMarkers.Has(RFiDGearCapabilityId.StructuredTaskLogging) &&
           lowWithMarkers.Has(RFiDGearCapabilityId.ExactAppSettingsCheck) && lowWithMarkers.Has(RFiDGearCapabilityId.AppKeyCountCheck) &&
           !lowWithMarkers.Has(RFiDGearCapabilityId.VerifiedApplicationKeyChange) &&
           lowWithMarkers.Has(RFiDGearCapabilityId.AuthoritativeTaskLoopTerminalStatus) &&
           lowWithMarkers.Has(RFiDGearCapabilityId.ApplicationFileInventory) &&
           !lowWithMarkers.Has(RFiDGearCapabilityId.FileSettingsCheck),
        "Normale Markerfähigkeiten oder die zusätzliche Release-Sperre wurden falsch bewertet.");
    Assert(lowWithMarkers.Capabilities.Single(item => item.Id == RFiDGearCapabilityId.VerifiedApplicationKeyChange).Evidence.Contains("Release-Build", StringComparison.Ordinal),
        "Keywechsel-Marker ohne Releasefreigabe werden nicht nachvollziehbar fail-closed ausgewiesen.");

    var approvedWithMarkers = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", lowVersion, ["RFiDGear.dll"], evidence.Contains,
        verifiedKeyChangeReleaseApproved: true, verifiedKeyChangeReleaseEvidence: "Synthetischer Testbuild ist für diesen Test freigegeben.");
    Assert(approvedWithMarkers.Has(RFiDGearCapabilityId.VerifiedApplicationKeyChange),
        "Implementierung plus explizite exakte Releasefreigabe wurde nicht als Keywechsel-Fähigkeit erkannt.");

    var partialKeyVerification = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", lowVersion, ["RFiDGear.dll"],
        token => token is "VerifyChangedApplicationKey" or "VerifyMifareDesfireKeyChange");
    Assert(!partialKeyVerification.Has(RFiDGearCapabilityId.VerifiedApplicationKeyChange),
        "Ein nur teilweise implementierter Keywechsel-Vertrag wurde freigegeben.");

    var partialTerminalStatus = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", lowVersion, ["RFiDGear.dll"],
        token => token is "TerminalStatus" or "UnhandledFailures");
    Assert(!partialTerminalStatus.Has(RFiDGearCapabilityId.AuthoritativeTaskLoopTerminalStatus),
        "Ein nur teilweise implementierter TaskLoop-Terminalvertrag wurde freigegeben.");

    var keyTask = new RfPrjTaskSummary(0, "10", "ApplicationKeyChangeover", "test", "", "Empty", "0xF482D0", "");
    var readTask = new RfPrjTaskSummary(0, "10", "ReadData", "test", "", "Empty", "0xF482D0", "0");
    Assert(!RFiDGearKeyChangeSafety.Evaluate(highWithoutMarkers, [keyTask]).Allowed,
        "Keywechsel wurde ohne binär nachgewiesene Nachverifikation freigegeben.");
    Assert(!RFiDGearKeyChangeSafety.Evaluate(lowWithMarkers, [keyTask]).Allowed,
        "Keywechsel wurde allein aufgrund von Implementierungsmarkern freigegeben.");
    Assert(RFiDGearKeyChangeSafety.Evaluate(approvedWithMarkers, [keyTask]).Allowed,
        "Explizit nachgewiesene und releasefreigegebene Keywechsel-Nachverifikation wurde nicht freigegeben.");
    Assert(RFiDGearKeyChangeSafety.Evaluate(highWithoutMarkers, [readTask]).Allowed,
        "Reines Lesen wurde durch die Keywechsel-Sperre blockiert.");

    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-capability-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var executable = Path.Combine(directory, "RFiDGear.exe");
        File.WriteAllBytes(executable, [0]);
        File.WriteAllBytes(Path.Combine(directory, "RFiDGear.dll"), System.Text.Encoding.Unicode.GetBytes("CUSTOMPROJECTFILE AUTORUN AUTOMODE"));
        var inspected = new RFiDGearCapabilityInspector().Inspect(executable);
        Assert(inspected.InspectedFiles.Count == 2 && inspected.Has(RFiDGearCapabilityId.AutorunOnce) && inspected.Has(RFiDGearCapabilityId.ContinuousAutoMode),
            "Sibling-RFiDGear.dll oder UTF-16-Marker wurden nicht geprüft.");
        Assert(!inspected.Has(RFiDGearCapabilityId.VerifiedApplicationKeyChange) &&
               RFiDGearKeyChangeReleasePolicy.ApprovedBuilds.Count == RFiDGearTrustedBuildCatalog.KeyChangeBuilds.Count,
            "Ein unbekannter lokaler RFiDGear-Build wurde zugelassen oder der Vertrauenskatalog ist unerwartet.");
        var releasePolicy = new RFiDGearKeyChangeReleasePolicy();
        foreach (var trustedBuild in RFiDGearTrustedBuildCatalog.KeyChangeBuilds)
        {
            var trustedVersion = new RFiDGearVersionInfo("RFiDGear.exe", trustedBuild.ProductVersion, new Version(2, 0, 10, 1), true);
            Assert(releasePolicy.TryApprove(trustedVersion, trustedBuild.EvidenceSha256, out var trustEvidence) &&
                   !string.IsNullOrWhiteSpace(trustEvidence) && trustEvidence.Contains(trustedBuild.Name, StringComparison.Ordinal),
                $"Der exakt hardwaregeprüfte Runtime-Stand '{trustedBuild.Name}' wurde nicht freigegeben.");
            Assert(!releasePolicy.TryApprove(trustedVersion, new string('0', 64), out _),
                "Ein abweichender EXE/DLL-Fingerprint wurde trotz gleicher Produktversion freigegeben.");
        }
        var brandedExecutable = Path.Combine(directory, "SieEncode.Runtime.exe");
        File.Copy(executable, brandedExecutable);
        var brandedInspection = new RFiDGearCapabilityInspector().Inspect(brandedExecutable);
        Assert(brandedInspection.InspectedFiles.Count == 2 && brandedInspection.InspectedFiles.Any(path => Path.GetFileName(path) == "RFiDGear.dll"),
            "Der gebrandete Runtime-Apphost findet den kompatiblen internen RFiDGear-Assemblynamen nicht.");
        var firstFingerprint = RFiDGearCapabilityInspector.EvidenceFingerprint(
            [executable, Path.Combine(directory, "RFiDGear.dll")],
            [File.ReadAllBytes(executable), File.ReadAllBytes(Path.Combine(directory, "RFiDGear.dll"))]);
        var secondFingerprint = RFiDGearCapabilityInspector.EvidenceFingerprint(
            [Path.Combine(directory, "RFiDGear.dll"), executable],
            [File.ReadAllBytes(Path.Combine(directory, "RFiDGear.dll")), File.ReadAllBytes(executable)]);
        Assert(firstFingerprint.Length == 64 && firstFingerprint == secondFingerprint,
            "Der exakte Release-Evidenzfingerprint ist nicht deterministisch oder reihenfolgeunabhängig.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestTestStationSafety()
{
    var root = Path.Combine(Path.GetTempPath(), "sieencode-teststation-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var shells = TaskShellLibrary.Load(Path.Combine(Directory.GetCurrentDirectory(), "assets", "task-shells"));
        var factory = new SupportProjectFactory(shells);
        const string key = "00112233445566778899AABBCCDDEEFF";
        var format = RfPrjSecretProtection.Protect(factory.BuildStandaloneFormat("DF_KEY_AES", key), "format");
        Assert(format.Project.Summarize().Single().Type == "FormatDesfireCard" && format.Secrets.Values.Contains(key, StringComparer.Ordinal),
            "Projektlose Formatierung besitzt keinen exakt geschützten Format-Task.");
        Assert(RfPrjSecretProtection.FindPlaintextSecretElements(format.Project).Count == 0 &&
               !string.Concat(format.Project.Tasks).Contains(key, StringComparison.Ordinal),
            "Projektlose Formatierung enthält Klartextschlüssel.");

        var runtimeOutput = Path.Combine(root, "runtime.json");
        var inventory = RfPrjSecretProtection.Protect(factory.BuildApplicationFileInventory(
            "F482D0", "DF_KEY_AES", 2, key, runtimeOutput, true), "inventory");
        var task = inventory.Project.Tasks.Single();
        Assert(TaskXml.Get(task, "SelectedTaskType") == "ApplicationFileInventory" &&
               TaskXml.Get(task, "AppNumberCurrent") == "0xF482D0" &&
               TaskXml.Get(task, "SelectedDesfireAppKeyNumberCurrent") == "2" &&
               TaskXml.Get(task, "ApplicationInventoryReadContent") == "true",
            "Vertiefte Application-Inventur wurde nicht vollständig erzeugt.");
        Assert(RfPrjSecretProtection.FindPlaintextSecretElements(inventory.Project).Count == 0 &&
               inventory.Secrets.Values.Contains(key, StringComparer.Ordinal), "Application-Inventur schützt den Sitzungsschlüssel nicht.");

        File.WriteAllText(runtimeOutput, """
        {"schemaVersion":"1.0","readOnly":true,"applicationId":"F482D0","authentication":"NoError","keySettings":"0B","keyType":"DF_KEY_AES","maxNumberOfKeys":4,"fileCount":1,"readableContentCount":1,
         "files":[{"fileId":1,"fileType":"StdDataFile","communicationMode":"CM_ENCRYPT","declaredSize":4,"metadataResult":"NoError","contentResult":"NoError",
         "access":{"read":2,"write":3,"readWrite":2,"change":0},"hex":"414200FF","ascii":"AB.."}]}
        """);
        var report = new ApplicationInventoryReportService().Create(root, runtimeOutput);
        var text = File.ReadAllText(report.TextPath);
        Assert(text.Contains("F482D0", StringComparison.Ordinal) && text.Contains("HEX: 414200FF", StringComparison.Ordinal) &&
               text.Contains("Lesen Key 2", StringComparison.Ordinal) && text.Contains("KeyType: DF_KEY_AES", StringComparison.Ordinal) &&
               text.Contains("MaxKeys: 4", StringComparison.Ordinal) && !text.Contains(key, StringComparison.Ordinal),
            "Teststationsbericht ist unvollständig oder enthält den Sitzungsschlüssel.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestHostContractV1()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-host-v1-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var project = Path.Combine(root, "Demo_Kodierung.rfPrj");
        File.WriteAllBytes(project, [1, 2, 3]);
        Directory.CreateDirectory(Path.Combine(root, "payloads"));
        Directory.CreateDirectory(Path.Combine(root, "verification"));
        var tasks = new[]
        {
            new RfPrjTaskSummary(0, "10", "WriteData", "write", "", "Empty", "0xF482D0", "0", Path.Combine(root, "payloads", "data.hex"), "", "14"),
            new RfPrjTaskSummary(1, "20", "ApplicationKeyChangeover", "change", "10", "NoError", "0xF482D0", "", "", "", ""),
            new RfPrjTaskSummary(2, "30", "ReadData", "verify", "20", "NoError", "0xF482D0", "0", "", Path.Combine(root, "verification", "data.hex"), "14")
        };
        var version = new RFiDGearVersionInfo("RFiDGear.exe", "test", new Version(9, 0), true);
        var capabilities = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", version, [], token => token is
            "CUSTOMPROJECTFILE" or "AUTOMODE" or "VerifyChangedApplicationKey" or "VerifyMifareDesfireKeyChange" or "ChangeKey reported" or "Task.Outcome" or "TaskLoop.Summary" or "RunId",
            verifiedKeyChangeReleaseApproved: true, verifiedKeyChangeReleaseEvidence: "Synthetischer Hostvertragstest.");
        var service = new RFiDGearHostContractServiceV1();
        const string requestId = "11111111-2222-3333-4444-555555555555";
        var run = service.Begin(root, project, "Demo", RFiDGearHostModeV1.Automode, tasks, capabilities,
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero), requestId);
        Assert(File.Exists(run.RequestPath) && run.Request.SchemaVersion == "1.1" && run.Request.ProjectFileSha256.Length == 64 && run.Request.ExpectedTaskCount == 3 && run.Request.Tasks[0].Input == "payloads/data.hex",
            "Host-V1-Request wurde nicht projektlokal oder vollständig erzeugt.");
        Assert(run.Request.RequiredCapabilities.Contains("ContinuousAutoMode", StringComparer.Ordinal) &&
               run.Request.RequiredCapabilities.Contains("VerifiedApplicationKeyChange", StringComparer.Ordinal),
            "Host-V1-Request enthält nicht alle aus Modus und Taskplan abgeleiteten Fähigkeiten.");
        var requestCorpus = File.ReadAllText(run.RequestPath);
        Assert(!requestCorpus.Contains("write", StringComparison.Ordinal) && !requestCorpus.Contains("change", StringComparison.Ordinal) &&
               !requestCorpus.Contains(root, StringComparison.OrdinalIgnoreCase),
            "Host-V1-Request enthält Beschreibungen oder absolute Projektpfade.");
        RFiDGearHostContractServiceV1.Validate(run.Request);

        var launch = new RFiDGearLaunchResult(123, "RFiDGear.exe", project, false, true, "CUSTOMPROJECTFILE=... AUTOMODE=1");
        var running = service.MarkRunning(run, launch, new DateTimeOffset(2026, 8, 20, 12, 0, 1, TimeSpan.Zero));
        Assert(running.State == "running" && running.ProcessId == 123 && File.Exists(run.ResultPath), "Host-V1-Running-Ergebnis fehlt.");
        var log = """
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"host-demo","TaskPosition":0,"TaskId":"10","TaskType":"WriteData","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError"}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"host-demo","TaskPosition":1,"TaskId":"20","TaskType":"ApplicationKeyChangeover","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError"}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"host-demo","TaskPosition":2,"TaskId":"30","TaskType":"ReadData","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError"}}
          [INF] {"Stage":"TaskLoop.Success","Details":{"RunId":"host-demo"}}
          """;
        var baseEvaluation = CodingRunEvaluator.Evaluate([], log, tasks);
        var evaluation = baseEvaluation with { Detail = "SOLL DEADBEEF IST CAFEBABE" };
        var completed = service.Complete(run, launch, evaluation, new DateTimeOffset(2026, 8, 20, 12, 0, 2, TimeSpan.Zero));
        Assert(completed.State == "successful" && completed.AcceptanceState == "successful" && completed.Accepted && completed.RunIdHash.Length == 64 &&
               completed.TerminalEvent == "TaskLoop.Success" && completed.TaskOutcomeEvents == 3 && completed.TaskOutcomes.Select(item => item.Position).SequenceEqual([0, 1, 2]),
            "Host-V1-Ergebnis normalisiert Terminal-/Taskevidenz nicht korrekt.");
        var resultCorpus = File.ReadAllText(run.ResultPath);
        Assert(!resultCorpus.Contains("DEADBEEF", StringComparison.Ordinal) && !resultCorpus.Contains("CAFEBABE", StringComparison.Ordinal) &&
               !completed.ContainsKeys && !completed.ContainsCardData,
            "Host-V1-Ergebnis enthält Nutzdaten oder deklariert sensible Inhalte.");
        RFiDGearHostContractServiceV1.Validate(completed, run.Request);
        var twoRuns = RFiDGearStructuredRunParser.Parse(log.Replace("host-demo", "previous-run", StringComparison.Ordinal) + Environment.NewLine + log, tasks);
        Assert(twoRuns.Correlated && twoRuns.RunIdHash == completed.RunIdHash && twoRuns.TaskOutcomes.Count == 3,
            "Host-V1 isoliert den letzten terminalen RunId-Lauf nicht sauber aus einem Logdelta mit mehreren Läufen.");
        AssertThrows<InvalidOperationException>(() => service.Begin(root, project, "Demo", RFiDGearHostModeV1.Automode, tasks, capabilities, requestId: requestId));
        AssertThrows<InvalidDataException>(() => RFiDGearHostContractServiceV1.Validate(completed with { RequestId = Guid.NewGuid().ToString("D") }, run.Request));
        var wrongOutcome = completed.TaskOutcomes.ToArray();
        wrongOutcome[0] = wrongOutcome[0] with { Type = "ReadData" };
        AssertThrows<InvalidDataException>(() => RFiDGearHostContractServiceV1.Validate(completed with { TaskOutcomes = wrongOutcome }, run.Request));

        var controlFlowTasks = new[]
        {
            new RfPrjTaskSummary(0, "10", "AppExistCheck", "must be absent", "", "Empty", "", "", "", "", ""),
            new RfPrjTaskSummary(1, "20", "CreateApplication", "create", "10", "IsNotTrue", "", "", "", "", ""),
            new RfPrjTaskSummary(2, "30", "CreateFile", "create file", "20", "NoError", "0xF482D0", "0", "", "", "14")
        };
        var controlFlowRun = service.Begin(root, project, "Demo", RFiDGearHostModeV1.Automode, controlFlowTasks, capabilities);
        var controlFlowLog = """
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"control-flow","TaskPosition":0,"TaskId":"10","TaskType":"AppExistCheck","Decision":"Executed","Outcome":"Failed","ErrorCode":"IsNotTrue"}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"control-flow","TaskPosition":1,"TaskId":"20","TaskType":"CreateApplication","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError"}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"control-flow","TaskPosition":2,"TaskId":"30","TaskType":"CreateFile","Decision":"Executed","Outcome":"Failed","ErrorCode":"TransportError"}}
          [INF] {"Stage":"TaskLoop.Failure","Details":{"RunId":"control-flow"}}
          """;
        var controlFlowEvaluation = CodingRunEvaluator.Evaluate([], controlFlowLog, controlFlowTasks);
        var controlFlowResult = service.Complete(controlFlowRun, launch, controlFlowEvaluation);
        Assert(controlFlowResult.State == "failed" && controlFlowResult.FailedOutcomes == 2 &&
               controlFlowResult.FailedTaskExecutionIndex == 2 && controlFlowResult.FailedTaskType == "CreateFile",
            "Host-V1 meldet eine erwartete negative Vorprüfung statt des ersten unbehandelten ursächlichen Fehlers.");

        var persistedRequest = File.ReadAllText(run.RequestPath);
        File.AppendAllText(run.RequestPath, " ");
        AssertThrows<InvalidDataException>(() => service.MarkRunning(run, launch));
        File.WriteAllText(run.RequestPath, persistedRequest);
        AssertThrows<InvalidOperationException>(() => service.MarkRunning(run, launch));

        var fingerprintRun = service.Begin(root, project, "Demo", RFiDGearHostModeV1.Open, tasks, capabilities);
        var originalProject = File.ReadAllBytes(project);
        File.AppendAllText(project, "tamper");
        AssertThrows<InvalidDataException>(() => service.MarkRunning(fingerprintRun, launch));
        File.WriteAllBytes(project, originalProject);

        var missingVerification = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", version, [], token => token is "CUSTOMPROJECTFILE" or "AUTOMODE");
        AssertThrows<InvalidDataException>(() => service.Begin(root, project, "Demo", RFiDGearHostModeV1.Automode, tasks, missingVerification));
        var tampered = run.Request with { TaskPlanSha256 = new string('0', 64) };
        AssertThrows<InvalidDataException>(() => RFiDGearHostContractServiceV1.Validate(tampered));
        var traversingTasks = run.Request.Tasks.ToArray();
        traversingTasks[0] = traversingTasks[0] with { Input = "../outside.hex" };
        var traversing = run.Request with { Tasks = traversingTasks, TaskPlanSha256 = run.Request.TaskPlanSha256 };
        AssertThrows<InvalidDataException>(() => RFiDGearHostContractServiceV1.Validate(traversing));
    }
    finally { Directory.Delete(root, true); }
}

static void TestReaderSettingsInspection()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-settings-inspection-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var path = Path.Combine(directory, "settings.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\"?><Settings><DefaultReaderProvider>None</DefaultReaderProvider><DefaultReaderName></DefaultReaderName><LastUsedComPort>USB</LastUsedComPort></Settings>");
        var missing = RFiDGearSettingsInspection.Inspect(path);
        Assert(missing.Exists && !missing.IsConfigured && missing.Provider == "None" && missing.Port == "USB", "Nicht konfigurierter Reader wurde falsch bewertet.");
        File.WriteAllText(path, "<?xml version=\"1.0\"?><Settings><DefaultReaderProvider>Elatec</DefaultReaderProvider><DefaultReaderName></DefaultReaderName><LastUsedComPort>USB</LastUsedComPort></Settings>");
        var elatec = RFiDGearSettingsInspection.Inspect(path);
        Assert(elatec.IsConfigured && elatec.Provider == "Elatec" && elatec.Port == "USB" && elatec.Error is null, "Elatec/USB wurde nicht als konfigurierte Leserwahl erkannt.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestRuntimeDiagnostics()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-log-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var path = Path.Combine(directory, "log-20260812.txt");
        File.WriteAllText(path, "old line\n");
        var monitor = new RFiDGearLogMonitor();
        var snapshot = monitor.Snapshot(directory);
        const string secret = "00112233445566778899AABBCCDDEEFF";
        var specific = $"[ERR] {{\"Stage\":\"TaskLoop.Failure\",\"Message\":\"AuthFailure {secret}\",\"Details\":{{\"CurrentTaskIndex\":4}}}}";
        File.AppendAllText(path, specific + "\n[ERR] {\"Stage\":\"ExecuteOnceAsync\",\"Details\":{\"CurrentTaskIndex\":4}}\n");
        var delta = monitor.ReadDelta(snapshot);
        Assert(delta.HasContent && !delta.Text.Contains("old line", StringComparison.Ordinal), "Logdelta enthält Baseline oder fehlt.");
        var signal = RFiDGearDiagnosticParser.Parse(delta.Text);
        Assert(signal.StrongFailure && signal.Category == "Authentifizierung / Key" && signal.Stage == "TaskLoop.Failure" && signal.TaskExecutionIndex == 4, "Strukturierter TaskLoop-Fehler wurde falsch ausgewertet.");
        Assert(!signal.SanitizedText.Contains(secret, StringComparison.Ordinal) && signal.SanitizedText.Contains("REDACTED_HEX_16B", StringComparison.Ordinal), "Logsanitizer ließ Schlüsselmaterial zurück.");
        var transport = RFiDGearDiagnosticParser.Parse("[ERR] TransportError while reader disconnected");
        Assert(transport.Category == "Reader / Transport", "Transportfehler wurde falsch klassifiziert.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestOperations()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-operation-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        void WriteProject(string name, params (int Index, string Type, int? Condition, string Level)[] definitions)
        {
            var document = RfPrjDocument.Create();
            foreach (var definition in definitions)
            {
                var task = new XElement("anyType",
                    new XElement("CurrentTaskIndex", definition.Index),
                    new XElement("SelectedTaskType", definition.Type),
                    new XElement("SelectedTaskDescription", definition.Type),
                    new XElement("SelectedExecuteConditionErrorLevel", definition.Condition is null ? "Empty" : definition.Level));
                if (definition.Condition is not null) task.Add(new XElement("SelectedExecuteConditionTaskIndex", definition.Condition.Value));
                document.Append(task);
            }
            RfPrjArchive.Save(Path.Combine(root, name), document);
        }

        WriteProject("Demo_Mastering.rfPrj", (10, "CreateApplication", null, "Empty"));
        WriteProject("preflight-t.rfPrj", (10, "ReadData", null, "Empty"));
        WriteProject("preflight-p.rfPrj", (10, "ReadData", null, "Empty"));
        WriteProject("recovery-t.rfPrj", (10, "FormatDesfireCard", null, "Empty"));
        WriteProject("recovery-p.rfPrj", (10, "FormatDesfireCard", null, "Empty"), (20, "PICCMasterKeyChangeover", 10, "NoError"));
        WriteProject("acceptance.rfPrj", (10, "ReadAppSettings", null, "Empty"), (20, "CheckAppKeyCount", null, "Empty"), (30, "CheckFileSettings", null, "Empty"));
        var acceptancePath = Path.Combine(root, "acceptance.rfPrj");
        var acceptance = RfPrjArchive.Load(acceptancePath);
        foreach (var task in acceptance.Tasks)
        {
            TaskXml.Set(task, "AppNumberCurrent", "0xF00001");
            if (TaskXml.Get(task, "SelectedTaskType") == "CheckFileSettings") TaskXml.Set(task, "FileNumberCurrent", "0");
        }
        RfPrjArchive.Save(acceptancePath, acceptance);
        var compatibilityCopy = Path.Combine(root, "acceptance-compatible.rfPrj");
        RfPrjArchive.Save(compatibilityCopy, acceptance);
        var compatible = RfPrjArchive.Load(compatibilityCopy);
        Assert(ApplicationAcceptanceCompatibilityService.RemoveUnsupportedFileSettingsTasks(compatible) == 1 &&
               compatible.Summarize().Select(task => task.Type).SequenceEqual(["ReadAppSettings", "CheckAppKeyCount"]),
            "Bestehendes CheckFileSettings-Abnahmeprojekt wird nicht sicher auf ausführbare Application-Prüfungen reduziert.");
        RfPrjArchive.Save(compatibilityCopy, compatible);
        File.WriteAllText(Path.Combine(root, "card-preflight.plan.json"), """
          {"transportProject":"preflight-t.rfPrj","productiveProject":"preflight-p.rfPrj","productiveProbeAvailable":true}
          """);
        File.WriteAllText(Path.Combine(root, "recovery.plan.json"), """
          {"transportProject":"recovery-t.rfPrj","productiveProject":"recovery-p.rfPrj"}
          """);
        File.WriteAllText(Path.Combine(root, "acceptance.plan.json"), """
          {"applicationAcceptanceProject":"acceptance.rfPrj"}
          """);

        var catalog = new RFiDGearOperationService().Inspect(root);
        Assert(catalog.Operations.Count == 7, "Betriebsartefakte wurden nicht vollständig erkannt.");
        Assert(catalog.Find(RFiDGearOperationKind.CardPreflightTransport) is { ReadOnly: true, Destructive: false }, "Karten-Preflight ist nicht als read-only klassifiziert.");
        Assert(catalog.Find(RFiDGearOperationKind.ApplicationAcceptanceProductive) is { ReadOnly: true, Destructive: false, Autorun: true }, "Application-Abnahme ist nicht als read-only Einmallauf klassifiziert.");
        Assert(catalog.Find(RFiDGearOperationKind.RecoveryProductive) is { Destructive: true, Autorun: true }, "Recovery ist nicht als destruktiver Einmallauf klassifiziert.");

        File.WriteAllText(Path.Combine(root, "acceptance.plan.json"), "{\"applicationAcceptanceProject\":\"acceptance-compatible.rfPrj\"}");
        var compatibleCatalog = new RFiDGearOperationService().Inspect(root);
        var compatibleAcceptance = compatibleCatalog.Find(RFiDGearOperationKind.ApplicationAcceptanceProductive);
        Assert(compatibleAcceptance is not null,
            "Mit RFiDGear 2.0.10 kompatibilisierte Anwendungsprüfung fehlt im Betriebskatalog.");
        Assert(compatibleAcceptance!.Tasks.Count == 2 &&
               compatibleAcceptance.Tasks.Select(task => task.Type).SequenceEqual(["ReadAppSettings", "CheckAppKeyCount"]),
            "Mit RFiDGear 2.0.10 kompatibilisierte Anwendungsprüfung enthält nicht exakt die zwei ausführbaren Tasks: " +
            string.Join(", ", compatibleAcceptance.Tasks.Select(task => task.Type)));
        File.WriteAllText(Path.Combine(root, "acceptance.plan.json"), "{\"applicationAcceptanceProject\":\"acceptance.rfPrj\"}");

        var operation = catalog.Find(RFiDGearOperationKind.MasteringAutorun)!;
        var started = DateTimeOffset.UtcNow;
        var session = new RFiDGearOperationSession(operation,
            new RFiDGearLaunchResult(42, "RFiDGear.exe", operation.ProjectPath, true, false, "AUTORUN=1"),
            new RFiDGearLogSnapshot(started, root, new Dictionary<string, long>()), started, TimeSpan.FromSeconds(30));
        var recovered = RFiDGearOperationService.EvaluateText(session,
            "[ERR] {\"Stage\":\"TaskLoop.Failure\"}\n" +
            "[INF] {\"Stage\":\"Task.Outcome\",\"Details\":{\"RunId\":\"test\",\"TaskPosition\":0,\"TaskId\":\"10\",\"TaskType\":\"CreateApplication\",\"Outcome\":\"Successful\"}}\n" +
            "[INF] {\"Stage\":\"TaskLoop.Summary\",\"Details\":{\"RunId\":\"test\",\"TotalTasks\":1,\"Executed\":1}}\n" +
            "[INF] {\"Stage\":\"TaskLoop.Success\",\"Details\":{\"RunId\":\"test\"}}", started.AddSeconds(2));
        Assert(recovered.State == RFiDGearOperationState.Successful, "Späterer terminaler TaskLoop.Success wurde nicht als Endstatus verwendet.");
        var emptyFalseSuccess = RFiDGearOperationService.EvaluateText(session,
            "[INF] {\"Stage\":\"TaskLoop.Summary\",\"Details\":{\"RunId\":\"empty\",\"TotalTasks\":0,\"Executed\":0}}\n" +
            "[INF] {\"Stage\":\"TaskLoop.Success\",\"Details\":{\"RunId\":\"empty\"}}", started.AddSeconds(2));
        Assert(emptyFalseSuccess.State == RFiDGearOperationState.Failed && emptyFalseSuccess.Summary.Contains("nicht vollständig", StringComparison.OrdinalIgnoreCase),
            "Ein leer ausgeführter Taskplan wird trotz terminalem Success nicht fail-closed abgewiesen.");
        var failed = RFiDGearOperationService.EvaluateText(session,
            "[INF] {\"Stage\":\"TaskLoop.Success\"}\n[ERR] {\"Stage\":\"TaskLoop.Failure\",\"Details\":{\"CurrentTaskIndex\":0}}", started.AddSeconds(2));
        Assert(failed.State == RFiDGearOperationState.Failed, "Späterer terminaler TaskLoop.Failure wurde übergangen.");
        Assert(RFiDGearOperationService.EvaluateText(session, "", started.AddMinutes(1)).State == RFiDGearOperationState.TimedOut, "Fehlender Terminalstatus erzeugt keinen Timeout.");

        File.WriteAllText(Path.Combine(root, "card-preflight.plan.json"), "{\"transportProject\":\"..\\\\escape.rfPrj\",\"productiveProbeAvailable\":false}");
        AssertThrows<InvalidDataException>(() => new RFiDGearOperationService().Inspect(root));
    }
    finally { Directory.Delete(root, true); }
}

static void TestRunSafety()
{
    var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.MasteringAutorun, "Mastering", "master.rfPrj", false, true, true, []);
    var launch = new RFiDGearLaunchResult(42, "RFiDGear.exe", "master.rfPrj", true, false, "AUTORUN=1");
    var session = new RFiDGearOperationSession(operation, launch,
        new RFiDGearLogSnapshot(DateTimeOffset.Now, "logs", new Dictionary<string, long>()), DateTimeOffset.Now, TimeSpan.FromMinutes(2));
    var stopped = new FakeProcessProbe(false);
    var waiting = RFiDGearRunSafety.Assess(session, operationFinalized: false, codingLaunch: null, stopped);
    Assert(!waiting.IsSafe && waiting.ProcessId == 42 && waiting.Reason.Contains("terminalen", StringComparison.OrdinalIgnoreCase),
        "Ein noch überwachter AUTORUN-Lauf lässt Schließen oder Projektwechsel zu.");

    var running = new FakeProcessProbe(true);
    var finalizedButOpen = RFiDGearRunSafety.Assess(session, operationFinalized: true, codingLaunch: null, running);
    Assert(!finalizedButOpen.IsSafe && finalizedButOpen.Reason.Contains("zuerst schließen", StringComparison.OrdinalIgnoreCase),
        "Ein nach Terminalstatus weiterhin laufender RFiDGear-Prozess wird nicht gesperrt.");
    var coding = new RFiDGearLaunchResult(77, "RFiDGear.exe", "coding.rfPrj", false, true, "AUTOMODE=1");
    Assert(!RFiDGearRunSafety.Assess(null, true, coding, running).IsSafe, "Ein laufender Kodier-/Serienprozess wird nicht gesperrt.");
    Assert(RFiDGearRunSafety.Assess(session, true, coding, stopped).IsSafe, "Beendete und finalisierte Prozesse blockieren den Builder weiterhin.");
}

static void TestIoPathsAndProcessCompletion()
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rfidgear-io-root-" + Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(root);
    try
    {
        var local = new RfPrjTaskSummary(0, "10", "ReadData", "local", "", "Empty", "0xF00001", "0", "", Path.Combine(root, "readback", "data.hex"));
        Assert(ProjectPreflight.ValidateIoPaths([local], root).Count == 0, "Projektlokaler Readbackpfad wird gesperrt.");
        var externalPath = Path.Combine(Path.GetTempPath(), "external-" + Guid.NewGuid().ToString("N"), "data.hex");
        var external = local with { OutputPath = externalPath };
        var issue = ProjectPreflight.ValidateIoPaths([external], root).Single();
        Assert(issue.Code == "TASK_IO_PATH_OUTSIDE" && issue.Message.Contains(externalPath, StringComparison.OrdinalIgnoreCase),
            "Externer Readbackpfad wird nicht exakt und fail-closed ausgewiesen.");
        var missingInput = local with { Type = "WriteData", InputPath = Path.Combine(root, "initialization", "missing.hex"), OutputPath = "", FileSize = "6" };
        Assert(ProjectPreflight.ValidateIoPaths([missingInput], root).Count == 0, "Noch nicht bereitgestellte Sitzungs-Payload wird im allgemeinen Pfadvertrag fälschlich gesperrt.");
        Assert(ProjectPreflight.ValidateIoPaths([missingInput], root, requireInputFiles: true).Single().Code == "TASK_INPUT_MISSING",
            "Betriebs-/Masteringvertrag erkennt eine fehlende Eingabedatei nicht.");

        var launch = new RFiDGearLaunchResult(91, @"C:\Program Files\RFiDGear\RFiDGear.exe", "test.rfPrj", true, false, "AUTORUN=1");
        var gracefulHandle = new FakeControllableProcess(91, launch.ExecutablePath, closeResult: true, exitsAfterClose: true);
        var graceful = new RFiDGearProcessCompletionService(new FakeControllableProcessSource(gracefulHandle)).Complete(launch, false);
        Assert(graceful.Completed && !graceful.RequiresForce && !gracefulHandle.Killed, "Normales RFiDGear-Schließen wird nicht bevorzugt.");

        var headlessHandle = new FakeControllableProcess(91, launch.ExecutablePath, closeResult: false, exitsAfterClose: false);
        var service = new RFiDGearProcessCompletionService(new FakeControllableProcessSource(headlessHandle));
        var needsForce = service.Complete(launch, false);
        Assert(!needsForce.Completed && needsForce.RequiresForce && !headlessHandle.Killed, "Fensterloser Prozess wird ohne Bestätigung beendet.");
        var forced = service.Complete(launch, true);
        Assert(forced.Completed && headlessHandle.Killed, "Bewusst bestätigter terminaler Prozess wird nicht beendet.");

        var terminalHeadless = new FakeControllableProcess(92, launch.ExecutablePath, closeResult: false, exitsAfterClose: false);
        var terminalLaunch = launch with { ProcessId = 92 };
        var terminalCompletion = new RFiDGearProcessCompletionService(new FakeControllableProcessSource(terminalHeadless)).CompleteTerminal(terminalLaunch);
        Assert(terminalCompletion.Completed && terminalHeadless.Killed && !terminalCompletion.RequiresForce,
            "Autoritativ terminaler fensterloser AUTORUN-Prozess wird nicht kontrolliert abgeschlossen.");

        var reused = new FakeControllableProcess(91, @"C:\Other\notepad.exe", false, false);
        AssertThrows<InvalidOperationException>(() => new RFiDGearProcessCompletionService(new FakeControllableProcessSource(reused)).Complete(launch, true));
        Assert(!reused.Killed, "Wiederverwendete fremde Prozess-ID wurde verändert.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestCardPreflight()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-card-preflight-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "payloads"));
    try
    {
        File.WriteAllText(Path.Combine(root, "card-preflight.plan.json"), """
        {
          "schemaVersion":"1.0","projectName":"Probe","readOnly":true,
          "transportDirectory":"card_preflight/transport","productiveDirectory":"card_preflight/productive",
          "credentialSetsEquivalent":false,
          "files":[
            {"aid":"F00001","fileId":0,"name":"ZeroData","size":4,"codingPayload":"payloads/data.hex","transportOutput":"card_preflight/transport/data.hex","productiveOutput":"card_preflight/productive/data.hex"}
          ]
        }
        """);
        File.WriteAllText(Path.Combine(root, "payloads", "data.hex"), "01 23 45 67");
        var service = new CardPreflightService();
        var initial = service.Evaluate(root, DateTimeOffset.Parse("2026-08-13T12:00:00+02:00"));
        Assert(initial.Report.Classification.Code == "NICHT_AUSGEFUEHRT", "Noch nicht ausgeführter Karten-Preflight wird falsch klassifiziert.");

        var transportDirectory = Path.Combine(root, "card_preflight", "transport");
        Directory.CreateDirectory(transportDirectory);
        File.WriteAllBytes(Path.Combine(transportDirectory, "data.hex"), new byte[4]);
        File.WriteAllText(Path.Combine(transportDirectory, "_probe.completed.json"), "{\"completedAt\":\"2026-08-13T12:01:00\",\"outcome\":\"RFiDGearSuccess\"}");
        var transport = service.Evaluate(root);
        Assert(transport.Report.Classification.Code == "TRANSPORT_MASTERING_OK" && transport.Report.Transport.ReadableCount == 1,
            "Vier Nullbytes werden fälschlich als leer statt als größenrichtiges Readback bewertet.");
        Assert(transport.Report.Recommendation.Code == "READY_FOR_CODING" && File.Exists(transport.JsonPath) && File.Exists(transport.TextPath), "Transportempfehlung oder Berichtsdateien fehlen.");

        var productiveDirectory = Path.Combine(root, "card_preflight", "productive");
        Directory.CreateDirectory(productiveDirectory);
        File.WriteAllText(Path.Combine(productiveDirectory, "data.hex"), "01 23 45 67");
        File.WriteAllText(Path.Combine(productiveDirectory, "_probe.completed.json"), "{\"completedAt\":\"2026-08-13T12:01:30\",\"outcome\":\"RFiDGearFailure\"}");
        Assert(service.Evaluate(root).Report.Classification.Code == "TRANSPORT_MASTERING_OK",
            "Alte Dateien einer fehlgeschlagenen Produktivprobe überstimmen fälschlich den erfolgreichen Transportnachweis.");

        service.Prepare(root, CardPreflightMode.Transport);
        Assert(!File.Exists(Path.Combine(transportDirectory, "data.hex")) && !File.Exists(Path.Combine(transportDirectory, "_probe.completed.json")), "Vorbereitung entfernt alte Probe-Ergebnisse nicht.");

        Directory.CreateDirectory(productiveDirectory);
        File.WriteAllText(Path.Combine(productiveDirectory, "data.hex"), "01 23 45 67");
        File.WriteAllText(Path.Combine(productiveDirectory, "_probe.completed.json"), "{\"completedAt\":\"2026-08-13T12:02:00\",\"outcome\":\"RFiDGearSuccess\"}");
        var productive = service.Evaluate(root);
        Assert(productive.Report.Classification.Code == "PRODUKTIV_OK" && productive.Report.Productive.PayloadMatchCount == 1, "Bytegleiche Produktivprobe wird nicht als PRODUKTIV_OK bewertet.");
        Assert(productive.Report.Productive.Files[0].HexValue == "01234567" && productive.Report.Productive.Files[0].AsciiValue == ".#Eg" &&
               File.ReadAllText(productive.TextPath).Contains("HEX 01234567 · ASCII .#Eg", StringComparison.Ordinal),
            "Gelesener Projektinhalt wird nicht verständlich als HEX und ASCII ausgewiesen.");
        var expectedDirectory = Path.Combine(root, "card_preflight");
        Directory.CreateDirectory(expectedDirectory);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([0x01, 0x23, 0x45, 0x67]));
        File.WriteAllText(Path.Combine(expectedDirectory, "expected-card.json"),
            $$"""{"preparedAt":"2026-08-13T12:01:50","payloads":[{"output":"payloads/data.hex","length":4,"sha256":"{{expectedHash}}"}]}""");
        File.Delete(Path.Combine(root, "payloads", "data.hex"));
        var persistentExpected = service.Evaluate(root);
        Assert(persistentExpected.Report.Classification.Code == "PRODUKTIV_OK" && persistentExpected.Report.Productive.PayloadMatchCount == 1,
            "Produktivprobe verliert nach sicherer Payload-Invaliderung den persistenten SHA-256-Sollnachweis.");
        File.WriteAllText(Path.Combine(root, "coding.schema.json"), """
        {
          "schemaVersion":"1.1","projectName":"Probe","codingProject":"Probe.rfPrj","finalVerificationDirectory":"verification",
          "variables":[],
          "payloads":[{"aid":"F00001","appName":"Test","fileId":0,"fileName":"Data","size":4,"output":"data.hex",
            "recipe":[{"op":"fixedHex","value":"01234567"}]}]
        }
        """);
        RfPrjArchive.Save(Path.Combine(root, "Probe.rfPrj"), RfPrjDocument.Create());
        File.WriteAllText(Path.Combine(expectedDirectory, "expected-card.json"),
            """{"preparedAt":"2026-08-13T12:01:50","containsCardData":true,"values":{}}""");
        var legacyExpected = service.Evaluate(root);
        Assert(legacyExpected.Report.Classification.Code == "PRODUKTIV_OK" && legacyExpected.Report.Productive.PayloadMatchCount == 1,
            "Älterer Sollnachweis ohne Payload-Hashes wird nicht aus coding.schema.json rekonstruiert oder dem payloads/-Pfad zugeordnet.");
        File.WriteAllText(Path.Combine(productiveDirectory, "data.hex"), "89 AB CD EF");
        Assert(service.Evaluate(root).Report.Classification.Code == "PRODUKTIV_DATEN_ABWEICHUNG", "Produktive Payload-Abweichung wird nicht gesperrt.");

        service.Prepare(root, CardPreflightMode.Productive);
        var fakeOperation = new RFiDGearOperationArtifact(RFiDGearOperationKind.CardPreflightProductive, "Probe", "probe.rfPrj", true, false, true, []);
        var started = DateTimeOffset.UtcNow;
        var session = new RFiDGearOperationSession(fakeOperation, new RFiDGearLaunchResult(1, "RFiDGear.exe", "probe.rfPrj", true, false, "AUTORUN=1"),
            new RFiDGearLogSnapshot(started, root, new Dictionary<string, long>()), started, TimeSpan.FromMinutes(2));
        var failedRun = RFiDGearOperationService.EvaluateText(session, "[ERR] {\"Stage\":\"TaskLoop.Failure\",\"Message\":\"AuthFailure\"}", started.AddSeconds(1));
        var failedReport = service.CompleteAndEvaluate(root, CardPreflightMode.Productive, failedRun, started.AddSeconds(1));
        Assert(failedReport.Report.Classification.Code == "PREFLIGHT_LAUFZEITFEHLER", "Terminaler RFiDGear-Fehler wird in der Kartenklassifikation nicht sichtbar.");
        Assert(File.ReadAllText(Path.Combine(productiveDirectory, "rfidgear.log.sanitized.txt")).Contains("TaskLoop.Failure", StringComparison.Ordinal), "Bereinigter Probe-Log fehlt.");
        Assert(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Temporäre Karten-Preflight-Datei blieb zurück.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestCardRecognition()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-card-recognition-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        File.WriteAllText(Path.Combine(root, "project.definition.json"), """
          {"applications":[{"id":"F482D0"},{"id":"F482D1"}]}
          """);
        var document = RfPrjDocument.Create();
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "10"), new XElement("SelectedTaskType", "PICCApplicationInventory"), new XElement("SelectedTaskDescription", "inventory")));
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "20"), new XElement("SelectedTaskType", "PICCAuthCheck"), new XElement("SelectedTaskDescription", "STANDARD_DES")));
        document.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "30"), new XElement("SelectedTaskType", "PICCAuthCheck"), new XElement("SelectedTaskDescription", "TRANSPORT")));
        var projectPath = Path.Combine(root, "Demo_CardRecognition_ReadOnly.rfPrj");
        RfPrjArchive.Save(projectPath, document);
        var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.CardRecognitionReadOnly, "Erkennung", projectPath, true, false, true, document.Summarize());
        var started = DateTimeOffset.UtcNow;
        var session = new RFiDGearOperationSession(operation,
            new RFiDGearLaunchResult(42, "RFiDGear.exe", projectPath, true, false, "AUTORUN=1"),
            new RFiDGearLogSnapshot(started, root, new Dictionary<string, long>()), started, TimeSpan.FromSeconds(30));
        var emptyLog = """
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"recognize","TaskPosition":0,"TaskId":"10","TaskType":"PICCApplicationInventory","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError","IO":{"PICCApplicationCount":0,"PICCApplicationIds":[],"PICCKeySettings":15,"PICCFreeMemoryBytes":1152,"PICCCreateDeleteWithoutMasterKey":true}}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"recognize","TaskPosition":1,"TaskId":"20","TaskType":"PICCAuthCheck","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError","IO":{"PICCAuthMatched":true,"PICCAuthResult":"NoError"}}}
          [INF] {"Stage":"Task.Outcome","Details":{"RunId":"recognize","TaskPosition":2,"TaskId":"30","TaskType":"PICCAuthCheck","Decision":"Executed","Outcome":"Successful","ErrorCode":"NoError","IO":{"PICCAuthMatched":true,"PICCAuthResult":"NoError"}}}
          [INF] {"Stage":"TaskLoop.Summary","Details":{"RunId":"recognize","TotalTasks":3,"Executed":3}}
          [INF] {"Stage":"TaskLoop.Success","Details":{"RunId":"recognize"}}
          """;
        var evaluation = RFiDGearOperationService.EvaluateText(session, emptyLog, started.AddSeconds(1));
        var report = new CardRecognitionService().Create(root, operation, evaluation).Report;
        Assert(report.Structure == "LEER" && report.ApplicationIds.Count == 0 && report.PiccState.Contains("MEHRDEUTIG", StringComparison.Ordinal) &&
               report.PiccKeySettings == 15 && report.PiccFreeMemoryBytes == 1152 && report.CreateDeleteWithoutMasterKey == true,
            "Erfolgreiche Null-AID-Inventur oder äquivalente PICC-Zugänge werden falsch klassifiziert.");

        var failedLog = emptyLog.Replace("\"Outcome\":\"Successful\",\"ErrorCode\":\"NoError\",\"IO\":{\"PICCApplicationCount\":0,\"PICCApplicationIds\":[],\"PICCKeySettings\":15,\"PICCFreeMemoryBytes\":1152,\"PICCCreateDeleteWithoutMasterKey\":true}",
            "\"Outcome\":\"Failed\",\"ErrorCode\":\"PermissionDenied\",\"IO\":{}", StringComparison.Ordinal)
            .Replace("TaskLoop.Success", "TaskLoop.Failure", StringComparison.Ordinal);
        var failed = RFiDGearOperationService.EvaluateText(session, failedLog, started.AddSeconds(1));
        var unknown = new CardRecognitionService().Create(root, operation, failed).Report;
        Assert(unknown.Structure == "UNBEKANNT" && unknown.State == "UNKNOWN",
            "Fehlgeschlagene Inventur darf niemals als leer gelten.");

        var resultText = File.ReadAllText(Path.Combine(root, "recognition", "card-recognition.result.json"));
        Assert(!resultText.Contains(new string('0', 32), StringComparison.Ordinal) && resultText.Contains("\"readOnly\": true", StringComparison.Ordinal),
            "Erkennungsbericht ist nicht nachweislich keyfrei/read-only.");
    }
    finally { Directory.Delete(root, true); }
}

static void TestExistingCardExtension()
{
    var root = Path.Combine(Path.GetTempPath(), "rfidgear-extension-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        // Synthetic fixtures keep the full extension regression runnable without customer profiles.
        var variable = new VariableDefinition { Id = "cardNumber", Label = "Test card", Scope = "card", Type = "digits", Length = 6, Required = true };
        var library = ProfileLibrary.FromProfiles([
            MinimalProfile("SYNTHETIC-BASE", "base", "A0B001", variable),
            MinimalProfile("SYNTHETIC-EXTENSION", "customer", "A0B002", variable)
        ]);
        var shells = TaskShellLibrary.Load(Path.Combine(Directory.GetCurrentDirectory(), "assets", "task-shells"));
        var project = ValidProject();
        project.Project.Name = "Extension";
        project.Project.CardMode = CardOperationMode.ExtendExisting;
        project.Project.Build = ["Mastering", "Coding"];
        project.Profiles.Base = "SYNTHETIC-BASE";
        project.Profiles.Customers = ["SYNTHETIC-EXTENSION"];
        project.Values["firmCode"] = "1234567890";
        var composition = library.Compose(project.Profiles.Base, project.Profiles.Customers);
        var additions = CardOperationMode.SelectApplications(composition, project);
        Assert(additions.Count == 1, "Zusatzprofil wird nicht auf genau seine neue Application begrenzt.");
        var targetAid = ProfileValidator.NormalizeAid(additions[0].Id);
        project.Productive.ApplicationKeys[targetAid] = Enumerable.Range(0, additions[0].KeyCount)
            .Select(index => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("extension-key-" + index)))[..32]).ToList();
        project.Productive.PiccKey.Value = "";
        Assert(ProjectContractValidator.Validate(project, library, requireProductiveSecrets: true).Count == 0,
            "Erweiterungsprojekt verlangt fälschlich einen produktiven PICC-Key oder Schlüssel der Grundprofil-AIDs.");

        var factory = new MainProjectFactory(shells);
        var mastering = factory.BuildMastering(composition, project, root);
        var coding = factory.BuildCoding(composition, project, root);
        var baseAids = library.Get(project.Profiles.Base).Applications.Select(application => ProfileValidator.NormalizeAid(application.Id)).ToArray();
        Assert(mastering.Summarize().Any(task => task.Aid.Contains(targetAid, StringComparison.OrdinalIgnoreCase)) &&
               !mastering.Summarize().Any(task => baseAids.Any(aid => task.Aid.Contains(aid, StringComparison.OrdinalIgnoreCase))),
            "Mastering der Erweiterung greift auf vorhandene Grundprofil-AIDs zu oder enthält die Ziel-AID nicht.");
        Assert(coding.Summarize().Any(task => task.Aid.Contains(targetAid, StringComparison.OrdinalIgnoreCase)) &&
               !coding.Summarize().Any(task => baseAids.Any(aid => task.Aid.Contains(aid, StringComparison.OrdinalIgnoreCase))),
            "Kodierung der Erweiterung greift auf vorhandene Grundprofil-AIDs zu.");
        Assert(!coding.Summarize().Any(task => task.Type is "PICCMasterKeyChangeover" or "PICCMasterKeySettingsChangeover"),
            "Erweiterung enthält einen unzulässigen PICC-Key- oder PICC-KeySettings-Wechsel.");
        var rollback = new SupportProjectFactory(shells).BuildExtensionRollback(composition, project);
        Assert(rollback.Summarize().Count == additions.Count && rollback.Summarize().All(task => task.Type == "DeleteApplication"),
            "Gezielter Erweiterungs-Rückbau enthält nicht genau einen DeleteApplication-Task je Ziel-AID.");
        var rollbackTask = rollback.Tasks.Single();
        Assert(TaskXml.Get(rollbackTask, "DesfireAppKeyCurrent") == project.Productive.ApplicationKeys[targetAid][0] &&
               TaskXml.Get(rollbackTask, "SelectedDesfireAppKeyEncryptionTypeCurrent") == additions.Single().KeyType,
            "Gezielter Erweiterungs-Rückbau besitzt nicht den eigenen produktiven Application-Masterkey der Ziel-AID.");

        var sourcePath = Path.Combine(root, "extension.project.json");
        File.WriteAllText(sourcePath, "{}");
        var context = new ContractArtifactContext(project, composition, sourcePath, "test", DateTime.Now);
        var artifacts = ContractArtifactFactory.Build(context);
        var recovery = artifacts["recovery.plan.json"].AsObject();
        Assert(recovery["transportProject"] is null && recovery["productiveProject"] is null &&
               recovery["extensionRollbackProject"]?.GetValue<string>() == "Extension_Erweiterung_Rueckbau.rfPrj" &&
               recovery["destructive"]?.GetValue<bool>() == false,
            "Erweiterungsmodus bietet weiterhin destruktive Format-Recovery an.");
        ContractArtifactFactory.SaveAll(root, artifacts);

        var recognition = RfPrjDocument.Create();
        recognition.Append(new XElement("anyType", new XElement("CurrentTaskIndex", "10"), new XElement("SelectedTaskType", "PICCApplicationInventory"), new XElement("SelectedTaskDescription", "inventory")));
        var recognitionPath = Path.Combine(root, "Extension_CardRecognition_ReadOnly.rfPrj");
        RfPrjArchive.Save(recognitionPath, recognition);
        var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.CardRecognitionReadOnly, "Erkennung", recognitionPath, true, false, true, recognition.Summarize());
        var started = DateTimeOffset.UtcNow;
        var session = new RFiDGearOperationSession(operation,
            new RFiDGearLaunchResult(42, "RFiDGear.exe", recognitionPath, true, false, "AUTORUN=1"),
            new RFiDGearLogSnapshot(started, root, new Dictionary<string, long>()), started, TimeSpan.FromSeconds(30));
        var allowedLog = "[INF] {\"Stage\":\"Task.Outcome\",\"Details\":{\"RunId\":\"extension\",\"TaskPosition\":0,\"TaskId\":\"10\",\"TaskType\":\"PICCApplicationInventory\",\"Outcome\":\"Successful\",\"ErrorCode\":\"NoError\",\"IO\":{\"PICCApplicationIds\":[\"F482D0\",\"F51773\",\"F52348\"],\"PICCKeySettings\":4,\"PICCCreateDeleteWithoutMasterKey\":true}}}\n" +
            "[INF] {\"Stage\":\"TaskLoop.Summary\",\"Details\":{\"RunId\":\"extension\",\"TotalTasks\":1,\"Executed\":1}}\n" +
            "[INF] {\"Stage\":\"TaskLoop.Success\",\"Details\":{\"RunId\":\"extension\"}}";
        var allowed = new CardRecognitionService().Create(root, operation,
            RFiDGearOperationService.EvaluateText(session, allowedLog, started.AddSeconds(1))).Report;
        Assert(allowed.ExtensionEligible && allowed.CreateDeleteWithoutMasterKey == true && allowed.ExtensionTargetAids.SequenceEqual([targetAid]),
            "Read-only PICC-Fähigkeitsprüfung gibt eine zulässige, konfliktfreie Erweiterung nicht frei.");
        var deniedLog = allowedLog.Replace("\"PICCKeySettings\":4,\"PICCCreateDeleteWithoutMasterKey\":true", "\"PICCKeySettings\":0,\"PICCCreateDeleteWithoutMasterKey\":false", StringComparison.Ordinal);
        var denied = new CardRecognitionService().Create(root, operation,
            RFiDGearOperationService.EvaluateText(session, deniedLog, started.AddSeconds(2))).Report;
        Assert(!denied.ExtensionEligible, "PICC ohne freie Create/Delete-Berechtigung wird fälschlich zur Erweiterung freigegeben.");
        var occupiedLog = allowedLog.Replace("\"F52348\"", "\"F52348\",\"" + targetAid + "\"", StringComparison.Ordinal);
        var occupied = new CardRecognitionService().Create(root, operation,
            RFiDGearOperationService.EvaluateText(session, occupiedLog, started.AddSeconds(3))).Report;
        Assert(!occupied.ExtensionEligible, "Bereits vorhandene Ziel-AID wird fälschlich zum Überschreiben freigegeben.");
    }
    finally { Directory.Delete(root, true); }
}

static void BuildHardwareExtensionFixture(string outputDirectory)
{
    var root = Path.GetFullPath(outputDirectory);
    Directory.CreateDirectory(root);
    var library = ProfileLibrary.Load(Path.Combine(Directory.GetCurrentDirectory(), "assets", "profiles"));
    var shells = TaskShellLibrary.Load(Path.Combine(Directory.GetCurrentDirectory(), "assets", "task-shells"));
    var project = ValidProject();
    project.Project.Name = "Hardware-Erweiterung-DEVK";
    project.Project.OutputDirectory = root;
    project.Project.CardMode = CardOperationMode.ExtendExisting;
    project.Project.Build = ["Mastering", "Coding"];
    project.Profiles.Customers = ["DEVK"];
    project.Values["firmCode"] = "1234567890";
    var composition = library.Compose(project.Profiles.Base, project.Profiles.Customers);
    var additions = CardOperationMode.SelectApplications(composition, project);
    foreach (var application in additions)
    {
        var aid = ProfileValidator.NormalizeAid(application.Id);
        project.Productive.ApplicationKeys[aid] = Enumerable.Range(0, application.KeyCount)
            .Select(_ => KeyVaultService.GenerateKey(application.KeyType)).ToList();
    }
    project.Productive.PiccKey.Value = "";
    var issues = ProjectContractValidator.Validate(project, library, requireProductiveSecrets: true);
    if (issues.Count > 0) throw new InvalidDataException(string.Join("; ", issues.Select(issue => issue.Code + ": " + issue.Message)));

    Directory.CreateDirectory(Path.Combine(root, "payloads"));
    Directory.CreateDirectory(Path.Combine(root, "verification"));
    Directory.CreateDirectory(Path.Combine(root, "readback"));
    Directory.CreateDirectory(Path.Combine(root, "readback_mastering"));
    MasteringInitializationService.Prepare(composition, root);
    var main = new MainProjectFactory(shells);
    var support = new SupportProjectFactory(shells);
    var name = project.Project.Name;
    RfPrjArchive.Save(Path.Combine(root, name + "_Mastering.rfPrj"), main.BuildMastering(composition, project, root));
    RfPrjArchive.Save(Path.Combine(root, name + "_Kodierung.rfPrj"), main.BuildCoding(composition, project, root));
    RfPrjArchive.Save(Path.Combine(root, name + "_CardRecognition_ReadOnly.rfPrj"), support.BuildCardRecognition(project, includeProductive: false));
    RfPrjArchive.Save(Path.Combine(root, name + "_CardPreflight_Transport.rfPrj"), support.BuildCardPreflight(composition, project, root, productive: false));
    RfPrjArchive.Save(Path.Combine(root, name + "_CardPreflight_Productive.rfPrj"), support.BuildCardPreflight(composition, project, root, productive: true));
    RfPrjArchive.Save(Path.Combine(root, name + "_Acceptance_Application_Productive.rfPrj"), support.BuildApplicationAcceptance(composition, project));
    RfPrjArchive.Save(Path.Combine(root, name + "_Erweiterung_Rueckbau.rfPrj"), support.BuildExtensionRollback(composition, project));
    var sourcePath = Path.Combine(root, name + ".project.json");
    ProjectJson.Save(sourcePath, ProjectJson.WithoutAllSecrets(project));
    ContractArtifactFactory.SaveAll(root, ContractArtifactFactory.Build(new ContractArtifactContext(project, composition, sourcePath, "hardware-test", DateTime.Now)));
    var sessions = new CodingSessionService();
    using (sessions.Prepare(sessions.Load(root), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["devkCardNumber"] = "123456"
    })) { }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        projectDirectory = root,
        targetAids = additions.Select(application => ProfileValidator.NormalizeAid(application.Id)).ToArray(),
        mastering = Path.Combine(root, name + "_Mastering.rfPrj"),
        transportPreflight = Path.Combine(root, name + "_CardPreflight_Transport.rfPrj"),
        coding = Path.Combine(root, name + "_Kodierung.rfPrj"),
        productivePreflight = Path.Combine(root, name + "_CardPreflight_Productive.rfPrj"),
        acceptance = Path.Combine(root, name + "_Acceptance_Application_Productive.rfPrj"),
        rollback = Path.Combine(root, name + "_Erweiterung_Rueckbau.rfPrj"),
        recognition = Path.Combine(root, name + "_CardRecognition_ReadOnly.rfPrj")
    }, ProjectJson.Options));
}

static void UpgradeHardwareExtensionRollback(string outputDirectory)
{
    var root = Path.GetFullPath(outputDirectory);
    var acceptancePath = Directory.GetFiles(root, "*_Acceptance_Application_Productive.rfPrj", SearchOption.TopDirectoryOnly).Single();
    var rollbackPath = Directory.GetFiles(root, "*_Erweiterung_Rueckbau.rfPrj", SearchOption.TopDirectoryOnly).Single();
    var acceptance = RfPrjArchive.Load(acceptancePath);
    var rollback = RfPrjArchive.Load(rollbackPath);
    var source = acceptance.Tasks.FirstOrDefault(task => TaskXml.Get(task, "SelectedTaskType") == "ReadAppSettings")
        ?? throw new InvalidDataException("Produktive Anwendungsabnahme besitzt keinen ReadAppSettings-Task.");
    var target = rollback.Tasks.Single();
    foreach (var field in new[]
    {
        "SelectedDesfireAppKeyEncryptionTypeCurrent",
        "SelectedDesfireAppKeyNumberCurrent",
        "DesfireAppKeyCurrent",
        "DesfireAppKeyCurrentOld",
        "DesfireAppKeyTarget"
    })
    {
        var value = TaskXml.Get(source, field);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Produktiver Rückbauschlüssel fehlt in Feld {field}.");
        TaskXml.Set(target, field, value);
    }
    RfPrjArchive.Save(rollbackPath, rollback);
    Console.WriteLine(JsonSerializer.Serialize(new { rollback = rollbackPath, targetKeyEmbedded = true }, ProjectJson.Options));
}

static void TestCodingRunEvaluation()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-run-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var path = Path.Combine(directory, "verify.hex");
        var expectation = new VerificationExpectation("verify.hex", path, [0x01, 0x02], 0);
        Assert(CodingRunEvaluator.Evaluate([expectation], "").State == CodingRunState.Waiting, "Fehlender Readback wurde nicht als wartend erkannt.");
        File.WriteAllText(path, "FF 02"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
        var mismatch = CodingRunEvaluator.Evaluate([expectation], "");
        Assert(mismatch.State == CodingRunState.Failed && mismatch.FailureCategory == "Readback / Verifikation", "Readback-Abweichung wurde nicht als Fehler erkannt.");
        File.WriteAllText(path, "01 02"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(3));
        Assert(CodingRunEvaluator.Evaluate([expectation], "").State == CodingRunState.Successful, "Bytegleicher neuer Readback wurde nicht als Erfolg erkannt.");
        var taskMap = new[]
        {
            new RfPrjTaskSummary(0, "10", "WriteData", "write", "", "Empty", "0xAABBCC", "0"),
            new RfPrjTaskSummary(1, "20", "ReadData", "read", "10", "NoError", "0xAABBCC", "0")
        };
        var failure = CodingRunEvaluator.Evaluate([expectation], "[ERR] {\"Stage\":\"TaskLoop.Failure\",\"Details\":{\"CurrentTaskIndex\":1}}", taskMap);
        Assert(failure.State == CodingRunState.Failed && failure.Task?.Type == "ReadData", "Starker Logfehler hat Readback-Erfolg nicht überstimmt oder wurde nicht dem Task zugeordnet.");
        var incompleteAnalysis = CodingRunOperatorAnalyzer.Analyze(CodingRunEvaluator.Evaluate([expectation], "", taskMap), taskMap.Length);
        Assert(!incompleteAnalysis.CanReleaseCard && incompleteAnalysis.Code == "EVIDENCE_INCOMPLETE" && incompleteAnalysis.NextAction.Contains("nicht als erfolgreich", StringComparison.OrdinalIgnoreCase),
            "Bytegleicher Readback ohne autoritativen Laufnachweis wird Laien fälschlich als Freigabe dargestellt.");
        var incompleteEvaluation = CodingRunEvaluator.Evaluate([expectation], "", taskMap);
        var gateStartedAt = new DateTimeOffset(2026, 8, 27, 18, 57, 40, TimeSpan.Zero);
        var pendingTerminal = CodingRunFinalizationGate.Decide(incompleteEvaluation, incompleteAnalysis, true, null, gateStartedAt);
        Assert(pendingTerminal.WaitForTerminalEvidence && pendingTerminal.PendingSince == gateStartedAt,
            "Bytegleicher Readback schließt den Lauf ab, obwohl der laufende RFiDGear-Prozess den Terminalstatus noch nachliefert.");
        var stillPending = CodingRunFinalizationGate.Decide(incompleteEvaluation, incompleteAnalysis, true,
            pendingTerminal.PendingSince, gateStartedAt.AddMilliseconds(36));
        Assert(stillPending.WaitForTerminalEvidence,
            "Das gemessene 36-ms-Nachlaufen des RFiDGear-Terminalstatus wird nicht sicher überbrückt.");
        var graceExpired = CodingRunFinalizationGate.Decide(incompleteEvaluation, incompleteAnalysis, true,
            pendingTerminal.PendingSince, gateStartedAt.AddSeconds(6));
        Assert(!graceExpired.WaitForTerminalEvidence && graceExpired.PendingSince is null,
            "Ein dauerhaft fehlender Terminalstatus hält den Datensatz unbegrenzt offen.");
        var processEnded = CodingRunFinalizationGate.Decide(incompleteEvaluation, incompleteAnalysis, false,
            pendingTerminal.PendingSince, gateStartedAt.AddMilliseconds(36));
        Assert(!processEnded.WaitForTerminalEvidence,
            "Ein bereits beendeter RFiDGear-Prozess wird ohne Terminalstatus fälschlich weiter abgewartet.");
        const string successfulLog = """
        [INF] {"Stage":"Task.Outcome","RunId":"run-1","TaskPosition":0,"TaskId":"10","TaskType":"WriteData","Outcome":"Successful","ErrorCode":"NoError"}
        [INF] {"Stage":"Task.Outcome","RunId":"run-1","TaskPosition":1,"TaskId":"20","TaskType":"ReadData","Outcome":"Successful","ErrorCode":"NoError"}
        [INF] {"Stage":"TaskLoop.Success","RunId":"run-1"}
        """;
        var released = CodingRunOperatorAnalyzer.Analyze(CodingRunEvaluator.Evaluate([expectation], successfulLog, taskMap), taskMap.Length);
        Assert(released.CanReleaseCard && released.Code == "RELEASED" && released.EvidenceSummary.Contains("2/2 Tasks", StringComparison.Ordinal),
            "Vollständiger korrelierter Tasklauf plus bytegleicher Readback erzeugt keine verständliche Kartenfreigabe.");
        var releasedEvaluation = CodingRunEvaluator.Evaluate([expectation], successfulLog, taskMap);
        Assert(!CodingRunFinalizationGate.Decide(releasedEvaluation, released, true, pendingTerminal.PendingSince,
                gateStartedAt.AddMilliseconds(36)).WaitForTerminalEvidence,
            "Ein bereits autoritativ erfolgreicher Lauf wird durch das Nachlaufffenster unnötig verzögert.");
        var terminalWithoutReadback = CodingRunEvaluator.Evaluate(
            [new VerificationExpectation("missing.hex", Path.Combine(directory, "missing.hex"), [0x01], 0)], successfulLog, taskMap);
        var finalizedWithoutReadback = CodingRunProcessExitPolicy.Finalize(terminalWithoutReadback);
        Assert(finalizedWithoutReadback.State == CodingRunState.Failed &&
               finalizedWithoutReadback.FailureCategory == "Readback / Verifikation" &&
               finalizedWithoutReadback.StructuredRun is { Correlated: true, TerminalEvent: "TaskLoop.Success" } &&
               finalizedWithoutReadback.Verification is { Waiting: 1 },
            "Ein terminal erfolgreicher Engine-Lauf verliert beim Prozessende seine Korrelation, wenn finale Readbacks fehlen.");
        var readerFailure = CodingRunEvaluator.Evaluate([expectation], "[ERR] TransportError while reader disconnected", taskMap);
        var readerAnalysis = CodingRunOperatorAnalyzer.Analyze(readerFailure, taskMap.Length);
        Assert(!readerAnalysis.CanReleaseCard && readerAnalysis.NextAction.Contains("Leser", StringComparison.OrdinalIgnoreCase) && readerAnalysis.NextAction.Contains("denselben Datensatz", StringComparison.OrdinalIgnoreCase),
            "Readerfehler liefert keine konkrete sichere Bedienhandlung.");
        var missingPayloadPath = Path.Combine(directory, "missing-payload.hex");
        var payloadTaskMap = new[]
        {
            new RfPrjTaskSummary(0, "10", "WriteData", "Payload schreiben", "", "Empty", "0xAABBCC", "0", missingPayloadPath),
            new RfPrjTaskSummary(1, "20", "ReadData", "Payload lesen", "10", "NoError", "0xAABBCC", "0")
        };
        const string missingPayloadLog = """
        [INF] {"Stage":"Task.Outcome","RunId":"payload-run","TaskPosition":0,"TaskId":"10","TaskType":"WriteData","Decision":"Executed","Outcome":"Failed","ErrorCode":"Unknown"}
        [INF] {"Stage":"Task.Outcome","RunId":"payload-run","TaskPosition":1,"TaskId":"20","TaskType":"ReadData","Decision":"Skipped","Outcome":"Skipped","ErrorCode":"Empty"}
        [INF] {"Stage":"TaskLoop.Summary","RunId":"payload-run","TotalTasks":2,"Executed":2,"Failed":1,"Successful":0}
        [INF] {"Stage":"TaskLoop.Failure","RunId":"payload-run"}
        """;
        var payloadFailure = CodingRunEvaluator.Evaluate([expectation], missingPayloadLog, payloadTaskMap);
        var payloadAnalysis = CodingRunOperatorAnalyzer.Analyze(payloadFailure, payloadTaskMap.Length);
        Assert(payloadFailure.State == CodingRunState.Failed && payloadFailure.FailureCategory == "Eingabedaten / Payload" &&
               payloadFailure.Task?.Index == "10" && payloadFailure.Detail.Contains("missing-payload.hex", StringComparison.OrdinalIgnoreCase),
            "Fehlende WriteData-Payload wird nicht konkret aus strukturierter Runtime-Evidenz erklärt.");
        Assert(payloadAnalysis.NextAction.Contains("Datensatz", StringComparison.OrdinalIgnoreCase) &&
               payloadAnalysis.NextAction.Contains("Payload", StringComparison.OrdinalIgnoreCase),
            "Fehlende Payload liefert keine laienverständliche sichere Bedienhandlung.");
        var stale = new VerificationExpectation("verify.hex", path, [0x01, 0x02], new FileInfo(path).LastWriteTimeUtc.Ticks);
        Assert(CodingRunEvaluator.Evaluate([stale], "").State == CodingRunState.Waiting, "Alter Readback wurde fälschlich wiederverwendet.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestCodingSeries()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-series-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        Assert(CodingSeriesService.NextSequential("0099", "2") == "0101", "Fortlaufende Nummer verliert Breite oder Schrittweite.");
        AssertThrows<InvalidDataException>(() => CodingSeriesService.NextSequential("A099", "1"));
        var listPath = Path.Combine(directory, "cards.csv");
        File.WriteAllText(listPath, "cardNumber;badge\n\"0001\";A\n0002;B\n");
        var rows = CodingSeriesService.LoadList(listPath, ["cardNumber", "badge"], "cardNumber");
        Assert(rows.Count == 2 && rows[0]["cardNumber"] == "0001" && rows[1]["badge"] == "B", "CSV-Kodierliste wurde nicht korrekt geladen.");

        var started = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
        var keySetIdentity = new string('A', 64);
        var service = new CodingSeriesService(directory, "Serie", CodingSeriesMode.Sequential, started, keySetIdentity);
        service.Append(new CodingSeriesRecord(started.AddMinutes(1), "0001", CodingSeriesResult.Successful, "", "OK", new Dictionary<string, string> { ["cardNumber"] = "0001" }));
        service.Append(new CodingSeriesRecord(started.AddMinutes(2), "0002", CodingSeriesResult.Failed, "Reader / Transport", "Fehler", new Dictionary<string, string> { ["cardNumber"] = "0002" }));
        service.Append(new CodingSeriesRecord(started.AddMinutes(3), "0002", CodingSeriesResult.Skipped, "", "Bewusst übersprungen", new Dictionary<string, string> { ["cardNumber"] = "0002" }));
        Assert(service.IsKnownSuccessful("0001") && !service.IsKnownSuccessful("0002"), "Duplikatschutz unterscheidet Erfolg und Fehler/Skip nicht.");
        var summary = service.ExportSummary(started.AddHours(1));
        Assert(summary.Successful == 1 && summary.Failed == 1 && summary.Skipped == 1, "Serienzusammenfassung zählt Zustände falsch.");
        var summaryText = File.ReadAllText(summary.JsonPath);
        Assert(summaryText.Contains("\"containsKeys\": false", StringComparison.Ordinal) && summaryText.Contains($"\"keySetIdentity\": \"{keySetIdentity}\"", StringComparison.Ordinal) &&
               !summaryText.Contains("productivePicc", StringComparison.OrdinalIgnoreCase), "Serienprotokoll ist nicht explizit keywertfrei oder nicht dem Keyset zugeordnet.");
        var reopened = new CodingSeriesService(directory, "Serie", CodingSeriesMode.Single, started.AddDays(1));
        Assert(reopened.IsKnownSuccessful("0001") && !reopened.IsKnownSuccessful("0002"), "Erfolgreiche Nummer aus früherer JSONL-Session wurde nicht geladen.");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestDiagnosticBundle()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-diagnostic-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        File.WriteAllText(Path.Combine(directory, "builder.manifest.json"), "{\"projectName\":\"Demo\"}");
        File.WriteAllText(Path.Combine(directory, "acceptance.plan.json"), "{\"projectName\":\"Demo\"}");
        File.WriteAllText(Path.Combine(directory, "Demo.project.json"), "{\"productivePicc\":\"00112233445566778899AABBCCDDEEFF\"}");
        var document = RfPrjDocument.Create();
        var task = new XElement("anyType");
        TaskXml.Set(task, "CurrentTaskIndex", "10"); TaskXml.Set(task, "SelectedTaskType", "ReadData");
        TaskXml.Set(task, "SelectedTaskDescription", "Final read"); TaskXml.Set(task, "AppNumberCurrent", "0xF482D0"); TaskXml.Set(task, "FileNumberCurrent", "0");
        document.Append(task);
        var projectPath = Path.Combine(directory, "Demo_Kodierung.rfPrj");
        RfPrjArchive.Save(projectPath, document);
        File.WriteAllText(Path.Combine(directory, "coding.schema.json"), "{\"projectName\":\"Demo\",\"codingProject\":\"Demo_Kodierung.rfPrj\",\"finalVerificationDirectory\":\"verification\"}");
        var sessionDirectory = Path.Combine(directory, "sessions"); Directory.CreateDirectory(sessionDirectory);
        var sessionPath = Path.Combine(sessionDirectory, "coding_test.jsonl"); File.WriteAllText(sessionPath, "{\"number\":\"0001\",\"result\":\"Fehler\"}\n");
        var verification = Path.Combine(directory, "verification"); Directory.CreateDirectory(verification); File.WriteAllText(Path.Combine(verification, "read.hex"), "00 00");
        var logs = Path.Combine(directory, "logs"); Directory.CreateDirectory(logs);
        var monitor = new RFiDGearLogMonitor(); var snapshot = monitor.Snapshot(logs);
        File.WriteAllText(Path.Combine(logs, "log-test.txt"), "[ERR] {\"Stage\":\"TaskLoop.Failure\",\"Key\":\"00112233445566778899AABBCCDDEEFF\",\"Details\":{\"CurrentTaskIndex\":0}}");
        var current = new CodingSeriesRecord(DateTimeOffset.Now, "0001", CodingSeriesResult.Failed, "Authentifizierung / Key", "Test", new Dictionary<string, string> { ["cardNumber"] = "0001" });
        var bundle = new DiagnosticBundleService().Create(new DiagnosticBundleRequest(directory, "11.0-preview28-test", "2.0.7.0", snapshot, sessionPath, current, projectPath), new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
        Assert(File.Exists(bundle.Path) && !bundle.ContainsKeys && bundle.ContainsCardData, "Diagnosepaket-Vertrag ist falsch.");
        using var archive = ZipFile.OpenRead(bundle.Path);
        var names = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        Assert(names.Contains("diagnostic.metadata.json") && names.Contains("task-map.json") && names.Contains("session.jsonl") && names.Contains("verification/read.hex"), "Diagnosepaket ist unvollständig.");
        Assert(!names.Any(name => name.EndsWith(".rfPrj", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".project.json", StringComparison.OrdinalIgnoreCase) || name.Contains("vault", StringComparison.OrdinalIgnoreCase)), "Diagnosepaket enthält verbotene Projekt-/Keydateien.");
        using var reader = new StreamReader(archive.GetEntry("rfidgear.log.sanitized.txt")!.Open());
        Assert(!reader.ReadToEnd().Contains("00112233445566778899AABBCCDDEEFF", StringComparison.Ordinal), "Diagnoselog enthält Schlüsselmaterial.");
        var unsafeDocument = RfPrjDocument.Create();
        var unsafeTask = new XElement("anyType");
        TaskXml.Set(unsafeTask, "CurrentTaskIndex", "10"); TaskXml.Set(unsafeTask, "SelectedTaskType", "ReadData");
        TaskXml.Set(unsafeTask, "SelectedTaskDescription", "00112233445566778899AABBCCDDEEFF");
        unsafeDocument.Append(unsafeTask); RfPrjArchive.Save(projectPath, unsafeDocument);
        AssertThrows<InvalidDataException>(() => new DiagnosticBundleService().Create(new DiagnosticBundleRequest(directory, "test", "test", CodingProjectPath: projectPath), new DateTimeOffset(2026, 8, 13, 12, 1, 0, TimeSpan.Zero)));
    }
    finally { Directory.Delete(directory, true); }
}

static void TestEnvironmentCheck()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-environment-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(directory);
        var secret = new string('A', 32);
        var settings = Path.Combine(directory, "release", "builder.settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "{\"productiveKey\":\"" + secret + "\"}");
        var request = new EnvironmentCheckRequest(
            Path.Combine(directory, "release"), settings, Path.Combine(directory, "missing-projects"),
            Path.Combine(directory, "missing-RFiDGear.exe"), Path.Combine(directory, "missing-profiles"),
            Path.Combine(directory, "missing-shells"), Path.Combine(directory, "release", "START_SieEncode.cmd"),
            [Path.Combine(directory, "release", "README.md")]);
        var service = new EnvironmentCheckService();
        var inspected = service.Inspect(request, new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
        Assert(inspected.Summary.Errors >= 5 && inspected.Checks.Any(item => item.Subject == "Versionsunabhängiger Speicherort" && item.Status == "Fehler"),
            "Umgebungstest erkennt fehlende Bestandteile oder Einstellungen im Release-Verzeichnis nicht.");
        var files = service.CreateReport(request, Path.Combine(directory, "reports"), new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
        var corpus = File.ReadAllText(files.TextPath) + File.ReadAllText(files.JsonPath);
        Assert(!corpus.Contains(secret, StringComparison.Ordinal), "Umgebungsbericht gibt Inhalte der Einstellungsdatei aus.");
        Assert(!Directory.EnumerateFiles(Path.GetDirectoryName(files.TextPath)!, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporäre Umgebungsberichtdatei blieb zurück.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void TestHardwareAcceptancePlan()
{
    var directory = Path.Combine(Path.GetTempPath(), "rfidgear-hardware-plan-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(directory);
        var secret = new string('B', 32);
        File.WriteAllText(Path.Combine(directory, "Demo.project.json"), "{\"productiveKey\":\"" + secret + "\"}");
        var coding = Path.Combine(directory, "Demo_Kodierung.rfPrj");
        File.WriteAllText(coding, "placeholder");
        var preflight = new ProjectPreflightReport { ProjectName = "Demo", Projects = { new InspectedProject("Demo.rfPrj", "2.0.6", 1, true, new Dictionary<string, int>()) } };
        var task = new RfPrjTaskSummary(0, "10", "ReadData", "read-only", "", "Empty", "0xF482D0", "0");
        var operations = new RFiDGearOperationCatalog(directory,
        [
            new(RFiDGearOperationKind.MasteringAutorun, "Mastering", Path.Combine(directory, "master.rfPrj"), false, true, true, [task]),
            new(RFiDGearOperationKind.CardPreflightTransport, "Transport", Path.Combine(directory, "transport.rfPrj"), true, false, true, [task]),
            new(RFiDGearOperationKind.CardPreflightProductive, "Productive", Path.Combine(directory, "productive.rfPrj"), true, false, true, [task])
        ]);
        var version = new RFiDGearVersionInfo("RFiDGear.exe", "2.0.10.0", new Version(2, 0, 10, 0), true);
        var capabilities = RFiDGearCapabilityInspector.AnalyzeEvidence("RFiDGear.exe", version, [], token => token is "CUSTOMPROJECTFILE" or "AUTORUN" or "AUTOMODE" or "ReadAppSettings" or "IsAppSettingsExactMatch" or "CheckAppKeyCount" or "CheckFileSettings" or "Task.Outcome" or "TaskLoop.Summary" or "RunId");
        var reader = new RFiDGearReaderSettingsInfo("settings.xml", true, "Elatec", "USB", "TWN4", true, null);
        var request = new HardwareAcceptancePlanRequest("11.0-test", "Demo", directory, Path.Combine(directory, "Demo.project.json"), false, coding, preflight, operations, capabilities, reader);
        var service = new HardwareAcceptancePlanService();
        var plan = service.Inspect(request, new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero));
        Assert(!plan.Readiness.Any(item => item.Status == "Blockiert") && plan.Steps.Count == 10, "Vollständiger Softwarestand wird nicht als hardwarebereit geplant.");
        Assert(plan.Steps.Where(step => step.Risk.Contains("Karte", StringComparison.OrdinalIgnoreCase) || step.Risk.Contains("DESTRUKTIV", StringComparison.Ordinal)).All(step => step.RequiresFreshTestChipApproval), "Kartenaktion fehlt die frische Freigabeanforderung.");
        Assert(plan.Steps.Single(step => step.Risk.Contains("DESTRUKTIV", StringComparison.Ordinal)).Optional, "Destruktive Recovery ist nicht klar optional.");
        Assert(plan.EvidenceGaps.Any(gap => gap.Contains("rfPrj-Hash-Korrelation", StringComparison.Ordinal)) &&
               plan.EvidenceGaps.Any(gap => gap.Contains("CheckFileSettings", StringComparison.Ordinal)) &&
               plan.EvidenceGaps.Any(gap => gap.Contains("#207", StringComparison.Ordinal)),
            "Aktuelle Builder-/Upstream-Evidenzgrenzen fehlen.");
        var files = service.CreateReport(request, Path.Combine(directory, "reports"), new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero));
        var corpus = File.ReadAllText(files.TextPath) + File.ReadAllText(files.JsonPath);
        Assert(!corpus.Contains(secret, StringComparison.Ordinal) && corpus.Contains("frische Testchipfreigabe", StringComparison.OrdinalIgnoreCase), "Hardwareplan ist nicht keyfrei oder benennt die Freigabe nicht.");
        var blocked = service.Inspect(request with { ArtifactOnly = true, CodingProjectPath = "" });
        Assert(blocked.Status.StartsWith("Nicht bereit", StringComparison.Ordinal) && blocked.Readiness.Count(item => item.Status == "Blockiert") >= 2, "Artifact-only/missing coding blockers are not fail-closed.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

static void TestRealAssets(string profileDirectory, string projectTemplate, string taskShellDirectory)
{
    var library = ProfileLibrary.Load(profileDirectory);
    var issues = library.Validate();
    Assert(issues.Count == 0, string.Join(" | ", issues.Select(issue => issue.Message)));
    Assert(library.Profiles.Count(profile => profile.Profile.Kind == "base") == 1, "Masterprofilanzahl weicht ab.");
    var project = ProjectJson.Load(projectTemplate);
    var baseOnly = library.Compose(project.Profiles.Base, project.Profiles.Customers);
    Assert(baseOnly.Profiles.Count == 1 && baseOnly.Applications.Count == 3 && baseOnly.Variables.Count == 2, "Grundprofil-Topologie weicht von v10.39 ab.");
    Assert(baseOnly.Applications.Sum(application => application.Files.Count) == 6, "Grundprofil muss sechs Files enthalten.");
    var withCustomer = library.Compose(project.Profiles.Base, ["DEVK"]);
    Assert(withCustomer.Profiles.Count == 2 && withCustomer.Applications.Count == 4 && withCustomer.Variables.Count == 4, "Additives Kundenprofil weicht von v10.39 ab.");
    Assert(withCustomer.Applications.Sum(application => application.Files.Count) == 11, "Grundprofil plus DEVK muss elf Files enthalten.");
    var realDiff = new ProfileDiffService().Compare(baseOnly, withCustomer, "Grundprofil", "Grundprofil + DEVK");
    Assert(realDiff.Added == 9 && realDiff.Removed == 0 && realDiff.Changed == 0,
        "Realer Grundprofil-plus-DEVK-Diff muss Profil, zwei Variablen, Application und fünf Files als additive Elemente zeigen.");

    project.Profiles.Customers = ["DEVK"];
    project.Project.Build = ["Coding"];
    var missingSecrets = ProjectContractValidator.Validate(project, library, requireProductiveSecrets: true);
    Assert(missingSecrets.Any(issue => issue.Code == "PRODUCTIVE_PICC"), "Fehlender produktiver PICC-Key wurde nicht erkannt.");
    Assert(missingSecrets.Count(issue => issue.Code.StartsWith("PRODUCTIVE_APP_KEY", StringComparison.Ordinal)) == 10, "Fehlende produktive Application Keys wurden nicht vollständig erkannt.");
    Assert(missingSecrets.Any(issue => issue.Code == "PROJECT_VALUE_REQUIRED"), "Fehlender Firmencode wurde nicht erkannt.");

    project.Productive.PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('A', 32) };
    project.Values["firmCode"] = "1234567890";
    foreach (var application in withCustomer.Applications)
        project.Productive.ApplicationKeys[application.Id] = Enumerable.Repeat(new string('B', application.KeyType == "DF_KEY_3K3DES" ? 48 : 32), application.KeyCount).ToList();
    Assert(ProjectContractValidator.Validate(project, library, requireProductiveSecrets: true).Count == 0, "Vollständiges Coding-Projekt wurde abgelehnt.");

    var contracts = ContractArtifactFactory.Build(new ContractArtifactContext(project, withCustomer, projectTemplate, "11.0-test", new DateTime(2026, 1, 2, 3, 4, 5)));
    Assert(contracts.Count == 8 && contracts.ContainsKey("coding.schema.json") && contracts.ContainsKey("rfidgear.host-contract.json") && contracts.ContainsKey("card-recognition.plan.json"), "Vertragsdateisatz ist unvollständig.");
    Assert(contracts["rfidgear.host-contract.json"]["schemaVersion"]!.GetValue<string>() == "1.1" &&
           contracts["rfidgear.host-contract.json"]["contractId"]!.GetValue<string>() == "de.rfidgear.host.v1" &&
           contracts["rfidgear.host-contract.json"]["resultSemantics"]!["terminalEngineResultImmutable"]!.GetValue<bool>() &&
           contracts["rfidgear.host-contract.json"]["privacy"]!["requestContainsKeys"]!.GetValue<bool>() == false,
        "Versionierter RFiDGear-Hostvertrag fehlt oder verletzt den Keyausschluss.");
    Assert(contracts["coding.schema.json"]["schemaVersion"]!.GetValue<string>() == "1.2" &&
           contracts["coding.schema.json"]["keySetIdentity"]!.GetValue<string>().Length == 64,
        "Kodierschema enthält keine stabile, keyfreie Keyset-Zuordnung.");
    var contractText = string.Join("\n", contracts.Values.Select(document => document.ToJsonString()));
    Assert(!contractText.Contains(new string('A', 32), StringComparison.Ordinal) && !contractText.Contains(new string('B', 32), StringComparison.Ordinal), "Vertragsdatei enthält produktive Schlüsselwerte.");
    Assert(contracts["card-preflight.plan.json"]["files"]!.AsArray().Count == 11, "Karten-Preflight-Plan enthält nicht alle Files.");
    Assert(contracts["card-recognition.plan.json"]["readOnly"]!.GetValue<bool>() &&
           contracts["card-recognition.plan.json"]["piccCredentialProbes"]!.AsArray().Count == 4,
        "Transponder-Erkennungsvertrag ist nicht vollständig read-only definiert.");
    Assert(contracts["acceptance.plan.json"]["applications"]!.AsArray().Count == 4, "Abnahmeplan enthält nicht alle Applications.");
    Assert(contracts["coding.schema.json"]["payloads"]!.AsArray().Count == 4, "Kodierschema enthält nicht alle Payloads.");
    Assert(!contracts["project.definition.json"].ToJsonString().Contains("\"creationSettings\":null", StringComparison.Ordinal), "Optionale Profilfelder wurden als null materialisiert.");

    var shells = TaskShellLibrary.Load(taskShellDirectory);
    Assert(shells.TaskTypes.Count == 12, "Builder muss zwölf unterstützte Task-Shells enthalten.");
    foreach (var required in new[] { "AppExistCheck", "CreateApplication", "CreateFile", "WriteData", "ReadData", "CheckFileSettings", "ChangeFileSettings", "ApplicationKeyChangeover", "ApplicationKeySettingsChangeover", "PICCMasterKeyChangeover", "PICCMasterKeySettingsChangeover", "FormatDesfireCard" })
        Assert(shells.TaskTypes.Contains(required), $"Task-Shell fehlt: {required}");

    project.Project.Build = ["Mastering", "Coding"];
    var analysisDirectory = Path.Combine(Path.GetTempPath(), "rfidgear-analysis-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var analysis = new ProjectAnalysisService().AnalyzeAndSave(project, library, shells, analysisDirectory, new DateTime(2026, 8, 13, 12, 0, 0));
        Assert(analysis.Report.Errors == 0, "In-Memory-Projektanalyse meldet Inkonsistenzen: " + string.Join(" | ", analysis.Report.Issues.Select(issue => issue.Message)));
        Assert(analysis.Report.MasteringTasks.Count == 45 && analysis.Report.CodingTasks.Count == 33, "Projektanalyse zeigt nicht den vollständigen v10.39-Taskplan.");
        var analysisText = File.ReadAllText(analysis.TextPath) + File.ReadAllText(analysis.JsonPath);
        Assert(!analysisText.Contains(new string('A', 32), StringComparison.Ordinal) && !analysisText.Contains(new string('B', 32), StringComparison.Ordinal), "Projektanalysebericht enthält produktive Schlüsselwerte.");
        Assert(analysisText.Contains("\"containsKeys\": false", StringComparison.Ordinal) && !Directory.EnumerateFiles(analysisDirectory, "*.rfPrj", SearchOption.AllDirectories).Any(), "Projektanalyse ist nicht explizit keyfrei oder schrieb temporäre rfPrj-Dateien.");
        Assert(!Directory.EnumerateFiles(analysisDirectory, "*.tmp-*", SearchOption.AllDirectories).Any(), "Temporäre Analyseberichtdatei blieb zurück.");
    }
    finally { if (Directory.Exists(analysisDirectory)) Directory.Delete(analysisDirectory, true); }
    project.Project.Build = ["Coding"];

    var supportFactory = new SupportProjectFactory(shells);
    var transportRecovery = supportFactory.BuildRecovery(project, productive: false);
    var productiveRecovery = supportFactory.BuildRecovery(project, productive: true);
    var transportPreflight = supportFactory.BuildCardPreflight(withCustomer, project, Path.GetTempPath(), productive: false);
    var productivePreflight = supportFactory.BuildCardPreflight(withCustomer, project, Path.GetTempPath(), productive: true);
    var applicationAcceptance = supportFactory.BuildApplicationAcceptance(withCustomer, project);
    Assert(transportRecovery.Tasks.Count == 1 && transportRecovery.Summarize()[0].Type == "FormatDesfireCard", "Transport-Recovery-Taskplan ist falsch.");
    Assert(productiveRecovery.Tasks.Count == 2 && productiveRecovery.Summarize()[1].ConditionIndex == "10" && productiveRecovery.Summarize()[1].ConditionLevel == "NoError", "Produktiv-Recovery setzt den PICC-Key nicht bedingt zurück.");
    Assert(transportPreflight.Tasks.Count == 11 && transportPreflight.Summarize().All(task => task.Type == "ReadData"), "Transport-Karten-Preflight ist nicht strikt read-only oder unvollständig.");
    Assert(productivePreflight.Tasks.Count == 11 && productivePreflight.Summarize().All(task => task.Type == "ReadData"), "Produktiv-Karten-Preflight ist nicht strikt read-only oder unvollständig.");
    Assert(applicationAcceptance.Tasks.Count == withCustomer.Applications.Count * 2 &&
           applicationAcceptance.Summarize().Count(task => task.Type == "ReadAppSettings") == withCustomer.Applications.Count &&
           applicationAcceptance.Summarize().Count(task => task.Type == "CheckAppKeyCount") == withCustomer.Applications.Count &&
           applicationAcceptance.Summarize().All(task => task.Type is "ReadAppSettings" or "CheckAppKeyCount"),
        "Anwendungs-Abnahme enthält nicht genau die von RFiDGear 2.0.10 ausführbaren read-only Prüfaufgaben.");
    Assert(applicationAcceptance.Tasks.Where(task => TaskXml.Get(task, "SelectedTaskType") == "ReadAppSettings").All(task => TaskXml.Get(task, "IsAppSettingsExactMatch") == "true") &&
           applicationAcceptance.Tasks.All(task => TaskXml.Get(task, "SelectedDesfireAppKeyNumberCurrent") == "0"),
        "Kartenstruktur-Abnahme erzwingt nicht Exact-Match und produktiven Keyslot 0.");

    var mainFactory = new MainProjectFactory(shells);
    var mastering = mainFactory.BuildMastering(withCustomer, project, Path.GetTempPath());
    var coding = mainFactory.BuildCoding(withCustomer, project, Path.GetTempPath());
    var masteringTasks = mastering.Summarize();
    var codingTasks = coding.Summarize();
    Assert(masteringTasks.Count == 45, "Mastering muss für Grundprofil plus DEVK 45 Tasks enthalten.");
    Assert(masteringTasks.Count(task => task.Type == "CreateFile") == 11 && masteringTasks.Count(task => task.Type == "WriteData") == 11 && masteringTasks.Count(task => task.Type == "ReadData") == 11, "Mastering erzeugt nicht für jedes File Create/Zero-Fill/Readback.");
    Assert(codingTasks.Count == 33, "Coding muss für Grundprofil plus DEVK 33 Tasks enthalten.");
    Assert(codingTasks.Take(4).All(task => task.Type == "AppExistCheck"), "Coding beginnt nicht mit vollständigem AID-Preflight.");
    Assert(codingTasks.TakeLast(4).All(task => task.Type == "ReadData" && task.Description.Contains("Endprüfung", StringComparison.Ordinal)), "Coding endet nicht mit vier finalen Produktiv-Readbacks.");
    foreach (var application in withCustomer.Applications)
    {
        var changes = codingTasks.Where(task => task.Type == "ApplicationKeyChangeover" && task.Description.StartsWith(application.Name + ":", StringComparison.Ordinal)).ToArray();
        Assert(changes.Length == application.KeyCount && changes[^1].Description.Contains("Key 0 ", StringComparison.Ordinal), $"AID {application.Id}: Key0 wird nicht zuletzt geändert.");
    }

    var buildRoot = Path.Combine(Path.GetTempPath(), "rfidgear-build-service-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(buildRoot);
        project.Project.Name = "BuildServiceTest";
        project.Project.OutputDirectory = ".";
        project.Project.Build = ["Mastering", "Coding"];
        var sourcePath = Path.Combine(buildRoot, "BuildServiceTest.project.json");
        ProjectJson.Save(sourcePath, ProjectJson.WithoutProductiveSecrets(project));
        ProfileBindingService.BindCurrent(project, library);
        var expectedBuildDirectory = ProjectPaths.ResolveBuiltProject(project, buildRoot);
        using (var activeCodingLease = WorkspaceLease.Acquire(expectedBuildDirectory, "simulierte Kodiersitzung"))
            AssertThrows<IOException>(() => new ProjectBuildService().Build(project, library, shells, sourcePath, buildRoot, "11.0-preview28-test"));
        var result = new ProjectBuildService().Build(project, library, shells, sourcePath, buildRoot, "11.0-preview28-test");
        using (var afterBuild = WorkspaceLease.Acquire(result.ProjectDirectory, "Prüfung nach Build"))
            Assert(afterBuild.IsHeld, "Projektbuild gab die Arbeitsbereichs-Lease nicht frei.");
        Assert(result.Preflight.Status == "OK" && result.Preflight.Projects.Count == 8, "Zentraler C#-Buildservice besteht seinen Abschluss-Preflight nicht.");
        Assert(result.SessionSecrets.Count > 0, "Schlüsselfreier Build liefert keine flüchtige Referenzauflösung für die Laufzeit.");
        foreach (var protectedRfPrjPath in Directory.EnumerateFiles(result.ProjectDirectory, "*.rfPrj", SearchOption.TopDirectoryOnly))
        {
            var protectedDocument = RfPrjArchive.Load(protectedRfPrjPath);
            Assert(RfPrjSecretProtection.FindPlaintextSecretElements(protectedDocument).Count == 0,
                "rfPrj enthält weiterhin ein Klartext-Schlüsselfeld: " + protectedRfPrjPath);
        }
        var operationalPreflight = ProjectPreflight.Inspect(ProjectJson.Load(sourcePath), library, sourcePath, result.ProjectDirectory, ProjectPreflightScope.Operational);
        Assert(operationalPreflight.Status == "OK" && operationalPreflight.Scope == ProjectPreflightScope.Operational,
            "Schlüsselfreie Projektdefinition besteht den operativen Preflight nicht.");
        var strictKeylessPreflight = ProjectPreflight.Inspect(ProjectJson.Load(sourcePath), library, sourcePath, result.ProjectDirectory);
        Assert(strictKeylessPreflight.Issues.Any(issue => issue.Code.StartsWith("PRODUCTIVE", StringComparison.Ordinal)),
            "Strenger Build-Preflight akzeptiert eine keyfreie Coding-Konfiguration ohne Vaultdaten.");
        var artifactPreflight = ProjectPreflight.InspectArtifacts(result.ProjectDirectory);
        Assert(artifactPreflight.Status == "OK" && artifactPreflight.Scope == ProjectPreflightScope.Artifact,
            "Strukturelle Prüfung eines vollständigen Bauartefakts ist fehlgeschlagen.");
        var recognitionPlan = Path.Combine(result.ProjectDirectory, "card-recognition.plan.json");
        var recognitionProject = Path.Combine(result.ProjectDirectory, "BuildServiceTest_CardRecognition_ReadOnly.rfPrj");
        var recognitionPlanBytes = File.ReadAllBytes(recognitionPlan);
        var recognitionProjectBytes = File.ReadAllBytes(recognitionProject);
        File.Delete(recognitionPlan);
        File.Delete(recognitionProject);
        var legacyOperational = ProjectPreflight.Inspect(ProjectJson.Load(sourcePath), library, sourcePath, result.ProjectDirectory, ProjectPreflightScope.Operational);
        Assert(legacyOperational.Status == "OK" && legacyOperational.Limitations.Any(item => item.StartsWith("Kompatibler Altbuild:", StringComparison.Ordinal)) &&
               legacyOperational.Projects.Count == 7,
            "Ein ansonsten vollständiger Altbuild wird wegen des rein additiven RC15-Erkennungsmoduls vollständig gesperrt.");
        var legacyArtifact = ProjectPreflight.InspectArtifacts(result.ProjectDirectory);
        Assert(legacyArtifact.Status == "OK" && legacyArtifact.Limitations.Any(item => item.Contains("card-recognition.plan.json", StringComparison.Ordinal)),
            "Ein reines Altartefakt behandelt die fehlende additive Transponder-Erkennung nicht als dokumentierte Einschränkung.");
        File.WriteAllBytes(recognitionPlan, recognitionPlanBytes);
        File.WriteAllBytes(recognitionProject, recognitionProjectBytes);
        Assert(File.Exists(Path.Combine(result.ProjectDirectory, "coding.schema.json")) && File.Exists(Path.Combine(result.ProjectDirectory, "BuildServiceTest_Kodierung.rfPrj")), "Buildservice erzeugt Coding-Vertrag oder rfPrj nicht.");
        Assert(File.Exists(Path.Combine(result.ProjectDirectory, "BuildServiceTest_Acceptance_Application_Productive.rfPrj")), "Buildservice erzeugt das Application-Abnahmeprojekt nicht.");
        var secureRoot = Path.Combine(buildRoot, "local-sessions");
        string secureDirectory;
        using (var secure = new SecureOperationalSessionService().Materialize(project, library, shells, sourcePath, "1.0.2-test", secureRoot))
        {
            secureDirectory = secure.SessionDirectory;
            Assert(secure.ProjectDirectory.StartsWith(secureDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "Sicherer Betriebsbuild wurde nicht im lokalen Sitzungsverzeichnis erzeugt.");
            Assert(File.Exists(Path.Combine(secure.ProjectDirectory, "BuildServiceTest_Kodierung.rfPrj")) &&
                   File.Exists(Path.Combine(secureDirectory, ".sieencode-session")),
                "Sichere Betriebssitzung enthält nicht alle Laufartefakte oder keinen Löschmarker.");
            Assert(secure.Build.SessionSecrets.Count > 0 &&
                   RfPrjSecretProtection.FindPlaintextSecretElements(
                       RfPrjArchive.Load(Path.Combine(secure.ProjectDirectory, "BuildServiceTest_Kodierung.rfPrj"))).Count == 0,
                "Sichere Betriebssitzung hält Schlüssel nicht ausschließlich als flüchtige Referenzauflösung.");
        }
        Assert(!Directory.Exists(secureDirectory), "Temporärer produktiver Betriebsbuild blieb nach Sitzungsende erhalten.");
        var masteringInputs = RfPrjArchive.Load(Path.Combine(result.ProjectDirectory, "BuildServiceTest_Mastering.rfPrj")).Summarize()
            .Where(task => task.Type == "WriteData" && !string.IsNullOrWhiteSpace(task.InputPath)).ToArray();
        Assert(masteringInputs.Length == 11, "Mastering enthält nicht für jedes erwartete File eine Zero-Fill-Eingabe.");
        foreach (var input in masteringInputs)
        {
            Assert(File.Exists(input.InputPath), "Buildservice erzeugte die Mastering-Initialisierungsdatei nicht: " + input.InputPath);
            var compact = string.Concat(File.ReadAllText(input.InputPath).Where(character => !char.IsWhiteSpace(character)));
            var bytes = Convert.FromHexString(compact);
            Assert(bytes.Length == int.Parse(input.FileSize) && bytes.All(value => value == 0), "Mastering-Initialisierung ist nicht exakt größenrichtig nullbelegt: " + input.InputPath);
        }
        var sourceText = File.ReadAllText(sourcePath);
        Assert(!sourceText.Contains(new string('A', 32), StringComparison.Ordinal) && !sourceText.Contains(new string('B', 32), StringComparison.Ordinal), "Buildservice benötigt Produktivkeys in der gespeicherten Projektdefinition.");
        var migrated = new LegacyProjectMigrationService().Import(Path.Combine(result.ProjectDirectory, "project.definition.json"), library);
        Assert(migrated.Project.Profiles.Base == "Grundprofil-9.1" && migrated.Project.Profiles.Customers.SequenceEqual(["DEVK"]) && migrated.Project.Values["firmCode"] == "1234567890", "Reales Grundprofil-plus-DEVK-Bauartefakt lässt sich nicht verlustarm als keyfreier Entwurf migrieren.");
        Assert(migrated.Project.Project.Build.SequenceEqual(["Mastering"]) && migrated.Project.Productive.ApplicationKeys.Values.SelectMany(keys => keys).All(string.IsNullOrEmpty), "Reale Bauartefaktmigration übernimmt einen unsicheren Buildumfang oder Schlüsselwerte.");
    }
    finally { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); }

    var rfPrj = RfPrjDocument.Create();
    var createFile = shells.Create("CreateFile");
    var firstApplication = withCustomer.Applications[0];
    var firstFile = firstApplication.Files[0];
    TaskXml.SetMetadata(createFile, 30, "C# rfPrj smoke");
    TaskXml.SetApplication(createFile, firstApplication, project, firstApplication.TransportKeySettings);
    TaskXml.SetFile(createFile, firstFile);
    TaskXml.SetData(createFile, firstApplication.Id, firstFile.Id, new byte[firstFile.Size]);
    rfPrj.Append(createFile);
    var tempDirectory = Path.Combine(Path.GetTempPath(), "rfidgear-rfprj-tests-" + Guid.NewGuid().ToString("N"));
    var rfPrjPath = Path.Combine(tempDirectory, "smoke.rfPrj");
    try
    {
        RfPrjArchive.Save(rfPrjPath, rfPrj);
        var loaded = RfPrjArchive.Load(rfPrjPath);
        Assert(loaded.ManifestVersion == "2.0.6" && loaded.Tasks.Count == 1, "C#-rfPrj-Container ist nicht wieder lesbar.");
        var summary = loaded.Summarize()[0];
        Assert(summary.Index == "30" && summary.Type == "CreateFile" && summary.Aid == "0xF482D0" && summary.FileId == "0", "C#-Taskfelder gingen im rfPrj-Container verloren.");
        Assert(!Directory.EnumerateFiles(tempDirectory, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Temporärer rfPrj-Container blieb zurück.");

        var masteringPath = Path.Combine(tempDirectory, "real-mastering.rfPrj");
        RfPrjArchive.Save(masteringPath, mastering);
        var importService = new RfPrjProfileImportService();
        var import = importService.Analyze(masteringPath, library);
        Assert(import.Errors == 0 && import.Applications.Count == 4 && import.Applications.All(application => !application.SuggestedInclude && application.ExistingProfile.StartsWith("Entspricht ", StringComparison.Ordinal)),
            "Realer v10.39-Masteringplan wird nicht vollständig gegen vorhandene Profile erkannt.");
        Assert(import.Applications.Sum(application => application.Definition.Files.Count) == 11, "rfPrj-Import verliert reale Files.");
        var proposal = importService.CreateProposal(import, ["F482D5"], "Import-DEVK", "IDEVK", "Import DEVK", "1.0");
        Assert(ProfileValidator.Validate(proposal).Count == 0 && proposal.Applications.Single().Files.Count == 5 && proposal.Applications.Single().Files.All(file => file.Coding is null),
            "Geprüfter rfPrj-Profilvorschlag ist ungültig oder erfindet Kodierrezepte.");
        var importFiles = importService.SaveReport(import, Path.Combine(tempDirectory, "import-report"));
        var importCorpus = File.ReadAllText(importFiles.TextPath) + File.ReadAllText(importFiles.JsonPath);
        Assert(!importCorpus.Contains(new string('A', 32), StringComparison.Ordinal) && !importCorpus.Contains(new string('B', 32), StringComparison.Ordinal) && importCorpus.Contains("\"containsKeys\": false", StringComparison.Ordinal),
            "Realer rfPrj-Importbericht ist nicht nachweislich schlüsselwertfrei.");
    }
    finally { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true); }
}

static ProfileDefinition MinimalProfile(string id, string kind, string aid, VariableDefinition variable) => new()
{
    Profile = new ProfileMetadata { Id = id, Code = id, Name = id, Kind = kind, Version = "1.0" },
    Variables = [variable],
    Applications =
    [
        new ApplicationDefinition
        {
            Id = aid, Name = id, KeyType = "DF_KEY_AES", KeyCount = 1,
            TransportKeySettings = "0F", ProductiveKeySettings = "0F", ChangeKeyMode = "ChangeKeyUsingMK",
            Files =
            [
                new FileDefinition
                {
                    Id = 0, Name = "Data", Type = "StdDataFile", Size = 6, Communication = "CM_ENCRYPT",
                    Access = new AccessDefinition(),
                    Coding = new CodingDefinition { Output = aid + ".hex", Recipe = [new RecipeStep { Op = "asciiDigits", Source = "cardNumber", Width = 6, PadLeft = "0" }] }
                }
            ]
        }
    ]
};

static ProjectDefinition ValidProject() => new()
{
    Project = new ProjectSection { Name = "Test", OutputDirectory = ".\\Ausgabe", Build = ["Mastering"] },
    Profiles = new ProfileSelection { Base = "Grundprofil-9.1" },
    Transport = new TransportConfiguration
    {
        PiccKey = new KeyReference { Type = "DF_KEY_DES", Value = new string('0', 32) },
        ApplicationKeys = new Dictionary<string, string>
        {
            ["DF_KEY_DES"] = new string('0', 32), ["DF_KEY_AES"] = new string('0', 32), ["DF_KEY_3K3DES"] = new string('0', 48)
        }
    },
    Productive = new ProductiveConfiguration { PiccKey = new KeyReference { Type = "DF_KEY_AES" }, PiccKeySettings = "0F" }
};

static bool JsonEquivalent(string left, string right)
{
    using var a = JsonDocument.Parse(left); using var b = JsonDocument.Parse(right);
    return JsonSerializer.Serialize(a.RootElement) == JsonSerializer.Serialize(b.RootElement);
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void AssertThrows<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Erwartete Ausnahme {typeof(T).Name} blieb aus."); }

sealed class FakeRunningProcess(int id, bool hasExited, int? exitCode) : IRunningProcess
{
    public int Id { get; } = id;
    public bool HasExited { get; } = hasExited;
    public int? ExitCode { get; } = exitCode;
}

sealed class FakeProcessStarter(IRunningProcess process) : IProcessStarter
{
    public System.Diagnostics.ProcessStartInfo? StartInfo { get; private set; }
    public IRunningProcess Start(System.Diagnostics.ProcessStartInfo startInfo) { StartInfo = startInfo; return process; }
}

sealed class ImmediateDelay : IAsyncDelay
{
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
}

sealed class FakeProcessProbe(bool running) : IRFiDGearProcessProbe
{
    public bool IsRunning(RFiDGearLaunchResult launch) => running;
}

sealed class FakeControllableProcessSource(FakeControllableProcess process) : IControllableProcessSource
{
    public IControllableProcess? Open(int processId) => process.Id == processId ? process : null;
}

sealed class FakeControllableProcess(int id, string executablePath, bool closeResult, bool exitsAfterClose) : IControllableProcess
{
    private bool exited;
    public int Id { get; } = id;
    public bool HasExited => exited;
    public string? ExecutablePath { get; } = executablePath;
    public bool Killed { get; private set; }
    public bool CloseMainWindow() { if (closeResult && exitsAfterClose) exited = true; return closeResult; }
    public bool WaitForExit(int milliseconds) => exited;
    public void Kill() { Killed = true; exited = true; }
    public void Dispose() { }
}

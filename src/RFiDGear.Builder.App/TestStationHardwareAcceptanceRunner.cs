using System.IO;
using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

internal static class TestStationHardwareAcceptanceRunner
{
    private sealed record PhaseResult(string Name, string State, int TaskCount, string Summary);

    public static async Task<string> RunAsync(string? requestedOutput = null, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(requestedOutput ?? Path.Combine(AppContext.BaseDirectory, "hardware-acceptance",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(root);
        var settings = new BuilderSettingsStore().Load();
        var executable = Environment.GetEnvironmentVariable("SIENCODE_HARDWARE_RUNTIME") is { Length: > 0 } overridePath
            ? Path.GetFullPath(overridePath) : settings.RFiDGear.ExecutablePath;
        if (!File.Exists(executable)) throw new FileNotFoundException("Konfigurierte RFiDGear-Runtime fehlt.", executable);
        var reader = RFiDGearSettingsInspection.Inspect();
        if (!reader.IsConfigured) throw new InvalidOperationException(reader.Error ?? "Reader-Provider ist nicht konfiguriert.");
        var capabilities = new RFiDGearCapabilityInspector().Inspect(executable);
        if (!capabilities.Has(RFiDGearCapabilityId.AutorunOnce) || !capabilities.Has(RFiDGearCapabilityId.ApplicationFileInventory))
            throw new InvalidOperationException("AUTORUN oder Application-/File-Inventur ist in der Runtime nicht nachgewiesen.");

        var fixtureSource = Path.Combine(AppContext.BaseDirectory, "assets", "test-fixtures", "Synthetic-AES-Mixed.base.json");
        if (!File.Exists(fixtureSource)) throw new FileNotFoundException("Synthetisches Hardwareprofil fehlt.", fixtureSource);
        var profileRoot = Path.Combine(root, "profiles");
        Directory.CreateDirectory(profileRoot);
        File.Copy(fixtureSource, Path.Combine(profileRoot, "Synthetic-AES-Mixed.base.json"), true);
        var library = ProfileLibrary.Load(profileRoot);
        var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
        var project = ProjectDraftFactory.Create(library, "RC15-Synthetic-Hardware");
        project.Productive.PiccKey.Type = "DF_KEY_AES";
        project.Productive.PiccKey.Value = new string('A', 32);
        project.Productive.PiccKeySettings = "0F";
        project.Productive.ApplicationKeys["A1B2C3"] = [new string('B', 32), new string('C', 32), new string('D', 32)];
        project.Values["testSite"] = "0042";
        project.Values["testHex"] = "A1B2C3";
        project.Values["testLabel"] = "OSS1";
        project.Values["testUnicode"] = "ÄÖ";
        ProfileBindingService.BindCurrent(project, library);
        var sourcePath = Path.Combine(root, "project.source.json");
        ProjectJson.Save(sourcePath, ProjectJson.WithoutAllSecrets(project));
        var build = new ProjectBuildService().Build(project, library, shells, sourcePath, Path.Combine(root, "projects"), AppIdentity.DisplayVersion + "-hardware");
        var phases = new List<PhaseResult>();

        var retryFromSynthetic = string.Equals(Environment.GetEnvironmentVariable("SIENCODE_HARDWARE_INITIAL_PICC"), "synthetic", StringComparison.OrdinalIgnoreCase);
        if (retryFromSynthetic)
        {
            phases.Add(await RunAsync("01 Formatieren und PICC auf DES-Transport zurücksetzen",
                Path.Combine(build.ProjectDirectory, "RC15-Synthetic-Hardware_Recovery_Format_ProductivePICC_ToTransport.rfPrj"),
                build.SessionSecrets, true, cancellationToken));
        }
        else
        {
            var format = ProtectAndSave(new SupportProjectFactory(shells).BuildStandaloneFormat("DF_KEY_DES", new string('0', 32)),
                Path.Combine(root, "01-format-standard.rfPrj"), "format-standard");
            phases.Add(await RunAsync("01 Formatieren mit Standard-PICC", format.Path, format.Secrets, true, cancellationToken));
        }

        phases.Add(await RunAsync("02 Synthetisches Mastering", Path.Combine(build.ProjectDirectory, "RC15-Synthetic-Hardware_Mastering.rfPrj"),
            build.SessionSecrets, true, cancellationToken));

        var codingService = new CodingSessionService();
        using (var prepared = codingService.Prepare(codingService.Load(build.ProjectDirectory),
                   new Dictionary<string, string>(StringComparer.Ordinal) { ["testSerial"] = "123456" }))
        {
            var coding = await RunAsync("03 Kodieren und Schlüssel wechseln", prepared.Definition.CodingProjectPath,
                build.SessionSecrets, true, cancellationToken);
            var evaluation = codingService.Evaluate(prepared, knownSecrets: build.SessionSecrets.Values);
            if (evaluation.State != CodingRunState.Successful)
                throw new InvalidOperationException("Payload-/Readback-Verifikation fehlgeschlagen: " + evaluation.Detail);
            phases.Add(coding with { Summary = coding.Summary + " Payload- und Readbackvergleich erfolgreich." });
        }

        var inventoryRuntime = Path.Combine(root, "04-application-inventory.runtime.json");
        var inventory = ProtectAndSave(new SupportProjectFactory(shells).BuildApplicationFileInventory(
            "A1B2C3", "DF_KEY_AES", 0, new string('B', 32), inventoryRuntime, true),
            Path.Combine(root, "04-application-inventory.rfPrj"), "application-inventory");
        phases.Add(await RunAsync("04 Vertiefte Datei-/Inhaltsanalyse", inventory.Path, inventory.Secrets, false, cancellationToken));
        if (!File.Exists(inventoryRuntime)) throw new InvalidDataException("Die Runtime hat kein Inventurartefakt erzeugt.");
        var inventoryReport = new ApplicationInventoryReportService().Create(root, inventoryRuntime);
        using (var inventoryJson = JsonDocument.Parse(File.ReadAllText(inventoryRuntime)))
        {
            var node = inventoryJson.RootElement;
            if (!node.TryGetProperty("authentication", out var auth) ||
                !string.Equals(auth.GetString(), "NoError", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Produktiver Application-Key 0 wurde nach dem Schlüsselwechsel nicht akzeptiert.");
            if (!node.TryGetProperty("fileCount", out var count) || count.GetInt32() != 3)
                throw new InvalidDataException("Die tiefe Inventur hat nicht exakt drei Dateien erkannt.");
        }

        phases.Add(await RunAsync("05 Rückbau auf leeren Standardzustand",
            Path.Combine(build.ProjectDirectory, "RC15-Synthetic-Hardware_Recovery_Format_ProductivePICC_ToTransport.rfPrj"),
            build.SessionSecrets, true, cancellationToken));

        var recognitionDoc = new SupportProjectFactory(shells).BuildStandaloneCardRecognition();
        var recognitionPath = Path.Combine(root, "06-final-recognition.rfPrj");
        RfPrjArchive.Save(recognitionPath, recognitionDoc);
        phases.Add(await RunAsync("06 Leeren Standardzustand bestätigen", recognitionPath,
            new Dictionary<string, string>(), false, cancellationToken));

        var reportPath = Path.Combine(root, "hardware-acceptance.result.json");
        var report = new
        {
            schemaVersion = "1.0", generatedAt = DateTimeOffset.Now, status = "OK",
            runtime = new { executable, capabilities.Version.ProductVersion },
            reader = new { reader.Provider, reader.Port, reader.ReaderName },
            fixture = new { aid = "A1B2C3", files = 3, payloadSerial = "123456", productiveKeyReadback = true },
            phases, inventoryReport = new { inventoryReport.TextPath, inventoryReport.JsonPath },
            safety = new { syntheticKeysOnly = true, productiveSecretsPersisted = false, finalCardState = "formatted-standard-transport" }
        };
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return reportPath;

        async Task<PhaseResult> RunAsync(string name, string projectPath, IReadOnlyDictionary<string, string> secrets, bool destructive, CancellationToken token)
        {
            using var server = secrets.Count == 0 ? null : ProjectSecretPipeServer.Start(secrets);
            var document = RfPrjArchive.Load(projectPath);
            var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.MasteringAutorun, name, projectPath,
                !destructive, destructive, true, document.Summarize());
            var service = new RFiDGearOperationService();
            var session = await service.StartAsync(executable, operation, timeout: TimeSpan.FromMinutes(4), secretServer: server, cancellationToken: token);
            RFiDGearOperationEvaluation result;
            do
            {
                await Task.Delay(400, token);
                result = service.Evaluate(session, knownSecrets: secrets.Values);
            } while (result.State == RFiDGearOperationState.Waiting);
            _ = new RFiDGearProcessCompletionService().CompleteTerminal(session.Launch);
            if (result.State != RFiDGearOperationState.Successful) throw new InvalidOperationException(name + " fehlgeschlagen: " + result.Summary);
            return new PhaseResult(name, "OK", operation.Tasks.Count, result.Summary);
        }
    }

    private static (string Path, IReadOnlyDictionary<string, string> Secrets) ProtectAndSave(RfPrjDocument document, string path, string prefix)
    {
        var protectedProject = RfPrjSecretProtection.Protect(document, prefix);
        if (RfPrjSecretProtection.FindPlaintextSecretElements(protectedProject.Project).Count != 0)
            throw new InvalidDataException("Testprojekt enthält Klartextschlüssel.");
        RfPrjArchive.Save(path, protectedProject.Project);
        return (path, protectedProject.Secrets);
    }
}

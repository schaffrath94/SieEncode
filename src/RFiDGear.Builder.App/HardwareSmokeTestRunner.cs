using System.IO;
using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

internal static class HardwareSmokeTestRunner
{
    public static async Task<string> RunAsync(string projectDirectory, bool productive = false, string? resultPath = null, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(projectDirectory);
        var output = Path.GetFullPath(string.IsNullOrWhiteSpace(resultPath)
            ? Path.Combine(root, "diagnostics", productive ? "hardware-smoke-productive.result.json" : "hardware-smoke-transport.result.json") : resultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var settings = new BuilderSettingsStore().Load();
        var executable = settings.RFiDGear.ExecutablePath;
        if (!File.Exists(executable)) throw new FileNotFoundException("Konfigurierte RFiDGear-Runtime fehlt.", executable);
        var capabilities = new RFiDGearCapabilityInspector().Inspect(executable);
        if (!capabilities.Has(RFiDGearCapabilityId.AutorunOnce)) throw new InvalidOperationException("AUTORUN-Fähigkeit ist nicht nachgewiesen.");
        var reader = RFiDGearSettingsInspection.Inspect();
        if (!reader.IsConfigured) throw new InvalidOperationException(reader.Error ?? "Reader-Provider ist nicht konfiguriert.");
        var service = new RFiDGearOperationService();
        var mode = productive ? CardPreflightMode.Productive : CardPreflightMode.Transport;
        var kind = productive ? RFiDGearOperationKind.CardPreflightProductive : RFiDGearOperationKind.CardPreflightTransport;
        var operation = service.Inspect(root).Find(kind)
            ?? throw new InvalidOperationException($"Read-only {mode}-Prüfung fehlt im Projekt.");
        if (!operation.ReadOnly || operation.Destructive || operation.Tasks.Count == 0 || operation.Tasks.Any(task => task.Type != "ReadData"))
            throw new InvalidOperationException("Hardware-Smoke-Test verweigert ein nicht ausschließlich lesendes Projekt.");
        var cards = new CardPreflightService();
        cards.Prepare(root, mode);
        var session = await service.StartAsync(executable, operation, timeout: TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);
        RFiDGearOperationEvaluation evaluation;
        do
        {
            await Task.Delay(500, cancellationToken);
            evaluation = service.Evaluate(session);
        } while (evaluation.State == RFiDGearOperationState.Waiting);
        var cardReport = cards.CompleteAndEvaluate(root, mode, evaluation);
        var probe = productive ? cardReport.Report.Productive : cardReport.Report.Transport;
        var closure = new RFiDGearProcessCompletionService().CompleteTerminal(session.Launch);
        var result = new
        {
            schemaVersion = "1.0", version = AppIdentity.DisplayVersion, generatedAt = DateTimeOffset.Now,
            status = evaluation.State != RFiDGearOperationState.Successful ? "FEHLER" : closure.Completed ? "OK" : "BEDIENAKTION_ERFORDERLICH",
            safety = new { readOnly = true, destructive = false, taskTypes = operation.Tasks.Select(task => task.Type).Distinct().ToArray() },
            reader = new { reader.Provider, reader.Port, reader.ReaderName, reader.SettingsPath },
            runtime = new { executable, capabilities.Version.ProductVersion, closure.Completed, closure.RequiresForce, closure.Message },
            operation = new { operation.Label, operation.ProjectPath, taskCount = operation.Tasks.Count, processId = session.Launch.ProcessId, state = evaluation.State.ToString(), evaluation.Summary },
            card = new { mode = mode.ToString(), cardReport.Report.Classification, probe, cardReport.JsonPath, cardReport.TextPath }
        };
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        if (evaluation.State != RFiDGearOperationState.Successful) throw new InvalidOperationException(evaluation.Summary + " Bericht: " + output);
        if (!closure.Completed) throw new InvalidOperationException(closure.Message + " Ein erzwungenes Beenden erfolgt nicht automatisch. Bericht: " + output);
        return output;
    }
}

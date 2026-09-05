using System.Globalization;
using System.Text.RegularExpressions;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record VerificationExpectation(string Output, string Path, byte[] ExpectedBytes, long BaselineLastWriteTicksUtc);
public enum CodingRunState { Waiting, Successful, Failed }
public sealed record CodingVerificationSummary(int Expected, int FreshAndMatching, int Waiting, int Mismatched);
public sealed record CodingRunEvaluation(CodingRunState State, string Detail, string FailureCategory,
    RFiDGearDiagnosticSignal Signal, RfPrjTaskSummary? Task, RFiDGearStructuredRunEvidence? StructuredRun = null,
    CodingVerificationSummary? Verification = null);

public static class CodingRunProcessExitPolicy
{
    public static CodingRunEvaluation Finalize(CodingRunEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (evaluation.State != CodingRunState.Waiting) return evaluation;
        if (evaluation.StructuredRun is { Correlated: true, TerminalEvent: "TaskLoop.Success" })
        {
            var signal = new RFiDGearDiagnosticSignal(true, true, "Readback", "Verifikation",
                "RFiDGear wurde terminal erfolgreich beendet, aber die finale Dateiverifikation ist unvollständig.", null, "Readback / Verifikation", "");
            return new CodingRunEvaluation(CodingRunState.Failed,
                "RFiDGear meldete TaskLoop.Success; mindestens ein finaler Readback fehlt oder ist zu alt.",
                "Readback / Verifikation", signal, null, evaluation.StructuredRun, evaluation.Verification);
        }
        var missingTerminal = new RFiDGearDiagnosticSignal(true, true, "Prozessende", "Runtime",
            "RFiDGear wurde beendet, ohne einen korrelierten terminalen Laufstatus zu protokollieren.", null, "Runtime", "");
        return new CodingRunEvaluation(CodingRunState.Failed,
            "RFiDGear wurde vor einem belastbaren Laufabschluss beendet.", "RFiDGear-Prozess / terminaler Laufnachweis",
            missingTerminal, null, evaluation.StructuredRun, evaluation.Verification);
    }
}

public static class CodingRunEvaluator
{
    public static CodingRunEvaluation Evaluate(
        IReadOnlyList<VerificationExpectation> expectations,
        string? logDelta,
        IReadOnlyList<RfPrjTaskSummary>? taskMap = null,
        IEnumerable<string>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        var signal = RFiDGearDiagnosticParser.Parse(logDelta, knownSecrets);
        var task = taskMap is null ? null : RFiDGearDiagnosticParser.CorrelateTask(signal, taskMap);
        var structured = taskMap is null ? RFiDGearStructuredRunEvidence.Empty : RFiDGearStructuredRunParser.Parse(logDelta, taskMap);
        var waiting = new List<string>();
        var mismatches = new List<string>();
        var matching = 0;
        foreach (var expectation in expectations)
        {
            if (!File.Exists(expectation.Path)) { waiting.Add(expectation.Output); continue; }
            var info = new FileInfo(expectation.Path);
            if (info.LastWriteTimeUtc.Ticks <= expectation.BaselineLastWriteTicksUtc) { waiting.Add(expectation.Output); continue; }
            byte[] actual;
            try { actual = ReadHexFile(expectation.Path); }
            catch (InvalidDataException) { waiting.Add(expectation.Output); continue; }
            if (actual.Length != expectation.ExpectedBytes.Length) { waiting.Add(expectation.Output); continue; }
            if (!actual.AsSpan().SequenceEqual(expectation.ExpectedBytes))
                mismatches.Add($"{expectation.Output}: SOLL {Convert.ToHexString(expectation.ExpectedBytes)}, IST {Convert.ToHexString(actual)}");
            else matching++;
        }
        var verification = new CodingVerificationSummary(expectations.Count, matching, waiting.Count, mismatches.Count);
        var structuredFailure = structured.TerminalEvent == "TaskLoop.Failure"
            ? structured.TaskOutcomes.FirstOrDefault(item => string.Equals(item.Outcome, "Failed", StringComparison.OrdinalIgnoreCase))
            : null;
        if (structuredFailure is not null && taskMap is not null &&
            structuredFailure.Position >= 0 && structuredFailure.Position < taskMap.Count)
        {
            var failedTask = taskMap[structuredFailure.Position];
            var missingInput = failedTask.Type == "WriteData" && !string.IsNullOrWhiteSpace(failedTask.InputPath) && !File.Exists(failedTask.InputPath);
            var category = missingInput
                ? "Eingabedaten / Payload"
                : RFiDGearDiagnosticParser.Classify(structuredFailure.ErrorCode);
            var detail = missingInput
                ? $"Datensatz nicht vorbereitet oder unvollständig: Die Payloaddatei '{Path.GetFileName(failedTask.InputPath)}' fehlt. RFiDGear konnte Task {failedTask.Index} (WriteData) deshalb nicht ausführen."
                : $"RFiDGear meldet Task {failedTask.Index} ({failedTask.Type}) als fehlgeschlagen. Fehlercode: {structuredFailure.ErrorCode}.";
            var structuredSignal = new RFiDGearDiagnosticSignal(true, true, category, "high", detail,
                structuredFailure.Position, "TaskLoop.Failure", signal.SanitizedText);
            return new CodingRunEvaluation(CodingRunState.Failed, detail, category, structuredSignal, failedTask, structured, verification);
        }
        if (signal.StrongFailure)
            return new CodingRunEvaluation(CodingRunState.Failed, signal.Summary, signal.Category, signal, task, structured, verification);
        if (waiting.Count > 0)
            return new CodingRunEvaluation(CodingRunState.Waiting, "Noch keine neue vollständige finale Verifikation für: " + string.Join(", ", waiting), "", signal, task, structured, verification);
        if (mismatches.Count > 0)
            return new CodingRunEvaluation(CodingRunState.Failed, "Readback-Abweichung: " + string.Join(" | ", mismatches), "Readback / Verifikation", signal, task, structured, verification);
        return new CodingRunEvaluation(CodingRunState.Successful, "Alle finalen Verifikations-Reads wurden nach dem Lauf aktualisiert und entsprechen den erwarteten Payloads.", "", signal, task, structured, verification);
    }

    public static IReadOnlyList<VerificationExpectation> CaptureBaseline(string projectDirectory, string verificationDirectory, IReadOnlyList<PayloadArtifact> payloads)
    {
        var root = Path.GetFullPath(projectDirectory);
        return payloads.Select(payload =>
        {
            var path = Path.GetFullPath(Path.Combine(root, verificationDirectory, payload.Output));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Verification-Pfad verlässt den Projektordner: {payload.Output}");
            return new VerificationExpectation(payload.Output, path, payload.Bytes, File.Exists(path) ? new FileInfo(path).LastWriteTimeUtc.Ticks : 0);
        }).ToArray();
    }

    private static byte[] ReadHexFile(string path)
    {
        var compact = Regex.Replace(File.ReadAllText(path), @"\s", "");
        if (compact.Length % 2 != 0 || !compact.All(Uri.IsHexDigit)) throw new InvalidDataException("Verification-Datei enthält kein gültiges Hexformat.");
        try { return Convert.FromHexString(compact); }
        catch (FormatException exception) { throw new InvalidDataException("Verification-Datei enthält kein gültiges Hexformat.", exception); }
    }
}

namespace RFiDGear.Builder.Runtime;

public sealed record CodingRunOperatorAnalysis(
    string Code, bool CanReleaseCard, string Headline, string Explanation,
    string NextAction, string EvidenceSummary, string TechnicalSummary);

public static class CodingRunOperatorAnalyzer
{
    public static CodingRunOperatorAnalysis Analyze(CodingRunEvaluation evaluation, int expectedTasks)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var structured = evaluation.StructuredRun ?? RFiDGearStructuredRunEvidence.Empty;
        var verification = evaluation.Verification;
        var verificationText = verification is null
            ? "Finale Datenprüfung: nicht verfügbar"
            : $"Finale Datenprüfung: {verification.FreshAndMatching}/{verification.Expected} bytegleich" +
              (verification.Waiting > 0 ? $" · {verification.Waiting} fehlen/zu alt" : "") +
              (verification.Mismatched > 0 ? $" · {verification.Mismatched} abweichend" : "");
        var runText = structured.TaskOutcomes.Count == 0
            ? "RFiDGear-Laufnachweis: nicht vorhanden"
            : $"RFiDGear-Laufnachweis: {structured.TaskOutcomes.Count}/{expectedTasks} Tasks · " +
              $"{(string.IsNullOrWhiteSpace(structured.TerminalEvent) ? "kein Abschlussstatus" : structured.TerminalEvent)} · " +
              $"Korrelation {(structured.Correlated ? "OK" : "fehlerhaft")}";
        var evidence = verificationText + "\n" + runText;

        if (evaluation.State == CodingRunState.Waiting)
            return new("WAITING", false, "NOCH NICHT ABGESCHLOSSEN",
                "SieEncode kann noch nicht bestätigen, dass die Karte korrekt kodiert wurde. Fehlende oder nicht neu erzeugte Rücklesedaten sind kein Erfolg.",
                "RFiDGear vollständig durchlaufen lassen und danach „Ergebnis auswerten“ wählen. Karte und Datensatz bis dahin nicht wechseln.",
                evidence, evaluation.Detail);

        if (evaluation.State == CodingRunState.Failed)
        {
            var category = string.IsNullOrWhiteSpace(evaluation.FailureCategory) ? "RFiDGear-Lauf / unbekannt" : evaluation.FailureCategory;
            var taskText = evaluation.Task is null ? "" : $" Betroffen: Task {evaluation.Task.Index} ({evaluation.Task.Type}) – {evaluation.Task.Description}.";
            var next = category switch
            {
                "Authentifizierung / Key" => "Nicht erneut mastern oder eine andere Nummer verwenden. Projekt und Kartenstatus prüfen; anschließend denselben Datensatz gezielt wiederholen.",
                "Reader / Transport" => "Karte liegen lassen, Leser/Verbindung prüfen und denselben Datensatz erneut versuchen. Bei Wiederholung Diagnosepaket an den Support geben.",
                "Karte entfernt / gewechselt" => "Keine neue Karte auflegen und nicht zur nächsten Nummer wechseln. Ursprüngliche Karte wieder auflegen und denselben Datensatz wiederholen.",
                "Readback / Verifikation" => "Karte nicht freigeben. SieEncode-SOLL und Karten-IST weichen ab; Diagnosepaket speichern und denselben Datensatz erst nach Klärung wiederholen.",
                "Eingabedaten / Payload" => "Karte nicht freigeben. Datensatz in SieEncode neu vorbereiten und erst starten, wenn alle Payloads als vollständig bereit angezeigt werden; danach denselben Datensatz wiederholen.",
                "Berechtigung / Protokoll" => "Karte nicht verändern. Projektdefinition, Kartenstatus und Runtime-Kompatibilität prüfen; Diagnosepaket weitergeben.",
                "RFiDGear-Ausnahme" => "RFiDGear regulär schließen, Diagnosepaket speichern und denselben Datensatz erst nach Prüfung erneut starten.",
                _ => "Karte nicht freigeben und nicht zur nächsten Nummer wechseln. Diagnosepaket speichern und Ursache prüfen lassen."
            };
            return new("FAILED", false, "KODIERUNG NICHT FREIGEGEBEN",
                $"Der Lauf ist fehlgeschlagen. Ursache: {category}.{taskText}", next, evidence, evaluation.Detail);
        }

        var authoritative = structured.TerminalEvent == "TaskLoop.Success" && structured.Correlated &&
                            expectedTasks > 0 && structured.TaskOutcomes.Count == expectedTasks;
        if (!authoritative)
            return new("EVIDENCE_INCOMPLETE", false, "NICHT FREIGEBEN – LAUFNACHWEIS UNVOLLSTÄNDIG",
                "Die Kartendaten wurden zwar bytegleich zurückgelesen, aber der vollständige korrelierte RFiDGear-Abschlussnachweis fehlt. Für einen nachvollziehbaren Produktionslauf reicht das nicht aus.",
                "Gleichen Datensatz nicht als erfolgreich verbuchen. Diagnosepaket speichern und den vollständigen Kodierlauf erneut ausführen.",
                evidence, string.IsNullOrWhiteSpace(structured.CorrelationError) ? evaluation.Detail : structured.CorrelationError);

        return new("RELEASED", true, "KODIERUNG ERFOLGREICH BESTÄTIGT",
            "RFiDGear hat den vollständigen Tasklauf erfolgreich abgeschlossen und alle finalen Kartendaten wurden danach frisch sowie bytegleich zurückgelesen.",
            "Transponder kann verwendet werden. Erst jetzt Karte entnehmen und den nächsten Datensatz vorbereiten.",
            evidence, $"Run-ID-Fingerprint: {structured.RunIdHash} · Terminal: {structured.TerminalEvent}");
    }
}

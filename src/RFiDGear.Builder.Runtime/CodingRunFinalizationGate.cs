namespace RFiDGear.Builder.Runtime;

public sealed record CodingRunFinalizationDecision(
    bool WaitForTerminalEvidence,
    DateTimeOffset? PendingSince,
    string Detail);

public static class CodingRunFinalizationGate
{
    public static readonly TimeSpan DefaultTerminalGrace = TimeSpan.FromSeconds(5);

    public static CodingRunFinalizationDecision Decide(
        CodingRunEvaluation evaluation,
        CodingRunOperatorAnalysis operatorAnalysis,
        bool processRunning,
        DateTimeOffset? pendingSince,
        DateTimeOffset now,
        TimeSpan? terminalGrace = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(operatorAnalysis);

        if (evaluation.State != CodingRunState.Successful || operatorAnalysis.CanReleaseCard || !processRunning)
            return new(false, null, "");

        var grace = terminalGrace ?? DefaultTerminalGrace;
        if (grace < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(terminalGrace), "Das Nachlaufffenster darf nicht negativ sein.");

        var started = pendingSince ?? now;
        var remaining = grace - (now - started);
        if (remaining <= TimeSpan.Zero)
            return new(false, null, "Der korrelierte RFiDGear-Abschlussstatus ist nicht innerhalb des Nachlaufffensters eingetroffen.");

        return new(true, started,
            $"Alle finalen Kartendaten sind bytegleich. SieEncode wartet noch bis zu {Math.Ceiling(remaining.TotalSeconds):0} Sekunden auf den korrelierten RFiDGear-Abschlussstatus.");
    }
}

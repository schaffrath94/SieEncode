using System.ComponentModel;
using System.Diagnostics;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearRunSafetyAssessment(bool IsSafe, string Reason, int? ProcessId);

public interface IRFiDGearProcessProbe
{
    bool IsRunning(RFiDGearLaunchResult launch);
}

public sealed class SystemRFiDGearProcessProbe : IRFiDGearProcessProbe
{
    public bool IsRunning(RFiDGearLaunchResult launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (launch.ProcessId <= 0) return false;
        try
        {
            using var process = Process.GetProcessById(launch.ProcessId);
            if (process.HasExited) return false;
            try
            {
                var actual = process.MainModule?.FileName;
                return string.IsNullOrWhiteSpace(actual) ||
                    string.Equals(Path.GetFullPath(actual), Path.GetFullPath(launch.ExecutablePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Win32Exception) { return true; }
            catch (InvalidOperationException) { return true; }
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}

public static class RFiDGearRunSafety
{
    public static RFiDGearRunSafetyAssessment Assess(
        RFiDGearOperationSession? operationSession,
        bool operationFinalized,
        RFiDGearLaunchResult? codingLaunch,
        IRFiDGearProcessProbe processProbe)
    {
        ArgumentNullException.ThrowIfNull(processProbe);
        if (operationSession is { Operation.Autorun: true } && !operationFinalized)
            return new(false,
                $"{operationSession.Operation.Label} besitzt noch keinen terminalen RFiDGear-Status. Die Logüberwachung muss bis Erfolg, Fehler oder Timeout aktiv bleiben.",
                operationSession.Launch.ProcessId);

        var launches = new[] { operationSession?.Launch, codingLaunch }
            .Where(launch => launch is not null)
            .Cast<RFiDGearLaunchResult>()
            .GroupBy(launch => launch.ProcessId)
            .Select(group => group.First());
        foreach (var launch in launches)
        {
            if (!processProbe.IsRunning(launch)) continue;
            return new(false,
                $"Der vom Builder gestartete RFiDGear-Prozess {launch.ProcessId} läuft noch. RFiDGear zuerst schließen und den Vorgang danach erneut ausführen.",
                launch.ProcessId);
        }
        return new(true, "Kein überwachter oder noch laufender RFiDGear-Prozess blockiert den Vorgang.", null);
    }
}

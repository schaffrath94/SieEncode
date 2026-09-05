using System.ComponentModel;
using System.Diagnostics;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearProcessCompletionResult(bool Completed, bool RequiresForce, string Message);

public interface IControllableProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    string? ExecutablePath { get; }
    bool CloseMainWindow();
    bool WaitForExit(int milliseconds);
    void Kill();
}

public interface IControllableProcessSource
{
    IControllableProcess? Open(int processId);
}

public sealed class RFiDGearProcessCompletionService(IControllableProcessSource? source = null)
{
    private readonly IControllableProcessSource source = source ?? new SystemControllableProcessSource();

    public RFiDGearProcessCompletionResult Complete(RFiDGearLaunchResult launch, bool force)
    {
        ArgumentNullException.ThrowIfNull(launch);
        using var process = source.Open(launch.ProcessId);
        if (process is null || process.HasExited)
            return new(true, false, $"RFiDGear-Prozess {launch.ProcessId} ist bereits beendet.");
        if (string.IsNullOrWhiteSpace(process.ExecutablePath) ||
            !string.Equals(Path.GetFullPath(process.ExecutablePath), Path.GetFullPath(launch.ExecutablePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Prozess-ID {launch.ProcessId} gehört nicht mehr zur erwarteten RFiDGear-EXE. Der Prozess wird nicht verändert.");

        if (process.CloseMainWindow() && process.WaitForExit(5000))
            return new(true, false, $"RFiDGear-Prozess {launch.ProcessId} wurde über sein Hauptfenster beendet.");
        if (!force)
            return new(false, true, $"RFiDGear-Prozess {launch.ProcessId} besitzt kein reagierendes Hauptfenster. Er kann nach bewusster Bestätigung beendet werden.");
        process.Kill();
        if (!process.WaitForExit(10000)) throw new InvalidOperationException($"RFiDGear-Prozess {launch.ProcessId} konnte nicht beendet werden.");
        return new(true, false, $"Der terminal abgeschlossene RFiDGear-Prozess {launch.ProcessId} wurde nach Bestätigung beendet.");
    }

    public RFiDGearProcessCompletionResult CompleteTerminal(RFiDGearLaunchResult launch)
    {
        var graceful = Complete(launch, force: false);
        return graceful.RequiresForce ? Complete(launch, force: true) : graceful;
    }
}

internal sealed class SystemControllableProcessSource : IControllableProcessSource
{
    public IControllableProcess? Open(int processId)
    {
        try { return new SystemControllableProcess(Process.GetProcessById(processId)); }
        catch (ArgumentException) { return null; }
    }
}

internal sealed class SystemControllableProcess(Process process) : IControllableProcess
{
    public int Id => process.Id;
    public bool HasExited { get { process.Refresh(); return process.HasExited; } }
    public string? ExecutablePath
    {
        get
        {
            try { return process.MainModule?.FileName; }
            catch (Win32Exception exception) { throw new InvalidOperationException("RFiDGear-Prozesspfad konnte nicht sicher geprüft werden.", exception); }
        }
    }
    public bool CloseMainWindow() => process.CloseMainWindow();
    public bool WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);
    public void Kill() => process.Kill(entireProcessTree: false);
    public void Dispose() => process.Dispose();
}

using System.Diagnostics;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearLaunchRequest(string ExecutablePath, string ProjectPath, bool Autorun = false, bool AutoMode = false,
    ProjectSecretPipeServer? SecretServer = null);
public sealed record RFiDGearLaunchResult(int ProcessId, string ExecutablePath, string ProjectPath, bool Autorun, bool AutoMode, string Arguments);
public sealed record RFiDGearVersionInfo(string Path, string? ProductVersion, Version? Version, bool MeetsRecommended);

public interface IRunningProcess
{
    int Id { get; }
    bool HasExited { get; }
    int? ExitCode { get; }
}

public interface IProcessStarter
{
    IRunningProcess Start(ProcessStartInfo startInfo);
}

public interface IAsyncDelay
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IRFiDGearLauncher
{
    Task<RFiDGearLaunchResult> LaunchAsync(RFiDGearLaunchRequest request, CancellationToken cancellationToken = default);
}

public sealed class RFiDGearLauncher : IRFiDGearLauncher
{
    public static readonly Version RecommendedVersion = new(2, 0, 10, 0);
    private readonly IProcessStarter processStarter;
    private readonly IAsyncDelay delay;
    private readonly IRFiDGearCapabilityInspector capabilityInspector;

    public RFiDGearLauncher(IProcessStarter? processStarter = null, IAsyncDelay? delay = null, IRFiDGearCapabilityInspector? capabilityInspector = null)
    {
        this.processStarter = processStarter ?? new SystemProcessStarter();
        this.delay = delay ?? new SystemAsyncDelay();
        this.capabilityInspector = capabilityInspector ?? new RFiDGearCapabilityInspector();
    }

    public async Task<RFiDGearLaunchResult> LaunchAsync(RFiDGearLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var executable = RequireFile(request.ExecutablePath, "RFiDGear.exe");
        var project = RequireFile(request.ProjectPath, "RFiDGear-Projekt");
        if (request.Autorun && request.AutoMode)
            throw new ArgumentException("AUTORUN und AUTOMODE dürfen nicht gleichzeitig aktiviert werden.", nameof(request));
        var capabilities = capabilityInspector.Inspect(executable);
        RequireCapability(capabilities, RFiDGearCapabilityId.CustomProjectFile, "Öffnen einer konkreten Projektdatei");
        if (request.Autorun) RequireCapability(capabilities, RFiDGearCapabilityId.AutorunOnce, "AUTORUN");
        if (request.AutoMode) RequireCapability(capabilities, RFiDGearCapabilityId.ContinuousAutoMode, "AUTOMODE");
        var arguments = BuildArguments(project, request.Autorun, request.AutoMode, request.SecretServer?.Descriptor);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(project)!,
            UseShellExecute = true
        };
        var process = processStarter.Start(startInfo);
        request.SecretServer?.AuthorizeClient(process.Id);
        await delay.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
        if (process.HasExited)
            throw new InvalidOperationException($"RFiDGear wurde gestartet, aber sofort mit Exit-Code {process.ExitCode?.ToString() ?? "unbekannt"} beendet. Details stehen im RFiDGear-Log unter %LOCALAPPDATA%\\RFiDGear\\log.");
        return new RFiDGearLaunchResult(process.Id, executable, project, request.Autorun, request.AutoMode, arguments);
    }

    public static string BuildArguments(string projectPath, bool autorun, bool autoMode = false, ProjectSecretPipeDescriptor? secretPipe = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (autorun && autoMode) throw new ArgumentException("AUTORUN und AUTOMODE dürfen nicht gleichzeitig aktiviert werden.");
        var fullPath = Path.GetFullPath(projectPath);
        if (fullPath.Contains('"')) throw new ArgumentException("Projektpfad darf kein Anführungszeichen enthalten.", nameof(projectPath));
        return $"CUSTOMPROJECTFILE=\"{fullPath}\"" +
            (autorun ? " AUTORUN=1" : "") +
            (autoMode ? " AUTOMODE=1" : "") +
            (secretPipe is null ? "" : $" PROJECTSECRETPIPE={secretPipe.PipeName} PROJECTSECRETSERVERPID={secretPipe.ServerProcessId}");
    }

    public static RFiDGearVersionInfo ReadVersion(string executablePath)
    {
        var path = RequireFile(executablePath, "RFiDGear.exe");
        var info = FileVersionInfo.GetVersionInfo(path);
        Version? version = null;
        if (!string.IsNullOrWhiteSpace(info.FileVersion)) Version.TryParse(NormalizeVersion(info.FileVersion), out version);
        if (version is null && !string.IsNullOrWhiteSpace(info.ProductVersion)) Version.TryParse(NormalizeVersion(info.ProductVersion), out version);
        return new RFiDGearVersionInfo(path, info.ProductVersion, version, version is not null && version >= RecommendedVersion);
    }

    private static string NormalizeVersion(string value)
    {
        var clean = value.Split('+', '-', ' ')[0];
        return clean;
    }

    private static void RequireCapability(RFiDGearCapabilityReport report, RFiDGearCapabilityId id, string action)
    {
        if (report.Has(id)) return;
        var capability = report.Capabilities.First(item => item.Id == id);
        throw new InvalidOperationException($"{action} ist für diese RFiDGear-Installation nicht freigegeben: {capability.Evidence}" +
            (string.IsNullOrWhiteSpace(report.Error) ? "" : $" ({report.Error})"));
    }

    private static string RequireFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new FileNotFoundException($"{label} wurde nicht angegeben.");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"{label} wurde nicht gefunden: {fullPath}", fullPath);
        return fullPath;
    }
}

internal sealed class SystemRunningProcess(Process process) : IRunningProcess
{
    public int Id => process.Id;
    public bool HasExited { get { process.Refresh(); return process.HasExited; } }
    public int? ExitCode => process.HasExited ? process.ExitCode : null;
}

internal sealed class SystemProcessStarter : IProcessStarter
{
    public IRunningProcess Start(ProcessStartInfo startInfo)
        => new SystemRunningProcess(Process.Start(startInfo) ?? throw new InvalidOperationException("RFiDGear-Prozess konnte nicht erzeugt werden."));
}

internal sealed class SystemAsyncDelay : IAsyncDelay
{
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}

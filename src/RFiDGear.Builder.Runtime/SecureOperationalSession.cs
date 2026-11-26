using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed class SecureOperationalSession : IDisposable
{
    private readonly string sessionsRoot;
    private readonly FileStream activeLock;
    private bool disposed;

    internal SecureOperationalSession(string sessionsRoot, string sessionDirectory, ProjectBuildResult build)
    {
        this.sessionsRoot = Path.GetFullPath(sessionsRoot);
        SessionDirectory = Path.GetFullPath(sessionDirectory);
        Build = build;
        activeLock = new FileStream(Path.Combine(SessionDirectory, ".active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public string SessionDirectory { get; }
    public string ProjectDirectory => Build.ProjectDirectory;
    public ProjectBuildResult Build { get; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var root = sessionsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!SessionDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase) || SessionDirectory.Length <= root.Length)
            throw new InvalidOperationException("Unsicheres Sitzungsverzeichnis wird nicht entfernt.");
        activeLock.Dispose();
        if (Directory.Exists(SessionDirectory)) Directory.Delete(SessionDirectory, recursive: true);
    }
}

public sealed class SecureOperationalSessionService
{
    public static string DefaultSessionsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "Sessions");

    public SecureOperationalSession Materialize(
        ProjectDefinition hydratedProject,
        ProfileLibrary library,
        TaskShellLibrary shells,
        string sourceConfigPath,
        string builderVersion,
        string? sessionsRoot = null)
    {
        ArgumentNullException.ThrowIfNull(hydratedProject);
        var root = Path.GetFullPath(sessionsRoot ?? DefaultSessionsRoot);
        Directory.CreateDirectory(root);
        RemoveAbandonedSessions(root);
        var safeName = string.Concat(hydratedProject.Project.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Projekt";
        var sessionDirectory = Path.Combine(root, $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sessionDirectory);
        try
        {
            var runtimeProject = JsonSerializer.Deserialize<ProjectDefinition>(
                JsonSerializer.Serialize(hydratedProject, ProjectJson.Options), ProjectJson.Options)
                ?? throw new InvalidDataException("Projekt konnte für die sichere Sitzung nicht kopiert werden.");
            runtimeProject.TransientKeyLibraryResolution = hydratedProject.TransientKeyLibraryResolution;
            runtimeProject.Project.OutputDirectory = sessionDirectory;
            var build = new ProjectBuildService().Build(runtimeProject, library, shells, sourceConfigPath, sessionDirectory, builderVersion);
            File.WriteAllText(Path.Combine(sessionDirectory, ".sieencode-session"),
                $"temporary=true{Environment.NewLine}createdUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}",
                new System.Text.UTF8Encoding(false));
            return new SecureOperationalSession(root, sessionDirectory, build);
        }
        catch
        {
            if (Directory.Exists(sessionDirectory)) Directory.Delete(sessionDirectory, recursive: true);
            throw;
        }
    }

    public int RemoveAbandonedSessions(string? sessionsRoot = null)
    {
        var root = Path.GetFullPath(sessionsRoot ?? DefaultSessionsRoot);
        if (!Directory.Exists(root)) return 0;
        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            var lockPath = Path.Combine(directory, ".active.lock");
            try
            {
                using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                Directory.Delete(directory, recursive: true);
                removed++;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return removed;
    }
}

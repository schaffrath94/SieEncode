using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class BuilderSettingsStore
{
    private readonly string settingsPath;
    private readonly string? legacySettingsPath;

    public BuilderSettingsStore(string? settingsPath = null, string? legacySettingsPath = null)
    {
        this.settingsPath = Path.GetFullPath(settingsPath ?? ProjectPaths.GetSettingsPath());
        this.legacySettingsPath = string.IsNullOrWhiteSpace(legacySettingsPath) ? null : Path.GetFullPath(legacySettingsPath);
    }

    public string SettingsPath => settingsPath;

    public BuilderSettings Load()
    {
        if (File.Exists(settingsPath)) return WithDefaults(Read(settingsPath));
        var migrationCandidates = new[] { legacySettingsPath, ProjectPaths.GetLegacySettingsPath() }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in migrationCandidates)
        {
            if (!File.Exists(candidate)) continue;
            var migrated = Read(candidate!);
            Save(migrated, createProjectsDirectory: false);
            return WithDefaults(migrated);
        }
        return WithDefaults(new BuilderSettings());
    }

    public void Save(BuilderSettings settings, bool createProjectsDirectory = true)
    {
        ArgumentNullException.ThrowIfNull(settings);
        WithDefaults(settings);
        settings.RFiDGear.ExecutablePath = NormalizeExecutable(settings.RFiDGear.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(settings.Projects.Directory))
        {
            settings.Projects.Directory = Path.GetFullPath(settings.Projects.Directory);
            if (createProjectsDirectory) Directory.CreateDirectory(settings.Projects.Directory);
            if (File.Exists(settings.Projects.Directory)) throw new InvalidDataException($"Projektbibliothek ist kein Ordner: {settings.Projects.Directory}");
        }
        if (!string.IsNullOrWhiteSpace(settings.Profiles.Directory))
        {
            settings.Profiles.Directory = Path.GetFullPath(settings.Profiles.Directory);
            if (createProjectsDirectory) Directory.CreateDirectory(settings.Profiles.Directory);
            if (File.Exists(settings.Profiles.Directory)) throw new InvalidDataException($"Profilbibliothek ist kein Ordner: {settings.Profiles.Directory}");
        }
        if (!string.IsNullOrWhiteSpace(settings.VaultBackup.Directory))
        {
            settings.VaultBackup.Directory = Path.GetFullPath(settings.VaultBackup.Directory);
            if (createProjectsDirectory) Directory.CreateDirectory(settings.VaultBackup.Directory);
            if (File.Exists(settings.VaultBackup.Directory)) throw new InvalidDataException($"Vault-Sicherungsziel ist kein Ordner: {settings.VaultBackup.Directory}");
        }
        AtomicSettingsWriter.Write(settingsPath, settings);
    }

    private static BuilderSettings WithDefaults(BuilderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.VaultBackup.Directory))
            settings.VaultBackup.Directory = ProjectPaths.GetDefaultVaultBackupDirectory();
        return settings;
    }

    public string ResolveProjectsDirectory(string builderDirectory, BuilderSettings? settings = null)
    {
        var loaded = settings ?? Load();
        var directory = ProjectPaths.ResolveLibrary(loaded.Projects.Directory, builderDirectory);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string ResolveProfilesDirectory(BuilderSettings? settings = null)
    {
        var loaded = settings ?? Load();
        var directory = string.IsNullOrWhiteSpace(loaded.Profiles.Directory)
            ? Path.Combine(Path.GetDirectoryName(settingsPath)!, "profiles")
            : Path.GetFullPath(loaded.Profiles.Directory);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static BuilderSettings Read(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<BuilderSettings>(stream, SettingsJson.Options)
            ?? throw new InvalidDataException($"Builder-Einstellungen sind leer: {path}");
    }

    private static string NormalizeExecutable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var path = Path.GetFullPath(value.Trim());
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException($"RFiDGear.exe wurde nicht gefunden: {path}", path);
        return path;
    }
}

internal static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

internal static class AtomicSettingsWriter
{
    public static void Write(string path, BuilderSettings settings)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, SettingsJson.Options);
                stream.Flush(true);
            }
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

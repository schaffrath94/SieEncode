namespace RFiDGear.Builder.Core;

public static class ProjectPaths
{
    public static string ResolveLibrary(string? configuredDirectory, string builderDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builderDirectory);
        var selected = string.IsNullOrWhiteSpace(configuredDirectory)
            ? GetDefaultProjectsDirectory()
            : configuredDirectory;
        return Path.GetFullPath(selected);
    }

    public static string ResolveOutput(string? configuredOutput, string projectsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsDirectory);
        if (string.IsNullOrWhiteSpace(configuredOutput)) return Path.GetFullPath(projectsDirectory);
        return Path.IsPathRooted(configuredOutput)
            ? Path.GetFullPath(configuredOutput)
            : Path.GetFullPath(Path.Combine(projectsDirectory, configuredOutput));
    }

    public static string ResolveBuiltProject(ProjectDefinition definition, string projectsDirectory)
        => Path.Combine(ResolveOutput(definition.Project.OutputDirectory, projectsDirectory), definition.Project.Name);

    public static string GetSettingsPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SieEncode", "studio.settings.json");

    public static string GetDefaultVaultBackupDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "VaultBackups");

    public static string GetLegacySettingsPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RFiDGear-Projektbuilder", "builder.settings.json");

    public static string GetDefaultProjectsDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SieEncode-Projekte");
}

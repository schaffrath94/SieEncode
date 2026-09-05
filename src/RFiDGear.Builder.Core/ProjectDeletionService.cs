namespace RFiDGear.Builder.Core;

public sealed record ProjectDeletionPreview(
    string ProjectName,
    string ProjectsRoot,
    string SourcePath,
    FileRevision SourceRevision,
    string VaultPath,
    bool VaultExists,
    string BuildDirectory,
    bool BuildDirectoryExists,
    string Contract);

public sealed record ProjectDeletionResult(
    string SourcePath,
    bool SourceDeleted,
    string VaultPath,
    bool VaultPreserved,
    string BuildDirectory,
    bool BuildDirectoryPreserved);

public sealed class ProjectDeletionService
{
    public ProjectDeletionPreview Prepare(ProjectCatalogEntry entry, string projectsRoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsRoot);
        if (entry.ArtifactOnly)
            throw new InvalidOperationException("Gebaute Projektartefakte sind keine bearbeitbare Projektdefinition und werden über diese Aktion nicht gelöscht.");

        var root = Path.GetFullPath(projectsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var source = Path.GetFullPath(entry.SourcePath);
        RequireWithinRoot(source, root);
        if (string.Equals(Path.GetFileName(source), "project.definition.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Die generierte project.definition.json ist kein löschbarer Projektentwurf.");
        if (!File.Exists(source)) throw new FileNotFoundException("Projektdefinition wurde nicht gefunden.", source);

        var project = ProjectJson.Load(source);
        if (!string.Equals(project.Project.Name, entry.Name, StringComparison.Ordinal))
            throw new InvalidDataException("Projektname in Katalog und Quelldatei stimmt nicht überein. Bitte Projektliste aktualisieren.");

        var vault = ProjectKeyVault.GetPath(source);
        var buildDirectory = Path.GetFullPath(entry.ProjectDirectory);
        return new ProjectDeletionPreview(
            entry.Name, root, source, ConcurrentFileWriter.Capture(source), vault, File.Exists(vault),
            buildDirectory, Directory.Exists(buildDirectory),
            "Gelöscht wird ausschließlich die bearbeitbare Projektdefinition. Vault, gebaute Projektordner, rfPrj-Dateien, Payloads und Berichte bleiben erhalten.");
    }

    public ProjectDeletionResult Delete(ProjectDeletionPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var root = Path.GetFullPath(preview.ProjectsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var source = Path.GetFullPath(preview.SourcePath);
        RequireWithinRoot(source, root);
        ConcurrentFileWriter.Delete(source, preview.SourceRevision);
        return new ProjectDeletionResult(
            source, !File.Exists(source), preview.VaultPath, File.Exists(preview.VaultPath),
            preview.BuildDirectory, Directory.Exists(preview.BuildDirectory));
    }

    private static void RequireWithinRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Projektdefinition liegt außerhalb des eingestellten übergeordneten Projektordners und darf nicht gelöscht werden.");
    }
}

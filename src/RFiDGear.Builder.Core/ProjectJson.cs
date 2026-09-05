using System.Text.Json;

namespace RFiDGear.Builder.Core;

public static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    public static ProjectDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProjectDefinition>(stream, Options)
            ?? throw new InvalidDataException($"Projektdefinition ist leer: {path}");
    }

    public static void Save(string path, ProjectDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definition);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, definition, Options);
                stream.Flush(true);
            }

            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static FileRevision Save(string path, ProjectDefinition definition, FileRevision expected)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, definition, Options);
            stream.Flush(true);
        });
    }

    public static ProjectDefinition WithoutProductiveSecrets(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = JsonSerializer.Deserialize<ProjectDefinition>(JsonSerializer.Serialize(source, Options), Options)!;
        clone.Productive.PiccKey.Value = "";
        foreach (var aid in clone.Productive.ApplicationKeys.Keys.ToArray())
            clone.Productive.ApplicationKeys[aid] = [];
        return clone;
    }

    public static ProjectDefinition WithoutAllSecrets(ProjectDefinition source)
    {
        var clone = WithoutProductiveSecrets(source);
        clone.Transport.PiccKey.Value = "";
        foreach (var keyType in clone.Transport.ApplicationKeys.Keys.ToArray())
            clone.Transport.ApplicationKeys[keyType] = "";
        return clone;
    }
}

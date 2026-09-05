using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed class ProfileDefinition
{
    public ProfileMetadata Profile { get; set; } = new();
    public PiccProfileSettings? Picc { get; set; }
    public List<VariableDefinition> Variables { get; set; } = [];
    public List<ApplicationDefinition> Applications { get; set; } = [];
}

public sealed class ProfileMetadata
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ModifiedAt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousVersion { get; set; }
}

public sealed class PiccProfileSettings
{
    public string ProductiveKeySettings { get; set; } = "";
}

public sealed class VariableDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Type { get; set; } = "";
    public int? Length { get; set; }
    public List<int>? AllowedLengths { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Default { get; set; }
    public bool Required { get; set; }
}

public sealed class ApplicationDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string KeyType { get; set; } = "";
    public int KeyCount { get; set; }
    public string TransportKeySettings { get; set; } = "";
    public string ProductiveKeySettings { get; set; } = "";
    public string ChangeKeyMode { get; set; } = "";
    public List<FileDefinition> Files { get; set; } = [];
}

public sealed class FileDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Size { get; set; }
    public string Communication { get; set; } = "";
    public AccessDefinition Access { get; set; } = new();
    public FileSettingsDefinition? CreationSettings { get; set; }
    public MasteringInitialization? MasteringInitialization { get; set; }
    public CodingDefinition? Coding { get; set; }
}

public sealed class FileSettingsDefinition
{
    public string Communication { get; set; } = "";
    public AccessDefinition Access { get; set; } = new();
}

public sealed class AccessDefinition
{
    public int Read { get; set; }
    public int Write { get; set; }
    public int ReadWrite { get; set; }
    public int Change { get; set; }
}

public sealed class MasteringInitialization
{
    public string Type { get; set; } = "";
}

public sealed class CodingDefinition
{
    public string Output { get; set; } = "";
    public List<RecipeStep> Recipe { get; set; } = [];
}

public sealed class RecipeStep
{
    public string Op { get; set; } = "";
    public string? Value { get; set; }
    public string? Source { get; set; }
    public int? Width { get; set; }
    public string? PadLeft { get; set; }
    public string? PadRight { get; set; }
}

public sealed record ComposedProfiles(
    IReadOnlyList<ProfileDefinition> Profiles,
    IReadOnlyList<ApplicationDefinition> Applications,
    IReadOnlyList<VariableDefinition> Variables);

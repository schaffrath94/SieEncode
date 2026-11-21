using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed class ProjectDefinition
{
    // Ephemeral, non-serializable marker set only by an authorized library resolver.
    // Never persist a decrypted build document as a project definition.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TransientKeyLibraryResolution { get; set; }

    public ProjectSection Project { get; set; } = new();
    public ProfileSelection Profiles { get; set; } = new();
    public TransportConfiguration Transport { get; set; } = new();
    public ProductiveConfiguration Productive { get; set; } = new();
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ProjectSection
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectGroupVaultBinding? GroupVault { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectKeyLibrarySelection? KeyLibrary { get; set; }
    public string Name { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public List<string> Build { get; set; } = [];
    public string CardMode { get; set; } = CardOperationMode.FullCard;
}

public sealed record ProjectGroupVaultBinding(Guid LibraryId, string GroupId);

public sealed class ProfileSelection
{
    public string Base { get; set; } = "";
    public List<string> Customers { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ProfileBinding>? Bindings { get; set; }
}

public sealed class ProfileBinding
{
    public string Version { get; set; } = "";
    public string Digest { get; set; } = "";
}

public sealed class TransportConfiguration
{
    public string KeySetId { get; set; } = "";
    public KeyReference PiccKey { get; set; } = new();
    public Dictionary<string, string> ApplicationKeys { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ProductiveConfiguration
{
    public KeyReference PiccKey { get; set; } = new();
    public string PiccKeySettings { get; set; } = "0F";
    public Dictionary<string, List<string>> ApplicationKeys { get; set; } = new(StringComparer.Ordinal);
}

public sealed class KeyReference
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed record ValidationIssue(string Code, string Message);

public sealed class BuilderSettings
{
    public bool ExpertMode { get; set; }
    public RFiDGearSettings RFiDGear { get; set; } = new();
    public ProjectsSettings Projects { get; set; } = new();
    public ProfilesSettings Profiles { get; set; } = new();
    public VaultBackupSettings VaultBackup { get; set; } = new();
}

public sealed class RFiDGearSettings
{
    public string ExecutablePath { get; set; } = "";
}

public sealed class ProjectsSettings
{
    public string Directory { get; set; } = "";
}

public sealed class ProfilesSettings
{
    public string Directory { get; set; } = "";
}

public sealed class VaultBackupSettings
{
    public string Directory { get; set; } = "";
}

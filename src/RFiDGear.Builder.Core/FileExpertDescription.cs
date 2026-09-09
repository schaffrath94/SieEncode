using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>A snapshot of profile file settings and recipes; never reads project credentials.</summary>
public sealed record FileExpertDescription(string FinalRights, string CreationRights, string Recipe)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static FileExpertDescription FromFile(FileDefinition file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new(DescribeRights(file.Access),
            file.CreationSettings is null
                ? "Keine abweichenden Erstellungseinstellungen definiert."
                : file.CreationSettings.Communication + Environment.NewLine + DescribeRights(file.CreationSettings.Access),
            file.Coding is null || file.Coding.Recipe.Count == 0
                ? "Kein Kodierrezept definiert."
                : JsonSerializer.Serialize(file.Coding.Recipe, Options));
    }

    private static string DescribeRights(AccessDefinition access) => string.Join(Environment.NewLine,
        "Lesen: " + DescribeRight(access.Read), "Schreiben: " + DescribeRight(access.Write),
        "Lesen/Schreiben: " + DescribeRight(access.ReadWrite), "Rechte ändern: " + DescribeRight(access.Change));

    private static string DescribeRight(int value) => value switch
    {
        >= 0 and <= 13 => $"Schlüssel {value} (0x{value:X1})",
        14 => "frei (0xE)", 15 => "gesperrt (0xF)", _ => $"Ungültiger Rechtewert: {value}"
    };
}

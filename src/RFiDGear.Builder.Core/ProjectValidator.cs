using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public static partial class ProjectValidator
{
    private static readonly HashSet<string> BuildModes = new(StringComparer.OrdinalIgnoreCase) { "Mastering", "Coding" };
    private static readonly HashSet<string> CardModes = new(StringComparer.OrdinalIgnoreCase) { CardOperationMode.FullCard, CardOperationMode.ExtendExisting };
    private static readonly IReadOnlyDictionary<string, int> KeyLengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["DF_KEY_DES"] = 32,
        ["DF_KEY_AES"] = 32,
        ["DF_KEY_3K3DES"] = 48
    };

    public static IReadOnlyList<ValidationIssue> Validate(ProjectDefinition definition, bool requireProductiveSecrets = false, bool requireTransportSecrets = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(definition.Project.Name)) issues.Add(new("PROJECT_NAME", "Projektname fehlt."));
        else if (definition.Project.Name is "." or ".." || definition.Project.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            issues.Add(new("PROJECT_NAME", "Projektname darf keine Pfad- oder ungültigen Dateinamenszeichen enthalten."));
        if (string.IsNullOrWhiteSpace(definition.Profiles.Base)) issues.Add(new("BASE_PROFILE", "Masterprofil fehlt."));
        if (definition.Project.Build.Count == 0 || definition.Project.Build.Any(mode => !BuildModes.Contains(mode)))
            issues.Add(new("BUILD_MODE", "Build muss Mastering und/oder Coding enthalten."));
        if (!CardModes.Contains(definition.Project.CardMode ?? ""))
            issues.Add(new("CARD_MODE", "Kartenvorgang muss FullCard oder ExtendExisting sein."));

        ValidateKey(issues, definition.Transport.PiccKey, "TRANSPORT_PICC", required: requireTransportSecrets);
        foreach (var pair in definition.Transport.ApplicationKeys)
            ValidateKeyValue(issues, pair.Key, pair.Value, "TRANSPORT_APP_KEY", required: requireTransportSecrets);

        if (!CardOperationMode.IsExtension(definition) && !PiccSettingsRegex().IsMatch(definition.Productive.PiccKeySettings ?? ""))
            issues.Add(new("PICC_SETTINGS", "Produktive PICC KeySettings müssen zwischen 00 und 0F liegen."));

        var coding = definition.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        ValidateKey(issues, definition.Productive.PiccKey, "PRODUCTIVE_PICC", requireProductiveSecrets && coding && !CardOperationMode.IsExtension(definition));
        if (requireProductiveSecrets && coding)
        {
            foreach (var pair in definition.Productive.ApplicationKeys.Where(pair =>
                         !CardOperationMode.IsExtension(definition) || pair.Value.Any(value => !string.IsNullOrWhiteSpace(value))))
            {
                if (pair.Value.Count == 0)
                    issues.Add(new("PRODUCTIVE_APP_KEY", $"Produktive Application Keys für {pair.Key} fehlen."));
                foreach (var value in pair.Value)
                {
                    if (string.IsNullOrWhiteSpace(value) || !HexKeyRegex().IsMatch(value) || (value.Length != 32 && value.Length != 48))
                        issues.Add(new("PRODUCTIVE_APP_KEY", $"Produktiver Application Key für {pair.Key} ist leer oder ungültig."));
                }
            }
        }

        return issues;
    }

    private static void ValidateKey(List<ValidationIssue> issues, KeyReference key, string code, bool required)
        => ValidateKeyValue(issues, key.Type, key.Value, code, required);

    private static void ValidateKeyValue(List<ValidationIssue> issues, string type, string value, string code, bool required)
    {
        if (!KeyLengths.TryGetValue(type ?? "", out var length))
        {
            issues.Add(new(code, $"Unbekannter Keytyp: {type}"));
            return;
        }

        if (!required && string.IsNullOrWhiteSpace(value)) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length != length || !HexKeyRegex().IsMatch(value))
            issues.Add(new(code, $"Key {type} muss aus {length} Hex-Zeichen bestehen."));
    }

    [GeneratedRegex("^[0-9A-Fa-f]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HexKeyRegex();

    [GeneratedRegex("^0[0-9A-Fa-f]$", RegexOptions.CultureInvariant)]
    private static partial Regex PiccSettingsRegex();
}

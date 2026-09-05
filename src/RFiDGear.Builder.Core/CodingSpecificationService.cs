using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class CodingSpecificationService
{
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BuildText(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = ProfileValidator.Validate(profile);
        if (issues.Count > 0)
            throw new InvalidDataException("Profil ist ungültig: " + string.Join("; ", issues.Select(issue => $"{issue.Code}: {issue.Message}")));

        var lines = new List<string>
        {
            "RFiDGear Kodiervorschrift",
            "=======================",
            "",
            "Profil-ID: " + profile.Profile.Id,
            "Kennung: " + profile.Profile.Code,
            "Name: " + profile.Profile.Name,
            "Typ: " + profile.Profile.Kind,
            "Version: " + profile.Profile.Version
        };
        if (!string.IsNullOrWhiteSpace(profile.Profile.Description)) lines.Add("Beschreibung: " + profile.Profile.Description);
        if (!string.IsNullOrWhiteSpace(profile.Picc?.ProductiveKeySettings))
            lines.Add("PICC Produktiv-KeySettings: " + KeySettingsText(profile.Picc.ProductiveKeySettings));

        if (profile.Variables.Count > 0)
        {
            lines.AddRange(["", "Variablen", "---------"]);
            foreach (var variable in profile.Variables)
            {
                var constraint = variable.Length is not null
                    ? $"Länge {variable.Length.Value}"
                    : variable.AllowedLengths is not null
                        ? "Längen " + string.Join("/", variable.AllowedLengths)
                        : variable.MinLength is not null || variable.MaxLength is not null
                            ? $"Länge {Text(variable.MinLength)}..{Text(variable.MaxLength)}"
                            : "";
                lines.Add($"- {variable.Id}: {variable.Label}; Scope={variable.Scope}; Typ={variable.Type}; {constraint}");
            }
        }

        foreach (var application in profile.Applications)
        {
            lines.Add("");
            lines.Add($"[Application {application.Id}]");
            lines.Add("Name = " + application.Name);
            lines.Add("Verschlüsselung = " + application.KeyType);
            lines.Add("Anzahl Keys = " + application.KeyCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Transport-KeySettings = " + KeySettingsText(application.TransportKeySettings));
            lines.Add("Produktiv-KeySettings = " + KeySettingsText(application.ProductiveKeySettings));
            lines.Add("Change-Key-Modus = " + application.ChangeKeyMode);

            foreach (var file in application.Files)
            {
                lines.Add("");
                lines.Add($"  File {file.Id}: {file.Name}");
                lines.Add("  Typ = " + file.Type);
                lines.Add($"  Größe = {file.Size} Byte");
                lines.Add("  Communication = " + file.Communication);
                lines.Add("  Access Rights = " + AccessText(file.Access));
                if (file.CreationSettings is not null)
                {
                    lines.Add($"  Mastering temporär = {file.CreationSettings.Communication}, {AccessText(file.CreationSettings.Access)}");
                    lines.Add("  Danach = ChangeFileSettings auf den oben angegebenen Endzustand");
                }
                if (file.MasteringInitialization is not null)
                    lines.Add("  Mastering-Initialisierung = " + file.MasteringInitialization.Type);
                if (file.Coding is not null)
                {
                    lines.Add("  Kodierdatei = " + file.Coding.Output);
                    lines.Add("  Kodieraufbau = " + RecipeText(file.Coding.Recipe));
                }
            }
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public string Export(ProfileDefinition profile, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(BuildText(profile));
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
            return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string ExportFile(string profilePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        using var stream = File.OpenRead(Path.GetFullPath(profilePath));
        var profile = JsonSerializer.Deserialize<ProfileDefinition>(stream, ProfileJsonOptions)
            ?? throw new InvalidDataException("Profil ist leer: " + profilePath);
        return Export(profile, outputPath);
    }

    private static string AccessText(AccessDefinition access)
        => $"Read=Key{access.Read}, Write=Key{access.Write}, Read/Write=Key{access.ReadWrite}, Change=Key{access.Change}";

    private static string KeySettingsText(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || normalized[0] != '0' || !int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)) return normalized;
        var parts = new[]
        {
            (parsed & 1) != 0 ? "MasterKey änderbar" : "MasterKey gesperrt",
            (parsed & 2) != 0 ? "Verzeichnis/Settings ohne MK lesbar" : "Verzeichnis/Settings nur mit MK",
            (parsed & 4) != 0 ? "Create/Delete ohne MK erlaubt" : "Create/Delete nur mit MK",
            (parsed & 8) != 0 ? "Settings änderbar" : "Settings gesperrt"
        };
        return $"{normalized} ({string.Join("; ", parts)})";
    }

    private static string RecipeText(IEnumerable<RecipeStep> recipe) => string.Join(" + ", recipe.Select(step => step.Op switch
    {
        "fixedHex" => $"HEX[{step.Value}]",
        "asciiDigits" => $"ASCII({step.Source}, Breite {step.Width}, links '{step.PadLeft}')",
        "bcdDigits" => $"BCD({step.Source}, Breite {step.Width}, links '{step.PadLeft}')",
        "zeroPadToFileSize" => "mit 00 bis Filegröße auffüllen",
        _ => step.Op
    }));

    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
}

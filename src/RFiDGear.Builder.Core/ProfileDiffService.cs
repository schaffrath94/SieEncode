using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record ProfileDiffItem(string Change, string Scope, string Target, string Property, string Left, string Right, string Impact);

public sealed class ProfileDiffReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string GeneratedAt { get; init; } = "";
    public string Left { get; init; } = "";
    public string Right { get; init; } = "";
    public List<ProfileDiffItem> Items { get; init; } = [];
    public int Added => Items.Count(item => item.Change == "HINZU");
    public int Removed => Items.Count(item => item.Change == "ENTFERNT");
    public int Changed => Items.Count(item => item.Change == "GEÄNDERT");
    public bool Identical => Items.Count == 0;
    public bool ContainsKeys => false;
}

public sealed record ProfileDiffFiles(ProfileDiffReport Report, string TextPath, string JsonPath);

public sealed class ProfileDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public ProfileDiffReport Compare(ComposedProfiles left, ComposedProfiles right, string leftLabel, string rightLabel, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var report = new ProfileDiffReport
        {
            GeneratedAt = (now ?? DateTime.Now).ToString("s", CultureInfo.InvariantCulture),
            Left = leftLabel,
            Right = rightLabel
        };

        CompareMap(report, "Profil", left.Profiles, right.Profiles, profile => profile.Profile.Id,
            profile => profile.Profile.Name,
            (target, a, b) =>
            {
                Difference(report, "Profil", target, "Code", a.Profile.Code, b.Profile.Code, "Identität");
                Difference(report, "Profil", target, "Name", a.Profile.Name, b.Profile.Name, "Dokumentation");
                Difference(report, "Profil", target, "Typ", a.Profile.Kind, b.Profile.Kind, "Komposition");
                Difference(report, "Profil", target, "Version", a.Profile.Version, b.Profile.Version, "Reproduzierbarkeit");
                Difference(report, "Profil", target, "Beschreibung", a.Profile.Description, b.Profile.Description, "Dokumentation");
                Difference(report, "Profil", target, "PICC KeySettings", a.Picc?.ProductiveKeySettings ?? "–", b.Picc?.ProductiveKeySettings ?? "–", "Security");
            });

        CompareMap(report, "Variable", left.Variables, right.Variables, variable => variable.Id,
            variable => variable.Label,
            (target, a, b) => Difference(report, "Variable", target, "Definition", Variable(a), Variable(b), "Kodierdaten"));

        CompareMap(report, "Application", left.Applications, right.Applications, application => ProfileValidator.NormalizeAid(application.Id),
            application => application.Name,
            (aid, a, b) =>
            {
                Difference(report, "Application", aid, "Name", a.Name, b.Name, "Dokumentation");
                Difference(report, "Application", aid, "Keytyp", a.KeyType, b.KeyType, "Security");
                Difference(report, "Application", aid, "Keyanzahl", a.KeyCount.ToString(CultureInfo.InvariantCulture), b.KeyCount.ToString(CultureInfo.InvariantCulture), "Security");
                Difference(report, "Application", aid, "Transport-KeySettings", a.TransportKeySettings, b.TransportKeySettings, "Security");
                Difference(report, "Application", aid, "Produktiv-KeySettings", a.ProductiveKeySettings, b.ProductiveKeySettings, "Security");
                Difference(report, "Application", aid, "Change-Key-Modus", a.ChangeKeyMode, b.ChangeKeyMode, "Security");
                CompareMap(report, "File", a.Files, b.Files, file => file.Id.ToString("D2", CultureInfo.InvariantCulture), file => file.Name,
                    (fileTarget, x, y) => CompareFile(report, fileTarget, x, y), $"AID {aid} / File ");
            });
        var leftApplications = left.Applications.ToDictionary(application => ProfileValidator.NormalizeAid(application.Id), StringComparer.OrdinalIgnoreCase);
        var rightApplications = right.Applications.ToDictionary(application => ProfileValidator.NormalizeAid(application.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var aid in rightApplications.Keys.Except(leftApplications.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            foreach (var file in rightApplications[aid].Files.OrderBy(file => file.Id))
                report.Items.Add(new("HINZU", "File", $"AID {aid} / File {file.Id:D2}", "Element", "–", file.Name, "Kartenstruktur erweitert"));
        foreach (var aid in leftApplications.Keys.Except(rightApplications.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            foreach (var file in leftApplications[aid].Files.OrderBy(file => file.Id))
                report.Items.Add(new("ENTFERNT", "File", $"AID {aid} / File {file.Id:D2}", "Element", file.Name, "–", "Kartenstruktur reduziert"));
        return report;
    }

    public ProfileDiffFiles Save(ProfileDiffReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var textPath = Path.Combine(root, "Profilvergleich.txt");
        var jsonPath = Path.Combine(root, "Profilvergleich.json");
        AtomicWrite(textPath, FormatText(report));
        AtomicWrite(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        return new ProfileDiffFiles(report, textPath, jsonPath);
    }

    public ProfileDiffFiles CompareAndSave(ComposedProfiles left, ComposedProfiles right, string leftLabel, string rightLabel, string directory, DateTime? now = null)
        => Save(Compare(left, right, leftLabel, rightLabel, now), directory);

    private static void CompareFile(ProfileDiffReport report, string target, FileDefinition left, FileDefinition right)
    {
        Difference(report, "File", target, "Name", left.Name, right.Name, "Dokumentation");
        Difference(report, "File", target, "Typ", left.Type, right.Type, "Kartenstruktur");
        Difference(report, "File", target, "Größe", left.Size + " B", right.Size + " B", "Kartenstruktur / Payload");
        Difference(report, "File", target, "Communication Mode", left.Communication, right.Communication, "Security");
        Difference(report, "File", target, "Access Rights", Access(left.Access), Access(right.Access), "Security");
        Difference(report, "File", target, "Initiale FileSettings", Settings(left.CreationSettings), Settings(right.CreationSettings), "Mastering / Security");
        Difference(report, "File", target, "Mastering-Initialisierung", Initialization(left), Initialization(right), "Mastering");
        Difference(report, "File", target, "Kodierausgabe", left.Coding?.Output ?? "–", right.Coding?.Output ?? "–", "Kodierdaten");
        Difference(report, "File", target, "Kodierrezept", Recipe(left.Coding), Recipe(right.Coding), "Kodierdaten");
    }

    private static void CompareMap<T>(ProfileDiffReport report, string scope, IEnumerable<T> left, IEnumerable<T> right,
        Func<T, string> key, Func<T, string> display, Action<string, T, T> compare, string targetPrefix = "")
    {
        var a = left.ToDictionary(key, StringComparer.OrdinalIgnoreCase);
        var b = right.ToDictionary(key, StringComparer.OrdinalIgnoreCase);
        foreach (var id in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!a.TryGetValue(id, out var leftValue))
            {
                var rightValue = b[id];
                report.Items.Add(new("HINZU", scope, targetPrefix + id, "Element", "–", display(rightValue), AddedImpact(scope)));
            }
            else if (!b.TryGetValue(id, out var rightValue))
                report.Items.Add(new("ENTFERNT", scope, targetPrefix + id, "Element", display(leftValue), "–", RemovedImpact(scope)));
            else compare(targetPrefix + id, leftValue, rightValue);
        }
    }

    private static void Difference(ProfileDiffReport report, string scope, string target, string property, string left, string right, string impact)
    {
        if (!string.Equals(left, right, StringComparison.Ordinal)) report.Items.Add(new("GEÄNDERT", scope, target, property, left, right, impact));
    }

    private static string Variable(VariableDefinition value)
    {
        var length = value.Length is { } exact ? "Länge=" + exact : value.AllowedLengths is { Count: > 0 } allowed ? "Längen=" + string.Join("/", allowed) : $"Länge={value.MinLength?.ToString() ?? "–"}..{value.MaxLength?.ToString() ?? "–"}";
        return $"{value.Label} · {value.Scope}/{value.Type} · {length} · Pflicht={(value.Required ? "ja" : "nein")} · Default={value.Default ?? "–"}";
    }

    private static string Access(AccessDefinition value) => $"R{value.Read} / W{value.Write} / RW{value.ReadWrite} / C{value.Change}";
    private static string Settings(FileSettingsDefinition? value) => value is null ? "direkt im Endzustand" : value.Communication + " · " + Access(value.Access);
    private static string Initialization(FileDefinition value) => value.MasteringInitialization?.Type ?? (value.Type is "StdDataFile" or "BackupFile" ? "zeroFill" : "none");
    private static string Recipe(CodingDefinition? value) => value is null ? "–" : string.Join(" → ", value.Recipe.Select(step => step.Op switch
    {
        "fixedHex" or "fixedAscii" or "fixedUtf8" => $"{step.Op}({step.Value})",
        "asciiDigits" or "bcdDigits" or "hexBytes" or "asciiText" or "utf8Text" or "uintBigEndian" or "uintLittleEndian" => $"{step.Op}({step.Source}, Breite {step.Width}, Pad L={step.PadLeft ?? "–"}/R={step.PadRight ?? "–"})",
        "padToFileSize" => $"padToFileSize({step.Value})",
        _ => step.Op
    }));
    private static string AddedImpact(string scope) => scope switch { "Application" or "File" => "Kartenstruktur erweitert", "Variable" => "Neue Eingabe/Kodierregel", _ => "Komposition erweitert" };
    private static string RemovedImpact(string scope) => scope switch { "Application" or "File" => "Kartenstruktur reduziert", "Variable" => "Eingabe/Kodierregel entfällt", _ => "Komposition reduziert" };

    private static string FormatText(ProfileDiffReport report)
    {
        var lines = new List<string>
        {
            "RFiDGear Projekt-/Profilvergleich", new('=', 40),
            "Links : " + report.Left, "Rechts: " + report.Right, ""
        };
        if (report.Identical) lines.Add("Keine fachlichen Unterschiede gefunden.");
        else foreach (var item in report.Items)
        {
            var marker = item.Change switch { "HINZU" => "+", "ENTFERNT" => "-", _ => "~" };
            lines.Add($"{marker} [{item.Scope}] {item.Target} · {item.Property}");
            lines.Add($"    links : {item.Left}");
            lines.Add($"    rechts: {item.Right}");
            lines.Add($"    Wirkung: {item.Impact}");
        }
        lines.Add("");
        lines.Add($"Ergebnis: {report.Added} hinzugefügt · {report.Removed} entfernt · {report.Changed} geändert");
        lines.Add("Schlüsselwerte: nicht enthalten");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void AtomicWrite(string path, string text)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

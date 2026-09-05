using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record CompatibilityMatrixScenarioResult(
    string Id,
    string Title,
    string Status,
    IReadOnlyList<string> KeyTypes,
    IReadOnlyList<string> FileTypes,
    IReadOnlyList<string> Communications,
    IReadOnlyList<string> RecipeOperations,
    int Profiles,
    int Applications,
    int Files,
    int Payloads,
    int RfPrjFiles,
    int Tasks,
    string? Error);

public sealed record CompatibilityMatrixReport(
    string SchemaVersion,
    string GeneratedAt,
    string CoverageMethod,
    string HardwareStatus,
    IReadOnlyList<CompatibilityMatrixScenarioResult> Scenarios)
{
    public bool Successful => Scenarios.Count > 0 && Scenarios.All(item => item.Status == "OK");
}

public sealed record CompatibilityMatrixFiles(CompatibilityMatrixReport Report, string JsonPath, string TextPath);

/// <summary>
/// Builds deliberately synthetic projects through the same production build path.
/// It proves all currently supported profile dimensions without adding test profiles
/// to the user's productive profile library. It does not claim hardware evidence.
/// </summary>
public sealed class CompatibilityMatrixService
{
    public CompatibilityMatrixFiles Run(TaskShellLibrary shells, string outputDirectory, string builderVersion)
    {
        ArgumentNullException.ThrowIfNull(shells);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(builderVersion);

        var results = new List<CompatibilityMatrixScenarioResult>();
        var root = Path.Combine(Path.GetTempPath(), "sieencode-compatibility-matrix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var scenario in Scenarios()) results.Add(RunScenario(shells, root, builderVersion, scenario));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        var report = new CompatibilityMatrixReport(
            "1.0",
            DateTimeOffset.Now.ToString("O"),
            "Repräsentative Grenz- und Paarabdeckung aller vom Profilmodell unterstützten Key-, Datei-, Kommunikations- und Payloadvarianten über ProjectBuildService; kein Anspruch auf die mathematisch vollständige Kombination aller Werte.",
            "Software-Nachweis. Die synthetischen Profile wurden nicht auf realen Transpondern kodiert; Hardwarefreigaben werden je Karten-/Chiptyp separat dokumentiert.",
            results);
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "SieEncode-Kompatibilitaetsmatrix.json");
        var textPath = Path.Combine(outputDirectory, "SieEncode-Kompatibilitaetsmatrix.txt");
        System.IO.File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }), new UTF8Encoding(false));
        System.IO.File.WriteAllText(textPath, ToText(report), new UTF8Encoding(false));
        return new CompatibilityMatrixFiles(report, jsonPath, textPath);
    }

    private static CompatibilityMatrixScenarioResult RunScenario(TaskShellLibrary shells, string root, string builderVersion, MatrixScenario scenario)
    {
        try
        {
            var library = ProfileLibrary.FromProfiles(scenario.Profiles);
            var profileIssues = library.Validate();
            if (profileIssues.Count > 0) throw new InvalidDataException(string.Join(" | ", profileIssues.Select(issue => issue.Code + ": " + issue.Message)));

            var scenarioRoot = Path.Combine(root, scenario.Id);
            Directory.CreateDirectory(scenarioRoot);
            var project = CreateProject(scenario, scenarioRoot);
            ProfileBindingService.BindCurrent(project, library);
            var sourcePath = Path.Combine(scenarioRoot, scenario.Id + ".project.json");
            ProjectJson.Save(sourcePath, ProjectJson.WithoutProductiveSecrets(project));
            var result = new ProjectBuildService().Build(project, library, shells, sourcePath, scenarioRoot, builderVersion);

            var composition = library.Compose(project.Profiles.Base, project.Profiles.Customers);
            var payloads = PayloadEngine.BuildAll(composition, project.Values, scenario.CardValues);
            foreach (var payload in payloads)
            {
                if (payload.Bytes.Length != composition.Applications.Single(app => app.Id.Equals(payload.Aid, StringComparison.OrdinalIgnoreCase)).Files.Single(file => file.Id == payload.FileId).Size)
                    throw new InvalidDataException($"Payload {payload.Aid}:{payload.FileId} besitzt eine falsche Länge.");
            }

            var artifact = ProjectPreflight.InspectArtifacts(result.ProjectDirectory);
            if (artifact.Issues.Count > 0) throw new InvalidDataException(string.Join(" | ", artifact.Issues.Select(issue => issue.Code + ": " + issue.Message)));
            var rfPrjPaths = Directory.EnumerateFiles(result.ProjectDirectory, "*.rfPrj", SearchOption.TopDirectoryOnly).ToArray();
            var documents = rfPrjPaths.Select(RfPrjArchive.Load).ToArray();
            if (documents.Any(document => document.ManifestVersion != "2.0.6"))
                throw new InvalidDataException("Mindestens ein Projekt verwendet nicht das rfPrj-Manifest 2.0.6.");
            if (documents.Any(document => RfPrjSecretProtection.FindPlaintextSecretElements(document).Count > 0))
                throw new InvalidDataException("Ein synthetisches rfPrj enthält Klartext-Schlüssel.");

            var applications = composition.Applications;
            return new CompatibilityMatrixScenarioResult(
                scenario.Id, scenario.Title, "OK",
                applications.Select(app => app.KeyType).Distinct(StringComparer.Ordinal).Order().ToArray(),
                applications.SelectMany(app => app.Files).Select(file => file.Type).Distinct(StringComparer.Ordinal).Order().ToArray(),
                applications.SelectMany(app => app.Files).Select(file => file.Communication).Distinct(StringComparer.Ordinal).Order().ToArray(),
                applications.SelectMany(app => app.Files).SelectMany(file => file.Coding?.Recipe ?? []).Select(step => step.Op).Distinct(StringComparer.Ordinal).Order().ToArray(),
                composition.Profiles.Count, applications.Count, applications.Sum(app => app.Files.Count), payloads.Count,
                rfPrjPaths.Length, documents.Sum(document => document.Tasks.Count), null);
        }
        catch (Exception exception)
        {
            return new CompatibilityMatrixScenarioResult(scenario.Id, scenario.Title, "FEHLER", [], [], [], [], scenario.Profiles.Count, 0, 0, 0, 0, 0, exception.Message);
        }
    }

    private static ProjectDefinition CreateProject(MatrixScenario scenario, string root)
    {
        var applications = scenario.Profiles.SelectMany(profile => profile.Applications).ToArray();
        var productive = applications.ToDictionary(
            app => ProfileValidator.NormalizeAid(app.Id),
            app => Enumerable.Range(0, app.KeyCount).Select(_ => KeyVaultService.GenerateKey(app.KeyType)).ToList(),
            StringComparer.Ordinal);
        return new ProjectDefinition
        {
            Project = new ProjectSection { Name = scenario.Id, OutputDirectory = scenario.Id + "-Build", Build = ["Mastering", "Coding"] },
            Profiles = new ProfileSelection
            {
                Base = scenario.Profiles.Single(profile => profile.Profile.Kind == "base").Profile.Id,
                Customers = scenario.Profiles.Where(profile => profile.Profile.Kind == "customer").Select(profile => profile.Profile.Id).ToList()
            },
            Transport = new TransportConfiguration
            {
                PiccKey = new KeyReference { Type = "DF_KEY_DES", Value = new string('0', 32) },
                ApplicationKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["DF_KEY_DES"] = new string('0', 32), ["DF_KEY_AES"] = new string('0', 32), ["DF_KEY_3K3DES"] = new string('0', 48)
                }
            },
            Productive = new ProductiveConfiguration
            {
                PiccKey = new KeyReference { Type = scenario.ProductivePiccType, Value = KeyVaultService.GenerateKey(scenario.ProductivePiccType) },
                PiccKeySettings = "0F", ApplicationKeys = productive
            },
            Values = new Dictionary<string, string>(scenario.ProjectValues, StringComparer.Ordinal)
        };
    }

    private static IReadOnlyList<MatrixScenario> Scenarios()
    {
        var aes = AesProfile("matrix-aes", "base");
        var des = DesProfile("matrix-des", "base");
        var triple = TripleDesProfile("matrix-3k3des", "base");
        var formats = FormatProfile("matrix-formats", "base");
        return
        [
            new("AES-Mixed", "AES mit Plain/MAC/Encrypt, Standard/Backup und temporärer Härtung", [aes], "DF_KEY_AES",
                new() { ["aesSite"] = "0042" }, new() { ["aesCard"] = "123" }),
            new("DES-Legacy", "DES mit beiden Dateitypen, variablen Längen und KeyNo-Wechsel", [des], "DF_KEY_DES",
                new() { ["desBatch"] = "12" }, new() { ["desCard"] = "12345678" }),
            new("3K3DES-Hardened", "3K3DES mit verschlüsselten Dateien, Grenz-FileID und mehreren Keys", [triple], "DF_KEY_3K3DES",
                new(), new() { ["tripleCard"] = "654321" }),
            new("Additive-Mixed", "Additive Kombination dreier unabhängiger erfundener Profile", [AesProfile("matrix-combined-base", "base"), DesProfile("matrix-combined-des", "customer"), TripleDesProfile("matrix-combined-3k", "customer")], "DF_KEY_AES",
                new() { ["aesSite"] = "0042", ["desBatch"] = "1234" }, new() { ["aesCard"] = "7", ["desCard"] = "87654321", ["tripleCard"] = "112233" }),
            new("Modular-Formats", "Modulares Infofile aus HEX, ASCII, UTF-8, BCD, Integerwerten und frei wählbarem Padding", [formats], "DF_KEY_AES",
                new() { ["formatToken"] = "A1B2C3", ["formatLabel"] = "OSS1", ["formatUnicode"] = "ÄÖ" }, new() { ["formatAmount"] = "513" })
        ];
    }

    private static ProfileDefinition AesProfile(string id, string kind) => new()
    {
        Profile = Meta(id, kind, "Synthetisches AES-Mischprofil"), Picc = new PiccProfileSettings { ProductiveKeySettings = "0F" },
        Variables =
        [
            new() { Id = "aesSite", Label = "Standort", Scope = "project", Type = "digits", Length = 4, Default = "0042", Required = true },
            new() { Id = "aesCard", Label = "Kartennummer", Scope = "card", Type = "digits", MinLength = 1, MaxLength = 6, Required = true }
        ],
        Applications =
        [
            App("A10001", "AES Mixed", "DF_KEY_AES", 3, "ChangeKeyUsingMK",
                File(0, "Plain Standard", "StdDataFile", 12, "CM_PLAIN", Access(0),
                    [Fixed("CAFE"), Ascii("aesSite", 4), Bcd("aesCard", 6), Pad()], "aes-plain.hex"),
                File(1, "MAC Backup", "BackupFile", 8, "CM_MAC", Access(1, 2, 1, 0),
                    [Fixed("0102030405060708")], "aes-mac.hex", initialization: "none"),
                File(2, "Encrypted Hardened", "StdDataFile", 32, "CM_ENCRYPT", Access(2, 2, 1, 0),
                    [Bcd("aesCard", 8), Pad()], "aes-encrypt.hex", creation: new FileSettingsDefinition { Communication = "CM_PLAIN", Access = Access(0) }))
        ]
    };

    private static ProfileDefinition DesProfile(string id, string kind) => new()
    {
        Profile = Meta(id, kind, "Synthetisches DES-Legacyprofil"),
        Variables =
        [
            new() { Id = "desBatch", Label = "Charge", Scope = "project", Type = "digits", AllowedLengths = [2, 4], Default = "12", Required = true },
            new() { Id = "desCard", Label = "Legacy-ID", Scope = "card", Type = "digits", Length = 8, Required = true }
        ],
        Applications =
        [
            App("A10002", "DES Legacy", "DF_KEY_DES", 2, "ChangeKeyUsingKeyNo",
                File(0, "Legacy Plain", "StdDataFile", 10, "CM_PLAIN", Access(0), [Ascii("desCard", 8), Bcd("desBatch", 4)], "des-plain.hex"),
                File(31, "Legacy MAC Backup", "BackupFile", 16, "CM_MAC", Access(1, 1, 0, 0), [Fixed("D35F"), Ascii("desCard", 8), Pad()], "des-backup.hex"))
        ]
    };

    private static ProfileDefinition TripleDesProfile(string id, string kind) => new()
    {
        Profile = Meta(id, kind, "Synthetisches 3K3DES-Profil"),
        Variables = [new() { Id = "tripleCard", Label = "3K-Karten-ID", Scope = "card", Type = "digits", Length = 6, Required = true }],
        Applications =
        [
            App("A10003", "3K3DES Hardened", "DF_KEY_3K3DES", 4, "ChangeKeyUsingMK",
                File(7, "3K Encrypted", "StdDataFile", 24, "CM_ENCRYPT", Access(3, 2, 1, 0), [Fixed("33CC"), Bcd("tripleCard", 6), Pad()], "3k-data.hex"),
                File(31, "3K Backup", "BackupFile", 6, "CM_ENCRYPT", Access(2, 3, 1, 0), [Ascii("tripleCard", 6)], "3k-backup.hex", initialization: "none"))
        ]
    };

    private static ProfileDefinition FormatProfile(string id, string kind) => new()
    {
        Profile = Meta(id, kind, "Synthetisches modulares Datenformatprofil"),
        Variables =
        [
            new() { Id = "formatToken", Label = "Rohe Kennung", Scope = "project", Type = "hex", Length = 6, Required = true },
            new() { Id = "formatLabel", Label = "ASCII-Bezeichnung", Scope = "project", Type = "text", Length = 4, Required = true },
            new() { Id = "formatUnicode", Label = "UTF-8-Bezeichnung", Scope = "project", Type = "text", Length = 2, Required = true },
            new() { Id = "formatAmount", Label = "Zahlenwert", Scope = "card", Type = "digits", MinLength = 1, MaxLength = 5, Required = true }
        ],
        Applications =
        [
            App("A10004", "Modular Formats", "DF_KEY_AES", 2, "ChangeKeyUsingMK",
                File(0, "Modulares Infofile", "StdDataFile", 24, "CM_ENCRYPT", Access(0),
                    [FixedAscii("OSS"), FixedUtf8("Ä"), HexBytes("formatToken", 3), AsciiText("formatLabel", 6, right: "20"), Utf8Text("formatUnicode", 4, right: "00"), UInt("formatAmount", 2, false), PadByte("FF")], "modular-info.hex"),
                File(1, "Little Endian", "BackupFile", 4, "CM_MAC", Access(1), [UInt("formatAmount", 4, true)], "modular-value.hex", initialization: "none"))
        ]
    };

    private static ProfileMetadata Meta(string id, string kind, string name) => new() { Id = id, Code = id.ToUpperInvariant(), Name = name, Kind = kind, Version = "1.0" };
    private static ApplicationDefinition App(string id, string name, string keyType, int keys, string mode, params FileDefinition[] files) => new()
    {
        Id = id, Name = name, KeyType = keyType, KeyCount = keys, TransportKeySettings = "0F", ProductiveKeySettings = "0B", ChangeKeyMode = mode, Files = [.. files]
    };
    private static FileDefinition File(int id, string name, string type, int size, string communication, AccessDefinition access, List<RecipeStep> recipe, string output,
        FileSettingsDefinition? creation = null, string? initialization = null) => new()
    {
        Id = id, Name = name, Type = type, Size = size, Communication = communication, Access = access, CreationSettings = creation,
        MasteringInitialization = initialization is null ? null : new MasteringInitialization { Type = initialization },
        Coding = new CodingDefinition { Output = output, Recipe = recipe }
    };
    private static AccessDefinition Access(int read, int? write = null, int? readWrite = null, int? change = null) => new()
    {
        Read = read, Write = write ?? read, ReadWrite = readWrite ?? read, Change = change ?? read
    };
    private static RecipeStep Fixed(string value) => new() { Op = "fixedHex", Value = value };
    private static RecipeStep FixedAscii(string value) => new() { Op = "fixedAscii", Value = value };
    private static RecipeStep FixedUtf8(string value) => new() { Op = "fixedUtf8", Value = value };
    private static RecipeStep HexBytes(string source, int width) => new() { Op = "hexBytes", Source = source, Width = width };
    private static RecipeStep AsciiText(string source, int width, string? left = null, string? right = null) => new() { Op = "asciiText", Source = source, Width = width, PadLeft = left, PadRight = right };
    private static RecipeStep Utf8Text(string source, int width, string? left = null, string? right = null) => new() { Op = "utf8Text", Source = source, Width = width, PadLeft = left, PadRight = right };
    private static RecipeStep UInt(string source, int width, bool little) => new() { Op = little ? "uintLittleEndian" : "uintBigEndian", Source = source, Width = width };
    private static RecipeStep Ascii(string source, int width) => new() { Op = "asciiDigits", Source = source, Width = width, PadLeft = "0" };
    private static RecipeStep Bcd(string source, int width) => new() { Op = "bcdDigits", Source = source, Width = width, PadLeft = "0" };
    private static RecipeStep Pad() => new() { Op = "zeroPadToFileSize" };
    private static RecipeStep PadByte(string value) => new() { Op = "padToFileSize", Value = value };

    private static string ToText(CompatibilityMatrixReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("SieEncode – synthetische Kompatibilitätsmatrix");
        text.AppendLine($"Erstellt: {report.GeneratedAt}");
        text.AppendLine($"Gesamtergebnis: {(report.Successful ? "BESTANDEN" : "FEHLER")}");
        text.AppendLine();
        text.AppendLine("Abdeckungsmethode: " + report.CoverageMethod);
        text.AppendLine("Hardwarestatus: " + report.HardwareStatus);
        foreach (var item in report.Scenarios)
        {
            text.AppendLine();
            text.AppendLine($"[{item.Status}] {item.Id} – {item.Title}");
            text.AppendLine($"  Profile {item.Profiles}, Applications {item.Applications}, Files {item.Files}, Payloads {item.Payloads}, rfPrj {item.RfPrjFiles}, Tasks {item.Tasks}");
            text.AppendLine($"  Keys: {string.Join(", ", item.KeyTypes)}");
            text.AppendLine($"  Dateien: {string.Join(", ", item.FileTypes)}");
            text.AppendLine($"  Kommunikation: {string.Join(", ", item.Communications)}");
            text.AppendLine($"  Payload: {string.Join(", ", item.RecipeOperations)}");
            if (!string.IsNullOrWhiteSpace(item.Error)) text.AppendLine("  Fehler: " + item.Error);
        }
        return text.ToString();
    }

    private sealed record MatrixScenario(string Id, string Title, IReadOnlyList<ProfileDefinition> Profiles, string ProductivePiccType,
        Dictionary<string, string> ProjectValues, Dictionary<string, string> CardValues);
}

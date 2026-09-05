using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record CodingSessionField(
    string Id, string Label, string Type, bool Required, string InitialValue, string Hint, int MaxLength);

public sealed record CodingSessionDefinition(
    string ProjectName,
    string ProjectDirectory,
    string CodingProjectPath,
    string VerificationDirectory,
    IReadOnlyList<CodingSessionField> CardFields,
    int PayloadCount,
    ComposedProfiles Profiles,
    IReadOnlyDictionary<string, string> ProjectValues,
    string KeySetIdentity = "");

public sealed record PreparedCodingSession(
    CodingSessionDefinition Definition,
    DateTimeOffset PreparedAt,
    IReadOnlyList<PayloadArtifact> Payloads,
    IReadOnlyList<VerificationExpectation> Expectations,
    IReadOnlyList<RfPrjTaskSummary> TaskMap,
    RFiDGearLogSnapshot LogSnapshot,
    WorkspaceLease WorkspaceLease) : IDisposable
{
    public void Dispose() => WorkspaceLease.Dispose();
}

public sealed class CodingSessionService
{
    private readonly RFiDGearLogMonitor logMonitor = new();
    private readonly PayloadPreparationStateService payloadState = new();

    public CodingSessionDefinition Load(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        var schemaPath = Path.Combine(root, "coding.schema.json");
        if (!File.Exists(schemaPath)) throw new FileNotFoundException("coding.schema.json fehlt.", schemaPath);

        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var schema = document.RootElement;
        var projectName = RequiredString(schema, "projectName");
        var keySetIdentity = OptionalString(schema, "keySetIdentity") ?? "";
        if (keySetIdentity.Length > 0 && (keySetIdentity.Length != 64 || keySetIdentity.Any(character => !Uri.IsHexDigit(character))))
            throw new InvalidDataException("coding.schema.json enthält keine gültige Keyset-ID.");
        var codingProjectPath = SafeChild(root, RequiredString(schema, "codingProject"), "RFiDGear-Kodierprojekt");
        if (!File.Exists(codingProjectPath)) throw new FileNotFoundException("RFiDGear-Kodierprojekt fehlt.", codingProjectPath);
        var verificationDirectory = OptionalString(schema, "finalVerificationDirectory") ?? "verification";
        _ = SafeChild(root, verificationDirectory, "Verification-Ordner");

        var variables = new List<VariableDefinition>();
        var projectValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in RequiredArray(schema, "variables"))
        {
            var variable = new VariableDefinition
            {
                Id = RequiredString(node, "id"), Label = RequiredString(node, "label"),
                Scope = RequiredString(node, "scope"), Type = RequiredString(node, "type"),
                Required = OptionalBoolean(node, "required"), Length = OptionalInt(node, "length"),
                AllowedLengths = OptionalIntArray(node, "allowedLengths"), MinLength = OptionalInt(node, "minLength"),
                MaxLength = OptionalInt(node, "maxLength"), Default = OptionalString(node, "default")
            };
            if (!variables.All(item => !string.Equals(item.Id, variable.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Variable ist mehrfach definiert: {variable.Id}");
            variables.Add(variable);
            if (string.Equals(variable.Scope, "project", StringComparison.OrdinalIgnoreCase))
                projectValues[variable.Id] = OptionalString(node, "value") ?? variable.Default ?? "";
        }

        var applications = new Dictionary<string, ApplicationDefinition>(StringComparer.OrdinalIgnoreCase);
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in RequiredArray(schema, "payloads"))
        {
            var aid = ProfileValidator.NormalizeAid(RequiredString(node, "aid"));
            if (!applications.TryGetValue(aid, out var application))
            {
                application = new ApplicationDefinition { Id = aid, Name = OptionalString(node, "appName") ?? aid };
                applications.Add(aid, application);
            }
            var output = RequiredString(node, "output");
            if (!outputs.Add(output)) throw new InvalidDataException($"Payload-Ausgabedatei ist mehrfach definiert: {output}");
            var recipeNode = RequiredProperty(node, "recipe");
            var recipe = JsonSerializer.Deserialize<List<RecipeStep>>(recipeNode.GetRawText(), ProjectJson.Options)
                ?? throw new InvalidDataException($"Kodierrezept ist leer: {output}");
            application.Files.Add(new FileDefinition
            {
                Id = RequiredInt(node, "fileId"), Name = OptionalString(node, "fileName") ?? output,
                Size = RequiredInt(node, "size"), Coding = new CodingDefinition { Output = output, Recipe = recipe }
            });
        }
        if (applications.Count == 0) throw new InvalidDataException("Kodierschema enthält keine Payloads.");

        var profiles = new ComposedProfiles([], applications.Values.ToArray(), variables);
        var fields = variables.Where(variable => string.Equals(variable.Scope, "card", StringComparison.OrdinalIgnoreCase))
            .Select(variable => new CodingSessionField(variable.Id, variable.Label, variable.Type, variable.Required, variable.Default ?? "", Hint(variable),
                variable.Length ?? variable.AllowedLengths?.DefaultIfEmpty(0).Max() ?? variable.MaxLength ?? 0)).ToArray();
        return new CodingSessionDefinition(projectName, root, codingProjectPath, verificationDirectory, fields,
            applications.Values.Sum(application => application.Files.Count), profiles, projectValues, keySetIdentity);
    }

    public PreparedCodingSession Prepare(CodingSessionDefinition definition, IReadOnlyDictionary<string, string> cardValues, string? logDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(cardValues);
        WorkspaceLease? workspaceLease = null;
        try
        {
            workspaceLease = WorkspaceLease.Acquire(definition.ProjectDirectory, "Kodiersitzung");
            payloadState.InvalidateUnderHeldLease(definition, "Neue Sitzung wird vorbereitet; vorherige Payloads sind nicht mehr freigegeben.");
            var normalizedCardValues = new Dictionary<string, string>(cardValues, StringComparer.OrdinalIgnoreCase);
            foreach (var variable in definition.Profiles.Variables)
            {
                var source = string.Equals(variable.Scope, "project", StringComparison.OrdinalIgnoreCase) ? definition.ProjectValues : normalizedCardValues;
                source.TryGetValue(variable.Id, out var value);
                value = string.IsNullOrEmpty(value) ? variable.Default ?? "" : value.Trim();
                ValidateValue(variable, value);
                if (string.Equals(variable.Scope, "card", StringComparison.OrdinalIgnoreCase)) normalizedCardValues[variable.Id] = value;
            }
            var payloads = PayloadEngine.BuildAll(definition.Profiles, definition.ProjectValues, normalizedCardValues);
            Directory.CreateDirectory(SafeChild(definition.ProjectDirectory, definition.VerificationDirectory, "Verification-Ordner"));
            var expectations = CodingRunEvaluator.CaptureBaseline(definition.ProjectDirectory, definition.VerificationDirectory, payloads);
            var logSnapshot = logMonitor.Snapshot(logDirectory);
            WritePayloads(definition.ProjectDirectory, payloads);
            var preparedAt = DateTimeOffset.Now;
            WriteLastCoding(definition, normalizedCardValues, payloads, preparedAt);
            WriteExpectedCard(definition, normalizedCardValues, payloads, preparedAt);
            var taskMap = RfPrjArchive.Load(definition.CodingProjectPath).Summarize();
            payloadState.MarkPreparedUnderHeldLease(definition, preparedAt);
            var result = new PreparedCodingSession(definition, preparedAt, payloads, expectations, taskMap, logSnapshot, workspaceLease);
            workspaceLease = null;
            return result;
        }
        finally { workspaceLease?.Dispose(); }
    }

    public PayloadInvalidationResult InvalidatePreparedPayloads(PreparedCodingSession session, string reason)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.WorkspaceLease.IsHeld) throw new InvalidOperationException("Die exklusive Kodiersitzung ist nicht mehr aktiv.");
        return payloadState.InvalidateUnderHeldLease(session.Definition, reason);
    }

    public CodingRunEvaluation Evaluate(PreparedCodingSession session, string? logDelta = null, IEnumerable<string>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsurePreparedPayloads(session);
        logDelta ??= logMonitor.ReadDelta(session.LogSnapshot).Text;
        return CodingRunEvaluator.Evaluate(session.Expectations, logDelta, session.TaskMap, knownSecrets);
    }

    public void EnsurePreparedPayloads(PreparedCodingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.WorkspaceLease.IsHeld) throw new InvalidOperationException("Die exklusive Kodiersitzung ist nicht mehr aktiv.");
        payloadState.RequirePrepared(session);
        var root = Path.Combine(session.Definition.ProjectDirectory, "payloads");
        foreach (var payload in session.Payloads)
        {
            var path = SafeChild(root, payload.Output, "Payload");
            if (!File.Exists(path)) throw new InvalidDataException($"Vorbereitete Payloaddatei fehlt: {payload.Output}");
            var compact = string.Concat(File.ReadAllText(path).Where(character => !char.IsWhiteSpace(character)));
            byte[] actual;
            try { actual = Convert.FromHexString(compact); }
            catch (FormatException exception) { throw new InvalidDataException($"Vorbereitete Payloaddatei ist nicht mehr gültig: {payload.Output}", exception); }
            if (!actual.AsSpan().SequenceEqual(payload.Bytes))
                throw new InvalidDataException($"Vorbereitete Payloaddatei wurde nach Sitzungsstart verändert: {payload.Output}. RFiDGear wird nicht gestartet; Datensatz bitte als fehlgeschlagen abschließen und neu vorbereiten.");
        }
    }

    private static void WritePayloads(string projectDirectory, IReadOnlyList<PayloadArtifact> payloads)
    {
        var payloadRoot = Path.GetFullPath(Path.Combine(projectDirectory, "payloads"));
        Directory.CreateDirectory(payloadRoot);
        var targets = payloads.Select(payload => (Payload: payload, Path: SafeChild(payloadRoot, payload.Output, "Payload"))).ToArray();
        foreach (var target in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target.Path)!);
            var temporary = target.Path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, PayloadEngine.ToHexFileText(target.Payload.Bytes));
                File.Move(temporary, target.Path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    private static void WriteLastCoding(
        CodingSessionDefinition definition,
        IReadOnlyDictionary<string, string> cardValues,
        IReadOnlyList<PayloadArtifact> payloads,
        DateTimeOffset preparedAt)
    {
        var values = new Dictionary<string, string>(definition.ProjectValues, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in cardValues) values[pair.Key] = pair.Value;
        var document = new
        {
            timestamp = preparedAt.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            project = definition.ProjectName,
            profiles = Array.Empty<string>(),
            values,
            payloadOnly = true,
            codingProject = Path.GetFileName(definition.CodingProjectPath),
            payloads = payloads.Select(payload => new
            {
                aid = payload.Aid,
                fileId = payload.FileId,
                output = payload.Output,
                size = payload.Bytes.Length,
                expectedHex = Convert.ToHexString(payload.Bytes),
                payloadPath = Path.Combine(definition.ProjectDirectory, "payloads", payload.Output),
                readbackPath = Path.Combine(definition.ProjectDirectory, "readback", payload.Output),
                verificationPath = Path.Combine(definition.ProjectDirectory, definition.VerificationDirectory, payload.Output)
            }).ToArray()
        };
        var path = Path.Combine(definition.ProjectDirectory, "Letzte_Kodierung.json");
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }), new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void WriteExpectedCard(CodingSessionDefinition definition, IReadOnlyDictionary<string, string> cardValues,
        IReadOnlyList<PayloadArtifact> payloads, DateTimeOffset preparedAt)
    {
        var values = new Dictionary<string, string>(definition.ProjectValues, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in cardValues) values[pair.Key] = pair.Value;
        var document = new
        {
            schemaVersion = "1.0",
            projectName = definition.ProjectName,
            preparedAt = preparedAt.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
            source = "Kodierstation",
            containsKeys = false,
            containsCardData = true,
            values,
            payloads = payloads.Select(payload => new
            {
                output = payload.Output.Replace('\\', '/'),
                length = payload.Bytes.Length,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload.Bytes))
            }).ToArray()
        };
        var path = Path.Combine(definition.ProjectDirectory, "card_preflight", "expected-card.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }), new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateValue(VariableDefinition variable, string value)
    {
        if (value.Length == 0)
        {
            if (variable.Required) throw new InvalidDataException($"{variable.Label} ist erforderlich.");
            return;
        }
        if (string.Equals(variable.Type, "digits", StringComparison.OrdinalIgnoreCase) && value.Any(character => character is < '0' or > '9'))
            throw new InvalidDataException($"{variable.Label} darf nur Ziffern enthalten.");
        if (string.Equals(variable.Type, "hex", StringComparison.OrdinalIgnoreCase) && (value.Length % 2 != 0 || value.Any(character => !Uri.IsHexDigit(character))))
            throw new InvalidDataException($"{variable.Label} muss aus vollständigen HEX-Bytes bestehen (z. B. A0FF01).");
        if (variable.Length is int exact && value.Length != exact) throw new InvalidDataException($"{variable.Label} muss genau {exact} Stellen haben.");
        if (variable.AllowedLengths is { Count: > 0 } allowed && !allowed.Contains(value.Length))
            throw new InvalidDataException($"{variable.Label} muss {string.Join(" oder ", allowed)} Stellen haben.");
        if (variable.MinLength is int minimum && value.Length < minimum) throw new InvalidDataException($"{variable.Label} muss mindestens {minimum} Stellen haben.");
        if (variable.MaxLength is int maximum && value.Length > maximum) throw new InvalidDataException($"{variable.Label} darf höchstens {maximum} Stellen haben.");
    }

    private static string Hint(VariableDefinition variable)
    {
        var format = variable.Type switch { "hex" => "HEX · ", "text" => "Text · ", _ => "Ziffern · " };
        if (variable.Length is int exact) return $"{format}{exact} Zeichen";
        if (variable.AllowedLengths is { Count: > 0 } allowed) return format + string.Join(" / ", allowed) + " Zeichen";
        if (variable.MinLength is int minimum || variable.MaxLength is int maximum) return $"{format}{variable.MinLength?.ToString() ?? "1"}..{variable.MaxLength?.ToString() ?? "∞"} Zeichen";
        return variable.Required ? "Pflichtfeld" : "Optional";
    }

    private static string SafeChild(string root, string relative, string label)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException($"{label} muss relativ zum Projektordner sein: {relative}");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{label} verlässt den Projektordner: {relative}");
        return path;
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : throw new InvalidDataException($"JSON-Feld fehlt: {name}");
    private static IEnumerable<JsonElement> RequiredArray(JsonElement element, string name)
    {
        var value = RequiredProperty(element, name);
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"JSON-Feld ist kein Array: {name}");
        return value.EnumerateArray();
    }
    private static string RequiredString(JsonElement element, string name)
        => OptionalString(element, name) is { Length: > 0 } value ? value : throw new InvalidDataException($"JSON-Textfeld fehlt: {name}");
    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int RequiredInt(JsonElement element, string name)
        => OptionalInt(element, name) ?? throw new InvalidDataException($"JSON-Zahlenfeld fehlt: {name}");
    private static int? OptionalInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
    private static bool OptionalBoolean(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static List<int>? OptionalIntArray(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.GetInt32()).ToList() : null;
}

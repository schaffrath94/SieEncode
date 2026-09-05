using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record LegacyProjectMigrationResult(
    ProjectDefinition Project,
    string SourceKind,
    IReadOnlyList<string> ImportedValues,
    IReadOnlyList<string> Warnings,
    string SourcePath);

public sealed class LegacyProjectMigrationService
{
    private static readonly JsonSerializerOptions CamelOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LegacyProjectMigrationResult Import(string path, ProfileLibrary library)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(library);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Alte Projektdatei wurde nicht gefunden.", fullPath);
        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        return IsBuiltArtifact(document.RootElement)
            ? ImportBuiltArtifact(fullPath, document.RootElement, library)
            : ImportLegacyConfiguration(fullPath, document.RootElement, library);
    }

    private static LegacyProjectMigrationResult ImportBuiltArtifact(string path, JsonElement root, ProfileLibrary library)
    {
        var name = RequiredString(RequiredObject(root, "project"), "name");
        var profileRows = RequiredArray(root, "profiles").EnumerateArray().Select(row => new
        {
            Id = RequiredString(row, "id"),
            Kind = RequiredString(row, "kind"),
            Code = OptionalString(row, "code"),
            Version = OptionalString(row, "version")
        }).ToArray();
        var bases = profileRows.Where(row => row.Kind == "base").ToArray();
        if (bases.Length != 1) throw new InvalidDataException($"Das Bauartefakt muss genau ein Masterprofil nennen; gefunden: {bases.Length}.");
        ProfileDefinition currentBase;
        try { currentBase = library.Get(bases[0].Id); }
        catch (KeyNotFoundException) { throw new InvalidDataException($"Das gebaute Projekt verwendet Grundprofil '{bases[0].Id}', das in der aktuellen Profilbibliothek fehlt."); }
        if (!string.Equals(currentBase.Profile.Kind, "base", StringComparison.Ordinal))
            throw new InvalidDataException($"Profil '{bases[0].Id}' ist in der aktuellen Bibliothek kein Grundprofil.");
        if (!string.Equals(bases[0].Id, currentBase.Profile.Id, StringComparison.Ordinal))
            throw new InvalidDataException($"Das gebaute Projekt verwendet Masterprofil '{bases[0].Id}', aktuell ist '{currentBase.Profile.Id}' installiert. Die Migration wurde blockiert, weil ein stiller Wechsel das Kartenlayout verändern würde.");

        foreach (var row in profileRows)
        {
            ProfileDefinition installed;
            try { installed = library.Get(row.Id); }
            catch (KeyNotFoundException) { throw new InvalidDataException($"Profil '{row.Id}' aus dem Bauartefakt fehlt in der aktuellen Profilbibliothek."); }
            if (!string.Equals(installed.Profile.Kind, row.Kind, StringComparison.Ordinal)) throw new InvalidDataException($"Profilart von '{row.Id}' stimmt nicht mit dem Bauartefakt überein.");
            if (row.Code.Length > 0 && !string.Equals(installed.Profile.Code, row.Code, StringComparison.Ordinal)) throw new InvalidDataException($"Profilcode von '{row.Id}' weicht ab: Artefakt '{row.Code}', installiert '{installed.Profile.Code}'.");
            if (row.Version.Length > 0 && !string.Equals(installed.Profile.Version, row.Version, StringComparison.Ordinal)) throw new InvalidDataException($"Profilversion von '{row.Id}' weicht ab: Artefakt '{row.Version}', installiert '{installed.Profile.Version}'.");
        }

        var customerIds = profileRows.Where(row => row.Kind == "customer").Select(row => row.Id).ToList();
        var composition = library.Compose(bases[0].Id, customerIds);
        if (!TryGet(root, "applications", out var applicationNode) || applicationNode.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Das Bauartefakt enthält kein prüfbares Application-/Filelayout.");
        var artifactApplications = JsonSerializer.Deserialize<List<ApplicationDefinition>>(applicationNode.GetRawText(), CamelOptions)
            ?? throw new InvalidDataException("Das Application-/Filelayout des Bauartefakts ist leer.");
        if (!SemanticEquals(artifactApplications, composition.Applications))
            throw new InvalidDataException("Das im Bauartefakt dokumentierte Application-/Filelayout weicht von den aktuell installierten Profilen ab. Eine Bearbeitung würde die Kartenstruktur verändern und wurde blockiert.");

        var draft = CreateSafeDraft(library, name + "_migriert", bases[0].Id, customerIds);
        var imported = new List<string>();
        var warnings = new List<string>
        {
            "Das vorhandene Bauartefakt bleibt unverändert; die Definition ist ein neuer, noch ungespeicherter Entwurf.",
            "Produktivkeys sind aus Bauartefakten nicht sicher rücklesbar und wurden nicht übernommen.",
            "Der Buildumfang wurde sicherheitshalber auf Mastering begrenzt. Kodierung muss nach Prüfung und gezielter Vault-Bereitstellung aktiviert werden."
        };
        var codingSchemaPath = Path.Combine(Path.GetDirectoryName(path)!, "coding.schema.json");
        if (File.Exists(codingSchemaPath)) ImportProjectValuesFromCodingSchema(draft, composition, codingSchemaPath, imported, warnings);
        else if (composition.Variables.Any(variable => variable.Scope == "project")) warnings.Add("coding.schema.json fehlt; projektspezifische Werte konnten nicht aus dem Bauartefakt übernommen werden.");
        ProjectDraftFactory.Synchronize(draft, library);
        return new LegacyProjectMigrationResult(draft, "Gebautes Projekt", imported, warnings, path);
    }

    private static LegacyProjectMigrationResult ImportLegacyConfiguration(string path, JsonElement root, ProfileLibrary library)
    {
        if (TryGet(root, "Profiles", out var modernProfiles) && modernProfiles.ValueKind == JsonValueKind.Object)
            throw new InvalidDataException("Diese Datei besitzt bereits das aktuelle Projektformat und sollte direkt geöffnet statt migriert werden.");
        var baseProfile = library.Profiles.Where(profile => profile.Profile.Kind == "base")
            .OrderByDescending(profile => string.Equals(profile.Profile.Id, "Grundprofil-9.1", StringComparison.Ordinal))
            .ThenBy(profile => profile.Profile.Id, StringComparer.Ordinal)
            .FirstOrDefault() ?? throw new InvalidDataException("Für die Altprojektmigration ist kein Grundprofil installiert.");
        var customers = new List<string>();
        var customer = OptionalString(root, "Customer");
        if (customer.Length > 0)
        {
            ProfileDefinition selected;
            try { selected = library.Get(customer); }
            catch (KeyNotFoundException) { throw new InvalidDataException($"Das alte Projekt verwendet Kundenprofil '{customer}'. Dieses Profil fehlt in der aktuellen Bibliothek."); }
            if (selected.Profile.Kind != "customer") throw new InvalidDataException($"Profil '{customer}' ist kein Kundenprofil.");
            customers.Add(customer);
        }
        var legacyName = OptionalString(root, "ProjectName");
        if (TryGet(root, "Project", out var projectNode) && projectNode.ValueKind == JsonValueKind.Object)
            legacyName = OptionalString(projectNode, "Name") is { Length: > 0 } nestedName ? nestedName : legacyName;
        if (legacyName.Length == 0) legacyName = Path.GetFileName(Path.GetDirectoryName(path)) ?? "Grundprofil";
        var draft = CreateSafeDraft(library, legacyName + "_migriert", baseProfile.Profile.Id, customers);
        var composition = library.Compose(draft.Profiles.Base, draft.Profiles.Customers);
        var imported = new List<string>();
        foreach (var variable in composition.Variables.Where(variable => variable.Scope == "project"))
        {
            var match = root.EnumerateObject().FirstOrDefault(property =>
                property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
                (string.Equals(property.Name, variable.Id, StringComparison.OrdinalIgnoreCase) || property.Name.EndsWith(variable.Id, StringComparison.OrdinalIgnoreCase)));
            if (match.Name is null) continue;
            var value = match.Value.ValueKind == JsonValueKind.String ? match.Value.GetString() ?? "" : match.Value.GetRawText();
            if (ProfileValidator.ValidateVariableValue(variable, value).Count > 0) throw new InvalidDataException($"Altwert für '{variable.Label}' entspricht nicht dem aktuellen Profilvertrag.");
            draft.Values[variable.Id] = value;
            imported.Add(variable.Label);
        }
        ProjectDraftFactory.Synchronize(draft, library);
        return new LegacyProjectMigrationResult(draft, "Alte JSON-Konfiguration", imported,
        [
            "Die alte Datei bleibt unverändert; die Definition ist ein neuer, noch ungespeicherter Entwurf.",
            "Schlüsselwerte aus Altformaten werden aus Sicherheitsgründen grundsätzlich nicht übernommen.",
            "Der Buildumfang wurde auf Mastering begrenzt. Profilwahl, Fachwerte und Ausgabeziel müssen vor dem Speichern bewusst geprüft werden."
        ], path);
    }

    private static ProjectDefinition CreateSafeDraft(ProfileLibrary library, string name, string baseId, List<string> customerIds)
    {
        var draft = ProjectDraftFactory.Create(library, SafeName(name));
        draft.Project.OutputDirectory = ".";
        draft.Project.Build = ["Mastering"];
        draft.Profiles.Base = baseId;
        draft.Profiles.Customers = customerIds;
        draft.Productive.PiccKey.Value = "";
        draft.Productive.ApplicationKeys.Clear();
        return ProjectDraftFactory.Synchronize(draft, library);
    }

    private static void ImportProjectValuesFromCodingSchema(ProjectDefinition draft, ComposedProfiles composition, string path, List<string> imported, List<string> warnings)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(path));
        if (!TryGet(schema.RootElement, "variables", out var variables) || variables.ValueKind != JsonValueKind.Array) { warnings.Add("coding.schema.json enthält keine lesbare Variablenliste."); return; }
        var currentVariables = composition.Variables.ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        foreach (var row in variables.EnumerateArray())
        {
            var id = OptionalString(row, "id");
            if (id.Length == 0 || !currentVariables.TryGetValue(id, out var definition) || definition.Scope != "project" || !TryGet(row, "value", out var valueNode) || valueNode.ValueKind != JsonValueKind.String) continue;
            var value = valueNode.GetString() ?? "";
            if (ProfileValidator.ValidateVariableValue(definition, value).Count > 0) throw new InvalidDataException($"Projektwert '{id}' aus coding.schema.json entspricht nicht dem aktuellen Profilvertrag.");
            draft.Values[id] = value;
            imported.Add(definition.Label);
        }
    }

    private static bool SemanticEquals(object left, object right)
        => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(left, CamelOptions), JsonSerializer.SerializeToNode(right, CamelOptions));

    private static bool IsBuiltArtifact(JsonElement root)
        => TryGet(root, "project", out var project) && project.ValueKind == JsonValueKind.Object && TryGet(root, "profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array;

    private static string SafeName(string value)
    {
        var result = string.Concat(value.Trim().Select(character => Path.GetInvalidFileNameChars().Contains(character) || char.IsWhiteSpace(character) ? '_' : character));
        while (result.Contains("__", StringComparison.Ordinal)) result = result.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(result) ? "Projekt_migriert" : result;
    }

    private static JsonElement RequiredObject(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.Object ? value : throw new InvalidDataException($"JSON-Objekt fehlt: {name}");
    private static JsonElement RequiredArray(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.Array ? value : throw new InvalidDataException($"JSON-Array fehlt: {name}");
    private static string RequiredString(JsonElement element, string name)
        => OptionalString(element, name) is { Length: > 0 } value ? value : throw new InvalidDataException($"JSON-Feld fehlt: {name}");
    private static string OptionalString(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }
}

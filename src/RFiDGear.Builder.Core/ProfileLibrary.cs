using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class ProfileLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Dictionary<string, ProfileDefinition> profiles;

    private ProfileLibrary(Dictionary<string, ProfileDefinition> profiles) => this.profiles = profiles;

    public IReadOnlyCollection<ProfileDefinition> Profiles => profiles.Values;

    public static ProfileLibrary FromProfiles(IEnumerable<ProfileDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var profiles = new Dictionary<string, ProfileDefinition>(StringComparer.Ordinal);
        foreach (var profile in definitions)
        {
            if (string.IsNullOrWhiteSpace(profile.Profile.Id)) throw new InvalidDataException("Profil-ID fehlt.");
            if (!profiles.TryAdd(profile.Profile.Id, profile)) throw new InvalidDataException($"Profil-ID ist mehrfach vorhanden: {profile.Profile.Id}");
        }
        return new ProfileLibrary(profiles);
    }

    public static ProfileLibrary Load(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory)) return FromProfiles([]);
        var profiles = new Dictionary<string, ProfileDefinition>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(fullDirectory, "*.json", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            var profile = JsonSerializer.Deserialize<ProfileDefinition>(stream, JsonOptions)
                ?? throw new InvalidDataException($"Profil ist leer: {path}");
            if (string.IsNullOrWhiteSpace(profile.Profile.Id)) throw new InvalidDataException($"Profil-ID fehlt: {path}");
            if (!profiles.TryAdd(profile.Profile.Id, profile)) throw new InvalidDataException($"Profil-ID ist mehrfach vorhanden: {profile.Profile.Id}");
        }
        return FromProfiles(profiles.Values);
    }

    public ProfileDefinition Get(string id)
        => profiles.TryGetValue(id, out var profile) ? profile : throw new KeyNotFoundException($"Profil '{id}' wurde nicht gefunden.");

    public ComposedProfiles Compose(string baseId, IEnumerable<string>? customerIds)
    {
        var selected = new List<ProfileDefinition>();
        var baseProfile = Get(baseId);
        if (!string.Equals(baseProfile.Profile.Kind, "base", StringComparison.Ordinal))
            throw new InvalidDataException($"Profil '{baseId}' ist kein Master-/Basisprofil.");
        selected.Add(baseProfile);
        foreach (var customerId in customerIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(customerId)) continue;
            var customer = Get(customerId);
            if (!string.Equals(customer.Profile.Kind, "customer", StringComparison.Ordinal))
                throw new InvalidDataException($"Profil '{customerId}' ist kein Kundenprofil und darf nicht additiv gewählt werden.");
            selected.Add(customer);
        }

        var applications = new List<ApplicationDefinition>();
        var aids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var variables = new List<VariableDefinition>();
        var variableJson = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var profile in selected)
        {
            foreach (var application in profile.Applications)
            {
                var aid = ProfileValidator.NormalizeAid(application.Id);
                if (!aids.Add(aid)) throw new InvalidDataException($"Profilkonflikt: AID {aid} wird von mehreren Profilen definiert.");
                applications.Add(application);
            }
            foreach (var variable in profile.Variables)
            {
                var json = JsonSerializer.Serialize(variable, JsonOptions);
                if (variableJson.TryGetValue(variable.Id, out var existing))
                {
                    if (!string.Equals(existing, json, StringComparison.Ordinal))
                        throw new InvalidDataException($"Profilkonflikt: Variable '{variable.Id}' ist unterschiedlich definiert.");
                }
                else
                {
                    variableJson.Add(variable.Id, json);
                    variables.Add(variable);
                }
            }
        }
        return new ComposedProfiles(selected, applications, variables);
    }

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        var baseCount = profiles.Values.Count(profile => profile.Profile.Kind == "base");
        if (baseCount < 1) issues.Add(new("BASE_PROFILE_COUNT", "Profilbibliothek muss mindestens ein Grundprofil enthalten."));
        foreach (var profile in profiles.Values) issues.AddRange(ProfileValidator.Validate(profile));
        return issues;
    }
}

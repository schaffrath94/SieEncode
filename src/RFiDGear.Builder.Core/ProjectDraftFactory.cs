namespace RFiDGear.Builder.Core;

public static class ProjectDraftFactory
{
    public static ProjectDefinition Create(ProfileLibrary library, string name = "Neues Projekt")
    {
        ArgumentNullException.ThrowIfNull(library);
        var baseProfile = library.Profiles.Where(profile => string.Equals(profile.Profile.Kind, "base", StringComparison.Ordinal))
            .OrderByDescending(profile => string.Equals(profile.Profile.Id, "Grundprofil-9.1", StringComparison.Ordinal))
            .ThenBy(profile => profile.Profile.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.Profile.Id, StringComparer.Ordinal)
            .FirstOrDefault() ?? throw new InvalidDataException("Die Profilbibliothek enthält kein Grundprofil.");
        var project = new ProjectDefinition
        {
            Project = new ProjectSection { Id = Guid.NewGuid(), Name = name, OutputDirectory = ".", Build = ["Mastering", "Coding"], CardMode = CardOperationMode.FullCard },
            Profiles = new ProfileSelection { Base = baseProfile.Profile.Id },
            Transport = new TransportConfiguration
            {
                KeySetId = "standard",
                PiccKey = new KeyReference { Type = "DF_KEY_DES", Value = new string('0', 32) },
                ApplicationKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DF_KEY_DES"] = new string('0', 32),
                    ["DF_KEY_AES"] = new string('0', 32),
                    ["DF_KEY_3K3DES"] = new string('0', 48)
                }
            },
            Productive = new ProductiveConfiguration
            {
                PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = "" },
                PiccKeySettings = string.IsNullOrWhiteSpace(baseProfile.Picc?.ProductiveKeySettings) ? "0F" : baseProfile.Picc!.ProductiveKeySettings
            }
        };
        Synchronize(project, library);
        ProfileBindingService.BindCurrent(project, library);
        return project;
    }

    public static ProjectDefinition Synchronize(ProjectDefinition project, ProfileLibrary library)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(library);
        var composition = library.Compose(project.Profiles.Base, project.Profiles.Customers);
        ProfileBindingService.PruneToSelection(project);

        foreach (var type in composition.Applications.Select(application => application.KeyType).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!project.Transport.ApplicationKeys.ContainsKey(type))
                project.Transport.ApplicationKeys[type] = project.Project.KeyLibrary is not null
                    ? "" // A catalog-bound draft must never silently acquire a raw fallback key.
                    : type == "DF_KEY_3K3DES" ? new string('0', 48) : new string('0', 32);
        }

        var productive = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var application in composition.Applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            if (project.Productive.ApplicationKeys.TryGetValue(aid, out var existing) && existing.Count == application.KeyCount)
                productive[aid] = existing.ToList();
            else
                productive[aid] = Enumerable.Repeat("", application.KeyCount).ToList();
        }
        project.Productive.ApplicationKeys = productive;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in composition.Variables.Where(variable => string.Equals(variable.Scope, "project", StringComparison.Ordinal)))
            values[variable.Id] = project.Values.GetValueOrDefault(variable.Id, variable.Default ?? "");
        project.Values = values;
        return project;
    }
}

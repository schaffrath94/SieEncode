namespace RFiDGear.Builder.Core;

public static class CardOperationMode
{
    public const string FullCard = "FullCard";
    public const string ExtendExisting = "ExtendExisting";

    public static bool IsExtension(ProjectDefinition project)
        => string.Equals(project.Project.CardMode, ExtendExisting, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<ApplicationDefinition> SelectApplications(
        ComposedProfiles profiles, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(project);
        if (!IsExtension(project)) return profiles.Applications;

        var baseProfile = profiles.Profiles.SingleOrDefault(profile =>
            string.Equals(profile.Profile.Kind, "base", StringComparison.OrdinalIgnoreCase));
        if (baseProfile is null)
            throw new InvalidDataException("Der Erweiterungsmodus benötigt genau ein Grundprofil.");

        var baseAids = baseProfile.Applications
            .Select(application => ProfileValidator.NormalizeAid(application.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = profiles.Applications
            .Where(application => !baseAids.Contains(ProfileValidator.NormalizeAid(application.Id)))
            .ToArray();
        if (additions.Length == 0)
            throw new InvalidDataException("Der Erweiterungsmodus benötigt mindestens eine neue Application aus einem Zusatzprofil.");
        return additions;
    }

    public static IReadOnlyList<ValidationIssue> Validate(ComposedProfiles profiles, ProjectDefinition project)
    {
        if (!IsExtension(project)) return [];
        var issues = new List<ValidationIssue>();
        if (project.Profiles.Customers.Count == 0)
            issues.Add(new("EXTENSION_PROFILE", "Für eine Kartenerweiterung muss mindestens ein Zusatzprofil gewählt werden."));
        try { _ = SelectApplications(profiles, project); }
        catch (InvalidDataException exception) { issues.Add(new("EXTENSION_SCOPE", exception.Message)); }
        return issues;
    }

    public static IReadOnlyList<VariableDefinition> SelectVariables(ComposedProfiles profiles, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(project);
        if (!IsExtension(project)) return profiles.Variables;
        var requiredIds = SelectApplications(profiles, project)
            .SelectMany(application => application.Files)
            .Where(file => file.Coding is not null)
            .SelectMany(file => file.Coding!.Recipe)
            .Select(step => step.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .ToHashSet(StringComparer.Ordinal);
        return profiles.Variables.Where(variable => requiredIds.Contains(variable.Id)).ToArray();
    }
}

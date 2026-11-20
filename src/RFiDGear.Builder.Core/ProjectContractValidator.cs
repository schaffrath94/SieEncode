using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public static partial class ProjectContractValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(
        ProjectDefinition project,
        ProfileLibrary profileLibrary,
        bool requireProductiveSecrets = false,
        bool requireTransportSecrets = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profileLibrary);
        var issues = new List<ValidationIssue>();
        issues.AddRange(ProjectValidator.Validate(project, requireProductiveSecrets: false, requireTransportSecrets: requireTransportSecrets));
        issues.AddRange(profileLibrary.Validate());

        ComposedProfiles composed;
        try { composed = profileLibrary.Compose(project.Profiles.Base, project.Profiles.Customers); }
        catch (Exception exception) when (exception is InvalidDataException or KeyNotFoundException)
        {
            issues.Add(new("PROFILE_COMPOSITION", exception.Message));
            return issues;
        }
        issues.AddRange(CardOperationMode.Validate(composed, project));

        IReadOnlyList<ApplicationDefinition> operationApplications;
        try { operationApplications = CardOperationMode.SelectApplications(composed, project); }
        catch (InvalidDataException) { operationApplications = []; }

        var needsCoding = project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        foreach (var keyType in operationApplications.Select(application => application.KeyType).Distinct(StringComparer.Ordinal))
        {
            project.Transport.ApplicationKeys.TryGetValue(keyType, out var value);
            if ((requireTransportSecrets || !string.IsNullOrWhiteSpace(value)) && !IsValidKey(value, keyType))
                issues.Add(new("TRANSPORT_APP_KEY", $"Transport Application Key für {keyType} fehlt oder ist ungültig."));
        }

        if (requireProductiveSecrets && needsCoding)
        {
            if (!CardOperationMode.IsExtension(project) && !IsValidKey(project.Productive.PiccKey.Value, project.Productive.PiccKey.Type))
                issues.Add(new("PRODUCTIVE_PICC", "Produktiver PICC Key fehlt oder ist ungültig."));
            foreach (var application in operationApplications)
            {
                var aid = ProfileValidator.NormalizeAid(application.Id);
                if (!project.Productive.ApplicationKeys.TryGetValue(aid, out var keys))
                {
                    issues.Add(new("PRODUCTIVE_APP_KEYS", $"Produktivschlüssel für AID {aid} fehlen."));
                    continue;
                }
                if (keys.Count != application.KeyCount)
                {
                    issues.Add(new("PRODUCTIVE_APP_KEY_COUNT", $"AID {aid} benötigt {application.KeyCount} Produktivschlüssel, konfiguriert sind {keys.Count}."));
                    continue;
                }
                for (var index = 0; index < keys.Count; index++)
                    if (!IsValidKey(keys[index], application.KeyType)) issues.Add(new("PRODUCTIVE_APP_KEY", $"AID {aid} Key{index} ist für {application.KeyType} ungültig."));
            }
        }

        if (needsCoding)
        {
            foreach (var variable in CardOperationMode.SelectVariables(composed, project).Where(variable => variable.Scope == "project"))
            {
                project.Values.TryGetValue(variable.Id, out var value);
                foreach (var issue in ProfileValidator.ValidateVariableValue(variable, value))
                    issues.Add(issue with { Code = "PROJECT_" + issue.Code });
            }
        }
        return issues;
    }

    private static bool IsValidKey(string? value, string keyType)
    {
        var expectedLength = keyType switch { "DF_KEY_DES" or "DF_KEY_AES" => 32, "DF_KEY_3K3DES" => 48, _ => -1 };
        return expectedLength > 0 && value is not null && value.Length == expectedLength && HexRegex().IsMatch(value);
    }

    [GeneratedRegex("^[0-9A-Fa-f]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HexRegex();
}

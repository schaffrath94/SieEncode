using System.Text.RegularExpressions;

namespace RFiDGear.Builder.RfPrj;

public sealed record RfPrjSecretProtectionResult(
    RfPrjDocument Project,
    IReadOnlyDictionary<string, string> Secrets);

public static class RfPrjSecretProtection
{
    public const string ReferencePrefix = "secret://rfidgear/v1/";

    private static readonly HashSet<string> SecretElements = new(StringComparer.Ordinal)
    {
        "DesfireMasterKeyCurrent", "DesfireMasterKeyTarget", "DesfireAppKeyCurrent",
        "DesfireAppKeyCurrentOld", "DesfireAppKeyTarget", "DesfireReadKeyCurrent", "DesfireWriteKeyCurrent",
        "ClassicKeyAKeyCurrent", "ClassicKeyBKeyCurrent", "ClassicKeyAKeyTarget", "ClassicKeyBKeyTarget",
        "ClassicMADKeyAKeyCurrent", "ClassicMADKeyBKeyCurrent", "ClassicMADKeyAKeyTarget", "ClassicMADKeyBKeyTarget"
    };

    public static RfPrjSecretProtectionResult Protect(RfPrjDocument project, string projectArtifactName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectArtifactName);
        var artifact = SafeSegment(Path.GetFileNameWithoutExtension(projectArtifactName));
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var task in project.Tasks.Select((element, position) => (element, position)))
        {
            var taskIndex = SafeSegment(TaskXml.Get(task.element, "CurrentTaskIndex"));
            if (string.IsNullOrWhiteSpace(taskIndex)) taskIndex = task.position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var element in task.element.Elements().Where(item => SecretElements.Contains(item.Name.LocalName)))
            {
                var value = element.Value;
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith(ReferencePrefix, StringComparison.Ordinal)) continue;
                var reference = $"{ReferencePrefix}artifact/{artifact}/task/{taskIndex}/{element.Name.LocalName}";
                if (!secrets.TryAdd(reference, value))
                    throw new InvalidDataException($"Doppelte Schlüsselreferenz im RFiDGear-Projekt: {reference}");
                element.Value = reference;
            }
        }

        return new(project, secrets);
    }

    public static IReadOnlyList<string> FindPlaintextSecretElements(RfPrjDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Tasks.SelectMany((task, position) => task.Elements()
                .Where(element => SecretElements.Contains(element.Name.LocalName) &&
                                  !string.IsNullOrWhiteSpace(element.Value) &&
                                  !element.Value.StartsWith(ReferencePrefix, StringComparison.Ordinal))
                .Select(element => $"Task {position}: {element.Name.LocalName}"))
            .ToArray();
    }

    private static string SafeSegment(string value)
    {
        var safe = Regex.Replace(value ?? "", "[^A-Za-z0-9._~-]", "-").Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "unnamed" : safe[..Math.Min(safe.Length, 80)];
    }
}

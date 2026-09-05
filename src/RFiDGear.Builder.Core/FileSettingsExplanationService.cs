namespace RFiDGear.Builder.Core;

public sealed record FileSettingsExplanation(
    string InitialTechnical,
    string FinalTechnical,
    string InitialMeaning,
    string FinalMeaning,
    string Transition,
    string MasteringSequence,
    bool IsTwoStage,
    bool IsRedundant);

public static class FileSettingsExplanationService
{
    public static bool UsesZeroFill(FileDefinition file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Type is "StdDataFile" or "BackupFile" && file.MasteringInitialization?.Type != "none";
    }

    public static void SetZeroFill(FileDefinition file, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(file);
        file.MasteringInitialization = enabled ? null : new MasteringInitialization { Type = "none" };
    }

    public static FileSettingsExplanation Describe(FileDefinition file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var final = new FileSettingsDefinition { Communication = file.Communication, Access = file.Access };
        var initial = file.CreationSettings ?? final;
        var twoStage = file.CreationSettings is not null;
        var changes = Differences(initial, final);
        var redundant = twoStage && changes.Count == 0;
        var initialization = UsesZeroFill(file) ? "zeroFill" : "none";
        var sequence = twoStage
            ? $"CreateFile (initial) → {(initialization == "zeroFill" ? "vollständiger Zero-Fill + Readback (initial) → " : "")}ChangeFileSettings mit initialem Change-Key {initial.Access.Change} → finaler Zustand"
            : $"CreateFile direkt im finalen Zustand{(initialization == "zeroFill" ? " → vollständiger Zero-Fill + Readback" : "")}; kein ChangeFileSettings-Task";
        var transition = !twoStage
            ? "Kein Übergang: initial und final sind derselbe Zustand."
            : redundant
                ? "Achtung: Initial- und Endzustand sind identisch. Der zusätzliche ChangeFileSettings-Task wäre fachlich redundant."
                : "Nach der Initialisierung ändert ChangeFileSettings: " + string.Join("; ", changes) + ".";
        return new FileSettingsExplanation(
            Technical(initial), Technical(final), Meaning(initial), Meaning(final), transition, sequence, twoStage, redundant);
    }

    private static List<string> Differences(FileSettingsDefinition initial, FileSettingsDefinition final)
    {
        var result = new List<string>();
        if (initial.Communication != final.Communication) result.Add($"Communication {initial.Communication} → {final.Communication}");
        AddRight(result, "Lesen", initial.Access.Read, final.Access.Read);
        AddRight(result, "Schreiben", initial.Access.Write, final.Access.Write);
        AddRight(result, "Lesen/Schreiben", initial.Access.ReadWrite, final.Access.ReadWrite);
        AddRight(result, "Einstellungen ändern", initial.Access.Change, final.Access.Change);
        return result;
    }

    private static void AddRight(List<string> result, string label, int initial, int final)
    {
        if (initial != final) result.Add($"{label} Key {initial} → Key {final}");
    }

    private static string Technical(FileSettingsDefinition settings)
        => $"{settings.Communication} · R{settings.Access.Read} / W{settings.Access.Write} / RW{settings.Access.ReadWrite} / C{settings.Access.Change}";

    private static string Meaning(FileSettingsDefinition settings)
        => $"{Communication(settings.Communication)} Lesen mit Key {settings.Access.Read}; Schreiben mit Key {settings.Access.Write}; kombinierter Zugriff mit Key {settings.Access.ReadWrite}; Settings-Änderung mit Key {settings.Access.Change}.";

    private static string Communication(string value) => value switch
    {
        "CM_PLAIN" => "Nutzdaten ohne verschlüsseltes Secure Messaging.",
        "CM_MAC" => "Nutzdaten mit kryptografischem Integritätsschutz.",
        "CM_ENCRYPT" => "Nutzdaten im verschlüsselten Secure-Messaging-Kanal.",
        _ => "Unbekannter Communication Mode."
    };
}

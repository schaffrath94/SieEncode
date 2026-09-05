namespace RFiDGear.Builder.RfPrj;

public static class ApplicationAcceptanceCompatibilityService
{
    public static int RemoveUnsupportedFileSettingsTasks(RfPrjDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "ReadAppSettings", "CheckAppKeyCount", "CheckFileSettings" };
        var unexpected = document.Tasks.FirstOrDefault(task => !allowed.Contains(TaskXml.Get(task, "SelectedTaskType")));
        if (unexpected is not null)
            throw new InvalidDataException("Abnahmeprojekt enthält einen unerwarteten Tasktyp: " + TaskXml.Get(unexpected, "SelectedTaskType"));

        var removable = document.Tasks.Where(task => TaskXml.Get(task, "SelectedTaskType") == "CheckFileSettings").ToArray();
        foreach (var task in removable) task.Remove();
        var remaining = document.Summarize();
        if (remaining.Count == 0 || remaining.Count % 2 != 0 ||
            remaining.Chunk(2).Any(pair => pair.Length != 2 || pair[0].Type != "ReadAppSettings" || pair[1].Type != "CheckAppKeyCount" ||
                !string.Equals(pair[0].Aid, pair[1].Aid, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Abnahmeprojekt enthält nach der Kompatibilisierung keine vollständigen ReadAppSettings-/CheckAppKeyCount-Paare.");
        return removable.Length;
    }
}

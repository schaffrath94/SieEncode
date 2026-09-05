using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearKeyChangeSafetyResult(bool Allowed, bool RequiresKeyChange, string Message);

public static class RFiDGearKeyChangeSafety
{
    private static readonly HashSet<string> KeyChangingTasks = new(StringComparer.Ordinal)
    {
        "ApplicationKeyChangeover",
        "ApplicationKeySettingsChangeover",
        "PICCMasterKeyChangeover",
        "PICCMasterKeySettingsChangeover"
    };

    public static RFiDGearKeyChangeSafetyResult Evaluate(
        RFiDGearCapabilityReport capabilities,
        IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(tasks);
        if (!RequiresVerifiedKeyChange(tasks))
            return new(true, false, "Das Projekt enthält keinen Keywechsel.");

        var capability = capabilities.Capabilities.FirstOrDefault(item => item.Id == RFiDGearCapabilityId.VerifiedApplicationKeyChange);
        if (capability?.State == RFiDGearCapabilityState.Available)
            return new(true, true, "RFiDGear weist den vollständigen autoritativen Keywechsel-Vertrag nach und der exakte Build ist im hardwaregeprüften Builder-Vertrauenskatalog freigegeben (frischer Provider-Kontext und Zielschlüssel-Authentifizierung auch nach mehrdeutigem ChangeKey-Ergebnis).");

        var version = capabilities.Version.ProductVersion ?? "unbekannt";
        return new(false, true,
            $"Kodierung gesperrt: RFiDGear {version} weist keine verifizierte und für diesen exakten Build hardwarefreigegebene DESFire-Keywechsel-Implementierung nach. " +
            "Der Hardwaretest mit 2.0.10 meldete ApplicationKeyChangeover fälschlich als erfolgreich, obwohl der Transport-Key aktiv blieb; teilweise PR-205-Builds waren ebenfalls noch nicht ausreichend (c3rebro/RFiDGear#178). " +
            "Implementierungsmarker oder eine hohe Versionsnummer allein reichen nicht aus. Mastering, reine Leseprüfungen und Format-Recovery bleiben davon unberührt.");
    }

    public static bool RequiresVerifiedKeyChange(IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return tasks.Any(task => KeyChangingTasks.Contains(task.Type));
    }

    public static void RequireAllowed(RFiDGearCapabilityReport capabilities, IReadOnlyList<RfPrjTaskSummary> tasks)
    {
        var result = Evaluate(capabilities, tasks);
        if (!result.Allowed) throw new InvalidOperationException(result.Message);
    }
}

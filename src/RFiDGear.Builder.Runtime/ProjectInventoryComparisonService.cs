using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

/// <summary>Builds complete expected bytes using the production payload engine, with separate project/card values.</summary>
public sealed class ProjectInventoryComparisonService
{
    public CardReport CreateReport(ComposedProfiles profiles, IReadOnlyDictionary<string, string> projectValues,
        IReadOnlyDictionary<string, string> cardValues, JsonElement inventory, string source)
    {
        var observed = new InventoryCardReportFactory().Create(inventory, source);
        var aid = observed.Applications.Single().Aid;
        var applications = profiles.Applications.Where(app => string.Equals(ProfileValidator.NormalizeAid(app.Id), aid, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (applications.Length != 1) throw new InvalidDataException("Inventur-AID muss im Sollprofil genau einmal vorhanden sein.");
        var application = applications[0];
        if (application.Files.GroupBy(file => file.Id).Any(group => group.Count() > 1))
            throw new InvalidDataException("Sollprofil enthält doppelte Datei-IDs.");
        var comparisons = new List<InventoryPayloadComparison>();
        var expectedHex = new Dictionary<int, string>();
        foreach (var file in application.Files)
        {
            if (file.Coding is null)
            {
                comparisons.Add(new(file.Id, PayloadComparisonState.NotComparable, "Kein Soll-Kodierrezept definiert."));
                continue;
            }
            byte[] bytes;
            try
            {
                var variables = file.Coding.Recipe.Where(step => step.Source is not null).Select(step => step.Source!).ToHashSet(StringComparer.Ordinal);
                var singleApplication = new ApplicationDefinition { Id = application.Id, Files = [file] };
                var scoped = new ComposedProfiles(profiles.Profiles, [singleApplication], profiles.Variables.Where(variable => variables.Contains(variable.Id)).ToArray());
                bytes = PayloadEngine.BuildAll(scoped, projectValues, cardValues).Single().Bytes;
            }
            catch (InvalidDataException)
            {
                comparisons.Add(new(file.Id, PayloadComparisonState.NotComparable, "Sollinhalt nicht erzeugbar: Rezept oder erforderliche Projekt-/Kartenwerte prüfen."));
                continue;
            }
            expectedHex[file.Id] = Convert.ToHexString(bytes);
            comparisons.AddRange(new InventoryPayloadComparisonService().Compare(aid, new Dictionary<int, byte[]> { [file.Id] = bytes }, inventory));
        }
        var findings = observed.Findings.ToList();
        findings.Add(new("INFO", "Vergleichsumfang", "Nur Dateiinhalte dieser Application; keine Prüfung von Rechten, Schlüsseln oder anderen Applications. Sollwerte stammen aus dem gewählten Profil und den übergebenen Projekt-/Kartenwerten."));
        foreach (var comparison in comparisons)
            findings.Add(new(comparison.State == PayloadComparisonState.Different ? "ERROR" : comparison.State == PayloadComparisonState.Equal ? "INFO" : "WARN",
                $"Inhaltsvergleich / File {comparison.FileId:D2}", comparison.Reason));
        foreach (var extra in observed.Applications[0].Files.Where(file => !application.Files.Any(expected => expected.Id == file.Id)))
            findings.Add(new("WARN", $"Inhaltsvergleich / File {extra.Id:D2}", "Kein Sollfile definiert; Inhalt nicht verglichen."));
        var reportFiles = observed.Applications[0].Files.ToList();
        foreach (var missing in application.Files.Where(file => !reportFiles.Any(actual => actual.Id == file.Id)))
            reportFiles.Add(new(missing.Id, missing.Name, "nicht gelesen", null, "nicht gelesen", "nicht gelesen",
                "nicht zutreffend", "nicht geprüft", "Kein Ist-Dateinachweis vorhanden.", "nicht gelesen", "nicht gelesen"));
        reportFiles = reportFiles.Select(file => file with
        {
            ExpectedHexDump = expectedHex.GetValueOrDefault(file.Id),
            Comparison = comparisons.FirstOrDefault(comparison => comparison.FileId == file.Id) is { } comparison
                ? (comparison.State switch { PayloadComparisonState.Equal => "Gleich", PayloadComparisonState.Different => "Abweichung", _ => "Nicht vergleichbar" }) + ": " + comparison.Reason
                : "Nicht vergleichbar: kein Sollfile definiert."
        }).ToList();
        return observed with
        {
            SchemaVersion = "1.2",
            Kind = "application-content-comparison",
            Title = "SieEncode Inhaltsvergleich",
            Applications = [observed.Applications[0] with { Files = reportFiles }],
            General = observed.General.Select(field => field.Name == "Berichtsart" ? field with { Value = "Soll/Ist - Dateiinhalte einer AID" } : field).ToArray(),
            Findings = findings
        };
    }
}

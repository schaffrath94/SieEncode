using System.Globalization;
using System.Text.Json;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

/// <summary>Allowlisted report fields from a single application inventory, never a whole-card completeness claim.</summary>
public sealed class InventoryCardReportFactory
{
    public CardReport Create(JsonElement top, string source)
    {
        if (!top.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Inventur ist nicht als nur lesend ausgewiesen.");
        var aid = Text(top, "applicationId");
        var findings = new List<ProjectAnalysisIssue>();
        var files = new List<CardReportFile>();
        if (top.TryGetProperty("files", out var array) && array.ValueKind == JsonValueKind.Array)
            foreach (var file in array.EnumerateArray())
            {
                var evidence = FileInventoryEvidence.FromRuntime(file);
                var id = Number(file, "fileId") ?? throw new InvalidDataException("Datei-ID fehlt im Inventurnachweis.");
                var rights = "nicht gelesen";
                if (file.TryGetProperty("access", out var access) && access.ValueKind == JsonValueKind.Object)
                    rights = $"R {Right(access, "read")} / W {Right(access, "write")} / RW {Right(access, "readWrite")} / C {Right(access, "change")}";
                files.Add(new(id, "", Text(file, "fileType"), evidence.DeclaredSize, Text(file, "communicationMode"), rights,
                    "nicht zutreffend", "nicht geprüft", evidence.Description + " | Runtime: " + evidence.RawResult +
                    " | Metadaten: " + Text(file, "metadataResult"), Text(file, "hex"), Text(file, "ascii")));
                if (evidence.State != InventoryReadState.Complete)
                    findings.Add(new("WARN", $"AID {aid} / File {id:D2}", evidence.Description));
            }
        var count = Number(top, "fileCount");
        var complete = Text(top, "listing") == "NoError" && count == files.Count;
        findings.Insert(0, new(complete ? "INFO" : "WARN", "Umfang",
            complete ? "Dateiliste dieser Application vollständig inventarisiert; Inhalte werden separat bewertet."
            : "Dateiliste nicht vollständig nachgewiesen. Fehlende Dateien dürfen nicht als nicht vorhanden gelten."));
        return new("1.1", "application-inventory", "SieEncode Istreport", "Application " + aid,
            DateTimeOffset.TryParse(Text(top, "createdUtc"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var created) ? created : DateTimeOffset.Now,
            source,
            [new("Berichtsart", "Ist / einzelne Application"), new("AID", aid), new("Authentisierung", Text(top, "authentication")),
             new("Dateiliste", Text(top, "listing")), new("UID / Chipversionen", "nicht in diesem Nachweis enthalten"),
             new("Schlüsselwerte", "nicht enthalten")], [],
            [new(aid, "", Text(top, "keyType"), Number(top, "maxNumberOfKeys"), "nicht zutreffend", Text(top, "keySettings"),
                "nicht geprüft", "nicht gelesen", files)], findings);
    }

    private static string Text(JsonElement value, string name)
        => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString()! : "nicht gelesen";
    private static int? Number(JsonElement value, string name)
        => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number) && number >= 0 ? number : null;
    private static string Right(JsonElement value, string name) => Number(value, name) switch
    {
        14 => "frei", 15 => "gesperrt", int number when number <= 13 => $"Key {number}", _ => "unbekannt"
    };
}

using System.Text;
using System.Text.Json;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

public sealed record ApplicationInventoryReportFiles(string TextPath, string JsonPath, string Summary);

public sealed class ApplicationInventoryReportService
{
    public ApplicationInventoryReportFiles Create(string root, string runtimeArtifactPath)
    {
        var directory = Path.GetFullPath(root);
        var source = Path.GetFullPath(runtimeArtifactPath);
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
            throw new InvalidDataException("Runtime-Inventur fehlt oder liegt außerhalb des Analyseordners.");
        using var json = JsonDocument.Parse(File.ReadAllText(source));
        var top = json.RootElement;
        if (!top.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Runtime-Inventur ist nicht als read-only ausgewiesen.");
        var aid = Value(top, "applicationId", "unbekannt");
        var auth = Value(top, "authentication", "unbekannt");
        var keySettings = Value(top, "keySettings", "nicht sicher gelesen");
        var keyType = Value(top, "keyType", "nicht sicher gelesen");
        var maxKeys = Value(top, "maxNumberOfKeys", "nicht sicher gelesen");
        int? count = top.TryGetProperty("fileCount", out var fileCount) && fileCount.TryGetInt32(out var fc) ? fc : null;
        var readable = top.TryGetProperty("readableContentCount", out var readableCount) && readableCount.TryGetInt32(out var rc) ? rc : 0;
        var builder = new StringBuilder();
        builder.AppendLine("SieEncode Teststation · Application-Inventur (nur lesend)")
            .AppendLine($"Application: {aid}").AppendLine($"Authentisierung: {auth}")
            .AppendLine($"KeySettings: {keySettings} · KeyType: {keyType} · MaxKeys: {maxKeys}")
            .AppendLine($"Files: {(count is int knownCount ? knownCount : "nicht vollständig inventarisierbar")} · Inhalte lesbar: {readable}").AppendLine();
        if (top.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                var evidence = FileInventoryEvidence.FromRuntime(file);
                builder.AppendLine($"File {Value(file, "fileId", "?")} · {Value(file, "fileType", "Typ unbekannt")} · {Value(file, "declaredSize", "Größe unbekannt")} Byte")
                    .AppendLine($"  Kommunikation: {Value(file, "communicationMode", "unbekannt")} · Metadaten: {Value(file, "metadataResult", "unbekannt")} · Inhalt: {Value(file, "contentResult", "nicht versucht")}");
                builder.AppendLine("  Lesenachweis: " + evidence.Description);
                if (file.TryGetProperty("access", out var access)) builder.AppendLine($"  Rechte: Lesen Key {Value(access, "read", "?")} · Schreiben Key {Value(access, "write", "?")} · Lesen/Schreiben Key {Value(access, "readWrite", "?")} · Ändern Key {Value(access, "change", "?")}");
                if (file.TryGetProperty("hex", out var hex) && hex.ValueKind == JsonValueKind.String)
                {
                    builder.AppendLine("  HEX: " + hex.GetString());
                    builder.AppendLine("  ASCII: " + Value(file, "ascii", ""));
                }
                builder.AppendLine();
            }
        }
        builder.AppendLine("Hinweis: Nicht lesbar bedeutet nicht leer. Value- und Record-Files werden in dieser Version inventarisiert, aber ihr Inhalt noch nicht ausgelesen.");
        var reportDirectory = Path.Combine(directory, "report"); Directory.CreateDirectory(reportDirectory);
        var textPath = Path.Combine(reportDirectory, "application-inventory.result.txt");
        var jsonPath = Path.Combine(reportDirectory, "application-inventory.result.json");
        File.WriteAllText(textPath, builder.ToString()); File.Copy(source, jsonPath, true);
        var pdfPath = Path.Combine(reportDirectory, "application-inventory.result.pdf");
        new CardReportPdfService().Save(new InventoryCardReportFactory().Create(top, source), pdfPath);
        File.AppendAllText(textPath, Environment.NewLine + "PDF-Istreport: " + pdfPath + Environment.NewLine);
        return new(textPath, jsonPath, count is int known
            ? $"AID {aid}: {known} File(s), {readable} Inhalt(e) lesbar; Authentisierung {auth}."
            : $"AID {aid}: File-Inventur nicht vollständig möglich; Authentisierung {auth}. Nicht als leer bewerten.");
    }

    private static string Value(JsonElement element, string name, string fallback)
        => element.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? value.ToString() : fallback;
}

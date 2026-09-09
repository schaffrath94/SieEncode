using System.Globalization;
using System.Text.Json;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.Runtime;

/// <summary>Compares final settings only, independently from content-read permission.</summary>
public sealed class InventoryFileSettingsComparisonService
{
    public IReadOnlyList<CardReportSettingComparison> Compare(FileDefinition expected, IReadOnlyList<JsonElement> observedFiles)
    {
        var matches = observedFiles.Where(file => Number(file, "fileId") == expected.Id).ToArray();
        var valid = matches.Length == 1 && Text(matches[0], "metadataResult") == "NoError";
        var observed = valid ? matches[0] : default;
        var rawType = valid ? Text(observed, "fileType") : null;
        var actualType = KnownType(rawType) ? rawType : null;
        var actualMode = valid ? Text(observed, "communicationMode") : null;
        if (!KnownMode(actualMode)) actualMode = null;
        var actualSize = valid && rawType is "StdDataFile" or "BackupFile" ? Number(observed, "declaredSize") : null;
        var access = valid && observed.TryGetProperty("access", out var rights) && rights.ValueKind == JsonValueKind.Object ? rights : default;
        return [
            Row("Dateityp", KnownType(expected.Type) ? expected.Type : null, actualType),
            Row("Dateigröße (Byte)", expected.Type is "StdDataFile" or "BackupFile" && expected.Size >= 0 ? expected.Size.ToString(CultureInfo.InvariantCulture) : null, actualSize?.ToString(CultureInfo.InvariantCulture)),
            Row("Kommunikation", KnownMode(expected.Communication) ? expected.Communication : null, actualMode),
            Row("Leserecht", Right(expected.Access.Read), Right(Number(access, "read"))),
            Row("Schreibrecht", Right(expected.Access.Write), Right(Number(access, "write"))),
            Row("Lesen/Schreiben", Right(expected.Access.ReadWrite), Right(Number(access, "readWrite"))),
            Row("Änderungsrecht", Right(expected.Access.Change), Right(Number(access, "change")))
        ];
    }

    private static CardReportSettingComparison Row(string name, string? expected, string? actual)
        => new(name, expected ?? "unbekannt", actual ?? "nicht nachgewiesen",
            expected is null || actual is null ? ReportComparisonState.NotComparable : expected == actual ? ReportComparisonState.Equal : ReportComparisonState.Different);
    private static bool KnownType(string? value) => value is "StdDataFile" or "BackupFile" or "ValueFile" or "LinearRecordFile" or "CyclicRecordFile";
    private static bool KnownMode(string? value) => value is "CM_PLAIN" or "CM_MAC" or "CM_ENCRYPT";
    private static string? Right(int? value) => value switch { 14 => "frei (0xE)", 15 => "gesperrt (0xF)", >= 0 and <= 13 => "Key " + value, _ => null };
    private static string? Text(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static int? Number(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number) && number >= 0 ? number : null;
}

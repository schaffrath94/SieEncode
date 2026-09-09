using System.Text.Json;

namespace RFiDGear.Builder.Runtime;

public enum InventoryReadState { Complete, Partial, NoAccess, NotChecked, Unsupported, Failed, Unknown }

/// <summary>Read evidence only; never infers a comparison result or an empty file from missing bytes.</summary>
public sealed record FileInventoryEvidence(InventoryReadState State, int? DeclaredSize, int? BytesRead, string RawResult)
{
    public string Description => State switch
    {
        InventoryReadState.Complete => $"Vollständig gelesen · {BytesRead} Byte",
        InventoryReadState.Partial => $"Teilweise gelesen · {BytesRead} von {DeclaredSize?.ToString() ?? "unbekannt vielen"} Byte; kein vollständiger Nachweis",
        InventoryReadState.NoAccess => "Kein Zugriff · Inhalt nicht bewertet",
        InventoryReadState.NotChecked => "Nicht geprüft · Inhalt nicht bewertet",
        InventoryReadState.Unsupported => "Dateityp noch nicht zum Lesen unterstützt · Inhalt nicht bewertet",
        InventoryReadState.Failed => "Lesefehler · Inhalt nicht bewertet",
        _ => "Lesenachweis unvollständig oder widersprüchlich · Inhalt nicht bewertet"
    };

    public static FileInventoryEvidence FromRuntime(JsonElement file)
    {
        var result = Text(file, "contentResult");
        var declared = file.TryGetProperty("declaredSize", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt32(out var number) && number >= 0 ? (int?)number : null;
        var hex = Text(file, "hex");
        int? bytes = hex is not null && hex.Length % 2 == 0 && hex.All(Uri.IsHexDigit) ? hex.Length / 2 : null;
        var truncated = file.TryGetProperty("truncated", out var limit) && limit.ValueKind == JsonValueKind.True;
        var state = result switch
        {
            "AuthFailure" or "AuthenticationError" or "PermissionDenied" => InventoryReadState.NoAccess,
            "NotRequested" or "NotAttempted" or "EmptyOrUnknownSize" => InventoryReadState.NotChecked,
            "DeferredFileType" => InventoryReadState.Unsupported,
            null or "" => InventoryReadState.Unknown,
            "NoError" when bytes is null || declared is null || bytes > declared => InventoryReadState.Unknown,
            "NoError" when truncated || bytes < declared => InventoryReadState.Partial,
            "NoError" => InventoryReadState.Complete,
            _ => InventoryReadState.Failed
        };
        return new(state, declared, bytes, result ?? "unbekannt");
    }

    private static string? Text(JsonElement file, string name)
        => file.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

using System.Text.Json;

namespace RFiDGear.Builder.Runtime;

public enum PayloadComparisonState { Equal, Different, NotComparable }
public sealed record InventoryPayloadComparison(int FileId, PayloadComparisonState State, string Reason);

/// <summary>Compares supplied expected bytes for one AID. Does not claim settings or whole-card acceptance.</summary>
public sealed class InventoryPayloadComparisonService
{
    public IReadOnlyList<InventoryPayloadComparison> Compare(string expectedAid, IReadOnlyDictionary<int, byte[]> expected, JsonElement inventory)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var aid = inventory.TryGetProperty("applicationId", out var aidValue) && aidValue.ValueKind == JsonValueKind.String ? aidValue.GetString() : null;
        if (!inventory.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.True ||
            !string.Equals(expectedAid, aid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Inventur gehört nicht zur erwarteten Nur-Lese-Application.");
        var files = inventory.TryGetProperty("files", out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().ToArray() : [];
        var result = new List<InventoryPayloadComparison>();
        foreach (var pair in expected.OrderBy(pair => pair.Key))
        {
            var matches = files.Where(file => file.TryGetProperty("fileId", out var id) &&
                id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number) && number == pair.Key).ToArray();
            if (matches.Length != 1)
            {
                result.Add(new(pair.Key, PayloadComparisonState.NotComparable,
                    matches.Length == 0 ? "Kein Lesenachweis für diese Datei." : "Mehrdeutiger Lesenachweis für diese Datei."));
                continue;
            }
            var evidence = FileInventoryEvidence.FromRuntime(matches[0]);
            if (evidence.State != InventoryReadState.Complete)
            {
                result.Add(new(pair.Key, PayloadComparisonState.NotComparable, evidence.Description));
                continue;
            }
            var actual = Convert.FromHexString(matches[0].GetProperty("hex").GetString()!);
            var equal = pair.Value.AsSpan().SequenceEqual(actual);
            result.Add(new(pair.Key, equal ? PayloadComparisonState.Equal : PayloadComparisonState.Different,
                equal ? "Vollständig gelesener Inhalt stimmt bytegenau überein."
                : "Vollständig gelesener Inhalt weicht vom Soll ab."));
        }
        return result.AsReadOnly();
    }
}

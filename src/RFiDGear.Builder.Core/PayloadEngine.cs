using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public sealed record PayloadArtifact(string Aid, int FileId, string Output, byte[] Bytes);

public static partial class PayloadEngine
{
    public static IReadOnlyList<PayloadArtifact> BuildAll(
        ComposedProfiles profiles,
        IReadOnlyDictionary<string, string> projectValues,
        IReadOnlyDictionary<string, string> cardValues)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(projectValues);
        ArgumentNullException.ThrowIfNull(cardValues);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in profiles.Variables)
        {
            var source = definition.Scope == "project" ? projectValues : cardValues;
            if (!source.TryGetValue(definition.Id, out var value) || string.IsNullOrEmpty(value)) value = definition.Default ?? "";
            var issues = ProfileValidator.ValidateVariableValue(definition, value);
            if (issues.Count > 0) throw new InvalidDataException(issues[0].Message);
            values.Add(definition.Id, value);
        }

        var result = new List<PayloadArtifact>();
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var application in profiles.Applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            foreach (var file in application.Files.Where(file => file.Coding is not null))
            {
                var output = file.Coding!.Output;
                if (!outputs.Add(output)) throw new InvalidDataException($"Payload-Ausgabedatei ist mehrfach definiert: {output}");
                result.Add(new(aid, file.Id, output, Build(file, values, aid)));
            }
        }
        return result;
    }

    public static byte[] Build(FileDefinition file, IReadOnlyDictionary<string, string> values, string aid)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(values);
        if (file.Coding is null) throw new InvalidDataException($"AID {aid} File {file.Id} besitzt kein Kodierrezept.");
        var bytes = new List<byte>(file.Size);
        foreach (var step in file.Coding.Recipe)
        {
            switch (step.Op)
            {
                case "fixedHex":
                    var hex = WhitespaceRegex().Replace(step.Value ?? "", "");
                    if (hex.Length % 2 != 0 || !HexRegex().IsMatch(hex)) throw new InvalidDataException($"AID {aid} File {file.Id}: fixedHex ist ungültig.");
                    bytes.AddRange(Convert.FromHexString(hex));
                    break;
                case "fixedAscii":
                    bytes.AddRange(GetAscii(step.Value ?? "", aid, file.Id));
                    break;
                case "fixedUtf8":
                    bytes.AddRange(Encoding.UTF8.GetBytes(step.Value ?? ""));
                    break;
                case "hexBytes":
                    bytes.AddRange(GetHexBytes(step, values, aid, file.Id));
                    break;
                case "asciiText":
                    bytes.AddRange(GetFixedText(step, values, aid, file.Id, Encoding.ASCII));
                    break;
                case "utf8Text":
                    bytes.AddRange(GetFixedText(step, values, aid, file.Id, Encoding.UTF8));
                    break;
                case "asciiDigits":
                    var ascii = GetPaddedDigits(step, values, aid, file.Id);
                    bytes.AddRange(Encoding.ASCII.GetBytes(ascii));
                    break;
                case "bcdDigits":
                    var bcd = GetPaddedDigits(step, values, aid, file.Id);
                    if (bcd.Length % 2 != 0) throw new InvalidDataException("BCD-Breite muss gerade sein.");
                    for (var index = 0; index < bcd.Length; index += 2)
                        bytes.Add(byte.Parse(bcd.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    break;
                case "uintBigEndian":
                case "uintLittleEndian":
                    bytes.AddRange(GetUnsignedInteger(step, values, aid, file.Id, step.Op == "uintLittleEndian"));
                    break;
                case "zeroPadToFileSize":
                    if (bytes.Count > file.Size) throw new InvalidDataException($"Payload für AID {aid} File {file.Id} überschreitet die Filegröße.");
                    while (bytes.Count < file.Size) bytes.Add(0);
                    break;
                case "padToFileSize":
                    if (bytes.Count > file.Size) throw new InvalidDataException($"Payload für AID {aid} File {file.Id} überschreitet die Filegröße.");
                    var padByte = ParseSingleHexByte(step.Value, "Padding-Byte");
                    while (bytes.Count < file.Size) bytes.Add(padByte);
                    break;
                default:
                    throw new InvalidDataException($"Unbekannte Payload-Operation: {step.Op}");
            }
        }
        if (bytes.Count != file.Size) throw new InvalidDataException($"Payload für AID {aid} File {file.Id} hat {bytes.Count} statt {file.Size} Byte.");
        return bytes.ToArray();
    }

    public static string ToHexFileText(ReadOnlySpan<byte> bytes)
        => string.Join(" ", bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

    private static string GetPaddedDigits(RecipeStep step, IReadOnlyDictionary<string, string> values, string aid, int fileId)
    {
        if (string.IsNullOrWhiteSpace(step.Source) || !values.TryGetValue(step.Source, out var value) || !DigitsRegex().IsMatch(value))
            throw new InvalidDataException($"Variable '{step.Source}' ist keine Ziffernfolge.");
        var width = step.Width ?? 0;
        if (width <= 0) throw new InvalidDataException($"AID {aid} File {fileId}: Breite muss >0 sein.");
        if (value.Length > width) throw new InvalidDataException($"Variable '{step.Source}' ist länger als {width} Stellen.");
        var padding = string.IsNullOrEmpty(step.PadLeft) ? '0' : step.PadLeft[0];
        return value.PadLeft(width, padding);
    }

    private static byte[] GetAscii(string value, string aid, int fileId)
    {
        if (value.Any(character => character > 0x7f))
            throw new InvalidDataException($"AID {aid} File {fileId}: ASCII-Text enthält nicht darstellbare Zeichen.");
        return Encoding.ASCII.GetBytes(value);
    }

    private static byte[] GetFixedText(RecipeStep step, IReadOnlyDictionary<string, string> values, string aid, int fileId, Encoding encoding)
    {
        var value = GetValue(step, values);
        var encoded = encoding == Encoding.ASCII ? GetAscii(value, aid, fileId) : encoding.GetBytes(value);
        var width = step.Width ?? 0;
        if (width <= 0) throw new InvalidDataException($"AID {aid} File {fileId}: Textbreite muss >0 sein.");
        if (encoded.Length > width) throw new InvalidDataException($"Variable '{step.Source}' benötigt {encoded.Length} Byte, vorgesehen sind {width}.");
        var result = new byte[width];
        var pad = ParseOptionalByte(step.PadLeft ?? step.PadRight, encoding == Encoding.ASCII ? (byte)0x20 : (byte)0x00);
        Array.Fill(result, pad);
        var offset = step.PadLeft is not null ? width - encoded.Length : 0;
        encoded.CopyTo(result, offset);
        return result;
    }

    private static byte[] GetHexBytes(RecipeStep step, IReadOnlyDictionary<string, string> values, string aid, int fileId)
    {
        var value = WhitespaceRegex().Replace(GetValue(step, values), "");
        if (value.Length % 2 != 0 || !HexRegex().IsMatch(value)) throw new InvalidDataException($"Variable '{step.Source}' ist keine vollständige HEX-Bytefolge.");
        var source = Convert.FromHexString(value);
        var width = step.Width ?? 0;
        if (width <= 0) throw new InvalidDataException($"AID {aid} File {fileId}: HEX-Breite muss >0 Byte sein.");
        if (source.Length > width) throw new InvalidDataException($"Variable '{step.Source}' benötigt {source.Length} Byte, vorgesehen sind {width}.");
        var result = new byte[width];
        var pad = ParseOptionalByte(step.PadLeft ?? step.PadRight, 0x00);
        Array.Fill(result, pad);
        source.CopyTo(result, step.PadLeft is not null ? width - source.Length : 0);
        return result;
    }

    private static byte[] GetUnsignedInteger(RecipeStep step, IReadOnlyDictionary<string, string> values, string aid, int fileId, bool littleEndian)
    {
        var value = GetValue(step, values);
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            throw new InvalidDataException($"Variable '{step.Source}' ist keine vorzeichenlose Dezimalzahl.");
        var width = step.Width ?? 0;
        if (width is < 1 or > 8) throw new InvalidDataException($"AID {aid} File {fileId}: Integerbreite muss 1..8 Byte sein.");
        if (width < 8 && number >= (1UL << (width * 8))) throw new InvalidDataException($"Variable '{step.Source}' passt nicht in {width} Byte.");
        var result = new byte[width];
        for (var index = 0; index < width; index++) result[littleEndian ? index : width - 1 - index] = (byte)(number >> (index * 8));
        return result;
    }

    private static string GetValue(RecipeStep step, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(step.Source) || !values.TryGetValue(step.Source, out var value))
            throw new InvalidDataException($"Variable '{step.Source}' ist nicht verfügbar.");
        return value;
    }

    private static byte ParseOptionalByte(string? value, byte fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : ParseSingleHexByte(value, "Füllbyte");

    private static byte ParseSingleHexByte(string? value, string label)
    {
        var normalized = WhitespaceRegex().Replace(value ?? "", "");
        if (normalized.Length != 2 || !HexRegex().IsMatch(normalized)) throw new InvalidDataException($"{label} muss genau ein HEX-Byte (00..FF) sein.");
        return byte.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("\\s", RegexOptions.CultureInvariant)] private static partial Regex WhitespaceRegex();
    [GeneratedRegex("^[0-9A-Fa-f]*$", RegexOptions.CultureInvariant)] private static partial Regex HexRegex();
    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex DigitsRegex();
}

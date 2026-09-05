using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public static partial class ProfileValidator
{
    private static readonly HashSet<string> ProfileKinds = new(StringComparer.Ordinal) { "base", "customer" };
    private static readonly HashSet<string> KeyTypes = new(StringComparer.Ordinal) { "DF_KEY_DES", "DF_KEY_AES", "DF_KEY_3K3DES" };
    private static readonly HashSet<string> KeyChangeModes = new(StringComparer.Ordinal) { "ChangeKeyUsingMK", "ChangeKeyUsingKeyNo" };
    private static readonly HashSet<string> FileTypes = new(StringComparer.Ordinal) { "StdDataFile", "BackupFile" };
    private static readonly HashSet<string> Communications = new(StringComparer.Ordinal) { "CM_PLAIN", "CM_MAC", "CM_ENCRYPT" };
    private static readonly HashSet<string> VariableTypes = new(StringComparer.Ordinal) { "digits", "text", "hex" };
    private static readonly HashSet<string> RecipeOperations = new(StringComparer.Ordinal)
    {
        "fixedHex", "fixedAscii", "fixedUtf8", "hexBytes", "asciiText", "utf8Text", "asciiDigits", "bcdDigits",
        "uintBigEndian", "uintLittleEndian", "zeroPadToFileSize", "padToFileSize"
    };

    public static IReadOnlyList<ValidationIssue> Validate(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(profile.Profile.Id)) issues.Add(new("PROFILE_ID", "profile.id fehlt."));
        if (string.IsNullOrWhiteSpace(profile.Profile.Code)) issues.Add(new("PROFILE_CODE", "profile.code fehlt."));
        if (string.IsNullOrWhiteSpace(profile.Profile.Name)) issues.Add(new("PROFILE_NAME", "profile.name fehlt."));
        if (string.IsNullOrWhiteSpace(profile.Profile.Version)) issues.Add(new("PROFILE_VERSION", "profile.version fehlt."));
        if (!ProfileKinds.Contains(profile.Profile.Kind)) issues.Add(new("PROFILE_KIND", "profile.kind muss base oder customer sein."));

        var variableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in profile.Variables)
        {
            if (!VariableIdRegex().IsMatch(variable.Id)) issues.Add(new("VARIABLE_ID", $"Ungültige Variablen-ID: {variable.Id}"));
            else if (!variableIds.Add(variable.Id)) issues.Add(new("VARIABLE_DUPLICATE", $"Variable '{variable.Id}' ist doppelt."));
            if (variable.Scope is not ("project" or "card")) issues.Add(new("VARIABLE_SCOPE", $"Variable {variable.Id}: scope muss project oder card sein."));
            if (!VariableTypes.Contains(variable.Type)) issues.Add(new("VARIABLE_TYPE", $"Variable {variable.Id}: type muss digits, text oder hex sein."));
            ValidateVariableRules(issues, variable);
        }

        var aids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var application in profile.Applications)
        {
            var aid = application.Id.Trim().ToUpperInvariant();
            if (!AidRegex().IsMatch(aid)) issues.Add(new("AID", $"Ungültige DESFire-AID: {application.Id}"));
            else if (!aids.Add(aid)) issues.Add(new("AID_DUPLICATE", $"AID {aid} ist im Profil doppelt."));
            if (string.IsNullOrWhiteSpace(application.Name)) issues.Add(new("APPLICATION_NAME", $"AID {aid}: Name fehlt."));
            if (!KeyTypes.Contains(application.KeyType)) issues.Add(new("KEY_TYPE", $"AID {aid}: ungültiger keyType."));
            if (application.KeyCount is < 1 or > 14) issues.Add(new("KEY_COUNT", $"AID {aid}: keyCount muss 1..14 sein."));
            ValidateKeySettings(issues, application.TransportKeySettings, $"AID {aid} transportKeySettings");
            ValidateKeySettings(issues, application.ProductiveKeySettings, $"AID {aid} productiveKeySettings");
            if (!KeyChangeModes.Contains(application.ChangeKeyMode)) issues.Add(new("CHANGE_KEY_MODE", $"AID {aid}: ungültiger changeKeyMode."));
            ValidateFiles(issues, application, profile.Variables.ToDictionary(variable => variable.Id, variable => variable.Type, StringComparer.Ordinal), aid);
        }
        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidateVariableValue(VariableDefinition definition, string? value)
    {
        value ??= "";
        var issues = new List<ValidationIssue>();
        var label = string.IsNullOrWhiteSpace(definition.Label) ? definition.Id : definition.Label;
        if (definition.Required && string.IsNullOrWhiteSpace(value)) issues.Add(new("VALUE_REQUIRED", $"{label} darf nicht leer sein."));
        if (!definition.Required && string.IsNullOrWhiteSpace(value)) return issues;
        if (value.Length > 0 && definition.Type == "digits" && !DigitsRegex().IsMatch(value)) issues.Add(new("VALUE_DIGITS", $"{label} darf nur Ziffern enthalten."));
        if (value.Length > 0 && definition.Type == "hex" && (!HexRegex().IsMatch(value) || value.Length % 2 != 0)) issues.Add(new("VALUE_HEX", $"{label} muss eine gerade HEX-Zeichenfolge (vollständige Bytes) sein."));
        if (definition.Length is { } length && value.Length != length) issues.Add(new("VALUE_LENGTH", $"{label} muss exakt {length} Stellen haben."));
        if (definition.AllowedLengths is { Count: > 0 } allowed && !allowed.Contains(value.Length)) issues.Add(new("VALUE_ALLOWED_LENGTH", $"{label} muss eine der Längen {string.Join(", ", allowed)} haben."));
        if (definition.MinLength is { } minimum && value.Length < minimum) issues.Add(new("VALUE_MIN_LENGTH", $"{label} muss mindestens {minimum} Stellen haben."));
        if (definition.MaxLength is { } maximum && value.Length > maximum) issues.Add(new("VALUE_MAX_LENGTH", $"{label} darf höchstens {maximum} Stellen haben."));
        return issues;
    }

    public static string NormalizeAid(string aid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aid);
        var normalized = aid.Trim().ToUpperInvariant();
        if (normalized.StartsWith("0X", StringComparison.Ordinal)) normalized = normalized[2..];
        if (!AidRegex().IsMatch(normalized)) throw new InvalidDataException($"Ungültige DESFire-AID: {aid}");
        return normalized;
    }

    private static void ValidateVariableRules(List<ValidationIssue> issues, VariableDefinition variable)
    {
        var ruleCount = (variable.Length is null ? 0 : 1) + (variable.AllowedLengths is null ? 0 : 1) + (variable.MinLength is null && variable.MaxLength is null ? 0 : 1);
        if (ruleCount != 1) issues.Add(new("VARIABLE_LENGTH_RULE", $"Variable {variable.Id}: genau eine Längenregel (length, allowedLengths oder min/max) ist erforderlich."));
        if (variable.Length is <= 0) issues.Add(new("VARIABLE_LENGTH", $"Variable {variable.Id}: length muss positiv sein."));
        if (variable.AllowedLengths is { } allowed && (allowed.Count == 0 || allowed.Any(value => value <= 0))) issues.Add(new("VARIABLE_ALLOWED_LENGTHS", $"Variable {variable.Id}: allowedLengths muss positive Werte enthalten."));
        if (variable.MinLength is <= 0) issues.Add(new("VARIABLE_MIN_LENGTH", $"Variable {variable.Id}: minLength muss positiv sein."));
        if (variable.MaxLength is <= 0) issues.Add(new("VARIABLE_MAX_LENGTH", $"Variable {variable.Id}: maxLength muss positiv sein."));
        if (variable.MinLength is { } minimum && variable.MaxLength is { } maximum && minimum > maximum) issues.Add(new("VARIABLE_RANGE", $"Variable {variable.Id}: minLength ist größer als maxLength."));
        if (variable.Default is not null) issues.AddRange(ValidateVariableValue(variable, variable.Default).Select(issue => issue with { Code = "DEFAULT_" + issue.Code }));
    }

    private static void ValidateFiles(List<ValidationIssue> issues, ApplicationDefinition application, IReadOnlyDictionary<string, string> variableTypes, string aid)
    {
        var fileIds = new HashSet<int>();
        foreach (var file in application.Files)
        {
            if (file.Id is < 0 or > 31) issues.Add(new("FILE_ID", $"AID {aid}: FileID {file.Id} außerhalb 0..31."));
            else if (!fileIds.Add(file.Id)) issues.Add(new("FILE_DUPLICATE", $"AID {aid}: FileID {file.Id} ist doppelt."));
            if (!FileTypes.Contains(file.Type)) issues.Add(new("FILE_TYPE", $"AID {aid} File {file.Id}: ungültiger Typ."));
            if (string.IsNullOrWhiteSpace(file.Name)) issues.Add(new("FILE_NAME", $"AID {aid} File {file.Id}: Name fehlt."));
            if (!Communications.Contains(file.Communication)) issues.Add(new("FILE_COMMUNICATION", $"AID {aid} File {file.Id}: ungültige Communication."));
            if (file.Size is < 1 or > 65535) issues.Add(new("FILE_SIZE", $"AID {aid} File {file.Id}: Größe muss zwischen 1 und 65535 liegen."));
            ValidateAccess(issues, file.Access, application.KeyCount, aid, file.Id, "Access");
            if (file.CreationSettings is not null)
            {
                if (!Communications.Contains(file.CreationSettings.Communication)) issues.Add(new("CREATION_COMMUNICATION", $"AID {aid} File {file.Id}: creationSettings.communication ist ungültig."));
                ValidateAccess(issues, file.CreationSettings.Access, application.KeyCount, aid, file.Id, "temporärer Mastering-Access");
            }
            if (file.MasteringInitialization is not null && file.MasteringInitialization.Type is not ("zeroFill" or "none"))
                issues.Add(new("MASTERING_INITIALIZATION", $"AID {aid} File {file.Id}: masteringInitialization.type ist ungültig."));
            if (file.Coding is not null) ValidateRecipe(issues, file, variableTypes, aid);
        }
    }

    private static void ValidateAccess(List<ValidationIssue> issues, AccessDefinition access, int keyCount, string aid, int fileId, string label)
    {
        foreach (var (name, keyNumber) in new[] { ("read", access.Read), ("write", access.Write), ("readWrite", access.ReadWrite), ("change", access.Change) })
            if (keyNumber < 0 || keyNumber >= keyCount) issues.Add(new("FILE_ACCESS", $"AID {aid} File {fileId}: {label} {name}=Key{keyNumber} außerhalb Keyanzahl."));
    }

    private static void ValidateRecipe(List<ValidationIssue> issues, FileDefinition file, IReadOnlyDictionary<string, string> variableTypes, string aid)
    {
        var coding = file.Coding!;
        if (string.IsNullOrWhiteSpace(coding.Output)) issues.Add(new("CODING_OUTPUT", $"AID {aid} File {file.Id}: coding.output fehlt."));
        if (coding.Recipe.Count == 0) { issues.Add(new("CODING_RECIPE", $"AID {aid} File {file.Id}: coding.recipe ist leer.")); return; }
        var knownBytes = 0;
        var hasPadding = false;
        for (var index = 0; index < coding.Recipe.Count; index++)
        {
            var step = coding.Recipe[index];
            if (!RecipeOperations.Contains(step.Op)) { issues.Add(new("RECIPE_OPERATION", $"AID {aid} File {file.Id}: unbekannte Recipe-Operation {step.Op}.")); continue; }
            if (step.Op is "asciiDigits" or "bcdDigits" or "hexBytes" or "asciiText" or "utf8Text" or "uintBigEndian" or "uintLittleEndian")
            {
                if (string.IsNullOrWhiteSpace(step.Source) || !variableTypes.ContainsKey(step.Source)) issues.Add(new("RECIPE_VARIABLE", $"AID {aid} File {file.Id}: Recipe referenziert unbekannte Variable '{step.Source}'."));
                else if ((step.Op is "asciiDigits" or "bcdDigits" or "uintBigEndian" or "uintLittleEndian") && variableTypes[step.Source] != "digits")
                    issues.Add(new("RECIPE_VARIABLE_TYPE", $"AID {aid} File {file.Id}: {step.Op} benötigt eine Ziffern-Variable, '{step.Source}' ist {variableTypes[step.Source]}."));
                else if (step.Op == "hexBytes" && variableTypes[step.Source] != "hex")
                    issues.Add(new("RECIPE_VARIABLE_TYPE", $"AID {aid} File {file.Id}: hexBytes benötigt eine HEX-Variable, '{step.Source}' ist {variableTypes[step.Source]}."));
            }
            switch (step.Op)
            {
                case "fixedHex":
                    var hex = Regex.Replace(step.Value ?? "", "\\s", "");
                    if (!HexRegex().IsMatch(hex) || hex.Length % 2 != 0) issues.Add(new("RECIPE_FIXED_HEX", $"AID {aid} File {file.Id}: fixedHex ungültig."));
                    else knownBytes += hex.Length / 2;
                    break;
                case "fixedAscii":
                    if ((step.Value ?? "").Any(character => character > 0x7f)) issues.Add(new("RECIPE_FIXED_ASCII", $"AID {aid} File {file.Id}: fester ASCII-Text enthält nicht darstellbare Zeichen."));
                    else knownBytes += (step.Value ?? "").Length;
                    break;
                case "fixedUtf8":
                    knownBytes += System.Text.Encoding.UTF8.GetByteCount(step.Value ?? "");
                    break;
                case "hexBytes":
                    ValidateWidth(issues, step, aid, file.Id, "HEX");
                    ValidatePadBytes(issues, step, aid, file.Id);
                    if (step.Width is > 0) knownBytes += step.Width.Value;
                    break;
                case "asciiText":
                case "utf8Text":
                    ValidateWidth(issues, step, aid, file.Id, step.Op == "asciiText" ? "ASCII-Text" : "UTF-8-Text");
                    ValidatePadBytes(issues, step, aid, file.Id);
                    if (step.Width is > 0) knownBytes += step.Width.Value;
                    break;
                case "asciiDigits":
                    if (step.Width is null or <= 0) issues.Add(new("RECIPE_ASCII_WIDTH", $"AID {aid} File {file.Id}: asciiDigits width muss >0 sein."));
                    else knownBytes += step.Width.Value;
                    break;
                case "bcdDigits":
                    if (step.Width is null or <= 0 || step.Width % 2 != 0) issues.Add(new("RECIPE_BCD_WIDTH", $"AID {aid} File {file.Id}: bcdDigits width muss positiv und gerade sein."));
                    else knownBytes += step.Width.Value / 2;
                    break;
                case "uintBigEndian":
                case "uintLittleEndian":
                    if (step.Width is null or < 1 or > 8) issues.Add(new("RECIPE_INTEGER_WIDTH", $"AID {aid} File {file.Id}: Integerbreite muss 1..8 Byte sein."));
                    else knownBytes += step.Width.Value;
                    break;
                case "zeroPadToFileSize":
                    if (index != coding.Recipe.Count - 1) issues.Add(new("RECIPE_PADDING_POSITION", $"AID {aid} File {file.Id}: zeroPadToFileSize muss letzter Baustein sein."));
                    hasPadding = true;
                    break;
                case "padToFileSize":
                    if (index != coding.Recipe.Count - 1) issues.Add(new("RECIPE_PADDING_POSITION", $"AID {aid} File {file.Id}: padToFileSize muss letzter Baustein sein."));
                    if (!IsHexByte(step.Value)) issues.Add(new("RECIPE_PADDING_BYTE", $"AID {aid} File {file.Id}: Padding muss genau ein HEX-Byte (00..FF) sein."));
                    hasPadding = true;
                    break;
            }
        }
        if (hasPadding && knownBytes > file.Size) issues.Add(new("RECIPE_SIZE", $"AID {aid} File {file.Id}: Rezept ist größer als die Filegröße."));
        if (!hasPadding && knownBytes != file.Size) issues.Add(new("RECIPE_SIZE", $"AID {aid} File {file.Id}: Rezept ergibt {knownBytes} Byte statt {file.Size}."));
    }

    private static void ValidateWidth(List<ValidationIssue> issues, RecipeStep step, string aid, int fileId, string label)
    {
        if (step.Width is null or <= 0) issues.Add(new("RECIPE_WIDTH", $"AID {aid} File {fileId}: {label}-Breite muss >0 Byte sein."));
    }

    private static void ValidatePadBytes(List<ValidationIssue> issues, RecipeStep step, string aid, int fileId)
    {
        if (step.PadLeft is not null && step.PadRight is not null) issues.Add(new("RECIPE_PADDING_DIRECTION", $"AID {aid} File {fileId}: nur links oder rechts auffüllen."));
        if (step.PadLeft is not null && !IsHexByte(step.PadLeft)) issues.Add(new("RECIPE_PADDING_BYTE", $"AID {aid} File {fileId}: linkes Füllbyte muss 00..FF sein."));
        if (step.PadRight is not null && !IsHexByte(step.PadRight)) issues.Add(new("RECIPE_PADDING_BYTE", $"AID {aid} File {fileId}: rechtes Füllbyte muss 00..FF sein."));
    }

    private static bool IsHexByte(string? value) => value is { Length: 2 } && HexRegex().IsMatch(value);

    private static void ValidateKeySettings(List<ValidationIssue> issues, string value, string label)
    {
        if (!KeySettingsRegex().IsMatch(value ?? "")) issues.Add(new("KEY_SETTINGS", $"{label} muss zwischen 00 und 0F liegen."));
    }

    [GeneratedRegex("^[0-9A-F]{6}$", RegexOptions.CultureInvariant)] private static partial Regex AidRegex();
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)] private static partial Regex VariableIdRegex();
    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex DigitsRegex();
    [GeneratedRegex("^[0-9A-Fa-f]*$", RegexOptions.CultureInvariant)] private static partial Regex HexRegex();
    [GeneratedRegex("^0[0-9A-Fa-f]$", RegexOptions.CultureInvariant)] private static partial Regex KeySettingsRegex();
}

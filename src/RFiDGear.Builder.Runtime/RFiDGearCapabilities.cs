using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.Runtime;

public enum RFiDGearCapabilityId
{
    CustomProjectFile,
    AutorunOnce,
    ContinuousAutoMode,
    ChangeFileSettings,
    ExactAppSettingsCheck,
    AppKeyCountCheck,
    FileSettingsCheck,
    ApplicationFileInventory,
    VerifiedApplicationKeyChange,
    StructuredTaskLogging,
    AuthoritativeTaskLoopTerminalStatus,
    LegacyUiExtensions,
    ApplicationUiExtensionHost,
    StableExtensionHostApi
}

public enum RFiDGearCapabilityState { Available, Unavailable, Unknown }

public sealed record RFiDGearCapability(
    RFiDGearCapabilityId Id,
    string Name,
    RFiDGearCapabilityState State,
    string Evidence);

public sealed record RFiDGearCapabilityReport(
    string ExecutablePath,
    RFiDGearVersionInfo Version,
    IReadOnlyList<string> InspectedFiles,
    IReadOnlyList<RFiDGearCapability> Capabilities,
    string? Error = null)
{
    public RFiDGearCapabilityState StateOf(RFiDGearCapabilityId id)
        => Capabilities.FirstOrDefault(capability => capability.Id == id)?.State ?? RFiDGearCapabilityState.Unknown;

    public bool Has(RFiDGearCapabilityId id) => StateOf(id) == RFiDGearCapabilityState.Available;
}

public interface IRFiDGearCapabilityInspector
{
    RFiDGearCapabilityReport Inspect(string executablePath);
}

public sealed record RFiDGearApprovedKeyChangeBuild(string ProductVersion, string EvidenceSha256, string Evidence);

public interface IRFiDGearKeyChangeReleasePolicy
{
    bool TryApprove(RFiDGearVersionInfo version, string evidenceSha256, out string evidence);
}

/// <summary>
/// Exact, audited RFiDGear builds that may perform productive DESFire key changes.
/// Implementation markers alone are deliberately insufficient because incomplete
/// development builds have previously exposed the same symbols.
/// </summary>
public sealed class RFiDGearKeyChangeReleasePolicy : IRFiDGearKeyChangeReleasePolicy
{
    public static IReadOnlyList<RFiDGearApprovedKeyChangeBuild> ApprovedBuilds { get; } =
        RFiDGearTrustedBuildCatalog.KeyChangeBuilds
            .Select(item => new RFiDGearApprovedKeyChangeBuild(item.ProductVersion, item.EvidenceSha256, $"{item.Name}: {item.Evidence}"))
            .ToArray();

    public bool TryApprove(RFiDGearVersionInfo version, string evidenceSha256, out string evidence)
    {
        var approved = ApprovedBuilds.FirstOrDefault(item =>
            string.Equals(item.ProductVersion, version.ProductVersion, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.EvidenceSha256, evidenceSha256, StringComparison.OrdinalIgnoreCase));
        evidence = approved?.Evidence ??
            "Implementationsmarker vorhanden, aber dieser exakte RFiDGear-Build ist nicht im hardwaregeprüften Builder-Vertrauenskatalog freigegeben.";
        return approved is not null;
    }
}

public sealed class RFiDGearCapabilityInspector : IRFiDGearCapabilityInspector
{
    private sealed record Definition(RFiDGearCapabilityId Id, string Name, string[][] Alternatives);

    private static readonly Definition[] Definitions =
    [
        new(RFiDGearCapabilityId.CustomProjectFile, "Projektdatei beim Start", [["CUSTOMPROJECTFILE"]]),
        new(RFiDGearCapabilityId.AutorunOnce, "Automatischer Einmallauf", [["CUSTOMPROJECTFILE", "AUTORUN"]]),
        new(RFiDGearCapabilityId.ContinuousAutoMode, "Kontinuierlicher Serienmodus", [["CUSTOMPROJECTFILE", "AUTOMODE"]]),
        new(RFiDGearCapabilityId.ChangeFileSettings, "DESFire-Dateieinstellungen ändern", [["ChangeFileSettings"]]),
        new(RFiDGearCapabilityId.ExactAppSettingsCheck, "Application-KeySettings exakt prüfen", [["ReadAppSettings", "IsAppSettingsExactMatch"]]),
        new(RFiDGearCapabilityId.AppKeyCountCheck, "Application-Keyanzahl prüfen", [["CheckAppKeyCount"]]),
        new(RFiDGearCapabilityId.FileSettingsCheck, "DESFire-Dateieinstellungen read-only prüfen", [["CheckFileSettings"], ["ReadFileSettings"]]),
        new(RFiDGearCapabilityId.ApplicationFileInventory, "Fremdapplication und Files read-only inventarisieren", [["ApplicationFileInventory", "ApplicationInventoryOutputPath"]]),
        new(RFiDGearCapabilityId.VerifiedApplicationKeyChange, "Verifizierter DESFire-Keywechsel",
            [["VerifyChangedApplicationKey", "VerifyMifareDesfireKeyChange", "ChangeKey reported"],
             ["PostChangeKeyVerification", "AuthoritativeKeyChangeResult"]]),
        new(RFiDGearCapabilityId.StructuredTaskLogging, "Korrelierte Task-Ergebnislogs", [["Task.Outcome", "TaskLoop.Summary", "RunId"]]),
        new(RFiDGearCapabilityId.AuthoritativeTaskLoopTerminalStatus, "Autoritativer TaskLoop-Terminalstatus",
            [["AcceptedFailures", "UnhandledFailures", "InvalidSkips", "TerminalStatus"]]),
        new(RFiDGearCapabilityId.LegacyUiExtensions, "Kartenspezifische UI-Erweiterungen", [["IUIExtension", "UiExtensionAttribute"]]),
        new(RFiDGearCapabilityId.ApplicationUiExtensionHost, "Anwendungsweiter UI-Erweiterungshost", [["IApplicationUIExtension"], ["ApplicationUiExtensionAttribute"]]),
        new(RFiDGearCapabilityId.StableExtensionHostApi, "Stabile Projekt-/Ausführungs-API", [["IRFiDGearHostV1"], ["IRFiDGearExtensionHost"], ["IProjectHost", "ITaskExecutionHost"]])
    ];

    private readonly IRFiDGearKeyChangeReleasePolicy keyChangeReleasePolicy;

    public RFiDGearCapabilityInspector(IRFiDGearKeyChangeReleasePolicy? keyChangeReleasePolicy = null)
    {
        this.keyChangeReleasePolicy = keyChangeReleasePolicy ?? new RFiDGearKeyChangeReleasePolicy();
    }

    public RFiDGearCapabilityReport Inspect(string executablePath)
    {
        var executable = Path.GetFullPath(executablePath ?? "");
        RFiDGearVersionInfo version;
        try { version = RFiDGearLauncher.ReadVersion(executable); }
        catch (Exception exception)
        {
            return Unknown(executable, exception.Message);
        }

        var files = ResolveEvidenceFiles(executable).ToArray();
        try
        {
            var byteSets = files.Select(File.ReadAllBytes).ToArray();
            var fingerprint = EvidenceFingerprint(files, byteSets);
            var approved = keyChangeReleasePolicy.TryApprove(version, fingerprint, out var approvalEvidence);
            return AnalyzeEvidence(executable, version, files, token => byteSets.Any(bytes => ContainsToken(bytes, token)), approved, approvalEvidence);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unknown(executable, exception.Message, version, files);
        }
    }

    public static RFiDGearCapabilityReport AnalyzeEvidence(
        string executablePath,
        RFiDGearVersionInfo version,
        IReadOnlyList<string> inspectedFiles,
        Func<string, bool> containsToken,
        bool verifiedKeyChangeReleaseApproved = false,
        string? verifiedKeyChangeReleaseEvidence = null,
        bool verifiedFileSettingsReleaseApproved = false)
    {
        ArgumentNullException.ThrowIfNull(containsToken);
        var capabilities = Definitions.Select(definition =>
        {
            var matching = definition.Alternatives.FirstOrDefault(alternative => alternative.All(containsToken));
            var expected = string.Join(" oder ", definition.Alternatives.Select(alternative => string.Join(" + ", alternative)));
            if (matching is null)
                return new RFiDGearCapability(definition.Id, definition.Name, RFiDGearCapabilityState.Unavailable, $"Marker nicht gefunden: {expected}");
            if (definition.Id == RFiDGearCapabilityId.FileSettingsCheck && !verifiedFileSettingsReleaseApproved)
                return new RFiDGearCapability(definition.Id, definition.Name, RFiDGearCapabilityState.Unavailable,
                    "Implementierungsmarker gefunden, aber nicht freigegeben: RFiDGear 2.0.10 kann CheckFileSettings im rfPrj-Tasktyp nicht deserialisieren. Marker allein ist kein Ausführungsnachweis.");
            if (definition.Id == RFiDGearCapabilityId.VerifiedApplicationKeyChange && !verifiedKeyChangeReleaseApproved)
                return new RFiDGearCapability(definition.Id, definition.Name, RFiDGearCapabilityState.Unavailable,
                    verifiedKeyChangeReleaseEvidence ?? "Implementationsmarker vorhanden, aber der exakte Release-Build ist nicht freigegeben.");
            return new RFiDGearCapability(definition.Id, definition.Name, RFiDGearCapabilityState.Available,
                definition.Id == RFiDGearCapabilityId.VerifiedApplicationKeyChange
                    ? $"Nachgewiesen und exakt freigegeben: {string.Join(" + ", matching)}. {verifiedKeyChangeReleaseEvidence}"
                    : $"Nachgewiesen: {string.Join(" + ", matching)}");
        }).ToArray();
        return new(executablePath, version, inspectedFiles, capabilities);
    }

    public static string EvidenceFingerprint(IReadOnlyList<string> files, IReadOnlyList<byte[]> contents)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(contents);
        if (files.Count != contents.Count || files.Count == 0) throw new ArgumentException("RFiDGear-Evidenzdateien und Inhalte sind inkonsistent.");
        using var stream = new MemoryStream();
        foreach (var item in files.Select((path, index) => new { Name = Path.GetFileName(path).ToLowerInvariant(), Bytes = contents[index] })
                     .OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var name = Encoding.UTF8.GetBytes(item.Name);
            stream.Write(name);
            stream.WriteByte(0);
            stream.Write(SHA256.HashData(item.Bytes));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static IEnumerable<string> ResolveEvidenceFiles(string executable)
    {
        yield return executable;
        var sibling = Path.ChangeExtension(executable, ".dll");
        if (File.Exists(sibling)) yield return sibling;
        var legacySibling = Path.Combine(Path.GetDirectoryName(executable)!, "RFiDGear.dll");
        if (!string.Equals(legacySibling, sibling, StringComparison.OrdinalIgnoreCase) && File.Exists(legacySibling)) yield return legacySibling;
    }

    private static bool ContainsToken(byte[] bytes, string token)
        => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(token)) >= 0 ||
           bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(token)) >= 0;

    private static RFiDGearCapabilityReport Unknown(string executable, string error, RFiDGearVersionInfo? version = null, IReadOnlyList<string>? files = null)
    {
        version ??= new(executable, null, null, false);
        var capabilities = Definitions.Select(definition => new RFiDGearCapability(
            definition.Id, definition.Name, RFiDGearCapabilityState.Unknown, "Binärprüfung nicht möglich.")).ToArray();
        return new(executable, version, files ?? [], capabilities, error);
    }
}

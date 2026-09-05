using System.Security.Cryptography;
using System.Text.Json;

namespace RFiDGear.Builder.Runtime;

public sealed record ApplicationAcceptanceTaskEvidence(
    int ExecutionIndex, string TaskIndex, string TaskType, string Aid,
    string Outcome, string ErrorCode, string FileId = "");

public sealed record ApplicationAcceptanceEvidence(
    string SchemaVersion, string ProjectName, string ProjectFile, string ProjectSha256,
    string CompletedAt, string State, string TerminalEvent, string RunIdHash,
    IReadOnlyList<ApplicationAcceptanceTaskEvidence> Tasks,
    bool ContainsKeys, bool ContainsCardData);

public sealed class ApplicationAcceptanceEvidenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public ApplicationAcceptanceEvidence Create(
        string projectDirectory,
        RFiDGearOperationArtifact operation,
        RFiDGearHostRunV1 hostRun,
        RFiDGearHostResultV1 hostResult,
        DateTimeOffset? now = null)
    {
        if (operation.Kind != RFiDGearOperationKind.ApplicationAcceptanceProductive)
            throw new InvalidOperationException("Evidenz darf nur aus dem Application-Abnahmelauf erzeugt werden.");
        RFiDGearHostContractServiceV1.Validate(hostResult, hostRun.Request);
        if (hostRun.Request.Operation != "application-acceptance-productive")
            throw new InvalidDataException("Host-Resultat gehört nicht zur Application-Abnahme.");
        var root = Path.GetFullPath(projectDirectory);
        var project = Path.GetFullPath(operation.ProjectPath);
        EnsureChild(root, project);
        if (hostRun.Request.ProjectFile != Path.GetFileName(project) || hostRun.Request.ProjectFileSha256 != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(project))))
            throw new InvalidDataException("Host-Request gehört nicht zum aktuellen Application-Abnahmeprojekt.");
        var outcomes = hostResult.TaskOutcomes.Select(item =>
        {
            var task = operation.Tasks.Single(task => task.Position == item.Position);
            return new ApplicationAcceptanceTaskEvidence(item.Position, item.Index, item.Type, task.Aid, item.Outcome, item.ErrorCode, task.FileId);
        }).OrderBy(item => item.ExecutionIndex).ToArray();
        var complete = outcomes.Length == operation.Tasks.Count && outcomes.All(item => item.Outcome == "Successful");
        var successful = hostResult.Accepted && hostResult.State == "successful" && hostResult.AcceptanceState == "successful" &&
            hostResult.TerminalEvent == "TaskLoop.Success" && complete && hostResult.RunIdHash.Length == 64;
        var evidence = new ApplicationAcceptanceEvidence(
            "1.2", Path.GetFileName(project).Replace("_Acceptance_Application_Productive.rfPrj", "", StringComparison.Ordinal),
            Path.GetFileName(project), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(project))),
            (now ?? DateTimeOffset.Now).ToString("O"), successful ? "successful" : hostResult.State == "timedout" ? "timed-out" : "failed",
            hostResult.TerminalEvent, hostResult.RunIdHash, outcomes, false, false);
        Validate(evidence, root);
        var directory = Path.Combine(root, "acceptance");
        Directory.CreateDirectory(directory);
        WriteAtomic(Path.Combine(directory, "application-settings.result.json"), evidence);
        return evidence;
    }

    public static ApplicationAcceptanceEvidence LoadAndValidate(string projectDirectory, string evidencePath)
    {
        var root = Path.GetFullPath(projectDirectory);
        var path = Path.GetFullPath(evidencePath);
        EnsureChild(root, path);
        var evidence = JsonSerializer.Deserialize<ApplicationAcceptanceEvidence>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Application-Abnahmeevidenz ist leer.");
        Validate(evidence, root);
        return evidence;
    }

    private static void Validate(ApplicationAcceptanceEvidence evidence, string root)
    {
        if (evidence.SchemaVersion is not ("1.1" or "1.2") || evidence.ContainsKeys || evidence.ContainsCardData)
            throw new InvalidDataException("Application-Abnahmeevidenz verletzt Version oder Datenschutzvertrag.");
        if (Path.GetFileName(evidence.ProjectFile) != evidence.ProjectFile) throw new InvalidDataException("Abnahmeprojektpfad ist nicht projektlokal.");
        var project = Path.Combine(root, evidence.ProjectFile);
        if (!File.Exists(project) || !string.Equals(evidence.ProjectSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(project))), StringComparison.Ordinal))
            throw new InvalidDataException("Application-Abnahmeevidenz gehört nicht zum aktuellen rfPrj-Artefakt.");
        if (evidence.Tasks.Any(task => task.TaskType is not ("ReadAppSettings" or "CheckAppKeyCount" or "CheckFileSettings")) ||
            evidence.Tasks.Select(task => task.ExecutionIndex).Distinct().Count() != evidence.Tasks.Count)
            throw new InvalidDataException("Application-Abnahmeevidenz enthält unerwartete oder doppelte Tasks.");
        if (evidence.Tasks.Any(task => task.TaskType == "CheckFileSettings" && string.IsNullOrWhiteSpace(task.FileId)))
            throw new InvalidDataException("FileSettings-Abnahmeevidenz enthält keine File-ID.");
        if (evidence.State == "successful" && (evidence.TerminalEvent != "TaskLoop.Success" || evidence.RunIdHash.Length != 64 || evidence.Tasks.Count == 0 || evidence.Tasks.Any(task => task.Outcome != "Successful")))
            throw new InvalidDataException("Application-Abnahmeevidenz meldet Erfolg ohne vollständige RFiDGear-Taskresultate.");
    }

    private static void EnsureChild(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Application-Abnahmepfad verlässt den Projektordner.");
    }

    private static void WriteAtomic(string path, ApplicationAcceptanceEvidence evidence)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(evidence, JsonOptions)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

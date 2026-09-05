using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.Runtime;

public sealed record PayloadPreparationState(
    string SchemaVersion,
    string State,
    DateTimeOffset ChangedAt,
    DateTimeOffset? PreparedAt,
    IReadOnlyList<string> Outputs,
    bool ContainsCardData,
    string Reason);

public sealed record PayloadInvalidationResult(PayloadPreparationState State, int RemovedPayloads, string StatePath);

public sealed class PayloadPreparationStateService
{
    public const string StateFileName = ".rfidgear-payload-state.json";

    public PayloadInvalidationResult Invalidate(CodingSessionDefinition definition, string reason)
    {
        ArgumentNullException.ThrowIfNull(definition);
        using var lease = WorkspaceLease.Acquire(definition.ProjectDirectory, "Payload-Sperrzustand");
        return InvalidateUnderHeldLease(definition, reason);
    }

    internal PayloadInvalidationResult InvalidateUnderHeldLease(CodingSessionDefinition definition, string reason)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var outputs = Outputs(definition);
        var payloadRoot = PayloadRoot(definition.ProjectDirectory);
        Directory.CreateDirectory(payloadRoot);
        var removed = 0;
        foreach (var output in outputs)
        {
            var path = SafeOutput(payloadRoot, output);
            if (!File.Exists(path)) continue;
            File.Delete(path);
            removed++;
        }
        var state = new PayloadPreparationState("1.0", "NotPrepared", DateTimeOffset.Now, null, outputs, false,
            string.IsNullOrWhiteSpace(reason) ? "Neue Kartendaten wurden noch nicht bewusst bereitgestellt." : reason);
        var statePath = WriteState(payloadRoot, state);
        return new(state, removed, statePath);
    }

    internal PayloadPreparationState MarkPreparedUnderHeldLease(CodingSessionDefinition definition, DateTimeOffset preparedAt)
    {
        var state = new PayloadPreparationState("1.0", "Prepared", DateTimeOffset.Now, preparedAt, Outputs(definition), true,
            "Payloads wurden für genau diesen Sitzungsdatensatz vollständig und atomar bereitgestellt.");
        WriteState(PayloadRoot(definition.ProjectDirectory), state);
        return state;
    }

    public PayloadPreparationState RequirePrepared(PreparedCodingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var path = Path.Combine(PayloadRoot(session.Definition.ProjectDirectory), StateFileName);
        if (!File.Exists(path)) throw new InvalidDataException("Payload-Sicherheitsstatus fehlt. RFiDGear wird nicht gestartet; Datensatz neu bereitstellen.");
        PayloadPreparationState state;
        try
        {
            state = JsonSerializer.Deserialize<PayloadPreparationState>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Payload-Sicherheitsstatus ist leer.");
        }
        catch (JsonException exception) { throw new InvalidDataException("Payload-Sicherheitsstatus ist ungültig.", exception); }
        var expectedOutputs = Outputs(session.Definition);
        if (!string.Equals(state.State, "Prepared", StringComparison.Ordinal) || state.PreparedAt != session.PreparedAt || !state.ContainsCardData ||
            !state.Outputs.SequenceEqual(expectedOutputs, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Payload-Sicherheitsstatus gehört nicht zur aktiven Sitzung. RFiDGear wird nicht gestartet; Datensatz neu bereitstellen.");
        return state;
    }

    private static IReadOnlyList<string> Outputs(CodingSessionDefinition definition)
        => definition.Profiles.Applications.SelectMany(application => application.Files)
            .Where(file => file.Coding is not null).Select(file => file.Coding!.Output)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string PayloadRoot(string projectDirectory)
        => Path.GetFullPath(Path.Combine(projectDirectory, "payloads"));

    private static string SafeOutput(string payloadRoot, string output)
    {
        if (Path.IsPathRooted(output)) throw new InvalidDataException("Payloadziel muss relativ sein: " + output);
        var root = payloadRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, output));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Payloadziel verlässt den Payloadordner: " + output);
        return path;
    }

    private static string WriteState(string payloadRoot, PayloadPreparationState state)
    {
        Directory.CreateDirectory(payloadRoot);
        var path = Path.Combine(payloadRoot, StateFileName);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporary, path, true);
            return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>Typed project-key integration. Binding changes are explicit; legacy vaults are never implicitly migrated.</summary>
public static class ProjectGroupVault
{
    // UI/catalog protection family marker, not the group's on-disk format version (which is 1).
    public const int ProtectionLevel = 4;
    public static bool IsGroupVaultFile(string path)
    {
        if (!File.Exists(path)) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GroupVaultService.MaximumBytes) throw new InvalidDataException("Vault-Dateigröße ist ungültig.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.TryGetProperty("Header", out var header) && header.ValueKind == JsonValueKind.Object &&
            header.TryGetProperty("Format", out var format) && format.GetString() == GroupVaultService.Format;
    }
    private sealed record Payload(string Format, int Version, ProjectKeyVaultPayload Keys);
    private const string PayloadFormat = "SieEncode-Group-Project-Keys";
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };

    public static ProjectDefinition Bind(ProjectDefinition source, OfflineGroupTrustStore trust, string groupId)
    {
        var current = trust.LoadOffline();
        if (current.Policy.Version != 2 || !current.Policy.Groups.Any(g => g.Id == groupId))
            throw new InvalidDataException("Ausgewählte Gruppe fehlt im registrierten Personenverzeichnis.");
        var project = Clone(source);
        var binding = new ProjectGroupVaultBinding(current.Policy.LibraryId, groupId);
        if (project.Project.GroupVault is not null && project.Project.GroupVault != binding)
            throw new InvalidOperationException("Bestehende Gruppenzuordnung darf nicht ohne kontrollierte Vault-Umzuordnung ersetzt werden.");
        project.Project.Id ??= Guid.NewGuid();
        project.Project.GroupVault = binding;
        ValidateBinding(project, current);
        return project;
    }

    public static FileRevision Create(string projectPath, ProjectDefinition source, ProjectKeyVaultPayload keys, OfflineGroupTrustStore trust,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now, bool recovery = false)
    {
        var project = Clone(source); var current = trust.LoadOffline();
        var binding = ValidateBinding(project, current);
        var path = ProjectKeyVault.GetPath(projectPath);
        if (ConcurrentFileWriter.Capture(path) != FileRevision.Missing) throw new ConcurrentFileChangeException(path);
        var payload = SerializePayload(keys);
        var document = GroupVaultService.Protect(payload, current, project.Project.Id!.Value, binding.GroupId, now);
        // Prove the creator's configured access before persisting a vault they cannot reopen.
        _ = ParsePayload(GroupVaultService.Unprotect(document, current, project.Project.Id.Value, binding.GroupId, identities, recovery));
        var bytes = GroupVaultService.Write(document);
        return ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
        {
            if (trust.LoadOffline().Checkpoint != current.Checkpoint) throw new CryptographicException("Gruppenstand während der Vault-Erstellung geändert.");
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes); stream.Flush(true);
        });
    }

    public static ProjectKeyVaultPayload Load(string projectPath, ProjectDefinition source, OfflineGroupTrustStore trust,
        IEnumerable<X509Certificate2> identities, bool recovery = false)
    {
        var project = Clone(source); var current = trust.LoadOffline();
        var binding = ValidateBinding(project, current);
        using var stream = new FileStream(ProjectKeyVault.GetPath(projectPath), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GroupVaultService.MaximumBytes) throw new InvalidDataException("Gruppen-Vault ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var document = GroupVaultService.Read(bytes);
        return ParsePayload(GroupVaultService.Unprotect(document, current, project.Project.Id!.Value, binding.GroupId, identities, recovery));
    }

    public static GroupVaultReconciliationResult Update(string projectPath, ProjectDefinition source, ProjectKeyVaultPayload keys,
        OfflineGroupTrustStore trust, IEnumerable<X509Certificate2> identities, FileRevision expected, DateTimeOffset now,
        bool recovery = false, string? privateBackupRoot = null)
    {
        var project = Clone(source); var current = trust.LoadOffline();
        var binding = ValidateBinding(project, current);
        var certificates = identities.ToArray();
        var path = ProjectKeyVault.GetPath(projectPath);
        var plan = GroupVaultReconciliation.Preview(path, trust, project.Project.Id!.Value, binding.GroupId, certificates, now, recovery);
        if (plan.VaultRevision != expected) throw new ConcurrentFileChangeException(path);
        if (plan.Added.Count != 0 || plan.Removed.Count != 0)
            throw new InvalidOperationException("Gruppenempfänger haben sich geändert. Bitte zuerst den kontrollierten Empfängerabgleich bestätigen.");
        // Reject a different application payload before overwriting it, even if its envelope is valid.
        _ = Load(projectPath, project, trust, certificates, recovery);
        return GroupVaultReconciliation.ApplyPayload(plan, trust, certificates, now, SerializePayload(keys), privateBackupRoot);
    }

    private static ProjectGroupVaultBinding ValidateBinding(ProjectDefinition project, VerifiedGroupPolicy current)
    {
        var binding = project.Project.GroupVault;
        if (project.Project.Id is null || project.Project.Id == Guid.Empty || binding is null || binding.LibraryId != current.Policy.LibraryId ||
            binding.GroupId is not { Length: > 0 and <= 128 } || !binding.GroupId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new CryptographicException("Stabile Projekt-ID oder passende Bibliotheks-/Gruppenzuordnung fehlt.");
        return binding;
    }

    private static ProjectDefinition Clone(ProjectDefinition source)
        => JsonSerializer.Deserialize<ProjectDefinition>(JsonSerializer.Serialize(source, ProjectJson.Options), ProjectJson.Options)
            ?? throw new InvalidDataException("Projektdefinition fehlt.");

    private static string SerializePayload(ProjectKeyVaultPayload keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.TemporaryLibraryPayload)
            throw new InvalidOperationException("Temporäre Bibliotheksschlüssel dürfen nicht in einem separaten Gruppen-Projekt-Vault gespeichert werden.");
        var json = JsonSerializer.Serialize(new Payload(PayloadFormat, 1, keys), Options);
        return JsonSerializer.Serialize(new Payload(PayloadFormat, 1, ParsePayload(json)), Options);
    }

    private static ProjectKeyVaultPayload ParsePayload(string json)
    {
        using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 })) OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        var payload = JsonSerializer.Deserialize<Payload>(json, Options);
        if (payload is null || payload.Format != PayloadFormat || payload.Version != 1 || payload.Keys is null)
            throw new InvalidDataException("Unbekannte Projekt-Schlüsselnutzdaten.");
        ProjectKeyVault.Normalize(payload.Keys); return payload.Keys;
    }
}

using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed class ProjectKeyVaultPayload
{
    public string SelectedInitialKeySetId { get; set; } = "";
    public List<InitialKeySet> InitialKeySets { get; set; } = [];
    public KeyReference TransportPicc { get; set; } = new();
    public Dictionary<string, string> TransportApplicationKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public KeyReference ProductivePicc { get; set; } = new();
    public string PiccKeySettings { get; set; } = "0F";
    public Dictionary<string, List<string>> ApplicationKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class InitialKeySet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public KeyReference PiccKey { get; set; } = new();
    public Dictionary<string, string> ApplicationKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ProjectKeyVaultAuditResult(
    string Status,
    string Format,
    int Version,
    string Kdf,
    int Iterations,
    string FileFingerprint,
    int ApplicationCount,
    int KeyCount,
    bool PortableAcrossUsers,
    IReadOnlyList<string> Findings);

public static class ProjectKeyVault
{
    public static string GetPath(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fullPath));
        return Path.Combine(Path.GetDirectoryName(fullPath)!, name + ".keys.vault.json");
    }

    public static ProjectKeyVaultPayload FromProject(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var id = string.IsNullOrWhiteSpace(project.Transport.KeySetId) ? "standard" : project.Transport.KeySetId;
        var payload = new ProjectKeyVaultPayload
        {
            SelectedInitialKeySetId = id,
            TransportPicc = new KeyReference { Type = project.Transport.PiccKey.Type, Value = project.Transport.PiccKey.Value },
            TransportApplicationKeys = project.Transport.ApplicationKeys.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            ProductivePicc = new KeyReference { Type = project.Productive.PiccKey.Type, Value = project.Productive.PiccKey.Value },
            PiccKeySettings = project.Productive.PiccKeySettings,
            ApplicationKeys = project.Productive.ApplicationKeys.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase)
        };
        payload.InitialKeySets.Add(CreateInitialKeySet(id, id == "standard" ? "Standard / Blankokarte" : id, project));
        return payload;
    }

    public static ProjectKeyVaultPayload UpdateFromProject(ProjectDefinition project, ProjectKeyVaultPayload? existing)
    {
        ArgumentNullException.ThrowIfNull(project);
        var payload = existing ?? FromProject(project);
        Normalize(payload);
        var id = string.IsNullOrWhiteSpace(project.Transport.KeySetId) ? payload.SelectedInitialKeySetId : project.Transport.KeySetId;
        if (string.IsNullOrWhiteSpace(id)) id = "standard";
        var current = payload.InitialKeySets.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            current = CreateInitialKeySet(id, id == "standard" ? "Standard / Blankokarte" : id, project);
            payload.InitialKeySets.Add(current);
        }
        else
        {
            current.PiccKey = Copy(project.Transport.PiccKey);
            current.ApplicationKeys = Copy(project.Transport.ApplicationKeys);
        }
        payload.SelectedInitialKeySetId = current.Id;
        payload.TransportPicc = Copy(current.PiccKey);
        payload.TransportApplicationKeys = Copy(current.ApplicationKeys);
        payload.ProductivePicc = Copy(project.Productive.PiccKey);
        payload.PiccKeySettings = project.Productive.PiccKeySettings;
        payload.ApplicationKeys = project.Productive.ApplicationKeys.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        return payload;
    }

    public static void Normalize(ProjectKeyVaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        payload.InitialKeySets ??= [];
        if (payload.InitialKeySets.Count == 0 && (!string.IsNullOrWhiteSpace(payload.TransportPicc.Value) || payload.TransportApplicationKeys.Count > 0))
            payload.InitialKeySets.Add(new InitialKeySet
            {
                Id = "legacy-default",
                Name = "Bisheriger Ausgangsschlüsselsatz",
                PiccKey = Copy(payload.TransportPicc),
                ApplicationKeys = Copy(payload.TransportApplicationKeys)
            });
        foreach (var item in payload.InitialKeySets)
        {
            item.Id = item.Id.Trim();
            item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name.Trim();
            item.ApplicationKeys = Copy(item.ApplicationKeys);
        }
        if (payload.InitialKeySets.Select(item => item.Id).Any(string.IsNullOrWhiteSpace) ||
            payload.InitialKeySets.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidDataException("Ausgangsschlüsselsätze benötigen eindeutige IDs.");
        if (payload.InitialKeySets.Count > 0 && !payload.InitialKeySets.Any(item => string.Equals(item.Id, payload.SelectedInitialKeySetId, StringComparison.OrdinalIgnoreCase)))
            payload.SelectedInitialKeySetId = payload.InitialKeySets[0].Id;
    }

    public static ProjectDefinition Apply(ProjectDefinition project, ProjectKeyVaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(payload);
        var clone = JsonSerializer.Deserialize<ProjectDefinition>(JsonSerializer.Serialize(project, ProjectJson.Options), ProjectJson.Options)
            ?? throw new InvalidDataException("Projektdefinition konnte nicht kopiert werden.");
        Normalize(payload);
        var requested = string.IsNullOrWhiteSpace(clone.Transport.KeySetId) ? payload.SelectedInitialKeySetId : clone.Transport.KeySetId;
        var initial = payload.InitialKeySets.FirstOrDefault(item => string.Equals(item.Id, requested, StringComparison.OrdinalIgnoreCase))
            ?? payload.InitialKeySets.FirstOrDefault();
        if (initial is not null)
        {
            clone.Transport.KeySetId = initial.Id;
            clone.Transport.PiccKey = Copy(initial.PiccKey);
            clone.Transport.ApplicationKeys = Copy(initial.ApplicationKeys);
            payload.SelectedInitialKeySetId = initial.Id;
            payload.TransportPicc = Copy(initial.PiccKey);
            payload.TransportApplicationKeys = Copy(initial.ApplicationKeys);
        }
        clone.Productive.PiccKey = new KeyReference { Type = payload.ProductivePicc.Type, Value = payload.ProductivePicc.Value };
        clone.Productive.PiccKeySettings = payload.PiccKeySettings;
        clone.Productive.ApplicationKeys = payload.ApplicationKeys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
        return clone;
    }

    private static InitialKeySet CreateInitialKeySet(string id, string name, ProjectDefinition project) => new()
    {
        Id = id,
        Name = name,
        PiccKey = Copy(project.Transport.PiccKey),
        ApplicationKeys = Copy(project.Transport.ApplicationKeys)
    };

    private static KeyReference Copy(KeyReference source) => new() { Type = source.Type, Value = source.Value };
    private static Dictionary<string, string> Copy(Dictionary<string, string> source)
        => source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    public static void Save(string path, ProjectKeyVaultPayload payload, string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(payload);
        var payloadJson = JsonSerializer.Serialize(payload, ProjectJson.Options);
        var vault = KeyVaultService.Protect(payloadJson, passphrase);
        AtomicSave(path, JsonSerializer.Serialize(vault, ProjectJson.Options));
    }

    public static FileRevision Save(string path, ProjectKeyVaultPayload payload, string passphrase, FileRevision expected)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var payloadJson = JsonSerializer.Serialize(payload, ProjectJson.Options);
        var vault = KeyVaultService.Protect(payloadJson, passphrase);
        var content = JsonSerializer.Serialize(vault, ProjectJson.Options);
        return ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(content); writer.Flush(); stream.Flush(true);
        });
    }

    public static FileRevision SaveForRecipients(string path, ProjectKeyVaultPayload payload, string recoveryPassphrase,
        IEnumerable<(X509Certificate2 Certificate, string DisplayName)> recipients, FileRevision expected)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var payloadJson = JsonSerializer.Serialize(payload, ProjectJson.Options);
        var vault = CertificateVaultService.Protect(payloadJson, recoveryPassphrase, recipients);
        var content = JsonSerializer.Serialize(vault, ProjectJson.Options);
        return ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(content); writer.Flush(); stream.Flush(true);
        });
    }

    public static FileRevision UpdateForExistingRecipients(string path, ProjectKeyVaultPayload payload,
        IEnumerable<X509Certificate2> certificates, FileRevision expected)
    {
        ArgumentNullException.ThrowIfNull(payload); ArgumentNullException.ThrowIfNull(certificates);
        if (ConcurrentFileWriter.Capture(path) != expected) throw new ConcurrentFileChangeException(path);
        var existing = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault ist leer.");
        var payloadJson = JsonSerializer.Serialize(payload, ProjectJson.Options);
        var updated = CertificateVaultService.ReprotectWithCertificate(existing, payloadJson, certificates);
        var content = JsonSerializer.Serialize(updated, ProjectJson.Options);
        return ConcurrentFileWriter.Write(path, expected, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(content); writer.Flush(); stream.Flush(true);
        });
    }

    public static ProjectKeyVaultPayload Load(string path, string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var vault = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault ist leer.");
        var json = KeyVaultService.Unprotect(vault, passphrase);
        return JsonSerializer.Deserialize<ProjectKeyVaultPayload>(json, ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault-Payload ist leer.");
    }

    public static ProjectKeyVaultPayload LoadWithCertificates(string path, IEnumerable<X509Certificate2> certificates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var vault = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault ist leer.");
        var json = CertificateVaultService.UnprotectWithCertificate(vault, certificates);
        return JsonSerializer.Deserialize<ProjectKeyVaultPayload>(json, ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault-Payload ist leer.");
    }

    public static ProjectKeyVaultAuditResult Audit(string path, string passphrase, ProjectDefinition keyFreeProject, ProfileLibrary library)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(keyFreeProject);
        ArgumentNullException.ThrowIfNull(library);
        var bytes = File.ReadAllBytes(path);
        var vault = JsonSerializer.Deserialize<KeyVaultDocument>(bytes, ProjectJson.Options)
            ?? throw new InvalidDataException("Key-Vault ist leer.");
        var payload = Load(path, passphrase);
        var hydrated = Apply(ProjectJson.WithoutProductiveSecrets(keyFreeProject), payload);
        var findings = ProjectContractValidator.Validate(hydrated, library, requireProductiveSecrets: true)
            .Where(issue => issue.Code.StartsWith("PRODUCTIVE", StringComparison.Ordinal) || issue.Code.StartsWith("PROJECT_", StringComparison.Ordinal))
            .Select(issue => $"{issue.Code}: {issue.Message}")
            .ToArray();
        var keyCount = (string.IsNullOrWhiteSpace(payload.TransportPicc.Value) ? 0 : 1) +
                       (string.IsNullOrWhiteSpace(payload.ProductivePicc.Value) ? 0 : 1) +
                       payload.TransportApplicationKeys.Values.Count(value => !string.IsNullOrWhiteSpace(value)) +
                       payload.ApplicationKeys.Sum(pair => pair.Value.Count(value => !string.IsNullOrWhiteSpace(value)));
        return new ProjectKeyVaultAuditResult(
            findings.Length == 0 ? "OK" : "FEHLER",
            vault.Format,
            vault.Version,
            vault.Kdf,
            vault.Iterations,
            Convert.ToHexString(SHA256.HashData(bytes))[..12],
            payload.ApplicationKeys.Count,
            keyCount,
            true,
            findings);
    }

    private static void AtomicSave(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporaryPath, fullPath, true);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }
}

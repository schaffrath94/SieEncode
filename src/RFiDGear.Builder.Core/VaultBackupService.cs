using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class VaultBackupManifest
{
    public string SchemaVersion { get; set; } = "1.0";
    public bool ContainsKeys { get; set; }
    public string ProjectName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string ProjectFile { get; set; } = "";
    public string VaultFile { get; set; } = "";
    public string ProjectSha256 { get; set; } = "";
    public string VaultSha256 { get; set; } = "";
    public string KeySetIdentity { get; set; } = "";
    public string RecoveryInstruction { get; set; } = "Projektdefinition und verschlüsselten Vault gemeinsam in die Projektbibliothek zurückkopieren und den Vault mit einer freigegebenen Windows-Identität oder der Recovery-Passphrase öffnen.";
}

public sealed record VaultBackupResult(string Directory, string ProjectPath, string VaultPath, string ManifestPath,
    string ProjectSha256, string VaultSha256, string KeySetIdentity);

public static class KeySetIdentityService
{
    public static string Compute(ProjectKeyVaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var canonical = new StringBuilder("SieEncode-KeySetIdentity-v1\n");
        canonical.Append("TRANSPORT-PICC|").Append(payload.TransportPicc.Type.Trim().ToUpperInvariant()).Append('|')
            .Append(Normalize(payload.TransportPicc.Value)).Append('\n');
        foreach (var application in payload.TransportApplicationKeys.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase))
            canonical.Append("TRANSPORT-APP|").Append(application.Key.Trim().ToUpperInvariant()).Append('|')
                .Append(Normalize(application.Value)).Append('\n');
        canonical.Append("PICC|").Append(payload.ProductivePicc.Type.Trim().ToUpperInvariant()).Append('|')
            .Append(Normalize(payload.ProductivePicc.Value)).Append('\n');
        foreach (var application in payload.ApplicationKeys.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase))
        {
            canonical.Append("APP|").Append(application.Key.Trim().ToUpperInvariant());
            for (var index = 0; index < application.Value.Count; index++)
                canonical.Append('|').Append(index).Append(':').Append(Normalize(application.Value[index]));
            canonical.Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string Normalize(string value)
    {
        var normalized = (value ?? "").Trim();
        if (normalized.Any(ch => !Uri.IsHexDigit(ch)))
            throw new InvalidDataException("Die Keyset-Identität kann nur aus gültigen hexadezimalen Schlüsselwerten gebildet werden.");
        return normalized.ToUpperInvariant();
    }
}

public sealed class VaultBackupService
{
    public VaultBackupResult Create(string projectPath, string vaultPath, string backupRoot, ProjectKeyVaultPayload verifiedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);
        ArgumentNullException.ThrowIfNull(verifiedPayload);

        var sourceProject = Path.GetFullPath(projectPath);
        var sourceVault = Path.GetFullPath(vaultPath);
        var root = Path.GetFullPath(backupRoot);
        if (!File.Exists(sourceProject)) throw new FileNotFoundException("Projektdefinition für Vault-Sicherung fehlt.", sourceProject);
        if (!File.Exists(sourceVault)) throw new FileNotFoundException("Verifizierter Vault für Sicherung fehlt.", sourceVault);
        var keyFreeProject = ProjectJson.Load(sourceProject);
        if (ContainsSecrets(keyFreeProject)) throw new InvalidDataException("Projektdefinition der Vault-Sicherung enthält Schlüsselwerte.");

        var projectHash = Hash(sourceProject);
        var vaultHash = Hash(sourceVault);
        var identity = KeySetIdentityService.Compute(verifiedPayload);
        var safeName = string.Concat(keyFreeProject.Project.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Projekt";
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss_fff");
        var destination = Path.Combine(root, safeName, $"{stamp}_{vaultHash[..12]}");
        Directory.CreateDirectory(destination);

        var projectCopy = Path.Combine(destination, safeName + ".project.json");
        var vaultCopy = Path.Combine(destination, safeName + ".keys.vault.json");
        File.Copy(sourceProject, projectCopy, overwrite: false);
        File.Copy(sourceVault, vaultCopy, overwrite: false);
        if (Hash(projectCopy) != projectHash || Hash(vaultCopy) != vaultHash)
            throw new CryptographicException("Vault-Sicherung stimmt nach dem Kopieren nicht mit der verifizierten Quelle überein.");

        var manifest = new VaultBackupManifest
        {
            ContainsKeys = false,
            ProjectName = keyFreeProject.Project.Name,
            CreatedAt = DateTimeOffset.Now.ToString("O"),
            ProjectFile = Path.GetFileName(projectCopy),
            VaultFile = Path.GetFileName(vaultCopy),
            ProjectSha256 = projectHash,
            VaultSha256 = vaultHash,
            KeySetIdentity = identity
        };
        var manifestPath = Path.Combine(destination, "RECOVERY-MANIFEST.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
        return new(destination, projectCopy, vaultCopy, manifestPath, projectHash, vaultHash, identity);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool ContainsSecrets(ProjectDefinition project)
        => !string.IsNullOrWhiteSpace(project.Transport.PiccKey.Value) ||
           project.Transport.ApplicationKeys.Values.Any(value => !string.IsNullOrWhiteSpace(value)) ||
           !string.IsNullOrWhiteSpace(project.Productive.PiccKey.Value) ||
           project.Productive.ApplicationKeys.Values.SelectMany(values => values).Any(value => !string.IsNullOrWhiteSpace(value));
}

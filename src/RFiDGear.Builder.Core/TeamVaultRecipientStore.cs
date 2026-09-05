using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public sealed class TeamVaultRecipientDocument
{
    public string Format { get; set; } = "SieEncode-Team-Vault-Recipients";
    public int Version { get; set; } = 1;
    public List<VaultRecipientInvitation> Recipients { get; set; } = [];
}

public sealed class TeamVaultRecipientStore
{
    private readonly string path;
    private FileRevision revision;

    public TeamVaultRecipientStore(string projectsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsDirectory);
        path = System.IO.Path.Combine(System.IO.Path.GetFullPath(projectsDirectory), ".sieencode", "team-vault-recipients.json");
        revision = ConcurrentFileWriter.Capture(path);
    }

    public string Path => path;

    public TeamVaultRecipientDocument Load()
    {
        if (!File.Exists(path)) { revision = FileRevision.Missing; return new TeamVaultRecipientDocument(); }
        var document = JsonSerializer.Deserialize<TeamVaultRecipientDocument>(File.ReadAllText(path), ProjectJson.Options)
            ?? throw new InvalidDataException("Team-Empfängerliste ist leer.");
        if (document.Format != "SieEncode-Team-Vault-Recipients" || document.Version != 1)
            throw new InvalidDataException("Unbekanntes Format der Team-Empfängerliste.");
        Validate(document.Recipients);
        revision = ConcurrentFileWriter.Capture(path);
        return document;
    }

    public void Save(IEnumerable<VaultRecipientInvitation> recipients)
    {
        var rows = recipients.OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        Validate(rows);
        var document = new TeamVaultRecipientDocument { Recipients = rows };
        revision = ConcurrentFileWriter.Write(path, revision, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, document, ProjectJson.Options);
            stream.Flush(true);
        });
    }

    private static void Validate(IEnumerable<VaultRecipientInvitation> recipients)
    {
        var fingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var invitation in recipients)
        {
            var imported = CertificateVaultService.ImportInvitation(invitation);
            using (imported.Certificate)
                if (!fingerprints.Add(invitation.Fingerprint)) throw new InvalidDataException("Team-Empfänger ist doppelt vorhanden.");
        }
    }
}

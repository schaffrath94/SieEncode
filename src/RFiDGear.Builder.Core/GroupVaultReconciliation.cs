using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record GroupVaultAccess(string Role, string Fingerprint);
public sealed class GroupVaultReconciliationPlan
{
    internal readonly byte[] Source;
    internal readonly string Path;
    internal readonly Guid ProjectId;
    internal readonly string GroupId;
    internal readonly bool Recovery;
    public FileRevision VaultRevision { get; }
    public GroupPolicyCheckpoint PolicyCheckpoint { get; }
    public IReadOnlyList<GroupVaultAccess> Added { get; }
    public IReadOnlyList<GroupVaultAccess> Removed { get; }

    internal GroupVaultReconciliationPlan(string path, byte[] source, Guid projectId, string groupId, bool recovery,
        GroupPolicyCheckpoint checkpoint, GroupVaultAccess[] added, GroupVaultAccess[] removed)
    {
        Path = path; Source = source.ToArray(); ProjectId = projectId; GroupId = groupId; Recovery = recovery;
        VaultRevision = new(true, Convert.ToHexString(SHA256.HashData(Source)));
        PolicyCheckpoint = checkpoint; Added = Array.AsReadOnly(added); Removed = Array.AsReadOnly(removed);
    }
}
public sealed record GroupVaultReconciliationResult(FileRevision Revision, Guid KeyGeneration, string EncryptedBackupPath,
    GroupPolicyCheckpoint AppliedPolicy);

/// <summary>Authenticated preview and guarded fresh-key rotation; no arbitrary recipient list supplied by the caller.</summary>
public static class GroupVaultReconciliation
{
    public static GroupVaultReconciliationPlan Preview(string path, OfflineGroupTrustStore trust, Guid projectId, string groupId,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now, bool recovery = false)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var bytes = ReadFile(fullPath);
        var document = GroupVaultService.Read(bytes);
        var current = trust.LoadOffline();
        _ = GroupVaultService.Unprotect(document, current, projectId, groupId, identities, recovery);
        var target = ResolveTarget(current.Policy, groupId, now);
        var previous = document.Header.Recipients.Select(r => new GroupVaultAccess(r.Role, r.Fingerprint)).ToArray();
        return new(fullPath, bytes, projectId, groupId, recovery, current.Checkpoint, target.Except(previous).ToArray(), previous.Except(target).ToArray());
    }

    public static GroupVaultReconciliationResult Apply(GroupVaultReconciliationPlan plan, OfflineGroupTrustStore trust,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now)
        => ApplyPayload(plan, trust, identities, now, null);

    public static GroupVaultReconciliationResult ApplyPayload(GroupVaultReconciliationPlan plan, OfflineGroupTrustStore trust,
        IEnumerable<X509Certificate2> identities, DateTimeOffset now, string? replacementPayloadJson)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (ConcurrentFileWriter.Capture(plan.Path) != plan.VaultRevision) throw new ConcurrentFileChangeException(plan.Path);
        var current = trust.LoadOffline();
        if (current.Checkpoint != plan.PolicyCheckpoint) throw new CryptographicException("Gruppenstand seit der Vorschau geändert. Bitte neu prüfen.");
        var old = GroupVaultService.Read(plan.Source);
        var payload = GroupVaultService.Unprotect(old, current, plan.ProjectId, plan.GroupId, identities, plan.Recovery);
        var next = GroupVaultService.Protect(replacementPayloadJson ?? payload, current, plan.ProjectId, plan.GroupId, now);
        var bytes = GroupVaultService.Write(next);
        var backup = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(plan.Path)!, "GroupVaultBackups", plan.VaultRevision.Sha256 + ".group-vault.json");
        var revision = ConcurrentFileWriter.Write(plan.Path, plan.VaultRevision, temporary =>
        {
            if (trust.LoadOffline().Checkpoint != plan.PolicyCheckpoint) throw new CryptographicException("Gruppenstand während der Rotation geändert. Bitte neu prüfen.");
            PreserveBackup(backup, plan);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (trust.LoadOffline().Checkpoint != plan.PolicyCheckpoint) throw new CryptographicException("Gruppenstand während der Rotation geändert. Bitte neu prüfen.");
        });
        return new(revision, next.Header.KeyGeneration, backup, current.Checkpoint);
    }

    private static void PreserveBackup(string path, GroupVaultReconciliationPlan plan)
    {
        if (File.Exists(path))
        {
            if (ConcurrentFileWriter.Capture(path) != plan.VaultRevision) throw new CryptographicException("Vorhandene verschlüsselte Vault-Sicherung hat einen abweichenden Inhalt.");
            return;
        }
        ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(plan.Source); stream.Flush(true);
        });
        if (ConcurrentFileWriter.Capture(path) != plan.VaultRevision) throw new CryptographicException("Vault-Sicherung konnte nicht unverändert bestätigt werden.");
    }

    private static GroupVaultAccess[] ResolveTarget(OfflineGroupPolicy policy, string groupId, DateTimeOffset now)
    {
        var members = OfflineGroupDirectory.ResolveRecipients(policy, groupId, now).Select(r => new GroupVaultAccess("member", r.Fingerprint));
        var recovery = policy.RecoveryDevices.Where(d => d.Enabled).ToArray();
        if (recovery.Length == 0) throw new InvalidOperationException("Aktiver separater Recovery-Zugang fehlt.");
        foreach (var device in recovery)
        {
            using var certificate = OfflineGroupDirectory.ReadPublicCertificate(device.Certificate);
            if (now.UtcDateTime < certificate.NotBefore.ToUniversalTime() || now.UtcDateTime >= certificate.NotAfter.ToUniversalTime())
                throw new CryptographicException("Aktiver Recovery-Zugang ist noch nicht gültig oder abgelaufen.");
        }
        return members.Concat(recovery.Select(d => new GroupVaultAccess("recovery", d.Fingerprint))).ToArray();
    }

    private static byte[] ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GroupVaultService.MaximumBytes) throw new InvalidDataException("Gruppen-Vault ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
}

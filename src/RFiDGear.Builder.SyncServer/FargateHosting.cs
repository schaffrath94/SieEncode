namespace RFiDGear.Builder.SyncServer;

/// <summary>Fail closed for the planned Linux Fargate/EFS deployment.
/// The mount type check detects accidental ephemeral storage, not AWS
/// identity, access-point permissions, backup freshness or distributed fencing.</summary>
internal static class FargateHosting
{
    internal const string MountPath = "/var/lib/sieencode";
    internal const string StatePath = MountPath + "/live/central-state.json";

    internal static void Validate(string? hosting, string stateFile, string mode)
    {
        if (string.IsNullOrEmpty(hosting) || hosting == "local") return;
        if (hosting is not ("fargate" or "fargate-maintenance"))
            throw new InvalidOperationException("Unbekanntes Hosting-Profil der Zentrale.");
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("Fargate-Profil erfordert Linux.");
        if (!string.Equals(stateFile, StatePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Fargate-Zustand muss im vorgesehenen persistenten Volume liegen.");
        if (hosting == "fargate" && mode != "existing")
            throw new InvalidOperationException("Regulärer Fargate-Dienst darf weder Bootstrap noch Recovery-Aktivierung ausführen.");
        if (hosting == "fargate-maintenance" && mode is not ("bootstrap" or "restore" or "backup"))
            throw new InvalidOperationException("Fargate-Wartung benötigt einen expliziten einmaligen Auftrag.");
        if (!HasPersistentMount(File.ReadLines("/proc/self/mountinfo")))
            throw new IOException("Persistentes EFS/NFS-Volume fehlt. Kein Start mit Container-Speicher.");
        if (hosting == "fargate" && !File.Exists(stateFile))
            throw new FileNotFoundException("Fargate-Zentralzustand fehlt. Kein automatischer Bootstrap.", stateFile);
    }

    internal static bool HasPersistentMount(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var separator = Array.IndexOf(fields, "-");
            if (separator < 6 || separator + 1 >= fields.Length) continue;
            // mountinfo field 5 is the mount point. Require an exact mount.
            if (fields[4] == MountPath && (fields[separator + 1] is "nfs" or "nfs4"))
                return true;
        }
        return false;
    }
}

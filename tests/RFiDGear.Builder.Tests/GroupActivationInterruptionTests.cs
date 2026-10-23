using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

internal static class GroupActivationInterruptionTests
{
    public static void Verify()
    {
        foreach (var phase in new[] { "before-save", "after-activation" })
        {
            var root = Path.Combine(Path.GetTempPath(), "SieEncode-activation-interruption-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "synthetic.key"), RandomNumberGenerator.GetBytes(32));
                var id = Guid.NewGuid();
                File.WriteAllText(Path.Combine(root, "library.txt"), id.ToString("D"));
                using var protection = new SyntheticProtection(root);
                var adminPath = Path.Combine(root, "admin.bin");
                var admin = new OfflineGroupAdministrationStore(adminPath, id, protection);
                var original = admin.Create();
                var oldBytes = File.ReadAllBytes(adminPath);
                var trust = new OfflineGroupTrustStore(Path.Combine(root, "trust.bin"), id, protection);
                trust.Enroll(original.AdministratorPublicKey,
                    OfflineGroupTrustStore.AdministratorFingerprint(original.AdministratorPublicKey), original.Policy);
                var pending = new OfflineGroupSuccessorStore(Path.Combine(root, "pending.bin"), id, protection);
                var request = pending.Create(trust, trust.LoadOffline().Checkpoint);
                var fingerprint = pending.GetFingerprint();
                var transition = admin.AuthorizeSuccessor(request, trust, trust.LoadOffline().Checkpoint, fingerprint, true);
                trust.AcceptAdministratorTransition(transition, trust.LoadOffline().Checkpoint, fingerprint, true);
                var trustBytes = File.ReadAllBytes(Path.Combine(root, "trust.bin"));
                var pendingBytes = File.ReadAllBytes(Path.Combine(root, "pending.bin"));

                using (var child = StartChild(root, phase))
                {
                    try
                    {
                        var timer = Stopwatch.StartNew();
                        while (!File.Exists(Path.Combine(root, "ready")) && !child.HasExited && timer.Elapsed < TimeSpan.FromSeconds(30))
                            Thread.Sleep(20);
                        if (!File.Exists(Path.Combine(root, "ready")) || child.HasExited)
                            throw new Exception("Activation child did not reach the interruption boundary: " + phase);
                        child.Kill(entireProcessTree: true);
                        if (!child.WaitForExit(10000)) throw new Exception("Terminated activation child did not exit.");
                        if (child.ExitCode == 0) throw new Exception("Activation child was not interrupted.");
                    }
                    finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(10000); } }
                }

                var archive = Directory.GetFiles(root, "admin.bin.retired-*.bin").Single();
                if (!File.ReadAllBytes(archive).SequenceEqual(oldBytes)) throw new Exception("Interruption damaged predecessor archive.");
                var interruptedBytes = File.ReadAllBytes(adminPath);
                if (phase == "before-save" && !interruptedBytes.SequenceEqual(oldBytes))
                    throw new Exception("Interruption before save replaced predecessor.");
                if (phase == "after-activation" && interruptedBytes.SequenceEqual(oldBytes))
                    throw new Exception("Completed activation did not replace predecessor.");

                // A fresh process reconstructs all stores from disk, with no inherited keys or objects.
                using (var resumed = StartChild(root, "resume"))
                {
                    try
                    {
                        if (!resumed.WaitForExit(30000)) throw new Exception("Activation restart timed out.");
                        if (resumed.ExitCode != 0) throw new Exception("Activation restart failed: " + resumed.ExitCode);
                    }
                    finally { if (!resumed.HasExited) { resumed.Kill(entireProcessTree: true); resumed.WaitForExit(10000); } }
                }
                if (phase == "after-activation" && !File.ReadAllBytes(adminPath).SequenceEqual(interruptedBytes))
                    throw new Exception("Restart rewrote completed activation.");
                if (!File.ReadAllBytes(archive).SequenceEqual(oldBytes) ||
                    !File.ReadAllBytes(Path.Combine(root, "trust.bin")).SequenceEqual(trustBytes) ||
                    !File.ReadAllBytes(Path.Combine(root, "pending.bin")).SequenceEqual(pendingBytes))
                    throw new Exception("Restart altered archive, registered trust or pending key.");
                var current = admin.ExportPublic();
                if (OfflineGroupTrustStore.AdministratorFingerprint(current.AdministratorPublicKey) != fingerprint)
                    throw new Exception("Restart activated wrong authority.");
                var update = admin.UpdateGroups([], trust.LoadOffline().Checkpoint);
                trust.AcceptPackageUpdate(update, trust.LoadOffline().Checkpoint);
                if (trust.LoadOffline().Checkpoint != GroupEnrollmentPackages.Validate(update).Checkpoint)
                    throw new Exception("Restarted authority could not issue an accepted update.");
            }
            finally { Directory.Delete(root, recursive: true); }
        }
    }

    public static void RunChild(string root, string phase)
    {
        if (phase is not ("before-save" or "after-activation" or "resume")) throw new ArgumentException("Unknown phase.");
        var id = Guid.Parse(File.ReadAllText(Path.Combine(root, "library.txt")));
        using var protection = new SyntheticProtection(root);
        var admin = new OfflineGroupAdministrationStore(Path.Combine(root, "admin.bin"), id, protection);
        var trust = new OfflineGroupTrustStore(Path.Combine(root, "trust.bin"), id, protection);
        var pending = new OfflineGroupSuccessorStore(Path.Combine(root, "pending.bin"), id, protection);
        void WaitForTermination()
        {
            using (var marker = new FileStream(Path.Combine(root, "ready"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { marker.WriteByte(1); marker.Flush(flushToDisk: true); }
            Thread.Sleep(Timeout.Infinite);
        }
        if (phase == "before-save") protection.BeforeProtect = WaitForTermination;
        admin.ActivateSuccessor(pending, trust, trust.LoadOffline().Checkpoint, true);
        if (phase == "after-activation") WaitForTermination();
    }

    private static Process StartChild(string root, string phase)
    {
        var executable = Environment.ProcessPath ?? throw new Exception("No current process path.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("group-activation-interruption");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add(phase);
        return Process.Start(start) ?? throw new Exception("Could not start activation child.");
    }

    // Disposable synthetic encryption shared by test processes; not Windows-user isolation.
    private sealed class SyntheticProtection(string root) : ILocalGroupTrustProtection, IDisposable
    {
        private readonly byte[] key = File.ReadAllBytes(Path.Combine(root, "synthetic.key"));
        public Action? BeforeProtect { get; set; }
        public byte[] Protect(byte[] plaintext)
        {
            BeforeProtect?.Invoke();
            var output = new byte[28 + plaintext.Length];
            RandomNumberGenerator.Fill(output.AsSpan(0, 12));
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(output.AsSpan(0, 12), plaintext, output.AsSpan(28), output.AsSpan(12, 16));
            return output;
        }
        public byte[] Unprotect(byte[] state)
        {
            var output = new byte[state.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(state.AsSpan(0, 12), state.AsSpan(28), state.AsSpan(12, 16), output);
            return output;
        }
        public void Dispose() => CryptographicOperations.ZeroMemory(key);
    }
}

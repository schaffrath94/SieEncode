using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyAdministratorTransitionDialog(string root, ICollection<string> checks)
    {
        using var oldKey = RSA.Create(3072);
        using var nextKey = RSA.Create(3072);
        var library = Guid.NewGuid();
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1, []);
        var original = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, library,
            oldKey.ExportSubjectPublicKeyInfo(), OfflineGroupPolicyService.Sign(policy, oldKey));
        var transition = GroupAdministratorTransition.Create(original, oldKey, nextKey);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(nextKey.ExportSubjectPublicKeyInfo());
        var pendingDirectory = Path.Combine(root, "successor-preparation");
        var pendingTrust = new OfflineGroupTrustStore(Path.Combine(pendingDirectory, "trust.bin"), library, new WindowsGroupTrustProtection(library));
        var pendingCheckpoint = pendingTrust.Enroll(original.AdministratorPublicKey,
            OfflineGroupTrustStore.AdministratorFingerprint(original.AdministratorPublicKey), original.Policy).Checkpoint;
        var pendingPath = Path.Combine(pendingDirectory, "pending.bin");
        var pending = new OfflineGroupSuccessorStore(pendingPath, library, new WindowsGroupSuccessorProtection(library));
        var pendingRequest = pending.Create(pendingTrust, pendingCheckpoint);
        var reloaded = new OfflineGroupSuccessorStore(pendingPath, library, new WindowsGroupSuccessorProtection(library));
        if (!GroupAdministratorTransition.WriteRequest(pendingRequest).SequenceEqual(
            GroupAdministratorTransition.WriteRequest(reloaded.ExportRequest(pendingTrust))))
            throw new Exception("DPAPI successor preparation changed on reload.");
        var protectedBytes = File.ReadAllBytes(pendingPath);
        Reject(() => new WindowsGroupTrustProtection(library).Unprotect(protectedBytes));
        Reject(() => new WindowsGroupTrustProtection(library, true).Unprotect(protectedBytes));
        Reject(() => new WindowsGroupSuccessorProtection(Guid.NewGuid()).Unprotect(protectedBytes));
        const string backupPhrase = "Synthetic DPAPI successor backup 2026!";
        var backup = reloaded.ExportPrivateBackup(backupPhrase);
        var activePath = Path.Combine(pendingDirectory, "active.bin");
        var active = new OfflineGroupAdministrationStore(activePath, library, new WindowsGroupTrustProtection(library, true));
        Reject(() => active.RestorePrivateBackup(backup, backupPhrase, pendingTrust));
        if (File.Exists(activePath)) throw new Exception("Pending DPAPI key activated before authorization.");
        var pendingNext = GroupAdministratorTransition.InspectRequest(pendingRequest, original);
        var pendingFingerprint = OfflineGroupTrustStore.AdministratorFingerprint(pendingNext.AdministratorPublicKey);
        var approved = GroupAdministratorTransition.Authorize(pendingRequest, original, oldKey, pendingFingerprint, true);
        pendingTrust.AcceptAdministratorTransition(approved, pendingCheckpoint, pendingFingerprint, true);
        active.RestorePrivateBackup(backup, backupPhrase, pendingTrust);
        var changed = active.UpdateGroups([], pendingTrust.LoadOffline().Checkpoint);
        pendingTrust.AcceptPackageUpdate(changed, pendingTrust.LoadOffline().Checkpoint);
        if (!File.ReadAllBytes(pendingPath).SequenceEqual(protectedBytes)) throw new Exception("DPAPI handover overwrote preparation.");
        checks.Add("Nachfolgervorbereitung: echte CurrentUser-DPAPI-Datei neu geladen; fremde Bibliothek, Trust- und Verwaltungszweck abgewiesen; Aktivierung erst nach autorisiertem Wechsel, danach Richtlinie signiert. Nur aktueller Windows-Benutzer, keine Zweitbenutzerabnahme.");
        foreach (var scenario in new[] { "frozen", "concurrent" })
        {
            var directory = Path.Combine(root, "transition-" + scenario);
            var path = Path.Combine(directory, library.ToString("N") + ".trust.bin");
            var trust = new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library));
            trust.Enroll(original.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(original.AdministratorPublicKey), original.Policy);
            var candidate = GroupAdministratorTransition.Read(GroupAdministratorTransition.Write(transition));
            var before = File.ReadAllBytes(path);
            var window = new GroupAdministratorTransitionWindow(this, candidate, directory);
            try
            {
                VerifyAutomationContract(window);
                window.SelectLibrary(library);
                Reject(() => window.Accept(fingerprint, false));
                Reject(() => window.Accept(new string('0', 64), true));
                if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new Exception("Unconfirmed transition UI wrote trust.");
                Reject(() => window.SelectLibrary(Guid.NewGuid()));
                Reject(() => window.Accept(fingerprint, true));
                window.SelectLibrary(library);
                if (scenario == "frozen")
                {
                    candidate.Payload[0] ^= 1; candidate.NextSignature[0] ^= 1;
                    window.Accept(fingerprint.ToLowerInvariant(), true);
                    if (!trust.ExportPublic().AdministratorPublicKey.SequenceEqual(nextKey.ExportSubjectPublicKeyInfo()))
                        throw new Exception("Frozen transition was not imported.");
                    Reject(() => window.Accept(fingerprint, true));
                }
                else
                {
                    trust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 2 }, oldKey));
                    var latest = File.ReadAllBytes(path);
                    Reject(() => window.Accept(fingerprint, true));
                    if (!File.ReadAllBytes(path).SequenceEqual(latest)) throw new Exception("Stale UI transition overwrote new policy.");
                }
            }
            finally { window.Close(); }
        }
        checks.Add("Verwaltungswechsel-Dialog: lokaler DPAPI-Trust, Vorschau ohne Schreiben, unabhängiger Fingerabdruck und Bestätigung erforderlich; falsche Auswahl verwirft alten Zielzustand, Kandidat eingefroren, konkurrierende Aktualisierung und Wiederholung abgewiesen. Keine private Schlüsselübergabe oder Zweitbenutzerabnahme.");

        static void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or InvalidOperationException) { return; }
            throw new Exception("Invalid transition UI action succeeded.");
        }
    }
}

using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyAdministratorTransitionDialog(string root, ICollection<string> checks)
    {
        VerifySuccessorAuthorizationExport(root, checks);
        VerifySuccessorActivationUi(root, checks);
        using var oldKey = RSA.Create(3072);
        using var nextKey = RSA.Create(3072);
        var library = Guid.NewGuid();
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1, []);
        var original = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, library,
            oldKey.ExportSubjectPublicKeyInfo(), OfflineGroupPolicyService.Sign(policy, oldKey));
        var transition = GroupAdministratorTransition.Create(original, oldKey, nextKey);
        var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(nextKey.ExportSubjectPublicKeyInfo());
        var setupRoot = Path.Combine(root, "successor-setup-ui");
        var setupTrust = new OfflineGroupTrustStore(Path.Combine(setupRoot, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library));
        setupTrust.Enroll(original.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(original.AdministratorPublicKey), original.Policy);
        var setup = new GroupSuccessorSetupWindow(this, setupRoot);
        var exportedRequest = Path.Combine(setupRoot, "public.group-successor.json");
        try
        {
            setup.ShowInTaskbar = false; setup.Opacity = 0; setup.Show();
            VerifyAutomationContract(setup);
            setup.SelectLibrary(library); setup.Prepare();
            Reject(() => setup.Prepare());
            Reject(() => setup.ExportRequest(exportedRequest));
            if (File.Exists(exportedRequest)) throw new Exception("Successor UI exported before private backup.");
            var backupWindow = setup.CreateBackupWindow();
            try
            {
                backupWindow.ExportTo(Path.Combine(setupRoot, "backup.group-private.json"), "Synthetic successor UI backup phrase 2026!", "Synthetic successor UI backup phrase 2026!", true);
            }
            finally { backupWindow.Close(); }
            setup.ExportRequest(exportedRequest);
            _ = GroupAdministratorTransition.InspectRequest(GroupAdministratorTransition.ReadRequest(File.ReadAllBytes(exportedRequest)), original);
            Reject(() => setup.ExportRequest(exportedRequest));
            Reject(() => setup.SelectLibrary(Guid.NewGuid()));
            Reject(() => setup.CreateBackupWindow());
        }
        finally { setup.Close(); }
        var setupRestart = new GroupSuccessorSetupWindow(this, setupRoot);
        try
        {
            setupRestart.ShowInTaskbar = false; setupRestart.Opacity = 0; setupRestart.Show();
            Reject(() => setupRestart.ExportRequest(Path.Combine(setupRoot, "without-new-backup.json")));
            setupTrust.AcceptUpdate(OfflineGroupPolicyService.Sign(policy with { Revision = 2 }, oldKey));
            Reject(() => setupRestart.SelectLibrary(library));
            var rescue = setupRestart.CreateBackupWindow();
            try { rescue.ExportTo(Path.Combine(setupRoot, "stale-backup.group-private.json"), "Synthetic stale successor recovery 2026!", "Synthetic stale successor recovery 2026!", true); }
            finally { rescue.Close(); }
            Reject(() => setupRestart.ExportRequest(Path.Combine(setupRoot, "stale-request.json")));
        }
        finally { setupRestart.Close(); }
        checks.Add("Nachfolge-Einrichtung: Vorbereitung erhalten, Anfrage erst nach geprüfter privater Sicherung, öffentliche Signatur geprüft, Wiederanlage/Überschreiben abgewiesen; neue Sitzung benötigt Sicherung, veraltete Vorbereitung bleibt sicherbar, ungültige Auswahl verwirft Ziel. Keine visuelle Dateidialogabnahme.");
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
    private void VerifySuccessorAuthorizationExport(string root, ICollection<string> checks)
    {
        var directory = Path.Combine(root, "authorizing-ui");
        var window = new GroupAdministrationWindow(this, directory);
        try
        {
            var id = window.CreateAdministration();
            var trustPath = Path.Combine(directory, id.ToString("N") + ".trust.bin");
            var adminPath = Path.Combine(directory, "Administrations", id.ToString("N") + ".admin.bin");
            var trust = new OfflineGroupTrustStore(trustPath, id, new WindowsGroupTrustProtection(id));
            var original = trust.ExportPublic();
            using var successor = RSA.Create(3072);
            var request = GroupAdministratorTransition.Prepare(original, successor);
            var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(successor.ExportSubjectPublicKeyInfo());
            var output = Path.Combine(directory, "public.group-transition.json");
            var trustBefore = File.ReadAllBytes(trustPath); var adminBefore = File.ReadAllBytes(adminPath);
            Reject(() => window.ExportSuccessorAuthorization(request, fingerprint, false, output));
            Reject(() => window.ExportSuccessorAuthorization(request, "", true, output));
            if (File.Exists(output)) throw new Exception("Invalid UI authorization created an output.");
            window.ExportSuccessorAuthorization(request, fingerprint.ToLowerInvariant(), true, output);
            var bytes = File.ReadAllBytes(output);
            var transition = GroupAdministratorTransition.Read(bytes);
            _ = GroupAdministratorTransition.Verify(transition, original, fingerprint);
            Reject(() => window.ExportSuccessorAuthorization(request, fingerprint, true, output));
            Reject(() => window.ExportSuccessorAuthorization(request, fingerprint, true, adminPath));
            if (!File.ReadAllBytes(output).SequenceEqual(bytes) || !File.ReadAllBytes(trustPath).SequenceEqual(trustBefore) ||
                !File.ReadAllBytes(adminPath).SequenceEqual(adminBefore)) throw new Exception("UI export overwrote output or local authority.");
            trust.AcceptAdministratorTransition(transition, trust.LoadOffline().Checkpoint, fingerprint, true);
            var staleOutput = Path.Combine(directory, "stale.group-transition.json");
            Reject(() => window.ExportSuccessorAuthorization(request, fingerprint, true, staleOutput));
            if (File.Exists(staleOutput)) throw new Exception("Stale UI exported old authority transition.");
            checks.Add("Nachfolgerautorisierung in Verwaltungsoberfläche: Bestätigung/Fingerabdruck erforderlich, öffentliche Ausgabe verifiziert, vorhandene Dateien und Verwaltung unverändert; alte Oberfläche nach Trustwechsel abgewiesen. Dateiauswahldialoge nicht visuell abgenommen.");
        }
        finally { window.Close(); }

        static void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or InvalidOperationException) { return; }
            throw new Exception("Invalid UI authorization succeeded.");
        }
    }

    private void VerifySuccessorActivationUi(string root, ICollection<string> checks)
    {
        foreach (var existing in new[] { false, true })
        {
            var directory = Path.Combine(root, "activation-ui-" + existing);
            var oldWindow = new GroupAdministrationWindow(this, directory);
            GroupSuccessorSetupWindow? setup = null;
            try
            {
                var id = oldWindow.CreateAdministration();
                var adminPath = Path.Combine(directory, "Administrations", id.ToString("N") + ".admin.bin");
                var oldBytes = File.ReadAllBytes(adminPath);
                var trust = new OfflineGroupTrustStore(Path.Combine(directory, id.ToString("N") + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
                var original = trust.ExportPublic();
                setup = new GroupSuccessorSetupWindow(this, directory) { ShowInTaskbar = false, Opacity = 0 };
                setup.Show(); setup.Prepare();
                Reject(() => setup.Activate(trust.LoadOffline().Checkpoint, true));
                var backup = setup.CreateBackupWindow();
                try { backup.ExportTo(Path.Combine(directory, "backup.group-private.json"), "Synthetic complete handover backup 2026!", "Synthetic complete handover backup 2026!", true); }
                finally { backup.Close(); }
                var requestPath = Path.Combine(directory, "request.group-successor.json");
                setup.ExportRequest(requestPath);
                var request = GroupAdministratorTransition.ReadRequest(File.ReadAllBytes(requestPath));
                var next = GroupAdministratorTransition.InspectRequest(request, original);
                var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey);
                var transitionPath = Path.Combine(directory, "approved.group-transition.json");
                oldWindow.ExportSuccessorAuthorization(request, fingerprint, true, transitionPath);
                var import = new GroupAdministratorTransitionWindow(this, GroupAdministratorTransition.Read(File.ReadAllBytes(transitionPath)), directory);
                try { import.SelectLibrary(id); import.Accept(fingerprint, true); }
                finally { import.Close(); }
                setup.Close(); setup = null;
                if (!existing) File.Move(adminPath, Path.Combine(directory, "synthetic-previous-location.bin"));
                setup = new GroupSuccessorSetupWindow(this, directory);
                VerifyAutomationContract(setup);
                var checkpoint = trust.LoadOffline().Checkpoint;
                Reject(() => setup.Activate(checkpoint, false));
                Reject(() => setup.Activate(new(999, checkpoint.PayloadSha256), true));
                setup.Activate(checkpoint, true);
                var activeBytes = File.ReadAllBytes(adminPath);
                setup.Activate(checkpoint, true);
                if (!File.ReadAllBytes(adminPath).SequenceEqual(activeBytes)) throw new Exception("Repeated UI activation rewrote authority.");
                if (existing)
                {
                    var archive = Directory.GetFiles(Path.GetDirectoryName(adminPath)!, "*.retired-*.bin").Single();
                    if (!File.ReadAllBytes(archive).SequenceEqual(oldBytes)) throw new Exception("Windows predecessor archive changed protected bytes.");
                }
                var active = new OfflineGroupAdministrationStore(adminPath, id, new WindowsGroupTrustProtection(id, true));
                var newer = active.UpdateGroups([], checkpoint);
                trust.AcceptPackageUpdate(newer, checkpoint);
                oldWindow.LoadAdministration(id);
            }
            finally { setup?.Close(); oldWindow.Close(); }
        }
        checks.Add("Nachfolge-Gesamtablauf unter Windows-DPAPI: Vorbereitung, Sicherung, Anfrage, Autorisierung, expliziter Import und Aktivierung nach Dialogneustart; mit/ohne bisherige Verwaltungsdatei, Archiv bytegleich, unbestätigt/stale abgewiesen, Wiederholung ohne Schreiben und neue Signatur erfolgreich. Kein echter Zweitbenutzer, keine visuelle Bestätigungsdialogabnahme.");

        static void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is CryptographicException or IOException or InvalidDataException or InvalidOperationException) { return; }
            throw new Exception("Invalid successor activation UI action succeeded.");
        }
    }
}

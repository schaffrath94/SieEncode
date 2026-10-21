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

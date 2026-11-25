using System.IO;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    private void ShowPrivateBackup()
    {
        RequireSavedAdministration();
        var selected = store!;
        new GroupPrivateBackupWindow(this, [new($"Hauptverwaltung · {currentLibrary:D}", selected.ExportPrivateBackup, null)], false).ShowDialog();
    }

    private void ShowPrivateRestore()
    {
        if (!ConfirmDiscard()) return;
        var trustDirectory = Directory.GetParent(localDirectory)!.FullName;
        var choices = Directory.Exists(trustDirectory) ? Directory.EnumerateFiles(trustDirectory, "*.trust.bin")
            .Select(p => Path.GetFileName(p).Replace(".trust.bin", "", StringComparison.Ordinal))
            .Where(n => Guid.TryParseExact(n, "N", out _)).Select(n =>
            {
                var id = Guid.Parse(n);
                return new GroupPrivateBackupTarget($"Registrierte Bibliothek · {id:D}", null, (bytes, password) =>
                {
                    var trust = new OfflineGroupTrustStore(Path.Combine(trustDirectory, n + ".trust.bin"), id, new WindowsGroupTrustProtection(id));
                    using var lease = CentralGroupPolicyWorkflow.Acquire(id);
                    CentralGroupPolicyWorkflow.RequireNoPending(id);
                    Store(id).RestorePrivateBackup(bytes, password, trust);
                    dirty = false; RefreshLibraries(); LoadAdministration(id);
                });
            }).ToArray() : [];
        if (choices.Length == 0) throw new InvalidOperationException("Zuerst die öffentliche Bibliotheksregistrierung mit unabhängig bestätigtem Fingerabdruck importieren.");
        new GroupPrivateBackupWindow(this, choices, true).ShowDialog();
    }
}

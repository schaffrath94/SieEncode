using System.IO;
using System.Windows;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    internal void ExportSuccessorAuthorization(AdministratorTransitionRequest request, string independentFingerprint, bool confirmed, string destination)
    {
        RequireSavedAdministration();
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary!.Value);
        var candidate = GroupAdministratorTransition.ReadRequest(GroupAdministratorTransition.WriteRequest(request));
        var normalized = string.Concat(independentFingerprint.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        var trust = OwnTrust(currentLibrary!.Value);
        // Validate before creating directories/files; validate again immediately before the write.
        _ = store!.AuthorizeSuccessor(candidate, trust, checkpoint!, normalized, confirmed);
        ConcurrentFileWriter.Write(destination, FileRevision.Missing, temporary =>
        {
            var approved = store.AuthorizeSuccessor(candidate, trust, checkpoint!, normalized, confirmed);
            File.WriteAllBytes(temporary, GroupAdministratorTransition.Write(approved));
        });
        status.Text = "Öffentliche Übergabe gespeichert. An den Nachfolger und alle Arbeitsplätze verteilen; dort den Wechsel ausdrücklich importieren. Die lokale Registrierung und der bisherige private Schlüssel wurden hier nicht verändert. Offline-Arbeitsplätze vertrauen bis zum Import weiter dem alten Schlüssel.";
    }

    private void ShowSuccessorAuthorization()
    {
        RequireSavedAdministration();
        var open = new OpenFileDialog { Title = "Öffentliche Nachfolgeranfrage öffnen", Filter = "Nachfolgeranfrage (*.group-successor.json)|*.group-successor.json" };
        if (open.ShowDialog(this) != true) return;
        byte[] bytes;
        using (var stream = new FileStream(open.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length is <= 0 or > GroupAdministratorTransition.MaximumBytes) throw new InvalidDataException("Nachfolgeranfrage ist leer oder zu groß.");
            bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        }
        var request = GroupAdministratorTransition.ReadRequest(bytes);
        var current = OwnTrust(currentLibrary!.Value).ExportPublic();
        var next = GroupAdministratorTransition.InspectRequest(request, current);
        var fingerprint = TextEntryPrompt.Show(this, "Nachfolge unabhängig bestätigen",
            $"Bibliothek: {current.LibraryId:D}\nNeuer Fingerabdruck laut Anfrage:\n{OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey)}\n\nDen separat vom Nachfolger erhaltenen SHA-256-Fingerabdruck eingeben. Die Anfrage allein ist keine Bestätigung.", "");
        if (fingerprint is null) return;
        if (MessageBox.Show(this, "Die Hauptverwaltung an diesen unabhängig bestätigten Nachfolger übergeben?\n\nPersonen, Geräte, Gruppen, Recovery und vorhandene Vollmachten bleiben unverändert. Eine öffentliche, von beiden Verwaltungen signierte Übergabe wird erstellt. Arbeitsplätze müssen diese anschließend ausdrücklich importieren; Offline-Kopien werden nicht sofort gesperrt.", "Nachfolge autorisieren", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var normalized = string.Concat(fingerprint.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        _ = store!.AuthorizeSuccessor(request, OwnTrust(currentLibrary.Value), checkpoint!, normalized, true);
        var save = new SaveFileDialog { Title = "Öffentliche Übergabe als neue Datei speichern", Filter = "Verwaltungswechsel (*.group-transition.json)|*.group-transition.json", FileName = currentLibrary.Value.ToString("N") + ".group-transition.json" };
        if (save.ShowDialog(this) != true) return;
        ExportSuccessorAuthorization(request, normalized, true, save.FileName);
    }
}

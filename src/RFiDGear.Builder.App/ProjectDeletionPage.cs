using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst eine bearbeitbare Projektdefinition auswählen.", true); return; }
        try
        {
            if (preparedSession is not null)
                throw new InvalidOperationException("Während einer aktiven Kodiersitzung kann keine Projektdefinition gelöscht werden. Bitte Sitzung zuerst fachlich abschließen.");
            var preview = new ProjectDeletionService().Prepare(selectedProject, projectsDirectory);
            var sameEditor = !string.IsNullOrWhiteSpace(editorSourcePath) && string.Equals(Path.GetFullPath(editorSourcePath), preview.SourcePath, StringComparison.OrdinalIgnoreCase);
            if (sameEditor && editorDirty && !CanReplaceEditor()) return;

            var vaultState = preview.VaultExists ? "vorhanden und bleibt erhalten" : "nicht vorhanden";
            var buildState = preview.BuildDirectoryExists ? "vorhanden und bleibt erhalten" : "nicht vorhanden";
            var answer = MessageBox.Show(this,
                $"Projektdefinition wirklich löschen?\n\nProjekt: {preview.ProjectName}\n\nWIRD GELÖSCHT:\n{preview.SourcePath}\n\nBLEIBT ERHALTEN:\nVault: {preview.VaultPath}\n  ({vaultState})\nBuildordner: {preview.BuildDirectory}\n  ({buildState})\n\n{preview.Contract}\n\nDiese Aktion kann nicht rückgängig gemacht werden.",
                "Projektdefinition löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) { SetStatus("Löschen abgebrochen. Es wurde nichts verändert."); return; }

            var result = new ProjectDeletionService().Delete(preview);
            if (!result.SourceDeleted) throw new IOException("Projektdefinition konnte nicht vollständig gelöscht werden.");
            if (sameEditor) ClearEditor();
            ClearProjectSelection();
            ProjectSelectionInfo.Text = "Projektdefinition gelöscht. Vault und Buildartefakte wurden nicht verändert.";
            ResetAcceptanceView();
            RefreshProjects();
            SetStatus("Projektdefinition gelöscht; Vault und Buildartefakte blieben erhalten: " + result.SourcePath);
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

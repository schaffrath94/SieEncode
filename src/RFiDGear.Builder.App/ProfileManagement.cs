using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void ProfilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var enabled = ProfilesGrid.SelectedItem is ProfileListRow { Source: not "Gebündelt" };
        ProfileEditButton.IsEnabled = enabled;
        ProfileDuplicateButton.IsEnabled = enabled;
        ProfileExportButton.IsEnabled = ProfilesGrid.SelectedItem is ProfileListRow;
        ProfileHistoryButton.IsEnabled = enabled;
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
        => OpenProfileEditor(new ProfileEditingService().CreateCustomer(), true);

    private void NewBaseProfile_Click(object sender, RoutedEventArgs e)
        => OpenProfileEditor(new ProfileEditingService().CreateBase(), true);

    private void ImportBaseProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Schlüsselwertfreies SieEncode-Grundprofil importieren",
                Filter = "SieEncode-Profil (*.json)|*.json",
                InitialDirectory = Directory.Exists(profileStore.BaseDirectory) ? profileStore.BaseDirectory : null
            };
            if (dialog.ShowDialog(this) != true) return;
            using var stream = File.OpenRead(dialog.FileName);
            var profile = JsonSerializer.Deserialize<ProfileDefinition>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Die ausgewählte Profildatei ist leer.");
            if (!string.Equals(profile.Profile.Kind, "base", StringComparison.Ordinal))
                throw new InvalidDataException("Die ausgewählte Datei ist kein Grundprofil (profile.kind muss 'base' sein).");
            var issues = ProfileValidator.Validate(profile);
            if (issues.Count > 0)
                throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            var digest = ProfileIdentityService.ComputeDigest(profile);
            var summary = $"ID: {profile.Profile.Id}\nName: {profile.Profile.Name}\nVersion: {profile.Profile.Version}\nApplications: {profile.Applications.Count}\nFingerprint: {digest[..12]}\n\n" +
                "Das Grundprofil wird in die gemeinsame Profilbibliothek übernommen. Bereits gebundene Projekte wechseln niemals still auf den neuen Inhalt. Import fortsetzen?";
            if (MessageBox.Show(this, summary, "Grundprofil kontrolliert importieren", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var path = profileStore.SaveBase(profile);
            LoadProfiles();
            RefreshOpenEditorAfterProfileLibraryChange();
            SetStatus("Grundprofil importiert und validiert: " + path);
        }
        catch (JsonException exception)
        {
            var location = exception.LineNumber is long line
                ? $" Fehler in Zeile {line + 1}."
                : string.Empty;
            ShowError(new InvalidDataException(
                "Die ausgewählte Datei kann nicht als SieEncode-Grundprofil gelesen werden. " +
                "Das JSON-Format oder ein enthaltenes Datenfeld ist ungültig." + location +
                " Bitte prüfen Sie die Datei oder wählen Sie eine gültige Profildatei aus.", exception));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ImportRfPrjProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Bestehendes RFiDGear-Projekt schreibgeschützt analysieren",
                Filter = "RFiDGear-Projekte (*.rfPrj)|*.rfPrj",
                InitialDirectory = Directory.Exists(projectsDirectory) ? projectsDirectory : null
            };
            if (dialog.ShowDialog(this) != true) return;
            var importer = new RfPrjProfileImportService();
            var report = importer.Analyze(dialog.FileName, LoadProfileLibrary());
            var reportDirectory = Path.Combine(Path.GetDirectoryName(settingsStore.SettingsPath)!, "reports", "rfprj-import",
                DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "_" + MakeSafeFileName(Path.GetFileNameWithoutExtension(dialog.FileName)));
            var files = importer.SaveReport(report, reportDirectory);
            var window = new RfPrjImportWindow(importer, files) { Owner = this };
            if (window.ShowDialog() == true && window.Proposal is not null)
            {
                SetStatus("rfPrj wurde als noch ungespeicherter, schlüsselwertfreier Profilvorschlag geöffnet.");
                OpenProfileEditor(window.Proposal, true);
            }
            else SetStatus("rfPrj-Analyse abgeschlossen; Importbericht: " + files.TextPath);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ImportBaseFromRfPrj_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "RFiDGear-Projekt als Grundprofil schreibgeschützt analysieren",
                Filter = "RFiDGear-Projekte (*.rfPrj)|*.rfPrj",
                InitialDirectory = Directory.Exists(projectsDirectory) ? projectsDirectory : null
            };
            if (dialog.ShowDialog(this) != true) return;
            var importer = new RfPrjProfileImportService();
            var report = importer.Analyze(dialog.FileName, LoadProfileLibrary());
            var reportDirectory = Path.Combine(Path.GetDirectoryName(settingsStore.SettingsPath)!, "reports", "rfprj-base-import",
                DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "_" + MakeSafeFileName(Path.GetFileNameWithoutExtension(dialog.FileName)));
            var files = importer.SaveReport(report, reportDirectory);
            var window = new RfPrjImportWindow(importer, files) { Owner = this };
            if (window.ShowDialog() != true || window.Proposal is null)
            {
                SetStatus("rfPrj-Grundprofilanalyse abgeschlossen; kein Profil importiert: " + files.TextPath);
                return;
            }
            window.Proposal.Profile.Kind = "base";
            var issues = ProfileValidator.Validate(window.Proposal);
            if (issues.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            var digest = ProfileIdentityService.ComputeDigest(window.Proposal);
            if (MessageBox.Show(this,
                $"Grundprofil: {window.Proposal.Profile.Name}\nID: {window.Proposal.Profile.Id}\nVersion: {window.Proposal.Profile.Version}\nApplications: {window.Proposal.Applications.Count}\nFingerprint: {digest[..12]}\n\nSchlüsselwerte wurden nicht übernommen. Grundprofil jetzt speichern?",
                "rfPrj als Grundprofil übernehmen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var path = profileStore.SaveBase(window.Proposal);
            LoadProfiles();
            RefreshOpenEditorAfterProfileLibraryChange();
            SetStatus("Grundprofil keywertfrei aus rfPrj abgeleitet: " + path);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfilesGrid.SelectedItem is not ProfileListRow row) throw new InvalidOperationException("Bitte zuerst ein Profil auswählen.");
            if (row.Source == "Gebündelt") throw new InvalidOperationException("Ein gebündeltes Profil ist schreibgeschützt.");
            OpenProfileEditor(LoadProfileLibrary().Get(row.Id), false);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfilesGrid.SelectedItem is not ProfileListRow { Kind: "customer" } row)
                throw new InvalidOperationException("Bitte zuerst ein Kundenprofil auswählen.");
            var library = LoadProfileLibrary();
            var draft = new ProfileEditingService().CreateCloneDraft(library.Get(row.Id), library.Profiles.Select(profile => profile.Profile.Id));
            SetStatus($"Profil '{row.Id}' als neuen Entwurf '{draft.Profile.Id}' dupliziert. Vor dem Speichern ID, Kennung, Name und fachlichen Inhalt prüfen.");
            OpenProfileEditor(draft, true);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfilesGrid.SelectedItem is not ProfileListRow row)
                throw new InvalidOperationException("Bitte zuerst ein Profil auswählen.");
            var profile = LoadProfileLibrary().Get(row.Id);
            Directory.CreateDirectory(profileStore.UserDirectory);
            var dialog = new SaveFileDialog
            {
                Title = "Keywertfreie Kodiervorschrift exportieren",
                Filter = "Textdatei (*.txt)|*.txt",
                AddExtension = true,
                DefaultExt = ".txt",
                FileName = profile.Profile.Id + "_Kodiervorschrift.txt",
                InitialDirectory = profileStore.UserDirectory
            };
            if (dialog.ShowDialog(this) != true) return;
            var path = new CodingSpecificationService().Export(profile, dialog.FileName);
            SetStatus("Kodiervorschrift exportiert: " + path);
            if (MessageBox.Show(this, "Kodiervorschrift wurde keywertfrei exportiert.\n\n" + path + "\n\nDatei jetzt öffnen?", "Kodiervorschrift", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenProfileEditor(ProfileDefinition profile, bool isNew)
    {
        var window = new ProfileEditorWindow(profile, profileStore, isNew) { Owner = this };
        if (window.ShowDialog() == true)
        {
            LoadProfiles();
            RefreshOpenEditorAfterProfileLibraryChange();
            SetStatus("Profil dauerhaft gespeichert: " + window.SavedPath);
        }
    }

    private void ProfileHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfilesGrid.SelectedItem is not ProfileListRow { Kind: "customer" } row)
                throw new InvalidOperationException("Bitte zuerst ein Kundenprofil auswählen.");
            var window = new ProfileHistoryWindow(profileStore, row.Id) { Owner = this };
            if (window.ShowDialog() == true)
            {
                LoadProfiles();
                RefreshOpenEditorAfterProfileLibraryChange();
                SetStatus("Kundenprofil aus der Historie als neue Version wiederhergestellt: " + window.RestoredPath);
            }
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ReloadProfiles_Click(object sender, RoutedEventArgs e)
    {
        try { LoadProfiles(); RefreshOpenEditorAfterProfileLibraryChange(); SetStatus("Profilbibliothek neu geladen."); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RefreshOpenEditorAfterProfileLibraryChange()
    {
        if (editorProject is null) return;
        var library = LoadEditorLibrary();
        var before = JsonSerializer.SerializeToNode(editorProject, ProjectJson.Options);
        ProjectDraftFactory.Synchronize(editorProject, library);
        var changed = !System.Text.Json.Nodes.JsonNode.DeepEquals(before,
            JsonSerializer.SerializeToNode(editorProject, ProjectJson.Options));
        suppressEditorChanges = true;
        try
        {
            RefreshEditorProfileChoices(library);
            PopulateDynamicEditorFields();
        }
        finally { suppressEditorChanges = false; }
        SetEditorDirty(editorDirty || changed);
        RefreshProfileBindingStatus();
    }

    private void OpenProfileFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(profileStore.UserDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", profileStore.UserDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenBaseProfileFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(profileStore.BaseDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", profileStore.BaseDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenProfileHistoryFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(profileStore.HistoryDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", profileStore.HistoryDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

public sealed record ProfileListRow(string Id, string Kind, string Version, string Name, string Source);

using System.Diagnostics;
using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private ProfileDiffFiles? lastDiffFiles;

    private void InitializeDiffPage()
    {
        DiffLeftProject.ItemsSource = projects;
        DiffRightProject.ItemsSource = projects;
    }

    private void RefreshDiffProfiles(ProfileLibrary library)
    {
        var choices = library.Profiles.OrderBy(profile => profile.Profile.Kind).ThenBy(profile => profile.Profile.Id)
            .Select(profile => new ProfileChoice(profile.Profile.Id, $"{profile.Profile.Name} · {profile.Profile.Version} ({profile.Profile.Id})")).ToArray();
        var left = DiffLeftProfile.SelectedValue as string;
        var right = DiffRightProfile.SelectedValue as string;
        DiffLeftProfile.ItemsSource = choices;
        DiffRightProfile.ItemsSource = choices;
        DiffLeftProfile.SelectedValue = choices.Any(choice => choice.Id == left) ? left : choices.FirstOrDefault()?.Id;
        DiffRightProfile.SelectedValue = choices.Any(choice => choice.Id == right) ? right : choices.Skip(1).FirstOrDefault()?.Id ?? choices.FirstOrDefault()?.Id;
    }

    private void CompareProjects_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DiffLeftProject.SelectedItem is not ProjectCatalogEntry left || DiffRightProject.SelectedItem is not ProjectCatalogEntry right)
                throw new InvalidOperationException("Bitte links und rechts jeweils ein Projekt auswählen.");
            var library = LoadProfileLibrary();
            RunDiff(Composition(left, library), Composition(right, library), ProjectLabel(left), ProjectLabel(right));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void CompareProfiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DiffLeftProfile.SelectedValue is not string leftId || DiffRightProfile.SelectedValue is not string rightId)
                throw new InvalidOperationException("Bitte links und rechts jeweils ein Profil auswählen.");
            var library = LoadProfileLibrary();
            var left = library.Get(leftId); var right = library.Get(rightId);
            RunDiff(Single(left), Single(right), ProfileLabel(left), ProfileLabel(right));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void CompareEditorToSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            if (selectedProject is null) throw new InvalidOperationException("Kein Vergleichsprojekt in der Projektliste ausgewählt.");
            CaptureEditorControls();
            ProjectDraftFactory.Synchronize(editorProject, editorLibrary);
            var left = editorLibrary.Compose(editorProject.Profiles.Base, editorProject.Profiles.Customers);
            RunDiff(left, Composition(selectedProject, editorLibrary), "Editorentwurf · " + editorProject.Project.Name, ProjectLabel(selectedProject));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RunDiff(ComposedProfiles left, ComposedProfiles right, string leftLabel, string rightLabel)
    {
        var output = Path.Combine(Path.GetDirectoryName(settingsStore.SettingsPath)!, "reports", "profile-diff");
        lastDiffFiles = new ProfileDiffService().CompareAndSave(left, right, leftLabel, rightLabel, output);
        var report = lastDiffFiles.Report;
        DiffGrid.ItemsSource = report.Items;
        DiffResult.Text = report.Identical
            ? $"IDENTISCH · {leftLabel} und {rightLabel} besitzen keine fachlichen Unterschiede."
            : $"{report.Added} hinzugefügt · {report.Removed} entfernt · {report.Changed} geändert · keine Schlüsselwerte im Bericht";
        DiffResultBorder.Background = report.Identical ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.LemonChiffon;
        DiffResultBorder.BorderBrush = report.Identical ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.Goldenrod;
        DiffReportPath.Text = $"Text: {lastDiffFiles.TextPath}\nJSON: {lastDiffFiles.JsonPath}";
        DiffOpenReportButton.IsEnabled = File.Exists(lastDiffFiles.TextPath);
        DiffOpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(lastDiffFiles.TextPath));
        Navigate("Diff");
        SetStatus(report.Identical ? "Profilvergleich abgeschlossen: fachlich identisch." : $"Profilvergleich abgeschlossen: {report.Items.Count} Unterschied(e).");
    }

    private static ComposedProfiles Composition(ProjectCatalogEntry entry, ProfileLibrary library)
    {
        var project = entry.ArtifactOnly ? new LegacyProjectMigrationService().Import(entry.SourcePath, library).Project : ProjectJson.Load(entry.SourcePath);
        return library.Compose(project.Profiles.Base, project.Profiles.Customers);
    }

    private static ComposedProfiles Single(ProfileDefinition profile) => new([profile], profile.Applications, profile.Variables);
    private static string ProjectLabel(ProjectCatalogEntry entry) => $"{entry.Name} · {entry.BaseProfile}" + (entry.Customers == "–" ? "" : " + " + entry.Customers);
    private static string ProfileLabel(ProfileDefinition profile) => $"{profile.Profile.Name} · {profile.Profile.Version} ({profile.Profile.Id})";

    private void OpenDiffReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastDiffFiles is null || !File.Exists(lastDiffFiles.TextPath)) throw new FileNotFoundException("Textbericht wurde nicht gefunden.");
            Process.Start(new ProcessStartInfo(lastDiffFiles.TextPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenDiffFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastDiffFiles is null) throw new InvalidOperationException("Noch kein Vergleich ausgeführt.");
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(lastDiffFiles.TextPath)!) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

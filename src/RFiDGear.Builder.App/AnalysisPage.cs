using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private ProjectAnalysisFiles? lastAnalysisFiles;
    private CardReport? lastCardReport;

    private void AnalyzeSelectedProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedProject is null) throw new InvalidOperationException("Bitte zuerst ein Projekt auswählen.");
            ProjectDefinition project;
            var source = selectedProject.SourcePath;
            if (selectedProject.ArtifactOnly)
            {
                var migration = new LegacyProjectMigrationService().Import(source, LoadProfileLibrary());
                project = migration.Project;
                source += " (sicher migrierter Mastering-Entwurf)";
            }
            else project = ProjectJson.Load(source);
            RunAnalysis(project, source, selectedProject.ProjectDirectory);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void AnalyzeEditorProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            CaptureEditorControls();
            ProjectDraftFactory.Synchronize(editorProject, editorLibrary);
            var source = string.IsNullOrWhiteSpace(editorSourcePath) ? "Ungespeicherter Editorentwurf" : editorSourcePath;
            var output = string.IsNullOrWhiteSpace(editorSourcePath)
                ? Path.Combine(Path.GetTempPath(), "SieEncode", "Projektanalyse", SafeName(editorProject.Project.Name))
                : Path.GetDirectoryName(editorSourcePath)!;
            RunAnalysis(editorProject, source, output);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void AnalyzeFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Projektdefinition für Dry-Run wählen",
                Filter = "RFiDGear-Projektdefinition (*.project.json)|*.project.json|JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*",
                InitialDirectory = Directory.Exists(projectsDirectory) ? projectsDirectory : null
            };
            if (dialog.ShowDialog(this) != true) return;
            ProjectDefinition project;
            try { project = ProjectJson.Load(dialog.FileName); }
            catch { project = new LegacyProjectMigrationService().Import(dialog.FileName, LoadProfileLibrary()).Project; }
            RunAnalysis(project, dialog.FileName, Path.GetDirectoryName(dialog.FileName)!);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RunAnalysis(ProjectDefinition project, string source, string outputDirectory)
    {
        var library = LoadProfileLibrary();
        var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
        lastAnalysisFiles = new ProjectAnalysisService().AnalyzeAndSave(project, library, shells, outputDirectory);
        var report = lastAnalysisFiles.Report;
        lastCardReport = new CardReportFactory().FromProject(project, report, source, library);

        AnalysisFilesGrid.ItemsSource = report.Applications.SelectMany(application => application.Files.Select(file => new AnalysisFileRow(
            application.Aid, application.Name, $"{file.Id:D2} · {file.Name}", $"{file.Type} · {file.Size} B",
            $"{file.Communication} · {file.Access}",
            file.CreationSettings is null ? file.Initialization : $"{file.Initialization} · temporär {file.CreationSettings}",
            string.IsNullOrWhiteSpace(file.CodingOutput) ? "–" : file.CodingOutput))).ToArray();
        AnalysisMasteringGrid.ItemsSource = report.MasteringTasks.Select(ToTaskRow).ToArray();
        AnalysisCodingGrid.ItemsSource = report.CodingTasks.Select(ToTaskRow).ToArray();
        AnalysisIssuesGrid.ItemsSource = report.Issues;
        AnalysisSource.Text = "Quelle: " + source;
        AnalysisResult.Text = report.Errors == 0
            ? $"OK · {report.Applications.Count} Applications · {report.MasteringTasks.Count} Mastering-Tasks · {report.CodingTasks.Count} Kodier-Tasks · keine Schlüsselwerte im Bericht"
            : $"NICHT FREIGEGEBEN · {report.Errors} Fehler · {report.Warnings} Warnungen";
        AnalysisResultBorder.Background = report.Errors == 0 ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose;
        AnalysisResultBorder.BorderBrush = report.Errors == 0 ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.Firebrick;
        AnalysisReportPath.Text = $"Text: {lastAnalysisFiles.TextPath}\nJSON: {lastAnalysisFiles.JsonPath}";
        AnalysisOpenReportButton.IsEnabled = File.Exists(lastAnalysisFiles.TextPath);
        AnalysisOpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(lastAnalysisFiles.TextPath));
        AnalysisExportPdfButton.IsEnabled = true;
        Navigate("Analysis");
        SetStatus(report.Errors == 0 ? "Projektanalyse ohne Inkonsistenzen abgeschlossen." : "Projektanalyse enthält Fehler; Details stehen im Befund-Tab.", report.Errors > 0);
    }

    private static AnalysisTaskRow ToTaskRow(ProjectAnalysisTask task)
        => new(task.Index, task.Type, task.Description,
            string.IsNullOrWhiteSpace(task.ConditionIndex) ? "–" : $"Task {task.ConditionIndex} = {task.ConditionLevel}",
            string.Join(" / ", new[] { task.Aid.Length == 0 ? "" : "AID " + task.Aid, task.FileId.Length == 0 ? "" : "File " + task.FileId }.Where(value => value.Length > 0)),
            string.IsNullOrWhiteSpace(task.KeyReference) ? "–" : task.KeyReference);

    private void OpenAnalysisReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastAnalysisFiles is null || !File.Exists(lastAnalysisFiles.TextPath)) throw new FileNotFoundException("Textbericht wurde nicht gefunden.");
            Process.Start(new ProcessStartInfo(lastAnalysisFiles.TextPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenAnalysisFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastAnalysisFiles is null) throw new InvalidOperationException("Noch keine Analyse ausgeführt.");
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(lastAnalysisFiles.TextPath)!) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ExportAnalysisPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastCardReport is null) throw new InvalidOperationException("Bitte zuerst ein Projekt analysieren.");
            var dialog = new SaveFileDialog
            {
                Title = "Schlüsselwertfreien PDF-Kartenreport speichern",
                Filter = "PDF-Dokument (*.pdf)|*.pdf",
                FileName = SafeName(lastCardReport.Project) + "_Kartenreport_Soll.pdf",
                InitialDirectory = lastAnalysisFiles is null ? projectsDirectory : Path.GetDirectoryName(lastAnalysisFiles.TextPath)
            };
            if (dialog.ShowDialog(this) != true) return;
            new CardReportPdfService().Save(lastCardReport, dialog.FileName);
            AnalysisReportPath.Text = (lastAnalysisFiles is null ? "" : $"Text: {lastAnalysisFiles.TextPath}\nJSON: {lastAnalysisFiles.JsonPath}\n") + $"PDF: {dialog.FileName}";
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            SetStatus("Schlüsselwertfreier PDF-Kartenreport wurde gespeichert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return safe.Length == 0 ? "Entwurf" : safe;
    }

    private sealed record AnalysisFileRow(string Aid, string Application, string File, string TypeSize, string FinalSettings, string Mastering, string Coding);
    private sealed record AnalysisTaskRow(string Index, string Type, string Description, string ConditionText, string Target, string KeyReference);
}

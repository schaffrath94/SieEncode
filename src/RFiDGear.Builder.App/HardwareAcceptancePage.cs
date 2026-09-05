using System.Diagnostics;
using System.IO;
using System.Windows;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void PrepareHardwareAcceptance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedProject is null) throw new InvalidOperationException("Bitte zuerst ein Projekt in der Projektbibliothek auswählen.");
            var preflight = RunSelectedPreflight();
            var operations = operationService.Inspect(selectedProject.ProjectDirectory);
            var capabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            var reader = RFiDGearSettingsInspection.Inspect();
            var codingProject = Directory.Exists(selectedProject.ProjectDirectory)
                ? Directory.EnumerateFiles(selectedProject.ProjectDirectory, "*_Kodierung.rfPrj", SearchOption.TopDirectoryOnly).SingleOrDefault() ?? ""
                : "";
            var request = new HardwareAcceptancePlanRequest(
                Version, selectedProject.Name, selectedProject.ProjectDirectory, selectedProject.SourcePath, selectedProject.ArtifactOnly,
                codingProject, preflight, operations, capabilities, reader);
            var outputDirectory = Path.Combine(Path.GetDirectoryName(settingsStore.SettingsPath)!, "reports", "hardware-acceptance", MakeSafeFileName(selectedProject.Name));
            var files = new HardwareAcceptancePlanService().CreateReport(request, outputDirectory);
            var blocked = files.Plan.Readiness.Count(item => item.Status == "Blockiert");
            MessageBox.Show(this,
                $"Hardware-Abnahmeplan wurde rein lesend vorbereitet.\n\nStatus: {files.Plan.Status}\nBlocker: {blocked}\n\n{files.Plan.SafetyContract}\n\nBericht: {files.TextPath}",
                "Hardware-Abnahme vorbereitet", MessageBoxButton.OK, blocked == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Process.Start(new ProcessStartInfo(files.TextPath) { UseShellExecute = true });
            SetStatus("Hardware-Abnahmeplan erzeugt. Keine Hardwareaktion wurde gestartet: " + files.TextPath, blocked > 0);
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private AcceptanceReportResult? lastAcceptanceReport;

    private void ResetAcceptanceView()
    {
        lastAcceptanceReport = null;
        AcceptanceGrid.ItemsSource = null;
        AcceptanceResult.Text = "Noch keine Prüfung ausgeführt.";
        AcceptanceResultBorder.Background = System.Windows.Media.Brushes.WhiteSmoke;
        AcceptanceResultBorder.BorderBrush = System.Windows.Media.Brushes.LightGray;
        AcceptanceReportPath.Text = "Bericht: –";
        AcceptanceOpenReportButton.IsEnabled = false;
        AcceptanceOpenFolderButton.IsEnabled = false;
        AcceptanceRunButton.IsEnabled = selectedProject is not null && File.Exists(Path.Combine(selectedProject.ProjectDirectory, "acceptance.plan.json"));
    }

    private void RunAcceptance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedProject is null) throw new InvalidOperationException("Kein Projekt für die Abnahme ausgewählt.");
            lastAcceptanceReport = new AcceptanceReportService().Create(selectedProject.ProjectDirectory);
            var report = lastAcceptanceReport.Report;
            AcceptanceGrid.ItemsSource = report.Checks;
            AcceptanceResult.Text = $"{report.Result} · OK {report.Summary.Ok} · Abweichung {report.Summary.Deviation} · Nicht prüfbar {report.Summary.NotVerifiable} · Gesamt {report.Summary.Total}\n" +
                $"Nachweisabdeckung: bytegenau {report.Coverage.ByteExact} · operativ {report.Coverage.Operational} · ohne Istwert {report.Coverage.WithoutActualValue}\n" +
                "Application- und FileSettings-Nachweise werden nur aus aktueller SHA-256-korrelierter ReadAppSettings-/CheckAppKeyCount-/CheckFileSettings-Evidenz übernommen.";
            if (report.Summary.Deviation > 0)
            {
                AcceptanceResultBorder.Background = System.Windows.Media.Brushes.MistyRose;
                AcceptanceResultBorder.BorderBrush = System.Windows.Media.Brushes.Firebrick;
            }
            else if (report.Summary.Ok > 0)
            {
                AcceptanceResultBorder.Background = System.Windows.Media.Brushes.Honeydew;
                AcceptanceResultBorder.BorderBrush = System.Windows.Media.Brushes.SeaGreen;
            }
            else
            {
                AcceptanceResultBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
                AcceptanceResultBorder.BorderBrush = System.Windows.Media.Brushes.Goldenrod;
            }
            AcceptanceReportPath.Text = $"Text: {lastAcceptanceReport.TextPath}\nJSON: {lastAcceptanceReport.JsonPath}";
            AcceptanceOpenReportButton.IsEnabled = File.Exists(lastAcceptanceReport.TextPath);
            AcceptanceOpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(lastAcceptanceReport.TextPath));
            SetStatus("Prüfung abgeschlossen: " + report.Result, report.Summary.Deviation > 0);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenAcceptanceReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastAcceptanceReport is null || !File.Exists(lastAcceptanceReport.TextPath)) throw new FileNotFoundException("Textbericht wurde nicht gefunden.");
            Process.Start(new ProcessStartInfo(lastAcceptanceReport.TextPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenAcceptanceFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastAcceptanceReport is null) throw new InvalidOperationException("Noch kein Prüfbericht erzeugt.");
            var directory = Path.GetDirectoryName(lastAcceptanceReport.TextPath)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Berichtsordner wurde nicht gefunden: " + directory);
            Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

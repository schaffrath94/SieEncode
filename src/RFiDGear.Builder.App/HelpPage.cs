using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void RunEnvironmentCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = FindDistributionRoot();
            var request = new EnvironmentCheckRequest(
                root,
                settingsStore.SettingsPath,
                projectsDirectory,
                settings.RFiDGear.ExecutablePath,
                Path.Combine(AppContext.BaseDirectory, "assets", "profiles"),
                Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"),
                Path.Combine(root, "START_SieEncode.cmd"),
                [Path.Combine(root, "README.md"), Path.Combine(root, "PARITY.md"), Path.Combine(root, "MIGRATION.md"), Path.Combine(root, "RUNTIME.md"), Path.Combine(root, "docs", "GRUNDPROFILE.md"), Path.Combine(root, "docs", "HARDWARE-ABNAHME.md"), Path.Combine(root, "docs", "OFFLINE-GRUPPEN-SCHNELLSTART.md")]);
            var reportDirectory = Path.Combine(Path.GetDirectoryName(settingsStore.SettingsPath)!, "reports", "environment");
            lastEnvironmentCheck = new EnvironmentCheckService().CreateReport(request, reportDirectory);
            EnvironmentChecksGrid.ItemsSource = lastEnvironmentCheck.Report.Checks;
            EnvironmentChecksGrid.Visibility = Visibility.Visible;
            var summary = lastEnvironmentCheck.Report.Summary;
            EnvironmentResult.Text = $"{lastEnvironmentCheck.Report.Result} · {summary.Ok} OK · {summary.Warnings} Warnung(en) · {summary.Errors} Fehler";
            EnvironmentResultBorder.Background = summary.Errors > 0 ? new SolidColorBrush(Color.FromRgb(255, 235, 238)) :
                summary.Warnings > 0 ? new SolidColorBrush(Color.FromRgb(255, 248, 225)) : new SolidColorBrush(Color.FromRgb(232, 245, 233));
            EnvironmentReportPath.Text = "Bericht: " + lastEnvironmentCheck.TextPath;
            EnvironmentOpenReportButton.IsEnabled = true;
            EnvironmentOpenFolderButton.IsEnabled = true;
            SetStatus("Umgebungstest abgeschlossen. RFiDGear, Reader und Karte wurden nicht gestartet.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenHelpDocument_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = (sender as Button)?.Tag as string ?? throw new InvalidOperationException("Dokument wurde nicht angegeben.");
            var path = Path.Combine(FindDistributionRoot(), name);
            if (string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
                new HelpDocumentWindow(this, path).ShowDialog();
            else OpenExisting(path, false);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenHelpProjects_Click(object sender, RoutedEventArgs e) => OpenFolderSafe(projectsDirectory);
    private void OpenHelpSettings_Click(object sender, RoutedEventArgs e) => OpenFolderSafe(Path.GetDirectoryName(settingsStore.SettingsPath)!);
    private void OpenHelpLogs_Click(object sender, RoutedEventArgs e) => OpenFolderSafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "log"));
    private void OpenEnvironmentReport_Click(object sender, RoutedEventArgs e) { if (lastEnvironmentCheck is not null) OpenExisting(lastEnvironmentCheck.TextPath, false); }
    private void OpenEnvironmentFolder_Click(object sender, RoutedEventArgs e) { if (lastEnvironmentCheck is not null) OpenFolderSafe(Path.GetDirectoryName(lastEnvironmentCheck.TextPath)!); }

    private async void RunFunctionAudit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Pfad der laufenden SieEncode-Anwendung ist unbekannt.");
            var report = Path.Combine(Path.GetTempPath(), "SieEncode", "ui-smoke", "ui-smoke-result.json");
            if (File.Exists(report)) File.Delete(report);
            FunctionAuditResult.Text = "Funktionsprüfung läuft in einer isolierten zweiten Instanz …";
            FunctionAuditOpenButton.IsEnabled = false;
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--ui-smoke-test");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Isolierte Funktionsprüfung konnte nicht gestartet werden.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !File.Exists(report))
                throw new InvalidOperationException($"Funktionsprüfung fehlgeschlagen (Exitcode {process.ExitCode}).");
            lastFunctionAuditPath = report;
            FunctionAuditResult.Text = "OK · Oberfläche und Bedienwege geprüft · 5/5 erfundene Kodierprojekte einschließlich modularer HEX/ASCII/UTF-8/BCD/Integer-Mischdaten erfolgreich gebaut (Software-Nachweis).";
            FunctionAuditOpenButton.IsEnabled = true;
            SetStatus("Isolierte Programmfunktionsprüfung erfolgreich.");
        }
        catch (Exception exception)
        {
            FunctionAuditResult.Text = "FEHLER · " + exception.Message;
            ShowError(exception);
        }
    }

    private void OpenFunctionAudit_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(lastFunctionAuditPath)) OpenExisting(lastFunctionAuditPath, false);
    }

    private void OpenFolderSafe(string path)
    {
        try { OpenExisting(path, true); }
        catch (Exception exception) { ShowError(exception); }
    }

    private static void OpenExisting(string path, bool directory)
    {
        if (directory ? !Directory.Exists(path) : !File.Exists(path)) throw new FileNotFoundException("Pfad wurde nicht gefunden.", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string FindDistributionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; current is not null && level < 9; level++, current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "VERSION"))) return current.FullName;
        throw new DirectoryNotFoundException("Release-Verzeichnis mit VERSION-Datei wurde nicht gefunden.");
    }
}

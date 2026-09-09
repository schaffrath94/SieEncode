using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void ExportInventoryComparison_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (LatestInventoryPdfPath() is null) throw new InvalidOperationException("Bitte zuerst eine Application untersuchen.");
            using var inventory = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(lastTestReportPath!, ".json")));
            var choose = new OpenFileDialog { Title = "Sollprojekt für den Dateivergleich auswählen", Filter = "SieEncode-Projekt (*.project.json)|*.project.json", CheckFileExists = true };
            if (choose.ShowDialog(this) != true) return;
            var project = ProjectJson.Load(choose.FileName);
            var profiles = LoadProfileLibrary().Compose(project.Profiles.Base, project.Profiles.Customers);
            var aid = inventory.RootElement.GetProperty("applicationId").GetString();
            var applications = profiles.Applications.Where(app => string.Equals(ProfileValidator.NormalizeAid(app.Id), aid, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (applications.Length != 1) throw new InvalidDataException("Die untersuchte AID ist im ausgewählten Sollprojekt nicht eindeutig vorhanden.");
            var sources = applications[0].Files.Where(file => file.Coding is not null).SelectMany(file => file.Coding!.Recipe)
                .Where(step => step.Source is not null).Select(step => step.Source!).ToHashSet(StringComparer.Ordinal);
            var variables = profiles.Variables.Where(variable => variable.Scope == "card" && sources.Contains(variable.Id)).ToArray();
            IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
            if (variables.Length > 0)
            {
                var inputs = new InventoryComparisonValuesDialog(project.Project.Name, aid!, variables) { Owner = this };
                if (inputs.ShowDialog() != true) return;
                values = inputs.Values;
            }
            var report = new ProjectInventoryComparisonService().CreateReport(profiles, project.Values, values, inventory.RootElement,
                Path.ChangeExtension(lastTestReportPath!, ".json")) with { Project = project.Project.Name };
            report = report with { Findings = report.Findings.Append(new ProjectAnalysisIssue("INFO", "Sollquelle", choose.FileName)).ToArray() };
            var options = new ComparisonReportOptionsDialog { Owner = this };
            if (options.ShowDialog() != true) return;
            var save = new SaveFileDialog { Title = "Soll/Ist-Dateivergleich speichern", Filter = "PDF-Dokument (*.pdf)|*.pdf", FileName = SafeName(project.Project.Name) + "_Dateivergleich.pdf" };
            if (save.ShowDialog(this) != true) return;
            new CardReportPdfService().Save(report, save.FileName, options.Detail);
            Process.Start(new ProcessStartInfo(save.FileName) { UseShellExecute = true });
            SetStatus("Dateivergleich gespeichert. Nicht vergleichbare Inhalte und Einstellungen sind ausdrücklich gekennzeichnet.");
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

internal sealed class ComparisonReportOptionsDialog : Window
{
    private readonly RadioButton full = new() { Content = "Vollständig - mit Soll-/Ist-Rohdaten", IsChecked = true, Margin = new Thickness(0, 8, 0, 8) };
    public CardReportDetail Detail => full.IsChecked == true ? CardReportDetail.Full : CardReportDetail.Compact;
    public ComparisonReportOptionsDialog()
    {
        Title = "Umfang des Vergleichsberichts"; Width = 530; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = "Beide Varianten enthalten alle Vergleichsergebnisse und nicht prüfbaren Anforderungen.", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(full);
        body.Children.Add(new RadioButton { Content = "Kompakt - ohne HEX-/ASCII-Rohdaten", Margin = new Thickness(0, 0, 0, 14) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = "Abbrechen", IsCancel = true });
        var save = new Button { Content = "Weiter zum Speichern", IsDefault = true };
        save.Click += (_, _) => DialogResult = true; buttons.Children.Add(save);
        body.Children.Add(buttons); Content = body;
    }
}

internal sealed class InventoryComparisonValuesDialog : Window
{
    private readonly Dictionary<string, TextBox> fields = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Values => fields.Where(pair => !string.IsNullOrEmpty(pair.Value.Text))
        .ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal);

    public InventoryComparisonValuesDialog(string project, string aid, IEnumerable<VariableDefinition> variables)
    {
        Title = "Soll-Kartenwerte für Inhaltsvergleich";
        Width = 620; Height = 520; MinWidth = 540; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var cancel = new Button { Content = "Abbrechen", IsCancel = true };
        var accept = new Button { Content = "Vergleich erstellen", IsDefault = true };
        accept.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(accept); root.Children.Add(buttons);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = $"{project} · AID {aid}", FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = "Erwartete Kartenwerte eingeben. Angezeigte Vorbelegungen stammen aus dem Profil, nicht aus der Karte. Fehlende oder ungültige Werte führen zu nicht vergleichbaren Dateien. Die Eingaben gelten nur für diesen Bericht.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) });
        foreach (var variable in variables)
        {
            body.Children.Add(new TextBlock { Text = $"{variable.Label} ({variable.Id})", TextWrapping = TextWrapping.Wrap });
            var field = new TextBox { Text = variable.Default ?? "", Margin = new Thickness(0, 4, 0, 12) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(field, "ComparisonValue_" + variable.Id);
            fields.Add(variable.Id, field); body.Children.Add(field);
        }
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}

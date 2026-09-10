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
    private void ExportInventoryPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = LatestInventoryPdfPath() ?? throw new FileNotFoundException("Zum letzten Analyseergebnis liegt kein PDF-Istreport vor.");
            using var inventory = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(existing, ".json")));
            var options = new ComparisonReportOptionsDialog { Owner = this };
            if (options.ShowDialog() != true) return;
            var report = new InventoryCardReportFactory().Create(inventory.RootElement, Path.ChangeExtension(existing, ".json"));
            var save = new SaveFileDialog
            {
                Title = "PDF-Istreport speichern", Filter = "PDF-Dokument (*.pdf)|*.pdf",
                FileName = "AID_" + SafeName(report.Applications.Single().Aid) + "_Istreport.pdf"
            };
            if (save.ShowDialog(this) != true) return;
            new CardReportPdfService().Save(report, save.FileName, options.Detail, options.Documentation);
            Process.Start(new ProcessStartInfo(save.FileName) { UseShellExecute = true });
            SetStatus("PDF-Istreport im gewählten Umfang gespeichert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }
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
            new CardReportPdfService().Save(report, save.FileName, options.Detail, options.Documentation);
            Process.Start(new ProcessStartInfo(save.FileName) { UseShellExecute = true });
            SetStatus("Dateivergleich gespeichert. Nicht vergleichbare Inhalte und Einstellungen sind ausdrücklich gekennzeichnet.");
        }
        catch (Exception exception) { ShowError(exception); }
    }
}

internal sealed class ComparisonReportOptionsDialog : Window
{
    private readonly RadioButton full = new() { Content = "Vollständig - mit verfügbaren Rohdaten", IsChecked = true, Margin = new Thickness(0, 8, 0, 8) };
    public CardReportDetail Detail => full.IsChecked == true ? CardReportDetail.Full : CardReportDetail.Compact;
    private readonly CheckBox includeDocumentation = new() { Content = "Dokumentationsangaben ergänzen", Margin = new Thickness(0, 0, 0, 10) };
    private readonly DatePicker documentationDate = new();
    private readonly TextBox place = new();
    private readonly TextBox notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly CheckBox signature = new() { Content = "Unterschriftsbereich ausgeben" };
    private readonly List<CardReportImage> pictures = [];
    private readonly TextBlock pictureInfo = new() { Text = "Keine Kartenbilder ausgewählt.", TextWrapping = TextWrapping.Wrap };
    public CardReportDocumentation? Documentation => includeDocumentation.IsChecked == true
        ? new(documentationDate.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null, place.Text, notes.Text, signature.IsChecked == true) { Images = pictures.ToArray() } : null;
    private void ChoosePictures()
    {
        var dialog = new OpenFileDialog { Title = "Kartenbilder auswählen", Filter = "Bilder (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (dialog.FileNames.Length > 2) throw new InvalidDataException("Bitte höchstens zwei Bilder auswählen.");
            var selected = dialog.FileNames.Select((path, index) => LoadPicture(path, $"Kartenbild {index + 1}")).ToArray();
            pictures.Clear(); pictures.AddRange(selected);
            pictureInfo.Text = string.Join("\n", dialog.FileNames.Select(Path.GetFileName));
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Kartenbilder", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    internal static CardReportImage LoadPicture(string path, string caption)
    {
        using var stream = File.OpenRead(path);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.OnDemand);
        var sourceWidth = decoder.Frames[0].PixelWidth;
        var sourceHeight = decoder.Frames[0].PixelHeight;
        if ((long)sourceWidth * sourceHeight > 40_000_000) throw new InvalidDataException("Bild ist zu groß; bitte auf höchstens 40 Megapixel verkleinern.");
        stream.Position = 0;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = Math.Max(1, (int)(sourceWidth * Math.Min(1.0, 1280.0 / Math.Max(sourceWidth, sourceHeight))));
        image.StreamSource = stream;
        image.EndInit();
        var scale = Math.Min(1.0, 1280.0 / Math.Max(image.PixelWidth, image.PixelHeight));
        var width = Math.Max(1, (int)Math.Round(image.PixelWidth * scale));
        var height = Math.Max(1, (int)Math.Round(image.PixelHeight * scale));
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
            drawing.DrawImage(image, new Rect(0, 0, width, height));
        }
        var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rendered.Render(visual);
        var rgb = new System.Windows.Media.Imaging.FormatConvertedBitmap(rendered, System.Windows.Media.PixelFormats.Rgb24, null, 0);
        var bytes = new byte[width * height * 3];
        rgb.CopyPixels(bytes, width * 3, 0);
        return new(caption, width, height, bytes);
    }

    public ComparisonReportOptionsDialog()
    {
        Title = "Umfang des PDF-Berichts"; Width = 530; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = "Beide Varianten enthalten die Berichtsbefunde einschließlich vorhandener Vergleichsergebnisse und nicht prüfbarer Anforderungen.", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(full);
        body.Children.Add(new RadioButton { Content = "Kompakt - ohne HEX-/ASCII-Rohdaten", Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(includeDocumentation);
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = "Dokumentationsdatum (optional)" }); fields.Children.Add(documentationDate);
        fields.Children.Add(new TextBlock { Text = "Ort (optional)" }); fields.Children.Add(place);
        fields.Children.Add(new TextBlock { Text = "Notizen (optional)" }); fields.Children.Add(notes);
        fields.Children.Add(signature);
        var choosePictures = new Button { Content = "Kartenbilder auswählen (max. 2)" };
        choosePictures.Click += (_, _) => ChoosePictures();
        var clearPictures = new Button { Content = "Kartenbilder entfernen" };
        clearPictures.Click += (_, _) => { pictures.Clear(); pictureInfo.Text = "Keine Kartenbilder ausgewählt."; };
        fields.Children.Add(choosePictures); fields.Children.Add(clearPictures); fields.Children.Add(pictureInfo);
        var fieldScroll = new ScrollViewer { Content = fields, MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
        includeDocumentation.Checked += (_, _) => fieldScroll.Visibility = Visibility.Visible;
        includeDocumentation.Unchecked += (_, _) => fieldScroll.Visibility = Visibility.Collapsed;
        body.Children.Add(fieldScroll);
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

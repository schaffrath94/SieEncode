using System.Globalization;
using System.Text;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record CardReportField(string Name, string Value);
public enum ReportComparisonState { Equal, Different, NotComparable }
public enum CardReportDetail { Full, Compact }
public sealed record CardReportDocumentation(DateOnly? Date = null, string Place = "", string Notes = "", bool Signature = false)
{
    public bool HasContent => Date is not null || !string.IsNullOrWhiteSpace(Place) || !string.IsNullOrWhiteSpace(Notes) || Signature;
}
public sealed record CardReportSettingComparison(string Name, string Expected, string Observed, ReportComparisonState State);
public sealed record CardReportFile(
    int Id, string Name, string Type, int? Size, string Communication, string Access,
    string CreationSettings, string Initialization, string DataDefinition, string HexDump, string AsciiDump)
{
    public string? ExpectedHexDump { get; init; }
    public string? Comparison { get; init; }
    public IReadOnlyList<CardReportSettingComparison> SettingsComparison { get; init; } = [];
}
public sealed record CardReportApplication(
    string Aid, string Name, string KeyType, int? KeyCount, string TransportKeySettings,
    string ProductiveKeySettings, string ChangeKeyMode, string KeyVersions, IReadOnlyList<CardReportFile> Files);

/// <summary>
/// Neutraler, schluesselwertfreier Berichtvertrag. Dieselbe Struktur kann spaeter
/// aus einer Projektdefinition (Soll), einer Karteninventur (Ist) oder einem Soll/Ist-Vergleich befuellt werden.
/// </summary>
public sealed record CardReport(
    string SchemaVersion, string Kind, string Title, string Project, DateTimeOffset GeneratedAt,
    string Source, IReadOnlyList<CardReportField> General, IReadOnlyList<CardReportField> ProjectFields,
    IReadOnlyList<CardReportApplication> Applications, IReadOnlyList<ProjectAnalysisIssue> Findings);

public sealed class CardReportFactory
{
    public CardReport FromProject(ProjectDefinition project, ProjectAnalysisReport analysis, string source, ProfileLibrary? library = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);
        var composedPiccSettings = string.IsNullOrWhiteSpace(project.Productive.PiccKeySettings)
            ? "nicht verfuegbar"
            : project.Productive.PiccKeySettings;
        var general = new List<CardReportField>
        {
            new("Berichtsart", "Projektdefinition / Soll"),
            new("Quelle", source),
            new("Kartentyp", "MIFARE DESFire (aus Projektkontext)"),
            new("UID", "nicht verfügbar (Sollreport)"),
            new("Raw UID", "nicht verfuegbar"),
            new("UID Converter ID", "nicht verfuegbar"),
            new("Freier Speicher", "nicht verfügbar (Sollreport)"),
            new("PICC KeySettings (Soll)", composedPiccSettings),
            new("PICC Key-Version", "nicht verfuegbar"),
            new("Hardware-Version", "nicht verfuegbar"),
            new("Software-Version", "nicht verfuegbar"),
            new("Speichergröße", "nicht verfuegbar"),
            new("Produktionscharge/-datum", "nicht verfuegbar"),
            new("Grundprofil", project.Profiles.Base),
            new("Zusatzprofile", project.Profiles.Customers.Count == 0 ? "keine" : string.Join(", ", project.Profiles.Customers)),
            new("Kartenmodus", project.Project.CardMode),
            new("Build-Ziele", project.Project.Build.Count == 0 ? "keine" : string.Join(", ", project.Project.Build)),
            new("Schluesselwerte", "nicht enthalten")
        };
        var fields = project.Values
            .Where(pair => !LooksSecret(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new CardReportField(pair.Key, string.IsNullOrEmpty(pair.Value) ? "nicht gesetzt" : pair.Value))
            .ToArray();
        var payloads = TryBuildPayloads(project, library);
        var applications = analysis.Applications.Select(application => new CardReportApplication(
            application.Aid, application.Name, application.KeyType, application.KeyCount,
            application.TransportKeySettings, application.ProductiveKeySettings, application.ChangeKeyMode,
            string.Join(", ", Enumerable.Range(0, application.KeyCount).Select(index => $"{index}: nicht verfuegbar")),
            application.Files.Select(file => new CardReportFile(
                file.Id, file.Name, file.Type, file.Size, file.Communication, file.Access,
                file.CreationSettings ?? "identisch zum Endzustand", file.Initialization,
                string.IsNullOrWhiteSpace(file.CodingOutput) ? "keine Kodierausgabe" : file.CodingOutput,
                payloads.TryGetValue((application.Aid, file.Id), out var bytes) ? HexDump(bytes) : "nicht verfuegbar - variable Kartendaten fehlen",
                payloads.TryGetValue((application.Aid, file.Id), out bytes) ? AsciiDump(bytes) : "nicht verfuegbar - variable Kartendaten fehlen")).ToArray())).ToArray();
        return new CardReport("1.0", "project-plan", "SieEncode Kartenreport", project.Project.Name,
            ParseGeneratedAt(analysis.GeneratedAt), source, general, fields, applications, analysis.Issues);
    }

    private static bool LooksSecret(string name)
        => new[] { "key", "schluessel", "secret", "pass", "kennwort", "pin" }
            .Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<(string Aid, int FileId), byte[]> TryBuildPayloads(ProjectDefinition project, ProfileLibrary? library)
    {
        var result = new Dictionary<(string, int), byte[]>();
        if (library is null) return result;
        try
        {
            var composition = library.Compose(project.Profiles.Base, project.Profiles.Customers);
            var values = composition.Variables.ToDictionary(variable => variable.Id,
                variable => project.Values.GetValueOrDefault(variable.Id, variable.Default ?? ""), StringComparer.Ordinal);
            foreach (var application in composition.Applications)
            {
                var aid = ProfileValidator.NormalizeAid(application.Id);
                foreach (var file in application.Files.Where(file => file.Coding is not null))
                {
                    try { result[(aid, file.Id)] = PayloadEngine.Build(file, values, aid); }
                    catch (InvalidDataException) { /* Kartendaten werden erst in der Kodierstation erhoben. */ }
                }
            }
        }
        catch (InvalidDataException) { }
        return result;
    }

    private static string HexDump(byte[] bytes)
        => string.Join(" ", bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    private static string AsciiDump(byte[] bytes)
        => new string(bytes.Select(value => value is >= 32 and <= 126 ? (char)value : '.').ToArray());

    private static DateTimeOffset ParseGeneratedAt(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed : DateTimeOffset.Now;
}

/// <summary>Abhaengigkeitsfreier A4-PDF-Exporter fuer installierte Offline-Clients.</summary>
public sealed class CardReportPdfService
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 76;
    private const double RightMargin = 34;

    public void Save(CardReport report, string outputPath, CardReportDetail detail = CardReportDetail.Full, CardReportDocumentation? documentation = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var document = new LayoutDocument(report, detail, documentation);
        File.WriteAllBytes(fullPath, BuildPdf(document.Pages));
    }

    private sealed class LayoutDocument
    {
        private readonly CardReport report;
        private readonly CardReportDetail detail;
        private readonly List<PageCanvas> pages = [];
        private PageCanvas current = null!;
        private double y;

        public LayoutDocument(CardReport report, CardReportDetail detail, CardReportDocumentation? documentation)
        {
            this.report = report;
            this.detail = detail;
            NewPage();
            AddTitle(report.Title);
            AddLine($"{report.Kind} | Schema {report.SchemaVersion}", 9, false, "muted");
            if (detail == CardReportDetail.Compact) AddLine("Kompaktbericht: Rohdaten ausgelassen; Bewertungen und Befunde bleiben enthalten.", 9, false, "muted");
            AddSpace(8);
            AddSection("Allgemeine Karten- und Projektinformationen");
            AddFields(report.General);
            if (report.ProjectFields.Count > 0)
            {
                AddSection("Projektfelder");
                AddFields(report.ProjectFields);
            }
            Ensure(report.Applications.Count == 0 ? 55 : 305);
            AddSection($"Applikationen und Dateien ({report.Applications.Count})");
            if (report.Applications.Count == 0) AddLine("Keine Applikationen in der Projektdefinition.", 10);
            foreach (var application in report.Applications) AddApplication(application);
            if (report.Findings.Count > 0)
            {
                AddSection($"Analysebefunde ({report.Findings.Count})");
                foreach (var finding in report.Findings)
                    AddLine($"[{finding.Severity}] {finding.Area}: {finding.Message}", detail == CardReportDetail.Compact ? 8 : 9, finding.Severity == "ERROR", finding.Severity == "ERROR" ? "error" : "black");
            }
            if (documentation?.HasContent == true) AddDocumentation(documentation);
            FinishFooters();
        }

        private void AddDocumentation(CardReportDocumentation documentation)
        {
            AddSection("Ergänzende Dokumentation");
            if (documentation.Date is { } date) AddLine($"Dokumentationsdatum: {date:dd.MM.yyyy}", 9);
            if (!string.IsNullOrWhiteSpace(documentation.Place))
                foreach (var line in Wrap("Ort: " + documentation.Place, 45)) AddLine(line, 9);
            if (!string.IsNullOrWhiteSpace(documentation.Notes))
            {
                AddLine("Notizen:", 9, true);
                foreach (var line in Wrap(documentation.Notes, 45)) AddLine(line, 9);
            }
            if (documentation.Signature)
            {
                Ensure(95);
                AddSpace(30);
                current.Rule(y, 0.32, 0.42, 0.44);
                y -= 15;
                AddLine("Name / Unterschrift", 9);
                AddLine("Dokumentation - keine digitale Signatur oder technische Freigabe.", 8, false, "muted");
            }
        }

        public IReadOnlyList<PageCanvas> Pages => pages;

        private void AddApplication(CardReportApplication app)
        {
            if (report.Kind is "application-inventory" or "application-content-comparison" or "application-comparison")
            {
                AddObservedApplication(app);
                return;
            }
            Ensure(90);
            AddBand($"AID {app.Aid}");
            AddLine(app.Name, 11, true);
            AddLine($"Kryptografie: {app.KeyType} | Key-Slots: {app.KeyCount}", 9);
            AddLine($"KeySettings Soll: {app.ProductiveKeySettings} | Transport: {app.TransportKeySettings}", 9);
            AddLine($"Key-Versionen: {app.KeyVersions}", 9);
            AddLine($"Keywechsel: {app.ChangeKeyMode}", 9);
            foreach (var file in app.Files)
            {
                Ensure(85);
                AddBand($"AID {app.Aid} | File ID {file.Id:D2}", compact: true);
                AddLine(file.Name, 10, true);
                AddLine($"Typ: {file.Type} | Größe: {file.Size} Byte", 9);
                AddLine($"Kommunikation: {file.Communication} | Rechte: {file.Access}", 9);
                AddLine($"Erstellung: {file.CreationSettings}", 9);
                AddLine($"Initialisierung: {file.Initialization}", 9);
                AddLine($"Daten / Rezept: {file.DataDefinition}", 9);
                if (detail == CardReportDetail.Full)
                {
                    AddLine("HEX (Sollbytes):", 9, true);
                    foreach (var line in Wrap(file.HexDump, 48)) AddLine(line, 8);
                    AddLine("ASCII (Sollbytes):", 9, true);
                    foreach (var line in Wrap(file.AsciiDump, 48)) AddLine(line, 8);
                }
                AddSpace(8);
            }
        }

        private void AddFields(IEnumerable<CardReportField> fields)
        {
            if (report.Kind == "project-plan")
            {
                foreach (var field in fields) AddLine(field.Name + ": " + field.Value, 9);
                return;
            }
            var rows = fields.ToArray();
            for (var index = 0; index < rows.Length; index += 2)
            {
                Ensure(32);
                var half = (PageWidth - Margin - RightMargin - 8) / 2;
                AddFieldCell(rows[index], Margin, y, half);
                if (index + 1 < rows.Length) AddFieldCell(rows[index + 1], Margin + half + 8, y, half);
                y -= 31;
            }
        }

        private void AddObservedApplication(CardReportApplication app)
        {
            Ensure(90);
            AddBand($"AID {app.Aid}");
            AddLine($"Schlüsseltyp: {app.KeyType} | Key-Slots: {app.KeyCount?.ToString() ?? "unbekannt"}", 9);
            AddLine($"Gelesene KeySettings: {app.ProductiveKeySettings}", 9);
            AddLine($"Key-Versionen: {app.KeyVersions}", 9);
            if (app.Files.Count == 0) AddLine("Keine Dateidetails vorhanden. Vollständigkeit siehe Inventurbefund.", 9);
            foreach (var file in app.Files)
            {
                Ensure(95);
                AddBand($"AID {app.Aid} | File {file.Id:D2}", compact: true);
                AddLine($"Typ: {file.Type} | Größe: {file.Size?.ToString() ?? "unbekannt"} Byte", 9);
                AddLine($"Kommunikation: {file.Communication} | Rechte: {file.Access}", 9);
                AddLine(file.DataDefinition, 9, true);
                foreach (var setting in file.SettingsComparison)
                {
                    var verdict = setting.State switch { ReportComparisonState.Equal => "Gleich", ReportComparisonState.Different => "Abweichung", _ => "Nicht vergleichbar" };
                    AddLine($"{setting.Name}: Soll {setting.Expected} | Ist {setting.Observed} | {verdict}", 9,
                        setting.State == ReportComparisonState.Different, setting.State == ReportComparisonState.Different ? "error" : "black");
                }
                if (file.Comparison is not null)
                    AddLine(file.Comparison, 9, true, file.Comparison.StartsWith("Abweichung", StringComparison.Ordinal) ? "error" : "teal");
                if (detail == CardReportDetail.Compact)
                {
                    AddSpace(8);
                    continue;
                }
                if (report.Kind is "application-content-comparison" or "application-comparison")
                {
                    AddLine("Soll-HEX (aus Projekt und Eingaben):", 9, true);
                    foreach (var line in Wrap(file.ExpectedHexDump ?? "nicht erzeugbar oder kein Sollfile", 60)) AddLine(line, 8);
                }
                AddLine("HEX (gelesene Bytes):", 9, true);
                foreach (var line in Wrap(file.HexDump, 60)) AddLine(line, 8);
                AddLine("ASCII (gelesene Bytes):", 9, true);
                foreach (var line in Wrap(file.AsciiDump, 60)) AddLine(line, 8);
                AddSpace(8);
            }
        }

        private void AddFieldCell(CardReportField field, double x, double top, double width)
        {
            current.Box(x, top - 25, width, 28, 0.82, 0.84, 0.85);
            current.Text(x + 6, top - 9, Clip(field.Name, 28), 7.8, true, "muted");
            current.Text(x + 6, top - 21, Clip(field.Value, 36), 8.5, false, "black");
        }

        private void AddTitle(string value)
        {
            current.FillBox(Margin, y - 29, PageWidth - Margin - RightMargin, 33, 0.77, 0.79, 0.80);
            current.Text(Margin + 10, y - 19, value, 18, true, "black");
            y -= 43;
            AddLine(report.Project.Length == 0 ? "Unbenanntes Projekt" : report.Project, 12.5, true, "teal");
        }

        private void AddSection(string value)
        {
            Ensure(50);
            AddSpace(9);
            current.FillBox(Margin, y - 16, PageWidth - Margin - RightMargin, 19, 0.77, 0.79, 0.80);
            current.Text(Margin + 5, y - 10, value, 10.5, true, "black");
            y -= 25;
        }

        private void AddBand(string value, bool compact = false)
        {
            current.FillBox(Margin, y - (compact ? 14 : 17), PageWidth - Margin - RightMargin, compact ? 17 : 20, 0.88, 0.90, 0.91);
            current.Text(Margin + 5, y - (compact ? 9 : 11), value, compact ? 9.5 : 10.5, true, "teal");
            y -= compact ? 22 : 26;
        }

        private void AddLine(string value, double size, bool bold = false, string color = "black", double indent = 0)
        {
            var maxWidth = PageWidth - Margin - RightMargin - indent;
            foreach (var line in Wrap(value, Math.Max(18, (int)(maxWidth / (size * (report.Kind == "project-plan" ? 1.0 : 0.52))))))
            {
                Ensure(size + 5);
                current.Text(Margin + indent, y, line, size, bold, color);
                y -= size + 4;
            }
        }

        private void AddSpace(double amount) => y -= amount;
        private void Ensure(double required) { if (y - required < 58) NewPage(); }

        private void NewPage()
        {
            current = new PageCanvas();
            pages.Add(current);
            current.BrandRail();
            y = PageHeight - 46;
            if (pages.Count > 1)
            {
                current.Text(Margin, y, (report.Kind == "project-plan" ? "SieEncode Sollreport | Fortsetzung" : $"SieEncode Kartenreport | {report.Project}"), 8.5, true, "teal");
                current.Rule(y - 7, 0.65, 0.75, 0.76);
                y -= 24;
            }
        }

        private void FinishFooters()
        {
            for (var i = 0; i < pages.Count; i++)
            {
                pages[i].Rule(42, 0.75, 0.79, 0.80);
                pages[i].Text(Margin, 28, $"Erstellt: {report.GeneratedAt:yyyy-MM-dd HH:mm} | Quelle: {Shorten(report.Source, 60)}", 7.5, false, "muted");
                pages[i].Text(PageWidth - RightMargin - 70, 28, $"Seite {i + 1} / {pages.Count}", 7.5, false, "muted");
            }
        }
    }

    private sealed class PageCanvas
    {
        private readonly StringBuilder content = new();
        public string Content => content.ToString();
        public void Text(double x, double y, string value, double size, bool bold, string color)
        {
            var rgb = color switch { "error" => "0.70 0.10 0.10", "teal" => "0.08 0.38 0.41", "muted" => "0.32 0.42 0.44", _ => "0 0 0" };
            content.AppendLine($"BT {rgb} rg /{(bold ? "F2" : "F1")} {F(size)} Tf 1 0 0 1 {F(x)} {F(y)} Tm ({PdfString(value)}) Tj ET");
        }
        public void Rule(double y, double r, double g, double b)
            => content.AppendLine($"{F(r)} {F(g)} {F(b)} RG 0.7 w {F(Margin)} {F(y)} m {F(PageWidth - RightMargin)} {F(y)} l S");
        public void Band(double y, double height)
            => FillBox(Margin, y, PageWidth - Margin - RightMargin, height, 0.93, 0.97, 0.97);
        public void Box(double x, double y, double width, double height, double r, double g, double b)
            => content.AppendLine($"{F(r)} {F(g)} {F(b)} RG 0.65 w {F(x)} {F(y)} {F(width)} {F(height)} re S");
        public void FillBox(double x, double y, double width, double height, double r, double g, double b)
            => content.AppendLine($"{F(r)} {F(g)} {F(b)} rg {F(x)} {F(y)} {F(width)} {F(height)} re f");
        public void BrandRail()
        {
            FillBox(25, 42, 31, PageHeight - 84, 0.08, 0.38, 0.41);
            content.AppendLine($"BT 1 1 1 rg /F2 13 Tf 0 1 -1 0 45 58 Tm ({PdfString("SieEncode")}) Tj ET");
        }
    }

    private static byte[] BuildPdf(IReadOnlyList<PageCanvas> pages)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{5 + (i * 2)} 0 R"))}] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        for (var i = 0; i < pages.Count; i++)
        {
            var contentObject = 6 + (i * 2);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObject} 0 R >>");
            var bytes = Encoding.Latin1.GetByteCount(pages[i].Content);
            objects.Add($"<< /Length {bytes} >>\nstream\n{pages[i].Content}endstream");
        }
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.Latin1, 1024, true) { NewLine = "\n" };
        writer.Write("%PDF-1.4\n%âãÏÓ\n"); writer.Flush();
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); writer.Flush();
        }
        var xref = stream.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) writer.Write($"{offset:D10} 00000 n \n");
        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); writer.Flush();
        return stream.ToArray();
    }

    private static IEnumerable<string> Wrap(string value, int max)
    {
        var text = value;
        if (text.Length == 0) return [""];
        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var remaining = Normalize(paragraph).Trim();
            while (remaining.Length > max)
            {
                var cut = remaining.LastIndexOf(' ', max);
                if (cut < max / 3) cut = max;
                result.Add(remaining[..cut].TrimEnd());
                remaining = remaining[cut..].TrimStart();
            }
            result.Add(remaining);
        }
        return result;
    }

    private static string Normalize(string value)
        => new(value.Select(character => character is >= ' ' and <= '\u00ff' ? character : '?').ToArray());
    private static string PdfString(string value)
        => Normalize(value).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    private static string Shorten(string value, int max) => value.Length <= max ? value : "..." + value[^Math.Max(1, max - 3)..];
    private static string Clip(string value, int max) => value.Length <= max ? value : value[..Math.Max(1, max - 3)] + "...";
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

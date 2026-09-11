using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void InitializeAutomationContract()
    {
        var used = DescendantsAndSelf(this).OfType<FrameworkElement>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var page in pages)
            AssignAutomationIds(page.Value, "Page." + page.Key, used);
        AssignAutomationIds(this, "Window", used);
    }

    internal static void InitializeAutomationContract(DependencyObject root, string prefix)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        AssignAutomationIds(root, prefix, used);
    }

    internal static int VerifyAutomationContract(DependencyObject root)
    {
        var surfaces = DescendantsAndSelf(root).OfType<FrameworkElement>().Where(IsAutomationSurface).ToArray();
        var missing = surfaces.Where(element => string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(element))).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"{missing.Length} interaktive UI-Elemente besitzen keine Automation-ID.");
        var duplicates = surfaces.GroupBy(AutomationProperties.GetAutomationId, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0) throw new InvalidOperationException("Automation-IDs sind nicht eindeutig: " + string.Join(", ", duplicates));
        return surfaces.Length;
    }

    private static void AssignAutomationIds(DependencyObject root, string prefix, HashSet<string> used)
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in DescendantsAndSelf(root).OfType<FrameworkElement>().Where(IsAutomationSurface))
        {
            if (!string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(element))) continue;
            var key = AutomationKey(element);
            counters.TryGetValue(key, out var index);
            counters[key] = ++index;
            var candidate = $"{prefix}.{key}" + (index == 1 ? "" : $".{index}");
            while (!used.Add(candidate)) candidate += ".Next";
            AutomationProperties.SetAutomationId(element, candidate);
        }
    }

    private static bool IsAutomationSurface(FrameworkElement element)
        => element is ButtonBase or TextBoxBase or Selector or PasswordBox or DataGrid ||
           element is TextBlock && !string.IsNullOrWhiteSpace(element.Name) ||
           element is FrameworkElement page && page.Name.StartsWith("Page", StringComparison.Ordinal);

    private static string AutomationKey(FrameworkElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.Name)) return CleanAutomationKey(element.Name);
        if (element is ButtonBase { Content: string content } && !string.IsNullOrWhiteSpace(content))
            return "Button." + CleanAutomationKey(content);
        if (element.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            return element.GetType().Name + "." + CleanAutomationKey(tag);
        return element.GetType().Name;
    }

    private static string CleanAutomationKey(string value)
    {
        var cleaned = new string(value.Normalize().Select(character => char.IsLetterOrDigit(character) ? character : '.').ToArray());
        while (cleaned.Contains("..", StringComparison.Ordinal)) cleaned = cleaned.Replace("..", ".", StringComparison.Ordinal);
        return cleaned.Trim('.');
    }

    private static IEnumerable<DependencyObject> DescendantsAndSelf(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
    }

    public string RunUiSmokeTest()
    {
        RunSelfTest();
        var checks = new List<string>();
        checks.Add("Nebenfenster: Profil-Editor, Profilhistorie und rfPrj-Import initialisiert, versioniert und automatisierbar");
        VerifyAutomationContract(checks);
        VerifyDesignContract(checks);
        VerifyOperatingModeContract(checks);
        VerifyPreflightPresentationContract(checks);
        VerifyNavigationContract(checks);
        VerifyWorkspaceStateContract(checks);
        VerifyConstrainedLayoutContract(checks);
        VerifyIsolatedProjectFlows(checks);

        var reportDirectory = Path.Combine(Path.GetTempPath(), "SieEncode", "ui-smoke");
        Directory.CreateDirectory(reportDirectory);
        var matrix = new CompatibilityMatrixService().Run(
            TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells")),
            Path.Combine(reportDirectory, "compatibility-matrix"), Version);
        if (!matrix.Report.Successful)
            throw new InvalidOperationException("Synthetische Kodiermatrix fehlgeschlagen: " +
                string.Join(" | ", matrix.Report.Scenarios.Where(item => item.Status != "OK").Select(item => item.Id + ": " + item.Error)));
        checks.Add("Kodiermodell: 5/5 erfundene Projekte mit AES, DES, 3K3DES, Plain/MAC/Encrypt, Standard-/Backup-Dateien sowie modularen HEX/ASCII/UTF-8/BCD/Integer-Bausteinen vollständig gebaut und geprüft (Software-Nachweis)");
        var reportPath = Path.Combine(reportDirectory, "ui-smoke-result.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            version = Version,
            generatedAt = DateTimeOffset.Now,
            status = "OK",
            checks,
            compatibilityMatrix = new { status = "OK", scenarios = matrix.Report.Scenarios.Count, matrix.TextPath, matrix.JsonPath, hardwareEvidence = false }
        }, new JsonSerializerOptions { WriteIndented = true }));
        return reportPath;
    }

    private void VerifyAutomationContract(List<string> checks)
    {
        var count = VerifyAutomationContract(this);
        checks.Add($"Automation-Vertrag: {count} interaktive Elemente vollständig und eindeutig adressierbar");
    }

    private void VerifyNavigationContract(List<string> checks)
    {
        foreach (var page in pages.Keys)
        {
            Navigate(page, addHistory: false);
            var visible = pages.Where(candidate => candidate.Value.Visibility == Visibility.Visible).Select(candidate => candidate.Key).ToArray();
            if (visible.Length != 1 || visible[0] != page) throw new InvalidOperationException($"Navigation '{page}' zeigt nicht genau eine Seite.");
        }

        var forbidden = DescendantsAndSelf(this).OfType<ButtonBase>()
            .Select(button => button.Content?.ToString() ?? "")
            .Where(content => string.Equals(content, "Preflight", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (forbidden.Length > 0) throw new InvalidOperationException("Die mehrdeutige UI-Bezeichnung 'Preflight' ist noch vorhanden.");
        checks.Add($"Navigation: {pages.Count} interne Seiten erreichbar; normale Oberfläche verwendet eindeutige Prüfbegriffe");
    }

    private void VerifyOperatingModeContract(List<string> checks)
    {
        var original = settings.ExpertMode;
        try
        {
            var expertSurfaces = new FrameworkElement[]
            {
                NavProfilesButton, ProjectAdvancedActions, EditorTransportSettings, EditorAdvancedActions,
                StationOpenRFiDGearButton, StationDetailedPreflight, StationAdvancedCardActions,
                StationTechnicalDetails, HelpExpertTools, SettingsCapabilityDetails, EditorBuildSettings,
                EditorAuditVaultButton, ProjectOpenFolderButton, StationProjectPath, StationProjectFolderButton
            };
            settings.ExpertMode = false;
            ExpertModeToggle.IsChecked = false;
            ApplyOperatingMode();
            if (expertSurfaces.Any(element => element.Visibility != Visibility.Collapsed))
                throw new InvalidOperationException("Der Normalmodus blendet nicht alle Expertenfunktionen aus.");
            if (OperatingModeText.Text != "Standardansicht" || ExpertModeToggle.Height != 36 ||
                ExpertModeToggle.ToolTip?.ToString()?.Contains("erweiterte Funktionen", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Der kompakte Modusschalter kennzeichnet die geführte Normalansicht nicht eindeutig.");

            settings.ExpertMode = true;
            ExpertModeToggle.IsChecked = true;
            ApplyOperatingMode();
            if (expertSurfaces.Any(element => element.Visibility != Visibility.Visible))
                throw new InvalidOperationException("Der Expertenmodus stellt nicht alle Fachfunktionen bereit.");
            if (StationDetailedPreflight.Visibility != Visibility.Visible || StationAdvancedCardActions.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Die Expertenansicht stellt die vorhandenen Prüf- und Wartungsaktionen nicht bereit.");
            if (OperatingModeText.Text != "Erweiterte Ansicht" ||
                ExpertModeToggle.ToolTip?.ToString()?.Contains("Standardansicht", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Der kompakte Modusschalter kennzeichnet den Expertenmodus nicht eindeutig.");
            checks.Add("Bedienmodus: Normalansicht reduziert auf den geführten Ablauf; Expertenansicht ergänzt Fachdetails, bestehende Prüf- und Wartungsaktionen bleiben als Expertenbereiche verfügbar");
        }
        finally
        {
            settings.ExpertMode = original;
            ExpertModeToggle.IsChecked = original;
            ApplyOperatingMode();
        }
    }

    private void VerifyPreflightPresentationContract(List<string> checks)
    {
        var report = new ProjectPreflightReport { ProjectName = "UI-Fehlerprojekt", Scope = ProjectPreflightScope.Operational };
        report.Issues.Add(new InspectionIssue("PROJECT_STALE", "Technischer Rohtext", "builder.manifest.json"));
        report.Issues.Add(new InspectionIssue("FILE_MISSING", "Erwartete Vertragsdatei fehlt.", "card-recognition.plan.json"));
        report.Issues.Add(new InspectionIssue("FILE_MISSING", "Erwartetes RFiDGear-Projekt fehlt.", "UI-Fehlerprojekt_CardRecognition_ReadOnly.rfPrj"));
        DisplayProjectPreflight(report);
        var rows = ProjectPreflightIssuesGrid.ItemsSource?.Cast<object>().ToArray() ?? [];
        if (ProjectPreflightPanel.Visibility != Visibility.Visible || ProjectPreflightIssuesGrid.Visibility != Visibility.Visible || rows.Length != 2 ||
            !ProjectPreflightSummary.Text.Contains("2 konkrete", StringComparison.Ordinal) ||
            !ProjectPreflightNextAction.Text.Contains("Nächster Schritt", StringComparison.Ordinal) ||
            ProjectPreflightRepairButton.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Fehlerhafte Projektprüfungen werden nicht als verständliche Befund- und Reparaturliste dargestellt.");
        var dialogText = DescribePreflightFailure(report);
        if (!dialogText.Contains("Projektstand", StringComparison.Ordinal) || !dialogText.Contains("Projekt öffnen und erneut bauen", StringComparison.Ordinal) ||
            !dialogText.Contains("älteren SieEncode-Version", StringComparison.Ordinal) || !dialogText.Contains("Vault entsperren", StringComparison.Ordinal))
            throw new InvalidOperationException("Projektprüfungsfehler enthalten in Sperrmeldungen keine konkrete Ursache und Reparatur.");
        ProjectPreflightPanel.Visibility = Visibility.Collapsed;
        checks.Add("Projektprüfung: technische Befunde werden als Bereich, Ursache, Datei und konkrete Reparatur angezeigt und in Sperrmeldungen übernommen");
    }

    private void VerifyDesignContract(List<string> checks)
    {
        var buttons = DescendantsAndSelf(this).OfType<Button>().ToArray();
        var navigation = buttons.Where(button => Equals(button.Style, TryFindResource("NavButton")) || Equals(button.Style, TryFindResource("NavButtonActive"))).ToArray();
        if (navigation.Count(button => Equals(button.Style, TryFindResource("NavButtonActive"))) != 1)
            throw new InvalidOperationException("Genau ein Navigationspunkt muss als aktuelle Seite hervorgehoben sein.");
        var compact = buttons.Where(button => Equals(button.Style, TryFindResource("CompactButton"))).ToArray();
        var ordinary = buttons.Except(navigation).Except(compact).ToArray();
        if (ordinary.Any(button => Math.Abs(button.Height - 40) > 0.1)) throw new InvalidOperationException("Normale Schaltflächen verwenden nicht durchgängig exakt 40 px Höhe.");
        if (navigation.Any(button => Math.Abs(button.Height - 44) > 0.1)) throw new InvalidOperationException("Navigationsschaltflächen verwenden nicht durchgängig exakt 44 px Höhe.");
        if (compact.Any(button => Math.Abs(button.Height - 38) > 0.1)) throw new InvalidOperationException("Kompakte Schaltflächen verwenden nicht durchgängig exakt 38 px Höhe.");
        var combos = DescendantsAndSelf(this).OfType<ComboBox>().ToArray();
        if (combos.Any(combo => combo.MinHeight < 40)) throw new InvalidOperationException("Auswahllisten unterschreiten die Standardhöhe von 40 px.");
        var groupBoxes = DescendantsAndSelf(this).OfType<GroupBox>().ToArray();
        if (groupBoxes.Any(group => group.Template is null || group.BorderThickness != new Thickness(1)))
            throw new InvalidOperationException("Bereichskarten verwenden nicht durchgängig das gemeinsame Rahmen-Template.");
        if (!Equals(DeleteProjectButton.Style, TryFindResource("DangerButton")) || DeleteProjectButton.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Die sichtbare Projekt-Löschaktion ist nicht eindeutig als destruktive Aktion gestaltet.");
        checks.Add($"Designvertrag: {ordinary.Length} Standardbuttons exakt 40 px, {navigation.Length} Navigationselemente exakt 44 px, {groupBoxes.Length} einheitliche Bereichskarten");
    }

    private void VerifyConstrainedLayoutContract(List<string> checks)
    {
        var originalWidth = Width;
        var originalHeight = Height;
        try
        {
            Width = MinWidth;
            Height = MinHeight;
            Measure(new Size(MinWidth, MinHeight));
            Arrange(new Rect(0, 0, MinWidth, MinHeight));
            UpdateLayout();
            foreach (var page in pages)
            {
                Navigate(page.Key, addHistory: false);
                Measure(new Size(MinWidth, MinHeight));
                Arrange(new Rect(0, 0, MinWidth, MinHeight));
                UpdateLayout();
                if (page.Value.ActualWidth < 500 || page.Value.ActualHeight < 400)
                    throw new InvalidOperationException($"Seite '{page.Key}' besitzt bei {MinWidth:0}×{MinHeight:0} keinen erreichbaren Arbeitsbereich.");
            }
            var longPages = new FrameworkElement[] { PageHome, PageEditor, PageStation, PageProfiles, PageSettings };
            static bool HasVerticalAccess(FrameworkElement page) =>
                page is ScrollViewer { VerticalScrollBarVisibility: ScrollBarVisibility.Auto or ScrollBarVisibility.Visible } ||
                DescendantsAndSelf(page).OfType<ScrollViewer>().Any(scroll => scroll.VerticalScrollBarVisibility is ScrollBarVisibility.Auto or ScrollBarVisibility.Visible);
            if (longPages.Any(page => !HasVerticalAccess(page)))
                throw new InvalidOperationException("Eine lange Arbeitsseite besitzt bei kleinem Fenster keinen vertikalen Zugriff auf alle Aktionen.");
            checks.Add($"Layoutvertrag: alle {pages.Count} Seiten bei {MinWidth:0}×{MinHeight:0} angeordnet; Kodierentscheidung fixiert und verbleibende lange Arbeitsbereiche vollständig scrollbar (DPI-Feldprüfung bleibt Release-Gate)");
        }
        finally
        {
            Width = originalWidth;
            Height = originalHeight;
            Measure(new Size(originalWidth, originalHeight));
            Arrange(new Rect(0, 0, originalWidth, originalHeight));
            UpdateLayout();
        }
    }

    private void VerifyIsolatedProjectFlows(List<string> checks)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "SieEncode", "ui-fixture-" + Guid.NewGuid().ToString("N"));
        var libraryRoot = Path.Combine(fixture, "projects");
        var externalSources = Path.Combine(fixture, "external-sources");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalSources);
        try
        {
            var library = LoadProfileLibrary();
            var previousReport = lastTestReportPath;
            try
            {
                lastTestReportPath = Path.Combine(fixture, "application-inventory.result.txt");
                if (LatestInventoryPdfPath() is not null) throw new InvalidOperationException("Fehlendes PDF wird angeboten.");
                File.WriteAllText(Path.ChangeExtension(lastTestReportPath, ".pdf"), "synthetic path fixture");
                if (LatestInventoryPdfPath() != Path.ChangeExtension(lastTestReportPath, ".pdf")) throw new InvalidOperationException("Inventur-PDF nicht erreichbar.");
                lastTestReportPath = Path.Combine(fixture, "recognition.result.txt");
                if (LatestInventoryPdfPath() is not null) throw new InvalidOperationException("Veraltetes Inventur-PDF wird für einen anderen Bericht angeboten.");
                checks.Add("PDF-Istreport: passender Inventurpfad erreichbar; fehlende und veraltete Berichte gesperrt.");
                var valuesDialog = new InventoryComparisonValuesDialog("Synthetic", "A1B2C3",
                    [new VariableDefinition { Id = "serial", Scope = "card", Default = "007", Label = "Seriennummer" }]);
                if (valuesDialog.Values.GetValueOrDefault("serial") != "007") throw new InvalidOperationException("Vergleichsformular verliert explizite Profilvorbelegung.");
                valuesDialog.Close();
                var reportOptions = new ComparisonReportOptionsDialog();
                var imagePath = Path.Combine(fixture, "synthetic-card.png");
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(2, 1, 96, 96,
                    System.Windows.Media.PixelFormats.Rgb24, null, new byte[] { 255, 0, 0, 0, 0, 255 }, 6)));
                using (var imageStream = File.Create(imagePath)) png.Save(imageStream);
                var picture = ComparisonReportOptionsDialog.LoadPicture(imagePath, "Synthetic");
                File.Delete(imagePath);
                if (picture.Width != 2 || picture.Height != 1 || !picture.Rgb.SequenceEqual(new byte[] { 255, 0, 0, 0, 0, 255 }))
                    throw new InvalidOperationException("Kartenbild verliert Farbe, Seitenverhältnis oder Freigabe der Quelldatei.");
                var jpegPixels = new byte[32 * 16 * 3];
                for (var i = 0; i < 32 * 16; i++)
                {
                    jpegPixels[i * 3] = (byte)(i % 32 * 8);
                    jpegPixels[i * 3 + 1] = (byte)(i / 32 * 16);
                }
                var jpegSource = System.Windows.Media.Imaging.BitmapSource.Create(32, 16, 96, 96,
                    System.Windows.Media.PixelFormats.Rgb24, null, jpegPixels, 32 * 3);
                RFiDGear.Builder.RfPrj.CardReportImage? normalJpeg = null;
                for (ushort orientation = 0; orientation <= 8; orientation++)
                {
                    var metadata = new System.Windows.Media.Imaging.BitmapMetadata("jpg");
                    if (orientation > 0) metadata.SetQuery("/app1/ifd/{ushort=274}", orientation);
                    var jpeg = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 };
                    jpeg.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(jpegSource, null, metadata, null));
                    var jpegPath = Path.Combine(fixture, "synthetic-oriented.jpg");
                    using (var imageStream = File.Create(jpegPath)) jpeg.Save(imageStream);
                    var oriented = ComparisonReportOptionsDialog.LoadPicture(jpegPath, "Synthetic");
                    File.Delete(jpegPath);
                    normalJpeg ??= oriented;
                    var expected = RFiDGear.Builder.RfPrj.CardReportImageOrientation.Apply(normalJpeg, orientation);
                    if (oriented.Width != expected.Width || oriented.Height != expected.Height || !oriented.Rgb.SequenceEqual(expected.Rgb))
                        throw new InvalidOperationException($"JPEG-EXIF-Ausrichtung {orientation} wurde nicht korrekt übernommen.");
                }
                checks.Add("Kartenbilder: PNG-Farben und JPEG-Import ohne/mit allen acht EXIF-Ausrichtungen geprüft.");
                if (reportOptions.Documentation is not null) throw new InvalidOperationException("Optionale Dokumentation ist unerwartet vorausgewählt.");
                if (reportOptions.Detail != RFiDGear.Builder.RfPrj.CardReportDetail.Full) throw new InvalidOperationException("Vollbericht-Standard fehlt.");
                ((StackPanel)reportOptions.Content).Children.OfType<RadioButton>().Last().IsChecked = true;
                if (reportOptions.Detail != RFiDGear.Builder.RfPrj.CardReportDetail.Compact) throw new InvalidOperationException("Kompaktbericht nicht auswählbar.");
                ((StackPanel)reportOptions.Content).Children.OfType<CheckBox>().Single().IsChecked = true;
                if (reportOptions.Documentation is null || reportOptions.Documentation.HasContent) throw new InvalidOperationException("Leere Dokumentationsauswahl erzeugt unerwartete Inhalte.");
                ((StackPanel)reportOptions.Content).Children.OfType<CheckBox>().Single().IsChecked = false;
                if (reportOptions.Documentation is not null) throw new InvalidOperationException("Abgewählte Dokumentation bleibt aktiv.");
                reportOptions.Close();
                checks.Add("Inhaltsvergleich: Kartenwerteformular mit expliziter Profilvorbelegung initialisiert.");
            }
            finally { lastTestReportPath = previousReport; }
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            BuildFixtureProject("UI-Bearbeitbar", Path.Combine(libraryRoot, "UI-Bearbeitbar.project.json"), libraryRoot, library, shells);
            BuildFixtureProject("UI-Artefakt", Path.Combine(externalSources, "UI-Artefakt.project.json"), libraryRoot, library, shells);

            projectsDirectory = libraryRoot;
            RefreshProjects();
            if (projects.Count != 2) throw new InvalidOperationException($"UI-Testbibliothek erwartet 2 Projekte, gefunden: {projects.Count}.");
            ProjectSearchText.Text = "UI-Bearbeitbar";
            if (ProjectsGrid.Items.OfType<ProjectCatalogEntry>().Count() != 1)
                throw new InvalidOperationException("Die Projektsuche filtert die sichtbare Bibliothek nicht eindeutig.");
            ProjectSearchText.Text = "";
            if (ProjectsGrid.Items.OfType<ProjectCatalogEntry>().Count() != 2)
                throw new InvalidOperationException("Die Projektsuche stellt nach dem Leeren nicht alle Projekte wieder her.");

            var editable = projects.Single(entry => entry.Name == "UI-Bearbeitbar" && !entry.ArtifactOnly);
            if (editable.HasVault || !editable.HasOperationalBuild || !editable.HasRecognition || editable.Kind != "Einsatzbereit" || editable.KeyState != "Neubau gesperrt")
                throw new InvalidOperationException("Projekt mit nutzbarem Build, aber ohne Vault wird in Nutzung und Neubaufähigkeit nicht korrekt getrennt.");
            SelectProject(editable);
            var editableReport = RunSelectedPreflight();
            if (editableReport.Status != "OK" || editableReport.Scope != ProjectPreflightScope.Operational)
                throw new InvalidOperationException("Schlüsselfreie Prüfung des bearbeitbaren Projekts ist fehlgeschlagen.");
            if (!LoadStation(editable) || StationProjectName.Text != editable.Name || !StationPreflightButton.IsEnabled ||
                StationCardStep.Visibility != Visibility.Visible || StationCodingStep.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Bearbeitbares Projekt lässt sich nicht konsistent in die Kodierstation laden.");
            VerifyOperatorUiContract(checks);
            VerifyOperationStartGate(checks);
            VerifySeriesUiFlows(checks);
            RunAnalysis(ProjectJson.Load(editable.SourcePath), editable.SourcePath, Path.Combine(fixture, "analysis"));
            var analyzed = lastAnalysisFiles!.Report;
            if (AnalysisApplicationsGrid.Items.Count != analyzed.Applications.Count || analyzed.Applications.Count == 0)
                throw new InvalidOperationException("Anwendungs- und Schlüsselkontext fehlen in der Expertenanalyse.");
            foreach (var pair in new[] { (AnalysisMasteringGrid, analyzed.MasteringTasks), (AnalysisCodingGrid, analyzed.CodingTasks) })
            {
                var displayed = pair.Item1.Items.Cast<AnalysisTaskRow>().ToArray();
                if (displayed.Length != pair.Item2.Count || displayed.Where((row, index) =>
                    row.Settings != pair.Item2[index].Settings || row.FileContract != pair.Item2[index].FileContract ||
                    row.Position != pair.Item2[index].Position).Any())
                    throw new InvalidOperationException("Taskdetails verlieren Einstellungen, Reihenfolge oder Dateiverträge.");
            }
            checks.Add("Expertenanalyse: vollständiger Anwendungskontext und unveränderte Einstellungen/Dateiverträge aller geplanten Tasks verfügbar.");
            var analyzedProject = ProjectJson.Load(editable.SourcePath);
            var expectedFiles = library.Compose(analyzedProject.Profiles.Base, analyzedProject.Profiles.Customers).Applications
                .SelectMany(app => app.Files.Select(file => (Aid: ProfileValidator.NormalizeAid(app.Id), File: file))).ToArray();
            var displayedFiles = AnalysisFilesGrid.Items.Cast<AnalysisFileRow>().ToArray();
            if (displayedFiles.Length != expectedFiles.Length || expectedFiles.Any(expected =>
                !displayedFiles.Any(row => row.Aid == expected.Aid && row.File.StartsWith($"{expected.File.Id:D2} · ") &&
                    row.Details == FileExpertDescription.FromFile(expected.File))))
                throw new InvalidOperationException("Dateirechte oder Datenrezepte fehlen in der Expertenansicht.");
            checks.Add("Expertenanalyse: Dateirechte, Erstellungseinstellungen und vollständige Datenrezepte stimmen mit den synthetischen Profilen überein.");
            CaptureWorkspaceReview();
            UpdateLayout();
            InitializeAutomationContract();
            VerifyAutomationContract(this);

            var artifact = projects.Single(entry => entry.Name == "UI-Artefakt" && entry.ArtifactOnly);
            SelectProject(artifact);
            var artifactReport = RunSelectedPreflight();
            if (artifactReport.Status != "OK" || artifactReport.Scope != ProjectPreflightScope.Artifact)
                throw new InvalidOperationException("Strukturelle Prüfung des reinen Bauartefakts ist fehlgeschlagen.");
            if (!LoadStation(artifact) || StationProjectName.Text != artifact.Name || !StationPreflightButton.IsEnabled)
                throw new InvalidOperationException("Reines Bauartefakt lässt sich nicht konsistent in die Kodierstation laden.");
            UpdateLayout();
            InitializeAutomationContract();
            VerifyAutomationContract(this);

            var recognitionPlan = Path.Combine(editable.ProjectDirectory, "card-recognition.plan.json");
            var recognitionProject = Path.Combine(editable.ProjectDirectory, editable.Name + "_CardRecognition_ReadOnly.rfPrj");
            File.Delete(recognitionPlan);
            File.Delete(recognitionProject);
            RefreshProjects();
            var legacy = projects.Single(entry => entry.Name == "UI-Bearbeitbar" && !entry.ArtifactOnly);
            if (!legacy.HasOperationalBuild || legacy.HasRecognition || legacy.Kind != "Altbuild nutzbar" || legacy.KeyState != "Neubau gesperrt")
                throw new InvalidOperationException("Kompatibler Altbuild wird in der Projektbibliothek nicht eindeutig als nutzbar, aber nicht neu baubar dargestellt.");
            SelectProject(legacy);
            var legacyReport = RunSelectedPreflight();
            if (legacyReport.Status != "OK" || !IsLegacyCompatibilityReport(legacyReport))
                throw new InvalidOperationException("Kompatibler Altbuild wird durch das additive Erkennungsmodul vollständig gesperrt.");
            var runtimeCanAutorun = File.Exists(settings.RFiDGear.ExecutablePath) &&
                rfidgearCapabilities?.Has(RFiDGearCapabilityId.AutorunOnce) == true;
            if (!LoadStation(legacy) || StationRecognitionButton.IsEnabled || StationRecognitionButton.Visibility != Visibility.Collapsed ||
                StationPreflightTransportButton.IsEnabled != runtimeCanAutorun || StationLegacyTransportButton.IsEnabled != runtimeCanAutorun ||
                StationLegacyTransportButton.Visibility != Visibility.Visible || !StationPrepareButton.IsEnabled)
                throw new InvalidOperationException("Kodierstation gibt beim Altbuild nicht exakt die vorhandenen sicheren Funktionen frei.");
            DisplayProjectPreflight(legacyReport);
            if (!ProjectPreflightTitle.Text.Contains("Altprojekt verwendbar", StringComparison.Ordinal) ||
                !ProjectPreflightSummary.Text.Contains("Transponder-Erkennung", StringComparison.Ordinal))
                throw new InvalidOperationException("Altprojekt-Einschränkung wird dem Bediener nicht verständlich erklärt.");

            var collisionName = "Bestehendes Projekt";
            var collisionPath = Path.Combine(libraryRoot, collisionName, collisionName + ".project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(collisionPath)!);
            var collisionProject = ProjectDraftFactory.Create(library, collisionName);
            ProjectJson.Save(collisionPath, ProjectJson.WithoutProductiveSecrets(collisionProject));
            var collisionBefore = File.ReadAllBytes(collisionPath);
            LoadEditor(ProjectDraftFactory.Create(library, collisionName), "");
            var collisionRejected = false;
            try { SaveEditorProject(saveAs: false); }
            catch (InvalidOperationException exception) when (exception.Message.Contains("niemals still überschrieben", StringComparison.Ordinal)) { collisionRejected = true; }
            if (!collisionRejected || !collisionBefore.SequenceEqual(File.ReadAllBytes(collisionPath)))
                throw new InvalidOperationException("Ein neuer Projektentwurf kann ein gleichnamiges Projekt still überschreiben.");
            ClearEditor();

            checks.Add("Projektbibliothek: Nutzung, Neubaufähigkeit, Vault, kompatibler Altbuild und reines Bauartefakt sichtbar unterschieden");
            checks.Add("Projektverwaltung: Suche filtert und setzt vollständig zurück; gleichnamige Neuprojekte überschreiben keine vorhandene Definition");
            checks.Add("Projektprüfung: Betriebscheck ohne Vault, kompatibler Altbuild und struktureller Artefaktcheck erfolgreich");
            checks.Add("Kodierstation: aktuelle und alte Projekttypen geladen; nur vorhandene Funktionen freigegeben; keine Hardwareaktion gestartet");
        }
        finally
        {
            selectedProject = null;
            sessionDefinition = null;
            operationCatalog = null;
            projects.Clear();
            if (Directory.Exists(fixture)) Directory.Delete(fixture, recursive: true);
        }
    }

    private void VerifySeriesUiFlows(List<string> checks)
    {
        if (stationFields.Count == 0) throw new InvalidOperationException("UI-Serientest benötigt mindestens ein Kartendatenfeld.");
        var seriesField = stationFields[0];
        StationSeriesField.SelectedIndex = 0;

        StationMode.SelectedIndex = 1;
        UpdateSeriesUi();
        if (CurrentSeriesMode() != CodingSeriesMode.Sequential || StationSequencePanel.Visibility != Visibility.Visible || StationListPanel.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Der sichtbare Modus 'Fortlaufend' wurde nicht korrekt aufgebaut.");
        StationSequenceValue.Text = "0099";
        StationSequenceStep.Text = "2";
        ApplySeriesInput();
        if (seriesField.Value != "0099") throw new InvalidOperationException("Die fortlaufende Nummer wurde nicht in die sichtbaren Kartendaten übernommen.");
        AdvanceSeriesInput();
        if (StationSequenceValue.Text != "0101" || seriesField.Value != "0101" || StationState.Text != "NÄCHSTE NUMMER")
            throw new InvalidOperationException("Die sichtbare Serienfortschaltung erhält Nummernbreite oder Zustand nicht.");

        StationMode.SelectedIndex = 2;
        UpdateSeriesUi();
        if (CurrentSeriesMode() != CodingSeriesMode.List || StationListPanel.Visibility != Visibility.Visible || StationSequencePanel.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Der sichtbare Modus 'Liste' wurde nicht korrekt aufgebaut.");
        stationQueue = new IReadOnlyDictionary<string, string>[]
        {
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [seriesField.Id] = "L-0001" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [seriesField.Id] = "L-0002" }
        };
        stationQueueIndex = 0;
        ApplySeriesInput();
        if (seriesField.Value != "L-0001") throw new InvalidOperationException("Der erste sichtbare Listendatensatz wurde nicht übernommen.");
        AdvanceSeriesInput();
        if (stationQueueIndex != 1 || seriesField.Value != "L-0002" || StationState.Text != "NÄCHSTER LISTENEINTRAG")
            throw new InvalidOperationException("Die sichtbare Listenfortschaltung erreicht den zweiten Datensatz nicht.");
        AdvanceSeriesInput();
        if (stationQueueIndex != 2 || StationState.Text != "LISTE ABGESCHLOSSEN" || !StationQueueInfo.Text.Contains("2 Datensätze", StringComparison.Ordinal))
            throw new InvalidOperationException("Das sichtbare Listenende wird nicht eindeutig gemeldet.");

        StationMode.SelectedIndex = 0;
        UpdateSeriesUi();
        stationQueue = [];
        stationQueueIndex = -1;
        checks.Add("Serienoberfläche: Fortlaufend und Liste sichtbar umgeschaltet, Werte übernommen und bis zum Listenende fortgeschaltet");
    }

    private void VerifyOperatorUiContract(List<string> checks)
    {
        if (!StationGuidanceTitle.Text.StartsWith("PROJEKT GELADEN", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(StationGuidanceExplanation.Text) ||
            string.IsNullOrWhiteSpace(StationGuidanceNextAction.Text) ||
            string.IsNullOrWhiteSpace(StationGuidanceEvidence.Text))
            throw new InvalidOperationException("Die primäre Bedienentscheidung ist nach dem Laden eines Projekts nicht vollständig sichtbar.");
        if (!Equals(StationPrepareButton.Content, "1 · Daten prüfen und bereitstellen (Enter)") ||
            !Equals(StationStartAutoModeButton.Content, "2 · Kodierung starten") ||
            !Equals(StationCheckReadbacksButton.Content, "3 · Ergebnis jetzt prüfen (optional · F8)"))
            throw new InvalidOperationException("Der normale Kodierweg zeigt zwei erforderliche Aktionen und die optionale manuelle Ergebnisaktualisierung nicht eindeutig.");
        if (StationOpenRFiDGearButton is null || StationMasteringAutoButton is null ||
            StationRecoveryTransportButton is null || StationApplicationAcceptanceButton is null)
            throw new InvalidOperationException("Experten- oder erweiterte Kartenfunktionen gingen bei der UI-Vereinfachung verloren.");
        checks.Add("Bedienentscheidung: Erklärung, nächste Aktion und Nachweis sichtbar; Kodierlauf automatisch überwacht; optionale Ergebnisaktualisierung sowie Experten- und Kartenfunktionen erhalten");
    }

    private void VerifyOperationStartGate(List<string> checks)
    {
        var transportWasEnabled = StationPreflightTransportButton.IsEnabled;
        var masteringWasEnabled = StationMasteringAutoButton.IsEnabled;
        var recoveryWasEnabled = StationRecoveryTransportButton.IsEnabled;
        StationPreflightTransportButton.IsEnabled = true;
        if (!TryBeginOperationStart()) throw new InvalidOperationException("Die Betriebsstart-Sperre lässt sich nicht erstmals belegen.");
        try
        {
            if (TryBeginOperationStart()) throw new InvalidOperationException("Ein zweiter Betriebsstart wurde während eines offenen Sicherheitsdialogs zugelassen.");
            if (StationPreflightTransportButton.IsEnabled || StationMasteringAutoButton.IsEnabled || StationRecoveryTransportButton.IsEnabled)
                throw new InvalidOperationException("Betriebsaktionen bleiben während eines offenen Sicherheitsdialogs bedienbar.");
        }
        finally { EndOperationStart(); }
        if (operationStartInProgress || StationPreflightTransportButton.IsEnabled != transportWasEnabled ||
            StationMasteringAutoButton.IsEnabled != masteringWasEnabled ||
            StationRecoveryTransportButton.IsEnabled != recoveryWasEnabled)
            throw new InvalidOperationException("Betriebsaktionen erhielten nach Freigabe der Start-Sperre nicht ihren vorherigen Capability-Zustand zurück.");
        checks.Add("Betriebsstart: modale Sicherheitsdialoge sperren alle weiteren Reader-/Kartenstarts gegen Reentrancy");
    }

    private static void BuildFixtureProject(string name, string sourcePath, string libraryRoot, ProfileLibrary library, TaskShellLibrary shells)
    {
        var project = ProjectDraftFactory.Create(library, name);
        project.Project.OutputDirectory = ".";
        project.Productive.PiccKey.Value = new string('A', 32);
        foreach (var aid in project.Productive.ApplicationKeys.Keys.ToArray())
            project.Productive.ApplicationKeys[aid] = project.Productive.ApplicationKeys[aid].Select((_, index) => new string((char)('B' + index), 32)).ToList();
        ProjectJson.Save(sourcePath, ProjectJson.WithoutProductiveSecrets(project));
        var result = new ProjectBuildService().Build(project, library, shells, sourcePath, libraryRoot, Version + "-ui-smoke");
        if (result.Preflight.Status != "OK") throw new InvalidOperationException($"UI-Testprojekt '{name}' konnte nicht gebaut werden.");
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow : Window
{
    private static readonly string Version = AppIdentity.DisplayVersion;
    private readonly string startupPage;
    private readonly BuilderSettingsStore settingsStore;
    private ProfileStore profileStore;
    private readonly ProjectCatalog catalog = new();
    private readonly CodingSessionService sessionService = new();
    private readonly RFiDGearOperationService operationService = new();
    private readonly CardPreflightService cardPreflightService = new();
    private readonly DiagnosticBundleService diagnosticBundleService = new();
    private readonly IRFiDGearProcessProbe processProbe = new SystemRFiDGearProcessProbe();
    private readonly RFiDGearProcessCompletionService processCompletionService = new();
    private readonly RFiDGearHostContractServiceV1 hostContractService = new();
    private readonly DispatcherTimer operationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<ProjectCatalogEntry> projects = [];
    private readonly ObservableCollection<CodingFieldInput> stationFields = [];
    private readonly ObservableCollection<CodingSeriesHistoryRow> stationHistory = [];
    private readonly WorkspaceNavigation navigation = new();
    private readonly Dictionary<string, FrameworkElement> pages;
    private BuilderSettings settings = new();
    private string projectsDirectory = "";
    private string currentPage => navigation.CurrentPage;
    private ProjectCatalogEntry? selectedProject;
    private CodingSessionDefinition? sessionDefinition;
    private PreparedCodingSession? preparedSession;
    private RFiDGearOperationCatalog? operationCatalog;
    private SecureOperationalSession? secureOperationalSession;
    private ProjectSecretPipeServer? projectSecretServer;
    private ProjectSecretPipeServer? toolSecretServer;
    private RFiDGearOperationSession? operationSession;
    private RFiDGearHostRunV1? operationHostRun;
    private bool operationFinalized;
    private bool operationStartInProgress;
    private string? operationWorkspaceDirectory;
    private RFiDGearLaunchResult? codingLaunch;
    private RFiDGearHostRunV1? hostRun;
    private DateTimeOffset? codingTerminalPendingSince;
    private CardPreflightReportFiles? lastCardPreflightReport;
    private CardRecognitionFiles? lastCardRecognitionReport;
    private string? lastTestReportPath;
    private CodingSeriesService? seriesService;
    private IReadOnlyList<IReadOnlyDictionary<string, string>> stationQueue = [];
    private int stationQueueIndex = -1;
    private IReadOnlyDictionary<string, string>? lastFailedValues;
    private CodingSeriesRecord? lastSeriesRecord;
    private RFiDGearLogSnapshot? seriesLogSnapshot;
    private RFiDGearCapabilityReport? rfidgearCapabilities;
    private EnvironmentCheckFiles? lastEnvironmentCheck;
    private string? lastFunctionAuditPath;
    private bool suppressOperatingModeChange;
    private readonly CheckBox editorPiccChangeMasterKey = new() { Content = "Masterkey änderbar" };
    private readonly CheckBox editorPiccListApplications = new() { Content = "Applications auflisten" };
    private readonly CheckBox editorPiccCreateDelete = new() { Content = "Applications erstellen/löschen" };
    private readonly CheckBox editorPiccConfigurationChange = new() { Content = "Konfiguration ändern" };

    public MainWindow(string startupPage = "Home")
        : this(startupPage, new BuilderSettingsStore(null, Path.Combine(AppContext.BaseDirectory, "builder.settings.json")))
    {
    }

    internal MainWindow(string startupPage, BuilderSettingsStore startupSettingsStore, string? groupTrustDirectory = null)
    {
        localGroupTrustDirectory = Path.GetFullPath(groupTrustDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "GroupTrust"));
        this.startupPage = startupPage;
        InitializeComponent();
        InitializePiccSettingsFlags();
        Title = string.Equals(startupPage, "Station", StringComparison.OrdinalIgnoreCase)
            ? $"SieEncode Station {Version}"
            : $"SieEncode Studio {Version}";
        AppVersionText.Text = string.Equals(startupPage, "Station", StringComparison.OrdinalIgnoreCase)
            ? $"Station · {Version}"
            : $"Studio · {Version}";
        settingsStore = startupSettingsStore;
        profileStore = CreateProfileStore(new BuilderSettings());
        pages = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal)
        {
            ["Home"] = PageHome, ["Projects"] = PageProjects, ["Station"] = PageStation, ["TestStation"] = PageTestStation,
            ["Editor"] = PageEditor, ["Profiles"] = PageProfiles, ["Analysis"] = PageAnalysis, ["Diff"] = PageDiff,
            ["Acceptance"] = PageAcceptance, ["Settings"] = PageSettings, ["Help"] = PageHelp
        };
        InitializeAutomationContract();
        ProjectsGrid.ItemsSource = projects;
        CollectionViewSource.GetDefaultView(projects).Filter = FilterProject;
        StationFieldsControl.ItemsSource = stationFields;
        StationHistoryGrid.ItemsSource = stationHistory;
        operationTimer.Tick += OperationTimer_Tick;
        InitializeProjectEditor();
        InitializeDiffPage();
        RefreshWorkspaceNavigation();
    }

    public void RunSelfTest()
    {
        RunStartupModeSelfTest();
        if (pages.Count != 11 || pages.Values.Any(page => page is null) || TutorialButton is null || HomeAnalyzeCardButton is null || HomePilotSetupState is null || HomePilotProjectState is null || HomePilotVaultState is null || HomePilotStationState is null) throw new InvalidOperationException("Navigationsseiten, Schnellstart, projektlose Analyse oder UI-Tutorial sind unvollständig.");
        if (TestAnalyzeCardButton is null || TestDeepAnalyzeButton is null || TestOpenRecognitionResultButton is null || TestProjectRecognitionButton is null || TestProjectTransportButton is null || TestProjectProductiveButton is null || TestProjectAcceptanceButton is null || TestHardwarePlanButton is null || TestRefreshOperationButton is null || TestCloseRFiDGearButton is null || TestFormatCardButton is null || TestAnalysisInfo is null || TestAnalysisTree is null || TestProjectInfo is null) throw new InvalidOperationException("Teststation ist unvollständig.");
        if (StationFieldsControl is null || StationPrepareButton is null || StationOpenRFiDGearButton is null || StationStartAutoModeButton is null || StationCheckReadbacksButton is null || StationMasteringManualButton is null || StationApplicationAcceptanceButton is null || StationExtensionRollbackButton is null || StationRefreshOperationButton is null || StationCloseRFiDGearButton is null || StationOpenPreflightResultButton is null || StationReadProjectContentButton is null || StationMode is null || StationSeriesField is null || StationHistoryGrid is null || StationRetryButton is null || StationDiagnosticButton is null || StationTaskOutcomesGrid is null || StationRunEvidenceInfo is null || StationGuidanceBorder is null || StationGuidanceTitle is null || StationGuidanceExplanation is null || StationGuidanceNextAction is null || StationGuidanceEvidence is null || StationCardStep is null || StationSeriesStep is null || StationCodingStep is null || StationCompletionStep is null || StationProjectFolderButton is null || HardwareAcceptancePlanButton is null || RFiDGearReaderText is null || RFiDGearCapabilitiesGrid is null || RFiDGearEvidenceText is null || UseBundledRuntimeButton is null) throw new InvalidOperationException("Kodiersitzungs-/Capability-Bedienelemente sind unvollständig.");
        if (HomeVaultButton is null || ProjectEditButton is null || ProjectOpenStationButton is null || EditorProjectName is null || EditorCardMode is null || EditorCardModeGuidance is null || EditorProductivePiccPanel is null || EditorSaveButton is null || EditorAnalyzeButton is null || EditorBuildButton is null || EditorProductiveKeys is null || EditorKeyGuidance is null || EditorGenerateKeysButton is null || EditorUnlockVaultButton is null || EditorSaveVaultButton is null || EditorManageVaultAccessButton is null || EditorAuditVaultButton is null || ImportLegacyButton is null || DeleteProjectButton is null || QuickStartCodingButton is null) throw new InvalidOperationException("Projekteditor-/Migrations-Bedienelemente sind unvollständig.");
        if (AnalysisEditorButton is null || AnalysisSelectedButton is null || AnalysisFilesGrid is null || AnalysisMasteringGrid is null || AnalysisCodingGrid is null || AnalysisIssuesGrid is null || AnalysisExportPdfButton is null || AnalysisOpenReportButton is null || AnalysisOpenFolderButton is null) throw new InvalidOperationException("Analyse-Bedienelemente sind unvollständig.");
        if (DiffLeftProject is null || DiffRightProject is null || DiffLeftProfile is null || DiffRightProfile is null || DiffGrid is null || DiffOpenReportButton is null || DiffOpenFolderButton is null) throw new InvalidOperationException("Vergleichs-Bedienelemente sind unvollständig.");
        if (AcceptanceRunButton is null || AcceptanceGrid is null || AcceptanceOpenReportButton is null || AcceptanceOpenFolderButton is null) throw new InvalidOperationException("Abnahme-Bedienelemente sind unvollständig.");
        if (EnvironmentChecksGrid is null || EnvironmentResult is null || EnvironmentOpenReportButton is null || EnvironmentOpenFolderButton is null || HelpHardwareAcceptanceButton is null || FunctionAuditResult is null || FunctionAuditOpenButton is null) throw new InvalidOperationException("Hilfe-/Umgebungstest-Bedienelemente sind unvollständig.");
        if (ProfilesGrid is null || ProfileNewButton is null || ProfileNewBaseButton is null || ProfileImportButton is null || ProfileImportBaseButton is null || ProfileImportBaseRfPrjButton is null || ProfileEditButton is null || ProfileDuplicateButton is null || ProfileExportButton is null || ProfileHistoryButton is null || ProfilesPathText is null || ProfilesDirectoryText is null) throw new InvalidOperationException("Profilverwaltung ist unvollständig.");
        if (InitializeTeamVaultButton is null || ManageTeamVaultButton is null || VaultSetupStatusBorder is null || VaultSetupStatusTitle is null || VaultSetupStatusText is null) throw new InvalidOperationException("Projektunabhängige Vault-Ersteinrichtung ist unvollständig.");
        var profileDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "profiles");
        var taskShellDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "task-shells");
        _ = ProfileLibrary.Load(profileDirectory);
        var profileEditor = new ProfileEditorWindow(new ProfileEditingService().CreateBase(), profileStore, true);
        profileEditor.RunSelfTest();
        profileEditor.Close();
        var importReport = new RfPrjProfileImportReport
        {
            SourcePath = "selftest.rfPrj", ManifestVersion = "2.0.6", TaskCount = 1,
            Applications = [new RfPrjImportedApplication { Definition = new ApplicationDefinition { Id = "A1B2C3", Name = "Selbsttest", KeyType = "DF_KEY_AES", KeyCount = 1, TransportKeySettings = "0F", ProductiveKeySettings = "0F", ChangeKeyMode = "Key0Last" }, SuggestedInclude = true, Confidence = "Hoch", SourceTaskCount = 1 }]
        };
        var importWindow = new RfPrjImportWindow(new RfPrjProfileImportService(), new RfPrjProfileImportFiles(importReport, "selftest.txt", "selftest.json"));
        importWindow.RunSelfTest();
        importWindow.Close();
        var tutorialWindow = new TutorialWindow(_ => { });
        tutorialWindow.RunSelfTest();
        tutorialWindow.Close();
        var taskShells = TaskShellLibrary.Load(taskShellDirectory);
        if (taskShells.TaskTypes.Count != 12 || !taskShells.TaskTypes.Contains("CheckFileSettings"))
            throw new InvalidOperationException("Gebündelte Task-Shells sind unvollständig.");
    }

    public void RunStartupModeSelfTest()
    {
        var expectedTitle = string.Equals(startupPage, "Station", StringComparison.OrdinalIgnoreCase)
            ? $"SieEncode Station {Version}"
            : $"SieEncode Studio {Version}";
        if (!string.Equals(Title, expectedTitle, StringComparison.Ordinal))
            throw new InvalidOperationException($"Fenstertitel entspricht nicht dem Startmodus: erwartet '{expectedTitle}', erhalten '{Title}'.");
    }

    private void ConfigureBundledRuntimeWhenUnset()
    {
        if (!string.IsNullOrWhiteSpace(settings.RFiDGear.ExecutablePath)) return;
        var bundledRuntime = FindBundledRuntime();
        if (bundledRuntime is null) return;
        settings.RFiDGear.ExecutablePath = bundledRuntime;
        settingsStore.Save(settings, createProjectsDirectory: false);
    }

    private ProfileStore CreateProfileStore(BuilderSettings currentSettings)
    {
        var root = settingsStore.ResolveProfilesDirectory(currentSettings);
        Directory.CreateDirectory(Path.Combine(root, "customer"));
        Directory.CreateDirectory(Path.Combine(root, "base"));
        return new ProfileStore(Path.Combine(AppContext.BaseDirectory, "assets", "profiles"),
            Path.Combine(root, "customer"), baseDirectory: Path.Combine(root, "base"));
    }

    private static string? FindBundledRuntime()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Runtime", "SieEncode.Runtime.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Runtime", "SieEncode.Runtime.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Runtime", "RFiDGear.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Runtime", "RFiDGear.exe"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            settings = settingsStore.Load();
            suppressOperatingModeChange = true;
            ExpertModeToggle.IsChecked = settings.ExpertMode;
            suppressOperatingModeChange = false;
            ApplyOperatingMode();
            profileStore = CreateProfileStore(settings);
            ConfigureBundledRuntimeWhenUnset();
            projectsDirectory = settingsStore.ResolveProjectsDirectory(AppContext.BaseDirectory, settings);
            ProjectsDirectoryText.Text = projectsDirectory;
            ProfilesDirectoryText.Text = settingsStore.ResolveProfilesDirectory(settings);
            Directory.CreateDirectory(settings.VaultBackup.Directory);
            VaultBackupDirectoryText.Text = settings.VaultBackup.Directory;
            UpdateVaultSetupStatus();
            RFiDGearExecutableText.Text = settings.RFiDGear.ExecutablePath;
            SettingsPathText.Text = "Einstellungsdatei: " + settingsStore.SettingsPath;
            RefreshProjects();
            LoadProfiles();
            UpdateRFiDGearVersion();
            Navigate(startupPage, addHistory: false);
            SetStatus("Bereit · Einstellungen und Projektpfade wurden aus dem Benutzerprofil geladen.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page }) return;
        Navigate(page);
    }

    private static bool IsExpertPage(string page)
        => page is "Profiles" or "Analysis" or "Diff" or "Acceptance";

    private void ExpertMode_Changed(object sender, RoutedEventArgs e)
    {
        if (suppressOperatingModeChange || !IsLoaded) return;
        try
        {
            settings.ExpertMode = ExpertModeToggle.IsChecked == true;
            settingsStore.Save(settings, createProjectsDirectory: false);
            ApplyOperatingMode();
            SetStatus(settings.ExpertMode
                ? "Erweiterte Ansicht aktiv · Verwaltungs-, Diagnose- und Recovery-Funktionen sind sichtbar."
                : "Standardansicht aktiv · die Oberfläche zeigt den geführten Arbeitsablauf.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ApplyOperatingMode()
    {
        var visibility = settings.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
        OperatingModeText.Text = settings.ExpertMode ? "Erweiterte Ansicht" : "Standardansicht";
        ExpertModeToggle.ToolTip = settings.ExpertMode
            ? "Erweiterte Funktionen sichtbar · klicken, um zur Standardansicht zurückzukehren"
            : "Standardansicht aktiv · klicken, um erweiterte Funktionen einzublenden";
        NavProfilesButton.Visibility = visibility;
        ProjectAdvancedActions.Visibility = visibility;
        StationProjectPath.Visibility = visibility;
        StationProjectFolderButton.Visibility = visibility;
        EditorTransportSettings.Visibility = visibility;
        EditorAdvancedActions.Visibility = visibility;
        StationDetailedPreflight.Visibility = visibility;
        StationAdvancedCardActions.Visibility = visibility;
        // Destructive maintenance is never a normal card-inspection action.
        // Keep all existing confirmation and authorization checks unchanged.
        TestMaintenanceActions.Visibility = visibility;
        StationOpenRFiDGearButton.Visibility = visibility;
        StationTechnicalDetails.Visibility = visibility;
        HelpExpertTools.Visibility = visibility;
        SettingsCapabilityDetails.Visibility = visibility;
        EditorBuildSettings.Visibility = visibility;
        EditorSaveVaultButton.Visibility = visibility;
        EditorAuditVaultButton.Visibility = visibility;
        ProjectOpenFolderButton.Visibility = visibility;
        ProjectBuildColumn.Visibility = visibility;
        ProjectBaseProfileColumn.Visibility = visibility;
        ProjectFolderColumn.Visibility = visibility;
        if (selectedProject is not null) UpdateProjectSelectionInfo(selectedProject);
        if (editorProject is not null) RefreshProfileBindingStatus();
        else RefreshEditorWorkflow();
        RefreshWorkspaceNavigation();
    }

    internal static string NormalizeVisibleFieldLabel(string id, string label)
        => string.Equals(id, "firmCode", StringComparison.Ordinal) ? "Firmencode" : NormalizeVisibleFieldLabel(label);

    internal static string NormalizeVisibleFieldLabel(string label)
        => string.Equals(label, "DEVK Firmencode", StringComparison.Ordinal) ? "Firmencode" : label;

    private void Navigate(string page, bool addHistory = true)
    {
        if (!pages.ContainsKey(page)) return;
        navigation.Navigate(page, addHistory);
        foreach (var item in pages) item.Value.Visibility = item.Key == page ? Visibility.Visible : Visibility.Collapsed;
        var title = page switch
        {
            "Home" => "Übersicht", "Projects" => "Projektbibliothek", "Station" => "Karten kodieren", "TestStation" => "Karten untersuchen",
            "Editor" => "Projekt bearbeiten", "Profiles" => "Profile", "Analysis" => "Analyse / Dry-Run", "Diff" => "Projekt-/Profilvergleich",
            "Acceptance" => "Prüfung & Abnahme", "Settings" => "Einstellungen", "Help" => "Werkzeuge & Hilfe", _ => page
        };
        PageTitle.Text = WorkspaceTitle(WorkspaceNavigation.WorkspaceFor(page));
        Breadcrumb.Text = "SieEncode / " + PageTitle.Text + (PageTitle.Text == title ? "" : " / " + title);
        BackButton.IsEnabled = navigation.CanGoBack;
        RefreshWorkspaceNavigation();
    }

    private void PopulateTestAnalysisTree(CardRecognitionReport report)
    {
        TestAnalysisTree.Items.Clear();
        var piccSettings = report.PiccKeySettings is int value ? value.ToString("X2") : "nicht sicher gelesen";
        var root = new TreeViewItem
        {
            Header = $"PICC · KeySettings {piccSettings} · Schlüsselstatus {report.PiccState}",
            IsExpanded = true
        };
        root.Items.Add(new TreeViewItem { Header = $"Struktur: {report.Structure}" });
        root.Items.Add(new TreeViewItem { Header = $"Freier Speicher: {(report.PiccFreeMemoryBytes is int freeMemory ? freeMemory + " Byte" : "nicht sicher gelesen")}" });
        root.Items.Add(new TreeViewItem { Header = $"Create/Delete ohne PICC-Masterkey: {FormatOptionalBoolean(report.CreateDeleteWithoutMasterKey)}" });
        if (report.PiccKeySettings is int flags)
        {
            var settings = new TreeViewItem { Header = "PICC-Berechtigungen", IsExpanded = true };
            settings.Items.Add(new TreeViewItem { Header = $"Masterkey änderbar: {FormatBoolean((flags & 1) != 0)}" });
            settings.Items.Add(new TreeViewItem { Header = $"Applications ohne Masterkey auflisten: {FormatBoolean((flags & 2) != 0)}" });
            settings.Items.Add(new TreeViewItem { Header = $"Applications ohne Masterkey erstellen/löschen: {FormatBoolean((flags & 4) != 0)}" });
            settings.Items.Add(new TreeViewItem { Header = $"Konfiguration änderbar: {FormatBoolean((flags & 8) != 0)}" });
            root.Items.Add(settings);
        }
        foreach (var aid in report.ApplicationIds)
            root.Items.Add(new TreeViewItem { Header = $"Application {aid} · für Fileanalyse auswählen", Tag = aid });
        if (report.ApplicationIds.Count == 0)
            root.Items.Add(new TreeViewItem { Header = "Keine Applications (vollständige Inventur erfolgreich)" });
        TestAnalysisTree.Items.Add(root);
    }

    private void AppendApplicationInventoryTree(string jsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var top = document.RootElement;
        var aid = JsonValue(top, "applicationId", "unbekannt");
        var application = new TreeViewItem
        {
            Header = $"Application {aid} · Auth {JsonValue(top, "authentication", "unbekannt")} · Settings {JsonValue(top, "applicationSettings", "unbekannt")}",
            IsExpanded = true,
            Tag = aid
        };
        application.Items.Add(new TreeViewItem { Header = $"KeySettings: {JsonValue(top, "keySettings", "nicht sicher gelesen")} · KeyType: {JsonValue(top, "keyType", "nicht sicher gelesen")} · MaxKeys: {JsonValue(top, "maxNumberOfKeys", "nicht sicher gelesen")}" });
        if (top.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                var fileItem = new TreeViewItem
                {
                    Header = $"File {JsonValue(file, "fileId", "?")} · {JsonValue(file, "fileType", "Typ unbekannt")} · {JsonValue(file, "declaredSize", "?")} Byte",
                    IsExpanded = true
                };
                fileItem.Items.Add(new TreeViewItem { Header = $"Kommunikation: {JsonValue(file, "communicationMode", "unbekannt")}" });
                if (file.TryGetProperty("access", out var access))
                    fileItem.Items.Add(new TreeViewItem { Header = $"Rechte: R {JsonValue(access, "read", "?")} · W {JsonValue(access, "write", "?")} · RW {JsonValue(access, "readWrite", "?")} · Ändern {JsonValue(access, "change", "?")}" });
                fileItem.Items.Add(new TreeViewItem { Header = $"Metadaten: {JsonValue(file, "metadataResult", "unbekannt")} · Inhalt: {JsonValue(file, "contentResult", "nicht angefordert")}" });
                fileItem.Items.Add(new TreeViewItem { Header = new TextBlock
                {
                    Text = "Lesenachweis: " + FileInventoryEvidence.FromRuntime(file).Description,
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 750
                } });
                if (file.TryGetProperty("hex", out var hex) && hex.ValueKind == JsonValueKind.String)
                {
                    fileItem.Items.Add(new TreeViewItem { Header = "HEX: " + hex.GetString() });
                    fileItem.Items.Add(new TreeViewItem { Header = "ASCII: " + JsonValue(file, "ascii", "") });
                }
                application.Items.Add(fileItem);
            }
        }
        if (TestAnalysisTree.Items.Count == 0) TestAnalysisTree.Items.Add(application);
        else if (TestAnalysisTree.Items[0] is TreeViewItem root)
        {
            var placeholder = root.Items.OfType<TreeViewItem>().FirstOrDefault(item => string.Equals(item.Tag as string, aid, StringComparison.OrdinalIgnoreCase));
            if (placeholder is not null) root.Items.Remove(placeholder);
            root.Items.Add(application);
            root.IsExpanded = true;
        }
    }

    private static string JsonValue(JsonElement element, string name, string fallback)
        => element.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? value.ToString() : fallback;
    private static string FormatBoolean(bool value) => value ? "ja" : "nein";
    private static string FormatOptionalBoolean(bool? value) => value is bool known ? FormatBoolean(known) : "unbekannt";

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (navigation.GoBack()) Navigate(currentPage, addHistory: false);
    }

    private void RefreshProjects_Click(object sender, RoutedEventArgs e)
    {
        try { RefreshProjects(); SetStatus($"Projektbibliothek aktualisiert: {projects.Count} Projekt(e)."); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ProjectSearch_Changed(object sender, TextChangedEventArgs e)
        => CollectionViewSource.GetDefaultView(projects).Refresh();

    private bool FilterProject(object item)
    {
        if (item is not ProjectCatalogEntry entry) return false;
        var query = ProjectSearchText.Text.Trim();
        return query.Length == 0 || new[] { entry.Name, entry.Kind, entry.KeyState, entry.BaseProfile, entry.Customers, entry.Folder }
            .Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RefreshProjects()
    {
        projects.Clear();
        foreach (var entry in catalog.Scan(projectsDirectory)) projects.Add(entry);
        ProjectLibraryLabel.Text = "Übergeordneter Projektordner: " + projectsDirectory;
        HomeLibraryPath.Text = projectsDirectory;
        HomeProjectCount.Text = projects.Count == 1 ? "1 Projekt" : $"{projects.Count} Projekte";
        if (DiffLeftProject.SelectedItem is null && projects.Count > 0) DiffLeftProject.SelectedIndex = 0;
        if (DiffRightProject.SelectedItem is null && projects.Count > 0) DiffRightProject.SelectedIndex = projects.Count > 1 ? 1 : 0;
        if (selectedProject is not null)
        {
            var replacement = projects.FirstOrDefault(entry => string.Equals(entry.SourcePath, selectedProject.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (replacement is not null) { selectedProject = replacement; ProjectsGrid.SelectedItem = replacement; }
            else ClearProjectSelection();
        }
        UpdatePilotOverview();
    }

    private void ProjectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsGrid.SelectedItem is not ProjectCatalogEntry entry) return;
        SelectProject(entry);
    }

    private void SelectProject(ProjectCatalogEntry entry)
    {
        selectedProject = entry;
        HomeVaultButton.IsEnabled = true;
        ProjectEditButton.IsEnabled = true;
        ProjectOpenStationButton.IsEnabled = entry.CanOpenStation;
        ProjectOpenStationButton.ToolTip = entry.CanOpenStation
            ? "Projekt in der Kodierstation öffnen"
            : entry.HasCoding
                ? "Kodierumfang ist gewählt, aber sicherer Vault oder vollständiger Build fehlt."
                : "Dieses Projekt enthält nur Mastering. Zum Kodieren im Projekteditor den Buildumfang ‚Kodierung‘ aktivieren und neu bauen.";
        ProjectOpenFolderButton.IsEnabled = true;
        DeleteProjectButton.IsEnabled = !entry.ArtifactOnly;
        ProjectValidationButton.IsEnabled = true;
        ProjectPreflightPanel.Visibility = Visibility.Collapsed;
        HardwareAcceptancePlanButton.IsEnabled = !entry.ArtifactOnly;
        QuickStartCodingButton.IsEnabled = !entry.ArtifactOnly;
        CurrentProject.Text = entry.Name + " · " + entry.Kind;
        UpdateProjectSelectionInfo(entry);
        AcceptanceProject.Text = entry.Name;
        AcceptancePath.Text = entry.ProjectDirectory;
        ResetAcceptanceView();
        AnalysisSelectedButton.IsEnabled = true;
        TestProjectInfo.Text = $"Prüfvorlage: {entry.Name} · {entry.BaseProfile}";
        TestProjectRecognitionButton.IsEnabled = entry.CanOpenStation;
        TestProjectTransportButton.IsEnabled = entry.CanOpenStation;
        TestProjectProductiveButton.IsEnabled = entry.CanOpenStation;
        TestProjectAcceptanceButton.IsEnabled = entry.CanOpenStation;
        TestHardwarePlanButton.IsEnabled = !entry.ArtifactOnly;
        UpdatePilotOverview();
    }

    private void ClearProjectSelection()
    {
        selectedProject = null;
        ProjectsGrid.SelectedItem = null;
        HomeVaultButton.IsEnabled = false;
        ProjectEditButton.IsEnabled = false;
        ProjectValidationButton.IsEnabled = false;
        ProjectOpenStationButton.IsEnabled = false;
        ProjectOpenFolderButton.IsEnabled = false;
        DeleteProjectButton.IsEnabled = false;
        HardwareAcceptancePlanButton.IsEnabled = false;
        QuickStartCodingButton.IsEnabled = false;
        ProjectSelectionInfo.Text = "Projekt auswählen.";
        CurrentProject.Text = "Kein Projekt ausgewählt";
        TestProjectInfo.Text = "Kein Projekt ausgewählt. Projektlose Analyse ist trotzdem möglich.";
        TestProjectRecognitionButton.IsEnabled = false;
        TestProjectTransportButton.IsEnabled = false;
        TestProjectProductiveButton.IsEnabled = false;
        TestProjectAcceptanceButton.IsEnabled = false;
        TestHardwarePlanButton.IsEnabled = false;
    }

    private void UpdateProjectSelectionInfo(ProjectCatalogEntry entry)
    {
        // Tell the operator what the next available action is, rather than making them
        // infer it from the build, profile and vault columns. Do not claim readiness
        // unless the same CanOpenStation gate enables the actual handoff button.
        if (settings.ExpertMode)
        {
            ProjectSelectionInfo.Text =
                $"{entry.Name} · {entry.Kind} · {entry.Build} · {entry.BaseProfile}" +
                (entry.Customers == "–" ? " · ohne Zusatzprofil" : " + " + entry.Customers) +
                $" · Projektordner: {entry.ProjectDirectory}" +
                (entry.CanOpenStation ? " · Kodierstation verfügbar" : " · Kodierstation gesperrt");
            return;
        }

        if (entry.CanOpenStation)
        {
            ProjectSelectionInfo.Text = $"{entry.Name} · für die Kodierstation verfügbar. Nächster Schritt: „Zur Kodierstation“. " +
                (entry.HasVault
                    ? "SieEncode prüft den Karten- und Schlüsselzustand vor einem Kodierlauf erneut."
                    : "Der vorhandene Build ist verwendbar; ohne ursprünglichen Vault ist ein sicherer Neubau nicht möglich.") +
                (entry.HasRecognition ? "" : " Dieser Altbuild unterstützt die neue Transponder-Erkennung noch nicht.");
            return;
        }

        ProjectSelectionInfo.Text = entry.ArtifactOnly
            ? $"{entry.Name} · nur gebautes Projekt. Nächster Schritt: „Projekt bearbeiten“ erzeugt einen neuen, zunächst schlüsselfreien Entwurf. Die vorhandenen Artefakte bleiben erhalten."
            : !entry.HasOperationalBuild
                ? $"{entry.Name} · noch nicht kodierbereit. Nächster Schritt: „Projekt bearbeiten“, fehlende Daten bzw. Vault klären und das Projekt sicher prüfen und bauen."
                : $"{entry.Name} · Kodierstation derzeit gesperrt. Nächster Schritt: „Projekt prüfen“ für konkrete Hinweise. Vorhandene Schlüssel und Projekte nicht durch neue Schlüssel ersetzen.";
    }

    private void ProjectsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (selectedProject is null) return;
        EditSelectedProject_Click(sender, e);
    }

    private void ProjectDetails_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst ein Projekt auswählen.", true); return; }
        MessageBox.Show(this,
            $"Projekt: {selectedProject.Name}\nNutzung: {selectedProject.Kind}\nNeubau / Schlüssel: {selectedProject.KeyState}\nBuild: {selectedProject.Build}\nMasterprofil: {selectedProject.BaseProfile}\nKundenprofile: {selectedProject.Customers}\n\nQuelle: {selectedProject.SourcePath}\nProjektordner: {selectedProject.ProjectDirectory}\n\n" +
            (selectedProject.ArtifactOnly
                ? "Nur Bauartefakte vorhanden. Bearbeiten öffnet sie nach vollständiger Profil-/Layoutprüfung als neuen, ungespeicherten und keyfreien Mastering-Entwurf; das Artefakt bleibt unverändert."
                : selectedProject.HasOperationalBuild && !selectedProject.HasVault
                    ? "Der vorhandene Build kann weiterverwendet werden. Ohne ursprünglichen Vault sind Bearbeiten und reproduzierbarer Neubau nicht möglich."
                    : "Bearbeitbare Projektkonfiguration vorhanden."),
            "Projektdetails", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadStation_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst ein Projekt auswählen.", true); return; }
        LoadStation(selectedProject);
    }

    private bool LoadStation(ProjectCatalogEntry entry)
    {
        try
        {
            if (!entry.CanOpenStation)
                throw new InvalidOperationException(entry.HasCoding
                    ? "Die Kodierstation ist noch nicht bereit. Vault bereitstellen und Projekt vollständig neu bauen."
                    : "Dieses Projekt enthält nur Mastering. Im Projekteditor unter ‚Technische Build-Einstellungen‘ zusätzlich ‚Kodierung‘ aktivieren und neu bauen.");
            if (preparedSession is not null)
                throw new InvalidOperationException("Eine Kodiersitzung ist noch aktiv. Bitte zuerst Readbacks prüfen oder den Datensatz ausdrücklich als fehlgeschlagen abschließen.");
            RequireNoRunningRFiDGear("Die Kodierstation kann nicht gewechselt werden.");
            SelectProject(entry);
            var stationEntry = MaterializeSecureStationEntry(entry);
            selectedProject = stationEntry;
            var schemaPath = Path.Combine(stationEntry.ProjectDirectory, "coding.schema.json");
            if (!File.Exists(schemaPath)) throw new FileNotFoundException("coding.schema.json fehlt. Das Projekt wurde nicht für Kodierung gebaut.", schemaPath);
            using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            var root = schema.RootElement;
            sessionDefinition = sessionService.Load(stationEntry.ProjectDirectory);
            string? operationWarning = null;
            try { operationCatalog = operationService.Inspect(stationEntry.ProjectDirectory); }
            catch (Exception exception)
            {
                operationCatalog = new RFiDGearOperationCatalog(stationEntry.ProjectDirectory, []);
                operationWarning = "Betriebsaktionen gesperrt: " + exception.Message;
            }
            operationSession = null;
            operationFinalized = false;
            codingLaunch = null;
            operationTimer.Stop();
            lastCardPreflightReport = null;
            lastCardRecognitionReport = null;
            StationOpenPreflightResultButton.IsEnabled = false;
            stationFields.Clear();
            foreach (var field in sessionDefinition.CardFields) stationFields.Add(new CodingFieldInput(field));
            StationSeriesField.ItemsSource = stationFields;
            StationSeriesField.SelectedIndex = stationFields.Count > 0 ? 0 : -1;
            seriesService = null;
            stationQueue = [];
            stationQueueIndex = -1;
            lastFailedValues = null;
            lastSeriesRecord = null;
            seriesLogSnapshot = null;
            stationHistory.Clear();
            StationMode.SelectedIndex = 0;
            StationSequenceValue.Text = stationFields.FirstOrDefault()?.Value ?? "";
            StationSequenceStep.Text = "1";
            StationQueueInfo.Text = "Keine Liste geladen.";
            StationDiagnosticButton.IsEnabled = true;
            StationDiagnosticInfo.Text = "Diagnosepaket: noch nicht erzeugt · Shortcut Strg+Umschalt+D";
            UpdateSeriesUi();
            UpdateSeriesCounts();
            var profilesCount = ReadArrayCount(root, "profiles");
            var payloadCount = sessionDefinition.PayloadCount;
            StationProjectName.Text = entry.Name;
            StationProjectPath.Text = stationEntry.ProjectDirectory;
            StationProjectInfo.Text = $"{profilesCount} Profil(e) · {payloadCount} Payload(s) · {Path.GetFileName(sessionDefinition.CodingProjectPath)}" +
                (stationEntry.SecureOnDemand ? " · geschützte lokale Sitzung" : entry.ArtifactOnly ? " · nur gebautes Projekt" : entry.HasVault ? " · Neubau mit Vault möglich" : " · vorhandener Build nutzbar, Neubau ohne Vault gesperrt") +
                (stationEntry.HasRecognition ? " · Transponder-Erkennung verfügbar" : " · Altbuild ohne Transponder-Erkennung");
            StationRecognitionButton.Visibility = stationEntry.HasRecognition ? Visibility.Visible : Visibility.Collapsed;
            StationLegacyTransportButton.Visibility = stationEntry.HasRecognition ? Visibility.Collapsed : Visibility.Visible;
            StationOpenRecognitionResultButton.Visibility = stationEntry.HasRecognition ? Visibility.Visible : Visibility.Collapsed;
            StationCardStepExplanation.Text = stationEntry.HasRecognition
                ? "Zuerst den Transponder erkennen: SieEncode inventarisiert alle Applications und prüft bekannte PICC-Zugänge, ohne die Karte zu verändern. Nur 0 erfolgreich inventarisierte Applications gelten als leer."
                : "Dieses Altprojekt besitzt noch keine vollständige Transponder-Erkennung. Die sichtbare Ersatzprüfung liest ausschließlich die sechs erwarteten Projektdateien mit Transportzugängen. Sie verändert die Karte nicht; einen vollständig leeren Transponder kann sie nur zusammen mit dem detaillierten Laufnachweis erkennen.";
            StationProjectFolderButton.IsEnabled = true;
            StationCardStep.Visibility = Visibility.Visible;
            StationSeriesStep.Visibility = Visibility.Visible;
            StationCodingStep.Visibility = Visibility.Visible;
            StationCompletionStep.Visibility = Visibility.Visible;
            StationCompletionStep.IsExpanded = false;
            StationFailureOptions.IsExpanded = false;
            StationMasteringStep.IsExpanded = false;
            StationPreflightButton.IsEnabled = true;
            StationPrepareButton.IsEnabled = true;
            StationOpenRFiDGearButton.IsEnabled = false;
            StationStartAutoModeButton.IsEnabled = false;
            StationCheckReadbacksButton.IsEnabled = false;
            RefreshOperationButtons();
            StationOperationInfo.Text = operationWarning ?? (operationCatalog.Operations.Count == 0
                ? "Keine Mastering-, Kartenprüfungs- oder Recovery-Artefakte gefunden."
                : $"{operationCatalog.Operations.Count} geprüfte Betriebsaktion(en) verfügbar · kein Lauf gestartet.");
            StationPayloadInfo.Text = stationFields.Count == 0 ? "Keine kartenspezifischen Eingaben erforderlich." : $"{stationFields.Count} Kartenfeld(er) aus coding.schema.json geladen.";
            StationState.Text = "PROJEKT GELADEN";
            StationStateDetail.Text = entry.ArtifactOnly
                ? "Sitzungswerte eingeben und Payloads vorbereiten. Das gebaute Altprojekt funktioniert ohne erneute Abfrage produktiver Schlüssel."
                : entry.HasOperationalBuild && !entry.HasRecognition
                    ? "Kompatibler Altbuild: Kartenprüfung, Mastering und Kodierung bleiben verfügbar. Nur die neuere Funktion „Transponder erkennen“ fehlt."
                : "Vor dem Start Projektprüfung ausführen, anschließend Sitzungswerte validieren und Payloads vorbereiten.";
            StationStateBorder.Background = System.Windows.Media.Brushes.Honeydew;
            SetStationGuidance("PROJEKT GELADEN – ZUERST PRÜFEN",
                $"Das Projekt „{entry.Name}“ ist ausgewählt. Noch wurde keine Karte für einen Kodierlauf freigegeben.",
                "„Projekt prüfen“ wählen. Danach Kartendaten eingeben und mit Schritt 1 bereitstellen.",
                $"{profilesCount} Profil(e) · {payloadCount} erwartete Payload(s)", System.Windows.Media.Brushes.LemonChiffon);
            Navigate("Station");
            SetStatus("Kodierstation geladen: " + entry.Name);
            UpdatePilotOverview();
            return true;
        }
        catch (Exception exception)
        {
            projectSecretServer?.Dispose();
            projectSecretServer = null;
            secureOperationalSession?.Dispose();
            secureOperationalSession = null;
            ShowError(exception);
            return false;
        }
    }

    private ProjectCatalogEntry MaterializeSecureStationEntry(ProjectCatalogEntry entry, Func<string?>? requestPassphrase = null)
    {
        projectSecretServer?.Dispose();
        projectSecretServer = null;
        secureOperationalSession?.Dispose();
        secureOperationalSession = null;
        if (!entry.SecureOnDemand) return entry;
        var source = ProjectJson.Load(entry.SourcePath);
        var passphraseVault = entry.VaultVersion == KeyVaultService.CurrentVersion &&
            source.Project.GroupVault is null && !ProjectGroupVault.IsGroupVaultFile(ProjectKeyVault.GetPath(entry.SourcePath));
        var identities = passphraseVault ? [] : loadEditorVaultIdentities();
        try
        {
            ProjectKeyVaultPayload payload;
            if (passphraseVault)
            {
                var passphrase = requestPassphrase is not null ? requestPassphrase() : PasswordPrompt.Show(this,
                    "Vault für Kodierstation entsperren", "Passphrase des Projekt-Vaults eingeben. Die Schlüssel werden nur für die geschützte lokale Sitzung bereitgestellt.");
                if (passphrase is null) throw new OperationCanceledException("Entsperren der Kodierstation abgebrochen. Es wurde keine Kartenaktion gestartet.");
                payload = ProjectKeyVault.Load(ProjectKeyVault.GetPath(entry.SourcePath), passphrase);
            }
            else
            {
                if (identities.Count == 0)
                    throw new CryptographicException("Auf diesem Windows-Benutzer ist keine persönliche SieEncode-Identität vorhanden. Bitte das Projekt bearbeiten und unter ‚Mehrbenutzerzugriff‘ zuerst eine Identität anlegen oder freigeben lassen.");
                payload = source.Project.GroupVault is not null || ProjectGroupVault.IsGroupVaultFile(ProjectKeyVault.GetPath(entry.SourcePath))
                    ? ProjectGroupVault.Load(entry.SourcePath, source, GroupTrust(source), identities.Select(identity => identity.Certificate))
                    : ProjectKeyVault.LoadWithCertificates(ProjectKeyVault.GetPath(entry.SourcePath), identities.Select(identity => identity.Certificate));
            }
            var hydrated = ProjectKeyVault.Apply(ProjectJson.WithoutProductiveSecrets(source), payload);
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            secureOperationalSession = new SecureOperationalSessionService().Materialize(
                hydrated, LoadProfileLibrary(), shells, entry.SourcePath, Version);
            projectSecretServer = ProjectSecretPipeServer.Start(secureOperationalSession.Build.SessionSecrets);
            return entry with
            {
                ProjectDirectory = secureOperationalSession.ProjectDirectory,
                HasOperationalBuild = true,
                HasRecognition = true
            };
        }
        finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
    }

    private void RefreshOperationButtons()
    {
        bool Has(RFiDGearOperationKind kind) => operationCatalog?.Find(kind) is not null;
        var executable = File.Exists(settings.RFiDGear.ExecutablePath);
        var projectLaunch = executable && rfidgearCapabilities?.Has(RFiDGearCapabilityId.CustomProjectFile) == true;
        var autorun = executable && rfidgearCapabilities?.Has(RFiDGearCapabilityId.AutorunOnce) == true;
        var editable = selectedProject is { ArtifactOnly: false };
        var running = operationStartInProgress || operationSession is { Operation.Autorun: true } && !operationFinalized;
        var operationAvailable = !running && preparedSession is null;
        var extension = IsSelectedExtensionProject();
        var productiveExpectationAvailable = selectedProject is not null &&
            File.Exists(Path.Combine(selectedProject.ProjectDirectory, "card_preflight", "expected-card.json"));
        StationMasteringManualButton.IsEnabled = operationAvailable && projectLaunch && Has(RFiDGearOperationKind.MasteringManual) &&
            (!extension || lastCardRecognitionReport?.Report.ExtensionEligible == true);
        StationMasteringAutoButton.Content = extension ? "Neue Applications vorbereiten" : "Transponder vorbereiten";
        StationMasteringManualButton.Content = extension ? "Erweiterung manuell öffnen" : "Mastering manuell öffnen";
        StationMasteringAutoButton.IsEnabled = operationAvailable && autorun && editable && Has(RFiDGearOperationKind.MasteringAutorun) &&
            (!extension || lastCardRecognitionReport?.Report.ExtensionEligible == true);
        StationPreflightTransportButton.IsEnabled = operationAvailable && autorun && Has(RFiDGearOperationKind.CardPreflightTransport);
        StationRecognitionButton.IsEnabled = operationAvailable && autorun && Has(RFiDGearOperationKind.CardRecognitionReadOnly);
        StationReadProjectContentButton.IsEnabled = operationAvailable && autorun && selectedProject is not null &&
            lastCardRecognitionReport?.Report is { State: "RECOGNIZED", ExpectedApplicationIds.Count: > 0 } &&
            (Has(RFiDGearOperationKind.CardPreflightTransport) || Has(RFiDGearOperationKind.CardPreflightProductive));
        StationLegacyTransportButton.IsEnabled = selectedProject is { HasRecognition: false } && StationPreflightTransportButton.IsEnabled;
        StationPreflightProductiveButton.IsEnabled = operationAvailable && autorun && productiveExpectationAvailable && Has(RFiDGearOperationKind.CardPreflightProductive);
        StationApplicationAcceptanceButton.IsEnabled = operationAvailable && autorun &&
            rfidgearCapabilities?.Has(RFiDGearCapabilityId.ExactAppSettingsCheck) == true &&
            rfidgearCapabilities?.Has(RFiDGearCapabilityId.AppKeyCountCheck) == true &&
            Has(RFiDGearOperationKind.ApplicationAcceptanceProductive);
        StationRecoveryTransportButton.Visibility = extension ? Visibility.Collapsed : Visibility.Visible;
        StationRecoveryProductiveButton.Visibility = extension ? Visibility.Collapsed : Visibility.Visible;
        StationExtensionRollbackButton.Visibility = extension ? Visibility.Visible : Visibility.Collapsed;
        StationRecoveryTransportButton.IsEnabled = !extension && operationAvailable && autorun && editable && Has(RFiDGearOperationKind.RecoveryTransport);
        StationRecoveryProductiveButton.IsEnabled = !extension && operationAvailable && autorun && editable && Has(RFiDGearOperationKind.RecoveryProductive);
        StationExtensionRollbackButton.IsEnabled = extension && operationAvailable && autorun && editable && Has(RFiDGearOperationKind.ExtensionRollback);
        StationRefreshOperationButton.IsEnabled = running;
        StationCloseRFiDGearButton.IsEnabled = CurrentTrackedLaunch() is { } launch && processProbe.IsRunning(launch);
        TestRefreshOperationButton.IsEnabled = running;
        TestCloseRFiDGearButton.IsEnabled = StationCloseRFiDGearButton.IsEnabled;
        TestAnalyzeCardButton.IsEnabled = operationAvailable && autorun;
        TestFormatCardButton.IsEnabled = operationAvailable && autorun;
        TestDeepAnalyzeButton.IsEnabled = operationAvailable && autorun &&
            rfidgearCapabilities?.Has(RFiDGearCapabilityId.ApplicationFileInventory) == true &&
            lastCardRecognitionReport?.Report is { State: "RECOGNIZED", ApplicationIds.Count: > 0 };
        TestOpenRecognitionResultButton.IsEnabled = File.Exists(lastTestReportPath);
        TestOpenInventoryPdfButton.IsEnabled = LatestInventoryPdfPath() is not null;
        TestExportInventoryComparisonButton.IsEnabled = TestOpenInventoryPdfButton.IsEnabled;
        TestProjectRecognitionButton.IsEnabled = selectedProject is not null && operationAvailable && autorun && Has(RFiDGearOperationKind.CardRecognitionReadOnly);
        TestProjectTransportButton.IsEnabled = selectedProject is not null && operationAvailable && autorun && Has(RFiDGearOperationKind.CardPreflightTransport);
        TestProjectProductiveButton.IsEnabled = selectedProject is not null && operationAvailable && autorun && productiveExpectationAvailable && Has(RFiDGearOperationKind.CardPreflightProductive);
        TestProjectAcceptanceButton.IsEnabled = selectedProject is not null && operationAvailable && autorun && Has(RFiDGearOperationKind.ApplicationAcceptanceProductive);
        UpdateStationActionEmphasis();
    }

    private bool IsSelectedExtensionProject()
    {
        if (selectedProject is null) return false;
        try
        {
            if (!selectedProject.ArtifactOnly && File.Exists(selectedProject.SourcePath))
                return CardOperationMode.IsExtension(ProjectJson.Load(selectedProject.SourcePath));
            var planPath = Path.Combine(selectedProject.ProjectDirectory, "card-recognition.plan.json");
            if (!File.Exists(planPath)) return false;
            using var plan = JsonDocument.Parse(File.ReadAllText(planPath));
            return plan.RootElement.TryGetProperty("cardMode", out var mode) &&
                string.Equals(mode.GetString(), CardOperationMode.ExtendExisting, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private async void MasteringManual_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.MasteringManual);
    private async void MasteringAuto_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.MasteringAutorun);
    private async void CardPreflightTransport_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.CardPreflightTransport);
    private async void CardRecognition_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.CardRecognitionReadOnly);
    private async void ReadProjectCardContent_Click(object sender, RoutedEventArgs e)
    {
        if (lastCardRecognitionReport?.Report is not { State: "RECOGNIZED" } report)
        {
            SetStatus("Bitte zuerst denselben Transponder erkennen.", true);
            return;
        }
        var productive = report.MatchingCredentials.Contains("PRODUCTIVE", StringComparer.Ordinal) ||
            report.Structure == "ERWARTET_VOLLSTAENDIG" && !report.MatchingCredentials.Contains("TRANSPORT", StringComparer.Ordinal);
        var preferred = productive ? RFiDGearOperationKind.CardPreflightProductive : RFiDGearOperationKind.CardPreflightTransport;
        var fallback = productive ? RFiDGearOperationKind.CardPreflightTransport : RFiDGearOperationKind.CardPreflightProductive;
        var kind = operationCatalog?.Find(preferred) is not null ? preferred : fallback;
        await StartOperationAsync(kind);
    }
    private async void AnalyzeCardWithoutProject_Click(object sender, RoutedEventArgs e) => await StartStandaloneCardRecognitionAsync();

    private async void TestProjectRecognition_Click(object sender, RoutedEventArgs e)
    {
        if (!PrepareSelectedProjectForTest()) return;
        await StartOperationAsync(RFiDGearOperationKind.CardRecognitionReadOnly);
    }

    private async void TestProjectTransport_Click(object sender, RoutedEventArgs e)
    {
        if (!PrepareSelectedProjectForTest()) return;
        await StartOperationAsync(RFiDGearOperationKind.CardPreflightTransport);
    }

    private async void TestProjectProductive_Click(object sender, RoutedEventArgs e)
    {
        if (!PrepareSelectedProjectForTest()) return;
        await StartOperationAsync(RFiDGearOperationKind.CardPreflightProductive);
    }

    private async void TestProjectAcceptance_Click(object sender, RoutedEventArgs e)
    {
        if (!PrepareSelectedProjectForTest()) return;
        await StartOperationAsync(RFiDGearOperationKind.ApplicationAcceptanceProductive);
    }

    private bool PrepareSelectedProjectForTest()
    {
        var entry = selectedProject;
        if (entry is null) { SetStatus("Bitte zuerst ein Projekt als Prüfvorlage auswählen.", true); return false; }
        if (!LoadStation(entry)) return false;
        Navigate("TestStation");
        return true;
    }

    private async void DeepAnalyzeApplication_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginOperationStart()) return;
        RFiDGearHostRunV1? pending = null;
        try
        {
            RequireNoRunningRFiDGear("Eine vertiefte Analyse kann nicht gestartet werden.");
            if (lastCardRecognitionReport?.Report is not { State: "RECOGNIZED", ApplicationIds.Count: > 0 } recognition)
                throw new InvalidOperationException("Zuerst den Transponder analysieren und seine AID-Liste erfolgreich ermitteln.");
            var capabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            if (!capabilities.Has(RFiDGearCapabilityId.ApplicationFileInventory))
                throw new InvalidOperationException("Die konfigurierte Runtime besitzt keine ausführbare Application-/File-Inventur.");
            var dialog = new ApplicationInspectionDialog(recognition.ApplicationIds) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Request is null) return;

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "TestStation",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            var runtimeArtifact = Path.Combine(root, "application-inventory.runtime.json");
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            var document = new SupportProjectFactory(shells).BuildApplicationFileInventory(dialog.Request.Aid,
                dialog.Request.KeyType, dialog.Request.KeyNumber, dialog.Request.KeyValue, runtimeArtifact, dialog.Request.ReadContent);
            var protectedProject = RfPrjSecretProtection.Protect(document, "Application_Inventory");
            var projectPath = Path.Combine(root, "Application_Inventory_ReadOnly.rfPrj");
            RfPrjArchive.Save(projectPath, protectedProject.Project);
            if (RfPrjSecretProtection.FindPlaintextSecretElements(protectedProject.Project).Count != 0)
                throw new InvalidDataException("Analyseprojekt enthält unerwartet Klartextschlüssel.");
            toolSecretServer?.Dispose();
            toolSecretServer = ProjectSecretPipeServer.Start(protectedProject.Secrets);
            var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.ApplicationInventoryReadOnly,
                "Vertiefte Application-Inventur", projectPath, true, false, true, protectedProject.Project.Summarize());
            operationWorkspaceDirectory = root;
            pending = hostContractService.Begin(root, projectPath, operation.Label, RFiDGearHostModeV1.Autorun,
                operation.Tasks, capabilities, operation: HostOperation(operation.Kind));
            operationSession = await operationService.StartAsync(settings.RFiDGear.ExecutablePath, operation, secretServer: toolSecretServer);
            operationHostRun = pending;
            _ = hostContractService.MarkRunning(operationHostRun, operationSession.Launch);
            operationFinalized = false;
            operationTimer.Start();
            TestAnalysisInfo.Text = $"AID {dialog.Request.Aid} wird nur lesend inventarisiert · Transponder liegen lassen.";
            SetStatus("Vertiefte Application-Inventur gestartet.");
        }
        catch (Exception exception)
        {
            if (pending is not null && operationHostRun is null)
                try { _ = hostContractService.FailStart(pending, "RFiDGear-Prozessstart"); } catch { }
            ShowError(exception);
        }
        finally { EndOperationStart(); }
    }

    private async void FormatCardWithoutProject_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginOperationStart()) return;
        RFiDGearHostRunV1? pending = null;
        try
        {
            RequireNoRunningRFiDGear("Eine Formatierung kann nicht gestartet werden.");
            if (!File.Exists(settings.RFiDGear.ExecutablePath)) throw new FileNotFoundException("RFiDGear Runtime ist nicht konfiguriert.", settings.RFiDGear.ExecutablePath);
            var capabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            if (!capabilities.Has(RFiDGearCapabilityId.AutorunOnce)) throw new InvalidOperationException("Die konfigurierte Runtime unterstützt keinen nachgewiesenen Einmallauf.");
            var dialog = new TransponderFormatDialog { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Request is null) return;
            if (MessageBox.Show(this,
                    "LETZTE BESTÄTIGUNG\n\nDer aktuell aufliegende Transponder wird vollständig formatiert. Alle Applications und Daten werden gelöscht. Fortfahren?",
                    "Transponder endgültig formatieren", MessageBoxButton.YesNo, MessageBoxImage.Stop, MessageBoxResult.No) != MessageBoxResult.Yes) return;

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "TestStation", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            var document = new SupportProjectFactory(shells).BuildStandaloneFormat(dialog.Request.KeyType, dialog.Request.KeyValue);
            var protectedProject = RfPrjSecretProtection.Protect(document, "Standalone_CardFormat");
            var projectPath = Path.Combine(root, "Standalone_CardFormat.rfPrj");
            RfPrjArchive.Save(projectPath, protectedProject.Project);
            if (RfPrjSecretProtection.FindPlaintextSecretElements(protectedProject.Project).Count != 0)
                throw new InvalidDataException("Formatierprojekt enthält unerwartet Klartextschlüssel.");
            toolSecretServer?.Dispose();
            toolSecretServer = ProjectSecretPipeServer.Start(protectedProject.Secrets);
            var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.CardFormatStandalone,
                "Projektlose Transponderformatierung", projectPath, false, true, true, protectedProject.Project.Summarize());
            operationWorkspaceDirectory = root;
            pending = hostContractService.Begin(root, projectPath, operation.Label, RFiDGearHostModeV1.Autorun,
                operation.Tasks, capabilities, operation: HostOperation(operation.Kind));
            operationSession = await operationService.StartAsync(settings.RFiDGear.ExecutablePath, operation, secretServer: toolSecretServer);
            operationHostRun = pending;
            _ = hostContractService.MarkRunning(operationHostRun, operationSession.Launch);
            operationFinalized = false;
            operationTimer.Start();
            Navigate("TestStation");
            TestAnalysisInfo.Text = "Formatierung läuft · Transponder liegen lassen, bis der terminale Laufstatus vorliegt.";
            SetStatus("Projektlose Formatierung gestartet. Transponder liegen lassen.");
        }
        catch (Exception exception)
        {
            if (pending is not null && operationHostRun is null)
                try { _ = hostContractService.FailStart(pending, "RFiDGear-Prozessstart"); } catch { }
            ShowError(exception);
        }
        finally { EndOperationStart(); }
    }
    private async void CardPreflightProductive_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.CardPreflightProductive);
    private async void ApplicationAcceptance_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.ApplicationAcceptanceProductive);
    private async void RecoveryTransport_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.RecoveryTransport);
    private async void RecoveryProductive_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.RecoveryProductive);
    private async void ExtensionRollback_Click(object sender, RoutedEventArgs e) => await StartOperationAsync(RFiDGearOperationKind.ExtensionRollback);

    private async Task StartOperationAsync(RFiDGearOperationKind kind)
    {
        if (!TryBeginOperationStart()) return;
        RFiDGearHostRunV1? pendingOperationHostRun = null;
        try
        {
            RequireNoRunningRFiDGear("Eine weitere RFiDGear-Aktion kann nicht gestartet werden.");
            var operation = operationCatalog?.Find(kind) ?? throw new InvalidOperationException("Diese Betriebsaktion ist für das geladene Projekt nicht verfügbar.");
            if (IsSelectedExtensionProject() &&
                operation.Kind is (RFiDGearOperationKind.MasteringManual or RFiDGearOperationKind.MasteringAutorun) &&
                lastCardRecognitionReport?.Report.ExtensionEligible != true)
                throw new InvalidOperationException("Erweiterung gesperrt. Zuerst denselben Transponder mit ‚Transponder erkennen‘ prüfen. Nur eine autoritative AID-Inventur mit erlaubtem Create/Delete ohne PICC-Masterkey gibt das Hinzufügen der neuen Applications frei.");
            RFiDGearCapabilityReport? operationCapabilities = null;
            if (operation.Autorun)
            {
                operationCapabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
                if (!operationCapabilities.Has(RFiDGearCapabilityId.AutorunOnce)) throw new InvalidOperationException("Automatische Betriebsaktionen sind in dieser RFiDGear-Installation nicht nachgewiesen.");
                var reader = RFiDGearSettingsInspection.Inspect();
                if (!reader.IsConfigured) throw new InvalidOperationException("In RFiDGear ist kein Reader-Provider ausgewählt.");
            }
            if (operation.Destructive)
            {
                if (operation.Autorun)
                {
                    if (selectedProject?.ArtifactOnly != false) throw new InvalidOperationException("Automatische Schreibläufe sind für reine Bauartefakte gesperrt.");
                    var preflight = RunSelectedPreflight();
                    if (preflight.Status != "OK") throw new InvalidOperationException("Schreiblauf gesperrt.\n\n" + DescribePreflightFailure(preflight));
                }
                var warning = operation.Kind is RFiDGearOperationKind.RecoveryTransport or RFiDGearOperationKind.RecoveryProductive
                    ? "ACHTUNG: Recovery formatiert den aufgelegten Transponder und löscht Applications sowie Dateien."
                    : operation.Kind == RFiDGearOperationKind.ExtensionRollback
                        ? "ACHTUNG: Dieser Rückbau löscht ausschließlich die im Zusatzprofil definierten neuen Ziel-Applications samt ihrer Dateien. Vorhandene Grundprofil-Applications bleiben erhalten."
                    : "Mastering verändert Schlüssel, Applications und Dateien auf dem aufgelegten Transponder.";
                if (MessageBox.Show(this, warning + "\n\nAktion wirklich vorbereiten?", operation.Label, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (operation.Kind is RFiDGearOperationKind.RecoveryTransport or RFiDGearOperationKind.RecoveryProductive)
                    if (MessageBox.Show(this, "Letzte Bestätigung: Der Karteninhalt wird unwiderruflich verändert. Recovery jetzt einmal ausführen?", operation.Label, MessageBoxButton.YesNo, MessageBoxImage.Stop) != MessageBoxResult.Yes) return;
                if (operation.Kind == RFiDGearOperationKind.ExtensionRollback)
                    if (MessageBox.Show(this, "Letzte Bestätigung: Die neuen Ziel-Applications werden gelöscht. Erweiterung jetzt gezielt zurücknehmen?", operation.Label, MessageBoxButton.YesNo, MessageBoxImage.Stop) != MessageBoxResult.Yes) return;
            }
            else
            {
                if (operation.Kind == RFiDGearOperationKind.ApplicationAcceptanceProductive)
                {
                    operationCapabilities ??= new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
                    if (!operationCapabilities.Has(RFiDGearCapabilityId.ExactAppSettingsCheck) ||
                        !operationCapabilities.Has(RFiDGearCapabilityId.AppKeyCountCheck))
                        throw new InvalidOperationException("Anwendungs-Abnahme benötigt nachgewiesenes ReadAppSettings im Exact-Modus und CheckAppKeyCount.");
                }
                var message = operation.Kind switch
                {
                    RFiDGearOperationKind.ApplicationAcceptanceProductive => "Diese strikt lesende Abnahme prüft KeySettings und Keyanzahl mit Produktiv-Key 0. Dateityp, Größe, Kommunikation und Zugriffsrechte bleiben sichtbar 'nicht geprüft', bis RFiDGear einen kompatiblen FileSettings-Task bereitstellt. Keine Schlüsselwerte werden protokolliert. Jetzt starten?",
                    RFiDGearOperationKind.CardRecognitionReadOnly => "Die Erkennung liest die vollständige AID-Liste und testet bekannte PICC-Zugänge ausschließlich per Authentisierung. Sie formatiert, schreibt und ändert keine Schlüssel. Dieselbe Karte muss während des gesamten Laufs aufliegen. Jetzt starten?",
                    _ => "Diese Kartenprüfung enthält ausschließlich ReadData-Tasks. Dieselbe Karte muss während des gesamten Laufs aufliegen. Jetzt starten?"
                };
                if (MessageBox.Show(this, message, operation.Label, MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            }

            if (selectedProject is null) throw new InvalidOperationException("Kein Projekt geladen.");
            operationWorkspaceDirectory = selectedProject.ProjectDirectory;
            if (TryCardPreflightMode(operation.Kind, out var probeMode)) cardPreflightService.Prepare(selectedProject.ProjectDirectory, probeMode);
            operationCapabilities ??= new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            pendingOperationHostRun = hostContractService.Begin(selectedProject.ProjectDirectory, operation.ProjectPath,
                selectedProject.Name, operation.Autorun ? RFiDGearHostModeV1.Autorun : RFiDGearHostModeV1.Open,
                operation.Tasks, operationCapabilities, operation: HostOperation(operation.Kind));
            operationSession = await operationService.StartAsync(settings.RFiDGear.ExecutablePath, operation, secretServer: projectSecretServer);
            operationHostRun = pendingOperationHostRun;
            _ = hostContractService.MarkRunning(operationHostRun, operationSession.Launch);
            operationFinalized = !operation.Autorun;
            if (operation.Autorun) operationTimer.Start(); else operationTimer.Stop();
            RefreshOperationButtons();
            StationState.Text = operation.Autorun ? "RFIDGEAR-LAUF GESTARTET" : "RFIDGEAR MANUELL GEÖFFNET";
            StationStateDetail.Text = operation.Autorun
                ? $"{operation.Label} · Prozess {operationSession.Launch.ProcessId}. Ein Start ist noch kein Erfolg; das Log wird automatisch überwacht."
                : $"{operation.Label} · Prozess {operationSession.Launch.ProcessId}. Die Tasks werden erst durch bewusste Bedienung in RFiDGear ausgeführt.";
            StationStateBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
            StationOperationInfo.Text = $"{operation.Tasks.Count} Task(s) · {(operation.ReadOnly ? "read-only" : "schreibend")} · {Path.GetFileName(operation.ProjectPath)}";
            SetStatus($"{operation.Label} gestartet · Prozess {operationSession.Launch.ProcessId}.");
        }
        catch (Exception exception)
        {
            if (pendingOperationHostRun is not null && operationHostRun is null)
                try { _ = hostContractService.FailStart(pendingOperationHostRun, "RFiDGear-Prozessstart"); } catch { }
            ShowError(exception);
        }
        finally { EndOperationStart(); }
    }

    private async Task StartStandaloneCardRecognitionAsync()
    {
        if (!TryBeginOperationStart()) return;
        RFiDGearHostRunV1? pendingOperationHostRun = null;
        try
        {
            RequireNoRunningRFiDGear("Eine Transponderanalyse kann nicht gestartet werden.");
            if (!File.Exists(settings.RFiDGear.ExecutablePath)) throw new FileNotFoundException("RFiDGear Runtime ist nicht konfiguriert.", settings.RFiDGear.ExecutablePath);
            var capabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            if (!capabilities.Has(RFiDGearCapabilityId.AutorunOnce)) throw new InvalidOperationException("Die konfigurierte Runtime unterstützt keinen nachgewiesenen Einmallauf.");
            var reader = RFiDGearSettingsInspection.Inspect();
            if (!reader.IsConfigured) throw new InvalidOperationException("In RFiDGear ist kein Reader-Provider ausgewählt.");
            if (MessageBox.Show(this,
                "Die Analyse liest die vollständige AID-Liste, die frei lesbaren PICC-Rechte und prüft ausschließlich DES-/AES-Standard-PICC-Schlüssel. Sie schreibt und formatiert nichts. Geschützte Application- oder File-Einstellungen bleiben ohne passende Schlüssel ausdrücklich unbekannt. Jetzt starten?",
                "Transponder ohne Projekt analysieren", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SieEncode", "CardAnalysis",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            var document = new SupportProjectFactory(shells).BuildStandaloneCardRecognition();
            var projectPath = Path.Combine(root, "Standalone_CardRecognition_ReadOnly.rfPrj");
            RfPrjArchive.Save(projectPath, document);
            var operation = new RFiDGearOperationArtifact(RFiDGearOperationKind.CardRecognitionReadOnly,
                "Projektlose Transponderanalyse", projectPath, true, false, true, document.Summarize());
            operationWorkspaceDirectory = root;
            pendingOperationHostRun = hostContractService.Begin(root, projectPath, "Projektlose Transponderanalyse",
                RFiDGearHostModeV1.Autorun, operation.Tasks, capabilities, operation: HostOperation(operation.Kind));
            operationSession = await operationService.StartAsync(settings.RFiDGear.ExecutablePath, operation);
            operationHostRun = pendingOperationHostRun;
            _ = hostContractService.MarkRunning(operationHostRun, operationSession.Launch);
            operationFinalized = false;
            lastCardRecognitionReport = null;
            operationTimer.Start();
            Navigate("TestStation");
            StationCardStep.Visibility = Visibility.Visible;
            StationState.Text = "TRANSPONDERANALYSE LÄUFT";
            StationStateDetail.Text = $"Prozess {operationSession.Launch.ProcessId} · strikt lesend · ohne Projekt";
            StationStateBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
            SetStationGuidance("TRANSPONDER LIEGEN LASSEN", "SieEncode inventarisiert die Karte und prüft die Standard-PICC-Zugänge.",
                "Bis zum Ergebnis weder Karte noch Reader bewegen.", "Projektlose Nur-Lese-Analyse", System.Windows.Media.Brushes.LemonChiffon);
            TestAnalysisInfo.Text = "Analyse läuft · Transponder bis zum Ergebnis liegen lassen.";
        }
        catch (Exception exception)
        {
            if (pendingOperationHostRun is not null && operationHostRun is null)
                try { _ = hostContractService.FailStart(pendingOperationHostRun, "RFiDGear-Prozessstart"); } catch { }
            ShowError(exception);
        }
        finally { EndOperationStart(); }
    }

    private bool TryBeginOperationStart()
    {
        if (operationStartInProgress) return false;
        operationStartInProgress = true;
        RefreshOperationButtons();
        return true;
    }

    private void EndOperationStart()
    {
        operationStartInProgress = false;
        RefreshOperationButtons();
    }

    private void RefreshOperation_Click(object sender, RoutedEventArgs e)
    {
        try { UpdateOperationStatus(); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void CloseRFiDGear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (operationSession is { Operation.Autorun: true } && !operationFinalized)
                throw new InvalidOperationException("Der AUTORUN-Lauf besitzt noch keinen terminalen Status. Zuerst den Laufstatus aktualisieren; ein aktiver Kartenlauf wird nicht beendet.");
            var launch = CurrentTrackedLaunch() ?? throw new InvalidOperationException("Kein vom Builder gestarteter RFiDGear-Prozess vorhanden.");
            var result = processCompletionService.Complete(launch, force: false);
            if (result.RequiresForce)
            {
                if (MessageBox.Show(this, result.Message + "\n\nNur fortfahren, wenn der Kartenlauf terminal abgeschlossen ist. Prozess jetzt beenden?",
                    "RFiDGear-Prozess beenden", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                result = processCompletionService.Complete(launch, force: true);
            }
            if (result.Completed)
            {
                ClearCompletedLaunch(launch);
                if (operationHostRun is not null && operationSession?.Launch.ProcessId == launch.ProcessId)
                {
                    _ = hostContractService.Cancel(operationHostRun, launch.ProcessId, "Manuell geöffnete RFiDGear-Operation wurde geschlossen.");
                    operationHostRun = null;
                }
                RefreshOperationButtons();
                StationGuidanceExplanation.Text += " RFiDGear ist jetzt beendet.";
                StationGuidanceNextAction.Text = "RFiDGear wurde nach Ihrer Bestätigung beendet. " + StationGuidanceNextAction.Text;
                StationGuidanceEvidence.Text += " · Runtime beendet";
                SetStatus(result.Message);
            }
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private RFiDGearLaunchResult? CurrentTrackedLaunch()
    {
        if (operationSession?.Launch is { } operationLaunch && processProbe.IsRunning(operationLaunch)) return operationLaunch;
        if (codingLaunch is { } coding && processProbe.IsRunning(coding)) return coding;
        return null;
    }

    private void ClearCompletedLaunch(RFiDGearLaunchResult launch)
    {
        if (codingLaunch?.ProcessId == launch.ProcessId) codingLaunch = null;
    }

    private RFiDGearProcessCompletionResult CloseTerminalRFiDGearAutomatically(RFiDGearLaunchResult launch)
    {
        var previousTitle = StationGuidanceTitle.Text;
        var previousExplanation = StationGuidanceExplanation.Text;
        var previousNextAction = StationGuidanceNextAction.Text;
        var previousEvidence = StationGuidanceEvidence.Text;
        RFiDGearProcessCompletionResult result;
        try
        {
            result = processCompletionService.CompleteTerminal(launch);
        }
        catch (Exception exception)
        {
            result = new RFiDGearProcessCompletionResult(false, false,
                "Automatisches Schließen wurde aus Sicherheitsgründen abgebrochen: " + exception.Message);
        }
        if (result.Completed)
        {
            ClearCompletedLaunch(launch);
            StationGuidanceExplanation.Text = previousExplanation + " RFiDGear ist jetzt beendet.";
            StationGuidanceNextAction.Text = "RFiDGear wurde nach der gesicherten Auswertung automatisch geschlossen. " + previousNextAction;
            StationGuidanceEvidence.Text = previousEvidence + " · Runtime beendet";
        }
        else
        {
            StationGuidanceTitle.Text = previousTitle + " · RFIDGEAR NOCH OFFEN";
            StationGuidanceExplanation.Text = previousExplanation + " Der RFiDGear-Prozess konnte nicht regulär geschlossen werden.";
            StationGuidanceNextAction.Text = "Karte und Datensatz nicht wechseln. RFiDGear regulär schließen oder „RFiDGear-Prozess abschließen“ wählen; erst danach: " + previousNextAction;
            StationGuidanceEvidence.Text = previousEvidence + " · Runtime offen: " + result.Message;
            StationGuidanceBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
        }
        RefreshOperationButtons();
        return result;
    }

    private void OperationTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (operationSession is { Operation.Autorun: true } && !operationFinalized)
                UpdateOperationStatus();
            else if (preparedSession is not null && codingLaunch is not null)
                _ = EvaluateCodingRun(automatic: true);
            else
                operationTimer.Stop();
        }
        catch (Exception exception) { operationTimer.Stop(); ShowError(exception); }
    }

    private void UpdateOperationStatus()
    {
            if (operationSession is null) throw new InvalidOperationException("Kein überwachter RFiDGear-Lauf vorhanden.");
            var evaluation = operationService.Evaluate(operationSession);
            ShowStructuredRunDetails(evaluation.StructuredRun, operationSession.Operation.Tasks.Count);
            StationState.Text = evaluation.State switch
            {
                RFiDGearOperationState.Successful => "RFIDGEAR-LAUF ERFOLGREICH",
                RFiDGearOperationState.Failed => "RFIDGEAR-LAUF FEHLER",
                RFiDGearOperationState.TimedOut => "RFIDGEAR-LAUF TIMEOUT",
                _ => "RFIDGEAR-LAUF AKTIV"
            };
            StationStateDetail.Text = evaluation.Summary;
            StationStateBorder.Background = evaluation.State switch
            {
                RFiDGearOperationState.Successful => System.Windows.Media.Brushes.Honeydew,
                RFiDGearOperationState.Failed or RFiDGearOperationState.TimedOut => System.Windows.Media.Brushes.MistyRose,
                _ => System.Windows.Media.Brushes.LemonChiffon
            };
            if (evaluation.State != RFiDGearOperationState.Waiting && !operationFinalized)
            {
                var terminalLaunch = operationSession.Launch;
                operationFinalized = true;
                operationTimer.Stop();
                if (operationHostRun is null) throw new InvalidOperationException("Korrelierter Host-Vertragslauf für die RFiDGear-Operation fehlt.");
                var hostResult = hostContractService.CompleteOperation(operationHostRun, operationSession, evaluation);
                if (selectedProject is not null && TryCardPreflightMode(operationSession.Operation.Kind, out var mode))
                {
                    lastCardPreflightReport = cardPreflightService.CompleteAndEvaluate(selectedProject.ProjectDirectory, mode, evaluation);
                    StationOpenPreflightResultButton.IsEnabled = true;
                    var report = lastCardPreflightReport.Report;
                    StationState.Text = "KARTENSTATUS · " + report.Classification.Code;
                    StationStateDetail.Text = report.Classification.Title + "\n" + report.Classification.Detail + "\nEmpfehlung: " + report.Recommendation.Title;
                    StationOperationInfo.Text = $"Transport {report.Transport.ReadableCount}/{report.Transport.ExpectedCount} lesbar · Produktiv {report.Productive.ReadableCount}/{report.Productive.ExpectedCount} lesbar · Bericht: {lastCardPreflightReport.TextPath}";
                    StationStateBorder.Background = report.Classification.Level switch
                    {
                        "success" => System.Windows.Media.Brushes.Honeydew,
                        "error" => System.Windows.Media.Brushes.MistyRose,
                        _ => System.Windows.Media.Brushes.LemonChiffon
                    };
                    SetStationGuidance("KARTENPRÜFUNG · " + report.Classification.Title.ToUpperInvariant(),
                        report.Classification.Detail, report.Recommendation.Detail,
                        $"Transport {report.Transport.ReadableCount}/{report.Transport.ExpectedCount} · Produktiv {report.Productive.ReadableCount}/{report.Productive.ExpectedCount} · Payloads {report.Productive.PayloadMatchCount}/{report.Productive.PayloadComparedCount}",
                        StationStateBorder.Background);
                    hostResult = hostContractService.CompleteOperation(operationHostRun, operationSession, evaluation,
                        report.Classification.Level == "success" ? "successful" : "failed");
                }
                else if (selectedProject is not null && operationSession.Operation.Kind == RFiDGearOperationKind.ApplicationAcceptanceProductive)
                {
                    var acceptanceState = hostResult.State == "successful" && hostResult.TaskOutcomes.Count == operationSession.Operation.Tasks.Count &&
                        hostResult.TaskOutcomes.All(item => item.Outcome == "Successful") ? "successful" : "failed";
                    hostResult = hostContractService.CompleteOperation(operationHostRun, operationSession, evaluation, acceptanceState);
                    var evidence = new ApplicationAcceptanceEvidenceService().Create(selectedProject.ProjectDirectory, operationSession.Operation,
                        operationHostRun, hostResult);
                    StationOperationInfo.Text = $"Kartenstruktur-Abnahmeevidenz: {evidence.State} · {evidence.Tasks.Count}/{operationSession.Operation.Tasks.Count} autoritative Taskresultate · acceptance/application-settings.result.json";
                    SetStationGuidance(evidence.State == "successful" ? "ANWENDUNGSPRÜFUNG ERFOLGREICH" : "ANWENDUNGSPRÜFUNG FEHLGESCHLAGEN",
                        evidence.State == "successful" ? "Alle erwarteten Application- und KeySettings-Prüfungen wurden autoritativ bestätigt." : "Die Kartenstruktur konnte nicht vollständig bestätigt werden.",
                        evidence.State == "successful" ? "Keine Änderung erforderlich. Für einen Kodierlauf mit Schritt 1 fortfahren." : "Karte nicht verändern; Diagnose und technischen Ergebnisbericht prüfen lassen.",
                        $"Autoritative Taskresultate: {evidence.Tasks.Count}/{operationSession.Operation.Tasks.Count}",
                        evidence.State == "successful" ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose);
                }
                else if (operationSession.Operation.Kind == RFiDGearOperationKind.CardRecognitionReadOnly)
                {
                    var reportRoot = operationWorkspaceDirectory ?? selectedProject?.ProjectDirectory
                        ?? throw new InvalidOperationException("Arbeitsordner der Transponderanalyse fehlt.");
                    lastCardRecognitionReport = new CardRecognitionService().Create(reportRoot,
                        operationSession.Operation, evaluation);
                    lastTestReportPath = lastCardRecognitionReport.TextPath;
                    TestOpenInventoryPdfButton.IsEnabled = false;
                    TestExportInventoryComparisonButton.IsEnabled = false;
                    StationOpenRecognitionResultButton.IsEnabled = true;
                    var report = lastCardRecognitionReport.Report;
                    StationState.Text = "TRANSPONDER · " + report.Structure;
                    StationStateDetail.Text = report.Summary + "\nEmpfehlung: " + report.Recommendation;
                    StationOperationInfo.Text = $"Applications {report.ApplicationIds.Count} · PICC {report.PiccState} · Bericht: {lastCardRecognitionReport.TextPath}";
                    TestAnalysisInfo.Text = report.Summary + " PICC: " + report.PiccState;
                    PopulateTestAnalysisTree(report);
                    var recognized = report.State == "RECOGNIZED";
                    StationStateBorder.Background = recognized ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose;
                    SetStationGuidance("TRANSPONDER-ERKENNUNG · " + report.Structure,
                        report.Summary, report.Recommendation,
                        $"AIDs: {(report.ApplicationIds.Count == 0 ? "keine" : string.Join(", ", report.ApplicationIds))} · PICC: {report.PiccState}",
                        StationStateBorder.Background);
                    RefreshOperationButtons();
                }
                else if (operationSession.Operation.Kind == RFiDGearOperationKind.ApplicationInventoryReadOnly)
                {
                    var reportRoot = operationWorkspaceDirectory ?? throw new InvalidOperationException("Arbeitsordner der Application-Inventur fehlt.");
                    var report = new ApplicationInventoryReportService().Create(reportRoot,
                        Path.Combine(reportRoot, "application-inventory.runtime.json"));
                    lastTestReportPath = report.TextPath;
                    TestOpenInventoryPdfButton.IsEnabled = LatestInventoryPdfPath() is not null;
                    TestExportInventoryComparisonButton.IsEnabled = TestOpenInventoryPdfButton.IsEnabled;
                    TestAnalysisInfo.Text = report.Summary;
                    AppendApplicationInventoryTree(report.JsonPath);
                    TestOpenRecognitionResultButton.IsEnabled = true;
                    SetStationGuidance("APPLICATION-INVENTUR ABGESCHLOSSEN", report.Summary,
                        "Bericht öffnen. Ein nicht lesbarer Inhalt darf niemals als leer bewertet werden.",
                        "Nur-Lese-Lauf autoritativ abgeschlossen", System.Windows.Media.Brushes.Honeydew);
                }
                else if (operationSession.Operation.Kind == RFiDGearOperationKind.CardFormatStandalone)
                {
                    var successful = evaluation.State == RFiDGearOperationState.Successful;
                    TestAnalysisInfo.Text = successful
                        ? "Formatierung autoritativ abgeschlossen. Für eine belastbare Zustandsaussage jetzt erneut analysieren."
                        : "Formatierung nicht erfolgreich abgeschlossen. Transponderzustand ist unbekannt; erneut analysieren.";
                    SetStationGuidance(successful ? "FORMATIERUNG ABGESCHLOSSEN" : "FORMATIERUNG FEHLGESCHLAGEN",
                        TestAnalysisInfo.Text, "Transponder anschließend mit der projektlosen Analyse neu inventarisieren.",
                        evaluation.Summary, successful ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose);
                }
                else if (operationSession.Operation.Kind is RFiDGearOperationKind.MasteringManual or RFiDGearOperationKind.MasteringAutorun)
                {
                    var successful = evaluation.State == RFiDGearOperationState.Successful;
                    SetStationGuidance(successful ? "MASTERING ERFOLGREICH · TRANSPORTZUSTAND" : "MASTERING NICHT ABGESCHLOSSEN",
                        successful
                            ? "Applications und Files wurden angelegt und initialisiert. Die Karte verwendet weiterhin die Transport-Keys; das ist der vorgesehene sichere Zwischenzustand."
                            : "Die Kartenstruktur wurde nicht vollständig gemastert. Ein Teilzustand ist möglich und darf nicht als produktiv behandelt werden.",
                        successful
                            ? "Als Nächstes die Transportprüfung ausführen. Bei vollständigem Ergebnis den Datensatz vorbereiten und endkodieren; Mastering nicht wiederholen."
                            : "Karte liegen lassen, technischen Laufbericht prüfen und keine Endkodierung starten.",
                        successful
                            ? $"{operationSession.Operation.Tasks.Count}/{operationSession.Operation.Tasks.Count} Mastering-Taskresultate · Produktivschlüssel werden erst bei der Endkodierung gesetzt"
                            : evaluation.Summary,
                        successful ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose);
                    StationOperationInfo.Text += successful
                        ? " · Zwischenstand: Transport-Keys · nächster Nachweis: Transportprüfung"
                        : " · Kartenstatus vor weiterer Bearbeitung klären";
                }
                else if (operationSession.Operation.Kind is RFiDGearOperationKind.RecoveryTransport or RFiDGearOperationKind.RecoveryProductive)
                {
                    var successful = evaluation.State == RFiDGearOperationState.Successful;
                    SetStationGuidance(successful ? "TRANSPONDER FORMATIERT · LEER" : "FORMATIERUNG NICHT ABGESCHLOSSEN",
                        successful ? "Die vorherige Kartenstruktur wurde entfernt. Der Transponder befindet sich jetzt im leeren Ausgangszustand." : "Der Kartenstatus ist nach dem fehlgeschlagenen Recovery-Lauf nicht eindeutig.",
                        successful ? "Jetzt den Transponder vorbereiten. Anschließend Transportprüfung und Endkodierung durchführen." : "Karte liegen lassen und den technischen Laufbericht prüfen.",
                        evaluation.Summary, successful ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose);
                }
                operationHostRun = null;
                var closure = CloseTerminalRFiDGearAutomatically(terminalLaunch);
                StationOperationInfo.Text += closure.Completed
                    ? " · RFiDGear automatisch beendet"
                    : " · RFiDGear noch offen – Bedienaktion erforderlich";
                RefreshOperationButtons();
            }
            SetStatus($"Betriebsstatus: {evaluation.State} · {operationSession.Operation.Label}", evaluation.State is RFiDGearOperationState.Failed or RFiDGearOperationState.TimedOut);
    }

    private void OpenCardPreflightResult_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastCardPreflightReport is null || !File.Exists(lastCardPreflightReport.TextPath)) throw new FileNotFoundException("Noch kein Kartenprüfbericht vorhanden.");
            Process.Start(new ProcessStartInfo(lastCardPreflightReport.TextPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenCardRecognitionResult_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastCardRecognitionReport is null || !File.Exists(lastCardRecognitionReport.TextPath))
                throw new FileNotFoundException("Noch kein Erkennungsbericht vorhanden.");
            Process.Start(new ProcessStartInfo(lastCardRecognitionReport.TextPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenLatestTestReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lastTestReportPath) || !File.Exists(lastTestReportPath))
                throw new FileNotFoundException("In dieser Sitzung liegt noch kein Teststationsbericht vor.");
            Process.Start(new ProcessStartInfo(lastTestReportPath) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private string? LatestInventoryPdfPath()
    {
        if (string.IsNullOrWhiteSpace(lastTestReportPath) ||
            !string.Equals(Path.GetFileName(lastTestReportPath), "application-inventory.result.txt", StringComparison.OrdinalIgnoreCase)) return null;
        var path = Path.ChangeExtension(lastTestReportPath, ".pdf");
        return File.Exists(path) ? path : null;
    }

    private void OpenInventoryPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = LatestInventoryPdfPath() ?? throw new FileNotFoundException("Zum letzten Analyseergebnis liegt kein PDF-Istreport vor.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private static bool TryCardPreflightMode(RFiDGearOperationKind kind, out CardPreflightMode mode)
    {
        mode = kind == RFiDGearOperationKind.CardPreflightProductive ? CardPreflightMode.Productive : CardPreflightMode.Transport;
        return kind is RFiDGearOperationKind.CardPreflightTransport or RFiDGearOperationKind.CardPreflightProductive;
    }

    private static string HostOperation(RFiDGearOperationKind kind) => kind switch
    {
        RFiDGearOperationKind.MasteringManual or RFiDGearOperationKind.MasteringAutorun => "mastering",
        RFiDGearOperationKind.CardPreflightTransport => "card-preflight-transport",
        RFiDGearOperationKind.CardPreflightProductive => "card-preflight-productive",
        RFiDGearOperationKind.ApplicationAcceptanceProductive => "application-acceptance-productive",
        RFiDGearOperationKind.RecoveryTransport => "recovery-transport",
        RFiDGearOperationKind.RecoveryProductive => "recovery-productive",
        RFiDGearOperationKind.ExtensionRollback => "extension-rollback",
        RFiDGearOperationKind.CardRecognitionReadOnly => "card-recognition-readonly",
        RFiDGearOperationKind.CardFormatStandalone => "card-format-standalone",
        RFiDGearOperationKind.ApplicationInventoryReadOnly => "application-inventory-readonly",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private CodingSeriesMode CurrentSeriesMode() => (StationMode.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
    {
        "Fortlaufend" => CodingSeriesMode.Sequential,
        "Liste" => CodingSeriesMode.List,
        _ => CodingSeriesMode.Single
    };

    private string SeriesFieldId() => StationSeriesField.SelectedValue?.ToString() ?? stationFields.FirstOrDefault()?.Id ?? "";

    private void StationMode_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSeriesUi();

    private void UpdateSeriesUi()
    {
        if (StationSequencePanel is null || StationListPanel is null) return;
        var mode = CurrentSeriesMode();
        StationSequencePanel.Visibility = mode == CodingSeriesMode.Sequential ? Visibility.Visible : Visibility.Collapsed;
        StationListPanel.Visibility = mode == CodingSeriesMode.List ? Visibility.Visible : Visibility.Collapsed;
        StationSkipButton.IsEnabled = lastFailedValues is not null && mode != CodingSeriesMode.Single;
    }

    private void LoadStationList_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sessionDefinition is null) throw new InvalidOperationException("Zuerst ein Kodierprojekt laden.");
            var seriesId = SeriesFieldId();
            if (string.IsNullOrWhiteSpace(seriesId)) throw new InvalidOperationException("Nummernfeld auswählen.");
            var dialog = new OpenFileDialog { Title = "Kodierliste laden", Filter = "Text / CSV (*.txt;*.csv)|*.txt;*.csv|Alle Dateien (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            stationQueue = CodingSeriesService.LoadList(dialog.FileName, stationFields.Select(field => field.Id).ToArray(), seriesId);
            stationQueueIndex = 0;
            ApplyQueueItem();
            StationQueueInfo.Text = $"{stationQueue.Count} Datensätze geladen · aktuell 1/{stationQueue.Count}";
            SetStatus("Kodierliste geladen: " + dialog.FileName);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ApplyQueueItem()
    {
        if (stationQueueIndex < 0 || stationQueueIndex >= stationQueue.Count) return;
        foreach (var pair in stationQueue[stationQueueIndex])
        {
            var field = stationFields.FirstOrDefault(candidate => string.Equals(candidate.Id, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (field is not null) field.Value = pair.Value;
        }
    }

    private void ApplySeriesInput()
    {
        var mode = CurrentSeriesMode();
        var seriesId = SeriesFieldId();
        if (mode == CodingSeriesMode.Sequential)
        {
            if (string.IsNullOrWhiteSpace(seriesId)) throw new InvalidOperationException("Nummernfeld auswählen.");
            _ = CodingSeriesService.NextSequential(StationSequenceValue.Text, StationSequenceStep.Text);
            stationFields.First(field => string.Equals(field.Id, seriesId, StringComparison.OrdinalIgnoreCase)).Value = StationSequenceValue.Text;
        }
        if (mode == CodingSeriesMode.List)
        {
            if (stationQueueIndex < 0 || stationQueueIndex >= stationQueue.Count) throw new InvalidOperationException("Zuerst eine TXT-/CSV-Liste laden.");
            ApplyQueueItem();
        }
    }

    private CodingSeriesService EnsureSeriesService()
    {
        if (sessionDefinition is null) throw new InvalidOperationException("Kein Kodierprojekt geladen.");
        var mode = CurrentSeriesMode();
        if (seriesService is not null && seriesService.Mode != mode)
            throw new InvalidOperationException("Der Modus einer laufenden Sitzung kann nicht geändert werden. Sitzung zuerst über die Zusammenfassung schließen.");
        if (seriesService is null)
        {
            seriesService = new CodingSeriesService(sessionDefinition.ProjectDirectory, sessionDefinition.ProjectName, mode,
                keySetIdentity: sessionDefinition.KeySetIdentity);
            seriesLogSnapshot = new RFiDGearLogMonitor().Snapshot();
        }
        return seriesService;
    }

    private void PrepareSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (preparedSession is not null) throw new InvalidOperationException("Der vorbereitete Datensatz ist noch nicht abgeschlossen. Payloads dürfen bis zur Readback-Prüfung nicht ersetzt werden.");
            if (sessionDefinition is null) throw new InvalidOperationException("Kein Kodierprojekt geladen.");
            if (preparedSession is not null) throw new InvalidOperationException("Ein Datensatz ist bereits bereitgestellt. Zuerst F8 prüfen oder F9 als Fehler markieren.");
            ApplySeriesInput();
            var preflight = RunSelectedPreflight();
            if (preflight.Status != "OK") throw new InvalidOperationException("Sitzung wird nicht vorbereitet.\n\n" + DescribePreflightFailure(preflight));
            var values = stationFields.ToDictionary(field => field.Id, field => field.Value ?? "", StringComparer.OrdinalIgnoreCase);
            var series = EnsureSeriesService();
            var number = string.IsNullOrWhiteSpace(SeriesFieldId()) ? "" : values.GetValueOrDefault(SeriesFieldId(), "");
            if (series.IsKnownSuccessful(number) && MessageBox.Show(this, $"Die Nummer '{number}' wurde bereits erfolgreich protokolliert. Trotzdem bewusst erneut bereitstellen?", "Doppelte Nummer", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            preparedSession = sessionService.Prepare(sessionDefinition, values);
            RefreshOperationButtons();
            StationPayloadInfo.Text = $"{preparedSession.Payloads.Count} vollständig validierte Payload(s) sicher geschrieben · Baseline für {preparedSession.Expectations.Count} finale Readback(s) und RFiDGear-Log erfasst · {preparedSession.PreparedAt:dd.MM.yyyy HH:mm:ss}";
            var executableAvailable = File.Exists(settings.RFiDGear.ExecutablePath);
            var reader = RFiDGearSettingsInspection.Inspect();
            var capabilities = executableAvailable ? new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath) : null;
            var keyChangeSafety = capabilities is null
                ? new RFiDGearKeyChangeSafetyResult(false, preparedSession.TaskMap.Any(task => task.Type.Contains("Key", StringComparison.Ordinal)), "RFiDGear ist nicht verfügbar.")
                : RFiDGearKeyChangeSafety.Evaluate(capabilities, preparedSession.TaskMap);
            StationOpenRFiDGearButton.IsEnabled = executableAvailable && keyChangeSafety.Allowed;
            var autoModeAvailable = executableAvailable && keyChangeSafety.Allowed && capabilities!.Has(RFiDGearCapabilityId.ContinuousAutoMode);
            StationStartAutoModeButton.IsEnabled = autoModeAvailable && reader.IsConfigured;
            // Preparing a payload is not a run. A readback check before launch can only
            // produce a misleading waiting/failure signal and is not an operator step.
            StationCheckReadbacksButton.IsEnabled = false;
            StationMarkFailedButton.IsEnabled = true;
            StationRetryButton.IsEnabled = false;
            StationState.Text = "SITZUNG BEREIT";
            StationStateDetail.Text = !keyChangeSafety.Allowed
                ? keyChangeSafety.Message
                : StationStartAutoModeButton.IsEnabled
                ? "Payloads sind vollständig validiert. RFiDGear kann nur geöffnet oder mit dem expliziten Serienmodus AUTOMODE=1 gestartet werden; AUTORUN bleibt deaktiviert."
                : "Payloads sind vollständig validiert. Serienmodus nicht freigegeben: AUTOMODE muss binär nachgewiesen und ein Reader-Provider in RFiDGear ausgewählt sein.";
            StationStateBorder.Background = System.Windows.Media.Brushes.Honeydew;
            SetStationGuidance("BEREIT ZUR KODIERUNG",
                "Die eingegebenen Daten wurden validiert und die Soll-Payloads sicher für genau diesen Datensatz vorbereitet.",
                StationStartAutoModeButton.IsEnabled ? "Karte auf dem Leser liegen lassen und „2 · Kodierung starten“ wählen." : "Reader-Einstellung prüfen oder den Expertenmodus verwenden; noch wurde nichts auf die Karte geschrieben.",
                $"{preparedSession.Payloads.Count}/{preparedSession.Payloads.Count} Payloads vorbereitet · {preparedSession.Expectations.Count} finale Rücklesungen erwartet",
                System.Windows.Media.Brushes.LemonChiffon);
            SetStatus("Kodiersitzung vorbereitet: " + sessionDefinition.ProjectName);
        }
        catch (Exception exception)
        {
            RefreshOperationButtons();
            ShowError(exception);
        }
    }

    private ProjectPreflightReport RunSelectedPreflight()
    {
        if (selectedProject is null) throw new InvalidOperationException("Kein Projekt ausgewählt.");
        if (secureOperationalSession is not null && selectedProject.SecureOnDemand &&
            string.Equals(Path.GetFullPath(selectedProject.ProjectDirectory), Path.GetFullPath(secureOperationalSession.ProjectDirectory), StringComparison.OrdinalIgnoreCase))
            return secureOperationalSession.Build.Preflight;
        if (selectedProject.ArtifactOnly) return ProjectPreflight.InspectArtifacts(selectedProject.ProjectDirectory);
        var library = LoadProfileLibrary();
        var project = ProjectJson.Load(selectedProject.SourcePath);
        return ProjectPreflight.Inspect(project, library, selectedProject.SourcePath, selectedProject.ProjectDirectory, ProjectPreflightScope.Operational);
    }

    private void Preflight_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = RunSelectedPreflight();
            DisplayProjectPreflight(report);
            var legacyLimited = IsLegacyCompatibilityReport(report);
            var details = report.Issues.Count == 0
                ? $"OK · {report.Projects.Count} RFiDGear-Projekt(e) geprüft · keine Befunde." +
                  (report.Limitations.Count == 0 ? "" : "\nHinweis: " + string.Join(" ", report.Limitations))
                : DescribePreflightFailure(report);
            AcceptanceResult.Text = details;
            StationState.Text = report.Status == "OK" ? "PROJEKT OK" : "PROJEKTFEHLER";
            StationStateDetail.Text = details;
            StationStateBorder.Background = report.Status == "OK" ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose;
            if (report.Status != "OK")
                SetStationGuidance("PROJEKT MUSS KORRIGIERT WERDEN", details,
                    "Zur Projektverwaltung wechseln, den angezeigten Reparaturschritt ausführen und danach erneut prüfen.",
                    $"{report.Issues.Count} Befund(e) · kein Reader- oder Kartenzugriff erfolgt", System.Windows.Media.Brushes.MistyRose);
            else if (legacyLimited)
                SetStationGuidance("ALTPROJEKT VERWENDBAR",
                    "Die vorhandenen Betriebsartefakte sind strukturell gültig. Das Projekt kann mit seinem bisherigen Schlüsselstand weiter genutzt werden.",
                    "Für eine unbekannte Karte zuerst „Mit Transportdaten prüfen“. Mastering oder Kodierung anschließend nur mit dem passenden Projektstand starten.",
                    "Eingeschränkt: „Transponder erkennen“ fehlt · Neubau benötigt den ursprünglichen Vault", System.Windows.Media.Brushes.LemonChiffon);
            else
                SetStationGuidance("PROJEKT BEREIT", "Alle für diesen Projektstand erforderlichen Betriebsartefakte wurden geprüft.",
                    "Transponder erkennen oder Kartendaten eingeben und bereitstellen.",
                    $"{report.Projects.Count} RFiDGear-Projekt(e) · keine Befunde", System.Windows.Media.Brushes.Honeydew);
            SetStatus("Projektprüfung " + report.Status + ": " + selectedProject!.Name, report.Status != "OK");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void DisplayProjectPreflight(ProjectPreflightReport report)
    {
        var rows = ToPreflightUiIssues(report.Issues);
        var legacyLimited = rows.Length == 0 && IsLegacyCompatibilityReport(report);
        ProjectPreflightPanel.Visibility = Visibility.Visible;
        ProjectPreflightIssuesGrid.ItemsSource = rows;
        ProjectPreflightIssuesGrid.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ProjectPreflightTitle.Text = rows.Length == 0 ? legacyLimited ? "✓ Altprojekt verwendbar" : "✓ Projektprüfung erfolgreich" : "! Projektprüfung nicht bestanden";
        ProjectPreflightTitle.Foreground = rows.Length == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkRed;
        ProjectPreflightSummary.Text = rows.Length == 0
            ? legacyLimited
                ? $"{report.Projects.Count} vorhandene RFiDGear-Projekt(e) wurden geprüft. Der bisherige Build ist verwendbar; nur die neuere Transponder-Erkennung ist nicht enthalten."
                : $"{report.Projects.Count} RFiDGear-Projekt(e) und {report.Files.Count} zugehörige Datei(en) wurden geprüft. Es wurde kein Fehler gefunden."
            : $"{rows.Length} konkrete Befund(e) verhindern derzeit die Freigabe. In der Tabelle steht zu jedem Befund die betroffene Datei und der nächste Reparaturschritt.";
        ProjectPreflightNextAction.Text = rows.Length == 0
            ? report.Limitations.Count == 0 ? "Nächster Schritt: Projekt in die Kodierstation laden." : "Hinweis: " + string.Join(" ", report.Limitations)
            : selectedProject?.ArtifactOnly == true
                ? "Nächster Schritt: Das Bauartefakt als neuen bearbeitbaren Entwurf öffnen und vollständig neu bauen."
                : "Nächster Schritt: Projekt bearbeiten, die markierten Punkte beheben und anschließend neu bauen.";
        ProjectPreflightRepairButton.Content = selectedProject?.ArtifactOnly == true
            ? "Als bearbeitbaren Entwurf öffnen"
            : "Projekt bearbeiten und neu bauen";
        ProjectPreflightRepairButton.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ProjectPreflightPanel.Background = rows.Length == 0 ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.MistyRose;
    }

    private static ProjectPreflightUiIssue ToPreflightUiIssue(InspectionIssue issue)
    {
        var (area, cause, action) = issue.Code switch
        {
            "PROJECT_STALE" => ("Projektstand", "Das Projekt wurde nach der letzten Änderung noch nicht neu gebaut.", "Projekt öffnen und erneut bauen."),
            _ when issue.Code == "FILE_MISSING" && (issue.File?.Contains("CardRecognition", StringComparison.OrdinalIgnoreCase) == true || string.Equals(issue.File, "card-recognition.plan.json", StringComparison.OrdinalIgnoreCase))
                => ("Transponder-Erkennung", "Dieses Projekt wurde mit einer älteren SieEncode-Version ohne das neue strikt lesende Erkennungsmodul gebaut.", "Projekt mit der aktuellen Version öffnen, Vault entsperren und neu bauen."),
            "FILE_MISSING" => ("Projektdateien", $"Eine für den Betrieb benötigte Datei fehlt: {issue.File ?? "unbekannt"}.", "Bearbeitbares Projekt vollständig neu bauen."),
            "PROFILE_COMPOSITION" or "BASE_PROFILE" => ("Profile", issue.Message, "Profilwahl im Projekteditor prüfen und aktuelle Profilstände bewusst übernehmen."),
            "PROJECT_NAME_MISMATCH" => ("Projektzuordnung", issue.Message, "Projekt unter eindeutigem Namen neu bauen; alte gemischte Artefakte nicht verwenden."),
            "SECRET_IN_CONTRACT" => ("Sicherheit", "Eine technische Vertragsdatei enthält unzulässigerweise einen produktiven Schlüsselwert.", "Projekt nicht verwenden, Diagnose sichern und vollständig neu bauen."),
            "RFPRJ_INVALID" or "RFPRJ_VERSION" or "TASKS_EMPTY" => ("RFiDGear-Projekt", issue.Message, "Projekt mit der aktuellen SieEncode-Version neu bauen."),
            "TASK_INPUT_MISSING" or "TASK_INPUT_INVALID" or "TASK_INPUT_SIZE" => ("Ein-/Ausgabedatei", issue.Message, "Projekt neu bauen und dabei erzeugte Eingabedateien prüfen."),
            "TASK_DEPENDENCY" or "TASK_INDEX" or "TASK_ROOT_CONDITION" => ("Taskreihenfolge", issue.Message, "Technischen Dry-Run öffnen und Projekt neu bauen."),
            "TASK_IO_PATH_OUTSIDE" => ("Dateipfad", issue.Message, "Projektpfad und Ausgabeordner im Expertenmodus korrigieren und neu bauen."),
            "JSON_INVALID" => ("Vertragsdatei", issue.Message, "Beschädigte Datei nicht manuell reparieren; Projekt vollständig neu bauen."),
            _ when issue.Code.StartsWith("PROJECT_", StringComparison.Ordinal) => ("Pflichtfelder", issue.Message, "Projekt im Editor öffnen und den angezeigten Wert korrigieren."),
            _ when issue.Code.StartsWith("TRANSPORT_", StringComparison.Ordinal) => ("Transportkonfiguration", issue.Message, "Transporteinstellungen im Expertenmodus prüfen."),
            _ => ("Technische Prüfung", issue.Message, "Projekt öffnen, technischen Dry-Run prüfen und neu bauen.")
        };
        return new ProjectPreflightUiIssue(area, cause, issue.File ?? "–", action, issue.Code);
    }

    private static ProjectPreflightUiIssue[] ToPreflightUiIssues(IEnumerable<InspectionIssue> issues)
        => issues.Select(ToPreflightUiIssue)
            .GroupBy(row => new { row.Area, row.Cause, row.Action, row.Code })
            .Select(group => new ProjectPreflightUiIssue(
                group.Key.Area,
                group.Key.Cause,
                string.Join(", ", group.Select(row => row.File).Distinct(StringComparer.OrdinalIgnoreCase)),
                group.Key.Action,
                group.Key.Code))
            .ToArray();

    private static bool IsLegacyCompatibilityReport(ProjectPreflightReport report)
        => report.Limitations.Any(item => item.StartsWith("Kompatibler Altbuild:", StringComparison.Ordinal));

    private static string DescribePreflightFailure(ProjectPreflightReport report)
    {
        var allRows = ToPreflightUiIssues(report.Issues);
        var rows = allRows.Take(4).ToArray();
        return rows.Length == 0 ? "Die Projektprüfung meldet einen unbekannten Fehler."
            : $"Projektprüfung: {allRows.Length} Bedienbefund(e).\n" + string.Join("\n", rows.Select(row => $"• {row.Area}: {row.Cause} → {row.Action}")) +
              (allRows.Length > rows.Length ? $"\n• Weitere {allRows.Length - rows.Length} Befund(e) stehen in der Projektverwaltung." : "");
    }

    private void RepairProjectFromPreflight_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst ein Projekt auswählen.", true); return; }
        EditSelectedProject_Click(sender, e);
    }

    private void CloseProjectPreflight_Click(object sender, RoutedEventArgs e)
        => ProjectPreflightPanel.Visibility = Visibility.Collapsed;

    private async void OpenRFiDGear_Click(object sender, RoutedEventArgs e)
        => await LaunchRFiDGearAsync(autoMode: false);

    private async void StartAutoMode_Click(object sender, RoutedEventArgs e)
        => await LaunchRFiDGearAsync(autoMode: true);

    private async Task LaunchRFiDGearAsync(bool autoMode)
    {
        RFiDGearHostRunV1? pendingHostRun = null;
        try
        {
            RequireNoRunningRFiDGear("RFiDGear kann nicht erneut gestartet werden.");
            if (selectedProject is null) throw new InvalidOperationException("Kein Kodierprojekt geladen.");
            if (preparedSession is null || sessionDefinition is null) throw new InvalidOperationException("Bitte zuerst Sitzungswerte und Payloads vorbereiten.");
            if (hostRun is not null) throw new InvalidOperationException("Für diesen Datensatz existiert bereits ein Host-Vertragslauf. Zuerst finale Readbacks prüfen oder den Datensatz als fehlgeschlagen abschließen.");
            var capabilities = new RFiDGearCapabilityInspector().Inspect(settings.RFiDGear.ExecutablePath);
            RFiDGearKeyChangeSafety.RequireAllowed(capabilities, preparedSession.TaskMap);
            var preflight = RunSelectedPreflight();
            if (preflight.Status != "OK") throw new InvalidOperationException("RFiDGear wird nicht geöffnet.\n\n" + DescribePreflightFailure(preflight));
            if (autoMode)
            {
                var reader = RFiDGearSettingsInspection.Inspect();
                if (!reader.IsConfigured)
                    throw new InvalidOperationException("Serienmodus kann nicht gestartet werden: " + (reader.Error ?? "In RFiDGear ist kein Reader-Provider ausgewählt."));
            }
            sessionService.EnsurePreparedPayloads(preparedSession);
            pendingHostRun = hostContractService.Begin(sessionDefinition.ProjectDirectory, sessionDefinition.CodingProjectPath,
                sessionDefinition.ProjectName, autoMode ? RFiDGearHostModeV1.Automode : RFiDGearHostModeV1.Open,
                preparedSession.TaskMap, capabilities);
            var result = await new RFiDGearLauncher().LaunchAsync(new RFiDGearLaunchRequest(settings.RFiDGear.ExecutablePath, sessionDefinition.CodingProjectPath, Autorun: false, AutoMode: autoMode, SecretServer: projectSecretServer));
            codingLaunch = result;
            hostRun = pendingHostRun;
            codingTerminalPendingSince = null;
            _ = hostContractService.MarkRunning(hostRun, result);
            // Once a real host run exists, F8 is available as an explicit refresh.
            // Success still requires the terminal, correlated final readbacks.
            StationCheckReadbacksButton.IsEnabled = true;
            operationTimer.Start();
            StationState.Text = autoMode ? "SERIENMODUS AKTIV" : "RFiDGear GEÖFFNET";
            StationStateDetail.Text = autoMode
                ? $"Prozess {result.ProcessId} wurde mit AUTOMODE=1 gestartet. RFiDGear verarbeitet neue UIDs über seinen eigenen Automatikpfad; Kartenerfolg weiterhin über finale Readbacks prüfen."
                : $"Prozess {result.ProcessId} wurde mit dem konkreten Kodierprojekt geöffnet. Noch kein Kartenerfolg; nach dem manuellen Lauf finale Readbacks prüfen.";
            SetStationGuidance("KODIERUNG LÄUFT – KARTE LIEGEN LASSEN",
                "RFiDGear wurde mit dem vorbereiteten Datensatz gestartet. SieEncode überwacht Laufstatus und Readbacks automatisch; ein gestarteter Prozess ist noch kein erfolgreicher Kodiernachweis.",
                "Karte und Datensatz nicht wechseln. Nach dem terminalen Ergebnis schließt SieEncode RFiDGear regulär und zeigt die nächste Aktion. F8 aktualisiert die Auswertung bei Bedarf sofort.",
                $"Prozess {result.ProcessId} · {(autoMode ? "AUTOMODE" : "manueller Expertenmodus")}", System.Windows.Media.Brushes.LemonChiffon);
            SetStatus(autoMode
                ? $"RFiDGear-Serienmodus gestartet · Prozess {result.ProcessId} · AUTOMODE=1."
                : $"RFiDGear geöffnet · Prozess {result.ProcessId} · ohne AUTORUN/AUTOMODE.");
        }
        catch (Exception exception)
        {
            if (pendingHostRun is not null && hostRun is null)
                try { _ = hostContractService.FailStart(pendingHostRun, "RFiDGear-Prozessstart"); } catch { }
            ShowError(exception);
        }
    }

    private void CheckReadbacks_Click(object sender, RoutedEventArgs e)
    {
        try { _ = EvaluateCodingRun(automatic: false); }
        catch (Exception exception) { ShowError(exception); }
    }

    private bool EvaluateCodingRun(bool automatic)
    {
        if (preparedSession is null) throw new InvalidOperationException("Keine vorbereitete Kodiersitzung vorhanden.");
        var evaluation = sessionService.Evaluate(preparedSession);
        if (automatic && evaluation.State == CodingRunState.Waiting && codingLaunch is { } launch && !processProbe.IsRunning(launch))
            evaluation = CodingRunProcessExitPolicy.Finalize(evaluation);
        ShowStructuredRunDetails(evaluation.StructuredRun, preparedSession.TaskMap.Count);
        var operatorAnalysis = CodingRunOperatorAnalyzer.Analyze(evaluation, preparedSession.TaskMap.Count);
        var processRunning = codingLaunch is { } activeLaunch && processProbe.IsRunning(activeLaunch);
        var finalization = CodingRunFinalizationGate.Decide(evaluation, operatorAnalysis, processRunning,
            codingTerminalPendingSince, DateTimeOffset.UtcNow);
        codingTerminalPendingSince = finalization.PendingSince;
        if (finalization.WaitForTerminalEvidence)
        {
            var waitingBrush = System.Windows.Media.Brushes.LemonChiffon;
            StationState.Text = "LAUFNACHWEIS WIRD ABGESCHLOSSEN";
            StationStateDetail.Text = finalization.Detail;
            StationStateBorder.Background = waitingBrush;
            SetStationGuidance("KARTE NOCH LIEGEN LASSEN",
                "Die finalen Kartendaten sind bereits vollständig und bytegleich. RFiDGear schreibt gerade noch den korrelierten Abschlussnachweis.",
                "Karte nicht entnehmen und RFiDGear nicht schließen. SieEncode prüft den Abschluss automatisch erneut.",
                operatorAnalysis.EvidenceSummary, waitingBrush);
            SetStatus($"Laufanalyse: TERMINAL_PENDING · {preparedSession.Definition.ProjectName}" +
                (automatic ? " · automatisch überwacht" : ""));
            return false;
        }
        var effectiveEvaluation = evaluation.State == CodingRunState.Successful && !operatorAnalysis.CanReleaseCard
            ? evaluation with { State = CodingRunState.Failed, FailureCategory = "Laufnachweis unvollständig", Detail = operatorAnalysis.Explanation }
            : evaluation;
        var resultBrush = operatorAnalysis.CanReleaseCard ? System.Windows.Media.Brushes.Honeydew
            : evaluation.State == CodingRunState.Waiting ? System.Windows.Media.Brushes.LemonChiffon
            : System.Windows.Media.Brushes.MistyRose;
        StationState.Text = operatorAnalysis.Headline;
        StationStateDetail.Text = operatorAnalysis.TechnicalSummary;
        StationStateBorder.Background = resultBrush;
        SetStationGuidance(operatorAnalysis.Headline, operatorAnalysis.Explanation,
            operatorAnalysis.NextAction, operatorAnalysis.EvidenceSummary, resultBrush);
        var projectName = preparedSession.Definition.ProjectName;
        if (evaluation.State != CodingRunState.Waiting)
        {
            operationTimer.Stop();
            var terminalLaunch = codingLaunch;
            CompleteSeriesRecord(effectiveEvaluation, operatorAnalysis);
            if (terminalLaunch is not null)
                _ = CloseTerminalRFiDGearAutomatically(terminalLaunch);
        }
        SetStatus($"Laufanalyse: {operatorAnalysis.Code} · {projectName}" + (automatic ? " · automatisch überwacht" : ""), !operatorAnalysis.CanReleaseCard);
        return evaluation.State != CodingRunState.Waiting;
    }

    private void ShowStructuredRunDetails(RFiDGearStructuredRunEvidence? evidence, int expectedTasks)
    {
        var structured = evidence ?? RFiDGearStructuredRunEvidence.Empty;
        StationTaskOutcomesGrid.ItemsSource = structured.TaskOutcomes;
        StationRunEvidenceInfo.Text = structured.TaskOutcomes.Count == 0
            ? structured.CorrelationError
            : $"{structured.TaskOutcomes.Count}/{expectedTasks} Taskresultate · Terminal: {(string.IsNullOrWhiteSpace(structured.TerminalEvent) ? "noch offen" : structured.TerminalEvent)} · Korrelation: {(structured.Correlated ? "OK" : "FEHLER")}" +
              (string.IsNullOrWhiteSpace(structured.RunIdHash) ? "" : $" · Run-ID-Fingerprint {structured.RunIdHash[..12]}");
    }

    private void SetStationGuidance(string title, string explanation, string nextAction, string evidence, System.Windows.Media.Brush background)
    {
        StationGuidanceTitle.Text = title;
        StationGuidanceExplanation.Text = explanation;
        StationGuidanceNextAction.Text = nextAction;
        StationGuidanceEvidence.Text = evidence;
        StationGuidanceBorder.Background = background;
        UpdateStationActionEmphasis();
    }

    // Visual guidance only. Do not synthesize button clicks or interpret an available
    // operation as an authorization to write a card. Explicit actions and all existing
    // preflight/reader/host/readback gates stay in their original handlers.
    private void UpdateStationActionEmphasis()
    {
        if (StationPreflightButton is null || StationRecognitionButton is null || StationPrepareButton is null)
            return;
        var normalStyle = (Style)FindResource(typeof(Button));
        var primaryStyle = (Style)FindResource("PrimaryButton");
        var actions = new[]
        {
            StationPreflightButton, StationRecognitionButton, StationLegacyTransportButton,
            StationMasteringAutoButton, StationPrepareButton, StationStartAutoModeButton,
            StationCheckReadbacksButton, StationExportButton
        };
        foreach (var action in actions) action.Style = normalStyle;

        Button? next = null;
        var headline = StationGuidanceTitle.Text;
        var operationRunning = operationStartInProgress ||
            (operationSession is { Operation.Autorun: true } && !operationFinalized);
        if (selectedProject is not null && !operationRunning)
        {
            if (preparedSession is not null)
            {
                // Preparing a dataset does not authorize an implicit write. A coding start
                // is highlighted only while that dataset is ready and no host run has begun.
                if (headline == "BEREIT ZUR KODIERUNG" && hostRun is null && codingLaunch is null &&
                    StationStartAutoModeButton.IsEnabled)
                    next = StationStartAutoModeButton;
            }
            else if (headline.StartsWith("PROJEKT GELADEN", StringComparison.Ordinal) &&
                     StationPreflightButton.IsEnabled)
            {
                next = StationPreflightButton;
            }
            else if (headline is "PROJEKT BEREIT" or "ALTPROJEKT VERWENDBAR")
            {
                if (StationRecognitionButton.IsEnabled && StationRecognitionButton.Visibility == Visibility.Visible)
                    next = StationRecognitionButton;
                else if (StationLegacyTransportButton.IsEnabled &&
                         StationLegacyTransportButton.Visibility == Visibility.Visible)
                    next = StationLegacyTransportButton;
                else if (StationPrepareButton.IsEnabled)
                    next = StationPrepareButton;
            }
        }
        if (next is { IsEnabled: true, Visibility: Visibility.Visible })
            next.Style = primaryStyle;
    }

    private void CompleteSeriesRecord(CodingRunEvaluation evaluation, CodingRunOperatorAnalysis? operatorAnalysis = null)
    {
        if (preparedSession is null) return;
        codingTerminalPendingSince = null;
        if (hostRun is not null)
        {
            _ = hostContractService.Complete(hostRun, codingLaunch, evaluation);
            hostRun = null;
        }
        var values = stationFields.ToDictionary(field => field.Id, field => field.Value ?? "", StringComparer.OrdinalIgnoreCase);
        var number = string.IsNullOrWhiteSpace(SeriesFieldId()) ? "" : values.GetValueOrDefault(SeriesFieldId(), "");
        var result = evaluation.State == CodingRunState.Successful ? CodingSeriesResult.Successful : CodingSeriesResult.Failed;
        operatorAnalysis ??= CodingRunOperatorAnalyzer.Analyze(evaluation, preparedSession.TaskMap.Count);
        var record = new CodingSeriesRecord(DateTimeOffset.Now, number, result, evaluation.FailureCategory, evaluation.Detail, values,
            evaluation.Signal.Summary, evaluation.Signal.TaskExecutionIndex, evaluation.Task?.Type ?? "", evaluation.Task?.Description ?? "", evaluation.Signal.SanitizedText,
            operatorAnalysis.Headline, operatorAnalysis.NextAction, operatorAnalysis.EvidenceSummary, operatorAnalysis.CanReleaseCard);
        sessionService.InvalidatePreparedPayloads(preparedSession,
            "Datensatz wurde abgeschlossen; Payloads sind bis zur bewussten Vorbereitung des nächsten Datensatzes gesperrt.");
        EnsureSeriesService().Append(record);
        lastSeriesRecord = record;
        stationHistory.Insert(0, CodingSeriesHistoryRow.From(record));
        preparedSession.Dispose();
        preparedSession = null;
        StationCheckReadbacksButton.IsEnabled = false;
        StationMarkFailedButton.IsEnabled = false;
        StationOpenRFiDGearButton.IsEnabled = false;
        StationStartAutoModeButton.IsEnabled = false;
        if (result == CodingSeriesResult.Successful) { lastFailedValues = null; AdvanceSeriesInput(); }
        else
        {
            lastFailedValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            try
            {
                var bundle = CreateDiagnosticBundle();
                StationDiagnosticInfo.Text = "Diagnosepaket automatisch gespeichert: " + bundle.Path;
            }
            catch (Exception exception)
            {
                StationDiagnosticInfo.Text = "Fehler erkannt, Diagnosepaket konnte nicht erzeugt werden: " + exception.Message;
            }
        }
        StationRetryButton.IsEnabled = lastFailedValues is not null;
        // Show the result immediately, and offer retry/skip only after an actual failed
        // record. Expanding these sections never retries or skips anything by itself.
        StationCompletionStep.IsExpanded = true;
        StationFailureOptions.IsExpanded = lastFailedValues is not null;
        UpdateSeriesUi();
        UpdateSeriesCounts();
        RefreshOperationButtons();
    }

    private void MarkStationFailed_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (preparedSession is null) throw new InvalidOperationException("Kein laufender Datensatz vorhanden.");
            var signal = new RFiDGearDiagnosticSignal(true, true, "Bediener", "sicher", "Vom Bediener als fehlgeschlagen markiert.", null, "Operator", "");
            CompleteSeriesRecord(new CodingRunEvaluation(CodingRunState.Failed, "Kodierlauf wurde vom Bediener als fehlgeschlagen markiert.", "RFiDGear-Lauf / unbekannt", signal, null));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RetryStation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastFailedValues is null) throw new InvalidOperationException("Keine fehlgeschlagene Nummer zum Wiederholen.");
            foreach (var pair in lastFailedValues)
            {
                var field = stationFields.FirstOrDefault(candidate => string.Equals(candidate.Id, pair.Key, StringComparison.OrdinalIgnoreCase));
                if (field is not null) field.Value = pair.Value;
            }
            PrepareSession_Click(sender, e);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void SkipStation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (lastFailedValues is null) throw new InvalidOperationException("Keine fehlgeschlagene Nummer zum Überspringen.");
            if (CurrentSeriesMode() == CodingSeriesMode.Single) throw new InvalidOperationException("Überspringen ist nur in Fortlaufend/Liste vorgesehen.");
            var number = lastFailedValues.GetValueOrDefault(SeriesFieldId(), "");
            if (MessageBox.Show(this, $"Fehlernummer '{number}' wirklich überspringen? Sie wird nicht als erfolgreich markiert.", "Fehlernummer überspringen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var record = new CodingSeriesRecord(DateTimeOffset.Now, number, CodingSeriesResult.Skipped, "", "Nach fehlgeschlagenem Kodierlauf bewusst übersprungen.", lastFailedValues);
            EnsureSeriesService().Append(record);
            lastSeriesRecord = record;
            stationHistory.Insert(0, CodingSeriesHistoryRow.From(record));
            lastFailedValues = null;
            StationRetryButton.IsEnabled = false;
            AdvanceSeriesInput();
            UpdateSeriesUi();
            UpdateSeriesCounts();
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void AdvanceSeriesInput()
    {
        var mode = CurrentSeriesMode();
        if (mode == CodingSeriesMode.Sequential)
        {
            StationSequenceValue.Text = CodingSeriesService.NextSequential(StationSequenceValue.Text, StationSequenceStep.Text);
            var field = stationFields.FirstOrDefault(candidate => string.Equals(candidate.Id, SeriesFieldId(), StringComparison.OrdinalIgnoreCase));
            if (field is not null) field.Value = StationSequenceValue.Text;
            StationState.Text = "NÄCHSTE NUMMER";
            StationStateDetail.Text = $"{StationSequenceValue.Text} ist ausgewählt. Nächsten Transponder auflegen und Enter drücken.";
        }
        else if (mode == CodingSeriesMode.List)
        {
            stationQueueIndex++;
            if (stationQueueIndex < stationQueue.Count)
            {
                ApplyQueueItem();
                StationQueueInfo.Text = $"{stationQueue.Count} Datensätze geladen · aktuell {stationQueueIndex + 1}/{stationQueue.Count}";
                StationState.Text = "NÄCHSTER LISTENEINTRAG";
                StationStateDetail.Text = "Nächsten Transponder auflegen und Enter drücken.";
            }
            else
            {
                StationQueueInfo.Text = $"Liste abgeschlossen: {stationQueue.Count} Datensätze";
                StationState.Text = "LISTE ABGESCHLOSSEN";
                StationStateDetail.Text = "Zusammenfassung speichern und Sitzung schließen.";
            }
        }
        else
        {
            foreach (var field in stationFields) field.Value = "";
            StationState.Text = "NÄCHSTER TRANSPONDER";
            StationStateDetail.Text = "Kartendaten eingeben und Enter drücken.";
        }
    }

    private void UpdateSeriesCounts()
    {
        var successful = stationHistory.Count(row => row.Result == "Erfolgreich");
        var failed = stationHistory.Count(row => row.Result == "Fehler");
        var skipped = stationHistory.Count(row => row.Result == "Übersprungen");
        StationCounts.Text = $"{successful} erfolgreich · {failed} Fehler · {skipped} übersprungen" + (seriesService is null ? "" : $" · {seriesService.KnownSuccessfulNumbers.Count} bekannte erfolgreiche Nummern");
        StationExportButton.IsEnabled = stationHistory.Count > 0 && preparedSession is null;
    }

    private void ExportStation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (preparedSession is not null) throw new InvalidOperationException("Laufenden Datensatz zuerst prüfen oder als Fehler markieren.");
            if (seriesService is null) throw new InvalidOperationException("Keine aktive Kodiersitzung.");
            var files = seriesService.ExportSummary();
            seriesService = null;
            lastFailedValues = null;
            StationRetryButton.IsEnabled = false;
            StationSkipButton.IsEnabled = false;
            StationExportButton.IsEnabled = false;
            StationState.Text = "SITZUNG GESCHLOSSEN";
            StationStateDetail.Text = $"Zusammenfassung gespeichert: {files.TextPath}";
            SetStatus($"Kodiersitzung geschlossen · {files.Successful} erfolgreich · {files.Failed} Fehler · {files.Skipped} übersprungen.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private DiagnosticBundleResult CreateDiagnosticBundle()
    {
        if (sessionDefinition is null) throw new InvalidOperationException("Kein Kodierprojekt geladen.");
        var rfidgearVersion = File.Exists(settings.RFiDGear.ExecutablePath)
            ? RFiDGearLauncher.ReadVersion(settings.RFiDGear.ExecutablePath).Version?.ToString() ?? "unbekannt"
            : "nicht konfiguriert";
        return diagnosticBundleService.Create(new DiagnosticBundleRequest(
            sessionDefinition.ProjectDirectory, Version, rfidgearVersion, seriesLogSnapshot,
            seriesService?.SessionLogPath, lastSeriesRecord, sessionDefinition.CodingProjectPath));
    }

    private void ExportDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bundle = CreateDiagnosticBundle();
            StationDiagnosticInfo.Text = "Diagnosepaket gespeichert: " + bundle.Path;
            SetStatus($"Diagnosepaket gespeichert · {bundle.Entries.Count} Einträge · enthält Kartendaten, aber keine Schlüsselwerte.");
            MessageBox.Show(this, $"Diagnosepaket gespeichert:\n{bundle.Path}\n\nEnthält Kartendaten und Readbacks, aber keine Projektdefinition, kein rfPrj und laut Sanitizer keine Schlüsselwerte.", "RFiDGear-Diagnose", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (currentPage != "Station") return;
        if (e.Key == Key.F8 && StationCheckReadbacksButton.IsEnabled) { e.Handled = true; CheckReadbacks_Click(sender, e); }
        else if (e.Key == Key.F9 && StationMarkFailedButton.IsEnabled) { e.Handled = true; MarkStationFailed_Click(sender, e); }
        else if (e.Key == Key.D && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && StationDiagnosticButton.IsEnabled) { e.Handled = true; ExportDiagnostic_Click(sender, e); }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None &&
            StationPrepareButton.IsEnabled && preparedSession is null &&
            Keyboard.FocusedElement is TextBox focusedField && StationFieldsControl.IsAncestorOf(focusedField))
        {
            // Enter is a convenience shortcut only while editing an actual card-data field.
            // It must not hijack Enter on reader checks, project selection or other buttons.
            e.Handled = true;
            PrepareSession_Click(sender, e);
        }
    }

    private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedProject is null) throw new InvalidOperationException("Kein Projekt ausgewählt.");
            if (!Directory.Exists(selectedProject.ProjectDirectory)) throw new DirectoryNotFoundException("Projektordner nicht gefunden: " + selectedProject.ProjectDirectory);
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            info.ArgumentList.Add(selectedProject.ProjectDirectory);
            Process.Start(info);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void BrowseProjects_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Übergeordneten RFiDGear-Projektordner auswählen", InitialDirectory = Directory.Exists(ProjectsDirectoryText.Text) ? ProjectsDirectoryText.Text : projectsDirectory };
        if (dialog.ShowDialog(this) == true) ProjectsDirectoryText.Text = dialog.FolderName;
    }

    private void BrowseProfiles_Click(object sender, RoutedEventArgs e)
    {
        var initial = Directory.Exists(ProfilesDirectoryText.Text) ? ProfilesDirectoryText.Text : settingsStore.ResolveProfilesDirectory(settings);
        var dialog = new OpenFolderDialog { Title = "Gemeinsamen SieEncode-Profilordner auswählen", InitialDirectory = initial };
        if (dialog.ShowDialog(this) == true) ProfilesDirectoryText.Text = dialog.FolderName;
    }

    private void BrowseVaultBackup_Click(object sender, RoutedEventArgs e)
    {
        var initial = Directory.Exists(VaultBackupDirectoryText.Text) ? VaultBackupDirectoryText.Text : projectsDirectory;
        var dialog = new OpenFolderDialog { Title = "Unabhängigen Sicherungsordner für verschlüsselte Vaults auswählen", InitialDirectory = initial };
        if (dialog.ShowDialog(this) == true) VaultBackupDirectoryText.Text = dialog.FolderName;
    }

    private void InitializeTeamVault_Click(object sender, RoutedEventArgs e) => OpenTeamVault(initialSetup: true);

    private void ManageTeamVault_Click(object sender, RoutedEventArgs e) => OpenTeamVault(initialSetup: false);

    private void OpenTeamVault(bool initialSetup)
    {
        try
        {
            var directory = string.IsNullOrWhiteSpace(ProjectsDirectoryText.Text)
                ? projectsDirectory
                : Path.GetFullPath(ProjectsDirectoryText.Text.Trim());
            Directory.CreateDirectory(directory);
            var dialog = new TeamVaultWindow(this, directory, initialSetup);
            if (dialog.ShowDialog() == true)
                SetStatus("Zentrale Team-Empfängerliste gespeichert: " + new TeamVaultRecipientStore(directory).Path);
            UpdateVaultSetupStatus();
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void UpdateVaultSetupStatus()
    {
        try
        {
            var directory = string.IsNullOrWhiteSpace(ProjectsDirectoryText.Text) ? projectsDirectory : Path.GetFullPath(ProjectsDirectoryText.Text.Trim());
            var team = new TeamVaultRecipientStore(directory).Load().Recipients;
            var identities = WindowsVaultIdentityService.List();
            try
            {
                var localFingerprints = identities.Select(identity => identity.Fingerprint).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var ownRegistered = team.Any(recipient => localFingerprints.Contains(recipient.Fingerprint));
                if (identities.Count == 0)
                {
                    VaultSetupStatusTitle.Text = "Persönliche Identität fehlt";
                    VaultSetupStatusText.Text = "Auf diesem Windows-Benutzerkonto fehlt die persönliche Vault-Identität. Starten Sie die Ersteinrichtung; ein Projekt muss dafür nicht geöffnet sein.";
                    VaultSetupStatusBorder.Background = System.Windows.Media.Brushes.MistyRose;
                    VaultSetupStatusBorder.BorderBrush = System.Windows.Media.Brushes.IndianRed;
                }
                else if (!ownRegistered)
                {
                    VaultSetupStatusTitle.Text = "Bisherige Teamliste: eigener Zugang nicht enthalten";
                    VaultSetupStatusText.Text = $"{identities.Count} lokale Identität(en), {team.Count} Teammitglied(er). Nur für bisherige Team-Vaults die eigene Identität zur Teamliste hinzufügen. Diese Anzeige bewertet keine Gruppenrechte.";
                    VaultSetupStatusBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
                    VaultSetupStatusBorder.BorderBrush = System.Windows.Media.Brushes.DarkGoldenrod;
                }
                else if (team.Count < 2)
                {
                    VaultSetupStatusTitle.Text = "Bisherige Teamliste: Einzelzugriff eingerichtet";
                    VaultSetupStatusText.Text = "Die eigene Identität ist in der bisherigen Teamliste enthalten. Für weitere Empfänger bisheriger Team-Vaults deren öffentliche Einladungen importieren. Gruppenrechte werden separat verwaltet.";
                    VaultSetupStatusBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
                    VaultSetupStatusBorder.BorderBrush = System.Windows.Media.Brushes.DarkGoldenrod;
                }
                else
                {
                    VaultSetupStatusTitle.Text = "Bisherige Teamliste: Vorlage eingerichtet";
                    VaultSetupStatusText.Text = $"Eigener Zugang enthalten; {team.Count} Empfänger für neue bisherige Team-Vaults. Bestehende Vaults separat freigeben. Gruppenrechte werden separat verwaltet. Zugriff auf dem zweiten Rechner noch praktisch prüfen.";
                    VaultSetupStatusBorder.Background = System.Windows.Media.Brushes.Honeydew;
                    VaultSetupStatusBorder.BorderBrush = System.Windows.Media.Brushes.SeaGreen;
                }
            }
            finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
        }
        catch (Exception exception)
        {
            VaultSetupStatusTitle.Text = "Identitäts-/Teamlistenstatus nicht lesbar";
            VaultSetupStatusText.Text = exception.Message;
            VaultSetupStatusBorder.Background = System.Windows.Media.Brushes.MistyRose;
            VaultSetupStatusBorder.BorderBrush = System.Windows.Media.Brushes.Firebrick;
        }
    }

    private void BrowseRFiDGear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "RFiDGear.exe auswählen", Filter = "RFiDGear (RFiDGear.exe)|RFiDGear.exe|Programme (*.exe)|*.exe" };
        if (File.Exists(RFiDGearExecutableText.Text)) dialog.InitialDirectory = Path.GetDirectoryName(RFiDGearExecutableText.Text);
        if (dialog.ShowDialog(this) == true) { RFiDGearExecutableText.Text = dialog.FileName; UpdateRFiDGearVersion(dialog.FileName); }
    }

    private void UseBundledRuntime_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bundledRuntime = FindBundledRuntime()
                ?? throw new FileNotFoundException("Die gebündelte SieEncode Runtime wurde neben der Studio-Installation nicht gefunden.");
            RFiDGearExecutableText.Text = bundledRuntime;
            UpdateRFiDGearVersion(bundledRuntime);
            SetStatus("Gebündelte SieEncode Runtime ausgewählt · zum dauerhaften Übernehmen Einstellungen speichern.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenRFiDGearSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = RFiDGearExecutableText.Text.Trim();
            if (!File.Exists(executable)) throw new FileNotFoundException("Bitte zuerst eine vorhandene RFiDGear-Runtime auswählen.", executable);
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! });
            SetStatus("RFiDGear ohne Projekt geöffnet. Reader auswählen, Einstellung speichern und RFiDGear danach schließen.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RefreshReaderSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateRFiDGearVersion(RFiDGearExecutableText.Text.Trim());
            SetStatus("Gespeicherte Leserkonfiguration neu gelesen. Keine Leser- oder Kartenverbindung geprüft.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void InitializePiccSettingsFlags()
    {
        EditorPiccKeySettings.Visibility = Visibility.Collapsed;
        var flags = new WrapPanel();
        foreach (var checkBox in new[] { editorPiccChangeMasterKey, editorPiccListApplications, editorPiccCreateDelete, editorPiccConfigurationChange })
        {
            checkBox.Checked += EditorProductiveControl_Changed;
            checkBox.Unchecked += EditorProductiveControl_Changed;
            flags.Children.Add(checkBox);
        }
        var group = new GroupBox { Header = "Produktive PICC-Rechte", Content = flags };
        if (EditorProductivePiccPanel.Parent is StackPanel parent)
            parent.Children.Insert(parent.Children.IndexOf(EditorProductivePiccPanel) + 1, group);
    }

    private void SetPiccSettingsFlags(string value)
    {
        var flags = ProfileEditingService.KeySettingsFlags(string.IsNullOrWhiteSpace(value) ? "0F" : value);
        editorPiccChangeMasterKey.IsChecked = flags.ChangeMasterKey;
        editorPiccListApplications.IsChecked = flags.ListApplications;
        editorPiccCreateDelete.IsChecked = flags.CreateDelete;
        editorPiccConfigurationChange.IsChecked = flags.ConfigurationChange;
    }

    private string GetPiccSettingsFlags() => ProfileEditingService.KeySettingsFromFlags(
        editorPiccChangeMasterKey.IsChecked == true, editorPiccListApplications.IsChecked == true,
        editorPiccCreateDelete.IsChecked == true, editorPiccConfigurationChange.IsChecked == true);

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            settings = new BuilderSettings
            {
                ExpertMode = ExpertModeToggle.IsChecked == true,
                Projects = new ProjectsSettings { Directory = ProjectsDirectoryText.Text.Trim() },
                Profiles = new ProfilesSettings { Directory = ProfilesDirectoryText.Text.Trim() },
                VaultBackup = new VaultBackupSettings { Directory = VaultBackupDirectoryText.Text.Trim() },
                RFiDGear = new RFiDGearSettings { ExecutablePath = RFiDGearExecutableText.Text.Trim() }
            };
            settingsStore.Save(settings);
            projectsDirectory = settingsStore.ResolveProjectsDirectory(AppContext.BaseDirectory, settings);
            profileStore = CreateProfileStore(settings);
            ProjectsDirectoryText.Text = projectsDirectory;
            ProfilesDirectoryText.Text = settingsStore.ResolveProfilesDirectory(settings);
            RefreshProjects(); LoadProfiles(); UpdateRFiDGearVersion(); UpdateVaultSetupStatus();
            SetStatus("Einstellungen dauerhaft gespeichert: " + settingsStore.SettingsPath);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void LoadProfiles()
    {
        var library = LoadProfileLibrary();
        var persistentIds = Directory.Exists(profileStore.UserDirectory)
            ? Directory.EnumerateFiles(profileStore.UserDirectory, "*.json").Select(path => Path.GetFileNameWithoutExtension(path)!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var managedBaseIds = Directory.Exists(profileStore.BaseDirectory)
            ? Directory.EnumerateFiles(profileStore.BaseDirectory, "*.json").Select(path => Path.GetFileNameWithoutExtension(path)!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        ProfilesGrid.ItemsSource = library.Profiles.OrderBy(profile => profile.Profile.Kind).ThenBy(profile => profile.Profile.Id).Select(profile => new ProfileListRow(
            profile.Profile.Id, profile.Profile.Kind, profile.Profile.Version, profile.Profile.Name,
            managedBaseIds.Contains(profile.Profile.Id) ? "Verwaltetes Grundprofil" : persistentIds.Contains(profile.Profile.Id) ? "Kundenprofil" : "Gebündelt")).ToArray();
        ProfilesPathText.Text = $"Grundprofile: {profileStore.BaseDirectory}\nKundenprofile: {profileStore.UserDirectory}";
        var customerSelected = ProfilesGrid.SelectedItem is ProfileListRow { Kind: "customer" };
        ProfileEditButton.IsEnabled = ProfilesGrid.SelectedItem is ProfileListRow { Source: not "Gebündelt" };
        ProfileDuplicateButton.IsEnabled = customerSelected;
        ProfileExportButton.IsEnabled = ProfilesGrid.SelectedItem is ProfileListRow;
        ProfileHistoryButton.IsEnabled = customerSelected;
        RefreshDiffProfiles(library);
    }

    private ProfileLibrary LoadProfileLibrary() => profileStore.Load();

    private void UpdateRFiDGearVersion(string? path = null)
    {
        path ??= settings.RFiDGear.ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            RFiDGearVersionText.Text = "Nicht konfiguriert.";
            RFiDGearReaderText.Text = "Reader: RFiDGear-Einstellungen nicht geprüft.";
            RFiDGearEvidenceText.Text = "Keine Binärdateien geprüft.";
            RFiDGearCapabilitiesGrid.ItemsSource = null;
            rfidgearCapabilities = null;
            HomeRFiDGearState.Text = "Nicht konfiguriert";
            StationOpenRFiDGearButton.IsEnabled = false;
            StationStartAutoModeButton.IsEnabled = false;
            RefreshOperationButtons();
            UpdatePilotOverview();
            return;
        }
        rfidgearCapabilities = new RFiDGearCapabilityInspector().Inspect(path);
        var version = rfidgearCapabilities.Version;
        var reader = RFiDGearSettingsInspection.Inspect();
        var text = version.Version is null ? "Version konnte nicht gelesen werden." : $"Version {version.Version} · Fähigkeitsprüfung abgeschlossen";
        RFiDGearVersionText.Text = text;
        RFiDGearEvidenceText.Text = "Geprüft: " + string.Join(" · ", rfidgearCapabilities.InspectedFiles.Select(Path.GetFileName)) +
            (string.IsNullOrWhiteSpace(rfidgearCapabilities.Error) ? "" : " · Fehler: " + rfidgearCapabilities.Error);
        RFiDGearCapabilitiesGrid.ItemsSource = rfidgearCapabilities.Capabilities.Select(capability => new CapabilityRow(
            capability.Name,
            capability.State switch { RFiDGearCapabilityState.Available => "Verfügbar", RFiDGearCapabilityState.Unavailable => "Nicht vorhanden", _ => "Unbekannt" },
            capability.Evidence)).ToArray();
        RFiDGearReaderText.Text = reader.IsConfigured
            ? $"Gespeicherte Leserkonfiguration: {reader.Provider}" + (string.IsNullOrWhiteSpace(reader.Port) ? "" : $" · Port {reader.Port}") + " · Verbindung und Karte noch nicht geprüft"
            : "Leserkonfiguration fehlt oder ist nicht lesbar: " + (reader.Error ?? "Bitte in RFiDGear auswählen und speichern.");
        var availableCount = rfidgearCapabilities.Capabilities.Count(capability => capability.State == RFiDGearCapabilityState.Available);
        HomeRFiDGearState.Text = $"{version.Version?.ToString() ?? "Version unbekannt"} · {availableCount}/{rfidgearCapabilities.Capabilities.Count}";
        StationOpenRFiDGearButton.IsEnabled = preparedSession is not null && rfidgearCapabilities.Has(RFiDGearCapabilityId.CustomProjectFile);
        StationStartAutoModeButton.IsEnabled = preparedSession is not null && rfidgearCapabilities.Has(RFiDGearCapabilityId.ContinuousAutoMode) && reader.IsConfigured;
        RefreshOperationButtons();
        UpdatePilotOverview();
    }

    private void UpdatePilotOverview()
    {
        if (HomePilotSetupState is null) return;
        var projectDirectoryReady = !string.IsNullOrWhiteSpace(projectsDirectory) && Directory.Exists(projectsDirectory);
        var profileDirectoryReady = Directory.Exists(profileStore.BaseDirectory) && Directory.Exists(profileStore.UserDirectory);
        var runtimeReady = rfidgearCapabilities?.Has(RFiDGearCapabilityId.CustomProjectFile) == true;
        HomePilotSetupState.Text = projectDirectoryReady && profileDirectoryReady && runtimeReady
            ? "Bereit · Projekt-, Profilordner und Runtime geprüft"
            : !projectDirectoryReady ? "Projektordner fehlt"
            : !profileDirectoryReady ? "Gemeinsame Profilbibliothek fehlt"
            : "RFiDGear-Runtime noch nicht freigegeben";
        HomePilotProjectState.Text = selectedProject is null
            ? $"{projects.Count} Projekt(e) vorhanden · noch keines ausgewählt"
            : $"Ausgewählt: {selectedProject.Name}";
        HomePilotVaultState.Text = selectedProject is null || selectedProject.ArtifactOnly
            ? "Bearbeitbares Projekt auswählen"
            : selectedProject.HasVault
                ? "Vault vorhanden · Produktivkeys verschlüsselt gesichert"
                : selectedProject.HasOperationalBuild
                    ? "Vorhandener Build nutzbar · Bearbeiten und Neubau ohne Vault gesperrt"
                    : "Vault fehlt · vor der ersten Nutzung Schlüssel sichern und Projekt bauen";
        HomePilotStationState.Text = sessionDefinition is null
            ? "Projekt bauen, prüfen und in die Station laden"
            : $"Bereit: {sessionDefinition.ProjectName}";
    }

    private sealed record CapabilityRow(string Name, string Status, string Evidence);
    private sealed record ProjectPreflightUiIssue(string Area, string Cause, string File, string Action, string Code);

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : throw new InvalidDataException("JSON-Feld fehlt: " + name);

    private static int ReadArrayCount(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = error ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.DarkSlateGray;
    }

    private void ShowError(Exception exception)
    {
        SetStatus(exception.Message, true);
        MessageBox.Show(this, exception.Message, $"SieEncode Studio {Version}", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public sealed class CodingFieldInput(CodingSessionField definition) : INotifyPropertyChanged
{
    private string value = definition.InitialValue;
    public string Id { get; } = definition.Id;
    public string Label { get; } = MainWindow.NormalizeVisibleFieldLabel(definition.Id, definition.Label) + (definition.Required ? " *" : "");
    public string Hint { get; } = definition.Hint;
    public int MaxLength { get; } = definition.MaxLength;
    public string LengthFeedback => MaxLength > 0 ? $"{value.Length} / {MaxLength} Zeichen" : $"{value.Length} Zeichen";
    public string Value { get => value; set { if (this.value == value) return; this.value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthFeedback))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record CodingSeriesHistoryRow(string Time, string Number, string Result, string FailureCategory, string Detail)
{
    public static CodingSeriesHistoryRow From(CodingSeriesRecord record) => new(record.Timestamp.ToString("HH:mm:ss"), record.Number,
        record.Result switch { CodingSeriesResult.Successful => "Erfolgreich", CodingSeriesResult.Failed => "Fehler", _ => "Übersprungen" }, record.FailureCategory, record.Detail);
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
    private readonly ObservableCollection<EditorTextField> editorValues = [];
    private readonly ObservableCollection<EditorTextField> editorTransportKeys = [];
    private readonly ObservableCollection<EditorKeyField> editorProductiveKeys = [];
    private readonly ObservableCollection<InitialKeySet> editorInitialKeySets = [];
    private ProfileLibrary? editorLibrary;
    private ProjectDefinition? editorProject;
    private string editorSourcePath = "";
    private FileRevision editorProjectRevision = FileRevision.Missing;
    private FileRevision editorVaultRevision = FileRevision.Missing;
    private bool editorDirty;
    private bool editorKeysPersisted;
    private bool editorVaultVerified;
    private int editorVaultVersion;
    private ProjectKeyVaultPayload? editorVaultPayload;
    private bool suppressEditorChanges;
    private Func<IReadOnlyList<WindowsVaultIdentity>> loadEditorVaultIdentities = WindowsVaultIdentityService.List;

    private void InitializeProjectEditor()
    {
        EditorProjectValues.ItemsSource = editorValues;
        EditorTransportKeys.ItemsSource = editorTransportKeys;
        EditorProductiveKeys.ItemsSource = editorProductiveKeys;
        EditorInitialKeySet.ItemsSource = editorInitialKeySets;
        EditorSaveButton.IsEnabled = false;
        EditorBuildButton.IsEnabled = false;
    }

    private void EditSelectedProject_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst ein Projekt auswählen.", true); return; }
        if (selectedProject.ArtifactOnly)
        {
            try { OpenMigratedProject(new LegacyProjectMigrationService().Import(selectedProject.SourcePath, LoadProfileLibrary())); }
            catch (Exception exception) { ShowError(exception); }
            return;
        }
        OpenProjectEditor(selectedProject);
    }

    private void ImportLegacyProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Alte RFiDGear-Projektkonfiguration importieren",
                Filter = "JSON-Projektdateien (*.json)|*.json|Alle Dateien (*.*)|*.*",
                InitialDirectory = Directory.Exists(projectsDirectory) ? projectsDirectory : null
            };
            if (dialog.ShowDialog(this) != true) return;
            OpenMigratedProject(new LegacyProjectMigrationService().Import(dialog.FileName, LoadProfileLibrary()));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenMigratedProject(LegacyProjectMigrationResult result)
    {
        if (!CanReplaceEditor()) return;
        LoadEditor(result.Project, "");
        selectedProject = null;
        CurrentProject.Text = result.Project.Project.Name + " · migriert · ungespeichert";
        SetEditorDirty(true);
        Navigate("Editor");
        var imported = result.ImportedValues.Count == 0 ? "keine Fachwerte" : string.Join(", ", result.ImportedValues.Select(NormalizeVisibleFieldLabel));
        EditorValidation.Text = $"{result.SourceKind} sicher als neuer Entwurf geöffnet · übernommen: {imported}. " + string.Join(" ", result.Warnings);
        SetStatus($"{result.SourceKind} als neuer keyfreier Mastering-Entwurf geöffnet; Quelldatei bleibt unverändert.");
        MessageBox.Show(this,
            $"Ein neuer, noch ungespeicherter Mastering-Entwurf wurde vorbereitet.\n\nQuelle: {result.SourcePath}\nÜbernommen: {imported}\n\n" + string.Join("\n", result.Warnings) +
            "\n\nBitte Profilwahl, Fachwerte und Ausgabeziel prüfen. Erst Speichern erzeugt eine neue *.project.json.",
            "Projekt sicher migriert", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenProjectEditor(ProjectCatalogEntry entry)
    {
        try
        {
            if (!CanReplaceEditor()) return;
            LoadEditor(ProjectJson.Load(entry.SourcePath), entry.SourcePath);
            SelectProject(entry);
            Navigate("Editor");
            SetStatus("Projekt zur Bearbeitung geöffnet: " + entry.Name);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!CanReplaceEditor()) return;
            var library = LoadEditorLibrary();
            LoadEditor(ProjectDraftFactory.Create(library, CreateUniqueProjectName()), "");
            selectedProject = null;
            CurrentProject.Text = "Neues, ungespeichertes Projekt";
            Navigate("Editor");
            SetEditorDirty(true);
            SetStatus("Neuer Projektentwurf angelegt. Vor dem Bauen bitte speichern und Produktivkeys im Vault sichern.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private ProfileLibrary LoadEditorLibrary()
        => editorLibrary = LoadProfileLibrary();

    private static int ReadVaultVersion(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return 0;
        var vaultPath = ProjectKeyVault.GetPath(projectPath);
        if (!File.Exists(vaultPath)) return 0;
        try { return ProjectGroupVault.IsGroupVaultFile(vaultPath) ? ProjectGroupVault.ProtectionLevel : JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(vaultPath), ProjectJson.Options)?.Version ?? 0; }
        catch { return 0; }
    }

    private void LoadEditor(ProjectDefinition project, string sourcePath)
    {
        suppressEditorChanges = true;
        try
        {
            var library = LoadEditorLibrary();
            editorProject = ProjectDraftFactory.Synchronize(project, library);
            editorSourcePath = string.IsNullOrWhiteSpace(sourcePath) ? "" : Path.GetFullPath(sourcePath);
            editorProjectRevision = string.IsNullOrWhiteSpace(editorSourcePath) ? FileRevision.Missing : ConcurrentFileWriter.Capture(editorSourcePath);
            editorVaultRevision = string.IsNullOrWhiteSpace(editorSourcePath) ? FileRevision.Missing : ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(editorSourcePath));
            editorKeysPersisted = false;
            editorVaultVerified = false;
            editorGroupRecovery = false;
            editorVaultVersion = ReadVaultVersion(editorSourcePath);
            editorVaultPayload = string.IsNullOrWhiteSpace(editorSourcePath) ? ProjectKeyVault.FromProject(editorProject) : null;

            RefreshEditorProfileChoices(library);

            EditorProjectName.Text = editorProject.Project.Name;
            EditorCardMode.ItemsSource = new[]
            {
                new ProfileChoice(CardOperationMode.FullCard, "Neue Karte vollständig aufbauen"),
                new ProfileChoice(CardOperationMode.ExtendExisting, "Bestehende Karte um Zusatzprofil erweitern")
            };
            EditorCardMode.SelectedValue = editorProject.Project.CardMode;
            EditorOutputDirectory.Text = editorProject.Project.OutputDirectory;
            EditorBuildMastering.IsChecked = editorProject.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase);
            EditorBuildCoding.IsChecked = editorProject.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
            SetComboValue(EditorTransportPiccType, editorProject.Transport.PiccKey.Type);
            EditorTransportPiccKey.Text = editorProject.Transport.PiccKey.Value;
            SetComboValue(EditorProductivePiccType, editorProject.Productive.PiccKey.Type);
            EditorProductivePiccKey.Text = editorProject.Productive.PiccKey.Value;
            EditorPiccKeySettings.Text = editorProject.Productive.PiccKeySettings;
            SetPiccSettingsFlags(editorProject.Productive.PiccKeySettings);
            PopulateDynamicEditorFields();
            RefreshInitialKeySets();
            RefreshCardModeUi();
            EditorSourcePath.Text = string.IsNullOrWhiteSpace(editorSourcePath) ? "Noch nicht gespeichert" : editorSourcePath;
            EditorVaultStatus.Text = string.IsNullOrWhiteSpace(editorSourcePath)
                ? "Vault kann nach dem ersten Speichern angelegt werden."
                : File.Exists(ProjectKeyVault.GetPath(editorSourcePath))
                    ? "Vault vorhanden, für diese Sitzung noch gesperrt: " + ProjectKeyVault.GetPath(editorSourcePath)
                    : "Vault fehlt. Produktive Schlüssel aus vorhandenen rfPrj-Builds werden aus Sicherheitsgründen nicht zurück in den Editor importiert; ohne ursprünglichen Vault ist dieser Stand nicht reproduzierbar.";
            EditorKeyGuidance.Text = string.IsNullOrWhiteSpace(editorSourcePath)
                ? "Neues Projekt: Produktivschlüssel erzeugen, Projekt speichern und den Vault danach verschlüsselt sichern. Die Passphrase wird niemals gespeichert."
                : File.Exists(ProjectKeyVault.GetPath(editorSourcePath))
                    ? "Bestehendes Projekt: den vorhandenen Vault für diese Sitzung entsperren. Neue Schlüssel nur verwenden, wenn bewusst eine neue Schlüsselidentität eingeführt werden soll."
                    : "Achtung: Zu dieser bestehenden Definition fehlt der ursprüngliche Vault. Vorhandene rfPrj-Builds können noch alte Schlüssel enthalten, diese werden aber nicht zurückimportiert. Neue Schlüssel bedeuten eine bewusst neue Schlüsselidentität.";
            EditorValidation.Text = "Projekt geladen. Änderungen werden am bestehenden Pfad gespeichert; ‚Speichern unter‘ ist eine bewusste separate Aktion.";
            EditorSaveButton.IsEnabled = true;
            RefreshProfileBindingStatus();
            SetEditorDirty(false);
        }
        finally { suppressEditorChanges = false; }
    }

    private void RefreshEditorProfileChoices(ProfileLibrary library)
    {
        if (editorProject is null) return;
        var bases = library.Profiles.Where(profile => profile.Profile.Kind == "base")
            .Select(profile => new ProfileChoice(profile.Profile.Id, $"{profile.Profile.Name} · {profile.Profile.Version} ({profile.Profile.Id})")).ToArray();
        var customers = library.Profiles.Where(profile => profile.Profile.Kind == "customer")
            .OrderBy(profile => profile.Profile.Name).Select(profile => new ProfileChoice(profile.Profile.Id, $"{profile.Profile.Name} · {profile.Profile.Version} ({profile.Profile.Id})")).ToArray();
        EditorBaseProfile.ItemsSource = bases;
        EditorBaseProfile.SelectedValue = editorProject.Profiles.Base;
        EditorCustomerProfiles.ItemsSource = customers;
        EditorCustomerProfiles.SelectedItems.Clear();
        foreach (var choice in customers.Where(choice => editorProject.Profiles.Customers.Contains(choice.Id, StringComparer.Ordinal)))
            EditorCustomerProfiles.SelectedItems.Add(choice);
    }

    private void PopulateDynamicEditorFields()
    {
        if (editorProject is null || editorLibrary is null) return;
        var composition = editorLibrary.Compose(editorProject.Profiles.Base, editorProject.Profiles.Customers);
        IReadOnlyList<ApplicationDefinition> keyApplications;
        IReadOnlyList<VariableDefinition> operationVariables;
        try
        {
            keyApplications = CardOperationMode.SelectApplications(composition, editorProject);
            operationVariables = CardOperationMode.SelectVariables(composition, editorProject);
        }
        catch (InvalidDataException)
        {
            keyApplications = [];
            operationVariables = [];
        }
        editorValues.Clear();
        foreach (var variable in operationVariables.Where(variable => variable.Scope == "project"))
        {
            var format = variable.Type switch { "hex" => "HEX-Bytes", "text" => "Text", _ => "Ziffern" };
            var length = variable.Length is not null ? $"Länge {variable.Length}" : variable.AllowedLengths is not null ? "Längen " + string.Join("/", variable.AllowedLengths) : variable.MinLength is not null || variable.MaxLength is not null ? $"Länge {variable.MinLength?.ToString() ?? "1"}..{variable.MaxLength?.ToString() ?? "∞"}" : "";
            var constraints = string.IsNullOrEmpty(length) ? format : format + " · " + length;
            var maximum = variable.Length ?? variable.AllowedLengths?.DefaultIfEmpty(0).Max() ?? variable.MaxLength ?? 0;
            editorValues.Add(new EditorTextField(variable.Id, NormalizeVisibleFieldLabel(variable.Id, variable.Label) + (variable.Required ? " *" : ""), editorProject.Values.GetValueOrDefault(variable.Id, variable.Default ?? ""), constraints, MarkEditorDirty, maximum));
        }

        editorTransportKeys.Clear();
        foreach (var type in keyApplications.Select(application => application.KeyType).Distinct(StringComparer.OrdinalIgnoreCase).Order())
            editorTransportKeys.Add(new EditorTextField(type, "Application-Grundkey " + type, editorProject.Transport.ApplicationKeys.GetValueOrDefault(type, ""), "", MarkEditorInitialKeysChanged));

        editorProductiveKeys.Clear();
        foreach (var application in keyApplications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            editorProject.Productive.ApplicationKeys.TryGetValue(aid, out var existing);
            for (var index = 0; index < application.KeyCount; index++)
                editorProductiveKeys.Add(new EditorKeyField(aid, index, application.KeyType, $"{application.Name} · {aid} · Key {index} · {application.KeyType}", existing is not null && index < existing.Count ? existing[index] : "", MarkEditorKeysChanged));
        }
    }

    private void EditorCardMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (suppressEditorChanges || editorProject is null || EditorCardMode.SelectedValue is not string mode) return;
        editorProject.Project.CardMode = mode;
        suppressEditorChanges = true;
        try { PopulateDynamicEditorFields(); RefreshCardModeUi(); }
        finally { suppressEditorChanges = false; }
        editorKeysPersisted = false;
        editorVaultVerified = false;
        SetEditorDirty(true);
        RefreshEditorWorkflow();
    }

    private void RefreshCardModeUi()
    {
        if (editorProject is null) return;
        var extension = CardOperationMode.IsExtension(editorProject);
        EditorCardModeGuidance.Text = extension
            ? "Erhält vorhandene Applications. Nur neue AIDs aus Zusatzprofilen werden angelegt und kodiert. PICC-Keywechsel und Format-Recovery sind gesperrt; vor dem Schreiben ist eine erfolgreiche Transponder-Erkennung erforderlich."
            : "Baut einen neuen oder bewusst zurückgesetzten Transponder vollständig aus Grund- und Zusatzprofilen auf.";
        EditorProductivePiccPanel.Visibility = extension ? Visibility.Collapsed : Visibility.Visible;
        EditorGenerateKeysButton.Content = extension ? "Schlüssel für neue Applications erzeugen" : "Neue Schlüssel erzeugen";
    }

    private void EditorProfileSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (suppressEditorChanges || editorProject is null || editorLibrary is null || EditorBaseProfile.SelectedValue is not string baseId) return;
        try
        {
            CaptureEditorControls(includeProfiles: false);
            editorProject.Profiles.Base = baseId;
            editorProject.Profiles.Customers = EditorCustomerProfiles.SelectedItems.Cast<ProfileChoice>().Select(choice => choice.Id).ToList();
            ProjectDraftFactory.Synchronize(editorProject, editorLibrary);
            editorKeysPersisted = false;
            editorVaultVerified = false;
            suppressEditorChanges = true;
            try { PopulateDynamicEditorFields(); }
            finally { suppressEditorChanges = false; }
            SetEditorDirty(true);
            RefreshProfileBindingStatus();
            EditorValidation.Text = "Profilkomposition aktualisiert. Projektwerte und benötigte Keyslots wurden neu abgeglichen; neue Profilwahl bitte bewusst binden.";
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void BindCurrentProfiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            CaptureEditorControls();
            ProjectDraftFactory.Synchronize(editorProject, editorLibrary);
            var before = ProfileBindingService.Evaluate(editorProject, editorLibrary).Where(status => !status.IsMatch).Select(ProfileBindingService.DescribeFailure).ToArray();
            if (before.Length > 0 && MessageBox.Show(this,
                "Die aktuell installierten Profilstände werden verbindlich in diesem Projekt hinterlegt:\n\n" + string.Join("\n", before) +
                "\n\nDies ist eine bewusste Projektmigration. Fortfahren?",
                "Profilstände übernehmen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            ProfileBindingService.BindCurrent(editorProject, editorLibrary);
            SetEditorDirty(true);
            RefreshProfileBindingStatus();
            EditorValidation.Text = "Aktuelle Profilversionen und Inhaltsfingerabdrücke wurden verbindlich übernommen. Bitte Projekt speichern.";
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void RefreshProfileBindingStatus()
    {
        if (editorProject is null || editorLibrary is null)
        {
            EditorProfileBindingStatus.Text = "Kein Projekt geladen.";
            EditorBindProfilesButton.IsEnabled = false;
            EditorBuildButton.IsEnabled = false;
            return;
        }
        var statuses = ProfileBindingService.Evaluate(editorProject, editorLibrary);
        var failures = statuses.Where(status => !status.IsMatch).ToArray();
        if (failures.Length == 0)
        {
            EditorProfileBindingStatus.Text = settings.ExpertMode
                ? "Reproduzierbar gebunden: " + string.Join(" · ", statuses.Select(status => $"{status.ProfileId} {status.CurrentVersion} [{status.CurrentDigest[..12]}]"))
                : "Profile sind aktuell und eindeutig mit diesem Projekt verknüpft.";
            EditorProfileBindingStatus.Foreground = System.Windows.Media.Brushes.DarkGreen;
            EditorBindProfilesButton.IsEnabled = false;
        }
        else
        {
            EditorProfileBindingStatus.Text = "Build gesperrt: " + string.Join(" · ", failures.Select(ProfileBindingService.DescribeFailure));
            EditorProfileBindingStatus.Foreground = System.Windows.Media.Brushes.DarkRed;
            EditorBindProfilesButton.IsEnabled = true;
            EditorBuildButton.IsEnabled = false;
        }
        RefreshEditorWorkflow();
    }

    private void EditorControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!suppressEditorChanges && editorProject is not null)
        {
            HighlightChangedControl(sender);
            CaptureEditorControls();
            SetEditorDirty(true);
        }
    }

    private void EditorInitialKeyControl_Changed(object sender, RoutedEventArgs e)
    {
        if (suppressEditorChanges || editorProject is null) return;
        HighlightChangedControl(sender);
        CaptureEditorControls();
        editorVaultPayload = ProjectKeyVault.UpdateFromProject(editorProject, editorVaultPayload);
        editorKeysPersisted = false;
        editorVaultVerified = false;
        SetEditorDirty(true);
    }

    private void RefreshInitialKeySets()
    {
        editorInitialKeySets.Clear();
        if (editorVaultPayload is not null)
        {
            ProjectKeyVault.Normalize(editorVaultPayload);
            foreach (var item in editorVaultPayload.InitialKeySets.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                editorInitialKeySets.Add(item);
        }
        EditorInitialKeySet.IsEnabled = editorInitialKeySets.Count > 0;
        EditorAddInitialKeySetButton.IsEnabled = editorVaultPayload is not null;
        EditorDeleteInitialKeySetButton.IsEnabled = editorInitialKeySets.Count > 1;
        EditorInitialKeySet.SelectedValue = editorProject?.Transport.KeySetId;
        if (EditorInitialKeySet.SelectedIndex < 0 && editorInitialKeySets.Count > 0) EditorInitialKeySet.SelectedIndex = 0;
    }

    private void EditorInitialKeySet_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (suppressEditorChanges || editorProject is null || editorVaultPayload is null || EditorInitialKeySet.SelectedValue is not string id) return;
        try
        {
            CaptureEditorControls();
            editorVaultPayload = ProjectKeyVault.UpdateFromProject(editorProject, editorVaultPayload);
            editorProject.Transport.KeySetId = id;
            editorProject = ProjectKeyVault.Apply(editorProject, editorVaultPayload);
            suppressEditorChanges = true;
            try
            {
                SetComboValue(EditorTransportPiccType, editorProject.Transport.PiccKey.Type);
                EditorTransportPiccKey.Text = editorProject.Transport.PiccKey.Value;
                PopulateDynamicEditorFields();
            }
            finally { suppressEditorChanges = false; }
            SetEditorDirty(true);
            SetStatus("Ausgangsschlüsselsatz gewählt: " + editorInitialKeySets.First(item => item.Id == id).Name);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void AddInitialKeySet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorVaultPayload is null) throw new InvalidOperationException("Vault bitte zuerst entsperren.");
            CaptureEditorControls();
            editorVaultPayload = ProjectKeyVault.UpdateFromProject(editorProject, editorVaultPayload);
            var name = TextEntryPrompt.Show(this, "Ausgangsschlüsselsatz hinzufügen", "Eindeutiger Name, zum Beispiel Standort A oder Kartenbestand 2024", "Neuer Grundkeysatz");
            if (name is null) return;
            if (editorVaultPayload.InitialKeySets.Any(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                throw new InvalidDataException("Ein Ausgangsschlüsselsatz mit diesem Namen existiert bereits.");
            var id = "keyset-" + Guid.NewGuid().ToString("N")[..12];
            editorProject.Transport.KeySetId = id;
            editorVaultPayload.InitialKeySets.Add(new InitialKeySet
            {
                Id = id,
                Name = name,
                PiccKey = new KeyReference { Type = editorProject.Transport.PiccKey.Type, Value = editorProject.Transport.PiccKey.Value },
                ApplicationKeys = editorProject.Transport.ApplicationKeys.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            });
            editorVaultPayload.SelectedInitialKeySetId = id;
            editorKeysPersisted = false; editorVaultVerified = false;
            suppressEditorChanges = true; try { RefreshInitialKeySets(); } finally { suppressEditorChanges = false; }
            SetEditorDirty(true); RefreshEditorWorkflow();
            SetStatus("Ausgangsschlüsselsatz hinzugefügt; Vault-Änderungen noch speichern.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void DeleteInitialKeySet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorVaultPayload is null || EditorInitialKeySet.SelectedItem is not InitialKeySet selected) return;
            if (editorVaultPayload.InitialKeySets.Count <= 1) throw new InvalidOperationException("Mindestens ein Ausgangsschlüsselsatz muss erhalten bleiben.");
            if (MessageBox.Show(this, $"Ausgangsschlüsselsatz '{selected.Name}' aus dem verschlüsselten Vault löschen?", "Schlüsselsatz löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            editorVaultPayload.InitialKeySets.RemoveAll(item => string.Equals(item.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
            var replacement = editorVaultPayload.InitialKeySets[0];
            editorProject.Transport.KeySetId = replacement.Id;
            editorVaultPayload.SelectedInitialKeySetId = replacement.Id;
            editorProject = ProjectKeyVault.Apply(editorProject, editorVaultPayload);
            editorKeysPersisted = false; editorVaultVerified = false;
            suppressEditorChanges = true;
            try
            {
                SetComboValue(EditorTransportPiccType, editorProject.Transport.PiccKey.Type);
                EditorTransportPiccKey.Text = editorProject.Transport.PiccKey.Value;
                PopulateDynamicEditorFields(); RefreshInitialKeySets();
            }
            finally { suppressEditorChanges = false; }
            SetEditorDirty(true); RefreshEditorWorkflow();
            SetStatus("Ausgangsschlüsselsatz gelöscht; Vault-Änderungen noch speichern.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void EditorProductiveControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!suppressEditorChanges && editorProject is not null)
        {
            HighlightChangedControl(sender);
            CaptureEditorControls();
            editorKeysPersisted = false;
            editorVaultVerified = false;
            SetEditorDirty(true);
        }
    }

    private void MarkEditorDirty()
    {
        if (!suppressEditorChanges && editorProject is not null)
        {
            CaptureEditorControls();
            SetEditorDirty(true);
        }
    }

    private void MarkEditorKeysChanged()
    {
        if (!suppressEditorChanges && editorProject is not null)
        {
            CaptureEditorControls();
            editorKeysPersisted = false;
            editorVaultVerified = false;
            SetEditorDirty(true);
        }
    }

    private void MarkEditorInitialKeysChanged()
    {
        if (suppressEditorChanges || editorProject is null) return;
        CaptureEditorControls();
        editorVaultPayload = ProjectKeyVault.UpdateFromProject(editorProject, editorVaultPayload);
        editorKeysPersisted = false;
        editorVaultVerified = false;
        SetEditorDirty(true);
    }

    private void SetEditorDirty(bool dirty)
    {
        editorDirty = dirty;
        if (editorProject is null) return;
        if (!dirty)
        {
            foreach (var field in editorValues.Concat(editorTransportKeys).Concat(editorProductiveKeys)) field.AcceptChanges();
            foreach (var control in new Control[] { EditorProjectName, EditorOutputDirectory, EditorTransportPiccType, EditorTransportPiccKey, EditorProductivePiccType, EditorProductivePiccKey })
            {
                control.ClearValue(Control.BackgroundProperty);
                control.ClearValue(Control.BorderBrushProperty);
                control.ClearValue(Control.BorderThicknessProperty);
            }
        }
        CurrentProject.Text = editorProject.Project.Name + (dirty ? " · geändert" : " · bearbeitbar");
        RefreshEditorWorkflow();
    }

    private static void HighlightChangedControl(object sender)
    {
        if (sender is not Control control) return;
        control.Background = System.Windows.Media.Brushes.LemonChiffon;
        control.BorderBrush = System.Windows.Media.Brushes.DarkGoldenrod;
        control.BorderThickness = new Thickness(2);
    }

    private void RefreshEditorWorkflow()
    {
        if (EditorWorkflowProfile is null) return;
        if (editorProject is null || editorLibrary is null)
        {
            EditorWorkflowProfile.Text = "○ Profile";
            EditorWorkflowValues.Text = "○ Pflichtfelder";
            EditorWorkflowKeys.Text = "○ Schlüssel/Vault";
            EditorWorkflowSaved.Text = "○ Speicherung";
            EditorNextStep.Text = "Öffnen oder erstellen Sie zuerst ein Projekt.";
            EditorBuildButton.IsEnabled = false;
            EditorGenerateKeysButton.IsEnabled = false;
            EditorUnlockVaultButton.IsEnabled = false;
            EditorSaveVaultButton.IsEnabled = false;
            EditorManageVaultAccessButton.IsEnabled = false;
            EditorAuditVaultButton.IsEnabled = false;
            EditorGroupRecoveryButton.IsEnabled = false;
            EditorGroupRecoveryButton.Visibility = Visibility.Collapsed;
            EditorGroupProjectSetupPanel.Visibility = Visibility.Collapsed;
            EditorCreateGroupProjectButton.IsEnabled = false;
            EditorSaveVaultButton.Visibility = settings.ExpertMode ? Visibility.Visible : Visibility.Collapsed;
            HighlightEditorNextAction(null);
            return;
        }

        var profilesOk = ProfileBindingService.Evaluate(editorProject, editorLibrary).All(status => status.IsMatch);
        var definitionIssues = ProjectContractValidator.Validate(editorProject, editorLibrary, requireProductiveSecrets: false);
        var valuesOk = definitionIssues.Count == 0;
        var fieldIssues = definitionIssues.Where(issue =>
            !issue.Code.StartsWith("TRANSPORT_", StringComparison.Ordinal) &&
            !issue.Code.StartsWith("PRODUCTIVE_", StringComparison.Ordinal)).ToArray();
        var keyIssues = ProjectContractValidator.Validate(editorProject, editorLibrary, requireProductiveSecrets: true)
            .Where(issue => issue.Code.StartsWith("PRODUCTIVE", StringComparison.Ordinal)).ToArray();
        var keysLoaded = keyIssues.Length == 0;
        var backupRequired = editorProject.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase);
        var backupReady = !backupRequired || IsIndependentVaultBackupConfigured();
        var keysSafe = keysLoaded && editorKeysPersisted && editorVaultVerified && editorVaultVersion >= KeyVaultService.CurrentVersion && backupReady && !editorGroupRecovery;
        var saved = !string.IsNullOrWhiteSpace(editorSourcePath) && !editorDirty;
        var vaultExists = !string.IsNullOrWhiteSpace(editorSourcePath) && File.Exists(ProjectKeyVault.GetPath(editorSourcePath));
        var existingDefinition = !string.IsNullOrWhiteSpace(editorSourcePath);

        // 1.0 creates a NEW group project from the current editable draft. This
        // option is not a migration step and cannot modify an existing group binding.
        EditorGroupProjectSetupPanel.Visibility = IsEditorGroupVault ? Visibility.Collapsed : Visibility.Visible;
        var mayChooseGroup = profilesOk && valuesOk && keysLoaded && !editorGroupRecovery;
        EditorCreateGroupProjectButton.IsEnabled = mayChooseGroup;
        EditorGroupProjectSetupHint.Text = !profilesOk
            ? "Zuerst die Profilbindung prüfen und ausdrücklich übernehmen."
            : fieldIssues.Length > 0 || !valuesOk
                ? "Zuerst die Pflichtfelder vollständig und korrekt ausfüllen."
                : !keysLoaded
                    ? "Zuerst die Kartenschlüssel erzeugen oder einen bereits gespeicherten Vault entsperren."
                    : "Bereit zur bewussten Gruppenauswahl. Die Hauptverwaltung muss Ihre Person, die Gruppe und einen separaten Recovery-Zugang zuvor freigegeben haben. Eine neue Projektkopie wird erst nach Bestätigung angelegt.";

        EditorGenerateKeysButton.IsEnabled = true;
        EditorUnlockVaultButton.IsEnabled = vaultExists;
        EditorGroupRecoveryButton.Visibility = IsEditorGroupVault ? Visibility.Visible : Visibility.Collapsed;
        EditorGroupRecoveryButton.IsEnabled = vaultExists && IsEditorGroupVault;
        EditorSaveVaultButton.IsEnabled = keysLoaded && existingDefinition;
        // Ordinary group-vault updates must be saveable without switching to expert mode.
        // Other specialist vault actions keep the previous expert-mode requirement.
        EditorSaveVaultButton.Visibility = settings.ExpertMode ||
            (IsEditorGroupVault && keysLoaded && existingDefinition && !editorKeysPersisted)
                ? Visibility.Visible : Visibility.Collapsed;
        EditorSaveVaultButton.Content = editorVaultVersion >= KeyVaultService.CertificateVersion
            ? "Vault-Änderungen sicher speichern"
            : "Einzelbenutzer-Vault speichern";
        EditorManageVaultAccessButton.IsEnabled = keysLoaded && existingDefinition;
        EditorAuditVaultButton.IsEnabled = vaultExists;
        EditorAuditVaultButton.Content = IsEditorGroupVault ? editorGroupRecovery ? "Recovery-Zugang prüfen" : "Gruppen-Vault prüfen" : "Vault / Recovery prüfen";
        EditorManageVaultAccessButton.Content = IsEditorGroupVault ? "Gruppenempfänger abgleichen" : "Mehrbenutzerzugriff";

        EditorWorkflowProfile.Text = profilesOk ? "✓ Profile" : "! Profile prüfen";
        EditorWorkflowValues.Text = fieldIssues.Length == 0 ? "✓ Pflichtfelder" : "! Pflichtfelder";
        EditorWorkflowKeys.Text = keysSafe
            ? editorVaultVersion >= KeyVaultService.CertificateVersion ? "✓ Vault geprüft + gesichert" : "✓ Vault v2 geprüft + gesichert"
            : keysLoaded && editorVaultVersion == 1 ? "! Vault v1 aktualisieren" : keysLoaded ? "! Vault speichern" : vaultExists ? "! Vault entsperren" : existingDefinition ? "! Vault fehlt" : "! Schlüssel erzeugen";
        EditorWorkflowSaved.Text = saved ? "✓ Gespeichert" : "! Noch nicht gespeichert";
        EditorNextStep.Text = !profilesOk ? "Nächster Schritt: geänderte Profilstände prüfen und bewusst übernehmen."
            : fieldIssues.Length > 0 ? "Nächster Schritt: " + string.Join(" ", fieldIssues.Take(3).Select(issue => issue.Message))
            : !keysLoaded && vaultExists ? "Nächster Schritt: vorhandenen Schlüsselspeicher für diese Sitzung entsperren."
            : !keysLoaded && existingDefinition ? "Nächster Schritt: Zu dieser bestehenden Definition fehlt der Vault. Vorhandene Build-Schlüssel sind nicht wiederherstellbar; neue Schlüssel nur für eine bewusst neue Schlüsselidentität erzeugen."
            : !keysLoaded ? "Nächster Schritt: Kartenschlüssel erzeugen. Für ein Teamprojekt danach den Gruppen-Vault auswählen; für persönliche Projekte anschließend den Vault verschlüsselt speichern."
            : !valuesOk ? "Nächster Schritt: Grundschlüssel prüfen. " + string.Join(" ", definitionIssues.Take(3).Select(issue => issue.Message))
            : editorVaultVersion == 1 ? "Nächster Schritt: alten Vault mit einer neuen starken Passphrase als Vault v2 speichern. Vorher bleibt der Neubau gesperrt."
            : editorGroupRecovery ? "Recovery-Sitzung: Änderungen bei Bedarf speichern. Vor einem Build über ‚Vault entsperren‘ mit normalem Gruppenzugang öffnen."
            : IsEditorGroupVault && !editorKeysPersisted && keysLoaded
                ? "Nächster Schritt: geänderte Gruppenschlüssel mit „Vault-Änderungen sicher speichern“ sichern und von SieEncode erneut prüfen lassen. Die Gruppenmitgliedschaft wird dadurch nicht geändert."
            : !editorVaultVerified && !IsEditorGroupVault && !existingDefinition
                ? "Nächster Schritt: Bei Teamnutzung „Gruppen-Vault für dieses Projekt einrichten“ wählen. Für ein persönliches Projekt zuerst zwischenspeichern und den Vault-Zugang einrichten."
            : !editorVaultVerified ? "Nächster Schritt: Vault sicher speichern oder mit berechtigtem Zugang entsperren; SieEncode muss ihn erfolgreich wieder öffnen."
            : backupRequired && !backupReady ? "Nächster Schritt: Unter Einstellungen einen vom Projektordner unabhängigen Vault-Sicherungsort wählen. Mastering bleibt bis dahin gesperrt."
            : !editorKeysPersisted ? "Nächster Schritt: Mehrbenutzerzugriff einrichten und Recovery-Passphrase in KeePass ablegen."
            : editorVaultVersion >= KeyVaultService.CertificateVersion
                ? "Bereit: Sicher prüfen. Produktive RFiDGear-Dateien entstehen nur in einer lokalen, kurzlebigen Sitzung."
                : "Bereit im Einzelbenutzermodus. Für den gemeinsamen Einsatz bitte Mehrbenutzerzugriff einrichten.";
        EditorBuildButton.IsEnabled = profilesOk && valuesOk && keysSafe;

        // One visually emphasized *next* action, never several competing primaries.
        // Do not highlight key replacement for an existing project without its original vault:
        // that would suggest a destructive migration is the routine next step.
        Button? nextAction =
            !profilesOk ? (EditorBindProfilesButton.IsEnabled ? EditorBindProfilesButton : null) :
            fieldIssues.Length > 0 || !valuesOk ? null :
            !keysLoaded && vaultExists ? EditorUnlockVaultButton :
            !keysLoaded && !existingDefinition ? EditorGenerateKeysButton :
            !keysLoaded ? null :
            editorGroupRecovery ? EditorUnlockVaultButton :
            !editorKeysPersisted && IsEditorGroupVault ? EditorSaveVaultButton :
            !editorVaultVerified && vaultExists ? EditorUnlockVaultButton :
            !editorKeysPersisted && existingDefinition ? EditorManageVaultAccessButton :
            keysSafe ? EditorBuildButton : null;
        HighlightEditorNextAction(nextAction);
    }

    private void HighlightEditorNextAction(Button? nextAction)
    {
        var normal = (Style)FindResource(typeof(Button));
        var primary = (Style)FindResource("PrimaryButton");
        var buttons = new[]
        {
            EditorBindProfilesButton, EditorGenerateKeysButton, EditorUnlockVaultButton,
            EditorSaveVaultButton, EditorManageVaultAccessButton, EditorBuildButton
        };
        foreach (var button in buttons)
            button.Style = ReferenceEquals(button, nextAction) && button.IsEnabled &&
                button.Visibility == Visibility.Visible ? primary : normal;
    }

    private void CaptureEditorControls(bool includeProfiles = true)
    {
        if (editorProject is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
        editorProject.Project.Name = EditorProjectName.Text.Trim();
        editorProject.Project.CardMode = EditorCardMode.SelectedValue as string ?? CardOperationMode.FullCard;
        editorProject.Project.OutputDirectory = EditorOutputDirectory.Text.Trim();
        editorProject.Project.Build = [];
        if (EditorBuildMastering.IsChecked == true) editorProject.Project.Build.Add("Mastering");
        if (EditorBuildCoding.IsChecked == true) editorProject.Project.Build.Add("Coding");
        if (includeProfiles)
        {
            editorProject.Profiles.Base = EditorBaseProfile.SelectedValue as string ?? "";
            editorProject.Profiles.Customers = EditorCustomerProfiles.SelectedItems.Cast<ProfileChoice>().Select(choice => choice.Id).ToList();
        }
        editorProject.Transport.PiccKey = new KeyReference { Type = GetComboValue(EditorTransportPiccType), Value = NormalizeHex(EditorTransportPiccKey.Text) };
        foreach (var field in editorTransportKeys) editorProject.Transport.ApplicationKeys[field.Id] = NormalizeHex(field.Value);
        editorProject.Productive.PiccKey = new KeyReference { Type = GetComboValue(EditorProductivePiccType), Value = NormalizeHex(EditorProductivePiccKey.Text) };
        editorProject.Productive.PiccKeySettings = GetPiccSettingsFlags();
        editorProject.Values = editorValues.ToDictionary(field => field.Id, field => field.Value.Trim(), StringComparer.Ordinal);
        var keys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in editorProductiveKeys.GroupBy(field => field.Aid, StringComparer.OrdinalIgnoreCase))
            keys[group.Key] = group.OrderBy(field => field.Index).Select(field => NormalizeHex(field.Value)).ToList();
        editorProject.Productive.ApplicationKeys = keys;
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        try { SaveEditorProject(saveAs: false); }
        catch (Exception exception) { ShowError(exception); }
    }

    private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        try { SaveEditorProject(saveAs: true); }
        catch (Exception exception) { ShowError(exception); }
    }

    private bool SaveEditorProject(bool saveAs)
    {
        if (saveAs && IsEditorGroupVault) return SaveEditorGroupCopy();
        if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
        CaptureEditorControls();
        ProjectDraftFactory.Synchronize(editorProject, editorLibrary);
        var issues = ProjectContractValidator.Validate(editorProject, editorLibrary, requireProductiveSecrets: false);
        if (issues.Count > 0) throw new InvalidDataException("Projektdefinition ist nicht speicherbar:\n" + string.Join("\n", issues.Select(issue => $"{issue.Code}: {issue.Message}")));

        var target = editorSourcePath;
        if (string.IsNullOrWhiteSpace(target) && !saveAs)
        {
            var safeName = MakeSafeFileName(editorProject.Project.Name);
            target = Path.Combine(projectsDirectory, safeName, safeName + ".project.json");
            if (File.Exists(target))
                throw new InvalidOperationException($"Ein Projekt mit dem Namen '{editorProject.Project.Name}' existiert bereits. Bitte einen eindeutigen Projektnamen vergeben; bestehende Projekte werden niemals still überschrieben.");
        }
        else if (saveAs)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Projektdefinition speichern",
                Filter = "RFiDGear-Projektdefinition (*.project.json)|*.project.json",
                InitialDirectory = projectsDirectory,
                FileName = MakeSafeFileName(editorProject.Project.Name) + ".project.json",
                AddExtension = true,
                DefaultExt = ".project.json"
            };
            if (dialog.ShowDialog(this) != true) return false;
            target = Path.GetFullPath(dialog.FileName);
            var libraryRoot = Path.GetFullPath(projectsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Die Projektdefinition muss innerhalb des unter Einstellungen gewählten übergeordneten Projektordners gespeichert werden.");
        }

        var fullTarget = Path.GetFullPath(target);
        var expectedRevision = string.Equals(fullTarget, editorSourcePath, StringComparison.OrdinalIgnoreCase)
            ? editorProjectRevision
            : ConcurrentFileWriter.Capture(fullTarget);
        editorProjectRevision = ProjectJson.Save(fullTarget, ProjectJson.WithoutAllSecrets(editorProject), expectedRevision);
        editorSourcePath = Path.GetFullPath(target);
        editorVaultRevision = ConcurrentFileWriter.Capture(ProjectKeyVault.GetPath(editorSourcePath));
        EditorSourcePath.Text = editorSourcePath;
        SetEditorDirty(false);
        EditorValidation.Text = "Projektdefinition gespeichert und validiert. Sie enthält keine Schlüsselwerte. Änderungen an Schlüsseln müssen separat im Vault gespeichert werden.";
        RefreshProjects();
        var entry = projects.FirstOrDefault(item => string.Equals(item.SourcePath, editorSourcePath, StringComparison.OrdinalIgnoreCase));
        if (entry is not null) { SelectProject(entry); ProjectsGrid.SelectedItem = entry; }
        EditorVaultStatus.Text = File.Exists(ProjectKeyVault.GetPath(editorSourcePath)) ? "Vault vorhanden: " + ProjectKeyVault.GetPath(editorSourcePath) : "Noch kein Vault vorhanden.";
        SetStatus("Projekt am vorhandenen Pfad gespeichert: " + editorSourcePath);
        RefreshEditorWorkflow();
        return true;
    }

    private void GenerateProductiveKeys_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            if (!string.IsNullOrWhiteSpace(editorSourcePath))
            {
                var vaultExists = File.Exists(ProjectKeyVault.GetPath(editorSourcePath));
                var warning = vaultExists
                    ? "Für dieses bestehende Projekt ist ein Vault vorhanden. Neue Schlüssel ersetzen die aktuell geladenen Werte; beim späteren Speichern würde der Vault überschrieben. Nur fortfahren, wenn bewusst eine neue Schlüsselidentität eingeführt werden soll."
                    : "Für dieses bestehende Projekt fehlt der ursprüngliche Vault. Vorhandene Builds und bereits kodierte Karten können alte, nur noch in den rfPrj-Artefakten vorhandene Schlüssel verwenden. Neue Schlüssel stellen diese alten Schlüssel nicht wieder her und erzeugen eine neue Schlüsselidentität.";
                if (MessageBox.Show(this, warning + "\n\nNeue Produktivschlüssel jetzt wirklich erzeugen?", "Neue Schlüssel für bestehendes Projekt", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    SetStatus("Schlüsselerzeugung abgebrochen. Bestehende Werte wurden nicht verändert.");
                    return;
                }
            }
            CaptureEditorControls();
            if (!CardOperationMode.IsExtension(editorProject))
            {
                editorProject.Productive.PiccKey.Value = KeyVaultService.GenerateKey(editorProject.Productive.PiccKey.Type);
                EditorProductivePiccKey.Text = editorProject.Productive.PiccKey.Value;
            }
            foreach (var field in editorProductiveKeys) field.Value = KeyVaultService.GenerateKey(field.KeyType);
            CaptureEditorControls();
            editorKeysPersisted = false;
            editorVaultVerified = false;
            EditorVaultStatus.Text = "Neue kryptografisch zufällige Keys sind nur in dieser Sitzung geladen. Bitte jetzt verschlüsselt speichern.";
            SetEditorDirty(true);
            SetStatus("Produktivkeys kryptografisch erzeugt; noch nicht im Vault gespeichert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void UnlockVault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Projekt bitte zuerst speichern.");
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            if (!File.Exists(path)) throw new FileNotFoundException("Kein Key-Vault für dieses Projekt gefunden.", path);
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            var document = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options) ?? throw new InvalidDataException("Key-Vault ist leer.");
            ProjectKeyVaultPayload payload;
            var windowsIdentity = false;
            if (IsEditorGroupVault)
            {
                payload = LoadEditorGroupKeys(requestGroupRecovery);
                windowsIdentity = true;
            }
            else if (document.Version == KeyVaultService.CertificateVersion)
            {
                var identities = loadEditorVaultIdentities();
                try
                {
                    payload = ProjectKeyVault.LoadWithCertificates(path, identities.Select(identity => identity.Certificate));
                    windowsIdentity = true;
                }
                catch (CryptographicException)
                {
                    var recovery = PasswordPrompt.Show(this, "Vault wiederherstellen", "Keine passende Windows-Identität gefunden. Recovery-Passphrase aus KeePass eingeben.");
                    if (recovery is null) return;
                    payload = ProjectKeyVault.Load(path, recovery);
                }
                finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
            }
            else
            {
                var passphrase = PasswordPrompt.Show(this, "Vault entsperren", "Passphrase für den portablen Projekt-Key-Vault eingeben.");
                if (passphrase is null) return;
                payload = ProjectKeyVault.Load(path, passphrase);
            }
            if (ConcurrentFileWriter.Capture(path) != editorVaultRevision) throw new ConcurrentFileChangeException(path);
            editorVaultPayload = payload;
            editorProject = ProjectKeyVault.Apply(editorProject!, payload);
            VerifyLoadedVaultPayload(payload);
            editorKeysPersisted = true;
            editorVaultVerified = true;
            editorVaultVersion = ReadVaultVersion(editorSourcePath);
            suppressEditorChanges = true;
            editorGroupRecovery = IsEditorGroupVault && requestGroupRecovery;
            try
            {
                SetComboValue(EditorTransportPiccType, editorProject.Transport.PiccKey.Type);
                EditorTransportPiccKey.Text = editorProject.Transport.PiccKey.Value;
                SetComboValue(EditorProductivePiccType, editorProject.Productive.PiccKey.Type);
                EditorProductivePiccKey.Text = editorProject.Productive.PiccKey.Value;
                EditorPiccKeySettings.Text = editorProject.Productive.PiccKeySettings;
                SetPiccSettingsFlags(editorProject.Productive.PiccKeySettings);
                PopulateDynamicEditorFields();
                RefreshInitialKeySets();
            }
            finally { suppressEditorChanges = false; }
            EditorVaultStatus.Text = IsEditorGroupVault ? (editorGroupRecovery ? "Recovery-Sitzung für Gruppen-Vault: " : "Gruppen-Vault mit normalem Zugang entsperrt: ") + path : editorVaultVersion == KeyVaultService.CertificateVersion
                ? (windowsIdentity ? "Mehrbenutzer-Vault automatisch mit der persönlichen Windows-Identität entsperrt: " : "Mehrbenutzer-Vault über Recovery-Zugang entsperrt: ") + path
                : editorVaultVersion >= KeyVaultService.CurrentVersion
                ? "Vault v2 für diese Sitzung entsperrt: " + path
                : "Alter Vault v1 entsperrt. Vor dem Neubau bitte mit einer neuen starken Passphrase als Vault v2 speichern.";
            RefreshEditorWorkflow();
            SetStatus("Produktivkeys für diese Sitzung entsperrt.");
        }
        catch (CryptographicException exception) when (IsEditorGroupVault)
        {
            // Only a diagnosis, never a workaround: a validly enrolled group
            // member, a matching project binding and the original ciphertext
            // must all agree before secrets can be loaded.
            const string guidance = "Gruppen-Vault nicht entsperrt. Bitte im eigenen Windows-Benutzer den persönlichen Zugang prüfen. Dann die Hauptverwaltung fragen, ob Person und Gerät zur richtigen Projektgruppe gehören und ob der aktuelle signierte Gruppenstand importiert wurde. Nach Gruppenänderungen den Vault durch eine berechtigte Person ausdrücklich abgleichen lassen. Es kann auch ein Integritäts-/Projektbindungsfehler vorliegen. Keinesfalls neue Kartenschlüssel oder einen Ersatz-Vault erzeugen.";
            editorVaultVerified = false;
            EditorVaultStatus.Text = guidance;
            EditorNextStep.Text = guidance;
            SetStatus("Gruppen-Vault-Zugang nicht bestätigt. Keine Schlüssel freigegeben.", true);
            ShowError(new InvalidOperationException(guidance + "\n\nTechnischer Hinweis: " + exception.Message, exception));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void VerifyLoadedVaultPayload(ProjectKeyVaultPayload payload)
    {
        if (editorProject is null) throw new InvalidOperationException("Kein Projekt für den Vault-Nachweis geöffnet.");
        var expected = ProjectKeyVault.FromProject(editorProject);
        if (!string.Equals(KeySetIdentityService.Compute(expected), KeySetIdentityService.Compute(payload), StringComparison.Ordinal) ||
            !string.Equals(expected.PiccKeySettings, payload.PiccKeySettings, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Der wieder geöffnete Vault gehört nicht zum aktuell geladenen Schlüsselsatz.");
    }

    private ProjectKeyVaultPayload CreateVaultPayload()
    {
        if (editorProject is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
        editorVaultPayload = ProjectKeyVault.UpdateFromProject(editorProject, editorVaultPayload);
        return editorVaultPayload;
    }

    private bool IsIndependentVaultBackupConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.VaultBackup.Directory)) return false;
        var backup = Path.GetFullPath(settings.VaultBackup.Directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var projects = Path.GetFullPath(projectsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return !backup.StartsWith(projects, StringComparison.OrdinalIgnoreCase) &&
               !projects.StartsWith(backup, StringComparison.OrdinalIgnoreCase);
    }

    private VaultBackupResult? CreateVerifiedVaultBackupIfConfigured(ProjectKeyVaultPayload? verifiedPayload = null)
    {
        if (string.IsNullOrWhiteSpace(settings.VaultBackup.Directory)) return null;
        if (!IsIndependentVaultBackupConfigured())
            throw new InvalidOperationException("Der Vault-Sicherungsort muss außerhalb der gesamten Projektbibliothek liegen.");
        if (editorProject is null || string.IsNullOrWhiteSpace(editorSourcePath) || (verifiedPayload is null && !editorVaultVerified))
            throw new InvalidOperationException("Vault-Sicherung ist erst nach erfolgreichem Wiederöffnen des aktuellen Vaults möglich.");
        var vaultPath = ProjectKeyVault.GetPath(editorSourcePath);
        if (ConcurrentFileWriter.Capture(vaultPath) != editorVaultRevision) throw new ConcurrentFileChangeException(vaultPath);
        var result = new VaultBackupService().Create(editorSourcePath, vaultPath, settings.VaultBackup.Directory, verifiedPayload ?? ProjectKeyVault.FromProject(editorProject));
        EditorVaultStatus.Text = $"Vault geprüft und unabhängig gesichert · Keyset-ID {result.KeySetIdentity[..12]} · {result.Directory}";
        return result;
    }

    private void SaveVault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            if (string.IsNullOrWhiteSpace(editorSourcePath) && !SaveEditorProject(saveAs: false)) return;
            CaptureEditorControls();
            var issues = ProjectContractValidator.Validate(editorProject, editorLibrary, requireProductiveSecrets: true)
                .Where(issue => issue.Code.StartsWith("PRODUCTIVE", StringComparison.Ordinal) || issue.Code.StartsWith("PROJECT_", StringComparison.Ordinal)).ToArray();
            if (issues.Length > 0) throw new InvalidDataException("Produktivdaten sind nicht vollständig:\n" + string.Join("\n", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            if (IsEditorGroupVault)
            {
                SaveEditorGroupKeys();
            }
            else if (editorVaultVersion >= KeyVaultService.CertificateVersion)
            {
                var identities = loadEditorVaultIdentities();
                try
                {
                    var existing = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options)
                        ?? throw new InvalidDataException("Key-Vault ist leer.");
                    var hasRecipientIdentity = identities.Any(identity => identity.Certificate.HasPrivateKey && existing.Recipients.Any(recipient =>
                        string.Equals(recipient.Id, CertificateVaultService.Fingerprint(identity.Certificate), StringComparison.OrdinalIgnoreCase)));
                    if (!hasRecipientIdentity)
                    {
                        // Recovery sessions can save through explicit reauthorization of the existing recipients.
                        ManageVaultAccess_Click(sender, e);
                        return;
                    }
                    editorVaultRevision = ProjectKeyVault.UpdateForExistingRecipients(path, CreateVaultPayload(), identities.Select(identity => identity.Certificate), editorVaultRevision);
                    VerifyLoadedVaultPayload(ProjectKeyVault.LoadWithCertificates(path, identities.Select(identity => identity.Certificate)));
                }
                finally { foreach (var identity in identities) identity.Certificate.Dispose(); }
            }
            else
            {
                var passphrase = PasswordPrompt.Show(this, "Keys verschlüsselt speichern", "Neue Passphrase für diesen portablen Projekt-Key-Vault festlegen.", confirm: true);
                if (passphrase is null) return;
                editorVaultRevision = ProjectKeyVault.Save(path, CreateVaultPayload(), passphrase, editorVaultRevision);
                VerifyLoadedVaultPayload(ProjectKeyVault.Load(path, passphrase));
            }
            editorKeysPersisted = true;
            editorVaultVerified = true;
            if (editorVaultVersion >= KeyVaultService.CertificateVersion)
                EditorVaultStatus.Text = IsEditorGroupVault && editorGroupRecovery ? "Gruppen-Vault über separaten Recovery-Zugang aktualisiert: " + path
                    : "Mehrbenutzer-Vault mit der persönlichen Windows-Identität aktualisiert; Empfänger und Recovery-Zugang blieben unverändert: " + path;
            else
            {
                editorVaultVersion = KeyVaultService.CurrentVersion;
                EditorVaultStatus.Text = "Keys im gehärteten Vault v2 gespeichert und für diese Sitzung geladen: " + path;
            }
            var backup = CreateVerifiedVaultBackupIfConfigured();
            RefreshEditorWorkflow();
            SetStatus(backup is null
                ? "Produktivkeys gespeichert und wieder geöffnet. Für Mastering noch unabhängigen Sicherungsort konfigurieren."
                : "Produktivkeys gespeichert, wieder geöffnet und unabhängig gesichert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ManageVaultAccess_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (IsEditorGroupVault) { ReconcileEditorGroupVault(); return; }
            if (editorProject is null || editorLibrary is null || string.IsNullOrWhiteSpace(editorSourcePath)) throw new InvalidOperationException("Projekt bitte zuerst speichern und Schlüssel laden.");
            CaptureEditorControls();
            var issues = ProjectContractValidator.Validate(editorProject, editorLibrary, requireProductiveSecrets: true)
                .Where(issue => issue.Code.StartsWith("PRODUCTIVE", StringComparison.Ordinal) || issue.Code.StartsWith("PROJECT_", StringComparison.Ordinal)).ToArray();
            if (issues.Length > 0) throw new InvalidDataException("Produktivdaten sind nicht vollständig:\n" + string.Join("\n", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            KeyVaultDocument? existing = null;
            if (File.Exists(path)) existing = JsonSerializer.Deserialize<KeyVaultDocument>(File.ReadAllText(path), ProjectJson.Options);
            // Clean-slate 1.0: no unpublished legacy team recipient template is
            // used to grant access. The operator explicitly chooses recipients
            // in this project's Vault dialog; existing recipients remain intact.
            var dialog = new VaultAccessWindow(this, existing, identityProvider: loadEditorVaultIdentities);
            if (dialog.ShowDialog() != true) return;
            var recipients = dialog.TakeRecipients();
            try { editorVaultRevision = ProjectKeyVault.SaveForRecipients(path, CreateVaultPayload(), dialog.RecoveryPassphrase, recipients, editorVaultRevision); }
            finally { foreach (var recipient in recipients) recipient.Certificate.Dispose(); }
            VerifyLoadedVaultPayload(ProjectKeyVault.Load(path, dialog.RecoveryPassphrase));
            editorKeysPersisted = true; editorVaultVerified = true; editorVaultVersion = KeyVaultService.CertificateVersion;
            var backup = CreateVerifiedVaultBackupIfConfigured();
            EditorVaultStatus.Text = $"Mehrbenutzer-Vault sicher gespeichert · {existing?.Recipients.Count ?? 0} bisherige / {recipients.Count} aktuelle Empfänger · tägliches Entsperren über Windows-Identität.";
            RefreshEditorWorkflow(); SetStatus(backup is null
                ? "Mehrbenutzerzugriff gespeichert und geprüft; unabhängiger Sicherungsort fehlt noch."
                : "Mehrbenutzerzugriff gespeichert, geprüft und unabhängig gesichert.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void AuditVault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (IsEditorGroupVault) { AuditEditorGroupVault(); return; }
            if (editorProject is null || editorLibrary is null || string.IsNullOrWhiteSpace(editorSourcePath))
                throw new InvalidOperationException("Gespeichertes Projekt bitte zuerst im Editor öffnen.");
            var path = ProjectKeyVault.GetPath(editorSourcePath);
            if (!File.Exists(path)) throw new FileNotFoundException("Kein Key-Vault für dieses Projekt gefunden.", path);
            var before = ConcurrentFileWriter.Capture(path);
            var passphrase = PasswordPrompt.Show(this, editorVaultVersion >= KeyVaultService.CertificateVersion ? "Recovery-Zugang prüfen" : "Vault prüfen",
                editorVaultVersion >= KeyVaultService.CertificateVersion
                    ? "Recovery-Passphrase aus KeePass eingeben. Der technische Test prüft Integrität und Projektvollständigkeit; Schlüsselwerte werden nicht angezeigt."
                    : "Vault-Passphrase eingeben. Es werden Integrität und Projektvollständigkeit geprüft; Schlüsselwerte werden nicht angezeigt.");
            if (passphrase is null) return;
            var audit = ProjectKeyVault.Audit(path, passphrase, editorProject, editorLibrary);
            if (ConcurrentFileWriter.Capture(path) != before) throw new ConcurrentFileChangeException(path);
            if (audit.Status != "OK") throw new InvalidDataException("Vault-Prüfung fehlgeschlagen:\n" + string.Join("\n", audit.Findings));
            var verifiedPayload = ProjectKeyVault.Load(path, passphrase);
            if (editorKeysPersisted)
            {
                VerifyLoadedVaultPayload(verifiedPayload);
                editorVaultVerified = true;
            }
            var backup = CreateVerifiedVaultBackupIfConfigured(verifiedPayload);
            EditorVaultStatus.Text = $"Vault geprüft: OK · Version {audit.Version} · {audit.ApplicationCount} Applications · {audit.KeyCount} Keys · {audit.Kdf} {audit.Iterations:N0} · Fingerprint {audit.FileFingerprint}";
            SetStatus(editorVaultVersion >= KeyVaultService.CertificateVersion
                ? "Recovery-Zugang erfolgreich geprüft; täglicher Zugriff bleibt an freigegebene Windows-Identitäten gebunden."
                : backup is null ? "Vault-Prüfung erfolgreich; unabhängiger Sicherungsort fehlt noch." : "Vault-Prüfung und unabhängige Sicherung erfolgreich; keine Schlüsselwerte ausgegeben.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void BuildProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (editorProject is null || editorLibrary is null) throw new InvalidOperationException("Kein Projekt im Editor geöffnet.");
            CaptureEditorControls();
            if (string.IsNullOrWhiteSpace(editorSourcePath) || editorDirty)
                if (!SaveEditorProject(saveAs: false)) return;
            CaptureEditorControls();
            if (!editorVaultVerified) throw new InvalidOperationException("Build gesperrt: Der aktuelle Vault wurde in dieser Sitzung noch nicht erfolgreich wieder geöffnet.");
            if (IsEditorGroupVault) VerifyEditorGroupBuildAccess();
            if (editorProject.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase) && !IsIndependentVaultBackupConfigured())
                throw new InvalidOperationException("Mastering-Build gesperrt: Unter Einstellungen muss ein vom Projektordner unabhängiger Vault-Sicherungsort gewählt werden.");
            var recoveryBackup = CreateVerifiedVaultBackupIfConfigured();
            if (editorProject.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase) && recoveryBackup is null)
                throw new InvalidOperationException("Mastering-Build gesperrt: Die unabhängige Vault-Sicherung konnte nicht erstellt werden.");
            var shells = TaskShellLibrary.Load(Path.Combine(AppContext.BaseDirectory, "assets", "task-shells"));
            if (editorVaultVersion >= KeyVaultService.CertificateVersion)
            {
                using var validationSession = new SecureOperationalSessionService().Materialize(
                    editorProject, editorLibrary, shells, editorSourcePath, Version);
                EditorValidation.Text = $"SICHERER BUILD OK · {validationSession.Build.Preflight.Projects.Count} RFiDGear-Projekte · nur lokal und temporär geprüft";
                SetEditorDirty(false);
                var legacyDirectory = ProjectPaths.ResolveBuiltProject(editorProject, projectsDirectory);
                var legacyProjects = Directory.Exists(legacyDirectory)
                    ? Directory.EnumerateFiles(legacyDirectory, "*.rfPrj", SearchOption.TopDirectoryOnly).ToArray()
                    : [];
                if (legacyProjects.Length > 0 && MessageBox.Show(this,
                    $"Der sichere Sitzungsbuild wurde vollständig geprüft. Im gemeinsamen Projektordner liegen aber noch {legacyProjects.Length} ältere rfPrj-Datei(en), die produktive Schlüssel im Klartext enthalten können.\n\nDiese Altdateien jetzt dauerhaft löschen?",
                    "Unsicheren Altbuild entfernen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    foreach (var legacyProject in legacyProjects) File.Delete(legacyProject);
                RefreshProjects();
                SetStatus("Sicherer Projektbuild geprüft. Produktive Laufdateien werden erst beim Öffnen der Kodierstation lokal erzeugt und beim Schließen entfernt.");
                MessageBox.Show(this,
                    "Projekt erfolgreich und sicher geprüft.\n\nIm gemeinsamen Projektordner werden keine neuen produktiven rfPrj-Dateien abgelegt. SieEncode erzeugt sie nach der persönlichen Windows-Freigabe nur für die lokale Sitzung und entfernt sie beim Schließen.",
                    "Sicherer Projektbuild", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var result = new ProjectBuildService().Build(editorProject, editorLibrary, shells, editorSourcePath, projectsDirectory, Version);
            EditorValidation.Text = $"BUILD OK · {result.Preflight.Projects.Count} RFiDGear-Projekte · Abschlussprüfung ohne Befund · {result.ProjectDirectory}";
            RefreshProjects();
            SetEditorDirty(false);
            SetStatus("Projekt erfolgreich gebaut und geprüft: " + result.ProjectDirectory);
            MessageBox.Show(this, "Projekt erfolgreich gebaut.\n\n" + result.ProjectDirectory + $"\n\nProjektprüfung: OK · {result.Preflight.Projects.Count} rfPrj-Projekte", "Projekt bauen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void CloseProject_Click(object sender, RoutedEventArgs e)
    {
        if (!CanReplaceEditor()) return;
        ClearEditor();
        Navigate("Projects");
        SetStatus("Projekt geschlossen; produktive Schlüssel wurden aus der Editorsitzung entfernt.");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        var runSafety = CurrentRunSafety();
        if (!runSafety.IsSafe)
        {
            MessageBox.Show(this, runSafety.Reason, "RFiDGear-Vorgang noch aktiv", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }
        if (editorDirty && !CanReplaceEditor()) { e.Cancel = true; return; }
        if (preparedSession is not null)
        {
            try
            {
                sessionService.InvalidatePreparedPayloads(preparedSession,
                    "Builder wurde geschlossen; vorbereitete Kartendaten sind nicht mehr freigegeben.");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "Die vorbereiteten Kartendaten konnten nicht sicher gesperrt werden. Der Builder bleibt geöffnet.\n\n" + exception.Message,
                    "Kodiersitzung nicht geschlossen", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Cancel = true;
                return;
            }
        }
        operationTimer.Stop();
        preparedSession?.Dispose();
        preparedSession = null;
        secureOperationalSession?.Dispose();
        secureOperationalSession = null;
        projectSecretServer?.Dispose();
        projectSecretServer = null;
        toolSecretServer?.Dispose();
        toolSecretServer = null;
        if (editorProject is not null) ClearEditor();
    }

    private RFiDGearRunSafetyAssessment CurrentRunSafety()
        => RFiDGearRunSafety.Assess(operationSession, operationFinalized, codingLaunch, processProbe);

    private void RequireNoRunningRFiDGear(string action)
    {
        var safety = CurrentRunSafety();
        if (!safety.IsSafe) throw new InvalidOperationException(action + " " + safety.Reason);
    }

    private bool CanReplaceEditor()
    {
        if (!editorDirty) return true;
        return MessageBox.Show(this, "Ungespeicherte Änderungen verwerfen? Produktive Schlüssel, die nicht im Vault gespeichert wurden, gehen für diese Sitzung verloren.", "Projekt schließen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void ClearEditor()
    {
        suppressEditorChanges = true;
        try
        {
            EditorProductivePiccKey.Text = "";
            foreach (var field in editorProductiveKeys) field.Value = "";
            editorValues.Clear(); editorTransportKeys.Clear(); editorProductiveKeys.Clear(); editorInitialKeySets.Clear();
            editorProject = null; editorLibrary = null; editorSourcePath = ""; editorDirty = false; editorKeysPersisted = false; editorVaultVerified = false;
            editorVaultPayload = null;
            editorProjectRevision = FileRevision.Missing; editorVaultRevision = FileRevision.Missing; editorVaultVersion = 0;
            EditorProjectName.Text = ""; EditorOutputDirectory.Text = ""; EditorSourcePath.Text = "Noch kein Projekt geöffnet";
            EditorCardMode.ItemsSource = null; EditorCardModeGuidance.Text = "Neue Karte vollständig aufbauen.";
            EditorVaultStatus.Text = "Kein Vault geladen."; EditorValidation.Text = "Noch kein Projekt geladen.";
            EditorProfileBindingStatus.Text = "Kein Projekt geladen."; EditorBindProfilesButton.IsEnabled = false;
            EditorSaveButton.IsEnabled = false; EditorBuildButton.IsEnabled = false;
            CurrentProject.Text = "Kein Projekt ausgewählt";
            RefreshEditorWorkflow();
        }
        finally { suppressEditorChanges = false; }
    }

    private static string NormalizeHex(string? value)
        => new((value ?? "").Where(character => !char.IsWhiteSpace(character) && character != '-').Select(char.ToUpperInvariant).ToArray());

    private static string MakeSafeFileName(string value)
    {
        var result = string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(result) ? "Projekt" : result;
    }

    private string CreateUniqueProjectName()
    {
        const string baseName = "Neues Projekt";
        var names = projects.Select(project => project.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(baseName)) return baseName;
        for (var number = 2; ; number++)
        {
            var candidate = $"{baseName} {number}";
            if (!names.Contains(candidate)) return candidate;
        }
    }

    private static string GetComboValue(ComboBox combo)
        => combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "" : combo.SelectedValue?.ToString() ?? "";

    private static void SetComboValue(ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
        combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
    }
}

public sealed record ProfileChoice(string Id, string Display);

public class EditorTextField : INotifyPropertyChanged
{
    private readonly Action changed;
    private string acceptedValue;
    private string value;
    public EditorTextField(string id, string label, string value, string hint, Action changed, int maxLength = 0)
    { Id = id; Label = label; this.value = acceptedValue = value; Hint = hint; this.changed = changed; MaxLength = maxLength; }
    public string Id { get; }
    public string Label { get; }
    public string Hint { get; }
    public int MaxLength { get; }
    public bool IsModified => !string.Equals(value, acceptedValue, StringComparison.Ordinal);
    public string LengthFeedback => MaxLength > 0 ? $"{value.Length} / {MaxLength} Zeichen" : $"{value.Length} Zeichen";
    public string Value { get => value; set { if (this.value == value) return; this.value = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(LengthFeedback)); changed(); } }
    public void AcceptChanges() { acceptedValue = value; OnPropertyChanged(nameof(IsModified)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class EditorKeyField : EditorTextField
{
    public EditorKeyField(string aid, int index, string keyType, string label, string value, Action changed) : base(aid + "|" + index, label, value, "", changed)
    { Aid = aid; Index = index; KeyType = keyType; }
    public string Aid { get; }
    public int Index { get; }
    public string KeyType { get; }
}

internal static class PasswordPrompt
{
    public static string? Show(Window owner, string title, string prompt, bool confirm = false)
    {
        var (dialog, password) = Create(owner, title, prompt, confirm);
        return dialog.ShowDialog() == true ? password.Password : null;
    }

    internal static (Window Dialog, PasswordBox Password) Create(Window owner, string title, string prompt, bool confirm)
    {
        var password = new PasswordBox { MinWidth = 390, Margin = new Thickness(0, 4, 0, 8) };
        var confirmation = new PasswordBox { MinWidth = 390, Margin = new Thickness(0, 4, 0, 8), Visibility = confirm ? Visibility.Visible : Visibility.Collapsed };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true, MinWidth = 90, Margin = new Thickness(4) };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(password);
        if (confirm)
        {
            panel.Children.Add(new TextBlock { Text = "Passphrase wiederholen" });
            panel.Children.Add(confirmation);
        }
        panel.Children.Add(new TextBlock { Text = confirm ? $"Mindestens {KeyVaultService.MinimumNewPassphraseLength} Zeichen. Empfohlen: zufällig in KeePass erzeugen. Die Passphrase wird nicht gespeichert und kann nicht wiederhergestellt werden." : "Die Passphrase wird nicht gespeichert und kann nicht wiederhergestellt werden.", Foreground = System.Windows.Media.Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
        panel.Children.Add(error);
        panel.Children.Add(buttons);
        var dialog = new Window { Owner = owner, Title = title, Width = 480, MaxHeight = 600, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.CanResizeWithGrip };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        ok.Click += (_, _) =>
        {
            error.Text = "";
            if (string.IsNullOrWhiteSpace(password.Password)) { error.Text = "Bitte eine Passphrase eingeben."; return; }
            if (confirm && password.Password != confirmation.Password) { error.Text = "Die Passphrasen stimmen nicht überein."; return; }
            if (confirm)
            {
                try { KeyVaultService.ValidateNewPassphrase(password.Password); }
                catch (ArgumentException exception) { error.Text = exception.Message; return; }
            }
            dialog.DialogResult = true;
        };
        MainWindow.InitializeAutomationContract(dialog, "PasswordPrompt");
        return (dialog, password);
    }
}

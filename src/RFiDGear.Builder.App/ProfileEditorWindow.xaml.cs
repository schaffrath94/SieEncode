using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class ProfileEditorWindow : Window
{
    private readonly ProfileEditingService editing = new();
    private readonly ProfileStore store;
    private readonly ProfileDefinition profile;
    private readonly bool isNew;
    private VariableDefinition? selectedVariable;
    private ApplicationDefinition? selectedApplication;
    private FileDefinition? selectedFile;
    private RecipeStep? selectedRecipe;
    private bool loading;
    private bool dirty;
    private bool saved;
    private FileRevision sourceRevision;

    public ProfileEditorWindow(ProfileDefinition source, ProfileStore store, bool isNew)
    {
        InitializeComponent();
        var isBase = string.Equals(source.Profile.Kind, "base", StringComparison.Ordinal);
        Title = $"{(isBase ? "Grundprofil" : "Zusatzprofil")} bearbeiten · SieEncode Studio {AppIdentity.DisplayVersion}";
        EditorHeading.Text = isBase ? "Grundprofil bearbeiten" : "Zusatzprofil bearbeiten";
        MainWindow.InitializeAutomationContract(this, "Window.ProfileEditor");
        this.store = store;
        this.isNew = isNew;
        profile = editing.Clone(source);
        sourceRevision = isNew ? FileRevision.Missing : ConcurrentFileWriter.Capture(isBase ? store.GetBasePath(source.Profile.Id) : store.GetCustomerPath(source.Profile.Id));
        LoadProfile();
        ProfileId.IsReadOnly = !isNew;
        ProfileId.ToolTip = isNew ? "Dauerhafte, eindeutige Profil-ID" : "Die ID bestehender Profile bleibt stabil, damit Projekte kompatibel bleiben.";
    }

    public string SavedPath { get; private set; } = "";

    public void RunSelfTest()
    {
        if (!Title.Contains(AppIdentity.DisplayVersion, StringComparison.Ordinal)) throw new InvalidOperationException("Profil-Editor zeigt die Studio-Version nicht an.");
        MainWindow.VerifyAutomationContract(this);
        if (VariablesGrid is null || ApplicationsGrid is null || FilesGrid is null || RecipeGrid is null || ProfileId is null || ValidationText is null || CreationSettingsPanel is null || FileSettingsPreview is null || FileSettingsSequence is null)
            throw new InvalidOperationException("Profil-Editor-Bedienelemente sind unvollständig.");
        if (profile.Profile.Kind is not ("customer" or "base") || ProfileId.Text != profile.Profile.Id) throw new InvalidOperationException("Profil wurde nicht korrekt in den Editor geladen.");
        if (ProfileId.IsReadOnly == isNew) throw new InvalidOperationException("Profil-ID besitzt nicht den zum Bearbeitungsmodus passenden Schutz.");
    }

    private void LoadProfile()
    {
        loading = true;
        ProfileId.Text = profile.Profile.Id;
        ProfileCode.Text = profile.Profile.Code;
        ProfileName.Text = profile.Profile.Name;
        ProfileVersion.Text = profile.Profile.Version;
        ProfileDescription.Text = profile.Profile.Description;
        RefreshVariables();
        RefreshApplications();
        var historyCount = store.ListHistory(profile.Profile.Id).Count;
        ProfileHistoryText.Text = isNew
            ? "Neues Profil · beim ersten Speichern beginnt die versionsunabhängige Historie."
            : $"Aktueller Stand {profile.Profile.Version} · {historyCount} archivierte Vorgängerversion(en). Fachliche Änderungen benötigen eine neue Versionsnummer.";
        loading = false;
        dirty = false;
    }

    private void RefreshVariables(VariableDefinition? select = null)
    {
        VariablesGrid.ItemsSource = null;
        VariablesGrid.ItemsSource = profile.Variables;
        RecipeSource.ItemsSource = profile.Variables;
        if (select is not null) VariablesGrid.SelectedItem = select;
    }

    private void RefreshApplications(ApplicationDefinition? select = null)
    {
        ApplicationsGrid.ItemsSource = null;
        ApplicationsGrid.ItemsSource = profile.Applications;
        if (select is not null) ApplicationsGrid.SelectedItem = select;
        else if (profile.Applications.Count > 0 && selectedApplication is null) ApplicationsGrid.SelectedIndex = 0;
        RefreshFiles();
    }

    private void RefreshFiles(FileDefinition? select = null)
    {
        FilesGrid.ItemsSource = null;
        FilesGrid.ItemsSource = selectedApplication?.Files;
        if (select is not null) FilesGrid.SelectedItem = select;
        else if (selectedApplication?.Files.Count > 0 && selectedFile is null) FilesGrid.SelectedIndex = 0;
        RefreshRecipes();
    }

    private void RefreshRecipes(RecipeStep? select = null)
    {
        var recipe = selectedFile?.Coding?.Recipe ?? [];
        RecipeGrid.ItemsSource = recipe.Select((step, index) => new RecipeRow(index + 1, step)).ToArray();
        RecipeContext.Text = selectedApplication is null || selectedFile is null
            ? "Zuerst Application und Datei im vorherigen Reiter auswählen."
            : $"AID {selectedApplication.Id} · Datei {selectedFile.Id} ({selectedFile.Name}) · Ausgabe: {selectedFile.Coding?.Output ?? "–"}";
        RecipeBytePreview.Text = BuildRecipePreview();
        if (select is not null) RecipeGrid.SelectedItem = RecipeGrid.Items.Cast<RecipeRow>().FirstOrDefault(row => ReferenceEquals(row.Step, select));
    }

    private void Dirty_TextChanged(object sender, TextChangedEventArgs e) { if (!loading) dirty = true; }

    private void VariableSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedVariable = VariablesGrid.SelectedItem as VariableDefinition;
        if (selectedVariable is null) return;
        VariableId.Text = selectedVariable.Id;
        VariableLabel.Text = selectedVariable.Label;
        SetCombo(VariableScope, selectedVariable.Scope);
        SetCombo(VariableType, selectedVariable.Type);
        VariableLength.Text = Text(selectedVariable.Length);
        VariableAllowed.Text = selectedVariable.AllowedLengths is null ? "" : string.Join(",", selectedVariable.AllowedLengths);
        VariableMin.Text = Text(selectedVariable.MinLength);
        VariableMax.Text = Text(selectedVariable.MaxLength);
        VariableDefault.Text = selectedVariable.Default ?? "";
        VariableRequired.IsChecked = selectedVariable.Required;
    }

    private void NewVariable_Click(object sender, RoutedEventArgs e)
    {
        var suffix = 1;
        while (profile.Variables.Any(variable => variable.Id == "Variable" + suffix)) suffix++;
        var variable = new VariableDefinition { Id = "Variable" + suffix, Label = "Neue Variable", Scope = "project", Type = "digits", Length = 1 };
        profile.Variables.Add(variable); dirty = true; RefreshVariables(variable);
    }

    private void ApplyVariable_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedVariable is null) throw new InvalidOperationException("Keine Variable ausgewählt.");
            editing.RenameVariable(profile, selectedVariable.Id, VariableId.Text);
            selectedVariable.Label = Required(VariableLabel.Text, "Bezeichnung");
            selectedVariable.Scope = Combo(VariableScope);
            selectedVariable.Type = Combo(VariableType);
            selectedVariable.Length = OptionalInt(VariableLength.Text, "Exakte Länge");
            selectedVariable.AllowedLengths = ParseIntList(VariableAllowed.Text);
            selectedVariable.MinLength = OptionalInt(VariableMin.Text, "Minimale Länge");
            selectedVariable.MaxLength = OptionalInt(VariableMax.Text, "Maximale Länge");
            selectedVariable.Default = BlankToNull(VariableDefault.Text);
            selectedVariable.Required = VariableRequired.IsChecked == true;
            dirty = true; RefreshVariables(selectedVariable); RefreshRecipes(); ValidateAndShow(false);
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void DeleteVariable_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedVariable is null) throw new InvalidOperationException("Keine Variable ausgewählt.");
            editing.DeleteVariable(profile, selectedVariable.Id); selectedVariable = null; dirty = true; RefreshVariables(); RefreshRecipes();
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void ApplicationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedApplication = ApplicationsGrid.SelectedItem as ApplicationDefinition;
        selectedFile = null;
        if (selectedApplication is not null)
        {
            ApplicationId.Text = selectedApplication.Id; ApplicationName.Text = selectedApplication.Name;
            SetCombo(ApplicationKeyType, selectedApplication.KeyType); ApplicationKeyCount.Text = selectedApplication.KeyCount.ToString(CultureInfo.InvariantCulture);
            SetCombo(ApplicationChangeMode, selectedApplication.ChangeKeyMode);
            SetFlags(selectedApplication.TransportKeySettings, TransportChange, TransportList, TransportCreate, TransportConfig);
            SetFlags(selectedApplication.ProductiveKeySettings, ProductiveChange, ProductiveList, ProductiveCreate, ProductiveConfig);
        }
        RefreshFiles();
    }

    private void NewApplication_Click(object sender, RoutedEventArgs e)
    {
        var number = 1;
        string aid;
        do aid = number++.ToString("X6", CultureInfo.InvariantCulture); while (profile.Applications.Any(application => application.Id.Equals(aid, StringComparison.OrdinalIgnoreCase)));
        var application = new ApplicationDefinition { Id = aid, Name = "Neue Application", KeyType = "DF_KEY_AES", KeyCount = 1, TransportKeySettings = "0F", ProductiveKeySettings = "00", ChangeKeyMode = "ChangeKeyUsingMK" };
        profile.Applications.Add(application); dirty = true; RefreshApplications(application);
    }

    private void ApplyApplication_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedApplication is null) throw new InvalidOperationException("Keine Application ausgewählt.");
            var aid = ProfileValidator.NormalizeAid(ApplicationId.Text);
            if (profile.Applications.Any(application => !ReferenceEquals(application, selectedApplication) && application.Id.Equals(aid, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException($"AID {aid} ist bereits vorhanden.");
            editing.ChangeKeyCount(selectedApplication, RequiredInt(ApplicationKeyCount.Text, "Keyanzahl"));
            selectedApplication.Id = aid; selectedApplication.Name = Required(ApplicationName.Text, "Application-Name"); selectedApplication.KeyType = Combo(ApplicationKeyType);
            selectedApplication.ChangeKeyMode = Combo(ApplicationChangeMode);
            selectedApplication.TransportKeySettings = Flags(TransportChange, TransportList, TransportCreate, TransportConfig);
            selectedApplication.ProductiveKeySettings = Flags(ProductiveChange, ProductiveList, ProductiveCreate, ProductiveConfig);
            dirty = true; RefreshApplications(selectedApplication); ValidateAndShow(false);
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void DeleteApplication_Click(object sender, RoutedEventArgs e)
    {
        if (selectedApplication is null) return;
        if (MessageBox.Show(this, $"Application {selectedApplication.Id} einschließlich aller Dateien löschen?", "Application löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        profile.Applications.Remove(selectedApplication); selectedApplication = null; selectedFile = null; dirty = true; RefreshApplications();
    }

    private void FileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedFile = FilesGrid.SelectedItem as FileDefinition;
        if (selectedFile is not null)
        {
            FileId.Text = selectedFile.Id.ToString(CultureInfo.InvariantCulture); FileNameText.Text = selectedFile.Name; SetCombo(FileType, selectedFile.Type); FileSize.Text = selectedFile.Size.ToString(CultureInfo.InvariantCulture); SetCombo(FileCommunication, selectedFile.Communication);
            PutAccess(selectedFile.Access, FileRead, FileWrite, FileReadWrite, FileChange);
            FileHasCreation.IsChecked = selectedFile.CreationSettings is not null;
            SetCombo(CreationCommunication, selectedFile.CreationSettings?.Communication ?? "CM_PLAIN");
            PutAccess(selectedFile.CreationSettings?.Access ?? new AccessDefinition(), CreationRead, CreationWrite, CreationReadWrite, CreationChange);
            FileZeroFill.IsChecked = FileSettingsExplanationService.UsesZeroFill(selectedFile);
            FileCodingOutput.Text = selectedFile.Coding?.Output ?? "";
            UpdateFileSettingsPreview();
        }
        RefreshRecipes();
    }

    private void FileHasCreationChanged(object sender, RoutedEventArgs e) => UpdateFileSettingsPreview();
    private void FileSettingsPreviewChanged(object sender, RoutedEventArgs e) => UpdateFileSettingsPreview();

    private void CopyFinalToCreation_Click(object sender, RoutedEventArgs e)
    {
        if (selectedFile is null) return;
        SetCombo(CreationCommunication, ComboOrDefault(FileCommunication, selectedFile.Communication));
        CreationRead.Text = FileRead.Text; CreationWrite.Text = FileWrite.Text; CreationReadWrite.Text = FileReadWrite.Text; CreationChange.Text = FileChange.Text;
        FileHasCreation.IsChecked = true;
        UpdateFileSettingsPreview();
    }

    private void UpdateFileSettingsPreview()
    {
        if (CreationSettingsPanel is null || FileSettingsPreview is null || FileSettingsSequence is null || FileHasCreation is null) return;
        CreationSettingsPanel.IsEnabled = FileHasCreation.IsChecked == true;
        if (selectedFile is null) { FileSettingsPreview.Text = "Datei auswählen, um Initial-/Endzustand zu erklären."; FileSettingsSequence.Text = ""; return; }
        if (!TryAccess(FileRead, FileWrite, FileReadWrite, FileChange, out var finalAccess) ||
            (FileHasCreation.IsChecked == true && !TryAccess(CreationRead, CreationWrite, CreationReadWrite, CreationChange, out _)))
        {
            FileSettingsPreview.Text = "Für die Vorschau müssen alle Access Rights gültige Keynummern sein.";
            FileSettingsSequence.Text = "";
            return;
        }
        var candidate = new FileDefinition
        {
            Type = ComboOrDefault(FileType, selectedFile.Type), Communication = ComboOrDefault(FileCommunication, selectedFile.Communication), Access = finalAccess,
            MasteringInitialization = FileZeroFill.IsChecked == true ? new MasteringInitialization { Type = "zeroFill" } : new MasteringInitialization { Type = "none" }
        };
        if (FileHasCreation.IsChecked == true)
        {
            TryAccess(CreationRead, CreationWrite, CreationReadWrite, CreationChange, out var initialAccess);
            candidate.CreationSettings = new FileSettingsDefinition { Communication = ComboOrDefault(CreationCommunication, "CM_PLAIN"), Access = initialAccess };
        }
        var explanation = FileSettingsExplanationService.Describe(candidate);
        FileSettingsPreview.Text = $"INITIAL: {explanation.InitialTechnical}\n{explanation.InitialMeaning}\n\nFINAL: {explanation.FinalTechnical}\n{explanation.FinalMeaning}\n\n{explanation.Transition}";
        FileSettingsPreview.Foreground = explanation.IsRedundant ? System.Windows.Media.Brushes.DarkGoldenrod : System.Windows.Media.Brushes.Black;
        FileSettingsSequence.Text = "Ablauf: " + explanation.MasteringSequence;
    }

    private static bool TryAccess(TextBox read, TextBox write, TextBox readWrite, TextBox change, out AccessDefinition access)
    {
        access = new AccessDefinition();
        if (!int.TryParse(read.Text, out var r) || !int.TryParse(write.Text, out var w) || !int.TryParse(readWrite.Text, out var rw) || !int.TryParse(change.Text, out var c)) return false;
        access = new AccessDefinition { Read = r, Write = w, ReadWrite = rw, Change = c };
        return true;
    }

    private static string ComboOrDefault(ComboBox combo, string fallback) => combo.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() ?? item.Content?.ToString() ?? fallback : combo.SelectedValue?.ToString() ?? fallback;

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedApplication is null) { ShowFailure(new InvalidOperationException("Zuerst eine Application auswählen.")); return; }
        var id = Enumerable.Range(0, 32).FirstOrDefault(candidate => selectedApplication.Files.All(file => file.Id != candidate));
        if (selectedApplication.Files.Count >= 32) { ShowFailure(new InvalidOperationException("Alle FileIDs 0..31 sind belegt.")); return; }
        var file = new FileDefinition { Id = id, Name = "Neue Datei", Type = "StdDataFile", Size = 1, Communication = "CM_PLAIN", Access = new AccessDefinition() };
        selectedApplication.Files.Add(file); dirty = true; RefreshFiles(file);
    }

    private void ApplyFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedApplication is null || selectedFile is null) throw new InvalidOperationException("Keine Datei ausgewählt.");
            var id = RequiredInt(FileId.Text, "FileID");
            if (selectedApplication.Files.Any(file => !ReferenceEquals(file, selectedFile) && file.Id == id)) throw new InvalidDataException($"FileID {id} ist bereits vorhanden.");
            selectedFile.Id = id; selectedFile.Name = Required(FileNameText.Text, "Dateiname"); selectedFile.Type = Combo(FileType); selectedFile.Size = RequiredInt(FileSize.Text, "Dateigröße"); selectedFile.Communication = Combo(FileCommunication);
            selectedFile.Access = ReadAccess(FileRead, FileWrite, FileReadWrite, FileChange);
            selectedFile.CreationSettings = FileHasCreation.IsChecked == true ? new FileSettingsDefinition { Communication = Combo(CreationCommunication), Access = ReadAccess(CreationRead, CreationWrite, CreationReadWrite, CreationChange) } : null;
            FileSettingsExplanationService.SetZeroFill(selectedFile, FileZeroFill.IsChecked == true);
            var output = BlankToNull(FileCodingOutput.Text);
            if (selectedFile.Coding is not null) selectedFile.Coding.Output = output ?? "";
            else if (output is not null) selectedFile.Coding = new CodingDefinition { Output = output };
            dirty = true; RefreshFiles(selectedFile); ValidateAndShow(false);
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedApplication is null || selectedFile is null) return;
        selectedApplication.Files.Remove(selectedFile); selectedFile = null; selectedRecipe = null; dirty = true; RefreshFiles();
    }

    private void RecipeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedRecipe = (RecipeGrid.SelectedItem as RecipeRow)?.Step;
        if (selectedRecipe is null) return;
        SetCombo(RecipeOperation, selectedRecipe.Op); RecipeSource.SelectedValue = selectedRecipe.Source; RecipeValue.Text = selectedRecipe.Value ?? ""; RecipeWidth.Text = Text(selectedRecipe.Width); RecipePadLeft.Text = selectedRecipe.PadLeft ?? ""; RecipePadRight.Text = selectedRecipe.PadRight ?? "";
        UpdateRecipeOperationHelp();
    }

    private void NewRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (selectedFile is null) { ShowFailure(new InvalidOperationException("Zuerst eine Datei auswählen.")); return; }
        selectedFile.Coding ??= new CodingDefinition { Output = string.IsNullOrWhiteSpace(FileCodingOutput.Text) ? "Daten" : FileCodingOutput.Text.Trim() };
        var step = new RecipeStep { Op = "fixedHex", Value = "00" }; selectedFile.Coding.Recipe.Add(step); dirty = true; RefreshRecipes(step);
    }

    private void ApplyRecipe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selectedRecipe is null) throw new InvalidOperationException("Kein Rezeptbaustein ausgewählt.");
            var op = Combo(RecipeOperation); selectedRecipe.Op = op;
            selectedRecipe.Source = UsesSource(op) ? RecipeSource.SelectedValue?.ToString() : null;
            selectedRecipe.Value = UsesValue(op) ? (op is "fixedAscii" or "fixedUtf8" ? RecipeValue.Text : BlankToNull(RecipeValue.Text)) : null;
            selectedRecipe.Width = UsesWidth(op) ? OptionalInt(RecipeWidth.Text, "Breite") : null;
            selectedRecipe.PadLeft = UsesDirectionalPadding(op) ? BlankToNull(RecipePadLeft.Text)?.ToUpperInvariant() : null;
            selectedRecipe.PadRight = UsesDirectionalPadding(op) ? BlankToNull(RecipePadRight.Text)?.ToUpperInvariant() : null;
            dirty = true; RefreshRecipes(selectedRecipe); ValidateAndShow(false);
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void DeleteRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (selectedFile?.Coding is null || selectedRecipe is null) return;
        selectedFile.Coding.Recipe.Remove(selectedRecipe); selectedRecipe = null;
        if (selectedFile.Coding.Recipe.Count == 0 && string.IsNullOrWhiteSpace(selectedFile.Coding.Output)) selectedFile.Coding = null;
        dirty = true; RefreshRecipes();
    }

    private void RecipeUp_Click(object sender, RoutedEventArgs e) => MoveRecipe(-1);
    private void RecipeDown_Click(object sender, RoutedEventArgs e) => MoveRecipe(1);
    private void MoveRecipe(int delta)
    {
        if (selectedFile?.Coding is null || selectedRecipe is null) return;
        var index = selectedFile.Coding.Recipe.IndexOf(selectedRecipe); var target = index + delta;
        if (target < 0 || target >= selectedFile.Coding.Recipe.Count) return;
        selectedFile.Coding.Recipe.RemoveAt(index); selectedFile.Coding.Recipe.Insert(target, selectedRecipe); dirty = true; RefreshRecipes(selectedRecipe);
    }

    private void RecipeOperationChanged(object sender, SelectionChangedEventArgs e) => UpdateRecipeOperationHelp();

    private void UpdateRecipeOperationHelp()
    {
        if (RecipeOperationHelp is null) return;
        var op = ComboOrDefault(RecipeOperation, "fixedHex");
        RecipeOperationHelp.Text = op switch
        {
            "fixedHex" => "Feste Bytes, z. B. A0 01 FF. Zwei HEX-Zeichen ergeben ein Byte.",
            "fixedAscii" => "Fester lesbarer Klartext. Nur ASCII-Zeichen; ein Zeichen ergibt ein Byte.",
            "fixedUtf8" => "Fester Unicode-Text als UTF-8. Umlaute können mehrere Bytes benötigen.",
            "hexBytes" => "Eine Variable wird als rohe HEX-Bytefolge eingesetzt, nicht als sichtbare Zeichen.",
            "asciiText" => "Beliebiger lesbarer ASCII-Inhalt aus einer Variable; optional links oder rechts auffüllen.",
            "utf8Text" => "Unicode-Text aus einer Variable mit fester Bytebreite; optional auffüllen.",
            "asciiDigits" => "Ziffernfolge als sichtbare Zeichen 0..9 (30..39 HEX), links auffüllbar.",
            "bcdDigits" => "Je zwei Dezimalziffern werden in einem Byte gepackt, z. B. 1234 → 12 34.",
            "uintBigEndian" => "Vorzeichenlose Dezimalzahl, höchstwertiges Byte zuerst.",
            "uintLittleEndian" => "Vorzeichenlose Dezimalzahl, niedrigstwertiges Byte zuerst.",
            "zeroPadToFileSize" => "Füllt den verbleibenden Dateibereich mit 00.",
            "padToFileSize" => "Füllt den verbleibenden Dateibereich mit dem angegebenen HEX-Byte.",
            _ => op
        };
    }

    private void Validate_Click(object sender, RoutedEventArgs e) => ValidateAndShow(true);

    private bool ValidateAndShow(bool dialog)
    {
        ApplyMetadata();
        var issues = editing.Validate(profile);
        ValidationText.Text = issues.Count == 0 ? "OK · Profil ist vollständig und konsistent." : $"{issues.Count} Befund(e): " + string.Join(" · ", issues.Take(6).Select(issue => $"{issue.Code}: {issue.Message}"));
        ValidationText.Foreground = issues.Count == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkRed;
        if (dialog) MessageBox.Show(this, ValidationText.Text, "Profilprüfung", MessageBoxButton.OK, issues.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        return issues.Count == 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyMetadata();
            if (!ValidateAndShow(false)) throw new InvalidDataException("Das Profil enthält noch Validierungsfehler und wurde nicht gespeichert.");
            if (isNew && store.Load().Profiles.Any(candidate => string.Equals(candidate.Profile.Id, profile.Profile.Id, StringComparison.Ordinal)) &&
                MessageBox.Show(this, $"Profil '{profile.Profile.Id}' existiert bereits. Den bestehenden Profilstand bewusst ersetzen?", "Profil ersetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var isBase = string.Equals(profile.Profile.Kind, "base", StringComparison.Ordinal);
            if (isNew && File.Exists(isBase ? store.GetBasePath(profile.Profile.Id) : store.GetCustomerPath(profile.Profile.Id)))
                sourceRevision = ConcurrentFileWriter.Capture(isBase ? store.GetBasePath(profile.Profile.Id) : store.GetCustomerPath(profile.Profile.Id));
            SavedPath = isBase ? store.SaveBase(profile) : store.SaveCustomer(profile, sourceRevision);
            var historyCount = store.ListHistory(profile.Profile.Id).Count;
            ProfileHistoryText.Text = $"Gespeichert: {profile.Profile.Version} · {historyCount} archivierte Vorgängerversion(en).";
            saved = true; dirty = false; DialogResult = true;
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void ApplyMetadata()
    {
        profile.Profile.Id = Required(ProfileId.Text, "Profil-ID"); profile.Profile.Code = Required(ProfileCode.Text, "Profilcode"); profile.Profile.Name = Required(ProfileName.Text, "Profilname"); profile.Profile.Version = Required(ProfileVersion.Text, "Version"); profile.Profile.Description = ProfileDescription.Text.Trim();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (saved || !dirty) return;
        if (MessageBox.Show(this, "Ungespeicherte Profiländerungen verwerfen?", "Profil schließen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void ShowFailure(Exception exception) => MessageBox.Show(this, exception.Message, "Profil-Editor", MessageBoxButton.OK, MessageBoxImage.Error);
    private string BuildRecipePreview()
    {
        if (selectedFile?.Coding is null) return "Noch kein Kodierrezept vorhanden.";
        try
        {
            var values = profile.Variables.ToDictionary(variable => variable.Id, SampleValue, StringComparer.Ordinal);
            var bytes = PayloadEngine.Build(selectedFile, values, selectedApplication?.Id ?? "000000");
            var lines = new List<string>();
            for (var offset = 0; offset < bytes.Length; offset += 16)
            {
                var row = bytes.Skip(offset).Take(16).ToArray();
                var hex = string.Join(" ", row.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))).PadRight(47);
                var plain = new string(row.Select(value => value is >= 0x20 and <= 0x7e ? (char)value : '·').ToArray());
                lines.Add($"{offset:X4}  {hex}  |{plain}|");
            }
            return string.Join(Environment.NewLine, lines) + $"{Environment.NewLine}{bytes.Length} / {selectedFile.Size} Byte";
        }
        catch (Exception exception) { return "Vorschau noch nicht möglich: " + exception.Message; }
    }

    private static string SampleValue(VariableDefinition variable)
    {
        if (variable.Default is not null) return variable.Default;
        var length = variable.Length ?? variable.AllowedLengths?.FirstOrDefault() ?? variable.MinLength ?? 1;
        if (variable.Type == "hex") { if (length % 2 != 0) length++; return new string('0', Math.Max(2, length)); }
        return variable.Type == "digits" ? new string('0', length) : new string('A', length);
    }

    private static bool UsesSource(string op) => op is "hexBytes" or "asciiText" or "utf8Text" or "asciiDigits" or "bcdDigits" or "uintBigEndian" or "uintLittleEndian";
    private static bool UsesValue(string op) => op is "fixedHex" or "fixedAscii" or "fixedUtf8" or "padToFileSize";
    private static bool UsesWidth(string op) => UsesSource(op);
    private static bool UsesDirectionalPadding(string op) => op is "hexBytes" or "asciiText" or "utf8Text";
    private static string Required(string value, string label) => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(label + " fehlt.") : value.Trim();
    private static int RequiredInt(string value, string label) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : throw new InvalidDataException(label + " ist keine ganze Zahl.");
    private static int? OptionalInt(string value, string label) => string.IsNullOrWhiteSpace(value) ? null : RequiredInt(value, label);
    private static List<int>? ParseIntList(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => RequiredInt(part, "Erlaubte Länge")).ToList();
    private static string? BlankToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Combo(ComboBox box) => box.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() ?? item.Content?.ToString() ?? throw new InvalidDataException("Auswahl fehlt.") : box.SelectedValue?.ToString() ?? throw new InvalidDataException("Auswahl fehlt.");
    private static void SetCombo(ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString() ?? item.Content?.ToString(), value, StringComparison.Ordinal));
    private static string Flags(CheckBox a, CheckBox b, CheckBox c, CheckBox d) => ProfileEditingService.KeySettingsFromFlags(a.IsChecked == true, b.IsChecked == true, c.IsChecked == true, d.IsChecked == true);
    private static void SetFlags(string value, CheckBox a, CheckBox b, CheckBox c, CheckBox d) { var flags = ProfileEditingService.KeySettingsFlags(value); a.IsChecked = flags.ChangeMasterKey; b.IsChecked = flags.ListApplications; c.IsChecked = flags.CreateDelete; d.IsChecked = flags.ConfigurationChange; }
    private static AccessDefinition ReadAccess(TextBox read, TextBox write, TextBox readWrite, TextBox change) => new() { Read = RequiredInt(read.Text, "Leserecht"), Write = RequiredInt(write.Text, "Schreibrecht"), ReadWrite = RequiredInt(readWrite.Text, "Lese-/Schreibrecht"), Change = RequiredInt(change.Text, "Änderungsrecht") };
    private static void PutAccess(AccessDefinition access, TextBox read, TextBox write, TextBox readWrite, TextBox change) { read.Text = access.Read.ToString(CultureInfo.InvariantCulture); write.Text = access.Write.ToString(CultureInfo.InvariantCulture); readWrite.Text = access.ReadWrite.ToString(CultureInfo.InvariantCulture); change.Text = access.Change.ToString(CultureInfo.InvariantCulture); }
}

public sealed record RecipeRow(int Position, RecipeStep Step)
{
    public string Format => Step.Op switch
    {
        "fixedHex" => "HEX-Konstante", "fixedAscii" => "Klartext · ASCII", "fixedUtf8" => "Text · UTF-8",
        "hexBytes" => "Variable · HEX", "asciiText" => "Variable · ASCII", "utf8Text" => "Variable · UTF-8",
        "asciiDigits" => "Ziffern · ASCII", "bcdDigits" => "Ziffern · BCD", "uintBigEndian" => "Ganzzahl · BE",
        "uintLittleEndian" => "Ganzzahl · LE", "zeroPadToFileSize" => "Padding · 00", "padToFileSize" => "Padding · Byte", _ => Step.Op
    };
    public string SourceOrValue => Step.Source ?? Step.Value ?? "bis Dateiende";
    public string ByteWidth => Step.Op switch
    {
        "fixedHex" => ((Step.Value ?? "").Count(Uri.IsHexDigit) / 2).ToString(CultureInfo.InvariantCulture),
        "fixedAscii" => (Step.Value ?? "").Length.ToString(CultureInfo.InvariantCulture),
        "fixedUtf8" => Encoding.UTF8.GetByteCount(Step.Value ?? "").ToString(CultureInfo.InvariantCulture),
        "bcdDigits" when Step.Width is { } width => (width / 2).ToString(CultureInfo.InvariantCulture),
        "zeroPadToFileSize" or "padToFileSize" => "Rest", _ => Step.Width?.ToString(CultureInfo.InvariantCulture) ?? "–"
    };
}

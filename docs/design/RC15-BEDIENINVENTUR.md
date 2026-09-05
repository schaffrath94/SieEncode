# RC15: technische Bedieninventur

Baseline: `41617c7ae104954b6ef7e9ac846002d62acb7832`. Automatisch aus dem RC15-Tag erzeugt.

Diese Liste belegt vorhandene Einstiegspunkte, nicht vollständige Funktionsgleichheit oder Hardwarefreigabe. Fachliche Zielzuordnung: FUNKTIONSPARITAET.md.

## XAML-Aktionen und Eingabeflächen

| Datei | Bereich | Element | Beschriftung | Ereignis / Ziel |
|---|---|---|---|---|
| MainWindow.xaml | MainWindow.xaml | Window |  | Loaded: Window_Loaded, Closing: Window_Closing |
| MainWindow.xaml | MainWindow.xaml | NavHomeButton | Übersicht | Click: Navigate_Click / Home |
| MainWindow.xaml | MainWindow.xaml | NavProjectsButton | Projekte | Click: Navigate_Click / Projects |
| MainWindow.xaml | MainWindow.xaml | NavStationButton | Kodierstation | Click: Navigate_Click / Station |
| MainWindow.xaml | MainWindow.xaml | NavTestStationButton | Teststation | Click: Navigate_Click / TestStation |
| MainWindow.xaml | MainWindow.xaml | NavProfilesButton | Profile verwalten | Click: Navigate_Click / Profiles |
| MainWindow.xaml | MainWindow.xaml | NavHelpButton | Hilfe | Click: Navigate_Click / Help |
| MainWindow.xaml | MainWindow.xaml | NavSettingsButton | Einstellungen | Click: Navigate_Click / Settings |
| MainWindow.xaml | MainWindow.xaml | BackButton | ← Zurück | Click: Back_Click |
| MainWindow.xaml | MainWindow.xaml | ExpertModeToggle |  | Checked: ExpertMode_Changed, Unchecked: ExpertMode_Changed |
| MainWindow.xaml | MainWindow.xaml | TutorialButton | Hilfe & Tour | Click: Tutorial_Click |
| MainWindow.xaml | PageHome | Button | Neues Projekt | Click: NewProject_Click |
| MainWindow.xaml | PageHome | Button | Projekt auswählen | Click: Navigate_Click / Projects |
| MainWindow.xaml | PageHome | HomeAnalyzeCardButton | Teststation öffnen | Click: Navigate_Click / TestStation |
| MainWindow.xaml | PageHome | HomeVaultButton | Projekt und Vault prüfen | Click: Navigate_Click / Editor |
| MainWindow.xaml | PageHome | Button | Einstellungen und Integration | Click: Navigate_Click / Settings |
| MainWindow.xaml | PageHome | Button | Kodierstation öffnen | Click: Navigate_Click / Station |
| MainWindow.xaml | PageTestStation | TestAnalyzeCardButton | Transponder analysieren | Click: AnalyzeCardWithoutProject_Click |
| MainWindow.xaml | PageTestStation | TestDeepAnalyzeButton | Application genauer untersuchen | Click: DeepAnalyzeApplication_Click |
| MainWindow.xaml | PageTestStation | TestOpenRecognitionResultButton | Letzten Bericht öffnen | Click: OpenLatestTestReport_Click |
| MainWindow.xaml | PageTestStation | TestAnalysisTree |  |  |
| MainWindow.xaml | PageTestStation | Button | Projekt wählen | Click: Navigate_Click / Projects |
| MainWindow.xaml | PageTestStation | TestProjectRecognitionButton | Struktur mit Projekt vergleichen | Click: TestProjectRecognition_Click |
| MainWindow.xaml | PageTestStation | TestProjectTransportButton | Projektfiles mit Ausgangskeys lesen | Click: TestProjectTransport_Click |
| MainWindow.xaml | PageTestStation | TestProjectProductiveButton | Projektfiles mit Produktivkeys lesen | Click: TestProjectProductive_Click |
| MainWindow.xaml | PageTestStation | TestProjectAcceptanceButton | Application-Einstellungen prüfen | Click: TestProjectAcceptance_Click |
| MainWindow.xaml | PageTestStation | TestHardwarePlanButton | Hardware-Abnahmeplan | Click: PrepareHardwareAcceptance_Click |
| MainWindow.xaml | PageTestStation | Button | RFiDGear für Lesereinstellungen öffnen | Click: OpenRFiDGearSettings_Click |
| MainWindow.xaml | PageTestStation | TestRefreshOperationButton | Laufstatus aktualisieren | Click: RefreshOperation_Click |
| MainWindow.xaml | PageTestStation | TestCloseRFiDGearButton | RFiDGear-Prozess abschließen | Click: CloseRFiDGear_Click |
| MainWindow.xaml | PageTestStation | TestMaintenanceActions | Wartung · verändert den Transponder |  |
| MainWindow.xaml | PageTestStation | TestFormatCardButton | Transponder formatieren … | Click: FormatCardWithoutProject_Click |
| MainWindow.xaml | PageProjects | ProjectSearchText |  | TextChanged: ProjectSearch_Changed |
| MainWindow.xaml | PageProjects | Button | Aktualisieren | Click: RefreshProjects_Click |
| MainWindow.xaml | PageProjects | ProjectsGrid |  | SelectionChanged: ProjectsGrid_SelectionChanged, MouseDoubleClick: ProjectsGrid_MouseDoubleClick |
| MainWindow.xaml | PageProjects | Button | Neues Projekt | Click: NewProject_Click |
| MainWindow.xaml | PageProjects | ProjectEditButton | Bearbeiten | Click: EditSelectedProject_Click |
| MainWindow.xaml | PageProjects | ProjectValidationButton | Projekt prüfen | Click: Preflight_Click |
| MainWindow.xaml | PageProjects | ProjectOpenStationButton | Projekt wählen | Click: LoadStation_Click |
| MainWindow.xaml | PageProjects | Button | In Teststation prüfen | Click: Navigate_Click / TestStation |
| MainWindow.xaml | PageProjects | ProjectOpenFolderButton | Ordner öffnen | Click: OpenProjectFolder_Click |
| MainWindow.xaml | PageProjects | DeleteProjectButton | Projekt löschen … | Click: DeleteProject_Click |
| MainWindow.xaml | PageProjects | ProjectAdvancedActions | Weitere Projektaktionen · Experten |  |
| MainWindow.xaml | PageProjects | Button | Details | Click: ProjectDetails_Click |
| MainWindow.xaml | PageProjects | ImportLegacyButton | Alte JSON-Konfiguration importieren | Click: ImportLegacyProject_Click |
| MainWindow.xaml | PageProjects | Button | Analyse / Dry-Run | Click: AnalyzeSelectedProject_Click |
| MainWindow.xaml | PageProjects | QuickStartCodingButton | Kodierstation und RFiDGear öffnen | Click: QuickStartCoding_Click |
| MainWindow.xaml | PageProjects | ProjectPreflightIssuesGrid |  |  |
| MainWindow.xaml | PageProjects | ProjectPreflightRepairButton | Projekt bearbeiten und neu bauen | Click: RepairProjectFromPreflight_Click |
| MainWindow.xaml | PageProjects | Button | Prüfung schließen | Click: CloseProjectPreflight_Click |
| MainWindow.xaml | PageEditor | EditorProjectName |  | TextChanged: EditorControl_Changed |
| MainWindow.xaml | PageEditor | EditorCardMode |  | SelectionChanged: EditorCardMode_Changed |
| MainWindow.xaml | PageEditor | EditorBuildSettings | Technische Build-Einstellungen · Experten |  |
| MainWindow.xaml | PageEditor | EditorOutputDirectory |  | TextChanged: EditorControl_Changed |
| MainWindow.xaml | PageEditor | EditorBuildMastering | Mastering | Checked: EditorControl_Changed, Unchecked: EditorControl_Changed |
| MainWindow.xaml | PageEditor | EditorBuildCoding | Kodierung | Checked: EditorControl_Changed, Unchecked: EditorControl_Changed |
| MainWindow.xaml | PageEditor | EditorBaseProfile |  | SelectionChanged: EditorProfileSelection_Changed |
| MainWindow.xaml | PageEditor | EditorCustomerProfiles |  | SelectionChanged: EditorProfileSelection_Changed |
| MainWindow.xaml | PageEditor | EditorBindProfilesButton | Aktuelle Profilstände verbindlich übernehmen | Click: BindCurrentProfiles_Click |
| MainWindow.xaml | PageEditor | EditorProjectValues |  |  |
| MainWindow.xaml | PageEditor | EditorTransportSettings | Grundkeys bearbeiten · Experteneinstellung |  |
| MainWindow.xaml | PageEditor | EditorTransportPiccType |  | SelectionChanged: EditorInitialKeyControl_Changed |
| MainWindow.xaml | PageEditor | EditorTransportPiccKey |  | TextChanged: EditorInitialKeyControl_Changed |
| MainWindow.xaml | PageEditor | EditorTransportKeys |  |  |
| MainWindow.xaml | PageEditor | EditorInitialKeySet |  | SelectionChanged: EditorInitialKeySet_Changed |
| MainWindow.xaml | PageEditor | EditorAddInitialKeySetButton | Aktuellen Satz hinzufügen | Click: AddInitialKeySet_Click |
| MainWindow.xaml | PageEditor | EditorDeleteInitialKeySetButton | Satz löschen | Click: DeleteInitialKeySet_Click |
| MainWindow.xaml | PageEditor | EditorGenerateKeysButton | Neue Schlüssel erzeugen | Click: GenerateProductiveKeys_Click |
| MainWindow.xaml | PageEditor | EditorUnlockVaultButton | Vault entsperren | Click: UnlockVault_Click |
| MainWindow.xaml | PageEditor | EditorSaveVaultButton | Einzelbenutzer-Vault speichern | Click: SaveVault_Click |
| MainWindow.xaml | PageEditor | EditorManageVaultAccessButton | Mehrbenutzerzugriff einrichten | Click: ManageVaultAccess_Click |
| MainWindow.xaml | PageEditor | EditorAuditVaultButton | Vault technisch prüfen | Click: AuditVault_Click |
| MainWindow.xaml | PageEditor | EditorProductiveKeyDetails | Schlüsselwerte anzeigen oder manuell prüfen |  |
| MainWindow.xaml | PageEditor | EditorProductivePiccType |  | SelectionChanged: EditorProductiveControl_Changed |
| MainWindow.xaml | PageEditor | EditorProductivePiccKey |  | TextChanged: EditorProductiveControl_Changed |
| MainWindow.xaml | PageEditor | EditorPiccKeySettings |  | TextChanged: EditorProductiveControl_Changed |
| MainWindow.xaml | PageEditor | EditorProductiveKeys |  |  |
| MainWindow.xaml | PageEditor | EditorSaveButton | Zwischenspeichern | Click: SaveProject_Click |
| MainWindow.xaml | PageEditor | EditorBuildButton | Projekt speichern, prüfen und bauen | Click: BuildProject_Click |
| MainWindow.xaml | PageEditor | Button | Projekt schließen | Click: CloseProject_Click |
| MainWindow.xaml | PageEditor | EditorAdvancedActions | Weitere Editoraktionen · Experten |  |
| MainWindow.xaml | PageEditor | Button | Neues Projekt | Click: NewProject_Click |
| MainWindow.xaml | PageEditor | Button | Speichern unter | Click: SaveProjectAs_Click |
| MainWindow.xaml | PageEditor | EditorAnalyzeButton | Sicherer Dry-Run | Click: AnalyzeEditorProject_Click |
| MainWindow.xaml | PageStation | Button | Projekt wählen | Click: Navigate_Click / Projects |
| MainWindow.xaml | PageStation | Button | Teststation öffnen | Click: Navigate_Click / TestStation |
| MainWindow.xaml | PageStation | StationPreflightButton | Projekt prüfen | Click: Preflight_Click |
| MainWindow.xaml | PageStation | StationProjectFolderButton | Projektordner öffnen | Click: OpenProjectFolder_Click |
| MainWindow.xaml | PageStation | StationRecognitionButton | Transponder erkennen | Click: CardRecognition_Click |
| MainWindow.xaml | PageStation | StationReadProjectContentButton | Projektinhalt auslesen | Click: ReadProjectCardContent_Click |
| MainWindow.xaml | PageStation | StationLegacyTransportButton | Altprojekt · Kartenstand prüfen | Click: CardPreflightTransport_Click |
| MainWindow.xaml | PageStation | StationOpenRecognitionResultButton | Erkennungsbericht öffnen | Click: OpenCardRecognitionResult_Click |
| MainWindow.xaml | PageStation | StationDetailedPreflight | Vertiefte Projektprüfung · Experten |  |
| MainWindow.xaml | PageStation | StationPreflightTransportButton | Mit Transportdaten prüfen | Click: CardPreflightTransport_Click |
| MainWindow.xaml | PageStation | StationPreflightProductiveButton | Mit Produktivdaten prüfen | Click: CardPreflightProductive_Click |
| MainWindow.xaml | PageStation | StationOpenPreflightResultButton | Prüfbericht öffnen | Click: OpenCardPreflightResult_Click |
| MainWindow.xaml | PageStation | StationMasteringAutoButton | Transponder vorbereiten | Click: MasteringAuto_Click |
| MainWindow.xaml | PageStation | StationAdvancedCardActions | Erweiterte Kartenaktionen · manuell, Abnahme und Recovery |  |
| MainWindow.xaml | PageStation | StationMasteringManualButton | Vorbereitung manuell öffnen | Click: MasteringManual_Click |
| MainWindow.xaml | PageStation | StationApplicationAcceptanceButton | Anwendungen prüfen | Click: ApplicationAcceptance_Click |
| MainWindow.xaml | PageStation | HardwareAcceptancePlanButton | Hardware-Abnahmeplan erzeugen | Click: PrepareHardwareAcceptance_Click |
| MainWindow.xaml | PageStation | StationRecoveryTransportButton | Recovery · Transport-PICC | Click: RecoveryTransport_Click |
| MainWindow.xaml | PageStation | StationRecoveryProductiveButton | Recovery · Produktiv-PICC | Click: RecoveryProductive_Click |
| MainWindow.xaml | PageStation | StationExtensionRollbackButton | Erweiterung gezielt zurücknehmen | Click: ExtensionRollback_Click |
| MainWindow.xaml | PageStation | StationRefreshOperationButton | Laufstatus aus Log aktualisieren | Click: RefreshOperation_Click |
| MainWindow.xaml | PageStation | StationCloseRFiDGearButton | RFiDGear-Prozess abschließen | Click: CloseRFiDGear_Click |
| MainWindow.xaml | PageStation | StationSeriesStep | 3 · Serienmodus (optional) |  |
| MainWindow.xaml | PageStation | StationMode |  | SelectionChanged: StationMode_SelectionChanged |
| MainWindow.xaml | PageStation | StationSeriesField |  |  |
| MainWindow.xaml | PageStation | StationSequenceValue |  |  |
| MainWindow.xaml | PageStation | StationSequenceStep |  |  |
| MainWindow.xaml | PageStation | StationLoadListButton | TXT / CSV laden | Click: LoadStationList_Click |
| MainWindow.xaml | PageStation | StationFieldsControl |  |  |
| MainWindow.xaml | PageStation | StationPrepareButton | 1 · Daten prüfen und bereitstellen (Enter) | Click: PrepareSession_Click |
| MainWindow.xaml | PageStation | StationStartAutoModeButton | 2 · Kodierung starten | Click: StartAutoMode_Click |
| MainWindow.xaml | PageStation | StationCheckReadbacksButton | 3 · Ergebnis jetzt prüfen (optional · F8) | Click: CheckReadbacks_Click |
| MainWindow.xaml | PageStation | StationOpenRFiDGearButton | Manuell in RFiDGear öffnen | Click: OpenRFiDGear_Click |
| MainWindow.xaml | PageStation | StationCompletionStep | 5 · Abschluss und Sitzungsverlauf |  |
| MainWindow.xaml | PageStation | StationMarkFailedButton | Lauf als Fehler markieren (F9) | Click: MarkStationFailed_Click |
| MainWindow.xaml | PageStation | StationRetryButton | Fehlernummer erneut versuchen | Click: RetryStation_Click |
| MainWindow.xaml | PageStation | StationSkipButton | Fehlernummer überspringen | Click: SkipStation_Click |
| MainWindow.xaml | PageStation | StationExportButton | Zusammenfassung / Sitzung schließen | Click: ExportStation_Click |
| MainWindow.xaml | PageStation | StationDiagnosticButton | Diagnosepaket speichern | Click: ExportDiagnostic_Click |
| MainWindow.xaml | PageStation | StationHistoryGrid |  |  |
| MainWindow.xaml | PageStation | StationTaskOutcomesGrid |  |  |
| MainWindow.xaml | PageProfiles | ProfileNewBaseButton | Neues Grundprofil | Click: NewBaseProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileImportBaseButton | Aus JSON importieren | Click: ImportBaseProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileImportBaseRfPrjButton | Aus rfPrj ableiten | Click: ImportBaseFromRfPrj_Click |
| MainWindow.xaml | PageProfiles | Button | Grundprofilordner öffnen | Click: OpenBaseProfileFolder_Click |
| MainWindow.xaml | PageProfiles | ProfileNewButton | Neues Zusatzprofil | Click: NewProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileImportButton | Bestehendes rfPrj importieren | Click: ImportRfPrjProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileEditButton | Ausgewähltes Profil bearbeiten | Click: EditProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileDuplicateButton | Zusatzprofil duplizieren | Click: DuplicateProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileExportButton | Kodiervorschrift exportieren | Click: ExportProfile_Click |
| MainWindow.xaml | PageProfiles | ProfileHistoryButton | Historie / Wiederherstellen | Click: ProfileHistory_Click |
| MainWindow.xaml | PageProfiles | Button | Profile neu laden | Click: ReloadProfiles_Click |
| MainWindow.xaml | PageProfiles | Button | Zusatzprofilordner öffnen | Click: OpenProfileFolder_Click |
| MainWindow.xaml | PageProfiles | Button | Historienordner öffnen | Click: OpenProfileHistoryFolder_Click |
| MainWindow.xaml | PageProfiles | ProfilesGrid |  | SelectionChanged: ProfilesGrid_SelectionChanged |
| MainWindow.xaml | PageAnalysis | AnalysisEditorButton | Aktuelles Editorprojekt analysieren | Click: AnalyzeEditorProject_Click |
| MainWindow.xaml | PageAnalysis | AnalysisSelectedButton | Ausgewähltes Projekt analysieren | Click: AnalyzeSelectedProject_Click |
| MainWindow.xaml | PageAnalysis | Button | Projektdatei wählen | Click: AnalyzeFile_Click |
| MainWindow.xaml | PageAnalysis | AnalysisFilesGrid |  |  |
| MainWindow.xaml | PageAnalysis | AnalysisMasteringGrid |  |  |
| MainWindow.xaml | PageAnalysis | AnalysisCodingGrid |  |  |
| MainWindow.xaml | PageAnalysis | AnalysisIssuesGrid |  |  |
| MainWindow.xaml | PageAnalysis | AnalysisExportPdfButton | PDF-Kartenreport speichern | Click: ExportAnalysisPdf_Click |
| MainWindow.xaml | PageAnalysis | AnalysisOpenReportButton | Textbericht öffnen | Click: OpenAnalysisReport_Click |
| MainWindow.xaml | PageAnalysis | AnalysisOpenFolderButton | Berichtsordner öffnen | Click: OpenAnalysisFolder_Click |
| MainWindow.xaml | PageDiff | DiffLeftProject |  |  |
| MainWindow.xaml | PageDiff | DiffRightProject |  |  |
| MainWindow.xaml | PageDiff | Button | Projekte vergleichen | Click: CompareProjects_Click |
| MainWindow.xaml | PageDiff | Button | Editor links / ausgewähltes Projekt rechts | Click: CompareEditorToSelected_Click |
| MainWindow.xaml | PageDiff | DiffLeftProfile |  |  |
| MainWindow.xaml | PageDiff | DiffRightProfile |  |  |
| MainWindow.xaml | PageDiff | Button | Profile vergleichen | Click: CompareProfiles_Click |
| MainWindow.xaml | PageDiff | DiffGrid |  |  |
| MainWindow.xaml | PageDiff | DiffOpenReportButton | Textbericht öffnen | Click: OpenDiffReport_Click |
| MainWindow.xaml | PageDiff | DiffOpenFolderButton | Berichtsordner öffnen | Click: OpenDiffFolder_Click |
| MainWindow.xaml | PageAcceptance | Button | Projekt auswählen | Click: Navigate_Click / Projects |
| MainWindow.xaml | PageAcceptance | Button | Projekt prüfen | Click: Preflight_Click |
| MainWindow.xaml | PageAcceptance | AcceptanceRunButton | Prüfbericht erzeugen | Click: RunAcceptance_Click |
| MainWindow.xaml | PageAcceptance | AcceptanceGrid |  |  |
| MainWindow.xaml | PageAcceptance | AcceptanceOpenReportButton | Textbericht öffnen | Click: OpenAcceptanceReport_Click |
| MainWindow.xaml | PageAcceptance | AcceptanceOpenFolderButton | Berichtsordner öffnen | Click: OpenAcceptanceFolder_Click |
| MainWindow.xaml | PageHelp | Button | Projekteditor | Click: Navigate_Click / Editor |
| MainWindow.xaml | PageHelp | Button | Projektanalyse | Click: Navigate_Click / Analysis |
| MainWindow.xaml | PageHelp | Button | Projekt-/Profilvergleich | Click: Navigate_Click / Diff |
| MainWindow.xaml | PageHelp | Button | Prüfung & Abnahme | Click: Navigate_Click / Acceptance |
| MainWindow.xaml | PageHelp | Button | Schnellstart | Click: OpenHelpDocument_Click / README.md |
| MainWindow.xaml | PageHelp | Button | Funktionsstand | Click: OpenHelpDocument_Click / PARITY.md |
| MainWindow.xaml | PageHelp | Button | Grundprofile | Click: OpenHelpDocument_Click / docs/GRUNDPROFILE.md |
| MainWindow.xaml | PageHelp | Button | Migration | Click: OpenHelpDocument_Click / MIGRATION.md |
| MainWindow.xaml | PageHelp | Button | Betrieb | Click: OpenHelpDocument_Click / RUNTIME.md |
| MainWindow.xaml | PageHelp | Button | Hardware-Testvertrag | Click: OpenHelpDocument_Click / docs/HARDWARE-ABNAHME.md |
| MainWindow.xaml | PageHelp | HelpHardwareAcceptanceButton | Projektplan erzeugen | Click: PrepareHardwareAcceptance_Click |
| MainWindow.xaml | PageHelp | Button | Projektbibliothek öffnen | Click: OpenHelpProjects_Click |
| MainWindow.xaml | PageHelp | Button | Einstellungsordner öffnen | Click: OpenHelpSettings_Click |
| MainWindow.xaml | PageHelp | Button | RFiDGear-Logs öffnen | Click: OpenHelpLogs_Click |
| MainWindow.xaml | PageHelp | Button | Programmfunktionen automatisch prüfen | Click: RunFunctionAudit_Click |
| MainWindow.xaml | PageHelp | FunctionAuditOpenButton | Funktionsbericht öffnen | Click: OpenFunctionAudit_Click |
| MainWindow.xaml | PageHelp | EnvironmentChecksGrid |  |  |
| MainWindow.xaml | PageHelp | Button | Umgebung jetzt prüfen | Click: RunEnvironmentCheck_Click |
| MainWindow.xaml | PageHelp | EnvironmentOpenReportButton | Textbericht öffnen | Click: OpenEnvironmentReport_Click |
| MainWindow.xaml | PageHelp | EnvironmentOpenFolderButton | Berichtsordner öffnen | Click: OpenEnvironmentFolder_Click |
| MainWindow.xaml | PageSettings | ProjectsDirectoryText |  |  |
| MainWindow.xaml | PageSettings | Button | Ordner wählen | Click: BrowseProjects_Click |
| MainWindow.xaml | PageSettings | ProfilesDirectoryText |  |  |
| MainWindow.xaml | PageSettings | Button | Ordner wählen | Click: BrowseProfiles_Click |
| MainWindow.xaml | PageSettings | VaultBackupDirectoryText |  |  |
| MainWindow.xaml | PageSettings | Button | Ändern | Click: BrowseVaultBackup_Click |
| MainWindow.xaml | PageSettings | InitializeTeamVaultButton | Vault-Ersteinrichtung starten | Click: InitializeTeamVault_Click |
| MainWindow.xaml | PageSettings | ManageTeamVaultButton | Teamzugriff verwalten | Click: ManageTeamVault_Click |
| MainWindow.xaml | PageSettings | RFiDGearExecutableText |  |  |
| MainWindow.xaml | PageSettings | Button | Datei wählen | Click: BrowseRFiDGear_Click |
| MainWindow.xaml | PageSettings | UseBundledRuntimeButton | Gebündelte SieEncode Runtime verwenden | Click: UseBundledRuntime_Click |
| MainWindow.xaml | PageSettings | SettingsCapabilityDetails | Technische Fähigkeitsmatrix · Experten |  |
| MainWindow.xaml | PageSettings | RFiDGearCapabilitiesGrid |  |  |
| MainWindow.xaml | PageSettings | OpenRFiDGearSettingsButton | Reader-Einstellungen in RFiDGear öffnen | Click: OpenRFiDGearSettings_Click |
| MainWindow.xaml | PageSettings | Button | Einstellungen speichern | Click: SaveSettings_Click |
| MainWindow.xaml | PageSettings | Button | Projektbibliothek neu laden | Click: RefreshProjects_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Window |  | Closing: Window_Closing |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ProfileId |  | TextChanged: Dirty_TextChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ProfileCode |  | TextChanged: Dirty_TextChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ProfileVersion |  | TextChanged: Dirty_TextChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ProfileName |  | TextChanged: Dirty_TextChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ProfileDescription |  | TextChanged: Dirty_TextChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Prüfen | Click: Validate_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Speichern | Click: Save_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Schließen | Click: Close_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Neu | Click: NewVariable_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Löschen | Click: DeleteVariable_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariablesGrid |  | SelectionChanged: VariableSelectionChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableId |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableLabel |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableScope |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableType |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableLength |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableAllowed |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableMin |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableMax |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | VariableDefault |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Variable übernehmen | Click: ApplyVariable_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Application neu | Click: NewApplication_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Application löschen | Click: DeleteApplication_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationsGrid |  | SelectionChanged: ApplicationSelectionChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationId |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationName |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationKeyType |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationKeyCount |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | ApplicationChangeMode |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Application übernehmen | Click: ApplyApplication_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Datei neu | Click: NewFile_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Datei löschen | Click: DeleteFile_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FilesGrid |  | SelectionChanged: FileSelectionChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileId |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileNameText |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileType |  | SelectionChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileSize |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileCommunication |  | SelectionChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileRead |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileWrite |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileReadWrite |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileChange |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileHasCreation | Abweichende initiale FileSettings für Mastering verwenden (erzeugt ChangeFileSettings) | Checked: FileHasCreationChanged, Unchecked: FileHasCreationChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Finale Einstellungen als Ausgangspunkt übernehmen | Click: CopyFinalToCreation_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | CreationCommunication |  | SelectionChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | CreationRead |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | CreationWrite |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | CreationReadWrite |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | CreationChange |  | TextChanged: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileZeroFill | Beim Mastering mit Nullen initialisieren | Checked: FileSettingsPreviewChanged, Unchecked: FileSettingsPreviewChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | FileCodingOutput |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Datei übernehmen | Click: ApplyFile_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Baustein neu | Click: NewRecipe_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Löschen | Click: DeleteRecipe_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Nach oben | Click: RecipeUp_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Nach unten | Click: RecipeDown_Click |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeGrid |  | SelectionChanged: RecipeSelectionChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeBytePreview |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeOperation |  | SelectionChanged: RecipeOperationChanged |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeSource |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeValue |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipeWidth |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipePadLeft |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | RecipePadRight |  |  |
| ProfileEditorWindow.xaml | ProfileEditorWindow.xaml | Button | Baustein übernehmen | Click: ApplyRecipe_Click |
| ProfileHistoryWindow.xaml | ProfileHistoryWindow.xaml | HistoryGrid |  | SelectionChanged: HistoryGrid_SelectionChanged |
| ProfileHistoryWindow.xaml | ProfileHistoryWindow.xaml | DiffGrid |  |  |
| ProfileHistoryWindow.xaml | ProfileHistoryWindow.xaml | NewVersion |  |  |
| ProfileHistoryWindow.xaml | ProfileHistoryWindow.xaml | RestoreButton | Als neue Version wiederherstellen | Click: Restore_Click |
| ProfileHistoryWindow.xaml | ProfileHistoryWindow.xaml | Button | Schließen | Click: Close_Click |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ApplicationsGrid |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | IssuesGrid |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ProposalId |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ProposalCode |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ProposalVersion |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ProposalName |  |  |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | Button | Importbericht öffnen | Click: OpenReport_Click |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | ReviewAcknowledgement | Befunde gelesen; fehlende Kodierrezepte werden im Profil-Editor bewusst geprüft | Checked: ReviewAcknowledgement_Changed, Unchecked: ReviewAcknowledgement_Changed |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | CreateProposalButton | Auswahl im Profil-Editor prüfen | Click: CreateProposal_Click |
| RfPrjImportWindow.xaml | RfPrjImportWindow.xaml | Button | Abbrechen | Click: Close_Click |
| TutorialWindow.xaml | TutorialWindow.xaml | StepsList |  | SelectionChanged: StepsList_SelectionChanged |
| TutorialWindow.xaml | TutorialWindow.xaml | OpenAreaButton | Diesen Bereich öffnen | Click: OpenArea_Click |
| TutorialWindow.xaml | TutorialWindow.xaml | PreviousButton | Zurück | Click: Previous_Click |
| TutorialWindow.xaml | TutorialWindow.xaml | NextButton | Weiter | Click: Next_Click |

## Dynamisch erzeugte Bedienelemente

- `src/RFiDGear.Builder.App/ApplicationInspectionDialog.cs:45`: `var cancel = new Button { Content = "Abbrechen", MinWidth = 100, Margin = new Thickness(4) }; cancel.Click += (_, _) => DialogResult = false;`
- `src/RFiDGear.Builder.App/ApplicationInspectionDialog.cs:46`: `var start = new Button { Content = "Nur-Lese-Analyse starten", MinWidth = 190, Margin = new Thickness(4), FontWeight = FontWeights.SemiBold }; start.Click += (_, _) => Confirm();`
- `src/RFiDGear.Builder.App/MainWindow.xaml.cs:2044`: `checkBox.Checked += EditorProductiveControl_Changed;`
- `src/RFiDGear.Builder.App/MainWindow.xaml.cs:2045`: `checkBox.Unchecked += EditorProductiveControl_Changed;`
- `src/RFiDGear.Builder.App/ProjectEditor.cs:1120`: `ok.Click += (_, _) =>`
- `src/RFiDGear.Builder.App/TeamVaultWindow.cs:30`: `identityList.SelectionChanged += (_, _) => UpdateStatus();`
- `src/RFiDGear.Builder.App/TeamVaultWindow.cs:31`: `recipientList.SelectionChanged += (_, _) => UpdateStatus();`
- `src/RFiDGear.Builder.App/TeamVaultWindow.cs:88`: `var button = new Button { Content = text }; button.Click += click;`
- `src/RFiDGear.Builder.App/TransponderFormatDialog.cs:40`: `cancel.Click += (_, _) => DialogResult = false;`
- `src/RFiDGear.Builder.App/TransponderFormatDialog.cs:42`: `confirm.Click += (_, _) => Confirm();`
- `src/RFiDGear.Builder.App/VaultAccessWindow.cs:79`: `private static Button Button(string text, RoutedEventHandler click) { var button = new Button { Content = text }; button.Click += click; return button; }`
- `src/RFiDGear.Builder.App/VaultAccessWindow.cs:156`: `ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; };`

## CLI-Befehle

- `validate-project`
- `sanitize-project`
- `generate-key`
- `inspect-rfprj`
- `migrate-acceptance-project`
- `build-support-projects`
- `build-main-projects`
- `build-contract-artifacts`
- `preflight-output`
- `preflight-operational`
- `preflight-artifacts`
- `export-task-map`
- `export-coding-specification`
- `export-card-report-pdf`
- `inspect-rfidgear-version`
- `inspect-rfidgear-capabilities`
- `snapshot-rfidgear-log`
- `diagnose-rfidgear-log`
- `compare-rfprj`
- `acceptance-report`
- `card-preflight-evaluate`
- `test-compatibility-matrix`
- `build-payloads`
- `vault-protect`
- `vault-unprotect`
- `audit-project-vault`

## Startparameter der Anwendung

- `--hardware-smoke-test`
- `--productive`
- `--self-test`
- `--station`
- `--teststation-hardware-acceptance`
- `--ui-smoke-test`

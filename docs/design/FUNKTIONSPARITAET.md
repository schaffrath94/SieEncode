# Verbindlicher Erhalt der RC15-Funktionen

Stand: 06.09.2026. Quelle: Tag `rc15-source-baseline`.
Status der ursprünglichen Tabelle: Bestandsaufnahme und Zielzuordnung. Umsetzung
inzwischen teilweise erfolgt; vollständige praktische Abnahme steht weiterhin aus.
Aktueller Nachweisstand: [Release-Status 09.09.2026](RELEASE-STATUS-2026-09-09.md).

## Abnahmeregel

Keine bestehende Funktion wird durch die Neugestaltung entfernt. Vorhandene
Prüfungen, Grenzen und Fehlerzustände bleiben wirksam. Neue Funktionen kommen
zusätzlich hinzu. Eine kürzere Normalansicht ist keine Kürzung des Funktionsumfangs.
Der Expertenmodus ist eine Darstellungswahl, keine zusätzliche Berechtigung.

Die technische Inventur enthält 286 XAML-Aktions-/Eingabeflächen, 12 dynamische
Ereignisbindungen, 146 konventionell benannte UI-Handler, 26 CLI-Befehle und
6 Startparameter. Diese Zahlen sind keine Anzahl unabhängiger Fachfunktionen.
Vollständige Einstiegsliste: RC15-BEDIENINVENTUR.md und rc15-inventory.json.

## Fachliche Zuordnung

Alle Zeilen: in RC15 vorhanden, Ziel festgelegt, vollständige Neuumsetzung noch nicht abgenommen.

| ID | Bestehender Funktionsumfang | Normalmodus / Ziel | Expertenmodus / Ergänzung | Maßgebliche Quelle |
|---|---|---|---|---|
| P01 | Projektbibliothek, Suche, Aktualisieren, Auswahl | Projekte / Bibliothek | Profil-, Build- und Pfadspalten | MainWindow.xaml |
| P02 | Neu, bearbeiten, zwischenspeichern, speichern unter, schließen | Projekte / Editor | Quellpfad und Ausgabeziele | ProjectEditor.cs |
| P03 | Altes JSON und gebaute Altprojekte migrieren | Projekte / Import | Herkunft und Einschränkungen | ProjectEditor.cs; LegacyProjectMigrationService |
| P04 | Projekt löschen, Vault und Artefakte erhalten | Projekte / Verwaltung | Betroffene Dateien vor Bestätigung | ProjectDeletionPage.cs |
| P05 | Grundprofil wählen, Zusatzprofile kombinieren, Version binden | Projekte / Profile | Fingerprints, Bindungsstatus, Konflikte | ProjectEditor.cs |
| P06 | Neues Grund-/Zusatzprofil, bearbeiten, duplizieren, exportieren, JSON importieren | Projekte / Profile | Vollständiger Profileditor | ProfileManagement.cs |
| P07 | rfPrj-Analyse und kontrollierter Grund-/Zusatzprofilimport | Projekte / Import | Vorschau, erkannte Grenzen und Bericht | RfPrjImportWindow; ProfileManagement.cs |
| P08 | Profilhistorie, historische Version wiederherstellen, Ordner öffnen | Projekte / Versionen | Historie und Quelldateien | ProfileHistoryWindow |
| P09 | AIDs, Dateitypen, Größen, Kommunikation, alle Rechte, Key-Settings | Projekte / Kartenstruktur mit verständlichen Begriffen | Vollständige technische Editorfelder | ProfileEditorWindow |
| P10 | Datenvariablen, Formate, Rezepte, Reihenfolge, HEX-/Bytevorschau | Projekte / Daten | ASCII/UTF-8/BCD/Integer/HEX/Padding und Offsets | ProfileEditorWindow; PayloadEngine |
| P11 | Projektvalidierung, Build, technische Buildziele | Projekte / Prüfen und bauen | Mastering/Kodierumfang, Ausgabeordner | ProjectEditor.cs |
| P12 | Analyse/Dry-Run aus Editor, Bibliothek oder Datei; Taskplan | Projekte / Prüfung | Bedingungen, Keyreferenzen, vollständige Tasktabellen | AnalysisPage.cs |
| P13 | Text-/JSON-Berichte und vorhandener PDF-Sollreport | Projekte / Berichte | Technischer Vollbericht zusätzlich; bestehende Exporte erhalten | AnalysisPage.cs |
| P14 | Projekt-/Profilvergleich und rfPrj-Vergleich | Projekte / Vergleichen | Semantische technische Änderungen | DiffPage.cs |
| V01 | Mehrere Ausgangsschlüsselsätze wählen, hinzufügen, löschen | Editor / Schlüsselzugriff | Typen und explizite sitzungsgebundene Werteansicht | ProjectEditor.cs |
| V02 | Produktivschlüssel erzeugen, Vault entsperren und verschlüsselt speichern | Editor / Vault | Einzelbenutzerformat, manuelle Werteprüfung, technisches Audit | ProjectEditor.cs |
| V03 | Windows-Identitäten, Einladungsexport/-import, Teamliste | Einstellungen / Team-Vault | Empfängerfingerprints und Recovery | TeamVaultWindow; WindowsVaultIdentityService |
| V04 | Projektbezogenen Mehrbenutzerzugriff bearbeiten | Editor / Zugriffsverwaltung | Empfänger- und Recovery-Nachweise | VaultAccessWindow |
| V05 | Unabhängige Vaultsicherung, Wiederöffnung und Keyset-Zuordnung | Geführte Einrichtung / Sicherungsstatus | Manifest, Wiederherstellungsnachweis | VaultBackupService; ProjectEditor.cs |
| K01 | Projekt in Kodierstation laden, Schnellstart ohne AUTORUN | Karten kodieren / Projekt wählen | Konkretes rfPrj kontrolliert in Runtime öffnen | CodingQuickStartPage.cs |
| K02 | Projekt-Preflight, Reparaturhinweis und Neubau | Karten kodieren / Bereitschaft | Artefakt-, Hash- und Capabilitybefunde | MainWindow.xaml.cs |
| K03 | Kartendaten erfassen, Payload vorbereiten, einzelne Karte kodieren | Karten kodieren / geführter Ablauf | Taskplan und Sollbytes | MainWindow.xaml.cs |
| K04 | Serienwarteschlange, Zähler, Verlauf, Dublettenschutz und Abschluss | Karten kodieren / Serienbetrieb | Queue und schlüsselfreie Laufnachweise | CodingSeriesService; MainWindow.xaml |
| K05 | Mastering und additiver Erweiterungsmodus | Karten kodieren / Kartenvorbereitung | Vollständiger Plan, betroffene AIDs, Bedingungen | MainWindow.xaml.cs |
| K06 | Format-Recovery und gezielter Erweiterungsrückbau | Verständlicher Wartungszugang | Getrennte Zielprüfung und bisherige Bestätigungen | RFiDGearOperations; MainWindow.xaml |
| K07 | Ausgangs-/Produktivprüfung, Readbacks und Ergebnisbewertung | Ergebnis und nächster Schritt | Bytevergleich, RunId, vollständige Taskoutcomes | CardPreflightService; CodingRunEvaluator |
| K08 | Laufstatus aktualisieren, Logüberwachung, kontrollierter Prozessabschluss | Laufstatus / Abschluss | Diagnose, Prozessdetails und Pfade | MainWindow.xaml.cs |
| A01 | Projektlose Karteninventur, Standardproben, Kartenbaum | Karten untersuchen / Lesen | PICC-Rechte und strukturierte Belege | MainWindow.xaml.cs |
| A02 | Application mit AID, Keytyp/-nummer und Sitzungsschlüssel vertieft lesen | Karten untersuchen / geschützter Bereich | Vollständige Metadaten, File-IDs und Grenzen | ApplicationInspectionDialog |
| A03 | Standard-/Backup-Inhalte soweit möglich als HEX/ASCII anzeigen | Karten untersuchen / Dateiinhalte | Offsets und gelesener Umfang | ApplicationInventoryReportService |
| A04 | Projektstrukturvergleich, Ausgangs-/Produktivfiles und Application-Einstellungen prüfen | Karten untersuchen / optionales Projekt | Technischer Vergleich und einzelne Prüfergebnisse | MainWindow.xaml |
| A05 | Letzten Bericht öffnen und projektlos formatieren | Analysebericht; Wartung separat | Formatiervorgang mit Sitzungsschlüssel und Bestätigungen | TransponderFormatDialog |
| S01 | Runtimepfad, Reader, Capabilityprüfung, Bibliothek und Einstellungen speichern | Einstellungen | Capabilitymatrix und Binärnachweise | MainWindow.xaml |
| S02 | Hilfe, Tour, Dokumente und Diagnose-/Funktionsprüfung | Hilfe in allen Bereichen | Werkzeuge und Diagnosedateien | HelpPage; TutorialWindow |
| S03 | Hardware-Abnahmeplan, Evidenzbericht, Abnahmeauswertung und Pilot-Werkzeuge | Hilfe / Abnahme | Vollständige Evidenz und Zuordnung | HardwareAcceptancePage; AcceptancePage |
| S04 | 26 CLI-Befehle, 6 Startparameter, Selbsttest-/Smoke-Einstiege | Unverändert unterstützt | Automatisierungsdokumentation | CLI Program.cs; App.xaml.cs |

## Was neu ist und nicht als bereits vorhandene Funktion gilt

- PDF-Istreport aus der aktuellen Transponderanalyse.
- PDF-Soll/Ist-Vergleich; Übersicht und technischer Vollbericht unabhängig vom UI-Modus.
- Neue konsolidierte Expertenansichten für Laufplan, Daten und Diagnose.
- Später: vollständige Chip-/Versionsdaten, Value-/Record-Unterstützung, formaler Host.

Der PDF-Sollreport, Taskpläne, Readbacks und tiefere Inventur existieren schon.
Deren Neuanordnung wird nicht als neue Funktion gezählt. Value-/Record-Unterstützung
und noch nicht lesbare Chipmerkmale bleiben ausdrücklich als nicht unterstützt/
nicht geprüft gekennzeichnet.

## Nachweis pro Funktion

Für jede Zeile bei der Umsetzung ergänzen: neue Ansicht/Handler, Softwaretest,
installierter Bediennachweis, ggf. Reader-/Transpondernachweis und exakte Buildhashes.
Bis alle notwendigen Nachweise vorliegen lautet der Status **nicht abgenommen**.
Reine XAML-/Handleranwesenheit ersetzt weder Erreichbarkeit noch korrektes Verhalten.

Besonders prüfen: bestehende Projekte/Profile/Vaults, gebundene alte Profilstände,
Artefakt-Altprojekte ohne Vault, ungespeicherte Änderungen beim Modus-/Bereichswechsel,
doppelte Serienkarten, abgebrochene Läufe, fehlende Berechtigungen, negative
Kontrollflussprüfungen, Keywechsel-/Recovery-Gates und unverändert schlüsselfreie Logs.

## Werkzeuge

Der Regressionstest `UiWiringParity.Verify` prüft zusätzlich alle 286 gespeicherten
XAML-Flächen gegen die aktuellen Dateien: Elementtyp, vorhandenen Namen, jedes
gespeicherte Ereignis und den Dispatch-Tag. Mehrfach vorhandene Aktionen werden
einzeln gezählt. Beschriftungen und Anordnung dürfen sich ändern. Drei explizite
Zuordnungen gelten in MainWindow.xaml: NavProjectsButton, NavStationButton und
NavTestStationButton verwenden statt Navigate_Click jetzt Workspace_Click.
Dieser ruft Navigate mit der letzten Unterseite des jeweiligen Arbeitsbereichs
auf; der WPF-Smoke prüft den Erhalt von Verlauf und ungespeichertem Editor.
Negative
Kontrollen entfernen eine Click-Bindung/ein Feld oder ändern einen Dispatch-Tag
in einer Speicherkopie; alle drei müssen erkannt werden. Der Test läuft durch
`Test-CI.ps1` auch in GitHub CI, ohne Reader oder zusätzliche Python-Abhängigkeit.
Stand 11.09.2026: keine fehlende XAML-Verdrahtung erkannt. Dies bestätigt weder
Laufzeit-Sichtbarkeit/Erreichbarkeit noch dynamische Bindungen, Fachverhalten oder
Hardwarefreigabe; dafür bleiben die Bedien- und Hardwareabnahmen erforderlich.

`python tools/inventory_rc15.py --check` prüft die gespeicherte Inventur gegen
den unveränderten Tag und meldet fehlende benannte UI-/CLI-/Start-Einstiege.
Absichtliche Umbenennungen benötigen eine geprüfte Zuordnung; sie sind keine
automatische Freigabe. Dynamische Lambdaaktionen sind erfasst und manuell zu prüfen.

Die Quellinventur ist kein Beleg für einen bitidentischen RC15-Neubau.
Hardwaretests werden in diesem Konzeptschritt nicht gestartet.

## Dynamische Kartendialoge – Softwarecheck 11.09.2026

ApplicationInspectionDialog und TransponderFormatDialog besitzen Scrollzugang
und eindeutige Automation-IDs. Der WPF-Smoke preview.10 prüft beide tatsächlichen
Start-/Vorbereitungs-Clickbindungen mit leerer Schlüsseleingabe: Validierung wird
angezeigt, Request bleibt null. Die Aktionsleiste ist bei 540x180 Inhaltsfläche
nach Scrollen erreichbar. Das ersetzt keine positive Hardwareausführung, Cancel-
Abnahme, Prüfung der übrigen dynamischen Bindungen oder reale DPI-Abnahme.
Nachweis: tmp/dialog-smoke/SieEncode/ui-smoke/ui-smoke-result.json (lokal).

### Team-/Vaultdialoge und Expertennavigation – preview.11

Synthetische öffentliche RSA-3072-Einladung, temporäre Teamliste, leerer interner
Identitätsprovider: tatsächliche Entfernen-Clicks in TeamVaultWindow und
VaultAccessWindow ändern nur den Entwurf. Gespeicherte Teamliste unverändert,
keine bestätigten Empfänger ohne Freigabe. Beide Aktionsleisten bei 640x220
Inhaltsfläche erreichbar, Automation-IDs eindeutig. Windows-Zertifikatsspeicher
nicht verwendet. Expertenzugänge Profile/Taskplan/Vergleich/Abnahme über Click
und sichtbare Vorfahren geprüft. Vollständiger WPF-Smoke bestanden:
tmp/vault-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json (lokal).
Positive Speicher-/Import-/Exportabläufe, Cancel, Identitätsauswahl und praktische
Zweitbenutzer-/Hardwareabnahme bleiben offen; keine vollständige Paritätsfreigabe.

### Editorbindungen und Abbrechen – preview.12

Alle vier PICC-Rechteschalter mit Checked und Unchecked am synthetischen Editor
geprüft: Modell, Änderungsmarkierung und Zurücksetzen der Vaultprüfzustände.
ApplicationInspectionDialog, TransponderFormatDialog, TeamVaultWindow und
VaultAccessWindow modal geöffnet und über Abbrechen geschlossen: keine Anfrage,
Freigabe oder Teamspeicherung. IsCancel-Zuordnung geprüft; kein physischer Tastentest.
Paket-EXE aus Commit 24890ce bestand den vollständigen WPF-Smoke.
TextEntryPrompt/PassphrasePrompt, Identitätsauswahl und positive Dateidialogabläufe
bleiben offen. Paketnachweis: UI-PREVIEW-PACKAGE-2026-09-11.md.

### Vault-Absturzkorrektur – preview.13

Benutzermeldung durch Windows-Ereignis 1026 (Invalid Handle in AddSelectedIdentity)
und synthetische Reproduktion bestätigt. Auswahl/ItemsSource werden in Team- und
Projekt-Vault vor Freigabe der alten Zertifikate gelöst. Dreimaliges Neuladen und
Hinzufügen pro Dialog bleibt gültig und ohne doppelte Empfänger. Team-Speicherkonflikt
zeigt Fehler ohne Überschreiben; nach erneutem Öffnen ist Speichern erfolgreich.
Paket-EXE-WPF-Smoke bestanden; keine echten Zertifikate/Vaults geändert.
Positive Neuanlage einer Windows-Identität und Benutzer-Bedienabnahme weiter offen.

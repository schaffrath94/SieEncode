# Migrationsvertrag von v10.39

`11.0-preview46` verändert keine Projektdefinitionen, Profile, Vaults, rfPrj-
Dateien, Readbacks oder Berichte automatisch. Beim Eintritt in eine neue
Kodiersitzung, bei ungültigen Sitzungswerten, nach Datensatzabschluss und beim
sicheren Schließen entfernt es ausschließlich die in `coding.schema.json`
aufgeführten Dateien unter dem jeweiligen `payloads`-Ordner. Der keyfreie
Status `payloads/.rfidgear-payload-state.json` dokumentiert `Prepared` oder
`NotPrepared`; er enthält keine Payloadbytes und keine Schlüssel.

Preview46 ergänzt ausschließlich den exakten hardwaregeprüften Fork-Build
`RFiDGear Builder Runtime 2.0.10.1` im Builder-Vertrauenskatalog. Selbst ein
Binary mit vollständigen Nachauthentifizierungsmarkern bleibt gesperrt, wenn
Produktversion oder EXE/DLL-Evidenzfingerprint abweichen. Bestehende Projekte,
Vaults und rfPrj werden weder migriert noch verändert; Mastering, reine
Leseprüfungen und Format-Recovery bleiben davon unberührt.

Preview44 ergänzt bei einem bewussten Coding-Projektbuild ausschließlich
`CheckFileSettings`-Aufgaben im getrennten
`*_Acceptance_Application_Productive.rfPrj`. Mastering, Kodierung, beide
Karten-Preflights und beide Recoveries bleiben unverändert. Der neue
Kartenstruktur-Lauf ist nur mit positiv nachgewiesener `FileSettingsCheck`-
Capability startbar. Vorhandene rfPrj, Projektdefinitionen, Vaults und
Abnahmeevidenzen werden nicht automatisch umgeschrieben; Evidenzschema 1.1
bleibt lesbar, neue vollständige Evidenz verwendet 1.2.

Preview43 ergänzt ausschließlich einen binär nachgewiesenen Capabilitywert für
den autoritativen RFiDGear-TaskLoop-Terminalstatus. Projektdefinitionen, rfPrj,
Host-Requests und bestehende Resultate werden weder migriert noch umgeschrieben.
Ohne diese neue Upstreamfähigkeit bleibt die bisherige vollständige
Task.Outcome-Auswertung des Builders aktiv.

Preview39 ergänzt bei einem erneuten Projektbuild ausschließlich den
schlüsselfreien `rfidgear.host-contract.json` und legt beim RFiDGear-Start
korrelierte Request-/Result-Dateien unter `integration/host-v1/runs` an.
Bestehende Projekte bleiben lesbar und werden nicht automatisch neu gebaut.

Preview40 ergänzt bei einem bewussten Coding-Projektbuild das getrennte
`*_Acceptance_Application_Productive.rfPrj`. Die sechs bestehenden rfPrj-Dateien
bleiben unverändert. Das neue Projekt wird nur mit nachgewiesenem Exact-
`ReadAppSettings` und `CheckAppKeyCount` startbar; alte RFiDGear-Versionen können
die übrigen Projekte weiterhin verwenden.

Vom Builder gestartete RFiDGear-Prozesse müssen ab preview34 vor dem Schließen
des Builders oder dem Wechsel zu einer anderen Kodierstation beendet sein. Ein
AUTORUN-Lauf muss außerdem zuerst einen terminalen Erfolg, Fehler oder Timeout
erreichen. Der Builder beendet RFiDGear nicht eigenmächtig und stoppt bei einem
abgebrochenen Schließversuch seine Logüberwachung nicht.

Ab preview35 werden `DesfireDataFilePath` und `DesfireReadDataFilePath` jedes
rfPrj-Tasks im keyfreien Taskbericht sichtbar. Der vollständige Preflight und
die Karten-/Mastering-/Recovery-Startpfade verlangen projektlokale Ziele. Damit
werden insbesondere kopierte Alt-/Diagnoseprojekte mit absoluten Pfaden nicht
mehr unbemerkt in einen früheren Ordner schreiben. rfPrj-Dateien selbst werden
dabei nicht umgeschrieben. Ein fensterlos verbliebener, terminal abgeschlossener
RFiDGear-Prozess kann über den neuen bestätigten Abschlussweg beendet werden.

Ab preview38 verhindert eine zusätzliche Laufzeitsperre das Öffnen oder Starten
eines Kodierprojekts mit Keywechsel, wenn RFiDGear keine verifizierte
Keywechsel-Implementierung binär nachweist. Bestehende Projekte und rfPrj-Dateien
werden nicht verändert; nur der unsichere Kartenstart wird verweigert.

Ab preview36 erzeugt ein neuer Build die für Mastering vorgesehenen
Nullinitialisierungsdateien unter `initialization` neu. Bestehende rfPrj-,
Projekt- und Vaultdateien werden nicht migriert. Vor Mastering und anderen
Kartenstarts müssen referenzierte Eingabedateien existieren, gültiges Hex
enthalten und exakt der im Task deklarierten Dateigröße entsprechen. Fehlende
Kodier-Payloads bleiben beim reinen Build zulässig und werden erst unmittelbar
vor dem Kartenstart verbindlich geprüft.

**Kodiersession starten** öffnet RFiDGear ohne `AUTORUN` und ohne `AUTOMODE`.
Beim Öffnen werden weder Payloads erzeugt noch Karten-/Readeraktionen ausgelöst.
Bereits vorhandene Payloads einer früheren Sitzung werden dabei bewusst
entfernt, damit ein manueller RFiDGear-Lauf keine alten Kartendaten verwenden
kann.
Die Payloadbereitstellung bleibt eine nachgelagerte bewusste Enter-Aktion. Bei
reinen Bauartefakten oder einem fehlerhaften vollständigen Projekt-Preflight
bleibt der Ein-Schritt-Start gesperrt; **Nur Kodierstation laden** bleibt für
bewusste Altprojektarbeit verfügbar.

Der Hardware-Abnahmeplan liest Projekt-Preflight, gebaute Aktionsartefakte,
RFiDGear-Binärfähigkeiten und Reader-Konfiguration. Er startet weder RFiDGear
noch Reader oder Karte und verändert keine Projekt-, Profil-, Vault- oder
Builddatei. Keyfreie Berichte liegen unter
`%APPDATA%\RFiDGear-Projektbuilder\reports\hardware-acceptance`.

Die neue Aktion **Projektdefinition löschen** entspricht dem v10.39-Vertrag
und entfernt ausschließlich die ausgewählte bearbeitbare JSON-Quelldatei. Vor
der Bestätigung zeigt sie den exakten Löschpfad sowie den ausdrücklich erhaltenen
Vault- und Buildpfad. Gebaute `project.definition.json`, rfPrj-Dateien,
Payloads, Berichte und Vaults werden nicht gelöscht. Änderungen der Quelldatei
zwischen Vorschau und Bestätigung brechen die Aktion konfliktgeschützt ab.

Der neue Umgebungstest liest ausschließlich Pfade, Dateiexistenz, Profile,
Task-Shells, RFiDGear-Binärmarker, Reader-Konfiguration und Logverzeichnis. Er
startet keine Programme und greift nicht auf Reader oder Karte zu. Seine
keywertfreien Berichte liegen versionsunabhängig unter
`%APPDATA%\RFiDGear-Projektbuilder\reports\environment`.

Das Duplizieren eines Kundenprofils erzeugt immer einen neuen ungespeicherten
Entwurf mit eigener ID und ohne Historienmetadaten. Der Kodiervorschriftenexport
liest Profile ausschließlich und verändert weder Profil noch Projekt oder Vault.

Beim ersten konfliktgeschützten Speichern kann neben einer Projekt-, Profil-
oder Vaultdatei eine leere Datei mit der Endung `.rfidgear-builder.lock`
entstehen. Sie enthält keine Nutz-, Karten- oder Schlüsseldaten und gehört nicht
zum Dateiformat. Auf gemeinsam verwendeten Ordnern koordiniert sie exklusive
Speichervorgänge zwischen Rechnern. Sie darf bestehen bleiben und wird von der
Projektbibliothek ignoriert.

Wird die bearbeitete Quelldatei nach dem Öffnen extern verändert, speichert
preview25 und neuer nicht darüber. Die aktuelle Fremdfassung bleibt erhalten; der
Benutzer muss neu laden oder bei Projekten bewusst „Speichern unter“ verwenden.

Gebaute Projektordner erhalten ab preview26 zusätzlich die leere Datei
`.rfidgear-builder.workspace.lock`. Sie verhindert, dass zwei Builderinstanzen
gleichzeitig Buildartefakte oder Karten-Payloads desselben Projekts verändern.
Die Datei darf nach Programmende bestehen bleiben; nur der exklusive
Betriebssystem-Handle kennzeichnet eine aktive Belegung. Ein abgestürzter
Prozess hinterlässt deshalb keine dauerhaft „aktive“ Sperre.

Die neue RFiDGear-Fähigkeitsprüfung liest die konfigurierte Programmdatei und
eine vorhandene benachbarte `RFiDGear.dll`, führt sie aber nicht aus und ändert
keine Installation. Fehlt ein für eine Aktion notwendiger Marker oder ist die
Binärdatei nicht lesbar, bleibt nur diese Aktion gesperrt. Bestehende Projekte,
Profile, Vaults und Einstellungen werden dabei nicht migriert.

Der neue `.rfPrj`-Import ist kein automatischer Roundtrip und keine
Kodierfreigabe. Die Quelldatei wird nur gelesen. Aus Taskfeldern sicher
ableitbare Kartenstruktur wird als Vorschlag gezeigt; widersprüchliche Evidenz
sperrt die Übergabe. Externe Payloaddateien, Variablenregeln und ursprüngliche
Kodierrezepte lassen sich aus einem rfPrj nicht zuverlässig zurückgewinnen und
werden deshalb nicht erfunden. Nach bestätigtem Import öffnet sich immer erst
der normale Kundenprofil-Editor. Ohne dessen Prüfung und Speichern entsteht kein
aktives Profil.

Importberichte liegen versionsunabhängig unter
`%APPDATA%\RFiDGear-Projektbuilder\reports\rfprj-import`. Sie enthalten keine
Schlüssel- oder Payloadwerte. Bereits strukturell in der Profilbibliothek
vorhandene Applications werden erkannt und standardmäßig nicht erneut in den
Kundenprofil-Entwurf aufgenommen.

Vorhandene `*.project.json` ohne `Profiles.Bindings` bleiben vollständig lesbar,
bearbeitbar und speicherbar. Ein Build ist bewusst gesperrt, bis im
Projekteditor **Aktuelle Profilstände verbindlich übernehmen** bestätigt wurde.
Erst dann ergänzt der Builder für jedes gewählte Profil Version und
SHA-256-Inhaltsfingerabdruck. Weicht später Version oder Inhalt ab, bleibt der
Build erneut gesperrt; die Projektdatei wird niemals still migriert.

Bestehende Kundenprofile ohne Historienmetadaten bleiben gültig. Beim ersten
fachlich geänderten Speichern muss eine neue Profilversion vergeben werden. Der
vorherige Stand wird unter dem versionsunabhängigen Benutzerpfad
`%APPDATA%\RFiDGear-Projektbuilder\profiles\history\<Profil-ID>` archiviert.
Der aktive Profilordner enthält weiterhin nur genau einen aktuellen Stand je ID,
sodass ältere Builder und die Profilkomposition nicht durch Archivkopien gestört
werden.

Archivierte Profile werden niemals direkt wieder zum aktuellen Stand kopiert.
Der Historienbrowser prüft Profil-ID, Typ, Schema und SHA-256-Fingerabdruck,
zeigt anschließend den vollständigen fachlichen Vergleich und verlangt eine
neue, historisch noch nicht verwendete Versionsnummer. Die Wiederherstellung
archiviert zuerst den bisherigen aktuellen Stand. Bereits gebundene Projekte
bleiben absichtlich am alten Fingerabdruck und müssen separat bewusst migriert
werden.

Der neue Dry-Run baut keine temporären `.rfPrj`-Dateien. Er verwendet für die
Ablaufsimulation ausschließlich synthetische, flüchtige Credential-Tokens und
schreibt neben der Projektdefinition nur die keyfreien Berichte
`Projektanalyse.txt` und `Projektanalyse.json`. Gebaute Altprojekte werden vor
der Analyse über denselben streng geprüften, unverändernden Mastering-
Migrationspfad wie im Projekteditor geführt.

Der Projekt-/Profilvergleich ist ebenfalls rein lesend. Bei alten gebauten
Projekten verwendet er den vorhandenen streng validierten Migrationspfad nur
zur Ermittlung der Profilkomposition im Speicher; Quelldateien und Vaults
bleiben unberührt. Die Berichte `Profilvergleich.txt/.json` enthalten keine
Schlüsselwerte.

Im Profil-Editor wird eine fehlende `masteringInitialization` jetzt korrekt als
Standard-`zeroFill` dargestellt. Erst bewusstes Abwählen speichert den bereits
seit v10.39 unterstützten expliziten Wert `type = none`. Beim bloßen Öffnen
werden keine Profile verändert; die kanonische Darstellung wird erst beim
bewussten Übernehmen und Speichern einer Datei geschrieben.

Neu verfügbare Betriebsaktionen verwenden vorhandene v10.39-Artefakte
unverändert. Reine Bauartefakte dürfen weiterhin manuell geöffnet und mit den
strikt lesenden Karten-Preflight-Projekten geprüft werden. Sie können außerdem
nach vollständigem Profil-/Layoutvergleich als neuer keyfreier Mastering-
Entwurf migriert werden. Am unveränderten Artefakt bleiben automatisches
Mastering und Recovery gesperrt, weil ohne Quellkonfiguration kein
vollständiger Stale-/Profil-Preflight möglich ist.

Bereits nachgewiesen kompatibel:

- Aufbau von `*.project.json`
- Groß-/Kleinschreibung der bisherigen JSON-Felder
- relative Ausgabeordner bezogen auf den Projekte-Stammordner
- absolute Ausgabeordner
- externer Einstellungsort unter `%APPDATA%\RFiDGear-Projektbuilder`
- dauerhafte Kundenprofile unter
  `%APPDATA%\RFiDGear-Projektbuilder\profiles\customer`; gebündelte Profile
  bleiben unverändert und Benutzerprofile werden beim Laden zusammengeführt
- optionale Übernahme einer alten Einstellungsdatei in diesen versionsunabhängigen Ort
- rekursive Projektbibliothek unter dem eingestellten übergeordneten Ordner
- identische Projektpfadauflösung in Projektliste und Kodierstation
- getrennte Erkennung bearbeitbarer Projektdefinitionen und gebauter Altprojekte
- Key-Vault-Format `RFiDGear-Projektbuilder-KeyVault`, Version 1
- PBKDF2-SHA1 mit bisheriger Iterationszahl
- AES-256-CBC/PKCS7 und HMAC-SHA256
- Entschlüsselung in beiden Richtungen zwischen PowerShell v10.39 und C# 11.0
- vollständiges Profilformat einschließlich creationSettings, Access Rights,
  Mastering-Initialisierung und Kodierrezepten
- genau ein Masterprofil plus 0..n additive Kundenprofile
- AID- und Variablenkonflikte
- Projektwerte und Anzahl/Typ der Produktivkeys anhand der Profilkomposition
- alle Payload-Rezeptoperationen und das bisherige Hex-Dateiformat
- bytegleiche Payloads mit internen, nicht ausgelieferten Kompatibilitätsfixtures gegenüber v10.39
- alle elf Task-Shells und der `.rfPrj`-ZIP-Container
- Mastering, Kodierung, Transport-/Produktiv-Karten-Preflight und beide Recovery-Projekte
- semantisch identische Taskreihenfolge, Bedingungen und XML-Felder gegenüber v10.39
- alle sechs schlüsselwertfreien Vertrags- und Plan-Dateien; Abweichungen gibt es
  nur bei Zeitstempel und der absichtlich neuen Builder-Version
- Projekt-Preflight für Konfigurationsvertrag, Stale-Erkennung, Pflichtdateien,
  rfPrj-Manifestversion und Task-Abhängigkeitsgraph
- schlüsselfreier JSON-Taskplan für Diagnosepakete und Issues
- Launcher-Vertrag `CUSTOMPROJECTFILE="<vollständiger Pfad>"` und optional
  `AUTORUN=1` oder ab RFiDGear 2.0.7.0 `AUTOMODE=1`, inklusive gegenseitigem
  Ausschluss und Erkennung eines sofortigen Prozessabbruchs
- RFiDGear-2.0.7-Versionsbewertung, read-only Prüfung der aktiven
  Readerkonfiguration und inkrementeller Logsnapshot
- kompatible Fehlerkategorien, Schlüsselredaktion und Zuordnung von
  `CurrentTaskIndex` zum schlüsselfreien rfPrj-Taskplan
- sichere Laufentscheidung: starker Logfehler vor Readback-Erfolg; Erfolg erst
  nach neuen, vollständigen und bytegleichen finalen Verification-Dateien

Neu als bedienbare Vorschau migriert:

- WPF-Grundgerüst mit Übersicht, Projekte, Kodierstation, Profile, Abnahme und Einstellungen
- grafische Kundenprofilbearbeitung für Metadaten, Variablen, Applications,
  KeySettings, Files, Access Rights, Creation Settings, Mastering-
  Initialisierung und Kodierrezepte mit atomischem Speichern
- dauerhafte Navigation, sichtbarer Zurück-Knopf und Version in der Titelzeile
- Projektwahl aus der gemeinsamen Projektbibliothek, C#-Preflight und sicheres
  Öffnen ohne automatischen Kartenlauf oder bewusstes Starten des
  hardwaregetesteten RFiDGear-Serienmodus über `AUTOMODE=1`
- schema-basierte Sitzungseingaben für bearbeitbare und nur gebaut vorhandene Projekte
- bewusste Migration gebauter Projekte und älterer JSON-Konfigurationen als
  neue, ungespeicherte Mastering-Entwürfe; Quellen und produktive Keys bleiben
  unberührt, Profil-/Layoutabweichungen blockieren den Vorgang
- validierte Payload-Erzeugung ohne erneute Abfrage produktiver Schlüssel
- Sitzungs-Baseline für RFiDGear-Log und finale Readbacks sowie manuelle Abschlussprüfung
- neue und bestehende Projektdefinitionen strukturiert öffnen, bearbeiten,
  am vorhandenen Pfad atomar speichern, bewusst unter neuem Pfad speichern und schließen
- v10.39-kompatiblen Projekt-Key-Vault entsperren, produktive Keys in der Sitzung
  sichtbar halten, kryptografisch erzeugen und wieder verschlüsselt speichern
- vollständiger C#-Projektbuild aller Haupt-/Support-rfPrj- und Vertragsartefakte
  mit gemeinsamem Abschluss-Preflight
- v10.39-kompatibler `Letzte_Kodierung.json`-Nachweis aus der C#-Kodiersitzung
- Serienmodi Einzel/Fortlaufend/Liste, TXT-/CSV-Import, F8/F9, Retry/Skip,
  projektübergreifender Duplikatschutz und keywertfreie Sitzungsberichte
- geprüfte Mastering-, Karten-Preflight- und Recovery-Aktionen in der Kodierstation
- automatischer und manueller keywertfreier Diagnose-ZIP-Export mit
  freigegebener Dateiliste und Ausschluss von Projektdefinition, Vault und rfPrj
- echte Abnahmeauswertung `OK / Abweichung / Nicht prüfbar` aus
  `acceptance.plan.json` und aktuellen finalen Readbacks einschließlich
  keywertfreier TXT-/JSON-Berichte

Noch nicht migriert:

- Hardwareaktionen außerhalb der bereits angebundenen RFiDGear-Projekte

Bis diese Punkte vollständig implementiert und gegen v10.39 verglichen sind,
bleibt v10.39 die produktive Anwendung.

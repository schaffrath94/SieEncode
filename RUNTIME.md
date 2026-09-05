# RFiDGear-Runtime-Vertrag

`RFiDGear.Builder.Runtime` trennt Prozess- und Laufzeitauswertung vollständig
von einer späteren Bedienoberfläche.

## Versionierter Hostvertrag V1.1

`rfidgear.host-contract.json` beschreibt pro gebautem Projekt die verbindliche
Eigentumsgrenze, Operationen, Korrelation, Sicherheitsregeln und den aktuellen
Prozessadapter zum Zielvertrag `de.rfidgear.host.v1`. Bei jedem realen
Kodier-, Mastering-, Preflight-, Application-Abnahme- oder Recovery-Start
erzeugt der Builder einen unveränderlichen, projektlokalen Request unter
`integration/host-v1/runs` und aktualisiert das korrelierte Resultat von
`running` auf den finalen Enginezustand und die getrennte Consumer-Abnahme.

Taskplan und I/O-Pfade werden ohne Beschreibungen und ausschließlich relativ
serialisiert. SHA-256 bindet Taskarray und konkretes rfPrj an den Request.
Geordnete `Task.Outcome`-Felder werden vor der allgemeinen Hex-Redaktion
strukturell gelesen; persistiert wird von der RFiDGear-`RunId` ausschließlich
ein domänenseparierter SHA-256. Absolute Pfade,
Traversal, unbekannte oder fehlende Capabilities, wiederverwendete Request-IDs
und inkonsistente Resultate werden abgelehnt. Request und Resultat enthalten
weder Keywerte noch Payload-/Kartenbytes. Der referenzierte rfPrj-Container darf
weiterhin die für RFiDGear notwendigen Schlüssel tragen.

Die vollständige Feld- und Zustandsdefinition steht in
`docs/RFIDGEAR-HOST-CONTRACT-V1.md`. Der Builder normalisiert nur RFiDGears
Prozess-, strukturierte Log- und Readbackevidenz; Reader, Kartenpolling,
Taskausführung und Kartenkommandos verbleiben vollständig in RFiDGear. Die
maschinellen Draft-2020-12-Schemas liegen unter `docs/contracts`.

## Mehrbenutzer- und Konfliktschutz

Auch das Löschen einer bearbeitbaren Projektdefinition verwendet die persistente
Sidecar-Lockdatei und den beim Erstellen der Vorschau erfassten SHA-256-Stand.
Eine zwischenzeitlich veränderte Datei wird nicht gelöscht. Zusätzlich muss die
Quelldatei innerhalb der eingestellten Projektbibliothek liegen. Die Aktion
entfernt weder Vault noch Buildordner oder daraus erzeugte Artefakte.

Projektdefinition, Kundenprofil und Vault werden beim Öffnen vollständig mit
SHA-256 erfasst. Der Schreibpfad öffnet anschließend eine persistente leere
Sidecar-Lockdatei exklusiv, vergleicht den aktuellen Inhalt erneut mit dem
Ausgangsstand und ersetzt die Zieldatei erst danach atomar. Fehlende Dateien
besitzen ebenfalls einen eindeutigen Ausgangszustand, sodass auch eine
zwischenzeitliche Neuanlage als Konflikt erkannt wird.

Die Lockdatei enthält keine Anwendungsdaten und bleibt absichtlich bestehen:
Nur so verwenden alle Rechner auf einem gemeinsamen Dateisystem dauerhaft
denselben Sperrknoten. Profilvorgänger, Versionshistorie und neuer aktiver Stand
werden innerhalb einer gemeinsamen Sperre verarbeitet.

Für den gebauten Projektordner existiert zusätzlich genau eine
`.rfidgear-builder.workspace.lock`. Der vollständige Build und eine aktive
Kodiersitzung verwenden denselben exklusiven Handle und können sich daher weder
gegenseitig Artefakte ersetzen noch unterschiedliche Kartendatensätze in
denselben Payloadpfad schreiben. Die Kodiersitzung besitzt die Lease bis zu
einem terminal bewerteten oder bewusst abgebrochenen Datensatz. Die
Payloaddateien werden unmittelbar vor RFiDGear-Start und vor der
Readback-Auswertung nochmals mit den vorbereiteten Bytes verglichen.

## Evidence-basierte RFiDGear-Fähigkeiten

Der projektbezogene Hardware-Abnahmeplan kombiniert Offline-Preflight,
Aktionskatalog, Binärfähigkeiten und Readerkonfiguration zu einer rein lesenden
Bereitschaftsentscheidung. Seine zehn Schritte kennzeichnen Offline-, Read-only-,
schreibende und optionale destruktive Abschnitte einzeln. Der Plan startet keine
Aktion und ersetzt keine frische Testchipfreigabe. Die behobene Upstream-Grenze
#201 ist über das getrennte, strikt lesende Kartenstruktur-Abnahmeprojekt
integriert. PR #206 ergänzt dort capability-gated den hardwarebewiesenen
FileSettings-Exact-Match; ohne passenden RFiDGear-Build bleibt er gesperrt.

Die Seite **Hilfe & Werkzeuge** bündelt die lokalen Betriebsdokumente und einen
rein lesenden Umgebungstest. Dieser validiert Release-Dateien, den externen
Einstellungsort, Projektbibliothek, Profile, Task-Shells, RFiDGear-Binärmarker,
Reader-Konfiguration und Logpfade. RFiDGear wird nicht gestartet; Reader und
Karte werden nicht angesprochen. Berichte unter
`%APPDATA%\RFiDGear-Projektbuilder\reports\environment` enthalten nur Status,
Pfade und technische Nachweise, niemals Inhalte von Einstellungen oder Keys.

`RFiDGearCapabilityInspector` liest ausschließlich die konfigurierte
`RFiDGear.exe` und – falls vorhanden – deren benachbarte `RFiDGear.dll`. ASCII-
und UTF-16-Marker werden ohne Laden oder Ausführen der Assembly gesucht. Der
Launcher verlangt einen positiven Nachweis für `CUSTOMPROJECTFILE`, zusätzlich
für AUTORUN beziehungsweise AUTOMODE. Unbekannte oder fehlende Evidenz sperrt
die jeweilige Aktion; die Dateiversion dient nur noch der Anzeige.

Die sichtbare Matrix trennt Startparameter, ChangeFileSettings, korrelierte
Tasklogs, den autoritativen TaskLoop-Terminalstatus und Erweiterungshosts. Die
Capability `AuthoritativeTaskLoopTerminalStatus` gilt nur bei gemeinsamem
Nachweis von `AcceptedFailures`, `UnhandledFailures`, `InvalidSkips` und
`TerminalStatus` aus RFiDGear PR #208. Damit werden auch teilweise aktualisierte
oder anders paketierte Installationen korrekt behandelt. Aus der bloßen
Versionsnummer wird keine Fähigkeit abgeleitet.

Die Profilbibliothek besteht aus den unveränderlichen gebündelten Profilen und
den dauerhaft gespeicherten Kundenprofilen im Benutzerprofil. Diese
Zusammenführung wird einheitlich für Projektbearbeitung, Build und Preflight
verwendet; Profile enthalten keine Schlüsselwerte.

## Reproduzierbare Profilbindung und Historie

Jeder neue Projektentwurf bindet alle gewählten Profile mit der sichtbaren
Version und einem deterministischen SHA-256-Fingerabdruck des fachlichen
Profilinhalts. Zeitstempel und Historienverweise verändern diesen Fingerabdruck
nicht. Der zentrale Buildservice verlangt eine vollständige, exakt passende
Bindung; dadurch kann weder ein ausgetauschtes Profil noch eine Änderung unter
derselben Version unbemerkt in neue Kartenprojekte einfließen.

Legacy-Projekte ohne Bindung werden bewusst nicht automatisch aktualisiert. Die
WPF-Oberfläche zeigt den Zustand und bietet eine bestätigte Übernahmeaktion an.
Beim Speichern eines geänderten Kundenprofils erzwingt der Profilstore eine neue
Versionsnummer und archiviert den vorherigen JSON-Stand atomar außerhalb des
aktiven, nur top-level eingelesenen Kundenprofilordners.

Der Historienbrowser behandelt Archive als nicht vertrauenswürdige Eingaben:
Pfadgrenze, Profil-ID, Profiltyp, vollständige Profilvalidierung und der beim
Archivieren in den Dateinamen geschriebene SHA-256-Präfix müssen übereinstimmen.
Ein beschädigter Stand wird angezeigt, aber weder verglichen noch aktiviert.
Wiederherstellungen laufen durch denselben atomaren `ProfileStore` und sind nur
als neue, eindeutige Version möglich. Dadurch bleibt die Historie unveränderlich
nachvollziehbar und vorhandene Projektbindungen werden nicht umgangen.

## Projektanalyse / Dry-Run

Die Projektanalyse komponiert Profile sowie Mastering- und Kodierplan im
Speicher. Produktive Schlüssel werden weder verlangt noch aus dem Vault
gelesen: Der Generator erhält stattdessen deterministische, nur während des
Aufrufs existierende Credential-Tokens. Der Dienst prüft Taskreihenfolge,
Abhängigkeiten, File-Verträge, Authentifizierungsrollen, Key0-zuletzt-Regel,
KeySettings und den simulierten produktiven Endzustand. Die exportierten
TXT-/JSON-Berichte enthalten keine Schlüsselwerte und keine `.rfPrj`-Archive.

## Projekt-/Profilvergleich

Der Vergleich arbeitet ausschließlich auf dem normalisierten Profilmodell und
nicht auf JSON-Zeilen oder `.rfPrj`-XML. Dadurch werden fachlich gleiche
Standardwerte – etwa implizites und explizites `zeroFill` – nicht künstlich als
Abweichung gemeldet. Sicherheits- und Strukturänderungen an AIDs, Files,
Keytypen, KeySettings, Communication Mode und Access Rights werden dagegen
einzeln ausgewiesen. Projekt- und Profilquellen werden nur gelesen; Vaults und
Schlüsselwerte gehören nicht zum Vergleichsmodell oder Bericht.

## Schreibgeschützter rfPrj-Profilimport

Der Importer öffnet den rfPrj-ZIP-Container über denselben geprüften
`RfPrjArchive`-Pfad wie Preflight und Analyse. Aus `taskdatabase.xml` werden nur
explizit freigegebene Strukturfelder gelesen. Credentialfelder, eingebettete
Schreibdaten und externe Payloaddateien gelangen nicht in Report oder
Profilvorschlag.

Mehrere Tasks dienen als unabhängige Evidenz für AID, Keytyp/-anzahl,
KeySettings und Fileparameter. Widersprüche werden nicht durch Heuristik
überschrieben, sondern als Fehler markiert und blockieren den Entwurf. Eine
Nullinitialisierung gilt nur dann als belegt, wenn sowohl die Aufgabenbeschreibung
den Initialisierungsvorgang bezeichnet als auch das eingebettete Datenmodell die
exakte Filelänge vollständig mit Nullbytes enthält. Andere WriteData-Inhalte
werden lediglich als nicht rekonstruierbares Kodierrezept gemeldet.

Der Vorschlag bleibt ein Kundenprofil ohne Schlüsselwerte. Der Benutzer muss
die Befunde bestätigen, Applications auswählen und den Entwurf anschließend im
vollständigen Profil-Editor validieren und speichern.

## Initiale und finale FileSettings

Der Profil-Editor erklärt für jede Datei den initialen Mastering-Zustand und den
finalen Kartenzustand getrennt. Bei aktivierten `creationSettings` zeigt er
sämtliche Änderungen an Communication Mode und Access Rights sowie den zur
Härtung verwendeten initialen Change-Key. Der angezeigte Ablauf entspricht dem
Generatorvertrag: CreateFile, optional vollständiger Zero-Fill mit Readback,
danach ChangeFileSettings. Identische Initial-/Endwerte werden als redundanter
zusätzlicher Task markiert.

## Mastering, Karten-Preflight und Recovery

Die Betriebsartefakte werden vor der Anzeige strukturell geprüft. Relative
Planpfade dürfen den Projektordner nicht verlassen. Read-only-Projekte dürfen
nur `ReadData` enthalten; Recovery-Taskfolgen sind fest auf Formatieren sowie
optional das bedingte Rücksetzen des PICC-Keys begrenzt.

AUTORUN-Aktionen erfassen vor Prozessstart einen Logsnapshot. Maßgeblich ist
das letzte danach protokollierte terminale Ereignis `TaskLoop.Success` oder
`TaskLoop.Failure`. Frühere Fehlerhinweise, die RFiDGear innerhalb desselben
Laufs erfolgreich behandelt hat, überschreiben einen späteren terminalen
Erfolg nicht. Ab dem PR-208-Vertrag leitet RFiDGear dieses Ereignis selbst aus
allen Taskoutcomes, tatsächlich ausgeführten Bedingungszweigen und explizitem
ErrorRouting ab. Der Builder prüft unabhängig davon weiterhin Reihenfolge,
Vollständigkeit und Outcome jedes erwarteten Tasks. Ohne Terminalereignis
bleibt der Lauf offen und endet nach zwei Minuten als Timeout.

Für Kartenproben werden alte Ausgabedateien nur im gewählten Probeordner vor
dem Start entfernt. Nach dem Terminalstatus entstehen `_probe.completed.json`,
ein schlüsselbereinigter Logauszug und `card-preflight.result.json/.txt`.
Die Klassifikation verwendet ausschließlich Dateiexistenz, exakte Bytezahl,
Schlüsselzugriff und optionalen Bytevergleich mit dem aktuellen Payload. Der
Byteinhalt `00` besitzt keine Sonderbedeutung.

## Startvertrag

Der Quick-Start aus der Projektbibliothek verwendet denselben Launcher, verlangt
aber zusätzlich eine bearbeitbare Projektdefinition und einen vollständigen
Preflight mit Status `OK`. Sein Argument enthält ausschließlich
`CUSTOMPROJECTFILE`; AUTORUN und AUTOMODE sind verboten. Der resultierende
Zwischenzustand wird als „noch keine Payload / kein Kartenlauf“ angezeigt.
Vor diesem Start werden ausschließlich die im Kodierschema genannten Dateien
unter `payloads` entfernt und der keyfreie Status auf `NotPrepared` gesetzt.
RFiDGear kann dadurch keine Payload einer vorherigen Karte wiederverwenden.

- gezielter Start mit `CUSTOMPROJECTFILE="<vollständiger rfPrj-Pfad>"`
- bei einem bewusst bestätigten Einmallauf zusätzlich `AUTORUN=1`
- bei bewusst gestartetem Serienbetrieb ab RFiDGear 2.0.7.0 stattdessen
  `AUTOMODE=1`; `AUTORUN` und `AUTOMODE` werden nicht kombiniert
- vor `AUTOMODE` muss in RFiDGear ein Reader-Provider ausgewählt sein
- Arbeitsverzeichnis ist immer der Projektordner
- ein Prozessende innerhalb der ersten 750 ms wird als Startfehler gemeldet
- ein weiterlaufender Prozess ist noch kein fachlicher Erfolg

Der Builder merkt sich Prozess-ID und Programmpfad jedes von ihm gestarteten
RFiDGear-Laufs. Solange ein Prozess noch läuft, sind Doppelstart,
Kodierstationswechsel und Builder-Schließen gesperrt. Bei AUTORUN bleibt die
Sperre unabhängig vom Prozesszustand bis zu einem terminalen Logstatus oder
Timeout bestehen. RFiDGear wird niemals ungefragt beendet; die Oberfläche
fordert stattdessen zum bewussten Schließen auf. Ein abgelehnter Builder-
Schließdialog lässt den Überwachungstimer unverändert weiterlaufen.

Ein- und Ausgabepfade aus `DesfireDataFilePath` und
`DesfireReadDataFilePath` werden vor dem Start vollständig aufgelöst. Nur Ziele
innerhalb des Ordners der jeweiligen rfPrj-Datei sind zulässig. Das verhindert,
dass kopierte Projekte Readbacks oder Payloadzugriffe unbemerkt in einem alten
absoluten Ordner ausführen. Nach terminalem Status versucht
**RFiDGear-Prozess abschließen** zuerst das Hauptfenster regulär zu schließen.
Ist kein reagierendes Fenster vorhanden, sind eine bewusste Bestätigung sowie
die Übereinstimmung von Prozess-ID und EXE-Pfad erforderlich.

## Laufentscheidung

Für jeden Kartenlauf werden vor der Payload-Bereitstellung erfasst:

- aktueller Dateizeitpunkt jeder finalen Verification-Datei
- aktueller Byteoffset aller `log-*.txt` unter `%LOCALAPPDATA%\RFiDGear\log`
- erwartete Payloadbytes
- schlüsselfreier rfPrj-Taskplan

Danach gelten folgende Ergebnisse:

1. Ein starker strukturierter RFiDGear-Fehler beendet den Lauf als Fehler und
   wird über `CurrentTaskIndex` dem Taskplan zugeordnet.
2. Fehlende, alte, unvollständige oder noch nicht parsebare Readbacks bedeuten
   `Waiting`, niemals Erfolg.
3. Neue vollständige, aber abweichende Readbacks bedeuten Fehler.
4. Erst wenn alle neuen Readbacks bytegleich sind, ist der Lauf erfolgreich.

Logtexte werden vor Ausgabe oder Persistenz um bekannte Schlüssel sowie
generische zusammenhängende beziehungsweise getrennte 16-/24-Byte-Hexfolgen
bereinigt.

## Kodiersitzung ab preview9

Die WPF-Kodierstation lädt Variablen, Rezepte und Ausgabedateien ausschließlich
aus der schlüsselwertfreien `coding.schema.json`. Damit ist keine erneute
Entschlüsselung des Projekt-Vaults erforderlich. Vor dem Öffnen von RFiDGear
werden alle Sitzungswerte validiert, sämtliche Payloads erzeugt und sowohl die
Verification-Zeitstempel als auch der offizielle RFiDGear-Logstand erfasst.
Jeder Vorbereitungsversuch sperrt zuerst alte Payloads. Erst nach vollständiger,
atomarer Erzeugung erhält `.rfidgear-payload-state.json` den Status `Prepared`
mit dem exakten Sitzungszeitpunkt und der Ausgabeliste. Die Startprüfung verlangt
zusätzlich bytegleiche Dateien. Ungültige Eingaben, Manipulationen, ein
abgeschlossener Datensatz und das Schließen des Builders setzen den Zustand
wieder auf `NotPrepared` und entfernen die definierten Sitzungs-Payloads.
Die Schaltfläche „Finale Readbacks prüfen“ wertet ausschließlich Änderungen
nach dieser Baseline aus; ein neuer starker Logfehler überstimmt bytegleiche
Readbacks.

„RFiDGear nur öffnen“ lädt das konkrete Kodierprojekt ohne automatische
Ausführung. „Serienmodus starten“ verwendet den von RFiDGear 2.0.7.0
bereitgestellten `AUTOMODE=1`-Pfad. RFiDGear übernimmt dabei Kartenpolling,
UID-Wechsel und Taskausführung; der Builder bewertet einen Kartenerfolg weiterhin
ausschließlich anhand neuer bytegleicher finaler Readbacks und neuer Logdaten.

Nach der Payload-Vorbereitung schreibt der Runtime-Adapter zusätzlich
`Letzte_Kodierung.json` im kompatiblen v10.39-Vertrag. Die Abnahmeengine nutzt
diesen Zeitpunkt und die erwarteten Bytes, damit alte Verification-Dateien
niemals als aktueller Nachweis gelten. Berichte enthalten keine Schlüsselwerte.

Ab preview27 besitzt jeder Prüfpunkt zusätzlich eine Nachweisart. `Bytegenau`
bezeichnet einen aktuellen Soll-/Ist-Vergleich der Nutzdaten. `Operativ`
bezeichnet einen daraus belegten erfolgreichen Pfad, etwa Application-
Erreichbarkeit und Authentisierung über den projektierten Produktiv-Keyslot.
`Kein Istwert` bedeutet, dass nur das Soll dokumentiert ist. Operative Evidenz
wird ausdrücklich nicht als bitgenauer Nachweis von KeySettings, Keyanzahl,
Communication Mode oder allen Access Rights ausgegeben.

RFiDGear 2.0.8 und neuer behebt #201 durch einen exakten
`ReadAppSettings`-Vergleich und den separaten `CheckAppKeyCount`-Task. Der
Builder erzeugt ab preview41 ein getrenntes Abnahme-rfPrj mit Exact-Match und
Keyanzahlprüfung. Start und Auswertung verlangen beide Binärfähigkeiten,
vollständige korrelierte `Task.Outcome`-Ereignisse, `TaskLoop.Success` und einen
SHA-256-identischen rfPrj-Stand. Andernfalls bleiben die Punkte sichtbar
`Nicht prüfbar` oder werden bei `IsNotTrue` als Abweichung ausgewiesen.

RFiDGear PR #206 stellt `CheckFileSettings` als automatisierbaren,
strukturierten und strikt lesenden Prüftask bereit. Der Builder erzeugt ab
preview46 je Datei eine Exact-Match-Aufgabe und verlangt die Capability
`FileSettingsCheck`. Nur vollständig korrelierte Taskresultate werden als
Nachweis für Filetyp, exakte Größe, Communication Mode und alle vier Access
Rights übernommen; ohne Merge-/Release-Binary bleibt die Aktion fail-closed.

## Seriensteuerung ab preview14

Die Builder-Sitzung und RFiDGears `AUTOMODE` haben getrennte Aufgaben:
RFiDGear erkennt neue Karten und führt den unveränderten Taskplan aus; der
Builder validiert und schreibt den jeweils ausgewählten Datensatz in die
Payloaddateien. Ein `.rfPrj`-Neubau pro Karte ist nicht erforderlich.

Nur eine erfolgreiche finale Byteverifikation nimmt eine Nummer in den
projektübergreifenden Duplikatschutz auf. Bedienerfehler, technische Fehler und
bewusstes Überspringen werden separat in `sessions/coding_*.jsonl` erfasst.
Sitzungszusammenfassungen verwenden den v10.39-Vertrag, enthalten Kartendaten,
aber keine Schlüsselwerte. Fortlaufende Werte behalten führende Nullen; CSV-
Listen können mehrere `card`-Variablen über deren Feld-IDs befüllen.

## Diagnosepakete ab preview15

Ein fehlgeschlagener Kartenlauf löst automatisch einen Diagnoseexport aus.
Manuell ist derselbe Export über die Kodierstation oder `Strg+Umschalt+D`
erreichbar. Das ZIP verwendet eine feste Positivliste und enthält – soweit
vorhanden – Metadaten, keyfreie Verträge, Session/aktuellen Datensatz,
bereinigten RFiDGear-Log samt Signal, keyfreie Task-Map, Karten-Preflight-
Ergebnisse und aktuelle Readback-/Verification-Dateien.

Projektdefinitionen, Key-Vaults, originale `taskdatabase.xml` und alle `.rfPrj`
werden niemals kopiert. Textquellen werden vor dem Packen erneut sanitisiert;
das Paket enthält weiterhin Kartendaten und ist entsprechend zu schützen.

## Offline-Diagnose

```powershell
artifacts\cli\RFiDGear.Builder.Cli.exe snapshot-rfidgear-log snapshot.json
artifacts\cli\RFiDGear.Builder.Cli.exe diagnose-rfidgear-log snapshot.json diagnose.json
```

Der zweite Befehl wertet ausschließlich die seit dem Snapshot neu geschriebenen
Logbytes aus. Exitcode `2` kennzeichnet einen starken Fehler. Diese Befehle
starten RFiDGear nicht und greifen weder auf Reader noch Karte zu.

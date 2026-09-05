# SieEncode 1.1.0-rc15 – interner Abnahmekandidat

Die Anwenderanwendung heißt `SieEncode.exe`. Sie startet standardmäßig als
SieEncode Studio und mit `--station` als SieEncode Station. Die gebündelte
Engine heißt `SieEncode.Runtime.exe` und weist sich sichtbar als „powered by
RFiDGear“ aus. Der interne Assemblyname `RFiDGear.dll` bleibt zur Kompatibilität
mit den ursprünglichen WPF-Ressourcen erhalten; Copyright und MIT-Lizenz des
Originalprojekts werden vollständig mitgeliefert.

SieEncode ist eine unabhängige Projekt-, Kodier- und Prüfverwaltung für
RFID-Karten. Die Suite besteht aus **SieEncode Studio**, dem direkten
Bedienmodus **SieEncode Station** und einer getrennt installierten
**SieEncode Runtime**, die auf dem MIT-lizenzierten RFiDGear-Projekt basiert.

Der Release Candidate übernimmt vorhandene Projekte und migriert die bisherigen
Builder-Einstellungen automatisch aus `%APPDATA%\RFiDGear-Projektbuilder` nach
`%APPDATA%\SieEncode`. Studio und Station verwenden denselben übergeordneten
Projektordner. Ohne vorhandene Konfiguration wird zunächst
`Dokumente\SieEncode-Projekte` verwendet; der Ordner kann unter Einstellungen
jederzeit auf den vorhandenen übergeordneten Projektordner geändert werden.

Der Installer legt genau einen Startmenüeintrag **SieEncode** an. Projektverwaltung,
Profile und Kodierstation sind Seiten derselben Anwendung.

Die Runtime bleibt technisch und lizenzrechtlich als eigener Unterordner
getrennt. Sie wird bei einer Neuinstallation automatisch erkannt. Herkunft und
Drittanbieter-Lizenzen stehen unter `Studio\Legal`.

RC15 erweitert die Teststation um eine hierarchische PICC-/Application-/File-
Analyse und markiert den aktuellen Navigationspunkt eindeutig. Die Runtime
`1.0.0-rc17+rfidgear.2.0.10.12` korrigiert den ELATEC-Ablauf für
`ChangeFileSettings` mit frischer Authentisierung, begrenzter Wiederholung und
Readback. Die Projektanalyse exportiert außerdem einen mehrseitigen,
schlüsselwertfreien PDF-Kartenreport mit PICC-, Application-, Datei-, Rechte-
und Datenrezeptinformationen. Der Report verwendet ein dichtes technisches
Protokolllayout mit Abschnittsbalken, zweispaltigen Informationsboxen und
vollständigen File-Blöcken. In den Einstellungen führt eine projektunabhängige
Vier-Schritt-Ersteinrichtung durch persönliche Windows-Identität, Einladung und
gemeinsame Teamliste. Dieser Stand ist bis zum realen Hardwarelauf ausdrücklich ein
interner Abnahmekandidat; RC13 bleibt der letzte vollständig hardwaregeprüfte Stand.

## Technischer Ursprung

Der Kern entspricht dem vollständig getesteten Stand des bisherigen
RFiDGear-Projektbuilders 11.0-preview46. Interne Namespaces und Dateiformate
bleiben in diesem RC absichtlich kompatibel; die sichtbare Produktidentität und
der Installer sind bereits SieEncode.

Vor produktiver Seriennutzung ist weiterhin eine Abnahme mit dem konkreten
Reader, der Kartencharge und der produktiven Kodiervorschrift erforderlich.

Der aktuelle Entwicklungsstand stellt Projektdateien auf schlüsselfreie
Referenzen um. Transport- und Produktivschlüssel bleiben im verschlüsselten
Mehrbenutzer-Vault und werden der gestarteten Runtime nur prozessgebunden für die
aktive Sitzung bereitgestellt. Der Sicherheitsvertrag steht unter
`docs/PROJECT-SECRETS-V1.md`.

Vor jedem Mastering muss der aktuelle Vault in derselben Sitzung erfolgreich
geöffnet werden. Zusätzlich erzeugt SieEncode eine hashverifizierte Kopie aus
schlüsselfreier Projektdefinition, verschlüsseltem Vault und Recovery-Manifest
an einem unabhängig von der Projektbibliothek konfigurierten Speicherort. Ohne
diese Sicherung bleibt der Mastering-Build gesperrt. Eine aus den Schlüsselwerten
abgeleitete SHA-256-Keyset-ID erscheint in Sicherung und Serienprotokoll, enthält
selbst aber keine Schlüsselwerte.

## Enthalten

- `RFiDGear.Builder.Core`: kompatibles Projektmodell, JSON-Persistenz,
  Projektvalidierung, Pfadauflösung und portabler Key-Vault sowie vollständiges
  Profilmodell mit additiver Komposition und Konfliktprüfung.
- `RFiDGear.Builder.Cli`: paketfreie Kommandozeilen-Schnittstelle zum Validieren,
  Bereinigen, Erzeugen von Keys, Erzeugen der Payload- und Vertragsdateien,
  Projekt-Preflight, schlüsselfreiem Task-Export sowie Ver- und Entschlüsseln
  des Vaults.
- `RFiDGear.Builder.RfPrj`: Task-Shell-Lader, atomischer `.rfPrj`-ZIP-Container
  sowie Generatoren für Mastering, Kodierung, Karten-Preflight und Recovery.
- `RFiDGear.Builder.Runtime`: testbarer Launcher für `CUSTOMPROJECTFILE`,
  `AUTORUN=1` und `AUTOMODE=1`, evidence-basierte Binärfähigkeits- und
  Readerkonfigurationsprüfung, inkrementelle Logüberwachung,
  Schlüsselredaktion, Fehler-/Taskkorrelation und finale Readback-Auswertung.
- `RFiDGear.Builder.Tests`: paketfreier automatischer Regressionstest.
- `Test-All.ps1`: Build, Tests, v10.39-Projektkompatibilität, vollständige
  Vertragsdatei-Parität, schlüsselfreie Betriebs- und Artefaktprüfung,
  bidirektionaler Vault-Kompatibilitätstest sowie kompletter isolierter
  UI-Smoke-Test mit eindeutigen Automation-IDs.

## Projektprüfung und UI-Testbarkeit in RC3

Die sichtbare Projektprüfung verlangt beim Öffnen, Bearbeiten und Vorbereiten
keine produktiven Schlüssel mehr. Bearbeitbare Definitionen werden gegen
Profil, Struktur und vorhandene Bauartefakte geprüft. Reine Alt-Bauordner ohne
Quelldefinition erhalten eine ausdrücklich begrenzte strukturelle
Artefaktprüfung. Der eigentliche Projektbau bleibt unverändert streng und
verlangt die produktiven Sitzungsschlüssel.

Alle interaktiven Flächen der Hauptnavigation und der drei Nebenfenster tragen
stabile, eindeutige WPF-Automation-IDs. `SieEncode.exe --ui-smoke-test` prüft
Navigation, Nebenfenster, ein temporäres bearbeitbares Projekt, ein reines
Bauartefakt und das Laden beider Typen in die Kodierstation. Der Test startet
weder RFiDGear noch Reader- oder Kartenaktionen.

## Geführte Bedienung und Hardware-Smoke in RC5

`Hilfe & Tour` ist von jeder Hauptseite erreichbar und führt in sechs Schritten
vom Projekt über die sichere Kartenprüfung bis zum Abschlussbericht. Schreibende
Schritte und reine Leseprüfungen sind dabei klar gekennzeichnet.

Der UI-Test umfasst nun auch benannte Statusausgaben sowie einen Designvertrag:
Standardschaltflächen sind mindestens 38 px, Navigationseinträge 42 px und
Auswahllisten 36 px hoch. Benannte Stile bauen auf demselben Basisstil auf.

Für unbeaufsichtigte Readerdiagnosen existiert ein ausdrücklich read-only
Hardware-Smoke-Modus. Er akzeptiert ausschließlich Projekte, deren sämtliche
Tasks `ReadData` sind, prüft Reader und AUTORUN-Fähigkeit, überwacht den
terminalen RFiDGear-Status, erzeugt einen schlüsselfreien JSON-Bericht und
beendet den gestarteten Runtime-Prozess kontrolliert.

Der Kern zielt zunächst auf `net8.0`, weil das aktuelle RFiDGear ebenfalls
`net8.0-windows` verwendet. Die Kernbibliothek selbst hängt nicht von WPF oder
Windows-spezifischen APIs ab. Ein späteres WPF-Frontend kann daher separat auf
einen neueren LTS-Runtime-Stand wechseln.

## Kartenbetrieb in der Kodierstation

`11.0-preview46` erkennt und prüft die mit dem Projekt gebauten Mastering-,
Karten-Preflight- und Recovery-Projekte. Der Karten-Preflight lässt nur
`ReadData` zu. Recovery akzeptiert ausschließlich den vorgesehenen Formatlauf
beziehungsweise Format plus bedingtem PICC-Key-Rücksetzen.

Die Aktion **Anwendungen prüfen** enthält je Application `ReadAppSettings` im
Exact-Modus und `CheckAppKeyCount`. Sie ist strikt lesend. RFiDGear 2.0.10 kann
den zusätzlich sichtbaren `CheckFileSettings`-Marker noch nicht als rfPrj-
Tasktyp laden; RC5 erzeugt ihn deshalb nicht und weist Dateityp, Größe,
Communication und Access Rights ausdrücklich als „nicht geprüft“ aus. Erfolg
wird an rfPrj-SHA-256, Taskplan, RunId, vollständige Taskoutcomes und
`TaskLoop.Success` gebunden.

Automatische Schreibläufe sind nur bei einer bearbeitbaren Projektdefinition,
erfolgreichem Projekt-Preflight, positivem AUTORUN-Binärnachweis und ausgewähltem
Reader-Provider freigeschaltet. Recovery benötigt zwei Bestätigungen. Der
Builder meldet einen Lauf erst nach einem terminalen `TaskLoop.Success` und
vollständig geordneten Ereignissen derselben `RunId` als RFiDGear-Engineerfolg;
die physische Datenprüfung erfolgt weiterhin getrennt über Readbacks.
RFiDGear-Builds mit PR #208 werden zusätzlich an der vollständigen Capability
`AuthoritativeTaskLoopTerminalStatus` erkannt. Ältere Builds bleiben nutzbar,
weil der Builder deren geordnete Taskoutcomes selbst fail-closed kontrolliert.
Das Log wird während AUTORUN automatisch überwacht. Nach einer Kartenprobe
werden Transport-/Produktivzugriff, Dateigrößen und vorhandene Soll-Payloads
ausgewertet und als kompatibler JSON-/TXT-Kartenprüfbericht gespeichert.
Die Produktivprobe wird erst nach Vorbereitung der aktuellen Sitzungswerte
freigeschaltet, damit kein veraltetes Payload als SOLL verwendet wird.

Jeder neue Build enthält zusätzlich `rfidgear.host-contract.json`. Beim Start
schreibt der Builder für Kodierung und jede Betriebsaktion einen korrelierten,
schlüsselfreien Host-V1.1-Request nach `integration/host-v1/runs`;
Startzustand, geordnete Taskresultate, unveränderlicher Engineabschluss und
separate Consumer-Abnahme werden unter derselben Request-ID protokolliert.
rfPrj- und Taskplan-SHA-256 verhindern einen Artefaktwechsel nach Annahme.
Readerauswahl, Kartenpolling und Taskausführung bleiben vollständig in
RFiDGear. Die formalen Request-/Result-Schemas liegen unter `docs/contracts`.

Kodierprojekte mit Application- oder PICC-Keywechsel sind zusätzlich
fail-closed gesperrt, solange RFiDGear keine binär nachweisbare Verifikation des
neu gesetzten Keys bereitstellt **und** der exakte Build nicht im
hardwaregeprüften Builder-Vertrauenskatalog steht. Implementierungsmarker oder
eine hohe Versionsnummer allein genügen ausdrücklich nicht. Freigegeben ist
der getrennte Fork-Build **RFiDGear Builder Runtime 2.0.10.1** auf Commit
`2d0429f`; RFiDGear bleibt ein extern konfigurierbares Paket und ist nicht im
Builder-ZIP enthalten. Hintergrund ist der mit 2.0.10 reproduzierte
Fehler c3rebro/RFiDGear#178: Keywechsel wurden als erfolgreich protokolliert,
obwohl der alte Transport-Key auf dem Testchip aktiv blieb. Unbekannte oder
inhaltlich abweichende Builds bleiben gesperrt; reine Leseprüfungen sind davon
nicht betroffen.

## Bedienbare Vorschau

- Neue WPF-Anwendung mit dauerhafter Seitennavigation, sichtbarer Zurück-Funktion
  und Version in der Titelzeile.
- Der Ein-Schritt-Pfad **Kodiersession starten** lädt aus der Projektbibliothek
  die Kodierstation, prüft den vollständigen Projektvertrag, öffnet das konkrete
  RFiDGear-Kodierprojekt ausschließlich per `CUSTOMPROJECTFILE` und fokussiert
  das erste Kartenfeld. AUTORUN/AUTOMODE und Payloadbereitstellung bleiben aus.
- Konfliktgeschütztes Speichern von Projektdefinitionen, Kundenprofilen und
  Key-Vaults über SHA-256-Ausgangsstände und rechnerübergreifende Lockdateien;
  Fremdänderungen werden nie still überschrieben.
- Bearbeitbare Projektdefinitionen können nach exakter Pfadvorschau gelöscht
  werden. Pfade außerhalb der Projektbibliothek und gebaute Altprojekte sind
  gesperrt; Vault und sämtliche Buildartefakte bleiben erhalten.
- Exklusive Arbeitsbereichs-Lease verhindert parallelen Build und parallele
  Kodiersitzungen im selben Projektordner. Vor RFiDGear-Start und Readback-
  Bewertung werden vorbereitete Payloads nochmals bytegenau geprüft.
- Seiten für Übersicht, Projekte, Kodierstation, Profile, Analyse/Dry-Run,
  Projekt-/Profilvergleich, Abnahme, Einstellungen sowie Hilfe & Werkzeuge.
- Der integrierte Umgebungstest prüft Builder-Dateien, Dokumentation, Profile,
  Task-Shells, externe Einstellungen, Projektbibliothek, RFiDGear-Fähigkeiten,
  Reader-Konfiguration und Logpfade rein lesend und exportiert keyfreie
  TXT-/JSON-Berichte.
- Die Kodierstation erzeugt für das ausgewählte Projekt einen keyfreien
  Hardware-Abnahmeplan. Er prüft Softwarebereitschaft und trennt Offline-,
  Read-only-, Schreib- und optionale Recovery-Schritte, startet aber selbst
  keine Hardwareaktion.
- Die Projektanalyse erzeugt Kartenaufbau, Mastering- und Kodier-Taskplan nur
  im Speicher, simuliert Credential-Rollen, Keywechsel und Endzustand mit
  synthetischen Tokens und exportiert ausschließlich keyfreie TXT-/JSON-Berichte.
  Sie funktioniert auch ohne entsperrten Produktivkey-Vault.
- Der semantische Projekt-/Profilvergleich stellt zwei vollständige
  Profilkompositionen oder zwei Einzelprofile gegenüber. Er zeigt Profile,
  Variablen, AIDs, Files, Größen, Communication Mode, Access Rights, initiale
  FileSettings, Initialisierung, KeySettings und Kodierrezepte getrennt nach
  hinzugefügt, entfernt und geändert; TXT-/JSON-Export bleiben keyfrei.
- Grafischer Grund- und Zusatzprofil-Editor für Metadaten, Variablen, Applications, Files,
  KeySettings, Access Rights, temporäre Creation Settings, Mastering-
  Initialisierung und Kodierrezepte. Referenzierte Variablen und verwendete
  Key-Slots werden beim Ändern geschützt.
- Kodierrezepte können frei geordnete HEX-, ASCII-, UTF-8-, BCD- und
  Integer-Bausteine sowie gerichtetes oder dateifüllendes Padding kombinieren.
  Eine Offset-/HEX-/Klartext-Bytetabelle zeigt die resultierende Datei bereits
  im Profileditor (`docs/DATENBAUSTEINE.md`).
- Kundenprofile können als unabhängiger neuer Entwurf dupliziert werden. ID,
  Kennung, Name und Version werden neu vorbelegt; Zeitstempel und
  Vorgängerversion des Originals werden nicht übernommen.
- Die ausgewählte Basis- oder Kundenprofildefinition kann als keywertfreie
  Kodiervorschrift exportiert werden. GUI und CLI verwenden denselben getesteten
  C#-Dienst; interne Kompatibilitätsfixtures bleiben ausschließlich im Testbestand.
- Der Datei-Editor trennt initiale Mastering-Settings und finalen Kartenzustand
  sichtbar. Technische Kompaktwerte, Klartexterklärung, konkrete Änderungen und
  die resultierende CreateFile-/Zero-Fill-/ChangeFileSettings-Reihenfolge werden
  live angezeigt; redundante zweistufige Einstellungen erhalten eine Warnung.
- Die Zero-Fill-Checkbox bildet nun den Profilvertrag exakt ab: fehlende Angabe
  bedeutet standardmäßig Zero-Fill, bewusstes Abwählen speichert explizit
  `masteringInitialization.type = none`.
- Eigene Grund- und Zusatzprofile werden versionsunabhängig unter
  `%APPDATA%\SieEncode\profiles\base` beziehungsweise `customer` gespeichert.
  SieEncode liefert keine fachlichen Grund- oder Kundenprofile vorinstalliert aus.
- Fachlich geänderte Kundenprofile benötigen eine neue Versionsnummer. Der
  vorherige JSON-Stand wird unveränderlich unter
  `%APPDATA%\RFiDGear-Projektbuilder\profiles\history\<Profil-ID>` archiviert;
  Erstellungs-, Änderungs- und Vorgängerversion werden im Profil dokumentiert.
- Über **Profile → Historie / Wiederherstellen** lassen sich aktuelle und
  archivierte Stände mit Integritätsstatus anzeigen und fachlich vergleichen.
  Ein Vorgänger kann nur als neue, noch nie verwendete Version aktiviert werden;
  der aktuelle Stand wird dabei automatisch selbst zum Archivstand.
- Über **Profile → Bestehendes rfPrj importieren** wird ein vorhandenes
  RFiDGear-Projekt ausschließlich gelesen und als prüfbarer Kundenprofil-Entwurf
  vorbereitet. Sicher erkennbare AIDs, Files, Größen, Communication Modes,
  Access Rights und KeySettings werden übernommen; Schlüsselwerte und
  Schreibdaten niemals. Kodierrezepte, die das rfPrj nicht eindeutig beschreibt,
  bleiben mit sichtbarer Warnung leer.
- Projektdefinitionen binden jeden gewählten Profilstand mit Versionsnummer und
  SHA-256-Inhaltsfingerabdruck. Legacy-Projekte bleiben les- und speicherbar,
  können aber erst nach einer sichtbar bestätigten Profilübernahme wieder
  gebaut werden. Auch eine Inhaltsänderung ohne Versionswechsel sperrt den Build.
- Strukturierter Projekteditor für neue und bestehende `*.project.json`-Dateien:
  Projektname, Ausgabeziel, Buildumfang, Master-/Kundenprofile, Projektwerte,
  verschlüsselte Ausgangsschlüsselsätze und produktive Sitzungsschlüssel.
- `Speichern` ersetzt die vorhandene Projektdefinition atomar am bestehenden
  Pfad; nur `Speichern unter` legt bewusst eine weitere Definition an.
- Produktivkeys sind sichtbar, solange das Projekt in der Editorsitzung geöffnet
  ist. Sie werden nie in `*.project.json` geschrieben. Für Mehrbenutzerbetrieb
  verschlüsselt Vault v3 den Inhalt mit AES-256-GCM und den Sitzungsschlüssel für
  jede freigegebene persönliche Windows-Zertifikatsidentität separat per
  RSA-OAEP-SHA256. Eine in KeePass verwahrte Passphrase dient nur der Recovery.
- Unter **Einstellungen → Teamzugriff auf Projekt-Vaults** registriert jedes
  Teammitglied seine öffentliche Einladung einmal in der gemeinsamen
  Projektbibliothek. Neue Vaults übernehmen diese Liste automatisch. Die Datei
  `.sieencode\team-vault-recipients.json` enthält ausschließlich öffentliche
  Zertifikate; private Identitäten bleiben im Windows-Zertifikatsspeicher des
  jeweiligen Benutzers. Änderungen werden mit Revisionsschutz gespeichert.
- Ein Vault kann mehrere benannte Ausgangsschlüsselsätze für Blankokarten und
  bereits personalisierte Karten enthalten. Nur die zufällige ID des gewählten
  Satzes steht in der Projektdefinition; Namen und Grundkey-Werte bleiben
  verschlüsselt. Alte Vaults werden als ein bisheriger Satz weiterverwendet.
- Bei Vault v3 erzeugt SieEncode produktive `.rfPrj` ausschließlich in einer
  lokalen, gesperrten Betriebssitzung unter dem Benutzerprofil. Beim Schließen
  werden diese Dateien entfernt; verwaiste Sitzungen werden beim nächsten Start
  bereinigt. Im gemeinsamen Projektstamm verbleiben Definition und Vault.
- Der neue zentrale C#-Buildservice erzeugt aus der gespeicherten keyfreien
  Definition plus Sitzungsschlüsseln sämtliche rfPrj- und Vertragsartefakte und
  verlangt anschließend einen vollständigen erfolgreichen Preflight.
- Die Seite „Prüfung & Abnahme“ erzeugt aus `acceptance.plan.json`,
  `Letzte_Kodierung.json` und den finalen Verification-Dateien echte
  Soll-/Ist-Berichte als TXT und JSON. Nur aktuelle bytegleiche Nachweise sind
  `OK`; Abweichungen und nicht sicher prüfbare Metadaten bleiben getrennt sichtbar.
  Jeder Prüfpunkt nennt zusätzlich seine Nachweisart: `Bytegenau`, `Operativ`
  oder `Kein Istwert`. RFiDGear 2.0.8 behebt #201 durch exakten
  `ReadAppSettings`-Vergleich und `CheckAppKeyCount`. Der Builder erzeugt dafür
  ein getrenntes strikt lesendes Application-Abnahmeprojekt, startet es nur bei
  nachgewiesenen Fähigkeiten und übernimmt ausschließlich vollständig
  korrelierte Taskresultate. PR #206 ergänzt den hardwarebewiesenen
  `CheckFileSettings`-Exact-Match; der Builder übernimmt ihn capability-gated
  für Typ, vollständige Größe, Communication Mode und alle Access Rights.
- Jede C#-Sitzung schreibt nach erfolgreicher Payload-Vorbereitung einen
  v10.39-kompatiblen, schlüsselwertfreien `Letzte_Kodierung.json`-Nachweis.
- Versionsunabhängige Einstellungen unter
  `%APPDATA%\SieEncode\studio.settings.json`; eine vorhandene alte
  `%APPDATA%\RFiDGear-Projektbuilder\builder.settings.json` wird einmalig
  migriert.
- Der eingestellte Projekteordner ist der übergeordnete Stammordner; darunter
  werden bearbeitbare `*.project.json`-Dateien und gebaute Altprojekte rekursiv
  gefunden. Projektliste und Kodierstation verwenden denselben Zielpfad.
- Gebaute Projekte können nach exakter Profil-/Layoutprüfung als neuer,
  ungespeicherter und keyfreier Mastering-Entwurf geöffnet werden. Zusätzlich
  importiert die Projektseite ältere JSON-Konfigurationen. Quelldateien bleiben
  unverändert; Kodierung und Vault werden erst nach bewusster Prüfung ergänzt.
- `START_RFiDGear-Projektbuilder.cmd` startet die veröffentlichte Anwendung.
- Ein Kodierprojekt wird nur nach C#-Preflight und bewusst ohne `AUTORUN` in
  RFiDGear geöffnet. Alternativ startet die ausdrücklich gewählte Schaltfläche
  „Serienmodus starten“ dasselbe Projekt mit `AUTOMODE=1`.
- Solange ein vom Builder gestarteter RFiDGear-Prozess läuft oder einem
  AUTORUN-Lauf noch der terminale Logstatus fehlt, sind Doppelstart,
  Projektwechsel und Builder-Schließen fail-closed gesperrt.
- Alle rfPrj-Eingabe- und Readbackpfade müssen im gebauten Projektordner liegen.
  Externe absolute Pfade werden im Preflight benannt und vor Hardwarestart
  gesperrt. Terminal abgeschlossene RFiDGear-Prozesse können über einen
  kontrollierten, PID-/EXE-geprüften Bedienweg abgeschlossen werden.
- Der Serienmodus wird nur bei positivem `AUTOMODE`-Binärnachweis und einem in
  RFiDGear ausgewählten Reader-Provider freigegeben. Der Builder verändert
  RFiDGears eigene Reader-Einstellung nicht.
- Die Kodierstation erzeugt ihre Eingabefelder direkt aus `coding.schema.json`.
  Dadurch lassen sich auch nur noch gebaut vorhandene Altprojekte ohne erneute
  Abfrage produktiver Schlüssel für eine Sitzung laden.
- Kartenwerte bleiben im Arbeitsspeicher der Sitzung. Erst nach vollständiger
  Validierung werden Payloads sicher geschrieben und Baselines für finale
  Readbacks sowie das RFiDGear-Log erfasst.
- Ein starker neuer Logfehler hat Vorrang. Erfolg wird nur bei neuen,
  vollständigen und bytegleichen finalen Verification-Dateien angezeigt.
- Die Builder-eigene Seriensteuerung unterstützt Einzel, Fortlaufend und
  einspaltige TXT- sowie mehrspaltige CSV-Listen. Nach Erfolg wird der nächste
  Datensatz ausgewählt; Enter stellt ihn ohne `.rfPrj`-Neubau bereit.
- F8 prüft die finalen Readbacks, F9 markiert einen Lauf bewusst als Fehler.
  Fehlgeschlagene Nummern können erneut versucht oder in Fortlaufend/Liste nach
  Bestätigung übersprungen werden.
- Erfolgreiche Nummern werden projektübergreifend aus früheren JSONL-Sitzungen
  erkannt. JSONL-Log und TXT-/JSON-Zusammenfassung enthalten Kartendaten, aber
  ausdrücklich keine Schlüsselwerte.
- Bei jedem fehlgeschlagenen Kartenlauf entsteht automatisch ein Diagnose-ZIP;
  zusätzlich ist der Export über Schaltfläche oder `Strg+Umschalt+D` möglich.
  Enthalten sind ausschließlich freigegebene Verträge, Sessiondaten, eine
  keyfreie Task-Map, Readbacks und bereinigte Logs. Projektdefinitionen,
  Key-Vaults und sämtliche `.rfPrj` sind technisch ausgeschlossen.

## Test

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-All.ps1
```

Die Tests sprechen weder Karte noch Reader an. Es werden keine externen
NuGet-Pakete und keine Netzwerkverbindung benötigt.

Der belegte Stand gegenüber v10.39 einschließlich bewusst offener Punkte steht
in `PARITY.md`.

## Bewusste Systemgrenze

SieEncode Studio greift nicht selbst auf den Leser zu. Sämtliche Reader-,
DESFire- und Taskausführung bleibt in der gebündelten SieEncode Runtime
(`powered by RFiDGear`). Studio steuert sie über Projektdatei, Startparameter
und den schlüsselfreien Host-/Logvertrag und wertet die Ergebnisse fail-closed
aus.

Payloads und alle sechs `.rfPrj`-Projektarten sind mit internen, nicht ausgelieferten
Kompatibilitätsfixtures semantisch gegen v10.39 getestet. Auch `project.definition.json`,
`builder.manifest.json`, `coding.schema.json`, `acceptance.plan.json`,
`recovery.plan.json` und `card-preflight.plan.json` werden strukturell und
semantisch identisch erzeugt; Zeitstempel und die absichtlich neue
Builder-Version sind ausgenommen. Die Ablösung von v10.39 verlangt zusätzlich
einen erfolgreichen vollständigen Bedienlauf des konkret installierten
Releasepakets.

Ein GUI-Prozess oder Exitcode gilt ausdrücklich nicht als Kartenerfolg. Der
Runtime-Adapter meldet Erfolg erst, wenn alle erwarteten finalen Readbacks seit
dem Laufbeginn neu geschrieben, vollständig und bytegleich sind. Details stehen
in `RUNTIME.md`.

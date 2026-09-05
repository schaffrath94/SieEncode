# Änderungsprotokoll

## SieEncode 1.1.0-rc15 - Vault-Ersteinrichtung und technischer Kartenreport

- Die Einstellungen enthalten eine projektunabhängige Vault-Ersteinrichtung für mehrere Nutzer mit dauerhaft sichtbarem Status und einem geführten Vier-Schritt-Ablauf: persönliche Windows-Identität anlegen, öffentliche Einladung exportieren, mehrere Team-Einladungen importieren und gemeinsame Empfängerliste speichern.
- Die Einrichtung verlangt, dass die eigene Identität in der Teamliste enthalten ist. Einzelzugriff und vollständiger Mehrbenutzerbetrieb werden klar unterschieden; private Schlüssel werden niemals exportiert.
- Der PDF-Kartenreport übernimmt die dichte Informationsarchitektur technischer DESFire-Analyseprotokolle in eigenem SieEncode-Design: feste Seitenleiste, Abschnittsbalken, zweispaltige Karten-/Application-/File-Boxen sowie separate Datenbereiche.
- Reine Sollreports kennzeichnen UID, Hardware-/Softwareversion, Speicher- und Produktionsdaten ausdrücklich als nicht verfügbar. Erzeugbare Sollnutzdaten werden als HEX und ASCII dargestellt; Schlüsselwerte bleiben ausgeschlossen.

## SieEncode 1.1.0-rc14 - Kartenreport und belastbarere Teststation

- Die aktive Hauptnavigation wird eindeutig hervorgehoben; die Teststation zeigt PICC, Applikationen und Dateien als hierarchische Kartenstruktur mit Einstellungen, Rechten sowie vorhandenen HEX-/ASCII-Inhalten.
- `ChangeFileSettings` wird in der RC17-Runtime bis zu dreimal mit frischer Kartenselektion ausgeführt und nur nach exaktem Readback von Communication Mode und Access Rights als erfolgreich gewertet. Der reale Hardwarebeweis steht noch aus.
- Aus der keywertfreien Projektanalyse kann ein professioneller, mehrseitiger PDF-Sollreport erzeugt werden. Er basiert auf dem neutralen `CardReport`-Vertrag, enthält Projekt-, PICC-, Applikations-, Datei-, Rechte- und Datenrezeptinformationen und übernimmt keine Schlüsselwerte.
- Der PDF-Vertrag ist für spätere Karten-Istberichte und Soll/Ist-Vergleiche vorbereitet; nicht gelesene Kartendaten werden sichtbar als nicht verfügbar ausgewiesen.
- Softwaretests: 183/183 RFiDGear-Runtime- und 49/49 SieEncode-Regressionstests. Die reale RC17-Hardwareabnahme bleibt bis zur sicheren Erkennung eines Testtransponders gesperrt; RC13/RC16 bleibt der letzte vollständig hardwarebestätigte Stand.

## SieEncode 1.1.0-rc13 – eigenständige Teststation

- Projektlose Transponderanalyse, Reader-Einrichtung, projektbezogene Kartenprüfungen und Wartung sind in einer eigenen Teststation gebündelt; die Kodierstation bleibt auf den geführten Projektablauf reduziert.
- Transponder lassen sich ohne Projekt mit einem flüchtig bereitgestellten PICC-Schlüssel formatieren. Zwei Bestätigungen schützen die destruktive Aktion; im temporären rfPrj steht ausschließlich eine Secret-Referenz.
- Nach der AID-Inventur kann eine fremde Application mit AID, Schlüsseltyp und Key-Nummer vertieft untersucht werden. RFiDGear inventarisiert File-IDs, Typ, Größe, Communication Mode und alle vier Access Rights.
- Standard- und Backup-File-Inhalte werden mit dem nur für die Sitzung bereitgestellten Schlüssel soweit möglich gelesen und als HEX/ASCII dargestellt. Value- und Record-Inhalte bleiben ausdrücklich als späterer Ausbau markiert.
- Fehlende Berechtigung oder ein abgelehnter Schlüssel wird niemals als „0 Files“ oder leer bewertet.
- Die exakte RC16-Runtime bestand den automatisierten ELATEC-End-to-End-Test: Recovery 2/2, Mastering 10/10, Kodierung 16/16 mit drei verifizierten AES-Keywechseln und bytegleichen Readbacks, tiefe Inventur 1/1 sowie abschließender Rückbau und Standardzustandsnachweis.
- Der reproduzierbare Kommandozeilenmodus `--teststation-hardware-acceptance` verwendet ausschließlich die mitgelieferte synthetische Testfixture, persistiert keine Schlüssel und hinterlässt die Testkarte leer mit DES-Standard-PICC.
- `ChangeFileSettings` lieferte im getrennten ELATEC-Vorversuch weiterhin `TransportError`; dieser unabhängige RFiDGear-Punkt wird nicht durch die RC16-Freigabe der Application-Keywechsel verdeckt.

## SieEncode 1.1.0-rc12 – ruhige Kodierstation und vertiefte Projektanalyse

- Die Bedienentscheidung bleibt oberhalb des scrollbaren Kodierablaufs dauerhaft sichtbar.
- Die Kodierstation verwendet eine kompaktere Schrittdarstellung und bezeichnet den Wechsel durchgängig als „Projekt wählen“.
- Die projektlose Transponderanalyse ist zusätzlich aus Projektverwaltung und Kodierstation erreichbar.
- Nach einer projektbezogenen Erkennung liest „Projektinhalt auslesen“ die erwarteten Files mit dem wahrscheinlich passenden Grund- oder Produktivschlüsselsatz strikt lesend. Der Bericht zeigt bis zu 256 Byte je File als HEX und ASCII.
- Die freie Tiefenanalyse fremder Applications bleibt capability-gated: Ohne Runtime-Unterstützung für sichere Application-/File-Inventur werden keine Schlüssel blind gegen unbekannte Strukturen ausprobiert.

## SieEncode 1.1.0-rc11 – verschlüsselte Grundkeysätze

- Grundkeys für Blankokarten und bereits personalisierte Karten werden als mehrere benannte Ausgangsschlüsselsätze ausschließlich im verschlüsselten Projekt-Vault verwaltet.
- Die Projektdefinition enthält nur die zufällige ID des gewählten Satzes; Satzname, PICC-Key und Application-Keys bleiben aus Projektdatei, Einstellungen, Protokollen und dauerhaftem Build ausgeschlossen.
- Ein Ausgangsschlüsselsatz kann im normalen Editor ausgewählt sowie aus den aktuellen Sitzungswerten hinzugefügt oder gelöscht werden. Die Werte selbst bleiben hinter der Expertenansicht.
- Bestehende Vaults werden ohne Formatbruch als ein Satz „Bisheriger Ausgangsschlüsselsatz“ weiterverwendet.
- Der bestehende Empfänger-Vault bleibt rechner- und benutzerübergreifend: Jeder Rechner nutzt seine eigene Windows-Identität, während Recovery-Zugang und freigegebene Empfängerliste erhalten bleiben.
- Eine zentrale, schlüsselfreie Team-Empfängerliste wird einmal in der gemeinsamen Projektbibliothek gepflegt. Neue Projekt-Vaults übernehmen alle Teammitglieder automatisch; eine Identität muss nicht mehr in jedem Projekt einzeln importiert werden.

## SieEncode 1.1.0-rc10 – neutrale Erstauslieferung und projektlose Analyse

- Die Releasepakete enthalten keinerlei fachliche Grund-, DEVK-/Kundenprofile oder vorgebundene Projektvorlage; interne Referenzen bleiben reine Testfixtures.
- Ein Transponder kann ohne Projekt strikt lesend analysiert werden: vollständige AID-Inventur, frei lesbare PICC-Rechte sowie DES-/AES-Standard-PICC-Proben mit eindeutigem Unbekannt-Status für geschützte Inhalte.
- Grundprofile lassen sich vollständig im GUI neu erstellen, importieren und versioniert bearbeiten. Mehrere Grundprofile bleiben parallel wählbar; jedes Projekt bindet genau eines.
- 47/47 Regressionstests, profilfreier Publish-Vertrag und UI-Smoke-Test bestanden.
- Installer-Version auf 1.1.9 angehoben.

## SieEncode 1.1.0-rc9 – geführte Kartenvorbereitung und klare Eingaben

- Nur noch eine Startmenü-Verknüpfung; Studio und Kodierstation bleiben Seiten derselben Anwendung.
- RFiDGear lässt sich ohne Projekt öffnen, damit Reader-Einstellungen direkt gepflegt werden können.
- Eingabefelder begrenzen die Profilvorgabe sofort, zeigen den Zeichenstand und markieren Änderungen sichtbar.
- Produktive PICC-Rechte sind als vier einzeln wählbare Optionen statt als HEX-Feld bedienbar.
- Ein unabhängiger Vault-Sicherungsordner wird automatisch im lokalen Benutzerprofil angelegt.
- Kartenvorbereitung ist ein sichtbarer normaler Arbeitsschritt; manuelle und destruktive Recovery-Aktionen bleiben erweitert.
- Nach einer Formatierung zeigt die Bedienentscheidung den leeren Zustand und führt direkt zur Vorbereitung.
- Value- und Record-Files bleiben als spätere Erweiterung vorgemerkt.
- 47/47 automatisierte Tests, vollständiger WPF-Publish und UI-Smoke-Test bestanden.
- Installer-Version auf 1.1.8 angehoben.

## SieEncode 1.1.0-rc8 – modularer Byteeditor für allgemeine Kodiervorschriften

- Kodierfiles lassen sich aus zwölf frei geordneten Bausteinen zusammensetzen: feste und variable HEX-Bytes, ASCII-/UTF-8-Klartext, ASCII-/BCD-Ziffern, vorzeichenlose Big-/Little-Endian-Zahlen sowie Null- oder frei gewähltes Byte-Padding.
- Projekt- und Kartenvariablen besitzen die validierten Datentypen Ziffern, Text oder HEX. Ungültige Zeichen, halbe HEX-Bytes, Überlängen und Integer-Überläufe sperren die Payload-Erzeugung fail-closed.
- Der Profileditor verwendet verständliche Formatnamen und Erklärungen statt interner Operationsnamen und zeigt eine Offset-/HEX-/Klartext-Bytetabelle des vollständigen Files.
- Die synthetische Softwarematrix enthält fünf unabhängig erfundene Kodiervorschriften; das neue Modular-Formats-Szenario kombiniert alle unterstützten Zeichen- und Zahlenformate in einem Infofile.
- Value-, Linear-Record- und Cyclic-Record-Files bleiben bewusst gesperrt, bis die fehlende ELATEC-Create-/Write-/Read-/Commit-Unterstützung in RFiDGear ergänzt und real abgenommen ist.
- Der installierte sichtbare RC8-Hardwarelauf erkannte den belegten Transponder mit `F482D0`, `F51773` und `F52348` zunächst als fremd, formatierte ihn erst nach konkreter Freigabe, masterte die erfundene AID `A1B2C3` und bestätigte anschließend 16/16 korrelierte Tasks sowie 3/3 bytegleiche finale Readbacks für alle zwölf Datenbausteine; die Runtime wurde automatisch geschlossen.
- 47/47 automatisierte Tests, vollständiger WPF-Publish und UI-Smoke-Test bestanden.
- Installer-Version auf 1.1.7 angehoben.

## SieEncode 1.1.0-rc7 – generische Kodiermatrix und belastbarere Laufdiagnose

- Der installierte Ein-Klick-Funktionstest baut vier vollständig erfundene Projekte über denselben Produktivpfad: AES, DES, 3K3DES, Standard-/Backup-Dateien, Plain/MAC/Encrypt, temporäre Dateihärtung, beide Key-Wechselmodi und alle vier Payload-Bausteine.
- Der Matrixbericht trennt Software- und Hardwareevidenz ausdrücklich. Er behauptet keine Transponderfreigabe für Varianten, die nur als Artefakt gebaut wurden.
- Direkte und zentrale Builds erzeugen alle Payload-, Readback- und Verifikationsordner vor dem RFiDGear-Start.
- Strukturierte RFiDGear-Ergebnisse erhalten bei einem Prozessende ihre Task- und Readback-Korrelation; erwartete Kontrollflussfehler werden nicht mehr fälschlich als Hauptursache gemeldet.
- Atomare Kartenprüfberichte tolerieren kurzzeitige Windows-Dateisperren begrenzt, ohne dauerhafte Berechtigungsfehler zu verbergen.
- Releasepakete enthalten jetzt alle von der installierten Hilfe und dem Umgebungstest erwarteten Versions- und Betriebsdokumente; der Releasebau prüft deren Vorhandensein fail-closed.
- 46/46 Builder-/Runtime-Regressionstests sowie der sichtbare UI-/Kodiermodell-Selbsttest bestanden.
- Installer-Version auf 1.1.6 angehoben.

## SieEncode 1.1.0-rc6 – verifizierte Vault-Wiederherstellung und Kartenserien-Zuordnung

- Ein Vault gilt erst dann als betriebsbereit, wenn SieEncode ihn nach dem Speichern kryptografisch erfolgreich wieder geöffnet und dem aktuell geladenen Schlüsselsatz zugeordnet hat.
- Mastering-Builds verlangen einen konfigurierten Sicherungsort außerhalb der gesamten Projektbibliothek; ohne unabhängige Sicherung bleibt der Build fail-closed gesperrt.
- Das versionierte Recovery-Paket enthält ausschließlich die keyfreie Projektdefinition, den unverändert verschlüsselten Vault und ein keyfreies SHA-256-Manifest. Kopien werden nach dem Schreiben bitgenau geprüft.
- Eine stabile SHA-256-Keyset-ID verbindet Vault-Backups, Kodierschema, JSONL-Laufprotokoll und Serienzusammenfassung, ohne einzelne Schlüsselwerte offenzulegen.
- Alte Kodierschemata ohne Keyset-ID bleiben lesbar und werden eindeutig als Altprojekt ohne Zuordnung behandelt.
- Das Abnahme-Kit übernimmt seine sichtbare Version aus dem aktuellen Paketmanifest statt aus fest verdrahteten Altständen; der Releasebau verwirft Fremdversionen künftig automatisch.
- Fünf-Karten-Evidenz zählt nur noch für exakt dieselbe installierte Produktversion sowie identische Studio-/Runtime-SHA-256 und eine vorhandene schlüsselfreie Keyset-ID; ältere Seriennachweise können einen neuen Build nicht mehr freigeben.
- Installer-Version auf 1.1.5 angehoben.

## In Entwicklung – schlüsselfreie Projekte und prozessgebundene Laufzeitfreigabe

- Neue `rfPrj`-Dateien enthalten für DESFire, MIFARE Classic und MAD ausschließlich opake Secret-Referenzen; Klartext-Schlüsselfelder sperren den Build-Preflight.
- Transport- und Produktivschlüssel werden beide aus der Projektdefinition entfernt und im empfängergebunden verschlüsselten Vault gespeichert.
- SieEncode gibt Schlüssel nur sitzungsweise über eine `CurrentUserOnly`-Named-Pipe frei; SieEncode und RFiDGear prüfen gegenseitig ihre Windows-Prozess-IDs.
- RFiDGear löst Referenzen erst direkt am Kartenkommando auf, stellt sie auch bei Teilfehlern wieder her und bricht bei fehlenden oder ungültigen Secrets fail-closed ab.
- RFiDGear liest `taskdatabase.xml` direkt aus dem Archiv in den Speicher; der bisherige Klartext-Zwischenschritt unter `%LOCALAPPDATA%` entfällt.
- RFiDGear blockiert das erneute Speichern von Projekten mit Klartext-Schlüsselfeldern. Altprojekte bleiben lesbar und werden als migrationsbedürftig gekennzeichnet.
- Leakage-Tests bewerten ausschließlich bekannte Schlüsselfelder; Nullfolgen in regulären Datei-Nutzdaten werden nicht als Schlüsselbeweis missverstanden.

## SieEncode 1.0.1 – Modusführung und DEVK-Projektmigration

- Die doppelte Moduskennzeichnung aus Badge und Standard-Checkbox wurde durch einen einzelnen kompakten Modusschalter mit eindeutiger Normal-/Expertenanzeige ersetzt.
- Veraltete RC8-Versionsangaben in Profil-Editor, Profilhistorie, rfPrj-Import und Tutorial wurden bereinigt.
- Das allgemeine Projektfeld `firmCode` wird im DEVK-Profil jetzt korrekt als `Firmencode` statt kundenspezifisch bezeichnet.
- Der alte DEVK-v6.2-Bauordner kann sicher als neues, keyfreies Projekt `DEVK_migriert` übernommen werden; die Quelle bleibt unverändert.
- Installer-Version auf 1.0.39 angehoben.

## SieEncode 1.0.0 – internes Final Release

- RC17 wird nach vollständiger Software-, UI-, Installer- und Hardwareabnahme unverändert als technische Basis des internen Final Release übernommen.
- Sämtliche sichtbaren RC-/Pilotkennzeichnungen wurden entfernt; Kopfzeile, Fenstertitel, Tutorial und Paketnamen zeigen konsistent Version 1.0.0.
- Kompatible Altprojekte bleiben im Normalmodus sicher nutzbar, ohne einen nicht vorhandenen Vault oder die additive Erkennungsfunktion vorauszusetzen.
- Installer-Version auf 1.0.38 angehoben; das Upgrade von RC17 bleibt ein echtes Major Upgrade mit stabilen Komponentenidentitäten.
- Fehlendes Code Signing ist für die ausschließlich interne Verteilung bewusst dokumentiert und bleibt vor jeder externen Verteilung ein Pflicht-Gate.

## SieEncode 1.0.0-rc17 – Altprojekte im Normalmodus sicher bedienbar

- Kompatible Altprojekte zeigen ihre vorhandene, rein lesende Transportprüfung jetzt direkt im normalen Kartenstatus-Schritt; Nutzer müssen dafür nicht mehr in den Expertenmodus wechseln.
- Erklärung und Schaltflächen unterscheiden vollständige Transponder-Erkennung und eingeschränkte Altprojekt-Prüfung eindeutig.
- Der reproduzierbare installierte UI-Hardwaretest verwendet stabile Automation-IDs und wählt bei Altprojekten automatisch die sichtbare sichere Ersatzprüfung.
- Installer-Version auf 1.0.37 angehoben.

## SieEncode 1.0.0-rc16 – kompatibler Altprojektbetrieb

- Projektbibliothek trennt jetzt klar zwischen `Nutzung` und `Neubau / Schlüssel`: Ein vorhandener vollständiger Build bleibt ohne Vault verwendbar, während Bearbeiten und reproduzierbarer Neubau gesperrt bleiben.
- Projekte aus RC15 und älteren Versionen ohne das additive Read-only-Erkennungsmodul werden als `Altbuild nutzbar` statt als vollständig fehlerhaft angezeigt.
- Der Betriebs-Preflight behandelt ausschließlich das Fehlen von `card-recognition.plan.json` und `_CardRecognition_ReadOnly.rfPrj` als dokumentierte Einschränkung. Alle übrigen Struktur-, Sicherheits-, Pfad-, Task- und Zuordnungsfehler bleiben harte Sperren.
- Kodierstation stellt beim Altbuild weiterhin vorhandene Transport-/Produktivprüfung, Mastering, Kodierung und Recovery bereit; nur `Transponder erkennen` bleibt gezielt deaktiviert.
- Projektinfo, Startseite, Detaildialog, Projektprüfung, Bedienempfehlung und Tutorial erklären, was weiter funktioniert und wofür der ursprüngliche Vault benötigt wird.
- Sichtbarer installierter Hardwaretest klappt die vertiefte Transportprüfung nun reproduzierbar über die Benutzeroberfläche auf.
- Installer-Version auf 1.0.36 angehoben.

## SieEncode 1.0.0-rc15 – professionelle Oberfläche und ehrlicher Vault-Status

- Gemeinsames WPF-Designsystem eingeführt: Standardbuttons exakt 40 px, Navigation 44 px, konsistente Primär-/Sekundär-/Löschaktionen, moderne Bereichskarten, Tabellen, Eingabefelder, Abstände, Fokus-, Hover- und Deaktiviertzustände.
- Übersicht auf einen klaren Einstieg, Systemstatus und einen kompakten sicheren Arbeitsablauf reduziert; doppelte Aktionen und doppelte Seitenüberschriften entfernt.
- Projektverwaltung neu geordnet: sichtbare Löschaktion, stabile Automation-ID für „Zur Kodierstation“, eindeutige Aktionsfreigaben erst nach Auswahl und verständliche Spalten für Bereitschaft und Schlüsselstatus.
- Projektkatalog unterscheidet gültig verschlüsselten Vault, fehlenden oder beschädigten Vault und reine Bauartefakte. Definitionen ohne Vault werden nicht länger irreführend als normal bearbeitbar bezeichnet, sondern als nicht reproduzierbar markiert.
- Editor deaktiviert Vault-Aktionen zustandsabhängig, erklärt Altbuilds mit fehlendem Vault und verlangt vor neuen Schlüsseln in bestehenden Projekten eine bewusste Warnbestätigung.
- Kodierstation zeigt vor Projektwahl nur den notwendigen ersten Schritt; Kartenprüfung, Serienmodus, Kodierung und Abschluss erscheinen erst nach dem Laden eines Projekts. Technische Pfade bleiben im Expertenmodus.
- Installierter UI-Testtreiber verwendet stabile Automation-IDs statt sichtbarer Buttontexte; Design-, Bedienmodus-, Layout-, Projekt- und Stationsverträge wurden erweitert.
- Pilottest-Kit und Release-Gates auf RC15 aktualisiert; Installer-Evidenz wird dynamisch aus installiertem Produkt und aktuellem Paket gebildet.
- Identische technische Projektprüfungsbefunde werden für Bediener zu einer Aussage zusammengefasst; die vollständige Dateizuordnung bleibt sichtbar.
- WiX-Komponenten erhalten dauerhaft stabile, aus ihren Zielpfaden abgeleitete GUIDs; frühe Pilotpakete mit zufälligen Komponentenidentitäten können Runtime-Dateien beim Upgrade nicht mehr unbemerkt entfernen.
- Upgrade-Reihenfolge und Dateierneuerung sind für den exklusiven SieEncode-Installationsordner explizit festgelegt; Installer-Version auf 1.0.35 angehoben.

## SieEncode 1.0.0-rc14 – geführter Hardwareabschluss

- Fehlgeschlagene `WriteData`-Tasks mit fehlender Eingabedatei werden aus der strukturierten Runtime-Evidenz konkret als „Datensatz/Payload nicht vorbereitet“ erklärt; die UI nennt betroffenen Task und Datei sowie die sichere Wiederholungsaktion.
- Nach erfolgreichem Mastering zeigt die Kodierstation ausdrücklich den vorgesehenen Transport-Zwischenzustand, verlangt als nächsten Nachweis die Transportprüfung und warnt vor einem erneuten Mastering.
- Ein autoritativ terminal abgeschlossener, aber fensterlos weiterlaufender AUTORUN-Prozess wird nach exakter Prozess-ID- und EXE-Prüfung kontrolliert beendet; aktive oder nicht terminal bestätigte Läufe bleiben unverändert geschützt.
- Realer ELATEC-/DESFire-Hardwarelauf: sichere Kartenidentifikation, Recovery 2/2, Leernachweis mit 0 Applications, Mastering 27/27, Transportread 6/6, Kodierung 26/26, Produktivread 6/6 und drei unabhängige bytegleiche Payload-Nachweise.
- Installer-Version auf 1.0.32 angehoben.

## SieEncode 1.0.0-rc13 – verständliche Projektprüfung

- Projektprüfungsfehler erscheinen direkt in der Projektverwaltung als Tabelle mit Bereich, verständlicher Ursache, betroffener Datei und konkreter Reparatur.
- Sperrmeldungen vor Kodierung, AUTORUN und Schnellstart enthalten nun dieselben Ursachen und Handlungsschritte statt eines allgemeinen Fehlertexts.
- Altbuilds ohne das neue strikt lesende Transponder-Erkennungsmodul werden ausdrücklich erkannt und zum kontrollierten Neubau mit vorhandenem Vault angeleitet.
- Fehlerhafte reine Bauartefakte können direkt als neuer bearbeitbarer Entwurf geöffnet werden; die Quelle bleibt unverändert.
- Erfolgreiche Prüfungen zeigen Umfang und verbleibende Einschränkungen ebenfalls direkt an.
- Installer-Version auf 1.0.31 angehoben.

## SieEncode 1.0.0-rc12 – geführte Projekterstellung und Verwaltung

- Projektbibliothek um eine gut sichtbare Neuanlage und eine Suche über Projektname, Status, Profile und Ordner ergänzt.
- Normalmodus reduziert die Tabelle und Aktionen; technische Build-, Profil- und Pfaddetails bleiben im Expertenmodus verfügbar.
- Projekteditor zeigt Profil-, Pflichtfeld-, Schlüssel/Vault- und Speicherbereitschaft sowie den jeweils nächsten notwendigen Schritt.
- Technische Buildfelder und sensible Schlüsselwerte sind standardmäßig eingeklappt, bleiben aber vollständig erreichbar.
- Bauen wird erst nach vollständigen Pflichtfeldern, gültiger Profilbindung und verschlüsselt gesicherten Sitzungsschlüsseln freigegeben.
- Neue gleichnamige Entwürfe überschreiben bestehende Projektdefinitionen nicht mehr still.
- Installer-Version auf 1.0.30 angehoben.

## SieEncode 1.0.0-rc11 – Normal- und Expertenmodus

- Normalmodus als gespeicherter Standard: Projektwahl, Bearbeitung, geführte Kodierung, Hilfe und Einstellungen bleiben unmittelbar erreichbar.
- Profilverwaltung, Dry-Run/Abnahme, Transportkey-Einstellungen, Recovery, manuelles RFiDGear und technische Laufdetails erscheinen erst im Expertenmodus.
- Moduswechsel ist direkt in der Titelzeile sichtbar und wird im Benutzerprofil gespeichert.
- UI-Vertrag prüft automatisiert, dass die reduzierte Ansicht keine Funktionen entfernt und der Expertenmodus alle Fachoberflächen wiederherstellt.
- Versionsanzeige im Seitenbereich korrigiert; Installer-Version auf 1.0.29 angehoben.

## SieEncode 1.0.0-rc10 – eindeutige Erkennungsanzeige

- Die sichtbare RFiDGear-Tasktabelle trennt nun technische Ausführung und fachliches Ergebnis: `TREFFER`, `KEIN TREFFER`, `NICHT PRÜFBAR` sowie die Zahl inventarisierter Applications werden direkt angezeigt.
- Deutsche und englische Bezeichnungen für die beiden Erkennungs-Tasktypen ergänzt; die zuvor protokollierten fehlenden Ressourcen sind behoben.
- Realer read-only Hardwarelauf auf belegtem Transponder: 4/4 Tasks, vollständige Inventur mit `F482D0`, `F51773`, `F52348`, alle Standard-/Transport-PICC-Proben ohne Treffer; damit eindeutig nicht leer.
- Runtime auf 2.0.10.5 und MSI auf 1.0.28 angehoben, damit der korrigierte Kandidat nicht still unter den RC9-Binärnamen ersetzt wird.

## SieEncode 1.0.0-rc9 – native Transponder-Erkennung

- Neuer primärer, strikt lesender Erkennungsmodus vor Datei-Preflight und Kodierung: vollständige DESFire-AID-Inventur plus getrennte PICC-Authentisierungsproben für Standard-DES, Standard-AES, Transport und – sofern vorhanden – Produktiv.
- „Leer“ wird ausschließlich nach erfolgreicher vollständiger Inventur mit exakt null Applications ausgegeben. Nullen in Dateien, fehlende Projekt-AIDs, `ReadData`-Fehler oder `AuthFailure` sind ausdrücklich kein Leernachweis.
- PICC-Zustände werden als Standard, Transport, Produktiv, unbekannt oder transparent mehrdeutig ausgegeben. Mehrdeutigkeit ist korrekt, wenn mehrere konfigurierte Zugänge identisch oder kryptografisch äquivalent sind; es wird keine Schlüsselsuche durchgeführt.
- Kartenstruktur unterscheidet leer, erwartete AIDs vollständig, erwartete AIDs teilweise, fremd und gemischt. Ergebnis und nächste sichere Aktion erscheinen laienverständlich; ein keyfreier Detailbericht bleibt für Support erhalten.
- Die bisherigen Transport-/Produktiv-Dateiprüfungen bleiben vollständig erhalten, sind aber als vertiefte Projektprüfung untergeordnet.
- Der Fünf-Karten-Lauf RC8.1 gilt als belastbarer technischer Mehrkartenlauf: fünf physische Karten, fortlaufende Nummern, je 26/26 Tasks, korrelierter Terminalstatus, drei bytegleiche Readbacks, Duplikatschutz und Runtime-Abschluss. Er gilt nicht als vollständige Usability-/Durchsatzsimulation, weil Kartenwechsel menschlich, die UI-Aktionen jedoch automatisiert waren und die damalige Blankoklassifikation noch im Testtreiber lag.
- Runtime 2.0.10.4 erweitert RFiDGear um die read-only Tasks `PICCApplicationInventory` und `PICCAuthCheck` und schreibt deren keyfreie Ergebnisse in strukturierte Task-Evidenz.
- 42/42 SieEncode-/Paritätsregressionen und 177/177 Regressionen des paketierten SieEncode-Runtime-Zweigs bestanden. Die neue Runtime benötigt vor Freigabe noch einen sichtbaren Hardwarelauf mit Blanko- und bereits belegtem Testtransponder.

## SieEncode 1.0.0-rc8.1 – belastbarer terminaler Laufabschluss

- Vollständige bytegleiche Readbacks schließen einen Kodierlauf nicht mehr vorschnell ab, während der von SieEncode gestartete RFiDGear-Prozess seinen korrelierten Terminalstatus noch schreibt.
- Ein begrenztes Nachlaufffenster hält Karte und Datensatz sichtbar fest; ausschließlich ein vollständiger `TaskLoop.Success` gibt den Transponder weiterhin frei.
- Fehlt der Terminalstatus nach Ablauf des Fensters oder ist RFiDGear bereits beendet, bleibt der Lauf unverändert fail-closed und erzeugt einen Fehlernachweis.
- Regressionstest bildet den im Hardwarelauf gemessenen 36-ms-Versatz zwischen letztem Taskresultat und `TaskLoop.Success` ab.
- Der sichtbare Mehrkartentest erkennt die nach erfolgreicher Kodierung sofort angezeigte „NÄCHSTE NUMMER“ nun über die erhaltene Freigabe-Evidenz; eine zusätzliche fail-closed Nachweisaktion kann einen bereits erfolgreichen Lauf anhand UI, Checkpoint und korreliertem Hostvertrag übernehmen, ohne die Karte erneut zu beschreiben.
- Neue Blankotransponder besitzen naturgemäß noch keine lesbaren Projekt-AIDs. Der RC8.1-Testtreiber verwendete hierfür einen ausdrücklich bestätigungspflichtigen provisorischen Testpfad aus fehlenden Projekt-AIDs plus Reader-/Chip-Erkennung. Diese Testheuristik war kein allgemeiner Leernachweis und wird ab RC9 durch die native vollständige AID-Inventur ersetzt.
- Der Sitzungsabschluss erweitert den zuvor eingeklappten Bereich „5 · Abschluss und Sitzungsverlauf“ automatisch, bevor die Zusammenfassung exportiert wird.
- Der Releasebuild erzeugt neben MSI und Portable-ZIP ein sauberes eigenständiges Pilottest-Kit-ZIP ohne alte Evidenz oder aktive Sitzungszeiger.
- Installerrevision auf 1.0.26 erhöht; die fingerprintete Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc8.0 – Pilottest-Kit

- Eigenständiger Windows-PowerShell-5.1-Sammler für reale DPI-, Benutzer-, Signatur-, Versions- und Pfadrechte-Evidenz ohne Quellcodeabhängigkeit.
- SID, Rechner und konfigurierte Pfade werden ausschließlich als SHA-256-Hashes ausgegeben; Schlüssel, Vaults, Projekte und Kartennutzdaten sind ausgeschlossen.
- Neutrale Testbögen führen zwei unabhängige Bedienpersonen und fünf physische Testkarten reproduzierbar durch die verbleibenden Feldgates.
- Die fail-closed Gesamtauswertung dedupliziert SID-/DPI-Nachweise, weist unveränderte Formulare ab, verlangt vorhandene Evidenz je Karte und kann die Codesignatur nicht aus extern editierbaren JSON-Dateien freigeben.
- Der PowerShell-5.1-Sammler setzt vor der DPI-Messung explizit Per-Monitor-DPI-Awareness; dadurch wird 125/150 % nicht durch Windows-DPI-Virtualisierung fälschlich als 100 % erfasst.
- Reale sichtbare UI-Smoke-Tests bei 100 %, 125 % und 150 % Windows-Skalierung sind vollständig bestanden und als getrennte DPI-Evidenz konsolidiert.

## SieEncode 1.0.0-rc8.0 – Grundprofilbibliothek und geführter Projektworkflow

- Ein gemeinsamer, in den Einstellungen wählbarer Profilstamm verwaltet getrennt administrative Grundprofile, bearbeitbare Kunden-/Zusatzprofile und deren Historie. Der Speicherort bleibt versionsunabhängig und kann von mehreren Benutzern erreicht werden.
- Die Bibliothek unterstützt mehrere Grundprofile parallel. Jedes Projekt bindet weiterhin genau ein Grundprofil und beliebig viele konfliktfreie Zusatzprofile; Neuprojekte wählen das bewährte Grundprofil deterministisch vor.
- Grundprofile können validiert aus Profil-JSON oder schreibgeschützt und schlüsselwertfrei aus vorhandenen rfPrj-Dateien abgeleitet werden. Vor der Übernahme werden ID, Version, Application-Anzahl und Inhaltsfingerprint gezeigt.
- Inhaltliche Änderungen eines Grundprofils verlangen eine neue Version, archivieren den Vorgänger und werden durch bestehende Projektbindungen niemals still übernommen. Revisions- und Dateisperren schützen parallele Schreibvorgänge.
- Der Projekteditor führt nun sichtbar durch Profilzusammenstellung, Projektwerte, Produktivschlüssel/Vault, Speichern sowie Dry-Run/Build. Transportkeys bleiben vollständig als eingeklappte Experteneinstellung erhalten.
- Die kontextbezogene Tour umfasst neun Schritte einschließlich Grundprofilverwaltung und automatisch überwachtem RFiDGear-Laufende. Veraltete RC5-Titel der Nebenfenster wurden auf RC8.0 korrigiert.
- Der sichtbare UI-Smoke adressiert 208 interaktive Flächen, ordnet alle zehn Seiten zusätzlich bei 1100×720 an und verlangt vertikalen Zugriff auf lange Arbeitsseiten. Eine echte DPI-Feldprüfung bei 100/125/150 % bleibt separates Release-Gate.
- Testsuite auf 41 Kern-/Sicherheitsregressionen erweitert. Installerrevision auf 1.0.25 erhöht; die fingerprintete Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc7.1 – Terminale Runtime-Führung

- Nach einer belastbar terminal ausgewerteten Kartenaktion oder Kodierung versucht SieEncode nun, ausschließlich den selbst gestarteten RFiDGear-Prozess regulär über dessen Hauptfenster zu schließen.
- Der normale Kodierlauf wird nach dem Start jede Sekunde automatisch auf korrelierten Terminalstatus und finale Readbacks geprüft. Die bisherige dritte Aktion bleibt als optionale Sofortaktualisierung erhalten.
- Ein aktiver oder noch nicht ausgewerteter Lauf bleibt unverändert geschützt. Ein erzwungenes Beenden findet nie automatisch statt und erfordert weiterhin eine ausdrückliche Bestätigung.
- Kann RFiDGear nicht regulär geschlossen werden, bleibt das Ergebnis erhalten und die zentrale Bedienentscheidung nennt sichtbar `RFIDGEAR NOCH OFFEN`, verlangt Karte und Datensatz unverändert zu lassen und führt zur bestätigungspflichtigen Abschlussfunktion.
- Die ursprüngliche Ergebnisbewertung und nächste fachliche Aktion bleiben auch nach automatischem oder manuellem Schließen sichtbar.
- Installerrevision auf 1.0.24 erhöht; die fingerprintete Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc7.0 – Pilotkandidat

- Neue primäre Bedienentscheidung in der Kodierstation: Ergebnis, verständliche Begründung, nächste sichere Aktion und kompakter Engine-/Readbacknachweis stehen vor den technischen Details.
- Kartenfreigabe verlangt jetzt gemeinsam korrelierten `TaskLoop.Success`, den vollständigen Taskplan und alle frischen bytegleichen Readbacks. Passende Readbacks ohne autoritativen Laufnachweis werden fail-closed blockiert.
- Fehlerkategorien Key/Auth, Reader/Transport, Kartenwechsel, Readback, Berechtigung/Protokoll und Runtime-Ausnahme liefern konkrete sichere Bedienhandlungen.
- Seriennachweis Schema 1.1 speichert Bedienentscheidung, Empfehlung, Evidenz und `cardReleased`, weiterhin ohne Schlüsselwerte.
- Normaler Kodierweg auf drei nummerierte Aktionen reduziert; manuelles RFiDGear, Serienfunktionen, Mastering, Recovery und technische Evidenz bleiben vollständig als Experten-/Erweiterungsfunktionen erhalten.
- Tour, Pilotabnahme und neues Bedien-/Freigabekonzept an die verbindliche Laufentscheidung angepasst.
- Installerrevision auf 1.0.23 erhöht; Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc6.7

- Produktiv-Preflight kann den keyfreien Sollzustand auch aus älteren `expected-card.json` ohne gespeicherte Payload-Hashes rekonstruieren.
- Payload-Pfade werden für den Hashvergleich unabhängig von `payloads\\`-Präfix und Slash-Schreibweise kanonisch zugeordnet.
- Installierte RC6.7 sichtbar mit ELATEC/DESFire abgenommen: Produktivprobe 6/6 mit 3/3 Payload-Matches und Application-/KeySettings-Prüfung 6/6 erfolgreich; die vorausgehenden RC4-Läufe belegen zusätzlich Recovery 2/2, Mastering 27/27, Kodierung 26/26 und finale Readbacks 3/3.
- Installerrevision auf 1.0.22 erhöht; Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc6.6

- Der persistente Produktiv-Preflight vergleicht Nutzdaten nach F8 über Länge und SHA-256 der erwarteten Payloads. Die absichtlich invalidierten temporären Kodierdateien werden nicht mehr fälschlich als Datenabweichung gemeldet.
- Bereits vorhandene `expected-card.json`-Dateien ohne Hashliste bleiben kompatibel: Der Sollzustand wird deterministisch aus `coding.schema.json` und den gespeicherten Sitzungswerten rekonstruiert.
- Installerrevision auf 1.0.21 erhöht; Runtime RC4/RFiDGear 2.0.10.3 bleibt binär unverändert.

## SieEncode 1.0.0-rc6.5 – in Entwicklung

- Separate Kartenaktionen sind während eines vorbereiteten Kodierdatensatzes fail-closed gesperrt. Recovery, Mastering oder Prüfläufe können damit den Abschlusszustand einer laufenden Karte nicht mehr überlagern.
- Ein versehentlicher zweiter Klick auf `Datensatz bereitstellen` weist den Doppelstart weiterhin ab, lässt F8/F9 und die übrigen Abschlussaktionen des bestehenden Datensatzes aber bedienbar.
- Installerrevision auf 1.0.20 erhöht; die Runtime bleibt bytegleich RC4/RFiDGear 2.0.10.3.

## SieEncode 1.0.0-rc6.4 – in Entwicklung

- Der sichtbare RC6.3-Bedienlauf entdeckte einen startkritischen Brandingfehler: Headless-Läufe funktionierten, aber `RFiDGear nur öffnen` blieb ohne Hauptfenster, weil drei WPF-Ressourcenpfade noch fest auf die Assembly `RFiDGear` zeigten. Runtime 2.0.10.3 verwendet assemblyneutrale Ressourcenpfade und öffnet wieder ein bedienbares Fenster.
- Runtime-Fenstertitel und About-Text werden nun aus der tatsächlichen Assembly-Produktversion gebildet statt eine alte RC2-Bezeichnung fest zu verdrahten.
- Installerrevision auf 1.0.19 erhöht; RC6.4 wird erneut über die installierte sichtbare UI und den freigegebenen ELATEC/DESFire-Testtransponder abgenommen.

## SieEncode 1.0.0-rc6.3 – in Entwicklung

- Der reale RC6.2-Bedienlauf bestand Recovery, Mastering und Kodierung mit 26/26 Tasks sowie drei frischen bytegleichen Readbacks. Dabei wurde sichtbar, dass der Produktiv-Preflight nach F8 unnötig gesperrt wurde; RC6.3 verwendet dafür nun die persistente `card_preflight/expected-card.json` statt die bereits abgeschlossene In-Memory-Sitzung.
- Die separate Anwendungsabnahme reproduzierte zwei sporadische `ReadAppSettings`-Transportfehler, während Keyanzahl und dritte Application erfolgreich waren. Die gebündelte Runtime 2.0.10.2 wiederholt SearchTag, Application-Select und GetKeySettings bis zu dreimal mit frischer Sitzung und schreibt die eigentliche Exception strukturiert ins Log.
- Installerrevision auf 1.0.18 erhöht; Runtime- und Produktiv-Preflight-Korrekturen werden erneut über die sichtbare UI mit dem freigegebenen ELATEC/DESFire-Testtransponder abgenommen.

## SieEncode 1.0.0-rc6.2

- RC6.0: Eigenständige Arbeitslinie auf Basis der vollständig abgenommenen RC5.6 eröffnet; Installerrevision 1.0.15.
- RC6.0: Fokus auf geführten Pilotworkflow, Mehrbenutzer-Vault-Abnahme, Serienbetrieb und releasefähige Signierhaken.
- RC6.0: Der Editor kann einen vorhandenen Vault keyfrei auf Integrität, Projektvollständigkeit und Benutzer-/Rechnerportabilität prüfen und zeigt nur Metadaten sowie einen gekürzten Dateifingerprint.
- RC6.0: Die Übersicht führt als kompakter Pilotstart durch Arbeitsplatz, Kodiervorschrift, Vault und Kodierstation und zeigt den jeweils erreichten Zustand an.
- RC6.0: Die kontextbezogene Tour umfasst jetzt Projekt, Sitzungsschlüssel, Mehrbenutzer-Vault, Prüfung, Karte, Kodierung und Serienabschluss in acht klar getrennten Schritten.
- RC6.0: Der Releasebuild besitzt optionale SHA-256-Authenticode-/Timestamp-Haken für Studio, Runtime und MSI sowie einen fail-closed `-RequireSignature`-Modus und einen maschinenlesbaren Signaturbericht.
- RC6.0: Die vollständige Regression öffnet denselben Projekt-Vault in zwei unabhängigen CLI-Prozessen und vergleicht ausschließlich keyfreie Portabilitäts- und Vollständigkeitsnachweise.
- RC6.0: Der UI-Regressionslauf bedient die sichtbaren Modi `Fortlaufend` und `Liste`, übernimmt Kartendaten, schaltet weiter und bestätigt das eindeutige Listenende ohne Hardwarezugriff.
- RC6.0: Eine reproduzierbare Zwei-Benutzer-/Mehrkarten-Pilotabnahme und die selektive RFiDGear-Upstream-Entscheidung sind dokumentiert.
- RC6.1: Neuer Paketstand mit Serien-UI-Vertrag, Mehrbenutzer-Abnahmeanleitung und Installerrevision 1.0.16.
- RC6.1: Bedien-, Pilot-, Vertrags- und Signaturdokumentation wird im Installer und im portablen Paket mitgeliefert.
- RC6.2: Reale UI-Hardwareabnahme deckte einen reentranten Doppelstart über gleichzeitig offene Sicherheitsdialoge auf. Eine Start-Sperre deaktiviert nun alle Reader-/Kartenaktionen bereits vor dem ersten Dialog, weist weitere Startversuche ab und stellt danach exakt den Capability-Zustand wieder her.
- RC6.2: Installerrevision auf 1.0.17 erhöht; der Hardwarelauf beginnt nach erfolgreichem Transport-Read und erfolgreicher Transport-Recovery erneut auf dem definiert formatierten Testchip.

## SieEncode 1.0.0-rc5.6 – 2026-08-26

- RC5.6: Die sichtbare keyfreie Task-Ergebnistabelle wird jetzt sowohl bei Betriebsaktionen als auch bei normalen Kodierläufen aus derselben Run-ID-korrelierten Auswertung befüllt.
- RC5.6: Installerrevision auf 1.0.14 / Paketrevision rc5.6 erhöht.
- RC5.5: Die Paketrevision ist jetzt Bestandteil der sichtbaren Produktversion und des Fenstertitels. Dynamische Projekt-, Transportkey-, Produktivkey- und Kartendatenfelder besitzen stabile fachliche Automation-IDs.
- RC5.5: Die Kodierstation zeigt die korrelierten keyfreien Taskresultate mit Position, Task-ID, Typ, Entscheidung, Outcome, Fehlercode, Skip-Grund, Terminalereignis und gekürztem Run-ID-Fingerprint sichtbar an.
- RC5.5: Installerrevision auf 1.0.13 / Paketrevision rc5.5 erhöht.
- RC5.4: Readback-Dateien zählen nur noch dann als Lesbarkeitsnachweis, wenn die zugehörige Probe terminal erfolgreich war. Damit bleiben erwartete Authentifizierungsfehler des jeweils falschen Schlüsselsatzes zulässig, während veraltete Dateien fehlgeschlagener Läufe keinen Mischzustand mehr vortäuschen; Regressionstests ergänzt.
- RC5.4: Installerrevision auf 1.0.12 / Paketrevision rc5.4 erhöht.
- RC5.2: Der Laufzeitvertrag akzeptiert jetzt sowohl die mit RFiDGear 2.0.10 kompatiblen `ReadAppSettings`-/`CheckAppKeyCount`-Paare als auch zukünftige Abnahmen mit zusätzlichen `CheckFileSettings`-Tasks. Dadurch blockiert eine korrekt migrierte Anwendungsprüfung weder Projektprüfung noch Karten-Preflight.
- RC5.2: Regressionstest für den vollständigen Betriebskatalog mit kompatibilisierter Anwendungsprüfung ergänzt; Installerrevision auf 1.0.10 / Paketrevision rc5.2 erhöht.

- Reale ELATEC-/DESFire-Vollabnahme wiederholt: Format 1/1, Mastering 27/27,
  Transport-Read 6/6, Kodierung 26/26, drei frische Nutzdaten-Readbacks
  bytegleich, Produktiv-Read 6/6, alte Transportkeys 6/6 abgewiesen, Recovery
  2/2, erneutes Mastering 27/27 und finaler Transport-Read 6/6.
- Falsches Grün geschlossen: `TaskLoop.Success` gilt nur noch, wenn der
  vollständige angeforderte Taskplan korreliert ausgeführt wurde. Ein leerer
  Lauf (`TotalTasks=0`) ist ausdrücklich ein Fehler.
- RFiDGear-2.0.10-Inkompatibilität abgefangen: `CheckFileSettings` ist als
  Marker vorhanden, aber nicht als rfPrj-Tasktyp ladbar. RC5 erzeugt deshalb
  nur ausführbare Application-Prüfungen; FileSettings bleiben sichtbar
  ungeprüft.
- Kodierstation entschlackt: klarer Fünf-Schritt-Ablauf, optionale Serie und
  Sitzungsdetails eingeklappt, kompakter Zurück-Button und präzisere Begriffe.
- Isolierte Programmfunktionsprüfung unter „Werkzeuge & Hilfe“ ergänzt.
- Version auf RC5 und Installerrevision auf 1.0.12 / Paketrevision rc5.4 erhöht.

## SieEncode 1.0.0-rc4 – 2026-08-25

- Kontextbezogenes sechsstufiges Tutorial über `Hilfe & Tour` ergänzt.
- Zentrales Designraster eingeführt: 38-px-Aktionen, 42-px-Navigation,
  36-px-Auswahllisten sowie einheitliche Padding-, Margin- und Style-Vererbung.
- UI-Vertrag von 144 auf 183 adressierbare Eingabe- und Statusflächen erweitert;
  automatischer Designcheck prüft 101 Schaltflächen und 9 Auswahllisten.
- Read-only Hardware-Smoke-Modus für Transport- und Produktivprüfung ergänzt.
  Nicht-ReadData-Projekte werden vor dem Start abgewiesen; RFiDGear wird nach
  terminalem Ergebnis kontrolliert beendet.
- Realer ELATEC-Test: Reader-Discovery und Chip-Hydration erfolgreich. Der
  aufliegende Transponder war weder mit Transport- noch mit den im Testprojekt
  hinterlegten Produktivkeys lesbar (jeweils 0/6, terminaler Fehler). Es wurde
  nichts auf die Karte geschrieben.

## SieEncode 1.0.0-rc3 – 2026-08-25

- Projektprüfung in drei klar getrennte Verträge aufgeteilt: strenge
  Build-Prüfung mit Sitzungsschlüsseln, schlüsselfreie Betriebsprüfung für
  bearbeitbare Projekte und strukturelle Prüfung reiner Bauartefakte.
- Vorhandene Altprojekte können damit wieder aus Projektliste und Kodierstation
  geprüft und geladen werden, ohne den produktiven Vault beim Bearbeiten zu
  verlangen; unvermeidbare Grenzen der Artefaktprüfung werden sichtbar genannt.
- Hauptnavigation auf sechs Arbeitsbereiche verdichtet. Seltene Projekt- und
  Kartenaktionen liegen in aufklappbaren Bereichen; die Kodierstation folgt nun
  einem nummerierten Normalablauf.
- Einheitliche Anwenderbegriffe `Projekt prüfen` und `Karte prüfen` ersetzen
  mehrdeutige Preflight-Schaltflächen.
- Vollständiger UI-Automation-Vertrag ergänzt: stabile eindeutige IDs für alle
  interaktiven Haupt- und Nebenfensterflächen sowie isolierter Smoke-Test für
  Navigation, Projektarten, Projektprüfung und Kodierstation.
- CLI-Diagnosen `preflight-operational` und `preflight-artifacts` ermöglichen
  dieselben Prüfungen ohne GUI-Klicks.
- Installerrevision rc3.1 / MSI 1.0.7; die separat hardwarefreigegebene Runtime
  2.0.10.1 bleibt absichtlich unverändert. Der reale Major-Upgrade-Lauf, beide
  Startmenülinks sowie Self- und UI-Smoke-Test der installierten Ausgabe sind
  bestätigt. Der Paketbau behandelt PowerShell-5.1-Skriptpfade und WiX-Ziele
  mit Leerzeichen nun eindeutig.

## SieEncode 1.0.0-rc2 – 2026-08-25

- Installerrevision rc2.5: Der direkte Station-Start trägt nun den korrekten Fenstertitel `SieEncode Station 1.0.0-rc2`; ein eigener Startmodus-Selbsttest schützt Studio- und Station-Titel. Unter Einstellungen kann die mitinstallierte, hardwaregeprüfte SieEncode Runtime sichtbar ausgewählt werden, ohne bewusst konfigurierte externe Runtimepfade still zu überschreiben. MSI-Version 1.0.6 aktualisiert rc2.4.
- Installerrevision rc2.4: Das Startmenü übernimmt nun vollständig die aktuell funktionierende WiX-Struktur des originalen RFiDGear-Installers: eigenes `StartMenu`-Feature, `ProgramMenuFolder`, separate `StartMenuShortcuts`-Komponente, nicht beworbene direkte EXE-Ziele und HKCU-Keypath. MSI-Version 1.0.5 aktualisiert rc2.3. Der Paketbau prüft diesen Vertrag direkt in der erzeugten MSI-Datenbank.
- Installerrevision rc2.3: Startmenue-Verknuepfungen sind nun Bestandteil derselben MSI-Komponente wie `SieEncode.exe`. Das beseitigt den auf dem Zielsystem nachgewiesenen Fall, dass Windows Installer eine separate Registry-basierte Shortcut-Komponente als installiert meldet, obwohl die `.lnk`-Dateien fehlen. MSI-Version 1.0.4 aktualisiert rc2.2.
- Installerrevision rc2.2: Startmenü-Verknüpfungen liegen in einem expliziten gemeinsamen Ordner `SieEncode`. Ein eigener revisionsspezifischer Komponenten-Keypath verhindert, dass Windows Installer eine fehlende Shortcut-Komponente wegen des bereits vorhandenen Installationspfad-Registrywerts fälschlich als installiert behandelt. MSI-Version 1.0.3 aktualisiert die vorherigen Installerausgaben.
- Einheitlicher Anwender-Einstieg `SieEncode.exe`; Studio und Station bleiben getrennte Betriebsarten derselben Anwendung.
- Eigenständiges SieEncode-Icon für EXE, Fenster, Taskleiste, Startmenü und MSI-Systemsteuerung ergänzt.
- Runtime als `SieEncode.Runtime.exe` mit Produktmetadaten, Icon und Fenstertitel „SieEncode Runtime – powered by RFiDGear“ co-gebrandet. Der interne Assembly-/Ressourcenname bleibt kompatibel `RFiDGear.dll`.
- RFiDGear-Herkunft, Copyright, MIT-Lizenz und Upstream-Link bleiben im Infofenster und Release erhalten.
- Direkte Upstream-Aktualisierung in der gebrandeten Runtime deaktiviert; Runtime-Updates erfolgen ausschließlich über hardwaregeprüfte SieEncode-Releases.
- Capability-Erkennung unterstützt gleichnamige Runtime-DLLs und den kompatiblen internen `RFiDGear.dll`-Namen.
- ELATEC-/DESFire-Schlüsselwechsel sind als wiederholbare Change-and-Verify-Transaktion gehärtet: Ein SDK-`NoError` gilt erst nach frischer Zielschlüssel-Authentifizierung; auch `ChangeKeySettings` baut die Reader-Session kontrolliert neu auf und protokolliert jeden Versuch.
- Neue Runtime erneut vollständig abgenommen: 177/177 Runtime-Tests, 40/40 Buildertests, Format, 27-Task-Mastering, 26-Task-Kodierung, 6/6 Produktivreads, negative 6/6-Transportkey-Gegenprobe sowie sichere Recovery bestanden.
- Exakter RC2-Runtimefingerprint in den fail-closed Vertrauenskatalog aufgenommen.

## SieEncode 1.0.0-rc1 – 2026-08-25

- Der bisherige Projektbuilder wird als **SieEncode Studio** weitergeführt.
- **SieEncode Station** öffnet die Kodierstation direkt über `--station`.
- Der Installer verwendet eine eigene Produktidentität, einen eigenen
  UpgradeCode und das getrennte Installationsverzeichnis `SieEncode`.
- Die vollständig hardwaregeprüfte Fork-Runtime 2.0.10.1 wird unter `Runtime`
  installiert und bei leeren Einstellungen automatisch erkannt. Der nur für
  read-only Dateiprüfungen abgenommene 2.0.10.2-Stand wird nicht vermischt.
- Bestehende Einstellungen werden einmalig aus
  `%APPDATA%\RFiDGear-Projektbuilder` nach `%APPDATA%\SieEncode` migriert.
- Frische Installationen verwenden den beschreibbaren Standardordner
  `Dokumente\SieEncode-Projekte` statt eines Unterordners im Programmverzeichnis.
- Die alte widersprüchliche Installer-EULA wurde nicht übernommen. MIT- und
  Drittanbieterhinweise werden getrennt mitgeliefert.

## 11.0-preview46 – 2026-08-20

- Der getrennte Fork-Build **RFiDGear Builder Runtime 2.0.10.1** integriert PR
  #205 und #208 auf Basis des offiziellen 2.0.10-Commits. Der kombinierte Stand
  bestand 177/177 Softwaretests und einen vollständigen ELATEC/DESFire-Lauf:
  26/26 Kodiertasks, 6/6 unabhängige Produktivreads, korrekte Ablehnung aller
  sechs alten Transportreads mit `TaskLoop.Failure` sowie Recovery 6/6.
- Produktive DESFire-Keywechsel werden ausschließlich für genau diesen Build
  freigegeben. Produktversion und deterministischer SHA-256-Evidenzfingerprint
  von `RFiDGear.exe` plus `RFiDGear.dll` müssen beide übereinstimmen.
  Versionsnummern oder Implementierungsmarker allein bleiben unzureichend.
- Der Runtime-Vertrauenskatalog ist Bestandteil von Capabilityprüfung,
  Hostvertrag und Regressionstests. RFiDGear bleibt ein separates externes
  Paket und wird nicht in das Builder-ZIP eingebettet.
- Projektdefinitionen, Profile, Vaults, Payloads und rfPrj-Dateien bleiben
  unverändert.

## 11.0-preview44 – 2026-08-20

- PR #206 wurde am realen ELATEC-/Testchip strikt lesend bestätigt: Typ,
  Dateigröße, Kommunikationsmodus und beide Access-Rights-Bytes von AID F482D0,
  Datei 0 stimmten exakt mit Grundprofil-9.1 überein. Der anschließende
  Transport-Readback war 6/6 erfolgreich; kein Authentifizierungsfehler und
  keine Kartenänderung.
- Der Builder enthält nun die additive Task-Shell `CheckFileSettings`. Das
  bisherige Application-Abnahmeprojekt prüft nach KeySettings und Keyanzahl
  zusätzlich jede projektierte Datei vollständig und strikt lesend.
- Die Bedienaktion heißt deshalb **Kartenstruktur-Abnahme · Produktiv** und ist
  nur verfügbar, wenn `ReadAppSettings` Exact-Match, `CheckAppKeyCount` und
  `CheckFileSettings` im konkret konfigurierten RFiDGear-Binary nachgewiesen
  sind. Ältere Installationen können alle bisherigen Projekte weiter nutzen.
- Erfolgreiche, RunId-/Taskplan-/rfPrj-korrelierte FileSettings-Ergebnisse
  werden schlüsselfrei im Abnahmebericht ausgewertet. Ohne aktuelle Evidenz
  bleiben die bisherigen „Nicht prüfbar“-Zeilen exakt erhalten; `IsNotTrue`
  wird als Settings-Abweichung ausgewiesen.
- Alle elf v10.39-Task-Shells bleiben bytegleich. Die neue zwölfte Shell ist
  isoliert additiv; Mastering, Kodierung, Karten-Preflight und Recovery bleiben
  semantisch unverändert.

## 11.0-preview43 – 2026-08-20

- Die neue Capability `AuthoritativeTaskLoopTerminalStatus` verlangt den
  vollständigen RFiDGear-PR-208-Vertrag aus `AcceptedFailures`,
  `UnhandledFailures`, `InvalidSkips` und `TerminalStatus`. Teilimplementierungen
  werden nicht als autoritativ erkannt.
- Der Builder bleibt mit RFiDGear 2.0.10 kompatibel und wertet weiterhin jedes
  korrelierte `Task.Outcome` fail-closed aus. Der neue Upstreamstatus ist ein
  zusätzlicher Engine-Nachweis und ersetzt nicht die getrennte Builder-Abnahme.
- PR #208 wurde mit dem ursprünglichen 26-Task-Hardwarefall bestätigt: ein
  `WriteData/Unknown`, 22 abhängige Skips und terminal `TaskLoop.Failure` statt
  des vorherigen falschen Erfolgs. Der anschließende Transport-Readback war 6/6
  erfolgreich; der Testchip blieb unverändert im Transportzustand.
- Die Upstreamübersicht berücksichtigt nun PR #205 für den Keywechsel, PR #206
  für die read-only FileSettings-Prüfung und PR #208 für den autoritativen
  Terminalstatus jeweils getrennt.

## 11.0-preview42 – 2026-08-20

- `VerifiedApplicationKeyChange` verlangt jetzt den vollständigen, auf ELATEC/TWN4
  nachgewiesenen Upstream-Vertrag: Nachauthentifizierung, frischer Provider-Kontext
  und autoritative Zielschlüsselprüfung auch nach einem mehrdeutigen `ChangeKey`-
  Fehler. Ein früher oder nur teilweise implementierter PR-Stand bleibt gesperrt.
- Builder-kompatible Hex-AIDs wie `0xF482D0` sind Teil des nachgewiesenen
  Keywechselpfads. Die unveränderte RFiDGear-Installation 2.0.10 bleibt blockiert;
  erst ein Build mit dem vollständigen PR-205-Vertrag wird freigegeben.
- Die Capability-Tests unterscheiden jetzt fehlende, teilweise und vollständige
  Keywechsel-Evidenz. Zusätzlich wird der reale installierte Build gegen den
  isolierten hardwaregeprüften Kandidaten getestet.
- Hardwareabnahme dokumentiert: Format, 27-Task-Mastering, 6-facher
  Transport-Readback, verifizierter Application-Keywechsel, unabhängiger
  Produktiv-/Transport-Gegenbeweis und abschließende Rücksetzung auf Transport.

## 11.0-preview41 – 2026-08-20

- `de.rfidgear.host.v1` verwendet jetzt Schema 1.1 und umschließt nicht nur
  Kodierung, sondern auch Mastering, Karten-Preflight, Application-Abnahme und
  Recovery. Request und Result besitzen formale Draft-2020-12-JSON-Schemas.
- Jeder angenommene Request bindet das konkrete rfPrj und den keyfreien
  Taskplan per SHA-256. Änderungen an Request oder rfPrj vor dem Abschluss
  werden abgewiesen; ein terminaler Enginezustand kann nicht zurückgesetzt oder
  auf einen anderen Lauf umgeschrieben werden.
- Das Resultat enthält geordnete, freitextfreie Taskpositionen, Entscheidungen,
  Skipgründe, Outcomes und Fehlercodes. Mehrere Läufe im selben Logdelta werden
  anhand der letzten terminalen RFiDGear-`RunId` getrennt; persistiert wird nur
  ihr domänenseparierter SHA-256.
- RFiDGear-Enginezustand und Builder-Consumer-Abnahme sind getrennt. Nur
  `state=successful` plus `acceptanceState=successful` setzt `accepted=true`.
  Die Application-Abnahmeevidenz konsumiert nun dieses korrelierte Hostresultat
  statt das RFiDGear-Log ein zweites Mal auszuwerten.

## 11.0-preview40 – 2026-08-20

- Der geschlossene Upstream-Punkt #201 ist als getrennte, strikt lesende
  Application-Abnahme integriert. Pro Application erzeugt der Builder exakt
  `ReadAppSettings` mit `IsAppSettingsExactMatch=true` und anschließend
  `CheckAppKeyCount`; die sechs kompatiblen v10.39-rfPrj bleiben unverändert.
- Der Start ist nur bei binär nachgewiesenem Exact-AppSettings- und
  AppKeyCount-Vertrag verfügbar. Reader, Authentifizierung, Kartenpolling und
  Taskausführung verbleiben vollständig in RFiDGear.
- Erfolg verlangt vollständige autoritative `Task.Outcome`-Ereignisse und ein
  terminales `TaskLoop.Success` mit durchgängig identischer, nichtleerer `RunId`. Die keyfreie Evidenz ist an den SHA-256 des
  Abnahme-rfPrj gebunden; veraltete, manipulierte oder unvollständige Evidenz
  wird fail-closed verworfen.
- Der Prüfbericht weist KeySettings und Keyanzahl nun je Application als OK,
  `IsNotTrue`-Abweichung oder fehlenden Istwert aus. #178 bleibt für
  Keywechsel-Kodierung gesperrt; #202 bleibt ohne zweite Readerlogik upstream.

## 11.0-preview39 – 2026-08-20

- Der Builder erzeugt jetzt einen formalen, versionierten Hostvertrag
  `de.rfidgear.host.v1`. Er hält die Verantwortungsgrenze fest: Profile,
  Projekt, Vault, Payload und Workflow verbleiben im Builder; Reader,
  Kartenpolling, Taskengine und operative Resultate verbleiben in RFiDGear.
- Jeder RFiDGear-Start erhält eine unveränderliche Request-ID, ausschließlich
  projektlokale Pfade, einen schlüsselfreien Taskplan-Hash und einen expliziten
  Capability-Handshake. Request, Startzustand und normalisiertes Endresultat
  werden korreliert unter `integration/host-v1/runs` abgelegt.
- Request und Result enthalten weder Schlüssel noch Karten-/Payloaddaten.
  Inkonsistente Korrelation, Pfadtraversal, unbekannte Fähigkeiten und fehlende
  Pflichtfähigkeiten werden fail-closed abgewiesen. Keywechsel verlangen weiter
  den noch fehlenden Nachweis `VerifiedApplicationKeyChange` aus #178.
- Projekt-Preflight, Diagnosepaket, WPF-Startpfad und Regressionstests kennen
  den neuen additiven Vertrag. Die sechs v10.39-Verträge und alle bestehenden
  Projekt-/rfPrj-Formate bleiben unverändert kompatibel.

## 11.0-preview38 – 2026-08-20

- Ein vollständiger ELATEC-Hardwarelauf mit RFiDGear 2.0.10 hat #178 erneut
  reproduziert: Vier `ApplicationKeyChangeover`-Tasks meldeten Erfolg, der
  Produktiv-Key wurde anschließend jedoch abgelehnt und derselbe Zugriff mit dem
  alten Transport-Key war erfolgreich. Der Keywechsel war auf der Karte also
  trotz positivem Taskstatus nicht erfolgt.
- Die Kodierstation sperrt deshalb Projekte mit Application-/PICC-Keywechsel,
  solange RFiDGear keine binär nachweisbare Nachverifikation des neu gesetzten
  Keys bereitstellt. Die Prüfung erfolgt sowohl beim Freischalten der Buttons als
  auch unmittelbar vor dem Prozessstart. Reine Leseprüfungen, Mastering und
  Transport-Recovery bleiben verfügbar.
- Der Hardware-Abnahmeplan führt die verifizierte Keywechsel-Fähigkeit nun als
  Pflichtnachweis und #178 als explizite Upstream-Evidenzlücke. Der schlüsselfreie
  Reproduktionsbericht wurde im Original-Issue ergänzt.

## 11.0-preview37 – 2026-08-20

- Der aktuelle Upstream-Stand wurde bis RFiDGear 2.0.10.0 geprüft. Diese
  Version ist nun empfohlen; Freigaben bleiben weiterhin vom positiven
  Binärmarker-Nachweis und nicht allein von der Versionsnummer abhängig.
- Die Fähigkeitsmatrix unterscheidet jetzt den ab 2.0.8 vorhandenen exakten
  `ReadAppSettings`-Vergleich, `CheckAppKeyCount` und den weiterhin fehlenden
  read-only FileSettings-Abnahmetask. Issue #201 ist upstream behoben; die
  Builder-Abnahmeshells hierfür bleiben bis zu einem verifizierten XML-Vertrag
  bewusst noch aus. Issue #202 wurde mit aktuellem Quellnachweis erneut gemeldet.
- Die Integrationsgrenze wurde in RFiDGear #190/#191 konkretisiert: Der Builder
  besitzt Profile, Vault, Payloads und Bedienworkflow; RFiDGear besitzt Reader,
  Kartenpolling, Taskengine und verbindliche Ergebnisereignisse. Ein
  versionierter Ausführungsvertrag hat Vorrang vor einer zweiten eingebetteten UI.
- GUI-Build und CLI-Paritätsaufbau verwenden dieselbe atomische Erzeugung der
  Mastering-Nullinitialisierungsdateien. Der vollständige Testlauf prüft damit
  auch den CLI-Pfad fail-closed.

## 11.0-preview36 – 2026-08-13

- Der Projektbuild erzeugt nun die in Mastering-`WriteData`-Tasks referenzierten
  Nullinitialisierungsdateien projektlokal, atomisch und in der exakt erwarteten
  Dateigröße. Zuvor fehlten diese Dateien vollständig; RFiDGear meldete deshalb
  beim realen Mastering `Unknown`, übersprang abhängige Leseaufgaben und ließ
  die Karte nur teilweise aufgebaut zurück.
- Mastering-Preflight und alle Kartenstartpfade prüfen Eingabedateien jetzt
  fail-closed auf Existenz, gültiges Hexformat und die im rfPrj deklarierte
  Byteanzahl. Kodier-Payloads dürfen beim reinen Projektbuild weiterhin fehlen,
  müssen jedoch unmittelbar vor einem Kartenstart vollständig bereitstehen.
- Ein realer ELATEC/USB-Test nach erneuter Formatierung bestätigte das korrigierte
  Mastering: 27/27 Taskereignisse, nur drei erwartete negative
  Application-Vorprüfungen, keine unerwarteten Fehler, keine übersprungenen
  Tasks und keine AuthFailure-Meldung. Alle sechs frischen Rücklesedateien waren
  mit 14, 32, 640, 256, 96 und 14 Byte exakt groß und vollständig nullgefüllt.
- Der nachgelagerte Karten-Preflight und die Endkodierung wurden wegen des
  lokalen Codex-Ausführungslimits noch nicht gestartet; sie sind daher nicht
  Bestandteil dieses Hardware-Nachweises.

## 11.0-preview35 – 2026-08-13

- Ein realer ELATEC/USB-Read-only-Test mit dem aufgelegten DESFire-Testchip
  endete mit `TaskLoop.Success`: sechs von sechs frischen Readbacks waren mit
  14, 32, 640, 256, 96 und 14 Byte vollständig und bytegleich; keine
  AuthFailure-Meldung trat auf.
- Der Test deckte absolute Ausgabeziele in einem kopierten Diagnose-rfPrj auf.
  Taskübersichten zeigen nun Ein- und Ausgabepfade; Preflight und alle
  Builder-Betriebsaktionen sperren ungültige oder außerhalb des Projektordners
  liegende Pfade vor einem RFiDGear-/Kartenstart.
- Die Kodierstation erhält **RFiDGear-Prozess abschließen**. Zuerst wird ein
  normales Fensterschließen versucht. Ein nach terminalem Status fensterlos
  verbliebener Prozess darf erst nach ausdrücklicher Bestätigung beendet werden;
  PID und EXE-Pfad werden gegen den ursprünglichen Startnachweis geprüft.
- Regressionen belegen projektlokale/externe Pfade, normales Schließen,
  Bestätigungspflicht für fensterlose Prozesse und Schutz vor wiederverwendeten
  fremden Prozess-IDs.

## 11.0-preview34 – 2026-08-13

- Eine zentrale, testbare Laufzeitsicherung verfolgt die vom Builder gestarteten
  RFiDGear-Prozesse. Ein noch nicht terminaler AUTORUN-Lauf oder ein weiterhin
  laufender manueller, Kodier- oder Serienprozess blockiert Schließen,
  Projektwechsel und Doppelstart mit einer eindeutigen Bedienmeldung.
- Der Quick-Start und beide Kodierstarts speichern nun die konkrete Prozess-ID.
  Eine wiederverwendete Windows-Prozess-ID wird zusätzlich gegen den erwarteten
  RFiDGear-Programmpfad geprüft.
- Wird das Schließen wegen ungespeicherter Änderungen oder einer fehlgeschlagenen
  Payload-Sperre abgebrochen, bleibt der Überwachungstimer aktiv. Erst ein
  tatsächlich zulässiges Schließen stoppt die Logüberwachung.
- Regressionen belegen Sperren vor Terminalstatus, bei weiterlaufendem Prozess
  und im AUTOMODE sowie die Freigabe nach Prozessende und Finalisierung.

## 11.0-preview33 – 2026-08-13

- Ein persistenter, keyfreier Payload-Sicherheitsstatus bindet bereitgestellte
  Dateien an genau den aktuellen Sitzungsdatensatz. Vor dem RFiDGear-Start
  werden Status, Zeitpunkt, Ausgabeliste und Dateiinhalt fail-closed geprüft.
- Der Ein-Schritt-Start entfernt alte, im Kodierschema definierte Payloads,
  bevor RFiDGear ohne Automatik geöffnet wird. Ungültige neue Eingaben lassen
  ebenfalls keine Nutzdaten einer vorherigen Karte freigegeben zurück.
- Nach erfolgreichem oder fehlgeschlagenem Datensatz und beim sicheren Schließen
  des Builders werden die Sitzungs-Payloads entfernt und auf `NotPrepared`
  gesetzt. Vault, Projekt, rfPrj, Readbacks und Berichte bleiben unverändert.
- Regressionen belegen Alt-Payload-Entfernung, den Ausschluss von Kartendaten im
  Sperrstatus, Manipulationsschutz sowie das Sperren nach Sitzungsabschluss.

## 11.0-preview32 – 2026-08-13

- Der v10.39-Ein-Schritt-Workflow **Kodiersession starten** ist nun direkt in
  der C#-Projektbibliothek verfügbar: Kodierstation laden, vollständigen
  Preflight prüfen, konkretes RFiDGear-Kodierprojekt öffnen und primäres
  Kartenfeld fokussieren.
- Der Start verwendet ausschließlich `CUSTOMPROJECTFILE`; `AUTORUN`, `AUTOMODE`
  und Payloadbereitstellung bleiben garantiert aus. Die Oberfläche kennzeichnet
  ausdrücklich, dass noch kein Kartenlauf gestartet wurde.
- Fehlerhafte Preflights und reine Bauartefakte blockieren den Quick-Start vor
  dem Prozessstart. Der separate bewusste Pfad **Nur Kodierstation laden** bleibt
  für Altprojekte erhalten.
- Ein Fake-Prozess-Test belegt Argumentvertrag, sicheren Zwischenzustand und
  vollständigen Startausschluss in beiden Blockerfällen.

## 11.0-preview31 – 2026-08-13

- Ein projektbezogener Hardware-Abnahmeplaner prüft Offline-Preflight,
  Mastering-/Kartenprobe-Artefakte, Kodierprojekt, RFiDGear-Binärfähigkeiten und
  Reader-Konfiguration, ohne RFiDGear oder Hardware zu starten.
- Der keyfreie TXT-/JSON-Plan führt zehn verbindliche Bedienabschnitte mit
  Risiko, erwartetem Nachweis und frischer Testchipfreigabe auf. Recovery ist
  klar als optional und destruktiv getrennt.
- Die offenen RFiDGear-Evidenzgrenzen #201 und #202 werden im Plan fail-closed
  ausgewiesen. Beide Issues wurden am 13.08.2026 erneut geprüft und sind offen.
- Kodierstation und Hilfe bieten direkten Zugriff; ein eigener dokumentierter
  Hardware-Abnahmevertrag verhindert, dass Planerzeugung als Testfreigabe
  missverstanden wird.
- Regressionen prüfen vollständige Bereitschaft, Blocker, alle Freigabeflags,
  Recovery-Trennung, Upstream-Grenzen, atomaren Export und Keyausschluss.

## 11.0-preview30 – 2026-08-13

- Bearbeitbare Projektdefinitionen lassen sich wieder wie in v10.39 direkt aus
  der Projektbibliothek löschen.
- Die Bestätigung zeigt den exakten Löschpfad und weist Vault sowie Buildordner
  ausdrücklich als erhalten aus. Gebaute Altprojekte sind für diese Aktion
  gesperrt.
- Eine Bibliothekspfadgrenze verhindert Löschungen außerhalb des eingestellten
  Projekte-Stammordners. SHA-256-Revisionsprüfung und Sidecar-Lock verhindern,
  dass eine nach der Vorschau fremdveränderte Datei gelöscht wird.
- Regressionen belegen erfolgreiche Löschung nur der Quelldefinition,
  Vault-/Artefakterhalt, Konfliktabbruch, Pfadgrenze und Artifact-only-Sperre.

## 11.0-preview29 – 2026-08-13

- Eine neue integrierte Seite **Hilfe & Werkzeuge** bietet direkte Wege zu den
  zentralen Arbeitsschritten, öffnet die lokale Betriebsdokumentation und die
  relevanten Projekt-, Einstellungs- und RFiDGear-Logordner.
- Der neue Umgebungstest prüft Release-Dateien, Startdateivertrag, Profile,
  Task-Shells, externen Einstellungsort, Projektbibliothek, RFiDGear-Binärmarker,
  Reader-Konfiguration und Logpfade vollständig lesend.
- TXT- und JSON-Berichte werden atomar im versionsunabhängigen Benutzerprofil
  gespeichert. Einstellungsinhalte werden nur auf gültiges JSON geprüft und
  niemals ausgegeben; Vaults und Schlüsselwerte werden nicht übernommen.
  RFiDGear, Reader und Karte werden nicht gestartet.
- Ein Regressionstest belegt Fehlerklassifikation, atomaren Berichtsexport und
  den Ausschluss selbst in einer Einstellungsdatei vorhandener Schlüsselwerte.

## 11.0-preview28 – 2026-08-13

- Die C#-Profilverwaltung kann Kundenprofile wieder wie v10.39 duplizieren.
  Duplikate sind tiefe Kopien mit neuer eindeutiger ID, sichtbarer Kopiekennung,
  Version `1.0` und ohne Zeitstempel oder Vorgängerverweis des Originals.
- Basis- und Kundenprofile lassen sich über die Oberfläche als keywertfreie
  Kodiervorschrift exportieren. Der atomare UTF-8-Export enthält Metadaten,
  Variablenregeln, Applications, KeySettings, Files, temporäre und finale
  FileSettings, Initialisierung sowie Kodierrezepte, aber keine Schlüsselwerte.
- Der CLI-Befehl `export-coding-specification` stellt denselben Export für
  automatisierte Prüf- und Freigabepfade bereit.
- Der vollständige DEVK-Export wird bytegenau gegen
  `Export-Kodiervorschrift.ps1` aus v10.39 geprüft. Zusätzliche Regressionen
  prüfen BOM-Freiheit, atomaren Export, tiefe Kopie, eindeutige Identität und
  ausgeschlossene Historienmetadaten.
- `PARITY.md` dokumentiert erstmals die nachgewiesene Funktionsgleichheit,
  bewusste Upstream-Grenzen und die noch offenen C#-Bedienpunkte.

## 11.0-preview27 – 2026-08-13

- Prüfberichte unterscheiden jetzt für jeden Prüfpunkt sichtbar zwischen
  `Bytegenau`, `Operativ` und `Kein Istwert`. Die zusätzliche Abdeckungsbilanz
  erscheint in WPF, TXT und JSON, ohne den kompatiblen v10.39-Statusvertrag zu
  verändern.
- Ein erfolgreicher finaler File-Read bleibt ein operativer Nachweis für
  Application-Erreichbarkeit und den verwendeten Produktiv-Keyslot/Keytyp. Er
  wird nicht mehr missverständlich mit einem bitgenauen Settings-Read
  gleichgesetzt.
- Der aktuelle RFiDGear-`ReadAppSettings`-Task wurde gegen den Originalcode
  geprüft: Bereits ein gemeinsames KeySettings-Bit führt derzeit zu Erfolg;
  Keytyp und Keyanzahl werden nicht verglichen. Der Builder verwendet diesen
  Task deshalb nicht als Abnahmenachweis und nennt die Einschränkung direkt im
  Bericht.
- Der bestätigte Upstream-Fehler ist als
  `c3rebro/RFiDGear#201` mit reproduzierbaren Soll-/Istfällen gemeldet.
- Die davon getrennte fehlende, strikt lesende FileSettings-Prüfschnittstelle
  ist als `c3rebro/RFiDGear#202` beschrieben. Gefordert werden exakter Filetyp,
  Größe, Communication Mode, alle vier Access Rights und ein keywertfreies
  strukturiertes Ergebnis.
- Regressionen prüfen Nachweisarten, Abdeckungszahlen, den sichtbaren Ausschluss
  des unsicheren Settings-Tasks sowie die weiterhin keywertfreien Berichte.

## 11.0-preview26 – 2026-08-13

- Ein Projektordner kann nicht mehr gleichzeitig gebaut und von einer zweiten
  Builder-Instanz als aktive Kodiersitzung verwendet werden. Beide Vorgänge
  teilen eine exklusive, rechnerübergreifende Arbeitsbereichs-Lease.
- Der zentrale Projektbuild hält die Lease vom ersten Artefaktzugriff bis zum
  erfolgreichen Abschluss-Preflight und gibt sie anschließend deterministisch
  frei.
- Eine Kodiersitzung hält dieselbe Lease von der validierten Payload-Erzeugung
  bis zur finalen Readback-Entscheidung oder der ausdrücklichen Markierung als
  fehlgeschlagen. Ein noch offener Datensatz kann nicht still durch einen neuen
  Datensatz oder ein anderes Projekt ersetzt werden.
- Direkt vor dem RFiDGear-Start und vor jeder Laufbewertung werden alle
  Payloaddateien erneut bytegenau gegen die im Arbeitsspeicher vorbereiteten
  Bytes geprüft. Externe Änderung oder Löschung sperrt den Start beziehungsweise
  Abschluss.
- Die persistente `.rfidgear-builder.workspace.lock` ist leer. Nach normalem
  Abschluss und auch nach Prozessabbruch wird nur der Betriebssystem-Handle
  freigegeben; die identische Datei kann sofort wieder als Sperrknoten dienen.
- Regressionen prüfen parallele Lease-Abweisung, Freigabe/Wiederanlauf,
  Build-gegen-Kodierung und Payloadmanipulation.

## 11.0-preview25 – 2026-08-13

- Projektdefinitionen, portable Key-Vaults und Kundenprofile erhalten beim
  Öffnen einen SHA-256-Ausgangsstand. Vor dem Speichern wird derselbe Stand
  unter einer rechnerübergreifenden dateibasierten Schreibsperre erneut geprüft.
- Wurde eine Datei zwischenzeitlich von einem anderen Benutzer, Rechner oder
  Programm geändert, bricht der Builder mit einer verständlichen
  Konfliktmeldung ab. Die fremde Fassung wird niemals still überschrieben;
  Projektentwürfe können bewusst unter einem anderen Namen gespeichert werden.
- Bei Kundenprofilen umfasst die Sperre den gesamten Vorgang aus
  Vorgängerprüfung, eindeutiger Versionskontrolle, unveränderlicher Archivierung
  und Aktivierung. Ein abgewiesener Konflikt verändert daher auch die
  Profilhistorie nicht.
- Extern ersetzte Vaults werden sowohl vor als auch nach dem Entschlüsseln
  erkannt. Beim verschlüsselten Speichern gilt derselbe Ausgangsstand wie beim
  Öffnen des Editors.
- Die Schutzlogik verändert keine Projekt-, Profil- oder Vaultformate.
  Persistente leere `*.rfidgear-builder.lock`-Dateien koordinieren auch
  parallele Builder auf Netzlaufwerken; sie enthalten keine Fach- oder
  Schlüsseldaten.
- Die Startseite beschreibt Mastering und Recovery jetzt entsprechend ihrer
  tatsächlichen Preflight-, Capability- und Bestätigungsregeln.

## 11.0-preview24 – 2026-08-13

- Die RFiDGear-Integration wird nicht mehr allein anhand einer Mindestversion
  freigeschaltet. Ein schreibgeschützter Binärscan prüft `RFiDGear.exe` und die
  danebenliegende `RFiDGear.dll` auf die tatsächlich benötigten Vertragsmarker.
- Die Fähigkeitsmatrix weist Projektstart, AUTORUN, AUTOMODE,
  ChangeFileSettings, korrelierte Task-Ergebnislogs, bestehende UI-Erweiterungen
  sowie die noch fehlenden anwendungsweiten Erweiterungsschnittstellen getrennt
  als verfügbar, nicht vorhanden oder unbekannt aus.
- Launcher und Kodierstation sperren jede Aktion fail-closed, wenn ihr positiver
  Nachweis fehlt. Eine hohe oder fehlerhaft gepflegte Versionsnummer kann damit
  keine Funktion mehr versehentlich aktivieren.
- Der installierte Upstream-Build 2.0.7.0 wurde automatisch geprüft. Projektstart,
  AUTORUN, AUTOMODE, ChangeFileSettings, `Task.Outcome` + `TaskLoop.Summary` +
  `RunId` und die bisherigen kartenspezifischen UI-Erweiterungen sind vorhanden.
  Die offenen anwendungsweiten Hosts aus GitHub #190/#191 sind erwartungsgemäß
  noch nicht enthalten.
- Regressionstests belegen ausdrücklich beide Richtungen: hohe Version ohne
  Marker bleibt gesperrt; vorhandene Marker werden unabhängig von einer
  Versionsschwelle erkannt.

## 11.0-preview23 – 2026-08-13

- Bestehende `.rfPrj`-Dateien können auf der Profilseite schreibgeschützt als
  Kundenprofilvorschlag analysiert werden. Der Import startet weder RFiDGear
  noch einen Reader und verändert die Quelldatei nicht.
- Der evidenzbasierte Import rekonstruiert AIDs, Applications, Keytyp/-anzahl,
  KeySettings, Change-Key-Modus sowie Files mit Typ, Größe, Communication Mode,
  Access Rights, initialen und finalen FileSettings und belegter
  Nullinitialisierung.
- Widersprüchliche Taskfelder, ungültige Abhängigkeiten oder unbestimmte
  Fileparameter sperren den Profilvorschlag. Nicht rekonstruierbare
  Variablen-/Kodierrezepte, fehlende Erstellungs- oder KeySettings-Tasks und
  nicht unterstützte Tasktypen werden ausdrücklich als Warnungen ausgewiesen.
- Der Import liest ausschließlich eine Whitelist struktureller XML-Felder.
  Schlüssel- und Payloadwerte werden nicht in das Importmodell übernommen;
  lange Hexfolgen in Beschreibungen und Quellpfaden werden zusätzlich redigiert.
- Applications werden strukturell gegen die installierte Profilbibliothek
  abgeglichen. Bereits bekannte Profilanteile sind standardmäßig abgewählt,
  neue AIDs zur Übernahme vorgeschlagen.
- Der WPF-Assistent zeigt Applications, Evidenz, vorhandene Profilzuordnung und
  alle Befunde. Erst nach ausdrücklicher Bestätigung öffnet er einen noch
  ungespeicherten Entwurf im vollständigen Profil-Editor; nur dessen normale
  Validierung und Speichern aktiviert das Profil.
- Atomare TXT-/JSON-Importberichte sind explizit keyfrei. Synthetische
  Konflikt-/Redaktionsregressionen und ein realer v10.39-Masteringplan mit vier
  Applications und elf Files prüfen den Import.

## 11.0-preview22 – 2026-08-13

- Die Profilseite besitzt jetzt einen integrierten Historienbrowser. Aktueller
  Stand und alle archivierten Vorgänger werden mit Version, Zeitstempel,
  SHA-256-Kurzfingerabdruck, Quelle und Integritätsstatus angezeigt.
- Ein ausgewählter Vorgänger wird mit dem vorhandenen semantischen
  Profilvergleich vollständig gegen den aktuellen Stand gestellt. Auswirkungen
  auf Struktur, Security, Mastering und Kodierdaten bleiben dadurch vor einer
  Wiederherstellung sichtbar und schlüsselwertfrei.
- Wiederherstellung überschreibt weder Archiv noch aktuellen Stand still. Der
  gewählte Inhalt darf nur unter einer neuen, noch nie verwendeten Version
  aktiviert werden; zuvor wird der aktuelle Stand ebenfalls atomar archiviert.
- Historienpfade werden strikt auf den Profilordner begrenzt. Archiv-ID, Typ,
  Profilvalidierung und der im Dateinamen verankerte SHA-256-Fingerabdruck werden
  vor Vergleich und Wiederherstellung erneut geprüft. Manipulierte Archive
  erscheinen als Fehler und bleiben gesperrt.
- Auch normales Speichern im Profil-Editor kann keine bereits archivierte
  Versionsnummer wiederverwenden. Nach externer Profiländerung werden ein offener
  Projekteditor und seine Bindungsanzeige sofort neu synchronisiert.
- Regressionen prüfen Pfadmanipulation, wiederverwendete Versionen, fachlichen
  Vergleich, Restore-as-new-version, erneute Archivierung, Projektbindungsbruch
  und manipulierte Archivdateien.

## 11.0-preview21 – 2026-08-13

- Neue Projekte binden die ausgewählten Profile mit Version und einem
  deterministischen SHA-256-Inhaltsfingerabdruck. Der Fingerabdruck erfasst
  fachliche Profilinhalte, ignoriert aber reine Historienzeitstempel.
- Bestehende Projekte ohne Bindung bleiben abwärtskompatibel les- und
  speicherbar. Der Build bleibt so lange sichtbar gesperrt, bis die aktuell
  installierten Profilstände im Editor bewusst übernommen wurden.
- Profilwechsel, Versionsabweichungen und sogar inhaltliche Änderungen unter
  derselben Versionsnummer werden erkannt. Der zentrale Buildservice verweigert
  solche nicht reproduzierbaren Builds auch außerhalb der WPF-Oberfläche.
- Kundenprofile führen eine dauerhafte Historie außerhalb des aktiven
  Profilordners. Fachliche Änderungen unter derselben Versionsnummer werden
  abgelehnt; bei einem Versionswechsel wird der exakte Vorgänger atomar
  archiviert und mit Erstellungs-, Änderungs- und Vorgängermetadaten verknüpft.
- Der Projekteditor zeigt Profil-ID, Version und Kurzfingerabdruck sowie den
  konkreten Bindungsbruch. Der Profil-Editor zeigt die Zahl archivierter
  Vorgänger und erklärt die erforderliche Versionserhöhung.
- 26 paketfreie Kern-/Runtime-/rfPrj-Regressionen prüfen Bindung, bewusste
  Migration, Same-Version-Manipulation, Historienarchivierung und die bisherigen
  Verträge. Es wurde keine Karte und kein Reader angesprochen.

## 11.0-preview20 – 2026-08-13

- Der Datei-Bereich des Profil-Editors trennt den initialen Mastering-Zustand
  und den finalen Kartenzustand jetzt sichtbar und verständlich.
- Eine Live-Vorschau zeigt Communication Mode und Access Rights technisch als
  `R/W/RW/C`, erklärt alle Keyrollen in Klartext, nennt jede konkrete Änderung
  und bildet die resultierende CreateFile-/Zero-Fill-/Readback-/
  ChangeFileSettings-Reihenfolge ab.
- Finale Settings können als sicherer Ausgangspunkt in die initialen Felder
  übernommen werden. Nicht aktive Creation Settings sind deaktiviert;
  identische Initial-/Endwerte werden als redundanter zusätzlicher
  ChangeFileSettings-Task gewarnt.
- Ein bestehender UI-Semantikfehler ist behoben: Eine fehlende
  `masteringInitialization` bedeutet laut Profilvertrag Standard-Zero-Fill und
  wird nun angehakt dargestellt. Bewusstes Abwählen speichert explizit `none`,
  statt irreführend wieder den Zero-Fill-Standard zu erzeugen.
- Der neue testbare Erklärungsdienst und Regressionen prüfen zweistufige,
  direkte, redundante sowie kanonische Zero-Fill-/`none`-Zustände.

## 11.0-preview19 – 2026-08-13

- Ein neuer semantischer Projekt-/Profilvergleich stellt zwei vollständige
  Projektkompositionen, einen ungespeicherten Editorentwurf gegen ein Projekt
  oder zwei einzelne Profile gegenüber.
- Unterschiede werden fachlich nach Profilen, Variablen, Applications/AIDs,
  Files und einzelnen Eigenschaften aufgelöst. Größen, Typen, Communication
  Mode, Access Rights, initiale FileSettings, Mastering-Initialisierung,
  Keytypen/-anzahl/-settings, Change-Key-Modus und Kodierrezepte sind sichtbar.
- Hinzugefügte und entfernte Applications listen ihre enthaltenen Files
  zusätzlich explizit auf. Jede Zeile nennt die praktische Wirkung als
  Kompositions-, Kartenstruktur-, Security-, Mastering- oder Kodierdatenänderung.
- Der Vergleich liest weder Vaults noch Schlüsselwerte und exportiert atomare,
  explizit keyfreie `Profilvergleich.txt`- und `.json`-Berichte in den
  versionsunabhängigen Benutzerbereich.
- Synthetische Änderungsregressionen und der reale Grundprofil-plus-DEVK-Diff
  prüfen identische Vergleiche sowie additive Profile, Variablen, AID und Files.

## 11.0-preview18 – 2026-08-13

- Die bisherige v10.39-Projektanalyse ist als testbarer C#-In-Memory-Dry-Run
  verfügbar. Kartenaufbau, Mastering- und Kodier-Taskpläne werden sichtbar,
  ohne RFiDGear zu starten oder Hardware anzusprechen.
- Produktivkeys und ein entsperrter Vault sind für die Analyse nicht nötig.
  Credential-Rollen und Keywechsel werden mit synthetischen, flüchtigen Tokens
  simuliert; es entstehen ausschließlich keyfreie TXT-/JSON-Berichte und keine
  temporären `.rfPrj`-Dateien.
- Der Dry-Run prüft Taskreihenfolge und -abhängigkeiten, vollständige File-
  Verträge, Transport-Credentials, Key0-zuletzt, Application-/PICC-KeySettings
  und den simulierten produktiven Endzustand.
- Die neue Seite ist aus Navigation, Projektliste und Projekteditor erreichbar.
  Gebaute Altprojekte laufen vor der Analyse durch den vorhandenen streng
  geprüften, unverändernden Mastering-Migrationspfad.
- Der reale Grundprofil-plus-DEVK-Test erwartet 45 Mastering- und 33 Kodier-
  Tasks und beweist, dass weder produktive Testkeys noch `.rfPrj`-Dateien in
  den Analyseexport gelangen.

## 11.0-preview17 – 2026-08-13

- Der sichere v10.39-Altprojektpfad ist in den C#-Kern und die Projektseite
  migriert. Gebaute Projekte lassen sich per Bearbeiten oder Doppelklick als
  neuer, noch ungespeicherter Mastering-Entwurf öffnen; beliebige ältere
  JSON-Konfigurationen besitzen einen eigenen Importknopf.
- Die Quelldatei wird niemals verändert. Produktivkeys werden grundsätzlich
  nicht aus Artefakten oder Altformaten übernommen und der Buildumfang bleibt
  bis zur bewussten Prüfung auf Mastering begrenzt.
- Bei Bauartefakten werden Masterprofil, Kundenprofile, Profilcode/-version und
  das vollständige Application-/File-/Recipe-Layout mit der aktuell
  installierten Profilbibliothek verglichen. Jeder nicht belegbare Profil- oder
  Layoutwechsel blockiert die Migration.
- Keyfreie Projektwerte werden – soweit vorhanden und aktuell gültig – aus
  `coding.schema.json` übernommen. Die Legacy-Erkennung ordnet alte skalare
  Felder allgemein den aktuellen Projektvariablen zu, ohne Kundennamen oder
  Firmencode-Sonderfälle fest zu verdrahten.
- Synthetische Positiv-/Negativtests und ein realer Grundprofil-plus-DEVK-
  Roundtrip prüfen Quellschutz, Layoutabweichung, Masterprofilwechsel,
  Key-Ausschluss und verlustarme Fachwertübernahme.

## 11.0-preview16 – 2026-08-13

- Der grafische Kundenprofil-Editor ist vollständig an den C#-Kern angebunden:
  Metadaten, Variablen, Applications, KeySettings, Files, Access Rights,
  temporäre Creation Settings, Mastering-Initialisierung und Kodierrezepte.
- Variablenumbenennungen aktualisieren Rezeptreferenzen automatisch; das Löschen
  referenzierter Variablen und das Reduzieren noch verwendeter Key-Slots wird
  blockiert. AIDs, FileIDs, Größen, Rechte und Rezeptlängen werden vor dem
  Speichern validiert.
- Kundenprofile liegen dauerhaft unter
  `%APPDATA%\RFiDGear-Projektbuilder\profiles\customer` und überleben damit
  Builder-Updates. Gebündelte Kundenprofile können als Benutzer-Override
  bearbeitet werden; das Masterprofil bleibt unveränderlich.
- Projekteditor, Build, Projekt-Preflight und Profilseite verwenden dieselbe
  zusammengeführte Profilbibliothek. Es gibt weiterhin keine fest verdrahteten
  Kundennamen und keine produktiven Schlüsselwerte in Profilen.
- Die Tests prüfen atomisches Speichern, Overrides, Referenzschutz,
  KeySettings-Bitmasken und weiterhin die vollständige v10.39-Parität.

## 11.0-preview15 – 2026-08-13

- Der v10.39-Diagnosepaketvertrag ist in den C#-Runtime-Kern migriert.
- Fehlgeschlagene Kodierläufe erzeugen automatisch ein Diagnose-ZIP; manueller
  Export ist über Schaltfläche und `Strg+Umschalt+D` möglich.
- Eine feste Positivliste nimmt Manifest, Prüfpläne, Kodierschema, Session,
  aktuellen Datensatz, keyfreie Task-Map, Readbacks und bereinigte Logs auf.
- `.project.json`, `.rfPrj`, Vault-Dateien und unbekannte Task-Map-Felder werden
  ausdrücklich abgelehnt. Textuelle Session-/Preflight-Daten durchlaufen vor
  dem Export zusätzlich den Hex-/Key-Sanitizer.
- Automatische Tests prüfen ZIP-Inhalt, Negativliste, Task-Map und Redaction.

## 11.0-preview14 – 2026-08-13

- Die C#-Kodierstation besitzt jetzt eine eigene Seriensteuerung für Einzel,
  fortlaufende Nummern und TXT-/CSV-Listen mit frei wählbarem Nummernfeld.
- Erfolgreiche Datensätze werden erst nach finaler Byteverifikation in das
  kompatible JSONL-Sitzungslog übernommen und projektübergreifend als mögliche
  Duplikate erkannt.
- Fehler, Retry und bewusstes Überspringen sind getrennte Zustände. F8 prüft
  Readbacks, F9 markiert den aktuellen Lauf als Fehler; der Datensatz bleibt
  danach für Wiederholung oder Skip stehen.
- Beim Sitzungsabschluss entstehen keywertfreie TXT-/JSON-Zusammenfassungen im
  Projektordner `sessions`; bestehende v10.39-JSONL-Dateien werden berücksichtigt.
- Veraltete Dokumentationshinweise zu bereits vorhandenen Mastering-,
  Karten-Preflight- und Recovery-Aktionen wurden bereinigt.
- Der aktuelle Upstream-Stand wurde bestätigt: RFiDGear 2.0.7.0 enthält die
  gemergten Fixes #197/#194, #198/#195 und #200/#199. Es gibt daraus derzeit
  keinen neuen, belastbar abgrenzbaren Fehlerbericht.

## 11.0-preview13 – 2026-08-13

- Die vollständige v10.39-Karten-Preflight-Klassifikation ist im C#-Kern
  verfügbar, einschließlich Transport-/Produktivzugriff, Payloadvergleich,
  Mischzuständen, Laufzeitfehlern und konkreter Schreibpolicy.
- Nullbytes werden ausdrücklich nicht als leerer Transponder interpretiert:
  Readbacks werden anhand von Existenz, exakter Bytezahl und gegebenenfalls
  Bytegleichheit bewertet.
- AUTORUN-Betriebsaktionen werden in der Kodierstation automatisch im
  RFiDGear-Log überwacht; parallele Starts bleiben bis zum Terminalstatus
  gesperrt.
- Abgeschlossene Kartenproben erzeugen kompatible JSON-/TXT-Berichte und einen
  bereinigten Logauszug, der direkt aus der Oberfläche geöffnet werden kann.
- Die Produktivprobe wird erst nach Vorbereitung der aktuellen Sitzungswerte
  freigeschaltet; dabei entsteht der keyfreie kompatible `expected-card.json`-
  Sollnachweis.
- Ein zusätzlicher Cross-Runtime-Test vergleicht den vollständigen Bericht
  semantisch mit `Card-Preflight.ps1` aus v10.39.

## 11.0-preview12 – 2026-08-13

- Sichere Betriebsaktionen für Mastering, strikt lesenden Karten-Preflight und
  Recovery sind direkt in der Kodierstation verfügbar.
- Alle angebotenen `.rfPrj`-Dateien werden vor dem Start auf Taskfolge,
  Abhängigkeiten und Pfadgrenzen geprüft; Read-only- und Recovery-Verträge
  werden explizit erzwungen.
- Automatische Schreibläufe benötigen eine bearbeitbare Projektdefinition und
  einen erfolgreichen vollständigen Projekt-Preflight. Recovery verlangt zwei
  Warnbestätigungen.
- Der Laufstatus folgt den von RFiDGear 2.0.7 bereitgestellten terminalen
  `TaskLoop.Success`-/`TaskLoop.Failure`-Ereignissen. Ein Prozessstart allein
  wird nicht als Erfolg behandelt.
- Der GitHub-Upstream-Stand mit #197, #198/#195 und #200/#199 wurde erneut
  abgeglichen; aktuell besteht kein belastbarer Grund für ein weiteres Issue.

## 11.0-preview11 – 2026-08-13

- echte C#-Prüf-/Abnahmeengine mit den Zuständen `OK`, `Abweichung` und
  `Nicht prüfbar` ergänzt
- finale Readbacks werden bytegenau gegen den aktuellen Payloadnachweis geprüft
- alte Verification-Dateien werden anhand des letzten Bereitstellungszeitpunkts
  ausdrücklich nicht als aktueller Nachweis gewertet
- Application-Erreichbarkeit wird nur aus einem erfolgreichen finalen File-Read
  abgeleitet; nicht separat gelesene Key-/File-/PICC-Metadaten bleiben offen
- keywertfreie TXT- und JSON-Berichte werden atomar unter `reports` geschrieben
- Abnahmeseite zeigt alle Prüfpunkte und öffnet Bericht bzw. Berichtsordner
- C#-Kodiersitzung erzeugt eine v10.39-kompatible `Letzte_Kodierung.json`
- CLI-Befehl `acceptance-report` für automatisierte Offline-Auswertung ergänzt
- Regressionstestumfang auf 18 Tests erweitert

## 11.0-preview10 – 2026-08-13

- strukturierter Editor für neue und bestehende Projektdefinitionen ergänzt
- bestehende Projekte werden am vorhandenen Pfad gespeichert; Kopien entstehen
  ausschließlich über die bewusste Aktion `Speichern unter`
- Projektkatalog unterdrückt das zugehörige gebaute Artefakt auch dann korrekt,
  wenn Definitions- und Ausgabeordner voneinander abweichen
- Profilkomposition synchronisiert Projektwerte, Transport-Keytypen und
  produktive Application-Keyslots dynamisch
- Produktivkeys können für die Sitzung sichtbar entsperrt oder kryptografisch
  neu erzeugt werden
- portabler Projekt-Key-Vault ist pfad- und formatkompatibel zu v10.39; die
  Projektdefinition bleibt auch beim Speichern und Bauen produktiv-keywertfrei
- zentraler C#-Buildservice erzeugt Haupt-, Preflight- und Recovery-Projekte
  sowie alle Vertragsartefakte und führt danach zwingend den Gesamt-Preflight aus
- Projekt schließen entfernt produktive Schlüssel aus der Editorsitzung
- Projektnamen mit Pfadzeichen werden vor Speichern und Build abgelehnt
- Regressionstestumfang auf 17 Tests erweitert

## 11.0-preview9 – 2026-08-13

- RFiDGear 2.0.7.0 ist die empfohlene Laufzeitversion.
- Die Kodierstation kann ein vorbereitetes Kodierprojekt ausdrücklich im neuen
  RFiDGear-Serienmodus mit `AUTOMODE=1` starten.
- `AUTORUN=1` und `AUTOMODE=1` sind gegenseitig ausgeschlossen; ein manueller
  Start setzt weiterhin keinen Automatikparameter.
- Der Serienmodus wird erst ab RFiDGear 2.0.7.0 freigegeben.
- Die in RFiDGear gespeicherte Reader-Konfiguration wird vor dem Serienstart
  schreibgeschützt geprüft und in den Einstellungen angezeigt. Der Builder
  verändert diese Konfiguration nicht.
- Die Vertragsdateien empfehlen 2.0.7.0. Die bedingte Mindestversion 2.0.6.0
  für `ChangeFileSettings` und die rfPrj-Manifestversion 2.0.6 bleiben aus
  Kompatibilitätsgründen unverändert.
- Neue Regressionstests decken Argumentbildung, Versionsgrenze,
  Parameterkonflikte und Reader-Konfigurationsauswertung ab.

### Prüfung

- vollständiger Build ohne Warnungen oder Fehler
- 15/15 C#-Regressionstests bestanden
- WPF-Selbsttest bestanden
- sechs Referenzprojekte einschließlich Payloads, rfPrj-Dateien und
  Vertragsartefakten semantisch gegen v10.39 geprüft
- keine Karte und kein Reader wurden durch die automatischen Builder-Tests
  angesprochen

Die getrennte Hardware-Abnahme der installierten RFiDGear-Version 2.0.7.0 mit
ELATEC-Leser und freigegebenem Testtransponder hat Formatierung, Mastering,
mehrfache Readbacks und `AUTOMODE=1` erfolgreich bestätigt.
## SieEncode 1.1.0-rc4 – sichere Erweiterung bestehender Karten

- Neuer Kartenvorgang „Bestehende Karte um Zusatzprofil erweitern“: Nur neue AIDs aus Zusatzprofilen werden gemastert, kodiert und geprüft.
- PICC-Masterkey und PICC-KeySettings bleiben dabei unverändert; Format-Recovery ist im Erweiterungsmodus nicht verfügbar.
- Die strikt lesende Transponder-Erkennung übernimmt PICC-KeySettings und die Create/Delete-Berechtigung in den strukturierten Laufnachweis. Bei unbekannter oder gesperrter Fähigkeit sowie bereits vorhandener Ziel-AID bleibt das Schreiben gesperrt.
- Projekteditor, Kodierstation und Tutorial erklären den geführten Erweiterungsablauf; Produktiv-PICC-Felder werden dafür ausgeblendet.
- Runtime 2.0.10.6 / rc11 ergänzt die rein lesenden, schlüsselfreien PICC-Fähigkeitsmetadaten.

## SieEncode 1.1.0-rc5 – hardwaregeprüfte Bestandskartenerweiterung

- Der vollständige sichtbare ELATEC/DESFire-Lauf erweitert eine Karte mit drei vorhandenen Anwendungen ausschließlich um AID `F482D5`, kodiert und prüft sie produktiv und erhält alle Altanwendungen.
- Der gezielte Rückbau enthält den Application MasterKey 0 der Ziel-AID und niemals einen Format-Task.
- `DeleteApplication` unterstützt PICC-Authentisierung, den expliziten freien Versuch und den gezielten Application-Masterkey-0-Pfad.
- Eine frische PICC-Inventur bewertet den tatsächlich erreichten Delete-Zielzustand korrekt, auch wenn der ELATEC-SDK-Aufruf nach erfolgreichem Löschen eine irreführende Ausnahme liefert.
- Runtime 1.0.0-rc15 / RFiDGear 2.0.10.10: 183/183 Softwaretests; Builder-Release-Gate: 45/45 Tests.

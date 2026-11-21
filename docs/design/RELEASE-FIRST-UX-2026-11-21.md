# Erste stabile SieEncode-Veröffentlichung: Oberfläche vor Runtime-Migration

Stand: 21.11.2026. Arbeitsgrundlage für den bestehenden Entwicklungszweig, **keine Releasefreigabe**.

## Verbindliche 1.0-Ausgangslage – erstmals veröffentlichtes SieEncode

**Korrektur vom Nutzer:** Es gibt **noch keinen veröffentlichten SieEncode-Projektstand**, keine produktiven SieEncode-Projektdateien und **keine Projekt-Vaults aus einer früheren Veröffentlichung**. Wir entwickeln das **erste Release 1.0**. Nummerierte Kandidaten und historische technische Adapter sind ausschließlich Entwicklungsartefakte, **keine** installierten Vorversionen bei Kunden.

Konsequenz: **Keine Altprojekt-Migration, keine alte Team-Empfängerliste und keine Wiederherstellung angeblich früher veröffentlichter Vaults in der normalen Bedienung oder Abnahme verlangen.** Der 1.0-Standardweg beginnt mit einem **neuen Projekt**: persönliche Windows-Identität → bei Teamnutzung von der Hauptverwaltung signierten Gruppenstand importieren → Projektprofil und Kartenschlüssel vorbereiten → Gruppen-Vault mit expliziter Gruppenauswahl und separat freigegebenem Recovery-Zugang neu anlegen → Vault wiederöffnen, prüfen und vor einer Kartenaktion autorisieren. Der persönliche Projekt-Vault ist eine zulässige Alternative bei echter Einzelnutzung.

Technische Konvertierungs-/Leseroutinen können intern bestehen bleiben, aber sie begründen keinen freigegebenen Produktumfang und keinen zusätzlichen Pflicht-Testfall. Auch für neu angelegte 1.0-Projekte bleiben künftig Datenerhalt, Revisions-/Sicherungskonflikte und bewusst bestätigte Empfängeränderungen relevante Sicherheitsanforderungen.

## Verbindlicher Entwicklungs- und Testrhythmus

**Zuerst zusammenhängende Änderungen umsetzen, dann gebündelt testen.** Nicht nach jedem UI-Text, Dialog-Fix oder einzelnen Commit einen Windows-Test, MSI-Build, Kandidaten oder Release verlangen.

- Mehrere UX-/Vault-/Projektverbesserungen in einer Entwicklungsrunde implementieren und im Entwicklungszweig sichern.
- Währenddessen Code sorgfältig prüfen und Sicherheitsinvarianten erhalten; notwendige Testfälle im Quellcode ergänzen, aber keine wiederholten manuellen Tests oder Releases anstoßen.
- Erst wenn ein zusammenhängender Meilenstein erreicht ist oder ein tatsächlich kritisches Risiko/Blocker vorliegt, einen **gemeinsamen** Build mit Regressionen, UI-Smoketests und gezielter manueller Restabnahme durchführen.
- Bereits bestandene, zum unveränderten Stand passende Nachweise wiederverwenden. Nur neue oder veränderte Abläufe erneut prüfen.
- MSI-/Testkandidaten gezielt für gebündelte Meilensteine erstellen; ein öffentliches Release erst nach gesamter Releaseabnahme, nicht nach kleinen Einzeländerungen.
- Die nötigen Testschritte für den Anwender als kurze, nachvollziehbare Bedienabläufe zusammenfassen. Keine fortlaufenden Testerfordernisse während der Implementierungsphase melden.

Diese Regel hat Vorrang vor älteren Formulierungen in dieser Planung, die einen sofortigen Test nach einer kleinen Änderung nahelegen. Kein Testlauf oder Release wird durch diese Dokumentationsänderung ausgelöst.

## Verbindliche Reihenfolge

1. **Bestehendes SieEncode Studio + mitgelieferte Runtime stabilisieren und die Bedienung vereinfachen.**
2. Kernabläufe und offene Dialog-/Hardware-Abnahmen mit exakten Kandidaten-Hashes belegen; freigegebenes Release herstellen und erst danach nach `main` integrieren.
3. **Anschließend** optionalen Wechsel der Ausführungsengine zur offiziellen, separat installierten RFiDGear-Bridge prüfen. Der aktuelle Bridge-Abnahmeentwurf bleibt als spätere technische Vorarbeit erhalten, ist aber nicht Teil des ersten Releases.

Keine produktive Runtime ersetzen, keine offizielle Bridge in das erste Release integrieren, keine Freigabe aus synthetischen oder historischen Tests ableiten. Der Releaseumfang umfasst bestehende, tatsächlich nachweisbare Funktionen; unbekannte Hardwarefähigkeit nicht schönreden.

## Warum die derzeitige Standardansicht noch zu komplex ist

Der Workspace-Rahmen in `MainWindow.xaml` hat bereits die drei richtigen Bereiche **Karten kodieren / Projekte / Karten untersuchen**. In den enthaltenen Seiten liegen aber weiterhin sehr viele RC15-Aktionen dicht nebeneinander. Der Schalter `ApplyOperatingMode` blendet einzelne technische Felder aus, macht daraus jedoch noch keinen konsequent geführten Ablauf.

Konkrete Stellen: Projektbibliothek bietet gleichzeitig Neu/Bearbeiten/Prüfen/Station/Teststation/Ordner/Löschen; Editor zeigt Profilauswahl, Projektdaten, Grund-/Produktivschlüssel, Vault- und Migrationsaktionen; TeamVaultWindow mischt Identitäts-/Einladungsaufgaben und historische Teamlisten-Funktionen in einem langen Dialog. Der Karten-Untersuchungsbereich enthält auch Wartung, die einen Transponder verändert. Status-, Nachweis- und Berechtigungsgrenzen müssen in der ersten Releasefassung klarer vermittelt werden.

## Ziel: drei Aufgaben statt Menü- und Button-Sammlung

### 1. Karten kodieren

- Ein Blick zeigt Projekt und freigegebenen Stand, Reader/Karte, Vaultberechtigung, Zustand, die **nächste erlaubte Aktion** und das letzte Ergebnis.
- Geführter Ablauf: Projekt auswählen → Karte nur lesend prüfen → gegebenenfalls ausdrücklich vorbereiten → Kartendaten eingeben → Kodierung explizit starten → read-only kontrollieren → nächste Karte/Abschluss.
- Pro Zustand **eine hervorgehobene nächste Aktion**; keine gleichzeitig gleichwertig wirkenden Varianten für Start, Prüfung, Diagnose, Recovery.
- Serienmodus, Mastering, Recovery und technischer Taskstatus bleiben erreichbar, werden aber nur im relevanten Ablauf beziehungsweise im Expertenbereich gezeigt. Kein stillschweigender Karten-Schreibvorgang und kein automatischer Wiederholungsversuch nach unklarem Status.

### 2. Projekte

- Bibliothek mit verständlicher Auswahl und drei erkennbaren Hauptzielen: **Neues Projekt**, **Bearbeiten**, **Zur Kodierstation**. Prüfbedarf wird im Status und vor Übergabe angezeigt; aufrufbare Prüfung bleibt erhalten.
- Editor: verständliche Abschnitte **Projekt → Profile → Kartendaten → Schlüsselzugriff → Speichern und prüfen**. Nur für die Situation erforderliche Eingaben und Schaltflächen prominent.
- Schlüssel selbst werden weder in der Standardansicht ausgegeben noch durch UI-Umbau in Projektdaten/Logs verlagert. Expertenfelder, Import/Export und Profilhistorie bleiben vorhanden.
- Vor Abschluss: gespeicherter Projektstand, Profilbindung, Vaultzugriff, Build-/Preflightstatus und nächster Schritt müssen erkennbar sein.

### 3. Karten untersuchen

- Standardansicht zuerst **Transponder analysieren** mit lesender Strukturübersicht und verständlichen Grenzen (nicht gelesen ≠ leer).
- Vergleich und Bericht als Folge der tatsächlichen Untersuchung, nicht fünf gleichgewichtige Startaktionen.
- **Wartung/Formatieren nur in der Expertenansicht**, zusätzlich zu allen bisherigen destruktiven Bestätigungen. Das ist bereits als erster, kleiner Source-Fix umgesetzt und im Dialog-Regressionstest verankert.

### Teamzugriff und Verwaltung

- Einstiegsentscheidung durch Tätigkeit, nicht kryptografischen Fachbegriff: „Eigenen Zugang einrichten“, „Kollegen Zugang geben“, „Rechte bearbeiten“, „Hauptverwaltung übergeben/wiederherstellen“.
- Für jede Aufgabe sichtbar: aktueller Windows-Benutzer, Rolle, Zielprojekt/-gruppe, erforderliche Aktion, **welche Person an welchem Arbeitsplatz als Nächstes handeln muss** und ob der andere Arbeitsplatz den neuesten öffentlichen Stand importiert hat.
- Moderne Offline-Gruppen nicht mit historischer Teamlistenpflege in demselben Standarddialog vermischen. Bestehende Identitäten, Schlüsseldateien und Berechtigungen nicht automatisch migrieren oder ersetzen. Physische Übergabe und Signatur-/Fingerprint-Abgleich bleiben unverändert.

## Release-Abnahme statt weiterer Grundsatzentwicklung

Für ein wirkliches erstes Release müssen folgende Nachweise an genau einem Releasekandidaten vorhanden sein:

1. **Erste Benutzung:** neues Projekt anlegen, Vorlage/Profil auswählen, Variablen eintragen, Vault erstellen/öffnen, speichern, prüfen und kodierbereit machen – ohne Anleitung durch einen Entwickler.
2. **Tägliche Bedienung:** vorhandenes Projekt wählen, Reader/Testkarte lesen, eindeutig kodieren, Ergebnis einschließlich read-only Readback prüfen, nach Fehler korrekt stoppen/weiterführen. Einzel- und bereits vorhandene Serienabläufe getrennt prüfen.
3. **Offline-Teamverwaltung:** positive Dialogwege für Delegation, Widerruf und Verwaltungsnachfolge zwischen zwei Windows-Benutzern/Rechnern soweit im Releaseumfang zugesagt; lokale Zustandsänderung und Importzwang klar anzeigen.
4. **Ergonomie:** echte UI-Kontrolle unter 100/125/150 % DPI, Tastatur-Fokus, Fehlermeldung ohne Scrollsuche, verständliche Sperrgründe, Navigation ohne verlorene Eingaben, keine gleichberechtigten Mehrfachstarts.
5. **Software & Bereitstellung:** vollständige relevante Studio-Regressionen, UI-Smoketests, MSI-Neuinstallation/Upgrade/Uninstall mit Datenerhalt, Quellstand/Artefakt-Hash, Version und Benutzer-Rückmeldungen dokumentieren. Bestehende gültige Nachweise versionsgebunden wiederverwenden; nicht künstlich wiederholen.
6. **Hardware:** mit ausdrücklich dafür freigegebenem Reader/Testkarten real nachweisen, was produktiv versprochen wird, einschließlich wesentlicher Schreib-/Readback-/Berechtigungsketten. Synthetic/VM ist dafür kein Ersatz.

**Offene Funktionen nicht durch Umbenennung des Kandidaten freigeben.** Der aktuelle Kandidat 7 aus der Offline-Gruppen-Reihe bleibt ein Kandidat. Releaseversion erst nach tatsächlich bestandenem Gate festlegen.

## Umsetzung in kleinen, abnehmbaren Schritten

- **UX-01 (aktiv):** Normal-/Expertenansicht sauber trennen; Wartungsfunktion nur für Experten; Klarheit der drei Arbeitsbereiche und Vermeidung doppelter Primäraktionen prüfen.
- **UX-02 (in Umsetzung):** Projektbibliothek und Editor nach dem tatsächlichen nächsten Schritt führen, mit vorhandenen Handlern und sicheren Speichergates. Die Bibliothek hält Prüfen/Bearbeiten/Kodierstation im Hauptweg; Untersuchung/Ordner/Löschen hinter weiteren Aktionen. Der Editor zeigt Bereitschaft und nächsten Schritt vor dem Formular und eine direkt erreichbare neue Gruppen-Vault-Option **erst nach vorbereiteten Profilen, Werten und Schlüsseln**. Unveröffentlichte Importadapter bleiben technisch vorhanden, sind aber aus der normalen 1.0-Oberfläche ausgeblendet.
- **UX-03 (aktiv):** Für das erstmalige Release 1.0 persönliche Identität, signierten Gruppenstand, neuen Gruppen-Vault und separaten Recovery-Zugang als **vier verschiedene Aufgaben** verständlich erklären. Keine bisherige Teamliste als stillschweigende Projektfreigabe laden. Mehrfachschlüssel nur mit bewusster Fingerabdruckwahl im Einrichtungsdialog; keine zweite Hauptverwaltung versehentlich erzeugen. Fehlende Gruppe/Recovery klar nennen; kryptografische Entsperrfehler nicht mit Schlüsselneuanlage „lösen“. Weiterhin keine Echt-Rollen-, Hardware- oder Windows-Abnahme vor dem Sammeltest.
- **UX-04 (aktiv):** Kodierstation und Kartenuntersuchung auf eine klare sichere Hauptaufgabe je Zustand reduzieren. Kodierung, optionale Kartenvorbereitung und Wiederholung nur ausdrücklich; Ergebnisse mit echten Nachweisgrenzen zeigen. Kartenuntersuchung nutzt eine verständliche Ergebnisübersicht mit gesondert aufklappbaren PICC-/Filedetails und weiteren Berichten.
- **REL-01:** Bestehende Testnachweise mit UI-/MSI-/Hardware-Restpunkten zusammenführen, reale Lücken schließen und den ersten Releasekandidaten abnehmen.
- **ENGINE-01 (geparkt):** Offizielle Bridge hinter optionalem Adapter statt Ersatz der bestehenden Runtime; erst nach der ersten stabilen Veröffentlichung.

## Änderung des ersten UX-Schrittes

`TestMaintenanceActions` wird in `ApplyOperatingMode` bei Standardansicht ausgeblendet und erscheint im Expertenmodus wieder. Die Prüfungen `VerifyStationLayout` und `VerifyOperatingModeContract` berücksichtigen die Trennung. In der Projektbibliothek heißt die eindeutige Übergabeaktion nun **„Zur Kodierstation“** statt des doppeldeutigen „Projekt wählen“; Handler und Automation-ID bleiben gleich. **Diese Änderungen sind committed, aber mangels Windows-WPF-Laufzeit hier noch nicht praktisch ausgeführt oder visuell abgenommen.** Keine sonstigen Kartenaktionen verändert.

## UX-03 – Vault- und Gruppenverwaltung: erste konkrete Vereinfachungen

**Bereits implementiert im Entwicklungszweig, aber noch nicht als MSI getestet:**

- **Persönlichen Zugang einrichten:** Ein eigenes kurzes Fenster mit vorhandene Identität wählen / bei Bedarf anlegen / öffentliche Einladung exportieren / Schließen. Die bisherige Teamlistenpflege, die ältere Vaults betrifft, hat einen getrennten Einstieg. Die persönliche Einrichtung lädt oder speichert die alte Teamliste nicht.
- **Einstellungen:** Persönlicher Zugang vor Gruppenverwaltung; längere Einrichtungshilfe standardmäßig eingeklappt. Der bisherige Hilfebutton führt zur kurzen Selbsttestanleitung `docs/VAULT-TEST-EINFACH-2026-11-21.md`.
- **Hauptverwaltung:** Ist nur eine lokal verwaltete Bibliothek vorhanden, wird sie lesend vorausgewählt; beim bloßen Öffnen wird **keine** lokale Registrierung synchronisiert. Speichern und Export sind als unterschiedliche, verständliche Schritte bezeichnet; Sonderaktionen für private Schlüssel/Nachfolge eingeklappt.
- **Delegationsplatz:** Einzige Bibliothek wird geladen. Ein persönlicher Zugang wird nur dann automatisch geladen, wenn genau **ein bereits bestehender lokaler Delegationsschlüssel** zu einer registrierten Person passt. Kein Schlüssel wird allein durch Öffnen erzeugt. Ohne aktive Vollmacht bleibt „Änderung signieren und exportieren“ deaktiviert. Mehrere nicht eindeutig zuordenbare Identitäten verlangen Auswahl.
- **Fehler:** Der Fehlerbereich des Delegationsplatzes bleibt außerhalb des langen scrollbaren Inhalts sichtbar.
- **Softwareprüfungen:** `VaultUiTestability` sichert die Trennung der persönlichen Einrichtung von alter Teamlistenpflege ab; `GroupDelegateTestability` sichert die Ablehnung mehrdeutiger Personenauswahl, fehlende Vollmacht sowie Wiederöffnen eines eindeutig vorhandenen Delegationsschlüssels ab. Die Prüfroutinen sind ergänzt, **noch nicht auf Windows ausgeführt**.

**Keine automatische Verwaltungsvollmacht, keine automatische Schlüsselgenerierung, keine Kartenaktion und keine Freigabe durch UI-Änderungen.** Die getrennte Windows- und Geräteabnahme, samt unabhängigem Fingerabdruckabgleich und Grenzen des Offline-Entzugs, bleibt verpflichtend.

**Abnahme erst am gebündelten Meilenstein:** Vorher weitere zusammenhängende UX-/Vault-/Projektarbeiten umsetzen und Testfälle im Quellcode ergänzen. Danach unter Windows gemeinsam bauen, die gesamte WPF-UI-Runde und Sicherheitsregressionen ausführen und erst für diese gesammelte Version einen Testkandidaten samt Prüfsummen, Installations-/Upgrade- und Datenerhaltungsnachweisen erstellen. Kein älterer Kandidat wird stillschweigend als neuer Stand ausgegeben.

## UX-02 – Projektbibliothek und Editor: laufendes Implementierungspaket

Die Änderungen sind auf `codex/offline-vault-groups` gespeichert. **Kein Einzeltest, keine neue MSI und keine neue Releasefreigabe**; erst zusammenhängende Funktionen abschließen und später gesammelt prüfen.

- Die Bibliothek behält `Neues Projekt`, `Projekt bearbeiten`, `Projekt prüfen` und `Zur Kodierstation` als sichtbaren Hauptweg. Die bisherigen Aktionen `Karte untersuchen`, `Projektordner öffnen` und `Projekt löschen` bleiben mit unveränderten Handlern hinter dem aufklappbaren Bereich `Weitere Projektaktionen` erreichbar; das technische Expertenmenü bleibt erhalten.
- Der gewählte Projekteintrag erklärt den **nächsten möglichen Schritt** anhand der tatsächlich gültigen Schranke `CanOpenStation`. Ein vorhandener Betriebbuild ohne alten Vault darf nicht als reproduzierbar neu baubar dargestellt werden; ein Altbuild ohne Erkennungsfunktion erhält weiterhin seinen Hinweis.
- Im Projekteditor stehen Bereitschaftsstatus und nächste Aktion **vor** den vielen Eingabefeldern. Zusätzliche Ausgangsschlüsselsätze sowie die bewusste Gruppenprojektübernahme sind weiterhin vorhanden, aber nicht permanent als alternative Hauptaktionen sichtbar.
- Das bestehende `RefreshEditorWorkflow` hebt maximal **eine** passende nächste Aktion hervor. Bei bestehendem Projekt ohne ursprünglichen Vault wird der gefährliche Weg `Neue Schlüssel erzeugen` ausdrücklich **nicht** als normale Standardempfehlung hervorgehoben; die vorhandene Warnung und Bestätigung bleiben.
- Eine wichtige bisherige Bedienlücke ist beseitigt: Wenn in einem **Gruppenprojekt** Schlüsselwerte bearbeitet wurden, erscheint in der Standardansicht `Vault-Änderungen sicher speichern` als erlaubter nächster Schritt; dafür ist kein Umschalten in den Expertenmodus mehr erforderlich. Der Aufruf verwendet unverändert den vorhandenen verschlüsselten Vault-Speicherweg und die Berechtigungs-/Revisionsprüfungen. Die Anzeige erteilt selbst keine Vault- oder Gruppenrechte.
- Die bestehenden Software-Smokeprüfungen enthalten zusätzliche **noch nicht ausgeführte** Invarianten für die Trennung der Projektaktionen, Platzierung des Status, Schlüsselsatz-Expander, maximal eine hervorgehobene Editoraktion und die sichere Gruppen-Vault-Speicheraktion in der Standardansicht.

**Abgrenzung:** Die Kartenkodierung, die tatsächlichen Rechte, die Runtime, das Installationpaket und `main` werden durch dieses Paket nicht umgestellt. Vor einem gebündelten Testlauf sind noch weitere UX-Verbesserungen vorgesehen; die Tests folgen **nicht pro Commit**.

## UX-04 – Kodierstation: geführter Ablauf ohne zusätzliche Knöpfe

**Implementiert im Entwicklungszweig, noch kein Windows-Build, Testlauf oder MSI.** Die nächste manuelle Abnahme findet erst nach weiteren zusammenhängenden UX-Verbesserungen statt.

- Der feste Hinweis oben nennt den Zustand und die nächste Bedienhandlung. Technische Evidenz ist weiter lesbar, im normalen Arbeitsablauf jedoch eingeklappt; im Expertenmodus öffnet sie sich.
- Auf der Stationsseite bleiben **Projekt wählen** und **Projekt prüfen** vorn. Die projektlose Untersuchung und der technische Projektordner liegen in `Weitere Projektaktionen`. In der lesenden Kartenprüfung bleiben Erkennen, Altprojekt-Ersatzprüfung und vorhandener Bericht zugänglich; zusätzliche Dateiauslesungen sind eingeklappt.
- Die **optionale, die Karte verändernde Vorbereitung** ist explizit als solche beschriftet und standardmäßig eingeklappt. Ihre vorhandenen Bestätigungs- und Fähigkeitsprüfungen werden nicht ersetzt; bloßes Aufklappen führt **keine** Kartenaktion aus.
- Bei den Kodierdaten gibt es nicht mehr gleichzeitig mehrere `PrimaryButton`-Schaltflächen. `UpdateStationActionEmphasis` hebt höchstens eine passende **bereits freigeschaltete** Aktion hervor: Projektprüfung, Nur-Lese-Kartenprüfung oder nach tatsächlichem `Prepare` den ausdrücklichen Kodierstart. Ein laufender/unbekannter Hostzustand wird niemals durch eine automatische Schreibaktion ersetzt.
- `Enter` zum Bereitstellen wird nur innerhalb eines tatsächlich fokussierten Kartendaten-Textfelds angenommen, nicht global auf der gesamten Station. F8 aktualisiert lediglich den bereits gestarteten Lauf; die finale Auswertung bleibt automatisch und verlangt korrelierte Terminal-/Readback-Evidenz.
- Nach erfolgreicher Datenbereitstellung wird **erneutes Bereitstellen** gesperrt; nach echtem Hoststart werden **weitere Startaktionen** gesperrt. F8 wird erst nach einem tatsächlichen Hoststart angeboten. Eine Aktualisierung von Runtime-/Reader-Einstellungen darf keinen parallelen zweiten Start wieder freischalten.
- Der Sitzungsabschluss wird nach einem terminalen Lauf automatisch aufgeklappt. Nur bei einer wirklichen Fehlerbuchung wird der separate Bereich für **Retry/Überspringen** geöffnet; weder Wiederholung noch Überspringen erfolgen automatisch. Diagnose und detaillierte Lognachweise sind erreichbar, aber nicht ständig prominent.
- **Sicherheit:** Solange ein von SieEncode gestarteter Karten-/Readerprozess noch läuft, bleiben eine neue Datenvorbereitung und der manuelle F9-Abschluss gesperrt. Auch die Handler selbst prüfen dies, statt nur auf deaktivierte UI-Buttons zu vertrauen. Ist der Host bereits beendet, kann ein nicht beweisbar erfolgreicher Lauf ausdrücklich als fehlgeschlagen gebucht werden; erst danach wird ein alter, beendeter Launch-Handle verworfen. Ein F9-Abschluss ist **keine** Erfolgsaussage.
- Die vorhandenen `UiTestability`-Prüfungen sind um aufklappbare Zusatzaktionen, höchstens eine primäre Stationaktion, keine vorgezogene Readbackprüfung und kein F9/Prepare bei laufendem Prozess ergänzt. Die F9-Zustandsregel wird zusätzlich mit einer kleinen synthetischen Wahrheitswertmatrix geprüft. **Der Testcode wurde geschrieben, aber noch nicht ausgeführt.**
- Die Statusleiste des normalen Kartenablaufs verliert nach einem bewusstem F9-Abschluss, einer übersprungenen Seriennummer und einem regulären Sitzungsabschluss keine alte `Bereit zur Kodierung`-Meldung: jeder dieser Schritte erhält einen eigenen, ausdrücklich **nicht erfolgreichen** beziehungsweise vollständigen Abschlussstatus.
- Quellcode-Sichtkontrolle: XAML-Tags und neue Steuerelementnamen sind konsistent und es wurden keine doppelt benannten Bedienelemente gefunden. **Keine** native WPF-/Windows-Ausführung, kein Build, keine Aktionen an Readern/Karten, kein Release in dieser Runde.

**Restarbeit vor gemeinsamer Abnahme:** Gesamtfluss im installierten Windows-Studio mit ausschließlich genehmigten Testkarten, Readback-/Serien- und Fehlerfällen visuell prüfen; tatsächliche Host-Abschlusszustände (einschließlich unklarer und abgestürzter Prozesse) gegen die verwendete Runtime belegen. Keine Hardwarefreigabe oder neues Release aus dieser Codearbeit ableiten. Offizielle neue RFiDGear-Bridge bleibt weiterhin außerhalb des ersten Releases.

## UX-04 – Karten untersuchen: Befund vor technischen Details

**Aktueller Implementierungsstand auf `codex/offline-vault-groups`:** Quellcode gespeichert, neue synthetische UI-Tests vorbereitet. **Keine Ausführung unter Windows, keine Hardwarefreigabe, kein MSI und kein Release.** Gebündelte Prüfung folgt erst nach Abschluss weiterer zusammenhängender Entwicklungsarbeiten.

- Die unabhängige, nur lesende Transponderanalyse bleibt die erste deutlich erkennbare Aktion. Eine **Befundkarte** nennt Status, Kurzbeschreibung und nächsten sicheren Schritt. Der letzte Textbericht kann direkt neben dem Befund geöffnet werden, sobald eine zugehörige Datei tatsächlich vorliegt.
- PICC-Berechtigungen, AIDs, File-Metadaten und gegebenenfalls gelesene HEX-Inhalte bleiben im bisherigen Strukturbaum vollständig zugänglich, sind jedoch standardmäßig unter `Kartendetails und technischer Nachweis` eingeklappt. Die Oberfläche kennzeichnet ausdrücklich: **Es ist der letzte abgeschlossene Lesevorgang, keine Liveaussage über eine möglicherweise inzwischen gewechselte Karte.**
- `CardRecognitionReport` wird für die Ergebnisdarstellung nur als **vollständig** gewertet, wenn `ReadOnly` und `State=RECOGNIZED` gelten und die Strukturklassifikation zur AID-Zahl passt. **Bestätigt 0 Applications** erfordert `Structure=LEER` und eine tatsächlich erfolgreiche Inventur. Widersprüchliche oder unbekannte Befunde ergeben `Kartenstatus ungeklärt`, niemals `leer`. Auch die Baumansicht zeigt bei unbekanntem Befund keine erfundene Leeraussage.
- Nach bestätigter Entdeckung von mindestens einer Application erscheint `Application genauer lesen` als zusätzlicher **rein lesender** Folgeschritt nur dann, wenn die bestehende Runtime dessen Fähigkeit tatsächlich nachgewiesen hat. **Dieselbe Bedingung wird auch im Klick-Handler geprüft**, nicht nur durch eine deaktivierte Schaltfläche. Unbekannte, widersprüchliche oder nicht als read-only ausgewiesene Berichte reichen nie aus. Die bisherige Dialog-/Schlüsselauswahl und die Fachprüfungen bleiben erhalten.
- Alte oder neue Berichte, PDF-Istreport, projektbezogene Vergleiche und Reader-Diagnosen bleiben erreichbar, überlagern aber den Normalablauf nicht. Die Destruktivaktion `Transponder formatieren` bleibt weiterhin **nur im Expertenmodus** und mit den vorhandenen separaten Bestätigungen verfügbar.
- **Nach einer ausdrücklich gestarteten Formatierung** wird ein vorheriger Lesebefund als veraltet aus der aktuellen Ansicht entfernt. Weder ein bestätigter noch ein fehlgeschlagener Formatlauf belegt ohne erneute autoritative Nur-Lese-Inventur einen leeren Transponder. Dies wird im Ergebnisstatus und der nächsten Aktion ausgewiesen.
- Beginnt eine neue projektlose Analyse, wird der vorherige sichtbare Kartenbefund samt vorherigem Berichts-Button zurückgesetzt und der neue Status als `Lesevorgang läuft` gekennzeichnet. Wenn die Auswertung einer Analyse oder Formatierung eine unerwartete Ausnahme meldet, zeigt der Ergebnisbereich `Untersuchungsergebnis nicht auswertbar` und fordert zur Prozess-/Kartenprüfung statt zum Weiterschreiben auf.
- Wird ein **anderes operatives Projekt** geladen, werden die vorherigen Karten-/Berichtsangaben in der Untersuchungsansicht ebenfalls invalidiert. Ein früherer AID-/Schlüsselbefund darf nicht stillschweigend als Nachweis zum neuen Projektkontext erscheinen. Alte Berichtsdateien auf Datenträgern werden durch diese Anzeigeänderung nicht gelöscht.
- Die vorhandenen `VerifyStationLayout`- und `RunUiSmokeTest`-Prüfroutinen wurden für die einklappbare technische Ansicht und eine synthetische Zustandsmatrix erweitert. Sie unterscheidet `UNKNOWN`, bestätigt leer, bestätigt belegt und widersprüchliche beziehungsweise nicht nur lesende Berichte. **Noch nicht ausgeführt.**

**Nächste Schritte ohne Einzeltests:** Verbleibende Hilfetexte, Berichtsrückkehr und Navigation konsolidieren, dann den gesamten Meilenstein gesammelt unter Windows prüfen. Das bestehende Kodier-/Formatierverhalten und die RFiDGear-Runtime sind nicht ausgetauscht; der offizielle RFiDGear-Unterbau bleibt nach dem ersten Release eingeplant.

**Quellcode-Abgleich am 21.11.2026:** Neue XAML-Steuerelemente in `MainWindow.xaml` sind eindeutig, das Tag-Nesting ist ausgeglichen; Anzeige- und Leseberechtigungsprüfungen sind sowohl in UI-Code als auch im vorgesehenen synthetischen Smoke-Vertrag verankert. Das ist eine **statische Sichtkontrolle**, kein Ersatz für die spätere Windows/WPF-Komplettprüfung.

## UX-03 – Fortsetzung: Vault-Einstieg und erste Teamfreigabe

**Auf dem Entwicklungszweig implementiert, noch nicht durch einen Windows-Build/Test ausgeführt. Keine MSI-/Releaseerstellung.** Diese Verbesserungen bilden einen zusammenhängenden Teil unseres späteren **gemeinsamen** UX- und Sicherheitsmeilensteins:

- **Persönliche Identität in Einstellungen:** `UpdateVaultSetupStatus` beurteilt ausschließlich vorhandene persönliche Windows-Zertifikate und öffnet **nicht mehr** die alte `TeamVaultRecipientStore`-Empfängerliste. `0 / 1 / mehrere` haben unterscheidbare Rückmeldungen. Eine persönliche Identität ist noch **kein Nachweis** für Gruppenrechte oder die Entschlüsselungsberechtigung an einem bestimmten Projekt.
- **Persönlicher Einrichtungsdialog:** Die oben stehende Kurzmeldung sagt, was die aktuelle Person selbst tun kann. Nur ohne vorhandenen Schlüssel wird „Neue persönliche Identität anlegen“ angeboten. Bei genau einem Zertifikat wird es vorausgewählt, bei mehreren – auch identisch benannten – **keines**. Nach einem bewussten Auswahlschritt überlebt diese Fingerabdruckwahl das Neuladen. Bei Neuanlage wird der erzeugte Schlüssel anhand seines **Fingerabdrucks statt des Anzeigenamens** wiedergefunden. Öffentlicher Einladungsexport ist optional, ändert keine alte Teamliste und vergibt keine Gruppenrechte.
- **Gruppeneinrichtung nach Rollen:** Ein gesonderter lokaler Gruppenstatus beschreibt die Zahl vorhandener Registrierungsdateien **ohne daraus Berechtigungen abzuleiten**. Im normalen Ablauf wird je nach erstem/anschließendem Import genau eine passende Aktion angeboten; eine zusätzliche unabhängige Bibliothek ist ein seltener Sonderfall. Hauptverwaltung, begrenzte Vollmacht, Recovery, Verwaltungsnachfolge und bisherige Team-Vaults sind funktional erhalten, jedoch keine gleichrangigen Ersteinrichtungsbuttons mehr. Nach bestätigtem Gruppenimport/Verwaltungswechsel aktualisiert die Seite ihren sichtbaren Status.
- **Hauptverwaltung:** Das Verwaltungsergebnis steht **oben** im Fenster. Das Erzeugen einer **zweiten unabhängigen Hauptverwaltung** wird nicht mehr als Standardaktion angezeigt, sondern erfordert weiterhin ausdrückliches Öffnen und Bestätigen. Signiertes Speichern und Export bleiben getrennte bewusste Aktionen. Keine Schlüsselneuanlage oder lokale Trust-Synchronisation beim bloßen Öffnen.
- **Projekt-Vault mit Zertifikatsempfängern:** Auch der ältere `VaultAccessWindow` zwingt bei mehreren privaten Identitäten zur bewussten Auswahl. Das zusätzliche Erstellen von Zertifikaten ist nicht mehr Teil des normalen Freigabeablaufs. Bestehende Empfänger werden beim Öffnen nicht ergänzt/ersetzt; Änderungen bleiben bis zur ausdrücklichen passphrasengeschützten Freigabe ein Entwurf.
- **Neues Gruppenprojekt:** Der Dialog erklärt zuerst die vorhandene Gruppenregistrierung, danach die gewählte Gruppe und den neuen Projektnamen. Die technische Gruppen-ID ist nicht mehr Teil des normalen Gruppenlabels, bleibt für die eigentliche eindeutige Projektbindung vorhanden. Der Erstellen-Knopf bleibt ohne gültige Bibliotheks-/Gruppenauswahl deaktiviert. Eine neue Vault-Kopie wird nur nach bewusster Bestätigung erstellt; Originale werden nicht stillschweigend migriert oder überschrieben.
- **Nachweise:** Der bereits vorhandene synthetische `VaultUiTestability`-Smokevertrag wurde um zwei gleichnamige Zertifikate, explizite Fingerabdruckwahl und unveränderte alte Teamliste erweitert – sowohl im persönlichen Zugang als auch im per-Projekt-Vault. Die Kurzanleitung `docs/VAULT-TEST-EINFACH-2026-11-21.md` wurde auf die neuen Aufgabenbezeichnungen aktualisiert. **Code ergänzt, noch nicht ausgeführt.**

**Fachliche Grenzen bleiben:** Es gibt keine automatischen Gruppenfreigaben, kein nachträgliches Fernsperren alter Offline-Vaultkopien, keine automatische Zertifikatserstellung beim Öffnen, keine Umgehung der Recovery-Anforderungen und keine Änderung der tatsächlichen Signatur-/Trust-/Vault-Kryptographie. Jeder produktive Schreib- und Gerätezugriff benötigt weiterhin eigene Nachweise und explizite Freigabe.

**Sammeltest später:** Ersteinrichtung mit null/einer/mehreren Identitäten; exportierte Einladung; zentral signierte Gruppenfreigabe mit zwei getrennten Windows-Benutzern; erneuter Import, Ablehnung unberechtigter Gruppen; Projekt-Vault-Empfängerliste und Recovery, Widerruf und alte Offline-Kopie; WPF-Layout, Fehlerführung, MSI-Upgrade und Datenerhalt. Keine Zwischenkandidaten nur für diese UI-Schritte erzeugen.

### Ergänzung zum Vault-UX-Paket: Einstieg und Statik

- Auf der Startseite verwendet **dieselbe vorhandene Vault-Aktion** bei noch nicht ausgewähltem Projekt den Weg zu `Einstellungen → Mein persönlicher Vault-Zugang`; sobald ein Projekt ausgewählt ist, führt sie unverändert zum Projekteditor. Kein zusätzlicher Startbutton ist nötig.
- Die Vorgabe `AGENTS.md` wurde auf denselben verbindlichen Rhythmus aktualisiert: mehrere zusammenhängende Quelländerungen, synthetische Prüfungen im Code vorbereiten, **ein gebündelter Windows-/Sicherheits-/MSI-Abnahmelauf** statt wiederholter Kleinsttests.
- Die Quelltextkontrolle der acht zentralen geänderten C#-Dateien ergab ausgeglichene äußere Klammerstrukturen ohne offensichtliche lexikalische Brüche. Die WPF-XAML-Hierarchie wurde auf ausgeglichene Tags, eindeutige `x:Name`-Werte und vorhandene Status-/Aktions-Elemente kontrolliert. **Nicht mit einem Compilerlauf verwechseln; noch kein Windows-Build oder manueller Dialogtest durchgeführt.**

## UX-03 / UX-02 – Clean-slate-1.0-Bereinigung (Fortsetzung)

- Unveröffentlichte RFiDGear-JSON-Importe und die bisherige Team-Empfängerliste sind aus der normalen Oberfläche **ausgeblendet**. Die Altadapter sind im Code vorerst enthalten, da ihr vorschnelles Löschen keine Sicherheit oder Bedienbarkeit verbessert; sie gehören **nicht** zum ersten Produktumfang. In persönlichen Projekt-Vaults lädt `ManageVaultAccess_Click` die historische `TeamVaultRecipientStore`-Vorlage **nicht mehr** ein; Empfänger werden projektspezifisch ausdrücklich gewählt.
- Neue Projekte erhalten direkt im Editor die Entscheidung `Gruppen-Vault für dieses Projekt einrichten`. Ohne geladenen Entwurf oder ohne vollständige Profil-/Werte-/Schlüsselvorbereitung bleibt sie gesperrt; nach existierender Gruppenbindung wird sie ausgeblendet. Das ist keine automatische Migration.
- Der Gruppenprojekt-Dialog nennt vor einem Klick fehlende Mitglieder oder fehlenden separaten Recovery-Zugang und sperrt die Erstellung entsprechend. Die eigentliche Kryptographie/Signaturprüfung und die Berechtigung des privaten Windows-Schlüssels werden weiterhin **erst beim Erstellen** autoritativ geprüft.
- Ein verweigerter kryptografischer Gruppen-Vault-Zugang führt zu erklärenden Schritten (eigenes Windows-Zertifikat, richtige Gruppe, signierte Revision, Abgleich bestehender Empfänger oder mögliche Integritätsstörung). Keine Schlüsselerneuerung oder Umgehung als vermeintliche Standardlösung.
- In der Hauptverwaltung wird die interne Gruppen-ID aus der normalen Tabelle ausgeblendet. Vault-Dateisicherungsordner liegt unter einem selten benötigten aufklappbaren Bereich statt vor der persönlichen Ersteinrichtung; Recovery-Identität und Dateisicherung werden ausdrücklich getrennt.
- **In-App-Erklärung:** `docs/VAULT-EINFACH-1.0.md` liegt über `Einstellungen → Teamzugriff → Vault verstehen` vor. Der spätere kurze Sammeltest `docs/VAULT-TEST-EINFACH-2026-11-21.md` beginnt jetzt ohne angebliche bestehende Projektdateien, Teamlisten oder veröffentlichtes Altrelease.
- `AGENTS.md` übernimmt das verbindliche 1.0-ohne-Altprojekt-Leitbild. Neu angelegte Quellen sind auf GitHub gespeichert; sämtliche WPF-/MSI-/Hardwaretests bleiben gesammelt auf den späteren Meilenstein verschoben.

**Grenze der hier geprüften Quellen:** Nur Code-/Struktur-Sichtprüfung. Kein Build, keine tatsächlich getestete Freigabe, kein Produktiv-Release und keine Zusage, dass die gesamte UI unter Windows bereits fehlerfrei ist.

## SEC-01 – Sicherheitsgrenze beim Entzug von Offline-Zugängen

**Wichtiger Architekturentscheid für 1.0, bislang nicht als sofortige Sperre erfüllbar.** Es wurde konkret festgestellt, dass ein historischer Gruppen-Vault mit alter signierter Richtlinie und ehemals freigegebenem privatem Zertifikat auch nach einem neuen Gruppenentzug entschlüsselbar bleiben kann. Ein eingespielter neuer Gruppenstand ändert nichts an einem separat verwahrten alten Dateischnappschuss. Die Wieder-Verschlüsselung des aktuellen Projekt-Vaults rotiert lediglich dessen zufälligen Inhaltsschlüssel, nicht automatisch die auf Karten/Lesern verwendeten DESFire-Schlüssel.

**Im Entwicklungszweig umgesetzt:**
- Frühere Gruppen-Vault-Stände werden nicht mehr automatisch unter `Projektordner/GroupVaultBackups` für alle Projektberechtigten abgelegt. Default ist nun das lokale Profil des ausführenden Benutzers, bibliotheks- und projektgebunden. Explizit übergebene Backup-Pfade im Projektordner werden abgewiesen. **Achtung:** Das lokale Verzeichnis ist nicht per se ein beweisbar exklusiv verwalteter Sicherheits-Tresor; Zugriffsrechte und Sicherungsstrategie müssen gesondert geprüft werden.
- Vor Signieren eines Entzugs und nach Export zeigt die Hauptverwaltung die Grenze und die erforderlichen Anschlussmaßnahmen. Der Gruppen-Vault-Empfängerabgleich benennt die aktuelle Reverschlüsselung ausdrücklich **nicht** als vollständigen Entzug früherer Kopien oder Kartenschlüssel.
- Synthetische Regressionen für abgelehnte Sicherung im gemeinsamen Projektordner, unveränderte Datei bei Konflikt und nachvollziehbare Entzugs-Erkennung wurden im Quellcode ergänzt. **Noch nicht ausgeführt.**

**Grundsatzentscheidung für das erste Release präzisiert:** Dauernde Internetverbindung und SAM-Leser sind keine praktikablen Pflichtvoraussetzungen. SieEncode soll neben Kodierung eine **allgemeine, zentral administrierte und gelegentlich synchronisierte Schlüsselverwaltung** mit verschlüsselten Offline-Arbeitsbeständen sein. **Berechtigte Benutzer müssen Schlüssel bei Bedarf auch im Klartext anzeigen, kopieren und für SIPORT nutzen können.** Damit ist sofortige, nicht umgehbare Rücknahme früher ausgehändigter Geheimnisse prinzipbedingt ausgeschlossen. Der Sicherheitsanspruch muss deshalb auf Zugriffskontrolle für zukünftige Ausgabe, verschlüsselten vollständigen Offline-Bestand mit rollenabhängigen Entschlüsselungsrechten, Versionskontrolle, auditierbare normale Abläufe und kontrollierte Karten-/Leserschlüsselrotation bei Kompromittierung zugeschnitten sein. Vollständiger neuer Rahmen: `docs/design/ZENTRALE-SCHLUESSELARCHITEKTUR-1.0.md`. Bereits vorhandene Vault-Codepfade bleiben vorerst erhalten, die zentrale Synchronisation und Schlüsselverwaltung sind **noch nicht implementiert**.

Keinen Release als „entzogene Mitarbeiter verlieren sicher jeden Zugriff auf alte Schlüsselkopien“ darstellen. Keine automatische Kartenrotation oder destruktive Änderung aus einem Gruppenentzug ableiten.

## KEY-01 – eigenständige zentrale Schlüsselbibliothek als Produktanforderung

Nicht nur Kodieren: Verzeichnis für Kunden, zentrale Schlüsselsätze, stabile Schlüsselreferenzen, Schlüsselversionen, Verwendungszweck und Berechtigungen. Getrennte, verantwortete Vorgänge: **geheimen Wert anzeigen**, in Zwischenablage kopieren, importieren, für **SIPORT** exportieren, an Projekt/Karte binden und kontrolliert rotieren. Die Projekte dürfen weiterhin keine geheimen Werte unverschlüsselt tragen.

**Reihenfolge:** 1. Modell und Rechte-/Key-Version-Vertrag → 2. Wiederverwendung des vorhandenen verschlüsselten persönlichen/Gruppen-Vault für die vollständige lokal verfügbare Schlüsselbibliothek ohne manuelle Projekt-/Kundenauswahl → 3. Bedienoberfläche für Schlüssel verwalten → 4. zentrale Masterdatenbank und signierter, verschlüsselter Abgleich ohne ständige Netzverbindung → 5. Konflikt-/Widerrufs-/Export-/Wiederherstellungs-Sammeltests inklusive expliziter SIPORT-Ausgabe und echter Kartentestkarten. Erst tatsächliche Nachweise erlauben Freigabe. Vorher keine neue Raw-Key-Verteilungslogik oder produktiver Export ohne Sicherheitskonzept implementieren.

Die bisherige Online-only-/HSM-Architektur ist **nicht** der Release-Standard; siehe das ersetzte Zielbild unter `docs/design/ZENTRALE-SCHLUESSELARCHITEKTUR-1.0.md`.


## KEY-03 – Keine Schlüsseldiversifikation in SieEncode 1.0

**Verbindlicher Ausschluss:** Keine Diversifikationsverfahren, Ableitungsparameter, Profile, UI-Optionen oder Tests hierfür in Version 1.0 planen oder umsetzen. Das Produkt soll die ausdrücklich hinterlegten vorhandenen Kundenschlüssel verwalten und verwenden, einschließlich Klartextausgabe für SIPORT, vollständiger Offline-Bibliothek, Versionierung und kontrolliertem Schlüsselwechsel. Eine spätere mögliche Erweiterung darf den 1.0-Fertigstellungsgrad nicht beeinflussen.

## KEY-02 – Vollständige Offline-Bibliothek statt Einsatzpaketen (21.11.2026)

**Benutzerentscheidung:** Hauptverwalter führt **immer alle Schlüssel aller Kunden und Projekte** mit. Eine projektweise oder kundenweise Mitnahmeauswahl ist im Alltag zu unbequem und darf **nicht** zur Pflicht des normalen Arbeitsablaufs werden. Das gilt auch für Schlüssel, die beim Kunden erst unerwartet für SIPORT, Leser oder Kartenauswertung benötigt werden.

- Nach Synchronisation besitzt der Hauptverwalter auf seinem PC die **vollständige verschlüsselte lokale Bibliothek** samt eindeutiger Key-ID, Versionierung, Schlüsselzuordnungen und verwalteter Historie. Ohne Internet nutzbar; kein „Diesen Kunden vorher laden“.
- **Bei anderen Benutzern** bleibt eine zentrale, explizit festgelegte Autorisierung maßgeblich; vollständige lokale Katalogverfügbarkeit ist nicht automatisch ein Recht zur Klartextentschlüsselung aller Geheimwerte. Für Vollzugriff berechtigte Mitarbeiter darf der gesamte Bestand ebenfalls nutzbar sein. Bloß versteckte UI-Elemente genügen nicht als Schutz.
- Standard-Navigation: Kunden/Schlüsselsätze suchen → erforderlichen Schlüssel gezielt entsperren und bewusst anzeigen, kopieren oder für SIPORT bereitstellen. Geheime Werte sind bis dahin verdeckt, nicht dauerhaft als Klartextliste geladen.
- Bei Gelegenheit **automatischer Vollabgleich** des autorisierten Katalogs mit nachvollziehbarem Stand und geprüfter Revision; Delta-Transport ist erlaubt, solange er den vollständigen Bestand zuverlässig wiederherstellt. Auch USB-/SSD-Pakete müssen diesen Standard ohne Kunden-Selektion erfüllen.
- **Risiko:** Ein kompromittierter Vollzugriff-PC bzw. ein Mitarbeiter mit früher rechtmäßig lesbaren Schlüsseln kann potentiell **alle Kunden** betreffen. Daher starke Verschlüsselung, Geräte-/Benutzerschutz, geeignete Datenträgerverschlüsselung und klar dokumentierte Rotation/Incident Response; **keine falsche Zusage rückwirkenden Entzugs**.

Keine zentrale Synchronisation, Vollabgleich-UI oder neue Schlüsselverwaltung sind durch diese Roadmap-Änderung bereits implementiert. Der integrierte vollständige Bestandsabgleich gehört in die spätere gemeinsame Funktions- und Sicherheitsabnahme.

## KEY-04 – Ein SieEncode als einzige Bedienoberfläche, Zentrale ohne UI (21.11.2026)

**Erneute ausdrückliche Nutzerentscheidung:** Dies ist weiterhin die Entwicklung der **ersten Veröffentlichung 1.0**, nicht eine Migration vorhandener produktiver SieEncode-Installationen. Keine Rücksicht auf angebliche Altprojektdateien/Alt-Vaults oder Zwischen-Releases als eigenständigen Aufwand. Vorhandene lauffähige Bausteine gezielt wiederverwenden; interne Dateiformate bei Bedarf vor dem ersten Release **einmal konsistent ändern**, statt künstliche Migration und zwei Geheimwertquellen einzuführen. Die endgültigen 1.0-Sicherheits-, Recovery- und Hardwareprüfungen bleiben notwendig.

**Nur SieEncode Studio hat eine Oberfläche.** Alle Neuanlagen, Änderungen und Freigaben von Kunden, Schlüsseln, Schlüsselsätzen, Rollen/Gruppen, Benutzer-/Geräteidentitäten, Recovery, Projekten und SIPORT-Ausgabe erfolgen **dort**. Die zentrale Instanz ist ein **headless Hintergrunddienst** für authentisierte Datenannahme, zentrale Revisions-/Berechtigungsprüfung, Speicherung, Synchronisation, Audit und Backup. Kein Web-Admin-Portal, keine zweite Kunden-/Schlüsselpflege und keine Serverbedienung im normalen Arbeitsfluss.

**Lokal-first Ablauf:** SieEncode darf bei fehlender Verbindung weiter mit dem vollständigen verschlüsselten Bestand arbeiten und Änderungen als **lokale Entwürfe mit Ausgangsrevision** festhalten. Bei Verbindung synchronisiert dieselbe Anwendung automatisch. Der Hintergrunddienst akzeptiert nur berechtigte, konsistente Änderungen und gibt ansonsten den Konflikt zurück. Die Auflösung findet in **SieEncode** statt; bis zur Quittung „noch nicht zentral übernommen“ anzeigen, nicht stillschweigend Erfolg melden.

**Entwicklungsreihenfolge aktualisiert:** Den bereits begonnenen metadatenfreien Geheimwertvertrag `KeyLibraryContracts` (stabile IDs/Versionen, keine Rohwerte) mit zentralen CRUD-/Sync-Operationen für **dieselbe SieEncode-UI** ergänzen, dann einen einzigen kryptografisch getrennten lokalen Bibliotheks-Vault darauf aufsetzen, Projektreferenzen auflösen, danach den headless Server samt vollständigem Abgleich anbinden. Erst danach finale UI-/Windows-/Sicherheitstests und echte Hardwareabnahme. Keine separate Server-UI und keine Diversifikation.

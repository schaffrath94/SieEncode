# Erste stabile SieEncode-Veröffentlichung: Oberfläche vor Runtime-Migration

Stand: 21.11.2026. Arbeitsgrundlage für den bestehenden Entwicklungszweig, **keine Releasefreigabe**.

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
- **UX-02 (in Umsetzung):** Projektbibliothek und Editor nach dem tatsächlichen nächsten Schritt führen, ohne Handler/Autosaves/Legacy-Importe zu entfernen. In der Bibliothek liegen Prüfen/Bearbeiten/Kodierstation im Hauptweg, Untersuchung/Ordner/Löschen hinter weiteren Aktionen. Der Editor zeigt Bereitschaft und nächsten Schritt vor dem Formular, klappt die Ausgangsschlüsselsatz-Verwaltung und Gruppenprojektübernahme ein und hebt jeweils höchstens eine sichere nächste Aktion hervor.
- **UX-03 (aktiv):** Team-/Vault-Einstiege und positive Delegations-/Nachfolgedialoge verbessern und mit echten Rollen testen. Persönliche Identität ist inzwischen als eigenständiger Zweischritt-Dialog von der bisherigen Teamlistenpflege getrennt; Delegation und Hauptverwaltung wurden nach Aufgabe gegliedert, Status und Fehlermeldungen verbessert.
- **UX-04 (aktiv):** Kodierstation auf eine hervorgehobene sichere Aktion pro Phase reduzieren. Zusätzlich Leseprüfungen, optionale Vorbereitung, Fehlerbehandlung und technische Nachweise aufklappbar strukturieren; automatische Ergebnisdarstellung und Freigabe erst nach korreliertem Terminalstatus unverändert bewahren.
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

**Nächste Umsetzung:** Quellstand unter Windows bauen, WPF-Smoketest inklusive neuer UX-Gates ausführen, dann Testkandidat mit Prüfsummen und Installations-/Upgrade-Prüfung erstellen. Erst mit genau diesem Kandidaten kann der Anwender die verkürzte UI-Testfolge wirklich bedienen und die sichtbaren Ergebnisse melden. Kein älterer Kandidat wird stillschweigend als neuer Stand ausgegeben.

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

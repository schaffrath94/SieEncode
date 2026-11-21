# Erste stabile SieEncode-Veröffentlichung: Oberfläche vor Runtime-Migration

Stand: 21.11.2026. Arbeitsgrundlage für den bestehenden Entwicklungszweig, **keine Releasefreigabe**.

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
- **UX-02:** Projektbibliothek und Editor nach dem tatsächlichen nächsten Schritt führen, ohne Handler/Autosaves/Legacy-Importe zu entfernen.
- **UX-03 (aktiv):** Team-/Vault-Einstiege und positive Delegations-/Nachfolgedialoge verbessern und mit echten Rollen testen. Persönliche Identität ist inzwischen als eigenständiger Zweischritt-Dialog von der bisherigen Teamlistenpflege getrennt; Delegation und Hauptverwaltung wurden nach Aufgabe gegliedert, Status und Fehlermeldungen verbessert.
- **UX-04:** Kodierstation und Untersuchung auf eine Hauptaktion pro Status verdichten, inklusive Ergebnisbericht und eindeutiger Freigabe.
- **REL-01:** Bestehende Testnachweise mit UI-/MSI-/Hardware-Restpunkten zusammenführen, reale Lücken schließen und den ersten Releasekandidaten abnehmen.
- **ENGINE-01 (geparkt):** Offizielle Bridge hinter optionalem Adapter statt Ersatz der bestehenden Runtime; erst nach der ersten stabilen Veröffentlichung.

## Änderung des ersten UX-Schrittes

`TestMaintenanceActions` wird in `ApplyOperatingMode` bei Standardansicht ausgeblendet und erscheint im Expertenmodus wieder. Die Prüfungen `VerifyStationLayout` und `VerifyOperatingModeContract` berücksichtigen die Trennung. In der Projektbibliothek heißt die eindeutige Übergabeaktion nun **„Zur Kodierstation“** statt des doppeldeutigen „Projekt wählen“; Handler und Automation-ID bleiben gleich. **Diese Änderungen sind committed, aber mangels Windows-WPF-Laufzeit hier noch nicht praktisch ausgeführt oder visuell abgenommen.** Keine sonstigen Kartenaktionen verändert.

# Einfacher Vault- und Offline-Gruppentest

**Ziel:** Die erste SieEncode-Version soll sich von einem Anwender mit zwei Test-Windows-Benutzern ohne Entwickleranleitung bedienen lassen. Stand 21.11.2026, Anleitung für den Entwicklungsstand. **Keine Abnahme ohne echte Ausführung.**

Dieser Kurztest nutzt die vorhandene Testbibliothek und **erfundene Testidentitäten**. Er schreibt keine Karten. Keine Schlüssel, Passphrasen, privaten Sicherungen oder Kundendaten in Rückmeldungen aufnehmen.

## Vorbereitung (einmalig)

- Auf zwei **verschiedenen Windows-Benutzern** arbeiten: **Verwaltung** (hat den privaten Hauptverwaltungsschlüssel) und **Kollege** (bekommt nur den eigenen Gruppen-Zugang). Die tatsächlichen Benutzerrollen prüfen, nicht allein nach Anmeldenamen urteilen.
- Beide nutzen die passende registrierte, ausdrücklich dafür vorgesehene Testbibliothek. Einen bereits vorhandenen persönlichen Vault-Zugang **weiterverwenden**, nicht für jeden Test neu anlegen.
- Falls noch kein eigener Zugang vorhanden: **Einstellungen → 1 · Mein persönlicher Vault-Zugang → Persönlichen Zugang einrichten**. Einmalig eine Identität anlegen. Eine öffentliche Einladung nur für Teamzugriff bzw. auf Anforderung exportieren und an die Hauptverwaltung geben. **Keine alte Teamliste speichern.**
- Den persönlichen Fingerabdruck **über einen zweiten Weg** mit der verantwortlichen Person abgleichen. Für die signierte Gruppenregistrierung auch den Verwaltungsfingerabdruck unabhängig bestätigen. Diese Sicherheitsprüfung niemals durch Kopieren desselben Werts aus der importierten Datei ersetzen.
- Nur synthetische Gruppen mit eindeutigen Namen verwenden, z. B. **Test-Lokal** und **Test-Andere**. Privat gesicherte Schlüssel und alte Vaultkopien bleiben geschützt auf dem jeweiligen Rechner.

## Test 1 – Mein eigener Zugang (Kollege)

1. **Einstellungen → 1 · Mein persönlicher Vault-Zugang** öffnen. Erwartung: Nur **Zugang auswählen/anlegen**, **Öffentliche Einladung exportieren** und **Schließen**; *keine* Teamlisten-Speicheraktion.
2. Bei genau einer lokalen Identität ist diese vorausgewählt. Bei mehreren ausdrücklich anhand des Fingerabdrucks die richtige auswählen; SieEncode darf keinen zufälligen ersten Schlüssel benutzen. Anschließend schließen. Erwartung: Keine neue Identität und keine Änderung der bisherigen Teamliste.
3. Nur wenn der Verwalter die Einladung noch nicht hat: „Öffentliche Einladung exportieren“ und übergeben. Erwartung: Datei enthält nur öffentliche Informationen; die eigentliche Gruppenberechtigung ist **noch nicht** erteilt. Ohne Teamprojekt kann man den Dialog nach Schritt 2 einfach schließen.

**Rückmeldung:** „Zugang OK“ oder Screenshot/Fehlermeldung – ohne Fingerabdruck oder Geheimwert.

## Test 2 – Gruppe freigeben und Änderung übernehmen

1. **Verwaltung:** Einstellungen → **2 · Teamzugriff und Offline-Gruppen → Ich verwalte Personen und Gruppen → Personen und Gruppen verwalten** öffnen. Gibt es nur eine lokal verwaltete Bibliothek, ist sie vorausgewählt und wird **ohne Änderung der lokalen Registrierung** geladen. Bei mehreren Bibliotheken die richtige ausdrücklich auswählen.
2. Die Einladung des Kollegen anhand der angezeigten Person und des **unabhängig** bestätigten Fingerabdrucks übernehmen; nur die Testgruppe **Test-Lokal** freigeben. **Änderungen signiert speichern**. Danach **Aktuellen Gruppenstand für Kollegen exportieren**.
3. **Kollege:** Unter **2 · Teamzugriff und Offline-Gruppen** bei erster Nutzung **Ersten Gruppenstand registrieren**, danach **Aktualisierten Gruppenstand importieren**. Öffentlich exportierte Registrierung öffnen, Vorschau und Fingerabdruck prüfen und den bestätigten Stand übernehmen. Die normale Oberfläche zeigt dafür jeweils nur den passenden Importschritt.
4. **Kollege:** Einstellungen → **2 · Teamzugriff und Offline-Gruppen → Ich bearbeite Gruppen mit einer Vollmacht → Meinen Gruppenverwaltungsplatz öffnen** öffnen, falls eine Verwaltungsvollmacht getestet wird. Eine einzige Bibliothek wird automatisch geladen. Existiert auf diesem Windows-Benutzer genau ein lokaler Verwaltungsschlüssel für die Bibliothek, wird dessen Person automatisch geladen – auch bei mehreren registrierten Personen. Ohne eindeutigen lokalen Schlüssel die eigene Person ausdrücklich auswählen. **Keine Schlüssel- oder Berechtigungserstellung beim Öffnen.**
5. Nur wenn eine **separat erteilte aktive Verwaltungsvollmacht** besteht, darf **Änderung signieren und exportieren** aktiviert sein. Einfache Gruppenmitgliedschaft allein reicht ausdrücklich nicht.

**Erwartung:** Der Kollege sieht nur die freigegebene Gruppe. Eine andere Testgruppe bleibt gesperrt. Ohne Vollmacht kann er keine signierte Gruppenänderung abgeben. Das signierte Speichern beim Verwalter macht den Stand **nicht** automatisch auf allen Offline-Rechnern aktuell.

**Rückmeldung:** „Freigabe OK“ / „Vollmacht OK oder nicht vorhanden“ / konkreter Fehler, ohne interne IDs abzuschreiben. Der sichtbare Revisionsstand kann für die spätere technische Analyse aus einem datensparsamen Supportnachweis abgelesen werden; keine Geheimwerte übertragen.

## Test 3 – Entzug (nur gesonderte Testgruppe)

1. **Verwaltung:** Den Testkollegen aus **Test-Lokal** entfernen, ausdrücklich **Änderungen signiert speichern** und neuen Stand exportieren.
2. Falls das betreffende Projekt einen bestehenden Gruppen-Vault hat: dessen **Gruppenempfänger explizit abgleichen**. Eine bloße Gruppenänderung entfernt noch keine alten verschlüsselten Empfänger aus vorhandenen Vaultkopien.
3. **Kollege:** Neuen Stand über **Registrierte Gruppen aktualisieren** übernehmen; den aktuellen Test-Vault erneut prüfen. Erwartung: Der entzogene Zugang ist abgewiesen. Eine absichtlich aufbewahrte **alte Offline-Kopie kann weiterhin zugänglich sein** – das ist eine dokumentierte Grenze, keine Fernsperre.
4. Nur falls im Testplan vorgesehen, Freigabe wiederherstellen und nach erneutem signiertem Export/Import prüfen.

**Rückmeldung:** „Entzug OK“ oder genaue Fehlermeldung. Keine Kunden- oder Produktivkarten.

## Was für den ersten Release zusätzlich offen bleibt

Das sind **kurze Bediennachweise**, kein Ersatz für die gesonderten Prüfungen von Recovery, Verwaltungsnachfolge, paralleler Bearbeitung, zweiten Rechnern, echten Karten/Readback, MSI-Upgrade und Datenerhalt. Diese Fälle stehen in `docs/MANUELLE-RESTABNAHME-1.3.0.md` und `docs/design/RELEASE-FIRST-UX-2026-11-21.md`. Bereits nachgewiesene Software-/API-Fälle nicht künstlich wiederholen.

**Status dieser Anleitung:** Nur eine spätere **gebündelte Release-Abnahme** führt diese Schritte tatsächlich aus. Codeänderungen sind im Entwicklungszweig vorhanden, aber noch nicht als aktualisierte MSI installiert oder unter WPF manuell abgenommen. Kandidaten 5–7 besitzen die neue Oberfläche nicht.

# Einfacher Vault- und Offline-Gruppentest

**Ziel:** Die erste SieEncode-Version soll sich von einem Anwender mit zwei Test-Windows-Benutzern ohne Entwickleranleitung bedienen lassen. Stand 21.11.2026, Anleitung für den Entwicklungsstand. **Keine Abnahme ohne echte Ausführung.**

Dieser Kurztest nutzt die vorhandene Testbibliothek und **erfundene Testidentitäten**. Er schreibt keine Karten. Keine Schlüssel, Passphrasen, privaten Sicherungen oder Kundendaten in Rückmeldungen aufnehmen.

## Vorbereitung (einmalig)

- Auf zwei **verschiedenen Windows-Benutzern** arbeiten: **Verwaltung** (hat den privaten Hauptverwaltungsschlüssel) und **Kollege** (bekommt nur den eigenen Gruppen-Zugang). Die tatsächlichen Benutzerrollen prüfen, nicht allein nach Anmeldenamen urteilen.
- Beide nutzen die passende registrierte, ausdrücklich dafür vorgesehene Testbibliothek. Einen bereits vorhandenen persönlichen Vault-Zugang **weiterverwenden**, nicht für jeden Test neu anlegen.
- Falls noch kein eigener Zugang vorhanden: **Einstellungen → Meinen Vault-Zugang einrichten**. Eigene Identität anlegen, öffentliche Einladung exportieren, Datei an die Verwaltung übergeben. **Keine alte Teamliste speichern.**
- Den persönlichen Fingerabdruck **über einen zweiten Weg** mit der verantwortlichen Person abgleichen. Für die signierte Gruppenregistrierung auch den Verwaltungsfingerabdruck unabhängig bestätigen. Diese Sicherheitsprüfung niemals durch Kopieren desselben Werts aus der importierten Datei ersetzen.
- Nur synthetische Gruppen mit eindeutigen Namen verwenden, z. B. **Test-Lokal** und **Test-Andere**. Privat gesicherte Schlüssel und alte Vaultkopien bleiben geschützt auf dem jeweiligen Rechner.

## Test 1 – Mein eigener Zugang (Kollege)

1. **Einstellungen → Meinen Vault-Zugang einrichten** öffnen. Erwartung: Nur **Zugang auswählen/anlegen**, **Öffentliche Einladung exportieren** und **Schließen**; *keine* Teamlisten-Speicheraktion.
2. Bestehenden Zugang auswählen und den Dialog schließen. Erwartung: Keine neue Identität und keine Änderung der bisherigen Teamliste.
3. Wenn noch keine gültige Einladung vorliegt, einmalig exportieren und dem Verwalter geben. Erwartung: Datei enthält nur öffentliche Informationen; die eigentliche Gruppenberechtigung ist **noch nicht** erteilt.

**Rückmeldung:** „Zugang OK“ oder Screenshot/Fehlermeldung – ohne Fingerabdruck oder Geheimwert.

## Test 2 – Gruppe freigeben und Änderung übernehmen

1. **Verwaltung:** Einstellungen → **Offline-Gruppen verwalten** öffnen. Gibt es nur eine lokal verwaltete Bibliothek, ist sie vorausgewählt und wird **ohne Änderung der lokalen Registrierung** geladen. Bei mehreren Bibliotheken die richtige ausdrücklich auswählen.
2. Die Einladung des Kollegen anhand der angezeigten Person und des **unabhängig** bestätigten Fingerabdrucks übernehmen; nur die Testgruppe **Test-Lokal** freigeben. **Änderungen signiert speichern**. Danach **Aktuellen Gruppenstand für Kollegen exportieren**.
3. **Kollege:** Einstellungen → **Registrierte Gruppen aktualisieren**. Öffentlich exportierte Registrierung öffnen, Vorschau und Fingerabdruck prüfen und den bestätigten Stand übernehmen.
4. **Kollege:** Einstellungen → **Mein Gruppenverwaltungsplatz** öffnen, falls eine Verwaltungsvollmacht getestet wird. Eine einzige Bibliothek wird automatisch geladen. Existiert auf diesem Windows-Benutzer genau ein lokaler Verwaltungsschlüssel für die Bibliothek, wird dessen Person automatisch geladen – auch bei mehreren registrierten Personen. Ohne eindeutigen lokalen Schlüssel die eigene Person ausdrücklich auswählen. **Keine Schlüssel- oder Berechtigungserstellung beim Öffnen.**
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

Das sind **kurze Bediennachweise**, kein Ersatz für die gesonderten Prüfungen von Recovery, Verwaltungsnachfolge, paralleler Bearbeitung, zweiten Rechnern, echten Karten/Readback, MSI-Upgrade und Datenerhalt. Diese Fälle stehen in `docs/MANUELLE-RESTABNAHME-1.3.0.md` und `docs/RELEASE-FIRST-UX-2026-11-21.md` (letzteres unter `docs/design`). Bereits nachgewiesene Software-/API-Fälle nicht künstlich wiederholen.

**Status dieser Anleitung:** Codeänderungen im Entwicklungszweig vorhanden, aber noch nicht als aktualisierte MSI installiert, unter WPF gebaut oder manuell abgenommen. Die bisherigen Kandidaten 5–7 besitzen diese vereinfachte Oberfläche noch nicht. Erst ein neuer, identifizierter Testkandidat darf dafür verwendet werden.

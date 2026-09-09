# Release-Stand am 09.09.2026

Entwicklungsstand, keine Produktivfreigabe. Ziel ist zunächst ein internes Release.
RC15 bleibt Referenz; neue Änderungen sind auf privaten Entwicklungsbranches.

## Funktionserhalt der bisherigen UI

`tools/inventory_rc15.py --check` heute bestanden: 146 erfasste UI-Handler,
26 CLI-Befehle und 6 Startparameter ohne gemeldete fehlende Einstiegspunkte.
Die Baseline inventarisiert außerdem 286 XAML-Flächen und 12 dynamische Bindungen.
Der Checker verifiziert deren Baseline-Inventar, aber nicht jede heutige Bindung
oder jedes Laufzeitverhalten. Die Zahlen sind keine Anzahl unabhängiger Funktionen.

Die drei Arbeitsbereiche verwenden die bestehenden Seiten, Editoren und Abläufe.
Der bisherige WPF-Smoke deckt 11 Seiten, Navigation, Moduswechsel, ungespeicherte
Editorwerte, Projekt-/Altprojektabläufe und synthetische Kodiermodelle ab.
Kein bekannter absichtlich entfernter RC15-Funktionsumfang. Eine vollständige
praktische Gleichwertigkeit aller P/V/K/A/S-Zeilen aus FUNKTIONSPARITAET.md ist
noch nicht abgenommen. Anwesenheit von Handlern ersetzt diesen Nachweis nicht.

## Vor einem freigegebenen internen Release offen

1. Softwareumfang abschließen: Expertenansichten gegen die Funktionsmatrix prüfen;
   historische Vault-Ersteinrichtungsänderungen gegen den kanonischen Stand abgleichen.
   Berichtsauswahl auch bei Soll-/Ist-Export anbieten, alten Sollrenderer ohne
   Daten-/Textkürzung paginieren, optionale Notizen/Bilder/Datum/Ort/Unterschrift
   umsetzen. Aktuelle Ist-/Vergleichsberichte betreffen einzelne Applications.
2. Vollständige UI-Abnahme: jede Funktion zuordnen und belegen; tatsächliche
   Dateidialogketten, Tastatur, 100/125/150 % Skalierung, Fehler-/Abbruch-/Recoveryfälle,
   Projekt-/Profil-/Vault-Kompatibilität und installierten Gesamtbedienlauf prüfen.
3. Vault-Abnahme: zweiter Benutzer/Rechner, Teamzugriff und Wiederherstellung.
   Verhalten bei Mitgliederentfernung/Schlüsselrotation festlegen und dokumentieren;
   alte Vaultkopien werden durch reine Empfängerentfernung nicht rückwirkend gesperrt.
4. Gemeinsamen Studio-/Runtime-Kandidaten bauen, Softwaretests ausführen, Installer,
   Upgrade/Start und synthetische Beispiele prüfen; Hashes und Versionsidentität sichern.
   UI-Previews ohne Runtime sind dafür kein Ersatz. Private Branches erst nach Review
   integrieren und Release-Notizen, Lizenz-/Drittanbieterhinweise sowie Artefakte prüfen.
5. Reader-/Transponderabnahme am exakten Kandidaten: insbesondere ChangeFileSettings,
   Keywechsel, Kodier-/Teststation, Serienlauf und Fehlerfälle. Nach Änderungen erneut
   passende Nachweise zuordnen; alte RC15-Freigabe gilt nicht automatisch für Neubauten.

Code-Signing ist nach docs/RELEASE-SIGNING.md für externe Produktionsverteilung
Pflicht; für rein interne Nutzung kann die fehlende Signatur ausdrücklich als Grenze
dokumentiert werden. Ein externer Pilot folgt der internen Abnahme.

## Späterer Ausbau, kein RC15-Funktionsverlust

Vollständige Chip-/Produktions-/Keyversionsdaten, Value-/Record-Inhalte, weitere
Tiefenanalyse, zentrale Benutzerverwaltung/Sperrung/Auditierung und umfassendere
Host-Schnittstelle sind separate Erweiterungen. Ihre fehlende Fertigstellung ist
nicht gleichbedeutend mit dem Verlust einer RC15-Funktion.

Automatisierbar: Implementierung, synthetische Tests, Quellabgleich, PDF-Prüfung,
Builds, Prüfsummen und private GitHub-Sicherung. Reale Hardware-/Zweitnutzer-Nachweise
erfordern die entsprechende Umgebung; sie dürfen nicht durch Softwaretests ersetzt werden.

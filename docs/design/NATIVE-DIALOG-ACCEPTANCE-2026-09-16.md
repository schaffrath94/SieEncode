# Native Dateidialoge: praktische Teilabnahme 16.09.2026

Geprueft mit Computer Use an der gestarteten Windows-Anwendung, nicht nur an
Handlern oder synthetisch ersetzten Dialogen. Build
`1.1.0-releasecheck.20260915.3`, Source `f8df5c9`, Start `--ui-test-workspace`.
Artefakt: `artifacts/native-dialog-check-20260915/Studio/SieEncode.exe`.
Der kombinierte Releasekandidat hat denselben Source, eine andere Versionskennung;
dieser Lauf ist keine erneute Installation oder Byteidentitaetspruefung.

Isolierte Daten:
`tmp/native-dialog-check-20260916/SieEncode/startup-test-d5f941719ff2412b85e1369490ed372a`.
Nur synthetisches Profil, keine produktiven Vaults, Zertifikate oder Karten.

## Bestandene Bedienfolgen

- Neues Projekt aus der Uebersicht; Expertenansicht; weitere Editoraktionen;
  Speichern unter: nativer Dialog mit Projektfilter und isoliertem Projektordner.
  Speichern bestaetigt; `projects/Neues Projekt.project.json` eingelesen und
  Transport-/Produktivschluesselfelder leer. Synthetische Werte inkl. Umlauten
  korrekt erhalten.
- Speichern unter nochmals geoeffnet und abgebrochen. Datei zuvor/danach SHA256
  `3857CE4D59966DD7223E39D650B9F3A4028606AE8FB6200BB16BC571E3131EBF`.
- Profile und Historie: gebuendeltes synthetisches Profil ausgewaehlt;
  Kodiervorschrift exportieren; nativen Speicherdialog bestaetigt;
  Erfolgsmeldung gesehen, anschliessendes Oeffnen verneint. Exportdatei
  `profiles/customer/SYNTHETIC-MODULAR-2.0_Kodiervorschrift.txt` vorhanden;
  Profilidentitaet, Variablen und Application im Text nachgeprueft.
- Aus JSON importieren: synthetische Fixture-Kopie mit eigener ID
  `SYNTHETIC-NATIVE-IMPORT-20260916` im nativen Oeffnen-Dialog ausgewaehlt.
  Kontrollierte Importbestaetigung zeigte ID, Version, Applicationanzahl und
  Fingerprint `0C6522A00A88`. Bestaetigt; zweite Profilzeile als verwaltetes
  Grundprofil sichtbar; `profiles/base/SYNTHETIC-NATIVE-IMPORT-20260916.json`
  vorhanden. Bestehende Projektdatei bleibt beim urspruenglichen Profil.
- Anwendung nach Test beendet. Nachfrage zu ungespeichertem synthetischen
  Editorzustand nach Profilimport bestaetigt. Prozess beendet, stderr leer.

## Gefundene Textkorrektur

Projektspeichern meldete pauschal, Schluessel seien im Vault abgelegt, obwohl
kein Vault angelegt war. `SaveEditorProject` schreibt ausschliesslich die
schluesselwertfreie Projektdefinition. Meldung korrigiert: Projekt gespeichert,
keine Schluesselwerte enthalten, Schluesselaenderungen separat im Vault speichern.
Keine Aenderung am Speichern, an Verschluesselung oder Dialogverhalten.
Der bereits gebaute Kandidat vom 15.09. enthaelt diese Textkorrektur noch nicht.

Validierung nach Textkorrektur: Test-CI mit 62/62 Regressionstests und CLI-Build
erfolgreich; WPF-Teil zunaechst durch Sandboxzugriff auf Microsoft SDKs blockiert.
Gezielter WPF-Build ausserhalb der Sandbox danach Exit 0, keine Warnungen/Fehler.
Logs: `tmp/native-dialog-status-ci.log`, `tmp/native-dialog-status-wpf-build.log`.
Keine neuen Tests fuer die reine Textaenderung; keine erneute Paketierung.

## Grenzen

Keine vollstaendige Abnahme aller nativen Dialogvarianten oder aller alten
UI-Funktionen. PDF bleibt zurueckgestellt. Installation/Upgrade/Deinstallation
in isoliertem Windows, echter zweiter Benutzer und Reader/Testkarte inklusive
ChangeFileSettings bleiben offene Freigaben. Bestehende automatische Nachweise
stehen in RELEASE-CANDIDATE-2026-09-15.md.

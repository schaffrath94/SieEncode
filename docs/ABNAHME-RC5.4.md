# Abnahme SieEncode 1.0.0-rc5.4

Stand: 26.08.2026

## Ergebnis

- MSI-Revision 1.0.12 erfolgreich als Major Upgrade installiert.
- Installierte Produktversion: `1.0.0-rc5`.
- Selbsttest und vollständiger UI-Smoke-Test: Exitcode 0.
- UI-Vertrag: 186 eindeutig adressierbare interaktive Elemente, 103 Buttons,
  9 Auswahllisten, 10 Seiten und alle relevanten Nebenfenster.
- Automatisierte Kern-, Sicherheits-, Kompatibilitäts- und Regressionstests:
  40/40 erfolgreich, 0 Buildwarnungen, 0 Buildfehler.
- Reale Projekt-Preflights für bearbeitbare und reine Artefaktprojekte: OK.
- Vollständiger Hardwarezyklus mit Format, Mastering, Kodierung,
  Produktivverifikation, negativer Gegenprobe, Recovery und erneutem Mastering:
  erfolgreich.
- Abschließender Test der installierten RC5.4 über ELATEC: 6/6 ReadData-Tasks,
  0 Fehler, 0 Skips, `TRANSPORT_MASTERING_OK`.
- Der Testtransponder bleibt im gemasterten Transportzustand.

## Während der Abnahme behobene Fehler

- Ein `TaskLoop.Success` mit null oder unvollständig ausgeführten Tasks wird
  nicht mehr als Erfolg akzeptiert.
- Die mit RFiDGear 2.0.10 kompatibilisierte Anwendungsprüfung wird vom
  Builder-Laufzeitvertrag korrekt akzeptiert.
- Readback-Dateien gelten nur bei terminal erfolgreicher zugehöriger Probe als
  Lesbarkeitsnachweis. Alte Dateien fehlgeschlagener Läufe können keinen
  Mischzustand oder falschen Erfolg vortäuschen.
- Der exakt freigegebene gebündelte Runtime-Build ist per Produktversion und
  Binärfingerprint freigeschaltet; abweichende Builds bleiben gesperrt.

## Bewusst verbleibende Grenzen

- Filetyp, Dateigröße, Communication Mode und sämtliche Access Rights können
  mit der gebündelten RFiDGear-2.0.10-Runtime noch nicht bitgenau von der Karte
  gelesen werden. SieEncode kennzeichnet diese Werte ausdrücklich als nicht
  geprüft; RFiDGear PR #206 bleibt die Upstream-Lösung.
- Die native Erweiterung im RFiDGear-Hauptfenster und ein stabiler Extension-
  Host bleiben Upstream-Themen #190/#191. RC5.4 arbeitet bis dahin über den
  belegten Projektdatei-/Startparameter-/Logvertrag.
- Ein realer Mehrkarten-Serienlauf mit fünf verschiedenen physischen
  Transpondern ist nicht durch einen einzelnen freigegebenen Testtransponder
  ersetzbar. Serienwarteschlange, Wiederholung, Überspringen, Historie und
  Duplikatschutz sind automatisiert getestet.

## Releaseartefakte

- `SieEncode_1.0.0-rc5.4_x64.msi`
- `SieEncode_1.0.0-rc5.4_portable_x64.zip`
- Prüfsummen: `artifacts/package/SHA256SUMS.json`

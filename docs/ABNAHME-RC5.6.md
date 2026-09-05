# Abnahme SieEncode 1.0.0-rc5.6

Stand: 26.08.2026

## Ergebnis

- MSI-Revision 1.0.14 erfolgreich als Major Upgrade installiert.
- Installierte Produktversion: `1.0.0-rc5.6`.
- Automatisierte Kern-, Sicherheits-, Kompatibilitäts- und Regressionstests:
  40/40 erfolgreich, 0 Buildwarnungen, 0 Buildfehler.
- Vollständiger UI-Smoke-Test einschließlich aller zehn Seiten, Nebenfenster,
  dynamischen Projekt-, Key- und Kartendatenfelder: erfolgreich.
- Reales Projekt ausschließlich über die sichtbare installierte Oberfläche
  erstellt, kryptografische Sitzungsschlüssel erzeugt, gespeichert, analysiert,
  gebaut und geprüft.
- Dry-Run: 3 Applications, 27 Mastering-Tasks, 26 Kodier-Tasks und keine
  Schlüsselwerte im Bericht.
- Build: 7 rfPrj-Projekte; Projekt-Preflight ohne Befund.
- ELATEC-Transportprüfung: 6/6 ReadData-Tasks, `TaskLoop.Success`, korrelierte
  RunId, `TRANSPORT_MASTERING_OK`.
- Reale Kodierung: 26/26 Taskresultate, `TaskLoop.Success`, korrelierte RunId;
  finale Readbacks erfolgreich.
- Recovery vom produktiven PICC-Key: 2/2 Taskresultate, erfolgreich.
- Erneutes Mastering: 27/27 Taskresultate, terminal erfolgreich; erwartete
  negative AppExistChecks werden in der Detailtabelle als `IsNotTrue` sichtbar,
  die nachfolgenden Erzeugungsschritte laufen erfolgreich.
- Abschließende Transportprüfung: 6/6 ReadData-Tasks,
  `TRANSPORT_MASTERING_OK`.
- Kodiersitzung sichtbar abgeschlossen: 1 erfolgreich, 0 Fehler,
  0 übersprungen. Diagnosepaket mit 20 erlaubten, schlüsselfreien Einträgen
  erzeugt.
- SieEncode- und Runtime-Prozesse wurden anschließend kontrolliert beendet.
  Der Testtransponder bleibt im vollständig gemasterten Transportzustand.

## Detaillierte Laufauswertung

SieEncode wertet und zeigt für Kartenaktionen und den normalen Kodierlauf
dieselbe strukturierte Evidenz an:

- erwartete und tatsächlich korrelierte Taskanzahl,
- Taskposition, fachlicher Index und Tasktyp,
- Ausführungsentscheidung und Skip-Grund,
- Ergebnis und Fehlercode,
- Zeitstempel,
- terminales TaskLoop-Ereignis,
- RunId-Korrelation und gekürzten RunId-Fingerprint.

Ein terminaler Erfolg mit null, unvollständigen, fremden oder nicht
korrelierbaren Taskresultaten wird weiterhin fail-closed abgelehnt. Rohschlüssel
werden weder in dieser Ansicht noch im Diagnosepaket ausgegeben.

## Bewusst verbleibende Grenzen

- Filetyp, Dateigröße, Communication Mode und sämtliche Access Rights können
  mit der gebündelten RFiDGear-2.0.10-Runtime noch nicht bitgenau von der Karte
  gelesen werden. SieEncode kennzeichnet diese Werte ausdrücklich als nicht
  geprüft; RFiDGear PR #206 bleibt die Upstream-Lösung.
- Die native Erweiterung im RFiDGear-Hauptfenster und ein stabiler Extension-
  Host bleiben Upstream-Themen #190/#191. RC5.6 arbeitet bis dahin über den
  belegten Projektdatei-, Startparameter-, Host-V1.1- und Logvertrag.
- Ein realer Mehrkarten-Serienlauf mit fünf verschiedenen physischen
  Transpondern ist nicht durch einen einzelnen freigegebenen Testtransponder
  ersetzbar. Warteschlange, Wiederholung, Überspringen, Historie und
  Duplikatschutz sind automatisiert getestet.

## Releaseartefakte

- `SieEncode_1.0.0-rc5.6_x64.msi`
  - SHA-256: `9F61EB86ED05E0BD4E21F4CDE92BDF20F49CF20A5591EC37A3BD22E9CB91078A`
- `SieEncode_1.0.0-rc5.6_portable_x64.zip`
  - SHA-256: `957DC2E5554182B969D0337566D03A4028B09784009A4DDF59218F4B1FFD210C`
- Maschinenlesbare Prüfsummen: `artifacts/package/SHA256SUMS.json`

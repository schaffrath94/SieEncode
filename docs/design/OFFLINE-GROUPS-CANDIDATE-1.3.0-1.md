# Offline-Gruppen: Kandidat 1.3.0-offline-groups.1

Stand 23.10.2026: lokal gebauter interner Testkandidat, keine abgeschlossene
Releasefreigabe und kein GitHub-Release. Vorhandener Release 1.2.1 unverändert.

- Studio-Quelle: `2be1be0857c1933e5bc91c95d3296f84052ce6b7`, sauberer Arbeitsbaum beim Build.
- MSI-Produktversion: `1.3.0`; kein portables Paket erzeugt.
- Verzeichnis: `artifacts/offline-groups-1.3.0-preview.1/package`.
- MSI: `SieEncode_1.3.0-offline-groups.1_internal_x64.msi`.
- MSI SHA-256: `268A850AD71354E702CB850831E226C51D88226FB00FAF05A47AC8BAB8B1DD4D`.
- Studio-DLL SHA-256: `81F79933C67CC007B63A0A8C14F4E7AB939FBA0EDF369C4BADE07D807EBB0F38`.
- Generisches Abnahmekit SHA-256: `B62C9B1FA1C57128AA31A9B0860325ABE6D7E391D8609E58C7A9AAA68A6945A5`.

Die Runtime wurde aus dem bestehenden 1.2.1-Paket bytegleich übernommen.
Ihr BUILD-IDENTITY nennt `c959bd4a930833af094b696100a71e248a1dc7c4` und
`sourceHadUncommittedChanges: true`; das ist kein Nachweis eines sauberen neuen
Runtime-Rebuilds. EXE/DLL stimmen mit diesem Identitätsnachweis überein, alle
kopierten Runtime-Dateien mit dem bisherigen Paket. Keine neue Hardwareabnahme.

## Ausgeführte Prüfungen

- Vorheriger Produktstand `a326cff`: CI 72/72, Builds ohne Warnungen/Fehler.
  Danach ausschließlich die Abnahmeanleitung hinzugefügt.
- Build-Release erfolgreich; MSI-Version und Startmenüvertrag geprüft.
- Genau die publizierte Kandidatenanwendung: UI-Smoke Exit 0, Version
  `1.3.0-offline-groups.1`, status OK, 60 Checks, stderr leer.
- MSI administrativ extrahiert, Exit 0. Alle 1.034 Dateien des gebauten
  Releasebaums stimmen mit den enthaltenen MSI-Dateien überein.
- Beide Einträge in SHA256SUMS.json nachgeprüft.
- Bisherige MSI 1.2.1, Studio-DLL und Runtime-EXE/DLL gegen vorab erfasste
  Prüfsummen unverändert. Keine tatsächliche Installation ausgeführt.

Lokale Nachweise: `CANDIDATE-VERIFICATION.json` im Kandidatenverzeichnis;
`tmp/offline-groups-candidate-package.log`,
`tmp/offline-groups-candidate-msi-admin.log`,
`tmp/offline-groups-candidate-smoke/SieEncode/ui-smoke/ui-smoke-result.json`.
Bekannte Installerwarnungen ICE40/ICE61; interner Kandidat ohne Code-Signatur.

## Ausstehende Abnahme

Die [Arbeitsplatz-Abnahmeanleitung](../OFFLINE-GRUPPEN-ABNAHME.md) ist im
Installer unter Studio/Docs enthalten. Sie beschreibt Registrierung, Gruppen-
und Offlinezugriff, Entzug/Abgleich, begrenzte Delegation, separate Recovery
und Verwaltungsnachfolge einschließlich Neustart und Artefaktidentität.

Tatsächliche Installation/Upgrade, native Gesamtbedienung, reale zweite
Windows-Umgebung und tatsächliche Windows-Recoveryinstallation sind offen.
Der Installer verwendet den regulären Upgradepfad und kann vorhandenes
SieEncode ersetzen: für diese Abnahme einen separaten Testrechner/eine Test-VM
verwenden. Extraktion und synthetische Tests ersetzen diese Nachweise nicht.

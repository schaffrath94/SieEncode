# Release-Pruefstand 2026-09-14

PDF-Weiterentwicklung und visuelle Abnahme sind vom Benutzer zurueckgestellt.
Der folgende Stand ist ein interner Testkandidat, keine abschliessende Freigabe.

## Software und Paket
- Studio: codex/workspace-ui, Quellstand 7f7c46e plus Build-Skriptkorrekturen;
  61/61 Regressionstests; CLI und WPF erfolgreich gebaut.
- Runtime: c959bd4a930833af094b696100a71e248a1dc7c4; 193/193 Tests,
  0 ausgelassen. Vorbestehende Arbeitskopieaenderungen sind im BUILD-IDENTITY
  als sourceHadUncommittedChanges=true sichtbar; kein sauberer Tag-Build behauptet.
- Runtime-DLL SHA256:
  FFF3C419DE115BFDA2EF3AE31C971D66441E79F6B39DEB6CED55055D1B7C7AA7.
- SDK 8.0.425, Paketversion 1.1.0-releasecheck.20260914, MSI 1.1.15.
- Ausgabe: artifacts/releasecheck-20260914-fixed/package.
- Build Exit 0; MSI-Startmenuevertrag verifiziert.
- MSI administrative Extraktion Exit 0. Alle 1024 Builddateien hashgleich.
- Portable: alle 1024 Eintraege hashgleich und Dateianzahl gleich Build.
- Alle drei Groessen/SHA256 mit SHA256SUMS.json abgeglichen; VERSION passt.
- Unsigierter interner Testkandidat. Keine produktive Installation vorgenommen.

| Paket | Bytes | SHA256 |
| --- | ---: | --- |
| MSI | 118563356 | C9DDE9677AA0D4E20F0F03EE6DD14077E398EB15BE8973D383DC13442BB2DE7F |
| Portable ZIP | 164977345 | 131A807E01A3CF0AE3C1E8E875D12EBE50A9255D15A5346779C5C69CDD4F290E |
| Abnahme-Kit ZIP | 31516 | 2223C2E4B7CD58862F9410E48C69464F5ECC2F2EF74BC1D2068655A9A602EF81 |

## Beim Test behoben
Build-Release.ps1 akzeptiert nun einen separaten Ausgabeunterordner unter artifacts.
Die paketierte VERSION verwendet PackageVersion. Die Versionskontrolle versteht
auch andere Vorabversionssuffixe als rc; stabil/rc/releasecheck separat geprueft.
Paketpruefsummen entstehen im Abnahme-Kit-Staging statt im Quellordner.
Der erste Kandidat scheiterte am Versionscheck; nur der fixed-Kandidat ist komplett.

## Weiter offene Freigaben
- Produktionspaket mit explizit isolierten Testdaten starten und vollstaendige
  Import-/Speicher-/Export- und Vault-Ablaeufe pruefen. Preview-Smoke ersetzt dies nicht.
- Installation, Upgrade und Deinstallation in isolierter Windows-Umgebung.
  WiX ICE40 (REINSTALLMODE) und ICE61 (gleichversionsbezogenes Upgrade) bleiben
  Hinweise fuer diese praktische Pruefung; administrative Extraktion deckt sie nicht ab.
- Mehrbenutzer-Vault und Wiederherstellung unter zweitem Benutzer/Rechner.
- ChangeFileSettings und kompletter Bedienlauf mit geeignetem Reader/Testtransponder.
  Neue Runtime-Hashes erben keine alte Hardwarefreigabe; Schutz bleibt unveraendert.
- Vollstaendige praktische RC15-UI-Paritaet, DPI und native Dateidialogablaeufe.
  Bestehende Quellvertragstests beweisen keine vollstaendige Bedienparitaet.

Lokale Logs: tmp/release-readiness-ci.log, tmp/release-readiness-runtime.log,
tmp/release-readiness-package-fixed.log, tmp/release-readiness-msi-admin-fixed.log.
Lokales Pruefergebnis: artifacts/releasecheck-20260914-fixed/package-verification.json.
Naechste Arbeit und Autorisierung: AUTONOMOUS-PROGRESS.md.

## Nachtrag: isolierter Produktionsstart
Regulaerer Publish 1.1.0-releasecheck.20260914.2 ohne Preview-Flag unter
artifacts/release-startcheck-20260914/Studio. Studio-DLL SHA256:
ACC0B14B355784E803198D1AF4D9611F84588B961A0F4D31EED837FB9B312599.
Runtime-DLL unveraendert FFF3C419DE115BFDA2EF3AE31C971D66441E79F6B39DEB6CED55055D1B7C7AA7.
CI: 62/62 Tests und CLI/WPF-Build erfolgreich (tmp/isolated-start-ci.log).
UI-Smoke Exit 0: 31 Checks, 5/5 synthetische Kodierszenarien; gebuendelte Runtime
erkannt. --self-test fuer Studio-/Stationskonstruktion ebenfalls Exit 0.
Nachweise: tmp/release-startcheck-smoke2/SieEncode/ui-smoke/ui-smoke-result.json,
stdout.log, stderr.log und selftest-stderr.log im selben Smoke-Hauptordner.

Testmodus erzeugt vor dem Fenster einen expliziten SettingsStore mit separaten
Projekt-/Profil-/Backup-Pfaden und synthetischem Basisprofil. Der UI-Test wartet
nun auf Loaded und prueft die Startpfade. Erster Versuch zeigte eine bisherige
Luecke: Show/UpdateLayout allein garantierte nicht die abgeschlossene Initialisierung.
Der normale Start behaelt seinen bisherigen SettingsStore samt Migration.
Die Pruefung bestaetigt Softwareablaeufe, keine Kartenaktionen oder volle native
Bedienabnahme. Die oben genannten MSI-/ZIP-Hashes bleiben unveraendert; sie enthalten
diese neue Teststartkorrektur noch nicht. Erst zum naechsten sinnvollen Paketstand
neu paketieren. Hardware-/Upgrade-/Zweitbenutzerfreigaben bleiben offen.

# Offline-Gruppen: Kandidat 1.3.0-offline-groups.2

Stand 05.11.2026: lokal gepruefter MSI-Testkandidat, keine Releasefreigabe.
Quellstand: `8dcde39b0f1e28b923538f30bb791d615a44f87b`, sauber vor dem Build.
Korrektur: Nach Gruppenprojektkopie und Neuanlage stimmen Editor,
Bibliotheksauswahl, Kopfzeile und nachgelagerte Projektaktionen ueberein.

- MSI: `artifacts/offline-groups-1.3.0-preview.2/package/SieEncode_1.3.0-offline-groups.2_internal_x64.msi`
- MSI SHA256: `280D3162B1FF913A056B32127688AE9CC8F234C9132792BFAA0E1034733546B9`
- Studio DLL SHA256: `C84C1772A6E3D6FD9E73A7DEB18A2069DAED4487A35B6BE0CEBAD71CA4661B39`
- MSI ProductVersion: `1.3.0`; bestehender gleichversionierter Upgradepfad.
- Kein portables Paket. Internes Paket ohne Codesignatur.

CI der Quelle: 72/72 erfolgreich. Publizierte Kandidatenanwendung: UI-Smoke
Exit 0, Version 1.3.0-offline-groups.2, 60 Checks erfolgreich. Administrative
MSI-Extraktion Exit 0; alle 1.076 Dateien gegen den Releasebaum hashgleich.
Alle 495 Runtime-Dateien bytegleich zum ersten Kandidaten. Dessen MSI und
Abnahmekit stimmen weiterhin mit ihren bisherigen Pruefsummen ueberein.
Runtime-Provenienz und Grenzen bleiben wie bei Kandidat 1; kein neuer
Runtime-Rebuild und keine Hardwarefreigabe.

WiX meldet ICE40/ICE61 zu REINSTALLMODE und gleichversioniertem Upgrade.
Der Build und seine MSI-Vertragspruefungen waren erfolgreich. Eine reale
Installation/Upgrade dieses Kandidaten und der korrigierte Bedienlauf in der VM
stehen noch aus. Die bisherigen Installationsnachweise gelten fuer Kandidat 1.
Weitere offene Gates: nativer Empfaengerwechsel, Delegationsgesamtweg,
Deinstallation/Wiederinstallation mit Datenerhalt, visuelle Schlusskontrolle der
Kollegenanleitung. Keine abgeschlossene Mehrbenutzer-/Hardwareabnahme behauptet.

Maschinenlesbarer Nachweis: [Kandidat 2](offline-groups-candidate2-verification.json).
Lokale Logs: `tmp/offline-groups-candidate2-package.log`,
`tmp/offline-groups-candidate2-msi-admin.log`,
`tmp/offline-groups-candidate2-smoke/SieEncode/ui-smoke/ui-smoke-result.json`.

# Internes UI-Testpaket – 11.09.2026

Version: 1.1.0-ui-preview.12, Windows x64, .NET enthalten, keine RFID-Runtime.
Quellcommit: 24890cef51d8fa85e929bb6b5411b11b4b2bcec8 (codex/workspace-ui).
Datei: artifacts/SieEncode-UI-Test-20260911-win-x64.zip (lokal, nicht GitHub-Release).
Größe: 74.629.192 Bytes.
SHA-256: 9AF78E84666D2FE9BC317C86DC4A11BD50733901A6EC8B07C8DE441EC7F045F1.

494 Dateien im ZIP gegen SHA256-MANIFEST.json geprüft, dazu das Manifest selbst.
BUILD-IDENTITY.json ordnet Commit/Buildparameter/EXE-Hash zu. Die konkrete Paket-EXE
bestand --ui-smoke-test, Nachweis UI-SMOKE.json im Paket und tmp/ui-package-smoke/.
Die ZIP-Prüfung bestätigt Bytegleichheit zum geprüften Paketinhalt; ein nativer
Entpack-/Start-Bedienlauf auf einem zweiten Rechner steht aus.

Enthalten: aktuelle Expertenansichten, Soll-/Ist-/Vergleichs-PDF-Funktionen,
optionale Dokumentation/Bilder samt EXIF, scrollbare Karten-/Vaultdialoge,
PICC-Bindungs-/Abbruchtests, synthetisches Grundprofil, Anleitung und Lizenzbeilagen.
Standardpfade liegen unter Testdaten neben Studio; die Pfadtrennung ist keine Sandbox.
Kein Installer, kein RC16, kein Ersatz für RC15 und keine Hardwarefreigabe.
Vollständige praktische UI-/DPI-/Vault-/Zweitbenutzer-/Hardwareabnahme bleibt offen.

Build:
```
dotnet publish src/RFiDGear.Builder.App/RFiDGear.Builder.App.csproj -c Release -r win-x64 --self-contained true -p:SieEncodeUiPreview=true -p:Version=1.1.0-ui-preview.12 -o artifacts/ui-test-20260911/Studio
```
Nur Studio, synthetisches Grundprofil und freigegebene Beilagen paketiert;
keine beim Smoke entstandenen Testdaten oder Windows-Zertifikatsspeicher übernommen.

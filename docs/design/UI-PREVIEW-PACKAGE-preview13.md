# UI preview.13 – Vault-Hotfix, 11.09.2026

Quellcommit: 8d89570 (codex/workspace-ui).
Paket: artifacts/SieEncode-UI-Test-preview13-win-x64.zip.
Größe: 74.630.964 Bytes.
SHA256: 1860CA54BCF0C4556960C364AF334E692AAA3878A03624BA1097E31ADFFD5BB3.
494 ZIP-Dateien gegen das beiliegende Manifest geprüft (+ Manifest selbst).
Die konkrete Paket-EXE bestand den vollständigen WPF-Smoke: tmp/preview13-smoke/.
BUILD-IDENTITY.json und UI-SMOKE.json liegen bei.

Behebt reproduzierten Invalid-Handle-Absturz nach Neuladen der Windows-
Identitätsauswahl in Team-/Projekt-Vault. WPF-Auswahl wird vor Dispose getrennt.
Team-Aktionsfehler bleiben im Dialog; Speicherkonflikte überschreiben keine Teamliste.
Synthetische Wiederholungs-/Konflikt-/Speichertests bestanden.

In neuen Ordner entpacken und UI-Test-starten.cmd öffnen. Bestehende Windows-
Identitäten müssen nicht neu erzeugt werden. Die Vorschau verwendet eigene
Standard-Testdatenpfade; keine automatische Übernahme produktiver Vaults.
Keine vorhandenen Benutzerdateien oder Zertifikate geändert, keine privaten
Schlüssel exportiert. Kein RC16, kein Installer, keine RFID-Runtime enthalten.
Realer Bedienlauf, Hardware-/Zweitbenutzerabnahme und übrige UI-Gates bleiben offen.
preview.12 bleibt historisch erhalten, wird wegen des Vault-Fehlers aber abgelöst.

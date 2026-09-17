# Gemeinsamer Testkandidat 2026-09-17

Version `1.1.0-releasecheck.20260917`, MSI `1.1.17`, intern und unsigniert.
Enthaelt Vault-/Recovery-Korrekturen sowie die korrigierte Speicherstatusmeldung,
den erhaltenen Aenderungsstatus beim Profil-Refresh und sofort aktualisierte
Profilauswahllisten im offenen Editor. PDF-Weiterentwicklung bleibt zurueckgestellt.

Studio-Quellstand: `977741b252d27b325dec705225071b8c72eafd1c`, beim Build sauber.
Studio-DLL SHA256:
`655C4D5980992AB6EC05C8B8D374313FB42CBB58D4DFD071869EBF647727FF85`.
Runtime aus `artifacts/runtime-releasecheck-20260914` unveraendert wiederverwendet,
Source `c959bd4a930833af094b696100a71e248a1dc7c4`, 193/193 Tests dokumentiert.
BUILD-IDENTITY weist vorbestehende Quellaenderungen aus. Runtime-DLL SHA256:
`FFF3C419DE115BFDA2EF3AE31C971D66441E79F6B39DEB6CED55055D1B7C7AA7`.

Ausgabe: `artifacts/releasecheck-20260917/package`.
Paketnamen und SHA256/Groessen stehen im dortigen `SHA256SUMS.json`.
Build-Release Exit 0. Exakter Studio-Publish mit beigelegter Runtime:
UI-Smoke Exit 0, Status OK, 37 Checks und 5/5 synthetische Kodierszenarien,
stderr leer. Portable-ZIP: 1027 Dateien hashgleich zum Releasebaum;
alle drei Paketmanifest-Eintraege und VERSION geprueft.

Nachweise: `tmp/releasecheck-20260917-package.log`,
`tmp/releasecheck-20260917-smoke/SieEncode/ui-smoke/ui-smoke-result.json`,
`artifacts/releasecheck-20260917/package-verification.json`.
MSI administrativ nach `artifacts/releasecheck-20260917/admin-image` extrahiert,
Exit 0; Log `tmp/releasecheck-20260917-msi-admin.log`. Keine Installation.
Alle 1027 extrahierten Dateien und deren Anzahl stimmen mit dem Releasebaum
ueberein (SHA256-Einzelvergleich).

| Paket | Bytes | SHA256 |
| --- | ---: | --- |
| MSI | 118583884 | 26D1A97B49BEC2511B943F4C3006F25C729BCBB9F78982F9A1AF4EF32B985202 |
| Portable ZIP | 164996106 | 3FDBA18AB19B52BFEBC97AC623663E56DB7EE358171CDF1BC3D878077A5619AD |
| Acceptance Testkit ZIP | 31517 | 6D6D9B920AFA9D2B428DD0978ABB578CFB48D56C7AF722E4AF77EB64D7635EAF |

WiX ICE40/ICE61 bleiben Hinweise fuer die praktische Upgrade-Abnahme.

Der Versuch vom 16.09. wurde vor fertiger Paketierung unterbrochen. Er ist kein
auslieferbarer Kandidat und wurde nicht als erfolgreicher Paketbuild gewertet.

## Offene Freigaben

Kein finales Release: isolierte Windows-Installation/Upgrade/Deinstallation,
echter Zweitbenutzer-Vault, Reader/Testkarte inklusive ChangeFileSettings und
vollstaendige praktische UI-Paritaetsabnahme weiterhin offen.
Native Dateidialoge sind teilweise praktisch geprueft, siehe
NATIVE-DIALOG-ACCEPTANCE-2026-09-16.md. Synthetische Tests ersetzen keine Hardware-
oder Mehrbenutzerfreigabe. Kein MSI /i auf dem produktiven RC15-Host.

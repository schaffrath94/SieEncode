# Gemeinsamer Testkandidat 2026-09-15

Version 1.1.0-releasecheck.20260915 (MSI 1.1.15), interner unsignierter Kandidat.
Enthaelt die aktuellen Vault-/Recovery-Korrekturen und isolierten Teststart.
Kein finales Release und keine Installations-/Hardwarefreigabe.

## Quellen und Identitaet
Studio f8df5c9db99f79a3bbb08123f1d68a261d237495, sauberer Quellstand beim Build.
Studio-DLL SHA256: 060F078EE496ECB7FDAA6E539808EA89AADB8244F7AD531AA4B74BD432196865.
Runtime wiederverwendet aus artifacts/runtime-releasecheck-20260914; c959bd4,
193/193 Tests dokumentiert, BUILD-IDENTITY weist vorbestehende Quellaenderungen aus.
Runtime-DLL SHA256: FFF3C419DE115BFDA2EF3AE31C971D66441E79F6B39DEB6CED55055D1B7C7AA7.
Neue Hardwarefreigabe daraus nicht ableiten.

## Nachweise
- Build-Release.ps1 Exit 0, MSI-Startmenuevertrag und ProductVersion geprueft.
- Exakter Studio-Publish mit beigelegter Runtime: --ui-smoke-test Exit 0,
  35 Checks, 5/5 synthetische Kodierszenarien, stderr leer.
- MSI nur administrativ entpackt, Exit 0, alle 1025 Quelldateien hashgleich.
- Portable-ZIP: alle 1025 Dateien und Anzahl mit Releasebaum abgeglichen.
- Drei Manifest-Hashes/Groessen und paketierte VERSION geprueft.
- WiX ICE40/ICE61 bleiben bestehende Hinweise fuer praktische Upgrade-Abnahme.

Ausgabe: artifacts/releasecheck-20260915/package.

| Paket | Bytes | SHA256 |
| --- | ---: | --- |
| SieEncode_1.1.0-releasecheck.20260915_internal_x64.msi | 118575660 | 1F15EC95A293DE0CE39649AF2D5AF080442402598178FFAB664857EA86BDB69D |
| SieEncode_1.1.0-releasecheck.20260915_internal_portable_x64.zip | 164990577 | B817A1263D3179992525A1E6F1F048C20300EF10C282185D0D7E4CDF5A455948 |
| SieEncode_1.1.0-releasecheck.20260915_internal_acceptance-testkit.zip | 31517 | 26B39990CBC2B5F3A130BB1CF669B96E234DE0BD48071198DBB7D141DEBA0606 |

Lokale Evidenz: tmp/releasecheck-20260915-package.log,
tmp/releasecheck-20260915-msi-admin.log,
tmp/releasecheck-20260915-smoke/SieEncode/ui-smoke/ui-smoke-result.json,
artifacts/releasecheck-20260915/package-verification.json.

## Noch offen
Native Dateidialogabnahme wartet auf Computer-Use-App-Freigabe (letzte Anfrage
lief ab). Keine erneute gleiche Anfrage ohne Benutzerreaktion.
Installation/Upgrade/Deinstallation in isoliertem Windows, reale Hardware-/Karten-
und Zweitbenutzerabnahme bleiben offen. Kein MSI /i auf produktivem RC15-Host.
PDF-Weiterentwicklung bleibt zurueckgestellt. Keine erneute Paketierung ohne neue
relevante Aenderungen. Alte Pakete bleiben historische Referenzen.

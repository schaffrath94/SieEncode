# SieEncode Runtime 2.0.10.2

Dieser lokale Abnahmekandidat basiert auf der bereits hardwaregeprüften
SieEncode Runtime 2.0.10.1. Geändert wurde ausschließlich der ELATEC-Pfad für
`ReadAppSettings`:

- SearchTag, Application-Select und GetKeySettings werden bei transienten
  Transportfehlern bis zu dreimal mit frischer Sitzung wiederholt.
- Authentifizierungs- und Transportausnahmen werden mit AID, Keynummer und
  Versuch strukturiert protokolliert.
- Ein Erfolg wird weiterhin nur nach einem tatsächlich erfolgreichen
  GetKeySettings-Aufruf gemeldet; Settings-Mismatches bleiben Fehler.

Binäre Identität des Abnahmekandidaten:

- Produktversion: `1.0.0-rc3+rfidgear.2.0.10.2`
- `SieEncode.Runtime.exe` SHA-256:
  `15B2D16B39EC91C596481145220FB72377EDECAD223086C2D59FB2CE174F12B6`
- `SieEncode.Runtime.dll` SHA-256:
  `7A15314794EA0E729F6656A1AE9924CE2AA376E49D6A24738BD5AFFF24C84C0C`
- gemeinsamer EXE/DLL-Evidenzfingerprint:
  `9AC74DCDD7402E7C7CA39AC22D9173267756DBBC3C13E7D09B3C8303E6B2100D`

Dieser Kandidat wurde durch Runtime 2.0.10.3 ersetzt: Der sichtbare manuelle
Start zeigte noch einen fest verdrahteten WPF-Assemblypfad. Headless-Läufe und
die ELATEC-Prüflogik waren davon nicht betroffen.

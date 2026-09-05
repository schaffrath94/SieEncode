# SieEncode Runtime 2.0.10.3

Diese lokal hardwareabgenommene Runtime enthält den Fresh-Session-Retry aus 2.0.10.2
und korrigiert zusätzlich den sichtbaren Start der gebrandeten WPF-Runtime:

- `ReadAppSettings` wiederholt SearchTag, Application-Select und
  GetKeySettings bei transienten ELATEC-Transportfehlern bis zu dreimal.
- Drei Statussymbol-Pfade sind assemblyneutral. Damit bleibt die BAML-
  Ressourcenauflösung auch beim Assemblynamen `SieEncode.Runtime` gültig.
- Fenstertitel und About-Text verwenden die echte Produktversion der Assembly.

Binäre Identität der abgenommenen Runtime:

- Produktversion: `1.0.0-rc4+rfidgear.2.0.10.3`
- `SieEncode.Runtime.exe` SHA-256:
  `79274DC0340A57B3E307BCE693B671D26CE20DC683A9906BD9E7E2585B4AFEC6`
- `SieEncode.Runtime.dll` SHA-256:
  `A0BD4D11BD49C337F058B757618D9E234DA16A0C93E1E829F0B52060F7B8E90E`
- gemeinsamer EXE/DLL-Evidenzfingerprint:
  `85590C40D9F3A9E64CE60F0699CBF8B60C413314517745BA38130677000A7669`

Abnahme am 27.08.2026 mit ELATEC-Leser und freigegebenem DESFire-Testtransponder:

- 177/177 RFiDGear-Runtime-Tests und 40/40 SieEncode-Buildertests erfolgreich
- Recovery 2/2, Mastering 27/27 und Kodierung 26/26 erfolgreich
- finale Readbacks 3/3 frisch und bytegleich
- Produktivprobe 6/6 lesbar, davon 3/3 kodierte Payloads bytegleich belegt
- Application-/KeySettings-Abnahme 6/6 mit autoritativen Taskresultaten erfolgreich

Die Freigabe gilt exakt für den oben genannten EXE/DLL-Evidenzfingerprint;
Versionsbezeichnung oder Assemblymarker allein gelten nicht als Nachweis.

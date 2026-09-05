# Project Secrets V1

## Sicherheitsziel

Neue SieEncode-Projektdefinitionen und RFiDGear-`rfPrj`-Dateien enthalten keine
Transport-, Produktiv-, DESFire-, MIFARE-Classic- oder MAD-Schlüssel im
Klartext. Ein Schlüssel wird in einer `rfPrj` ausschließlich durch eine opake
Referenz der Form `secret://rfidgear/v1/...` bezeichnet.

Die Regel wird anhand der tatsächlich schlüsseltragenden Felder geprüft. Eine
Nullfolge in Datei-Nutzdaten ist ausdrücklich kein Schlüsselindikator.

## Speicherung

- Alle Transport- und Produktivschlüssel liegen ausschließlich im
  empfängergebunden verschlüsselten Projekt-Vault.
- Die Projektdefinition speichert Schlüsseltyp und Struktur, aber keinen
  Schlüsselwert.
- Neue `rfPrj`-Dateien enthalten nur Referenzen. Der Build-Preflight weist jedes
  Klartext-Schlüsselfeld als Fehler ab.
- Alte Klartextprojekte bleiben lesbar und werden im Betriebs-Preflight sichtbar
  als unsichere Altprojekte gekennzeichnet. RFiDGear speichert sie nicht erneut,
  bevor sie über SieEncode migriert wurden.

## Laufzeit

Nach dem Entsperren hält SieEncode die Referenzzuordnung nur für die aktive
Sitzung im Arbeitsspeicher. RFiDGear lädt `taskdatabase.xml` aus dem Archiv in
den Speicher und entpackt es nicht mehr nach `%LOCALAPPDATA%`.

SieEncode stellt einen lokalen Named-Pipe-Server mit `CurrentUserOnly` bereit.
Der Server akzeptiert ausschließlich die Prozess-ID der von SieEncode
gestarteten RFiDGear-Instanz. RFiDGear prüft umgekehrt über Windows, dass die
Pipe wirklich von der übergebenen SieEncode-Prozess-ID bereitgestellt wird.
Kommandozeile und Umgebungsvariablen enthalten nur Pipe-Name und Prozess-ID,
keinen Schlüssel, kein Passwort und keinen Entschlüsselungsschlüssel.

Ein Task erhält seine Schlüssel erst unmittelbar vor dem Kartenkommando. Nach
dem Kommando werden die Eigenschaften auch bei Fehlern wieder auf die Referenzen
zurückgesetzt. Fehlt eine Referenz oder ist ein Schlüssel ungültig, bricht der
Task fail-closed ab. Bereits aufgelöste Werte desselben Tasks werden ebenfalls
zurückgesetzt.

## Bewusste Grenzen

Ein vollständig kompromittierter Windows-Benutzerprozess, Administratorzugriff,
Prozessinjektion oder ein Speicherabbild während eines Kartenkommandos kann nicht
allein durch dieses Protokoll abgewehrt werden. Das Verfahren verhindert jedoch
die bisherigen dauerhaften Klartextkopien und eine unbemerkte Entschlüsselung
durch fremde Benutzer oder bloßen Dateizugriff. Es verwendet keinen statischen
Masterkey, keine selbstentschlüsselnde Sidecar-Datei und keine allein an einen
einzelnen Windows-Login gebundene DPAPI-Lösung.

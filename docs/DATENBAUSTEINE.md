# Modulare Datenbausteine

SieEncode baut den Inhalt eines Standard- oder Backup-Files aus einer geordneten
Liste von Bausteinen. Dadurch kann beispielsweise ein OSS-Infofile feste
Kennbytes, BCD-Ziffern, rohe HEX-Werte und lesbaren Text in genau vorgegebener
Bytefolge kombinieren. Die Bytevorschau im Profileditor zeigt Offset, HEX und
darstellbare Zeichen, bevor ein Projekt gespeichert wird.

| Auswahl in der Oberfläche | Schema-Operation | Ergebnis |
|---|---|---|
| HEX-Konstante | `fixedHex` | feste rohe Bytes |
| Klartext-Konstante (ASCII) | `fixedAscii` | fester Text mit genau einem Byte je Zeichen |
| Text-Konstante (UTF-8) | `fixedUtf8` | fester Unicode-Text; Zeichen können mehrere Bytes belegen |
| HEX-Bytes aus Variable | `hexBytes` | variabler Rohwert, z. B. `A0FF01` → `A0 FF 01` |
| Klartext aus Variable (ASCII) | `asciiText` | variabler lesbarer Text |
| Text aus Variable (UTF-8) | `utf8Text` | variabler Unicode-Text |
| Ziffern als ASCII | `asciiDigits` | `1234` → `31 32 33 34` |
| Ziffern als BCD | `bcdDigits` | `1234` → `12 34` |
| Ganzzahl Big-Endian | `uintBigEndian` | vorzeichenlose Zahl, höchstwertiges Byte zuerst |
| Ganzzahl Little-Endian | `uintLittleEndian` | vorzeichenlose Zahl, niedrigstwertiges Byte zuerst |
| Mit 00 auffüllen | `zeroPadToFileSize` | Rest der Datei wird `00` |
| Mit Byte auffüllen | `padToFileSize` | Rest der Datei wird mit einem gewählten Byte gefüllt |

Variable Bausteine besitzen eine feste Ausgabebreite in Byte. Bei HEX-, ASCII-
und UTF-8-Variablen kann links oder rechts mit einem HEX-Byte aufgefüllt werden.
Ohne Angabe verwendet ASCII ein Leerzeichen (`20`), UTF-8 und HEX verwenden
`00`. Ein Inhalt, der nicht in die Breite passt, wird niemals abgeschnitten,
sondern sperrt die Payload-Erzeugung mit einer verständlichen Fehlermeldung.

Beispiel eines modularen Infofiles:

```text
HEX CAFE → ASCII "OSS" → BCD Kartennummer → HEX Standort →
ASCII Namenskürzel, rechts mit 20 → Padding 00 bis Dateiende
```

Die Längenprüfung erfolgt gegen die tatsächliche Bytezahl, nicht nur gegen die
Anzahl sichtbarer Zeichen. Das ist besonders für UTF-8 wichtig.

Der interne RC8-Hardwaretest hat dieselbe modulare Payload mit allen zwölf
Bausteinen über die installierte Oberfläche geschrieben und anschließend drei
Files bytegenau zurückgelesen (16/16 korrelierte RFiDGear-Tasks, 3/3 Readbacks).

## DESFire-Dateitypen

SieEncode gibt aktuell nur `StdDataFile` und `BackupFile` zur produktiven
Kodierung frei. Value-, Linear-Record- und Cyclic-Record-Files benötigen eigene
Create-/Write-/Read-/Commit-Semantik. Der eingesetzte ELATEC-RFiDGear-Provider
und dessen Taskmodell decken diese Abläufe noch nicht vollständig ab. Sie werden
daher bewusst nicht nur als Dropdown-Option vorgetäuscht, sondern erst nach
Runtime-Erweiterung und realer Hardwareabnahme freigeschaltet.

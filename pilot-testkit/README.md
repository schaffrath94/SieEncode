# SieEncode – internes Abnahme-Kit

Dieses Kit sammelt reale interne Abnahmenachweise, ohne Schlüssel,
Vault-Inhalte, Projektdefinitionen oder Kartennutzdaten zu kopieren.

## Vorbereitung

1. Die in `PACKAGE-SHA256.txt` bezeichnete SieEncode-Version mit dem bereitgestellten internen MSI installieren.
2. Den gesamten Ordner dieses Kits an einen beschreibbaren Ort entpacken.
3. RFiDGear-Leser und Testkarten nur verwenden, wenn sie ausdrücklich für den
   Test freigegeben sind.
4. Produktive Schlüssel niemals in Testbögen oder Kommentare kopieren.

## Automatische Umgebungsprüfung

`START_PILOTTEST.cmd` doppelklicken. Das sichtbare SieEncode-Fenster wird
automatisch durch den UI-Smoke-Test geführt und wieder geschlossen. Reader und
Karte werden dabei nicht angesprochen.

Die Ergebnisse landen unter `Evidenz`. Der Sammler speichert nur Hashes der
Windows-SID, des Rechnernamens und der konfigurierten Pfade. Dadurch lassen sich
unterschiedliche Benutzer/Rechner belegen, ohne Kontonamen offenzulegen.

Nach dem Zusammenführen aller zurückgegebenen Nachweise startet
`Merge-PilotEvidence.ps1` die strikte Gesamtauswertung. Derselbe SID-Hash oder
dieselbe DPI-Stufe wird nur einmal gezählt. Unvollständige Testbögen und fehlende
Karten-Evidenzdateien bleiben offen.

Den Lauf durchführen:

- unter zwei unterschiedlichen Windows-Benutzerkonten oder Rechnern;
- jeweils bei realen 100 %, 125 % und 150 % Windows-Skalierung;
- nachdem beide Benutzer denselben vorgesehenen Projekt- und Profilstamm in
  SieEncode eingestellt haben.

## Unabhängiger Bedienertest

Zwei Personen, die nicht an SieEncode mitentwickelt haben, führen den normalen
Ablauf ohne Zuruf aus. Eine beobachtende Person verwendet
`USABILITY-TESTBOGEN.md`. Fachliche Erklärungen dürfen erst nach Abschluss eines
Schritts gegeben werden; sonst ist der Schritt nicht selbstständig bestanden.

## Mehrkartentest

`START_MEHRKARTENTEST.cmd` doppelklicken. Der Ablauf steuert ausschließlich die
installierte sichtbare SieEncode-Oberfläche und führt Projektwahl, Preflight,
zustandsabhängige Recovery, Mastering, Kodierung, finale Verifikation und den
regulären Runtime-Abschluss wie ein Bediener aus. Ein gestoppter Lauf wird nie
automatisch mit der nächsten Karte fortgesetzt und kann über denselben Start
sicher wieder aufgenommen werden.

Fünf physisch unterscheidbare und ausdrücklich freigegebene Testtransponder
verwenden. Vor Schreibzugriffen ist exakt `FREIGABE FUER 5 TESTTRANSPONDER`
einzugeben; anschließend muss jedes sichtbare Kartenlabel `TESTKARTE-01` bis
`TESTKARTE-05` einzeln bestätigt werden. Für jeden Transponder fragt das Kit
zusätzlich ausdrücklich ab, ob er neu und vollständig leer ist. Der
Blankokarten-Pfad akzeptiert nur die enge Signatur aus erfolgreicher Reader-/
Chip-Erkennung und 6/6 fehlenden Projekt-Reads unter beiden Keysets; erst dann
wird formatiert. Andere Fehlerbilder bleiben gesperrt. `MEHRKARTEN-TESTBOGEN.csv`
dokumentiert nur Labels, Ergebnisse und Evidenzdateien – niemals Schlüssel oder
vollständige Kartendaten.

## Rückgabe

Zur Auswertung zurückgeben:

- den Ordner `Evidenz`;
- die beiden ausgefüllten Usability-Testbögen;
- den Mehrkarten-Testbogen;
- die von SieEncode exportierten schlüsselwertfreien Sitzungs- und
  Diagnoseberichte bei Fehlern.

Anschließend ausführen:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Merge-PilotEvidence.ps1
```

Ein nicht bestandener Test ist wertvolle Evidenz und darf nicht nachträglich als
„bestanden“ umformuliert werden.

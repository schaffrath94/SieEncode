# Hardware-Abnahmevertrag

## Nachgewiesener Keywechsel-Vertrag (2026-08-20)

Der in c3rebro/RFiDGear PR #205 enthaltene Stand `c771070` wurde mit einem
freigegebenen ELATEC/TWN4-Testchip nachgewiesen: Format und Mastering erfolgreich,
Transport-Preflight 6/6, `ApplicationKeyChangeover` mit frischer Authentifizierung
des Zielschlüssels erfolgreich, unabhängiger Produktiv-Read erfolgreich und der
ersetzte Transport-Key mit `AuthFailure` abgewiesen. Anschließend wurde der Chip
erneut formatiert, gemastert und per Transport-Readback 6/6 bestätigt.

Der kombinierte Fork-Build `RFiDGear Builder Runtime 2.0.10.1` auf Commit
`2d0429f` integrierte PR #205 und #208. Er bestand 177/177 Softwaretests, den
vollständigen 26-Task-Kodierlauf, 6/6 unabhängige Produktivreads, die korrekte
Ablehnung von 6/6 alten Transportreads mit `TaskLoop.Failure` sowie Recovery
und abschließenden Transport-Readback 6/6.

Die Builder-Capability `VerifiedApplicationKeyChange` verlangt deshalb nicht
nur die vollständigen Implementierungsmarker, sondern zusätzlich genau die
freigegebene Produktversion und den gemeinsamen SHA-256-Evidenzfingerprint von
EXE und DLL. Die installierte offizielle RFiDGear-Version 2.0.10, teilweise
PR-Builds und jeder abweichende Fork-Build bleiben gesperrt.

Der C#-Builder ersetzt v10.39 erst nach einem vollständigen realen Bedienlauf
mit ausdrücklich freigegebenem Test- oder Wegwerftransponder. Das Erzeugen eines
Hardware-Abnahmeplans ist rein lesend und noch keine Freigabe für Reader- oder
Kartenaktionen.

## Nachgewiesener Terminalstatus-Vertrag (2026-08-20)

RFiDGear PR #208 (`9d42830`) wurde mit demselben freigegebenen Testchip und dem
ursprünglichen 26-Task-Fehlerfall geprüft. Nach drei erfolgreichen Preflights
meldete Task 40 `WriteData/Unknown`; 22 abhängige Aufgaben wurden übersprungen.
Der Lauf endete nun korrekt mit `TaskLoop.Failure`,
`UnhandledFailures=1` und `TerminalStatus=Failed`. Der anschließende strikt
lesende Transport-Preflight war 6/6 erfolgreich und endete mit
`TaskLoop.Success`; kein Keywechsel war erreicht worden.

## Nachgewiesener FileSettings-Vertrag (2026-08-20)

RFiDGear PR #206 (`57af444`) wurde isoliert mit ELATEC und demselben
freigegebenen Testchip geprüft. `CheckFileSettings` authentifizierte AID F482D0
mit dem Transport-Keyslot 0 und verglich Datei 0 vollständig: Typ 0/0, Größe
14/14, Kommunikationsmodus 3/3, Access-Rights-Byte 0 49/49 und Byte 1 3/3.
Der Task endete mit `NoError`, `Matches=true` und `TaskLoop.Success`. Eine
anschließende sechsfache Transport-Leseprüfung war vollständig erfolgreich.

## Sicherheitsgrenzen

- Jede reale Karten-/Readeraktion benötigt eine neue ausdrückliche
  Testchipfreigabe.
- Mastering und Kodierung sind schreibend.
- Recovery/FormatPICC löscht alle Applications und Files. Dieser optionale
  Abschnitt benötigt zusätzlich eine ausdrückliche destruktive Freigabe.
- Produktivmedien und unbekannte Schlüssel sind ausgeschlossen.
- Ein unbekannter Kartenstatus gilt niemals automatisch als leer.
- „Leer“ verlangt eine erfolgreiche vollständige AID-Inventur mit genau null
  Applications. Nullbytes, fehlende Projekt-AIDs, `ReadData`-Fehler und
  `AuthFailure` reichen niemals aus.
- Fehlerfälle werden nur bei ungefährlichen Read-only-Aufgaben provoziert;
  niemals absichtlich während Create, Write oder KeyChange die Karte entfernen.
- Berichte, Screenshots und Diagnosepakete dürfen keine Schlüsselwerte enthalten.

## Verbindliche Reihenfolge

1. Softwarebereitschaft und Projekt-Preflight ohne Hardware prüfen.
2. Transport- und Produktivprobe auf derselben Karte ausführen.
3. Neutralen Testtransponder genau einmal mastern.
4. Masteringzustand mit der Transportprobe kontrollieren.
5. Testdaten kodieren und terminalen RFiDGear-Status abwarten.
6. Finale Readbacks bytegenau auswerten und Prüfbericht erzeugen.
7. Mindestens fünf unterschiedliche Testnummern als Serie bearbeiten.
8. Einen ungefährlichen Fehlerfall kontrolliert erzeugen und dieselbe Nummer
   wiederholen.
9. Fehlernummer bewusst überspringen und persistenten Duplikatschutz prüfen.
10. Recovery nur optional auf separat freigegebenem Wegwerfmedium testen.

## Abnahmekriterien

- Kein stiller Erfolg und kein hängender Taskloop.
- Keine implizite Wiederholung einer nicht-idempotenten Schreibaufgabe.
- Alle erwarteten Nutzdaten besitzen aktuelle bytegleiche Readbacks.
- Erfolg, Fehler und Übersprungen sind in der Sitzung korrekt getrennt.
- Diagnose- und Prüfberichte bleiben keywertfrei.
- Application-/FileSettings ohne echten Istwert bleiben sichtbar „Nicht
  prüfbar“; ein Nutzdaten-Readback wird nicht als bitgenauer Settingsnachweis
  ausgegeben.

## Aktuelle Upstream-Grenzen

- RFiDGear #178 ist mit 2.0.10 und ELATEC erneut reproduziert: Der Taskstatus
  meldet erfolgreiche Application-Keywechsel, obwohl eine anschließende
  Produktiv-Key-Authentifizierung scheitert und der Transport-Key weiterhin
  funktioniert. Der kombinierte Builder-Runtime-Fork mit PR #205 und #208 ist
  exakt freigegeben; andere Builds bleiben gesperrt. Die Upstream-PRs bleiben
  bis zur Rückkehr des Owners offen.
- RFiDGear 2.0.8 und neuer behebt #201 mit exaktem `ReadAppSettings`-Modus und
  separatem `CheckAppKeyCount`. Der Builder integriert beide ab preview41 als
  getrennten strikt lesenden Lauf mit Capability-, Ergebnis- und rfPrj-Hash-Gate.
- RFiDGear #202/PR #206 stellt den getrennten read-only FileSettings-Task für
  Typ, Größe, Communication Mode und alle Access Rights bereit. Software- und
  Hardware-Smoke sind bestanden; Merge und Upstream-Release stehen noch aus.
- RFiDGear #207 ist mit PR #208 softwareseitig (172/172) und im ursprünglichen
  Hardwarefehlerfall behoben. Merge und Upstream-Release stehen noch aus.

Der Builder behandelt nicht freigegebene Runtime-Stände und die verbleibenden
FileSettings-/Host-Integrationsgrenzen fail-closed und weist sie im erzeugten
Abnahmeplan ausdrücklich aus.

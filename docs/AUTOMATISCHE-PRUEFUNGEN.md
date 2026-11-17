# Kompakte Prüfungen auf dem Entwicklungsrechner

## Ein Einstieg, getrennte Prüfbereiche

Aus dem Studio-Repository mit PowerShell 7:

```powershell
./tools/Invoke-Checks.ps1 -Suite Core,RuntimeStartup
```

`Core` verwendet die bestehende synthetische CI-Suite. `RuntimeStartup` führt
gezielt die Update-/Startargumenttests des benachbarten Runtime-Repositories aus.
Der Runtime-Prüfbereich setzt bereits wiederhergestellte NuGet-Pakete voraus.

Für eine ausdrücklich ausgewählte, bereits gebaute Testanwendung:

```powershell
./tools/Invoke-Checks.ps1 -Suite AppSmoke -Executable ./artifacts/offline-groups-1.3.0-preview.5/release/Studio/SieEncode.exe
```

Vorher SieEncode schließen. `AppSmoke` verwendet den bestehenden sichtbaren
Anwendungsprüflauf mit synthetischen Projekten, eigenen Einstellungen und
separaten temporären Verzeichnissen. `AppSelf` ist zusätzlich einzeln verfügbar;
AppSmoke enthält bereits RunSelfTest, daher nicht routinemäßig beide ausführen.
Die normale Hostinstallation wird nicht ersetzt oder bedient. Kein Readerzugriff,
keine Karten-Schreibtests und keine automatischen Installationen.

## Weniger Wiederholungen und Tokens

- `-Plan`: nur Eingaben prüfen und Prüfplan protokollieren, nichts ausführen.
- `PASSED`: in diesem Lauf bestanden.
- `REUSED`: frühere bestandene Ausführung bei gleichen Eingaben; ursprüngliches
  Datum und Log stehen im Bericht. Das ist ausdrücklich kein neuer Test.
- `FAILED`: konkretes Problem; der Prozesslog liegt neben dem Bericht.
- `-Force`: bewusster neuer Lauf auch bei gleichem Fingerabdruck.
- `-TimeoutSeconds`: Zeitlimit pro gestartetem Testprozess (Standard 600).

Die Fingerabdrücke berücksichtigen Runner, SDK, Rechner/Benutzer, Betriebssystem
und Quell-/Testdateien beziehungsweise den vollständigen Anwendungsordner.
Es gibt keine Wiederverwendung allein anhand einer Versionsnummer. Nach externen
Änderungen wie Treibern, Berechtigungen oder Zertifikatsumgebung `-Force` verwenden;
ein Dateifingerabdruck beschreibt nicht den gesamten Zustand des Rechners.

Ergebnisse bleiben lokal unter `artifacts/checks/<Lauf-ID>/summary.json`.
Keine Logs oder synthetischen Schlüssel automatisch veröffentlichen. Der Runner
startet Kindprozesse, trennt deren Ausgaben und beendet bei Timeout nur seinen
eigenen gestarteten Prozessbaum. Eine Sperrdatei verhindert parallele Runner.

## Grenzen und nächste Ausbaustufe

Der bestehende AppSmoke ist eine Anwendungsintegration mit Testmethoden,
kein vollständiger externer Klicktest. Noch ausstehend sind persistente synthetische
Fixtures und externe Dialogtests für Kopierauswahl nach Neustart,
Mitglied-/Geräteentzug und Delegation. Die bisherigen 22 Mehrbenutzer-API-Phasen
und MSI-Upgrades bleiben historische Nachweise; ihre versionsgebundenen
Einmalskripte werden nicht unbemerkt auf dem Host ausgeführt.

Die Freigabe vom 17.11.2026 erlaubt sichtbare Tests mit synthetischen Daten auf
dem Host. Zweite Windows-Benutzer und zweite Rechner bleiben eigenständige
Nachweise. Produktive Einstellungen, Vaults und Identitäten bleiben außerhalb
der Testdaten. Ein neuer Desktoptest muss ausschließlich seinen eigenen Prozess
und seine eigenen Fixtures adressieren.

## RFiDGear-Upstream separat bearbeiten

Erst diese Testbasis festigen, dann aktuelle Upstream-Änderungen inventarisieren,
nach Kompatibilität und Nutzen bewerten und auf separatem Entwicklungsbranch
selektiv übernehmen. Keine Upstream-Übernahme ist Teil dieses Runner-Schritts.
Runtime-Start, Studio-/Runtime-Vertrag und Kartenabläufe brauchen unterschiedliche
Nachweise. Bestandene Softwaretests ersetzen keine reale Hardwareabnahme.

## Externe Dialogprüfung (Stand 17.11.2026)

Im Studio-Repository mit PowerShell 7 ausführen:

```powershell
./tools/Build-DialogTests.ps1
./tools/Invoke-Checks.ps1 -Suite AppDialogs -Executable ./artifacts/dialog-test-build/SieEncode.DialogTests.exe
```

Der separate Testbuild verwendet ausschließlich synthetische Identitäten und eine eigene Bibliothek. Der Lauf `20261117-221516-06c52093` bestätigte Projektkopie einschließlich Neustart, unveränderte Quelle, Mitgliedsentzug und gezielte Deaktivierung des zweiten Geräts. Der Delegationstest erreichte den Exportdialog. Gesamtstatus dieses Laufs war FAILED, weil die erwartete Exportdatei fehlte. Der JSON-Bericht jedes Laufs weist abgeschlossene Teilschritte einzeln aus. Die Prüfung verwendet ein Windows-Konto und eine vorbereitete Vollmacht.

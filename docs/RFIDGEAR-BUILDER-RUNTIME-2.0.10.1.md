# Externe RFiDGear Builder Runtime 2.0.10.1

Diese Runtime ist ein getrenntes, in unserem Fork veröffentlichtes
RFiDGear-Paket. Sie ist nicht Bestandteil des Projektbuilder-ZIPs und kein
offizielles Release von c3rebro.

- Repository: `https://github.com/schaffrath94/RFiDGear`
- Branch: `codex/builder-runtime-2.0.10.1`
- Commit: `2d0429f2a26985a2d52a83caa493e619dc1c1cfc`
- Paket: `RFiDGear_BuilderRuntime_2.0.10.1_win10-x64_20260820.zip`
- Paket-SHA-256: `FE0873BEE26C9FB0E642794B89242D19CA2723EE9BE961BD1BC7EEA42077905C`
- `RFiDGear.exe`-SHA-256:
  `62583ED478559E7E55343145831AE638FED25823E4F564F38842AC1CDDED32E5`
- gemeinsamer EXE/DLL-Evidenzfingerprint:
  `0208509E43AE8ADF93583948FC2660CE3D287558E86DA9093CB3CE962C496582`

## Verwendung

1. Runtime-ZIP in einen eigenen dauerhaften Ordner entpacken.
2. Im Builder unter **Einstellungen** die darin enthaltene `RFiDGear.exe`
   auswählen.
3. Umgebungstest ausführen. `Verifizierter DESFire-Keywechsel` und
   `Autoritativer TaskLoop-Terminalstatus` müssen als verfügbar erscheinen.
4. Ein abweichender Pfad ist erlaubt, ein abweichender Binärinhalt nicht. Schon
   ein verändertes Byte sperrt Keywechsel fail-closed.

## Nachweis

- 177/177 RFiDGear-Softwaretests bestanden.
- 40/40 Builder-Kern-/Runtime-Tests und die vollständige v10.39-Paritätsmatrix
  bestanden.
- Vollständiger ELATEC/DESFire-Hardwarelauf: 26/26 Kodiertasks erfolgreich,
  6/6 unabhängige Produktivreads erfolgreich, 6/6 alte Transportreads korrekt
  mit `TaskLoop.Failure` abgewiesen, Recovery und Transportabschluss 6/6.

Sobald ein offizielles Upstream-Release denselben Vertrag erfüllt und als
exaktes Release-Binary erneut hardwaregeprüft wurde, kann dessen eigener
Fingerprint additiv in den Vertrauenskatalog aufgenommen und die Fork-Runtime
geordnet abgelöst werden.

# RFiDGear Host Contract V1

Contract-ID: `de.rfidgear.host.v1`  
Schema-Version: `1.1`

Dieser Vertrag definiert die Integrationsgrenze zwischen Projektbuilder und
RFiDGear. Er ist heute als Builder-seitiger Prozessadapter umgesetzt. Die
formalen JSON-Schemas unter `docs/contracts` bilden zugleich die Übergabegrenze
für einen späteren nativen `IRFiDGearHostV1`.

## Verantwortung

Der Builder besitzt ausschließlich:

- Profile und Projektdefinitionen,
- Key-Vault und Freigabe der Sitzungsschlüssel,
- Payloaderzeugung und Bedienworkflow,
- Serienhistorie, Abnahme und Diagnoseexport.

RFiDGear besitzt ausschließlich:

- Reader-Provider und Readerzustand,
- Kartenpolling und UID-Wechsel,
- Taskengine und sämtliche Kartenkommandos,
- verbindliche Task- und Terminalergebnisse.

Der Vertrag implementiert keine Reader- oder Taskengine im Builder.

## Transport heute und Ziel

Heute startet der Adapter RFiDGear mit `CUSTOMPROJECTFILE`, einer
prozessgebundenen Secret-Pipe und optional `AUTORUN=1` beziehungsweise
`AUTOMODE=1`. Die `rfPrj` enthält dabei ausschließlich Secret-Referenzen; Details
und Bedrohungsgrenzen beschreibt `PROJECT-SECRETS-V1.md`. Der Adapter erfasst für Kodierung, Mastering,
Karten-Preflight, Application-Abnahme und Recovery jeweils denselben
unveränderlichen Request. Strukturierte RFiDGear-Ereignisse und anschließende
Builder-Abnahmen werden getrennt in das V1-Ergebnis normalisiert.

Ziel ist ein nativer Host `IRFiDGearHostV1`, der denselben Request akzeptiert
und dasselbe Resultat beziehungsweise korrelierte Ereignisse liefert. Damit
ändert sich der Transport, nicht die fachliche Eigentumsgrenze.

Jeder Build erzeugt `rfidgear.host-contract.json`. Alle vom Builder gestarteten
RFiDGear-Operationen schreiben
atomar nach:

```text
integration/host-v1/runs/<requestId>.request.json
integration/host-v1/runs/<requestId>.result.json
```

Die `requestId` ist eine nicht wiederverwendbare GUID. Request und Resultat
verwenden dieselbe ID. Ein terminaler Engine-Status darf nicht auf einen
anderen Lauf zurückgesetzt oder nachträglich umgeschrieben werden.

## Request

Der Request enthält:

- Contract- und Schema-Version,
- Operationskennung,
- Projektname und projektlokalen rfPrj-Dateinamen,
- SHA-256 des tatsächlich angenommenen rfPrj,
- Modus `open`, `autorun` oder `automode`,
- keyfreien Taskplan ohne Beschreibungen,
- SHA-256 des normalisierten Taskplans,
- erwartete Taskanzahl,
- erforderliche und binär nachgewiesene Fähigkeiten,
- erkannte RFiDGear-Version.

Absolute Pfade, `..`, Schlüssel, Payloadbytes, Kartenwerte und
Taskbeschreibungen sind verboten. Das referenzierte rfPrj darf weiterhin Keys
enthalten; der Request selbst enthält sie nicht.

Beispiel, gekürzt:

```json
{
  "schemaVersion": "1.1",
  "contractId": "de.rfidgear.host.v1",
  "requestId": "11111111-2222-3333-4444-555555555555",
  "operation": "coding",
  "projectName": "Demo",
  "projectFile": "Demo_Kodierung.rfPrj",
  "projectDirectory": ".",
  "mode": "automode",
  "taskPlanSha256": "<64 HEX-Zeichen>",
  "expectedTaskCount": 26,
  "requiredCapabilities": [
    "CustomProjectFile",
    "ContinuousAutoMode",
    "VerifiedApplicationKeyChange"
  ],
  "containsKeys": false,
  "containsCardData": false,
  "referencedProjectMayContainKeys": true
}
```

## Resultat: Engine und Consumer-Abnahme getrennt

`state` ist ausschließlich der normalisierte RFiDGear-Enginezustand:

- `requested`: Request persistent, Prozess noch nicht gestartet,
- `running`: Prozess gestartet, fachliches Ergebnis ausstehend,
- `successful`: RFiDGear lieferte einen vollständig korrelierten
  `TaskLoop.Success`-Lauf,
- `failed`: Prozess-, Task-, Terminal- oder Korrelationsfehler,
- `timedout`: kein verbindlicher Abschluss innerhalb des Zeitlimits,
- `cancelled`: bewusst abgebrochener, nicht erfolgreicher Lauf.

`acceptanceState` ist davon getrennt und gehört dem Builder. Mögliche Werte
sind `waiting`, `successful`, `failed`, `not-evaluated` und `not-applicable`.
`accepted=true` gilt ausschließlich bei `state=successful` und
`acceptanceState=successful`.

Das Resultat enthält Prozess-ID, SHA-256 der strukturierten RFiDGear-`RunId`,
das Terminalereignis sowie für jeden Task geordnet Position, Index, Typ,
Entscheidung, Skipgrund, Outcome, Fehlercode und Zeitstempel. Freitext aus dem
RFiDGear-Log wird nicht übernommen. Payload-SOLL/IST, Schlüssel, UID und andere
Kartenwerte gehören ebenfalls nicht in dieses Ergebnis.

Ein positives RFiDGear-Terminalereignis ist damit noch keine positive
Builder-Abnahme. Kodierung verlangt weiterhin frische bytegleiche Readbacks;
Kartenstruktur-Abnahme verlangt alle erwarteten exakten Application- und
FileSettings-Taskresultate. Für
Keywechsel gilt zusätzlich: `VerifiedApplicationKeyChange` muss nachgewiesen
sein.

## Capability-Handshake

Der Request leitet die Pflichtfähigkeiten deterministisch ab:

| Bedingung | Pflichtfähigkeit |
|---|---|
| jeder Request | `CustomProjectFile` |
| `autorun` | `AutorunOnce` |
| `automode` | `ContinuousAutoMode` |
| jeder ausführende Modus | `StructuredTaskLogging` |
| `ReadAppSettings` | `ExactAppSettingsCheck` |
| `CheckAppKeyCount` | `AppKeyCountCheck` |
| `CheckFileSettings` | `FileSettingsCheck` |
| Application-/PICC-Keytask | `VerifiedApplicationKeyChange` |

Eine Pflichtfähigkeit, die nicht zugleich als verfügbar nachgewiesen ist,
verhindert den Request. Unbekannt bedeutet gesperrt.

`VerifiedApplicationKeyChange` ist absichtlich zweistufig. Die vollständigen
Nachauthentifizierungsmarker im Binary belegen nur die Implementierung. Eine
produktive Freigabe verlangt zusätzlich einen exakten, pfadunabhängigen
SHA-256-Fingerprint der gemeinsam geprüften `RFiDGear.exe` und
`RFiDGear.dll`. Freigegeben ist ausschließlich der kombinierte, separat
paketierte Fork-Build `RFiDGear Builder Runtime 2.0.10.1` auf Commit `2d0429f`,
der den vollständigen Software- und ELATEC/DESFire-Hardwaretest bestanden hat.
Andere offizielle, PR- oder Entwicklungsbuilds bleiben bis zu ihrem eigenen
exakten Hardware-Smoke gesperrt. Das RFiDGear-Paket wird nicht in den Builder
eingebettet.

`AuthoritativeTaskLoopTerminalStatus` ist bis zu einem Upstream-Release von PR
#208 eine additive Fähigkeit und keine Pflicht für Altinstallationen. Ist sie
vorhanden, garantiert RFiDGear, dass `TaskLoop.Success` beziehungsweise
`TaskLoop.Failure` aus den vollständigen Taskoutcomes, ausgeführten passenden
Bedingungszweigen und explizitem `ErrorRouting` abgeleitet wurde. Der Builder
behält seine eigene geordnete Taskabnahme trotzdem bei.

## Keywechsel-Sicherheitsregel

RFiDGear 2.0.10 meldete im Hardwaretest Application-Keywechsel als erfolgreich,
obwohl der Transport-Key aktiv blieb. Deshalb muss ein nativer Host nach einem
Keywechsel Karte und Application neu auswählen und mit dem neuen Ziel-Key im
geänderten Slot authentifizieren. Erst diese Nachauthentifizierung darf ein
positives Taskergebnis erzeugen. Referenz: `c3rebro/RFiDGear#178`.

## Invarianten

1. Der Request ist unveränderlich und eine ID darf nicht erneut verwendet werden.
2. rfPrj- und Taskplan-Hash müssen bis zum terminalen Resultat unverändert
   bleiben.
3. Request- und Result-Capabilities sind geschlossene Enumwerte ohne Duplikate.
4. Ein Engine-Erfolg benötigt genau eine korrelierte `RunId`,
   `TaskLoop.Success` und ein Ergebnis für jede erwartete Taskposition.
5. Enginezustand und Consumer-Abnahme bleiben getrennt; nur ihre gemeinsame
   positive Bewertung setzt `accepted=true`.
6. Taskzähler werden aus dem geordneten Taskarray abgeleitet und dürfen diesem
   nicht widersprechen.
7. Ein terminaler Enginezustand und ein finaler Consumer-Abnahmestatus sind
   unveränderlich.
8. Request und Resultat deklarieren und enthalten weder Keys noch Kartendaten.
9. Vertragsdateien werden atomar geschrieben.

## Formale Schemas

- `docs/contracts/de.rfidgear.host.v1.request.schema.json`
- `docs/contracts/de.rfidgear.host.v1.result.schema.json`

Beide Schemas sind Draft 2020-12, schließen unbekannte Top-Level-Felder und
setzen Contract-ID sowie Schema-Version fest. Die C#-Validierung ergänzt
querschnittliche Regeln wie Taskplan-/Projektfingerprint, Capability-Mengen,
Request/Result-Korrelation und erlaubte Zustandsübergänge.

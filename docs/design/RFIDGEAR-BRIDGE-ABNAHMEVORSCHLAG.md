# Vorschlag für einen gemeinsamen Bridge-Abnahmetest

Ergänzung zu c3rebro/RFiDGear#232, Stand 17.11.2026. Der letzte Kommentar beschreibt bereits Projektbindung, Kartenpräsenz, Vault-Freigabe und strukturierte Prüfergebnisse. Dieser Vorschlag definiert beobachtbare Ergebnisse für einen Test mit simuliertem Reader und erfundenen Referenzen. Er ist noch nicht gegen eine offizielle Engine ausgeführt.

## Minimale Testanordnung

Zwei synthetische Projektzustände A und B teilen Pfad und Taskanzahl. Sie unterscheiden sich in einer öffentlichen Dateigröße. Zwei simulierte Kartenpräsenz-Ereignisse und ein zählender Testprovider liefern deterministische Eingaben. Der Reader zählt ausgeführte Schreibaufträge. Der Provider zählt freigegebene Schlüsselauflösungen, ohne deren Werte aufzuzeichnen.

| Testfall | Erwartetes Ergebnis |
| --- | --- |
| A freigeben, anschließend im Host auf B ändern, dann Auftrag A starten | Ablehnung vor Kartenausführung. Schreibzähler 0. Keine Schlüsselfreigabe. |
| Karte A scannen und freigeben, vor Start durch Karte B ersetzen | Ablehnung. Schreibzähler 0. Keine Wiederholung ohne neue Freigabe. |
| Eine gültige Referenz aus einem anderen Auftrag anfragen | Keine Schlüsselfreigabe. Auftrag und Ablehnung sind ohne Schlüsselwert zuordenbar. |
| Einen Schreibauftrag abschließen, Karte entfernen, erneut auflegen und ausschließlich Lesetest starten | Schreibzähler bleibt beim Wert nach dem ersten Auftrag. Lesetest erhält eine eigene Laufkennung. |
| Verbindung während der Auflösung verlieren und Client neu verbinden | Kein selbsttätiger Neustart des Schreibauftrags. Client verlangt bei uneindeutigem Ergebnis Prüfung. |
| Lesetest erwartet 32 Byte und beobachtet 16 Byte | Abweichung wird dem richtigen Lauf und Task zugeordnet. Der Client kann sie ohne Logtext erkennen. |

Zum Austausch genügen Host-Commit, Bridge-Version, Testfall, beobachteter Terminalstatus, Taskposition sowie beide Zähler vor und nach dem Versuch. Feldnamen und Transport können sich weiterentwickeln. Interessant für beide Projekte ist, welche Fälle der SampleClient am aktuellen master bereits nachweisen kann. Ein gemeinsamer erster Test wäre der Projektwechsel bei identischem Pfad und identischer Taskanzahl.

## Ausgeführter Kontrolltest am 17.11.2026

Offizieller Quellstand e607c2986cd000c88a6a3101620a963882f738e6, Windows x64, lokales SDK 10.0.404, Release. Der einzelne Test BridgeSampleClientTests.ARunThatExpectsAnotherProject_IsRefused_AndTheHostIsNeverAsked bestand (1/1). Der SampleClient kommuniziert über eine Named Pipe mit dem Testhost. Ein abweichender Projektpfad und eine abweichende Taskanzahl wurden abgelehnt. Der passende Auftrag wurde abgeschlossen. Bei einem Checkout unterhalb des Studio-Verzeichnisses die Studio-spezifische Directory.Build.props vom Upstream-Build ausschließen.

Der getestete StartRunRequest enthält die Projektvorbedingungen ExpectProjectPath und ExpectTaskCount. Ein erwarteter Revisionswert für den geladenen Projektinhalt fehlt im DTO. Die Ablehnung eines Inhaltswechsels bei unverändertem Pfad und gleicher Taskanzahl bleibt daher ein offenes Abnahmekriterium. Der Test nutzte weder den installierten Upstream-Release noch einen Reader.

Der Befund zur Projektbindung wurde in https://github.com/c3rebro/RFiDGear/issues/232#issuecomment-6963270675 übergeben. Auf demselben Commit bestanden zwei weitere hardwarefreie Tests aus BridgeKeyProviderTests (2/2): AClientThatLeavesWhileItIsAsked_CostsTheKey_NotTheWholeTimeout und AKeyThatArrivesAfterTheTimeout_IsNotHandedOut. Sie decken den Verlust des Clients während der Anfrage und die verspätete Antwort nach Timeout ab. Offen bleibt der Gesamtzustand eines laufenden Schreibauftrags nach Verbindungsabbruch und Wiederverbindung.

## Verbindungsverlust und Wiederanlauf

Am selben Upstream-Commit bestanden fünf gezielte Prüfungen aus BridgeSampleClientTests und BridgeRunRecoveryTests (5/5, Named Pipe, synthetischer Host). Nach Verbindungsverlust meldet der SampleClient zunächst Unknown. ResumeAsync mit derselben requestId findet den laufenden Auftrag beim weiterhin aktiven Host wieder. Im Test blieb es bei genau einem Ausführungsauftrag. Bei einem Start-Timeout fragt der Client den Status ab.

Nach simuliertem Hostneustart mit erhaltenem Ausführungsjournal wurde ein zuvor laufender Auftrag als unterbrochen erkannt. Ein zuvor abgeschlossener Auftrag blieb als abgeschlossen bekannt, sein Detailergebnis war verloren. In beiden Fällen wurde er nicht erneut gestartet.

Für eine spätere Studio-Anbindung: Auftragskennung vor dem Start persistent speichern, nach Verbindungsverlust den Status abfragen und unklare oder unterbrochene Läufe zur Prüfung anzeigen. Bei fehlenden Detailergebnissen ist vor Kartenfreigabe ein unabhängiger Readback erforderlich. Die Kombination aus Schlüsselauflösung, Abbruch, Wiederverbindung und tatsächlicher Kartenänderung ist noch nicht im Gesamtablauf geprüft.

## Kombinierter Test mit ausstehender Vault-Anfrage

ResumeAfterPendingVaultDisconnect bestand in 911 ms am Upstream-Stand e607c2986cd000c88a6a3101620a963882f738e6 mit lokalem Testzusatz. Der synthetische Ausführungshost fordert während des Auftrags einen Schlüssel über die Bridge an. Erst nachdem der Client die Anfrage erhalten hat, trennt der Test die Pipe. Der Provider liefert ProviderUnavailable und keinen Schlüssel. Der Testhost modelliert diesen Fehler als fehlgeschlagenen Auftrag. Der getrennte Client sieht Unknown. Ein neuer Client erhält bei ResumeAsync zur ursprünglichen requestId den Terminalstatus failure. Der Ausführungszähler bleibt bei eins. Es wird keine Karte angesprochen.

Reproduktion im sauberen Checkout des genannten Commits mit SDK 10.0.404: beiliegenden Patch mit git apply anwenden und dotnet test RFiDGear.Tests/RFiDGear.Tests.csproj -c Release --filter FullyQualifiedName~ResumeAfterPendingVaultDisconnect ausführen. Der Patch ergänzt ausschließlich die Tests. Die Fehlerzuordnung und der physische Kartenzustand in einem realen DESFire-Task bleiben separat abzunehmen.

## DESFire-Task bei ausbleibendem Schlüssel

Stand e607c2986cd000c88a6a3101620a963882f738e6. Drei bestehende Upstream-Testmethoden mit aufzeichnendem Reader-Doppel ausgeführt. Zehn DESFire-Fälle bestanden. Bei ReadData ergaben sieben Auflösungsfehler (NotFound, NotAuthorized, ProviderUnavailable, Timeout, Cancelled, InvalidKey, Unsupported) jeweils KeyUnavailable und null Reader-Aufrufe. Bei ProviderUnavailable bestätigte der TaskExecutionService zusätzlich IsSuccess=false, NormalizedErrorCode=KeyUnavailable und ExecutionOutcome.Failure. Bei ApplicationKeyChangeover und PICCMasterKeyChangeover scheiterte die Beschaffung des neuen Schlüssels, obwohl ein anderer Schlüssel bereits angefragt wurde. Auch hier null Reader-Aufrufe, einschließlich Authentifizierung.

Der Methodenfilter erfasste zusätzlich acht gleichnamige Classic-Fälle. Gesamt 18/18. Der lokale Bridge-Testzusatz aus dem vorherigen Lauf war im Testassembly enthalten und wurde nicht erneut ausgeführt. Diese Ergebnisse beziehen sich auf den jeweiligen Task vor dessen erstem Kartenbefehl. Bereits ausgeführte frühere Tasks eines Auftrags sind separat zu bewerten. Hardware und der externe Studio-Adapter bleiben eigenständige Abnahmen.

## Soll/Ist-Ergebnis und öffentlicher Installer, 18.11.2026

Im Release-Testbuild von f58785f (mit dem lokalen Updater-Patch) bestanden zwei bisher offene Prüfungen aus CheckFileSettingsTests mit simuliertem Reader. TheTaskReportsItsComparisonToTheLogAsPlainNumbers_AndForgetsItOnRequest prüft am ViewModel unter anderem Matches, die Istgröße 16 und das erste Zugriffsbyte. TheComparisonOfOneRun_DoesNotSurviveIntoTheNext bestätigt, dass nach einem Authentifizierungsfehler der vorherige Vergleich gelöscht ist. Beide lassen sich über den Methodennamen mit dotnet test --filter auswählen.

Im Bridge-Vertrag dieses Commits enthält TaskOutcomeDto Position, Projektindex, Typ, Erfolg, Fehlercode und Überspringgrund. Ein Feld für die verglichenen Größen fehlt. Offen bleibt die Übermittlung eines abweichenden Größenpaars (Soll 32, Ist 16) an Studio mit Lauf- und Taskzuordnung.

Der öffentliche Release v2.0.10.0 verweist laut GitHub-API auf 1045f70ac64d0a049dfcd82f8b61b627faa22ea6. Sein vollständig gelisteter Git-Baum enthält keinen Pfad mit RFiDGear.Bridge. GitHub nennt für RFiDGearBundleSetup.exe 285441360 Byte und SHA-256 5584e013e5eacde915b27124518979721bab8e56e25c01a76084ec787cbc9471. Dies ist ein Metadatenabgleich. Der Installerstart und die Abnahme einer offiziell ausgelieferten Bridge stehen aus.

## Automatik bei Kartenpräsenz, 18.11.2026

Vier AutoModeControllerTests im vorhandenen Release-Testbuild f58785f mit lokalem Updater-Patch bestanden (4/4). Eine beim Start bereits aufliegende Karte löst einen Lauf aus. Bei unveränderter Karte über fünf Abfragen bleibt es bei einem Lauf. Nach erkanntem Entfernen und erneutem Auflegen derselben Karte erfolgt ein zweiter Lauf mit neuer RunId. Eine andere UID löst auch ohne dazwischen beobachteten Leerzustand einen Lauf aus. Alle vier Fälle verwenden simulierte Präsenzereignisse.

Für die Stationsintegration folgt daraus: Vor Aktivierung der Automatik muss der beabsichtigte Auftrag feststehen, auch wenn die Karte bereits aufliegt. Der Wechsel vom Schreibauftrag zum reinen Lesetest samt Zählung real ausgelöster Schreibbefehle bleibt gesondert zu prüfen.

---

## Aktualisierung 20.11.2026 – Upstream #396/#397 und Antwort auf #232

**Herkunft:** Upstream `c3rebro/RFiDGear` `master` bei `1cb5b9854283`; Stellungnahme des Entwicklers in
https://github.com/c3rebro/RFiDGear/issues/232#issuecomment-7032412757. Unsere Antworten in
https://github.com/c3rebro/RFiDGear/issues/232#issuecomment-7042751419.

**Releasegrenze:** Der offizielle Installer `v2.1.0` vom 19.11.2026 enthält die grundlegende JSON-RPC-Bridge, **aber nicht** die erst danach gemergten Schutzfunktionen `project.revision` (#396) und `Check UID` mit Laufvariable (#397). In `master` ist die Versionsnummer bereits 2.1.1.0, jedoch am 20.11. nicht als Release veröffentlicht. Nie `version >= 2.1.0` als Beweis dieser Fähigkeiten akzeptieren; `hello.capabilities`, laufende `instanceId`, Original-DLLs und tatsächliche Testnachweise prüfen.

### P0 – Eindeutiger Projektstand (#396, upstream implementiert)

- `loadProject` und `getStatus.project` liefern die opake `revision` bei Capability `project.revision`; `startRun.expectProjectRevision` bindet den Start an genau den freigegebenen geladenen Inhalt. Ergebnis: `projectRevision`, die Revision beim Laufstart.
- Ein falscher/fehlender oder nicht prüfbarer Erwartungswert muss als `projectMismatch` + `neverStarted=true` **vor Kartenzugriff** abgewiesen werden. Unbekannter Capability-Status sperrt unseren produktiven Adapter.
- `revision` ist ein **prozessgebundener keyed hash**, kein persistent verwendbarer SHA und keine stabile Identität. Nach Änderung von `hello.instanceId`: neu laden, neu prüfen und neu freigeben.
- Upstream erkennt die bei einem eigenen Bridge-Lauf in Task-Einstellungen entstandenen Änderungen als legitime Lineage; Änderungen durch UI, Extension oder AutoMode sind nicht automatisch freigegeben. Fail closed und Revision erneut prüfen.
- **Nicht abgedeckt** sind externe Payloaddateien, die erst zum Lauf geladen werden. Die bestehende Studio-Logik für unveränderliche Auftrags-ID, Profil-/Taskplanfingerprint, Projekt-Workspace-Lease, Datei-SHA und unmittelbare Prüfung der Schreibbytes bleibt aktiv.

### P0 – Einzelläufe mit Kartenfreigabe (#397, upstream implementiert)

Das Kodierprojekt erhält zu Beginn einen `Check UID`-Task mit Sollwert `$EXPECTEDUID`. **Alle** schreibenden Tasks müssen ausdrücklich von dem erfolgreichen UID-Check abhängen. Studio erzeugt dieses Projekt und verifiziert die Abhängigkeiten im Preflight; der Host erzwingt nicht generell, dass beliebige Schreib-Tasks das Gate benutzen.

1. `scanReader` zur Beobachtung; Studio prüft Auftrag, erwartete UID, Seriennummer, Profilbindung und Duplikate. UID ist keine kryptografische Echtheitsprüfung.
2. Vor dem `startRun`: frische Projekt-Revision, Payload-Hashes und einen dauerhaft gespeicherten eindeutigen `requestId` verwenden.
3. `startRun` mit `expectProjectPath`, `expectTaskCount`, `expectProjectRevision` und `variables: [{name: "$EXPECTEDUID", value: "<synthetische UID>"}]`. Die Taskengine liest die tatsächliche UID selbst; ein Wechsel zwischen Scan und Lauf führt zu `CardNotExpected`, und abhängige Schreib-Tasks werden übersprungen. Eine nicht gelieferte/leer gelieferte Variable ist ebenfalls ein Fehler.
4. Unklarer Start oder Verbindungsabbruch: ausschließlich `getRunResult` mit **derselben** `requestId`; niemals eine neue ID zum erneuten Schreiben erfinden. `unknown`, `bridgeFault` oder fehlende Details erfordern unabhängige Lesekontrolle.
5. **Schreibprojekt beenden → Karte entfernen → erneut auflegen → getrenntes ausschließlich lesendes Abnahmeprojekt**; keine automatische zweite Schreibausführung.

**AutoMode darf derzeit nicht produktiv schreiben:** Eine beim Start gesetzte UID-Variable wird nach Abnehmen/Wiederauflegen derselben Karte erneut akzeptiert. Auf #232 ist deshalb Variante **(b) – Freigabe beim Auflegen** angefragt: Reader-Ereignis → Studio-Entscheidung → einmalige, präsenzgebundene Genehmigung mit Timeout, Abbruch/Clientverlust fail closed, keine Wiederverwendung beim Wiederauflegen. Bis zur Implementierung strikt Einzelläufe.

### P0 – Tresorfreigabe pro Lauf (vom Upstream zugesagt, noch nicht implementiert)

Geplant sind `resolveKey.requestId`, `source`, `projectRevision` und ein optionaler `registerKeyProvider`-Schalter, der nur Bridge-Client-eigene Läufe akzeptiert. Studio muss diese Felder plus `instanceId`, UID, AID, Slot, Verwendung und Credential-Referenz gegen die eigene **explizite, kurzfristige** Auftragsfreigabe prüfen. Nicht passend → `notAuthorized`; keine Geheimwerte in Logs/Events oder Report. Die Abwehr gilt nicht gegenüber einem böswilligen Programm im selben Windows-Konto. **Kein produktiver Vault-Adapter**, solange das Host-Gate und die negativen Integrationstests fehlen.

### P1 – Strukturierte Check-File-Settings-Ergebnisse (noch nicht implementiert)

Angefragt: optionales `verificationDetails` mit Schema-Version; AID, FileNo, erwarteter/tatsächlicher Typ; Dateigröße (Regel exact/atLeast/atMost, Soll/Ist, gewählt ja/nein); Communication Mode (Soll/Ist, gewählt, Einzelerfolg); vier Access-Nibbles Read, Write, ReadWrite, Change (je Soll/Ist/gewählt/Einzelerfolg). Keine Karten-Payloadbytes oder Schlüssel; freie Texte nie als maschinenlesbaren Abnahmebeweis behandeln. Bis zur Umsetzung kein „kompletter Soll-Ist-Bericht“ aus einer reinen `TaskOutcomeDto` ableiten.

### Unverzichtbare Testmatrix vor Umstellung

| Test | Erwarteter Host-/Studio-Nachweis | Status 20.11.2026 |
| --- | --- | --- |
| Gleicher Pfad/Taskanzahl, aber Taskinhalt geändert | `projectMismatch`, `neverStarted`, 0 Schreiboperationen | Upstream-Softwaretests #396 berichtet; kein Studio-/Hardwaretest |
| Dateipayload nach Freigabe geändert | Studio blockiert bei Hashabweichung vor Start | Bestehende Studio-Prüflogik; kombinierter Bridge-Test offen |
| Karte zwischen Scan und `startRun` getauscht | `CardNotExpected`, schreibende Abhängigkeiten übersprungen | Upstream-Softwaretests #397 berichtet; Hardwaretest offen |
| UID beim Start leer/nicht gesetzt | `CardNotExpected`, keine Schreiboperationen | Upstream-Softwaretests #397 berichtet |
| Karte entfernt und erneut aufgelegt | Keine zweite Schreibfreigabe; nur Readback | Vollständiger Produktionsablauf offen; AutoMode-Schreiben gesperrt |
| Nicht autorisierte Anfrage aus UI/fremdem Lauf | `notAuthorized`, 0 Schlüsselbereitstellungen | Host-Gate noch nicht implementiert |
| Verbindungsabbruch während Start/Schlüsselauflösung | Gleiche `requestId`, Abfrage statt Wiederholung; unklarer Kartenzustand wird kontrolliert | Einzelne frühere synthetische Tests; echter Ablauf offen |
| `CheckFileSettings` Sollgröße 32/Istgröße 16 | Strukturierte Abweichung im korrelierten Task, kein Log-Parsing | `verificationDetails` noch nicht implementiert |
| Wechsel zu offizieller Engine | Exakte Binäridentität, installierter Host, Reader-/DESFire-HIL, Projekt-/Vault-/Abnahme-Parität | Nicht erfolgt |

**Keine Tests von uns in dieser Aktualisierung ausgeführt.** Der Stand ist eine nachprüfbare Auswertung von Upstream-Code, PR-Beschreibungen, Bridge-Vertrag und bereits dokumentierten historischen Softwaretests. Nicht mit einem getesteten, installierten SIE-Encode-Bridge-Adapter oder einer Hardwarefreigabe verwechseln.

---

## Aktualisierung 20.11.2026, Abend – #398/#399 gemergt, #400/#401 offen

Quelle: [RFiDGear #398](https://github.com/c3rebro/RFiDGear/pull/398), [#399](https://github.com/c3rebro/RFiDGear/pull/399), [#400](https://github.com/c3rebro/RFiDGear/pull/400), [#401](https://github.com/c3rebro/RFiDGear/pull/401). Verglichen mit dem zuvor dokumentierten Stand `1cb5b985` liegen #398 und #399 in `master` (`0c29710`), aber nicht in der veröffentlichten v2.1.0. Keine SIE-Encode-Hardwaretests in dieser Dokumentationsänderung.

### P0 – Schlüsselkontext umgesetzt (#398, upstream master)

- Neue Capability **`keys.context`**: Bei `resolveKey` sind `requestId`, `source` und `projectRevision` optional verfügbar. Fehlende `source` ist `other` und **nicht** implizit `bridge`; fehlende ID/Revision nie als freigegeben interpretieren.
- `registerKeyProvider(ownRunsOnly=true)` schränkt den Provider im Host auf Bridge-Läufe desselben verbundenen Clients ein; für andere Laufursprünge gibt RFiDGear `notAuthorized` zurück, ohne den Client aufzurufen. **Antwort `ownRunsOnly=true` verifizieren**; Host ohne Capability sperren. Ein reconnecteter Client besitzt nicht rückwirkend die Läufe der vorigen Verbindung.
- Studio prüft zusätzlich, *welcher* eigene Lauf, Auftrag, Projekt, UID, AID, Slot und Verwendungszweck autorisiert wurde. `ownRunsOnly` ersetzt **nicht** unsere auftragsbezogene Freigabe, noch schützt es vor anderer Software desselben Windows-Benutzers.
- Upstream berichtet 6.803 bestandene Softwaretests, darunter echter Pipe-Test gegen simulierten Host. Die Hardwarefälle 38–42 wurden noch nicht ausgeführt.

### P1 – Vergleichsergebnisse umgesetzt (#399, upstream master)

- Capability **`execution.verificationDetails`**: Ein `TaskOutcomeDto` kann optionale `verificationDetails` mit `schemaVersion: 1`, `kind: desfire.fileSettings`, `overallMatch`, `compareMode` (`all`/`any`), `applicationId`, `fileNumber`, `errorCode` und geordnete `checks` enthalten.
- Ein Check enthält `name`, `compared`, `result` (`match`, `mismatch`, `notRead`, `notCompared`), ggf. `rule` (`exact`, `atLeast`, `atMost`), `expected` und `actual`. Die festen Checknamen sind `fileType`, `size`, `communicationMode`, `accessRead`, `accessWrite`, `accessReadWrite`, `accessChange`.
- Nicht gewählte oder nicht lesbare Felder dürfen **nicht** als erfolgreiche Vergleiche behandelt werden. Bei einem Fehler vor der eigentlichen Gegenüberstellung fehlt `verificationDetails` vollständig. Nur `Check File Settings` liefert sie bislang. Keine Schlüssel, keine rohen Kartennutzdaten.
- Upstream dokumentiert Software-/Pipe- und Mapper-Tests, aber die Hardwarefälle 43–46 sind noch offen. Studio muss die Daten für den **separaten lesenden Abnahmelauf** zuordnen und darf sie nicht mit dem Erfolg des vorherigen Schreibdurchlaufs verwechseln.

### F – Referenzablauf für *einen* Schreibauftrag (#400, **PR offen**)

Der vorgeschlagene SampleClient `RunApprovedCardAsync` führt das Prinzip vor: nur Reader-Status `ready`, genau eine primäre Karte, externe Auftragsfreigabe, anschließend Einzellauf mit `$EXPECTEDUID` und `expectProjectRevision`. Der Ergebnis-UID-Abgleich ist nötig, aber **nicht hinreichend für unabhängige physische Readback-Freigabe**; der separate lesende Lauf bleibt erforderlich. Die offene PR enthält nur Beispielcode/Guide/Checkliste, keinen produktiven Studio-Adapter. Nicht als gemergtes Host-Feature dokumentieren.

### C1 – AutoMode-Resultat enthält Ausgangsrevision (#401, **PR offen**)

Die Projekt-Revision soll im Ergebnis eines automatischen Runs erscheinen. Dies ist **keine** einmalige Kartenfreigabe: PR #401 ändert die Schreibautomatik nicht, die per-Präsenz-Genehmigung ist für später geplant. Daher bleiben automatische **Schreib**-Läufe für SIE Encode gesperrt.

### Prioritäten für die nächste Entwicklungsrunde

1. **A/B read-only/synthetischer Adapter:** capabilities `project.revision`, `keys.context`, `execution.verificationDetails` nur auf tatsächlicher Hostversion prüfen; v2.1.0 hat die neuen drei Capabilities noch nicht.
2. **Einzellauf als einzige Schreibstrategie** bis zur Einmalfreigabe nach Kartenauflage; `Check UID` muss alle Schreib-Tasks tatsächlich kontrollieren.
3. **Schlüsselgates / Prüfung der Strukturwerte mit negativen Tests:** Verweigerung fremder Laufursprünge, fehlender ID/Revision und falscher AID/Referenz; Ergebnis `notRead`/fehlende Details dürfen nicht zu einem grünen Abnahmebericht führen.
4. **Erst nach Windows-Installation und freigegebenem Karten-/Reader-Test:** funktionale Parität und möglicher Runtime-Wechsel. Echte Karten-HIL nicht durch PR-Testzahlen oder GitHub-Pages-Erfolg ersetzen.

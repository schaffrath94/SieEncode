# SieEncode – Zielbild für zentral kontrollierte DESFire-Schlüsseloperationen

**Entwurf zur Architekturentscheidung, 21.11.2026. Keine Implementierung oder Freigabe; erstes SieEncode-Release 1.0 ist noch in Entwicklung.**

## Sicherheitsziel

Ein entfernter Mitarbeiter darf **nach Wirksamwerden eines Entzugs keine neue produktive DESFire-Kryptographie mehr ausführen**, sofern die Station nur mit einer aktuell autorisierenden, vertrauenswürdigen Stelle arbeiten darf. Ein Mitarbeiter darf **niemals Master- oder diversifizierte Langzeitschlüssel als Klartext vom Dienst erhalten**. **Historisch vorher ausgehändigte Schlüssel bleiben kompromittiert**, bis die betroffenen Karten und Leser/SAMs kontrolliert umgestellt wurden.

Sofortiger, globaler Entzug bei beliebig langer Funk-/Netzwerk-Trennung ist **nicht möglich**. Eine Offline-Ausnahme benötigt eine klar benannte maximale Nachlaufzeit und eine geschützte Hardware-Vertrauensgrenze; andernfalls strikt `fail closed`.

## Empfohlenes Zielmodell: Control Plane und Crypto Plane

### Zentrale Verwaltung (SieEncode Control Plane)

- Kunden-/Mandantenisolation: je Kunde getrennte Schlüsselräume, Gruppen, Projekte, Zertifikate, Recovery und Audit. Keine globale Karten-Masterschlüsselkopie in frei kopierbaren Projektdateien.
- Personen- und Geräteidentitäten mit gesonderter Authentisierung (z. B. Unternehmens-SSO/FIDO2 plus Geräte-Zertifikat/mTLS). Der persönliche Windows-Vault-Schlüssel allein ist **keine** zentrale Autorisierung.
- Rollen projekt- und tätigkeitsbezogen: z. B. Gruppenverwaltung, Projektplanung, Kartenuntersuchung, Testkodierung, produktive Personalisierung, Key Change, Recovery.
- Vorgangsfreigabe mit Zweck, Projekt, Kartenfamilie, AID, Key-Version, Leser, Zeitfenster, Limit und optional Vier-Augen-Prüfung. Zentraler Status `REVOKED` muss zukünftige Vorgänge verweigern.
- Unveränderbar nachvollziehbares Ereignisprotokoll: wer, welches Gerät, welcher freigegebene Auftrag, welcher Kartenlauf, Ergebnis/Abbruch; niemals Rohkeys, komplette authentisierte APDUs, Passphrasen oder nicht erforderliche personenbezogene Daten protokollieren.
- Verwaltung von Schlüsselgenerationen, geplanten und nachgewiesenen Rotationen, betroffenen Karten und Reader-/SAM-Freigaben.

### Kryptografischer Dienst (SieEncode Crypto Plane)

- Vertrauenswürdiges HSM bzw. abgeschottete SAM-basierte Kryptografie. Master-Schlüssel werden geschützt **innerhalb** dieser Grenze erzeugt/importiert und bleiben nicht exportierbar.
- Keine generische `GetKey`-API und kein beliebig für Clients verfügbarer AES-/CMAC-Oracle. Stattdessen überprüfte, kartenspezifische Vorgänge: `BeginCardSession`, `AuthenticateApplication`, `PrepareSecureCommand`, `VerifyCardResponse`, `CommitOrAbort` und separat freigegebener `RotateCardKey`.
- Der Server bzw. das Hardwaremodul übernimmt die nötige DESFire-Authentisierung, Diversifikation und Secure Messaging; die Windows-Kodierstation leitet nur die vorgesehenen APDU-/NFC-Daten zwischen Reader/Karte und vertrauenswürdigem Dienst weiter. Die tatsächliche APDU-/SAM-Fähigkeit und Latenz müssen erprobt werden.
- Sitzung eindeutig an bestätigten Bediener, Gerät, Aufgabe, Projekt, Leser und Karten-/Authentisierungsdialog binden; Nonces, Sequenznummern, kurze Laufzeit, Abbruch bei Richtlinienwechsel, Rate Limiting und manipulationssichere Ergebniszuordnung.
- Diversifikation pro Karte/Applikation/Key-Version mit dokumentierter Kartenidentität und kompatibler Leserstrategie (etwa NXP AN10922), sofern für den Kundentyp überhaupt zugelassen. Nicht blind UID nutzen, wenn Karten mit Random-ID arbeiten.
- Kryptografische Freigabe ist kein Ersatz für eine dokumentierte Kontrolle über die Leser-/Kartenwelt: jede Key-Rotation erfordert kartenseitige Verifikation sowie abgestimmten Rollout der betroffenen Leser/SAMs.

## SieEncode-Station und vorhandene RFiDGear-Engine: tatsächlicher Änderungsbedarf

Aktuell existieren `ProjectSecretPipeServer` (Antwort mit einem vollständigen Schlüsselwert an den lokalen Prozess) und `SecureOperationalSessionService.Materialize` (lokal temporär aus einem mit Schlüsseln angereicherten Projekt erzeugte Laufdateien). **Diese Pfade dürfen in einer strengen Zentral-Krypto-Sitzung nicht verwendet werden.** Ein Zentralserver, der nur denselben Rohschlüssel über HTTPS liefert, beseitigt das Kopier- oder Extraktionsrisiko nicht.

Geplante Schnittstellengrenze (noch nicht implementiert):

- `IKeyOperationProvider` für geprüfte DESFire-Operationen **ohne Rohschlüsselrückgabe**.
- `CentralAuthorizedProvider`: Netzwerk zu vertrauenswürdigem Crypto-Service, striktes Deny bei Ausfall.
- `StationSamProvider`: nur bei dafür gebauter Reader-/SAM-Hardware, mit ebenfalls geschützter Schlüsselprovisionierung und klar definierten, überprüfbaren Offline-Grenzen.
- `LegacyVaultProvider`: bestehender interner Entwicklungsweg; **keine Freigabe als zentral sofort widerrufbares Verfahren**.
- Capability-Handshake für die tatsächliche unterstützte Reader-/RFiDGear-Engine, bevor produktive Kartenänderungen zur Auswahl stehen. Das heutige RFiDGear kann nicht ohne Weiteres als vollwertiger `IKeyOperationProvider` vorausgesetzt werden.

### Strenger Sicherheitsmodus

**Jede produktive Schlüsseloperation braucht aktuellen Serverkontakt.** Kein Netz, ungültige Server-Identität, widerrufener Mitarbeiter, kein freigegebener Auftrag oder Timeout → **keine Kodierung / kein Key Change**. Rein öffentliche Karteninventur kann bei gesichertem Lesepfad unabhängig bleiben. Ein autorisierter Auftrag ist **kein** Freibrief für spätere offline ausgeführte Schlüsseloperationen.

### Optionaler Standort-/Offline-Modus

- **Kunden-LAN mit Standort-Krypto-Appliance:** Station kann ohne Internet mit einem lokalen, vertrauenswürdigen Dienst arbeiten. Ein Entzug aus der zentralen Zentrale wird erst nach einem zuverlässigen, überprüften Abgleich am Standort wirksam; der Standort braucht gegebenenfalls einen eigenen autorisierten Administrator für sofortige lokale Sperren.
- **Mobiler SAM-Einsatz:** Technisch begrenzter, nur durch Hardware durchsetzbarer freigegebener Vorgangsumfang; Schlüssel verlassen den SAM nicht. Sicherheitsfenster/secure time/rollback-Schutz dürfen nur zugesagt werden, wenn die gewählte Hardware das **nachweislich** implementiert. Stets Restgefahr bei gestohlenem oder noch offline befindlichem autorisiertem Gerät. Ein normaler Zertifikatstoken auf einem Windows-PC genügt nicht.
- Weder Standort-Appliance noch SAM garantieren rückwirkende Löschung früher bekannter Karten-Geheimnisse.

## Abnahmekriterien und PoC-Vorgehen

1. **Vorab: Entscheidung und Threat Model** für echte Mitarbeiterabmeldung, gestohlenen PC, gestohlenen SAM, kopierte Projektdatei, kompromittiertes früheres Master-Key-Material, Offline-Standorte, abgelaufene Zertifikate, Zentral- oder Standortausfall, Angreifer mit lokalem Admin-Recht.
2. **Enger Adapter-Prototyp ohne Karten-Write**: Client kann für einen synthetischen Job nur eine autorisierte Crypto-Operation anfordern; statische Analyse/Tests zeigen, dass weder Named Pipe noch temporäre `rfPrj` einen Raw-Key enthalten. Negativtests für Rollen, Widerruf, Mandantenwechsel, Replay, Session-/Prozesswechsel, Serverausfall und lokale Manipulation.
3. **DESFire-Labortest mit freigegebener Testkarte**: vollständige mehrschrittige AES-Authentisierung, Secure Messaging, tatsächlicher Kartenstatus und Latenz ohne lokalen Raw-Key. Vergleich mit konkret unterstützten Readern und RFiDGear-Runtime; exakte Fähigkeiten nachweisen, nicht vermuten.
4. **Geschützte Master-Schlüsselherkunft und Recovery**: HSM/SAM-Key-Provisionierung, Dual Control, Backup/Wiederanlauf, Versionierung, Notfallwechsel und vollständige Trennung der Kundendomänen.
5. **Optional Hardware-SAM-PoC**: NXP SAM AV3 und ein kompatibler Reader/SAM-Host (S- oder X-Mode) unter realen Protokollbedingungen; Offline-Berechtigungen nur mit verifizierter Hardwarefunktion.
6. **Erst danach** Migrations- und Releaseentscheidung für die gesamte 1.0-Architektur. Kein automatischer produktiver Key Change, keine vorgetäuschte Sofortsperre und keine Erweiterung der normalen UI mit unerprobten Sicherheitsaktionen.

## Strategische Entscheidung

**Bei geforderter sofortiger Sperre:** Standardmodus auf Online-`Crypto Plane` mit HSM, verpflichtender aktueller Autorisierung und ohne Rohschlüsselausgabe ausrichten. Das ist ein **substantieller Umbau der RFiDGear-Schlüsselverwendung**, kein zusätzlicher Vault-Dialog.

**Bei zwingender Offline-Kodierung:** zusätzliche Kunden-LAN-Appliance oder SAM-Station, aber deutlich und vertraglich benannter Sicherheits-/Widerrufshorizont; gegebenenfalls betroffene DESFire-Kartenschlüssel rotieren. Keine Architektur behauptet gleichzeitige garantierte Sofortsperre und unbegrenzten Offline-Betrieb.

Referenzen: NXP MIFARE SAM AV3; NXP AN12696 und AN12701 (SAM-/DESFire-Integration); NXP AN10922 (Diversifikation); NIST SP 800-57 (Key-Management-Lebenszyklus). Hersteller-Unterlagen begründen **Fähigkeiten**, keine automatisch bestätigte Integration oder Freigabe für unseren derzeitigen Reader-/Engine-Stand.

# SieEncode 1.0 – bestehende Vault-/Projekttechnik zur vollständigen Schlüsselbibliothek erweitern

**Umsetzungsplan vom 21.11.2026. Kein Rewrite, keine alte veröffentlichte SieEncode-Version, keine Kunden-Altprojektmigration.** Die zentrale Bibliothek, Synchronisation und Klartextverwaltung sind noch **nicht implementiert**. Der erste unabhängige Metadatenvertrag `KeyLibraryContracts.cs` und synthetische Tests liegen auf dem Entwicklungszweig, noch **nicht ausgeführt**.

## Technischer Befund aus aktuellen Quellen

| Vorhandene Datei | Tatsächliche Rolle | Entscheidung |
|---|---|---|
| `ProjectModels.cs`, `ProjectJson.cs` | Projektentwurf, Profile, Transporteinstellungen, produktive Schlüsselplätze; `WithoutAllSecrets` entfernt die Geheimwerte vor der Speicherung | Behalten, später **optionale Schlüsselreferenzen** hinzufügen |
| `ProjectKeyVault.cs` / `ProjectKeyVaultPayload` | Typisierte Schlüsselplätze, alternative Ausgangsschlüsselsätze, `Apply` hydratisiert ein Projekt für einen Lauf | Behalten als **bestehende Brücke zum Kodier-/Buildpfad** |
| `GroupVaultService.cs` | Authentisierte AES-GCM-Verschlüsselung und gruppen-/zertifikatgebundener Zugriff; sein Header ist derzeit **projektgebunden** (`ProjectId`) | Kryptografische Implementierung **gezielt wiederverwenden**, aber eine Bibliothek **nicht** stillschweigend als normales Kartenprojekt tarnen. Separaten, explizit zweckgebundenen Hüllen-/Dokumentvertrag vor Implementierung prüfen und testen |
| `ProjectGroupVault.cs` | Konkretes Projekt an Gruppe/Revision binden, geheimen Projekt-Payload verschlüsseln/laden | Als funktionierende Übergangslösung behalten; für neue kataloggebundene Projekte langfristig **nicht** zweite unabhängige dauerhafte Schlüsselquelle |
| `OfflineGroupTrustStore`, `GroupAdministrationWindow` | Signierte Richtlinien, Personen-/Geräte-/Gruppenzugänge, Recovery | Weiterverwenden und um Bibliotheksberechtigungen erweitern, nicht neu erfinden |
| `SecureOperationalSessionService`, `ProjectSecretPipeServer`, RFiDGear-Host | Temporärer Buildsitzungs-/Schlüsselpfad für vorhandene Kartenkodierung; eine Named Pipe kann **Klartextwerte** liefern | Behalten, nach späterer autorisierter Auflösung wie bisher aufrufen; keine Änderungen am Reader/Runtime ohne separaten Hardwaretest |
| `ProjectCatalog` | Such- und Statusliste vorhandener Projekte | Um Katalogbindung/Version und fehlende Schlüssel ergänzen, keine zweite Projektverwaltung |

## Zielbild ohne Doppelpflege

**Eine führende Schlüsselbibliothek, viele Referenzen.** Ein Geheimwert ist fachlich ein versioniertes `KeyId + Version` in genau einem zentral verwalteten Schlüsselsatz. Mehrere Projekte dürfen dieselbe Referenz benutzen; ein Kartenprojekt besitzt nicht mehr seine eigene unabhängige Kopie desselben produktiven Schlüssels.

```text
Zentrale Hauptbibliothek (ein autoritativer, versionierter Gesamtbestand)
    │  bei Gelegenheit signiert und verschlüsselt abgleichen
    ▼
Vollständiger lokaler Offline-Bestand (geschützte Geheimwerte + Metadaten)
    ├── Schlüssel anzeigen / kopieren / für SIPORT bereitstellen (explizite Rechte)
    ├── Projekt A -> KeyId + Version
    └── Projekt B -> dieselbe KeyId + Version
                    │ gezielte autorisierte Entschlüsselung, keine stille Versionserhöhung
                    ▼
           bestehender ProjectKeyVaultPayload
                    ▼
           ProjectKeyVault.Apply / bestehender Build
                    ▼
           vorhandene SieEncode-Kodierstation + RFiDGear
```

**Wichtige Trennung:** Der Bibliotheksindex (`KeyLibrarySnapshot`) enthält **nur Metadaten**, keine Geheimwerte. Ein späterer **verschlüsselter** Speicher enthält die Rohwerte mit gleichem `KeyId + Version` als geprüfte, von Bibliotheks-ID und Revision abhängige Zuordnung. Geheimwerte dürfen nicht in die Projektdefinition, Katalogsuche, Berichte, Git oder das Synchronisationsprotokoll geraten.

### Fachliche Schlüsselidentität

- `KeyId` bleibt unverändert, auch bei Umbenennung oder Versionswechsel; `Version` adressiert eine **exakte** Generation. `CustomerId` und `KeyType` dürfen sich für dieselbe `KeyId` nicht zwischen Generationen ändern.
- `Status` unterscheidet aktive, abgelöste und kompromittierte Versionen. Produktive Neuvorgänge benötigen ausdrücklich aktive Versionen; Geschichte darf dennoch nachvollziehbar bleiben.
- Ein Projekt erhält eigene, eindeutig definierte Slot-Zuordnungen (PICC/Transport/Anwendungs-AID/Key-Nummer/gegebenenfalls Ausgangsschlüsselsatz) zu `KeyId + Version`. **Slot-Schema erst mit dem Editor/Runtime-Adapter festlegen**, nicht per freier Namensgleichheit raten.
- **Keine automatische Umstellung auf die neueste Version.** Ein Wechsel ist eine geprüfte Projekt- und gegebenenfalls Karten-/SIPORT-Änderung, nicht ein gewöhnlicher Bibliothek-Sync.
- Diversifikation ist **ausdrücklich nicht** Teil von 1.0.

## Inkrementeller Ablauf – immer nur eine Umstellung zur Zeit

### Phase 1 – verlässliche IDs und Metadaten (erster Codebaustein vorhanden)

`src/RFiDGear.Builder.Core/KeyLibraryContracts.cs`: `KeyLibraryAddress`, `KeyLibraryEntry`, `KeyLibrarySnapshot` und `ProjectKeyLibraryBinding` als **nichtpersistenter, secrets-freier Fachvertrag**. Validiert Bibliothek-ID/Revision, eindeutige Versionen, Kunden-/Typkonsistenz, bekannte Referenzen und Betriebsstatus. Synthetische Tests unter `KeyLibraryContractTests`; erst zum gebündelten Testmeilenstein ausführen.

**Noch nicht:** Kunden-UI, zentraler Datentransport, Klartextschlüssel, Projekt-Formatänderung oder Runtime-Änderung.

### Phase 2 – verschlüsselte vollständige lokale Bibliothek

- Bereits implementierte AES-GCM-/Zertifikats-/Recovery-Bausteine **wiederverwenden**, dabei den Bibliotheks-Typ kryptografisch von Projekt-Vaults trennen und Manipulation/Vertauschung per AAD/Domänenbindung verhindern. Keine neue unabhängige zweite Kryptoimplementierung.
- Bibliotheks-Schlüsselcontainer selbst besitzt stabilen `LibraryId`, einen definierten Schlüsselsatz, zugelassene Empfänger und atomare Revisionen. Für Benutzer mit begrenzten Rechten sind echte getrennte Kryptozugänge pro Kunden-/Schlüsseldomäne nötig; eine einzige von allen Kollegen entschlüsselbare Vollkopie reicht **nicht**. Voll berechtigter Administrator führt hingegen den **gesamten** Schlüsselbestand verschlüsselt bei sich.
- Recovery, Backups und Versionshistorie müssen getrennt geschützt sein. Keinen historischen Vault in einen gemeinsam lesbaren Projektordner kopieren.

### Phase 3 – Projektreferenz und eindeutiger Resolver

- Projektdefinition bekommt *optional* geprüfte, **geheimwertfreie** Slot-`KeyId + Version`-Bindungen.
- Bestehende Projekte/Drafts funktionieren bis zur **ausdrücklich bestätigten** Umstellung unverändert. Das ist intern ein Entwicklungspfad, **keine behauptete Migration früherer Produktversionen**.
- Für kataloggebundene Projekte **keinen** kompletten Projekt-Vault mit identischen Geheimwerten dauerhaft parallel pflegen. Fachlich führt nur die Bibliothek. In einem streng kontrollierten temporären Build-Schritt werden genau die gebundenen Werte aufgelöst und in den **vorhandenen** `ProjectKeyVaultPayload` projiziert. Dann `ProjectKeyVault.Apply` und die vorhandene Kodier-/Bauprüfung aufrufen.
- **Fail closed**: unbekannte KeyId, falsche Version, falscher Kunde/Keytyp, unberechtigter Benutzer, abgelöster/kompromittierter Schlüssel, inkonsistenter Bibliotheksstand oder nicht auflösbarer Slot **stoppt den Vorgang**. Kein automatischer Rückfall auf ältere feste Rohwerte.
- Die bestehende Bereitstellung der Rohwerte für RFiDGear ist für den beabsichtigten Offline-Kodiermodus weiterhin erforderlich. Temporäre Artefakte sind möglichst kurzlebig; **kein Versprechen rückwirkend widerrufbarer Klartextschlüssel**.

### Phase 4 – eine verständliche Schlüsselverwaltungsansicht

- Hauptnavigation mit **einem** klaren Arbeitsbereich „Schlüsselverwaltung“, nicht einem eigenen Knopf je Unterfunktion.
- Kunden → Schlüsselsätze → Schlüsselversionen; Werte standardmäßig verdeckt.
- Berechtigte bewusste Aktionen: Schlüssel importieren, anlegen, anzeigen, kopieren, SIPORT-Ausgabe, Version kontrolliert wechseln. Exportformat und Integration mit SIPORT erst nach tatsächlichem Format-/Verwendungstest zusagen.
- Bestehende Projekteditoren können aus der Bibliothek wählen; keine zweite manuell gepflegte Schlüsselbezeichnungsliste.

### Phase 5 – zentraler Gesamt-Abgleich ohne Online-Zwang

- Jede autorisierte Vollzugriffs-Station synchronisiert **alle Kunden und alle Schlüssel** automatisch, einschließlich historischer Versionen. Keine Kunden-Mitnahmeliste. Effiziente signierte Deltas sind zulässig, müssen aber den vollständigen konsistenten Bestand nachweisbar herstellen; offline signierte/verschlüsselte Transportpakete ebenfalls.
- Zentraler Revisionsstand und pro-Schlüssel-Version trennen. Offline-Bearbeitungen als Vorschläge/Ereignisse, nicht ohne Konflikterkennung gegenseitig überschreiben. Signaturen, Rollback-/Replay-Schutz, unvollständige Delta-Reparatur, Atomic Commit und Revision-/Integritätsprüfung.
- Zentraler Widerruf sperrt **neue zentrale Freigaben** nach bekanntem Abgleich, **nicht** alte Kopien oder früher gelesene Schlüssel. Bei realem kompromittierten Schlüssel ist ein ausdrücklich geplanter Karten-/Leser-/SIPORT-Schlüsselwechsel notwendig.

### Phase 6 – Abnahme ohne unnötige Zwischentests

- **Ein gebündelter Entwicklungs-/Windows-Testmeilenstein** nach dem zusammenhängenden Feature; synthetisch mit zwei Windows-Benutzern, getrennten Geräten und mehreren Kunden.
- Nachweisen: komplette Offline-Bibliothek, neue Schlüsselversion, unveränderte andere Projekte, exakt gebundene Projektversion, Authentisierung/Rechte, Recovery, geänderte Gruppen, alte Offline-Kopien, Kopieren/SIPORT, Daten-/Hash-/Versionskonflikte, Verlust/Wiederherstellung, Race-Conditions, kein Secret in Projekt-JSON/Logs und verweigerter Build bei unauflösbarem Schlüssel.
- Karten-/Reader-/Runtime-Integration erst separat mit freigegebener Testhardware prüfen. Weder Github-Codeänderung noch kompilierter Test beweisen Hardwareabnahme.

## Nicht tun

- Keine zweite parallele Bedienoberfläche, Datenbank oder Verschlüsselungsimplementation.
- Keine Speichermigration einer nicht existierenden veröffentlichten SieEncode-Version.
- Keine Umstellung der bestehenden Runtime, bevor die Schlüsselauflösung bis zur temporären Payload synthetisch nachweisbar ist.
- Keine automatische Schlüsselrotation oder Diversifikation.
- Keine dauerhafte Internetverbindung, SAM-Pflicht oder Projekt-/Kunden-Auswahl zum Mitnehmen.
- Nicht alle Schlüssel bei jedem Start im Klartext laden; für die Suche genügen Metadaten, geheimwertige Nutzung bleibt gezielt.

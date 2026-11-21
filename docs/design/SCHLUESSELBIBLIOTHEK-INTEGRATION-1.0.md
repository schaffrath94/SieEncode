# SieEncode 1.0 – bestehende Vault-/Projekttechnik zur vollständigen Schlüsselbibliothek erweitern

**Umsetzungsplan vom 21.11.2026. Kein Rewrite. Erster Produktrelease 1.0 – keine veröffentlichte Altversion, keine Kunden-Altprojektmigration oder Zwischenrelease-Kompatibilität.** Alle Verwaltungsaktionen erfolgen ausschließlich in **SieEncode Studio**. Die Zentrale ist ein **headless** Daten-, Rechte- und Synchronisationsdienst **ohne eigene UI**. Der Metadatenvertrag, der verschlüsselte lokale Bibliotheksspeicherkern, die **geheimwertfreien Projektbindungen mit temporärer Schlüsselauflösung** und grundlegende **Schlüsselbearbeitungsoperationen** sind im Quellcode vorhanden, aber **noch nicht gebaut oder getestet**. Die **erste Projekteditor-Zuordnung** zu einer vorhandenen lokal entschlüsselbaren Bibliothek ist inzwischen über den bestehenden Schlüssel-Arbeitsschritt angeschlossen. **Schlüsselpflege/Neuanlage über Studio, zentrale Synchronisation, Klartext-Anzeige/Export, sichere Rollenfreigaben und produktive Build-/Stationsauflösung sind weiterhin offen.**

## Technischer Befund aus aktuellen Quellen

| Vorhandene Datei | Tatsächliche Rolle | Entscheidung |
|---|---|---|
| `ProjectModels.cs`, `ProjectJson.cs` | Projektentwurf, Profile, Transporteinstellungen, produktive Schlüsselplätze; `WithoutAllSecrets` entfernt Geheimwerte; projektgebundene Schlüsselreferenzen über `Project.KeyLibrary` | **Erweitert:** Bibliotheksbindungen sind möglich; Speichern und Laden verweigern gespeicherte Klartextwerte bei gebundenen Projekten |
| `ProjectKeyVault.cs` / `ProjectKeyVaultPayload` | Typisierte Schlüsselplätze, alternative Ausgangsschlüsselsätze, `Apply` hydratisiert ein Projekt für einen Lauf | Behalten als **bestehende Brücke zum Kodier-/Buildpfad** |
| `GroupVaultService.cs` | Authentisierte AES-GCM-Verschlüsselung und gruppen-/zertifikatgebundener Zugriff; sein Header ist derzeit **projektgebunden** (`ProjectId`) | Kryptografische Implementierung **gezielt wiederverwenden**, aber eine Bibliothek **nicht** stillschweigend als normales Kartenprojekt tarnen. Separaten, explizit zweckgebundenen Hüllen-/Dokumentvertrag vor Implementierung prüfen und testen |
| `ProjectGroupVault.cs` | Konkretes Projekt an Gruppe/Revision binden, geheimen Projekt-Payload verschlüsseln/laden | Als funktionierende Übergangslösung behalten; für neue kataloggebundene Projekte langfristig **nicht** zweite unabhängige dauerhafte Schlüsselquelle |
| `OfflineGroupTrustStore`, `GroupAdministrationWindow` | Signierte Richtlinien, Personen-/Geräte-/Gruppenzugänge, Recovery | Weiterverwenden und um Bibliotheksberechtigungen erweitern, nicht neu erfinden |
| `SecureOperationalSessionService`, `ProjectSecretPipeServer`, RFiDGear-Host | Temporärer Buildsitzungs-/Schlüsselpfad für vorhandene Kartenkodierung; eine Named Pipe kann **Klartextwerte** liefern | Behalten, nach späterer autorisierter Auflösung wie bisher aufrufen; keine Änderungen am Reader/Runtime ohne separaten Hardwaretest |
| `ProjectCatalog` | Such- und Statusliste vorhandener Projekte | Um Katalogbindung/Version und fehlende Schlüssel ergänzen, keine zweite Projektverwaltung |

## Zielbild ohne Doppelpflege

**Eine führende Schlüsselbibliothek, viele Referenzen.** Ein Geheimwert ist fachlich ein versioniertes `KeyId + Version` in genau einem zentral verwalteten Schlüsselsatz. Mehrere Projekte dürfen dieselbe Referenz benutzen; ein Kartenprojekt besitzt nicht mehr seine eigene unabhängige Kopie desselben produktiven Schlüssels.

```text
Zentraler SieEncode-Hintergrunddienst (autoritativ, OHNE eigene Oberfläche)
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
- Ein Projekt erhält eigene, eindeutig definierte Slot-Zuordnungen (PICC/Transport/Anwendungs-AID/Key-Nummer) zu `KeyId + Version`. Die **Kern-Slotnamen sind jetzt in `ProjectKeyLibraryResolution` festgelegt:** `transport/picc`, `transport/application/{KeyType}`, `productive/picc` und `productive/application/{AID}/{Index}`. Die Auswahl zusätzlicher Ausgangsschlüsselsätze und die grafische Auswahl werden in derselben SieEncode-Oberfläche integriert; keine Namens-Heuristik.
- **Keine automatische Umstellung auf die neueste Version.** Ein Wechsel ist eine geprüfte Projekt- und gegebenenfalls Karten-/SIPORT-Änderung, nicht ein gewöhnlicher Bibliothek-Sync.
- Diversifikation ist **ausdrücklich nicht** Teil von 1.0.

## Inkrementeller Ablauf – immer nur eine Umstellung zur Zeit

### Phase 1 – verlässliche IDs und Metadaten (erster Codebaustein vorhanden)

`src/RFiDGear.Builder.Core/KeyLibraryContracts.cs`: `KeyLibraryAddress`, `KeyLibraryEntry`, `KeyLibrarySnapshot` und `ProjectKeyLibraryBinding` als **nichtpersistenter, secrets-freier Fachvertrag**. Validiert Bibliothek-ID/Revision, eindeutige Versionen, Kunden-/Typkonsistenz, bekannte Referenzen und Betriebsstatus. Synthetische Tests unter `KeyLibraryContractTests`; erst zum gebündelten Testmeilenstein ausführen.

**Noch nicht:** Kunden-UI, zentraler Datentransport, Klartextschlüssel, Projekt-Formatänderung oder Runtime-Änderung.

### Phase 2 – verschlüsselte vollständige lokale Bibliothek (Kern implementiert, ungetestet)

- **Quellcode vorhanden:** `GroupVaultService` nutzt für beide Domänen dieselbe geprüfte Kryptografie, aber **zwei verschiedene authentisierte Formate** (`SieEncode-Group-Vault` und `SieEncode-Group-Key-Library-Vault`). Die Auswahl `GroupVaultPurpose` wird beim Öffnen erzwungen. Projekt-Vaults sollen nicht als Schlüsselbibliothek funktionieren und umgekehrt; keine zweite AES-Implementierung.
- **Quellcode vorhanden:** `KeyLibraryVault.cs` verschlüsselt einen vollständigen `KeyLibrarySnapshot` plus exakt einen realen Geheimwert je `KeyId + Version`, gebunden an die signierte Bibliotheks-ID, ausgewählte berechtigte Gruppe und separate Recovery-Zertifikate. Die Geheimwerte sind bewusst **nicht** im unverschlüsselten Index `KeyLibraryContracts` enthalten; das neue Vault-Format hat einen zusätzlichen geprüften internen Dokumenttyp.
- **Quellcode vorhanden:** `KeyLibraryVaultStore` erstellt einen neuen verschlüsselten Bibliothekssnapshot, lädt ihn für berechtigte Gruppenmitglieder oder über gesonderten Recovery-Zugang und aktualisiert ihn unter **optimistischer Dateirevision** sowie exakt um **eine** Bibliotheksrevision. Historische Schlüsselgenerationen können nicht gelöscht, unter derselben Version überschrieben oder unbemerkt reaktiviert werden; die bereits gespeicherte Kunden-/Typbindung ist unveränderlich. Geänderte signierte Gruppenpolitik darf **nicht** bei einem gewöhnlichen Schlüsselupdate automatisch Empfänger umwickeln.
- **Synthetische Testfälle vorbereitet, noch nicht ausgeführt:** Neuer `KeyLibraryVaultTests.Verify` ist in `tests/RFiDGear.Builder.Tests/Program.cs` registriert: vollständiger verschlüsselter Zwei-Kunden-Roundtrip, Gruppen-/Recovery-Rechte, Projekt-/Bibliotheksdomänentrennung, Datei-/Versionskonflikte, Manipulation, Historie, Rollenfehler und Empfängerpolitikänderung.
- **Noch offen vor einer echten Anwendung:** Das ist ein **isolierter Kernbaustein**, nicht bereits die vollständige Schlüsselverwaltung. Bislang wird **eine** berechtigte Bibliotheksgruppe mit vollständigem Entschlüsselungsrecht verwendet. Die technisch getrennte Verschlüsselung nach Kunden-/Mitarbeiterrechten, sichere Aufbewahrungs-ACL, Recovery-/Backup-Lebenszyklus, Empfängerabgleich nach Gruppenänderung, größere Bibliotheken (gegenwärtige Gruppenvault-Obergrenze: 16 MiB), echte UI-Anbindung, Datenbank/Synchronisation, Konfliktbehandlung und RFiDGear-Projektauflösung sind **nicht** implementiert oder nachgewiesen.
- Der Einbau bleibt **auf die vorhandenen Vault-, Zertifikats- und Gruppenfunktionen** begrenzt. Kein eigenständiger Krypto-Stack, kein Server-Admin-Client und keine separate Schlüsselverwaltung als zweites Programm. Vor Abschluss des nächsten zusammenhängenden Meilensteins **keine produktiven Kundenschlüssel** in dieser neuen Datenstruktur pflegen.



### Phase 3 – Projektreferenz und eindeutiger Resolver (Kern implementiert, noch nicht gebaut/getestet)

- **Vorhanden:** `ProjectSection.KeyLibrary` speichert optional eine **geheimwertfreie** `ProjectKeyLibrarySelection` mit `LibraryId`, Standard-`CustomerId` und einer exakten `ProjectKeyLibraryBinding` je Schlüsselplatz. **Verbundkarten aus mehreren Kundenprofilen** können die verantwortliche Kunden-ID pro Slot **ausdrücklich überschreiben**; ein solcher kundenspezifischer Zugriff wird nie aus ähnlichen Schlüsselnamen erraten. `ProjectKeyLibraryResolution.RequiredSlots` ermittelt die notwendigen PICC-, Transport- und AID-Keynummern aus den **tatsächlich gewählten Profilen und dem Kartenmodus**, nicht aus erkennbaren Hexwerten.
- **Vorhanden:** `ProjectKeyLibraryResolution.Bind` prüft Kunde, Bibliothek, Typ, aktive Version, Vollständigkeit der Belegung und Eindeutigkeit und entfernt bisherige geheime Projektwerte. Ein gleichzeitiger unabhängiger `Project.GroupVault` wird abgelehnt, damit keine zwei persistenten Geheimwertquellen entstehen.
- **Vorhanden:** `ProjectKeyLibraryResolution.ResolveForBuild` verlangt einen **bereits autorisiert entschlüsselten** Offline-Bibliotheksstand und eine vollständige *keyfreie* gespeicherte Definition. Er baut daraus eine **temporäre** `ProjectDefinition` mit den exakt referenzierten Schlüsseln. `ResolveTransientPayload` erzeugt daraus den **bestehenden** `ProjectKeyVaultPayload` zur Weiterverwendung von `ProjectKeyVault.Apply` und dem heutigen RFiDGear-Ausführungsweg. Keine automatische Versionserhöhung; falscher Kunde, fehlende Version, kompromittierter Schlüssel, Typ-/Slotkonflikt und versteckter Klartext führen zu einem Abbruch.
- **Vorhanden:** `ProjectJson.Load` und beide `ProjectJson.Save`-Wege verweigern bibliotheksgebundene Projektdefinitionen mit darin enthaltenen Klartextschlüsseln. Die temporär entschlüsselte Build-Definition darf **nicht** als gewöhnliche Projektdatei veröffentlicht werden.
- **Synthetische, noch nicht ausgeführte Tests:** `ProjectKeyLibraryResolutionTests.Verify` deckt die vollständige Slotbelegung, Bindung/JSON-Roundtrip, bestehendes `ProjectKeyVault.Apply`, falschen Kunden/Typ/Status, Dubletten, fehlende Slots, Klartext-Dateiablehnung und Quellprojekterhalt ab. Zusätzlich prüft `KeyLibraryVaultTests` die kryptografische Domänentrennung und den Einzelschlüssel-Resolver.
- **Teilweise angebunden:** Der Projekteditor kann über seinen bestehenden Schlüssel-Arbeitsschritt einen vorhandenen verschlüsselten lokalen Bibliotheks-Vault autorisiert für die **Metadaten-Auswahl** öffnen und vollständige Slot-Referenzen übernehmen. Er übergibt keine Rohschlüsselwerte an die gespeicherte Projektdefinition. Der Build bleibt für unaufgelöste Bibliotheksprojekte ausdrücklich gesperrt.
- **Noch nicht:** allgemeine Schlüsselpflege und Erstbereitstellung der Bibliothek im Studio, vollständige Fach-/Rollenfreigabe, tatsächliche kurzlebige Lauf-Payload-Auflösung aus dem Bibliotheks-Vault, Mehrfach-Ausgangsschlüsselsätze in derselben UI sowie vollständige Windows-/Runtime-/Hardwareprüfung. Daher ist die Projektbindung **noch kein benutzbarer Kodierweg**.
- Da noch **kein SieEncode-Release existiert**, dürfen wir das interne Projekt-/Vault-Schema für 1.0 konsolidieren. Während der laufenden Entwicklung können vorhandene Test-Drafts weiterhin geöffnet werden, soweit nützlich, aber **keine** zusätzliche veröffentlichte Altformat-Schnittstelle oder kundenbezogene Migration aufbauen. Die Umstellung erfolgt an einer einzigen geprüften Stelle; kein dauerhaftes Nebeneinander zweier unabhängiger Geheimwertspeicher.
- Für kataloggebundene Projekte **keinen** kompletten Projekt-Vault mit identischen Geheimwerten dauerhaft parallel pflegen. Fachlich führt nur die Bibliothek. In einem streng kontrollierten temporären Build-Schritt werden genau die gebundenen Werte aufgelöst und in den **vorhandenen** `ProjectKeyVaultPayload` projiziert. Dann `ProjectKeyVault.Apply` und die vorhandene Kodier-/Bauprüfung aufrufen.
- **Fail closed**: unbekannte KeyId, falsche Version, falscher Kunde/Keytyp, unberechtigter Benutzer, abgelöster/kompromittierter Schlüssel, inkonsistenter Bibliotheksstand oder nicht auflösbarer Slot **stoppt den Vorgang**. Kein automatischer Rückfall auf ältere feste Rohwerte.
- Die bestehende Bereitstellung der Rohwerte für RFiDGear ist für den beabsichtigten Offline-Kodiermodus weiterhin erforderlich. Temporäre Artefakte sind möglichst kurzlebig; **kein Versprechen rückwirkend widerrufbarer Klartextschlüssel**.

### Phase 3b – gemeinsame Schlüsselpflege als einziger Fachvertrag (Quellcode vorhanden, Tests vorbereitet)

- `KeyLibraryEditing` bietet reine In-Memory-Operationen für **Schlüssel importieren**, **sicher neu erzeugen**, **eine explizite neue Version hinzufügen** sowie **abgelöst/kompromittiert markieren**. Sie erzeugen jeweils eine neue Bibliotheksrevision und werden später über **dieselbe SieEncode-Oberfläche** sowie `KeyLibraryVaultStore.Update` gespeichert. Kein Rohwert in einer normalen Projektdatei.
- Eine neue Version verändert **nicht** automatisch die alte Schlüsselversion, Projekte, Karten, Reader oder SIPORT. Eine Statusänderung ist ein eigener, bewusster Vorgang; abgelöste und kompromittierte Werte werden nicht still wieder aktiviert. Die Import-, Versions- und Status-Tests `KeyLibraryEditingTests` sind im Test-Harness registriert, **nicht ausgeführt**.
- **Noch offen:** fachliche Freigabe-/Rollenprüfungen in Studio, sichere Zwischenablage/Anzeige, ein vollständig geplanter Karten-/Leserschlüsselwechsel und die tatsächliche zentrale Bestätigung.

### Phase 4 – eine verständliche Schlüssel- UND Gruppenverwaltungsansicht in SieEncode

**Teilstand (21.11.2026, Quellcode, ungetestet):** Im vorhandenen Projekteditor nutzt `KeyLibraryAssignmentWindow` den bereits vorhandenen Schlüssel-Arbeitsschritt; kein zusätzlicher Hauptfenster-Button. Die Auswahl findet vorhandene lokale verschlüsselte Bibliotheks-Vaults, öffnet sie erst nach authentisiertem signiertem Gruppenzugang über Windows-Zertifikate, zeigt nur aktive Metadaten und verlangt eine eindeutige, ausdrücklich ausgewählte `KeyId + Version` pro aus den aktuellen Profilen berechnetem Slot. Mehrkunden-Zuordnungen werden pro Slot festgehalten. Die Oberfläche übernimmt nur die geheimwertfreie `ProjectKeyLibrarySelection`; die entschlüsselten Schlüssel werden nicht an den Editor weitergegeben. Ein bereits bestehender unabhängiger Projekt-Vault verhindert den Wechsel am selben Pfad.

**Bewusste Zwischensperre:** Rohschlüssel-Felder, alte Vault-Aktionen und der bisherige Build sind für bibliotheksgebundene Projekte gesperrt, weil die produktive sichere Sitzungsauflösung noch nicht vollständig angeschlossen ist. Die `ProjectBuildService`-Einstiegsprüfung verhindert auch Mastering-only-Builds mit unaufgelösten Bibliotheksreferenzen. Das ist **noch keine produktive Kodierfunktion** und keine zentrale Schlüsselverwaltung. Der aktuell verschlüsselte Bibliothekskern hat nur dokumentweiten Gruppenzugang; kundenspezifische Entschlüsselungsrechte und editierende Rollen müssen vor einer Betriebsfreigabe erzwungen werden.

**Noch offen für Phase 4:** Eine reguläre Studio-Schlüsselverwaltung für initiales Erstellen, Importieren, Erzeugen, Versionieren, Anzeigen, Kopieren und SIPORT-Ausgabe sowie durchgängige Rollen-/Auditprüfung. Erst danach die geprüfte temporäre Auflösung in Build und Station integrieren.

- Hauptnavigation mit **einem** klaren Arbeitsbereich „Schlüsselverwaltung“ und vorhandener sinnvoll integrierter Gruppen-/Zugriffsverwaltung, nicht einem eigenen Knopf je Unterfunktion. **Alle** Kunden-/Schlüssel-/Rollen-/Gruppen-/Recovery-Aktionen finden in SieEncode statt. **Kein Verwaltungsportal am Server**.
- Kunden → Schlüsselsätze → Schlüsselversionen; Werte standardmäßig verdeckt.
- Berechtigte bewusste Aktionen: Schlüssel importieren, anlegen, anzeigen, kopieren, SIPORT-Ausgabe, Version kontrolliert wechseln. Exportformat und Integration mit SIPORT erst nach tatsächlichem Format-/Verwendungstest zusagen.
- Bestehende Projekteditoren können aus der Bibliothek wählen; keine zweite manuell gepflegte Schlüsselbezeichnungsliste.

### Phase 5 – zentraler headless Gesamt-Abgleich ohne Online-Zwang

- Jede autorisierte Vollzugriffs-Station synchronisiert **alle Kunden und alle Schlüssel** automatisch, einschließlich historischer Versionen. Keine Kunden-Mitnahmeliste. Effiziente signierte Deltas sind zulässig, müssen aber den vollständigen konsistenten Bestand nachweisbar herstellen; offline signierte/verschlüsselte Transportpakete ebenfalls.
- Zentraler Revisionsstand und pro-Schlüssel-Version trennen. **SieEncode Studio erzeugt** die authentisierten Änderungsbefehle aus seinen bestehenden Verwaltungsdialogen; der **headless Zentraldienst** prüft Berechtigung, Ausgangsrevision, Signatur, Schema und Konflikte und speichert atomar. Offline-Bearbeitungen sind lokal als **noch nicht zentral bestätigte Entwürfe** erkennbar, bis der Dienst sie annimmt. Keine stillschweigende Konfliktüberschreibung; Konfliktentscheidung wieder **im SieEncode Studio**, niemals in einer Server-UI. Signaturen, Rollback-/Replay-Schutz, unvollständige Delta-Reparatur, Atomic Commit und Revision-/Integritätsprüfung.
- Zentraler Widerruf sperrt **neue zentrale Freigaben** nach bekanntem Abgleich, **nicht** alte Kopien oder früher gelesene Schlüssel. Bei realem kompromittierten Schlüssel ist ein ausdrücklich geplanter Karten-/Leser-/SIPORT-Schlüsselwechsel notwendig.

### Phase 6 – Abnahme ohne unnötige Zwischentests

- **Ein gebündelter Entwicklungs-/Windows-Testmeilenstein** nach dem zusammenhängenden Feature; synthetisch mit zwei Windows-Benutzern, getrennten Geräten und mehreren Kunden.
- Nachweisen: komplette Offline-Bibliothek, neue Schlüsselversion, unveränderte andere Projekte, exakt gebundene Projektversion, Authentisierung/Rechte, Recovery, geänderte Gruppen, alte Offline-Kopien, Kopieren/SIPORT, Daten-/Hash-/Versionskonflikte, Verlust/Wiederherstellung, Race-Conditions, kein Secret in Projekt-JSON/Logs und verweigerter Build bei unauflösbarem Schlüssel.
- Karten-/Reader-/Runtime-Integration erst separat mit freigegebener Testhardware prüfen. Weder Github-Codeänderung noch kompilierter Test beweisen Hardwareabnahme.

## Nicht tun

- Keine zweite parallele Bedienoberfläche, keinen extra Server-Admin-Client, keine zweite fachlich unabhängig bearbeitete Datenbank oder Verschlüsselungsimplementation. Eine **zentrale Speicherreplik und ein lokaler Offline-Cache** sind gewollt, aber keine zwei voneinander unabhängigen Benutzeroberflächen/Wahrheiten.
- Keine Altformat-Speichermigration oder Kompatibilitätstests für eine nicht existierende veröffentlichte SieEncode-Version. Interne Entwicklungsdatenmodelle dürfen vor 1.0 gezielt konsolidiert werden; Kryptosicherheit, Recovery und korrekte Tests bleiben trotzdem erforderlich.
- Keine Umstellung der bestehenden Runtime, bevor die Schlüsselauflösung bis zur temporären Payload synthetisch nachweisbar ist.
- Keine automatische Schlüsselrotation oder Diversifikation.
- Keine dauerhafte Internetverbindung, SAM-Pflicht oder Projekt-/Kunden-Auswahl zum Mitnehmen.
- Nicht alle Schlüssel bei jedem Start im Klartext laden; für die Suche genügen Metadaten, geheimwertige Nutzung bleibt gezielt.

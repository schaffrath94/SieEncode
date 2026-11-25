# SieEncode – Headless HTTPS-Synchronisation (Entwicklungsvertrag 1.0)

**Stand 25.11.2026: Quellcode, noch kein Windows-/TLS-/Installations- oder Hardwaretest. Nicht produktiv bereitstellen.**

## Architektur und Bedienung

SieEncode Studio bleibt die **einzige** Verwaltungsoberfläche. In der bestehenden Schlüsselbibliotheks-Verwaltung wird einmalig die HTTPS-Zentrale sowie der **unabhängig bestätigte SHA256-Fingerabdruck des exakten DER-Serverzertifikats** eingegeben. Die lokale Einstellung liegt geschützt im Benutzerprofil. Danach versucht SieEncode beim Öffnen einer ausgewählten Schlüsselgruppe und nach Änderungen den Abgleich. Es gibt keinen zusätzlichen Hauptmenüknopf und keine Server-Admin-Webseite.

Der eigenständige Quellcode `src/RFiDGear.Builder.SyncServer` nutzt .NET 8/Kestrel, **ausschließlich HTTPS mit Client-Zertifikatspflicht (mTLS)**, TLS 1.2/1.3, keine Passwortanmeldung und kein unverschlüsseltes Fallback. Ein selbstsigniertes Mitarbeiterzertifikat wird **nicht pauschal** vertraut: sein exaktes öffentliches Zertifikat samt Fingerabdruck muss in einer von der bestätigten Hauptverwaltung signierten, aktuell geladenen Gruppenrichtlinie für die aktive Bibliotheksgruppe stehen. Das TLS-Protokoll prüft den Besitz des zugehörigen privaten Schlüssels. Nur neue persönliche SieEncode-Zertifikate mit DigitalSignature-KeyUsage sind für diesen Weg vorgesehen. Die konkrete TLS-Interoperabilität ist noch abzunehmen.

### Headless-API

| Methode und Pfad | Authentifizierung | Antwort |
| --- | --- | --- |
| `GET /v1/libraries/{libraryId}/head` | signiert registriertes Mitglied via mTLS | Bibliotheks-ID, Gruppenrichtlinien-Checkpoint, zentraler Revisionszähler, verschlüsselter Dateihash, signierte Katalogrevision |
| `POST /v1/libraries/{libraryId}/upload` | mTLS-Mitglied **plus** gültige Signatur des privaten Hauptverwaltungsschlüssels über exakten verschlüsselten Vault, Gruppe, Richtlinie und erwarteten Stand | neuer zentraler Stand oder Konflikt 409 |
| `POST /v1/libraries/{libraryId}/download` | mTLS-Mitglied **plus** kurzlebiger, privat signierter Download-Nachweis mit einmaliger ID, zentraler Revision und Hash | verschlüsselter Vault mit zugehörigem signierten Transportauftrag |

Die API besitzt **keinen** Endpunkt zum Anlegen von Nutzern, Entschlüsseln von Karten-/SQL-/DESFire-Schlüsseln, Ändern der Rechte oder Registrieren einer neuen Hauptverwaltung. Eine Remote-Anfrage darf sich nicht selbst zum Verwalter machen. Der Server verlangt eine **unabhängig bestätigte** lokale Provisionierung der signierten Verwaltungsidentität. Ein einmaliger Start ohne vorhandene Datendatei kann sie aus dem bereits von **SieEncode Studio** exportierten öffentlichen, signierten Gruppenpaket einrichten, **aber nur**, wenn der administrative Fingerabdruck separat in der Dienstkonfiguration bestätigt wurde. Besteht die Datendatei bereits, kann der Bootstrap **nicht** erneut registrieren oder die bisherige Autorität überschreiben. Der Installer und die geschützte Übernahme dieser einmaligen Einstellungen sind noch nicht umgesetzt.

Beim Download wird die signierte Anfrage-ID **vor Rückgabe der Chiffre** dauerhaft mit einem Dateirevisionsvergleich gespeichert. Wiederholte IDs werden auch nach einem Neustart abgewiesen. Nach Zeitablauf wird der Eintrag begrenzt aufbewahrt; eine zu alte Anfrage kann ohnehin nicht mehr erfolgreich signaturzeitlich geprüft werden. Der Server speichert keine Geheimwerte im Klartext.

## Server-Konfiguration (ausschließlich Entwicklungsvertrag)

Der Code erfordert folgende Umgebungsvariablen bei der Serverstartkonfiguration:

- `SIEENCODE_SYNC_LIBRARY_ID`: bestätigte Bibliotheks-GUID
- `SIEENCODE_SYNC_STATE_FILE`: zuvor sicher provisionierte, nur für den Dienst beschreibbare zentrale Datendatei
- `SIEENCODE_SYNC_TLS_PFX`: Pfad des Serverzertifikats mit privatem Schlüssel, ausschließlich mit geeigneten ACLs
- `SIEENCODE_SYNC_TLS_PASSWORD`: Kennwort des PFX, durch den endgültigen Installer durch geschützte Zertifikatsverwaltung zu ersetzen
- `SIEENCODE_SYNC_PORT`: expliziter HTTPS-Port (1024–65535)
- **Nur beim ersten Start ohne Datendatei**: `SIEENCODE_SYNC_BOOTSTRAP_ENROLLMENT_FILE` zeigt auf ein öffentliches, in SieEncode Studio exportiertes signiertes Gruppenpaket.
- **Nur beim ersten Start ohne Datendatei**: `SIEENCODE_SYNC_BOOTSTRAP_ADMIN_FINGERPRINT` muss den über einen unabhängigen Kanal bestätigten vollständigen SHA256 des öffentlichen Verwaltungsschlüssels enthalten. Nicht automatisch aus demselben Paket übernehmen.

Der aktuelle Code lauscht mit Kestrel auf der gewählten Netzwerkschnittstelle. **Firewall, restriktive Dienstidentität, Backup-/Restore-Ablauf, TLS-Serverzertifikatserneuerung, Sperrung unnötiger Netzwerkzugriffe und Windows-Service-Installation sind noch nicht abgenommen.** Beim Bootstrap müssen sowohl die exportierte Paketdatei als auch der unabhängig bestätigte Fingerabdruck vorliegen; die lokale Serverkonfiguration benötigt geschützte ACLs. Diese Bootstrap-Funktion ist keine zusätzliche Benutzungs-/Verwaltungsoberfläche. Kein öffentliches Deployment oder Produktivnetz ohne diese Schutzmaßnahmen und synthetische/Windows-Akzeptanz.

## Gemeinsamer HTTPS-Transportvertrag

Studio und Server verwenden denselben strikten PascalCase-JSON-Codec (`KeyLibrarySyncWire`); die ASP.NET-Web-Defaults dürfen keine abweichende camelCase-Antwort erzeugen. Doppelte und unbekannte Felder werden abgewiesen. Die inneren signierten Claim-, Signatur- und Chiffrebytes werden unverändert übertragen. Größenlimits gelten auch beim Streaming ohne Content-Length (16 KiB Head-Antwort, 64 KiB Download-Anfrage, 32 MiB verschlüsselter JSON-Transport); der Client begrenzt den vollständigen Abruf einschließlich Antwortkörper auf zwei Minuten. TLS 1.2/1.3 und der bestätigte Zertifikatspin bleiben verpflichtend. Die synthetischen Codec-Regressionen sind registriert, **nicht ausgeführt**. Ein tatsächlicher Kestrel-/Studio-mTLS-Rundlauf ist weiterhin offen.

## Offline-Lebenszyklus

1. Bei fehlender Verbindung bleibt der bestehende verschlüsselte lokale Vault benutzbar. Ohne erreichte und überprüfte zentrale Annahme erscheint keine zentrale Freigabemeldung.
2. Ist die Zentrale neuer, muss der verschlüsselte Download sowohl die bestätigte Serverrevision als auch die tatsächliche entschlüsselte Katalogrevision erfüllen. Bestehende KeyId/Version-Geheimwerte und historische Kundenzuordnungen dürfen nicht verschwinden; sonst **Konflikt**, kein automatischer Ersatz.
3. Ist die lokale Bibliothek neuer, prüft der Client zuerst die vollständige zentral bekannte Historie, bevor der private Hauptverwaltungsschlüssel den verschlüsselten Upload signiert. Ein bloß berechtigter Leser darf nicht hochladen.
4. Bei gleicher logischer Revision und verschiedenem Chiffre-Hash wird der gesamte entschlüsselte Nutzinhalt verglichen; ein Unterschied führt zu **Konflikt**.
5. Ein geschützter lokaler Cursor merkt sich den zuletzt beobachteten zentralen Katalog-/Serverstand. Alte oder widersprüchliche Head-Antworten werden abgelehnt. Vor einem bestätigten Download wird die neue Revision im Cursor gesichert.
6. Zusätzlich hält derselbe geschützte Cursor den **letzten tatsächlich erfolgreich abgeglichenen** zentralen Head und den exakten lokalen Chiffre-Dateihash. Eine beobachtete neuere Revision oder ein Konflikt ersetzt diese Basis ausdrücklich nicht. Automatischer Upload auf eine nichtleere Zentrale erfordert genau den zuletzt gemeinsam abgeglichenen Head. Automatischer Download über einen vorhandenen lokalen Vault erfordert dessen unveränderten Abgleichhash. Bei unabhängigen Offline-Änderungen oder fehlender Basis wird der Bestand nicht überschrieben; ein Erstabgleich gleicher Inhalte darf eine Basis herstellen. Bei laufenden Dateiänderungen vor dem Upload wird abgebrochen; eine spätere Änderung nach dem gesendeten Snapshot bleibt als Offline-Änderung erhalten. Diese konservative Schranke verhindert blindes Zusammenführen, ersetzt jedoch keine manuelle Konfliktauflösung oder echte Drei-Wege-Zusammenführung.

**Grenze:** Die lokale Cursor-Datei ist gegen zufälliges Wiederherstellen eines älteren Server-Backups hilfreich, aber keine von der Zentrale signierte oder extern unveränderlich verankerte Quittung. Werden die Daten eines Windows-Benutzerprofils **einschließlich aller geschützten Merker** gemeinsam zurückgesetzt, ist dieser Schutz nicht ausreichend. Für 1.0 sind unabhängig signierte Quittungen, Audit und Restore-/Recovery-Abnahme notwendig.

## Richtlinien- und Empfängerwechsel (Kern, noch nicht per HTTPS freigegeben)

Der neue Kern `KeyLibraryPolicyTransition` und `CentralKeyLibraryStore.AcceptPolicyTransition` akzeptiert eine **neuere** signierte Personen-/Gruppenrichtlinie unter derselben unabhängig bestätigten Hauptverwaltung zusammen mit dem passenden neu verschlüsselten Gesamt-Vault in **einem** atomaren CAS. Der signierte Auftrag bindet die bisherige zentrale Revision und deren exakten Chiffre-Hash, dieselbe Bibliothek und Gruppe, den neuen Richtliniencheckpoint und die neue Chiffre. Die logische Katalogrevision bleibt unverändert; normale Uploads dürfen diese Sonderoperation nicht ersetzen. Ein Verwaltungswurzelwechsel, Gruppenwechsel, initialer leerer Server oder ein gleichzeitiger Katalogedit sind damit ausdrücklich nicht erlaubt.

`OfflineGroupAdministrationStore.PrepareKeyLibraryPolicyTransition` benutzt den vorhandenen geschützten Verwaltungsschlüssel, den **noch lokal bestätigten alten** Gruppenstand, den exakt dazu passenden zentral signierten Vorgänger und ein weiterhin berechtigtes privates persönliches Zertifikat. Nach ausdrücklicher Bestätigung entschlüsselt der Studio-Kern den Vorgänger, erzeugt einen vollständigen neuen AES-GCM-Vault mit frischem Inhaltsschlüssel und der neuen Empfängerliste, öffnet ihn probeweise mit demselben persönlichen Zugang und vergleicht den gesamten Katalog. Vorbereitung ändert weder lokalen Vault noch lokales Vertrauen noch den zentralen Bestand. Der Server prüft die signierte Bindung und den Empfängerkreis, nicht den Klartext. Er kann die Inhaltsgleichheit nicht unabhängig nachweisen.

**Noch nicht mit der vorhandenen Oberfläche oder dem HTTP-Host verbunden.** Die bisherige Gruppenverwaltung übernimmt den lokal signierten Gruppenstand unmittelbar in die eigene Registrierung. Das darf nicht unbesehen mit der neuen vorbereitenden Operation kombiniert werden. Vor einer nutzbaren Integration müssen der alte Vertrauensstand bis zur bestätigten zentralen Annahme gesichert, unabhängige Offline-Katalogänderungen abgeglichen, ein bestätigter zentraler Wechsel lokal crash-sicher übernommen, Cursor-Policy-Übergänge explizit autorisiert und Fehler/Retry/Neustart behandelt werden. Bis dahin bleibt der bestehende HTTPS-API-Vertrag unverändert und der reguläre Sync sperrt abweichende Richtlinien. Keine neuen Server-Adminfunktionen und keine produktive Bereitstellung.

Synthetische Regression `KeyLibraryPolicyTransitionTests` registriert, **nicht kompiliert oder ausgeführt**: unveränderte Kataloghistorie, hinzugefügte/entzogene Empfänger, Recovery, explizite Bestätigung, falsche Wurzel/Signatur/CAS, Replay, Neustart und die unvermeidliche weiterhin entschlüsselbare historische Offline-Kopie. Ein entzogener Empfänger darf künftig keine neuen zentralen Downloads erhalten; das macht früher kopierte Kartenschlüssel nicht ungültig.

## Bewusst noch nicht abgeschlossen

Diese Version enthält einen **Quellcodehost und den Studio-Anschluss**, aber keinen gestarteten oder auf Windows überprüften Dienst. Ausstehend sind: gebündelter .NET/WPF/Server-Build, mTLS-Integrationstests, IIS/Service-/Firewall-/ACL-Bereitstellung, vorab sicherer Trust-Bootstrap, serverseitige Auditkette, signierte Serverquittungen, vollständige Studio-/HTTPS-Integration und lokale Wiederaufnahme der Richtlinien-/Empfängerwechsel sowie kundenspezifische Entschlüsselungsgruppen. Der Headless-Server kann den Inhalt eines AES-GCM-Vaults bewusst **nicht** entschlüsseln oder die tatsächlichen Schlüsselwerte unabhängig prüfen; er prüft den signierten Chiffretransport und bindet seine Revision. Die berechtigte Studio-Seite prüft die entschlüsselte Historie. Offline eingesehene oder kopierte Schlüssel bleiben auch nach späterem Gruppenwiderruf bekannt. Änderungen an realen Karten/Lesern/SIPORT erfolgen nicht automatisch.

Nächste Abnahme: `Test-CI.ps1` auf Windows führt den synthetischen Regression-Harness aus und baut nun auch das Serverprojekt. Netzwerk/TLS-Zertifikate, Zwei-Rechner-Verbindung und Restore-Szenarien sind darüber hinaus separat mit **synthetischen Schlüsseln** zu testen. Keine reale Karte/kein produktives Kundengeheimnis ohne gesonderte Freigabe.

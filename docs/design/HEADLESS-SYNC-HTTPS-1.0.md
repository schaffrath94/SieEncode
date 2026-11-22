# SieEncode – Headless HTTPS-Synchronisation (Entwicklungsvertrag 1.0)

**Stand 22.11.2026: Quellcode, noch kein Windows-/TLS-/Installations- oder Hardwaretest. Nicht produktiv bereitstellen.**

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

## Offline-Lebenszyklus

1. Bei fehlender Verbindung bleibt der bestehende verschlüsselte lokale Vault benutzbar. Ohne erreichte und überprüfte zentrale Annahme erscheint keine zentrale Freigabemeldung.
2. Ist die Zentrale neuer, muss der verschlüsselte Download sowohl die bestätigte Serverrevision als auch die tatsächliche entschlüsselte Katalogrevision erfüllen. Bestehende KeyId/Version-Geheimwerte und historische Kundenzuordnungen dürfen nicht verschwinden; sonst **Konflikt**, kein automatischer Ersatz.
3. Ist die lokale Bibliothek neuer, prüft der Client zuerst die vollständige zentral bekannte Historie, bevor der private Hauptverwaltungsschlüssel den verschlüsselten Upload signiert. Ein bloß berechtigter Leser darf nicht hochladen.
4. Bei gleicher logischer Revision und verschiedenem Chiffre-Hash wird der gesamte entschlüsselte Nutzinhalt verglichen; ein Unterschied führt zu **Konflikt**.
5. Ein geschützter lokaler Cursor merkt sich den zuletzt bestätigten zentralen Katalog-/Serverstand. Alte oder widersprüchliche Head-Antworten werden abgelehnt. Vor einem bestätigten Download wird die neue Revision im Cursor gesichert.

**Grenze:** Die lokale Cursor-Datei ist gegen zufälliges Wiederherstellen eines älteren Server-Backups hilfreich, aber keine von der Zentrale signierte oder extern unveränderlich verankerte Quittung. Werden die Daten eines Windows-Benutzerprofils **einschließlich aller geschützten Merker** gemeinsam zurückgesetzt, ist dieser Schutz nicht ausreichend. Für 1.0 sind unabhängig signierte Quittungen, Audit und Restore-/Recovery-Abnahme notwendig.

## Bewusst noch nicht abgeschlossen

Diese Version enthält einen **Quellcodehost und den Studio-Anschluss**, aber keinen gestarteten oder auf Windows überprüften Dienst. Ausstehend sind: gebündelter .NET/WPF/Server-Build, mTLS-Integrationstests, IIS/Service-/Firewall-/ACL-Bereitstellung, vorab sicherer Trust-Bootstrap, serverseitige Auditkette, signierte Serverquittungen, sichere zentrale Richtlinien-/Empfängerwechsel und kundenspezifische Entschlüsselungsgruppen. Der Headless-Server kann den Inhalt eines AES-GCM-Vaults bewusst **nicht** entschlüsseln oder die tatsächlichen Schlüsselwerte unabhängig prüfen; er prüft den signierten Chiffretransport und bindet seine Revision. Die berechtigte Studio-Seite prüft die entschlüsselte Historie. Offline eingesehene oder kopierte Schlüssel bleiben auch nach späterem Gruppenwiderruf bekannt. Änderungen an realen Karten/Lesern/SIPORT erfolgen nicht automatisch.

Nächste Abnahme: `Test-CI.ps1` auf Windows führt den synthetischen Regression-Harness aus und baut nun auch das Serverprojekt. Netzwerk/TLS-Zertifikate, Zwei-Rechner-Verbindung und Restore-Szenarien sind darüber hinaus separat mit **synthetischen Schlüsseln** zu testen. Keine reale Karte/kein produktives Kundengeheimnis ohne gesonderte Freigabe.

# Option: SieTask als zentraler SieEncode-Dienst

Status: Architekturvorschlag vom 26.11.2026; keine Änderung an SieTask, keine Freigabe einer produktiven Integration. Die bisherige eigenständige headless Referenzimplementierung bleibt für lokale Entwicklung und unabhängige Offline-Nutzung erhalten.

## Aufteilung

- **SieTask:** möglicher zentraler Konnektor / API-Host, organisatorische Benutzer- und Rollenverwaltung, Freigabeanforderungen, verschlüsselte Bibliotheksstände, Revisions-/Konfliktprüfung, Audit, Betriebsüberwachung. Eine Verwaltungsansicht in SieTask kann organisatorische Aktionen vorbereiten und Status anzeigen, aber nicht ohne zusätzliche kryptografische Autorität signierte Kundenzugänge ändern.
- **SieEncode Studio:** Offline-Katalog, lokale geschützte Geräte- und Verwaltungsidentitäten, kryptografische Freigabe und Neuverschlüsselung, Schlüsselverwaltung und bewusst autorisierte Klartextnutzung, Projekt- und Runtime-Auflösung. Keine erzwungene Serververbindung beim Kodieren.
- **Grenze:** Unternehmensanmeldung (z. B. Entra ID) authentifiziert ein organisatorisches Konto, nicht automatisch den privaten Geräte- oder Verwaltungsschlüssel. Rollen aus SieTask dürfen keine RSA-/AES-Berechtigung vortäuschen. Der Server braucht keinen DESFire-Klartext und keine privaten Offline-Identitäten. Ein vollständiger Verwaltungswechsel auf den Server wäre eine neue Vertrauensentscheidung und ist nicht Bestandteil dieses Vorschlags.

## Wiederverwendbarer Vertrag

Das neue Interface `IKeyLibrarySyncTransport` kapselt Kopfstand, Upload, Download, Snapshot samt Richtlinie, Richtlinienwechsel und Audit (Meldung/Abfrage). Die existierende Implementierung `KeyLibraryHttpsTransport` bleibt unverändert bezüglich TLS-Pin und persönlichem mTLS-Zugang. Eine künftige SieTask-Implementierung muss die exakten signierten Bytefolgen und dieselbe CAS-Semantik beibehalten. Keine direkte Datenbankintegration aus Studio.

| Operation | Vertrag | Zentrale Prüfung |
|---|---|---|
| Head | GET /v1/libraries/{id}/head | Authentifizierter Zugriff, aktueller Revisionsstand |
| Upload | POST .../upload | Signatur, Bibliothek, Richtlinie, exakter vorheriger Hash und Revision |
| Download | POST .../download | Signierter gerätegebundener Nachweis, aktuelle Berechtigung, Replay-Schutz |
| Snapshot | POST .../snapshot | Wie Download, zusätzlich konsistenter signierter Richtlinienstand |
| Policy | POST .../policy-transition | Signierte neue Richtlinie und verschlüsselter Bestand als atomarer Wechsel; scoped Übergänge noch nicht freigegeben |
| Audit | POST .../audit | Gerätesignatur, idempotente Ereignis-ID, kein Schlüsselwert |
| Audit review | POST .../audit-review | Aktuelle Hauptverwaltungsfreigabe, gerätegebundene Abfrage, begrenzte Seiten |

Ein SieTask-Host kann diese Verträge direkt implementieren oder an den vorhandenen Dienst weiterleiten. Eine Proxy-Weiterleitung der TLS-Identität benötigt einen gesondert geprüften vertrauenswürdigen Kanal; frei gesetzte HTTP-Header dürfen niemals Geräteauthentifizierung ersetzen. Kein automatischer Wechsel der gepinnten Vertrauenswurzel bei Wechsel des Hosts. Kein Zugriffstoken oder private Zertifikate in Projektdateien. Das eigentliche Rollen-/Freigabemodell, API-Basisadresse, Provisionierung, Unternehmensrichtlinien und Akzeptanztests müssen mit der SieTask-Umgebung abgestimmt werden.

## Implementierungsreihenfolge

1. Bestehenden kryptografischen und Offline-Workflow einschließlich Kundenbereiche, Wiederaufnahme und Windows-Abnahme abschließen.
2. Versionierten dienstneutralen Vertrag mit synthetischen Interoperabilitätstests absichern.
3. SieTask-Konnektor und organisatorische Verwaltungsansicht in der zugelassenen internen Entwicklungsumgebung entwerfen; keine privaten Schlüssel, Kundenbestände oder Zugangsdaten in das SieTask- oder SieEncode-Repository übernehmen.
4. Erst nach überprüfter Autorisierung, CAS, Restore, Offline-Entzug, Audit und Freigabe der betrieblichen Verantwortlichen den Referenzhost optional durch SieTask ersetzen.

Der aktuelle Code enthält keine SieTask-URL, keinen Entra-Anmeldedialog, keinen internen API-Client und keine produktive Verbindung.

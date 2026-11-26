# SieEncode-Zentrale auf bestehendem SieTask AWS Fargate – 1.0

## Verbindliche Zielplattform (Nutzerentscheidung, 26.11.2026)

Die Zentrale muss auf der **vorhandenen SieTask-AWS-Fargate-Plattform** mitbetrieben werden. Kein eigener Windows-Server und keine externe Hosting-Plattform voraussetzen. SieEncode Studio bleibt das einzige fachliche Verwaltungsprogramm; SieTask kann Plattform, Authentifizierung und später Konnektor bereitstellen. Keine Kopplung der DESFire-Klarwerte an SieTask, keine Runtime-/RFiDGear-Migration. Der vorhandene nicht frei konfigurierbare Entra-Login-Sidecar ist eine zu respektierende Zugangsgrenze. Konkrete ECS/VPC/Ingress-/Speicherrechte sind noch nicht bestätigt; kein neuer Zugang am Sidecar vorbei ohne ausdrückliche Infrastrukturfreigabe. Kein AWS-Deployment.

## Getrennte Lebenszyklen auf derselben Plattform

Bevorzugt eigener headless Linux-Container im bestehenden SieTask-ECS/Fargate-Umfeld, sofern die Plattform einen zusätzlichen Container/Service zulässt. Ein eigener ECS-Service ist wegen getrenntem Update-/Wiederanlauf und eigener Schreibberechtigung sinnvoll, aber **keine vorausgesetzte Berechtigung und kein anderes Hosting**. Wenn ausschließlich der bestehende SieTask-Task oder die bestehende HTTPS-API erweiterbar ist, muss der Integrationspfad daran angepasst werden. Keine zweite fachlich editierbare Schlüsselbibliothek.

Der SyncServer erwartet echtes Ende-zu-Ende-mTLS bis Kestrel und einen separat gepinnten Serverzertifikat-DER-Hash. Er prüft das echte TLS-Clientzertifikat gegen die signierte Benutzer-/Geräteliste und bindet Download-/Audit-/Upload-Autorisierung daran. Eine ALB-/Entra-Sidecar-TLS-Terminierung verändert diese Vertrauensgrenze. Ein gewöhnlicher Reverse Proxy, Header wie X-Client-Cert oder eine Browser-Login-Weiterleitung sind **kein** Ersatz für diesen Nachweis. Keine Header-basierte Identität oder Lockerung der TLS-Prüfung in diesem Commit.

### Ingress-Entscheidung vor produktiver Integration

1. Bevorzugte Plattformintegration: vorhandene SieTask-HTTPS-Adresse und obligatorische Entra-Grenze beibehalten, sobald ein unterstützter Desktop/API-Authentifizierungsweg und die gesicherte Weitergabe persönlicher Geräteidentität geklärt sind. Gegebenenfalls eigener signierter Request-Envelope mit Challenge/Nonce, Tokenprüfung, Audience/Issuer, Benutzer-/Gerätebindung und Replay-Schutz. Fachliche Verwaltungs-/Vault-Signaturen bleiben zusätzlich verpflichtend. Dies ist ein zu prüfender Entwurf, kein implementiertes Authentifizierungsverfahren.
2. Wenn durch die SieTask-Infrastrukturbetreiber ausdrücklich zugelassen: transparenter TCP-Ingress (z.B. NLB TCP-Listener, TCP-IP-Target) bis zur bestehenden mTLS-Anwendung; keine TLS-Terminierung am Ingress. Nur wenn das mit der verpflichtenden Entra-Zugangsregel vereinbar ist. Keine alternative öffentliche Route als Umgehung des Sidecars.

Die bestehende SieTask-ALB-/Sidecar-Konfiguration, Domain, Entra-App-Registrierung, Token-/API-Fähigkeiten und Netzfreigaben müssen aus dem internen SieTask-Projekt geklärt werden. Dieses Repository enthält keine Infrastruktur-Zugangsdaten.

## Dauerhafter Speicher und Single Writer

Fargate-Container-Dateisystem und Task-Ephemeral-Storage sind **kein** Zentralzustand. Für die bestehende dateibasierte Implementierung ist ein verschlüsseltes EFS-Volume mit eigenem Access Point, IAM- und Transitverschlüsselung eine mögliche AWS-native Persistenz. Keine EFS-Verfügbarkeit unterstellen. Falls SieTask nur eine vorhandene relationale Datenbank oder Objektablage bereitstellt, ist ein separater transaktionaler Storage-Adapter zu planen; kein blindes Umhängen der Dateizugriffe auf einen S3-Pfad. Keine ungeprüfte parallele Datenbankmigration.

Vorgesehenes EFS-Layout: dedizierter Access Point an /var/lib/sieencode, darunter live/central-state.json, zugehörige .audit-Daten und Locks. UID/GID des nicht privilegierten Containerusers (derzeit .NET app UID 1654) bei Access Point und Berechtigungen abgleichen. Nur SyncServer-Task-Role erhält Zugriff. Keine TLS-Private-Keys auf dem gemeinsam genutzten Zustandsvolume. Verschlüsselung im Ruhezustand, EFS-IAM-Autorisierung, TLS beim Mount, Netzwerkregeln für NFS und Backup nach internen Vorgaben.

Solange das vorhandene Dateiprotokoll verwendet wird: **desiredCount=1, keine automatische horizontale Skalierung**, ECS Rolling Update minimumHealthyPercent=0, maximumPercent=100, keine Blue/Green- oder Canary-Doppelinstanz. Wartungsunterbrechung für Synchronisation akzeptabel; Studio arbeitet offline weiter. Die lokale FileShare/CAS-Lease ist kein belegtes rechnerübergreifendes Fencing. Vor Freigabe mit zwei Fargate-Tasks auf demselben Volume und tatsächlichem Prozessabbruch prüfen, dass Locks, Rename, Flush, Wiederanlauf und atomare CAS-Ersetzung wie erwartet funktionieren. Falls nicht nachweisbar, nicht produktiv betreiben; transaktionales Storage-/Lease-Design erforderlich. ECS-Scheduler allein ist keine Split-Brain-Sicherung.

## Implementierte Containergrundlage

Vom Repository-Root aus:
```sh
docker build -f src/RFiDGear.Builder.SyncServer/Dockerfile -t sieencode-sync:dev .
```

Multi-stage .NET 8, nur Core und SyncServer, kein Windows-Studio, keine Runtime, keine produktiven Dateien im Build-Kontext. global.json bleibt maßgeblich; Basisimage-Digests und Verfügbarkeit des exakten SDK-Patches im freigegebenen CI prüfen und pinnen. Nicht privilegierter Container. Standardprofil Fargate, Port 8443. Startprüfung verlangt Linux, festen Zustandspfad, ein genau dort vorgesehenes NFS/EFS-Mount am Volume-Root und bereits vorhandenen Zustand. Kein stiller Bootstrap, kein Restore bei regulärem Task-Start. Mountprüfung ist ein Schutz gegen versehentliche flüchtige Ablage, kein Nachweis von IAM, korrektem EFS Access Point oder Fencing. Normales bestehendes lokales Hosting bleibt für Tests erhalten.

Konfiguration (nur Platzhalter, keine Secrets committen):
- SIEENCODE_SYNC_LIBRARY_ID: bestätigte Bibliotheks-ID.
- SIEENCODE_SYNC_TLS_PFX **oder** SIEENCODE_SYNC_TLS_PFX_BASE64: TLS-Zertifikat mit privatem Schlüssel, entweder geschützt als Datei oder aus der freigegebenen Secret-Injektion; nicht gleichzeitig. Base64 begrenzt auf 65536 Zeichen, decodiertes Material nach Import überschrieben; Umgebungsvariable bleibt im Prozesskontext. ECS-Secrets-Injektion benötigt entsprechende Rechte und gesicherten Task-Zugriff, keine Ausgabe in Logs.
- SIEENCODE_SYNC_TLS_PASSWORD: separat aus Secret-Management.
- SIEENCODE_SYNC_HOSTING=fargate, SIEENCODE_SYNC_START_MODE=existing, SIEENCODE_SYNC_STATE_FILE=/var/lib/sieencode/live/central-state.json, SIEENCODE_SYNC_PORT=8443.
- Healthcheck erst nach erfolgreicher Startprüfung: TCP-Port bei direktem Ingress; kein anonymer Schlüssel-/Status-Endpunkt und keine mTLS-Ausnahme. TCP-Check nur Liveness/Erreichbarkeit, kein fachlicher Bereitschaftsnachweis.

Erstprovisionierung und kalte Wiederherstellung benötigen einen separaten expliziten Wartungsvorgang auf dem persistenten Volume, während der normale ECS-Dienst vollständig gestoppt ist. Bestehende Bootstrap-/Restore-Signaturgates bleiben bestehen. Konkreter Maintenance-Task, EFS-/AWS-Backup-Integration und Epochenwechsel noch offen. Keine automatische Erstellung eines neuen Zustands durch Fargate-Restart.

## Abnahme vor Produktivbetrieb

- Linux-Container-Build im genehmigten SieTask-CI, Quellcode-/Windows-Regressionen, non-root, Secret-Injektion, kein Zustand ohne tatsächliches Volume.
- Entscheidung über zulässigen Ingress/Entra-Anbindung und persönliche Geräteauthentifizierung; negative Auth-/Replay-/Proxy-Tests, keine Entra-Umgehung.
- EFS Access Point, IAM, ACL, Verschlüsselung, zwei konkurrierende Tasks, Task-Replacement, Prozessabbruch, Wiederanlauf und externe Backup-Referenzen.
- Bestätigung von Bibliothek/Serverzustand, Backup/Restore, tatsächlich stillgelegter alter Task, Client-Vertrauenswechsel und Live-HTTPS-Mehrplatztest.
- Kein Rollout auf bestehendes SieTask, keine AWS-Ressourcen erstellt, keine Hardwareaktion. Docker/SDK hier nicht ausgeführt. Nur Containergrundlage und Zielvertrag.

AWS-Referenzen: [EFS im ECS-Task](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/specify-efs-config.html), [ECS Service Deployment](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/deployment-type-ecs.html), [NLB Listener](https://docs.aws.amazon.com/elasticloadbalancing/latest/network/load-balancer-listeners.html), [ALB mTLS](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/mutual-authentication.html), [EFS Locking-Grenzen](https://docs.aws.amazon.com/efs/latest/ug/limits.html).

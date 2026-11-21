# Offline-Vault: Berechtigungsentzug und Sicherheitsgrenzen für SieEncode 1.0

**Stand: Entwicklungsentscheidung, 21.11.2026. Kein Produktiv-Release und kein geprüfter Hardware-Schutz.**

## Kernaussage

Vollständig offline verteilte, zuvor für ein Zertifikat verschlüsselte Kartenschlüssel sind **nicht rückwirkend widerrufbar**. Ein Mitarbeiter mit dem eigenen privaten Zertifikat, altem signierten Gruppenstand und einer zuvor kopierten Vault-Datei kann die alte Datei weiter entschlüsseln, auch nachdem die Hauptverwaltung die Person aus der aktuellen Gruppe entfernt hat. **Der Import einer neueren Richtlinie auf dem Arbeitsplatz des Mitarbeiters ist keine wirksame Fernlöschung alter Kopien.**

Die Verschlüsselung des *aktuellen* Projekt-Vaults mit einem neuen zufälligen Inhaltsschlüssel nach Entfernung eines Empfängers verhindert nur, dass dieser Empfänger die **neue aktuelle Vault-Datei** öffnet. Bereits bekannte, darin enthaltene **DESFire-Kartenschlüssel bleiben unverändert**, sofern sie nicht auf Karten und Lesern/SAMs ausdrücklich rotiert wurden.

## Drei technisch getrennte Sicherheitsstufen

| Änderung | Was schützt sie? | Was schützt sie **nicht**? |
|---|---|---|
| Neue signierte Gruppenrevision | Autorisierung bei Verwendung des aktuellen lokalen Gruppenstands | Abgekoppelte alte Stände, selbst kontrollierte alte Software |
| Empfängerabgleich / frischer Vault-Inhaltsschlüssel | Neue aktuelle Vault-Datei nur für aktuell freigegebene Empfänger | Historische ciphertext-Kopien mit alten Empfängern; bereits bekannte Kartenschlüssel |
| Re-Keying betroffener DESFire-Karten und zuständiger Leser/SAMs | Wirklicher Entzug *alter Kartenschlüssel* nach vollständiger erfolgreicher Umstellung | Aufbewahrte Datenkenntnis, nicht umgestellte Karten/Leser, andere Systeme mit demselben Schlüssel |

## In 1.0 unmittelbar umgesetzte Risikominderung

- Die Hauptverwaltung erkennt **Entfernung aus einer Gruppe, Personendeaktivierung, Gerätedeaktivierung und Gruppenlöschung** als Sicherheitsentzug. Die UI verlangt vor dem Signieren bewusste Bestätigung und warnt vor alten Offline-Kopien und erforderlicher Karten-/Leserschlüsselrotation. Die Warnung bleibt auch nach dem Export der neuen öffentlichen Registrierung sichtbar.
- Im Projekteditor weist der Gruppen-Vault-Empfängerabgleich bei entfernten Mitgliedern darauf hin, dass nur der **aktuelle** Vault umverschlüsselt wird und **keine** Kartenschlüsselrotation erfolgt.
- `GroupVaultReconciliation` speichert historische Ciphertext-Stände nicht mehr automatisch in `<Projektordner>/GroupVaultBackups`, sondern per Default unter `LocalApplicationData/SieEncode-Private-Vault-History/<Bibliothek>/<Projekt>/<Hash>.group-vault.json` des **ausführenden Windows-Benutzers**. Der Backup-Root lässt sich im API-Test explizit übergeben; ein Ziel **innerhalb des Projektordners** wird abgelehnt. **Achtung:** Auch diese Datei ist weiterhin mit alten Empfängern entschlüsselbar. Dateisystemrechte, Benutzerkontenschutz, Backup-Retention und ggf. getrennte Administratorverwahrung müssen organisatorisch geprüft werden. Ein lokaler Pfad beweist keine sichere ACL.
- Der aktuelle Quellstand enthält synthetische Regressionen für Entzugs-Erkennung, getrennte Backup-Ablage, unveränderten Projektstand bei Konflikt sowie die **fortbestehende Lesbarkeit eines historischen Vaults** mit alter gültiger Richtlinie. Noch kein WPF-/Testlauf in diesem Paket.

## Offen vor einer Zusage wie „sofortiger Entzug“

Ein **rein offline** verfügbarer ehemaliger Nutzer mit einer kopierten Datei und dem damals gültigen privaten Schlüssel kann nicht zentral gezwungen werden, eine neuere Sperrliste einzuspielen. Auch eine lokale Anwendungssperre oder ein Datum im signierten Stand kann unter Kontrolle des Nutzers bzw. durch einen alten Dateischnappschuss umgangen werden. Der Vertrauensanker muss **außerhalb** des frei kopierbaren Bestands liegen.

**Architekturoptionen:**

1. **Weiterhin völlig offline, Best-Effort-Entzug:** Nur aktuelle, regelmäßig synchronisierte Vaults sind geschützt. Bei ernstem Mitarbeiteraustritt/Schlüsselkompromittierung ist ein **kontrollierter Re-Key** der betroffenen Karten- und Leserlandschaft erforderlich. Eine sofortige Fernsperre ist nicht versprechbar. Für 1.0 darf der Dialog daher niemals `Zugriff vollständig widerrufen` oder `Schlüssel gesperrt` suggerieren.
2. **Zeitlich begrenzte Offline-Autorisierungen mit geschütztem, nicht rollbackbarem Gerät:** Freigaben laufen nach kurzer, festgelegter Dauer ab; ein Hardwaregerät mit geschützter Uhr/monotonem Zähler kann alte Richtlinien ablehnen. **Kein sofortiger Entzug ohne erneuten Kontakt**, und nur wenn sämtliche Schlüsseloperationen auf dem Gerät bleiben. Ein exportierbarer Vault oder bekannter DESFire-Masterschlüssel umgeht das Ziel.
3. **Online-KMS / zentral erreichbare Schlüsseloperationen pro Kartenlauf:** Echte Berechtigungsprüfung vor jeder neuen Schlüsselverwendung, aktuelle Entzüge auf der zentralen Stelle durchsetzbar. Erfordert eine Verbindung oder einen entsprechend abgesicherten dedizierten Dienst; nicht kompatibel mit uneingeschränkt beliebig langer Offline-Nutzung.
4. **SAM/HSM-basierte Schlüsselverwendung und kartenspezifische Ableitung:** Keine ungeschützte Verteilung eines Master-Geheimnisses an alle PCs; gut zur Schadensbegrenzung und für per-card diversifizierte Schlüssel. **Ein rein offline befindliches, noch autorisiertes Gerät kann trotzdem nicht aus der Ferne sofort entzogen werden**, sofern es keine zuverlässige kurzlebige Autorisierung oder keinen Kontaktzwang gibt.

### Konsequenz für den ersten Release-Gate

Vor dem Marketing oder der Kundenfreigabe muss entschieden werden, welche Sicherheitsbehauptung SieEncode 1.0 tragen soll:

- **Offline-Projektverwaltung mit offen benannten Entzugsgrenzen**: entspricht dem jetzigen Design, nach echten Sicherheits- und Hardwaretests.
- **Sofortige/wirksame Sperre von früher berechtigten Mitarbeitern und Kopien**: Das heutige reine Offline-Vault-Modell **erfüllt diese Forderung nicht**. Dafür muss die Schlüsselausgabe und -verwendung architektonisch neu begrenzt werden, statt nur mehr Buttons oder Policy-Meldungen einzubauen.

Keine automatische Rotation der DESFire-Kartenschlüssel aus einem Vault-Abgleich ableiten. Re-Keying ist eine gesonderte sicherheitskritische, vollständige Standort-/Karten-/Lesermigration mit verifizierten Ergebnissen.

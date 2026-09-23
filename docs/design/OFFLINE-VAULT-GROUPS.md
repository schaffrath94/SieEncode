# Offline-Vaults mit zentral verwalteten Gruppen

Status: Entwicklung auf codex/offline-vault-groups, nicht Teil von 1.2.1.
Keine produktiven Vaults laut Benutzer. Testdateien und Identitaeten bleiben erhalten.

## Aktueller Delegationsbaustein

Die signierte v2-Richtlinie bindet Vollmacht-ID, Person, separaten oeffentlichen
RSA-Signierschluessel, erlaubte Gruppen-IDs und Aktivstatus. Gruppenverwalter
koennen ausschliesslich Namen und Mitgliedschaften vorhandener zugewiesener
Gruppen aendern. Personen, Geraete, Vollmachten, neue Gruppen und Recovery sind
kein Bestandteil dieses Aenderungsformats. Verwaltungsschluessel duerfen keine
Entschluesselungsschluessel aus dem Personenverzeichnis wiederverwenden.

Jede delegierte Aenderung ist RSA-PSS-signiert und an Bibliothek, Vollmacht und
den exakten vorherigen Richtlinien-Checkpoint (Revision plus SHA256) gebunden.
Der aktuelle vertrauenswuerdige Stand bestimmt die Berechtigung; eine mitgelieferte
Behauptung oder ein alter Rollenstand wird nicht verwendet. Deaktivierte Personen
und Vollmachten sind abgewiesen, ebenso fremde Gruppen, Wiedereinspielungen und
uneindeutige JSON-Felder. Ein abgelehnter Import veraendert den lokalen Stand nicht.

OfflineGroupAdministrationStore kann eine verifizierte Aenderung atomar in eine
neue hauptsignierte Richtlinie uebernehmen. Konkurrierender Entzug gewinnt gegen
einen alten Schreibversuch. Dieses Core-Verfahren ist noch nicht der fertige
delegierte Bedienablauf: lokale geschuetzte Delegatenschluessel, gefuehrte Vergabe,
Transport ueber die gemeinsame Ablage und Rollenanzeige fehlen weiterhin.
Aktuell muss die Hauptverwaltung einen delegierten Aenderungsdatensatz einlesen;
unabhaengige Offline-Veroeffentlichung durch Gruppenverwalter ist damit noch
nicht implementiert. Keine Wirkung auf vorhandene Vaults oder alte Kopien.

## Ziel und Vertrauensgrenzen

Personen koennen in mehreren Gruppen sein (Lokal, Lokal 2, Lokal 3) und mehrere
Geraeteidentitaeten besitzen. Projekte werden explizit einer Gruppe zugeordnet.
Administratoren verwalten Personen und Gruppen; delegierte Verantwortliche nur
ihre zugewiesenen Gruppen. Normale Nutzer entschluesseln offline.

Die gemeinsame Dateiablage transportiert Daten, ist aber keine Vertrauenswurzel.
Ein Administrator-Schluessel wird bei der Erstregistrierung ueber einen separat
bestaetigten Fingerabdruck gebunden. Schluessel aus einer geladenen Richtlinie
werden niemals automatisch vertrauenswuerdig. Verwaltungs- und Entschluesselungs-
schluessel bleiben getrennt. Keine gemeinsame private Identitaet je Gruppe.

Signierte Richtlinien binden Bibliotheks-ID, Version, Gruppen und Identitaets-
Fingerabdruecke. Projekt-ID, Gruppenzuordnung und Vaultinhalt brauchen zusaetzlich
authentisierte Bindungen. Eine reine Gruppenliste erzwingt noch keine Vaultrechte.
Mitgliederaenderungen erfordern kontrolliertes Neuverpacken bzw. bei Entzug neue
Inhaltsschluessel. Fuer einen administrativen Abgleich benoetigt der Ausfuehrende
auch den entsprechenden Entschluesselungs-/Recoveryzugang; Signierrecht allein
gewaehrt keinen Zugriff auf Schluessel.

Offline sind vorhandene entschluesselbare Kopien nicht rueckrufbar. Berechtigte
Nutzer koennen gelesene Schluessel weitergeben. Ein manipuliertes Programm oder
ein lokaler Administrator liegt ausserhalb der Durchsetzung durch die normale UI.
Ein lokaler Hoechststand erkennt bekannte Rueckspruenge, aber keinen global
neuesten Stand, keine eingefrorene Ablage und kein Zuruecksetzen des ganzen PCs.
Diese Grenzen werden angezeigt statt als sofortige Sperrung ausgegeben.

## Implementierungsfolge

1. Signierte, streng validierte Gruppenrichtlinie mit expliziter Vertrauenswurzel,
   Bibliotheksbindung, Mindestversion und Konflikterkennung gleicher Version.
2. Sichere Erstregistrierung und persistenter lokaler Vertrauens-/Versionsstand;
   Administratorwechsel und signierte, gruppenbegrenzte Delegationen.
3. Personen-/Geraeteverzeichnis, Gruppenverwaltung und nachvollziehbare Aenderungen.
4. Authentisierte Projektzuordnung, neue Vault-Anbindung und kontrollierter
   Empfaengerabgleich. Separater zertifikatsbasierter Recoveryzugang.
5. Integration in Arbeitsplatz-Einrichtung, Offline-Status und Abnahmematrix.

## Erster implementierter Baustein

OfflineGroupPolicyService signiert die exakten JSON-Nutzbytes mittels RSA-PSS/
SHA-256 (RSA mindestens 3072 Bit). Der Empfaenger liefert den extern bestaetigten
oeffentlichen Verwaltungsschluessel; keine Schluesselannahme aus der Nachricht.
Unbekannte Felder, doppelte JSON-Felder, doppelte Gruppen/Mitglieder, falsche
Bibliothek, bekannte alte Version und abweichender Inhalt derselben Version
werden abgewiesen. Eingabegroesse ist begrenzt. Gruppen duerfen sich ueberlappen.

Dieser erste Baustein allein umfasst keine App-Anbindung, Delegation oder Persistenz.
Bestehende Vaults werden nicht geaendert.
Der Baustein ist keine fertige Zugriffskontrolle und kein neues Vaultformat.

## Lokaler Vertrauensspeicher (zweiter Baustein)

OfflineGroupTrustStore verlangt bei der Registrierung den unabhaengig bestaetigten
SHA-256-Fingerabdruck des RSA-SPKI-Verwaltungsschluessels. Erstregistrierung kann
eine bestehende Datei nicht ersetzen. Updates verwenden ausschliesslich die
bereits registrierte Vertrauenswurzel und den lokal gespeicherten Hoechststand.
Die letzte signierte Richtlinie bleibt offline verfuegbar und wird bei jedem
Laden erneut geprueft. Speichern ist atomar mit Konflikterkennung anhand des
tatsaechlich gelesenen Dateisnapshots. Lesefehler fuehren nicht zur Neuregistrierung.

Die verpflichtende Schnittstelle ILocalGroupTrustProtection hat keinen Klartext-
Fallback. Tests nutzen einen rein synthetischen authentisierten Protector.
WindowsGroupTrustProtection in der App-Schicht nutzt nun DPAPI CurrentUser mit
bibliotheksspezifischer Anwendungsbindung. Der UI-Smoke prueft einen echten
Windows-Roundtrip, erneutes Laden, falsche Bibliotheksbindung und manipulierte
Datei in einem isolierten Testordner. Es werden keine Windows-Zertifikate erzeugt.
Dies ist keine Zweitbenutzer-/Zweitrechnerabnahme. AppData-Pfad und gefuehrte
Erstregistrierung sind noch anzubinden; noch keine produktive Vault-Anbindung.
Ein komplettes Zuruecksetzen/Loeschen des lokalen Zustands durch dessen Besitzer
oder einen Administrator bleibt ausserhalb der Ruecksprungerkennung.

## Gefuehrte Erstregistrierung

Einstellungen enthalten einen ausdruecklich als Entwicklung gekennzeichneten
Einstieg. GroupEnrollmentPackages prueft Format/Groesse, doppelte/unbekannte Felder,
SPKI-Schluessel, Richtliniensignatur und Bibliotheksbindung. Das belegt nur
Konsistenz, noch kein Vertrauen. GroupEnrollmentWindow verlangt den unabhaengig
erhaltenen Fingerabdruck und Bestaetigung der Bibliotheks-ID. Der Kandidat wird
beim Oeffnen als private Kopie eingefroren. Ohne Bestaetigung/falscher Fingerabdruck
kein Schreibzugriff; bestehende Registrierung wird nicht ersetzt.

Normaler Speicherort: LocalApplicationData/SieEncode/GroupTrust/<LibraryId>.trust.bin.
Der isolierte Startup-/UI-Testmodus injiziert einen getrennten Testpfad. Die neue
UI-Regressionspruefung registriert ausschliesslich synthetische Daten in Temp.
Native Dateiauswahl/visuelle Abnahme noch offen. Erstellung einer neuen Verwaltung,
Delegation, Gruppen-/Personenverwaltung und Projekt-Vault-Anbindung folgen noch.

## Lokaler Verwaltungsschluessel

OfflineGroupAdministrationStore erzeugt einen separaten RSA-3072-Signierschluessel
und eine anfangs leere signierte Richtlinie. Privater PKCS8-Schluessel und Stand
werden zusammen authentisiert lokal gespeichert. Windows verwendet einen anderen
DPAPI-Zweck als fuer den lesenden Vertrauenscache. Der oeffentliche Export enthaelt
nur SPKI und signierte Richtlinie und ist direkt als Registrierung transportierbar.
Gruppenaktualisierung verlangt den gelesenen Richtlinien-Checkpoint und eine
unveraenderte Datei; die neue Revision wird automatisch erhoeht. Bestehende
Verwaltung kann nicht durch Create ersetzt werden. Kein globaler Rueckrollschutz
gegen einen Besitzer, der die komplette lokale Datei zuruecksetzt.

Synthetischer Windows-Test: anlegen, ueberlappende Gruppen signieren, erneut laden,
oeffentlichen Export verifizieren, falschen DPAPI-Zweck/veralteten Checkpoint und
erneutes Create ablehnen, unveraenderte Datei nach Ablehnung pruefen.
Noch kein Verwaltungsdialog, keine Delegation und keine Sicherung/Wiederherstellung
des Verwaltungsschluessels. Testschluessel werden nur im isolierten Temp angelegt.

## Verwaltungsdialog (Entwicklungsstand)

GroupAdministrationWindow ist in Einstellungen erreichbar. Lokale Verwaltungen
werden aus dem isolierbaren Administrations-Unterordner gewaehlt; Anlegen erzeugt
eine neue Bibliotheks-ID. Gruppen haben stabile IDs und bearbeitbare Namen.
Oeffentliche Empfaengereinladungen werden erst nach Fingerabdruckbestaetigung
einer Gruppe hinzugefuegt. Dieselbe Identitaet darf in mehreren Gruppen stehen.
Signiertes Speichern verwendet den beim Laden gemerkten Checkpoint. Export bietet
nur den gespeicherten oeffentlichen Stand an, nicht ungespeicherte Entwuerfe.
Schliessen/Wechsel mit offenen Aenderungen verlangt eine Verwerfbestaetigung.

Synthetischer UI-Test: Erstellen, Gruppe per Button, Mitglieder, signiertes
Speichern/Neuladen, externer Versionsfortschritt und Ablehnung des alten Entwurfs.
Native Datei-/Bestaetigungsdialoge und visuelle Abnahme stehen noch aus. Die
Personen-/Geraetezuordnung ist noch nicht vorhanden; aktuell Fingerabdruecke.
Keine gruppenbasierte Vault-Anbindung, Delegation oder Admin-Recovery behauptet.

## Personen und Geraete (Richtlinienversion 2)

Signierte Richtlinien v2 enthalten Personen mit stabiler ID, Anzeigename und
aktivem/inaktivem Zustand sowie deren oeffentliche Geraetezertifikate. Gruppen
referenzieren Personen-IDs. Eine Person kann mehrere Geraete und Mitgliedschaften
haben. Jedes Zertifikat darf genau einer Person gehoeren; Fingerabdruck und
oeffentliches RSA-Zertifikat muessen zusammenpassen. PKCS12-/Private-Key-Archive
werden bereits vor Zertifikatsimport abgewiesen. Nur eindeutiges DER akzeptiert.

ResolveRecipients loest eine explizite Gruppe in aktive Geraete auf. Unbekannte
Gruppen/Personen und Gruppen ohne aktive Empfaenger werden nicht still ersetzt.
Aktive Zertifikate muessen zum uebergebenen Zeitpunkt gueltig sein. Das Lesen
historischer signierter Richtlinien bleibt auch nach Zertifikatsablauf moeglich.
Der Aufrufer muss zuerst die Signatur/Vertrauenswurzel pruefen; Aufloesen allein
ist keine Berechtigungspruefung und noch keine Vault-Anbindung.

UpdateDirectory schreibt Personen und Gruppen gemeinsam als signierte v2-Revision.
Bestehende v1-Teststaende bleiben lesbar; keine automatische Umdeutung von
Zertifikats-Fingerabdruecken zu Personen-IDs. Personenpflege in der UI folgt noch.
Core-Test deckt die gewuenschte Acht-Personen-/Drei-Gruppen-Matrix ab, inklusive
zweitem Geraet einer Person, Deaktivierung, doppelten Zuordnungen, falschen
Fingerabdruecken, unbekannten Mitgliedern und Zeitgueltigkeit.

## Personenpflege im Verwaltungsdialog

Personen und Geraete haben eigene Tabellen mit bearbeitbaren Namen und Aktivstatus.
Geraete werden einer ausgewaehlten Person ueber eine bestaetigte oeffentliche
Einladung zugeordnet; doppelte Geraetezuordnung wird abgewiesen. Gruppen zeigen
Personennamen und erlauben Mehrfachmitgliedschaften. Speichern signiert Personen,
Geraete und Gruppen gemeinsam als v2. Tabellenbearbeitung wird vorher explizit
abgeschlossen; Export verweigert offene Entwuerfe.

Leere neue Verwaltungen verwenden das Personenmodell. Alte v1-Testgruppen werden
nicht still umgedeutet: Umstellung erst nach bestaetigter vollstaendiger Zuordnung
aller bisherigen Geraete zu Personen; Fehler laesst den Entwurf unveraendert.
Der gemeinsame Einladungsimport weist jetzt ebenfalls Private-Key-Archive vor
dem Zertifikatsimport ab und gibt Zertifikate bei Validierungsfehlern frei.

Synthetische UI-Abnahme: Person mit zwei Geraeten, doppelte Zuordnung abgewiesen,
signiertes v2-Verzeichnis nach Neuladen aufloesbar, veralteter Entwurf abgewiesen.
Native Import-/Umstellungsdialoge und visuelle Pruefung weiterhin offen.

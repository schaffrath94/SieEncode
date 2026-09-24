# Offline-Vaults mit zentral verwalteten Gruppen

Status: Entwicklung auf codex/offline-vault-groups, nicht Teil von 1.2.1.
Keine produktiven Vaults laut Benutzer. Testdateien und Identitaeten bleiben erhalten.

## Portable private Sicherungen (Core, Bedienoberflaeche noch offen)

GroupPrivateKeyBackups kapselt PKCS8-Schluessel und bei Recovery das oeffentliche
Zertifikat in einem strikt versionierten, begrenzten und authentisiert
verschluesselten Format. Bestehendes AES-256-GCM/PBKDF2-SHA256-Verfahren mit
600000 Iterationen; neue Passphrasen unterliegen den bestehenden Staerkepruefungen.
Bibliothek, Zweck und gegebenenfalls Personen-ID liegen im verschluesselten Inhalt.
Import verlangt einen extern bestimmten Fingerabdruck. Der Export entschluesselt
seine Sicherung und prueft eine private RSA-PSS-Operation vor Rueckgabe.
Private Bytepuffer werden geloescht; fuer verwaltete JSON-Strings ist keine
vollstaendige Speicherloeschung zugesichert.

Hauptverwalter-Restore benoetigt eine zuvor bestaetigte lokale Registrierung
und uebernimmt deren aktuellen Richtlinienstand statt eines Standes aus dem Backup.
Gruppenverwalter-Restore verlangt aktive Person und passende aktive Vollmacht.
Beide schreiben nur neue lokal geschuetzte Dateien, niemals ueber vorhandene.
Vor dem Schreiben wird der registrierte Stand erneut geprueft; dies ist keine
globale Transaktion ueber Vertrauens- und Schluesseldatei und keine Aussage ueber
noch nicht empfangene Offline-Aenderungen. Eine alte private Sicherung kann nicht
rueckgerufen werden. Private Backups gehoeren nicht in den gemeinsamen Projektordner.

Synthetische CI prueft echte Signatur-/Entschluesselungsoperationen, Rollen- und
Kontextverwechslung, Manipulation, Entzug und konkurrierende Richtlinienaktualisierung.
Gefuehrte Dateiablage, separate Recovery-Erzeugung vor nicht-exportierbarer
Windows-Schluesselpersistenz und native Wiederherstellungsdialoge bleiben offen.

## Zentrale Freigabe der Recovery-Identitaeten

Die Hauptverwaltung zeigt Recovery-Zugaenge mit Name, Aktivstatus und Fingerabdruck.
Oeffentliche Empfaengereinladungen werden erst nach manuell eingegebenem,
unabhaengigem Fingerabdruck als bibliotheksweite Recovery-Zugaenge aufgenommen.
GrantRecovery verifiziert das oeffentliche Zertifikat, sperrt doppelte Eintraege
und schreibt ueber den vorhandenen signierten/konfliktgeprueften Verwaltungsweg.
Die Richtlinienpruefung erzwingt getrennte Schluessel gegen normale Geraete,
Delegaten und Hauptverwaltung. Gruppenverwalter besitzen keine Recovery-Schreibrechte.

Entziehen/Reaktivieren benoetigt eine ausdrueckliche Bestaetigung und erzeugt eine
neue signierte Revision. Ohne aktiven Recovery-Zugang sind neue Gruppen-Vaults
und Rotation gesperrt. Verteilen des neuen Standes und Abgleich vorhandener Vaults
bleiben erforderlich; alte Kopien bleiben zugaenglich. Die Oberflaeche weist auf
separate private Sicherung/Wiederherstellungspruefung hin. Deren gefuehrte Erzeugung
und portable Sicherung sind weiterhin offen; eine Einladung ist kein Schluesselbackup.

## Separater Recovery-Bedienweg im Editor

Gruppenprojekte zeigen eine eigene Taste zum Oeffnen mit Recovery-Schluessel.
Es gibt keinen stillen Wechsel bei fehlendem normalen Zugang. Die erfolgreiche
Entsperrung kennzeichnet die Sitzung als Recovery, verwendet diesen Zugang fuer
Schluesselspeichern/Empfaengerabgleich/Pruefung und sperrt operative Builds.
Die Kodierstation bleibt ausschliesslich beim normalen Gruppenzugang. Erst ein
erfolgreiches ausdrueckliches normales Entsperren beendet den Recovery-Modus;
Projektneuladen verwirft den Sitzungszustand.

Der App-Test verwendet getrennte synthetische Identitaeten und echte Button-Events:
Recovery-Identitaet kann nicht normal oeffnen, Recovery-Speichern erhaelt beide
Zugangswege, Audit nennt den richtigen Zugangsweg, Build bleibt gesperrt und
normales Entsperren beendet den Sondermodus. Kein Zertifikatsspeichereingriff.
Erzeugung/Import/Freigabe dieser separaten Identitaeten und deren portable
private Sicherung sind noch gefuehrt zu integrieren.

## Editor und sichere Stationsbereitstellung

Vorhandene gebundene Gruppenprojekte sind jetzt im Editor ueber die persoenliche
Identitaet entsperrbar und mit typisierter Projekt-/Gruppenpruefung speicherbar.
Die Freigabetaste bietet fuer diese Projekte den bestaetigten Empfaengerabgleich.
Normale Integritaetspruefung ist getrennt von einem noch einzubindenden separaten
Recovery-Bedienweg beschriftet. Die Sicherungsanweisung benennt Gruppenregistrierung
und private Geraete-/Recovery-Schluessel als Voraussetzungen.

Der Katalog leitet Gruppenprojekte in den sicheren Stationspfad. Die interne
Schutzstufe 4 dient nur dieser UI-/Katalogklassifizierung; das Gruppen-Dateiformat
bleibt Version 1. Beschaedigte/fehlende Vaults gebundener Projekte duerfen nicht
auf alte unverschluesselte Builds zurueckfallen. Stationsbereitstellung prueft
die registrierte Richtlinie und Vaultbindung erneut und materialisiert lokal.
Ein Build aus entsperrter Editorsitzung prueft den Zugang nochmals; nach bekanntem
Entzug wird die Sitzungsverifikation verworfen und der Build gesperrt.

App-Smoke: tatsaechliche Entsperr-/Speicher-Buttons, lokale Stationsbereitstellung
ohne Readeraufruf, Entzug und gesperrter Altbuild-Fallback. Noch offen sind gefuehrte
Erstellung/Migration, Recovery-Schluesselverwaltung und deren eigener Editorweg,
native Dialogabnahme sowie echte Mehrbenutzer-/Rechnerabnahme.

## Projektidentitaet und typisierte Schluesselspeicherung (Core)

Neue Projektentwuerfe erhalten eine stabile GUID. Alte Projektdateien ohne ID
bleiben beim Laden/Speichern unveraendert; erst Bind erzeugt bei ausdruecklicher
Gruppenzuordnung eine ID auf einer Projektkopie. Project.GroupVault enthaelt die
registrierte Bibliothek und Gruppen-ID. Wiederholtes Binden derselben Gruppe
erhaelt die ID, eine andere Gruppe erfordert einen noch separaten kontrollierten
Umzuordnungsablauf. Projektmetadaten alleine sind keine Freigabe: beim Entsperren
muessen sie exakt zum authentisierten Vault-Header passen.

ProjectGroupVault bindet die vorhandenen ProjectKeyVaultPayload-Daten ueber einen
strikten versionierten Nutzdatenumschlag an das Gruppenformat. Erstellen prueft
vor dem Schreiben, dass der konkrete normale oder Recovery-Zugang den neuen Vault
wieder oeffnen kann. Die Datei nutzt den bisherigen abgeleiteten Vaultpfad, aber
Create ersetzt niemals eine vorhandene Datei oder migriert einen Altvault still.
Laden prueft Bindung, Zugang und Nutzdatentyp. Aktualisieren authentisiert den
alten Vault, prueft den erwarteten Dateistand und sichert verschluesselt vor der
Neuschreibung. Geaenderte Empfaenger sperren normales Schluesselspeichern, bis
zuvor der separate kontrollierte Abgleich erfolgt ist.

Schluesselfreie Projektkopien und ProjectKeyVault.Apply erhalten ID/Zuordnung.
Die Core-API schreibt den Vault und die Projektdatei bewusst nicht als angeblich
atomare Zweidateitransaktion. Editor muss Erstellung/Migration, Speichern unter,
Entsperren und Recovery konsistent orchestrieren; diese Oberflaechenanbindung und
Altvault-Migration sind noch offen. Noch keine neue Release-Freigabe.

## Kontrollierter Empfaengerabgleich (Core)

GroupVaultReconciliation.Preview authentisiert den vorhandenen Vault mit einem
aktuell freigegebenen normalen oder ausdruecklichen Recovery-Zugang. Sie zeigt
hinzukommende/entfernte Rollen und Fingerabdruecke gegen den registrierten Stand,
ohne Dateien zu schreiben. Ein neuer Empfaenger ohne bisherigen Zugang kann den
alten Vault nicht selbst umschluesseln. Der Plan bindet exakte Quelldatei und
Richtliniencheckpoint; frei vom Aufrufer gelieferte Empfaengerlisten gibt es nicht.

Apply prueft Quelldatei und Richtlinienstand erneut, entschluesselt authentisiert,
verschluesselt mit frischem Inhaltsschluessel und erhaelt vor dem Austausch eine
gepruefte bytegleiche verschluesselte Sicherung in GroupVaultBackups. Vorhandene
abweichende Sicherungen werden nicht ersetzt. Der neue Vault wird ueber den
bestehenden Dateischreiber mit Anwendungslock/Tempdatei gespeichert; der Schreiber
prueft nun auch nach dem Tempdateischreiben erneut auf Fremdaenderungen.

Dies ist keine verteilte Transaktion mit Richtliniencache und Cloud-Synchronisation:
der neue Vault dokumentiert den konkret angewendeten Richtlinienstand. Spaetere
Updates erfordern erneuten Abgleich. Nicht kooperierende Fremdschreiber unmittelbar
zwischen letzter Pruefung und Rename sind durch den lokalen Anwendungslock nicht
global ausgeschlossen. Alte Sicherungen bleiben mit alten Schluesseln zugaenglich.
Bedienvorschau/Einbindung in Projektverwaltung stehen noch aus.

## Gruppen-Vault-Kern und separate Recovery

Neues getrenntes Format SieEncode-Group-Vault (Version 1), noch ohne Anbindung
an Projektdateien/Editor. Bestehende v2/v3-Vaults und deren Funktionen bleiben
unveraendert. AES-256-GCM authentisiert Projekt-ID, Bibliothek, Gruppen-ID,
Richtlinien-Checkpoint, zufaellige Schluesselgeneration und saemtliche Rollen/
Fingerabdruecke/RSA-OAEP-SHA256-Schluesselhuellen gemeinsam mit dem Nutzinhalt.
Ein Austausch von Metadaten, Huellen oder Nutzdaten schlaegt bei der Pruefung fehl.

Neue Vaults erhalten ausschliesslich die aktiven gueltigen Geraete der expliziten
Gruppe plus zentral signierte separate Recovery-Geraete. RecoveryDevices sind
Bestandteil der Hauptverwaltungsrichtlinie, nicht delegiert aenderbar. Recovery
erfordert andere Schluessel als normale Geraete und Delegaten; auch der
Hauptverwaltungsschluessel darf weder Delegaten- noch Entschluesselungsschluessel
sein. Mindestens ein aktiver Recovery-Zugang ist fuer neue Gruppen-Vaults Pflicht.
Keine pro Projekt einzugebende Recovery-Passphrase in diesem neuen Format.

Entschluesseln verlangt einen verifizierten lokalen Richtlinienstand und die
vom Aufrufer erwartete stabile Projekt-/Gruppenidentitaet. Normale und Recovery-
Zugaenge sind explizit getrennt. Neuere Vault-Richtlinienstaende erfordern zuerst
ein lokales Update. Nach bekanntem Entzug gibt der normale Client keinen Zugriff
mehr frei. Separate aktive Recovery kann auch nach Entfernung der alten Gruppe
retten. Abgelaufene Zertifikate bleiben zum Lesen alter Daten verwendbar; fuer
neue Verschluesselung muessen aktive Zertifikate zeitlich gueltig sein.

Jeder Protect-Aufruf erzeugt einen frischen zufaelligen Inhaltsschluessel. Der
Test weist frische Schluessel bei geaenderter Mitgliedschaft nach. Ein alter Vault
mit alter Richtlinie bleibt mit seinem alten privaten Schluessel entschluesselbar.
Die AEAD-Bindung ist kein unabhaengiger Herkunftsnachweis gegen berechtigte
Schluesselbesitzer; diese koennen Klartext/Schluessel weitergeben. Der Aufrufer muss
die Projektidentitaet korrekt verwalten und echte Vertrauensregistrierung nutzen.

Noch offen: stabile Projekt-ID/Zuordnungsmodell, Editor-/Stationsintegration,
Recovery-Einrichtung und -Sicherung, kontrollierter atomarer Empfaengerabgleich
und Rotation realer Projektdateien. Die Core-Tests sind ausschliesslich synthetisch.

## Arbeitsplatz fuer Gruppenverwalter

Einstellungen enthaelt einen getrennten Gruppenverwaltungsplatz. Er laedt eine
registrierte Bibliothek und die eigene Person, erzeugt den separaten lokalen
Signierschluessel erst auf ausdrueckliche Aktion und exportiert die oeffentliche
Verwaltungseinladung samt unabhaengig weiterzugebendem Fingerabdruck. Die Auswahl
einer Person allein verleiht keinerlei Rechte; die Hauptverwaltung muss deren
konkreten Schluessel bestaetigen und eine Vollmacht erteilen.

Nach Import des freigegebenen Richtlinienstandes sind ausschliesslich die Gruppen
der aktiven, zum lokalen Schluessel passenden Vollmacht sichtbar/bearbeitbar.
Namen und Personenzuordnungen werden als Entwurf gepflegt. Personen, Geraete und
Vollmachten koennen hier nicht veraendert werden. Der Export signiert nur geaenderte
Gruppen, prueft den aktuellen registrierten Stand erneut und uebertraegt keine
privaten Schluessel. Ungespeicherte/nicht exportierte Entwuerfe erfordern beim
Wechsel oder Schliessen eine Verwerfbestaetigung.

Die Hauptverwaltung uebernimmt die Aenderungsdatei nach gepruefter Vorschau und
verteilt den neuen signierten Gesamtstand. Export alleine aktualisiert keine
Registrierung und keinen Vault. Entzug wird beim Signieren wirksam, sobald der
neuere registrierte Stand lokal vorliegt. Der synthetische App-Test deckt diesen
Ablauf einschliesslich Mitgliederauswahl ueber ein ListBox-Ereignis und Ablehnung
eines alten Entwurfs ab. Native Dateidialoge, visuelle Abnahme und echter zweiter
Windows-Benutzer bleiben offen.

## Aktualisierung registrierter Richtlinien

Einstellungen bietet jetzt die Aktualisierung einer vorhandenen Registrierung.
Der Update-Dialog friert die Eingabedatei ein und prueft sie gegen den bereits
lokal bestaetigten Verwaltungsschluessel, die Bibliothek und den Versionsstand.
Ein Schluessel aus der Aktualisierungsdatei ersetzt niemals die Vertrauenswurzel.
Vorschau zeigt vorher/nachher mit Gruppen, Personen, Geraete-Aktivstatus und
Fingerabdruecken sowie Vollmachten. Ohne ausdrueckliche Bestaetigung keine Aenderung.
Die Uebernahme prueft erneut den beim Anzeigen gelesenen exakten Stand und schreibt
atomar mit Dateikonflikterkennung. Gleiche bereits registrierte Version benoetigt
keinen erneuten Import. Aktualisierung ist keine Neuregistrierung und veraendert
keine Vault-Dateien. Ein global aktueller Stand ist offline nicht beweisbar.

## Vollmachtvergabe und oeffentlicher Dateiaustausch

GroupDelegationTransport transportiert versionierte oeffentliche Einladungen und
signierte Aenderungen getrennt. Grenzen fuer Datei-/Nutzdatengroesse und JSON-Tiefe,
unbekannte/doppelte Felder sowie reine kanonische SPKI-Schluessel werden geprueft.
Das Einlesen prueft Struktur; Berechtigung entsteht erst aus bestaetigter Vergabe
beziehungsweise Signatur-/Umfangspruefung gegen den gespeicherten Verwaltungsstand.

Die Hauptverwaltungsoberflaeche bietet Freigabe fuer ausgewaehlte Gruppen mit
manuell eingegebenem unabhaengigem Fingerabdruck, eine Vollmachtenliste und Entzug.
Einladungen ersetzen keine vorhandene Vollmacht. Offene Tabellenentwuerfe sperren
diese Aktionen. Eingehende Aenderungen werden vor Bestaetigung geprueft und als
Gruppenname sowie hinzugefuegte/entfernte Personen mit stabilen IDs dargestellt.
Die abschliessende Speicherung prueft weiterhin den exakten Ausgangsstand.

Der Delegaten-Arbeitsplatz zum Erstellen/Exportieren der Einladung und Bearbeiten
seiner Gruppen sowie das Aktualisieren registrierter Richtlinien fehlen noch.
Die nativen Datei-/Bestaetigungsdialoge sind noch visuell und interaktiv abzunehmen.

## Lokaler Delegatenschluessel

OfflineGroupDelegateStore erzeugt einen eigenen RSA-3072-Schluessel und schreibt
ihn ausschliesslich ueber einen verpflichtenden authentisierten lokalen Protector.
WindowsGroupDelegateProtection nutzt DPAPI CurrentUser mit separatem Zweck sowie
Bibliotheks- und Personenbindung. Der oeffentliche Export enthaelt nur Bibliothek,
Personen-ID und SPKI; er ist noch keine Vollmacht. Erneutes Create ersetzt nichts.

SignChange laedt den registrierten Vertrauensspeicher neu, vergleicht den genauen
Entwurfsstand, prueft Personen-/Schluesselzuordnung der Vollmacht und validiert die
exakten signierten Aenderungsbytes gegen Aktivstatus und Gruppenumfang. Vor der
Rueckgabe wird ein zwischenzeitlicher lokaler Richtlinienwechsel abgefangen.
Die empfangende Verwaltung muss weiterhin ihren eigenen aktuellen Stand pruefen;
ein offline zurueckgehaltener Widerruf wird dadurch nicht global erkennbar.
Private Bytepuffer werden nach Nutzung geloescht, keine Zertifikatsstore-Eintraege.

Core-Tests decken Neustart, falsche Person/Bibliothek/Protector, beschaedigte Datei,
ueberholten Entwurf, fremden Schluessel, falschen Gruppenbereich und Entzug ab.
Der App-Smoke prueft reale Windows-DPAPI-Zwecktrennung in einem Tempordner, nicht
den Betrieb unter einem zweiten Windows-Benutzer. Noch fehlen Dialog und sichere
Einladungs-/Vollmacht-/Aenderungsdateien fuer den gemeinsamen Offline-Bedienablauf.

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

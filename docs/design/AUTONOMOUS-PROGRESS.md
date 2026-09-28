# Autonome Weiterarbeit - Stand 2026-09-15

## Auftrag und Grenzen
Fehlende Recovery-Registrierung erklaert (2026-09-28): Bibliotheksauswahl
beschriftet, bei leerer Liste konkrete Anleitung zur Registrierung bzw. neuen
Hauptverwaltung und erneutem Oeffnen. Ohne Auswahl sind private Erzeugung und
Wiederherstellung deaktiviert. Vor dem Speicherdialog wird der lokale Trust
geladen/geprueft; alle Fehlerpfade des Erzeugungsbuttons leeren Passphrasenfelder.
WPF-Build Exit 0, 0 Warnungen/Fehler:
tmp/group-recovery-empty-state-verified-build.log.
Bestehender isolierter UI-Smoke Exit 0, 55 Checks/5 Szenarien, stderr leer:
tmp/group-recovery-empty-state-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Der konkrete Hinweis wurde noch nicht erneut am Bildschirm abgenommen; keine
neue native Recovery-Installation oder echte Zweitbenutzerpruefung. Ziel aktiv.

Native Anleitung pruefbar und Dateizuordnungsfehler behoben (2026-09-28):
Gruppeneinstellungen bei 1426x893 sichtbar geprueft; Rollenabschnitte, Anleitung
und bisherige Teamliste lesbar. Anleitungsklick fuehrte zuvor zur Windows-
Programmauswahl fuer .md. Keine Dateizuordnung geaendert. Markdown-Hilfedokumente
oeffnen jetzt in HelpDocumentWindow als lokale schreibgeschuetzte Textansicht,
ohne Browser/aktive Links/externes Programm. WPF-Build Exit 0, 0 Warnungen/Fehler:
tmp/group-internal-guide-build.log. Native erneute Pruefung: Anleitungsfenster
oeffnet, Text bis zum Ende per Scrollleiste erreichbar, Schliessen kehrt zu
Einstellungen zurueck. Markdownzeichen bleiben in der einfachen Textansicht
sichtbar; kein formatierter Markdownrenderer behauptet.
Recovery-Dialog nativ geoeffnet und geschlossen, keine Schluessel erzeugt oder
installiert. Konkreter naechster Bedienpunkt: leere Bibliotheksauswahl erklaert
fehlende Erstregistrierung noch nicht ausreichend. Alle weiteren befuellten
Verwaltungs-/Dateidialoge und echte Recovery-Persistenz bleiben offen.
Nur isolierte --ui-test-workspace-Starts unter tmp/group-native-review-20260928
und tmp/group-internal-guide-visual. Bestehende Identitaeten unveraendert.
Ziel aktiv; kein neuer Release.

Gruppenanleitung in die Anwendung eingebunden (2026-09-28): direkte Schaltflaeche
in den Gruppeneinstellungen verwendet den vorhandenen Dokumentoeffner. Release-
Skript kopiert bereits docs vollstaendig und verlangt jetzt die Anleitung als
Pflichtdatei. Umgebungstest prueft sie ebenfalls. WPF-Build Exit 0, 0 Warnungen/
Fehler: tmp/group-guide-entry-build.log. XAML-Linkziel und PowerShell-Syntax
geprueft. Kein neuer Installer gebaut; installierte Dateizuordnung/Oeffnen und
native Dialogabnahme weiterhin offen. Letzte Core-CI 70/70 unveraendert gueltig,
da keine Coreaenderung. Ziel aktiv; Release 1.2.1 unveraendert.

Acht-Personen-/Drei-Gruppen-Ablauf kryptografisch geprueft (2026-09-28):
OfflineGroupMatrixWorkflowTests verwendet acht getrennte RSA-Schluessel plus
separate Recovery, neun getrennte lokale Vertrauensdateien und oeffentliche
Registrierungspakete. Alle 24 Personen-/Gruppenkombinationen entschluesseln nur
gemaess gewuenschter Zuordnung. Recovery funktioniert getrennt in allen Gruppen;
normale Identitaeten werden im Recoverymodus und Recovery im Normalmodus abgewiesen.
Person 3 wird nur in Lokal 2 durch Person 1 ersetzt: Richtlinienimport allein
gibt Person 1 noch keinen alten Vaultzugang. Vorschau/Abgleich durch Person 4
erzeugt neue Schluesselgeneration und unveraendertes verschluesseltes Backup.
Neuer Zugang funktioniert, entzogener Zugang scheitert; andere Vaultdateien und
Person-3-Zugang zu Lokal/Lokal 3 bleiben erhalten. Absichtlich offline belassener
Stand oeffnet alte Kopie weiterhin, verweigert neueren Vault; spaeterer Import
und Rollbackabwehr auf allen neun Speichern geprueft.
Erstlauf 69/70 wegen ungueltigem JSON im neuen Testpayload, korrigiert.
Vollstaendige CI danach Exit 0, 70/70, CLI/WPF erfolgreich:
tmp/group-matrix-workflow-verified-ci.log. Kein Produktcode dafuer geaendert.
Synthetischer authentisierter Speicherschutz im selben Prozess, KEIN Nachweis
echter Windows-Benutzer-/Rechnertrennung. Native Abnahme und echte Recovery-
Persistenz bleiben offen. Ziel aktiv; Release unveraendert.

Einrichtungshinweise korrigiert (implementiert/geprueft 2026-09-25, gesichert
2026-09-28): Einstellungen unterscheiden Teammitglied, Hauptverwaltung und
delegierte Gruppenverwaltung. Persoenliche Identitaeten bleiben wiederverwendbar;
die bisherige Teamliste ist kein Gruppenberechtigungsnachweis. Status und
Identitaetsdialog kennzeichnen diesen Unterschied. Gruppenprojekte verlangen
keine Recovery-Passphrase pro Projekt; separate Recovery bleibt erforderlich.
Oeffentlicher Verwaltungsexport zeigt Bibliotheks-ID und Fingerabdruck nach dem
Export an. Bibliotheksauswahl im Verwaltungsdialog beschriftet.
WPF-Build Exit 0, 0 Warnungen/Fehler: tmp/group-onboarding-guidance-build.log.
UI-Smoke Exit 0, 55 Checks/5 Szenarien, stderr leer:
tmp/group-onboarding-guidance-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Native Sichtpruefung des vorherigen Standes fand den Anleitungsbruch. Die
Sichtpruefung des korrigierten Standes wurde per Benutzer-Escape beendet und
ist NICHT abgenommen. Keine Wiederholung unveraenderter Tests erforderlich.
Bedienablauf dokumentiert in docs/OFFLINE-GRUPPEN-SCHNELLSTART.md.
Offen: durchgaengige native Dialog-/Dateiauswahlabnahme, Gesamtworkflow ueber
getrennte Arbeitsplaetze sowie echte Windows-Recovery-Persistenz. Synthetische
Einzel-/Integrationspruefungen sind kein echter Zweitbenutzernachweis.
Ziel aktiv; Release 1.2.1 unveraendert.

Hauptverwalter-Ersteinrichtung vereinfacht (2026-09-25): bestaetigtes Erzeugen
einer neuen privaten Verwaltung registriert den selbst erzeugten Vertrauensanker
lokal. Einrichtungstext fuehrt durch private Sicherung, Personen/Gruppen, separate
Recovery und oeffentliche Verteilung. Keine automatische Gruppenmitgliedschaft.
Eigene signierte Aenderungen werden beim Speichern/Neuladen in vorhandenes
lokales Vertrauen uebernommen, nach denselben Anker-/Signatur-/Versionspruefungen.
Kein Schreiben bei unveraendertem Stand. Bei fehlender Registrierung einer
bestehenden Verwaltung ist eine ausdrueckliche eigene Bestaetigung erforderlich;
fremde Anker werden auch dann nicht ersetzt. Fremde Bibliotheken behalten den
unabhaengigen Fingerabdruckweg. Signierspeicher/Trust sind keine gemeinsame
Transaktion; Meldungen/Doku erklaeren gespeicherte Teilzustaende und Wiederaufnahme.
WPF-Build Exit 0, 0 Warnungen/Fehler; tmp/group-own-registration-build.log.
UI-Smoke Exit 0, 55 Checks/5 Szenarien, stderr leer;
tmp/group-own-registration-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neue isolierte DPAPI-Tests fuer initiale Registrierung, Uebernahme eigener
Aenderungen, unveraendertes Dateihash bei Neuladen, fehlende Registrierung ohne
stille Neuanlage und abgewiesenen fremden Anker ohne Ueberschreiben.
Core unveraendert; letzte komplette Regression 69/69 aus group-migration-conflicts-ci.
Naechster Schritt: integrierten Gesamtworkflow und visuelle Bedienbarkeit der
neuen Dialoge abnehmen, offene Fehler/Anleitungsbrueche beheben. Echte Windows-
Recovery-Persistenz und Zweitbenutzer bleiben separat offen. Ziel aktiv.

Namens-/Ausgabezielkonflikte und weitere Altvault-Migrationswege geprueft
(2026-09-25). Gruppenanlage/Kopie sperrt jetzt auch katalogisierte gleiche Namen
(case-insensitive) und kanonisch gleiche Ausgabeziele ohne vorhandenen Buildordner.
Damit kann eine flache/alte Definition nicht erst beim spaeteren Build mit der
neuen Kopie kollidieren. Pruefung unter Zielnamensperre, keine globale Transaktion
mit externen Schreibern behauptet.
Erweiterte synthetische Tests fuer V1/V2-Passphrasenmigration und V3-Zertifikat/
Recovery-Passphrase: alter Entschluesselungszugang und neue Gruppenmitgliedschaft
unabhaengig erforderlich. Ganze normalisierte Payloads inklusive mehrerer
Transportschluesselsaetze, Auswahl, PICC und Anwendungen nach Erstellung/Kopie/
Migration ueber normalen und separaten Recovery-Zugang verglichen; Originalhashes
erhalten. Flat-/Alias-Ausgabezielkonflikte hinterlassen keinen neuen Zielordner.
CI Exit 0, 69/69, CLI/WPF ohne Warnungen/Fehler;
tmp/group-migration-conflicts-ci.log. UI-Smoke Exit 0, 54 Checks/5 Szenarien,
stderr leer; tmp/group-migration-conflicts-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Naechster Schritt: integrierte Hauptverwalter-Ersteinrichtung und lokale Uebernahme
eigener signierter Aenderungen vereinfachen, danach Gesamtworkflow-/visuelle
Abnahme. Native Windows-Recovery-Persistenz und Zweitbenutzer bleiben separat
offen. Ziel aktiv, keine Releaseaenderung.

Vollstaendiges Speichern unter fuer Gruppenprojekte umgesetzt (2026-09-25):
neuer Projekt-/Ordnername, frische authentisierte ID und frischer Vault;
Bibliothek/Gruppe bleiben gleich, aktuelle zentrale Empfaenger werden verwendet.
Originaldateien werden per erwartetem Hash geprueft und lesegesperrt gehalten.
Normaler Zugriff wird erneut am Originalvault geprueft, auch bei geladenen
Editor-Schluesseln. Gesperrte Quelle nutzt Originalkeys; offene Editor-/geladene
Schluesseländerungen koennen in die Kopie einfliessen. Entwurf-ID/Bindung muessen
zur Quelle passen. Recovery-Sitzung, entzogener Zugang und stille Umzuordnung
sind gesperrt. Die vorherige temporaere Save-As-Sperre ist ersetzt.
CI Exit 0, 69/69, CLI/WPF ohne Warnungen/Fehler;
tmp/group-project-copy-verified-ci.log. UI-Smoke Exit 0, 54 Checks/5 Szenarien,
stderr leer; tmp/group-project-copy-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neue Negativtests: unberechtigte Identitaet/Recovery, falscher Dateistand,
Entwurfs-ID und Gruppenbindung; UI prueft gesperrte Quelle, offene Keyaenderung,
erhaltene Originalhashes sowie Kopiersperre nach importiertem Entzug.
Namensdialog noch nicht visuell bedient. Naechster Schritt: weitere Migrations-
und Namens-/Ausgabeziel-Konfliktfaelle, danach integrierte Einrichtung/Gesamtabnahme.
Native Dialoge/echte Windows-Recovery-Persistenz/Zweitbenutzer weiterhin offen.
Ziel aktiv, Release unveraendert.

Gefuehrte Gruppenprojekt-Erstellung und explizite Altvault-Migration als neue
Projektkopie umgesetzt (2026-09-25). Editor waehlt registrierte Bibliothek/Gruppe,
neuen eindeutigen Namen und bestaetigt Kopierumfang/alte Zugriffsmoeglichkeiten.
Aktive normale Mitgliedschaft und Recovery-Empfaenger werden im Core erzwungen.
Neue Projekt-ID, frischer Vault, Originaldateien unveraendert; bestehende
Zielordner werden nicht ueberschrieben. Quelle wird anhand erwarteter Hashes
geprueft und waehrend der Migration unter Windows lesegesperrt gehalten.
Arbeitsordner enthaelt nur verschluesselten Vault und schluesselfreie pending-
Definition. Erst die abschliessende Umbenennung veroeffentlicht im Katalog.
Fehler koennen nicht gelistete Arbeitsreste/belegte Zielordner hinterlassen;
keine pauschale Loeschung und keine behauptete verteilte/Cloud-Transaktion.

Erster UI-Lauf tmp/group-project-setup-smoke scheiterte an echter Fehlvalidierung:
gesperrte Quelle enthaelt absichtlich keine Transportkeys. Vollstaendige Pruefung
erfolgt jetzt nach Entschluesselung, vor jedem Veroeffentlichen. Migrationsabsicht
bleibt an geladene Vaultrevision gebunden (kein Fallback bei geloeschter Quelle).
Reines Definitions-Speichern-unter fuer Gruppenprojekte vorlaeufig gesperrt;
vollstaendige Gruppenprojektkopie mit neuer Bindung muss noch implementiert werden.
Altprojekt-Funktionen bleiben erhalten. Neue Gruppe wird nach Erfolg gesperrt
geladen und ueber vorhandenen normalen Entsperrweg geoeffnet.

CI Exit 0, 69/69 und CLI/WPF ohne Warnungen/Fehler;
tmp/group-project-creation-verified-ci.log. UI-Smoke Exit 0, 53 Checks/5 Szenarien,
stderr leer; tmp/group-project-setup-verified-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neue Nachweise: Originalhashes, neue ID, verweigerter Ersteller, falsche Passphrase,
veralteter Dateistand, geaenderte Richtlinie und unsichtbare Fehlversuche im Katalog;
Editor-Neuanlage/Migration und anschliessendes Entsperren mit erhaltenem PICC-Key.
Naechster Schritt: vollstaendige Kopie bestehender Gruppenprojekte/Save As,
weitere Migrationswege und Gesamtabnahme. Native Dialoge/echte Windows-Recovery-
Persistenz und Zweitbenutzerabnahme bleiben offen. Ziel aktiv, Release unveraendert.

Gefuehrte separate Recovery-Identitaet integriert (2026-09-24): Einstellungen
bieten registrierte Bibliotheksauswahl, neue private Sicherung mit zweifacher
Passphrase/Bestaetigung, geprueftes Dateiruecklesen und Wiederherstellung,
anschliessend separaten oeffentlichen Einladungsexport. Erzeugung installiert
keinen Windows-Schluessel und erteilt keine Freigabe. Wiederherstellung verlangt
aktiven zentral freigegebenen Recovery-Fingerabdruck im registrierten Stand;
Bibliotheks-/Zweckbindung und echte RSA-OAEP-Entschluesselung werden geprueft.
Explizite Windows-Uebernahme verwendet nicht-exportierbare UserKeySet-Persistenz,
ersetzt keine bestehenden Zertifikate und prueft gespeicherte Entschluesselung.
Dieser echte Installer wurde NICHT ausgefuehrt: UI-Test injiziert eine pruefende
ephemere Uebernahme, keine Zertifikatsspeicheraenderung/Mehrbenutzerfreigabe.
CI Exit 0, 69/69, CLI/WPF erfolgreich; tmp/group-recovery-setup-ci.log.
Nach finalem Textabgleich WPF-Build Exit 0, keine Warnungen/Fehler;
tmp/group-recovery-setup-ui-build.log. UI-Smoke Exit 0, 52 Checks/5 Szenarien,
stderr leer; tmp/group-recovery-setup-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neuer Test prueft Gesamtablauf, fehlende Freigabe/falsche Passphrase/Entzug,
Ueberschreibschutz und erfolgreiche Wiederherstellung ohne echten Installer.
Naechster Schritt: konsistente gefuehrte Gruppenprojekt-Erstellung und explizite
Altvault-Migration; native/visuelle Einrichtung und echte Windows-Installer-
Abnahme separat offen. Ziel aktiv, Release 1.2.1 unveraendert.

Private Sicherung/Wiederherstellung fuer Haupt- und Gruppenverwaltung in der UI
integriert (2026-09-24). Eigenes Passphrase-/Bestaetigungsfenster mit Auswahl
registrierter Bibliothek bzw. Vollmacht; oeffentliche Einladungen klar getrennt.
Sicherung verlangt zweimalige Passphrase und bestaetigte getrennte Verwahrung,
schreibt nur neue Dateien und prueft identisches Ruecklesen. Restore verwendet
registriertes Vertrauen; ueberschreibt keine lokale Identitaet. Passwortfelder
werden nach dem Versuch/Schliessen geleert. Keine private Sicherung automatisch
im gemeinsamen Projektordner. Gespeicherte Sicherungen bleiben erhalten.
WPF-Build Exit 0, keine Warnungen/Fehler; tmp/group-private-backup-ui-build.log.
UI-Smoke Exit 0, 51 Checks/5 Szenarien; tmp/group-private-backup-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neuer Dialogmethodentest: fehlende Bestaetigung, ungleiche/falsche Passphrase,
oeffentliche Registrierung statt privater Sicherung, Dateiruecklesen,
Windows-DPAPI-Wiederherstellung und Ueberschreibschutz. Native Dateiauswahl und
visuelle Abnahme noch offen. Core unveraendert, vorherige CI 69/69 weiterhin Basis.
Naechster Schritt: separate Recovery-Identitaet mit gepruefter privater Sicherung
vor lokaler Persistenz gefuehrt erzeugen/wiederherstellen; Projektmigration und
Gesamtworkflow-Abnahme weiterhin offen. Ziel aktiv, kein neuer Release.

Portable private Schluesselsicherung im Core implementiert (2026-09-24):
AES-GCM/PBKDF2-SHA256 mit 600000 Iterationen, getrennte Zwecke fuer Hauptverwaltung,
Gruppenverwalter und Recovery; Bibliothek/Person/Fingerabdruck werden beim Import
geprueft. Export prueft echte private Signatur nach Entschluesselung der Sicherung.
Hauptverwaltung wird mit dem aktuell lokal registrierten Richtlinienstand
wiederhergestellt, Delegaten nur mit aktiver registrierter Vollmacht; vorhandene
Schluesseldateien werden nicht ueberschrieben. Lokale Schutzschluessel sind im Test
verschieden; keine echte zweite Windows-Umgebung und kein Zertifikatsspeichereingriff.
CI Exit 0, 69/69, CLI/WPF ohne Warnungen/Fehler; tmp/group-private-backup-ci.log.
Neue Tests: falsche Passphrase/Zweck/Bibliothek/Fingerabdruck, Manipulation,
striktes JSON, Zertifikat-Schluessel-Verwechslung, Recovery-RSA-Entschluesselung,
restaurierte Verwaltungssignaturen, Entzug, Richtlinienwechsel beim Speichern.
Keine UI-Aenderung in diesem Schritt; vorherige UI-Abnahme nicht wiederholt.
Naechster Schritt: gefuehrte private Sicherung/Wiederherstellung in der Oberflaeche
und separate Recovery-Identitaetserzeugung. Projekt-Erstellung/Migration und
Gesamtworkflow-Abnahme bleiben offen. Ziel aktiv, Release 1.2.1 unveraendert.

Zentrale Recovery-Freigabe integriert: Hauptverwalter importiert oeffentliche
Einladung mit unabhaengigem Fingerabdruck, sieht aktive/entzogene Zugaenge und
kann sie bestaetigt umschalten. Core erzwingt Schluesseltrennung/keine Duplikate
und signiertes konfliktgeprueftes Speichern. Bibliotheksweite Reichweite, benoetigte
Verteilung/Rotation und Grenzen alter Kopien werden angezeigt.
CI Exit 0, 68/68, CLI/WPF erfolgreich; tmp/group-recovery-grant-ci.log.
Erster UI-Lauf scheiterte beim Aufraeumen einer gesperrten synthetischen
card-preflight.plan.json; anschliessender exklusiver Lesezugriff belegte Freigabe.
Unveraenderter Build in neuem Testordner erfolgreich: Exit 0, 50 Checks/5 Szenarien,
stderr leer; tmp/group-recovery-grant-smoke2/SieEncode/ui-smoke/ui-smoke-result.json.
Dialogmethoden pruefen falschen Fingerabdruck, normale Geraeteschluessel, Duplikate,
Entzug und Reaktivierung ohne Zertifikatsspeichereingriff. Native Importdialoge
bleiben offen. Naechster Schritt: portable private Schluesselsicherung und
gefuehrte separate Recovery-Identitaetserzeugung; Gruppenprojekt-Erstellung/Migration
und Gesamtworkflow-Abnahme bleiben ebenfalls offen.

Separater Recovery-Editorweg integriert: eigene Taste, kein automatischer
Fallback, expliziter Sitzungsstatus; Speichern/Abgleich/Audit verwenden den
Recovery-Zugang. Builds bleiben gesperrt bis erfolgreichem normalen Entsperren;
Stationszugang bleibt normal, Projektneuladen verwirft den Recovery-Modus.
WPF-Build Exit 0, 0 Warnungen/Fehler; tmp/group-recovery-editor-build.log.
UI-Smoke Exit 0, 49 Checks/5 Szenarien;
tmp/group-recovery-editor-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neuer synthetischer Button-Ablauf prueft Rollentrennung, Recovery-Schluesselupdate,
Audit und bewussten Wechsel zum normalen Zugang. Core unveraendert; letzte
vollstaendige Core-/CLI-Regression weiterhin 68/68 aus dem vorherigen Stand.
Naechster Schritt: gefuehrte Recovery-Identitaetsfreigabe und portable private
Schluesselsicherung, danach Gruppenprojekt-Erstellung/Migration. Native Dialog-
und echte Mehrbenutzerabnahme weiterhin offen.

Editor-/Stationsintegration fuer bestehende Gruppenprojekte umgesetzt: persoenlich
entsperren, Schluesselupdates mit Sicherung, bestaetigter Empfaengerabgleich und
normaler Integritaetsaudit. Stationspfad laedt ueber registrierten Gruppenstand.
Erneute Zugriffspruefung vor Editorbuild invalidiert Sitzung bei Entzug. Katalog
verhindert Altbuild-Fallback bei defektem/fehlendem Vault eines gebundenen Projekts.
Interne Schutzstufe 4 ist kein neues Dateiformat; Gruppenformat bleibt Version 1.
CI Exit 0, 68/68, CLI/WPF ohne Warnungen/Fehler;
tmp/group-editor-integration-ci-verified.log. UI-Smoke Exit 0, 48 Checks/5 Szenarien;
tmp/group-editor-integration-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neuer synthetischer Test prueft Editor-Buttons, Schluessel-Roundtrip, Katalog,
lokale sichere Stationsbereitstellung, Entzug/Bausperre und defekten-Vault-Fallback.
Keine Reader-/Kartenoperation, keine echte Mehrbenutzerfreigabe.
Naechste Schritte: gefuehrte Gruppenzuordnung/Erstellung und explizite Migration,
separater Recovery-Bedienweg, Einrichtung/Sicherung privater Recovery-/Verwaltungs-
schluessel; visuelle/native Abnahme und Vereinfachung des Gesamtworkflows.

Stabile Projektidentitaet und typisierte Gruppen-Vault-Projektanbindung im Core:
neue Entwuerfe erhalten ID, alte Projekte erst bei expliziter Zuordnung auf Kopie.
ID/Bibliothek/Gruppe bleiben beim schluesselfreien Speichern und Anwenden von
Schluesseln erhalten. Erstellen prueft eigenen Entschluesselungszugang und ersetzt
keine vorhandenen Vaults; Laden prueft authentisierte Bindung und Nutzdatentyp.
Aktualisieren mit Sicherung/frischem Schluessel; geaenderte Empfaenger sperren
normales Speichern bis zum separaten kontrollierten Abgleich.
Synthetische Tests fuer Projekt-Roundtrip, falsche ID/Gruppe, unberechtigten
Ersteller ohne Dateischreibzugriff, Update/Recovery/Sicherung und alte Revision.
CI Exit 0, 68/68, CLI/WPF ohne Warnungen/Fehler;
tmp/project-group-binding-ci.log. UI-Smoke Exit 0, 47 Checks/5 Szenarien;
tmp/project-group-binding-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Naechster Schritt: Editor-/Stationsintegration mit konsistentem Erstellen und
Speichern von Projekt plus Vault, explizite Migration, Recovery-Einrichtung und
geschuetzte Sicherung der privaten Verwaltungs-/Recovery-Schluessel.

Kontrollierter Gruppen-Vault-Empfaengerabgleich im Core umgesetzt: authentisierte
schreibfreie Vorschau, exakte Quell-/Richtlinienbindung, erneute Zugriffskontrolle,
frischer Inhaltsschluessel und gepruefte bytegleiche verschluesselte Sicherung vor
Dateiaustausch. Dateischreiber prueft jetzt auch nach Tempdateischreiben nochmals
gegen Fremdaenderungen. Alte Sicherungen bleiben mit alten Schluesseln zugaenglich;
keine globale Transaktion mit Richtliniencache/Cloud-Sync behauptet.
Tests: Entzug/Zugangszuwachs, neuer Empfaenger ohne Altzugang abgewiesen,
Preview ohne Schreiben, veraltete Datei/Richtlinie, erfolgreiche Recovery-Rotation,
Sicherungswiederherstellung und spaete Fremdaenderung ohne Ueberschreiben.
CI Exit 0, 68/68, CLI/WPF ohne Fehler; tmp/group-vault-reconciliation-ci.log.
UI-Smoke Exit 0, 47 Checks/5 Szenarien, stderr leer;
tmp/group-vault-reconciliation-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Naechster Schritt: stabile Projekt-ID/Zuordnung und Integration des neuen
Vaultformats/Abgleichs in Projektverwaltung; Recovery-Einrichtung und Sicherung
der privaten Recovery-/Verwaltungsschluessel sind ebenfalls noch offen.

Gruppen-Vault-Kern als separates Format implementiert (noch keine Editoranbindung):
AES-GCM bindet Projekt/Bibliothek/Gruppe/Richtliniencheckpoint/Schluesselgeneration
und komplette RSA-OAEP-Empfaengerhuellen. Normale Gruppe und separate signierte
RecoveryDevices, verpflichtender aktiver Recoveryzugang fuer neue Vaults.
Schluesseltrennung zwischen Hauptverwaltung, Delegation, Geraeten und Recovery;
Recovery bleibt bei entfernter Gruppe moeglich, normaler Zugang wird verweigert.
Neue Verschluesselung erzeugt frische Inhaltsschluessel. Alte Kopien bleiben mit
altem Stand entschluesselbar; dies wird explizit getestet und nicht als Widerruf
ausgegeben. Geaenderte Projekt-/Gruppenangaben auch bei passender geaenderter
Erwartung, manipulierte Huellen/Nutzdaten, falsche Rolle und veralteter Stand
negativ getestet. Recoveryinformationen erscheinen in Richtlinienupdate-Vorschau.
CI Exit 0, 68/68, CLI/WPF ohne Warnungen/Fehler;
tmp/group-vault-core-ci-verified.log. UI-Smoke Exit 0, 47 Checks/5 Szenarien,
stderr leer; tmp/group-vault-core-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Naechste Schritte: stabile Projekt-ID/Zuordnung, Recovery-Einrichtung/-Sicherung,
kontrollierter atomarer Empfaengerabgleich und Vaultrotation, Editor-/Stations-
integration. Kein Release und keine echte Hardware-/Mehrbenutzerabnahme.

Delegaten-Arbeitsplatz integriert: registrierte Bibliothek/Person laden,
Schluessel ausdruecklich erzeugen, oeffentliche Einladung/Fingerabdruck exportieren,
nur erlaubte Gruppen bearbeiten und signierte Aenderung weitergeben. Personen,
Geraete und Vollmachten bleiben dort schreibgeschuetzt. Export aendert noch keinen
Vault/registrierten Stand; Hauptverwaltung uebernimmt und verteilt Gesamtstand.
CI Exit 0, 67/67, CLI/WPF ohne Warnungen/Fehler;
tmp/group-delegate-workplace-ci.log. UI-Smoke Exit 0, 47 Checks/5 Szenarien,
stderr leer; tmp/group-delegate-workplace-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Synthetischer Ablauf: Einladung -> Vollmacht -> Richtlinienupdate -> Mitgliederauswahl
im Dialog -> signierte Datei -> Uebernahme -> Entzug; alter Entwurf abgewiesen,
nach Neuladen keine bearbeitbare Gruppe. Keine echte Zweitbenutzer-/native Abnahme.
Naechster Schwerpunkt: authentisierte Projekt-/Gruppenzuordnung und neue
Vault-/Recoverybindung, kontrollierter Empfaengerabgleich. Visuelle/native
Abnahme und Vereinfachung des integrierten Einrichtungsablaufs bleiben offen.

Registrierte Richtlinien lassen sich jetzt in Einstellungen aktualisieren:
gegen fest registrierten Schluessel/Bibliothek/Checkpoint gepruefte eingefrorene
Vorschau, alter/neuer Stand inkl. Personen/Geraete/Vollmachten, Bestaetigung vor
atomarer Uebernahme. Veraltete Bestaetigung und Schluesselaustausch abgewiesen.
CI Exit 0, 67/67, CLI/WPF ohne Fehler; tmp/group-policy-update-ci.log.
UI-Smoke Exit 0, 46 Checks/5 Szenarien, stderr leer;
tmp/group-policy-update-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Native Dateiauswahl/visuelle Dialogabnahme bleibt offen. Naechster Schritt ist
der Delegaten-Arbeitsplatz (Identitaetseinladung exportieren, eigene Gruppen
bearbeiten, signierte Aenderung exportieren), danach Projekt-/Vault-/Recoverybindung.

Vollmachtvergabe in Hauptverwaltung und strikter oeffentlicher Dateitransport
integriert: versionierte Einladungen/Aenderungen, Groessen-/JSON-/SPKI-Pruefung;
Freigabe ausgewaehlter Gruppen nur mit unabhaengig eingegebenem Fingerabdruck.
Vollmachtenliste/Entzug und signaturgepruefte Aenderungsvorschau mit Personen-IDs.
Offene Entwuerfe und veraltete Richtlinien sperren die Uebernahme.
CI Exit 0, 67/67, CLI/WPF 0 Fehler; tmp/group-delegation-transport-ci.log.
UI-Smoke Exit 0, 45 Checks/5 Szenarien, stderr leer;
tmp/group-delegation-transport-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Dialogmethoden mit synthetischer Person/Vollmacht und transportierter delegierter
Aenderung geprueft. Native Datei-/Bestaetigungsdialoge noch nicht abgenommen.
Naechster Schritt: Delegaten-Arbeitsplatz (Einladung erzeugen/exportieren, eigene
Gruppen bearbeiten/Aenderung exportieren) und Updates registrierter Richtlinien;
anschliessend authentisierte Projekt-/Vaultbindung und separater Recoveryzugang.

Lokaler Delegatenschluessel implementiert: eigener RSA-3072-Speicher, oeffentlicher
Export und Signieren ausschliesslich gegen neu geladenen registrierten Stand mit
passender Person, Schluessel, Vollmacht und Gruppenumfang. Windows-DPAPI-Zweck ist
von Hauptverwaltung/Trust getrennt und an Bibliothek/Person gebunden. Keine
Zertifikatsspeicher-Aenderung oder Ueberschreibung vorhandener Schluessel.
CI Exit 0, 67/67, CLI/WPF gebaut; tmp/group-delegate-key-ci.log.
UI-Smoke Exit 0, 45 Checks/5 Kompatibilitaetsszenarien, stderr leer;
tmp/group-delegate-key-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Synthetischer Windows-DPAPI-Nachweis, keine zweite Benutzerumgebung behauptet.
Naechster Schritt: gefuehrter Delegatendialog und striktes oeffentliches
Einladungs-/Aenderungstransportformat, Vollmachtvergabe in Hauptverwaltung und
Aktualisierung des registrierten Standes. Vault-/Recoveryintegration bleibt offen.

Gruppenbegrenzte Delegationen im Core umgesetzt: hauptsignierte Vollmachten mit
separatem Signierschluessel, Person/Aktivstatus und erlaubten Gruppen. Signierte
Aenderungen binden Bibliothek und exakten Ausgangs-Checkpoint. Nur Namen und
Mitgliedschaften vorhandener erlaubter Gruppen veraenderbar, keine Personen-,
Geraete- oder Rolleneskalation. Verwaltungsstore uebernimmt nach Pruefung atomar;
konkurrierender Vollmachtentzug, Replay, fremde Gruppen/Signaturen/Bibliotheken,
doppelte/unbekannte Felder und Schluesselwiederverwendung negativ getestet.
CI nach Korrektur des Negativtest-Exceptionfilters Exit 0, 66/66, CLI/WPF gebaut.
Nachweis tmp/group-delegation-ci-final.log. Isolierter App-Smoke Exit 0,
44 Checks/5 Kompatibilitaetsszenarien, stderr leer:
tmp/group-delegation-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Noch kein delegierter UI-/Ablageablauf: derzeit nimmt die Hauptverwaltung die
signierten Aenderungen entgegen. Naechster Schritt: geschuetzte Delegatenschluessel,
gefuehrte Vollmachtvergabe und Offline-Transport mit wirksamer Rollenanzeige;
danach authentisierte Projekt-/Vaultbindung, Empfaengerabgleich und Recovery.
Keine neue Release-, Hardware- oder Mehrbenutzerfreigabe.

Personen-/Geraeteoberflaeche integriert: Namen/Aktivstatus, bestaetigte
Geraeteeinladungen, Personen als Gruppenmitglieder, signiertes v2-Speichern.
Neue leere Verwaltungen nutzen Personenmodell; bestehende v1-Testgruppen nur
explizit mit vollstaendiger Geraetezuordnung umstellbar. Export uebernimmt keine
offenen Tabellenentwuerfe. Gemeinsamer Einladungsimport jetzt Public-DER-only,
keine PFX-Importierung und korrekte Zertifikatsfreigabe bei Fehler.
CI Exit 0, 65/65, CLI/WPF erfolgreich; UI-Smoke Exit 0, 44 Checks/5 Szenarien,
stderr leer. Nachweise tmp/group-people-ui-ci.log und
tmp/group-people-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Native Import-/Umstellungs-/visuelle Abnahme noch offen. Naechster Schritt:
signierte gruppenbegrenzte Delegationen und wirksame Rollenpruefung, danach
authentisierte Projektzuordnung/Vault-/Recoveryanbindung.

Personen-/Geraeteverzeichnis im Core ergaenzt: signierte Richtlinie v2 mit stabilen
Personen-IDs, mehreren oeffentlichen Geraetezertifikaten, Aktivstatus und Gruppen
als Personenlisten. Eindeutige Zertifikatszuordnung, DER/Public-only und SHA256-
Bindung geprueft. Explizite Gruppenaufloesung liefert nur aktive gueltige Geraete;
keine Ausweichfreigabe bei unbekannter/leerer Gruppe. UpdateDirectory speichert
Verzeichnis und Gruppen zusammen, v1 bleibt lesbar ohne automatische Migration.
CI Exit 0, 65/65 Tests, CLI/WPF gebaut, tmp/group-directory-ci.log.
Neue Tests nutzen die Benutzer-Matrix (8 Personen/3 ueberlappende Gruppen) plus
Mehrgeraete-/Deaktivierungs-/Zertifikatsnegativfaelle. Noch keine UI-Personenpflege
oder Vaultintegration. Naechster Schritt: Personen/Geraete in Verwaltungsdialog
integrieren und begrenzte Delegationen umsetzen.

Gruppen-Verwaltungsdialog ergaenzt: neue lokale Verwaltung, Auswahl vorhandener
Signierspeicher, Gruppenname/stabile ID, bestaetigter Einladungseintrag und
Mitgliederentfernung im Entwurf, signiertes Speichern und oeffentlicher Export.
Ungespeicherte Entwuerfe nicht exportierbar; Verwerfabfrage bei Wechsel/Schliessen;
fehlgeschlagener Verwaltungswechsel stellt die vorige Auswahl wieder her.
WPF-Build Exit 0; UI-Smoke Exit 0, 44 Checks/5 Szenarien, stderr leer.
tmp/group-administration-ui-final-build.log und
tmp/group-administration-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Nur synthetischer UI-Ablauf, native Dialog-/visuelle Abnahme noch offen.
Naechster Schritt: signiertes Personen-/Geraeteverzeichnis und Delegationen;
danach authentisierte Projektzuordnung und neues Vault-/Recoverymodell.

Fortsetzung Verwaltungsgrundlage: OfflineGroupAdministrationStore implementiert
mit separatem lokal geschuetztem RSA-Signierschluessel, signierter Gruppenrevision
und oeffentlichem Registrierungs-Export. DPAPI-Zweck fuer Signierer getrennt vom
Vertrauenscache. Veraltete Richtlinienentwuerfe und konkurrierende Dateiaenderungen
werden abgewiesen, Create ersetzt keine vorhandene Verwaltung.
WPF-Build Exit 0, 0 Warnungen/Fehler; UI-Smoke Exit 0, 43 Checks/5 Szenarien,
stderr leer. tmp/group-administration-build.log und
tmp/group-administration-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Nur synthetische Verwaltung im Testordner. Naechster Schritt: Verwaltungsdialog
mit Erstellen/Export/Gruppenpflege, danach Delegationen und Personen/Geraete.
Verwaltungsschluessel-Recovery und neue Vault-Anbindung weiterhin offen.

Fortsetzung 23.09.: oeffentliches GroupEnrollmentPackage mit striktem Parser und
Signatur-/Bibliothekspruefung; GroupEnrollmentWindow in Einstellungen (klar als
Entwicklung markiert). Unabhaengigen Fingerabdruck eingeben und Bibliotheks-ID
bestaetigen, erst dann lokale DPAPI-Registrierung. Kandidatkopie gegen Austausch
nach Dialogoeffnung; keine Ueberschreibung bestehender Registrierung.
Normaler LocalApplicationData-Pfad, explizit isolierter Pfad fuer Startup-Tests.
CI Exit 0, 64/64; anschliessender WPF-Isolationsbuild Exit 0, keine Warnungen/Fehler;
UI-Smoke Exit 0, 42 Checks/5 Szenarien, stderr leer. Nachweise:
tmp/group-enrollment-ci.log, tmp/group-enrollment-isolation-build.log,
tmp/group-enrollment-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Native Dialogabnahme offen. Naechster Schritt: Verwaltung erstellen/exportieren,
delegierte gruppenbegrenzte Rollen und Personen-/Geraeteverzeichnis. Registrierung
allein ist noch keine Vaultfreigabe; neues Vaultformat und Projektbindung fehlen.

Fortsetzung Offline-Gruppen 22.09.: WindowsGroupTrustProtection implementiert,
DPAPI CurrentUser mit Anwendungs-/Bibliotheksbindung, ohne Klartextfallback.
Echter Roundtrip nur fuer synthetischen Cache im isolierten Testordner;
erneutes Laden OK, falsche Bindung und manipulierte Datei abgewiesen.
WPF-Build Exit 0, keine Warnungen/Fehler; UI-Smoke Exit 0, 41 Checks/5 Szenarien,
stderr leer. tmp/windows-group-trust-build.log und
tmp/windows-group-trust-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Keine echte Zweitbenutzerabnahme. Naechster Schritt: gesicherte Erstregistrierung
und lokaler AppData-Pfad in der Oberflaeche, danach Delegation/Projektbindung.

Explizites aktives Ziel seit 22.09.: Offline-Gruppenverwaltung konsequent bis zur
geprueften Integration entwickeln (create_goal, kein Tokenlimit). Zweiter Baustein:
OfflineGroupTrustStore mit bestaetigtem SPKI-Fingerabdruck, atomarer Registrierung,
signiertem Offlinecache, Wiederanlauf und konkurrierendem Update-Schutz.
Verpflichtender authentisierender Protector ohne Klartextfallback; bisher nur
synthetischer Testprotector, Windows-Benutzerbindung/App-Pfad noch anzubinden.
Finale CI Exit 0, 64/64 Tests und CLI/WPF-Build erfolgreich:
tmp/offline-group-trust-ci-final.log. Erster Lauf: erwartete InvalidDataException
bei falscher Bibliothek war im Test nicht abgefangen; Testauswertung korrigiert.
Naechster konkreter Schritt: Windows-geschuetzter lokaler Store und sichere
Erstregistrierung anbinden, dann gruppenbegrenzte Delegation/Projekt-Vaultbindung.

Neuer Auftrag 22.09.: Offline-Vaults mit zentral signierten Gruppen und getrennten
Verwaltungsrechten entwickeln. Laut Benutzer keine produktiven Vaults; vorhandene
Testdateien/Identitaeten trotzdem nicht ungefragt loeschen. Neuer Branch
codex/offline-vault-groups, ab 1fe2e42; Release 1.2.1 unveraendert.
Modell und Grenzen: OFFLINE-VAULT-GROUPS.md. Erster Core-Baustein implementiert:
RSA-PSS-signierte Gruppenrichtlinien, externe Vertrauenswurzel, Bibliotheksbindung,
Versions-/Konfliktpruefung, strikte Struktur- und Groessenpruefung. Vollstaendige
CI Exit 0, 63/63 Tests, CLI/WPF-Build erfolgreich; tmp/offline-group-policy-ci.log.
Noch keine App-/Vault-Anbindung und keine fertige Rollenverwaltung. Naechster
Schritt: sichere Erstregistrierung und persistenter lokaler Vertrauensstand;
danach Delegation, Projektbindung, Recovery und UI. Keine Hardwarefreigabe.

Release 1.2.1 am 22.09.2026 auf ausdruecklichen Benutzerauftrag gebaut:
Source 06520fbdc5e60fc8b49caf9ab3926c1dcccf5115 (sauber), MSI-Version 1.2.1,
unveraenderte getestete Runtime. Enthaelt die unten beschriebene Vault-Bedienhilfe.
Build-Release Exit 0; exakter Release UI-Smoke Exit 0, 40 Checks/5 Szenarien,
stderr leer. MSI /a Exit 0, alle 1030 Dateien hashgleich, zwei Paketmanifeste
geprueft. Keine echte Installation durch diese Extraktion belegt.
artifacts/release-1.2.1/package: MSI und Abnahme-Kit, kein Portable.
MSI SHA256: 720C4EE587A354E177A270C07BCF7D366504F2ACBA19818372AAF07A6B85508C.
Studio-DLL SHA256: C7FFB4E1A2DB661C6E33FEAA1C244000355C452525C3A3CE43B92E14D0776E77.
Logs: tmp/release-1.2.1-package.log, tmp/release-1.2.1-msi-admin.log,
tmp/release-1.2.1-smoke/SieEncode/ui-smoke/ui-smoke-result.json,
artifacts/release-1.2.1/package-verification.json. Bekannte ICE40/ICE61;
praktische Abnahmen weiterhin offen, siehe docs/RELEASE-1.2.1.md.
Automatische Weiterarbeit bleibt bis zur Kollegenrueckmeldung pausiert.

Aktualisierung 22.09.2026: Benutzer verlangt ausdruecklich regulaeren MSI-Release
statt Portable; derzeit kein produktiver Betrieb, gemeinsame Kollegenabnahme
folgt nach Auslieferung. Interner Release 1.2.0 gebaut aus 65dd9808e602e650945227b260a0532933c6f5c3,
Runtime unveraendert c959bd4. Test-CI Exit 0 (62/62); exakter Release UI-Smoke
Exit 0 (40 Checks, 5 Szenarien), stderr leer. MSI /a Exit 0, 1029 Dateien
hashgleich. Keine tatsaechliche Installation durch /a belegt.
Artefakte: artifacts/release-1.2.0/package, MSI und Abnahme-Kit, kein Portable.
MSI SHA256: 57058E0CAE70142947BE5EBCC4813840ADA4B11ECC4066A9E0C4197B6D473F01.
Studio-DLL SHA256: 831C1888B71B95257C7983C2FAAB5D386E14EA9A8C684E39B895A3B6D29E46B0.
Nachweise: tmp/release-1.2.0-ci.log, tmp/release-1.2.0-package.log,
tmp/release-1.2.0-smoke/SieEncode/ui-smoke/ui-smoke-result.json,
tmp/release-1.2.0-msi-admin.log, artifacts/release-1.2.0/package-verification.json.
Bekannte WiX ICE40/ICE61 bleiben; praktische Installation/Upgrade, Mehrbenutzer,
Hardware und vollstaendige UI-Abnahme fuer Kollegen offen (docs/RELEASE-1.2.0.md).
Diese bewusste interne Auslieferung ersetzt keine ausstehenden Abnahmen.

Autonom und tokensparsam bis zum geprueften internen Release weiterarbeiten.
PDF-Weiterentwicklung/visuelle Abnahme auf Benutzerwunsch zurueckgestellt;
bestehende Funktionen erhalten. Nur kanonische private Repositories verwenden.
Produktive Daten, Vaults, Zertifikate und installierte RC15 nicht ersetzen.
Keine unbekannten Karten beschreiben. Praktische Abnahmen nicht simuliert freigeben.
Routinefixes, Tests, private Branch-Pushes und isolierte Kandidaten sind autorisiert.

## Aktuell abgeschlossen
Studio codex/workspace-ui: Recovery-Speichern ohne passende Windows-Identitaet
korrigiert. Save-Vault leitet dann in die bestehende explizite Empfaengerfreigabe;
vorhandene Empfaenger werden uebernommen, Recovery wird bestaetigt. Kein Speichern
von Passphrasen oder Abschwaechen der Kryptografie. Identitaetspfad unveraendert.
Abbruch laesst Vaultbytes unveraendert und bestaetigt geaenderte Keys nicht.

62/62 Regressionstests sowie CLI/WPF-Build erfolgreich (tmp/recovery-save-ci.log).
Regulaerer Publish 1.1.0-releasecheck.20260915.2 unter
artifacts/recovery-save-check-20260915/Studio, ohne Runtime in diesem Testordner.
35 UI-Checks und 5 Kodierszenarien, Prozess Exit 0; Nachweis
 tmp/recovery-save-smoke-final/SieEncode/ui-smoke/ui-smoke-result.json.
Studio-DLL SHA256 EE96A35AEBA21065CE3A09D6F1F468109C7B92F83A8BD42A70A303F80952377A.
Erster Versuch: Dateisperre beim Aufraeumen der Kodiermatrix, spaeter Timeout.
Zweiter: 35 Checks OK, aber synthetischer Editor blieb dirty und blockierte Ende.
Testcleanup korrigiert, finaler Lauf komplett OK. Keine weitere Wiederholung noetig.

## Bisherige Nachweise
- Vault-Invalid-Handle-Fix und Dialogvalidierung synthetisch geprueft.
- v2-Editor: Speichern, Backup, erneutes Laden/Entsperren per Buttons/Passwortdialog.
- v3-Editor: Freigabe, Identitaets-Unlock, Schluesselaenderung und Save,
  Recovery-Unlock ohne Identitaet, danach Save/Freigabe sowie Abbruch.
- Vault-Empfaengerdialog: Hinzufuegen/Entfernen, positive Freigabe, nach Dialogende
  gueltige Zertifikatskopien; bestehende v3-Empfaenger nicht aus Teamliste ergaenzen.
- Core-Vaultkette: Empfaengerwechsel, Recovery-Wechsel, Sicherung, Quelldateien
  entfernen und Wiederherstellen. Alte Kopien bleiben mit alten Berechtigungen lesbar.
- Produktions-Teststart isoliert explizite Settings-/Projekt-/Profil-/Backuppfade
  vor Fensterkonstruktion und wartet auf Loaded; normaler Start unveraendert.
- Runtime c959bd4: 193/193 Tests, artifacts/runtime-releasecheck-20260914;
  BUILD-IDENTITY meldet vorbestehende Arbeitskopieaenderungen. Keine Hardwarefreigabe.
- Gemeinsamer MSI-/ZIP-Kandidat artifacts/releasecheck-20260914-fixed:
  1024 Dateien im ZIP und administrativ extrahierten MSI hashgleich, Manifeste OK.
  Enthaelt die neueren Start-/Recovery-Korrekturen noch NICHT. Keine Installation.
Historische Details/Hashwerte: RELEASE-READINESS-2026-09-14.md.

## Aktuelles gemeinsames Paket 17.09.
artifacts/releasecheck-20260917/package, 1.1.0-releasecheck.20260917 (MSI 1.1.17),
Studio 977741b, unveraenderte getestete Runtime. Enthaelt alle nachfolgend
beschriebenen UI-Korrekturen vom 16.09. Build Exit 0; exakter Kandidat im
UI-Smoke Exit 0, 37 Checks/5 Szenarien, stderr leer. ZIP und administrativ
extrahiertes MSI jeweils 1027 Dateien hashgleich, drei Paketmanifeste geprueft.
RELEASE-CANDIDATE-2026-09-17.md dokumentiert Hashwerte und offene Freigaben.
Kein finales Release, keine Installation. Versuch vom 16.09. war vor fertiger
Paketierung unterbrochen; nicht ausliefern. Unveraenderten Kandidaten nicht neu bauen.

## Vorheriges gemeinsames Paket 15.09.
artifacts/releasecheck-20260915/package ist fertig und geprueft, Version
1.1.0-releasecheck.20260915. Enthaelt aktuelle Vault-/Recovery-Korrekturen.
Studio f8df5c9, Runtime unveraendert wiederverwendet. Build Exit 0, 35 UI-Checks
und 5 Kodierszenarien mit exakt diesem Studio/Runtime-Kandidaten bestanden.
MSI administrative Extraktion Exit 0; 1025 Dateien dort und im Portable-ZIP
hashgleich zum Releasebaum. Alle drei Paketmanifeste geprueft. Keine Installation.
Details/Hashes: RELEASE-CANDIDATE-2026-09-15.md. Kein erneuter unveraenderter Build.

## Native Dialogabnahme: Teilabnahme 16.09. bestanden
Interaktiver Modus --ui-test-workspace verwendet denselben isolierten SettingsStore
und synthetischen Profilbestand wie der Smoke-Test; Fenster mit Zusatz Testdaten.
Build 1.1.0-releasecheck.20260915.3 unter artifacts/native-dialog-check-20260915/Studio
veroeffentlicht und gestartet; getrennte Settings unter tmp/native-dialog-check,
stderr leer. Fenster durch Computer Use list_windows eindeutig gefunden.
Die fruehere Computer-Use-Freigabeblockade ist behoben. Am 16.09. wurden echte
Windows-Dateidialoge bedient: Projekt Speichern unter, erneutes Oeffnen/Abbrechen,
keywertfreier Kodiervorschrift-Export und kontrollierter JSON-Grundprofilimport.
Dateien im isolierten Testordner nachgeprueft; Projekt nach Abbruch hashgleich.
Testfenster anschliessend geschlossen, stderr leer. Details und Grenzen:
NATIVE-DIALOG-ACCEPTANCE-2026-09-16.md. Keine vollstaendige UI-Paritaetsabnahme.
Gefundene falsche Vault-Erfolgsmeldung beim Projektspeichern im Source korrigiert;
Kandidat vom 15.09. enthaelt diese reine Textkorrektur noch nicht.
Get-Command fand keine WindowsSandbox.exe/vmconnect.exe/VBoxManage.exe/docker.exe;
das ist noch kein vollstaendiger Nachweis, dass keine Test-VM existiert.
Gezielte Folgepruefung 16.09.: VirtualBox 7.2.16r174877 ist unter
C:/Program Files/Oracle/VirtualBox installiert (nicht im PATH). VBoxManage
list vms/list runningvms lief erfolgreich, beide Listen leer. Im Workspace
keine ISO/OVA/VBOX/VDI/VHDX gefunden. Keine VM angelegt oder gestartet.
PnP meldet Gemalto Virtual PCSC Multi-slots Reader und Alcor Micro USB Smart
Card Reader mit Status OK. Das belegt weder einen passenden kontaktlosen
DESFire-Reader noch eine geeignete Testkarte; keine Kartenkommunikation gestartet.
Gemeinsames Testpaket ist inzwischen fertig (siehe oben). Echte Installation/
Upgrade weiterhin nur in isolierter Umgebung.

## Naechste sinnvolle Arbeiten
Arbeitsplatz-Einrichtung 22.09. auf Benutzerwunsch vereinfacht: Einstellungen
zeigen vier Schritte fuer gemeinsame Ordner, eigene Identitaet/Team, lokale
Leserkonfiguration und erstes Testprojekt samt Recovery. Neuer Button liest die
gespeicherte Leserkonfiguration erneut, ohne Leser-/Kartenkommunikation.
Providerwahl wird klar von gepruefter Verbindung unterschieden; Fehler aus der
Konfigurationsinspektion sind sichtbar. Teamstatus behauptet keine vollstaendige
Mehrbenutzerabnahme; unzutreffender Tooltip zur angeblich hardwaregeprueften
gebuendelten Runtime entfernt. Keine automatische Konfigurationsaenderung.
WPF-Build Exit 0, 0 Warnungen/Fehler; bestehender UI-Smoke Exit 0, 40 Checks und
5 Szenarien, stderr leer. tmp/workplace-setup-build.log und
tmp/workplace-setup-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Neue native visuelle Bedienabnahme und echter Readercheck noch offen. Kein
weiteres Release gebaut; 1.2.1 bleibt unveraendert. Recovery weiterhin pro Vault.

Vault-Bedienhilfe 22.09. nach Benutzerfeedback: TeamVaultWindow erklaert getrennt
lokale Identitaet, Teamvorlage und bestehende Vaultfreigaben. Aufklappbare Anleitung
fuer Einzelbenutzer, Kollegen/Verantwortliche, bestehende Projekte und doppelte
Identitaeten. Ausgewaehlter Zugang zeigt seine Zugehoerigkeit zur Teamliste.
Status behauptet keine Mehrbenutzerbereitschaft allein wegen zweier Identitaeten.
Entfernen aus Teamliste, Loeschen eines Zertifikats und verbleibender Zugriff auf
alte Kopien werden unterschieden. VaultAccessWindow weist auf Wiederverwendung
vorhandener Identitaeten und projektbezogenen Geltungsbereich hin.
Keine Zugriffs-/Kryptoaenderung, keine echten Identitaeten oder Teamlisten geaendert.
WPF-Build Exit 0, 0 Warnungen/Fehler; UI-Smoke Exit 0, 40 Checks/5 Szenarien,
stderr leer. tmp/team-vault-guidance-build.log und
tmp/team-vault-guidance-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Native visuelle Abnahme der neuen Hilfe offen. Veroeffentlichter Release 1.2.0
bleibt unveraendert; diese Verbesserung ist noch nicht im Installer enthalten.

Importmeldung 22.09. im Source verbessert: ImportBaseProfile_Click behandelt
JsonException mit deutschem Hinweis zu ungueltigem JSON/Datenfeld, optionaler
1-basierter Fehlerzeile und Aufforderung, die Datei zu pruefen oder eine gueltige
Profildatei auszuwaehlen. Parserexception bleibt als InnerException erhalten;
ShowError zeigt nur den deutschen Kontext. Validierung/Speicherpfad unveraendert.
Gezielter WPF Release-Build Exit 0, 0 Warnungen/Fehler:
tmp/profile-import-message-build.log. Keine zusaetzlichen Tests fuer reine
Meldungsaenderung; neue Meldung noch nicht visuell abgenommen. Bestehender
Kandidat 20260917 unveraendert, enthaelt diese Verbesserung noch nicht.

Praktische Import-Fehler-/Abbruchabnahme 22.09. am unveraenderten gemeinsamen
Kandidaten 20260917 (Studio 977741b), --ui-test-workspace: Expertenansicht,
Profile und Historie, Aus JSON importieren. Absichtlich syntaktisch ungueltige
synthetische Datei im nativen Dateidialog ausgewaehlt. Fehlerdialog erscheint,
kein Absturz; nach OK erneut oeffnen und Escape kehrt zur Profilverwaltung zurueck.
Regulaeres Fensterschliessen erfolgreich, Prozess 28688 beendet, stderr 0 Bytes.
Isolierter Datenordner:
tmp/practical-import-20260922/SieEncode/startup-test-bf968a5f8457441d92690cfcada6fcf1.
Profilbestand danach ausschliesslich Synthetic-AES-Mixed.base.json, hashgleich
zur Testfixture: 7A6CC7A9C8A646962EBEDB4A405462976D28F54832CE8BFF0F0442C1BEC8A66F.
Keine importierte Teildatei, keine produktiven Daten/Karten verwendet.
Offener Bedienhinweis: JSON-Fehlerdialog zeigt noch rohe englische Parsermeldung;
verstaendlichen deutschen Kontext ergaenzen. Keine vollstaendige UI-Abnahme,
kein Neubuild und keine Installation durch diesen Test belegt.

Ergaenzende Vault-Anleitungsregression 21.09.: vorhandener gesperrter v2-Vault
zeigt Entsperrhinweis statt Pflichtfeldfehler. Entsperrabbruch bewahrt Vaultbytes,
gesperrten Status und Bausperre. Nach echtem synthetischem Unlock wird ein
ungueltiger Grundschluessel separat angezeigt und verhindert Neubau. Originalkey
wiederhergestellt und Vault erneut gespeichert; anschliessende v3-/Recoverykette
weiter erfolgreich. Nur Tests ergaenzt, kein weiterer Produktfehler gefunden.
Build Exit 0, 0 Warnungen/Fehler; UI-Smoke Exit 0, 40 Checks/5 Szenarien, stderr
leer. Logs: tmp/locked-vault-guidance-build.log und
tmp/locked-vault-guidance-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Kandidat vom 17.09. bleibt unveraendert; er enthaelt weder die Anzeigeaenderung
vom 21.09. noch diese Tests. Keine praktische Mehrbenutzer-/Hardwarefreigabe.

Vault-Anzeige 21.09. korrigiert: Transport-/Produktiv-Keyfehler zaehlen fuer die
Darstellung nicht mehr als Pflichtfeldfehler. Fehlender Vault bei bestehender
Definition erhaelt eigene Statusanzeige und Wiederherstellungs-/Neuanlagehinweis.
Echte Feldfehler bleiben priorisiert; geladene ungueltige Grundkeys werden weiter
angezeigt. Vollstaendige definitionIssues bleiben unveraendert Teil der Bausperre.
Regression: keyfreie bestehende Definition ohne Vault und danach leerer Name;
korrekte Anzeige und gesperrter Build in beiden Faellen. WPF-Build Exit 0,
0 Warnungen/Fehler; UI-Smoke Exit 0, 39 Checks/5 Szenarien, stderr leer.
Logs: tmp/missing-vault-guidance-build.log und
tmp/missing-vault-guidance-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Darstellungsbefund der praktischen Teilabnahme damit im Source behoben;
Kandidat 20260917 unveraendert, enthaelt diese neue Korrektur noch nicht.

Praktische UI-Teilabnahme 21.09. am unveraenderten Kandidaten 20260917:
Neuer synthetischer Entwurf; Fensterschliessen zeigt Verlustwarnung, Nein erhaelt
den Entwurf. Nach temporaerem Benutzerabbruch per Escape erst auf neues Weiter
fortgesetzt. Zwischenspeichern, Projekt schliessen, ueber Bibliothek Bearbeiten
wieder oeffnen und Anwendung regulaer beenden erfolgreich. Korrigierte
Speicherstatusmeldung sichtbar. Kein Vault/keine Karte verwendet, stderr leer.
Projektdatei mit erhaltenen Werten und leeren PICC-Keywerten nachgeprueft:
tmp/practical-close-20260921/SieEncode/startup-test-f12342bdfd5b4b4aaf9ee6839656cadb/
projects/Neues Projekt/Neues Projekt.project.json, SHA256
3857CE4D59966DD7223E39D650B9F3A4028606AE8FB6200BB16BC571E3131EBF.
Neue Darstellungsauffaelligkeit fuer Folgepruefung: Wiedergeoeffnetes Projekt ohne
Vault meldet Schluessellaengenfehler unter Pflichtfelder statt einer klaren
Vault-Anweisung. Kein Datenverlust festgestellt; keine vollstaendige UI-Abnahme.

Zusatzprofil-Bedienregression 17.09.: synthetisches Kundenprofil in isolierter
Bibliothek gespeichert, im offenen Editor ausgewaehlt, eigenes Projektfeld
angezeigt; geaenderte Grund-/Zusatzprofilwerte und Auswahl bleiben beim Refresh
erhalten. Abwahl entfernt nur das Zusatzprofilfeld, der andere Wert bleibt.
WPF-Build erfolgreich; finaler UI-Smoke Exit 0, 38 Checks/5 Szenarien, stderr leer.
Erster Lauf wies die unvollstaendige Testfixture wegen fehlender Laengenregel ab;
Fixture korrigiert, kein Produktfehler. Logs: tmp/customer-flow-build-final.log,
tmp/customer-flow-smoke-final/SieEncode/ui-smoke/ui-smoke-result.json.
Nur Testcode erweitert, kein neues Paket erforderlich. Kandidat vom 17.09.
bleibt unveraendert bei seinen dokumentierten 37 Checks. Kein nativer Importdialog
und keine vollstaendige praktische UI-Abnahme durch diese Regression belegt.

Profilauswahl-Korrektur 16.09.: Refresh aktualisiert nun auch Grund-/Zusatzprofil-
Auswahllisten im bereits offenen Editor (zuvor nur dynamische Felder).
Gemeinsamer Auswahlaufbau bei Laden und Refresh, mit unterdrueckten Change-Events;
bestehende Auswahl wird aus dem Projekt wiederhergestellt. Regression importiert
ein synthetisches Grundprofil in die isolierte Bibliothek und prueft sofortige
Verfuegbarkeit sowie unveraenderte Auswahl, Bindung und clean-Status.
WPF-Build Exit 0, 0 Warnungen/Fehler; UI-Smoke Exit 0, 37 Checks/5 Szenarien,
stderr leer. Nachweise: tmp/profile-choices-build.log und
tmp/profile-choices-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Entwicklungsbuild mit Defaultkennung 1.1.0-rc15, kein neues Releasepaket.
Kandidat vom 15.09. enthaelt diese Korrektur noch nicht.

Profil-Refresh-Korrektur 16.09.: Nach unveraenderter Synchronisierung bleibt ein
sauberes Projekt sauber. Vorbestehende Aenderungen bleiben erhalten; tatsaechliche
Synchronisierungsaenderungen werden weiterhin speicherpflichtig. Ursache der
unnoetigen Schliessen-Nachfrage aus der nativen Importabnahme behoben.
WPF Release-Build Exit 0, 0 Warnungen/Fehler; isolierter UI-Smoke Exit 0,
36 Checks und 5 synthetische Kodierszenarien, stderr leer. Neue Regression prueft
unveraenderten Refresh, bestehendes dirty und wiederhergestelltes Pflichtfeld.
Nachweise: tmp/profile-refresh-build.log und
tmp/profile-refresh-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Entwicklungsbuild traegt mangels Versionsoverride 1.1.0-rc15; kein neues RC15-Paket
und keine Hardwarefreigabe. Kandidat vom 15.09. enthaelt diesen Fix noch nicht.

1. Native Import-/Export-/Speicherdateidialoge und UI-Paritaet als Anwenderablauf
   abschliessen; synthetische Source-/Buttonpruefungen nicht als Vollabnahme ausgeben.
2. Isolierte Windows-Installation/Upgrade/Deinstallation: passende Umgebung bisher
   nicht bestaetigt. Nie MSI /i auf produktivem Host: gleiche UpgradeCode wie RC15.
3. Zweiter Benutzer/Rechner fuer echten Mehrbenutzer-Vault; Reader/Testkarte fuer
   ChangeFileSettings und kompletten Bedienlauf. Fehlende Nachweise offen lassen.
4. Zum naechsten sinnvollen Freigabestand gemeinsames Paket aktualisieren, erneute
   Hash-/Paketpruefung. Neue Runtime-Hashes erben keine bisherigen Hardwarefreigaben.
5. Erst bei belegten Kriterien finales internes Release. Code-Signing/externer
   Pilot weiter zurueckgestellt. Keine erneute Inventur bestandener Vaultpfade.

Automation sieencode-ui-und-pdf-fertigstellen: stuendlicher Heartbeat dieser Aufgabe.
Keine doppelten Aufgaben/Automationen. Nur wesentliche Meilensteine/Fehler/erforderliche
Benutzeraktionen melden. Nach echtem Abschluss oder wiederholt unloesbarer Blockade
konkret berichten und pausieren; keine endlosen unveraenderten Pruefungen.

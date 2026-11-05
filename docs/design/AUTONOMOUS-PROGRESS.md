# Autonome Weiterarbeit - Stand 2026-09-15

## Auftrag und Grenzen
Erweiterter Benutzerauftrag: bis zum vollstaendig geprueften internen Release
weiterarbeiten; Ziel bei Bedarf fortsetzen. Anschliessend eine bebilderte deutsche
Ersteinrichtungsanleitung fuer Kollegen erstellen (vom Benutzer alternativ zu
einem Video akzeptiert). Den tatsaechlich abgenommenen Ablauf zeigen: Installation,
gemeinsame Ordner, eigene Identitaet/Einladung, Teambeitritt mit zentralen Gruppen,
Offlinezugriff, getrennte Recovery, Leser-Einrichtung und erster sicherer Test.
Rollen von Hauptverwaltung und normalen Kollegen klar trennen. Screenshots nur
aus synthetischer Testumgebung, ohne Passwoerter/private Schluessel/Recoverycodes.
Anleitung ist Teil des Abschlusses, kein Ersatz fuer offene Abnahmen.
Bestehendes Ziel vom Benutzer wieder aktiviert; aktive Fortsetzung bestaetigt.
Kein neues Ziel und keine neue Automation anlegen. Abschluss erst nach Abnahme.

05.11.2026 Reales MSI-Upgrade Kandidat 1 -> 1.3.0-offline-groups.2 bestanden:
MSI Exit 0, installierte DLL C84C1772... exakt erwartet. Vorher-/Nachherpruefung
in allen drei geladenen Profilen PASS: labadmin 7 Dateien/1 RSA-Identitaet,
testerA 69/2, testerB 104/2. Alle Dateihashes und Zertifikatinventare unveraendert,
RSA-OAEP-SHA256 weiter nutzbar. Temporaere Anmeldedatei separat als entfernt
bestaetigt, Upgradeprozess beendet. Keine native Bedienabnahme daraus ableiten.
Nachweis offline-groups-candidate2-upgrade-result.json. Helfer ergaenzt um
BeforeUpgrade/AfterUpgrade, gebunden an alte Baseline, Benutzer-SID und exakten
neuen DLL-Hash; Originalbelege nicht ueberschrieben. Nativ Projekt wieder oeffnen,
normal/Recovery entsperren und Kopie-Auswahlkorrektur pruefen bleibt naechster Schritt.
Vorbereiteter Launcher tmp/Start-Candidate2WithProfile.ps1 noch nicht ausgefuehrt.
05.11.2026 RSA-Providerfehler als Startkontextproblem eingegrenzt: derselbe
Windows-PowerShell-Pruefer besteht mit ProcessStartInfo.LoadUserProfile=true.
Guestcontrol --profile allein reichte fuer testerA nicht. Keine Zertifikate oder
Schluessel ersetzt. Capture PASS: labadmin 7 Dateien/1 RSA-Identitaet,
testerA 69/2, testerB 104/2. Temporaere Anmeldedatei danach separat als entfernt
bestaetigt. Gemeinsame Fixtures unter beiden berechtigten normalen Benutzern.
Echter Kandidat-1-Deinstallations-/Wiederinstallationstest abgeschlossen, PID 4948 beendet,
Protokoll C:/SieEncodeLab/Evidence/preservation-20261105/lifecycle.log. Ergebnis
PASS: MSI Deinstallation und Wiederinstallation jeweils Exit 0. Nicht wiederholen. Installer hashgeprueft, App geschlossen,
alle drei Baselines vorhanden, VerifyRemoved und VerifyRestored unter allen drei Konten PASS. DLL-Hash nach Wiederinstallation exakt Kandidat 1; temporaere Anmeldedatei entfernt. Nachweis offline-groups-uninstall-preservation-result.json. Kandidat-2-Upgrade und natives Wiederoeffnen bleiben offen.
05.11.2026 Installer-Datenerhalt begonnen, noch keine Deinstallation/Upgrade:
VM laeuft, Worktree vor Test sauber. Shared-VaultProbe1-ACL erlaubt testerA/B,
Administratoren und SYSTEM; labadmin-Gastprozess hat gefilterten Token und
kann Shared nicht lesen. Pruefhelfer erfasst Shared deshalb ausdruecklich unter
beiden normalen Benutzern, labadmin nur eigene Daten; ScopeRoots im Nachweis.
Keine ACL geaendert. Labadmin Capture PASS: 7 Dateien, 1 nutzbare RSA-Identitaet.
TesterA Capture bricht vor Baseline-Schreiben ab: GetRSAPrivateKey meldet unter
Windows PowerShell fuer beide RSA-Zertifikate "Ungueltigen Anbietertyp angegeben".
Separate Diagnose bestaetigt HasPrivateKey=true fuer Synthetic VM testera und
Separate Group Recovery, aber denselben Providerfehler bei beiden. TesterB noch
nicht ausgefuehrt. Kein Schluessel geloescht/exportiert, kein Installer gestartet.
Naechster Schritt: Provider-/Prozessumgebung gegen installierten .NET-App-Zugriff
pruefen; Fehler nicht ueberspringen. Bestehende labadmin-Baseline nicht ersetzen.
Parser und git diff --check fuer Helferanpassung bestanden.
05.11.2026 Kandidat 2 fertig gebaut und lokal geprueft: Paketversion
1.3.0-offline-groups.2, MSI ProductVersion 1.3.0, Quelle 8dcde39 (sauber).
Publizierter UI-Smoke OK/60 mit exakter neuer Version. MSI administrativ
extrahiert, alle 1076 Release-Dateien hashgleich. Runtime 495 Dateien bytegleich
zu Kandidat 1, dessen Paketpruefsummen unveraendert. Exakte Identitaet in
OFFLINE-GROUPS-CANDIDATE-1.3.0-2.md und offline-groups-candidate2-verification.json.
WiX ICE40/ICE61 Hinweise wie dokumentiert; Build Exit 0. Kein portables Paket,
keine Installation auf Host/VM, keine neue Releasefreigabe. VM laeuft; separate
Abfrage bestaetigt keine SieEncode-Prozesse und weiterhin Kandidat-1-DLL.
Naechster Schritt: Daten-/Identitaetsbaseline vor Installerlebenszyklus und
Upgrade erfassen; Deinstallationserhalt-Helfer ist noch an Kandidat 1 gebunden.
GitHub-Push weiterhin nicht wiederholt nach frueherer Ablehnung.
05.11.2026 Konkreten UI-Kontextfehler aus nativer Kopierabnahme behoben:
Nach Kopie/Neuanlage lud der Editor das neue Gruppenprojekt, RefreshProjects
waehlte jedoch das alte Bibliotheksprojekt erneut. Kopfzeile und nachgelagerte
Projektaktionen konnten weiter auf das Original zeigen. Gemeinsamer Ladepfad
waehlt nun den neu angelegten Katalogeintrag wie beim normalen Speichern.
Gezielte UI-Regression prueft Kopie-Pfad, Kopfzeile, Abnahmepfad und gesperrten
Vault. Test-CI Exit 0, 72/72; isolierter UI-Smoke Exit 0, OK, 60 Checks.
Nachweis: offline-groups-copy-selection-fix-result.json. Gebaute lokale Quelle
traegt die Standardversion 1.1.0-rc15; dies ist kein RC15-Releasepaket.
Installierter Kandidat 1.3.0-offline-groups.1 bleibt unveraendert und enthaelt
diese Korrektur noch nicht. Neues MSI mit eigener Identitaet und gezielte
Upgrade-/Bedienabnahme erforderlich, bevor die Korrektur freigegeben wird.
04.11.2026 Recovery der nativen Projektkopie und Inhaltsschluesselrotation
bestanden: separater Recovery-Button, Integritaetspruefung und Build-Sperre
beobachtet (Bild 25). Empfaenger-Vorschau zeigt keine Hinzufuegungen/Entfernungen;
bestaetigte Neuverschluesselung erzeugt verschluesselte Altsicherung (Bild 26).
Deren Hash entspricht exakt dem Kopie-Vault vor Rotation. Projektdatei bleibt
hashgleich; neuer Vault-Hash und Details im nativen Projektnachweis. Recovery-
Integritaetspruefung danach erneut erfolgreich. Normales Entsperren angeklickt,
aber Ergebnis durch Fenstergroessenwechsel/Hosteingaben verdeckt: bleibt offen.
Dies belegt Rotation bei unveraenderten Empfaengern, keinen Rechteentzug.
04.11.2026 Native Projektkopie bestanden: VM lief, SieEncode war geschlossen.
Geprueften Kandidaten als testerB gestartet (PID 4832), Profil und Entfernung
der temporaeren Anmeldedatei separat bestaetigt. Vorhandenes Gruppenprojekt
wieder geladen und normal entsperrt. Speichern unter erzeugte vollstaendige
Kopie mit neuer ID 6fd126e2-5401-45d9-9c6f-d7c75dccd081; gleiche Bibliothek/Gruppe.
Kopie nativ normal entsperrt. Originalprojekt und Originalvault separat gehasht:
beide identisch mit Nachweis vom 03.11. Bild 24 und Metadaten im nativen
Projektnachweis. Recovery der Kopie noch offen: Hostfenster verdeckte Klick,
Aktivierung meldete Benutzereingabe; kein erfolgreicher Recoveryklick behauptet.
Weiterhin offen: nativer Empfaengerwechsel, Delegationsgesamtweg,
Deinstallation/Datenbewahrung und abschliessende Anleitungskontrolle.
03.11.2026 Nativer Gruppenprojekt-/Recoverylauf bestanden: unter testerB ein
neues synthetisches Grundprofil mit AES-Application 000001 nativ erstellt und
gespeichert. Projektentwurf, zufaellige Schluessel und Gruppenprojekt samt Vault
nativ erzeugt: Neues Projekt Gruppe, Bibliothek d9c6..., Revision 4, erste Gruppe.
Separater Recovery-Button entsperrt; Recovery-Zugang pruefen bestaetigt Zugang,
Projektbindung und Integritaet. Build bleibt sichtbar gesperrt. Ausdrueckliches
normales Entsperren aktiviert Build wieder. Bilder 22/23 und Projekt-/Vaulthashes
in offline-groups-native-project-result.json. Beide getrennten Identitaeten liegen
im selben Testprofil: keine neue Recovery-only-/zweite-Maschinen-Abnahme behauptet.
Projektkopie, Empfaengerwechsel und Installer-Deinstallation weiter offen.
03.11.2026 Nativen Projektlauf begonnen: testerB besitzt noch keine Projekte.
Neues Projekt meldet: Die Profilbibliothek enthaelt kein Grundprofil.
Gegen docs/GRUNDPROFILE.md und ProjectDraftFactory geprueft: Installer liefert
absichtlich kein fachliches Profil; kein Vaultfehler. Noch kein Testprojekt
angelegt. Fuer Fortsetzung synthetisches Grundprofil im Testprofil erstellen
oder importieren, bestehende Identitaeten/Dateien erhalten. Parallele
Hosteingaben unterbrachen VM-Bedienung; keine weiteren Klickversuche.
Kollegenanleitung um getrennten Recovery-Ablauf mit Originalbild 21 und Hilfe
bei fehlendem Grundprofil erweitert. Zielaudit auf tatsaechlich abgeschlossene
native Registrierung und Recovery-Schluesselwiederherstellung aktualisiert.
Visuelle Gesamtabnahme der Anleitung und native Projekt-Recovery bleiben offen.
03.11.2026 Nach VM-Neustart Kandidat unveraendert als testerB mit geladenem
Benutzerprofil erneut gestartet (PID 5856); temporäre Anmeldedatei danach
unabhaengig als entfernt bestaetigt. Benutzer stellte vorhandene Recovery-
Sicherung im nativen Dialog wieder her. Erfolg selbst beobachtet, Screenshot 21.
Separater Windows-Prozess unter testerB findet exakt erwarteten Recovery-
Fingerprint mit privatem Schluessel; RSA-OAEP-SHA256 Roundtrip erfolgreich.
Verschluesselte Sicherung weiterhin vorhanden, SHA256 im Recovery-Nachweis.
Keine Passphrase/privaten Schluessel exportiert. Projekt-Recovery bleibt offen.
Vorheriger Push von e2330cb wurde vom Benutzer abgelehnt; kein erneuter Push.
03.11.2026 Recovery zentral nativ freigegeben: Benutzer bestaetigte Fingerprint,
Verwaltung zeigt Separate Team-Recovery aktiv und Revision 4 (Screenshot 20).
Oeffentlichen Stand exportiert und unabhaengig mit hashgeprueftem Kandidaten-Core
validiert: Ursprungsverwaltung, genau erwarteter aktiver Recovery-Fingerprint,
Personen/Gruppen/Delegationen gegen Revision 3 unveraendert. Nachweis
offline-groups-native-recovery-result.json. Nach erneuter ausdruecklicher
Freigabe der VM-Bedienung Recovery-Assistent geoeffnet, d9c6... gewaehlt und
freigegebener Recovery-Schluessel zur Wiederherstellung angeboten. Benutzer um
Passphrase und Dokumente/recovery.group-private.json gebeten. Windows-Installation
dieses Recovery-Schluessels und Projektentschluesselung noch offen.

03.11.2026 VM wieder sichtbar verfuegbar, gespeicherter Recovery-Dialog erhalten.
Erfolgreiche private Sicherung erneut beobachtet und Screenshot 19 gespeichert.
Oeffentliche recovery.recipient.json nativ unter testerB/Documents exportiert.
Zentrale Verwaltung d9c6... mit Revision 3 geladen und Recovery-Einladung
ausgewaehlt. Benutzer um Eingabe des zuvor im Recovery-Assistenten angezeigten
Fingerprints A44D1E2A656C8864DAADBD3598F3249A66863613C04BCFCF252A4E0480CD83B8
gebeten; zentraler Freigabeerfolg noch offen. Oeffentliche Datei separat zum Host
abgeholt: reines Zertifikat ohne privaten Schluessel, SHA256 des Zertifikats stimmt
mit angezeigtem Fingerprint und Einladungsfeld ueberein. Keine private Datei
ins Repository kopiert; Passphrase bleibt beim Benutzer.

03.11.2026 Fortsetzung: VBox showvminfo meldet VMState=saved, seit
2026-11-02T12:21:25Z. Sichtbarer Start per startvm --type gui wurde mit
"rejected by user" abgelehnt; kein Ersatzweg/erneuter Startversuch.
Am 02.11. hatte der Benutzer die Recovery-Sicherung abgeschlossen. Native
Erfolgsmeldung selbst beobachtet: private Sicherung gespeichert und
wiederhergestellt geprueft, noch kein Windows-Schluessel installiert und
keine Freigabe erteilt. Das unten genannte Warten auf diese Eingabe ist erledigt.
Oeffentlicher Recovery-Export, zentrale Freigabe, Windows-Wiederherstellung
und Gruppenprojektablaeufe bleiben offen. Gespeicherten VM-Zustand erhalten.

02.11.2026 Recovery-Gesamtablauf vorbereitet: Verwaltungsdialog nach gespeichertem
Stand 3 geschlossen, separate Recovery-Einrichtung unter testerB geoeffnet und
d9c6ffd7-0081-4189-b965-518c9204c10c ausgewaehlt. Benutzer um eigene Testpassphrase,
private Sicherung unter Documents/recovery.group-private.json und offene
Erfolgsmeldung gebeten. Antwort steht aus; keine erzeugte Recovery-Identitaet
oder Sicherung behaupten. Danach oeffentliche Einladung nativ exportieren,
Fingerprint dokumentieren, zentral freigeben und Wiederherstellung pruefen.

02.11.2026 Personen-/Geraete-/Gruppenzuordnung nativ bestanden: in neuer d9c6...
Verwaltung Testperson B angelegt (Name durch Benutzer, da Texteingabe VM nicht
erreicht). Vorhandenes oeffentliches testerB-Zertifikat per PowerShell als reine
Importfixture in Documents/native-testerB.recipient.json exportiert, keine neue
Identitaet/kein privater Export. Fingerprint aus Quellzertifikat
EEB1B9D4AE2ACE50174B5E0948D0A86A7A4EE4AE5DDA19081918A043EE1C0414
gegen nativen Importdialog abgeglichen. Geraet importiert, zwei neue Gruppen
nativ angelegt und dieselbe Person beiden zugeordnet. Revision 3 signiert und
oeffentlich exportiert. Kandidaten-Core validiert Signatur, Ursprungsverwaltung,
genau eine aktive Person/ein aktives Geraet sowie dieselbe Personen-ID in beiden
Gruppen. Ergebnis offline-groups-native-people-result.json, Screenshot 18.
Gruppen tragen noch Testnamen Neue Gruppe, unterscheiden sich durch stabile IDs.
Keine Projektentschluesselung oder unabhaengiger menschlicher Vertrauenskanal
damit behauptet. Neue Bibliothek besitzt weiter keinen Recovery-Zugang.
Naechster nativer Schritt: separaten Recovery-Zugang einrichten/aufnehmen,
danach Gruppenprojekt erstellen und Empfaenger-/Recoveryablaeufe pruefen.

02.11.2026 Wiederherstellungs-Persistenz nativ bestanden: App regulaer ueber
Gast-Taskleiste geschlossen, kein SieEncode-Prozess mehr per Gastabfrage.
Danach mit LoadUserProfile=true als testerB neu gestartet, PID 4500,
FixturesCopied=0. Verwaltung erneut geoeffnet, d9c6... ausgewaehlt:
gespeicherte Revision 2 und urspruenglicher Fingerabdruck sichtbar, eigener
signierter Stand lokal aktiv. Screenshot 17. Vorherige offene Persistenzpruefung
damit erledigt; keine vollstaendige native Gruppen-/Projektabnahme behauptet.

02.11.2026 Native private Wiederherstellung unter testerB erfolgreich: Benutzer
gab seine Testpassphrase ein, Erfolgsmeldung selbst beobachtet (Screenshot 15).
Neue Bibliothek d9c6ffd7-0081-4189-b965-518c9204c10c danach im Verwaltungsdialog
geladen und ohne Inhaltsaenderung als Revision 2 signiert (Screenshot 16).
Oeffentlichen Export nativ im B-Dokumenteordner gespeichert, per VBox abgeholt
und mit hashgeprueftem Kandidaten-Core unabhaengig validiert. Signatur und
urspruenglicher Fingerprint 9A6F17C6...D72CCD stimmen; maschinenlesbarer Nachweis
offline-groups-native-restore-result.json. Keine Passphrase/private Sicherung im
Repository. App-Neustart/Persistenz dieser Wiederherstellung noch offen.
Die weiter unten beschriebene ausstehende Passphrase ist damit erledigt.

02.11.2026 Deinstallations-Pruefhelfer gehaertet: VerifyRestored akzeptiert nicht
mehr die blosse Existenz von VerifyRemoved.json. Phase, Windows-SID, Kandidatenhash
und SHA256 der Capture-Baseline muessen passen. Parser geprueft; isolierte Pruefung
der tatsaechlichen Validierungsfunktion akzeptiert passenden Datensatz und weist
vier Abweichungen (je ein Feld) ab. Kein Gast-/Deinstallationstest damit behauptet.
Native private Wiederherstellung wartet weiterhin auf die Testpassphrase des Benutzers.

02.11.2026 Fortsetzung: VM laeuft, App unter echtem testerB-Profil gestartet
(PID 5680), temporaere Testzugangsdatei nachweislich entfernt. Oeffentliche
Datei der neuen d9c6...-Verwaltung ist seit 30.10. nativ exportiert und nach B
uebertragen. Heute mit hashgeprueftem Kandidaten-Core validiert: Revision 1,
Fingerprint 9A6F17C6C287614C8C140B8328276F6C92918F802260C5771EAD4653B2D72CCD,
Export-SHA256 7D65FBC8642F5EF3B443E95A5D58F16C726816BE1532B9AE6DF1B2E16419B87B.
Passt zum zuvor nativ angezeigten Fingerabdruck. Screenshot 14 belegt erneutes
Laden unter labadmin nach App-Neustart. B besitzt anfangs weiterhin nur 494f...
Trust/Admin; neue d9c6... noch nicht vorhanden. Registrierungsdialog unter B
mit neuer Datei geoeffnet. Benutzer um Fingerprinteingabe/Bestaetigung gebeten,
weil automatisierte Texteingabe im Gast nicht verlaesslich funktioniert.
Registrierung vom Benutzer erfolgreich abgeschlossen und nativ bestaetigt. Restore-Dialog bietet neue d9c6... nun an; richtige Bibliothek ausgewaehlt. Benutzer um Eingabe seiner geheim bleibenden Testpassphrase und Wiederherstellung aus Dokumente/verwaltung-native-test.group-private.json gebeten. Privater Restore und anschliessende Signatur-/Persistenzpruefung noch offen.

30.10.2026 Private Erstverwaltungssicherung nativ bestanden, Benutzer gab eigene
Testpassphrase ein. Sichtbarer Erfolg: gespeichert, unveraendert zurueckgelesen,
Schluesselfunktion nach Entschluesselung geprueft. Screenshot 13. Gastdatei
labadmin/Documents/verwaltung.group-private.json, 4114 Bytes, SHA256
5B8B2A748E1C35B2EE41F0BFF3D0478F4E8FC78CFFD0D8CBA05FBCC23F693DAD.
Verschluesselte Kopie ausserhalb Repository unter Testlab/Private sowie in
testerB/Documents/verwaltung-native-test.group-private.json bereitgestellt.
Labadmin-App regulaer geschlossen, App unter echtem testerB-Profil (PID 6552,
LoadUserProfile=true) gestartet. Temp-Credentialdatei nachweislich entfernt.
Restore-Dialog bietet nur bereits registrierte 494f...-Bibliothek an; fuer neue
d9c6... zuerst deren oeffentlichen Stand exportieren und unter B explizit
registrieren. Restore ohne Aenderung geschlossen, Verwaltungsdialog B offen.
Keine Wiederherstellung bestanden behaupten. Testpassphrase nur beim Benutzer;
automatisch erzeugte, nicht erfolgreich eingegebene Testpassphrase verworfen.

30.10.2026 Native Rollentrennung geprueft: unter labadmin ohne privaten
Verwaltungsschluessel war keine Bibliothek verwaltbar; Gruppen signiert speichern
wurde mit "Bitte eine Verwaltung auswaehlen oder erstellen" abgewiesen.
Screenshot 12 gesichert. Danach separate synthetische Erstverwaltung nativ
angelegt: d9c6ffd7-0081-4189-b965-518c9204c10c. Neue .admin.bin und .trust.bin
per Dateiinventar bestaetigt, bestehende Registrierung 494f... weiterhin vorhanden.
Keine neue Berechtigung an der bisherigen A/B-Bibliothek. Privaten Sicherungs-
dialog geoeffnet; automatisierte Texteingabe bleibt leer. Benutzer um zweimalige
Eingabe einer eigenen Testpassphrase und Speichern gebeten; Antwort/Erfolg noch
offen. Keine Sicherung oder Wiederherstellung als bestanden behaupten.

30.10.2026 Vorbereitung fuer echte Deinstallations-/Wiederinstallationsabnahme:
tools/Test-TestlabUninstallPreservation.ps1 hinzugefuegt. Ausschliesslich VM
SIEENCODE-LAB, echte Profile labadmin/testerA/testerB; Phasen Capture,
VerifyRemoved, VerifyRestored. Vergleicht Fixture-Dateien bytegenau per SHA256
und prueft private RSA-Zertifikate durch OAEP-SHA256-Roundtrip ohne Schluessel-
export. Keine Installeraktion im Helfer. Parser und ablehnender Hostguard
geprueft; noch nicht im Gast ausgefuehrt, keine Deinstallationsfreigabe behauptet.
Details und Ausfuehrungsreihenfolge in OFFLINE-GROUPS-TESTLAB.md.

30.10.2026 Zusaetzliche native Ruecksprungpruefung unter labadmin bestanden:
Nach persistierter Revision 9 die bisherige native-test.group-enrollment.json
(Revision 8) im Aktualisierungsdialog ausgewaehlt. Sichtbare Abweisung:
"Veraltete oder widerspruechliche Gruppenfreigabe." Screenshot 11 gesichert.
Danach Host-Patchmeldung ueber VM, Benutzereingabe erkannt, VM minimiert;
keine weitere Mausbedienung. Fehlermeldung moeglicherweise noch offen.
Pruefung fehlender Verwaltungsrechte unter labadmin noch nicht ausgefuehrt.

30.10.2026 Native Aktualisierung unter labadmin abgeschlossen: geprueften Export
Revision 9 per Dateidialog geladen, Vorschau 8 -> 9 mit dritter leerer Gruppe
und unveraenderten Personen/Recovery angezeigt, explizit bestaetigt, Erfolg.
App regulaer geschlossen und neu gestartet; erneute Auswahl derselben Datei
meldet "Dieser Gruppenstand ist bereits registriert." Persistenz damit nativ
belegt. Aufnahmen 08-10 gesichert. Normale App-Staende B und labadmin jetzt 9,
separate API-Fixtures weiterhin 8. Keine Projekt-/Vaultdateien bearbeitet.
Rearm-Nachkontrolle nach abgeschlossenem Boot: verbleibend 1 (vorher 2),
LicenseStatus weiterhin 5, StatusReason 3221549065, GracePeriodRemaining=0.
Keine wirksame Verlaengerung belegt; kein weiterer Rearm. VM aktuell nutzbar,
Abschaltrisiko bleibt. Native Restablaeufe/Anleitung/Release weiterhin offen.

30.10.2026 Benutzer autorisiert einmaligen regulaeren Rearm der Test-VM und
beschafft parallel eine Lizenz. Vorher: EnterpriseSEval / TIMEBASED_EVAL,
LicenseStatus=5, GracePeriodRemaining=0, RemainingWindowsReArmCount=2.
Nicht erhoehter Aufruf abgewiesen (0xC004F025); nach ausdruecklicher UAC-
Bestaetigung des Benutzers meldet slmgr /rearm Erfolg und verlangt Neustart.
Regulaerer Neustart angestossen, sichtbar noch "Wird neu gestartet".
Wirksame Dauer und Lizenzstatus nach Boot noch nicht bestaetigt. Kein zweiter
Rearm, keine Registry-/Zeit-/Dienstmanipulation. Testdaten/Kandidat unveraendert.

29.10.2026 Benutzerantwort: gueltig lizenzierte Testumgebung derzeit nicht
verfuegbar. VM/Testdaten erhalten, keine weiteren Neustartschleifen.
Auftragsabgleich um konkrete native Nachweise und verbleibende Gates aktualisiert.
Kollegenanleitung um die Originalansichten fuer signiertes Speichern und
oeffentlichen Export ergaenzt; fuenf Bilder integriert, visuelle Gesamtpruefung
und die noch ausstehenden Bedienablaeufe weiterhin offen. Kein Releaseabschluss.

29.10.2026 Exportpruefung abgeschlossen: native Revision 9 auf Host gesichert,
mit identifiziertem Kandidaten-Core Signatur und Inhalt validiert. Genau eine
neue leere Gruppe, bestehende Gruppen/Personen/Recovery/Delegationen/Root erhalten.
Ergebnis offline-groups-native-export-result.json. Temp-Credentialdatei im Gast
nachweislich entfernt. Zweitimport auf labadmin weiterhin offen.
Ursache wiederholter Abschaltungen belegt: Systemlog 1074 nennt wlms.exe und
abgelaufenen Windows-Lizenzzeitraum (vier letzte 1074-Ereignisse). Kein Produktcrash.
Keine Lizenz-/Zeit-/Dienstmanipulation, keine Neustartschleife. Gueltig lizenzierte
Testumgebung fuer verlaessliche laengere Abnahme erforderlich.

29.10.2026 Verwaltungsfortschritt: vorhandene geschuetzte B-Testfixtures ohne
Ueberschreiben/Originalaenderung fuer normalen App-Pfad bereitgestellt; sichtbare
App mit echtem testerB-Profil gestartet. Native Bibliotheksauswahl, Laden A/B,
dritte leere Gruppe hinzufuegen, signiert als Revision 9 speichern und oeffentlich
in B/Dokumente exportieren bestanden laut sichtbarer Statusmeldung. Aufnahmen
06/07 und genauer Ablauf in OFFLINE-GROUPS-NATIVE-SETUP-ACCEPTANCE.md.
Danach VM unerwartet powered off (VBox beim copyfrom); kein belegter Appabsturz.
Naechster Schritt einmaliger Neustart und Exportdatei aus B/Dokumente abholen,
Signatur/Inhalt pruefen, auf labadmin aktualisieren. Nicht Gruppe erneut anlegen.
B normale GroupTrust-Dateien Revision 9; B API-Fixtures weiterhin 8, labadmin 8.
Temp-Credentialdatei laut erfolgreich beendetem Launcher entfernt; separate
Existenzkontrolle noch offen. Wiederholtes Ausschalten anhand Gast-Ereignislog
klaeren; abgelaufene Windows-Evaluation ist sichtbar, Ursache bisher unbewiesen.

29.10.2026 gesichert: Nativer Bedienlauf vom 28.10. erfolgreich abgeschlossen
fuer Gruppenregistrierung unter labadmin (Bibliothek 494f0a91-fc19-4e0d-8a73-30b4b4578e76,
Revision 8, zwei Gruppen). Nach regulaerem App-Ende und neuem Start denselben
Stand ueber Aktualisierung erkannt: Dieser Gruppenstand ist bereits registriert.
Details/Grenzen in OFFLINE-GROUPS-NATIVE-SETUP-ACCEPTANCE.md. Kein echter
unabhaengiger Fingerprintkanal, sondern kontrollierte synthetische Daten;
labadmin weiterhin kein Gruppenmitglied. Fuenf Originalansichten samt Hashliste
gesichert, drei davon in Kollegenanleitung integriert. Keine weitere Registrierung
dieser Bibliothek wiederholen. Naechster offener Schritt: native Hauptverwaltung
unter testerB (aktueller Rootinhaber), Projekt-/Recovery-/Delegationsablauf,
Deinstallation; Layoutkontrolle der Anleitung weiter offen.

28.10.2026: Kollegenanleitung als eigenstaendige lokale HTML-Datei vorbereitet:
docs/KOLLEGEN-ERSTEINRICHTUNG.html. Getrennte Rollenwege, exakte Gruppenmatrix,
illustrierter Aenderungsablauf, Einrichtung einschliesslich Leser und Fehlerhilfe.
Beschriftungen mit MainWindow.xaml und Gruppendialogen/Schnellstart abgeglichen.
Expliziter Entwurf: Diagramme sind keine Screenshots, native Gesamtpruefung und
finale Bilder fehlen. SVG-Struktur, lokale Links und Anker statisch geprueft.
Visuelle Browserpruefung nicht erfolgt: Browser-Sicherheitsrichtlinie verweigert
file:-URLs; keine Umgehung. Bestehender Installer unveraendert, Anleitung liegt
zunaechst nur im Quellrepository. Native Fortsetzung bei freiem Host-Eingabefokus.

Fortsetzung 28.10.2026: VM war nachweislich poweroff (letzter Zustandswechsel
26.10.2026 19:40:58 UTC). Sichtbar gestartet; ohne Passworteingabe erschien der
Desktop. Gastabfrage Win32_ComputerSystem.UserName bestaetigt
SIEENCODE-LAB\labadmin. Damit praktische Autologin-Abnahme bestanden.
Installierten Kandidaten erneut gestartet; keine Neuinstallation/Identitaetsanlage.

Native Fortsetzung 26.10.2026 abgeschlossen fuer persoenliche Identitaet und
Einladungsexport: nach Neustart vorhandene Identitaet labadmin wiedererkannt,
nativen Speicherdialog benutzt, Einladung im Dokumente-Ordner gespeichert.
Datei separat validiert (oeffentliches Zertifikat ohne privaten Schluessel,
Fingerprint korrekt). Assistent geschlossen, Status eine Identitaet/null alte
Teammitglieder korrekt; keine Teamliste fuer Gruppenbeitritt gespeichert.
Details und Dateihash: OFFLINE-GROUPS-NATIVE-SETUP-ACCEPTANCE.md.
Naechste native Abnahme: Gruppenregistrierung und Hauptverwaltungsdialoge;
Projekt-/Recovery-/Delegationsbedienung sowie Deinstallation weiter offen.

Autologin vom Benutzer ausdruecklich angefordert und eingerichtet (26.10.2026):
Nur Test-VM SIEENCODE-LAB, Konto labadmin. Benutzer bestaetigte UAC; anschliessend
AutoAdminLogon=1, DefaultUserName=labadmin und DefaultDomainName=SIEENCODE-LAB
aus dem Gast gelesen. Erfolgsbericht bestaetigt LSA private secret; kein
DefaultPassword-Klartextwert in Winlogon und keine temporaere Passwortdatei mehr.
Passwort unveraendert. Noch kein Neustart zur praktischen Autologin-Abnahme;
BootVerified=false. Nicht erneut einrichten, beim naechsten geplanten Neustart
automatische Desktopanmeldung pruefen. Hostanmeldung unveraendert.

Native Erstidentitaet 26.10.2026: Benutzer oeffnete Einstellungen; danach
Vault-Ersteinrichtung per Computer Use geoeffnet, Neue persoenliche Identitaet
angeklickt und voreingestellter synthetischer Name labadmin bestaetigt.
Assistent zeigte anschliessend labadmin mit Fingerabdruck sowie Teamlistenentwurf
(Aenderungen noch speichern). Damit native Schluesselerstellung ohne sichtbaren
Fehler belegt; weder Einladungsexport noch Teamlistenspeicherung bestaetigt.
Bei Fortsetzung VM-Fenster verschwunden; einmalige VBox-Pruefung bestaetigt
poweroff, VMStateChangeTime 2026-10-26T09:59:34Z. Ursache nicht festgestellt,
kein behaupteter Anwendungsabsturz. VM sichtbar neu starten; vorhandene Identitaet
wiederverwenden. Naechster Schritt Einladungsexport, Dialogschliessen und
Status-/Persistenzpruefung. Keine weitere Identitaet gleichen Namens erzeugen.

Native UI-Fortsetzung 26.10.2026: Installierte SieEncode.exe ueber Gastdienst
gestartet; sichtbare Startseite im angemeldeten labadmin-Desktop. Leere Bibliothek
C:/Users/labadmin/Documents/SieEncode-Projekte, Arbeitsplatz meldet Bereit.
Noch keine native Einrichtungsabnahme: automatische Klicks auf Gast-Suche und
Startmenue ohne sichtbare Oeffnung; Fenstergroessenwechsel/Computer-Use meldet
wiederholt user input detected. Letzte Aufnahme zeigt nur 640x480 Gastbereich,
Anwendungsinhalt abgeschnitten. Benutzer soll VM maximieren und Einstellungen
oeffnen, danach frische Beobachtung statt erneuter Status-/Klickschleife.
Windows-Gast zeigt ausdruecklich abgelaufene Evaluierungslizenz. Keine Umgehung
oder Aktivierung vorgenommen. Erfolgreiche API-Nachweise bleiben erhalten;
native Gesamtbedienung und Deinstallation weiter offen.

Verwaltungsabnahme 26.10.2026: Neun neue Phasen unter testerA/testerB bestanden,
gesichert in offline-groups-vm-authority-results.json. B erzeugt separaten
DPAPI-Signierspeicher, A erteilt Vollmacht nur fuer Lokal. Lokal2-Aenderung
abgewiesen; erlaubter Antrag durch A autorisiert, andere Gruppe/Recovery
unveraendert, Replay ohne Mutation abgewiesen. Entzug verhindert neue Antraege
bei erhaltenem Delegatenschluessel. B bereitet Nachfolger samt privater Sicherung
vor; A prueft/autorisiert (falscher Fingerabdruck abgewiesen), B importiert und
aktiviert, signiert in neuem Prozess; A akzeptiert neuen Stand. Alte Root-Signatur
abgewiesen, bestehender Projekt- und separater Recoveryzugang erhalten.
API-Nachweise gegen installierte Produkt-DLLs, synthetisch bereitgestellte
Fingerabdruecke, kein nativer Dialog-/unabhaengiger Fingerprintkanaltest.
Helfer tmp/GuestVaultProbe und tmp/Run-AuthorityProbeWithProfile.ps1.
VM-Hauptverwaltung liegt jetzt bei testerB; testerA besitzt weiterhin seinen
alten Verwaltungsspeicher, den aktueller Trust nicht mehr akzeptiert.
Noch offen: nativer Einrichtungs-/Projekt-/Verwaltungsbedienlauf und
Deinstallationsabnahme. Hardwarefreigabe bleibt separat; PDF zurueckgestellt.
Keine bestandenen Phasen erneut ausfuehren und keine alten Truststaende laden.

Projektabnahme 26.10.2026: Sechs weitere Phasen unter testerA/testerB bestanden
und gesichert (offline-groups-vm-project-results.json). Vollstaendige synthetische
Projektdatei mit Vault angelegt; Quelle unveraendert, neue ID, Schluesselnutzdaten
vollstaendig erhalten. Entzogenem B Oeffnen/Kopieren verweigert, kein Kopieordner.
Nach Wiederaufnahme (Policyrevision 3) und Abgleich kopiert B das Projekt;
eigene ID, Originaldateien unveraendert. A liest Kopie in neuem Prozess;
manipulierte Projekt-ID abgewiesen. Echte Recoveryidentitaet nach erfolgtem
VM-/Hostneustart wiedergefunden und ausdrueckliche Entschluesselung bestanden;
normaler Zugang mit Recoveryidentitaet weiter verweigert.
API-Integration gegen identifizierte installierte DLLs, kein nativer Dialoglauf.
Synthetisches Profil ohne vollstaendige Profilvertrags-/Hardwarepruefung.
Naechste offene Schritte: Delegation/Verwaltungsnachfolge unter beiden Benutzern,
native Einrichtung und Projektbedienung, Deinstallation. PDF bleibt zurueckgestellt.
Helfer tmp/GuestVaultProbe erweitert; neuer Starter tmp/Run-ProjectProbeWithProfile.ps1.
Vorherige acht Phasen nicht wiederholt. VM-Trust steht jetzt auf Revision 3.

Fortsetzung 26.10.2026 nach Hostneustart: Der vorherige VM-Lauf meldete alle
acht Phasen PASS (testerA/testerB identity, setup, access, rotate, revoked,
recovery-install, recovery-reopen). Installierte Produkt-DLLs wurden vor jeder
Phase anhand der Kandidatenhashes geprueft. Normale Windows-Benutzer mit echten
CurrentUser-Schluesseln; Gruppenfreigabe/-ablehnung, B-eigener Vault, alte lokale
Kopie, Entzug mit Rotation/Altsicherung, Rollbackablehnung und echte Recovery-
Schluesselinstallation samt Wiederlesen in separatem Prozess bestanden.
Dies ist API-Integration, keine native Dialogabnahme oder Hardwarefreigabe.
Der Helfer nutzt .NET 8.0.30; die installierte Anwendung nutzt 8.0.32.
Fingerprint wurde synthetisch vom Harness geliefert, kein unabhaengiger
menschlicher Vertrauensabgleich. Keine komplette Projektdatei-/Delegations-/
Verwaltungsnachfolgeabnahme daraus ableiten.
VirtualBox guestcontrol --profile allein lud den Benutzerhive nicht; deshalb
scheiterte der erste Identitaetsversuch. Windows-Prozessstart mit Credentials
und LoadUserProfile=true behob den Teststart; keine Produktaenderung.
Helfer: tmp/GuestVaultProbe und tmp/Run-VaultProbeWithProfile.ps1.
Alle acht Gastnachweise am 26.10.2026 gesichert und geprueft: offline-groups-vm-vault-results.json.
Bestandene Phasen nicht wiederholen. Native Projekt-/Dialogablaeufe, Delegation und Verwaltungsnachfolge bleiben offen.

Abgeschlossener Installationsmeilenstein (24.10.2026): Benutzer meldet PASS;
installed-suite.json und Transcript aus dem Gast gesichert und Ergebnis geprueft.
Reale MSI-Neuinstallation 1.2.1 und Upgrade auf 1.3.0-offline-groups.1 jeweils
Exit 0; beide installierten Studio-DLL-Hashes stimmen. Self-Test und UI-Smoke
beider Versionen jeweils Exit 0. Tests liefen als Administrator in der LTSC-VM.
Der fruehere ExitCode=null-Harnessfehler ist damit fuer diesen Lauf behoben.
Nachweis: offline-groups-installed-suite.json neben dieser Datei; volle lokale
Logs unter D:/VirtualBox/SieEncode-Testlab/Evidence. Keine erneuten Paketbauten.
Naechster Nachweis: SieEncode-Vault-Gesamtablauf unter testerA/testerB mit
Gruppenzugriff, Offlinebetrieb, Entzug/Rotation und echter Recoveryinstallation.
Native Gesamtbedienung, Deinstallation und Hardwareabnahme bleiben offen.

Aktueller VM-Teststand (24.10.2026): LTSC-Gast eingerichtet, Guest Additions
7.2.20 vorhanden, Secure Boot/TPM bestaetigt, automatische Anmeldung entfernt.
Zwei normale Benutzer angelegt; synthetische Windows-DPAPI-Isolation mit
testerA/testerB bestanden (keine SieEncode-Vault-Abnahme).
Reale MSI-Neuinstallation 1.2.1 Exit 0 und installierter DLL-Hash bestaetigt.
Erster Self-Test wurde vom Harness wegen ExitCode=null abgebrochen; stdout/stderr
leer, daher kein belegter Produktfehler und kein bestandener Self-Test.
Harness auf direkten System.Diagnostics.Process-Start umgestellt, lokal mit
Exitcodes 0 und 7 geprueft und in den Gast kopiert. Kontrollierte Fortsetzung
prueft vorhandene 1.2.1 samt Hash, erhaelt vorheriges Ergebnis und wiederholt
die Anwendungspruefung ohne erneute MSI-Installation. Benutzer startet denselben
Desktop-Launcher erneut. Upgrade/Kandidaten-/Mehrbenutzerabnahme weiter offen.
Keine Statusschleifen; auf Benutzerergebnis warten.

Testlab-Fortsetzung am 24.10.2026: Erster Windows-26H2-Versuch blieb bei
42 Prozent; nach Snapshot und einmaligem Neustart meldet Setup eine nicht
fortsetzbare Installation. Ursache des vorherigen Stillstands ungeklaert.
Erste VM gestoppt und erhalten. Frische SieEncode-Testlab-LTSC mit offiziellem
Microsoft-Evaluierungsmedium 26100.1742 aufgebaut; lokale Installation ohne
Netzwerk laeuft. Noch keine Gast-/Produktabnahme. Siehe OFFLINE-GROUPS-TESTLAB.md.

Neue Freigabe am 24.10.2026: Benutzer autorisiert den eigenstaendigen Aufbau
einer separaten VirtualBox-Testumgebung. Neue VM SieEncode-Testlab angelegt;
bestehende fremde VMs und Hostinstallation unveraendert. Windows-11-Enterprise-
Evaluierungsmedium von Microsoft heruntergeladen und gegen veroeffentlichten
SHA256 geprueft; unbeaufsichtigte Installation gestartet. Noch keine fertige
Windows-/SieEncode-Installation oder Mehrbenutzerabnahme behauptet.
Details und Fortsetzungsstand: OFFLINE-GROUPS-TESTLAB.md.

Auftrag gegen fertigen Kandidaten abgeglichen (2026-10-23):
OFFLINE-GROUPS-GOAL-AUDIT.md ordnet jede Anforderung konkreten Quellen und
Nachweisen zu und trennt lokale Belege von offener echter Abnahme. Benutzer
bestaetigt: derzeit keine separate Testumgebung. VirtualBox ist vorhanden;
registrierte VMs wurden nur aufgelistet, nicht gestartet oder veraendert und
nicht als freigegebene Testumgebung betrachtet. Keine weitere Installation,
kein Zertifikatsspeichereingriff, kein neuer unveraenderter Build/Testlauf.
Externe Voraussetzung fuer Gesamtabschluss: geeignete getrennte Windows-
Umgebung fuer Installation/Upgrade, Mehrbenutzer-/Offlineablauf, tatsaechliche
Recoveryinstallation und nativen Gesamtbedienlauf. Kandidat bleibt unveraendert.
Erste dokumentierte externe Blockade nach fertig geprueftem Kandidaten; Ziel
noch aktiv, nicht als abgeschlossen markieren und keine Abnahme simulieren.

Installierbarer Offline-Gruppen-Testkandidat gebaut und geprueft (2026-10-23):
1.3.0-offline-groups.1 aus sauberem Commit 2be1be0, MSI 1.3.0, kein Portable.
Artefakte: artifacts/offline-groups-1.3.0-preview.1/package. Build Exit 0;
exakter publizierter UI-Smoke Exit 0, status OK, 60 Checks, stderr leer.
MSI /a Exit 0, alle 1034 Release-Dateien hashgleich. Zwei Paketmanifest-Eintraege
geprueft. Runtime komplett bytegleich aus 1.2.1; deren bestehender BUILD-IDENTITY
mit dirty=true bleibt ehrlich erhalten. Geschuetzte bisherige MSI/Studio-DLL/
Runtime-EXE/DLL vor/nach Build unveraendert. Keine Installation durchgefuehrt.
MSI SHA256: 268A850AD71354E702CB850831E226C51D88226FB00FAF05A47AC8BAB8B1DD4D.
Detailnachweise und offene Gates: OFFLINE-GROUPS-CANDIDATE-1.3.0-1.md.
Neue OFFLINE-GRUPPEN-ABNAHME.md ist im Installer enthalten und definiert den
echten Zweitarbeitsplatz-Ablauf. Naechster wesentlicher Nachweis: reale getrennte
Windows-Installation, Mehrbenutzer-/Offlineablauf und Recoveryinstallation.
Kein GitHub-Release, keine neue Hardwarefreigabe, Ziel aktiv.

Geraeteeinrichtung weist wiederverwendete Schluessel sofort ab (2026-10-23):
AddDevice vergleicht den RSA-Schluessel der Einladung mit allen bereits im
Entwurf zugeordneten Geraeten, auch deaktivierten. Die Meldung nennt Person und
Geraet sowie den notwendigen Schritt, eine eigene Identitaet zu erzeugen.
Kein ungueltiger Eintrag wird erst zum spaeteren Speichern vorgemerkt.
UI-Integration prueft abweichendes Zertifikat mit gleichem Schluessel bei aktivem
und deaktiviertem Altgeraet, hilfreiche Meldung und unveraenderten Entwurf.
Anschliessendes regulaeres Speichern/Neuladen mit zwei unabhaengigen Geraeten
weiterhin erfolgreich. CI Exit 0, 72/72, Builds ohne Warnungen/Fehler:
tmp/group-device-import-ci.log. Windows-UI-Smoke Exit 0, status OK, stderr leer:
tmp/group-device-import-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Pruefung ueber Dialogfunktionen mit synthetischen Daten, keine native Datei-
oder echte Zweitbenutzerabnahme. Ziel aktiv, bestehender Release unveraendert.

Empfaengerabgleich bei spaeten Konflikten geprueft (2026-10-23):
GroupVaultTests.VerifyReconciliation entzieht Recovery waehrend der tatsaechlichen
Identitaetsenumeration beim Entschluesseln. Apply bricht ohne Vault-/Backupaenderung
ab. Weitere Faelle pruefen beschaedigte vorhandene Sicherung und konkurrierend
ersetzte Vaultdatei waehrend des Entschluesselns: keine Ueberschreibung. Danach
funktioniert der regulaere Abgleich mit neu signierter Freigabe auf hoeherer
Revision und reparierter synthetischer Testdatei. Kein Trustreset.
Erster CI-Lauf: neue Tests bestanden, insgesamt 71/72 wegen UnauthorizedAccess
beim rfPrj-Dateiersatz in TestOperations; Ursache nicht abschliessend geklaert:
tmp/group-reconciliation-conflicts-ci.log. Wiederholung mit frischem isoliertem
TEMP/TMP erfolgreich: Exit 0, 72/72, CLI/WPF ohne Warnungen/Fehler,
tmp/group-reconciliation-conflicts-ci-verified.log. Kein Produktcode geaendert.
Zwei veraltete Aussagen zur noch fehlenden Editoranbindung im Konzept korrigiert.
Nur synthetische lokale Konflikte; keine verteilte Cloud- oder Mehrbenutzerabnahme.
Ziel aktiv, bestehender Release unveraendert.

Unabhaengige Geraeteschluessel erzwungen (2026-10-23):
Auftragspruefung fand normale Geraetezuordnung nur nach Zertifikatsfingerabdruck.
Andere Zertifikate mit identischem RSA-Schluessel konnten mehreren Personen oder
Geraeten zugeordnet werden. Schluesselbesitz ist jedoch nicht durch ein neues
Zertifikat getrennt; damit waren getrennte Geraetezugaenge nicht garantiert.
OfflineGroupDirectory lehnt jetzt doppelte SPKI-Schluesselfingerabdruecke ab,
einschliesslich deaktivierter Eintraege. Gilt beim Signieren, Verifizieren und
Empfaengeraufloesen. Keine automatische Datenmigration oder Dateiaenderung.
Regression: gleiche/fremde Person, aktives/deaktiviertes Original, unabhaengig
signierter unzulaessiger Import. Positive Core-/UI-Fixtures verwenden nun eigene
RSA-Schluessel pro Geraet. Acht-Personen-/Drei-Gruppen-Matrix weiterhin bestanden.
CI Exit 0, 72/72, Builds ohne Warnungen/Fehler:
tmp/group-independent-device-keys-ci.log. Windows-UI-Smoke Exit 0, status OK,
stderr leer: tmp/group-independent-device-keys-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Schnellstart und Architektur dokumentieren Anforderung und Altstand-Grenze.
Keine echte Zweitbenutzerabnahme; Ziel aktiv, Release unveraendert.

Echte Prozessabbrueche an Aktivierungsgrenzen geprueft (2026-10-23):
GroupActivationInterruptionTests erstellt ausschliesslich synthetische Verwaltung,
Trust und Vorbereitung. Zwei Kindprozesse werden nach Archivierung vor dem
Schutz/Schreiben bzw. nach abgeschlossener Aktivierung hart beendet; Bereitschaft
wird per geflushter Markierungsdatei signalisiert, Prozessende kontrolliert.
Jeweils ein neuer Prozess rekonstruiert die Stores von Platte und aktiviert
erfolgreich. Vorgängerarchiv, Trust und Vorbereitung bleiben bytegleich; eine
abgeschlossene Aktivierung wird nicht erneut geschrieben. Der neue Schluessel
signiert danach ein akzeptiertes Update. Temporäre Testdaten werden entfernt.
CI Exit 0, 72/72 Tests, CLI/WPF ohne Warnungen/Fehler:
tmp/group-process-interruption-ci.log. Keine Produktcodeaenderung erforderlich.
Grenzen: synthetischer Speicherschutz, kein Stromausfall, kein Abbruch mitten
im Dateiersatz/Trustimport und keine echte zweite Windows-Umgebung. Diese
fehlenden Nachweise bleiben in der Freigabematrix offen. Ziel aktiv.

Leerer Nachfolgerdialog nativ geprueft und verbessert (2026-10-23):
Ohne registrierte Bibliothek sind jetzt alle vier Nachfolgeaktionen deaktiviert;
der Hinweis nennt zuerst erforderliche Registrierung. Schliessen bleibt bedienbar.
Im isolierten --ui-test-workspace per Screenshot und Accessibility bestaetigt:
vier disabled-Schaltflaechen, sichtbarer Hinweis, Rueckkehr zu Einstellungen.
Release-Build erfolgreich, 0 Warnungen/Fehler:
tmp/group-native-empty-build-verified.log. Erster Build war durch eine noch
laufende Testanwendung blockiert; nach deren Ende erfolgreich wiederholt.
Keine Sicherheitskonfiguration durch native Bedienung geaendert. Dies ist nur
die Leerzustandsabnahme, kein vollstaendiger nativer Nachfolge-/Mehrbenutzerlauf.
Weiter offen: Prozessabbruchnachweise und echte getrennte Windows-Umgebung.
Ziel aktiv; bestehender Release unveraendert.

Delegierter Ablauf ueber Verwaltungswechsel nachgewiesen (2026-10-23):
OfflineGroupDelegateStoreTests.VerifyAdministratorChange fuehrt vorbereitete
Nachfolge, Autorisierung, getrennte Trustimporte und Aktivierung durch. Alter
Antrag scheitert an neuer Verwaltung; delegierter Schluessel bleibt bytegleich.
Nach Trustimport/Neuladen: alter Entwurfsstand und fremde Gruppe abgewiesen,
neuer oeffentlich transportierter Antrag fuer freigegebene Gruppe erfolgreich.
Unbeteiligte Gruppe und Vollmachtsumfang bleiben erhalten. Replay, vor Entzug
erstellter offener Antrag und Signieren nach importiertem Entzug scheitern;
lokale Identitaet bleibt erhalten. Synthetischer Speicherschutz in einem Prozess,
kein nativer Bedienlauf und keine echte Zweitbenutzerabnahme.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-delegation-handover-ci.log. Schnellstart und Freigabeabgleich aktualisiert.
Naechste offene Abnahmen: native Dialoge/Gesamtablauf und Prozessabbruchverhalten;
echte getrennte Windows-Umgebung weiterhin nicht nachgewiesen. Ziel aktiv.

Offline-Verwaltungswechsel gezielt nachgewiesen (2026-10-23):
VerifyOfflineTransition nutzt drei getrennte synthetisch geschuetzte Trustdateien.
Wechsel/Updates des ersten Arbeitsplatzes lassen offline Datei bytegleich unter
altem Anker. Neuer Schluessel und unbestaetigter Wechsel werden dort abgewiesen.
Spaeterer expliziter Import plus Neuladen und neues Update konvergieren zum neuen
Stand; alter Schluessel mit hoeherer Revision und Replay werden dann abgewiesen.
Dritter Arbeitsplatz akzeptiert bewusst noch gueltiges altes Update und weicht ab:
urspruengliche Uebergabe und Ruecksetzung scheitern, sein Stand bleibt erhalten.
CI Exit 0: tmp/group-offline-transition-ci.log. Keine Produktcodeaenderung,
keine echte zweite Windows-Umgebung. Schnellstart nennt divergierende Staende
und verbietet manuelle Registrierungsruecksetzung; Abnahmematrix aktualisiert.
Naechster Nachweis: delegierter Ablauf ueber den Verwaltungswechsel hinweg;
native Gesamtabnahme bleibt separat offen. Ziel aktiv, Release unveraendert.

Freigabenachweise abgeglichen und Aktivierungskonkurrenz geprueft (2026-10-22):
Neue Tests injizieren beschaedigtes Vorgaengerarchiv, Trustfortschritt waehrend
Schutz der neuen Verwaltungsdatei und eine konkurrierende erfolgreiche Aktivierung.
Beschädigtes Archiv und Vorgänger bleiben unveraendert, veraltete Aktivierung
scheitert vor Ersatz, konkurrierende Gewinnerdatei bleibt bytegleich erhalten.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-activation-conflicts-ci.log. Keine Produktcodeaenderung erforderlich.
Wechselkonzept enthaelt jetzt Abgleich aller sieben verlangten Nachweise mit
konkreten Grenzen. Noch nicht bewiesen: dedizierter offline belassener Trust beim
Verwaltungswechsel, delegierter Ablauf ueber Wechsel, nativer Gesamtlauf,
Prozessabbruch-/Stromausfall und echte zweite Windows-Umgebung. Teilpruefungen
werden nicht als End-to-End-Freigabe ausgegeben. Veraltete letzte Anweisung zur
noch fehlenden UI im Wechselkonzept ersetzt. Ziel aktiv, Release unveraendert.

Aktivierungsdialog und Windows-Nachfolge-Gesamtablauf abgeschlossen (2026-10-22):
Schritt 4 im Nachfolgerdialog prueft registrierten neuen Fingerabdruck, zeigt
Bibliothek/Archivierungsfolgen und verlangt ausdrueckliche Bestaetigung. Vor der
Bestaetigung gelesener Checkpoint wird beim Schreiben erneut geprueft. Nach
Trustimport erkennt neu geoeffneter Dialog den vorbereiteten Nachfolger, ohne
erneuten Anfrageexport gegen alten Stand zu verlangen. Erfolg verweist auf die
Gruppenverwaltung und verbleibende Importe anderer Arbeitsplaetze.
Windows-Test ueber Dialogfunktionen: Vorbereitung, private Sicherung, oeffentliche
Anfrage, alte Autorisierung, Importdialog, Neustart des Nachfolgerdialogs,
Aktivierung mit/ohne Vorgaengerdatei, Archiv bytegleich, unbestaetigt/stale
abgewiesen, Wiederholung ohne Schreiben, neue Signatur und Verwaltungsneuladen.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler: tmp/group-activation-ui-ci.log.
Windows-Smoke Exit 0, status OK, 60 Checks, stderr leer:
tmp/group-activation-ui-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Nur synthetische Dateien und aktueller Windows-Benutzer. Keine echte
Zweitbenutzer-/Hardware- oder visuelle Datei-/Bestaetigungsdialogabnahme.
Schnellstart und Architekturstand aktualisiert; naechster Schritt sind noch
offene Gesamtabnahme/Sicherheitspruefung gegen den vollstaendigen Auftrag.
Bestehender Release unveraendert, Ziel aktiv.

Kontrollierte Nachfolgeraktivierung im Core umgesetzt (2026-10-22):
ActivateSuccessor verlangt vorbereiteten Schluessel passend zum bereits
registrierten neuen Anker, erwarteten Checkpoint und ausdrueckliche Bestaetigung.
Fehlende Verwaltung wird angelegt; nur exakter vorbereiteter Vorgaenger darf nach
bytegetreuer geschuetzter Archivierung ersetzt werden. Archivname enthaelt Hash;
vorhandenes Archiv muss identisch sein. CAS schuetzt aktive Zieldatei, Trust wird
vor Schreiben erneut geprueft. Keine gemeinsame atomare Transaktion mit Trustimport.
Wiederholte bereits abgeschlossene Aktivierung mit gleichem Stand schreibt nichts.
Tests: Aktivierung vor Trustwechsel/unbestaetigt/falscher Checkpoint abgewiesen,
fremde Verwaltung erhalten, Schutzfehler nach Archivierung laesst alten Stand
erhalten, erneuter Versuch erfolgreich, Vorbereitung unveraendert, neuer Schluessel
signiert, alter archivierter Schluessel weiterhin lesbar. Archiv ist kein Widerruf.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-successor-activation-ci.log. Keine Windows-/UI-Aktivierungsabnahme in
diesem Schritt; als Naechstes Dialoganbindung und entsprechende Integrationspruefung.
Bestehender Release unveraendert; Ziel aktiv.

Nachfolger-Einrichtung integriert und getestet (2026-10-22):
Neue Einstellungsaktion mit registrierter Bibliotheksauswahl, expliziter
Vorbereitung, privater Sicherung und oeffentlichem Anfrageexport. Vorbereitungen
bleiben erhalten; pro Sitzung erst nach erfolgreichem Schreiben/Zuruecklesen der
privaten Sicherung und Schliessen des Sicherungsdialogs exportierbar. Fingerabdruck
bleibt separat sichtbar/kopierbar. Ungueltige Auswahl verwirft vorheriges Ziel;
bei geaendertem Trust bleibt private Sicherung moeglich, Anfrageexport scheitert.
Neue Windows-Pruefungen decken Ablauf, Neuladen, fehlende Sicherung, Wiederanlage,
vorhandene Ausgabedatei, falsche Bibliothek und stale Trust ab. Testaufbau korrigiert:
WPF-Elternfenster muss vor Erstellen seines Kinddialogs angezeigt sein.
App-Build Exit 0 ohne Warnungen/Fehler: tmp/group-successor-setup-build.log.
Korrigierter UI-Smoke Exit 0, status OK, 59 Checks, stderr leer:
tmp/group-successor-setup-smoke-fixed/SieEncode/ui-smoke/ui-smoke-result.json.
Vorheriger fehlgeschlagener UI-Lauf unter tmp/group-successor-setup-smoke bleibt
als Diagnose erhalten. Keine visuelle Dateidialog-/Zweitbenutzerabnahme behauptet.
Naechster Schritt: gefuehrte Aktivierung nach autorisiertem Wechsel einschliesslich
vorhandener Verwaltungsdateien. Bestehender Release unveraendert, Ziel aktiv.

Autorisierungsablauf in Verwaltungsoberflaeche integriert (2026-10-22):
Neue Aktion liest begrenzte oeffentliche Nachfolgeranfrage, fordert unabhaengigen
Fingerabdruck und ausdrueckliche Bestaetigung. Pruefung vor Dateiauswahl und vor
Schreiben; ausschliesslich neue oeffentliche Wechseldatei, keine automatische
Aktivierung. Bestehende Ausgabe-, Verwaltungs- und Trustdateien bleiben erhalten.
Windows-Smoke testet gueltigen Export, fehlende/falsche Angaben, vorhandene Ziele
und veraltete Oberflaeche nach Trustwechsel. App-Build ohne Warnungen/Fehler:
tmp/group-authorization-ui-build.log. Voriger Lauf unterbrochen, Handle nicht mehr
vorhanden und kein Bericht; kein Erfolg daraus abgeleitet. Neuer isolierter Lauf
Exit 0, status OK, 58 Checks, stderr leer:
tmp/group-authorization-ui-smoke-recheck/SieEncode/ui-smoke/ui-smoke-result.json.
Dateiauswahldialoge noch nicht visuell abgenommen. Schnellstart beschreibt den
vorhandenen Teilablauf und benennt fehlende Nachfolger-Einrichtung ausdruecklich.
Als Naechstes Vorbereitung/Sicherung/Anforderungsexport beim Nachfolger und
kontrollierte Aktivierung integrieren. Ziel aktiv, bestehender Release unveraendert.

Autorisierung aus geschuetztem Verwaltungsspeicher umgesetzt (2026-10-21):
AuthorizeSuccessor verarbeitet ausschliesslich oeffentliche Anforderung plus
lokalen Trust, erwarteten Checkpoint, unabhaengigen Fingerabdruck und explizite
Bestaetigung. Privater Schluessel bleibt intern; Verwaltung und Registrierung
muessen Schluessel und Stand teilen. Vor Rueckgabe erneute Pruefung von
Verwaltungsdateihash und registriertem Stand. Ausgabe ist oeffentlicher Wechsel;
keine automatische Aktivierung oder Aenderung der lokalen Dateien.
Tests fuer unbestaetigte/falsche Angaben, manipulierten Besitznachweis, fremden
lokalen Schluessel, divergierende Staende, veraltete Anfrage und alte Verwaltung
nach Trustwechsel; erfolgreiche Ausgabe laesst beide Dateien bytegleich.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-stored-authorization-ci.log. Keine UI-Aenderung in diesem Schritt.
Naechster Schritt bleibt integrierte Vorbereitung/Autorisierung und kontrollierte
Aktivierung; kein neuer Release, Ziel weiterhin aktiv.

Nachfolgervorbereitung mit Windows-CurrentUser-DPAPI geprueft (2026-10-21):
Isolierte synthetische Bibliothek/Schluesseldatei erstellt und mit neuer Store-
und Schutzinstanz geladen. Falsche Bibliotheksentropie, Trust-Zweck und aktiver
Verwaltungszweck koennen die Vorbereitung nicht entschluesseln. Private Sicherung
vor Autorisierung nicht aktivierbar; nach bestaetigtem Trustwechsel wiederhergestellt,
neue Richtlinie signiert und akzeptiert. Vorbereitung bleibt bytegleich erhalten.
App-Build Exit 0 ohne Warnungen/Fehler: tmp/group-successor-dpapi-build.log.
UI-Smoke Exit 0, status OK, 57 Checks, stderr leer:
tmp/group-successor-dpapi-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Kein Zertifikatsspeicher veraendert; dies ist keine Zweitbenutzerabnahme.
Naechster Schritt: integrierte Vorbereitung/Autorisierung und wiederaufnehmbare
Aktivierung mit ausdruecklicher Behandlung vorhandener Verwaltungsdateien.

Geschuetzte Nachfolgervorbereitung im Core umgesetzt (2026-10-21):
OfflineGroupSuccessorStore speichert neuen RSA-3072-Schluessel, Ausgangsstand
und signierte oeffentliche Anforderung ohne Aktivierung als Verwaltung.
Nur Neuanlage; geschuetztes Neuladen mit Schluessel-/Signaturbindung;
Anforderungsexport verlangt unveraenderten registrierten Ausgangsstand.
Private verschluesselte Sicherung bleibt nach Trustwechsel exportierbar.
Wiederherstellung als aktive Verwaltung gelingt erst nach bestaetigtem Wechsel
und nur in fehlende Zieldatei. Eigener Windows-CurrentUser-DPAPI-Zweck angelegt.
Synthetische Tests: Neustart, falsche Bibliothek/Schutzinstanz, stale Checkpoint,
Ueberschreibschutz, vorzeitige Aktivierung abgewiesen, autorisierter Wechsel mit
anschliessendem Signieren, konkurrierendes Update bei Vorbereitung und korrupte
Schluesseldatei. CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-successor-store-ci.log. Sandbox-Build brach zuvor beim Restore ohne
Compilerdiagnose ab; regulaerer Lauf ausserhalb der Sandbox erfolgreich.
Noch offen: Lauf des neuen DPAPI-Zwecks, Bedienweg fuer Vorbereitung und
Autorisierung, kontrollierter Umgang mit bereits vorhandener lokaler Verwaltung.
Kein neuer Release, keine echten Identitaeten oder Zertifikatsspeicher veraendert.
Ziel bleibt aktiv.

Getrennte Nachfolgeranforderung und Autorisierung im Core umgesetzt (2026-10-21):
Prepare benoetigt nur neuen privaten Schluessel und registrierten oeffentlichen
Ausgangsstand. Eigenes strikt begrenztes Request-Format mit Nachfolgersignatur.
Authorize benoetigt nur oeffentliche Anforderung, bisherigen privaten Schluessel,
aktuellen Ausgangsstand, unabhaengigen neuen Fingerabdruck und Bestaetigung.
Kein Austausch privater Schluessel notwendig. Create kombiniert diese Bausteine
weiterhin fuer bestehende Core-Aufrufer. Request kann nicht als fertiger Wechsel
importiert werden, vollstaendiger Wechsel nicht als Request; private Snapshots
verhindern nachtraegliche Manipulation der autorisierten Ausgabe.
Erweiterte Tests fuer beide getrennten Schritte, fehlende/falsche Bestaetigung,
falschen bisherigen Schluessel, geaenderten Ausgangsstand, manipulierte Payload/
Nachfolgersignatur, Formate, doppelte/unbekannte Felder, Groessenlimit und
nachtraegliche Aenderung der Aufruferarrays. CI Exit 0, 71/71, CLI/WPF erfolgreich:
tmp/group-successor-request-ci.log.
Noch keine persistente private Vorbereitung oder neuer Erzeugungs-/Autorisierungs-
dialog; naechster Schritt ist geschuetztes Speichern/Neuladen beim Nachfolger.
Ziel aktiv, bestehender Release und Identitaeten unveraendert.

Verwaltungswechsel-Importdialog integriert (2026-10-20, Nachweise am 21.10.
nach Unterbrechung erneut gelesen): eigene Aktion in Einstellungen, begrenztes
Einlesen der Wechseldatei, explizite Auswahl einer vorhandenen Registrierung.
Alte/neue Fingerabdruecke, Revisionen, unveraenderte Rechte und lokale Wirkung
werden angezeigt. Unabhaengige Eingabe des neuen Fingerabdrucks plus Bestaetigung
erforderlich. Inspect prueft nur kryptografisch fuer die Anzeige; Verify und
persistenter Import verlangen weiterhin den extern bestaetigten Fingerabdruck.
Neue UI-Pruefungen: kein Schreiben bei Vorschau/falschem Fingerabdruck/fehlender
Bestaetigung, fehlgeschlagene Bibliothekswahl verwirft vorheriges Ziel, eingefrorener
Dateikandidat, Konkurrenz- und Wiederholungsabwehr. Windows-DPAPI im isolierten
Testverzeichnis; keine Zertifikatsspeicheraenderung.
CI-Log: tmp/group-transition-dialog-ci.log, 71/71, abschliessende Builds erfolgreich.
UI-Bericht status OK, 56 Checks/5 Szenarien, stderr leer:
tmp/group-transition-dialog-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Alter Prozesshandle 40501 beim Wiederaufnehmen nicht mehr vorhanden; kein neuer
Lauf allein wegen Unterbrechung gestartet. Kein nachtraeglich behaupteter Exitcode.
Native Dateiauswahl/Sichtpruefung des neuen Dialogs noch offen.
Naechster Schritt: private Uebergabe mit getrennten Schluesselinhabern. Nachfolger
soll den neuen Schluessel selbst erzeugen und eine oeffentliche Besitzbestaetigung
liefern; bisherige Verwaltung autorisiert diese ohne Kenntnis des neuen privaten
Schluessels. Vorhandenes Create mit beiden RSA-Objekten ist bisher nur Core-/Test-
Baustein, kein fertiger verteilter Uebergabeprozess. Ziel aktiv, Release unveraendert.

Persistenter Verwaltungswechsel-Import umgesetzt (2026-10-20):
OfflineGroupTrustStore bietet getrennte Vorschau und ausdruecklich bestaetigten
Import. Beide pruefen den lokal gespeicherten Anker und erwarteten Checkpoint.
Import speichert neuen Anker plus Richtlinie in einer geschuetzten Datei ueber
den vorhandenen Dateihash-Konfliktschutz. Keine Neuregistrierung/kein Loeschen.
Erweiterte Tests: Vorschau und Ablehnungen unveraendert, simuliertes Versagen
des Speicherschutzes erhaelt alten Stand, konkurrierende normale Aktualisierung
und konkurrierender Verwaltungswechsel gewinnen gegen den veralteten Schreiber.
Store-Neustart erhaelt neuen Anker/Checkpoint; alter Schluessel auch bei Revision
100 und wiederholter Import abgewiesen, neuer Schluessel kann weiter aktualisieren.
CI Exit 0, 71/71, CLI/WPF ohne Warnungen/Fehler:
tmp/group-administrator-transition-store-ci.log.
Nur synthetischer lokaler Schutzzugang; keine echte Benutzertrennung behauptet.
Naechster Schritt: wiederaufnehmbare private Verwaltungsschluessel-Uebergabe und
geführter UI-Import. Ziel aktiv; bestehender Release unveraendert.

Reiner Verwaltungswechsel-Pruefkern umgesetzt (2026-10-20):
GroupAdministratorTransition bindet alten SPKI/Checkpoint, Bibliothek und neues
signiertes Paket an zwei domaenengetrennte RSA-PSS/SHA-256-Signaturen.
Unabhaengig bestaetigter neuer Fingerabdruck erforderlich; genau eine Revision
weiter, Richtlinieninhalt sonst unveraendert. Kein impliziter Ankerwechsel,
keine Persistenz/Bedienung in diesem Baustein. Normale Paketupdates lehnen den
neuen Anker weiterhin ab und lassen gespeicherten Trust unveraendert.
Neue Tests fuer Signatur-/Payloadmanipulation, falsche Bibliothek/Anker/
Ausgangsstaende, Wiederholung, gleichbleibenden Schluessel, korrekt doppelt
signierte Rechte-/Revisionsmanipulation, doppelte/unbekannte JSON-Felder und
Groessenbegrenzung. Personenverzeichnis plus bestehender Vault unter neuem
Pruefstand normal und per separater Recovery lesbar; Mitglieder-/Recovery-
Schluessel als neue Verwaltung abgewiesen.
CI nach ergaenztem Vault-Test Exit 0, 71/71, CLI/WPF erfolgreich:
tmp/group-administrator-transition-verified-ci.log.
Naechster Schritt: konfliktgepruefter persistenter Uebergangsimport, danach
wiederaufnehmbare private Uebergabe und UI. Keine neue Hardware-/Mehrbenutzer-
Freigabe, keine Releaseaenderung. Ziel aktiv.

Wiederaufnahme/Auftragsabgleich (2026-10-20): Arbeitsbaum sauber, getesteter
Recovery-Commit c3048f6 war noch nicht remote und wurde jetzt auf den privaten
Branch gepusht. Vorhandene Build-/Smoke-Nachweise erneut gelesen, nicht ohne
Aenderung wiederholt. Der Abgleich mit Implementierungsfolge Punkt 2 zeigt
einen bisher nicht umgesetzten Administrator-Schluesselwechsel: RestorePrivateBackup
restauriert denselben registrierten Schluessel, VerifyPackageUpdate lehnt
abweichende Anker ab. Das bleibt fuer normale Updates richtig, ersetzt aber
keinen gesonderten Wechselweg. Konkreter offener Vertrag und Abnahmen in
OFFLINE-GROUP-ADMINISTRATOR-CHANGE.md. Ziel daher nicht auf reine UI-/Hardware-
Abnahme verengt. Naechster Entwicklungsbaustein ist der gesonderte
Uebergangs-Verifikationskern, danach Persistenz/Bedienweg.
WindowsSandbox.exe am Standard-Systempfad nicht vorhanden; daraus keine
generelle Aussage ueber andere VM-Umgebungen abgeleitet. Echte Windows-Recovery-
Persistenz/Zweitbenutzerabnahme weiterhin unbewiesen. Ziel aktiv.

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

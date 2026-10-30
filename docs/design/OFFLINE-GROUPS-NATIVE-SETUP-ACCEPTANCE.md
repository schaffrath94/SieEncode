# Native Einrichtung in der Test-VM

Stand 29.10.2026, letzter Bedienlauf 28.10.2026. Installierter Kandidat 1.3.0-offline-groups.1,
SieEncode-Testlab-LTSC, Windows-Benutzer labadmin. Ausschliesslich synthetische
Identitaet. Diese Pruefung verwendet sichtbare Dialoge, nicht Testability-Aufrufe.

## Bestanden

- Einstellungen und Vault-Ersteinrichtung geoeffnet. Neue persoenliche Identitaet
  mit voreingestelltem Testnamen labadmin ueber Namensdialog/OK angelegt.
- Nach zwischenzeitlichem VM-Neustart und erneutem Appstart dieselbe Identitaet
  vorhanden: Einstellungen melden eine lokale Identitaet, Assistent zeigt labadmin
  und denselben Fingerabdruck. Keine zweite Identitaet erzeugt.
- Oeffentliche Einladung exportieren oeffnet den nativen Windows-Speicherdialog.
  Vorgeschlagenen Namen im Dokumente-Ordner gespeichert; Dialog kehrt zurueck.
- Erzeugte Datei C:/Users/labadmin/Documents/labadmin.recipient.json separat
  gelesen: Format SieEncode-Vault-Recipient, Anzeigename labadmin,
  Zertifikat HasPrivateKey=false, SHA256 des Zertifikats entspricht Fingerprint.
  Datei-SHA256: B11253A6D0B2B0C0524736F996D436FABF861B5BF48578889906AAD83551152B.
- Assistent ueber X geschlossen. Einstellungen zeigen weiterhin eine Identitaet
  und null bisherige Teammitglieder; die Anzeige grenzt sich ausdruecklich von
  Gruppenrechten ab. Fuer Gruppenbeitritt wurde keine alte Teamliste gespeichert.

## Gruppenregistrierung am 28.10.2026 bestanden

Im nativen Dateidialog die vorbereitete synthetische Datei
`C:/Users/labadmin/Documents/native-test.group-enrollment.json` gewaehlt.
Registrierungsdialog zeigt Bibliothek `494f0a91-fc19-4e0d-8a73-30b4b4578e76`,
Revision 8 und zwei Gruppen. Oeffentlichen SHA256-Verwaltungsfingerabdruck aus
der kontrollierten Testdatei eingegeben, Bestaetigung angekreuzt und
**Verwaltung registrieren** angeklickt. Dialog schliesst; Status bestaetigt
Registrierung fuer diesen Windows-Benutzer, ohne Projekt-Vaults zu veraendern.

Anwendung ueber Gast-Taskleiste regulaer geschlossen und installierte EXE neu
gestartet. Unter **Registrierte Gruppen aktualisieren** dieselbe Datei erneut
gewaehlt: **Dieser Gruppenstand ist bereits registriert.** Damit hat der neue
App-Prozess den geschuetzten lokalen Trust geladen und denselben signierten
Stand erkannt. Keine zweite Registrierung und kein Zuruecksetzen vorgenommen.

Grenze: Fingerabdruck aus synthetischem Testmaterial, kein unabhaengiger
menschlicher Vertrauenskanal. labadmin wurde keiner Gruppe hinzugefuegt;
Registrierung belegt keine Vaultberechtigung. Echte Windows-Benutzertrennung
ist separat in den VM-API-Pruefungen dokumentiert.

Computer-Use-Texteingaben und Tastenkombinationen kamen in der VM nicht an.
Oeffentlichen Testfingerabdruck deshalb als Textdatei uebertragen, im Gast-Editor
per Maus markiert/kopiert und per Kontextmenue in den Dialog eingefuegt.
Keine geteilte Host-Zwischenablage aktiviert und keine privaten Schluessel kopiert.
Direkte Notepad-Aufrufe mit VBox-start hatten Dateinamenfehler; der Gastaufruf
ueber PowerShell Start-Process oeffnete die Datei korrekt. Kein Produktfehler.

Originalaufnahmen unter `../images/onboarding/`, Hashes in `SHA256SUMS.json`:
01 Arbeitsplatz, 02 Gruppenbereich, 03 Registrierungsdialog, 04 Erfolgsmeldung,
05 Wiedererkennung nach App-Neustart. Nur synthetische Daten/oeffentliche
Fingerabdruecke; Bilder nicht als Nachweis weiterer ungepruefter Schritte verwenden.

## Verbleibende Grenzen

### Hauptverwaltung: nativer Teillauf 29.10.2026

Gespeicherte Test-VM sichtbar fortgesetzt. Bestehende geschuetzte Dateien
`testerB/AppData/Local/SieEncodeLab/VaultProbe1/{trust,admin}.bin` ohne Veraenderung
der Originale in die normalen GroupTrust-/Administrationspfade dieses Benutzers
kopiert. Vorab Konfliktpruefung (abweichende Zieldateien werden nicht ersetzt),
danach Hashvergleich. Dies ist Testvorbereitung, kein nativer Wiederherstellungs-
oder Ersteinrichtungsnachweis. Helfer in tmp/Open-NativeAdministrator.ps1 und
tmp/Start-NativeAdministratorWithProfile.ps1; LoadUserProfile=true, installierter
Studio-DLL-Hash vor Start gegen Kandidatenbaseline geprueft.

Sichtbarer App-Prozess unter testerB (PID 4100, eigene Profilpfade) gestartet.
Einstellungen / Offline-Gruppen verwalten / bestehende Bibliothek ausgewaehlt:
Personen A/B und zwei bestehende Gruppen geladen, Revision 8 angezeigt.
**Gruppe hinzufuegen** erzeugte leere Gruppe `Neue Gruppe`, ID
`gruppe-8b2767cc5eef4ed88036c26f875eede9`. **Gruppen signiert speichern** zeigte
Revision 9 und lokal aktiven signierten Stand. **Gespeicherten Stand oeffentlich
exportieren** oeffnete nativen Speicherdialog; im Dokumente-Ordner unter dem
vorgeschlagenen Namen gespeichert, anschliessend sichtbare Erfolgsmeldung.
Aufnahmen 06-verwaltung-signiert.png und 07-verwaltung-exportiert.png.

Erwartete Exportdatei:
`C:/Users/testerB/Documents/494f0a91fc194e0d8a7330b4b4578e76.group-enrollment.json`.
Vor unabhaengiger Datei-/Signaturpruefung meldete VBox copyfrom ausdruecklich
powered off. Ursache nicht belegt. Exportdatei noch nicht zum Host kopiert;
Hash, inhaltlicher Vergleich und Import auf labadmin bleiben offen. Nicht erneut
die Gruppe anlegen oder Revision 9 nochmals erzeugen. Originale API-Testdateien
liegen weiterhin getrennt; normale B-App-Dateien jetzt Revision 9, labadmin 8.
Launcher beendet mit Exit 0 und finally-Loeschung der temporaeren Credentialdatei;
separate Existenzkontrolle wegen ausgeschalteter VM noch nicht ausgefuehrt.

Der sichtbare Teillauf ersetzt weder Personen-/Geraeteaenderungen, private
Sicherung/Restore, Recovery-/Delegationsdialoge noch den Gesamtprojektablauf.

### Native Rollentrennung und separate Erstanlage am 30.10.2026

Unter labadmin (registrierte Bibliothek 494f0a91..., aber kein dazugehoeriger
privater Verwaltungsschluessel) Verwaltungsdialog geoeffnet. Leere Auswahl
privat verwalteter Bibliotheken. **Gruppen signiert speichern** verweigert:
**Bitte eine Verwaltung auswaehlen oder erstellen.** Screenshot
`12-keine-verwaltungsrechte.png`. Dies prueft den normalen Oberflaechenweg,
keinen Angriff auf Windows-Administratorrechte oder Dateisystemberechtigungen.

Danach **Neue Verwaltung erstellen**, bestaetigt: separate neue Testbibliothek
`d9c6ffd7-0081-4189-b965-518c9204c10c` in der Auswahl sichtbar. Gast-Dateiinventar
bestaetigt eigenen .admin.bin (3494 Bytes) und .trust.bin (1686 Bytes); bisherige
494f...-Registrierung bleibt vorhanden. Keine Ersetzung der bisherigen Verwaltung.

**Privaten Schluessel sichern** oeffnet den Dialog mit neuer Bibliotheks-ID,
zwei Passphrasefeldern und ausdruecklicher Bestaetigung getrennter Aufbewahrung.
Automatisierte Texteingabe erzeugt keine sichtbaren Passwortzeichen. Deshalb
Benutzer um manuelle Eingabe einer reinen Testpassphrase und Speichern gebeten.
Sicherung und Wiederherstellung zu diesem Zeitpunkt noch nicht bestaetigt.

### Native Ruecksprungabwehr am 30.10.2026

Nach dem unten belegten Import und Neustart mit Revision 9 unter labadmin
erneut **Registrierte Gruppen aktualisieren** ausgewaehlt. Im nativen
Dateidialog die bisherige `native-test.group-enrollment.json` (Revision 8)
geoeffnet. Anwendung verweigert den Import mit
**Veraltete oder widerspruechliche Gruppenfreigabe.**
Originalaufnahme `11-alter-stand-abgewiesen.png` gesichert.
Damit ist die native Abweisung dieses konkreten Ruecksprungs belegt;
ein anschliessendes unabhaengiges Wiederlesen wurde noch nicht ausgefuehrt.
Die weitere Bedienung wurde bei erkannter Benutzereingabe und minimierter VM
unterbrochen. Noch keine native Abnahme fehlender Verwaltungsrechte unter
labadmin, keine Aenderung an Verwaltungsschluesseln oder Mitgliedschaften.

### Native Aktualisierung und Neustart am 30.10.2026

Unter labadmin den unabhaengig validierten oeffentlichen Export Revision 9
ueber **Registrierte Gruppen aktualisieren** und nativen Dateidialog geladen.
Vorschau zeigt dieselbe Bibliothek, Revision 8 -> 9, alte zwei Gruppen und
zusaetzlich die leere Neue Gruppe; Personen A/B und Recovery bleiben erhalten.
Checkbox bestaetigt, **Geprueften Stand uebernehmen** angeklickt. Status meldet
erfolgreiche Aktualisierung und unveraenderte bestehende Vault-Dateien.
Keine Projektdateien in diesem Test geoeffnet oder bearbeitet.

App per Taskleistenmenue regulaer geschlossen (Desktop ohne App sichtbar),
installierte EXE erneut gestartet. Dieselbe Exportdatei erneut ueber die
Aktualisierungsaktion ausgewaehlt: **Dieser Gruppenstand ist bereits registriert.**
Damit ist das Wiederlesen des geschuetzten lokalen Stands im neuen App-Prozess
nativ belegt. Screenshots 08-aktualisierung-vorschau.png,
09-aktualisierung-uebernommen.png und 10-aktualisierung-nach-neustart.png.
Normale App-Staende testerB/labadmin nun Revision 9, API-Testfixtures weiterhin 8.
labadmin erhaelt hierdurch keine Gruppenmitgliedschaft. Kein echter zweiter
Rechner, keine neue Projekt-/Recovery-/Hardwareabnahme.

Einmaliger vom Benutzer autorisierter Windows-Rearm nach UAC-Bestaetigung
erfolgreich ausgefuehrt, regulaerer VM-Neustart abgeschlossen. Danach Rearmcount 1
statt 2, aber LicenseStatus=5 und GracePeriodRemaining=0 unveraendert.
Eine wirksame Verlaengerung ist nicht belegt; kein weiterer Rearm ausgefuehrt.

### Unabhaengige Exportpruefung am 29.10.2026

Nach einmaligem VM-Neustart Export erfolgreich abgeholt. Mit dem per SHA256
identifizierten Kandidaten-Core gelesen und Signatur validiert. Ergebnis in
`offline-groups-native-export-result.json`: Revision 8 -> 9, drei Gruppen,
genau eine neue leere Gruppe; bisherige Gruppen, Personen, Recovery, Delegationen
und Verwaltungsschluessel unveraendert. Export-SHA256
`7C2009EE47C114AF7A3087DAB55B768DA12DF408C7FA59FE4BFB6B9E23BB61C7`.
Pruefung auf dem Host mit Kandidaten-Core, kein behaupteter nativer Zweitimport.
Gastabfrage bestaetigt temporaere Credentialdatei nicht vorhanden.

Abschaltursache jetzt belegt: Windows-Systemlog, Ereignis 1074, Initiator
`C:\WINDOWS\system32\wlms\wlms.exe`, Benutzer SYSTEM, geplanter Shutdown,
Kommentar: Lizenzzeitraum dieser Windows-Installation abgelaufen.
Die letzten vier 1074-Ereignisse nennen diesen Grund; ein aelteres Ereignis 41
meldet zusaetzlich einen unsauberen Neustart mit unbestimmter Ursache.
Die lizenzbedingt geplanten Abschaltungen sind keine SieEncode-Abstuerze.
Keine Lizenz-/Dienst-/Zeitmanipulation vorgenommen. Fuer verlaessliche laengere
Abnahmen wird eine gueltig lizenzierte Testumgebung benoetigt.

Kein vollstaendiger nativer Gruppen- oder Projektbedienlauf. Die Aktualisierung
auf einen neueren Gruppenstand ist seit 30.10. nativ belegt. Hauptverwaltung,
Recoverydialoge, Delegation, Nachfolge und Projektkopien sind weiter vollstaendig nativ abzunehmen;
die separaten VM-API-Nachweise decken diese Oberflaechen nicht ab.
Kein Reader in der VM freigegeben. Einstellungen melden korrekt fehlende
Leserkonfiguration/settings.xml; daraus folgt keine Hardwareabnahme.
Windows zeigt eine abgelaufene Evaluierungslizenz. Keine Lizenzumgehung vorgenommen.
Autologin fuer labadmin auf Benutzerwunsch eingerichtet und am 28.10.2026
praktisch bestaetigt: VBox meldete zuvor poweroff, startvm --type gui erfolgreich;
anschliessend erschien ohne Passworteingabe der Desktop. Gastabfrage
Win32_ComputerSystem.UserName lieferte SIEENCODE-LAB\labadmin. Installierter
SieEncode-Kandidat danach erfolgreich mit sichtbarer Uebersicht gestartet.
Kein Passwort in diesem Nachweis gespeichert. Die zunaechst wegen Hostbedienung
unterbrochene Gruppenregistrierung wurde spaeter wie oben beschrieben fortgesetzt.

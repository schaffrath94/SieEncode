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

Kein vollstaendiger nativer Gruppen- oder Projektbedienlauf. Neuere Gruppenstaende, Hauptverwaltung,
Recoverydialoge, Delegation, Nachfolge und Projektkopien sind weiter nativ abzunehmen;
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

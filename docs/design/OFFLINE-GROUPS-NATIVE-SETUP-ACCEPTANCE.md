# Native Einrichtung in der Test-VM

Stand 26.10.2026. Installierter Kandidat 1.3.0-offline-groups.1,
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

## Grenzen und naechste Schritte

Kein vollstaendiger nativer Gruppen- oder Projektbedienlauf. Import, Hauptverwaltung,
Recoverydialoge, Delegation, Nachfolge und Projektkopien sind weiter nativ abzunehmen;
die separaten VM-API-Nachweise decken diese Oberflaechen nicht ab.
Kein Reader in der VM freigegeben. Einstellungen melden korrekt fehlende
Leserkonfiguration/settings.xml; daraus folgt keine Hardwareabnahme.
Windows zeigt eine abgelaufene Evaluierungslizenz. Keine Lizenzumgehung vorgenommen.
Autologin fuer labadmin auf Benutzerwunsch eingerichtet, aber noch nicht durch
einen gezielten anschliessenden Neustart bestaetigt.

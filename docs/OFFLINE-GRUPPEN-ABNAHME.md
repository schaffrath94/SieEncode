# Offline-Gruppen: Abnahme auf getrennten Windows-Arbeitsplätzen

Testkandidat, keine Freigabe für produktive Vaults. Die automatisierten Prüfungen
ersetzen diese Abnahme nicht. Ausschließlich neue synthetische Projekte und
Testidentitäten verwenden; vorhandene Identitäten und Vaultdateien erhalten.

Der Kandidat verwendet den regulären SieEncode-Installer. Eine Installation kann
eine vorhandene SieEncode-Version ersetzen. Deshalb auf einem separaten
Testrechner bzw. einer geeigneten Test-VM installieren, nicht ungeprüft über eine
benötigte Installation. Administrative MSI-Extraktion ist keine Installation.

## Vorbereitung und Artefaktidentität

- MSI und SHA256SUMS.json müssen aus demselben Kandidatenpaket stammen. Den
  MSI-Hash mit `Get-FileHash -Algorithm SHA256 <MSI-Pfad>` vergleichen.
- Paketversion, MSI-Hash, Studio-Version und Windows-Umgebungen im Ergebnis
  festhalten. Keine privaten Schlüssel, Passphrasen oder Vaultinhalte beilegen.
- Zwei tatsächlich getrennte Windows-Benutzer oder Rechner A und B verwenden.
  Zwei Ordner unter demselben Benutzer belegen keine Benutzerisolation.
- Einen neuen gemeinsamen Testprojektordner verwenden. A übernimmt die
  Hauptverwaltung, B ist zunächst normales Teammitglied. Eine separate
  Recovery-Sicherung verschlüsselt und außerhalb des gemeinsamen Ordners verwahren.
- Die [Einrichtungsanleitung](OFFLINE-GRUPPEN-SCHNELLSTART.md) verwenden. Ein
  unbekannter Fingerabdruck wird nicht allein aufgrund einer Datei bestätigt.

## Durchgehender Bedienlauf

| Schritt | Aktion | Erwartetes Ergebnis |
| --- | --- | --- |
| 1 | Auf A Verwaltung erstellen, private Sicherung erstellen; A und B erzeugen jeweils eigene persönliche Identitäten und exportieren öffentliche Einladungen. | Eigene Schlüssel pro Gerätezugang; private Sicherungen werden nicht als Einladungen verteilt. |
| 2 | Auf A beide Personen aufnehmen; Lokal enthält A+B, Lokal 2 nur A. Separate Recovery einrichten, öffentlich freigeben und Gruppenstand exportieren. | Gruppen und Recovery werden getrennt angezeigt; Verwaltung allein erteilt keinen normalen Vaultzugang. |
| 3 | Auf B öffentliche Registrierung mit unabhängig geprüftem Verwaltungsfingerabdruck importieren. Anwendung schließen und neu starten. | Bibliothek und Versionsstand bleiben registriert. Falscher Fingerabdruck und fremde Bibliothek werden abgewiesen. |
| 4 | A erstellt synthetische Gruppenprojekte für beide Gruppen, speichert und öffnet sie erneut. B öffnet beide Projekte. | B kann Lokal öffnen; Lokal 2 wird abgewiesen. B kann selbst ein neues Lokal-Projekt erstellen und wieder öffnen. |
| 5 | B öffnet ein bereits lokal verfügbares berechtigtes Projekt ohne Verbindung zum gemeinsamen Ordner. | Mit importierter Registrierung und eigener Identität ist Offline-Entschlüsselung möglich. Kein Onlinedienst ist erforderlich. |
| 6 | A entfernt B aus Lokal; B bleibt zunächst offline. A gleicht den vorhandenen Vault nach Vorschau ausdrücklich ab. | Neuer Vault erhält frischen Inhaltsschlüssel; verschlüsselte Altsicherung bleibt erhalten. Ein neuer Gruppenstand allein ändert alte Vaultkopien nicht. |
| 7 | B öffnet bewusst die alte lokale Kopie unter altem lokalem Stand. Danach neuen Stand importieren und aktuellen Vault prüfen. | Alte Offlinekopie kann weiterhin zugänglich sein. Nach Import wird B abgewiesen. Alten Stand erneut importieren: Rücksprung wird abgewiesen. |
| 8 | A nimmt B wieder auf und gleicht ab. A erteilt B eine begrenzte Verwaltungsvollmacht für Lokal. B erstellt einen Änderungsantrag; A prüft und autorisiert ihn. | Antrag darf nur Lokal ändern. Lokal 2 und Recovery bleiben außerhalb der Vollmacht. Wiederholter oder veralteter Antrag wird abgewiesen. |
| 9 | Vollmacht entziehen, neuen Stand auf B importieren und mit vorhandenem Delegatenschlüssel erneut versuchen. | Keine neuen gültigen Änderungen unter der entzogenen Vollmacht; Identität bleibt erhalten. |
| 10 | Separate Recovery-Sicherung auf einem dafür vorgesehenen Testarbeitsplatz wiederherstellen, Anwendung schließen und neu starten. | Tatsächlich installierter Windows-Schlüssel kann einen Test-Vault über den ausdrücklichen Recoveryweg öffnen. Normale Station/Buildfreigabe entsteht dadurch nicht. |
| 11 | Testprojekt kopieren und unter neuem Namen speichern; beide Kopien wieder öffnen. | Neue Projekt-ID, aktuelle Empfänger, erhaltene Projektschlüssel. Quelle bleibt unverändert. Manipulierte Projekt-/Gruppenzuordnung wird abgewiesen. |

Nicht auf reale Karten schreiben. Dieser Ablauf belegt keine Hardwarefreigabe.

## Verwaltungsnachfolge separat prüfen

Auf B Nachfolger vorbereiten, private Sicherung erstellen und nur die öffentliche
Anfrage an A geben. A bestätigt den unabhängig erhaltenen neuen Fingerabdruck
und autorisiert. Übergabe auf B ausdrücklich importieren, privaten Nachfolger
aktivieren, Anwendung neu starten und eine neue Änderung signieren. Den neuen
Stand auf A übernehmen. Alte Verwaltung darf danach keinen neuen Stand mehr
einspielen. Normale Gruppenrechte, begrenzte Vollmachten und Recovery bleiben
unverändert. Die detaillierten Schritte stehen in der Einrichtungsanleitung.

Ein verlorener oder kompromittierter alter Verwaltungsschlüssel ist kein normaler
Nachfolgefall. Es gibt keinen automatischen Ersatzanker aus dem gemeinsamen
Ordner. Lokalen Trust nicht löschen oder auf einen älteren Stand zurücksetzen.

## Ergebnisprotokoll

Für jeden Schritt `bestanden`, `fehlgeschlagen` oder `nicht geprüft` eintragen.
Bei Fehlern Aktion und genaue Meldung notieren; keine Geheimnisse fotografieren
oder mitsenden. Tatsächliche Installation, Neustart, Benutzertrennung,
Recovery-Installation und Offlineprüfung einzeln bestätigen. Nicht ausgeführte
Schritte bleiben offen, auch wenn lokale automatisierte Tests grün sind.

## Öffentliche Freigabe an den zweiten Arbeitsplatz übergeben

Am 19.11.2026 auf SieEncode-Testlab-LTSC aus dem lokalen Vertrauensspeicher von testerB über die installierte Kandidat-5-Produkt-API exportiert. Vor dem Schreiben wurden Signatur, bekannter Hauptverwaltungsfingerabdruck, Bibliothek d9c6ffd7-0081-4189-b965-518c9204c10c und Revision 6 geprüft. Die aktive Vollmacht der Testperson B umfasst genau die Gruppe Lokal. Unter testerA wurde dieselbe öffentliche Datei in den öffentlichen Dokumenten des Gasts unabhängig gelesen und gehasht. SHA-256: 1B810097125A1C5EDDDF28221AC225814018B698B5A4D1143DFE56C55A0FA44E.

Nachweisumfang: Übergabe der unveränderten öffentlichen Datei zwischen zwei Windows-Konten. Die ausdrückliche Registrierung im installierten Studio von testerA, die Vollmacht für dessen eigenen Signierschlüssel und die anschließenden Dialogprüfungen sind offen.

Am 19.11.2026 wurde die öffentliche Datei im installierten Kandidaten 5 unter testerA über Einstellungen → Gruppenverwaltung registrieren geöffnet. Der Bestätigungsdialog zeigte die erwartete Bibliotheks-ID, Richtlinienstand 6 und zwei Gruppen. Nach Eingabe des unabhängig dokumentierten Verwaltungsfingerabdrucks und explizitem Abgleich meldete Studio die erfolgreiche Registrierung für diesen Windows-Benutzer. Das VM-Netzwerkkabel war getrennt. SieEncode lief mit geladenem testerA-Profil in der interaktiven Laborsitzung. Die Registrierung erteilt für sich keine Vollmacht. Die begrenzte Delegation an einen eigenen Signierschlüssel von testerA und der anschließende Entzug sind separate Bediennachweise.

Die Hauptverwaltung unter testerB hat zur Vorbereitung des getrennten Bediennachweises mit der installierten Kandidat-5-Produkt-API die eigene Testperson A (person-0446ed60e14f4313a622f5cc9965f2be) angelegt. Ausgangsstand war die signierte Revision 6 mit abgeglichenem Verwaltungsfingerabdruck und bestehender Vollmacht für B. In Revision 7 wurden für A null Geräte, null Gruppenmitgliedschaften und null Vollmachten geprüft. Der öffentliche Export liegt in den öffentlichen Dokumenten der VM als Vault-Abnahme-Revision7.group-enrollment.json. SHA-256 A8718236C9F46034D8A62637D91EFE722810D1494652D4101CEF8750D99AF567. Der Aktualisierungsdialog unter testerA, die Einladung mit eigenem Signierschlüssel und die spätere gezielte Freigabe sind getrennte Nachweise.

Unter testerA wurde Revision 7 in der installierten Oberfläche übernommen. Für Testperson A erschien keine aktive Vollmacht. Der Export der öffentlichen Verwaltungseinladung war erfolgreich. Ihr angezeigter Fingerabdruck 8306FFF624CFE20EA03A72E7C4C7873D7BC15C2622385B2A4DA63B48E7420FBB stimmt mit dem unabhängig berechneten SHA-256 des öffentlichen Signierschlüssels überein. Transportformat, Bibliotheks-ID und Personen-ID wurden vor der Übergabe geprüft. Die öffentliche Datei liegt in der Test-VM unter C:\Users\Public\Documents\Testperson-A.group-delegate.json (Datei-SHA-256 C2D7CE877EDDB4A4FE21295477749713442211A8DFA8C52095E5438D29BF5B09). Als Nächstes unter testerB ausschließlich Lokal freigeben und den signierten Stand an testerA übertragen.

Unter testerB wurde die Einladung für Testperson A mit dem separat erfassten Fingerabdruck über die installierte Produkt-API des Kandidaten 5 freigegeben. Der signierte Ausgangsstand entsprach Revision 7. Die geprüfte Revision 8 enthält für A genau eine aktive Vollmacht für den eingeladenen Signierschlüssel, ausschließlich für Lokal (gruppe-f96d623a10a84f1fb80b660539135e7f). Der lokale Vertrauensspeicher von B bestätigte denselben Stand. Unter testerA wurde die öffentliche Datei Vault-Abnahme-Revision8.group-enrollment.json unabhängig gelesen und die Datei-SHA-256 2789DA298D9454D35CD3B954283BC740AFFD90826E9D6BED7858D86560574365 bestätigt. Der Vergleich der Datei unter A ist vom Import in As Vertrauensspeicher zu unterscheiden. Die Dialogfreigabe war an diesem Vorgang nicht beteiligt. Als Nächstes Revision 8 unter A importieren und einen auf Lokal begrenzten Änderungsantrag prüfen.

### Gruppenänderung und Entzug über zwei Windows-Konten, 19.11.2026

Installierter Kandidat 5, Produkt-API im Testgast. testerA übernahm die geprüfte Revision 8 in seinen lokalen Vertrauensspeicher. Ein Signierversuch für die zweite Gruppe wurde wegen Überschreitung der Vollmacht abgewiesen. Für Lokal signierte A eine Umbenennung in „Lokal (Abnahme)“. testerB prüfte Signatur, Gruppe, neuen Namen und unveränderte Mitgliedschaften, übernahm den Antrag und erhielt Revision 9. Dieselbe Anfrage gegen den aktuellen Stand wurde wegen des abweichenden Bibliotheksstands abgewiesen. Der anschließend erneut gelesene signierte Prüfstand blieb identisch. B entzog nur As Vollmacht und verteilte Revision 10. A importierte die Revisionen 9 und 10 und versuchte mit seinem weiterhin vorhandenen lokalen Schlüssel, die Gruppe erneut zu ändern. Die Produkt-API wies den Signierversuch mit „Gruppenverwaltung ist nicht freigegeben.“ ab.

| Öffentliche Testdatei | SHA-256 |
| --- | --- |
| Lokal-Testperson-A-R8.group-change.json | 1F2F799829A28999F204E55A46873AB03E7988B1F581B90F44A423D696EA6534 |
| Vault-Abnahme-Revision9.group-enrollment.json | 8D32ED5E4E1F9F192C9387ABA5F6847C2ADBE0FFE0E5AABDCDDCBEBE4589F94B |
| Vault-Abnahme-Revision10.group-enrollment.json | 1055FB0F1EE81450E67CEB563FA03E470C1252C4A40464406CB274E36859E1E1 |

Der Nachweis betrifft die installierten Produktbibliotheken und zwei getrennte Windows-Profile auf derselben VM. Die Dialogbedienung dieser Schritte und die Verwaltungsnachfolge sind getrennte Abnahmen.

### Verwaltungsnachfolge zwischen zwei Windows-Profilen

Installierter Kandidat 5 in derselben Test-VM, eigene synthetische Bibliothek 7b0082ce-2af3-4db2-bf21-233289993d31. Unter testerB wurden zwei Gruppen angelegt. Unter testerA wurde der geprüfte öffentliche Ausgangsstand registriert und ein eigener DPAPI-geschützter Nachfolgeschlüssel vorbereitet. Der Test übernahm den alten bzw. neuen öffentlichen Fingerabdruck jeweils direkt aus der Ausgabe des erzeugenden Prozesses. B autorisierte die öffentliche Anfrage. A importierte die Übergabe und aktivierte den Nachfolger bei Revision 3. In einem neuen Prozess öffnete A seinen lokalen Verwaltungsspeicher erneut und signierte die Umbenennung von Lokal in „Lokal nach Übergabe“ als Revision 4. B übernahm Übergabe und Änderung, prüfte die beiden Gruppennamen und wies anschließend einen mit dem alten Schlüssel signierten Stand trotz höherer Revision ab. Der lokale Prüfstand blieb nach der Ablehnung identisch.

Öffentlicher Export der Revision 4, SHA-256: 64C38BFF5ADEC9F88367AA475144629A03D8282A7B600FE92C38C622CFA0CE25. Prüfung über die installierte Produkt-API mit zwei Windows-Konten und neu gestarteten Prüfprozessen. Sichtbare Studio-Dialoge, Neustart der Studio-Anwendung und private Sicherung im Nachfolgedialog wurden in diesem Lauf nicht bedient. Die Bibliothek für die Delegationsabnahme bleibt bei Revision 10.

Im sichtbaren Studio von testerB wurde die Bibliothek der Delegationsabnahme über Einstellungen → Offline-Gruppen verwalten geladen. Der Dialog zeigte Revision 10, „Testperson B · aktiv · Lokal (Abnahme)“ und „Testperson A · entzogen · Lokal (Abnahme)“. Über „Signierte Gruppenänderung prüfen und übernehmen“ wurde der bereits verarbeitete Antrag Lokal-Testperson-A-R8.group-change.json erneut geöffnet. Studio zeigte „Gruppenänderung passt nicht zum aktuellen Bibliotheksstand.“ Die angezeigte Revision blieb 10. In Kandidat 5 lag der rote Fehlertext unterhalb des sichtbaren Ausschnitts und wurde erst nach Scrollen sichtbar.

Am 20.11.2026 meldete der Bediener in der Test-VM beim Test mit ausgewählter Bibliothek den roten Text „Ungültige delegierte Gruppenänderung“ erst nach manuellem Scrollen. Damit ist die Sichtprüfung für Kandidat 6 fehlgeschlagen. Die SHA-256 der öffentlichen Datei Lokal-Testperson-A-R8.group-change.json in den öffentlichen Dokumenten der VM wurde erneut mit dem oben dokumentierten Wert abgeglichen. Welche Bibliotheks-ID im Dialog ausgewählt war, wurde bei diesem Versuch nicht erfasst. Die Ablehnung wegen veralteter Revision aus dem früheren Test wurde hier nicht beobachtet.

Am 20.11.2026 bestätigte der Bediener im installierten Kandidaten 7, dass die rote Meldung beim Sichttest mit dem alten Gruppenantrag sofort vollständig erschien und beim Scrollen vollständig sichtbar blieb. Damit ist die Sichtprüfung bestanden. Der genaue Meldungstext wurde in diesem Durchlauf nicht protokolliert.

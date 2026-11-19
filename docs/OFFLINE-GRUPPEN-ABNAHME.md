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

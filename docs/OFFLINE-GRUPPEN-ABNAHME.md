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

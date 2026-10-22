# Offline-Gruppen einrichten

Entwicklungsstand auf `codex/offline-vault-groups`, noch nicht Teil von 1.2.1.
Die vollständige Bedienungs- und Mehrbenutzerabnahme steht aus.
Diese Anleitung ist unter **Einstellungen → Offline-Gruppenverwaltung →
Schritt-für-Schritt-Anleitung öffnen** erreichbar und wird mit dem Installer
als lokale Datei mitgeliefert.

## Wer macht was?

| Rolle | Aufgabe |
| --- | --- |
| Teammitglied | Eigene Identität verwenden, öffentliche Einladung weitergeben, Gruppenstand registrieren/aktualisieren und berechtigte Projekte öffnen. |
| Hauptverwalter | Personen, Geräte, Gruppen, Recovery-Zugänge und Verwaltungsvollmachten pflegen; Änderungen signieren und öffentlich verteilen. |
| Beauftragter Gruppenverwalter | Änderungen für ausdrücklich freigegebene Gruppen vorbereiten. Die Hauptverwaltung bestätigt sie vor der Verteilung. |
| Recovery-Verantwortlicher | Separate private Recovery-Sicherung verwahren und nur bei Bedarf auf einem vertrauenswürdigen Arbeitsplatz wiederherstellen. |

Verwaltungsrechte allein erlauben kein Entschlüsseln. Auch der Hauptverwalter
benötigt für normale Projektarbeit eine eigene Gruppenmitgliedschaft.

## Einmalig durch den Hauptverwalter

1. Gemeinsame Projekt- und Profilordner in **Einstellungen** auswählen und speichern.
2. Unter **Offline-Gruppen verwalten** eine neue Verwaltung erstellen. Damit
   entsteht eine Bibliotheks-ID und der eigene öffentliche Stand wird lokal registriert.
   Für ein bestehendes Team dessen Verwaltung verwenden, keine Ersatzverwaltung anlegen.
3. **Privaten Schlüssel sichern** ausführen. Die private Sicherung und ihre
   Passphrase getrennt verwahren; beides gehört nicht in den allgemein zugänglichen Projektordner.
4. Personen anlegen, deren öffentliche Geräteeinladungen aufnehmen und die
   Fingerabdrücke unabhängig mit den Personen vergleichen. Eine Person kann
   mehrere Geräte haben und mehreren Gruppen angehören.
5. Gruppen anlegen, Personen zuordnen und **Gruppen signiert speichern** wählen.
6. Unter **Separate Recovery-Identität einrichten** für die registrierte Bibliothek
   einen eigenen Recovery-Schlüssel erzeugen und verschlüsselt sichern. Die
   öffentliche Recovery-Einladung exportieren. Die Hauptverwaltung nimmt diese
   unter **Separate Recovery-Zugänge** nach unabhängigem Fingerabdruckabgleich auf.
   Der private Recovery-Schlüssel muss für die normale Einrichtung nicht installiert werden.
7. **Gespeicherten Stand öffentlich exportieren**. Nur diese öffentliche
   Registrierung an Kollegen verteilen. Bibliotheks-ID und Verwaltungsfingerabdruck
   zusätzlich über einen unabhängig bestätigten Weg abgleichen, beispielsweise persönlich.

Beispiel für die gewünschte Zuordnung:

| Gruppe | Personen |
| --- | --- |
| Lokal | 1, 2, 3, 4, 5, 6, 7, 8 |
| Lokal 2 | 3, 4, 5, 8 |
| Lokal 3 | 1, 2, 3, 6, 7, 8 |

Eine Verwaltung mit drei Gruppen genügt. Separate Recovery-Zugänge gelten für
die ganze Bibliothek; sie sind nicht auf eine dieser Gruppen begrenzt.

## Einmalig auf jedem Arbeitsplatz

1. Dieselben gemeinsamen Projekt- und Profilordner einstellen.
2. In **Vault-Ersteinrichtung starten** eine vorhandene persönliche Identität
   auswählen; nur bei Bedarf eine neue anlegen. **Öffentliche Einladung exportieren**
   und an den Hauptverwalter geben. Für Gruppen-Vaults muss die bisherige Teamliste
   nicht ergänzt oder gespeichert werden. Danach den Dialog schließen.
3. Nach Aufnahme durch den Hauptverwalter **Gruppenverwaltung registrieren** wählen,
   dessen öffentliche Datei laden und den unabhängig erhaltenen Fingerabdruck bestätigen.
4. Spätere öffentliche Stände über **Registrierte Gruppen aktualisieren** übernehmen.
   Ein gemeinsamer Projektordner ersetzt diesen lokalen Import nicht.

Die Registrierung allein erteilt keine Mitgliedschaft. Jede neue Geräteidentität
muss zentral der richtigen Person zugeordnet werden. Updates der Anwendung
benötigen keine neue persönliche Identität.

## Projekte und Änderungen im Alltag

Im Projekteditor **Als neues Gruppenprojekt erstellen / migrieren** wählen,
Bibliothek und Gruppe auswählen und einen neuen Projektnamen angeben. Der
Ersteller benötigt normalen Gruppenzugang und die Bibliothek einen aktiven
Recovery-Zugang. Eine Migration erzeugt eine neue Projektkopie; das Original
und seine bisherigen Zugänge bleiben erhalten.

Gruppenprojekte öffnen mit der persönlichen Windows-Identität. Eine
Recovery-Passphrase je Gruppenprojekt entfällt. Die Passphrasen privater
Verwaltungs-/Recovery-Sicherungen werden weiterhin für deren Wiederherstellung gebraucht.
Bei bisherigen Team-Vaults gelten deren bisherige Recovery-Regeln weiter.

Nach Mitgliedschafts- oder Geräteänderungen verteilt die Hauptverwaltung den
neuen signierten Stand. Betroffene Arbeitsplätze importieren ihn. Bestehende
Gruppen-Vaults müssen anschließend im Projekteditor kontrolliert mit den neuen
Empfängern abgeglichen werden. Dafür ist neben dem aktuellen Gruppenstand ein
berechtigter Entschlüsselungszugang erforderlich; die Vorschau vor dem Bestätigen prüfen.

Offline-Zugriff verwendet den zuletzt lokal übernommenen Stand. Ein offline
gebliebener Rechner kennt spätere Entzüge nicht. Alte Vaultkopien bleiben mit
damals berechtigten Schlüsseln entschlüsselbar. Weder eine neue Gruppenliste
noch ein Empfängerabgleich kann solche Kopien aus der Ferne sperren.

## Angekündigter Wechsel der Hauptverwaltung (Entwicklungsstand)

Der vollständige Einrichtungsdialog beim Nachfolger ist noch in Arbeit. Eine
vorbereitete öffentliche Nachfolgeranfrage kann die bisherige Hauptverwaltung
bereits prüfen:

1. In der Gruppenverwaltung die betreffende Bibliothek auswählen und offene
   Änderungen zuerst signiert speichern.
2. **Nachfolgeranfrage prüfen und autorisieren** wählen und die öffentliche
   `.group-successor.json` öffnen. Keine private Sicherung entgegennehmen.
3. Den neuen Fingerabdruck separat mit dem Nachfolger abgleichen und eingeben.
   Die Anzeige aus der Datei allein genügt nicht als unabhängige Bestätigung.
4. Die Übergabe ausdrücklich bestätigen. Personen, Geräte, Gruppen, Recovery
   und vorhandene Vollmachten bleiben dabei unverändert.
5. Die öffentliche `.group-transition.json` unter einem neuen Dateinamen speichern
   und verteilen. Jeder Arbeitsplatz muss den angekündigten Wechsel ausdrücklich
   importieren; der Export aktiviert ihn auf keinem Arbeitsplatz automatisch.

Die Aktivierung des vorbereiteten privaten Schlüssels beim Nachfolger ist ein
zusätzlicher Schritt. Offline-Arbeitsplätze vertrauen bis zum Import weiterhin
dem alten Schlüssel. Vorhandene private Dateien nicht löschen oder überschreiben.

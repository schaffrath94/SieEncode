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

Beim Nachfolger in den Einstellungen **Nachfolge der Hauptverwaltung vorbereiten**
öffnen. Die bisherige Bibliothek muss bereits registriert und aktuell sein:

1. Bibliothek auswählen und einmalig **Neuen Nachfolgeschlüssel vorbereiten** wählen.
   Bei erneutem Öffnen wird die vorhandene Vorbereitung verwendet.
2. **Private Sicherung erstellen** wählen, eine getrennte Passphrase vergeben und
   die verschlüsselte Datei außerhalb des gemeinsamen Projektordners aufbewahren.
   Den Sicherungsdialog anschließend schließen.
3. **Öffentliche Anfrage exportieren** wählen. Nur diese `.group-successor.json`
   weitergeben und den angezeigten neuen Fingerabdruck separat bestätigen.

Nach einem Neustart verlangt der Dialog vor erneutem Anfrageexport eine neue
Sicherungsdatei. Bei geändertem registriertem Stand wird die Anfrage abgewiesen;
die bestehende Vorbereitung bleibt für eine private Sicherung verfügbar.
Keine Vorbereitung löschen, um diese Prüfung zu umgehen.

Die bisherige Hauptverwaltung prüft und autorisiert die öffentliche Anfrage:

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

Beim Nachfolger anschließend:

1. Den Vorbereitungsdialog schließen und in den Einstellungen
   **Angekündigten Verwaltungswechsel importieren** wählen. Die zurückerhaltene
   Übergabedatei und Bibliothek prüfen, den unabhängig bestätigten Fingerabdruck
   eingeben und den Import bestätigen.
2. **Nachfolge der Hauptverwaltung vorbereiten** erneut öffnen und dieselbe
   Bibliothek auswählen. Der Dialog erkennt den bereits registrierten neuen Schlüssel.
3. **Nach importierter Übergabe lokal aktivieren** wählen und bestätigen.
   Eine passende bisherige lokale Verwaltung wird zuerst geschützt archiviert;
   eine fremde Verwaltung wird nicht ersetzt. Bei einem abgebrochenen Versuch den
   Schritt erneut ausführen, vorhandene Dateien nicht manuell löschen.
4. Dialog schließen und die Gruppenverwaltung öffnen. Dort wird nun mit dem neuen
   Schlüssel gearbeitet. Vorbereitung und gegebenenfalls Vorgängerarchiv bleiben
   erhalten. Das lokale Archiv ersetzt keine portable private Sicherung.

Offline-Arbeitsplätze vertrauen bis zum Import weiterhin dem alten Schlüssel.
Das Vorgängerarchiv enthält weiterhin einen nutzbaren alten Schlüssel; weder
Archivierung noch Aktivierung sperren bereits vorhandene Kopien aus der Ferne.

Hat ein noch nicht umgestellter Arbeitsplatz inzwischen weitere Änderungen der
alten Verwaltung importiert, passt die ursprüngliche Übergabe möglicherweise
nicht mehr zu seinem Stand. SieEncode weist sie dann ab. Die lokale Registrierung
nicht löschen oder zurücksetzen, sondern die abweichenden Stände mit der
Hauptverwaltung klären. Es gibt dafür keine automatische Zusammenführung.

Beauftragte Gruppenverwalter behalten beim reinen Verwaltungswechsel ihre
bisherigen Vollmachten und ihren separaten Delegatenschlüssel. Nach dem eigenen
Import der Übergabe müssen sie offene Änderungsanträge auf dem neuen Stand erneut
erstellen. Anträge vom alten Stand werden von der neuen Hauptverwaltung abgewiesen.
Weiterhin sind nur die ausdrücklich freigegebenen Gruppen bearbeitbar; ein späterer
Vollmachtsentzug wird wirksam, sobald der neue Stand am Arbeitsplatz übernommen ist.
Die Hauptverwaltung lehnt eingereichte Änderungen bereits anhand ihres eigenen
aktuellen Stands ab.

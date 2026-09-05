# SieEncode RC6 – Pilotabnahme

Diese Abnahme prüft einen realen Arbeitsablauf ohne gemeinsame Active Directory. Produktive Schlüssel bleiben ausschließlich im verschlüsselten Projekt-Vault. Dauerhafte Projektdateien, Serienprotokolle und Diagnosepakete müssen schlüsselwertfrei bleiben; produktive rfPrj-Dateien dürfen nur im lokalen, kurzlebigen Sitzungsverzeichnis existieren.

## Voraussetzungen

- Freigegebener gemeinsamer Projektstamm ist in den Einstellungen gewählt.
- Beide Benutzer können denselben Projektordner lesen und schreiben.
- Beide Benutzer besitzen eine eigene lokale Windows-Anmeldung. Eine gemeinsame AD oder Sicherheitsgruppe ist nicht erforderlich.
- Die Recovery-Passphrase liegt ausschließlich im administrativen KeePass-Eintrag und wird nicht als tägliches Kennwort verteilt.
- RFiDGear 2.0.10 mit dem von SieEncode freigegebenen Runtime-Paket ist installiert oder gebündelt.
- ELATEC-Leser ist ausgewählt; für Hardwaretests liegt ein ausdrücklich freigegebener Testtransponder auf.

## A – Projekt und Vault durch Benutzer 1

1. Eine Kodiervorschrift aus dem Grundprofil erstellen oder eine vorhandene Testvorschrift öffnen.
2. Firmencode und alle weiteren fachlichen Felder prüfen. Kundennamen oder Schlüssel dürfen nicht fest im Grundprofil stehen.
3. Produktive Schlüssel in der Schlüsselverwaltung erzeugen oder bewusst eingeben.
4. Unter `Mehrbenutzerzugriff einrichten` eine persönliche Windows-Identität anlegen und zur Empfängerliste hinzufügen.
5. Eine zufällige Recovery-Passphrase mit mindestens 20 Zeichen im administrativen KeePass-Eintrag ablegen und den Vault sicher freigeben.
6. Die öffentliche Empfängereinladung exportieren. Den vollständigen SHA-256-Fingerabdruck getrennt von der Datei übermitteln.
7. Recovery-Zugang technisch prüfen, Projekt speichern und den sicheren Build ausführen.
8. Prüfen, dass Projektdefinition und gemeinsamer Projektordner keine produktiven Schlüssel und keine neuen produktiven `.rfPrj` enthalten.

Erwartung: Vault-Prüfung `OK`, vollständige App-/Key-Anzahl und sicherer temporärer Build `OK`.

## B – Öffnen durch Benutzer 2

1. SieEncode unter einem zweiten Windows-Benutzer oder auf einem zweiten freigegebenen Rechner starten.
2. Denselben übergeordneten Projektstamm einstellen.
3. Unter `Mehrbenutzerzugriff einrichten` eine eigene Windows-Identität anlegen und die öffentliche Einladung exportieren.
4. Benutzer 1 importiert die Einladung erst nach Vergleich des vollständigen SHA-256-Fingerabdrucks über einen zweiten Kanal und speichert die neue Empfängerliste.
5. Benutzer 2 öffnet dieselbe Kodiervorschrift; keine Datei kopieren und kein gemeinsames Tageskennwort eingeben.
6. Die Kodierstation laden und den automatisch mit der persönlichen Windows-Identität entsperrten, lokalen Sitzungsbuild prüfen.
7. SieEncode schließen und kontrollieren, dass das lokale Sitzungsverzeichnis entfernt wurde.

Erwartung: automatischer Zugriff nur für beide freigegebenen Identitäten; ein nicht freigegebener dritter Benutzer wird abgewiesen; keine Projektkopie und keine produktiven rfPrj im gemeinsamen Stamm.

## C – Sichtbarer Kodierlauf

1. Kodiervorschrift in die Kodierstation laden.
2. Arbeitsplatzprüfung ausführen.
3. Gewünschten Betriebsmodus wählen: Einzel, Fortlaufend oder Liste.
4. Kodierdaten eingeben beziehungsweise CSV/TXT laden.
5. Sitzung vorbereiten und RFiDGear ausschließlich über die sichtbaren Schaltflächen starten.
6. Den Lauf in RFiDGear beobachten und anschließend in SieEncode `Laufstatus aus Log aktualisieren` beziehungsweise die Readback-Prüfung ausführen.
7. Bei Fehlern dieselbe Nummer wiederholen oder bewusst überspringen. Eine fehlgeschlagene Nummer darf nicht als erfolgreich gelten.
8. Serienzusammenfassung speichern und Sitzung schließen.

Erwartung: Jeder Task besitzt einen nachvollziehbaren Zustand. Nur die Bedienentscheidung `KODIERUNG ERFOLGREICH BESTÄTIGT` gibt die Karte frei; sie erfordert korrelierten `TaskLoop.Success`, den vollständigen Taskplan und frische bytegleiche Readbacks. Prozessende, Nullwerte oder passende Readbacks ohne autoritativen Laufnachweis reichen nicht.

Der Fünf-Karten-Nachweis gilt ausschließlich für die dabei protokollierte Produktversion sowie die exakten Studio- und Runtime-SHA-256. Eine ältere Serie darf einen neueren Build nicht freigeben. Das Serienprotokoll muss außerdem eine schlüsselfreie Keyset-ID enthalten, damit alle fünf Karten eindeutig demselben Vault-Schlüsselsatz zugeordnet sind.

## D – Datenschutz- und Diagnosekontrolle

1. Serien-JSONL, Zusammenfassung, Projektdatei und Diagnosepaket stichprobenartig prüfen.
2. Nach produktiven PICC-/App-Schlüsseln suchen.
3. Prüfen, dass Diagnose und Ergebnis trotzdem Projekt, Karte, Task, Zeitpunkt, Fehlerkategorie und RFiDGear-Logbezug enthalten.
4. Gemeinsamen Projektstamm nach `.rfPrj` und bekannten Testschlüsselwerten durchsuchen.

Erwartung: keine Schlüsselwerte; ausreichend Metadaten zur technischen Nachvollziehbarkeit.

## Abnahmegrenzen

- Ein einzelner wiederverwendeter Transponder beweist den technischen Ablauf, aber keinen echten Mehrkarten-Durchsatz und keine Verwechslungssicherheit zwischen physischen Karten.
- Ein produktiver Pilot benötigt deshalb mindestens fünf unterscheidbare Testtransponder oder freigegebene Karten und zwei reale Benutzerkonten.
- Das Entfernen eines Empfängers verhindert künftiges Entschlüsseln nach Neuverschlüsselung. Bereits zuvor gelesene oder kopierte Schlüssel können technisch nicht zurückgerufen werden; bei Verdacht müssen Produktivschlüssel und betroffene Karten rotiert werden.
- Ein signiertes Release setzt ein freigegebenes Code-Signing-Zertifikat voraus. Ohne Zertifikat ist das Paket ausdrücklich ein unsignierter Entwicklungsstand.
- Vor einer breiteren Pilotverteilung müssen mindestens zwei fachfremde Testpersonen den normalen Ablauf ohne Zuruf bedienen; Missverständnisse und Fehlklicks werden als Usability-Befunde dokumentiert.
- Layout und Erreichbarkeit aller Aktionen werden bei 100 %, 125 % und 150 % Windows-Skalierung sowie 1366 × 768 geprüft.

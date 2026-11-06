# Kurze manuelle Abnahme: Station mit Passphrasen-Vault

Erst mit einem neuen Installer ausfuehren, dessen Build den Stationsfix enthaelt.
Kandidat 1.3.0-offline-groups.3 enthaelt diesen Fix noch nicht. Exakte Version
und Installer-Pruefsumme im Ergebnis notieren. Keine Passphrase oder Schluessel
in die Rueckmeldung aufnehmen.

1. Vorhandenes Projekt mit Passphrasen-Vault auswaehlen, Kodierstation oeffnen.
   Erwartung: Dialog **Vault fuer Kodierstation entsperren**. Abbrechen:
   keine Kartenaktion und keine neue freigegebene Sitzung.
2. Wieder oeffnen, absichtlich falsche Passphrase eingeben. Erwartung:
   Entsperren scheitert; keine Kartenaktion. Danach nochmals oeffnen und die
   richtige Passphrase eingeben. Keine neue Identitaet oder Migration notwendig.
3. Erwartung: Station geladen, Hinweis **geschuetzte lokale Sitzung**.
   **Projekt pruefen**: keine unberechtigten Meldungen zu fehlenden
   Transport-Schluesselwerten. Echte Projektbefunde weiterhin beachten.
4. Nur mit bekannter geeigneter Testkarte und angeschlossenem Reader:
   **Transponder erkennen** einmal starten. Keine Vorbereitung/Kodierung starten.
   Erwartung: kein Fehler wegen unaufloesbarer secret://-Referenz. Ein sonstiger
   Authentisierungs-/Readerfehler ist getrennt zu melden und keine Freigabe.

Rueckmeldung: Version, Schritt 1/2/3/4 jeweils bestanden oder genauer Meldungstext.
Die Anleitung ist keine bereits erfolgte Hardwareabnahme. Der Agent uebernimmt
die bestaetigten Ergebnisse danach in die Release-Dokumentation.

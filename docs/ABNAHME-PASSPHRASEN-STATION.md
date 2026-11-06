# Kurze manuelle Abnahme: Station mit Passphrasen-Vault

Mit Kandidat **1.3.0-offline-groups.4** ausfuehren (MSI, kein portables Paket).
SHA256: `0F1AABF98BF91D72A4B2475757C97017563766414EFD2E60D64ACD48F8E7FD73`.
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

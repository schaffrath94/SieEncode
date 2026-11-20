# SieEncode 1.2.0 – interner Release

Installierbarer Windows-x64-Release fuer die gemeinsame Erprobung mit Kollegen.
Auf Benutzerentscheidung vom 22.09.2026 erfolgt die Auslieferung als regulaerer
MSI-Installer. Die praktische Abnahme erfolgt anschliessend gemeinsam; derzeit
arbeitet laut Benutzer niemand produktiv mit SieEncode.

## Aenderungen

- Neue Arbeitsbereiche fuer Projekte, Kodieren und Untersuchen mit Standard-
  und Expertenansicht.
- Vault-Einrichtung, Speichern, Recovery und Empfaengerverwaltung korrigiert.
- Verstaendliche Hinweise bei fehlendem oder gesperrtem Vault; ungueltige
  Schluessel verhindern weiterhin den Neubau.
- Profilauswahl und Eingaben bleiben bei Bibliotheksaktualisierungen erhalten.
- Korrigierte Speicherstatusmeldung und deutsche JSON-Importfehler.
- Bestehende PDF-Funktionen erhalten; PDF-Ueberarbeitung zurueckgestellt.

## Installation und erster Test

1. Bestehende Projekte und Vaults vor einem Upgrade separat sichern.
2. SieEncode schliessen, MSI ausfuehren und SieEncode im Startmenue starten.
   Die Installation fuer alle Benutzer benoetigt Administratorrechte.
3. Versionsanzeige 1.2.0 pruefen. Mit synthetischem Testprojekt beginnen:
   Projekt anlegen, speichern, schliessen und wieder oeffnen.
4. Vault einrichten, speichern und erneut entsperren; Recovery und Freigabe
   anschliessend auf einem zweiten Benutzer/Rechner erproben.
5. Kartenablaeufe ausschliesslich mit geeigneten, ausdruecklich freigegebenen
   Testtranspondern pruefen. Das beigefuegte Abnahme-Kit enthaelt Testboegen.

## Bekannte Grenzen

Installation/Upgrade/Deinstallation auf einem separaten Windows-System,
vollstaendige praktische UI-Funktionsabnahme, echter Mehrbenutzer-Vault und
Hardwareabnahme einschliesslich ChangeFileSettings sind noch offen. Automatische
Tests ersetzen diese Nachweise nicht. Der Release ist intern und unsigniert.
Entfernte Vault-Empfaenger koennen alte, bereits vorhandene Kopien weiterhin
oeffnen; eine Empfaengeraenderung sperrt diese Kopien nicht rueckwirkend.

Rueckmeldungen bitte mit Version, Arbeitsschritt, erwartetem/tatsaechlichem
Ergebnis und Fehlermeldung erfassen. Keine Schluessel oder produktiven Vaults
in Rueckmeldungen oder GitHub hochladen.

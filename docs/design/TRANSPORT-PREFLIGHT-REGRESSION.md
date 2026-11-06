# Transport-Befunde bei schluesselfreier Projektdefinition

06.11.2026: In der gestarteten Hostinstallation 1.2.1 zeigt die Projektbibliothek
nach der Projektpruefung fuenf Transport-Befunde (PICC, DES, AES, 3K3DES und
profilbezogener Application-Key). Keine Kartenaktion beobachtet. Keine privaten
Schluessel gelesen, Kundendateien kopiert oder Hostdaten veraendert.

## Ursache und Korrektur

`RunSelectedPreflight` liest die gespeicherte Definition ohne Vaultentsperrung.
`WithoutAllSecrets` entfernt absichtlich auch Transportwerte. Der operative
Preflight verlangte trotzdem deren Vollstaendigkeit. Der bisherige Integrationstest
entfernte nur Produktivwerte und erfasste diesen Fall daher nicht.

Nur der operative Preflight darf nun fehlende Transportwerte akzeptieren.
Vorhandene ungueltige Werte und unbekannte Keytypen bleiben Fehler. Build- und
Editorvalidierung verlangen unveraendert vollstaendige Transportwerte.
Der Betriebscheck bestaetigt weder Vaultentschluesselung noch Kartenauthentisierung.

## Kurze manuelle Abnahme nach Installation des korrigierten Kandidaten

1. Versionsnummer notieren. Vorhandenes Projekt mit Vault in der Bibliothek
   auswaehlen und **Projekt pruefen** waehlen, ohne vorher den Vault zu entsperren.
2. Es duerfen keine Transportfehler allein wegen ausgelagerter Schluessel
   erscheinen. Andere konkrete Artefakt-/Profilfehler bleiben relevant.
3. **Bearbeiten → Vault entsperren** verwenden. Vorhandene Identitaet bzw.
   vorgesehenen Vaultzugang nutzen; keine neuen Schluessel auf Verdacht erzeugen.
4. Bei vollstaendigem Projekt pruefen, ob der normale Build wieder angeboten
   wird. Einen Neubau nur an einer ausdruecklich dafuer vorgesehenen Testkopie
   ausfuehren. Keine Karte fuer diese Softwarepruefung erforderlich.
5. Rueckmeldung: Version, Schritt, Ergebnis und genauer Meldungstext; keine
   Schluesselwerte, Passphrasen oder privaten Sicherungen senden.

Manuelle Ergebnisse werden nach Rueckmeldung mit Datum und Version erfasst,
als Benutzerabnahme gekennzeichnet und nicht als automatisch beobachtet ausgegeben.
Diese Anleitung ist vorbereitet; die manuelle Abnahme ist noch offen.

Automatischer Nachweis: Test-CI.ps1 Exit 0, 72/72 Tests; CLI und App ohne
Warnungen/Fehler. Log: tmp/transport-preflight-ci.log. Noch nicht paketiert.

Korrigiertes MSI: artifacts/offline-groups-1.3.0-preview.3/package/SieEncode_1.3.0-offline-groups.3_internal_x64.msi.
Kandidat 3: UI-Smoke 60/60, 1081 MSI-Dateien hashgleich. Echte Installation und
manuelle Benutzerabnahme noch offen. Installer kann die vorhandene Version ersetzen.

06.11.: Kandidat 3 in Test-VM erfolgreich von Kandidat 2 aktualisiert;
Daten-/Schluesselerhalt in drei Profilen, installierter Self-Test und UI-Smoke
(60 Checks) bestanden. Benutzerprojekt in Hostinstallation noch nicht nachgeprueft.

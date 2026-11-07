# SieEncode – Kurzstart für Kollegen

Stand: 07.11.2026. Gilt für den internen Kandidaten **1.3.0-offline-groups.4**.
Die abschließende Releasefreigabe steht noch aus. Installation nur im vereinbarten
Testumfang. Die [ausführliche Anleitung](KOLLEGEN-ERSTEINRICHTUNG.html) enthält
Bilder, Verwaltungsabläufe und Wiederherstellung.

## Einmal am eigenen Arbeitsplatz

1. Den bereitgestellten **MSI-Installer** installieren und SieEncode starten.
   Bei einem Update die vorhandene eigene Identität weiterverwenden.
2. **Einstellungen:** Projektbibliothek und gemeinsame Profilbibliothek auf die
   vom Verantwortlichen genannten Ordner einstellen; Einstellungen speichern.
3. **Vault-Ersteinrichtung starten:** vorhandene eigene Identität auswählen oder
   einmal eine neue erstellen. **Öffentliche Einladung exportieren** und an die
   Hauptverwaltung geben. Keine privaten Schlüssel oder Sicherungen weitergeben.
4. Die Hauptverwaltung nimmt dein Gerät auf und ordnet deine Person den Gruppen
   zu. Sie gibt dir die öffentliche `.group-enrollment.json` und bestätigt über
   einen unabhängigen Kontakt Bibliotheks-ID und Verwaltungsfingerabdruck.
5. **Gruppenverwaltung registrieren:** öffentliche Datei öffnen, ID und
   Fingerabdruck abgleichen, Fingerabdruck eingeben und bestätigen.
6. Ein freigegebenes Gruppenprojekt öffnen und **Vault entsperren**. Dafür braucht
   ein normaler Kollege keine Recovery-Passphrase. Eine Gruppenmitgliedschaft und
   Schreibrechte im Projektordner erlauben auch das Anlegen eigener Gruppenprojekte.

## Leser und erster Kartentest

Leser anschließen. **Reader-Einstellungen in RFiDGear öffnen**, Anbieter und ggf.
Port einstellen, speichern und RFiDGear schließen. **Leserkonfiguration neu prüfen**.
Das bestätigt nur die Konfiguration. Eine Kartenprüfung zunächst mit dem
Verantwortlichen und einer eindeutig geeigneten Testkarte durchführen.
Ohne abgestimmte Testkarte keine Vorbereitung, Formatierung oder Kodierung starten.

## Im Alltag

- Eine neue Person oder ein weiterer Rechner braucht eine eigene Geräteeinladung
  und Freigabe. Ein gemeinsamer Ordner überträgt keine private Identität.
- Bei geänderten Rechten den neuen öffentlichen Stand über **Registrierte Gruppen
  aktualisieren** importieren. Bestehende Vaults muss ein Berechtigter zusätzlich
  über **Gruppenempfänger abgleichen** aktualisieren.
- Offline benötigt dein Rechner Projekt, Vault, Profile, registrierten Gruppenstand
  und eigene Identität lokal. Eine nur auf einem unerreichbaren Server liegende
  Datei wird durch Offline-Entschlüsselung nicht verfügbar.
- Entfernte Mitglieder können alte Kopien mit früherem Zugang weiterhin öffnen.
  Es gibt ohne Onlinedienst keine sofortige globale Sperrung alter Kopien.
- **Recovery** ist ein gesonderter Wiederherstellungsweg für beauftragte Personen.
  Dabei bleibt normales Bauen gesperrt. Anschließend wieder normal entsperren.

## Wenn etwas fehlt

| Meldung oder Beobachtung | Vorgehen |
| --- | --- |
| Keine Projekte sichtbar | Richtigen Windows-Benutzer und Projektordner prüfen. Nicht auf Verdacht neue Identitäten erstellen. |
| Gruppe registriert, aber kein Zugriff | Verwaltung prüft Person, genau dieses Gerät und Projektgruppe; aktuellen Gruppenstand importieren. |
| Älteres Projekt fragt nach Passphrase | Bisherige Projektpassphrase lokal eingeben. Gruppenregistrierung migriert alte Passphrasen-Vaults nicht automatisch. |
| Projektprüfung meldet fehlende Builddateien | Genauen Befund prüfen. Ein gespeicherter Entwurf ist noch kein gebautes Projekt; fehlende Artefakte sind kein Nachweis falscher Kartenschlüssel. |
| Verwaltungsfingerabdruck stimmt nicht | Abbrechen und mit der Hauptverwaltung unabhängig klären. |

Für Hilfe: Version, Schaltfläche und genauen Meldungstext mitteilen. Keine
Schlüssel, Passphrasen oder privaten Sicherungen senden. Projektprüfungsbefunde
werden derzeit nicht als eigener Verlauf gespeichert; den sichtbaren Text notieren.

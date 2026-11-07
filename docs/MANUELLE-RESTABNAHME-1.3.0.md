# Kurze manuelle Restabnahme – Kandidat 1.3.0-offline-groups.4

Stand 07.11.2026. Die unten aufgeführten manuellen Bediennachweise bleiben offen.
Installation/Upgrade in der VM, Erhalt der Dateien und RSA-Schlüssel in drei
Windows-Profilen sowie installierter Self-Test und 61 Anwendungsprüfungen sind bereits
belegt. Diese Prüfungen müssen für dieselbe unveränderte Umgebung nicht erneut
ausgeführt werden. Der Installer ersetzt eine vorhandene SieEncode-Version.

Zusätzlich sind 22 Produkt-API-Phasen für Gruppen/Recovery/Projektkopie/Delegation
und Verwaltungsnachfolge unter zwei Windows-Benutzern bestanden. Diese fachlichen
Skripttests nicht wiederholen; B–D prüfen hier nur noch die tatsächliche Bedienung
einschließlich Dialogen und Rollenwechsel. Für den Stationsfehler zuerst die
[gezielte Passphrasen-Abnahme](ABNAHME-PASSPHRASEN-STATION.md) verwenden.

MSI SHA256: `0F1AABF98BF91D72A4B2475757C97017563766414EFD2E60D64ACD48F8E7FD73`.
Keine Schlüsselwerte oder privaten Sicherungen in Rückmeldungen aufnehmen.

## A – Gemeldeten Fehler prüfen (ohne Karte)

1. Version **1.3.0-offline-groups.4** im Fenster kontrollieren.
2. Dasselbe Projekt in **Projekte → Bibliothek** auswählen, **Projekt prüfen**.
3. Erwartung: Keine Transport-Key-Längenfehler allein wegen der im Vault
   gespeicherten Werte. Andere Meldungen wörtlich notieren, nicht übergehen.

Antwort genügt: `A bestanden, Version …` oder `A Fehler: …`.
Dieser Schritt bestätigt weder Kartenschlüssel noch vollständige Buildfähigkeit.

## B – Bestehenden Test-Vault und Projektkopie prüfen

Nur ein synthetisches Gruppenprojekt verwenden. Normale Mitgliedschaft und
separate Recovery-Identität müssen bereits eingerichtet sein.

1. **Bearbeiten → Vault entsperren**: normaler Zugang muss bestätigt werden.
2. **Mit Recovery-Schlüssel öffnen → Recovery-Zugang prüfen**: Zugang,
   Projektbindung und Integrität müssen bestätigt, Build muss gesperrt bleiben.
3. Wieder **Vault entsperren**: normaler Zugang muss wiederhergestellt werden.
4. Unter **Weitere Editoraktionen · Experten → Speichern unter** einen neuen
   Testnamen verwenden. Editor, Kopfzeile und ausgewähltes Bibliotheksprojekt
   müssen jetzt dieselbe neue Kopie nennen. Die Quelle muss erhalten bleiben.
5. Anwendung schließen, neu starten, Kopie erneut öffnen und normal entsperren.

Antwort: `B1 …, B2 …, B3 …, B4 …, B5 …`; nicht ausgeführte Punkte offenlassen.
Für den unabhängigen Nachweis neuer Projekt-ID und unveränderter Quellhashes
die verwendeten synthetischen Testnamen nennen. Diesen Nachweis ergänzt Codex.

## C – Zentralen Rechtewechsel prüfen (zwei Testbenutzer)

Vorbereitung nach [Gruppen-Abnahme](OFFLINE-GRUPPEN-ABNAHME.md): getrennte
Windows-Benutzer A/B, A Hauptverwaltung und Mitglied beider Gruppen, B Mitglied
von Lokal, nicht von Lokal 2; separate Recovery aktiv. Nur Testprojekte ändern.

1. B öffnet ein Lokal-Projekt, Lokal 2 wird abgewiesen. Eine alte Testkopie von
   Lokal getrennt aufbewahren, bevor die Verwaltung geändert wird.
2. A: **Einstellungen → Offline-Gruppen verwalten**, Lokal auswählen, B über
   **Mitglied aus Gruppe entfernen** entfernen, **Gruppen signiert speichern**.
   **Gespeicherten Stand öffentlich exportieren**.
3. A öffnet den aktuellen Lokal-Vault und wählt **Gruppenempfänger abgleichen**.
   Vorschau muss den entfernten Gerätezugang zeigen. Erst nach Prüfung bestätigen.
   Verschlüsselte Altsicherung und Erfolgsmeldung müssen vorhanden sein.
4. B übernimmt den neuen Stand über **Registrierte Gruppen aktualisieren**.
   Der aktuelle Lokal-Vault muss abgewiesen werden. Die alte lokale Kopie kann
   vor diesem Import unter altem Stand weiterhin zugänglich sein: erwartete
   Offline-Grenze, keine sofortige globale Sperrung.
5. A nimmt B wieder auf, speichert/exportiert und gleicht den Vault erneut ab.
   B importiert den neuen Stand; aktuelles Lokal-Projekt wieder entsperrbar.

Antwort: `C1 … bis C5 …`, mit Bibliotheksrevisionen und genauer Fehlermeldung.
Ein Zugriffsversuch auf eine alte Kopie ersetzt nicht die Prüfung des aktuellen Vaults.

## D – Begrenzte Verwaltungsvollmacht prüfen

Dieselbe synthetische Bibliothek verwenden; B ist wieder Mitglied von Lokal.

1. B: **Einstellungen → Mein Gruppenverwaltungsplatz**. Bibliothek auswählen,
   **Bibliothek laden**, eigene Person auswählen, **Person und Arbeitsplatz laden**,
   **Öffentliche Verwaltungseinladung exportieren**. Nur die öffentliche Datei an A.
2. A: **Offline-Gruppen verwalten**, nur Lokal als Vollmachtsumfang auswählen,
   **Verwaltungseinladung für ausgewählte Gruppen freigeben**. Fingerabdruck
   unabhängig abgleichen. Gespeicherten Stand öffentlich exportieren, B importiert.
3. B lädt seinen Verwaltungsplatz erneut. Nur Lokal darf bearbeitbar sein;
   Lokal 2, Personen-/Geräteverwaltung und Recovery dürfen nicht freigegeben sein.
   Lokal testweise umbenennen, **Änderung signieren und exportieren**.
4. A: **Signierte Gruppenänderung prüfen und übernehmen**. Inhalt prüfen und
   bestätigen. Erst dieser Schritt veröffentlicht die Änderung; B allein nicht.
   Denselben Antrag erneut einreichen: muss abgewiesen werden.
5. A: **Ausgewählte Vollmacht entziehen**, aktuellen Stand exportieren. B
   importiert und lädt seinen Verwaltungsplatz neu: kein neuer gültiger Antrag
   mit dieser Vollmacht möglich. Persönliche Identität muss erhalten bleiben.

Antwort: `D1 … bis D5 …`, mit Revisionen und tatsächlichem Ergebnis.

## Dokumentation und Abschluss

Codex trägt gemeldete Ergebnisse mit Version, Datum, Testumgebung und Herkunft
„Benutzerrückmeldung“ ein. Eigene Toolbeobachtungen und automatische Prüfungen
bleiben getrennt. Eine fehlende Rückmeldung bedeutet niemals „bestanden“.

Separat offen: visuelle Schlusskontrolle der Kollegenanleitung, tatsächliche
Reader-/Testkartenprüfung und gegebenenfalls eine zweite Rechnerumgebung.
Die Schritte A–D schreiben keine Karte. Keine erneute Paketproduktion allein
für Protokolländerungen; Codefehler erfordern einen neuen identifizierten Kandidaten.

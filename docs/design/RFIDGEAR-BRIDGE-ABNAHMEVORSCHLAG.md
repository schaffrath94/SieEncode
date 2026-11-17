# Vorschlag für einen gemeinsamen Bridge-Abnahmetest

Ergänzung zu c3rebro/RFiDGear#232, Stand 17.11.2026. Der letzte Kommentar beschreibt bereits Projektbindung, Kartenpräsenz, Vault-Freigabe und strukturierte Prüfergebnisse. Dieser Vorschlag definiert beobachtbare Ergebnisse für einen Test mit simuliertem Reader und erfundenen Referenzen. Er ist noch nicht gegen eine offizielle Engine ausgeführt.

## Minimale Testanordnung

Zwei synthetische Projektzustände A und B teilen Pfad und Taskanzahl. Sie unterscheiden sich in einer öffentlichen Dateigröße. Zwei simulierte Kartenpräsenz-Ereignisse und ein zählender Testprovider liefern deterministische Eingaben. Der Reader zählt ausgeführte Schreibaufträge. Der Provider zählt freigegebene Schlüsselauflösungen, ohne deren Werte aufzuzeichnen.

| Testfall | Erwartetes Ergebnis |
| --- | --- |
| A freigeben, anschließend im Host auf B ändern, dann Auftrag A starten | Ablehnung vor Kartenausführung. Schreibzähler 0. Keine Schlüsselfreigabe. |
| Karte A scannen und freigeben, vor Start durch Karte B ersetzen | Ablehnung. Schreibzähler 0. Keine Wiederholung ohne neue Freigabe. |
| Eine gültige Referenz aus einem anderen Auftrag anfragen | Keine Schlüsselfreigabe. Auftrag und Ablehnung sind ohne Schlüsselwert zuordenbar. |
| Einen Schreibauftrag abschließen, Karte entfernen, erneut auflegen und ausschließlich Lesetest starten | Schreibzähler bleibt beim Wert nach dem ersten Auftrag. Lesetest erhält eine eigene Laufkennung. |
| Verbindung während der Auflösung verlieren und Client neu verbinden | Kein selbsttätiger Neustart des Schreibauftrags. Client verlangt bei uneindeutigem Ergebnis Prüfung. |
| Lesetest erwartet 32 Byte und beobachtet 16 Byte | Abweichung wird dem richtigen Lauf und Task zugeordnet. Der Client kann sie ohne Logtext erkennen. |

Zum Austausch genügen Host-Commit, Bridge-Version, Testfall, beobachteter Terminalstatus, Taskposition sowie beide Zähler vor und nach dem Versuch. Feldnamen und Transport können sich weiterentwickeln. Interessant für beide Projekte ist, welche Fälle der SampleClient am aktuellen master bereits nachweisen kann. Ein gemeinsamer erster Test wäre der Projektwechsel bei identischem Pfad und identischer Taskanzahl.

## Ausgeführter Kontrolltest am 17.11.2026

Offizieller Quellstand e607c2986cd000c88a6a3101620a963882f738e6, Windows x64, lokales SDK 10.0.404, Release. Der einzelne Test BridgeSampleClientTests.ARunThatExpectsAnotherProject_IsRefused_AndTheHostIsNeverAsked bestand (1/1). Der SampleClient kommuniziert über eine Named Pipe mit dem Testhost. Ein abweichender Projektpfad und eine abweichende Taskanzahl wurden abgelehnt. Der passende Auftrag wurde abgeschlossen. Bei einem Checkout unterhalb des Studio-Verzeichnisses die Studio-spezifische Directory.Build.props vom Upstream-Build ausschließen.

Der getestete StartRunRequest enthält die Projektvorbedingungen ExpectProjectPath und ExpectTaskCount. Ein erwarteter Revisionswert für den geladenen Projektinhalt fehlt im DTO. Die Ablehnung eines Inhaltswechsels bei unverändertem Pfad und gleicher Taskanzahl bleibt daher ein offenes Abnahmekriterium. Der Test nutzte weder den installierten Upstream-Release noch einen Reader.

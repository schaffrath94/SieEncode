# Technische Konsolidierung der Schlüsselbibliothek am 26. November 2026

Geprüft wurde der Studio-Entwicklungszweig `codex/offline-vault-groups` ab `4a8ffabfaf47a4f03bb86bbce899d41976e965bd`. Die Prüfung verwendet synthetische Identitäten, Kunden, Schlüssel und Projekte. Maßgeblich waren der Releasefahrplan für 1.0 sowie die Konzepte für Bibliotheksintegration, HTTPS-Abgleich, Sicherung und Offline-Entzug.

## Befunde und Änderungen

- **Build** (`dd579be`). Der unveränderte Stand brach beim fehlenden Zertifikattyp ab. Nach Korrektur des Imports traten weitere Compilerfehler in der Bibliotheksvalidierung, Synchronisation und WPF-Testunterstützung hervor. Ungültige Typen beziehungsweise fehlende Slots werden ausdrücklich abgewiesen. Der Uploadpfad mit vorhandener Verwaltung verlangt vollständigen Katalogzugriff.
- **Zentraler Abgleich** (`8641cc9`). Beim ersten Upload konnte die allgemeine TLS-Mitgliedschaft aus einer anderen Gruppe stammen. Der Server prüft jetzt den Absender gegen die signierte Zielgruppe im selben Zustand, dessen Revision beim Schreiben verglichen wird. Der ältere Download-Endpunkt prüft die Übereinstimmung von TLS-Gerät und signierter Abrufberechtigung wie der Snapshot-Endpunkt.
- **Geprüfte und gespeicherte Bytes** (`8641cc9`). Bei Annahme und lokaler Installation übernimmt die Bibliothek die begrenzten Übertragungspuffer vor der Prüfung. Auch die Wiederinbetriebnahme übernimmt den Aktivierungsauftrag vor Prüfung und Ablage. Damit können Änderungen an Aufruferpuffern die bereits geprüften Inhalte dieser Vorgänge nicht nachträglich ersetzen.
- **Studio-Bedienprüfung** (`42843d3`). Der Test erwartete zwei überholte Beschriftungen. Er prüft die tatsächlich ausgelieferte ausdrückliche Startaktion, den F8-Knopf und den Hinweis auf automatische Auswertung.

## Prüfumfang und Aussage

| Bereich | Software-Nachweis |
|---|---|
| Synchronisation und Revisionen | Signaturen, erwartete Revision und Chiffre-Hash, konkurrierende Änderungen, Wiedereinspielen, geschützter Cursor und lokale Abgleichbasis. Ergänzt wurden Ablehnung eines Recovery-Geräts und eines Mitglieds einer anderen Gruppe beim ersten Upload sowie Annahme durch das richtige Mitglied. |
| Atomare Speicherung und Unterbrechung | Die Suite prüft die Aktivierung nach Beenden eines Kindprozesses und den Wiederanlauf. Richtlinienwechsel und Neuverschlüsselung werden gegen einen gemeinsamen Ausgangsstand geprüft. Ein zusätzlicher Test verändert den ursprünglichen Downloadpuffer während der lokalen Verarbeitung und prüft die gespeicherte Datei. |
| Kundentrennung und Projektintegration | Kundenbezogene Empfängerverschlüsselung, getrennte Teil- und Vollsicht, exakte Schlüsselversionen und Slots, geschlossene Fehlerpfade bei fehlenden Referenzen. Der Sitzungsabgleich bindet Gerät, Kunden, Richtlinienstand und lokale Vault-Datei. |
| Freigabe und Audit | Separate Anzeige-/Kopierrechte, signierte Meldungen, Abfrage mit berechtigtem Gerät, Ablaufzeit, Seitengrenzen und Validierung der zurückgelesenen Einträge. |
| Backup, Recovery und Serveraktivierung | Isolierte Wiederherstellung, unabhängiger Abgleich mit Referenz und Manifest, Bindung an Zielpfad, TLS-Zertifikat und Zeitfenster, Ablehnung eines belegten Ziels und Startprüfung. |
| Zusammenführung | Die Tests prüfen konfligierende Quellen, fehlende Basishistorie, Freigabe der konkreten Vorschau und Erstellung eines verschlüsselten, signierten Kandidaten gegen einen bestimmten zentralen Ausgangsstand. |

## Verbleibende Grenzen

Eine früher berechtigte Person kann bereits kopierte Klarwerte oder eine früher entschlüsselbare Offline-Kopie behalten. Ein Entzug für zukünftige Stände und eine Rotation betroffener Schlüssel lösen unterschiedliche Probleme. Beim Aktivieren eines Ersatzservers muss der Betreiber den bisherigen Dienst separat stilllegen. Eine Signatur und eine lokale Dateisperre beweisen das Abschalten eines anderen Rechners nicht.

Vor der internen Freigabe empfehlen sich ein Ende-zu-Ende-Lauf mit zwei Windows-Profilen gegen einen laufenden HTTPS-Dienst sowie eine Wiederherstellungsübung mit getrennten Dienstverzeichnissen und tatsächlich gestopptem Altserver. Der Softwarelauf umfasst keine physische Stromunterbrechung, Kartenabnahme oder MSI-Verteilung. Die fachliche Sicherheit der gesamten Lösung benötigt weiterhin unabhängiges Review.

## Tatsächliches Ergebnis

- Ausgangsstand: Test-Build fehlgeschlagen (CS0246). Nach den Korrekturen bestanden alle **84 von 84** Einträgen des ausführbaren Regressionstests. Die abschließend korrigierte Testidentität benutzt die vorgeschriebene kleingeschriebene Personen-ID.
- Release-Builds von Test-Harness, CLI, Studio und SyncServer waren mit .NET SDK 8.0.428 unter Windows erfolgreich, jeweils **0 Warnungen und 0 Fehler**. Beim zwischenzeitigen parallelen Start des WPF-Tests sperrte dessen Prozess DLLs im Buildverzeichnis. Studio und Server wurden anschließend separat erfolgreich gebaut.
- Der installierungsfreie WPF-Lauf mit isolierten Testdaten endete mit Exitcode **0**, Status **OK**, **70** protokollierten Prüfpositionen und **5 von 5** synthetischen Modellprojekten. Der Empfängertest prüft jetzt, dass eine gespeicherte Teamliste dem neuen Projekt von sich aus keinen Empfänger gibt, und klickt anschließend ausdrücklich auf „Zur Empfängerliste hinzufügen“. Der frühere Test wartete ohne diese Auswahl im Dialog.
- Reproduktion im Checkout: zuerst `./Test-CI.ps1` abschließen, danach das gebaute Studio im Modus `--ui-smoke-test` unter isolierten Testpfaden ausführen. Beide Schritte verwenden Software-Testdaten. Ein Live-mTLS-Lauf gegen einen separaten Server und die Legacy-Integrationssuite mit externen Fixtures waren kein Bestandteil dieses Ergebnisses.

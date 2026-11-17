# Kandidat 5: kurzer Starttest

Stand: 17.11.2026. Interner MSI-Testkandidat 1.3.0-offline-groups.5.

## Änderung
Die Online-Updateprüfung der Runtime läuft im Hintergrund. Sie darf das Öffnen des Hauptfensters nicht mehr blockieren. Leere HTTP-Fehlerantworten werden abgefangen. Studio-/Vault-Funktionen wurden für diesen Fix nicht geändert. Beim Neubau wurde das enthaltene .NET auf 8.0.33 aktualisiert.

## Installation und genau ein gezielter Test
1. Offene Arbeit speichern und SieEncode sowie RFiDGear/Runtime schließen. Falls vom fehlgeschlagenen Start noch ein unsichtbarer Runtime-Prozess läuft: Windows nach dem Speichern neu starten.
2. `SieEncode_1.3.0-offline-groups.5_internal_x64.msi` installieren und die Windows-Sicherheitsabfrage bestätigen. Kein portables Paket, keine vorherige Deinstallation erforderlich.
3. SieEncode öffnen. Dasselbe Testprojekt wie zuvor auswählen; bekannten Reader anschließen und ausschließlich die bekannte Testkarte verwenden.
4. Einmal **Transponder erkennen** anklicken. Falls angefordert, die Projektpassphrase lokal eingeben; nicht im Chat senden.
5. Erwartung: Das Runtime-Fenster öffnet sich ohne auf die Online-Updateantwort zu warten. Danach separat festhalten, ob die Karte erkannt wird oder welche Fehlermeldung erscheint.
6. Rückmeldung: Fenster geöffnet ja/nein, ungefähre Uhrzeit, genaue Meldung. Keine Schlüssel oder Passphrasen senden. Bei Fehler die Anwendung möglichst offen lassen, damit die Logs zugeordnet werden können.

Keine Schreib-, Formatierungs- oder Kodieraktion für diesen Test ausführen. Die erneute Vault-/Recovery-Gesamtabnahme ist für diese Änderung nicht angefordert.

## Nachweise und offene Grenzen
11 gezielte Update-/Startargumenttests bestanden, einschließlich verzögerter lokaler HTTP-Antwort und leerer HTTP-503-Antwort. MSI-Inhalt separat gegen Build-Dateien geprüft; Prüfsummen in `offline-groups-candidate5-verification.json`.
Eine echte Installation dieses Kandidaten und der native Fenster-/Kartenablauf sind noch nicht bestätigt. Die vorhandenen Kollegenanleitungen gelten weiter; historische Kandidat-4-Nachweise bleiben als solche gekennzeichnet. Noch keine vollständige Hardwarefreigabe oder GitHub-Veröffentlichung.

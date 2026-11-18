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

## Installationsnachweis vom 18.11.2026

Die VirtualBox-Test-VM SieEncode-Testlab-LTSC wurde mit dem MSI aus dem Kollegenpaket von Kandidat 4 auf Kandidat 5 aktualisiert. Die SHA-256-Prüfung vor dem Installieren entsprach dem Paketnachweis. Der Windows Installer meldete am 18.11.2026 um 19:05:28 den Abschlusscode 0. Die installierte Produktversion lautet 1.3.0-offline-groups.5+921da3edf8bc588391571d4c2d953db9c07d18ff. Die installierten Dateien stimmen mit den Prüfsummen im Paketnachweis überein:

- Studio/SieEncode.dll: 8533F2FBDFE4888CC8A80E89A8A35025888B9C05FA831B395289D030B291DDEC
- Runtime/SieEncode.Runtime.dll: 453EC36354ED7647B0846E855C259EA655FE6CDA27FC2874D7F38AA4251B9F69

Dieser Nachweis ergänzt die frühere Rückmeldung zur Kartenerkennung. Die installierten Dialogabläufe für Delegation und Verwaltungsnachfolge werden separat bewertet.

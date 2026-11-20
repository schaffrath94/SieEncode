# Kollegentest und Bedienung des Offline-Vaults

Für den Kollegentest gilt [Kandidat 6 mit MSI, Anleitung und Testplan](https://github.com/schaffrath94/SieEncode/releases/tag/v1.3.0-offline-groups.6). Das Upgrade von Kandidat 5, die installierten Prüfsummen, der Selbsttest und der Erhalt aller 14 geprüften Verwaltungsdateien sind im [Paketnachweis](offline-groups-candidate6-verification.json) dokumentiert.

Der [Abnahmebericht](../OFFLINE-GRUPPEN-ABNAHME.md) belegt für Kandidat 5 die begrenzte Delegation, Ablehnung wiederholter Anträge, den Entzug und den Verwaltungswechsel über die installierte Produkt-API mit zwei Windows-Profilen. Der sichtbare Studio-Dialog bestätigte Revision 10 und wies den erneut eingereichten Antrag ab. Offen sind die vollständigen positiven Dialogabläufe und die Sichtprüfung des automatischen Scrollens zur Fehlermeldung in Kandidat 6. Der Entwurf für die Integration nach main bleibt bis zum Abgleich der Nachweise offen.

### Arbeitsplan vom 18.11.2026

1. **Vault-Abnahme abschließen.** Revision 6 zeigt lokal „Testperson B · aktiv · Lokal“. Die öffentliche Freigabe zwischen zwei Windows-Profilen übertragen. Am Arbeitsplatz mit begrenzter Vollmacht den Gruppenumfang prüfen, eine Änderung beantragen, durch die Hauptverwaltung genehmigen und die erneute Einreichung prüfen. Nach Entzug den aktuellen Stand importieren und die Wirkung prüfen. Anschließend die autorisierte Verwaltungsnachfolge über die installierte Oberfläche einschließlich Neustart, neuer Signatur und Ablehnung der alten Verwaltung belegen. Die beiden Rollen so einrichten, dass ein zusätzlich vorhandener Hauptverwaltungsschlüssel den Berechtigungstest nicht verfälscht.
2. **Bedienung nach Aufgabe gliedern.** Projekt öffnen, Teamzugriff verwalten und Verwaltung übergeben oder wiederherstellen erhalten verständliche Einstiege. Die Oberfläche zeigt Rolle, Gruppe, Projektbezug, erlaubte Aktion und nächsten Schritt. Bei Offline-Verteilung unterscheidet sie lokal gespeicherten Stand und erforderlichen Import auf anderen Arbeitsplätzen. Den Erhalt der Funktionen und der Schlüsseltrennung prüfen.
3. **Kollegenstand liefern.** Kurzanleitung und Testszenarien mit den tatsächlichen Schaltflächen abgleichen. Zum freigegebenen Quellstand versionierten MSI, Prüfsummen sowie Installations- und Upgrade-Nachweise bereitstellen. Änderungen und Nachweise auf privaten GitHub-Branches sichern und den geprüften Stand nach `main` integrieren.
4. **Externe RFiDGear-Integration parallel erproben.** Die mitgelieferte Runtime für den Kollegentest beibehalten. Die Umstellung auf eine separat installierte offizielle Version setzt nachgewiesene Funktionsparität voraus.

Bestehende Nachweise mit ihrer Version wiederverwenden. Simulierte Tests, Mehrbenutzerprüfung und echte Kartenprüfung getrennt dokumentieren. PDF-Ausbau bleibt zurückgestellt.

## Ausgangslage für den Test mit getrennten Rollen

Die Profilprüfung im Gast ergab für die Bibliothek d9c6ffd7-0081-4189-b965-518c9204c10c: Bei testerB liegen lokale Registrierung und Hauptverwaltungsspeicher vor. Bei testerA fehlen beide. In den öffentlichen Dokumenten lag bei dieser Prüfung kein Registrierungspaket. Für die Abnahme übernimmt testerB die Hauptverwaltung. Auf testerA zuerst den geprüften öffentlichen Stand registrieren, dann eine eigene Verwaltungseinladung für eine getrennte Testperson erstellen und nur die vorgesehene Gruppe freigeben. Die bestehende Vollmacht für Testperson B in Revision 6 ist an deren Signierschlüssel gebunden. Ein Import der öffentlichen Bibliothek allein verschafft testerA diese Vollmacht nicht.
## Aktueller Einstieg für die Fortsetzung am 19.11.2026

Im installierten Kandidaten 5 hat testerA die Bibliothek offline registriert und den öffentlichen Stand von Revision 6 auf 7 aktualisiert. Der Verwaltungsplatz zeigte für Testperson A die Revision 7, keine aktive Vollmacht und keine verwaltbaren Gruppen. Diese Schritte sind erledigt.

Als Nächstes die öffentliche Einladung von Testperson A fertig exportieren, den Fingerabdruck abgleichen und unter testerB ausschließlich die Gruppe Lokal freigeben. Danach gelten die offenen Bediennachweise aus Schritt 1. Für den Kollegentest soll jede Rolle aus der Oberfläche erkennen können, wer die nächste Aktion an welchem Arbeitsplatz ausführt.

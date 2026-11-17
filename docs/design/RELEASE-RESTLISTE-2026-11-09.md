# Interner Kollegenrelease – Ziel Montag, 09.11.2026

Benutzerpriorität: ausschließlich bisher fehlende Szenarien prüfen. Keine
Wiederholung unveränderter Installations-, Core-, Recovery- oder Kopiertests.
Kandidat 4 bleibt erhalten; keine Umbenennung in eine finale Version ohne
ausdrückliche und nachvollziehbare Releaseentscheidung.

## Nicht erneut ausführen

17.11.2026: Fensterstart und Kartenerkennung nach Kandidat-5-Uebergabe vom
Benutzer bestaetigt. Kein erneuter Hashabgleich der installierten Version;
dieser Bericht ersetzt keine vollstaendige Analyse-/Kodierabnahme.

72 Regressionstests, 61 Anwendungsprüfungen, echte MSI-Upgrades und Datenerhalt
in drei Profilen; 22 Gruppen-/Recovery-/Delegationsphasen unter zwei Benutzern;
Normal-/Recovery-Buildsperre vom Benutzer bestätigt; Kopie und normale
Wiederöffnung des konkreten Testprojekts in getrennten Prozessen bestanden.
Die historische native Kopier-/Wiederherstellungsabnahme bleibt gültiger Nachweis
ihres damaligen Versionsstands. Keine künstliche vollständige Neuabnahme.

## Noch fehlende Szenarien und Prüfweg

| Szenario | Prüfweg / Grenze |
| --- | --- |
| Tatsächlich getrenntes Netzwerk, lokale Daten | Am 07.11. bestanden: VM-Kabel getrennt, Windows meldet 0 aktive Nicht-Loopback-Schnittstellen, normaler Zugang und Recovery erfolgreich. Nachweis offline-groups-candidate4-offline-result.json. Nicht wiederholen. |
| Korrigierte Auswahl nach Kopieren | Nur Editor-/Bibliotheksauswahl nach neuer Kopie manuell bestätigen. Kopierlogik und Schlüssel erneut zu testen ist nicht erforderlich. |
| Vollständiger Rechte-/Geräteentzug durch Dialoge | Benutzer nach Abschnitt C der manuellen Restabnahme; zusätzliche Geräte separat. Produkt-API bereits bestanden. |
| Delegation durch Dialoge | Benutzer nach Abschnitt D; keine automatische Desktopbedienung autorisiert. Produkt-API bereits bestanden. |
| Stationsfix mit Reader/Testkarte und erforderlicher Kartenablauf | Fensterstart und Kartenerkennung am 17.11. vom Benutzer bestaetigt. Vollstaendige Analyse/Kodieren und ChangeFileSettings bleiben offen. Kein Karten-Schreibversuch ohne klare Testumgebung. |
| Zweiter Rechner, nicht erreichbare Netzwerkablage, echte Synchronisationskonflikte | Noch nicht nachgewiesen. Lokale VM-Tests nicht als diesen Nachweis ausgeben. Für Verteilung bekannte Einschränkungen ausdrücklich nennen. |
| Reale Stromunterbrechung | Nicht getestet; Prozessabbruchtests ersetzen keinen solchen Nachweis. |

## Anleitung und Übergabe

- Kollegen: `docs/KOLLEGEN-KURZSTART.md` und `docs/KOLLEGEN-ERSTEINRICHTUNG.html`.
- Verwaltung: `docs/OFFLINE-GRUPPEN-SCHNELLSTART.md`.
- Manuelle Restabnahme: `docs/MANUELLE-RESTABNAHME-1.3.0.md` und gezielte
  `docs/ABNAHME-PASSPHRASEN-STATION.md`.
- Aktuelle Bilder sind nachrangig. Alte Bilder sind als solche gekennzeichnet.
- Keine Veröffentlichung oder abgeschlossene Releasefreigabe allein aus dieser
  Terminplanung ableiten. Offene Befunde und Nachweisgrenzen bleiben sichtbar.

GitHub-Push wurde früher abgelehnt und bislang nicht erneut autorisiert;
lokale Commits sind keine Sicherung auf GitHub. Vor Veröffentlichung klären.

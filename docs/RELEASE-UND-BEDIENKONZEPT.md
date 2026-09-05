# SieEncode – Bedien- und Freigabekonzept

## Zielbild

SieEncode soll von eingewiesenen Personen ohne RFID-, DESFire- oder
RFiDGear-Fachwissen sicher bedient werden können. Technische Funktionen werden
nicht entfernt. Die Oberfläche trennt stattdessen zwei Ebenen:

1. **Bedienebene:** eindeutige Entscheidung, kurze Begründung und genau eine
   nächste sichere Aktion.
2. **Fach-/Supportebene:** vollständige keyfreie Evidenz mit Run-ID-Korrelation,
   Taskposition, Tasktyp, Entscheidung, Outcome, Fehlercode, Skipgrund,
   Zeitstempel und Readbackvergleich.

Die Bedienebene darf technische Unsicherheit niemals als Erfolg vereinfachen.

## Verbindliche Laufentscheidung

Eine Karte wird nur mit **KODIERUNG ERFOLGREICH BESTÄTIGT** freigegeben. Dafür
müssen gleichzeitig gelten:

- korrelierter `TaskLoop.Success` für genau eine Run-ID,
- vollständige Taskresultate für den erwarteten Projektplan,
- alle finalen Readbacks nach Sitzungsbeginn frisch erzeugt,
- alle finalen Readbacks vollständig und bytegleich zum vorbereiteten SOLL.

| Bedienentscheidung | Bedeutung | Sichere nächste Aktion |
|---|---|---|
| Noch nicht abgeschlossen | Lauf oder Readbacks fehlen | Karte liegen lassen, Lauf beenden, erneut auswerten |
| Kodierung nicht freigegeben | Eindeutiger Lauf- oder Datenfehler | Nicht weiterzählen; Fehlerhinweis befolgen, Diagnose sichern |
| Laufnachweis unvollständig | Daten passen, aber autoritativer Engine-Nachweis fehlt | Nicht als Erfolg buchen; vollständigen Lauf wiederholen |
| Kodierung erfolgreich bestätigt | Engine- und Datennachweis vollständig | Karte entnehmen, nächsten Datensatz vorbereiten |

Fehlertexte nennen zusätzlich Ursache, betroffenen Task – sofern belastbar
korrelierbar – und eine kategorisierte Handlung für Key/Auth, Reader/Transport,
Kartenwechsel, Berechtigung/Protokoll, Readbackabweichung oder Runtime-Ausnahme.

## Ende eines RFiDGear-Laufs

- SieEncode schließt die selbst gestartete RFiDGear-Instanz erst, nachdem der
  Lauf terminal ausgewertet und sein Ergebnis gespeichert wurde.
- Zuerst wird ausschließlich das reguläre Schließen des Hauptfensters versucht.
  Ein erzwungenes Prozessende erfolgt nie automatisch.
- Konnte das Fenster regulär geschlossen werden, nennt die Bedienentscheidung
  dies vor der fachlichen nächsten Aktion (Karte entnehmen, Fehler behandeln
  oder nächsten Datensatz vorbereiten).
- Reagiert das Fenster nicht, bleibt es offen. Die Bedienentscheidung zeigt
  `RFIDGEAR NOCH OFFEN`, verlangt Karte und Datensatz unverändert zu lassen und
  verweist auf die bestätigungspflichtige Abschlussfunktion.

## UI-Hierarchie ohne Funktionsverlust

- Die Bedienentscheidung steht vor den Arbeitsschritten und bleibt der primäre
  Orientierungspunkt.
- Der normale Weg besitzt zwei erforderliche nummerierte Aktionen: Daten
  bereitstellen und Kodierung starten. Die automatische Ergebnisüberwachung
  kann mit der optionalen dritten Aktion sofort manuell aktualisiert werden.
- Kartenprüfung ist sichtbar als read-only gekennzeichnet.
- Der primäre Erkennungsmodus inventarisiert zuerst alle AIDs und prüft bekannte
  PICC-Zugänge. Ausschließlich eine erfolgreiche Inventur mit null Applications
  ergibt „leer“; Schlüssel- und Dateilesefehler bleiben unbekannt/ungeklärt.
- Serienmodus ist optional eingeklappt.
- Mastering, Recovery, Application-Abnahme und Hardwareplan bleiben unter
  **Erweiterte Kartenaktionen** vollständig erreichbar.
- Manuelles Öffnen von RFiDGear bleibt als Expertenfunktion erhalten.
- Technische Task-/Run-ID-Details und Diagnose bleiben vollständig vorhanden,
  dominieren aber nicht die normale Bedienentscheidung.
- Der Projekteditor führt sichtbar durch Profilwahl, Projektwerte,
  Produktivschlüssel/Vault, Speichern sowie Dry-Run/Build. Transportkeys bleiben
  vollständig vorhanden, sind aber als Experteneinstellung eingeklappt.
- Mehrere Grundprofile liegen in einer gemeinsamen, konfigurierbaren Bibliothek;
  jedes Projekt wählt genau eines. Import und Versionswechsel sind administrativ
  und fingerprintgesichert.

## Inhalt der Serien- und Supportnachweise

Ab Schema 1.1 speichert jeder abgeschlossene Datensatz zusätzlich:

- verständliche Bedienentscheidung,
- empfohlene nächste Aktion,
- kompakten Engine-/Readbacknachweis,
- explizite Kartenfreigabe `true/false`.

Kartendaten können enthalten sein; Schlüsselwerte bleiben ausgeschlossen.

## Aussagekraft des Fünf-Karten-Laufs vom 27.08.2026

Der Lauf stellt die technische Mehrkartenfolge realistisch dar: fünf physische
Transponder wurden nacheinander mit fünf fortlaufenden Datensätzen bearbeitet.
Jeder Lauf besaß 26/26 korrelierte Taskresultate, terminalen Erfolg, drei frische
bytegleiche Readbacks, persistenten Duplikatschutz und einen regulär beendeten
Runtimeprozess. Damit sind Zustandsfortschaltung, Kartenwechselgrenze und
Nachweisführung belastbar geprüft.

Noch nicht realistisch abgebildet wurden menschliche Bedienzeit, Fehlklicks,
Schichtübergabe und nachhaltiger Durchsatz: Das Auflegen erfolgte menschlich,
die sichtbaren UI-Aktionen wurden reproduzierbar automatisiert. Die damalige
Blankoklassifikation lag zudem im Testtreiber. Der Lauf ist daher ein technischer
Mehrkarten-Nachweis, aber weder ein Usability-Test noch eine Produktions-
Taktzeitmessung. Diese beiden Gates bleiben getrennt offen.

## Gate für einen ersten Pilot-Release

Ein Paket darf an weitere Testanwender gehen, wenn alle folgenden Punkte grün
oder ausdrücklich als Pilotgrenze dokumentiert sind:

1. **Software:** vollständige Unit-, Vertrags-, Paritäts- und UI-Smoke-Tests.
2. **Hardware:** sichtbarer Projekt-, Mastering-, Kodier-, Readback-, Preflight-
   und Application-Abnahmelauf mit exakt dem ausgelieferten Runtimefingerprint.
3. **Fehlerführung:** mindestens Readerunterbrechung, fehlender Readback,
   Readbackabweichung und unvollständige Run-ID-Evidenz werden fail-closed und
   laienverständlich dargestellt.
4. **Installation:** Neuinstallation, Upgrade, Deinstallation, Startmenü und
   persistente Einstellungen geprüft.
5. **Mehrbenutzer ohne gemeinsame AD:** gemeinsamer Projektstamm,
   zertifikatsbasierte persönliche Empfängerfreigaben, getrennt geprüfte
   SHA-256-Fingerabdrücke, abgewiesener dritter Benutzer und Bereinigung der
   lokalen produktiven Sitzungsartefakte mit zwei Windows-Benutzern oder
   Rechnern geprüft.
6. **Usability-Pilot:** mindestens zwei nicht am Projekt beteiligte Personen
   bearbeiten den normalen Ablauf ohne Zuruf; beobachtete Missverständnisse
   werden protokolliert.
7. **Darstellung:** 100 %, 125 % und 150 % Windows-Skalierung sowie mindestens
   1366 × 768 werden auf abgeschnittene oder unerreichbare Aktionen geprüft.
8. **Releasehygiene:** Versionsnummer, Changelog, bekannte Grenzen, Hashes und
   Signaturstatus sind sichtbar. Ein extern verteilter Produktionsrelease ist
   signiert; ein unsignierter Pilot ist unübersehbar als solcher gekennzeichnet.

## Bewusste Pilotgrenzen

- Ein einzelner Transponder belegt den technischen Pfad, nicht Verwechslungs-
  sicherheit oder echten Mehrkartendurchsatz.
- Die Runtimefreigabe gilt ausschließlich für den exakten EXE/DLL-Fingerprint.
- Upstream-Grenzen von RFiDGear werden nicht durch stille Builderheuristiken
  verdeckt.
- Ein Pilot ist noch keine unbeaufsichtigte Produktionsfreigabe.

## Wiederholbarer Feldgate-Sammler

Im Quell- und Releasearbeitsbereich sammelt
`tools\Test-PilotReleaseGates.ps1 -RunVisibleUiSmoke` die lokal
belegbaren Freigabenachweise. Er prüft die installierte Produktversion und
Authenticode-Signatur, führt den sichtbaren UI-Smoke-Test aus, erfasst die reale
Windows-DPI-Stufe und testet unter der aktuellen Windows-SID die Schreibbarkeit
des konfigurierten Projekt- und Profilstamms mit sofort wieder entfernten
Probendateien.

Die DPI-Evidenz wird je Skala und die Benutzer-Evidenz je SID getrennt
gespeichert. 100/125/150 Prozent beziehungsweise zwei Benutzer werden daher
nicht durch wiederholte Ausführung unter derselben Umgebung vorgetäuscht.
Usability und mehrere physische Karten bleiben ausdrücklich menschliche oder
physische Gates und werden niemals aus synthetischen Tests als bestanden
abgeleitet.

# Autonome Weiterarbeit - Stand 2026-09-09

## Autorisierung und Ziel
Der Benutzer hat autonome, tokensparsame Fertigstellung einschließlich automatischer
Weiterarbeit autorisiert. Keine Bestätigungen für gewöhnliche Implementierung,
Tests, private Branch-Pushes oder lokale Testpakete nötig. Keine echten Karten
beschreiben und RC15 nicht ersetzen. Hardware/Zweitbenutzer-Abnahmen offen ausweisen.

## Ausgangsstand
- Studio: codex/workspace-ui; drei Arbeitsbereiche, Anwendungskontext, Task- und Dateidetails.
- Runtime: codex/upstream-key-context, c959bd4; selektiver Key-Kontext-Port,
  193 Tests nach dokumentiertem Dispatcher-Flake bestanden. Noch keine Hardwareabnahme.
- UI-Paket: artifacts/SieEncode-UI-Test-20260908-win-x64.zip, 88.1 MiB,
  492 Dateien per SHA256 geprüft; Paket-Smoke-Test bestanden.
- Standard-Studio: 53 Regressionstests bestanden. Preview-Build nutzt
  -p:SieEncodeUiPreview=true und eigene ../Testdaten-Pfade. Nicht als RC16 ausgeben.

## Erledigt im aktuellen Abschnitt
Teststation hat direkten PDF-Istreport-Knopf; öffnet nur vorhandenen PDF-Pfad zum
letzten Inventurbericht, nicht das alte PDF nach neuer Erkennung. Textbericht-Knopf
prüft jetzt den tatsächlichen Berichtpfad statt nur das Karten-Erkennungsobjekt.
InventoryPayloadComparisonService als Fachlogik ergänzt: vergleicht explizit
bereitgestellte Sollbytes mit vollständigen eindeutigen Lesenachweisen derselben
AID. Gleich/abweichend/nicht vergleichbar getrennt; fehlende, doppelte, verweigerte
und partielle Nachweise bestätigen keine Gleichheit oder Abweichung. Noch keine
UI-/PDF-Anbindung dieses Vergleichsdienstes und keine Einstellungsabnahme.
53/53 Tests, CLI/WPF-Build bestanden (tmp/comparison-ci.log).
Isolierter Publish ui-preview.4 unter artifacts/ui-report-check/Studio inklusive
synthetischem Profil; WPF-Smoke OK (tmp/report-smoke/SieEncode/ui-smoke/),
275 UI-Elemente, Berichtpfadprüfung, 11 Seiten und bisherige Bedienabläufe geprüft.
Kein neues ZIP/Release. Vorheriger synthetischer Istreport bleibt unter
output/pdf/Istreport-Synthetische-Abnahme.pdf (beide Seiten visuell geprüft).

## Nächster Abschnitt - tatsächlich implementieren
1. Als Nächstes Vergleichsdienst in UI und Vergleichs-PDF anbinden: Sollbytes direkt
   aus PayloadEngine erzeugen, nicht aus aktuell auf 256 Byte gekürztem Sollreport.
   Metadatenvergleich separat nach Evidenzstatus; kein Zugriff ist keine Abweichung.
   Neue UI-Zustände mit synthetischer Inventur im WPF-Bediencheck abdecken.
   Expertenseiten der drei Arbeitsbereiche anhand FUNKTIONSPARITAET.md und
   UI-PDF-KONZEPT.md ausarbeiten: vollständige Anwendungen/Dateirechte,
   Schlüsselkontext, Datenrezepte, geordnete Taskpläne, detaillierte Istbefunde.
   Vorhandene Editoren und Modelle erweitern; keine parallele zweite Fachlogik.
2. Produktions-Berichtsdatenmodell für Soll, Ist und Vergleich; kein Zugriff,
   nicht geprüft und Abweichung getrennt. Vollständige Rohdaten mit Pagination,
   keine stille Kürzung, keine Schlüsselwerte. Kompakt/vollständig unabhängig
   vom UI-Modus; optional Notizen/Bilder/Datum/Ort/Unterschrift.
3. UI-Export integrieren, gezielte Regressionen und WPF-Smoke durchführen,
   alle finalen PDF-Seiten rendern/prüfen, altes Funktionsangebot erhalten.
4. Neues isoliertes internes Paket mit synthetischem Grundprofil, Identität,
   Anleitung und Prüfsummen bauen; als nicht hardwareabgenommen kennzeichnen.
5. Ergebnis liefern und Weiterarbeits-Automation pausieren. Bei tatsächlich
   unauflösbarer Blockade ebenfalls pausieren statt endlos erneut prüfen.

## Arbeitsweise
Pro Lauf einen sinnvollen Abschnitt fertigstellen, prüfen und private Branches
sichern. Diesen Stand kurz ersetzen/aktualisieren (erledigt, Testnachweis, nächster
Schritt); keine wachsenden Vollprotokolle. Keine unveränderten Gesamtinventuren,
unnötige Testwiederholungen, neuen Aufgaben oder weiteren Automationen.
Nur bei wesentlichem fertigem Meilenstein, fertigem Paket oder notwendiger
Benutzeraktion knapp informieren. Keine Routine-Zusammenfassungen ohne Änderung.

## Lokale Hinweise
.NET 8 vorhanden. Tests: Test-CI.ps1 (Konsolenharness, nicht dotnet test).
Windows SDK kann Sandbox-Eskalation benötigen. Gebündelte Python/Node-Runtimes:
C:/Users/z003t1jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/.
PDF-Skill bei PDF-Arbeit lesen. WPF-Smoke über veröffentlichte EXE mit
--ui-smoke-test. Für saubere Umgebung synthetisches Grundprofil
pilot-testkit/fixtures/Synthetic-AES-Mixed.base.json unter assets/profiles/base
mitliefern. Keine installierten Kundenprofile als Testvoraussetzung verwenden.
Preview-Publish-Befehl in UI-PREVIEW-PACKAGE-2026-09-08.md. Hardwareaktionen ohne
Runtime bleiben gesperrt. Es gibt keinen Produktquellcode im Elternrepository.

Automation: sieencode-ui-und-pdf-fertigstellen; stündlicher Heartbeat dieser Aufgabe.

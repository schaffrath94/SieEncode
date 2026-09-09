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
- Standard-Studio: 55 Regressionstests bestanden. Preview-Build nutzt
  -p:SieEncodeUiPreview=true und eigene ../Testdaten-Pfade. Nicht als RC16 ausgeben.

## Erledigt im aktuellen Abschnitt
Kleiner Folgeschritt auf ausdrücklichen Wunsch wegen knappem Nutzungslimit:
Soll-PDF-Export nutzt jetzt denselben kompakt/vollständig-Auswahldialog.
Dialogtexte gelten für Soll- und Vergleichsberichte. Gezielt nur WPF-Build geprüft:
0 Fehler/0 Warnungen, tmp/soll-options-build.log. Kein erneuter Gesamttestlauf,
keine PDF-Neuerzeugung, keine Behebung der bekannten Kürzungen im Soll-Vollrenderer.

Vergleichsexport bietet eigenes Dialogfenster kompakt/vollständig unabhängig vom
UI-Modus. Renderer akzeptiert CardReportDetail (Standard Full); Compact lässt
Rohdaten ausdrücklich weg und erhält alle Bewertungen/Befunde. Keine Veränderung
der bestehenden Exportaufrufe ohne Optionen. Alter Soll-Vollrenderer bleibt zu überarbeiten.
55/55 Tests bestanden; CLI/WPF-Build erfolgreich (tmp/compact-report-ci.log),
finale Renderer-Tests tmp/compact-report-final-tests.log. PDF-Vollbeispiel (2 Seiten)
und kompakte Variante (1 Seite) unter output/pdf/Dateivergleich-Vollstaendig*.pdf
vollständig visuell geprüft. Isolierter Publish preview.6 unter artifacts/ui-compact-check/Studio;
WPF-Smoke tmp/compact-smoke/ einschließlich Umschaltung im Berichtsdialog bestanden.
RC15-Quellinventur erneut bestanden; Nachweisgrenzen und gesamte Release-Restliste
in RELEASE-STATUS-2026-09-09.md festgehalten. Keine vollständige Funktions-/Hardwarefreigabe.

## Nächster Abschnitt - tatsächlich implementieren
1. Als Nächstes Berichtsauswahl auch bei Ist-Export anbieten und alten
   Sollreport ohne Kürzung paginieren. Optionale Dokumentationsfelder umsetzen.
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

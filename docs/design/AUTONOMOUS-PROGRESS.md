# Autonome Weiterarbeit - Stand 2026-09-11

## Autorisierung und Ziel
Der Benutzer hat autonome, tokensparsame Fertigstellung einschließlich automatischer
Weiterarbeit autorisiert. Keine Bestätigungen für gewöhnliche Implementierung,
Tests, private Branch-Pushes oder lokale Testpakete nötig. Keine echten Karten
beschreiben und RC15 nicht ersetzen. Hardware/Zweitbenutzer-Abnahmen offen ausweisen.

## Ausgangsstand
- Studio: codex/workspace-ui; drei Arbeitsbereiche, Anwendungskontext, Task- und Dateidetails.
- Runtime: codex/upstream-key-context, c959bd4; selektiver Key-Kontext-Port,
  193 Tests nach dokumentiertem Dispatcher-Flake bestanden. Noch keine Hardwareabnahme.
- UI-Paket: artifacts/SieEncode-UI-Test-preview13-win-x64.zip, ca. 71.2 MiB,
  494 Dateien per SHA256 geprüft; Paket-Smoke-Test bestanden.
- Standard-Studio: 61 Regressionstests bestanden. Preview-Build nutzt
  -p:SieEncodeUiPreview=true und eigene ../Testdaten-Pfade. Nicht als RC16 ausgeben.

## Erledigt im aktuellen Abschnitt
Benutzer meldete Invalid Handle/Absturz bei Vault-Einrichtung/Speichern.
Windows-Ereignis 1026 am 11.09.2026 15:23:23 lokal gelesen: CryptographicException
m_safeCertContext is an invalid handle in TeamVaultWindow.AddSelectedIdentity/
AddRecipient. Derselbe Fehler mit synthetischem Neuladen der Identitäten reproduziert.
Ursache: ComboBox behält freigegebene Auswahl beim Austausch gleichwertiger Einträge.
Beide Dialoge lösen nun Auswahl/ItemsSource vor Zertifikatsfreigabe und Neuaufbau.
Team-Aktionsfehler werden im Dialog angezeigt; Konflikthinweis verlangt erneutes Öffnen.
Quellcommit 8d89570. WPF-Smoke: dreimal Neuladen/Hinzufügen pro Dialog, keine ungültigen
Handles/Dubletten; Konflikt überschreibt Teamliste nicht; erneutes Öffnen/Speichern OK.
Windows-Speicher nur lesend geprüft: zwei gültige SieEncode-Zertifikate, nichts geändert,
keine privaten Schlüssel exportiert. Alle Regressionen hierzu verwenden synthetische Daten.
Korrigiertes Paket preview.13 erstellt, konkrete Paket-EXE bestand vollständigen
WPF-Smoke (tmp/preview13-smoke/). Paketnachweis: UI-PREVIEW-PACKAGE-preview13.md.
preview.12 wegen reproduziertem Vault-Fehler nicht mehr als aktuelle Empfehlung verwenden.
Realer Benutzer-Bedienlauf mit preview.13 sowie Hardware-/Zweitbenutzerabnahme offen.
## Nächster Abschnitt - tatsächlich implementieren
1. Verbleibende dynamische Dialoge gezielt prüfen: TextEntryPrompt/PassphrasePrompt
   (Bestätigen/Abbrechen), Identitätsauswahl und Statusaktualisierung mit synthetischen
   Identitäten. Keine erneute Gesamtinventur und kein unverändertes Paket neu bauen.
2. Positive Import-/Export-/Speicherabläufe und abschließenden UI-/PDF-Funktionsabgleich
   soweit synthetisch möglich abschließen. Native Dateidialoge, reale DPI-/Hardware-
   und Zweitbenutzerabnahme klar als offene praktische Release-Gates dokumentieren.
3. Nach Abschluss des Softwareumfangs Paket nur bei relevanten Änderungen aktualisieren,
   Ergebnis liefern und Automation pausieren. Aktuell verbleiben Softwareprüfungen;
   das fertige interne Zwischenpaket ist kein vollständiger Paritätsabschluss.
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

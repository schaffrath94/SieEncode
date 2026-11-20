# UI/PDF design review

The agreed direction is described in UI-PDF-KONZEPT.md: three dedicated workspaces,
reduced normal views and on-demand technical detail. This branch contains design
material only; production WPF and runtime source files are unchanged.

The three-page synthetic PDF sample can be regenerated on Windows with Python,
ReportLab and the Segoe UI / Consolas system fonts:

    python docs/design/build_pdf_preview.py output/pdf/SieEncode_Berichtsentwurf.pdf

The example includes a target report, observed-card report and target/actual
comparison. Every page explicitly marks synthetic data and no hardware acceptance.
No customer reference document, customer identifiers, real card evidence or keys
are included in this repository.

Validation on 2026-09-05:
- All three generated A4 PDF pages rendered with Poppler and visually inspected.
- Companion in-conversation mockup: coding simulation, invalid card-number rejection,
  project save state, cross-workspace project-name retention and comparison view checked.
- Layout checked at widths 360, 736 and 1024 in light/dark; no JavaScript errors or
  horizontal overflow. Narrow tables use stacked labeled fields.

The interactive mockup is conversation review material, not a replacement for WPF.
After product-owner feedback, implement the agreed flows and the production report
renderer in separate changes. Long-report pagination and real hardware evidence
remain part of implementation acceptance, not established by the three-page sample.
## Erweiterung vom 06.09.2026

- FUNKTIONSPARITAET.md ordnet die bestehenden Aufgaben den neuen Oberflächen zu.
- RC15-BEDIENINVENTUR.md und rc15-inventory.json dokumentieren die aus dem
  unveränderten Baseline-Tag erfassten Einstiegspunkte. Regeneration und Prüfung:

      python tools/inventory_rc15.py
      python tools/inventory_rc15.py --check

  Erfasst: 286 XAML-Flächen, 12 dynamische Ereignisbindungen, 146 UI-Handler,
  26 CLI-Befehle und 6 Startparameter. Diese Zählung ist kein Verhaltenstest.
- Erweiterter Expertenentwurf: Taskplan, Serie, Mastering/Recovery, Protokoll;
  Dateirechte, Schlüsselreferenzen, Payload, Profile/Historie; Chip/PICC,
  Anwendungsinventur, Tiefenanalyse und Soll/Ist; Vault und Diagnose.
- Moduswechsel erhält ungespeicherte Eingaben. Die Detailbereiche verwenden
  ausschließlich synthetische Daten und führen keine Kartenoperationen aus.
- Fünfseitigen technischen PDF-Entwurf erzeugen:

      python docs/design/build_full_pdf_preview.py output/pdf/SieEncode_Technischer_Vollbericht_Entwurf.pdf

  Alle fünf Seiten wurden gerendert und visuell geprüft. Das Beispiel enthält
  Prüfkontext, Chipfelder mit unbekanntem Status, Anwendungen, vier Dateirechte,
  HEX/ASCII, Vergleich, geplante Tasks und Nachweise. Automatische Fortsetzung
  großer Datenbestände ist als Anforderung beschrieben, noch nicht implementiert.
- Vorschau automatisiert geprüft: alle Expertentabs, Moduswechsel ohne Datenverlust,
  bestehende Demoabläufe und Ansichten bei 360/736/1024 Pixel in Hell/Dunkel.

Produktive WPF-Dateien und Runtime wurden in diesem Schritt nicht geändert.

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
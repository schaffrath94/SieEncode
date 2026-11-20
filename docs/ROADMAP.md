# Development after RC15

> **Aktuelle Priorität, 21.11.2026:** Erst das bestehende SieEncode als klar bedienbares, geprüftes erstes Release fertigstellen; **keine Runtime-/Upstream-Bridge-Migration vor dem Release**. Die aktuelle Arbeitsreihenfolge und die UX-/Abnahmekriterien stehen in [RELEASE-FIRST-UX-2026-11-21.md](design/RELEASE-FIRST-UX-2026-11-21.md). Der nachstehende RC15-Fahrplan ist historischer Ausgangspunkt und keine alternative Prioritätenliste.

1. Preserve RC15, establish independent private repositories and software CI.
2. Agree on an integrated UI and reporting concept with the product owner.
3. Implement UI navigation, project editing, vault setup and coding station flows
   on separate branches; progressively extract responsibilities from large UI classes.
4. Rework the PDF export for three explicit purposes: project target specification,
   observed card inventory and target/actual acceptance comparison. Render and
   visually verify all pages, including long tables and HEX data.
5. Build a new acceptance candidate and bind hardware evidence to its exact hashes.

UI/PDF implementation has not started in the repository migration. RC15 behavior
remains the reference. No software CI result substitutes for reader/card acceptance.

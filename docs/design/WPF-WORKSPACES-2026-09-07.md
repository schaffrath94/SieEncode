# WPF workspace shell - 2026-09-07

Implemented on codex/workspace-ui, based on the reviewed UI/PDF concept.

## Delivered

- Three primary workspaces: Karten kodieren, Projekte, Karten untersuchen.
- Project-local navigation to library, editor, profiles/history, structure/taskplan,
  and project/profile comparison. Inspection navigation includes acceptance.
- Session navigation remembers the last page per workspace and actual back history.
  Navigation changes visibility of existing views; it does not reload projects,
  editor controls, series inputs, reader sessions or report state.
- Normal mode reduces additional entry points. Switching to it preserves an open
  expert page and its history, with a visible route and explanation.
- Existing detailed preflight and advanced card actions are reachable in expert
  mode. Existing confirmations, enablement and hardware gates are unchanged.
- Eleven original pages, existing command handlers, CLI and startup switches remain.

## Validation

- Test-CI.ps1: 49/49 executable regression tests passed; tests, CLI and WPF build
  with zero warnings/errors.
- inventory_rc15.py --check: passed (structural retention only).
- Actual WPF --ui-smoke-test: passed. Includes all eleven pages, automation IDs,
  navigation, normal/expert visibility, minimum 1100x720 layout, synthetic project
  loading, legacy artifact compatibility, series controls and five synthetic
  coding-matrix scenarios. No reader operations were performed.
- New WPF test exercises an unsaved editor value, workspace return to last page,
  mode switch while profiles are open, and back navigation.
- Optional SIEENCODE_UI_REVIEW_DIRECTORY emits six WPF-rendered PNGs during the
  explicit smoke test using its synthetic fixture. Representative project,
  coding and inspection views visually inspected. These are source-build previews.

## Remaining implementation and acceptance

This delivers the workspace shell and state-preserving navigation. Individual
page layouts still contain RC15 components. The richer expert editors and the
new production Soll/Ist/full PDF exporter are subsequent work, not completed by
this commit. No installation, version promotion, bit-identical baseline claim,
new hardware acceptance or merge of the runtime branch occurred.

Windows DPI 125/150 percent and the installed end-to-end hardware/user acceptance
remain release gates. See FUNKTIONSPARITAET.md for behavioral acceptance scope.

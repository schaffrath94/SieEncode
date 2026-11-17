# Codex instructions — SieEncode Studio

These instructions apply to the entire `schaffrath94/SieEncode` repository. Read `README.md`, `docs/ROADMAP.md`, and the relevant feature/contract documents before making changes. This repository is the **Studio/project builder**, not the card execution engine.

## Scope and working method

- Preserve and improve the existing implementation; search for the current owner of a feature before introducing new classes, abstractions, workflows, or parallel implementations.
- Work on the requested backlog item only. Identify existing behavior, dependencies, acceptance criteria, and regression risks first. Avoid unrelated refactors and sweeping rewrites.
- Treat **RC15 as a preserved baseline**, not permission to replace or silently migrate project data. `rc15-source-baseline` and the original delivered binaries are provenance records, not fresh approvals.
- Prefer small, reviewable changes. Keep older project/profile/vault formats readable; never silently rewrite existing project definitions, profiles, version history, rfPrj files, payloads, or reports.
- Keep product behavior, UI, and documentation consistent. State any unresolved limitations clearly; do not declare a feature complete based only on a successful compile.
- When documentation disagrees, check current code and tests; `README.md`, `VERSION`, current build scripts, and explicit baseline records take precedence over historical version statements.

## Repository boundaries

- `SieEncode`: project/profile editing, versioning, vault access, project building, preflight, coding station orchestration, reports, and UI.
- `SieEncode.Runtime`: separately maintained RFiDGear-derived execution engine. It is normally cloned next to this repository; `runtime.lock.json` pins the RC15 source baseline. Do not casually update its pin or imply a new engine build is previously hardware-approved.
- `schaffrath94/RFiDGear`: upstream-oriented fork, kept separate for upstream contributions. Evaluate changes selectively; never blindly merge its history into the Studio or sanitized Runtime.
- RFiDGear owns reader/card polling, physical tasks, and card commands. Studio owns orchestration and evidence evaluation; do not duplicate the reader/crypto execution engine in Studio.
- Review the existing `RUNTIME.md`, `docs/RFIDGEAR-HOST-CONTRACT-V1.md`, `PARITY.md`, `MIGRATION.md`, and `docs/HARDWARE-ABNAHME.md` before changing their respective contracts.

## Security and correctness are non-negotiable

- Fail closed on unknown reader/runtime capabilities, mismatched binary versions or hashes, missing/uncorrelated task results, failed preflight, or ambiguous card state. A version label alone does not prove a capability.
- Never report a card operation as successful without the required authoritative terminal status, correlated task evidence, and applicable readback/acceptance checks.
- Never silently retry non-idempotent card operations (create/write/format/key change). Maintain the explicit confirmation and verified state transitions required by existing flows.
- No hardware/card access, writes, formatting, recovery, or use of customer media without explicit approval for that specific test. Recovery/FormatPICC requires distinct destructive approval. Software CI and synthetic fixtures are **not** hardware acceptance.
- Keep productive keys, vault contents, customer/base profiles, raw card/payload bytes, credentials, certificates, local reader evidence, binaries, installers, and build caches out of the repository and out of logs, reports, test fixtures, and PR text. Only synthetic, non-sensitive fixtures may be committed.
- Preserve profile version/fingerprint binding, atomic save + optimistic revision checks, persistent sidecar locks and exclusive workspace lease, project-local path containment, and key-free evidence.
- Respect supported DESFire file types and evidence limitations; never add UI-only support that the engine cannot safely execute and verify.

## Build, test, and release

- Development baseline: Windows, PowerShell, .NET SDK from `global.json`.
- Run `./Test-CI.ps1` for changes affecting behavior. This builds the test harness, **executes it with `dotnet run`**, and builds the CLI and WPF app. `dotnet test` by itself does not execute the Studio's custom regression harness.
- Add or update focused, synthetic regression cases for bug fixes and new behavior. Clearly distinguish **executed**, **not executed**, and **hardware-only** checks in the final report.
- `Test-All.ps1` may require legacy external assets/customer fixtures; do not assume these are available or load real customer data into CI.
- Build the Runtime separately with `Build-Runtime.ps1` only when relevant. Never treat a rebuild as bit-identical to a delivered binary or automatically inherit its hardware approval.
- `Build-Release.ps1` needs an explicit verified Runtime source and WiX 3 path. Do not generate, sign, publish, distribute, or assert a new release/hardware approval unless that task is explicitly requested and its evidence is available.
- Respect third-party notices and licensing (`LICENSE.md`, `legal/`).

## Completion report

For each task, briefly give: (1) what changed and where; (2) tests actually run and results; (3) anything not tested, especially hardware; (4) remaining risks/known blockers; and (5) whether a separate Runtime update or new hardware acceptance is required. Do not mark an item complete if an acceptance criterion remains unverified.

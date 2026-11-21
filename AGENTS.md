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
- **Batched development and test milestones (21.11.2026):** Implement several cohesive UX/Vault/project/station improvements first, secure the incremental source commits, and add focused synthetic regression cases as code. Do **not** demand a new MSI, Windows session, UI click-through, workflow execution, or release after every small edit/commit. Treat the first release as a **single milestone acceptance**, not a stream of mini-releases. Exceptional critical blockers must be identified transparently, not masked.
- **First-release baseline clarified:** SieEncode is developing its **first 1.0 release**. There are no previously released SieEncode project files or Vault files needing customer migration. Older candidate numbers are internal development artifacts, **not** earlier production releases. Do not plan or expose migration from a nonexistent previous SieEncode release as a default workflow. Preserve tested low-level adapters without expanding them into normal UI or promising backwards-compatibility requirements the user never requested. The default path is personal identity → signed group policy where needed → brand-new project with group Vault and separate recovery → explicit safe coding. Avoid conflating group registration, certificate possession and actual project authorization.
- At the **cohesive milestone gate**, run `./Test-CI.ps1` once for the accumulated behavior changes. This builds the test harness, **executes it with `dotnet run`**, and builds the CLI and WPF app. `dotnet test` alone does not execute the Studio's custom regression harness. Reuse valid unchanged evidence; focus manual checks on new or impacted workflows. No release without the appropriate Windows, installer, security and hardware acceptance evidence.
- Add or update focused, synthetic regression cases for bug fixes and new behavior. Clearly distinguish **executed**, **not executed**, and **hardware-only** checks in the final report.
- `Test-All.ps1` may require legacy external assets/customer fixtures; do not assume these are available or load real customer data into CI.
- Build the Runtime separately with `Build-Runtime.ps1` only when relevant. Never treat a rebuild as bit-identical to a delivered binary or automatically inherit its hardware approval.
- `Build-Release.ps1` needs an explicit verified Runtime source and WiX 3 path. Do not generate, sign, publish, distribute, or assert a new release/hardware approval unless that task is explicitly requested and its evidence is available.
- Respect third-party notices and licensing (`LICENSE.md`, `legal/`).

## Completion report

For each task, briefly give: (1) what changed and where; (2) tests actually run and results; (3) anything not tested, especially hardware; (4) remaining risks/known blockers; and (5) whether a separate Runtime update or new hardware acceptance is required. Do not mark an item complete if an acceptance criterion remains unverified.

## Official RFiDGear bridge migration (2026-11-20)

- Keep the shipped Runtime and its historical evidence intact. Work incrementally behind an **optional** adapter and require a separate parity and hardware acceptance before switching it on. Never equate the new upstream v2.1.0 installer with the unpublished v2.1.1 master features.
- Upstream issues: `c3rebro/RFiDGear#232` (bridge), `#233` (auto mode), `#234` (key providers). Current host capabilities `project.revision` (PR #396) and `Check UID` with runtime variable (PR #397) exist on upstream `master` as of 2026-11-20, **not** in published v2.1.0. Gate by actual `hello.capabilities`, not a version number.
- On the bridge use `hello.instanceId` + opaque `projectRevision`, pass `expectProjectRevision` to `startRun`, and preserve a distinct persistent Studio-approved project/profile/payload digest. External files and persisted payloads are not covered by the upstream in-process revision.
- A writing job must be tied to a fresh card approval. Until upstream provides one-shot AutoMode approval, restrict writes to single `startRun` operations: `scanReader` → Studio approval → `Check UID($EXPECTEDUID)` as a gating task → all writing tasks conditional on that check → independent read-only acceptance after a remove/reapply cycle. A UID match is *not* cryptographic card authentication. Do not use an AutoMode startup variable as a reusable writing authorization.
- Persist a unique request ID **before** sending `startRun`. If start outcome is uncertain, query `getRunResult` with the same ID; never retry a non-idempotent write with a new ID. Mark unknown/bridgeFault/lost task detail as requiring independent inspection. Treat bridge project revision as valid for one host process only; reapprove after host restart.
- Upstream PR #398 (`keys.context`) has merged into `master`: `resolveKey` now includes optional requestId, source and projectRevision; `registerKeyProvider(ownRunsOnly=true)` lets the host refuse non-client runs itself. Require capability `keys.context`, verify the response confirms `ownRunsOnly`, and additionally bind every vault decision to the approved request ID, instance, project revision, UID, AID, key slot, usage and reference. **Not included in the published v2.1.0 release.** No productive vault resolution until our adapter has negative tests and independent hardware acceptance.
- Upstream PR #399 (`execution.verificationDetails`) has also merged into `master`: only DESFire `Check File Settings` currently provides versioned non-secret numeric comparisons through `TaskOutcomeDto.verificationDetails`. Accept unknown kinds/versions safely; a terminal success or matching UID is **not** independent card readback. Neither #398 nor #399 is in published v2.1.0.
- PR #400 proposes an approved single-run sample; PR #401 proposes including project revision on AutoMode results. **Both are open, not released** as of 2026-11-20. AutoMode has no one-time after-presentation approval yet: no productive writes by AutoMode.
- The canonical additional test design and remaining gaps are in `docs/design/RFIDGEAR-BRIDGE-ABNAHMEVORSCHLAG.md`. Read it together with upstream `RFiDGear.Bridge.Contracts/CLIENT-GUIDE.md`; tests using simulated readers do not count as real reader/card acceptance.

# SieEncode Studio

Private development repository for **SieEncode 1.1.0-rc15** and subsequent work.
SieEncode manages RFID projects, card encoding workflows, vault access and reports.
The execution engine is maintained separately in [SieEncode.Runtime](https://github.com/schaffrath94/SieEncode.Runtime).

## Source and releases

- `rc15-source-baseline` preserves the imported RC15 source.
- [Baseline records](docs/baseline/README.md) distinguish original package hashes, source provenance and software/hardware evidence.
- `main` contains repository integration changes after that snapshot.
- The existing [RFiDGear fork](https://github.com/schaffrath94/RFiDGear) remains the source for upstream contributions.

## Build and test

Windows with the .NET 8 SDK selected by `global.json`:

```powershell
./Test-CI.ps1
```

This builds the CLI, WPF app and headless HTTPS sync host, and executes the synthetic regression harness.
No card reader is accessed. The optional legacy `Test-All.ps1` additionally
requires the external historical builder and customer fixtures; it is not the CI entry point.

Clone both repositories as siblings. `Build-Runtime.ps1` defaults to the sibling
`SieEncode.Runtime` source and writes to `artifacts/runtime`. Run it before
`Build-Release.ps1 -RuntimeSource ./artifacts/runtime -WixBin '<WiX-3-bin>'`.
Release packaging is deliberately manual until exact-build hardware evidence is attached.
A rebuilt binary is not automatically covered by the old RC15 hardware approval.

## Repository boundaries

Customer/base profile files, vaults, live card evidence, certificates, installers
and build caches are not source-controlled. Only synthetic test fixtures are shipped.
See [licensing status](LICENSE.md), [third-party notices](legal/THIRD-PARTY-NOTICES.md)
and [development roadmap](docs/ROADMAP.md).

Older operational documents retain historical version statements. The baseline
records, current VERSION and current build scripts take precedence for repository setup.

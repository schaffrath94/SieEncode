# UI preview package - 2026-09-08

Publish command (Windows x64, bundled .NET runtime):

```powershell
dotnet publish src/RFiDGear.Builder.App/RFiDGear.Builder.App.csproj -c Release -r win-x64 --self-contained true -p:SieEncodeUiPreview=true -p:Version=1.1.0-ui-preview.1 -p:InformationalVersion=1.1.0-ui-preview.1 -o artifacts/ui-preview-20260908/Studio
```

The compile-time preview flag changes only the preview build's identity and default
settings/project/profile/backup paths. Defaults live under ../Testdaten relative
to Studio. Legacy installed settings are not imported. This is path separation,
not a sandbox: users must not deliberately select production directories.
Normal builds retain their existing RC15 paths and version behavior.

Copy only pilot-testkit/fixtures/Synthetic-AES-Mixed.base.json into
Studio/assets/profiles/base for a synthetic starter profile. Do not package user
profiles, vaults or the installed runtime. No RFID runtime or installer is bundled.
The included .NET runtime is the application framework, not SieEncode.Runtime.

Final published EXE passed --ui-smoke-test. The initial fresh-package run exposed
missing starter profiles. The second run exposed a test assumption that a runtime
was installed. The test now expects hardware actions to remain disabled when no
verified autorun-capable executable is configured; production gating is unchanged.
Final run passed with the synthetic profile and no RFID runtime. No hardware test.

The package includes a start script, German instructions, local UI-test evidence,
build-input hashes and SHA-256 file manifest. It is an internal UI development
preview, not RC16 and not a replacement for the installed RC15 release.

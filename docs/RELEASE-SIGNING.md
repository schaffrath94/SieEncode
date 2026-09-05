# Release-Signierung

Ein extern verteiltes Produktionspaket darf nur mit gültiger Authenticode-Signatur
freigegeben werden. Ein ausschließlich intern eingesetztes Release darf die fehlende
Signatur ausdrücklich als akzeptierte interne Grenze dokumentieren. Der Build signiert Studio-EXE, Runtime-EXE und MSI mit
SHA-256 und prüft jede Signatur unmittelbar.

Beispiel:

```powershell
.\Build-Release.ps1 `
  -RuntimeSource '<PFAD-ZUR-ABGENOMMENEN-RUNTIME>' `
  -RequireSignature `
  -CodeSigningCertificateThumbprint '<THUMBPRINT>' `
  -TimestampUrl 'http://timestamp.digicert.com'
```

`RuntimeSource` ist absichtlich verpflichtend. Der Releasebau akzeptiert nur eine
Runtime mit `BUILD-IDENTITY.json`, die zuvor durch `Build-Runtime.ps1` erzeugt und
getestet wurde. Ein interner Hardware-Abnahmekandidat wird ausschließlich mit
`-AcceptanceCandidateRuntime` gebaut; diese Freischaltung ist in normalen Builds
nicht enthalten.

Ohne Thumbprint entsteht ausschließlich ein ausdrücklich als unsigniert
gekennzeichnetes internes Paket. `artifacts/package/SIGNING.json`
enthält den maschinenlesbaren Status. `-RequireSignature` verhindert in diesem
Fall fail-closed die Paketerzeugung.

Der Zertifikats-Private-Key und seine PIN gehören nicht in Repository,
Projektdefinition, Vault, Buildskript oder CI-Log. Der Thumbprint ist kein
Geheimnis; das Zertifikat muss im Windows-Zertifikatsspeicher des ausführenden
Kontos verfügbar sein.

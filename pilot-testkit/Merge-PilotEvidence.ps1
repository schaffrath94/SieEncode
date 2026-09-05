[CmdletBinding()]
param(
    [string]$EvidenceDirectory = '',
    [string]$TestkitDirectory = '',
    [switch]$FailOnOpenGates
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($TestkitDirectory)) { $TestkitDirectory = $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = Join-Path $TestkitDirectory 'Evidenz' }

$records = @(Get-ChildItem -LiteralPath $EvidenceDirectory -Filter 'pilot-*.json' -File -ErrorAction SilentlyContinue | ForEach-Object {
    $record = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
    if ($record.schemaVersion -ne '1.0' -or $record.containsKeys -ne $false) { throw "Ungültige oder nicht schlüsselwertfreie Evidenz: $($_.FullName)" }
    if (-not $record.environment.sidHash -or -not $record.environment.scalePercent) { throw "Identitäts-/DPI-Evidenz unvollständig: $($_.FullName)" }
    $record
})

$validEnvironment = @($records | Where-Object {
    $_.ui.fresh -eq $true -and $_.ui.status -eq 'OK' -and
    $_.sharedPaths.projects.Writable -eq $true -and $_.sharedPaths.profiles.Writable -eq $true
})
$scales = @($validEnvironment | Select-Object -ExpandProperty environment | Select-Object -ExpandProperty scalePercent -Unique | Sort-Object)
$sids = @($validEnvironment | Select-Object -ExpandProperty environment | Select-Object -ExpandProperty sidHash -Unique)
$dpiPassed = @(@(100,125,150) | Where-Object { $scales -notcontains $_ }).Count -eq 0
$usersPassed = $sids.Count -ge 2

$usabilityFiles = @(Get-ChildItem -LiteralPath $TestkitDirectory -Filter 'USABILITY-TESTER-*.md' -File -ErrorAction SilentlyContinue)
$validUsability = @($usabilityFiles | Where-Object {
    $text = [IO.File]::ReadAllText($_.FullName, (New-Object Text.UTF8Encoding($false)))
    $text -match '(?m)^- Normaler Ablauf vollständig ohne Zuruf:\s*Ja\s*$' -and
    $text -match '(?m)^- Sicherheitsentscheidung korrekt:\s*Ja\s*$'
})
$usabilityPassed = (@($validUsability.BaseName | Sort-Object -Unique).Count -ge 2)

$cardCsv = Join-Path $TestkitDirectory 'MEHRKARTEN-TESTBOGEN.csv'
$cards = if (Test-Path -LiteralPath $cardCsv) { @(Import-Csv -LiteralPath $cardCsv) } else { @() }
$validCards = @($cards | Where-Object {
    $_.Freigabe_bestaetigt -eq 'Ja' -and
    $_.SieEncode_Ergebnis -eq 'KODIERUNG ERFOLGREICH BESTÄTIGT' -and
    $_.Kartenfreigabe -eq 'Ja' -and $_.Runtime_automatisch_beendet -eq 'Ja' -and
    -not [string]::IsNullOrWhiteSpace($_.Kartenlabel) -and
    -not [string]::IsNullOrWhiteSpace($_.Serienwert) -and
    -not [string]::IsNullOrWhiteSpace($_.Evidenzdatei) -and
    (Test-Path -LiteralPath (Join-Path $TestkitDirectory $_.Evidenzdatei) -PathType Leaf)
})
$cardLabels = @($validCards.Kartenlabel | Sort-Object -Unique)
$seriesValues = @($validCards.Serienwert | Sort-Object -Unique)
$cardsPassed = $cardLabels.Count -ge 5 -and $seriesValues.Count -ge 5

$gates = @(
    [pscustomobject]@{ Gate='DPI 100/125/150'; Status=if($dpiPassed){'BESTANDEN'}else{'OFFEN'}; Evidence=('Eindeutige reale Skalen: '+($scales -join ', ')+' %') },
    [pscustomobject]@{ Gate='Zwei Windows-Benutzer'; Status=if($usersPassed){'BESTANDEN'}else{'OFFEN'}; Evidence=("$($sids.Count)/2 eindeutige SID-Hashes mit UI- und Schreibnachweis") },
    [pscustomobject]@{ Gate='Zwei unabhängige Bediener'; Status=if($usabilityPassed){'BESTANDEN'}else{'OFFEN'}; Evidence=("$($validUsability.Count)/2 strikt bestandene Testbögen") },
    [pscustomobject]@{ Gate='Fünf physische Karten'; Status=if($cardsPassed){'BESTANDEN'}else{'OFFEN'}; Evidence=("$($cardLabels.Count)/5 Labels und $($seriesValues.Count)/5 Serienwerte mit vorhandener Evidenz") },
    [pscustomobject]@{ Gate='Codesignatur'; Status='PILOTGRENZE'; Evidence='Muss vom Release-Verantwortlichen direkt am finalen MSI und den Binärdateien verifiziert werden; externe JSON-Evidenz darf dieses Gate nicht schließen.' }
)
$result = [ordered]@{ schemaVersion='1.0'; containsKeys=$false; generatedAt=[DateTimeOffset]::Now.ToString('O'); records=$records.Count; gates=$gates }
$jsonPath = Join-Path $EvidenceDirectory 'GESAMT-AUSWERTUNG.json'
$textPath = Join-Path $EvidenceDirectory 'GESAMT-AUSWERTUNG.txt'
$packageManifest = Join-Path $TestkitDirectory 'PACKAGE-SHA256.txt'
$packageHeadline = if (Test-Path -LiteralPath $packageManifest) {
    [IO.File]::ReadAllLines($packageManifest, (New-Object Text.UTF8Encoding($false))) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
} else { 'SieEncode – internes Releasepaket' }
$evidenceHeadline = $packageHeadline -replace 'interne Releasepakete', 'Gesamt-Abnahmeevidenz'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$lines = @($evidenceHeadline,('Erzeugt: '+$result.generatedAt),('Umgebungsnachweise: '+$records.Count),'')
$lines += $gates | ForEach-Object { "[$($_.Status)] $($_.Gate): $($_.Evidence)" }
$lines | Set-Content -LiteralPath $textPath -Encoding UTF8
$lines
Write-Host $jsonPath
Write-Host $textPath
if($FailOnOpenGates -and @($gates | Where-Object Status -eq 'OFFEN').Count -gt 0){exit 2}

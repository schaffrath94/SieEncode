[CmdletBinding()]
param(
    [string]$ProjectName = 'UI-Abnahme-RC56-20260826',
    [string]$StartNumber = '',
    [string]$InstalledExe = 'C:\Program Files\SieEncode\Studio\SieEncode.exe'
)

$ErrorActionPreference = 'Stop'
$StartNumber = if ([string]::IsNullOrWhiteSpace($StartNumber)) { Get-Date -Format 'MMddHHmm' } else { $StartNumber }
$host.UI.RawUI.WindowTitle = 'SieEncode Mehrkartentest – sichtbarer Benutzerlauf'

$driver = Join-Path $PSScriptRoot 'Automation\Invoke-FiveCardUiSeries.ps1'
$evidenceBase = Join-Path $PSScriptRoot 'Evidenz'
$activePointer = Join-Path $evidenceBase 'aktive-mehrkartenserie.txt'

function Invoke-Driver {
    param([Parameter(Mandatory)][string]$Action,[int]$CardIndex = 0,[string]$CardLabel = '',[switch]$Authorized,[switch]$BlankCardConfirmed)
    $arguments = @{
        Action = $Action
        CardIndex = $CardIndex
        CardLabel = $CardLabel
        ProjectName = $ProjectName
        InstalledExe = $InstalledExe
        StartNumber = $StartNumber
        EvidenceRoot = $script:runRoot
        CardEvidenceRoot = $script:cardRoot
        CardCsvPath = $script:csvPath
    }
    if ($Authorized) { $arguments.Authorized = $true }
    if ($BlankCardConfirmed) { $arguments.BlankCardConfirmed = $true }
    & $driver @arguments
}

if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) {
    throw "UI-Treiber fehlt: $driver"
}
New-Item -ItemType Directory -Path $evidenceBase -Force | Out-Null

if (Test-Path -LiteralPath $activePointer) {
    $runRoot = (Get-Content -LiteralPath $activePointer -Raw).Trim()
    $resumeStatePath = Join-Path $runRoot 'active-series.json'
    $resumeProcess = $null
    if (Test-Path -LiteralPath $resumeStatePath) {
        try { $resumeProcess = Get-Process -Id ((Get-Content -LiteralPath $resumeStatePath -Raw | ConvertFrom-Json).processId) -ErrorAction Stop } catch { $resumeProcess = $null }
    }
    if (-not (Test-Path -LiteralPath $resumeStatePath) -or -not $resumeProcess) {
        Remove-Item -LiteralPath $activePointer -Force
        $runRoot = ''
    }
}
if ([string]::IsNullOrWhiteSpace($runRoot)) {
    $runRoot = Join-Path $evidenceBase ('Mehrkartentest-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    Set-Content -LiteralPath $activePointer -Value $runRoot -Encoding UTF8
}

$cardRoot = Join-Path $runRoot 'cards'
$csvPath = Join-Path $runRoot 'MEHRKARTEN-TESTBOGEN.csv'
New-Item -ItemType Directory -Path $cardRoot -Force | Out-Null

try {
    $statePath = Join-Path $runRoot 'active-series.json'
    if (-not (Test-Path -LiteralPath $statePath)) {
        Write-Host ''
        Write-Host 'SieEncode wird jetzt wie von einem Benutzer über die installierte Oberfläche geöffnet.' -ForegroundColor Cyan
        Write-Host 'Bis zur späteren Freigabe werden keine Kartendaten verändert.'
        Invoke-Driver -Action Start
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Write-Host ''
    Write-Host "Installierte Version: $($state.installedProductVersion)" -ForegroundColor Green
    Write-Host "Projekt:             $($state.project)"
    Write-Host "Evidenzordner:       $runRoot"
    Write-Host ''
    Write-Host 'Ab hier werden die fünf ausdrücklich freigegebenen Testtransponder formatiert, gemastert, kodiert und verifiziert.' -ForegroundColor Yellow
    Write-Host 'Für jede Position muss ein physisch anderer Transponder aufgelegt werden.' -ForegroundColor Yellow
    $release = Read-Host 'Zum Fortfahren exakt eingeben: FREIGABE FUER 5 TESTTRANSPONDER'
    if ($release -cne 'FREIGABE FUER 5 TESTTRANSPONDER') { throw 'Keine passende Schreibfreigabe eingegeben. Es wurde nichts geschrieben.' }

    for ($index = 1; $index -le 5; $index++) {
        $label = 'TESTKARTE-{0:D2}' -f $index
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if (@($state.completedCards | Where-Object { $_.index -eq $index }).Count -gt 0) {
            Write-Host "$label ist bereits nachweislich abgeschlossen – wird nicht erneut beschrieben." -ForegroundColor DarkGray
            continue
        }
        Write-Host ''
        Write-Host "Karte $index/5: $label auflegen und alle anderen Karten vom Leser entfernen." -ForegroundColor Cyan
        $confirmation = Read-Host "Dann zur Verwechslungssicherung exakt $label eingeben"
        if ($confirmation -cne $label) { throw "Kartenbestätigung stimmt nicht. $label wurde nicht gestartet." }
        $blank = Read-Host 'Ist dies ausdrücklich ein neuer, vollständig leerer Transponder? Exakt JA oder NEIN eingeben'
        if ($blank -cnotin @('JA','NEIN')) { throw 'Blankokarten-Angabe ist nicht eindeutig. Es wurde nichts geschrieben.' }
        if ($blank -ceq 'JA') {
            Invoke-Driver -Action ProcessBlankCard -CardIndex $index -CardLabel $label -Authorized -BlankCardConfirmed
        }
        else {
            Invoke-Driver -Action ProcessCard -CardIndex $index -CardLabel $label -Authorized
        }
        Write-Host "$label bestanden. Karte jetzt vom Leser nehmen." -ForegroundColor Green
    }

    Invoke-Driver -Action Finish
    Remove-Item -LiteralPath $activePointer -Force -ErrorAction SilentlyContinue
    Write-Host ''
    Write-Host 'MEHRKARTENTEST BESTANDEN: 5/5. SieEncode und RFiDGear wurden geschlossen.' -ForegroundColor Green
    Write-Host "Die schlüsselfreie Evidenz liegt in: $runRoot"
}
catch {
    Write-Host ''
    Write-Host 'TEST GESTOPPT' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host 'Es wird nicht mit der nächsten Karte fortgefahren. Der Evidenzordner bleibt für Analyse/Wiederaufnahme erhalten.'
    exit 1
}

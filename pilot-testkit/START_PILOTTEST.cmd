@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-PilotEvidence.ps1" -RunVisibleUiSmoke
set "SIEENCODE_TEST_EXIT=%ERRORLEVEL%"
echo.
if not "%SIEENCODE_TEST_EXIT%"=="0" echo Pilottest fehlgeschlagen. Bitte Ausgabe und Evidenz unveraendert weitergeben.
if "%SIEENCODE_TEST_EXIT%"=="0" echo Automatische Pilotpruefung abgeschlossen. Ergebnis liegt im Ordner Evidenz.
pause
exit /b %SIEENCODE_TEST_EXIT%

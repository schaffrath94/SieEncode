@echo off
setlocal
title SieEncode Mehrkartentest
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-FiveCardUiSeries.ps1"
set "TEST_EXIT=%ERRORLEVEL%"
echo.
if not "%TEST_EXIT%"=="0" echo Der Test wurde sicher gestoppt. Fehlercode: %TEST_EXIT%
pause
exit /b %TEST_EXIT%

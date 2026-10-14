@echo off
rem PedalKeys one-click setup. Per-user install, no administrator prompt.
rem   1. copies PedalKeys.exe to %LOCALAPPDATA%\PedalKeys
rem   2. adds a Start-with-Windows entry (HKCU\...\Run)
rem   3. starts the app (it appears in the system tray)
setlocal
set "DEST=%LOCALAPPDATA%\PedalKeys"
set "SRC=%~dp0"

echo.
echo  PedalKeys setup
echo  ---------------

if not exist "%SRC%PedalKeys.exe" goto :noexe

rem Stop a running copy so the file can be replaced (safe if none is running).
taskkill /im PedalKeys.exe /f >nul 2>&1
ping -n 2 127.0.0.1 >nul

if not exist "%DEST%" mkdir "%DEST%"
copy /y "%SRC%PedalKeys.exe" "%DEST%\PedalKeys.exe" >nul
if errorlevel 1 goto :copyfail

rem Files from a downloaded zip carry a "downloaded from the internet" mark. Clear it
rem on our own copy so Windows doesn't re-prompt every time it starts. Best effort.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%DEST%\PedalKeys.exe'" >nul 2>&1

rem Start with Windows (current user only).
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v PedalKeys /t REG_SZ /d "\"%DEST%\PedalKeys.exe\"" /f >nul
reg add "HKCU\Software\PedalKeys" /v Initialized /t REG_DWORD /d 1 /f >nul

start "" "%DEST%\PedalKeys.exe"

echo  Installed to %DEST%
echo  PedalKeys is now running in the system tray (check the ^^ overflow arrow).
echo.
echo  Next: open YouTube, click the video once, then use the pedal.
echo.
timeout /t 8 >nul
exit /b 0

:noexe
echo  PedalKeys.exe was not found next to setup.bat.
echo  Right-click the downloaded zip, choose "Extract All...", then run setup.bat again.
echo.
pause
exit /b 1

:copyfail
echo  Could not copy to %DEST%.
echo  Close PedalKeys if it is running and try again.
echo.
pause
exit /b 1

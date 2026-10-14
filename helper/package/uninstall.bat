@echo off
rem Removes PedalKeys: stops it, deletes the startup entry and the install folder.
setlocal
set "DEST=%LOCALAPPDATA%\PedalKeys"

taskkill /im PedalKeys.exe /f >nul 2>&1
ping -n 2 127.0.0.1 >nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v PedalKeys /f >nul 2>&1
reg delete "HKCU\Software\PedalKeys" /f >nul 2>&1
if exist "%DEST%" rmdir /s /q "%DEST%"

echo.
echo  PedalKeys has been removed.
echo.
timeout /t 5 >nul

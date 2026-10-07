@echo off
title FlightRadar Screensaver - Install

echo ==============================================================
echo        FlightRadar Screensaver - Install
echo        Live flights map (OpenSky Network)
echo ==============================================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run as ADMINISTRATOR!
    echo Right-click the file -^> "Run as administrator"
    pause
    exit /b 1
)

set SOURCE_DIR=%~dp0
set DEST_SYSTEM=%windir%\System32\
set DEST_USER=%LOCALAPPDATA%\Microsoft\Windows\Themes\

echo Install for:
echo   1) All users -^> %windir%\System32\  (RECOMMENDED)
echo   2) Current user only
echo.
set /p CHOICE="Select (1/2): "

if "%CHOICE%"=="2" (
    set DEST=%DEST_USER%
) else (
    set DEST=%DEST_SYSTEM%
)
if not exist "%DEST%" mkdir "%DEST%"

REM The .scr needs its DLLs and the WebAssets folder next to it,
REM so we copy the whole set of files, not just the screensaver.
echo.
echo Copying ALL files to "%DEST%" ...

copy /Y "%SOURCE_DIR%FlightRadarScreensaver.scr"                "%DEST%" >nul
copy /Y "%SOURCE_DIR%FlightRadarScreensaver.dll"                "%DEST%" >nul
copy /Y "%SOURCE_DIR%FlightRadarScreensaver.deps.json"          "%DEST%" >nul
copy /Y "%SOURCE_DIR%FlightRadarScreensaver.runtimeconfig.json" "%DEST%" >nul
copy /Y "%SOURCE_DIR%FlightRadarScreensaver.exe"                "%DEST%" >nul
copy /Y "%SOURCE_DIR%appsettings.json"                          "%DEST%" >nul
copy /Y "%SOURCE_DIR%Microsoft.Web.WebView2.*.dll"              "%DEST%" >nul
copy /Y "%SOURCE_DIR%Newtonsoft.Json.dll"                       "%DEST%" >nul
if not exist "%DEST%Assets" mkdir "%DEST%Assets"
copy /Y "%SOURCE_DIR%Assets\app.ico"                            "%DEST%Assets\" >nul
if not exist "%DEST%WebAssets" mkdir "%DEST%WebAssets"
copy /Y "%SOURCE_DIR%WebAssets\*.*"                             "%DEST%WebAssets\" >nul
if exist "%SOURCE_DIR%runtimes" xcopy /E /I /Y "%SOURCE_DIR%runtimes" "%DEST%runtimes\" >nul

if not exist "%DEST%FlightRadarScreensaver.scr" (
    echo [ERROR] Copy failed: screensaver not found in destination
    pause
    exit /b 1
)
if not exist "%DEST%FlightRadarScreensaver.dll" (
    echo [ERROR] Copy failed: DLL missing
    pause
    exit /b 1
)
if not exist "%DEST%WebAssets\map.html" (
    echo [ERROR] Copy failed: WebAssets\map.html missing
    pause
    exit /b 1
)

echo.
echo DONE! Installed to %DEST%
echo.
echo Next steps:
echo   1. Win+R -^> desk.cpl -^> Enter
echo   2. "Screen Saver" tab -^> FlightRadarScreensaver
echo   3. Click "Settings..." and enter coordinates and zoom
echo   4. "Preview" or "OK"
echo.
echo Or open settings directly:
echo   "%DEST%FlightRadarScreensaver.scr" /c
echo.
pause
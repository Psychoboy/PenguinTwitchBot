@echo off
setlocal

set SCRIPT_DIR=%~dp0
set OUTPUT=%SCRIPT_DIR%LocalPackages

echo Packing TwitchLib packages to: %OUTPUT%
echo.

rem -----------------------------------------------------------------------
rem TwitchLib.EventSub.Core (leaf - no local TwitchLib deps)
rem -----------------------------------------------------------------------
echo [1/2] Packing TwitchLib.EventSub.Core...
dotnet pack "%SCRIPT_DIR%TwitchLib.EventSub.Core\TwitchLib.EventSub.Core\TwitchLib.EventSub.Core.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

rem -----------------------------------------------------------------------
rem TwitchLib.EventSub.Websockets (after EventSub.Core is in the feed)
rem -----------------------------------------------------------------------
echo [2/2] Packing TwitchLib.EventSub.Websockets...
dotnet pack "%SCRIPT_DIR%TwitchLib.EventSub.Websockets\TwitchLib.EventSub.Websockets\TwitchLib.EventSub.Websockets.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

rem -----------------------------------------------------------------------
rem Clear stale TwitchLib entries from the global NuGet packages cache so
rem the freshly-packed versions are used on the next restore/build.
rem -----------------------------------------------------------------------
echo Clearing TwitchLib EventSub entries from global NuGet cache...
for /d %%P in ("%USERPROFILE%\.nuget\packages\twitchlib.eventsub.*") do (
    echo   Removing %%P
    rd /s /q "%%P"
)

echo.
echo All packages packed successfully to: %OUTPUT%
echo Global NuGet cache cleared for TwitchLib.EventSub.* packages.
echo You can now build PenguinTwitchBot normally.

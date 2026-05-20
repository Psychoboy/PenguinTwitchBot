@echo off
setlocal

set SCRIPT_DIR=%~dp0
set OUTPUT=%SCRIPT_DIR%LocalPackages

echo Packing TwitchLib packages to: %OUTPUT%
echo.

rem -----------------------------------------------------------------------
rem TwitchLib.EventSub.Core (leaf - no local TwitchLib deps)
rem -----------------------------------------------------------------------
echo [1/9] Packing TwitchLib.EventSub.Core...
dotnet pack "%SCRIPT_DIR%TwitchLib.EventSub.Core\TwitchLib.EventSub.Core\TwitchLib.EventSub.Core.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

rem -----------------------------------------------------------------------
rem TwitchLib.Api sub-projects (in dependency order)
rem -----------------------------------------------------------------------
echo [2/9] Packing TwitchLib.Api.Core.Enums...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Core.Enums\TwitchLib.Api.Core.Enums.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [3/9] Packing TwitchLib.Api.Core.Interfaces...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Core.Interfaces\TwitchLib.Api.Core.Interfaces.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [4/9] Packing TwitchLib.Api.Core.Models...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Core.Models\TwitchLib.Api.Core.Models.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [5/9] Packing TwitchLib.Api.Core...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Core\TwitchLib.Api.Core.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [6/9] Packing TwitchLib.Api.Helix.Models...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Helix.Models\TwitchLib.Api.Helix.Models.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [7/9] Packing TwitchLib.Api.Helix...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api.Helix\TwitchLib.Api.Helix.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo [8/9] Packing TwitchLib.Api...
dotnet pack "%SCRIPT_DIR%TwitchLib.Api\TwitchLib.Api\TwitchLib.Api.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

rem -----------------------------------------------------------------------
rem TwitchLib.EventSub.Websockets (after EventSub.Core is in the feed)
rem -----------------------------------------------------------------------
echo [9/9] Packing TwitchLib.EventSub.Websockets...
dotnet pack "%SCRIPT_DIR%TwitchLib.EventSub.Websockets\TwitchLib.EventSub.Websockets\TwitchLib.EventSub.Websockets.csproj" ^
    -o "%OUTPUT%" -c Release
if %ERRORLEVEL% neq 0 (echo FAILED & exit /b %ERRORLEVEL%)

echo.
echo All packages packed successfully to: %OUTPUT%
echo You can now build DotNetTwitchBot normally.

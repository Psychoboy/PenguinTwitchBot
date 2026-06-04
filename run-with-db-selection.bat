@echo off
setlocal enabledelayedexpansion

:menu
cls
echo.
echo ========================================
echo   Select Database Provider
echo ========================================
echo.
echo 1. SQLite (default)
echo 2. PostgreSQL
echo 3. Exit
echo.
set /p choice="Select database provider (1-3): "

if "!choice!"=="1" (
    set DATABASE_PROVIDER=sqlite
    echo Selected: SQLite
) else if "!choice!"=="2" (
    set DATABASE_PROVIDER=postgres
    echo Selected: PostgreSQL
) else if "!choice!"=="3" (
    exit /b 0
) else (
    echo Invalid choice. Please try again.
    timeout /t 2
    goto menu
)

echo.
echo Starting application with !DATABASE_PROVIDER! provider...
echo.

REM Launch Visual Studio or dotnet with the selected provider
cd PenguinTwitchBot
dotnet run
cd ..

exit /b 0

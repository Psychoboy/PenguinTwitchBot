@echo off
setlocal enabledelayedexpansion

:menu
cls
echo.
echo ========================================
echo   Select Database Provider
echo ========================================
echo.
echo 1. MariaDB (default)
echo 2. PostgreSQL
echo 3. SQLite
echo 4. Exit
echo.
set /p choice="Select database provider (1-4): "

if "!choice!"=="1" (
    set DATABASE_PROVIDER=mariadb
    echo Selected: MariaDB
) else if "!choice!"=="2" (
    set DATABASE_PROVIDER=postgres
    echo Selected: PostgreSQL
) else if "!choice!"=="3" (
    set DATABASE_PROVIDER=sqlite
    echo Selected: SQLite
) else if "!choice!"=="4" (
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
cd DotNetTwitchBot
dotnet run
cd ..

exit /b 0

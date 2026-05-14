@echo off
REM Remove last migration from all 3 database providers

echo.
echo Building solution first...
dotnet build DotNetTwitchBot.sln -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Removing last migration from all 3 providers...
echo.

set ROOT_DIR=%cd%

echo Removing from MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations remove -c ApplicationDbContext
if errorlevel 1 (
    echo Error removing MariaDB migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Removing from PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations remove -c ApplicationDbContext
if errorlevel 1 (
    echo Error removing PostgreSQL migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Removing from SQLite...
REM Create Data directory if it doesn't exist
if not exist "%ROOT_DIR%\DotNetTwitchBot\Data" mkdir "%ROOT_DIR%\DotNetTwitchBot\Data"
cd DotNetTwitchBot.Migrations.Sqlite
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations remove -c ApplicationDbContext
if errorlevel 1 (
    echo Error removing SQLite migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo Last migration removed from all 3 providers!
exit /b 0

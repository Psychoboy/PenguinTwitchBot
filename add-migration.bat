@echo off
REM Quick migration generator for all 3 database providers
REM Usage: add-migration.bat MigrationName

if "%1"=="" (
    echo Usage: add-migration.bat MigrationName
    echo Example: add-migration.bat AddUserFeature
    exit /b 1
)

set migrationName=%1

echo.
echo Building solution first...
dotnet build DotNetTwitchBot.sln -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Generating migration: %migrationName%
echo.

set ROOT_DIR=%cd%

echo Generating for MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations
if errorlevel 1 (
    echo Error generating MariaDB migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Generating for PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations
if errorlevel 1 (
    echo Error generating PostgreSQL migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Generating for SQLite...
REM Create Data directory if it doesn't exist
if not exist "%ROOT_DIR%\DotNetTwitchBot\Data" mkdir "%ROOT_DIR%\DotNetTwitchBot\Data"
cd DotNetTwitchBot.Migrations.Sqlite
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations
if errorlevel 1 (
    echo Error generating SQLite migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo ✓ Migrations generated successfully for all 3 providers!
exit /b 0

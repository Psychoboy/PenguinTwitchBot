@echo off
REM Quick migration generator for all 3 database providers
REM Usage: add-migration.bat MigrationName

if "%~1"=="" (
    echo Usage: add-migration.bat MigrationName
    echo Example: add-migration.bat AddUserFeature
    exit /b 1
)

set "migrationName=%~1"

echo.
echo Building solution first...
dotnet build PenguinTwitchBot.sln -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Copying migration DLLs to startup project bin...
copy /Y "PenguinTwitchBot.Migrations.MariaDb\bin\Debug\net10.0\PenguinTwitchBot.Migrations.MariaDb.dll" "PenguinTwitchBot\bin\Debug\net10.0\" >nul
copy /Y "PenguinTwitchBot.Migrations.Postgres\bin\Debug\net10.0\PenguinTwitchBot.Migrations.Postgres.dll" "PenguinTwitchBot\bin\Debug\net10.0\" >nul
copy /Y "PenguinTwitchBot.Migrations.Sqlite\bin\Debug\net10.0\PenguinTwitchBot.Migrations.Sqlite.dll" "PenguinTwitchBot\bin\Debug\net10.0\" >nul

echo.
echo Generating migration: %migrationName%
echo.

set ROOT_DIR=%cd%

echo Generating for MariaDB...
cd PenguinTwitchBot.Migrations.MariaDb
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=mariadb
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\PenguinTwitchBot"
if errorlevel 1 (
    echo Error generating MariaDB migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Generating for PostgreSQL...
cd PenguinTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=postgres
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\PenguinTwitchBot"
if errorlevel 1 (
    echo Error generating PostgreSQL migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Generating for SQLite...
REM Create Data directory if it doesn't exist
if not exist "%ROOT_DIR%\PenguinTwitchBot\Data" mkdir "%ROOT_DIR%\PenguinTwitchBot\Data"
cd PenguinTwitchBot.Migrations.Sqlite
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=sqlite
dotnet ef migrations add "%migrationName%" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\PenguinTwitchBot"
if errorlevel 1 (
    echo Error generating SQLite migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo Migrations generated successfully for all 3 providers!
exit /b 0

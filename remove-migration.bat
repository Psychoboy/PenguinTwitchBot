@echo off
REM Remove last migration from PostgreSQL and SQLite

echo.
echo Building solution first...
dotnet build PenguinTwitchBot.sln -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Copying migration DLLs to startup project bin...
copy /Y "PenguinTwitchBot.Migrations.Postgres\bin\Debug\net10.0\PenguinTwitchBot.Migrations.Postgres.dll" "PenguinTwitchBot\bin\Debug\net10.0\" >nul
copy /Y "PenguinTwitchBot.Migrations.Sqlite\bin\Debug\net10.0\PenguinTwitchBot.Migrations.Sqlite.dll" "PenguinTwitchBot\bin\Debug\net10.0\" >nul

echo.
echo Removing last migration from both providers...
echo.

set ROOT_DIR=%cd%

echo Removing from PostgreSQL...
cd PenguinTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=postgres
dotnet ef migrations remove -c ApplicationDbContext --startup-project "%ROOT_DIR%\PenguinTwitchBot"
if errorlevel 1 (
    echo Error removing PostgreSQL migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Removing from SQLite...
REM Create Data directory if it doesn't exist
if not exist "%ROOT_DIR%\PenguinTwitchBot\Data" mkdir "%ROOT_DIR%\PenguinTwitchBot\Data"
cd PenguinTwitchBot.Migrations.Sqlite
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=sqlite
dotnet ef migrations remove -c ApplicationDbContext --startup-project "%ROOT_DIR%\PenguinTwitchBot"
if errorlevel 1 (
    echo Error removing SQLite migration
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo Last migration removed from all 3 providers!
exit /b 0

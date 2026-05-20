@echo off
REM Update database for all 3 database providers

echo.
echo Building solution first...
dotnet build DotNetTwitchBot.sln -c Debug
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Copying migration DLLs to startup project bin...
copy /Y "DotNetTwitchBot.Migrations.MariaDb\bin\Debug\net10.0\DotNetTwitchBot.Migrations.MariaDb.dll" "DotNetTwitchBot\bin\Debug\net10.0\" >nul
copy /Y "DotNetTwitchBot.Migrations.Postgres\bin\Debug\net10.0\DotNetTwitchBot.Migrations.Postgres.dll" "DotNetTwitchBot\bin\Debug\net10.0\" >nul
copy /Y "DotNetTwitchBot.Migrations.Sqlite\bin\Debug\net10.0\DotNetTwitchBot.Migrations.Sqlite.dll" "DotNetTwitchBot\bin\Debug\net10.0\" >nul

echo.
echo Updating databases for all 3 providers...
echo.

set ROOT_DIR=%cd%

echo Updating MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=mariadb
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating MariaDB
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Updating PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=postgres
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating PostgreSQL
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Updating SQLite...
REM Create Data directory if it doesn't exist (SQLite needs it)
if not exist "%ROOT_DIR%\DotNetTwitchBot\Data" mkdir "%ROOT_DIR%\DotNetTwitchBot\Data"
cd DotNetTwitchBot.Migrations.Sqlite
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
set DATABASE_PROVIDER=sqlite
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating SQLite
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo All databases updated successfully!
exit /b 0

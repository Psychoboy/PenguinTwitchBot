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
echo Updating databases for all 3 providers...
echo.

set ROOT_DIR=%cd%

echo Updating MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef database update -c ApplicationDbContext
if errorlevel 1 (
    echo Error updating MariaDB
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo Updating PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
set DOTNET_TWITCHBOT_ROOT=%ROOT_DIR%
dotnet ef database update -c ApplicationDbContext
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
dotnet ef database update -c ApplicationDbContext
if errorlevel 1 (
    echo Error updating SQLite
    cd %ROOT_DIR%
    exit /b 1
)
cd %ROOT_DIR%

echo.
echo All databases updated successfully!
exit /b 0

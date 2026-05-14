@echo off
setlocal enabledelayedexpansion
set ROOT_DIR=%cd%

:menu
cls
echo.
echo ========================================
echo   Database Migration Manager
echo ========================================
echo.
echo 1. Generate new migration
echo 2. Remove last migration
echo 3. Update database
echo 4. Show migration status
echo 5. Exit
echo.
set /p choice="Select option (1-5): "

if "!choice!"=="1" goto generate
if "!choice!"=="2" goto remove
if "!choice!"=="3" goto update
if "!choice!"=="4" goto status
if "!choice!"=="5" goto end
echo Invalid choice. Please try again.
timeout /t 2
goto menu

:generate
echo.
set /p migrationName="Enter migration name: "
if "!migrationName!"=="" (
    echo Migration name cannot be empty.
    timeout /t 2
    goto menu
)

echo.
echo Generating migration for MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
dotnet ef migrations add "!migrationName!" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error generating MariaDB migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Generating migration for PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
dotnet ef migrations add "!migrationName!" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error generating PostgreSQL migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Generating migration for SQLite...
cd DotNetTwitchBot.Migrations.Sqlite
dotnet ef migrations add "!migrationName!" -c ApplicationDbContext -o Migrations --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error generating SQLite migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Migrations generated successfully for all 3 providers!
timeout /t 3
goto menu

:remove
echo.
echo Removing last migration from MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
dotnet ef migrations remove -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error removing MariaDB migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Removing last migration from PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
dotnet ef migrations remove -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error removing PostgreSQL migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Removing last migration from SQLite...
cd DotNetTwitchBot.Migrations.Sqlite
dotnet ef migrations remove -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error removing SQLite migration
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Last migration removed from all 3 providers!
timeout /t 3
goto menu

:update
echo.
echo Updating database for MariaDB...
cd DotNetTwitchBot.Migrations.MariaDb
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating MariaDB database
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Updating database for PostgreSQL...
cd DotNetTwitchBot.Migrations.Postgres
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating PostgreSQL database
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Updating database for SQLite...
cd DotNetTwitchBot.Migrations.Sqlite
dotnet ef database update -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error updating SQLite database
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo Databases updated successfully!
timeout /t 3
goto menu

:status
echo.
echo MariaDB Migrations:
cd DotNetTwitchBot.Migrations.MariaDb
dotnet ef migrations list -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error listing MariaDB migrations
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo PostgreSQL Migrations:
cd DotNetTwitchBot.Migrations.Postgres
dotnet ef migrations list -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error listing PostgreSQL migrations
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
echo SQLite Migrations:
cd DotNetTwitchBot.Migrations.Sqlite
dotnet ef migrations list -c ApplicationDbContext --startup-project "%ROOT_DIR%\DotNetTwitchBot"
if errorlevel 1 (
    echo Error listing SQLite migrations
    cd %ROOT_DIR%
    timeout /t 3
    goto menu
)
cd ..

echo.
timeout /t 5
goto menu

:end
echo Exiting...
exit /b 0

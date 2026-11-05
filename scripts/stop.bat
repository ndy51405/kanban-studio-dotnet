@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%.."

cd /d "%PROJECT_ROOT%"

docker compose version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    docker compose down
) else (
    docker-compose down
)

if %ERRORLEVEL% EQU 0 (
    echo Kanban Studio stopped.
) else (
    echo Failed to stop Kanban Studio.
    exit /b %ERRORLEVEL%
)

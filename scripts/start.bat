@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%.."

cd /d "%PROJECT_ROOT%"

if not exist "data" mkdir "data"

docker compose version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    docker compose up --build -d
) else (
    docker-compose up --build -d
)

if %ERRORLEVEL% EQU 0 (
    echo Kanban Studio is running at http://localhost:8000
) else (
    echo Failed to start Kanban Studio.
    exit /b %ERRORLEVEL%
)


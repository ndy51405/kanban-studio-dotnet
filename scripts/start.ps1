$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\.."

Set-Location $ProjectRoot

if (-not (Test-Path -Path "data")) {
    New-Item -ItemType Directory -Path "data" | Out-Null
}

$useDockerComposeCmd = $false
try {
    & docker compose version 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $useDockerComposeCmd = $true
    }
} catch {
    $useDockerComposeCmd = $false
}

if ($useDockerComposeCmd) {
    & docker compose up --build -d
} else {
    & docker-compose up --build -d
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Kanban Studio is running at http://localhost:8000"
} else {
    Write-Error "Failed to start Kanban Studio."
    exit $LASTEXITCODE
}


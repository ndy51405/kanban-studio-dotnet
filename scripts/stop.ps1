$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\.."

Set-Location $ProjectRoot

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
    & docker compose down
} else {
    & docker-compose down
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Kanban Studio stopped."
} else {
    Write-Error "Failed to stop Kanban Studio."
    exit $LASTEXITCODE
}

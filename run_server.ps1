# Run IPC MCP Server
param(
    [string]$Token = "7f46fda81f4d4b51878cdf01aca45804"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "WARNING: Not running with Administrator privileges." -ForegroundColor Yellow
    Write-Host "Some IPC operations may require admin access." -ForegroundColor Yellow
}

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $scriptPath "IpcMcp\bin\Release\net10.0-windows\ipc-mcp.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Building server..." -ForegroundColor Yellow
    Push-Location (Join-Path $scriptPath "IpcMcp")
    dotnet build -c Release
    Pop-Location
}

if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Server executable not found" -ForegroundColor Red
    exit 1
}

Write-Host "Starting IPC MCP Server..." -ForegroundColor Green
Write-Host "HTTP endpoint: http://localhost:23481" -ForegroundColor Cyan
Write-Host "Token authentication: Enabled" -ForegroundColor Cyan
& $exePath -token $Token

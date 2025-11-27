# Install IPC MCP Server as Windows Service
# Requires NSSM (Non-Sucking Service Manager) or similar

param(
    [string]$Token = "7f46fda81f4d4b51878cdf01aca45804"
)

$ErrorActionPreference = "Stop"

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script requires Administrator privileges. Please run as Administrator."
    exit 1
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

# Check for NSSM
$nssmPath = Get-Command nssm -ErrorAction SilentlyContinue
if (-not $nssmPath) {
    Write-Host "NSSM not found. Checking common locations..." -ForegroundColor Yellow
    $commonPaths = @(
        "C:\Program Files\nssm\nssm.exe",
        "C:\ProgramData\chocolatey\bin\nssm.exe"
    )
    
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $nssmPath = $path
            break
        }
    }
    
    if (-not $nssmPath) {
        Write-Host "NSSM not found. Attempting to install via winget..." -ForegroundColor Yellow
        try {
            winget install NSSM.NSSM
            $nssmPath = "C:\Program Files\nssm\nssm.exe"
        }
        catch {
            Write-Host "ERROR: Could not install NSSM automatically. Please install it manually:" -ForegroundColor Red
            Write-Host "  choco install nssm -y" -ForegroundColor Yellow
            Write-Host "  or" -ForegroundColor Yellow
            Write-Host "  winget install NSSM.NSSM" -ForegroundColor Yellow
            exit 1
        }
    }
}
else {
    $nssmPath = $nssmPath.Source
}

if (-not (Test-Path $nssmPath)) {
    Write-Host "ERROR: NSSM not found. Please install it manually." -ForegroundColor Red
    exit 1
}

$serviceName = "IpcMcp"

Write-Host "Installing service: $serviceName" -ForegroundColor Green
Write-Host "Token: $Token" -ForegroundColor Cyan

# Remove existing service if it exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    & $nssmPath stop $serviceName
    Start-Sleep -Seconds 2
    & $nssmPath remove $serviceName confirm
    Start-Sleep -Seconds 2
}

# Create logs directory
$logsDir = Join-Path $scriptPath "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

# Install service with token parameter
Write-Host "Installing service..." -ForegroundColor Yellow
& $nssmPath install $serviceName $exePath "-token:$Token"

# Configure service settings
Write-Host "Configuring service settings..." -ForegroundColor Yellow
& $nssmPath set $serviceName AppDirectory (Split-Path $exePath)
& $nssmPath set $serviceName DisplayName "IPC MCP Server"
& $nssmPath set $serviceName Description "MCP Server for Windows IPC operations (Named Pipes, Memory-Mapped Files, COM, P/Invoke)"
& $nssmPath set $serviceName Start SERVICE_AUTO_START
& $nssmPath set $serviceName AppStdout (Join-Path $logsDir "stdout.log")
& $nssmPath set $serviceName AppStderr (Join-Path $logsDir "stderr.log")
& $nssmPath set $serviceName AppRotateFiles 1
& $nssmPath set $serviceName AppRotateOnline 1
& $nssmPath set $serviceName AppRotateSeconds 86400
& $nssmPath set $serviceName AppRotateBytes 10485760

# Set service to run as LocalSystem (has admin privileges)
Write-Host "Setting service to run as LocalSystem (highest privileges)..." -ForegroundColor Yellow
& $nssmPath set $serviceName ObjectName "LocalSystem"

Write-Host "Starting service..." -ForegroundColor Green
& $nssmPath start $serviceName

Start-Sleep -Seconds 2

# Verify service is running
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Write-Host "" -ForegroundColor Green
    Write-Host "Service installed and started successfully!" -ForegroundColor Green
    Write-Host "Service name: $serviceName" -ForegroundColor Cyan
    Write-Host "Service status: $($service.Status)" -ForegroundColor Cyan
    Write-Host "HTTP endpoint: http://localhost:23481" -ForegroundColor Cyan
    Write-Host "MCP endpoint: http://localhost:23481/mcp" -ForegroundColor Cyan
    Write-Host "Token: $Token" -ForegroundColor Cyan
    Write-Host "" -ForegroundColor Green
    Write-Host "Service Management Commands:" -ForegroundColor Cyan
    Write-Host "  Start:   nssm start $serviceName" -ForegroundColor White
    Write-Host "  Stop:    nssm stop $serviceName" -ForegroundColor White
    Write-Host "  Restart: nssm restart $serviceName" -ForegroundColor White
    Write-Host "  Remove:  nssm remove $serviceName confirm" -ForegroundColor White
    Write-Host "  Edit:    nssm edit $serviceName" -ForegroundColor White
    Write-Host "  Logs:    Get-Content $logsDir\stdout.log -Tail 50" -ForegroundColor White
}
else {
    Write-Host "WARNING: Service installed but may not be running. Check logs:" -ForegroundColor Yellow
    Write-Host "  $logsDir\stderr.log" -ForegroundColor Yellow
}

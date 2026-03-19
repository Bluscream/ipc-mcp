# Restart IpcMcp Service
$Token = "<TOKEN_PLACEHOLDER>"
$headers = @{ 
    "Content-Type" = "application/json"; 
    "Accept" = "application/json, text/event-stream";
    "Authorization" = "Bearer $Token" 
}
$stopData = @{ "jsonrpc" = "2.0"; "method" = "tools/call"; "params" = @{ "name" = "stop"; "arguments" = @{} }; "id" = 1 } | ConvertTo-Json;

write-host "--- IpcMcp RESTART SCRIPT ---" -ForegroundColor Cyan

write-host "Step 1: Requesting graceful stop via MCP API..." -ForegroundColor Yellow
try {
    $stopResult = Invoke-RestMethod -Uri "http://localhost:23481/mcp/" -Method Post -Headers $headers -Body $stopData -ErrorAction Stop
    $stopResult | ConvertTo-Json
} catch {
    write-host "Graceful stop failed or server already down: $($_.Exception.Message)" -ForegroundColor Gray
}

write-host "Step 2: Ensuring service is stopped via PowerShell..." -ForegroundColor Yellow
Stop-Service -Name "IpcMcp" -Force -ErrorAction SilentlyContinue

write-host "Step 3: Killing any remaining processes..." -ForegroundColor Yellow
Get-Process -Name "ipc-mcp", "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

write-host "Step 4: Waiting 1 second for cleanup..." -ForegroundColor Gray
Start-Sleep -Seconds 1

write-host "Step 5: Starting IpcMcp service via sudo (Elevation required)..." -ForegroundColor Green
# Check if sudo is available, otherwise fall back to Start-Process with -Verb RunAs
if (Get-Command sudo -ErrorAction SilentlyContinue) {
    sudo.exe net start IpcMcp
} else {
    Start-Process cmd.exe -ArgumentList "/c net start IpcMcp" -Verb RunAs
}

write-host "--- RESTART COMPLETE ---" -ForegroundColor Green

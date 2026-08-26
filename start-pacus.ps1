$ErrorActionPreference = "Stop"

$PacusRoot = "C:\Users\hecto\OneDrive\Desktop\pacus"
$BackendRoot = Join-Path $PacusRoot "backend"
$FrontendRoot = Join-Path $PacusRoot "frontend"

Write-Host "Iniciando PACUS..." -ForegroundColor Cyan

Start-Process powershell.exe -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command",
    "Set-Location '$BackendRoot'; .\scripts\start-api.ps1"
)

Start-Process powershell.exe -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$FrontendRoot'; dotnet serve -p 5500"
)

Start-Sleep -Seconds 2

Start-Process "http://localhost:5500"

Write-Host ""
Write-Host "PACUS iniciado." -ForegroundColor Green
Write-Host "Backend : http://localhost:5000"
Write-Host "Frontend: http://localhost:5500"

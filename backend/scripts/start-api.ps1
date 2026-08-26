$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

$env:ASPNETCORE_ENVIRONMENT = "Development"

$secrets = dotnet user-secrets list --project .\src\Pacus.Api

$env:JWT_SECRET = (
    $secrets |
    Where-Object { $_ -match '^JWT_SECRET\s*=' } |
    ForEach-Object { ($_ -split '=', 2)[1].Trim() }
)

$env:MONGODB_URI = (
    $secrets |
    Where-Object { $_ -match '^MONGODB_URI\s*=' } |
    ForEach-Object { ($_ -split '=', 2)[1].Trim() }
)

$env:MONGODB_DATABASE = (
    $secrets |
    Where-Object { $_ -match '^MONGODB_DATABASE\s*=' } |
    ForEach-Object { ($_ -split '=', 2)[1].Trim() }
)

if (-not $env:JWT_SECRET) {
    throw "JWT_SECRET nao encontrado nos User Secrets."
}

if (-not $env:MONGODB_URI) {
    throw "MONGODB_URI nao encontrado nos User Secrets."
}

if (-not $env:MONGODB_DATABASE) {
    $env:MONGODB_DATABASE = "pacus"
}

Write-Host ""
Write-Host "PACUS API"
Write-Host "JWT: OK"
Write-Host "MongoDB: OK"
Write-Host "Database: $env:MONGODB_DATABASE"
Write-Host ""

dotnet run --project .\src\Pacus.Api
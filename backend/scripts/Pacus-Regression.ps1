$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$ApiProcess = $null
$ApiStartedByRunner = $false

$Results = New-Object System.Collections.Generic.List[object]

# ============================================================
# CARREGAR .ENV
# ============================================================

$EnvFile = Join-Path $Root ".env"

if (Test-Path $EnvFile) {

    Write-Host "[INFO] Carregando $EnvFile"

    Get-Content $EnvFile | ForEach-Object {

        $line = $_.Trim()

        if (
            $line -and
            -not $line.StartsWith("#") -and
            $line.Contains("=")
        ) {

            $parts = $line.Split("=", 2)

            $name = $parts[0].Trim()
            $value = $parts[1].Trim()

            if (
                $value.StartsWith('"') -and
                $value.EndsWith('"')
            ) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            if (
                $value.StartsWith("'") -and
                $value.EndsWith("'")
            ) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            [Environment]::SetEnvironmentVariable(
                $name,
                $value,
                "Process"
            )
        }
    }
}
else {
    Write-Host "[WARN] .env não encontrado."
}

# ============================================================
# FUNÇÕES
# ============================================================

function Write-Section {
    param(
        [string]$Title
    )

    Write-Host ""
    Write-Host "========================================="
    Write-Host " $Title"
    Write-Host "========================================="
}

function Add-RegressionResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail = ""
    )

    $Results.Add(
        [PSCustomObject]@{
            Name   = $Name
            Passed = $Passed
            Detail = $Detail
        }
    )

    if ($Passed) {
        Write-Host "[PASS] $Name $Detail"
    }
    else {
        Write-Host "[FAIL] $Name $Detail"
    }
}

function Test-Environment {

    $required = @(
        "JWT_SECRET",
        "MONGODB_URI",
        "MONGODB_DATABASE"
    )

    foreach ($name in $required) {

        $value = [Environment]::GetEnvironmentVariable(
            $name,
            "Process"
        )

        if ([string]::IsNullOrWhiteSpace($value)) {

            throw "Variável obrigatória não configurada: $name"
        }

        Write-Host "[PASS] ENV $name configurada"
    }
}

function Test-ApiPort {

    try {

        $client = New-Object System.Net.Sockets.TcpClient

        $async = $client.BeginConnect(
            "127.0.0.1",
            5000,
            $null,
            $null
        )

        $connected = $async.AsyncWaitHandle.WaitOne(1500)

        if (-not $connected) {

            $client.Dispose()
            return $false
        }

        $client.EndConnect($async)
        $client.Dispose()

        return $true
    }
    catch {

        return $false
    }
}

function Start-ApiIfNeeded {

    if (Test-ApiPort) {

        Write-Host "[INFO] Porta 5000 já está disponível."
        Write-Host "[PASS] API disponível em http://localhost:5000"

        return
    }

    Write-Host "[INFO] API não está rodando. Iniciando..."

    $script:ApiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "run --project src\Pacus.Api" `
        -WorkingDirectory $Root `
        -PassThru `
        -NoNewWindow

    $script:ApiStartedByRunner = $true

    Write-Host "[INFO] Processo da API iniciado. PID: $($ApiProcess.Id)"

    $timeout = 60
    $elapsed = 0

    while (-not (Test-ApiPort) -and $elapsed -lt $timeout) {

        if ($ApiProcess.HasExited) {

            throw "A API encerrou durante a inicialização. ExitCode: $($ApiProcess.ExitCode)"
        }

        Start-Sleep -Seconds 1
        $elapsed++
    }

    if (-not (Test-ApiPort)) {

        throw "A API não abriu a porta 5000 em $timeout segundos."
    }

    Write-Host "[PASS] API disponível em http://localhost:5000"
}

function Stop-ApiIfNeeded {

    if (
        $ApiStartedByRunner -and
        $null -ne $ApiProcess
    ) {

        Write-Host "[INFO] Encerrando API iniciada pelo runner..."

        try {

            if (-not $ApiProcess.HasExited) {

                $ApiProcess.Kill($true)
                $ApiProcess.WaitForExit()
            }
        }
        catch {
        }
    }
}

# ============================================================
# PIPELINE
# ============================================================

try {

    # ----------------------------------------------------------
    # 1. UNIT TESTS
    # ----------------------------------------------------------

    Write-Section "1. UNIT TESTS"

    dotnet test `
        .\tests\Pacus.UnitTests\Pacus.UnitTests.csproj `
        --no-restore

    if ($LASTEXITCODE -ne 0) {

        throw "Unit Tests falharam. ExitCode: $LASTEXITCODE"
    }

    Add-RegressionResult `
        "Unit Tests" `
        $true

    # ----------------------------------------------------------
    # 2. INTEGRATION TESTS
    # ----------------------------------------------------------

    Write-Section "2. INTEGRATION TESTS"

    dotnet test `
        .\tests\Pacus.IntegrationTests\Pacus.IntegrationTests.csproj `
        --no-restore

    if ($LASTEXITCODE -ne 0) {

        throw "Integration Tests falharam. ExitCode: $LASTEXITCODE"
    }

    Add-RegressionResult `
        "Integration Tests" `
        $true

    # ----------------------------------------------------------
    # 3. API
    # ----------------------------------------------------------

    Write-Section "3. API"

    Test-Environment
    Start-ApiIfNeeded

    Add-RegressionResult `
        "API" `
        $true `
        "http://localhost:5000"

    # ----------------------------------------------------------
    # 4. E2E ORIGINAL
    # ----------------------------------------------------------

    Write-Section "4. E2E ORIGINAL"

    & .\scripts\Pacus-E2E.ps1

    if ($LASTEXITCODE -ne 0) {

        throw "E2E Original falhou. ExitCode: $LASTEXITCODE"
    }

    Add-RegressionResult `
        "E2E Original" `
        $true `
        "20 testes"

    # ----------------------------------------------------------
    # 5. E2E EXTENDED
    # ----------------------------------------------------------

    Write-Section "5. E2E EXTENDED"

    & .\scripts\Pacus-E2E-Extended.ps1

    if ($LASTEXITCODE -ne 0) {

        throw "E2E Extended falhou. ExitCode: $LASTEXITCODE"
    }

    Add-RegressionResult `
        "E2E Extended" `
        $true `
        "30 testes"
}
catch {

    Write-Host ""
    Write-Host "[ERROR] $($_.Exception.Message)"

    Add-RegressionResult `
        "Regression Pipeline" `
        $false `
        $_.Exception.Message
}
finally {

    Stop-ApiIfNeeded

    Write-Section "PACUS REGRESSION RESULT"

    foreach ($result in $Results) {

        if ($result.Passed) {

            Write-Host "[PASS] $($result.Name) $($result.Detail)"
        }
        else {

            Write-Host "[FAIL] $($result.Name) $($result.Detail)"
        }
    }

    $passed = @(
        $Results |
        Where-Object { $_.Passed }
    ).Count

    $failed = @(
        $Results |
        Where-Object { -not $_.Passed }
    ).Count

    Write-Host ""
    Write-Host "PASS: $passed"
    Write-Host "FAIL: $failed"
    Write-Host ""

    if ($failed -eq 0) {

        Write-Host "PACUS REGRESSION: TODOS OS TESTES PASSARAM."
        exit 0
    }

    Write-Host "PACUS REGRESSION: EXISTEM FALHAS."
    exit 1
}

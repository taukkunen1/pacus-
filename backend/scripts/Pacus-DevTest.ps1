param(
    [Parameter(Mandatory = $true)]
    [string]$TestFilter,

    [switch]$Diagnose,

    [switch]$AutoFix
)

$ErrorActionPreference = "Continue"

$Project = ".\tests\Pacus.IntegrationTests\Pacus.IntegrationTests.csproj"
$ArtifactDir = ".\artifacts"

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logFile = Join-Path $ArtifactDir "DevTest-$timestamp.log"

function Find-TestFile {
    param([string]$Filter)

    Get-ChildItem ".\tests" -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" } |
        ForEach-Object {
            $content = Get-Content $_.FullName -Raw

            if ($content -match [regex]::Escape($Filter)) {
                return $_.FullName
            }
        }

    return $null
}

function Fix-ChildLogin {
    param([string]$File)

    if (-not $File) {
        return $false
    }

    $content = Get-Content $File -Raw

    if (
        $content -match '/api/v1/auth/child/login' -and
        $content -match 'childLoginResponse' -and
        $content -notmatch 'userId\s*=\s*childUserId'
    ) {
        $pattern = '(?s)var childLoginResponse\s*=\s*await client\.PostAsJsonAsync\(\s*"/api/v1/auth/child/login".*?\);'

        $replacement = "var childLoginResponse = await client.PostAsJsonAsync(`r`n" +
            '    "/api/v1/auth/child/login",' + "`r`n" +
            "    new" + "`r`n" +
            "    {" + "`r`n" +
            "        userId = childUserId," + "`r`n" +
            "        pin = bootstrapRequest.childPin" + "`r`n" +
            "    });"

        $newContent = [regex]::Replace(
            $content,
            $pattern,
            $replacement,
            1
        )

        if ($newContent -ne $content) {
            Set-Content $File -Value $newContent -Encoding UTF8
            return $true
        }
    }

    return $false
}

Write-Host ""
Write-Host "========================================="
Write-Host " PACUS DEV TEST"
Write-Host "========================================="
Write-Host "[INFO] Teste: $TestFilter"
Write-Host "[INFO] Log:   $logFile"
Write-Host ""

$testFile = Find-TestFile $TestFilter

if ($testFile) {
    Write-Host "[INFO] Arquivo de teste: $testFile"
}
else {
    Write-Host "[WARN] Arquivo do teste não localizado."
}

if ($AutoFix -and $testFile) {
    if (Fix-ChildLogin $testFile) {
        Write-Host "[FIX] Login infantil corrigido automaticamente."
    }
}

Write-Host ""
Write-Host "[1/2] BUILD"

dotnet build $Project 2>&1 |
    Tee-Object -FilePath $logFile -Append

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Build"
    exit $LASTEXITCODE
}

Write-Host "[PASS] Build"

Write-Host ""
Write-Host "[2/2] TEST"

$testArgs = @(
    "test",
    $Project,
    "--filter",
    "FullyQualifiedName~$TestFilter",
    "--no-build"
)

if ($Diagnose) {
    $testArgs += "--logger"
    $testArgs += "console;verbosity=detailed"
}

& dotnet @testArgs 2>&1 |
    Tee-Object -FilePath $logFile -Append

$exitCode = $LASTEXITCODE

Write-Host ""

if ($exitCode -eq 0) {
    Write-Host "[PASS] $TestFilter"
    Write-Host ""
    Write-Host "========================================="
    Write-Host " DEV TEST: PASS"
    Write-Host "========================================="
    exit 0
}

Write-Host "[FAIL] $TestFilter"

if ($Diagnose) {
    Write-Host ""
    Write-Host "[DIAGNOSTIC] Últimas linhas:"
    Get-Content $logFile -Tail 50
}

Write-Host ""
Write-Host "========================================="
Write-Host " DEV TEST: FAIL"
Write-Host "========================================="

exit $exitCode

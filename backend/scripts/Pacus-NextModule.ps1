$ErrorActionPreference = "Stop"

$controllers = Get-ChildItem ".\src\Pacus.Api\Controllers" -File -Filter "*.cs" |
    Where-Object { $_.Name -notlike "*Base*" } |
    ForEach-Object {
        [PSCustomObject]@{
            Controller = $_.BaseName -replace "Controller$",""
            File = $_.FullName
        }
    }

$tests = Get-ChildItem ".\tests" -Recurse -File -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" } |
    ForEach-Object {
        [PSCustomObject]@{
            Name = $_.BaseName
            File = $_.FullName
        }
    }

Write-Host ""
Write-Host "========================================="
Write-Host " PACUS NEXT MODULE AUDIT"
Write-Host "========================================="
Write-Host ""

$result = foreach ($controller in $controllers) {

    $controllerName = $controller.Controller

    $matchingTests = $tests |
        Where-Object {
            $_.Name -match [regex]::Escape($controllerName) -or
            (
                $controllerName -eq "DailyRoutine" -and
                $_.Name -match "DailyRoutine"
            )
        }

    [PSCustomObject]@{
        Controller = $controllerName
        Tests = if ($matchingTests) {
            ($matchingTests.Name -join ", ")
        }
        else {
            "SEM TESTE DEDICADO"
        }
        Covered = [bool]$matchingTests
    }
}

$result |
    Sort-Object Covered, Controller |
    Format-Table -AutoSize

Write-Host ""
Write-Host "========================================="
Write-Host " PRÓXIMO CANDIDATO"
Write-Host "========================================="

$candidate = $result |
    Where-Object { -not $_.Covered } |
    Select-Object -First 1

if ($candidate) {
    Write-Host ""
    Write-Host "[NEXT] $($candidate.Controller)"
    Write-Host "[INFO] Controller sem teste HTTP dedicado."
}
else {
    Write-Host ""
    Write-Host "[INFO] Todos os controllers possuem algum teste correspondente."
    Write-Host "[INFO] Próxima auditoria deve ser por cobertura de endpoints/métodos."
}

Write-Host ""

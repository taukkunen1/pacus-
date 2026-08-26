$ErrorActionPreference = "Stop"

$BaseUrl = "http://localhost:5000"
$Results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail = ""
    )

    $Results.Add([PSCustomObject]@{
        Status = $Status
        Test   = $Name
        Detail = $Detail
    })

    $symbol = switch ($Status) {
        "PASS" { "[PASS]" }
        "FAIL" { "[FAIL]" }
        "WARN" { "[WARN]" }
        default { "[----]" }
    }

    Write-Host "$symbol $Name $Detail"
}

function Invoke-Api {
    param(
        [ValidateSet("GET","POST","PUT","DELETE")]
        [string]$Method,

        [string]$Path,

        [string]$Token = "",

        [object]$Body = $null
    )

    $headers = @{
        Accept = "*/*"
    }

    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $jsonBody = $null

    if ($null -ne $Body) {
        $headers["Content-Type"] = "application/json"
        $jsonBody = $Body | ConvertTo-Json -Depth 20 -Compress
    }

    try {
        $response = Invoke-WebRequest `
            -Uri "$BaseUrl$Path" `
            -Method $Method `
            -Headers $headers `
            -Body $jsonBody `
            -UseBasicParsing

        $content = $null

        if ($response.Content) {
            try {
                $content = $response.Content | ConvertFrom-Json
            }
            catch {
                $content = $response.Content
            }
        }

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Body       = $content
            Raw        = $response.Content
        }
    }
    catch {
        $statusCode = 0
        $raw = ""

        if ($_.Exception.Response) {
            try {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
            catch {}

            try {
                $reader = New-Object System.IO.StreamReader(
                    $_.Exception.Response.GetResponseStream()
                )
                $raw = $reader.ReadToEnd()
                $reader.Dispose()
            }
            catch {}
        }

        $body = $null

        if ($raw) {
            try {
                $body = $raw | ConvertFrom-Json
            }
            catch {
                $body = $raw
            }
        }

        return [PSCustomObject]@{
            StatusCode = $statusCode
            Body       = $body
            Raw        = $raw
        }
    }
}

Write-Host ""
Write-Host "========================================="
Write-Host "       PACUS E2E EXTENDED TEST SUITE"
Write-Host "========================================="
Write-Host ""

# ============================================================
# 1. HEALTH
# ============================================================

$r = Invoke-Api -Method GET -Path "/api/v1/health"

if ($r.StatusCode -eq 200 -and $r.Body.database -eq "connected") {
    Add-Result "Health / MongoDB" "PASS"
}
else {
    Add-Result "Health / MongoDB" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 2. BOOTSTRAP
# ============================================================

$stamp = Get-Date -Format "yyyyMMddHHmmssfff"
$testEmail = "pacus.e2e.$stamp@gmail.com"

$bootstrapBody = @{
    adultName     = "PACUS E2E Adult"
    adultEmail    = $testEmail
    adultPassword = "E2E150402"
    childName     = "PACUS E2E Child"
    childPin      = "1234"
}

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/bootstrap" `
    -Body $bootstrapBody

if (($r.StatusCode -eq 200 -or $r.StatusCode -eq 201) -and $r.Body.familyId) {
    Add-Result "Bootstrap" "PASS"
}
else {
    Add-Result "Bootstrap" "FAIL" "HTTP $($r.StatusCode) $($r.Raw)"
    exit 1
}

$AdultUserId = $r.Body.adultUserId
$ChildUserId = $r.Body.childUserId
$FamilyId    = $r.Body.familyId
$PacusId     = $r.Body.pacusId

# ============================================================
# 3. ADULT LOGIN
# ============================================================

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/auth/adult/login" `
    -Body @{
        email    = $testEmail
        password = "E2E150402"
    }

if ($r.StatusCode -eq 200 -and $r.Body.token) {
    $AdultToken = $r.Body.token
    Add-Result "Adult Login" "PASS"
}
else {
    Add-Result "Adult Login" "FAIL" "HTTP $($r.StatusCode) $($r.Raw)"
    exit 1
}

# ============================================================
# 4. ADULT -> PACUS
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/pacus/me" `
    -Token $AdultToken

if ($r.StatusCode -eq 200 -and $r.Body.name -eq "Pacus") {
    Add-Result "Adult -> Pacus" "PASS"
}
else {
    Add-Result "Adult -> Pacus" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 5. PACUS STATE
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/pacus/me/state" `
    -Token $AdultToken

if ($r.StatusCode -eq 200) {
    Add-Result "Pacus State" "PASS"
}
else {
    Add-Result "Pacus State" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 6. DAILY ROUTINE
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $AdultToken

if ($r.StatusCode -eq 200 -and $r.Body.date) {
    Add-Result "Daily Routine Today" "PASS"
}
else {
    Add-Result "Daily Routine Today" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 7. CREATE TASK TEMPLATES
# ============================================================

$taskRequests = @(
    @{
        title       = "E2E Escovar os dentes"
        description = "Tarefa E2E"
        type        = "mandatory"
        period      = "morning"
        points      = 1
    },
    @{
        title       = "E2E Tomar banho"
        description = "Tarefa E2E"
        type        = "mandatory"
        period      = "morning"
        points      = 2
    },
    @{
        title       = "E2E Arrumar a cama"
        description = "Tarefa E2E"
        type        = "mandatory"
        period      = "morning"
        points      = 1
    }
)

$createdTemplates = New-Object System.Collections.Generic.List[object]

foreach ($taskBody in $taskRequests) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/tasks" `
        -Token $AdultToken `
        -Body $taskBody

    if (($r.StatusCode -eq 200 -or $r.StatusCode -eq 201) -and $r.Body.id) {
        $createdTemplates.Add($r.Body)
        Add-Result "Create Task: $($taskBody.title)" "PASS"
    }
    else {
        Add-Result "Create Task: $($taskBody.title)" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 8. LIST TASK TEMPLATES
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/tasks" `
    -Token $AdultToken

if ($r.StatusCode -eq 200 -and $r.Body.Count -ge 3) {
    Add-Result "List Task Templates" "PASS"
}
else {
    Add-Result "List Task Templates" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 9. SYNC DAILY ROUTINE
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $AdultToken

$dailyTasks = @()

if ($r.StatusCode -eq 200) {
    $dailyTasks = @(
        $r.Body.tasks |
        Where-Object { $_.title -like "E2E *" }
    )
}

if ($dailyTasks.Count -ge 3) {
    Add-Result "Sync Templates -> Daily Routine" "PASS"
}
else {
    Add-Result "Sync Templates -> Daily Routine" "FAIL" "Encontradas $($dailyTasks.Count) tarefas E2E"
}

# ============================================================
# 10. COMPLETE TASK
# ============================================================

$targetTask = $dailyTasks | Select-Object -First 1

if ($targetTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($targetTask.id)/complete" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200 -and $r.Body.tasks) {

        $completed = $r.Body.tasks |
            Where-Object { $_.id -eq $targetTask.id }

        if ($completed.status -eq "done") {
            Add-Result "Complete Task" "PASS"
        }
        else {
            Add-Result "Complete Task" "FAIL" "Status inesperado"
        }
    }
    else {
        Add-Result "Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}
else {
    Add-Result "Complete Task" "FAIL" "Nenhuma tarefa E2E encontrada"
}

# ============================================================
# 11. POINTS BALANCE
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/points" `
    -Token $AdultToken

if ($r.StatusCode -eq 200 -and $r.Body.balance -ge 1) {
    Add-Result "Points Balance" "PASS"
}
else {
    Add-Result "Points Balance" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 12. POINT TRANSACTIONS
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/points/transactions" `
    -Token $AdultToken

if ($r.StatusCode -eq 200 -and $r.Body.Count -ge 1) {
    Add-Result "Point Transactions" "PASS"
}
else {
    Add-Result "Point Transactions" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 13. REOPEN TASK
# ============================================================

if ($targetTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($targetTask.id)/reopen" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {

        $reopened = $r.Body.tasks |
            Where-Object { $_.id -eq $targetTask.id }

        if ($reopened.status -eq "pending") {
            Add-Result "Reopen Task" "PASS"
        }
        else {
            Add-Result "Reopen Task" "FAIL"
        }
    }
    else {
        Add-Result "Reopen Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 14. HISTORY
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/history" `
    -Token $AdultToken

if ($r.StatusCode -eq 200) {
    Add-Result "History Endpoint" "PASS"
}
else {
    Add-Result "History Endpoint" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 15. CHILD LOGIN
# ============================================================

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/auth/child/login" `
    -Body @{
        userId = $ChildUserId
        pin    = "1234"
    }

if ($r.StatusCode -eq 200 -and $r.Body.token) {
    $ChildToken = $r.Body.token
    Add-Result "Child Login" "PASS"
}
else {
    Add-Result "Child Login" "FAIL" "HTTP $($r.StatusCode) $($r.Raw)"
}

# ============================================================
# 16. CHILD -> PACUS
# ============================================================

if ($ChildToken) {

    $r = Invoke-Api `
        -Method GET `
        -Path "/api/v1/pacus/me" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child -> Pacus" "PASS"
    }
    else {
        Add-Result "Child -> Pacus" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 17. CHILD -> DAILY ROUTINE
# ============================================================

if ($ChildToken) {

    $r = Invoke-Api `
        -Method GET `
        -Path "/api/v1/daily-routines/today" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child -> Daily Routine" "PASS"
    }
    else {
        Add-Result "Child -> Daily Routine" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 18. STORE ITEMS
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/store/items" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {
    Add-Result "Store Items" "PASS"
}
else {
    Add-Result "Store Items" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 19. CHILD CREATE AD-HOC TASK
# ============================================================

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Tarefa Infantil"
        description = "Criada pelo perfil infantil"
        type        = "expected"
        period      = "evening"
        points      = 1
    }

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {

    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -Last 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode) $($childAdHoc.Raw)"
}

# ============================================================
# 20. CHILD EDIT AD-HOC TASK
# ============================================================

if ($childTask) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/daily-tasks/$($childTask.id)" `
        -Token $ChildToken `
        -Body @{
            title       = "E2E Tarefa Infantil Editada"
            description = "Descricao editada"
            type        = "expected"
            period      = "evening"
            points      = 1
        }

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Edit Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Edit Ad-Hoc Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 21. ADULT ADJUST DAILY TASK POINTS
# ============================================================

if ($childTask) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/daily-tasks/$($childTask.id)/points" `
        -Token $AdultToken `
        -Body @{
            points = 2
        }

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Adjust Daily Task Points" "PASS"
    }
    else {
        Add-Result "Adult Adjust Daily Task Points" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 22. CHILD COMPLETE AD-HOC TASK
# ============================================================

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Complete Ad-Hoc Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 23. ADULT ADJUST COMPLETED TASK POINTS
# ============================================================

if ($childTask) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/daily-tasks/$($childTask.id)/points" `
        -Token $AdultToken `
        -Body @{
            points = 3
        }

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Adjust Completed Task Points" "PASS"
    }
    else {
        Add-Result "Adult Adjust Completed Task Points" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ============================================================
# 24. CHILD REORDER TASKS
# ============================================================

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $reorderIds = @(
        $r.Body.tasks |
        Where-Object { $null -eq $_.deletedAt } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($reorderIds.Count -ge 2) {

        [array]::Reverse($reorderIds)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $reorderIds

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Poucas tarefas"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 25. CHILD BLOCKED FROM TEMPLATE ADMIN
# ============================================================

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "Unauthorized Template"
        description = "Nao deveria criar template"
        type        = "mandatory"
        period      = "morning"
        points      = 1
    }

if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
    Add-Result "Child Blocked From Template Admin" "PASS"
}
else {
    Add-Result "Child Blocked From Template Admin" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# 26. ADULT CREATE STORE ITEM
# ============================================================

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body @{
        title       = "E2E Recompensa"
        description = "Recompensa E2E"
        cost        = 1
        category    = "toy"
        icon        = "gift"
        stock       = 2
    }

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {

    $storeItemId = [string]$storeCreate.Body.id

    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode) $($storeCreate.Raw)"
}

# ============================================================
# 27. CHILD REQUEST REDEMPTION
# ============================================================

if ($storeItemId) {

    $redemptionBody = @{
        storeItemId = [string]$storeItemId
    }

    Write-Host ""
    Write-Host "REDEMPTION BODY:"
    Write-Host ($redemptionBody | ConvertTo-Json -Compress)

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body $redemptionBody

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {

        $redemptionId = [string]$redemption.Body.id

        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode) $($redemption.Raw)"
    }
}
else {
    Add-Result "Child Request Redemption" "FAIL" "StoreItemId vazio"
}

# ============================================================
# 28. ADULT UPDATE PACUS STATE
# ============================================================

$r = Invoke-Api `
    -Method PUT `
    -Path "/api/v1/pacus/me/state" `
    -Token $AdultToken

if ($r.StatusCode -eq 204) {
    Add-Result "Adult Update Pacus State" "PASS"
}
else {
    Add-Result "Adult Update Pacus State" "FAIL" "HTTP $($r.StatusCode)"
}

# ============================================================
# SUMMARY
# ============================================================

Write-Host ""
Write-Host "========================================="
Write-Host "              RESULTADO"
Write-Host "========================================="

$Results | Format-Table -AutoSize

$pass = @($Results | Where-Object Status -eq "PASS").Count
$fail = @($Results | Where-Object Status -eq "FAIL").Count
$warn = @($Results | Where-Object Status -eq "WARN").Count

Write-Host ""
Write-Host "PASS: $pass"
Write-Host "FAIL: $fail"
Write-Host "WARN: $warn"

if ($fail -gt 0) {

    Write-Host ""
    Write-Host "FALHAS:"

    $Results |
        Where-Object Status -eq "FAIL" |
        Format-Table -AutoSize

    exit 1
}

Write-Host ""
Write-Host "PACUS E2E EXTENDED: TODOS OS TESTES EXECUTADOS."
exit 0
$file = ".\scripts\Pacus-E2E.ps1"
$text = Get-Content $file -Raw

$marker = '# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

# ------------------------------------------------------------
# SUMMARY
# ------------------------------------------------------------'

$extra = @'
# ------------------------------------------------------------
# 20. CHILD CREATE AD-HOC TASK
# ------------------------------------------------------------

$childAdHocBody = @{
    title       = "E2E Tarefa Infantil"
    description = "Criada pelo perfil infantil"
    type        = "expected"
    period      = "night"
    points      = 3
}

$childAdHoc = Invoke-Api `
    -Method POST `
    -Path "/api/v1/daily-tasks" `
    -Token $ChildToken `
    -Body $childAdHocBody

if ($childAdHoc.StatusCode -eq 200 -and $childAdHoc.Body.tasks) {
    $childTask = $childAdHoc.Body.tasks |
        Where-Object { $_.title -eq "E2E Tarefa Infantil" } |
        Select-Object -First 1

    if ($childTask) {
        Add-Result "Child Create Ad-Hoc Task" "PASS"
    }
    else {
        Add-Result "Child Create Ad-Hoc Task" "FAIL" "Tarefa nao encontrada na resposta"
    }
}
else {
    Add-Result "Child Create Ad-Hoc Task" "FAIL" "HTTP $($childAdHoc.StatusCode)"
}

# ------------------------------------------------------------
# 21. CHILD COMPLETE OWN TASK
# ------------------------------------------------------------

if ($childTask) {

    $r = Invoke-Api `
        -Method POST `
        -Path "/api/v1/daily-tasks/$($childTask.id)/complete" `
        -Token $ChildToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Child Complete Task" "PASS"
    }
    else {
        Add-Result "Child Complete Task" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 22. CHILD REORDER TASKS
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method GET `
    -Path "/api/v1/daily-routines/today" `
    -Token $ChildToken

if ($r.StatusCode -eq 200) {

    $ids = @(
        $r.Body.tasks |
        Where-Object { $_.deletedAt -eq $null } |
        Sort-Object order |
        Select-Object -ExpandProperty id
    )

    if ($ids.Count -ge 2) {

        [array]::Reverse($ids)

        $r2 = Invoke-Api `
            -Method PUT `
            -Path "/api/v1/daily-routines/today/order" `
            -Token $ChildToken `
            -Body $ids

        if ($r2.StatusCode -eq 200) {
            Add-Result "Child Reorder Tasks" "PASS"
        }
        else {
            Add-Result "Child Reorder Tasks" "FAIL" "HTTP $($r2.StatusCode)"
        }
    }
    else {
        Add-Result "Child Reorder Tasks" "WARN" "Menos de 2 tarefas disponiveis"
    }
}
else {
    Add-Result "Child Reorder Tasks" "FAIL" "Falha ao obter rotina"
}

# ------------------------------------------------------------
# 23. CHILD CANNOT USE ADULT TASK TEMPLATE ADMIN
# ------------------------------------------------------------

$r = Invoke-Api `
    -Method POST `
    -Path "/api/v1/tasks" `
    -Token $ChildToken `
    -Body @{
        title       = "E2E Unauthorized Template"
        description = "Nao deveria ser criado"
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

# ------------------------------------------------------------
# 24. ADULT CREATE STORE ITEM
# ------------------------------------------------------------

$storeBody = @{
    title       = "E2E Recompensa"
    description = "Recompensa de teste"
    cost        = 10
    category    = "toy"
    icon        = "gift"
    stock       = 1
}

$storeCreate = Invoke-Api `
    -Method POST `
    -Path "/api/v1/store/items" `
    -Token $AdultToken `
    -Body $storeBody

if (($storeCreate.StatusCode -eq 200 -or $storeCreate.StatusCode -eq 201) -and $storeCreate.Body.id) {
    $storeItemId = $storeCreate.Body.id
    Add-Result "Adult Create Store Item" "PASS"
}
else {
    Add-Result "Adult Create Store Item" "FAIL" "HTTP $($storeCreate.StatusCode)"
}

# ------------------------------------------------------------
# 25. CHILD REQUEST REDEMPTION
# ------------------------------------------------------------

if ($storeItemId) {

    $redemption = Invoke-Api `
        -Method POST `
        -Path "/api/v1/store/redemptions" `
        -Token $ChildToken `
        -Body @{
            storeItemId = $storeItemId
        }

    if (($redemption.StatusCode -eq 200 -or $redemption.StatusCode -eq 201) -and $redemption.Body.id) {
        $redemptionId = $redemption.Body.id
        Add-Result "Child Request Redemption" "PASS"
    }
    else {
        Add-Result "Child Request Redemption" "FAIL" "HTTP $($redemption.StatusCode)"
    }
}

# ------------------------------------------------------------
# 26. CHILD CANNOT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $ChildToken

    if ($r.StatusCode -eq 401 -or $r.StatusCode -eq 403) {
        Add-Result "Child Blocked From Approve Redemption" "PASS"
    }
    else {
        Add-Result "Child Blocked From Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 27. ADULT APPROVE REDEMPTION
# ------------------------------------------------------------

if ($redemptionId) {

    $r = Invoke-Api `
        -Method PUT `
        -Path "/api/v1/store/redemptions/$redemptionId/approve" `
        -Token $AdultToken

    if ($r.StatusCode -eq 200) {
        Add-Result "Adult Approve Redemption" "PASS"
    }
    else {
        Add-Result "Adult Approve Redemption" "FAIL" "HTTP $($r.StatusCode)"
    }
}

# ------------------------------------------------------------
# 28. PACUS STATE UPDATE
# ------------------------------------------------------------

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

'@

if (-not $text.Contains($marker)) {
    Write-Host "Marcador do SUMMARY nao encontrado. Nenhuma alteracao feita."
    exit 1
}

$text = $text.Replace($marker, $extra + "`r`n" + $marker)

[System.IO.File]::WriteAllText(
    (Resolve-Path $file),
    $text,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host "Pacus-E2E.ps1 ampliado com sucesso."
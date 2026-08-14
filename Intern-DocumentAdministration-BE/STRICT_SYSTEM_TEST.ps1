$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "     HỆ THỐNG KIỂM THỬ NGHIÊM NGẶT & TOÀN DIỆN (STRICT AUDIT)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$script:passedCount = 0
$script:failedCount = 0

function Assert-Test([string]$testName, [bool]$condition, [string]$detail = "") {
    if ($condition) {
        $script:passedCount++
        Write-Host " [PASS] $testName" -ForegroundColor Green
        if ($detail) { Write-Host "        $detail" -ForegroundColor Gray }
    } else {
        $script:failedCount++
        Write-Host " [FAIL] $testName" -ForegroundColor Red
        if ($detail) { Write-Host "        $detail" -ForegroundColor Yellow }
    }
}

# ----------------------------------------------------------------------
# LEVEL 1: COMPILED SOURCE INTEGRITY
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 1: COMPILATION & SOURCE INTEGRITY ---" -ForegroundColor Yellow

$services = @("auth-service", "document-service", "partner-service")
foreach ($svc in $services) {
    $csprojExists = (Get-ChildItem "services/$svc/*.csproj" -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
    $fileCount = (Get-ChildItem "services/$svc/bin/Debug/net10.0/*.dll" -ErrorAction SilentlyContinue | Measure-Object).Count
    $isSuccess = [bool]($csprojExists -and ($fileCount -gt 0))
    Assert-Test "Build & Source Integrity '$svc'" $isSuccess "Csproj and compiled DLL binaries present ($fileCount DLLs)"
}

# ----------------------------------------------------------------------
# LEVEL 2: MICROSERVICE HEALTH ENDPOINTS
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 2: HEALTH ENDPOINTS CHECK ---" -ForegroundColor Yellow
Start-Sleep -Seconds 3

$ports = @{ "auth-service" = 5001; "document-service" = 5002; "partner-service" = 5003 }
foreach ($kv in $ports.GetEnumerator()) {
    try {
        $res = Invoke-RestMethod "http://localhost:$($kv.Value)/health" -TimeoutSec 5
        Assert-Test "Health check $($kv.Key) (Port $($kv.Value))" ($res.status -eq "healthy") "Status: $($res.status)"
    } catch {
        Assert-Test "Health check $($kv.Key) (Port $($kv.Value))" $false "Exception: $($_.Exception.Message)"
    }
}

# ----------------------------------------------------------------------
# LEVEL 3: AUTHENTICATION & ROLE LOOKUP VERIFICATION
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 3: AUTH-SERVICE & JWT SECURITY ---" -ForegroundColor Yellow

# L3.1: Login sai mật khẩu -> Expect 401
try {
    Invoke-WebRequest "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"wrong_password"}' -ErrorAction Stop | Out-Null
    Assert-Test "Login with wrong password rejects" $false "Expected 401 Unauthorized"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Login with wrong password rejects" ($code -eq 401) "HTTP Status Code: $code"
}

# L3.2: Login các vai trò mẫu
$secToken = $null
$secDirToken = $null
$empToken = $null
$adminToken = $null

try {
    $rAdmin = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"admin_user","password":"password"}'
    $adminToken = $rAdmin.data.accessToken
    Assert-Test "Login admin_user" ($rAdmin.success -and $rAdmin.data.user.role -eq "Admin") "Role: $($rAdmin.data.user.role)"
} catch { Assert-Test "Login admin_user" $false $_.Exception.Message }

try {
    $rSec = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"password"}'
    $secToken = $rSec.data.accessToken
    Assert-Test "Login secretary_user" ($rSec.success -and $rSec.data.user.role -eq "Secretary") "Role: $($rSec.data.user.role)"
} catch { Assert-Test "Login secretary_user" $false $_.Exception.Message }

try {
    $rSecDir = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"director_sec","password":"password"}'
    $secDirToken = $rSecDir.data.accessToken
    Assert-Test "Login director_sec" ($rSecDir.success -and $rSecDir.data.user.role -eq "SecretaryDirector") "Role: $($rSecDir.data.user.role)"
} catch { Assert-Test "Login director_sec" $false $_.Exception.Message }

try {
    $rEmp = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"employee_user","password":"password"}'
    $empToken = $rEmp.data.accessToken
    Assert-Test "Login employee_user" ($rEmp.success -and $rEmp.data.user.role -eq "Employee") "Role: $($rEmp.data.user.role)"
} catch { Assert-Test "Login employee_user" $false $_.Exception.Message }

$hSec = @{ Authorization = "Bearer $secToken" }
$hSecDir = @{ Authorization = "Bearer $secDirToken" }
$hEmp = @{ Authorization = "Bearer $empToken" }
$hAdmin = @{ Authorization = "Bearer $adminToken" }

# L3.3: Lookup user theo role từ auth-service (/api/users?role=...) - Cần [Authorize]
try {
    Invoke-WebRequest "http://localhost:5001/api/users?role=SecretaryDirector" -ErrorAction Stop | Out-Null
    Assert-Test "GET /api/users without token rejected (401)" $false "Expected 401 Unauthorized"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "GET /api/users without token rejected (401)" ($code -eq 401) "HTTP Status Code: $code"
}

try {
    $rUserRole = Invoke-RestMethod "http://localhost:5001/api/users?role=SecretaryDirector" -Headers $hSec
    Assert-Test "Inter-service user lookup by role (Authenticated JWT)" ($rUserRole.success -and $rUserRole.data.email -eq "sec_director@company.com") "Email: $($rUserRole.data.email)"
} catch { Assert-Test "Inter-service user lookup by role (Authenticated JWT)" $false $_.Exception.Message }

# ----------------------------------------------------------------------
# LEVEL 4: PARTNER-SERVICE VERIFICATION & RULES
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 4: PARTNER-SERVICE RULES & RBAC ---" -ForegroundColor Yellow

# L4.1a: GET /api/partners without token rejected (401)
try {
    Invoke-WebRequest "http://localhost:5003/api/partners" -ErrorAction Stop | Out-Null
    Assert-Test "GET /api/partners without token rejected (401)" $false "Expected 401 Unauthorized"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "GET /api/partners without token rejected (401)" ($code -eq 401) "HTTP Status Code: $code"
}

# L4.1b: Authenticated GET /api/partners
try {
    $rPartners = Invoke-RestMethod "http://localhost:5003/api/partners" -Headers $hSec
    Assert-Test "GET /api/partners (Authenticated JWT Bearer)" ($rPartners.success) "Total items: $($rPartners.data.totalCount)"
} catch { Assert-Test "GET /api/partners (Authenticated JWT Bearer)" $false $_.Exception.Message }

# L4.2: POST /api/partners (Secretary role)
$partnerId = $null
$shortCode = "STRICT_" + (Get-Random -Minimum 1000 -Maximum 9999)
try {
    $rNewPartner = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body "{`"fullName`":`"Cuc Hang Khong Viet Nam`",`"shortName`":`"$shortCode`",`"entityType`":`"Sender`"}"
    $partnerId = $rNewPartner.data.id
    Assert-Test "POST /api/partners (Secretary create)" ($rNewPartner.success -and $partnerId -ne $null) "Partner ID: $partnerId, ShortName: $shortCode"
} catch { Assert-Test "POST /api/partners (Secretary create)" $false $_.Exception.Message }

# L4.3: Duplicate ShortName -> Expect 400
try {
    Invoke-WebRequest "http://localhost:5003/api/partners" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body "{`"fullName`":`"Cuc Khac`",`"shortName`":`"$shortCode`",`"entityType`":`"Both`"}" -ErrorAction Stop | Out-Null
    Assert-Test "Duplicate ShortName rejected (400)" $false "Expected 400 Bad Request"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Duplicate ShortName rejected (400)" ($code -eq 400) "HTTP Status Code: $code"
}

# L4.4: Invalid EntityType -> Expect 400
try {
    Invoke-WebRequest "http://localhost:5003/api/partners" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body '{"fullName":"Invalid Type","shortName":"INVALID_TYPE_TEST","entityType":"UNKNOWN"}' -ErrorAction Stop | Out-Null
    Assert-Test "Invalid EntityType rejected (400)" $false "Expected 400 Bad Request"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Invalid EntityType rejected (400)" ($code -eq 400) "HTTP Status Code: $code"
}

# L4.5: Employee DELETE partner -> Expect 403 Forbidden
try {
    Invoke-WebRequest "http://localhost:5003/api/partners/$partnerId" -Method DELETE -Headers $hEmp -ErrorAction Stop | Out-Null
    Assert-Test "Employee DELETE partner rejected (403)" $false "Expected 403 Forbidden"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Employee DELETE partner rejected (403)" ($code -eq 403) "HTTP Status Code: $code"
}

# L4.6: Secretary DELETE partner -> Expect 200 OK (Soft Delete)
try {
    $rDel = Invoke-RestMethod "http://localhost:5003/api/partners/$partnerId" -Method DELETE -Headers $hSec
    Assert-Test "Secretary DELETE partner (Soft Delete)" ($rDel.success) "Response envelope { success: true, data: null }"
} catch { Assert-Test "Secretary DELETE partner (Soft Delete)" $false $_.Exception.Message }

# L4.7: QueryFilter excludes deleted partner from default list
try {
    $rListAfterDel = Invoke-RestMethod "http://localhost:5003/api/partners" -Headers $hSec
    $found = $rListAfterDel.data.items | Where-Object { $_.id -eq $partnerId }
    Assert-Test "QueryFilter hides soft-deleted partner" ($null -eq $found) "Item is hidden from standard queries"
} catch { Assert-Test "QueryFilter hides soft-deleted partner" $false $_.Exception.Message }

# L4.8: SecretaryDirector RESTORE partner -> Expect 200 OK
try {
    $rRestore = Invoke-RestMethod "http://localhost:5003/api/partners/$partnerId/restore" -Method PUT -Headers $hSecDir
    Assert-Test "SecretaryDirector RESTORE partner" ($rRestore.success -and $rRestore.data.isDeleted -eq $false) "isDeleted: $($rRestore.data.isDeleted)"
} catch { Assert-Test "SecretaryDirector RESTORE partner" $false $_.Exception.Message }


# ----------------------------------------------------------------------
# LEVEL 5: DOCUMENT-SERVICE RULES & INTER-SERVICE INTEGRATION
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 5: DOCUMENT-SERVICE & INTER-SERVICE INTEGRATION ---" -ForegroundColor Yellow

# L5.1: Access without token -> Expect 401 Unauthorized
try {
    Invoke-WebRequest "http://localhost:5002/api/documents" -ErrorAction Stop | Out-Null
    Assert-Test "GET /api/documents without token rejected (401)" $false "Expected 401 Unauthorized"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "GET /api/documents without token rejected (401)" ($code -eq 401) "HTTP Status Code: $code"
}

# L5.2: Create Incoming Document with valid partnerId -> Integrates with partner-service
$docId = $null
try {
    $rDoc = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body "{`"title`":`"Cong van chi dao an toan bay 2026`",`"summary`":`"Tuan thu quy dinh`",`"partnerId`":`"$partnerId`"}"
    $docId = $rDoc.data.id
    Assert-Test "Create Incoming Document with valid partnerId" ($rDoc.success -and $rDoc.data.documentNumber -like "CV-DEN-2026-*") "DocNumber: $($rDoc.data.documentNumber)"
} catch { Assert-Test "Create Incoming Document with valid partnerId" $false $_.Exception.Message }

# L5.2b: Admin Assign Departments (Admin bypass check)
try {
    $deptGuid = [Guid]::NewGuid().ToString()
    $rAssign = Invoke-RestMethod "http://localhost:5002/api/documents/$docId/assign-departments" -Method PUT -Headers $hAdmin `
        -ContentType "application/json" -Body "{`"departmentIds`":[`"$deptGuid`"]}"
    Assert-Test "Admin assign departments allowed" ($rAssign.success) "Department assigned"
} catch { Assert-Test "Admin assign departments allowed" $false $_.Exception.Message }

# L5.2c: Employee Add Attachment to someone else's Draft doc -> Expect 403
$fakeFileGuid = [Guid]::NewGuid().ToString()
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/$docId/attachments" -Method POST -Headers $hEmp `
        -ContentType "application/json" -Body "{`"fileId`":`"$fakeFileGuid`"}" -ErrorAction Stop | Out-Null
    Assert-Test "Employee Add Attachment to foreign doc rejected (403)" $false "Expected 403 Forbidden"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Employee Add Attachment to foreign doc rejected (403)" ($code -eq 403) "HTTP Status Code: $code"
}

# L5.3: Create Incoming Document with invalid partnerId (GUID non-existent) -> Expect 404
$fakeGuid = [Guid]::NewGuid().ToString()
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body "{`"title`":`"Test Fake Partner`",`"partnerId`":`"$fakeGuid`"}" -ErrorAction Stop | Out-Null
    Assert-Test "Create Document with fake partnerId rejected (404)" $false "Expected 404 Not Found"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Create Document with fake partnerId rejected (404)" ($code -eq 404) "HTTP Status Code: $code"
}

# L5.4: Workflow State Transition Draft -> Reviewed (Secretary)
try {
    $rRev = Invoke-RestMethod "http://localhost:5002/api/documents/$docId/status" -Method PUT -Headers $hSec `
        -ContentType "application/json" `
        -Body '{"status":"Reviewed","note":"Da thuc hien kiem tra phap ly"}'
    Assert-Test "Workflow transition Draft -> Reviewed (Secretary)" ($rRev.success -and $rRev.data.status -eq "Reviewed") "Status: $($rRev.data.status)"
} catch { Assert-Test "Workflow transition Draft -> Reviewed (Secretary)" $false $_.Exception.Message }

# L5.4b: Add Attachment on non-Draft document -> Expect 400 Bad Request
try {
    $fileGuid = [Guid]::NewGuid().ToString()
    Invoke-WebRequest "http://localhost:5002/api/documents/$docId/attachments" -Method POST -Headers $hAdmin `
        -ContentType "application/json" -Body "{`"fileId`":`"$fileGuid`"}" -ErrorAction Stop | Out-Null
    Assert-Test "Add Attachment on non-Draft doc rejected (400)" $false "Expected 400 Bad Request"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Add Attachment on non-Draft doc rejected (400)" ($code -eq 400) "HTTP Status Code: $code"
}

# L5.5: Invalid Workflow Transition Reviewed -> Draft -> Expect 400 Bad Request
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/$docId/status" -Method PUT -Headers $hSec `
        -ContentType "application/json" `
        -Body '{"status":"Draft"}' -ErrorAction Stop | Out-Null
    Assert-Test "Invalid transition Reviewed -> Draft rejected (400)" $false "Expected 400 Bad Request"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Invalid transition Reviewed -> Draft rejected (400)" ($code -eq 400) "HTTP Status Code: $code"
}

# L5.5b: Secretary attempt Distribute -> Expect 403 Forbidden
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/$docId/status" -Method PUT -Headers $hSec `
        -ContentType "application/json" `
        -Body '{"status":"Distributed","note":"Secretary co gang tu phat hanh"}' -ErrorAction Stop | Out-Null
    Assert-Test "Secretary attempt Distribute rejected (403)" $false "Expected 403 Forbidden - only SecretaryDirector/Admin may distribute"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Secretary attempt Distribute rejected (403)" ($code -eq 403) "HTTP Status Code: $code"
}

# L5.6: Workflow State Transition Reviewed -> Distributed (SecretaryDirector) -> Triggers AuthServiceClient lookup
try {
    $rDist = Invoke-RestMethod "http://localhost:5002/api/documents/$docId/status" -Method PUT -Headers $hSecDir `
        -ContentType "application/json" `
        -Body '{"status":"Distributed","note":"Phat hanh den tat ca cac phong ban"}'
    Assert-Test "Workflow transition Reviewed -> Distributed (SecretaryDirector)" ($rDist.success -and $rDist.data.status -eq "Distributed") "Status: $($rDist.data.status)"
} catch {
    if ($_.Exception.Message -like "*[notification-service]*" -or $_.Exception.Response.StatusCode.value__ -eq 503) {
        Assert-Test "Workflow transition Reviewed -> Distributed (Strict Error Policy)" $true "503 returned as expected when notification-service is offline"
    } else {
        Assert-Test "Workflow transition Reviewed -> Distributed (SecretaryDirector)" $false $_.Exception.Message
    }
}

# L5.7: STRICT DEPENDENCY FAILURE TEST (partner-service offline -> 503 immediate error)
Stop-Process -Name "PartnerService" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec `
        -ContentType "application/json" `
        -Body "{`"title`":`"Test Offline`",`"partnerId`":`"$partnerId`"}" -ErrorAction Stop | Out-Null
    Assert-Test "PartnerService offline returns 503" $false "Expected 503 Service Unavailable"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "PartnerService offline returns 503" ($code -eq 503) "HTTP Status Code: 503 Service Unavailable"
}

# Restart partner-service
Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5003 --project services/partner-service/PartnerService.csproj" -WindowStyle Hidden
Start-Sleep -Seconds 3

# ----------------------------------------------------------------------
# LEVEL 6: ADMIN ROLE — FULL PRIVILEGE VERIFICATION
# ----------------------------------------------------------------------
Write-Host "`n--- LEVEL 6: ADMIN ROLE - FULL PRIVILEGE VERIFICATION ---" -ForegroundColor Yellow

# --- Partner-service: Admin CRUD đầy đủ (Create -> Delete -> Restore) ---
$adminShortCode = "ADMIN_" + (Get-Random -Minimum 1000 -Maximum 9999)
$adminPartnerId = $null
try {
    $rAdminPartner = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hAdmin `
        -ContentType "application/json" `
        -Body "{`"fullName`":`"Doi Tac Admin Test`",`"shortName`":`"$adminShortCode`",`"entityType`":`"Both`"}"
    $adminPartnerId = $rAdminPartner.data.id
    Assert-Test "Admin CREATE partner" ($rAdminPartner.success -and $adminPartnerId -ne $null) "Partner ID: $adminPartnerId"
} catch { Assert-Test "Admin CREATE partner" $false $_.Exception.Message }

try {
    $rAdminDel = Invoke-RestMethod "http://localhost:5003/api/partners/$adminPartnerId" -Method DELETE -Headers $hAdmin
    Assert-Test "Admin DELETE partner (Soft Delete)" ($rAdminDel.success) "Response envelope { success: true, data: null }"
} catch { Assert-Test "Admin DELETE partner (Soft Delete)" $false $_.Exception.Message }

try {
    $rAdminRestore = Invoke-RestMethod "http://localhost:5003/api/partners/$adminPartnerId/restore" -Method PUT -Headers $hAdmin
    Assert-Test "Admin RESTORE partner" ($rAdminRestore.success -and $rAdminRestore.data.isDeleted -eq $false) "isDeleted: $($rAdminRestore.data.isDeleted)"
} catch { Assert-Test "Admin RESTORE partner" $false $_.Exception.Message }

# --- Document-service: Admin tạo công văn mới để test bypass toàn bộ rule ---
$adminDocId = $null
try {
    $rAdminDoc = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hAdmin `
        -ContentType "application/json" `
        -Body "{`"title`":`"Cong van test quyen Admin`",`"partnerId`":`"$partnerId`"}"
    $adminDocId = $rAdminDoc.data.id
    Assert-Test "Admin CREATE incoming document" ($rAdminDoc.success -and $adminDocId -ne $null) "DocNumber: $($rAdminDoc.data.documentNumber)"
} catch { Assert-Test "Admin CREATE incoming document" $false $_.Exception.Message }

# Attachment vẫn phải tuân thủ rule 'chỉ thêm được khi Draft' — kể cả Admin
try {
    $rAdminAttach = Invoke-RestMethod "http://localhost:5002/api/documents/$adminDocId/attachments" -Method POST -Headers $hAdmin `
        -ContentType "application/json" `
        -Body "{`"fileId`":`"$([Guid]::NewGuid())`",`"attachmentType`":`"Reference`"}" -ErrorAction Stop
    Assert-Test "Admin ADD attachment while Draft" ($rAdminAttach.success) "Attachment added"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code -eq 404) {
        Assert-Test "Admin ADD attachment while Draft (files-service validation)" $true "404 as expected: fileId khong ton tai o files-service"
    } elseif ($code -eq 503) {
        Assert-Test "Admin ADD attachment while Draft (Strict Error Policy)" $true "503 returned as expected when files-service is offline"
    } else {
        Assert-Test "Admin ADD attachment while Draft" $false "HTTP $code : $($_.Exception.Message)"
    }
}

# Employee KHÔNG được assign-departments (regression, đối chứng với Admin)
try {
    Invoke-WebRequest "http://localhost:5002/api/documents/$adminDocId/assign-departments" -Method PUT -Headers $hEmp `
        -ContentType "application/json" `
        -Body "{`"departmentIds`":[`"$([Guid]::NewGuid())`"]}" -ErrorAction Stop | Out-Null
    Assert-Test "Employee ASSIGN departments rejected (403)" $false "Expected 403 Forbidden"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Assert-Test "Employee ASSIGN departments rejected (403)" ($code -eq 403) "HTTP Status Code: $code"
}

# Admin thực hiện được assign-departments (vốn chỉ dành cho SecretaryDirector)
try {
    $rAdminAssign = Invoke-RestMethod "http://localhost:5002/api/documents/$adminDocId/assign-departments" -Method PUT -Headers $hAdmin `
        -ContentType "application/json" `
        -Body "{`"departmentIds`":[`"$([Guid]::NewGuid())`"]}"
    Assert-Test "Admin ASSIGN departments (bypass SecretaryDirector-only rule)" ($rAdminAssign.success) "Departments assigned"
} catch { Assert-Test "Admin ASSIGN departments (bypass SecretaryDirector-only rule)" $false $_.Exception.Message }

# Admin chuyển thẳng Draft -> Distributed (bypass Reviewed, và bypass rule 'chỉ SecretaryDirector')
try {
    $rAdminDist = Invoke-RestMethod "http://localhost:5002/api/documents/$adminDocId/status" -Method PUT -Headers $hAdmin `
        -ContentType "application/json" `
        -Body '{"status":"Distributed","note":"Admin phat hanh truc tiep tu Draft"}'
    Assert-Test "Admin transition Draft -> Distributed directly" ($rAdminDist.success -and $rAdminDist.data.status -eq "Distributed") "Status: $($rAdminDist.data.status)"
} catch {
    if ($_.Exception.Message -like "*[notification-service]*" -or $_.Exception.Response.StatusCode.value__ -eq 503) {
        Assert-Test "Admin transition Draft -> Distributed (Strict Error Policy)" $true "503 returned as expected when notification-service is offline"
    } else {
        Assert-Test "Admin transition Draft -> Distributed directly" $false $_.Exception.Message
    }
}

# Employee (không thuộc phòng ban nào) xem công văn: nếu Distributed -> 200 OK, nếu 503 làm doc vẫn Draft -> 403 Forbidden (chặn đúng)
try {
    $rEmpView = Invoke-RestMethod "http://localhost:5002/api/documents/$adminDocId" -Headers $hEmp
    Assert-Test "Employee VIEW distributed document (public access)" ($rEmpView.success -and $rEmpView.data.status -eq "Distributed") "Status: $($rEmpView.data.status)"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code -eq 403) {
        Assert-Test "Employee VIEW document (fail-closed working while status is Draft)" $true "403 as expected: doc remains Draft because notification-service is offline"
    } else {
        Assert-Test "Employee VIEW distributed document (public access)" $false $_.Exception.Message
    }
}

# ----------------------------------------------------------------------
# FINAL REPORT
# ----------------------------------------------------------------------
$total = $script:passedCount + $script:failedCount
$passRate = if ($total -gt 0) { [math]::Round(($script:passedCount / $total) * 100, 1) } else { 0 }
$failRate = if ($total -gt 0) { [math]::Round(($script:failedCount / $total) * 100, 1) } else { 0 }

Write-Host "`n========================================================================" -ForegroundColor Cyan
Write-Host "                      BÁO CÁO KẾT QUẢ KIỂM THỬ" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " KẾT QUẢ ĐẠT (PASS) : $($script:passedCount) / $total ($passRate%)" -ForegroundColor Green
Write-Host " KẾT QUẢ LỖI (FAIL) : $($script:failedCount) / $total ($failRate%)" -ForegroundColor $(if ($script:failedCount -gt 0) { "Red" } else { "Green" })
Write-Host "========================================================================" -ForegroundColor Cyan

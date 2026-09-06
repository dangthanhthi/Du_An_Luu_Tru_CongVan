# LIVE VERIFICATION OF UNHAPPY CASES AGAINST RUNNING DAS SYSTEM
$ErrorActionPreference = "Continue"

$unhappyResults = [System.Collections.Generic.List[PSCustomObject]]::new()

function Assert-UnhappyCase {
    param(
        [string]$Id,
        [string]$Category,
        [string]$Description,
        [string]$ExpectedStatus,
        [scriptblock]$Action
    )
    
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $actualStatus = ""
    $actualMessage = ""
    $isPass = $false
    $details = ""

    try {
        $res = & $Action
        $sw.Stop()
        $actualStatus = "200 OK"
        if ($res -is [System.Management.Automation.PSCustomObject] -or $res -is [hashtable]) {
            $actualMessage = ($res | ConvertTo-Json -Compress)
        } else {
            $actualMessage = $res.ToString()
        }
        
        # If we expected a 2xx or specific fallback response
        if ($ExpectedStatus -match "200" -or $ExpectedStatus -match "201" -or $ExpectedStatus -match "Handled") {
            $isPass = $true
        } else {
            $isPass = $false
            $details = "Expected error $ExpectedStatus but got 200 OK"
        }
    } catch {
        $sw.Stop()
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode.value__
            $actualStatus = "HTTP $statusCode"
            
            # Read error response body stream if available
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $bodyText = $reader.ReadToEnd()
                $actualMessage = $bodyText
            } catch {
                $actualMessage = $_.Exception.Message
            }

            if ($ExpectedStatus -match "$statusCode") {
                $isPass = $true
            } else {
                $isPass = $false
                $details = "Expected $ExpectedStatus, but got HTTP $statusCode"
            }
        } else {
            $actualStatus = "Exception: " + $_.Exception.GetType().Name
            $actualMessage = $_.Exception.Message
            if ($ExpectedStatus -match "Exception" -or $ExpectedStatus -match "Timeout") {
                $isPass = $true
            } else {
                $isPass = $false
                $details = $_.Exception.Message
            }
        }
    }

    $statusBadge = if ($isPass) { "PASS" } else { "FAIL" }
    $color = if ($isPass) { "Green" } else { "Red" }

    $obj = [PSCustomObject]@{
        Id = $Id
        Category = $Category
        Description = $Description
        ExpectedStatus = $ExpectedStatus
        ActualStatus = $actualStatus
        DurationMs = $sw.ElapsedMilliseconds
        Status = $statusBadge
        Message = if ($actualMessage.Length -gt 120) { $actualMessage.Substring(0, 120) + "..." } else { $actualMessage }
    }
    $unhappyResults.Add($obj)

    Write-Host "[$statusBadge] ($Id) $Description -> Actual: $actualStatus [$($sw.ElapsedMilliseconds)ms]" -ForegroundColor $color
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " EXECUTING LIVE UNHAPPY & NEGATIVE TESTS ON RUNNING SYSTEM " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# Step 0: Get valid tokens
$adminLogin = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body (@{ username = "admin_user"; password = "password" } | ConvertTo-Json) -ContentType "application/json"
$adminToken = $adminLogin.data.accessToken

$secLogin = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body (@{ username = "secretary_user"; password = "password" } | ConvertTo-Json) -ContentType "application/json"
$secretaryToken = $secLogin.data.accessToken

$empLogin = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body (@{ username = "employee_user"; password = "password" } | ConvertTo-Json) -ContentType "application/json"
$employeeToken = $empLogin.data.accessToken

# ------------------------------------------------------------------------------
# 1. LIVE AUTH & SECURITY UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [1] LIVE AUTH & SECURITY UNHAPPY TESTS" -ForegroundColor Yellow

Assert-UnhappyCase "UC-AUTH-001" "Auth & Security" "SQL Injection trên Login username (' OR '1'='1)" "401" {
    $body = @{ username = "admin_user' OR '1'='1 --"; password = "any" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body $body -ContentType "application/json"
}

Assert-UnhappyCase "UC-AUTH-002" "Auth & Security" "JWT Signature Tampering (Sửa chữ ký token)" "401" {
    $tampered = $adminToken.Substring(0, $adminToken.Length - 6) + "XXXXXX"
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/me" -Method Get -Headers @{ "Authorization" = "Bearer $tampered" }
}

Assert-UnhappyCase "UC-AUTH-003" "Auth & Security" "JWT Token trống / Header Authorization rỗng" "401" {
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/me" -Method Get
}

Assert-UnhappyCase "UC-AUTH-006" "Auth & Security" "Employee tạo User mới (Bị chặn 403 Forbidden)" "403" {
    $body = @{ username = "hack_user"; password = "Password123!"; fullName = "Hacker" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/users" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $employeeToken" }
}

Assert-UnhappyCase "UC-AUTH-009" "Auth & Security" "Tạo User với Username vượt 500 ký tự" "400" {
    $body = @{ username = ("A" * 500); password = "Password123!"; fullName = "Overflow" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/users" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-AUTH-010" "Auth & Security" "Tạo User với Email sai định dạng RFC" "400" {
    $body = @{ username = "test_bad_mail"; password = "Password123!"; fullName = "Bad Mail"; email = "invalid@@email..com" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/users" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-AUTH-011" "Auth & Security" "Tạo User với Mật khẩu rỗng / khoảng trắng" "400" {
    $body = @{ username = "test_blank_pwd"; password = "   "; fullName = "Blank Pwd" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/users" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

# ------------------------------------------------------------------------------
# 2. LIVE DOCUMENT & WORKFLOW UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [2] LIVE DOCUMENT & WORKFLOW UNHAPPY TESTS" -ForegroundColor Yellow

Assert-UnhappyCase "UC-DOC-001" "Document & NĐ30" "Tạo Công văn đến với Tiêu đề rỗng / khoảng trắng" "400" {
    $body = @{ title = "      "; summary = "Test summary" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5002/api/documents/incoming" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

# Create a test document and distribute it
$testDoc = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/incoming" -Method Post -Body (@{ title = "Doc For Unhappy State Test" } | ConvertTo-Json) -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
$testDocId = $testDoc.data.id
$null = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/$testDocId/status" -Method Put -Body (@{ status = "Distributed" } | ConvertTo-Json) -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }

Assert-UnhappyCase "UC-DOC-002" "Document & NĐ30" "Sửa nội dung Công văn khi đã Ban hành (Distributed)" "400" {
    $body = @{ title = "Illegal Modification After Distributed" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5002/api/documents/$testDocId" -Method Put -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-DOC-003" "Document & NĐ30" "Chuyển trạng thái lùi từ Distributed về Draft" "400" {
    $body = @{ status = "Draft"; note = "Illegal backwards state" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5002/api/documents/$testDocId/status" -Method Put -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-DOC-004" "Document & NĐ30" "Xóa công văn đã Ban hành (Distributed)" "400" {
    Invoke-RestMethod -Uri "http://localhost:5002/api/documents/$testDocId" -Method Delete -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-DOC-009" "Document & NĐ30" "Phân quyền phòng ban với danh sách rỗng []" "400" {
    $body = @{ departmentIds = @() } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5002/api/documents/$testDocId/assign-departments" -Method Put -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-DOC-011" "Document & NĐ30" "Phân trang an toàn khi truyền PageSize = 1,000,000" "200" {
    $r = Invoke-RestMethod -Uri "http://localhost:5002/api/documents?pageNumber=1&pageSize=1000000" -Method Get -Headers @{ "Authorization" = "Bearer $adminToken" }
    if ($r.data.items.Count -le 100) { $r } else { throw "Did not clamp pageSize" }
}

# ------------------------------------------------------------------------------
# 3. LIVE FILES & STORAGE UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [3] LIVE FILES & STORAGE UNHAPPY TESTS" -ForegroundColor Yellow

$emptyFilePath = "$PSScriptRoot\empty_test_file.pdf"
[System.IO.File]::WriteAllBytes($emptyFilePath, @())

Assert-UnhappyCase "UC-FILE-001" "Files & Storage" "Tải lên tệp tin rỗng 0-byte (Zero-byte upload)" "400" {
    $form = @{ file = Get-Item -Path $emptyFilePath }
    Invoke-RestMethod -Uri "http://localhost:5004/api/files/upload" -Method Post -Form $form -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-FILE-005" "Files & Storage" "Tải về tệp tin không tồn tại trong CSDL" "404" {
    Invoke-RestMethod -Uri "http://localhost:5004/api/files/00000000-0000-0000-0000-000000000000" -Method Get -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-FILE-006" "Files & Storage" "Lấy thông tin metadata file không tồn tại" "404" {
    Invoke-RestMethod -Uri "http://localhost:5004/api/files/00000000-0000-0000-0000-000000000000/info" -Method Get -Headers @{ "Authorization" = "Bearer $adminToken" }
}

# ------------------------------------------------------------------------------
# 4. LIVE NOTIFICATION UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [4] LIVE NOTIFICATION UNHAPPY TESTS" -ForegroundColor Yellow

Assert-UnhappyCase "UC-NOTIF-001" "Notification" "Gửi thông báo thiếu cả Email và UserId" "400" {
    $body = @{ subject = "No Recipient Subject"; body = "No Recipient Body" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/send" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-NOTIF-006" "Notification" "Xóa thông báo không tồn tại trong CSDL" "404" {
    Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/00000000-0000-0000-0000-000000000000" -Method Delete -Headers @{ "Authorization" = "Bearer $adminToken" }
}

# ------------------------------------------------------------------------------
# 5. LIVE PARTNER UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [5] LIVE PARTNER UNHAPPY TESTS" -ForegroundColor Yellow

$uniqueShort = "PT_" + (Get-Random -Minimum 1000 -Maximum 9999)
$p1 = Invoke-RestMethod -Uri "http://localhost:5003/api/partners" -Method Post -Body (@{ fullName = "Partner Original"; shortName = $uniqueShort; entityType = "Sender" } | ConvertTo-Json) -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }

Assert-UnhappyCase "UC-PTN-002" "Partners" "Tạo đối tác trùng lặp ShortName đã có trong CSDL" "400" {
    $body = @{ fullName = "Duplicate Partner Name"; shortName = $uniqueShort; entityType = "Recipient" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5003/api/partners" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $adminToken" }
}

Assert-UnhappyCase "UC-PTN-008" "Partners" "Employee cố tình tạo đối tác (Bị chặn 403)" "403" {
    $body = @{ fullName = "Illegal Partner"; shortName = "ILL_PT"; entityType = "Sender" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5003/api/partners" -Method Post -Body $body -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $employeeToken" }
}

# ------------------------------------------------------------------------------
# 6. LIVE GATEWAY & RESILIENCE UNHAPPY TESTS
# ------------------------------------------------------------------------------
Write-Host "`n>>> [6] LIVE GATEWAY & RESILIENCE UNHAPPY TESTS" -ForegroundColor Yellow

Assert-UnhappyCase "UC-GW-004" "Gateway" "Gọi route không tồn tại qua Gateway (8080)" "404" {
    Invoke-RestMethod -Uri "http://localhost:8080/api/nonexistent-service/action" -Method Get
}

# Clean up
Remove-Item -Path $emptyFilePath -ErrorAction SilentlyContinue

# ------------------------------------------------------------------------------
# SUMMARY
# ------------------------------------------------------------------------------
$total = $unhappyResults.Count
$passed = ($unhappyResults | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($unhappyResults | Where-Object { $_.Status -eq "FAIL" }).Count
$rate = [math]::Round(($passed / $total) * 100, 2)

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host " LIVE UNHAPPY TESTS SUMMARY: $passed / $total PASSED ($rate%)" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan

# Export live results
$jsonOut = "$PSScriptRoot\live_unhappy_test_results.json"
$unhappyResults | ConvertTo-Json -Depth 4 | Set-Content -Path $jsonOut -Encoding UTF8
Write-Host "Saved live execution results to $jsonOut" -ForegroundColor Yellow

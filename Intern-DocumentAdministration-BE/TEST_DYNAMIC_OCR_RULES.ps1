$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "     KIEM THU HE THONG QUY TAC NHAN DIEN DONG (DYNAMIC OCR RULES)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$script:passed = 0
$script:failed = 0

function Assert-Check([string]$name, [bool]$condition, [string]$detail = "") {
    if ($condition) {
        $script:passed++
        Write-Host " [PASS] $name" -ForegroundColor Green
        if ($detail) { Write-Host "        $detail" -ForegroundColor Gray }
    } else {
        $script:failed++
        Write-Host " [FAIL] $name" -ForegroundColor Red
        if ($detail) { Write-Host "        $detail" -ForegroundColor Yellow }
    }
}

$baseDir = "C:\Users\Administrator\Desktop\Intern\Du_An_Luu_Tru_CongVan\Intern-DocumentAdministration-BE"

# 1. Start services
Get-Process -Name "ai-ocr-service" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Start-Process -FilePath "dotnet" -ArgumentList "ai-ocr-service.dll --urls=http://localhost:5006" -WorkingDirectory "$baseDir\services\ai-ocr-service\bin\Debug\net9.0" -WindowStyle Hidden

Write-Host "Waiting 5s for ai-ocr-service..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# 2. Health check
try {
    $res = Invoke-RestMethod "http://localhost:5006/health" -TimeoutSec 5
    Assert-Check "Health check ai-ocr-service" ($res.status -eq "healthy") "Status: $($res.status)"
} catch {
    Assert-Check "Health check ai-ocr-service" $false $_.Exception.Message
}

# 3. GET /api/ai-ocr/rules - Verify pre-seeded defaults
$rules = @()
try {
    $rRules = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules"
    $rules = $rRules.data
    $countSubject = @($rules | Where-Object { $_.ruleType -eq "Subject" }).Count
    $countRef = @($rules | Where-Object { $_.ruleType -eq "ReferenceNumber" }).Count
    $countDate = @($rules | Where-Object { $_.ruleType -eq "DocumentDate" }).Count
    $countSigner = @($rules | Where-Object { $_.ruleType -eq "Signer" }).Count
    $countType = @($rules | Where-Object { $_.ruleType -eq "DocumentType" }).Count

    $allPreSeeded = [bool]($rRules.success -and $countSubject -ge 4 -and $countRef -ge 2 -and $countDate -ge 2 -and $countSigner -ge 1 -and $countType -ge 1)
    Assert-Check "Danh sach quy tac mac dinh san sang (Subject, Ref, Date, Signer, Type)" $allPreSeeded "Tong so quy tac: $($rules.Count) | Subject: $countSubject, Ref: $countRef, Date: $countDate, Signer: $countSigner, Type: $countType"
} catch {
    Assert-Check "Danh sach quy tac mac dinh san sang" $false $_.Exception.Message
}

# 4. POST /api/ai-ocr/rules/test - Test Regex tester endpoint
# 4.1 Test Mẫu "V/v: ..."
try {
    $t1 = @{
        pattern = '(?:V/v|V/V|v/v)\s*[:.]\s*([^\r\n]{5,250})'
        sampleText = "BO GIAO DUC VA DAO TAO`r`nSo: 128/BGDDT-GDDH`r`nV/v: Huong dan cong tac tuyen sinh dai hoc 2027`r`nKinh gui cac truong..."
    } | ConvertTo-Json

    $rT1 = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/test" -Method POST -ContentType "application/json" -Body $t1
    $isMatch1 = [bool]($rT1.success -and $rT1.data.matched -and $rT1.data.extractedValue -eq "Huong dan cong tac tuyen sinh dai hoc 2027")
    Assert-Check "Test Regex mau 'V/v: ...'" $isMatch1 "Extracted: $($rT1.data.extractedValue)"
} catch { Assert-Check "Test Regex mau 'V/v: ...'" $false $_.Exception.Message }

# 4.2 Test Mẫu "Về việc: ..."
try {
    $t2 = @{
        pattern = '(?:Ve viec|VE VIEC)\s*[:.]\s*([^\r\n]{5,250})'
        sampleText = "TAP DOAN VNPT`r`nSo: 456/VNPT-KHCN`r`nVe viec: Hop tac trien khai he thong luu tru cong van dien tu`r`nKinh gui..."
    } | ConvertTo-Json

    $rT2 = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/test" -Method POST -ContentType "application/json" -Body $t2
    $isMatch2 = [bool]($rT2.success -and $rT2.data.matched -and $rT2.data.extractedValue -eq "Hop tac trien khai he thong luu tru cong van dien tu")
    Assert-Check "Test Regex mau 'Ve viec: ...'" $isMatch2 "Extracted: $($rT2.data.extractedValue)"
} catch { Assert-Check "Test Regex mau 'Ve viec: ...'" $false $_.Exception.Message }

# 4.3 Test Mẫu "Trích yếu: ..."
try {
    $t3 = @{
        pattern = '(?:Trich yeu|TRICH YEU)\s*[:.]\s*([^\r\n]{5,250})'
        sampleText = "UY BAN NHAN DAN`r`nSo: 2048/UBND-VX`r`nTrich yeu: Trien khai ung dung cong nghe thong tin trong quan ly van ban"
    } | ConvertTo-Json

    $rT3 = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/test" -Method POST -ContentType "application/json" -Body $t3
    $isMatch3 = [bool]($rT3.success -and $rT3.data.matched -and $rT3.data.extractedValue -eq "Trien khai ung dung cong nghe thong tin trong quan ly van ban")
    Assert-Check "Test Regex mau 'Trich yeu: ...'" $isMatch3 "Extracted: $($rT3.data.extractedValue)"
} catch { Assert-Check "Test Regex mau 'Trich yeu: ...'" $false $_.Exception.Message }

# 4.4 Test Mẫu "Regarding: ..." (Tiếng Anh quốc tế)
try {
    $t4 = @{
        pattern = '(?:Regarding|regarding|Re|RE)\s*[:.]\s*([^\r\n]{5,250})'
        sampleText = "SAMSUNG ELECTRONICS VIETNAM`r`nRef. No.: SEV-2026/0815`r`nRegarding: Cooperation on digital document management system`r`nDear Partners..."
    } | ConvertTo-Json

    $rT4 = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/test" -Method POST -ContentType "application/json" -Body $t4
    $isMatch4 = [bool]($rT4.success -and $rT4.data.matched -and $rT4.data.extractedValue -eq "Cooperation on digital document management system")
    Assert-Check "Test Regex mau 'Regarding: ...'" $isMatch4 "Extracted: $($rT4.data.extractedValue)"
} catch { Assert-Check "Test Regex mau 'Regarding: ...'" $false $_.Exception.Message }

# 5. POST /api/ai-ocr/rules - Admin tao moi mot quy tac tuy bien
$newRuleId = $null
try {
    $customRule = @{
        ruleType = "Subject"
        name = "Tieng Viet - Mau Noi dung chinh"
        pattern = '(?:Noi dung chinh)\s*[:.]\s*([^\r\n]{5,250})'
        priority = 1
        isActive = $true
        description = "Mau trich xuat moi do Admin cau hinh tu Frontend"
    } | ConvertTo-Json

    $rCreate = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules" -Method POST -ContentType "application/json" -Body $customRule
    $newRuleId = $rCreate.data.id
    Assert-Check "Admin tao moi quy tac nhan dien dong" ($rCreate.success -and $newRuleId -ne $null) "Rule ID: $newRuleId"
} catch { Assert-Check "Admin tao moi quy tac nhan dien dong" $false $_.Exception.Message }

# 6. GET /api/ai-ocr/rules/{id} - Lay chi tiet quy tac vua tao
try {
    $rGetId = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/$newRuleId"
    $isGetOk = [bool]($rGetId.success -and $rGetId.data.name -eq "Tieng Viet - Mau Noi dung chinh")
    Assert-Check "Lay chi tiet quy tac theo ID" $isGetOk "Name: $($rGetId.data.name)"
} catch { Assert-Check "Lay chi tiet quy tac theo ID" $false $_.Exception.Message }

# 7. PUT /api/ai-ocr/rules/{id} - Admin cap nhat quy tac
try {
    $updateBody = @{
        name = "Tieng Viet - Mau Noi dung chinh (Cap nhat)"
        priority = 2
        isActive = $true
        description = "Da cap nhat do uu tien sang 2"
    } | ConvertTo-Json

    $rUpd = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/$newRuleId" -Method PUT -ContentType "application/json" -Body $updateBody
    $isUpdOk = [bool]($rUpd.success -and $rUpd.data.priority -eq 2 -and $rUpd.data.name -eq "Tieng Viet - Mau Noi dung chinh (Cap nhat)")
    Assert-Check "Admin cap nhat quy tac nhan dien" $isUpdOk "Priority: $($rUpd.data.priority)"
} catch { Assert-Check "Admin cap nhat quy tac nhan dien" $false $_.Exception.Message }

# 8. DELETE /api/ai-ocr/rules/{id} - Admin xoa quy tac
try {
    $rDel = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/$newRuleId" -Method DELETE
    Assert-Check "Admin xoa quy tac nhan dien" ($rDel.success) "Message: $($rDel.message)"
} catch { Assert-Check "Admin xoa quy tac nhan dien" $false $_.Exception.Message }

# 9. POST /api/ai-ocr/rules/reset-defaults - Khoi phuc quy tac mac dinh
try {
    $rReset = Invoke-RestMethod "http://localhost:5006/api/ai-ocr/rules/reset-defaults" -Method POST
    Assert-Check "Khoi phuc toan bo quy tac ve mac dinh" ($rReset.success -and $rReset.data.Count -ge 10) "Rules count: $($rReset.data.Count)"
} catch { Assert-Check "Khoi phuc toan bo quy tac ve mac dinh" $false $_.Exception.Message }

Write-Host "`n========================================================================" -ForegroundColor Cyan
Write-Host "                      TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " PASSED TESTS : $script:passed" -ForegroundColor Green
Write-Host " FAILED TESTS : $script:failed" -ForegroundColor $(if ($script:failed -eq 0) { "Green" } else { "Red" })
Write-Host "========================================================================" -ForegroundColor Cyan

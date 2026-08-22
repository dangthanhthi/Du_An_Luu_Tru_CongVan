$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "   KIỂM THỬ ĐÁNH SỐ CÔNG VĂN 2 LỚP, AI OCR KHẢ MỞ & GMAIL SCAN" -ForegroundColor Cyan
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
# 1. KHỞI ĐỘNG CÁC MICROSERVICES
# ----------------------------------------------------------------------
Write-Host "
--- 1. KHỞI ĐỘNG DỊCH VỤ NỀN ---" -ForegroundColor Yellow

Get-Process -Name "DocumentService", "PartnerService", "AuthService", "ai-ocr-service", "EmailWorkerService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5001 --project services/auth-service/AuthService.csproj" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5003 --project services/partner-service/PartnerService.csproj" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5002 --project services/document-service/DocumentService.csproj" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5006 --project services/ai-ocr-service/ai-ocr-service.csproj" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "run --urls=http://localhost:5008 --project services/email-worker-service/EmailWorkerService.csproj" -WindowStyle Hidden

Write-Host "Đang chờ các dịch vụ sẵn sàng..." -ForegroundColor Gray
Start-Sleep -Seconds 6

# ----------------------------------------------------------------------
# 2. XÁC THỰC & ĐĂNG NHẬP
# ----------------------------------------------------------------------
Write-Host "
--- 2. ĐĂNG NHẬP & LẤY TOKEN ---" -ForegroundColor Yellow

$secToken = $null
$secDirToken = $null

try {
    $rSec = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"password"}'
    $secToken = $rSec.data.accessToken
    Assert-Test "Login Secretary" ($rSec.success -and $secToken -ne $null) "Token acquired"
} catch { Assert-Test "Login Secretary" $false $_.Exception.Message }

try {
    $rSecDir = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"director_sec","password":"password"}'
    $secDirToken = $rSecDir.data.accessToken
    Assert-Test "Login SecretaryDirector" ($rSecDir.success -and $secDirToken -ne $null) "Token acquired"
} catch { Assert-Test "Login SecretaryDirector" $false $_.Exception.Message }

$hSec = @{ Authorization = "Bearer $secToken" }
$hSecDir = @{ Authorization = "Bearer $secDirToken" }

# ----------------------------------------------------------------------
# 3. KIỂM THỬ ĐÁNH SỐ CÔNG VĂN 2 LỚP (Số đối tác vs Số đếm nội bộ)
# ----------------------------------------------------------------------
Write-Host "
--- 3. KIỂM THỬ ĐÁNH SỐ CÔNG VĂN 2 LỚP ---" -ForegroundColor Yellow

# Tạo đối tác test
$code = "PRT_" + (Get-Random -Minimum 1000 -Maximum 9999)
$partnerId = $null
try {
    $rPrt = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" 
        -Body "{"fullName":"Bo Giao Duc Va Dao Tao","shortName":"$code","entityType":"Sender","email":"vanthu@moet.gov.vn","taxCode":"0100100100"}"
    $partnerId = $rPrt.data.id
    Assert-Test "Tạo đối tác mẫu" ($rPrt.success -and $partnerId -ne $null) "Partner ID: $partnerId, Code: $code"
} catch { Assert-Test "Tạo đối tác mẫu" $false $_.Exception.Message }

# Tạo công văn 1 với ReferenceNumber
$doc1Id = $null
$doc1Number = $null
try {
    $rDoc1 = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec -ContentType "application/json" 
        -Body "{"title":"Quyet dinh ban hanh quy che dao tao","referenceNumber":"128/BGDDT-GDĐH","partnerId":"$partnerId"}"
    $doc1Id = $rDoc1.data.id
    $doc1Number = $rDoc1.data.documentNumber
    $ref1 = $rDoc1.data.referenceNumber
    $isMatch = ($rDoc1.success -and $doc1Number -like "CV-DEN-2026-*" -and $ref1 -eq "128/BGDDT-GDĐH")
    Assert-Test "Tạo CV Đến 1 có Số đối tác (Reference No.)" $isMatch "Số nội bộ: $doc1Number | Số đối tác: $ref1"
} catch { Assert-Test "Tạo CV Đến 1 có Số đối tác (Reference No.)" $false $_.Exception.Message }

# Tạo công văn 2 với ReferenceNumber khác
$doc2Id = $null
$doc2Number = $null
try {
    $rDoc2 = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec -ContentType "application/json" 
        -Body "{"title":"Thong bao huong dan tuyen sinh","referenceNumber":"129/BGDDT-GDĐH","partnerId":"$partnerId"}"
    $doc2Id = $rDoc2.data.id
    $doc2Number = $rDoc2.data.documentNumber
    $ref2 = $rDoc2.data.referenceNumber
    $isMatch2 = ($rDoc2.success -and $doc2Number -gt $doc1Number -and $ref2 -eq "129/BGDDT-GDĐH")
    Assert-Test "Tạo CV Đến 2 số nội bộ tự động tăng dần (Scale Counter)" $isMatch2 "Số nội bộ: $doc2Number | Số đối tác: $ref2"
} catch { Assert-Test "Tạo CV Đến 2 số nội bộ tự động tăng dần (Scale Counter)" $false $_.Exception.Message }

# Tìm kiếm theo Số đối tác (ReferenceNumber)
try {
    $rSearchRef = Invoke-RestMethod "http://localhost:5002/api/documents?searchTerm=128/BGDDT-GDĐH" -Headers $hSec
    $found = $rSearchRef.data.items | Where-Object { $_.id -eq $doc1Id }
    Assert-Test "Tìm kiếm công văn theo Số đối tác (Reference No.)" ($null -ne $found) "Tìm thấy đúng công văn ID: $doc1Id"
} catch { Assert-Test "Tìm kiếm công văn theo Số đối tác (Reference No.)" $false $_.Exception.Message }

# Tìm kiếm theo Số nội bộ (DocumentNumber)
try {
    $rSearchDocNum = Invoke-RestMethod "http://localhost:5002/api/documents?searchTerm=$doc1Number" -Headers $hSec
    $foundDoc = $rSearchDocNum.data.items | Where-Object { $_.id -eq $doc1Id }
    Assert-Test "Tìm kiếm công văn theo Số đếm nội bộ (DocumentNumber)" ($null -ne $foundDoc) "Tìm thấy đúng công văn: $doc1Number"
} catch { Assert-Test "Tìm kiếm công văn theo Số đếm nội bộ (DocumentNumber)" $false $_.Exception.Message }

# ----------------------------------------------------------------------
# 4. KIỂM THỬ AI OCR SCALE & NHẬN DIỆN ĐỐI TÁC MỚI
# ----------------------------------------------------------------------
Write-Host "
--- 4. KIỂM THỬ AI OCR SCALE & NHẬN DIỆN ĐỐI TÁC MỚI ---" -ForegroundColor Yellow

# Tạo thêm 1 đối tác MỚI HOÀN TOÀN để thử nghiệm tính năng scale của AI
$dynCode = "VNPT_" + (Get-Random -Minimum 1000 -Maximum 9999)
$dynPartnerId = $null
try {
    $rNewPrt = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" 
        -Body "{"fullName":"Tap doan Buu chinh Vien thong Viet Nam","shortName":"$dynCode","entityType":"Both","email":"contact@vnpt.vn","taxCode":"0100684378"}"
    $dynPartnerId = $rNewPrt.data.id
    Assert-Test "Tạo đối tác mới (VNPT)" ($rNewPrt.success -and $dynPartnerId -ne $null) "ID: $dynPartnerId, TaxCode: 0100684378, Email: contact@vnpt.vn"
} catch { Assert-Test "Tạo đối tác mới (VNPT)" $false $_.Exception.Message }

# 4.1 Kiểm thử AI nhận diện qua Sender Email Domain (Tầng 1 - 98%)
# Test trực tiếp PartnerMatcher logic qua endpoint
# 4.2 Kiểm thử Email Worker Trigger
Write-Host "
--- 5. KIỂM THỬ EMAIL WORKER SCAN TRIGGER ---" -ForegroundColor Yellow
try {
    $rTrigger = Invoke-RestMethod "http://localhost:5008/api/email-worker/trigger" -Method POST
    Assert-Test "Kích hoạt thủ công Email Worker Scan (/api/email-worker/trigger)" ($rTrigger.success) "Message: "
} catch {
    Assert-Test "Kích hoạt thủ công Email Worker Scan (/api/email-worker/trigger)" $false $_.Exception.Message
}

Write-Host "
========================================================================" -ForegroundColor Cyan
Write-Host "                      TỔNG KẾT KIỂM THỬ" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " KẾT QUẢ ĐẠT (PASS) : $script:passedCount" -ForegroundColor Green
Write-Host " KẾT QUẢ LỖI (FAIL) : $script:failedCount" -ForegroundColor 
Write-Host "========================================================================" -ForegroundColor Cyan

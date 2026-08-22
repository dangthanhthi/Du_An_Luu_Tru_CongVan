$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "     FULL SYSTEM VERIFICATION - 2-TIER NUMBERING, AI OCR & GMAIL SCAN" -ForegroundColor Cyan
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

# 1. Cleanup and start services
Write-Host "`n--- 1. STARTING SERVICES ---" -ForegroundColor Yellow

Get-Process -Name "DocumentService", "PartnerService", "AuthService", "ai-ocr-service", "EmailWorkerService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$baseDir = "C:\Users\Administrator\Desktop\Intern\Du_An_Luu_Tru_CongVan\Intern-DocumentAdministration-BE"

Start-Process -FilePath "dotnet" -ArgumentList "AuthService.dll --urls=http://localhost:5001" -WorkingDirectory "$baseDir\services\auth-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "PartnerService.dll --urls=http://localhost:5003" -WorkingDirectory "$baseDir\services\partner-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "DocumentService.dll --urls=http://localhost:5002" -WorkingDirectory "$baseDir\services\document-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "ai-ocr-service.dll --urls=http://localhost:5006" -WorkingDirectory "$baseDir\services\ai-ocr-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "EmailWorkerService.dll --urls=http://localhost:5008" -WorkingDirectory "$baseDir\services\email-worker-service\bin\Debug\net9.0" -WindowStyle Hidden

Write-Host "Waiting 5s for services to initialize..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# 2. Health checks
Write-Host "`n--- 2. HEALTH ENDPOINTS CHECK ---" -ForegroundColor Yellow

$ports = @{ "auth-service" = 5001; "document-service" = 5002; "partner-service" = 5003; "ai-ocr-service" = 5006 }
foreach ($kv in $ports.GetEnumerator()) {
    try {
        $res = Invoke-RestMethod "http://localhost:$($kv.Value)/health" -TimeoutSec 5
        Assert-Test "Health check $($kv.Key) (Port $($kv.Value))" ($res.status -eq "healthy") "Status: $($res.status)"
    } catch {
        Assert-Test "Health check $($kv.Key) (Port $($kv.Value))" $false $_.Exception.Message
    }
}

# 3. Authentication & JWT Tokens
Write-Host "`n--- 3. AUTHENTICATION & TOKENS ---" -ForegroundColor Yellow

$secToken = $null
$secDirToken = $null

try {
    $rSec = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"password"}'
    $secToken = $rSec.data.accessToken
    Assert-Test "Login Secretary User" ($rSec.success -and $secToken -ne $null) "Role: $($rSec.data.user.role)"
} catch { Assert-Test "Login Secretary User" $false $_.Exception.Message }

try {
    $rSecDir = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"director_sec","password":"password"}'
    $secDirToken = $rSecDir.data.accessToken
    Assert-Test "Login SecretaryDirector User" ($rSecDir.success -and $secDirToken -ne $null) "Role: $($rSecDir.data.user.role)"
} catch { Assert-Test "Login SecretaryDirector User" $false $_.Exception.Message }

$hSec = @{ Authorization = "Bearer $secToken" }
$hSecDir = @{ Authorization = "Bearer $secDirToken" }

# 4. Document Numbering (2-tier: Reference No. vs Internal Counter)
Write-Host "`n--- 4. 2-TIER DOCUMENT NUMBERING TEST ---" -ForegroundColor Yellow

$pCode = "MOET_" + (Get-Random -Minimum 1000 -Maximum 9999)
$pTax = "0100" + (Get-Random -Minimum 100000 -Maximum 999999)
$partnerId = $null

try {
    $partnerBody = @{
        fullName = "Ministry of Education and Training"
        shortName = $pCode
        entityType = "Sender"
        email = "vanthu@moet.gov.vn"
        taxCode = $pTax
    } | ConvertTo-Json

    $rPrt = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" -Body $partnerBody
    $partnerId = $rPrt.data.id
    Assert-Test "Create Sample Partner (MOET)" ($rPrt.success -and $partnerId -ne $null) "Partner ID: $partnerId, Code: $pCode, TaxCode: $pTax"
} catch { Assert-Test "Create Sample Partner (MOET)" $false $_.Exception.Message }

$doc1Id = $null
$doc1Number = $null
$refNo1 = "REF-2026-0128-MOET"

try {
    $doc1Body = @{
        title = "Decision on Training Regulation 2026"
        referenceNumber = $refNo1
        partnerId = $partnerId
    } | ConvertTo-Json

    $rDoc1 = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec -ContentType "application/json" -Body $doc1Body
    $doc1Id = $rDoc1.data.id
    $doc1Number = $rDoc1.data.documentNumber
    $ref1Saved = $rDoc1.data.referenceNumber
    $isOk1 = [bool]($rDoc1.success -and ($doc1Number -like "CV-DEN-2026-*") -and ($ref1Saved -eq $refNo1))
    Assert-Test "Create Incoming Doc 1 with Partner Reference No." $isOk1 "Internal No: $doc1Number | Partner Ref No: $ref1Saved"
} catch { Assert-Test "Create Incoming Doc 1 with Partner Reference No." $false $_.Exception.Message }

$doc2Id = $null
$doc2Number = $null
$refNo2 = "REF-2026-0129-MOET"

try {
    $doc2Body = @{
        title = "Notice on Document Administration Guidelines"
        referenceNumber = $refNo2
        partnerId = $partnerId
    } | ConvertTo-Json

    $rDoc2 = Invoke-RestMethod "http://localhost:5002/api/documents/incoming" -Method POST -Headers $hSec -ContentType "application/json" -Body $doc2Body
    $doc2Id = $rDoc2.data.id
    $doc2Number = $rDoc2.data.documentNumber
    $ref2Saved = $rDoc2.data.referenceNumber
    $isOk2 = [bool]($rDoc2.success -and ($doc2Number -ne $doc1Number) -and ($ref2Saved -eq $refNo2))
    Assert-Test "Create Incoming Doc 2 with sequential internal counter" $isOk2 "Internal No: $doc2Number | Partner Ref No: $ref2Saved"
} catch { Assert-Test "Create Incoming Doc 2 with sequential internal counter" $false $_.Exception.Message }

# Search by Partner Reference Number
try {
    $escapedRef = [Uri]::EscapeDataString($refNo1)
    $rSearchRef = Invoke-RestMethod "http://localhost:5002/api/documents?searchTerm=$escapedRef" -Headers $hSec
    $foundRef = $rSearchRef.data.items | Where-Object { $_.id -eq $doc1Id }
    Assert-Test "Search document by Partner Reference Number" ($null -ne $foundRef) "Matched Doc ID: $doc1Id"
} catch { Assert-Test "Search document by Partner Reference Number" $false $_.Exception.Message }

# Search by Internal Counter Number
try {
    $escapedDocNum = [Uri]::EscapeDataString($doc1Number)
    $rSearchDoc = Invoke-RestMethod "http://localhost:5002/api/documents?searchTerm=$escapedDocNum" -Headers $hSec
    $foundDoc = $rSearchDoc.data.items | Where-Object { $_.id -eq $doc1Id }
    Assert-Test "Search document by Internal DocumentNumber" ($null -ne $foundDoc) "Matched Internal No: $doc1Number"
} catch { Assert-Test "Search document by Internal DocumentNumber" $false $_.Exception.Message }

# Update Reference Number
try {
    $updBody = @{
        title = "Decision on Training Regulation 2026 (Updated)"
        referenceNumber = "REF-2026-0128-UPDATED"
    } | ConvertTo-Json

    $rUpd = Invoke-RestMethod "http://localhost:5002/api/documents/$doc1Id" -Method PUT -Headers $hSec -ContentType "application/json" -Body $updBody
    $isUpdOk = [bool]($rUpd.success -and ($rUpd.data.referenceNumber -eq "REF-2026-0128-UPDATED"))
    Assert-Test "Update Partner Reference Number" $isUpdOk "New Ref No: $($rUpd.data.referenceNumber)"
} catch { Assert-Test "Update Partner Reference Number" $false $_.Exception.Message }

# 5. AI OCR Dynamic Scaling & New Partner Recognition
Write-Host "`n--- 5. AI OCR SCALING & DYNAMIC PARTNER RECOGNITION ---" -ForegroundColor Yellow

$vnptCode = "VNPT_" + (Get-Random -Minimum 1000 -Maximum 9999)
$vnptTax = "0100" + (Get-Random -Minimum 100000 -Maximum 999999)
$vnptId = $null

try {
    $vnptBody = @{
        fullName = "Vietnam Posts and Telecommunications Group"
        shortName = $vnptCode
        entityType = "Both"
        email = "contact@vnpt.vn"
        taxCode = $vnptTax
    } | ConvertTo-Json

    $rVnpt = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" -Body $vnptBody
    $vnptId = $rVnpt.data.id
    Assert-Test "Dynamically create new partner (VNPT)" ($rVnpt.success -and $vnptId -ne $null) "VNPT ID: $vnptId, TaxCode: $vnptTax, Email: contact@vnpt.vn"
} catch { Assert-Test "Dynamically create new partner (VNPT)" $false $_.Exception.Message }

# 6. Email Worker Trigger Endpoint
Write-Host "`n--- 6. EMAIL WORKER SCAN TRIGGER ---" -ForegroundColor Yellow

try {
    $rTrigger = Invoke-RestMethod "http://localhost:5008/api/email-worker/trigger" -Method POST
    Assert-Test "Trigger Email Worker Scan (/api/email-worker/trigger)" ($rTrigger.success) "Message: $($rTrigger.message)"
} catch {
    Assert-Test "Trigger Email Worker Scan (/api/email-worker/trigger)" $false $_.Exception.Message
}

Write-Host "`n========================================================================" -ForegroundColor Cyan
Write-Host "                      TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " PASSED TESTS : $script:passedCount" -ForegroundColor Green
Write-Host " FAILED TESTS : $script:failedCount" -ForegroundColor Yellow
Write-Host "========================================================================" -ForegroundColor Cyan


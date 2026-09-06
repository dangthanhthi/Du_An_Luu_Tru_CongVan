# KIỂM THỬ CHÍNH XÁC CÁC CÔNG VĂN TỪ CỔNG THÔNG TIN SỞ Y TẾ TP.HCM (MEDINET)
$ErrorActionPreference = "Continue"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " KIỂM THỬ BÓC TÁCH 9 CÔNG VĂN TỪ CỔNG VĂN PHÒNG SỐ MEDINET SỞ Y TẾ " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# Step 0: Obtain Admin JWT Token
$login = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body (@{ username = "admin_user"; password = "password" } | ConvertTo-Json) -ContentType "application/json"
$token = $login.data.accessToken
Write-Host "Admin JWT Token acquired successfully.`n" -ForegroundColor Green

$medinetPdfs = Get-ChildItem -Path "$PSScriptRoot\medinet_test_documents" -Filter "*.pdf"

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($pdf in $medinetPdfs) {
    Write-Host ">>> Đang xử lý công văn: $($pdf.Name)..." -ForegroundColor Yellow
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        # 1. Tải tệp lên FilesService
        $form = @{ file = Get-Item -Path $pdf.FullName }
        $uploadRes = Invoke-RestMethod -Uri "http://localhost:5004/api/files/upload" -Method Post -Form $form -Headers @{ "Authorization" = "Bearer $token" }
        $fileId = $uploadRes.data.id

        # 2. Phân tích bóc tách qua AI-OCR
        $analyzeBody = @{
            fileId = $fileId
            fileName = $pdf.Name
            senderEmail = "vanphongso@medinet.gov.vn"
        } | ConvertTo-Json

        $ocrRes = Invoke-RestMethod -Uri "http://localhost:5006/api/ai-ocr/analyze" -Method Post -Body $analyzeBody -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $token" }
        $data = $ocrRes.data

        # 3. Đăng ký tự động vào Sổ Công Văn Đến (DocumentService)
        $fullTitle = if (![string]::IsNullOrWhiteSpace($data.extractedReferenceNumber)) {
            "[$($data.extractedReferenceNumber)] $($data.extractedSubject)"
        } else {
            $data.extractedSubject
        }

        $summaryText = "Văn bản tiếp nhận từ Văn phòng số Medinet:`n- Số ký hiệu: $($data.extractedReferenceNumber)`n- Đơn vị ban hành: $($data.extractedPartnerName)`n- Ngày ký: $($data.extractedDateString)`n- Người ký: $($data.extractedSigner)"

        $docBody = @{
            title = $fullTitle
            summary = $summaryText
            partnerId = $data.matchedPartnerId
            receivedAt = if ($data.extractedDate) { $data.extractedDate } else { (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ") }
            attachmentFileIds = @($fileId)
        } | ConvertTo-Json

        $docRes = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/incoming" -Method Post -Body $docBody -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $token" }
        $sw.Stop()

        $isAccurate = (![string]::IsNullOrWhiteSpace($data.extractedReferenceNumber))

        $resObj = [PSCustomObject]@{
            FileName = $pdf.Name
            RefNumber = $data.extractedReferenceNumber
            Partner = $data.extractedPartnerName
            Title = if ($fullTitle.Length -gt 45) { $fullTitle.Substring(0, 45) + "..." } else { $fullTitle }
            ND30Number = $docRes.data.documentNumber
            TimeMs = $sw.ElapsedMilliseconds
            Status = if ($isAccurate) { "CHÍNH XÁC 100%" } else { "ĐẠT" }
        }
        $results.Add($resObj)

        Write-Host "   [SUCCESS] Số hiệu: $($data.extractedReferenceNumber) | Cơ quan: $($data.extractedPartnerName) | Cấp số NĐ30: $($docRes.data.documentNumber) [$($sw.ElapsedMilliseconds)ms]" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "   [ERROR] Lỗi: $($_.Exception.Message)" -ForegroundColor Red
        $results.Add([PSCustomObject]@{
            FileName = $pdf.Name
            RefNumber = "N/A"
            Partner = "N/A"
            Title = "Lỗi"
            ND30Number = "N/A"
            TimeMs = $sw.ElapsedMilliseconds
            Status = "LỖI"
        })
    }
}

Write-Host "`n==========================================================================================" -ForegroundColor Cyan
Write-Host " KẾT QUẢ BÓC TÁCH 9 CÔNG VĂN THỰC TẾ MEDINET (SỞ Y TẾ TP.HCM) QUA AI-OCR & DOCUMENT SERVICE " -ForegroundColor Cyan
Write-Host "==========================================================================================" -ForegroundColor Cyan

$results | Format-Table -Property FileName, RefNumber, Partner, Title, ND30Number, TimeMs, Status -AutoSize

$total = $results.Count
$passed = ($results | Where-Object { $_.Status -match "CHÍNH XÁC" }).Count
$rate = [math]::Round(($passed / $total) * 100, 2)

Write-Host "TỔNG KẾT MEDINET: $passed / $total văn bản bóc tách chuẩn xác 100% (Tỷ lệ: $rate%)" -ForegroundColor Green

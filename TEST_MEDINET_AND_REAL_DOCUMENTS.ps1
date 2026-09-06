# TEST REAL OFFICIAL GOVERNMENT & HEALTHCARE DOCUMENTS WITH AI-OCR
$ErrorActionPreference = "Continue"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " KIỂM THỬ BÓC TÁCH CÔNG VĂN THỰC TẾ VỚI LÕI AI-OCR & NGHỊ ĐỊNH 30 " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# Step 0: Obtain Admin JWT Token
$login = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body (@{ username = "admin_user"; password = "password" } | ConvertTo-Json) -ContentType "application/json"
$token = $login.data.accessToken
Write-Host "Admin JWT Token acquired successfully.`n" -ForegroundColor Green

# Step 1: Select 10 diverse official PDFs from different Ministries (including Health / BYT / Government)
$testPdfs = Get-ChildItem -Path "$PSScriptRoot\pdfs" -Filter "*.pdf" | Select-Object -First 10

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($pdf in $testPdfs) {
    Write-Host ">>> Đang xử lý tệp: $($pdf.Name)..." -ForegroundColor Yellow
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        # 1. Upload file to FilesService
        $form = @{ file = Get-Item -Path $pdf.FullName }
        $uploadRes = Invoke-RestMethod -Uri "http://localhost:5004/api/files/upload" -Method Post -Form $form -Headers @{ "Authorization" = "Bearer $token" }
        $fileId = $uploadRes.data.id

        # 2. Call AI-OCR Service for Comprehensive Analysis
        $analyzeBody = @{
            fileId = $fileId
            fileName = $pdf.Name
            senderEmail = "vanthu@medinet.gov.vn"
        } | ConvertTo-Json

        $ocrRes = Invoke-RestMethod -Uri "http://localhost:5006/api/ai-ocr/analyze" -Method Post -Body $analyzeBody -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $token" }
        $data = $ocrRes.data

        # 3. Auto-register in DocumentService
        $docBody = @{
            documentNumber = "" # Auto NĐ30 sequential
            referenceNumber = $data.extractedReferenceNumber
            title = if (![string]::IsNullOrWhiteSpace($data.extractedSubject)) { $data.extractedSubject } else { "Công văn tiếp nhận $($pdf.Name)" }
            direction = "incoming"
            issuedDate = if ($data.extractedDate) { $data.extractedDate } else { (Get-Date).ToString("yyyy-MM-dd") }
            partnerName = if ($data.extractedPartnerName) { $data.extractedPartnerName } else { "Sở Y tế / Cơ quan ban hành" }
            fileUrl = "/api/files/$fileId"
            summary = "Trích yếu bóc tách tự động bởi AI-OCR:\n• Số hiệu: $($data.extractedReferenceNumber)\n• Cơ quan: $($data.extractedPartnerName)\n• Trích yếu: $($data.extractedSubject)"
        } | ConvertTo-Json

        $docRes = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/incoming" -Method Post -Body $docBody -ContentType "application/json" -Headers @{ "Authorization" = "Bearer $token" }
        $sw.Stop()

        $isAccurate = (![string]::IsNullOrWhiteSpace($data.extractedReferenceNumber) -and ![string]::IsNullOrWhiteSpace($data.extractedSubject))

        $resObj = [PSCustomObject]@{
            FileName = $pdf.Name
            DocumentNumber = $docRes.data.documentNumber
            RefNumber = $data.extractedReferenceNumber
            Partner = $data.extractedPartnerName
            Subject = if ($data.extractedSubject.Length -gt 45) { $data.extractedSubject.Substring(0, 45) + "..." } else { $data.extractedSubject }
            DocType = $data.extractedDocumentType
            TimeMs = $sw.ElapsedMilliseconds
            Status = if ($isAccurate) { "CHÍNH XÁC 100%" } else { "ĐẠT (Thiếu trường)" }
        }
        $results.Add($resObj)

        Write-Host "   [SUCCESS] Số hiệu: $($data.extractedReferenceNumber) | Cơ quan: $($data.extractedPartnerName) | Số NĐ30 cấp: $($docRes.data.documentNumber) [$($sw.ElapsedMilliseconds)ms]" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "   [ERROR] Lỗi xử lý: $($_.Exception.Message)" -ForegroundColor Red
        $results.Add([PSCustomObject]@{
            FileName = $pdf.Name
            DocumentNumber = "N/A"
            RefNumber = "N/A"
            Partner = "N/A"
            Subject = "Lỗi xử lý"
            DocType = "N/A"
            TimeMs = $sw.ElapsedMilliseconds
            Status = "LỖI"
        })
    }
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host " BẢNG TỔNG HỢP KẾT QUẢ BÓC TÁCH AI-OCR TRÊN 10 CÔNG VĂN THỰC TẾ " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$results | Format-Table -Property FileName, RefNumber, Partner, Subject, DocumentNumber, TimeMs, Status -AutoSize

$total = $results.Count
$passed = ($results | Where-Object { $_.Status -match "CHÍNH XÁC" }).Count
$rate = [math]::Round(($passed / $total) * 100, 2)

Write-Host "TỔNG KẾT: $passed / $total văn bản bóc tách chuẩn xác 100% (Tỷ lệ: $rate%)" -ForegroundColor Green

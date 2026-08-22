$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "     KIỂM THỬ THUẬT TOÁN AI OCR - MULTI-TIER MATCHING & REFERENCE REGEX" -ForegroundColor Cyan
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

# 1. Khởi động AI OCR Service và Partner Service
$baseDir = "C:\Users\Administrator\Desktop\Intern\Du_An_Luu_Tru_CongVan\Intern-DocumentAdministration-BE"
Get-Process -Name "ai-ocr-service", "PartnerService", "AuthService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Start-Process -FilePath "dotnet" -ArgumentList "AuthService.dll --urls=http://localhost:5001" -WorkingDirectory "$baseDir\services\auth-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "PartnerService.dll --urls=http://localhost:5003" -WorkingDirectory "$baseDir\services\partner-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "ai-ocr-service.dll --urls=http://localhost:5006" -WorkingDirectory "$baseDir\services\ai-ocr-service\bin\Debug\net9.0" -WindowStyle Hidden

Start-Sleep -Seconds 4

# Login lấy token
$rSec = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"password"}'
$hSec = @{ Authorization = "Bearer $($rSec.data.accessToken)" }

# Tạo 2 đối tác mới thử nghiệm tính năng scale
$pCode1 = "VIETTEL_" + (Get-Random -Minimum 1000 -Maximum 9999)
$pTax1 = "0100" + (Get-Random -Minimum 100000 -Maximum 999999)
$rPrt1 = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" -Body (@{
    fullName = "Tập đoàn Công nghiệp - Viễn thông Quân đội"
    shortName = "VIETTEL"
    entityType = "Both"
    email = "contact@viettel.com.vn"
    taxCode = $pTax1
} | ConvertTo-Json)

$viettelId = $rPrt1.data.id
Assert-Check "Đăng ký đối tác mới vào PartnerService (Viettel)" ($rPrt1.success -and $viettelId -ne $null) "ID: $viettelId, Tax: $pTax1"

$pCode2 = "FPT_" + (Get-Random -Minimum 1000 -Maximum 9999)
$pTax2 = "0100" + (Get-Random -Minimum 100000 -Maximum 999999)
$rPrt2 = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" -Body (@{
    fullName = "Công ty Cổ phần FPT"
    shortName = "FPT CORP"
    entityType = "Both"
    email = "info@fpt.com.vn"
    taxCode = $pTax2
} | ConvertTo-Json)

$fptId = $rPrt2.data.id
Assert-Check "Đăng ký đối tác mới vào PartnerService (FPT)" ($rPrt2.success -and $fptId -ne $null) "ID: $fptId, Tax: $pTax2"

Write-Host "`n========================================================================" -ForegroundColor Cyan
Write-Host " TỔNG KẾT: ĐẠT $script:passed | LỖI $script:failed" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan


$ErrorActionPreference = "Continue"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "     SETUP DOI TAC MAU CHO 10 FILE CONG VAN TEST" -ForegroundColor Cyan  
Write-Host "========================================================================" -ForegroundColor Cyan

$baseDir = "C:\Users\Administrator\Desktop\Intern\Du_An_Luu_Tru_CongVan\Intern-DocumentAdministration-BE"

Get-Process -Name "DocumentService", "PartnerService", "AuthService", "ai-ocr-service", "EmailWorkerService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Start-Process -FilePath "dotnet" -ArgumentList "AuthService.dll --urls=http://localhost:5001" -WorkingDirectory "$baseDir\services\auth-service\bin\Debug\net9.0" -WindowStyle Hidden
Start-Process -FilePath "dotnet" -ArgumentList "PartnerService.dll --urls=http://localhost:5003" -WorkingDirectory "$baseDir\services\partner-service\bin\Debug\net9.0" -WindowStyle Hidden

Write-Host "Waiting 5s for services..." -ForegroundColor Gray
Start-Sleep -Seconds 5

$rSec = Invoke-RestMethod "http://localhost:5001/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"secretary_user","password":"password"}'
$hSec = @{ Authorization = "Bearer $($rSec.data.accessToken)" }

Write-Host "`n--- CREATING PARTNERS ---" -ForegroundColor Yellow

$partners = @(
    @{ fullName = "Bo Giao duc va Dao tao";           shortName = "BGDDT";    email = "vanthu@moet.gov.vn";    taxCode = "0100100100"; entityType = "Sender" },
    @{ fullName = "Tap doan Buu chinh Vien thong VN";  shortName = "VNPT";     email = "contact@vnpt.vn";       taxCode = "0100684378"; entityType = "Both" },
    @{ fullName = "Cong ty Co phan FPT";               shortName = "FPT CORP"; email = "info@fpt.com.vn";       taxCode = "0101248141"; entityType = "Both" },
    @{ fullName = "Tap doan Cong nghiep Vien thong Quan doi"; shortName = "VIETTEL"; email = "contact@viettel.com.vn"; taxCode = "0100109106"; entityType = "Both" },
    @{ fullName = "Uy ban Nhan dan TP Ho Chi Minh";    shortName = "UBND TPHCM"; email = "ubnd@tphcm.gov.vn";   taxCode = "0301177741"; entityType = "Sender" },
    @{ fullName = "Bo Cong an";                        shortName = "BCA";      email = "vanthu@mps.gov.vn";     taxCode = "0106092071"; entityType = "Sender" },
    @{ fullName = "Samsung Electronics Vietnam";       shortName = "SAMSUNG";  email = "office@samsung.com";    taxCode = "0200478223"; entityType = "Sender" },
    @{ fullName = "Tap doan Dien luc Viet Nam";        shortName = "EVN";      email = "vp@evn.com.vn";         taxCode = "0100100079"; entityType = "Both" },
    @{ fullName = "Bao hiem Xa hoi Viet Nam";          shortName = "BHXH VN";  email = "bhxhvn@vss.gov.vn";     taxCode = "0100100322"; entityType = "Sender" },
    @{ fullName = "Cong ty TNHH Thuong mai ABC";       shortName = "ABC CO";   email = "lienhe@abccompany.vn";  taxCode = "0316789456"; entityType = "Both" }
)

foreach ($p in $partners) {
    try {
        $body = $p | ConvertTo-Json
        $res = Invoke-RestMethod "http://localhost:5003/api/partners" -Method POST -Headers $hSec -ContentType "application/json" -Body $body
        if ($res.success) {
            Write-Host " [OK] $($p.shortName) - ID: $($res.data.id)" -ForegroundColor Green
        }
    } catch {
        Write-Host " [SKIP] $($p.shortName) - may exist" -ForegroundColor Yellow
    }
}

Write-Host "`n--- DONE ---" -ForegroundColor Cyan

# ==========================================================
#    DAS - HE THONG QUAN LY VA LUU TRU CONG VAN (POWERSHELL LAUNCHER)
# ==========================================================

$rootDir = $PSScriptRoot
if (-not $rootDir) { $rootDir = (Get-Location).Path }

$jwtSecret = "DAS_SECRET_KEY_FOR_LOCAL_DEV_AT_LEAST_32_BYTES_LONG"

# 1. Kiem tra .env.local cho Frontend
$envLocal = Join-Path $rootDir "frontend\.env.local"
$envExample = Join-Path $rootDir "frontend\.env.example"
if (-not (Test-Path $envLocal)) {
    if (Test-Path $envExample) {
        Write-Host "[*] Dang tao file frontend\.env.local tu .env.example..." -ForegroundColor Cyan
        Copy-Item $envExample $envLocal
        Write-Host "[OK] Da tao frontend\.env.local thanh cong." -ForegroundColor Green
    }
} else {
    Write-Host "[OK] File frontend\.env.local da san sang." -ForegroundColor Green
}

# 2. Kiem tra node_modules cho Frontend
$nodeModules = Join-Path $rootDir "frontend\node_modules"
if (-not (Test-Path $nodeModules)) {
    Write-Host "[*] Dang cai dat dependencies cho Frontend lan dau (mat khoang 1-2 phut)..." -ForegroundColor Cyan
    Push-Location (Join-Path $rootDir "frontend")
    npm ci --ignore-scripts --no-audit --no-fund
    npm run generate:prisma
    npx next typegen
    Pop-Location
    Write-Host "[OK] Da chuan bi xong dependencies Frontend." -ForegroundColor Green
} else {
    Write-Host "[OK] Thu vien Frontend (node_modules) da san sang." -ForegroundColor Green
}

# 3. Khoi dong 4 cua so PowerShell rieng biet
Write-Host "[1/4] Dang mo Auth Service (Port 5001)..." -ForegroundColor Yellow
$authCmd = @"
`$host.UI.RawUI.WindowTitle = 'DAS [1/4] - Auth Service (:5001)';
`$env:ASPNETCORE_ENVIRONMENT='Development';
`$env:ASPNETCORE_URLS='http://localhost:5001';
`$env:Database__Provider='Sqlite';
`$env:ConnectionStrings__Default='Data Source=local-auth.db';
`$env:Database__Initialize='true';
`$env:Database__SeedDemoUsers='true';
`$env:Jwt__Secret='$jwtSecret';
Write-Host '=== [1/4] AUTH SERVICE (:5001) - SQLITE & DEMO ACCOUNTS ===' -ForegroundColor Cyan;
Write-Host 'Demo: secretary_user / password | admin_user / password' -ForegroundColor Green;
Set-Location '$rootDir\backend\services\auth-service';
dotnet run;
"@
Start-Process powershell -ArgumentList "-NoExit", "-Command", $authCmd

Write-Host "[2/4] Dang mo Document Service (Port 5002)..." -ForegroundColor Yellow
$docCmd = @"
`$host.UI.RawUI.WindowTitle = 'DAS [2/4] - Document Service (:5002)';
`$env:ASPNETCORE_ENVIRONMENT='Development';
`$env:ASPNETCORE_URLS='http://localhost:5002';
`$env:Database__Provider='Sqlite';
`$env:ConnectionStrings__Default='Data Source=local-document.db';
`$env:Database__Initialize='true';
`$env:Jwt__Secret='$jwtSecret';
Write-Host '=== [2/4] DOCUMENT SERVICE (:5002) - SQLITE ===' -ForegroundColor Cyan;
Set-Location '$rootDir\backend\services\document-service';
dotnet run;
"@
Start-Process powershell -ArgumentList "-NoExit", "-Command", $docCmd

Write-Host "[3/4] Dang mo API Gateway (Port 8080)..." -ForegroundColor Yellow
$gatewayCmd = @"
`$host.UI.RawUI.WindowTitle = 'DAS [3/4] - API Gateway (:8080)';
`$env:ASPNETCORE_ENVIRONMENT='Development';
`$env:ASPNETCORE_URLS='http://localhost:8080';
`$env:Jwt__Secret='$jwtSecret';
Write-Host '=== [3/4] API GATEWAY OCELOT (:8080) ===' -ForegroundColor Cyan;
Set-Location '$rootDir\backend\gateway';
dotnet run;
"@
Start-Process powershell -ArgumentList "-NoExit", "-Command", $gatewayCmd

Write-Host "[4/4] Dang mo Frontend Next.js (Port 3000)..." -ForegroundColor Yellow
$frontendCmd = @"
`$host.UI.RawUI.WindowTitle = 'DAS [4/4] - Frontend Next.js (:3000)';
Write-Host '=== [4/4] FRONTEND NEXT.JS (:3000) ===' -ForegroundColor Cyan;
Set-Location '$rootDir\frontend';
npm run dev;
"@
Start-Process powershell -ArgumentList "-NoExit", "-Command", $frontendCmd

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " DA KHOI CHAY 4 TIEN TRINH TRONG 4 CUA SO POWERSHELL RIENG:" -ForegroundColor Green
Write-Host "  1. Auth Service     -> http://localhost:5001"
Write-Host "  2. Document Service -> http://localhost:5002"
Write-Host "  3. Gateway (Ocelot) -> http://localhost:8080"
Write-Host "  4. Frontend         -> http://localhost:3000"
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "LUU Y: Vui long cho khoang 5-10 giay de ca 4 cua so hien dong 'Now listening on' hoac 'Ready'." -ForegroundColor Yellow
Write-Host "Sau do mo trinh duyet truy cap: http://localhost:3000/vi/login" -ForegroundColor Cyan
Write-Host "Tai khoan demo: secretary_user / password" -ForegroundColor Gray

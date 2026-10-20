@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Development

echo ========================================================
echo   KHOI DONG HE THONG QUAN LY CONG VAN (DAS) - NATIVE
echo ========================================================

echo [1/4] Khoi dong AuthService (Port 5001)...
start "DAS - AuthService (Port 5001)" /D "%~dp0backend\services\auth-service" cmd /k dotnet run --environment Development --urls http://localhost:5001

echo [2/4] Khoi dong DocumentService (Port 5002)...
start "DAS - DocumentService (Port 5002)" /D "%~dp0backend\services\document-service" cmd /k dotnet run --environment Development --urls http://localhost:5002

echo [3/4] Khoi dong Gateway (Port 8080)...
start "DAS - Gateway (Port 8080)" /D "%~dp0backend\gateway" cmd /k dotnet run --environment Development --urls http://localhost:8080

echo [4/4] Khoi dong Frontend Next.js (Port 3000)...
start "DAS - Frontend (Port 3000)" /D "%~dp0frontend" cmd /k npm run dev

echo.
echo ========================================================
echo   TAT CA DICH VU DANG DUOC KHOI CHAY TRONG CAC CUA SO!
echo   Vui long doi 5-10 giay de he thong nap xong.
echo.
echo   - Link truy cap: http://localhost:3000
echo   - Tai khoan Admin: admin@das.vn (hoac admin) / password
echo   - Tai khoan Thu ky: secretary@das.vn / password
echo ========================================================
echo.

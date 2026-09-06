@echo off
setlocal EnableDelayedExpansion
chcp 65001 > nul
title KHOI DONG HE THONG QUAN LY CONG VAN (DAS)

echo ==============================================================================
echo        KHOI DONG HE THONG QUAN LY CONG VAN VA VAN THU SO (AI-OCR)
echo ==============================================================================
echo.

:: 1. Giai phong cac cong port cu de tranh xung dot
echo [*] Dang don dep tien trinh cu tren cac cong port...
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5001,5002,5003,5004,5005,5006,5007,8080,3000 -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -gt 4 } | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"

:: 2. Xac dinh thu muc Backend va Frontend
set "ROOT_DIR=%~dp0"
set "BE_DIR=%ROOT_DIR%Intern-DocumentAdministration-BE"
set "FE_DIR=%ROOT_DIR%Intern-DocumentAdministration-FE-Web"

if not exist "%BE_DIR%" if exist "%ROOT_DIR%services" set "BE_DIR=%ROOT_DIR%"
if not exist "%FE_DIR%" if exist "%ROOT_DIR%DAS-Frontend" set "FE_DIR=%ROOT_DIR%DAS-Frontend"

:: 3. Kiem tra va cai dat npm dependencies cho Frontend neu can
if exist "%FE_DIR%" (
    if not exist "%FE_DIR%\node_modules" (
        echo [*] Phat hien Frontend chua co thu vien node_modules, dang tu dong cai dat...
        cd /d "%FE_DIR%"
        call npm install --legacy-peer-deps
    )
)

:: 4. Build Backend sieu toc 1 lan duy nhat
cd /d "%BE_DIR%"
echo [*] Dang kiem tra build Backend...
dotnet build DocumentAdministration.slnx --verbosity quiet -m

echo.
echo [1/9] Khoi chay API Gateway (Port 8080)...
start "API-Gateway [8080]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project gateway/Gateway.csproj --no-build --launch-profile http"

echo [2/9] Khoi chay Auth Service (Port 5001)...
start "Auth-Service [5001]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/auth-service/AuthService.csproj --no-build --launch-profile http"

echo [3/9] Khoi chay Document Service (Port 5002)...
start "Document-Service [5002]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/document-service/DocumentService.csproj --no-build --launch-profile http"

echo [4/9] Khoi chay Partner Service (Port 5003)...
start "Partner-Service [5003]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/partner-service/PartnerService.csproj --no-build --launch-profile http"

echo [5/9] Khoi chay Files Service (Port 5004)...
start "Files-Service [5004]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/files-service/FileService.API.csproj --no-build --launch-profile http"

echo [6/9] Khoi chay Notification Service (Port 5005)...
start "Notification-Service [5005]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/notification-service/NotificationService.csproj --no-build --launch-profile http"

echo [7/9] Khoi chay AI-OCR Service (Port 5006)...
start "AI-OCR-Service [5006]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/ai-ocr-service/ai-ocr-service.csproj --no-build --launch-profile http"

echo [8/9] Khoi chay Email Worker Service (Port 5007)...
start "Email-Worker [5007]" cmd /k "cd /d ""%BE_DIR%"" && dotnet run --project services/email-worker-service/EmailWorkerService.csproj --no-build --launch-profile http"

echo [9/9] Khoi chay Frontend Next.js (Port 3000)...
start "Frontend-Webapp [3000]" cmd /k "cd /d ""%FE_DIR%"" && npm run dev"

echo.
echo ==============================================================================
echo    TAT CA DICH VU DA KHOI CHAY THANH CONG!
echo.
echo    - Trinh duyet Webapp:  http://localhost:3000/vi/dashboards/overview
echo    - API Gateway:         http://localhost:8080
echo    - Bao cao Unhappy:     http://localhost:3000/Bang_Kiem_Thu_Unhappy_Cases_DAS.html
echo ==============================================================================
echo.

ping 127.0.0.1 -n 4 > nul
start http://localhost:3000/vi/dashboards/overview

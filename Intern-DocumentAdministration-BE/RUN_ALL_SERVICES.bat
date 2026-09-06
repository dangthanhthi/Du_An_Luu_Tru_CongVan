@echo off
chcp 65001 > nul
echo ========================================================
echo   KHOI CHAY TOAN BO HE THONG BACKEND (PORT 8080 & MICROSERVICES)
echo ========================================================
echo.

cd /d "%~dp0"

echo [1/8] Khoi chay API Gateway (Port 8080)...
start "Gateway 8080" cmd /k "dotnet run --project gateway/Gateway.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [2/8] Khoi chay Auth Service (Port 5001)...
start "Auth Service 5001" cmd /k "dotnet run --project services/auth-service/AuthService.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [3/8] Khoi chay Document Service (Port 5002)...
start "Document Service 5002" cmd /k "dotnet run --project services/document-service/DocumentService.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [4/8] Khoi chay Partner Service (Port 5003)...
start "Partner Service 5003" cmd /k "dotnet run --project services/partner-service/PartnerService.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [5/8] Khoi chay Files Service (Port 5004)...
start "Files Service 5004" cmd /k "dotnet run --project services/files-service/FileService.API.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [6/8] Khoi chay Notification Service (Port 5005)...
start "Notification Service 5005" cmd /k "dotnet run --project services/notification-service/NotificationService.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [7/8] Khoi chay AI-OCR Service (Port 5006)...
start "AI-OCR Service 5006" cmd /k "dotnet run --project services/ai-ocr-service/ai-ocr-service.csproj --launch-profile http"

timeout /t 2 /nobreak > nul

echo [8/8] Khoi chay Email Worker Service (Port 5007)...
start "Email Worker Service 5007" cmd /k "dotnet run --project services/email-worker-service/EmailWorkerService.csproj --launch-profile http"

echo.
echo ========================================================
echo   TAT CA DICH VU BACKEND DA DUOC KHOI CHAY THANH CONG!
echo   API Gateway dang lang nghe tai: http://localhost:8080
echo ========================================================
pause

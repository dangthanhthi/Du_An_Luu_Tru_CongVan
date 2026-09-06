@echo off
chcp 65001 > nul
title HE THONG BACKEND MICROSERVICES - INTERN DOCUMENT ADMINISTRATION

:: Thiet lap JWT Secret cho moi truong Local Development
if "%Jwt__Secret%"=="" set "Jwt__Secret=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"
if "%JWT_SECRET%"=="" set "JWT_SECRET=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"

echo ==============================================================================
echo       KHỞI ĐỘNG TOÀN BỘ CÁC DỊCH VỤ BACKEND MICROSERVICES (.NET 9/10)
echo ==============================================================================
echo.

if exist "%~dp0gateway" (
    cd /d "%~dp0"
) else if exist "%~dp0Intern-DocumentAdministration-BE\gateway" (
    cd /d "%~dp0Intern-DocumentAdministration-BE"
) else (
    cd /d "%~dp0"
)

echo [1/8] Đang khởi động Auth Service (Port 5001)...
start "Auth-Service [Port 5001]" cmd /k "dotnet run --project services/auth-service/AuthService.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [2/8] Đang khởi động Document Service (Port 5002)...
start "Document-Service [Port 5002]" cmd /k "dotnet run --project services/document-service/DocumentService.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [3/8] Đang khởi động Partner Service (Port 5003)...
start "Partner-Service [Port 5003]" cmd /k "dotnet run --project services/partner-service/PartnerService.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [4/8] Đang khởi động Files Service (Port 5004)...
start "Files-Service [Port 5004]" cmd /k "dotnet run --project services/files-service/FileService.API.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [5/8] Đang khởi động Notification Service (Port 5005)...
start "Notification-Service [Port 5005]" cmd /k "dotnet run --project services/notification-service/NotificationService.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [6/8] Đang khởi động AI-OCR Service (Port 5006)...
start "AI-OCR-Service [Port 5006]" cmd /k "dotnet run --project services/ai-ocr-service/ai-ocr-service.csproj --launch-profile \"http\""

timeout /t 2 /nobreak > nul

echo [7/8] Đang khởi động Email Worker Service (Background Worker)...
start "Email-Worker-Service [Background]" cmd /k "dotnet run --project services/email-worker-service/EmailWorkerService.csproj"

timeout /t 2 /nobreak > nul

echo [8/8] Đang khởi động API Gateway Ocelot (Port 5000)...
start "API-Gateway [Port 5000]" cmd /k "dotnet run --project gateway/Gateway.csproj --launch-profile \"http\""

echo.
echo ==============================================================================
echo    TẤT CẢ 8 DỊCH VỤ BACKEND ĐÃ ĐƯỢC KHỞI ĐỘNG THÀNH CÔNG!
echo.
echo    - API Gateway      : http://localhost:5000
echo    - Auth Service     : http://localhost:5001/swagger
echo    - Document Service : http://localhost:5002/swagger
echo    - Partner Service  : http://localhost:5003/swagger
echo    - Files Service    : http://localhost:5004/swagger
echo    - Notification Svc : http://localhost:5005/swagger
echo    - AI-OCR Service   : http://localhost:5006/swagger
echo ==============================================================================
echo.
pause

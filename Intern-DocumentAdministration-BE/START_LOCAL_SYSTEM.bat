@echo off
chcp 65001 > nul
title KHOI DONG TOAN BO HE THONG (FRONTEND + BACKEND)

:: Thiet lap JWT Secret cho moi truong Local Development
if "%Jwt__Secret%"=="" set "Jwt__Secret=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"
if "%JWT_SECRET%"=="" set "JWT_SECRET=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"

echo ==============================================================================
echo       KHOI DONG HE THONG QUAN LY CONG VAN VA VAN THU SO [AI-OCR]
echo ==============================================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [CANH BAO] May chua co .NET SDK. Dang tu dong goi trinh cai dat...
    call "%~dp0INSTALL_PREREQUISITES.bat"
)

where node >nul 2>nul
if %errorlevel% neq 0 (
    echo [CANH BAO] May chua co Node.js. Dang tu dong goi trinh cai dat...
    call "%~dp0INSTALL_PREREQUISITES.bat"
)

:: Tu dong tim thu muc Backend
if exist "%~dp0Intern-DocumentAdministration-BE" (
    set "BE_DIR=%~dp0Intern-DocumentAdministration-BE"
) else if exist "%~dp0gateway" (
    set "BE_DIR=%~dp0"
) else (
    set "BE_DIR=%~dp0Intern-DocumentAdministration-BE"
)

:: Tu dong tim thu muc Frontend
if exist "%~dp0Intern-DocumentAdministration-FE-Web" (
    set "FE_DIR=%~dp0Intern-DocumentAdministration-FE-Web"
) else if exist "%~dp0..\Intern-DocumentAdministration-FE-Web" (
    set "FE_DIR=%~dp0..\Intern-DocumentAdministration-FE-Web"
) else if exist "%~dp0DAS-Frontend" (
    set "FE_DIR=%~dp0DAS-Frontend"
) else (
    set "FE_DIR=%~dp0Intern-DocumentAdministration-FE-Web"
)

pushd "%BE_DIR%"

echo [1/9] Khoi chay API Gateway (Port 8080)...
start "API-Gateway [8080]" cmd /k "cd gateway && dotnet run --launch-profile http"

ping 127.0.0.1 -n 3 > nul

echo [2/9] Khoi chay Auth Service (Port 5001)...
start "Auth-Service [5001]" cmd /k "cd services\auth-service && dotnet run --launch-profile http"

echo [3/9] Khoi chay Document Service (Port 5002)...
start "Document-Service [5002]" cmd /k "cd services\document-service && dotnet run --launch-profile http"

echo [4/9] Khoi chay Partner Service (Port 5003)...
start "Partner-Service [5003]" cmd /k "cd services\partner-service && dotnet run --launch-profile http"

echo [5/9] Khoi chay Files Service (Port 5004)...
start "Files-Service [5004]" cmd /k "cd services\files-service && dotnet run --launch-profile http"

echo [6/9] Khoi chay Notification Service (Port 5005)...
start "Notification-Service [5005]" cmd /k "cd services\notification-service && dotnet run --launch-profile http"

echo [7/9] Khoi chay AI-OCR Service (Port 5006)...
start "AI-OCR-Service [5006]" cmd /k "cd services\ai-ocr-service && dotnet run --launch-profile http"

echo [8/9] Khoi chay Email Worker Service (Port 5007)...
start "Email-Worker [5007]" cmd /k "cd services\email-worker-service && dotnet run --launch-profile http"

ping 127.0.0.1 -n 4 > nul

popd
pushd "%FE_DIR%"

if not exist "%FE_DIR%\.env" (
    if exist "%FE_DIR%\.env.example" (
        copy "%FE_DIR%\.env.example" "%FE_DIR%\.env" > nul
    )
)

if not exist "%FE_DIR%\node_modules" (
    echo.
    echo ==============================================================================
    echo  [LAN DAU CHAY TREN MAY MOI] Dang tu dong cai dat thu vien Frontend [npm install]...
    echo ==============================================================================
    call npm install
)

echo [9/9] Khoi chay Frontend Next.js (Port 3000)...
start "Frontend-Webapp [3000]" cmd /k "npm run dev"

ping 127.0.0.1 -n 4 > nul

echo.
echo ==============================================================================
echo    TẤT CẢ DỊCH VỤ ĐÃ KHỞI CHẠY THÀNH CÔNG!
echo.
echo    - Trinh duyet Webapp:  http://localhost:3000
echo    - API Gateway:         http://localhost:8080
echo ==============================================================================
echo.

start http://localhost:3000

pause

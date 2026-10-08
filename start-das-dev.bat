@echo off
setlocal enabledelayedexpansion
title DAS - System Launcher

echo ==========================================================
echo    DAS - HE THONG QUAN LY VA LUU TRU CONG VAN (LOCAL DEV)
echo ==========================================================
echo.

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"
set "JWT_SECRET=DAS_SECRET_KEY_FOR_LOCAL_DEV_AT_LEAST_32_BYTES_LONG"

:: 1. Kiem tra va tao file .env.local cho Frontend neu chua co
if not exist "%ROOT_DIR%\frontend\.env.local" (
    if exist "%ROOT_DIR%\frontend\.env.example" (
        echo [*] Dang tao file frontend\.env.local tu .env.example...
        copy "%ROOT_DIR%\frontend\.env.example" "%ROOT_DIR%\frontend\.env.local" >nul
        echo [OK] Da tao frontend\.env.local thanh cong.
    ) else (
        echo [!] Canh bao: Khong tim thay frontend\.env.example!
    )
) else (
    echo [OK] File frontend\.env.local da san sang.
)

:: 2. Kiem tra va cai dat dependencies cho Frontend neu chua co
if not exist "%ROOT_DIR%\frontend\node_modules" (
    echo [*] Dang cai dat dependencies cho Frontend lan dau - vui long doi 1-2 phut...
    pushd "%ROOT_DIR%\frontend"
    call npm ci --ignore-scripts --no-audit --no-fund
    call npm run generate:prisma
    call npx next typegen
    popd
    echo [OK] Da chuan bi xong thu vien cho Frontend.
) else (
    echo [OK] Thu vien Frontend node_modules da san sang.
)
echo.

:: 3. Cua so 1: Auth Service (Port 5001)
echo [1/4] Dang mo cua so Auth Service (Port 5001)...
start "DAS [1/4] - Auth Service (:5001)" cmd /k "cd /d ""%ROOT_DIR%\backend\services\auth-service"" && set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:5001 && set Database__Provider=Sqlite && set ""ConnectionStrings__Default=Data Source=local-auth.db"" && set Database__Initialize=true && set Database__SeedDemoUsers=true && set ""Jwt__Secret=%JWT_SECRET%"" && echo === [1/4] AUTH SERVICE (:5001) - SQLITE ^& DEMO ACCOUNTS === && echo Tai khoan demo: secretary_user / password ^| admin_user / password && dotnet run"

:: 4. Cua so 2: Document Service (Port 5002)
echo [2/4] Dang mo cua so Document Service (Port 5002)...
start "DAS [2/4] - Document Service (:5002)" cmd /k "cd /d ""%ROOT_DIR%\backend\services\document-service"" && set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:5002 && set Database__Provider=Sqlite && set ""ConnectionStrings__Default=Data Source=local-document.db"" && set Database__Initialize=true && set ""Jwt__Secret=%JWT_SECRET%"" && echo === [2/4] DOCUMENT SERVICE (:5002) - SQLITE === && dotnet run"

:: 5. Cua so 3: API Gateway (Port 8080)
echo [3/4] Dang mo cua so API Gateway (Port 8080)...
start "DAS [3/4] - API Gateway (:8080)" cmd /k "cd /d ""%ROOT_DIR%\backend\gateway"" && set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:8080 && set ""Jwt__Secret=%JWT_SECRET%"" && echo === [3/4] API GATEWAY OCELOT (:8080) === && dotnet run"

:: 6. Cua so 4: Frontend Next.js (Port 3000)
echo [4/4] Dang mo cua so Frontend Next.js (Port 3000)...
start "DAS [4/4] - Frontend Next.js (:3000)" cmd /k "cd /d ""%ROOT_DIR%\frontend"" && echo === [4/4] FRONTEND NEXT.JS (:3000) === && npm run dev"

echo.
echo ----------------------------------------------------------
echo  DA KHOI CHAY 4 TIEN TRINH TRONG 4 CUA SO CMD RIENG BIET:
echo  1. Auth Service     -^> http://localhost:5001
echo  2. Document Service -^> http://localhost:5002
echo  3. Gateway (Ocelot) -^> http://localhost:8080
echo  4. Frontend         -^> http://localhost:3000
echo ----------------------------------------------------------
echo.
echo LUU Y QUAN TRONG:
echo  - Vui long cho khoang 5-10 giay de ca 4 cua so hien dong 'Ready' hoac 'Now listening on'.
echo  - Sau do moi mo trinh duyet truy cap: http://localhost:3000/vi/login
echo.
echo Tai khoan demo:
echo  - Thu ky:    secretary_user / password
echo  - Quan tri:  admin_user     / password
echo  - Nhan vien: employee_user  / password
echo.
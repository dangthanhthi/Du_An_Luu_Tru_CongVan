@echo off
chcp 65001 > nul
title HE THONG QUAN LY CONG VAN & AI OCR (DAS)

echo ======================================================================
echo    KHỞI ĐỘNG HỆ THỐNG QUẢN LÝ VĂN THƯ LƯU TRỮ CÔNG VĂN (DAS)
echo ======================================================================
echo.

echo [1/2] Đang khởi động Backend AI OCR Service (Port 5006)...
start "AI-OCR-Service [Port 5006]" cmd /k "dotnet run --project Intern-DocumentAdministration-BE/services/ai-ocr-service/ai-ocr-service.csproj --launch-profile \"http\""

timeout /t 3 /nobreak > nul

echo [2/2] Đang khởi động Frontend Webapp (Port 3000)...
start "DAS-Frontend [Port 3000]" cmd /k "cd DAS-Frontend && npm run dev"

echo.
echo ======================================================================
echo    HỆ THỐNG ĐÃ KHỞI ĐỘNG THÀNH CÔNG!
echo    - Frontend Webapp: http://localhost:3000
echo    - AI OCR Swagger : http://localhost:5006/swagger
echo ======================================================================
echo.
pause

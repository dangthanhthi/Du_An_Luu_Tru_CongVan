@echo off
chcp 65001 > nul
title DỪNG TOÀN BỘ HỆ THỐNG

echo Dang dung tat ca cac tien trinh Backend va Frontend (Ports 5001-5007, 8080, 3000)...

powershell -Command "Get-NetTCPConnection -LocalPort 5001,5002,5003,5004,5005,5006,5007,8080,3000 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"

taskkill /F /IM Gateway.exe 2>nul
taskkill /F /IM AuthService.exe 2>nul
taskkill /F /IM DocumentService.exe 2>nul
taskkill /F /IM PartnerService.exe 2>nul
taskkill /F /IM FileService.API.exe 2>nul
taskkill /F /IM NotificationService.exe 2>nul
taskkill /F /IM ai-ocr-service.exe 2>nul
taskkill /F /IM EmailWorkerService.exe 2>nul
taskkill /F /IM node.exe 2>nul

echo.
echo ==============================================================================
echo    ĐÃ DỪNG TOÀN BỘ CÁC DỊCH VỤ THÀNH CÔNG!
echo ==============================================================================
pause

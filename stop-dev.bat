@echo off
chcp 65001 >nul
echo Dang dung cac tien trinh DAS...
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM AuthService.exe 2>nul
taskkill /F /IM DocumentService.exe 2>nul
taskkill /F /IM Gateway.exe 2>nul
taskkill /F /IM node.exe 2>nul
echo Da dung toan bo cac dich vu DAS!

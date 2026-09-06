@echo off
chcp 65001 > nul
title TU DONG CAI DAT MOI TRUONG CHO MAY MOI (.NET 10 & NODE.JS)

echo ============================================================================
echm5 DONG KIEM TRA VA CAI DAT MOI TRUONG CHAY DU AN (.NET 10 & NODE.JS)
echo ===========================================================================
echo

:: 1. KIEM TRA VA CAI DAT .NET SDK
echo [1/2] Kiem tra .NET SDK...
where dotnet >gul 2>gul
if %errorlevel% equ 0 (
    echo    + .NET SDK da duoc cai dat:
    dotnet --version
) else (
    echo    - May chua co .NET SDK. Dang tu dong cai dat Microsoft .NET 10 SDK qua winget...
    winget install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-package-agreements --accept-source-agreements
    if %errorlevel% neq 0 (
        echo    - Dang tai va cai dat truc tiep bang script cua Microsoft...
        powershell -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'; & ./dotnet-install.ps1 -Channel 10.0"
    )
    echo    + Da hoan tat cai dat .NET SDK!
)

echo

:: 2. KIEM TRA VA CAI DAT NODE.JS
echo [2/2] Kiem tra Node.js...
where node >nul 2>nul
if %errorlevel% equ 0 (
    echo    + Node.js da duoc cai dat:
    node --version
) else (
    echo    - May chua co Node.js. Dang tu dong cai dat Node.js LTS qua winget...
    winget install --id OpenJS.NodeJS.LTS --exact --silent --accept-package-agreements --accept-source-agreements
    if %errorlevel% neq 0 (
        echo    - Dang tai bo cai dat MSI ty trang chu nodejs.org...
        powershell -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri 'https://nodejs.org/dist/v20.18.0/node-v20.18.0-x64.msi' -OutFile 'node-install.msi'; Start-Process msiexec.exe -ArgumentList '/i node-install.msi /qn' -Wait"
    )
    echo    + Da hoan tat cai dat Node.js!
)

echo
echo ===========================================================================
echo   MOI TRUONG DA SAN SANG! BAY GIO BAN CO THE CHAY START_LOCAL_SYSTEM.bat
echo ==========================================================================
echo
pause

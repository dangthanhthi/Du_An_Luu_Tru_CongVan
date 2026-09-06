@echo off
chcp 65001 > nul
title KHOI DONG TOAN BO HE THONG TEST

echo ==============================================================================
echo   HE THONG THU NGHIEM DAS
echo   Backend:  feat/be-complete
echo   Frontend: ocr-fix
echo ==============================================================================
echo.

pushd "%~dp0Intern-DocumentAdministration-BE"
call START_LOCAL_SYSTEM.bat
popd

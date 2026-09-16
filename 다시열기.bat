@echo off
chcp 65001 >nul
title 쏙캡처 다시 열기
echo 쏙캡처가 차단됐을 때 실행하세요. 재빌드로 해시를 바꿔가며 실행을 시도합니다.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0다시열기.ps1"
echo.
pause

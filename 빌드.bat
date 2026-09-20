@echo off
chcp 65001 > nul
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /out:"소울곰 캡처.exe" ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "src\SsokCapture.cs"

if errorlevel 1 (
  echo.
  echo 빌드 실패
) else (
  echo.
  echo 빌드 완료: 소울곰 캡처.exe
)
pause

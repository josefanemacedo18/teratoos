@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem  Téra Tools - instalador para Revit 2027
rem  Dê dois cliques neste arquivo com o Revit FECHADO.
rem ============================================================

set "ORIGEM=%~dp0"
set "DESTINO=%APPDATA%\Autodesk\Revit\Addins\2027"

echo.
echo  Instalando Téra Tools para Revit 2027...
echo  Destino: %DESTINO%
echo.

tasklist /FI "IMAGENAME eq Revit.exe" 2>nul | find /I "Revit.exe" >nul
if not errorlevel 1 (
  echo  ATENCAO: o Revit esta aberto. Feche o Revit e execute novamente.
  echo.
  pause
  exit /b 1
)

if not exist "%ORIGEM%TeraTools\TeraTools.dll" (
  echo  ERRO: a pasta "TeraTools" nao foi encontrada ao lado deste arquivo.
  echo  Extraia o arquivo .zip inteiro antes de executar o instalador.
  echo.
  pause
  exit /b 1
)

if not exist "%DESTINO%" mkdir "%DESTINO%"
if exist "%DESTINO%\TeraTools" rmdir /S /Q "%DESTINO%\TeraTools"

xcopy /E /I /Y /Q "%ORIGEM%TeraTools" "%DESTINO%\TeraTools" >nul
copy /Y "%ORIGEM%TeraTools.addin" "%DESTINO%\TeraTools.addin" >nul

rem Remove o bloqueio "arquivo baixado da internet" do Windows.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%DESTINO%\TeraTools' -Recurse | Unblock-File; Unblock-File -LiteralPath '%DESTINO%\TeraTools.addin'" >nul 2>&1

if exist "%DESTINO%\TeraTools\TeraTools.dll" (
  echo  Pronto! Abra o Revit 2027 e procure a aba "Téra Tools".
) else (
  echo  ERRO: a copia dos arquivos falhou.
)
echo.
pause

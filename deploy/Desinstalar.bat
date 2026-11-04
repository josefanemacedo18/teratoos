@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem  Téra Tools - desinstalador (Revit 2027)
rem  Suas configurações e presets ficam guardados em
rem  %APPDATA%\TeraTools e NÃO são apagados.
rem ============================================================

set "DESTINO=%APPDATA%\Autodesk\Revit\Addins\2027"

tasklist /FI "IMAGENAME eq Revit.exe" 2>nul | find /I "Revit.exe" >nul
if not errorlevel 1 (
  echo  ATENCAO: o Revit esta aberto. Feche o Revit e execute novamente.
  pause
  exit /b 1
)

if exist "%DESTINO%\TeraTools.addin" del /F /Q "%DESTINO%\TeraTools.addin"
if exist "%DESTINO%\TeraTools" rmdir /S /Q "%DESTINO%\TeraTools"

echo  Téra Tools removido do Revit 2027.
echo  (Configuracoes preservadas em %APPDATA%\TeraTools)
pause

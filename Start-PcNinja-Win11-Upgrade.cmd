@echo off
setlocal

cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0src\PcNinja-Win11-Upgrade-Assistant.ps1"
set "SCRIPT_EXIT_CODE=%errorlevel%"

if not "%SCRIPT_EXIT_CODE%"=="0" (
    echo.
    echo PcNinja Windows 11 Upgrade Assistant stopped with exit code %SCRIPT_EXIT_CODE%.
    echo Review the log in C:\PcNinja\Win11UpgradeAssistant.
    pause
)

exit /b %SCRIPT_EXIT_CODE%


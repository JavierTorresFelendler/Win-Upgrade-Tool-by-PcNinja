@echo off
setlocal

set "SETUP_PATH=%~1"

if not defined SETUP_PATH (
    echo ERROR: No setup.exe path was supplied.
    exit /b 2
)

if not exist "%SETUP_PATH%" (
    echo ERROR: setup.exe was not found at "%SETUP_PATH%".
    exit /b 3
)

echo.
echo ============================================================
echo PcNinja Windows 11 Upgrade Assistant
echo ============================================================
echo.
echo Starting the Windows 11 upgrade...
echo Windows Setup will control the restart process.
echo Do not turn off the computer during the upgrade.
echo.

REM Keep this command identical to the known-good upgrade command.
REM Windows Setup controls the natural restart; do not add shutdown here.
start /wait "" "%SETUP_PATH%" /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning

set "SETUP_EXIT_CODE=%errorlevel%"

echo.
echo Windows Setup exit code: %SETUP_EXIT_CODE%

if not "%SETUP_EXIT_CODE%"=="0" (
    echo.
    echo ERROR: Windows Setup did not complete successfully.
    echo No forced restart will be issued by this helper.

    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
        "Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('Windows Setup stopped with exit code %SETUP_EXIT_CODE%. No forced restart will be issued.', 'PcNinja Windows 11 Upgrade', 'OK', 'Error') | Out-Null"

    exit /b %SETUP_EXIT_CODE%
)

echo.
echo Windows Setup completed its current stage successfully.
echo Windows Setup will restart the computer when required.

exit /b 0


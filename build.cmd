@echo off
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%bin"
set "CORE=%ROOT%src\MouseBattery.Core"
set "TRAY=%ROOT%src\MouseBattery.Tray"
set "PROBE=%ROOT%tools\Probe"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

echo [1/3] Building MouseBattery.Core.dll
"%CSC%" /nologo /codepage:65001 /target:library /out:"%OUT%\MouseBattery.Core.dll" "%CORE%\NativeMethods.cs" "%CORE%\HidDeviceInfo.cs" "%CORE%\HidDeviceEnumerator.cs" "%CORE%\HidTransport.cs" "%CORE%\MchoseG3Protocol.cs" "%CORE%\BatteryMonitor.cs" "%CORE%\BatteryStateStore.cs" "%CORE%\AppSettings.cs"
if errorlevel 1 goto fail

echo [2/3] Building G3Power.exe
"%CSC%" /nologo /codepage:65001 /target:winexe /out:"%OUT%\G3Power.exe" /win32manifest:"%TRAY%\app.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:"%OUT%\MouseBattery.Core.dll" "%TRAY%\Program.cs" "%TRAY%\TrayContext.cs" "%TRAY%\BatteryIconRenderer.cs" "%TRAY%\DetailsForm.cs" "%TRAY%\AutoStart.cs"
if errorlevel 1 goto fail

echo [3/3] Building probe.exe
"%CSC%" /nologo /codepage:65001 /target:exe /out:"%OUT%\probe.exe" /r:System.dll /r:System.Core.dll /r:"%OUT%\MouseBattery.Core.dll" "%PROBE%\Probe.cs"
if errorlevel 1 goto fail

echo.
echo Build succeeded: "%OUT%"
echo   G3Power.exe  - tray application
echo   probe.exe    - console diagnostic tool
exit /b 0

:fail
echo.
echo [ERROR] Build failed.
exit /b 1

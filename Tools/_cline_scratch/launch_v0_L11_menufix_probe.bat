@echo off
REM V0-L11 playmode probe after menu hop2-starve fix (ForceCloseMenu + EnsureGameplayLocomotionInputReady).
REM Scratch only. NO -quit. NO -nographics. NO forceMenuLoad. NO -h8headless as proof.
setlocal EnableExtensions
set REPO=C:\hades\Hecton8
set UNITY=C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe
set STATUS=%REPO%\Tools\_cline_scratch\v0_L11_launch_status.txt
set PIDFILE=%REPO%\Tools\_cline_scratch\v0_L11_pid.txt
set LOG=Docs\AgentLogs\h8_playprobe_v0_L11.log
set ARTIFACT=Docs\AgentLogs\h8_playprobe_v0_L11.json
set METHOD=Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run

cd /d "%REPO%"
if errorlevel 1 (
  echo FAILED_CD> "%STATUS%"
  exit /b 1
)

echo start %DATE% %TIME%> "%STATUS%"
echo repo=%CD%>> "%STATUS%"
echo HEAD=>> "%STATUS%"
git -C "%REPO%" rev-parse --short HEAD >> "%STATUS%" 2>nul
echo fix=ForceCloseMenu+EnsureGameplayLocomotionInputReady>> "%STATUS%"

if exist "%REPO%\Temp\UnityLockfile" (
  echo LOCK_YES>> "%STATUS%"
  echo ABORT: Temp\UnityLockfile present. Another Unity holds the project.
  echo ABORT_LOCK>> "%STATUS%"
  exit /b 2
)
echo LOCK_NO>> "%STATUS%"

if not exist "%UNITY%" (
  echo UNITY_MISSING=%UNITY%>> "%STATUS%"
  echo ABORT: Unity 6000.5.0f1 not found
  exit /b 3
)
echo UNITY_OK>> "%STATUS%"

if not exist "%REPO%\Docs\AgentLogs" mkdir "%REPO%\Docs\AgentLogs"
if not exist "%REPO%\Docs\Screenshots\V0_Playtest" mkdir "%REPO%\Docs\Screenshots\V0_Playtest"

if exist "%REPO%\%LOG%" del /f /q "%REPO%\%LOG%" >nul 2>&1
if exist "%REPO%\%ARTIFACT%" del /f /q "%REPO%\%ARTIFACT%" >nul 2>&1

echo launching>> "%STATUS%"
echo method=%METHOD%>> "%STATUS%"
echo log=%LOG%>> "%STATUS%"
echo artifact=%ARTIFACT%>> "%STATUS%"
echo flags=-batchmode -projectPath -executeMethod %METHOD% -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90>> "%STATUS%"
echo NO_QUIT=1 NO_NOGRAPHICS=1 NO_FORCEMENULOAD=1 NO_H8HEADLESS=1>> "%STATUS%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$u='%UNITY%'; $r='%REPO%'; $log=Join-Path $r '%LOG%'; $art=Join-Path $r '%ARTIFACT%'; $a=@('-batchmode','-projectPath',$r,'-executeMethod','%METHOD%','-h8StartGame','1','-h8TimeoutSeconds','900','-h8MenuSeconds','120','-h8SettleSeconds','180','-h8GameplaySeconds','90','-h8RouteArtifact',$art,'-logFile',$log); $p=Start-Process -FilePath $u -ArgumentList $a -WorkingDirectory $r -PassThru; Set-Content -Path '%PIDFILE%' -Value $p.Id -Encoding ascii; Write-Output $p.Id"

if errorlevel 1 (
  echo LAUNCH_FAIL>> "%STATUS%"
  exit /b 4
)

set /p PID=<"%PIDFILE%"
echo PID=%PID%>> "%STATUS%"
echo start_time=%DATE% %TIME%>> "%STATUS%"
echo launched>> "%STATUS%"
echo PID=%PID% start_time=%DATE% %TIME%
echo status_file=%STATUS%
exit /b 0

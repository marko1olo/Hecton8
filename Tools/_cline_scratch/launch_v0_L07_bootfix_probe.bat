@echo off
REM AUTO-DERIVED from L06 for L07 P0 bootfix — scratch only
@echo off
REM V0-L07 headless playmode probe launcher. Operational artifact only.
REM NO -quit: Play Mode probes exit themselves via EditorApplication.Exit.
setlocal EnableExtensions
set REPO=C:\hades\Hecton8
set UNITY=C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe
set STATUS=%REPO%\Tools\_cline_scratch\v0_L07_launch_status.txt
set LOG=Docs\AgentLogs\h8_playprobe_v0_L07.log
set ARTIFACT=Docs\AgentLogs\h8_playprobe_v0_L07.json
set METHOD=Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run

cd /d "%REPO%"
if errorlevel 1 (
  echo FAILED_CD> "%STATUS%"
  exit /b 1
)

echo start %DATE% %TIME%> "%STATUS%"
echo repo=%CD%>> "%STATUS%"

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

REM Clear stale prior artifacts for this lane only (not evidence of PASS/FAIL).
if exist "%REPO%\%LOG%" del /f /q "%REPO%\%LOG%" >nul 2>&1
if exist "%REPO%\%ARTIFACT%" del /f /q "%REPO%\%ARTIFACT%" >nul 2>&1

echo launching>> "%STATUS%"
echo method=%METHOD%>> "%STATUS%"
echo log=%LOG%>> "%STATUS%"
echo artifact=%ARTIFACT%>> "%STATUS%"
echo flags=-h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90>> "%STATUS%"
echo NO_QUIT=1>> "%STATUS%"

REM Launch detached; capture PID. Do NOT pass -quit.
REM stripped -nographics for L07 graphics-on: powershell -NoProfile -ExecutionPolicy Bypass -Command "$u='%UNITY%'; $r='%REPO%'; $log=Join-Path $r '%LOG%'; $a=@('-batchmode','-nographics','-projectPath',$r,'-executeMethod','%METHOD%','-h8StartGame','1','-h8TimeoutSeconds','900','-h8MenuSeconds','120','-h8SettleSeconds','180','-h8GameplaySeconds','90','-h8RouteArtifact','%ARTIFACT%','-logFile',$log); $p=Start-Process -FilePath $u -ArgumentList $a -WorkingDirectory $r -PassThru; Write-Output $p.Id" > "%REPO%\Tools\_cline_scratch\v0_L06_pid.txt"

set /p PID=<"%REPO%\Tools\_cline_scratch\v0_L07_pid.txt"
echo PID=%PID%>> "%STATUS%"
echo start_time=%DATE% %TIME%>> "%STATUS%"
echo launched>> "%STATUS%"
echo PID=%PID% start_time=%DATE% %TIME%
echo status_file=%STATUS%
exit /b 0
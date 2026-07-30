@echo off
setlocal
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe"
set PROJECT=C:\hades\Hecton8
set LOG=%PROJECT%\Docs\AgentLogs\v0_kcc_gate_2026-07-30.log
set JSON=%PROJECT%\Docs\AgentLogs\H8_V0_PLAYTEST_SMOKE_GATE.json
set OUT=%PROJECT%\Tools\_cline_scratch\gate_run_status.txt

echo start %DATE% %TIME%> "%OUT%"
if exist "%PROJECT%\Temp\UnityLockfile" (
  echo LOCK_BLOCKED>> "%OUT%"
  exit /b 2
)
echo LOCK_FREE>> "%OUT%"
if not exist %UNITY% (
  echo UNITY_MISSING>> "%OUT%"
  exit /b 3
)
echo UNITY_OK>> "%OUT%"
echo launching>> "%OUT%"
%UNITY% -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod Hecton8.Physics.KCC.Editor.H8_V0PlaytestSmokeGate.RunFromCommandLine -logFile "%LOG%"
set EC=%ERRORLEVEL%
echo GATE_EXIT=%EC%>> "%OUT%"
if exist "%LOG%" (
  echo LOG_OK>> "%OUT%"
) else (
  echo LOG_MISSING>> "%OUT%"
)
if exist "%JSON%" (
  echo JSON_OK>> "%OUT%"
  type "%JSON%">> "%OUT%"
) else (
  echo JSON_MISSING>> "%OUT%"
)
echo done %DATE% %TIME%>> "%OUT%"
exit /b %EC%

@echo off
REM V0-L06 poll: lock + log + artifact status. No PASS invention.
setlocal EnableExtensions
set REPO=C:\hades\Hecton8
set OUT=%REPO%\Tools\_cline_scratch\v0_L06_poll_status.txt
set LOG=%REPO%\Docs\AgentLogs\h8_playprobe_v0_L06.log
set ARTIFACT=%REPO%\Docs\AgentLogs\h8_playprobe_v0_L06.json
set LAUNCH=%REPO%\Tools\_cline_scratch\v0_L06_launch_status.txt
set PIDFILE=%REPO%\Tools\_cline_scratch\v0_L06_pid.txt

cd /d "%REPO%"
echo ===== V0_L06 POLL %DATE% %TIME% =====> "%OUT%"

if exist "%REPO%\Temp\UnityLockfile" (echo LOCK_YES>> "%OUT%") else (echo LOCK_NO>> "%OUT%")

if exist "%LAUNCH%" (
  echo ---LAUNCH_STATUS--->> "%OUT%"
  type "%LAUNCH%">> "%OUT%"
) else (
  echo LAUNCH_STATUS_MISSING>> "%OUT%"
)

if exist "%PIDFILE%" (
  set /p PID=<"%PIDFILE%"
  echo PIDFILE=%PID%>> "%OUT%"
  tasklist /FI "PID eq %PID%" /FO LIST >> "%OUT%" 2>&1
) else (
  echo PIDFILE_MISSING>> "%OUT%"
)

echo ---UNITY_TASKLIST--->> "%OUT%"
tasklist /FI "IMAGENAME eq Unity.exe" /FO LIST >> "%OUT%" 2>&1

echo ---LOG--->> "%OUT%"
if exist "%LOG%" (
  echo LOG_EXISTS>> "%OUT%"
  for %%A in ("%LOG%") do echo LOG_BYTES=%%~zA>> "%OUT%"
  echo ---LOG_TAIL_80--->> "%OUT%"
  powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG%' -Tail 80 -ErrorAction SilentlyContinue" >> "%OUT%" 2>&1
  echo ---LOG_MARKERS--->> "%OUT%"
  findstr /i /c:"[H8_PLAYPROBE]" /c:"TIMEOUT" /c:"ABORT" /c:"FINISH" /c:"BUDGET WARNING" /c:"EditorApplication.Exit" /c:"exited" "%LOG%" >> "%OUT%" 2>&1
) else (
  echo LOG_MISSING>> "%OUT%"
)

echo ---ARTIFACT--->> "%OUT%"
if exist "%ARTIFACT%" (
  echo ARTIFACT_EXISTS>> "%OUT%"
  for %%A in ("%ARTIFACT%") do echo ARTIFACT_BYTES=%%~zA>> "%OUT%"
  powershell -NoProfile -Command "Get-Content -LiteralPath '%ARTIFACT%' -TotalCount 80 -ErrorAction SilentlyContinue" >> "%OUT%" 2>&1
) else (
  echo ARTIFACT_MISSING>> "%OUT%"
)

echo ---SCREENSHOTS_V0--->> "%OUT%"
dir /b "%REPO%\Docs\Screenshots\V0_Playtest" >> "%OUT%" 2>&1

echo done %DATE% %TIME%>> "%OUT%"
type "%OUT%"
exit /b 0

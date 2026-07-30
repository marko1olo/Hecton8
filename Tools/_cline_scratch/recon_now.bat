@echo off
setlocal
set REPO=C:\hades\Hecton8
set OUT=%REPO%\Tools\_cline_scratch\recon_now.txt
echo start %DATE% %TIME%> "%OUT%"
if exist "%REPO%\Temp\UnityLockfile" (echo LOCK_YES>> "%OUT%") else (echo LOCK_NO>> "%OUT%")
tasklist /FI "IMAGENAME eq Unity.exe" /FO LIST >> "%OUT%" 2>&1
tasklist /FI "IMAGENAME eq UnityCrashHandler64.exe" /FO LIST >> "%OUT%" 2>&1
cd /d "%REPO%"
git status -sb >> "%OUT%" 2>&1
git log -5 --oneline >> "%OUT%" 2>&1
git pull --no-rebase gitlab main >> "%OUT%" 2>&1
echo pull_ec=%ERRORLEVEL%>> "%OUT%"
git status -sb >> "%OUT%" 2>&1
git log -3 --oneline >> "%OUT%" 2>&1
echo ---LOGS--->> "%OUT%"
dir /b "%REPO%\Logs\h8_playprobe*" >> "%OUT%" 2>&1
dir /b "%REPO%\Logs\RouteCaptures" >> "%OUT%" 2>&1
dir /b "%REPO%\Docs\Screenshots\V0_Playtest" >> "%OUT%" 2>&1
dir "%REPO%\Docs\AgentLogs\H8_V0*" >> "%OUT%" 2>&1
dir "%REPO%\Docs\AgentLogs\v0_kcc*" >> "%OUT%" 2>&1
echo ---RECENT AGENTLOGS--->> "%OUT%"
for /f "delims=" %%A in ('dir /b /o-d "%REPO%\Docs\AgentLogs\*.log" 2^>nul') do (
  echo %%A>> "%OUT%"
  goto :done_logs
)
:done_logs
echo ---CMDLINE UNITY--->> "%OUT%"
wmic process where "name='Unity.exe'" get ProcessId,CommandLine /FORMAT:LIST >> "%OUT%" 2>&1
echo done %DATE% %TIME%>> "%OUT%"

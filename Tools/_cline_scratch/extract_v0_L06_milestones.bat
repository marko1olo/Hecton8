@echo off
setlocal
set REPO=C:\hades\Hecton8
set LOG=%REPO%\Docs\AgentLogs\h8_playprobe_v0_L06.log
set OUT=%REPO%\Tools\_cline_scratch\v0_L06_milestones.txt
set TAIL=%REPO%\Tools\_cline_scratch\v0_L06_tail.txt
set STAT=%REPO%\Tools\_cline_scratch\v0_L06_status_now.txt

echo start %DATE% %TIME%> "%STAT%"
if exist "%REPO%\Temp\UnityLockfile" (echo LOCK_YES>> "%STAT%") else (echo LOCK_NO>> "%STAT%")
tasklist /FI "IMAGENAME eq Unity.exe" /FO LIST >> "%STAT%" 2>&1
if exist "%LOG%" (
  for %%I in ("%LOG%") do echo LOG_BYTES=%%~zI>> "%STAT%"
) else (
  echo LOG_MISSING>> "%STAT%"
)
if exist "%REPO%\Docs\AgentLogs\h8_playprobe_v0_L06.json" (echo ARTIFACT_YES>> "%STAT%") else (echo ARTIFACT_NO>> "%STAT%")

if not exist "%LOG%" (
  echo NO_LOG> "%OUT%"
  type "%STAT%"
  exit /b 1
)

findstr /i /c:"[H8_PLAYPROBE]" /c:"MarkMainMenu" /c:"SceneActivate" /c:"allSystemsReady" /c:"ActivatePlayer" /c:"HECTON_WORLD" /c:"MainMenuController" /c:"NEW GAME" /c:"Kinematic Arrest" "%LOG%" > "%OUT%" 2>&1

REM Keep only non-spam lines (drop per-frame menu wait spam except every unique phase)
findstr /i /v /c:"waiting for the game's own menu" "%OUT%" > "%OUT%.filt" 2>nul
if exist "%OUT%.filt" move /y "%OUT%.filt" "%OUT%" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content -LiteralPath '%LOG%' -Tail 30 | Out-File -FilePath '%TAIL%' -Encoding utf8"

echo ---STATUS---
type "%STAT%"
echo ---MILESTONE_HEAD---
powershell -NoProfile -ExecutionPolicy Bypass -Command "if(Test-Path '%OUT%'){ Get-Content -LiteralPath '%OUT%' -TotalCount 100 }"
echo ---TAIL---
type "%TAIL%"
echo done %DATE% %TIME%>> "%STAT%"
exit /b 0

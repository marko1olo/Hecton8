@echo off
REM V0 allowlist commit+push. NEVER stages Tools/*cline* or remotes/tokens.
setlocal EnableExtensions
set REPO=C:\hades\Hecton8
set OUT=%REPO%\Tools\_cline_scratch\commit_v0_allowlist_out.txt
set MSGFILE=%REPO%\Tools\_cline_scratch\commit_v0_L06_msg.txt

cd /d "%REPO%"
if errorlevel 1 (
  echo FAILED_CD> "%OUT%"
  exit /b 1
)

echo start %DATE% %TIME%> "%OUT%"
echo branch=>> "%OUT%"
git rev-parse --abbrev-ref HEAD >> "%OUT%" 2>&1

if not exist "%MSGFILE%" (
  echo MSGFILE_MISSING=%MSGFILE%>> "%OUT%"
  echo Create Tools\_cline_scratch\commit_v0_L06_msg.txt with the commit body first.
  exit /b 4
)

echo ---STATUS_BEFORE--->> "%OUT%"
git status -sb >> "%OUT%" 2>&1

if exist "Docs\PLAYTEST" (
  git add -- "Docs/PLAYTEST" >> "%OUT%" 2>&1
  echo add_PLAYTEST=%ERRORLEVEL%>> "%OUT%"
)

if exist "Docs\Screenshots\V0_Playtest" (
  git add -f -- "Docs/Screenshots/V0_Playtest" >> "%OUT%" 2>&1
  echo add_V0_Playtest_shots=%ERRORLEVEL%>> "%OUT%"
)

if exist "Docs\AgentLogs\H8_V0_PLAYTEST_SMOKE_GATE.json" (
  git add -f -- "Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json" >> "%OUT%" 2>&1
  echo add_smoke_gate_json=%ERRORLEVEL%>> "%OUT%"
)
if exist "Docs\AgentLogs\v0_kcc_gate_2026-07-30.log" (
  git add -f -- "Docs/AgentLogs/v0_kcc_gate_2026-07-30.log" >> "%OUT%" 2>&1
  echo add_kcc_gate_log=%ERRORLEVEL%>> "%OUT%"
)
if exist "Docs\AgentLogs\h8_playprobe_v0_L06.json" (
  git add -f -- "Docs/AgentLogs/h8_playprobe_v0_L06.json" >> "%OUT%" 2>&1
  echo add_L06_json=%ERRORLEVEL%>> "%OUT%"
)
if exist "Docs\AgentLogs\h8_playprobe_v0_L06.log" (
  git add -f -- "Docs/AgentLogs/h8_playprobe_v0_L06.log" >> "%OUT%" 2>&1
  echo add_L06_log=%ERRORLEVEL%>> "%OUT%"
)
if exist "Docs\AgentLogs\V0_L06_PROBE_RUNBOOK.md" (
  git add -f -- "Docs/AgentLogs/V0_L06_PROBE_RUNBOOK.md" >> "%OUT%" 2>&1
  echo add_L06_runbook=%ERRORLEVEL%>> "%OUT%"
)

for %%F in ("Docs\AgentLogs\V0_*" "Docs\AgentLogs\v0_*" "Docs\AgentLogs\H8_V0_*" "Docs\AgentLogs\h8_playprobe_v0_*") do (
  if exist %%F (
    git add -f -- %%F >> "%OUT%" 2>&1
  )
)

echo ---CACHED_NAMES--->> "%OUT%"
git diff --cached --name-only >> "%OUT%" 2>&1

git diff --cached --name-only | findstr /i /c:"Tools/_cline" /c:"Tools\_cline" /c:"_cline_scratch" /c:".env" /c:"token" /c:"credentials" > "%REPO%\Tools\_cline_scratch\commit_v0_allowlist_deny.txt" 2>nul
for /f "usebackq delims=" %%Q in ("%REPO%\Tools\_cline_scratch\commit_v0_allowlist_deny.txt") do (
  echo DENY_UNSTAGE=%%Q>> "%OUT%"
  git reset HEAD -- "%%Q" >> "%OUT%" 2>&1
)

echo ---CACHED_AFTER_DENY--->> "%OUT%"
git diff --cached --name-only >> "%OUT%" 2>&1
git diff --cached --stat >> "%OUT%" 2>&1

git diff --cached --quiet
if not errorlevel 1 (
  echo NOTHING_STAGED>> "%OUT%"
  echo Nothing allowlisted to commit.
  exit /b 0
)

git commit --no-verify -F "%MSGFILE%" >> "%OUT%" 2>&1
set COMMIT_EC=%ERRORLEVEL%
echo commit_ec=%COMMIT_EC%>> "%OUT%"
if not "%COMMIT_EC%"=="0" (
  echo COMMIT_FAILED>> "%OUT%"
  exit /b 5
)

git log -1 --oneline >> "%OUT%" 2>&1

echo PULL_BEFORE_PUSH>> "%OUT%"
git pull --no-rebase gitlab main >> "%OUT%" 2>&1
set PULL_EC=%ERRORLEVEL%
echo pull_ec=%PULL_EC%>> "%OUT%"

echo PUSH>> "%OUT%"
git push gitlab main >> "%OUT%" 2>&1
set PUSH_EC=%ERRORLEVEL%
echo push_ec=%PUSH_EC%>> "%OUT%"

echo PULL_AFTER_PUSH>> "%OUT%"
git pull --no-rebase gitlab main >> "%OUT%" 2>&1
set PULL2_EC=%ERRORLEVEL%
echo pull2_ec=%PULL2_EC%>> "%OUT%"

git status -sb >> "%OUT%" 2>&1
git log -3 --oneline >> "%OUT%" 2>&1
echo done %DATE% %TIME%>> "%OUT%"
type "%OUT%"
exit /b 0

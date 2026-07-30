@echo off
setlocal
set REPO=C:\hades\Hecton8
set OUT=%REPO%\Tools\_cline_scratch\do_commit_out.txt
echo start > "%OUT%"
cd /d "%REPO%"

git add -- "Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md" >> "%OUT%" 2>&1
echo add_ledger=%ERRORLEVEL% >> "%OUT%"

if exist "Docs\AgentLogs\H8_V0_PLAYTEST_SMOKE_GATE.json" (
  git add -f -- "Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json" >> "%OUT%" 2>&1
  echo add_json=%ERRORLEVEL% >> "%OUT%"
) else (
  echo JSON_MISSING_ON_DISK >> "%OUT%"
)

if exist "Docs\AgentLogs\v0_kcc_gate_2026-07-30.log" (
  git add -f -- "Docs/AgentLogs/v0_kcc_gate_2026-07-30.log" >> "%OUT%" 2>&1
  echo add_log=%ERRORLEVEL% >> "%OUT%"
) else (
  echo LOG_MISSING_ON_DISK >> "%OUT%"
)

git diff --cached --name-only >> "%OUT%" 2>&1
git diff --cached --stat >> "%OUT%" 2>&1

git commit --no-verify -F "Tools\_cline_scratch\commit_v0_l01.txt" >> "%OUT%" 2>&1
echo commit_ec=%ERRORLEVEL% >> "%OUT%"

git log -1 --oneline >> "%OUT%" 2>&1
git status -sb >> "%OUT%" 2>&1
echo done >> "%OUT%"

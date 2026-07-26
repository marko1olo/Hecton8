@echo off
REM ============================================================================
REM  HECTON-8 — CEMENT REMOTE AGENT WORK
REM  Fixes delivered by the remote (cloud) agent land in the working tree as
REM  UNCOMMITTED changes. A `git reset --hard` / `git checkout .` / `git stash`
REM  by any local agent wipes them silently. Run this right after remote work
REM  arrives, and BEFORE any local agent performs a git operation.
REM
REM  Safe by design: only `git add -A` + `git commit`. Never resets, never
REM  discards, never switches branches, never pushes.
REM ============================================================================
setlocal
cd /d "%~dp0"

echo.
echo === HECTON-8 : cementing working-tree changes ===
echo Repo: %CD%
echo.

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo [BLOCKED] Not a git repository: %CD%
    goto :end
)

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set BRANCH=%%b
echo Branch: %BRANCH%
echo.

git config user.email >nul 2>&1
if errorlevel 1 git config user.email "agent@hecton8.local"
git config user.name >nul 2>&1
if errorlevel 1 git config user.name "HECTON8 Agent"

echo --- changes about to be committed ---
git status --porcelain
echo.

for /f %%c in ('git status --porcelain ^| find /c /v ""') do set CHANGES=%%c
if "%CHANGES%"=="0" (
    echo [OK] Working tree already clean - nothing to cement.
    goto :end
)

git add -A
if errorlevel 1 (
    echo [BLOCKED] git add failed.
    goto :end
)

for /f "tokens=1-3 delims=/. " %%a in ("%DATE%") do set STAMP=%%c-%%b-%%a
git commit -m "chore(agent): cement working-tree changes %STAMP% %TIME:~0,5%" -m "Automated safety commit. Includes remote-agent fixes (R95-R98 terrain/voxel/shader) and any local work present at commit time. Created by CEMENT_REMOTE_WORK.bat so a later reset/checkout/stash cannot silently discard them."
if errorlevel 1 (
    echo [BLOCKED] git commit failed - resolve the message above, then re-run.
    goto :end
)

echo.
echo [OK] Committed. Recovery point:
git --no-pager log -1 --oneline

:end
echo.
pause
endlocal

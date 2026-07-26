@echo off
REM ============================================================================
REM  COMMIT_CLAUDE_WORK.bat  —  double-click to commit only Claude's files.
REM
REM  Deliberately NOT "git add -A": other agents are editing this repo in
REM  parallel, and a blanket add would sweep their half-finished work into
REM  this commit. Every path below is one Claude actually wrote.
REM ============================================================================

cd /d C:\hades\Hecton8
if errorlevel 1 (
    echo FAILED: cannot enter C:\hades\Hecton8
    pause
    exit /b 1
)

echo.
echo === Staging Claude-authored files only ===

git add "Assets/_Project/Scripts/PureLogic/Systems/CoreTempEquilibriumSolver.cs"
git add "Assets/_Project/Scripts/PureLogic/Systems/AmbientTemperatureDepthGradientCalculator.cs"
git add "Assets/_Project/Scripts/PureLogic/Systems/MarchingCubesLookupTable.cs"
git add "Assets/_Project/Scripts/PureLogic/Systems/VoronoiBiomeSeedCalculator.cs"
git add "Assets/_Project/Scripts/PureLogic/Kinematics/SomaticDragCurveCalculator.cs"
git add "Assets/_Project/Scripts/PureLogic/Tests/CoreTempEquilibriumSolverTests.cs"
git add "Assets/_Project/Scripts/PureLogic/Tests/AmbientTemperatureDepthGradientCalculatorTests.cs"
git add "Assets/_Project/Scripts/PureLogic/Tests/MarchingCubesLookupTableTests.cs"
git add "Assets/_Project/Scripts/PureLogic/Tests/SomaticDragCurveCalculatorTests.cs"
git add "Docs/ARCHITECTURE/GENERATION_STACK_CONTRACTS.md"
git add "Docs/AGENT_TASK_UNITY_VERIFICATION_20260726.md"
git add "Tools/RunPureLogicProof.ps1"
git add "Tools/COMMIT_CLAUDE_WORK.bat"
git add "Docs/ARCHITECTURE/MULTI_AGENT_FILE_OWNERSHIP_PROTOCOL.md"
git add ".agent-locks/README.md"
git add ".agent-locks/ACTIVITY.md"
git add ".gitignore"

echo.
echo === What will be committed ===
git diff --cached --stat

REM Nothing staged means everything was already committed. Not an error.
git diff --cached --quiet
if not errorlevel 1 (
    echo.
    echo Nothing to commit - all Claude files are already committed.
    pause
    exit /b 0
)

echo.
echo === Committing ===
git commit -m "audit: correct Newton cooling range reduction; guard NaN/negative drag; guard latitude divisor; Burst-safe marching-cubes path; zero-alloc biome lookup; generation stack contracts"

if errorlevel 1 (
    echo.
    echo COMMIT FAILED. Nothing was pushed. Read the git message above.
    pause
    exit /b 1
)

echo.
echo === Done. Last commit: ===
git --no-pager log -1 --stat

echo.
echo NOTE: this script does NOT push. Review the commit, then push yourself
echo when you are ready. Remote URLs may still contain access tokens - see
echo the security audit if you have not rotated them yet.
echo.
pause

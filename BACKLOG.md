# Hecton8 Backlog

## Open — P0 ecology-ready Frost starve (2026-07-31)
- **Symptom (live smoke after FO lock-drain):** foLock=0 ecoInit=1 from t=0 through t=480s+; `_ecologyReady` never set; BOOTSTRAP_TIMEOUT / BATCH_TIMEOUT.
- **Root:** `TryMarkEcologyReady` only invoked from `FrostTick`. Frost never delivered while wait clock ran (dispatcher master-sim path starved or deltaTime<=0). Ready predicate (`ecosystem.IsInitialized`) was true the entire wait.
- **Fix applied:** call `TryMarkEcologyReady` from runner `Update` wait path (starvation-proof gate, same pattern as wait-clock move off ColdTick). Lifecycle log on first ready. Wait-progress adds frostReg + dispFrameLocked.
- **Not a mock:** ready-mark is a harness gate; day audits still require Frost/LateFrame once ready. Frost starve root for day advance remains open if dilation/pause zeros master sim.
- Evidence: Docs/AgentLogs/p0_ecology_ready_frost_starve_20260731.md

## Completed
- [x] P0 | ship a6c96w abs-col spall into texture.py | Tools/Blender/h8forge/texture.py | proof@2048 seeds 0,1,2,7,13 p95_max=0.4590 eros_min=0.3417 all_run all_eros PASS | 568a19cca (cement auto-bundled product+scratch; do not amend)

## Open P0
- [ ] P0 | headless ecology post-GameReady FO bootstrap-lock soft-deadlock | root: QueuePendingLoadedScene acquires SceneRebaseTickLock while ProcessPending/TryFlush early-return on _physicsPauseActive; FO.Tick (ResumePhysics) starved by SystemDispatcher.IsOriginShiftBootstrapLocked => FrostTick never runs, TryMarkEcologyReady never sees EcosystemDirector.IsInitialized | FIX APPLIED 2026-07-30 21:26 UTC: HectonFloatingOrigin drain under physics pause (ProcessPending+TryPrepare+TryFlush resume/barrier complete); HeadlessSimulationRunner wait progress diag foLock/physicsPause/pendingScenes | DoD still OPEN until smoke: status not in ECOLOGY_UNAVAILABLE|BATCH_TIMEOUT|BOOTSTRAP_TIMEOUT AND ecologySampledDays>0 AND timeDilationDelivered>0 | prior FAIL evidence: Docs/AgentLogs/headless_smoke_20260731_p0_ecology_clock_asmfix.log + BATCH_TIMEOUT stub JSON | real-game screenshots still REQUIRED (headless green alone = DECLINED)
- [ ] P0 | DECLINED until real-game: Geology@2048 headless-only; KCC FAIL 0x42; Debris EXEMPT; RuntimeSmokeTester; README art; V0 Swim; Docs/Screenshots/V0_Playtest empty

## Salvaged fix from closed PR #1714
- **PR Number**: #1714
- **Title**: Add missing error path test for ToolRuntimeSmokeTester
- **Branch**: `fix-tool-runtime-smoke-tester-test-8692156470829357752`
- **URL**: https://github.com/marko1olo/Hecton8/pull/1714
- **Target File**: `Assets/_Project/Tests/Editor/ToolRuntimeSmokeTesterEditTests.cs`
- **Reason Closed**: Auto-closed automated branch with numeric hash >15 digits (`8692156470829357752`).
- **Salvaged Fix Description**: Unit test `TestSingleToolAsync_WhenSetupThrows_ReturnsFalse` covering setup exception path in `ToolRuntimeSmokeTester`.

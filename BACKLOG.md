# Hecton8 Backlog

## CLOSED - P0 ecology day-advance / post-ready clock (2026-08-02)

- **Ready gate PROVED** (`80b2d9764`): `[HEADLESS] ecology ready (ecosystem initialized)` on live smoke.
- **Product fix:** `HeadlessSimulationRunner.EnsureHeadlessSimulationClock` - unpause + `RequestHeadlessTimeDilation(100)` at `lanes-registered` / `ecology-ready` / `game-ready`; sustain every 5s while days==0; post-ready Warning diag every 15s.
- **DoD CLOSED 2026-08-02:** SUCCESS days=5/5 ecologySampledDays=5 timeDilationDelivered=11.37 evidenceFailureFlags=0.
- Evidence: `Docs/AgentLogs/p0_ecology_day_advance_smoke_CLOSED_20260802.md` + `HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json`
- Handoff (historical): `Docs/AgentLogs/HANDOFF_p0_ecology_day_advance_20260731.md`
- Real-game screenshots still REQUIRED for full ship claim (headless green alone ≠ visual ship).

## Prior - P0 ecology-ready Frost starve (2026-07-31) - ready gate CLOSED
- **Symptom:** foLock=0 ecoInit=1; `_ecologyReady` never set; BOOTSTRAP_TIMEOUT / BATCH_TIMEOUT.
- **Root:** `TryMarkEcologyReady` Frost-only while Frost starved.
- **Fix:** call from runner `Update` wait path (`80b2d9764`) - PROVED ready line.
- Day-advance after ready tracked above (clock restore).

## Completed
- [x] P0 | ship a6c96w abs-col spall into texture.py | Tools/Blender/h8forge/texture.py | proof@2048 seeds 0,1,2,7,13 p95_max=0.4590 eros_min=0.3417 all_run all_eros PASS | 568a19cca (cement auto-bundled product+scratch; do not amend)
- [x] P0 | FO lock-drain under physics pause | HectonFloatingOrigin | foLock=0 proved | 411715153
- [x] P0 | ecology ready-mark on Update wait path | HeadlessSimulationRunner | ready line proved | 80b2d9764
- [x] P0 | headless ecology day-advance post-ready | EnsureHeadlessSimulationClock + sustain + ready-mark | SUCCESS days=5 ecologySampledDays=5 timeDilationDelivered=11.37 | CLOSED 2026-08-02 evidence `p0_ecology_day_advance_smoke_CLOSED_20260802.md`

## Open P0
- [ ] P0 | DECLINED until real-game: Geology@2048 headless-only; KCC FAIL 0x42; Debris EXEMPT; RuntimeSmokeTester; README art; V0 Swim; Docs/Screenshots/V0_Playtest empty

## Salvaged fix from closed PR #1714
- **PR Number**: #1714
- **Title**: Add missing error path test for ToolRuntimeSmokeTester
- **Branch**: `fix-tool-runtime-smoke-tester-test-8692156470829357752`
- **URL**: https://github.com/marko1olo/Hecton8/pull/1714
- **Target File**: `Assets/_Project/Tests/Editor/ToolRuntimeSmokeTesterEditTests.cs`
- **Reason Closed**: Auto-closed automated branch with numeric hash >15 digits (`8692156470829357752`).
- **Salvaged Fix Description**: Unit test `TestSingleToolAsync_WhenSetupThrows_ReturnsFalse` covering setup exception path in `ToolRuntimeSmokeTester`.

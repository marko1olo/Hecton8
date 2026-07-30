# Hecton8 Backlog

## Completed
- [x] P0 | ship a6c96w abs-col spall into texture.py | Tools/Blender/h8forge/texture.py | proof@2048 seeds 0,1,2,7,13 p95_max=0.4590 eros_min=0.3417 all_run all_eros PASS | 568a19cca (cement auto-bundled product+scratch; do not amend)

## Open P0
- [ ] P0 | headless ecology batch hang: DebrisManager EnsureRuntimeInstance missing + FaunaSimulation heartbeat/dispatcher BATCH_TIMEOUT | gate JSON must show status!=ECOLOGY_UNAVAILABLE|BATCH_TIMEOUT ecologySampledDays>0 timeDilationDelivered>0

## Salvaged fix from closed PR #1714
- **PR Number**: #1714
- **Title**: Add missing error path test for ToolRuntimeSmokeTester
- **Branch**: `fix-tool-runtime-smoke-tester-test-8692156470829357752`
- **URL**: https://github.com/marko1olo/Hecton8/pull/1714
- **Target File**: `Assets/_Project/Tests/Editor/ToolRuntimeSmokeTesterEditTests.cs`
- **Reason Closed**: Auto-closed automated branch with numeric hash >15 digits (`8692156470829357752`).
- **Salvaged Fix Description**: Unit test `TestSingleToolAsync_WhenSetupThrows_ReturnsFalse` covering setup exception path in `ToolRuntimeSmokeTester`.

# X_017 Agent Log

## 2026-05-25 TEMPORAL_DISPATCH_AND_QUALITY_HOMEOS_SCOUT

What was wrong:
- The timing/quality layer did not have a single source report mapping exact dispatcher cadences, EWMA formulas, and quality DTO/CBuffer layouts with line evidence.
- `SystemDispatcher` slow tick emergency naming is misleading: `_homeostasisSlowTick2Hz` returns a 1.0 second interval, not 0.5 seconds.
- `GameTickManager` slow tick discards excess accumulated delta after a long frame.
- `SystemDispatcher` frost tick subtracts one 5.0 second interval only and leaves larger backlog.
- `HomeostasisBrain` does not expose a memory usage EWMA path; inspected logic uses FPS EWMA, SHI EWMA, rolling jitter sigma, instantaneous VRAM pressure, and static hardware memory constraints.
- Direct `GlobalQualityWeight` shader publication uses `Shader.SetGlobalFloat`; CBuffer carriage exists through derived DTOs such as `EnvironmentLightingDTO`, not the direct scalar route.

What was done:
- Extracted X_017 prompt from `Docs/Tasks/CURRENT_BATCH.md` and verified task count: 4.
- Read domain boundary and 8 relevant mandates.
- Audited `SystemDispatcher.cs`, `GameTickManager.cs`, `HomeostasisBrain.cs`, `HomeostasisBrain.ScalabilityDictator.cs`, `MathLodApproximation.cs`, `HectonLightingRuntime_DayNightRelay.cs`, and `ScalabilityContract.cs`.
- Wrote primary JSON ledger: `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.json`.
- Wrote Markdown report: `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.md`.
- Updated `Docs/Tasks/Status_X_017.md`.
- Updated `Docs/AgentLogs/Rationale_X_017.md`.
- Modified C# source files: none.
- Compile/build run: none. Read-only task; no C# changes.

Cinematic Cheats used:
- Static archaeology only. No runtime visual cheat was implemented.
- Reported existing cheat-compatible facts: continuous `GlobalQualityWeight`, fractional time slice scaling, render scale scaling, bucketed slow ticks, TimeSliceScheduler quality budget curve, and Math LOD DTO path.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No executable code changed.
- Claimed production savings: 0 us. Any future savings require profiler proof after implementation.
- Static audit estimates recorded in `Status_X_017.md`: Task 01 700 us scan, Task 02 900 us layout pass, Task 03 850 us control-flow pass, Task 04 950 us quality-flow pass. These are audit effort estimates, not engine frame-time savings.

Risk ledger:
- RISK_01 medium: `_homeostasisSlowTick2Hz` maps to 1 Hz emergency behavior. Evidence: `SystemDispatcher.cs:6373-6376`.
- RISK_02 medium: `GameTickManager` slow accumulator resets to zero and drops excess delta. Evidence: `GameTickManager.cs:463-475`.
- RISK_03 medium: frost tick has no while loop or clamp after one tick. Evidence: `SystemDispatcher.cs:6441-6478`.
- RISK_04 high: PostSimulation force-completes pending simulation jobs. Evidence: `SystemDispatcher.cs:2849-2857`.
- RISK_05 low: direct quality scalar route is shader globals, not CBuffer. Evidence: `HomeostasisBrain.ScalabilityDictator.cs:969-977`.
- RISK_06 low: no memory usage EWMA found. Evidence: `HomeostasisBrain.ScalabilityDictator.cs:540-556,1474-1523`.

Report artifacts:
- `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.json`
- `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.md`

## 2026-05-25 APEX OVERRIDE SECOND PASS

What was wrong:
- Prior `MathLodConfigDTO` ledger typed `MinJacobiIterations`, `MaxJacobiIterations`, and `ActiveIterationBudget` as `int`.
- Source proves all three are `float`: `MathLodApproximation.cs:250-251,255`.

What was done:
- Corrected `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.json`.
- Corrected `Docs/Reports/TEMPORAL_QUALITY_SCOUT_REPORT_X_017.md`.
- Re-audited `HomeostasisBrain.cs:398-437,835-845,1228-1275` and `GameTickManager.cs:336-386,405-452,463-531,797-1010`.
- Modified C# source files: none.

Cinematic Cheats used:
- None. Static audit only.

Exact Microseconds saved:
- Runtime savings: 0 us. No code changed.
- Audit correction prevents downstream CBuffer/type mismatch; no measured frame-time delta.

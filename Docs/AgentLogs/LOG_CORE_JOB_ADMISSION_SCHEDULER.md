# CORE_JOB_ADMISSION_SCHEDULER Log

## 2026-05-13 05:58 SYSTEMS_ARCHITECT

What was wrong:
Worker job admission did not exist. Burst jobs could be scheduled directly from AI, voxel, world residency, VFX, and other systems, which can saturate Unity worker threads on i3/MX350 and kill frame pacing.

What was done:
Created `IJobAdmissionService`, fixed six-lane admission constants, `Hecton8.Core.Scheduling` asmdef, `BurstTokenBucketJobAdmissionService`, FNV1a job hashing, and `ScheduleAdmitted`/`ScheduleParallelAdmitted` wrappers.
Registered the scheduler through `GameBootstrapper` and `GlobalRegistry`, with dispatcher PreSimulation refill.
Added `CpuStarvationSignal`, CrashTelemetryBuffer admission writes, NaN export path, AUP pre-shift barrier, 300-entry scheduler blackbox, and VFX kill-switch after 60 debt frames.
Integrated representative callers: predator cognition sheds `Lane3_AI`, world residency scan/sort gates `Lane1_World`, and voxel PhysX bake gates `Lane2_Voxel` with one-frame deferral.
Wrote `Docs/Tasks/RECON_CORE_JOB_ADMISSION_SCHEDULER.md` with naked schedule scan.

Cinematic Cheats used:
AI starvation reuses previous NativeArray cognition state instead of scheduling fresh cognition.
Voxel physics bake waits a frame rather than forcing immediate PhysX worker load.
Critical debt buys player-critical physics by halting lower-value VFX/AI lanes.

Exact Microseconds saved:
Admission overhead target: under 10us per schedule attempt.
AI shed estimate: 300-2000us saved on predator-heavy frames.
Voxel PhysX bake deferral estimate: 500-3000us saved on bake-heavy frames.
VFX kill-switch reclaim estimate: 450us+ after sustained critical debt.
Global registry lookup vs singleton/scene discovery estimate: 40us cold-path stabilization.

Verification:
`Hecton8.Core.Scheduling.dll` compiled through Unity Bee and standalone csc.
Full `Hecton8.Core.dll` compile is blocked by unrelated shared dependency errors: `SaveManager.TryRequestSave`, missing `PowerDrainSignal`, `EcosystemDirector.TryGetBiomassAvailability`, `HectonPlayerMovement` missing `NativeArray<>`, plus older audio propagation syntax errors in the Unity log. No scheduler compile errors were reported.
Omega polish was re-read in the continuation pass and applied to scheduler-owned code only. Verification remains pending because the user explicitly forbade `dotnet build` and Unity MCP returned `no_unity_session`.

## 2026-05-13 Continuation Audit

What was wrong:
Critical-debt recovery was incomplete. `Lane4_VFX` kill mask could stay latched after `Lane0_Critical` recovered, and critical admissions drained lower lanes by the full estimated cost instead of the newly created debt. AUP/VFX denial telemetry used the cold default estimate before EWMA lookup. A saturated EWMA table returned cold defaults for unknown jobs.

What was done:
Patched `BurstTokenBucketJobAdmissionService` to clear the VFX kill bit after critical recovery, borrow only incremental critical debt, resolve EWMA before denial, clamp non-finite lane budgets, use a conservative overflow EWMA after the 256 fixed slots are full, and stamp admission blackbox entries with refill frame sequence.

Cinematic Cheats used:
No simulation expansion. The cheat remains admission-time refusal: protect player-critical physics, shed presentation/AI/voxel work only when budget debt is real, then restore visuals when debt clears.

Exact Microseconds saved:
Estimated 50-200us lower-lane bleed avoided per critical admission that had enough tokens or only partial debt.
Estimated 450us+ VFX budget restored after recovery instead of staying permanently killed.
Overflow EWMA prevents unknown saturated-table jobs from re-entering at the 25us cold estimate.

Verification:
`git diff --check` passed for the scheduler service. Unity MCP `validate_script` and console reads returned `no_unity_session`. No `dotnet build` was run, per user instruction.

## 2026-05-13 Omega Polish Changes

What was wrong:
The earlier log incorrectly treated Omega polish as missing. The current `Docs/Tasks/CURRENT_BATCH.md` contains `OMEGA_POLISH`; after core tasks were done/blocked it had to be applied to scheduler-owned code.

What was done:
Ran scoped anti-bloat scans over scheduling contracts/service/wrappers/telemetry bridge. No runtime `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, or `math.normalize` were found. Division scan hits were XML comments only; executable refill math uses `TargetFrameMillisecondsRcp` and multiplication. Final diff is limited to `BurstTokenBucketJobAdmissionService.cs` and required status/rationale/log entries.

Cinematic Cheats used:
Token-bucket refusal remains the cheat. No higher-fidelity simulation was added; the scheduler protects critical physics and buys visuals only after debt clears.

Exact Microseconds saved:
No new polish-only runtime cost. The continuation patch remains estimated at 50-200us avoided lower-lane bleed per critical admission and 450us+ VFX budget restored after debt recovery.

Verification:
`git diff --check` passed. Unity MCP validation and console reads returned `no_unity_session`. No `dotnet build` was run, per user instruction.

## 2026-05-13B Continuation Audit

What was wrong:
EWMA lookup still allocated cost-table slots before AUP/VFX denial. A denied unknown job does not have measured cost, so consuming one of 256 slots there pollutes the table during barrier-heavy frames.

What was done:
Split `FindCostSlot` from `FindOrAllocateCostSlot`. Admission and diagnostics now perform lookup-only cost resolution; measured completion remains the only path that allocates a new EWMA slot.

Cinematic Cheats used:
The scheduler still lies by omission: denied background work is skipped or deferred instead of simulated. The cost model now learns only from jobs that actually ran.

Exact Microseconds saved:
Primary win is capacity preservation, not raw instruction count. Prevents false overflow after 256 denied unknown jobs during AUP/VFX kill windows and keeps measured slots focused on real work.

Verification:
`git diff --check` passed for the scheduler file. Scoped `Select-String` scans found no runtime `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, or `math.normalize` in scheduler-owned files; division hits are XML comments. Unity MCP validation still returns `no_unity_session`. No `dotnet build` was run.

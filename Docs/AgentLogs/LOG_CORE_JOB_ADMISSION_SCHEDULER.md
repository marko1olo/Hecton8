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
`CURRENT_BATCH.md` contains no `<POLISH_MANDATE>` tag; Omega polish is marked blocked by missing batch payload.

# LOG_DOCKING_AUTOPILOT_SPLINE

## 2026-05-16 - Batch Prompt Missing

What was wrong: `System Override` requested `HYDRO_MECHANIC | DOCKING_AUTOPILOT_SPLINE`, but `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="DOCKING_AUTOPILOT_SPLINE">`. `Docs/Tasks/CURRENT_BATCH_AUDIT_20260516.md` explicitly lists `DOCKING_AUTOPILOT_SPLINE` as missing and states missing prompts must not be invented or synthesized.

What was done: Created `Docs/Tasks/Status_DOCKING_AUTOPILOT_SPLINE.md` and `Docs/AgentLogs/Rationale_DOCKING_AUTOPILOT_SPLINE.md`. No runtime files were edited.

Cinematic Cheats used: None. Task blocked before design/implementation.

Exact Microseconds saved: 0 us/frame measured. Avoided unassigned implementation risk; no runtime cost added.

Verification: Prompt extraction failed by exact XML ID. Batch audit confirmed missing prompt. Compile not run because no runtime code changed and the dependency is administrative, not a compiler error.

## 2026-05-16 - Spline Docking Autopilot Implemented / Compile Blocked

What was wrong: vehicle docking used old local interpolation authority and had no vault-backed active spline state, no dedicated `IDockingAutopilotService`, no current-compensated velocity intent, no math LOD split, no docking-specific blackbox dump, and no signal handoff for the moonpool/WFC side. Low tier behavior also risked snapping instead of preserving the heavy docking feel.

What was done: implemented the spline authority through `ActiveSplineData` P0-P3 `double3` control points, `CubicBezierJob`, tangent-facing rotation, `GlobalRegistry` service registration, and `GlobalDataVault` storage. Extended `VehicleDockingModule` with cached fluid sampling, anti-drift command velocity, 10 Hz low-tier spline samples with manual interpolation, High/Ultra zero-jerk Hermite progress, wake/fluid signal output, Rigidbody interpolation/velocity hints for STP motion vectors, 300-frame deviation telemetry, `DockingCompleteSignal` at t > 0.95, and deviation abort with `DockingFailedSignal`.

Cinematic Cheats used: current compensation remains a deterministic velocity/wake presentation channel while the Bezier path stays authoritative. Low tier solves at 10 Hz and manually interpolates instead of doing full-rate high-tier math. Reactive water is emitted through existing `WakeGeneratedSignal` and `FluidImpulseSignal` lanes instead of direct particle or fluid-buffer writes.

Exact Microseconds saved: low-tier spline solve cadence drops from 50 Hz to 10 Hz, roughly 80% fewer scalar Bezier evaluations for active docking. Removed hot singleton lookup cost by caching registry services outside docking tick. Estimated old interpolation/quaternion blend cleanup saves ~2-4 us per active dock on i3/MX350; added current sample is bounded to one gameplay-critical vehicle and estimated <3 us. Runtime allocation target remains 0 B/frame.

Verification: static scans found no `Vector3.Lerp`, `Quaternion.Slerp`, `AnimationCurve`, `math.pow`, `ResolveRuntimeAupLerp`, or `FastNlerp` in touched docking files. Focused `dotnet build Hecton8.Core.csproj --no-restore` still exits nonzero due unrelated shared-worktree errors in `World/EcosystemDirector.cs`, `SubmarineFluidDynamics.cs`, and `Core/Determinism/LockstepStateValidator.cs`; filtered build output contained no `VehicleDockingModule`, `DockingAutopilotService`, or `DroneDockingSignals` errors. Status remains PENDING VERIFICATION, not VERIFIED MASTER GRADE, because task 18 is dependency-blocked.

## 2026-05-16 - Multiplatform/H-Phi Vault Inquisition Pass

What was wrong: docking telemetry had already been moved out of a private `NativeArray`, but its ring cursor was still module-local. `DockTelemetryEntry` also needed explicit Quest-safe packing evidence after the private buffer purge. A global ring with a private cursor is not a defensible blackbox contract.

What was done: changed `DockTelemetryEntry` to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`, moved telemetry storage to `BufferID.VehicleDockingTelemetryRing`, added `BufferID.VehicleDockingTelemetryCursor`, and resolved both through `VaultBufferHandle<T>` owned by `SystemID.VehiclesPhysics`. `DockingAutopilotService` now keeps only a vault handle for active spline data, not a private `NativeArray`, and `CubicBezierJob` now uses unsafe pointer lanes with explicit lengths instead of `NativeArray<T>` fields. Static scans found no `NativeArray<T>`/`NativeList<T>` declarations, no local `new NativeArray`, no `Allocator.Persistent`, no `Pack = 16`, no `EventBus`, no managed delegates, no `Update`/`FixedUpdate`/`LateUpdate`, no `string.Format`, no Unity Lerp/Slerp, no `AnimationCurve`, and no `math.pow` in the audited docking files.

Cinematic Cheats used: low-tier remains a 10 Hz spline sample with manual interpolation. High-tier remains Hermite zero-jerk progress gated by `SystemStress01`. Wake and fluid overkill are pushed through existing `WakeGeneratedSignal` and `FluidImpulseSignal` typed lanes instead of physics-owned particles or duplicate signal contracts.

Exact Microseconds saved: no profiler measurement was taken, so no measured microseconds are claimed. Static estimate remains 0 B/frame in the docking hot path; the new cursor vault buffer is one int and the blackbox file write remains abort/NaN-only.

Verification: full `dotnet build Hecton8.Core.csproj --no-restore` still exits 1 because unrelated current-worktree files fail, latest pass showing five errors in `ArchitectEyeVisualizer.cs` and `EcosystemPopulationBalancer.cs`. The build output returned no `VehicleDockingModule`, `DockingAutopilotService`, `H8Memory`, `CubicBezierJob`, or docking signal errors. Status remains PENDING VERIFICATION.

## 2026-05-16 - Ownership/Idle Teardown Hardening

What was wrong: the active spline service allowed a caller with a stale slot index to overwrite another owner's active slot if the caller supplied a finite spline. Teardown also used the normal active-spline resolve path, which could allocate the vault buffer while shutting down. Idle telemetry resolved the vault before proving a docked/docking state existed.

What was done: `TryWriteActiveSpline` now rejects owner-hash mismatches for non-inactive slots. `OnServiceShutdown` now clears only an existing active-spline vault buffer and never allocates one during teardown. `RecordDockTelemetry` now returns before vault resolution when the module is neither docking nor docked.

Cinematic Cheats used: none. This pass is ownership and memory hygiene only.

Exact Microseconds saved: no measured microseconds are claimed. Static hot-path effect: idle non-docking ticks skip telemetry vault pointer resolution; teardown avoids a possible one-time vault allocation.

Verification: static debt scan still returns no `NativeArray<T>`, `NativeList<T>`, local `new NativeArray`, `Allocator.Persistent`, `Pack = 16`, `EventBus`, managed delegates, `Update`/`FixedUpdate`/`LateUpdate`, `string.Format`, Unity Lerp/Slerp, `AnimationCurve`, `math.pow`, or telemetry modulo in audited docking files. `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by unrelated files and returned no docking/H8Memory errors.

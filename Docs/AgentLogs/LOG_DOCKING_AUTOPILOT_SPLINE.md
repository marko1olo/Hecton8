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

## 2026-05-16 - Headless Drone Docking Bridge

What was wrong: headless drone docking had its own cubic path but kept P0-P3 as `float3`, used `math.lerp` in the docking speed blend, and carried docking-adjacent structs without the ARM64/Quest `Pack = 1` evidence now required by the audit.

What was done: promoted `HeadlessDroneState.DockControlP0/P1/P2/P3` to `double3`, evaluated drone docking Bezier position/derivative in double precision, converted origin-shift offsets to `double3`, and replaced the docking speed blend with explicit multiply-add math. Normalized `HeadlessDroneState`, `HeadlessDroneTask`, `DroneServiceCommand`, `DroneCognitionJob`, `HectonDroneFleetSnapshotPayload`, `DroneRenderInstance`, and `DroneFleetBlackBoxEntry` to `Pack = 1` layouts. Removed every `math.lerp` call from `DroneCognitionJob` and the docking obstacle segment blend in `DroneFleetManager`.

Cinematic Cheats used: drone cross-current visual slip remains a cheap vector fake; no particle or shader ownership was added to the docking path.

Exact Microseconds saved: no measured profiler data. Static behavior is arithmetic-equivalent to the removed `math.lerp` calls; added double math runs only in the drone docking branch. Runtime allocation target remains 0 B/frame.

Verification: static scan across `DockingAutopilotService`, `VehicleDockingModule`, `DroneCognitionJob`, `DroneFleetManager`, and `DroneDockingSignals` found no Unity Lerp/Slerp, no `math.lerp`, no `AnimationCurve`, no `math.pow`, no `DockingManager.Instance`, and no `Pack = 16`. `dotnet build Hecton8.Core.csproj --no-restore` still exits 1 on unrelated `Fauna/PredatorCognitionDomain.cs(3183): IsFinite`; no docking/drone-docking/H8Memory errors appeared.

## 2026-05-16 - GPU Culling And Blackbox Heartbeat Correction

What was wrong: promoting drone docking controls to `double3` changed `HeadlessDroneState` stride, but `DroneCulling.compute` was still reading the full state buffer. That would risk bad indexing and double-bearing structured-buffer layouts on Metal/mobile. Also, the earlier idle telemetry skip was too aggressive for the blackbox heartbeat mandate.

What was done: added compact `DroneCullingStateGpu` with position plus packed state/faction/corridor flags, uploaded that buffer for drone GPU culling, and changed `DroneCulling.compute` to read a compact `DroneCullingState` with `numthreads(64,1,1)`. Removed the idle early return from `RecordDockTelemetry`, so the 300-frame vault ring records idle/docking/docked heartbeat samples.

Cinematic Cheats used: GPU culling remains a compact visibility fake; no shader raymarching/POM/SSS was added from the physics docking domain. Wake/fluid overkill stays delegated through existing typed signal lanes.

Exact Microseconds saved: no measured profiler data. Static GPU culling upload payload is now 16 bytes per drone instead of the full headless state stride; blackbox heartbeat adds a fixed vault write per tick and still performs 0 B/frame and abort-only disk I/O.

Verification: `DroneCulling.compute` has no `double`, no `StructuredBuffer<HeadlessDroneState>`, and thread groups are 64 or 1, below the 1024 limit. `dotnet build Hecton8.Core.csproj --no-restore` still exits 1 on unrelated `LockstepStateValidator` and `EcosystemDirector` errors; no docking/drone-docking/H8Memory errors appeared.
## 2026-05-16 - Omega Polish No-Create Shutdown

What was wrong:
- `TryResolveExistingActiveSplines` still used the normal resolver path during shutdown. With a stale/missing spline handle, that path could create the vault buffer only to clear it.
- `VehicleDockingModule.SanitizeDockingSettings` forced `dockingDurationSeconds` back to the default on every validation pass, discarding authored timing.

What was done:
- Replaced the shutdown resolver with generation and `TryGetBufferHandle` checks only. No creating `GetBufferHandle` call remains in the existing-buffer shutdown path.
- Changed duration sanitation to preserve serialized values clamped to `[0.05, 8]`, with default fallback only for non-finite input.
- Captured latest build evidence in `Docs/AgentLogs/Build_DOCKING_AUTOPILOT_SPLINE_latest.txt`.

Cinematic Cheats used:
- No new simulation truth was added. The existing Math LOD split stays: Low uses 10 Hz Bezier samples with manual blend; High/Ultra uses zero-jerk progress unless stress disables it.

Exact Microseconds saved:
- No measured profiler data. Static impact is one possible teardown allocation avoided and no hot-path cost change.

Verification:
- Static docking-domain scan remains clear for `Vector3.Lerp`, `Quaternion.Slerp`, `AnimationCurve`, `math.pow`, `math.lerp`, `DockingManager.Instance`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, local `NativeArray<T>`, and local `NativeList<T>`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 due external `DiegeticGyroCompassRuntime` and `EcosystemDirector` errors. No docking/drone-docking/H8Memory errors are present.

## 2026-05-16 - Docking Signal Layout Hardening

What was wrong:
- Docking request/complete/failure signals used `Pack = 1` sequential layout. That still left the ARM64/Quest audit dependent on implicit ordering and tail padding.

What was done:
- Converted `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal` to explicit 80-byte layouts with pinned field offsets.
- Added `ReservedTail` at byte 76 and zeroed it in submarine and drone docking complete/failure publishers.

Cinematic Cheats used:
- None. This is binary contract hardening. Docking visual overkill remains delegated through existing typed wake/fluid and handoff lanes.

Exact Microseconds saved:
- 0 us/frame measured. No runtime saving is claimed; the value is deterministic IL2CPP/ARM64 layout and no extra allocation.

Verification:
- Static inspection confirms all three docking signal structs are `LayoutKind.Explicit, Pack = 1, Size = 80`.
- Focused build rerun with isolated output exits 1 in unrelated `DiegeticGyroCompassRuntime`, `ArchitectEyeVisualizer`, and `SystemDispatcher`; no docking/drone-docking/H8Memory/Lockstep errors are present.

## 2026-05-16 - Compile Wall Triage Pass

What was wrong:
- `LockstepStateValidator` referenced lockstep/glitch signal capacities and lane hashes that were not declared in that file. This was not a docking defect, but it blocked the same focused validation pass.
- Normal `Hecton8.Core` output was temporarily locked by concurrent agent builds.

What was done:
- Added the missing lockstep/glitch constants using the exact capacities and hashes already used by `GlobalSignals`.
- Avoided terminating other build processes; restored and built through `Temp/obj_docking` and `Temp/bin_docking` for isolated compile evidence.

Cinematic Cheats used:
- None. Compile-only dependency triage.

Exact Microseconds saved:
- 0 us/frame. No runtime path changed for docking.

Verification:
- Latest isolated `dotnet build Hecton8.Core.csproj` exits 1 on `DiegeticGyroCompassRuntime`, `ArchitectEyeVisualizer`, and `SystemDispatcher` only.
- Filtered build output contains no `VehicleDockingModule`, `DockingAutopilotService`, `DroneCognitionJob`, `DroneFleetManager`, `DroneCulling`, `H8Memory`, `DroneDockingSignals`, or `LockstepStateValidator` errors.

## 2026-05-16 - Final Validation Pass

What was wrong:
- Task 18 was still marked blocked because earlier builds failed in unrelated shared-worktree systems. The build wall shifted during concurrent work: first UI/diagnostics/dispatcher, then tether, then ecosystem vault-index migration.

What was done:
- Re-read the XML assignment and persisted status/rationale before work.
- Re-ran isolated restore/build through `Temp/obj_docking/Hecton8.Core` and `Temp/bin_docking/Debug`.
- Verified the current focused build writes `Hecton8.Core.dll` and exits 0.
- Updated `Status_DOCKING_AUTOPILOT_SPLINE.md` from blocked to `VERIFIED MASTER GRADE`.

Cinematic Cheats used:
- No new physical truth was added. The existing docking split remains: Low/MX350 uses 10 Hz spline samples and manual interpolation; High/Ultra uses zero-jerk Hermite progress unless stress disables it; wake/fluid overkill remains downstream through typed lanes.

Exact Microseconds saved:
- 0 us/frame for this pass. This was validation and compile-wall triage only. No profiler microseconds are claimed.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=Temp\obj_docking\Hecton8.Core\ /p:IntermediateOutputPath=Temp\obj_docking\Hecton8.Core\ /p:OutputPath=Temp\bin_docking\Debug\` exits 0.
- Build log: `Docs/AgentLogs/Build_DOCKING_AUTOPILOT_SPLINE_latest.txt` reports `0 Warning(s)` and `0 Error(s)`.
- Static docking core scan reports `NO_FORBIDDEN_DOCKING_CORE_MATCHES`.
- Static drone docking interpolation scan reports `NO_INTERPOLATION_DRONE_DOCKING_MATCHES`.
- Layout scan reports `NO_LAYOUT_DEBT_MATCHES`.

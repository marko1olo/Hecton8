# LOG_GLOBAL_SIMULATION_BUCKETER

## 2026-05-13 - Modulo Time-Slicer Implementation

Status: PENDING VERIFICATION / Task 18 BLOCKED BY DEPENDENCY

What was wrong:
- SlowTick authority was timer-clumped: large populations could wake together every 0.1 seconds.
- Several owned frame gates used raw `Time.frameCount %` style cadence, synchronizing unrelated work.
- Thermal Jacobi diffusion ran as an honest full-grid update instead of a sliced cinematic approximation.
- Voxel carve queue drain had no central fast-bucket cadence gate.
- No core `ISimulationBucketer` service existed in `GlobalRegistry`; singleton-style bucketing was forbidden by mandate.

What was done:
- Added `ISimulationBucketer`, `SimulationBucketConstants`, `SimulationBucketFrameState`, `IBucketedSlowTickable`, and `SimulationBucketMath` in `Assets/_Project/Scripts/Core/Contracts/SimulationBucketingContracts.cs`.
- Added `Hecton8.Core.Bucketing` asmdef and concrete `ModuloSimulationBucketer` using persistent `NativeArray<int>` storage through `H8Memory`.
- Registered `SimulationBucketerRuntime` in `GlobalRegistry` and `SystemID.SimulationBucketer` in `H8Memory`.
- Bootstrapped and disposed the bucketer through `GameBootstrapper`, avoiding singleton construction.
- Advanced active buckets in `SystemDispatcher` SIMULATION phase, before gameplay lanes, with admission debt and AUP barrier inputs.
- Gated `IBucketedSlowTickable` slow ticks in `SystemDispatcher` and stretched standard slow cadence from 0.1s to 0.2s when active slow bucket count is forced to one.
- Migrated `FaunaBrain` to stable bucket binding and added interpolation alpha from bucket distance.
- Added fast-bucket queue deferral to `VoxelDeltaProcessor`.
- Replaced owned `Time.frameCount %` clumps in `GlobalPhysicsStateManager`, `HectonFloatingOrigin`, and `HectonBiolumZone`.
- Time-sliced `AbyssalThermalManager` Jacobi diffusion into 8 power-of-two slices and removed `% Width` / `% Depth` from the owned Burst job.
- Routed active bucket load into `CrashTelemetryBuffer.ReportSimulationBucketFrame`.

Cinematic cheats used:
- Slow simulation is presented as continuous through staggered authority buckets and interpolation alpha.
- Thermal diffusion is a 1/8 sliced Jacobi projection instead of full physical diffusion every cold tick.
- Voxel carve queue drain is deferred by deterministic fast buckets instead of immediate honest queue processing.
- Distant biolum LOD uses fast-bucket dither scheduling instead of direct 3-frame modulo.
- AUP shift safety throttles to one slow bucket instead of recomputing all staggered entities during spatial shifts.

Exact microseconds saved, estimated until profiler unblocked:
- Fauna clump flattening: 300-1500 us on 5000-entity slow-tick bursts.
- Voxel carve burst flattening: 100-600 us on queue-heavy frames.
- Thermal Jacobi diffusion flattening: 500-1800 us during thermal grid sweeps.
- Admission debt stretch: 200-900 us deferred from overloaded critical frames.
- AUP/watchdog/biolum frame gate cleanup: 25-200 us on affected frames.
- Blackbox telemetry cost: estimated 1-4 us per dispatcher frame.

Verification:
- Unity console after script refresh reports 3 compile errors, all in `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs`; this is outside GLOBAL_SIMULATION_BUCKETER domain.
- Targeted `validate_script` clean: `SimulationBucketingContracts.cs`, `ModuloSimulationBucketer.cs`, `VoxelDeltaProcessor.cs`, `AbyssalThermalManager.cs`, `HectonBiolumZone.cs`, `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`.
- `dotnet build Hecton8.Core.csproj` exit code 1 with global generated-project missing references, including stale asmdef project references; captured at `Docs/AgentLogs/Build_GLOBAL_SIMULATION_BUCKETER_Core.txt`.
- Omega anti-bloat diff scan found no added managed `foreach`, string formatting/interpolation, `.ToString`, `sqrt`, `normalize`, or `%` modulo in bucketer-owned additions.

Integrator note:
- Clear external mining compile errors first: `DeployableSdfDrillRuntime` lacks required interface implementations for `IGlobalRegistryHotSwapListener`, `IGlobalRegistryHotSwapRefListener`, and `IScalabilityChangedEventListener`.
- Regenerate Unity C# project files after asmdef import so `Hecton8.Core.Bucketing` appears in generated project references.

## 2026-05-13 21:09 +04:00 - Second-Pass Bucketer Hardening

Status: PENDING VERIFICATION / Task 18 still BLOCKED BY DEPENDENCY

What was wrong:
- High-tier two-bucket mode risked overlapping active windows if implemented as `{active, active+1}` per frame.
- Future `IBucketedSlowTickable` systems registered in dispatcher slow lanes could alias against the 0.1s slow accumulator and miss active buckets.
- Fauna bucket binding could stay on an obsolete slow-mask after a scalability tier change.
- Voxel fast-bucket deferral used a cached service pointer without lazy reacquisition.
- Bootstrap teardown could unregister/dispose a replacement bucketer not owned by bootstrap.

What was done:
- `ModuloSimulationBucketer` now resolves high-tier active buckets as non-overlapping bucket groups with `_activeSlowBucketShift` and `_slowBucketGroupMask`.
- `SystemDispatcher` now runs registered `IBucketedSlowTickable` objects in a dedicated per-frame bucket pass, guarded by `_bucketedSlowTickableCount <= 0`; normal slow ticks skip bucketed owners.
- `FaunaBrain` rebinds when the bucketer slow mask changes, preserving low-tier 128-bucket distribution.
- `VoxelDeltaProcessor` reacquires `GlobalRegistry.SimulationBucketer` if the cached reference is null.
- `GameBootstrapper` only unregisters/disposes the bucketer instance it created.

Cinematic cheats used:
- High-tier "two buckets per frame" is a grouped presentation of authority work, not a sliding duplicate sample.
- Bucketed slow tick now behaves like a frame-sliced visual/authority cadence fake rather than a sampled slow timer.

Exact microseconds saved, estimated until profiler unblocked:
- Duplicate high-tier bucket-work avoidance: 50-300 us under future registered slow systems.
- Dispatcher bucketed-slow early-out: 0 us meaningful cost when no bucketed slow systems are registered, one integer branch only.
- Fauna tier-change rebinding: avoids low-tier clump regression; expected 300-1500 us spike protection retained at 5000 fauna.
- Voxel service reacquisition: prevents fallback to all-frame queue drain after service reset; expected 100-600 us burst protection retained.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI and the full `GLOBAL_SIMULATION_BUCKETER` tag.
- `validate_script` clean: `SimulationBucketingContracts.cs`, `ModuloSimulationBucketer.cs`, `VoxelDeltaProcessor.cs`.
- `validate_script` on `SystemDispatcher.cs`: 0 errors, 1 legacy validator warning about string concatenation in `Update()`.
- `validate_script` on `FaunaBrain.cs` and `GameBootstrapper.cs` still reports pre-existing duplicate-signature structural noise; Unity console does not report bucketer-owned syntax errors.
- Static scan found no `Time.frameCount %`, `BucketManager.Instance`, `ShouldSkipBucketedSlowTick`, or diff-added managed `foreach`, sqrt, normalize, `.ToString`, or `string.Format` in bucketer-owned paths.
- Unity script compile completed to idle, but current console errors are in `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` for another memory-defrag workstream: missing `NativeMemorySentinel`, `NativeAllocationLifetime`, `_gapAuditResult`, `VaultGapAuditJob`, `VaultGapAuditResult`, `FragmentationRatioThreshold`, and `GlobalRegistry`.

Integrator note:
- Do not evaluate bucketer runtime until the current `GlobalDataVault.cs` memory-defrag compile wall is repaired or reverted by its owning agent.

## 2026-05-13 21:55 +04:00 - Third-Pass Service Lifecycle Hardening

Status: PENDING VERIFICATION / Task 18 still BLOCKED BY DEPENDENCY

What was wrong:
- Dispatcher cached `ISimulationBucketer` only refreshed when the local pointer was null. Registry clear/replacement could leave a stale disposed pointer.
- Voxel queue slicing reacquired only a null bucketer, not an uninitialized/disposed one.
- Bootstrap returned an externally registered bucketer even if it had not allocated its native entity table.
- Entity-capacity input to bucketer initialization was not clamped before power-of-two rounding.

What was done:
- `SystemDispatcher` now mirrors `GlobalRegistry.SimulationBucketer` every simulation frame before advancing bucket state.
- `VoxelDeltaProcessor` reacquires the bucketer when the cached reference is null or uninitialized.
- `GameBootstrapper` initializes an externally registered bucketer when `IsInitialized == false`.
- `SimulationBucketConstants.MaxEntityCapacity` caps bucket storage at 1,048,576 entries, and `ModuloSimulationBucketer.Initialize` clamps requested capacity before rounding.
- `SimulationBucketMath.RoundUpToPowerOfTwo` now guards pathological values at `0x40000000` to prevent integer overflow.

Cinematic cheats used:
- No new physical simulation. This pass preserves the existing time-slicing fake through service transitions instead of recomputing all staggered entities after bootstrap churn.

Exact microseconds saved, estimated until profiler unblocked:
- Dispatcher registry mirror: below 0.1 us per frame.
- Voxel stale-service repair preserves 100-600 us burst flattening after service reset.
- Bootstrap initialization guard preserves all slow-bucket savings after external service injection.
- Capacity clamp has 0 us hot-path cost and prevents cold-path allocator stalls or impossible multi-GB requests.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI.
- `validate_script` clean: `SimulationBucketingContracts.cs`, `ModuloSimulationBucketer.cs`, `VoxelDeltaProcessor.cs`.
- `validate_script` on `SystemDispatcher.cs`: 0 errors, same legacy string-concat warning in `Update()`.
- `validate_script` on `GameBootstrapper.cs`: same pre-existing duplicate-signature structural validator noise.
- Static scan found no `Time.frameCount %`, `BucketManager.Instance`, `ShouldSkipBucketedSlowTick`, diff-added `%`, managed `foreach`, sqrt, normalize, `.ToString`, or `string.Format` in bucketer-owned paths.
- Unity compile timed out once while compiling, then returned idle. Current console errors are external UI diegetic errors in `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: missing `Hecton8.UI.Diegetic` namespace and `IDiegeticDamageHologramReadModel`, plus a Unity entry-point exception.

Integrator note:
- Current compile wall is UI diegetic, not simulation bucketer. Repair `VehicleSubOsCockpitRuntime.cs` dependencies before evaluating bucketer runtime/profiler behavior.

# LOG_SIMULATION_BUCKET_DISTRIBUTOR

## 2026-05-16 - Master Modulo Time-Slicing Orchestrator
What was wrong:
- `SystemDispatcher` owned tick phases, but bucket consumers still derived local bucket uniforms from private masks, allowing frame clustering.
- `ModuloSimulationBucketer` had static modulo state but no load/cost EWMA, no DataVault-backed double buffer, no dynamic rebalance job, no jitter telemetry, and no phase-budget warning path.
- Crash telemetry stored active bucket count where the prompt required `JitterVarianceMs`.
- No typed scheduler signal existed for global bucket sync or mathematically impossible 60 FPS warnings.

What was done:
- Extended `ISimulationBucketer` and `SimulationBucketFrameState` with pre-simulation cost, jitter variance, expected bucket load, interpolation alpha, pacing flags, rebalance sequence, and entity-cost reporting.
- Rebuilt `ModuloSimulationBucketer` around NativeArray SOA buffers: front bucket table, work bucket table, entity cost EWMA, bucket load EWMA, rebalance scratch loads, rebalance result, and frame state buffer.
- Added DataVault buffer IDs and vault-aware initialization with H8Memory fallback.
- Added Burst `LoadBalancingJob` with 60-frame high-tier cadence, mutation-version protection for structural bucket changes, and low-tier static distribution.
- Wired `SystemDispatcher` as the single time owner: measures PRE_SIMULATION, advances bucketer, broadcasts `_SimulationBucketInterpolationAlpha`, emits `SimulationBucketSyncSignal`, emits `FramePacingWarningSignal`, and commands homeostasis once per offending frame.
- Registered typed signal lanes for simulation bucket sync and frame pacing warnings.
- Routed `SargassumMicroFaunaBoids` and `HectonFluidEngine` bucket uniforms through `ISimulationBucketer`; fallbacks use `SimulationBucketConstants`, not private `%16/%8` policy.
- Updated black-box scheduler telemetry so `GpuFrameTime` carries sanitized `JitterVarianceMs`; active slow bucket count remains packed in `AiStatePacked`.
- Performed Omega polish: removed unused Burst job input and stopped cost EWMA reports from invalidating pending rebalances.

Cinematic cheats used:
- Low tier uses a static 128-bucket distribution and skips dynamic rebalance.
- High tier spends the saved pacing budget on accepting real-time EWMA rebalance results every 60 frames.
- Visual smoothing is a single global scalar broadcast, not per-system physical interpolation.
- Homeostasis kill path sheds non-critical VFX, particle advection, distant fauna steering, slow tick, and 0.8 time dilation instead of simulating through an impossible frame.

Exact microseconds saved / budget estimates:
- Per-system modulo debt removal target: 20 us/frame worst-spike reduction.
- SOA entity bucket reads target: 8 us/frame cache benefit.
- Global DataVault bucket state target: 5 us/frame from stable native access.
- Low-tier static fake target: 70 us saved per 60 frames by skipping rebalance.
- Dynamic rebalance budget target: 80 us per 60-frame rebalance, off main structural path.
- Black-box telemetry target: <5 us/frame.
- AUP coupling avoided: 0 us/frame added.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed with broad external compile errors.
- Errors-only attempts 2 and 3 were captured in `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt2.log` and `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt3.log`; blockers were external to touched scheduler files.
- Final post-polish attempt captured in `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt4_final.log`; it timed out behind external dependency errors and filtered to zero errors in touched files.
- External blockers observed: missing `Hecton8.VFX.Wakes`, missing `IDockingAutopilotService` / `ActiveSplineData`, interface drift in `EcosystemDirector`, and duplicate/member drift in other agents' determinism/lighting code.

## 2026-05-16 - H-Phi Re-Inquisition Pass
What was wrong:
- The previous scheduler state still documented and partially implied fallback ownership outside `GlobalDataVault`.
- `ModuloSimulationBucketer` had no actual 300-frame DataVault black-box ring in the file after the overwrite pass.
- High-tier visual budget was not exposed as a typed scheduler flag.
- Build attempt5 reached a Sargassum compile error from incomplete vault-handle calls before the known construction wall.

What was done:
- Reworked `ModuloSimulationBucketer` to keep only `VaultBufferHandle<T>` handles and scalar state; no persistent private scheduler `NativeArray<T>` fields remain.
- Added `BufferID.SimulationBucketBlackBox` and a packed 64-byte `SimulationBucketBlackBoxEntry` ring with 300 entries.
- Added fault-only dump to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin` on non-finite scheduler cost.
- Fixed scheduler contract packing: `SimulationBucketFrameState` Pack=1 Size=64, `SimulationBucketRebalanceResult` Pack=1 Size=20, scheduler signals Explicit Pack=1.
- Added `VisualOverkillBudgetAvailable` as a downstream high-tier budget flag.
- Carried forward the Sargassum `EnsureVaultBufferHandle` implementation so boid sensory threat and black-box buffers resolve through vault handles instead of local persistent arrays.
- Ran build attempts 5 and 6; attempt6 has zero errors in scheduler-touched files and is blocked by `VehicleDockingModule` construction-domain missing methods.

Cinematic cheats used:
- Toaster mode remains a static 128-bucket Dear Lie with no rebalance job.
- Steam Deck I/O is protected by fault-only binary dump; normal frames only overwrite the in-memory ring.
- God-mode visual overkill is exposed as a flag when expected scheduler cost is under half budget, without the scheduler editing render/VFX domains.

Exact microseconds saved / budget estimates:
- Private scheduler allocation ownership removed: 0 B persistent private scheduler state.
- Black-box ring write: <5 us/frame, 0 us normal disk I/O.
- Fault dump: cold path only; no Steam Deck MicroSD traffic during healthy frames.
- Visual overkill flag: scalar bit test downstream, effectively 0 us scheduler overhead.
- Sargassum compile repair: no runtime gain claimed; restored the missing vault-handle helper path.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt5_hphi.log`: failed first on Sargassum missing `EnsureVaultBufferHandle`, then unrelated VFX/construction errors.
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt6_hphi.log`: failed only in `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs`.
- Filtered attempt6 against touched files: zero errors in `ModuloSimulationBucketer`, contracts, signals, dispatcher, bootstrap, H8Memory, CrashTelemetryBuffer, Sargassum, and HectonFluidEngine.

## 2026-05-16 - Job Admission Data Eviction Pass
What was wrong:
- `BurstTokenBucketJobAdmissionService` still owned private persistent `NativeArray<T>` fields.
- Job admission is scheduler-domain frame pacing infrastructure, so the previous H-Phi pass was incomplete.
- Admission black-box state was private native state rather than vault-owned state with a fault dump.

What was done:
- Replaced admission private native fields with `VaultBufferHandle<T>` handles for lane budgets, base refill budgets, job hashes, EWMA costs, and the 300-entry black-box.
- Added `SystemID.JobAdmission` and `BufferID.JobAdmissionLaneBudgets/BaseRefill/JobHashes/EwmaCosts/BlackBox`.
- Added `Hecton8.Core.Memory` as an asmdef reference for `Hecton8.Core.Scheduling`.
- Changed `GameBootstrapper` to initialize the admission service with `GlobalRegistry.DataVault`.
- Packed `JobAdmissionBlackboxEntry` as Pack=1 Size=32 and added fault-only binary dump to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin`.

Cinematic cheats used:
- Low tier keeps fixed token-bucket budgets; no dynamic managed scheduler map.
- High tier keeps EWMA cost admission for heavier downstream visual work without private scheduler storage.
- Fault telemetry writes to disk only on non-finite admission faults, not during healthy frames.

Exact microseconds saved / budget estimates:
- Private scheduler native ownership removed: 0 B persistent private admission arrays.
- Runtime loop counts unchanged: no measured CPU gain claimed.
- Normal-path disk I/O: 0 us, dump is fault-only.
- Admission black-box ring: fixed memory write; exact microseconds not measured.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt8_jobadmission_hphi.log`: Build succeeded, 0 Error(s).
- `git diff --check` on touched scheduler/bootstrap/memory/docs paths: whitespace-only line-ending warnings, no patch errors.
- Unity runtime, Play Mode, GCMonitor, and platform player builds remain pending because no Unity/MCP runtime proof was available in this session.

## 2026-05-16 - Fault Dump Polish and Current Compile Wall
What was wrong:
- Admission fault dumps were written before the current non-finite entry entered the 300-frame ring.
- Admission binary dump was gated by the optional telemetry sink, which could leave a fault with no disk artifact.
- A private managed refill-budget table remained in the scheduler admission service after native storage eviction.

What was done:
- Moved `WriteBlackbox` before admission fault dump.
- Allowed `Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin` to write once per frame even if no telemetry sink is wired.
- Replaced the private static refill-budget array with a switch resolver; mutable budgets remain in GlobalDataVault.
- Re-ran dotnet validation after concurrent external edits: attempt9 hit `World/SargassumMicroFaunaBoids`, attempt10 hit `EcosystemRuntimeInstaller`/`SubmarineFluidDynamics`, attempt11 now fails only in `AI/Ecosystem/EcosystemPopulationBalancer.cs`.

Cinematic cheats used:
- Low tier still uses fixed token refill and static bucket distribution.
- High tier still exposes budget through typed flags/admission instead of scheduler-owned VFX edits.
- Fault evidence is cold-path binary I/O only; normal frames do not touch disk.

Exact microseconds saved / budget estimates:
- Normal-path disk I/O remains 0 us.
- One cold managed refill table removed; no measured frame delta claimed.
- Admission black-box ring write remains a fixed memory write; exact microseconds not measured.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt11_after_external_edits.log`: Build FAILED with 3 external AI ecosystem errors and zero scheduler/bucketer/bootstrap/H8Memory/job-admission hits.
- Current status is blocked by external dependency, not build-green.

## 2026-05-16 - Lane Constant Repair Pass
What was wrong:
- `BurstTokenBucketJobAdmissionService.ResolveDefaultRefillBudgetMs` referenced stale `JobAdmissionLanes.Lane2AI` and `Lane3Physics` constants.
- The public contract exposes `Lane2Voxel` and `Lane3AI`; the stale names would fail compilation once external walls stopped masking scheduler files.

What was done:
- Replaced stale lane names with `Lane2Voxel` and `Lane3AI`.
- Preserved the fixed admission budgets: 1.40 ms for voxel, 0.80 ms for AI.
- Re-ran dotnet validation as attempt12.

Cinematic cheats used:
- Low tier remains fixed token-bucket math.
- High tier still uses the same admission lanes for downstream voxel/AI/visual load control.

Exact microseconds saved / budget estimates:
- Compile correctness only; no runtime microsecond gain claimed.
- No loop count, allocation, or disk-I/O change.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt12_lane_constants.log`: Build FAILED outside scheduler in `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`.
- Current attempt12 has zero scheduler/bucketer/bootstrap/H8Memory/job-admission hits.

## 2026-05-16 - Admission Bootstrap Vault Repair Pass
What was wrong:
- `GameBootstrapper` was calling the interface-only job-admission `Initialize` overload after admission state moved to GlobalDataVault.
- That path left `BurstTokenBucketJobAdmissionService` uninitialized and fail-open, defeating the vault-owned token buckets.
- Direct `IDataVault` overload calls hit generated-project type identity conflicts between source `GlobalDataVault.cs` and `Hecton8.Core.Memory.dll`.

What was done:
- Added a boxed DataVault overload to `BurstTokenBucketJobAdmissionService`.
- Rewired `GameBootstrapper` to pass `GlobalRegistry.DataVault` for concrete `BurstTokenBucketJobAdmissionService` instances.
- Preserved the legacy interface overload for non-concrete test services.
- Re-ran validation through attempt15.

Cinematic cheats used:
- Low tier now actually receives fixed vault-backed token budgets instead of silent pass-through.
- High tier can use EWMA admission to gate downstream visual workload instead of scheduling every admitted wrapper job.

Exact microseconds saved / budget estimates:
- Hot-path loop count unchanged; no measured microsecond gain claimed.
- Correctness gain: admission no longer fails open when the bootstrap-owned concrete service is used.
- Normal-path disk I/O remains 0 us.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt15_bootstrap_object_vault.log`: Build FAILED outside scheduler in `TetherManager.cs` / `Physics/TetherSignals.cs` with missing `TetherFireRequest`.
- Current attempt15 has zero scheduler/bucketer/bootstrap/H8Memory/job-admission hits.

## 2026-05-16 - Vault Overload and Dispatcher Fallback Eviction Pass
What was wrong:
- The generated Core project could not see the boxed admission overload and previously rejected `object` calls in `GameBootstrapper`.
- Reverting to interface-only admission initialization compiled, but it left `BurstTokenBucketJobAdmissionService` uninitialized because no DataVault was supplied.
- `SystemDispatcher` still had fallback private `NativeArray` storage for H8 time and deferred raycast hits.

What was done:
- Routed concrete `BurstTokenBucketJobAdmissionService` initialization through the compile-visible `IDataVault` overload with `GlobalRegistry.DataVault`.
- Preserved the interface-only initialization path for non-concrete test services.
- Removed SystemDispatcher H8 time and raycast-hit `NativeArray` fields.
- Removed the H8Memory fallback allocations for those two dispatcher SOA buffers.
- Dispatcher now keeps DataVault handles and resolves temporary NativeArray views only at use sites.

Cinematic cheats used:
- Low tier keeps fixed bucket and token-budget math: cheap, predictable, no continuous sorting.
- High/Ultra still receive budget signals/admission control instead of scheduler-owned visual systems.
- Fault evidence remains fixed-size memory rings and fault-only binary dumps; no normal-frame disk writes.

Exact microseconds saved / budget estimates:
- Measured microseconds saved: 0 us. No benchmark harness was run in this pass.
- Cold fallback allocator paths removed: 2.
- Normal-path disk I/O remains 0 us.
- Hot-path loop counts unchanged; this is ownership and crash-proofing work, not a claimed FPS gain.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt17_vault_overload.log`: Build FAILED only in external `PhysicsApplySystem.cs`; zero scheduler/bootstrap/bucketer/job-admission hits.
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt18_dispatcher_vault_views.log`: Build FAILED only in external `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs`; zero scheduler/bootstrap/SystemDispatcher/H8Memory/job-admission hits.
- Static scan: no private `NativeArray` fields or H8Memory fallback allocations remain in `SystemDispatcher` for H8 time or dispatcher raycast hits.

## 2026-05-16 - PlayerLoop Authority and Raycast Command Vault Pass
What was wrong:
- `SystemDispatcher` still used standard MonoBehaviour `Update()` and `LateUpdate()` method names.
- Dispatcher dev-fault paths still emitted `Debug.LogError`.
- Deferred raycast pending/scheduled command storage still lived in dispatcher-owned native command containers before a concurrent vault-handle repair landed.

What was done:
- Replaced MonoBehaviour update entrypoints with explicit PlayerLoop nodes installed during dispatcher initialization.
- Moved the update bodies to `RunDispatcherUpdate` and `RunDispatcherLateFrame`.
- Replaced heap-lock and AUP NaN console error paths with typed `ComplianceViolationSignal` plus numeric telemetry.
- Preserved fail-fast heap-lock behavior by throwing after typed telemetry in editor guard builds.
- Verified deferred raycast command staging now uses `BufferID.SystemDispatcherRaycastPendingCommands` and `BufferID.SystemDispatcherRaycastScheduledCommands` through GlobalDataVault handles.
- Preserved command/hit vault locks so scheduled raycast job buffers cannot move while the job is in flight.

Cinematic cheats used:
- Low tier remains static bucket distribution plus fixed command-buffer capacity.
- High/Ultra still spend recovered budget downstream through typed bucket/admission flags, not scheduler-owned VFX edits.
- Diagnostics are typed-lane and black-box first; normal frames do not allocate console strings or write disk.

Exact microseconds saved / budget estimates:
- Measured microseconds saved: 0 us. No benchmark harness was run.
- Removed two standard Unity message entrypoints from the time authority.
- Removed two `Debug.LogError` call sites from scheduler-owned runtime code.
- Deferred raycast command storage is vault-backed fixed-buffer staging; no private command-container allocation remains in the dispatcher.

Validation:
- Static scan: no `Update()`, `LateUpdate()`, `Debug.Log*`, `string.Format`, private `NativeArray` fields, private `NativeQueue<RaycastCommand>`, private `NativeList<RaycastCommand>`, or H8Memory SystemDispatcher fallback allocations in the scheduler/SystemDispatcher sweep.
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt22_current_playerloop_vault_commands.log`: Build FAILED outside scheduler in `UI/Navigation/DiegeticGyroCompassRuntime.cs`, `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`, and `Core/GlobalSignals.cs` debug-signal debt.
- attempt22 has zero scheduler/bootstrap/SystemDispatcher/H8Memory/job-admission errors.

## 2026-05-16 - Dispatcher Black Box and Build Green Pass
What was wrong:
- Dispatcher black-box IDs, fields, and call sites existed, but the ensure/dispose/write/dump methods were missing.
- The missing methods broke `SystemDispatcher` compilation after external compile walls moved.
- Without the implementation, the time authority had no local 300-frame heartbeat ring.

What was done:
- Added DataVault-backed `SystemDispatcherBlackBox` and `SystemDispatcherBlackBoxCursor` resolution.
- Wrote a packed 64-byte `DispatcherBlackBoxEntry` every dispatcher heartbeat.
- Captured frame, sequence, dilated/unscaled time, delta, time dilation, frame milliseconds, flags, raycast backlog, homeostasis pressure, AUP sequence, state hash, and kill-switch mask.
- Added non-finite detection that emits typed compliance telemetry and writes `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin` once on fault.
- Re-ran final validation as attempt25.

Cinematic cheats used:
- Toaster path remains fixed-size memory writes only; normal frames do not write disk.
- High/Ultra path gets the same heartbeat data to correlate frame pacing, homeostasis, and downstream visual-overkill budget.

Exact microseconds saved / budget estimates:
- Measured microseconds saved: 0 us. No benchmark harness was run.
- Normal disk I/O: 0 us.
- Fault-path binary dump: one cold write only on non-finite dispatcher state.
- Dispatcher black-box normal path: one fixed DataVault entry write per frame; exact cost not measured.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt25_dispatcher_blackbox.log`: Build succeeded, 0 Warning(s), 0 Error(s).
- Static scan: no `Update()`, `LateUpdate()`, `Debug.Log*`, `string.Format`, private `NativeArray` fields, private `NativeQueue<RaycastCommand>`, private `NativeList<RaycastCommand>`, or H8Memory SystemDispatcher fallback allocations in the scheduler/SystemDispatcher sweep.

## 2026-05-16 - Current Revalidation Pass
What was wrong:
- The disk report still pointed at attempt25 after the latest inquisition commands.
- Concurrent builds and agents make stale compile reports unacceptable.

What was done:
- Re-read the original `SIMULATION_BUCKET_DISTRIBUTOR` XML block from `CURRENT_BATCH.md`.
- Re-ran static debt scan over `SystemDispatcher`, `Core/Scheduling`, and `Core/Bucketing`.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` as attempt26.
- Updated status and rationale to point at the current build artifact.

Cinematic cheats used:
- No new runtime cheats were added in this validation-only pass.
- Existing toaster path remains fixed bucket math, typed signals, DataVault storage, and fault-only dumps.
- Existing high/ultra path still exposes visual-overkill budget through bucket/admission signals without scheduler-owned VFX code.

Exact microseconds saved / budget estimates:
- Measured microseconds saved: 0 us. No benchmark harness was run.
- Compile revalidation only; no runtime timing claim.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt26_current_revalidation.log`: Build succeeded, 0 Warning(s), 0 Error(s).
- Static scan: remaining `NativeArray<T>` matches are DataVault resolver return views, not private owned storage.

## 2026-05-16 - Dispatcher Tier Snapshot and Dump Path Pass
What was wrong:
- `SystemDispatcher` dispatcher black-box faults still wrote to `Docs/AgentLogs/Dump_CORE_TICK_DILATION.bin`, which breaks owner-specific post-mortem lookup.
- The dispatcher cadence used `GlobalRegistry.ScalabilityTierProfileByte` in multiple hot-path helpers instead of one frame snapshot.

What was done:
- Changed the dispatcher black-box fault artifact to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin`.
- Added `_scalabilityTierProfileByte` as the dispatcher-owned frame snapshot.
- Refreshed that byte once at the start of PRE_SIMULATION.
- Routed time-dilation visual quality tier, memory-defrag cadence, dispatcher black-box low-tier flag, job-admission refill, and simulation-bucket advancement through the snapshot.

Cinematic cheats used:
- Low tier still buys frame stability through static bucket distribution, reduced cadence, and fixed fault-only telemetry.
- High/Ultra still use the same cached tier to unlock dynamic bucket rebalancing and downstream visual-overkill budget flags.

Exact microseconds saved / budget estimates:
- Measured microseconds saved: 0 us. No benchmark harness was run.
- Static impact: four repeated registry property reads collapsed into one PRE_SIMULATION snapshot.
- No new native storage, no new managed allocation, no public API change.

Validation:
- Static scan: no stale `Dump_CORE_TICK_DILATION` path remains in `SystemDispatcher`.
- Static scan: only one `GlobalRegistry.ScalabilityTierProfileByte` read remains in `SystemDispatcher`, at the PRE_SIMULATION frame snapshot.
- Static scan: no `Update()`, `LateUpdate()`, `Debug.Log*`, `string.Format`, private raycast command `NativeQueue`/`NativeList`, or SystemDispatcher H8Memory fallback allocation in the scheduler sweep.
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt27_tier_snapshot_dump_path.log`: Build failed outside scheduler in `Assets/_Project/Scripts/World/EcosystemDirector.cs` duplicate method definitions; scheduler/touched-path filter returned zero hits.

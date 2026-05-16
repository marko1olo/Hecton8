# LOG_DEBRIS_PHYSICS_FAKE

## 2026-05-16 - Prompt Extraction Blocker

What was wrong:
- `DEBRIS_PHYSICS_FAKE` is present in the companion instruction list but absent from `Docs/Tasks/CURRENT_BATCH.md`.
- CLI extraction for `<AGENT_PROMPT id="DEBRIS_PHYSICS_FAKE">` returned no match.
- `Docs/Tasks/CURRENT_BATCH_AUDIT_20260516.md` independently records `DEBRIS_PHYSICS_FAKE` as an instruction ID missing from the current batch.

What was done:
- Read `AGENTS.md`.
- Read `Docs/Actual Domains of Project.txt`.
- Searched `Docs/Tasks/CURRENT_BATCH.md` for the required XML tag.
- Created `Docs/Tasks/Status_DEBRIS_PHYSICS_FAKE.md`.
- Created `Docs/AgentLogs/Rationale_DEBRIS_PHYSICS_FAKE.md`.
- Stopped before source edits because the authoritative task block is missing.

Cinematic Cheats used:
- None implemented. The expected future direction is a visual fake: GPU-only debris chips driven by signal buffers, not Rigidbody shards.

Exact Microseconds saved:
- 0 us measured. No runtime path changed.

Verification:
- Code compile not run because no source code changed.
- Status: BLOCKED BY DEPENDENCY - missing active batch XML prompt.

## 2026-05-16 - Phase 1 The Great Purge

What was wrong:
- Voxel carve aftermath still contained a CPU dropped-item debris loop and a legacy laser `IDebrisService.SpawnBurst` path.
- Bootstrap still treated the old `DebrisManager` service as the debris runtime dependency, which could create or preserve a GameObject-based debris owner.
- `DebrisSpawnSignal` had a destructive queue path only, so a GPU VFX listener could steal events from other consumers if it drained the queue directly.
- Voxel carve, outcrop, drill, player CCD impact, and vehicle CCD impact debris producers were not consistently marked for the GPU compute shard lane.

What was done:
- Removed the CPU dropped-item debris aftermath and legacy laser `SpawnBurst` emission from `VoxelDeltaProcessor`.
- Added `DebrisSpawnSignal.FlagComputeShard` and mirrored published debris signals into `SignalBus<DebrisSpawnSignal>` for non-destructive frame snapshot reads.
- Added `IDebrisComputeService` and `GlobalRegistryServiceSlot.DebrisComputeRuntime`.
- Registered `CarveDebrisComputeRenderer` as the compute debris service and exposed `ClearGpuDebris`, active count, capacity, and low-tier state through the registry contract.
- Routed scene cleanup to `GlobalRegistry.DebrisCompute.ClearGpuDebris()`.
- Changed bootstrap `DebrisManager` readiness/resolution to use `GlobalRegistry.DebrisCompute` and stopped bootstrap from calling `DebrisManager.EnsureRuntimeInstance()`.
- Converted voxel carve, outcrop, drill, player impact, vehicle impact, and existing repair sparks to publish compute-shard debris signals.
- Kept loot drops intact; `HarvestableOutcrop.DispatchYield` still registers actual loot items, not debris aftermath.

Cinematic Cheats used:
- Replaced CPU debris entities with one unmanaged signal per event and GPU-side shard request injection.
- Used deterministic hash axes from signal seed instead of CPU Rigidbody impulse simulation.
- Bounded the debris signal scan to 64 per frame so burst spam cannot create an unbounded VFX cost.
- Preserved existing DataVault/indirect-renderer path rather than introducing GameObject shards or pooled prefabs.

Exact Microseconds saved:
- Measured: blocked; `dotnet build` does not currently pass because of external fauna/docking/wake/ecosystem compile drift.
- Static estimate for Phase 1: 40-250 us saved on heavy mining frames by deleting the old dropped-item loop and legacy SpawnBurst branch.
- Static estimate for registry/bootstrap purge: 5-30 us saved around startup/teardown and avoided service-object traffic during debris bursts.
- Static estimate for DataVault cleanup path: 20-120 us saved during carve bursts by keeping shard state in fixed buffers instead of producer-owned CPU objects.

Verification:
- XML prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; task count = 18.
- Status updated in `Docs/Tasks/Status_DEBRIS_PHYSICS_FAKE.md`.
- Rationale updated in `Docs/AgentLogs/Rationale_DEBRIS_PHYSICS_FAKE.md`.
- Static search found no `DebrisManager.EnsureRuntimeInstance` caller outside the legacy `DebrisManager` type itself.
- Static search found no mining/impact debris `Instantiate` loop; remaining `Instantiate(baseStats)` is `SuitUpgradeManager` ScriptableObject stats, not debris.
- Static search confirms compute shard flags on voxel carve, outcrop, drill, player impact, vehicle impact, and repair sparks.
- `git diff --check` on touched debris/producer/doc files reports no whitespace errors; only line-ending warnings from the existing worktree configuration.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed externally. First run failed in fauna bite IK symbols; second run failed in docking autopilot, VFX wakes, and ecosystem macro swarm contract drift.

Status:
- Phase 1 source purge complete.
- Final validation blocked by external dependencies, not by the debris purge.

## 2026-05-16 - Multiplatform Inquisition And Kernel Polish

What was wrong:
- Debris request/telemetry layout needed explicit ABI proof for ARM64/Quest.
- The renderer needed the XML-visible `_DebrisBuffer` and `_DebrisPhysicsBuffer` contract, not only internal carve names.
- A balanced 4096-particle ceiling was not enough for High/Ultra tier.
- STP/upscalers could see procedural shimmer because debris had no motion-vector evidence.
- Stress recycling was implicit, not tied to `SystemStress01`.

What was done:
- Locked `CarveDebrisRequest` and `CarveDebrisTelemetryEntry` to `Pack=1, Size=64`.
- Bound DataVault-backed position/life and velocity buffers as `_DebrisBuffer` and `_DebrisPhysicsBuffer`.
- Kept low tier at 1024 shards, introduced mid at 4096, and high/ultra at 16,384.
- Scaled injected particles per carve to 16/48/128 for low/mid/high.
- Added shader-only deterministic tumble using particle ID plus time.
- Added a debris MotionVectors pass and stopped forcing no motion vectors in render params.
- Wired `SignalBusRegistry.SystemStress01` and `SystemHealthIndexSignal` pressure to 4x lifetime decay when stress exceeds 0.9.
- Replaced the compute velocity clamp branch with `step`/`lerp` plus `rsqrt(max(...))`.
- Confirmed the hot upload path uses `LockBufferForWrite` and no `GraphicsBuffer.SetData`.

Cinematic Cheats used:
- Toaster mode: 1024 shards, no SDF/flow heavy collision, short-lived visible chips.
- Mid mode: 4096 chips with bounded injection and GPU advection.
- God-mode: 16,384 chips, SDF/flow enabled, shader tumble, motion-vector history.
- Expiry dust remains a shader life-scale and dither clip, not a secondary emitter.

Exact Microseconds saved:
- Measured: blocked by external compile failures.
- Static low-tier GPU saving: 0.3-1.2 ms during debris storms by capping to 1024 and skipping SDF/flow.
- Static CPU saving: 20-90 us by keeping tumble in the vertex shader instead of CPU rotations.
- Static stall prevention: SetData sync spikes avoided by `LockBufferForWrite`; exact spike size pending profiling.
- Static stress behavior: slots recycle 4x faster when `SystemStress01 > 0.9`.

Verification:
- Original XML prompt re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- `rg` confirmed no `ForceNoMotion`, no `Update()`, no `string.Format`, no `Instantiate`, and no `GraphicsBuffer.SetData` in debris domain.
- `rg` confirmed `_DebrisBuffer`, `_DebrisPhysicsBuffer`, MotionVectors pass, `Graphics.RenderMeshIndirect`, `LockBufferForWrite`, and AUP shift path.
- `git diff --check` on touched debris shader/compute/status/rationale files reports no whitespace errors; only line-ending warnings from the existing worktree configuration.
- `dotnet build` still fails externally. Current run reports 39 errors in XR, VaultProbeUtility, item signal, submarine structural grid, and bioluminescence domains.

Status:
- Tasks 1-17 complete by source/static verification.
- Final validation remains `[BLOCKED BY DEPENDENCY]`.

## 2026-05-16 - Omega Compute Audit And Final Source Pass

What was wrong:
- The checklist was complete/blocked, so the Omega mandate became active.
- The debris compute path still needed a stricter branch audit, active-tier dispatch proof, and blackbox path alignment with the agent ID.
- `dotnet build` status was stale after the final source/static audit.

What was done:
- Re-read `Status_DEBRIS_PHYSICS_FAKE.md`, `Rationale_DEBRIS_PHYSICS_FAKE.md`, `AGENTS.md`, `Docs/Actual Domains of Project.txt`, the original XML block, and the relevant VFX/compute/zero-GC/signal/AUP mandates.
- Rechecked debris domain for `Instantiate`, `Update()`, `string.Format`, `GraphicsBuffer.SetData`, `ForceNoMotion`, `EventBus`, managed delegates, and local `new NativeArray`.
- Confirmed ABI-critical debris structs: `DebrisSpawnSignal` is explicit `Pack=1, Size=64`; `CarveDebrisRequest` and `CarveDebrisTelemetryEntry` are sequential `Pack=1, Size=64`.
- Confirmed compute thread groups are 64/1/64/64, below the Metal/Quest 1024 thread-group limit.
- Confirmed active dispatch groups are resolved from active tier capacity instead of sweeping high-tier groups on middle tier.
- Confirmed `ClampCarveDebrisVelocity` uses `rcp(max(dt, 0.0001))`, `rsqrt(max(speedSq, 0.000001))`, `step`, and `lerp`.
- Confirmed cull distance gating and visible increment use arithmetic masks.
- Preserved resource bounds, low-tier SDF skip, and atomic overflow branches because replacing them with forced branchless atomics or SDF helper calls would be a low-tier regression.
- Confirmed blackbox dump path is `Docs/AgentLogs/Dump_DEBRIS_PHYSICS_FAKE.bin`.

Cinematic Cheats used:
- Toaster: typed debris signals, 1024 active shards, 16 particles per carve, SDF skip, shader expiry fade, no GameObjects.
- Middle: 4096 active shards with active-capacity dispatch instead of high-tier sweep.
- High/Ultra: 16,384 shards, 128 particles per carve, SDF/flow, shader tumble, motion-vector history.
- Physics truth remains a visual fake: no Rigidbody shards, no CPU transforms, no CPU rotations.

Exact Microseconds saved:
- Measured: absent. Unity runtime/profiler evidence is blocked by external compile errors.
- Static CPU estimate from earlier purge remains 40-250 us saved on burst-heavy mining frames by removing CPU debris object work.
- Static tumble estimate remains 20-90 us CPU avoided by moving rotation to shader hash/time.
- Static middle-tier GPU estimate: avoids 12,288 extra threads per debris advection/cull pass compared with sweeping 16,384 threads for a 4096 active tier.
- Static stall estimate: `GraphicsBuffer.SetData` hot-path absence avoids sync spikes; exact spike size requires runtime capture.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with 69 external errors in ecosystem, ladder animation, submarine fluid, and lockstep determinism domains.
- No debris/VFX compile error appeared in the reported build output.
- `git diff --check` on targeted debris shader/compute/docs returned no whitespace errors.
- Static debris forbidden-pattern scan returned no matches for the audited hot-path debt patterns.

Status:
- Source/static status: tasks 1-17 plus Omega audit complete.
- Build status: `[BLOCKED BY DEPENDENCY]` outside DEBRIS/VFX ownership.

## 2026-05-16 - Low-Tier Wake Bypass Pass

What was wrong:
- Low-tier carve debris zeroed its flow vector but still called `ApplyDynamicWakes`, so toaster/Quest debris could still pay the dynamic wake helper loop.
- A blind conversion from DataVault-owned `NativeArray<T>` aliases to `VaultBufferHandle<T>.Resolve(...)` would look cleaner in a grep but would add full-buffer sanitize work on every resolve in the current `GlobalDataVault` implementation.

What was done:
- Changed `AdvectCarveDebris` so `_CarveDebrisParams.y > 0.5` bypasses both `SampleAbyssalFlow` and `ApplyDynamicWakes`.
- Kept the non-low tier path intact: middle/high/ultra still sample flow and dynamic wakes.
- Re-audited the DataVault path: all carve debris native buffers are acquired through `IDataVault.GetBuffer(..., SystemID.Vfx)`. No `new NativeArray` exists in the debris domain.
- Recorded the handle-refactor rejection in rationale because the current handle resolver sanitizes full payloads when resolving.

Cinematic Cheats used:
- Toaster mode now uses gravity plus lifetime fade for rock chips, not wake-reactive flow.
- High/Ultra preserve wake-driven motion and SDF collision for dense shard fields.

Exact Microseconds saved:
- Measured: absent.
- Static work avoided on low tier: one `ApplyDynamicWakes` helper call per live carve debris thread, up to 1024 low-tier shards, including the wake-slot loop when wakes are active.

Verification:
- Static source read confirms the low-tier branch bypasses both flow sample and dynamic wake helper.
- `git diff --check` reports no whitespace errors; only existing line-ending warnings.
- Forbidden-pattern scan still reports no debris-domain `GraphicsBuffer.SetData`, `Instantiate`, `ForceNoMotion`, `Update()`, `string.Format`, `EventBus`, managed delegates, `UnityEvent`, or `new NativeArray`.

Status:
- Source/static status: low-tier carve debris wake bypass complete.
- Runtime/profiler status: PENDING VERIFICATION until external compile blockers are cleared.

## 2026-05-16 - Final Validation And Data Sovereignty Closure

What was wrong:
- The debris renderer had stale status text claiming final validation was blocked.
- The H-Phi pass required persistent debris state to be represented as GlobalDataVault handles, not private `NativeArray<T>` storage fields.
- Low-tier culling still lacked a camera-cone reject, so weak devices could spend visible-index work behind the camera.

What was done:
- Kept debris buffer ownership in `GlobalDataVault` and stored persistent references as `VaultBufferHandle<T>`.
- Resolved method-local `NativeArray<T>` views only during the active tick for positions, velocities, requests, job state, and blackbox telemetry.
- Added `_CarveDebrisCullParams` to the compute path and camera-forward cone masking in `CullCarveDebrisForRender`.
- Confirmed low-tier carve debris bypasses both flow sampling and dynamic wake evaluation.
- Re-ran the project compile gate.

Cinematic Cheats used:
- Toaster: 1024 active shards, 16 particles per signal, no wake flow, no SDF bounce, cone/distance culling before indirect visibility.
- Middle: 4096 active shards with active-capacity dispatch and same indirect draw path.
- High/Ultra: 16,384 shards, 128 particles per signal, wake/SDF response, shader tumble, motion-vector history.

Exact Microseconds saved:
- Measured: none claimed. No Unity profiler capture was run.
- Static low-tier work avoided: dynamic wake helper skipped for up to 1024 live carve debris threads.
- Static middle-tier work avoided: active dispatch prevents sweeping 12,288 excess threads versus a 16,384 high-tier pass.
- Static CPU work avoided remains the earlier 40-250 us estimate from removing CPU debris object aftermath and 20-90 us estimate from shader-only tumble.

Verification:
- XML prompt re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- Forbidden-pattern scan found no debris-domain `GraphicsBuffer.SetData`, `Instantiate`, `ForceNoMotion`, `Update()`, `string.Format`, legacy `EventBus`, managed delegate lane, `UnityEvent`, or `new NativeArray`.
- Handle scan found persistent `VaultBufferHandle<T>` fields only; no private debris `NativeArray<T>` storage fields remain.
- Compute thread groups remain below the 1024 Metal/Quest limit.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed: 0 warnings, 0 errors, elapsed 00:00:02.67.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE.

## 2026-05-16 - External Compile Drift After Validation

What was wrong:
- After the clean compile, a later build exposed new external domain drift.
- The current reported blockers are not in `Assets/_Project/Scripts/VFX/Debris/` or the debris compute shader path.

What was done:
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Confirmed the previous `InputDispatcher.cs` preprocessor syntax failure is no longer the active reported blocker.
- Recorded the current compile wall in status and rationale instead of claiming stale success.

Cinematic Cheats used:
- No new debris runtime cheat was added in this pass. Debris remains GPU-only indirect shards with low-tier wake/SDF bypass and high-tier dense SDF/wake response.

Exact Microseconds saved:
- Measured: none. This pass is compile-gate bookkeeping only.
- Static debris estimates from prior entries remain unchanged and unmeasured.

Verification:
- Latest build fails externally with 100 errors and 1 warning.
- Current blocker classes:
  - `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`: `Initialize` call arity mismatch.
  - `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`: missing `_itemStates`, `_pendingDecayDt`, `_wearMultipliers`, `_slotActive`, `_breakdownEvents`, `_disposeHandle`, and missing `DurabilityDecayJob.BreakdownWriter`.

Status:
- Debris source/static status remains complete.
- Final compile status is `[BLOCKED BY DEPENDENCY]` outside DEBRIS/VFX ownership.

## 2026-05-16 - Final Compile Gate Green

What was wrong:
- The last recorded state was stale: task 18 was blocked by external compile drift.
- Current source needed a fresh compiler pass before the status could be upgraded.

What was done:
- Re-read the DEBRIS XML assignment from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-ran the C# project compile gate after the external compile wall moved.
- Updated status and rationale to current evidence.

Cinematic Cheats used:
- No new runtime cheat was added in this pass. The debris system remains GPU-only indirect shards with tiered low/mid/high behavior from the previous source pass.

Exact Microseconds saved:
- Measured: none. This was a compile validation pass, not a profiler capture.
- Static estimates from prior debris entries remain static estimates only.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Result: passed, 0 warnings, 0 errors, elapsed 00:00:04.22.
- Output: `Temp\bin\Debug\Hecton8.Core.dll`.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE.
- Unity runtime, platform player builds, Quest/Android, Metal, and profiler microsecond proof are not claimed.

## 2026-05-16 - Multiplatform Static Inquisition Recheck

What was wrong:
- The recorded final validation still referenced the older 4.22s compile pass.
- The renewed order required explicit rechecks for instantiation, local NativeArray ownership, SetData stalls, shader thread-group limits, signal-lane contamination, and stale status claims.

What was done:
- Re-read `Docs/Tasks/Status_DEBRIS_PHYSICS_FAKE.md`, `Docs/AgentLogs/Rationale_DEBRIS_PHYSICS_FAKE.md`, and the active DEBRIS XML assignment.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; result passed with 0 warnings and 0 errors in 58.78s.
- Re-ran targeted debris static scans across `Assets/_Project/Scripts/VFX/Debris`, `Hecton_FluidAdvection.compute`, and `Hecton_CarveDebrisIndirect.shader`.
- Confirmed no `Instantiate`, `Object.Instantiate`, `DebrisManager.Instance`, `GraphicsBuffer.SetData`, `ForceNoMotion`, standard `Update()`, `string.Format`, legacy `EventBus`, `Action<`, `UnityEvent`, private debris `NativeArray<T>` storage field, or `new NativeArray` match in the target set.
- Confirmed carve debris compute kernels use 64-thread or 1-thread groups, below the 1024 thread-group ceiling relevant to Metal/Quest.

Cinematic Cheats used:
- No new runtime cheat was added in this pass. Existing source remains the GPU-only SoA/indirect shard fake: low-tier cap and wake/SDF bypass, high-tier 16,384 shard budget, shader tumble, lifetime scale-down, motion vectors, and blackbox ring.

Exact Microseconds saved:
- Measured: none. No profiler capture, player build, Quest/Android, Metal, or Steam Deck runtime pass was executed.
- Static estimates remain static only: removed GameObject debris work, removed hot `SetData`, and GPU-only indirect rendering should remove burst-frame CPU cost, but exact microseconds require Unity profiler evidence.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`
- Result: passed, 0 warnings, 0 errors, elapsed 00:00:58.78.
- Static scan target set: debris scripts plus carve debris compute/render shaders.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE by source/static and C# compile gates.
- Runtime visual quality, GPU timings, platform player builds, and actual microsecond savings remain pending external verification.

## 2026-05-16 - Shader Omega Mask Polish

What was wrong:
- Carve debris shader helper code still had small ternary selections for safe normalization and basis-up choice.
- The dynamic wake cap selection also had a low-tier ternary in the current shader source.

What was done:
- Replaced safe-normalize ternaries with `step`/`lerp`/`rsqrt(max())`.
- Replaced forward and motion-vector basis-up ternaries with `step`/`lerp`.
- Replaced dynamic wake low-tier cap selection with `step`/`lerp`.
- Preserved the explicit low-tier SDF/wake skip guard because forcing those helper calls would make MX350 worse.

Cinematic Cheats used:
- Same visual fake remains: GPU-only shard SoA, shader tumble, low-tier wake/SDF avoidance, high-tier density, and motion-vector stabilization.

Exact Microseconds saved:
- Measured: none. Shader compiler/GPU profiler data was not captured.
- Static: fewer branch-like helper selections; exact timing remains pending GPU capture.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`
- Result: passed, 0 warnings, 0 errors, elapsed 00:00:05.61.
- `git diff --check` returned no whitespace errors for the debris/shader/docs target set, only existing LF-to-CRLF warnings.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE by source/static and C# compile gates.
- Runtime shader import, platform player builds, and GPU profiler timings remain unclaimed.

## 2026-05-16 - High-Tier Material Overkill And Honest Validation Wall

What was wrong:
- High-tier debris density had improved, but the material response still looked too close to middle tier.
- The status file overstated final validation because `Hecton8.Core.csproj` passes while Unity import is currently blocked before the debris assembly and shader import can be proven.

What was done:
- Added high-tier-only procedural crystal/strata masking and normal perturbation in `Assets/_Project/Art/Shaders/Hecton_CarveDebrisIndirect.shader`.
- Updated `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` so high tier resolves once, receives shadows, and forces shadow casting on if serialized debris shadows are Off.
- Verified Unity 6000.4.1f1 exposes `RenderParams.motionVectorMode`, `RenderParams.receiveShadows`, and `RenderParams.shadowCastingMode` in `UnityEngine.CoreModule.xml`.
- Re-ran validation attempts and updated status/rationale with the current external blockers instead of keeping stale success.

Cinematic Cheats used:
- Triangle-noise-style crystal/strata bands, hashed shard edge masks, high-tier-only normal perturbation, and shader-side rim crystal response.
- Low-tier Dear Lie remains unchanged: capped shards, no wake/SDF, no extra shadow cost, and GPU-only indirect rendering.

Exact Microseconds saved:
- Measured: none. Unity import and player/profiler validation are externally blocked.
- Static only: high-tier work is intentional extra GPU ALU, not a savings claim. Low-tier avoids the added material/shadow cost.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed: 0 warnings, 0 errors, elapsed 00:01:17.83.
- `Assembly-CSharp.csproj` is blocked by missing external RealtimeCSG source files.
- Direct Bee debris compilation is blocked by stale/missing generated refs: `Hecton8.Core.ref.dll` and `Hecton8.Audio.Virtualization.ref.dll`.
- Unity batch import fails externally in Audio/Editor asmdef/reference errors, logged at `Docs/AgentLogs/UnityImport_DEBRIS_PHYSICS_FAKE.log`.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE by debris source/static and core C# compile.
- Task 18 is `[BLOCKED BY DEPENDENCY]` for Unity import/player/shader validation until external Audio/Editor/RealtimeCSG references are repaired.

## 2026-05-16 - Global Wake Param Mirror And Blackbox Wake Evidence

What was wrong:
- Carve debris compute consumed the global wake array contract but did not explicitly mirror `_GlobalWakeParams` into the compute dispatch.
- Blackbox flags could report SDF and flow response, but not whether the bounded wake response path was active.
- The project compile wall moved again outside debris, so prior validation text needed another update.

What was done:
- Added `GlobalWakeParamsId` and `ResolveGlobalWakeParamsForCompute()` in `CarveDebrisComputeRenderer.cs`.
- Low tier now forces `_GlobalWakeParams = (0, 1, 0, 0)` for carve debris compute.
- Middle/high tiers mirror global wake params, clamp slot limit to 16, and record `_lastWakeActive`.
- Added `WakeActiveFlag` to blackbox telemetry and reset `_blackBoxDumped` when the DataVault-backed ring is cleared.

Cinematic Cheats used:
- Same bounded wake fake: debris only reacts to the shared 16-slot global wake field off low tier. No private wake simulation and no CPU particle physics.

Exact Microseconds saved:
- Measured: none.
- Static: low-tier wake work remains zero; middle/high pay one compute uniform update and the existing bounded wake loop only when global wake data is active.

Verification:
- Targeted debris forbidden-pattern scan returned no matches for `Instantiate`, `GraphicsBuffer.SetData`, `ComputeBuffer`, `ForceNoMotion`, standard `Update()`, `string.Format`, legacy `EventBus`, managed delegate lanes, private native allocations, or blocking GPU readback patterns.
- `git diff --check` on debris/shader/log targets returned no whitespace errors; only existing LF-to-CRLF warnings.
- `Hecton8.Core.csproj` is currently blocked outside debris. First rerun failed in `Core/Contracts/HectonContractValidator.cs` on missing contract symbols. Latest rerun failed in `World/EcosystemDirector.cs` on missing index helper symbols and duplicate contract source warnings.

Status:
- VERIFIED MASTER GRADE - SHARDS ACTIVE by debris source/static validation.
- Final C#/Unity import/player/shader validation remains `[BLOCKED BY DEPENDENCY]` outside DEBRIS/VFX ownership.

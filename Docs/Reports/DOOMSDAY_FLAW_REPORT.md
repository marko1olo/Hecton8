# HECTON-8 DOOMSDAY FLAW REPORT

Generated: 2026-05-01  
Mode: Deep Flaw Discovery / Forensics / QA Architecture  
Status: PENDING VERIFICATION - May 4 MCP console read returned `0` errors and `18` warnings; no PlayMode/profiler/GC proof
Scope: `Assets/_Project/Scripts/`

## Executive Read

This is a static forensic report. No Play Mode was launched. No GCMonitor, Jobs Debugger, Memory Profiler, RenderDoc, or 10-minute retention run was captured. Findings are code-review evidence, not runtime measurements.

## 2026-05-04 Source Recheck Delta

Follow-up evidence: `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`.

Current scan surface now reads:

- `Assets/_Project/**/*.cs`: `1118` first-party C# files.
- `Assets/_Project/Scripts/**/*.cs`: `1078` C# files.
- Current filesystem LOC under `Assets/_Project/Scripts`: `519952`.
- `.agents-skills`: `52` mandate files indexed.

Current post-repair foundation guard scan exits `0`:

- `.Run(` sites: `0`.
- Hot-path `.Run(` review sites: `0`.
- `.Complete(` text hits: `5`.
- Guarded dispatcher completion sites: `1`.
- `UnsafeUtility.MemCpy outside guard`: `0`.
- Unauthorized Unity loop methods: `0`.
- Runtime Find API review hits outside Editor folders: `8`.

The May 2 `.Complete(` count and source-size snapshot below are historical. Current risk classification remains static/source-only until PlayMode, profiler, GC, and memory-retention evidence exists.

## 2026-05-02 Source Recheck Delta

Follow-up evidence: `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`.

Historical May 2 scan surface read:

- `Assets/_Project/Scripts/`: `1047` C# files.
- May 2 filesystem LOC under `Assets/_Project/Scripts`: `571562`.
- `.agents-skills`: `52` mandate files indexed.

Current strict `.Complete(` grep under `Assets/_Project/Scripts` finds `6` text hits:

- `ItemCatalog.cs`: `dispatcher.Complete(...)` request completion callbacks.
- `Optimization/AssetLifecycleGovernor.cs`: `dispatcher.Complete(...)` request completion callbacks.
- `World/DispatcherJobSwap.cs`: one explicit `JobHandle.Complete()` in the dispatcher swap helper.

This makes the older broad claim that `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` currently own `.Complete()` barriers stale by strict source grep.
Remaining risk is narrower: prove `DispatcherJobSwap` is only called in a legal dispatcher/end-of-frame swap window and capture runtime stall profiling.

## 2026-05-01 Source Recheck Delta

Follow-up evidence: `Docs/Reports/2026-05-01_HEADLESS_FAUNA_CONSOLE_DELTA.md`.

Current source no longer matches the original `FaunaBrain.UpdateBioluminescentHypnosis()` camera-specific finding. The method now reads `PlayerRuntimeContext.LookState` and uses the blittable `PlayerLookState` snapshot (`EyePosition`, `AimForward`, `Flags`) instead of `runtimeContext.PlayerCamera`. The specific "no Camera component disables dazzle gameplay" defect is source-fixed.

This does not prove fauna headless correctness. `FaunaSensorSuite` still uses player `Transform` / Rigidbody references for perception and distance gating, and no no-camera headless Play Mode test has been run.

Original May 1 scan surface:

- `Assets/_Project/Scripts/`: 1020 C# files.
- First-party C# LOC scanned during the original Doomsday pass: 466768.
- Historical 2026-05-01 filesystem LOC under `Assets/_Project/Scripts`: 544728. Superseded for count purposes by the May 4 scan above.
- `.agents-skills`: 52 mandate files indexed.
- Mandates loaded for classification: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `CORE_Submarine_Vehicles_Kinematics_AUP`, `PHYS_Physics_Integrity_Determinism_ForceMode`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

Primary risk model:

- CPU: old frame-lane `.Complete()` barrier examples need reclassification; May 2 strict grep only finds one explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs` plus custom dispatcher request completion callbacks. Runtime stall proof is still absent.
- Headless correctness: some gameplay state transitions still depend on Camera or Animator components.
- Native memory: 122 files contain `Allocator.Persistent` or `Allocator.TempJob`; the one clear no-local-dispose outlier is BRG direct-draw TempJob memory.
- Physics correctness: `~0` / Everything masks and default layer fallbacks remain in runtime query paths.
- AUP correctness: `FaunaBrain` caches raw world-space route points without implementing origin-shift listener behavior.
- Event safety: `SystemDispatcher` has a late-frame budget breaker and `HectonEventBus` now has a hard max-depth cap. Same-frame generation split is still not proven across NativeQueue-backed event lanes.

## Surgery Log: 3 Most Dangerous Flaws

1. CRITICAL: original `FaunaBrain.UpdateBioluminescentHypnosis()` camera dependency is stale by current source recheck; broader fauna headless proof is still absent because perception still depends on player Transform/Rigidbody paths and no no-camera Play Mode test was run.
2. CRITICAL: original `StorageCrate.OpenCrate()` Animator-event dead-state finding is stale by current source recheck; `OpenCrate()` now calls `CompleteOpen()` directly and `OnAnimationComplete()` is idempotent fallback. Play Mode proof is still absent.
3. HIGH: original `JobHandle.Complete()` in `Tick` / `PostFixedTick` lane finding is stale by strict source grep. Current explicit `JobHandle.Complete()` evidence is `World/DispatcherJobSwap.cs`; verify legal swap-window usage and runtime stall cost before closure.

## CRITICAL Findings

### CRITICAL-01: Fauna Gameplay Depends On Player Camera

Evidence:

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:830` returns if `runtimeContext.PlayerCamera == null`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:835` computes `transform.position - runtimeContext.PlayerCamera.transform.position`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:840` reads `runtimeContext.PlayerCamera.transform.forward`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:845` calls `runtimeContext.PlayerMovement.ApplyFaunaHypnosisPull(...)`.

Failure path:

`UpdateBioluminescentHypnosis()` is not presentation-only. It mutates player movement if the player is looking at a dazzle-capable fauna. In a headless simulation there may be no Camera component. The method returns early, so the AI/player state diverges from visual-client state.

Mathematical reason:

The predicate is effectively:

`DazzleActive = CameraExists AND distance <= range AND dot(cameraForward, faunaDirection) >= threshold`

For headless simulation, `CameraExists = false`, therefore `DazzleActive = false` for every possible fauna/player state. That is a different state machine, not a missing visual.

Required fix:

- Move look/aim truth into a headless-safe snapshot: `PlayerLookState { float3 eyePosition; float3 aimForward; uint frame; }`.
- Produce the snapshot from input/player runtime state, not from `Camera`.
- Presentation Camera may consume the snapshot; fauna logic must not depend on Camera.
- Add a headless test: dazzle-capable fauna + synthetic look vector + no Camera must still apply the gameplay effect.

Regression model:

- CPU: neutral if snapshot is already produced by player runtime context.
- GC: must be 0 B/frame; snapshot must be struct-backed.
- Correctness: fixes visual/headless divergence.
- Failure mode if done badly: camera presentation and gameplay aim can desync if two sources remain.

### CRITICAL-02: Storage Crate State Depends On Animator Event

Status: STALE BY 2026-05-02 SOURCE RECHECK. Keep as historical evidence only.

Current source evidence:

- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:314` calls `CompleteOpen()` directly from `OpenCrate()`.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:356` keeps `OnAnimationComplete()` as an idempotent fallback.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:875` guards `CompleteOpen()` against double-open state.

Original evidence:

- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:296` sets `_state = CrateState.Opening`.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:307` triggers `animator.SetTrigger(_openTriggerHash)`.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:314` completes immediately only when `animator == null`.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:359` exposes `OnAnimationComplete()`.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:361` completes the open transition only from that animation event.
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:406` blocks item transfer unless `_state == CrateState.Open`.

Failure path:

If an Animator component exists but its controller is disabled, stripped, missing the event, or the clip never fires `OnAnimationComplete`, the crate remains in `Opening`. Inventory access is then blocked permanently.

Mathematical reason:

The state graph is:

`Closed -> Opening -> Open`

The second edge is presentation-driven when `animator != null`. If the presentation event is absent, `Opening` has no timeout or logic-owned exit edge.

Original required fix:

- Logic owns the open transition; animation only reads state.
- Use a gameplay timer or deterministic immediate state transition independent of Animator event.
- Animator event can still notify presentation, but it must not be the only route to `Open`.
- Add test: Animator component exists, no animation event, crate still reaches `Open`.

Regression model:

- CPU: negligible.
- GC: no new allocations required.
- Correctness: removes visual dependency from inventory access.
- Failure mode if done badly: double `OnOpened` event if animator event and timer both complete without idempotent guard.

## HIGH Findings

### HIGH-01: Job Completion Barriers In Frame Lanes

Status: PARTIALLY STALE BY 2026-05-02 STRICT SOURCE GREP.

Current evidence:

- `Assets/_Project/Scripts/ItemCatalog.cs`: two `dispatcher.Complete(...)` request completion callbacks.
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`: three `dispatcher.Complete(...)` request completion callbacks.
- `Assets/_Project/Scripts/World/DispatcherJobSwap.cs`: one explicit `JobHandle.Complete()` in the dispatcher swap helper.

Original method-context scan, now stale:

- `Assets/_Project/Scripts/ProximityColliderSystem.cs:426` declares `Tick(float deltaTime)`.
- `Assets/_Project/Scripts/ProximityColliderSystem.cs:456` calls `_jobHandle.Complete()`.
- `Assets/_Project/Scripts/SaveManager.cs:300` declares `Tick(float deltaTime)`.
- `Assets/_Project/Scripts/SaveManager.cs:304` calls `_integrityScanHandle.Complete()`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs:789` declares `PostFixedTick(float fixedDeltaTime)`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs:796` calls `_scheduledBuoyancyHandle.Complete()`.

Original scan totals, now stale:

- Total `.Complete(` lexical hits in first-party scripts: 149.
- Direct frame-lane `.Complete()` hits found by method-context scan: 3.
- `.Complete()` inside job `Execute()` methods: none found by static method-context scan.

Current failure path:

The old listed call sites are not current by grep. The remaining `DispatcherJobSwap` helper may be legal if it is only reached from the dispatcher-owned swap window. Without runtime/frame-phase proof, it remains a stall-risk candidate, not a closed defect.

Mathematical reason:

With `n` independent job owners and local result application, there can be up to `n` independent generation swaps per frame. System A can consume job output for frame `N` while System B still exposes frame `N-1`. This is not a guaranteed deadlock, but it is a race-prone cadence model.

Required fix:

- Move frame-lane completions into a dispatcher-owned `LateFrameJobSwap` stage.
- Producers write only to back buffers/queues.
- Consumers read only front buffers/queues.
- `SystemDispatcher` owns `IsCompleted -> Complete -> swap -> telemetry`.
- If a handle is incomplete at the swap stage, defer consumption and emit telemetry; do not locally force completion from gameplay `Tick`.

Regression model:

- CPU: may improve worst-case frame variance; may add one-frame result latency.
- GC: no allocation needed.
- Correctness: improves generation consistency.
- Failure mode if done badly: stale commands for one extra frame; must be explicitly accepted per system.

### HIGH-02: Collision Matrix Holes Via Everything / Default Masks

2026-05-01 source delta:

- Patched to named/fallback masks: `GravityTetherTool`, `PhysicalInteractionHandler`, `PlayerInteraction`, `HectonMusicDirector`, `HectonVoxelVolume`, `ResourceNode`, `SubmarineFluidDynamics`, `AbyssalThermalManager`.
- Still open pending scene-layer verification: `AutonomousExtractorSystem`, `WorldCaveDirector`, `WorldProceduralFieldSampler`.
- Runtime proof is absent; Play Mode was not launched.

Evidence:

- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:706` uses `Physics.OverlapSphereNonAlloc`; `:710` passes `~0`.
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:495` uses `Physics.OverlapSphereNonAlloc`; nearby query path uses broad/default behavior.
- `Assets/_Project/Scripts/ResourceNode.cs:529` uses `Physics.OverlapSphereNonAlloc`; `:533` passes `~0`.
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:3234` uses `Physics.OverlapSphereNonAlloc`; `:3238` passes `~0`.
- `Assets/_Project/Scripts/WorldCaveDirector.cs:1113` uses `Physics.RaycastNonAlloc`; `:1117` passes `~0`.
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2299` uses `Physics.SphereCastNonAlloc`; `:2305` passes `~0`.
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs:2705` uses `Physics.RaycastNonAlloc`; `:2710` passes `~0`.
- `Assets/_Project/Scripts/GravityTetherTool.cs:33` serializes `LayerMask interactableMask = ~0`.
- `Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs:91` serializes `LayerMask panelButtonMask = ~0`.
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:194` warns if `interactableMask.value == ~0`, but does not reject the configuration.

Failure path:

Everything masks couple gameplay to unrelated colliders. New VFX, debug, trigger, or third-party colliders can silently enter gameplay query results. Hit ordering can change without code changes.

Mathematical reason:

Candidate set with Everything mask is `C_all`; domain mask candidate set is `C_domain`. In dense scenes, `C_all >> C_domain`, so query cost and result ambiguity scale with unrelated content.

Required fix:

- Runtime physics queries must use centralized `HectonLayerMasks`/domain masks.
- Serialized LayerMasks for gameplay must fail validation when equal to Everything.
- `~0` in Physics query arguments should be a static audit error outside editor/test code.

Regression model:

- CPU: likely improves query cost.
- GC: no change.
- Correctness: reduces accidental hits.
- Failure mode if masks are incomplete: legitimate targets can become unqueryable; requires scene validation.

### HIGH-03: AUP Drift In Fauna Route Cache

Evidence:

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:23` class does not implement `IOriginShiftListener`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:165` stores `_voxelRouteWaypoints` as raw `Vector3[]`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:178` stores `_voxelRouteTargetPosition` as raw `Vector3`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:725` builds route waypoints from runtime world positions.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:728` caches `_voxelRouteTargetPosition`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:744` and `:749` consume cached route waypoints later.

Failure path:

Floating origin rebases runtime coordinates. The cached route remains raw pre-shift `Vector3`. Since `FaunaBrain` is not an origin-shift listener and the route is not AUP-backed, the route can become offset by the shift vector.

Mathematical reason:

If origin shift applies vector `S`, cached runtime waypoint `W` must become `W + S` or be recomputed from absolute coordinates. Current cache keeps `W`. Error after one shift is `|S|`; at 5000m threshold, path error can be kilometers.

Required fix:

- Store route waypoints as AUP/int64 grid + local offset.
- Or implement `IOriginShiftListener` and atomically shift `_voxelRouteWaypoints` and `_voxelRouteTargetPosition`.
- During `HectonFloatingOrigin.IsShiftInProgress`, route consumption must pause or use absolute state only.

Regression model:

- CPU: small if shifting array on origin event; better if AUP source is used.
- GC: no allocation required.
- Correctness: prevents kilometer-scale path drift.
- Failure mode if done badly: double-applying shift to waypoints.

### HIGH-04: Event Bus Has Budget Breaker And Depth Cap, But No Generation Split Proof

Evidence:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:32` defines `MaxLateFrameEventsPerFrame = 1000`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:450` enters `LateUpdate()`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:475-487` flushes multiple event lanes sequentially.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:522` resets the late-frame dispatch budget.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:493` `TryConsumeLateFrameEventDispatch()` consumes dispatch budget.
- `Assets/_Project/Scripts/Interaction/InteractionEvents.cs:127` `FlushPending()` drains while the queue is non-empty.
- `Assets/_Project/Scripts/Interaction/InteractionEvents.cs:331` `DrainWithoutDispatch()` drains with no dispatch, used when there are no listeners.
- `Assets/_Project/Scripts/CraftingEvents.cs:151` `FlushPending()` uses the same budgeted pattern.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:136` defines `MaxDispatchDepth = 4`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:321-337` rejects dispatch when the global depth reaches the cap.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:462`, `:616`, and `:778` route unmanaged, native-byte, and managed event dispatch through the global guard.

Failure path:

The previous unbounded-event defect is partially addressed: late-frame event dispatch has a frame budget and the managed mod bus has a hard recursion-depth cap. The remaining risk is same-frame reenqueue in NativeQueue-backed lanes. The flush drains the active queue while it is non-empty; events published during handling can extend the same lane until the global budget trips.

Mathematical reason:

The new upper bound is `MaxLateFrameEventsPerFrame = 1000`, so infinite same-frame loops are cut. But without generation split, a cycle still consumes the full budget every frame. That is bounded but still pathological: `1000 * handlerCost` per frame until the cycle source is removed.

Required fix:

- Keep the current budget breaker and existing `HectonEventBus` depth cap.
- Add generation split: front queue drained this frame, back queue receives publishes during flush.
- Emit zero-alloc telemetry when budget trips or depth cap rejects/defer events.

Regression model:

- CPU: bounded and more predictable.
- GC: no allocation required with NativeQueue double buffering.
- Correctness: events published by handlers shift to next frame; systems depending on same-frame reentrancy need explicit opt-in.

## MEDIUM Findings

### MEDIUM-01: BRG TempJob Direct-Draw Allocation Has No Project-Owned Free Path

Evidence:

- `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:151` comments that Unity owns TempJob memory after the callback returns.
- `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:163` allocates `visibleInstances` with `UnsafeUtility.Malloc(... Allocator.TempJob)`.
- `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:171` allocates `drawCommands` with `UnsafeUtility.Malloc(... Allocator.TempJob)`.
- `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:179` allocates `drawRanges` with `UnsafeUtility.Malloc(... Allocator.TempJob)`.
- Static sweep found no `Dispose()` / `UnsafeUtility.Free` in this file.

Risk:

This may be valid Unity BRG ownership behavior, but the project has no local proof in source. If the ownership assumption is wrong for Unity 6000.4, memory growth is linear per culling callback.

Required verification:

- Confirm Unity 6000.4 BRG culling callback ownership contract.
- Add source-linked inline exception if Unity owns the memory.
- Run Memory Profiler and Unity leak detection for culling-heavy scene.

### MEDIUM-02: MaterialPropertyBlock On Standard Geometry / Headless Presentation Coupling

Evidence examples:

- `Assets/_Project/Scripts/InteractionHighlighter.cs:402` / `:420` uses renderer property blocks.
- `Assets/_Project/Scripts/BuilderTool.cs:450` / `:485` uses MPB on tool LCD renderer.
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs:587` / `:612` uses MPB on visor renderer.
- `Assets/_Project/Scripts/Gameplay/BioReactor.cs`, `SolarPanel.cs`, `SealedDoor.cs`, and similar gameplay props contain MPB status bridges in scan output.

Risk:

Some are legitimate presentation paths, but project mandate forbids MPB on standard geometry because it can break SRP Batcher residency. This is mostly render-performance risk, not headless correctness, unless gameplay reads renderer state elsewhere.

Required fix:

- Classify MPB uses into allowed legacy UI/particle/BRG exceptions vs standard geometry.
- Move standard geometry state to material variants, CBUFFER-compatible material data, or GPU instancing buffers.
- Verify SetPass/SRP Batcher before and after.

### MEDIUM-03: Editor Defines Exist Inside Hot Paths

Evidence:

- Static scan found 885 `#if UNITY_EDITOR` occurrences in runtime script tree.
- 74 are inside detected frame lanes (`Tick`, `FixedTick`, `SlowTick`, `LateFrameTick`, `Update`, `LateUpdate`, `FixedUpdate`).
- Examples: `SystemDispatcher.cs:401`, `SystemDispatcher.cs:460`, `PlayerToolManager.cs:295`, `PlayerInteraction.cs:330`, `FaunaBrain.cs:1432`, `CameraJuiceSystem.cs:430`.

Production result:

These blocks are compiled out of non-editor builds if they are exactly `#if UNITY_EDITOR`. They should not slow production builds. They do contaminate Editor/Development profiling and can hide production-only timing behavior.

Required fix:

- Do not move editor diagnostics into production.
- For hot paths, prefer static telemetry counters guarded by `DEVELOPMENT_BUILD` only when required.
- Keep Editor-only debug cost out of benchmark captures.

## Native Memory Sweep Result

Observed:

- 122 files contain `Allocator.Persistent` or `Allocator.TempJob`.
- Files with allocation tokens but no local dispose/free path: `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs`.
- Many Persistent allocations have visible dispose paths in owner files; this report does not certify every path without Memory Profiler/leak detection.

Required verification:

- Enable full leak detection stack traces.
- Run scene for 10 minutes under heavy culling / world streaming.
- Compare native memory slope, not snapshot.
- Fail build on TempJob leak warnings.

## Collision Matrix Sweep Result

Primary issue is not alloc-heavy Physics APIs; most sampled calls are NonAlloc or `RaycastCommand`. The defect is mask discipline:

- `~0` / Everything exists in runtime query arguments.
- Serialized gameplay masks default to `~0`.
- Some validation only warns.

Required validation:

- Static gate: fail runtime files containing `~0` in Physics query arguments.
- Boot validator: reject gameplay `LayerMask` values equal to Everything.

## Event Bus Circuit Breaker State

Current state:

- `SystemDispatcher` has a global late-frame dispatch budget and logs circuit-breaker trips in editor/development builds.
- Event lanes call `SystemDispatcher.TryConsumeLateFrameEventDispatch()`.
- `HectonEventBus` defines `MaxDispatchDepth = 4` and rejects managed/native mod event dispatch beyond that depth.

Remaining gap:

- No generation split is proven.
- NativeQueue-backed first-party lanes can still process same-frame reenqueue until the global budget trips.

Minimum acceptable model:

```csharp
// Pseudocode only. Do not paste directly.
Swap(front, back);
int processed = 0;
while (processed < MaxEventsPerFrame && front.TryDequeue(out payload))
{
    processed++;
    Dispatch(payload); // Publish() writes to back, not front.
}
```

## Verification State

MCP console log: May 4 readback returned `0` error entries and `18` warning entries.
Play Mode: editor was seen in Play Mode transition on May 4, but no bounded gameplay run or assertion pass was captured.
GC validation: measured proof absent.
Regression check: measured proof absent.
Memory retention guard: measured proof absent.
In-game result: not verified; this is a report-only operation.

## Evidence Commands

```powershell
rg --files Assets/_Project/Scripts -g "*.cs"
rg -n "\.Complete\s*\(|JobHandle\.Complete\s*\(" Assets/_Project/Scripts -g "*.cs"
rg -n "Physics\.(Raycast|SphereCast|CapsuleCast|BoxCast|OverlapSphere|OverlapBox|OverlapCapsule|RaycastAll|SphereCastAll|OverlapSphereNonAlloc|RaycastNonAlloc|CapsuleCastNonAlloc|SphereCastNonAlloc)\b|RaycastCommand\.ScheduleBatch|LayerMask\.GetMask|LayerMask\.NameToLayer|DefaultRaycastLayers|IgnoreRaycastLayer|~0" Assets/_Project/Scripts -g "*.cs" -g "!**/Editor/**"
rg -n "Camera\.main|GetComponent<Camera>|MeshRenderer|SkinnedMeshRenderer|\bAnimator\b|MaterialPropertyBlock|SetPropertyBlock|\.GetPropertyBlock" Assets/_Project/Scripts -g "*.cs"
rg -n "Allocator\.(Persistent|TempJob)|new Native(Array|List|HashMap|HashSet|Queue|ParallelHashMap|ParallelMultiHashMap)|UnsafeUtility\.Malloc" Assets/_Project/Scripts -g "*.cs"
rg -n "while \([^\n]*TryDequeue|FlushPending|_dispatchDepth|MaxFlush|Max.*PerFrame|LateUpdate\s*\(" Assets/_Project/Scripts -g "*.cs"
```

## Mandate Compliance Statement

- Zero-GC: report-only file edit; no runtime hot path changed.
- Native memory/job protocol: `.Complete()` findings classified against dispatcher-owned barrier rule.
- AUP/floating-origin: raw world position caches checked against listener/AUP contract.
- Physics integrity: NonAlloc use separated from layer-mask correctness; Everything/default masks flagged.
- Global registry/event architecture: dispatcher circuit breaker and `HectonEventBus` depth cap verified by source; generation split remains a gap.
- Telemetry/post-mortem: no fake runtime proof; unmeasured items stay `PENDING VERIFICATION`.

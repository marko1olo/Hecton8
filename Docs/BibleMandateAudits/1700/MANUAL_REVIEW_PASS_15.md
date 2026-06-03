# Manual Hotspot Classification Pass 15 - Core Runtime Boundary And Lazy Native Init

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

This pass classifies the core runtime architecture triage noise around logging, bootstrap `Update`, cold lookups, scene-transition UI, and lazy native initialization. The goal is to stop treating every `Debug.Log`, `GetComponentsInChildren`, or `Allocator.Persistent` hit as equal while still refusing to call lazy first-use allocation release-clean without boot proof.

## Files Reviewed

- `Assets/_Project/Scripts/Core/H8Debug.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`
- `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs`
- `Assets/_Project/Scripts/Core/PlayerSensoryManager.cs`
- `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs`
- `Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs`
- `Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs`
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`
- `Assets/_Project/Scripts/Core/UIStateStore.cs`
- `Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs`
- `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs`
- `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`
- `Assets/_Project/Scripts/Core/BurstCallback.cs`

## Method Evidence

### H8Debug And Direct Debug Boundaries

- `H8Debug` methods are decorated with `[Conditional("UNITY_EDITOR")]` and `[Conditional("DEVELOPMENT_BUILD")]`. Calls to the facade are omitted from non-development player IL, including argument evaluation at the callsite.
- Therefore most `H8Debug.*` hits in runtime triage are `LEGAL_EDITOR_OR_DEV_GUARDED` when they only route through this facade.
- Direct `Debug.Log*` calls remain separate. `GameBootstrapper.TryValidateSceneRootBudget(...)`, `FailSceneActivation(...)`, `VerifySingletons(...)`, and several fatal/bootstrap watchdog routes call Unity `Debug` directly or inside partial `#if` guards. These are not per-frame gameplay logs, but release closure still requires RB-113 proof: fatal/boot-only route, black-box record, or compile/development guard.

Verdict: `GREEN_H8DEBUG_FACADE`, `YELLOW_DIRECT_DEBUG_FATAL_BOOT_BOUNDARY_PROOF_REQUIRED`.

### GameBootstrapper

- `GameBootstrapper.Update()` only calls `EnsureBootstrapProgressAfterLifecycleResume()` while `_isBootstrapComplete` is false. Once boot is complete it becomes inert.
- The bootstrap state machine is explicit (`HardwareCheck`, `MemoryPreWarm`, `CoreServices`, `Environment`, `Player`, `UI`, `SceneActivate`, `Complete`, `Fatal`) and records boot state markers.
- Bootstrap presentation creates camera, lights, TMP labels, and materials as cold boot presentation objects. These objects are acceptable only if they are destroyed or excluded from normal scene runtime and do not become a hidden UI/render asset factory.
- Scene graph validation and service verification direct logs are fatal/boot guards, not normal gameplay telemetry, but still need RB-113 closure.

Verdict: `GREENISH_BOOTSTRAP_UPDATE_INERT_AFTER_COMPLETE_WITH_BOOT_PROOF_REQUIRED`.

### SceneRuntimeService

- `LoadSceneAsync(...)` owns an async scene activation gate and yields through activation readiness rather than a private gameplay tick loop.
- Scene transition overlay creation is cold scene-transition work, but it creates `GameObject` UI roots and a `new Material(ditherShader)` before assigning `image.material`.
- `FindActiveCameraInScene(...)` scans root GameObjects and cameras during scene activation, not as a gameplay read accessor.
- Shader fallback lookup is editor/development guarded, but release needs assigned shader/material proof for the transition path and lifecycle proof that material/UI roots are not repeatedly created/leaked across scene loads.

Verdict: `YELLOW_SCENE_TRANSITION_COLD_UI_MATERIAL_LIFECYCLE_PROOF_REQUIRED`.

### PlayerRuntimeContextService And PlayerSensoryManager

- `PlayerRuntimeContextService` uses cold rebind paths (`SyncPlayerContextColdInternal`, `CachePlayerHierarchyReferencesCold`, `RefreshDynamicContextReferencesCold`) for `TryGetComponent` and visor hierarchy scans.
- Its no-cold-lookup hot path (`SyncPlayerContextInternalNoColdLookups`) avoids hierarchy scans and republishes cached runtime context.
- `PlayerSensoryManager.Tick(...)` calls `RefreshSensoryContextHot()`. The hot path reads cached context and active runtime references; `GetComponentsInChildren` appears in `SyncSensoryContextCold()` on enable/hot-swap/rebind.
- `PlayerSensoryManager.EnsureRuntimeInstance()` can still create a `[PlayerSensoryManager]` GameObject if no authored/registered runtime exists. That is a bootstrap recovery route, not a release-normal composition route.

Verdict: `GREENISH_COLD_REBIND_LOOKUPS_WITH_REBIND_COUNT_PROOF_REQUIRED`, plus `YELLOW_AUTHORED_PLAYER_SENSORY_BOOTSTRAP_PROOF_REQUIRED`.

### H8PrefabRegistry And URP Texture Guard

- `H8PrefabRegistry.OnEnable()` validates entries; prefab renderer scanning for VRAM estimation is editor-facing in the reviewed route.
- `OnValidate()` can request live sync when play mode validation is enabled, but `OnValidate` is editor-only. Release player proof needs no active editor live-sync dependency.
- `HectonUrpTextureRequirementsGuard` can scan scene cameras into scratch buffers and cache URP camera data. This reads as boot/validation texture-policy work, not gameplay camera search, but it still requires boot/dev route proof.

Verdict: `GREENISH_EDITOR_OR_BOOT_VALIDATION_LOOKUP_WITH_BUILD_PROOF_REQUIRED`.

### Core Native Queues, Telemetry, And UI State

- `GlobalTelemetryBus.EnsureInitialized()` lazily allocates a fixed black-box ring, snapshot buffer, and export scratch. After initialization, publish routes write fixed records without managed text payloads.
- `UIStateStore.EnsureInitialized()` lazily allocates fixed native UI state arrays and event rings. However, many UI state writers call `EnsureInitialized()` directly, so the first user interaction can become an allocation event unless bootstrap prewarms it.
- `ThreadSafeCommandQueue.Initialize()` lazily creates and prewarms the structural command `NativeQueue`. `EnsureStorageReservationCommitResolvedQueue()` lazily creates another persistent queue for storage acknowledgements.
- `SignalBusRuntime.EnsureInitialized()` lazily creates each generic signal lane ring and H8Memory writer budget array on first use.
- `FrameTimeWatchdog` and `BurstCallbackQueue` allocate fixed persistent rings/queues and register them with `NativeMemorySentinel`, but they still require boot lifetime and no-growth proof.

Verdict: `YELLOW_CORE_LAZY_FIRST_USE_NATIVE_INIT_PROOF_REQUIRED`.

### Job Fence Helpers

- `CoreLowLevelUtilities.TryComplete(ref JobHandle, bool forceComplete)` returns false when the handle is incomplete and `forceComplete` is false. It only blocks when the caller has already observed completion or explicitly forces.
- `TryFinalizeCompleted(...)` completes only when `handle.IsCompleted` is already true.
- `H8Memory.CompleteAllOwnerJobs()` routes through shutdown and carries an explicit blocking sync point comment. The helper is not a defect by itself; every forced callsite still needs owner-window proof.

Verdict: `GREENISH_JOB_FENCE_HELPERS_WITH_FORCE_CALLSITE_PROOF_REQUIRED`.

## Release Blocker Updates

- RB-113 remains open but is now more precise: `H8Debug` facade calls are release-stripped, while direct `Debug.Log*` in bootstrap/global systems need fatal/boot/dev proof or migration to black-box/diagnostic lanes.
- New RB-129 tracks lazy first-use native initialization. Fixed native buffers are good only after they exist; release boot must prewarm `GlobalTelemetryBus`, `UIStateStore`, `ThreadSafeCommandQueue`, relevant `SignalBus<T>` lanes, `FrameTimeWatchdog`, and any `BurstCallbackQueue` owners before gameplay can publish/consume them.
- RB-008 remains relevant for dynamic bootstrap recovery roots, including `PlayerSensoryManager.EnsureRuntimeInstance()` and bootstrap presentation scene objects.

## Required Proof Packet

- Boot trace proving `GameBootstrapper.Update()` becomes inert after `_isBootstrapComplete` and never acts as a private gameplay scheduler.
- Player runtime rebind stress with counts for `SyncPlayerContextColdInternal`, `CachePlayerHierarchyReferencesCold`, `SyncSensoryContextCold`, and `GetComponentsInChildren` calls. Healthy gameplay should show zero steady-state hierarchy scans.
- Release build-symbol proof that `H8Debug` facade calls vanish and editor/dev validation/logging is excluded where claimed.
- Direct `Debug.Log*` audit proving each remaining direct call is fatal/boot/dev only and mirrored into black-box telemetry where it matters.
- Boot prewarm proof for telemetry/UI state/command queue/signal lanes/frame watchdog/burst callback queues, followed by 300-frame proof that no first-use native initialization happens during gameplay or UI interaction.
- Scene transition stress proving transition overlay material/UI roots are fixed-count, assigned-shader, and released/reused without repeated leaks.

## Non-Closure

This pass reduces false positives but does not close full runtime triage. No Unity import, Console, Play Mode, Profiler, GCMonitor, Frame Debugger, Memory Profiler, player build, or hardware device proof was run.

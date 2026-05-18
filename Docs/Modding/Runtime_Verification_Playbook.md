# HECTON-8 Mod API Runtime Verification Playbook

Date: 2026-05-17
Status: RUNTIME PLAYBOOK / NOT EXECUTED IN THIS PASS  

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner prompt: MODDING_API_SCHEMA_BUILDER  
Companion files:

- `Docs/Modding/Signal_Schema.json`
- `Docs/Modding/Signal_Audit_Matrix.md`
- `Docs/Modding/Command_Audit_Matrix.md`
- `Docs/Modding/API_Surface_Audit_Matrix.md`
- `Docs/Modding/Payload_Layout_Audit_Matrix.md`
- `Docs/Modding/Loader_Save_Audit_Matrix.md`
- `Docs/Modding/Event_Subscription_Audit_Matrix.md`
- `Docs/Modding/Resource_Content_Audit_Matrix.md`
- `Docs/Modding/Change_Control_Checklist.md`
- `Docs/Modding/Sample_InfiniteO2_Mod.md`
- `Docs/Modding/Validate_Mod_API_Static.ps1`
- `Docs/Modding/Mod_API_Specification.md`

## Purpose

This playbook defines the exact runtime evidence required before the mod API status can move from `PENDING RUNTIME VERIFICATION` to `VERIFIED`. It does not grant new API rights. It verifies that the static contract survives Unity runtime execution.

## Entry Points Under Test

Source-backed hooks:

- `IHectonMod.OnLoad`
- `IHectonMod.OnInitialize`
- `IHectonMod.OnUnload`
- `HectonAPI.Events.SubscribeProjected`
- `HectonAPI.Events.SubscribeNative`
- `HectonAPI.Events.Subscribe<TPayload>`
- `HectonAPI.Events.OnPlayerSpawned`
- `HectonAPI.Events.OnBiomeChanged`
- `HectonAPI.Commands.Request`
- `HectonAPI.Commands.RequestAup`
- `HectonAPI.Commands.RequestRenderInstance`
- `HectonAPI.SaveState.SetModString`
- `HectonAPI.SaveState.GetModString`
- `ModEventProjectionBridge.ProjectPostSimulation`
- `ModEventProjectionBridge.LateFrameTick`
- `ModCommandDispatcher.DrainPreSimulation`
- `ModCommandDispatcher.DrainLateFrame`

Source-backed limits:

- Current mod API version: 2.
- Projected event cap: 10 low tier / 50 high tier.
- Command drain cap: 256 per late frame.
- Per-mod command cap: 128 per tick.
- Raycast result cap: 128.
- Render instance cap: 1024 per frame.
- Mod heap quota: 16 MB total / 1 MB per frame.
- Voxel modification radius cap: 8 meters.
- Manifest file: `mod.json`.
- Manifest field count: 9.
- `ModMetadata` field count: 8.
- `ModRuntimeInfo` field count: 7.
- `IHectonMod` lifecycle method count: 3.
- Mod save payload cap: 16352 bytes.
- Public event method count: 7.
- Native event kind count: 2.
- Projected event kind count including `None`: 3.
- Native queue bridge publish lanes: 2.
- Dispatch recursion depth cap: 5.
- Event callback watchdog: 2.0 ms.

## Required Test Mod

Create a temporary test mod implementing `IHectonVersionedMod`.

Required behavior:

1. `RequiredAPIVersion` returns `2`.
2. `OnLoad` subscribes to:
   - `SubscribeProjected`
   - `SubscribeNative`
   - `Subscribe<ModRaycastResultPayload>`
   - `Subscribe<ModInteractionRejectedPayload>`
   - `Subscribe<ModCriticalMemoryEvictionPayload>`
   - `Subscribe<ModAupResponse>`
   - `OnPlayerSpawned`
   - `OnBiomeChanged`
3. `OnInitialize` records `HectonAPI.World.IsGameReady` and `TryGetPlayerEntityHash`.
4. `OnInitialize` writes and reads one namespaced `HectonAPI.SaveState` key.
5. `OnUnload` disposes every `HectonEventSubscription` and clears mod-owned counters.
6. No callback may allocate known managed collections, use reflection, call `GameObject.Find`, or store `ReadOnlySpan<byte>` from `SubscribeNative`.

Test mod counters must be primitive fields only:

```text
ProjectedCombatDamageCount
ProjectedWeatherChangedCount
NativeInteractionCount
NativeCraftingCount
SaveRoundTripCount
RaycastResultCount
RejectedCommandCount
MemoryEvictionCount
AupResponseCount
PlayerSpawnedCount
BiomeChangedCount
UnloadDisposedCount
UnexpectedEventHashCount
```

## Runtime Steps

### Step 1 - Static Gate

Run before Unity:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```

Required result:

- `Status = PASS`
- `SourceSignals = 160`
- `AllowedProjectedSignals = 2`
- `DeniedByDefaultSignals = 158`
- `ProjectionBridgeSignals = CombatDamageSignal,WeatherChangedSignal`
- `AcceptedCommandOpcodes = 8`
- `CommandRejectReasons = 19`
- `PublicApiSurfaces = 16`
- `PublicApiMethods = 34`
- `PublicApiProperties = 2`
- `ModEventDtoSizeBytes = 64`
- `ModEventDtoFieldOffsets = 15`
- `ModCommandSizeBytes = 64`
- `ModAupResponseSizeBytes = 64`
- `CurrentApiVersion = 2`
- `ManifestFieldCount = 9`
- `ModMetadataFieldCount = 8`
- `ModRuntimeInfoFieldCount = 7`
- `LifecycleMethodCount = 3`
- `SaveStatePublicMethods = 2`
- `ModPayloadMaxBytes = 16352`
- `PublicEventMethodCount = 7`
- `NativeEventKindCount = 2`
- `ProjectedEventKindCountIncludingNone = 3`
- `NativeQueueBridgePublishLaneCount = 2`
- `MaxEventDispatchDepth = 5`
- `CallbackWatchdogMilliseconds = 2`
- `ChangeControlChecklistPath = Docs/Modding/Change_Control_Checklist.md`
- `PublicResourceMethodCount = 3`
- `ResourceKindCount = 3`
- `ResourceRegistryCapacity = 256`
- `PublicContentMethodCount = 14`
- `RawTextureMaxBytes = 8388608`
- `RawTextureMaxDimension = 2048`

### Step 2 - Unity Load Gate

Start Unity and load the normal scene flow:

```text
00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD
```

Required evidence:

- Unity Console has no compile errors.
- Test mod is discovered and loaded.
- Test mod manifest is named `mod.json` and declares `RequiredAPIVersion = 2`.
- `OnLoad` executes once.
- `OnInitialize` executes once after gameplay bootstrap.
- `RequiredAPIVersion = 2` is accepted.
- A test manifest with `RequiredAPIVersion = 3` is disabled without executing callbacks.
- No direct Unity object reference is exposed to the mod.

### Step 3 - Projection Gate

Force or simulate one source `CombatDamageSignal` and one source `WeatherChangedSignal` through first-party owners, then wait for `ProjectPostSimulation` and `LateFrameTick`.

Required evidence:

- Test mod receives `ModEventDto.EventHash = 0x43444D47` for combat damage.
- Test mod receives `ModEventDto.EventHash = 0x57454154` for weather change.
- `UnexpectedEventHashCount = 0`.
- No other `GlobalSignals.cs` `ISignal` type reaches `SubscribeProjected`.
- Low tier sets `ModEventDto.LowTierSampleFlag` when capped.
- High tier does not exceed 50 projected events per frame.

### Step 4 - Native Byte Event Gate

Trigger one interaction event and one crafting event through first-party systems.

Required evidence:

- `SubscribeNative` receives `HectonNativeEventKind.Interaction`.
- `SubscribeNative` receives `HectonNativeEventKind.Crafting`.
- The callback does not store the `ReadOnlySpan<byte>`.
- Native event payloads are immutable copies only; no `NativeQueue` or `NativeArray` handle is exposed.
- Only `Interaction` and `Crafting` native event kinds are observed.

### Step 5 - Command Result Gate

Submit the following from `OnInitialize` or a cold test command path:

- One valid `RequestAup` raycast.
- One invalid `RequestAup` raycast with invalid target/opcode.
- One valid `RequestRenderInstance` under the frame cap.
- One invalid render-instance burst exceeding 1024 in one frame.

Required evidence:

- Valid raycast returns `ModRaycastResultPayload` with status hit or miss.
- Invalid command returns `ModInteractionRejectedPayload`.
- Render overflow returns rejection reason `RenderCapacityExceeded`.
- No direct spawn/despawn/GameObject reference is exposed.

### Step 6 - Quota Gate

Submit 129 commands from the same mod in one tick.

Required evidence:

- At least one rejection reason is `CommandFlood`.
- The engine remains responsive.
- No unbounded queue growth is observed.
- Command drain remains capped at 256 per late frame.

### Step 7 - Memory Eviction Gate

Use a controlled test mod callback to exceed the 1 MB frame quota or 16 MB tracked quota.

Required evidence:

- `ModCriticalMemoryEvictionPayload` is published before disable.
- `ModLoader.DisableManagedMod` disables the offending mod.
- Other mod subscriptions continue to dispatch.
- No first-party system crashes.

### Step 8 - SaveState Gate

Round-trip one namespaced mod save key through `HectonAPI.SaveState` from an active callback scope.

Required evidence:

- `SetModString` then `GetModString` returns the same payload.
- Stored payload is mod-owned text, not first-party save truth.
- Payloads larger than 16352 bytes are rejected from the MMF mod payload commit path.
- Calling SaveState outside an active `ModExecutionScope` throws `IllegalContractException`.

### Step 9 - Teardown Gate

Unload or disable the test mod.

Required evidence:

- `OnUnload` executes once.
- Every `HectonEventSubscription` is disposed.
- No callback from the unloaded mod fires on the next projected/native/unmanaged event.
- No command from the unloaded mod is accepted.
- `HectonEventSubscription.IsActive` is false after disposal.

### Step 10 - GC/Profiler Gate

Capture runtime metrics during the projection and quota tests.

Required evidence format:

```text
BEFORE: <measured KB/frame>
AFTER: <measured KB/frame>
STATUS: 0 B hot-path projection dispatch / PENDING if nonzero
Profiler markers: ModEventProjectionBridge.ProjectPostSimulation, ModEventProjectionBridge.LateFrameTick, ModCommandDispatcher.DrainPreSimulation, ModCommandDispatcher.DrainLateFrame
```

Required result:

- Hot-path projection dispatch: 0 B/frame.
- No mod callback path exceeds the documented watchdog.
- No single mod API system exceeds 0.1 ms without tier gate and written justification.

## Failure Handling

Any failure keeps status at `PENDING RUNTIME VERIFICATION`.

Required failure report:

```text
Failed Step:
Expected:
Observed:
Unity Console errors:
GCMonitor:
Profiler marker:
Suspected owner:
Rollback needed: yes/no
```

## Pass Criteria

The mod API can be marked `VERIFIED` only when all conditions below are true:

- Static validator passes.
- Unity Console is clean after loading the test mod.
- Only `CombatDamageSignal` and `WeatherChangedSignal` reach `SubscribeProjected`.
- Native byte events expose no native container handles.
- Command rejection and quota payloads are delivered.
- Loader/save contracts match `Loader_Save_Audit_Matrix.md`.
- Event subscription contracts match `Event_Subscription_Audit_Matrix.md`.
- Resource/content contracts match `Resource_Content_Audit_Matrix.md`.
- Change control requirements match `Change_Control_Checklist.md`.
- SaveState stores only mod-owned text under the scoped mod boundary.
- `OnUnload` prevents later callbacks.
- GC hot-path projection dispatch is 0 B/frame.
- Profiler captures show no unbounded callback fanout.

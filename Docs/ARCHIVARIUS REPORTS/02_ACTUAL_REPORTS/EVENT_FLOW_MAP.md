# HECTON-8 EVENT FLOW MAP

Date: 2026-05-01
Status: PENDING VERIFICATION
Scope: source-backed event topology visible in first-party code; no profiler or play-mode proof in this document.

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## 1. Audit Standard

This file records event flows directly rechecked in source.

Evidence basis:
- direct reads of bus definitions
- direct scan of `SystemDispatcher.LateUpdate()`
- direct scans for `NativeQueue<TPayload>` ownership
- direct scans for `Raise*`, `Register`, `Unregister`, and `FlushPending()` paths
- direct scan for remaining static `Action` surfaces in the migrated global buses

No runtime replay, scene wiring proof, or GCMonitor capture is claimed here.

## 2. Dispatcher Flush Topology

`SystemDispatcher.LateUpdate()` is the current deferred-event drain owner.

Flush order visible in source:
1. `ThreadSafeCommandQueue.DrainMainThread()`
2. `NarrativeEvents.FlushPending()`
3. `InteractionEvents.FlushPending()`
4. `CraftingEvents.FlushPending()`
5. `ScanEvents.FlushPending()`
6. `SaveEvents.FlushPending()`
7. `QuestEvents.FlushPending()`
8. `AudioLogEvents.FlushPending()`
9. `HectonSubmarineOsEvents.FlushPending()`
10. `FlashlightEvents.FlushPending()`
11. `WeatherEvents.FlushPending()`
12. `ModuleStatusEvents.FlushPending()`
13. `DepthZoneEvents.FlushPending()`
14. `SoundscapeEvents.FlushPending()`
15. `EmergencyServiceRelayEvents.FlushPending()`
16. `SargassumGlobalDragManager.FlushPendingEvents()`
17. `AtlasSignalEvents.FlushPending()`
18. `NotificationEvents.FlushPending()`
19. `PDAEvents.FlushPending()`
20. `SceneBootstrap.FlushPendingEvents()`
21. `ObjectPoolDiagnostics.FlushPending()`
22. `Atlas6Events.FlushPending()`
23. `GlobalRegistry.FlushPendingServiceReboundEvents()`
24. `GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime)`

`SystemDispatcher.TryConsumeLateFrameEventDispatch()` enforces the shared late-frame event budget. If the budget is exhausted, queues retain remaining events for a later frame instead of draining unbounded work.

## 3. Queue-Backed Buses

Confirmed queue-backed deferred buses:

| Bus | Payload | Notes |
| --- | --- | --- |
| `SaveEvents` | `SaveEventPayload` | NativeQueue; fixed-string slot/message fields. |
| `QuestEvents` | `QuestEventPayload` | NativeQueue; quest hash + event type. |
| `ScanEvents` | `ScanEventPayload` | NativeQueue; hash payload plus cold metadata table. |
| `NarrativeEvents` | `NarrativeEventPayload` | NativeQueue for discovery/depth lane; POI callback lane remains separate. |
| `AudioLogEvents` | `AudioLogEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for dispatch-time `AudioLogData` resolution. |
| `HectonSubmarineOsEvents` | `SubmarineOsEventPayload` | NativeQueue; sequential payload with module hash, emergency level, and status bits. |
| `FlashlightEvents` | `FlashlightEventPayload` | NativeQueue; flashlight state bits plus battery/heat scalar fields. |
| `WeatherEvents` | `WeatherEventPayload` | NativeQueue; weather state mask, wind/current vectors, and current metadata. |
| `ModuleStatusEvents` | `ModuleStatusEventPayload` | NativeQueue; module entity/hash payload plus bounded managed sidecar for dispatch-time `BaseModule` resolution. |
| `DepthZoneEvents` | `DepthZoneEventPayload` | NativeQueue; zone hash + enter/exit event type; managed profile lookup remains outside the queued payload. |
| `SoundscapeEvents` | `SoundscapeEventPayload` | NativeQueue; old/new soundscape tier enums. |
| `EmergencyServiceRelayEvents` | `RelayEventPayload` | NativeQueue; relay entity id + first-activation flag; managed relay lookup remains outside the queued payload. |
| `SargassumGlobalDragManager` | `EntanglementStrainSignal` / `MassiveDisplacementSignal` | Two NativeQueues drained by dispatcher for sargassum strain and displacement signals. |
| `GlobalRegistry` rebound lane | `RegistryEventPayload` | NativeQueue; service slot + object identity hashes; managed service refs stay in fixed sidecar slots. |
| `InteractionEvents` | `InteractionEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for first-party reference resolution during dispatch. |
| `CraftingEvents` | `CraftingEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for first-party reference resolution during dispatch. |
| `AtlasSignalEvents` | `AtlasSignalEventPayload` | NativeQueue; hash payload plus cold decoded-message table. |
| `NotificationEvents` | `NotificationEventPayload` | NativeQueue; message hash plus cold message table. |
| `ObjectPoolDiagnostics` | `PoolDiagnosticsEventPayload` | NativeQueue; pool hash + metric payload. |

## 4. Interaction Lane

Definition: `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`

Current shape:
- `NativeQueue<InteractionEventPayload>`
- `RegistryBucket<IInteractionEventListener>`
- `InteractionEventPayload` is blittable
- payload carries `uint` hash IDs:
  - `ItemHashId`
  - `TargetHashId`
  - `InteractorHashId`
- managed references are not stored in the payload
- first-party managed reference resolution uses a fixed `InteractionReferenceSlot[128]` sidecar
- sidecar occupancy is tracked by `bool[128]` to prevent wrap overwrite before deferred flush

There are no public static `Action` events in this file.

## 5. Crafting Lane

Definition: `Assets/_Project/Scripts/CraftingEvents.cs`

Current shape:
- `NativeQueue<CraftingEventPayload>`
- `RegistryBucket<ICraftingEventListener>`
- `CraftingEventPayload` is blittable
- payload carries `uint` hash IDs:
  - `FabricatorHashId`
  - `RecipeHashId`
  - `ResultItemHashId`
- managed references are not stored in the payload
- first-party managed reference resolution uses a fixed `CraftingReferenceSlot[128]` sidecar
- sidecar occupancy is tracked by `bool[128]` to prevent wrap overwrite before deferred flush
- progress/closed/cancelled events do not reserve sidecar slots

There are no public static `Action` events in this file.

## 6. Modding Boundary

Definition: `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`

Current shape:
- `ModLoader.InstallHooks()` installs the native queue bridge.
- `HectonEventBus.InstallNativeQueueBindings()` registers one internal bridge listener to `InteractionEvents` and `CraftingEvents`.
- Mods receive immutable `ReadOnlySpan<byte>` payload copies through `SubscribeNative(...)`.
- Mods never receive `NativeQueue`, `NativeArray`, sidecar slot arrays, or mutable first-party references.
- Subscriber exceptions isolate and disable the offending mod through `ModLoader.DisableManagedMod(...)`.

This bridge is a projection layer. It is not the owner of first-party event queues.

## 7. Listener Lifecycle

Confirmed listener registration pattern for the migrated lanes:
- `FirstHourDirector`: registers `CraftingEvents` and `InteractionEvents` in `OnEnable`; unregisters both in `OnDisable`.
- `HectonFabricatorUI`: registers `CraftingEvents` in `OnEnable`; unregisters in `OnDisable`.
- `InteractionUI`: registers `InteractionEvents` in `OnEnable`; unregisters in `OnDisable`.
- `DiegeticTooltipSystem`: registers `InteractionEvents` in `OnEnable`; unregisters in `OnDisable`.
- `CameraJuiceSystem`: registers `InteractionEvents` in `OnEnable`; unregisters in `OnDisable`.
- `TraumaDispatcher`: registers `ModuleStatusEvents` in `OnEnable`; unregisters in `OnDisable`.
- `PlayerToolManager`: registers `ModuleStatusEvents` in `OnEnable`; unregisters in `OnDisable`.
- `HectonEventBus` bridge: installed/uninstalled by `ModLoader` hook lifecycle.

Scene/prefab UnityEvent bindings are outside this source-only scan.

## 8. Registry And Environmental Lanes

Definitions:
- `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`
- `Assets/_Project/Scripts/PlayerFlashlight.cs`
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs`
- `Assets/_Project/Scripts/ModuleStatusEvents.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`

Current shape:
- direct static multicast delegates were removed from `HectonSubmarineOsEvents` and `FlashlightEvents`
- weather snapshots are published through `WeatherEvents` only when state/vector/intensity crosses the director epsilon checks
- `ModuleStatusEvents` publishes enter/exit through `NativeQueue<ModuleStatusEventPayload>` and uses `IModuleStatusEventListener`
- `GlobalRegistry` service rebounds enqueue `RegistryEventPayload` and drain in `SystemDispatcher.LateUpdate()`
- registry sidecar references are dispatch-scoped; the native payload remains unmanaged
- `SystemDispatcher` records the active lane hash when the late-frame circuit breaker trips and publishes a `PerformanceWarning` telemetry event with the dominant offender hash

## 9. Remaining Drift

Known remaining direct/static event surfaces still need separate audits:
- feature-local celestial/weather/direct callback surfaces
- any UI-only UnityEvent inspector binding not visible in class scans

This document does not certify those surfaces.

## 10. Regression Model

CPU: event drains remain bounded by the dispatcher late-frame budget; sidecar occupancy scans are capped at 128 probes on publish only.

GC: queued payloads are blittable structs; listener dispatch uses preallocated `RegistryBucket` arrays; no queued payload strings were added.

Memory: two fixed `bool[128]` occupancy arrays were added for sidecar safety; no unbounded event cache was introduced.

Cadence: event flush cadence remains `SystemDispatcher.LateUpdate()`.

Correctness: stale claims that `InteractionEvents` and `CraftingEvents` are direct static `Action` buses were removed.

## 11. Hot Path Impact

No per-frame allocation is introduced.

The changed work is in event publish and late-frame drain paths:
- publish: bounded native enqueue plus fixed sidecar reservation when a first-party reference is required
- drain: reverse `for` over listener registry, then sidecar release

## 12. Failure Modes

- If more than 128 unresolved sidecar-backed interaction or crafting events are queued before flush, `TryReserveReferenceSlot` returns false and that sidecar-backed event is dropped.
- If late-frame budget is exhausted, remaining queue entries stay pending for a later frame.
- Hash fields expose stable payload data to mods, but first-party listeners that need Unity references still depend on dispatch-time sidecar resolution.
- Runtime GC and listener leak proof still require MCP/Profiler validation.

## 13. Why This Version Was Kept

Kept because it matches current source after the interaction/crafting queue migration and sidecar hardening.

STATUS: PENDING VERIFICATION

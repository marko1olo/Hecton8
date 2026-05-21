# HECTON-8 EVENT FLOW MAP

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Scope: source-backed event topology visible in first-party code; no profiler or play-mode proof in this document.

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`

## 1. Audit Standard

This file records event flows directly rechecked in source.

Evidence basis:
- direct reads of bus definitions
- direct scan of `SystemDispatcher.LateUpdate()`
- direct scans for `NativeQueue<TPayload>` ownership
- direct scans for `Raise*`, `Register`, `Unregister`, and `FlushPending()` paths
- direct scan for remaining static `Action` surfaces in the migrated global buses
- direct scan for `DontDestroyOnLoad(` runtime ownership boundaries

No runtime replay, scene wiring proof, or GCMonitor capture is claimed here.

## 2. Dispatcher Flush Topology

`SystemDispatcher.LateUpdate()` is the current deferred-event drain owner.

## 2.1 Typed Signal-Lane Orientation

The old five-bucket bus grouping is legacy shorthand only. Current ownership uses the 9-echelon / 85-domain map plus typed `SignalBus<T>` and `NativeQueue` lanes; Core/Env/Player/Base/AI are local reading buckets, not complete architecture coverage or exclusive cross-domain authority.

| Legacy bucket | Scope | Representative source queues |
| --- | --- | --- |
| Core | bootstrap, save/load, registry, telemetry, scene bootstrap, localization, mod registry, object-pool diagnostics, performance warnings | `BootstrapEvents`, `SaveEvents`, `LocalizationEvents`, `ModRegistryEvents`, `GlobalRegistry`, `ObjectPoolDiagnostics`, `PerformanceEvents`, `GlobalTelemetryBus` |
| Env | atmosphere, weather, ocean/acoustics, celestial, physics signals, fluid feedback, biome/depth/soundscape changes | `AtmosphereEvents`, `WeatherEvents`, `CelestialEvents`, `PhysicsEventBus`, `FluidFeedbackEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `DepthZoneEvents`, `SoundscapeEvents` |
| Player | interaction, crafting, scanner, PDA, flashlight, laser cutter, inventory, player signals, player expression, notifications | `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `PDAEvents`, `FlashlightEvents`, `LaserCutterEvents`, `InventoryEvents`, `PlayerSignalEvents`, `PlayerExpressionEvents`, `NotificationEvents` |
| Base | submarine OS, modules, base integrity, power telemetry, emergency service relays, pressure alarm lanes | `HectonSubmarineOsEvents`, `ModuleStatusEvents`, `BaseIntegrityEvents`, `PowerGridTelemetryEvents`, `EmergencyServiceRelayEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents` |
| AI | director, drone fleet, random events, Atlas signal/directive, narrative/audio-log discovery pressure | `DirectorAIEvents`, `HectonDroneFleetEvents`, `RandomEventEvents`, `AtlasSignalEvents`, `Atlas6Events`, `NarrativeEvents`, `AudioLogEvents` |

This orientation is a local reading aid, not a new runtime dispatcher or complete cross-domain authority map. `SystemDispatcher.LateUpdate()` remains the actual drain point and enforces the shared late-frame budget.

Cross-domain authority rule:
- Core, Env, Player, Base, and AI are the only documented first-party cross-domain arteries.
- Direct `Action`, `Func`, `delegate`, C# `event`, or `UnityEvent` chains are legacy debt when they cross domain boundaries.
- Local owner callbacks, async completion callbacks, and inspector-only UI bindings are not promoted into event-flow authority by this map.
- `HectonEventBus` remains the managed modding boundary; it is not a replacement for queue-backed first-party lanes.

Strict shedding budget: the five arteries share a hard `2.0ms` late-frame event dispatch ceiling. If the Core, Env, Player, Base, or AI artery cannot drain inside that budget, remaining queued payloads are retained for a later frame; unbounded drain is forbidden.

Flush order visible in source:
1. `ThreadSafeCommandQueue.DrainMainThread()`
2. `ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents()`
3. `ModCommandDispatcher.DrainLateFrame()`
4. `ModRegistryEvents.FlushPending()`
5. `BootstrapEvents.FlushPending()`
6. `LocalizationEvents.FlushPending()`
7. `NarrativeEvents.FlushPending()`
8. `InteractionEvents.FlushPending()`
9. `CraftingEvents.FlushPending()`
10. `ScanEvents.FlushPending()`
11. `SaveEvents.FlushPending()`
12. `QuestEvents.FlushPending()`
13. `FirstHourEvents.FlushPending()`
14. `EndingEvents.FlushPending()`
15. `AudioLogEvents.FlushPending()`
16. `AtmosphereEvents.FlushPending()`
17. `HighPressureEvents.FlushPending()`
18. `FatalPressureImplosionEvents.FlushPending()`
19. `CelestialEvents.FlushPending()`
20. `EclipseGameplayEvents.FlushPending()`
21. `AcousticZoneEvents.FlushPending()`
22. `PhysicsEventBus.FlushPending()`
23. `FluidFeedbackEvents.FlushPending()`
24. `RepairDroneTorchAcousticEvents.FlushPending()`
25. `ElectrolysisAcousticEvents.FlushPending()`
26. `AudioCaptionEvents.FlushPending()`
27. `SpectrumEvents.FlushPending()`
28. `ProceduralAudioEvents.FlushPending()`
29. `HectonSubmarineOsEvents.FlushPending()`
30. `FlashlightEvents.FlushPending()`
31. `LaserCutterEvents.FlushPending()`
32. `PlayerSignalEvents.FlushPending()`
33. `MapMagicBiomeEvents.FlushPending()`
34. `BiomeMatrixEvents.FlushPending()`
35. `DirectorAIEvents.FlushPending()`
36. `HectonDroneFleetEvents.FlushPending()`
37. `WeatherEvents.FlushPending()`
38. `RandomEventEvents.FlushPending()`
39. `PowerGridTelemetryEvents.FlushPending()`
40. `ModuleStatusEvents.FlushPending()`
41. `DepthZoneEvents.FlushPending()`
42. `SoundscapeEvents.FlushPending()`
43. `EmergencyServiceRelayEvents.FlushPending()`
44. `SargassumGlobalDragManager.FlushPendingEvents()`
45. `AtlasSignalEvents.FlushPending()`
46. `InventoryEvents.FlushPending()`
47. `PlayerExpressionEvents.FlushPending()`
48. `BaseIntegrityEvents.FlushPending()`
49. `NotificationEvents.FlushPending()`
50. `PDAIntrusionEvents.FlushPending()`
51. `PDAEvents.FlushPending()`
52. `SceneBootstrap.FlushPendingEvents()`
53. `ObjectPoolDiagnostics.FlushPending()`
54. `PerformanceEvents.FlushPending()`
55. `Atlas6Events.FlushPending()`
56. `GlobalRegistry.FlushPendingServiceReboundEvents()`
57. `GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime)`

`SystemDispatcher.TryConsumeLateFrameEventDispatch()` enforces the shared late-frame event budget. If the budget is exhausted, queues retain remaining events for a later frame instead of draining unbounded work.

## 3. Queue-Backed Buses

Confirmed queue-backed deferred buses:

| Bus | Payload | Notes |
| --- | --- | --- |
| `ThreadSafeCommandQueue` storage ack lane | `StorageReservationCommitResolvedPayload` | NativeQueue; storage reservation commit acknowledgements replacing direct command-queue static callback. |
| `ModRegistryEvents` | `ModRegistryEventPayload` | NativeQueue; mod runtime/settings/recipe/buildable registry invalidation lane replacing direct modding static callbacks; same-type invalidations coalesce while pending. |
| `BootstrapEvents` | `BootstrapEventPayload` | NativeQueue; bootstrap completion lane replacing legacy direct `OnBootstrapComplete` callback. |
| `LocalizationEvents` | `LocalizationEventPayload` | NativeQueue; language-change and corruption visual refresh lane replacing `LocalizationManager` direct static callbacks. |
| `SaveEvents` | `SaveEventPayload` | NativeQueue; fixed-string slot/message fields. |
| `QuestEvents` | `QuestEventPayload` | NativeQueue; quest hash + event type. |
| `FirstHourEvents` | `FirstHourEventPayload` | NativeQueue; first-hour milestone lane. |
| `EndingEvents` | `EndingEventPayload` | NativeQueue; ending-state lane. |
| `ScanEvents` | `ScanEventPayload` | NativeQueue; hash payload plus cold metadata table. |
| `NarrativeEvents` | `NarrativeEventPayload` | NativeQueue for discovery/depth lane; POI callback lane remains separate. |
| `AudioLogEvents` | `AudioLogEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for dispatch-time `AudioLogData` resolution. |
| `AtmosphereEvents` | `EnvironmentState` | NativeQueue; atmosphere snapshot lane. |
| `HighPressureEvents` | `HighPressureEventPayload` | NativeQueue; submarine high-pressure warning lane replacing direct atmosphere static callbacks. |
| `FatalPressureImplosionEvents` | `FatalPressureImplosionEventPayload` | NativeQueue; fatal implosion lane replacing direct atmosphere static callbacks. |
| `CelestialEvents` | `CelestialEventPayload` | NativeQueue; celestial event lane. |
| `EclipseGameplayEvents` | `EclipseGameplayEventPayload` | NativeQueue; eclipse gameplay state lane. |
| `AcousticZoneEvents` | `AcousticZoneChangedEvent` | NativeQueue; acoustic-zone transition lane. |
| `PhysicsEventBus` | `PhysicsEventPayload` | NativeQueue; pressure impulse, EMP, and acoustic ping lane replacing direct physics static callbacks. |
| `FluidFeedbackEvents` | `SplashEvent` | NativeQueue; fluid splash presentation lane replacing private static splash delegate callback. |
| `RepairDroneTorchAcousticEvents` | `RepairDroneTorchAcousticPayload` | NativeQueue; repair drone torch audio pulse lane with fixed `AudioClip[32]` sidecar. |
| `ElectrolysisAcousticEvents` | `ElectrolysisAcousticPayload` | NativeQueue; electrolysis acoustic pulse lane replacing unused public static acoustic delegate. |
| `AudioCaptionEvents` | `AudioCaptionPayload` | NativeQueue; spatial audio caption lane with fixed `string[32]` sidecar. |
| `SpectrumEvents` | sonar/spectrum payloads | NativeQueue-backed sonar/spectrum lane. |
| `ProceduralAudioEvents` | `AudioPingTriggerInfo` / `StructuralStressAudioInfo` | Two NativeQueues for procedural audio triggers. |
| `HectonSubmarineOsEvents` | `SubmarineOsEventPayload` | NativeQueue; sequential payload with module hash, emergency level, and status bits. |
| `FlashlightEvents` | `FlashlightEventPayload` | NativeQueue; flashlight state bits plus battery/heat scalar fields. |
| `LaserCutterEvents` | `LaserCutterEventPayload` | NativeQueue; laser cutter deferred gameplay lane. |
| `PlayerSignalEvents` | `TraumaHudSignal` / `InteractionSignal` / `ToolDepletedSignal` | NativeQueues for player-facing deferred signal fanout. |
| `MapMagicBiomeEvents` | `int` biome id | NativeQueue; MapMagic biome transition lane. |
| `BiomeMatrixEvents` | `BiomeMatrixEventPayload` | NativeQueue; biome matrix transition lane. |
| `DirectorAIEvents` | `DirectorAIEventPayload` | NativeQueue; director AI event lane. |
| `HectonDroneFleetEvents` | `HectonDroneFleetSnapshotPayload` | NativeQueue; drone fleet snapshot lane replacing direct snapshot static delegate. |
| `WeatherEvents` | `WeatherEventPayload` | NativeQueue; weather state mask, wind/current vectors, and current metadata. |
| `RandomEventEvents` | random-event payloads | NativeQueues for random event starts, ends, and seismic shockwaves. |
| `PowerGridTelemetryEvents` | power telemetry payloads | NativeQueue-backed power telemetry lane. |
| `ModuleStatusEvents` | `ModuleStatusEventPayload` | NativeQueue; module entity/hash payload plus bounded managed sidecar for dispatch-time `BaseModule` resolution. |
| `DepthZoneEvents` | `DepthZoneEventPayload` | NativeQueue; zone hash + enter/exit event type; managed profile lookup remains outside the queued payload. |
| `SoundscapeEvents` | `SoundscapeEventPayload` | NativeQueue; old/new soundscape tier enums. |
| `EmergencyServiceRelayEvents` | `RelayEventPayload` | NativeQueue; relay entity id + first-activation flag; managed relay lookup remains outside the queued payload. |
| `SargassumGlobalDragManager` | `EntanglementStrainSignal` / `MassiveDisplacementSignal` | Two NativeQueues drained by dispatcher for sargassum strain and displacement signals. |
| `GlobalRegistry` rebound lane | `RegistryEventPayload` | NativeQueue; service slot + object identity hashes; managed service refs stay in fixed sidecar slots. |
| `InteractionEvents` | `InteractionEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for first-party reference resolution during dispatch. |
| `CraftingEvents` | `CraftingEventPayload` | NativeQueue; hash payload plus bounded managed sidecar for first-party reference resolution during dispatch. |
| `AtlasSignalEvents` | `AtlasSignalEventPayload` | NativeQueue; hash payload plus cold decoded-message table. |
| `InventoryEvents` | `InventoryEventPayload` | NativeQueue; inventory state mutation lane. |
| `PlayerExpressionEvents` | `PlayerExpressionEventPayload` | NativeQueue; player expression transition lane. |
| `BaseIntegrityEvents` | base integrity payloads | NativeQueue-backed UI integrity lane. |
| `NotificationEvents` | `NotificationEventPayload` | NativeQueue; message hash plus cold message table. |
| `PDAIntrusionEvents` | PDA intrusion payloads | NativeQueue-backed PDA intrusion lane. |
| `PDAEvents` | `PDAEventPayload` | NativeQueue; PDA open/close/tab payloads with per-frame dispatch cap. |
| `ObjectPoolDiagnostics` | `PoolDiagnosticsEventPayload` | NativeQueue; pool hash + metric payload. |
| `PerformanceEvents` | `PerformanceEventPayload` | NativeQueue; performance threshold lane. |
| `Atlas6Events` | `Atlas6EventPayload` | NativeQueue; Atlas-6 directive lane. |

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
- `ModMenuUIController`: registers `ModRegistryEvents` in `OnEnable`; unregisters in `OnDisable`.
- `Fabricator`: registers `ModRegistryEvents` in `OnEnable`; unregisters in `OnDisable`.
- `TraumaDispatcher`: registers `PhysicsEventBus` EMP listener in `OnEnable`; unregisters in `OnDisable`.
- `SpectrumSystem`: registers `PhysicsEventBus` acoustic ping listener when active; unregisters through the existing teardown hook.
- `FluidFeedbackListener`: registers `FluidFeedbackEvents` in `OnEnable`; unregisters in `OnDisable`.
- `SpatialAudioManager`: registers `RepairDroneTorchAcousticEvents` in service event subscription; unregisters through the same teardown hook.
- `AcousticEcholocationTranslator`: registers `AudioCaptionEvents` in `OnEnable`; unregisters in `OnDisable` and `OnDestroy`.
- `HectonSubmarineOS`: registers `HighPressureEvents` and `FatalPressureImplosionEvents` through its existing subscription path; unregisters through matching teardown.
- `SpatialAudioManager`: registers `FatalPressureImplosionEvents` in service event subscription; unregisters through the same teardown hook.
- `DroneFleetManager`: registers a static bridge object for `ThreadSafeCommandQueue` storage reservation acknowledgements and unregisters it during reset.
- `HectonSubmarineOS`: registers `HectonDroneFleetEvents` in its service subscription path; unregisters through matching teardown.
- Localization UI/gameplay listeners: register `LocalizationEvents` in `OnEnable`; unregister in `OnDisable` or existing teardown hook. Duplicate teardown calls are idempotent through `RegistryBucket.Contains`.
- `HectonEventBus` bridge: installed/uninstalled by `ModLoader` hook lifecycle.

Scene/prefab UnityEvent bindings are outside this source-only scan.

## 8. Registry And Environmental Lanes

Definitions:
- `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`
- `Assets/_Project/Scripts/PlayerFlashlight.cs`
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs`
- `Assets/_Project/Scripts/LocalizationEvents.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`
- `Assets/_Project/Scripts/ModuleStatusEvents.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`

Current shape:
- direct static multicast delegates were removed from `HectonSubmarineOsEvents` and `FlashlightEvents`
- weather snapshots are published through `WeatherEvents` only when state/vector/intensity crosses the director epsilon checks
- `PhysicsEventBus` publishes pressure impulse, EMP, and acoustic ping signals through `NativeQueue<PhysicsEventPayload>`; old direct static `OnPressureImpulse`, `OnElectromagneticPulse`, and `OnAcousticPing` callbacks are removed
- `FluidFeedbackEvents` publishes `SplashEvent` through `NativeQueue<SplashEvent>`; the old private static splash delegate is removed
- `RepairDroneTorchAcousticEvents` publishes torch audio pulses through `NativeQueue<RepairDroneTorchAcousticPayload>`; the managed `AudioClip` reference is sidecar-only and cleared after dispatch
- `ElectrolysisAcousticEvents` publishes electrolysis acoustic pulses through `NativeQueue<ElectrolysisAcousticPayload>`; no first-party listeners are currently registered in source
- `AudioCaptionEvents` publishes caption requests through `NativeQueue<AudioCaptionPayload>`; the cached caption string is sidecar-only and cleared after dispatch
- `HighPressureEvents` and `FatalPressureImplosionEvents` publish submarine pressure alarms through NativeQueues; payloads are unmanaged pressure/temperature/node fields only
- `ThreadSafeCommandQueue` storage reservation acknowledgements publish through `NativeQueue<StorageReservationCommitResolvedPayload>` after command drain; `DroneFleetManager` consumes through a static bridge object
- `HectonDroneFleetEvents` publishes changed fleet snapshots through `NativeQueue<HectonDroneFleetSnapshotPayload>`; the old `OnSnapshotUpdated` delegate is removed
- `ModuleStatusEvents` publishes enter/exit through `NativeQueue<ModuleStatusEventPayload>` and uses `IModuleStatusEventListener`
- `ModRegistryEvents` publishes runtime/settings/recipe/buildable invalidation through `NativeQueue<ModRegistryEventPayload>`, uses `IModRegistryEventListener`, and coalesces redundant same-type invalidations until flush
- `LocalizationEvents` publishes language-change and corruption visual refresh events through `NativeQueue<LocalizationEventPayload>` and uses interface listeners instead of `LocalizationManager` static delegates
- `GlobalRegistry` service rebounds enqueue `RegistryEventPayload` and drain in `SystemDispatcher.LateUpdate()`
- registry sidecar references are dispatch-scoped; the native payload remains unmanaged
- `SystemDispatcher` records the active lane hash when the late-frame circuit breaker trips and publishes a `PerformanceWarning` telemetry event with the dominant offender hash

## 9. Remaining Drift

Current `Assets/_Project/Scripts` C# scan has no `public static event`, `private static event`, or `static event` declarations.

Known remaining topology risks:
- instance-level `Action`/`event Action` surfaces still need owner-by-owner leak audits
- `DontDestroyOnLoad` call sites are textually restricted to `GameBootstrapper` and `CrashTelemetryBuffer`; bootstrap-owned services persist as children under the `GameBootstrapper` root through `GameBootstrapper.PersistRuntimeService(...)`
- UI-only UnityEvent inspector bindings are not visible in class scans

This document does not certify those surfaces.

## 10. Regression Model

CPU: event drains remain bounded by the dispatcher late-frame budget; sidecar occupancy scans are capped at 128 probes on publish only.

GC: queued payloads are blittable structs; listener dispatch uses preallocated `RegistryBucket` arrays; no queued payload strings were added.

Memory: two fixed `bool[128]` occupancy arrays were added for sidecar safety; no unbounded event cache was introduced.
Localization adds fixed listener buckets and one persistent native queue capped by software guard at 128 pending payloads.

Cadence: event flush cadence remains `SystemDispatcher.LateUpdate()`.

Correctness: stale claims that `InteractionEvents` and `CraftingEvents` are direct static `Action` buses were removed.

## 11. Hot Path Impact

No per-frame allocation is introduced.

The changed work is in event publish and late-frame drain paths:
- publish: bounded native enqueue plus fixed sidecar reservation when a first-party reference is required
- drain: reverse `for` over listener registry, then sidecar release

## 12. Failure Modes

- If more than 128 unresolved sidecar-backed interaction or crafting events are queued before flush, `TryReserveReferenceSlot` returns false and that sidecar-backed event is dropped.
- If more than 128 localization events are queued before flush, excess localization payloads are dropped and `GlobalTelemetryBus` receives a `PerformanceWarning`.
- If late-frame budget is exhausted, remaining queue entries stay pending for a later frame.
- Hash fields expose stable payload data to mods, but first-party listeners that need Unity references still depend on dispatch-time sidecar resolution.
- Runtime GC and listener leak proof still require MCP/Profiler validation.

## 13. Why This Version Was Kept

Kept because it matches current source after the interaction/crafting queue migration and sidecar hardening.

STATUS: PENDING VERIFICATION

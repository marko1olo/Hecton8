# Event Cascade Recheck

Date: `2026-05-01`
Status: `PENDING VERIFICATION`
Scope: static source recheck of event cascade/depth guard claims

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Verification Boundary

This is source evidence only. No Play Mode event-loop stress test was run.
No GCMonitor, profiler, or runtime telemetry capture was collected.

MCP was not used. Local `Editor.log` remained clean after the prior console-stabilization pass, but this report is not a runtime certification.

## Corrected Finding

The older same-day audit claim that `HectonEventBus` tracks dispatch depth but has no max-depth cap is stale.

Current source evidence:

- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:136` defines `MaxDispatchDepth = 4`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:321` enters `TryEnterDispatch()`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:323` rejects dispatch when `_dispatchDepth >= MaxDispatchDepth`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:332` reports cascade telemetry before dropping the payload.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:340` routes the warning through `CrashTelemetryBuffer.ReportEventCascadeWarning()`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:462`, `:616`, and `:778` call the global dispatch-depth guard before unmanaged, native-byte, and managed event dispatch.

Dispatcher-side source evidence:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:32` defines `MaxLateFrameEventsPerFrame = 1000`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:470` starts the late-frame event budget.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:501` exposes `TryConsumeLateFrameEventDispatch()`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:529` reports circuit-breaker trips through `CrashTelemetryBuffer.ReportEventCascadeWarning()`.

## Remaining Risk

The hard depth cap is present. The remaining event risk is not "unbounded recursion with no cap."

The remaining risk is same-frame generation processing in unmigrated NativeQueue-backed lanes:

- `BootstrapEvents.FlushPending()` now has front/back source-level generation split.
- `InteractionEvents.FlushPending()` now has front/back source-level generation split.
- `CraftingEvents.FlushPending()` now has front/back source-level generation split.
- `ScanEvents.FlushPending()` now has front/back source-level generation split.
- `SaveEvents.FlushPending()` now has front/back source-level generation split.
- `InventoryEvents.FlushPending()` now has front/back source-level generation split.
- `WeatherEvents.FlushPending()` now has front/back source-level generation split.
- `QuestEvents.FlushPending()` now has front/back source-level generation split.
- `PowerGridTelemetryEvents.FlushPending()` now has front/back source-level generation split.
- `NarrativeEvents.FlushPending()` now has front/back source-level generation split.
- `NotificationEvents.FlushPending()` now has front/back source-level generation split.
- `FirstHourEvents.FlushPending()` now has front/back source-level generation split.
- `EndingEvents.FlushPending()` now has front/back source-level generation split.
- `AtmosphereEvents.FlushPending()` now has front/back source-level generation split.
- `EclipseGameplayEvents.FlushPending()` now has front/back source-level generation split.
- `AcousticZoneEvents.FlushPending()` now has front/back source-level generation split.
- `CelestialEvents.FlushPending()` now has front/back source-level generation split.
- `MapMagicBiomeEvents.FlushPending()` now has front/back source-level generation split.
- `BiomeMatrixEvents.FlushPending()` now has front/back source-level generation split.
- Other NativeQueue-backed lanes still need source review and one-by-one migration before this can be claimed globally.
- In unmigrated lanes, if a listener publishes another event into the same lane during dispatch, the new payload can be consumed in the same `LateUpdate` until the global budget trips.

The upper bound is currently:

```text
MaxLateFrameEventsPerFrame * handler_cost
```

With the current source value:

```text
1000 * handler_cost
```

That is bounded, but still capable of burning a full late-frame budget every frame if a logic cycle keeps producing work.

## Required Future Fix

Do not add another depth cap to `HectonEventBus`; one already exists.

The next correct fix is continued generation split rollout for the remaining NativeQueue-backed lanes:

- current generation drains from a front queue
- publishes during listener dispatch write into a back queue
- front/back promotion happens only after the current front generation is empty
- payloads created by handlers are processed next frame unless a lane explicitly opts into same-frame reentrancy

This is a behavior change. It needs a Play Mode test because some systems may currently rely on same-frame event propagation.

## Regression Model

CPU: current source is bounded by the dispatcher budget and mod bus depth cap. Generation split would reduce same-frame spikes but can add one-frame latency.

GC: no runtime code changed in this recheck. A future generation split must use pre-created NativeQueues or fixed native buffers.

Memory: no runtime memory changed in this recheck. A future double-buffered lane costs one additional queue/buffer per event lane.

Cadence: current cadence permits same-frame propagation until budget exhaustion. Generation split would make event cadence more deterministic.

Correctness: static finding corrected. Runtime behavior remains unverified.

## Status Change

- `HectonEventBus` max-depth cap: SOURCE-PRESENT.
- `SystemDispatcher` late-frame budget breaker: SOURCE-PRESENT.
- NativeQueue generation split: PARTIAL SOURCE-PRESENT as of 2026-05-03 for `BootstrapEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `CelestialEvents`, `MapMagicBiomeEvents`, and `BiomeMatrixEvents` only.
- Runtime stress proof: ABSENT.

## 2026-05-02 InteractionEvents Delta

`Assets/_Project/Scripts/Interaction/InteractionEvents.cs` now owns front/back `NativeQueue<InteractionEventPayload>` generations.

- listener reenqueue during `OnInteractionEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations

This is source-level only. It does not prove global event safety because other NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 CraftingEvents Delta

`Assets/_Project/Scripts/CraftingEvents.cs` now owns front/back `NativeQueue<CraftingEventPayload>` generations.

- listener reenqueue during `OnCraftingEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- managed fabricator/recipe/item sidecar slots remain occupied until listener dispatch completes or no-listener drain releases the payload

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 ScanEvents Delta

`Assets/_Project/Scripts/ScanEvents.cs` now owns front/back `NativeQueue<ScanEventPayload>` generations.

- listener reenqueue during `OnScanEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- scan metadata remains in the existing hash cache and is only retained after enqueue success

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 InventoryEvents Delta

`Assets/_Project/Scripts/InventoryEvents.cs` now owns front/back `NativeQueue<InventoryEventPayload>` generations.

- listener reenqueue during `OnInventoryEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- managed `ItemData` / `PlayerInventory` sidecar slots remain occupied until listener dispatch completes or no-listener drain releases the payload
- existing per-frame duplicate suppression is retained before enqueue

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 SaveEvents Delta

`Assets/_Project/Scripts/SaveEvents.cs` now owns front/back `NativeQueue<SaveEventPayload>` generations.

- listener reenqueue during `OnSaveEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- payload remains unmanaged with `FixedString` slot/message fields

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 BootstrapEvents Delta

`Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs` now owns front/back `NativeQueue<BootstrapEventPayload>` generations.

- listener reenqueue during `OnBootstrapEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- payload remains unmanaged and the lane is capped at four pending payloads

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 WeatherEvents Delta

`Assets/_Project/Scripts/Environment/WeatherEvents.cs` now owns front/back `NativeQueue<WeatherEventPayload>` generations.

- listener reenqueue during `OnWeatherEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- payload remains unmanaged and carries runtime weather/current snapshot fields

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 QuestEvents Delta

`Assets/_Project/Scripts/Quest/QuestEvents.cs` now owns front/back `NativeQueue<QuestEventPayload>` generations.

- listener reenqueue during `OnQuestEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- `QuestGraphEvaluator.FlushPendingSignals()` remains before quest event drain

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 PowerGridTelemetryEvents Delta

`Assets/_Project/Scripts/Power/PowerGridTelemetryEvents.cs` now owns front/back `NativeQueue<PowerGridTelemetrySnapshot>` generations.

- listener reenqueue during `OnPowerGridTelemetryUpdated(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- payload remains a readonly aggregate telemetry snapshot

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 NarrativeEvents Delta

`Assets/_Project/Scripts/NarrativeEvents.cs` now owns front/back `NativeQueue<NarrativeEventPayload>` generations.

- listener reenqueue during `OnNarrativeEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- direct POI listener callbacks remain immediate and outside the queue lane
- discovery id lookup is retained only after enqueue success

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 NotificationEvents Delta

`Assets/_Project/Scripts/UI/NotificationEvents.cs` now owns front/back `NativeQueue<NotificationEventPayload>` generations.

- listener reenqueue during `OnNotificationEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- message lookup is retained only after capacity acceptance

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 FirstHourEvents Delta

`Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs` now owns front/back `NativeQueue<FirstHourEventPayload>` generations.

- listener reenqueue during `OnFirstHourMilestoneReached(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- listener-count gate and unmanaged milestone payload are retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 EndingEvents Delta

`Assets/_Project/Scripts/Gameplay/EndingSystem.cs` now owns front/back `NativeQueue<EndingEventPayload>` generations.

- listener reenqueue during `OnEndingEvent(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- listener-count gate and unmanaged ending payload are retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 AtmosphereEvents Delta

`Assets/_Project/Scripts/HectonAtmosphereManager.cs` now owns front/back `NativeQueue<EnvironmentState>` generations.

- listener reenqueue during `OnAtmosphereStateChanged(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- existing atmosphere state machine and `EnvironmentState` payload are retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 EclipseGameplayEvents Delta

`Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs` now owns front/back `NativeQueue<EclipseGameplayEventPayload>` generations.

- listener reenqueue during eclipse gameplay callbacks writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- phase, predator pressure, and temperature delta event types are retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 AcousticZoneEvents Delta

`Assets/_Project/Scripts/AcousticZoneController.cs` now owns front/back `NativeQueue<AcousticZoneChangedEvent>` generations.

- listener reenqueue during `OnAcousticZoneChanged(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- acoustic-zone payload, mixer logic, and snapshot application are retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 CelestialEvents Delta

`Assets/_Project/Scripts/HectonCelestialEngine.cs` now owns front/back `NativeQueue<CelestialEventPayload>` generations.

- listener reenqueue during celestial callbacks writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- sun-angle and planet-phase coalescing flags remain active and latest-value storage is retained

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 MapMagicBiomeEvents Delta

`Assets/_Project/Scripts/MapMagicBridge.cs` now owns front/back `NativeQueue<int>` generations for biome id events.

- listener reenqueue during `OnMapMagicBiomeChanged(...)` writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- MapMagic integration remains routed through `MapMagicBridge`; no third-party asset/runtime wrapper changes were made

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

## 2026-05-03 BiomeMatrixEvents Delta

`Assets/_Project/Scripts/BiomeMatrixDirector.cs` now owns front/back `NativeQueue<BiomeMatrixEventPayload>` generations.

- listener reenqueue during matrix/depth callbacks writes to the next-frame queue
- current front generation must drain before next-frame payloads are promoted
- budget trips do not mix reentrant events into the current lane
- `PendingCount` reports both generations
- existing profile-slot sidecar cache is retained and still resolves profile references outside the unmanaged payload

This is source-level only. It does not prove global event safety because the remaining NativeQueue-backed lanes still use their existing flush models, and no Play Mode event-cascade stress test was run.

STATUS: PENDING VERIFICATION

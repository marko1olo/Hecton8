# HECTON-8 EVENT BUS MAP
Date: 2026-05-07
Status: PENDING VERIFICATION

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


Scope: source-backed orientation map for the event-bus layer.

---

## Purpose

This file is a lightweight orientation map only.

It should not be treated as the definitive publisher/subscriber truth table.
For the larger static readout, use:

- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`

## Active Topology

The first-party queue-backed pattern now used by the migrated buses is:

1. Publisher computes stable `uint` FNV-1a hashes for authored string IDs.
2. Publisher enqueues a blittable payload struct into a bus-local `NativeQueue<TPayload>`.
3. `SystemDispatcher.LateUpdate()` flushes each queue on the main thread.
4. Listeners are explicit interfaces registered into `RegistryBucket<TListener>`.
5. Cold-path string recovery, when still required for UI/save compatibility, uses hash-to-string dictionaries outside hot paths.

This removes direct managed string-delegate fanout from the migrated buses and moves runtime dispatch into deterministic late-frame drains.

## Typed Signal-Lane Orientation

The old five-artery grouping is legacy shorthand only. Current ownership uses the 9-echelon / 85-domain map plus typed `SignalBus<T>` and `NativeQueue` lanes; Core/Env/Player/Base/AI are local reading buckets, not complete architecture coverage or exclusive cross-domain authority.

Authority rule:

- `Core`, `Env`, `Player`, `Base`, and `AI` are local documentation buckets, not the only documented first-party cross-domain communication authority.
- Any direct `Action`, `Func`, C# `event`, `delegate`, or `UnityEvent` chain that crosses a domain boundary is legacy debt unless this document explicitly marks it as local, inspector-only, or modding-only.
- Local callbacks inside a single owner are not signal-lane authority and must not be documented as cross-domain authority.
- `HectonEventBus` remains a separate managed modding boundary; it is not the first-party bus authority for gameplay systems.

| Legacy bucket | Scope | Representative lanes |
|---|---|---|
| Core | bootstrap, registry, save/load, localization, telemetry, object-pool diagnostics, scene bootstrap, mod registry | `BootstrapEvents`, `GlobalRegistry`, `SaveEvents`, `LocalizationEvents`, `GlobalTelemetryBus`, `ObjectPoolDiagnostics`, `SceneBootstrap`, `ModRegistryEvents` |
| Env | atmosphere, weather, celestial, biome, acoustic, fluid, pressure, physics, depth, soundscape, random/seismic world pressure | `AtmosphereEvents`, `WeatherEvents`, `CelestialEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `AcousticZoneEvents`, `FluidFeedbackEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `PhysicsEventBus`, `DepthZoneEvents`, `SoundscapeEvents`, `RandomEventEvents` |
| Player | interaction, crafting, scanner, PDA, inventory, tool state, player signals, notifications, Atlas signal UI-facing lanes | `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `PDAEvents`, `PDAIntrusionEvents`, `InventoryEvents`, `FlashlightEvents`, `LaserCutterEvents`, `PlayerSignalEvents`, `PlayerExpressionEvents`, `NotificationEvents`, `AtlasSignalEvents` |
| Base | construction/base module state, submarine OS, power telemetry, airlock, base integrity, emergency relays, drone fleet telemetry | `ModuleStatusEvents`, `BaseAirlockEvents`, `BaseIntegrityEvents`, `HectonSubmarineOsEvents`, `PowerGridTelemetryEvents`, `EmergencyServiceRelayEvents`, `HectonDroneFleetEvents` |
| AI | director, quest/progression, narrative/audio-log, first-hour/ending, Atlas-6 directives, ecosystem/fauna pressure | `DirectorAIEvents`, `QuestEvents`, `NarrativeEvents`, `AudioLogEvents`, `FirstHourEvents`, `EndingEvents`, `Atlas6Events`, `SargassumGlobalDragManager` |

## Queue-Backed Buses Observed In Static Source

| Bus | Payload | Listener interface | Flush owner |
|---|---|---|---|
| `NarrativeEvents` | `NarrativeEventPayload` | `INarrativeEventListener` | `SystemDispatcher.LateUpdate()` |
| `ScanEvents` | `ScanEventPayload` | `IScanEventListener` | `SystemDispatcher.LateUpdate()` |
| `SaveEvents` | `SaveEventPayload` | `ISaveEventListener` | `SystemDispatcher.LateUpdate()` |
| `AudioLogEvents` | `AudioLogEventPayload` | `IAudioLogEventListener` | `SystemDispatcher.LateUpdate()` |
| `QuestEvents` | `QuestEventPayload` | `IQuestEventListener` | `SystemDispatcher.LateUpdate()` |
| `AtlasSignalEvents` | `AtlasSignalEventPayload` | `IAtlasSignalEventListener` | `SystemDispatcher.LateUpdate()` |
| `NotificationEvents` | `NotificationEventPayload` | `INotificationEventListener` | `SystemDispatcher.LateUpdate()` |
| `SceneBootstrap` event lane | `SceneBootstrapEventPayload` | `ISceneBootstrapEventListener` | `SystemDispatcher.LateUpdate()` |
| `Atlas6Events` | `Atlas6EventPayload` | `IAtlas6EventListener` | `SystemDispatcher.LateUpdate()` |
| `ObjectPoolDiagnostics` | `PoolDiagnosticsEventPayload` | `IObjectPoolDiagnosticsListener` | `SystemDispatcher.LateUpdate()` |

## Modding Bus Boundary

`HectonEventBus` is still a separate managed typed bus for moddable runtime events.
It is not the owner of `SaveEvents`, `QuestEvents`, or `ScanEvents`.
Those first-party buses remain `NativeQueue<TPayload>` lanes flushed by `SystemDispatcher.LateUpdate()`.

## Current Documentation Boundary

The present workspace contains two event-mapping documents:

1. This file in `01_GENERAL_INFO`
2. `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md` in `02_ACTUAL_REPORTS`

The detailed routing document is the better source for raw mappings.
This file should remain a short orientation page, not a second large truth table.

## File Map

Primary first-party event files currently tied to this topology:

- `Assets/_Project/Scripts/NarrativeEvents.cs`
- `Assets/_Project/Scripts/ScanEvents.cs`
- `Assets/_Project/Scripts/SaveEvents.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs`
- `Assets/_Project/Scripts/Quest/QuestEvents.cs`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs`
- `Assets/_Project/Scripts/UI/NotificationEvents.cs`
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`
- `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`
- `Assets/_Project/Scripts/ObjectPoolDiagnostics.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`

## Constraints From Project Instructions

| Constraint | Source |
|---|---|
| Event buses are expected to be static and zero-allocation | `AGENTS.md` |
| String-based event names are forbidden in first-party event-bus design | `AGENTS.md` |
| Queue-backed / late-flush behavior is the mandated direction for the canonical event bus | `AGENTS.md` |

## Open Risk

- `HectonEventBus` remains a separate managed typed bus and is not replaced by these queue lanes.
- Current source scan found one static `Action`-typed callback property in `SaveThumbnailSystem.cs`; it is an async GPU readback bridge, not a documented cross-domain bus lane.
- Instance-level `Action`, `delegate`, and `UnityEvent` surfaces still exist across the script tree and remain owner-by-owner leak/debt audit candidates when they cross domain boundaries.
- Current reachable editor readback is not compile-clean proof. Latest visible console slice shows package-side MCP `ManageAsset` failures on `ResourceNodeTemplate_*` assets, not first-party event-bus compile errors.
- Runtime wiring, subscriber completeness, and teardown behavior still remain `PENDING VERIFICATION`.

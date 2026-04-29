# HECTON-8 EVENT BUS MAP

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Scope:** Historical summary of the event-bus layer.  
**Chronology Note:** The previous version carried an impossible future scan date. This rewrite removes that contradiction.

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

## Queue-Backed Buses Verified In Code

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
2. `EVENT_FLOW_MAP.md` in `02_ACTUAL_REPORTS`

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
- `Assets/_Project/Scripts/SceneBootstrap.cs`
- `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`
- `Assets/_Project/Scripts/ObjectPoolDiagnostics.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`

## Verified Constraints From Project Instructions

| Constraint | Source |
|---|---|
| Event buses are expected to be static and zero-allocation | `AGENTS.md` |
| String-based event names are forbidden in first-party event-bus design | `AGENTS.md` |
| Queue-backed / late-flush behavior is the mandated direction for the canonical event bus | `AGENTS.md` |

## Open Risk

- `HectonEventBus` remains a separate managed typed bus and is not replaced by these queue lanes.
- Multiple older static `Action<T>` buses still exist outside this migrated set.
- Current reachable editor readback is not compile-clean proof. Latest visible console slice shows package-side MCP `ManageAsset` failures on `ResourceNodeTemplate_*` assets, not first-party event-bus compile errors.
- Runtime wiring, subscriber completeness, and teardown behavior still remain `PENDING VERIFICATION`.

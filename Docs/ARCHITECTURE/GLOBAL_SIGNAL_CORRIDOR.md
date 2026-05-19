# Global Signal Corridor

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

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

Owner Source: `Assets/_Project/Scripts/Core/GlobalSignals.cs`

## Authority

`GlobalSignals` is the retained direct-queue bridge and `SignalBus<T>` initialization surface. First-party broadcasts use typed `SignalBus<T>` lanes unless a direct `NativeQueue` lane is explicitly documented with owner, capacity, overflow policy, layout, and telemetry. It is not the old five-bus prose model and not a catch-all event bus.

R27 source-counter snapshot, retained until a newer counter pass reruns it:

| Fact | Value |
|---|---:|
| Direct `CreateQueue(...)` native queue slots in `InitializeAllQueues()` | 73 |
| Typed `SignalBus<T>.EnsureInitialized()` lanes in the `GlobalSignals` initialization surface | 133 including `DebugSignal` via `ConfigureDebugSignalLane()` |
| Modding validator source `ISignal` structs | current static validator pass reports `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, and `ModCommandSizeBytes=64`; this is static schema/input-surface proof only, not runtime-lane, profiler, or mod smoke proof |
| signal struct sizes | validator source exists via `ValidateSignalSize` / `ValidateSignalPayload`; current static validator pass covers Mod API schema only and is not compile, runtime-lane, profiler, or mod smoke proof |
| fallback SPSC container | `SpscSignalRingBuffer<T>` |

## Lane Table

| Capacity | Lanes |
|---:|---|
| 256 | Legacy direct queue examples: `DamageSignal`, `ImpactSignal`, `ControlSignal` |
| 128 | `DebrisSpawnSignal`, `DeflectSignal`, `AnomalySignal`, `TelemetryAnomalySignal`, `InteractionUiSignal`, `SpectrumScanSignal`, `RigidbodySleepSignal`, `ScanCompleteSignal`, `ReconDataSignal` |
| 64 | `AupPreShiftSignal`, `AupShiftSignal`, `BrownoutSignal`, `EntityDeathSignal`, `RebaseSignal`, `HabitatConstructionSignal`, `VocalWarningSignal`, `AcousticPingSignal`, `SonarPingSignal`, `ComplianceViolationSignal`, `ItemDecaySignal`, `PlayerStressSignal` |
| 32 | `DataReloadSignal`, `HypoxiaSignal`, `OxygenCriticalSignal`, `WeatherStrengthSignal` |
| 16 | `SolarFlareSignal`, `MemoryPressureSignal`, `SaveLifecycleSignal`, `GlobalTimeSyncSignal` |

## ParallelWriter Contract

The `ParallelWriter` API exists only for job/background producers that cannot touch Unity objects and cannot block the main thread.

Exposed writer lanes:

- `DamageSignalWriter`
- `ImpactSignalWriter`
- `AupPreShiftSignalWriter`
- `AupShiftSignalWriter`
- `BrownoutSignalWriter`
- `DeflectSignalWriter`
- `EntityDeathSignalWriter`
- `AnomalySignalWriter`
- `AcousticPingSignalWriter`
- `HypoxiaSignalWriter`
- `ScanCompleteSignalWriter`
- `RigidbodySleepSignalWriter`
- `GlobalTimeSyncSignalWriter`

Rule:

```text
producer job -> NativeQueue<T>.ParallelWriter.Enqueue
main/thread owner -> TryDequeue* drain
```

No consumer is allowed to wait on the producer lane during frame-critical execution. The drain side must consume only the available packets for the current budget.

## Main Thread Stall Avoidance

`InitializeAllQueues()` allocates each lane with `Allocator.Persistent`, registers it with `NativeMemorySentinel`, and prewarms the queue by enqueue/dequeue cycling `expectedCapacity` packets. That moves the allocator cost to boot.

`Publish(in T)` is a main-thread convenience wrapper over enqueue. It is not a dispatch callback. It does not invoke listeners.

`TryDequeue*` returns immediately. It is the only legal runtime consume model for this corridor.

## Payload Law

Signal payloads must remain:

- unmanaged
- no `string`
- no managed array
- no `UnityEngine.Object`
- no delegate
- fixed size, normally 32 or 64 bytes

Development/editor validation in `GlobalSignals` rejects managed-reference payloads and size drift.

## SPSC Versus MPSC

`SpscSignalRingBuffer<T>` is the single-producer/single-consumer escape hatch. It uses power-of-two capacity, `_head/_tail`, `Volatile.Read`, `Volatile.Write`, and a mask for wrapping.

`NativeQueue<T>.ParallelWriter` is the MPSC path. It is the correct choice when multiple jobs can produce the same event type.

## Integration Points

`GameBootstrapper` initializes the corridor before simulation service activation and disposes it on shutdown:

- initialize: `GlobalSignals.InitializeAllQueues()`
- shutdown: `GlobalSignals.DisposeAllQueues()`

## Forbidden

- Main-thread event cascades from inside `Publish`.
- Cross-domain C# `event`, `Action`, `Func`, or `UnityEvent` as architecture authority.
- Signal structs above 64 bytes unless a source comment documents the cold-lane exception.
- Managed allocations in hot signal production or drain code.

## 2026-05-19 EventBus Boundary

`SignalBus<T>` is the first-party runtime broadcast path.

`HectonEventBus` is not that path. It is a mod/API/cold boundary with managed
callback isolation. It may protect external extensions; it must not become the
hot gameplay bus.

Rules:

- New first-party hot gameplay traffic uses typed `SignalBus<T>` lanes.
- New mod/API traffic may use `HectonEventBus` only when it remains outside the
  gameplay hot path and has watchdog/failure isolation.
- `GlobalSignals.Publish` is legacy bridge traffic unless the payload is already
  owned by a documented direct queue lane.
- New direct `NativeQueue` surfaces in `GlobalSignals.cs` are rejected unless the
  task is explicitly a bridge/migration task.
- A lane is incomplete until owner, producer phase, consumer phase, capacity,
  overflow policy, retention policy, payload layout, and telemetry are recorded.

Failure classification:

| Symptom | Classification | Required Fix |
|---|---|---|
| First-party `HectonEventBus.Publish` in gameplay loop | Hot managed bus leak | Move to `SignalBus<T>` or owner interface |
| New `GlobalSignals.Publish` for unrelated traffic | Legacy corridor growth | Add/route through typed lane |
| One giant enum/switch signal | Monolithic lane | Split by owner/domain and phase |
| No overflow telemetry | Unbounded failure mode | Add drop/coalesce/fail-fast counter |

See `GLOBAL_AUTHORITY_BOUNDARIES.md` for cross-surface rules.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

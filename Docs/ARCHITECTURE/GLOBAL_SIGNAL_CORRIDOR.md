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

`GlobalSignals` is the current first-party signal corridor. It is not the old five-bus prose model.

Source scan:

| Fact | Value |
|---|---:|
| Direct `CreateQueue(...)` native queue slots in `InitializeAllQueues()` | 73 |
| Typed `SignalBus<T>.EnsureInitialized()` lanes in `InitializeCategorySignalLanes()` | 133 |
| Debug lane | `DebugSignal` via `ConfigureDebugSignalLane()` |
| Modding validator source `ISignal` structs | 160 |
| signal struct sizes | source-validated by `ValidateSignalSize` / `ValidateSignalPayload`; not profiler proof |
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

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

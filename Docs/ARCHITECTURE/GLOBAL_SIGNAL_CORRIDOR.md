# Global Signal Corridor

Date: 2026-05-12
Status: SOURCE VERIFIED / RUNTIME PENDING
Owner Source: `Assets/_Project/Scripts/Core/GlobalSignals.cs`

## Authority

`GlobalSignals` is the current first-party signal corridor. It is not the old five-bus prose model.

Source scan:

| Fact | Value |
|---|---:|
| NativeQueue lanes | 33 |
| public signal structs | 33 |
| `ParallelWriter` lanes | 13 |
| signal struct sizes | 32 or 64 bytes |
| fallback SPSC container | `SpscSignalRingBuffer<T>` |

## Lane Table

| Capacity | Lanes |
|---:|---|
| 256 | `DamageSignal`, `ImpactSignal`, `ControlSignal` |
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

STATUS: SOURCE VERIFIED / RUNTIME PENDING

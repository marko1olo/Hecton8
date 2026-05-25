# Global Signal Corridor

Date: 2026-05-12

Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime lane health, profiler, mod smoke, GC, or player-build proof.

- `Assets/_Project/Scripts/Core/GlobalSignals.cs`

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

- `Assets/_Project/Scripts/Editor/SignalPayloadLayoutValidator.cs`

- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`

Owner Source: `Assets/_Project/Scripts/Core/GlobalSignals.cs`

## Authority

`GlobalSignals` is the retained direct-queue bridge and `SignalBus<T>` initialization surface.

- First-party broadcasts use typed `SignalBus<T>` lanes.
- Exception: direct `NativeQueue` lane with owner, capacity, overflow policy, layout, and telemetry.
- This is not the old five-bus prose model.
- It is not a catch-all event bus.

2026-05-19 SHINOBU_02 read-only source recapture, retained until a locked counter pass reruns it:

| Fact | Value |

|---|---:|

| Direct `CreateQueue(...)` native queue slots in `InitializeAllQueues()` | 73 |

| Typed `SignalBus<T>.EnsureInitialized()` lanes | 135 by 2026-05-20 R38 source recapture |
| Direct `CreateQueue(...)` slots | 73 remain |
| `SignalBus<T>.Configure/EnsureInitialized` hits in `GlobalSignals.cs` | 271 |

| Modding validator source `ISignal` structs | static validator pass: `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`; STATIC_SOURCE / PY_TOOL only; no compile/import/runtime/profiler/GC/platform/mod-smoke proof |

| signal struct sizes | validator source exists via `ValidateSignalSize` / `ValidateSignalPayload`; static pass covers Mod API schema orientation only; no compile/runtime/profiler/GC/platform/mod-smoke proof |

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

- The `ParallelWriter` API is a retained legacy MPSC bridge for low-frequency job/background producers that cannot touch Unity objects and cannot block the main thread.
- Cache-line-critical producer storms must not use it as the primary route; they use owner-local or Vault-backed thread-local scratch plus deterministic commit.
- `GlobalSignals.OpenSignalWriterForProducerPhase<TSignal>()` opens only the requested typed bridge lane; it must not prewarm every direct `GlobalSignals` queue.

Adjacent Core helpers use the same vocabulary.

Writer acquisition uses `Open*` names; compatibility aliases are low-frequency bridge debt. `TryGet*` surfaces must not initialize queue storage.

Exposed writer lanes:

The list below is a legacy non-authoritative sample. Current source exposes 34

named writer properties plus generic `SignalBus<T>.ParallelWriter`; regenerate

with the grep command in `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md` before exact use.

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

Legacy bridge rule:

```text

low-frequency producer job -> NativeQueue<T>.ParallelWriter.Enqueue

main/thread owner -> TryConsumeFrame/TryDequeue* bridge drain

```

No consumer is allowed to wait on the producer lane during frame-critical execution. The drain side must consume only the available packets for the current budget.

## Main Thread Stall Avoidance

`InitializeAllQueues()` allocates each lane with `Allocator.Persistent`, registers it with `NativeMemorySentinel`, and prewarms the queue by enqueue/dequeue cycling `expectedCapacity` packets. That moves the allocator cost to boot.

`Publish(in T)` and typed `SignalBus<T>.Push/TryPush` are producer-side enqueue/snapshot-entry surfaces. They are not dispatch callbacks and do not invoke listeners.

`TryConsumeFrame(...)` and retained bridge `TryDequeue*` drains return immediately. Snapshot reads remain read-only; destructive cursor drains must use explicit consume/dequeue names.

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

`SpscSignalRingBuffer<T>` is the single-producer/single-consumer escape hatch. It uses power-of-two capacity, `_head/_tail`, `Volatile.Read`, `Interlocked.Exchange`, and a mask for wrapping.

`NativeQueue<T>.ParallelWriter` is the legacy MPSC bridge path.

- Valid only for low-frequency or unpredictable producer traffic.
- Use only when thread-local batching costs more than event volume.
- High-frequency same-lane producers use the SHINOBU thread-local corridor.
- Required corridor traits: per-worker scratch, explicit 64-byte payload layout, deterministic post-simulation commit, coalescence, telemetry-visible overflow.

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

- Signal publication is never hidden inside `Get*`, `TryGet*`, `Resolve*`, or

  `Read*` APIs. Owners publish from named dispatcher phases; consumers read

  snapshots.

- SignalBus is not request/response. One private caller uses a cached owner

  interface or command queue, not a broadcast lane.

Failure classification:

| Symptom | Classification | Required Fix |

|---|---|---|

| First-party `HectonEventBus.Publish` in gameplay loop | Hot managed bus leak | Move to `SignalBus<T>` or owner interface |

| New `GlobalSignals.Publish` for unrelated traffic | Legacy corridor growth | Add/route through typed lane |

| One giant enum/switch signal | Monolithic lane | Split by owner/domain and phase |

| No overflow telemetry | Unbounded failure mode | Add drop/coalesce/fail-fast counter |

See `GLOBAL_AUTHORITY_BOUNDARIES.md` for cross-surface rules.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

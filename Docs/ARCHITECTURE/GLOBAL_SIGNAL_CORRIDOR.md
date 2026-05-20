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

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime lane health, profiler, mod smoke, GC, or player-build proof.

- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/Editor/SignalPayloadLayoutValidator.cs`
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`

Owner Source: `Assets/_Project/Scripts/Core/GlobalSignals.cs`

## Authority

`GlobalSignals` is the retained direct-queue bridge and `SignalBus<T>` initialization surface. First-party broadcasts use typed `SignalBus<T>` lanes unless a direct `NativeQueue` lane is explicitly documented with owner, capacity, overflow policy, layout, and telemetry. It is not the old five-bus prose model and not a catch-all event bus.

2026-05-19 SHINOBU_02 read-only source recapture, retained until a locked counter pass reruns it:

| Fact | Value |
|---|---:|
| Direct `CreateQueue(...)` native queue slots in `InitializeAllQueues()` | 73 |
| Typed `SignalBus<T>.EnsureInitialized()` lanes in the `GlobalSignals` initialization surface | 135 by 2026-05-20 R38 source recapture; direct `CreateQueue(...)` slots remain 73; `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs` are 271 |
| Modding validator source `ISignal` structs | current static validator pass reports `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, and `ModCommandSizeBytes=64`; this is STATIC_SOURCE / PY_TOOL orientation only, not compile, Unity import, runtime-lane, profiler, GC, platform, or mod smoke proof |
| signal struct sizes | validator source exists via `ValidateSignalSize` / `ValidateSignalPayload`; current static validator pass covers Mod API schema orientation only and is not compile, runtime-lane, profiler, GC, platform, or mod smoke proof |
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



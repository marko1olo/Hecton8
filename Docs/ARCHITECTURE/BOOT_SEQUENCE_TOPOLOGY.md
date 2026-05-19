# Boot Sequence Topology

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

Owner Source: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

## Six-Stage Lifecycle

The stable lifecycle is:

```text
Allocators -> Signals -> I/O -> Monolith -> Simulation -> Presentation
```

This is the documentation contract. The current implementation still has legacy phase names inside `GameBootstrapper`, but the source-backed dependency order maps to the six stages below.

| Stage | Source Owner | Required Work | Hard Rule |
|---:|---|---|---|
| 1 | allocators / memory sentinels | persistent native memory, arena, budget trackers | allocate cold, never in hot tick |
| 2 | `GlobalSignals` | prewarm source-observed native signal queues: R27 scan sees `73` direct `CreateQueue(...)` slots and `133` typed `SignalBus<T>.EnsureInitialized()` lanes including `DebugSignal`; rerun command, timestamp, and artifact before exact-count use | no listener callbacks during publish |
| 3 | platform I/O | persistent path, FileStream save I/O, native bridges | no MMF claims without source proof |
| 4 | `H8StaticDataArena` | load `.h8bin` into persistent native arena | validate header, directory, checksum |
| 5 | registry services | Kahn-sorted bootstrap nodes and GlobalRegistry service slots | no singleton self-wiring |
| 6 | presentation | UI, render dispatcher, shader math LOD warmup | presentation consumes state, not owns simulation |

## Current `GameBootstrapper` Nodes

`GameBootstrapper` contains a Kahn topological execution cache and 26 bootstrap dependency nodes:

| Order Class | Nodes |
|---|---|
| Core cadence | `SystemDispatcher`, `GameTickManager`, `SaveManager`, `ObjectPoolManager`, `RenderDispatcher`, `SceneRuntimeService`, `EquipmentInteractionHandler`, `ModWorldPersistenceManager` |
| World/simulation | `HectonFloatingOrigin`, `ConnectionSplineBatchRenderer`, `GlobalPhysicsStateManager`, `PhysicsApplySystem`, `DebrisManager`, `EnvironmentRuntimeContextService`, `OceanKinematicsRuntimeService`, `EcosystemDirector`, `FaunaSimulation`, `PowerGridManager`, `ConstructionManager` |
| Input/player | `NativeInputManager`, `InputDispatcher`, `PlayerRuntimeContextService`, `PlayerInventoryManager`, `PlayerSensoryManager`, `BeaconNetworkSystem` |
| Presentation bridge | `SpatialAudioManager` |

## Source Evidence

| Source Event | Location |
|---|---|
| bootstrap enum exists | `BootstrapPhase` in `GameBootstrapper.cs` |
| Kahn order cache exists | `_bootstrapExecutionOrder` |
| signal corridor init | `GlobalSignals.InitializeAllQueues()` |
| data monolith init | `H8StaticDataArena.TryInitializeFromStreamingAssets(...)` |
| math LOD warmup | `WarmMathLodShaderKeywords()` |
| signal corridor shutdown | `GlobalSignals.DisposeAllQueues()` |

## Topological Rule

No system can use a service before the service is registered in `GlobalRegistry`.

No simulation system can publish a signal before `GlobalSignals.InitializeAllQueues()`.

No presentation system can be treated as authoritative state. Presentation can read from registry services, consume signals, or render snapshots.

## Current Discrepancies

| Claim | Code Truth |
|---|---|
| "Single clean bootstrap sovereign" | false; bootstrap authority still spans `GameBootstrapper`, `SceneBootstrap`, and legacy owner surfaces |
| "FileStream everywhere" | false for Data Monolith; `H8StaticDataArena` still uses boot-only `File.ReadAllBytes` staging |
| "Five artery event bus" | stale; R27 static source scan sees `73` direct native queue slots and `133` typed `SignalBus<T>` lanes including `DebugSignal`; rerun before exact use |

## Verification Required

Still pending:

- Unity import
- clean Console readback
- Play Mode boot pass
- frame-time proof for service initialization
- no post-boot managed allocation trace

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

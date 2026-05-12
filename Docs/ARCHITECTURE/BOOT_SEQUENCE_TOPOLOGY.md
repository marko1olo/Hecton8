# Boot Sequence Topology

Date: 2026-05-12
Status: SOURCE VERIFIED / RUNTIME PENDING
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
| 2 | `GlobalSignals` | create 33 persistent signal queues and prewarm them | no listener callbacks during publish |
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
| "Five artery event bus" | stale; current source has 33 typed `GlobalSignals` lanes |

## Verification Required

Still pending:

- Unity import
- clean Console readback
- Play Mode boot pass
- frame-time proof for service initialization
- no post-boot managed allocation trace

STATUS: SOURCE VERIFIED / RUNTIME PENDING

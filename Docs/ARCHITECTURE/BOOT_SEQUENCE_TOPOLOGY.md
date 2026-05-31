# Boot Sequence Topology

Date: 2026-05-12

Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity scene import, boot completion, Console cleanliness, profiler, or player-build proof.

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs`

- `Assets/_Project/Scripts/Bootstrap/BootstrapRouteEnforcer.cs`

- `Assets/_Project/Scripts/Bootstrap/BootstrapHealthMonitor.cs`

- `Assets/_Project/Scripts/Bootstrap/SceneGuard.cs`

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

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

| 2 | `GlobalSignals` | prewarm source-observed native signal queues; R43 scan linked below | no listener callbacks during publish |

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

- Presentation is not authoritative state.
- Presentation consumes cached injected service interfaces, typed `SignalBus<T>` snapshots, documented `GlobalSignals` bridge snapshots, or render snapshots.
- Presentation must not hot-read `GlobalRegistry` slots during updates.

## Current Discrepancies

| Claim | Code Truth |

|---|---|

| "Single clean bootstrap sovereign" | false; bootstrap authority still spans `GameBootstrapper`, `BootstrapEvents`, `SceneGuard`, and legacy owner surfaces; no first-party `SceneBootstrap.cs` exists in the current source scan |

| "FileStream everywhere" | false for Data Monolith; `H8StaticDataArena` uses MMF/Win32 native reads on desktop and the Android player branch uses the NDK `AAssetManager` source-plugin bridge to copy `static_data.h8bin` directly into the Vault arena. Android/Quest URI staging is not the monolith route. |

| "Five artery event bus" | stale; R43 scan: `73` direct queue slots, `135` typed lanes, `271` configure/ensure hits, `116` `NativeQueue` refs, `1328` script-level typed-lane matches; rerun before use |

## Verification Required

Still pending:

- Unity import

- clean Console readback

- Play Mode boot pass

- frame-time proof for service initialization

- no post-boot managed allocation trace

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

# Boot Sequence Topology

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

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
| 2 | `GlobalSignals` | prewarm source-observed native signal queues: R43-corrected static scan sees `73` direct `CreateQueue(...)` slots, `135` typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`, `271` `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs`, `116` `NativeQueue<...>` refs, and `1328` broader script-level `SignalBus<T>` typed-lane matches. Rerun command, timestamp, and artifact before exact-count use | no listener callbacks during publish |
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

No presentation system can be treated as authoritative state. Presentation consumes cached injected service interfaces, typed `SignalBus<T>` snapshots, documented `GlobalSignals` bridge snapshots, or render snapshots. It must not hot-read `GlobalRegistry` slots during presentation updates.

## Current Discrepancies

| Claim | Code Truth |
|---|---|
| "Single clean bootstrap sovereign" | false; bootstrap authority still spans `GameBootstrapper`, `BootstrapEvents`, `SceneGuard`, and legacy owner surfaces; no first-party `SceneBootstrap.cs` exists in the current source scan |
| "FileStream everywhere" | false for Data Monolith; `H8StaticDataArena` uses MMF-first desktop reads, Android/Quest StreamingAssets URI staging to cache, then direct `FileStream` into Vault-owned bytes. Managed whole-file runtime staging is not the Data Monolith route. |
| "Five artery event bus" | stale; R43-corrected static source scan sees `73` direct native queue slots, `135` typed `SignalBus<T>` lanes inside `GlobalSignals.cs`, `271` `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs`, `116` `NativeQueue<...>` refs, and `1328` broader script-level `SignalBus<T>` typed-lane matches; rerun before exact use |

## Verification Required

Still pending:

- Unity import
- clean Console readback
- Play Mode boot pass
- frame-time proof for service initialization
- no post-boot managed allocation trace

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING

# Procedural Wreckage Assembler - SHINOBU_121

Date: 2026-05-19
Status: STATIC SOURCE / UNITY IMPORT PENDING

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

## Scope

`Hecton8.World.ProceduralWreckage` is the new data-only wreck generation path for Echelon 2 World. It does not instantiate GameObjects, does not build MeshColliders, and does not mutate sibling domains. Legacy `ProceduralWreckGenerator.cs` remains on disk for compatibility until an integrator migrates existing references.

## Vault Route

Owner: `SystemID.WorldStreaming`

Buffers:

- `70840` `Rules`
- `70841` `Grid`
- `70842` `Nodes`
- `70843` `DebrisNodes`
- `70844` `RenderMatrices`
- `70845` `IndirectArgs`
- `70846` `SectorTriggers`
- `70847` `LootRequests`
- `70848` `CollisionProxies`
- `70849` `TelemetryRing`
- `70850` `TelemetryCursor`
- `70851` `Tuning`
- `70852` `CsvScratch`
- `70853` `Counters`
- `70854` `DebugCells`
- `70855` `GpuScalars`
- `70856` `SelfAudit`
- `70857` `HzbTiles`

## Job Graph

`MockSectorTriggerJob` feeds a deterministic sector trigger. `WreckageCollapseJob` solves a bounded WFC grid. `ApplyStructuralShearJob` applies deterministic torsion and deletions. `GenerateDebrisFieldJob` uses a curl-noise Dear Lie for scrap. `InjectLootRequestsJob` and `StageCollisionProxiesJob` write data requests only. `ExtractRenderMatricesJob` subtracts camera AUP before float conversion, applies quality culling and optional HZB rejection, then writes indirect args. `WreckageSelfAuditJob` records overlap and open-hull flags.

## Layout Contract

Primary DTO: `WreckageNodeDTO`, explicit size 128 bytes. Offsets: `LocalMatrix` 0, `PrefabHash` 64, `StateFlags` 68, `SectorAUP` 72, `BoundsExtents` 96, `BoundsRadius` 108, `SectorHash` 112, `ModuleId` 116, `GraphDegree` 120, `StableId` 124. `WreckagePaddedCounterDTO` is 64 bytes to avoid false sharing.

## Dear Lie

Debris is not simulated. The system scatters scrap matrices with deterministic 2D curl noise around the wreck root and lets shaders consume quality/rust/silt scalars. Collision truth is coarse box DTO staging only.

## Verification Pending

Unity import, Burst compile, Play Mode, Frame Debugger, GCMonitor, and profiler captures are still pending. A local build was not launched while CPU load exceeded the project build gate.

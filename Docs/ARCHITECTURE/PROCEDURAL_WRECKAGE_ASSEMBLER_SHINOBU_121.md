# Procedural Wreckage Assembler - SHINOBU_121

Date: 2026-05-19

Status: STATIC SOURCE / UNITY IMPORT PENDING

Source anchors: `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs`, `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs`, `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageContracts.cs`.

## Scope

`Hecton8.World.ProceduralWreckage` is the data-only wreck generation path for Echelon 2 World.

It does not instantiate GameObjects, build MeshColliders, or mutate sibling domains.

Legacy `ProceduralWreckGenerator.cs` remains for compatibility until integrator migration.

## Vault Route

Owner: `SystemID.WorldStreaming`

Route-card proof: `Docs/ARCHITECTURE/PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md`. Current disposition is `YELLOW` because static source exists but Unity import, Burst compile, GCMonitor, Frame Debugger, and player proof are absent.

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

- `MockSectorTriggerJob` feeds a deterministic sector trigger.
- `WreckageCollapseJob` solves a bounded WFC grid.
- `ApplyStructuralShearJob` applies deterministic torsion and deletions.
- `GenerateDebrisFieldJob` uses a curl-noise Dear Lie for scrap.
- `InjectLootRequestsJob` and `StageCollisionProxiesJob` write data requests only.
- `ExtractRenderMatricesJob` subtracts camera AUP before float conversion, applies quality culling and optional HZB rejection, then writes indirect args.
- `WreckageSelfAuditJob` records overlap and open-hull flags.

## Layout Contract

Primary DTO: `WreckageNodeDTO`, explicit size 128 bytes.

Offsets: `LocalMatrix@0`, `PrefabHash@64`, `StateFlags@68`, `SectorAUP@72`, `BoundsExtents@96`, `BoundsRadius@108`, `SectorHash@112`, `ModuleId@116`, `GraphDegree@120`, `StableId@124`.

`WreckagePaddedCounterDTO` is 64 bytes to avoid false sharing.

## Dear Lie

Debris is not simulated. Deterministic 2D curl noise scatters scrap matrices; shaders consume quality/rust/silt scalars. Collision truth is coarse box DTO staging.

## Binary Rule ABI

- Optional `wreckage_module_rules.h8bin` is cold-loaded only.
- Header is 16 bytes: `RuleBinaryMagic` (`H8WR` little-endian read value `0x52573848`), endian marker `0x01020304`, version, and declared rule count.
- Each row is a 64-byte `WreckageRuleDTO`-compatible record parsed field-by-field with endian swapping through `math.reversebytes`; records are copied into aligned runtime DTOs rather than `Pack=1` file structs.
- If the binary is absent or invalid, deterministic mock rules remain active.

## Verification Pending

Unity import, Burst compile, Play Mode, Frame Debugger, GCMonitor, and profiler captures are still pending. A local build was not launched while CPU load exceeded the project build gate.

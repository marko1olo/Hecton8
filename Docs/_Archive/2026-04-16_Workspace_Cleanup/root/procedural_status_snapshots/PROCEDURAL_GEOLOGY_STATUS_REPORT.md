Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Procedural Geology Status Report

- Root: `Assets/_Project/Data/World/ProceduralFamilies`
- Scope: geological procedural families only (`Rock`, `RockCluster`, `RockArch`, `RockShelf`, `CaveEntrance`, `Landmark`).
- Explicit profile: `WorldPrefabFamilyProfile.generativeGeologyProfile` assigned.
- Emergency fallback: geological behavior inferred from domain without explicit geology profile.
- Status remains `PENDING VERIFICATION` until runtime/seam/profiler evidence exists.

## Summary

- Geological families: `5`
- Families with real finals: `5`
- Placeholder-only families: `0`
- Explicit geology profiles: `3`
- Emergency fallback families: `0`
- Real-final families without missing large-form LODGroup: `5`

## Family Table

| Family | Domain | Streaming | Variants | Proxy | Real Finals | Placeholder Finals | Explicit Profile | Profile Enabled | Profile LOD | Max Renderers | Max LODGroups | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.cave.entrance | CaveEntrance | TerrainLod | 3 | 2 | 1 | 0 | yes | yes | 3 [0.62/0.24/0.08] | 7 | 1 | profile:geology.cave.entrance, mode:HeuristicSdfFallback, shape:CaveBridge |
| family.landmark.spire | Landmark | TerrainLod | 3 | 2 | 1 | 0 | yes | yes | 3 [0.58/0.22/0.07] | 6 | 1 | profile:geology.landmark.spire, mode:HeuristicSdfFallback, shape:ComplexRock |
| family.rock.arch.large | RockArch | TerrainLod | 3 | 2 | 1 | 0 | yes | yes | 3 [0.65/0.28/0.08] | 6 | 1 | profile:geology.rock.arch.large, mode:HeuristicSdfFallback, shape:Arch |
| family.rock.cluster.medium | RockCluster | TerrainLod | 6 | 3 | 3 | 0 | no | no | - | 1 | 0 | ok |
| family.rock.small_floor | Rock | TerrainLod | 5 | 3 | 2 | 0 | no | no | - | 1 | 0 | ok |

## Readiness Notes

- Real-final geology baseline: `family.cave.entrance`, `family.landmark.spire`, `family.rock.arch.large`, `family.rock.cluster.medium`, `family.rock.small_floor`
- Placeholder-driven geology families: `none`
- Large geological silhouettes should converge on explicit geology profiles plus real-final prefabs with LODGroup support.

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Procedural Organic Misc Status Report

- Root: `Assets/_Project/Data/World/ProceduralFamilies`
- Scope: organic procedural families outside the main kelp/coral baked pipeline (`Egg`, `Plant`).
- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.
- Managed materials must live under `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc`.
- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.

## Summary

- Organic misc families: `2`
- Families with real finals: `2`
- Placeholder-only families: `0`
- Families with managed organic material stack: `2`

## Family Table

| Family | Domain | Streaming | Real Finals | Placeholder Finals | Max Renderers | Max LODGroups | Managed Organic Material Stack | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.egg.cluster | Egg | Fauna | 1 | 0 | 11 | 1 | yes | ok |
| family.plant.giant | Plant | Flora | 1 | 0 | 12 | 1 | yes | ok |

## Readiness Notes

- Real-final organic misc baseline: `family.egg.cluster`, `family.plant.giant`
- Placeholder-driven organic misc families: `none`
- This path currently enforces mesh/material/LOD discipline only. Authored texture and custom flora shader coverage are still separate decisions.

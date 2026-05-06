Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Procedural Structural Status Report

- Root: `Assets/_Project/Data/World/ProceduralFamilies`
- Scope: structural procedural families only (`Debris`, `RuinModule`, `PowerRoute`, `ServiceScar`).
- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.
- Placeholder finals: `WorldProceduralPlaceholderAuthoring` output still standing in for missing structure content.
- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.

## Summary

- Structural families: `7`
- Families with real finals: `7`
- Placeholder-only families: `0`
- Debris families: `2`
- Ruin families: `3`
- Service/power families: `2`
- Families with managed structural material stack: `7`

## Family Table

| Family | Domain | Streaming | Variants | Proxy | Real Finals | Placeholder Finals | Max Renderers | Max Material Slots | Managed Material Stack | Max LODGroups | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.debris.field | Debris | Debris | 3 | 2 | 1 | 0 | 6 | 6 | yes | 0 | ok |
| family.debris.scatter | Debris | Debris | 3 | 2 | 1 | 0 | 4 | 4 | yes | 0 | ok |
| family.route.power | PowerRoute | Construction | 4 | 2 | 2 | 0 | 1 | 1 | yes | 0 | ok |
| family.ruin.cluster.medium | RuinModule | Construction | 3 | 2 | 1 | 0 | 11 | 11 | yes | 1 | ok |
| family.ruin.megastructure | RuinModule | Construction | 4 | 3 | 1 | 0 | 15 | 15 | yes | 1 | ok |
| family.ruin.module.single | RuinModule | Construction | 4 | 2 | 2 | 0 | 9 | 9 | yes | 1 | ok |
| family.service.scar | ServiceScar | Construction | 3 | 2 | 1 | 0 | 1 | 1 | yes | 0 | ok |

## Readiness Notes

- Real-final structural baseline: `family.debris.field`, `family.debris.scatter`, `family.route.power`, `family.ruin.cluster.medium`, `family.ruin.megastructure`, `family.ruin.module.single`, `family.service.scar`
- Placeholder-driven families: `none`
- Structural validator now checks managed opaque material stack plus required ruin LOD gates. Dedicated structural texture-source rules are still absent.

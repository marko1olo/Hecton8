**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Procedural World Support Status Report

- Root: `Assets/_Project/Data/World/ProceduralFamilies`
- Scope: support procedural families only (`ResourcePocket`, `HazardPocket`, `SafePocket`, `CreatureSpawn`).
- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.
- Large-threat zones are support families with `contributesLargeThreatZone=true`.
- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.

## Summary

- Support families: `9`
- Families with real finals: `9`
- Placeholder-only families: `0`
- Large-threat zone families: `4`
- Families with managed support material stack: `9`

## Family Table

| Family | Domain | Streaming | Large Threat Zone | Real Finals | Placeholder Finals | Max Renderers | Max LODGroups | Managed Support Material Stack | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.creature.spawn.passive | CreatureSpawn | Fauna | no | 1 | 0 | 8 | 1 | yes | ok |
| family.creature.spawn.predator | CreatureSpawn | Fauna | no | 1 | 0 | 9 | 1 | yes | ok |
| family.creature.zone.abyss_apex | CreatureSpawn | LargeThreats | yes | 1 | 0 | 9 | 1 | yes | ok |
| family.creature.zone.large_threat | CreatureSpawn | LargeThreats | yes | 1 | 0 | 9 | 1 | yes | ok |
| family.creature.zone.reef_apex | CreatureSpawn | LargeThreats | yes | 1 | 0 | 9 | 1 | yes | ok |
| family.creature.zone.ruin_apex | CreatureSpawn | LargeThreats | yes | 1 | 0 | 9 | 1 | yes | ok |
| family.pocket.hazard | HazardPocket | Construction | no | 1 | 0 | 9 | 1 | yes | ok |
| family.pocket.resource | ResourcePocket | Resources | no | 1 | 0 | 9 | 1 | yes | ok |
| family.pocket.safe | SafePocket | Construction | no | 1 | 0 | 8 | 1 | yes | ok |

## Readiness Notes

- Real-final support baseline: `family.creature.spawn.passive`, `family.creature.spawn.predator`, `family.creature.zone.abyss_apex`, `family.creature.zone.large_threat`, `family.creature.zone.reef_apex`, `family.creature.zone.ruin_apex`, `family.pocket.hazard`, `family.pocket.resource`, `family.pocket.safe`
- Placeholder-driven support families: `none`
- Support validator now checks routing, managed support materials, and LOD coverage for large-threat ownership zones.

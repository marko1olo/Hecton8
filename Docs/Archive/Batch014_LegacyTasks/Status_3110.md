# Status 3110 - Lore / World Consistency

Status: `STATIC ROUTE DECISION / PENDING UNITY PROOF`

First 20 Minutes moment: damaged safe anchor -> bright photic exit -> swim/return -> O2/depth/pressure -> starter resource -> craft/repair/build -> save/load restore.

Route impact: removes the narrow Copper-only assumption from 3110 route ownership and records the preferred static reroute: `Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal`.

Proof required: Unity scene placement, Play Mode route, Console, GC/profiler, screenshot/clip, inventory/craft signal evidence, save/load diff.

Parked work rejected: lore walls, text-only evidence, Drill-gate weakening, and runtime-ready claims from static docs.

## Done

- Loaded required authority docs and 3110 Batch31 evidence.
- Loaded relevant mandates: quest state graph, O2/pressure survival, resource layout, save persistence, evidence reporting, SignalBus lanes, zero-GC.
- Rechecked static assets/source for FiberKelp/FiberMesh/PressureSeal and CopperVein/Drill blocker.
- Recorded object briefs and route evidence requirements in `Docs/Reports/Batch31/3110_LORE_WORLD_CONSISTENCY_OWNER.md`.

## Current Verdict

- Copper data remains coherent but first-route copper is not reachable as V0 proof unless a real starter seafloor drill route is authored.
- CopperVein must stay Drill-gated; `ContentSanityValidator` explicitly rejects Knife/Any weakening.
- Preferred 3110 reroute is FiberKelp -> FiberMesh -> PressureSeal because FiberKelp is shallow, has a pickup prefab, uses `requiredToolClass: 0`, and `FirstHourDirector` already accepts `Comp_PressureSeal` as a first craft result.
- PressureSeal route is still static only. Membrane/resin placement, fabricator reachability, seal application target, quest flag, and save/load restore need runtime proof.

## Blocked / Pending

- Scene proof absent.
- Boot route conflict remains: root flow excludes `01_ORBIT`; first-20 contract includes it.
- Runtime quest activation/completion proof absent.
- Native-final localization proof absent.
- Surface/water/Aegir visual failures cannot be fixed by lore text.

## Next

- Author or verify route placement for FiberKelp stand, membrane tissue, hydrocarbon resin, fabricator, and pressure-seal target.
- Bind PressureSeal to a visible repair/build improvement: P-63 hatch ring, pump seal, bathy-drop safe-anchor seal, or service buoy casing.
- Add/verify route state IDs for harvested resource, crafted seal, applied repair, opened/safer return path, and save/load restore.

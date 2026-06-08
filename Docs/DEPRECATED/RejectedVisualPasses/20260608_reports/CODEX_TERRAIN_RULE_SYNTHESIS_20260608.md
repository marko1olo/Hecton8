# Codex Terrain Rule Synthesis 2026-06-08

Status: STATIC_RULE_SYNTHESIS_FOR_TERRAIN_PASS
Evidence class: READ_DOCS_PLUS_AUTHORING_PLAN

## Sources Read

- `terrain.md`
- `world.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `Assets/_Project/Scripts/WORLD_GENERATIVE_GEOLOGY_PIPELINE.md`
- `Docs/ARCHITECTURE/OFFLINE_GEOLOGY_MESH_BAKER.md`
- `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`
- `Docs/Lore/AppliedContent/in_game_wiki/en_US/P018_HECTON8_DROWNED_GEOLOGY.md`
- `Docs/Lore/AppliedContent/in_game_wiki/en_US/P092_GLOBAL_OCEAN_DEPTH_BANDS.md`
- `Docs/Lore/AppliedContent/in_game_wiki/en_US/P093_ACCESSIBLE_SEAFLOOR_WINDOWS.md`
- `Docs/Lore/AppliedContent/in_game_wiki/en_US/P094_SEED_GEOLOGY_INVARIANTS.md`
- `Docs/Orchestration/MAPMAGIC_HYDRAULIC_EROSION_JOB_SAFETY_STATIC_REVIEW_20260606.md`

## Terrain Target

The immediate target is a procedural first photic-shelf terrain baseline, not a
water/sky/Aegir beauty pass.

Required terrain read:

- macro route: shoreline ledge, collapsed shelf ramp, S-shaped canyon funnel,
  exposed ridges, and deeper pressure window;
- meso geology: fractured route ridges, ledge shelves, waterline strata cuts,
  sediment/collapse pockets;
- micro contract: vertex colors and existing photic terrain materials carry
  chip/mineral reveal, wetness, cavity AO, and sediment/material blend;
- proof angle: gameplay-height route view must show terrain decisions, not only
  a top-down beauty shot.

## Rejections

Reject if the pass reads as:

- flat pool floor;
- square-map plate;
- smooth Perlin hills;
- random rock scatter;
- decorative cliffs with no route function;
- dense flora/rocks used to hide terrain failure;
- diagnostic MapMagic bypass with erosion/anomaly disabled;
- proof hidden behind fog, bloom, dark grading, or cropped camera angles.

## MapMagic Boundary

Current MapMagic production terrain proof remains absent. The old diagnostic
h8_1914/h8_1916/h8_1917/h8_1918 lanes are rejected because they relied on
disabled or unproven graph routes, memory-leak payloads, and visually failed
terrain. This terrain pass is therefore editor-authored deterministic procedural
mesh output with a manifest; it does not claim production MapMagic generation.

## Authoring Implementation

`Assets/_Project/Scripts/Editor/CodexTerrainRouteAuthoring.cs` is the current
terrain-only authoring script.

It should:

- deprecate rejected recent Codex scene roots;
- create `H8_CODEX_TERRAIN_PROCEDURAL_ROUTE_20260608`;
- generate a non-rectangular photic shelf/canyon mesh;
- generate separate fractured ridges, collapsed shelf ledges, and waterline
  strata cut faces;
- save a separate low-poly collision proxy mesh;
- write `Docs/AgentLogs/CODEX_TERRAIN_PROCEDURAL_ROUTE_20260608.txt`;
- capture macro, gameplay-height, shoreline, canyon cross-section, and top-down
  proof images.

## Next Gate

Run Unity only when process gates are green. Active `dotnet`, `csc`,
`MSBuild`, Unity compile/import, or CPU above project threshold blocks batch
execution.

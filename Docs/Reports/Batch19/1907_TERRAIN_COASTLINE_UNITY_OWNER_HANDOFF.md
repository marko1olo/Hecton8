# 1907 Terrain Coastline Unity Owner Handoff

Evidence class: STATIC_DOC
Unity/build/runtime/profiler: NOT RUN
Scope: future Unity-owner tasks only. This file is not a permission to edit while Unity/build/import ownership is contested.

## Gate Before Work

Future Unity owner must confirm:

- No other Unity owner, import, player build, dotnet build, or profiler capture is active.
- No `Assets/**` write is performed from this 1907 packet.
- Third-party packages remain read-only: `Assets/Crest`, `Assets/MapMagic`, `Assets/GPUInstancer`, `Assets/MeshBaker`, `Assets/SciFiFacility`.
- Normal surface/coast/photic work remains bright, readable, and premium. Darkness, storm, fog, silt, bloom, crushed exposure, or UI overlays cannot be used as proof.

## Future Task Order

1. Inspect active scene objects in `02_HECTON_WORLD`:
   - `H8_SURFACE_COASTAL_ISLAND_1428`
   - `H8_SURFACE_SHORE_FOAM_1428`
   - `SURFACE_FOAM_RIBBON_1428_0..17`
   - `H8_SURFACE_OCEAN_READ_1428`
2. Verify current renderer/material slots before editing.
3. Generate or import missing first-party source outputs only through an uncontested editor slot:
   - `TX_H8_ShorelineFoamRibbonPacked_*`
   - `TX_H8_ShorelineWaterlineMask_*`
   - `TX_H8_WetDryBasaltMask_*`
   - `TX_BiomeWeightMap_SHORELINE_*`
   - shoreline caustic flipbook/cookie/waterline mask where justified
4. Bind generated source only to first-party materials or approved duplicated first-party material assets. Do not mutate Crest materials/textures/shaders.
5. Fill or reject empty slots:
   - `MAT_H8_SurfaceFoamRibbons_1428`: `_BaseMap`, `_MainTex`
   - `MAT_H8TerrainLit_BasaltSediment_1428`: `_Control`, `_Mask0-3`
   - wet basalt secondary/detail/wetness slots if claimed
6. Verify shader channel contract:
   - Foam packed RGB: R long strand, G secondary/cross-flow, B lace breakup, A reserved/confidence.
   - Waterline mask: R contact foam, G wet edge, B sediment/salt, A caustic receiver/confidence.
   - Wet/dry basalt: R wetness, G drying falloff, B mineral breakup, A specular boost.
   - Biome weight: R rock, G sand, B silt, A erosion/deposited silt.
7. Capture proof. Use exact route angles below.
8. Produce profiler/GC/Frame Debugger/RenderGraph/memory notes only after actual Unity execution.

## Required Visual Proof Angles

| Angle | Required read |
|---|---|
| Glancing water | Ocean specular, long swell normals, foam strands, sky/Aegir reflection, coast silhouette. |
| Vertical waterline | Wet/dry gradient, contact foam, salt/sediment band, readable rock/water edge. |
| Close wet rock | Basalt roughness, normal detail, mineral strata, cavity sediment, wet sheen without glossy overlay. |
| Wide coast | Ocean, coastline, sky/Aegir/moons/clouds, route cue, return landmark, no dark cover-up. |
| Underwater edge | Surface underside, 0-20 m photic entry, caustic hints if justified, return route visible. |
| Shallow transition | Seabed sediment, coral rubble/biota, water color, route readability, instrument legibility. |
| Same-camera quality comparison | Compact, Middle, High, Ultra; Compact remains attractive and readable. |

## Compact Middle High Ultra Consequences

Compact:

- Lower texture/mask resolution and fewer active foam lanes.
- Preserve readable coastline silhouette, white/cyan foam identity, wet basalt material identity, wet/dry line, photic brightness, and return route.
- Reject black water, flat foam, muddy waterline, blurry basalt, or UI-only readability.

Middle:

- Increase mask resolution, add moderate foam breakup, richer salt/sediment bands, and stronger wet/dry gradient.
- Keep terrain/traversal/material authority unchanged.

High:

- Add richer basalt detail masks, stronger foam lace, better shallow caustic hints, and longer material detail residency after proof.
- Any render/runtime change needs profiler and Frame Debugger evidence.

Ultra:

- Add sensory density: dense foam lace, stronger glancing specular, richer caustic edge, high-detail wet basalt strata, and Aegir/ocean reference richness.
- Do not alter gameplay truth, save identity, terrain route, DTO layout, or third-party package ownership.

## Rejection Gates

Reject future proof if:

- Static path existence is claimed as visual proof.
- Foam/ribbon objects remain inactive while visible foam is claimed.
- `MAT_H8_SurfaceFoamRibbons_1428` has empty source texture slots.
- Terrain `_Control` or packed masks are empty while terrain blend quality is claimed.
- Foam is flat, opaque, uniform, card-like, or scale-less.
- Wet basalt is a generic glossy overlay instead of material-specific wet rock.
- Surface/coast/photic route is darkened, fogged, stormed, bloomed, or cropped to hide weak art.
- Third-party package materials/textures/shaders are mutated.
- Compact lane looks flat, muddy, blurry, primitive, or below the Subnautica-level floor.

## Required Final Evidence For Future Owner

- Screenshot/capture paths.
- Scene, camera route, and quality tier.
- Console state.
- Frame Debugger or RenderGraph proof.
- Unity Profiler and GC evidence if runtime/render path was exercised or changed.
- Memory/VRAM notes for new masks/flipbooks/textures/control maps.
- Explicit unresolved issues list.

Valid final states for the future Unity owner:

- `PENDING UNITY OWNER`
- `BLOCKED BY SPECIFIC UNITY EVIDENCE`
- `RUNTIME PROOF PASS WITH CURRENT ARTIFACTS`

This 1907 handoff remains `PENDING UNITY OWNER`.

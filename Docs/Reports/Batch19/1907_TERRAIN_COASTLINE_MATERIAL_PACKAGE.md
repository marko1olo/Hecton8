# 1907 Terrain Coastline Material Package

ID: 1907
Role: TERRAIN_COASTLINE_MATERIAL_PACKAGE_PREP
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime/profiler: NOT RUN
Final state: STATIC PACKAGE PREP COMPLETE / VISUAL PROOF PENDING UNITY OWNER

## Boundary

This packet prepares source roles, gap lists, prompt requests, and future Unity-owner instructions for surface terrain, coastline, wet basalt, shoreline foam, waterline masks, and photic-shallow transitions.

No `Assets/**` file was written. No Unity Editor, Unity MCP, dotnet build, import, material edit, shader edit, terrain layer edit, scene edit, prefab edit, or Data Monolith action was performed.

Static evidence proves path existence, serialized YAML/material fields, old screenshot report observations, and source-package gaps only. It does not prove active visual quality, import health, scene binding, frame time, GC, VRAM, RenderGraph, or gameplay acceptance.

## First-20-Minutes Route Blocker Removed

This packet reduces source-package debt for the first semi-open surface/photic exit: grey procedural-looking coastline, sparse/inactive waterline foam, missing wet/dry masks, incomplete shoreline terrain channels, and missing photic transition source requests. It does not fix the route in Unity.

## Authority And Mandates

Read for this packet:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `terrain.md`
- `world.md`
- `water.md`
- `rendering.md`
- `performance.md`
- `quality.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`
- `Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC.md`
- `Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_BAKE_INPUTS.csv`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1901_SHALLOW_PROOF_ARTIFACT_PRIORITY_RUNBOOK.md`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: terrain/coastline/waterline material source package prep.

## Static Evidence Summary

| Item | Static result | Package decision |
|---|---|---|
| `H8_SURFACE_COASTAL_ISLAND_1428` | Present in `02_HECTON_WORLD.unity`; `m_IsActive: 1` by static YAML | Coastline mesh route exists but visual/material proof is pending Unity owner. |
| `H8_SURFACE_SHORE_FOAM_1428` | Present; `m_IsActive: 0` | Foam material may exist, but visible shoreline foam is not proven. |
| `SURFACE_FOAM_RIBBON_1428_0..17` | Present; sampled entries `m_IsActive: 0` and Batch18 reported all inactive | Foam ribbon route cannot be claimed active. Source package must request real packed ribbon masks before Unity slot. |
| `H8_SURFACE_OCEAN_READ_1428` | Present; `m_IsActive: 0` | Long-swell/read-card route remains pending Unity owner. |
| `MAT_H8SurfaceShoreFoam_1428` | `_BaseMap` and `_MainTex` point to `Assets/_Project/Art/TEXTURES/foam.png` | Static candidate only. Needs active renderer and waterline capture. |
| `MAT_H8_SurfaceFoamRibbons_1428` | `_BaseMap` and `_MainTex` are empty | Reject as final until `TX_H8_ShorelineFoamRibbonPacked_*` or equivalent is produced and bound. |
| `MAT_H8SurfaceWetBasaltReal_1428` | Base and normal refs exist; several secondary texture slots remain empty | Primary wet basalt candidate, still missing full waterline/wetness/mask package. |
| `MAT_SurfaceIslandWetBasalt_1428` | Base texture exists; `_BumpMap` is empty | Secondary/weaker candidate only. |
| `MAT_H8TerrainLit_BasaltSediment_1428` | Current static scan shows `_Splat0-3` and `_Normal0-3` refs, but `_Control` and `_Mask0-3` are empty | Batch18 empty-layer debt is partially changed; control and packed mask debt remains. No terrain proof. |
| `TX_H8_WetBasaltShoreline_Albedo_1428.png` | Present with Gemini manifest and tile2x2 QA JPG | Albedo-only source candidate. Normal/MRAO/wetness family still pending. |
| 1821 generated shoreline outputs | `TX_H8_ShorelineFoamRibbonPacked_1821.png`, waterline mask, wet/dry mask, biome weight map, caustic outputs are missing | Marked `PENDING SOURCE`. |
| Rejected placeholders | `WorldRuntime/ProceduralPlaceholders` and `bubble vent atlas - bad - redo.png` remain rejected references | Not allowed in product-face coastline package. |

## Material Role Package

Machine-readable rows are in:

`Docs/Reports/Batch19/1907_TERRAIN_COASTLINE_MATERIAL_PACKAGE.csv`

Required source roles:

| Domain | Required roles | Current package state |
|---|---|---|
| Shoreline foam | Albedo/source shape, foam opacity mask, wet edge breakup, sediment contamination mask, optional caustic interference mask | `foam.png` is a static candidate. Packed ribbon/waterline masks are missing. Scene foam/ribbons inactive. |
| Wet basalt | Albedo, normal/height source, roughness/wetness logic, AO crevice logic, salt/mineral mask, waterline transition mask | Existing wet basalt material is a candidate. New Gemini albedo exists but lacks normal/MRAO/wetness family. |
| Basalt sediment/black sand | Albedo, roughness, normal/height, shell/mineral flecks, slope/traversal readability | Terrain material has splat/normal refs but empty control and mask channels. Black sand/shell source remains pending. |
| Waterline grime | Vertical gradient mask, tide stain, foam residue, algae/salt bands, non-dark surface readability | Pending source. Must not be simulated or replaced by dark grading. |
| Biome weight | Rock/sand/silt/erosion channel map | Pending generated control map through future editor bake. |
| Caustic mask | Shallow/waterline mask, flipbook, light cookie where physically justified | Pending source; optional presentation only. |
| Shallow transition | Seabed sediment, coral rubble, photic caustic mask, wet/dry blend, route readability | Coral texture candidates exist; transition package still pending source and Unity placement proof. |
| Aegir/ocean reference | Reference panel for sky/ocean/coast color and scale | Static candidates exist. Reference-only, not import proof. |

## Visual Fake Decisions

Use source textures, masks, flipbooks, and shader blends before simulation:

- Shoreline foam: packed RGB mask and ribbon geometry. No droplet/particle shoreline truth as default.
- Wet basalt waterline: wet/dry mask, roughness/specular response, cavity AO, salt/mineral bands. No runtime wetness painting for the normal coast route.
- Waterline grime: vertical gradient/tide stain mask. No dark post grade, fog, storm, or silt cover-up.
- Caustics: baked flipbook/cookie/mask only where shallow water and light justify it. No global caustics over abyss or shoreline as visual camouflage.
- Terrain blend: biome weight map and terrain layer masks. No runtime terrain material generation in gameplay.
- Shallow transition: source textures and authored placement manifests. No generic blue fog transition and no random coral carpet.

## Continuous Quality Consequences

`GlobalQualityWeight` is continuous. The values below describe consequences, not binary switches.

| Lane | Consequence |
|---|---|
| Compact, near 0.0 | 512 to 1024 source targets where appropriate, sparse but readable foam, strong coast silhouette, wet/dry line, basalt material identity, bright photic color, shared masks/atlases, no muddy fallback. |
| Middle, around 0.35 | More mask resolution, moderate foam lace, stronger sediment/salt bands, clearer roughness and normal variation, photic transition source with coral rubble and route cues. |
| High, around 0.7 | Richer wet basalt strata, denser foam breakup, improved caustic hints, more precise biome weights and seabed material variation after proof. |
| Ultra, near 1.0 | Visual overkill only: dense foam lace, high-detail wetness/mineral masks, strong shallow caustic/source richness, Aegir/ocean reference fidelity. No gameplay truth, terrain authority, save identity, or material route changes. |

## Acceptance Gates For Future Owner

This packet is not enough for visual acceptance. Future owner must provide:

- Current Unity screenshots: glancing water, vertical waterline, close wet rock, wide coast, underwater edge, 5-20 m photic transition, same-camera Compact/Middle/High/Ultra.
- Frame Debugger/RenderGraph proof for active ocean, foam, wet basalt, terrain material, caustic, sky/Aegir/moon, and relevant UI passes.
- Unity Profiler/GC proof if any runtime or render feature is changed.
- Memory/VRAM notes for generated textures, masks, caustic flipbooks, terrain control maps, and Crest/ocean textures.
- Material slot proof: no empty `_BaseMap`, `_MainTex`, terrain `_Control`, or packed mask slots in claimed final routes.
- Rejection check against flat foam, glossy overlay basalt, muddy/noir shoreline, dark surface cover-up, inactive object claims, third-party package mutation, and static-only proof upgrades.

## Blockers

- Packed foam ribbon, waterline, wet/dry basalt, biome control, and caustic source outputs are missing.
- Moon/source gaps remain from 1883 and are relevant to wide coast/Aegir/ocean reference proof.
- Scene foam and foam ribbon objects are inactive by static YAML.
- Visual proof remains `PENDING UNITY OWNER`.

## Final Claim Guard

Claim: Static material/source package, CSV, prompt pack, and Unity-owner handoff exist for 1907.
Evidence Class: STATIC_DOC / STATIC_SOURCE
Artifact: files under `Docs/Reports/Batch19`, `Docs/GeneratedAssets/Gemini/Prompts/1907`, `Docs/Tasks/Status_1907.md`, and `Docs/AgentLogs`.
Command or Unity tool: PowerShell static reads and file writes under `Docs/**`.
Date: 2026-06-04
Residual risk: no Unity visual, import, binding, profiler, Frame Debugger, GC, memory, VRAM, or gameplay proof.

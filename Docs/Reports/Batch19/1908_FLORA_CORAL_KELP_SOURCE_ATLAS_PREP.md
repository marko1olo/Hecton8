# 1908 Flora Coral Kelp Source Atlas Prep

ID: 1908  
Evidence class: STATIC_SOURCE  
Runtime/editor/render/profiler proof: PENDING VERIFICATION  
Unity owner: NO  
Build owner: NO  
Scene owner: NO

## Scope Boundary

This packet prepares source atlas requirements, prompt packs, ecological placement rules, proof naming, and Unity-owner handoff for shallow flora, coral, kelp, and supporting geology families. It does not create Unity meshes, prefabs, materials, import settings, scene placement, render captures, profiler captures, or runtime proof.

First-20-minutes route blocker removed: source requirements are now defined for the bright photic exit/reef material families that must carry the first semi-open shallow route. This is static preparation only.

No Assets path was written by this agent. Any future package, import, material binding, atlas packing, prefab assignment, scene scatter, screenshot, collider proof, LOD proof, or profiler capture remains `PENDING UNITY OWNER`.

## Authority And Mandates

Authority read:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `rendering.md`
- `performance.md`
- `quality.md`
- `ecosystem.md`
- `terrain.md`
- `world.md`
- `water.md`
- `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md`
- `Docs/Reports/Batch18/1901_SHALLOW_PROOF_ARTIFACT_PRIORITY_RUNBOOK.md`

Mandates read:

- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Evidence Basis

Batch18 1858 and 1901 establish the current static debt:

- Coral families: `coral_branching`, `coral_brittle`, `coral_low`, `coral_massive`, `coral_plate`.
- Kelp families: `kelp_canopy`, `kelp_tall`, `kelp_patch_dense`; task-level alias `kelp_patch` remains source-required until the Unity owner confirms whether it maps to `kelp_patch_dense`.
- Geology support: `CaveEntrance_00`, `LandmarkSpire_00`, `RockArch_00`, `RockShelf_00`; direct report citations also include `RockCluster_00`, `RockFloor_00`, and BioForge shallow sample stems.
- Static proof cannot clear `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`.
- Static proof cannot prove LOD quality, collider safety, material import state, scene scatter, route composition, compact capture, high/ultra capture, or profiler state.

## Stem Lock

Primary source stems:

- CORAL: `coral_branching`, `coral_brittle`, `coral_low`, `coral_massive`, `coral_plate`.
- KELP: `kelp_canopy`, `kelp_patch`, `kelp_patch_dense`, `kelp_tall`.
- GEOLOGY_SUPPORT: `CaveEntrance_00`, `LandmarkSpire_00`, `RockArch_00`, `RockShelf_00`, `RockCluster_00`, `RockFloor_00`.
- SOURCE_PRESENT_STATIC candidates from Batch18: first-priority `GEN_family_*` baked flora stems, BioForge shallow samples, and WorldProceduralGeology LOD/COL filename evidence.
- PENDING SOURCE: actual atlas bitmap sources, source ledgers, source previews, channel derivation notes, and tile/mip QA.
- PENDING UNITY OWNER: import settings, material assignment, prefab route, LOD/collider proof, scene placement, screenshots, profiler, Frame Debugger, and runtime scatter proof.

The proof naming matrix is in `Docs/Reports/Batch19/1908_FLORA_CORAL_KELP_PROOF_NAMING_MATRIX.csv`.

## Atlas Groups

The atlas planning CSV is in `Docs/Reports/Batch19/1908_FLORA_CORAL_KELP_SOURCE_ATLAS_PREP.csv`.

### Coral Hard Calcified Surfaces

Applies to branching, brittle, plate rims, low calcified caps, and massive coral shell surfaces.

Source roles:

- albedo/source: calcified tissue color, pigment bands, old growth scars, sediment stains;
- height/normal source: pores, ridges, knuckles, branch collars, chipped rims;
- AO cavity source: branch intersections, underside plates, porous cups;
- roughness/wetness source: wet matte calcification with slick edge variance;
- emission/biolum source: sparse polyp/tip masks only where ecology demands it;
- thickness/readability source: rim thickness and branch radius masks;
- detail overlay source: micro pores and calcium grain, not noise wallpaper.

### Coral Soft Porous Surfaces

Applies to low beds, massive porous coral, tube coral source candidates, and soft polyps.

Rules:

- Porous identity must survive mip and compression.
- Emission is restrained, localized to organs/tips/cups, and compatible with vertex color G.
- Soft tissue cannot become candy gradients or neon flood.

### Kelp Blade Fiber Surfaces

Applies to kelp canopy, tall kelp, patch/patch-dense families, and future shallow blade variants.

Rules:

- Blade veins run root to tip.
- Edge wear, serration, scars, folds, and tears are source requirements.
- Dense fields use opaque/cutout/dither-friendly masks. Alpha-blend dependency is rejected on compact hardware.

### Kelp Holdfast Root Surfaces

Applies to holdfasts, stipes, root clusters, anchor pads, and substrate contact material.

Rules:

- Holdfasts are not optional decoration. They prove ecological attachment and placement logic.
- Root sockets need AO, wetness, sediment, and abrasion masks.
- Root vertices must remain low-sway in future vertex color R contracts.

### Shallow Rubble Geology Support

Applies to cave entrances, spires, arches, shelves, rock clusters, floor support, porous rock, rubble, coral fragments, wet strata, and mineral chips.

Rules:

- Geology support must show strata, chips, wetness, mineral logic, and waterline/sediment context.
- It supports coral/kelp placement by substrate logic, not random decorative scatter.
- Triplanar/atlas plans must preserve scale and route readability.

### Biolum And Detail Masks

Applies to coral biolum masks, kelp speckle masks, algae film, sediment overlays, AO/detail overlays, and edge/detail masks.

Rules:

- Bioluminescence is system information, not random glow.
- Emission masks must be sparse, biologically placed, and not gameplay truth.
- Detail overlays must survive 2x2 tile tests, mip preview, and channel independence QA.

## Ecological Placement Constraints

Static source art must support these future placement rules:

- Coral attaches to rock shelves, arch undersides, cave-mouth rims, stable rubble, and current-exposed hard substrate; it does not float in sand fields.
- Branching and brittle coral need flow-facing asymmetry, shelter pockets, and nonuniform cluster spacing.
- Plate coral prefers ledges, wall lips, and shelf terraces where underside AO and rim thickness read.
- Massive/low coral marks stable substrate, old reef beds, and shelter edges.
- Kelp requires holdfasts on rock, rubble, wreck hardware, or compacted substrate; it cannot appear as loose vertical ribbons growing from flat sand.
- Kelp canopy and tall kelp require readable current direction, root-to-tip blade flow, and return-path silhouette value.
- Algae film appears on wet limestone, basalt, wreck metal, and low-current sheltered surfaces; it is not a uniform green wash.
- Sand/sediment overlays gather in cavities, lee sides, branch bases, rubble pockets, and shelf foots.
- Industrial remnants may carry biofilm and holdfast attachment, but source prompts must not create signs, labels, logos, or brand-like markings.
- Empty zones are allowed only for route readability, predator pressure, current scour, sediment instability, or performance relief. They are not failure to author ecology.

## Rejection Gates

Reject source candidates if they show:

- primitive-looking coral/flora: cylinders, spheres, flat ribbons, generic fronds, cheap planes, or low-poly silhouettes;
- baked directional light, cast shadows, horizon, perspective object render, labels, text, UI, watermark, logo, or game-asset extraction;
- muddy grayscale, neon flood, random glowing speckles, cartoon/painterly/noir output, blurry output, low-poly style, or generic wallpaper repetition;
- alpha-blend dense-field dependency;
- texture detail used to hide missing organic topology;
- geology without strata, chips, wetness, mineral logic, or water/pressure history;
- material channels that cannot become albedo, height, AO, roughness/wetness, emission, thickness, or detail sources;
- any static text that claims Unity import, runtime, profiler, LOD, collider, or visual proof.

## Hardware Consequences

`GlobalQualityWeight` is continuous. Labels below are documentation checkpoints only.

Compact, near 0.0:

- fewer atlas variants;
- 512 source masters where family importance allows, 1024 for standard world materials that carry shallow route identity;
- minimum 8 px padding for 512 sources, edge bleed mandatory;
- tighter channel packing, shared detail overlays, dither/cutout-friendly masks;
- strong silhouette/source readability preserved; no muddy cheapness, no alpha-blend fields.

Middle, around 0.35:

- 1024 to 2048 source masters for key shallow materials;
- minimum 12 px padding for 1024 sources;
- better cavity/roughness separation, stronger local decals, richer material masks;
- more atlas groups only when streaming locality and material identity improve.

High, around 0.7:

- 2048 source masters for hero coral, kelp blades, and route geology;
- minimum 16 px padding for 2048 sources;
- richer normals, scar detail, wetness, cavity AO, and controlled biolum masks;
- longer LOD residency and denser near-field source detail remain future Unity-owner proof.

Ultra, near 1.0:

- 2048/4096 hero-only source masters;
- minimum 24 px padding for 4096 sources;
- visual overkill through stronger bakes, pore fields, vein fibers, chipped rims, mineral edges, and secondary detail overlays;
- gameplay truth, collider identity, harvest anchors, vertex color semantics, save identity, and authority route do not change.

## Future Unity Owner Only

Future Unity owner work, not performed here:

- import settings;
- texture compression, sRGB/linear/normal map setup, mip streaming, and edge bleed validation;
- atlas packing into Unity assets;
- material binding and shader channel validation;
- prefab/material assignment;
- scene scatter and ecological placement;
- LOD, HLOD, proxy, collider, and trigger proof;
- flat/clay preview, atlas preview, final material screenshot, compact screenshot, high/ultra screenshot;
- Frame Debugger, RenderGraph, profiler, GC, and memory/VRAM proof;
- player capture and route composition proof.

Future runtime owners also need black-box/telemetry only if they create critical runtime systems. This static source prep does not implement telemetry.

## Blockers

- Actual atlas bitmap sources are not present in this packet.
- Source ledgers are not present yet.
- Gemini prompt output is not generated by this packet.
- Unity import, material, prefab, scene scatter, visual, and profiler proof remain pending.
- `kelp_patch` versus `kelp_patch_dense` alias requires Unity-owner confirmation against actual package stems.

## Final State

STATIC SOURCE PREP COMPLETE. No Unity proof closed. No Assets path written.

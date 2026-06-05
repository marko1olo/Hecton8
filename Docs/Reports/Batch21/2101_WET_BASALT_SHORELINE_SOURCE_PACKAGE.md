# 2101 Wet Basalt Shoreline Source Package

Agent ID: 2101  
Role: WET_BASALT_SHORELINE_SOURCE_PACKAGE_AND_QA_GATE  
Batch: batch21_art_replacement_wave  
Date: 2026-06-04  
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW  
Runtime state: PENDING VERIFICATION

Unity, MCP, Play Mode, profiler, imports, builds, Assets edits, material edits, scene edits, prefab edits, package edits, shader edits, and runtime script edits were not run or performed.

## What Was Wrong

Surface/coast material readiness is statically rejected by existing reports. The supported shoreline blockers are:

- terrain/triplanar rock material source gaps;
- leak wet sheen and waterline wetness source gaps;
- surface rock material unresolved refs and missing ORM/detail route;
- primitive or grey shoreline geology proxy debt;
- wet/dry shoreline geology source debt;
- waterline foam/salt contact source debt.

This package removes only the static source-definition blocker. It does not prove generated images, PBR derivation, Unity import, material binding, route capture, Frame Debugger, profiler, GC, memory, or VRAM.

## Authorities Read

| Authority | State |
|---|---|
| `AGENTS.md` | READ |
| `TASTE.md` | READ |
| `VISION_LOCKS.md` | READ |
| `PROJECT_BIBLES.md` | READ |
| `quality.md` | READ |
| `PROCEDURAL_ASSET_PIPELINE.md` | READ |
| `3dmodel.md` | READ |
| `3DMODEL_GEOLOGY_ROCKS.md` | READ |
| `3DMODEL_TEXTURES_MATERIALS.md` | READ |
| `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` | READ |
| `world.md` | READ |
| `terrain.md` | READ |
| `water.md` | READ |
| `rendering.md` | READ |
| `shaders.md` | READ |
| `Docs/Reports/Batch20/2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md` | READ |
| `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md` | READ |
| `Docs/Reports/Batch20/2019_PROXY_DEBT_QUEUE.csv` | READ |
| `Docs/Reports/Batch20/2019_GENERATION_ROUTE_MATRIX.csv` | READ |
| `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` | READ |

Relevant mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`

`Docs/Actual Domains of Project.txt` was checked and absent. Narrow inferred domain: shoreline/coastline geology material source package, texture prompts, QA checklist, and future Unity-owner handoff.

## Support Path Discovery

| Requested support file | Discovery state | Path |
|---|---|---|
| `2016_TOP_BLOCKING_MATERIALS.csv` | VERIFIED | `Docs/Reports/Batch20/2016_TOP_BLOCKING_MATERIALS.csv` |
| `2016_REPLACEMENT_ACCEPTANCE_RUBRIC.csv` | VERIFIED | `Docs/Reports/Batch20/2016_REPLACEMENT_ACCEPTANCE_RUBRIC.csv` |
| `2005_GEOLOGY_SHORELINE_ROCK_SOURCE_PACKAGE.md` | VERIFIED | `Docs/Reports/Batch20/2005_GEOLOGY_SHORELINE_ROCK_SOURCE_PACKAGE.md` |
| `2005_TEXTURE_CHANNEL_CONTRACTS.csv` | VERIFIED | `Docs/Reports/Batch20/2005_TEXTURE_CHANNEL_CONTRACTS.csv` |
| `WET_BASALT_SEAMFIX_QA_CHECKLIST_20260604.md` | VERIFIED | `Docs/Reports/Batch20/WET_BASALT_SEAMFIX_QA_CHECKLIST_20260604.md` |
| `2022_gemini_texture_generation_queue.csv` | VERIFIED | `Docs/Reports/Batch21/2022_gemini_texture_generation_queue.csv` |

## Evidence Table

| Blocker | Evidence class | Report row/source | Static fact used |
|---|---|---|---|
| Terrain material source | STATIC_DOC | `2016_TOP_BLOCKING_MATERIALS.csv` row rank 4 | `terrain.mat` has unresolved terrain/rock albedo refs and empty normal/detail/mask roles. |
| Triplanar rock material | STATIC_DOC | `2016_TOP_BLOCKING_MATERIALS.csv` row rank 5 | `Mat_TriplanarRock.mat` has unresolved rock albedo and empty normal/detail/mask roles. |
| Wet sheen/waterline | STATIC_DOC | `2016_TOP_BLOCKING_MATERIALS.csv` row rank 6 | `Mat_LeakWetSheen.mat` has unresolved wetness normal refs and missing detail route. |
| Surface rock material refs | STATIC_DOC | `2016_TOP_BLOCKING_MATERIALS.csv` rows rank 7-8 | `mat_Rock2.mat` and `mat_Rock_Shared.mat` are surface blockers with unresolved base/normal/main/occlusion refs and no ORM/detail. |
| Terrain/triplanar source blocker | STATIC_DOC | `2019_PROXY_DEBT_QUEUE.csv` row `2019-Q005` | Wet basalt/dry basalt/triplanar material stack plus locked channel contract is missing. |
| Shoreline geology proxy | STATIC_DOC | `2019_PROXY_DEBT_QUEUE.csv` row `2019-Q011` | Shoreline rocks/cliffs are primitive/grey/wetness-missing and need source plus Unity-owner proof. |
| Wetness/waterline source | STATIC_DOC | `2019_PROXY_DEBT_QUEUE.csv` row `2019-Q016` | Wetness/waterline response is invalid or generic glossy overlay until masks/refs are authored. |
| Terrain and triplanar route | STATIC_DOC | `2019_GENERATION_ROUTE_MATRIX.csv` row `2019-G002` | Source is missing; reject unresolved albedo, missing normal/detail/mask, guessed channel order, grey diffuse-only output. |
| Wetness route | STATIC_DOC | `2019_GENERATION_ROUTE_MATRIX.csv` row `2019-G003` | Source is missing; reject generic glossy overlay and invalid normal refs. |
| Shore wet/dry outcrop | STATIC_DOC | `2019_GENERATION_ROUTE_MATRIX.csv` row `2019-G011` | Planned but not imported; reject no wet line, grey diffuse-only, primitive proxy. |
| Wet basalt texture target | STATIC_DOC | `2022_gemini_texture_generation_queue.csv` rank 1 / prompt ID `TX_B21_WetBasaltShoreline_Albedo_20260604` | Spend-first albedo source target for terrain/triplanar shoreline source. |
| Foam/salt texture target | STATIC_DOC | `2022_gemini_texture_generation_queue.csv` rank 2 / prompt ID `TX_B21_ShoreFoamSaltContact_Mask_20260604` | Spend-first RGBA mask source target for foam/contact/waterline fake. |
| Wet basalt channel contract | STATIC_DOC | `2005_TEXTURE_CHANNEL_CONTRACTS.csv` row `Wet basalt shoreline` | Requires base/albedo, normal, packed MRAO/wetness, detail maps, waterline mask, foam/salt residue mask. |
| Foam/salt contract | STATIC_DOC | `2005_TEXTURE_CHANNEL_CONTRACTS.csv` row `Foam and salt residue decal/mask` | Requires packed foam/salt mask; reject hard white ribbon and dense alpha without profiler proof. |

Applicability gate: PASS STATIC. Existing reports support shoreline material blockers. Task is applicable.

## Source Package Design

Source families owned by this package:

1. `WetBasaltShoreline`
2. `DryBasaltTransition`
3. `TriplanarCliffRock`
4. `FoamSaltContactMask`
5. `WetSheenLeakWaterlineMask`

Unsupported sibling families are not included. Photic seabed, coral, Aegir, cloud deck, scanner, and resource materials remain outside 2101 unless a future task assigns them.

### Physical Material Truth

| Family | Physical truth | Scale intent | Reject if |
|---|---|---:|---|
| `WetBasaltShoreline` | Volcanic black-gray basalt, chipped fracture planes, pores, salt-water erosion, damp cavity darkening, pale salt in crevices, sediment trapped in cracks, wet lower band. | 2 m tile source, 0.25-0.5 m shared detail. | Generic black rock, chrome wetness, flat gray diffuse, baked light, no wet/dry boundary. |
| `DryBasaltTransition` | Dry upper faces of the same basalt system: sunlit mineral chips, oxide/mineral stain, drier rougher planes, damp underside only near signed splash/waterline band. | 2 m tile source, 0.5 m detail. | One-note gray, abyss darkness, wetness everywhere, no mineral breakup. |
| `TriplanarCliffRock` | Large coast and medium-depth cliff projection: strata, sheared fracture planes, erosion shelves, sediment streaks, chip/mineral reveal, scale-stable projection across LODs. | 4 m macro projection, 0.5 m detail overlay. | Projection scale undocumented, large repeated plates, grey procedural cliff, LOD material pop. |
| `FoamSaltContactMask` | Presentation mask only: thin foam lace, cross-flow breakup, micro bubbles, salt residue, wet edge/sediment interruption along waterline. | 2 m mask source, projected/decal use. | Opaque white ribbon, scenic wave photo, storm grime default, mask hiding weak rock art. |
| `WetSheenLeakWaterlineMask` | Authored wetness state mask: feathered wet/dry transition, drying falloff, crack-following salt/mineral breakup, local specular/wet response. | 1-2 m mask source. | Generic glossy overlay, straight black stripe, muddy cover-up, gameplay truth encoded in visual mask. |

## PBR Channel Contract

This package declares a source-packing contract, not a Unity shader acceptance claim.

| Texture | Contract | Color space intent | Notes |
|---|---|---|---|
| Albedo | Base color only. No baked light, no cast shadow, no perspective, no labels. | sRGB | Bright photic readability is mandatory. |
| Normal or height source | Relief from cracks, pores, chipped edges, erosion shelves, foam/salt contact if applicable. | Linear | Future Unity owner must confirm normal orientation before import. |
| Packed MRAO/wetness | R = Metallic. G = Roughness. B = AO. A = wetness/family mask unless the target shader requires emission or an unused alpha. | Linear | R must be zero for basalt, salt, foam, sediment, and ordinary rock. If shader requires smoothness, invert G offline and rename contract before import. |
| Detail albedo/normal | Shared detail maps, no per-instance material clones. | Albedo sRGB, normal linear | Supports close wet rock and cliff microdetail. |
| Foam/salt contact mask | R = long foam/contact strength. G = cross-flow wet edge breakup. B = foam lace/bubbles/sediment interruption. A = confidence/caustic receiver reserved only if accepted. | Linear | Presentation only. Not water truth. |
| Wetness/waterline mask | R = wetness strength. G = drying falloff/transition softness. B = salt/sediment/mineral breakup. A = specular boost or confidence reserved only if accepted. | Linear | Does not change terrain, water, save, or collision truth. |

Channel-order risk: `2005_TEXTURE_CHANNEL_CONTRACTS.csv` warns that mandate/bible channel order differs. This package names G as Roughness for source derivation. Unity owner must lock the actual target shader before packing imported production textures.

The detailed role/import table is `Docs/Reports/Batch21/2101_WET_BASALT_PBR_ROLE_AND_IMPORT_INTENT.csv`.

## Prompt Requirements

Prompt IDs from 2022 used by this package:

- Rank 1: `TX_B21_WetBasaltShoreline_Albedo_20260604`
- Rank 2: `TX_B21_ShoreFoamSaltContact_Mask_20260604`

No browser or generation was run. The prompt pack is written for future source generation:

- `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2101_wet_basalt_shoreline_prompt_pack.md`

Generation rules:

- no account names or emails;
- no browser steps in reports;
- no generated image claim;
- outputs go under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`, never `Assets/**`;
- every candidate stays source candidate until intake QA, PBR derivation, Unity import, material binding, route capture, and profiler/VRAM proof exist where required.

## Source QA Criteria

Static source candidates must pass:

- square power-of-two source where required;
- seamless 2x2 review;
- manual 3x3 review;
- no baked lighting, no cast shadow, no directional highlights;
- no perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, or border;
- no muddy/noir cover-up;
- no crayon/procedural scribbles, random noise sold as material, smooth blob rock, or low-resolution bands;
- no false metallic for basalt/foam/salt/sediment;
- no false normal data from albedo stains;
- no alpha/transparency dependency without accepted shader/decal route.

The checklist is `Docs/Reports/Batch21/2101_WET_BASALT_STATIC_QA_GATE_CHECKLIST.md`.

## PBR Derivation Route

Allowed derivation order:

1. Intake albedo/source only after 2x2 and 3x3 seam review.
2. Remove baked-light contamination. Reject if cleanup cannot preserve material truth.
3. Derive or ingest height/normal source from real relief signals: cracks, pores, chips, strata, erosion shelves.
4. Derive roughness from wet/dry state: wet cracks and damp mineral stains smoother; dry chips, salt crust, and eroded pores rougher.
5. Derive AO from cavity bias only: cracks, pores, undercuts, sediment pockets.
6. Build wetness and foam/contact masks from waterline contact logic, not broad dirt overlays.
7. Pack source masks only after shader channel order is locked.
8. Run albedo-only, normal-only, mask-channel, flat-light, and URP-lit previews in the future Unity-owner slot.

No production MRAO is allowed if albedo fails seam, baked-light, perspective, or material-identity gates.

## Future Preview Proof List

These proof artifacts do not exist from this task. They are required later:

- albedo-only preview;
- normal-only preview;
- packed mask channel debug;
- flat-light neutral material preview;
- grazing-light preview for wet/dry response;
- final URP-lit preview;
- shoreline close capture;
- shoreline wide capture;
- glancing waterline edge capture;
- underwater edge / 5-20 m photic transition capture;
- compact/low-tier capture;
- Frame Debugger/RenderGraph proof if shader/render route changes;
- profiler/GC/memory/VRAM proof if runtime code, texture residency, render features, or material binding behavior changes.

## Dependency Scan Specification

Future Unity owner must scan before relink or deletion:

- scenes and prefabs for references to `terrain.mat`, `Mat_TriplanarRock.mat`, `Mat_LeakWetSheen.mat`, `mat_Rock2.mat`, `mat_Rock_Shared.mat`, and placeholder material families;
- material YAML and imported texture GUIDs for unresolved albedo/normal/mask/detail refs;
- production prefab/scene dependencies on `WorldRuntime/ProceduralPlaceholders/*_Placeholder.mat`;
- shader slot names and packed channel order before treating any `ORM`, `ARM`, `MRAO`, or `mask` filename as valid;
- old broken material state: fixed in place or no production scene/prefab binds it.

No deletion, relink, or import instruction is valid without this dependency proof.

## Visual Rejection Gates

Reject immediately:

- primitive, flat, or grey diffuse terrain/geology;
- muddy/noir surface or black cover-up;
- unresolved texture refs;
- default/package/proxy/placeholder material route in production;
- generic glossy overlay sold as wetness;
- guessed channel order;
- weak wet/dry boundary;
- hard white foam ribbon;
- broad terrain tile seam;
- baked light in albedo;
- material that looks acceptable only through fog, darkness, blur, storm, or water distortion.

No exception is allowed for "optimized".

## Continuous GlobalQualityWeight Consequences

These are source/material consequences only. Gameplay truth, water truth, terrain authority, save identity, collision, shader channel semantics, and proof state do not change.

| Lane | Consequence |
|---|---|
| Low / compact | Smaller future import sizes, fewer secondary detail maps, earlier HLOD and mip bias. Wet/dry basalt identity, strata, waterline, foam/salt edge, and readable shoreline silhouette remain mandatory. |
| Middle | Expected player hardware lane. Complete PBR role table, correct normal/packed masks, usable wetness and foam/contact source, and clear material read. |
| High | Richer normals, wetness masks, mineral breakup, foam contact, detail overlays, and longer near-field material detail after static and Unity proof. |
| Ultra | Hero shoreline microdetail and visual overkill only after the same source passes static QA, import, scene capture, Frame Debugger/profiler, and VRAM proof. |

## Future Unity-Owner Packet Template

Future owner packet must include:

- source file paths outside `Assets/**` and SHA-256 hashes;
- prompt ID and candidate ID;
- generated output path and QA output path;
- source QA result and rejection notes;
- PBR derivation report;
- texture role table and channel contract;
- Unity imported texture GUIDs after import;
- material/TerrainLayer slots and shader contract;
- dependency scan before/after;
- Game View and Scene View captures;
- compact/middle/high/ultra capture comparison if visual settings change;
- Frame Debugger/RenderGraph proof if shader/render route changes;
- profiler/GC/memory/VRAM proof if runtime or material binding behavior changes.

## Self-Audit

- Uses existing evidence only: YES.
- Depends on tasks 2102-2107: NO.
- Claims visual proof from static YAML/path existence: NO.
- Claims Unity material readiness: NO.
- Uses static evidence labels: YES.
- Moves any Unity/import/render work to future owner: YES.

## In-Game Result

Not run; PENDING VERIFICATION.

## What Was Verified

STATIC VERIFIED:

- authority docs read;
- support paths discovered;
- report rows extracted;
- source package written;
- PBR role/import intent CSV written;
- prompt pack written;
- static QA checklist written.

PENDING VERIFICATION:

- Gemini/offline image generation;
- source image QA;
- PBR derivation;
- Unity import;
- material binding;
- scene route captures;
- Frame Debugger;
- profiler;
- GC;
- memory;
- VRAM.


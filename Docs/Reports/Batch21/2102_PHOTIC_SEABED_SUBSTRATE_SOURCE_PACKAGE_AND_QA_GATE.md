# 2102 Photic Seabed Substrate Source Package And QA Gate

Agent ID: 2102
Batch: batch21_art_replacement_wave
Date: 2026-06-04
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW

No Unity, MCP, Play Mode, profiler, import, dotnet build, csc, project build, asset edit, material edit, scene edit, prefab edit, shader edit, package edit, or runtime script edit was performed.

## Boundary

This package owns photic seabed, sediment, substrate, shallow shelf, reef-anchor ground material source planning, prompt requirements, import intent, and static QA gates.

It does not own shoreline wet basalt/waterline rock replacement, coral/flora source packages, Unity import, TerrainLayer binding, material relink, scene scatter, placement rule edits, material previews, screenshots, profiler proof, or runtime acceptance.

First-20-minutes blocker removed: this package prepares the spend-first source and QA contract for the bright semi-open shallow exit floor so the player can read substrate identity, reef anchors, route edges, and medium-depth markers instead of generic blue water over placeholder floor.

Gate result: APPLICABLE. Batch20 and Batch21 static evidence explicitly names photic seabed/geology proxy and substrate source debt.

## Authorities Read

| Path | State | Evidence label |
|---|---|---|
| `AGENTS.md` | READ | STATIC_DOC |
| `TASTE.md` | READ | STATIC_DOC |
| `VISION_LOCKS.md` | READ | STATIC_DOC |
| `PROJECT_BIBLES.md` | READ | STATIC_DOC |
| `quality.md` | READ | STATIC_DOC |
| `PROCEDURAL_ASSET_PIPELINE.md` | READ | STATIC_DOC |
| `3dmodel.md` | READ | STATIC_DOC |
| `3DMODEL_GEOLOGY_ROCKS.md` | READ | STATIC_DOC |
| `3DMODEL_TEXTURES_MATERIALS.md` | READ | STATIC_DOC |
| `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` | READ | STATIC_DOC |
| `world.md` | READ | STATIC_DOC |
| `terrain.md` | READ | STATIC_DOC |
| `water.md` | READ | STATIC_DOC |
| `rendering.md` | READ | STATIC_DOC |
| `shaders.md` | READ | STATIC_DOC |
| `Docs/Reports/Batch20/2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md` | READ | STATIC_DOC |
| `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md` | READ | STATIC_DOC |
| `Docs/Reports/Batch20/2019_PROXY_DEBT_QUEUE.csv` | READ | STATIC_SOURCE |
| `Docs/Reports/Batch20/2019_GENERATION_ROUTE_MATRIX.csv` | READ | STATIC_SOURCE |
| `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` | READ | STATIC_DOC |

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain inferred from task: photic seabed/substrate material source planning and static QA.

Relevant registry mandates loaded:

| Mandate | Reason |
|---|---|
| `QA_Evidence_Text_Filter_Audit.txt` | Static text cannot become Unity/runtime proof. |
| `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` | Caustic/sediment/flow response must be authored fake-first, not simulation-first. |
| `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` | Texture size, streaming, and visual density must respect compact VRAM and frame budget. |
| `STRM_Async_Asset_Upload_Texture_Settings.txt` | Future texture import must respect upload budget and streaming proof. |
| `REND_Terrain_VirtualTexturing.txt` | Terrain material variety cannot add unbounded Texture2D bindings; arrays/SVT need proof. |
| `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt` | Shallow caustics/fog and material masks must not hide weak art or bake lighting into albedo. |

## Discovered Inputs

| Input | Path | State |
|---|---|---|
| 2005 rock variant matrix | `Docs/Reports/Batch20/2005_ROCK_VARIANT_MATRIX.csv` | FOUND |
| 2005 texture channel contracts | `Docs/Reports/Batch20/2005_TEXTURE_CHANNEL_CONTRACTS.csv` | FOUND |
| 2005 collider/LOD contracts | `Docs/Reports/Batch20/2005_COLLIDER_LOD_CONTRACTS.csv` | FOUND |
| Baseline material issues audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv` | FOUND |
| Baseline texture import issues audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_texture_import_issues.csv` | FOUND |
| Baseline surface unresolved refs audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_surface_unresolved_texture_refs.csv` | FOUND |
| Baseline unresolved refs audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv` | FOUND |
| Baseline surface material migration queue | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_surface_material_migration_queue.csv` | FOUND |
| Baseline texture memory hotspots audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_texture_memory_hotspots.csv` | FOUND |
| Baseline channel packing candidates audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_channel_packing_candidates.csv` | FOUND |
| Baseline detail candidates audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_detail_candidates.csv` | FOUND |
| Baseline detail missing materials audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_detail_map_missing_materials.csv` | FOUND |
| Baseline global detail overlay plan | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_global_detail_overlay_plan.csv` | FOUND |
| Baseline god-mode texture override audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_god_mode_texture_overrides.csv` | FOUND |
| Baseline texture read errors audit | `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_texture_read_errors.csv` | FOUND |

## Evidence Extract

| Source | Static finding | 2102 consequence |
|---|---|---|
| `2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md` | `terrain.mat`, `Mat_TriplanarRock.mat`, surface rock materials, coral proxy material, and placeholder materials remain blockers; terrain/rock roles have unresolved refs or empty normal/detail/mask slots. | Seabed package must define source and map roles, but cannot claim material readiness. |
| `2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md` | Unity owner instructions require albedo/normal/MRAO/detail/emission role tables, import settings, previews, route captures, and no darkening surface to hide material debt. | QA gate must reject muddy grade, baked light, proxy/default material, and missing proof. |
| `2019_PROXY_DEBT_QUEUE.csv` Q012 | Photic geology proxy debt names primitive/placeholder reef anchors, shelves, and medium-depth route markers; lane is `GENERATED_SOURCE_MISSING_THEN_UNITY_OWNER`. | 2102 source package is valid static prep for seabed/substrate; Unity owner still must import/bind/prove. |
| `2019_PROXY_DEBT_QUEUE.csv` Q014-Q015 | BioForge/geology material fallback routes can bind default/placeholder materials. | Future handoff must require dependency scans against placeholder/default material use. |
| `2019_GENERATION_ROUTE_MATRIX.csv` G014 | Shallow reef anchor and underwater shelf rocks need 2005 geology variants, submerged placement, route readability, and collider proof. | Substrate transition rules must support reef anchor and shelf material identity without editing placement. |
| `2019_GENERATION_ROUTE_MATRIX.csv` G015 | Hero arch and medium-depth route marker need silhouette, compound collider, and medium-depth readability proof. | Medium-depth marker substrate needs route-cue color/mineral chip read and no fog-only acceptance. |
| `2005_ROCK_VARIANT_MATRIX.csv` | `GEO2005_SHALLOW_REEF_ANCHOR_ROCK`, `GEO2005_UNDERWATER_SHELF_ROCK`, and `GEO2005_MEDIUM_DEPTH_ROUTE_MARKER` are planned with material sets and submerged placement constraints. | Seabed substrate families should feed those geology routes as source candidates, not final Unity assets. |
| `2005_TEXTURE_CHANNEL_CONTRACTS.csv` | Reef anchor and medium-depth material stacks need albedo, normal, packed mask, algae/silt/mineral masks, and shader-locked MRAO order. | MRAO order must remain pending until shader owner locks it; metallic is zero for sediment/calcite/rock substrate except exposed ore. |
| `2005_COLLIDER_LOD_CONTRACTS.csv` | Reef anchors/shelves/route markers need LOD and separate collider proof; visual LOD0 cannot be collider. | 2102 cannot close collider/LOD proof; it only states later proof requirements. |
| `2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` | Rank 3 spend-first target is `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604`. | Primary prompt and source QA gate are required now. |

Evidence boundary: all rows above are static text or CSV evidence. They support source planning only. They do not prove generated images, PBR maps, import settings, material bindings, scene usage, visual quality, performance, memory, or runtime readiness.

## Scope Split

| Adjacent work | Owned by 2102? | Handling |
|---|---:|---|
| Shoreline wet basalt and waterline foam/salt residue | NO | Handoff only; source family may include submerged basalt-sediment transition only where seabed touches rock. |
| Coral/flora albedo/height source package | NO | Handoff only; reef substrate must support coral anchoring, but coral texture/body source is out of scope. |
| Seabed sand/silt/shell/calcite substrate | YES | Full source design and QA gate. |
| Reef anchor ground surface below coral/rocks | YES | Substrate material truth, prompt variant, and future transition matrix. |
| Medium-depth route-marker ground substrate | YES | Material source and readability criteria only. |
| Unity TerrainLayer/material relink/import/placement | NO | Future Unity-owner proof requirement only. |

## Source Families

| Family | Evidence support | Source role | Status |
|---|---|---|---|
| Shallow sand/silt mix | 2022 rank 3; terrain/world/water visual floor | Primary photic seabed tile for 0-30 m open shallow floor. | SUPPORTED |
| Shell/calcite fragment substrate | 2022 rank 3; reef/calcite material truth from 3D texture bible | Close readable shell-grit and calcium fragments for photic identity. | SUPPORTED |
| Reef anchor rock surface | 2005 reef anchor row; 2019 G014 | Hard substrate blend where coral/rock anchor points meet sediment. | SUPPORTED |
| Underwater shelf sediment | 2005 underwater shelf row; 2019 G014 | Silt line and layered shelf sediment for 8-60 m route shelves. | SUPPORTED |
| Medium-depth route-marker substrate | 2005 route marker row; 2019 G015 | Darker but readable mineral-chip and silt substrate around landmarks. | SUPPORTED |
| Subtle caustic-compatible detail | water/rendering fake-first; 2022 caustic warning | Mask/source planning for shallow light response without baked caustic shadows in albedo. | SUPPORTED AS MASK INTENT |
| Basalt-sediment transition | 2005/2019 geology material blockers | Only the submerged seabed contact aspect is in scope. | CANDIDATE |
| Reef calcite floor | 2022 photic substrate and 2019 coral substrate blockers | Useful if primary prompt does not capture shell/calcite read. | CANDIDATE |

## Material Truth Table

| Family | Grain / fragment scale | Silt deposition | Biology / stain | Hardness / erosion | Wetness behavior |
|---|---|---|---|---|---|
| Shallow sand/silt mix | Fine sand 0.1-2 mm with occasional 2-12 mm grains. | Current ripples, lee-side buildup, no uniform Perlin. | Sparse algae/biofilm dusting only where light supports it. | Soft, compressible, low relief; route cue comes from ripple direction and color transitions. | Fully submerged; roughness high except wet glints on compacted ridges. |
| Shell/calcite fragment substrate | Shell chips 3-40 mm, broken coral/calcium flecks, no repeated obvious shells. | Fine sediment partially buries fragments and softens cavities. | Pale biofilm stains, not neon reef carpet. | Brittle fragments over soft bed; chips create cavity AO and height source. | Fully submerged; calcite fragments have small grazing highlights, not glossy plastic. |
| Reef anchor rock surface | Basalt/limestone knobs 0.2-2 m context; texture tile captures 1-2 m surface. | Sand and silt collect in rock cavities and coral-safe sockets. | Algae stain and mineral film in sheltered areas. | Hard substrate with chipped edges, pitted cavities, limestone/calcite blend. | Always wet; roughness varies by algae/silt/calcite exposure. |
| Underwater shelf sediment | Layered silt/sand bands, 1-8 cm ledge deposits. | Directional lines along shelf and under overhangs. | Low algae, more mineral staining than reef color. | Medium-hard shelf with sediment veneer; chipped edge readable. | Wetness always on; cavities darker but not black. |
| Medium-depth route-marker substrate | Coarser mineral chips 5-60 mm plus darker silt. | Lower contrast sediment lines that preserve marker silhouette. | Sparse biofilm only; no bright shallow carpet. | Harder basalt/mineral base, pressure-eroded and chipped. | Wetness constant; material must stay readable in twilight without fog hiding. |
| Caustic-compatible detail | Fine relief and broken fragments that accept projected light. | No baked light direction in source. | Optional pale flecks catch caustic projection. | Micro height supports normal derivation. | Response comes from future shader/material masks, not painted highlights. |

## Map Stack Contract

No source image from this package is a complete PBR material. The prompt output is a source candidate for later derivation.

| Family | Albedo source | Height-like source | Normal | Packed mask / MRAO | Optional detail | Optional caustic response |
|---|---|---|---|---|---|---|
| Shallow sand/silt mix | Base color only, bright photic tan/gray/cyan-green water-washed range; no shadows. | Ripple and grain relief source from the same or companion source. | Future offline derivation, BC5 intent. | R=metallic 0; G=roughness/smoothness shader-locked; B=cavity AO; A=wetness/substrate mask pending shader lock. | Shared fine grain detail candidate. | Dedicated linear mask candidate from pale ridges and fragment exposure; no baked caustic. |
| Shell/calcite fragment substrate | Shell/calcite chips with sediment; no object-render shells. | Fragment edge/cavity height source. | Future offline derivation with controlled strength. | Metallic 0; roughness high variance; AO cavity-biased; A shell/calcite blend or wetness pending shader. | Fragment micro-detail. | Mask favors exposed calcite chips and ripple ridges. |
| Reef anchor rock surface | Basalt/limestone/algae/sand blend, photic readable. | Pitted rock and sand-cavity source. | Future offline derivation; no noisy mush. | Metallic 0; roughness by algae/silt/calcite; AO in cavities; A material blend/wetness pending shader. | Algae/silt decal candidate. | Local mask, not albedo light. |
| Underwater shelf sediment | Layered shelf silt and rock-sediment color. | Shelf line/cavity relief. | Future offline derivation. | Metallic 0; roughness high in silt, lower on polished chips; AO cavity-biased; A shelf/silt family mask. | Fine silt line detail. | Low-intensity only if used near light/shallow. |
| Medium-depth route-marker substrate | Subdued but readable basalt/mineral/silt color; no black blob. | Mineral chip/fracture relief. | Future offline derivation. | Metallic 0 except real ore if later declared; roughness varied; AO cavity; A mineral/route cue mask. | Mineral chip detail. | Usually disabled below shallow caustic band unless local light reason exists. |

Channel warning: the 2005 contracts explicitly say MRAO order is shader-locked and must not be guessed from filenames. This package recommends roles but cannot lock the final shader channel order.

## Primary Prompt

Prompt file: `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2102_TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_PROMPT.md`

Target ID: `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604`

Purpose: create one seamless square source candidate for bright photic seabed substrate, used later for albedo cleanup and height/normal/mask derivation.

Prompt requirement summary:

- seamless square orthographic PBR material source;
- photic seabed, shallow 0-30 m, bright and readable;
- sand/silt base with shell/calcite fragments, reef anchor grit, subtle algae/mineral stain, directional current ripples;
- no baked shadows, no directional highlights, no horizon, no objects, no camera perspective, no text, no logo, no watermark, no border;
- no muddy blue-gray sand, flat Perlin, candy reef carpet, obvious repeated AI shells, fantasy coral pattern, or crayon marks;
- source should contain usable height-like relief but not fake normal-map colors.

## Optional Prompt Variants

| Variant | Reason | Status |
|---|---|---|
| `ShellGritCalcite` | Shell/calcite fragments are critical to avoid generic sand and support reef substrate identity. | JUSTIFIED |
| `ReefCalciteFloor` | Reef anchor substrate must support coral/rock sockets without becoming coral/flora source. | JUSTIFIED |
| `ShallowShelfSiltLines` | 2005 underwater shelf rows need sediment line read from 8-60 m. | JUSTIFIED |
| `BasaltSedimentTransition` | Useful only for submerged rock-sediment contact; shoreline wet basalt is sibling scope. | CANDIDATE / KEEP SEABED-ONLY |

No more variants are justified until the primary source candidate fails intake QA with a specific failure reason.

## QA Checklist

Source intake rejects:

- not square or not tileable;
- visible seam in 2x2 or manual 3x3 review;
- baked light, cast shadow, directional highlight, painted caustic shadow, horizon, object render, perspective, border, text, logo, UI, watermark;
- generic blue-gray sand with no substrate identity;
- flat Perlin or uniform noise;
- muddy darkness/noir grade;
- repeated obvious AI shells or cloned fragments;
- fantasy candy reef carpet or random neon coral texture;
- low-resolution mush, crayon marks, JPEG damage, or smeared fragments;
- material scale impossible for 1-2 m seabed tile;
- no height-like relief usable for later normal/AO derivation;
- source depends on future fog/darkness to look acceptable.

Manual review must include 1x, 2x2, and 3x3 tile views. Static audit may assist, but visual tileability remains pending until a human/Unity owner reviews the generated candidate.

## PBR Derivation Rules

| Map | Rule | Reject if |
|---|---|---|
| Albedo | Base color only. Remove baked light and shadow. Preserve bright photic color and material identity. | Painted caustics, black cavities, directional highlights, muddy grade. |
| Normal | Derive from height/source or high-quality offline process. Scale to player-height shallow visibility; no exaggerated pebbles. | Inverted green, flat accidental map, noisy false relief, seam mismatch. |
| Roughness | Follow wet/dry and grain/material truth: wet compacted grains and calcite chips can glint; silt remains rough. | Constant gray without rationale; glossy sand/coral-plastic look. |
| Metallic | Zero for sediment, shell, calcite, limestone, algae, and basalt substrate unless a future ore route explicitly declares exposed metal. | Any broad metallic substrate channel. |
| AO | Cavity-biased under fragments, ripples, and rock pits. It must not randomly dirty exposed surfaces. | Random dirt, baked directional shading, crushed black. |
| MRAO / mask | Pack only after shader owner locks G roughness/smoothness and A wetness/family/caustic role. | Filename-based channel guess. |
| Detail | Optional shared fine grain/shell micro detail; must survive mips and compression. | Detail map turns compact lane into noisy shimmer or blurry mud. |
| Caustic response mask | Optional linear mask for shallow projected light on exposed ridges/calcite chips. | Baked caustics in albedo or global dancing caustics without light reason. |

## Substrate Transition Matrix

This matrix is import/placement intent only. It does not edit placement rules.

| Future route | Source family | Transition rule | Proof required later |
|---|---|---|---|
| Shallow open photic floor | Shallow sand/silt mix | Broad tile base, directionally aligned to current/route where owner supports it. | Player-height low-tier and high-tier photic capture; 2x2/3x3 tile review. |
| Reef base | Shell/calcite plus reef anchor rock surface | Blend shell grit into hard anchor substrate; no coral texture substitution. | Reef base capture, substrate proof, dry-land zero proof by Unity owner. |
| Medium-depth marker | Medium-depth route-marker substrate | Darker mineral/silt around landmark, but silhouette and route edge stay readable without black fog. | Medium-depth low-quality route readability capture and final URP material preview. |
| Under rock shelf | Underwater shelf sediment | Silt lines and cavity AO under ledges; no black crush. | Grazing-light substrate preview and collider/route context proof by Unity owner. |
| Route edge / shelf break | Shelf sediment plus shell/calcite flecks | Increase fragment contrast and ripple direction to guide route edge. | Top-down shallow water and player-height route-edge captures. |
| Submerged basalt contact | Basalt-sediment transition candidate | Only seabed contact blend; shoreline/waterline wet basalt remains out of scope. | Handoff to shoreline/wet basalt owner, dependency scan before use. |

## Import Intent

Not Unity import proof.

| Texture role | Color space / type | Compression intent | Mips / streaming | Max size by `GlobalQualityWeight` |
|---|---|---|---|---|
| Albedo | sRGB true, texture default | BC7 Standalone, ASTC 6x6 mobile/XR if required | Mips on, streaming on, Read/Write off after import | Compact 1024, Middle 2048, High 2048, Ultra 4096 hero/source only |
| Normal | Linear, Normal Map type | BC5 Standalone where possible; ASTC normal route for mobile/XR | Mips on, streaming on | Compact 1024, Middle 2048, High 2048, Ultra 2048/4096 only with proof |
| Packed MRAO / masks | Linear data texture | BC7/BC3 based on platform and channel precision need | Mips on, streaming on | Compact 1024, Middle 2048, High 2048, Ultra 4096 source only if justified |
| Detail | Linear or sRGB by role; document role | Compressed high quality | Mips on, streaming on | Compact 512, Middle 1024, High 1024/2048, Ultra 2048 |
| Caustic response mask | Linear single/packed channel | Compressed data, preferably channel-packed | Mips on, streaming on | Compact 512, Middle 1024, High 1024, Ultra 2048 if visible |

Texture variety must use shared material families, packed masks, atlases/arrays, or validated terrain system paths. Do not add unbounded independent Texture2D bindings per terrain shader.

## Future Visual Proof Template

Required later before any production acceptance:

| Proof | Purpose | Evidence label after completion |
|---|---|---|
| Low-tier photic seabed capture at player height | Proves compact readability, material identity, route cues. | PLAYER-CAPTURE VERIFIED or EDITOR/PLAYMODE label as applicable |
| High-tier photic seabed capture at player height | Proves richer detail without changing truth. | PLAYER-CAPTURE VERIFIED or EDITOR/PLAYMODE label as applicable |
| Shallow water top-down capture | Proves substrate visible through water and not blue fog cover-up. | PLAYER-CAPTURE VERIFIED |
| Grazing-light substrate preview | Proves normal/roughness response and no baked-light albedo. | EDITOR VERIFIED / PLAYER-CAPTURE VERIFIED |
| Mask debug view | Proves roughness/AO/wetness/caustic channels have semantic separation. | EDITOR VERIFIED |
| Final URP material preview | Proves material under project lighting. | EDITOR VERIFIED |
| Dependency scan | Proves no placeholder/default material or unresolved refs are production-bound. | STATIC_SOURCE then EDITOR VERIFIED if Unity owner confirms |
| Profiler/GC/memory/VRAM proof | Required if shader, render route, residency, or material binding changes. | PROFILER VERIFIED |

## Rejection Gates

Reject future candidates if they contain:

- generic blue-gray sand;
- flat Perlin/noise floor;
- muddy darkness or noir cover-up;
- baked caustic shadows or baked directional lighting in albedo;
- proxy/default/package material substitution;
- unresolved texture refs or empty role slots;
- substrate that hides route cues;
- substrate that looks like fantasy candy reef carpet;
- source that depends on coral/flora work from 2101/2103;
- source that claims production readiness without generated image, intake QA, PBR derivation, Unity import, material binding, route capture, and performance proof.

## Future Dependency Scan

Unity owner must verify actual target materials/TerrainLayers/prefabs/scenes before import or binding. Static route names are not proof.

Minimum future scan:

- target material/TerrainLayer path and GUID;
- current texture slot role table;
- unresolved GUID/null slot report;
- shader/channel contract;
- old placeholder/default material dependency report;
- production route inclusion/exclusion proof;
- no relink to `WorldRuntime/ProceduralPlaceholders/*_Placeholder.mat`;
- no material clone or runtime material mutation.

## Continuous Quality Consequences

`GlobalQualityWeight` is continuous. These rows are documentation checkpoints, not binary switches.

| Lane | Consequence |
|---|---|
| Low / compact | Smaller imported maps and reduced scatter/detail density only. Substrate identity, route edge, shell/calcite read, and shallow clarity remain mandatory. No muddy fallback. |
| Middle | Expected player lane. Full PBR roles, readable terrain transition, reef substrate identity, and non-muddy photic color. |
| High | Richer height/normal detail, shell fragments, sediment breakup, wetness/silt variation, stronger caustic-compatible masks where justified. |
| Ultra | Hero-quality closeups and visual density only after source, import, route capture, profiler, GC, memory, and VRAM proof. Ultra cannot rescue bad seams, wrong channels, or weak material identity. |

Quality may scale source resolution, detail intensity, decal density, mask precision, and streaming residency. It must not change material role semantics, gameplay route truth, collision identity, save identity, or shader channel order.

## Self-Audit

- No dependency on 2101 or 2103 is present.
- Out-of-scope shoreline wet basalt and coral/flora source details are marked handoff/candidate only.
- No static source claim is upgraded to visual/runtime acceptance.
- No Unity, build, import, profiler, MCP, asset, package, material, shader, scene, prefab, or runtime script action was performed.

## Proof Packet

STATIC VERIFIED:

- Required authority docs and targeted mandates were read.
- Batch20/Batch21 evidence for photic seabed/substrate blockers was extracted.
- 2005 contract CSVs and active material audit CSVs were discovered.
- Source families, material truths, map stack, primary prompt, optional variants, QA gates, PBR derivation rules, import intent, transition matrix, proof template, rejection gates, future dependency scan, and continuous quality consequences were written.

PENDING VERIFICATION:

- Gemini/source image generation.
- Image download and SHA-256.
- Static intake audit output.
- Manual 2x2 and 3x3 tiling review.
- PBR derivation.
- Unity import.
- Material/TerrainLayer binding.
- Placeholder/default material dependency cleanup.
- Scene placement/transition proof.
- Low/high player-height captures.
- Shallow top-down water capture.
- Grazing-light material preview.
- Mask debug.
- Final URP material preview.
- Profiler, GC, memory, VRAM, Frame Debugger, or RenderGraph proof.

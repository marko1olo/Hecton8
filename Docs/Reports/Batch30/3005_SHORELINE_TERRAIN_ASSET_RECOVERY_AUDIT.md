# 3005_SHORELINE_TERRAIN_ASSET_RECOVERY_AUDIT

Status: STATIC VERIFIED / UNITY MATERIAL AND RUNTIME PROOF PENDING
Date: 2026-06-04
Owner role: Shoreline / Terrain / Generated Asset Recovery Auditor
Write scope: `Docs/Reports/Batch30/3005_SHORELINE_TERRAIN_ASSET_RECOVERY_AUDIT.md`

## Evidence Boundary

No Unity run. No Play Mode. No build. No profiler. No `Assets/**` edits.

Evidence classes used:

- `STATIC_DOC`: authority docs, manifests, prompt packs, QA reports.
- `STATIC_SOURCE`: Unity YAML/text asset inspection and screenshot metadata text.
- `STATIC_IMAGE_QA`: generated texture intake metrics and 2x2 preview inspection.
- `SCREENSHOT_REVIEW`: existing screenshot image review only; not fresh runtime proof.

Any claim about in-game quality, import correctness, Frame Debugger state, profiler cost, GC, VRAM, or final route acceptance remains `PENDING VERIFICATION`.

First-20 route impact: removes the current surface/shoreline visual recovery blocker by separating usable source references from rejected/import-blocked material candidates and defining the controlled recovery path.

## Authority Read

`STATIC_DOC`:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `terrain.md`
- `world.md`
- `3dmodel.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `quality.md`

Relevant mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

`Docs/Actual Domains of Project.txt` was checked and is missing. Narrow domain inferred: terrain/shoreline/generated texture recovery.

## Evidence Inspected

`SCREENSHOT_REVIEW / STATIC_SOURCE`:

- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png`
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`

`STATIC_DOC / STATIC_IMAGE_QA`:

- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1429_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicDarkPreserve/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/1906/TX_H8_WetBasaltShoreline_1428_static_channel_QA_metrics.txt`
- `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch29/2903_SHORELINE_FOAM_CAUSTIC_TEXTURE_PROMPT_AND_QA_PACK.md`

`STATIC_SOURCE` material/terrain inspection:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/L_Basalt.terrainlayer`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/*.meta`
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_PhoticHeroTerrain_1453.mat`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticReefBasaltSand_1435.mat`
- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat`

## Current Visible Terrain Failure

Claim: The current surface/shoreline view fails the surface/photic visual floor.

Evidence Class: `SCREENSHOT_REVIEW`

Artifacts:

- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png`
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`

Findings:

- Foreground shoreline reads as black primitive shells/boulders and flat planes.
- Yellow/green sheet artifacts are visible at terrain/water contact.
- Terrain lacks wet basalt material truth, strata, sediment, waterline erosion, foam contact ownership, scale witnesses, and believable geology.
- `h8_1912_surface_quarantine.txt` lists multiple disabled/rejected renderers with reasons including `black_primitive_foreground_boulder`, `black_rejected_photic_rock_garden`, `broken_debug_foam_sheet`, `flat_green_surface_haze_sheet`, and `surface_noir_slab_or_curtain`.
- `h8_1912_surface_edit_main.txt` reports the visible renderer set as mainly `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` and `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428`. That is not a terrain recovery proof.

Residual risk: screenshot is an existing artifact, not fresh runtime proof. Still enough to classify the visible result as below the static visual floor.

## Active Material/Texture State

Claim: Existing active/static material routes do not prove a recovered shoreline terrain material.

Evidence Class: `STATIC_SOURCE`

Artifacts:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/L_Basalt.terrainlayer`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticReefBasaltSand_1435.mat`
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_PhoticHeroTerrain_1453.mat`
- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat`

Findings:

- `L_Basalt.terrainlayer` still references `Rock031_1K-JPG_Color.jpg` and `Rock031_1K-JPG_NormalGL.jpg`; it has no mask map.
- `TX_H8_WetBasaltShoreline_Albedo_1428.png` exists under `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/`, but the manifest marks it `UNITY_MATERIAL_BLOCKED` and static QA rejects it as a production tile.
- Several `MAT_H8_*` materials reference the rejected 1428 albedo GUID, but this is not material acceptance. The required normal/MRAO/wetness/contact stack is missing.
- `MAT_H8_PhoticReefBasaltSand_1435` has `_MetallicGlossMap`, `_OcclusionMap`, `_DetailMask`, `_DetailAlbedoMap`, and `_DetailNormalMap` unset. It cannot prove roughness/AO/wetness/material breakup.
- `MAT_H8_ShorelineFoamFine_1469` uses the shared `foam.png` texture and transparent sheet settings. It is a support foam surface, not a contact-owned foam/salt/wetness mask family.

Residual risk: material YAML inspection does not prove what Unity currently renders after other agents' changes. Runtime proof remains pending.

## Candidate Classification

### Wet Basalt 1428

Path:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md`

Evidence Class: `STATIC_DOC / STATIC_IMAGE_QA`

Classification: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`

Facts:

- 1024x1024 albedo only.
- Static QA verdict: `REJECT`.
- LR seam: `30.780`; TB seam: `33.396`; luma mean: `85.971`.
- No normal, no MRAO, no wetness/salt/contact mask, no accepted material/TerrainLayer.
- 2x2 review shows useful wet basalt identity, but the diagonal teal/mineral forms repeat obviously.

Allowed use: source/reference, small masked detail decal, or heavily blended detail under macro masks.

Forbidden use: direct broad terrain tile, direct `L_Basalt` replacement, production PBR material claim, or surface-darkening cover-up.

### Wet Basalt 1429 Raw

Path:

- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1429_MANIFEST.md`

Evidence Class: `STATIC_DOC / STATIC_IMAGE_QA`

Classification: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`

Facts:

- Static QA verdict: `REJECT`.
- LR seam: `30.611`; TB seam: `34.508`; LR band: `37.609`; TB band: `40.462`; luma mean: `82.999`.
- Better source identity than 1428 in places, but still has edge/band mismatch and visible repeated forms.

Allowed use: reference for future correction prompt or manual paintover.

Forbidden use: naked terrain tile, Unity production material, or derivation base for final normal/MRAO without cleanup.

### Wet Basalt 1429 Periodic / DarkPreserve

Paths:

- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png`

Evidence Class: `STATIC_IMAGE_QA`

Classification: `DUPLICATE REFINEMENT / STATIC_REJECTED`

Facts:

- Exact edge metrics read `0.000 / 0.000`, but strict band metrics still reject: LR band `57.797`, TB band `62.653`.
- Luma mean `156.559`; clipped white pixels `12.54%`; saturated channels `14.22%`.
- Edge pinning is diagnostic-only. It hides edge mismatch while preserving/revealing inner wrap-band failure and clipped/baked albedo behavior.

Allowed use: failure reference for seam-fix process.

Forbidden use: claim of seam-fixed source.

### Wet Basalt 1429 Periodic Mean

Path:

- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`

Evidence Class: `STATIC_IMAGE_QA`

Classification: `REJECT`

Facts:

- LR seam `68.255`; TB seam `84.430`; LR band `69.583`; TB band `75.465`.
- Clipped black pixels `16.73%`; clipped white pixels `3.50%`; saturated channels `21.90%`.
- Visual preview has harsher crushed high/low values and obvious repeated forms.

Allowed use: none beyond rejection evidence.

### Batch21 Photic Seabed Substrate

Path:

- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png`

Evidence Class: `STATIC_DOC / STATIC_IMAGE_QA`

Classification: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`

Facts:

- Static QA verdict: `REJECT`.
- LR seam `15.702`; TB seam `17.115`; LR band `19.358`; TB band `23.681`; luma mean `169.267`.
- Bright shallow substrate direction is useful.
- 2x2 preview repeats large diagonal dune/ripple bands.

Allowed use: prompt/paintover reference for corrected photic substrate.

Forbidden use: direct Unity import or terrain layer binding.

### Batch21 Photic Shell/Sand Substrate

Path:

- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`

Evidence Class: `STATIC_DOC / STATIC_IMAGE_QA`

Classification: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`

Facts:

- Static QA verdict: `REJECT`.
- LR seam `16.978`; TB seam `17.091`; LR band `19.845`; TB band `23.251`; luma mean `172.502`.
- Useful bright shell/sand/calcite reference.
- 2x2 preview repeats recognizable shell/stone clusters.

Allowed use: reference for corrected source.

Forbidden use: direct Unity import or runtime material binding.

### Batch29 2903 Prompt/QA Pack

Path:

- `Docs/GeneratedAssets/Gemini/Prompts/Batch29/2903_SHORELINE_FOAM_CAUSTIC_TEXTURE_PROMPT_AND_QA_PACK.md`

Evidence Class: `STATIC_DOC`

Classification: `VALID STAGING PLAN / NO GENERATED ASSET`

Facts:

- Defines future wet basalt, shell/sand, foam/salt/wet-contact, and shallow caustic prompt/QA lanes.
- Generation state: `NOT RUN`.
- It cannot promote any asset to Unity import by itself.

Allowed use: next controlled source generation route.

Forbidden use: treating prompt text as image, material, or runtime proof.

## Why The Current Visible Route Fails

Evidence Class: `SCREENSHOT_REVIEW / STATIC_SOURCE`

Root causes visible in static evidence:

- Black shell/primitive foreground geometry dominates the waterline and violates the surface/photic beauty lock.
- Materials do not carry a full PBR shoreline stack: no accepted normal, no packed MRAO, no wetness/salt/contact mask, no channel-independence proof.
- Active/support foam is a transparent sheet/ribbon, not a contact mask bound to rough wet basalt and shell/sand receiver logic.
- Existing broad terrain candidates are either old Rock031 references or rejected wet basalt sources, not controlled HECTON-8 shoreline materials.
- Terrain lacks visible strata, sediment accumulation, waterline erosion, mineral breakup, foam contact ownership, and scale witnesses.
- Texture candidates alone cannot fix primitive geometry. The route also needs improved geology meshes/terrain shapes with LODs, collision proxies, and flat-material silhouette proof.

## Controlled Recovery Path

No random download may enter `Assets/**`. All generated material candidates stay in `Docs/GeneratedAssets/**` until a later Unity-owner task proves import readiness.

1. Generate or collect new source candidates only under:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/
```

Required family folders:

```text
WetBasaltShoreline/
ShellSandSubstrate/
ShoreFoamSaltWetContact/
ShallowCausticMasks/
Manifests/
TilePreviews/
ContactSheets/
Rejected/
```

2. Every image gets a sidecar manifest with:

- prompt ID;
- full prompt and negative prompt;
- source tool and timestamp;
- SHA-256;
- dimensions;
- intended meters per tile;
- color-space intent;
- channel contract;
- evidence class;
- QA command;
- QA output paths;
- status: `SOURCE_CANDIDATE`, `REJECT`, `REVIEW`, or `PASS_STATIC`.

3. Run static intake and manual tile review:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/[Family]/[Candidate].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch29/2903/[Candidate]
```

Required review outputs:

- 1x source inspection;
- 2x2 preview;
- 3x3 preview;
- contact sheet;
- histogram/luma/seam/band/channel report;
- manual rejection note for repeated hero shapes, baked lighting, perspective, false PBR, or material mush.

4. Only `PASS_STATIC` candidates may advance to PBR derivation planning. Required families:

- wet basalt albedo/source;
- wet basalt normal;
- wet basalt packed MRAO/wetness/salt mask;
- shell/sand/calcite albedo/source;
- shell/sand/calcite normal;
- shell/sand/calcite packed MRAO/family mask;
- shoreline foam/salt/wet-contact RGBA mask;
- shallow caustic receiver and lookup masks.

5. Unity-owner import may occur only after the full package exists:

- manifest reviewed;
- no baked lighting/random noise/false PBR;
- import settings specified for albedo, normal, MRAO/masks, optional height/detail;
- texture compression/mip/streaming settings specified per role;
- shared `MAT_*` and TerrainLayer route specified;
- no GUID replacement of shared Rock031 assets;
- no replacement of active terrain while another Unity owner is working;
- screenshot/profiler/Frame Debugger proof plan exists.

6. Scene recovery must include geometry, not only textures:

- replace black primitive shells with geology/shoreline meshes or terrain shapes that show fracture, strata, wet waterline, sediment shelves, and scale witnesses;
- provide LOD0/LOD1/LOD2 or HLOD/impostor route;
- provide collision proxy separate from LOD0 visual mesh;
- provide flat-material override screenshot and final-material screenshot;
- prove compact and high-tier readability.

## Material Proof Requirements

Before any candidate can be called ready for controlled Unity-owner import:

- Static QA verdict is at least `PASS_STATIC`.
- 2x2 and 3x3 previews show no hard seam, wallpaper rhythm, repeated hero crack, repeated shell cluster, repeated foam stamp, or diagonal banding.
- Albedo has no baked shadows, directional highlights, perspective, horizon, object render, text, watermark, or noir crush.
- Normal source is derived or authored offline and imported as normal, linear, compressed, mip-enabled.
- MRAO is packed and documented. R metallic must be zero/near-zero for basalt and shell/sand except true mineral/ore inclusions. G roughness/smoothness must be declared. B AO must be cavity-biased. A must be wetness/emission/family mask per manifest.
- Foam/contact RGBA channels must be semantically separable; no full-white strip or opaque snow field.
- Caustic masks must be receiver/lookup support only, not baked caustic albedo.
- Material uses shared assets compatible with SRP Batcher. No runtime material clones. No runtime texture synthesis.
- Import report includes sRGB/linear role, compression, max size, mips, streaming setting, wrap mode, and texture memory expectation.

## Mesh/Terrain Proof Requirements

Texture recovery cannot accept the shoreline by itself. Geometry proof must include:

- terrain/mesh source, deterministic seed or authoring route;
- geological process tag: wet basalt shoreline, fractured shelf, sediment apron, shell/sand transition, undercut, cliff, boulder field, or waterline shelf;
- silhouette proof with flat material override;
- final material proof under URP lighting;
- UV or triplanar scale report;
- vertex/mask channel meaning where used;
- LOD triangle counts and decimation method;
- collision proxy type and budget; LOD0 visual mesh as collider is rejected;
- waterline/foam contact placement proof;
- compact screenshot and high-tier screenshot.

## Low / Middle / High / Ultra Consequences

These are authoring consequences only, not runtime proof.

Low / compact (`GlobalQualityWeight` near 0.0):

- Use 512-1024 accepted shipped maps for standard world surfaces.
- Preserve wet basalt identity, shell/sand scale, foam contact, route silhouettes, and waterline readability.
- Prefer shared texture arrays/materials, packed masks, HLOD/proxy geometry, and fewer decals.
- Do not use darkness, green haze, blur, or generic noise to hide weak terrain.

Middle:

- Use 1024-2048 accepted key shoreline maps where memory budget permits.
- Add stronger roughness/AO variation, wetness masks, shell/calcite breakups, and controlled local decals.
- Keep the same material/channel contracts as compact.

High:

- Spend budget on richer normals, better wetness/salt masks, denser near-field geology, stronger foam breakup, local caustic receiver logic, and longer LOD residency.
- Keep gameplay truth, terrain ownership, material identity, and channel semantics unchanged.

Ultra:

- Allow hero-only 4096 source bakes after static and Unity proof.
- Add dense offline decal layers, sharper PBR derivation, richer close-waterline material witnesses, better reflections/caustic support, and visual overkill.
- Ultra buys sensory richness only. It must not create new route truth or bypass material/import proof.

## Final Findings

- No inspected wet basalt, shell/sand, foam/contact, or caustic candidate is ready for production import.
- `1428` and `1429` wet basalt are useful source references but static rejects for broad terrain.
- `1429_periodic`, `1429_periodic_darkpreserve`, and `1429_periodic_mean` are rejected seam-fix attempts or duplicate refinements. They must not be used as seam-fixed proof.
- Batch21 photic seabed and shell/sand candidates are useful visual references but static rejects.
- Batch29 2903 is the correct prompt/QA staging route, but it has not generated assets yet.
- The active scene/terrain failure is not just texture absence. It is black primitive geometry plus incomplete material families plus sheet-style foam/caustic support.
- Controlled recovery requires a full source-to-PBR-to-material-to-geometry-to-scene proof chain while keeping generated assets in `Docs/GeneratedAssets/**` until a Unity owner proves import and route quality.

## Remaining Pending Verification

- Unity material binding proof.
- TerrainLayer proof.
- Mesh/terrain geometry replacement proof.
- Compact and high-tier screenshots.
- Frame Debugger / RenderGraph proof for material/shader route if changed.
- Profiler/GC/VRAM proof for runtime route impact.
- Addressables/streaming residency proof if any imported assets become runtime shoreline terrain content.

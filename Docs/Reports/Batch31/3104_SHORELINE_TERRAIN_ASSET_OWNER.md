# 3104_SHORELINE_TERRAIN_ASSET_OWNER

Status: STATIC VERIFIED / STATIC_IMAGE_QA / UNITY ACCEPTANCE PENDING
Date: 2026-06-05
Owner role: SHORELINE_TERRAIN_ASSET_OWNER

## Evidence Boundary

No Unity run. No Play Mode. No build. No profiler. No `Assets/**` edits.

Evidence classes:
- `STATIC_DOC`: authority docs, task file, prior Batch30/31 reports.
- `STATIC_SOURCE`: Unity YAML/material/shader/import metadata and screenshot metadata text.
- `STATIC_IMAGE_QA`: Batch31 LocalPBR contact sheet and package manifests.
- `SCREENSHOT_REVIEW`: existing MCP screenshot metadata only.

Runtime quality, material readback, Frame Debugger state, GC, VRAM, and route acceptance remain `PENDING VERIFICATION`.

First-20-minutes route impact: removes a surface/shoreline route blocker by separating usable wet shoreline sources from production blockers and defining the controlled geology/material replacement path.

## Authority And Mandates

Read:
- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `terrain.md`
- `world.md`
- `3dmodel.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/Reports/Batch30/3005_SHORELINE_TERRAIN_ASSET_RECOVERY_AUDIT.md`
- `Docs/Reports/Batch31/GEMINI_TEXTURE_VISUAL_REVIEW_AND_LOCAL_PBR_20260605.md`
- `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`
- `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`

Mandates followed:
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`

## Current Route Failure

Existing evidence still supports Batch30's verdict: current shoreline recovery is blocked by geometry and material debt.

Static screenshot metadata:
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`

Named rejected objects/materials:
- `H8_HeroWetBasaltBoulder_1453_00..09`: `black_primitive_foreground_boulder`, material `MAT_H8_PhoticHeroTerrain_1453`.
- `H8_PHOTIC_ROCK_GARDEN_1469`: `black_rejected_photic_rock_garden`, material `MAT_H8_PhoticReefBasaltSand_1435`.
- `H8_FloorCausticSoft_1443`: `yellow_surface_visible_caustic_sheet`.
- multiple `H8_SurfaceFoamTopOnly_*`, `H8_BrokenShoreFoam_*`, `H8_BrokenReadableFoam_1443`, `H8_VisibleBrokenFoam_1435`: broken/debug foam sheet routes.
- `H8_PHOTIC_SOFT_WATER_HAZE_1430`: flat green surface haze sheet.
- surface noir slabs/curtains are quarantined or inactive and must not return as visual cover.

Static result: texture work alone cannot recover the shoreline. The replacement must include believable wet basalt/shell-sand materials and non-primitive geology with LOD/collider proof.

## Source Candidate Classification

### Wet Basalt 1429 Full Source Prototype

Path:
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429_FullSourcePrototype/`

Classification: `SOURCE/PROTOTYPE ONLY`

Useful:
- preserves full wet basalt source without destructive watermark crop;
- strong rock relief, wet mineral/salt/barnacle direction;
- usable for temporary material prototype outside `Assets`.

Blockers:
- Gemini mark remains final-cleanup debt;
- baked lighting and macro forms remain;
- generated normal/MRAO are local static source bakes, not accepted PBR channels;
- no Unity import, no shader binding proof, no route screenshot.

Allowed next use: prototype material work and reference/prompt correction under `Docs/GeneratedAssets`.

Forbidden use: final production texture, broad terrain tile, direct `Assets` import, or acceptance claim.

### Wet Basalt 1429 Crop / Local PBR Source Bake

Path:
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/`

Classification: `SOURCE/PROTOTYPE ONLY`

Useful:
- local albedo/height/normal/MRAO source package exists;
- 2x2 preview and contact sheet expose repeat behavior for manual review.

Blockers:
- crop loses useful source information compared with full-source prototype;
- not final PBR separation;
- no material-channel convention proof;
- no import settings proof.

Allowed next use: compare against full-source package and build corrected source prompt/paintover.

### Wet Basalt 1428 Imported Asset

Path:
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`

Classification: `EXISTING FIRST-PARTY SOURCE / STATIC REJECTED FOR FINAL`

Facts:
- import meta exists;
- albedo role only;
- Standalone max size is 512 with BC-format override;
- `streamingMipmaps: 0`, so it is not currently a proven streamed world-terrain source;
- no accepted normal/MRAO/wetness/contact stack.

Allowed next use: reference or masked/detail prototype.

Forbidden use: broad terrain final, production PBR material claim, or direct fix for black primitive geometry.

### Batch21 Photic Seabed Substrate 2102

Path:
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/`

Classification: `SOURCE/PROTOTYPE ONLY`

Useful:
- bright shallow substrate and shell/debris detail;
- better direction than current placeholder route materials.

Blockers:
- report states baked shadows reduced, not removed/proven;
- local normal/MRAO are static source bakes only;
- no seam/import/material proof.

Allowed next use: controlled shell/sand or seabed material authoring source.

### Batch21 Photic Shell/Sand Substrate 2102

Path:
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/`

Classification: `SOURCE/PROTOTYPE ONLY`

Useful:
- bright shell/sand/calcite shallow route identity;
- supports photic readability and route beauty.

Blockers:
- no final seam proof;
- no PBR role acceptance;
- no imported material proof.

Allowed next use: corrected shell/sand/calcite family authoring.

### Foam / Salt / Wet Contact

Existing static support:
- `Assets/Crest/Crest/Textures/foam.png`
- `Assets/Crest/Crest/Textures/Foam2.png`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch29/2903_SHORELINE_FOAM_CAUSTIC_TEXTURE_PROMPT_AND_QA_PACK.md`

Classification: `SUPPORT ONLY / MISSING ROUTE-OWNED CONTACT MASK FAMILY`

Current route problem:
- existing foam objects are sheet/ribbon/debug routes, not material-owned wet contact masks;
- current `MAT_H8_ShorelineFoamFine_1469` is active in screenshot metadata but it does not prove rock/sand receiver integration.

Replacement requirement:
- RGBA foam/salt/wet-contact mask family;
- channel semantics: foam lace, salt crust, wet edge, breakup/noise/contact fade;
- receiver integration on basalt/shell/sand material, not detached strips as the main shoreline proof.

### Caustic Receiver

Existing static support:
- `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`
- `Assets/_Project/Editor/Bakers/CausticOpticsBaker1719.cs`
- `H8_FloorCausticSoft_1443` is disabled/rejected as yellow visible sheet in screenshot metadata.

Classification: `SUPPORT ONLY / CURRENT SHEET ROUTE REJECTED`

Replacement requirement:
- caustics as receiver/lookup mask or shader support;
- no baked caustic albedo;
- no visible yellow sheet artifacts;
- compact route must remain readable without high-cost caustics.

## Material And Shader Route Classification

### Broken / Do Not Reuse

`Assets/_Project/Art/Materials/terrain.mat`
- shader GUID `58f9232bdfcb5064f9b47d1dddb46260` unresolved in current static meta scan;
- `_BaseMap`, `_MainTex`, `_Rock_Albedo` GUID `47f0a231c050423488e0ff6f7d66f813` unresolved;
- many PBR roles empty.

Disposition: stale. Do not raw-patch. Replace through Unity owner.

`Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- shader GUID `74659692f6350ba46b88180d9c826630` unresolved in current static meta scan;
- `_Rock_Albedo` GUID `47f0a231c050423488e0ff6f7d66f813` unresolved;
- `_BaseMap`, `_BumpMap`, MRAO/occlusion/detail roles empty.

Disposition: stale. Do not raw-patch. Replace through Unity owner.

### Valid Static Candidate / Terrain Route

`Assets/_Project/Art/Materials/Mat_Terrain.mat`
- shader: `Assets/_Project/Art/Shaders/TerrainMaster.shader`;
- shader GUID resolves to `8f437952d5c77cc4c89dd9aa309a73cf`;
- binds `_RockTex`, `_SandTex`, `_FlowNormal`;
- supports terrain control map, biome texture array, silt layer, micro erosion, and `_HectonMathLodWeight`.

Limit:
- current material lacks bound `_SiltTex`, `_TerrainControlRGBA`, and `_HectonBiomeGroundArray` in YAML;
- proof of current scene binding absent.

Disposition: valid static route candidate, not accepted material proof.

### Valid Static Candidate / Photic Geology Prototype

`Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader`
- first-party URP shader exists;
- triplanar base sampling, caustic fake, fill light, and wet spec fake;
- uses CBUFFER and no runtime material clone logic.

Limit:
- unlit forward pass and sine caustic fake are prototype-grade until Frame Debugger/visual proof;
- lacks full packed PBR mask role.

Disposition: prototype/support shader candidate, not final terrain acceptance.

### Valid Static Candidate / First-Party Wet Basalt Materials

`Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`
- binds wet basalt albedo 1428 and Rock031 normal;
- has `_NORMALMAP`;
- missing metallic/smoothness/occlusion/detail maps.

`Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`
- binds wet basalt albedo 1428;
- no normal/MRAO/detail maps.

`Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticReefBasaltSand_1435.mat`
- binds wet basalt albedo 1428 and Rock031 normal;
- missing MRAO/detail/contact roles.

Disposition: usable first-party prototypes. Not final shoreline material proof.

### TerrainLayer Basalt

`Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/L_Basalt.terrainlayer`
- diffuse: `Rock031_1K-JPG_Color.jpg`;
- normal: `Rock031_1K-JPG_NormalGL.jpg`;
- mask: missing;
- smoothness: 0.

Disposition: old terrain layer. Not enough for wet shoreline recovery.

## Missing PBR Roles

Wet basalt:
- accepted albedo without watermark/baked light/repeated hero forms;
- BC5 normal authored from height/sculpt/source, not accidental albedo derivative;
- packed MRAO/wetness/salt mask;
- contact wet-edge mask;
- detail normal for near waterline;
- triplanar scale/material manifest.

Shell/sand/calcite:
- accepted albedo with no baked shadows or repeating shell clusters;
- normal map for shell fragments/ripple relief;
- packed MRAO/family mask;
- sediment/wetness transition mask;
- scale witness and meters-per-tile manifest.

Foam/contact:
- RGBA semantic mask;
- receiver integration with basalt/sand;
- no detached sheet as main proof.

Caustic receiver:
- lookup/receiver support only;
- no visible colored plane;
- compact fallback with material/specular readability.

## Geology Mesh Replacement Requirements

Black primitive objects cannot be fixed by material assignment alone.

Required replacement package:
- wet basalt shoreline boulders/shelves/undercuts with fracture planes, strata, waterline erosion, mineral breakup, sediment ledges, and shell/sand transition;
- deterministic source or authored mesh route;
- LOD0/LOD1/LOD2 or HLOD/impostor with silhouette preservation;
- separate collision proxy; LOD0 `MeshCollider` rejected;
- flat-material silhouette screenshot and final-material screenshot;
- waterline foam/contact placement proof;
- compact and high-tier route screenshots;
- material manifest with UV/triplanar scale and mask semantics;
- scale witnesses: shell fragments, small pebbles, waterline crust, erosion shelves, route landmark shape.

## Controlled Generation / QA Route

Keep all new source and proof under:

```text
Docs/GeneratedAssets/Batch31_ShorelineTerrain/
```

Suggested folders:

```text
WetBasalt/
ShellSand/
FoamSaltWetContact/
CausticReceiver/
GeologyMeshRequirements/
Manifests/
TilePreviews/
Rejected/
```

Required manifest fields:
- source path;
- prompt/source ID;
- SHA-256;
- dimensions;
- intended meters per tile;
- role: albedo/normal/MRAO/contact/height/detail;
- color-space intent;
- channel contract;
- static QA path;
- manual review verdict;
- status: `SOURCE_CANDIDATE`, `PROTOTYPE_ONLY`, `PASS_STATIC`, `REJECT`.

Static QA command shape:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root <candidate.png> --out-dir Docs/GeneratedAssets/Batch31_ShorelineTerrain/<Family>/Audit/<Candidate>
```

Script output is advisory for seams/clipping. Manual art review remains mandatory.

## Low / Middle / High / Ultra Consequences

Compact / low:
- use 512-1024 compressed maps for standard world terrain;
- preserve wet basalt/shell/sand identity, waterline contact, and route silhouettes;
- use packed masks, shared materials, texture arrays when validated, HLOD/proxy geometry;
- no fog/darkness/noir slabs to hide weak assets.

Middle:
- use 1024-2048 key shoreline maps where budget allows;
- add roughness/AO/wetness variation, shell/calcite breakup, and local decals;
- maintain same channel semantics and terrain truth.

High:
- richer normals, wetness/salt masks, denser near-field geology, stronger foam/contact breakup, longer LOD residency;
- no new gameplay truth.

Ultra:
- hero-only 2048/4096 source bakes after static and Unity proof;
- dense offline decal layers, sharper close-waterline PBR, richer reflections/caustic support;
- sensory overkill only, not new route authority.

## Final Disposition

- Preserve useful full sources. Do not destructively crop/remove marks before prototype value is proven.
- No current wet basalt or shell/sand source is final production import.
- `terrain.mat` and `Mat_TriplanarRock.mat` are broken/stale and must not be patched by text.
- `Mat_Terrain.mat` + `TerrainMaster.shader` is the strongest static terrain route candidate.
- `H8_PhoticTerrainLit_1453.shader` and wet basalt photic materials are useful first-party prototypes but lack full PBR roles and Unity proof.
- Foam/contact and caustics require receiver-integrated masks, not visible sheets.
- Shoreline recovery requires geometry replacement with LOD/collider/material proof.

## Pending Verification

- Unity material readback and scene assignment proof.
- Import settings proof for any future texture package.
- Compact/middle/high/ultra screenshots.
- Frame Debugger/RenderGraph proof for shader route.
- Profiler/GC/VRAM proof after any Unity import or scene change.
- Geometry replacement validation and route screenshot proof.

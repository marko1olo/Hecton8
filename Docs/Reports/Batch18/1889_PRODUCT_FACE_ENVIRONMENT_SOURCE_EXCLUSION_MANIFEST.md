# 1889 Product Face Environment Source Exclusion Manifest

Date: 2026-06-04
Agent: 1889
Mode: REPORT_ONLY_STATIC_BOUNDARY_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

This report blocks product-face agents from treating sky, Aegir, moons, ocean, Crest, terrain, coastline, flora, sargassum, depth/noir/weather, or visor glass routes as shortcut texture/material sources for resources, tools, vehicles, transport, or player suit work.

Owned outputs:

- `Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md`
- `Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MATRIX.csv`
- `Docs/Tasks/Status_1889.md`
- `Docs/AgentLogs/Rationale_1889.md`
- `Docs/AgentLogs/LOG_1889.md`

No source, asset, prefab, scene, binary, generated mesh, task file, `.meta`, Unity import, DataMonolith, PlayMode, profiler, or build action was performed.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `water.md`
- `terrain.md`
- `world.md`
- `lighting.md`
- `vfx.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`

`Docs/Actual Domains of Project.txt` was checked and produced no content. Narrow domain used: static environment-source/product-face material boundary.

## Static Inventories Checked

Targeted inventories only:

- `Assets/_Project/Art/Materials`
- `Assets/_Project/Art/TEXTURES`
- `Assets/Crest`

Environment families found in targeted scan:

- celestial and sky materials: `MAT_AegirGasGiant_Impostor_1428`, `MAT_SurfaceCloudPanorama_1428`, `MAT_AegirSky_Master`, `MAT_CelestialMoon_*`, `MAT_H8SurfaceGasGiant*`, `MAT_SurfaceMoonCold_1428`;
- sky and Aegir textures: `clouds0_diff.png`, `Aegir_storms.png`, `TX_H8AegirGasGiantBakedDisc_1428.png`, `TX_H8SurfaceGasGiant*`, `Sky/clod1.png`, `Sky/clod2.png`, `Sky/oblakajip.png`;
- surface ocean and foam materials/textures: `MAT_H8_SurfaceCrestOcean_1428`, `MAT_H8_SurfaceFoamRibbons_1428`, `MAT_H8SurfaceOceanRead_1428`, `MAT_H8SurfaceShoreFoam_1428`, `foam.png`, `TX_H8_SurfaceWaterNormals_1428`, `TX_SurfaceOceanInterference_1428`, `TX_H8SurfaceOceanLongSwell_1428`;
- Crest package materials/textures/shaders: `Assets/Crest/Crest/Materials/Ocean.mat`, `Ocean-Underwater.mat`, `Ocean_UnderwaterCurtain.mat`, `Ocean_UnderwaterMeniscus.mat`, `OceanInputs/*`, `WaveNormals/WaveNormals.png`, `Foam2.png`, `foam.png`, `Caustics_tex_color.png`, `Ocean.shader`, OceanInputs shaders;
- terrain/rock families: `Terrain Textures/basalt`, `2rock`, `rocks`, `gravel`, `mud`, `sand`, `MAT_H8TerrainLit_BasaltSediment_1428`, `MAT_H8SurfaceWetBasaltReal_1428`, `TX_H8TerrainBasaltSediment_1428`, `TX_SurfaceBasaltWetStrata_1428`;
- flora/coral/kelp atlases: `WorldProceduralFlora/TX_Coral*`, `TX_Kelp*`, imported family albedo/detail/mask/normal sets, `MAT_ProceduralBio_Shallows`, `MAT_sargassum_*`;
- sargassum hidden/input materials: `MAT_SargassumOilFilm`, `MAT_SargassumWaveDamping`, `MAT_SargassumFoamDamping`, `MAT_SargassumMicroFaunaBoids`;
- depth/noir/fog/storm materials: `MAT_NoirDepthFog`, `MAT_H8WorldDeepAbyss_1428`, `MAT_H8WorldDepthCurtain_1428`, `MAT_SurfaceStormWater_1428`, `MAT_SurfaceSplashFoamDirty_1428`, `MAT_SurfaceSkyDomeNoir_1428`, `MAT_SurfaceNoirProceduralSkybox_1428`, `TX_SurfaceSkyNoirGradient_1428`, weather fog LUTs;
- visor textures/materials: `visor droplet mask.png`, `visor runoff normal.png`, `Mat_Visor_Glass`, `MAT_VisorFluidDistortion`, `MAT_VisorUberPost`, `MAT_VisorTraumaDeferredDecal`.

## Exclusion Matrix

Machine-readable matrix:

`Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MATRIX.csv`

Rows: 12.

Required classifications covered:

- sky/cloud/Aegir/moons;
- Crest ocean package materials/textures;
- first-party surface ocean/waterline/foam;
- basalt/terrain/rock textures;
- kelp/coral/flora atlases;
- sargassum hidden input masks/materials;
- depth/noir/fog/storm materials;
- visor glass droplet/runoff textures.

Additional boundary rows cover weather fog LUTs, surface/celestial support materials, RuntimeVisualProof material proofs, and controlled product-face derivative outputs.

## Hard Exclusions

Default rule: environment assets are visual reference only. They are not product-face texture or material source.

Hard excluded as product-face source:

- sky, cloud, Aegir, and moon textures/materials;
- Crest package materials, textures, OceanInputs, and shaders;
- first-party surface ocean, foam, waterline, and swell/interference assets;
- basalt, terrain, rock, gravel, mud, sand, wet-strata, and terrain layer assets;
- kelp, coral, flora, and shallow biological atlases/materials;
- sargassum oil, wave damping, foam damping, hidden input masks, and micro-fauna input/presentation materials;
- depth, noir, fog, storm, weather, eclipse, pressure veil, and dirty splash materials;
- weather fog LUTs as albedo/mask/color sources;
- runtime visual proof swatches as production materials.

Conditional only:

- visor droplet/runoff textures remain route-locked to player/UI/visor glass work;
- product-face derivative outputs are allowed only when a manifest proves project-owned albedo, normal, packed mask, shader channel layout, import settings, category, owner approval, and proof path.

## Highest Misuse Risks

1. Crest shortcut cloning: copying Crest `Ocean.mat`, foam, wave normal, caustic, or OceanInputs assets into tools, pickups, vehicles, or suit wetness would violate third-party integrity and create unsupported shader/material dependency.
2. Surface water theft: direct use of first-party ocean, waterline, and foam assets on product-face props would weaken the surface/ocean pillar and fake wetness without category material truth.
3. Terrain swatch theft: direct use of basalt/rock terrain textures for pickups/resources would make resources read as clipped ground material instead of authored product-face objects.
4. Flora atlas theft: direct use of kelp/coral atlases on resource/tool/suit/vehicle parts would imply false ecology and turn product-face assets into generic reef derivatives.
5. Noir/storm concealment: using fog, storm water, dirty foam, or noir gradients to make product-face or normal surface art look acceptable is rejected. Darkness belongs to depth, caves, interiors, storms, and eclipse windows, not normal surface proof.
6. Visor route dilution: visor droplet and runoff maps are part of player instrument identity. Generic reuse as wetness masks on other assets is forbidden unless a new owner-approved derivative exists.

## Required Future Gate

Before any product-face agent uses environment material language as source input, it must produce a source/import/channel manifest with:

- route owner approval;
- derivative texture names and paths under product-face ownership;
- albedo, normal, and packed mask roles;
- shader-specific channel layout;
- import settings and compression;
- category: resource, tool, transport, player suit, visor, organic, glass, rubber, metal, mineral, or fabric;
- Low, Middle, High, and Ultra `GlobalQualityWeight` consequences;
- explicit statement that gameplay truth, save identity, DTO layout, and authority route are unchanged;
- proof state label. Static source remains `STATIC_SOURCE`; visual/runtime claims remain `PENDING VERIFICATION` until Unity/profiler/capture artifacts exist.

## Scaling Consequences

Low: no ugly shortcut mode. Product-face assets still need owned albedo, normal, and packed mask sources at reduced resolution or simpler material response. Environment textures remain references only.

Middle: product-face categories need distinct material families, correct packed masks, and wetness/wear semantics. Environment route assets still cannot be relinked directly.

High: richer product-face source maps may echo environment material language only through approved derivatives. Ocean/terrain/sky ownership stays intact.

Ultra: visual overkill may add wetness, scratches, runoff, detail normals, and stronger masks to product-face materials, but not by consuming Crest, sky, terrain, flora, or route-owned environment assets directly.

## Evidence Claims

Claim: Environment material and texture families exist for sky/cloud/Aegir/moons, Crest ocean, first-party surface ocean/foam, terrain/basalt/rocks, flora/kelp/coral, sargassum, depth/noir/storm, weather LUTs, and visor droplet/runoff.
Evidence Class: STATIC_SOURCE
Artifact: targeted inventories under `Assets/_Project/Art/Materials`, `Assets/_Project/Art/TEXTURES`, and `Assets/Crest`; this report; matrix CSV
Command or Unity tool: PowerShell `Get-ChildItem`
Date: 2026-06-04
Residual risk: no Unity import, scene binding, visual quality, Frame Debugger, profiler, or runtime proof.

Claim: Product-face agents may use those environment families as visual references, not source material.
Evidence Class: STATIC_DOC
Artifact: `AGENTS.md`, `VISION_LOCKS.md`, `TASTE.md`, `water.md`, `terrain.md`, `world.md`, `lighting.md`, `vfx.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, this report
Command or Unity tool: static document read
Date: 2026-06-04
Residual risk: future agents can still violate the boundary unless relink validators enforce it.

Claim: Product-face source reuse is conditional only through owner approval plus derivative channel/import manifest.
Evidence Class: STATIC_DOC
Artifact: this report and `1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MATRIX.csv`
Command or Unity tool: static report authoring
Date: 2026-06-04
Residual risk: no automated validator was implemented in this report-only task.

## Verification Results

Verification commands required by task were run after file creation. Current results are recorded in `Docs/Tasks/Status_1889.md` and `Docs/AgentLogs/LOG_1889.md`.

Static term cross-check targets:

- `sky`
- `Aegir`
- `Crest`
- `ocean`
- `foam`
- `basalt`
- `terrain`
- `kelp`
- `sargassum`
- `noir`
- `storm`
- `depth`

## Final State

STATIC SOURCE EXCLUSION MANIFEST COMPLETE.

No runtime, visual, import, profiler, or Unity acceptance is claimed.

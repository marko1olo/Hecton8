# 1883 Sky/Ocean Material Texture Role Package

Date: 2026-06-04
Agent: 1883
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Report-only material and texture source audit for bright surface sky, Aegir/moons, ocean surface, waterline/foam, photic shallows, and Sargassum micro-fauna.

Owned outputs:

- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Tasks/Status_1883.md`
- `Docs/AgentLogs/Rationale_1883.md`
- `Docs/AgentLogs/LOG_1883.md`

No source, prefab, asset, scene, `.meta`, binary, Unity menu, import, bake, PlayMode, profiler, build, or Data Monolith action was performed.

## Authorities And Inputs

Read:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_PROOF_PACKET.md`
- `Docs/Reports/Batch18/1873_SKY_OCEAN_SOURCE_CLEANUP_AND_PROOF_SLOT_PACKET.md`
- `Docs/Reports/Batch18/1878_SKY_OCEAN_SOURCE_VALIDATOR_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`

`Docs/Actual Domains of Project.txt` was checked and produced no content. Narrow domain used: sky/ocean/material/texture/static proof.

## Evidence Boundary

Static file reads prove candidate paths, serialized material references, prefab YAML fields, and script text only. They do not prove:

- Unity import validity;
- active material binding in GameView;
- first-frame hidden state for Crest input planes;
- sky, Aegir, moons, ocean, waterline, foam, photic-shallow visual quality;
- Frame Debugger pass order;
- profiler, GC, memory, VRAM, or runtime cost;
- Low/Middle/High/Ultra behavior in Unity.

All visual acceptance remains `PENDING UNITY SLOT`.

## Material/Texture Role Matrix

Machine-readable role matrix:

`Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`

Required roles are represented:

- sky dome / clouds;
- Aegir gas giant disc/bands/storms;
- moons/celestial bodies;
- surface ocean water/normal/swell;
- foam/waterline/ribbons;
- Crest hidden input materials;
- Sargassum oil/wave/foam damping;
- SargassumMicroFaunaBoids material/mesh/texture role;
- photic shallows.

## Static Source Findings

### Sky / Clouds

Credible candidates exist:

- `Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat`
- `Assets/_Project/Art/Shaders/H8_SurfaceCloudPanorama_1428.shader`
- `Assets/_Project/Art/TEXTURES/Sky/clod1.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod2.png`
- `Assets/_Project/Art/Models/SkyDome_Inverted.asset`

Serialized material values show dual cloud textures and explicit lit/shadow cloud colors. Prior 1865/1873 static reports show `02_HECTON_WORLD` overrides `Sky_System` to `SkyDome_Inverted.asset` and `MAT_SurfaceCloudPanorama_1428.mat`.

Blocker: `Assets/_Project/Prefabs/Sky_System.prefab` source still contains an enabled built-in primitive sphere. Static scene override reduces one-scene risk; it does not clean the prefab source.

### Aegir

Credible candidates exist:

- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`
- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`
- `Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat`

`GasGiant_Aegir.prefab` uses a non-built-in mesh GUID and material GUID `ab7b03af667690149bdc7be9a1ae023c`. Static audit did not resolve that material path from the prefab alone in the final matrix because no Unity import was run. The role remains candidate-backed but visually unaccepted.

Aegir must remain blue/purple/methane-readable only if band detail, storm masks, limb softness, scale, and capture proof are strong. Procedural sine stripes or muddy flat blobs are rejected.

### Moons

Moon material assets exist:

- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat`
- `Assets/_Project/Art/Shaders/Hecton_CelestialMoon.shader`

Gap: explicit moon texture paths and phase map semantics were not proven in this static pass. The matrix marks moon texture source as `MISSING_SOURCE_REQUIRED` until a future pass resolves albedo/normal/phase texture bindings and active scene route.

### Surface Ocean / Crest

Credible first-party Crest-material candidate:

- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- shader: `Assets/Crest/Crest/Shaders/Ocean.shader`
- normal: `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset`
- foam: `Assets/Crest/Crest/Textures/Foam2.png`
- caustic: `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`

Active static route remains third-party Crest package material:

- `Assets/Crest/Crest/Materials/Ocean.mat`
- `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png`
- `Assets/Crest/Crest/Textures/Foam2.png`
- `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`

Per `AGENTS.md`, do not clone or mutate Crest package materials as a shortcut. Future work may assign a scoped first-party material only through a Unity-owner relink and proof slot. This report does not authorize that edit.

### Foam / Waterline

Credible candidates exist:

- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceShoreFoam_1428.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat`
- `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader`
- `Assets/_Project/Art/TEXTURES/foam.png`

Scene text contains `SURFACE_FOAM_RIBBON` and `H8_WORLD_SHORELINE_FOAM_ONLY` names. Static text does not prove actual waterline quality, depth fade, refraction edge behavior, or absence of flat ribbon artifacts.

### Sargassum Crest Inputs

Existing hidden/input candidates:

- `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat`
- `Assets/_Project/Art/Materials/MAT_SargassumWaveDamping.mat`
- `Assets/_Project/Art/Materials/MAT_SargassumFoamDamping.mat`
- `Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumOilFilm.shader`
- `Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumWaveDamping.shader`
- `Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumFoamDamping.shader`
- `Assets/_Project/Art/Shaders/Hecton_SargassumDampingFacade.compute`

`SargassumCrestDampingController` text shows facade texture ownership for wave damping and oil film, cache of legacy input renderers, and `DisableLegacyInputs()` in `Awake`. `Ocean_Crest.prefab` still contains built-in primitive planes for the three input carriers. They are acceptable only under hidden-input proof criteria from 1873: renderer hidden before player-visible frames, no visible draw, named data-input pass if any, and Frame Debugger proof.

### SargassumMicroFaunaBoids

Existing material/compute:

- `Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat`
- `Assets/_Project/Scripts/BoidFishInstanced.shader`
- `Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute`
- fallback flow texture: `Assets/_Project/Art/TEXTURES/RuntimeFallbacks/TX_H8NeutralAbyssalFlow_1x1x1_1428.asset`

Blocker:

- `Ocean_Crest.prefab` binds `boidMesh` to Unity built-in primitive plane `10209`.
- `boidVatPositionTexture` and `boidVatNormalTexture` are null in prefab YAML.

The script uses `Graphics.RenderMeshIndirect`, boid buffers, optional VAT fields, dither keep, hit flash, and continuous `HomeostasisBrain.GlobalQualityWeight` in static source. That does not make the current mesh acceptable. The visual package still needs authored non-primitive mesh or designed impostor/card/VAT assets plus capture proof.

## Noir / Dark / Storm Risk Classification

Surface normal state is bright/beautiful/readable. Darkness/noir is constrained to depth, caves, interiors, storms, and eclipse windows.

Risk materials:

- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceSkyNoirGradient_1428.mat` - `LEGACY_RISK_EVENT_ONLY`.
- `Assets/_Project/Art/Materials/MAT_SurfaceSkyDomeNoir_1428.mat` - `LEGACY_RISK_EVENT_ONLY`.
- `Assets/_Project/Art/Materials/MAT_SurfaceNoirProceduralSkybox_1428.mat` - `LEGACY_RISK_EVENT_ONLY`, also built-in procedural skybox shader risk.
- `Assets/_Project/Art/Materials/World/MAT_SurfaceStormWater_1428.mat` - `WEATHER_ONLY`.
- `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat` - `WEATHER_ONLY`.
- `Assets/_Project/Art/Materials/MAT_NoirDepthFog.mat` - `DEEP_ONLY`.
- `Assets/_Project/Art/Materials/MAT_H8WorldDeepAbyss_1428.mat` - `DEEP_ONLY`.
- `Assets/_Project/Art/Materials/MAT_H8WorldDepthCurtain_1428.mat` - `DEEP_ONLY`.

Reject any route that uses fog, darkness, bloom, storm state, crushed exposure, silt, or camera crop as the primary proof for normal surface, sky, Aegir, ocean, waterline, or photic shallows.

## Shader / Texture Semantics

Required semantics for future source package:

- Sky/cloud panorama: cloud texture A/B are luminance/alpha cloud masks; color controls lit cloud, shadow cloud, softness, threshold, and opacity. Horizon must remain readable.
- Aegir: main disc/band texture is color/detail authority; storm texture is overlay/mask; limb/halo is shader-controlled softness; phase/planet-shine is celestial state, not texture swap truth.
- Moons: each moon needs albedo, optional normal, phase/terminator semantic, and body identity. Missing explicit texture path remains a source gap.
- Ocean surface: normals drive wave/swell/refraction; foam texture is white/bubble breakup; caustic texture is optional sensory layer; smoothness/specular must preserve real ocean skin.
- Shore foam/ribbons: base/foam mask controls translucent waterline edge, not opaque strips; depth fade and soft edges required.
- Sargassum oil/wave/foam inputs: hidden facade masks generated from density and cut mask. They are data inputs, not visible art.
- Micro-fauna: `StructuredBuffer<BoidData>` drives instance transform/state; optional VAT position/normal textures provide authored biological motion; base material color alone is insufficient for final fauna identity.

## Continuous Quality Scaling

`GlobalQualityWeight` is a continuous scalar. Do not create low/high art switches.

- Compact: normal surface stays bright and readable. Keep authored sky/cloud silhouette, Aegir/moon silhouettes, ocean color, specular, sparse foam, and clean waterline. Use lower facade resolution, lower boid count/cadence, fewer foam ribbons, simpler cloud layers. No ugly mode.
- Middle: add richer cloud motion, stronger wave normal response, more foam/waterline breakup, better shallow clarity, and moderate micro-fauna density.
- High: add reflection/specular richness, Aegir limb softness, moon phase detail, stronger foam/refraction, better sargassum facade fidelity, and optional VAT motion.
- Ultra: spend saved budget on visual overkill: layered clouds, planet-shine/halo, richer waterline and foam, shallow caustic hints, dense but readable micro-fauna, longer visual residency. No new gameplay truth.

## Future Unity Proof Steps

From 1873/1878/1879, future owner must run one uncontested Unity slot:

1. Confirm no Unity/build/profiler/import/DataMonolith owner is active and no forbidden build process is running.
2. Open Unity only after slot is clean; wait for compile/import readiness.
3. Run `Hecton8/Validation/Sky-Ocean Source Primitive Gate`.
4. Inspect active `Sky_System`, `Ocean_Crest`, Aegir, moons, Crest inputs, and micro-fauna bindings.
5. Capture normal daylight surface, shore/waterline, photic shallows, Aegir/moons, storm, eclipse, night, and medium-depth matched shots.
6. Capture Frame Debugger proof for sky/clouds/Aegir/moons/ocean/foam/Crest inputs/micro-fauna.
7. Capture profiler/GC proof for celestial, Crest, sargassum damping, and micro-fauna routes.
8. Capture Compact, Middle, High, and Ultra matched camera comparisons. Storm/night/eclipse cannot replace normal daylight proof.

## Evidence Claims

Claim: Material/texture candidates exist for sky/clouds, Aegir, surface ocean, foam, Crest inputs, and sargassum micro-fauna.
Evidence Class: STATIC_SOURCE
Artifact: material/texture paths listed in `1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`
Command or Unity tool: PowerShell `Get-ChildItem`, `Select-String`, `rg`
Date: 2026-06-04
Residual risk: no Unity import, scene binding, or visual proof.

Claim: Moon material assets exist, but explicit moon texture role paths remain unresolved in this audit.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_*.mat`
Command or Unity tool: static file inventory
Date: 2026-06-04
Residual risk: moon texture/package semantics require future source inspection or Unity proof.

Claim: `SargassumMicroFaunaBoids` remains a product-face primitive risk because prefab `boidMesh` is Unity built-in plane `10209`.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Prefabs/Ocean_Crest.prefab`
Command or Unity tool: static prefab read / `rg`
Date: 2026-06-04
Residual risk: runtime may hide or stylize it, but no capture proof exists.

Claim: Surface/noir/storm/depth materials must be constrained by route scope, not used to hide weak surface art.
Evidence Class: STATIC_DOC / STATIC_SOURCE
Artifact: `AGENTS.md`, `VISION_LOCKS.md`, `TASTE.md`, this report
Command or Unity tool: static doc/material review
Date: 2026-06-04
Residual risk: actual scene usage requires Unity/Frame Debugger proof.

## Final State

STATIC MATERIAL/TEXTURE ROLE PACKAGE COMPLETE.

No visual acceptance is claimed. Sky, Aegir, moons, ocean, waterline, foam, photic shallows, Crest input hidden state, and micro-fauna presentation remain `PENDING UNITY SLOT`.

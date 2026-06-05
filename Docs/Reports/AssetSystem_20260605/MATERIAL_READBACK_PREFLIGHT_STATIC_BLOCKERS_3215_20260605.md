# Material Readback Preflight Static Blockers - Asset Worker 3215 - 2026-06-05

Status: `PENDING UNITY READBACK`.
Evidence boundary: `STATIC_SOURCE` / `STATIC_YAML_SCAN` only.
Write scope: report only. No `.mat`, `.prefab`, `.unity`, `.asset`, importer, Unity, Play Mode, build, or project setting mutation was performed.

First-20 route moment: addresses readback ambiguity for bright surface exit, Aegir/sky/moon visibility, ocean/shoreline foam, photic shallows, and medium-depth route material proof.

Mandates followed:

- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `rendering.md`
- `water.md`
- `terrain.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`

No visual/material acceptance claims are made in this report.

## Exact Scene Targets

Read these in Unity later without saving or applying changes:

- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
  - `m_SkyboxMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - GUID: `c94a1beef2372b8458941c2ed9d05d5e`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `m_SkyboxMaterial: {fileID: 0}`. Confirm menu sky/lighting fallback path, but do not assign anything in this pass.
- `Assets/_Project/Scenes/01_ORBIT.unity`
  - `m_SkyboxMaterial` -> `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
  - GUID: `6a3f1601ae9165f4a001000000000002`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - `m_SkyboxMaterial` line 29 -> `Mat_HectonSky.mat`
  - `oceanUnderwaterMaterial` line 4651 -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - `skyMaterial` line 4652 -> `Mat_HectonSky.mat`
  - `_skyMaterial` line 90895 -> `Mat_HectonSky.mat`
  - `daySkybox` line 91165 -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - `nightSkybox` line 91166 -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
  - `blendedSkyboxMaterial` line 91167 -> `Mat_HectonSky.mat`
  - Cloud deck renderers: `MAT_H8SurfaceCloudDeck_1428.mat` at lines 24048, 52175, 71017.
  - Gas giant disc renderer: `MAT_H8SurfaceGasGiantDisc_1428.mat` at line 54649.
  - Gas giant surface renderer: `MAT_SurfaceGasGiant_1428.mat` at line 97304.
  - Aegir haze renderer: `Mat_AegirHazeOverlay.mat` at line 41764.
  - Aegir impostor prefab override: `MAT_AegirGasGiant_Impostor_1428.mat` at line 89893.
  - Aegir sky material renderer: `MAT_AegirSky_Master.mat` at line 94882.
  - Foam ribbon/splash renderers: `MAT_SurfaceSplashFoamDirty_1428.mat` at lines 24471, 37319, 65743.
  - Crest foam input renderer: `MAT_H8_CrestFoamInput_1464.mat` at line 38733.
  - Crest `RegisterFoamInput` component starts at line 38698.
  - Crest `ShapeGerstnerBatched` component starts at line 48339. Spectrum GUID resolves to `Assets/_Project/Art/Materials/World/Photic1457/SPEC_H8_SurfaceReadableWaves_1457.asset`.
  - Crest `UnderwaterRenderer` component starts at line 67216 and has `_copyOceanMaterialParamsEachFrame: 1`.
  - Active proxy material refs:
    - `MAT_family_coral_branching.mat` line 100083.
    - `MAT_family_coral_massive.mat` line 22880.
    - `MAT_family_coral_plate.mat` line 13734.
    - `MAT_family_kelp_patch_dense.mat` line 81248.

## Exact Material And Shader Targets

### Sky, Clouds, Aegir

- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - Material GUID: `c94a1beef2372b8458941c2ed9d05d5e`
  - Shader: `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
  - Shader GUID: `6302a783d2378694c9db8d0036358965`
  - `_MainCloudTex`: null in static YAML.
  - `_HighCloudTex`: GUID `97dacc0c8637b304f9451ecd290acffb`; no `Assets/**/*.meta` hit.
  - `_MainCloudAtlas`: GUID `161f2ad7f77e8bf408b29aa7e3d29966`; no `Assets/**/*.meta` hit.
  - Extra texture slot at line 105 resolves to `Assets/_Project/Art/TEXTURES/Sky/bo2.png`.
- `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
  - Material GUID: `aef05365f93ac30409951ec591f40822`
  - Shader: `Hecton_AlienSky_Master.shader`
  - `_MainCloudTex`: `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
  - `_HighCloudTex`: same missing GUID `97dacc0c8637b304f9451ecd290acffb`.
  - `_MainCloudAtlas`: same missing GUID `161f2ad7f77e8bf408b29aa7e3d29966`.
  - Target readback: confirm whether this overlay is active at runtime; scene GUID scan did not find a direct `02_HECTON_WORLD` material ref.
- `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
  - Material GUID: `6a3f1601ae9165f4a001000000000002`
  - Shader GUID in material: `6a3f1601ae9165f4a001000000000001`; no `Assets/**/*.meta` hit.
  - `_AegirBandTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
  - Material GUID: `ab7b03af667690149bdc7be9a1ae023c`
  - Shader: `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`
  - `_DetailTex`: `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
  - `_MainTex`: `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
  - `_StormTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
  - Material GUID: `2792169a3d108184d9d7915f0d2e464a`
  - Shader: `Assets/_Project/Art/Shaders/SG_GasGiant_Master.shader`
  - `_CelestialOcclusionTex`: `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
  - `_EmissionTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
  - `_MainTex`: `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
  - Static scene/prefab scan did not find this material directly active in production scenes; still read it because texture usage review listed it as a sky/Aegir route candidate.
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`
  - Material GUID: `61f029642960ddc4eaf2f619724d34ea`
  - Shader: `Assets/_Project/Art/Shaders/Hecton_SurfaceCloudDeck_1428.shader`
  - `_BaseMap`: `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`
  - Material GUID: `34171e07d1f312044bd74385d37d8c93`
  - Shader GUID in material: `650dd9526735d5b46b79224bc6e94025`; no `Assets/**/*.meta` hit.
  - `_BaseMap` and `_MainTex`: `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceGasGiant_1428.mat`
  - Material GUID: `89948bc29e432d442bd2ecda75556ef6`
  - Shader GUID in material: `650dd9526735d5b46b79224bc6e94025`; no `Assets/**/*.meta` hit.
  - `_BaseMap` and `_MainTex`: `Assets/_Project/Art/TEXTURES/TX_H8SurfaceGasGiantStormBands_1428.asset`
- `Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat`
  - Material GUID: `89dae82c1103b4447b57e228710c19c0`
  - Shader GUID in material: `650dd9526735d5b46b79224bc6e94025`; no `Assets/**/*.meta` hit.
  - `_BaseMap` and `_MainTex`: `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png`
- `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat`
  - Material GUID: `a52b971a4295e1247930e9b8c430c34b`
  - Shader: `Assets/_Project/Art/Shaders/Hecton_AegirHazeOverlay.shader`
  - Static texture slots: none.

### Day/Night Skybox Fields

- `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - Material GUID: `adb9fe9be55e5c240a9028e61ecea987`
  - Shader: built-in skybox/panoramic GUID `0000000000000000f000000000000000`
  - `_Tex`: `Assets/_Project/Art/Skyboxes/panorama_den.png`
  - Other serialized texture slots are null.
- `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
  - Material GUID: `e6841cd12af6d9b42a1016d5492aa4b1`
  - Shader: built-in skybox/panoramic GUID `0000000000000000f000000000000000`
  - `_Tex`: `Assets/_Project/Art/Skyboxes/panorama_noch.png`
  - Other serialized texture slots are null.

### Moon / Celestial Material Candidates

Read these because prior evidence said moon materials still reuse terrain/rock textures:

- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat`
  - `_BaseMap` / `_MainTex`: `Assets/_Project/Art/TEXTURES/Terrain Textures/rocks/Rocks019_1K-JPG_Color.jpg`
  - `_BumpMap` and `_EmissionMap`: null in static YAML.
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat`
  - `_BaseMap` / `_MainTex`: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`
  - `_BumpMap` and `_EmissionMap`: null in static YAML.
- Shared moon shader: `Assets/_Project/Art/Shaders/Hecton_CelestialMoon.shader`.

These are readback blockers, not rejection proof. Unity owner must verify whether these materials are active in visible moon renderers before any art decision.

### Crest / Ocean / Foam

- `Assets/Crest/Crest/Materials/Ocean.mat`
  - Material GUID: `9def92ac79181fe41b238e91663f0fad`
  - Shader: `Assets/Crest/Crest/Shaders/Ocean.shader`
  - `_FoamTexture`: `Assets/Crest/Crest/Textures/Foam2.png`
  - `_Normals`: `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png`
  - `_CausticsTexture`: `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`
  - Several texture fields serialize GUIDs with no `Assets/**/*.meta` hit: `33331381cbc5c564583cd5e47314cf78`, `f9a8c5bb065e21748a23f214a1f3a250`, `ba628b5ad7a570e4b95c3ee64a5c605d`, `6b165028befdf0745b04ebdfbf672681`, `e94a5d7132329854281515fe36afb70e`. Treat as Unity readback required; they may be package/generated/internal fields, but static Assets-only lookup cannot prove that.
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - Material GUID: `ef94c26e44a36e24a9dcbc5995a2bed1`
  - Same Crest shader.
  - `_FoamTexture`: `Assets/Crest/Crest/Textures/Foam2.png`
  - `_Normals`: `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png`
  - `_CausticsTexture`: `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`
  - Same missing internal texture GUID set as `Ocean.mat`.
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
  - Material GUID: `cb6742dd8bbf8d843ba150a5e6dd5eb9`
  - Same Crest shader.
  - `_FoamTexture`: `Assets/Crest/Crest/Textures/Foam2.png`
  - `_Normals`: `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset`
  - It is a first-party Crest-ocean material candidate. Confirm whether it is active. Do not clone, patch, or wrap Crest materials at runtime.
- `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`
  - Material GUID: `58c0ef725a796d243b4eb522bb560ff8`
  - Shader: `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader`
  - `_BaseMap`: `Assets/Crest/Crest/Textures/foam.png`
  - Static disposition from asset index: `foam.png` is rejected visible support only, but this report does not make visual acceptance claims.
- `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
  - Material GUID: `2e3b1edbfc811f74d8f8af3317d6c105`
  - Shader: `Assets/Crest/Crest/Shaders/OceanInputs/FoamAddFromVertCol.shader`
  - Static texture slots: none.

### Terrain / Geology

- `Assets/_Project/Art/Materials/Mat_Terrain.mat`
  - Material GUID: `855da5d65cd929c41a0946f4f04488de`
  - Shader: `Assets/_Project/Art/Shaders/TerrainMaster.shader`
  - `_FlowNormal`: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`
  - Basalt color slot: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`
  - `_SandTex`: `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png`
- `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
  - Material GUID: `49fab836122f5a14ab7898cf9d4b976c`
  - Shader GUID in material: `74659692f6350ba46b88180d9c826630`; no `Assets/**/*.meta` hit.
  - Most texture slots are null.
  - Texture GUID `47f0a231c050423488e0ff6f7d66f813` has no `Assets/**/*.meta` hit.
- `Assets/_Project/Art/Materials/terrain.mat`
  - Material GUID: `cd2a36a0be9c9d949a2d382b10b639f0`
  - Shader GUID in material: `58f9232bdfcb5064f9b47d1dddb46260`; no `Assets/**/*.meta` hit.
  - Texture GUID `47f0a231c050423488e0ff6f7d66f813` repeats in `_BaseMap`, `_MainTex`, and another texture slot; no `Assets/**/*.meta` hit.
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`
  - Material GUID: `78f932044d4618343af64670364d1a2f`
  - Shader GUID in material: `933532a4fcc9baf4fa0491de14d08ed7`; Assets-only meta lookup does not resolve it. It may be package/built-in; Unity readback must decide.
  - `_BaseMap` / `_MainTex`: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
  - `_BumpMap`: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`
- `Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`
  - Material GUID: `409048a8e1f182e4c81eacb0ab5ab469`
  - Same shader GUID `933532a4fcc9baf4fa0491de14d08ed7`.
  - `_BaseMap` / `_MainTex`: `TX_H8_WetBasaltShoreline_Albedo_1428.png`
  - `_BumpMap`: null in static YAML.
- `Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader`
  - Required readback target because `ASSET_SYSTEM_INDEX_20260605.md` calls it a terrain/prototype route candidate.
- `Assets/_Project/Art/Shaders/TerrainMaster.shader`
  - Required readback target for the strongest static terrain material route.

### Flora / Geology Proxy Refs

The four proxy materials directly referenced by `02_HECTON_WORLD.unity` are readback blockers until replaced or proven route-safe:

- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
  - Material GUID: `258b2520dce86ef4f906901838cf9f88`
  - Shader GUID: `b162c13e398ef054cbaab6dc14c8661f`
  - Direct `02_HECTON_WORLD` refs in static scan: 1.
  - Usage review says visible users: 10.
  - Slots: `_BaseMap`, `_DetailMap`, `_MaskMap`, `_NormalMap`; `_BumpMap`, `_MainTex`, `_MetallicGlossMap`, `_OcclusionMap` are null.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat`
  - Material GUID: `a688ea3cea12e9446854797cf70f1d8b`
  - Shader GUID: `b162c13e398ef054cbaab6dc14c8661f`
  - Direct `02_HECTON_WORLD` refs in static scan: 1.
  - Usage review says visible users: 9.
  - Slots: `_BaseMap`, `_DetailMap`, `_MaskMap`, `_NormalMap`; `_BumpMap`, `_MainTex`, `_MetallicGlossMap`, `_OcclusionMap` are null.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat`
  - Material GUID: `89806719d9249ca4b8589c28decd50bb`
  - Shader GUID: `b162c13e398ef054cbaab6dc14c8661f`
  - Direct `02_HECTON_WORLD` refs in static scan: 1.
  - Usage review says visible users: 9.
  - Slots: `_BaseMap`, `_DetailMap`, `_MaskMap`, `_NormalMap`; `_BumpMap`, `_MainTex`, `_MetallicGlossMap`, `_OcclusionMap` are null.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat`
  - Material GUID: `9a61a69f8720a03409a0b5ff5e899170`
  - Shader GUID: `446dc86f53b9a6e46aff1b420e3334f5`
  - Direct `02_HECTON_WORLD` refs in static scan: 1.
  - Usage review says visible users: 16.
  - Slots: `_BaseMap`, `_DetailMap`, `_MaskMap`, `_NormalMap`; `_BumpMap`, `_MainTex`, `_MetallicGlossMap`, `_OcclusionMap` are null.

## Static Blockers And Risks

- `Mat_HectonSky.mat` is active in `00_BOOTSTRAP` and `02_HECTON_WORLD`, but `_MainCloudTex` is null and `_HighCloudTex` / `_MainCloudAtlas` point to GUIDs with no `Assets/**/*.meta` hit.
- `Mat_HectonSky_CloudOverlay.mat` has a valid `_MainCloudTex` but repeats the same missing `_HighCloudTex` / `_MainCloudAtlas` GUIDs.
- `MAT_AegirSky_Master.mat` is active as `01_ORBIT` skybox and as a `02_HECTON_WORLD` renderer material, but its shader GUID has no `Assets/**/*.meta` hit.
- `MAT_H8SurfaceGasGiantDisc_1428.mat`, `MAT_SurfaceGasGiant_1428.mat`, and `MAT_H8AegirGasGiantReal_1428.mat` all use shader GUID `650dd9526735d5b46b79224bc6e94025`, with no `Assets/**/*.meta` hit. Do not assume stale; Unity must distinguish package/built-in from missing asset.
- Moon materials serialize terrain/rock textures and mostly null normal/emission maps. This is a hero-celestial readback risk, not final rejection proof.
- `MAT_SurfaceSplashFoamDirty_1428.mat` uses `Assets/Crest/Crest/Textures/foam.png`, already marked support/rejected-visible in the asset index. It is active in `02_HECTON_WORLD`.
- Crest `Ocean.mat`, `Ocean-Underwater.mat`, and `MAT_H8_SurfaceCrestOcean_1428.mat` must be read as actual Unity material instances. Do not create wrappers, clones, or runtime overrides.
- `MAT_H8_SurfaceCrestOcean_1428.mat` is a first-party material using the Crest ocean shader. Confirm whether it is active; the scene scan found active `Ocean.mat` through prefab override and `Ocean-Underwater.mat` through `oceanUnderwaterMaterial`.
- `Mat_TriplanarRock.mat` and `terrain.mat` have missing shader/texture GUIDs in Assets-only lookup and many null slots. The asset index already calls them stale/broken in prior evidence.
- `Mat_Terrain.mat` is the strongest static terrain route, but active scene use was not proven by this pass. Unity readback must confirm terrain receiver binding and shader property validity.
- The four `WorldProceduralProxy` flora/coral/kelp materials are direct `02_HECTON_WORLD` refs and remain visible-route contamination until replaced or proven route-safe.
- `Assets/AddressableAssetsData` has no static proof for these heavy material/texture routes in the asset index. Material binding is not residency proof.
- Package/built-in GUIDs can legitimately miss an `Assets/**/*.meta` lookup. Treat missing Assets meta as a readback blocker, not automatic deletion proof.

## No-Mutation Unity Readback Checklist

Gate before opening Unity or triggering import/build/play:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 3
Get-Process dotnet,csc,Unity -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,CPU,StartTime
```

Do not start Unity/readback if CPU is over 50 percent, or `dotnet`/`csc.exe` is active. If Unity is already open, do not enter Play Mode or save scenes during this pass.

Readback actions:

- Open `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD` one at a time.
- Confirm active RenderSettings skybox for each scene.
- In `02_HECTON_WORLD`, inspect the scripts/objects holding `skyMaterial`, `_skyMaterial`, `daySkybox`, `nightSkybox`, `blendedSkyboxMaterial`, and `oceanUnderwaterMaterial`.
- Read `Mat_HectonSky.mat` effective shader and effective texture bindings for `_MainCloudTex`, `_HighCloudTex`, `_MainCloudAtlas`, plus all Aegir/cloud scalar fields.
- Read `Mat_HectonSky_CloudOverlay.mat` if runtime objects reference it; otherwise record as static candidate only.
- Read Aegir material slots for `MAT_AegirSky_Master`, `MAT_AegirGasGiant_Impostor_1428`, `Mat_GasGiant`, `MAT_H8SurfaceGasGiantDisc_1428`, `MAT_SurfaceGasGiant_1428`, `MAT_H8AegirGasGiantReal_1428`, and `Mat_AegirHazeOverlay`.
- Read moon materials `MAT_CelestialMoon_*` and record whether the rock/basalt texture reuse is actually visible in the route scene.
- Read Crest material slots on the active OceanRenderer/prefab instance:
  - active ocean material
  - underwater material
  - `_FoamTexture`
  - `_Normals`
  - `_CausticsTexture`
  - foam toggles and wave foam scalars
  - `RegisterFoamInput` renderer material
  - `UnderwaterRenderer._copyOceanMaterialParamsEachFrame`
- Confirm `MAT_SurfaceSplashFoamDirty_1428` active users and whether `Assets/Crest/Crest/Textures/foam.png` contributes to visible shoreline/ocean contact.
- Read active terrain route:
  - terrain component material/template
  - `Mat_Terrain.mat`
  - `Mat_TriplanarRock.mat`
  - `terrain.mat`
  - `MAT_H8_HeroWetBasaltRock_1453.mat`
  - `MAT_H8_AuthoredWetBasaltBreakup_1465.mat`
  - `TerrainMaster.shader`
  - `H8_PhoticTerrainLit_1453.shader`
- Read flora/geology proxy refs:
  - `MAT_family_coral_branching.mat`
  - `MAT_family_coral_massive.mat`
  - `MAT_family_coral_plate.mat`
  - `MAT_family_kelp_patch_dense.mat`
  - exact renderer/object names using them
  - whether proxy materials are in any camera-visible route slice
- Capture required proof after readback:
  - Unity Console after scene load, filtered for material/shader/import warnings.
  - Game View screenshot of surface sky/Aegir/moons.
  - Game View screenshot of ocean surface/shoreline foam.
  - Game View screenshot of photic terrain/flora proxy area.
  - Scene View screenshot showing selected readback objects/material refs.
  - Stats overlay: SetPass, batches, tris/verts, texture memory if available.
  - Frame Debugger: skybox pass, cloud/gas giant renderers, Crest ocean/foam, terrain material draw, proxy material draw if visible.
  - No visual acceptance statement until screenshots and Frame Debugger are reviewed against `TASTE.md`/rendering/water/terrain bibles.

Forbidden during readback:

- Do not press Apply on prefabs.
- Do not save scenes.
- Do not edit material slots.
- Do not reimport.
- Do not run Play Mode unless a clean gate and explicit proof scope exist.
- Do not create Crest runtime wrappers, material clones, or override scripts.
- Do not raw YAML patch `.mat`, `.prefab`, `.unity`, or `.asset` files.

## Static Commands Used / Re-run Targets

These are no-mutation static checks:

```powershell
rg -n "m_SkyboxMaterial|daySkybox|nightSkybox|blendedSkyboxMaterial|_skyMaterial|skyMaterial|oceanUnderwaterMaterial" Assets\_Project\Scenes\00_BOOTSTRAP.unity Assets\_Project\Scenes\01_MAIN_MENU.unity Assets\_Project\Scenes\01_ORBIT.unity Assets\_Project\Scenes\02_HECTON_WORLD.unity
rg -n "Mat_HectonSky|_MainCloudTex|_HighCloudTex|_MainCloudAtlas|Aegir|Crest|Ocean|foam|Mat_Terrain|Mat_TriplanarRock|WorldProceduralProxy" Assets Docs\AssetAudit
Select-String -Path Assets\_Project\Art\Materials\Mat_HectonSky.mat -Pattern "m_Name:|m_Shader:|_MainCloudTex|_HighCloudTex|_MainCloudAtlas" -Context 0,1
Select-String -Path Assets\Crest\Crest\Materials\Ocean.mat,Assets\Crest\Crest\Materials\Ocean-Underwater.mat -Pattern "m_Name:|m_Shader:|_FoamTexture|_Normals|_CausticsTexture|_Foam" -Context 0,1
```

For GUID lookup, use `^guid:` only. Plain `guid:` can match embedded subreferences inside `.meta` files and produce false positives.

## Scalability Readback Consequences

- Low/compact: readback must prove surface sky, ocean, foam, and photic terrain keep material identity when expensive effects are reduced. Missing cloud/atlas refs cannot be hidden by fog, darkness, or bloom.
- Middle: readback must prove route-owned PBR stacks, clean foam/contact masks, and stable cloud/Aegir bindings before density or lighting is increased.
- High: readback must identify which valid materials can receive richer detail normals, reflections, longer LOD residency, or stronger water response.
- Ultra: readback must identify overkill-safe material paths for richer Aegir/cloud detail, shore breakup, water response, and route dressing without changing gameplay truth or DTO/save authority.

## Regression Model For Later Owner

- CPU: this report changes nothing. Unity readback must not add runtime scripts or material polling.
- GC: this report changes nothing. Future material systems need 0 B/frame proof if runtime code changes.
- Memory/VRAM: static texture/material reachability is not residency proof. Addressables, texture memory, and mip residency remain blocked.
- Cadence: this report changes nothing.
- Correctness: blockers prevent false promotion of sky, Aegir, ocean, terrain, and proxy materials before Unity readback.

Final status: `PENDING UNITY READBACK`.

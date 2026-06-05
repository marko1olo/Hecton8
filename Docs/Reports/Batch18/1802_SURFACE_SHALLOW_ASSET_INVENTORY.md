# Agent 1802 Surface Shallow Visual Asset Inventory

ID: 1802
Role: SURFACE_SHALLOW_VISUAL_ASSET_INVENTORY
Proof mode: STATIC VERIFIED unless marked otherwise. No Unity/editor/runtime screenshot, import, profiler, or in-game quality proof was produced in this task.

## Static Proof Rules

- STATIC VERIFIED means the path exists and was inspected by file scan, YAML, texture metadata, or local image view.
- CANDIDATE means the asset exists but usability, import health, or visual fit is not proven.
- REJECTED_PLACEHOLDER means the asset should not be used as final production reference.
- PENDING UNITY SLOT means the asset requires Unity visual/import/runtime verification.
- No asset in this report is marked production-ready from path existence.

## Fresh Evidence Verification

| Lead | Result | Proof State | Notes |
|---|---|---|---|
| `Assets/Screenshots/h8_water_ui_baseline_before_08.png` | Exists and viewed | STATIC VERIFIED baseline image | 1008x567. Readable UI/horizon, but coast is grey/procedural-looking, waterline is sparse, Aegir integration is harsh. |
| `Assets/Screenshots/h8_scene_water_ui_baseline_before_08.png` | Exists and viewed | STATIC VERIFIED baseline image | 1008x591. Scene view confirms same surface weakness and visible editor guides. |
| `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/Hecton8_Surface.prefab` missing material claim | Not proven statically | CANDIDATE, PENDING UNITY SLOT | YAML has two renderer material refs to `Mat_HectonSurface.mat`. The collider material slot is null, which is not a render material proof. |
| Aegir baked disc and storm assets | Exist | CANDIDATE | `TX_H8AegirGasGiantBakedDisc_1428.png` is 2048x2048. `Aegir_storms.png` under prologue is 4096x2048 but imports max 2048. |
| Wet basalt/coast assets | Mostly exist | CANDIDATE | Wet basalt texture assets exist. Provided terrain-layer path was wrong; actual file is `Assets/_Project/Data/H8_TerrainLayer_AbyssBasalt_1428.terrainlayer`. |
| Finalized rocks | Exist | CANDIDATE | `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/` has 49 prefabs. Representative prefabs contain LODGroups and material refs. No visual proof. |
| Photic biota assets | Exist | CANDIDATE | Textures, generated mesh assets, proxy prefabs, baked prefab families, and placement rules exist. No Unity placement proof. |
| Placeholder families | Exist | REJECTED_PLACEHOLDER | `WorldRuntime/ProceduralPlaceholders` prefab/material families are not final references. |

## Inventory

### Ocean Skin

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Crest ocean core | `Assets/_Project/Prefabs/Ocean_Crest.prefab`; `Assets/Crest/Crest/Materials/Ocean.mat`; `Assets/Crest/Crest/Shaders/Ocean.shader` | Third-party prefab/material/shader | Primary ocean surface | CANDIDATE, PENDING UNITY SLOT | Prefab YAML references Crest `Ocean.mat`, `Spectrum.asset`, shadow settings, and sargassum input materials. | Third-party package. Configure via Unity slots or first-party bridge only. Some prefab refs are null or optional: camera/viewpoint/time/light, tile prefab, several sim settings. |
| Crest wave/foam/caustics textures | `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png`; `Assets/Crest/Crest/Textures/Foam2.png`; `Assets/Crest/Crest/Textures/foam.png`; `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png` | Third-party PNG textures | Normals, foam, caustic breakup | CANDIDATE | WaveNormals 1024x1024, sRGB off, normal type. Foam2 450x450 sRGB. Foam 512x512 sRGB. Caustics 630x630 sRGB, bilinear/filter mode 2. | Good utility, not final beauty proof. Do not mutate Crest package textures. |
| H8 Crest ocean material | `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`; `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset` | First-party material and Texture2D asset | Project ocean color/specular/foam tuning on Crest shader | CANDIDATE | Material uses Crest ocean shader, `_FOAM_ON`, `_CAUSTICS_ON`, `_ALBEDO_ON`; references Crest Foam2 and Caustics plus first-party normal texture. | Strongest existing ocean skin candidate, but must be assigned and compared in Unity. |
| Surface read/wave meshes | `Assets/_Project/Art/Meshes/World/MESH_H8SurfaceOceanRead_1428.asset`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceOceanWavefield_1428.asset`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceWaterPatch_1428.asset`; `Assets/_Project/Art/Materials/World/MAT_H8SurfaceOceanRead_1428.mat` | Meshes/material | Readable surface cards, water patches, long-swell reads | CANDIDATE | `MAT_H8SurfaceOceanRead_1428.mat` references `TX_H8SurfaceOceanLongSwell_1428.asset`. | Could help readability but may look card-like if overused. Needs camera-distance proof. |
| Surface foam ribbons | `Assets/_Project/Art/Meshes/World/MESH_H8SurfaceShoreFoamRing_1428.asset`; `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat`; `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader` | Mesh/material/shader | Shoreline waterline breakup | CANDIDATE | Foam ribbon material is colored but static scan found no base texture assigned. | Existing route, but weak if left as flat color. Assign texture or bake mask. |
| Legacy/simple water | `Assets/_Project/Prefabs/Hecton Ocean.prefab`; `Assets/HectonWaterMesh.asset`; `Assets/_Project/_Archive/Mat_Ocean.mat`; `Assets/_Project/Art/Materials/Mat_Water.mat` | Prefab/mesh/material | Legacy or fallback water | CANDIDATE | `Hecton Ocean.prefab` references `HectonWaterMesh.asset` and archived `Mat_Ocean.mat`. | Use only as fallback reference. It is not the premium route. |

### Underwater Optics

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Water optics runtime | `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs`; `Assets/_Project/Scripts/Rendering/WaterOptics/HectonWaterOpticsTelemetryFeature.cs`; `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin` | Runtime scripts/data | Underwater attenuation, optical telemetry | CANDIDATE, PENDING UNITY SLOT | Static path only. | Needs runtime feature ordering and visual proof. |
| Ocean single pass | `Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassRuntime.cs`; `Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs` | Renderer feature/runtime scripts | Ocean compositing path | CANDIDATE, PENDING UNITY SLOT | Static path only. | Must not schedule/readback hot loops without profiler proof. |
| Deferred caustics | `Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs`; `Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs`; `Assets/_Project/Art/Shaders/Hecton_DeferredCaustics.shader`; `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs` | Renderer feature/shader/service | Shallow caustics, depth cueing | CANDIDATE | Shader and feature paths exist. | Shallows require bright premium caustics, not deep-only darkness. Needs screenshot proof. |
| Shoreline foam graft | `Assets/_Project/Editor/ShorelineFoamGraftEditorTools.cs`; `Assets/_Project/Data/shoreline_foam_profiles.csv`; `Assets/_Project/Scripts/Rendering/WaterOptics/ShorelineFoamGraftContracts.cs`; `Assets/_Project/Scripts/Rendering/WaterOptics/ShorelineFoamGraftGizmos.cs` | Editor tools/contracts/data | Waterline foam placement and review | CANDIDATE | Static CSV/tool path exists. | Use offline/editor placement. Runtime generation risk if used as live hero substitute. |

### Coast/Rock

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Wet basalt materials | `Assets/_Project/Art/Materials/World/MAT_H8SurfaceWetBasaltReal_1428.mat`; `Assets/_Project/Art/Materials/World/MAT_SurfaceIslandWetBasalt_1428.mat`; `Assets/_Project/Art/TEXTURES/TX_H8SurfaceBasaltWetSediment_1428.asset`; `Assets/_Project/Art/TEXTURES/TX_SurfaceBasaltWetStrata_1428.asset` | Materials/Texture2D assets | Coast/island wet rock breakup | CANDIDATE | Wet basalt materials reference `TX_H8SurfaceBasaltWetSediment_1428.asset`; `MAT_H8SurfaceWetBasaltReal_1428.mat` also references basalt normal JPG. Detail albedo/normal slots are empty. | Good base, but not enough for hero coast. Needs detail/mask completion and Unity close-up proof. |
| Surface island/coast meshes | `Assets/_Project/Art/Meshes/World/MESH_H8SurfaceCoastalIsland_1428.asset`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceCoastlineJagged_1428.asset`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceIslandJagged_1428.asset`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceBasaltStack_1428.asset` | Mesh assets | Coast silhouette and foreground rock stacks | CANDIDATE | Static asset presence only. | Baseline screenshot suggests current island/coast reads procedural and flat. Needs better material layering and composition. |
| Finalized procedural rocks | `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/` | Prefabs | Shore and shallow rock dressing | CANDIDATE | Directory has 49 prefabs. Representative `PFB_Geo_RockFloor_00.prefab` and `PFB_Geo_RockArch_Large.prefab` include LODGroups, LOD meshes, material refs, and at least one collision proxy in rock arch. | Candidate for dressing. Static scan does not prove silhouette quality, scale, or lighting response. |
| Terrain baselines | `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/L_Basalt.terrainlayer`; `Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/L_Gravel.terrainlayer`; `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/L_Sand.terrainlayer`; `Assets/_Project/Art/TEXTURES/Terrain Textures/rocks/L_Rocks.terrainlayer` | TerrainLayer assets | Terrain splat sources | CANDIDATE | Paths exist. | These are support layers, not enough for premium surface coastline by themselves. |

### Terrain/Sediment

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| AbyssBasalt terrain layer | `Assets/_Project/Data/H8_TerrainLayer_AbyssBasalt_1428.terrainlayer`; `Assets/_Project/Art/TEXTURES/TX_H8TerrainBasaltSediment_1428.asset` | TerrainLayer/Texture2D asset | Terrain/sediment base material | CANDIDATE | TerrainLayer references `TX_H8TerrainBasaltSediment_1428.asset`; normal and mask map slots are null. | Provided path in task was wrong. Needs normal/mask completion for close surface proof. |
| Splat and caustic profile data | `Assets/_SourceData/Terrain/terrain_splatmap_profiles.csv`; `Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv` | CSV source data | Offline terrain/surface tuning | CANDIDATE | Static path only. | Data needs importer/bake proof. |
| MapMagic bridge/tooling | `Assets/MapMagic/`; `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/HectonRockOutput.cs` | Third-party package plus first-party bridge scripts | Terrain graph inputs and procedural placement | CANDIDATE, PENDING UNITY SLOT | Static path only. | Configure or wrap through first-party bridge. Do not mutate MapMagic package. Runtime terrain generation is a hero-asset risk unless baked/offline. |

### Coral/Flora

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| World procedural flora textures | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/` | Texture folder | Coral and kelp material atlases/imported texture sets | CANDIDATE | 180 files: 48 PNG, 36 `.asset`. Shallows atlases are 1024x1024; albedo/normal/ORM imports include platform max entries down to 512 and 2048. | Good existing texture source, but atlas quality and color grading need in-scene proof. |
| Coral master materials | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`; `MAT_family_coral_plate.mat`; `MAT_family_coral_massive.mat`; `MAT_family_coral_low.mat`; `MAT_family_coral_brittle.mat`; `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` | Materials/shader | Coral visuals for proxy and baked prefabs | CANDIDATE | Branching material references imported albedo, detail, mask, normal textures; has caustic parameters and normal strength. | Strong candidate. Needs density, lighting, scale, and color proof in photic shallows. |
| Kelp master materials | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat`; `MAT_family_kelp_patch_dense.mat`; `MAT_family_kelp_canopy.mat`; `MAT_family_kelp_abyssal.mat`; `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` | Materials/shader | Kelp and canopy visuals | CANDIDATE | Kelp material references imported albedo, detail, mask, normal textures; has caustic parameters. | Strong candidate for density, but animation/sway and instancing must be verified. |
| Baked flora prefabs | `Assets/_Project/Prefabs/Nature/Flora/Baked/` | Prefab families | Offline baked coral/kelp placement assets | CANDIDATE | Families: coral branching 6 prefabs, coral brittle 10, coral low 6, coral massive 6, coral plate 6, kelp abyssal 14, kelp canopy 15, kelp patch dense 12, kelp tall 14. Representative prefabs include 3 LOD meshes and GPUI prototype refs. | Best static candidate for production dressing, but needs placement and visual proof. |
| BioForge shallows generated meshes | `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp`; `.../TubeCoral`; `.../PorousRock` | Generated mesh assets | Shallow biota and porous rock variants | CANDIDATE | 1203 files total under shallows. Kelp 600 files/300 assets, TubeCoral 300/150 assets, PorousRock 300/150 assets. | Editor/offline asset source only. Do not rely on runtime mesh generation for hero scenes. |
| Placement rules | `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_kelp_starter.asset`; `ProceduralRule_rule_coral_branching.asset`; `ProceduralRule_rule_coral_plate.asset` | ScriptableObject rules | Density and biome placement | CANDIDATE | Kelp starter depth 0-180 m, density scale 1.2. Coral branching depth 0-600 m. Coral plate depth 18-520 m. | Rules are useful but not visual density proof. Need Unity scatter proof and route readability. |

### Industrial Traces

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| First-party construction finals | `Assets/_Project/Prefabs/Construction/Final/`; `Assets/_Project/Art/Materials/Construction/` | Prefabs/materials | Wreckage, ruin, service scar, industrial traces | CANDIDATE | Construction final folder has 10 prefabs. Representative `PFB_Debris_WreckField.prefab` uses built-in primitive meshes and material refs. | Not acceptable as hero reference if left primitive. Needs offline replacement/bake or detailed dressing. |
| World procedural industrial proxies | `Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_ruin_*`; `PFB_family_debris_*`; `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_*`; `MAT_family_debris_*` | Prefabs/materials | Procedural scatter of traces | CANDIDATE | Proxy folder has ruin/debris families and variants. Debris material has no base textures in static scan, only color. | Useful layout proxies. Not final beauty. |
| Offline wreckage tooling | `Assets/_Project/Scripts/World/OfflineWreckageBaker/`; `Assets/_Project/Editor/Bakers/WreckageTextureBaker.cs`; `Assets/_Project/Art/Shaders/Include/WreckageCarbonizationBaker1727.compute` | Editor/offline tools | Generate/bake better wreckage visuals | CANDIDATE | Static path only. | Correct route for industrial upgrade; requires offline bake and Unity proof. |
| SciFiFacility | `Assets/SciFiFacility/` | Third-party asset package | Source kit for panels, pipes, hull parts, wet glass | CANDIDATE | Static count: 1534 files, 255 prefabs, 100 materials, 75 texture files. | Third-party. Use as-is or instantiate/configure. Do not overwrite package assets. |
| Surface drop pod/debris | `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceDropPodBurnt_1428.asset`; `Assets/_Project/Art/Materials/World/MAT_SurfaceDropPodCharredHull_1428.mat`; `Assets/_Project/Prefabs/TECH_DEBRIS.prefab` | Mesh/material/prefab | Surface or shallows industrial traces | CANDIDATE | Static path only. | Needs material/detail proof. |

### Sky/Aegir/Moons/Clouds

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Aegir baked disc | `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`; `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`; `Assets/_Project/Art/Meshes/World/MESH_H8SurfaceGasGiantDisc_1428.asset` | PNG/material/mesh | Surface sky Aegir disc | CANDIDATE | PNG is 2048x2048 sRGB, alpha transparency on, clamp wrap. Material references baked disc texture. | Good candidate, but baseline shows Aegir integration/rim softness needs work. |
| Aegir impostor/storms | `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`; `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`; `Assets/_Project/Art/TEXTURES/Aegir_storms.png`; `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png` | Material/shader/PNG | Higher fidelity gas giant presentation | CANDIDATE | Prologue storm texture is 4096x2048 but importer max entries include 2048. Material refs clouds and storms. | Needs softness, terminator, atmospheric blend, and scale proof. |
| Gas giant prefab | `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab`; `Assets/_Project/Prefabs/GasGiant_Aegir.prefab` | Prefab | Sky object instance | CANDIDATE | Prologue prefab uses built-in sphere mesh and a material ref. | Built-in sphere is acceptable for distant celestial only if material/shader carries premium look. Needs Unity sky proof. |
| Cloud sheets/panoramas | `Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat`; `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`; `Assets/_Project/Art/Shaders/H8_SurfaceCloudPanorama_1428.shader`; `Assets/_Project/Art/Shaders/H8_AtmosphericCloudSheet_1428.shader`; `Assets/_Project/Art/TEXTURES/Sky/` | Materials/shaders/textures | Cloud deck and sky depth | CANDIDATE | Sky texture folder contains large PNGs. Cloud materials reference texture assets. | Must not become dark gradient cover. Needs daylight proof. |
| Moons | `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat`; `MAT_CelestialMoon_Thalos.mat`; `MAT_CelestialMoon_Pelagia.mat`; `MAT_CelestialMoon_Nammu.mat`; `Assets/_Project/Art/Shaders/Hecton_CelestialMoon.shader` | Materials/shader | Moon discs and surface cues | CANDIDATE | Moon materials reference base maps; many secondary texture slots are null. | Needs phase/scale/horizon proof. |
| Celestial scripts | `Assets/_Project/Scripts/HectonCelestialEngine.cs`; `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs`; `Assets/_Project/Scripts/SkySystemFollowCamera.cs`; `Assets/_Project/Editor/HectonSkyTools.cs`; `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`; `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs` | Runtime/editor scripts | Sky placement, atlas generation, observer-relative presentation | CANDIDATE | Static path only. | Needs Unity slot. |

### VFX/Particles

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Foam and wake VFX | `Assets/_Project/Scripts/VFX/JacobianFoam/`; `Assets/_Project/Art/Shaders/Hecton_CalculateFoam.compute`; `Assets/_Project/Art/Shaders/Hidden_Hecton_OceanDepthFoam.shader`; `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceWakeArc_1428.asset`; `MESH_SurfaceSplashRing_1428.asset` | Runtime/editor scripts/shaders/meshes | Wake arcs, splash rings, depth foam | CANDIDATE, PENDING UNITY SLOT | Static path only. | Must be bounded by frame budget and not hide weak water. |
| Silt and particles | `Assets/_SourceData/VFX/Propwash/vfx_silt_profiles.csv`; `Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs`; `Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs`; `Assets/_Project/Art/Shaders/Hecton_HalfResParticleComposite.shader`; `Assets/_Project/Art/Materials/MAT_HalfResParticleComposite.mat` | CSV/scripts/shader/material | Propwash, silt, particulate depth | CANDIDATE | Static path only. | Shallow photic zones cannot use silt as cover for missing art. |
| Leak/plume/debris VFX | `Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat`; `Assets/_Project/Art/Shaders/Hecton_LeakPlume.shader`; `Assets/_Project/Art/Shaders/Hecton_LeakPlume.compute`; `Assets/_Project/Scripts/VFX/Debris/` | Material/shaders/scripts | Industrial trace life and motion | CANDIDATE | Static path only. | Needs placement proof. |
| Bad bubble atlas | `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png` | PNG texture | None for final | REJECTED_PLACEHOLDER | 1024x1024. Filename explicitly says bad/redo. | Do not use as final production reference. |

### UI/Instrument Overlays

| Family | Verified Paths | Type | Likely Use | Proof State | Static Import/Preview State | Risk |
|---|---|---|---|---|---|---|
| Diegetic visor/HUD shaders | `Assets/_Project/Shaders/UI/Hecton_DiegeticVisorCurvedHUD.shader`; `Assets/_Project/Art/Shaders/Hecton_DiegeticVisorLens.compute`; `Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader`; `Assets/_Project/Art/Shaders/Hecton_VisorAR.shader`; `Assets/_Project/Art/Shaders/HectonVisorUberPost.shader` | Shaders/compute | Surface/shallow readability overlay and lens effects | CANDIDATE | Static path only. | Instruments support readability; they do not excuse weak surface art. |
| Sonar and PDA overlays | `Assets/_Project/Art/Shaders/Hecton_HUD_AcousticRadarOverlay.shader`; `Assets/_Project/Art/Shaders/Hecton_PDA_SonarPointCloud.shader`; `Assets/_Project/Art/Shaders/Hecton_PDA_SonarMap.shader`; `Assets/_Project/Art/Shaders/Hecton_SonarMap.compute`; `Assets/_Project/Art/Shaders/Hecton_SonarRaymarch.compute` | Shaders/compute | Route/evidence readability under water | CANDIDATE | Static path only. | Needs view proof at surface, waterline, and shallow depth. |
| HUD meshes/materials | `Assets/_Project/Art/Meshes/M_Diegetic_HUD_V4_CurvedPanel.asset`; `Assets/_Project/Art/Materials/MAT_Diegetic_HUD_V4_Projection.mat`; `Assets/_Project/Art/Materials/MAT_HUD_AcousticRadarOverlay.mat`; `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`; `Assets/_SourceData/Visor/visor_hud_profiles.csv` | Mesh/material/prefab/data | Diegetic instrument overlay | CANDIDATE | Static path only. | Runtime legibility and non-overlap require Unity proof. |

## Primitive Or Rejected Families

- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/` and `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/`: REJECTED_PLACEHOLDER for final visual reference.
- `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png`: REJECTED_PLACEHOLDER by filename and task standard.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab`: CANDIDATE only. Representative static scan shows built-in primitive meshes; not hero-ready without replacement/dressing.
- `Assets/_Project/Prefabs/Hecton Ocean.prefab` and `_Archive/Mat_Ocean.mat`: legacy/fallback candidate, not premium surface target.
- Flat/no-texture proxy materials under `Assets/_Project/Art/Materials/WorldProceduralProxy/` for debris/ruins: layout candidates only.

## Prioritized Upgrade Plan

1. Ocean color/specular/foam
   - Route: assign existing `MAT_H8_SurfaceCrestOcean_1428.mat` to `Ocean_Crest.prefab` or scene slot; configure Crest in Unity; use Crest textures as-is.
   - Add: Unity screenshots across horizon, glancing specular, near camera, and underwater transition.
   - Risk: third-party Crest refs and null prefab fields need Unity slot verification.

2. Waterline and shoreline foam
   - Route: use `MESH_H8SurfaceShoreFoamRing_1428.asset`, `MAT_H8_SurfaceFoamRibbons_1428.mat`, `H8_ShorelineFoamRibbon_1428.shader`, and `ShorelineFoamGraftEditorTools.cs`.
   - Add: assign/bake real foam masks or texture. Current ribbon material has no base texture assigned.
   - Risk: flat-colored foam will fail the floor.

3. Wet rock and coastline breakup
   - Route: assign existing wet basalt materials/textures, fill missing detail/normal/mask slots, use `MESH_SurfaceCoastlineJagged_1428.asset`, `MESH_SurfaceBasaltStack_1428.asset`, and finalized rock prefabs for dressing.
   - Add: offline/editor bake for wet edge masks and sediment variation.
   - Risk: baseline image shows current coast reads procedural and grey.

4. Aegir softness and sky integration
   - Route: choose between baked disc path and impostor path; use `MAT_AegirGasGiant_Impostor_1428.mat`, `H8_AegirGasGiantImpostor_1428.shader`, `Aegir_storms.png`, cloud materials, and celestial scripts.
   - Add: horizon atmospheric blend, terminator softness, scale sanity, and rim breakup.
   - Risk: giant body dominates the frame; any shader/material weakness is immediately visible.

5. Shallow coral/biota density
   - Route: prefer baked flora prefabs under `Assets/_Project/Prefabs/Nature/Flora/Baked/` and imported WorldProceduralFlora materials; use placement rules as density intent, not proof.
   - Add: Unity scatter preview from 0-30 m, 30-100 m, and shoreline silhouette.
   - Risk: rules and proxies do not prove density or premium look.

6. Industrial traces
   - Route: use final/proxy industrial assets for layout only, then upgrade with OfflineWreckageBaker and SciFiFacility as source kit.
   - Add: replace primitive debris with baked hull panels, pipes, wet glass, service scars, and carbonization textures.
   - Risk: existing representative debris prefab is primitive-heavy.

## Third-Party Handling

| Third-Party Family | Handling |
|---|---|
| Crest | Use as-is, configure in Unity slot, wrap through first-party bridge. Do not mutate package code/materials/textures. |
| MapMagic | Use as-is for graph inputs and placement, wrap through first-party bridge. Do not mutate package. |
| MeshBaker | Use as-is if needed for offline/editor mesh consolidation. Do not change package assets. |
| GPUInstancer | Use as-is for baked flora/prototype route. Validate runtime density and no hot allocations. |
| SciFiFacility | Use as-is as source kit or scene instances. Do not overwrite package assets. |

## Continuous Quality Scaling

| Family | Compact | Middle | High | Ultra |
|---|---|---|---|---|
| Ocean Skin | Crest surface with readable color, conservative foam density, no ugly flat fallback | Add foam ribbons and long-swell read cards | Stronger specular/normal blend, caustic modulation, richer waterline masks | Overkill reflections/foam breakup/shore wetness while preserving same gameplay truth |
| Underwater Optics | Stable attenuation and legible instruments | Add cheap caustic cards/fakes | Deferred caustics and richer particulate layers | Higher cadence/quality optics and richer shafts within profiler limits |
| Coast/Rock | Wet basalt material identity, dressed silhouettes, no grey blockout | More rock variants and foam edge masks | Detail/mask textures, layered wetness, larger formations | Dense hero coastline breakup and cinematic wet edge detail |
| Terrain/Sediment | Terrain layers with readable material identity | Add splat variation and sediment bands | Bake normals/masks and shoreline sediment blends | High-density terrain material variation and route-specific hero dressing |
| Coral/Flora | Baked LOD prefabs with lower density, still colorful and readable | Moderate kelp/coral density using placement rules | Rich scatter, sway, caustic-aware materials | Dense photic biota, varied silhouettes, overkill material response |
| Industrial Traces | Sparse but non-primitive readable traces | Baked wreck clusters and wet materials | More panels, pipes, service scars, VFX leaks | Dense narrative wreckage dressing without runtime generation |
| Sky/Aegir/Moons/Clouds | Aegir/cloud/moon forms remain readable and soft | Better cloud sheet and haze blend | Impostor gas giant with terminator and atmosphere | Overkill sky depth, layered clouds, soft celestial integration |
| VFX/Particles | Bounded foam/silt/instrument effects, no cover-up haze | More wakes and shallow caustics | Half-res composite, stronger splash/propwash | Rich VFX density inside frame budget |
| UI/Instrument | Core HUD legible, no overlap | Sonar/visor support waterline navigation | Better curved HUD and depth overlays | Rich diegetic overlays that support, not replace, visual proof |

## Runtime Generation Risk

- Hero surface/coast/biota/industrial assets must be editor/offline generated or pre-baked. Runtime placeholder generation is rejected for production surface/shallow visuals.
- MapMagic/Crest/GPUInstancer routes need Unity slot verification and first-party ownership boundaries.
- Any runtime VFX or scatter path must be bounded by budget and validated with profiler proof before acceptance.
- Static route data and ScriptableObject rules do not prove runtime density, material response, or quality.

## Future Unity Visual Implementer Prompt

Use Agent 1802 inventory to apply surface/shallow candidate assets when Unity is free. Do not mutate Crest, MapMagic, GPUInstancer, MeshBaker, or SciFiFacility package assets. Configure `Ocean_Crest.prefab` with `MAT_H8_SurfaceCrestOcean_1428.mat` or the approved first-party Crest bridge, verify null/optional refs, add shoreline foam ribbons with real masks, assign wet basalt coast materials, place finalized rock prefabs, place baked coral/kelp families from `Assets/_Project/Prefabs/Nature/Flora/Baked/`, integrate Aegir with `MAT_AegirGasGiant_Impostor_1428.mat` or baked disc route, and capture proof screenshots from the angles listed below. Mark any runtime/build/editor conflict as PENDING UNITY SLOT.

## Future Offline Generated Asset Agent Prompt

Use Agent 1802 inventory to replace weak static candidates without runtime hero generation. Produce editor/offline baked wet-rock detail masks, shoreline foam masks, shallow coral/kelp placement manifests, upgraded industrial wreckage meshes/materials from first-party tools or SciFiFacility as source kit, and Aegir/cloud texture refinements. Do not use WorldRuntime procedural placeholders as final reference. Do not delete assets. Output bake manifests and path-based proof only.

## Required Screenshot Angles For Proof

- Surface horizon from player eye level: Aegir, moons/clouds, coastline, ocean specular, and UI visible.
- Low waterline shot: foam ribbons, wet basalt edge, water color, and horizon readable.
- Close coast rock shot: wet basalt material breakup, detail/mask response, and no flat grey terrain.
- 5-20 m photic shallows: coral/kelp density, caustics, sediment, route readability.
- 30-100 m shallow route: biota silhouettes, water optics, instrument overlay legibility.
- Industrial trace close shot: wreck/debris materials, non-primitive geometry, wet/aged surface detail.
- Wide composition shot: coast, Aegir, clouds, water surface, and shallow route in one frame.
- Compact/Middle/High/Ultra comparison from same camera, verifying Compact remains attractive and readable.

## Final Claim Guard

This report is a static inventory and replacement plan. It proves asset existence, some YAML bindings, some texture dimensions/import settings, and baseline screenshot observations. It does not prove final visual quality, runtime behavior, import correctness in Unity, frame time, memory cost, or gameplay acceptance.

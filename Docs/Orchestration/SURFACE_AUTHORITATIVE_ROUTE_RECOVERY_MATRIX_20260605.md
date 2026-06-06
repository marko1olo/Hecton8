# Surface Authoritative Route Recovery Matrix - 2026-06-05

Status: `CONTROLLER_MATRIX / STATIC_ONLY / VISUAL_REJECTED / HISTORICAL_RECOVERY_CANDIDATES_MAPPED`.
Evidence class: `USER_DIALOGUE_REVIEW + STATIC_DOC + STATIC_SOURCE + STATIC_LOG + DIRECT_SCREENSHOT_REJECTION`.

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, project-setting mutation, or raw YAML edit was performed by this controller pass.

## Current Front

- Process gate: red. Latest refresh saw CPU 87-100 percent with active `dotnet`/`VBCSCompiler`; Unity-side compile/import activity was still moving. Unity mutation/readback/build/import/screenshot work is blocked.
- Last visual evidence: `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png` and `.txt`, overwritten at the latest observed `03:13` pass.
- Last log evidence: `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeN_20260606_025336.log` has Tundra success but remains h8_1914 diagnostic output; `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log` later writes h8_1914 output, emits Unity `MemoryLeaks`, and then records `CS0103` in `SeamGapDitherRenderer.cs` after the file changed while Csc was running. ProbeO is compile-poisoned and not acceptance proof.
- Last visual verdict: rejected. Metadata still shows `SURFACE_HORIZON_SALT_HAZE_1428` active, `H8_WORLD_TERRAIN_SHELL_1428` active, MapMagic erosion disabled, anomaly disabled, `splat.sedimentIn=UNLINKED`, and `anomaly.heightIn=UNLINKED`. The screenshot remains diagnostic negative evidence, not product proof.
- Terrain/job blocker: `Docs/Orchestration/MAPMAGIC_HYDRAULIC_EROSION_JOB_SAFETY_STATIC_REVIEW_20260606.md`.
- Active controller owner: orchestrator lane. Unity owner work must wait for a green process gate and no-mutation readback.
- Next action: stop symptom probes, require authoritative route readback, then repair owner route in safe order.

## Root Failure

The current surface failure is not a color problem. It is an authority-route failure:

- water is not presented as a believable ocean body;
- coastline and terrain do not read as lit wet geology;
- temporary green haze/cards mask the problem instead of fixing it;
- h8_1914 output is diagnostic-only and mutates or depends on editor-only state;
- active player/HUD/tool/movement proof is absent, so scenic screenshots cannot pass first-20-minutes proof.

## Reference Target Signals

Mandatory visual references require:

- cyan/blue readable surface water, not green acid haze;
- visible depth/transparency, whitewater, foam, and wet edge at shore contact;
- readable cliff/island/terrain material shape, not black slabs or flat heightfield noise;
- bright surface sky with integrated Aegir/moons/clouds;
- vegetation/geology density only after the base route is solid;
- gameplay witnesses: player/camera/HUD/tool route where the proof view requires them.

## Active / Candidate / Rejected Matrix

| Route item | Current classification | Evidence / anchor | Controller action |
|---|---|---|---|
| `02_HECTON_WORLD.unity` + `Ocean_Crest.prefab` + `Assets/Crest/Crest/Materials/Ocean.mat` | `ACTIVE_SAVED_ROUTE / CURRENT_VISUAL_REJECTED` | Surface classification says this is the saved ocean route; screenshot rejects it. | Read back exact Crest/OceanRenderer state before edits. Do not patch with haze first. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` | `CANDIDATE_ONLY / OVERDRIVE_RISK` | Not proven active; prior reports warn high teal/foam/light values. | Do not assign blindly. Test only after readback and with before/after Frame Debugger/Stats. |
| `H8_TEMP_SurfaceHorizonHazeProbe_1428` / temp haze | `DIAGNOSTIC_REJECTED` | Metadata shows temp haze in h8_1914; image remains rejected. | Keep as rejection evidence only. It is not a root fix. |
| Temporary water-skin card/mesh probes | `DIAGNOSTIC_REJECTED` | Dialogue shows green card probes read as visible rectangular planes. | Stop as product direction. Use only if explicitly labelled as diagnostic A/B, never acceptance. |
| `MAT_H8TerrainLit_BasaltSediment_1428` active terrain route | `ACTIVE_ROUTE / CURRENT_VISUAL_REJECTED` | Terrain reads black/acid/noisy; metadata reports flat `Main Terrain` size `(15000,0,15000)` plus shell. | Read back terrain height/material/splat/lighting/MapMagic state. Repair terrain before decorative dressing. |
| `MAT_H8_ShorelineFoamFine_1469.mat` and foam sources | `ACTIVE_OR_CANDIDATE / INSUFFICIENT` | Thin ribbon/contact is visually rejected. | Need wet edge breakup, foam contact masks, channel/import proof, and 1m shoreline capture. |
| `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` + `MAT_AegirGasGiant_Impostor_1428.mat` | `ACTIVE / CURRENT_QUALITY_REJECTED` | Aegir reads pasted/translucent in latest image. | Read slots and scalars; fix integration, limb, cloud band, atmospheric relation. |
| `Mat_HectonSky.mat` / cloud decks | `ACTIVE_OR_LINKED / READBACK_REQUIRED` | Static docs show active skybox but missing slot proof. | Read skybox, cloud textures, horizon parameters, lighting relation. |
| `Player.prefab` / HUD prefabs / input/movement | `CANDIDATE_ONLY / SCENE_ACTIVE_NOT_PROVEN` | Player/HUD synthesis says scene lacks production prefab GUIDs. | Scenic proof rejected until active player/HUD/tool/movement route is proved. |
| `H8VisualProofCapture1912.cs` | `DIAGNOSTIC_REJECTION_RUNNER` | Static audits show scene/material/MapMagic mutation and raw MCP output. | Do not extend for h8_1475. Build separate no-mutation proof harness. |

## Historical Recovery Candidates

Static git archaeology by the surface sidecar identified candidate restore points. These are not revert instructions and not acceptance proof.

| Candidate | Evidence | Use |
|---|---|---|
| `857689d2b` (`2026-04-28 14:55 +0400`) | Touched `02_HECTON_WORLD.unity`, `Assets/Crest/Crest/Materials/Ocean.mat`, `Assets/_Project/Art/Materials/Mat_HectonSky.mat`, and `Hecton_AegirHazeOverlay.shader`. Static material snapshot has `Mat_HectonSky.mat` `_SunElevation: 0.5960179` with blue/purple sky colors, and older Crest fog/diffuse/subsurface setup. | Primary historical sky/Crest/scene candidate for field comparison after Unity gate is green. Do not blindly apply. |
| `06cff4605` (`2026-05-07`) plus `2442f78e9` and `474759516` | First-week-May changes to `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`; `06cff4605` is a small serialized graph delta. | Candidate MapMagic graph baseline for comparison, not raw YAML patch material. |
| `fd33b9521` and `7073862dd` (`2026-05-01`) | Scene placement changes to `02_HECTON_WORLD.unity`. | Candidate surface scene-layout comparison only after scene readback. |
| `b81b0da81` (`2026-05-10 20:59 +0400`) | Major divergence touching `Mat_HectonSky.mat`, `Mat_Terrain.mat`, `Hecton_AlienSky_Master.shader`, and `TerrainMaster.shader`; terrain shader route removed the older adaptive triplanar/texture-array path and added packed control sampling. | Use `b81b0da81^` as a pre-divergence terrain/sky shader comparison point if active route proof shows current shader/material path is the failure. |

## 2026-06-06 Hubble Static Surface Anchor Findings

Static git forensics refined the comparison anchors. These are field-readback questions, not revert commands:

| Anchor | Static route signal | Required Unity readback question |
|---|---|---|
| `857689d2b` Apr 28 `Ocean_Crest.prefab` | `OceanRenderer._material` points to `Ocean.mat` GUID `9def92...`; `_minScale 8`, `_maxScale 256`, `_lodDataResolution 256`; sea-floor depth, foam, shadow enabled; `OceanDepthCache` at Y `4900`, scale `256`. | Is the active scene `OceanRenderer` instantiated from `Ocean_Crest.prefab`, using `Ocean.mat` GUID `9def92...`, with depth/foam/shadow/cache enabled and sane water level/extents? |
| `857689d2b` Apr 28 `Ocean.mat` | `_Underwater 1`, `_Foam 1`, `Foam2.png`, `WaveNormals.png`, caustics texture slots, blue/teal diffuse/subsurface setup. | Does active water use this exact asset or a runtime/temp/1428 replacement, and what are active foam/normals/caustics/subsurface scalar values? |
| `857689d2b` Apr 28 `Mat_HectonSky.mat` | Cloud/star slots, `_SunElevation 0.5960179`, `_NightBlend 0`, blue/purple sky and haze values, Aegir halo fields. | Is `RenderSettings.skybox` or celestial engine using this route, and did later values darken/null the daylight surface? |
| `857689d2b` Apr 28 `Hecton_AegirHazeOverlay.shader` | Aegir is an atmospheric overlay with sun/Aegir direction, horizon veil, and blue-noise dither, not a pasted sphere. | Is an Aegir haze overlay active and receiving sun/Aegir directions, blue-noise, and horizon veil scalars? |
| `2442f78e9` May 6 `ACTUAL TERRAIN.asset` | Adds biome matrix, hydraulic erosion, and terrain splatmap MapMagic nodes. | Are Hecton hydraulic/splatmap nodes active and linked, or are height/sediment links serialized/read back as zero/unlinked? |
| `2442f78e9` May 6 `HectonTerrainSplatmapMapMagicNode.cs` | Intended sand/rock/silt/cavity outputs with slope/cavity/sediment job and cold MapMagic completion before graph publish. | Does MapMagic consume produced matrices into terrain layers/control textures? |
| `474759516` May 7 | Adds `HectonAnomalyMapMagicNode`, splatmap `slopeWeightOut`, and water enter/exit/splash bridge. | Are anomaly height and splatmap inputs linked, and is `WaterTransitionHandler` attached to the active player/movement route? |
| `b81b0da81` May 10 | `_NightBlend 0 -> 0.5`, `_SunElevation 0.596 -> 0`, orange haze/sky colors; terrain route changes toward packed control/flow normals. | Did active surface inherit this night/orange/packed route, causing dark/muddy surface and terrain-material drift? |

Rejected or weak anchors:

- `06cff4605` is weak as MapMagic proof because the graph delta is small and it removes terrain normal-array sampling; use only as comparison context.
- `fd33b9521` and `7073862dd` are scene-placement hints because `02_HECTON_WORLD.unity` is binary and mixed with unrelated churn.
- `MAT_H8_SurfaceCrestOcean_1428` remains candidate-only with overdrive risk; do not assign blindly.

Historical comparison gate:

- compare fields through Unity/API readback when process gate is green;
- no raw scene/asset YAML revert;
- no Crest material swap without asset path/GUID proof, Frame Debugger/Stats, and screenshot proof;
- no MapMagic graph mutation without Unity import/readback and explicit terrain owner acceptance.

## Unity Readback Gate

When process gate is green, the next Unity owner must read these fields before editing:

- dirty state before/after for loaded scenes and touched assets;
- active scene path and build scene index;
- all active player roots, prefab source GUIDs, shell-vs-production classification, movement/input/camera/HUD/tool components;
- active main camera owner and capture source;
- Crest `OceanRenderer` material asset path/GUID, active water object transform/sea level, LOD/extents/resolution/downsample, foam/depth/shadow/underwater flags, normals/foam/caustics texture slots, and any temp/HideAndDontSave material;
- terrain object, MapMagic graph/generation state, terrain height size, material template, splat/control/normal/mask slots, draw instanced, pixel error, basemap distance;
- shoreline/foam renderers, materials, render queues, ZWrite/ZTest, bounds, and waterline relationship;
- skybox, celestial engine, Aegir object/material/mesh, Aegir texture slots and scalar values, cloud deck renderers/materials;
- console clean state and no import/compile/domain reload/log spam;
- Frame Debugger/Stats after any visual promotion attempt.

## Wegener Historical Readback Matrix - 2026-06-06

Wegener completed a read-only historical surface matrix. It is comparison input only. No checkout, copy, revert, Unity run, build, import, Play Mode, profiler, scene save, material save, prefab save, or YAML mutation was performed.

Critical current readback fields:

- Crest/ocean: `02_HECTON_WORLD.unity`, `Ocean.mat`, `Ocean-Underwater.mat`, `MAT_H8_SurfaceCrestOcean_1428`, `MAT_H8SurfaceOceanRead_1428`, `H8_WORLD_CREST_OCEAN_RUNTIME_1428`, `oceanRenderer`, `oceanDepthCache`, `mapMagicBridge`, `waterSurfaceLevel`, `waterLevelFallback`, Crest shader/material refs, foam/wave/sky/depth properties.
- Sky/Aegir/moons: `Mat_HectonSky.mat`, `MAT_AegirSky_Master`, `MAT_AegirHazeOverlay`, `MAT_AegirGasGiant_Impostor_1428`, `Hecton_AlienSky_Master.shader`, `Hecton_CelestialMoon.shader`, `Hecton_CelestialAtmosphere.hlsl`, `HectonCelestialEngine.cs`, scene `m_SkyboxMaterial`, `_skyMaterial`, `sunLight`, `aegirTransform`, `aegirRenderer`, cloud/ring shadow cookies, surface readability floors.
- MapMagic terrain/anomaly/splat: `ACTUAL TERRAIN.asset`, sandbox graph, `MapMagicBridge.cs`, `HectonTerrainSplatmapMapMagicNode.cs`, `HectonAnomalyMapMagicNode.cs`, `HectonAnomalyEngine.cs`, `MapMagicRuntimeBridge`, `mapMagicObject`, terrain fade/shadow mask, terrain splat color routes, custom node ports, MicroSplat refs, terrain debug counters.
- Water transition/surface exit: `WaterTransitionHandler.cs`, `WaterTransitionKind : byte`, `SurfaceEnter=1`, `SurfaceExit=2`, `Splash=3`, `SubmergeChanged=4`, surface exit gravity delay/acceleration/duration, `SignalBus<WaterTransitionSignal>` snapshot consumption.
- URP/render: `QualitySettings.asset`, `GraphicsSettings.asset`, URP assets, scene RenderSettings, `HectonUnderwaterVisuals` surface/underwater blends.

Historical expectations:

- Apr28 `857689d2b`: Crest `Ocean.mat` shader GUID `986f7c...`, `_Foam=1`, `_FoamScale=0.001528351`, `_NormalsScale=40`, `_NormalsStrength=0.08`, `_WaveFoamCoverage=0.52`, `_WaveFoamStrength=1.25`, `_DepthFogDensity={0.12,0.12,0.08635997}`.
- Current `MAT_H8_SurfaceCrestOcean_1428` static values are stronger and risky: `_FoamScale=0.028`, `_NormalsStrength=0.38`, `_WaveFoamStrength=3.8`, `_DepthFogDensity={0.014,0.018,0.024}`. It is not accepted until active route proof and screenshot/Stats evidence exist.
- Apr28/`e94c11c4d` sky route: `Mat_HectonSky` shader GUID `6302a7...`, `_AegirHaloIntensity=0.58`, `_SkyLuminanceMultiplier=1`, `_SunSize=0.002`, zenith `{0.1,0.16,0.5}`. Current `Mat_HectonSky` static values are `_AegirHaloIntensity=0.74`, `_SkyLuminanceMultiplier=1.16`, `_SunSize=0.0065`, zenith `{0.28,0.48,0.68}`.
- May6 `2442f78e9`: graph contains `HectonHydraulicErosionMapMagicNode`, `HectonTerrainSplatmapMapMagicNode`, `sandOut`, `rockOut`, `siltOut`, `cavityOut`.
- May7 `474759516`: graph adds `slopeWeightOut`, `HectonAnomalyMapMagicNode`, `brineMaskOut`, `fissureMaskOut`, and water transition route.

Current blockers from this matrix:

- `Packages/com.waveharmonic.crest/package.json` is missing; Crest is vendored under `Assets/Crest` plus first-party plugin scripts.
- Scene static values show `MapMagicRuntimeBridge.waterSurfaceLevel: 0` while underwater owner has `waterLevelFallback: 14.02`. This is a hard readback question, not a guess.
- Scene skybox GUID resolves to `Assets/_Project/Art/Materials/Mat_HectonSky.mat`, not newer `MAT_AegirSky_Master`.
- `ACTUAL TERRAIN.asset` is modified and contains both old `Hecton8.Core` and current `Hecton8.Plugins` custom node entries. Current owner scripts are under `Assets/_Project/Scripts/Plugins/MapMagic`, not old `Assets/_Project/Scripts/World/*MapMagicNode.cs` paths.
- `WaterTransitionHandler` / `WaterTransition` does not text-hit in `02_HECTON_WORLD.unity`; next readback must resolve through player prefab/script GUID or owner binding, not scene name search.

Forbidden from this matrix:

- no checkout/copy/revert of historical assets into current;
- no blind Crest material assignment;
- no material instance mutation;
- no Unity/import/Play/profile/build while process gate is red;
- no static-to-runtime proof upgrade.

## Safe Repair Order

1. Establish no-mutation readback. If dirty state changes during readback, abort.
2. Prove or reject the active saved Crest route from actual material/renderer fields.
3. Prove or reject terrain route from actual height/material/MapMagic fields.
4. Prove the MapMagic hydraulic erosion job cleanup path has no `HydraulicErosionDeltaApplyJob` safety exception and no TempJob leak.
5. Fix the slab-water/horizon geometry before haze/post.
6. Fix shoreline wet-rock/foam/contact at 1m route height.
7. Fix sky/Aegir/cloud integration.
8. Only after base water/shore/terrain/sky passes, allow flora/geology placement and decorative density.
9. Only after active player/HUD/tool route is known, attempt canonical h8_1475 packet.

## Hard Rejections

- Green haze as root fix.
- Temporary water card/overlay as product proof.
- Any h8_1914 screenshot as acceptance evidence.
- Reusing overwritten `Docs/Screenshots/MCP/h8_1914_*` filenames for proof.
- Blind Crest material swaps or custom runtime Crest wrappers.
- Rocks, flora, coral, fog, bloom, darkness, or vignette used to hide broken water/terrain/sky.
- Scenic proof without player/HUD/tool route where first-20 proof is required.

## Low / Middle / High / Ultra

- Low: still requires bright cyan/blue water, readable coastline, wet edge, terrain silhouette/material identity, Aegir/sky, and HUD/tool route when relevant. No ugly green fallback.
- Middle: adds stable contact foam, better terrain breakup, normal/detail response, and route composition.
- High: spends budget on richer water normals, caustics, foam masks, shoreline wetness, sky/cloud/Aegir layering, and near-field geology/flora after base pass.
- Ultra: capture-grade density and polish may be added, but route truth, no-mutation proof, and player/HUD/tool predicates do not change.

Final status: `P0 ROUTE BLOCKED / STATIC_CONTROLLER_MATRIX_READY`.

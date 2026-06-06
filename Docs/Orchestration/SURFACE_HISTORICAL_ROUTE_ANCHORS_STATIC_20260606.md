# Surface Historical Route Anchors - Static Synthesis - 2026-06-06

Status: STATIC_ORCHESTRATION_SYNTHESIS / PENDING UNITY READBACK / NOT ACCEPTANCE PROOF
Evidence class: STATIC_DOC / STATIC_SOURCE / SCREENSHOT_REVIEW

## Authority Used

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `water.md`
- `terrain.md`
- `rendering.md`
- `world.md`
- `lighting.md`
- `presentation.md`
- `quality.md`
- Mandates: `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `OPT_Premium_Approximation_Protocol.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`, `REND_Terrain_VirtualTexturing.txt`, `REND_DescriptorBinding_Reality_Check.txt`
- Visual references: `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)`

## Verdict

Current surface route is rejected.

This is not a color-grade problem. It is not fixed by green haze, temporary water cards, disabled renderers, or another `H8VisualProofCapture1912` diagnostic capture.

The failed screenshots `h8_1917_surface_crest_daylight_probe.png` and `h8_1919_surface_crest_skycard_horizon_probe.png` still show:

- slab/card water with a hard horizon band;
- black clipped shore and island masses;
- no credible waterline, wetness, surf, or contact foam;
- terrain that does not read as lit wet geology;
- Aegir present but disconnected from water/sky/terrain lighting;
- no player/HUD/tool/movement witness;
- diagnostic-only capture route, not h8_1475 acceptance.

The mandatory surface reference and project-local May references show the missing target traits:

- blue/cyan readable ocean body with depth and surface pattern;
- white foam and surf at shore contact;
- readable wet rock, cliffs, shelves, and coastline material;
- bright atmosphere and sky;
- Aegir as a soft, integrated scale object, not a pasted sphere;
- route composition with terrain, water, sky, and gameplay space in one shot.

## Current Static Route Facts

These are source/YAML facts only. They do not prove runtime state.

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` active MapMagic scene object references graph GUID `569d36fc879e1e044a410c62ce64a383`.
- That GUID belongs to `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`.
- `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset` has GUID `4b0faac2e4d571e49b4a4ad83e927683` and is not the scene graph reference.
- The scene `MapMagic::MapMagic.Core.MapMagicObject` component has `m_Enabled: 0`.
- `MapMagicRuntimeBridge` is enabled and references that disabled MapMagic component.
- Scene MapMagic `terrainSettings.material` is `{fileID: 0}`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab` has `_createSeaFloorDepthData: 0` and `_createFoamSim: 0`.
- The scene overrides `H8_WORLD_CREST_OCEAN_RUNTIME_1428` to use `Assets/Crest/Crest/Materials/Ocean.mat`.
- Current `Assets/Crest/Crest/Materials/Ocean.mat` has `m_EnableInstancingVariants: 0`, `_Specular: 0.68`, `_RefractionStrength: 0.58`, `_NormalsStrength: 0.42`, `_FoamScale: 0.032`, `_Caustics: 1`, `_CausticsStrength: 0.92`, `_WaveFoamStrength: 2.35`.
- First-party `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat` exists but static scene binding still points to Crest `Ocean.mat`.
- `H8_SurfaceWetCoastApron_1435` exists in the scene but `m_IsActive: 0`.
- `H8_SurfaceCoastalWetRibs_1446` exists in the scene but `m_IsActive: 0`.
- `H8_CREST_FOAM_INPUT_PASS_1464` is active, but its renderer is disabled as a Crest input object.
- `H8_SHORELINE_FOAM_RING_ASSET_1428` is active, but screenshots do not show acceptable contact/wetness.
- `Mat_HectonSky.mat` current values include `_CloudDensityThreshold: 0.2`, `_CloudSoftness: 0.32`, `_CloudSpeedMult: 0.035`, `_SkyLuminanceMultiplier: 1.16`, `_SunElevation: 0.29254407`, `_SunSize: 0.0065`.

## Root Cause Hypothesis

The likely root route defect is broken active-route ownership and binding:

1. Scene terrain is not proving the May-style MapMagic + texture + Crest route. It is using the sandbox graph, disabled MapMagic component, null terrain material setting, and bridge-side presentation patches.
2. Crest is present, but the scene binding and material route do not create readable surface ocean in the failed captures.
3. Shoreline wetness/contact assets exist, but key wet coast/rib objects are inactive or visually ineffective.
4. Sky/Aegir can appear, but it is not integrated with water/terrain lighting.
5. Diagnostic tooling has repeatedly mutated scene/material/render state, making captures useful for rejection only.

## Rejected Work Direction

Stop these routes:

- green haze or acid water tint;
- temporary water-skin cards as product proof;
- more `h8_1915` through `h8_1919` diagnostic captures as acceptance attempts;
- object/renderer disabling to make one angle less ugly;
- decorative rocks/flora/coral used to hide broken water/terrain;
- raw scene/material/MapMagic YAML patching;
- blind Crest material swaps;
- any acceptance claim without player/HUD/tool/movement witness and h8_1475 packet.

## Next Green Unity Window - Readback Order

No scene edit before this readback.

1. Process/no-mutation preflight:
   - `cpu_total_percent`, busy Unity/import/build/compiler process list, active scene path, scene dirty state, project dirty state, console compile/import state.
   - Reject if process gate is red, baseline is dirty without owner explanation, or import/build/compile is active.
2. MapMagic / terrain owner state:
   - MapMagic object active/enabled, `MapMagicRuntimeBridge.enabled`, graph path/GUID, `terrainSettings.material` path/GUID, water surface level, player transform, debug/main/draft tile counts, active terrain count, terrain data path/GUID, heightmap/alphamap resolution, terrain material draw path, and Frame Debugger terrain draws/materials.
   - Reject disabled MapMagic, sandbox graph without approved production explanation, null terrain material, zero tiles, no terrain draw, or black/flat terrain material.
3. Crest / ocean owner state:
   - `OceanRenderer` object path, prefab source/GUID, component enabled, ocean/underwater material paths/GUIDs, material clone status, transform/sea level, ViewCamera/Viewpoint, camera-to-ocean relation, LOD count/extents, `_createSeaFloorDepthData`, `_createFoamSim`, runtime depth/foam resources, normals/foam/caustics slots/scalars, `CrestOceanRuntimeAdapter.seaLevelFallback`, `TryReadGlobalWaterLevel`, and Frame Debugger Crest passes/materials/SetPass.
   - Reject slab/card water, vendor fallback without first-party proof, depth/foam off with missing shoreline proof, hard horizon, bad camera/viewpoint relation, or material clone drift.
4. Shoreline contact / foam / wet apron / ribs:
   - `H8_SurfaceWetCoastApron_1435`, `H8_SurfaceCoastalWetRibs_1446`, `H8_SHORELINE_FOAM_RING_ASSET_1428`, `H8_CREST_FOAM_INPUT_PASS_1464`, `RegisterFoamInput`, `_disableRenderer`, renderer enabled state, `OceanSinglePassRuntime`, render feature state, `_H8OceanDepthFoamMask`, `_GlobalShorelineFoam`, `_H8OceanWakeDisplacement`, `ShorelineFoamRuntimeState.ActiveCount`, `GlobalQualityWeight`, water/camera local Y, CSV profile path, and Frame Debugger contact/foam/wake resources.
   - Reject inactive apron/ribs, no foam input, no depth/foam mask, black shore, dry coastline, or decorative foam ring not connected to water/terrain.
5. Sky / Aegir / lighting owner state:
   - `RenderSettings.skybox`, fog, ambient intensity/colors, `HectonCelestialEngine.enabled`, sky material/skyboxes, surface readability values, Aegir transform/observer body/renderer/material/shader/textures/mesh, fixed direction, angular diameter, anchor distance, `SurfaceWeatherDirector` profile, atmosphere manager route, and Frame Debugger sky/Aegir passes.
   - Reject disconnected Aegir, missing renderer/material, hard grey horizon, fog/lighting causing black shore, green haze/card cover, or noir darkness outside storm/eclipse/depth.
6. Player / HUD / tool witness:
   - `BootstrapState` current player, active player object path/source, production prefab vs scene shell, `Player` tag object, camera owner, `HectonPlayerMovement`, `WaterTransitionHandler`, `PlayerInteraction`, `PlayerPDA`, `PlayerToolManager`, `PlayerInventory`, `PlayerFlashlight`, `VisorHUDController`, HUD canvases/render modes, `HUD_Internal forceScreenSpaceOverlay`, active tool/held item, interaction prompt, screenshot witness status.
   - Reject free/editor camera, missing HUD/tool, scene shell masquerading as player, or missing water-transition owner.

h8_1475 prerequisites: green process gate, active `02_HECTON_WORLD`, no-mutation readback harness, clean dirty-state audit before/after, console export, 60 clean post-capture seconds, Frame Debugger stats, visual reference comparison, and player/HUD/tool witness route. Packet root must contain `manifest.json`, `manifest.sha256`, Unity log, and the six named screenshots: `01_surface_coast_aegir_ui_off.png`, `02_shoreline_close_1m.png`, `03_underwater_0_5m.png`, `04_underwater_20_50m_route.png`, `05_aegir_celestial_long.png`, `06_regression_low_oblique.png`. Manifest must include continuous `global_quality_weight`; binary labels are invalid.

## Repair Order After Readback

Do not tune color first. Repair ownership/binding first.

1. Terrain route:
   - prove whether sandbox graph is intentional or wrong;
   - prove why MapMagic component is disabled while bridge is enabled;
   - bind or generate terrain material/control texture route through the approved MapMagic owner path;
   - reject any terrain result that still reads as black/yellow noise or dead carpet.
2. Water route:
   - prove current Crest `Ocean.mat` versus first-party `MAT_H8_SurfaceCrestOcean_1428` binding;
   - prove depth/foam/extents and camera horizon coverage;
   - restore readable blue/cyan water body, specular, refraction, and surface pattern.
3. Shoreline/contact route:
   - activate or replace wet apron/ribs only through Unity API after readback;
   - prove waterline alignment, wet rock material, surf/foam breakup, and no rectangular/card edges.
4. Sky/Aegir route:
   - prove active sky material and Aegir material route;
   - integrate Aegir through lighting/atmosphere scale, not by pasting a sphere over a broken scene.
5. Proof route:
   - h8_1475 canonical packet only;
   - player/HUD/tool/movement witness required;
   - compact and normal visual captures required;
   - Frame Debugger/Console/profiler evidence required before runtime claims.

## First-20 Route Impact

This removes a blocker for the first exit and first semi-open shallow route. Without readable water, terrain, sky/Aegir, shoreline contact, and player-scale proof, the first 20 minutes cannot be product-facing, even if resource/quest data exists.

## GlobalQualityWeight Consequences

- Low: preserve readable cyan/blue water, coastline silhouette, wet rock breakup, sky/Aegir readability, and route cues. Reduce density/resolution/cadence only.
- Middle: add stronger material identity, foam/contact detail, normal/refraction quality, and route dressing after base route passes.
- High: add richer reflections, cloud depth, shoreline detail, denser near-field geology/flora, and better Aegir atmospheric integration.
- Ultra: visual-overkill capture grade with richer water/sky/terrain density, but same gameplay truth, save identity, and route authority.

## Proof Boundary

Current evidence is static and screenshot-review only. Unity compile/import, Play Mode, Frame Debugger, profiler, GCMonitor, h8_1475 packet, player-build, and user acceptance are all `PENDING VERIFICATION`.

## 2026-06-06 h8_1921 / Epicurus Next-Green Packet

Evidence class: `STATIC_METADATA / DIRECT_SCREENSHOT_REVIEW / STATIC_PLAN`. No Unity, import, build, Play Mode, profiler, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

`h8_1921_surface_owner_lighting_nonmutating` is failure/readback evidence only. Metadata proves the scene has active Crest ocean, terrain shell, and Aegir, but also proves the route is still structurally wrong:

- `MapMagic.enabled=False`.
- MapMagic grid count is `0`.
- Scene graph path is `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`.
- Crest uses `Assets/Crest/Crest/Materials/Ocean.mat`.
- Crest `_viewpoint=NULL` and `_camera=NULL`.
- Sea-floor depth is disabled.
- Crest input tiles are inactive.
- The screenshot still shows rectangular slab water, black clipped shore, weak/no wet contact, overbearing Aegir, and no production player/HUD/tool witness.

Exact readback order for the next green Unity window:

1. Process/no-mutation preflight: CPU/process gate, no Unity/import/build/dotnet/csc worker activity, clean dirty state, clean console/log, active scene `02_HECTON_WORLD`.
2. Player/camera/HUD baseline: production player root, movement/input, main camera ownership, HUD/visor/PDA/tool/interaction route. Reject landscape-only proof.
3. MapMagic/terrain: component enabled state, graph path/GUID, terrain material, grid/tile counts, terrain data/material/layers, erosion/anomaly/splat/sediment links.
4. Crest/ocean: OceanRenderer material, sea level, viewpoint/camera/follow state, LOD/extents/resolution, sea-floor depth, foam sim/input tiles, caustic/normal/foam slots.
5. Shoreline/contact: wet apron/ribs/foam ring/fine foam/input pass, renderer active/enabled state, waterline Y, contact mask/profile, Frame Debugger foam/contact passes.
6. Sky/Aegir/lighting: skybox, fog/ambient/sun, celestial owner, cloud decks, Aegir material/scale/direction/texture integration.
7. Post-readback audit: dirty before/after, console export, Frame Debugger/stats, no mutation, no save prompt.
8. Only after all pass: run the real `h8_1475` proof packet route.

Forbidden before readback:

- `SetActive`, renderer toggles, camera pose cheats, material swaps, temp materials, water cards, haze cards, MapMagic `Refresh` / `StartGenerate` / `Pin`, Crest serialized edits, terrain/splat edits, prefab Apply/Revert, `SaveScene`, `MarkSceneDirty`, `SetDirty`, `SaveAssets`, raw MCP PNG acceptance, or extending `H8VisualProofCapture1912` as canonical proof.

First allowed Unity API changes after readback, only if evidence confirms the owner fault:

1. Repair active MapMagic/terrain binding through Unity Editor API using the approved production graph/material owner. No raw YAML and no blind historical revert.
2. Repair OceanRenderer material/viewpoint/camera/depth/foam/input ownership through the approved Crest owner route. No temp clone or decorative water plane.
3. Repair existing wet apron/rib/foam/contact objects and mask/profile bindings through Unity API, then verify the 1 m shoreline pass.

Sky/Aegir repair follows unless readback proves it is the blocking owner before terrain/water/contact.

Additional h8_1475 proof predicates:

- Packet root: `Docs/Screenshots/HectonProofPackets/h8_1475_<session>/`.
- Required files: `manifest.json`, `manifest.sha256`, Unity log, console/readback/dirty/stats artifacts.
- Required screenshots: `01_surface_coast_aegir_ui_off.png`, `02_shoreline_close_1m.png`, `03_underwater_0_5m.png`, `04_underwater_20_50m_route.png`, `05_aegir_celestial_long.png`, `06_regression_low_oblique.png`.
- Process gate green, clean dirty before/after, 60 clean post-capture seconds, no compile/import/log/leak markers, screenshots outside `Assets`, no `.png.meta`, all PNGs unique and at least `1280x720`, manifest/log newer than final screenshot.
- `global_quality_weight` must be a continuous float `[0,1]`; binary low/medium/high/ultra labels are invalid as proof truth.
- Visual pass requires cyan/blue readable ocean, real wave structure, foam/wet shoreline contact, non-black terrain/coastline, integrated Aegir/sky/clouds, shallow and medium-depth route readability, and production player/HUD/tool witness.

Updated status: `H8_1921_REJECTED / NEXT_GREEN_PACKET_LOCKED / PENDING UNITY READBACK`.

## 2026-06-06 h8_1925 Pure-Ocean Uniform-Sky Rejection

Evidence class: `STATIC_METADATA / DIRECT_SCREENSHOT_REVIEW / STATIC_SOURCE_PATCH`. No Unity, import, build, Play Mode, profiler, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

Reviewed:

- `Docs/Screenshots/MCP/h8_1925_surface_crest_pure_ocean_uniform_sky_probe.png`
- `Docs/Screenshots/MCP/h8_1925_surface_crest_pure_ocean_uniform_sky_probe.txt`

Direct visual rejection:

- The frame is a sterile ocean/sky test, not a surface route.
- Terrain, coastline, wet rock, foam/contact, Aegir, clouds as composition, player, HUD, tool, movement, and route witness are absent.
- It cannot answer the actual product question: whether `02_HECTON_WORLD` has readable water, terrain, shoreline contact, and sky/Aegir together.

Useful static readback only:

- The diagnostic can show that `MAT_H8_SurfaceCrestOcean_1428` can render blue water in isolation.
- It also proves the false path: cutting terrain/Aegir/route context creates a prettier but useless proof.

Controller action:

- `CaptureSurfaceCrestFlatSkyHorizonProbeAndExit`, `CaptureSurfaceCrestPureOceanFlatSkyProbeAndExit`, and `CaptureSurfaceCrestPureOceanUniformSkyProbeAndExit` are now disabled direct routes.
- Validator and unit tests now enforce that h8_1922, h8_1923, and h8_1925 do not open the scene or render.
- `h8_1920_surface_crest_ocean_extent_probe` remains a narrow diagnostic allowance only; it is not acceptance proof.

Updated status: `H8_1925_REJECTED_AS_ACCEPTANCE / PURE_OCEAN_FALSE_PROOF / DIRECT_ROUTE_DISABLED`.

## 2026-06-06 Pasteur Historical Route Forensic Report

Evidence class: `STATIC_GIT / STATIC_SERIALIZED_YAML_READ / STATIC_SCREENSHOT_REVIEW / STATIC_DOC_REVIEW`. No Unity, import, build, Play Mode, profiler, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

Verdict: end-April / first-week-May surface was a real route, not a color trick. It used a connected cluster: Crest ocean, textured MapMagic terrain/cliffs, Hecton sky, and Aegir material. Current surface is an inconsistent hybrid where the saved prefab, scene runtime capture, and diagnostic probes disagree. Treat it as owner-route breakage, not minor lighting drift.

Historical anchors:

- Commit `857689d2b` changed `02_HECTON_WORLD.unity`, `Ocean_Crest.prefab`, `Assets/Crest/Crest/Materials/Ocean.mat`, and `Mat_HectonSky.mat` in one surface cluster on 2026-04-28.
- Historical `Ocean_Crest.prefab` used `Assets/Crest/Crest/Materials/Ocean.mat` GUID `9def92ac79181fe41b238e91663f0fad`.
- Historical Crest prefab had `_createSeaFloorDepthData: 1` and `_createFoamSim: 1`.
- Historical `Ocean.mat` carried `_FOAM_ON`, `_TRANSPARENCY_ON`, `_UNDERWATER_ON`, `_SUBSURFACESHALLOWCOLOUR_ON`, `_Foam=1`, `_Transparency=1`, `_Underwater=1`, and cyan shallow/subsurface color values.
- Historical reference images label the route as MapMagic + textures + Crest ocean + sky material, and show textured cliffs, readable waterline, cyan water, and Aegir integrated into the sky.

Current broken anchors:

- Current `Assets/_Project/Prefabs/Ocean_Crest.prefab` points at `MAT_H8_SurfaceCrestOcean_1428` GUID `cb6742dd8bbf8d843ba150a5e6dd5eb9`, but has `_viewpoint`/`_camera` null and `_createSeaFloorDepthData: 0`, `_createFoamSim: 0`.
- Current `h8_1921` metadata shows scene runtime object `H8_WORLD_CREST_OCEAN_RUNTIME_1428` using `Assets/Crest/Crest/Materials/Ocean.mat`, with `_viewpoint=NULL`, `_camera=NULL`, `_followSceneCamera=False`, `_createSeaFloorDepthData=False`, and ocean input tiles `active=0 enabled=0`.
- Current `h8_1921` also reports `MapMagic.enabled=False`, sandbox graph `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`, and `gridCount=0`.
- Diagnostic `h8_1917` / `h8_1919` temporarily bind `ACTUAL TERRAIN`, `MAT_H8_SurfaceCrestOcean_1428`, camera/viewpoint, depth, foam, and shadow data, but they are editor-only unsaved probes using temporary sky/Aegir materials. They are not green proof.
- `h8_1925` proves only that the first-party ocean material can render blue water in isolation. It cuts terrain, Aegir, shoreline foam, player, HUD, tool, movement, and route context.

Canonical next-green action list:

1. Unity API readback only: load `02_HECTON_WORLD`, record dirty state, active scene, RenderSettings, camera, and console state. Do not save.
2. Enumerate all `Crest.OceanRenderer` instances and prefab sources. Read material path/GUID, `_viewpoint`, `_camera`, `_followSceneCamera`, LOD/extents, depth/foam/shadow flags, and active input counts.
3. Repair one canonical Crest owner only through `SerializedObject` / `PrefabUtility`: bind camera/viewpoint, enable sea-floor depth and foam if route owner confirms, and assign the chosen existing material. No runtime material clones.
4. Read MapMagic object state: enabled, graph path/GUID, grid count, terrain count, pinned tiles, and apply mode. Compare sandbox graph versus `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`.
5. Read `ACTUAL TERRAIN` generator states for erosion, splat sediment, and anomaly links. Disabled or unlinked production nodes become intentional quarantine or repair targets before generation.
6. Enumerate active `Terrain` objects: material, terrain layers, size, height range, and waterline intersection. Reject `H8_WORLD_TERRAIN_SHELL_1428` fallback as product terrain proof.
7. Read shoreline anchors by exact object/material names: `H8_ORGANIC_SHORELINE_FOAM_FINE_1469`, wet coast apron, coastal ribs, foam ring, and Crest foam inputs. Repair only if geometry intersects the waterline.
8. Read sky/Aegir: `RenderSettings.skybox`, sun/fog/ambient, Aegir material path, texture GUIDs, transform, layer, and bounds. Reject temporary proof materials and prototype gas-giant discs.
9. After repair, capture one proof packet with screenshot, metadata manifest, console state, Frame Debugger stats, and player/HUD/tool witness. Until then status remains `PENDING VERIFICATION`.

Scalability consequence: Low preserves the same route with reduced LOD/tile/cadence, not flat water. Middle restores stable terrain-water-foam contact. High adds richer sky/shore/water material detail. Ultra spends on reflections/cloud/Aegir polish after the route is proven. No tier changes route ownership or uses darkness to hide failure.

Updated status: `HISTORICAL_ROUTE_BREAK_CONFIRMED / OWNER_ROUTE_REPAIR_REQUIRED / PENDING UNITY READBACK`.

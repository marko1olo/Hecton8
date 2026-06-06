# Unity Worker Dialogue Reference Rejection - 2026-06-06

Status: `CONTROLLER_STEER / STATIC_DIALOGUE_AND_IMAGE_REVIEW / VISUAL_REJECTED`.
Evidence class: `USER_ATTACHMENT + DIRECT_SCREENSHOT_REVIEW + REFERENCE_IMAGE_REVIEW + STATIC_METADATA`.

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, project-setting mutation, runtime source mutation, or raw YAML edit was performed by this controller pass.

## Evidence Reviewed

- `C:\Users\danat\.codex\attachments\2f281a38-64e6-4996-8703-bf2ace6239e8\pasted-text.txt`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt`
- `Docs/Screenshots/MCP/h8_1914_surface_water_recovery_probe.png`
- `Docs/Screenshots/MCP/h8_1914_surface_water_recovery_probe.txt`
- `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)/BEST ILLUST - ON SURFACE (WITH TREES AND GRASS) - CHECK WATER, GAS GIANT. it is perfect! your goal to look like it! make plan and do it.png`
- `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)/CLIFFS AND WATER PREVIOUSLY IN DEVELOPMENT (MAPMAGIC + TEXTURES + CREST OCEAN).jpg`
- `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)/CLIFFS SKY AND GAS GIANT PREVIOUSLY IN DEVELOPMENT (MAPMAGIC + TEXTURES + CREST OCEAN + SKY MATERIAL (CHECK COMMITS IN MAY 1ST WEEK, IT IS THIS MATERIAL)).jpg.jpg`

## Controller Verdict

The Unity worker's direction was wrong for too long. The dialogue shows repeated local A/B attempts around green haze, temporary water-skin cards, MCP bridge recovery, and h8_1914 diagnostic captures while the visible route still lacked a real surface ocean, readable terrain, shoreline contact, and gameplay witness.

This is not a color-tuning failure. It is a route-authorship failure:

- no accepted active surface-water route;
- no accepted active MapMagic/Crest/sky route comparable to the late-April and first-week-of-May references;
- no production player/HUD/tool proof;
- no canonical h8_1475 no-mutation proof packet;
- h8_1914 diagnostic output kept being treated as if it could become acceptance.

## Direct Visual Rejection

`h8_1914_surface_crest_recovery_probe.png` is rejected:

- the water reads as a dark rectangular sheet, not an ocean body;
- the lower-right terrain/material patch is a visible rectangular checker-like artifact;
- island/shore geometry has black undercut chunks and detached silhouettes;
- the shoreline lacks credible wet edge, foam breakup, shallow transparency, and material contact;
- terrain reads as green/black noise, not lit wet geology;
- Aegir is present but reads pasted and weakly integrated with atmosphere and horizon;
- no player, HUD, tool, or interaction route is visible or proved.

`h8_1914_surface_water_recovery_probe.png` is also rejected:

- the probe proves only that a green rectangular plane can cover the hole;
- the water skin has visible straight edges;
- the hue is neon green/acid, not the cyan/blue surface target;
- the coastline remains black and broken;
- foam/contact/shoreline truth remains absent;
- metadata says the probe object is missing from route scan, so the proof is not production state.

## Reference Target

The mandatory surface reference requires:

- broad cyan/blue readable ocean, not green haze or acid water;
- wave form, surface sparkle, depth, and distant water plane continuity;
- whitewater/foam at coast contact;
- sculpted cliffs and island silhouettes with material breakup;
- dense but readable flora/geology after the base route is correct;
- sky/cloud mass and Aegir integrated through atmosphere, not a pasted translucent sphere;
- route/gameplay witness when first-20-minutes proof is required.

The current h8_1914 output misses the target by structure, not by tuning.

## Worker Error Pattern

Rejected worker pattern:

1. Treating missing water as a material color problem.
2. Treating missing terrain/coast route as haze/post-process problem.
3. Running temporary h8_1914 captures and A/B cards instead of recovering the authoritative MapMagic + Crest + sky route.
4. Letting MCP instability consume time without separating proof-tool failure from route failure.
5. Considering diagnostic scenery without active player/HUD/tool predicates.
6. Not using the mandatory reference images as the primary acceptance frame.

## Historical Route Anchors

Static git/doc evidence gives comparison anchors, not raw revert permission:

- `857689d2b` is the strongest Apr 28 sky/Crest/scene comparison candidate. It changed `02_HECTON_WORLD.unity`, `Assets/Crest/Crest/Materials/Ocean.mat`, `Assets/_Project/Art/Materials/Mat_HectonSky.mat`, and `Hecton_AegirHazeOverlay.shader`.
- `06cff4605`, plus first-week-May commits `2442f78e9` and `474759516`, are MapMagic graph comparison candidates for `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`.
- `fd33b9521` and `7073862dd` are May 1 scene placement candidates.
- `b81b0da81^` is the pre-divergence terrain/sky shader comparison point before later terrain/sky shader changes.

These anchors are for no-mutation Unity readback and A/B comparison only. Blind scene YAML revert, blind Crest package shader edits, blind material assignment, and more `h8_1914` capture-tool extension remain rejected.

## Corrected Steering

Future Unity surface owner must not start with another green/blue overlay pass. The next valid path is:

1. Wait for process gate green.
2. Perform no-mutation readback of active scene, dirty state, player/HUD/tool route, Crest OceanRenderer/material, MapMagic terrain state, terrain shell, shoreline/foam renderers, skybox, clouds, Aegir, and console.
3. Compare active route against late-April / first-week-of-May MapMagic + Crest + sky evidence.
4. Prove whether the saved Crest route or first-party `MAT_H8_SurfaceCrestOcean_1428` candidate is the correct owner before assignment.
5. Fix slab/ocean extent/horizon geometry before haze/post.
6. Fix terrain height/material/lighting route before flora/rocks/coral.
7. Fix wet shoreline and foam/contact before decorative density.
8. Fix Aegir/sky/cloud integration.
9. Only then attempt h8_1475 no-mutation proof through a separate canonical harness.

## Hard Rejections

- Green haze as root fix.
- Temporary water cards as product proof.
- `h8_1914_*` output as acceptance.
- Extending `H8VisualProofCapture1912` for h8_1475.
- Cosmetic flora/geology placement while water/terrain/sky base is rejected.
- Landscape-only proof without player/HUD/tool/movement when first-20 proof is required.
- Darkness, fog, bloom, vignette, or Aegir scale used to hide weak water/terrain.

## 2026-06-06 Refresh

Additional direct image review:

- `base.webp` confirms the minimum undersea product face: clear blue water, readable modules, visible terrain slope, and no muddy/acid water cast.
- `beauty.webp` confirms photic-shallow target language: bright cyan water, readable rock forms, attached flora/coral, strong depth read, and material-scale witnesses.

The current `h8_1914_surface_crest_recovery_probe.png` fails both. It is not a weak pass; it is a wrong route: yellow-black foreground carpet, rectangular water shelf, black island undercuts, weak shore contact, and toy-scale Aegir integration.

The current `h8_1914_surface_water_recovery_probe.png` also fails both. The green/acid rectangle proves only that a temporary card can cover the hole. It does not provide ocean body, shoreline truth, terrain lighting, or material identity.

Fresh validators:

- `ValidateTerrainProbeEvidence.py --require-production` rejects the current h8_1914 metadata with `blockers=10`: missing log, non-production `captureTruth`, editor-only unsaved, h8_1914 diagnostic marker, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- `ValidatePlayerRouteStaticEvidence.py --require-production-static` currently rejects the player route with `blockers=19 notes=4`; scenic surface proof cannot waive this.
- `ValidateAssetProofArtifactIndex.py` accepts the proof index only because it classifies diagnostic h8_1914 assets as rejected, not because it accepts them.

## Low / Middle / High / Ultra

- Low: clean readable cyan/blue water, shore contact, terrain silhouette/material identity, sky/Aegir, and player/HUD/tool route still pass. No ugly green fallback.
- Middle: stable foam/contact breakup, terrain material scale, water depth read, and route composition.
- High: richer normals, caustics, reflection, shoreline wetness, sky/cloud/Aegir integration, and near-field geology/flora after base pass.
- Ultra: capture-grade density and polish only after the same route truth and proof predicates pass.

Final status: `REJECTED_AS_ACCEPTANCE / USEFUL_AS_FAILURE_EVIDENCE`.

## 2026-06-06 h8_1915 Refresh

Reviewed:

- `Docs/Logs/UnityCaptureSurfaceCrestAprilRouteProbe_EditorGPU_20260606_075327.log`
- `Docs/Screenshots/MCP/h8_1915_surface_crest_april_route_probe.png`
- `Docs/Screenshots/MCP/h8_1915_surface_crest_april_route_probe.txt`

Static proof result:

- `python -B Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestAprilRouteProbe_EditorGPU_20260606_075327.log --metadata Docs\Screenshots\MCP\h8_1915_surface_crest_april_route_probe.txt --require-production` returns `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`.
- Blockers: non-production `captureTruth`, Unity `MemoryLeaks`, editor-only unsaved capture, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.

Direct visual rejection:

- The water is now visibly an ocean route, but it still reads as a diagnostic cyan/green sheet under a hard horizon band.
- Coast/island geometry remains black-clipped and detached; shoreline contact still lacks convincing wet edge, foam breakup, and shallow transparency.
- Foreground terrain reads as yellow-green carpet/noise, not premium wet geology.
- Aegir is present but oversized and weakly integrated with the sky/horizon.
- No player, HUD, tool, movement, or interaction witness exists.

Useful readback:

- Active Crest reports `MAT_H8_SurfaceCrestOcean_1428` in the diagnostic capture.
- `MAT_H8_SurfaceCrestOcean_1428` is still not accepted as production proof because the capture is editor-only unsaved and the MapMagic graph remains disabled/unlinked.
- `SURFACE_HORIZON_SALT_HAZE_1428` is still active and must not become the root fix.
- MapMagic graph state remains structurally broken for production terrain: erosion/anomaly disabled and required inputs unlinked.

Updated status: `H8_1915_REJECTED_AS_ACCEPTANCE / USEFUL_AS_READBACK_AND_FAILURE_EVIDENCE`.

## 2026-06-06 h8_1916 Refresh

Reviewed:

- `Docs/Logs/UnityCaptureSurfaceCrestCleanTerrainProbe_EditorGPU_20260606_073231.log`
- `Docs/Screenshots/MCP/h8_1916_surface_crest_clean_terrain_probe.png`
- `Docs/Screenshots/MCP/h8_1916_surface_crest_clean_terrain_probe.txt`

Static proof result:

- `python -B Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestCleanTerrainProbe_EditorGPU_20260606_073231.log --metadata Docs\Screenshots\MCP\h8_1916_surface_crest_clean_terrain_probe.txt --require-production` currently returns `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`.
- Blockers are unchanged in kind from `h8_1915`: non-production `captureTruth`, Unity `MemoryLeaks`, editor-only unsaved capture, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- `H8VisualProofCapture1912` guardrail count increased to `violations=93`; the diagnostic tool is expanding, not becoming canonical proof.

Direct visual rejection:

- Terrain layers are cleaner than the previous acid/yellow foreground, but this is a symptom polish, not route repair.
- The coastline still reads as black clipped chunks.
- The ocean still has a hard horizon band and weak shoreline contact.
- Aegir is still oversized and weakly integrated.
- No player, HUD, tool, movement, or interaction witness exists.

Updated status: `H8_1916_REJECTED_AS_ACCEPTANCE / CLEANER_TERRAIN_SYMPTOM_ONLY / USEFUL_AS_FAILURE_EVIDENCE`.

## 2026-06-06 h8_1917 Direct Reference Review

Reviewed:

- `Docs/Screenshots/MCP/h8_1917_surface_crest_daylight_probe.png`
- Mandatory reference: `BEST ILLUST - ON SURFACE ...`
- Prior in-development reference: `CLIFFS AND WATER PREVIOUSLY IN DEVELOPMENT (MAPMAGIC + TEXTURES + CREST OCEAN).jpg`

Direct visual rejection:

- `h8_1917` water still reads as a flat cyan rectangular sheet, not ocean volume.
- The horizon has a hard black artificial strip.
- Coast and islands are black clipped masses with no wet-rock transition, no material-scale read, and no convincing shallow transparency.
- Shoreline contact has weak or absent foam breakup.
- Foreground water edge is visibly planar and presentation-driven.
- Aegir is overbearing and toy-integrated: huge, high-contrast, and not blended into sky light/atmosphere.
- No player, HUD, tool, movement, or interaction witness exists.

Reference delta:

- The surface reference demands patterned ocean scale, whitewater, readable coastline geometry, route vegetation, integrated Aegir/sky, and bright breathable air.
- The prior in-development cliff/water reference already shows the key missing route traits: transparent shallow water, readable submerged rock, non-black shoreline, and water/rock contact.
- Therefore the current failure is not an unrealistic new art target. It is a route regression or active-owner failure against project-local prior evidence.

Updated status: `H8_1917_REJECTED_AS_ACCEPTANCE / ACTIVE_MAT_NOT_ENOUGH / ROUTE_REGRESSION_EVIDENCE`.

## 2026-06-06 h8_1917 Daylight Refresh

New external evidence:

- `Docs/Logs/UnityCaptureSurfaceCrestDaylightProbe_EditorGPU_20260606_074642.log`
- `Docs/Screenshots/MCP/h8_1917_surface_crest_daylight_probe.png`
- `Docs/Screenshots/MCP/h8_1917_surface_crest_daylight_probe.txt`

Findings:

- `python -B Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestDaylightProbe_EditorGPU_20260606_074642.log --metadata Docs\Screenshots\MCP\h8_1917_surface_crest_daylight_probe.txt --require-production` returned `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10`.
- Blockers: non-production `captureTruth`, Unity `MemoryLeaks`, `compile-input-mutated`, editor-only unsaved capture, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- Visual rejection: overbearing Aegir, black hard horizon strip, cyan slab water, dark clipped shoreline masses, weak/no shoreline contact foam, and crude terrain silhouette.

Updated status: `H8_1917_REJECTED_AS_ACCEPTANCE / USEFUL_AS_FAILURE_EVIDENCE_ONLY`.

## 2026-06-06 h8_1918 Coast-Horizon Refresh

New external evidence:

- `Docs/Logs/UnityCaptureSurfaceCrestCoastHorizonProbe_EditorGPU_20260606_080213.log`
- `Docs/Screenshots/MCP/h8_1918_surface_crest_coast_horizon_probe.png`
- `Docs/Screenshots/MCP/h8_1918_surface_crest_coast_horizon_probe.txt`

Findings:

- `python -B Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestCoastHorizonProbe_EditorGPU_20260606_080213.log --metadata Docs\Screenshots\MCP\h8_1918_surface_crest_coast_horizon_probe.txt --require-production` returned `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`.
- Blockers: non-production `captureTruth`, Unity `MemoryLeaks`, editor-only unsaved capture, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- Visual rejection: overbearing Aegir, black hard horizon strip, cyan slab water, black clipped shoreline masses, weak/no shoreline contact foam, and noisy gold/speckled terrain.

Updated status: `H8_1918_REJECTED_AS_ACCEPTANCE / USEFUL_AS_FAILURE_EVIDENCE_ONLY`.

## 2026-06-06 h8_1921 Surface-Owner Lighting Refresh

Reviewed:

- `Docs/Screenshots/MCP/h8_1921_surface_owner_lighting_nonmutating.png`
- `Docs/Screenshots/MCP/h8_1921_surface_owner_lighting_nonmutating.txt`

Static metadata facts:

- `captureTruth=surface_owner_lighting_main_camera_nonmutating`; this is still not h8_1475 acceptance.
- Scene is `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- `H8_WORLD_CREST_OCEAN_RUNTIME_1428` uses `Assets/Crest/Crest/Materials/Ocean.mat`, not first-party `MAT_H8_SurfaceCrestOcean_1428`.
- Crest state reports `_ClipSurface=1`, `_ClipUnderTerrain=1`, `_viewpoint=NULL`, `_camera=NULL`, `_showOceanProxyPlane=False`, `_waterBodyCulling=True`, `_createSeaFloorDepthData=False`, `_createFoamSim=True`.
- Ocean tile summary reports only three disabled/inactive input renderers and `totals rendererCount=3 active=0 enabled=0 inFrustum=0`; this is not a visible ocean-body proof.
- MapMagic scene object remains disabled and points to `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`.
- `ACTUAL TERRAIN.asset` still has erosion disabled, anomaly disabled, anomaly height unlinked, and splat sediment unlinked in the metadata.
- `H8_PhoticRouteTerrain_1464` exists but `activeHierarchy=False`; current visible route leans on shell/coast objects, not a proven production terrain pipeline.

Direct visual rejection:

- Foreground water reads as a rectangular, flat, dark-green/gold sheet with a visible planar edge. It is not ocean volume.
- Coast and island undercuts are black clipped masses, not readable wet rock.
- Shore contact lacks convincing whitewater, wet edge, shallow transparency, and foam breakup.
- Aegir is oversized and toy-integrated: it reads as a huge backdrop decal behind black terrain, not atmospheric scale.
- Terrain/shore composition still lacks the reference route density, readable material scale, and believable coastline shape.
- No production player/HUD/tool/movement witness exists.

Updated status: `H8_1921_REJECTED_AS_ACCEPTANCE / CURRENT_FAILURE_EVIDENCE / NEXT_ACTION_IS_ROUTE_READBACK_NOT_COLOR_TUNING`.

## 2026-06-06 Pasted Worker Dialogue Refresh / h8_1925

New evidence:

- `C:\Users\danat\.codex\attachments\2f281a38-64e6-4996-8703-bf2ace6239e8\pasted-text.txt`
- `Docs/Screenshots/MCP/h8_1925_surface_crest_pure_ocean_uniform_sky_probe.png`
- `Docs/Screenshots/MCP/h8_1925_surface_crest_pure_ocean_uniform_sky_probe.txt`

Worker-line classification:

- The worker spent cycles on green haze, green/acid water tuning, temporary water-skin/card probes, pure-ocean sky probes, and repeated `H8VisualProofCapture1912` diagnostic captures.
- The user complaints were technically correct: the frame failure was not "water hue"; the route lacked credible water and terrain together.
- The late `h8_1925` image is not a fix. It is a blue-water isolation test with the game removed.

Hard rejection:

- Green overlay/card direction: rejected.
- Pure-ocean / flat-sky direction: rejected as product proof.
- More `H8VisualProofCapture1912` direct execute captures as acceptance: rejected.
- Any screenshot without terrain, shoreline contact, sky/Aegir integration, and player/HUD/tool witness: rejected for first-20 proof.

Controller action:

- `h8_1922`, `h8_1923`, `h8_1924`, and `h8_1925` direct diagnostic routes are disabled in source.
- Guard validator now has explicit tests preventing pure-ocean/flat-sky route revival.
- Current front is still `PENDING UNITY READBACK` after process gate green: player/camera/HUD baseline, MapMagic/terrain, Crest/ocean, shoreline/contact, sky/Aegir/lighting, then h8_1475.

Updated status: `WORKER_DIRECTION_REJECTED / PURE_OCEAN_FALSE_PROOF_DISABLED / ROUTE_READBACK_REQUIRED`.

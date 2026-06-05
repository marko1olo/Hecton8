# 1865 Sky/Ocean Primitive Risk Proof Packet

Date: 2026-06-04
Agent: 1865
Evidence class: STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

Audited the two high-risk sky/ocean primitive prefabs named by 1859:

- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`

No source, prefab, asset, scene, binary, `.meta`, importer, bake, Unity Editor, PlayMode, screenshot, profiler, or build action was executed. This packet writes only the owned 1865 status/log/rationale/report/matrix files.

First-20-minutes impact: surface exit, coastline/ocean read, Aegir/moon readability, and photic-shallow water are product-face blockers. Static primitive sky/ocean carriers cannot be accepted as final visual proof.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `water.md` used as binding ocean authority because requested `ocean.md` is missing at project root.
- `Docs/Reports/Batch18/1859_NON_PROXY_PRIMITIVE_PREFAB_CLASSIFICATION_PACKET.md`
- `Docs/Reports/Batch18/1859_NON_PROXY_PRIMITIVE_PREFAB_MATRIX.csv`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

`Docs/Actual Domains of Project.txt` was checked and is missing. Narrow domain inferred from task: sky/ocean/world/water/rendering proof.

## Static Method

- Read prefab YAML for primitive mesh references, renderer enabled flags, material GUIDs, component names, active flags, and Crest input flags.
- Read prefab `.meta` GUIDs and searched current `Assets` references.
- Read `02_HECTON_WORLD.unity` prefab-instance overrides for the two prefab GUIDs.
- Resolved material/script GUIDs by `.meta` text search.
- Searched relevant current source paths for likely owners and runtime path.

## Evidence Boundary

Static text proves file content, references, and scene override text only. It does not prove:

- Unity import validity;
- actual renderer state after Crest registers inputs;
- GameView composition;
- sky material quality;
- Aegir/moon readability;
- ocean surface quality;
- waterline/refraction/foam behavior;
- Low/Middle/High/Ultra scaling;
- profiler, GC, Frame Debugger, or player-capture state.

All visual acceptance remains `PENDING VERIFICATION`.

## Prefab 1: `Assets/_Project/Prefabs/Sky_System.prefab`

### Static Prefab Evidence

- Prefab GUID: `0f6bce861507514438034ae0ebadea15`.
- Root `Sky_System` is active.
- Child `Sphere` is active.
- Child MeshFilter uses Unity built-in primitive mesh:
  - `m_Mesh: {fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}`
  - `10207` is Unity built-in sphere.
- Child MeshRenderer is enabled:
  - `m_Enabled: 1`
  - shadow casting disabled, receive shadows enabled in prefab source.
- Prefab material resolves to:
  - `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - shader GUID `6302a783d2378694c9db8d0036358965`
  - cloud/star textures are assigned, but static material fields do not prove visual quality.
- Component:
  - `SkySystemFollowCamera`
  - script path: `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
  - follows scene/runtime camera through dispatcher/LateFrame route when playing.

### Scene Reference Evidence

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` instantiates `Sky_System.prefab`.
- Scene instance overrides the primitive MeshFilter:
  - `m_Mesh` -> `Assets/_Project/Art/Models/SkyDome_Inverted.asset` (`82a557da3388e5c4ab037b7bce64c08f`)
- Scene instance overrides material:
  - `m_Materials.Array.data[0]` -> `Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat` (`4746c0454c9f1a74c84e406956ab30e3`)
- Scene instance overrides renderer properties:
  - `m_ReceiveShadows = 0`
  - `m_ReflectionProbeUsage = 0`
  - `m_MotionVectors = 2`
- Scene instance assigns `runtimeCamera` and sets `followVerticalPosition = 1`, `lockToSeaLevel = 0`.
- Scene instance adds child objects under the sky root, but static text alone does not prove their visual composition or Aegir/moon readability.

### Likely Owner And Runtime Path

- Likely owner: Celestial/sky presentation.
- Follow owner: `SkySystemFollowCamera`.
- Celestial runtime influence likely routes through `HectonCelestialEngine` in `02_HECTON_WORLD`, which owns Aegir direction, moon/celestial shader globals, eclipse/planet-shine state, and celestial black-box fields.
- The scene instance appears to be the production path for `02_HECTON_WORLD`, but source prefab remains a primitive-enabled asset and should not be treated as final art proof.

### Static Decision

`PENDING RUNTIME PROOF`.

The scene instance reduces immediate production risk by overriding the built-in sphere to `SkyDome_Inverted.asset` and a specific panorama material. The source prefab still contains an enabled built-in primitive sphere, so the prefab asset itself is not accepted. No static evidence proves the actual sky is bright, premium, non-muddy, or Subnautica-floor compliant.

### What Cannot Be Accepted From Static Evidence

- Actual sky dome appearance.
- Actual Aegir texture quality, scale, cloud-band detail, atmospheric softness, or horizon placement.
- Moon readability and material quality.
- Surface luminance/readability in normal day, night, eclipse, and storm windows.
- Whether the scene override survives prefab reverts, instantiation outside `02_HECTON_WORLD`, addressable spawning, or future scenes.
- Whether primitive sphere source can leak into production via another prefab instance.

## Prefab 2: `Assets/_Project/Prefabs/Ocean_Crest.prefab`

### Static Prefab Evidence

- Prefab GUID: `0a7f97b6028cb014e80782578e9bf734`.
- Root `Ocean_Crest` is active.
- Crest ocean component:
  - `Crest.OceanRenderer`
  - material: `Assets/Crest/Crest/Materials/Ocean.mat`
  - `_showOceanProxyPlane: 0`
  - `_minScale: 8`, `_maxScale: 256`, `_lodDataResolution: 256`, `_geometryDownSampleFactor: 2`, `_lodCount: 6`
  - `_createSeaFloorDepthData: 0`, `_createFoamSim: 0`, `_createDynamicWaveSim: 0`, `_createFlowSim: 0`, `_createAlbedoData: 0`
- `Crest.ShapeFFT` is enabled with `Spectrum.asset` and 128 resolution.
- `OceanDepthCache` child is inactive and its component is disabled in prefab source.

Primitive references in prefab source:

1. `SargassumOilFilmInput`
   - GameObject active.
   - Built-in primitive plane mesh:
     - `m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}`
   - MeshRenderer enabled in prefab:
     - `m_Enabled: 1`
   - material: `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat`
   - Crest component: `Crest.RegisterAlbedoInput`
   - `_disableRenderer: 1`

2. `SargassumWaveDampingInput`
   - GameObject active.
   - Built-in primitive plane mesh `10209`.
   - MeshRenderer enabled in prefab.
   - material: `Assets/_Project/Art/Materials/MAT_SargassumWaveDamping.mat`
   - Crest component: `Crest.RegisterAnimWavesInput`
   - `_disableRenderer: 1`

3. `SargassumFoamDampingInput`
   - GameObject active.
   - Built-in primitive plane mesh `10209`.
   - MeshRenderer enabled in prefab.
   - material: `Assets/_Project/Art/Materials/MAT_SargassumFoamDamping.mat`
   - Crest component: `Crest.RegisterFoamInput`
   - `_disableRenderer: 1`

4. `SargassumMicroFaunaBoids`
   - component enabled on root.
   - `boidMesh` references built-in primitive plane mesh `10209`.
   - material: `Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat`
   - this is not a MeshRenderer field; it is an indirect/presentation mesh input and remains a visible primitive risk until GameView capture proves it does not read as flat planes.

### Scene Reference Evidence

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` instantiates `Ocean_Crest.prefab`.
- Scene instance renames root to `H8_WORLD_CREST_OCEAN_RUNTIME_1428`.
- Scene instance overrides all three primitive input MeshRenderers disabled:
  - `SargassumWaveDampingInput` renderer `m_Enabled = 0`
  - `SargassumFoamDampingInput` renderer `m_Enabled = 0`
  - `SargassumOilFilmInput` renderer `m_Enabled = 0`
- Scene instance keeps Crest ocean material as `Assets/Crest/Crest/Materials/Ocean.mat`.
- Scene instance assigns `_primaryLight`.
- Scene text does not override `SargassumMicroFaunaBoids.boidMesh`, so the built-in plane mesh remains a static presentation risk.

### Likely Owner And Runtime Path

- Ocean renderer owner: Crest bridge/ocean presentation.
- First-party kinematics owner path:
  - `Assets/_Project/Scripts/Plugins/Crest/Crest4KinematicsAdapter.cs`
  - registers with `OceanVisualBridgeRegistry` and `OceanKinematicsRuntimeService`.
- Sargassum visual/input owner path:
  - `Assets/_Project/Scripts/World/SargassumCrestDampingController.cs`
  - `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- Ecosystem/AI consumers reference `SargassumMicroFaunaBoids.ActiveRuntimeInstance`.
- Editor/static validators reference `Ocean_Crest.prefab` for ocean render layout constraints.

### Static Decision

`PENDING RUNTIME PROOF`.

The scene instance explicitly disables the three Crest input renderers, and the Crest input components also serialize `_disableRenderer: 1`. That supports a hidden/input-only interpretation for those three planes in `02_HECTON_WORLD`, but it does not accept the prefab source because the source MeshRenderers are enabled. The micro-fauna `boidMesh` still references a built-in plane and is not covered by the scene renderer-disable overrides.

### What Cannot Be Accepted From Static Evidence

- Whether Crest disables input renderers before any frame where they could flash.
- Whether another scene/spawn path instantiates the prefab without the `02_HECTON_WORLD` renderer-disable overrides.
- Whether the ocean surface looks premium in daylight, shore approach, waterline, or photic shallows.
- Whether foam/refraction/specular/shoreline/subsurface settings read as real water rather than generic blue/dark fog.
- Whether micro-fauna primitive planes are visible as flat cards/quads.
- Whether Low/Middle/High/Ultra quality routes preserve beauty and material identity.
- Whether Crest cost, GPU passes, draw calls, transparent overdraw, GC, and kinematics sampling stay inside budget.

## Required Visual Proof Plan

The next proof pass must use Unity Editor/GameView/player capture. Static source is not enough.

### Required Editor Screenshot Angles

For `Sky_System`:

- Scene View, surface camera at first-exit height, looking east toward Aegir/horizon.
- Scene View, looking straight up enough to expose sky dome/cloud gradients and moons.
- Scene View, near coastline/wet rock with sky, water, and terrain in one frame.
- Scene View with sky object selected or hierarchy visible enough to prove the active instance uses `SkyDome_Inverted.asset`, not the prefab primitive sphere.
- Inspector capture of active scene instance MeshFilter and material override.

For `Ocean_Crest`:

- Scene View above water, low grazing angle across ocean skin.
- Scene View at waterline, half above/half below if possible.
- Scene View just below surface in photic shallows, looking toward terrain and shore.
- Scene View with the three Sargassum input objects selected or Inspector-visible, proving renderers disabled at runtime/editor instance.
- Scene View of micro-fauna/boid presentation area if active, close enough to detect primitive planes/cards.

### Required GameView / Player-Capture Proof

- Normal daylight surface shot: ocean, sky, Aegir/moons if visible, coastline/wet rock, no darkness excuse.
- Shore approach shot: foam, waterline wetness, refraction, terrain seen through shallow water.
- Photic shallow underwater shot: surface caustic hint, readable route, not generic blue fog.
- Waterline transition shot: camera crossing surface without ugly clipping, flat planes, or horizon seams.
- Aegir readability shot: scale, texture/cloud bands, atmosphere/halo, no muddy sine-stripe look.
- Moon readability shot: silhouettes/materials remain legible and not tiny debug props.
- Ocean stress shot: wave normals/specular/foam under normal weather; storm/fog may be additional proof only, not the primary acceptance shot.
- Micro-fauna/sargassum shot: prove boid planes/cards do not read as visible primitive quads.

### Required Render/Performance Proof

- Frame Debugger or RenderGraph proof for ocean/sky passes and no hidden expensive pass chain.
- Profiler proof for Crest ocean, sargassum damping, micro-fauna presentation, and sky/celestial update.
- GC proof for runtime ocean/sky update paths.
- Low-tier/compact capture with the same route cues visible.
- High/Ultra capture showing added sensory richness without changing route truth.

## Acceptance Expectations By Tier

Compact / Low:

- Surface remains bright, readable, and attractive.
- Ocean keeps believable color, specular, wave read, foam cue, and waterline identity.
- Sky keeps clean horizon gradient, cloud read, and Aegir/moon silhouette/readability.
- Sargassum input planes must be hidden before player-visible frames.
- Micro-fauna, if visible, must dissolve into premium VFX/biological motion, not flat planes.

Middle:

- Adds richer water response, cleaner shoreline foam, more stable shallow refraction, and better cloud motion.
- Keeps route/shore/photic-shallow readability without relying on dark grading.

High:

- Adds stronger reflections/specular, richer clouds, better Aegir/moon atmosphere, more convincing surface motion, and denser but controlled micro-fauna.
- Does not change gameplay truth, ocean authority, save identity, or route facts.

Ultra:

- Buys visual overkill: richer cloud depth, planet-shine/halo, better waterline detail, stronger foam breakup, shallow shafts/caustic hints where justified, and sensory density.
- Still cannot use high-tier-only features for navigation or basic readability.

## Rejection Gates

Reject if any proof shot shows:

- primitive sphere/plane visible as final art;
- muddy, black, or crushed surface sky/ocean;
- Aegir as low-detail stripes or unreadable blob;
- moons as untextured/debug spheres;
- ocean as generic blue/dark fog;
- shore/waterline with no foam/specular/refraction identity;
- photic shallows below Subnautica-level readability;
- micro-fauna rendered as obvious flat primitive cards;
- visual quality switch that jumps between binary low/high modes instead of continuous `GlobalQualityWeight`;
- storm/fog/darkness used as the only proof state.

## Evidence Claims

Claim: Both requested prefabs contain Unity built-in primitive mesh references.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Prefabs/Sky_System.prefab`, `Assets/_Project/Prefabs/Ocean_Crest.prefab`, `1865_SKY_OCEAN_PRIMITIVE_RISK_MATRIX.csv`
Command or Unity tool: PowerShell `Get-Content`, `rg`
Date: 2026-06-04
Residual risk: no import/runtime proof.

Claim: `02_HECTON_WORLD` instantiates both prefabs and overrides sky mesh/material plus ocean input renderer states.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
Command or Unity tool: PowerShell `Get-Content`, `rg`
Date: 2026-06-04
Residual risk: scene text does not prove actual runtime state or visual quality.

Claim: Neither sky nor ocean can be accepted visually from static evidence.
Evidence Class: STATIC_SOURCE
Artifact: this packet
Command or Unity tool: static review only
Date: 2026-06-04
Residual risk: must be resolved by Unity Editor/GameView/player capture and profiler proof.

## Final Static Classification

| Prefab | Static classification | Reason |
|---|---|---|
| `Sky_System.prefab` | `PENDING RUNTIME PROOF` | Source prefab has enabled primitive sphere. Scene instance overrides to authored mesh/material, but no visual proof exists. |
| `Ocean_Crest.prefab` | `PENDING RUNTIME PROOF` | Source prefab has enabled primitive input plane renderers and primitive boid mesh. Scene disables three input renderers, but runtime visibility and boid presentation are unproven. |


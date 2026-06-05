# 1821 Shoreline Waterline Offline Bake Spec

Agent: 1821
Role: SHORELINE_WATERLINE_BAKE_SPEC_REPLACEMENT
Evidence class: STATIC_DOC / STATIC_SOURCE only
Runtime/editor/profiler/build/screenshot proof: PENDING UNITY SLOT
Final state: STATIC BAKE SPEC COMPLETE

## Boundary

This packet replaces the missing stalled 1807 shoreline/waterline bake spec. It defines the offline bake products, static inputs, existing first-party tools, future material-slot routing, and Unity-slot proof requirements for the first surface/coast/waterline route.

No Unity Editor, PlayMode, profiler, Frame Debugger, build, or live screenshot was run. No scene, prefab, material, shader, package asset, or task file was edited. Static path and YAML evidence is not upgraded to runtime proof.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `water.md`
- `world.md`
- `terrain.md`
- `rendering.md`
- `shaders.md`
- `performance.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`
- `Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.md`
- `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md`

Selected mandates:

- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

`Docs/Actual Domains of Project.txt` was absent. The inferred narrow domain is shoreline/waterline offline bake specification for the first surface route.

## Static Scene Findings

Static YAML search in `Assets/_Project/Scenes/02_HECTON_WORLD.unity` found:

- `H8_SURFACE_COASTAL_ISLAND_1428`: present and `m_IsActive: 1`.
- `H8_SURFACE_SHORE_FOAM_1428`: present but `m_IsActive: 0`.
- `SURFACE_FOAM_RIBBON_1428_0` through `SURFACE_FOAM_RIBBON_1428_17`: present but each sampled YAML block shows `m_IsActive: 0`.
- `H8_SURFACE_OCEAN_READ_1428`: present but `m_IsActive: 0`.

Conclusion: the project has shoreline/coast/foam source objects, but the foam visual route is not proven active. A future Unity owner must inspect, activate or replace, bind material slots, and capture current proof. This packet does not claim visible foam.

## Static Material Findings

`MAT_H8_SurfaceCrestOcean_1428.mat`:

- Shader: Crest `Ocean.shader`.
- Existing texture slots include `_FoamTexture`, `_CausticsTexture`, `_Normals`, `_MainTex`, `_WaveDataTex`, and Crest wave-data samplers.
- Static values include `_Foam: 1`, `_Caustics: 1`, `_ShorelineFoamMinDepth: 1.28`, `_NormalsStrengthOverall: 0.86`, `_RefractionStrength: 0.35`, `_SubSurfaceShallowColour: 1`.
- Role: ocean material candidate for future Unity inspection. Do not mutate Crest package shader or blindly enable realtime Crest foam/depth cameras.

`MAT_H8SurfaceShoreFoam_1428.mat`:

- Shader: `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader`.
- `_BaseMap` and `_MainTex` are wired to `Assets/_Project/Art/TEXTURES/foam.png`.
- Static values include `_Alpha: 0.58`, `_Threshold: 0.34`, `_Softness: 0.24`, `_EdgeFade: 0.16`, `_TilingA`, `_TilingB`, and `_FoamColor`.
- Role: safest current first-party shoreline foam material candidate.
- Risk: associated scene object is inactive by static YAML; runtime proof pending.

`MAT_H8_SurfaceFoamRibbons_1428.mat`:

- Shader GUID resolves to a transparent URP material route, but `_BaseMap` and `_MainTex` are empty in static YAML.
- Role: candidate only.
- Risk: reject as final until a packed foam ribbon texture is generated and assigned.

`H8_ShorelineFoamRibbon_1428.shader`:

- Samples `_BaseMap` red and green as two foam flows.
- Samples blue as breakup.
- Uses edge fade, threshold, softness, alpha, tiling A/B, and `Transparent+12` queue.
- Role: correct shader target for a packed RGB foam ribbon texture.

`MAT_H8SurfaceWetBasaltReal_1428.mat`:

- `_BaseMap` and `_MainTex` resolve to `TX_H8SurfaceBasaltWetSediment_1428.asset`.
- `_BumpMap` resolves to `Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`.
- Static values include `_Smoothness: 0.31`, `_BumpScale: 0.82`, `_EnvironmentReflections: 0.35`.
- Role: primary wet basalt material candidate for future waterline assignment.

`MAT_SurfaceIslandWetBasalt_1428.mat`:

- `_BaseMap` and `_MainTex` resolve to `TX_H8SurfaceBasaltWetSediment_1428.asset`.
- `_BumpMap` is empty.
- Static color is much brighter than the primary wet basalt material.
- Role: secondary candidate only; weaker until normal/detail slots are proven or fixed.

`MAT_H8TerrainLit_BasaltSediment_1428.mat`:

- `_Control`, `_Splat0-3`, `_Normal0-3`, and `_Mask0-3` are empty.
- Role: future terrain material target after control/layer texture bake.
- Risk: cannot be accepted as current terrain material proof.

## Third-Party Boundaries

Read-only source/reference boundaries:

- `Assets/Crest`
- `Assets/MapMagic` if present
- `Assets/GPUInstancer` if present
- `Assets/MeshBaker` if present
- `Assets/SciFiFacility` if present

Future implementation may inspect or configure first-party wrappers and first-party project assets. It must not mutate package materials, shaders, prefabs, textures, or code.

## Offline Bake Products

The shoreline/waterline packet needs these products before visual acceptance:

1. `TX_H8_ShorelineFoamRibbonPacked_1821.png`
   - Suggested path: `Assets/_Project/Art/TEXTURES/Generated/TX_H8_ShorelineFoamRibbonPacked_1821.png`
   - Format intent: RGB mask texture.
   - R: long foam strand flow.
   - G: secondary cross-flow and tide breakup.
   - B: noisy breakup and lace erosion.
   - A: optional reserved alpha or constant opaque import depending on shader needs.
   - Target material: `MAT_H8_SurfaceFoamRibbons_1428` or a future duplicated first-party material.

2. `TX_H8_ShorelineWaterlineMask_1821.png`
   - Suggested path: `Assets/_Project/Art/TEXTURES/Generated/TX_H8_ShorelineWaterlineMask_1821.png`
   - Format intent: packed waterline control.
   - R: contact foam coverage.
   - G: wet edge vertical gradient.
   - B: sediment/salt deposit band.
   - A: caustic edge receiver or confidence.

3. `TX_H8_WetDryBasaltMask_1821.png`
   - Suggested path: `Assets/_Project/Art/TEXTURES/Generated/TX_H8_WetDryBasaltMask_1821.png`
   - Format intent: wet/dry basalt transition.
   - R: wetness.
   - G: drying falloff.
   - B: dark mineral/lichen breakup guard.
   - A: specular boost mask.

4. `TX_BiomeWeightMap_SHORELINE_1821.asset`
   - Suggested path: `Assets/_Project/BakedGeometry/Splatmaps/TX_BiomeWeightMap_SHORELINE_1821.asset`
   - Format intent: BC7 terrain control map.
   - Channels follow existing forge convention: R rock, G sand, B silt, A erosion/deposited silt.

5. `TX_CausticFlipbook_shoreline_1821.png`
   - Suggested path: `Assets/_Project/Art/Textures/Lighting/TX_CausticFlipbook_shoreline_1821.png`
   - Source tool: `CausticOpticsBaker1719`.
   - Format intent: shallow edge caustic flipbook with continuous `GlobalQualityWeight` dimensions.

6. `TX_CausticLightCookie_shoreline_1821.png`
   - Suggested path: `Assets/_Project/Art/Textures/Lighting/TX_CausticLightCookie_shoreline_1821.png`
   - Source tool: `CausticOpticsBaker1719`.
   - Format intent: light cookie for future selected lights or caustic receiver route.

7. `TX_CausticWaterlineMask_shoreline_1821.png`
   - Suggested path: `Assets/_Project/Art/Textures/Lighting/TX_CausticWaterlineMask_shoreline_1821.png`
   - Source tool: `CausticOpticsBaker1719`.
   - Existing baker statically defines a 256 px waterline mask output route.

8. Long-swell read card data
   - Existing source candidates: `TX_H8SurfaceOceanLongSwell_1428.asset`, `TX_SurfaceOceanInterference_1428.asset`, and `TX_H8_SurfaceWaterNormals_1428.asset`.
   - Future output may remain an asset-material binding if Unity proof shows it reads well. If it reads flat, produce a new long-swell card or packed normal/read mask in first-party generated texture space.

## Existing Tool Route

Use existing project tools first:

1. `CausticOpticsBaker1719`
   - Existing editor-only baker for caustic flipbook, light cookie, and waterline mask.
   - Static fields include `GlobalQualityWeight`, `TileMeters`, `ReceiverDepthMeters`, `WaterlineNormalized`, atlas dimensions, and `WaterlineMaskSize = 256`.
   - It validates unmanaged layouts via `UnsafeUtility.SizeOf`.
   - It schedules a job and calls `Complete()` only in editor menu bake context; this is not runtime proof and must not be copied into runtime routes.

2. `BiomeSplatmapForgeWindow` and `BiomeWeightMapBakePipeline`
   - Existing editor-only terrain control bake route.
   - Existing source profile: `Assets/_SourceData/Terrain/terrain_splatmap_profiles.csv`.
   - Existing channel convention: R rock, G sand, B silt, A erosion/deposited silt.
   - Existing output folder: `Assets/_Project/BakedGeometry/Splatmaps`.
   - Existing DTO sizes are explicitly laid out in code and validated in the pipeline.

3. `ShorelineFoamGraftEditorTools` and `ShorelineFoamGraftContracts`
   - Existing profile source: `Assets/_Project/Data/shoreline_foam_profiles.csv`.
   - Existing DTO sizes: params 32 bytes, profile 32 bytes, runtime state 64 bytes, telemetry entry 64 bytes.
   - Existing telemetry capacity: 300.
   - Existing runtime-facing constants include max capacity 64, shader loop max 16, profile capacity 16.
   - Role: runtime contract and tuning profile, not the missing packed mask generator.

4. `SedimentAccumulationManager` and `SedimentAccumulation.compute`
   - Current manager publishes shader-only sediment globals and owns no capture camera, render texture, or compute pass.
   - Compute shader can inform future offline accumulation, but this task does not route it as a proven live owner.

## Missing Texture/Mask Generation Prompt

No dedicated first-party offline generator for the packed shoreline contact/wet-edge mask was found by static scan. Future owner prompt:

```xml
<OFFLINE_MASK_BAKER_PROMPT id="1821_SHORELINE_PACKED_MASK">
  <ROLE>Implement or run a first-party editor-only shoreline mask baker. Do not mutate third-party packages.</ROLE>
  <INPUTS>
    Assets/_Project/Scenes/02_HECTON_WORLD.unity
    Assets/_Project/Art/Meshes/World/MESH_H8SurfaceCoastalIsland_1428.asset
    Assets/_Project/Art/Meshes/World/MESH_H8SurfaceShoreFoamRing_1428.asset
    Assets/_Project/Art/Meshes/Generated/MESH_SurfaceFoamRingAAA_1428.asset
    Assets/_Project/Art/Meshes/Generated/MESH_SurfaceCoastlineJagged_1428.asset
    Assets/_Project/Art/TEXTURES/TX_H8SurfaceBasaltWetSediment_1428.asset
    Assets/_Project/Art/TEXTURES/TX_SurfaceBasaltWetStrata_1428.asset
    Assets/_SourceData/Terrain/terrain_splatmap_profiles.csv
    Assets/_Project/Data/shoreline_foam_profiles.csv
    Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv
  </INPUTS>
  <OUTPUTS>
    Assets/_Project/Art/TEXTURES/Generated/TX_H8_ShorelineFoamRibbonPacked_1821.png
    Assets/_Project/Art/TEXTURES/Generated/TX_H8_ShorelineWaterlineMask_1821.png
    Assets/_Project/Art/TEXTURES/Generated/TX_H8_WetDryBasaltMask_1821.png
    Assets/_Project/BakedGeometry/Splatmaps/TX_BiomeWeightMap_SHORELINE_1821.asset
  </OUTPUTS>
  <PACKING>
    FoamRibbonPacked: R long strand flow, G secondary cross-flow, B lace breakup, A reserved.
    ShorelineWaterlineMask: R contact foam, G wet edge, B sediment/salt band, A caustic receiver confidence.
    WetDryBasaltMask: R wetness, G drying gradient, B mineral breakup, A specular boost.
  </PACKING>
  <RULES>
    Editor-only work is allowed; runtime allocation or same-frame readback is not.
    Use continuous GlobalQualityWeight for resolution, octave count, blur, and optional sensory density.
    Compact remains bright, readable, and premium.
    Do not darken, fog, bloom, or UI-cover the surface route.
    Do not write JSON proof dumps for this task.
  </RULES>
</OFFLINE_MASK_BAKER_PROMPT>
```

## Material-Slot Assignment Plan For Future Unity Owner

Future Unity owner must inspect actual renderer bindings before writing:

1. Ocean route
   - Candidate material: `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`.
   - Verify `_FoamTexture`, `_CausticsTexture`, `_Normals`, `_ShorelineFoamMinDepth`, shallow subsurface values, and active renderer binding.
   - Do not mutate `Assets/Crest` shader or texture assets.

2. Primary shoreline foam route
   - Candidate material: `Assets/_Project/Art/Materials/World/MAT_H8SurfaceShoreFoam_1428.mat`.
   - Candidate object: `H8_SURFACE_SHORE_FOAM_1428`.
   - Static issue: object inactive.
   - Future proof: active renderer, material binding, camera close-up, Frame Debugger pass, profiler cost, GC zero route.

3. Foam ribbon route
   - Candidate material: `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat`.
   - Static issue: empty `_BaseMap` and `_MainTex`.
   - Required before assignment: bind `TX_H8_ShorelineFoamRibbonPacked_1821.png` or duplicate a first-party material with this texture.
   - Reject flat single-channel white texture.

4. Wet basalt route
   - Primary candidate: `MAT_H8SurfaceWetBasaltReal_1428`.
   - Secondary candidate: `MAT_SurfaceIslandWetBasalt_1428`.
   - Required future check: coastline renderer slot count, UV scale, normal intensity, wet/dry mask receiver route, and visual separation between saturated basalt, drying basalt, salt/sediment, and dry rock.

5. Terrain sediment route
   - Candidate material: `MAT_H8TerrainLit_BasaltSediment_1428`.
   - Static issue: control, splat, normal, and mask slots are empty.
   - Required before assignment: bake or bind control/layer textures. Terrain proof must include material pass, memory/VRAM, and route-angle screenshots.

## Shoreline Route Angles

Every future visual proof set must cover these angles:

1. Glancing water
   - Low camera near the water surface, looking along the coast.
   - Required read: long-swell normals, glint, foam strands, Aegir/sky light reflection, route silhouette.

2. Vertical waterline
   - Camera at contact height, looking at rock/ocean intersection.
   - Required read: wet/dry gradient, contact foam, salt/sediment line, no muddy single-color edge.

3. Close wet rock
   - Camera within inspection distance of basalt.
   - Required read: roughness/smoothness breakup, normal detail, mineral strata, sediment caught in ledges, water beads or wet sheen via material truth.

4. Wide coast
   - Player-eye route shot with ocean, coast, sky/Aegir, and return landmark.
   - Required read: premium bright surface, no darkness/noir cover-up, no flat coastline silhouette.

5. Underwater edge
   - Camera just below surface and 0-20 m entry.
   - Required read: caustic edge hints, readable photic entry, return route, water color and foam edge visible from below if physically plausible.

## Quality Consequences

Compact:

- Use lower resolution generated masks and limited active foam lanes.
- Keep bright water color, readable wet basalt, contact foam identity, and route silhouette.
- No black water, flat foam, flat wet edge, or UI-only route.

Middle:

- Increase mask resolution, mild additional foam breakup, stronger wet/dry gradient, and cheap caustic edge overlay.
- Keep terrain and material identity stable.

High:

- Increase foam ribbon strand complexity, caustic contrast, basalt detail masks, and shoreline sediment variation.
- Add richer glancing water read after profiler/Frame Debugger proof.

Ultra:

- Add visual overkill: denser foam lace breakup, richer specular response, stronger shallow caustic edge, high-detail wet basalt strata, and more precise long-swell read cards.
- Ultra adds sensory density only. It must not alter gameplay route truth, save identity, DTO layout, resource authority, or item identity.

## Rejection Gates

Reject the waterline route if any of these remain true in future proof:

- Foam objects are inactive or unbound and still claimed visible.
- `MAT_H8_SurfaceFoamRibbons_1428` is used with empty `_BaseMap` or `_MainTex`.
- Foam is flat-color, uniform, opaque, or scale-less.
- Wet basalt reads as a glossy overlay instead of material truth.
- Terrain/control material slots remain empty while terrain quality is claimed.
- Coast is grey, procedural-looking, or lacks strata/sediment/wet breakup.
- Surface route is darkened, fogged, bloomed, stormed, or graded noir to hide weak waterline art.
- Compact tier is ugly or removes route clarity.
- Third-party package assets are mutated instead of using first-party wrappers or generated outputs.
- Static path existence is used as screenshot, runtime, profiler, GC, or Frame Debugger proof.

## Future Proof Requirements

Future Unity/editor owner must produce current artifacts:

- Screenshot set covering glancing water, vertical waterline, close wet rock, wide coast, underwater edge, and Compact/Middle/High/Ultra same-camera comparison.
- Unity Profiler artifact for the same route sequence.
- GC allocation proof for exercised route. Zero-GC claims require current profiler or recorder artifact.
- Frame Debugger artifact for active ocean, foam, wet basalt/coast, caustic, terrain, sky/Aegir, and UI overlay passes if visible.
- Memory/VRAM notes for generated masks, ocean textures, caustic flipbooks, render targets, and terrain control textures.
- Batches and SetPass notes for coastline, foam ribbons, terrain, ocean, sky, and biota if visible.
- Console state and exact scene/tier/camera route.

All runtime proof remains `PENDING UNITY SLOT` in this 1821 packet.

## Unity-Slot Implementer Prompt

```xml
<UNITY_IMPLEMENTER_PROMPT id="1821_SHORELINE_WATERLINE_SLOT">
  <ROLE>Future Unity owner for shoreline/waterline offline bake application and proof.</ROLE>
  <INPUTS>
    Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC.md
    Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_BAKE_INPUTS.csv
    Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.csv
    Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md
  </INPUTS>
  <BOUNDARY>
    Use Unity only when the slot is safe.
    Do not mutate Crest, MapMagic, GPUInstancer, MeshBaker, SciFiFacility, or any third-party package asset.
    Do not edit unrelated route systems.
    Do not claim runtime, screenshot, profiler, GC, Frame Debugger, material binding, or visual acceptance without current artifact paths.
    Keep the normal surface and coast bright, premium, and readable. Darkness/noir is not a surface fix.
  </BOUNDARY>
  <STATIC_START>
    H8_SURFACE_COASTAL_ISLAND_1428 is statically present and active.
    H8_SURFACE_SHORE_FOAM_1428 and SURFACE_FOAM_RIBBON_1428_* are statically present but inactive.
    MAT_H8SurfaceShoreFoam_1428 has a first-party foam texture.
    MAT_H8_SurfaceFoamRibbons_1428 has empty texture slots and must receive a real packed bake before use.
    MAT_H8SurfaceWetBasaltReal_1428 is the primary wet basalt candidate.
  </STATIC_START>
  <ORDER>
    01 Verify active renderers and material slots.
    02 Generate or assign offline packed foam/wet/sediment/caustic masks from the 1821 spec.
    03 Bind generated masks only to first-party materials or duplicated first-party material instances.
    04 Prove glancing water, vertical waterline, close wet rock, wide coast, and underwater edge.
    05 Capture Compact, Middle, High, Ultra from matched camera routes.
    06 Produce profiler, GC, Frame Debugger, memory/VRAM, batches/SetPass, console, and screenshot artifacts.
  </ORDER>
  <REJECT>
    Flat foam.
    Flat wet edge.
    Muddy single-color waterline.
    Dark/noir cover-up.
    Empty texture slots.
    Inactive object proof.
    Third-party package mutation.
    Static-only proof upgraded to runtime proof.
  </REJECT>
  <FINAL_STATE>
    Use only one final state:
    SHORELINE WATERLINE RUNTIME PROOF PASS WITH CURRENT ARTIFACTS
    BLOCKED BY SPECIFIC UNITY EVIDENCE
    ABORTED DUE TO UNITY SLOT/BUSY BUILD GATE
  </FINAL_STATE>
</UNITY_IMPLEMENTER_PROMPT>
```

## Unsafe Or Rejected References

Rejected as final visual proof:

- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png`
- Inactive `H8_SURFACE_OCEAN_READ_1428` as proof of active ocean quality.
- Inactive `H8_SURFACE_SHORE_FOAM_1428` or inactive `SURFACE_FOAM_RIBBON_1428_*` as proof of visible foam.
- Empty material slots as proof of final waterline art.

## Static Completion

Status: STATIC BAKE SPEC COMPLETE.

The static packet is sufficient for a later Unity/offline owner to generate and bind shoreline/waterline bake assets without waiting for 1807. Runtime/editor acceptance remains `PENDING UNITY SLOT`.

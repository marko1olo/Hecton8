# 1806 Surface Route Action Manifest

Agent: 1806
Role: SURFACE_ROUTE_ACTION_MANIFEST_BUILDER
Evidence class: STATIC_DOC / STATIC_SOURCE only
Runtime/editor/profiler proof: PENDING UNITY SLOT

## Boundary

This report converts the completed 1801 and 1802 static reports into a later Unity-slot action manifest for the first surface/photic route. It does not prove runtime visuals, scene wiring, interaction behavior, material assignment, Play Mode health, frame time, GC, Frame Debugger state, or profiler cost.

No Unity/editor control was used. No scene, prefab, material, script, or asset was edited.

Owned machine-readable output:

- `Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.csv`

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `terrain.md`
- `water.md`
- `presentation.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`

Selected mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`

## Static Confirmations

Scene object/data hooks confirmed by static YAML search in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`:

- `Main Camera`
- `Player`
- `Route_Anchor`
- `Route_Frontier`
- `Node_Copper_A`
- `Scrap_A`
- `Forward_Fabricator`
- `Fabrication_Outpost`
- `Resource_FieldSources`
- `Starter_ReefField`
- `H8_SURFACE_COASTAL_ISLAND_1428`
- `H8_SURFACE_SHORE_FOAM_1428`
- `SURFACE_FOAM_RIBBON_1428_*`
- `SUB_PRESSURE_HULL`
- `SUB_PORTLIGHT_*`
- `Power_CurrentTurbine`
- `HectonCelestialEngine` component reference

Static path confirmations used by the manifest:

- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat`
- `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceWetBasaltReal_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceIslandWetBasalt_1428.mat`
- `Assets/_Project/Art/TEXTURES/TX_H8SurfaceBasaltWetSediment_1428.asset`
- `Assets/_Project/Art/TEXTURES/TX_SurfaceBasaltWetStrata_1428.asset`
- `Assets/_Project/Art/Meshes/World/MESH_H8SurfaceCoastalIsland_1428.asset`
- `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceCoastlineJagged_1428.asset`
- `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceBasaltStack_1428.asset`
- `Assets/_Project/Data/H8_TerrainLayer_AbyssBasalt_1428.terrainlayer`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`
- `Assets/_Project/Prefabs/Nature/Flora/Baked`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat`
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`
- `Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat`
- `Assets/SciFiFacility`
- `Assets/_Project/Scripts/World/OfflineWreckageBaker`
- `Assets/_Project/Prefabs/TECH_DEBRIS.prefab`
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`

Static counts:

- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`: 49 prefab files.
- `Assets/_Project/Prefabs/Nature/Flora/Baked`: 89 prefab files by recursive static count.

## Active, Candidate, Rejected

Active scene references by static YAML evidence:

- Player/camera and route chain: `Main Camera`, `Player`, `Route_Anchor`, `Route_Frontier`, `Node_Copper_A`, `Scrap_A`, `Forward_Fabricator`, `Fabrication_Outpost`, `Resource_FieldSources`.
- Surface/coast/waterline: `H8_SURFACE_COASTAL_ISLAND_1428`, `H8_SURFACE_SHORE_FOAM_1428`, `SURFACE_FOAM_RIBBON_1428_*`.
- Photic/industrial/celestial: `Starter_ReefField`, `SUB_PRESSURE_HULL`, `SUB_PORTLIGHT_*`, `Power_CurrentTurbine`, `HectonCelestialEngine`.

Candidate-only references:

- `H8_SURFACE_OCEAN_READ_1428`: exists in scene, `m_IsActive: 0`.
- `H8_AEGIR_SKY_BACKDROP_1428`: exists in scene, GameObject active, MeshRenderer `m_Enabled: 0`.
- `SURFACE_GAS_GIANT_1428`: exists in scene, `m_IsActive: 0`.
- `SURFACE_SKY_NOIR_BACKDROP_1428`, `SURFACE_SKY_DOME_NOIR_1428`, `Lane_DarkRoute`, `DarkRoute_HazardProbe`: risky dark/noir names. They are not authority to darken the normal surface route.

Rejected final references:

- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png`
- Primitive-heavy construction/debris candidates are layout sources only until replaced, baked, dressed, and captured.
- Legacy/simple water fallback paths are not premium surface targets.

## Manifest Route Beats

The CSV contains one row per route beat:

1. `spawn`
2. `first_surface_look`
3. `waterline`
4. `aegir_horizon`
5. `coastline`
6. `0_30m_shallows`
7. `30_100m_route`
8. `industrial_trace`
9. `resource_node`
10. `scrap`
11. `forward_fabricator`
12. `return_path`

Each row separates:

- player moment;
- active scene object or data;
- candidate asset/material;
- static state;
- visual gap;
- gameplay gap;
- optimization risk;
- exact Unity-slot action;
- future screenshot proof label;
- future runtime metric proof label;
- Compact/Middle/High/Ultra consequence;
- rejection gate;
- source report references.

## Unity-Slot Actions

Minimum concrete actions for the later Unity implementer:

1. Capture spawn from player eye level and verify the first route cue before any UI or art masking.
2. Verify the active sky/Aegir renderer/material path; do not use disabled/inactive candidates as proof.
3. Inspect `Ocean_Crest.prefab` material/optional refs; compare existing Crest path with `MAT_H8_SurfaceCrestOcean_1428.mat` candidate.
4. Texture or bake masks for shoreline foam ribbons; reject flat-color foam.
5. Verify or assign wet basalt coastline material route and add finalized rock dressing only with HLOD/instancing discipline.
6. Place or verify baked coral/kelp clusters around `Starter_ReefField`; placement rules are intent, not proof.
7. Capture 30-100 m route visibility with instruments and return cue readable.
8. Capture/dress industrial trace objects so dock/sub/turbine history reads from route distance.
9. Verify `Node_Copper_A` as a physical resource target, not an abstract colored dot.
10. Verify `Scrap_A` as believable salvage, not a primitive or loot sparkle.
11. Verify `Forward_Fabricator`/`Fabrication_Outpost` as physical machines, not menu-only UI.
12. Capture return path from resource/scrap/fabricator back to `Route_Anchor` and coast.

All runtime metrics remain pending until a Unity slot runs current captures and profiler/Frame Debugger checks.

## Required Proof Labels

Future screenshot labels:

- `1806_SHOT_01_spawn_first_read`
- `1806_SHOT_02_first_surface_aegir`
- `1806_SHOT_03_waterline_foam_wet_edge`
- `1806_SHOT_04_horizon_aegir_coast`
- `1806_SHOT_05_coast_wet_basalt_foam`
- `1806_SHOT_06_0_30m_photic_scatter`
- `1806_SHOT_07_30_100m_route_read`
- `1806_SHOT_08_industrial_trace_route_cue`
- `1806_SHOT_09_resource_node_context`
- `1806_SHOT_10_scrap_salvage_read`
- `1806_SHOT_11_forward_fabricator_anchor`
- `1806_SHOT_12_return_path_sequence`

Future runtime metric labels:

- `1806_METRIC_01_spawn_frame_gc_ui`
- `1806_METRIC_02_sky_framedebugger_profiler`
- `1806_METRIC_03_water_frame_debugger_profiler_gc`
- `1806_METRIC_04_sky_material_pass_cost`
- `1806_METRIC_05_coast_batches_setpass_vram`
- `1806_METRIC_06_scatter_instancing_gc_profiler`
- `1806_METRIC_07_optics_rendergraph_profiler_gc`
- `1806_METRIC_08_industrial_batches_setpass_profiler`
- `1806_METRIC_09_resource_interaction_gc_profiler`
- `1806_METRIC_10_scrap_interaction_batches_gc`
- `1806_METRIC_11_fabricator_interaction_gc_profiler`
- `1806_METRIC_12_return_hud_sonar_gc_profiler`

## Do Not Do

- Do not darken the normal surface route to hide weak water, coast, terrain, sky, or Aegir.
- Do not blindly enable Crest realtime depth/foam cameras as an easy fix.
- Do not mutate Crest, MapMagic, GPUInstancer, MeshBaker, or SciFiFacility package assets.
- Do not claim path existence as material binding, import health, runtime behavior, or final quality.
- Do not treat `H8_SURFACE_OCEAN_READ_1428`, `H8_AEGIR_SKY_BACKDROP_1428`, or `SURFACE_GAS_GIANT_1428` as active proof.
- Do not use `WorldRuntime/ProceduralPlaceholders` or the bad bubble atlas as final production references.
- Do not use silt, fog, bloom, noir post, or HUD overlays to cover missing art.
- Do not make Compact ugly. `GlobalQualityWeight = 0.0` still needs attractive composition, readable ocean color, clear route cues, and material identity.
- Do not introduce runtime hero generation for surface/coast/biota/industrial visuals. Use baked/offline assets or verified third-party configuration.

## Unity Implementer Prompt

```xml
<UNITY_IMPLEMENTER_PROMPT id="1806">
Role: SURFACE_ROUTE_UNITY_VISUAL_IMPLEMENTER
Input: Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.md and .csv
Boundary:
- Use Unity only when a slot is safe.
- Verify active renderer/material bindings before editing.
- No runtime/profiler/Frame Debugger claim without fresh artifact path.
- Preserve bright, beautiful, readable surface and 0-100 m photic route.
- Do not depend on unfinished 1803-1805.

Start from active scene references:
Main Camera; Player; Route_Anchor; Route_Frontier; Node_Copper_A; Scrap_A; Forward_Fabricator; Fabrication_Outpost; Resource_FieldSources; Starter_ReefField; H8_SURFACE_COASTAL_ISLAND_1428; H8_SURFACE_SHORE_FOAM_1428; SURFACE_FOAM_RIBBON_1428_*; SUB_PRESSURE_HULL; SUB_PORTLIGHT_*; Power_CurrentTurbine; HectonCelestialEngine.

Use candidates only after inspection:
Ocean_Crest.prefab; MAT_H8_SurfaceCrestOcean_1428.mat; MAT_H8_SurfaceFoamRibbons_1428.mat; H8_ShorelineFoamRibbon_1428.shader; MAT_H8SurfaceWetBasaltReal_1428.mat; MAT_SurfaceIslandWetBasalt_1428.mat; finalized rock prefabs; baked flora prefabs; MAT_family_coral_branching.mat; MAT_family_kelp_tall.mat; TX_H8AegirGasGiantBakedDisc_1428.png; MAT_AegirGasGiant_Impostor_1428.mat; MAT_SurfaceCloudPanorama_1428.mat; SciFiFacility as source kit; OfflineWreckageBaker.

Reject candidate-only proof:
H8_SURFACE_OCEAN_READ_1428 inactive; H8_AEGIR_SKY_BACKDROP_1428 renderer disabled; SURFACE_GAS_GIANT_1428 inactive; WorldRuntime/ProceduralPlaceholders rejected final reference.

Produce all 12 screenshots and all matching runtime metric artifacts listed in 1806.
Final state must be either SURFACE ROUTE PASS WITH CURRENT UNITY ARTIFACTS or BLOCKED BY SPECIFIC UNITY EVIDENCE.
</UNITY_IMPLEMENTER_PROMPT>
```

## Visual QA Prompt

```xml
<VISUAL_QA_PROMPT id="1806">
Role: SURFACE_ROUTE_VISUAL_QA
Input: 12 screenshots and metric artifacts produced from the 1806 manifest.
Check:
- Surface and 0-100 m route are bright, colorful, readable, and beautiful.
- Ocean surface has material response, wave normal read, specular, waterline foam, and believable color.
- Aegir/sky/moons/clouds are textured, soft, scaled, and not muddy/noisy placeholders.
- Coastline shows wet basalt, strata, foam contact, sediment, and route silhouette.
- 0-30 m shallows show authored coral/kelp/biota density without random scatter or aquarium blandness.
- 30-100 m route remains readable with instruments and return cues.
- Industrial traces read as machinery/history, not primitive decoration.
- Resource, scrap, fabricator, and return path create player decisions.
- Compact remains attractive and route-readable; High/Ultra add sensory richness only.

Reject:
- dark-cover surface;
- flat water;
- grey procedural coast;
- UI-only navigation;
- path-existence quality claims;
- primitive debris as hero trace;
- placeholder assets;
- unmeasured expensive render/VFX paths;
- any proof label upgraded beyond its artifact.
</VISUAL_QA_PROMPT>
```

## Scaling Consequences

Compact:

- Keep bright ocean color, strong route silhouettes, wet material masks, limited but authored scatter, minimal HUD, conservative foam and VFX.
- No bloom cover, no full volumetrics, no hidden simulation.

Middle:

- Add denser route dressing, shoreline foam blending, cheap caustics, richer HUD/sonar support, more material breakup.

High:

- Add stronger water reflection/glint, richer wet basalt detail, denser flora with LOD, better sky/cloud transition, measured optics/VFX.

Ultra:

- Add visual overkill: richer sky layering, near-field water and foam breakup, dense photic biota, detailed machinery wear, stronger instrument/lens response.
- Ultra adds sensory density only. It must not add route truth, resource truth, save identity, or gameplay authority.

## Final State

Manifest status: STATIC MANIFEST COMPLETE.

Blocked runtime/editor claims:

- No Play Mode proof.
- No Unity scene/material binding proof.
- No Frame Debugger proof.
- No profiler/GC proof.
- No final visual quality proof.

This packet is sufficient for a later Unity visual implementer to inspect/change/capture the first surface route without rereading 1801/1802 in full.

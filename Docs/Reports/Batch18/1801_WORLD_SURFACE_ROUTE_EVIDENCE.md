# 1801 WORLD SURFACE ROUTE EVIDENCE

Final state: STATIC VERIFIED ACTION PACKET.

Runtime/editor proof: PENDING UNITY SLOT. No Unity control, scene editing, prefab editing, build, profiler run, Frame Debugger run, or PlayMode capture was performed by agent 1801.

## Evidence Boundary

This packet is a static evidence map for the surface/photic-shallow first route. It proves local file existence, source references, scene YAML state, candidate asset paths, stale leads, and required future proof. It does not prove runtime visual quality, runtime interaction, profiler cost, GC behavior, camera composition, or player-route readability.

Authority read: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `world.md`, `terrain.md`, `water.md`, `rendering.md`, `lighting.md`, `presentation.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`.

Mandates selected: `QA_Evidence_Text_Filter_Audit`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows`, `REND_Terrain_VirtualTexturing`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration`.

## Static Screenshot Evidence

Current screenshots inspected:

| File | Size | Timestamp | Static read |
|---|---:|---|---|
| `Assets/Screenshots/h8_water_ui_baseline_before_08.png` | 1008x567, 537369 bytes | 2026-06-04 03:36:54 | Readable sky and Aegir. Water is broad and flat. Route content and waterline richness are weak. |
| `Assets/Screenshots/h8_scene_water_ui_baseline_before_08.png` | 1008x591, 354808 bytes | 2026-06-04 03:36:56 | Scene-view evidence shows same broad water/coast problem, with editor overlays. It is not player-capture acceptance proof. |

Archive search found older surface/water screenshot hits under `Docs/_Archive/WorkspaceHygiene_1331/Assets/`, but those are stale compared to the current `before_08` captures and were not used to override the current state.

## Verified Static Targets

Hard source paths verified:

| Target | Evidence label | Static result |
|---|---|---|
| `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | STATIC YAML EVIDENCE | Exists and contains terrain, MapMagic bridge, player, camera, celestial engine, route markers, resource/fabrication sockets, shore/coast objects, foam ribbons, dock/sub/turbine traces, passive biolum names, and fauna shadow names. |
| `Assets/_Project/Prefabs/Ocean_Crest.prefab` | STATIC PREFAB EVIDENCE | Exists. Contains `Crest::Crest.OceanRenderer`, `_createSeaFloorDepthData: 0`, `_createFoamSim: 0`, and `HectonCrestOceanDepthCacheBootstrap`. Scene references the prefab instance. |
| `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/Hecton8_Surface.prefab` | STATIC PREFAB EVIDENCE | Exists. The reported `m_Material: {fileID: 0}` hit is a `SphereCollider` physic-material slot, not a MeshRenderer material slot. MeshRenderer material list is bound. |
| `Assets/_Project/Art/TEXTURES/` | STATIC ASSET PATH EVIDENCE | Contains Aegir, water, basalt, ocean, foam, and photic-shallow candidate textures. |
| `Assets/_Project/Data/Biomes/MatrixProfiles/` | STATIC ASSET PATH EVIDENCE | Contains matrix profile assets, including starter-surface profile evidence such as `Archipelago Needles` and `Sea-Stack Forest`. |
| `Assets/_Project/Data/World/ZonePlans/` | STATIC ASSET PATH EVIDENCE | Contains route-relevant zone plans including `Resources_Starter`, `Fabrication_Early`, and `Trial_Early`. |

Relevant world/editor scripts verified by path search include `Assets/_Project/Editor/OceanRenderLayoutValidator.cs`, `SinglePassOceanTunerWindow.cs`, `SinglePassOceanRendererFeatureInstaller.cs`, `ShorelineFoamGraftEditorTools.cs`, `HectonWaterGrid.cs`, `HectonSurfacePainter.cs`, `HectonSkyTools.cs`, `HectonSkyAtlasGenerator.cs`, `HectonMeshGenerator.cs`, `GeminiWorldBuilder.cs`, `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`, `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs`, `HectonWorldShellVisualDriver1428.cs`, `GPUScatterDirector.cs`, `GlobalWorldSampler.cs`, `WorldContentSocket.cs`, and `Assets/_Project/Scripts/Rendering/OceanSinglePass/*`.

## Route State Classification

| Component | Classification | Static evidence | Blocker / risk | Required proof |
|---|---|---|---|---|
| Ocean/Crest | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `Ocean_Crest.prefab` is in the scene. Crest depth and foam generation are disabled in source. | Current screenshot shows broad flat water. Re-enabling Crest realtime cameras is not a safe default fix. | Player-capture waterline screenshot, above-water ocean screenshot, Frame Debugger material/pass check, profiler cost. |
| Single-pass/custom water candidates | CANDIDATE / STALE | `H8_SURFACE_OCEAN_READ_1428` exists in scene but is inactive. `MAT_H8SurfaceOceanRead_1428.mat`, `MESH_H8SurfaceOceanRead_1428.asset`, `TX_H8SurfaceOceanLongSwell_1428.asset`, `TX_H8_SurfaceWaterNormals_1428.asset`, and `TX_SurfaceOceanInterference_1428.asset` exist. | Candidate assets do not prove active runtime binding. | Unity slot must confirm active renderer/material path and capture near-field/far-field water quality. |
| Terrain / MapMagic | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `MapMagicRuntimeBridge`, `MapMagicObject`, Terrain, and TerrainCollider exist in `02_HECTON_WORLD.unity`; TerrainData is referenced. | Screenshot terrain/coast reads grey and under-rich for surface lock. YAML cannot prove splat/detail quality. | Scene view and player captures at spawn, coast, and 0-30 m entry; material binding check. |
| Sky / Aegir / celestial | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `HectonCelestialEngine` exists with sun/Aegir/player/sky material references. Current screenshot shows readable Aegir. Candidate Aegir textures/materials exist. | `H8_AEGIR_SKY_BACKDROP_1428` renderer is disabled and `SURFACE_GAS_GIANT_1428` is inactive, so those objects must not be treated as proven active path. | Above-water player capture with Aegir, skybox/material check, disabled-candidate cleanup decision. |
| Coast / rocks / shore foam | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `H8_SURFACE_COASTAL_ISLAND_1428`, `H8_SURFACE_SHORE_FOAM_1428`, many `SURFACE_FOAM_RIBBON_1428_*` objects, wet-basalt and shore-foam materials/meshes exist. | Current screenshots show weak shoreline richness and flat waterline. | Coastline close-up capture, waterline capture, material close inspection, Frame Debugger material confirmation. |
| Photic shallow scatter / flora | CANDIDATE; NEEDS UNITY VISUAL PROOF | `Starter_ReefField` and biolum/passive-life names exist. Shallow coral/kelp/generated flora assets and materials exist. | Static names and asset folders do not prove density, placement, silhouettes, or route readability. | Underwater 0-30 m capture, scatter density capture, route-frame proof from player entry. |
| Fauna | CANDIDATE; NEEDS UNITY VISUAL PROOF | `H8_FAUNA_SHADOW_BODY_*` and `H8_FAUNA_SHADOW_TAIL_*` names exist. | Static object names do not prove live behavior, movement, scale, or gameplay pressure. | Player capture and, if active, telemetry/profiler evidence for fauna update cost. |
| Industrial remnants | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | Dock deck/rails/stanchions, `SUB_PRESSURE_HULL`, `SUB_PORTLIGHT_*`, `Power_CurrentTurbine`, deck seams, scan accents exist in scene YAML. | Current screenshot does not clearly sell machinery/history at first-exit scale. | Surface and waterline captures with dock/sub/turbine cue readability. |
| Route markers / content sockets | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `Route_Anchor`, `Route_Frontier`, `Node_Copper_A`, `Scrap_A`, `Forward_Fabricator`, `Fabrication_Outpost`, `Resource_FieldSources`, lane roots, and `WorldContentSocket` references exist. | Static sockets do not prove player can read or interact with the route without UI. | Player route capture from spawn to anchor/resource/fabricator; interaction proof if Unity slot permits. |
| Player / camera entry | VERIFIED EXISTS; NEEDS UNITY VISUAL PROOF | `Main Camera` and `Player` tagged object exist. Player object has world-shell controller references. | Static start position does not prove camera exposure, input, swim route, or water transition. | Player-capture from initial spawn and first swim. |
| Starter biome / zone planning | VERIFIED EXISTS AS DATA; NEEDS UNITY VISUAL PROOF | Starter resource/fabrication/trial zone plans exist. Matrix profiles include starter-surface/photic route identities. | Data plans do not prove scene placement or runtime visual density. | Runtime capture and any relevant authoring-validator output after Unity slot. |

## 0-100 m Bright Surface Lock

Static evidence supports intent, not acceptance. The scene has bright-surface infrastructure, Aegir/celestial assets, surface/coast objects, route sockets, starter zone data, shallow flora assets, and industrial-remnant names. Current screenshot evidence still fails the visual floor on broad water flatness, shoreline material richness, and route-content density. Therefore the 0-100 m route remains PENDING UNITY/PLAYER-CAPTURE VERIFICATION.

Surface darkness is not an acceptable cover. Noir/depth systems are allowed for depth, caves, interiors, storms, and temporary eclipse windows. The first surface/photic exit must remain bright, beautiful, readable, and detailed.

## Stale Or Risky Assumptions

- The `Hecton8_Surface.prefab` line-103 report is stale as a visual blocker. It is a `SphereCollider` physic-material null, not a MeshRenderer material null.
- `H8_SURFACE_OCEAN_READ_1428` is inactive in the scene. Treat it as a candidate, not active route proof.
- `H8_AEGIR_SKY_BACKDROP_1428` has a disabled renderer. `SURFACE_GAS_GIANT_1428` is inactive. The active visible Aegir route appears to rely on other celestial pathing and must be verified in Unity.
- `SURFACE_SKY_NOIR_BACKDROP_1428`, `SURFACE_SKY_DOME_NOIR_1428`, `Lane_DarkRoute`, `DarkRoute_HazardProbe`, and many `NOIR_*` names exist. These must not steer surface art toward black fog, darkness, or hidden weakness.
- Any `WorldRuntime/ProceduralPlaceholders` material use on the first-exit route is suspect until replaced or explicitly proven acceptable in screenshot.

## Exact Route Blockers

- Broad flat ocean read in current screenshot.
- Weak shoreline wet-material richness and weak contact foam read.
- Sparse first-route composition in current screenshot: not enough immediate route cues, industrial remnants, shallow biota, or readable return path.
- No current underwater 0-30 m player capture proving photic-shallow quality.
- No current proof that starter resource, scrap, anchor, and fabricator sockets are visible and interactable as a route chain.
- No current proof that fauna/flora are alive, scaled, and compositionally useful rather than named static clutter.
- No current profiler, GC, Frame Debugger, or material-pass evidence.

## Safe No-Unity Fixes

| Fix | Output | Scope |
|---|---|---|
| Keep this evidence packet as the authoritative static handoff. | `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md` | Completed by this agent. |
| If another static pass is assigned, create a route-binding manifest from existing scene objects only. | `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_ACTION_MANIFEST.md` | Future static work only. It must not invent placements or prefab bindings. |
| If documentation is updated, preserve the surface lock and stale-object warnings. | Existing route docs or a short addendum under `Docs/Reports/Batch18/` | No broad docs churn. |

No scene, prefab, material, or script edits are safe in this pass because the task forbids taking over the live editor and this work did not obtain runtime capture proof.

## Unity-Slot Required Fixes

| Fix | Proof required after implementation |
|---|---|
| Bind or activate a premium waterline route using existing single-pass/custom water assets or an equivalent visual fake. Do not blindly re-enable Crest realtime depth/foam cameras. | Player above-water screenshot, waterline screenshot, Frame Debugger pass/material proof, profiler cost, no visible flat-ocean failure. |
| Upgrade coastline/wet-basalt/shore-foam read using existing wet basalt, shore foam, coastline mesh, and foam-ribbon assets. | Close coastline screenshot, oblique waterline screenshot, material binding evidence, compact-tier capture. |
| Verify or place photic-shallow coral/kelp/biolum density around `Starter_ReefField` and first route. | 0-30 m underwater screenshot, route-facing screenshot, scatter density proof, profiler/GC proof if runtime systems are modified. |
| Verify Aegir active path and clean stale inactive sky objects. | Above-water Aegir/sky screenshot, active material/renderer evidence, statement of which inactive candidates remain unused. |
| Verify industrial remnants as route/evidence cues. | Capture showing dock/sub/turbine silhouettes readable from player route, with no UI-only dependency. |
| Verify starter route chain from player spawn through anchor/resource/scrap/fabricator. | Player-capture route sequence and interaction proof where tools are available. |
| Validate performance after any visual work. | Profiler sample, Frame Debugger snapshot, and no GC allocations from new hot-path systems. |

## Scalability Consequences

| Proposal | Compact | Middle | High | Ultra |
|---|---|---|---|---|
| Surface ocean readability | Single authored long-swell normal/interference material, static foam ribbons, strong silhouettes. Must still look beautiful. | Add richer shoreline foam blending and near-field color variation. | Add reflection/caustic hints and better glint response. | More near-field water detail and cinematic glints without changing gameplay truth. |
| Coast / wet basalt | Strong wet/dry masks, readable basalt silhouettes, limited decals. | Add contact wetness and sediment variation. | Add microdetail and richer foam contact. | Close-camera overkill material variation and secondary detail. |
| Aegir / sky | Baked high-quality Aegir disc/cloud panorama and clean bright sky. | Extra cloud depth and atmospheric haze. | Softer celestial lighting transitions and richer bands. | Observer-relative celestial richness and premium atmospheric layering. |
| Photic shallows | Fewer authored clusters with strong color/silhouette and clear swimming path. | Moderate coral/kelp/biolum density. | Larger density and motion variety with LOD. | Dense alive shallows with visual overkill, still route-readable. |
| Industrial traces | Bold dock/sub/turbine silhouettes and amber/cyan scan accents. | More material breakup and readable route lighting. | More decals, wear, and secondary machinery. | Rich close-up machinery history without new gameplay authority. |

Compact is not an ugly mode. It is the cheapest beautiful approximation. GlobalQualityWeight may scale fidelity, cadence, capacity, and telemetry, but must not change gameplay truth, DTO layout, save identity, or authority route.

## TASTE Cross-Check

- Pressure: current screenshots do not yet sell pressure through waterline, coast, or shallow route density. Needs capture and polish.
- Machinery: industrial object names exist, but first-read machinery evidence is not visually proven.
- Route cue: anchors/sockets exist, but route chain readability is unproven without player capture.
- Evidence cue: dock/sub/turbine and resource/fabrication sockets exist; visual evidence cue strength remains unproven.
- Beauty under stress: current sky/Aegir is a positive sign; water and shoreline are below the required surface floor.

## Candidate Screenshot List

Capture these when Unity is free:

1. Player spawn / first surface look with HUD disabled or minimal.
2. Waterline view toward Aegir and coastline.
3. Underwater 0-30 m view into `Starter_ReefField`.
4. Shallow route from `Route_Anchor` toward `Node_Copper_A` / `Scrap_A`.
5. Aegir/sky/horizon close composition.
6. Coastline close-up: wet basalt, foam contact, water material.
7. Industrial trace: dock/sub/turbine readable as route/evidence cue.
8. Resource/fabricator chain: resource pocket to `Forward_Fabricator`.
9. Compact/low-quality equivalent capture to prove minimum tier remains beautiful.

## Rejection Gates

- Any surface route that hides weak art with darkness fails.
- Any claim of runtime visual quality without current screenshot proof fails.
- Any claim of performance without profiler/Frame Debugger/GC proof fails.
- Any new hot-path visual system over budget without proof fails.
- Any static route packet that invents placements, material bindings, counts, hashes, or line numbers fails.
- Any solution that is beautiful but empty, fast but flat, or complex but slow fails.

## Next-Agent Prompt

```xml
<NEXT_AGENT_PROMPT>
Role: WORLD_SURFACE_UNITY_VISUAL_PLACEMENT_VERIFIER
Scope: Use a Unity slot only when free. Start from Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md.
Rules:
- Do not re-enable Crest realtime depth/foam cameras as an easy fix.
- Do not treat inactive objects H8_SURFACE_OCEAN_READ_1428 or SURFACE_GAS_GIANT_1428 as active proof until inspected in Unity.
- Verify active renderer/material bindings before changing them.
- Preserve bright/beautiful/readable surface and photic-shallow lock.
- Use existing scene objects/assets first: Ocean_Crest, HectonCelestialEngine, H8_SURFACE_COASTAL_ISLAND_1428, H8_SURFACE_SHORE_FOAM_1428, SURFACE_FOAM_RIBBON_1428_*, Starter_ReefField, Route_Anchor, Node_Copper_A, Scrap_A, Forward_Fabricator, dock/sub/turbine traces.
- Capture the nine screenshot angles listed in the report.
- For every visual change, report Compact/Middle/High/Ultra result and required profiler/Frame Debugger proof.
Expected final state: either RUNTIME VERIFIED SURFACE ROUTE PASS or BLOCKED BY SPECIFIC UNITY EVIDENCE.
</NEXT_AGENT_PROMPT>
```


# Surface Authoritative Route Recovery Matrix - 2026-06-05

Status: `CONTROLLER_MATRIX / STATIC_ONLY / VISUAL_REJECTED`.
Evidence class: `USER_DIALOGUE_REVIEW + STATIC_DOC + STATIC_SOURCE + STATIC_LOG + DIRECT_SCREENSHOT_REJECTION`.

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, project-setting mutation, or raw YAML edit was performed by this controller pass.

## Current Front

- Process gate: red. Unity and Unity support processes were active, and CPU samples were approximately 97-99 percent during refresh. Unity mutation/readback/build/import/screenshot work is blocked.
- Last visual evidence: `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png` and `.txt`, overwritten at the latest observed `00:39` pass.
- Last visual verdict: rejected. The image shows slab water, black detached shore/terrain, rectangular material patch, acid green haze/terrain, weak Aegir integration, and no player/HUD/tool proof.
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

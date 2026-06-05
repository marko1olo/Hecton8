# 2007 Ocean Shoreline Render Proof Packet

ID: 2007
Role: BATCH20 / OCEAN_SHORELINE_WATERLINE_RENDER_PROOF_PACKET
Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/build/runtime/profiler: NOT RUN
Final state: SOURCE ROUTE PACKET COMPLETE / UNITY VISUAL PROOF PENDING OWNER

## Boundary

This packet defines proof expectations for ocean surface, shoreline foam, waterline, photic shallows, and medium-depth readability. It does not prove active Unity visual quality, scene binding, shader import, RenderGraph order, Frame Debugger state, profiler cost, GC, VRAM, or gameplay acceptance.

No `Assets/**` file was edited. Crest vendor files remain unchanged. No Unity Editor, Unity MCP, import, build, dotnet build, shader import, profiler, Frame Debugger, scene edit, material edit, prefab edit, or screenshot capture was run by this worker.

First-20-minutes route blocker removed: the Unity visual owner now has exact ocean/shoreline proof requirements for the first semi-open surface/photic exit instead of vague "check water" language.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `atmosphere.md`
- `lighting.md`
- `performance.md`
- `Docs/ARCHITECTURE/SHINOBU_262_SINGLE_PASS_OCEAN_RENDERGRAPH.md`
- `Docs/ARCHITECTURE/SHINOBU_265_WATER_OPTICS_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/SHINOBU_266_JACOBIAN_FOAM_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/SHINOBU_277_CREST_SHORELINE_FOAM_GRAFT.md`
- `Docs/ARCHITECTURE/CREST_VERSION_QUARANTINE_SHINOBU_260.md`
- `Docs/ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md`
- `Docs/Reports/Batch19/1907_TERRAIN_COASTLINE_MATERIAL_PACKAGE.md`
- `Docs/Reports/Batch20/UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

`Docs/Actual Domains of Project.txt` was checked through the required path and produced no domain text. Narrow domain used: ocean surface, shoreline waterline, foam, photic-shallow render proof.

## Source Route Diagram

```text
HomeostasisBrain.GlobalQualityWeight
  -> ShinobuOceanSurfaceAtmosphereRuntime
     -> WaveParametersDTO / WeatherStateDTO / OceanSurfaceLodDTO
     -> shader globals: _H8OceanSurfaceTime, _H8OceanGlobalQualityWeight,
        _H8OceanWaveParameters, _H8OceanRadialGridLod, _GlobalFlowVector

SystemDispatcher.VisualSync
  -> OceanSinglePassRuntime.VisualSyncTick
     -> GlobalDataVault rows 71895..71902
     -> OceanVisualOverridesDTO[1] constant buffer
     -> PropwashEventDTO upload buffer
     -> ShorelineFoamGraftRuntime.VisualSyncTick
        -> GlobalDataVault rows 71940..71946
        -> ShorelineFoamParamsDTO[0..63]
        -> double-buffered GraphicsBuffer.LockBufferForWrite

URP RenderGraph
  -> HectonSinglePassOceanFeature.RecordRenderGraph
     -> imports ocean constant buffer
     -> imports shoreline foam GraphicsBuffer when active
     -> raster pass "Hecton Ocean Single-Camera Depth"
     -> shader "Hidden/Hecton8/OceanDepthFoam"
     -> output _H8OceanDepthFoamMask
     -> optional compute pass "Hecton Ocean Wake Compute"
     -> output _H8OceanWakeDisplacement

Crest boundary
  -> Assets/Crest/Crest/** remains vendor-owned
  -> Ocean_Crest.prefab has _createSeaFloorDepthData: 0 and _createFoamSim: 0 by static text
  -> Crest realtime depth cache, foam sim, and planar reflection routes require Unity proof that they stay disabled
```

## Static Source Findings

| Fact | Static evidence | Evidence label | Runtime status |
|---|---|---|---|
| Single-pass ocean route records into URP RenderGraph. | `HectonSinglePassOceanFeature.cs:98`, `:195`, `:266`. | STATIC_SOURCE | PENDING UNITY/FRAME DEBUGGER |
| Shoreline foam buffer is imported into the depth-mask pass. | `HectonSinglePassOceanFeature.cs:125-131`, `:202-208`, `:222-225`. | STATIC_SOURCE | PENDING UNITY/FRAME DEBUGGER |
| Depth/foam pass reads primary camera depth, not an auxiliary shoreline camera. | `Hidden_Hecton_OceanDepthFoam.shader:76-115`. | STATIC_SOURCE | PENDING UNITY/FRAME DEBUGGER |
| Shoreline foam is a screen-space visual fake. | Shader computes world position from screen UV/depth and compares against localized sea level. | STATIC_SOURCE | PENDING VISUAL PROOF |
| No `Camera.Render`, `ReadPixels`, `SetData`, `Graphics.Blit`, `CommandBuffer.Blit`, or `AddUnsafePass` hit was found in the targeted ocean/shoreline render route. | Targeted `rg` over OceanSinglePass, Atmosphere, ocean shaders, and validators. Atmosphere has opt-in `AsyncGPUReadback`, not shoreline capture. | STATIC_SOURCE | PENDING UNITY PROFILER |
| ShorelineFoamParamsDTO is a fixed 32-byte DTO with two `float4` lanes. | `ShorelineFoamGraftContracts.cs:43-47`; layout validator in `ShorelineFoamGraftEditorTools.cs`. | STATIC_SOURCE | PENDING UNITY IMPORT |
| `GlobalQualityWeight` scales foam active count, shader loop limit, decay, intensity/falloff, and normal perturbation. | `ShorelineFoamGraftContracts.cs:120-190`, `:620`, `:944-945`. | STATIC_SOURCE | PENDING CAPTURE/PROFILER |
| GPU uploads use double `GraphicsBuffer` lanes and `LockBufferForWrite`. | `OceanSinglePassRuntime.cs:646-743`; `ShorelineFoamGraftContracts.cs:846-878`. | STATIC_SOURCE | PENDING GC/PROFILER |
| Atmosphere/wave surface owns optional async wave-height readback. | `ShinobuOceanSurfaceAtmosphereRuntime.cs:110-112`, `:1494`. | STATIC_SOURCE | PENDING READBACK CADENCE PROOF |
| Crest vendor texture candidates exist. | `WaveNormals.png` 1156066 bytes, `foam.png` 751949 bytes, `Foam2.png` 98476 bytes, `Caustics_tex_color.png` 408773 bytes. | STATIC_SOURCE | PENDING UNITY IMPORT/BINDING |
| Crest foam/depth generation is disabled on the project ocean prefab by static YAML. | `Ocean_Crest.prefab:480` `_createSeaFloorDepthData: 0`; `:482` `_createFoamSim: 0`. | STATIC_SOURCE | PENDING UNITY INSPECTOR/FRAME DEBUGGER |
| Crest donor boundary is active and quarantined. | `CREST_VERSION_QUARANTINE_SHINOBU_260.md`; bridge-only ownership. | STATIC_DOC | PENDING UNITY IMPORT |

## Critique Target

Reference target: `Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png`

This image is `CRITIQUE_REFERENCE_ONLY`. It is not acceptance, before-proof, after-proof, Game View proof, Scene View proof, profiler proof, or player proof.

Visible problems to recreate and test:

- shoreline/waterline foam is not readable from the camera;
- ocean horizon reads as a hard flat band;
- coast rock material reads grey and weak at distance, with little wet/dry waterline breakup;
- shallow-water transparency/caustic read is not proven;
- Aegir scale reads, but texture/cloud softness must be checked by long shot plus crop;
- Unity/MCP window and editor UI make this unsuitable as an acceptance artifact.

## Visual Floor

Surface, coastline, ocean skin, sky, Aegir, moons, photic shallows, and medium-depth hero routes must stay bright, readable, detailed, and beautiful. Darkness/noir belongs to depth, caves, interiors, storms, and temporary eclipse windows.

Required water traits:

- real ocean color with wave normals and specular response;
- visible waterline at coast and player water entry;
- foam/contact breakup that is not a flat white ribbon;
- wet basalt/terrain material identity at the boundary;
- 0-5 m photic readability with visible floor, caustic hint or premium substitute, and surface underside;
- 20-50 m medium-depth readability with route structure, not abyss darkness;
- Aegir/horizon color and haze that do not fight ocean and sky composition.

## Source And Material Gaps From 1907

The 1907 coastline package remains relevant and unresolved:

- `H8_SURFACE_SHORE_FOAM_1428` and sampled `SURFACE_FOAM_RIBBON_1428_*` objects were static-inactive in the prior packet.
- `MAT_H8_SurfaceFoamRibbons_1428` had empty `_BaseMap` and `_MainTex`.
- packed shoreline foam ribbon, waterline, wet/dry basalt, biome control, and caustic source outputs were missing.
- terrain control and packed mask channels were still incomplete.
- existing `foam.png` is a candidate, not proof of active route quality.

The Unity owner must inspect the active scene/material bindings before claiming visual repair. Static source paths are not material-slot proof after import.

## Frame Debugger And Profiler Requirements

Frame Debugger or RenderGraph Viewer must prove:

- named pass `Hecton Ocean Single-Camera Depth` executes for the player/Game View camera;
- pass uses active camera depth as `_H8OceanSourceDepth`;
- `_H8OceanDepthFoamMask` is produced before water transparents sample it;
- `_GlobalShorelineFoam`, `_GlobalShorelineFoamCount`, and `_GlobalShorelineFoamRuntime` are bound when shoreline foam is active;
- `Hecton Ocean Wake Compute` runs only when compute support and kernels are valid, otherwise `Hecton Ocean Wake Clear` publishes a harmless clear texture;
- Crest realtime depth, Crest foam sim, and Crest planar reflection cameras do not reappear as active hidden render work;
- no auxiliary camera, `Camera.Render`, `ReadPixels`, `Graphics.Blit`, `CommandBuffer.Blit`, `AddUnsafePass`, CPU particle shoreline foam, or `DecalProjector` foam route is used.

Profiler/GC/memory proof must include:

- CPU main/render thread cost for ocean depth mask, shoreline foam upload, wake compute submission, and atmosphere/wave publication;
- GPU timing or render profiler cost for the depth mask and wake compute passes;
- GC Alloc column: 0 B/frame across at least 300 gameplay frames for ocean/shoreline update paths;
- Memory Profiler or rendering stats for RT and Crest texture residency;
- async wave-height readback cadence proof when `enableGpuWaveHeightReadback` is on;
- no same-frame schedule/readback/complete loop and no blocking `WaitForCompletion` in healthy gameplay cadence;
- no managed `SetData` churn in the shoreline/ocean proof route without an explicit waiver and profiler artifact.

## Continuous Quality Consequences

`GlobalQualityWeight` is continuous. It may scale visual detail, cadence, capacity, and optional telemetry. It must not change gameplay truth, DTO layout, save identity, rollback authority, buffer ownership, material identity, or route ownership.

| Lane | Required consequence |
|---|---|
| Compact / low weight | Fewer foam rows, lower shader loop limit, simpler normals, lower caustic cadence, lower wake resolution, but still readable ocean color, waterline, wet rock, route silhouettes, and photic clarity. |
| Middle | Stable default waterline/foam, readable shallow transition, credible wave/specular response, and genuinely good material identity. |
| High | Richer normal perturbation, stronger localized foam breakup, higher wake/caustic/detail cadence, stronger shoreline material response after profiler proof. |
| Ultra | Visual overkill: dense foam lace, richer reflections/shore contact, stronger shallow caustic/detail, Aegir/ocean harmony, no new required gameplay truth. |

## Reject Gates

Reject the route if any item is true:

- surface or photic water is dark/noir by default;
- water is flat opaque blue, generic blue fog, or a hard horizon band;
- shoreline has no visible contact foam/waterline breakup;
- foam is a flat strip, CPU particle spam, DecalProjector spam, or auxiliary-camera artifact;
- shallow water hides terrain instead of showing credible depth falloff;
- 20-50 m is treated as abyss darkness;
- coast terrain looks grey, crayonish, muddy, toy-like, or untextured at gameplay distance;
- Aegir, moons, or sky read as low-resolution bands, sine stripes, or placeholder;
- Crest vendor package is edited or runtime-instantiated as a workaround;
- screenshots/profiler/frame-debugger artifacts are saved under `Assets` or `Assets/Screenshots`;
- a passive screenshot or static report is counted as runtime proof;
- Compact is ugly and High/Ultra is used to hide it;
- any profiler or Frame Debugger claim lacks matching artifact path.

## Unity Owner Handoff

The Unity owner must create a fresh packet under:

`Docs/Reports/Batch20/VisualProof/<SESSION_STAMP>_ocean_shoreline_waterline/`

Required subfolders and naming follow `UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md`. Do not save proof under `Assets`.

Minimum owner actions:

1. Capture baseline before changes.
2. Recreate the critique target angle from `unity_focus_state_20260604_125701.png`.
3. Capture Game View and Scene View from matching positions.
4. Capture UI on and UI off for player-relevant shots.
5. Capture Compact, Middle, High, and Ultra with numeric `GlobalQualityWeight`.
6. Export Frame Debugger or RenderGraph Viewer proof for ocean depth, shoreline foam, wake, Crest hidden inputs, and water material passes.
7. Export Unity Profiler, GC, rendering stats, and memory/VRAM artifacts if any runtime/render/material residency path changed.
8. Record material slot proof for active water, foam, wet basalt, terrain, Aegir/sky, and caustic inputs.
9. Record rejected captures instead of deleting them.
10. Mark every unproven claim as `PENDING_VERIFICATION`.

## Task 2001 Capture Requirements

Task 2001 or the next Unity-slot owner must include these ocean-specific shots in its proof packet:

- `VP-OCE-SURF-WIDE-001`: wide surface/ocean/Aegir/coast from the critique route.
- `VP-OCE-SHL-CP-001`: close shoreline waterline at glancing angle.
- `VP-OCE-SHL-WET-002`: wet basalt/waterline material close read.
- `VP-OCE-FOAM-003`: foam contact breakup with UI off.
- `VP-OCE-ENTRY-004`: player water entry/exit line with UI on.
- `VP-OCE-UW-005`: underwater 0-5 m, surface underside and bottom readable.
- `VP-OCE-UW-025`: underwater 20-50 m, medium-depth readable, not abyss dark.
- `VP-OCE-HORIZON-006`: horizon band and Aegir relationship.
- `VP-OCE-FDG-001`: Frame Debugger/RenderGraph named pass proof.
- `VP-OCE-PROF-001`: Profiler/GC/memory proof for changed ocean/shoreline route.

Each shot row must record scene, camera coordinates, mode, UI state, quality lane, numeric `GlobalQualityWeight`, evidence label, artifact path, pass/fail, and residual risk.

## Final Claim Guard

Claim: Static source route facts, proof checklist, reject gates, and Unity owner handoff exist for ocean shoreline waterline proof.
Evidence label: STATIC_DOC / STATIC_SOURCE
Artifact: this file plus `2007_OCEAN_ROUTE_CONTRACTS.csv`, `2007_UNITY_PROOF_CHECKLIST.md`, and `2007_RISK_LEDGER.csv`.
Residual risk: no Unity import, shader import, Game View, Scene View, player, Frame Debugger, profiler, GC, memory, VRAM, or gameplay proof.


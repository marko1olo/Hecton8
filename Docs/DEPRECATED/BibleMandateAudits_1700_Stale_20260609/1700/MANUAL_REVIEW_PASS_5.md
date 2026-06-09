# Manual Review Pass 5 - Remaining Runtime Fallback Asset Factories And UI Assembly

Status: STATIC MANUAL REVIEW - NO UNITY/PROFILER/PLAYER PROOF
Date: 2026-06-02

This pass reviewed the next layer of runtime fallback factories found after pass 4. It focuses on world fallback materials/meshes, drone mock routes, Sargassum/brine runtime generation, thermal/cable effect child creation, and UI runtime mesh/material/hierarchy assembly.

## Reviewed Files

- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs`
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/World/BioCableIK.cs`
- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`
- `Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs`
- `Assets/_Project/Scripts/UI/SonarHoloCompass.cs`
- `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/UI/UIParticleEffect.cs`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`
- `Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs`
- `Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs`

## Static Findings

### World/HLOD/Wreck Material Fallbacks

- `WreckMaterialRegistry.cs` creates a BRG-local runtime material clone at line 548 and uses tier fallback material slots. This may be a legal cold BRG owner route, but release proof must show fixed material count, no per-instance clone churn, and assigned source materials for all wreck tiers.
- `HectonHLODRenderer.cs` creates a fallback runtime material at line 420 if no explicit HLOD material is assigned.
- `HectonDistantLandmarkRenderer.cs` creates a fallback runtime material at line 532 if no silhouette material is assigned.
- `HectonVoxelStreamingBridge.cs` creates a prewarmed voxel fade material pool at line 621. This can be acceptable only if pool size is fixed, bootstrap-only, and profiled.
- Classification: `YELLOW_RUNTIME_RENDER_MATERIAL_POOL_PROOF_REQUIRED`.

### DroneFleetManager Mock Routes

- Evidence: mock repair/mining signal buses, `BuildMockSdfGrid()` at line 3969, mock SDF uses at lines 3379/4385/7347, fallback drone chassis specs at lines 3687/3900/7697, and procedural drone material at line 6374.
- Classification: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.
- Why it blocks: drones/automation can mutate world state. Mock repair/mining/SDF routes cannot be indistinguishable from production truth. Procedural material fallback also conflicts with authored visual route requirements.
- Required closure: production drone data/SDF/task providers, authored drone material proof, mock routes explicitly editor/test/headless diagnostics or disabled in release gameplay.

### Sargassum Runtime Fallback Geometry/Materials

- `SargassumGlobalDragManager.cs` creates fallback crate/scrap/scavenger materials, fallback meshes, BRG material clones, and fallback nesting prototypes. Evidence includes fallback mesh/material creation around lines 2891/2907/3401/3871/4018/5038.
- `SargassumCollapseChunk.cs` creates a fallback `SiltTrail` GameObject and ParticleSystem at lines 668-679 when prefab wiring is missing.
- Classification: `P0_SARGASSUM_RUNTIME_FALLBACK_ASSET_FACTORY`.
- Required closure: authored Sargassum meshes/materials/trail prefab assignments in production, pool/prewarm proof, and fallback route restricted to editor/dev/recovery.

### Brine Pool Runtime Mesh/GameObject Generation

- `HectonBrinePoolMeshGenerator.cs` creates a generated root at line 233, pool objects at line 311, fog objects at line 427, and a runtime mesh at line 444.
- Classification: `P0_BRINE_POOL_RUNTIME_MESH_OBJECT_GENERATION`.
- Why it blocks: brine pool terrain/visual/collider assets are world content. If generated in player runtime, they violate the offline-generated-asset pipeline unless proven to be an editor bake or a bounded streaming-authority exception with profiler proof.
- Required closure: editor/offline brine pool bake path or explicit runtime exception route card with fixed budgets, no per-frame allocation, collision proxy proof, and device captures.

### Thermal/Cable/Chemical/Flora Runtime Effect Roots

- `AbyssalThermalManager.cs` forbids runtime material creation in serialized tooltips for fluid decals and BioCable line renderers, which is good. It still creates `BioCableIK` GameObjects at line 4199.
- `BioCableIK.cs` forbids runtime material creation for spark particles, but creates a `CableSparkFX` GameObject at line 480.
- `ChemicalInfluenceGrid.cs` creates a runtime root at line 334.
- `FloraInteractionManager.cs` creates `__VegetationSedimentBursts` at line 2589.
- Classification: `YELLOW_RUNTIME_EFFECT_ROOT_PREFAB_OR_POOL_PROOF_REQUIRED`.
- Required proof: these roots must be bootstrap/pool-only, bounded, non-spawning after gameplay begins, or replaced by authored prefab/pool assignments.

### UI Runtime Mesh/Material/Hierarchy Assembly

- `ShaderCompassRibbon.cs` creates a runtime root at line 148 and material at line 201.
- `SonarHoloCompass.cs` creates runtime UI roots/rects at lines 253 and 753.
- `SubmarineSonarHoloMapRenderer.cs` creates runtime mesh/material at lines 402 and 432.
- `SubtitleManager.cs` creates manager/text/waveform/bar UI GameObjects at lines 314/2153/2178/2204 while using fixed char buffers for text.
- `SuitHUDV4CanvasOverlay.cs` creates threat chevron mesh/material and several runtime materials around lines 2664/2668/2700/2744/2800/3645/3647.
- `UIParticleEffect.cs` creates a runtime particle GameObject at line 152.
- `VehicleSubOsCockpitRuntime.cs` creates runtime materials at lines 1319/1488 and runtime meshes around lines 2595/2654; fallback damage proxy/glyph paths are present.
- `WorldSpaceTMPSharpnessController.cs` creates a per-label TMP material clone at line 276.
- `WristHologramHudRuntime.cs` creates a wrist HUD runtime material at line 885, property block at line 899, runtime mesh at line 2237, and fallback quad arrays.
- Classification: `YELLOW_UI_RUNTIME_ASSEMBLY_AND_MATERIAL_PROOF_REQUIRED`.
- Required closure: UI prefab/material assignment proof, bounded bootstrap-only construction proof, no post-bootstrap hierarchy growth, no repeated material clones, and 300-frame GC/canvas rebuild profiler proof.

## Pass 5 Verdict

- The project has many cold-owner comments and some good guard language, but there are still numerous runtime asset factories.
- Several are potentially legal bootstrap routes, but none are release-closed by static code inspection.
- New P0 blockers are warranted for drones mock truth/procedural material, Sargassum fallback asset factory, and brine pool runtime mesh/object generation.
- UI runtime assembly remains yellow rather than P0 because UI can legally build pooled runtime views, but only with profiler/prefab/material proof.

## Non-Closure

This pass adds more blockers and proof gates. It does not close the full line-level runtime triage. The next pass should method-read remaining `new GameObject`, `new Material`, `new Mesh`, `AsyncGPUReadback`, `Find*`, `Resources.Load`, and `WaitForCompletion` sites not yet classified in manual passes.

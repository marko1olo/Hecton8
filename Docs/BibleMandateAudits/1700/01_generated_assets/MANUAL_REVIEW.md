# Generated Assets Manual Review

Status: STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` from pass 1
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` for generated HUD mesh
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` for fallback material
- `Assets/_Project/Scripts/World/ImpostorSystem.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs`
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

## What Exists

- Root bibles and procedural asset pipeline define offline generation, mesh validation, LOD, material, texture, and collision proxy law.
- Editor-only collider fitting in `ProceduralWreckGenerator` is guarded with editor APIs.
- Some runtime fallback meshes/materials are marked `COLD ALLOC` and appear intended as fail-safes.

## What Is Missing / Not Proven

- `ProceduralWreckGenerator` still contains player-runtime mesh merge paths unless registry assignment or player exclusion is proven.
- HUD threat-chevron mesh is generated during player bootstrap rather than loaded as a serialized asset.
- Radar fallback material exists if prefab material assignment is missing.
- No generated asset manifest / flat-material / wireframe / collider-proxy proof was generated in this audit pass.
- `ImpostorSystem` creates runtime materials for billboards/impostors instead of relying only on baked impostor atlases/material assets.
- `ResourceDistributionDirector` creates runtime resource proxy prefab templates and ghost materials if authored assets are missing.
- `FoundationPylonGpuBatch` can create runtime pylon material fallback and use mock SDF if encoded terrain data is missing.
- `SargassumGlobalDragManager` can build fallback meshes/materials/BRG material clones for Sargassum presentation.
- `SargassumCollapseChunk` can create fallback silt trail GameObject/ParticleSystem if prefab wiring is missing.
- `HectonBrinePoolMeshGenerator` creates runtime GameObjects and a runtime mesh for brine pool/fog content in the reviewed path.
- `DroneFleetManager` has procedural material and mock SDF/data routes.

## Current Classification

- `ProceduralWreckGenerator.cs`: `YELLOW_POTENTIAL_RUNTIME_MESH_GENERATION`.
- UI/generated fallback meshes: `YELLOW_BOOTSTRAP_FALLBACK_ONLY`.
- Material fallbacks: `YELLOW_PREFAB_ASSIGNMENT_PROOF_REQUIRED`.
- `HectonIndirectVegetationRenderer.cs`: `YELLOW_RELEASE_PREFAB_ASSIGNMENT_REQUIRED` because `_generateMeshAtRuntime`, `_generateImpostorMeshAtRuntime`, and `BuildImpostorCardMesh()` allow runtime mesh fallback.
- `MarauderOutpostGenerationService.cs`: `YELLOW_RUNTIME_FALLBACK_ASSET_RISK` because `CreateCubeMesh()` and fallback material creation run if shell assets are not assigned.
- `ImpostorSystem.cs`: `P0_RUNTIME_IMPOSTOR_MATERIAL_DERIVATION`.
- `ResourceDistributionDirector.cs`: `P0_RUNTIME_RESOURCE_PROXY_ASSET_FALLBACK`.
- `FoundationPylonGpuBatch.cs`: `P0_FOUNDATION_MOCK_SDF_TRUTH_ROUTE` because production construction must not silently use mock SDF or runtime material fallback.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.
- `SargassumGlobalDragManager.cs` and `SargassumCollapseChunk.cs`: `P0_SARGASSUM_RUNTIME_FALLBACK_ASSET_FACTORY`.
- `HectonBrinePoolMeshGenerator.cs`: `P0_BRINE_POOL_RUNTIME_MESH_OBJECT_GENERATION`.

## Required Next Proof

- Player-build exclusion or serialized asset manifest for wreck visual mesh generation.
- Prefab audit confirming release assignments for radar/HUD/generated asset materials.
- Generated asset package proof: mesh, prefab, material, texture, LOD, collider proxy, manifest, validation report.
- Release prefab audit confirming vegetation renderers and outpost generators use authored meshes/materials, not runtime fallback primitives.
- Baked impostor package proof: atlas textures, material assets, mesh/proxy data, and registry assignment.
- Authored resource node/magma vent prefab proof and pool prewarm proof.
- Encoded SDF/DataMonolith proof for foundation/pylon placement and no mock SDF in production player builds.
- Production drone provider/material proof and mock route exclusion.
- Authored Sargassum meshes/materials/trail proof.
- Brine pool editor/offline bake proof or explicit runtime terrain exception route with profiler/device/collider proof.

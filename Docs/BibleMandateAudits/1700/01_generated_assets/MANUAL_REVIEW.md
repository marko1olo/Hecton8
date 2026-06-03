# Generated Assets Manual Review

Status: YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING - NO UNITY/PROFILER PROOF
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
- `HectonIndirectVegetationRenderer` current source has runtime generation flags false and wraps procedural near/impostor mesh construction in `#if UNITY_EDITOR`.
- `MarauderOutpostGenerationService` current source validates authored `shellMesh`/`shellMaterial` and faults if missing; the old runtime cube/material synthesis scan evidence is stale.
- `SargassumGlobalDragManager` current source no longer contains the old runtime `new Mesh`, `new Material`, or `RecalculateNormals` fallback lines from the raw scan.
- Some runtime fallback meshes/materials are marked `COLD ALLOC` and appear intended as fail-safes.

## What Is Missing / Not Proven

- `ProceduralWreckGenerator` still contains player-runtime mesh merge paths unless registry assignment or player exclusion is proven.
- HUD threat-chevron mesh is generated during player bootstrap rather than loaded as a serialized asset.
- Radar fallback material exists if prefab material assignment is missing.
- No generated asset manifest / flat-material / wireframe / collider-proxy proof was generated in this audit pass.
- `ImpostorSystem` creates runtime materials for billboards/impostors instead of relying only on baked impostor atlases/material assets.
- `ResourceDistributionDirector` creates runtime resource proxy prefab templates and ghost materials if authored assets are missing.
- `FoundationPylonGpuBatch` can create runtime pylon material fallback and use mock SDF if encoded terrain data is missing.
- Sargassum release proof is still missing: authored meshes/materials/nesting archetypes, trail/pool prefabs, MapMagic fallback policy, and capture proving no primitive/default replacement path in production.
- `SargassumCollapseChunk` can create fallback silt trail GameObject/ParticleSystem if prefab wiring is missing.
- `HectonBrinePoolMeshGenerator` creates runtime GameObjects and a runtime mesh for brine pool/fog content in the reviewed path.
- `DroneFleetManager` has procedural material and mock SDF/data routes.

## Current Classification

- Line-level result: all 249 generated-asset runtime suspect lines are classified as 118 editor/dev guarded, 120 cold/setup/fault/owner-lifetime, 4 false positives, and 7 registered runtime violations.
- `ProceduralWreckGenerator.cs`: `RUNTIME_VIOLATION_REGISTERED_RB001` for runtime `new Mesh()` merge/proxy routes at `:5660`, `:5866`, and `:5988`.
- `HectonWorldShellController1428.cs` / `HectonWorldShellVisualDriver1428.cs`: `RUNTIME_VIOLATION_REGISTERED_RB015` for prototype direct `Camera.main`, `Update()`, and `LateUpdate()` shell routes.
- UI/generated fallback meshes: `YELLOW_BOOTSTRAP_FALLBACK_ONLY`.
- Material fallbacks: `YELLOW_PREFAB_ASSIGNMENT_PROOF_REQUIRED`.
- `HectonIndirectVegetationRenderer.cs`: `YELLOW_AUTHORED_PREFAB_PROOF_REQUIRED_AFTER_EDITOR_ONLY_HARDENING`.
- `MarauderOutpostGenerationService.cs`: `YELLOW_AUTHORED_SHELL_RESOURCE_PROOF_REQUIRED_CURRENT_RUNTIME_FALLBACK_STALE`.
- `ImpostorSystem.cs`: `P0_RUNTIME_IMPOSTOR_MATERIAL_DERIVATION`.
- `ResourceDistributionDirector.cs`: `P0_RUNTIME_RESOURCE_PROXY_ASSET_FALLBACK`.
- `FoundationPylonGpuBatch.cs`: `P0_FOUNDATION_MOCK_SDF_TRUTH_ROUTE` because production construction must not silently use mock SDF or runtime material fallback.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.
- `SargassumGlobalDragManager.cs`: `YELLOW_AUTHORED_SARGASSUM_PACKAGE_PROOF_REQUIRED_CURRENT_RUNTIME_FALLBACK_STALE`.
- `SargassumCollapseChunk.cs`: `P0_SARGASSUM_TRAIL_POOL_PREFAB_PROOF_REQUIRED`.
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

## Pass 7 Addendum - Wreck Method Structure

- `ProceduralWreckGenerator` is not editor-only by type: it is a runtime `MonoBehaviour` and dispatcher-facing generator.
- `GenerateInternal()` and `GenerateInternalAsync()` build merged visual meshes only when `wreckMaterialRegistry == null`; therefore release closure cannot say "editor generator" unless player prefab/build proof makes `wreckMaterialRegistry` mandatory.
- `BuildProxyMesh()` also creates a runtime `Mesh` when `wreckCollisionProxyMesh` is absent and `buildAsyncNavigationBake` is enabled. This is a proxy route, not the editor-only compound collider fitter.
- `HectonCompoundColliderAutoFitter` remains legal offline tooling because it is under `#if UNITY_EDITOR`.

## Pass 8 Addendum - Vegetation Default Runtime Mesh Fallback - Superseded By Current Source

- Older review found `_generateMeshAtRuntime = true`, `_generateImpostorMeshAtRuntime = true`, and a runtime `BuildImpostorCardMesh()` route. Current source is materially different: the flags are false and procedural builders are editor-only.
- The remaining release issue is proof, not the old default: production prefabs must show authored near/impostor meshes, full organic vertex-channel semantics, UV/material compliance, LODs, bounds, and collider proxies.

## Pass 22 Addendum - Generated Asset Line-Level Closure

- `LINE_LEVEL_CLASSIFICATION.md` closes the generated-assets line-level queue at static source level: 249 total suspect lines, 118 editor/dev guarded, 120 cold/setup/fault/owner-lifetime, 4 false positives, and 7 registered runtime violations.
- Confirmed runtime generated mesh violations remain in `ProceduralWreckGenerator.cs` under `RB-001`.
- Confirmed prototype execution-phase violations remain in `HectonWorldShellController1428.cs` and `HectonWorldShellVisualDriver1428.cs` under `RB-015`.
- Stale evidence was corrected for `MarauderOutpostGenerationService.cs`, `SargassumGlobalDragManager.cs`, and `HectonIndirectVegetationRenderer.cs`.
- This pass does not prove generated asset quality. Required artifacts remain generated package manifests, import settings, LOD reports, flat-material and wireframe screenshots, UV/atlas reports, collider proxy reports, authored package proof, and compact/high player-build profiler/device captures.

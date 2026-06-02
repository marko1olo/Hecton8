# World / Terrain / Voxels / Ecosystem Manual Review

Status: STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs`
- `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs`
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`
- `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` from pass 1
- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`
- `Assets/_Project/Scripts/World/BiomeBoundarySdfRuntime.cs`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/World/ImpostorSystem.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs`
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/World/BioCableIK.cs`
- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`

## What Exists

- Vegetation and radar systems use DataVault/H8Memory ownership instead of unmanaged anonymous native buffers.
- Vegetation path jobs are scheduled and disposed through tracked job dependencies.
- Radar runtime uses `ISlowTickable`, `ILateFrameTickable`, and `IRenderable`, not raw `Update`.
- Radar persistent state handles and graphics buffers are created in cold setup.
- Persistent world registry owns large native storage with explicit owner structures and telemetry rings.
- `BiomeBoundarySdfRuntime` has owner/cold native setup shape and fault-dump-only temp buffer use in the reviewed route.
- `GPUScatterDirector` owns GPU scatter resources and visible-count telemetry/readback routes.
- `SargassumMicroFaunaBoids` has a large GPU/job ownership shape with GraphicsBuffers and DataVault handles.

## What Is Missing / Not Proven

- `VegetationNavGridSynchronizer` allocates persistent native scratch (`NativeList<Vector3>`, H8Memory `NativeArray<T>`) during path scheduling. That is a real runtime allocation path until proven rare/cold or replaced with preallocated owner scratch.
- `GroundPenetratingRadarRuntime` allocates H8Memory persistent pending-job buffers per scan and can grow SDF snapshot memory.
- Persistent world registry paging paths include temp native arrays around sector hydration; pass 1 did not fully classify every paging window.
- No profiler/native memory capture proves path/radar stress stays within compact budgets.
- `VegetationFlowFieldIntegrator` and `VegetationChunkResidencyDirector` perform tracked slow-tick/chunk-build native scratch allocation. This is better than hot `Update`, but still requires pool/proof.
- `HectonIndirectVegetationRenderer` has runtime generated near/impostor mesh fallback. Release scenes need authored mesh assignment.
- `VoxelDynamicNavGridRuntime` has static pools and DataVault handles, but dynamic obstacle/volume registration must prove no repeated growth after bootstrap.
- `EcosystemRuntimeInstaller` dynamically creates scene root/components; production should prefer authored bootstrap root prefabs.
- `ImpostorSystem` derives runtime billboard materials from scene renderers (`new Material(sourceMaterial)` and `new Material(shader)`). Production needs baked impostor atlas/material proof.
- `ResourceDistributionDirector` creates runtime resource/magma proxy prefab templates and runtime materials. Production needs authored proxy prefabs/materials and pool prewarm proof.
- `GPUScatterDirector.TryRefreshBiomeHeatmapTextureHot()` must prove biome heatmap allocation/upload is not recurring after gameplay begins.
- `SargassumMicroFaunaBoids` needs GPU/material/readback proof before dense ecology presentation can be accepted.
- Wreck/HLOD/landmark/voxel fade systems have runtime material clone/pool routes requiring fixed-count proof.
- `SargassumGlobalDragManager` and `SargassumCollapseChunk` have runtime fallback mesh/material/trail factories when authored assets are missing.
- `HectonBrinePoolMeshGenerator` creates runtime brine pool GameObjects/fog and a runtime mesh in the reviewed path.
- Thermal/cable/chemical/flora systems create effect roots/children (`BioCableIK`, `CableSparkFX`, chemical grid root, sediment burst root) that require authored prefab/pool proof.

## Current Classification

- `VegetationNavGridSynchronizer.cs`: `YELLOW_NATIVE_RUNTIME_ALLOCATION_REVIEW_REQUIRED`.
- `VegetationFlowFieldIntegrator.cs`: `YELLOW_BOUNDED_SLOW_TICK_NATIVE_ALLOCATION`.
- `VegetationChunkResidencyDirector.cs`: `YELLOW_BOUNDED_CHUNK_BUILD_ALLOCATION`.
- `HectonIndirectVegetationRenderer.cs`: `YELLOW_RELEASE_PREFAB_ASSIGNMENT_REQUIRED`.
- `VoxelDynamicNavGridRuntime.cs`: `YELLOW_RUNTIME_GROWTH_PROOF_REQUIRED`.
- `GroundPenetratingRadarRuntime.cs`: `YELLOW_NATIVE_RUNTIME_ALLOCATION_REVIEW_REQUIRED`.
- `PersistentWorldRegistry.cs`: `YELLOW_NATIVE_OWNER_LIFETIME_PROOF_REQUIRED`.
- `EcosystemRuntimeInstaller.cs`: `YELLOW_BOOTSTRAP_PREFAB_ROUTE_REQUIRED`.
- `BiomeBoundarySdfRuntime.cs`: `YELLOW_OWNER_COLD_STORAGE_WITH_FAULT_DUMP_PROOF_REQUIRED`.
- `GPUScatterDirector.cs`: `YELLOW_GPU_UPLOAD_READBACK_PROOF_REQUIRED`.
- `ImpostorSystem.cs`: `P0_RUNTIME_IMPOSTOR_MATERIAL_DERIVATION`.
- `ResourceDistributionDirector.cs`: `P0_RUNTIME_RESOURCE_PROXY_ASSET_FALLBACK`.
- `SargassumMicroFaunaBoids.cs`: `YELLOW_GPU_BOID_MATERIAL_READBACK_PROOF_REQUIRED`.
- Wreck/HLOD/landmark/voxel fade material pools: `YELLOW_RUNTIME_RENDER_MATERIAL_POOL_PROOF_REQUIRED`.
- `SargassumGlobalDragManager.cs` / `SargassumCollapseChunk.cs`: `P0_SARGASSUM_RUNTIME_FALLBACK_ASSET_FACTORY`.
- `HectonBrinePoolMeshGenerator.cs`: `P0_BRINE_POOL_RUNTIME_MESH_OBJECT_GENERATION`.
- Thermal/cable/chemical/flora effect roots: `YELLOW_RUNTIME_EFFECT_ROOT_PREFAB_OR_POOL_PROOF_REQUIRED`.

## Required Next Proof

- Stress test path requests and radar pings for native allocation count, bytes, and frame cost.
- Convert recurring scratch allocations to preallocated DataVault/H8Memory pools if stress proof fails.
- Add or expose counters for path request cadence, radar scan cadence, allocation bytes, and job completion delay.
- Add vegetation renderer prefab proof for authored meshes/materials and disabled runtime mesh fallback.
- Add voxel dynamic obstacle churn proof and ecosystem bootstrap scene inclusion proof.
- Add baked impostor/resource proxy asset proof and remove or guard runtime material/proxy fallback paths for release.
- Add GPU scatter/microfauna captures with buffer lifetime, readback cadence, and no post-bootstrap allocation growth.
- Add Sargassum/brine authored asset or bake proof, plus prefab/pool proof for thermal/cable/chemical/flora effect roots.

## Pass 6 Addendum - World Shell And Scene Lookup Sweep

- `HectonWorldShellController1428.cs` is a P0 release-boundary risk if present in gameplay scenes: raw `Update`, direct keyboard/mouse polling, `Camera.main`, and transform/camera mutation bypass the input/camera/systems bibles.
- `HectonWorldShellVisualDriver1428.cs` uses raw `LateUpdate` for visual animation and `FindObjectsByType`/managed arrays in `Awake` when references are not serialized. This is acceptable only as a demo/dev shell or one-time authored startup cache with proof.
- `WorldProceduralScatterWorkingMemory.cs` registers persistent native working memory with `NativeMemorySentinel`, but `EnsureCapacity()` can reallocate scratch arrays. It needs a 300-frame scatter stress proof showing no growth after gameplay begins.
- `ScavengingLootOracle.cs:1782` uses `Resources.FindObjectsOfTypeAll<GameObject>()` for HideAndDontSave orphan cleanup. It requires reload/fault-only proof and must not run in gameplay hot paths.

## Pass 7 Addendum - Native Staging Detail

- `VegetationNavGridSynchronizer` allocates per-path `NativeList<Vector3>` with `Allocator.Persistent` and H8Memory job snapshots during abyssal path scheduling. This is a runtime hot suspect until preallocated scratch or path-spam native-memory proof exists.
- `VegetationFlowFieldIntegrator` allocates slow-tick owner staging arrays for flow/threat/thermal work. Owner-phase scheduling is better than raw `Update`, but the release gate is still zero post-bootstrap allocation/growth under stress.
- `GroundPenetratingRadarRuntime` creates pending scan arrays for each scheduled radar job and can allocate/grow an SDF snapshot to the leased SDF payload length. Radar ping spam needs native memory proof or fixed-capacity preallocation.
- `PersistentWorldRegistry` sector IO uses TempJob arrays/lists and managed arrays in explicit load/write windows. That is not automatically a gameplay hot-path defect, but save/stream stress proof must show frame impact and allocation windows are bounded.

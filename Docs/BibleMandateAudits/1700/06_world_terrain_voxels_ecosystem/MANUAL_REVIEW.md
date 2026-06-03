# World / Terrain / Voxels / Ecosystem Manual Review

Status: YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING - NO UNITY/PROFILER PROOF
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
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs` (`GraphicsBufferUploadUtility`)

## What Exists

- Vegetation and radar systems use DataVault/H8Memory ownership instead of unmanaged anonymous native buffers.
- Vegetation path jobs are scheduled and disposed through tracked job dependencies.
- Radar runtime uses `ISlowTickable`, `ILateFrameTickable`, and `IRenderable`, not raw `Update`.
- Radar persistent state handles and graphics buffers are created in cold setup.
- Persistent world registry owns large native storage with explicit owner structures and telemetry rings.
- `BiomeBoundarySdfRuntime` has owner/cold native setup shape and fault-dump-only temp buffer use in the reviewed route.
- `GPUScatterDirector` owns GPU scatter resources and visible-count telemetry/readback routes.
- `SargassumMicroFaunaBoids` has a large GPU/job ownership shape with GraphicsBuffers and DataVault handles.
- `HectonIndirectVegetationRenderer` current source has runtime generation flags false and editor-only procedural near/impostor mesh builders.
- Current `SargassumGlobalDragManager` no longer contains the old runtime `new Mesh`, `new Material`, or `RecalculateNormals` fallback evidence from stale scans.
- Current `MarauderOutpostGenerationService` faults on missing authored shell resources rather than synthesizing a runtime cube/material path.

## What Is Missing / Not Proven

- `VegetationNavGridSynchronizer` allocates persistent native scratch (`NativeList<Vector3>`, H8Memory `NativeArray<T>`) during path scheduling. That is a real runtime allocation path until proven rare/cold or replaced with preallocated owner scratch.
- `GroundPenetratingRadarRuntime` allocates H8Memory persistent pending-job buffers per scan and can grow SDF snapshot memory.
- Persistent world registry paging paths include temp native arrays around sector hydration; pass 1 did not fully classify every paging window.
- No profiler/native memory capture proves path/radar stress stays within compact budgets.
- `VegetationFlowFieldIntegrator` and `VegetationChunkResidencyDirector` perform tracked slow-tick/chunk-build native scratch allocation. This is better than hot `Update`, but still requires pool/proof.
- `HectonIndirectVegetationRenderer` no longer has the older player-runtime mesh fallback shape, but release scenes still need authored near/impostor mesh, material, UV, vertex-channel, LOD, and collider proof.
- `VoxelDynamicNavGridRuntime` has static pools and DataVault handles, but dynamic obstacle/volume registration must prove no repeated growth after bootstrap.
- `EcosystemRuntimeInstaller` dynamically creates scene root/components; production should prefer authored bootstrap root prefabs.
- `ImpostorSystem` derives runtime billboard materials from scene renderers (`new Material(sourceMaterial)` and `new Material(shader)`). Production needs baked impostor atlas/material proof.
- `ResourceDistributionDirector` creates runtime resource/magma proxy prefab templates and runtime materials. Production needs authored proxy prefabs/materials and pool prewarm proof.
- `GPUScatterDirector.TryRefreshBiomeHeatmapTextureHot()` must prove biome heatmap allocation/upload is not recurring after gameplay begins.
- `SargassumMicroFaunaBoids` needs GPU/material/readback proof before dense ecology presentation can be accepted.
- Wreck/HLOD/landmark/voxel fade systems have runtime material clone/pool routes requiring fixed-count proof.
- Sargassum proof remains missing: authored meshes/materials/nesting archetypes, trail/pool prefabs, MapMagic fallback policy, and capture proving no primitive/default replacement in production.
- `HectonBrinePoolMeshGenerator` creates runtime brine pool GameObjects/fog and a runtime mesh in the reviewed path.
- Thermal/cable/chemical/flora systems create effect roots/children (`BioCableIK`, `CableSparkFX`, chemical grid root, sediment burst root) that require authored prefab/pool proof.

## Current Classification

- `VegetationNavGridSynchronizer.cs`: `YELLOW_NATIVE_RUNTIME_ALLOCATION_REVIEW_REQUIRED`.
- `VegetationFlowFieldIntegrator.cs`: `YELLOW_BOUNDED_SLOW_TICK_NATIVE_ALLOCATION`.
- `VegetationChunkResidencyDirector.cs`: `YELLOW_BOUNDED_CHUNK_BUILD_ALLOCATION`.
- Line-level result: all 254 world/terrain/voxel/ecosystem runtime suspect lines are classified as 120 editor/dev guarded, 123 cold/setup/fault/owner-lifetime, 4 false positives, and 7 registered runtime violations.
- `ProceduralWreckGenerator.cs`: `RUNTIME_VIOLATION_REGISTERED_RB001` for runtime `new Mesh()` merge/proxy routes at `:5660`, `:5866`, and `:5988`.
- `HectonWorldShellController1428.cs` / `HectonWorldShellVisualDriver1428.cs`: `RUNTIME_VIOLATION_REGISTERED_RB015` for prototype `Camera.main`, `Update()`, and `LateUpdate()` routes.
- `HectonIndirectVegetationRenderer.cs`: `YELLOW_AUTHORED_PREFAB_PROOF_REQUIRED_AFTER_EDITOR_ONLY_HARDENING`.
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
- `SargassumGlobalDragManager.cs`: `YELLOW_AUTHORED_SARGASSUM_PACKAGE_PROOF_REQUIRED_CURRENT_RUNTIME_FALLBACK_STALE`.
- `SargassumCollapseChunk.cs`: `P0_SARGASSUM_TRAIL_POOL_PREFAB_PROOF_REQUIRED`.
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

## Pass 8 Addendum - Vegetation Chunk, MapMagic, And Voxel Obstacle Detail

- `VegetationChunkResidencyDirector` allocates job record arrays for grass/floating/kelp chunk slices, H8Memory threat echo copies, and sand/rock/height chunk payloads. Finalization uses non-forced job completion, which is a good owner shape but still needs chunk-stream stress proof.
- `HectonMapMagicVegetationBridge` allocates persistent job records and managed finalized matrix/data/int arrays for chunk payloads. Its dispose/teardown helpers force-complete dependencies; closure requires proof these paths are tile teardown/shutdown only, not normal-frame cadence.
- `VoxelDynamicNavGridRuntime` uses registered Box/Capsule collider arrays and persistent snapshot pools instead of scene-wide collider lookup. That is structurally better than scene scanning, but obstacle dirty cadence, primitive-count caps, snapshot lease capacity, and no post-bootstrap growth still need stress proof.

## Pass 11 Addendum - Sonar SDF, GPU Scatter, Biome SDF, And Resources

- `TopographicalSonarSynthesizer` uses persistent H8Memory buffers, DataVault handles, graphics buffers, and non-forced job finalization. That is a good sensor rendering shape, but `ScheduleSonarScan()` falls back to `GenerateMockSdfJob` when published SDF is unavailable. Release sonar needs published SDF/DataMonolith proof; mock SDF may remain only as diagnostic/degraded route.
- `GPUScatterDirector` owns GPU scatter resources but can recreate capacity-dependent buffers and refresh a biome heatmap texture. Scatter closure needs capacity-stability counters, no recurring heatmap `Apply()`, async readback cadence proof, and no normal-frame `WaitForCompletion()`.
- `BiomeBoundarySdfRuntime` reviewed as `GREENISH_OWNER_COLD_STORAGE_WITH_FAULT_DUMP_PROOF_REQUIRED`: one persistent sample result scratch and Temp byte payload only in black-box dump worker.
- `ResourceDistributionDirector` reviewed as `GREENISH_RESOURCE_METAMORPHISM_OWNER_STORAGE_WITH_POOL_PROOF_REQUIRED`: metamorphism workspace has owner capacity checks and no-growth pool intent. This does not close the separate resource proxy prefab/material fallback P0.

## Pass 13 Addendum - Scatter Working Memory And Voxel Streaming Bridge

- `WorldProceduralScatterWorkingMemory` owns persistent native arrays/lists/maps and registers them with `NativeMemorySentinel`, but candidate acceptance, grid placement, and sampling buffers can grow by capacity reassignment or recreate-to-next-power-of-two.
- The growth routes are reachable from runtime scatter evaluation through `WorldProceduralScatterDirector.Tick()`, `SlowTick()`, and candidate acceptance calls. This is not an automatic defect, but it remains `YELLOW_SCATTER_WORKING_MEMORY_RUNTIME_GROWTH_PROOF_REQUIRED` until scatter stress proves capacities are prewarmed and stable after gameplay starts.
- `HectonVoxelStreamingBridge` uses dispatcher phases and bounded arrays for cave streaming requests. Its fade path clones up to 32 materials from the voxel volume source material and mutates `_ChunkDissolveFade` through pooled runtime materials.
- Voxel fade materials remain `YELLOW_VOXEL_STREAMING_FADE_MATERIAL_POOL_PROOF_REQUIRED`: source material assignment, clone count, hot-swap recreation count, SRP compatibility, and cave transition captures are still missing.

## Pass 14 Addendum - Vegetation/Radar Staging Method Closure

- `VegetationNavGridSynchronizer.TryScheduleAbyssalPath(...)` is not naive raw-frame work: it finite-checks requests, blocks concurrent path work, uses preallocated navigation handles, and finalizes through non-forced `DispatcherJobSwap.TryComplete`. The unresolved issue is per-request `NativeList<Vector3>` path buffers plus H8Memory job snapshots. Current classification: `YELLOW_ABYSSAL_PATH_PERSISTENT_SCRATCH_PER_REQUEST_PROOF_REQUIRED`.
- `VegetationFlowFieldIntegrator` rotates threat, flow, and thermal phases and scales intervals through continuous `GlobalQualityWeight`. Its scheduling/completion shape is correct, but `ScheduleThreatPropagationJob(...)`, `ScheduleFlowFieldJob(...)`, and `ScheduleThermalGridJob(...)` stage persistent H8Memory arrays per solve. Current classification: `YELLOW_FLOW_THREAT_THERMAL_OWNER_PHASE_SCRATCH_PROOF_REQUIRED`.
- `GroundPenetratingRadarRuntime` uses dispatcher interfaces, cold resource setup, and non-forced completion, but `TryCreateRadarPendingJob(...)` allocates pending H8Memory arrays per scan and `TryStageSdfLeaseToPendingSnapshot(...)` can resize the SDF snapshot to lease length. Fallback draw material creation is still covered by RB-005. Current classification: `YELLOW_GPR_PER_SCAN_PENDING_AND_SDF_SNAPSHOT_PROOF_REQUIRED`.
- `VegetationChunkResidencyDirector` and `HectonMapMagicVegetationBridge` have fixed maps, prewarm routes, slow-tick chunk build limits, and non-forced job completion. They still copy tile cache data into persistent job payloads, allocate grass/floating/kelp job records, can grow DataVault-backed aggregate buffers, and can recreate GraphicsBuffers when active vegetation counts exceed previous capacity. Current classification: `YELLOW_CHUNK_BUILD_STAGING_AND_POOL_GROWTH_PROOF_REQUIRED`.
- `GraphicsBufferUploadUtility.UploadNativeArray(...)` uses `LockBufferForWrite` and has upload-budget gates, which is the right helper shape. It does not close callsite risk; vegetation aggregate and flow uploads still need upload-byte, buffer-recreate, dirty-page, and GPU-capture proof.

## Pass 23 Addendum - World Line-Level Closure

- `LINE_LEVEL_CLASSIFICATION.md` closes the world/terrain/voxel/ecosystem line-level queue at static source level: 254 total suspect lines, 120 editor/dev guarded, 123 cold/setup/fault/owner-lifetime, 4 false positives, and 7 registered runtime violations.
- Runtime generated wreck mesh/proxy branches remain under `RB-001`.
- World-shell prototype camera/update/late-update routes remain under `RB-015`.
- Current-source stale evidence was corrected for `HectonIndirectVegetationRenderer`, `MarauderOutpostGenerationService`, and `SargassumGlobalDragManager`.
- This pass does not prove world quality. Required artifacts remain seed manifests, terrain mask proof, voxel seam reports, chunk/residency stress, SDF/DataMonolith publication, GPU scatter/readback captures, vegetation upload/recreate counters, material pool proof, Sargassum/brine/resource/impostor authored package proof, ecosystem scene composition proof, and compact/high player-build profiler/device captures.

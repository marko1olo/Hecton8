# Manual Review Pass 4 - Runtime Asset Fallbacks, GPU Scatter, Water, Construction, Offline QA

Status: STATIC MANUAL REVIEW - NO UNITY/PROFILER/PLAYER PROOF
Date: 2026-06-02

This pass inspected additional high-risk runtime suspects after pass 3. It separates legal cold owner setup, editor/development-only QA, and release-blocking runtime fallback assets. The pass does not close the full runtime triage queue.

## Reviewed Files

- `Assets/_Project/Scripts/World/BiomeBoundarySdfRuntime.cs`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/Physics/GasDynamicsSolver.cs`
- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs`
- `Assets/_Project/Scripts/World/ImpostorSystem.cs`
- `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs`
- `Assets/_Project/Scripts/SaveSystem/_SHINOBU357.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`
- `Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorJobs.cs`
- `Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs`

## Static Findings

### BiomeBoundarySdfRuntime

- Shape: dispatcher/owner runtime (`ISlowTickable`, origin shift, registry hot swap) with cold native storage setup.
- Evidence: `EnsureNativeStorage()` and `EnsureSampleScratchCold()` are cold setup routes; fault dump uses a temporary byte buffer only during dump.
- Classification: `YELLOW_OWNER_COLD_STORAGE_WITH_FAULT_DUMP_PROOF_REQUIRED`.
- Required proof: DataVault growth counter, fault-dump path proof, and no per-frame allocation after bootstrap.

### GPUScatterDirector

- Shape: owner-managed GPU scatter with resources initialized in setup and per-frame/slow-tick dispatches.
- Evidence: `TryRefreshBiomeHeatmapTextureHot()` at lines 698/1115/1118; visible-count readback via `AsyncGPUReadback.RequestIntoNativeArray` at line 2234.
- Classification: `YELLOW_GPU_UPLOAD_READBACK_PROOF_REQUIRED`.
- Risk: the heatmap method name is hot and must prove it does not allocate or recreate `Texture2D`/upload arrays after gameplay begins. Async readback must prove it is cadence-limited and not a same-frame stall.
- Required proof: compact/high GPU profiler, Frame Debugger/RenderGraph capture, readback cadence counters, and memory proof for biome heatmap resources.

### GasDynamicsSolver

- Shape: fixed/post-fixed owner solver using scheduled jobs and explicit completion windows.
- Evidence: `FixedTick`, `PostFixedTick`, and late-frame completion routes exist; completion is owner-side, not arbitrary consumer polling.
- Classification: `YELLOW_JOB_COMPLETION_WINDOW_PROOF_REQUIRED`.
- Required proof: 300-frame gas stress showing no hidden same-frame completion stall, no allocation growth, and no gameplay truth mutation outside owner phases.

### AutonomousExtractorSystem

- Shape: slow-tick construction/economy owner with cold fixed native state and bounded contact collection.
- Classification: `GREENISH_OWNER_PHASE_WITH_STRESS_PROOF_REQUIRED`.
- Required proof: extractor density stress, tool/construction query proof, and black-box state ring validation.

### ImpostorSystem

- Shape: runtime billboard/impostor material derivation from scene-owned renderers.
- Evidence: file comment says "Build runtime billboard materials"; `new Material(sourceMaterial)` at line 730; `BuildDistantGeologyBillboardMaterial()` creates new materials at lines 1066 and 1102.
- Classification: `P0_RUNTIME_IMPOSTOR_MATERIAL_DERIVATION`.
- Why it blocks: generated asset and rendering bibles require serialized/baked material families, atlases, and impostor assets for production. Runtime derivation may be cold, but it can silently create per-source materials and hide missing authoring.
- Required closure: baked impostor atlases/materials, a production material registry, or strict proof that runtime derivation is editor/dev/recovery-only and never the normal release route.

### WAL Fuzzer / _SHINOBU357

- Shape: offline QA/fuzzer code, not gameplay runtime.
- Evidence: comments mark the validation barrier as offline NUnit/editor proof; the code is not a MonoBehaviour gameplay system in the reviewed route.
- Classification: `LEGAL_OFFLINE_QA_PROOF_ROUTE`.
- Required proof: run the WAL fuzzer/fault-injection suite when persistence acceptance is requested. This code does not prove runtime persistence correctness by existing.

### SargassumMicroFaunaBoids

- Shape: large GPU/job boid system with GraphicsBuffers, DataVault handles, job swap completion, and async readback.
- Evidence: owner-local indirect material clone at line 2097; parasite latch readback at line 7582.
- Classification: `YELLOW_GPU_BOID_MATERIAL_READBACK_PROOF_REQUIRED`.
- Required proof: compact/high GPU capture, SRP/material proof, readback cadence proof, no post-bootstrap GraphicsBuffer/native growth, and visible density scaling through continuous `GlobalQualityWeight`.

### Visor Volumetric / Sonar Point Cloud Features

- Shape: RenderGraph/compute presentation features.
- Reviewed features: scooter volumetric shafts, sonar point cloud, volumetric particulate fog, volumetric light.
- Classification: `YELLOW_RENDERGRAPH_GPU_PROOF_REQUIRED`.
- Required proof: Frame Debugger/RenderGraph named pass capture, compact GPU profiler, high-tier overkill capture, and quality scaling evidence that gameplay truth does not change.

### ResourceDistributionDirector

- Shape: runtime resource-node prefab/pool creation with runtime primitive/material fallback.
- Evidence: `EnsureRuntimePrefab()` at line 1353, `_runtimePrefab = new GameObject(...)` at line 1364, `_magmaVentPrefab = new GameObject(...)` at line 1389, `EnsureRuntimePool()` at line 1402, runtime ghost materials at lines 3094 and 3126.
- Classification: `P0_RUNTIME_RESOURCE_PROXY_ASSET_FALLBACK`.
- Why it blocks: world/resource presentation should use authored prefabs/materials/meshes or baked proxy assets. Runtime GameObject prefab templates and generic materials risk placeholder-looking resources in production.
- Required closure: authored resource proxy prefabs/materials/meshes assigned in production, pool prewarm proof, and fallback restricted to editor/dev/recovery.

### FoundationPylonGpuBatch / FoundationSnappingCalculator

- Shape: GPU pylon presentation route with fallback SDF config and runtime material fallback.
- Evidence: `CreateDefaultMockSdfConfig()` fallback at `FoundationPylonGpuBatch.cs` lines 262/321/343, runtime `new Material(pylonShader)` at line 726, `MockSdfFallback` flag in `FoundationSnappingCalculatorData.cs`, mock fallback assignment in `FoundationSnappingCalculatorJobs.cs`.
- Classification: `P0_FOUNDATION_MOCK_SDF_TRUTH_ROUTE`.
- Why it blocks: construction placement/snapping cannot use mock SDF as production truth. If the encoded voxel SDF is absent, production must fail visibly or remain in dev/recovery mode, not silently build on fake substrate data.
- Required closure: encoded SDF/DataMonolith proof in production, mock SDF disabled or dev-only, and authored material assignment/pool proof.

### ConstructionRuntimeProxyFactory

- Shape: dev/editor runtime construction proxy factory.
- Evidence: file-level `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; it creates proxy GameObjects, a wirebox mesh, and material only inside that compile guard.
- Classification: `LEGAL_EDITOR_OR_DEVELOPMENT_BUILD_PROXY`.
- Required proof: release player build symbols exclude this file. If `DEVELOPMENT_BUILD` is used for shipped review builds, the route must be considered unsafe for production visuals.

## Additional Queue From Static Search

These files were identified by broader static search and still require method-level review before closure:

- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` - runtime material fallback.
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs` and `HectonDistantLandmarkRenderer.cs` - fallback runtime materials.
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs` - prewarmed voxel fade material pool.
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` - procedural material and mock SDF/grid routes.
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs` - fallback crate/scrap/scavenger/BRG materials.
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs` - runtime silt trail object.
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs` - runtime brine pool GameObjects.
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`, `BioCableIK.cs`, `HectonBiolumZone.cs`, and `ChemicalInfluenceGrid.cs` - runtime roots/effects needing owner/cold/proof classification.

## Pass 4 Verdict

- Static route coverage remains intact.
- More P0 release blockers were found: runtime impostor material derivation, runtime resource proxy prefab/material creation, and foundation mock SDF truth fallback.
- Several systems look structurally aligned with owner-phase doctrine but still need profiler/device proof: GPU scatter, gas dynamics, microfauna boids, visor volumetrics, and autonomous extraction.
- Offline QA code is legal but does not count as runtime proof.

## Non-Closure

This pass does not close `Runtime suspect classification remains open` or `Full line-level runtime closure remains open` in `Status_1700.md`. It adds higher-confidence blockers and proof gates for the next implementation/fix pass.

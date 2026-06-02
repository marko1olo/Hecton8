# Rendering / Shaders / VFX Manual Review

Status: STATIC REVIEW - NO FRAME DEBUGGER / GPU PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs`
- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`
- `Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs`
- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`

## What Exists

- `HectonBiolumSSGIFeature` uses Unity 6 RenderGraph entry points through `RecordRenderGraph`.
- The SSGI feature creates its composite material in `Create()` and enqueues passes in `AddRenderPasses()`.
- It declares texture reads/writes and has a compute-unavailable proxy path.
- `CameraJuiceSystem` uses dispatcher interfaces and writes presentation state through late-frame routes.
- `CameraJuiceSystem` logging is conditional/dev-only through `H8Debug` wrappers.
- Camera juice telemetry writes into a fixed DataVault ring once initialized.

## What Is Missing / Not Proven

- No RenderGraph Viewer / Frame Debugger proof has been run for `HectonBiolumSSGIFeature`.
- No compact GPU profiler capture proves Biolum SSGI cost, proxy behavior, or load-shed behavior.
- No profiler capture proves camera juice stacked effects stay within budget.
- No visual capture proves camera juice improves impact/readability without nausea or cheap post-stack noise.
- Indirect vegetation, sonar, volumetric, SSGI, point cloud, and camera-juice routes all need device GPU proof before they can be called release-correct.
- `GPUScatterDirector.TryRefreshBiomeHeatmapTextureHot()` and visible-count readback need proof that heatmap resources are not allocated/grown after gameplay starts and that async readback cadence does not stall.
- `SargassumMicroFaunaBoids` clones one owner-local indirect material and uses async readback. This can be legal only with SRP/material proof, crowd density proof, and readback cadence proof.
- Scooter volumetric shafts, sonar point cloud, particulate fog, and volumetric light features need compact/high Frame Debugger and GPU profiler proof before they satisfy rendering/presentation bibles.
- `WreckMaterialRegistry`, `HectonHLODRenderer`, `HectonDistantLandmarkRenderer`, and `HectonVoxelStreamingBridge` create runtime material clones/pools. These may be legal cold render-owner pools, but they need fixed-count and SRP batcher proof.

## Current Classification

- `HectonBiolumSSGIFeature.cs`: `YELLOW_GPU_COST_PROOF_REQUIRED`, not a structural RenderGraph violation in static review.
- `CameraJuiceSystem.cs`: `YELLOW_PROFILER_PROOF_REQUIRED`, with log findings downgraded to legal diagnostic route.
- `HectonIndirectVegetationRenderer.cs`: `YELLOW_GPU_AND_FALLBACK_ASSET_PROOF_REQUIRED`.
- `TopographicalSonarSynthesizer.cs`: `YELLOW_GPU_UPLOAD_PROOF_REQUIRED`.
- `GPUScatterDirector.cs`: `YELLOW_GPU_UPLOAD_READBACK_PROOF_REQUIRED`.
- `SargassumMicroFaunaBoids.cs`: `YELLOW_GPU_BOID_MATERIAL_READBACK_PROOF_REQUIRED`.
- Visor volumetric/point-cloud features: `YELLOW_RENDERGRAPH_GPU_PROOF_REQUIRED`.
- HLOD/landmark/wreck/voxel runtime material pools: `YELLOW_RUNTIME_RENDER_MATERIAL_POOL_PROOF_REQUIRED`.

## Required Next Proof

- RenderGraph Viewer or Frame Debugger capture showing named Biolum SSGI passes and declared resources.
- Compact lane GPU profiler capture for Biolum SSGI, camera juice, DoF/vignette/speed-line stack.
- High-tier capture showing visual overkill path without changing gameplay truth.
- BRG/indirect vegetation proof with authored meshes, no runtime generated fallback, bounded dirty-page uploads, and SRP batcher/material proof.
- GPU scatter proof with biome heatmap upload counters, visible-count readback cadence, buffer lifetime proof, and continuous `GlobalQualityWeight` density scaling.
- Microfauna boid proof with material/SRP batching evidence, no post-bootstrap GraphicsBuffer/native growth, compact/high GPU capture, and readback stall absence.
- Fixed material-pool counts, no per-instance clone churn, SRP batcher compatibility, and material assignment proof for wreck/HLOD/landmark/voxel fade paths.

## Pass 6 Addendum - Readback And Fault Payload Sweep

- Non-editor scan found `WaitForCompletion` in `GpuScatterLodManager.cs:1725`, `SargassumMicroFaunaBoids.cs:8544`, and `GPUScatterDirector.cs:2303`. These are release blockers until proven teardown/fault-only or converted to deferred async readback.
- `FoveatedRenderCommander.cs:1208` / `:1261` uses a transient `Allocator.TempJob` payload for black-box dump output. Static context suggests a fault-dump path, not normal render cadence, but proof is still required.
- Rendering proof now explicitly needs a "no blocking readback in healthy gameplay" capture, not only RenderGraph pass visibility.

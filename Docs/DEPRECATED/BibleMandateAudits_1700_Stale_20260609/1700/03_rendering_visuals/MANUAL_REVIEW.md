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

## Pass 8 Addendum - VFX Runtime Asset Lifecycle

- `HectonVolumetricParticulateFogFeature` uses `RecordRenderGraph`, but also has a synchronous mock light job route and creates fallback 3D density/flow textures. Static review cannot call this wrong, but it requires RenderGraph Viewer, Frame Debugger, GPU profiler, and one-time lifecycle proof.
- `HectonVisorARStencilRendererFeature` creates a fallback stencil mesh. Production visor masking needs authored/default mesh proof or proof that this fallback is a one-time cold safety route and not normal art content.
- `HectonMarineSnowRenderer`, `NativeTrailRenderer`, `ShinobuPlasmaBeamRuntime`, and `CarveDebrisComputeRenderer` contain runtime mesh/material/render texture creation routes. Dynamic trails and RTs can be valid, but release proof needs fixed counts, no repeated creation under enable/disable/reload, compact/high GPU captures, and no fallback low-poly debris geometry as the production visual route.

## Pass 9 Addendum - RenderFeature Material Lifecycle

- Biolum SSGI, scooter shafts, sonar point cloud, volumetric light, and particulate fog all use `RecordRenderGraph`; static review does not see a legacy `Execute()` render pass violation in this cluster.
- The same features create/recreate render materials through `CoreUtils.CreateEngineMaterial` in `Create()` or proxy setup. This is legal only if creation is one-time per renderer/quality load and not repeated through hot-swap or gameplay quality changes.
- Several shader fallbacks use `Shader.Find` under editor/development guards. Release closure still needs assigned shader assets and shader variant collection proof; guarded fallback does not prove production assignment.
- Hot-swap listener registration is the right dependency shape, but it now needs event-count proof so service replacement cannot become a hidden material/resource recreation loop.

## Pass 11 Addendum - Scatter And Ocean Readback Detail

- `GPUScatterDirector` owns persistent GPU resources, but `EnsureInstanceBufferCapacity()`, `EnsureVisibleIndexBufferCapacity()`, and `EnsureVisibilityCacheBufferCapacity()` release/recreate buffers when required capacity grows. Closure requires proof that capacity stabilizes before gameplay stress or grows only during explicit quality/residency changes.
- `GPUScatterDirector.TryRefreshBiomeHeatmapTextureHot()` writes a 256x256 R8 texture using `SetPixelData()` and `Apply(false, false)` when Data Monolith byte length/checksum changes. That can be valid, but release proof must show it is not a recurring gameplay-cadence upload.
- `GPUScatterDirector` uses async visible-count readback into persistent native data, but `CompletePendingVisibleCountReadbackForRelease()` calls `WaitForCompletion()`. Required proof: teardown/fault-only callsite, no healthy-frame blocking wait.
- `ShinobuOceanSurfaceAtmosphereRuntime` uses triple-buffered query/result graphics buffers and async wave-height readback into persistent native arrays. Static shape is good; compact/high proof still needs GPU cost, readback latency, and waterline readability captures.

## Pass 12 Addendum - TBDR, Construction Preview, Ambient Biota, And Diagnostics

- `TBDRPipelineSurgeonRuntime` and `TBDRPipelineSurgeonTypes` route through DataVault when available, but fall back to persistent mock/native buffers and emergency mock limits. Rendering acceptance now needs DataVault readiness proof or release-player exclusion of the mock fallback route.
- `FoundationPylonGpuBatch`, `HectonBlueprintPreviewBatch`, and `VRPipeBlueprintPreview` use double-buffered GPU upload paths, which is a better shape than per-object rendering. They still create preview/pylon materials at runtime when no material is assigned.
- `AmbientBiotaDirector` has an indirect-draw route with dirty uploads, but clones an owner-local material and can create a fallback quad mesh. This needs SRP/material instance count, fixed buffer count, and authored mesh/material assignment proof.
- `ArchitectEyeVisualizer` creates a runtime quad mesh, material, glyph atlas resources, and graphics buffers for diagnostic visualization. This must be debug/diagnostic policy, not normal release presentation.

## Pass 13 Addendum - Voxel Fade Pool And DataVault Readiness

- `HectonVoxelStreamingBridge` fade presentation clones a fixed pool of up to 32 materials and drives `_ChunkDissolveFade` through runtime material instances during late-frame fade. This can be valid visual-fake presentation, but it still needs fixed clone count, SRP/material instance proof, and no normal-gameplay hot-swap recreation.
- Rendering/TBDR readiness remains tied to DataVault boot proof. `GlobalDataVault` can grow/relocate arena state and macro payload caches, so render systems that depend on DataVault handles are not release-green until boot and soak counters show no fallback/mock route in production play.

## Pass 16 Addendum - Buoyancy And Thermodynamics Visual Uploads

- `AsyncBuoyancyReadbackRuntime` has the right deferred shape: dispatch in visual sync, consume completed requests with `SystemDispatcher.IsAsyncReadbackReadyNoWait(...)`, and reserve `AsyncGPUReadback.WaitAllRequests()` for release/teardown. GPU proof is still required because request/wave buffers are created during enable, readback arrays are persistent on first dispatch, and whole-buffer uploads/readbacks can stall compact hardware if cadence or capacity is wrong.
- `AbyssalThermodynamicsSolver.ReactorBridge` uploads reactor visual data through double-buffered `GraphicsBuffer.LockBufferForWrite(...)` and sets global shader vectors/buffers. That can be a good visual-fake path, but it needs fixed buffer count proof, upload-byte proof, SRP/shader consumption proof, and compact/high captures.
- `ThermodynamicsHazardGridRuntime.UploadVisualTextureIfDirty()` creates/rebuilds a runtime `Texture3D`, calls `SetPixelData()`, and applies it to a global heat texture. The quality-scaled upload stride is a valid mitigation, not closure. Required proof: no repeated texture rebuilds after bootstrap/resolution stabilization, measured `Texture3D.Apply` cost, and visual readability at low resolution.

## Line-Level Classification Addendum

- All 109 static runtime suspect lines for this group are now classified in `LINE_LEVEL_CLASSIFICATION.md`.
- Classification totals: 68 editor/development guarded, 39 cold/setup/fault/owner-lifetime, 1 false positive, and 1 registered runtime violation.
- The registered violation is `CarveDebrisComputeRenderer.BuildOctahedronMesh()` using `mesh.RecalculateNormals()` for fallback octahedron debris geometry. This remains covered by `RB-123`; it is acceptable only if the release route uses authored/default debris meshes or the fallback is excluded from production presentation.
- This addendum does not provide RenderGraph, Frame Debugger, GPU profiler, Memory Profiler, player-build, or device proof.

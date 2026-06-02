# CSharpComputeDispatchAuditor Log

## 2026-05-26 Static Compute Dispatch Audit

What was wrong: Direct compute dispatch sites exist with mixed sizing discipline. Some first-party files query `GetKernelThreadGroupSizes`; others still use fixed constants, hardcoded `/8`, or `+63 >> 6`.

What was done: Static audit only. Used `rg --no-ignore` and file-local parsing under `Assets` and `Assets/_Project`. No source edits. No dotnet build. No Unity import. No profiler.

Cinematic cheats used: None. This was an audit, not simulation work.

Exact microseconds saved: 0 measured. Any savings from future fixes are PENDING GPU CAPTURE / RenderDoc / Unity Profiler.

Mandates used: `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `GPU_Compute_Warp_Sizing_Mobile`, `REND_GPU_Sovereignty`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `REND_DescriptorBinding_Reality_Check`.

Counts:
- All `Assets` direct `.Dispatch(` calls with `--no-ignore`: 90.
- First-party `Assets/_Project` direct `.Dispatch(` calls: 62 across 26 files.
- Non-project/vendor direct `.Dispatch(` calls: 28 across 7 files.
- First-party dispatch files with same-file `GetKernelThreadGroupSizes`: 15.
- First-party dispatch files without same-file `GetKernelThreadGroupSizes`: 11.

First-party files without same-file `GetKernelThreadGroupSizes`:
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` - 2 dispatches; hardcoded `(sourceCount + 63) >> 6`.
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` - 1 dispatch; `PhantomDroneThreadGroupSize` constant.
- `Assets/_Project/Scripts/HectonCelestialEngine.cs` - 3 dispatches; `ComputeThreadGroup8Inv` / `FirmamentStarBakeThreadsInv`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs` - 5 dispatches; `textureGroupCount` and `GpuThreadGroupSize/GpuThreadGroupShift`.
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` - 1 dispatch; fixed `1,1,1`.
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs` - 1 dispatch; `WakeTrailThreadGroupSize`.
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs` - 5 dispatches; `ThreadGroupSize`, `/8` depth pyramid groups.
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` - 7 dispatches; `ThreadsPerGroup`, `/8` depth pyramid groups.
- `Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs` - wrapper `culling.Dispatch`, not a direct `ComputeShader.Dispatch`.
- `Assets/_Project/Scripts/World/SargassumCrestDampingController.cs` - 1 dispatch; `FacadeThreadGroupSize`.
- `Assets/_Project/Scripts/World/SargassumCutManager.cs` - 2 dispatches; `StampThreadGroupSize` / `DamageVolumeThreadGroupSize`.

Current notable first-party direct dispatch details:
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs`: `TryDispatchVisibilityCulling`, lines 3525/3547: `compute.Dispatch(cullingParams.ClearArgsKernel, 1, 1, 1);` and `compute.Dispatch(cullingParams.CullKernel, math.max(1, groups), 1, 1);`. Evidence: `int groups = (sourceCount + 63) >> 6;`. No same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`: `DispatchWaveHeightReadback`, line 1324: `waveHeightSamplerCompute.Dispatch(_waveSamplerKernel, groupCount, 1, 1);`. Evidence: `_waveSamplerThreadGroupSize`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`: `RenderPhantomSwarm`, line 6435: `s_PhantomDronesCompute.Dispatch(s_PhantomDroneKernel, (PhantomDroneCount + PhantomDroneThreadGroupSize - 1) / PhantomDroneThreadGroupSize, 1, 1);`. No same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`: `Dispatch`, line 255: `_activeComputeShader.Dispatch(_kernel, dispatchGroups, 1, 1);`. Evidence: `_threadGroupSize`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs`: `TryDispatch`, line 1034: `shader.Dispatch(kernel, LastDispatchGroupsX, LastDispatchGroupsY, LastDispatchGroupsZ);`. Evidence: `DivCeil(workItems*, group*)`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/HectonBoidController.cs`: `RunBoidVisualSync`, `DispatchSpatialGridBuild`, `DispatchComputeFrustumCulling`, lines 623/1335/1336/1370/1371. Uses `_dispatchGroupCount` and `_clearSpatialGridGroupCount`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`: `TryBakeFirmamentOnce`, lines 3412/3417/3434. Evidence: `ComputeThreadGroup8Inv`, `FirmamentStarBakeThreadsInv`. No same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs`: `TryDispatchGpuAbyssalFlowField`, `DispatchAbyssalVortexImpulses`, `TryDispatchGpuBuoyancySampling`, lines 7160/7167/7177/7302/7742. Evidence: `textureGroupCount`, `GpuThreadGroupSize`, `GpuThreadGroupShift`. No same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`: `UpdateHudFogLuminanceDownsample`, `UpdateFlashlightPhotophobiaField`, lines 5558/5772. Same-file `GetKernelThreadGroupSizes`; photophobia still uses `FlashlightPhotophobiaFieldResolution / 8` near dispatch.
- `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs`: `DispatchGpuReadback`, line 579. Evidence: `_threadGroupSize`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs`: `DispatchCull`, line 1330. Evidence: `_dispatchThreadGroupSizeX`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`: `DispatchLeakPlumeCompute`, line 1731: `leakPlumeCompute.Dispatch(_leakPlumeKernelIndex, 1, 1, 1);`. No same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/UI/PDAMapTab.cs`: lines 1092/1099. Evidence: `CeilDividePositive(dispatchWordCount, _sonarBuildMapPointsThreadGroupSizeX)`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs`: `DispatchDirtyScreens`, line 2028: `terminalBlitCompute.Dispatch(_blitKernel, _groupsX, _groupsY, dirtyCount);`. Same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `UploadSonarTapsAndDispatchRadar`, `DispatchDamageHologramCompute`, lines 2017/2385. Evidence: `CeilDividePositive(..., _radarThreadGroupSizeX/_damageHologramThreadGroupSizeX)`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`: `DispatchGpu`, lines 1046/1052/1057. Evidence: `ResolveDispatchGroups(activeCapacity, _threadGroupSize)`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`: `DispatchVisibleClear`, `DispatchParticleKernelChunked`, `DispatchClearKernelChunked`, lines 3520/3727/3755. Evidence: `threadGroupSize`, `MaxParticleDispatchGroupsPerCall`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`: `FlushSmokeVisualSync`, line 3963. Same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs`: `DispatchVolumeKernel`, line 607. Evidence: cached thread group sizes and `ResolveDispatchCount`; same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`: `ExecuteWakeTrailSimulation`, line 8087. Evidence: `WakeTrailThreadGroupSize`; no same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`: `LateFrameTick` and `BuildDepthPyramid`, lines 551/606/615/1444/1456. Evidence: `ThreadGroupSize`, `(_depthPyramidWidth + 7) / 8`; no same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: culling/snap/depth-pyramid dispatches, lines 2540/2567/2602/2620/2675/2687/3460. Evidence: `ThreadsPerGroup`, `(_depthPyramidWidth + 7) / 8`; no same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/SargassumCrestDampingController.cs`: `RefreshFacadeTextures`, line 376. Evidence: `FacadeThreadGroupSize`; no same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/SargassumCutManager.cs`: `ProcessQueuedMaskUpdate`, `ProcessQueuedDamageVolumeUpdate`, lines 1561/1753. Evidence: `StampThreadGroupSize`, `DamageVolumeThreadGroupSize`; no same-file `GetKernelThreadGroupSizes`.
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`: `RunMicroFaunaVisualSync`, clear kernels, origin-shift kernels, lines 2182/2183/2186/6884/6928/6936/8278/8281. Evidence: `_dispatchGroupCount`, `_clearSpatialGridDispatchGroupCount`, cached `_threadGroupSizeX`; same-file `GetKernelThreadGroupSizes`.

Non-project/vendor dispatch files:
- `Assets/Crest/Crest/Scripts/Collision/QueryBase.cs`: line 495; no same-file `GetKernelThreadGroupSizes`; `SetData` visible in `ExecuteQueries` line 490.
- `Assets/Crest/Crest/Scripts/Helpers/TextureArrayHelpers.cs`: line 57; no same-file `GetKernelThreadGroupSizes`; divides by `LodDataMgr.THREAD_GROUP_SIZE_X/Y`.
- `Assets/Editor/x64/Bakery/scripts/ftBuildGraphics.cs`: lines 3003/3017; editor/vendor; no same-file `GetKernelThreadGroupSizes`.
- `Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs`: line 637; no same-file `GetKernelThreadGroupSizes`; uses `GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT`.
- `Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs`: 20 dispatches; no same-file `GetKernelThreadGroupSizes`; uses `GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT` and `_2D`.
- `Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs`: lines 378/584; no same-file `GetKernelThreadGroupSizes`; uses `GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT/_2D`.
- `Assets/GPUInstancer/Scripts/GPUInstancerTreeManager.cs`: line 264; no same-file `GetKernelThreadGroupSizes`; uses `GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT`.

Hot-like upload/binding findings:
- First-party hot-like `.SetData(`: none detected by static nearest-method scan.
- Vendor hot-like `.SetData(`: `Assets/Crest/Crest/Scripts/Collision/QueryBase.cs:490` in `ExecuteQueries`.
- First-party hot-like `.SetBuffer(` is visible in dispatch/render methods across: `ShinobuOceanSurfaceAtmosphereRuntime`, `DroneFleetManager`, `InstanceCullingService`, `HectonBoidController`, `HectonFluidEngine`, `AsyncBuoyancyReadbackRuntime`, `GpuScatterLodManager`, `SubmarineStructuralGrid`, `TerminalOsRuntime`, `VehicleSubOsCockpitRuntime`, `CarveDebrisComputeRenderer`, `HectonMarineSnowRenderer`, `FloraInteractionManager`, `GPUScatterDirector`, `HectonOctahedralImpostorRenderer`, `SargassumCutManager`, and `SargassumMicroFaunaBoids`.

Regression model: CPU/GC unchanged by this audit. Runtime risk remains in files without queried thread-group sizes and in vendor `SetData` path. Correctness risk: static parser is approximate for method names; file paths and line-level dispatch expressions were verified by current file scan. `CommandBuffer.DispatchCompute` was out of scope.

Verification state: STATIC ONLY / PENDING UNITY VERIFICATION.

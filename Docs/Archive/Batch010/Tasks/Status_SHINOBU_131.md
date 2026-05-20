# SHINOBU_131 Status

Agent: SHINOBU_131
Domain: LIGHTING_PROBE_GRID_ARCHITECT
Prompt task count: 20
Status: STATIC SOURCE VERIFIED / UNITY COMPILE AND RUNTIME PROFILER PROOF PENDING

## Mandates Read

- `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `REND_DescriptorBinding_Reality_Check.txt`
- `Docs/Tasks/POLISH.txt`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`

## Task Checklist

- [x] Task 01: UNITY_LIGHT_PROBE_ERADICATION | DOD: static scan over `Assets/_Project` found zero `LightProbeGroup`, `m_LightProbeUsage: 1`, managed probe calls, or managed SH writes after the replacement route. Rejected retaining Unity scene-baked probes because streaming bake state is not owner-local. Estimate: 80-250 us main-thread streaming spike avoided per moving-probe sample cluster; profiler proof pending.
- [x] Task 02: MANAGED_SH_EVALUATION_PURGE | DOD: `LightProbes.GetInterpolatedProbe`, `SphericalHarmonicsL2`, and `RenderSettings.ambientProbe` are absent in the `_Project` C#/YAML scan. Rejected managed `SphericalHarmonicsL2` as a CPU-side API boundary. Estimate: 15-60 us per 1k entities avoided.
- [x] Task 03: CS1612_ENCAPSULATION_PURGE | DOD: `CustomLightProbeDTO`, source DTOs, telemetry DTOs, and ambient profile DTOs expose public fields only. Rejected hot DTO properties and interface arrays. Estimate: avoids defensive struct-copy churn in every probe interpolation lane.
- [x] Task 04: ARM64_PADDING_RECONSTRUCTION | DOD: `CustomLightProbeDTO` is explicit 128 bytes, offsets validated by `CustomLightProbeLayoutAudit`. Rejected impossible `double3 + 27 floats` in 128 bytes; chose spatial hash header plus packed `float4` coefficient lanes. Estimate: stable 2-cache-line stride, no unaligned trap.
- [x] Task 05: EMERGENCY_MOCK_PROBE_DATA | DOD: `GenerateMockProbeGridJob` seeds deterministic top-to-depth gradient and caustic fake into front/back Vault buffers. Rejected terrain/SDF dependency for profiling bootstrap. Estimate: isolates probe solver from missing environment data.
- [x] Task 06: BURST_SH_INTERPOLATION_KERNEL | DOD: `EvaluateProbeLightingJob` is `IJobParallelFor`, `[NoAlias]`, Burst synchronous/fast/standard, AUP-relative, 8-cell trilinear at quality, nearest/fallback collapse under thermal pressure. Rejected main-thread entity SH sampling. Estimate: O(entity) SIMD-friendly pass.
- [x] Task 07: SDF_OCCLUSION_BAKING | DOD: `UpdateProbeOcclusionJob` is scheduled after propagation, consumes `InteriorGIOcclusionCellDTO`, applies packed biome tint and SDF darkening, and preserves finite guards. Rejected raytracing, `Physics.Raycast`, and duplicate float SDF buffers. Estimate: O(probe) scalar field pass instead of O(probe * rays).
- [x] Task 08: THE_DEAR_LIE_DYNAMIC_BOUNCE | DOD: `InjectDynamicLightJob` adds nearest-8 directional SH boosts with decay and finite clamps. Rejected radiosity/light transport simulation. Estimate: 8 writes per significant light event.
- [x] Task 09: ASYNCHRONOUS_GPU_UPLOAD_DISPATCHER | DOD: double `GraphicsBuffer`, `LockBufferForWrite<CustomLightProbeDTO>`, Burst `UnsafeUtility.MemCpy` upload copy, deferred next-frame global buffer bind `_H8CustomLightProbeGrid`, boot-prewarmed max capacity, and `Hecton_CustomLightProbeGrid.hlsl` consumers wired into direct project shader ambient. Rejected `Texture3D`, half-texture scratch writes, same-frame upload/bind, Unity shader `SampleSH` ambient, and managed staging upload. Estimate: no per-frame managed array upload, no quality-driven GPU buffer churn, and no same-frame GPU publication hazard.
- [x] Task 10: CONTINUOUS_SCALABILITY_PROBE_DENSITY | DOD: `GlobalQualityWeight` drives resolution, cell decimation, cadence, source sample limit, propagation iterations, L1/L2 weights, and upload cadence through `math.lerp`, `math.step`, and smooth curves. Rejected low/high binary switches. Estimate: 32^3 to 12^3 active probe collapse at low weight.
- [x] Task 11: GLOBAL_DIRECTIONAL_FALLBACK | DOD: `EvaluateProbeLightingJob` blends to `GlobalFallback` and returns O(1) when quality collapses. Rejected forced trilinear lookup during thermal emergency. Estimate: 8 probe reads reduced to 1 DTO write.
- [x] Task 12: BIOME_TINT_INTEGRATION | DOD: no direct World assembly reference; `BiomeGradientSignal` contract and ambient profile Vault table drive a packed RGB10 biome tint consumed by occlusion/propagation jobs. Rejected concrete `BiomeTransitionManagerRuntime`/`CurrentAtmosphereDTO` dependency because it would break the compile wall. Estimate: one packed scalar in tuning, no sibling assembly route.
- [x] Task 13: AUP_PRECISION_GRID_MAPPING | DOD: source, light, and entity mapping subtract `Tuning.RootAup` before casting to `float3`; light shaft helper now uses Core double3 AUP without `Hecton8.World`. Rejected absolute world floats. Estimate: prevents map-edge SH cell drift.
- [x] Task 14: ROLLBACK_NETCODE_STATE_FENCE | DOD: probe grid is presentation-only Vault/GPU state, not registered into rollback state hash or Merkle state ring. Rejected gameplay ownership of visual GI. Estimate: zero rollback copy/hash cost.
- [x] Task 15: ZERO_INIT_OVERHEAD_BYPASS | DOD: Vault handles use `NativeArrayOptions.UninitializedMemory`; `InteriorGIClearStateJob` owns scheduled boot clearing and chains mock-grid generation without a cold `Complete()`. Rejected `GlobalDataVault.Create` fallback and local persistent arrays. Estimate: boot clear is explicit Burst work, no hidden allocator zero-fill assumption or first-tick clear fence.
- [x] Task 16: TELEMETRY_LIGHTING_RECORDER | DOD: 300-entry `InteriorGITelemetryEntry` ring in Vault records probe count, source count, quality, timing, luma, NaNs, hash, and dump path `Docs/AgentLogs/Dump_LIGHTING_SURGEON.bin`. Rejected chat-only fault state. Estimate: 64 * 300 = 19.2 KB blackbox.
- [x] Task 17: PROBE_TUNER_EDITOR_WINDOW | DOD: `AbyssalLightingTunerWindow` UI Toolkit facade exposes quality, emergency, propagation, wall/water absorption, mock grid, CSV reload, ambient CSV reload, dump, Unity probe scan/disable, and fixed-buffer telemetry graph over `SolverCompleteMs`. Rejected runtime UI coupling. Estimate: 0 us player hot path.
- [x] Task 18: CSV_AMBIENT_PROFILES_INGESTOR | DOD: `AmbientLightingProfileCsvParser` tokenizes `ReadOnlySpan<byte>` backed by the Vault CSV scratch into `AmbientLightingProfileDTO` rows with FNV-1a IDs and no `string.Split`/LINQ/foreach. Rejected ScriptableObject/managed dictionary runtime path. Estimate: cold-only parser.
- [x] Task 19: LIVE_PROBE_DEBUG_GIZMO | DOD: editor gizmo evaluates SH in fixed forward direction and draws colored spheres from the Vault readback. Rejected raw L0-only debug. Estimate: editor-only.
- [x] Task 20: SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static source gates pass; architecture ledger and final log contain byte layout, Vault IDs, aliasing, dependency graph, compile-guard, and Dear Lie proof. `dotnet build` intentionally not run per explicit user instruction.

## Iteration Loop

- Loop 0: Prompt extracted with `Select-String`; mandates selected; code untouched.
- Loop 1: Tasks 01-05 implemented and audited. DOD practice: static archaeology, explicit DTO layout, mock data path. Rejected Unity LightProbeGroup and impossible `double3 + 27 floats`/128B layout. Estimate: removes managed probe sampling spikes; compile not run by user instruction.
- Loop 2: Tasks 06-10 implemented and audited. DOD practice: Burst jobs with `[NoAlias]`, continuous quality curves, GPU buffer upload. Rejected main-thread SH sampling and managed texture staging. Estimate: 8-probe lookup scales down to nearest/fallback at low quality.
- Loop 3: Tasks 11-15 implemented and audited. DOD practice: O(1) fallback, AUP-relative mapping, rollback fence, Vault-only buffers. Rejected local persistent arrays and standalone DataVault fallback. Estimate: no rollback hash/copy cost for presentation GI.
- Loop 4: Tasks 16-20 implemented and audited. DOD practice: fixed 300-frame blackbox, UI Toolkit facade, byte CSV parser, editor gizmos, self-audit. Rejected chat-only reporting and runtime-managed designer data.
- Loop 5: Polish pass found two residual `using Hecton8.World` in Lighting shaft files and missing owner-local biome tint consumption in probe propagation. Patched both through Core AUP double3 and `BiomeGradientSignal`/RGB10 packed tuning; re-ran static gates.
- Loop 6: Polish pass found obsolete half-texture upload scratch still written by propagation after the direct SH DTO GraphicsBuffer path existed. Removed `InteriorGITextureVoxelDTO`, Vault buffer `0x630807` request, and the per-probe scratch write; GPU buffers now prewarm/grow-only at `MaxCellCount`.
- Loop 7: Polish pass found `UpdateProbeOcclusionJob` was dead code. Rewired it to `InteriorGIOcclusionCellDTO` and scheduled it after propagation before telemetry; removed duplicate biome tint application from propagation.
- Loop 8: Polish pass tightened ambient CSV ingestion from direct NativeArray indexing to a `ReadOnlySpan<byte>` parser with an unsafe Vault-scratch wrapper, preserving zero-GC cold parsing while matching the XML requirement.
- Loop 9: Polish pass found the GPU upload had no shader-side consumer contract and the editor facade graph was incomplete. Added `Hecton_CustomLightProbeGrid.hlsl`, replaced direct project shader `SampleSH`/`SampleSHPixel` ambient with quality-scaled custom grid resolve, sent runtime root separately from AUP residue, added a fixed-buffer compute-time graph, and removed a duplicate MethodImpl compile-risk.
- Loop 10: Polish pass found the upload dispatcher still scheduled a copy and immediately completed/bound the write buffer, and resolution changes still used a blocking full clear from `Tick`. Reworked upload into a pending-state machine that completes only after `IsCompleted`, unlocks, and publishes no earlier than the next frame; reworked resolution clears into `InteriorGIProbeGridClearJob` scheduled through the normal simulation handle.
- Loop 11: Polish pass found scheduled resolution clears could still upload old grid constants because `BuildTuning` ran after the clear branch. `Tick` now resolves biome tint, cadence, and new `InteriorGITuningDTO` before `ScheduleGridClear`, then primes the upload accumulator so the cleared grid publishes current resolution/count constants after the clear job completes.
- Loop 12: Re-read `Docs/Tasks/POLISH.txt` and `Docs/PROJECT_STATE_STATIC_XRAY.md`; confirmed this lane stays static-source only, keeps global authority owner-local, and does not claim Unity import/runtime/profiler proof.
- Loop 13: Polish pass found `LateFrameTick` could start a GPU upload while a simulation handle was still running. Because even propagation iteration counts can write the final buffer back into `_probeFront`, this was a real read/write race. Late-frame upload now waits until no simulation is active or the active handle has completed.
- Loop 14: Re-extracted the SHINOBU_131 block from `CURRENT_BATCH.md`; Task 09 still explicitly requires `LockBufferForWrite`, Burst `UnsafeUtility.MemCpy`, double buffering, and subsequent-frame binding. Current code matches that static contract without launching a build.
- Loop 15: Polish pass removed the cold boot `InteriorGIClearStateJob.Complete()` fence. Boot clear now schedules through `_simulationHandle`, optionally chains `GenerateMockProbeGridJob`, blocks readback while `_scheduledBootClear` is true, and publishes only after LateFrame reclamation.
- Loop 16: Polish pass found editor CSV polling could run immediately after `EnsureNativeState` while scheduled boot clear still owns the Vault scratch/profile buffers. `SlowTick` now exits while native boot clear or any simulation handle is active.

## Verification

- `rg` scan over `Assets/_Project` for `m_LightProbeUsage: 1`, `LightProbeGroup`, `LightProbes.GetInterpolatedProbe`, `SphericalHarmonicsL2`, `RenderSettings.ambientProbe`: `0`.
- Lighting domain scan for direct sibling `using Hecton8.World|Gameplay|Environment|AI|Physics|Audio|Ecosystem|Vehicles|Habitat|Combat`: `0`.
- `Hecton8.Lighting.asmdef` references only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, Burst/Collections/Mathematics, and URP/Core RP packages; no sibling runtime asmdef reference.
- Lighting domain scan for `StructLayout(... Pack=`, hot `new NativeArray<`, `Allocator.Persistent`, LINQ, `foreach`, `string.Format`, `UnityEngine.Random`, `Texture3D`, obsolete upload DTOs, `GlobalDataVault.Create`: `0`.
- Lighting Burst attribute regex gate: `0` offenders.
- Shader-side probe-grid check: `Hecton_CustomLightProbeGrid.hlsl` declares `_H8CustomLightProbeGrid`; direct project shader ambient calls now route through `H8CustomLightProbeResolveAmbient`; `SampleSH(`, `SampleSHPixel(`, and raw `unity_SH*` references are absent from `_Project` shader/HLSL files after this pass.
- Shader target gate: every shader pass that includes `Hecton_CustomLightProbeGrid.hlsl` or calls `H8CustomLightProbeResolveAmbient` resolves to `#pragma target 4.5` or higher; `grid_shader_target_bad=0`.
- GPU/dependency audit: `CustomLightProbeGpuUploadJob` is a single Burst `IJob` using `UnsafeUtility.MemCpy`; `TryStartGpuUploadIfDirty` schedules only one pending mapped upload, `TryPublishCompletedGpuUpload` publishes only after `IsCompleted` and a later frame, and `ScheduleGridClear` routes dynamic clear work through `InteriorGIProbeGridClearJob` instead of a Tick-path blocking boot clear.
- Resolution-change audit: `Tick` stores a fresh `InteriorGITuningDTO` before scheduling `InteriorGIProbeGridClearJob`, preventing old resolution/active-count shader constants from being uploaded after a clear.
- Race audit: `LateFrameTick` no longer calls `TryStartGpuUploadIfDirty` while `_simulationJobActive && !_simulationHandle.IsCompleted`, preventing GPU copy reads from `_probeFront` while propagation may be writing `_probeFront`.
- Remaining `Complete()` calls are classified: completed simulation reclamation in `LateFrameTick`, editor/manual mock generation, teardown drain, and post-`IsCompleted` GPU unlock/publish. The previous cold boot clear `Complete()` was removed.
- `git diff --check` on edited SHINOBU_131 source/doc files: pass with line-ending warnings only.
- Unity import, Burst compile, Play Mode, Profiler, Frame Debugger, and GPU visual proof: pending. `dotnet build` was not launched because the user explicitly prohibited it until necessary.

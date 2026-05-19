# Status_SHINOBU_120

Agent: SHINOBU_120
Domain: Echelon 7 Atmosphere & Celestial / Domains 66-67 Marine Snow, Silt, Volumetric Fog
Prompt source: Docs/Tasks/CURRENT_BATCH.md, extracted by SHINOBU_120 tag
Task count: 20
Created: 2026-05-19
Last update: 2026-05-19

## Mandates Read
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Execution_Phases.txt
- ARCH_Signal_Lane_Segregation.txt

## Current Loop
Loop 19/5 runtime DTO boundary sanitization and camera-local shader noise active. A guarded narrow Core build was attempted earlier only after CPU sampled below the 50 percent threshold and no `dotnet`/`csc` process was active. The build failed before SHINOBU validation on a non-SHINOBU dependency: `Hecton8.Core.csproj` includes missing `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`. That file is absent on disk and not SHINOBU-owned. No runtime-complete claim is authorized.

## Iterative Loops
- Loop 1: Tasks 01-05. Registry/domain read, prefab/fog scans, DTO/contracts/job implementation. Compile skipped by guard. Prompt re-extracted after block with SHINOBU_120 regex.
- Loop 2: Tasks 06-10. Compute raymarch, low-end proxy, abyssal flow, MarineSnow density consume, persistent RTHandle bilateral composite. Static shader/code inspection only.
- Loop 3: Tasks 11-14. Quality mapping 4..64, biome bridge, AUP local wrap, rollback isolation scan. Prompt re-extracted by SHINOBU_120 tag.
- Loop 4: Tasks 15-18. Uninitialized vault buffers, LockBufferForWrite upload, telemetry ring/dump path, UI Toolkit tuner, ReadOnlySpan CSV parser.
- Loop 5: Tasks 19-20. Heatmap path, renderer validator wiring, path-limited diff/allocation scan, final log/self-audit.

## Checklist
- [x] 01 Particle-system eradication for ambient MarineSnow/SiltDust/DeepSeaParticles. DOD: exact prefab/scene scan found no target-named Shuriken assets; unrelated hazard/construction ParticleSystems left intact. Rejected: deleting unrelated third-party or construction VFX. Microseconds: PENDING PROFILER.
- [x] 02 Purge standard/black-box fog dependency. DOD: renderer validator now injects HectonVolumetricParticulateFogFeature; scanned scenes keep postprocessing disabled and no target standard fog stack was used for this feature. Rejected: RenderSettings/URP black-box fog. Microseconds: PENDING PROFILER.
- [x] 03 DTO/vault-owned fog state with no encapsulated hot-path properties. DOD: VolumetricFogParamsDTO is explicit vault DTO with raw fields and ref access helpers. Rejected: MonoBehaviour/property graph hot path. Microseconds: PENDING PROFILER.
- [x] 04 Validate VolumetricFogParamsDTO layout and offsets. DOD: VolumetricFogNativeLayout.Validate checks 64B size and offsets 0/16/32/48 via UnsafeUtility size plus Marshal offsets. Rejected: trusting C# default packing. Microseconds: PENDING PROFILER.
- [x] 05 Burst mock volumetric lights into vault NativeArray<PointLightDTO>. DOD: BuildMockVolumetricLightsJob writes deterministic fixed-capacity PointLightDTO entries. Rejected: managed per-frame light list allocation. Microseconds: PENDING PROFILER.
- [x] 06 Raymarch shader and RenderGraph integration. DOD: Hecton_VolumetricFog.compute and HectonVolumetricParticulateFogFeature add two compute RenderGraph passes. Rejected: CommandBuffer.Blit/temporary RT path. Microseconds: PENDING PROFILER.
- [x] 07 Low-end dithered proxy mode. DOD: proxyBlend from GlobalQualityWeight bypasses the raymarch loop at survival quality and writes deterministic dithered analytic fog. Rejected: binary quality tier switch. Microseconds: PENDING PROFILER.
- [x] 08 Abyssal flow advection. DOD: shader samples _AbyssalFlowFieldTexture and advects wrapped local fog noise by flow strength. Rejected: CPU fluid/proton simulation. Microseconds: PENDING PROFILER.
- [x] 09 Submarine wake turbulence through persistent SiltBuffer. DOD: consumes existing HectonMarineSnow fog-density texture populated from wake turbulence instead of owning another wake solver. Rejected: direct submarine dependency or duplicate 3D fluid owner. Microseconds: PENDING PROFILER.
- [x] 10 Half/quarter resolution plus bilateral upsample. DOD: persistent reduced/full RTHandles, quantized dimensions, 3x3 depth-aware compute composite. Rejected: per-frame RenderTexture.GetTemporary. Microseconds: PENDING PROFILER.
- [x] 11 Continuous ray steps 4..64 and opacity early break. DOD: ResolveRaySteps maps GlobalQualityWeight smoothly to 4..64; shader exits on opacity threshold. Rejected: low/ultra dichotomy. Microseconds: PENDING PROFILER.
- [x] 12 Biome-specific extinction profiles. DOD: vault-loaded extinction profiles and depth-band blending feed color/density/extinction; direct BiomeMatrix access was removed pending an unmanaged biome contract. Rejected: FindObjectOfType polling, hard-coded biome branches, and sibling-domain concrete reads. Microseconds: PENDING PROFILER.
- [x] 13 AUP precision noise wrapping. DOD: C# sends 256m wrapped float3 noise offset; shader never receives absolute double3. Rejected: world-coordinate noise at large AUP. Microseconds: PENDING PROFILER.
- [x] 14 Rollback/netcode isolation. DOD: scan found new BufferIDs only in render/editor/H8Memory paths; no rollback/Merkle/state-authority wiring. Rejected: simulation authority from visual fog. Microseconds: PENDING PROFILER.
- [x] 15 Zero-init overhead bypass and persistent GPU buffers. DOD: vault buffers use UninitializedMemory; constant/structured GPU uploads use LockBufferForWrite; RTHandles/GraphicsBuffers are persistent cold allocations. Rejected: SetData/GetData/frame temp allocation. Microseconds: PENDING PROFILER.
- [x] 16 300-entry telemetry ring and surge dump. DOD: NativeArray telemetry ring capacity 300; dump streams native telemetry bytes directly to Docs/AgentLogs/Dump_VOLUMETRIC_SURGEON.bin on NaN or estimated >2ms without managed `byte[]` staging. Rejected: no black-box state and managed dump copy. Microseconds: PENDING PROFILER; current value is estimate, not GPU query.
- [x] 17 UI Toolkit Abyssal Atmosphere Tuner. DOD: editor window mutates vault DTO, forces continuous GlobalQualityWeight, loads CSV, draws telemetry graph. Rejected: IMGUI-only debug panel. Microseconds: PENDING PROFILER; graph uses estimated telemetry pending GPU query.
- [x] 18 Allocation-free CSV parser for water_extinction_profiles.csv. DOD: ReadOnlySpan<byte> parser avoids string.Split, writes WaterExtinctionProfileDTO values directly into the vault profile array, and editor file bytes stream into vault-owned CsvScratch instead of `byte[]`/NativeHashMap temp staging. Rejected: managed Split/float.Parse allocation chain and temp NativeHashMap/GetValueArray copy. Microseconds: PENDING PROFILER.
- [x] 19 Live raymarch debug heatmap. DOD: debugHeatmapWeight blends executed-step heatmap in shader and is exposed in renderer settings. Rejected: CPU readback heatmap. Microseconds: PENDING PROFILER.
- [x] 20 Self-audit log. DOD: LOG_SHINOBU_120.md contains final report and <SELF_AUDIT>. Rejected: chat-only report. Microseconds: PENDING PROFILER.

## Verification
- `git diff --check -- <SHINOBU_120 files>`: no whitespace errors; only repo CRLF warnings for pre-existing file style.
- Runtime hot-path grep: no RenderTexture.GetTemporary, CommandBuffer.Blit, Graphics.Blit, SetData, GetData, string.Split, FindObjectOfType in new runtime files. Cold allocations remain for persistent buffers/fallback textures and surge dump.
- CPU guard: Get-Counter returned 100; build/profiler not executed by rule.

## Ultra-Think Polish Pass - 2026-05-19
- [x] Removed direct `using Hecton8.Environment` and the concrete `GlobalRegistry.BiomeMatrix` read from the render feature. DOD: no sibling-domain concrete reference remains in SHINOBU runtime files. Rejected: direct BiomeMatrixDirector/AtmosphereProfile dependency.
- [x] Replaced per-frame `BuildMockVolumetricLightsJob.Run()` with a one-frame-latency scheduled job. DOD: `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`, `[NoAlias]`, and `JobHandle.IsCompleted` gate before `Complete()`. Rejected: blocking same-frame job execution.
- [x] Double-buffered point-light GPU upload. DOD: two persistent GraphicsBuffers, inactive-buffer upload, active-buffer flip, `UnsafeUtility.MemCpy`. Rejected: single buffer CPU write while GPU may read.
- [x] Strengthened Dear Lie shader path. DOD: low-quality proxy uses explicit 4x4 Bayer dither blended with temporal noise and bypasses the raymarch loop. Rejected: stochastic-only dither without ordered matrix proof.
- [x] Added directional scattering from `_SunDirection` and `_FinalGiantAbyssLight`. DOD: primary directional light contribution exists alongside PointLightDTO scattering. Rejected: point-light-only raymarch.
- [x] Flow advection now multiplies sampled `_AbyssalFlowFieldTexture` by the explicit SHINOBU visual phase in `_HectonVolumetricFogCompositeParams.w`. DOD: current field motion is visible without CPU fluid simulation or hidden Unity `_Time`. Rejected: static flow offset and implicit global time.
- [x] UI Toolkit tuner no longer uses `System.Func` mutation wrappers for DTO sliders. DOD: direct ref mutation via `VolumetricFogParamsAccess.ElementAt`. Rejected: editor delegate churn for simple field writes.
- [x] Cached DataVault route now avoids registry polling while a valid vault exists. DOD: `GlobalRegistry.DataVault` is used only when cached vault is null/fenced. Rejected: hot-path registry lookup every frame.
- [x] RenderGraph now declares SHINOBU constant/structured buffer reads. DOD: `_paramsBuffer` and active point-light buffer are imported with `renderGraph.ImportBuffer`, declared with `builder.UseBuffer`, and bound per compute dispatch. Rejected: hidden `Shader.SetGlobalConstantBuffer` global state before the graph pass.
- [x] Shader color now has a Deep Sea Noir luminance floor. DOD: raymarch/proxy fog call `ResolveNoirFloorColor` so settings cannot collapse the medium to pure black. Rejected: `max(color, 0)` that violates the noir mandate.
- [x] Fixed low-end proxy bypass math. DOD: `ResolveProxyBlend` now uses explicit smoothstep polynomial over 0.12..0.42 quality, so quality <=0.12 reaches proxyBlend 1.0 and shader bypasses the raymarch loop. Rejected: Unity `Mathf.SmoothStep(0.12, 0.42, q)` misuse.
- [x] RenderGraph now declares external MarineSnow/AbyssalFlow texture reads. DOD: external textures are wrapped in cached RTHandles, imported, and declared with `builder.UseTexture` before compute binding. Rejected: raw global texture bind with no graph edge.
- [x] Shader UAV writes are finite-sanitized. DOD: raymarch/proxy outputs go through `ResolveSafeFogWrite`; composite clamps NaN fog/source color before writing. Rejected: trusting upstream depth/texture data to stay finite.
- [x] CSV editor load now uses the reserved vault scratch route. DOD: `ShinobuVolumetricFogCsvScratch` is requested as a vault byte buffer, `ReadFileIntoScratch` streams into it, and `TryParseInto` writes directly to `ShinobuVolumetricFogExtinctionProfiles`. Rejected: editor `File.ReadAllBytes`, temp `NativeHashMap`, and `GetValueArray(Allocator.Temp)`.
- [x] Black-box dump no longer allocates managed `byte[]`. DOD: `DumpTelemetryRing` wraps the NativeArray pointer in `ReadOnlySpan<byte>` and writes through `FileStream.Write`. Rejected: `Marshal.Copy` into managed dump staging.
- [x] Shader no longer depends on hidden Unity `_Time.y`. DOD: C# sends a quality-quantized visual phase in `_HectonVolumetricFogCompositeParams.w`; shader dither/flow drift and mock lights use that phase. Rejected: implicit `_Time` global controlling visual update cadence.
- [YELLOW] Task 09 exact 3D SiltBuffer remains intentionally folded into existing MarineSnow wake/fog-density owner. Reason: one fact -> one owner; duplicate 3D silt owner would be architectural debt. This needs integrator acceptance if the original literal 3D buffer remains mandatory.
- [YELLOW] Task 12 exact BiomeTransitionManager output remains contract-blocked. Direct concrete reference was removed. Current implementation consumes vault-loaded extinction profiles and depth bands; a future biome owner must publish an unmanaged DTO/signal before direct biome hash blending is safe.
- [YELLOW] Task 16 GPU query is still not real GPU timing. Current telemetry stores source-derived estimated usec and dump trigger. Profiler/GPU query proof remains pending.

## Polish Verification
- Direct bad-pattern grep after polish: no `using Hecton8.Environment`, `BiomeMatrixDirector`, `.Run(`, `FindObjectOfType`, `RenderTexture.GetTemporary`, `Blit`, `SetData`, `GetData`, `System.Func`, `Pack=`, DTO properties, or `OnGUI` in SHINOBU runtime/editor files.
- Allocation grep after polish: cold persistent GraphicsBuffer/RenderTexture/Texture3D allocations only; CSV editor path and black-box dump no longer use managed `byte[]`, temp `NativeHashMap`, or `GetValueArray(Allocator.Temp)`.
- Shader grep after polish: `ResolveBayer4`, `ResolveProxyDither`, `ResolveDirectionalScattering`, `_AbyssalFlowFieldTexture`, and proxy loop bypass are present.
- `git diff --check` on edited SHINOBU files: clean.
- Compile guard after polish: CPU sampled 97.716%, 100%, 100% with active `csc`/`dotnet`, then 89.43% with no dotnet/csc; build not launched because CPU stayed above 50%.

## Compile-Wall Evidence - 2026-05-19
- Guard condition before build: CPU below 50 percent and no active `dotnet`/`csc` process, so a narrow `Hecton8.Core.csproj` build was legal by AGENTS.md.
- Command attempted: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Result: build failed with `CS2001` because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is referenced by `Hecton8.Core.csproj` and absent on disk.
- Boundary decision: no restoration, deletion, or csproj mutation was performed because the missing World file is outside SHINOBU_120 authority.
- Strike count: compile-wall strike 1, `[BLOCKED BY DEPENDENCY]` for Unity/Core compile proof only. SHINOBU static checks remain the current evidence level.

## Loop 11 - Bandwidth, Layout, Variant Audit - 2026-05-19
- [x] Cached `VolumetricFogNativeLayout.Validate()` after static initialization. DOD: render/editor calls now return a static bool instead of repeating `Marshal.OffsetOf` reflection on every RenderGraph record. Rejected: deleting layout validation entirely. Microseconds: PENDING PROFILER.
- [x] Added SHINOBU constant-buffer dirty gate. DOD: `VolumetricFogParamsDTO` hashes four float4 lanes and skips `LockBufferForWrite` when the 64B page is unchanged. Rejected: blind CBuffer upload every frame. Microseconds: PENDING PROFILER.
- [x] Removed unused `_MATH_LOD_LOW/_MATH_LOD_HIGH` variants from `Hecton_VolumetricFog.compute`. DOD: no preprocessor branch referenced those keywords inside the compute shader; GlobalQualityWeight DTO remains the active continuous quality route. Rejected: carrying dead variants that increase warmup/variant surface. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` over SHINOBU runtime/contracts/shader/docs returned only repo CRLF warnings. Bad-pattern grep found no blind `UploadConstantBuffer(` call, no `_Time`, no temp RT/blit/SetData/GetData/File.ReadAllBytes/new byte[]/NativeHashMap/Allocator.Temp/sibling biome concrete route. One `Marshal.OffsetOf` call remains cold-only behind cached static validation.
- [BLOCKED BY DEPENDENCY] Compile proof remains blocked by missing non-SHINOBU `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`; no new build was launched in this loop.

## Loop 12 - Point-Light Structured Buffer Dirty Page - 2026-05-19
- [x] Added dirty hash for the 8-entry `PointLightDTO` structured buffer page. DOD: completed mock-light jobs hash the active count and float4 lanes; unchanged pages skip `LockBufferForWrite`, buffer flip, and GPU upload. Rejected: uploading identical synthetic lights during low-quality visual-phase cadence freezes. Microseconds: PENDING PROFILER.
- [x] Reset point-light dirty state on structured-buffer recreation and teardown. DOD: cold buffer reallocation forces the next completed page to upload once. Rejected: stale dirty flag after graphics-resource loss. Microseconds: PENDING PROFILER.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: Core build still stops on missing non-SHINOBU World source before this domain can be validated.

## Loop 13 - AUP-Local Camera Input Sanitization - 2026-05-19
- [x] Replaced raw camera transform use for fog DTO/job inputs with a Core floating-origin offset snapshot and immediate local float3 recast. DOD: SHINOBU does not import `Hecton8.World.AbsoluteUniversePosition` from a sibling domain, but camera local coordinates are now derived through `HectonFloatingOrigin.CurrentTotalOffsetDouble` before narrowing. Rejected: adding a direct World/AUP using to the Visor runtime. Microseconds: PENDING PROFILER.
- [x] Finite-normalized camera forward before scheduling mock lights. DOD: non-finite or zero forward vectors fall back to +Z before Burst job input. Rejected: trusting `Transform.forward` to stay finite forever. Microseconds: PENDING PROFILER.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build.

## Loop 14 - Quality Continuum Math Authority - 2026-05-19
- [x] Converted SHINOBU quality-control curves to Unity.Mathematics scalar authority. DOD: ray steps, internal scale, proxy blend, point-light count, visual phase cadence, setup quality clamp, and estimate scaling now use `math.saturate`, `math.lerp`, cubic polynomial smoothing, and an explicit `math.step` survival floor for the low-end proxy. Rejected: leaving mixed `Mathf` quality code that made the GlobalQualityWeight continuum less auditable. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; quality grep found `ResolveQualityCurve` and `math.step` and no `Mathf.Clamp01(quality)`, `Mathf.Lerp(VolumetricFogConstants...)`, `Mathf.Lerp(5f...)`, `Mathf.Clamp01(HomeostasisBrain...)`, or `Mathf.SmoothStep` in the SHINOBU runtime feature.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

## Loop 15 - Finite Quality Scalar Vaccination - 2026-05-19
- [x] Added finite-saturated scalar gate for quality and estimate curves. DOD: `ResolveFiniteSaturated` now converts NaN/infinity to `0.0` before `math.saturate`; GlobalQualityWeight, render-scale estimate, ray steps, proxy blend, visual cadence, and point-light count now fail toward minimum survival cost instead of propagating NaN. Rejected: trusting `math.saturate` alone as a NaN guard. Microseconds: PENDING PROFILER.
- [x] Added finite fallback endpoints for editor internal-scale settings. DOD: invalid `minimumInternalScale` falls back to 0.25 and invalid `maximumInternalScale` falls back to 0.67 before render-target sizing. Rejected: allowing editor NaN to poison quantized RTHandle dimensions. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; quality grep shows `ResolveFiniteSaturated` guarding quality curve, proxy blend, point-light count, setup quality, estimate scale, and `HomeostasisBrain.GlobalQualityWeight`.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

## Loop 16 - Shader FBM Octave Collapse - 2026-05-19
- [x] Removed mandatory second FBM detail evaluation from low/middle raymarch density. DOD: `ResolveFogDensity` now computes one coarse FBM by default, then blends in the expensive fine FBM only after a continuous quality ramp above 0.35. Rejected: paying two FBM paths as soon as proxyBlend drops below 1.0. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; shader grep shows `fineBlend` gating the second `Fbm3` call inside `ResolveFogDensity`.
- [BLOCKED BY DEPENDENCY] Compile/shader import proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

## Loop 17 - External Shader-Global Snapshot Consolidation - 2026-05-19
- [x] Consolidated MarineSnow/AbyssalFlow global shader reads into one RenderGraph-record snapshot. DOD: telemetry no longer calls `Shader.GetGlobalTexture` or `Shader.GetGlobalFloat`; it receives pre-read MarineSnow/flow-active flags from the same snapshot used for graph texture binding. Rejected: duplicate global state reads inside telemetry after resource binding already needed those values. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; grep shows `Shader.GetGlobalTexture/GetGlobalFloat` only in the single external snapshot block before `UpdateVaultAndGpuState`, not inside `RecordTelemetry`.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

## Loop 18 - Editor Facade NaN Quarantine - 2026-05-19
- [x] Hardened Abyssal Atmosphere Tuner writes before they touch vault DTOs. DOD: slider reads and slider apply methods now route through finite fallback clamps; invalid designer/editor values collapse to domain defaults instead of writing NaN into `VolumetricFogParamsDTO`. Rejected: treating editor UI as harmless because it is cold-only. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; tuner grep shows `ClampFinite` on default quality seed and all density/scatter/extinction/anisotropy/flow/quality writes, with no raw `math.clamp(value...)` or `math.saturate(HomeostasisBrain...)` route left.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

## Loop 19 - Runtime DTO Boundary Sanitization And Local Noise - 2026-05-19
- [x] Hardened runtime settings/vault/CSV profile inputs before they become `VolumetricFogParamsDTO` or telemetry evidence. DOD: fog color, density, scattering, extinction, anisotropy, opacity break, flow, max ray distance, silt strength, bilateral scale, profile depth ranges, absorption/scatter lanes, debug heatmap, and estimated GPU time now pass finite fallback clamps in the runtime feature. Rejected: relying on inspector ranges or shader output clamps after DTO poisoning. Microseconds: PENDING PROFILER.
- [x] Moved shader FBM coordinates from `samplePositionWS` to `samplePositionLocal + wrappedCameraNoiseOffset`. DOD: raymarch still samples AbyssalFlow using the owner world/sample coordinate, but the high-frequency fog noise now obeys camera-local/AUP-wrapped precision rather than large float world coordinates. Rejected: continuing world-space FBM and hoping origin shifts always happen before precision loss. Microseconds: PENDING PROFILER.
- [x] Verification: `git diff --check` returned only CRLF warnings; bad-pattern grep found no temp RT, blit, SetData/GetData, File.ReadAllBytes, new byte[], NativeHashMap, Allocator.Temp, Pack=, `_Time`, `multi_compile`, or direct Environment/World using in SHINOBU files. Shader grep confirms `ResolveFogDensity(samplePositionLocal, samplePositionWS, ...)` and no `samplePositionWS + _HectonVolumetricFogFlowAdvection` coordinate route.
- [BLOCKED BY DEPENDENCY] Compile proof unchanged: missing non-SHINOBU World source still blocks Core build; no new build was launched in this loop.

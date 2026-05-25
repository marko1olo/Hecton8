# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_236 Bilateral DRS Route Card

Owner: SHINOBU_236 / Rendering / DRS reconstruction.
Status: STATIC SOURCE ONLY - Unity import, shader compile, Play Mode, Frame Debugger, profiler, GCMonitor, and player-build proof pending.

## 2026-05-21 Static Integration Blockers

- XR provider route is not proven on disk. `ProjectSettings/ProjectSettings.asset:544` still serializes `m_BuildTargetVRSettings: []`, and a filesystem scan found no serialized XR Management/OpenXR settings assets. `Packages/manifest.json` contains XR Management/OpenXR/Meta OpenXR packages, but package presence is not an active loader route. The existing platform-owner repair path is `Assets/_Project/Scripts/Editor/Build/PlatformPortabilityRouteRepairer.cs`, which calls `XrPlatformReadinessValidator.WireAndroidOpenXrProviderRouteForCi()`; SHINOBU_236 does not hand-edit platform settings.
- Renderer feature serialization is not proven on disk. Static grep over `PC_Renderer.asset`, `PC_High_Renderer.asset`, `Mobile_Renderer.asset`, and `Quest_VR_Renderer.asset` still finds no serialized `HectonBilateralDrsUpscalerFeature` sub-asset. The SHINOBU-owned route is `BilateralDrsRendererFeatureInstaller` through Unity import/`InitializeOnLoadMethod` and build preprocess verification, not YAML text surgery.
- Quest depth route is now guarded but still requires a platform-owner decision. `URP_Quest_VR.asset` currently serializes `m_RequireDepthTexture: 1`, but `QuestVulkanRenderPipelineConfigurator.ConfigureUrpAsset()` writes the same field to `false` during Quest platform repair/build preprocessing. SHINOBU_236 requires a valid `cameraDepthTexture`; `BilateralDrsRendererFeatureBuildGuard` now fails player builds if any target URP asset disables camera depth texture. SHINOBU_236 still does not mutate platform settings or invent an unverified color-only upscaler route.
- Until those importer-owned routes execute, SHINOBU_236 remains source-present and fail-closed, but not runtime-proven.

## R48 Exact Route Fields

Route ID: SHINOBU_236_BILATERAL_DRS_RECONSTRUCTION

Owner: `HectonBilateralDrsUpscalerRuntime`

Instrument: GlobalDataVault + all-or-fail dispatcher route + dispatcher-scheduled simulation jobs + RenderGraph constant-buffer bridge + black-box telemetry
Producer/consumer phase: `PreSimulation` captures frame/dimension intent only; `Simulation` schedules mock-state and parameter jobs and returns a `JobHandle`; `PostSimulation` publishes the active DTO after the dispatcher-owned completion window; `VisualSync` uploads active constant-buffer DTO; URP RenderGraph consumes in the rendering phase. Dispatcher registration failure is fail-closed; no partial `IUpdatable`/late-frame fallback route exists.
Cadence/capacity: one active/pending 32-byte DTO pair, fixed 300-entry telemetry ring, profile/scratch/mock lanes `71050..71056`; updates are dirty/cadence gated by continuous quality.

Overflow/failure: missing Vault/service/layout/non-finite rows fail closed, set fault flags, and request a generated-on-fault dump.

Shutdown/disposal: runtime releases generation handles and GPU buffers; Vault owner retains native buffer lifecycle.

Proof required before GREEN: Unity import, shader compile, RenderGraph/Frame Debugger capture, Play Mode route, profiler/GC proof, and linked dump readback.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

## Route

Fact: presentation-only DRS reconstruction parameters and diagnostics.

Owner: `HectonBilateralDrsUpscalerRuntime`.

Owner lifecycle:
- Scene-local runtime owner is created from `RuntimeInitializeOnLoadMethod` and `SceneManager.sceneLoaded`.
- Render features never create the owner; they use `TryGetRuntimeInstance`, submit render dimensions for the next owner phase, and read only already-published constant buffers during RenderGraph recording.
- Dispatcher registration is all-or-fail across PreSimulation, Simulation, PostSimulation, and VisualSync. Partial dispatcher registration is rolled back before the owner is allowed to publish or upload. Dispatcher absence does not register a second update route.

Producer phase:
- `IDispatcherSystem.PreSimulation`: advances presentation frame/time, captures submitted render dimensions, and fail-closes when Vault state is not ready. It does not schedule jobs, allocate Vault buffers, upload GPU buffers, or publish DTO rows.
- `IDispatcherSystem.Simulation`: `SimulationKernelBridge` schedules `GenerateMockDrsStateJob` when the scaler snapshot is absent, chains `CalculateUpscalerParamsJob`, registers the resulting handle with `H8Memory`, and returns the handle to `SystemDispatcher`.
- `IDispatcherSystem.PostSimulation`: `PostSimulationPublishBridge` copies the pending DTO row to the active row only after the dispatcher completion window has resolved the simulation handle.
- `IDispatcherSystem.VisualSync`: upload bridge copies the active 32-byte DTO to the double `GraphicsBuffer.Target.Constant` lane.

Consumer phase:

- URP RenderGraph pass `HectonBilateralDrsUpscalerFeature.RecordRenderGraph`.

- `AddRenderPasses` submits current source/full dimensions and jitter through `SubmitRenderDimensions`; this does not touch Vault, run jobs, or upload GPU buffers. When the camera descriptor does not prove a scaled low-res source, low dimensions are submitted as `0` sentinels and the owner phase resolves them from `IResolutionScalerService` or the Vault-backed mock lane instead of mislabeling a full-size descriptor as the DRS input.

- `RecordRenderGraph` reads a matching owner-published CBuffer through `TryGetActiveConstantBufferForDimensions`. If the owner has not published a CBuffer for the current dimensions yet, the pass fails closed for that frame instead of running jobs or buffer uploads while recording the graph.
- Compute shader kernels `SobelDepthMask`, `BilateralUpscale`, and development-only `EdgeMaskDebugComposite`.
- XR/VR texture-array inputs use paired kernels `ClearEdgeMaskArray`, `SobelDepthMaskArray`, `BilateralUpscaleArray`, and `EdgeMaskDebugCompositeArray` when color/depth descriptors are both `Texture2DArray`, `SystemInfo.supports2DArrayTextures` is true, slice counts match, slice count is `1..2`, and MSAA is off.
- Compute shader kernel `ClearEdgeMask` writes a declared 1x1 black mask for graph-valid fail-close/skip paths so `_H8BilateralDrsEdgeMask` cannot expose stale edge data from an older successful DRS frame. If compute is unavailable or the compute asset is missing, the same proof artifact is published by a 1x1 raster RenderGraph clear instead of a blit or CPU/global shader setter.
- Editor-only installer `BilateralDrsRendererFeatureInstaller` creates/repairs renderer feature sub-assets for `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, and `Quest_VR_Renderer` through `SerializedObject` and rebuilds `m_RendererFeatureMap`; no YAML hand-edit is the authority route. Reload-time auto-install uses `EditorUserBuildSettings.activeBuildTarget` rather than the no-target all-renderer path. The editor build preprocessor calls the target-scoped installer and verifier for assets consumed by `BuildReport.summary.platform`: standalone checks/repairs `PC_Renderer`, `PC_High_Renderer`, and `URP_Low`/`URP_Medium`/`URP_High`; Android checks/repairs `Mobile_Renderer`, `Quest_VR_Renderer`, `URP_Low`, and `URP_Quest_VR`; iOS checks/repairs `Mobile_Renderer` and `URP_Low`; manual no-target setup still checks/repairs all by explicit menu action. Each scoped path still verifies feature references, feature map entries, compute shader binding, injection point, forced-full-resolution state, activation scale, all mono/array kernels, and required camera depth texture.
- Platforms or graphics backends reporting `SystemInfo.supportsComputeShaders == false` enqueue only the raster clear proof pass; they never enter Sobel/upscale compute reconstruction and never fall back to bilinear blit ownership.

Route:

- `GlobalDataVault` buffers `71050..71056` for CPU-side owner data.
- Imported `GraphicsBuffer` constant buffer for GPU scalar payload.
- RenderGraph-declared `TextureHandle` reads/writes for low-res color, high-res depth, edge mask, and output color.

Proof artifact: ABSENT until a timestamped runtime trigger/output exists. `UpscalerTelemetryEntry[300]` in Vault buffer `71052`, binary dump path `Docs/AgentLogs/Dump_SHINOBU_236.bin`, and static scanner report path `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` are STATIC_SOURCE/planned-fault orientation only.

## Vault Buffers

- `71050` `Shinobu236BilateralDrsParams`: `UpscalerParamsDTO[2]`, active/pending rows.

- `71051` `Shinobu236BilateralDrsTuning`: `UpscalerTuningDTO[1]`, editor/CSV-authored tuning.

- `71052` `Shinobu236BilateralDrsTelemetry`: `UpscalerTelemetryEntry[300]`, black-box ring.

- `71053` `Shinobu236BilateralDrsTelemetryCursor`: `int[1]`, black-box cursor.

- `71054` `Shinobu236BilateralDrsProfiles`: `UpscalerProfileDTO[32]`, cold profile table.
- `71055` `Shinobu236BilateralDrsCsvScratch`: `byte[16384]`, cold CSV staging.
- `71056` `Shinobu236BilateralDrsMockState`: `DrsStateDTO[1]`, emergency synthetic DRS fallback.

Cold CSV profile ingestion:
- `upscaler_quality_profiles.csv` is project-relative only; null, rooted, and parent-traversal paths fail closed.
- The profile Vault lane is cleared before parsing, and `_profilesSeeded` is set only after at least one valid strict-schema row is parsed.
- The parser scans the whole file. Any non-header/non-comment malformed row or valid-row overflow beyond `UpscalerProfileDTO[32]` clears the lane and returns zero rows.
- Missing, malformed, inaccessible, over-capacity, or zero-row CSV input leaves no stale profile override active; the runtime falls back to the base continuous quality curve.

## ABI

`UpscalerParamsDTO` is 32 bytes:

- offset 0: `float4 ResolutionParams`, 16 bytes.

- offset 16: `float4 FilterParams`, 16 bytes.

- padding: 0 bytes.

`UpscalerTelemetryEntry` is 64 bytes:

- scalar header offsets `0..28`, two `float4` lanes at offsets `32` and `48`.

- padding: 0 bytes.

- one cache line per telemetry row; not an atomic shared counter row.

No `[StructLayout(Pack=1)]`, managed references, runtime `bool`, properties, or variable-size fields are part of the DTO route.

## Failure Mode

- Missing Vault or stale handles: fail closed; no RenderGraph pass gets a constant buffer.
- Partial dispatcher route registration: unregister partial route, clear pending/scheduled upload flags, invalidate published constant-buffer state, and force RenderGraph into clear-only edge-mask publication.
- Runtime Vault resolve failure during Simulation, PostSimulation publication, or VisualSync upload: clear `_vaultStateReady`, pending upload, scheduled job state, `s_hasPublishedParameters`, and the published constant-buffer frame index so stale GPU constants cannot be consumed.
- Non-finite active DTO after `PostSimulation` publication: dump black-box telemetry, clear pending upload, invalidate `s_hasPublishedParameters`, and force RenderGraph into cleared edge-mask fail-close instead of reusing the previous constant buffer.
- Invalid DTO layout: set `FaultLayout` and dump telemetry if available.
- Non-finite output parameters: set `FaultNonFinite` and dump telemetry once per fault streak.

- `SystemInfo.supportsSetConstantBuffer == false`: set `FaultConstantBufferUnsupported`, request a dump once, and fail closed rather than falling back to `Shader.SetGlobal*` or `SetData`.

- Missing ResolutionScaler service: use Vault-backed mock DRS state for editor/CI isolation.

- Debug mask disabled: no debug composite pass; normal bilateral path continues.

- Compute shaders unsupported by the active backend: enqueue/record only the 1x1 raster-cleared edge-mask proof artifact; no fallback blit or color-only upscaler is introduced.
- Non-2D, mismatched texture-array, array texture support missing, XR array with more than two slices, or MSAA color/depth inputs: fail closed until dedicated kernels or resolves exist.
- Temporary edge/output UAVs are explicitly created as `TextureDimension.Tex2D`/`Tex2DArray`, `slices = 1..2`, and matching `VRTextureUsage` to match the shader declarations.
- Edge masks use split shader bindings: `_H8EdgeMaskWrite` / `_H8EdgeMaskArrayWrite` for clear/Sobel UAV writes and `_H8EdgeMaskRead` / `_H8EdgeMaskArrayRead` for upscale/debug SRV reads.
- Unsupported UAV formats: fail closed unless `R8_UNorm`/`R16_SFloat` edge mask and output color LoadStore support resolve.

- Runtime-absent, unsupported descriptor, active-backbuffer, missing input, stale CBuffer, unsupported format, and zero-contribution Sobel paths attempt to publish a graph-declared 1x1 cleared edge mask. Compute-supported paths use the clear kernel; compute-missing/unsupported paths use the raster clear.
- Clear-only RenderGraph mode requires the mono and array clear kernels only for compute-backed stale-mask invalidation. Raster clear is the explicit fallback for compute-unavailable proof publication. Active upscale/debug recording separately requires the mono and array Sobel, upscale, and debug kernels.
- After a successful upscale or debug composite, `cameraTargetDescriptor.width/height/graphicsFormat` is updated to the full output descriptor. `_ScreenSize` global constant repair is deliberately not duplicated here because URP's own helper uses `AddUnsafePass`; profiler/Frame Debugger proof is required before adding an unsafe global-state pass to this route.
- Vault resolve failures now set `FaultVaultUnavailable` before fail-close and request a black-box dump when the telemetry lane remains resolvable.

## Scalability

`GlobalQualityWeight` remains a continuous scalar. It changes only presentation fidelity:

- low quality collapses tap gates toward bilinear/cross-like behavior and raises flat-surface bypass.

- middle quality keeps Sobel-gated 3x3-ish reconstruction.

- high/ultra quality permits wider edge taps inside the fixed 5x5 loop envelope.

- shader quality collapse uses `smoothstep(0.015, 0.075, quality)` rather than a literal hard quality cutoff.

- at the zero-contribution edge of that same continuous curve, Sobel is not dispatched, the graph-cleared edge mask is published, and `BilateralUpscale` returns manual bilinear before reading the edge mask or entering the bilateral loop. This is a scalar zero-work collapse, not a hardware-tier switch.

- depth confidence maps output pixels into the actual depth texture dimensions, so DRS-scaled depth does not smear silhouettes.

It does not alter DTO layout, save identity, rollback authority, or gameplay truth.

## Compile-Wall Boundary

Runtime source is isolated by `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef`.

Runtime references:

- `Hecton8.Core`

- `Hecton8.Core.Contracts`

- `Hecton8.Core.Memory`

- Unity Burst/Collections/Jobs/Mathematics/RenderPipeline packages.

No direct sibling runtime references to AI, World, Gameplay, Physics, Audio, VFX, Environment, Vehicles, Habitat, Logistics, Power, or Input are introduced by this route.

Editor facade remains in `Hecton8.Editor` and references `Hecton8.Rendering.BilateralDrs` explicitly.

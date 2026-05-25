# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/BILATERAL_DRS_UPSCALER_SHINOBU_236.md
Rule: historical snapshot only; not active doctrine.

# Bilateral DRS Upscaler - SHINOBU_236

Owner: Rendering / DRS reconstruction.

Route card: `Docs/ARCHITECTURE/SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md`.

Current static integration blockers:
- XR provider route is not serialized yet: `ProjectSettings/ProjectSettings.asset` still has an empty build-target VR settings list, and no XR Management/OpenXR settings assets were found on disk. The platform-owner repair route exists in `PlatformPortabilityRouteRepairer`/`XrPlatformReadinessValidator`; SHINOBU_236 does not mutate that route directly.
- Renderer assets still do not serialize `HectonBilateralDrsUpscalerFeature`. `BilateralDrsRendererFeatureInstaller` and the build guard are the Unity import-aware authority route; static YAML remains inert until Unity imports/runs the installer.
- Quest depth route is guarded but still needs a platform-owner budget decision: the static Quest URP asset currently requests depth, but the Quest platform configurator source disables depth during repair/build preprocessing. SHINOBU_236 requires `cameraDepthTexture`; `BilateralDrsRendererFeatureBuildGuard` now fails the player build if any target URP asset disables camera depth texture. If platform owners choose depthless Quest, this upscaler intentionally fails closed rather than inventing an unverified color-only algorithm.

Runtime path:

- `HectonBilateralDrsUpscalerRuntime` owns Vault lanes `71050-71056`, caches `DataVault`/`ResolutionScalerService` during cold init, and rebinds them through `IGlobalRegistryHotSwapListener`. Dispatcher registration is all-or-fail across PreSimulation, Simulation, PostSimulation, and VisualSync; partial registration is rolled back before any DTO publication is allowed, and dispatcher absence now fails closed instead of registering an incomplete `IUpdatable` route. `PreSimulation` only advances presentation timing and validates Vault readiness. `SimulationKernelBridge` schedules `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob` through returned `JobHandle`s. `PostSimulationPublishBridge` publishes the active DTO after the dispatcher completion window. `VisualSync` uploads one 32-byte constant buffer through double `GraphicsBuffer.Target.Constant` buffers, and the runtime records a 300-entry telemetry ring. The owner is scene-local and bootstrapped from runtime-load/scene-loaded hooks, not from RenderGraph. If Vault resolve, non-finite DTO validation, or CBuffer upload fails after a previous successful publish, the runtime requests the relevant dump where possible, invalidates `s_hasPublishedParameters`, and clears the published frame index so RenderGraph cannot consume stale GPU constants. If `SystemInfo.supportsSetConstantBuffer` is false, the owner sets `FaultConstantBufferUnsupported`, requests a dump once, and fail-closes instead of adding a global-float or `SetData` fallback.
- `HectonBilateralDrsUpscalerFeature` injects a URP RenderGraph pass before post processing. `AddRenderPasses` enqueues clear-only mode for compute-missing or compute-unsupported frames so the global edge-mask proof artifact is invalidated instead of left stale; active reconstruction still requires compute. It rejects non-2D/non-array, mismatched array, missing 2D-array texture support for array descriptors, array slice count above two, and MSAA descriptors before active enqueue, but graph-valid runtime-absent or unsupported-descriptor paths enqueue a clear-only pass. Compute-supported clear-only setup requires `ClearEdgeMask` plus `ClearEdgeMaskArray`; compute-missing/unsupported clear-only uses a 1x1 raster black mask published through RenderGraph. Active upscale/debug setup separately requires mono and array Sobel/upscale/debug kernels. It submits exact low dimensions only when the descriptor proves a scaled source or when forced full-resolution testing is enabled; otherwise it submits `0` low-dimension sentinels for the owner phase to resolve through the scaler service or mock DRS lane. `RecordRenderGraph` reads only an already-published CBuffer through `TryGetActiveConstantBufferForDimensions`, supports matched `Texture2D` and `Texture2DArray` color/depth inputs only when required capabilities and slices are valid, fail-closes for stale dimensions, missing CBuffer, or unsupported UAV formats, and publishes a graph-declared 1x1 black `_H8BilateralDrsEdgeMask` on fail paths through compute clear when possible or raster clear when compute cannot run. Successful normal and debug output update `cameraTargetDescriptor.width/height/graphicsFormat`, then write a matching `Texture2D` or two-slice `Texture2DArray` reconstructed camera color.
- `Hecton_BilateralUpscale.compute` normally runs `SobelDepthMask` or `SobelDepthMaskArray` first. When quality is inside the zero-contribution edge of the continuous `smoothstep(0.015, 0.075, quality)` gate, C# skips Sobel, publishes a graph-cleared black edge mask, and the shader returns manual bilinear before reading that mask or entering the bilateral loop. Flat pixels bypass to bilinear. Edge pixels use depth/color/spatial bilateral weights gated by the same continuous quality collapse, not device-tier branches. Depth coordinates map from output pixels into actual depth texture dimensions; all UAV writes have finite guards. Edge masks use split read/write bindings so clear/Sobel bind UAV names and upscale/debug bind SRV names. Development debug can switch the second pass to mono or array `EdgeMaskDebugComposite` black/green output without CPU readback.
- `BilateralDrsRendererFeatureInstaller` is editor-only glue: it attaches the feature to PC, PC High, Mobile, and Quest renderer assets via `SerializedObject`, rebuilds `m_RendererFeatureMap`, and binds the compute shader path without runtime YAML assumptions. Reload-time auto-install uses `EditorUserBuildSettings.activeBuildTarget` so ordinary script reloads repair only the active target route. `BilateralDrsRendererFeatureBuildGuard` runs before player builds, invokes target-scoped installation, and then validates the `BuildReport.summary.platform` route: standalone uses PC/PC High plus Low/Medium/High URP assets, Android uses Mobile/Quest plus Low/Quest URP assets, iOS uses Mobile plus Low URP, and manual no-target setup still scans/repairs all by explicit menu action. The guard throws if scoped renderer feature references, feature-map entries, compute binding, mono/array kernels, injection point, disabled forced-full-res mode, activation scale, or required URP camera depth texture settings are missing.

DTO:

- `UpscalerParamsDTO` is exactly 32 bytes.

- Offset 0: `float4 ResolutionParams` = low width, low height, high width, high height.

- Offset 16: `float4 FilterParams` = depth weight, color weight, packed radius+jitter, quality scalar.

- Jitter is packed into the fractional residual of `FilterParams.z`; radius is quantized to 1/16 pixel to preserve the 32-byte ARM64 constant-buffer contract.

Rollback isolation:

- The upscaler is presentation-only. It is not registered with rollback, save state, Merkle hashing, or gameplay authority routes.

- DRS scale and filter params may change across frames without affecting simulation truth.

Debug:

- Edge mask is exported as global texture `_H8BilateralDrsEdgeMask`.

- Editor tuner exposes depth weight, color weight, radius limits, quality override, quality bias, CSV profile load, layout validation, and a cached edge-mask debug toggle.

CSV profile format:
`profile,minScale,maxScale,depthWeight,colorWeight,minRadius,maxRadius,qualityBias`

CSV failure behavior:
- Profile paths are project-relative only; null, absolute/rooted, and parent-traversal paths fail closed.
- The Vault profile lane is cleared before parsing, and profiles are marked seeded only after at least one strict-schema row parses.
- The parser scans the whole file. Any non-header/non-comment malformed row or valid-row overflow beyond `UpscalerProfileDTO[32]` clears the lane and returns zero rows.
- Missing, malformed, inaccessible, over-capacity, or zero-row CSV input leaves no stale profile override active; runtime falls back to the base continuous quality curve.

Current static evidence / planned fault target:
- Static scan report path: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` if present on disk; this is STATIC_SOURCE only.

- Black-box dump target: `Docs/AgentLogs/Dump_SHINOBU_236.bin`; no runtime proof artifact is attached until a timestamped trigger and output file exist.

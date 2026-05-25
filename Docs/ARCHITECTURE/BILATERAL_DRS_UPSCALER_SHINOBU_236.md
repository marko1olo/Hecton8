# Bilateral DRS Upscaler - SHINOBU_236

Owner: Rendering / DRS reconstruction.

Route card: `Docs/ARCHITECTURE/SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md`.

Current static integration blockers:
- XR provider route is not serialized yet.
- `ProjectSettings/ProjectSettings.asset` has an empty build-target VR settings list.
- No XR Management/OpenXR settings assets were found on disk.
- Platform-owner repair route: `PlatformPortabilityRouteRepairer` / `XrPlatformReadinessValidator`.
- SHINOBU_236 does not mutate that route directly.
- Renderer assets still do not serialize `HectonBilateralDrsUpscalerFeature`. `BilateralDrsRendererFeatureInstaller` and the build guard are the Unity import-aware authority route; static YAML remains inert until Unity imports/runs the installer.
- Quest depth route:
  - Status: guarded; platform-owner budget decision pending.
  - Static Quest URP asset: requests depth.
  - Quest platform configurator source: disables depth during repair/build preprocessing.
  - SHINOBU_236 requirement: `cameraDepthTexture`.
  - Build guard: `BilateralDrsRendererFeatureBuildGuard` fails if any target URP asset disables camera depth.
  - Depthless Quest outcome: fail closed; no unverified color-only algorithm.

Runtime path:

- `HectonBilateralDrsUpscalerRuntime` owns Vault lanes `71050-71056`, caches `DataVault`/`ResolutionScalerService` during cold init, and rebinds them through `IGlobalRegistryHotSwapListener`.
- Dispatcher registration is all-or-fail across PreSimulation, Simulation, PostSimulation, and VisualSync.
- Partial registration rolls back before DTO publication.
- Dispatcher absence fails closed instead of registering an incomplete `IUpdatable` route.
- `PreSimulation` only advances presentation timing and validates Vault readiness.
- `SimulationKernelBridge` schedules `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob` through returned `JobHandle`s.
- `PostSimulationPublishBridge` publishes the active DTO after the dispatcher completion window.
- `VisualSync` uploads one 32-byte constant buffer through double `GraphicsBuffer.Target.Constant` buffers, and the runtime records a 300-entry telemetry ring.
- The owner is scene-local and bootstrapped from runtime-load/scene-loaded hooks, not from RenderGraph.
- If Vault resolve, DTO validation, or CBuffer upload fails after publish:
  - request the relevant dump when possible;
  - invalidate `s_hasPublishedParameters`;
  - clear published frame index.
- RenderGraph must not consume stale GPU constants.
- If `SystemInfo.supportsSetConstantBuffer` is false, the owner sets `FaultConstantBufferUnsupported`, requests a dump once, and fail-closes instead of adding a global-float or `SetData` fallback.
- `HectonBilateralDrsUpscalerFeature` injects a URP RenderGraph pass before post processing.
- `AddRenderPasses` enqueues clear-only mode for compute-missing or compute-unsupported frames so the global edge-mask proof artifact is invalidated instead of left stale; active reconstruction still requires compute.
- Rejects before active enqueue: non-2D/non-array, mismatched array, array slice count above two, MSAA descriptors.
- Also rejects array descriptors when 2D-array texture support is missing.
- Graph-valid runtime-absent or unsupported-descriptor paths enqueue clear-only pass.
- Compute-supported clear-only setup requires `ClearEdgeMask` plus `ClearEdgeMaskArray`; compute-missing/unsupported clear-only uses a 1x1 raster black mask published through RenderGraph.
- Active upscale/debug setup separately requires mono and array Sobel/upscale/debug kernels.
- Exact low dimensions require a descriptor-proven scaled source or forced full-resolution testing.
- Otherwise SHINOBU_236 submits `0` low-dimension sentinels.
- Owner phase resolves sentinels through the scaler service or mock DRS lane.
- `RecordRenderGraph`:
  - reads only an already-published CBuffer through `TryGetActiveConstantBufferForDimensions`;
  - supports matched `Texture2D`/`Texture2DArray` color/depth inputs only with valid capabilities and slices;
  - fail-closes on stale dimensions, missing CBuffer, or unsupported UAV formats;
  - publishes graph-declared 1x1 black `_H8BilateralDrsEdgeMask` on fail paths through compute clear or raster clear.
- Successful normal and debug output update `cameraTargetDescriptor.width/height/graphicsFormat`, then write a matching `Texture2D` or two-slice `Texture2DArray` reconstructed camera color.
- `Hecton_BilateralUpscale.compute` normally runs `SobelDepthMask` or `SobelDepthMaskArray` first.
- Zero-contribution gate: `smoothstep(0.015, 0.075, quality)`.
- Inside the zero edge, C# skips Sobel and publishes a graph-cleared black edge mask.
- Shader returns manual bilinear before mask read or bilateral loop.
- Flat pixels bypass to bilinear.
- Edge pixels use depth/color/spatial bilateral weights gated by the same continuous quality collapse, not device-tier branches.
- Depth coordinates map from output pixels into actual depth texture dimensions; all UAV writes have finite guards.
- Edge masks use split read/write bindings so clear/Sobel bind UAV names and upscale/debug bind SRV names.
- Development debug can switch the second pass to mono or array `EdgeMaskDebugComposite` black/green output without CPU readback.
- `BilateralDrsRendererFeatureInstaller` is editor-only glue: attaches feature to PC/PC High/Mobile/Quest renderers via `SerializedObject`, rebuilds `m_RendererFeatureMap`, and binds compute shader path.
- Reload-time auto-install uses `EditorUserBuildSettings.activeBuildTarget` so ordinary script reloads repair only the active target route.
- `BilateralDrsRendererFeatureBuildGuard` runs before player builds and invokes target-scoped installation.
- Platform route: standalone uses PC/PC High plus Low/Medium/High URP assets.
- Android uses Mobile/Quest plus Low/Quest URP assets; iOS uses Mobile plus Low URP.
- Manual no-target setup scans/repairs all by explicit menu action.
- Guard throws on missing scoped renderer feature refs, feature-map entries, compute binding, mono/array kernels, injection point, forced-full-res disable, activation scale, or URP depth settings.

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

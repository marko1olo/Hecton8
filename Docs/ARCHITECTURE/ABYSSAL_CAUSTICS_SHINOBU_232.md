# ABYSSAL CAUSTICS ROUTE CARD - SHINOBU_232



Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: ABYSSAL_CAUSTICS_AND_PROJECTION_PASS
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

Owner: `SHINOBU_232`



Domain: `ABYSSAL_CAUSTICS_AND_PROJECTION_PASS`



## Authority Boundary



The system owns presentation-only caustic lighting parameters. It does not own sunlight, waves, cave topology, rollback state, or deterministic gameplay facts.

External facts are read through cached registry and Vault routes, then collapsed into one 64-byte shader payload.



## Data Routes



- Input: `GlobalRegistry.Weather` cached cold as `IWeatherService`; frame setup reads one zero-allocation `WeatherRuntimeSnapshot` when initialized.



- Input: `BufferID.ShinobuOceanSurfaceSwell` when present.



- Input bridge owner: `Hecton8.World.HectonCaveVoxelLightingVolume`.
- Shader globals: `_HectonCaveVoxelSdfTex`, `_HectonCaveVoxelActive`, `_HectonCaveVoxelWorldToLocal`, `_HectonCaveVoxelHalfExtents`, `_HectonCaveVoxelInvDoubleHalfExtents`.
- Bridge status: legacy shader-global until World exposes a RenderGraph `TextureHandle` or Vault texture descriptor.
- SHINOBU_232 does not allocate, update, or republish the SDF volume.



- Output: `BufferID.ShinobuCausticsParameters` as two `CausticsParametersDTO` slots: active index 0 and pending index 1. The shader sees active 64-byte payload; the second slot is a CPU-side commit guard.



- Output: `BufferID.ShinobuCausticsTelemetryRing` as a 300-frame `CausticsTelemetryEntry` ring.



- Output: `BufferID.ShinobuCausticsTelemetryCursor` as one integer cursor.



- Tuning: `BufferID.ShinobuCausticsTuning`.



- CSV profiles: `BufferID.ShinobuCausticsProfiles`.



- CSV scratch: `BufferID.ShinobuCausticsCsvScratch`.



The weather/wave route is a cached Core service interface, not a sibling DTO Vault route.

`AbyssalDeferredCausticsRuntime` caches `IWeatherService` during bootstrap/hot-swap.

It reads `WeatherRuntimeSnapshot` only after init, then collapses weather intensity, wind, state mask, and three `GerstnerWaveComponent` lanes.



Surface swell remains optional producer state. Cold Vault setup/hot-swap repair caches a non-owning `VaultGenerationHandle<float4>` through `TryGetGenerationHandle`.

The caustics runtime resolves that descriptor read-only per tick. It never allocates, grows, polls, or releases producer-owned lanes in the frame path.



Telemetry flags match the current route vocabulary.

- `FlagWeatherSnapshotBound`: cached Core weather snapshot.
- `FlagWaveInputBound`: Gerstner/surface-swell wave input folded into the snapshot.
- Old weather/wave Vault-bound flag names are not current source authority.



- Owner output/tuning/telemetry/profile lanes are cold-acquired once and guarded by `_vaultStateReady`.
- Per-frame `Tick` skips duplicate owner-lane acquire probes while generation descriptors remain valid.
- Failed required resolves clear the gate and fail closed until bootstrap, DataVault hot-swap, editor tuning, or explicit profile reload repairs the Vault state outside frame memory ownership.
- The frame path returns immediately when tuning, telemetry, telemetry cursor, or profile lanes fail to resolve, so optional producer lanes cannot drive a partial owner-state parameter kernel.



- CSV profile names are cold-parsed with `ReadOnlySpan<byte>`.
- Known weather names map to canonical `WeatherState` masks; `Calm`/`Hurricane` bind to Core masks, unknown names produce FNV-1a keys for future routes.
- Matched profiles feed scale, intensity, max depth, flow speed, chromatic dispersion, and SDF shadow strength into the 64-byte CBuffer.
- The default editable profile file is `Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv`, exposed through the editor tuner reload button.
- Cold file load catches `IOException`/`UnauthorizedAccessException`, returns zero bytes, fails closed, and preserves existing/default Vault profiles instead of throwing into editor/boot flow.


## Render Path



- `HectonDeferredCausticsFeature` injects a URP RenderGraph full-screen pass.
- The pass binds private `_HectonDeferredCausticsSource` and `_HectonDeferredCausticsDepth` textures instead of rebinding URP-owned global color/depth names.
- The active caustics CBuffer is imported with `renderGraph.ImportBuffer` and declared through `builder.UseBuffer(..., AccessFlags.Read)` before `RasterCommandBuffer.SetGlobalConstantBuffer` binds it inside the render function.
- Shader reconstructs world position from bound depth, projects optional 1719-baked RGB flipbook caustics when a precompressed atlas is bound, samples inside each atlas cell with a CPU-authored texel inset vector to reduce mip/cell bleed, falls back to procedural Voronoi caustics when the atlas is absent, samples the World-owned cave SDF bridge, and composites into camera color.
- No Unity Projector, runtime-generated caustic RenderTexture, or per-object redraw is part of this route. The optional atlas is an offline asset bound through cold material setup, not a runtime texture generator. The 1719 baker also emits a static `TX_CausticLightCookie_*` derivative imported as a Unity Cookie texture and exposes an explicit selected-light assignment button for Directional/Spot `Light.cookie` fallback when a scene author wants a non-animated cookie path.
- 1719 importer configuration is fail-fast. After `SaveAndReimport`, the baker verifies texture type, 2D shape, sRGB state, mip state, wrap/filter modes, max texture size, and Standalone/Android platform compression overrides before reporting a successful bake.



Legacy shims only:
- `Hecton8.Graphics.Caustics.AnalyticalCausticsService`
- `Hecton8.Visor.CausticsProjectorManager`

They allocate no native/GPU buffers, dispatch no compute, publish no shader globals, and query no gameplay/physics services.

`GameBootstrapper` no longer stores or adopts `analyticalCausticsCompute`. It only attempts `AbyssalDeferredCausticsRuntime`.



- The old `Hecton_CausticsGenerator.compute` asset and meta have been deleted after active project scans found no remaining references to GUID `27b7cf5d630bd8d4dbc699ff38f19ac2`.
- `00_BOOTSTRAP.unity` no longer serializes that compute reference, and the inert shim no longer exposes `AssignComputeShader(ComputeShader)`.
- `GlobalShaderDispatcher` also no longer publishes `_H8CausticProjectionMatrix` or `_H8CausticRuntime`; those globals have no active shader consumer in the deferred caustics route.
- Any remaining archive references are historical evidence, not runtime authority.



- Material shader fallback branches no longer declare or sample `_HectonCausticsMap`, `_HectonProjectedCaustics*`, `_HectonCausticsRuntimeParams`, `_HectonCausticsSimulationParams*`, or `_UberNoirCaustic*`, and `H8_UBERNOIR_CAUSTICS_TEXTURED` is not part of the caustics path.
- Old material helper signatures remain as zero-return compatibility stubs only so dependent shaders compile without carrying a second caustics authority.
- UberNoir material consolidator no longer enables a caustic keyword.
- UberNoir runtime bridge no longer publishes analytical/secondary material caustic feature bits.
- Homeostasis uses neutral `CausticsDetail` for that quality-shed slot.



XR fullscreen path:

- Stereo macros: `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`.
- UV transform: `UnityStereoTransformScreenSpaceTex(input.screenUV)`.
- Transformed UV feeds `TEXTURE2D_X`, bound-depth sampling, and `ComputeWorldSpacePosition`.
- Single-pass instanced stereo keeps correct-eye sampling without a second pass or XR-specific variant family.



- Shader warmup is curated through `Assets/_Project/Art/Shaders/Variants/HectonDeferredCaustics.shadervariants` with GUID `232232232ca00147aa7d232232ca0014`.
- `00_BOOTSTRAP.unity` serializes SVC through `BootstrapController.shaderVariantCollections`; `BootstrapController.ApplySerializedShaderVariantCollections`, `GameBootstrapper.EnsureRuntimeInstance(GameObject)`, and the no-owner `GameBootstrapper.EnsureRuntimeInstance()` active-instance path transfer it to the runtime bootstrapper before any `BeginBootstrap()` path can start `MemoryPreWarm`.
- The handoff is skipped after bootstrap starts or completes.
- `BootstrapController` admits this route only when the scene name exactly equals `00_BOOTSTRAP` with `System.StringComparison.Ordinal`; substring scene matches are rejected.
- `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` calls `WarmUp()` during `MemoryPreWarm` before scene activation.
- `HectonDeferredCausticsFeature` no longer declares or serializes a `warmupVariants` field, and `PC_Renderer.asset`, `PC_High_Renderer.asset`, `Mobile_Renderer.asset`, and `Quest_VR_Renderer.asset` carry only the caustic shader/feature reference, not a second SVC warmup route.
- The Mobile/Quest renderer assets install `HectonDeferredCausticsFeature` immediately after SSDO, with `m_RendererFeatures` and the little-endian `m_RendererFeatureMap` both decoding to 12 matching entries.
- `URP_Quest_VR.asset` explicitly requires a depth texture because the Quest renderer has this depth-reconstructing feature active.


## Scalability



- `GlobalQualityWeight` is consumed as a continuous scalar.
- Low quality contracts maximum caustic depth, collapses to one monochrome noise layer, and keeps cave shadowing to the first cheap SDF lookup.
- Middle quality blends the second caustic layer and admits partial sun-ray SDF samples.
- High and ultra quality add chromatic dispersion, deeper visibility, and the full four-sample SDF confidence path.
- The shader keeps the same route and changes mathematical budgets/weights, avoiding hardware class booleans.



The fullscreen Voronoi helper keeps squared cell distance and remaps line intensity from that squared metric. It does not call `sqrt` in the per-pixel caustic line path.



RenderGraph destination inherits active camera color format and strips depth, MSAA, mips, and auto-mips.

This avoids fixed-format conversion risk while preserving the fullscreen presentation approximation.



## Memory And Compile Guard



- Runtime persistent CPU memory is Vault-owned.
- The runtime stores generation-handle descriptors and resolves phase-local `NativeArray` views only while writing or uploading.
- SHINOBU_232 keeps 64-byte parameter kernels as unmanaged pointer carriers; cold init compiles Burst `FunctionPointer` entrypoints from XML kernel names.
- Missing pointers set `FaultBurstKernelUnavailable`, suppress GPU upload, and try a BlackBox dump; no direct C# fallback executes.
- There is no `IJob`, `job.Run`, scheduled `JobHandle`, or hidden completion fence for a one-DTO update.
- The kernel carriers and entrypoints use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` to match Task 14's extracted rollback-adjacent visual reproducibility requirement.
- DTO layout is explicit and editor-audited through `UnsafeUtility.GetFieldOffset` in `AbyssalCausticsLayoutAudit`.


Optional weather snapshot, surface swell, profile, and tuning data is sanitized into `CausticsInputSnapshotDTO` before Burst kernels.

Kernel structs retain no optional producer `NativeArray` fields. Only SHINOBU-owned parameter, telemetry, and telemetry-cursor pointers cross into the write kernel; each has `[NoAlias]`, `[NativeDisableUnsafePtrRestriction]`, and explicit length.



Alias proof depends on `Unity.Burst.CompilerServices`.

`AbyssalCausticsContracts.cs` imports it so `[NoAlias]` stays compile-visible after JobSystem removal. Removing the import or attributes regresses pointer-kernel proof.



- The Burst function-pointer ABI passes a pointer to the stack-local kernel carrier, not the carrier by value.
- `GenerateMockCausticLightingKernelDelegate` and `CalculateCausticParametersKernelDelegate` accept `GenerateMockCausticLightingJob*` / `CalculateCausticParametersJob*`; runtime dispatch calls `Invoke(&job)`, and the Burst entrypoints null-check before `UnsafeUtility.AsRef<T>(job).Execute()`.
- This keeps the hot compiled call to one native pointer and avoids copying the large carrier that already contains Vault lane pointers plus the 128-byte input snapshot.



- GPU parameter upload uses double-buffered `GraphicsBuffer.Target.Constant`.
- Runtime records `_activeConstantBufferFrameIndex` when pending payload becomes active.
- RenderGraph snapshots active buffer during pass recording.
- Command context binds the snapshotted buffer.



- If `SystemInfo.supportsSetConstantBuffer` is false:
  - no double CBuffer pair;
  - service not initialized;
  - no update/late-frame/origin-shift hooks;
  - no active CBuffer publish;
  - `FaultConstantBufferUnavailable` recorded when telemetry is available.
- The RenderGraph feature fails closed through `TryGetActiveConstantBuffer`.
- No projector, cookie, runtime texture bake, or per-object material-caustic fallback is used for unsupported platforms.
- Quest now has explicit depth-texture asset support for this pass; runtime CBuffer capability, XR import state, RenderGraph Viewer output, and device frame cost still require Unity/device capture.



Legacy compute-facing `ICausticsService` accessors were removed after scans found no active consumer of `IsComputeActive`, `CausticsMap`, or `CausticsAup`.

The interface is now only the registry identity marker for the caustics service slot. Active rendering reads owner-published `GraphicsBuffer` only through `TryGetActiveConstantBuffer(out GraphicsBuffer, out uint frameIndex)`.



- BlackBox telemetry is Vault-owned and seeded to zero once when the ring is acquired. The active caustics fault dump route is `Docs/AgentLogs/Dump_1719.bin`.
- The dump path and directory are resolved and created from lifecycle/cold setup before faults.
- `DumpBlackBox()` does not call the path resolver; if lifecycle setup failed, fault export fails closed instead of doing directory work in the fault route.
- Telemetry cursor normalization uses modulo plus negative correction rather than `math.abs`, so `int.MinValue` cannot become a negative ring index.
- Dump writes entries oldest to newest by cursor order.
- Header records live cursor.
- `IOException` and `UnauthorizedAccessException` set `FaultDumpIo`.
- Fault export route does not throw.
- No dump path, directory creation, or telemetry clearing runs in the render frame path.


- The RenderGraph pass does not read the lifecycle singleton.
- The owner publishes only the currently active `GraphicsBuffer` plus frame index after a successful upload; pass recording reads that immutable render snapshot.
- Editor-only tuning/profile bridges use `s_publishedRuntime`, which is assigned only after `GlobalRegistry.Caustics` ownership is proven.
- The two 64-byte constant buffers are created during lifecycle/boot ownership setup.
- `Tick`/`LateFrameTick` return when uninitialized or non-owner; they do not initialize, resolve dump paths, acquire Vault/GPU buffers, or cold-repair.



- Current source placement is the root `Hecton8.Core.asmdef`; no standalone `Hecton8.Rendering.Runtime.asmdef` or caustics asmref exists in the tree.
- SHINOBU_232 did not add any new sibling runtime assembly reference.
- Weather and wave facts are read through the cached Core `IWeatherService` snapshot; SHINOBU_232 does not import Atmosphere DTOs, call Atmosphere runtime helpers, or mutate producer lanes.
- No direct dependency on sibling concrete rendering, physics, celestial, or voxel runtime types is required for execution.
- Optional external data is accepted through existing Vault IDs and cached global services available at boot.

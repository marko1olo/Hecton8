# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_233_COMPUTE_VOLUMETRIC_FOG.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_233 Compute Volumetric Fog Route

Owner: Echelon 7 Atmosphere & Celestial / Volumetric Fog & Light Shafts.

Runtime route:

- `HectonVolumetricParticulateFogFeature` owns the URP RenderGraph pass chain.

- RenderGraph creates transient `_HectonVolumetricFogFrustumGrid`, `_HectonVolumetricFogHalf`, and `_HectonVolumetricFogComposite` textures; persistent RTHandle ping-pong is not used for the main graph outputs.

- `BuildVolumetricFogGrid` writes `_HectonVolumetricFogFrustumGrid` as capped `RWTexture3D<float4>` frustum voxels and dispatches from actual volume dimensions, not half-res screen dimensions.

- `RaymarchVolumetricFog` integrates that grid into `_HectonVolumetricFogHalf`.

- `Hecton_VolumetricFog_DearLie.shader` performs the final raster pass. Pass `DearLieProxy` owns the low/XR analytical dithered depth fog; pass `BilateralComposite` performs depth-aware 3x3 bilateral upsample from `_HectonVolumetricFogHalf`.

- At proxy blend `>= 0.999`, RenderGraph records one raster `Hecton Dear Lie Fog Proxy` pass and returns before scheduling grid, raymarch, or compute composite dispatches. The raster `HectonNoirDepthFogFeature` remains a separate previous-frame depth fog safety lane, not the only SHINOBU_233 output.

- `_HectonVolumetricFogComposite` is a raster camera-color replacement target. Volume and half fog textures remain `R16G16B16A16_SFloat` compute UAVs; the final composite uses the source camera format except `RGBA16F/RGBA32F/None`, which collapse to `B10G11R11_UFloatPack32` to avoid RGBA16F main-color output.

- `_HectonMarineSnowFogDensityTex` and `_AbyssalFlowFieldTexture` are legacy shader-global bridge inputs. They are sampled once into an owner-local bridge snapshot after compute-kernel binding succeeds, then `RecordRenderGraph` consumes that immutable snapshot instead of polling global shader state again. They are treated as previous-frame presentation inputs; this route does not claim same-frame RenderGraph producer dependency until the upstream owners publish graph `TextureHandle`s through a shared resource contract. External RTHandle wrappers are separated from fallback wrappers so invalid bridge inputs cannot poison fallback binding, and frame CBuffer upload happens only after final fallback resource resolution.

- XR cameras are routed through the Dear Lie proxy until a per-eye 3D frustum-grid contract exists. The compute shader uses `RW_TEXTURE2D_X`, `COORD_TEXTURE2D_X`, and `UNITY_XR_ASSIGN_VIEW_INDEX`; 2D kernels compile with `DISABLE_TEXTURE2D_X_ARRAY`, while XR kernels compile without it. There is no runtime compute-keyword mutation from RenderGraph passes. Single-pass XR writes one proxy fog slice per active view only after the source descriptor proves `Tex2DArray` shape and sufficient slices; non-XR and XR multipass keep 2D targets.

- Compute kernel discovery is cold and guarded. `Create()` pre-validates the three required grid/raymarch kernels through `ComputeShader.HasKernel` before any `FindKernel` call; `AddRenderPasses` fails closed if the shader asset lacks the required 2D or XR raymarch kernel. Kernel indices and thread-group sizes reset when the compute asset identity changes, preventing stale index reuse after hot asset swaps.

- Editor validation also inspects the compute and raster shader sources. `VolumetricFogLayoutValidator` rejects shader variant pragmas, verifies the exact 2D/XR grid/raymarch kernel pragma split, and verifies the `DearLieProxy`/`BilateralComposite` fragment passes so shader permutation creep is caught before play mode.

- `HectonVolumetricFogParams` is a 64-byte CBuffer backed by `FogConstantsDTO`.

- `HectonVolumetricFogFrameParams` is a 224-byte CBuffer: ten 16-byte vector lanes followed by a 64-byte inverse view-projection matrix, with runtime offset validation before GraphicsBuffer creation.

Vault buffers:

- `BufferID.ShinobuVolumetricFogParams`: one `FogConstantsDTO`, explicit 64 bytes.

- `BufferID.ShinobuVolumetricFogPointLights`: `PointLightDTO[8]`, deterministic mock lights until lighting owner publishes a route.

- `BufferID.ShinobuVolumetricFogTelemetryRing`: `VolumetricFogTelemetryEntry[300]`.

- `BufferID.ShinobuVolumetricFogExtinctionProfiles`: `WaterExtinctionProfileDTO[16]`.

Handle policy:

- Runtime stores pointer-free `VaultGenerationHandle<T>` descriptors and resolves phase-local `NativeArray<T>` views only inside the active render/setup phase.

- The UI Toolkit tuner reads live fog params and telemetry through existing generation handles plus `TryResolveHandle`; it does not create editor-owned shadow state for the 300-frame ring.

- Vault allocation/growth and fallback GPU allocation are attempted from `Create()` and a throttled pre-enqueue cold repair lane only while native/GPU readiness is missing. The repair cadence is 30 frames, `TryPrepareNativeState` refuses `IDataVault.IsAllocationLocked`, and active render frames do not repair state through `GlobalRegistry`.

- The render feature captures `Time.frameCount` once in `AddRenderPasses` and passes it into the owned render pass setup. Visual phase quantization and telemetry `FrameIndex` use that owner-local frame snapshot; graph recording does not re-read Unity frame time.

- RenderGraph target dimensions are normalized once into positive `fullWidth/fullHeight` values. Reduced-resolution target sizing and `_HectonVolumetricFogFullSize` CBuffer reciprocals consume that sanitized snapshot, not repeated raw descriptor dimensions.

- External bridge RTHandles use two fixed cache slots per bridge. If a producer publishes an invalid/null shader-global texture for a frame, old wrappers are retained but not imported unless they match the current valid texture. If a producer churns beyond the cache, graph recording falls back to the prewarmed 1x1 resources instead of release/realloc loops. Valid abyssal flow bridge textures must be created 3D float4 resources (`R16G16B16A16_SFloat` or `R32G32B32A32_SFloat`).

- `RecordRenderGraph` performs no Vault acquisition, fallback allocation, or RTHandle allocation.

- Diagnostic dump I/O is deferred out of telemetry ring writes. The ring records the fault frame first; file export is attempted from the same 30-frame cold maintenance gate that repairs missing native/GPU state, before normal setup timing starts.

Rollback fence:

Fog constants, frustum grid, point-light presentation payload, CSV extinction profiles, and telemetry are visual-only. They are not Merkle leaves, not `StateRingBuffer` payloads, and not save authority. Rollback rewinds gameplay truth; volumetric fog keeps drifting as a presentation effect.

Scaling:

`HomeostasisBrain.GlobalQualityWeight` continuously drives ray steps, proxy blend, internal resolution scale, frustum-grid XY cap, light count, and shader noise octaves. `ResolveProxyBlendForQuality` holds full proxy below quality 0.12 through saturated polynomial input, not a binary step, then fades continuously toward volumetric contribution. Low and XR use the Dear Lie screen-space dither proxy; those proxy frames also skip CPU inverse view-projection construction, 3D volume descriptor construction, external 3D bridge imports, and all fog compute dispatches. Near-proxy quality scales volumetric contribution before grid dispatch, so a mostly-proxy frame does not pay full grid resolution. Middle uses small capped grid dimensions with low Z. High and Ultra spend extra budget on more grid coverage, slices, flow advection, and light scattering before the raster bilateral composite.

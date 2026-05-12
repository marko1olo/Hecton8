# RENDER_VFX_POST Rationale

Status: PENDING VERIFICATION

## Initial Decision: Unified Presentation Fake

Problem: Visor damage, water distortion, heat haze, pressure warp, hypoxia, blood edge tint, lens dirt, and stress vignette are separate perceptual effects that can multiply fullscreen pass cost and fill-rate on MX350.
Solution: Consolidate into one URP RenderGraph fullscreen shader pass with one scene-color sample per pixel. Use packed textures plus ALU/dither fakes for chroma, cracks, dirt, pressure, and hypoxia instead of separate transparent overlays.
Rejected Alternatives: Separate Unity Volume overrides and multiple transparent overlay quads were rejected because they repeat fullscreen work, increase SetPass/fill-rate, and violate the "visual fake first" and post-stack budget mandates.
Scalability potential: Low disables heat haze displacement and uses CA/vignette/crack threshold only. Middle enables one-wave haze. High enables stronger pressure/dirty-lens response. Ultra can spend the same unified pass on heavier crack/lens richness without adding new passes.
Hardware Impact: Estimated low-end i3/MX350 gain: 300-900 microseconds versus 3-5 independent fullscreen/transparent overlay passes, pending profiler proof.

## Initial Decision: No Temporal Accumulation

Problem: AUP shifts can invalidate temporal history and smear screen-space effects across a rebase.
Solution: Implement the first version without persistent temporal buffers. Add an explicit shift-frame uniform so any later temporal branch can hard reset when `AupShiftSignal` increments the frame id.
Rejected Alternatives: Temporal distortion accumulation was rejected because task scope requires shift safety and no existing post-process temporal owner was confirmed.
Scalability potential: Low/Middle stay history-free. High/Ultra can add temporal smoothing later only behind shift-frame invalidation and profiler proof.
Hardware Impact: Estimated MX350 gain: 40-120 microseconds and zero extra RT memory because no history target is allocated.

## Decision: Single-Sample Chromatic Damage

Problem: Real chromatic aberration costs 2-3 scene-color taps, but the task requires the Uber pass to sample scene color exactly once per pixel.
Solution: Use one `_BlitTexture` sample after all UV distortion and fake chroma through channel bias at the screen edge, driven by health, hull stress, and player stress.
Rejected Alternatives: Three-tap RGB channel splitting was rejected because it violates the explicit one-sample pass contract and repeats the same MX350 fill-rate problem the task is removing.
Scalability potential: Low uses chroma bias plus vignette only. Middle adds pressure warp. High and Ultra spend saved taps on crack texture, blue-noise dirt, and stronger heat/pressure math inside the same pass.
Hardware Impact: Estimated i3/MX350 gain: 120-260 microseconds versus a 3-tap CA pass at 1080p, pending profiler proof.

## Decision: Decoupled Global Signals

Problem: Parallel agents may own survival, combat, and status-mask writers; directly consuming queues such as `HypoxiaSignal` or `AupShiftSignal` inside rendering would steal events from authoritative systems.
Solution: Read stable shader globals and core UI slots for `HealthFraction`, `LocalTemperature`, `AmbientPressure`, `PlayerStress01`, `HypoxiaSignal`, and `StatusMask`. Use `HectonFloatingOrigin.CurrentShiftSequence` as the AUP reset frame marker. No queue drain, no new direct gameplay dependency.
Rejected Alternatives: Dequeueing `GlobalSignals.TryDequeueHypoxia` or `TryDequeueAupShift` in the render pass was rejected because it would create cross-domain ownership bugs and event loss.
Scalability potential: Low/Middle use existing UI/global scalar bridges. High/Ultra can publish richer globals later without changing renderer ownership.
Hardware Impact: Estimated low-end gain: 5-20 microseconds saved by avoiding managed event fanout or per-frame component searches beyond the existing player context path.

## Decision: RenderGraph Compatibility Contract

Problem: The prompt requires `builder.UseColorBuffer` and `builder.UseDepthBuffer`; installed Unity 6000 also exposes the newer raster builder API with `SetRenderAttachment`.
Solution: Implement `RecordRenderGraph` with the existing project fullscreen pattern and the legacy RenderGraph builder APIs, explicitly calling `ReadTexture`, `UseColorBuffer`, and `UseDepthBuffer`. CS0618 is scoped to the callsite only.
Rejected Alternatives: `AddUnsafePass` was rejected for this pass because it does not satisfy the literal prompt requirement. A pure raster pass was rejected because it would not call the requested APIs.
Scalability potential: Low/High/Ultra all share one pass. Future migration can swap the builder to raster attachments after the prompt-specific compliance window ends.
Hardware Impact: Estimated neutral runtime cost; the compatibility choice affects compile API surface, not shader ALU or texture fetch count.

## Decision: Legacy Double-Processing Kill Switch

Problem: Existing retina, visor-fluid, Unity Volume chromatic aberration/lens distortion, and volumetric shaft lens/haze settings can double-process the same fullscreen damage/dirty-lens work.
Solution: Add render-pipeline validator repairs that require `HectonVisorUberPostFeature`, disable old retina and visor-fluid features, zero duplicated shaft lens/haze settings, and deactivate default Volume CA/Lens Distortion components.
Rejected Alternatives: Leaving old features active during rollout was rejected because it hides the real performance and visual cost of the new Uber pass.
Scalability potential: Low tier gets one deterministic fullscreen path. High/Ultra can increase parameters in the same pass instead of stacking new passes.
Hardware Impact: Estimated MX350 gain: 250-700 microseconds by removing redundant fullscreen passes and transparent opaque-texture sampling, pending profiler proof.

## OMEGA POLISH CHANGES

Problem: The first heat-haze implementation used nested sine waves per pixel. It satisfied the visual fake requirement but spent extra ALU for motion that will be hidden by visor grime and pressure distortion.
Solution: Reduced heat haze to two direct sine waves, one per UV axis, still driven by `_Time.y` and `LocalTemperature`. Low tier still forces amplitude to zero.
Rejected Alternatives: A 1D LUT was rejected for this pass because the prompt explicitly required `sin(uv * freq)` math and a LUT would add another texture dependency.
Scalability potential: Low/Middle avoid nested trigonometry. High/Ultra can raise amplitude/frequency in the same pass without adding texture taps or history.
Hardware Impact: Estimated i3/MX350 gain: 3-8 microseconds in the active haze path, pending shader profiler proof.

## R&D Continuation: Transparent Opaque Texture Purge

Problem: `SuitVisor.shader` was still a transparent visor path with `_CameraOpaqueTexture` declaration and two scene-color samples. That kept the old fill-rate dependency alive after the Uber pass and forced a second presentation authority for refraction/chromatic damage.
Solution: Removed the `_CameraOpaqueTexture` declaration and samples from `SuitVisor.shader`. Replaced the transparent scene-refraction feed with a deterministic procedural surrogate built from base visor color, fresnel, HUD tint, glare data, radial edge, runoff mask, and a hash dither. Converted the visor path toward cutout/depth/stencil behavior with `ZWrite On`, `AlphaToMask On`, dithered `clip`, and close-depth alpha fade so it stops behaving like a permanently blended full-screen pane. Perspective divisions now use `rcp(max(...))`.
Rejected Alternatives: Keeping the opaque texture samples was rejected because it preserves the exact debt task 14 exposed. Adding another blit or downsample refraction texture was rejected because the Uber pass already owns screen distortion. Standard transparent blending was rejected because it burns fill-rate on MX350 and fights deterministic visor masking.
Scalability potential: Low uses the procedural surrogate and dithered alpha without scene fetches. Middle keeps hazard/glare chroma fakes. High/Ultra spend the saved bandwidth inside `HectonVisorUberPost.shader` on dirt/crack/haze intensity instead of restoring transparent refraction.
Hardware Impact: Estimated i3/MX350 gain: 120-300 microseconds from deleting two opaque-texture taps in the visor shader plus 40-150 microseconds from reduced transparent blending pressure, pending Unity profiler/Frame Debugger proof.
Regression Risk: Alpha-test/stencil visor behavior can change edge softness, MSAA coverage, and sorting against close geometry. Requires Unity import, Game View capture, and Frame Debugger verification before it can be called visually accepted.

## R&D Continuation: Textureless Fallback and Variant Removal

Problem: The new Uber pass depended on optional crack, dirt, and blue-noise textures for visible richness. When the validator adds the feature without asset binding, fallback black/white/gray textures can collapse cracks/dirt into weak or absent visuals while still allowing extra texture fetches. `_QUALITY_MX350` also created a shader-variant dependency for a runtime-created material, which can be stripped in a player build if no asset material references that keyword.
Solution: Added `_HectonUberTextureFlags` from C# and uniform shader branches. If assets are assigned, High/Ultra can use packed crack/dirt/blue-noise textures. If assets are missing, the pass uses procedural crack veins, procedural grime streaks, and IGN/hash dither with no optional texture fetches. Removed `_QUALITY_MX350` and the C# keyword mutation; low-tier heat haze is disabled through `_HectonUberLowTier` and zero amplitude.
Rejected Alternatives: Keeping blank fallback textures was rejected because it makes the pass look under-authored when renderer assets are auto-repaired. Keeping the keyword was rejected because stripped variants are a build-time failure mode and adding a ShaderVariantCollection for one low-tier branch is bloat. Runtime Texture2D generation was rejected because it adds cold asset lifetime/ownership risk for a visual fake that ALU can cover.
Scalability potential: Low/Middle use procedural cracks/grime/noise and skip optional texture fetches when no assets are bound. High/Ultra can bind hero packed textures for art-directed crack silhouettes and dirt breakup without changing the pass architecture.
Hardware Impact: Estimated i3/MX350 gain: 8-35 microseconds when optional textures are unbound because three decorative texture fetches become ALU branches. Variant removal saves build/variant memory risk, not measurable frame time.
Regression Risk: Procedural crack layout is deterministic but not art-directed. Requires Unity screenshot review to tune perceived crack density and grime strength.

## R&D Continuation: Exact Bleeding Gate

Problem: The blood edge tint reconstructed `StatusMask & Bleeding` inside HLSL from a float uniform. That is brittle: large bitmasks lose low-bit precision in float form, and fallback to global `_StatusMask` can leave stale blood tint after the authoritative player context has cleared bleeding.
Solution: Keep status ownership on CPU. `HectonVisorUberPostFeature` reads `PlayerRuntimeContext.SurvivalState.StatusMask`, falls back to `HectonSurvivalSystem.StatusMask` only inside the active player context, converts bit 0 to `_HectonUberBleeding01`, and the shader consumes that scalar directly. Removed the GPU `_HectonUberStatusMask` uniform and `_StatusMask` global fallback.
Rejected Alternatives: GPU modulo/floor bit extraction was rejected because float masks are not a safe integer transport. Reading a global shader float was rejected because exact player context exists and zero is a valid authoritative value, not a reason to fall back to stale global data.
Scalability potential: Low/Middle/High/Ultra all share the same scalar bleeding gate. Future status overlays can add explicit scalar gates per visual effect instead of overloading a float bitfield.
Hardware Impact: Estimated gain is negligible frame time, roughly <1 microsecond, but removes one material float upload and one shader arithmetic path. Correctness impact is the actual reason.
Regression Risk: If another agent expected `_StatusMask` global alone to drive visor blood without player context, that path is now intentionally ignored because the pass already requires the active player camera context.

## R&D Continuation: Low-Tier Cache and Hidden Pass Classification

Problem: `SystemInfo.graphicsMemorySize` was queried from `AddRenderPasses` through `IsLowTier`, so every render-camera path paid a native hardware query for a device property that is effectively static during a session. The hidden fullscreen shader also declared Transparent tags, which could mislead renderer audits even though RenderGraph owns the actual pass target.
Solution: Cache low-tier classification per renderer feature instance and recompute only when `lowTierVideoMemoryMb` changes. Keep the low-tier result as a uniform heat-haze amplitude gate. Change `HectonVisorUberPost.shader` tags to `RenderType=Opaque` and `Queue=Geometry`; the pass still uses `ZTest Always`, `ZWrite Off`, and RenderGraph color/depth declarations.
Rejected Alternatives: Per-frame hardware queries were rejected because they do not buy visual quality. A runtime quality keyword was rejected again because variant stripping is a larger build risk than a uniform branch. Leaving Transparent tags was rejected because this is not a transparent material path and should not be counted with alpha-overdraw debt.
Scalability potential: Low retains the cached MX350 heat-haze kill switch. Middle/High/Ultra keep the same one-pass architecture and can spend saved CPU/native-call noise on richer same-pass shader parameters, not extra passes.
Hardware Impact: Estimated gain is small, roughly <1-3 microseconds per active render camera, but removes a native query from the hot render setup path. The tag change is audit/maintenance hygiene, not expected frame time.
Regression Risk: If a developer changes `lowTierVideoMemoryMb` at runtime, the cache updates because the threshold is part of the cache key. If the GPU memory report itself changes mid-session, the cache will not chase it; Unity hardware memory is treated as session-static.

## R&D Continuation: Shader Portability Tightening

Problem: The shader used scalar swizzle shorthand (`frameSalt.xx`, `shiftSalt.xx`, `luma.xxx`). Some HLSL compilers accept scalar swizzles, but this is not worth carrying in a new URP hidden shader when Unity import is still blocked by external compile errors.
Solution: Replace scalar swizzles with explicit `float2(frameSalt, frameSalt)`, a local `salt2`, and `half3(luma, luma, luma)`. The math and one-sample contract are unchanged.
Rejected Alternatives: Waiting for Unity shader import to fail was rejected because this is cheap preventive cleanup. Rewriting dither/hypoxia logic was rejected because no visual formula change was needed.
Scalability potential: Low/Middle/High/Ultra all get the same shader source portability. Optional art textures and low-tier amplitude gates remain unchanged.
Hardware Impact: Frame-time neutral. The value is shader compile resilience, not performance.
Regression Risk: No expected visual delta; constructors are mathematically equivalent to the intended scalar splats.

Exact cinematic cheats used:
- Single-sample chromatic aberration: channel bias, not RGB scene re-sampling.
- Heat haze: sine UV fake, no thermal simulation.
- Textureless cracks/grime/noise: deterministic ALU fallback, no runtime texture generation.
- Pressure warp: radial barrel term, no physical lens/camera model.
- Lens dirt: blue-noise dithered multiply, no transparent blend pass.
- Blood: status-bit edge tint, no blood texture.
- Bleeding gate: CPU bit test to scalar, not float bitfield math in shader.
- Low-tier heat-haze gate: cached hardware classification plus uniform amplitude zeroing, not pass swapping.
- Hypoxia: grayscale lerp, no Volume stack.

Final Git Diff scope:
- `Assets/_Project/Art/Shaders/HectonVisorUberPost.shader`: new single-sample fullscreen shader.
- `Assets/_Project/Art/Shaders/SuitVisor.shader`: removed legacy transparent `_CameraOpaqueTexture` path and replaced it with a procedural visor scene surrogate.
- `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs`: new RenderGraph feature and runtime scalar binder.
- `Assets/_Project/Editor/HectonRenderPipelineValidator.cs`: validator repairs for Uber pass and legacy post disable.
- `Docs/AgentLogs/RECON_RENDER_VFX_POST.md`: shader recon output.
- `Docs/Tasks/Status_RENDER_VFX_POST.md`: task state and blocked compile evidence.

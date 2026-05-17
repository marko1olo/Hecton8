# Rationale_SCREEN_SPACE_REFRACTION

Prompt: SCREEN_SPACE_REFRACTION
Domain: VFX/POST
Status: CORE COMPLETE / CSHARP BUILD PASSED WITH SOURCELINK DISABLED / UNITY RUNTIME PENDING

## Decision 0 - Mandate Selection

Problem: Visor refraction touches rendering, shader sampling, low-tier GPU budget, visual fake policy, and stress-driven damage feedback.
Solution: Use eight mandates: visual fake first, frame/VRAM budgets, zero GC, URP RenderGraph, noir shader doctrine, descriptor binding reality, visor stencil masking, and hull-stress feedback.
Rejected Alternatives: Reading unrelated AI/physics/data mandates would expand scope without changing the rendering contract.
Scalability potential: Low uses chromatic-only offset; Middle uses one mask/normal perturbation; High adds richer droplet normals; Ultra can push stronger stress/dirt response after profiler proof.
Hardware Impact: Low-end i3/MX350 avoids per-object GrabPass and raytracing; expected gain is reduced RT churn and one bounded post pass instead of repeated captures. Exact microseconds pending measurement.

## Decision 1 - Authority Boundary

Problem: The task domain is limited to Assets/_Project/Art/Shaders/Post/ while RenderGraph features may live in Scripts/Visor or rendering folders.
Solution: Inspect existing first-party renderer features and place shader code in the assigned post shader domain; only edit C# feature code if an existing owner already consumes that shader.
Rejected Alternatives: Creating a new global rendering manager or singleton violates the batch decoupling rule and widens domain ownership.
Scalability potential: Shader-owned quality branches preserve tier control without new runtime service coupling.
Hardware Impact: Minimal CPU impact if implemented as existing RenderGraph pass plus material uniforms; no managed hot-path allocations expected.

## Decision 2 - Opaque Texture Instead of GrabPass

Problem: Existing visor glass synthesized a fake scene color and the fluid feature used `AddBlitPass`, leaving no literal `_CameraOpaqueTexture` refraction path.
Solution: Reuse URP opaque texture and declare it in RenderGraph; the mesh shader samples `_CameraOpaqueTexture`, and the fluid pass binds `cameraOpaqueTexture` through `AddRasterRenderPass`.
Rejected Alternatives: Unity `GrabPass`, per-renderer capture, or raytraced refraction are over budget and break URP RenderGraph ownership.
Scalability potential: Low uses opaque color with chromatic-only offsets; Middle/High use bounded normal perturbation; Ultra can raise authored normal/droplet signal while staying inside the same shader path.
Hardware Impact: Expected MX350 gain is avoiding extra grab/copy per glass object. Exact microseconds pending profiler; static result removes legacy blit feature path.

## Decision 3 - Depth and Dirt Gates

Problem: Blind UV offsets bend foreground pixels and look wrong when dirt/frost/cracks should obscure refraction.
Solution: Gate Snell offset by depth comparison and inverse dirt mask; clamp all perturbation to `[-0.1, 0.1]`.
Rejected Alternatives: Full-screen unconditional distortion is cheaper to write but violates the depth-test and mask-dirt tasks.
Scalability potential: Low keeps only chromatic split; higher tiers spend samples only where glass is clean enough to show refraction.
Hardware Impact: A few ALU ops replace visually incorrect over-refraction. GPU cost pending; foreground/dirt rejection reduces high-tier samples where dirt blocks the effect.

## Decision 4 - LUT and Signal Flow

Problem: Snell response needs water-density feel without binding directly to another agent's concrete implementation.
Solution: Use a four-float IOR LUT for air, water, dense water, and glass; feed `_HectonWaterDensitySignal` from an existing shader global or `GlobalRegistry.FluidSimulation` density when available.
Rejected Alternatives: Per-material cloned arrays and direct `SubmarineFluidDynamics` references were rejected because they create brittle cross-domain dependencies.
Scalability potential: Low ignores most LUT-driven Snell and uses chromatic-only; Middle uses LUT bend; High/Ultra can increase authored LUT contrast and droplet masks without C# shape changes.
Hardware Impact: i3/MX350 path pays one scalar upload on value change and avoids new managers. GPU cost is bounded ALU; exact microseconds pending profiler.

## Decision 5 - Stress and Tier Fallback

Problem: Full normal perturbation during high hull stress can add shimmer and sample cost when the image is already unstable.
Solution: Use existing hull-stress signals and a memory threshold to degrade to chromatic-only sampling under stress or low-tier hardware.
Rejected Alternatives: A physical vibrating-glass simulation was rejected as frame-budget waste and visually less controllable.
Scalability potential: Low/Middle keep fake chroma; High keeps Snell normal perturbation; Ultra can increase wet-mask coverage while retaining the same fallback.
Hardware Impact: Low-end silicon avoids the high path under stress. Exact savings are pending profiler, but static cost removes one Snell branch and opaque blend path when fallback is active.

## Decision 6 - RenderGraph API Drift

Problem: The old fluid post used `RenderGraphUtils.AddBlitPass`, which does not expose explicit opaque/depth texture binding for the Snell path.
Solution: Replace it with `AddRasterRenderPass`, declare color/depth/opaque reads, set the destination attachment, and bind `_BlitTexture`, `_CameraDepthTexture`, and `_CameraOpaqueTexture` in the render function.
Rejected Alternatives: Keeping `AddBlitPass` and relying on implicit globals was rejected because depth/opaque availability would be fragile across URP versions.
Scalability potential: The same pass supports low chromatic, middle Snell, high droplet perturbation, and ultra stronger masks by material parameters only.
Hardware Impact: CPU path remains one RenderGraph pass. Exact microseconds pending profiler; static benefit is removing the incompatible blit utility path and avoiding per-object grab copies.

## Decision 7 - Compile Wall Boundary

Problem: Validation build is blocked by unrelated missing namespace/type/interface errors outside VFX/POST.
Solution: Mark RenderGraph compile verification blocked after three unrelated compile walls and keep changes scoped to visor/refraction ownership.
Rejected Alternatives: Editing fauna, AI perception, animation IK, global resolution scaler, or marine snow listener signatures would violate domain boundaries and interfere with other agents.
Scalability potential: No impact to low/middle/high/ultra path; blocker is integration dependency, not runtime design.
Hardware Impact: No runtime impact from the blocker. Exact microseconds remain unmeasured until the shared project compiles.

## Decision 8 - Foreground and Dirt Rejection

Problem: Screen-space refraction bends the wrong pixels if foreground geometry or opaque dirt is ignored.
Solution: Mesh visor refraction samples scene depth and gates offset by `HectonDepthBehindMask`; both visor paths multiply intensity by inverse dirt/grime/frost/crack/dust masks.
Rejected Alternatives: Uniform screen offset and clean-glass-only assumptions were rejected because they fail under noir dirt/frost visual language.
Scalability potential: Low still uses dirt-gated chroma; Middle/High use dirt-gated Snell; Ultra can increase dirt mask texture detail without changing the branch contract.
Hardware Impact: One depth sample plus ALU on the mesh visor path; exact cost pending profiler. Low-end avoids wasting high-path distortion where dirt masks conceal it.

## Decision 9 - Polish Mandate Honesty

Problem: The polish mandate requests VERIFIED MASTER GRADE, but code build alone does not prove Unity runtime shader compilation, platform shader translation, RenderGraph frame ordering, or profiler microseconds.
Solution: Keep status as core complete/build passed with caveat, record the exact warning and missing runtime/profiler evidence, and avoid fabricated microsecond numbers.
Rejected Alternatives: Claiming master-grade verification from C# build only, or editing unrelated agent files to force earlier build walls forward, would be a false report.
Scalability potential: Low/Middle/High/Ultra design is present; certification now waits on Unity runtime, platform shader compilation, Frame Debugger ordering, and GPU profiling.
Hardware Impact: Exact microseconds saved are not measured. The proven value is static removal of legacy blit/grab-style paths, bounded shader branches, and C# build success with SourceLink disabled.

## Decision 10 - DataVault Blackbox Correction

Problem: The prior N/A blackbox judgment was too weak for the continuation mandate; the visor post now has CPU-side runtime state worth preserving when NaN input hits the GPU boundary.
Solution: Store a 300-entry `VisorRefractionTelemetryEntry` ring in `GlobalRegistry.DataVault` with `BufferID.VisorRefractionBlackBox`, `SystemID.Vfx`, explicit 48-byte packed layout, generation checks, and one-shot binary dump only on non-finite input.
Rejected Alternatives: A private persistent `NativeArray` would violate data sovereignty; per-frame text logging would violate Steam Deck/MicroSD I/O pressure; a managed list would violate zero-GC.
Scalability potential: Low records the same compact heartbeat while rendering chromatic-only; Middle records Snell state; High records visual-overkill state; Ultra can widen telemetry fields later only through a versioned packed struct.
Hardware Impact: i3/MX350 cost is one 48-byte DataVault write when the player camera is evaluated, exact microseconds unmeasured. Quest/ARM64 uses `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`, so no implicit padding assumption is required.

## Decision 11 - Multiplatform Shader Target

Problem: The fullscreen visor fragment shader declared target 4.5 without compute, UAV, or SM4.5-only behavior, increasing mobile/Metal risk for no visual return.
Solution: Lower the shader target to 3.5 and keep all refraction/salt work in fragment ALU with URP texture macros.
Rejected Alternatives: Keeping SM4.5 was unnecessary; compute/raymarch/POM additions were rejected inside this low-cost post pass because they would violate the original MX350 objective.
Scalability potential: Low/MX350 gets chromatic-only refraction and no salt growth; Mid gets normal Snell; High gets salt crystal ALU; Ultra gets stronger salt growth through `_HectonVisorFluidVisualOverkill`.
Hardware Impact: Metal/Mac and Quest avoid needless shader-model pressure. Exact GPU microseconds are unmeasured; CPU impact is 0.0 us/frame.

## Decision 12 - Visual Overkill Without Domain Creep

Problem: The continuation asked for god-mode spectacle, but volumetric silt wakes and dented hulls belong to fluid/vehicle domains, not this VFX/POST visor refraction task.
Solution: Add domain-valid procedural salt-crystal growth on wet clean visor glass, gated by quality tier and inverse dirt/depth masks, using no extra textures, no particles, and no I/O.
Rejected Alternatives: Raymarching, 16-tap POM, SSS, hull dent simulation, or silt particle systems were rejected here because they create out-of-domain dependencies and exceed the low-cost Snell contract.
Scalability potential: Toaster mode remains a Dear Lie: chromatic aberration plus dirt/depth gates. Middle uses one bounded Snell perturbation. High adds sparse salt glints. Ultra increases salt density/growth via the same uniform without changing render topology.
Hardware Impact: Low-end i3/MX350 sees 0.0 CPU cost and the salt branch forced off. Top-tier devices spend only gated fragment ALU on clean wet visor pixels; exact GPU microseconds need Unity profiler after the shared build compiles.

## Decision 13 - Continuation Compile Wall

Problem: The shared project still does not compile after visor static verification.
Solution: Re-run `dotnet build` and record the then-current first blockers: UI compass blackbox/visual overkill drift, lockstep replay header drift, homeostasis missing buffers/helpers, item pickup missing `ItemAcquiredSignal`, and tether signal type constraints.
Rejected Alternatives: Editing UI, determinism, core homeostasis, item, or physics signal code would exceed the VFX/POST assignment and collide with other agents.
Scalability potential: Refraction Low/Mid/High/Ultra paths remain implemented; certification is blocked by unrelated integration debt.
Hardware Impact: No runtime impact from this blocker. Exact visor microseconds remain unmeasured until the compile wall is cleared.

## Decision 14 - Literal NativeArray Eviction

Problem: Even a DataVault-owned `NativeArray` alias in the visor feature left a literal private `NativeArray` declaration, which is too easy to misread as local ownership and fails the stricter H-Phi mandate.
Solution: Replace the alias with `VaultBufferHandle<VisorRefractionTelemetryEntry>`, resolve the current pointer through the vault each write, and index the telemetry ring by `Time.frameCount % 300` instead of storing a private cursor.
Rejected Alternatives: Keeping the alias with a rationale comment was rejected because the file still contained the forbidden type token; adding a separate cursor buffer was rejected as unnecessary for a fixed frame-indexed ring.
Scalability potential: Low, Middle, High, and Ultra all emit identical compact telemetry while their visual paths diverge; the handle remains generation-checked across vault relocation/fence events.
Hardware Impact: Same 48-byte heartbeat write when evaluated. Removing cursor/last-frame state saves two small field updates per evaluated player-camera frame, exact microseconds unmeasured.

## Decision 15 - Compile Wall Retry

Problem: The first retry hit a shared SourceLink file lock; the second retry reached C# and failed outside the visor domain.
Solution: Record the then-current blockers without touching unrelated domains: `HectonXRRuntimeState`, `BiolumPulseSyncRuntime`, `VaultProbeUtility`, `SpatialAudioManager`, and `SubmarineStructuralGrid`.
Rejected Alternatives: Fixing XR refresh-rate APIs, biolum telemetry structs, vault diagnostics, audio residency helpers, or submarine breach buffers would exceed the VFX/POST assignment.
Scalability potential: No change to visor scalability; the compile wall is integration debt.
Hardware Impact: No runtime impact from the blocker. Exact visor microseconds remain unmeasured until the shared project compiles.

## Decision 16 - Silt Overkill As A Dear Lie

Problem: The god-mode request wants suspended silt, but real volumetric silt or wake particles are out of scope for screen-space visor refraction and would add cross-domain dependencies.
Solution: Add a High/Ultra-only visor-space suspended-silt shimmer using procedural `ValueNoise`, hashed specks, inverse dirt, depth validity, wetness/rain activity, and the existing `_HectonVisorFluidVisualOverkill` uniform.
Rejected Alternatives: A particle system, fluid wake bridge, raymarching volume, or extra silt textures were rejected because they violate the low-cost Snell contract and Steam Deck I/O pressure.
Scalability potential: Low/MX350 gets 0 contribution because visual overkill is forced to zero; Mid gets reduced/zero depending quality tier; High gets sparse filaments; Ultra gets denser shimmer through the same uniform.
Hardware Impact: CPU impact is 0.0 us/frame. GPU impact is gated fragment ALU only and unmeasured; no new samples, buffers, or disk reads were added.

## Decision 17 - Shared Build Lock Boundary

Problem: The post-silt build retry failed before C# compilation because another process locked `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json`.
Solution: Record the workspace lock and avoid terminating unknown concurrent agent build processes.
Rejected Alternatives: Killing all `dotnet` processes would violate multi-agent safety and could corrupt other agents' validation runs.
Scalability potential: No impact to visor Low/Mid/High/Ultra paths.
Hardware Impact: No runtime impact. Exact visor microseconds remain unmeasured.

## Decision 18 - Shader Uniform NaN Hardening

Problem: Several visor refraction shader branches still relied on raw `saturate()` for externally-fed uniforms, and the shared Snell helper accepted raw `nDotV`/`strength`. On mobile/Metal-class GPUs, a non-finite wetness, stress, rain, visual-overkill, or Snell-strength input can poison downstream UV math before the final clamp.
Solution: Route refraction-critical uniforms through `HectonFinite01`, guard `_HectonVisorSnellStrength` with explicit `isfinite`, and finite-guard `nDotV` plus `strength` inside `Hecton_SnellRefractionCore.hlsl`.
Rejected Alternatives: A C#-only sanitation claim was rejected because shader-side guards are the last boundary before GPU texture coordinates; a per-call-site-only fix was rejected because the shared helper should fail closed for every present and future caller; a broad whole-visor rewrite was rejected as out of scope and higher regression risk.
Scalability potential: Low remains chromatic-only and finite-gated. Middle keeps bounded Snell. High keeps finite-gated salt crystals and silt shimmer. Ultra keeps visual overkill without allowing non-finite uniforms to escape into opaque texture sampling.
Hardware Impact: CPU impact is 0.0 us/frame. GPU impact is a small finite-check ALU cost on affected fragments, exact microseconds pending profiler. The change buys stability on i3/MX350, Quest/ARM64, and Metal without adding textures, buffers, I/O, or render passes.

## Decision 19 - Build Recovery Boundary

Problem: Earlier validation attempts were blocked by unrelated compile drift and a shared SourceLink lock, so the refraction work could not be honestly certified beyond static analysis.
Solution: Retry the same non-shared-compiler build after the NaN hardening pass and record the checkpoint-green code compile result without expanding scope into unrelated domains.
Rejected Alternatives: Killing unknown concurrent build processes was rejected; editing unrelated blockers was rejected; claiming Unity runtime/profiler verification from a `dotnet build` was rejected.
Scalability potential: Low/Middle/High/Ultra shader paths are now code-build verified. Runtime visual quality, Frame Debugger ordering, GCMonitor, and GPU microseconds still require Unity execution.
Hardware Impact: Code compile succeeded at that checkpoint with 0 warnings and 0 errors. Later retries are now superseded by unrelated `PredatorCognitionDomain.cs` and `DroneFleetManager.cs` blockers, and no profiler data exists yet, so exact MX350/Quest/Metal/4090 microseconds remain pending.

## Decision 20 - Common Snell Boundary Hardening

Problem: The shared Snell helper still allowed non-finite IOR LUT, depth, softness, and clamp-bound inputs to enter `max`, `smoothstep`, and `min` before the final UV clamp. The water-density fallback also sanitized invalid density without tagging the blackbox.
Solution: Fail closed in `Hecton_SnellRefractionCore.hlsl` by substituting stable IOR defaults, finite depth/softness values, and zero clamp bounds when inputs are non-finite. Pass telemetry flags into `ResolveWaterDensitySignal01` so invalid shader-global or fluid-simulation density is recorded before the safe zero fallback.
Rejected Alternatives: Trusting material settings and upstream systems was rejected because shared shader helpers must be the final GPU boundary; editing `SubmarineFluidDynamics` was rejected because it is outside VFX/POST and currently belongs to another integration wall.
Scalability potential: Low/MX350 stays chromatic-only with finite masks. Middle keeps bounded Snell. High/Ultra keep salt and silt fakes with stronger common-helper immunity. No new texture, buffer, render pass, or signal was added.
Hardware Impact: CPU cost is two finite checks when the player camera is evaluated; exact microseconds pending profiler. GPU cost is small ALU in the common Snell helper; exact microseconds pending profiler. That build retry was later superseded by the vector-global guard retry, then by the shared SourceLink file-lock retry.

## Decision 21 - Shader Vector Global Guard

Problem: The fullscreen visor shader still trusted vector globals for local velocity, screen-space rain parameters, and global wind before droplet flow, suspended-silt drift, rain exposure, and `rsqrt` wind direction math. C# sanitation helps, but it is not the final boundary before GPU UV/sample math.
Solution: Add `ResolveFinite4`, resolve `_HectonVisorFluidLocalVelocity` once in `Frag`, pass that safe vector into droplet, silt, and refraction offset functions, and resolve `_HectonScreenSpaceRainParams` plus `_GlobalWind` before rain density/exposure and wind normalization.
Rejected Alternatives: C#-only sanitization was rejected because shader globals can be overridden by other systems or material state; a whole-shader rewrite was rejected because the local boundary guard solves the actual non-finite risk without changing render topology; editing fauna/construction build blockers was rejected as out-of-domain.
Scalability potential: Low/MX350 still uses chromatic/depth/dirt fakes and receives finite-safe flow inputs. Middle keeps bounded Snell. High and Ultra keep salt crystals and suspended silt visual overkill, but invalid vector globals now collapse to stable zero/fallback motion instead of corrupting opaque texture sampling.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is a small vector finite-check ALU boundary before affected branches, exact microseconds pending Unity profiler. Later retries were superseded by the dynamic division guard build, which succeeded with SourceLink disabled.

## Decision 22 - Shared SourceLink Lock Boundary

Problem: That validation retry did not reach C# compilation because `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` was locked by another concurrent `dotnet build` process in the shared multi-agent workspace.
Solution: Retry once after a short wait, query active `dotnet.exe` build processes for evidence, record the exact lock, and keep VFX/POST code unchanged.
Rejected Alternatives: Killing unknown build processes was rejected because this workspace is explicitly shared with other agents; deleting `Temp/obj` was rejected because it is destructive to concurrent validation; changing SourceLink project settings was rejected because it is outside the screen-space refraction assignment.
Scalability potential: No change to Low/Middle/High/Ultra visor paths. The lock is validation infrastructure contention, not runtime behavior.
Hardware Impact: No runtime impact. Exact visor microseconds remain pending Unity profiler and a clean build window.

## Decision 23 - Full Visor Scalar Boundary Guard

Problem: After vector guarding, two visor shader paths still had scalar shader globals that could feed NaN into HUD/VR/foveation color math or fluid droplet/refraction motion before final clamps.
Solution: Add finite scalar/vector resolution in `SuitVisor.shader` for HECTON HUD fog/frost, suit-health glitch, VR comfort, foveation, stress vignette, HUD focus blur, and visual-static seed. Add finite scalar fallbacks in `Hecton_VisorFluidDistortion.shader` for droplet scale, runoff speed, edge exponent, streak strengths, distortion strength, depth softness, low-tier/homeostasis flags, Snell strength, water-density input, and ambient dust response.
Rejected Alternatives: C#-only sanitation was rejected because shader globals are the last GPU boundary; a broad physical rewrite was rejected because the problem is invalid scalar inputs, not missing simulation; editing the current tether build blocker was rejected as out-of-domain.
Scalability potential: Low/MX350 keeps chromatic/dirt/depth fakes with fail-closed motion. Middle keeps bounded Snell. High/Ultra keep salt, silt, HUD glitch, VR comfort, and foveated effects without allowing non-finite globals to contaminate final color or UV sampling.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is extra finite-check ALU in the touched visor fragments, exact microseconds pending profiler. That build retry was superseded by the shared-helper consolidation and dynamic division guard passes; latest SourceLink-disabled build succeeded with 1 warning and 0 errors.

## Decision 24 - Shared Finite Helper Consolidation

Problem: The fullscreen fluid shader and mesh visor shader had separate finite scalar/vector helper implementations after the NaN hardening pass. That creates drift risk: one caller can be hardened while another silently diverges.
Solution: Move the reusable scalar/vector finite fallback functions into `Hecton_SnellRefractionCore.hlsl` as `HectonFiniteValue`, `HectonFiniteNonNegative`, and `HectonFinite4`, then make both visor shaders consume those shared helpers.
Rejected Alternatives: Leaving duplicate helpers was rejected because shared Snell safety belongs in the shared refraction core; a broader shader framework refactor was rejected because it adds surface area without improving the current NaN boundary; editing UI/navigation/world build blockers was rejected as out-of-domain.
Scalability potential: Low/MX350, Middle, High, and Ultra now share identical fail-closed shader input semantics while still selecting different visual paths. High/Ultra salt/silt overkill and mesh HUD effects no longer depend on local duplicate helper behavior.
Hardware Impact: CPU cost is 0.0 us/frame. GPU instruction shape is effectively unchanged; helper calls are inlined by shader compilation. Exact GPU microseconds remain pending Unity profiler. A later SourceLink-disabled build after the dynamic division guard succeeded with 0 errors and one SourceLink cleanup warning.

## Decision 25 - Dynamic Division Guard

Problem: Several shader denominators were safe by intent but still written as direct division, which weakens NaN-vaccination evidence for mobile/Metal review.
Solution: Convert dynamic divisions for screen texel size, sonar wave and fade timing, foveated quantization, droplet-grid normalization, and fluid radial direction to bounded reciprocal paths using `rcp(max(...))`.
Rejected Alternatives: Trusting engine screen params and authored sonar values was rejected because the shader should prove its denominator safety; replacing literal Bayer/HUD-box constant divisions was rejected as churn with no runtime safety value.
Scalability potential: Low/MX350 keeps the same cheap chromatic/depth/dirt path with stronger denominator guards. Middle/High/Ultra retain Snell, HUD, salt, silt, and foveated visual paths with explicit divide safety.
Hardware Impact: CPU cost is 0.0 us/frame. GPU ALU is equivalent reciprocal math on already-active shader paths; exact microseconds pending Unity profiler. Latest SourceLink-disabled build succeeded with 1 warning and 0 errors; the warning is the existing locked `Hecton8.Core.sourcelink.json` cleanup.

## Decision 26 - Literal Division And Boundary Guard

Problem: The remaining visor slash audit still contained literal Bayer divisions and a fixed HUD normalization divide, and the mesh visor still trusted several material/engine values around approximate normalization, screen-position W, depth W, close-depth occlusion, static/hazard controls, and refraction-adjacent glass dirt controls.
Solution: Replace literal lookup divisions with multiply constants, convert the fixed HUD box normalization to multiply constants, finite-guard approximate normalization inputs, resolve refraction-adjacent material knobs through shared finite helpers, guard screen/depth W before reciprocal math, reuse finite HUD close occlusion distance, and zero non-finite fullscreen fluid base offsets before deriving the Snell normal.
Rejected Alternatives: Leaving constant divisions was rejected because it keeps automated denominator audits noisy; trusting Unity material ranges and engine screen/depth values was rejected because shader-side NaN vaccination must be the final boundary; editing the transient tether compile wall was rejected because it is outside VFX/POST and was superseded by later green builds.
Scalability potential: Low/MX350 still receives chromatic/depth/dirt fakes with fewer invalid-input escape paths. Middle keeps bounded Snell. High/Ultra keep salt crystals, suspended silt, HUD glitch, grime, and lens-crack polish while invalid controls fail closed rather than contaminating UVs or final color.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is small finite-check ALU and multiply constants replacing compile-time divides; exact GPU microseconds remain pending Unity profiler. SourceLink-disabled and normal `dotnet build` checkpoints both succeeded with 0 warnings and 0 errors, but latest retries are now blocked outside VFX/POST by `EcosystemDirector.cs` duplicate members and `LockstepStateValidator.cs` missing lane constants.

## Decision 27 - Uber Post Tier Shed And Finite Boundary

Problem: `HectonVisorUberPost.shader` was still compiled as target 4.5 without compute/UAV/group-memory behavior, accepted raw post-process globals, allowed low-tier PC to enter the non-mobile 16-tap light-shaft path, and used `pow` inside the tap loop. `SuitVisor.shader` also had remaining raw material/global knobs around droplet density, chromatic split, sonar, hypoxia, HUD tint, smoothness, reflection, and screen-size static noise.
Solution: Lower Uber post to target 3.5, include the shared Snell finite helpers, finite-guard Uber screen/waterline/brine/light/comfort/dirt/crack/physiology controls, hard-return zero light shafts when `_HectonUberLowTier` is active, replace per-tap `pow` with `FastRadialFalloff01`, and route the remaining SuitVisor knobs through finite aliases.
Rejected Alternatives: Keeping target 4.5 was rejected because the shader has no SM4.5-only work; keeping high-sample shafts on MX350 was rejected by the render hot-path mandate; using real volumetric shafts was rejected because this pass is a visual fake layer, not atmosphere simulation; editing `SubmarineFluidDynamics.cs` to force a green build was rejected as out-of-domain.
Scalability potential: Low/MX350 now gets the cheap visor post stack with shafts off, chromatic/depth/dirt fakes intact, and no `pow` loop. Middle can enable bounded post polish through uniforms. High/Ultra keep the 16-tap light shaft fake, salt crystals, suspended silt, grime, cracks, and HUD polish behind finite fail-closed inputs.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost on Low is reduced by skipping the 16-tap shaft loop; exact microseconds require Unity GPU profiler. GPU cost on High/Ultra trades per-tap `pow` for multiply/lerp polynomial ALU; exact microseconds pending profiler. Latest SourceLink-disabled build reaches C# and is blocked outside VFX/POST by `SubmarineFluidDynamics.cs` ambiguous `float3`/`Vector3` subtraction.

## Decision 28 - Uber Fragment UV Closure

Problem: After the Uber post tier shed, helper functions still trusted incoming fragment UVs after XR stereo transform and depth/world reconstruction. Those inputs are normally valid, but a mobile GPU NaN boundary cannot rely on normal editor behavior.
Solution: Sanitize the stereo-transformed fragment UV once in `Frag`, and add helper-local safe UV/world-position fallbacks for internal water masking, droplet masks, comfort masks, lens dirt, light shafts, and brine fog.
Rejected Alternatives: Trusting XR transform output was rejected because this pass samples screen textures directly; a bad UV can poison every later sample. Adding a new CPU validation path was rejected because this is GPU-side input sanitation and should stay in shader code.
Scalability potential: Low/MX350 keeps the cheapest post stack with finite UVs, shafts disabled, chromatic/depth/dirt fakes intact. Middle/High/Ultra keep richer fake shafts, cracks, brine, grime, and waterline polish without allowing invalid fragment coordinates to leak into texture sampling.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is small finite-check ALU in the post fragment path; exact microseconds require Unity profiler. Latest SourceLink-disabled build reaches C# and is blocked outside VFX/POST by `TetherManager.cs` missing `ISlowTickable.SlowTick()`.

## Decision 29 - Screen Params And Helper UV Closure

Problem: The remaining shader audit still found helper-level `uv * _ScreenParams` and HUD/screen hash paths that depended on raw engine screen-size globals or interpolated UVs. These are normally valid, but they are still final-boundary GPU inputs for hash/floor/static paths on Quest/Android/Metal.
Solution: Add `HectonFinite2` to the shared Snell core, sanitize fullscreen/stereo UVs in the fluid pass, sanitize mesh visor `screenUV` after stereo transform, finite-guard helper UVs/tile scales/time values, finite-guard HUD-distorted UVs after additive glitch offsets, and route raw `_ScreenParams`/`_ScaledScreenParams` use through finite local aliases before hash/floor/noise math.
Rejected Alternatives: Leaving these as "engine guaranteed" values was rejected because the NaN-vaccination rule requires the shader's final sampling boundary to fail closed. Duplicating float2 finite helpers in each shader was rejected because shared refraction boundaries should not drift. Editing the current Fauna compile blocker was rejected as out-of-domain.
Scalability potential: Low/MX350 keeps chromatic/depth/dirt fakes and cheap static without invalid UV poisoning. Middle keeps bounded Snell. High/Ultra keep salt crystals, suspended silt, lens grime, pressure cracks, HUD glitch, VR comfort, and BIOS recovery polish behind finite screen-space inputs. No new render pass, texture, buffer, material clone, particle system, or disk read was added.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is small finite-check ALU on active visor/post fragments; exact GPU microseconds require Unity profiler. Latest SourceLink-disabled build reaches C# and is blocked outside VFX/POST by `FaunaBrain.Compatibility.cs` missing `FlagsAttribute`/`Flags`.

## Decision 30 - Shared Depth Boundary Closure

Problem: After UV/screen-param closure, the remaining shader stability risk was raw scene-depth sampling: `SampleSceneDepth`, `_ZBufferParams`, `LinearEyeDepth`, and reconstructed world positions could still feed refraction, brine fog, light shafts, and sonar contour logic without a shared finite boundary.
Solution: Add shared depth helpers to `Hecton_SnellRefractionCore.hlsl`, centralizing reversed-Z invalid-depth selection and finite depth validity. Route fullscreen fluid depth, SuitVisor depth/refraction/sonar depth, and Uber post shaft/brine/waterline depth through these helpers. Guard `_ZBufferParams` before `LinearEyeDepth`, and fail reconstructed world positions to stable fallbacks.
Rejected Alternatives: Repeating local `UNITY_REVERSED_Z` blocks in each shader was rejected because it creates platform drift. Trusting Unity depth globals was rejected because the final GPU math boundary still needs a NaN vaccine. Killing concurrent `dotnet` builds after validation timeout was rejected because this workspace is shared with other agents.
Scalability potential: Low/MX350 keeps chromatic/depth/dirt fakes with invalid depth collapsing to no fullscreen-fluid refraction and stable mobile waterline math. Middle keeps bounded Snell and sonar contour masks. High/Ultra keep 16-tap light-shaft fake, brine fog, salt crystals, silt, lens grime, cracks, and HUD polish without invalid depth or world positions poisoning texture coordinates or final color.
Hardware Impact: CPU cost is 0.0 us/frame. GPU cost is small finite-check ALU around active depth consumers; exact GPU microseconds require Unity profiler. Latest build validation timed out under concurrent `dotnet` build contention, so no new compile result is certified.

# Rationale_SCREEN_SPACE_REFRACTION

Prompt: SCREEN_SPACE_REFRACTION
Domain: VFX/POST
Status: CORE COMPLETE / BUILD BLOCKED BY DEPENDENCY

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

Problem: The polish mandate requests VERIFIED MASTER GRADE, but the shared project build fails in unrelated domains before this work can be fully compiled and profiled.
Solution: Keep status as core complete/build blocked, record the exact blocker list, and avoid fabricated profiler numbers.
Rejected Alternatives: Claiming master-grade verification without a clean build, or editing unrelated agent files to force the build forward, would be a false report.
Scalability potential: Low/Middle/High/Ultra design is present; certification waits on integration compile and GPU profiling.
Hardware Impact: Exact microseconds saved are not measured. The only proven value is static removal of legacy blit/grab-style paths and bounded shader branches.

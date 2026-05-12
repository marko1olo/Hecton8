# FILL-RATE_DICTATOR Log

## 2026-05-11 - Fill-Rate Enforcement Slice

Status: PENDING VERIFICATION

What was wrong:
- First-party HUD projection, leak plume, and abyssal smoke used alpha-blend/Transparent rendering paths that keep shading stacked pixels on MX350.
- The half-resolution particle composite used a depth-derivative fade, not a true bilateral resolve, so half-res VFX could bleed over opaque edges.
- HUD projection had no stencil Equal gate, so the helmet/visor frame could not prevent hidden HUD fragments from shading.
- Shader variant stripping did not explicitly remove POINT/SPOT light variants for the MX350 target.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_VisorStencilMask.shader`.
- Added `Assets/_Project/Art/Shaders/Hecton_InternalBlackError.shader`.
- Changed `Hecton_HUD_DiegeticProjectionUnlit.shader` from alpha blend Transparent+80 to AlphaTest+80, `Blend Off`, dithered `clip`, `AlphaToMask On`, and `Stencil Comp Equal Ref 1`.
- Replaced the half-res particle depth-edge fake with 2x2 depth-weighted bilateral upsample in `Hecton_HalfResParticleComposite.shader`.
- Added `_HectonHalfResParticlesBilateralDepthScale` to `HectonHalfResParticlesFeature.cs` with cached property upload.
- Converted `Hecton_LeakPlume.shader` and `AbyssalBlackSmoke.shader` to dithered cutout with `ZWrite On` and `Blend Off`.
- Extended `HectonShaderVariantStripper.cs` to strip POINT/SPOT lighting keywords by default for MX350-targeted builds.

Cinematic Cheats used:
- Stencil write instead of hidden HUD shading.
- Dithered cutout instead of transparent blending.
- Half-resolution VFX with bilateral upscale instead of full-resolution transparent storms.
- Probe/proxy lighting bias by stripping point/spot variants instead of preserving real-time local light paths.

Exact microseconds saved:
- HUD stencil/cutout: estimated 40-120 us GPU, PENDING VERIFICATION.
- Leak/smoke dither cutout: estimated 120-350 us GPU in stacked VFX zones, PENDING VERIFICATION.
- Half-res particle bilateral path versus native transparent VFX: estimated 300-900 us GPU, PENDING VERIFICATION.
- Shader variant stripping: runtime microseconds not claimed; build/warmup pressure reduction is PENDING VERIFICATION.

Verification:
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed. One pre-existing warning in `GlobalPhysicsStateManager`.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed. One third-party Crest warning.
- Unity shader import, Console, Frame Debugger, RenderDoc, and MX350 profiler capture were not available in this shell session.

REGRESSION MODEL:
- CPU: no gameplay Tick/Update added; editor stripper only runs during shader processing/build.
- GC: no hot-path managed allocation added; renderer feature uses cached property IDs and existing material lifetime.
- Memory: one extra black fallback shader and one visor mask shader; no runtime RT added. Half-res path reuses existing RT topology.
- Cadence: render feature still records through URP RenderGraph; no compatibility `Execute` path added.
- Correctness: HUD will disappear if the visor stencil mask material/pass is not assigned or if an overlay camera clears stencil before HUD rendering.

HOT PATH IMPACT:
- Shader fragment cost shifts from alpha blend overdraw to clip/discard and early depth/stencil rejection.
- Half-res composite adds four scene-depth samples in the composite pass but preserves half-res particle rendering.

FAILURE MODES:
- If `Hecton_VisorStencilMask` is not rendered before HUD, HUD stencil Equal test rejects all pixels.
- If the HUD overlay camera clears stencil, HUD rejects all pixels.
- If a platform lacks a usable stencil attachment, HUD stencil gating cannot work.
- Dithered VFX can look noisy without TAA/temporal resolve.
- `ZWrite On` cutout smoke can alter sorting in dense overlapping particle stacks; this is the required fill-rate tradeoff and needs visual review.

WHY KEPT/REJECTED:
- Kept dither/cutout because MX350 is fill-rate bound and the prompt bans alpha blending.
- Rejected Crest/water prepass edits because AGENTS forbids direct third-party wrapper/material override without ownership proof.

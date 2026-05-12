# FILL-RATE_DICTATOR Rationale

Status: PENDING VERIFICATION

## Decision 1 - HUD Hidden Pixel Shading
Problem: HUD projection was alpha-blended in Transparent+80, so the fragment shader shaded pixels that the helmet frame later hides.
Solution: Add a visor stencil writer and change the HUD projection shader to stencil Equal 1 with dithered clip coverage.
Rejected Alternatives: Fullscreen HUD alpha blend; separate mask texture; scene YAML/prefab mutation. Alpha blend preserves overdraw, mask textures spend bandwidth, raw prefab edits risk unrelated state.
Scalability potential: Low uses 4x4 Bayer cutout. Middle can swap to blue-noise threshold. High/Ultra can let TAA resolve finer stochastic coverage.
Hardware Impact: Estimated 40-120 us GPU saving on i3/MX350 when helmet frame covers HUD regions; measured proof absent.

## Decision 2 - Half-Resolution Particle Resolve
Problem: The existing half-res composite faded edges from depth derivatives instead of actually rejecting cross-edge half-res taps.
Solution: Replace edge-fake sampling with a four-tap bilateral upsample comparing full-res scene depth per tap against center depth.
Rejected Alternatives: Native-resolution smoke/silt, full 3x3/5x5 upsample, storing an additional half-res particle depth target in this slice. Native-res burns fill-rate; 3x3+ costs more ALU; extra depth RT increases memory and requires renderer-asset wiring.
Scalability potential: Low uses 2x2 bilateral. Middle/High can raise tap count or add half-res particle depth if profiler grants it.
Hardware Impact: Keeps half-res VFX while reducing bleeding; estimated 300-900 us saved versus native transparent VFX in dense zones, pending profiler.

## Decision 3 - Heavy VFX Alpha Blend Removal
Problem: Leak plume and abyssal smoke used Transparent queue with SrcAlpha blending and ZWrite Off.
Solution: Convert clean first-party VFX shaders to AlphaTest/TransparentCutout with dithered clip, ZWrite On, and AlphaToMask.
Rejected Alternatives: Soft particles via alpha blend; sorting-only fixes. Both keep the same fill-rate failure mode.
Scalability potential: Low relies on deterministic IGN/Bayer cutout. High/Ultra can use temporal blue-noise rotation and TAA for smoother coverage.
Hardware Impact: Estimated 120-350 us saved in stacked leak/smoke regions on MX350; measured proof absent.

## Decision 4 - Shader Variant Strip Bias
Problem: MX350 cannot afford point/spot lighting variant explosion when fauna lighting is supposed to be probe/proxy driven.
Solution: Extend editor shader stripper to remove POINT_LIGHTS/SPOT_LIGHTS style variants by default unless HECTON_MX350_SHADER_STRIP=0 is set.
Rejected Alternatives: Manual shader inspector cleanup; runtime keyword disabling. Manual cleanup is brittle and runtime disable does not reduce build variant count.
Scalability potential: Low strips aggressive dynamic-light variants. High/Ultra can opt out through build environment and use richer local lights.
Hardware Impact: Reduces shader warmup/build memory pressure; runtime microsecond saving depends on variant residency and is pending measurement.

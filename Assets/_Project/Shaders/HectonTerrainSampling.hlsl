#ifndef HECTON_TERRAIN_SAMPLING_HLSL
#define HECTON_TERRAIN_SAMPLING_HLSL

// _Control / sampler_Control are declared by TerrainLitInput.hlsl (line 52) which is
// included by every pass BEFORE this file — do NOT redeclare (redefinition error / magenta).
TEXTURE2D(_Control1);

// Our custom texture arrays (bound at runtime by HectonTerrainMaterialInjector.cs).
TEXTURE2D_ARRAY(_AlbedoArray);
SAMPLER(sampler_AlbedoArray);
TEXTURE2D_ARRAY(_NormalArray);
SAMPLER(sampler_NormalArray);
TEXTURE2D_ARRAY(_MaskArray);
SAMPLER(sampler_MaskArray);
// sampler_LinearRepeat is declared by core GlobalSamplers.hlsl — do NOT redeclare.

// NOTE: these three scalars are intentionally at global scope ($Globals), NOT inside a
// second CBUFFER_START(UnityPerMaterial). TerrainLitInput.hlsl already opens AND closes
// its own UnityPerMaterial block before this include; reopening a same-named cbuffer is
// rejected by DXC on Unity 6. Global scope compiles on every target. Tradeoff: this
// shader is not SRP-Batcher-compatible for these props (perf, not correctness) — acceptable
// for correct rendering; revisit if the batcher penalty shows in a profiler capture.
float _HectonUVScale;
float _HectonTriplanarBlend;
float _HectonMacroVariationStrength;
// R85 PBR real-gap fixes (global scope for the same DXC/cbuffer reason as above).
float _HectonMicroUVScale;    // fine-UV multiplier for ~1.5m tactile micro-grain (default 3.0 if unbound).
float _HectonSmoothnessScale; // global gain on per-layer smoothness (default 1.0 if unbound).
float _HeightBlendSoftness;   // height-based splat transition width (default 0.1 if unbound).
// R96: AUP anchor. Published by HectonShaderGlobalDataVaultBridge on every floating-origin shift
// (H8ShaderIDs.TotalUniverseOffset). positionWS + _TotalUniverseOffset is invariant across origin
// shifts, so every world-anchored pattern field below stays put instead of jumping with each
// rebase. Unbound default (0,0,0) reproduces the exact pre-R96 behavior.
float3 _TotalUniverseOffset;

struct TerrainSample
{
    half3 albedo;
    half3 normalTS;
    half  metallic;
    half  smoothness;
    float ao;
};

#ifndef SAMPLE_TEXTURE2D_ARRAY_GRAD
#define SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, coord2D, arrayIndex, dx, dy) tex.SampleGrad(smp, float3(coord2D, arrayIndex), dx, dy)
#endif

// --- STOCHASTIC ANTI-TILING (Burley/Heitz style) ---
// Hash returns a per-cell random 2D offset + rotation angle
float3 HectonCellHash3(float2 cell)
{
    float3 p = float3(cell.x, cell.y, cell.x + cell.y * 37.0);
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.xxy + p.yzz) * p.zyx);
}

// Stochastic texture sampling: breaks tiling completely via per-cell UV rotation + offset
// Works by blending 4 overlapping cells with smooth weights.
// stochasticFade: 0 = single cheap sample, 1 = full 4-corner anti-tiling.
// Explicit gradients (dx, dy) derived before branching eliminate GPU quad derivative divergence hazards.
float3 SampleStochastic_Albedo(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float stochasticFade)
{
    // Precalculate explicit gradients before dynamic branching to prevent quad derivative divergence
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    // Fast path: single sample at distance (saves 3 TMU reads)
    [branch] if (stochasticFade < 0.01)
    {
        return SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy).rgb;
    }

    // Scale to cell grid (each cell = 1 uv unit)
    float2 cell  = floor(uv);
    float2 fuv   = frac(uv);
    // Cubic smooth weight function (C2 continuity)
    float2 w = fuv * fuv * (3.0 - 2.0 * fuv);

    // 4 corners
    float3 h00 = HectonCellHash3(cell + float2(0, 0));
    float3 h10 = HectonCellHash3(cell + float2(1, 0));
    float3 h01 = HectonCellHash3(cell + float2(0, 1));
    float3 h11 = HectonCellHash3(cell + float2(1, 1));

    // Per-corner UV: rotate + offset by hash
    float2 uv00 = uv + h00.xy * 0.3 + float2(h00.z * 0.7, 0.0);
    float2 uv10 = uv + h10.xy * 0.3 + float2(h10.z * 0.7, 0.0);
    float2 uv01 = uv + h01.xy * 0.3 + float2(h01.z * 0.7, 0.0);
    float2 uv11 = uv + h11.xy * 0.3 + float2(h11.z * 0.7, 0.0);

    float3 s00 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv00, layerIdx, dx, dy).rgb;
    float3 s10 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv10, layerIdx, dx, dy).rgb;
    float3 s01 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv01, layerIdx, dx, dy).rgb;
    float3 s11 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv11, layerIdx, dx, dy).rgb;

    // Bilinear blend in perceptual space (sqrt -> blend -> sq to reduce darkening)
    s00 = sqrt(max(s00, 0.0001));
    s10 = sqrt(max(s10, 0.0001));
    s01 = sqrt(max(s01, 0.0001));
    s11 = sqrt(max(s11, 0.0001));
    float3 stochResult = lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);
    stochResult = stochResult * stochResult;

    [branch] if (stochasticFade < 0.99)
    {
        float3 simpleResult = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy).rgb;
        return lerp(simpleResult, stochResult, stochasticFade);
    }

    return stochResult;
}

float3 SampleStochastic_Normal(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float stochasticFade, float gradScale)
{
    // gradScale > 1 inflates the SampleGrad derivatives -> forces a coarser (higher) mip level.
    // Used as a distance-driven positive mip-bias for normals so mid/far-field micro-normal
    // frequency stays below the pixel Nyquist limit (kills the checkerboard moire at the source).
    float2 dx = ddx(uv) * gradScale;
    float2 dy = ddy(uv) * gradScale;

    [branch] if (stochasticFade < 0.01)
    {
        return SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy).rgb;
    }

    float2 cell  = floor(uv);
    float2 fuv   = frac(uv);
    float2 w = fuv * fuv * (3.0 - 2.0 * fuv);

    float3 h00 = HectonCellHash3(cell + float2(0, 0));
    float3 h10 = HectonCellHash3(cell + float2(1, 0));
    float3 h01 = HectonCellHash3(cell + float2(0, 1));
    float3 h11 = HectonCellHash3(cell + float2(1, 1));

    float2 uv00 = uv + h00.xy * 0.3 + float2(h00.z * 0.7, 0.0);
    float2 uv10 = uv + h10.xy * 0.3 + float2(h10.z * 0.7, 0.0);
    float2 uv01 = uv + h01.xy * 0.3 + float2(h01.z * 0.7, 0.0);
    float2 uv11 = uv + h11.xy * 0.3 + float2(h11.z * 0.7, 0.0);

    float3 s00 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv00, layerIdx, dx, dy).rgb;
    float3 s10 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv10, layerIdx, dx, dy).rgb;
    float3 s01 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv01, layerIdx, dx, dy).rgb;
    float3 s11 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv11, layerIdx, dx, dy).rgb;

    float3 stochResult = lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);

    [branch] if (stochasticFade < 0.99)
    {
        float3 simpleResult = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy).rgb;
        return lerp(simpleResult, stochResult, stochasticFade);
    }

    return stochResult;
}

float4 SampleStochastic_Mask(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float stochasticFade)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    [branch] if (stochasticFade < 0.01)
    {
        return SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy);
    }

    float2 cell  = floor(uv);
    float2 fuv   = frac(uv);
    float2 w = fuv * fuv * (3.0 - 2.0 * fuv);

    float3 h00 = HectonCellHash3(cell + float2(0, 0));
    float3 h10 = HectonCellHash3(cell + float2(1, 0));
    float3 h01 = HectonCellHash3(cell + float2(0, 1));
    float3 h11 = HectonCellHash3(cell + float2(1, 1));

    float2 uv00 = uv + h00.xy * 0.3 + float2(h00.z * 0.7, 0.0);
    float2 uv10 = uv + h10.xy * 0.3 + float2(h10.z * 0.7, 0.0);
    float2 uv01 = uv + h01.xy * 0.3 + float2(h01.z * 0.7, 0.0);
    float2 uv11 = uv + h11.xy * 0.3 + float2(h11.z * 0.7, 0.0);

    float4 s00 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv00, layerIdx, dx, dy);
    float4 s10 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv10, layerIdx, dx, dy);
    float4 s01 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv01, layerIdx, dx, dy);
    float4 s11 = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv11, layerIdx, dx, dy);

    float4 stochResult = lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);

    [branch] if (stochasticFade < 0.99)
    {
        float4 simpleResult = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, smp, uv, layerIdx, dx, dy);
        return lerp(simpleResult, stochResult, stochasticFade);
    }

    return stochResult;
}

// --- LARGE-SCALE COLOR VARIATION: breaks uniform material look across distance ---
float HectonNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float3 p3 = frac(float3(i.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    float h00 = frac((p3.x + p3.y) * p3.z);
    p3 = frac(float3((i + float2(1, 0)).xyx) * .1031); p3 += dot(p3, p3.yzx + 33.33);
    float h10 = frac((p3.x + p3.y) * p3.z);
    p3 = frac(float3((i + float2(0, 1)).xyx) * .1031); p3 += dot(p3, p3.yzx + 33.33);
    float h01 = frac((p3.x + p3.y) * p3.z);
    p3 = frac(float3((i + float2(1, 1)).xyx) * .1031); p3 += dot(p3, p3.yzx + 33.33);
    float h11 = frac((p3.x + p3.y) * p3.z);
    return lerp(lerp(h00, h10, u.x), lerp(h01, h11, u.x), u.y);
}

float HectonMacroNoise(float2 worldXZ)
{
    float n0 = HectonNoise2D(worldXZ * 0.00155);
    float n1 = HectonNoise2D(worldXZ * 0.00047 + float2(19.73, -6.11));
    return smoothstep(0.24, 0.82, n0 * 0.68 + n1 * 0.32);
}

// --- R86 PHASE 11 GOAL 1: PROCEDURAL UV DOMAIN WARP (zero extra texture fetches) ---
// Returns a smooth, continuous low-frequency offset (in texture-repeat units) driven by
// world-space position. Added to every sample UV so the rigid square repeat grid bends
// organically across ~0.8-3km spatial waves. Two detuned, mutually non-commensurate octaves
// are summed so the warp field itself does not tile. All frequencies (<=0.0081 /m) sit far
// below the pixel Nyquist limit even when a 4km tile fills the frame (~4 m/pixel), so the
// warp can never alias — it only displaces sampling coordinates. C-infinity (pure sin/cos),
// so SampleGrad derivatives stay well-defined and quad-safe.
float2 HectonUVDomainWarp(float2 worldXZ)
{
    // Octave A: period ~776m / ~795m (x,y detuned so the lobes never phase-lock into a grid).
    float2 wA = float2(sin(worldXZ.y * 0.0081 + 1.7), cos(worldXZ.x * 0.0079 - 0.9));
    // Octave B: period ~3.1km / ~3.2km — large-scale drift that shifts whole ridgelines.
    float2 wB = float2(sin(worldXZ.x * 0.00203 - 2.3), cos(worldXZ.y * 0.00197 + 0.6));
    return wA * 0.28 + wB * 0.52; // texture-repeat units
}

float2 HectonRotatedScaledUV(float2 uv, float layerIdx)
{
    const float c = 0.5;
    const float s = 0.86602540378;
    float2 r = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
    return r * 0.6 + float2(11.37, -7.91) + layerIdx * float2(0.173, 0.119);
}

float3 HectonDecodeArrayNormal(float3 packedNormal)
{
    float3 n;
    n.xy = packedNormal.rg * 2.0 - 1.0;
    n.z = sqrt(max(1e-6, 1.0 - dot(n.xy, n.xy)));
    return normalize(n);
}

// Per-layer tangent-space normal strength. Hardrock/stone layers get amplified tactile
// depth; soft sediment (sand/silt/clay) stays near-neutral so it does not read as gritty.
// Scale is applied to the tangent XY (the physically correct way to strengthen a normal map:
// steepen slope, re-derive Z), NOT a post-hoc lerp that would denormalize the vector.
float HectonLayerNormalScale(int layer)
{
    if (layer == 3) return 1.75; // Hard basalt / serpentinite — deep chiselled rock.
    if (layer == 5) return 1.60; // Manganese nodule plain — pebbled relief.
    if (layer == 6) return 1.55; // Reef rubble — broken carbonate.
    if (layer == 1) return 1.40; // Limestone shelf — moderate karst.
    return 1.0;                  // Sand / silt / clay / brine — soft, near-flat.
}

// R85: Per-layer PBR regime. The 0.18 global smoothness cap (pre-R85) forced every
// substrate — sand and wet basalt alike — to the same near-matte response, which was the
// primary reason the surface read as uniform "clay". These set the physical base per layer;
// the _MaskArray.a channel then modulates micro-variation AROUND this base, and
// _HectonSmoothnessScale is the artist gain. Wet-submarine target: hard rock glossy
// (~0.7-0.8), loose sediment diffuse (~0.12-0.18).
float HectonLayerSmoothnessBase(int layer)
{
    if (layer == 3) return 0.60; // Hard basalt / serpentinite — wet DAMP rock. R88: was 0.75, which
                                 // blew to lacquered-plastic specular on steep cliff walls under lifted
                                 // exposure. 0.60 keeps it the glossiest substrate (still clearly wet)
                                 // without the mirror-highlight blowout. Remains > all sediment layers.
    if (layer == 5) return 0.58; // Manganese nodule plain — semi-metallic sheen.
    if (layer == 4) return 0.40; // Brine salt crust — crystalline glint.
    if (layer == 1) return 0.34; // Limestone shelf — damp carbonate.
    if (layer == 6) return 0.32; // Reef rubble — broken carbonate.
    if (layer == 7) return 0.28; // Seep oxide crust — mineral film.
    if (layer == 2) return 0.14; // Clay / silt — matte turbidity.
    return 0.15;                 // ShellSand — diffuse.
}

// Substrate hardness / metallic. Only mineralised layers carry appreciable metallic
// response (manganese nodules, oxide seep films); sediment stays dielectric.
float HectonLayerMetallicBase(int layer)
{
    if (layer == 5) return 0.55; // Manganese nodule plain — genuinely metallic.
    if (layer == 7) return 0.22; // Seep oxide crust — semi-metallic mineral film.
    if (layer == 3) return 0.10; // Basalt — trace ferrous sheen.
    return 0.0;                  // Sediments / carbonate — dielectric.
}

float3 HectonDecodeArrayNormalScaled(float3 packedNormal, float scale)
{
    float2 xy = (packedNormal.rg * 2.0 - 1.0) * scale;
    float z = sqrt(max(1e-6, 1.0 - saturate(dot(xy, xy))));
    return normalize(float3(xy, z));
}

float3 HectonWhiteoutBlendTS(float3 baseNormalTS, float3 detailNormalTS)
{
    return normalize(float3(baseNormalTS.xy + detailNormalTS.xy, baseNormalTS.z * detailNormalTS.z));
}

float3 HectonSampleMacroAlbedo(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float macroMask, float stochasticFade)
{
    float3 baseSample = SampleStochastic_Albedo(tex, smp, uv, layerIdx, stochasticFade);
    // Skip rotated anti-tile sample at distance — macro variation noise handles far-field variety
    [branch] if (stochasticFade > 0.01 && macroMask > 0.01)
    {
        float3 rotatedSample = SampleStochastic_Albedo(tex, smp, HectonRotatedScaledUV(uv, layerIdx), layerIdx, stochasticFade);
        return lerp(baseSample, rotatedSample, macroMask);
    }
    return baseSample;
}

float3 HectonSampleMacroNormalTS(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float macroMask, float stochasticFade)
{
    float nScale = HectonLayerNormalScale((int)layerIdx);
    // Distance-driven positive mip-bias for normals: 1.0 (no bias) in the near field where
    // stochasticFade==1, ramping to ~2.83 (+1.5 mip) as fade->0 in the far field. This pushes
    // the sampled normal-map frequency below screen Nyquist so it cannot alias into moire.
    float normalGradScale = lerp(2.83, 1.0, saturate(stochasticFade));
    float3 baseNormal = HectonDecodeArrayNormalScaled(SampleStochastic_Normal(tex, smp, uv, layerIdx, stochasticFade, normalGradScale), nScale);
    // R84 moire fix: the rotated-UV anti-tile grid is a second incoherent sampling frequency that,
    // under minification, beats against the pixel grid into a checkerboard across the 10..120m band.
    // Gate it OFF once stochasticFade drops below 0.5 (>~90m): the far field keeps a single clean
    // normal sample. macroMask still gates it in the near field where it is coherent and useful.
    [branch] if (stochasticFade > 0.5 && macroMask > 0.01)
    {
        float3 rotatedNormal = HectonDecodeArrayNormalScaled(SampleStochastic_Normal(tex, smp, HectonRotatedScaledUV(uv, layerIdx), layerIdx, stochasticFade, normalGradScale), nScale);
        return normalize(lerp(baseNormal, rotatedNormal, macroMask));
    }
    return baseNormal;
}

float2 HectonSandRippleGradient(float2 planeMeters, float layerIdx)
{
    const float waveNumber = 20.94395102; // 2*pi / 0.30m.
    float2 dirA = normalize(float2(0.9238795, 0.3826834));
    float2 dirB = normalize(float2(-0.2588190, 0.9659258));
    float phaseA = dot(planeMeters, dirA) * waveNumber + layerIdx * 1.731;
    float phaseB = dot(planeMeters, dirB) * (waveNumber * 0.57) + layerIdx * 2.193 + sin(phaseA * 0.23) * 0.35;
    float2 gradA = cos(phaseA) * dirA * (0.010 * waveNumber);
    float2 gradB = cos(phaseB) * dirB * (0.006 * waveNumber * 0.57);
    return gradA + gradB;
}

float3 HectonSandRippleNormalTS(float2 planeMeters, float layerIdx)
{
    float2 gradient = HectonSandRippleGradient(planeMeters, layerIdx);
    return normalize(float3(-gradient.x, -gradient.y, 1.0));
}

float3 HectonApplySandRippleTS(float3 baseNormalTS, float2 planeMeters, float layerIdx, float strength)
{
    float3 ripple = HectonSandRippleNormalTS(planeMeters, layerIdx);
    ripple = normalize(float3(ripple.xy * saturate(strength), ripple.z));
    return HectonWhiteoutBlendTS(baseNormalTS, ripple);
}

float3 HectonMaterialPalette(int layer)
{
    if (layer == 0) return float3(0.145, 0.155, 0.150); // ShellSand: pale shell hash under deep blue lighting.
    if (layer == 1) return float3(0.120, 0.135, 0.130); // Limestone shelf carbonate.
    if (layer == 2) return float3(0.070, 0.085, 0.105); // Clay/silt turbidity basin.
    if (layer == 3) return float3(0.045, 0.050, 0.060); // Hard basalt/serpentinite.
    if (layer == 4) return float3(0.165, 0.150, 0.128); // Brine salt crust.
    if (layer == 5) return float3(0.035, 0.035, 0.040); // Manganese nodule plain.
    if (layer == 6) return float3(0.115, 0.125, 0.118); // Reef rubble.
    return float3(0.060, 0.050, 0.040);                 // Seep oxide crust.
}

float HectonMaterialBaseLum(int layer)
{
    if (layer == 0) return 0.155;
    if (layer == 1) return 0.135;
    if (layer == 2) return 0.092;
    if (layer == 3) return 0.060;
    if (layer == 4) return 0.170;
    if (layer == 5) return 0.052;
    if (layer == 6) return 0.125;
    return 0.074;
}

float3 HectonApplyLuminanceOverride(float3 sampledAlbedo, int layer, float procLum, float macroVar)
{
    float3 palette = HectonMaterialPalette(layer);
    float srcLum = dot(sampledAlbedo, float3(0.2126, 0.7152, 0.0722));
    float sampledValid = step(0.018, srcLum) * step(srcLum, 0.92);
    float3 sampledChroma = sampledAlbedo / max(srcLum, 0.002);
    float paletteLum = dot(palette, float3(0.2126, 0.7152, 0.0722));
    float3 paletteChroma = palette / max(paletteLum, 0.002);
    // R85: was 0.45 — texture chroma was 55% discarded, flattening real geology into a grey
    // procedural mush. Keep the palette only as a tint floor; let real texture chroma dominate
    // (0.85) when the sample is valid so albedo pattern/veining survives.
    float3 chroma = lerp(paletteChroma, sampledChroma, sampledValid * 0.85);
    float macroStrength = max(_HectonMacroVariationStrength, 0.0);
    // R85: brightness is no longer a pure procedural override. Blend the palette base luminance
    // WITH the sampled texture luminance (0.55 weight) so bright/dark detail in _AlbedoArray is
    // preserved instead of being crushed to a fixed per-layer value. Macro/meso/micro noise and
    // macroVar still add low-frequency variety on top so stub textures never render dead-flat.
    float baseLum   = HectonMaterialBaseLum(layer);
    float sampledLumClamped = clamp(srcLum, baseLum * 0.6, baseLum * 2.2);
    float targetLum = lerp(baseLum, sampledLumClamped, sampledValid * 0.55)
                    * lerp(0.78, 1.30, procLum)
                    * lerp(0.90, 1.14, macroVar * macroStrength);
    return saturate(chroma * targetLum);
}



// 8 layers from _Control and _Control1
TerrainSample SampleHectonTerrain(float2 controlUV, float2 detailUV, float3 worldPos, float3 worldNormal, float3 viewDirTS)
{
    float4 ctrl  = SAMPLE_TEXTURE2D(_Control,  sampler_Control, controlUV);
    float4 ctrl1 = SAMPLE_TEXTURE2D(_Control1, sampler_Control, controlUV);

    float weights[8];
    weights[0] = ctrl.r;
    weights[1] = ctrl.g;
    weights[2] = ctrl.b;
    weights[3] = ctrl.a;
    weights[4] = ctrl1.r;
    weights[5] = ctrl1.g;
    weights[6] = ctrl1.b;
    weights[7] = ctrl1.a;

    float uvScale = _HectonUVScale;
    if (uvScale < 0.0001) uvScale = 200.0;

    // R85 tactile micro-grain: the fine tier carries the near-field detail, so multiply it by
    // _HectonMicroUVScale to push it toward ~1.5m period (default 3.0 if unbound). The coarse
    // tier stays at the ~20m macro scale. fineFade already cross-blends the two by camera
    // distance, so no extra texture sample is added — this only shifts the fine tier's frequency.
    float microScale = _HectonMicroUVScale;
    if (microScale < 0.0001) microScale = 3.0;
    float fineScale = uvScale * microScale;

    // R86 PHASE 11 GOAL 2 (superseded by R94): dual-scale anti-tiling.
    // scaleA is the steady coarse macro tier; the fine tier B carries tactile near-field grain.
    // R94 removed the distance-morphing non-commensurate partner tier (its radial UV-scale lerp
    // was the vinyl-swirl root cause), so both tiers are now FIXED scales and distance only
    // weights the blend of sampled RESULTS (dualMix below). R95 cleanup: the orphaned
    // coarseScaleB constant from the R86 design is deleted.
    float coarseScaleA = uvScale * 0.18;         // ~1 repeat / 14m macro tier

    // R96: AUP-stable anchor for every world-anchored pattern field (warp, macro noise, lateral
    // UVs, palette jitters, luminance noise, sand ripple). Camera-distance math below still uses
    // render-space worldPos — only pattern anchoring switches to the shift-invariant domain.
    float3 aupPos = worldPos + _TotalUniverseOffset;

    // GOAL 1 warp field (texture-repeat units) — shared world-anchored offset bending both grids.
    // R88: scaled by farFieldWeight so the warp is zero in the near field (<60m, where 4-corner
    // stochastic already breaks tiling) and full in the far field (>120m, where it erases the
    // large-scale wallpaper repeat). This is the primary purge of the near-field vinyl swirl.
    // NOTE: farFieldWeight is defined just above but the warp is consumed below after the two
    // fade fields (fineFade etc.) are computed; the multiply is applied at the uvWarp use sites.
    float2 uvWarp = HectonUVDomainWarp(aupPos.xz);

    // Distance-based blend between fine and coarse (fade fine tiling at distance).
    // Squared distance avoids a per-pixel sqrt (length()); the fade START/END radii are
    // preserved exactly (endpoints squared), so material appearance at <10m and >120m is
    // unchanged. Only the interpolation curve BETWEEN the endpoints changes shape slightly
    // (now non-linear in metric distance) — acceptable for an LOD blend, and monotonic.
    float3 deltaWS = worldPos - _WorldSpaceCameraPos;
    float distSq = dot(deltaWS, deltaWS);
    // Fine tiling fades out 10..60m  -> 100..3600 in squared space.
    float fineFade = 1.0 - saturate((distSq - 100.0) * (1.0 / 3500.0));
    // R84 moire ROOT CAUSE: the 0.30m analytic sand-ripple normal (HectonSandRippleGradient) is a
    // procedural high-frequency wave, NOT a texture — mip-bias/stochastic gating cannot touch it.
    // At distance its 0.30m period projects below one screen pixel and aliases into a checkerboard.
    // Fade the ripple amplitude out over 4..25m (distSq 16..625) — a TIGHT window so the wave is
    // fully gone before it minifies into the mid-field checkerboard band (a 10..60m fade left a
    // residual band in the transition zone where the ripple was present but already sub-pixel).
    float rippleFade = 1.0 - saturate((distSq - 16.0) * (1.0 / 609.0));
    // Stochastic anti-tiling distance gate:
    // <60m (distSq<3600): full 4-corner stochastic (stochasticFade=1)
    // 60..120m (3600..14400): smooth transition zone
    // >120m (distSq>14400): single cheap sample (stochasticFade=0) — saves 80-90% TMU
    float stochasticFade = 1.0 - saturate((distSq - 3600.0) * (1.0 / 10800.0));
    float macroAntiTileMask = HectonMacroNoise(aupPos.xz);

    // R88 VINYL-SWIRL PURGE (near field). Root cause proven by the _DEBUG_NORMALS pass: the
    // concentric basalt swirl is ABSENT from the tangent-normal channel, so it is not a sampling-
    // geometry Jacobian singularity — it lives in albedo + specular. Two near-field contributors,
    // both faded out where the 4-corner stochastic already kills tiling on its own:
    //   (1) the domain warp field (added below to every tier), and
    //   (2) the 30deg-rotated low-frequency macro-albedo overlay (HectonSampleMacroAlbedo).
    // stochasticFade is 1 at <60m (near) and 0 at >120m (far), so (1 - stochasticFade) is 0 near
    // and 1 far. Warp/overlay therefore contribute ONLY in the far field where they break the
    // large-scale repeat, and vanish in the near field so basalt renders crisp and swirl-free.
    // C-continuous (stochasticFade is a smooth function of squared distance) -> SampleGrad-safe.
    float farFieldWeight = 1.0 - stochasticFade;
    // Rotated albedo overlay near-field suppression mask (normals keep their own gate untouched;
    // that channel showed no swirl, so its rotated overlay is left intact for near-field variety).
    float macroAlbedoOverlayMask = macroAntiTileMask * farFieldWeight;

    // R86 PHASE 11 GOAL 2: build the two non-commensurate, domain-warped UV tiers.
    // scaleB morphs from the tactile fine grain (near) to the non-commensurate coarse partner
    // (far); dualMix weights the A/B blend so the near field keeps tactile detail (B-heavy) while
    // the far field balances the two incommensurate grids (macro-noise modulated) — that balance
    // is what erases the wallpaper repeat once the single-grid far path used to take over.
    // R94 TRUE-SCALE-BLEND FIX (vinyl swirl root cause). The R86 line was:
    //   float scaleB = lerp(coarseScaleB, fineScale, fineFade);
    // which INTERPOLATED THE UV SCALE ITSELF by camera distance (fineFade is a distSq function),
    // morphing the tier frequency ~11.5x (51.9 -> 600) radially outward from the camera. Scaling UVs
    // by radial distance mathematically produces concentric iso-distance contours — the "vinyl record"
    // swirl, visible on basalt because its specular amplifies the bands. FIX: both tiers are now FIXED
    // scales (A = coarse ~14m, B = fine tactile). Distance no longer touches any SCALE; it only weights
    // the BLEND OF THE SAMPLED RESULTS via dualMix below (lerp results, never lerp coordinates). No
    // radial UV distortion -> no rings. Tradeoff: the fine tier B stays present at low weight in the far
    // field (dualMix->~0.35); its albedo is single-sampled + the normal path is separately mip-biased,
    // so this is far less objectionable than the swirl. Verify in PASS 2/4 far field.
    float scaleB  = fineScale;
    float dualMix = lerp(0.35 + macroAntiTileMask * 0.30, 0.85, fineFade);
    // R88: apply the near-field warp suppression now that farFieldWeight exists. uvWarp becomes
    // 0 in the near field (crisp basalt, no swirl) and full-strength far (anti-wallpaper).
    uvWarp *= farFieldWeight;
    float2 uvXZ_A = detailUV * coarseScaleA + uvWarp;
    float2 uvXZ_B = detailUV * scaleB       + uvWarp;
    float2 uvXY_A = (aupPos.xy * 0.001) * coarseScaleA + uvWarp;
    float2 uvXY_B = (aupPos.xy * 0.001) * scaleB       + uvWarp;
    float2 uvZY_A = (aupPos.zy * 0.001) * coarseScaleA + uvWarp;
    float2 uvZY_B = (aupPos.zy * 0.001) * scaleB       + uvWarp;

    // Normal for biplanar. _HectonTriplanarBlend sharpens the two surviving axes before
    // the weakest projection is dropped, so cliffs keep sidewall texture without full triplanar cost.
    float3 absNormal = pow(max(abs(worldNormal), 1e-4), max(1.0, _HectonTriplanarBlend));
    float3 biW = absNormal;
    if (biW.x <= biW.y && biW.x <= biW.z) biW.x = 0;
    else if (biW.y <= biW.x && biW.y <= biW.z) biW.y = 0;
    else biW.z = 0;
    biW /= (biW.x + biW.y + biW.z + 1e-6);

    // Height blending
    float blend[8];
    float maxBlend = 0.0;
    [unroll]
    for (int h = 0; h < 8; h++)
    {
        if (weights[h] > 0.005)
        {
            float height = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_B, (float)h, stochasticFade).b;
            blend[h] = weights[h] + height;
            maxBlend = max(maxBlend, blend[h]);
        }
        else
        {
            blend[h] = 0.0;
        }
    }

    float heightTransition = _HeightBlendSoftness;
    if (heightTransition < 0.02) heightTransition = 0.1; // default/soft if unbound
    float totalW = 1e-4;
    [unroll]
    for (int w = 0; w < 8; w++)
    {
        if (weights[w] > 0.005)
        {
            // Normalize the height delta by heightTransition BEFORE the power so the winning
            // layer resolves to ~1.0. Without this, pow(0.05, 8) ~ 4e-11 collapsed every weight
            // below the downstream 0.005 accumulation gate -> zero albedo -> black terrain.
            float b = saturate((blend[w] - maxBlend + heightTransition) / heightTransition);
            b = pow(b, 8.0);
            weights[w] = b;
            totalW += weights[w];
        }
        else
        {
            weights[w] = 0.0;
        }
    }

    float invW = 1.0 / totalW;
    [unroll]
    for (int i = 0; i < 8; i++) weights[i] *= invW;

    // Large-scale color variation noise (breaks uniform look at macro distance)
    // Period ~500m in worldspace → visible at 200-2000m camera distance
    float macroColorNoise = HectonNoise2D(aupPos.xz * 0.002) * 0.5 + 0.5;
    float macroColorNoise2 = HectonNoise2D(aupPos.xz * 0.0007 + float2(17.3, 5.1)) * 0.5 + 0.5;
    // Combined: large blob variation
    float macroVar = macroColorNoise * 0.6 + macroColorNoise2 * 0.4;

    // R86 PHASE 11 GOAL 3: MACRO COLOR & SMOOTHNESS PALETTE JITTER.
    // Three decorrelated low-frequency world-space fields (periods ~110m, ~270m, ~450m) give
    // each stretch of a 4km range its own tone, warmth and wetness, so adjacent ~20m patches of
    // sand or rock are never identical even where the same texture layer wins. These are pure
    // procedural noise (no texture fetch) and are applied as multiplicative, zero-mean-ish
    // modulations AFTER per-layer PBR so energy conservation and the layer weight sum are intact.
    float jTone = HectonNoise2D(aupPos.xz * 0.00370 + float2(41.7,  9.3));  // ~270m tonal blobs
    float jWarm = HectonNoise2D(aupPos.xz * 0.00222 + float2(-13.1, 27.9)); // ~450m warm/cool drift
    float jWet  = HectonNoise2D(aupPos.xz * 0.00909 + float2(7.7,  -19.4));  // ~110m wetness patches
    // Brightness: +/-10% around 1.0. Warmth: tilt R up / B down (or vice-versa) by up to +/-5%.
    float macroTone  = lerp(0.90, 1.10, jTone);
    float3 macroTint = float3(lerp(0.95, 1.05, jWarm), 1.0, lerp(1.05, 0.95, jWarm));
    // Wetness: darken + boost smoothness on "wet" patches (submarine surfaces glisten unevenly).
    float macroWetSmooth = lerp(-0.08, 0.14, jWet); // added to smoothness, zero-crossing ~ jWet=0.36
    float macroWetAlbedo = lerp(1.04, 0.90, jWet);  // wet patches read slightly darker

    // R85 global smoothness gain (default 1.0 if the property is unbound).
    float smoothGain = _HectonSmoothnessScale;
    if (smoothGain < 0.0001) smoothGain = 1.0;

    float3 albedo    = (float3)0;
    float3 normalTS  = (float3)0;
    float  smoothness = 0;
    float  metallic   = 0;
    float  ao         = 0;

    [unroll]
    for (int k = 0; k < 8; k++)
    {
        [branch]
        if (weights[k] > 0.005)
        {
            float3 a = 0;
            float3 n = 0;
            float4 m = 0;

            float3 a_y = 0; float3 n_y = 0; float4 m_y = 0;
            float3 a_x = 0; float3 n_x = 0; float4 m_x = 0;
            float3 a_z = 0; float3 n_z = 0; float4 m_z = 0;

            // Sample fine + coarse and blend by distance.
            // Biplanar early-exit: skip a projection whose blend weight is negligible. On flat
            // seabed (N_y->1) biW.x/biW.z fall below 1e-3, so both lateral projections are skipped
            // and only the top (Y) projection samples — halving TMU lookups there. The gate is
            // C-continuous because biW is already a smooth normalized weight (no hard N_y threshold).
            [branch] if (biW.y > 0.001)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_B, (float)k, macroAlbedoOverlayMask, stochasticFade);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_A, (float)k, macroAlbedoOverlayMask, stochasticFade);
                a_y = lerp(ac, af, dualMix);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXZ_B, (float)k, macroAntiTileMask, stochasticFade);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXZ_A, (float)k, macroAntiTileMask, stochasticFade);
                n_y = HectonApplySandRippleTS(lerp(nc, nf, dualMix), aupPos.xz, (float)k, saturate(weights[0] * 1.35) * rippleFade);
                m_y = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_B, (float)k, stochasticFade);
                a += a_y * biW.y;
                n += n_y * biW.y;
                m += m_y * biW.y;
            }
            [branch] if (biW.x > 0.001)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvZY_B, (float)k, macroAlbedoOverlayMask, stochasticFade);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvZY_A, (float)k, macroAlbedoOverlayMask, stochasticFade);
                a_x = lerp(ac, af, dualMix);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvZY_B, (float)k, macroAntiTileMask, stochasticFade);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvZY_A, (float)k, macroAntiTileMask, stochasticFade);
                n_x = lerp(nc, nf, dualMix); // R88: NO sand ripple on lateral (cliff) projection —
                // a 0.30m horizontal beach-ripple wave applied to a vertical rock wall via worldPos.zy
                // smears into the "melted plastic" vertical streak. Ripple is a flat-seabed feature and
                // stays on the Y (top) projection only.
                m_x = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvZY_B, (float)k, stochasticFade);
                a += a_x * biW.x;
                n += float3(n_x.z, n_x.y, n_x.x) * biW.x;
                m += m_x * biW.x;
            }
            [branch] if (biW.z > 0.001)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXY_B, (float)k, macroAlbedoOverlayMask, stochasticFade);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXY_A, (float)k, macroAlbedoOverlayMask, stochasticFade);
                a_z = lerp(ac, af, dualMix);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXY_B, (float)k, macroAntiTileMask, stochasticFade);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXY_A, (float)k, macroAntiTileMask, stochasticFade);
                n_z = lerp(nc, nf, dualMix); // R88: NO sand ripple on lateral (cliff) projection (see n_x note).
                m_z = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXY_B, (float)k, stochasticFade);
                a += a_z * biW.z;
                n += float3(n_z.x, n_z.z, n_z.y) * biW.z;
                m += m_z * biW.z;
            }

            float3 nd = normalize(n);

            // True geological luminance override. The texture array supplies chroma when valid;
            // procedural macro/meso/micro noise owns brightness so stub textures cannot flatten the seafloor.
            float lumMacro = HectonNoise2D(aupPos.xz * 0.002  + float2(3.7, 8.1));
            float lumMeso  = HectonNoise2D(aupPos.xz * 0.022  + float2(11.3, 2.9));
            float lumMicro = HectonNoise2D(aupPos.xz * 0.31   + float2(5.7, 17.1));
            float procLum  = lumMacro * 0.50 + lumMeso * 0.33 + lumMicro * 0.17;

            float3 finalColor = HectonApplyLuminanceOverride(a, k, procLum, macroVar);
            // R86 PHASE 11 GOAL 3: apply macro tone/warmth/wetness jitter so neighbouring
            // patches of the same layer diverge in tone across a 4km range. saturate() keeps
            // energy bounded; the modulations are ~zero-mean so mean albedo across the tile holds.
            finalColor = saturate(finalColor * (macroTone * macroWetAlbedo) * macroTint);

            // R85 per-layer PBR response. The old path was `smoothness += m.a * 0.18` for ALL
            // layers, which capped wet basalt and dry sand to the same near-matte value — the
            // main cause of the uniform "clay" look. Now each layer has a physical smoothness
            // regime (HectonLayerSmoothnessBase); the mask.a channel modulates micro-variation
            // AROUND that base (±), and _HectonSmoothnessScale is the global artist gain.
            float smoothBase = HectonLayerSmoothnessBase(k);
            float smoothMod  = (m.a - 0.5) * 0.5;               // texture detail, zero-mean
            // R94 SPECULAR BREAK-UP (plastic cliff fix). The additive smoothMod (±0.25) was too weak
            // to break the uniform sheet gloss on wet basalt — under lifted sun the whole cliff face
            // blew to lacquered plastic. Now the mask.a roughness channel MULTIPLICATIVELY gates
            // smoothness (map m.a from [0,1] -> [0.45,1.0] and multiply), so the specular highlight
            // clings only to the sharp, high-mask rock detail (edges, crystal facets, fracture lines)
            // and is knocked down in the flat/dull areas between them. Result reads as damp faceted
            // stone, not a plastic sheet. Applied stronger on the hard mineral layers that actually
            // go glossy (basalt/nodule/oxide/limestone); soft sediment keeps its tuned matte response.
            float maskGloss = lerp(0.45, 1.0, saturate(m.a));   // roughness-map gloss gate
            float glossBreak = (smoothBase >= 0.30) ? maskGloss : 1.0;
            float layerSmooth = saturate((smoothBase * glossBreak + smoothMod + macroWetSmooth * smoothBase) * smoothGain);
            // Metallic: per-layer substrate base, with the mask.r channel adding local mineral
            // speckle. Sediments stay dielectric; nodule/oxide layers carry real metallic.
            float metalBase  = HectonLayerMetallicBase(k);
            float layerMetal = saturate(metalBase + m.r * 0.35);

            albedo    += finalColor * weights[k];
            normalTS  += nd        * weights[k];
            metallic  += layerMetal  * weights[k];
            ao        += m.g         * weights[k];
            smoothness += layerSmooth * weights[k];
        }
    }

    // R95 NaN SHIELD (shaders.md: finite parameter ranges with NaN/Inf guards). If every control
    // weight in this texel is <= 0.005 (zeroed/unbound control-map region), the accumulators stay
    // at exactly 0 and normalize(0) yields NaN, which poisons TAA/bloom with spreading fireflies.
    // Fall back to a flat ShellSand presentation instead of emitting NaN.
    float normalLenSq = dot(normalTS, normalTS);
    bool degenerateTexel = normalLenSq < 1e-8;

    TerrainSample result;
    result.albedo     = degenerateTexel ? HectonMaterialPalette(0) : albedo;
    result.normalTS   = degenerateTexel ? float3(0.0, 0.0, 1.0) : normalTS * rsqrt(normalLenSq);
    result.smoothness = degenerateTexel ? HectonLayerSmoothnessBase(0) : smoothness;
    result.metallic   = degenerateTexel ? 0.0 : metallic;
    result.ao         = degenerateTexel ? 1.0 : ao;

    // --- HECTON-8 Phase 8.5 GPU diagnostic overrides ---
    // Fragment-only multi_compile variants. The consumer (SplatmapFragment) short-circuits
    // to an unlit passthrough for these variants so the encoded value is read straight off
    // the framebuffer without PBR shading distortion.
#if defined(_DEBUG_NORMALS)
    // Tangent-space surface normal encoded to [0,1]. Flat surface -> (0.5,0.5,1.0) bluish;
    // high-frequency bump structure from _NormalArray shows as R/G perturbation.
    result.albedo = result.normalTS * 0.5 + 0.5;
#elif defined(_DEBUG_STOCHASTIC_FADE)
    // Distance-gate heatmap: R = stochasticFade (1.0 full 4-corner < 60m),
    // G = 1-fade (1.0 single-sample far > 120m). Red->Yellow->Green across camera space.
    result.albedo = float3(stochasticFade, 1.0 - stochasticFade, 0.0);
#endif

    return result;
}

#endif

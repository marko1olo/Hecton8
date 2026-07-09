#ifndef HECTON_TERRAIN_SAMPLING_HLSL
#define HECTON_TERRAIN_SAMPLING_HLSL

// _Control / sampler_Control are declared by TerrainLitInput.hlsl - do NOT redeclare.
TEXTURE2D(_Control1);

// Our custom texture arrays (set via AssignTex.cs on the material)
TEXTURE2D_ARRAY(_AlbedoArray);
TEXTURE2D_ARRAY(_NormalArray);
TEXTURE2D_ARRAY(_MaskArray);
// SAMPLER(sampler_LinearRepeat); // Declared by URP internals

CBUFFER_START(UnityPerMaterial)
    float _HectonUVScale;
    float _HectonTriplanarBlend;
    float _HectonMacroVariationStrength;
CBUFFER_END

struct TerrainSample
{
    half3 albedo;
    half3 normalTS;
    half  metallic;
    half  smoothness;
    float ao;
};

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
// Works by blending 3 overlapping cells with smooth weights
float3 SampleStochastic_Albedo(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx)
{
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
    float2 dxdy = uv - cell;
    float2 uv00 = uv + h00.xy * 0.3 + float2(h00.z * 0.7, 0.0);
    float2 uv10 = uv + h10.xy * 0.3 + float2(h10.z * 0.7, 0.0);
    float2 uv01 = uv + h01.xy * 0.3 + float2(h01.z * 0.7, 0.0);
    float2 uv11 = uv + h11.xy * 0.3 + float2(h11.z * 0.7, 0.0);

    float3 s00 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv00, layerIdx).rgb;
    float3 s10 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv10, layerIdx).rgb;
    float3 s01 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv01, layerIdx).rgb;
    float3 s11 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv11, layerIdx).rgb;

    // Bilinear blend in perceptual space (sqrt -> blend -> sq to reduce darkening)
    s00 = sqrt(max(s00, 0.0001));
    s10 = sqrt(max(s10, 0.0001));
    s01 = sqrt(max(s01, 0.0001));
    s11 = sqrt(max(s11, 0.0001));
    float3 result = lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);
    return result * result;
}

float3 SampleStochastic_Normal(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx)
{
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

    float3 s00 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv00, layerIdx).rgb;
    float3 s10 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv10, layerIdx).rgb;
    float3 s01 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv01, layerIdx).rgb;
    float3 s11 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv11, layerIdx).rgb;

    return lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);
}

float4 SampleStochastic_Mask(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx)
{
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

    float4 s00 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv00, layerIdx);
    float4 s10 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv10, layerIdx);
    float4 s01 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv01, layerIdx);
    float4 s11 = SAMPLE_TEXTURE2D_ARRAY(tex, smp, uv11, layerIdx);

    return lerp(lerp(s00, s10, w.x), lerp(s01, s11, w.x), w.y);
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

float3 HectonWhiteoutBlendTS(float3 baseNormalTS, float3 detailNormalTS)
{
    return normalize(float3(baseNormalTS.xy + detailNormalTS.xy, baseNormalTS.z * detailNormalTS.z));
}

float3 HectonSampleMacroAlbedo(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float macroMask)
{
    float3 baseSample = SampleStochastic_Albedo(tex, smp, uv, layerIdx);
    float3 rotatedSample = SampleStochastic_Albedo(tex, smp, HectonRotatedScaledUV(uv, layerIdx), layerIdx);
    return lerp(baseSample, rotatedSample, macroMask);
}

float3 HectonSampleMacroNormalTS(TEXTURE2D_ARRAY_PARAM(tex, smp), float2 uv, float layerIdx, float macroMask)
{
    float3 baseNormal = HectonDecodeArrayNormal(SampleStochastic_Normal(tex, smp, uv, layerIdx));
    float3 rotatedNormal = HectonDecodeArrayNormal(SampleStochastic_Normal(tex, smp, HectonRotatedScaledUV(uv, layerIdx), layerIdx));
    return normalize(lerp(baseNormal, rotatedNormal, macroMask));
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
    float3 chroma = lerp(paletteChroma, sampledChroma, sampledValid * 0.45);
    float macroStrength = max(_HectonMacroVariationStrength, 0.0);
    float targetLum = HectonMaterialBaseLum(layer) * lerp(0.70, 1.34, procLum) * lerp(0.88, 1.16, macroVar * macroStrength);
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

    // Two UV sets at different scales for near/far frequency coverage
    // uvScale=400 → tile every 2.5m at 1000m terrain. Good for micro.
    // We also add a coarser set at /4 scale for mid-distance.
    float2 uvXZ_fine  = detailUV * uvScale;
    float2 uvXZ_coarse = detailUV * (uvScale * 0.18); // ~1 tile per 14m
    
    float2 uvXY_fine   = (worldPos.xy * 0.001) * uvScale;
    float2 uvZY_fine   = (worldPos.zy * 0.001) * uvScale;
    float2 uvXY_coarse = (worldPos.xy * 0.001) * (uvScale * 0.18);
    float2 uvZY_coarse = (worldPos.zy * 0.001) * (uvScale * 0.18);

    // Distance-based blend between fine and coarse (fade fine tiling at distance)
    float camDist = length(worldPos - _WorldSpaceCameraPos);
    // Fine tiling fades out 10-60m, coarse dominates beyond
    float fineFade = 1.0 - saturate((camDist - 10.0) / 50.0);
    float macroAntiTileMask = HectonMacroNoise(worldPos.xz);

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
        if (weights[h] > 0.001)
        {
            float height = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_fine, (float)h).b;
            blend[h] = weights[h] + height;
            maxBlend = max(maxBlend, blend[h]);
        }
        else
        {
            blend[h] = 0.0;
        }
    }

    float heightTransition = 0.05;
    float totalW = 0.001;
    [unroll]
    for (int w = 0; w < 8; w++)
    {
        if (weights[w] > 0.001)
        {
            float b = max(0.0, blend[w] - maxBlend + heightTransition);
            b = pow(b, 8.0);
            weights[w] = b;
            totalW += weights[w];
        }
    }

    float invW = 1.0 / totalW;
    [unroll]
    for (int i = 0; i < 8; i++) weights[i] *= invW;

    // Large-scale color variation noise (breaks uniform look at macro distance)
    // Period ~500m in worldspace → visible at 200-2000m camera distance
    float macroColorNoise = HectonNoise2D(worldPos.xz * 0.002) * 0.5 + 0.5;
    float macroColorNoise2 = HectonNoise2D(worldPos.xz * 0.0007 + float2(17.3, 5.1)) * 0.5 + 0.5;
    // Combined: large blob variation
    float macroVar = macroColorNoise * 0.6 + macroColorNoise2 * 0.4;

    float3 albedo    = (float3)0;
    float3 normalTS  = (float3)0;
    float  smoothness = 0;
    float  metallic   = 0;
    float  ao         = 0;

    [unroll]
    for (int k = 0; k < 8; k++)
    {
        [branch]
        if (weights[k] > 0.001)
        {
            float3 a = 0;
            float3 n = 0;
            float4 m = 0;

            float3 a_y = 0; float3 n_y = 0; float4 m_y = 0;
            float3 a_x = 0; float3 n_x = 0; float4 m_x = 0;
            float3 a_z = 0; float3 n_z = 0; float4 m_z = 0;

            // Sample fine + coarse and blend by distance
            [branch] if (biW.y > 0)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_fine,   (float)k, macroAntiTileMask);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_coarse, (float)k, macroAntiTileMask);
                a_y = lerp(ac, af, fineFade);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXZ_fine,   (float)k, macroAntiTileMask);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXZ_coarse, (float)k, macroAntiTileMask);
                n_y = HectonApplySandRippleTS(lerp(nc, nf, fineFade), worldPos.xz, (float)k, saturate(weights[0] * 1.35));
                m_y = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_fine, (float)k);
                a += a_y * biW.y;
                n += n_y * biW.y;
                m += m_y * biW.y;
            }
            [branch] if (biW.x > 0)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvZY_fine,   (float)k, macroAntiTileMask);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvZY_coarse, (float)k, macroAntiTileMask);
                a_x = lerp(ac, af, fineFade);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvZY_fine,   (float)k, macroAntiTileMask);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvZY_coarse, (float)k, macroAntiTileMask);
                n_x = HectonApplySandRippleTS(lerp(nc, nf, fineFade), worldPos.zy, (float)k, saturate(weights[0] * 1.35));
                m_x = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvZY_fine, (float)k);
                a += a_x * biW.x;
                n += float3(n_x.z, n_x.y, n_x.x) * biW.x;
                m += m_x * biW.x;
            }
            [branch] if (biW.z > 0)
            {
                float3 af = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXY_fine,   (float)k, macroAntiTileMask);
                float3 ac = HectonSampleMacroAlbedo(_AlbedoArray, sampler_LinearRepeat, uvXY_coarse, (float)k, macroAntiTileMask);
                a_z = lerp(ac, af, fineFade);
                float3 nf = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXY_fine,   (float)k, macroAntiTileMask);
                float3 nc = HectonSampleMacroNormalTS(_NormalArray, sampler_LinearRepeat, uvXY_coarse, (float)k, macroAntiTileMask);
                n_z = HectonApplySandRippleTS(lerp(nc, nf, fineFade), worldPos.xy, (float)k, saturate(weights[0] * 1.35));
                m_z = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXY_fine, (float)k);
                a += a_z * biW.z;
                n += float3(n_z.x, n_z.z, n_z.y) * biW.z;
                m += m_z * biW.z;
            }

            float3 nd = normalize(n);

            // True geological luminance override. The texture array supplies chroma when valid;
            // procedural macro/meso/micro noise owns brightness so stub textures cannot flatten the seafloor.
            float lumMacro = HectonNoise2D(worldPos.xz * 0.002  + float2(3.7, 8.1));
            float lumMeso  = HectonNoise2D(worldPos.xz * 0.022  + float2(11.3, 2.9));
            float lumMicro = HectonNoise2D(worldPos.xz * 0.31   + float2(5.7, 17.1));
            float procLum  = lumMacro * 0.50 + lumMeso * 0.33 + lumMicro * 0.17;

            float3 finalColor = HectonApplyLuminanceOverride(a, k, procLum, macroVar);



            albedo    += finalColor * weights[k];
            normalTS  += nd        * weights[k];
            metallic  += m.r       * weights[k];
            ao        += m.g       * weights[k];
            smoothness += m.a * 0.18 * weights[k]; // 0.18 cap — wet submarine rock gets slight specular
        }
    }

    TerrainSample result;
    result.albedo     = albedo;
    result.normalTS   = normalize(normalTS);
    result.smoothness = smoothness;
    result.metallic   = metallic;
    result.ao         = ao;
    return result;
}

#endif

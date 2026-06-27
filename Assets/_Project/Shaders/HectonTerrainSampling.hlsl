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

    // Normal for biplanar
    float3 absNormal = abs(worldNormal);
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
                float3 af = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_fine,   (float)k);
                float3 ac = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_coarse, (float)k);
                a_y = lerp(ac, af, fineFade);
                float3 nf = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXZ_fine,   (float)k);
                float3 nc = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXZ_coarse, (float)k);
                n_y = lerp(nc, nf, fineFade);
                m_y = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_fine, (float)k);
                a += a_y * biW.y;
                n += n_y * biW.y;
                m += m_y * biW.y;
            }
            [branch] if (biW.x > 0)
            {
                float3 af = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvZY_fine,   (float)k);
                float3 ac = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvZY_coarse, (float)k);
                a_x = lerp(ac, af, fineFade);
                float3 nf = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvZY_fine,   (float)k);
                float3 nc = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvZY_coarse, (float)k);
                n_x = lerp(nc, nf, fineFade);
                m_x = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvZY_fine, (float)k);
                a += a_x * biW.x;
                n += float3(n_x.z, n_x.y, n_x.x) * biW.x;
                m += m_x * biW.x;
            }
            [branch] if (biW.z > 0)
            {
                float3 af = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXY_fine,   (float)k);
                float3 ac = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXY_coarse, (float)k);
                a_z = lerp(ac, af, fineFade);
                float3 nf = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXY_fine,   (float)k);
                float3 nc = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXY_coarse, (float)k);
                n_z = lerp(nc, nf, fineFade);
                m_z = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXY_fine, (float)k);
                a += a_z * biW.z;
                n += float3(n_z.x, n_z.z, n_z.y) * biW.z;
                m += m_z * biW.z;
            }

            // Decode BC5 normal
            float3 nd;
            nd.xy = n.rg * 2.0 - 1.0;
            nd.z  = sqrt(max(1e-6, 1.0 - dot(nd.xy, nd.xy)));

            float lum = dot(a, float3(0.299, 0.587, 0.114));
            float3 finalColor;

            if (k >= 2)
            {
                // HardRock/Basalt: dark, desaturated, with macro variation
                // Base: very dark teal-grey. macroVar modulates to brownish basalt.
                float3 darkRock  = float3(0.045, 0.050, 0.060);
                float3 lightRock = float3(0.160, 0.175, 0.185);
                float3 tintCold  = float3(0.85, 0.90, 1.00); // cold grey-blue
                float3 tintWarm  = float3(1.05, 0.95, 0.85); // warm basalt brown
                float3 base = lerp(darkRock, lightRock, lum);
                base *= lerp(tintCold, tintWarm, macroVar);
                // Additional meso-scale luminance patch
                float mesoVar = HectonNoise2D(worldPos.xz * 0.015) * 0.5 + 0.5;
                finalColor = base * lerp(0.75, 1.15, mesoVar);

                // Micro-normals: rock grit
                float nx = HectonNoise2D(worldPos.xz * 120.0) - 0.5;
                float ny = HectonNoise2D(worldPos.xz * 120.0 + float2(17.3, 31.1)) - 0.5;
                float3 gritNormal = normalize(float3(nx * 1.2, ny * 1.2, 1.0));
                nd = normalize(float3(nd.xy + gritNormal.xy, nd.z));
            }
            else
            {
                // Sand/Silt: cold deep-sea sediment with subtle macro colour drift
                float3 darkSand  = float3(0.08, 0.10, 0.13);
                float3 lightSand = float3(0.20, 0.24, 0.28);
                float3 tintA = float3(0.90, 0.95, 1.05); // cold blue-grey silt
                float3 tintB = float3(1.05, 1.00, 0.90); // slight ochre drift at sediment fans
                float3 base = lerp(darkSand, lightSand, lum);
                base *= lerp(tintA, tintB, macroVar);
                float mesoVar = HectonNoise2D(worldPos.xz * 0.012) * 0.5 + 0.5;
                finalColor = base * lerp(0.80, 1.10, mesoVar);

                // Sand ripples procedural normal
                float dx = cos(worldPos.x * 12.0 + worldPos.z * 3.0) * 0.05;
                float dy = sin(worldPos.z * 12.0 + worldPos.x * 3.0) * 0.05;
                float3 rippleNormal = normalize(float3(-dx, -dy, 1.0));
                nd = normalize(float3(nd.xy + rippleNormal.xy, nd.z));
            }

            albedo    += finalColor * weights[k];
            normalTS  += nd        * weights[k];
            metallic  += m.r       * weights[k];
            ao        += m.g       * weights[k];
            smoothness += m.a * 0.12 * weights[k];
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

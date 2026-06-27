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

// --- MACRO NOISE ANTI-TILING GENERATOR ---
float HectonHash2D(float2 p)
{
    // High-quality PRNG hash to eliminate grid tiling artifacts
    float3 p3  = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float HectonNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(lerp(HectonHash2D(i + float2(0.0, 0.0)), 
                     HectonHash2D(i + float2(1.0, 0.0)), u.x),
                lerp(HectonHash2D(i + float2(0.0, 1.0)), 
                     HectonHash2D(i + float2(1.0, 1.0)), u.x), u.y);
}

float HectonMacroNoise(float2 p)
{
    float v = 0.0;
    v += HectonNoise2D(p) * 0.6;
    v += HectonNoise2D(p * 2.5 + float2(3.1, 1.7)) * 0.4;
    return v;
}

void SampleTerrainLayerWithMacroNoise(float2 baseUV, float2 macroNoiseUV, float layerIndex, out float3 albedo, out float3 normal, out float4 mask)
{
    // 1. Generate low-frequency macro noise
    float macroNoise = HectonMacroNoise(macroNoiseUV);

    // 2. Sample at two different scales, rotations, and offsets to break tiling patterns
    float2 uv1 = baseUV;
    float2 uv2 = float2(baseUV.y, -baseUV.x) * 0.618 + float2(0.37, 0.19);

    float3 a1 = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, uv1, layerIndex).rgb;
    float3 n1 = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, uv1, layerIndex).rgb;
    float4 m1 = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, uv1, layerIndex);

    float3 a2 = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, uv2, layerIndex).rgb;
    float3 n2 = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, uv2, layerIndex).rgb;
    float4 m2 = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, uv2, layerIndex);

    // 3. Blend them based on the macro noise
    albedo = lerp(a1, a2, macroNoise);
    normal = lerp(n1, n2, macroNoise);
    mask   = lerp(m1, m2, macroNoise);
    
    // 4. Large-scale macro color blend to destroy repetition across the landscape
    float colorNoise = HectonNoise2D(macroNoiseUV * 0.15) * 0.5 + 0.5; // Smooth large gradient
    float3 tintA = float3(0.7, 0.75, 0.8);
    float3 tintB = float3(1.0, 0.95, 0.9);
    albedo *= lerp(tintA, tintB, colorNoise);
}

// 8 layers from _Control and _Control1
TerrainSample SampleHectonTerrain(float2 controlUV, float2 detailUV, float3 worldPos, float3 worldNormal, float3 viewDirTS)
{
    float4 ctrl = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);
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
    if (uvScale < 0.0001) uvScale = 1.0; // Fallback: 1.0 keeps detailUV as-is

    // Top-down UV: use detailUV (terrain local 0..1) scaled by uvScale
    // Do NOT use worldPos.xz — it breaks UV continuity across chunks at low uvScale values
    float2 uvXZ = detailUV * uvScale;
    // Biplanar wall projections still use worldPos for correct cliff texturing
    float2 uvXY = worldPos.xy * uvScale;
    float2 uvZY = worldPos.zy * uvScale;

    // Нормаль для весов
    float3 absNormal = abs(worldNormal);
    // Отсекаем слабую ось для Biplanar
    float3 biW = absNormal;
    if (biW.x <= biW.y && biW.x <= biW.z) biW.x = 0;
    else if (biW.y <= biW.x && biW.y <= biW.z) biW.y = 0;
    else biW.z = 0;
    
    biW /= (biW.x + biW.y + biW.z + 1e-6);

    // Height blending over the Y axis mostly, to sharpen material transitions
    float blend[8];
    float maxBlend = 0.0;
    [unroll]
    for (int h = 0; h < 8; h++)
    {
        if (weights[h] > 0.001)
        {
            float height = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_LinearRepeat, uvXZ, (float)h).b;
            blend[h] = weights[h] + height;
            maxBlend = max(maxBlend, blend[h]);
        }
        else
        {
            blend[h] = 0.0;
        }
    }

    float heightTransition = 0.15;
    float totalW = 0.001;
    [unroll]
    for (int w = 0; w < 8; w++)
    {
        if (weights[w] > 0.001)
        {
            weights[w] = max(0.0, blend[w] - maxBlend + heightTransition);
            totalW += weights[w];
        }
    }

    float invW = 1.0 / totalW;
    [unroll]
    for (int i = 0; i < 8; i++) weights[i] *= invW;

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

            [branch] if (biW.y > 0)
            {
                SampleTerrainLayerWithMacroNoise(uvXZ, worldPos.xz * 0.0015, (float)k, a_y, n_y, m_y);
                a += a_y * biW.y;
                n += n_y * biW.y;
                m += m_y * biW.y;
            }
            [branch] if (biW.x > 0)
            {
                SampleTerrainLayerWithMacroNoise(uvZY, worldPos.zy * 0.0015, (float)k, a_x, n_x, m_x);
                a += a_x * biW.x;
                // Swizzle normal for X plane
                n += float3(n_x.z, n_x.y, n_x.x) * biW.x;
                m += m_x * biW.x;
            }
            [branch] if (biW.z > 0)
            {
                SampleTerrainLayerWithMacroNoise(uvXY, worldPos.xy * 0.0015, (float)k, a_z, n_z, m_z);
                a += a_z * biW.z;
                // Swizzle normal for Z plane
                n += float3(n_z.x, n_z.z, n_z.y) * biW.z;
                m += m_z * biW.z;
            }

            // Decode BC5 normal
            float3 nd;
            nd.xy = n.rg * 2.0 - 1.0;
            nd.z  = sqrt(max(1e-6, 1.0 - dot(nd.xy, nd.xy)));

            albedo    += a    * weights[k];
            normalTS  += nd   * weights[k];
            metallic  += m.r  * weights[k];
            ao        += m.g  * weights[k];
            smoothness += m.a * 0.15 * weights[k];
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

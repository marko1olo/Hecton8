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
    float2 uv  = detailUV * uvScale;

    // --- HEIGHT BLENDING ---
    // Sample height for all present layers to sharpen transitions and avoid mud
    float blend[8];
    float maxBlend = 0.0;
    [unroll]
    for (int h = 0; h < 8; h++)
    {
        if (weights[h] > 0.001)
        {
            float height = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_LinearRepeat, uv, (float)h).b;
            blend[h] = weights[h] + height;
            maxBlend = max(maxBlend, blend[h]);
        }
        else
        {
            blend[h] = 0.0;
        }
    }

    float heightTransition = 0.15; // Lower means sharper transitions
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

    // Normalize
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
            float2 curUV = uv;

            float3 a = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, curUV, (float)k).rgb;
            float3 n = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, curUV, (float)k).rgb;
            float4 m = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, curUV, (float)k);

            // Stochastic macro-variation on flat sediment (0, 2, 5)
            if (k == 0 || k == 2 || k == 5)
            {
                float2 uv2  = curUV * 0.41 + 0.3;

                float3 a2 = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, uv2, (float)k).rgb;
                float3 n2 = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, uv2, (float)k).rgb;
                float4 m2 = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, uv2, (float)k);

                // Multi-scale stochastic variation to completely hide tiling from a distance
                float noiseLarge = sin(worldPos.x * 0.002) * cos(worldPos.z * 0.0031);
                float noiseMedium = sin(worldPos.x * 0.013 + worldPos.z * 0.009) * cos(worldPos.x * 0.011 - worldPos.z * 0.017);
                float noiseSmall = sin(worldPos.x * 0.05) * cos(worldPos.z * 0.07);
                
                float noise = saturate((noiseLarge * 0.5 + noiseMedium * 0.35 + noiseSmall * 0.15) * 0.5 + 0.5);

                a = lerp(a, a2, noise);
                n = lerp(n, n2, noise);
                m = lerp(m, m2, noise);
            }

            // Decode BC5 normal
            float3 nd;
            nd.xy = n.rg * 2.0 - 1.0;
            nd.z  = sqrt(max(1e-6, 1.0 - dot(nd.xy, nd.xy)));

            float slope = abs(worldNormal.y);
            // Universal Triplanar on ALL layers on steep slopes to prevent vertical stretching
            if (slope < 0.7)
            {
                float triplanarScale = 0.05; // 20m per tile
                float2 uvX  = worldPos.zy * triplanarScale;
                float2 uvZ  = worldPos.xy * triplanarScale;

                float3 aX = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, uvX, (float)k).rgb;
                float3 nX = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, uvX, (float)k).rgb;
                float4 mX = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, uvX, (float)k);

                float3 aZ = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_LinearRepeat, uvZ, (float)k).rgb;
                float3 nZ = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_LinearRepeat, uvZ, (float)k).rgb;
                float4 mZ = SAMPLE_TEXTURE2D_ARRAY(_MaskArray,   sampler_LinearRepeat, uvZ, (float)k);

                float bF     = pow(1.0 - saturate(slope / 0.7), _HectonTriplanarBlend);
                float normX  = abs(worldNormal.x);
                float normZ  = abs(worldNormal.z);
                float triTot = max(0.001, normX + normZ);

                float3 nXd; nXd.xy = nX.rg * 2.0 - 1.0; nXd.z = sqrt(max(1e-6, 1.0 - dot(nXd.xy, nXd.xy)));
                float3 nZd; nZd.xy = nZ.rg * 2.0 - 1.0; nZd.z = sqrt(max(1e-6, 1.0 - dot(nZd.xy, nZd.xy)));

                float3 tsX = float3(nXd.z * sign(worldNormal.x), nXd.x, nXd.y);
                float3 tsZ = float3(nZd.x, nZd.z * sign(worldNormal.z), nZd.y);

                a  = lerp(a,  (aX * normX + aZ * normZ) / triTot, bF);
                nd = lerp(nd, (tsX * normX + tsZ * normZ) / triTot, bF);
                m  = lerp(m,  (mX * normX + mZ * normZ)  / triTot, bF);
            }

            albedo    += a    * weights[k];
            normalTS  += nd   * weights[k];
            metallic  += m.r  * weights[k];
            ao        += m.g  * weights[k];
            smoothness += m.a * weights[k];
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

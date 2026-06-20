#ifndef HECTON_TERRAIN_SAMPLING_HLSL
#define HECTON_TERRAIN_SAMPLING_HLSL

// _Control / sampler_Control are declared by TerrainLitInput.hlsl — do NOT redeclare.

// Our custom texture arrays (set via AssignTex.cs on the material)
TEXTURE2D_ARRAY(_AlbedoArray);
TEXTURE2D_ARRAY(_NormalArray);
TEXTURE2D_ARRAY(_MaskArray);
SAMPLER(sampler_AlbedoArray);

// No custom CBUFFER — UVScale/TriplanarBlend are hardcoded for now
// to avoid SRP Batcher CBUFFER conflict with UnityPerMaterial from TerrainLitInput.hlsl.
// TODO: move these into UnityPerMaterial or use material property overrides.

CBUFFER_START(UnityPerMaterial)
    float _HectonUVScale;
    float _HectonTriplanarBlend;
CBUFFER_END

struct TerrainSample
{
    float3 albedo;
    float3 normalTS;
    float  smoothness;
    float  metallic;
    float  ao;
};

// 4 layers from _Control: R=ShellSand, G=LimestoneShelf, B=ClaySilt, A=HardRock
TerrainSample SampleHectonTerrain(float2 controlUV, float2 detailUV, float3 worldPos, float3 worldNormal, float3 viewDirTS)
{
    float4 ctrl = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);

    float weights[4];
    weights[0] = ctrl.r;
    weights[1] = ctrl.g;
    weights[2] = ctrl.b;
    weights[3] = ctrl.a;

    // Normalize
    float totalW = weights[0] + weights[1] + weights[2] + weights[3];
    if (totalW > 0.001)
    {
        float invW = 1.0 / totalW;
        weights[0] *= invW;
        weights[1] *= invW;
        weights[2] *= invW;
        weights[3] *= invW;
    }
    else
    {
        weights[0] = 1.0;
    }

    float3 albedo    = (float3)0;
    float3 normalTS  = (float3)0;
    float  smoothness = 0;
    float  metallic   = 0;
    float  ao         = 0;

    float uvScale = _HectonUVScale;
    float2 uv  = detailUV * uvScale;
    float2 dx  = ddx(uv);
    float2 dy  = ddy(uv);

    float2 uvX  = worldPos.zy * uvScale;
    float2 dxX  = ddx(uvX);
    float2 dyX  = ddy(uvX);

    float2 uvZ  = worldPos.xy * uvScale;
    float2 dxZ  = ddx(uvZ);
    float2 dyZ  = ddy(uvZ);

    [unroll]
    for (int k = 0; k < 4; k++)
    {
        [branch]
        if (weights[k] > 0.001)
        {
            float2 curUV = uv;

            // Parallax on rocky layers (1=Limestone, 3=HardRock)
            if (k == 1 || k == 3)
            {
                float h = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray, sampler_AlbedoArray, curUV, (float)k, dx, dy).b;
                curUV += viewDirTS.xy * (h - 0.5) * 0.01 / max(0.2, viewDirTS.z);
            }

            float3 a = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, curUV, (float)k, dx, dy).rgb;
            float3 n = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, curUV, (float)k, dx, dy).rgb;
            float4 m = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,   sampler_AlbedoArray, curUV, (float)k, dx, dy);

            // Stochastic macro-variation on flat sediment (0=Sand, 2=Clay)
            if (k == 0 || k == 2)
            {
                float2 uv2  = curUV * 0.41 + 0.3;
                float2 dx2  = dx * 0.41;
                float2 dy2  = dy * 0.41;

                float3 a2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv2, (float)k, dx2, dy2).rgb;
                float3 n2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, uv2, (float)k, dx2, dy2).rgb;
                float4 m2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,   sampler_AlbedoArray, uv2, (float)k, dx2, dy2);

                float noise = saturate(sin(worldPos.x * 0.05) * cos(worldPos.z * 0.07)
                            + sin(worldPos.x * 0.13 + worldPos.z * 0.09)) * 0.5 + 0.5;

                a = lerp(a, a2, noise);
                n = lerp(n, n2, noise);
                m = lerp(m, m2, noise);
            }

            // Decode BC5 normal (RG -> XYZ)
            float3 nd;
            nd.xy = n.rg * 2.0 - 1.0;
            nd.z  = sqrt(max(1e-6, 1.0 - dot(nd.xy, nd.xy)));

            // Triplanar on steep hard rock (layer 3)
            if (k == 3)
            {
                float slope = abs(worldNormal.y);
                if (slope < 0.7)
                {
                    float3 aX = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uvX, 3.0, dxX, dyX).rgb;
                    float3 nX = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, uvX, 3.0, dxX, dyX).rgb;
                    float4 mX = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,   sampler_AlbedoArray, uvX, 3.0, dxX, dyX);

                    float3 aZ = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uvZ, 3.0, dxZ, dyZ).rgb;
                    float3 nZ = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, uvZ, 3.0, dxZ, dyZ).rgb;
                    float4 mZ = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,   sampler_AlbedoArray, uvZ, 3.0, dxZ, dyZ);

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

Shader "Hidden/Hecton8/DeferredCaustics"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        // This fullscreen composite is LIGHT_COOKIE independent. LIGHT_COOKIE variants are stripped deliberately:
        // caustics come from screen depth + procedural math, not from LIGHT_COOKIE or PROJECTOR passes.
        #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
        #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(HectonAbyssalCaustics)
            float4 ProjectionVectorAndScale;   // xyz=sun-to-surface direction, w=noise scale
            float4 NoiseAnimationSpeed;        // xy=wrapped AUP xz, z=flow phase, w=chromatic dispersion
            float4 IntensityAndDepthFalloff;   // x=intensity, y=inv depth range, z=max depth, w=SDF shadow strength
            float4 QualityAndColor;            // x=GlobalQualityWeight, yzw=linear RGB tint
        CBUFFER_END

        TEXTURE2D_X(_HectonDeferredCausticsSource);
        TEXTURE2D_X_FLOAT(_HectonDeferredCausticsDepth);
        TEXTURE3D(_HectonCaveVoxelSdfTex);
        SAMPLER(sampler_HectonCaveVoxelSdfTex);
        float _HectonCaveVoxelActive;
        float4x4 _HectonCaveVoxelWorldToLocal;
        float4 _HectonCaveVoxelHalfExtents;
        float4 _HectonCaveVoxelInvDoubleHalfExtents;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float ResolveDepthValid(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float SampleCausticsDepth(float2 screenUV)
        {
            return SAMPLE_TEXTURE2D_X(_HectonDeferredCausticsDepth, sampler_PointClamp, screenUV).r;
        }

        float SafeFinite(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float3 SafeNormalize3(float3 value, float3 fallbackValue)
        {
            float lenSq = dot(value, value);
            float valid = (isfinite(lenSq) && lenSq > 0.000001) ? 1.0 : 0.0;
            float3 normalized = value * rsqrt(max(lenSq, 0.000001));
            return lerp(fallbackValue, normalized, valid);
        }

        float2 Hash22(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.xx + p3.yz) * p3.zy);
        }

        float VoronoiDistanceSq(float2 uv)
        {
            float2 cell = floor(uv);
            float2 local = frac(uv);
            float best = 8.0;
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = float2((float)x, (float)y);
                    float2 jitter = Hash22(cell + neighbor);
                    float2 delta = neighbor + jitter - local;
                    best = min(best, dot(delta, delta));
                }
            }

            return best;
        }

        float CausticLineLayer(float2 uv)
        {
            float distanceSqToCell = VoronoiDistanceSq(uv);
            float lineMask = saturate(1.0 - distanceSqToCell * 1.85);
            lineMask *= lineMask;
            return lineMask * lineMask;
        }

        float SampleCaveVoxelSignedDistance(float3 positionWS)
        {
            if (_HectonCaveVoxelActive <= 0.5)
                return _HectonCaveVoxelHalfExtents.w;

            float3 invDoubleHalfExtents = _HectonCaveVoxelInvDoubleHalfExtents.xyz;
            if (any(invDoubleHalfExtents <= 0.0))
                return _HectonCaveVoxelHalfExtents.w;

            float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
            float3 sampleUv = localPosition * invDoubleHalfExtents + 0.5;
            if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
                return _HectonCaveVoxelHalfExtents.w;

            float encoded = SAMPLE_TEXTURE3D_LOD(_HectonCaveVoxelSdfTex, sampler_HectonCaveVoxelSdfTex, sampleUv, 0).r;
            return lerp(-_HectonCaveVoxelHalfExtents.w, _HectonCaveVoxelHalfExtents.w, encoded);
        }

        float ResolveSdfCavernOcclusion(float3 worldPos, float3 sunToSurface, float quality, float strength)
        {
            if (_HectonCaveVoxelActive <= 0.5 || strength <= 0.0001)
                return 1.0;

            float firstSdf = SampleCaveVoxelSignedDistance(worldPos + (-sunToSurface) * 0.35);
            float shadow = smoothstep(-0.08, 0.85, firstSdf);
            float3 towardSun = -sunToSurface;
            float stepBase = lerp(1.2, 3.8, saturate(quality));
            float sdfSampleBudget = saturate((quality - 0.30) * 1.4285715) * 4.0;
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                float stepWeight = saturate(sdfSampleBudget - (float)i);
                [branch]
                if (stepWeight > 0.0001)
                {
                    float3 samplePos = worldPos + towardSun * (stepBase * ((float)i + 1.0));
                    float sdf = SampleCaveVoxelSignedDistance(samplePos);
                    float rayOpen = smoothstep(0.05, 1.65 + (float)i * 0.45, sdf);
                    shadow = min(shadow, lerp(1.0, rayOpen, stepWeight));
                }
            }

            return lerp(1.0, saturate(shadow), saturate(strength));
        }

        float2 ProjectCausticUv(float3 worldPos, float3 sunToSurface, float scale)
        {
            float projectedDepth = dot(worldPos, sunToSurface);
            float2 wrappedOffset = NoiseAnimationSpeed.xy;
            float flowPhase = NoiseAnimationSpeed.z;
            float2 projected = worldPos.xz - sunToSurface.xz * projectedDepth;
            float2 flow = float2(flowPhase * 0.073, flowPhase * -0.041);
            return projected * max(scale, 0.0001) + wrappedOffset * max(scale, 0.0001) + flow;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 screenUV = UnityStereoTransformScreenSpaceTex(input.screenUV);
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_HectonDeferredCausticsSource, sampler_LinearClamp, screenUV);
            float rawDepth = SampleCausticsDepth(screenUV);
            [branch]
            if (ResolveDepthValid(rawDepth) <= 0.5)
                return sourceColor;

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            float maxDepthMeters = max(SafeFinite(IntensityAndDepthFalloff.z, 0.0), 0.0);
            [branch]
            if (linearEyeDepth > maxDepthMeters || maxDepthMeters <= 0.001)
                return sourceColor;

            float quality = saturate(SafeFinite(QualityAndColor.x, 0.0));
            float intensity = max(SafeFinite(IntensityAndDepthFalloff.x, 0.0), 0.0);
            [branch]
            if (intensity <= 0.0001 || quality <= 0.0001)
                return sourceColor;

            float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            float3 sunToSurface = SafeNormalize3(ProjectionVectorAndScale.xyz, float3(0.0, -1.0, 0.0));
            float baseScale = max(SafeFinite(ProjectionVectorAndScale.w, 0.05), 0.001);
            float2 uv0 = ProjectCausticUv(worldPos, sunToSurface, baseScale);

            float layer0 = CausticLineLayer(uv0);
            float secondWeight = smoothstep(0.34, 0.82, quality);
            float chromaWeight = smoothstep(0.62, 1.0, quality);
            float caustic = layer0;
            [branch]
            if (secondWeight > 0.0001)
            {
                float2 uv1 = uv0 * (1.731 + quality * 0.19) + float2(17.31, -9.42) + NoiseAnimationSpeed.z * float2(-0.019, 0.027);
                caustic += CausticLineLayer(uv1) * secondWeight * 0.62;
            }

            float depthFade = saturate(1.0 - linearEyeDepth * max(IntensityAndDepthFalloff.y, 0.00001));
            depthFade *= depthFade;
            float sdfOcclusion = ResolveSdfCavernOcclusion(worldPos, sunToSurface, quality, IntensityAndDepthFalloff.w);
            float chromaticDispersion = saturate(NoiseAnimationSpeed.w) * chromaWeight;
            float causticR = caustic;
            float causticB = caustic;
            [branch]
            if (chromaticDispersion > 0.0001)
            {
                causticR = lerp(caustic, CausticLineLayer(uv0 + sunToSurface.xz * (0.035 + chromaticDispersion * 0.055)), chromaticDispersion);
                causticB = lerp(caustic, CausticLineLayer(uv0 - sunToSurface.xz * (0.029 + chromaticDispersion * 0.051)), chromaticDispersion);
            }

            half3 causticTint = half3(QualityAndColor.y, QualityAndColor.z, QualityAndColor.w);
            half3 causticRgb = half3(causticR, caustic, causticB) * causticTint;
            half energy = (half)(intensity * depthFade * sdfOcclusion);
            sourceColor.rgb = sourceColor.rgb + sourceColor.rgb * causticRgb * energy;
            return sourceColor;
        }
        ENDHLSL

        Pass
        {
            Name "DeferredCaustics"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}

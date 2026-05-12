Shader "Hidden/Hecton8/AbyssalSSDO"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5
        #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
        #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _HectonAbyssalSsdoPassMode;
            float4 _HectonAbyssalSsdoInputSize;
            float4 _HectonAbyssalSsdoOutputSize;
            float _HectonAbyssalSsdoRadiusMeters;
            float _HectonAbyssalSsdoIntensity;
            float _HectonAbyssalSsdoBias;
            float _HectonAbyssalSsdoDepthSigma;
            float _HectonAbyssalSsdoBlurDepthThreshold;
            float _HectonAbyssalSsdoProjectionScale;
            float _HectonAbyssalSsdoCompositeStrength;
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonAbyssalSSDOTex);
        float4 _BlitTexture_TexelSize;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float SafeRcp(float value)
        {
            return value > 0.00001 ? rcp(value) : 0.0;
        }

        float ResolveTaaDitherPhaseNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            uint2 pixelParity = (uint2)pixel & 1u;
            uint phaseIndex = pixelParity.x | (pixelParity.y << 1u);
            float2 taaPhase = float2((float)(phaseIndex & 1u), (float)((phaseIndex >> 1u) & 1u)) * 0.5;
            return frac(52.9829189 * frac(dot(pixel + taaPhase, float2(0.06711056, 0.00583715))));
        }

        float2 ResolveOctantRotation(float noiseValue)
        {
            static const float2 kOctantRotations[8] =
            {
                float2(1.0, 0.0),
                float2(0.7071068, 0.7071068),
                float2(0.0, 1.0),
                float2(-0.7071068, 0.7071068),
                float2(-1.0, 0.0),
                float2(-0.7071068, -0.7071068),
                float2(0.0, -1.0),
                float2(0.7071068, -0.7071068)
            };
            uint rotationIndex = (uint)(saturate(noiseValue) * 8.0) & 7u;
            return kOctantRotations[rotationIndex];
        }

        float2 Rotate2D(float2 value, float2 rotation)
        {
            return float2(
                value.x * rotation.x - value.y * rotation.y,
                value.x * rotation.y + value.y * rotation.x);
        }

        float ResolveRawDepthValidity(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        void SampleSceneLinearDepth(float2 screenUV, out float rawDepth, out float depthValid, out float linearEyeDepth)
        {
            rawDepth = SampleSceneDepth(screenUV);
            depthValid = ResolveRawDepthValidity(rawDepth);
            if (depthValid <= 0.5)
            {
                linearEyeDepth = 0.0;
                return;
            }

            linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        }

        half EvaluateDirectionalOcclusion(float2 screenUV)
        {
            float rawDepth;
            float depthValid;
            float linearEyeDepth;
            SampleSceneLinearDepth(screenUV, rawDepth, depthValid, linearEyeDepth);
            if (depthValid <= 0.5)
                return 1.0h;

            float radiusMeters = max(_HectonAbyssalSsdoRadiusMeters, 0.05);
            float radiusMetersSq = radiusMeters * radiusMeters;
            float pixelRadius = _HectonAbyssalSsdoProjectionScale * SafeRcp(max(linearEyeDepth, 0.1));
            float2 uvRadius = pixelRadius * _HectonAbyssalSsdoInputSize.zw;
            float2 rotation = ResolveOctantRotation(ResolveTaaDitherPhaseNoise(screenUV));
            float2 screenBias = (screenUV - 0.5) * 0.25;

            static const float2 kKernel[4] =
            {
                float2(1.0, 0.0),
                float2(-1.0, 0.0),
                float2(0.0, 1.0),
                float2(0.0, -1.0)
            };

            float accumulated = 0.0;
            float invRadiusMeters = SafeRcp(radiusMeters);
            float invRadiusMetersSq = SafeRcp(radiusMetersSq);
            [unroll(4)]
            for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
            {
                float2 rotatedDirection = Rotate2D(kKernel[sampleIndex], rotation);
                float2 sampleUV = saturate(screenUV + rotatedDirection * uvRadius);
                float sampleRawDepth;
                float sampleDepthValid;
                float sampleLinearEyeDepth;
                SampleSceneLinearDepth(sampleUV, sampleRawDepth, sampleDepthValid, sampleLinearEyeDepth);
                if (sampleDepthValid <= 0.5)
                    continue;

                float depthDelta = linearEyeDepth - sampleLinearEyeDepth;
                if (depthDelta <= 0.0)
                    continue;

                float depthDeltaSq = depthDelta * depthDelta;
                if (depthDeltaSq >= radiusMetersSq)
                    continue;

                float rangeWeight = 1.0 - saturate(depthDeltaSq * invRadiusMetersSq);
                float horizonWeight = saturate(depthDelta * invRadiusMeters - _HectonAbyssalSsdoBias);
                float directionalWeight = saturate(0.7 + dot(rotatedDirection, screenBias));
                float depthWeight = rcp(1.0 + depthDelta * _HectonAbyssalSsdoDepthSigma * 0.01);
                accumulated += horizonWeight * directionalWeight * rangeWeight * depthWeight;
            }

            float normalizedOcclusion = accumulated * 0.25;
            return saturate(1.0 - normalizedOcclusion * _HectonAbyssalSsdoIntensity);
        }

        half BlurOcclusion(float2 screenUV, float2 axis)
        {
            float centerRawDepth;
            float centerDepthValid;
            float centerLinearEyeDepth;
            SampleSceneLinearDepth(screenUV, centerRawDepth, centerDepthValid, centerLinearEyeDepth);

            float centerOcclusion = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV).r;
            if (centerDepthValid <= 0.5)
                return centerOcclusion;
            float accumulated = centerOcclusion;
            float totalWeight = 1.0;
            float2 texelOffset = axis * _BlitTexture_TexelSize.xy;

            [unroll]
            for (int tapIndex = 1; tapIndex <= 2; tapIndex++)
            {
                float2 offset = texelOffset * tapIndex;
                float2 uvA = saturate(screenUV + offset);
                float2 uvB = saturate(screenUV - offset);

                float sampleRawDepthA;
                float sampleDepthValidA;
                float sampleLinearEyeDepthA;
                SampleSceneLinearDepth(uvA, sampleRawDepthA, sampleDepthValidA, sampleLinearEyeDepthA);
                float depthDeltaA = abs(sampleLinearEyeDepthA - centerLinearEyeDepth);
                float weightA = sampleDepthValidA > 0.5 && depthDeltaA <= _HectonAbyssalSsdoBlurDepthThreshold ? 1.0 : 0.0;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvA).r * weightA;
                totalWeight += weightA;

                float sampleRawDepthB;
                float sampleDepthValidB;
                float sampleLinearEyeDepthB;
                SampleSceneLinearDepth(uvB, sampleRawDepthB, sampleDepthValidB, sampleLinearEyeDepthB);
                float depthDeltaB = abs(sampleLinearEyeDepthB - centerLinearEyeDepth);
                float weightB = sampleDepthValidB > 0.5 && depthDeltaB <= _HectonAbyssalSsdoBlurDepthThreshold ? 1.0 : 0.0;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).r * weightB;
                totalWeight += weightB;
            }

            return accumulated * SafeRcp(totalWeight);
        }

        half4 FragOcclusion(Varyings input) : SV_Target
        {
            half occlusion = EvaluateDirectionalOcclusion(input.screenUV);
            return half4(occlusion, occlusion, occlusion, 1.0);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            half occlusion = BlurOcclusion(input.screenUV, float2(1.0, 0.0));
            return half4(occlusion, occlusion, occlusion, 1.0);
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            half occlusion = BlurOcclusion(input.screenUV, float2(0.0, 1.0));
            return half4(occlusion, occlusion, occlusion, 1.0);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            float rawDepth;
            float depthValid;
            float linearEyeDepth;
            SampleSceneLinearDepth(input.screenUV, rawDepth, depthValid, linearEyeDepth);
            if (depthValid <= 0.5)
                return sourceColor;

            half occlusion = SAMPLE_TEXTURE2D_X(_HectonAbyssalSSDOTex, sampler_LinearClamp, input.screenUV).r;
            sourceColor.rgb *= lerp(1.0h, occlusion, (half)_HectonAbyssalSsdoCompositeStrength);
            return sourceColor;
        }
        ENDHLSL

        Pass
        {
            Name "Occlusion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOcclusion
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }

    FallBack Off
}

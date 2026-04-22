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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

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
            int _HectonAbyssalSsdoSampleCount;
            float4 _HectonAbyssalSsdoAmbientDirection;
            float _HectonAbyssalSsdoHasBlueNoise;
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonAbyssalSSDOTex);
        TEXTURE2D(_BlueNoiseTex);
        SAMPLER(sampler_BlueNoiseTex);
        float4 _BlitTexture_TexelSize;
        float4 _BlueNoiseTex_TexelSize;

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

        float3 SafeNormalize3(float3 value)
        {
            float lenSq = dot(value, value);
            return lenSq > 0.00001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
        }

        float ResolveInterleavedNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float ResolveBlueNoise(float2 screenUV)
        {
            float fallback = ResolveInterleavedNoise(screenUV);
            float useBlueNoise = step(0.5, _HectonAbyssalSsdoHasBlueNoise) * step(0.0001, _BlueNoiseTex_TexelSize.z);
            if (useBlueNoise <= 0.5)
                return fallback;

            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float2 blueNoiseUV = frac(pixel / 64.0);
            float sampled = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r;
            return lerp(fallback, sampled, useBlueNoise);
        }

        float2 Rotate2D(float2 value, float angle)
        {
            float s;
            float c;
            sincos(angle, s, c);
            return float2(value.x * c - value.y * s, value.x * s + value.y * c);
        }

        float ResolveRawDepthValidity(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float depthValid, out float linearEyeDepth)
        {
            rawDepth = SampleSceneDepth(screenUV);
            depthValid = ResolveRawDepthValidity(rawDepth);
            if (depthValid <= 0.5)
            {
                linearEyeDepth = 0.0;
                return 0.0.xxx;
            }

            float3 positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            return positionWS;
        }

        float3 SampleSceneWorldNormal(float2 screenUV, out float normalValid)
        {
            float3 normalWS = SampleSceneNormals(screenUV);
            float normalLengthSq = dot(normalWS, normalWS);
            normalValid = step(0.01, normalLengthSq);
            return normalValid > 0.5 ? normalWS * rsqrt(max(normalLengthSq, 0.00001)) : float3(0.0, 1.0, 0.0);
        }

        half EvaluateDirectionalOcclusion(float2 screenUV)
        {
            float rawDepth;
            float depthValid;
            float linearEyeDepth;
            float3 positionWS = SampleSceneWorldPosition(screenUV, rawDepth, depthValid, linearEyeDepth);
            if (depthValid <= 0.5)
                return 1.0h;

            float normalValid;
            float3 normalWS = SampleSceneWorldNormal(screenUV, normalValid);
            if (normalValid <= 0.5)
                return 1.0h;

            float radiusMeters = max(_HectonAbyssalSsdoRadiusMeters, 0.05);
            float pixelRadius = _HectonAbyssalSsdoProjectionScale * SafeRcp(max(linearEyeDepth, 0.1));
            float2 uvRadius = pixelRadius * _HectonAbyssalSsdoInputSize.zw;
            float angle = ResolveBlueNoise(screenUV) * 6.2831853;
            float3 ambientDirectionWS = SafeNormalize3(_HectonAbyssalSsdoAmbientDirection.xyz);

            static const float2 kKernel[6] =
            {
                float2(1.0, 0.0),
                float2(-1.0, 0.0),
                float2(0.0, 1.0),
                float2(0.0, -1.0),
                float2(0.7071, 0.7071),
                float2(-0.7071, 0.7071)
            };

            float accumulated = 0.0;
            int sampleCount = clamp(_HectonAbyssalSsdoSampleCount, 4, 6);
            [unroll(6)]
            for (int sampleIndex = 0; sampleIndex < 6; sampleIndex++)
            {
                if (sampleIndex >= sampleCount)
                    break;

                float2 rotatedDirection = Rotate2D(kKernel[sampleIndex], angle);
                float2 sampleUV = saturate(screenUV + rotatedDirection * uvRadius);
                float sampleRawDepth;
                float sampleDepthValid;
                float sampleLinearEyeDepth;
                float3 samplePositionWS = SampleSceneWorldPosition(sampleUV, sampleRawDepth, sampleDepthValid, sampleLinearEyeDepth);
                if (sampleDepthValid <= 0.5)
                    continue;

                float3 deltaWS = samplePositionWS - positionWS;
                float distSq = dot(deltaWS, deltaWS);
                if (distSq <= 0.0001 || distSq >= radiusMeters * radiusMeters)
                    continue;

                float dist = sqrt(distSq);
                float3 sampleDirectionWS = deltaWS * SafeRcp(dist);
                float rangeWeight = 1.0 - saturate(dist * SafeRcp(radiusMeters));
                float horizonWeight = saturate(1.0 - dot(normalWS, sampleDirectionWS) - _HectonAbyssalSsdoBias);
                float directionalWeight = saturate(dot(sampleDirectionWS, ambientDirectionWS));
                float depthWeight = exp2(-abs(sampleLinearEyeDepth - linearEyeDepth) * _HectonAbyssalSsdoDepthSigma * 0.01);
                accumulated += horizonWeight * directionalWeight * rangeWeight * depthWeight;
            }

            float normalizedOcclusion = accumulated * SafeRcp((float)sampleCount);
            return saturate(1.0 - normalizedOcclusion * _HectonAbyssalSsdoIntensity);
        }

        half BlurOcclusion(float2 screenUV, float2 axis)
        {
            float centerRawDepth;
            float centerDepthValid;
            float centerLinearEyeDepth;
            SampleSceneWorldPosition(screenUV, centerRawDepth, centerDepthValid, centerLinearEyeDepth);

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
                SampleSceneWorldPosition(uvA, sampleRawDepthA, sampleDepthValidA, sampleLinearEyeDepthA);
                float depthDeltaA = abs(sampleLinearEyeDepthA - centerLinearEyeDepth);
                float weightA = sampleDepthValidA > 0.5 && depthDeltaA <= _HectonAbyssalSsdoBlurDepthThreshold ? 1.0 : 0.0;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvA).r * weightA;
                totalWeight += weightA;

                float sampleRawDepthB;
                float sampleDepthValidB;
                float sampleLinearEyeDepthB;
                SampleSceneWorldPosition(uvB, sampleRawDepthB, sampleDepthValidB, sampleLinearEyeDepthB);
                float depthDeltaB = abs(sampleLinearEyeDepthB - centerLinearEyeDepth);
                float weightB = sampleDepthValidB > 0.5 && depthDeltaB <= _HectonAbyssalSsdoBlurDepthThreshold ? 1.0 : 0.0;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).r * weightB;
                totalWeight += weightB;
            }

            return accumulated * SafeRcp(totalWeight);
        }

        half4 FragOcclusion(Varyings input) : SV_Target
        {
            return half4(EvaluateDirectionalOcclusion(input.screenUV), 0.0, 0.0, 1.0);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            return half4(BlurOcclusion(input.screenUV, float2(1.0, 0.0)), 0.0, 0.0, 1.0);
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            return half4(BlurOcclusion(input.screenUV, float2(0.0, 1.0)), 0.0, 0.0, 1.0);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            float rawDepth;
            float depthValid;
            float linearEyeDepth;
            SampleSceneWorldPosition(input.screenUV, rawDepth, depthValid, linearEyeDepth);
            if (depthValid <= 0.5)
                return sourceColor;

            float normalValid;
            SampleSceneWorldNormal(input.screenUV, normalValid);
            if (normalValid <= 0.5)
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
}

Shader "HECTON/World/AbyssalFluidDecal"
{
    Properties
    {
        [HDR] _TintColor("Tint Color", Color) = (0.22, 0.12, 0.18, 0.72)
        _Radius("Radius", Range(0.1, 12.0)) = 1.0
        _Softness("Softness", Range(0.05, 2.0)) = 0.28
        _WakeDistortion("Wake Distortion", Range(0.0, 1.0)) = 0.22
        _WakeTearStrength("Wake Tear Strength", Range(0.0, 1.0)) = 0.68
        _WakeThreshold("Wake Threshold", Range(0.0, 1.0)) = 0.08
        _DepthFadeDistance("Depth Fade Distance", Range(0.02, 2.0)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest+40"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        ZWrite On
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "AbyssalFluidDecal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Radius;
                half _Softness;
                half _WakeDistortion;
                half _WakeTearStrength;
                half _WakeThreshold;
                half _DepthFadeDistance;
            CBUFFER_END

            TEXTURE2D(_HectonShallowWaterFieldRT);
            SAMPLER(sampler_HectonShallowWaterFieldRT);
            float4 _HectonShallowWaterFieldWorldRect;
            float _HectonShallowWaterFieldActive;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv * 2.0 - 1.0;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float SampleWakeTrail(float2 worldXZ)
            {
                if (_HectonShallowWaterFieldActive < 0.5)
                    return 0.0;

                float2 uv = float2(
                    (worldXZ.x - _HectonShallowWaterFieldWorldRect.x) * _HectonShallowWaterFieldWorldRect.z,
                    (worldXZ.y - _HectonShallowWaterFieldWorldRect.y) * _HectonShallowWaterFieldWorldRect.w);
                if (any(uv < 0.0) || any(uv > 1.0))
                    return 0.0;

                return SAMPLE_TEXTURE2D(_HectonShallowWaterFieldRT, sampler_HectonShallowWaterFieldRT, uv).b;
            }

            void ClipDitheredAlpha(half alpha, float4 positionCS)
            {
                clip((float)alpha - InterleavedGradientNoise(positionCS.xy));
            }

            half ResolveLinearRamp01(half edge0, half edge1, half value)
            {
                return saturate((value - edge0) * rcp(max(edge1 - edge0, 0.0001h)));
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 worldXZ = input.positionWS.xz;
                float wakeCenter = SampleWakeTrail(worldXZ);
                float wakeOffsetX = SampleWakeTrail(worldXZ + float2(0.8, 0.0)) - SampleWakeTrail(worldXZ + float2(-0.8, 0.0));
                float wakeOffsetZ = SampleWakeTrail(worldXZ + float2(0.0, 0.8)) - SampleWakeTrail(worldXZ + float2(0.0, -0.8));
                float wakeMask = saturate((wakeCenter - _WakeThreshold) * rcp(max(0.001, 1.0 - _WakeThreshold)));
                float2 distortedUv = input.uv + float2(wakeOffsetX, wakeOffsetZ) * (_WakeDistortion * wakeMask);
                half2 radialAbs = abs(half2(distortedUv.x, distortedUv.y));
                half radial = max(radialAbs.x, radialAbs.y) + min(radialAbs.x, radialAbs.y) * 0.375h;
                half edge = saturate(1.0h - ResolveLinearRamp01(max(0.0h, 1.0h - _Softness), 1.0h, radial));
                half centerBoost = saturate(1.0h - radial * 0.82h);
                half tearMask = saturate(1.0h - wakeMask * _WakeTearStrength);
                float2 screenUV = input.positionCS.xy * rcp(max(_ScaledScreenParams.xy, float2(1.0, 1.0)));
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
                float sceneRawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float fragmentEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half depthFade = saturate((half)((sceneEyeDepth - fragmentEyeDepth) * rcp(max(_DepthFadeDistance, 0.001h))));
                half alpha = saturate(edge * tearMask * depthFade * _TintColor.a * lerp(0.72h, 1.0h, centerBoost));
                half3 color = _TintColor.rgb * lerp(0.86h, 1.08h, centerBoost) * lerp(1.0h, 1.12h, wakeMask * 0.25h);
                ClipDitheredAlpha(alpha, input.positionCS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

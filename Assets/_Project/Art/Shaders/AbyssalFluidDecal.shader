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
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AbyssalFluidDecal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Radius;
                half _Softness;
                half _WakeDistortion;
                half _WakeTearStrength;
                half _WakeThreshold;
            CBUFFER_END

            TEXTURE2D(_HectonVegetationWakeTrailRT);
            SAMPLER(sampler_HectonVegetationWakeTrailRT);
            float4 _HectonVegetationWakeTrailWorldRect;
            float _HectonVegetationWakeTrailActive;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv * 2.0 - 1.0;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float SampleWakeTrail(float2 worldXZ)
            {
                if (_HectonVegetationWakeTrailActive < 0.5)
                    return 0.0;

                float2 uv = float2(
                    (worldXZ.x - _HectonVegetationWakeTrailWorldRect.x) * _HectonVegetationWakeTrailWorldRect.z,
                    (worldXZ.y - _HectonVegetationWakeTrailWorldRect.y) * _HectonVegetationWakeTrailWorldRect.w);
                if (any(uv < 0.0) || any(uv > 1.0))
                    return 0.0;

                return SAMPLE_TEXTURE2D(_HectonVegetationWakeTrailRT, sampler_HectonVegetationWakeTrailRT, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 worldXZ = input.positionWS.xz;
                float wakeCenter = SampleWakeTrail(worldXZ);
                float wakeOffsetX = SampleWakeTrail(worldXZ + float2(0.8, 0.0)) - SampleWakeTrail(worldXZ + float2(-0.8, 0.0));
                float wakeOffsetZ = SampleWakeTrail(worldXZ + float2(0.0, 0.8)) - SampleWakeTrail(worldXZ + float2(0.0, -0.8));
                float wakeMask = saturate((wakeCenter - _WakeThreshold) / max(0.001, 1.0 - _WakeThreshold));
                float2 distortedUv = input.uv + float2(wakeOffsetX, wakeOffsetZ) * (_WakeDistortion * wakeMask);
                half radial = length(distortedUv);
                half edge = saturate(1.0h - smoothstep(max(0.0h, 1.0h - _Softness), 1.0h, radial));
                half centerBoost = saturate(1.0h - radial * 0.82h);
                half tearMask = saturate(1.0h - wakeMask * _WakeTearStrength);
                half alpha = saturate(edge * tearMask * _TintColor.a * lerp(0.72h, 1.0h, centerBoost));
                half3 color = _TintColor.rgb * lerp(0.86h, 1.08h, centerBoost) * lerp(1.0h, 1.12h, wakeMask * 0.25h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Hecton8/Submarine/SonarHoloMapStencil"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.10, 0.92, 0.76, 1)
        _SweepBoost ("Sweep Boost", Range(0, 4)) = 1.1
        _CutoutPhase ("Cutout Phase", Range(0, 1)) = 0.12
        _StencilRef ("Stencil Reference", Float) = 8
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilComp ("Stencil Comparison", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+85"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite On
        ZTest LEqual
        Blend One Zero
        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            Comp [_StencilComp]
            Pass Keep
        }

        Pass
        {
            Name "SonarHoloMapStencil"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _SweepBoost;
                float _CutoutPhase;
            CBUFFER_END

            float4 _HectonSubOsSonarSweep;
            float4 _SubInteriorLightingState;

            float TrianglePulse01(float value)
            {
                return 1.0 - abs(frac(value) * 2.0 - 1.0);
            }

            float ApproxRadialLength(float2 value)
            {
                float2 a = abs(value);
                float majorAxis = max(a.x, a.y);
                float minorAxis = min(a.x, a.y);
                return majorAxis + minorAxis * 0.375;
            }

            float LowPowerFlicker01(float2 positionOS)
            {
                float lowPower = saturate((0.15 - _SubInteriorLightingState.z) * 6.666667);
                float gridNoise = frac(positionOS.x * 19.0 + positionOS.y * 31.0 + _Time.y * 23.0);
                return lerp(1.0, 0.62 + gridNoise * 0.38, lowPower);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionOS = input.positionOS.xyz;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radial = ApproxRadialLength(input.positionOS.xz);
                float sweep = TrianglePulse01(radial * 9.0 - _HectonSubOsSonarSweep.x * 2.5);
                float emergency = saturate(_SubInteriorLightingState.y);
                float gate = max(sweep * saturate(_HectonSubOsSonarSweep.y + 0.18), 0.18);
                clip(gate - _CutoutPhase);
                half3 color = lerp(_BaseColor.rgb, half3(1.0h, 0.12h, 0.08h), (half)emergency);
                color *= (half)(0.72 + gate * _SweepBoost);
                color *= (half)LowPowerFlicker01(input.positionOS.xz);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Hecton8/UI/AcousticRadarVoxel"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.38, 0.98, 0.88, 0.72)
        _PulseIntensity ("Pulse Intensity", Range(0, 4)) = 1.15
        _VoxelDitherDensity ("Voxel Dither Density", Range(2, 32)) = 9
        _ScanlineDensity ("Scanline Density", Range(4, 64)) = 22
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.25
        _StencilRef ("Stencil Reference", Float) = 8
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilComp ("Stencil Comparison", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+80"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                Comp [_StencilComp]
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _PulseIntensity;
                float _VoxelDitherDensity;
                float _ScanlineDensity;
                float _AlphaCutoff;
            CBUFFER_END

            float Hash31(float3 p)
            {
                float3 hash = frac(p * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 cell = floor((input.positionOS + 0.5) * max(2.0, _VoxelDitherDensity));
                float dither = Hash31(cell + floor(_Time.y * 9.0));
                clip(dither - 0.08);

                float scanline = frac((input.positionWS.y * _ScanlineDensity) + (_Time.y * 3.5));
                float scanBase = 1.0 - abs(scanline * 2.0 - 1.0);
                float scanSq = scanBase * scanBase;
                float scanGlow = scanSq * scanSq * scanBase;
                half pulse = (half)(0.72 + 0.28 * (FastTrianglePulse01(_Time.y * 7.0 + input.positionWS.x * 13.0) * 2.0 - 1.0));
                half alpha = saturate(_BaseColor.a * (0.44h + scanGlow * 0.42h + pulse * 0.24h));
                clip(alpha - (half)_AlphaCutoff);
                half3 color = _BaseColor.rgb * (0.75h + scanGlow * (half)_PulseIntensity);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

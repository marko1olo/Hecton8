Shader "Hecton8/UI/AcousticRadarVoxel"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.38, 0.98, 0.88, 0.72)
        _PulseIntensity ("Pulse Intensity", Range(0, 4)) = 1.15
        _VoxelDitherDensity ("Voxel Dither Density", Range(2, 32)) = 9
        _ScanlineDensity ("Scanline Density", Range(4, 64)) = 22
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _PulseIntensity;
                float _VoxelDitherDensity;
                float _ScanlineDensity;
            CBUFFER_END

            float Hash31(float3 p)
            {
                return frac(sin(dot(p, float3(17.13, 61.71, 113.37))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 cell = floor((input.positionOS + 0.5) * max(2.0, _VoxelDitherDensity));
                float dither = Hash31(cell + floor(_Time.y * 9.0));
                clip(dither - 0.08);

                float scanline = frac((input.positionWS.y * _ScanlineDensity) + (_Time.y * 3.5));
                float scanGlow = pow(1.0 - abs(scanline * 2.0 - 1.0), 5.0);
                half pulse = (half)(0.72 + 0.28 * sin(_Time.y * 7.0 + input.positionWS.x * 13.0));
                half alpha = saturate(_BaseColor.a * (0.44h + scanGlow * 0.42h + pulse * 0.24h));
                half3 color = _BaseColor.rgb * (0.75h + scanGlow * (half)_PulseIntensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

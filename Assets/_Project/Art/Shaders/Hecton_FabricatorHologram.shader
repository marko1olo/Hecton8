Shader "HECTON/UI/FabricatorHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.88, 1.0, 0.42)
        _CraftProgress ("Craft Progress", Range(0, 1)) = 0
        _ScanProgress ("Scan Progress", Range(0, 1)) = 0
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0
        _ScanlineDensity ("Scanline Density", Range(1, 64)) = 18
        _ScanlineSpeed ("Scanline Speed", Range(0, 16)) = 4
        _ScanlineEmission ("Scanline Emission", Range(0, 4)) = 1.15
        _VoxelDensity ("Voxel Fragment Density", Range(2, 32)) = 11
        _VoxelDitherStrength ("Voxel Dither Strength", Range(0, 0.45)) = 0.22
        _VoxelEdgeEmission ("Voxel Edge Emission", Range(0, 3)) = 0.85
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
            Cull Back

            HLSLPROGRAM
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _CraftProgress;
                float _ScanProgress;
                float _GlitchAmount;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineEmission;
                float _VoxelDensity;
                float _VoxelDitherStrength;
                float _VoxelEdgeEmission;
            CBUFFER_END

            float HectonVoxelHash31(float3 p)
            {
                return frac(sin(dot(p, float3(17.13, 61.71, 113.37))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float glitchSlice = step(0.58, frac((positionOS.y * 2.7) + (_Time.y * 1.6)));
                float glitchWave = sin((_Time.y * 22.0) + (positionOS.y * 34.0) + (positionOS.z * 19.0));
                float glitchOffsetX = glitchWave * 0.028 * _GlitchAmount * glitchSlice;
                float glitchOffsetZ = cos((_Time.y * 17.0) + (positionOS.x * 21.0)) * 0.014 * _GlitchAmount;
                positionOS.x += glitchOffsetX;
                positionOS.z += glitchOffsetZ;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float revealBand = saturate((input.positionOS.y * 0.5) + 0.5);
                float craftProgress = saturate(max(_CraftProgress, _ScanProgress));
                float reveal = saturate((craftProgress * 1.2) - revealBand + 0.18);
                float3 voxelCell = floor((input.positionOS + 0.5) * max(2.0, _VoxelDensity));
                float voxelNoise = HectonVoxelHash31(voxelCell);
                float voxelGate = saturate((craftProgress * 1.35) - (voxelNoise * _VoxelDitherStrength));
                clip(min(reveal - 0.02, voxelGate - 0.01));

                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDirection)), 2.4);
                float pulse = 0.7 + 0.3 * sin(_Time.y * 6.0 + input.positionWS.y * 8.0);
                float scanline = frac(input.positionOS.y * _ScanlineDensity + _Time.y * _ScanlineSpeed);
                float scanlineBand = 1.0 - abs(scanline * 2.0 - 1.0);
                float scanlineGlow = pow(saturate(scanlineBand), 6.0) * _ScanlineEmission;
                float revealEdge = saturate(1.0 - abs(reveal - 0.08) * 12.0);
                float voxelEdge = saturate(1.0 - abs(voxelGate - 0.04) * 18.0) * _VoxelEdgeEmission;
                half alpha = saturate((_BaseColor.a + fresnel * 0.45 + scanlineGlow * 0.18 + revealEdge * 0.24 + voxelEdge * 0.16) * pulse * (0.35 + reveal * 0.65));
                half3 color = (_BaseColor.rgb * (0.85 + fresnel * 0.75)) + (_BaseColor.rgb * scanlineGlow) + (_BaseColor.rgb * (revealEdge + voxelEdge) * (1.2 + (_GlitchAmount * 0.4)));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

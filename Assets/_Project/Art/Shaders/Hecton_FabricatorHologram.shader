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
        _HologramBobAmplitude ("Hologram Bob Amplitude", Range(0, 0.2)) = 0.035
        _HologramBobFrequency ("Hologram Bob Frequency", Range(0, 8)) = 1.4
        _HologramSwayAmplitude ("Hologram Sway Amplitude", Range(0, 0.2)) = 0.035
        _HologramSwayFrequency ("Hologram Sway Frequency", Range(0, 8)) = 0.9
        _HologramPulseAmplitude ("Hologram Pulse Amplitude", Range(0, 1)) = 0.16
        _HologramPulseFrequency ("Hologram Pulse Frequency", Range(0, 8)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend Off
            ZWrite On
            AlphaToMask On
            Cull Off

            HLSLPROGRAM
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
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
                float _HologramBobAmplitude;
                float _HologramBobFrequency;
                float _HologramSwayAmplitude;
                float _HologramSwayFrequency;
                float _HologramPulseAmplitude;
                float _HologramPulseFrequency;
            CBUFFER_END

            float HectonVoxelHash31(float3 p)
            {
                float3 hash = frac(p * float3(0.1031, 0.11369, 0.13787));
                hash += dot(hash, hash.yzx + 31.31);
                return frac((hash.x + hash.y) * hash.z);
            }

            float HectonTemporalFlicker01(float timeSeconds, float speed, float phaseOffset)
            {
                float hash = frac((timeSeconds * max(speed, 0.001) + phaseOffset) * 0.1031);
                hash *= hash + 33.33;
                hash *= hash + hash;
                return frac(hash);
            }

            float HectonFastTriangleSine01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float HectonFastTriangleSine(float phase)
            {
                return HectonFastTriangleSine01(phase) * 2.0 - 1.0;
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS.xyz;
                float glitchSlice = step(0.58, HectonTemporalFlicker01(_Time.y, 19.0, positionOS.y * 2.7 + positionOS.x * 0.13));
                float glitchWave = HectonFastTriangleSine((_Time.y * 22.0) + (positionOS.y * 34.0) + (positionOS.z * 19.0));
                float glitchOffsetX = glitchWave * 0.028 * _GlitchAmount * glitchSlice;
                float glitchOffsetZ = HectonFastTriangleSine((_Time.y * 17.0) + (positionOS.x * 21.0) + 1.5707963) * 0.014 * _GlitchAmount;
                positionOS.x += glitchOffsetX;
                positionOS.z += glitchOffsetZ;

                float3 objectOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float phaseSeed = dot(objectOriginWS.xz, float2(6.173, 11.317));
                float bobPhase = (_Time.y * max(0.0, _HologramBobFrequency) * 6.2831853) + phaseSeed;
                float swayPhase = (_Time.y * max(0.0, _HologramSwayFrequency) * 6.2831853) + (phaseSeed * 1.37);
                float3 positionWS = TransformObjectToWorld(positionOS);
                positionWS.y += HectonFastTriangleSine(bobPhase) * _HologramBobAmplitude;
                positionWS.xz += float2(
                    HectonFastTriangleSine(swayPhase),
                    HectonFastTriangleSine(swayPhase * 0.83 + 1.5707963)) * _HologramSwayAmplitude;
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float revealBand = saturate((input.positionOS.y * 0.5) + 0.5);
                float craftProgress = saturate(max(_CraftProgress, _ScanProgress));
                float reveal = saturate((craftProgress * 1.2) - revealBand + 0.18);
                float3 voxelCell = floor((input.positionOS + 0.5) * max(2.0, _VoxelDensity));
                float voxelNoise = HectonVoxelHash31(voxelCell);
                float voxelGate = saturate((craftProgress * 1.35) - (voxelNoise * _VoxelDitherStrength));
                clip(min(reveal - 0.02, voxelGate - 0.01));

                float3 viewVector = _WorldSpaceCameraPos - input.positionWS;
                float3 viewDirection = viewVector * rsqrt(max(dot(viewVector, viewVector), 1e-5));
                float3 normalDirection = input.normalWS * rsqrt(max(dot(input.normalWS, input.normalWS), 1e-5));
                float fresnelBase = 1.0 - saturate(dot(normalDirection, viewDirection));
                float fresnelSq = fresnelBase * fresnelBase;
                float fresnel = fresnelSq * (0.76 + 0.24 * fresnelBase);
                float pulse = 0.7 + (_HologramPulseAmplitude * 0.3 * HectonFastTriangleSine(_Time.y * max(0.0, _HologramPulseFrequency) * 6.2831853 + input.positionWS.y * 8.0));
                float scanline = frac(input.positionOS.y * _ScanlineDensity + _Time.y * _ScanlineSpeed);
                float scanlineBand = 1.0 - abs(scanline * 2.0 - 1.0);
                float scanlineMask = saturate(scanlineBand);
                float scanlineSq = scanlineMask * scanlineMask;
                float scanlineGlow = scanlineSq * scanlineSq * scanlineSq * _ScanlineEmission;
                float revealEdge = saturate(1.0 - abs(reveal - 0.08) * 12.0);
                float voxelEdge = saturate(1.0 - abs(voxelGate - 0.04) * 18.0) * _VoxelEdgeEmission;
                half alpha = saturate((_BaseColor.a + fresnel * 0.45 + scanlineGlow * 0.18 + revealEdge * 0.24 + voxelEdge * 0.16) * pulse * (0.35 + reveal * 0.65));
                clip(alpha - (half)HectonDitherCoverage(input.positionCS.xy));
                half3 color = (_BaseColor.rgb * (0.85 + fresnel * 0.75)) + (_BaseColor.rgb * scanlineGlow) + (_BaseColor.rgb * (revealEdge + voxelEdge) * (1.2 + (_GlitchAmount * 0.4)));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

Shader "Hecton8/Fabrication/HologramAssembly"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.05, 0.86, 1.0, 0.72)
        _PausedColor("Paused Color", Color) = (1.0, 0.04, 0.02, 0.86)
        _AssemblyHeightY("Assembly Height Y", Float) = 0
        _AssemblyBaseY("Assembly Base Y", Float) = 0
        _AssemblyTopY("Assembly Top Y", Float) = 1
        _AssemblyEdgeWidth("Assembly Edge Width", Range(0.005, 0.15)) = 0.05
        _AssemblyQuality("Assembly Quality", Range(0, 1)) = 1
        _PowerPause01("Power Pause", Range(0, 1)) = 0
        _WireDensity("Wire Density", Range(2, 96)) = 24
        _WireStrength("Wire Strength", Range(0, 4)) = 1.2
        _FresnelPower("Fresnel Power", Range(0.5, 6)) = 2.2
        _Alpha("Alpha", Range(0, 1)) = 0.72
        _PulseSpeed("Pulse Speed", Range(0, 16)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "AssemblyForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite On
            AlphaToMask On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PausedColor;
                float _AssemblyHeightY;
                float _AssemblyBaseY;
                float _AssemblyTopY;
                float _AssemblyEdgeWidth;
                float _AssemblyQuality;
                float _PowerPause01;
                float _WireDensity;
                float _WireStrength;
                float _FresnelPower;
                float _Alpha;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float height01 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float bottomY = _AssemblyBaseY;
                float topY = max(bottomY + 0.001, _AssemblyTopY);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height01 = saturate((input.positionOS.y - bottomY) / (topY - bottomY));
                return output;
            }

            float HectonAssemblyGridLine(float2 localXZ, float density)
            {
                float2 grid = abs(frac(localXZ * max(2.0, density)) - 0.5);
                float line = max(grid.x, grid.y);
                return 1.0 - smoothstep(0.44, 0.5, line);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float localY = input.positionOS.y;
                clip(_AssemblyHeightY - localY);

                float edgeMask = 0.0;
                if (_AssemblyQuality > 0.5)
                {
                    float edgeWidth = max(0.001, _AssemblyEdgeWidth);
                    edgeMask = 1.0 - smoothstep(0.0, edgeWidth, abs(localY - _AssemblyHeightY));
                }

                float3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
                float3 viewDirWS = HectonCoreLitSafeNormalize(GetCameraPositionWS() - input.positionWS);
                float fresnelBase = 1.0 - saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(fresnelBase, max(0.5, _FresnelPower));
                float wire = HectonAssemblyGridLine(input.positionOS.xz, _WireDensity);
                float pausePulse = saturate(_PowerPause01) *
                    (0.55 + 0.45 * HectonCoreLitTrianglePulse01(_Time.y * max(0.001, _PulseSpeed) + input.height01 * 7.0));

                half3 baseColor = lerp(_BaseColor.rgb, _PausedColor.rgb, (half)pausePulse);
                half alpha = (half)saturate(_Alpha * (_BaseColor.a + fresnel * 0.42 + wire * _WireStrength * 0.16 + edgeMask * 0.9));
                half dither = (half)HectonCoreLitTaaAccumulatedInterleavedGradientNoise(floor(input.positionCS.xy));
                clip(alpha - dither);

                half3 edgeColor = half3(0.72h, 0.95h, 1.0h) * (half)(edgeMask * 2.6);
                half3 color = baseColor * (half)(0.42 + fresnel * 1.1 + wire * _WireStrength * 0.55) + edgeColor;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

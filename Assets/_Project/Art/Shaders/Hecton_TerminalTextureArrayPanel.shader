Shader "HECTON/UI/Terminal TextureArray Panel"
{
    Properties
    {
        _TerminalTextureArray("Terminal Texture Array", 2DArray) = "" {}
        _TerminalSlice("Terminal Slice", Float) = 0
        _EmissionTint("Emission Tint", Color) = (0.70, 1.0, 0.78, 1.0)
        _TerminalScreenAlbedoAtlas("Baked CRT Albedo Atlas", 2D) = "black" {}
        _TerminalScreenProjectionLut("Baked CRT Projection LUT", 2D) = "black" {}
        _TerminalScreenPackedMrao("Baked CRT Packed MRAO", 2D) = "white" {}
        _TerminalScreenBakedProjectionReady("Baked Projection Ready", Float) = 0
        _TerminalScreenBakedProjectionWeight("Baked Projection Weight", Range(0, 1)) = 1
        _TerminalScreenBurnInWeight("Burn-In Weight", Range(0, 1)) = 0.68
        _TerminalScreenGlassWeight("Glass Mask Weight", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Back
        ZWrite On
        Blend Off

        Pass
        {
            Name "TerminalPanel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ HECTON_TERMINAL_INSTANCED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_TerminalTextureArray);
            SAMPLER(sampler_TerminalTextureArray);
            TEXTURE2D(_TerminalScreenAlbedoAtlas);
            SAMPLER(sampler_TerminalScreenAlbedoAtlas);
            TEXTURE2D(_TerminalScreenProjectionLut);
            SAMPLER(sampler_TerminalScreenProjectionLut);
            TEXTURE2D(_TerminalScreenPackedMrao);
            SAMPLER(sampler_TerminalScreenPackedMrao);

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionTint;
                float _TerminalSlice;
                float _TerminalScreenBakedProjectionReady;
                float _TerminalScreenBakedProjectionWeight;
                float _TerminalScreenBurnInWeight;
                float _TerminalScreenGlassWeight;
            CBUFFER_END
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;
            float _H8GlobalQualityWeight;

            struct TerminalPanelInstanceDTO
            {
                float4x4 LocalToWorld;
                float4 SliceFlags;
            };

            StructuredBuffer<TerminalPanelInstanceDTO> _TerminalPanelInstances;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float slice : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
#if defined(HECTON_TERMINAL_INSTANCED)
                TerminalPanelInstanceDTO instance = _TerminalPanelInstances[input.instanceID];
                float4 world = mul(instance.LocalToWorld, float4(input.positionOS.xyz, 1.0));
                output.positionCS = TransformWorldToHClip(world.xyz);
                output.slice = instance.SliceFlags.x;
#else
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.slice = _TerminalSlice;
#endif
                output.uv = input.uv;
                return output;
            }

            float HectonComfortIgn(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveHectonComfortEyeStableScreenUV(float2 positionCS)
            {
                float2 screenUV = saturate(positionCS * rcp(max(_ScreenParams.xy, float2(1.0, 1.0))));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                float4 stereoScaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                screenUV = (screenUV - stereoScaleOffset.zw) * rcp(max(stereoScaleOffset.xy, float2(0.0001, 0.0001)));
#endif
                return saturate(screenUV);
            }

            float ResolveHectonComfortBlackAmount(float2 screenUV, float2 positionCS)
            {
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float somaticTunnel = saturate(_HectonVRSomaticComfortState.x);
                float vrComfortTunnel = saturate(max(max(_HectonVrComfortSignals.x, _HectonVrComfortMotion.z) * vrComfortEnabled, max(_HectonTunnelingIntensity, somaticTunnel)));
                float vrComfortBlackout = saturate(max(_HectonVrComfortSignals.y * vrComfortEnabled, _HectonVRBrownoutIntensity));
                float2 radial = screenUV * 2.0 - 1.0;
                radial.x *= _ScreenParams.x * rcp(max(_ScreenParams.y, 1.0));
                float radialMagnitudeSq = saturate(dot(radial, radial));
                float tunnelInner = lerp(0.74, 0.34, vrComfortTunnel);
                float tunnelInnerSq = tunnelInner * tunnelInner;
                float tunnelMask = saturate((radialMagnitudeSq - tunnelInnerSq) * rcp(max(1.0 - tunnelInnerSq, 0.0009765625))) * vrComfortTunnel;
                float ign = HectonComfortIgn(floor(positionCS));
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float ready = step(0.5, _TerminalScreenBakedProjectionReady);
                half3 color = half3(0.0h, 0.0h, 0.0h);
                UNITY_BRANCH
                if (ready > 0.5)
                {
                    half4 projectionPacked = SAMPLE_TEXTURE2D(_TerminalScreenProjectionLut, sampler_TerminalScreenProjectionLut, input.uv);
                    float2 projectedUv = saturate(projectionPacked.rg);
                    float projectionWeight = saturate(_TerminalScreenBakedProjectionWeight);
                    float2 terminalUv = lerp(input.uv, projectedUv, projectionWeight);
                    float scanMask = projectionPacked.a;
                    float glitchFrame = floor(frac(_Time.y * 0.25) * 64.0) * 0.015625;
                    float glitchBand = 1.0 - abs(frac(glitchFrame + input.uv.y * 7.0) * 2.0 - 1.0);
                    terminalUv.x = saturate(terminalUv.x + (scanMask - 0.5) * glitchBand * projectionWeight * 0.006);
                    half4 sampleColor = SAMPLE_TEXTURE2D_ARRAY(_TerminalTextureArray, sampler_TerminalTextureArray, terminalUv, input.slice);
                    half4 bakedAlbedo = SAMPLE_TEXTURE2D(_TerminalScreenAlbedoAtlas, sampler_TerminalScreenAlbedoAtlas, terminalUv);
                    half4 packedMrao = SAMPLE_TEXTURE2D(_TerminalScreenPackedMrao, sampler_TerminalScreenPackedMrao, input.uv);
                    color = sampleColor.rgb * _EmissionTint.rgb;
                    color = lerp(color, max(color, bakedAlbedo.rgb * _EmissionTint.rgb), bakedAlbedo.a * 0.32h);
                    half burnIn = (half)(projectionPacked.b * saturate(_TerminalScreenBurnInWeight));
                    half scanNoise = (half)scanMask;
                    half glassWeight = (half)saturate(_TerminalScreenGlassWeight);
                    half ao = lerp(1.0h, packedMrao.b, glassWeight);
                    half roughScratch = (1.0h - packedMrao.g) * glassWeight;
                    color *= lerp(1.0h, ao, 0.72h);
                    color += _EmissionTint.rgb * (burnIn * 0.22h + packedMrao.a * burnIn * 0.18h);
                    color = lerp(color, color * (0.90h + scanNoise * 0.16h), 0.75h);
                    color += roughScratch * half3(0.018h, 0.024h, 0.022h);
                }
                else
                {
                    half4 sampleColor = SAMPLE_TEXTURE2D_ARRAY(_TerminalTextureArray, sampler_TerminalTextureArray, input.uv, input.slice);
                    color = sampleColor.rgb * _EmissionTint.rgb;
                }
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                color = lerp(color, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}

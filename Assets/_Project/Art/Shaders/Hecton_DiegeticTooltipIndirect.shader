Shader "Hecton8/UI/DiegeticTooltipIndirect"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _GradientScale ("Gradient Scale", Float) = 8
        _FaceDilate ("Face Dilate", Float) = 0
        _DitherEnabled ("Dither Enabled", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        ZWrite On
        Cull Off
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "TooltipGlyphIndirect"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _GradientScale;
                float _FaceDilate;
                float _DitherEnabled;
            CBUFFER_END

            struct TooltipGlyphInstance
            {
                float4x4 LocalToWorld;
                float4 Tint;
                float4 GlyphIndex;
            };

            StructuredBuffer<TooltipGlyphInstance> _TooltipInstances;
            StructuredBuffer<float4> _TooltipUvRects;
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;


            float _H8GlobalQualityWeight;
            static const float HectonBayer4x4[16] =
            {
                0.0000, 0.5000, 0.1250, 0.6250,
                0.7500, 0.2500, 0.8750, 0.3750,
                0.1875, 0.6875, 0.0625, 0.5625,
                0.9375, 0.4375, 0.8125, 0.3125
            };

            float HectonDitherCoverage(float2 positionCS)
            {
                uint2 pixel = (uint2)positionCS;
                uint index = (pixel.x & 3u) | ((pixel.y & 3u) << 2);
                return HectonBayer4x4[index];
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

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 tint : COLOR0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                TooltipGlyphInstance instance = _TooltipInstances[input.instanceID];
                uint glyphIndex = min((uint)max(0.0, instance.GlyphIndex.x), 127u);
                float4 uvRect = _TooltipUvRects[glyphIndex];
                float4 world = mul(instance.LocalToWorld, float4(input.positionOS, 1.0));
                output.positionCS = TransformWorldToHClip(world.xyz);
                output.uv = lerp(uvRect.xy, uvRect.zw, input.uv);
                output.tint = instance.Tint;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float sdf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float alpha = saturate((sdf - 0.5 + _FaceDilate) * _GradientScale + 0.5);
                float coverage = input.tint.a * alpha;
                float threshold = 0.01;
                [branch]
                if (_DitherEnabled > 0.5)
                    threshold = HectonDitherCoverage(input.positionCS.xy);

                clip(coverage - threshold);
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                half3 color = lerp(input.tint.rgb, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Hecton8/UI/PDA Sonar Point Cloud"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 2.5
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _DepthFadeMeters ("Depth Fade Meters", Float) = 0.08
        _DeepColor ("Deep Color", Color) = (0.02, 0.12, 0.34, 1)
        _HighColor ("High Color", Color) = (1.0, 0.32, 0.06, 1)
        _PredatorColor ("Predator Color", Color) = (1.0, 0.05, 0.02, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "PDASonarPointCloud"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            StructuredBuffer<float4> _SonarPoints;

            CBUFFER_START(UnityPerMaterial)
                float4x4 _PointCloudLocalToWorld;
                float4 _AcousticPingSignal;
                float4 _DeepColor;
                float4 _HighColor;
                float4 _PredatorColor;
                float _PointSize;
                float _Opacity;
                float _DepthFadeMeters;
                float _HeightColorization;
                float _ActiveSonarRadius;
                float _ActiveSonarMaxRange;
            CBUFFER_END
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;
            float _H8GlobalQualityWeight;

            struct Attributes
            {
                float3 positionOS : POSITION;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 color : COLOR0;
                float clipAlpha : TEXCOORD1;
            };

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
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
                float ign = HectonDitherCoverage(positionCS);
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 0.000001f ? value * rsqrt(lengthSq) : fallback;
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            Varyings vert(Attributes input)
            {
                float4 pointData = _SonarPoints[input.instanceId];
                float3 localCenter = pointData.xyz;
                float3 signalNoise = frac((float(input.instanceId) + float3(17.0f, 59.0f, 113.0f)) * 0.1031f + _AcousticPingSignal.z * 0.071f);
                localCenter += (signalNoise - 0.5f) * 0.006f;
                bool predator = pointData.w < 0.0f;
                float intensity = saturate(abs(pointData.w));

                float3 worldCenter = mul(_PointCloudLocalToWorld, float4(localCenter, 1.0f)).xyz;
                float3 cameraRight = SafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0f, 0.0f, 0.0f));
                float3 cameraUp = SafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0f, 1.0f, 0.0f));
                float quadScale = max(_PointSize, 0.25f) * 0.0015f * lerp(1.0f, 1.65f, predator ? 1.0f : 0.0f);
                float3 worldPosition = worldCenter + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * quadScale;

                float localDistance = max(max(abs(localCenter.x), abs(localCenter.y)), abs(localCenter.z));
                float activeRadius01 = saturate(_ActiveSonarRadius * rcp(max(_ActiveSonarMaxRange, 0.001f)));
                float pingRadius = _AcousticPingSignal.w > 0.5f ? activeRadius01 : saturate(_AcousticPingSignal.x);
                float pingWidth = max(_AcousticPingSignal.y, 0.001f);
                float insidePing = _AcousticPingSignal.w > 0.5f ? step(localDistance, pingRadius) : 1.0f;
                float pingBand = 1.0f - saturate(abs(localDistance - pingRadius) * rcp(pingWidth));
                float pingBoost = 0.55f + pingBand * 0.45f;
                float sweepX = lerp(-0.52f, 0.52f, frac(_Time.y * 0.18f));
                float sweepLine = 1.0f - saturate(abs(localCenter.x - sweepX) * 38.0f);

                float height01 = saturate(localCenter.z * 2.0f + 0.5f);
                float3 heightColor = lerp(_DeepColor.rgb, _HighColor.rgb, height01);
                float3 defaultColor = float3(0.18f, 0.95f, 1.0f);
                float3 sonarColor = lerp(defaultColor, heightColor, saturate(_HeightColorization));
                sonarColor = predator ? _PredatorColor.rgb : sonarColor;
                sonarColor = saturate(sonarColor * (1.0f + sweepLine * 0.65f));

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = float4(sonarColor, saturate(intensity * _Opacity * insidePing * max(pingBoost, 0.72f + sweepLine * 0.28f)));
                output.clipAlpha = output.color.a;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUv = input.screenPos.xy * rcp(max(input.screenPos.w, 0.0001f));
                float sceneRawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUv));
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float particleEyeDepth = max(input.screenPos.w, 0.0001f);
                float depthFade = saturate((sceneEyeDepth - particleEyeDepth) * rcp(max(_DepthFadeMeters, 0.0001f)));
                float alpha = input.clipAlpha * depthFade;
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.001));
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                half3 color = lerp(input.color.rgb, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

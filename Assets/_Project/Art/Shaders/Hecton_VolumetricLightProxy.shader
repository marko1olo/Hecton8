Shader "Hidden/Hecton8/VolumetricLightProxy"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ProxyComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #ifndef UNITY_PASS_STEREO_INSTANCE_ID
            #define UNITY_PASS_STEREO_INSTANCE_ID(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            #endif

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_X(_HectonVolumetricSourceDepth);

            float4 _HectonVolumetricFullSize;
            float4 _HectonVolumetricMainLightDirection;
            float4 _HectonVolumetricMainLightColor;
            float4 _HectonFlashlightDirectionWS;
            float4 _HectonFlashlightColor;
            float4 _HectonVolumetricScatteringParams;
            float4 _HectonVolumetricMarchParams;
            float4 _HectonVolumetricShadowParams;
            float4 _HectonVolumetricProxyParams;
            float _HectonFlashlightActive;
            float _HectonFlashlightVolumetricOpacity;
            float _HectonFreezeFrameDither;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
#endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
#else
                return screenUV;
#endif
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            float ResolveDepthValidMask(float rawDepth)
            {
                float finiteMask = isfinite(rawDepth) ? 1.0 : 0.0;
#if defined(UNITY_REVERSED_Z)
                return finiteMask * step(0.0001, rawDepth);
#else
                return finiteMask * step(rawDepth, 0.9999);
#endif
            }

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float2 SafeNormalize2(float2 value)
            {
                float lenSq = dot(value, value);
                return lenSq > 1e-5 ? value * rsqrt(lenSq) : float2(0.23, -0.97);
            }

            half3 SafeHalf3(half3 value)
            {
                return all(isfinite((float3)value)) ? value : half3(0.0h, 0.0h, 0.0h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                UNITY_PASS_STEREO_INSTANCE_ID(input);

                float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
                float2 sourceUV = ResolveFoveatedSourceUV(screenUV);
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV);
                float rawDepth = SAMPLE_TEXTURE2D_X(_HectonVolumetricSourceDepth, sampler_PointClamp, sourceUV).r;
                float validDepth = ResolveDepthValidMask(rawDepth);
                float eyeDepth = validDepth > 0.5 ? LinearEyeDepth(rawDepth, _ZBufferParams) : 0.0;
                float maxDistance = max(_HectonVolumetricMarchParams.x, 0.1);
                float depth01 = saturate(eyeDepth / maxDistance);

                float quality01 = saturate(_HectonVolumetricProxyParams.x);
                float density = max(_HectonVolumetricScatteringParams.x, 0.0) *
                    max(_HectonVolumetricScatteringParams.y, 0.0) *
                    max(_HectonVolumetricScatteringParams.w, 0.0);
                float2 shaftDirection = SafeNormalize2(_HectonVolumetricMainLightDirection.xy +
                    _HectonFlashlightDirectionWS.xy * saturate(_HectonFlashlightActive));
                float stripe = FastTrianglePulse01(dot(screenUV, shaftDirection) * 42.0 + _Time.y * 0.31 + _HectonFreezeFrameDither * 0.13);
                float2 centeredUv = screenUV - 0.5;
                float screenFalloff = saturate(1.35 - dot(centeredUv, centeredUv) * 3.61);
                float fogDepth = saturate(depth01 * 1.85) * validDepth;
                float proxyStrength = density * lerp(0.12, 0.52, quality01) * (0.28 + stripe * stripe * 0.72);

                half3 mainLight = SafeHalf3((half3)_HectonVolumetricMainLightColor.rgb) *
                    (half)saturate(_HectonVolumetricMainLightDirection.w);
                half3 flashlight = SafeHalf3((half3)_HectonFlashlightColor.rgb) *
                    (half)(saturate(_HectonFlashlightActive) * saturate(_HectonFlashlightVolumetricOpacity) * saturate(_HectonFlashlightColor.w));
                half3 shaftColor = mainLight * 0.08h + flashlight * 0.18h;
                half shaftMask = (half)saturate(proxyStrength * fogDepth * screenFalloff);
                half3 composed = SafeHalf3(sourceColor.rgb) + shaftColor * shaftMask;
                return half4(composed, sourceColor.a);
            }
            ENDHLSL
        }
    }
}

Shader "Hidden/Hecton8/VolumetricFogDearLie"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_X(_HectonVolumetricFogSourceColor);
        TEXTURE2D_X(_HectonVolumetricFogSourceDepth);
        TEXTURE2D_X(_HectonVolumetricFogHalfInput);

        CBUFFER_START(HectonVolumetricFogParams)
            float4 _HectonVolumetricFogColorAndDensity;
            float4 _HectonVolumetricFogScatteringParams;
            float4 _HectonVolumetricFogFlowAdvection;
            float4 _HectonVolumetricFogQualityAndLimits;
        CBUFFER_END

        CBUFFER_START(_GlobalWaterOptics)
            float4 _H8WaterOpticsAbsorptionCoefficientsRGB;
            float4 _H8WaterOpticsScatteringCoefficientsRGB;
            float4 _H8WaterOpticsDirectionalLightColorAndIntensity;
            float4 _H8WaterOpticsQualityAndDepthLimits;
        CBUFFER_END

        CBUFFER_START(HectonVolumetricFogFrameParams)
            float4 _HectonVolumetricFogFullSize;
            float4 _HectonVolumetricFogHalfSize;
            float4 _HectonVolumetricFogCompositeParams;
            float4 _HectonVolumetricFogDebugParams;
            float4 _HectonMarineSnowFogDensityTexelSize;
            float4 _HectonMarineSnowFogDensityParams;
            float4 _AbyssalFlowCenter;
            float4 _AbyssalFlowSpacing;
            float4 _AbyssalFlowTextureParams;
            float _AbyssalFlowTextureActive;
            float3 _HectonVolumetricFogPad0;
            float4x4 _HectonVolumetricFogInverseViewProjection;
        CBUFFER_END

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

        float2 ResolveStereoUv(float2 uv)
        {
        #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
            return UnityStereoTransformScreenSpaceTex(uv);
        #else
            return uv;
        #endif
        }

        float SafeRcp(float value)
        {
            return abs(value) > 1e-5 ? rcp(value) : 0.0;
        }

        float FastNegativeExp(float value)
        {
            value = max(0.0, value);
            float value2 = value * value;
            return rcp(1.0 + value + 0.48 * value2 + 0.235 * value2 * value);
        }

        float SafeFiniteScalar(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float SafeFiniteSaturate(float value)
        {
            return isfinite(value) ? saturate(value) : 0.0;
        }

        float3 SafeFiniteColor(float3 color, float3 fallbackColor)
        {
            return all(isfinite(color)) ? color : fallbackColor;
        }

        float3 ResolveNoirFloorColor(float3 color)
        {
            float3 floorColor = max(
                SafeFiniteColor(_HectonVolumetricFogColorAndDensity.rgb, float3(0.0015, 0.0023, 0.0031)),
                float3(0.0015, 0.0023, 0.0031));
            return max(SafeFiniteColor(color, floorColor), floorColor);
        }

        float WaterOpticsActive()
        {
            return step(0.001, SafeFiniteScalar(_H8WaterOpticsQualityAndDepthLimits.w, 0.0));
        }

        float WaterOpticsQualityWeight()
        {
            return SafeFiniteSaturate(_H8WaterOpticsQualityAndDepthLimits.x);
        }

        float WaterOpticsCameraUnderwaterGate()
        {
            return step(0.001, SafeFiniteScalar(_H8WaterOpticsQualityAndDepthLimits.y, 0.0));
        }

        float3 WaterOpticsExtinction()
        {
            float multiplier = max(0.0, SafeFiniteScalar(_H8WaterOpticsAbsorptionCoefficientsRGB.w, 1.0));
            float3 absorption = max(SafeFiniteColor(_H8WaterOpticsAbsorptionCoefficientsRGB.xyz, float3(0.42, 0.105, 0.028)), float3(0.0, 0.0, 0.0)) * multiplier;
            float3 scattering = max(SafeFiniteColor(_H8WaterOpticsScatteringCoefficientsRGB.xyz, float3(0.035, 0.09, 0.16)), float3(0.0, 0.0, 0.0));
            return absorption + scattering;
        }

        float WaterOpticsWaterlineWeight(float2 uv, float rayDistance)
        {
            float quality = WaterOpticsQualityWeight();
            float maxDistance = max(1.0, SafeFiniteScalar(_H8WaterOpticsQualityAndDepthLimits.z, 5000.0));
            float localSurfaceY = SafeFiniteScalar(_H8WaterOpticsQualityAndDepthLimits.y, 0.0);
            float waterlineY = saturate(0.5 - localSurfaceY * SafeRcp(max(1.0, min(rayDistance, maxDistance))) * 0.42);
            float width = lerp(0.012, 0.075, quality * quality * (3.0 - 2.0 * quality));
            return WaterOpticsActive() * WaterOpticsCameraUnderwaterGate() * SafeFiniteSaturate(1.0 - abs(uv.y - waterlineY) * SafeRcp(max(width, 0.001)));
        }

        float3 WaterOpticsDearLieTint(float3 fogColor, float rayDistance, float waterlineWeight)
        {
            float3 extinction = WaterOpticsExtinction();
            float scalarExtinction = max(0.000001, dot(extinction, float3(0.299, 0.587, 0.114)));
            float monoTransmittance = FastNegativeExp(scalarExtinction * max(0.0, rayDistance) * lerp(0.025, 0.12, WaterOpticsQualityWeight()));
            float3 opticsLight = max(_H8WaterOpticsDirectionalLightColorAndIntensity.rgb, fogColor);
            float3 tinted = fogColor * monoTransmittance + opticsLight * waterlineWeight * 0.018;
            float cameraUnderwaterGate = WaterOpticsCameraUnderwaterGate();
            float tintGate = WaterOpticsActive() * max(waterlineWeight, cameraUnderwaterGate);
            return ResolveNoirFloorColor(lerp(fogColor, tinted, tintGate * (0.35 + 0.65 * WaterOpticsQualityWeight())));
        }

        float ResolveSafeLinearEyeDepth(float rawDepth)
        {
            float safeRawDepth = isfinite(rawDepth) ? rawDepth : 0.0;
        #if UNITY_REVERSED_Z
            safeRawDepth = safeRawDepth > 0.0001 ? safeRawDepth : 0.0;
        #else
            safeRawDepth = safeRawDepth < 0.9999 ? safeRawDepth : 1.0;
        #endif
            float linearDepth = LinearEyeDepth(safeRawDepth, _ZBufferParams);
            return isfinite(linearDepth) && linearDepth >= 0.0 ? linearDepth : 0.0;
        }

        float ResolveDepthValidMask(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        int Bayer2(int2 p)
        {
            int x = p.x & 1;
            int y = p.y & 1;
            return ((x ^ y) << 1) + y;
        }

        float Bayer4(float2 pixel)
        {
            int2 p = int2((int)floor(pixel.x) & 3, (int)floor(pixel.y) & 3);
            int value = Bayer2(p) * 4 + Bayer2(int2(p.x >> 1, p.y >> 1));
            return ((float)value + 0.5) * (1.0 / 16.0);
        }

        float Hash21(float2 value)
        {
            value = all(isfinite(value)) ? value : float2(0.0, 0.0);
            float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
            hash += dot(hash, hash.yzx + 33.33);
            return frac((hash.x + hash.y) * hash.z);
        }

        float ResolveProxyDither(float2 uv)
        {
            float2 pixel = uv * max(_HectonVolumetricFogFullSize.xy, float2(1.0, 1.0));
            float bayer = Bayer4(pixel);
            float phase = SafeFiniteScalar(_HectonVolumetricFogCompositeParams.w, 0.0);
            float stochastic = Hash21(floor(pixel * 0.25) + phase * 0.071);
            float quality = SafeFiniteSaturate(_HectonVolumetricFogQualityAndLimits.x);
            float stochasticBlend = quality * quality * (3.0 - 2.0 * quality);
            return lerp(bayer, stochastic, stochasticBlend);
        }

        float4 ResolveCompositeWrite(float4 sourceColor, float4 fogAccum)
        {
            fogAccum = all(isfinite(fogAccum)) ? fogAccum : float4(0.0, 0.0, 0.0, 0.0);
            float alpha = SafeFiniteSaturate(fogAccum.a);
            float3 source = SafeFiniteColor(sourceColor.rgb, ResolveNoirFloorColor(_HectonVolumetricFogColorAndDensity.rgb));
            float3 color = source * (1.0 - alpha) + SafeFiniteColor(fogAccum.rgb, float3(0.0, 0.0, 0.0));
            return float4(SafeFiniteColor(color, source), SafeFiniteSaturate(SafeFiniteScalar(sourceColor.a, 1.0)));
        }

        float4 ResolveProxyFog(float2 uv)
        {
            float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogSourceDepth, sampler_PointClamp, uv, 0).r;
            float validMask = ResolveDepthValidMask(rawDepth);
            float maxRayDistance = max(0.25, SafeFiniteScalar(_HectonVolumetricFogQualityAndLimits.z, 70.0));
            float rayDistance = validMask > 0.5 ? min(ResolveSafeLinearEyeDepth(rawDepth), maxRayDistance) : maxRayDistance;
            float density = max(0.0, SafeFiniteScalar(_HectonVolumetricFogColorAndDensity.w, 0.0));
            float extinction = max(0.0001, SafeFiniteScalar(_HectonVolumetricFogScatteringParams.y, 0.12));
            float opacity = SafeFiniteSaturate(1.0 - FastNegativeExp(density * extinction * max(0.0001, rayDistance)));
            float dither = ResolveProxyDither(uv);
            opacity = SafeFiniteSaturate(opacity + (dither - 0.5) * SafeFiniteSaturate(_HectonVolumetricFogDebugParams.y) * 0.08);
            float2 centeredUv = uv * 2.0 - 1.0;
            float shaftFake = SafeFiniteSaturate(1.0 - dot(centeredUv, centeredUv) * 0.65);
            float3 fogColor = ResolveNoirFloorColor(_HectonVolumetricFogColorAndDensity.rgb);
            float waterline = WaterOpticsWaterlineWeight(uv, rayDistance);
            fogColor = WaterOpticsDearLieTint(fogColor, rayDistance, waterline);
            opacity = SafeFiniteSaturate(opacity + waterline * lerp(0.018, 0.07, WaterOpticsQualityWeight()));
            return float4(fogColor * opacity * (0.86 + 0.22 * shaftFake), opacity);
        }

        half4 FragProxy(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = saturate(ResolveStereoUv(input.screenUV));
            float4 source = SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogSourceColor, sampler_LinearClamp, uv, 0);
            float4 fog = ResolveProxyFog(uv);
            return (half4)ResolveCompositeWrite(source, fog);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = saturate(ResolveStereoUv(input.screenUV));
            float4 source = SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogSourceColor, sampler_LinearClamp, uv, 0);
            float centerDepth = SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogSourceDepth, sampler_PointClamp, uv, 0).r;
            float centerEyeDepth = ResolveSafeLinearEyeDepth(centerDepth);
            float depthScale = max(0.01, SafeFiniteScalar(_HectonVolumetricFogCompositeParams.x, 8.0));
            float4 fogAccum = float4(0.0, 0.0, 0.0, 0.0);
            float weightSum = 0.0;

            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2((float)x, (float)y) * _HectonVolumetricFogHalfSize.zw;
                    float2 sampleUv = saturate(uv + offset);
                    float sampleDepth = SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogSourceDepth, sampler_PointClamp, sampleUv, 0).r;
                    float depthWeight = FastNegativeExp(abs(ResolveSafeLinearEyeDepth(sampleDepth) - centerEyeDepth) * depthScale);
                    float kernelWeight = (x == 0 && y == 0) ? 1.0 : 0.5;
                    float weight = depthWeight * kernelWeight;
                    fogAccum += SAMPLE_TEXTURE2D_X_LOD(_HectonVolumetricFogHalfInput, sampler_LinearClamp, sampleUv, 0) * weight;
                    weightSum += weight;
                }
            }

            fogAccum *= SafeRcp(max(weightSum, 1e-4));
            return (half4)ResolveCompositeWrite(source, fogAccum);
        }
        ENDHLSL

        Pass
        {
            Name "DearLieProxy"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragProxy
            ENDHLSL
        }

        Pass
        {
            Name "BilateralComposite"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
    Fallback Off
}

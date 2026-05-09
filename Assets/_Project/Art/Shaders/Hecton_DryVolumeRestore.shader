Shader "Hidden/Hecton8/DryVolumeRestore"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 64
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _StencilRef;
        CBUFFER_END

        TEXTURE2D_X(_Crest_CameraColorTexture);
        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D(_BlueNoiseTex);
        SAMPLER(sampler_BlueNoiseTex);
        float4 _BlueNoiseTex_TexelSize;

        float4 _HectonNoirResolveSettings;
        float4 _HectonNoirAbyssFloor;
        float4 _HectonNoirFogStratification;
        float4 _HectonNoirDitherParams;
        float4 _HectonFloatingOriginOffset;
        float4 _HectonBiolumVolumeHalfExtents;
        float4 _HectonBiolumVolumeParams;
        float _HectonBiolumVolumeActive;
        float _HectonFreezeFrameDither;
        float4x4 _HectonBiolumVolumeWorldToLocal;

        TEXTURE3D(_HectonBiolumVolumeTex);
        SAMPLER(sampler_HectonBiolumVolumeTex);
        Texture2D<int> _HectonMarineSnowSonarGlowTex;
        float4 _HectonMarineSnowSonarGlowTexelSize;
        float4 _HectonMarineSnowSonarGlowParams;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float ResolveInterleavedNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float ResolveBlueNoise(float2 screenUV)
        {
            float fallback = ResolveInterleavedNoise(screenUV);
            if (_BlueNoiseTex_TexelSize.z < 0.0001)
                return fallback;

            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float textureSize = max(_HectonNoirDitherParams.z, 64.0);
            float2 blueNoiseUV = frac((pixel / textureSize) + _HectonNoirDitherParams.xy);
            float sampled = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r;
            return sampled;
        }

        float ResolveFarRawDepth()
        {
        #if UNITY_REVERSED_Z
            return 0.0;
        #else
            return 1.0;
        #endif
        }

        float ResolveRawDepthValidity(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float depthValid, out float linearEyeDepth)
        {
            rawDepth = SampleSceneDepth(screenUV);
            depthValid = ResolveRawDepthValidity(rawDepth);
            float resolvedRawDepth = depthValid > 0.5 ? rawDepth : ResolveFarRawDepth();
            float3 positionWS = ComputeWorldSpacePosition(screenUV, resolvedRawDepth, UNITY_MATRIX_I_VP);
            linearEyeDepth = LinearEyeDepth(resolvedRawDepth, _ZBufferParams);
            return positionWS;
        }

        half3 ApplyNoirFog(half3 sourceColor, float3 absolutePositionWS, float linearEyeDepth)
        {
            float inverseSpan = max(_HectonNoirFogStratification.y, 0.00001);
            float abyssFloorY = _HectonNoirFogStratification.x - rcp(inverseSpan);
            float worldYNorm = saturate((absolutePositionWS.y - abyssFloorY) * inverseSpan);
            float densityLocal = _HectonNoirFogStratification.w * (1.0 + (1.0 - worldYNorm) * _HectonNoirFogStratification.z);
            half fogFactor = saturate(1.0h - exp2(-linearEyeDepth * densityLocal));
            half fogPower = (half)max(_HectonNoirResolveSettings.x, 1.0);
            half fogSq = fogFactor * fogFactor;
            half fogQuad = fogSq * fogSq;
            half fogLow = lerp(fogFactor, fogSq, saturate(fogPower - 1.0h));
            half fogHigh = lerp(fogSq, fogQuad, saturate((fogPower - 2.0h) * 0.5h));
            fogFactor = fogPower < 2.0h ? fogLow : fogHigh;
            half3 abyssFloor = _HectonNoirAbyssFloor.rgb;
            return lerp(sourceColor, max(abyssFloor, sourceColor * 0.18h), fogFactor);
        }

        float3 SampleBiolumVolumeRadiance(float3 positionWS)
        {
            if (_HectonBiolumVolumeActive <= 0.5)
                return 0.0;

            float3 halfExtents = max(_HectonBiolumVolumeHalfExtents.xyz, float3(0.001, 0.001, 0.001));
            float3 localPosition = mul(_HectonBiolumVolumeWorldToLocal, float4(positionWS, 1.0)).xyz;
            float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
            if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
                return 0.0;

            float4 volumeSample = SAMPLE_TEXTURE3D_LOD(_HectonBiolumVolumeTex, sampler_HectonBiolumVolumeTex, sampleUv, 0);
            return volumeSample.rgb * max(_HectonBiolumVolumeParams.x, 0.0);
        }

        float3 SampleMarineSnowSonarGlow(float2 screenUV)
        {
            if (_HectonMarineSnowSonarGlowParams.y <= 0.0001 || _HectonMarineSnowSonarGlowTexelSize.z < 1.0 || _HectonMarineSnowSonarGlowTexelSize.w < 1.0)
                return 0.0;

            int2 pixel = int2(
                saturate(screenUV.x) * (_HectonMarineSnowSonarGlowTexelSize.z - 1.0) + 0.5,
                saturate(screenUV.y) * (_HectonMarineSnowSonarGlowTexelSize.w - 1.0) + 0.5);
            int rawGlow = _HectonMarineSnowSonarGlowTex.Load(int3(pixel, 0)).r;
            float decodedGlow = saturate(rawGlow / max(_HectonMarineSnowSonarGlowParams.z, 1.0));
            float intensity = decodedGlow * _HectonMarineSnowSonarGlowParams.y;
            return float3(0.12, 0.42, 0.58) * intensity;
        }

        half4 FragRestore(Varyings input) : SV_Target
        {
            return SAMPLE_TEXTURE2D_X(_Crest_CameraColorTexture, sampler_LinearClamp, input.screenUV);
        }

        half4 FragResolve(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            if (_HectonNoirResolveSettings.z < 0.5)
                return sourceColor;

            float rawDepth;
            float depthValid;
            float linearEyeDepth;
            float3 scenePositionWS = SampleSceneWorldPosition(input.screenUV, rawDepth, depthValid, linearEyeDepth);
            half3 resolvedColor = sourceColor.rgb;
            if (depthValid > 0.5)
            {
                float3 absolutePositionWS = scenePositionWS + _HectonFloatingOriginOffset.xyz;
                resolvedColor = ApplyNoirFog(resolvedColor, absolutePositionWS, linearEyeDepth);
                float depthBiolumScale = saturate(1.0 - (linearEyeDepth * 0.0125));
                resolvedColor += (half3)(SampleBiolumVolumeRadiance(absolutePositionWS) * depthBiolumScale * 0.22);
            }

            resolvedColor += (half3)SampleMarineSnowSonarGlow(input.screenUV);

            float noise = ResolveBlueNoise(input.screenUV);
            half freeze = (half)saturate(_HectonFreezeFrameDither);
            half scanline = (half)step(0.5, frac(input.positionCS.y * 0.5));
            half ditherMask = (half)step(noise, freeze);
            half3 frozenTint = resolvedColor * 0.64h + half3(0.010h, 0.055h, 0.075h) * 0.36h;
            frozenTint += (((half)noise - 0.5h) * 0.070h) + (scanline * 0.022h);
            frozenTint *= lerp(1.0h, 0.76h + ditherMask * 0.24h, freeze);
            resolvedColor = lerp(resolvedColor, frozenTint, freeze);

            half dither = (half)(noise - 0.5) * (half)(_HectonNoirResolveSettings.y / 255.0);
            return half4(max(resolvedColor + dither.xxx, 0.0h), sourceColor.a);
        }
        ENDHLSL

        Pass
        {
            Name "Restore"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Stencil
            {
                Ref [_StencilRef]
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma vertex Vert
            #pragma fragment FragRestore
            ENDHLSL
        }

        Pass
        {
            Name "Resolve"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma vertex Vert
            #pragma fragment FragResolve
            ENDHLSL
        }
    }

    FallBack Off
}

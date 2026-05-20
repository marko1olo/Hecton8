Shader "Hecton8/Environment/Hecton_OctahedralImpostor"
{
    Properties
    {
        [MainTexture] _ImpostorAlbedoDepthAtlas ("Albedo/Depth Atlas", 2D) = "white" {}
        _ImpostorNormalDepthAtlas ("Normal XY Atlas", 2D) = "bump" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _AlphaClipThreshold ("Depth Occupancy Threshold", Range(0, 0.05)) = 0.003
        _DepthBias ("Depth Bias", Range(0, 0.01)) = 0.001
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _AmbientFloor ("Ambient Floor", Range(0, 1)) = 0.18
        _HeadlightBoost ("Headlight Boost", Range(0, 4)) = 1.25
        _HectonGlobalQualityWeight ("Global Quality Weight", Range(0, 1)) = 1
        _HectonImpostorDepthScaleMeters ("Depth Scale Meters", Float) = 1
        _HectonImpostorAtlasGrid ("Atlas Grid", Vector) = (4, 4, 0.25, 0.25)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest+12"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "OctahedralImpostor"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AlphaClipThreshold;
                float _DepthBias;
                half _NormalStrength;
                half _AmbientFloor;
                half _HeadlightBoost;
                float _HectonGlobalQualityWeight;
                float _HectonImpostorDepthScaleMeters;
                float4 _HectonImpostorAtlasGrid;
                float _HectonImpostorTimeSeconds;
                float _HectonImpostorFadeOutSeconds;
                int _HectonUseVisibleMatrixStream;
                float _HectonImpostorRuntimePad0;
                float4 _GlobalFloatingOffset;
            CBUFFER_END

            TEXTURE2D(_ImpostorAlbedoDepthAtlas);
            SAMPLER(sampler_ImpostorAlbedoDepthAtlas);
            TEXTURE2D(_ImpostorNormalDepthAtlas);
            SAMPLER(sampler_ImpostorNormalDepthAtlas);

            #include "Assets/_Project/Art/Shaders/Hecton_Impostor.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 viewDirectionWS : TEXCOORD2;
                half fade01 : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            struct FragmentOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            Varyings Vert(Attributes input)
            {
                HectonImpostorVertexResult result = HectonBuildImpostorVertex(
                    input.positionOS.xy,
                    input.uv,
                    input.instanceID,
                    _GlobalFloatingOffset);

                Varyings output;
                output.positionCS = result.positionCS;
                output.positionWS = result.positionWS;
                output.uv = result.uv;
                output.viewDirectionWS = result.viewDirectionWS;
                output.fade01 = (half)result.fade01;
                output.fogFactor = (half)result.fogFactor;
                return output;
            }

            FragmentOutput Frag(Varyings input)
            {
                uint primaryView;
                uint secondaryView;
                float viewBlend;
                HectonImpostorSelectViews(HectonSafeNormalize(input.viewDirectionWS, float3(0.0, 0.0, 1.0)), primaryView, secondaryView, viewBlend);

                float quality01 = saturate(_HectonGlobalQualityWeight);
                float interpolationGate = smoothstep(0.22, 0.55, quality01);
                float blendWeight = saturate(viewBlend * interpolationGate);
                float2 primaryUv = HectonImpostorAtlasUv(input.uv, primaryView, _HectonImpostorAtlasGrid);

                half4 albedoDepthA = SAMPLE_TEXTURE2D(_ImpostorAlbedoDepthAtlas, sampler_ImpostorAlbedoDepthAtlas, primaryUv);
                half4 normalDepthA = SAMPLE_TEXTURE2D(_ImpostorNormalDepthAtlas, sampler_ImpostorNormalDepthAtlas, primaryUv);
                half4 albedoDepth = albedoDepthA;
                half4 normalDepth = normalDepthA;
                [branch]
                if (interpolationGate > 0.001)
                {
                    float2 secondaryUv = HectonImpostorAtlasUv(input.uv, secondaryView, _HectonImpostorAtlasGrid);
                    half4 albedoDepthB = SAMPLE_TEXTURE2D(_ImpostorAlbedoDepthAtlas, sampler_ImpostorAlbedoDepthAtlas, secondaryUv);
                    half4 normalDepthB = SAMPLE_TEXTURE2D(_ImpostorNormalDepthAtlas, sampler_ImpostorNormalDepthAtlas, secondaryUv);
                    albedoDepth = lerp(albedoDepthA, albedoDepthB, (half)blendWeight);
                    normalDepth = lerp(normalDepthA, normalDepthB, (half)blendWeight);
                }
                half occupancyDepth = albedoDepth.a;
                clip(occupancyDepth - _AlphaClipThreshold);
                if (input.fade01 < 0.999h)
                {
                    float fadeDither = HectonInterleavedGradientNoise(input.positionCS.xy + _HectonImpostorTimeSeconds);
                    clip(input.fade01 - fadeDither);
                }

                float3 normalWS = HectonDecodeImpostorNormal(normalDepth, _NormalStrength);
                half headlight = (half)saturate(dot(normalWS, HectonSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS, float3(0.0, 0.0, 1.0))));
                half lighting = saturate(_AmbientFloor + headlight * _HeadlightBoost);
                half3 color = albedoDepth.rgb * _BaseColor.rgb * lighting;
                color = MixFog(color, input.fogFactor);

                float deviceDepth = saturate(input.positionCS.z);
                float decodedDepthMeters = saturate((float)occupancyDepth) * max(0.01, _HectonImpostorDepthScaleMeters);
                float depthOffset = saturate(decodedDepthMeters * rcp(max(0.01, _HectonImpostorDepthScaleMeters))) * _DepthBias;
            #if UNITY_REVERSED_Z
                deviceDepth = saturate(deviceDepth - depthOffset);
            #else
                deviceDepth = saturate(deviceDepth + depthOffset);
            #endif

                FragmentOutput output;
                output.color = half4(color, 1.0h);
                output.depth = deviceDepth;
                return output;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Hecton/Environment/Storm Ocean Surface"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.018, 0.084, 0.112, 0.78)
        _FoamColor ("Foam Color", Color) = (0.78, 0.9, 0.94, 1.0)
        _ReflectionTint ("Reflection Tint", Color) = (0.10, 0.22, 0.26, 1.0)
        _ReflectionCubemap ("Reflection Cubemap", Cube) = "" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.52
    }

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
            Name "StormOceanForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hecton_OceanSurfaceAtmosphere.hlsl"

            TEXTURECUBE(_ReflectionCubemap);
            SAMPLER(sampler_ReflectionCubemap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                half4 _ReflectionTint;
                half _Smoothness;
                half _ReflectionStrength;
                half2 _StormOceanPad0;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float foam : TEXCOORD2;
                float2 surfaceUv : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float2 cameraLocalXZ = positionWS.xz - _WorldSpaceCameraPos.xz;

                float3 displacement;
                float3 normalWS;
                float foamScalar;
                H8EvaluateOceanSurface(cameraLocalXZ, displacement, normalWS, foamScalar);

                positionWS += displacement;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = H8OceanNormalize3(normalWS, float3(0.0, 1.0, 0.0));
                output.positionWS = positionWS;
                output.foam = saturate(foamScalar);
                output.surfaceUv = cameraLocalXZ;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 stormNormalWS = H8ApplyRainSurfaceDisturbance(input.normalWS, input.surfaceUv);
                half fresnel = (half)pow(saturate(1.0 - stormNormalWS.y), 2.0);
                float screenFoam = H8OceanSampleScreenFoam(input.positionCS);
                half foam = (half)saturate(input.foam + screenFoam);
                half3 baseColor = lerp(_BaseColor.rgb, _FoamColor.rgb, foam);
                float3 viewDirWS = H8OceanNormalize3(_WorldSpaceCameraPos.xyz - input.positionWS, float3(0.0, 1.0, 0.0));
                half quality = (half)H8OceanSafeQuality(_H8OceanFoamAndShadowParams.w);
                float2 foamSlope = float2(ddx(screenFoam), ddy(screenFoam)) * lerp(0.035, 0.18, quality);
                float3 reflectionNormalWS = H8OceanNormalize3(
                    stormNormalWS + float3(foamSlope.x, screenFoam * 0.035, foamSlope.y),
                    float3(0.0, 1.0, 0.0));
                float3 reflectDirWS = reflect(-viewDirWS, reflectionNormalWS);
                half cubemapMip = lerp(5.0, 0.0, quality);
                half3 cubemapReflection = SAMPLE_TEXTURECUBE_LOD(_ReflectionCubemap, sampler_ReflectionCubemap, reflectDirWS, cubemapMip).rgb * _ReflectionTint.rgb;
                half3 skyProxy = lerp(_BaseColor.rgb, _ReflectionTint.rgb, (half)saturate(reflectDirWS.y * 0.5 + 0.5));
                half cubemapWeight = (half)saturate(dot(cubemapReflection, half3(0.25, 0.5, 0.25)) * 8.0);
                half3 reflection = lerp(skyProxy, cubemapReflection, cubemapWeight);
                half reflectionMix = saturate((fresnel * _Smoothness + _ReflectionStrength * 0.25) * lerp(0.35, 1.0, quality));
                baseColor = lerp(baseColor, reflection, reflectionMix * (1.0 - foam * 0.72));
                baseColor += fresnel * _Smoothness * half3(0.05, 0.11, 0.14);
                return half4(baseColor, saturate(_BaseColor.a + foam * 0.18));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Hecton/Environment/Surface Readable Ocean Fallback"
{
    Properties
    {
        _BaseMap ("Long Swell Texture", 2D) = "white" {}
        _BumpMap ("Surface Normal Texture", 2D) = "bump" {}
        _FoamMap ("Interference Foam Texture", 2D) = "gray" {}
        _BaseColor ("Readable Water", Color) = (0.09, 0.30, 0.58, 0.62)
        _DeepColor ("Distance Depth Tint", Color) = (0.025, 0.13, 0.30, 1.0)
        _HorizonColor ("Horizon Reflection", Color) = (0.22, 0.46, 0.72, 1.0)
        _FoamColor ("Soft Foam Glint", Color) = (0.82, 0.96, 1.0, 1.0)
        _WaveScale ("Wave Scale", Vector) = (0.020, 0.034, 0.071, 0.047)
        _WaveSpeed ("Wave Speed", Vector) = (0.018, 0.011, -0.012, 0.017)
        _FoamScaleSpeed ("Foam Scale/Speed", Vector) = (0.045, 0.075, -0.018, 0.012)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.52
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.34
        _DistanceTintStrength ("Distance Tint Strength", Range(0, 1)) = 0.48
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.1
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.46
        _Alpha ("Surface Alpha", Range(0, 1)) = 0.62
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
            Name "SurfaceReadableOceanFallback"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_FoamMap);
            SAMPLER(sampler_FoamMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                float4 _WaveScale;
                float4 _WaveSpeed;
                float4 _FoamScaleSpeed;
                float _NormalStrength;
                float _FoamAmount;
                float _DistanceTintStrength;
                float _FresnelPower;
                float _ReflectionStrength;
                float _Alpha;
                float2 _FallbackPad0;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 oceanUvA : TEXCOORD1;
                float2 oceanUvB : TEXCOORD2;
                float2 foamUv : TEXCOORD3;
            };

            float2 H8SafeWaveScale(float2 value, float2 fallbackValue)
            {
                return max(abs(value), fallbackValue);
            }

            float3 H8DecodeReadableOceanNormal(float4 packedNormal, float strength)
            {
                float2 xy = packedNormal.rg * 2.0 - 1.0;
                xy *= max((float)strength, 0.0);
                float z = sqrt(saturate(1.0 - dot(xy, xy)));
                return normalize(float3(xy.x, z, xy.y));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float2 worldXZ = positionWS.xz;
                float timeSeconds = _Time.y;
                float2 scaleA = H8SafeWaveScale(_WaveScale.xy, float2(0.005, 0.005));
                float2 scaleB = H8SafeWaveScale(_WaveScale.zw, float2(0.005, 0.005));
                output.oceanUvA = worldXZ * scaleA + timeSeconds * _WaveSpeed.xy;
                output.oceanUvB = worldXZ * scaleB + timeSeconds * _WaveSpeed.zw;
                output.foamUv = worldXZ * max(abs(_FoamScaleSpeed.xy), float2(0.005, 0.005)) + timeSeconds * _FoamScaleSpeed.zw;

                float4 swellA = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, output.oceanUvA, 0);
                float4 swellB = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, output.oceanUvB * 0.73 + 0.19, 0);
                float4 foamShape = SAMPLE_TEXTURE2D_LOD(_FoamMap, sampler_FoamMap, output.foamUv, 0);
                float lift = (swellA.r - 0.5) * 0.06 + (swellB.g - 0.5) * 0.035 + (foamShape.r - 0.5) * 0.018;
                positionWS.y += lift;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 normalA = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.oceanUvA * 1.13);
                float4 normalB = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.oceanUvB * 0.67 + 0.31);
                float3 normalWS = normalize(
                    H8DecodeReadableOceanNormal(normalA, _NormalStrength) +
                    H8DecodeReadableOceanNormal(normalB, _NormalStrength * 0.68) +
                    float3(0.0, 1.0, 0.0));

                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half fresnel = (half)pow(saturate(1.0 - dot(normalWS, viewDirWS)), max(_FresnelPower, 0.5));

                float4 swellA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.oceanUvA * 0.47);
                float4 swellB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.oceanUvB * 0.59 + 0.37);
                float4 foamA = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, input.foamUv);
                float4 foamB = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, input.foamUv * 0.43 + 0.21);
                half interferenceFoam = (half)smoothstep(0.52, 0.86, max(foamA.r, foamB.g));
                half swellFoam = (half)smoothstep(0.62, 0.94, max(swellA.b, swellB.g));
                half softFoam = (half)(max(swellFoam * 0.7, interferenceFoam) * saturate(_FoamAmount));
                half horizonGlint = (half)saturate(fresnel * saturate(_ReflectionStrength));

                float cameraDistance = length(_WorldSpaceCameraPos.xz - input.positionWS.xz);
                half distanceTint = (half)(saturate(cameraDistance * 0.0045) * saturate(_DistanceTintStrength));
                half3 waterColor = lerp(_BaseColor.rgb, _DeepColor.rgb, distanceTint);
                waterColor = lerp(waterColor, _HorizonColor.rgb, horizonGlint);
                waterColor += normalWS.yyy * half3(0.015, 0.035, 0.055);
                waterColor = lerp(waterColor, _FoamColor.rgb, softFoam);

                half alpha = saturate(saturate(_Alpha) + fresnel * 0.16 + softFoam * 0.18);
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

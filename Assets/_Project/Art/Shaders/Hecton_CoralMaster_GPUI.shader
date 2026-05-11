Shader "GPUInstancer/Hecton8/Flora/CoralMaster"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _MaskMap ("Mask Map", 2D) = "white" {}

        [Header(Master Grade PBR)]
        [Normal] _DetailNormalMap ("Detail Normal (Micro-Porosity)", 2D) = "bump" {}
        _DetailNormalStrength ("Detail Normal Strength", Range(0, 2)) = 0.55
        _MicroPorosityScale ("Micro-Porosity Scale", Range(0.1, 8)) = 3.2
        _DetailMap ("Detail Map (Overlay)", 2D) = "gray" {}

        [Header(Colors)]
        [MainColor] _BaseColor ("Base Color", Color) = (0.54, 0.32, 0.28, 1)
        _AccentColor ("Accent Color", Color) = (0.82, 0.58, 0.42, 1)
        _RimColor ("Rim Color", Color) = (0.24, 0.68, 0.72, 1)
        _SubsurfaceColor ("Subsurface Color", Color) = (0.94, 0.62, 0.48, 1)
        _BiolumColor ("Biolum Color", Color) = (0.26, 0.95, 0.84, 1)

        [Header(PBR and Lighting)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.34
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.46
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.8
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.28
        _SubsurfaceStrength ("Subsurface Strength", Range(0, 2)) = 0.36
        _VertexTintStrength ("Vertex Tint Strength", Range(0, 2)) = 0.74
        _AgeDarkening ("Age Darkening", Range(0, 1)) = 0.18
        _MoistureBoost ("Moisture Boost", Range(0, 1)) = 0.14
        _DetailStrength ("Detail Strength", Range(0, 2)) = 0.42
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.78
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.75
        _TriplanarScale ("Triplanar Scale", Range(0.05, 4)) = 0.44
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 4.8
        _CurvatureWetnessStrength ("Curvature Wetness Strength", Range(0, 2)) = 0.64
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.22
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.8
        _HeightScale ("Height Scale", Range(0, 0.05)) = 0.03
        _ThicknessStrength ("Thickness Strength", Range(0, 2)) = 0.52
        _SpecularNoiseStrength ("Specular Noise Strength", Range(0, 2)) = 0.38
        _CavityStrength ("Cavity Strength", Range(0, 2)) = 0.58
        _CausticStrength ("Caustic Strength", Range(0, 2)) = 0.18
        _CausticScale ("Caustic Scale", Range(0.1, 8)) = 1.6
        _CausticSpeed ("Caustic Speed", Range(0, 4)) = 0.42
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0
        _BiolumMaskStrength ("Biolum Mask Strength", Range(0, 2)) = 1
        _BiolumPulseAmplitude ("Biolum Pulse Amplitude", Range(0, 1)) = 0.28
        _BiolumPulseFrequency ("Biolum Pulse Frequency", Range(0, 8)) = 0.58

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _QUALITY_MX350 _QUALITY_HIGH
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half4 _RimColor;
                half4 _SubsurfaceColor;
                half4 _BiolumColor;
                half _Smoothness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _SubsurfaceStrength;
                half _VertexTintStrength;
                half _AgeDarkening;
                half _MoistureBoost;
                half _DetailStrength;
                half _NormalStrength;
                half _NormalScale;
                half _DetailNormalStrength;
                half _MicroPorosityScale;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _CurvatureWetnessStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _HeightScale;
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _CavityStrength;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumPulseAmplitude;
                half _BiolumPulseFrequency;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DetailNormalMap);
            SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            half4 _HectonOceanBiolumColor;
            half _HectonOceanBiolumStrength;
            half4 _HectonFloorBiolumColor;
            half _HectonFloorBiolumStrength;
            float _GlobalBiolumPhase;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half2 uv : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 ResolveFloraNormalCheap(half3 value)
            {
                return (half3)HectonCoreLitSafeNormalize((float3)value);
            }

            void ResolveFloraDominantAxisProjection(float3 positionWS, half3 normalWS, out float2 uv, out half dominantAxis)
            {
                half3 absNormal = max(abs(normalWS), half3(0.0001h, 0.0001h, 0.0001h));
                half maxAxis = max(absNormal.x, max(absNormal.y, absNormal.z));
                half tiling = max(_TriplanarScale, 0.001h);

                if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                {
                    uv = positionWS.zy * tiling;
                    dominantAxis = 0.0h;
                }
                else if (absNormal.z >= absNormal.y)
                {
                    uv = positionWS.xy * tiling;
                    dominantAxis = 2.0h;
                }
                else
                {
                    uv = positionWS.xz * tiling;
                    dominantAxis = 1.0h;
                }

                half edgeBand = saturate((1.0h - maxAxis) * max(_TriplanarSharpness, 1.0h));
                float stochastic = HectonCoreLitValueNoise2(floor(positionWS.xz * tiling * 31.0 + dominantAxis * 13.17)) * 2.0 - 1.0;
                uv += float2(stochastic, -stochastic) * edgeBand * 0.037;
            }

            half4 SampleFloraDominantAxis(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS)
            {
                float2 uv;
                half dominantAxis;
                ResolveFloraDominantAxisProjection(positionWS, normalWS, uv, dominantAxis);
                return SAMPLE_TEXTURE2D(tex, samp, uv);
            }

            half3 SampleFloraDominantAxisNormal(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS, half strength)
            {
                float2 uv;
                half dominantAxis;
                ResolveFloraDominantAxisProjection(positionWS, normalWS, uv, dominantAxis);
                half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uv), strength);

                if (dominantAxis < 0.5h)
                    return ResolveFloraNormalCheap(half3(0.0h, tangentNormal.y, tangentNormal.x));

                if (dominantAxis > 1.5h)
                    return ResolveFloraNormalCheap(half3(tangentNormal.x, tangentNormal.y, 0.0h));

                return ResolveFloraNormalCheap(half3(tangentNormal.x, 0.0h, tangentNormal.y));
            }

            half ComputeCurvatureWetness(half3 normalWS)
            {
                half3 derivative = abs(ddx(normalWS)) + abs(ddy(normalWS));
                return saturate(dot(derivative, half3(0.5h, 0.5h, 0.5h)) * _CurvatureWetnessStrength);
            }

            half CoralTrianglePulse01(float phase01)
            {
                return (half)(1.0 - abs(frac(phase01) * 2.0 - 1.0));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = HectonCoreLitSanitizePositionOS(input.positionOS.xyz);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = (half3)HectonCoreLitSafeNormalize(normalInputs.normalWS);
                output.color = input.color;
                output.uv = input.uv;
                output.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half3 baseNormalWS = ResolveFloraNormalCheap(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                half tintMask = saturate(input.color.r) * _VertexTintStrength;
                half moisture = saturate(input.color.g);
                half age = saturate(input.color.b);
                float3 samplePositionWS = input.positionWS;
                half4 maskSample = SampleFloraDominantAxis(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), samplePositionWS, baseNormalWS);
                #if defined(_QUALITY_HIGH)
                samplePositionWS -= viewDirWS * ((maskSample.b - 0.5h) * _HeightScale);
                maskSample = SampleFloraDominantAxis(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), samplePositionWS, baseNormalWS);
                #endif

                half3 baseTex = SampleFloraDominantAxis(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), samplePositionWS, baseNormalWS).rgb;
                // NOTE: baseNormalWS already declared at L229 - no redeclaration.
                half3 triplanarNormalWS = SampleFloraDominantAxisNormal(
                        TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                        samplePositionWS,
                        baseNormalWS,
                        _NormalStrength * _NormalScale);
                
                // Micro-Porosity Detail Normal
                half3 detailNormalWS = SampleFloraDominantAxisNormal(
                        TEXTURE2D_ARGS(_DetailNormalMap, sampler_DetailNormalMap),
                        samplePositionWS * _MicroPorosityScale,
                        baseNormalWS,
                        _DetailNormalStrength);

                half3 normalWS = ResolveFloraNormalCheap(baseNormalWS + triplanarNormalWS + detailNormalWS);
                float2 detailUv = samplePositionWS.xz * (_CausticScale * 0.06h)
                    + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.61h));
                half detailSample = (half)HectonCoreLitValueNoise2(detailUv);

                Light mainLight = GetMainLight();
                half3 lightDir = (half3)mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir));
                half wrapDiffuse = max(0.0h, dot(normalWS, lightDir) + 0.5h) * 0.6666667h;
                half rim = (half)HectonCoreLitFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);

                half floorZoneInfluence = saturate(_HectonFloorBiolumStrength);
                half oceanZoneInfluence = saturate(_HectonOceanBiolumStrength * 0.35h);
                half zoneBiolumStrength = saturate(floorZoneInfluence + oceanZoneInfluence);
                half3 volumeBiolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(samplePositionWS);

                half curvatureWetness = ComputeCurvatureWetness(normalWS);
                half cavity = saturate(1.0h - maskSample.r * _CavityStrength);
                half wetness = saturate(maskSample.g + moisture * _MoistureBoost + curvatureWetness + cavity * 0.18h + zoneBiolumStrength * 0.45h);
                half thickness = saturate(lerp(maskSample.b, maskSample.a, _ThicknessStrength));
                half glossNoise = lerp(1.0h, maskSample.g, _SpecularNoiseStrength);
                half roughness = saturate(lerp(0.7h, 0.2h, wetness));
                half causticMask = saturate(0.68h + detailSample * _CausticStrength + maskSample.a * 0.18h);

                half3 accent = lerp(_BaseColor.rgb, _AccentColor.rgb, saturate(maskSample.r + tintMask * 0.48h));
                half3 moistureTint = lerp(half3(1.0h, 1.0h, 1.0h), _AccentColor.rgb, wetness * 0.48h);
                half3 ageTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h - _AgeDarkening, 1.0h - _AgeDarkening, 1.0h - _AgeDarkening), age);
                half3 albedo = accent * baseTex * moistureTint * ageTint;
                albedo *= lerp(1.0h, detailSample, _DetailStrength);
                albedo = lerp(albedo, albedo * 0.78h, cavity * 0.22h);

                half3 ambient = SampleSH(normalWS) * (_AmbientStrength + wetness * 0.1h);
                half3 diffuse = albedo * (ambient + mainLight.color * wrapDiffuse);
                diffuse *= (1.0h - cavity * _CavityStrength * 0.5h);

                half3 subsurface = _SubsurfaceColor.rgb * (wrapDiffuse * _SubsurfaceStrength * causticMask);
                half3 rimLighting = _RimColor.rgb * (rim * _RimStrength);
                
                half specularSheen = NdotL * NdotL;
                half3 specular = specularSheen * (1.0h - roughness) * 0.22h * glossNoise * mainLight.color;
                half slimeSheen = specularSheen * specularSheen;
                half3 slimeSpecular = slimeSheen * wetness * 0.45h * mainLight.color;

                half3 biolum = volumeBiolum * (0.5h + thickness * 0.5h);
                [branch]
                if (_BiolumStrength > 0.0001h)
                {
                    float spatialPhaseScale = max((float)_BiolumPulseFrequency, 0.001);
                    float pulsePhase = frac(_GlobalBiolumPhase + (samplePositionWS.x * 0.011 + samplePositionWS.z * 0.008 + detailSample * 0.38) * spatialPhaseScale);
                    half pulse = 1.0h + (CoralTrianglePulse01(pulsePhase) * 2.0h - 1.0h) * _BiolumPulseAmplitude;
                    half biolumMask = saturate((cavity * 0.42h + maskSample.a * 0.28h + maskSample.b * 0.24h + detailSample * 0.18h) * _BiolumMaskStrength);
                    half authoredBiolumEnergy = _BiolumStrength * (1.0h + zoneBiolumStrength * 0.76h) * biolumMask * pulse;
                    [branch]
                    if (authoredBiolumEnergy > 0.0001h)
                    {
                        half3 zoneBiolumColor = lerp(_BiolumColor.rgb, _HectonFloorBiolumColor.rgb, floorZoneInfluence);
                        zoneBiolumColor = lerp(zoneBiolumColor, _HectonOceanBiolumColor.rgb, oceanZoneInfluence);
                        half3 authoredBiolum = zoneBiolumColor * authoredBiolumEnergy;
                        authoredBiolum *= HectonCoreLitResolveFlashlightPhotophobia(samplePositionWS);
                        biolum += authoredBiolum;
                    }
                }
                half fresnel = (half)HectonCoreLitFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;

                half3 color = diffuse + subsurface + rimLighting + specular + slimeSpecular + biolum;
                color = lerp(color, unity_FogColor.rgb * 0.88h, saturate(fresnel * (0.5h + wetness * 0.5h)));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 positionWS = TransformObjectToWorld(safePositionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                output.positionCS = TransformObjectToHClip(safePositionOS);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

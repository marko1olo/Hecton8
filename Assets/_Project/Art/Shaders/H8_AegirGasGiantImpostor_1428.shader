Shader "HECTON/Celestial/H8_AegirGasGiantImpostor_1428"
{
    Properties
    {
        _MainTex ("Base Cloud Bands", 2D) = "gray" {}
        _DetailTex ("Storm Detail", 2D) = "gray" {}
        _StormTex ("Storm Glow", 2D) = "black" {}
        [HDR] _DeepTint ("Deep Belt Tint", Color) = (0.16, 0.20, 0.42, 1)
        [HDR] _HighTint ("High Cloud Tint", Color) = (0.58, 0.64, 1.05, 1)
        [HDR] _WarmTint ("Warm Storm Tint", Color) = (0.80, 0.43, 0.24, 1)
        [HDR] _RimTint ("Atmosphere Rim Tint", Color) = (0.48, 0.58, 1.25, 1)
        _Exposure ("Exposure", Range(0.05, 3.0)) = 0.82
        _DetailStrength ("Detail Strength", Range(0.0, 2.0)) = 0.72
        _StormStrength ("Storm Strength", Range(0.0, 4.0)) = 0.42
        _RimStrength ("Rim Strength", Range(0.0, 4.0)) = 0.72
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.4
        _PhaseCenter ("Phase Center", Range(0.0, 1.0)) = 0.56
        _PhaseSoftness ("Phase Softness", Range(0.05, 1.0)) = 0.42
        _Rotation ("Band Rotation", Range(0.0, 1.0)) = 0.08
        _GlobalRotation ("Global Rotation Sync", Float) = 0.0
        _GameTime ("Game Time Sync", Float) = 0.0
        _PlanetPhase ("Runtime Planet Phase", Float) = 0.0
        _AutoRotationSpeed ("Auto Rotation Speed", Range(0.0, 0.02)) = 0.0022
        _DetailTiling ("Detail Tiling", Vector) = (2.2, 2.2, 0, 0)
        _StormTiling ("Storm Tiling", Vector) = (1.4, 1.4, 0, 0)
        _LightDirection ("Local Light Direction", Vector) = (-0.38, 0.28, 0.88, 0)
        [HDR] _AtmosphereVeilColor ("Atmosphere Veil Color", Color) = (0.62, 0.78, 0.94, 1)
        _HorizonVeilStrength ("Horizon Veil Strength", Range(0.0, 1.0)) = 0.46
        _HorizonVeilStart ("Horizon Veil Start", Range(-0.20, 0.35)) = 0.03
        _HorizonVeilEnd ("Horizon Veil End", Range(0.02, 0.55)) = 0.24
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
        }

        LOD 100

        Pass
        {
            Name "AegirImpostorForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            TEXTURE2D(_StormTex);  SAMPLER(sampler_StormTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                float4 _StormTex_ST;
                half4 _DeepTint;
                half4 _HighTint;
                half4 _WarmTint;
                half4 _RimTint;
                half _Exposure;
                half _DetailStrength;
                half _StormStrength;
                half _RimStrength;
                half _RimPower;
                half _PhaseCenter;
                half _PhaseSoftness;
                half _Rotation;
                float _GlobalRotation;
                float _GameTime;
                float _PlanetPhase;
                half _AutoRotationSpeed;
                half _pad0;
                float4 _DetailTiling;
                float4 _StormTiling;
                float4 _LightDirection;
                half4 _AtmosphereVeilColor;
                half _HorizonVeilStrength;
                half _HorizonVeilStart;
                half _HorizonVeilEnd;
            CBUFFER_END

            float4 _H8AegirSunDirection;
            float _H8GlobalQualityWeight;
            float4 _HectonCelestialLightReadability0;
            float4 _HectonCelestialLightReadability1;
            float4 _HectonCelestialLightReadability2;
            float4 _HectonCelestialLightReadability3;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half Luma(half3 color)
            {
                return dot(color, half3(0.299h, 0.587h, 0.114h));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUv = input.uv;
                float syncTime = max(_GameTime, _Time.y);
                baseUv.x = frac(baseUv.x + _Rotation + _GlobalRotation + syncTime * _AutoRotationSpeed);
                half quality = (half)saturate(max(_H8GlobalQualityWeight, 0.16));

                half3 baseSample = (half3)SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(baseUv, _MainTex)).rgb;

                float2 detailUv = baseUv * _DetailTiling.xy + _DetailTiling.zw;
                half3 detailSample = (half3)SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, detailUv).rgb;

                float2 stormUv = baseUv * _StormTiling.xy + _StormTiling.zw;
                half stormSample = Luma((half3)SAMPLE_TEXTURE2D(_StormTex, sampler_StormTex, stormUv).rgb);

                half baseLuma = Luma(baseSample);
                half detailLuma = Luma(detailSample);
                half cloudDeck = saturate((baseLuma - 0.10h) * 1.65h);
                half detailDeck = saturate((detailLuma - 0.28h) * 1.35h);
                half detailQuality = lerp(0.35h, 1.0h, quality);
                half beltSignal = saturate(cloudDeck * 0.82h + detailDeck * _DetailStrength * 0.22h * detailQuality);

                half3 authoredBands = baseSample * half3(0.86h, 0.96h, 1.22h);
                half3 controlledTint = lerp(_DeepTint.rgb, _HighTint.rgb, beltSignal);
                half3 bandColor = lerp(authoredBands, controlledTint, 0.28h);
                bandColor += (detailSample - half3(0.50h, 0.50h, 0.50h)) * (_DetailStrength * 0.18h * detailQuality);
                half stormMask = saturate((stormSample - 0.22h) * 1.55h);
                bandColor += _WarmTint.rgb * stormMask * _StormStrength * lerp(0.28h, 1.0h, quality);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDir = normalize((half3)(_WorldSpaceCameraPos.xyz - input.positionWS));
                float runtimeLightSq = dot(_H8AegirSunDirection.xyz, _H8AegirSunDirection.xyz);
                half3 lightDir = runtimeLightSq > 0.0001 ? normalize((half3)_H8AegirSunDirection.xyz) : normalize((half3)_LightDirection.xyz);
                half lit = saturate(dot(normalWS, lightDir) * 0.5h + 0.5h);
                half runtimePhase = saturate((half)_PlanetPhase * 0.5h + 0.5h);
                half phaseDriver = saturate(lit * 0.72h + runtimePhase * 0.28h);
                half phase = smoothstep(_PhaseCenter - _PhaseSoftness, _PhaseCenter + _PhaseSoftness, phaseDriver);

                half rim = pow(saturate(1.0h - dot(normalWS, viewDir)), _RimPower) * _RimStrength;
                half limb = saturate(1.0h - dot(normalWS, viewDir));
                half limbDarken = lerp(1.0h, 0.58h, pow(limb, 1.25h));
                half3 color = bandColor * lerp(0.42h, 1.0h, phase);
                color *= limbDarken;
                color += _RimTint.rgb * rim;
                color *= _Exposure;

                float readabilitySignal =
                    abs(_HectonCelestialLightReadability0.x) +
                    abs(_HectonCelestialLightReadability0.y) +
                    abs(_HectonCelestialLightReadability0.z) +
                    abs(_HectonCelestialLightReadability0.w) +
                    abs(_HectonCelestialLightReadability3.z);
                half readabilityKnown = readabilitySignal > 0.0001 ? 1.0h : 0.0h;
                half ambientReadability = (half)saturate(max(_HectonCelestialLightReadability0.y * 0.75, _HectonCelestialLightReadability0.z));
                half underwaterRange = (half)saturate(_HectonCelestialLightReadability0.w / 112.0);
                half deepLoss = (half)saturate(_HectonCelestialLightReadability1.y);
                half depthVisible = underwaterRange * lerp(0.36h, 1.0h, ambientReadability) * (1.0h - deepLoss * 0.78h);
                half waterVisibility = lerp(1.0h, depthVisible, saturate((half)_HectonCelestialLightReadability0.x * 0.05h));
                half systemVisibility = (half)saturate(1.0 - _H8AegirSunDirection.w);
                systemVisibility = min(systemVisibility, lerp(1.0h, waterVisibility, readabilityKnown));

                half3 viewRay = normalize((half3)(input.positionWS - _WorldSpaceCameraPos.xyz));
                half veilRange = max(_HorizonVeilEnd - _HorizonVeilStart, 0.001h);
                half horizonVeil = 1.0h - smoothstep(
                    _HorizonVeilStart,
                    _HorizonVeilStart + veilRange,
                    viewRay.y);
                half atmosphericDepth = saturate(horizonVeil * _HorizonVeilStrength);
                half edgeBleed = saturate(rim * 0.32h + atmosphericDepth * 0.68h);
                half3 veilColor = lerp(_AtmosphereVeilColor.rgb * 0.92h, _AtmosphereVeilColor.rgb * 1.14h, phase);
                half limbVeil = pow(saturate(1.0h - dot(normalWS, viewDir)), 0.72h);
                half atmosphereMask = saturate(atmosphericDepth + edgeBleed * 0.20h + limbVeil * 0.34h);
                color *= lerp(0.16h, 1.0h, max(systemVisibility, 0.035h));
                atmosphereMask = saturate(atmosphereMask + (1.0h - systemVisibility) * 0.55h);
                color = lerp(color, veilColor, atmosphereMask);

                return half4(max(color, half3(0.0h, 0.0h, 0.0h)), 1.0h);
            }
            ENDHLSL
        }
    }
}

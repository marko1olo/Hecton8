Shader "Hecton8/Environment/Hecton_DryZoneLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        [NoScaleOffset] _HectonMicroNormalTex("Micro Normal 128", 2D) = "bump" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _EnvironmentalWear("Environmental Wear", Range(0.0, 1.0)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _MicroNormalStrength("Micro Normal Strength", Range(0.0, 1.0)) = 0.18
        _MicroNormalTiling("Micro Normal Tiling", Range(4.0, 128.0)) = 48.0
        _StochasticTilingStrength("Stochastic Tiling Strength", Range(0.0, 1.0)) = 0.0
        _InteriorCondensationStrength("Interior Condensation Strength", Range(0.0, 1.0)) = 0.26
        _InteriorCondensationScale("Interior Condensation Scale", Range(0.05, 2.0)) = 0.42
        _InteriorCondensationRunoff("Interior Condensation Runoff", Range(0.0, 1.0)) = 0.34
        _InteriorCondensationTint("Interior Condensation Tint", Color) = (0.64, 0.76, 0.70, 1)
        _InteriorAbyssalFrostStrength("Interior Abyssal Frost Strength", Range(0.0, 1.0)) = 0.36
        _InteriorAbyssalFrostDepthStart("Interior Abyssal Frost Start Depth", Float) = 1200.0
        _InteriorAbyssalFrostDepthRange("Interior Abyssal Frost Depth Range", Float) = 2200.0
        _InteriorAbyssalFrostFlowThreshold("Interior Abyssal Frost Flow Threshold", Range(0.0, 20.0)) = 3.0
        _InteriorAbyssalFrostTint("Interior Abyssal Frost Tint", Color) = (0.68, 0.86, 0.92, 1)
        _WaterlineTint("Module Waterline Tint", Color) = (0.10, 0.38, 0.34, 0.32)
        _WaterlineDarken("Module Waterline Darken", Range(0.0, 1.0)) = 0.42
        _WaterlineRefractionStrength("Module Waterline Refraction Strength", Range(0.0, 0.08)) = 0.015
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        _ParasiteOverlayMap("Parasite Overlay", 2D) = "white" {}
        _ParasiteNormalMap("Parasite Normal", 2D) = "bump" {}
        [HDR] _ParasiteOverlayColor("Parasite Tint", Color) = (0.48, 0.92, 0.42, 1)
        [HDR] _ParasiteOverlayEmissionColor("Parasite Emission", Color) = (0.10, 0.42, 0.16, 1)
        _ParasiteOverlayScale("Parasite UV Scale", Float) = 0.18
        _ParasiteOverlayStrength("Parasite Blend", Range(0.0, 1.0)) = 0.8
        _ParasiteOverlayNormalStrength("Parasite Normal Strength", Range(0.0, 2.0)) = 0.65
        _ParasiteOverlaySmoothness("Parasite Smoothness", Range(0.0, 1.0)) = 0.18
        _ParasiteOverlayMetallic("Parasite Metallic", Range(0.0, 1.0)) = 0.02
        _Cull("Cull", Float) = 2.0
        [ToggleUI] _AlphaClip("Alpha Clip", Float) = 0.0
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _BumpMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [HideInInspector] _SpecColor("Specular", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _ParallaxMap("Height Map", 2D) = "black" {}
        [HideInInspector] _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        [HideInInspector] _DetailMask("Detail Mask", 2D) = "white" {}
        [HideInInspector] _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        [HideInInspector] _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [HideInInspector] _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        [HideInInspector] _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [HideInInspector] _WorkflowMode("WorkflowMode", Float) = 1.0
        [HideInInspector] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0.0
        [HideInInspector] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [HideInInspector] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask On

            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            StructuredBuffer<float4> _HectonModuleAmbienceDataBuffer;
            StructuredBuffer<float4> _HectonModuleWaterLevelsBuffer;
            int _ModuleWaterLevelCount;
            #include "Assets/_Project/Art/Shaders/Hecton_HabitatInterior.hlsl"
            float _BaseVoltage;
            float _BaseVoltageFlickerSpeed;
            float _BaseVoltageMinimum;
            float4 _BaseBrownoutEmergencyColor;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _RustSaltColor;
                float4 _ParasiteOverlayColor;
                float4 _ParasiteOverlayEmissionColor;
                float4 _InteriorCondensationTint;
                float4 _InteriorAbyssalFrostTint;
                float4 _BaseMap_ST;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _OcclusionStrength;
                float _EnvironmentalWear;
                float _MicroNormalStrength;
                float _MicroNormalTiling;
                float _StochasticTilingStrength;
                float _InteriorCondensationStrength;
                float _InteriorCondensationScale;
                float _InteriorCondensationRunoff;
                float _InteriorAbyssalFrostStrength;
                float _InteriorAbyssalFrostDepthStart;
                float _InteriorAbyssalFrostDepthRange;
                float _InteriorAbyssalFrostFlowThreshold;
                float4 _WaterlineTint;
                float _WaterlineDarken;
                float _WaterlineRefractionStrength;
                float _ParasiteOverlayScale;
                float _ParasiteOverlayStrength;
                float _ParasiteOverlayNormalStrength;
                float _ParasiteOverlaySmoothness;
                float _ParasiteOverlayMetallic;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURE2D(_DetailMask);
            SAMPLER(sampler_DetailMask);
            TEXTURE2D(_ParasiteOverlayMap);
            SAMPLER(sampler_ParasiteOverlayMap);
            TEXTURE2D(_ParasiteNormalMap);
            SAMPLER(sampler_ParasiteNormalMap);

            struct Attributes
            {
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
                HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half xrNearClipFade : TEXCOORD5;
                float2 xrFoveatedVector : TEXCOORD6;
                half hullDentShadow : TEXCOORD7;
                half habitatStress01 : TEXCOORD8;
            };

            half3 SafeNormalize3(half3 value)
            {
                half lenSq = dot(value, value);
                return lenSq > 0.0001h ? value * rsqrt(lenSq) : half3(0.0h, 1.0h, 0.0h);
            }

            half HectonFastSpecularLobe(half specularBase, half smoothness)
            {
                half b2 = specularBase * specularBase;
                half b4 = b2 * b2;
                half b8 = b4 * b4;
                half b16 = b8 * b8;
                half b32 = b16 * b16;
                half b64 = b32 * b32;
                return lerp(b16, b64, saturate(smoothness));
            }

            half3 BuildParasiteNormalWS(float3 positionWS, half3 baseNormalWS)
            {
                float2 uv = positionWS.xz * max(_ParasiteOverlayScale, 0.001);
                half3 parasiteNormalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_ParasiteNormalMap, sampler_ParasiteNormalMap, uv),
                    _ParasiteOverlayNormalStrength);
                half3 baseNormal = SafeNormalize3(baseNormalWS);
                half3 tangentWS = abs(baseNormal.y) < 0.999h
                    ? SafeNormalize3(cross(half3(0.0h, 1.0h, 0.0h), baseNormal))
                    : half3(1.0h, 0.0h, 0.0h);
                half3 bitangentWS = SafeNormalize3(cross(baseNormal, tangentWS));
                return SafeNormalize3(
                    tangentWS * parasiteNormalTS.x +
                    bitangentWS * parasiteNormalTS.y +
                    baseNormal * parasiteNormalTS.z);
            }

            half3 BlendProceduralDropletNormalWS(half filmNoise, half dripNoise, half frostCrystal, half3 baseNormalWS, half strength)
            {
                half3 baseNormal = SafeNormalize3(baseNormalWS);
                half3 tangentWS = abs(baseNormal.y) < 0.999h
                    ? SafeNormalize3(cross(half3(0.0h, 1.0h, 0.0h), baseNormal))
                    : half3(1.0h, 0.0h, 0.0h);
                half3 bitangentWS = SafeNormalize3(cross(baseNormal, tangentWS));
                half slopeX = ((dripNoise - 0.5h) * 1.36h + (frostCrystal - 0.5h) * 0.52h) * strength;
                half slopeY = (filmNoise - 0.5h) * strength;
                half3 dropletNormalWS = SafeNormalize3(
                    tangentWS * slopeX +
                    bitangentWS * slopeY +
                    baseNormal);
                return SafeNormalize3(lerp(baseNormal, dropletNormalWS, saturate(strength)));
            }

            void ApplyInteriorCondensation(
                float3 positionWS,
                float2 uv,
                inout half3 normalWS,
                half depthCondensation01,
                inout half3 albedo,
                inout half smoothness)
            {
                half depthStrength = saturate(depthCondensation01);
                half condensationStrength = saturate((half)_InteriorCondensationStrength) * depthStrength;
                half abyssalFlowSpeedSq = (half)dot(_AbyssalFlowWeatherCurrent.xyz, _AbyssalFlowWeatherCurrent.xyz);
                half frostFlowThresholdSq = (half)(_InteriorAbyssalFrostFlowThreshold * _InteriorAbyssalFrostFlowThreshold);
                half abyssalFlowCold01 = saturate(
                    (abyssalFlowSpeedSq - frostFlowThresholdSq) /
                    max(0.01h, 400.0h - frostFlowThresholdSq));
                half depthMeters = saturate((half)((-positionWS.y - _InteriorAbyssalFrostDepthStart) / max(_InteriorAbyssalFrostDepthRange, 1.0)));
                half frostStrength = saturate((half)_InteriorAbyssalFrostStrength) * abyssalFlowCold01 * depthMeters;
                if (condensationStrength <= 0.0001h && frostStrength <= 0.0001h)
                    return;

                half wallMaskBase = saturate(1.0h - abs(normalWS.y));
                half wallMask = wallMaskBase * lerp(0.68h, 1.0h, wallMaskBase);
                if (wallMask <= 0.0001h)
                    return;

                float scale = max(_InteriorCondensationScale, 0.05);
                float2 wallUv = float2(dot(positionWS.xz, float2(0.73, 0.41)), positionWS.y) * scale;
                float slowTime = _Time.y * lerp(0.03, 0.16, _InteriorCondensationRunoff);
                half filmNoise = (half)HectonCoreLitValueNoise2(wallUv * 3.1 + slowTime);
                half dripNoise = (half)HectonCoreLitValueNoise2(float2(wallUv.x * 8.7, wallUv.y * 0.52 - slowTime * 3.4));
                half dripLines = smoothstep(0.76h, 0.98h, dripNoise) * smoothstep(0.18h, 0.92h, filmNoise);
                half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).r;
                half condensation = saturate(
                    (filmNoise * 0.34h + dripLines * _InteriorCondensationRunoff) *
                    wallMask *
                    condensationStrength *
                    lerp(0.35h, 1.0h, detailMask));

                half frostCrystal = smoothstep(0.58h, 0.93h, (half)HectonCoreLitValueNoise2(wallUv * 13.3 + float2(0.17, -0.09) * _Time.y));
                half frostRime = smoothstep(0.42h, 0.96h, filmNoise) * smoothstep(0.15h, 0.86h, saturate(1.0h - abs(normalWS.y)));
                half frost = saturate((frostCrystal * 0.76h + frostRime * 0.24h) * wallMask * frostStrength * lerp(0.28h, 1.0h, detailMask));

                albedo = lerp(albedo, _InteriorCondensationTint.rgb, condensation * lerp(0.10h, 0.24h, depthStrength));
                albedo = lerp(albedo, _InteriorAbyssalFrostTint.rgb, frost * 0.48h);
                smoothness = lerp(smoothness, lerp(0.72h, 0.96h, depthStrength), condensation);
                smoothness = lerp(smoothness, 0.32h, frost);
                half dropletNormalStrength = saturate((condensation + frost * 0.35h) * 0.55h);
                if (dropletNormalStrength > 0.0001h)
                    normalWS = BlendProceduralDropletNormalWS(filmNoise, dripNoise, frostCrystal, normalWS, dropletNormalStrength);
            }

            void ResolveModuleAmbience(
                float3 positionWS,
                out half level01,
                out float waterY,
                out half flicker01,
                out half condensationDepth01)
            {
                level01 = 0.0h;
                waterY = -100000.0;
                flicker01 = 1.0h;
                condensationDepth01 = 0.0h;
                int count = min(max(_ModuleWaterLevelCount, 0), 64);
                float bestDistanceSq = 1.0e20;

                [loop]
                for (int i = 0; i < count; i++)
                {
                    float4 centerRadius = _HectonModuleAmbienceDataBuffer[i];
                    float radius = max(centerRadius.w, 0.001);
                    float3 delta = positionWS - centerRadius.xyz;
                    float distanceSq = dot(delta, delta);
                    if (distanceSq > radius * radius || distanceSq >= bestDistanceSq)
                        continue;

                    float4 floodAndFlicker = _HectonModuleWaterLevelsBuffer[i];
                    bestDistanceSq = distanceSq;
                    waterY = floodAndFlicker.x;
                    level01 = (half)saturate(floodAndFlicker.y);
                    flicker01 = (half)saturate(floodAndFlicker.z);
                    condensationDepth01 = (half)saturate(floodAndFlicker.w);
                }
            }

            half ResolveModuleSubmerged01(float3 positionWS, half level01, float waterY)
            {
                if (level01 <= 0.0001h)
                    return 0.0h;

                return (half)smoothstep(0.04, -0.04, positionWS.y - waterY);
            }

            float2 ResolveModuleWaterlineWarp(float3 positionWS, half submerged01)
            {
                if (submerged01 <= 0.0001h)
                    return float2(0.0, 0.0);

                float2 rippleUv = positionWS.xz * 1.9 + _Time.y * float2(0.06, -0.041);
                half rippleX = (half)HectonCoreLitValueNoise2(rippleUv);
                half rippleY = (half)HectonCoreLitValueNoise2(rippleUv * 1.37 + float2(2.13, -0.71));
                return float2(rippleX - 0.5h, rippleY - 0.5h) * _WaterlineRefractionStrength * submerged01;
            }

            void ApplyModuleWaterline(half level01, half submerged01, inout half3 albedo, inout half smoothness)
            {
                if (submerged01 <= 0.0001h)
                    return;

                half floodStrength = saturate(level01 * submerged01);
                half darken = submerged01 * (half)saturate(_WaterlineDarken);
                half tintStrength = floodStrength * (half)saturate(_WaterlineTint.a);
                half3 waterTint = half3(_WaterlineTint.r, _WaterlineTint.g, _WaterlineTint.b);
                albedo = saturate(albedo * (1.0h - darken * 0.55h));
                albedo = lerp(albedo, waterTint, tintStrength);
                smoothness = lerp(smoothness, 0.88h, saturate(submerged01 * 0.62h));
            }

            half ResolveBaseVoltageFlicker01(half moduleVoltage01)
            {
                half voltage01 = saturate((half)_BaseVoltage * moduleVoltage01);
                half brownout01 = saturate((0.8h - voltage01) * 1.25h);
                if (brownout01 <= 0.0001h)
                    return 1.0h;

                float speed = max(_BaseVoltageFlickerSpeed, 0.1);
                float2 noiseUv = float2(_Time.y * speed, _Time.y * (speed * 0.271 + 1.37));
                half noise01 = (half)HectonCoreLitValueNoise2(noiseUv);
                half dropout01 = (half)HectonCoreLitValueNoise2(noiseUv * 1.83 + float2(13.17, -4.91));
                half sine01 = (half)HectonCoreLitTrianglePulse01(_Time.y * 20.0);
                half floor01 = saturate((half)_BaseVoltageMinimum);
                half flicker01 = lerp(1.0h, lerp(floor01, 1.0h, noise01), brownout01);
                flicker01 *= lerp(1.0h, lerp(0.14h, 0.42h, voltage01), brownout01 * step(0.68h, dropout01));
                flicker01 *= lerp(1.0h, lerp(0.68h, 1.28h, sine01), brownout01);
                return saturate(max(floor01, flicker01));
            }

            float2 ResolveBrownoutGlitchWarp(float2 uv, half moduleVoltage01)
            {
                half voltage01 = saturate((half)_BaseVoltage * moduleVoltage01);
                half brownout01 = saturate((0.62h - voltage01) * 1.6129h);
                if (brownout01 <= 0.0001h)
                    return uv;

                half rowNoise = (half)HectonCoreLitValueNoise2(float2(floor(uv.y * 64.0), floor(_Time.y * 11.0)));
                half rowGate = smoothstep(0.74h, 1.0h, rowNoise);
                half jitter = (half)HectonCoreLitValueNoise2(float2(floor(_Time.y * 18.0), floor(uv.y * 23.0))) - 0.5h;
                uv.x += jitter * rowGate * brownout01 * 0.012;
                return uv;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
                HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                half hullDentShadow;
                float3 safePositionOS = HectonCoreLitApplyHullDentsOS(input.positionOS.xyz, input.normalOS, hullDentShadow);
                float habitatPeakStress01 = _HectonHabitatModuleStressParams.w;
                float habitatStress01 = isfinite(habitatPeakStress01) ? saturate(habitatPeakStress01) : 0.0;
                if (_HectonHabitatModuleStressParams.z <= 0.5 &&
                    _HectonHabitatModuleStressParams.x > 0.5 &&
                    habitatStress01 > HECTON_HABITAT_INTERIOR_STRESS_EPSILON)
                {
                    VertexPositionInputs preBendPositionInputs = GetVertexPositionInputs(safePositionOS);
                    habitatStress01 = HectonHabitatInteriorResolveStress01(preBendPositionInputs.positionWS);
                }
                half habitatBendShadow = 0.0h;
                half habitatPanelMask01 = 0.0h;
                half2 habitatPanelCenteredUv = half2(0.0h, 0.0h);
                bool habitatVertexBendActive =
                    _HectonHabitatModuleStressParams.z <= 0.5 &&
                    _HectonHabitatModuleStressParams.y > 0.00001 &&
                    habitatStress01 > HECTON_HABITAT_INTERIOR_STRESS_EPSILON;
                if (habitatVertexBendActive)
                {
                    safePositionOS = HectonHabitatInteriorApplyPanelBendOS(
                        safePositionOS,
                        input.normalOS,
                        input.uv,
                        habitatStress01,
                        habitatBendShadow,
                        habitatPanelMask01,
                        habitatPanelCenteredUv);
                }
                VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                half3 normalWS = SafeNormalize3(normalInputs.normalWS);
                if (habitatVertexBendActive && habitatPanelMask01 > 0.0001h)
                    normalWS = HectonHabitatInteriorApplyCheapNormalBiasWS(normalWS, habitatStress01, habitatPanelMask01, habitatPanelCenteredUv);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalWS;
                output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.xrNearClipFade = (half)HectonCoreLitEvaluateXRNearClipFade(output.positionWS);
                output.xrFoveatedVector = HectonCoreLitBuildStereoFoveationVector(output.positionWS);
                output.hullDentShadow = max(hullDentShadow, habitatBendShadow);
                output.habitatStress01 = (half)saturate(habitatStress01);
                return output;
            }

            half3 EvaluateLighting(float3 positionWS, float4 positionCS, half3 normalWS, half3 viewDirWS, half3 albedo, half metallic, half smoothness, half occlusion)
            {
                half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
                half3 color = SampleSH(normalWS) * albedo * occlusion * caveAmbientFactor;
                half specularStrength = lerp(0.04h, 0.22h, metallic);

                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDir = SafeNormalize3(mainLight.direction);
                half nDotL = saturate(dot(normalWS, lightDir));
                half specular = 0.0h;
                half specularEnergy = smoothness * specularStrength;
                if (nDotL > 0.0001h && specularEnergy > 0.0001h)
                {
                    half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
                    half specularBase = saturate(dot(normalWS, halfDir));
                    if (specularBase > 0.0001h)
                        specular = HectonFastSpecularLobe(specularBase, smoothness) * specularEnergy;
                }
                half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
                color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    half3 additionalDir = SafeNormalize3(light.direction);
                    half additionalNdotL = saturate(dot(normalWS, additionalDir));
                    half additionalSpecular = 0.0h;
                    if (additionalNdotL > 0.0001h && specularEnergy > 0.0001h)
                    {
                        half3 additionalHalfDir = SafeNormalize3(additionalDir + viewDirWS);
                        half additionalSpecularBase = saturate(dot(normalWS, additionalHalfDir));
                        if (additionalSpecularBase > 0.0001h)
                            additionalSpecular = HectonFastSpecularLobe(additionalSpecularBase, smoothness) * specularEnergy;
                    }
                    float additionalShadowAttenuation = HectonCoreLitResolveFlashlightAdditionalShadow(lightIndex, positionWS, normalWS, light.shadowAttenuation);
                    color += (albedo * additionalNdotL + additionalSpecular) * light.color * (light.distanceAttenuation * additionalShadowAttenuation);
                LIGHT_LOOP_END
                #endif

                color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;

                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HectonCoreLitClipXRNearWallDither(input.xrNearClipFade, input.positionCS);
                bool xrFullQuality = HectonCoreLitShouldRunXRFullQuality(input.xrFoveatedVector);
                half moduleFloodLevel01;
                float moduleWaterY;
                half moduleFlicker01;
                half moduleCondensationDepth01;
                ResolveModuleAmbience(input.positionWS, moduleFloodLevel01, moduleWaterY, moduleFlicker01, moduleCondensationDepth01);
                half moduleSubmerged01 = ResolveModuleSubmerged01(input.positionWS, moduleFloodLevel01, moduleWaterY);
                float2 baseUv = input.uv + ResolveModuleWaterlineWarp(input.positionWS, moduleSubmerged01);
                baseUv = ResolveBrownoutGlitchWarp(baseUv, moduleFlicker01);
                half4 albedoSample = HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), baseUv, input.uv * 0.031, (half)_StochasticTilingStrength) * _BaseColor;
                half coverage = 1.0h;
                #if defined(_ALPHATEST_ON)
                coverage = saturate((albedoSample.a - _Cutoff) * 14.0h + 0.5h);
                #endif

                half4 packedMask = HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), baseUv, input.uv * 0.031, (half)_StochasticTilingStrength);
                HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
                half occlusion = decodedMask.occlusion;
                half metallic = decodedMask.metallic;
                half smoothness = decodedMask.smoothness;
                half emissionMask = decodedMask.emissionMask;
                half3 normalWS = SafeNormalize3(input.normalWS);
                half3 albedo = albedoSample.rgb;
                if (xrFullQuality)
                {
                    normalWS = HectonCoreLitApplyTripleDetailMicroNormals(input.positionWS, normalWS, (half)_MicroNormalStrength, (half)_MicroNormalTiling, 2.0h);
                    HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);
                    ApplyInteriorCondensation(input.positionWS, input.uv, normalWS, moduleCondensationDepth01, albedo, smoothness);
                }
                ApplyModuleWaterline(moduleFloodLevel01, moduleSubmerged01, albedo, smoothness);
                HectonCoreLitApplyEnvironmentalWear(input.positionWS, normalWS, (half)_EnvironmentalWear, (half3)_RustSaltColor.rgb, albedo, metallic, smoothness);
                half hullDentShadow = input.hullDentShadow;
                [branch]
                if (_HectonHullDentParams.y > 0.5 && _HectonHullDentParams.z > 0.0001)
                {
                    half lowTierScarTexture = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv * 2.7).r;
                    hullDentShadow = max(hullDentShadow, (half)_HectonHullDentParams.z * lowTierScarTexture * 0.28h);
                }
                [branch]
                if (_HectonHabitatModuleStressParams.z > 0.5 && input.habitatStress01 > HECTON_HABITAT_INTERIOR_STRESS_EPSILON_HALF)
                {
                    half habitatPanelMask = HectonHabitatInteriorCheapPanelMask(input.uv);
                    [branch]
                    if (habitatPanelMask > 0.0001h)
                    {
                        half habitatCreaseMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv * 3.1).r;
                        HectonHabitatInteriorApplyLowTierCrease(input.habitatStress01, habitatPanelMask, habitatCreaseMask, hullDentShadow, albedo, smoothness);
                    }
                }
                HectonCoreLitApplyHullDentSurfaceCheat(hullDentShadow, albedo, smoothness);
                float parasitePulse = 1.0;
                float thermalGrowthMask = 0.0;
                float parasiteMask = HectonCoreLitEvaluateParasiteField(input.positionWS, parasitePulse, thermalGrowthMask);
                half3 parasiteEmissionMask = half3(0.0h, 0.0h, 0.0h);
                if (parasiteMask > 0.0001)
                {
                    float2 parasiteUv = input.positionWS.xz * max(_ParasiteOverlayScale, 0.001);
                    half4 parasiteOverlay = SAMPLE_TEXTURE2D(_ParasiteOverlayMap, sampler_ParasiteOverlayMap, parasiteUv);
                    half parasiteBlend = saturate((half)(parasiteMask * _ParasiteOverlayStrength * parasiteOverlay.a));
                    parasiteEmissionMask = parasiteOverlay.rgb;
                    [branch]
                    if (parasiteBlend > 0.0001h)
                    {
                        half3 parasiteColor = parasiteOverlay.rgb * _ParasiteOverlayColor.rgb;
                        half3 parasiteNormalWS = BuildParasiteNormalWS(input.positionWS, normalWS);
                        albedo = lerp(albedo, parasiteColor, parasiteBlend);
                        normalWS = SafeNormalize3(lerp(normalWS, parasiteNormalWS, parasiteBlend));
                        metallic = lerp(metallic, (half)_ParasiteOverlayMetallic, parasiteBlend);
                        smoothness = lerp(smoothness, (half)_ParasiteOverlaySmoothness, parasiteBlend);
                    }
                }
                half3 litColor = EvaluateLighting(
                    input.positionWS,
                    input.positionCS,
                    normalWS,
                    SafeNormalize3(input.viewDirWS),
                    albedo,
                    metallic,
                    smoothness,
                    saturate(occlusion));
                half3 emission = _EmissionColor.rgb * emissionMask;
                half baseVoltageFlicker01 = ResolveBaseVoltageFlicker01(moduleFlicker01);
                half brownoutEmergency01 = saturate((0.8h - saturate((half)_BaseVoltage * moduleFlicker01)) * 1.25h);
                half3 emergencyTint = half3(_BaseBrownoutEmergencyColor.r, _BaseBrownoutEmergencyColor.g, _BaseBrownoutEmergencyColor.b);
                litColor *= lerp(0.62h, 1.0h, baseVoltageFlicker01);
                litColor = lerp(litColor, litColor * emergencyTint, brownoutEmergency01 * (1.0h - baseVoltageFlicker01 * 0.35h));
                emission *= baseVoltageFlicker01;
                emission = lerp(emission, emergencyTint * max(emission.r, max(emission.g, emission.b)), brownoutEmergency01 * 0.65h);
                if (parasiteMask > 0.0001)
                {
                    half parasiteEmission = (half)(parasiteMask * saturate(parasitePulse) * lerp(1.0, 1.35, thermalGrowthMask));
                    emission += parasiteEmissionMask * _ParasiteOverlayEmissionColor.rgb * parasiteEmission;
                }
                emission += (half3)HectonCoreLitEvaluateActiveSonarGeoEmission(input.positionWS);
                half3 finalColor = MixFog(litColor + emission, input.fogFactor);
                finalColor = HectonCoreLitApplyXRFoveatedResolve(finalColor, input.xrFoveatedVector);
                return half4(finalColor, coverage);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }

    FallBack Off
}

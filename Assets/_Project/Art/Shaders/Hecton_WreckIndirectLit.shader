Shader "Hecton8/World/WreckIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Packed MRAO (R Metallic G Roughness B AO A Emission/Carbon)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.42
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EnvironmentalWear("Environmental Wear", Range(0, 1)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _StormRainDripAmplitude("Storm Rain Drip Amplitude", Range(0, 0.025)) = 0.004
        _StormRainDripTiling("Storm Rain Drip Tiling", Range(0.5, 16)) = 5
        _StormRainDripSpeed("Storm Rain Drip Speed", Range(0, 8)) = 1.8
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
        _WreckSiltStrength("Wreck Silt Strength", Range(0, 1)) = 0.36
        _WreckRustStrength("Wreck Rust Strength", Range(0, 1)) = 0.82
        _WreckSiltTint("Wreck Silt Tint", Color) = (0.23, 0.28, 0.26, 1)
        _WreckRustTint("Heavy Orange Rust", Color) = (0.86, 0.28, 0.055, 1)
        _WreckVertexRustInfluence("Vertex Rust Influence", Range(0, 1)) = 1.0
        _WreckVertexAlgaeInfluence("Vertex Algae Influence", Range(0, 1)) = 0.65
        _WreckAlgaeTint("Algae Tint", Color) = (0.16, 0.34, 0.22, 1)
        _WreckGrimeStrength("Vertex Blue Grime Strength", Range(0, 1)) = 0.58
        _WreckSootStrength("Vertex Alpha Soot Strength", Range(0, 1)) = 0.92
        _WreckSootTint("Burnt Soot Tint", Color) = (0.035, 0.04, 0.045, 1)
        _WreckSwayAmplitude("Boneless Debris Sway", Range(0, 0.08)) = 0.018
        _WreckSwaySpeed("Boneless Debris Sway Speed", Range(0, 4)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

        StructuredBuffer<float4x4> _HectonWreckMatrices;
        StructuredBuffer<float> _HectonWreckAges;
        float _HectonWreckEmergencyFlicker;
        float _HectonWreckEmergencyPhase;
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _RustSaltColor;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EnvironmentalWear;
            float _StormRainDripAmplitude;
            float _StormRainDripTiling;
            float _StormRainDripSpeed;
            float _Cutoff;
            float _DepthBias;
            float _WreckSiltStrength;
            float _WreckRustStrength;
            float4 _WreckSiltTint;
            float4 _WreckRustTint;
            float _WreckVertexRustInfluence;
            float _WreckVertexAlgaeInfluence;
            float4 _WreckAlgaeTint;
            float _WreckGrimeStrength;
            float _WreckSootStrength;
            float4 _WreckSootTint;
            float _WreckSwayAmplitude;
            float _WreckSwaySpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            half age01 : TEXCOORD4;
            half4 vertexColor : TEXCOORD5;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float4x4 ResolveWreckMatrix(uint instanceID)
        {
            return _HectonWreckMatrices[instanceID];
        }

        half ResolveWreckAge(uint instanceID)
        {
            return (half)saturate(_HectonWreckAges[instanceID]);
        }

        half3 ResolveWreckNormalCheap(float3 value)
        {
            return (half3)HectonCoreLitSafeNormalize(value);
        }

        float3 TransformWreckNormal(float4x4 instanceMatrix, float3 normalOS)
        {
            return (float3)ResolveWreckNormalCheap(mul((float3x3)instanceMatrix, normalOS));
        }

        half ResolveWreckTriangle01(float phase)
        {
            return (half)saturate(abs(frac(phase) - 0.5) * 2.0);
        }

        float3 ApplyBonelessDebrisSway(float3 positionWS, float3 normalWS, half4 vertexColor, half age01)
        {
            half freeEdgeMask = saturate((1.0h - abs((half)normalWS.y)) + vertexColor.r * 0.45h + vertexColor.g * 0.25h);
            half swayMask = saturate(freeEdgeMask * (half)_WreckSwayAmplitude);
            float phase = dot(positionWS.xz, float2(0.071, 0.043)) + _Time.y * _WreckSwaySpeed + age01 * 3.17 + _HectonWreckEmergencyPhase;
            half waveA = ResolveWreckTriangle01(phase) * 2.0h - 1.0h;
            half waveB = ResolveWreckTriangle01(phase * 0.73 + 0.31) * 2.0h - 1.0h;
            return positionWS + float3(waveA * swayMask, waveB * swayMask * 0.25h, waveB * swayMask * 0.45h);
        }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            uint instanceID = input.instanceID;
        #if UNITY_ANY_INSTANCING_ENABLED
            instanceID = unity_InstanceID;
        #endif
            float4x4 instanceMatrix = ResolveWreckMatrix(instanceID);
            float4 positionWS = mul(instanceMatrix, float4(HectonCoreLitSanitizePositionOS(input.positionOS.xyz), 1.0));
            output.normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            output.positionWS = HectonCoreLitApplySubmarineCrushDepth(positionWS.xyz, output.normalWS);
            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(output.positionWS, output.normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.age01 = ResolveWreckAge(instanceID);
            output.vertexColor = saturate(input.color);
            output.positionWS = ApplyBonelessDebrisSway(output.positionWS, output.normalWS, output.vertexColor, output.age01);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, _DepthBias, 1.0);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleWreckSurface(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        half4 SamplePackedMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
        }

        half ResolveBakedCarbonization(half maskAlpha, half3 albedo, half roughness, half metallic)
        {
            half luminance = dot(albedo, half3(0.299h, 0.587h, 0.114h));
            half burnDarkness = saturate(1.0h - luminance);
            half oxidizedInsulator = saturate((1.0h - metallic) * roughness);
            return saturate(maskAlpha * burnDarkness * oxidizedInsulator);
        }

        half3 EvaluateWreckLighting(
            float3 positionWS,
            float4 positionCS,
            half3 normalWS,
            half3 albedo,
            half metallic,
            half smoothness,
            half ambientOcclusion)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = H8CustomLightProbeResolveAmbient(positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * albedo * ambientOcclusion * caveAmbientFactor;
            half specularStrength = 0.04h + 0.18h * metallic;

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = (half3)mainLight.direction;
            half nDotL = saturate(dot(normalWS, lightDir));
            half specularEnergy = smoothness * specularStrength;
            half nl2 = nDotL * nDotL;
            half broadSpecular = nl2 * nl2;
            half tightSpecular = broadSpecular * broadSpecular;
            half specularMask = step(0.0001h, nDotL * specularEnergy);
            half specular = (broadSpecular + (tightSpecular - broadSpecular) * smoothness) * specularEnergy * specularMask;
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow * contactShadow);

            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 surface = SampleWreckSurface(input.uv);
        #if defined(_ALPHATEST_ON)
            clip(surface.a - _Cutoff);
        #endif

            half4 packedMask = SamplePackedMask(input.uv);
            half metallic = saturate(packedMask.r * (half)_Metallic);
            half roughness = saturate(packedMask.g);
            half ambientOcclusion = saturate(lerp(1.0h, packedMask.b, (half)_OcclusionStrength));
            half smoothness = saturate((1.0h - roughness) * (half)_Smoothness);
            half maskAlpha = saturate(packedMask.a);

            half3 normalWS = ResolveWreckNormalCheap(input.normalWS);
            half3 albedo = surface.rgb;
            half bakedCarbonization = ResolveBakedCarbonization(maskAlpha, albedo, roughness, metallic);
            HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);
            half vertexMask = 1.0h - saturate(input.vertexColor.b);
            half vertexRust = saturate(input.vertexColor.r * (half)_WreckVertexRustInfluence * vertexMask);
            half vertexAlgae = saturate(input.vertexColor.g * (half)_WreckVertexAlgaeInfluence * vertexMask);
            half vertexGrime = saturate(input.vertexColor.b * (half)_WreckGrimeStrength);
            half vertexSoot = saturate(input.vertexColor.a * (half)_WreckSootStrength);
            half sootResponse = max(vertexSoot, saturate(bakedCarbonization * (half)_WreckSootStrength));
            half edgeWearMask = saturate((1.0h - ambientOcclusion) * 0.7h + (1.0h - smoothness) * 0.35h + vertexRust);
            half rustAge = saturate(input.age01 + vertexRust * 0.35h);
            HectonCoreLitApplyProceduralRustSilt(
                input.positionWS,
                normalWS,
                normalWS,
                edgeWearMask,
                rustAge,
                (half)_WreckSiltStrength,
                (half)_WreckRustStrength,
                half3(_WreckSiltTint.rgb),
                half3(_WreckRustTint.rgb),
                albedo,
                metallic,
                smoothness);
            albedo = lerp(albedo, albedo * half3(_WreckAlgaeTint.rgb), vertexAlgae);
            ambientOcclusion = lerp(ambientOcclusion, ambientOcclusion * 0.82h, vertexAlgae);
            smoothness = lerp(smoothness, smoothness * 0.72h, vertexAlgae);
            albedo = lerp(albedo, albedo * half3(0.52h, 0.58h, 0.61h), vertexGrime);
            ambientOcclusion = lerp(ambientOcclusion, ambientOcclusion * 0.68h, vertexGrime);
            smoothness = lerp(smoothness, smoothness * 0.58h, vertexGrime);
            albedo = lerp(albedo, albedo * half3(_WreckSootTint.rgb), sootResponse);
            ambientOcclusion = lerp(ambientOcclusion, ambientOcclusion * 0.48h, sootResponse);
            smoothness = lerp(smoothness, smoothness * 0.36h, sootResponse);
            metallic = lerp(metallic, metallic * 0.22h, bakedCarbonization);
            HectonCoreLitApplyEnvironmentalWear(input.positionWS, normalWS, (half)_EnvironmentalWear, (half3)_RustSaltColor.rgb, albedo, metallic, smoothness);
            half3 litColor = EvaluateWreckLighting(
                input.positionWS,
                input.positionCS,
                normalWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half emergencyPulse = saturate((half)_HectonWreckEmergencyFlicker * (0.35h + ResolveWreckTriangle01(_Time.y * 3.7 + _HectonWreckEmergencyPhase) * 0.65h));
            half3 emission = _EmissionColor.rgb * maskAlpha * emergencyPulse;
            emission += (half3)HectonCoreLitEvaluateActiveSonarGeoEmission(input.positionWS);
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input, uint instanceID)
        {
            float4x4 instanceMatrix = ResolveWreckMatrix(instanceID);
            float4 positionWS = mul(instanceMatrix, float4(HectonCoreLitSanitizePositionOS(input.positionOS.xyz), 1.0));
            float3 normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            positionWS.xyz = HectonCoreLitApplyStormRainDripVertexRipple(positionWS.xyz, normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            positionWS.xyz = ApplyBonelessDebrisSway(positionWS.xyz, normalWS, saturate(input.color), ResolveWreckAge(instanceID));
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS.xyz, normalWS, lightDirectionWS));
        #if UNITY_REVERSED_Z
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                output.positionCS = GetShadowPositionHClip(input, instanceID);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_ALPHATEST_ON)
                half alpha = SampleWreckSurface(input.uv).a;
                clip(alpha - _Cutoff);
            #endif
                return 0.0h;
            }
            ENDHLSL
        }
    }
}

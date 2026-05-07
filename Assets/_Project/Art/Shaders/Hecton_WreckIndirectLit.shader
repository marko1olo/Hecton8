Shader "Hecton8/World/WreckIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Mask Map", 2D) = "white" {}
        [NoScaleOffset] _HectonMicroNormalTex("Micro Normal 128", 2D) = "bump" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.42
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EnvironmentalWear("Environmental Wear", Range(0, 1)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _MicroNormalStrength("Micro Normal Strength", Range(0, 1)) = 0.28
        _MicroNormalTiling("Micro Normal Tiling", Range(4, 128)) = 48
        _StochasticTilingStrength("Stochastic Tiling Strength", Range(0, 1)) = 0.65
        _StormRainDripAmplitude("Storm Rain Drip Amplitude", Range(0, 0.025)) = 0.004
        _StormRainDripTiling("Storm Rain Drip Tiling", Range(0.5, 16)) = 5
        _StormRainDripSpeed("Storm Rain Drip Speed", Range(0, 8)) = 1.8
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
        _WreckSiltStrength("Wreck Silt Strength", Range(0, 1)) = 0.36
        _WreckRustStrength("Wreck Rust Strength", Range(0, 1)) = 0.82
        _WreckSiltTint("Wreck Silt Tint", Color) = (0.23, 0.28, 0.26, 1)
        _WreckRustTint("Heavy Orange Rust", Color) = (0.86, 0.28, 0.055, 1)
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
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        StructuredBuffer<float4x4> _HectonWreckMatrices;
        StructuredBuffer<float> _HectonWreckAges;
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
            float _MicroNormalStrength;
            float _MicroNormalTiling;
            float _StochasticTilingStrength;
            float _StormRainDripAmplitude;
            float _StormRainDripTiling;
            float _StormRainDripSpeed;
            float _Cutoff;
            float _DepthBias;
            float _WreckSiltStrength;
            float _WreckRustStrength;
            float4 _WreckSiltTint;
            float4 _WreckRustTint;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 viewDirWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            half age01 : TEXCOORD5;
        };

        float4x4 ResolveWreckMatrix(uint instanceID)
        {
            return _HectonWreckMatrices[instanceID];
        }

        half ResolveWreckAge(uint instanceID)
        {
            return (half)saturate(_HectonWreckAges[instanceID]);
        }

        float3 SafeNormalize3(float3 value)
        {
            float lenSq = dot(value, value);
            return lenSq > 0.0001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
        }

        float3 TransformWreckNormal(float4x4 instanceMatrix, float3 normalOS)
        {
            return SafeNormalize3(mul((float3x3)instanceMatrix, normalOS));
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float4x4 instanceMatrix = ResolveWreckMatrix(input.instanceID);
            float4 positionWS = mul(instanceMatrix, float4(HectonCoreLitSanitizePositionOS(input.positionOS.xyz), 1.0));
            output.normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            output.positionWS = HectonCoreLitApplySubmarineCrushDepth(positionWS.xyz, output.normalWS);
            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(output.positionWS, output.normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, _DepthBias, 1.0);
            output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(output.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.age01 = ResolveWreckAge(input.instanceID);
            return output;
        }

        half4 SampleWreckSurface(float2 uv)
        {
            return HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), uv, uv * 0.031, (half)_StochasticTilingStrength) * _BaseColor;
        }

        half4 SamplePackedMask(float2 uv)
        {
            return HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), uv, uv * 0.031, (half)_StochasticTilingStrength);
        }

        half3 EvaluateWreckLighting(
            float3 positionWS,
            half3 normalWS,
            half3 viewDirWS,
            half3 albedo,
            half metallic,
            half smoothness,
            half ambientOcclusion)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = SampleSH(normalWS) * albedo * ambientOcclusion * caveAmbientFactor;
            half specularStrength = lerp(0.04h, 0.22h, metallic);
            half specularPower = lerp(16.0h, 96.0h, smoothness);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = SafeNormalize3(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadow(positionWS, normalWS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);

            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 surface = SampleWreckSurface(input.uv);
        #if defined(_ALPHATEST_ON)
            clip(surface.a - _Cutoff);
        #endif

            half4 packedMask = SamplePackedMask(input.uv);
            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;

            half3 normalWS = SafeNormalize3(input.normalWS);
            normalWS = HectonCoreLitApplyTripleDetailMicroNormals(input.positionWS, normalWS, (half)_MicroNormalStrength, (half)_MicroNormalTiling, 2.0h);
            half3 viewDirWS = SafeNormalize3(input.viewDirWS);
            half3 albedo = surface.rgb;
            HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);
            half edgeWearMask = saturate((1.0h - ambientOcclusion) * 0.7h + (1.0h - smoothness) * 0.35h);
            HectonCoreLitApplyProceduralRustSilt(
                input.positionWS,
                normalWS,
                normalWS,
                edgeWearMask,
                input.age01,
                (half)_WreckSiltStrength,
                (half)_WreckRustStrength,
                half3(_WreckSiltTint.rgb),
                half3(_WreckRustTint.rgb),
                albedo,
                metallic,
                smoothness);
            HectonCoreLitApplyEnvironmentalWear(input.positionWS, normalWS, (half)_EnvironmentalWear, (half3)_RustSaltColor.rgb, albedo, metallic, smoothness);
            half3 litColor = EvaluateWreckLighting(
                input.positionWS,
                normalWS,
                viewDirWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 emission = _EmissionColor.rgb * emissionMask;
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            float4x4 instanceMatrix = ResolveWreckMatrix(input.instanceID);
            float4 positionWS = mul(instanceMatrix, float4(HectonCoreLitSanitizePositionOS(input.positionOS.xyz), 1.0));
            float3 normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            positionWS.xyz = HectonCoreLitApplyStormRainDripVertexRipple(positionWS.xyz, normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
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
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                half alpha = SampleWreckSurface(input.uv).a;
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
                return 0.0h;
            }
            ENDHLSL
        }
    }
}

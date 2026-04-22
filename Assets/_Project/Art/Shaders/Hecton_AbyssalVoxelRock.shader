Shader "Hecton8/Environment/Hecton_AbyssalVoxelRock"
{
    Properties
    {
        [MainTexture] _Base_Map ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _Normal_Map ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _Mask_Map ("Mask Map", 2D) = "white" {}
        _Instance_Color ("Instance Color", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling", Range(0.01, 4)) = 0.2
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Mask Map", 2D) = "white" {}
        _SkirtSandTint ("Skirt Sand Tint", Color) = (0.42, 0.38, 0.31, 1)
        _SkirtBlendContrast ("Skirt Blend Contrast", Range(0.1, 4)) = 1.4
        _CutScarColor ("Cut Scar Color", Color) = (1.0, 0.42, 0.12, 1)
        _CutScarWarmColor ("Cut Scar Warm Color", Color) = (0.42, 0.06, 0.03, 1)
        _CutScarCharColor ("Cut Scar Char Color", Color) = (0.06, 0.03, 0.02, 1)
        _CutScarEmission ("Cut Scar Emission", Range(0, 8)) = 1.8
        _CutScarSharpness ("Cut Scar Sharpness", Range(0.5, 8)) = 2.2
        _CutScarDarkening ("Cut Scar Darkening", Range(0, 1)) = 0.24
        _ShadowScarErosion ("Shadow Scar Erosion", Range(0, 0.35)) = 0.14
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
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

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Instance_Color;
            float4 _SkirtSandTint;
            float4 _CutScarColor;
            float4 _CutScarWarmColor;
            float4 _CutScarCharColor;
            float _Tiling;
            float _Smoothness;
            float _SkirtBlendContrast;
            float _CutScarEmission;
            float _CutScarSharpness;
            float _CutScarDarkening;
            float _ShadowScarErosion;
        CBUFFER_END

        float4 _SargassumCutMaskWorldRect;
        float4 _HectonFloatingOriginOffset;
        float _SargassumCutMaskActive;
        float _HectonVoxelSSAOActive;
        int _HectonRecentCutHeatCount;
        float3 _LightDirection;

        #define HECTON_RECENT_CUT_HEAT_MAX 16
        float4 _HectonRecentCutHeatPositionRadius[HECTON_RECENT_CUT_HEAT_MAX];
        float4 _HectonRecentCutHeatStrengthTime[HECTON_RECENT_CUT_HEAT_MAX];

        TEXTURE2D(_Base_Map);
        SAMPLER(sampler_Base_Map);
        TEXTURE2D(_Normal_Map);
        SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_Mask_Map);
        SAMPLER(sampler_Mask_Map);
        TEXTURE2D(_HectonVoxelSSAOTex);
        SAMPLER(sampler_HectonVoxelSSAOTex);
        TEXTURE2D(_SargassumCutMaskRT);
        SAMPLER(sampler_SargassumCutMaskRT);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
        };

        struct SurfaceVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half3 viewDirWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            half skirtAlpha : TEXCOORD4;
        };

        struct ClipVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half skirtAlpha : TEXCOORD1;
        };

        struct ShadowVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half skirtAlpha : TEXCOORD2;
        };

        half3 SafeNormalize3(half3 value)
        {
            half lenSq = dot(value, value);
            return lenSq > 0.0001h ? value * rsqrt(lenSq) : half3(0.0h, 1.0h, 0.0h);
        }

        float Hash21(float2 value)
        {
            return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
        }

        float ResolveDitherNoise(float2 positionCS)
        {
            float2 pixel = floor(positionCS);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float3 ResolveSamplePositionWS(float3 positionWS)
        {
            return positionWS + _HectonFloatingOriginOffset.xyz;
        }

        half3 ComputeTriplanarWeights(half3 normalWS)
        {
            half3 weights = saturate(abs(normalWS));
            half weightSum = max(weights.x + weights.y + weights.z, 0.0001h);
            return weights / weightSum;
        }

        half4 SampleTriplanarColor(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 weights)
        {
            float tiling = max(_Tiling, 0.0001);
            half4 ySample = SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling);
            if (weights.y >= 0.999h)
                return ySample;

            half4 xSample = SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * tiling);
            half4 zSample = SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * tiling);
            return xSample * weights.x + ySample * weights.y + zSample * weights.z;
        }

        half3 SampleTriplanarNormal(float3 positionWS, half3 baseNormalWS, half3 weights)
        {
            half3 normalSign = sign(baseNormalWS);
            float tiling = max(_Tiling, 0.0001);

            half3 normalY = UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, positionWS.xz * tiling));
            normalY = half3(normalY.x, normalY.z * normalSign.y, normalY.y);
            if (weights.y >= 0.999h)
                return SafeNormalize3(normalY);

            half3 normalX = UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, positionWS.zy * tiling));
            normalX = half3(normalX.z * normalSign.x, normalX.y, normalX.x);

            half3 normalZ = UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, positionWS.xy * tiling));
            normalZ = half3(normalZ.x, normalZ.y, normalZ.z * normalSign.z);

            return SafeNormalize3(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
        }

        half EvaluateGlobalCutMask(float3 positionWS)
        {
            if (_SargassumCutMaskActive < 0.5)
                return 0.0h;

            float2 uv = float2(
                (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return 0.0h;

            return SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r;
        }

        half ResolveSkirtCoverage(half vertexAlpha, float2 positionCS)
        {
            half shapedAlpha = saturate(pow(saturate(vertexAlpha), max(_SkirtBlendContrast, 0.1)));
            clip(shapedAlpha - ResolveDitherNoise(positionCS));
            return shapedAlpha;
        }

        half ResolveShadowCoverage(half vertexAlpha, float2 positionCS, half scarMask, float3 positionWS)
        {
            half shapedAlpha = saturate(pow(saturate(vertexAlpha), max(_SkirtBlendContrast, 0.1)));
            float scarNoise = Hash21(floor(positionWS.xz * 3.0 + scarMask * 19.0));
            half erosion = scarMask * _ShadowScarErosion * (1.0h - scarNoise);
            half coverage = saturate(shapedAlpha - erosion);
            clip(coverage - ResolveDitherNoise(positionCS));
            return coverage;
        }

        half SampleVoxelAmbientOcclusion(float4 positionCS)
        {
            if (_HectonVoxelSSAOActive < 0.5)
                return 1.0h;

            float2 screenUV = saturate(positionCS.xy * _ScaledScreenParams.zw);
            return SAMPLE_TEXTURE2D_LOD(_HectonVoxelSSAOTex, sampler_HectonVoxelSSAOTex, screenUV, 0).r;
        }

        void EvaluateRecentCutHeat(float3 positionWS, out half heatMask, out half age01)
        {
            heatMask = 0.0h;
            age01 = 1.0h;

            [unroll]
            for (int i = 0; i < HECTON_RECENT_CUT_HEAT_MAX; i++)
            {
                if (i >= _HectonRecentCutHeatCount)
                    break;

                float4 positionRadius = _HectonRecentCutHeatPositionRadius[i];
                float4 strengthTime = _HectonRecentCutHeatStrengthTime[i];
                float lifetime = max(strengthTime.z, 0.01);
                float elapsed = _Time.y - strengthTime.y;
                if (elapsed < 0.0 || elapsed >= lifetime)
                    continue;

                float radius = max(positionRadius.w, 0.05);
                float3 delta = positionWS - positionRadius.xyz;
                float distanceToStamp = length(delta);
                if (distanceToStamp >= radius)
                    continue;

                float radial = saturate(1.0 - distanceToStamp / radius);
                radial *= radial;
                float localAge01 = saturate(elapsed / lifetime);
                float thermal = strengthTime.x * radial * (1.0 - localAge01);
                if (thermal <= heatMask)
                    continue;

                heatMask = thermal;
                age01 = localAge01;
            }
        }

        half3 EvaluateLighting(float3 positionWS, half3 normalWS, half3 viewDirWS, half3 albedo, half metallic, half smoothness, half occlusion)
        {
            half3 color = SampleSH(normalWS) * albedo * occlusion;
            half specularStrength = lerp(0.04h, 0.22h, metallic);
            half specularPower = lerp(16.0h, 96.0h, smoothness);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = SafeNormalize3(mainLight.direction);
            half NdotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            color += (albedo * NdotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);

            #if defined(_ADDITIONAL_LIGHTS)
            uint lightCount = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(lightCount)
                Light light = GetAdditionalLight(lightIndex, positionWS);
                half3 additionalDir = SafeNormalize3(light.direction);
                half additionalNdotL = saturate(dot(normalWS, additionalDir));
                half3 additionalHalfDir = SafeNormalize3(additionalDir + viewDirWS);
                half additionalSpecular = pow(saturate(dot(normalWS, additionalHalfDir)), specularPower) * smoothness * specularStrength;
                color += (albedo * additionalNdotL + additionalSpecular) * light.color * (light.distanceAttenuation * light.shadowAttenuation);
            LIGHT_LOOP_END
            #endif

            return color;
        }

        SurfaceVaryings Vert(Attributes input)
        {
            SurfaceVaryings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = normalize(normalInputs.normalWS);
            output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(positionInputs.positionWS));
            output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            output.skirtAlpha = saturate(input.color.a);
            return output;
        }

        ClipVaryings DepthVert(Attributes input)
        {
            ClipVaryings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.skirtAlpha = saturate(input.color.a);
            return output;
        }

        ShadowVaryings ShadowVert(Attributes input)
        {
            ShadowVaryings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionWS = positionInputs.positionWS;
            output.normalWS = normalize(normalInputs.normalWS);
            output.skirtAlpha = saturate(input.color.a);
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, output.normalWS, _LightDirection));

            #if UNITY_REVERSED_Z
            output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
            output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            half4 Frag(SurfaceVaryings input) : SV_Target
            {
                half skirtCoverage = ResolveSkirtCoverage(input.skirtAlpha, input.positionCS.xy);
                half3 baseNormalWS = normalize(input.normalWS);
                half3 triplanarWeights = ComputeTriplanarWeights(baseNormalWS);
                float3 samplePositionWS = ResolveSamplePositionWS(input.positionWS);
                half3 triplanarNormalWS = SampleTriplanarNormal(samplePositionWS, baseNormalWS, triplanarWeights);
                half3 normalWS = SafeNormalize3(baseNormalWS + triplanarNormalWS);

                half4 baseSample = SampleTriplanarColor(TEXTURE2D_ARGS(_Base_Map, sampler_Base_Map), samplePositionWS, triplanarWeights);
                half4 maskSample = SampleTriplanarColor(TEXTURE2D_ARGS(_Mask_Map, sampler_Mask_Map), samplePositionWS, triplanarWeights);
                half cutMask = EvaluateGlobalCutMask(input.positionWS);
                half scarMask = pow(saturate(cutMask), max(_CutScarSharpness, 0.5h));
                half recentHeatMask;
                half recentHeatAge01;
                EvaluateRecentCutHeat(input.positionWS, recentHeatMask, recentHeatAge01);
                half skirtBlend = 1.0h - skirtCoverage;

                half3 albedo = baseSample.rgb * _Instance_Color.rgb;
                albedo = lerp(albedo, _SkirtSandTint.rgb, skirtBlend * 0.72h);
                albedo *= lerp(1.0h, 1.0h - _CutScarDarkening, scarMask);
                albedo = lerp(albedo, _CutScarCharColor.rgb, scarMask * 0.38h);

                half3 thermalColor = lerp(_CutScarWarmColor.rgb, _CutScarColor.rgb, saturate(1.0h - recentHeatAge01 * 0.9h));
                albedo = lerp(albedo, thermalColor, recentHeatMask * 0.18h);

                half metallic = 0.0h;
                half smoothness = saturate(lerp(_Smoothness, 0.88h, scarMask * 0.65h));
                half ambientOcclusion = saturate(maskSample.g) * SampleVoxelAmbientOcclusion(input.positionCS);

                half3 litColor = EvaluateLighting(input.positionWS, normalWS, normalize(input.viewDirWS), albedo, metallic, smoothness, ambientOcclusion);
                half thermalEmission = _CutScarEmission * recentHeatMask * lerp(0.22h, 1.0h, saturate(1.0h - recentHeatAge01));
                half3 emission = (_CutScarWarmColor.rgb * (_CutScarEmission * scarMask * 0.12h)) + (thermalColor * thermalEmission);
                half3 finalColor = MixFog(litColor + emission, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                half scarMask = pow(saturate(EvaluateGlobalCutMask(input.positionWS)), max(_CutScarSharpness, 0.5h));
                ResolveShadowCoverage(input.skirtAlpha, input.positionCS.xy, scarMask, input.positionWS);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            half4 DepthFrag(ClipVaryings input) : SV_Target
            {
                ResolveSkirtCoverage(input.skirtAlpha, input.positionCS.xy);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

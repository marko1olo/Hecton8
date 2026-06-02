Shader "Hecton8/Bakers/MraoAtlasLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _MraoMap("M.R.A.O. (R Metallic G Roughness B AO A Emission)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0.25, 0.55, 0.75, 1)
        _MetallicScale("Metallic Scale", Range(0, 1)) = 0
        _RoughnessScale("Roughness Scale", Range(0, 1)) = 1
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _EmissionStrength("Emission Strength", Range(0, 4)) = 0
        _NormalScale("Normal Scale", Range(0, 2)) = 1
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0
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
        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MraoMap);
        SAMPLER(sampler_MraoMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _MetallicScale;
            float _RoughnessScale;
            float _OcclusionStrength;
            float _EmissionStrength;
            float _NormalScale;
            float _Cutoff;
            float _DepthBias;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct MraoSurface1605
        {
            half metallic;
            half roughness;
            half ambientOcclusion;
            half emissionMask;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(HectonCoreLitSanitizePositionOS(input.positionOS.xyz));
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionWS = positionInputs.positionWS;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(positionInputs.positionCS, _DepthBias, 1.0);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        MraoSurface1605 DecodeMrao1605(half4 packedMrao)
        {
            // 1605 M.R.A.O. packing: R=Metallic, G=Roughness, B=Ambient Occlusion, A=Emissive.
            MraoSurface1605 surface;
            surface.metallic = saturate(packedMrao.r * (half)_MetallicScale);
            surface.roughness = saturate(packedMrao.g * (half)_RoughnessScale);
            surface.ambientOcclusion = saturate(lerp(1.0h, packedMrao.b, (half)_OcclusionStrength));
            surface.emissionMask = saturate(packedMrao.a);
            return surface;
        }

        float3 BuildMraoFallbackTangent(float3 normalWS)
        {
            return abs(normalWS.y) < 0.999
                ? HectonCoreLitSafeNormalize(cross(float3(0.0, 1.0, 0.0), normalWS))
                : float3(1.0, 0.0, 0.0);
        }

        float3x3 BuildMraoTangentToWorld(float3 normalWS, float4 tangentWS)
        {
            float3 safeNormal = HectonCoreLitSafeNormalize(normalWS);
            float3 projectedTangent = tangentWS.xyz - safeNormal * dot(safeNormal, tangentWS.xyz);
            float tangentLenSq = dot(projectedTangent, projectedTangent);
            float3 safeTangent = isfinite(tangentLenSq) && tangentLenSq > 0.0001
                ? projectedTangent * rsqrt(tangentLenSq)
                : BuildMraoFallbackTangent(safeNormal);
            float handedness = tangentWS.w >= 0.0 ? 1.0 : -1.0;
            float3 safeBitangent = HectonCoreLitSafeNormalize(cross(safeNormal, safeTangent) * handedness);
            return float3x3(safeTangent, safeBitangent, safeNormal);
        }

        half3 ResolveNormalWS(Varyings input)
        {
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), (half)_NormalScale);
            float3x3 tangentToWorld = BuildMraoTangentToWorld(input.normalWS, input.tangentWS);
            return (half3)HectonCoreLitSafeNormalize(TransformTangentToWorld(normalTS, tangentToWorld));
        }

        half3 EvaluateLitColor(
            float3 positionWS,
            float4 positionCS,
            half3 normalWS,
            half3 albedo,
            MraoSurface1605 surface)
        {
            half smoothness = saturate(1.0h - surface.roughness);
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = SampleSH(normalWS) * albedo * surface.ambientOcclusion * caveAmbientFactor;

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = (half3)mainLight.direction;
            half nDotL = saturate(dot(normalWS, lightDir));
            half specularStrength = lerp(0.04h, 0.22h, surface.metallic);
            half specularEnergy = smoothness * specularStrength;
            half specularBase = nDotL * nDotL;
            half specular = specularBase * specularBase * specularEnergy * step(0.0001h, nDotL * specularEnergy);
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow * contactShadow);
            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 baseSample = SampleBase(input.uv);
        #if defined(_ALPHATEST_ON)
            clip(baseSample.a - _Cutoff);
        #endif
            MraoSurface1605 surface = DecodeMrao1605(SAMPLE_TEXTURE2D(_MraoMap, sampler_MraoMap, input.uv));
            half3 normalWS = ResolveNormalWS(input);
            half3 lit = EvaluateLitColor(input.positionWS, input.positionCS, normalWS, baseSample.rgb, surface);
            half3 emission = _EmissionColor.rgb * surface.emissionMask * (half)_EmissionStrength;
            half3 finalColor = HectonCoreLitApplyNoirFog(lit + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            VertexPositionInputs positionInputs = GetVertexPositionInputs(HectonCoreLitSanitizePositionOS(input.positionOS.xyz));
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, normalWS, lightDirectionWS));
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_ALPHATEST_ON)
                half alpha = SampleBase(input.uv).a;
                clip(alpha - _Cutoff);
            #endif
                return 0.0h;
            }
            ENDHLSL
        }
    }
}

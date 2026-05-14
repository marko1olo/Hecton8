Shader "Hecton8/VFX/CarveDebrisIndirect"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.31, 0.29, 0.24, 1)
        _EdgeColor("Fresh Chip Edge", Color) = (0.66, 0.58, 0.45, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            StructuredBuffer<float4> _CarveDebrisRead;
            StructuredBuffer<float4> _CarveDebrisVelocityRead;
            StructuredBuffer<uint> _CarveDebrisVisibleIndices;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _Metallic;
                half _Smoothness;
                float4 _CarveDebrisMaterialParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half life : TEXCOORD4;
                half edgeMask : TEXCOORD5;
                half impactMask : TEXCOORD6;
            };

            float Hash11(uint value)
            {
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) * 0.000000059604644775390625;
            }

            void BuildDebrisBasis(uint particleIndex, out float3 rightWS, out float3 upWS, out float3 forwardWS)
            {
                float3 rawForward = float3(
                    Hash11(particleIndex ^ 0x9E3779B9u) * 2.0 - 1.0,
                    Hash11(particleIndex ^ 0x85EBCA6Bu) * 0.7 - 0.35,
                    Hash11(particleIndex ^ 0xC2B2AE35u) * 2.0 - 1.0);
                forwardWS = HectonCoreLitSafeNormalize(rawForward);
                float3 basisUp = abs(forwardWS.y) < 0.92 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                rightWS = HectonCoreLitSafeNormalize(cross(basisUp, forwardWS));
                upWS = HectonCoreLitSafeNormalize(cross(forwardWS, rightWS));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                uint particleIndex = _CarveDebrisVisibleIndices[input.instanceID];
                float4 particle = _CarveDebrisRead[particleIndex];
                half life = (half)saturate(particle.w);
                float randomScale = lerp(_CarveDebrisMaterialParams.x, _CarveDebrisMaterialParams.y, Hash11(particleIndex));
                float scale = max(0.001, randomScale * saturate(particle.w * 1.8));

                float3 rightWS;
                float3 upWS;
                float3 forwardWS;
                BuildDebrisBasis(particleIndex, rightWS, upWS, forwardWS);

                float3 localPosition = input.positionOS.xyz * scale;
                float3 positionWS = particle.xyz +
                    rightWS * localPosition.x +
                    upWS * localPosition.y +
                    forwardWS * localPosition.z;
                float3 normalWS = HectonCoreLitSafeNormalize(
                    rightWS * input.normalOS.x +
                    upWS * input.normalOS.y +
                    forwardWS * input.normalOS.z);

                output.positionWS = HectonCoreLitSanitizePositionWS(positionWS);
                output.normalWS = (half3)normalWS;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.viewDirWS = (half3)HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                output.life = life;
                output.edgeMask = (half)saturate(abs(input.normalOS.y) * 0.35 + Hash11(particleIndex ^ 0xC2B2AE35u) * 0.25);
                output.impactMask = 0.0h;
                [branch]
                if (_CarveDebrisMaterialParams.w > 0.5)
                {
                    float3 velocityWS = _CarveDebrisVelocityRead[particleIndex].xyz;
                    float speedSq = dot(velocityWS, velocityWS);
                    output.impactMask = (half)(saturate(speedSq * 0.055) * saturate(life * 1.2));
                }

                return output;
            }

            half3 EvaluateDebrisLighting(float3 positionWS, float4 positionCS, half3 normalWS, half3 viewDirWS, half3 albedo)
            {
                half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
                half3 color = SampleSH(normalWS) * albedo * caveAmbientFactor;
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDir = (half3)HectonCoreLitSafeNormalize(mainLight.direction);
                half nDotL = saturate(dot(normalWS, lightDir));
                half3 halfDir = (half3)HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                half specular = specularBase * specularBase;
                specular *= specular * lerp(0.035h, 0.18h, _Smoothness);
                half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
                color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow);
                color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fadeOut = (half)(1.0 - saturate(input.life * 1.15));
                HectonCoreLitClipDitheredTransparencyFade(fadeOut, input.positionCS);
                half3 normalWS = (half3)HectonCoreLitSafeNormalize(input.normalWS);
                half3 viewDirWS = (half3)HectonCoreLitSafeNormalize(input.viewDirWS);
                half edgeMask = saturate(input.edgeMask + input.impactMask * 0.35h);
                half3 albedo = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeMask);
                half3 lit = EvaluateDebrisLighting(input.positionWS, input.positionCS, normalWS, viewDirWS, albedo);
                half3 finalColor = HectonCoreLitApplyNoirFog(lit, input.fogFactor, input.positionWS);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

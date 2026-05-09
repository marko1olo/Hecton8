Shader "Hecton8/Fauna/LeviathanOrganic"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        [HDR] _SssColor("SSS Color", Color) = (0.44, 0.86, 0.92, 1)
        [HDR] _WoundColor("Wound Color", Color) = (0.42, 0.05, 0.04, 1)
        [HDR] _WoundBurnColor("Wound Burn Color", Color) = (0.10, 0.01, 0.01, 1)
        _NormalScale("Normal Scale", Range(0, 2)) = 1.0
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.45
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EmissionStrength("Emission Strength", Range(0, 8)) = 1.0
        _FaunaBiolumDim("Fauna Biolum Dim", Range(0, 1)) = 1.0
        _DeathDitherFade("Death Dither Fade", Range(0, 1)) = 0.0
        _CorpseBloatAge01("Corpse Bloat Age 01", Range(0, 1)) = 0.0
        _CorpseBloatStartTime("Corpse Bloat Start Time", Float) = -1.0
        _CorpseBloatDuration("Corpse Bloat Duration", Range(1, 120)) = 60.0
        _CorpseBloatStrength("Corpse Bloat Strength", Range(0, 0.35)) = 0.08
        _TailSwayStrength("Tail Sway Strength", Range(0, 0.35)) = 0.045
        _TailSwaySpeed("Tail Sway Speed", Range(0, 16)) = 4.6
        _TailSwayPhase("Tail Sway World-Y Phase", Range(0, 8)) = 1.35
        _TailSwayMaskPower("Tail Sway Mask Power", Range(0.25, 6)) = 1.65
        _SssDistortion("SSS Distortion", Range(0, 2)) = 0.48
        _SssPower("SSS Power", Range(0.1, 16)) = 3.8
        _SssScale("SSS Scale", Range(0, 4)) = 1.15
        _WetnessStrength("Wetness Strength", Range(0, 2)) = 1.0
        _WetnessSmoothnessBoost("Wetness Smoothness Boost", Range(0, 1)) = 0.28
        _WetnessNormalWobble("Wetness Normal Wobble", Range(0, 1)) = 0.08
        _WetnessVelocityWS("Wetness Velocity WS", Vector) = (0, 0, 0, 0)
        _WoundSmoothnessDrop("Wound Smoothness Drop", Range(0, 1)) = 0.34
        _WoundEmissionBoost("Wound Emission Boost", Range(0, 4)) = 0.55
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _SssColor;
            float4 _WoundColor;
            float4 _WoundBurnColor;
            float4 _WetnessVelocityWS;
            float _NormalScale;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EmissionStrength;
            float _FaunaBiolumDim;
            float _DeathDitherFade;
            float _CorpseBloatAge01;
            float _CorpseBloatStartTime;
            float _CorpseBloatDuration;
            float _CorpseBloatStrength;
            float _TailSwayStrength;
            float _TailSwaySpeed;
            float _TailSwayPhase;
            float _TailSwayMaskPower;
            float _SssDistortion;
            float _SssPower;
            float _SssScale;
            float _WetnessStrength;
            float _WetnessSmoothnessBoost;
            float _WetnessNormalWobble;
            float _WoundSmoothnessDrop;
            float _WoundEmissionBoost;
            float _DepthBias;
        CBUFFER_END

        float _HectonCreatureWoundCount;
        float4 _HectonCreatureWounds[8];
        float4x4 _HectonCreatureWoundOwnerWorldToLocal;
        float4 _HectonCreatureWoundOwnerSphere;
        float _GlobalOceanPanic;
        float4 _GlobalOceanPanicColor;
        float4 _HectonSonarPrimaryPulse;
        float4 _HectonSonarEchoPulse;
        float4 _HectonSonarVisualParams;
        float4 _HectonSonarEchoParams;
        float4 _HectonSonarColor;
        float _HectonSonarNoirHideDistance;
        float _SonarActive;

        struct Attributes
        {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            UNITY_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 positionOS : TEXCOORD6;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            half fogFactor : TEXCOORD5;
        };

        float ApproximateMagnitude3D(float3 value)
        {
            float3 delta = abs(value);
            float maxAxis = max(delta.x, max(delta.y, delta.z));
            float minAxis = min(delta.x, min(delta.y, delta.z));
            float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375 + minAxis * 0.125;
        }

        float3 NormalizeApprox3D(float3 value)
        {
            return value * rcp(max(0.0001, ApproximateMagnitude3D(value)));
        }

        float CheapSignedWave(float phase)
        {
            float t = frac(phase * 0.15915494 + 0.25);
            return 1.0 - abs(t * 2.0 - 1.0) * 2.0;
        }

        float3x3 BuildTangentToWorld(float3 normalWS, float4 tangentWS)
        {
            float3 tangent = NormalizeApprox3D(tangentWS.xyz);
            float3 bitangent = NormalizeApprox3D(cross(normalWS, tangent) * tangentWS.w);
            return float3x3(tangent, bitangent, normalWS);
        }

        float3 ApplyWetnessNormalWobble(float3 normalWS, float3 positionWS, float velocityMagnitudeSq)
        {
            float3 velocityWS = _WetnessVelocityWS.xyz;
            if (velocityMagnitudeSq <= 0.000001 || _WetnessNormalWobble <= 0.0001 || _WetnessStrength <= 0.0001)
                return normalWS;

            float3 velocityDir = NormalizeApprox3D(velocityWS);
            float velocityGate = saturate(velocityMagnitudeSq * 0.0025);
            float wobblePhase = _Time.y * (2.0 + velocityGate * 4.0) + dot(positionWS, velocityDir) * 0.12;
            float3 wobbleAxis = NormalizeApprox3D(cross(normalWS, velocityDir + float3(0.0, 0.18, 0.0)));
            float wobbleStrength = velocityGate * _WetnessNormalWobble * _WetnessStrength;
            return NormalizeApprox3D(normalWS + wobbleAxis * (CheapSignedWave(wobblePhase) * wobbleStrength));
        }

        float3 ApplyFaunaVertexPresentation(float3 positionOS, float3 normalOS)
        {
            float3 worldPos = TransformObjectToWorld(positionOS);
            float tailMaskBase = saturate(-positionOS.z);
            float tailMaskSquared = tailMaskBase * tailMaskBase;
            float tailMaskQuartic = tailMaskSquared * tailMaskSquared;
            float tailMask = lerp(tailMaskBase, tailMaskQuartic, saturate((_TailSwayMaskPower - 1.0) * 0.33333334));
            float tailWave = CheapSignedWave(_Time.y * _TailSwaySpeed + worldPos.y * _TailSwayPhase + worldPos.x * 13.37);
            positionOS.x += tailWave * _TailSwayStrength * tailMask;

            float timedBloat01 = saturate((_Time.y - _CorpseBloatStartTime) / max(_CorpseBloatDuration, 0.001)) * step(0.0, _CorpseBloatStartTime);
            float bloat01 = max(saturate(_CorpseBloatAge01), timedBloat01);
            positionOS += normalOS * (bloat01 * bloat01 * _CorpseBloatStrength);
            return positionOS;
        }

        half2 EvaluateWoundMask(float3 positionWS)
        {
            if (_HectonCreatureWoundCount < 0.5)
                return half2(0.0h, 0.0h);

            float3 toOwner = positionWS - _HectonCreatureWoundOwnerSphere.xyz;
            float ownerRadius = max(_HectonCreatureWoundOwnerSphere.w, 0.001);
            if (dot(toOwner, toOwner) > ownerRadius * ownerRadius)
                return half2(0.0h, 0.0h);

            float3 ownerLocalPosition = mul(_HectonCreatureWoundOwnerWorldToLocal, float4(positionWS, 1.0)).xyz;
            half woundMask = 0.0h;
            half burnMask = 0.0h;
            [unroll]
            for (int woundIndex = 0; woundIndex < 8; woundIndex++)
            {
                if ((float)woundIndex >= _HectonCreatureWoundCount)
                    break;

                float4 wound = _HectonCreatureWounds[woundIndex];
                float woundRadius = max(wound.w, 0.001);
                float3 woundDelta = ownerLocalPosition - wound.xyz;
                float woundRadiusSq = woundRadius * woundRadius;
                float woundDistanceSq = dot(woundDelta, woundDelta);
                if (woundDistanceSq > woundRadiusSq)
                    continue;

                float coreRadius = max(woundRadius * 0.45, 0.001);
                half woundContribution = saturate(1.0h - (half)(woundDistanceSq / woundRadiusSq));
                half coreContribution = saturate(1.0h - (half)(woundDistanceSq / (coreRadius * coreRadius)));
                woundMask = max(woundMask, woundContribution * woundContribution);
                burnMask = max(burnMask, coreContribution * coreContribution);
            }

            return half2(woundMask, burnMask);
        }

        half ResolveFaunaDither(float4 positionCS)
        {
            uint2 pixel = (uint2)positionCS.xy;
            uint hash = pixel.x * 1103515245u + pixel.y * 12345u + 0x9E3779B9u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (half)((hash & 255u) * (1.0 / 255.0));
        }

        float EvaluateLeviathanSonarBand(float4 pulse, float4 parameters, float3 positionWS)
        {
            float active = saturate(_SonarActive) * saturate(parameters.w);
            if (active <= 0.0001)
                return 0.0;

            float speed = max(parameters.x, 0.01);
            float maxRadius = max(parameters.y, 0.01);
            float bandWidth = max(parameters.z, 0.05);
            float age = _Time.y - pulse.w;
            if (age <= 0.0)
                return 0.0;

            float radius = age * speed;
            float lifeMask = 1.0 - saturate((radius - maxRadius) / bandWidth);
            if (lifeMask <= 0.0001)
                return 0.0;

            float3 pulseDelta = positionWS - pulse.xyz;
            float distanceToPulseSq = dot(pulseDelta, pulseDelta);
            float outerBandRadius = radius + bandWidth;
            if (distanceToPulseSq > outerBandRadius * outerBandRadius)
                return 0.0;

            float innerBandRadius = max(radius - bandWidth, 0.0);
            if (innerBandRadius > 0.0 && distanceToPulseSq < innerBandRadius * innerBandRadius)
                return 0.0;

            float radiusSq = radius * radius;
            float bandSq = max((outerBandRadius * outerBandRadius) - (innerBandRadius * innerBandRadius), 0.001);
            float band = saturate(1.0 - abs(distanceToPulseSq - radiusSq) / bandSq);
            band = band * band * (3.0 - 2.0 * band);
            float cinematicFalloff = rcp(1.0 + distanceToPulseSq * 0.000016);
            return band * lifeMask * active * cinematicFalloff;
        }

        float EvaluateLeviathanSonarReveal(float3 positionWS)
        {
            float primary = EvaluateLeviathanSonarBand(_HectonSonarPrimaryPulse, _HectonSonarVisualParams, positionWS);
            float echo = EvaluateLeviathanSonarBand(_HectonSonarEchoPulse, _HectonSonarEchoParams, positionWS) * 0.72;
            return saturate(primary + echo);
        }

        void ClipLeviathanNoirSilhouette(float3 positionWS, float sonarReveal)
        {
            float hideEnabled = step(0.5, _HectonSonarNoirHideDistance);
            float3 cameraDelta = positionWS - _WorldSpaceCameraPos;
            float hideDistanceSq = _HectonSonarNoirHideDistance * _HectonSonarNoirHideDistance;
            float farNoirMask = hideEnabled * step(hideDistanceSq, dot(cameraDelta, cameraDelta));
            clip(lerp(1.0, sonarReveal - 0.012, farNoirMask));
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 deformedPositionOS = HectonCoreLitSanitizePositionOS(ApplyFaunaVertexPresentation(input.positionOS.xyz, input.normalOS));
            VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionWS = positionInputs.positionWS;
            output.positionOS = deformedPositionOS;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(positionInputs.positionCS, _DepthBias, 1.0);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half deathDitherFade = saturate((half)_DeathDitherFade);
            if (deathDitherFade > 0.001h)
                clip((1.0h - deathDitherFade) - ResolveFaunaDither(input.positionCS));

            float sonarReveal = EvaluateLeviathanSonarReveal(input.positionWS);
            ClipLeviathanNoirSilhouette(input.positionWS, sonarReveal);

            half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
            half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
            float3x3 tangentToWorld = BuildTangentToWorld(NormalizeApprox3D(input.normalWS), input.tangentWS);
            half3 normalWS = NormalizeApprox3D(TransformTangentToWorld(tangentNormal, tangentToWorld));
            float wetnessVelocityMagnitudeSq = dot(_WetnessVelocityWS.xyz, _WetnessVelocityWS.xyz);
            normalWS = ApplyWetnessNormalWobble(normalWS, input.positionWS, wetnessVelocityMagnitudeSq);

            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;
            half3 viewDirWS = NormalizeApprox3D(input.viewDirWS);
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(input.positionWS, normalWS);
            half2 woundMasks = EvaluateWoundMask(input.positionWS);
            half woundMask = woundMasks.x;
            half woundBurnMask = woundMasks.y;

            float wetnessSignal = saturate(wetnessVelocityMagnitudeSq * 0.0025) * _WetnessStrength;
            smoothness = saturate(smoothness + wetnessSignal * _WetnessSmoothnessBoost);
            smoothness = saturate(smoothness * (1.0h - woundMask * _WoundSmoothnessDrop));
            half3 woundColor = lerp(_WoundColor.rgb, _WoundBurnColor.rgb, woundBurnMask);
            surface.rgb = lerp(surface.rgb, woundColor, woundMask);

            half3 color = SampleSH(normalWS) * surface.rgb * ambientOcclusion * caveAmbientFactor;
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specularStrength = lerp(0.05h, 0.22h, metallic);
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (nDotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                half specular2 = specularBase * specularBase;
                half specular4 = specular2 * specular2;
                half specular8 = specular4 * specular4;
                half specular16 = specular8 * specular8;
                half specular32 = specular16 * specular16;
                half specular64 = specular32 * specular32;
                half specularLow = specular16 * specular8;
                half specularHigh = specular64 * specular32 * specular16;
                specular = lerp(specularLow, specularHigh, smoothness) * specularEnergy;
            }
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(input.positionWS, normalWS, mainLight.direction);
            color += (surface.rgb * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);

            half3 sss = HectonCoreLitEvaluateOrganicSss(
                viewDirWS,
                lightDir,
                normalWS,
                _SssColor.rgb,
                _SssDistortion,
                _SssPower,
                _SssScale);

            half3 caustics = HectonCoreLitEvaluateProjectedCausticsScattering(input.positionWS, normalWS) * surface.rgb;
            half faunaBiolumDim = saturate((half)_FaunaBiolumDim);
            half3 biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask;
            half3 woundEmission = woundColor * (woundMask * _WoundEmissionBoost);
            half oceanPanic = saturate((half)_GlobalOceanPanic);
            half3 panicEmissionColor = lerp(_EmissionColor.rgb, _GlobalOceanPanicColor.rgb, oceanPanic);
            half panicBlink = lerp(1.0h, lerp(0.35h, 1.45h, (half)step(0.5, frac(_Time.y * 7.0 + input.positionWS.y * 0.03))), oceanPanic);
            half3 emission = ((panicEmissionColor * (_EmissionStrength * emissionMask) * panicBlink) + biolum) * faunaBiolumDim + woundEmission;
            half sonarFresnelBase = saturate(1.0h - dot(normalWS, viewDirWS));
            half sonarFresnel = sonarFresnelBase * sonarFresnelBase;
            emission += half3(_HectonSonarColor.rgb) * ((half)sonarReveal * (0.65h + sonarFresnel * 1.8h));
            half3 finalColor = HectonCoreLitApplyNoirFog(color + caustics + emission + sss, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            float3 deformedPositionOS = HectonCoreLitSanitizePositionOS(ApplyFaunaVertexPresentation(input.positionOS.xyz, input.normalOS));
            VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, normalInputs.normalWS, lightDirectionWS));
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 deformedPositionOS = HectonCoreLitSanitizePositionOS(ApplyFaunaVertexPresentation(input.positionOS.xyz, input.normalOS));
                output.positionCS = GetShadowPositionHClip(input);
                output.positionWS = TransformObjectToWorld(deformedPositionOS);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half deathDitherFade = saturate((half)_DeathDitherFade);
                if (deathDitherFade > 0.001h)
                    clip((1.0h - deathDitherFade) - ResolveFaunaDither(input.positionCS));

                float sonarReveal = EvaluateLeviathanSonarReveal(input.positionWS);
                ClipLeviathanNoirSilhouette(input.positionWS, sonarReveal);
                return 0.0h;
            }
            ENDHLSL
        }
    }
}

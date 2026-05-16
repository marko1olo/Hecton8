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
        [HDR] _FaunaCamouflageTint("Fauna Camouflage Tint", Color) = (0.18, 0.28, 0.30, 1)
        _FaunaCamouflageParams("Fauna Camouflage Params", Vector) = (35, 0.00444444, 1.35, 0.18)
        _FaunaCamouflageStrength("Fauna Camouflage Strength", Range(0, 1)) = 0.55
        _DeathDitherFade("Death Dither Fade", Range(0, 1)) = 0.0
        _CorpseBloatAge01("Corpse Bloat Age 01", Range(0, 1)) = 0.0
        _CorpseBloatStartTime("Corpse Bloat Start Time", Float) = -1.0
        _CorpseBloatDuration("Corpse Bloat Duration", Range(1, 7200)) = 60.0
        _CorpseBloatStrength("Corpse Bloat Strength", Range(0, 0.35)) = 0.08
        _DecayAmount("Decay Amount", Range(0, 1)) = 0.0
        _HitFlash("Hit Flash", Range(0, 1)) = 0.0
        _FaunaMutationHueShift("Fauna Mutation Hue Shift", Range(0, 1)) = 0.0
        _FaunaMutationTwitch("Fauna Mutation Twitch", Range(0, 1)) = 0.0
        _HitFlashBloatStrength("Hit Flash Bloat Strength", Range(0, 0.12)) = 0.035
        [HDR] _HitFlashEmissionColor("Hit Flash Emission Color", Color) = (1.2, 0.12, 0.04, 1)
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
            float4 _FaunaCamouflageTint;
            float4 _FaunaCamouflageParams;
            float4 _HitFlashEmissionColor;
            float _NormalScale;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EmissionStrength;
            float _FaunaBiolumDim;
            float _FaunaCamouflageStrength;
            float _DeathDitherFade;
            float _CorpseBloatAge01;
            float _CorpseBloatStartTime;
            float _CorpseBloatDuration;
            float _CorpseBloatStrength;
            float _DecayAmount;
            float _HitFlash;
            float _FaunaMutationHueShift;
            float _FaunaMutationTwitch;
            float _HitFlashBloatStrength;
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
        float4 _GlobalBiolumStates[16];
        float4 _GlobalBiolumParams;
        float4 _GlobalBiolumClock;
        float4 _GlobalBiolumAupOffset;
        StructuredBuffer<float4x4> _H8LeviathanBones;
        float _H8LeviathanBoneCount;
        float _H8LeviathanIkTier;
        float _H8LeviathanTailWhip01;
        float _H8LeviathanSegmentLength;
        float _H8LeviathanGpuSkinning;

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
            half3 ambientSH : TEXCOORD7;
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

        float ResolveHitFlash01()
        {
            return smoothstep(0.0, 1.0, saturate(_HitFlash));
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

            float timedBloat01 = saturate((_Time.y - _CorpseBloatStartTime) * rcp(max(_CorpseBloatDuration, 0.001))) * step(0.0, _CorpseBloatStartTime);
            float bloat01 = max(saturate(_CorpseBloatAge01), timedBloat01);
            float decay01 = saturate(_DecayAmount);
            positionOS += normalOS * ((bloat01 * bloat01 * _CorpseBloatStrength) - (decay01 * decay01 * 0.035));
            float hitFlash01 = ResolveHitFlash01();
            positionOS += normalOS * (hitFlash01 * _HitFlashBloatStrength);
            float mutationTwitch = saturate(_FaunaMutationTwitch);
            if (mutationTwitch > 0.0001)
            {
                float twitchMask = 0.35 + tailMask * 0.65;
                float twitchWave = CheapSignedWave(_Time.y * lerp(18.0, 42.0, mutationTwitch) + worldPos.x * 11.73 + worldPos.z * 7.41);
                float lateralWave = CheapSignedWave(_Time.y * 31.0 + worldPos.y * 5.19 + worldPos.x * 3.67);
                positionOS += normalOS * (twitchWave * mutationTwitch * 0.035 * twitchMask);
                positionOS.x += lateralWave * mutationTwitch * 0.018 * tailMask;
            }
            return positionOS;
        }

        float3 ResolveLeviathanBoneCenterWS(float4x4 boneMatrix)
        {
            return mul(boneMatrix, float4(0.0, 0.0, 0.0, 1.0)).xyz;
        }

        float3 ResolveLeviathanBoneAxisWS(float4x4 boneMatrix, float3 axis)
        {
            return NormalizeApprox3D(mul((float3x3)boneMatrix, axis));
        }

        void ApplyLeviathanGpuSkinning(
            float3 sourcePositionOS,
            float3 presentedPositionOS,
            float3 sourceNormalOS,
            float3 sourceTangentOS,
            inout float3 positionWS,
            inout float3 normalWS,
            inout float3 tangentWS)
        {
            if (_H8LeviathanGpuSkinning < 0.5 || _H8LeviathanBoneCount < 1.5)
                return;

            int boneCount = min(max((int)_H8LeviathanBoneCount, 2), 20);
            float segmentLength = max(_H8LeviathanSegmentLength, 0.001);
            float bodyLength = segmentLength * max((float)(boneCount - 1), 1.0);
            float bodyT = saturate(-sourcePositionOS.z * rcp(bodyLength));
            float segment = bodyT * (float)(boneCount - 1);
            int boneAIndex = clamp((int)floor(segment), 0, boneCount - 1);
            int boneBIndex = min(boneAIndex + 1, boneCount - 1);
            float blend01 = saturate(segment - (float)boneAIndex);
            float4x4 boneA = _H8LeviathanBones[boneAIndex];
            float4x4 boneB = _H8LeviathanBones[boneBIndex];
            float3 centerWS = lerp(ResolveLeviathanBoneCenterWS(boneA), ResolveLeviathanBoneCenterWS(boneB), blend01);
            float3 rightWS = NormalizeApprox3D(lerp(ResolveLeviathanBoneAxisWS(boneA, float3(1.0, 0.0, 0.0)), ResolveLeviathanBoneAxisWS(boneB, float3(1.0, 0.0, 0.0)), blend01));
            float3 upWS = NormalizeApprox3D(lerp(ResolveLeviathanBoneAxisWS(boneA, float3(0.0, 1.0, 0.0)), ResolveLeviathanBoneAxisWS(boneB, float3(0.0, 1.0, 0.0)), blend01));
            float3 forwardWS = NormalizeApprox3D(lerp(ResolveLeviathanBoneAxisWS(boneA, float3(0.0, 0.0, 1.0)), ResolveLeviathanBoneAxisWS(boneB, float3(0.0, 0.0, 1.0)), blend01));
            float3 bindCenterOS = float3(0.0, 0.0, -bodyT * bodyLength);
            float3 localOffsetOS = presentedPositionOS - bindCenterOS;
            float tierBlend = lerp(0.72, 1.0, saturate(_H8LeviathanIkTier));
            float3 targetPositionWS = centerWS + rightWS * localOffsetOS.x + upWS * localOffsetOS.y + forwardWS * localOffsetOS.z;
            float tailWhipMask = bodyT * bodyT;
            float tailWhipAmplitude = saturate(_H8LeviathanTailWhip01) * tailWhipMask * lerp(0.08, 0.18, saturate(_H8LeviathanIkTier));
            targetPositionWS += rightWS * (CheapSignedWave(_Time.y * 11.0 + bodyT * 9.0) * tailWhipAmplitude);
            positionWS = lerp(
                positionWS,
                targetPositionWS,
                tierBlend);
            float3 skinnedNormalWS = NormalizeApprox3D(rightWS * sourceNormalOS.x + upWS * sourceNormalOS.y + forwardWS * sourceNormalOS.z);
            float3 skinnedTangentWS = NormalizeApprox3D(rightWS * sourceTangentOS.x + upWS * sourceTangentOS.y + forwardWS * sourceTangentOS.z);
            normalWS = NormalizeApprox3D(lerp(normalWS, skinnedNormalWS, tierBlend));
            tangentWS = NormalizeApprox3D(lerp(tangentWS, skinnedTangentWS, tierBlend));
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
                float invWoundRadiusSq = rcp(woundRadiusSq);
                float invCoreRadiusSq = rcp(coreRadius * coreRadius);
                half woundContribution = saturate(1.0h - (half)(woundDistanceSq * invWoundRadiusSq));
                half coreContribution = saturate(1.0h - (half)(woundDistanceSq * invCoreRadiusSq));
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
            return (half)((hash & 255u) * 0.00392156863);
        }

        half4 ResolveFaunaGlobalBiolum(float3 positionWS)
        {
            int activeCount = min(max((int)_GlobalBiolumParams.x, 0), 16);
            if (activeCount <= 0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            float selector = frac(abs(positionWS.x * 0.023 + positionWS.z * 0.071 + _GlobalBiolumAupOffset.x * 0.0015 + _GlobalBiolumAupOffset.z * 0.0011));
            int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
            float4 state = _GlobalBiolumStates[stateIndex];
            half strobe = saturate((half)_GlobalBiolumParams.z);
            half highTier = step(4.0h, (half)_GlobalBiolumParams.y);
            int secondaryIndex = stateIndex + 1;
            if (secondaryIndex >= activeCount)
                secondaryIndex = 0;
            float4 secondaryState = _GlobalBiolumStates[secondaryIndex];
            half overPulse = (half)(1.0 - abs(frac(_GlobalBiolumClock.x * 0.07 + selector * 3.0) * 2.0 - 1.0));
            half overdrive = highTier * overPulse * 0.35h;
            half3 color = lerp((half3)state.rgb, half3(1.0h, 1.0h, 1.0h), strobe);
            half intensity = clamp(max((half)state.w, strobe * 10.0h), 0.0h, 10.0h);
            color = lerp(color, (half3)secondaryState.rgb, overdrive);
            intensity = clamp(intensity + (half)secondaryState.w * overdrive, 0.0h, 10.0h);
            return half4(color, intensity);
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
            float lifeMask = 1.0 - saturate((radius - maxRadius) * rcp(bandWidth));
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
            float band = saturate(1.0 - abs(distanceToPulseSq - radiusSq) * rcp(bandSq));
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

        half3 ApplyFaunaCamouflage(half3 albedo, float3 positionWS, half3 ambient)
        {
            float waterDepth = max(0.0, _HectonNoirFogStratification.x - positionWS.y);
            float depth01 = saturate((waterDepth - _FaunaCamouflageParams.x) * _FaunaCamouflageParams.y);
            half ambientLuma = dot(ambient, half3(0.2126h, 0.7152h, 0.0722h));
            half dark01 = saturate(1.0h - max(ambientLuma - (half)_FaunaCamouflageParams.w, 0.0h) * (half)_FaunaCamouflageParams.z);
            half camouflage01 = saturate((half)_FaunaCamouflageStrength * (half)depth01 * dark01);
            half3 tintColor = half3(_FaunaCamouflageTint.r, _FaunaCamouflageTint.g, _FaunaCamouflageTint.b);
            return albedo * lerp(half3(1.0h, 1.0h, 1.0h), tintColor, camouflage01);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 deformedPositionOS = HectonCoreLitSanitizePositionOS(ApplyFaunaVertexPresentation(input.positionOS.xyz, input.normalOS));
            VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            float3 positionWS = positionInputs.positionWS;
            float3 normalWS = normalInputs.normalWS;
            float3 tangentWS = normalInputs.tangentWS;
            ApplyLeviathanGpuSkinning(input.positionOS.xyz, deformedPositionOS, input.normalOS, input.tangentOS.xyz, positionWS, normalWS, tangentWS);
            output.positionWS = positionWS;
            output.positionOS = deformedPositionOS;
            output.normalWS = normalWS;
            output.tangentWS = float4(tangentWS, input.tangentOS.w);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(TransformWorldToHClip(positionWS), _DepthBias, 1.0);
            output.viewDirWS = NormalizeApprox3D(GetWorldSpaceViewDir(positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.ambientSH = SampleSH(output.normalWS);
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
            half bodyFxTier = _H8LeviathanGpuSkinning > 0.5 ? saturate((half)_H8LeviathanIkTier) : 1.0h;
            half3 normalWS = NormalizeApprox3D(input.normalWS);
            float wetnessVelocityMagnitudeSq = dot(_WetnessVelocityWS.xyz, _WetnessVelocityWS.xyz);
            [branch]
            if (bodyFxTier > 0.5h)
            {
                half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                float3x3 tangentToWorld = BuildTangentToWorld((float3)normalWS, input.tangentWS);
                normalWS = NormalizeApprox3D(TransformTangentToWorld(tangentNormal, tangentToWorld));
                normalWS = ApplyWetnessNormalWobble(normalWS, input.positionWS, wetnessVelocityMagnitudeSq);
            }
            half3 ambientSh = input.ambientSH;
            surface.rgb = ApplyFaunaCamouflage(surface.rgb, input.positionWS, ambientSh);
            half mutationHue01 = saturate((half)_FaunaMutationHueShift);
            half3 sicklyMutationColor = half3(0.72h, 0.86h, 0.16h);
            surface.rgb = lerp(surface.rgb, surface.rgb * sicklyMutationColor + sicklyMutationColor * 0.18h, mutationHue01);
            half decay01 = saturate((half)_DecayAmount);
            half boneReveal01 = smoothstep(0.55h, 1.0h, decay01);
            half3 rotColor = surface.rgb * half3(0.25h, 0.19h, 0.13h);
            half3 boneColor = half3(0.70h, 0.66h, 0.55h);
            surface.rgb = lerp(surface.rgb, lerp(rotColor, boneColor, boneReveal01), decay01 * 0.88h);
            half crawlNoise = saturate((half)(CheapSignedWave(_Time.y * 0.85 + input.positionWS.x * 0.31 + input.positionWS.z * 0.23) * 0.5 + 0.5));
            surface.rgb *= lerp(1.0h, 0.76h + crawlNoise * 0.18h, decay01 * (1.0h - boneReveal01));

            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;
            smoothness = saturate(smoothness * (1.0h - decay01 * 0.75h));
            emissionMask = saturate(emissionMask * (1.0h - decay01));
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

            half3 color = ambientSh * surface.rgb * ambientOcclusion * caveAmbientFactor;
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = NormalizeApprox3D(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specularStrength = lerp(0.05h, 0.22h, metallic);
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (nDotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = NormalizeApprox3D(lightDir + viewDirWS);
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
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, input.positionCS);
            color += (surface.rgb * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow * contactShadow);

            half faunaBiolumDim = saturate((half)_FaunaBiolumDim);
            half3 sss = half3(0.0h, 0.0h, 0.0h);
            half3 caustics = half3(0.0h, 0.0h, 0.0h);
            half3 biolum = half3(0.0h, 0.0h, 0.0h);
            [branch]
            if (bodyFxTier > 0.5h)
            {
                sss = HectonCoreLitEvaluateOrganicSss(
                    viewDirWS,
                    lightDir,
                    normalWS,
                    _SssColor.rgb,
                    _SssDistortion,
                    _SssPower,
                    _SssScale);
                caustics = HectonCoreLitEvaluateProjectedCausticsScattering(input.positionWS, normalWS) * surface.rgb;
                biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask;
            }
            half4 globalBiolumState = ResolveFaunaGlobalBiolum(input.positionWS);
            half globalBiolumMask = step(0.001h, globalBiolumState.w);
            biolum += globalBiolumState.rgb * (globalBiolumState.w * 0.08h * emissionMask * globalBiolumMask);
            half3 woundEmission = woundColor * (woundMask * _WoundEmissionBoost);
            half oceanPanic = saturate((half)_GlobalOceanPanic);
            half3 panicEmissionColor = lerp(_EmissionColor.rgb, _GlobalOceanPanicColor.rgb, oceanPanic);
            half panicBlink = lerp(1.0h, lerp(0.35h, 1.45h, (half)step(0.5, frac(_Time.y * 7.0 + input.positionWS.y * 0.03))), oceanPanic);
            half3 emission = ((panicEmissionColor * (_EmissionStrength * emissionMask) * panicBlink) + biolum) * faunaBiolumDim + woundEmission;
            half mutationGlow = saturate(mutationHue01 * 0.55h + (half)_FaunaMutationTwitch * 0.35h);
            emission += half3(0.36h, 0.82h, 0.12h) * mutationGlow * (0.25h + emissionMask);
            half hitFlash01 = (half)ResolveHitFlash01();
            emission = lerp(emission, emission + half3(_HitFlashEmissionColor.rgb) * (half)_HitFlashEmissionColor.a, hitFlash01);
            half sonarFresnelBase = saturate(1.0h - dot(normalWS, viewDirWS));
            half sonarFresnel = sonarFresnelBase * sonarFresnelBase;
            emission += half3(_HectonSonarColor.rgb) * ((half)sonarReveal * (0.65h + sonarFresnel * 1.8h));
            half3 finalColor = HectonCoreLitApplyNoirFog(color + caustics + emission + sss, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
        {
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
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
            #pragma skip_variants POINT POINT_COOKIE POINT_LIGHTS _POINT _POINT_LIGHTS _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
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
                VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 positionWS = positionInputs.positionWS;
                float3 normalWS = normalInputs.normalWS;
                float3 tangentWS = normalInputs.tangentWS;
                ApplyLeviathanGpuSkinning(input.positionOS.xyz, deformedPositionOS, input.normalOS, input.tangentOS.xyz, positionWS, normalWS, tangentWS);
                output.positionCS = GetShadowPositionHClip(positionWS, normalWS);
                output.positionWS = positionWS;
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

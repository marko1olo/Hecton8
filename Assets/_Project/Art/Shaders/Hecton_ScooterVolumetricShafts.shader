Shader "Hidden/Hecton8/ScooterVolumetricShafts"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #define HECTON_MAX_SCOOTER_HEADLIGHTS 2
        #define HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE 30.0
        #define HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START 24.0

        CBUFFER_START(UnityPerMaterial)
            float _HectonShaftPassMode;
            float _HectonShaftRenderScale;
            float _HectonShaftRaymarchSteps;
            float _HectonShaftMaxRayDistance;
            float _HectonShaftScatteringAnisotropy;
            float _HectonShaftDensity;
            float _HectonShaftBlueNoiseJitter;
            float _HectonShaftBilateralDepthSigma;
            float _HectonShaftIntensity;
            float _HectonBiolumPatternScale;
            float _HectonBiolumProjectionStrength;
            float _HectonSiltStrength;
            float _HectonSiltNoiseScale;
            float _HectonSiltFloorBoost;
            float _HectonSiltDriftSpeed;
            float _HectonContactShadowStrength;
            float _HectonContactShadowSteps;
            float _HectonContactShadowBias;
            float _HectonContactShadowMaxDistance;
            float _HectonHasBlueNoiseTex;
        CBUFFER_END

        int _HectonScooterHeadlightCount;
        float4 _HectonScooterHeadlightPositionsWS[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightDirectionsWS[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightColors[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightConeData[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterVelocityWS;
        float4 _HectonFloorBiolumColor;
        float4 _HectonVegetationWakeTrailWorldRect;
        float4 _GlobalDriftOffset;
        float4 _BlitTexture_TexelSize;
        float4 _BlueNoiseTex_TexelSize;
        float _HectonFloorBiolumStrength;
        float _HectonVegetationWakeTrailActive;
        float _HectonScooterBrakeCloud;

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D(_BlueNoiseTex);
        SAMPLER(sampler_BlueNoiseTex);
        TEXTURE2D(_HectonVegetationWakeTrailRT);
        SAMPLER(sampler_HectonVegetationWakeTrailRT);
        TEXTURE2D_X(_HectonShaftsTexture);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float SafeRcp(float value)
        {
            return value > 0.00001 ? rcp(value) : 0.0;
        }

        float3 SafeNormalize3(float3 value)
        {
            float lenSq = dot(value, value);
            return lenSq > 0.00001 ? value * rsqrt(lenSq) : float3(0.0, 0.0, 1.0);
        }

        float ResolveInterleavedNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float ResolveBlueNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float2 blueNoiseUV = frac(pixel / 64.0);
            float sampled = _HectonHasBlueNoiseTex > 0.5 ? SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r : 0.0;
            float fallback = ResolveInterleavedNoise(screenUV);
            float useBlueNoise = step(0.5, _HectonHasBlueNoiseTex) * step(0.0001, _BlueNoiseTex_TexelSize.z);
            return lerp(fallback, sampled, useBlueNoise);
        }

        float ResolveFarRawDepth()
        {
        #if UNITY_REVERSED_Z
            return 0.0;
        #else
            return 1.0;
        #endif
        }

        float3 ReconstructWorldPosition(float2 screenUV, float rawDepth)
        {
            return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
        }

        void ResolveDepthData(float2 screenUV, out float rawDepth, out float validMask, out float3 scenePositionWS, out float linearEyeDepth)
        {
            rawDepth = SampleSceneDepth(screenUV);
        #if UNITY_REVERSED_Z
            validMask = step(0.0001, rawDepth);
        #else
            validMask = step(rawDepth, 0.9999);
        #endif

            float resolvedRawDepth = validMask > 0.5 ? rawDepth : ResolveFarRawDepth();
            scenePositionWS = ReconstructWorldPosition(screenUV, resolvedRawDepth);
            linearEyeDepth = LinearEyeDepth(resolvedRawDepth, _ZBufferParams);
        }

        float EvaluateSchlickPhase(float cosTheta, float anisotropy)
        {
            float k = anisotropy * 0.5;
            float denominator = max(1.0 - k * cosTheta, 0.08);
            return (1.0 - k * k) / (12.56637 * denominator * denominator);
        }

        float Hash31(float3 value)
        {
            return frac(sin(dot(value, float3(127.1, 311.7, 74.7))) * 43758.5453);
        }

        float ValueNoise3(float3 samplePosition)
        {
            float3 cell = floor(samplePosition);
            float3 fracPart = frac(samplePosition);
            float3 smoothFrac = fracPart * fracPart * (3.0 - 2.0 * fracPart);

            float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
            float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
            float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
            float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
            float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
            float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
            float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
            float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

            float nx00 = lerp(n000, n100, smoothFrac.x);
            float nx10 = lerp(n010, n110, smoothFrac.x);
            float nx01 = lerp(n001, n101, smoothFrac.x);
            float nx11 = lerp(n011, n111, smoothFrac.x);
            float nxy0 = lerp(nx00, nx10, smoothFrac.y);
            float nxy1 = lerp(nx01, nx11, smoothFrac.y);
            return lerp(nxy0, nxy1, smoothFrac.z);
        }

        float ResolveSiltField(float3 samplePositionWS, float3 rayDirectionWS, float surfaceProximity)
        {
            float timePhase = _Time.y * _HectonSiltDriftSpeed;
            float3 driftedPosition = samplePositionWS * _HectonSiltNoiseScale;
            driftedPosition += _GlobalDriftOffset.xyz * 0.12;
            driftedPosition += float3(timePhase, -timePhase * 0.73, timePhase * 0.41);

            float coarse = ValueNoise3(driftedPosition);
            float fine = ValueNoise3(driftedPosition * 1.97 + 11.0);
            float streakBias = saturate(dot(abs(rayDirectionWS), float3(0.22, 0.14, 0.64)));
            float floorBoost = lerp(1.0, 1.0 + _HectonSiltFloorBoost, surfaceProximity * surfaceProximity);
            float density = saturate(coarse * 0.66 + fine * 0.34 + streakBias * 0.08 - 0.22);
            return density * floorBoost * _HectonSiltStrength;
        }

        float4 EvaluateWakeTrailData(float3 positionWS)
        {
            if (_HectonVegetationWakeTrailActive < 0.5)
                return float4(0.5, 0.5, 0.0, 0.0);

            float2 uv = float2(
                (positionWS.x - _HectonVegetationWakeTrailWorldRect.x) * _HectonVegetationWakeTrailWorldRect.z,
                (positionWS.z - _HectonVegetationWakeTrailWorldRect.y) * _HectonVegetationWakeTrailWorldRect.w);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return float4(0.5, 0.5, 0.0, 0.0);

            return SAMPLE_TEXTURE2D_LOD(_HectonVegetationWakeTrailRT, sampler_HectonVegetationWakeTrailRT, uv, 0);
        }

        float ResolveBrakeSiltImpulse(float3 cameraPositionWS, float3 samplePositionWS, float3 rayDirectionWS)
        {
            float speed = _HectonScooterVelocityWS.w;
            if (speed <= 0.05 || _HectonScooterBrakeCloud <= 0.0001)
                return 0.0;

            float3 velocityDirectionWS = SafeNormalize3(_HectonScooterVelocityWS.xyz);
            if (all(velocityDirectionWS == 0.0))
                return 0.0;

            float cloudDistance = 1.2 + speed * 0.08 + _HectonScooterBrakeCloud * 2.2;
            float cloudRadius = 1.0 + speed * 0.05 + _HectonScooterBrakeCloud * 2.4;
            float3 cloudCenterWS = cameraPositionWS + velocityDirectionWS * cloudDistance;
            float sampleDistance = distance(samplePositionWS, cloudCenterWS);
            float radialMask = 1.0 - smoothstep(cloudRadius * 0.35, cloudRadius, sampleDistance);
            float forwardMask = saturate(dot(SafeNormalize3(samplePositionWS - cameraPositionWS), velocityDirectionWS));
            float shaftMask = saturate(dot(rayDirectionWS, velocityDirectionWS) * 0.5 + 0.5);
            return radialMask * forwardMask * shaftMask * _HectonScooterBrakeCloud;
        }

        float EvaluateSurfaceHeadlightMask(float3 surfacePositionWS, float3 normalWS)
        {
            if (_HectonScooterHeadlightCount <= 0)
                return 0.0;

            float accumulatedMask = 0.0;
            [unroll(HECTON_MAX_SCOOTER_HEADLIGHTS)]
            for (int lightIndex = 0; lightIndex < HECTON_MAX_SCOOTER_HEADLIGHTS; lightIndex++)
            {
                if (lightIndex >= _HectonScooterHeadlightCount)
                    break;

                float3 lightPositionWS = _HectonScooterHeadlightPositionsWS[lightIndex].xyz;
                float volumetricLightFade = ResolveVolumetricLightDistanceFade(lightPositionWS);
                if (volumetricLightFade <= 0.0001)
                    continue;
                float lightRange = max(0.1, _HectonScooterHeadlightPositionsWS[lightIndex].w);
                float3 toSurfaceWS = surfacePositionWS - lightPositionWS;
                float surfaceDistance = length(toSurfaceWS);
                if (surfaceDistance >= lightRange)
                    continue;

                float3 surfaceDirectionWS = toSurfaceWS * SafeRcp(surfaceDistance);
                float3 lightDirectionWS = SafeNormalize3(_HectonScooterHeadlightDirectionsWS[lightIndex].xyz);
                float innerCos = _HectonScooterHeadlightDirectionsWS[lightIndex].w;
                float outerCos = _HectonScooterHeadlightConeData[lightIndex].x;
                float inverseRange = _HectonScooterHeadlightConeData[lightIndex].z;
                float coneAttenuation = ResolveSpotConeAttenuation(dot(lightDirectionWS, surfaceDirectionWS), innerCos, outerCos);
                float rangeAttenuation = saturate(1.0 - surfaceDistance * inverseRange);
                rangeAttenuation *= rangeAttenuation;
                float noL = saturate(dot(normalWS, -surfaceDirectionWS));
                float lightIntensity = _HectonScooterHeadlightColors[lightIndex].w;
                accumulatedMask += coneAttenuation * rangeAttenuation * noL * volumetricLightFade * saturate(lightIntensity * 0.35);
            }

            return saturate(accumulatedMask);
        }

        float EvaluateContactShadow(float3 surfacePositionWS, float3 normalWS)
        {
            if (_HectonScooterHeadlightCount <= 0 || _HectonContactShadowStrength <= 0.0001)
                return 1.0;

            int stepCount = max(1, (int)round(_HectonContactShadowSteps));
            float3 biasedSurfacePositionWS = surfacePositionWS + normalWS * _HectonContactShadowBias;
            float shadowOcclusion = 0.0;

            [loop]
            for (int stepIndex = 0; stepIndex < 8; stepIndex++)
            {
                if (stepIndex >= stepCount)
                    break;

                float stepT = (stepIndex + 1.0) * SafeRcp((float)stepCount + 1.0);

                [unroll(HECTON_MAX_SCOOTER_HEADLIGHTS)]
                for (int lightIndex = 0; lightIndex < HECTON_MAX_SCOOTER_HEADLIGHTS; lightIndex++)
                {
                    if (lightIndex >= _HectonScooterHeadlightCount)
                        break;

                    float3 lightPositionWS = _HectonScooterHeadlightPositionsWS[lightIndex].xyz;
                    float volumetricLightFade = ResolveVolumetricLightDistanceFade(lightPositionWS);
                    if (volumetricLightFade <= 0.0001)
                        continue;
                    float lightRange = max(0.1, _HectonScooterHeadlightPositionsWS[lightIndex].w);
                    float3 lightRayWS = lightPositionWS - biasedSurfacePositionWS;
                    float lightDistance = length(lightRayWS);
                    if (lightDistance <= 0.0001)
                        continue;

                    float marchDistance = min(lightDistance, _HectonContactShadowMaxDistance);
                    float marchT = stepT * SafeRcp(lightDistance) * marchDistance;
                    float3 raySampleWS = lerp(biasedSurfacePositionWS, lightPositionWS, marchT);
                    float4 raySampleCS = TransformWorldToHClip(raySampleWS);
                    if (raySampleCS.w <= 0.0)
                        continue;

                    float2 raySampleUV = raySampleCS.xy * SafeRcp(raySampleCS.w) * 0.5 + 0.5;
                    if (raySampleUV.x <= 0.0 || raySampleUV.x >= 1.0 || raySampleUV.y <= 0.0 || raySampleUV.y >= 1.0)
                        continue;

                    float sampledRawDepth = SampleSceneDepth(raySampleUV);
                #if UNITY_REVERSED_Z
                    float sampledDepthValid = step(0.0001, sampledRawDepth);
                #else
                    float sampledDepthValid = step(sampledRawDepth, 0.9999);
                #endif
                    if (sampledDepthValid <= 0.5)
                        continue;

                    float3 sampledScenePositionWS = ComputeWorldSpacePosition(raySampleUV, sampledRawDepth, UNITY_MATRIX_I_VP);
                    float sceneEyeDistance = distance(_WorldSpaceCameraPos, sampledScenePositionWS);
                    float rayEyeDistance = distance(_WorldSpaceCameraPos, raySampleWS);
                    float occluded = step(sceneEyeDistance + (_HectonContactShadowBias * 0.5), rayEyeDistance);
                    if (occluded <= 0.5)
                        continue;

                    float3 surfaceVectorWS = surfacePositionWS - lightPositionWS;
                    float3 surfaceDirectionWS = SafeNormalize3(surfaceVectorWS);
                    float3 lightDirectionWS = SafeNormalize3(_HectonScooterHeadlightDirectionsWS[lightIndex].xyz);
                    float innerCos = _HectonScooterHeadlightDirectionsWS[lightIndex].w;
                    float outerCos = _HectonScooterHeadlightConeData[lightIndex].x;
                    float inverseRange = _HectonScooterHeadlightConeData[lightIndex].z;
                    float coneAttenuation = ResolveSpotConeAttenuation(dot(lightDirectionWS, surfaceDirectionWS), innerCos, outerCos);
                    float rangeAttenuation = saturate(1.0 - min(lightDistance, lightRange) * inverseRange);
                    rangeAttenuation *= rangeAttenuation;
                    float noL = saturate(dot(normalWS, -surfaceDirectionWS));
                    shadowOcclusion = max(shadowOcclusion, coneAttenuation * rangeAttenuation * noL * volumetricLightFade);
                }
            }

            return 1.0 - saturate(shadowOcclusion * _HectonContactShadowStrength);
        }

        float ResolveSpotConeAttenuation(float cosAngle, float innerCos, float outerCos)
        {
            float coneRange = max(innerCos - outerCos, 0.0001);
            return saturate((cosAngle - outerCos) / coneRange);
        }

        float ResolveVolumetricLightDistanceFade(float3 lightPositionWS)
        {
            float lightDistanceToCamera = distance(_WorldSpaceCameraPos, lightPositionWS);
            return 1.0 - smoothstep(HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START, HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE, lightDistanceToCamera);
        }

        half3 EvaluateHeadlightScattering(float3 samplePositionWS, float3 rayDirectionWS)
        {
            if (_HectonScooterHeadlightCount <= 0)
                return half3(0.0, 0.0, 0.0);

            half3 accumulated = half3(0.0, 0.0, 0.0);
            [unroll(HECTON_MAX_SCOOTER_HEADLIGHTS)]
            for (int lightIndex = 0; lightIndex < HECTON_MAX_SCOOTER_HEADLIGHTS; lightIndex++)
            {
                if (lightIndex >= _HectonScooterHeadlightCount)
                    break;

                float3 lightPositionWS = _HectonScooterHeadlightPositionsWS[lightIndex].xyz;
                float volumetricLightFade = ResolveVolumetricLightDistanceFade(lightPositionWS);
                if (volumetricLightFade <= 0.0001)
                    continue;
                float lightRange = max(0.1, _HectonScooterHeadlightPositionsWS[lightIndex].w);
                float3 lightDirectionWS = SafeNormalize3(_HectonScooterHeadlightDirectionsWS[lightIndex].xyz);
                float innerCos = _HectonScooterHeadlightDirectionsWS[lightIndex].w;
                float outerCos = _HectonScooterHeadlightConeData[lightIndex].x;
                float shaftStrength = _HectonScooterHeadlightConeData[lightIndex].y;
                float inverseRange = _HectonScooterHeadlightConeData[lightIndex].z;
                float3 sampleVectorWS = samplePositionWS - lightPositionWS;
                float sampleDistance = length(sampleVectorWS);
                if (sampleDistance >= lightRange)
                    continue;

                float3 sampleDirectionWS = sampleVectorWS * SafeRcp(sampleDistance);
                float coneAttenuation = ResolveSpotConeAttenuation(dot(lightDirectionWS, sampleDirectionWS), innerCos, outerCos);
                if (coneAttenuation <= 0.0001)
                    continue;

                float rangeAttenuation = saturate(1.0 - sampleDistance * inverseRange);
                rangeAttenuation *= rangeAttenuation;
                float halo = exp2(-sampleDistance * (1.35 * inverseRange));
                float phaseCos = saturate(dot(sampleDirectionWS, -rayDirectionWS));
                float phase = EvaluateSchlickPhase(phaseCos, _HectonShaftScatteringAnisotropy);
                float volumetricEnergy = ((coneAttenuation * rangeAttenuation * phase * shaftStrength) + (halo * 0.08)) * volumetricLightFade;
                half3 lightColor = _HectonScooterHeadlightColors[lightIndex].rgb;
                float lightIntensity = _HectonScooterHeadlightColors[lightIndex].w;
                accumulated += lightColor * (lightIntensity * volumetricEnergy);
            }

            return accumulated;
        }

        half3 IntegrateHeadlightShafts(float2 screenUV)
        {
            if (_HectonScooterHeadlightCount <= 0)
                return half3(0.0, 0.0, 0.0);

            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);

            float3 cameraPositionWS = _WorldSpaceCameraPos;
            float3 rayVectorWS = scenePositionWS - cameraPositionWS;
            float rayLength = min(length(rayVectorWS), max(1.0, _HectonShaftMaxRayDistance));
            if (rayLength <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float3 rayDirectionWS = rayVectorWS * SafeRcp(rayLength);
            float noise = ResolveBlueNoise(screenUV);
            float jitter = lerp(0.5, noise, _HectonShaftBlueNoiseJitter);
            int steps = max(1, (int)round(_HectonShaftRaymarchSteps));
            float stepLength = rayLength * SafeRcp((float)steps);
            float extinction = max(0.0001, _HectonShaftDensity * 0.08);
            half3 accumulated = half3(0.0, 0.0, 0.0);

            [loop]
            for (int stepIndex = 0; stepIndex < 32; stepIndex++)
            {
                if (stepIndex >= steps)
                    break;

                float travelDistance = min(rayLength, ((stepIndex + jitter) * stepLength));
                float3 samplePositionWS = cameraPositionWS + rayDirectionWS * travelDistance;
                half3 scattering = EvaluateHeadlightScattering(samplePositionWS, rayDirectionWS);
                float surfaceProximity = saturate(travelDistance * SafeRcp(rayLength));
                float4 wakeTrailData = EvaluateWakeTrailData(samplePositionWS);
                float wakeIntensity = saturate(wakeTrailData.b);
                float2 wakeDirection = wakeTrailData.rg * 2.0 - 1.0;
                float3 wakeTurbulence = float3(wakeDirection.x, 0.0, wakeDirection.y) * (wakeIntensity * 2.1);
                float brakeImpulse = ResolveBrakeSiltImpulse(cameraPositionWS, samplePositionWS, rayDirectionWS);
                float3 brakeTurbulence = SafeNormalize3(_HectonScooterVelocityWS.xyz) * (brakeImpulse * 1.8);
                float siltField = ResolveSiltField(samplePositionWS + wakeTurbulence + brakeTurbulence, rayDirectionWS, surfaceProximity);
                siltField *= lerp(1.0, 1.0 + wakeIntensity * 1.35, wakeIntensity);
                siltField *= 1.0 + brakeImpulse * 2.4;
                scattering *= (1.0 + siltField);
                float distanceFade = exp2(-travelDistance * extinction);
                distanceFade *= exp2(-(siltField + brakeImpulse * 0.75) * 0.035);
                accumulated += scattering * (distanceFade * stepLength);
            }

            return accumulated * _HectonShaftIntensity;
        }

        float ResolveLinearEyeDepthAtUv(float2 screenUV)
        {
            float rawDepth = SampleSceneDepth(screenUV);
        #if UNITY_REVERSED_Z
            rawDepth = max(rawDepth, 0.0001);
        #else
            rawDepth = min(rawDepth, 0.9999);
        #endif
            return LinearEyeDepth(rawDepth, _ZBufferParams);
        }

        half4 BlurShafts(float2 screenUV, float2 direction)
        {
            float2 texelSize = _BlitTexture_TexelSize.xy;
            float2 axis = direction * texelSize;
            float centerDepth = ResolveLinearEyeDepthAtUv(screenUV);
            half3 accumulated = half3(0.0, 0.0, 0.0);
            float weightSum = 0.0;

            [unroll(5)]
            for (int tap = -2; tap <= 2; tap++)
            {
                float2 sampleUV = saturate(screenUV + axis * tap);
                float sampleDepth = ResolveLinearEyeDepthAtUv(sampleUV);
                float spatialWeight = tap == 0 ? 0.4 : (abs(tap) == 1 ? 0.24 : 0.06);
                float depthDelta = abs(sampleDepth - centerDepth);
                float bilateralWeight = exp2(-depthDelta * max(0.01, _HectonShaftBilateralDepthSigma));
                float foregroundDelta = max(0.0, centerDepth - sampleDepth);
                float foregroundReject = exp2(-foregroundDelta * max(0.01, _HectonShaftBilateralDepthSigma) * 4.0);
                float weight = spatialWeight * bilateralWeight * foregroundReject;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV).rgb * weight;
                weightSum += weight;
            }

            return half4(accumulated * SafeRcp(weightSum), 1.0);
        }

        float3 ApproximateWorldNormal(float2 screenUV, float3 centerPositionWS)
        {
            float2 texel = 1.0 / _ScaledScreenParams.xy;
            float2 sampleUvX = saturate(screenUV + float2(texel.x, 0.0));
            float2 sampleUvY = saturate(screenUV + float2(0.0, texel.y));
            float rawDepthX;
            float validMaskX;
            float3 positionXWS;
            float linearDepthX;
            ResolveDepthData(sampleUvX, rawDepthX, validMaskX, positionXWS, linearDepthX);

            float rawDepthY;
            float validMaskY;
            float3 positionYWS;
            float linearDepthY;
            ResolveDepthData(sampleUvY, rawDepthY, validMaskY, positionYWS, linearDepthY);

            float3 normalWS = SafeNormalize3(cross(positionYWS - centerPositionWS, positionXWS - centerPositionWS));
            return normalWS.y < 0.0 ? -normalWS : normalWS;
        }

        half3 EvaluateBiolumFloorProjection(float2 screenUV)
        {
            if (_HectonFloorBiolumStrength <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);
            if (depthValid <= 0.5)
                return half3(0.0, 0.0, 0.0);

            float3 normalWS = ApproximateWorldNormal(screenUV, scenePositionWS);
            float floorMask = saturate((normalWS.y - 0.42) * 2.4);
            if (floorMask <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float patternScale = max(0.001, _HectonBiolumPatternScale);
            float2 patternCoord = scenePositionWS.xz * patternScale;
            float pattern =
                sin(patternCoord.x + _Time.y * 0.72) * 0.5 +
                cos(patternCoord.y * 1.13 - _Time.y * 0.57) * 0.35 +
                sin((patternCoord.x + patternCoord.y) * 0.74 + _Time.y * 1.11) * 0.15;
            pattern = saturate(pattern * 0.5 + 0.5);
            pattern = smoothstep(0.28, 0.92, pattern);
            float distanceFade = exp2(-linearEyeDepth * 0.004);
            return _HectonFloorBiolumColor.rgb * (_HectonFloorBiolumStrength * _HectonBiolumProjectionStrength * floorMask * pattern * distanceFade);
        }

        half4 FragRaymarch(Varyings input) : SV_Target
        {
            return half4(IntegrateHeadlightShafts(input.screenUV), 1.0);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            return BlurShafts(input.screenUV, float2(1.0, 0.0));
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            return BlurShafts(input.screenUV, float2(0.0, 1.0));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            half3 shafts = SAMPLE_TEXTURE2D_X(_HectonShaftsTexture, sampler_LinearClamp, input.screenUV).rgb;
            half3 biolumProjection = EvaluateBiolumFloorProjection(input.screenUV);
            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(input.screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);
            if (depthValid > 0.5)
            {
                float3 normalWS = ApproximateWorldNormal(input.screenUV, scenePositionWS);
                float headlightMask = EvaluateSurfaceHeadlightMask(scenePositionWS, normalWS);
                float contactShadow = EvaluateContactShadow(scenePositionWS, normalWS);
                sourceColor.rgb *= lerp(1.0, contactShadow, headlightMask);
            }

            return half4(sourceColor.rgb + shafts + biolumProjection, sourceColor.a);
        }
        ENDHLSL

        Pass
        {
            Name "Raymarch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRaymarch
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }

    FallBack Off
}

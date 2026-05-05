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
        #define HECTON_RECENT_CUT_HEAT_MAX 16
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
            float _HectonFlashlightShadowSteps;
            float _HectonFlashlightShadowSoftness;
            float _HectonFlashlightShadowMinStep;
            float _HectonFlashlightShadowBias;
            float _HectonFlashlightShadowFloor;
            float _HectonNoirPower;
            float _HectonNoirFogDensity;
            float4 _HectonNoirLiftColor;
            float _HectonLensGhostIntensity;
            float _HectonLensGhostScale;
            float _HectonLensChromaticAberration;
            float _HectonLensEdgeWeight;
            float _HectonLensDirtIntensity;
            float _HectonCondensationIntensity;
            float _HectonThermalHazeIntensity;
            float _HectonThermalHazeScale;
            float _HectonHasExposureState;
            float _HectonHasBlueNoiseTex;
            float _HectonFrameCount;
        CBUFFER_END

        StructuredBuffer<float4> _HectonNoirExposureState;
        int _HectonScooterHeadlightCount;
        float4 _HectonScooterHeadlightPositionsWS[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightDirectionsWS[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightColors[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonScooterHeadlightConeData[HECTON_MAX_SCOOTER_HEADLIGHTS];
        float4 _HectonFlashlightPositionWS;
        float4 _HectonFlashlightDirectionWS;
        float4 _HectonFlashlightColor;
        float4 _HectonFlashlightConeData;
        float4 _HectonFlashlightVoxelHalfExtents;
        float4x4 _HectonFlashlightVoxelWorldToLocal;
        float4 _HectonCaveVoxelHalfExtents;
        float4x4 _HectonCaveVoxelWorldToLocal;
        float4 _SunDirection;
        float4 _HectonScooterVelocityWS;
        float4 _HectonFloorBiolumColor;
        float4 _HectonShallowWaterFieldWorldRect;
        float4 _HectonCaveVoxelAoParams;
        float4 _GlobalDriftOffset;
        float4 _BlitTexture_TexelSize;
        float4 _BlueNoiseTex_TexelSize;
        float4 _HectonShaftsTexture_TexelSize;
        float _EclipseOcclusion;
        float _HectonFloorBiolumStrength;
        float _HectonShallowWaterFieldActive;
        float _HectonScooterBrakeCloud;
        float _HectonFlashlightActive;
        float _HectonFlashlightVoxelActive;
        float _HectonCaveVoxelActive;
        int _HectonRecentCutHeatCount;
        float4 _HectonRecentCutHeatPositionRadius[HECTON_RECENT_CUT_HEAT_MAX];
        float4 _HectonRecentCutHeatStrengthTime[HECTON_RECENT_CUT_HEAT_MAX];

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D(_BlueNoiseTex);
        SAMPLER(sampler_BlueNoiseTex);
        TEXTURE2D(_HectonShallowWaterFieldRT);
        SAMPLER(sampler_HectonShallowWaterFieldRT);
        TEXTURE2D_X(_HectonShaftsTexture);
        TEXTURE3D(_VoxelDensityTex);
        SAMPLER(sampler_VoxelDensityTex);
        TEXTURE3D(_HectonCaveVoxelSdfTex);
        SAMPLER(sampler_HectonCaveVoxelSdfTex);

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

        float ResolveSpotConeAttenuation(float cosAngle, float innerCos, float outerCos)
        {
            float coneRange = max(innerCos - outerCos, 0.0001);
            return saturate((cosAngle - outerCos) / coneRange);
        }

        float ResolveVolumetricLightDistanceFade(float3 lightPositionWS)
        {
            float lightDistanceToCamera = distance(_WorldSpaceCameraPos, lightPositionWS);
            float safeDistance = isfinite(lightDistanceToCamera) ? lightDistanceToCamera : HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE;
            float fadeRange = max(HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE - HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START, 0.0001);
            float fade = 1.0 - saturate((safeDistance - HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START) / fadeRange);
            return max(fade, 0.0);
        }

        float ResolveTemporalFrameIndex()
        {
            return max(_HectonFrameCount, floor(_Time.y * 60.0));
        }

        float2 ResolveTemporalR2Offset()
        {
            const float2 r2Sequence = float2(0.7548776662466927, 0.5698402909980532);
            return frac(ResolveTemporalFrameIndex() * r2Sequence);
        }

        float2 ResolveBlueNoiseTexelScale()
        {
            float useImportedTexelScale = step(0.0001, _BlueNoiseTex_TexelSize.z) * step(0.0001, _BlueNoiseTex_TexelSize.w);
            return lerp(float2(1.0 / 64.0, 1.0 / 64.0), _BlueNoiseTex_TexelSize.xy, useImportedTexelScale);
        }

        float ResolveInterleavedNoise(float2 screenUV)
        {
            float2 temporalOffset = ResolveTemporalR2Offset() * _ScaledScreenParams.xy;
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy + temporalOffset);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 34.45);
            return frac(p.x * p.y);
        }

        float ValueNoise2D(float2 p)
        {
            float2 cell = floor(p);
            float2 local = frac(p);
            float2 u = local * local * (3.0 - 2.0 * local);
            float a = Hash21(cell);
            float b = Hash21(cell + float2(1.0, 0.0));
            float c = Hash21(cell + float2(0.0, 1.0));
            float d = Hash21(cell + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float ResolveBlueNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float2 temporalOffset = ResolveTemporalR2Offset();
            float2 blueNoiseUV = frac(pixel * ResolveBlueNoiseTexelScale() + temporalOffset);
            float sampled = _HectonHasBlueNoiseTex > 0.5 ? SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r : 0.0;
            float fallback = ResolveInterleavedNoise(screenUV);
            float useBlueNoise = step(0.5, _HectonHasBlueNoiseTex) * step(0.0001, _BlueNoiseTex_TexelSize.z);
            return lerp(fallback, sampled, useBlueNoise);
        }

        half3 ApplyResolveBlueNoiseDither(half3 color, float2 screenUV)
        {
            float dither = (ResolveBlueNoise(screenUV) - 0.5) * (1.0 / 255.0);
            return max(color + (half)dither.xxx, 0.0h);
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
            if (!isfinite(linearEyeDepth) || linearEyeDepth < 0.0)
            {
                validMask = 0.0;
                linearEyeDepth = 0.0;
            }
        }

        float PhaseHG(float cosTheta, float anisotropy)
        {
            float g = clamp(anisotropy, -0.95, 0.95);
            float gSq = g * g;
            float denominator = max(1.0 + gSq - 2.0 * g * cosTheta, 0.0001);
            return (1.0 - gSq) * SafeRcp(12.56637 * denominator * sqrt(denominator));
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

        float3 ResolveShaftCurlNoise(float3 samplePosition)
        {
            const float epsilon = 0.37;
            float3 xOffset = float3(epsilon, 0.0, 0.0);
            float3 yOffset = float3(0.0, epsilon, 0.0);
            float3 zOffset = float3(0.0, 0.0, epsilon);

            float fxY1 = ValueNoise3(samplePosition + yOffset + float3(17.1, 9.3, 31.7));
            float fxY0 = ValueNoise3(samplePosition - yOffset + float3(17.1, 9.3, 31.7));
            float fxZ1 = ValueNoise3(samplePosition + zOffset + float3(17.1, 9.3, 31.7));
            float fxZ0 = ValueNoise3(samplePosition - zOffset + float3(17.1, 9.3, 31.7));

            float fyX1 = ValueNoise3(samplePosition + xOffset + float3(43.7, 13.9, 7.1));
            float fyX0 = ValueNoise3(samplePosition - xOffset + float3(43.7, 13.9, 7.1));
            float fyZ1 = ValueNoise3(samplePosition + zOffset + float3(43.7, 13.9, 7.1));
            float fyZ0 = ValueNoise3(samplePosition - zOffset + float3(43.7, 13.9, 7.1));

            float fzX1 = ValueNoise3(samplePosition + xOffset + float3(5.9, 27.4, 49.3));
            float fzX0 = ValueNoise3(samplePosition - xOffset + float3(5.9, 27.4, 49.3));
            float fzY1 = ValueNoise3(samplePosition + yOffset + float3(5.9, 27.4, 49.3));
            float fzY0 = ValueNoise3(samplePosition - yOffset + float3(5.9, 27.4, 49.3));

            float dFxDy = fxY1 - fxY0;
            float dFxDz = fxZ1 - fxZ0;
            float dFyDx = fyX1 - fyX0;
            float dFyDz = fyZ1 - fyZ0;
            float dFzDx = fzX1 - fzX0;
            float dFzDy = fzY1 - fzY0;

            float3 curl = float3(
                dFzDy - dFyDz,
                dFxDz - dFzDx,
                dFyDx - dFxDy);
            return SafeNormalize3(curl);
        }

        float ResolveSiltField(float3 samplePositionWS, float3 rayDirectionWS, float surfaceProximity)
        {
            float timePhase = _Time.y * _HectonSiltDriftSpeed;
            float3 driftedPosition = samplePositionWS * _HectonSiltNoiseScale;
            driftedPosition += _GlobalDriftOffset.xyz * 0.12;
            driftedPosition += float3(timePhase, -timePhase * 0.73, timePhase * 0.41);

            float coarse = ValueNoise3(driftedPosition);
            float fine = ValueNoise3(driftedPosition * 1.97 + 11.0);
            float3 curlNoise = ResolveShaftCurlNoise(driftedPosition * 0.73 + float3(0.0, timePhase * 0.45, -timePhase * 0.31));
            float curlDensity = saturate(length(curlNoise.xz) * 0.68 + abs(curlNoise.y) * 0.32);
            float curlAlignment = saturate(dot(curlNoise, SafeNormalize3(rayDirectionWS + float3(0.0, 0.2, 0.0))) * 0.5 + 0.5);
            float streakBias = saturate(dot(abs(rayDirectionWS), float3(0.22, 0.14, 0.64)));
            float floorBoost = lerp(1.0, 1.0 + _HectonSiltFloorBoost, surfaceProximity * surfaceProximity);
            float density = saturate(coarse * 0.52 + fine * 0.24 + curlDensity * 0.16 + curlAlignment * 0.12 + streakBias * 0.08 - 0.22);
            return density * floorBoost * _HectonSiltStrength;
        }

        float4 EvaluateShallowWaterFieldData(float3 positionWS)
        {
            if (_HectonShallowWaterFieldActive < 0.5)
                return float4(0.5, 0.5, 0.0, 0.0);

            float2 uv = float2(
                (positionWS.x - _HectonShallowWaterFieldWorldRect.x) * _HectonShallowWaterFieldWorldRect.z,
                (positionWS.z - _HectonShallowWaterFieldWorldRect.y) * _HectonShallowWaterFieldWorldRect.w);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return float4(0.5, 0.5, 0.0, 0.0);

            return SAMPLE_TEXTURE2D_LOD(_HectonShallowWaterFieldRT, sampler_HectonShallowWaterFieldRT, uv, 0);
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

        float ResolveFlashlightShadowFloor()
        {
            float noirLiftFloor = max(_HectonNoirLiftColor.r, max(_HectonNoirLiftColor.g, _HectonNoirLiftColor.b));
            return max(_HectonFlashlightShadowFloor, noirLiftFloor);
        }

        float3 TransformFlashlightVoxelLocal(float3 positionWS)
        {
            return mul(_HectonFlashlightVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
        }

        float SampleFlashlightVoxelSignedDistance(float3 positionWS)
        {
            if (_HectonFlashlightVoxelActive <= 0.5)
                return _HectonFlashlightVoxelHalfExtents.w;

            float3 halfExtents = max(_HectonFlashlightVoxelHalfExtents.xyz, float3(0.001, 0.001, 0.001));
            float3 localPosition = TransformFlashlightVoxelLocal(positionWS);
            float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
            if (sampleUv.x <= 0.0 || sampleUv.x >= 1.0 ||
                sampleUv.y <= 0.0 || sampleUv.y >= 1.0 ||
                sampleUv.z <= 0.0 || sampleUv.z >= 1.0)
            {
                return _HectonFlashlightVoxelHalfExtents.w;
            }

            float encoded = SAMPLE_TEXTURE3D_LOD(_VoxelDensityTex, sampler_VoxelDensityTex, sampleUv, 0).r;
            return lerp(-_HectonFlashlightVoxelHalfExtents.w, _HectonFlashlightVoxelHalfExtents.w, encoded);
        }

        float SampleCaveVoxelSignedDistance(float3 positionWS)
        {
            if (_HectonCaveVoxelActive <= 0.5)
                return _HectonCaveVoxelHalfExtents.w;

            float3 halfExtents = max(_HectonCaveVoxelHalfExtents.xyz, float3(0.001, 0.001, 0.001));
            float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
            float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
            if (sampleUv.x <= 0.0 || sampleUv.x >= 1.0 ||
                sampleUv.y <= 0.0 || sampleUv.y >= 1.0 ||
                sampleUv.z <= 0.0 || sampleUv.z >= 1.0)
            {
                return _HectonCaveVoxelHalfExtents.w;
            }

            float encoded = SAMPLE_TEXTURE3D_LOD(_HectonCaveVoxelSdfTex, sampler_HectonCaveVoxelSdfTex, sampleUv, 0).r;
            return lerp(-_HectonCaveVoxelHalfExtents.w, _HectonCaveVoxelHalfExtents.w, encoded);
        }

        float ResolveCaveVoxelFogFade(float signedDistance)
        {
            return saturate(signedDistance * 2.0);
        }

        float EvaluateFlashlightVoxelShadowRay(float3 rayOriginWS, float3 rayDirectionWS, float rayLength)
        {
            if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5 || rayLength <= 0.0001)
                return 1.0;

            int stepCount = max(1, (int)round(_HectonFlashlightShadowSteps));
            float minStep = max(_HectonFlashlightShadowMinStep, 0.01);
            float shadowFloor = ResolveFlashlightShadowFloor();
            float result = 1.0;
            float travel = minStep;

            [loop]
            for (int stepIndex = 0; stepIndex < 32; stepIndex++)
            {
                if (stepIndex >= stepCount || travel >= rayLength)
                    break;

                float3 samplePositionWS = rayOriginWS + rayDirectionWS * travel;
                float h = SampleFlashlightVoxelSignedDistance(samplePositionWS);
                if (h <= 0.02)
                    return shadowFloor;

                result = min(result, _HectonFlashlightShadowSoftness * h / max(travel, 0.001));
                travel += max(minStep, h);
            }

            return max(saturate(result), shadowFloor);
        }

        float EvaluateFlashlightSurfaceMask(float3 surfacePositionWS, float3 normalWS)
        {
            if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
                return 0.0;

            float3 lightPositionWS = _HectonFlashlightPositionWS.xyz;
            float lightRange = max(0.1, _HectonFlashlightPositionWS.w);
            float3 toSurfaceWS = surfacePositionWS - lightPositionWS;
            float surfaceDistance = length(toSurfaceWS);
            if (surfaceDistance <= 0.0001 || surfaceDistance >= lightRange)
                return 0.0;

            float3 surfaceDirectionWS = toSurfaceWS * SafeRcp(surfaceDistance);
            float3 lightDirectionWS = SafeNormalize3(_HectonFlashlightDirectionWS.xyz);
            float coneAttenuation = ResolveSpotConeAttenuation(
                dot(lightDirectionWS, surfaceDirectionWS),
                _HectonFlashlightDirectionWS.w,
                _HectonFlashlightConeData.x);
            if (coneAttenuation <= 0.0001)
                return 0.0;

            float rangeAttenuation = saturate(1.0 - surfaceDistance * _HectonFlashlightConeData.z);
            rangeAttenuation *= rangeAttenuation;
            float noL = saturate(dot(normalWS, -surfaceDirectionWS));
            return coneAttenuation * rangeAttenuation * noL * saturate(_HectonFlashlightColor.w * 0.35);
        }

        float EvaluateFlashlightSurfaceShadow(float3 surfacePositionWS, float3 normalWS)
        {
            if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
                return 1.0;

            float3 lightRayWS = _HectonFlashlightPositionWS.xyz - surfacePositionWS;
            float lightDistance = length(lightRayWS);
            if (lightDistance <= 0.0001)
                return 1.0;

            float3 rayDirectionWS = lightRayWS * SafeRcp(lightDistance);
            float3 rayOriginWS = surfacePositionWS + normalWS * _HectonFlashlightShadowBias;
            float rayLength = max(lightDistance - _HectonFlashlightShadowBias, 0.0);
            return EvaluateFlashlightVoxelShadowRay(rayOriginWS, rayDirectionWS, rayLength);
        }

        half3 EvaluateFlashlightScattering(float3 samplePositionWS, float3 rayDirectionWS)
        {
            if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
                return half3(0.0, 0.0, 0.0);

            float3 lightPositionWS = _HectonFlashlightPositionWS.xyz;
            float lightRange = max(0.1, _HectonFlashlightPositionWS.w);
            float3 lightDirectionWS = SafeNormalize3(_HectonFlashlightDirectionWS.xyz);
            float3 sampleVectorWS = samplePositionWS - lightPositionWS;
            float sampleDistance = length(sampleVectorWS);
            if (sampleDistance <= 0.0001 || sampleDistance >= lightRange)
                return half3(0.0, 0.0, 0.0);

            float3 sampleDirectionWS = sampleVectorWS * SafeRcp(sampleDistance);
            float coneAttenuation = ResolveSpotConeAttenuation(
                dot(lightDirectionWS, sampleDirectionWS),
                _HectonFlashlightDirectionWS.w,
                _HectonFlashlightConeData.x);
            if (coneAttenuation <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float rangeAttenuation = saturate(1.0 - sampleDistance * _HectonFlashlightConeData.z);
            rangeAttenuation *= rangeAttenuation;
            float halo = exp2(-sampleDistance * (1.35 * _HectonFlashlightConeData.z));
            float phaseCos = saturate(dot(sampleDirectionWS, -rayDirectionWS));
            float phase = PhaseHG(phaseCos, _HectonShaftScatteringAnisotropy);
            float volumetricEnergy = (coneAttenuation * rangeAttenuation * phase * _HectonFlashlightConeData.y) + (halo * 0.08);
            float shadow = EvaluateFlashlightVoxelShadowRay(
                samplePositionWS - sampleDirectionWS * _HectonFlashlightShadowBias,
                -sampleDirectionWS,
                max(sampleDistance - _HectonFlashlightShadowBias, 0.0));
            half3 lightColor = _HectonFlashlightColor.rgb;
            float lightIntensity = _HectonFlashlightColor.w;
            return lightColor * (lightIntensity * volumetricEnergy * shadow);
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
                float phase = PhaseHG(phaseCos, _HectonShaftScatteringAnisotropy);
                float volumetricEnergy = ((coneAttenuation * rangeAttenuation * phase * shaftStrength) + (halo * 0.08)) * volumetricLightFade;
                half3 lightColor = _HectonScooterHeadlightColors[lightIndex].rgb;
                float lightIntensity = _HectonScooterHeadlightColors[lightIndex].w;
                accumulated += lightColor * (lightIntensity * volumetricEnergy);
            }

            return accumulated;
        }

        half3 IntegrateHeadlightShafts(float2 screenUV)
        {
            if (_HectonScooterHeadlightCount <= 0 &&
                (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5))
            {
                return half3(0.0, 0.0, 0.0);
            }

            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);
            if (depthValid <= 0.5 || linearEyeDepth <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float3 cameraPositionWS = _WorldSpaceCameraPos;
            float3 rayVectorWS = scenePositionWS - cameraPositionWS;
            float rayLength = min(length(rayVectorWS), max(1.0, _HectonShaftMaxRayDistance));
            if (rayLength <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float3 rayDirectionWS = rayVectorWS * SafeRcp(rayLength);
            float noise = ResolveBlueNoise(screenUV);
            float jitter = lerp(0.5, noise, _HectonShaftBlueNoiseJitter);
            const int steps = 8;
            float stepLength = rayLength * SafeRcp((float)steps);
            float extinction = max(0.0001, _HectonShaftDensity * 0.08);
            half3 accumulated = half3(0.0, 0.0, 0.0);

            [unroll(8)]
            for (int stepIndex = 0; stepIndex < steps; stepIndex++)
            {
                float travelDistance = min(rayLength, ((stepIndex + jitter) * stepLength));
                float3 samplePositionWS = cameraPositionWS + rayDirectionWS * travelDistance;
                float caveFogFade = 1.0;
                if (_HectonCaveVoxelActive > 0.5)
                {
                    float signedDistance = SampleCaveVoxelSignedDistance(samplePositionWS);
                    caveFogFade = ResolveCaveVoxelFogFade(signedDistance);
                    if (caveFogFade <= 0.0001)
                        break;
                }

                half3 scattering =
                    EvaluateHeadlightScattering(samplePositionWS, rayDirectionWS) +
                    EvaluateFlashlightScattering(samplePositionWS, rayDirectionWS);
                float surfaceProximity = saturate(travelDistance * SafeRcp(rayLength));
                float4 wakeTrailData = EvaluateShallowWaterFieldData(samplePositionWS);
                float wakeDisplacement = saturate(wakeTrailData.b);
                float2 wakeVelocity = wakeTrailData.rg * 2.0 - 1.0;
                float wakeVelocityMagnitude = saturate(length(wakeVelocity));
                float3 wakeTurbulence = float3(wakeVelocity.x, 0.0, wakeVelocity.y) * (wakeDisplacement * 1.4 + wakeVelocityMagnitude * 0.7);
                float brakeImpulse = ResolveBrakeSiltImpulse(cameraPositionWS, samplePositionWS, rayDirectionWS);
                float3 brakeTurbulence = SafeNormalize3(_HectonScooterVelocityWS.xyz) * (brakeImpulse * 1.8);
                float siltField = ResolveSiltField(samplePositionWS + wakeTurbulence + brakeTurbulence, rayDirectionWS, surfaceProximity);
                siltField *= lerp(1.0, 1.0 + wakeDisplacement * 1.35, wakeDisplacement);
                siltField *= 1.0 + brakeImpulse * 2.4;
                scattering *= (1.0 + siltField) * caveFogFade;
                float distanceFade = exp2(-travelDistance * extinction);
                distanceFade *= exp2(-(siltField + brakeImpulse * 0.75) * 0.035);
                distanceFade *= caveFogFade;
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
            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            return isfinite(linearEyeDepth) && linearEyeDepth >= 0.0 ? linearEyeDepth : 0.0;
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

        float ResolveNoirFogFactor(float linearEyeDepth)
        {
            float depthLinear = max(0.0, linearEyeDepth);
            float fogNoir = pow(saturate(1.0 - exp(-depthLinear * max(_HectonNoirFogDensity, 0.0001))), max(_HectonNoirPower, 0.001));
            return saturate(fogNoir);
        }

        float ResolveExposureMultiplier()
        {
            if (_HectonHasExposureState <= 0.5)
                return 1.0;

            return max(_HectonNoirExposureState[0].z, 0.05);
        }

        half3 ResolveNoirMinimumColor()
        {
            return max(_HectonNoirLiftColor.rgb, half3(0.01h, 0.012h, 0.016h));
        }

        float2 ResolveSunScreenUv(out float visibility)
        {
            float3 toSunWS = SafeNormalize3(-_SunDirection.xyz);
            if (dot(toSunWS, toSunWS) <= 0.0001)
            {
                visibility = 0.0;
                return float2(0.5, 0.5);
            }

            float3 cameraPositionWS = GetCameraPositionWS();
            float4 clip = TransformWorldToHClip(cameraPositionWS + toSunWS * 4096.0);
            if (clip.w <= 0.0)
            {
                visibility = 0.0;
                return float2(0.5, 0.5);
            }

            float2 uv = clip.xy * SafeRcp(clip.w) * 0.5 + 0.5;
            float inside =
                step(-0.25, uv.x) *
                step(uv.x, 1.25) *
                step(-0.25, uv.y) *
                step(uv.y, 1.25);
            visibility = inside * saturate(1.0 - _EclipseOcclusion);
            return uv;
        }

        half3 EvaluateLensGhost(float2 screenUV, float2 ghostCenter, float ghostRadius, float2 aberrationOffset, float intensity)
        {
            float2 deltaR = screenUV - (ghostCenter + aberrationOffset);
            float2 deltaG = screenUV - ghostCenter;
            float2 deltaB = screenUV - (ghostCenter - aberrationOffset);
            float radiusSq = max(ghostRadius * ghostRadius, 0.000001);
            float maskR = saturate(1.0 - dot(deltaR, deltaR) * SafeRcp(radiusSq));
            float maskG = saturate(1.0 - dot(deltaG, deltaG) * SafeRcp(radiusSq));
            float maskB = saturate(1.0 - dot(deltaB, deltaB) * SafeRcp(radiusSq));
            maskR *= maskR;
            maskG *= maskG;
            maskB *= maskB;
            return half3(maskR, maskG, maskB) * intensity;
        }

        half3 EvaluateProceduralLensArtifacts(float2 screenUV)
        {
            if (_HectonLensGhostIntensity <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float sunVisibility;
            float2 sunUV = ResolveSunScreenUv(sunVisibility);
            if (sunVisibility <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float2 centerUV = float2(0.5, 0.5);
            float2 flareVector = centerUV - sunUV;
            float2 radialFromCenter = screenUV - centerUV;
            float edgeFactor = saturate(dot(radialFromCenter, radialFromCenter) * 4.84);
            float edgeWeight = saturate(edgeFactor * max(_HectonLensEdgeWeight, 0.0));
            float2 aberrationDirection = SafeNormalize3(float3(flareVector, 0.0)).xy;
            float2 aberrationOffset = aberrationDirection * (_HectonLensChromaticAberration * lerp(0.35, 1.0, edgeFactor));
            float baseRadius = max(_HectonLensGhostScale, 0.001);
            float ghostFade = saturate(1.0 - dot(sunUV - centerUV, sunUV - centerUV) * 1.6);
            float visibility = sunVisibility * ghostFade * _HectonLensGhostIntensity;
            if (visibility <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            half3 ghosts = half3(0.0, 0.0, 0.0);
            ghosts += EvaluateLensGhost(screenUV, centerUV + flareVector * 0.35, baseRadius * 1.20, aberrationOffset * 0.7, visibility * 0.75);
            ghosts += EvaluateLensGhost(screenUV, centerUV + flareVector * 0.68, baseRadius * 0.94, aberrationOffset * 1.0, visibility * 0.55);
            ghosts += EvaluateLensGhost(screenUV, centerUV + flareVector * 1.05, baseRadius * 0.72, aberrationOffset * 1.35, visibility * 0.42);
            return ghosts * lerp(0.32, 1.0, edgeWeight);
        }

        float ResolveBrightLensDrive()
        {
            float sunVisibility;
            ResolveSunScreenUv(sunVisibility);

            float drive = sunVisibility;
            drive = max(drive, _HectonFlashlightActive * saturate(_HectonFlashlightColor.w * 0.35));
            drive = max(drive, saturate(_HectonFloorBiolumStrength * 0.18));

            [unroll]
            for (int lightIndex = 0; lightIndex < HECTON_MAX_SCOOTER_HEADLIGHTS; lightIndex++)
            {
                if (lightIndex >= _HectonScooterHeadlightCount)
                    continue;

                float lightDistanceFade = ResolveVolumetricLightDistanceFade(_HectonScooterHeadlightPositionsWS[lightIndex].xyz);
                drive = max(drive, saturate(_HectonScooterHeadlightColors[lightIndex].w * lightDistanceFade * 0.28));
            }

            return saturate(drive);
        }

        half3 EvaluateLensDirtCondensation(float2 screenUV, half3 sourceColor)
        {
            float totalArtifactStrength = _HectonLensDirtIntensity + _HectonCondensationIntensity;
            if (totalArtifactStrength <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float lightDrive = ResolveBrightLensDrive();
            if (lightDrive <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float aspect = _ScaledScreenParams.x * SafeRcp(max(_ScaledScreenParams.y, 1.0));
            float2 artifactUV = float2(screenUV.x * aspect, screenUV.y);
            float grime = smoothstep(0.42, 0.96, ValueNoise2D(artifactUV * 31.0 + float2(5.7, 13.1)));
            float dust = smoothstep(0.78, 0.985, ValueNoise2D(artifactUV * 113.0 + float2(37.1, 4.2)));
            float drops = smoothstep(0.84, 0.995, ValueNoise2D(artifactUV * 54.0 + float2(1.9, _Time.y * 0.035)));
            float streaks = smoothstep(
                0.76,
                0.975,
                ValueNoise2D(float2(artifactUV.x * 84.0 + _Time.y * 0.018, artifactUV.y * 12.0 - _Time.y * 0.045)));

            half sourceLuminance = dot(sourceColor, half3(0.2126h, 0.7152h, 0.0722h));
            float reflectedLight = saturate((float)sourceLuminance * 1.65 + lightDrive * 0.72);
            float dirtMask = (grime * 0.34 + dust * 0.22) * _HectonLensDirtIntensity;
            float condensationMask = (drops * 0.58 + streaks * 0.31) * _HectonCondensationIntensity;
            float artifactMask = saturate(dirtMask + condensationMask) * reflectedLight * lightDrive;

            return half3(0.54h, 0.72h, 0.93h) * (half)artifactMask;
        }

        float ResolveRecentHeatHazeWeight(float3 scenePositionWS)
        {
            if (_HectonRecentCutHeatCount <= 0)
                return 0.0;

            float hazeWeight = 0.0;
            [unroll]
            for (int heatIndex = 0; heatIndex < HECTON_RECENT_CUT_HEAT_MAX; heatIndex++)
            {
                if (heatIndex >= _HectonRecentCutHeatCount)
                    continue;

                float4 positionRadius = _HectonRecentCutHeatPositionRadius[heatIndex];
                float4 strengthTime = _HectonRecentCutHeatStrengthTime[heatIndex];
                float radius = max(positionRadius.w, 0.001);
                float age01 = saturate((_Time.y - strengthTime.y) * SafeRcp(max(strengthTime.z, 0.001)));
                float spatialMask = saturate(1.0 - distance(scenePositionWS, positionRadius.xyz) * SafeRcp(radius));
                hazeWeight += spatialMask * spatialMask * max(strengthTime.x, 0.0) * (1.0 - age01);
            }

            return saturate(hazeWeight);
        }

        float2 EvaluateThermalHazeOffset(float2 screenUV, float depthValid, float3 scenePositionWS)
        {
            if (_HectonThermalHazeIntensity <= 0.000001 || depthValid <= 0.5)
                return float2(0.0, 0.0);

            float heatWeight = ResolveRecentHeatHazeWeight(scenePositionWS);
            if (heatWeight <= 0.0001)
                return float2(0.0, 0.0);

            float2 quarterResPixels = max(_ScaledScreenParams.xy * 0.25, float2(1.0, 1.0));
            float2 lowResCell = floor(screenUV * quarterResPixels * max(_HectonThermalHazeScale, 0.001));
            float2 noiseCoord = lowResCell * 0.067 + float2(_Time.y * 0.21, -_Time.y * 0.17);
            float2 displacement = float2(
                ValueNoise2D(noiseCoord + float2(19.7, 2.3)),
                ValueNoise2D(noiseCoord + float2(5.1, 43.8))) - 0.5;
            float shimmer = ValueNoise2D(noiseCoord * 1.73 + float2(11.0, 29.0));
            return displacement * (_HectonThermalHazeIntensity * heatWeight * lerp(0.35, 1.0, shimmer));
        }

        half3 BilateralUpsampleShafts(float2 screenUV, float centerDepth)
        {
            float2 texelSize = _HectonShaftsTexture_TexelSize.xy;
            float2 lowPixel = screenUV * _HectonShaftsTexture_TexelSize.zw - 0.5;
            float2 basePixel = floor(lowPixel);
            half3 accumulated = half3(0.0, 0.0, 0.0);
            float weightSum = 0.0;

            [unroll(4)]
            for (int tapIndex = 0; tapIndex < 4; tapIndex++)
            {
                float2 tapOffset = float2((tapIndex & 1) != 0 ? 1.0 : 0.0, tapIndex >= 2 ? 1.0 : 0.0);
                float2 sampleUV = (basePixel + tapOffset + 0.5) * texelSize;
                float2 sampleDelta = (sampleUV - screenUV) * _HectonShaftsTexture_TexelSize.zw;
                float spatialWeight = exp2(-dot(sampleDelta, sampleDelta));
                float sampleDepth = ResolveLinearEyeDepthAtUv(saturate(sampleUV));
                float depthDelta = abs(sampleDepth - centerDepth);
                float depthWeight = exp2(-depthDelta * max(0.01, _HectonShaftBilateralDepthSigma));
                float weight = spatialWeight * depthWeight;
                accumulated += SAMPLE_TEXTURE2D_X(_HectonShaftsTexture, sampler_LinearClamp, saturate(sampleUV)).rgb * weight;
                weightSum += weight;
            }

            return accumulated * SafeRcp(weightSum);
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
            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(input.screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);
            float2 sourceUV = saturate(input.screenUV + EvaluateThermalHazeOffset(input.screenUV, depthValid, scenePositionWS));
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV);
            float exposureMultiplier = ResolveExposureMultiplier();
            half3 noirMinimum = ResolveNoirMinimumColor();
            sourceColor.rgb *= exposureMultiplier;
            half3 shafts = BilateralUpsampleShafts(input.screenUV, linearEyeDepth);
            shafts *= exposureMultiplier;
            half3 biolumProjection = EvaluateBiolumFloorProjection(input.screenUV) * exposureMultiplier;
            half3 lensGhosts = EvaluateProceduralLensArtifacts(input.screenUV) * exposureMultiplier;
            half3 lensDirtCondensation = EvaluateLensDirtCondensation(input.screenUV, sourceColor.rgb) * exposureMultiplier;
            if (depthValid > 0.5)
            {
                float3 normalWS = ApproximateWorldNormal(input.screenUV, scenePositionWS);
                float headlightMask = EvaluateSurfaceHeadlightMask(scenePositionWS, normalWS);
                float contactShadow = EvaluateContactShadow(scenePositionWS, normalWS);
                sourceColor.rgb *= lerp(1.0, contactShadow, headlightMask);
                float flashlightMask = EvaluateFlashlightSurfaceMask(scenePositionWS, normalWS);
                float flashlightShadow = EvaluateFlashlightSurfaceShadow(scenePositionWS, normalWS);
                sourceColor.rgb *= lerp(1.0, flashlightShadow, flashlightMask);

                float fogNoir = ResolveNoirFogFactor(linearEyeDepth);
                sourceColor.rgb = lerp(sourceColor.rgb, max(noirMinimum, sourceColor.rgb * 0.14h + noirMinimum), fogNoir);
                sourceColor.rgb = max(sourceColor.rgb, noirMinimum);
            }
            else
            {
                sourceColor.rgb = max(sourceColor.rgb, noirMinimum);
            }

            half3 finalColor = sourceColor.rgb + shafts + biolumProjection + lensGhosts + lensDirtCondensation;
            finalColor = max(finalColor, noirMinimum);
            if (any(isnan(finalColor)) || any(isinf(finalColor)))
                finalColor = noirMinimum;
            finalColor = ApplyResolveBlueNoiseDither(finalColor, input.screenUV);
            return half4(finalColor, sourceColor.a);
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

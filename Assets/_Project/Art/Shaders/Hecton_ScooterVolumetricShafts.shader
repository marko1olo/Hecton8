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
        #include "Hecton_WaterExtinction.hlsl"

        #define HECTON_MAX_SCOOTER_HEADLIGHTS 2
        #define HECTON_RECENT_CUT_HEAT_MAX 16
        #define HECTON_RECENT_CUT_HEAT_EVAL_MAX 8
        #define HECTON_FLASHLIGHT_SHADOW_EVAL_MAX 5
        #define HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE 30.0
        #define HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START 24.0
        #define HECTON_SHAFT_CHEAP_FALLOFF_THRESHOLD 0.08
        #define HECTON_SHAFT_CHEAP_DRIVE_THRESHOLD 0.18
        #define HECTON_SHAFT_FAKE_RAYMARCH_STEP_CUTOFF 3.0
        #define HECTON_THERMAL_HAZE_CULL_SPEED_SQ 225.0
        #define HECTON_CONTACT_SHADOW_CULL_SPEED_SQ 225.0
        #ifndef UNITY_PASS_STEREO_INSTANCE_ID
        #define UNITY_PASS_STEREO_INSTANCE_ID(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
        #endif

        CBUFFER_START(HectonScooterVolumetricShaftsGlobals)
            float _HectonShaftPassMode;
            float _HectonShaftRenderScale;
            float _HectonShaftRaymarchSteps;
            float _HectonShaftMaxRayDistance;
            float _HectonShaftScatteringAnisotropy;
            float _HectonShaftDensity;
            float _HectonShaftIgnJitter;
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
            float2 _HectonNoirPadding0;
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
            float3 _HectonNoirPadding1;
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
        float4 _HectonFlashlightFailureState;
        float4 _HectonFlashlightVoxelHalfExtents;
        float4x4 _HectonFlashlightVoxelWorldToLocal;
        float4 _HectonCaveVoxelHalfExtents;
        float4 _HectonCaveVoxelInvDoubleHalfExtents;
        float4x4 _HectonCaveVoxelWorldToLocal;
        float4 _SunDirection;
        float4 _HectonScooterVelocityWS;
        float4 _HectonFloorBiolumColor;
        float4 _HectonShallowWaterFieldWorldRect;
        float4 _HectonCaveVoxelAoParams;
        float4 _GlobalDriftOffset;
        float4 _HectonXRFoveatedParams;
        float4 _BlitTexture_TexelSize;
        float4 _HectonShaftsTexture_TexelSize;
        float4 _HectonHalfResDepthTexture_TexelSize;
        float _EclipseOcclusion;
        float _HectonFreezeFrameDither;
        float _GamePaused;
        float _HectonFloorBiolumStrength;
        float _HectonShallowWaterFieldActive;
        float _HectonScooterBrakeCloud;
        float _HectonFlashlightActive;
        float _HectonFlashlightVoxelActive;
        float _HectonCaveVoxelActive;
        int _HectonRecentCutHeatCount;
        float4 _HectonRecentCutHeatPositionRadius[HECTON_RECENT_CUT_HEAT_MAX];
        float4 _HectonRecentCutHeatStrengthTime[HECTON_RECENT_CUT_HEAT_MAX];
        float4 _HectonSonarPrimaryPulse;
        float4 _HectonSonarEchoPulse;
        float4 _HectonSonarVisualParams;
        float4 _HectonSonarEchoParams;
        float4 _HectonSonarColor;
        float _SonarActive;

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D(_HectonShallowWaterFieldRT);
        SAMPLER(sampler_HectonShallowWaterFieldRT);
        TEXTURE2D_X(_HectonShaftsTexture);
        TEXTURE2D_X_FLOAT(_HectonHalfResDepthTexture);
        TEXTURE3D(_VoxelDensityTex);
        SAMPLER(sampler_VoxelDensityTex);
        TEXTURE3D(_HectonCaveVoxelSdfTex);
        SAMPLER(sampler_HectonCaveVoxelSdfTex);

        struct Attributes
        {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float2 ResolveXRStereoScreenUV(float2 screenUV)
        {
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
            return UnityStereoTransformScreenSpaceTex(screenUV);
#else
            return screenUV;
#endif
        }

        float SafeRcp(float value)
        {
            return value > 0.00001 ? rcp(value) : 0.0;
        }

        float FastNegativeExp(float x)
        {
            x = max(0.0, x);
            float x2 = x * x;
            return rcp(1.0 + x + 0.48 * x2 + 0.235 * x2 * x);
        }

        float FastNegativeExp2(float x)
        {
            return FastNegativeExp(x * 0.69314718);
        }

        float FastTrianglePulse01(float phase)
        {
            return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
        }

        float FastNoirPowerCurve(float value, float noirPower)
        {
            float baseCurve = saturate(value);
            float squaredCurve = baseCurve * baseCurve;
            float quarticCurve = squaredCurve * squaredCurve;
            float lowPowerBlend = saturate(2.0 - noirPower);
            float highPowerBlend = saturate((noirPower - 2.0) * 0.5);
            return lerp(lerp(squaredCurve, quarticCurve, highPowerBlend), baseCurve, lowPowerBlend);
        }

        float3 SafeNormalize3(float3 value)
        {
            float lenSq = dot(value, value);
            return lenSq > 0.00001 ? value * rsqrt(lenSq) : float3(0.0, 0.0, 1.0);
        }

        float2 ApproximateUnitDirectionDiamond(float2 value)
        {
            float2 absValue = abs(value);
            float invRadius = rcp(max(absValue.x + absValue.y, 0.0001));
            return value * invRadius;
        }

        float ResolveSpotConeAttenuation(float cosAngle, float innerCos, float outerCos)
        {
            float coneRange = max(innerCos - outerCos, 0.0001);
            return saturate((cosAngle - outerCos) / coneRange);
        }

        float ResolveXRCheapRangeAttenuation(float distanceSq, float invDistance, float inverseRange)
        {
            float rangeAttenuation;
            [branch]
            if (_HectonXRFoveatedParams.x > 0.5)
            {
                float inverseRangeSq = inverseRange * inverseRange;
                rangeAttenuation = saturate(1.0 - distanceSq * inverseRangeSq);
            }
            else
            {
                rangeAttenuation = saturate(1.0 - distanceSq * invDistance * inverseRange);
            }

            return rangeAttenuation * rangeAttenuation;
        }

        float ResolveVolumetricLightDistanceFade(float3 lightPositionWS)
        {
            float3 lightDelta = _WorldSpaceCameraPos - lightPositionWS;
            float lightDistanceSq = dot(lightDelta, lightDelta);
            float cullDistanceSq = HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE * HECTON_VOLUMETRIC_LIGHT_CULL_DISTANCE;
            float fadeStartSq = HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START * HECTON_VOLUMETRIC_LIGHT_CULL_FADE_START;
            float safeDistanceSq = isfinite(lightDistanceSq) ? max(lightDistanceSq, 0.0) : cullDistanceSq;
            float fadeRangeSq = max(cullDistanceSq - fadeStartSq, 0.0001);
            float fade = 1.0 - saturate((safeDistanceSq - fadeStartSq) * SafeRcp(fadeRangeSq));
            return max(fade, 0.0);
        }

        float HectonShaftAnimationWeight()
        {
            return 1.0 - saturate(_GamePaused);
        }

        float HectonShaftAnimationTime()
        {
            return _Time.y * HectonShaftAnimationWeight();
        }

        float ResolveInterleavedGradientNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float frameIndex = floor(_Time.y * 60.0);
            float2 temporalPhase = float2(fmod(frameIndex, 2.0), fmod(floor(frameIndex * 0.5), 2.0)) * 0.5;
            return frac(52.9829189 * frac(dot(pixel + temporalPhase, float2(0.06711056, 0.00583715))));
        }

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 34.45);
            return frac(p.x * p.y);
        }

        float ResolveFlashlightFailureFlicker(float3 surfacePositionWS)
        {
            float battery01 = saturate(_HectonFlashlightFailureState.x);
            float thermal01 = saturate(_HectonFlashlightFailureState.y);
            float failure01 = saturate(_HectonFlashlightFailureState.z);
            float lowBatteryDrop = saturate((0.22 - battery01) * 4.5454545);
            float cellNoise = Hash21(floor(surfacePositionWS.xz * lerp(0.65, 3.4, thermal01)) + floor(HectonShaftAnimationTime() * lerp(5.0, 23.0, failure01)));
            float carrier = FastTrianglePulse01(HectonShaftAnimationTime() * lerp(3.0, 16.0, failure01) + cellNoise * 6.2831853);
            float dropout = lerp(0.7, 0.2, max(lowBatteryDrop, thermal01 * thermal01));
            return saturate(lerp(1.0, lerp(dropout, 1.0, carrier * carrier), max(failure01, lowBatteryDrop)));
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

        half3 ApplyResolveIgnDither(half3 color, float noise)
        {
            float dither = (noise - 0.5) * (1.0 / 255.0);
            return max(color + (half)dither.xxx, 0.0h);
        }

        half3 ApplyFreezeFrameDither(half3 color, float4 positionCS, float noise)
        {
            half freeze = (half)saturate(_HectonFreezeFrameDither);
            if (freeze <= 0.0001h)
                return color;

            half scanline = (half)step(0.5, frac(positionCS.y * 0.5));
            half ditherMask = (half)step(noise, freeze);
            half3 frozenTint = color * 0.62h + half3(0.010h, 0.056h, 0.078h) * 0.38h;
            frozenTint += (((half)noise - 0.5h) * 0.072h) + (scanline * 0.024h);
            frozenTint *= lerp(1.0h, 0.74h + ditherMask * 0.26h, freeze);
            return lerp(color, frozenTint, freeze);
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

        float SampleHalfResContactDepth(float2 screenUV)
        {
            return SAMPLE_TEXTURE2D_X(_HectonHalfResDepthTexture, sampler_PointClamp, saturate(screenUV)).r;
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
                float surfaceDistanceSq = dot(toSurfaceWS, toSurfaceWS);
                if (surfaceDistanceSq >= lightRange * lightRange)
                    continue;

                float invSurfaceDistance = rsqrt(max(surfaceDistanceSq, 0.00001));
                float3 surfaceDirectionWS = toSurfaceWS * invSurfaceDistance;
                float3 lightDirectionWS = SafeNormalize3(_HectonScooterHeadlightDirectionsWS[lightIndex].xyz);
                float innerCos = _HectonScooterHeadlightDirectionsWS[lightIndex].w;
                float outerCos = _HectonScooterHeadlightConeData[lightIndex].x;
                float inverseRange = _HectonScooterHeadlightConeData[lightIndex].z;
                float coneAttenuation = ResolveSpotConeAttenuation(dot(lightDirectionWS, surfaceDirectionWS), innerCos, outerCos);
                float rangeAttenuation = ResolveXRCheapRangeAttenuation(surfaceDistanceSq, invSurfaceDistance, inverseRange);
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

            if (dot(_HectonScooterVelocityWS.xyz, _HectonScooterVelocityWS.xyz) > HECTON_CONTACT_SHADOW_CULL_SPEED_SQ)
                return 1.0;

            const int stepCount = 3;
            float3 biasedSurfacePositionWS = surfacePositionWS + normalWS * _HectonContactShadowBias;
            float4 surfaceCS = TransformWorldToHClip(surfacePositionWS);
            float jitter = surfaceCS.w > 0.0001
                ? frac(52.9829189 * frac(dot(surfaceCS.xy, float2(0.06711056, 0.00583715))))
                : 0.0;
            float shadowOcclusion = 0.0;

            [unroll]
            for (int stepIndex = 0; stepIndex < 3; stepIndex++)
            {
                float stepT = (stepIndex + 0.5 + jitter * 0.35) * 0.33333334;

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
                    float lightDistanceSq = dot(lightRayWS, lightRayWS);
                    if (lightDistanceSq <= 0.00000001)
                        continue;

                    float invLightDistance = rsqrt(max(lightDistanceSq, 0.00001));
                    float lightDistance = lightDistanceSq * invLightDistance;
                    float marchDistance = min(lightDistance, _HectonContactShadowMaxDistance);
                    float marchT = stepT * invLightDistance * marchDistance;
                    float3 raySampleWS = lerp(biasedSurfacePositionWS, lightPositionWS, marchT);
                    float4 raySampleCS = TransformWorldToHClip(raySampleWS);
                    if (raySampleCS.w <= 0.0)
                        continue;

                    float2 raySampleUV = raySampleCS.xy * SafeRcp(raySampleCS.w) * 0.5 + 0.5;
                    if (raySampleUV.x <= 0.0 || raySampleUV.x >= 1.0 || raySampleUV.y <= 0.0 || raySampleUV.y >= 1.0)
                        continue;

                    float sampledRawDepth = SampleHalfResContactDepth(raySampleUV);
                #if UNITY_REVERSED_Z
                    float sampledDepthValid = step(0.0001, sampledRawDepth);
                #else
                    float sampledDepthValid = step(sampledRawDepth, 0.9999);
                #endif
                    if (sampledDepthValid <= 0.5)
                        continue;

                    float3 sampledScenePositionWS = ComputeWorldSpacePosition(raySampleUV, sampledRawDepth, UNITY_MATRIX_I_VP);
                    float3 sceneCameraDelta = sampledScenePositionWS - _WorldSpaceCameraPos;
                    float3 rayCameraDelta = raySampleWS - _WorldSpaceCameraPos;
                    float depthBiasSq = _HectonContactShadowBias * _HectonContactShadowBias * 0.25;
                    float occluded = step(dot(sceneCameraDelta, sceneCameraDelta) + depthBiasSq, dot(rayCameraDelta, rayCameraDelta));
                    if (occluded <= 0.5)
                        continue;

                    float3 surfaceVectorWS = surfacePositionWS - lightPositionWS;
                    float3 surfaceDirectionWS = SafeNormalize3(surfaceVectorWS);
                    float3 lightDirectionWS = SafeNormalize3(_HectonScooterHeadlightDirectionsWS[lightIndex].xyz);
                    float innerCos = _HectonScooterHeadlightDirectionsWS[lightIndex].w;
                    float outerCos = _HectonScooterHeadlightConeData[lightIndex].x;
                    float inverseRange = _HectonScooterHeadlightConeData[lightIndex].z;
                    float coneAttenuation = ResolveSpotConeAttenuation(dot(lightDirectionWS, surfaceDirectionWS), innerCos, outerCos);
                    float clampedLightDistanceSq = min(lightDistanceSq, lightRange * lightRange);
                    float invClampedLightDistance = rsqrt(max(clampedLightDistanceSq, 0.00001));
                    float rangeAttenuation = ResolveXRCheapRangeAttenuation(clampedLightDistanceSq, invClampedLightDistance, inverseRange);
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

            float3 invDoubleHalfExtents = _HectonCaveVoxelInvDoubleHalfExtents.xyz;
            if (any(invDoubleHalfExtents <= 0.0))
                return _HectonCaveVoxelHalfExtents.w;

            float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
            float3 sampleUv = localPosition * invDoubleHalfExtents + 0.5;
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

            int stepCount = clamp((int)(_HectonFlashlightShadowSteps + 0.5), 1, HECTON_FLASHLIGHT_SHADOW_EVAL_MAX);
            float minStep = max(_HectonFlashlightShadowMinStep, 0.01);
            float shadowFloor = ResolveFlashlightShadowFloor();
            float result = 1.0;
            float travel = minStep;

            [loop]
            for (int stepIndex = 0; stepIndex < HECTON_FLASHLIGHT_SHADOW_EVAL_MAX; stepIndex++)
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
            float surfaceDistanceSq = dot(toSurfaceWS, toSurfaceWS);
            if (surfaceDistanceSq <= 0.00000001 || surfaceDistanceSq >= lightRange * lightRange)
                return 0.0;

            float invSurfaceDistance = rsqrt(max(surfaceDistanceSq, 0.00001));
            float3 surfaceDirectionWS = toSurfaceWS * invSurfaceDistance;
            float3 lightDirectionWS = SafeNormalize3(_HectonFlashlightDirectionWS.xyz);
            float coneAttenuation = ResolveSpotConeAttenuation(
                dot(lightDirectionWS, surfaceDirectionWS),
                _HectonFlashlightDirectionWS.w,
                _HectonFlashlightConeData.x);
            if (coneAttenuation <= 0.0001)
                return 0.0;

            float rangeAttenuation = ResolveXRCheapRangeAttenuation(surfaceDistanceSq, invSurfaceDistance, _HectonFlashlightConeData.z);
            float noL = saturate(dot(normalWS, -surfaceDirectionWS));
            float failureFlicker = ResolveFlashlightFailureFlicker(surfacePositionWS);
            return coneAttenuation * rangeAttenuation * noL * saturate(_HectonFlashlightColor.w * failureFlicker * 0.35);
        }

        float EvaluateFlashlightSurfaceShadow(float3 surfacePositionWS, float3 normalWS)
        {
            if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
                return 1.0;

            float3 lightRayWS = _HectonFlashlightPositionWS.xyz - surfacePositionWS;
            float lightDistanceSq = dot(lightRayWS, lightRayWS);
            if (lightDistanceSq <= 0.00000001)
                return 1.0;

            float invLightDistance = rsqrt(max(lightDistanceSq, 0.00001));
            float lightDistance = lightDistanceSq * invLightDistance;
            float3 rayDirectionWS = lightRayWS * invLightDistance;
            float3 rayOriginWS = surfacePositionWS + normalWS * _HectonFlashlightShadowBias;
            float rayLength = max(lightDistance - _HectonFlashlightShadowBias, 0.0);
            return EvaluateFlashlightVoxelShadowRay(rayOriginWS, rayDirectionWS, rayLength);
        }

        float2 ResolveSunScreenUv(out float visibility);
        float ResolveBrightLensDrive();

        half3 SampleBrightShaftSource(float2 sampleUV)
        {
            half3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(sampleUV)).rgb;
            float luminance = dot(source, float3(0.2126, 0.7152, 0.0722));
            float threshold = lerp(1.12, 0.68, saturate(_HectonShaftDensity * 0.25));
            float brightMask = saturate((luminance - threshold) * 1.65);
            brightMask *= brightMask;
            return source * brightMask;
        }

        half3 IntegrateHeadlightShafts(float2 screenUV)
        {
            float drive = ResolveBrightLensDrive();
            if (drive <= 0.0001 || _HectonShaftIntensity <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float sunVisibility;
            float2 originUV = ResolveSunScreenUv(sunVisibility);
            float originOnScreen =
                step(0.0, originUV.x) * step(originUV.x, 1.0) *
                step(0.0, originUV.y) * step(originUV.y, 1.0);
            if (originOnScreen <= 0.0)
                originUV = float2(0.5, 0.5);

            float2 radial = screenUV - originUV;
            float radialDistanceSq = dot(radial, radial);
            float radialFalloff = saturate(1.0 - radialDistanceSq * 1.65);
            float jitter = (ResolveInterleavedGradientNoise(screenUV) - 0.5) * _HectonShaftIgnJitter * 0.12;

            if (radialFalloff <= HECTON_SHAFT_CHEAP_FALLOFF_THRESHOLD ||
                drive <= HECTON_SHAFT_CHEAP_DRIVE_THRESHOLD ||
                _HectonShaftRaymarchSteps <= HECTON_SHAFT_FAKE_RAYMARCH_STEP_CUTOFF)
            {
                float fakeTapT = saturate(0.38 + jitter * 0.5);
                float2 fakeSampleUV = lerp(screenUV, originUV, fakeTapT);
                half3 fakeSource = SampleBrightShaftSource(fakeSampleUV);
                float fakeIntensity = radialFalloff * radialFalloff * lerp(0.12, 0.42, saturate(drive * 5.0));
                return fakeSource * (_HectonShaftIntensity * drive * fakeIntensity);
            }

            const int taps = 8;
            half3 accumulated = half3(0.0, 0.0, 0.0);
            float weightSum = 0.0;

            [unroll(8)]
            for (int tapIndex = 0; tapIndex < taps; tapIndex++)
            {
                float tapT = saturate(((float)tapIndex + 0.5 + jitter) * SafeRcp((float)taps));
                float2 sampleUV = lerp(screenUV, originUV, tapT);
                float weight = 1.0 - tapT;
                weight *= weight;
                accumulated += SampleBrightShaftSource(sampleUV) * weight;
                weightSum += weight;
            }

            half3 shafts = accumulated * SafeRcp(max(weightSum, 0.0001));
            return shafts * (_HectonShaftIntensity * drive * lerp(0.25, 1.0, radialFalloff));
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
                float bilateralWeight = FastNegativeExp2(depthDelta * max(0.01, _HectonShaftBilateralDepthSigma));
                float foregroundDelta = max(0.0, centerDepth - sampleDepth);
                float foregroundReject = FastNegativeExp2(foregroundDelta * max(0.01, _HectonShaftBilateralDepthSigma) * 4.0);
                float weight = spatialWeight * bilateralWeight * foregroundReject;
                accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV).rgb * weight;
                weightSum += weight;
            }

            return half4(accumulated * SafeRcp(weightSum), 1.0);
        }

        float ResolveNoirFogFactor(float linearEyeDepth)
        {
            float depthLinear = max(0.0, linearEyeDepth);
            float fogBase = saturate(1.0 - FastNegativeExp(depthLinear * max(_HectonNoirFogDensity, 0.0001)));
            return saturate(FastNoirPowerCurve(fogBase, max(_HectonNoirPower, 0.001)));
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
            float2 aberrationDirection = ApproximateUnitDirectionDiamond(flareVector);
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
            float animationTime = HectonShaftAnimationTime();
            float drops = smoothstep(0.84, 0.995, ValueNoise2D(artifactUV * 54.0 + float2(1.9, animationTime * 0.035)));
            float streaks = smoothstep(
                0.76,
                0.975,
                ValueNoise2D(float2(artifactUV.x * 84.0 + animationTime * 0.018, artifactUV.y * 12.0 - animationTime * 0.045)));

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
            float animationTime = HectonShaftAnimationTime();
            [unroll]
            for (int heatIndex = 0; heatIndex < HECTON_RECENT_CUT_HEAT_EVAL_MAX; heatIndex++)
            {
                if (heatIndex >= _HectonRecentCutHeatCount)
                    break;

                float4 positionRadius = _HectonRecentCutHeatPositionRadius[heatIndex];
                float4 strengthTime = _HectonRecentCutHeatStrengthTime[heatIndex];
                float radius = max(positionRadius.w, 0.001);
                float age01 = saturate((animationTime - strengthTime.y) * SafeRcp(max(strengthTime.z, 0.001)));
                float3 heatDelta = scenePositionWS - positionRadius.xyz;
                float radiusSq = max(radius * radius, 0.000001);
                float spatialMask = saturate(1.0 - dot(heatDelta, heatDelta) * SafeRcp(radiusSq));
                hazeWeight += spatialMask * spatialMask * max(strengthTime.x, 0.0) * (1.0 - age01);
            }

            return saturate(hazeWeight);
        }

        float2 EvaluateThermalHazeOffset(float2 screenUV, float depthValid, float3 scenePositionWS)
        {
            if (_HectonThermalHazeIntensity <= 0.000001 || depthValid <= 0.5)
                return float2(0.0, 0.0);

            if (dot(_HectonScooterVelocityWS.xyz, _HectonScooterVelocityWS.xyz) > HECTON_THERMAL_HAZE_CULL_SPEED_SQ)
                return float2(0.0, 0.0);

            float heatWeight = ResolveRecentHeatHazeWeight(scenePositionWS);
            if (heatWeight <= 0.0001)
                return float2(0.0, 0.0);

            float2 quarterResPixels = max(_ScaledScreenParams.xy * 0.25, float2(1.0, 1.0));
            float2 lowResCell = floor(screenUV * quarterResPixels * max(_HectonThermalHazeScale, 0.001));
            float animationTime = HectonShaftAnimationTime();
            float2 noiseCoord = lowResCell * 0.067 + float2(animationTime * 0.21, -animationTime * 0.17);
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
                float spatialWeight = FastNegativeExp2(dot(sampleDelta, sampleDelta));
                float weight = spatialWeight;
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

        half3 EvaluateBiolumFloorProjection(
            float2 screenUV,
            float depthValid,
            float3 scenePositionWS,
            float linearEyeDepth,
            float3 normalWS)
        {
            if (_HectonFloorBiolumStrength <= 0.0001 || depthValid <= 0.5)
                return half3(0.0, 0.0, 0.0);

            float floorMask = saturate((normalWS.y - 0.42) * 2.4);
            if (floorMask <= 0.0001)
                return half3(0.0, 0.0, 0.0);

            float patternScale = max(0.001, _HectonBiolumPatternScale);
            float2 patternCoord = scenePositionWS.xz * patternScale;
            float animationTime = HectonShaftAnimationTime();
            float pattern =
                (FastTrianglePulse01(patternCoord.x + animationTime * 0.72) * 2.0 - 1.0) * 0.5 +
                (FastTrianglePulse01(patternCoord.y * 1.13 - animationTime * 0.57 + 1.5707963) * 2.0 - 1.0) * 0.35 +
                (FastTrianglePulse01((patternCoord.x + patternCoord.y) * 0.74 + animationTime * 1.11) * 2.0 - 1.0) * 0.15;
            pattern = saturate(pattern * 0.5 + 0.5);
            pattern = smoothstep(0.28, 0.92, pattern);
            float distanceFade = FastNegativeExp2(linearEyeDepth * 0.004);
            return _HectonFloorBiolumColor.rgb * (_HectonFloorBiolumStrength * _HectonBiolumProjectionStrength * floorMask * pattern * distanceFade);
        }

        half3 ApplyAbyssalSensorEdgePulse(float2 screenUV, half3 color, half3 noirMinimum)
        {
            float2 centered = screenUV - 0.5;
            half edgeMask = saturate((half)dot(centered, centered) * 3.4h);
            float animationTime = HectonShaftAnimationTime();
            half scan = smoothstep(0.82h, 1.0h, (half)FastTrianglePulse01(screenUV.y * 720.0 + animationTime * 19.0));
            half grain = (half)ValueNoise2D(screenUV * float2(94.0, 53.0) + float2(animationTime, animationTime) * 0.17);
            half pulse = edgeMask * scan * saturate(grain * 1.35h - 0.28h) * 0.035h;
            half3 shifted = half3(color.r * 0.96h, color.g * 1.015h, color.b * 1.04h);
            return max(lerp(color, shifted + _HectonNoirLiftColor.rgb * 0.08h, pulse), noirMinimum);
        }

        float ResolveSceneDepthEdgeWeight(float2 screenUV, float rawDepth, float centerLinearEyeDepth)
        {
            float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / max(_ScaledScreenParams.xy, float2(1.0, 1.0)));
            float depthDx = SampleSceneDepth(saturate(screenUV + float2(texel.x, 0.0)));
            float depthDy = SampleSceneDepth(saturate(screenUV + float2(0.0, texel.y)));
            float depthGradient = abs(depthDx - rawDepth) + abs(depthDy - rawDepth);
            float adaptiveThreshold = max(0.000025, 0.00045 * rcp(1.0 + centerLinearEyeDepth * 0.035));
            return smoothstep(adaptiveThreshold, adaptiveThreshold * 4.0, depthGradient);
        }

        float ApproximatePulseDistance(float3 a, float3 b)
        {
            float3 delta = abs(a - b);
            float maxAxis = max(delta.x, max(delta.y, delta.z));
            float minAxis = min(delta.x, min(delta.y, delta.z));
            float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375 + minAxis * 0.125;
        }

        float EvaluateSonarPulseBand(float4 pulse, float4 parameters, float3 scenePositionWS, float ageOffset, float intensityScale)
        {
            float active = saturate(_SonarActive) * saturate(parameters.w);
            if (active <= 0.0001)
                return 0.0;

            float speed = max(parameters.x, 0.01);
            float maxRadius = max(parameters.y, 0.01);
            float bandWidth = max(parameters.z, 0.05);
            float age = HectonShaftAnimationTime() - pulse.w - ageOffset;
            if (age <= 0.0)
                return 0.0;

            float radius = age * speed;
            float lifeMask = 1.0 - saturate((radius - maxRadius) * SafeRcp(bandWidth));
            if (lifeMask <= 0.0001)
                return 0.0;

            float distanceToOrigin = ApproximatePulseDistance(scenePositionWS, pulse.xyz);
            float band = saturate(1.0 - abs(distanceToOrigin - radius) * SafeRcp(bandWidth));
            band = band * band * (3.0 - 2.0 * band);
            float cinematicFalloff = rcp(1.0 + distanceToOrigin * 0.006);
            return band * lifeMask * active * intensityScale * cinematicFalloff;
        }

        half3 EvaluateAcousticSonarOverlay(float2 screenUV, float depthValid, float3 scenePositionWS, float rawDepth, float linearEyeDepth)
        {
            if (_SonarActive <= 0.0001 || depthValid <= 0.5)
                return half3(0.0h, 0.0h, 0.0h);

            float edgeWeight = ResolveSceneDepthEdgeWeight(screenUV, rawDepth, linearEyeDepth);
            float primary = EvaluateSonarPulseBand(_HectonSonarPrimaryPulse, _HectonSonarVisualParams, scenePositionWS, 0.0, 1.0);
            float4 automaticEchoParams = float4(
                max(_HectonSonarVisualParams.x * 0.72, 0.01),
                _HectonSonarVisualParams.y,
                max(_HectonSonarVisualParams.z * 1.75, 0.05),
                _HectonSonarVisualParams.w * 0.32);
            float automaticEcho = EvaluateSonarPulseBand(
                _HectonSonarPrimaryPulse,
                automaticEchoParams,
                scenePositionWS,
                0.06 + edgeWeight * 0.035,
                edgeWeight);
            float eventEcho = EvaluateSonarPulseBand(_HectonSonarEchoPulse, _HectonSonarEchoParams, scenePositionWS, 0.0, 1.0);
            float contourBoost = max(_HectonSonarColor.w, 0.0);
            float contour = edgeWeight * (primary + eventEcho) * contourBoost;
            float fill = primary * 0.18 + automaticEcho * 0.55 + eventEcho * 0.35;
            float grain = lerp(0.82, 1.14, ValueNoise2D(screenUV * float2(173.0, 91.0) + HectonShaftAnimationTime() * 0.31));
            float sonar = saturate(contour + fill) * grain;
            return half3(_HectonSonarColor.rgb * sonar);
        }

        half4 FragScreenSpaceShafts(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            return half4(IntegrateHeadlightShafts(screenUV), 1.0);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            return BlurShafts(screenUV, float2(1.0, 0.0));
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            return BlurShafts(screenUV, float2(0.0, 1.0));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            float rawDepth;
            float depthValid;
            float3 scenePositionWS;
            float linearEyeDepth;
            ResolveDepthData(screenUV, rawDepth, depthValid, scenePositionWS, linearEyeDepth);
            float2 sourceUV = screenUV;
            [branch]
            if (_HectonThermalHazeIntensity > 0.000001 && depthValid > 0.5)
                sourceUV = saturate(screenUV + EvaluateThermalHazeOffset(screenUV, depthValid, scenePositionWS));
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV);
            float exposureMultiplier = ResolveExposureMultiplier();
            half3 noirMinimum = ResolveNoirMinimumColor();
            sourceColor.rgb *= exposureMultiplier;
            half3 shafts = half3(0.0h, 0.0h, 0.0h);
            if (_HectonShaftIntensity > 0.0001)
                shafts = BilateralUpsampleShafts(screenUV, linearEyeDepth) * exposureMultiplier;

            half3 extinctionColor = half3(1.0h, 1.0h, 1.0h);
            half3 biolumProjection = half3(0.0h, 0.0h, 0.0h);
            half3 lensGhosts = half3(0.0h, 0.0h, 0.0h);
            if (_HectonLensGhostIntensity > 0.0001)
                lensGhosts = EvaluateProceduralLensArtifacts(screenUV) * exposureMultiplier;

            half3 lensDirtCondensation = half3(0.0h, 0.0h, 0.0h);
            if ((_HectonLensDirtIntensity + _HectonCondensationIntensity) > 0.0001)
                lensDirtCondensation = EvaluateLensDirtCondensation(screenUV, sourceColor.rgb) * exposureMultiplier;

            half3 acousticSonarOverlay = EvaluateAcousticSonarOverlay(screenUV, depthValid, scenePositionWS, rawDepth, linearEyeDepth) * exposureMultiplier;
            if (depthValid > 0.5)
            {
                extinctionColor = H8WaterExtinctionResolveRgbByDepthMeters(linearEyeDepth, (half)_ExtinctionLUTRuntime.y);
                shafts *= extinctionColor;
                float3 normalWS = ApproximateWorldNormal(screenUV, scenePositionWS);
                biolumProjection = EvaluateBiolumFloorProjection(screenUV, depthValid, scenePositionWS, linearEyeDepth, normalWS) * exposureMultiplier;
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

            half3 finalColor = sourceColor.rgb + shafts + biolumProjection + lensGhosts + lensDirtCondensation + acousticSonarOverlay;
            finalColor = max(finalColor, noirMinimum);
            finalColor = ApplyAbyssalSensorEdgePulse(screenUV, finalColor, noirMinimum);
            finalColor = min(finalColor, half3(64.0h, 64.0h, 64.0h));
            float resolveNoise = ResolveInterleavedGradientNoise(screenUV);
            finalColor = ApplyFreezeFrameDither(finalColor, input.positionCS, resolveNoise);
            finalColor = ApplyResolveIgnDither(finalColor, resolveNoise);
            return half4(finalColor, sourceColor.a);
        }

        float4 FragDownsampleDepth(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            return SampleSceneDepth(screenUV).xxxx;
        }
        ENDHLSL

        Pass
        {
            Name "ScreenSpaceShafts"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragScreenSpaceShafts
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "HalfResContactDepth"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsampleDepth
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
    }

    FallBack Off
}

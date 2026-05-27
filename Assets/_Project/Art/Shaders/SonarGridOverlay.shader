Shader "Hidden/Hecton8/SonarGridOverlay"
{
    Properties
    {
        _OverlayOpacity ("Overlay Opacity", Range(0, 2)) = 1.0
        _TopoBandScale ("Topographic Band Scale", Range(0.01, 2)) = 0.12
        _TopoBandWidth ("Topographic Band Width", Range(0.001, 0.2)) = 0.028
        _PulseBoost ("Contact Pulse Boost", Range(0, 4)) = 1.45
        _PersistenceSeconds ("Point Cloud Persistence", Range(0.5, 15)) = 12.0
        _PointDensity ("Point Cloud Density", Range(0.05, 4)) = 1.15
        _PointBoost ("Point Cloud Boost", Range(0, 4)) = 1.35
        _WorldPersistenceSeconds ("World Point Cloud Persistence", Range(0.5, 15)) = 12.0
        _WorldPointRadius ("World Point Radius", Range(0.1, 8)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.5

        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

        #ifndef UNITY_PASS_STEREO_INSTANCE_ID
        #define UNITY_PASS_STEREO_INSTANCE_ID(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
        #endif

        CBUFFER_START(UnityPerMaterial)
            half _OverlayOpacity;
            half _TopoBandScale;
            half _TopoBandWidth;
            half _PulseBoost;
            half _PersistenceSeconds;
            half _PointDensity;
            half _PointBoost;
            half _HasHistory;
            half _WorldPersistenceSeconds;
            half _WorldPointRadius;
            half _HasWorldHistory;
        CBUFFER_END

        float4 _SonarRevealOriginWS;
        float4 _SonarRevealWaveParams;
        float4 _SonarPingCenter;
        float4 _SonarPingParams;
        float _SonarRadius;
        float _SonarWaveFront;
        float _LidarPersistence;
        float4 _SonarGridParams0;
        float4 _SonarGridHardColor;
        float4 _SonarGridOrganicColor;
        float4 _SonarGridAbyssalColor;
        float4 _HectonSonarWorldMemoryRect;
        float4 _HectonSonarWorldScrollUvOffset;
        float4 _HectonSonarWorldOriginOffset;
        float4 _HectonSonarPrimaryPulse;
        float4 _HectonSonarEchoPulse;
        float4 _HectonSonarVisualParams;
        float4 _HectonSonarEchoParams;
        float4 _HectonSonarColor;
        float _SonarActive;
        float _AbyssalDistortion;
        float _SonarRevealExpireTime;

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonSonarHistoryTex);
        TEXTURE2D(_HectonSonarWorldHistoryTex);
        TEXTURE2D(_HectonSonarWorldPointCloudRT);

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

        struct SonarPointData
        {
            half3 color;
            half alpha;
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

        float Hash21(float2 value)
        {
            float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
            hash += dot(hash, hash.yzx + 33.33);
            return frac((hash.x + hash.y) * hash.z);
        }

        float Hash31(float3 value)
        {
            float3 hash = frac(value * float3(0.1031, 0.1030, 0.0973));
            hash += dot(hash, hash.yzx + 33.33);
            return frac((hash.x + hash.y) * hash.z);
        }

        float DitherNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            return Hash21(pixel);
        }

        float2 ClampSceneDepthUVWithTexel(float2 screenUV, float2 texel)
        {
            return clamp(screenUV, texel, 1.0 - texel);
        }

        float ResolveLinearRamp01(float edge0, float edge1, float value)
        {
            return saturate((value - edge0) * rcp(max(0.0001, edge1 - edge0)));
        }

        float2 ResolveFoveatedSourceUV(float2 uv)
        {
            return FoveatedRemapLinearToNonUniform(saturate(uv));
        }

        float2 ClampSceneDepthUV(float2 screenUV)
        {
            float2 texel = rcp(max(_ScaledScreenParams.xy, float2(1.0, 1.0)));
            return ClampSceneDepthUVWithTexel(screenUV, texel);
        }

        float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float validMask)
        {
            screenUV = ClampSceneDepthUV(screenUV);
            rawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
#if UNITY_REVERSED_Z
            validMask = step(0.0001, rawDepth);
#else
            validMask = step(rawDepth, 0.9999);
#endif
            return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
        }

        float ComputeSonarContourMask(float2 screenUV, float rawDepth)
        {
            float2 texel = rcp(max(_ScaledScreenParams.xy, float2(1.0, 1.0)));
            screenUV = ClampSceneDepthUVWithTexel(screenUV, texel);
            float2 leftUv = ClampSceneDepthUVWithTexel(screenUV - float2(texel.x, 0.0), texel);
            float2 rightUv = ClampSceneDepthUVWithTexel(screenUV + float2(texel.x, 0.0), texel);
            float2 downUv = ClampSceneDepthUVWithTexel(screenUV - float2(0.0, texel.y), texel);
            float2 upUv = ClampSceneDepthUVWithTexel(screenUV + float2(0.0, texel.y), texel);
            float depthLeft = SampleSceneDepth(ResolveFoveatedSourceUV(leftUv));
            float depthRight = SampleSceneDepth(ResolveFoveatedSourceUV(rightUv));
            float depthDown = SampleSceneDepth(ResolveFoveatedSourceUV(downUv));
            float depthUp = SampleSceneDepth(ResolveFoveatedSourceUV(upUv));
            float depthDx = depthRight - depthLeft;
            float depthDy = depthUp - depthDown;
            float depthGradient = abs(depthDx) + abs(depthDy);
            float oneSidedGradient = abs(depthRight - rawDepth) + abs(depthUp - rawDepth);
            return saturate(max(depthGradient, oneSidedGradient) * max(1.0, _SonarGridParams0.w) * 180.0);
        }

        float ComputeSonarGridMask(float3 sceneWorldPos)
        {
            float lineScale = max(0.1, _SonarGridParams0.y);
            float lineWidth = max(0.001, _SonarGridParams0.z);
            float2 cell = abs(frac(sceneWorldPos.xz * lineScale) - 0.5);
            return 1.0 - ResolveLinearRamp01(lineWidth, lineWidth * 2.5, min(cell.x, cell.y));
        }

        float ComputeTopographicBands(float3 sceneWorldPos)
        {
            float bandScale = max(0.01, _TopoBandScale);
            float bandWidth = max(0.001, _TopoBandWidth);
            float bandCoord = abs(frac(sceneWorldPos.y * bandScale) - 0.5);
            return 1.0 - ResolveLinearRamp01(bandWidth, bandWidth * 2.6, bandCoord);
        }

        float ResolveWavefrontMask(float distanceToOrigin, float waveRadius, float waveBandWidth)
        {
            return 1.0 - ResolveLinearRamp01(waveBandWidth, waveBandWidth * 2.0, abs(distanceToOrigin - waveRadius));
        }

        float ApproximatePulseDistance(float3 a, float3 b)
        {
            float3 delta = abs(a - b);
            float maxAxis = max(delta.x, max(delta.y, delta.z));
            float minAxis = min(delta.x, min(delta.y, delta.z));
            float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375 + minAxis * 0.125;
        }

        float ApproximatePulseDistance2D(float2 a, float2 b)
        {
            float2 delta = abs(a - b);
            float maxAxis = max(delta.x, delta.y);
            float minAxis = min(delta.x, delta.y);
            return maxAxis + minAxis * 0.375;
        }

        float EvaluateScreenSpacePulseBand(float4 pulse, float4 parameters, float3 sceneWorldPos, float ageOffset, float intensityScale)
        {
            float active = saturate(_SonarActive) * saturate(parameters.w) * saturate(intensityScale);
            if (active <= 0.0001)
                return 0.0;

            float speed = max(parameters.x, 0.01);
            float maxRadius = max(parameters.y, 0.01);
            float bandWidth = max(parameters.z, 0.05);
            float invBandWidth = rcp(bandWidth);
            float age = _Time.y - pulse.w - ageOffset;
            if (age <= 0.0)
                return 0.0;

            float radius = age * speed;
            float lifeMask = 1.0 - saturate((radius - maxRadius) * invBandWidth);
            if (lifeMask <= 0.0001)
                return 0.0;

            float distanceToPulse = ApproximatePulseDistance(sceneWorldPos, pulse.xyz);
            float band = saturate(1.0 - abs(distanceToPulse - radius) * invBandWidth);
            band = band * band * (3.0 - 2.0 * band);
            float cinematicFalloff = rcp(1.0 + distanceToPulse * 0.004);
            return band * lifeMask * active * cinematicFalloff;
        }

        SonarPointData EvaluateSonarPointCloud(float2 screenUV)
        {
            SonarPointData pointData;
            pointData.color = half3(0.0, 0.0, 0.0);
            pointData.alpha = 0.0;

            float sonarGridIntensity = saturate(_SonarGridParams0.x);
            if (sonarGridIntensity <= 0.0001)
                return pointData;

            float rawDepth;
            float depthValid;
            float3 sceneWorldPos = SampleSceneWorldPosition(screenUV, rawDepth, depthValid);
            if (depthValid <= 0.5)
                return pointData;

            float sonarWaveSpeed = max(0.01, _SonarRevealWaveParams.y);
            float invSonarWaveSpeed = rcp(sonarWaveSpeed);
            float sonarFadeDuration = max(0.05, _SonarRevealWaveParams.z);
            float invSonarFadeDuration = rcp(sonarFadeDuration);
            float overlayLifetimeMask = step(
                _Time.y,
                _SonarRevealWaveParams.x + (_SonarRevealOriginWS.w * invSonarWaveSpeed) + sonarFadeDuration);

            float distanceToOrigin = ApproximatePulseDistance(sceneWorldPos, _SonarRevealOriginWS.xyz);
            float timeSinceArrival = _Time.y - (_SonarRevealWaveParams.x + distanceToOrigin * invSonarWaveSpeed);
            float arrivalMask = step(0.0, timeSinceArrival);
            float terrainFade = arrivalMask * saturate(1.0 - (timeSinceArrival * invSonarFadeDuration));
            float waveRadius = max(0.0, _SonarWaveFront);
            float waveBandWidth = lerp(6.0, 2.0, saturate(_SonarRevealWaveParams.w));
            float waveFront = ResolveWavefrontMask(distanceToOrigin, waveRadius, waveBandWidth);
            float contourMask = ComputeSonarContourMask(screenUV, rawDepth);
            float planarGridMask = ComputeSonarGridMask(sceneWorldPos);
            float topoBandMask = ComputeTopographicBands(sceneWorldPos);
            float topoGridMask = saturate(max(planarGridMask, topoBandMask * 0.82));
            float primaryVisualWave = EvaluateScreenSpacePulseBand(_HectonSonarPrimaryPulse, _HectonSonarVisualParams, sceneWorldPos, 0.0, 1.0);
            float4 automaticEchoParams = float4(
                max(_HectonSonarVisualParams.x * 0.72, 0.01),
                _HectonSonarVisualParams.y,
                max(_HectonSonarVisualParams.z * 1.75, 0.05),
                _HectonSonarVisualParams.w * 0.32);
            float automaticEchoWave = EvaluateScreenSpacePulseBand(
                _HectonSonarPrimaryPulse,
                automaticEchoParams,
                sceneWorldPos,
                0.06 + contourMask * 0.035,
                contourMask);
            float eventEchoWave = EvaluateScreenSpacePulseBand(_HectonSonarEchoPulse, _HectonSonarEchoParams, sceneWorldPos, 0.0, 1.0);
            float reflectedWave = saturate(automaticEchoWave * 0.72 + eventEchoWave);
            float screenSpaceWave = saturate(primaryVisualWave + reflectedWave);
            float edgePulseMask = saturate(contourMask * (0.65 + screenSpaceWave * 2.75));
            float cinematicRidge = screenSpaceWave * (0.18 + edgePulseMask * 1.85);
            float terrainGrid =
                topoGridMask *
                max(contourMask, 0.14) *
                max(terrainFade, waveFront * 0.9) *
                step(_Time.y, _SonarRevealExpireTime) *
                overlayLifetimeMask;

            float hardAccum = terrainGrid * 0.55;
            float organicAccum = terrainGrid * 0.18;
            float abyssalAccum = 0.0;
            hardAccum += (primaryVisualWave * 0.28 + reflectedWave * max(0.28, contourMask) + cinematicRidge * 0.48) *
                max(contourMask, topoGridMask * 0.18) *
                1.15;
            organicAccum += cinematicRidge * contourMask * 0.22;
            abyssalAccum += reflectedWave * edgePulseMask * 0.18;

            float hardStrength = saturate(hardAccum);
            float organicStrength = saturate(organicAccum);
            float abyssalStrength = saturate(abyssalAccum);
            float compositeMask =
                sonarGridIntensity *
                saturate(max(max(hardStrength, organicStrength), abyssalStrength) +
                    waveFront * contourMask * 0.4 +
                    screenSpaceWave * max(0.15, contourMask) * 0.6);
            if (compositeMask <= 0.0001)
                return pointData;

            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            float pointHash = Hash31(float3(pixel.xy, floor(_Time.y * 10.0)));
            float pointThreshold = saturate(compositeMask * _PointDensity);
            float contourBias = saturate(contourMask * 0.85 + topoGridMask * 0.45);
            float pointMask = step(pointHash, saturate(pointThreshold * contourBias));
            if (pointMask <= 0.5)
                return pointData;

            half3 overlayColor =
                (_SonarGridHardColor.rgb * hardStrength) +
                (_SonarGridOrganicColor.rgb * organicStrength) +
                (_SonarGridAbyssalColor.rgb * abyssalStrength);

            float lidarFlash = saturate(_LidarPersistence);
            pointData.color = overlayColor * (half)(compositeMask * _PointBoost * lerp(1.0, 1.65, lidarFlash));
            pointData.alpha = (half)saturate(compositeMask + lidarFlash * 0.18);
            return pointData;
        }

        float3 ResolveAbsoluteWorldPosition(float3 sceneWorldPos)
        {
            return sceneWorldPos + _HectonSonarWorldOriginOffset.xyz;
        }

        float2 ResolveWorldMemoryUv(float3 absoluteWorldPos)
        {
            return float2(
                (absoluteWorldPos.x - _HectonSonarWorldMemoryRect.x) * _HectonSonarWorldMemoryRect.z,
                (absoluteWorldPos.z - _HectonSonarWorldMemoryRect.y) * _HectonSonarWorldMemoryRect.w);
        }

        float2 ResolveWorldMemoryAbsoluteXZ(float2 worldUv)
        {
            float2 worldSize = rcp(max(_HectonSonarWorldMemoryRect.zw, float2(0.000001, 0.000001)));
            return _HectonSonarWorldMemoryRect.xy + worldUv * worldSize;
        }

        half4 SampleWorldHistory(float2 worldUv)
        {
            if (_HasWorldHistory <= 0.5h)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            float2 historyUv = worldUv + _HectonSonarWorldScrollUvOffset.xy;
            if (historyUv.x < 0.0 || historyUv.x > 1.0 || historyUv.y < 0.0 || historyUv.y > 1.0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            return SAMPLE_TEXTURE2D(_HectonSonarWorldHistoryTex, sampler_LinearClamp, historyUv);
        }

        float EvaluateWorldMemoryPulseBand(float4 pulse, float4 parameters, float2 absoluteXZ, float ageOffset, float intensityScale)
        {
            float active = saturate(_SonarActive) * saturate(parameters.w) * saturate(intensityScale);
            if (active <= 0.0001)
                return 0.0;

            float speed = max(parameters.x, 0.01);
            float maxRadius = max(parameters.y, 0.01);
            float bandWidth = max(max(parameters.z, _WorldPointRadius), 0.05);
            float invBandWidth = rcp(bandWidth);
            float age = _Time.y - pulse.w - ageOffset;
            if (age <= 0.0)
                return 0.0;

            float radius = age * speed;
            float lifeMask = 1.0 - saturate((radius - maxRadius) * invBandWidth);
            if (lifeMask <= 0.0001)
                return 0.0;

            float distanceToPulse = ApproximatePulseDistance2D(absoluteXZ, pulse.xz);
            float band = saturate(1.0 - abs(distanceToPulse - radius) * invBandWidth);
            band = band * band * (3.0 - 2.0 * band);
            float cinematicFalloff = rcp(1.0 + distanceToPulse * 0.004);
            return band * lifeMask * active * cinematicFalloff;
        }

        half4 SampleWorldPointCloud(float3 absoluteWorldPos)
        {
            float2 uv = ResolveWorldMemoryUv(absoluteWorldPos);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            return SAMPLE_TEXTURE2D(_HectonSonarWorldPointCloudRT, sampler_LinearClamp, uv);
        }

        float2 ResolveAbyssalDistortionOffset(float2 screenUV, float3 absoluteWorldPos)
        {
            float distortion = saturate(_AbyssalDistortion);
            if (distortion <= 0.0001)
                return float2(0.0, 0.0);

            float timePhase = _Time.y * (0.85 + distortion * 2.3);
            float phaseA = Hash31(float3(floor(screenUV * _ScaledScreenParams.xy * 0.35), floor(timePhase * 11.0)));
            float phaseB = Hash31(float3(floor(absoluteWorldPos.xz * 0.08 + timePhase * 0.47), floor(timePhase * 7.0)));
            float2 jitterVector = float2(phaseA * 2.0 - 1.0, phaseB * 2.0 - 1.0) + 0.0001;
            float2 jitterDirection = jitterVector * rcp(max(0.0001, ApproximatePulseDistance2D(jitterVector, float2(0.0, 0.0))));
            float distanceToPulse2D = ApproximatePulseDistance2D(absoluteWorldPos.xz, _SonarRevealOriginWS.xz);
            float jitterPixels = distortion * lerp(0.5, 4.0, saturate(distanceToPulse2D * 0.0025));
            return jitterDirection * (jitterPixels * _ScaledScreenParams.zw);
        }

        half4 SampleScreenPointCloudDistorted(float2 screenUV, float3 absoluteWorldPos)
        {
            float2 uvOffset = ResolveAbyssalDistortionOffset(screenUV, absoluteWorldPos);
            float2 distortedUv = saturate(screenUV + uvOffset);
            half4 center = SAMPLE_TEXTURE2D_X(_HectonSonarHistoryTex, sampler_LinearClamp, distortedUv);
            float distortion = saturate(_AbyssalDistortion);
            if (distortion <= 0.0001)
                return center;

            half4 smear = SAMPLE_TEXTURE2D_X(_HectonSonarHistoryTex, sampler_LinearClamp, saturate(distortedUv - uvOffset * 0.65));
            return lerp(center, max(center, smear), distortion * 0.38h);
        }

        half4 SampleWorldPointCloudDistorted(float3 absoluteWorldPos)
        {
            float2 uv = ResolveWorldMemoryUv(absoluteWorldPos);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            float distortion = saturate(_AbyssalDistortion);
            float2 uvOffset = ResolveAbyssalDistortionOffset(uv, absoluteWorldPos) * 1.75;
            half4 center = SAMPLE_TEXTURE2D(_HectonSonarWorldPointCloudRT, sampler_LinearClamp, saturate(uv + uvOffset));
            if (distortion <= 0.0001)
                return center;

            half4 smear = SAMPLE_TEXTURE2D(_HectonSonarWorldPointCloudRT, sampler_LinearClamp, saturate(uv - uvOffset * 0.5));
            return lerp(center, max(center, smear), distortion * 0.44h);
        }

        half4 FragAccumulate(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            half4 previousHistory = SAMPLE_TEXTURE2D_X(_HectonSonarHistoryTex, sampler_LinearClamp, screenUV) * _HasHistory;
            SonarPointData pointData = EvaluateSonarPointCloud(screenUV);
            float persistence = max(0.05, _PersistenceSeconds * lerp(1.0, 1.35, saturate(_LidarPersistence)));
            float fade = rcp(1.0 + 1.442695f * unity_DeltaTime.x * rcp(persistence));
            half4 fadedHistory = previousHistory * (half)fade;
            half3 resolvedColor = max(fadedHistory.rgb, pointData.color);
            half resolvedAlpha = max(fadedHistory.a, pointData.alpha);
            return half4(resolvedColor, resolvedAlpha);
        }

        half4 FragAccumulateWorld(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            half4 previousHistory = SampleWorldHistory(input.screenUV);
            float persistence = max(0.05, _WorldPersistenceSeconds * lerp(1.0, 1.35, saturate(_LidarPersistence)));
            float fade = rcp(1.0 + 1.442695f * unity_DeltaTime.x * rcp(persistence));
            half4 fadedHistory = previousHistory * (half)fade;
            float2 absoluteXZ = ResolveWorldMemoryAbsoluteXZ(input.screenUV);
            float primaryWave = EvaluateWorldMemoryPulseBand(_HectonSonarPrimaryPulse, _HectonSonarVisualParams, absoluteXZ, 0.0, 1.0);
            float4 automaticEchoParams = float4(
                max(_HectonSonarVisualParams.x * 0.72, 0.01),
                _HectonSonarVisualParams.y,
                max(_HectonSonarVisualParams.z * 1.75, 0.05),
                _HectonSonarVisualParams.w * 0.32);
            float automaticEchoWave = EvaluateWorldMemoryPulseBand(
                _HectonSonarPrimaryPulse,
                automaticEchoParams,
                absoluteXZ,
                0.06,
                1.0);
            float eventEchoWave = EvaluateWorldMemoryPulseBand(_HectonSonarEchoPulse, _HectonSonarEchoParams, absoluteXZ, 0.0, 1.0);
            float reflectedWave = saturate(automaticEchoWave * 0.72 + eventEchoWave);
            float2 cell = abs(frac(absoluteXZ * max(0.1, _SonarGridParams0.y)) - 0.5);
            float gridMask = 1.0 - ResolveLinearRamp01(max(0.001, _SonarGridParams0.z), max(0.001, _SonarGridParams0.z) * 2.5, min(cell.x, cell.y));
            float worldPulseMask = saturate((primaryWave + reflectedWave) * (0.28 + gridMask * 0.92) * saturate(_SonarGridParams0.x));
            half3 worldColor =
                _SonarGridHardColor.rgb * (half)(worldPulseMask * (0.72 + primaryWave)) +
                _SonarGridAbyssalColor.rgb * (half)(reflectedWave * 0.28);
            return half4(max(fadedHistory.rgb, worldColor), max(fadedHistory.a, (half)worldPulseMask));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_PASS_STEREO_INSTANCE_ID(input);
            float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(screenUV));
            float rawDepth;
            float depthValid;
            float3 sceneWorldPos = SampleSceneWorldPosition(screenUV, rawDepth, depthValid);
            float3 absoluteWorldPos = ResolveAbsoluteWorldPosition(sceneWorldPos);
            half4 pointCloud = SampleScreenPointCloudDistorted(screenUV, absoluteWorldPos);
            half4 worldPointCloud = depthValid > 0.5 ? SampleWorldPointCloudDistorted(absoluteWorldPos) : half4(0.0h, 0.0h, 0.0h, 0.0h);
            half3 overlay =
                pointCloud.rgb * (pointCloud.a * _OverlayOpacity) +
                worldPointCloud.rgb * (worldPointCloud.a * (_OverlayOpacity * 0.85h));

            float pingActive = saturate(_SonarPingCenter.w) * step(_Time.y, _SonarPingParams.w);
            float pingBandWidth = max(0.25, _SonarPingParams.y);
            float pingDistance = depthValid > 0.5 ? ApproximatePulseDistance(sceneWorldPos, _SonarPingCenter.xyz) : 0.0;
            float pingShell = pingActive * (1.0 - ResolveLinearRamp01(pingBandWidth, pingBandWidth * 2.0, abs(pingDistance - _SonarRadius)));
            float pingContour = ComputeSonarContourMask(screenUV, rawDepth);
            float depthEdgePulse = saturate(pingContour * (0.72 + pingShell * 2.2));
            half3 pingColor = half3(0.0h, 0.92h, 1.0h) * (half)(pingShell * (1.0 + depthEdgePulse * 2.25));
            float primaryVisualWave = depthValid > 0.5
                ? EvaluateScreenSpacePulseBand(_HectonSonarPrimaryPulse, _HectonSonarVisualParams, sceneWorldPos, 0.0, 1.0)
                : 0.0;
            float4 automaticEchoParams = float4(
                max(_HectonSonarVisualParams.x * 0.72, 0.01),
                _HectonSonarVisualParams.y,
                max(_HectonSonarVisualParams.z * 1.75, 0.05),
                _HectonSonarVisualParams.w * 0.32);
            float automaticEchoWave = depthValid > 0.5
                ? EvaluateScreenSpacePulseBand(
                    _HectonSonarPrimaryPulse,
                    automaticEchoParams,
                    sceneWorldPos,
                    0.06 + pingContour * 0.035,
                    pingContour)
                : 0.0;
            float eventEchoWave = depthValid > 0.5
                ? EvaluateScreenSpacePulseBand(_HectonSonarEchoPulse, _HectonSonarEchoParams, sceneWorldPos, 0.0, 1.0)
                : 0.0;
            float reflectedWave = saturate(automaticEchoWave * 0.72 + eventEchoWave);
            float cinematicEdgePulse = saturate((primaryVisualWave + reflectedWave) * (0.16 + depthEdgePulse * 1.95));
            half3 acousticWaveColor = half3(_HectonSonarColor.rgb) *
                (half)((primaryVisualWave * 0.36 + reflectedWave * 0.78 + cinematicEdgePulse) * (0.28 + depthEdgePulse * max(0.0, _HectonSonarColor.w)));

            return half4(sourceColor.rgb + overlay + pingColor + acousticWaveColor, sourceColor.a);
        }
        ENDHLSL

        Pass
        {
            Name "AccumulatePointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAccumulate
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            ENDHLSL
        }

        Pass
        {
            Name "AccumulateWorldPointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAccumulateWorld
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            ENDHLSL
        }

        Pass
        {
            Name "CompositePointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            ENDHLSL
        }
    }

    FallBack Off
}

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
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #define HECTON_SONAR_MAX_CONTACTS 24

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
        float _SonarWaveFront;
        float _LidarPersistence;
        float4 _SonarGridParams0;
        float4 _SonarGridHardColor;
        float4 _SonarGridOrganicColor;
        float4 _SonarGridAbyssalColor;
        float4 _HectonSonarWorldMemoryRect;
        float4 _HectonSonarWorldScrollUvOffset;
        float4 _HectonSonarWorldOriginOffset;
        float4 _SonarRevealContacts[HECTON_SONAR_MAX_CONTACTS];
        float4 _SonarRevealContactMeta[HECTON_SONAR_MAX_CONTACTS];
        float _SonarRevealExpireTime;
        int _SonarRevealContactCount;

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonSonarHistoryTex);
        TEXTURE2D(_HectonSonarWorldHistoryTex);
        TEXTURE2D(_HectonSonarWorldPointCloudRT);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
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
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
#endif
            return output;
        }

        float Hash21(float2 value)
        {
            return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
        }

        float Hash31(float3 value)
        {
            return frac(sin(dot(value, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }

        float DitherNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            return Hash21(pixel);
        }

        float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float validMask)
        {
            rawDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
            validMask = step(0.0001, rawDepth);
#else
            validMask = step(rawDepth, 0.9999);
#endif
            return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
        }

        float ComputeSonarContourMask(float2 screenUV, float rawDepth)
        {
            float2 texel = 1.0 / _ScaledScreenParams.xy;
            float depthDx = SampleSceneDepth(saturate(screenUV + float2(texel.x, 0.0)));
            float depthDy = SampleSceneDepth(saturate(screenUV + float2(0.0, texel.y)));
            float depthGradient = abs(depthDx - rawDepth) + abs(depthDy - rawDepth);
            return saturate(depthGradient * max(1.0, _SonarGridParams0.w) * 180.0);
        }

        float ComputeSonarGridMask(float3 sceneWorldPos)
        {
            float lineScale = max(0.1, _SonarGridParams0.y);
            float lineWidth = max(0.001, _SonarGridParams0.z);
            float2 cell = abs(frac(sceneWorldPos.xz * lineScale) - 0.5);
            return 1.0 - smoothstep(lineWidth, lineWidth * 2.5, min(cell.x, cell.y));
        }

        float ComputeTopographicBands(float3 sceneWorldPos)
        {
            float bandScale = max(0.01, _TopoBandScale);
            float bandWidth = max(0.001, _TopoBandWidth);
            float bandCoord = abs(frac(sceneWorldPos.y * bandScale) - 0.5);
            return 1.0 - smoothstep(bandWidth, bandWidth * 2.6, bandCoord);
        }

        float ResolveWavefrontMask(float distanceToOrigin, float waveRadius, float waveBandWidth)
        {
            return 1.0 - smoothstep(waveBandWidth, waveBandWidth * 2.0, abs(distanceToOrigin - waveRadius));
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
            float sonarFadeDuration = max(0.05, _SonarRevealWaveParams.z);
            float overlayLifetimeMask = step(
                _Time.y,
                _SonarRevealWaveParams.x + (_SonarRevealOriginWS.w / sonarWaveSpeed) + sonarFadeDuration);

            float distanceToOrigin = distance(sceneWorldPos, _SonarRevealOriginWS.xyz);
            float timeSinceArrival = _Time.y - (_SonarRevealWaveParams.x + distanceToOrigin / sonarWaveSpeed);
            float arrivalMask = step(0.0, timeSinceArrival);
            float terrainFade = arrivalMask * saturate(1.0 - (timeSinceArrival / sonarFadeDuration));
            float waveRadius = max(0.0, _SonarWaveFront);
            float waveBandWidth = lerp(6.0, 2.0, saturate(_SonarRevealWaveParams.w));
            float waveFront = ResolveWavefrontMask(distanceToOrigin, waveRadius, waveBandWidth);
            float contourMask = ComputeSonarContourMask(screenUV, rawDepth);
            float planarGridMask = ComputeSonarGridMask(sceneWorldPos);
            float topoBandMask = ComputeTopographicBands(sceneWorldPos);
            float topoGridMask = saturate(max(planarGridMask, topoBandMask * 0.82));
            float terrainGrid =
                topoGridMask *
                max(contourMask, 0.14) *
                max(terrainFade, waveFront * 0.9) *
                step(_Time.y, _SonarRevealExpireTime) *
                overlayLifetimeMask;

            float hardAccum = terrainGrid * 0.55;
            float organicAccum = terrainGrid * 0.18;
            float abyssalAccum = 0.0;

            [unroll(HECTON_SONAR_MAX_CONTACTS)]
            for (int contactIndex = 0; contactIndex < HECTON_SONAR_MAX_CONTACTS; contactIndex++)
            {
                float active = step((float)contactIndex + 0.5, (float)_SonarRevealContactCount);
                float contactArrivalTime = _SonarRevealWaveParams.x + _SonarRevealContacts[contactIndex].w;
                float contactTimeSinceArrival = _Time.y - contactArrivalTime;
                float contactArrivalMask = step(0.0, contactTimeSinceArrival);
                float contactFade = active * contactArrivalMask * saturate(1.0 - (contactTimeSinceArrival / 3.0));
                float contactRadius = max(0.25, _SonarRevealContactMeta[contactIndex].z);
                float contactDistance = distance(sceneWorldPos, _SonarRevealContacts[contactIndex].xyz);
                float contactCore = 1.0 - smoothstep(contactRadius * 0.45, contactRadius, contactDistance);
                float contactRing = ResolveWavefrontMask(contactDistance, 0.0, max(0.25, contactRadius * 0.55));
                float contactPulse = max(contactCore, contactRing * 0.42) * contactFade * _PulseBoost;
                float abyssalMask = step(0.5, _SonarRevealContactMeta[contactIndex].w);
                hardAccum += contactPulse * _SonarRevealContactMeta[contactIndex].x * (1.0 - abyssalMask);
                organicAccum += contactPulse * _SonarRevealContactMeta[contactIndex].y * (1.0 - abyssalMask);
                abyssalAccum += contactPulse * abyssalMask * 1.15;
            }

            float hardStrength = saturate(hardAccum);
            float organicStrength = saturate(organicAccum);
            float abyssalStrength = saturate(abyssalAccum);
            float compositeMask =
                sonarGridIntensity *
                saturate(max(max(hardStrength, organicStrength), abyssalStrength) + waveFront * contourMask * 0.4);
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

        half4 SampleWorldHistory(float2 worldUv)
        {
            if (_HasWorldHistory <= 0.5h)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            float2 historyUv = worldUv + _HectonSonarWorldScrollUvOffset.xy;
            if (historyUv.x < 0.0 || historyUv.x > 1.0 || historyUv.y < 0.0 || historyUv.y > 1.0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            return SAMPLE_TEXTURE2D(_HectonSonarWorldHistoryTex, sampler_LinearClamp, historyUv);
        }

        half4 SampleWorldPointCloud(float3 absoluteWorldPos)
        {
            float2 uv = ResolveWorldMemoryUv(absoluteWorldPos);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            return SAMPLE_TEXTURE2D(_HectonSonarWorldPointCloudRT, sampler_LinearClamp, uv);
        }

        half4 FragAccumulate(Varyings input) : SV_Target
        {
            half4 previousHistory = SAMPLE_TEXTURE2D_X(_HectonSonarHistoryTex, sampler_LinearClamp, input.screenUV) * _HasHistory;
            SonarPointData pointData = EvaluateSonarPointCloud(input.screenUV);
            float persistence = max(0.05, _PersistenceSeconds * lerp(1.0, 1.35, saturate(_LidarPersistence)));
            float fade = exp2(-1.442695f * unity_DeltaTime.x / persistence);
            half4 fadedHistory = previousHistory * (half)fade;
            half3 resolvedColor = max(fadedHistory.rgb, pointData.color);
            half resolvedAlpha = max(fadedHistory.a, pointData.alpha);
            return half4(resolvedColor, resolvedAlpha);
        }

        half4 FragAccumulateWorld(Varyings input) : SV_Target
        {
            half4 previousHistory = SampleWorldHistory(input.screenUV);
            float persistence = max(0.05, _WorldPersistenceSeconds * lerp(1.0, 1.35, saturate(_LidarPersistence)));
            float fade = exp2(-1.442695f * unity_DeltaTime.x / persistence);
            half4 fadedHistory = previousHistory * (half)fade;

            float worldSizeX = rcp(max(_HectonSonarWorldMemoryRect.z, 0.0001));
            float worldSizeY = rcp(max(_HectonSonarWorldMemoryRect.w, 0.0001));
            float3 absoluteWorldPos = float3(
                _HectonSonarWorldMemoryRect.x + input.screenUV.x * worldSizeX,
                _SonarRevealOriginWS.y + _HectonSonarWorldOriginOffset.y,
                _HectonSonarWorldMemoryRect.y + input.screenUV.y * worldSizeY);

            half hardAccum = 0.0h;
            half organicAccum = 0.0h;
            half abyssalAccum = 0.0h;
            float worldPointRadius = max(0.1, _WorldPointRadius);

            [unroll(HECTON_SONAR_MAX_CONTACTS)]
            for (int contactIndex = 0; contactIndex < HECTON_SONAR_MAX_CONTACTS; contactIndex++)
            {
                float active = step((float)contactIndex + 0.5, (float)_SonarRevealContactCount);
                float contactArrivalTime = _SonarRevealWaveParams.x + _SonarRevealContacts[contactIndex].w;
                float contactTimeSinceArrival = _Time.y - contactArrivalTime;
                float contactArrivalMask = step(0.0, contactTimeSinceArrival);
                float contactFade = active * contactArrivalMask * saturate(1.0 - (contactTimeSinceArrival / 5.0));
                float3 absoluteContactPos = _SonarRevealContacts[contactIndex].xyz + _HectonSonarWorldOriginOffset.xyz;
                float contactRadius = max(worldPointRadius, _SonarRevealContactMeta[contactIndex].z * 0.65);
                float contactDistance = distance(absoluteWorldPos.xz, absoluteContactPos.xz);
                float pointMask = 1.0 - smoothstep(contactRadius * 0.35, contactRadius, contactDistance);
                float contactPulse = pointMask * contactFade;
                float abyssalMask = step(0.5, _SonarRevealContactMeta[contactIndex].w);
                hardAccum += (half)(contactPulse * _SonarRevealContactMeta[contactIndex].x * (1.0 - abyssalMask));
                organicAccum += (half)(contactPulse * _SonarRevealContactMeta[contactIndex].y * (1.0 - abyssalMask));
                abyssalAccum += (half)(contactPulse * abyssalMask * 1.15);
            }

            half hardStrength = saturate(hardAccum);
            half organicStrength = saturate(organicAccum);
            half abyssalStrength = saturate(abyssalAccum);
            half compositeMask = saturate(max(max(hardStrength, organicStrength), abyssalStrength));
            half3 newColor =
                (_SonarGridHardColor.rgb * hardStrength) +
                (_SonarGridOrganicColor.rgb * organicStrength) +
                (_SonarGridAbyssalColor.rgb * abyssalStrength);
            half3 resolvedColor = max(fadedHistory.rgb, newColor * compositeMask);
            half resolvedAlpha = max(fadedHistory.a, compositeMask);
            return half4(resolvedColor, resolvedAlpha);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            half4 pointCloud = SAMPLE_TEXTURE2D_X(_HectonSonarHistoryTex, sampler_LinearClamp, input.screenUV);
            float rawDepth;
            float depthValid;
            float3 sceneWorldPos = SampleSceneWorldPosition(input.screenUV, rawDepth, depthValid);
            float3 absoluteWorldPos = ResolveAbsoluteWorldPosition(sceneWorldPos);
            half4 worldPointCloud = depthValid > 0.5 ? SampleWorldPointCloud(absoluteWorldPos) : half4(0.0h, 0.0h, 0.0h, 0.0h);
            half3 overlay =
                pointCloud.rgb * (pointCloud.a * _OverlayOpacity) +
                worldPointCloud.rgb * (worldPointCloud.a * (_OverlayOpacity * 0.85h));
            return half4(sourceColor.rgb + overlay, sourceColor.a);
        }
        ENDHLSL

        Pass
        {
            Name "AccumulatePointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAccumulate
            ENDHLSL
        }

        Pass
        {
            Name "AccumulateWorldPointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAccumulateWorld
            ENDHLSL
        }

        Pass
        {
            Name "CompositePointCloud"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }

    FallBack Off
}

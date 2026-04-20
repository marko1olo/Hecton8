Shader "Hecton8/Vegetation/IndirectStrip"
{
    Properties
    {
        _GrassBaseColor ("Grass Base Color", Color) = (0.18, 0.48, 0.20, 1)
        _GrassTipColor ("Grass Tip Color", Color) = (0.58, 0.82, 0.42, 1)
        _KelpBaseColor ("Kelp Base Color", Color) = (0.08, 0.24, 0.16, 1)
        _KelpTipColor ("Kelp Tip Color", Color) = (0.22, 0.56, 0.36, 1)
        _SargassumBaseColor ("Sargassum Base Color", Color) = (0.32, 0.38, 0.10, 1)
        _SargassumTipColor ("Sargassum Tip Color", Color) = (0.64, 0.56, 0.18, 1)
        _TranslucencyColor ("Translucency Color", Color) = (0.28, 0.72, 0.38, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.08
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.75
        _TranslucencyStrength ("Translucency Strength", Range(0, 2)) = 0.38
        _GrassWindAmplitude ("Grass Wind Amplitude", Range(0, 2)) = 0.26
        _GrassWindFrequency ("Grass Wind Frequency", Range(0, 12)) = 6.4
        _GrassWindSpeed ("Grass Wind Speed", Range(0, 12)) = 2.8
        _KelpCurrentAmplitude ("Kelp Current Amplitude", Range(0, 4)) = 1.25
        _KelpCurrentFrequency ("Kelp Current Frequency", Range(0, 6)) = 0.85
        _KelpCurrentSpeed ("Kelp Current Speed", Range(0, 4)) = 0.42
        _SargassumWaveAmplitude ("Sargassum Wave Amplitude", Range(0, 4)) = 0.55
        _SargassumWaveFrequency ("Sargassum Wave Frequency", Range(0, 8)) = 1.2
        _SargassumWaveSpeed ("Sargassum Wave Speed", Range(0, 6)) = 1.1
        _SargassumPulsationAmplitude ("Sargassum Pulsation Amplitude", Range(0, 1)) = 0.12
        _SargassumPulsationFrequency ("Sargassum Pulsation Frequency", Range(0, 6)) = 1.4
        _SargassumPulsationSpeed ("Sargassum Pulsation Speed", Range(0, 4)) = 0.52
        _SargassumWoundCurlStrength ("Sargassum Wound Curl Strength", Range(0, 1)) = 0.2
        _InteractionPushStrength ("Interaction Push Strength", Range(0, 4)) = 1.35
        _InteractionVelocityBias ("Interaction Velocity Bias", Range(0, 1)) = 0.85
        _InteractionDistancePower ("Interaction Distance Power", Range(1, 4)) = 2.2
        _NormalResponse ("Normal Response", Range(0, 1)) = 0.32
        _AnisotropicSssStrength ("Anisotropic SSS Strength", Range(0, 2)) = 0.72
        _AnisotropicSssPower ("Anisotropic SSS Power", Range(1, 12)) = 4.5
        _BacklightViewBias ("Backlight View Bias", Range(0, 1)) = 0.58
        _EdgeBloomStrength ("Edge Bloom Strength", Range(0, 2)) = 0.62
        _SurfaceWaterLevelFallback ("Surface Water Level Fallback", Float) = 4900
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local _QUALITY_MX350 _QUALITY_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12
            #define HECTON_MAX_IMPACT_SPHERES 8
            #define HECTON_KELP_FADE_START_DEPTH 150.0
            #define HECTON_KELP_FADE_END_DEPTH 200.0
            #define HECTON_ABYSS_SUN_FADE_START_DEPTH 350.0
            #define HECTON_ABYSS_SUN_BLACKOUT_DEPTH 500.0
            #define HECTON_ABYSS_SUN_ABSOLUTE_DEPTH 600.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH 1000.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH 1600.0

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassBaseColor;
                half4 _GrassTipColor;
                half4 _KelpBaseColor;
                half4 _KelpTipColor;
                half4 _SargassumBaseColor;
                half4 _SargassumTipColor;
                half4 _TranslucencyColor;
                half _Opacity;
                half _AlphaClip;
                half _AmbientStrength;
                half _TranslucencyStrength;
                half _GrassWindAmplitude;
                half _GrassWindFrequency;
                half _GrassWindSpeed;
                half _KelpCurrentAmplitude;
                half _KelpCurrentFrequency;
                half _KelpCurrentSpeed;
                half _SargassumWaveAmplitude;
                half _SargassumWaveFrequency;
                half _SargassumWaveSpeed;
                half _SargassumPulsationAmplitude;
                half _SargassumPulsationFrequency;
                half _SargassumPulsationSpeed;
                half _SargassumWoundCurlStrength;
                half _InteractionPushStrength;
                half _InteractionVelocityBias;
                half _InteractionDistancePower;
                half _NormalResponse;
                half _AnisotropicSssStrength;
                half _AnisotropicSssPower;
                half _BacklightViewBias;
                half _EdgeBloomStrength;
                float _SurfaceWaterLevelFallback;
                float _HectonLodPassMode;
                float _HectonLodNearDistance;
                float _HectonLodFarDistance;
                float _HectonLodTransitionRange;
                float _HectonImpostorWidth;
                float _HectonImpostorHeight;
            CBUFFER_END

            struct FloraInteractionPointGpuData
            {
                float4 positionRadius;
                float4 velocitySpeed;
            };

            StructuredBuffer<float4x4> _HectonInstanceMatrices;
            StructuredBuffer<float4> _HectonVegetationInstanceData;
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;
            StructuredBuffer<float4> _HectonImpactSpheres;

            float4 _HectonVegetationFogColor;
            float4 _HectonVegetationAmbientColor;
            float4 _HectonVegetationCurrentVector;
            float4 _SargassumGlobalDriftOffset;
            float4 _SargassumCutMaskWorldRect;
            float4 _HectonVegetationWakeTrailWorldRect;
            float _HectonVegetationDepth;
            float _HectonVegetationLightFactor;
            float _HectonVegetationTurbidity;
            float _HectonVegetationWaterLevel;
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentNoiseScale;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            float _SargassumCutMaskActive;
            float _HectonVegetationWakeTrailActive;
            int _HectonFloraInteractionCount;
            int _HectonImpactSphereCount;

            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);
            TEXTURE2D(_HectonVegetationWakeTrailRT);
            SAMPLER(sampler_HectonVegetationWakeTrailRT);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 originWS : TEXCOORD2;
                half heightMask : TEXCOORD3;
                half lodAlpha : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half instanceType : TEXCOORD6;
                half kelpDepthFade : TEXCOORD7;
                half edgeMask : TEXCOORD8;
            };

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
            }

            float Hash31(float3 value)
            {
                return frac(sin(dot(value, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            float ValueNoise3D(float3 samplePosition)
            {
                float3 cell = floor(samplePosition);
                float3 fracPart = frac(samplePosition);
                float3 smoothPart = fracPart * fracPart * (3.0 - 2.0 * fracPart);

                float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
                float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, smoothPart.x);
                float nx10 = lerp(n010, n110, smoothPart.x);
                float nx01 = lerp(n001, n101, smoothPart.x);
                float nx11 = lerp(n011, n111, smoothPart.x);
                float nxy0 = lerp(nx00, nx10, smoothPart.y);
                float nxy1 = lerp(nx01, nx11, smoothPart.y);
                return lerp(nxy0, nxy1, smoothPart.z) * 2.0 - 1.0;
            }

            float SampleCurrentNoise3D(float3 samplePosition)
            {
                #if defined(_QUALITY_HIGH)
                    float lowFrequency = ValueNoise3D(samplePosition);
                    float highFrequency = ValueNoise3D(samplePosition * 1.83 + float3(19.7, 7.1, 13.4));
                    return lowFrequency * 0.68 + highFrequency * 0.32;
                #else
                    float layer0 = sin(dot(samplePosition, float3(1.11, 0.73, 1.37)));
                    float layer1 = cos(dot(samplePosition.zxy + 17.0, float3(0.83, 1.27, 1.07)));
                    float layer2 = sin(dot(samplePosition.yzx - 9.0, float3(1.41, 0.69, 0.92)));
                    return layer0 * 0.55 + layer1 * 0.30 + layer2 * 0.15;
                #endif
            }

            float ResolveBayer4x4(float2 pixel)
            {
                float2 cell = fmod(pixel, 4.0);
                return (
                    (cell.x < 0.5 && cell.y < 0.5) ? 0.0 :
                    (cell.x < 1.5 && cell.y < 0.5) ? 8.0 :
                    (cell.x < 2.5 && cell.y < 0.5) ? 2.0 :
                    (cell.y < 0.5) ? 10.0 :
                    (cell.x < 0.5 && cell.y < 1.5) ? 12.0 :
                    (cell.x < 1.5 && cell.y < 1.5) ? 4.0 :
                    (cell.x < 2.5 && cell.y < 1.5) ? 14.0 :
                    (cell.y < 1.5) ? 6.0 :
                    (cell.x < 0.5 && cell.y < 2.5) ? 3.0 :
                    (cell.x < 1.5 && cell.y < 2.5) ? 11.0 :
                    (cell.x < 2.5 && cell.y < 2.5) ? 1.0 :
                    (cell.y < 2.5) ? 9.0 :
                    (cell.x < 0.5) ? 15.0 :
                    (cell.x < 1.5) ? 7.0 :
                    (cell.x < 2.5) ? 13.0 :
                    5.0) / 16.0;
            }

            float TemporalDitherNoise(float2 positionCS, float lodAlpha)
            {
                float2 pixel = floor(positionCS);
                float framePhase = fmod(floor(_Time.y * 60.0), 4.0);
                float bayer = ResolveBayer4x4(pixel + float2(framePhase, framePhase * 2.0));
                float hash = frac(52.9829189 * frac(dot(pixel + framePhase, float2(0.06711056, 0.00583715))));
                float temporalBlend = saturate(1.0 - abs(lodAlpha * 2.0 - 1.0));
                return lerp(hash, bayer, temporalBlend);
            }

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return normalize(mul((float3x3)matrixValue, direction));
            }

            float ResolveWaterLevel()
            {
                return _HectonVegetationWaterLevel > 0.01 ? _HectonVegetationWaterLevel : _SurfaceWaterLevelFallback;
            }

            float ResolveWaterDepth(float3 positionWS)
            {
                return max(0.0, ResolveWaterLevel() - positionWS.y);
            }

            float ResolveAbyssalFactor(float3 positionWS)
            {
                return saturate((ResolveWaterDepth(positionWS) - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_BLACKOUT_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
            }

            float ResolveAbyssalSunVisibility(float3 positionWS)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth >= HECTON_ABYSS_SUN_ABSOLUTE_DEPTH)
                    return 0.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_ABSOLUTE_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
                return exp2(-12.0 * normalizedDepth * normalizedDepth);
            }

            float ResolveAbyssalAmbientVisibility(float3 positionWS)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth >= HECTON_ABYSS_SUN_ABSOLUTE_DEPTH)
                    return 0.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_ABSOLUTE_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
                return exp2(-9.0 * normalizedDepth * normalizedDepth);
            }

            float ResolveAbyssalAdditionalLightVisibility(float3 positionWS, float cameraDistance)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth <= HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH)
                    return 1.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH) /
                    (HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH));
                float extinction = lerp(0.0, 0.026, normalizedDepth * normalizedDepth);
                return exp2(-cameraDistance * extinction);
            }

            float EvaluateSchlickPhase(float cosTheta, float anisotropy)
            {
                float k = anisotropy * 0.5;
                float denominator = max(1.0 - k * cosTheta, 0.08);
                return (1.0 - k * k) / (12.56637 * denominator * denominator);
            }

            float2 SafeNormalize2(float2 value)
            {
                float lenSq = dot(value, value);
                return lenSq > 0.0001 ? value * rsqrt(lenSq) : float2(1.0, 0.0);
            }

            float3 SafeNormalize3(float3 value)
            {
                float lenSq = dot(value, value);
                return lenSq > 0.0001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
            }

            void ResolveInstanceShape(float instanceType, float heightScale, float widthScale, out float instanceHeight, out float instanceWidth)
            {
                if (instanceType < 0.5)
                {
                    instanceHeight = lerp(0.35, 1.4, heightScale);
                    instanceWidth = lerp(0.65, 1.25, saturate(widthScale));
                    return;
                }

                if (instanceType < 1.5)
                {
                    instanceHeight = lerp(10.0, 20.0, heightScale);
                    instanceWidth = lerp(0.55, 1.6, saturate(widthScale));
                    return;
                }

                instanceHeight = lerp(0.75, 2.4, heightScale);
                instanceWidth = lerp(0.75, 1.35, saturate(widthScale));
            }

            float ResolveLodAlpha(float distanceToCamera, float passMode)
            {
                float nearDistance = max(_HectonLodNearDistance, 0.01);
                float farDistance = max(_HectonLodFarDistance, nearDistance);
                float transitionRange = max(_HectonLodTransitionRange, 0.01);

                float nearAlpha = 1.0 - smoothstep(
                    max(0.0, nearDistance - transitionRange),
                    nearDistance + transitionRange,
                    distanceToCamera);

                float farAlpha = smoothstep(
                        max(0.0, nearDistance - transitionRange),
                        nearDistance + transitionRange,
                        distanceToCamera) *
                    (1.0 - smoothstep(
                        max(nearDistance, farDistance - transitionRange),
                        farDistance + transitionRange,
                        distanceToCamera));

                return passMode < 0.5 ? nearAlpha : farAlpha;
            }

            float ResolveInteractionDistance()
            {
                return max(12.0, min(_HectonLodNearDistance + _HectonLodTransitionRange, 55.0));
            }

            half EvaluateGlobalSargassumCutMask(float3 positionWS)
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

            half ResolveVegetationCutMask(float instanceType, float3 positionWS)
            {
                if (instanceType > 0.5h && instanceType < 1.5h)
                    return 0.0h;

                return EvaluateGlobalSargassumCutMask(positionWS);
            }

            float3 DecodeWakeTrailDirection(float2 encodedDirection)
            {
                float2 directionXZ = encodedDirection * 2.0 - 1.0;
                return SafeNormalize3(float3(directionXZ.x, 0.0, directionXZ.y));
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

            float3 ResolveWakeTrailOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float heightMask, float instanceType)
            {
                if (bendMask <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float4 wakeTrailData = EvaluateWakeTrailData(evaluationPositionWS);
                float wakeIntensity = saturate(wakeTrailData.b);
                if (wakeIntensity <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 wakeDirection = DecodeWakeTrailDirection(wakeTrailData.rg);
                float3 planarWakeDirection = wakeDirection - baseNormalWS * dot(wakeDirection, baseNormalWS);
                planarWakeDirection = SafeNormalize3(planarWakeDirection);
                float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.05 : 0.38);
                float flattening = wakeIntensity * bendMask * typeScale;
                float downwardBias = lerp(0.04, 0.18, heightMask) * flattening;
                return (planarWakeDirection + baseNormalWS * 0.02) * flattening + float3(0.0, -downwardBias, 0.0);
            }

            float EvaluateSargassumOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.024 + _SargassumGlobalDriftOffset.xz * 0.014;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.87 + 21.0));
                float wave = sin(sample.x * 1.18 + sample.y * 0.86 + _Time.y * 0.12) * 0.5 + 0.5;
                return saturate(coarse * 0.44 + fine * 0.34 + wave * 0.22);
            }

            half ResolveSargassumPorousCoverage(float3 positionWS, float heightMask)
            {
                float organicDensity = EvaluateSargassumOrganicDensity(positionWS.xz + float2(heightMask * 1.1, -heightMask * 0.9));
                float laceNoise = Hash21(floor(positionWS.xz * 1.65 + heightMask * 19.0));
                float interiorBias = lerp(0.58, 0.8, saturate(heightMask));
                return saturate(organicDensity * 1.15 + laceNoise * 0.18 - interiorBias);
            }

            float3 ResolveInteractionOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float distanceToCamera)
            {
                if (bendMask <= 0.0001 || distanceToCamera > ResolveInteractionDistance())
                    return float3(0.0, 0.0, 0.0);

                float3 interactionOffset = float3(0.0, 0.0, 0.0);
                int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);

                [loop]
                for (int i = 0; i < activeInteractionCount; i++)
                {
                    FloraInteractionPointGpuData interactionPoint = _HectonFloraInteractionPoints[i];
                    float3 velocity = interactionPoint.velocitySpeed.xyz;
                    float speed = interactionPoint.velocitySpeed.w;
                    float speedFactor = saturate(speed * 0.18);
                    if (speedFactor <= 0.0001)
                        continue;

                    float3 delta = evaluationPositionWS - interactionPoint.positionRadius.xyz;
                    delta.y *= 0.22;

                    float bendRadius = max(interactionPoint.positionRadius.w, 0.05);
                    float distSq = dot(delta, delta);
                    float dist = sqrt(max(distSq, 0.0001));
                    float proximity = saturate(1.0 - dist / bendRadius);
                    proximity = pow(proximity, max(_InteractionDistancePower, 1.0));
                    if (proximity <= 0.0001)
                        continue;

                    float3 planarVelocityDir = velocity - baseNormalWS * dot(velocity, baseNormalWS);
                    planarVelocityDir = SafeNormalize3(planarVelocityDir);
                    float3 radialDirection = SafeNormalize3(float3(delta.x, 0.0, delta.z));
                    float3 bendDirection = SafeNormalize3(lerp(radialDirection, planarVelocityDir, _InteractionVelocityBias));
                    float directionalBias = 0.65 + 0.35 * saturate(dot(-radialDirection, planarVelocityDir));

                    interactionOffset += (bendDirection + baseNormalWS * 0.04) * (proximity * speedFactor * directionalBias);
                }

                return interactionOffset * bendMask;
            }

            float3 ResolveImpactOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask)
            {
                if (bendMask <= 0.0001 || _HectonImpactSphereCount <= 0)
                    return float3(0.0, 0.0, 0.0);

                float3 impactOffset = float3(0.0, 0.0, 0.0);
                int impactCount = min(_HectonImpactSphereCount, HECTON_MAX_IMPACT_SPHERES);

                [loop]
                for (int i = 0; i < impactCount; i++)
                {
                    float4 impactSphere = _HectonImpactSpheres[i];
                    float radius = max(impactSphere.w, 0.05);
                    float3 delta = evaluationPositionWS - impactSphere.xyz;
                    float dist = length(delta);
                    float proximity = saturate(1.0 - dist / radius);
                    if (proximity <= 0.0001)
                        continue;

                    float3 planarDirection = SafeNormalize3(delta - baseNormalWS * dot(delta, baseNormalWS));
                    impactOffset += (planarDirection + float3(0.0, -0.18, 0.0)) * (proximity * proximity);
                }

                return impactOffset * bendMask;
            }

            float3 CalculateUnderwaterCurrents(
                float3 originWS,
                float3 basePositionWS,
                float bendMask,
                float heightMask,
                float instanceType,
                float instanceNoise,
                float timeValue,
                float2 currentVector,
                float currentStrength,
                out float torsion)
            {
                float currentTimeScale = max(_HectonVegetationCurrentTimeScale, 0.05);
                float currentNoiseScale = max(_HectonVegetationCurrentNoiseScale, 0.002);
                float currentMagnitude = max(currentStrength, 0.1);
                float2 currentDirection = SafeNormalize2(currentVector + float2(0.35, 0.18));
                float2 currentPerpendicular = float2(-currentDirection.y, currentDirection.x);

                float3 samplePosition = float3(
                    basePositionWS.x * currentNoiseScale,
                    basePositionWS.y * (currentNoiseScale * 0.58),
                    basePositionWS.z * currentNoiseScale);
                samplePosition += float3(
                    timeValue * currentTimeScale * 0.11,
                    timeValue * currentTimeScale * 0.037,
                    -timeValue * currentTimeScale * 0.083);

                float gustNoise = SampleCurrentNoise3D(samplePosition + instanceNoise * 4.13);
                float eddyNoise = SampleCurrentNoise3D(samplePosition.zxy * 1.57 + float3(11.0, 19.7, 5.0));
                float phaseA = dot(originWS.xz, float2(0.018, 0.012)) + timeValue * (0.38 + currentTimeScale * 0.42) + instanceNoise * 6.28318;
                float phaseB = dot(originWS.xz, float2(-0.011, 0.016)) - timeValue * (0.27 + currentTimeScale * 0.33) + basePositionWS.y * 0.045;
                float phaseC = dot(originWS.xz, float2(0.007, -0.009)) + timeValue * 0.21 + heightMask * 1.7;

                float surge = sin(phaseA) * 0.55 + cos(phaseB) * 0.35 + sin(phaseC) * 0.25;
                float curl = cos(phaseA * 0.73 - phaseB * 1.12) * 0.45 + eddyNoise * 0.65;
                float2 flowXZ = currentDirection * (0.55 + surge * 0.45 + gustNoise * 0.55) +
                    currentPerpendicular * (curl * 0.42);

                float verticalFlow = (gustNoise * 0.35 + surge * 0.18) * _HectonVegetationCurrentVerticalFactor;
                float3 flowVector = float3(flowXZ.x, verticalFlow, flowXZ.y);
                float typeAmplitude = instanceType < 0.5
                    ? _GrassWindAmplitude * 0.38
                    : (instanceType < 1.5 ? _KelpCurrentAmplitude * 0.72 : _SargassumWaveAmplitude * 0.28);
                float tipWeight = lerp(0.18, 1.0, bendMask);
                float strengthScale = (0.45 + currentMagnitude * 0.55) * typeAmplitude * tipWeight;

                torsion = (curl * 0.6 + surge * 0.4) * strengthScale;
                return flowVector * strengthScale;
            }

            float3 ResolveBillboardPositionWS(float3 originWS, float3 localPosition, float instanceHeight, float instanceWidth, float heightMask)
            {
                float3 cameraDelta = _WorldSpaceCameraPos - originWS;
                float3 cameraForwardXZ = SafeNormalize3(float3(cameraDelta.x, 0.0, cameraDelta.z));
                float3 billboardRight = SafeNormalize3(float3(cameraForwardXZ.z, 0.0, -cameraForwardXZ.x));
                float3 billboardUp = float3(0.0, 1.0, 0.0);
                float widthAtHeight = instanceWidth * lerp(1.0, 0.42, heightMask) * max(_HectonImpostorWidth, 0.25);
                float heightScale = instanceHeight * max(_HectonImpostorHeight, 0.25);

                return originWS +
                    billboardRight * (localPosition.x * widthAtHeight) +
                    billboardUp * (heightMask * heightScale);
            }

            Varyings Vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                uint sourceInstanceIndex = _HectonVisibleInstanceIndices[instanceID];
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                float4 instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];
                float3 originWS = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0));
                float distanceToCamera = distance(originWS, _WorldSpaceCameraPos);
                float lodAlpha = ResolveLodAlpha(distanceToCamera, _HectonLodPassMode);

                float instanceType = clamp(round(instanceData.x), 0.0, 2.0);
                float heightScale = saturate(instanceData.y);
                float widthScale = max(0.2, instanceData.z);
                float variation = frac(instanceData.w);
                float heightMask = saturate(input.uv.y);
                float bendMask = heightMask * heightMask;
                float2 originXZ = originWS.xz;
                float instanceNoise = Hash21(originXZ + variation);
                float resolvedWaterLevel = ResolveWaterLevel();

                float instanceHeight;
                float instanceWidth;
                ResolveInstanceShape(instanceType, heightScale, widthScale, instanceHeight, instanceWidth);

                float3 localPosition = input.positionOS.xyz;
                float3 baseNormalWS = TransformDirection(instanceMatrix, input.normalOS);
                float3 driftOffsetWS = instanceType > 1.5 ? _SargassumGlobalDriftOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 renderOriginWS = originWS + driftOffsetWS;
                float timeValue = _Time.y;
                float2 currentVector = _HectonVegetationCurrentVector.xz;
                float currentStrength = _HectonVegetationCurrentStrength;

                if (instanceType < 0.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.42, heightMask);
                }
                else if (instanceType < 1.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.18, heightMask);
                    localPosition.z += sin(heightMask * PI) * instanceHeight * 0.024;
                }
                else
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.30, heightMask);
                }

                float3 basePositionWS = TransformPoint(instanceMatrix, localPosition) + driftOffsetWS;
                float3 animatedPositionWS = basePositionWS;
                float3 wakeTrailOffset = ResolveWakeTrailOffset(basePositionWS, baseNormalWS, bendMask, heightMask, instanceType);

                if (_HectonLodPassMode < 0.5)
                {
                    float detailAmplitude = saturate(lodAlpha + 0.2);
                    float currentTorsion;
                    float3 currentOffset = CalculateUnderwaterCurrents(
                        renderOriginWS,
                        basePositionWS,
                        bendMask,
                        heightMask,
                        instanceType,
                        instanceNoise,
                        timeValue,
                        currentVector,
                        currentStrength,
                        currentTorsion);

                    if (instanceType < 0.5)
                    {
                        float phase = timeValue * _GrassWindSpeed + instanceNoise * 6.28318 +
                            originWS.x * (_GrassWindFrequency * 0.35) +
                            originWS.z * (_GrassWindFrequency * 0.28);
                        float2 grassWind = SafeNormalize2(float2(
                            sin(phase),
                            cos(phase * 1.37 + heightMask * _GrassWindFrequency)));
                        animatedPositionWS += wakeTrailOffset;
                        animatedPositionWS += currentOffset * (0.85 * detailAmplitude);
                        animatedPositionWS.xz += grassWind * (_GrassWindAmplitude * bendMask * detailAmplitude);
                        animatedPositionWS.y += sin(phase * 1.9 + variation * 5.0) * (0.05 * bendMask * detailAmplitude);
                    }
                    else if (instanceType < 1.5)
                    {
                        float phase = timeValue * (_KelpCurrentSpeed + _HectonVegetationCurrentTimeScale) +
                            (originWS.x + originWS.z) * max(_HectonVegetationCurrentNoiseScale, 0.001) +
                            instanceNoise * 7.0;
                        float2 noiseFlow = float2(
                            sin(phase),
                            cos(phase * 0.71 + heightMask * _KelpCurrentFrequency));
                        float2 kelpFlow = SafeNormalize2(currentVector + noiseFlow * max(currentStrength, 0.15));
                        float kelpAmplitude = _KelpCurrentAmplitude;
                        #if defined(_QUALITY_MX350)
                        kelpAmplitude *= 0.8;
                        #endif
                        animatedPositionWS += wakeTrailOffset * 1.1;
                        animatedPositionWS += currentOffset * (1.15 * detailAmplitude);
                        animatedPositionWS.xz += kelpFlow * (kelpAmplitude * bendMask * detailAmplitude);
                        animatedPositionWS.xz += float2(currentTorsion, -currentTorsion) * (bendMask * 0.42 * detailAmplitude);
                        animatedPositionWS.y += sin(phase * 0.55 + heightMask * 2.2) *
                            (_KelpCurrentAmplitude * 0.12 * _HectonVegetationCurrentVerticalFactor * bendMask * detailAmplitude);
                    }
                    else
                    {
                        float phase = timeValue * _SargassumWaveSpeed + instanceNoise * 8.0 +
                            dot(originXZ, float2(_SargassumWaveFrequency * 0.2, _SargassumWaveFrequency * 0.16));
                        float organicDensity = EvaluateSargassumOrganicDensity(renderOriginWS.xz);
                        float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                        float waveLift = sin(phase) * _SargassumWaveAmplitude;
                        float bob = cos(phase * 1.31 + variation * 6.0) * (_SargassumWaveAmplitude * 0.18);
                        float verticalFromRoot = basePositionWS.y - renderOriginWS.y;
                        renderOriginWS.y = resolvedWaterLevel + driftOffsetWS.y + waveLift;
                        animatedPositionWS.y = renderOriginWS.y + verticalFromRoot + bob * bendMask;
                        float2 surfaceDrift = SafeNormalize2(float2(sin(phase * 0.73), cos(phase * 0.91))) + currentVector * 0.25;
                        animatedPositionWS.xz += SafeNormalize2(surfaceDrift) * (_SargassumWaveAmplitude * 0.22 * bendMask * detailAmplitude);
                        float pulsePhase = timeValue * _SargassumPulsationSpeed + instanceNoise * 9.7 + organicDensity * (_SargassumPulsationFrequency * 6.28318);
                        float pulse = sin(pulsePhase) * _SargassumPulsationAmplitude * edgePulse * bendMask * detailAmplitude;
                        float2 radialWS = SafeNormalize2(animatedPositionWS.xz - renderOriginWS.xz + float2(0.001, 0.001));
                        animatedPositionWS += wakeTrailOffset * 0.45;
                        animatedPositionWS.xz += currentOffset.xz * (0.5 * detailAmplitude);
                        animatedPositionWS.y += currentOffset.y * (0.18 * detailAmplitude);
                        animatedPositionWS.xz += radialWS * pulse;
                        animatedPositionWS.y += pulse * 0.18;
                    }
                }
                else
                {
                    float farPhase = timeValue * (0.6 + _HectonVegetationCurrentTimeScale * 0.5) + instanceNoise * 9.0;
                    float farCurrentTorsion;
                    float3 farCurrentOffset = CalculateUnderwaterCurrents(
                        renderOriginWS,
                        basePositionWS,
                        bendMask * saturate(lodAlpha + 0.1),
                        heightMask,
                        instanceType,
                        instanceNoise,
                        timeValue,
                        currentVector,
                        currentStrength,
                        farCurrentTorsion);

                    if (instanceType > 1.5)
                    {
                        renderOriginWS.y = resolvedWaterLevel + driftOffsetWS.y + sin(farPhase) * (_SargassumWaveAmplitude * 0.9);
                    }

                    animatedPositionWS = ResolveBillboardPositionWS(renderOriginWS, localPosition, instanceHeight, instanceWidth, heightMask);

                    float2 farFlow = SafeNormalize2(currentVector + float2(sin(farPhase), cos(farPhase * 0.83)) * max(currentStrength, 0.2));
                    float farSwayStrength = instanceType < 0.5 ? _GrassWindAmplitude * 0.55 : _KelpCurrentAmplitude * 0.42;
                    animatedPositionWS += wakeTrailOffset * 0.8;
                    animatedPositionWS.xz += farFlow * (farSwayStrength * bendMask * lodAlpha);
                    animatedPositionWS += farCurrentOffset * 0.65;
                }

                float3 interactionOffset = ResolveInteractionOffset(animatedPositionWS, baseNormalWS, bendMask, distanceToCamera);
                float3 impactOffset = ResolveImpactOffset(animatedPositionWS, baseNormalWS, bendMask);
                float interactionTypeScale = instanceType < 0.5 ? 0.7 : (instanceType < 1.5 ? 1.15 : 0.85);
                animatedPositionWS += impactOffset * 0.95;
                animatedPositionWS += interactionOffset * (_InteractionPushStrength * interactionTypeScale);

                if (instanceType > 1.5)
                {
                    half cutMask = ResolveVegetationCutMask(instanceType, animatedPositionWS);
                    float woundCurl = smoothstep(0.05, 0.9, cutMask) * bendMask * _SargassumWoundCurlStrength;
                    if (woundCurl > 0.0001)
                    {
                        float2 woundDir = SafeNormalize2(animatedPositionWS.xz - renderOriginWS.xz + float2(0.001, 0.001));
                        animatedPositionWS.xz -= woundDir * woundCurl;
                        animatedPositionWS.y -= woundCurl * 0.22;
                    }
                }

                float3 swayOffset = animatedPositionWS - basePositionWS;
                float3 normalWS = normalize(baseNormalWS - swayOffset * (_NormalResponse * bendMask));

                if (_HectonLodPassMode >= 0.5)
                {
                    float3 cameraDelta = _WorldSpaceCameraPos - animatedPositionWS;
                    float3 viewFacingNormal = SafeNormalize3(float3(cameraDelta.x, 0.3, cameraDelta.z));
                    normalWS = SafeNormalize3(lerp(normalWS, viewFacingNormal, 0.78));
                }

                float kelpDepthBelowWater = max(0.0, resolvedWaterLevel - renderOriginWS.y);
                float kelpDepthFade = saturate((kelpDepthBelowWater - HECTON_KELP_FADE_START_DEPTH) / (HECTON_KELP_FADE_END_DEPTH - HECTON_KELP_FADE_START_DEPTH));

                output.positionWS = animatedPositionWS;
                output.originWS = renderOriginWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.heightMask = heightMask;
                output.lodAlpha = lodAlpha;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.instanceType = instanceType;
                output.kelpDepthFade = kelpDepthFade;
                output.edgeMask = saturate(abs(input.uv.x * 2.0 - 1.0));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                half3 lightDirectionWS = normalize(mainLight.direction);
                half abyssFactor = ResolveAbyssalFactor(input.positionWS);
                half sunVisibility = ResolveAbyssalSunVisibility(input.positionWS);
                half ambientVisibility = ResolveAbyssalAmbientVisibility(input.positionWS);
                half NdotL = saturate(dot(normalWS, lightDirectionWS));
                half wrapDiffuse = saturate(NdotL * 0.5h + 0.5h);
                half backLight = saturate(dot(-normalWS, lightDirectionWS));
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 3.0h);

                half3 baseColor;
                half3 tipColor;
                if (input.instanceType < 0.5h)
                {
                    baseColor = _GrassBaseColor.rgb;
                    tipColor = _GrassTipColor.rgb;
                }
                else if (input.instanceType < 1.5h)
                {
                    baseColor = _KelpBaseColor.rgb;
                    tipColor = _KelpTipColor.rgb;
                }
                else
                {
                    baseColor = _SargassumBaseColor.rgb;
                    tipColor = _SargassumTipColor.rgb;
                }

                half3 gradientColor = lerp(baseColor, tipColor, input.heightMask);

                if (input.instanceType > 0.5h && input.instanceType < 1.5h)
                {
                    half lightFactor = saturate(_HectonVegetationLightFactor);
                    half turbidity = saturate(_HectonVegetationTurbidity * 0.5h);
                    half fogBlend = saturate(0.35h + input.kelpDepthFade * 0.65h);
                    gradientColor = lerp(gradientColor, _HectonVegetationFogColor.rgb, fogBlend);
                    half kelpLuma = dot(gradientColor, half3(0.299h, 0.587h, 0.114h));
                    gradientColor = lerp(half3(kelpLuma, kelpLuma, kelpLuma), gradientColor, 1.0h - input.kelpDepthFade);
                    gradientColor *= lerp(1.0h, 0.72h, turbidity);
                    gradientColor *= lerp(0.38h, 1.0h, lightFactor);
                }

                half3 ambient = lerp(_HectonVegetationAmbientColor.rgb, SampleSH(normalWS), 0.55h) * (_AmbientStrength * ambientVisibility);
                half3 diffuse = gradientColor * ambient;
                diffuse += gradientColor * (mainLight.color * wrapDiffuse * sunVisibility);
                half3 transmission = _TranslucencyColor.rgb * backLight * input.heightMask * _TranslucencyStrength * sunVisibility;
                half3 bladeAxisWS = SafeNormalize3(input.positionWS - input.originWS + float3(0.0, 0.02, 0.0));
                half3 lightViewBisector = SafeNormalize3(lightDirectionWS + viewDirectionWS);
                half bladeAlignment = saturate(1.0h - abs(dot(bladeAxisWS, lightViewBisector)));
                half backlightPhase = pow(saturate(dot(lightDirectionWS, -viewDirectionWS)), lerp(2.0h, 7.0h, _BacklightViewBias));
                half anisotropicPhase = pow(bladeAlignment, _AnisotropicSssPower);
                half anisotropicMask = input.instanceType < 0.5h ? 0.42h : (input.instanceType < 1.5h ? 1.0h : 0.78h);
                half thicknessMask = saturate(0.28h + input.heightMask * 0.72h);
                half kelpMask = input.instanceType > 0.5h && input.instanceType < 1.5h ? 1.0h : 0.0h;
                half sargassumMask = input.instanceType > 1.5h ? 1.0h : 0.0h;
                half edgeBacklightMask = saturate(input.edgeMask * input.edgeMask);
                half edgeBiasedThickness = lerp(thicknessMask * 0.48h, thicknessMask * 1.38h, edgeBacklightMask);
                half3 kelpGoldTint = lerp(tipColor, half3(1.0h, 0.78h, 0.34h), 0.58h);
                half3 sargassumGoldTint = lerp(tipColor, half3(1.0h, 0.82h, 0.28h), 0.74h);
                half3 anisotropicTint = lerp(_TranslucencyColor.rgb, kelpGoldTint, kelpMask * 0.82h);
                anisotropicTint = lerp(anisotropicTint, sargassumGoldTint, sargassumMask * (0.62h + edgeBacklightMask * 0.38h));
                half edgeFocusedBacklight = lerp(1.0h, edgeBiasedThickness * 1.12h, sargassumMask);
                half3 anisotropicSss = anisotropicTint * (anisotropicPhase * backlightPhase * edgeFocusedBacklight * anisotropicMask * _AnisotropicSssStrength);
                half edgeBloomMask = saturate(backlightPhase * edgeBacklightMask * rim);
                half3 edgeBloomTint = lerp(kelpGoldTint, sargassumGoldTint, sargassumMask);
                half3 edgeBloom = edgeBloomTint * (edgeBloomMask * _EdgeBloomStrength * (0.35h + 0.65h * max(kelpMask, sargassumMask)));
                half3 finalColor = diffuse + transmission + tipColor * rim * 0.08h;
                finalColor += anisotropicSss * mainLight.color * sunVisibility;
                finalColor += edgeBloom * mainLight.color * (1.45h * sunVisibility);
                half porousCoverageMask = input.instanceType > 1.5h ? ResolveSargassumPorousCoverage(input.positionWS, input.heightMask) : 1.0h;

                #ifdef _ADDITIONAL_LIGHTS
                uint addLightCount = GetAdditionalLightsCount();
                half3 additionalDiffuse = half3(0.0h, 0.0h, 0.0h);
                half3 localVolumetric = half3(0.0h, 0.0h, 0.0h);
                half abyssLightVisibility = ResolveAbyssalAdditionalLightVisibility(input.positionWS, distance(input.positionWS, _WorldSpaceCameraPos));
                for (uint lightIndex = 0u; lightIndex < addLightCount; lightIndex++)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half attenuation = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    if (attenuation <= 0.0001h)
                        continue;

                    half localNdotL = saturate(dot(normalWS, addLight.direction));
                    half localBacklight = saturate(dot(-normalWS, addLight.direction));
                    half3 localContribution = gradientColor * (addLight.color * localNdotL * attenuation);
                    half3 localTranslucency = edgeBloomTint * (localBacklight * input.heightMask * attenuation * (_TranslucencyStrength * 0.8h));
                    half phaseCos = saturate(dot(viewDirectionWS, -addLight.direction));
                    half forwardScatter = EvaluateSchlickPhase(phaseCos, 0.68h);
                    half volumetricMask = saturate((0.4h + _HectonVegetationTurbidity) * (0.55h + abyssFactor * 0.9h));
                    half selfShadowGuard = saturate(lerp(0.28h, 1.0h, max(edgeBacklightMask, input.heightMask)) * lerp(1.0h, porousCoverageMask, sargassumMask));
                    half shaftRange = saturate(0.22h + localBacklight * 0.78h);
                    half3 localShaft = addLight.color * (forwardScatter * attenuation * volumetricMask * selfShadowGuard * shaftRange * (0.08h + 0.18h * max(kelpMask, sargassumMask)));

                    additionalDiffuse += (localContribution + localTranslucency) * abyssLightVisibility;
                    localVolumetric += localShaft * abyssLightVisibility;
                }
                finalColor += additionalDiffuse;
                finalColor += localVolumetric;
                #endif
                half cutMask = ResolveVegetationCutMask(input.instanceType, input.positionWS);

                half kelpOpacityScale = input.instanceType > 0.5h && input.instanceType < 1.5h
                    ? lerp(1.0h, 0.25h, input.kelpDepthFade)
                    : 1.0h;
                half alpha = saturate(_Opacity * input.lodAlpha * kelpOpacityScale);
                clip(0.08h - cutMask);

                if (input.instanceType > 1.5h)
                {
                    clip(porousCoverageMask - 0.16h);
                }

                float dither = TemporalDitherNoise(input.positionCS.xy / max(input.positionCS.w, 0.0001), input.lodAlpha);
                clip(alpha - max(_AlphaClip, dither));

                half3 abyssFogColor = lerp(_HectonVegetationFogColor.rgb, half3(0.01h, 0.03h, 0.045h), abyssFactor);
                half fogBlend = saturate(input.fogFactor * lerp(1.0h, 1.25h, abyssFactor));
                finalColor = lerp(finalColor, abyssFogColor, fogBlend);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}

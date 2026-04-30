Shader "Hidden/Hecton8/VegetationIndirectMotionVectors"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HECTON_GPU_INDIRECT

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnityIndirect.cginc"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float _HectonLodPassMode;
                float _HectonImpostorWidth;
                float _HectonImpostorHeight;
            CBUFFER_END

            struct FloraInteractionPointGpuData
            {
                float4 positionRadius;
                float4 velocitySpeed;
            };

            struct HectonVegetationInstanceGpuData
            {
                float Type;
                float HeightScale;
                float WidthScale;
                float Variation;
                float TemplateIndex;
                float RuntimeState;
                float RuntimeFlags;
                float PulseFrequency;
                float4 BioluminescenceColor;
                float SwaySpeed;
                float BendAmplitude;
                float HealthNormalized;
                float Reserved0;
            };

            StructuredBuffer<float4x4> _HectonInstanceMatrices;
            StructuredBuffer<HectonVegetationInstanceGpuData> _HectonVegetationInstanceData;
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            StructuredBuffer<float2> _MarineSnowFlowField;
            float4 _ChunkWorldOffset;
            float4 _GlobalFloatingOffset;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;

            float4 _MarineSnowFlowFieldCenterCellSize;
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _SargassumGlobalDriftOffset;
            float4 _HectonShallowWaterFieldWorldRect;
            float4 _SargassumCutMaskWorldRect;
            float4 _HectonPlayerRuntimePosition;
            float4 _HectonPlayerFloraInteractionParams;
            float4 _HectonFloraLifecycleParams;
            float4 _HectonFlowSynchronyParams;
            float3 _HectonPreviousCameraPosition;
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            float _HectonShallowWaterFieldActive;
            float _SargassumCutMaskActive;
            int _HectonFloraFlowFieldResolution;
            int _HectonFloraInteractionCount;

            TEXTURE2D(_HectonShallowWaterFieldRT);
            SAMPLER(sampler_HectonShallowWaterFieldRT);
            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
                float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
                float3 positionWS : TEXCOORD0;
                float2 vegetationData : TEXCOORD1;
            };

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return normalize(mul((float3x3)matrixValue, direction));
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

            float2 ResolvePlanarCurrentDirection()
            {
                float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001
                    ? _GlobalOceanFlow.xz
                    : _HectonVegetationCurrentVector.xz;
                return SafeNormalize2(flow);
            }

            float ResolvePlanarCurrentStrength()
            {
                return max(length(_GlobalOceanFlow.xyz), _HectonVegetationCurrentStrength);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
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

            float2 SampleMarineSnowFlowFieldXZ(float3 positionWS)
            {
                int resolution = _HectonFloraFlowFieldResolution;
                float cellSize = max(_MarineSnowFlowFieldCenterCellSize.w, 0.001);
                if (resolution <= 1 || cellSize <= 0.0)
                    return float2(0.0, 0.0);

                float halfExtent = (resolution - 1) * cellSize * 0.5;
                float2 centerXZ = _MarineSnowFlowFieldCenterCellSize.xz;
                float2 gridPosition = ((positionWS.xz - centerXZ) + halfExtent.xx) / cellSize;
                gridPosition = clamp(gridPosition, float2(0.0, 0.0), float2(resolution - 1, resolution - 1));

                int2 baseCell = (int2)floor(gridPosition);
                int2 nextCell = min(baseCell + 1, int2(resolution - 1, resolution - 1));
                float2 fracValue = frac(gridPosition);

                int index00 = baseCell.x + baseCell.y * resolution;
                int index10 = nextCell.x + baseCell.y * resolution;
                int index01 = baseCell.x + nextCell.y * resolution;
                int index11 = nextCell.x + nextCell.y * resolution;

                float2 sample00 = _MarineSnowFlowField[index00];
                float2 sample10 = _MarineSnowFlowField[index10];
                float2 sample01 = _MarineSnowFlowField[index01];
                float2 sample11 = _MarineSnowFlowField[index11];

                float2 sample0 = lerp(sample00, sample10, fracValue.x);
                float2 sample1 = lerp(sample01, sample11, fracValue.x);
                return lerp(sample0, sample1, fracValue.y);
            }

            float3 ResolveMarineSnowFlowField(float3 positionWS)
            {
                float2 flowXZ = SampleMarineSnowFlowFieldXZ(positionWS);
                return float3(flowXZ.x, 0.0, flowXZ.y);
            }

            float ResolveFlowSynchronyPhase(float3 positionWS, float instanceNoise)
            {
                return _HectonFlowSynchronyParams.z + dot(positionWS.xz, float2(0.031, -0.027)) + instanceNoise * 6.28318;
            }

            float3 ResolveFlowSynchronyOffset(float3 positionWS, float bendMask, float instanceType, float instanceNoise)
            {
                if (bendMask <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 flowSample = ResolveMarineSnowFlowField(positionWS);
                float flowMagnitude = length(flowSample.xz) * max(_HectonFlowSynchronyParams.x, 1.0);
                if (flowMagnitude <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 flowDirection = SafeNormalize3(float3(flowSample.x, 0.0, flowSample.z));
                float typeScale = instanceType < 0.5 ? 0.24 : (instanceType < 1.5 ? 0.42 : 0.18);
                float flowWave = sin(ResolveFlowSynchronyPhase(positionWS, instanceNoise));
                return flowDirection * (flowWave * flowMagnitude * typeScale * bendMask);
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

            float2 DecodeShallowWaterVelocity(float2 encodedVelocity)
            {
                return encodedVelocity * 2.0 - 1.0;
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
                if (instanceType > 0.5 && instanceType < 1.5)
                    return 0.0h;

                return EvaluateGlobalSargassumCutMask(positionWS);
            }

            float EvaluateSargassumOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.024 + _SargassumGlobalDriftOffset.xz * 0.014;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.87 + 21.0));
                float wave = sin(sample.x * 1.18 + sample.y * 0.86 + _Time.y * 0.12) * 0.5 + 0.5;
                return saturate(coarse * 0.44 + fine * 0.34 + wave * 0.22);
            }

            half ResolveSargassumMotionCoverage(float3 positionWS, float heightMask)
            {
                float organicDensity = EvaluateSargassumOrganicDensity(positionWS.xz + float2(heightMask * 1.1, -heightMask * 0.9));
                float laceNoise = Hash21(floor(positionWS.xz * 1.65 + heightMask * 19.0));
                float interiorBias = lerp(0.58, 0.8, saturate(heightMask));
                return saturate(organicDensity * 1.15 + laceNoise * 0.18 - interiorBias);
            }

            float3 ResolveWakeTrailOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float instanceType)
            {
                float4 shallowWaterData = EvaluateShallowWaterFieldData(evaluationPositionWS);
                float displacement = saturate(shallowWaterData.b);
                float2 planarVelocity = DecodeShallowWaterVelocity(shallowWaterData.rg);
                float velocityMagnitude = saturate(length(planarVelocity));
                if (displacement <= 0.0001 && velocityMagnitude <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 wakeDirection = SafeNormalize3(float3(planarVelocity.x, 0.0, planarVelocity.y));
                float3 planarWakeDirection = SafeNormalize3(wakeDirection - baseNormalWS * dot(wakeDirection, baseNormalWS));
                float typeScale = instanceType < 0.5 ? 0.7 : (instanceType < 1.5 ? 1.0 : 0.3);
                return planarWakeDirection * ((displacement + velocityMagnitude * 0.5) * bendMask * typeScale);
            }

            float3 ResolveInteractionOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float historyDelta)
            {
                float3 interactionOffset = float3(0.0, 0.0, 0.0);
                int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);

                [loop]
                for (int i = 0; i < activeInteractionCount; i++)
                {
                    FloraInteractionPointGpuData interactionPoint = _HectonFloraInteractionPoints[i];
                    float3 velocity = interactionPoint.velocitySpeed.xyz;
                    float speedFactor = saturate(interactionPoint.velocitySpeed.w * 0.18);
                    float3 rewoundInteractionPosition = interactionPoint.positionRadius.xyz - velocity * max(historyDelta, 0.0);
                    float3 delta = evaluationPositionWS - rewoundInteractionPosition;
                    delta.y *= 0.22;
                    float proximity = saturate(1.0 - length(delta) / max(interactionPoint.positionRadius.w, 0.05));
                    if (proximity <= 0.0001 || speedFactor <= 0.0001)
                        continue;

                    float3 planarVelocityDir = SafeNormalize3(velocity - baseNormalWS * dot(velocity, baseNormalWS));
                    interactionOffset += planarVelocityDir * (proximity * speedFactor);
                }

                return interactionOffset * bendMask;
            }

            float3 ResolvePlayerBendOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float instanceType)
            {
                float playerRadius = _HectonPlayerRuntimePosition.w;
                if (bendMask <= 0.0001 ||
                    _HectonPlayerFloraInteractionParams.w < 0.5 ||
                    playerRadius <= 0.0001)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float playerRuntimePosition = _HectonPlayerRuntimePosition.xyz;
                float playerSpeed = _HectonPlayerFloraInteractionParams.x;
                float playerPush = _HectonPlayerFloraInteractionParams.y;
                if (playerSpeed <= 0.0001 || playerPush <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 delta = evaluationPositionWS - playerRuntimePosition;
                delta.y *= 0.22;
                float radiusSq = playerRadius * playerRadius;
                float distSq = dot(delta, delta);
                if (distSq >= radiusSq)
                    return float3(0.0, 0.0, 0.0);

                float dist = sqrt(max(distSq, 0.0001));
                float proximity = saturate(1.0 - dist / playerRadius);
                proximity *= proximity;
                float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.08 : 0.52);
                return (SafeNormalize3(float3(delta.x, 0.0, delta.z)) + baseNormalWS * 0.04) *
                    (proximity * saturate(playerSpeed * 0.16) * playerPush * typeScale * bendMask);
            }

            float3 ResolveBillboardPositionWS(float3 originWS, float3 localPosition, float instanceHeight, float instanceWidth, float heightMask, float3 cameraPositionWS)
            {
                float3 cameraDelta = cameraPositionWS - originWS;
                float3 cameraForwardXZ = SafeNormalize3(float3(cameraDelta.x, 0.0, cameraDelta.z));
                float3 billboardRight = SafeNormalize3(float3(cameraForwardXZ.z, 0.0, -cameraForwardXZ.x));
                float3 billboardUp = float3(0.0, 1.0, 0.0);
                float widthAtHeight = instanceWidth * lerp(1.0, 0.42, heightMask) * max(_HectonImpostorWidth, 0.25);
                float heightScale = instanceHeight * max(_HectonImpostorHeight, 0.25);
                return originWS + billboardRight * (localPosition.x * widthAtHeight) + billboardUp * (heightMask * heightScale);
            }

            float ResolveOrganicEntropyProgress(float encodedHeightScale, float encodedWidthScale, float timeValue)
            {
                if (encodedHeightScale >= 0.0)
                    return 0.0;

                return saturate((timeValue - max(0.0, encodedWidthScale)) / 0.85);
            }

            float2 ResolveStateBlendWeights(float runtimeState)
            {
                float agitated = saturate(1.0 - abs(runtimeState - 1.0));
                float dying = saturate(1.0 - abs(runtimeState - 2.0));
                return float2(agitated, dying);
            }

            float ResolveGrowth01(float encodedGrowth01)
            {
                return encodedGrowth01 > 0.0001 ? saturate(encodedGrowth01) : 1.0;
            }

            float3 AnimatePositionWS(float3 localPosition, float3 normalOS, float2 uv, float4x4 instanceMatrix, HectonVegetationInstanceGpuData instanceData, float timeValue, float3 cameraPositionWS)
            {
                float3 originWS = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0)) + _GlobalFloatingOffset.xyz;
                float instanceType = clamp(round(instanceData.Type), 0.0, 2.0);
                float encodedHeightScale = instanceData.HeightScale;
                float encodedWidthScale = instanceData.WidthScale;
                float entropyProgress = ResolveOrganicEntropyProgress(encodedHeightScale, encodedWidthScale, timeValue);
                float heightScale = saturate(abs(encodedHeightScale));
                float widthScale = entropyProgress > 0.0001 ? 1.0 : max(0.2, encodedWidthScale);
                float variation = frac(instanceData.Variation);
                float heightMask = saturate(uv.y);
                float bendMask = heightMask * heightMask * max(instanceData.BendAmplitude, 0.0);
                float curvatureMask = heightMask;
                float instanceNoise = Hash21(originWS.xz + variation);
                float authoredSwaySpeed = max(instanceData.SwaySpeed, 0.05);
                float healthSwayScale = lerp(0.35, 1.0, saturate(instanceData.HealthNormalized));
                float instanceHeight;
                float instanceWidth;
                ResolveInstanceShape(instanceType, heightScale, widthScale, instanceHeight, instanceWidth);
                instanceHeight *= ResolveGrowth01(instanceData.Reserved0);

                if (instanceType < 0.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.42, heightMask);
                }
                else if (instanceType < 1.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.18, heightMask);
                }
                else
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.30, heightMask);
                }

                float3 baseNormalWS = TransformDirection(instanceMatrix, normalOS);
                float3 driftOffsetWS = instanceType > 1.5 ? _SargassumGlobalDriftOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 basePositionWS = TransformPoint(instanceMatrix, localPosition) + driftOffsetWS + _GlobalFloatingOffset.xyz;
                float3 animatedPositionWS = basePositionWS;
                float historyDelta = max(0.0, _Time.y - timeValue);
                float3 sampledFlowVector = ResolveMarineSnowFlowField(basePositionWS);
                float2 sampledCurrentVector = sampledFlowVector.xz;
                float2 currentVector = dot(sampledCurrentVector, sampledCurrentVector) > 0.0001
                    ? SafeNormalize2(sampledCurrentVector)
                    : ResolvePlanarCurrentDirection();
                float currentStrength = max(length(sampledCurrentVector), ResolvePlanarCurrentStrength());
                float scaledTimeValue = timeValue * authoredSwaySpeed;
                float swayWave = sin(scaledTimeValue * (0.55 + _HectonVegetationCurrentTimeScale * 0.35) + instanceNoise * 6.28318 + originWS.x * 0.015 + originWS.z * 0.01);
                float3 flowSynchronyOffset = ResolveFlowSynchronyOffset(basePositionWS, bendMask, instanceType, instanceNoise);
                animatedPositionWS.xz += currentVector * (currentStrength * 0.28 * bendMask * swayWave * healthSwayScale);
                animatedPositionWS.y += swayWave * (_HectonVegetationCurrentVerticalFactor * 0.12 * bendMask * healthSwayScale);
                animatedPositionWS += flowSynchronyOffset;
                animatedPositionWS += ResolveWakeTrailOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType);
                animatedPositionWS += ResolveInteractionOffset(animatedPositionWS, baseNormalWS, bendMask, historyDelta);
                animatedPositionWS += ResolvePlayerBendOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType) * 1.1;
                float2 stateWeights = ResolveStateBlendWeights(instanceData.RuntimeState);
                if (stateWeights.x > 0.0001 || stateWeights.y > 0.0001)
                {
                    float statePhase = sin(scaledTimeValue * (1.35 + max(instanceData.PulseFrequency, 0.05)) + instanceNoise * 9.0 + heightMask * 3.2);
                    animatedPositionWS.xz += ResolvePlanarCurrentDirection() * (statePhase * bendMask * 0.16 * stateWeights.x);
                    animatedPositionWS.y -= instanceHeight * bendMask * (0.06 * stateWeights.x + 0.18 * stateWeights.y);
                }

                if (_HectonLodPassMode >= 0.5)
                {
                    animatedPositionWS = ResolveBillboardPositionWS(originWS + driftOffsetWS, localPosition, instanceHeight, instanceWidth, heightMask, cameraPositionWS);
                    animatedPositionWS += flowSynchronyOffset * 0.85;
                }

                float seasonalDecayWeight = saturate(_HectonFloraLifecycleParams.y) * saturate(_HectonFloraLifecycleParams.w);
                if (seasonalDecayWeight > 0.0001)
                {
                    float seasonalWiltWeight = seasonalDecayWeight *
                        saturate(lerp(0.18, 1.0, heightMask) * lerp(0.35, 1.0, bendMask));
                    float3 renderOriginWS = originWS + driftOffsetWS;
                    animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, seasonalWiltWeight * 0.24);
                    animatedPositionWS.y -= instanceHeight * seasonalWiltWeight * lerp(0.04, 0.19, heightMask);
                    animatedPositionWS.xz += currentVector * (-seasonalWiltWeight * instanceHeight * 0.018 * heightMask);
                }

                if (entropyProgress > 0.0001)
                {
                    float entropyWeight = saturate(entropyProgress * lerp(0.22, 1.0, heightMask) * lerp(0.35, 1.0, curvatureMask));
                    animatedPositionWS = lerp(animatedPositionWS, originWS + driftOffsetWS, entropyWeight * 0.72);
                    animatedPositionWS.y -= entropyWeight * instanceHeight * lerp(0.08, 0.42, heightMask);
                }

                return animatedPositionWS;
            }

            Varyings Vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                uint sourceInstanceIndex = instanceID;
                #if defined(HECTON_GPU_INDIRECT)
                    InitIndirectDrawArgs(0);
                    sourceInstanceIndex = _HectonVisibleInstanceIndices[GetIndirectInstanceID(instanceID)];
                #endif
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                HectonVegetationInstanceGpuData instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];

                float3 currentPositionWS = AnimatePositionWS(input.positionOS.xyz, input.normalOS, input.uv, instanceMatrix, instanceData, _Time.y, _WorldSpaceCameraPos);
                float previousTime = _Time.y - unity_DeltaTime.x;
                float3 previousPositionWS = AnimatePositionWS(input.positionOS.xyz, input.normalOS, input.uv, instanceMatrix, instanceData, previousTime, _HectonPreviousCameraPosition);
                output.positionWS = currentPositionWS;
                output.vegetationData = float2(clamp(round(instanceData.Type), 0.0, 2.0), saturate(input.uv.y));

                output.positionCS = TransformWorldToHClip(currentPositionWS);
                output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(currentPositionWS, 1.0));
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(previousPositionWS, 1.0));
                ApplyMotionVectorZBias(output.positionCS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                half instanceType = input.vegetationData.x;
                half heightMask = input.vegetationData.y;
                half cutMask = ResolveVegetationCutMask(instanceType, input.positionWS);
                clip(0.08h - cutMask);

                if (instanceType > 1.5h)
                {
                    half porousCoverage = ResolveSargassumMotionCoverage(input.positionWS, heightMask);
                    clip(porousCoverage - 0.16h);
                }

                half coverage = saturate(_Opacity);
                clip(coverage - ResolveBayer4x4(floor(input.positionCS.xy)));

                return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
            }
            ENDHLSL
        }
    }
}

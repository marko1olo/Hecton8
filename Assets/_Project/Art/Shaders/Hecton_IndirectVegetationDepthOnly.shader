Shader "Hidden/Hecton8/VegetationIndirectDepthOnly"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [HideInInspector] _InteractionPushStrength ("Interaction Push Strength", Range(0, 4)) = 1.35
        [HideInInspector] _InteractionVelocityBias ("Interaction Velocity Bias", Range(0, 1)) = 0.85
        [HideInInspector] _InteractionDistancePower ("Interaction Distance Power", Range(1, 4)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnityIndirect.cginc"

            #define HECTON_MAX_INTERACTION_POINTS 12
            #define HECTON_MAX_IMPACT_SPHERES 8

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float4 _HectonVegetationRuntimeLodParams;
                float4 _HectonVegetationRuntimeDrawParams;
                // Kept per-material to match the lit pass rather than converted to globals: all three
                // live in UnityPerMaterial there, so a global would have meant editing the CBUFFER
                // layout and dropping authored values on four materials. HectonIndirectVegetationRenderer
                // copies them from the lit material instead, so there is still one authored source.
                half _InteractionPushStrength;
                half _InteractionVelocityBias;
                half _InteractionDistancePower;
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
            StructuredBuffer<float> _HectonFloraAges01;
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            StructuredBuffer<float2> _MarineSnowFlowField;
            float4 _ChunkWorldOffset;
            float4 _GlobalFloatingOffset;
            float4 _HectonFloatingOriginOffset;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;
            StructuredBuffer<float4> _HectonImpactSpheres;

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
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            float _HectonShallowWaterFieldActive;
            float _SargassumCutMaskActive;
            int _HectonFloraFlowFieldResolution;
            int _HectonFloraInteractionCount;
            int _HectonImpactSphereCount;

            TEXTURE2D(_HectonShallowWaterFieldRT);
            SAMPLER(sampler_HectonShallowWaterFieldRT);
            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);

            struct Attributes
            {
                uint instanceID : SV_InstanceID;
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 vegetationData : TEXCOORD1;
            };

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz;
            }

            float ApproxMagnitude2(float2 value)
            {
                float2 axis = abs(value);
                float major = max(axis.x, axis.y);
                float minor = min(axis.x, axis.y);
                return major + minor * 0.375;
            }

            float ApproxMagnitude3(float3 value)
            {
                float3 axis = abs(value);
                float major = max(max(axis.x, axis.y), axis.z);
                float minor = min(min(axis.x, axis.y), axis.z);
                float mid = axis.x + axis.y + axis.z - major - minor;
                return major + mid * 0.375 + minor * 0.125;
            }

            float3 SafeNormalize3(float3 value)
            {
                float approxLen = ApproxMagnitude3(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float3(0.0, 1.0, 0.0);
            }

            float2 SafeNormalize2(float2 value)
            {
                float approxLen = ApproxMagnitude2(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            float SanitizeNonNegativeFinite(float value)
            {
                return isfinite(value) ? max(value, 0.0) : 0.0;
            }

            float SanitizePositiveFinite(float value, float fallbackValue)
            {
                return isfinite(value) && value > fallbackValue ? value : fallbackValue;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return SafeNormalize3(mul((float3x3)matrixValue, direction));
            }

            float TrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float TriangleSigned(float phase)
            {
                return TrianglePulse01(phase) * 2.0 - 1.0;
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
                return max(ApproxMagnitude3(_GlobalOceanFlow.xyz), _HectonVegetationCurrentStrength);
            }

            // Was a local float frac-hash that disagreed with the lit pass, so the depth prepass
            // placed each plant on a different wind phase than the plant. Shared across all passes now.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationHash.hlsl"

            // Same story for the sway wave: the local TrianglePulse01 below displaced vertices on a
            // triangle where the lit pass uses a sine, so the prepass depth disagreed with forward.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationWave.hlsl"

            // This pass summed ONE flow field where the lit pass sums TWO, purely because the abyssal
            // field's seven globals were never declared here. They are C# globals, not material
            // properties, so declaring them costs no authoring and no material to keep in sync.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationAbyssalFlow.hlsl"

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

                float3 flowSample = ResolveMarineSnowFlowField(positionWS) + ResolveAbyssalFlowField(positionWS);
                float flowMagnitudeSq = dot(flowSample.xz, flowSample.xz);
                if (flowMagnitudeSq <= 0.00000001)
                    return float3(0.0, 0.0, 0.0);

                float typeScale = instanceType < 0.5 ? 0.24 : (instanceType < 1.5 ? 0.42 : 0.18);
                float flowWave = FastSinApprox(ResolveFlowSynchronyPhase(positionWS, instanceNoise));
                float flowScale = flowWave * max(_HectonFlowSynchronyParams.x, 1.0) * typeScale * bendMask;
                return float3(flowSample.x, 0.0, flowSample.z) * flowScale;
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
                float wave = FastSinApprox(sample.x * 1.18 + sample.y * 0.86 + _Time.y * 0.12) * 0.5 + 0.5;
                return saturate(coarse * 0.44 + fine * 0.34 + wave * 0.22);
            }

            half ResolveSargassumPorousCoverage(float3 positionWS, float heightMask)
            {
                float organicDensity = EvaluateSargassumOrganicDensity(positionWS.xz + float2(heightMask * 1.1, -heightMask * 0.9));
                float laceNoise = Hash21(floor(positionWS.xz * 1.65 + heightMask * 19.0));
                float interiorBias = lerp(0.58, 0.8, saturate(heightMask));
                return saturate(organicDensity * 1.15 + laceNoise * 0.18 - interiorBias);
            }

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationWakeTrail.hlsl"

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationInteraction.hlsl"

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationPlayerBend.hlsl"

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
                    if (!all(isfinite(impactSphere.xyz)))
                        continue;

                    float radius = SanitizePositiveFinite(impactSphere.w, 0.05);
                    float3 delta = evaluationPositionWS - impactSphere.xyz;
                    float proximity = saturate(1.0 - dot(delta, delta) / (radius * radius));
                    if (proximity <= 0.0001)
                        continue;

                    float3 planarDirection = SafeNormalize3(delta - baseNormalWS * dot(delta, baseNormalWS));
                    impactOffset += (planarDirection + float3(0.0, -0.18, 0.0)) * (proximity * proximity);
                }

                return impactOffset * bendMask;
            }

            float3 ResolveBillboardPositionWS(float3 originWS, float3 localPosition, float instanceHeight, float instanceWidth, float heightMask)
            {
                float3 cameraDelta = _WorldSpaceCameraPos - originWS;
                float3 cameraForwardXZ = SafeNormalize3(float3(cameraDelta.x, 0.0, cameraDelta.z));
                float3 billboardRight = SafeNormalize3(float3(cameraForwardXZ.z, 0.0, -cameraForwardXZ.x));
                float3 billboardUp = float3(0.0, 1.0, 0.0);
                float widthAtHeight = instanceWidth * lerp(1.0, 0.42, heightMask) * max(_HectonVegetationRuntimeDrawParams.y, 0.25);
                float heightScale = instanceHeight * max(_HectonVegetationRuntimeDrawParams.z, 0.25);
                return originWS + billboardRight * (localPosition.x * widthAtHeight) + billboardUp * (heightMask * heightScale);
            }

            // Reference body from the lit pass. The old local version hardcoded a 0.85 s decay and
            // read _Time.y raw, so it knew nothing of the negative-width encoding that means a 600 s
            // decay - for those instances the prepass computed an entropy progress off by most of the
            // range, not by a rounding error, and scaled the blade accordingly.
            float ResolveOrganicEntropyProgress(float encodedHeightScale, float encodedWidthScale, float timeValue)
            {
                if (!isfinite(encodedHeightScale))
                    return 0.0;

                if (encodedHeightScale >= 0.0)
                    return 0.0;

                float safeWidthScale = isfinite(encodedWidthScale) ? encodedWidthScale : 0.0;
                float entropyDuration = safeWidthScale < 0.0 ? 600.0 : 0.85;
                float entropyStartTime = safeWidthScale < 0.0 ? abs(safeWidthScale) : max(0.0, safeWidthScale);
                return saturate((timeValue - entropyStartTime) / entropyDuration);
            }

            float2 ResolveStateBlendWeights(float runtimeState)
            {
                float safeRuntimeState = isfinite(runtimeState) ? runtimeState : 0.0;
                float agitated = saturate(1.0 - abs(safeRuntimeState - 1.0));
                float dying = saturate(1.0 - abs(safeRuntimeState - 2.0));
                return float2(agitated, dying);
            }

            float ResolveMetadataGrowth01(float encodedGrowth01)
            {
                if (!isfinite(encodedGrowth01))
                    return 1.0;

                if (encodedGrowth01 < 0.0)
                    return -1.0;

                return encodedGrowth01 > 0.0001 ? saturate(SanitizeNonNegativeFinite(encodedGrowth01)) : 1.0;
            }

            float ResolveGrowth01(uint sourceInstanceIndex, float encodedGrowth01)
            {
                float soaAge01 = _HectonFloraAges01[sourceInstanceIndex];
                if (!isfinite(soaAge01))
                    return ResolveMetadataGrowth01(encodedGrowth01);

                if (soaAge01 < 0.0)
                    return -1.0;

                if (soaAge01 > 0.0001 || encodedGrowth01 <= 0.0001)
                    return saturate(SanitizeNonNegativeFinite(soaAge01));

                return ResolveMetadataGrowth01(encodedGrowth01);
            }

            float3 AnimatePositionWS(float3 localPosition, float3 normalOS, float2 uv, float4x4 instanceMatrix, HectonVegetationInstanceGpuData instanceData, uint sourceInstanceIndex)
            {
                float3 originWS = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0)) + _GlobalFloatingOffset.xyz;
                float instanceType = clamp(round(instanceData.Type), 0.0, 2.0);
                float encodedHeightScale = instanceData.HeightScale;
                float encodedWidthScale = instanceData.WidthScale;
                // Hoisted above the entropy call to match the lit pass, which computes the same
                // _Time.y * max(SwaySpeed, 0.05) before using it. It was declared further down here,
                // which is why the local entropy function had to invent its own time base.
                float timeValue = _Time.y * max(instanceData.SwaySpeed, 0.05);
                float entropyProgress = ResolveOrganicEntropyProgress(encodedHeightScale, encodedWidthScale, timeValue);
                float heightScale = saturate(abs(encodedHeightScale));
                float widthScale = entropyProgress > 0.0001 ? 1.0 : max(0.2, encodedWidthScale);
                float variation = frac(instanceData.Variation);
                float heightMask = saturate(uv.y);
                float bendMask = heightMask * heightMask * max(instanceData.BendAmplitude, 0.0);
                float curvatureMask = heightMask;
                float instanceNoise = Hash21(originWS.xz + variation);
                float instanceHeight;
                float instanceWidth;
                ResolveInstanceShape(instanceType, heightScale, widthScale, instanceHeight, instanceWidth);
                float growthHeightScale = saturate(ResolveGrowth01(sourceInstanceIndex, instanceData.Reserved0));
                float growthWidthScale = sqrt(max(growthHeightScale, 0.0));
                instanceHeight *= growthHeightScale;

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

                localPosition.xz *= growthWidthScale;
                float3 baseNormalWS = TransformDirection(instanceMatrix, normalOS);
                float3 driftOffsetWS = instanceType > 1.5 ? _SargassumGlobalDriftOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 basePositionWS = TransformPoint(instanceMatrix, localPosition) + driftOffsetWS + _GlobalFloatingOffset.xyz;
                float3 animatedPositionWS = basePositionWS;
                float3 sampledFlowVector = ResolveMarineSnowFlowField(basePositionWS);
                float2 sampledCurrentVector = sampledFlowVector.xz;
                float2 currentVector = dot(sampledCurrentVector, sampledCurrentVector) > 0.0001
                    ? SafeNormalize2(sampledCurrentVector)
                    : ResolvePlanarCurrentDirection();
                float currentStrength = max(ApproxMagnitude2(sampledCurrentVector), ResolvePlanarCurrentStrength());
                float healthSwayScale = lerp(0.35, 1.0, saturate(instanceData.HealthNormalized));
                float swayWave = TriangleSigned(timeValue * (0.55 + _HectonVegetationCurrentTimeScale * 0.35) + instanceNoise * 6.28318 + originWS.x * 0.015 + originWS.z * 0.01);
                float3 flowSynchronyOffset = ResolveFlowSynchronyOffset(basePositionWS, bendMask, instanceType, instanceNoise);
                animatedPositionWS.xz += currentVector * (currentStrength * 0.28 * bendMask * swayWave * healthSwayScale);
                animatedPositionWS.y += swayWave * (_HectonVegetationCurrentVerticalFactor * 0.12 * bendMask * healthSwayScale);
                animatedPositionWS += flowSynchronyOffset;
                animatedPositionWS += ResolveWakeTrailOffset(basePositionWS, baseNormalWS, bendMask, heightMask, instanceType);
                animatedPositionWS += ResolveInteractionOffset(
                    animatedPositionWS,
                    baseNormalWS,
                    bendMask,
                    ResolveVegetationViewDistanceSq(animatedPositionWS),
                    0.0) * (_InteractionPushStrength * ResolveInteractionTypeScale(instanceType));
                animatedPositionWS += ResolvePlayerBendOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType) *
                    (_InteractionPushStrength * 1.1);
                animatedPositionWS += ResolveImpactOffset(animatedPositionWS, baseNormalWS, bendMask) * 0.95;
                float2 stateWeights = ResolveStateBlendWeights(instanceData.RuntimeState);
                if (stateWeights.x > 0.0001 || stateWeights.y > 0.0001)
                {
                    float statePhase = TriangleSigned(timeValue * (1.35 + max(instanceData.PulseFrequency, 0.05)) + instanceNoise * 9.0 + heightMask * 3.2);
                    animatedPositionWS.xz += ResolvePlanarCurrentDirection() * (statePhase * bendMask * 0.16 * stateWeights.x);
                    animatedPositionWS.y -= instanceHeight * bendMask * (0.06 * stateWeights.x + 0.18 * stateWeights.y);
                }

                if (_HectonVegetationRuntimeLodParams.x >= 0.5)
                {
                    animatedPositionWS = ResolveBillboardPositionWS(originWS + driftOffsetWS, localPosition, instanceHeight, instanceWidth, heightMask);
                    animatedPositionWS += flowSynchronyOffset * 0.85;
                }

                float seasonalDecayWeight =
                    saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.y)) *
                    saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.w));
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

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                uint sourceInstanceIndex = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
                sourceInstanceIndex = unity_InstanceID;
#endif
                if (_HectonVegetationRuntimeDrawParams.w > 0.5)
                {
                    InitIndirectDrawArgs(0);
                    sourceInstanceIndex = _HectonVisibleInstanceIndices[GetIndirectInstanceID(sourceInstanceIndex)];
                }
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                HectonVegetationInstanceGpuData instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];
                float instanceType = clamp(round(instanceData.Type), 0.0, 2.0);
                float heightMask = saturate(input.uv.y);
                float3 positionWS = AnimatePositionWS(input.positionOS.xyz, input.normalOS, input.uv, instanceMatrix, instanceData, sourceInstanceIndex);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.vegetationData = float2(instanceType, heightMask);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half cutMask = ResolveVegetationCutMask(input.vegetationData.x, input.positionWS);
                half coverageVisibility = saturate((0.08h - cutMask) / 0.025h);

                if (input.vegetationData.x > 1.5h)
                {
                    half porousCoverage = ResolveSargassumPorousCoverage(input.positionWS, input.vegetationData.y);
                    coverageVisibility *= saturate((porousCoverage - 0.16h) / 0.08h);
                }

                half coverage = saturate(_Opacity);
                coverageVisibility *= (half)step(ResolveBayer4x4(floor(input.positionCS.xy)), coverage);

                return half4(0.0h, 0.0h, 0.0h, saturate(coverageVisibility));
            }
            ENDHLSL
        }
    }
}

using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Visor;
using Hecton8.Biolum;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GPU boids confined to dense sargassum walls. Density comes from <see cref="SargassumGlobalDragManager"/>
    /// and panic comes from <see cref="SargassumCutManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-101)]
    public sealed class SargassumMicroFaunaBoids : MonoBehaviour, ITickable, IFixedTickable, ISlowTickable, IOriginShiftListener
    {
        private const int MaxLeviathanNodePathIterations = 4096;
        private const int WhileLoopWatchdogLimit = 10000;
        private const float FullSimulationDistanceMeters = 50f;
        private const float SleepSimulationDistanceMeters = 200f;
        private const float MinimumPopulationBudgetScale = 0.35f;
#if UNITY_EDITOR
        private const int MaxEditorValidateDepth = 4;
        private static int _editorValidateDepth;
#endif

        internal static SargassumMicroFaunaBoids ActiveRuntimeInstance { get; private set; }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        internal struct BoidData
        {
            // Byte layout proof vs HLSL StructuredBuffer<BoidData>:
            // Position   -> offset  0, size 12
            // Velocity   -> offset 12, size 12
            // Panic      -> offset 24, size  4
            // StateFlags -> offset 28, size  4
            public Vector3 Position;
            public Vector3 Velocity;
            public float Panic;
            public uint StateFlags;
        }

        [Flags]
        private enum BoidStateFlags : uint
        {
            None = 0u,
            Active = 1u << 0,
            Hunting = 1u << 1,
            Fleeing = 1u << 2,
            Consumed = 1u << 3
        }

        [Flags]
        private enum MassiveThreatFlags : uint
        {
            None = 0u,
            LeviathanHuntPulse = 1u << 0
        }

        private enum SimulationLodTier : int
        {
            Full = 0,
            Simplified = 1,
            Sleep = 2
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct FoveatedSimulationInput
        {
            public float FrameDeltaTime;
            public float CameraDistanceSq;
            public float FullDistanceMeters;
            public float SleepDistanceMeters;
            public float MaxStepSeconds;
            public float MinTimeScale;
            public float PreviousAccumulator;
            public float Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct FoveatedSimulationDecision
        {
            public float SimulationDeltaTime;
            public float Hibernation01;
            public float Accumulator;
            public int Tier;
            public int DispatchSimulation;
            public float CameraDistanceMeters;
            public float Padding0;
            public float Padding1;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateSimulationLodJob : IJob
        {
            [ReadOnly] public NativeArray<FoveatedSimulationInput> Input;
            public NativeArray<FoveatedSimulationDecision> Output;

            public void Execute()
            {
                if (!Input.IsCreated || Input.Length <= 0 || !Output.IsCreated || Output.Length <= 0)
                    return;

                FoveatedSimulationInput input = Input[0];
                FoveatedSimulationDecision decision = default;
                float safeFrameDeltaTime = math.max(0f, input.FrameDeltaTime);
                float fullDistanceMeters = math.max(0f, input.FullDistanceMeters);
                float sleepDistanceMeters = math.max(fullDistanceMeters + 0.01f, input.SleepDistanceMeters);
                float cameraDistanceMeters = math.sqrt(math.max(0f, input.CameraDistanceSq));
                float safeMaxStepSeconds = math.max(1f / 60f, input.MaxStepSeconds);
                float safeMinTimeScale = math.clamp(input.MinTimeScale, 0.1f, 1f);
                float previousAccumulator = math.max(0f, input.PreviousAccumulator);
                decision.CameraDistanceMeters = cameraDistanceMeters;

                if (cameraDistanceMeters > sleepDistanceMeters)
                {
                    decision.Hibernation01 = 1f;
                    decision.Tier = (int)SimulationLodTier.Sleep;
                    Output[0] = decision;
                    return;
                }

                if (cameraDistanceMeters <= fullDistanceMeters)
                {
                    decision.Tier = (int)SimulationLodTier.Full;
                    decision.SimulationDeltaTime = safeFrameDeltaTime;
                    decision.DispatchSimulation = safeFrameDeltaTime > 0f ? 1 : 0;
                    Output[0] = decision;
                    return;
                }

                decision.Tier = (int)SimulationLodTier.Simplified;
                decision.Hibernation01 = math.saturate((cameraDistanceMeters - fullDistanceMeters) / math.max(0.01f, sleepDistanceMeters - fullDistanceMeters));
                decision.Accumulator = previousAccumulator + safeFrameDeltaTime;
                if (decision.Accumulator + 0.0001f < safeMaxStepSeconds)
                {
                    Output[0] = decision;
                    return;
                }

                decision.SimulationDeltaTime = decision.Accumulator * math.lerp(1f, safeMinTimeScale, decision.Hibernation01);
                decision.Accumulator = 0f;
                decision.DispatchSimulation = decision.SimulationDeltaTime > 0f ? 1 : 0;
                Output[0] = decision;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct GrazingAnchorData
        {
            public Vector3 Position;
            public float Radius;
            public float Strength;
            public float Phase;
            public Vector2 Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
        private struct MassiveThreatData
        {
            public Vector3 Position;
            public float InnerRadius;
            public float PanicRadius;
            public float Strength;
            public float EndTime;
            public Vector3 DirectionWS;
            public uint ThreatFlags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct FormationBeaconData
        {
            public Vector3 Position;
            public float Radius;
            public float Strength;
            public float Phase;
            public Vector2 Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct FormationObstacleData
        {
            public Vector3 Position;
            public float Radius;
            public float Weight;
            public Vector3 Padding;
        }

        private readonly struct StaticObstacleData
        {
            public readonly float3 Center;
            public readonly float3 Extents;
            public readonly float Radius;

            public StaticObstacleData(float3 center, float3 extents, float radius)
            {
                Center = center;
                Extents = extents;
                Radius = radius;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct LeviathanNodeData
        {
            public float3 Position;
            public float Distance01;
            public float3 Tangent;
            public float Radius;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct BuildLeviathanNodeJob : IJob
        {
            [ReadOnly] public NativeArray<float3> SourcePath;
            public int SourceCount;
            public NativeArray<LeviathanNodeData> OutputNodes;
            public NativeArray<int> OutputCount;
            public float BodyRadius;

            public void Execute()
            {
                if (!OutputCount.IsCreated || OutputCount.Length <= 0 || !OutputNodes.IsCreated)
                    return;

                OutputCount[0] = 0;
                int safePathCount = math.min(SourceCount, SourcePath.Length);
                if (safePathCount < 2)
                    return;

                float totalLength = 0f;
                for (int i = 1; i < safePathCount; i++)
                    totalLength += math.distance(SourcePath[i - 1], SourcePath[i]);

                if (totalLength <= 0.001f)
                    return;

                int targetCount = math.min(OutputNodes.Length, safePathCount);
                float distanceStep = totalLength / math.max(1, targetCount - 1);
                int pathCursor = 1;
                float traversed = 0f;
                float3 previousPoint = SourcePath[0];

                for (int nodeIndex = 0; nodeIndex < targetCount; nodeIndex++)
                {
                    float targetDistance = distanceStep * nodeIndex;
                    int pathIterationCount = 0;
                    int maxPathIterations = math.min(safePathCount, MaxLeviathanNodePathIterations);
                    int whileWatchdog = 0;
                    while (pathCursor < safePathCount && pathIterationCount < maxPathIterations)
                    {
                        if (whileWatchdog++ > WhileLoopWatchdogLimit)
                            break;

                        pathIterationCount++;
                        float segmentLength = math.distance(SourcePath[pathCursor - 1], SourcePath[pathCursor]);
                        if (traversed + segmentLength >= targetDistance || pathCursor >= safePathCount - 1)
                        {
                            float segmentT = segmentLength > 0.0001f
                                ? math.saturate((targetDistance - traversed) / segmentLength)
                                : 0f;
                            previousPoint = math.lerp(SourcePath[pathCursor - 1], SourcePath[pathCursor], segmentT);
                            break;
                        }

                        traversed += segmentLength;
                        pathCursor++;
                    }

                    if (pathCursor >= safePathCount)
                        previousPoint = SourcePath[safePathCount - 1];

                    OutputNodes[nodeIndex] = new LeviathanNodeData
                    {
                        Position = previousPoint,
                        Distance01 = 0f,
                        Tangent = new float3(0f, 0f, 1f),
                        Radius = BodyRadius
                    };
                }

                float cumulativeDistance = 0f;
                for (int nodeIndex = 0; nodeIndex < targetCount; nodeIndex++)
                {
                    float3 nodePosition = OutputNodes[nodeIndex].Position;
                    if (nodeIndex > 0)
                        cumulativeDistance += math.distance(OutputNodes[nodeIndex - 1].Position, nodePosition);

                    float3 tangent;
                    if (nodeIndex < targetCount - 1)
                        tangent = math.normalizesafe(OutputNodes[nodeIndex + 1].Position - nodePosition, new float3(0f, 0f, 1f));
                    else
                        tangent = math.normalizesafe(nodePosition - OutputNodes[math.max(0, nodeIndex - 1)].Position, new float3(0f, 0f, 1f));

                    float distance01 = totalLength > 0.0001f ? math.saturate(cumulativeDistance / totalLength) : 0f;
                    float bodyRadius = math.lerp(BodyRadius, math.max(0.5f, BodyRadius * 0.18f), distance01);
                    OutputNodes[nodeIndex] = new LeviathanNodeData
                    {
                        Position = nodePosition,
                        Distance01 = distance01,
                        Tangent = tangent,
                        Radius = bodyRadius
                    };
                }

                OutputCount[0] = targetCount;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 640)]
        private struct SimulationFrameConstants
        {
            public float4 Simulation0;
            public float4 Motion0;
            public float4 Neighbor0;
            public float4 Flocking0;
            public float4 Flocking1;
            public float4 Flocking2;
            public float4 Grazing0;
            public float4 Time0;
            public float4 FieldCenter;
            public float4 FieldExtents;
            public float4 SpatialGridOrigin;
            public int4 SpatialGridMeta;
            public int4 Counts0;
            public int4 Counts1;
            public float4 DensityWorldRect;
            public float4 CutMaskWorldRect;
            public float4 DriftOffset;
            public float4 DriftDelta;
            public float4 PlayerPosition;
            public float4 PlayerVelocity;
            public float4 PlayerRight;
            public float4 PlayerUp;
            public float4 PlayerForward;
            public float4 CameraAvoidPosition;
            public float4 CameraAvoidData;
            public float4 ParasiteAndFormation0;
            public float4 Formation1;
            public float4 Leviathan0;
            public float4 Leviathan1;
            public float4 Leviathan2;
            public float4 CameraPosition;
            public int4 ThreatGridMeta;
            public float4 ThreatGridCenter;
            public int4 ThreatVoxelMeta;
            public float4 ThreatVoxelOrigin;
            public float4 ThreatVoxelCellSize;
            public float4 TransportCapsule0;
            public float4 TransportCapsule1;
            public float4 Ecosystem0;
            public float4 Fragmentation0;
            public float4 Fragmentation1;
            public float4 SonarScatter0;
        }

        private const int BoidStride = 32;
        private const int GrazingAnchorStride = 32;
        private const int MassiveThreatStride = 48;
        private const int FormationBeaconStride = 32;
        private const int FormationObstacleStride = 32;
        private const int LeviathanNodeStride = 32;
        private const int PbdCorrectionStride = sizeof(int) * 4;
        private const int ThreatGridStride = sizeof(uint);
        private const int ThreatVoxelStride = sizeof(uint);
        private const int SpatialGridCountStride = sizeof(int);
        private const int SpatialGridCellEntryStride = sizeof(uint);
        private const int SpatialGridMaxAxisResolution = 32;
        private const int SpatialGridMaxCellCount =
            SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution;
        private const int SpatialGridClearThreadGroupSize = 64;
        private const int SpatialGridMaxBoidsPerCell = 32;
        private const float ThreatVoxelCellEpsilon = 0.001f;
        private const uint DefaultBoidStateFlags = (uint)(BoidStateFlags.Active | BoidStateFlags.Hunting);
        private const int LatchStatsLatchedCountIndex = 0;
        private const int LatchStatsLatchedSumXIndex = 1;
        private const int LatchStatsLatchedSumYIndex = 2;
        private const int LatchStatsLatchedSumZIndex = 3;
        private const int LatchStatsWakeCountIndex = 7;
        private const int LatchStatsWakePosXIndex = 8;
        private const int LatchStatsWakePosYIndex = 9;
        private const int LatchStatsWakePosZIndex = 10;
        private const int LatchStatsWakeVelXIndex = 11;
        private const int LatchStatsWakeVelYIndex = 12;
        private const int LatchStatsWakeVelZIndex = 13;
        private const int LatchStatsElementCount = 14;
        private const int LatchStatsStride = sizeof(int);
        private const float LatchStatsQuantize = 2048f;
        private const float WakeStatsQuantize = 1024f;
        private const int WakeMinimumFleeBoids = 20;
        private const float WakeMinimumSpeedMetersPerSecond = 10f;
        private const float WakeFlowStrength = 0.35f;
        private const float WakeFlowRadius = 6f;
        private const float WakeFlowLifetimeSeconds = 1.25f;
        private const uint HashSeed = 0x9E3779B9u;
        private const float SimulationPhaseWrapSeconds = 60f;
        private const int SimulationFrameConstantsStride = 640;
        private const int BoidDataPositionOffsetBytes = 0;
        private const int BoidDataVelocityOffsetBytes = 12;
        private const int BoidDataPanicOffsetBytes = 24;
        private const int BoidDataStrideBytes = 32;
        private const int BoidDataStateFlagsOffsetBytes = 28;
        private const int BoidDataAlignmentBytes = 4;
        private const uint ComputeThreadGroupSizeX = 64;
        private const string MainKernelName = "CSMain";
        private const string ClearLatchStatsKernelName = "ClearLatchStats";
        private const string ClearSpatialGridKernelName = "ClearSpatialGrid";
        private const string BuildSpatialGridKernelName = "BuildSpatialGrid";
        private const string ClearPbdCorrectionsKernelName = "ClearPBDCorrections";
        private const string PbdSolveKernelName = "KernelPBDSolve";
        private const string ApplyOriginShiftKernelName = "ApplyOriginShift";
        private const int MainKernelIndex = 0;
        private const int ClearLatchStatsKernelIndex = 1;
        private const int ClearSpatialGridKernelIndex = 2;
        private const int BuildSpatialGridKernelIndex = 3;
        private const int ClearPbdCorrectionsKernelIndex = 4;
        private const int PbdSolveKernelIndex = 5;
        private const int ApplyOriginShiftKernelIndex = 6;

        private static readonly int _BoidsBufferId = Shader.PropertyToID("_BoidsBuffer");
        private static readonly int _BoidsBufferReadId = Shader.PropertyToID("_BoidsBufferRead");
        private static readonly int _BoidsBufferWriteId = Shader.PropertyToID("_BoidsBufferWrite");
        private static readonly int _OriginShiftDeltaId = Shader.PropertyToID("_OriginShiftDelta");
        private static readonly int _BoidCountId = Shader.PropertyToID("_BoidCount");
        private static readonly int _DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int _FieldCenterId = Shader.PropertyToID("_FieldCenterWS");
        private static readonly int _FieldExtentsId = Shader.PropertyToID("_FieldExtents");
        private static readonly int _WaterLevelId = Shader.PropertyToID("_WaterLevel");
        private static readonly int _MinDepthId = Shader.PropertyToID("_MinDepthBelowSurface");
        private static readonly int _MaxDepthId = Shader.PropertyToID("_MaxDepthBelowSurface");
        private static readonly int _CruiseSpeedId = Shader.PropertyToID("_CruiseSpeed");
        private static readonly int _MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
        private static readonly int _PanicSpeedBoostId = Shader.PropertyToID("_PanicSpeedBoost");
        private static readonly int _PerceptionRadiusId = Shader.PropertyToID("_PerceptionRadius");
        private static readonly int _SeparationRadiusId = Shader.PropertyToID("_SeparationRadius");
        private static readonly int _BoidBodyRadiusId = Shader.PropertyToID("_BoidBodyRadius");
        private static readonly int _ConsumedCollapseSpeedId = Shader.PropertyToID("_ConsumedCollapseSpeed");
        private static readonly int _SeparationWeightId = Shader.PropertyToID("_SeparationWeight");
        private static readonly int _AlignmentWeightId = Shader.PropertyToID("_AlignmentWeight");
        private static readonly int _CohesionWeightId = Shader.PropertyToID("_CohesionWeight");
        private static readonly int _ContainmentWeightId = Shader.PropertyToID("_ContainmentWeight");
        private static readonly int _PanicWeightId = Shader.PropertyToID("_PanicWeight");
        private static readonly int _NoiseWeightId = Shader.PropertyToID("_NoiseWeight");
        private static readonly int _DensityThresholdId = Shader.PropertyToID("_DensityThreshold");
        private static readonly int _WindowThresholdId = Shader.PropertyToID("_WindowThreshold");
        private static readonly int _GradientWorldStepId = Shader.PropertyToID("_GradientWorldStep");
        private static readonly int _PanicThresholdId = Shader.PropertyToID("_PanicThreshold");
        private static readonly int _PanicDecayId = Shader.PropertyToID("_PanicDecay");
        private static readonly int _GrazingAnchorsId = Shader.PropertyToID("_GrazingAnchors");
        private static readonly int _GrazingAnchorCountId = Shader.PropertyToID("_GrazingAnchorCount");
        private static readonly int _GrazingWeightId = Shader.PropertyToID("_GrazingWeight");
        private static readonly int _GrazingRadiusId = Shader.PropertyToID("_GrazingRadius");
        private static readonly int _GrazingRestSpeedScaleId = Shader.PropertyToID("_GrazingRestSpeedScale");
        private static readonly int _GrazingRestHoldThresholdId = Shader.PropertyToID("_GrazingRestHoldThreshold");
        private static readonly int _CanopyAffinityWeightId = Shader.PropertyToID("_CanopyAffinityWeight");
        private static readonly int _SimulationTimeId = Shader.PropertyToID("_SimulationTime");
        private static readonly int _PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");
        private static readonly int _PlayerPositionId = Shader.PropertyToID("_PlayerPositionWS");
        private static readonly int _PlayerVelocityId = Shader.PropertyToID("_PlayerVelocityWS");
        private static readonly int _PlayerRightId = Shader.PropertyToID("_PlayerRightWS");
        private static readonly int _PlayerUpId = Shader.PropertyToID("_PlayerUpWS");
        private static readonly int _PlayerForwardId = Shader.PropertyToID("_PlayerForwardWS");
        private static readonly int _PlayerSpeedId = Shader.PropertyToID("_PlayerSpeed");
        private static readonly int _PanicPlayerSpeedThresholdId = Shader.PropertyToID("_PanicPlayerSpeedThreshold");
        private static readonly int _PanicPlayerRadiusId = Shader.PropertyToID("_PanicPlayerRadius");
        private static readonly int _PanicPlayerRadiusScaleId = Shader.PropertyToID("_PanicPlayerRadiusScale");
        private static readonly int _CameraAvoidPositionId = Shader.PropertyToID("_CameraAvoidPositionWS");
        private static readonly int _CameraAvoidRadiusId = Shader.PropertyToID("_CameraAvoidRadius");
        private static readonly int _CameraAvoidWeightId = Shader.PropertyToID("_CameraAvoidWeight");
        private static readonly int _MassiveThreatsId = Shader.PropertyToID("_MassiveThreats");
        private static readonly int _MassiveThreatCountId = Shader.PropertyToID("_MassiveThreatCount");
        private static readonly int _MassiveThreatWeightId = Shader.PropertyToID("_MassiveThreatWeight");
        private static readonly int _VatEnabledId = Shader.PropertyToID("_VatEnabled");
        private static readonly int _VatPositionTexId = Shader.PropertyToID("_VatPositionTex");
        private static readonly int _VatNormalTexId = Shader.PropertyToID("_VatNormalTex");
        private static readonly int _VatFrameCountId = Shader.PropertyToID("_VatFrameCount");
        private static readonly int _VatVertexCountId = Shader.PropertyToID("_VatVertexCount");
        private static readonly int _VatPlaybackSpeedId = Shader.PropertyToID("_VatPlaybackSpeed");
        private static readonly int _VatInstancePhaseScaleId = Shader.PropertyToID("_VatInstancePhaseScale");
        private static readonly int _VatPositionScaleId = Shader.PropertyToID("_VatPositionScale");
        private static readonly int _VatNormalBlendId = Shader.PropertyToID("_VatNormalBlend");
        private static readonly int _DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int _DensityWorldRectId = Shader.PropertyToID("_DensityWorldRect");
        private static readonly int _CutMaskTexId = Shader.PropertyToID("_CutMaskTex");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_CutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_CutMaskActive");
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_GlobalDriftOffset");
        private static readonly int _GlobalDriftDeltaId = Shader.PropertyToID("_GlobalDriftDelta");
        private static readonly int _DeepModeId = Shader.PropertyToID("_DeepMode");
        private static readonly int _DeepClusterWeightId = Shader.PropertyToID("_DeepClusterWeight");
        private static readonly int _HeadlightPanicId = Shader.PropertyToID("_HeadlightPanic");
        private static readonly int _ParasiteModeId = Shader.PropertyToID("_ParasiteMode");
        private static readonly int _ParasiteAffinityWeightId = Shader.PropertyToID("_ParasiteAffinityWeight");
        private static readonly int _ParasiteAggressionId = Shader.PropertyToID("_ParasiteAggression");
        private static readonly int _VelocitySleepScaleId = Shader.PropertyToID("_VelocitySleepScale");
        private static readonly int _ParasiteLatchRadiusId = Shader.PropertyToID("_ParasiteLatchRadius");
        private static readonly int _LatchStatsId = Shader.PropertyToID("_LatchStats");
        private static readonly int _FormationModeId = Shader.PropertyToID("_FormationMode");
        private static readonly int _FormationBeaconsId = Shader.PropertyToID("_FormationBeacons");
        private static readonly int _FormationBeaconCountId = Shader.PropertyToID("_FormationBeaconCount");
        private static readonly int _FormationWeightId = Shader.PropertyToID("_FormationWeight");
        private static readonly int _FormationRingThicknessId = Shader.PropertyToID("_FormationRingThickness");
        private static readonly int _FormationPulseAmplitudeId = Shader.PropertyToID("_FormationPulseAmplitude");
        private static readonly int _FormationPulseSpeedId = Shader.PropertyToID("_FormationPulseSpeed");
        private static readonly int _FormationBreakPanicThresholdId = Shader.PropertyToID("_FormationBreakPanicThreshold");
        private static readonly int _FormationObstaclesId = Shader.PropertyToID("_FormationObstacles");
        private static readonly int _FormationObstacleCountId = Shader.PropertyToID("_FormationObstacleCount");
        private static readonly int _FormationObstacleWeightId = Shader.PropertyToID("_FormationObstacleWeight");
        private static readonly int _LeviathanModeId = Shader.PropertyToID("_LeviathanMode");
        private static readonly int _LeviathanNodesId = Shader.PropertyToID("_LeviathanNodes");
        private static readonly int _LeviathanNodeCountId = Shader.PropertyToID("_LeviathanNodeCount");
        private static readonly int _LeviathanBodyWeightId = Shader.PropertyToID("_LeviathanBodyWeight");
        private static readonly int _LeviathanForwardWeightId = Shader.PropertyToID("_LeviathanForwardWeight");
        private static readonly int _LeviathanWaveAmplitudeId = Shader.PropertyToID("_LeviathanWaveAmplitude");
        private static readonly int _LeviathanWaveFrequencyId = Shader.PropertyToID("_LeviathanWaveFrequency");
        private static readonly int _LeviathanThreatLevelId = Shader.PropertyToID("_LeviathanThreatLevel");
        private static readonly int _LeviathanSurroundThreatThresholdId = Shader.PropertyToID("_LeviathanSurroundThreatThreshold");
        private static readonly int _LeviathanSurroundRadiusId = Shader.PropertyToID("_LeviathanSurroundRadius");
        private static readonly int _LeviathanSurroundWeightId = Shader.PropertyToID("_LeviathanSurroundWeight");
        private static readonly int _LeviathanSurroundSpinSpeedId = Shader.PropertyToID("_LeviathanSurroundSpinSpeed");
        private static readonly int _LeviathanModeBlendId = Shader.PropertyToID("_LeviathanModeBlend");
        private static readonly int _PbdCorrectionsId = Shader.PropertyToID("_PbdCorrections");
        private static readonly int _ThreatGridId = Shader.PropertyToID("_ThreatGrid2D");
        private static readonly int _ThreatVoxelGridId = Shader.PropertyToID("_ThreatVoxelGrid");
        private static readonly int _SpatialGridCountsId = Shader.PropertyToID("_SpatialGridCounts");
        private static readonly int _SpatialGridCellsId = Shader.PropertyToID("_SpatialGridCells");
        private static readonly int _SimulationFrameBufferId = Shader.PropertyToID("_SargassumSimulationFrame");

        [Header("â”€â”€ Runtime Wiring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField]
        [Tooltip("Compute shader that simulates the micro-fauna flock.")]
        private ComputeShader boidCompute;

        [SerializeField]
        [Tooltip("Instanced mesh rendered for each micro-fauna boid.")]
        private Mesh boidMesh;

        [SerializeField]
        [Tooltip("Instanced material used by RenderMeshPrimitives.")]
        private Material boidMaterial;

        [Header("── VAT Rendering ──────────────────")]
        [SerializeField]
        [Tooltip("Optional VAT position texture used to animate small-fish vertices entirely on the GPU.")]
        private Texture2D boidVatPositionTexture;

        [SerializeField]
        [Tooltip("Optional VAT normal texture paired with the position VAT. Leave unset to fall back to procedural tail wag.")]
        private Texture2D boidVatNormalTexture;

        [SerializeField, Min(1)]
        [Tooltip("Frame count stored in the VAT textures.")]
        private int boidVatFrameCount = 1;

        [SerializeField, Min(0f)]
        [Tooltip("Playback speed multiplier applied to the VAT animation.")]
        private float boidVatPlaybackSpeed = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Per-instance VAT phase offset scale. Multiplied by SV_InstanceID in the shader.")]
        private float boidVatInstancePhaseScale = 0.0175f;

        [SerializeField, Min(0.0001f)]
        [Tooltip("World-scale multiplier applied to VAT position samples.")]
        private float boidVatPositionScale = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Blend factor between authored mesh normals and VAT normal samples.")]
        private float boidVatNormalBlend = 1f;

        [SerializeField]
        [Tooltip("Primary density owner. If null the controller resolves the active runtime singleton.")]
        private SargassumGlobalDragManager dragManager;

        [SerializeField]
        [Tooltip("Primary cut-mask owner. If null the controller resolves the active runtime singleton.")]
        private SargassumCutManager cutManager;

        [SerializeField]
        [Tooltip("Optional deep-sea biolum owner used when the flock switches from canopy grazing into abyssal bait-ball mode.")]
        private HectonBiolumManager biolumManager;

        [SerializeField]
        [Tooltip("Optional direct gameplay camera override for frustum culling.")]
        private Camera viewCamera;

        [SerializeField]
        [Tooltip("Optional direct player override used only to resolve the gameplay camera hierarchy.")]
        private Transform playerTransform;

        [Header("â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(128, 2048)]
        [Tooltip("Total boid count rendered and simulated on the GPU.")]
        private int boidCount = 768;

        [SerializeField, Range(0.15f, 1f)]
        [Tooltip("Minimum density required for spawn and containment.")]
        private float densityThreshold = 0.42f;

        [SerializeField, Range(0f, 0.75f)]
        [Tooltip("Maximum allowed window openness for valid spawn points. Lower values keep boids inside dense walls.")]
        private float windowThreshold = 0.32f;

        [SerializeField, Range(4, 32)]
        [Tooltip("Maximum rejection-sampling attempts per boid when rebuilding the spawn set.")]
        private int maxSpawnAttempts = 18;

        [Header("â”€â”€ Motion â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Steady-state swim speed before panic boosts are applied.")]
        private float cruiseSpeed = 1.8f;

        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Hard velocity clamp for the GPU simulation.")]
        private float maxSpeed = 3.8f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional speed unlocked while fleeing from the cut mask.")]
        private float panicSpeedBoost = 2.4f;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Neighbor perception radius used for cohesion and alignment.")]
        private float perceptionRadius = 2.25f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Personal-space radius used for short-range separation.")]
        private float separationRadius = 0.85f;

        [SerializeField, Range(0.02f, 1.5f)]
        [Tooltip("Hard-sphere radius used by the GPU collision constraint pass. Must stay below the soft separation radius.")]
        private float boidBodyRadius = 0.18f;

        [SerializeField, Range(2f, 24f)]
        [Tooltip("Collapse speed applied to consumed boids before they are fully swallowed and clipped out of rendering.")]
        private float consumedCollapseSpeed = 6f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Separation force weight.")]
        private float separationWeight = 1.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Alignment force weight.")]
        private float alignmentWeight = 0.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Cohesion force weight.")]
        private float cohesionWeight = 0.7f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Force that keeps boids inside dense sargassum walls.")]
        private float containmentWeight = 3.4f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Force applied away from fresh cuts.")]
        private float panicWeight = 4.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Low-amplitude deterministic wander added to avoid rigid movement.")]
        private float noiseWeight = 0.35f;

        [SerializeField, Range(0.05f, 4f)]
        [Tooltip("World-space sampling step used when computing density and cut gradients.")]
        private float gradientWorldStep = 0.8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Cut-mask value that upgrades the flock into panic mode.")]
        private float panicThreshold = 0.08f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Seconds^-1 decay applied to the per-boid panic accumulator.")]
        private float panicDecay = 1.4f;

        [Header("â”€â”€ Grazing & Threat Response â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(4, 96)]
        [Tooltip("Deterministic pneumatocyst grazing anchors sampled inside dense canopy walls.")]
        private int grazingAnchorCount = 28;

        [SerializeField, Range(0.25f, 6f)]
        [Tooltip("World-space radius around each grazing anchor that attracts nearby boids.")]
        private float grazingRadius = 2.35f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Attraction force toward nearby pneumatocyst grazing anchors while calm.")]
        private float grazingWeight = 1.25f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional calm-state pull toward the densest nearby canopy so the flock stays inside the thickest walls instead of orbiting dead centers.")]
        private float canopyAffinityWeight = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum dense-wall value required before a grazing anchor is accepted.")]
        private float grazingDensityThreshold = 0.58f;

        [SerializeField, Range(0.05f, 0.6f)]
        [Tooltip("Speed scale applied while a calm boid is in a short feeding pause near a pneumatocyst anchor.")]
        private float grazingRestSpeedScale = 0.12f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Minimum grazing hold intensity required before a boid can briefly freeze to imitate feeding.")]
        private float grazingRestHoldThreshold = 0.48f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Player approach speed that upgrades a nearby flock from calm grazing into panic.")]
        private float panicPlayerSpeedThreshold = 2.4f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Player threat radius used when evaluating fast approach panic.")]
        private float panicPlayerRadius = 3.6f;

        [SerializeField, Range(0.25f, 3f)]
        [Tooltip("Radius around the gameplay camera that repels boids and prevents near-field clipping through the player's view volume.")]
        private float cameraAvoidRadius = 0.95f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Strength of the camera-avoidance force applied when a boid enters the player's near clip bubble.")]
        private float cameraAvoidWeight = 4.8f;

        [SerializeField, Range(0.25f, 12f)]
        [Tooltip("World-space ray distance projected through the ecosystem threat voxel grid before the boid commits a terrain-avoidance turn.")]
        private float voxelAvoidanceLookAheadDistance = 3.5f;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Repulsive steering weight applied when the velocity-aligned voxel DDA detects solid terrain ahead.")]
        private float voxelAvoidanceWeight = 7.5f;

        [SerializeField, Range(1, 8)]
        [Tooltip("Hard cap for concurrent leviathan or submarine panic threats cached on the CPU and uploaded to the compute shader.")]
        private int maxMassiveThreatCount = 4;

        [SerializeField, Range(50f, 96f)]
        [Tooltip("Minimum flee radius used when a leviathan-scale object tears through the canopy.")]
        private float massiveThreatPanicRadius = HectonVegetationConstants.BoidMassiveDisplacementPanicRadius;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Additional flee force weight applied when a leviathan-scale threat is active.")]
        private float massiveThreatWeight = 8.6f;

        [Header("── Adaptive Prey Genetics ─────────────")]
        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional canopy and obstacle-hugging force derived from ecosystem camouflage adaptation.")]
        private float ecosystemCamouflageWeight = 1.8f;

        [Header("── Swarm Fragmentation ─────────────")]
        [SerializeField, Range(0f, 12f)]
        [Tooltip("Steering weight applied while a massive displacement event splits the swarm into two temporary centers.")]
        private float fragmentationWeight = 5.2f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Offset scale used when deriving the two temporary swarm fragment centers from a massive displacement event.")]
        private float fragmentationOffsetScale = 1.6f;

        [SerializeField, Range(5f, 10f)]
        [Tooltip("Minimum duration the split swarm persists before cohesion fully reasserts itself.")]
        private float fragmentationMinDurationSeconds = 5f;

        [SerializeField, Range(5f, 10f)]
        [Tooltip("Maximum duration the split swarm persists before cohesion fully reasserts itself.")]
        private float fragmentationMaxDurationSeconds = 9f;

        [Header("── Active Sonar Evasion ─────────────")]
        [SerializeField, Range(1f, 120f)]
        [Tooltip("Propagation speed of the active-sonar panic wave injected into the boid compute shader.")]
        private float activeSonarWaveSpeed = 52f;

        [SerializeField, Range(0.25f, 12f)]
        [Tooltip("Band width of the active-sonar wavefront that scatters boids when it reaches them.")]
        private float activeSonarWaveBandWidth = 3.25f;

        [SerializeField, Range(0f, 24f)]
        [Tooltip("Instantaneous velocity override strength applied when the active-sonar wave reaches a boid.")]
        private float activeSonarScatterImpulse = 12f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional flee acceleration applied while the active-sonar wave crosses the swarm.")]
        private float activeSonarScatterWeight = 6.8f;

        [Header("â”€â”€ Vertical Band â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Min(0f)]
        [Tooltip("Water surface level used to clamp the vertical simulation band.")]
        private float waterLevel = 4900f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Minimum depth below the surface for the boid band.")]
        private float minDepthBelowSurface = 0.8f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Maximum depth below the surface for the boid band.")]
        private float maxDepthBelowSurface = 4.5f;

        [Header("â”€â”€ Deep Sea Adaptation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField]
        [Tooltip("World-space Y threshold where the flock abandons canopy confinement and rebuilds as abyssal bait balls around biolum sources.")]
        private float deepSeaWorldYThreshold = -1000f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Maximum nearby biolum zones copied into the deep-sea bait-ball anchor set without allocations.")]
        private int deepBiolumAnchorCapacity = 8;

        [SerializeField, Range(10f, 250f)]
        [Tooltip("Maximum search radius used when harvesting nearby biolum anchors for abyssal bait-ball mode.")]
        private float deepBiolumSearchRadius = 140f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Horizontal radius of the dense bait-ball cluster around each deep biolum source.")]
        private float deepBaitBallRadius = 4.5f;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Vertical half-height used by abyssal bait-ball spawn and render bounds.")]
        private float deepBaitBallHeight = 2.1f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional anchor-attraction weight applied while deep bait-ball mode is active.")]
        private float deepClusterWeight = 3.8f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Seconds that abyssal boids stay in headlight panic after a sudden lamp activation while transport is active.")]
        private float deepHeadlightPanicDuration = 3.5f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Additional player panic radius multiplier applied while abyssal headlight panic is active.")]
        private float deepHeadlightPanicRadiusScale = 2.8f;

        [SerializeField, Range(-4000f, -1000f)]
        [Tooltip("World-space Y threshold where abyssal technical zones replace calm fish behavior with parasite-drone affinity toward active transport.")]
        private float parasiteDroneWorldYThreshold = -2000f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Base attraction weight pulling parasite drones toward an active scooter hull in abyssal technical zones.")]
        private float parasiteAffinityWeight = 4.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized hull-stress request applied while parasite drones aggressively latch onto a lit scooter hull.")]
        private float parasiteHullStressIntensity = 0.42f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Additional hull-stress intensity unlocked when scooter lights are active and parasite drones switch into hard latch behavior.")]
        private float parasiteHullStressLightBoost = 0.34f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Near-hull radius used when parasite drones clamp to the scooter body instead of orbiting at bait-ball distance.")]
        private float parasiteLatchRadius = 1.35f;

        [SerializeField, Range(1, 96)]
        [Tooltip("Latched drone count that drives parasite drag to its maximum multiplier without needing a larger GPU readback payload.")]
        private int parasiteMaxLatchedDronesForFullDrag = 24;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum additional environmental drag multiplier applied to active transport while parasite drones stay latched to the hull.")]
        private float parasiteMaxEnvironmentalDragMultiplier = 1.85f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Minimum interval between asynchronous GPU latch-count readbacks. Keeps the CPU informed without stalling the render thread.")]
        private float parasiteLatchReadbackInterval = 0.12f;

        [SerializeField, Range(1, 32)]
        [Tooltip("Minimum latched parasite count required before the hive starts dragging the player toward the nearest DeadZone massive structure.")]
        private int parasiteHarvesterLatchThreshold = 5;

        [SerializeField, Range(1, 96)]
        [Tooltip("Latched parasite count treated as full harvester pull strength.")]
        private int parasiteHarvesterFullLatchCount = 18;

        [Header("â”€â”€ Hive-Mind Formation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(1, 8)]
        [Tooltip("Maximum nearby abyss beacons copied into the GPU formation anchor set without allocations.")]
        private int formationBeaconCapacity = 4;

        [SerializeField, Range(8f, 250f)]
        [Tooltip("Maximum search radius for nearby abyss beacons used by the calm hive-mind ring formation.")]
        private float formationBeaconSearchRadius = 120f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Formation pull weight applied when the abyssal hive-mind is calm.")]
        private float formationWeight = 3.2f;

        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Thickness of the procedural ring formation around nearby abyss beacons.")]
        private float formationRingThickness = 1.8f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Radius pulse amplitude applied to the hive-mind ring to make it breathe like a synthetic organism.")]
        private float formationPulseAmplitude = 0.26f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Pulse speed applied to the hive-mind ring animation.")]
        private float formationPulseSpeed = 1.1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Panic level above which the hive-mind abandons geometric formation and returns to flee behavior.")]
        private float formationBreakPanicThreshold = 0.24f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Maximum obstacle proxies uploaded to the compute shader so the ring can bend around nearby rock silhouettes.")]
        private int formationObstacleCapacity = 8;

        [SerializeField]
        [Tooltip("Collider layers treated as formation obstacles. Use rock / ruin / terrain layers only.")]
        private LayerMask formationObstacleLayers = ~0;

        [SerializeField, Range(4f, 80f)]
        [Tooltip("Non-alloc overlap radius used when harvesting nearby rock obstacles for formation avoidance.")]
        private float formationObstacleSearchRadius = 24f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Repulsion weight applied against uploaded formation obstacle proxies.")]
        private float formationObstacleWeight = 3.6f;

        [Header("â”€â”€ Swarm Leviathan â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(8, 64)]
        [Tooltip("Maximum abyssal nav-path nodes copied into the leviathan body spline without allocations.")]
        private int leviathanNodeCapacity = 24;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum threat-hotspot level required before parasite drones collapse into LeviathanForm.")]
        private float leviathanThreatThreshold = 0.42f;

        [SerializeField, Range(10f, 200f)]
        [Tooltip("Minimum hotspot distance from the player before the leviathan path will arm.")]
        private float leviathanHotspotMinDistance = 28f;

        [SerializeField, Range(20f, 400f)]
        [Tooltip("Maximum hotspot distance sampled when asking the cartographer for the current leviathan target.")]
        private float leviathanHotspotMaxDistance = 180f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Radial pull that keeps each drone collapsed onto the leviathan body spline.")]
        private float leviathanBodyWeight = 4.8f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Forward steering weight that drives the swarm body along the abyssal nav spline.")]
        private float leviathanForwardWeight = 3.6f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Maximum local body radius used by the leviathan spline before tail taper is applied.")]
        private float leviathanBodyRadius = 6.5f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Lateral amplitude of the leviathan body undulation.")]
        private float leviathanWaveAmplitude = 0.42f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Temporal frequency of the leviathan body undulation.")]
        private float leviathanWaveFrequency = 1.35f;

        [SerializeField, Range(0.6f, 1f)]
        [Tooltip("Threat level where the centipede abandons hotspot pursuit and starts closing a player ring.")]
        private float leviathanSurroundThreatThreshold = 0.8f;

        [SerializeField, Range(4f, 48f)]
        [Tooltip("Base ring radius used when the leviathan swarm surrounds the player.")]
        private float leviathanSurroundRadius = 14f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional pull applied toward the player ring once threat exceeds the surround threshold.")]
        private float leviathanSurroundWeight = 4.25f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Angular speed of the encirclement ring around the player.")]
        private float leviathanSurroundSpinSpeed = 0.7f;

        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Blend sharpness used when transitioning between free swarm behavior and leviathan-form body steering without teleporting the flock.")]
        private float leviathanModeBlendSharpness = 3.2f;

        [SerializeField, Range(25f, 300f)]
        [Tooltip("Maximum camera distance allowed before the indirect draw is culled. Simulation continues behind hibernation cadence beyond this range.")]
        private float simulationCullDistance = SleepSimulationDistanceMeters;

        [SerializeField, Range(16f, 240f)]
        [Tooltip("Distance where the GPU flock starts stepping on a slower hibernation cadence instead of running full-rate simulation.")]
        private float hibernationStartDistance = FullSimulationDistanceMeters;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Maximum accumulated simulation step applied when the swarm is fully hibernating at long distance.")]
        private float hibernationMaxStepSeconds = 0.18f;

        [SerializeField, Range(0.15f, 1f)]
        [Tooltip("Time-scale multiplier applied to far-field hibernation steps so distant swarms decelerate instead of freezing hard.")]
        private float hibernationMinTimeScale = 0.4f;

        [Header("â”€â”€ Leviathan Strike â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(1f, 24f)]
        [Tooltip("Radius around the swarm-head centerline that counts as a direct physical strike on the player hull.")]
        private float leviathanStrikeRadius = 5f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Normalized trauma weight passed into HectonPlayerMovement when the leviathan head collides with the player.")]
        private float leviathanStrikeTraumaWeight = 0.48f;

        [SerializeField, Range(1f, 120f)]
        [Tooltip("Base impulse magnitude forwarded into ApplyPhysicalTrauma when the leviathan head lands a strike.")]
        private float leviathanStrikeImpulse = 34f;

        [SerializeField, Range(0.1f, 100f)]
        [Tooltip("Health damage injected into HectonPlayerHealth when the leviathan head lands a confirmed strike.")]
        private float leviathanStrikeDamage = 12f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Cooldown between successive physical head-strikes so the player is not re-traumatized every fixed step.")]
        private float leviathanStrikeCooldown = 0.42f;

        [SerializeField, Range(2f, 40f)]
        [Tooltip("Minimum leviathan-head speed required before the swarm emits a debris-pushing shockwave.")]
        private float leviathanShockwaveSpeedThreshold = 8.5f;

        [SerializeField, Range(2f, 32f)]
        [Tooltip("Radius used when the leviathan shockwave pushes nearby rigidbodies and registered field debris.")]
        private float leviathanShockwaveRadius = 15f;

        [SerializeField, Range(2f, 96f)]
        [Tooltip("Impulse magnitude applied to nearby rigidbodies when the leviathan head emits a high-speed shockwave.")]
        private float leviathanShockwaveImpulse = 18f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Additional upward bias applied to leviathan shockwaves so floating debris gets kicked clear of the path.")]
        private float leviathanShockwaveVerticalLift = 0.24f;

        [SerializeField, Range(0.05f, 1.5f)]
        [Tooltip("Cooldown between consecutive shockwave force bursts while the leviathan keeps sprinting.")]
        private float leviathanShockwaveCadence = 0.18f;

        [SerializeField, Range(4, 32)]
        [Tooltip("Maximum rigidbody candidates processed per leviathan shockwave without allocations.")]
        private int leviathanShockwaveHitCapacity = 12;

        [SerializeField]
        [Tooltip("Layer mask used when the leviathan supplements the vegetation spatial hash with a rigidbody overlap query.")]
        private LayerMask leviathanShockwaveLayers = ~0;

        [Header("â”€â”€ Rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField]
        [Tooltip("Shadow mode used for the indirect draw.")]
        private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("True if the indirect draw should render into the layer of this GameObject.")]
        private bool useGameObjectLayer = true;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField]
        [Tooltip("Field revision used to build the current spawn set.")]
        private int _debugFieldRevision;

        [SerializeField]
        [Tooltip("Current render bounds used by RenderMeshPrimitives.")]
        private Bounds _debugRenderBounds;

        [SerializeField]
        [Tooltip("Current drift offset applied to the boid field.")]
        private Vector3 _debugDriftOffset;

        [SerializeField]
        [Tooltip("Current dispatch group count.")]
        private int _debugDispatchGroups;

        [SerializeField]
        [Tooltip("Current active boid count after world budget culling is applied.")]
        private int _debugActiveBoidCount;

        [SerializeField]
        [Tooltip("Current world-budget scale applied to the authored boid capacity.")]
        private float _debugPopulationBudgetScale = 1f;

        [SerializeField]
        [Tooltip("Current prey fitness sampled from the ecosystem sector containing the player.")]
        private float _debugEcosystemFitness;

        [SerializeField]
        [Tooltip("Current prey camouflage bias sampled from the ecosystem sector containing the player.")]
        private float _debugEcosystemCamouflageIndex;

        [SerializeField]
        [Tooltip("Active pneumatocyst grazing anchor count uploaded to the GPU.")]
        private int _debugGrazingAnchorCount;

        [SerializeField]
        [Tooltip("Latest parasite center-of-mass reconstructed from the asynchronous GPU stats readback in player-local space.")]
        private Vector3 _debugParasiteCenterOfMassLS;

        [SerializeField]
        [Tooltip("Latest harvester pull direction resolved against the nearest DeadZone massive-structure anchor.")]
        private Vector3 _debugParasiteHarvesterPullWS;

        [SerializeField]
        [Tooltip("Measured player speed fed into the panic gate.")]
        private float _debugPlayerSpeed;

        [SerializeField]
        [Tooltip("Current panic-radius multiplier uploaded to the GPU. Transport spikes this to the authored scooter fear radius.")]
        private float _debugPlayerPanicRadiusScale = 1f;

        [SerializeField]
        [Tooltip("Current far-field hibernation blend used to downsample simulation cadence without freezing the swarm.")]
        private float _debugHibernation01;

        #pragma warning disable CS0414
        [SerializeField]
        [Tooltip("True when the boid bounds intersect the current gameplay camera frustum.")]
        private bool _debugVisible;
        #pragma warning restore CS0414

        [SerializeField]
        [Tooltip("Active leviathan-scale panic threat count uploaded to the compute shader.")]
        private int _debugMassiveThreatCount;

        [SerializeField]
        [Tooltip("True while the flock is running in abyssal bait-ball mode instead of canopy mode.")]
        private bool _debugDeepModeActive;

        [SerializeField]
        [Tooltip("Active abyssal headlight-panic strength uploaded to the compute shader.")]
        private float _debugHeadlightPanic01;

        [SerializeField]
        [Tooltip("True while abyssal technical zones replace calm bait-ball fish behavior with parasite-drone hull affinity.")]
        private bool _debugParasiteModeActive;

        [SerializeField]
        [Tooltip("Current parasite aggression strength uploaded to the compute shader. Lights drive this toward hard hull latch behavior.")]
        private float _debugParasiteAggression01;

        [SerializeField]
        [Tooltip("Latest asynchronously reported count of parasite drones currently latched onto the player hull.")]
        private int _debugLatchedDroneCount;

        [SerializeField]
        [Tooltip("True while the abyssal flock is using calm hive-mind geometric formation instead of bait-ball clustering.")]
        private bool _debugFormationModeActive;

        [SerializeField]
        [Tooltip("Active nearby formation beacon count uploaded to the compute shader.")]
        private int _debugFormationBeaconCount;

        [SerializeField]
        [Tooltip("Active obstacle proxy count uploaded to the compute shader for formation deformation around rocks.")]
        private int _debugFormationObstacleCount;

        [SerializeField]
        [Tooltip("True while parasite drones are collapsed into the swarm-leviathan body path instead of free bait-ball or latch behavior.")]
        private bool _debugLeviathanModeActive;

        [SerializeField]
        [Tooltip("Active abyssal nav nodes uploaded to the compute shader for LeviathanForm.")]
        private int _debugLeviathanNodeCount;

        [SerializeField]
        [Tooltip("Latest threat-hotspot level resolved for LeviathanForm targeting.")]
        private float _debugLeviathanThreatLevel;

        [SerializeField]
        [Tooltip("Latest threat-hotspot position requested from the cartographer for LeviathanForm targeting.")]
        private Vector3 _debugLeviathanHotspotWS;

        [SerializeField]
        [Tooltip("Current fragmentation blend applied while the swarm is split into two temporary centers.")]
        private float _debugFragmentation01;

        [SerializeField]
        [Tooltip("Current active-sonar scatter strength uploaded to the compute shader.")]
        private float _debugSonarScatter01;

        private MaterialPropertyBlock _materialPropertyBlock;
        // COLD ALLOC: Plane[6] - cached frustum plane array reused for no-alloc visibility tests - owner: SargassumMicroFaunaBoids
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private BoidData[] _spawnData;
        private GrazingAnchorData[] _grazingAnchors;
        private MassiveThreatData[] _massiveThreats;
        private FormationBeaconData[] _formationBeacons;
        private FormationObstacleData[] _formationObstacles;
        private HectonBiolumZone[] _deepBiolumZones;
        private float[] _deepBiolumZoneScores;
        private BeaconNetworkSystem.BeaconSnapshot[] _formationBeaconSnapshots;
        private Collider[] _formationObstacleColliders;
        private SpatialQueryHit[] _leviathanShockwaveSpatialHits;
        private Collider[] _leviathanShockwaveColliders;
        private Rigidbody[] _leviathanShockwaveRigidbodies;
        private NativeArray<StaticObstacleData> _staticObstacleCache;
        private NativeArray<float3> _leviathanPathScratchNative;
        private NativeArray<LeviathanNodeData> _leviathanNodeFrontNative;
        private NativeArray<LeviathanNodeData> _leviathanNodeBackNative;
        private NativeArray<int> _leviathanNodeCountNative;
        private NativeArray<FoveatedSimulationInput> _foveatedSimulationInputNative;
        private NativeArray<FoveatedSimulationDecision> _foveatedSimulationFrontNative;
        private NativeArray<FoveatedSimulationDecision> _foveatedSimulationBackNative;
        private NativeArray<SimulationFrameConstants> _simulationFrameNative;
        private NativeArray<uint> _threatGridUploadNative;
        private NativeArray<uint> _threatVoxelUploadNative;
        private GraphicsBuffer _boidsBufferA;
        private GraphicsBuffer _boidsBufferB;
        private GraphicsBuffer _grazingAnchorBuffer;
        private GraphicsBuffer _massiveThreatBuffer;
        private GraphicsBuffer _formationBeaconBuffer;
        private GraphicsBuffer _formationObstacleBuffer;
        private GraphicsBuffer _leviathanNodeBuffer;
        private GraphicsBuffer _latchStatsBuffer;
        private GraphicsBuffer _pbdCorrectionBuffer;
        private GraphicsBuffer _threatGridBuffer;
        private GraphicsBuffer _threatVoxelBuffer;
        private GraphicsBuffer _spatialGridCountBuffer;
        private GraphicsBuffer _spatialGridCellBuffer;
        private GraphicsBuffer _simulationFrameBuffer;
        private Bounds _renderBounds;
        private Vector4 _densityWorldRect;
        private int _kernelIndex = -1;
        private int _clearStatsKernelIndex = -1;
        private int _clearPbdCorrectionsKernelIndex = -1;
        private int _pbdSolveKernelIndex = -1;
        private int _clearSpatialGridKernelIndex = -1;
        private int _buildSpatialGridKernelIndex = -1;
        private int _applyOriginShiftKernelIndex = -1;
        private uint _threadGroupSizeX = ComputeThreadGroupSizeX;
        private int _dispatchGroupCount = 1;
        private int _clearSpatialGridDispatchGroupCount = 1;
        private ComputeShader _boundBoidCompute;
        private int _frameParity;
        private int _lastFieldRevision = -1;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredSlowTick;
        private bool _hasSpawnData;
        private bool _computeKernelBindingsValid;
        private bool _computeDispatchDisabled;
        private Vector3 _fieldCenter;
        private Vector3 _fieldExtents;
        private Vector3 _previousDriftOffset;
        private float _headlightPanicTimer;
        private bool _deepModeActive;
        private bool _lastSpawnModeDeep;
        private bool _lastDeepLeviathanMode;
        private float _simulationTime;
        private float _simulationPhaseOffset;
        private float _spatialGridCellSizeWS = 1f;
        private Vector3 _spatialGridOriginWS = Vector3.zero;
        private Vector3Int _spatialGridResolution = Vector3Int.one;
        private int _activeBoidCount;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private int _activeGrazingAnchorCount;
        private int _activeMassiveThreatCount;
        private Rigidbody _playerRigidbody;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerHealth _playerHealth;
        private PlayerFlashlight _playerFlashlight;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private HectonMapMagicVegetationBridge _mapMagicVegetationBridge;
        private bool _flashlightOn;
        private bool _parasiteModeActive;
        private bool _formationModeActive;
        private bool _leviathanModeActive;
        private float _ecosystemFitness;
        private float _ecosystemSpeedMultiplier = 1f;
        private float _ecosystemCamouflageIndex;
        private int _reportedLatchedDroneCount;
        private Vector3 _reportedParasiteCenterOfMassLS;
        private Vector3 _reportedParasiteHarvesterPullWS;
        private int _reportedWakeFleeCount;
        private Vector3 _reportedWakeCenterWS;
        private Vector3 _reportedWakeFlowDirectionWS;
        private float _parasiteLatchReadbackTimer;
        private bool _parasiteLatchReadbackPending;
        private AsyncGPUReadbackRequest _parasiteLatchReadbackRequest;
        private float _leviathanThreatLevel;
        private Vector3 _leviathanHotspotWS;
        private int _leviathanPathNodeCount;
        private Vector3 _leviathanHeadPositionWS;
        private Vector3 _leviathanHeadForwardWS = Vector3.forward;
        private Vector3 _leviathanHeadVelocityWS;
        private float _leviathanHeadRadiusWS = 1f;
        private bool _leviathanHeadValid;
        private float _leviathanStrikeCooldownTimer;
        private float _leviathanShockwaveCooldownTimer;
        private float _leviathanModeBlend;
        private Vector3 _fragmentationCenterAWS;
        private Vector3 _fragmentationCenterBWS;
        private float _fragmentationStartTime = float.NegativeInfinity;
        private float _fragmentationExpireTime = float.NegativeInfinity;
        private Vector3 _sonarScatterOriginWS;
        private float _sonarScatterWaveFrontWS;
        private float _sonarScatterStrength01;
        private float _sonarScatterExpireTime = float.NegativeInfinity;
        private int _threatGridResolution;
        private Vector3 _threatGridCenterWS = Vector3.zero;
        private float _threatGridCellSizeWS = 1f;
        private int _threatGridCellCount;
        private bool _threatGridDataValid;
        private Vector3Int _threatVoxelDimensions = Vector3Int.zero;
        private Vector3 _threatVoxelOriginWS = Vector3.zero;
        private Vector3 _threatVoxelCellSizeWS = Vector3.one;
        private int _threatVoxelCellCount;
        private int _threatVoxelSolidThreshold = VoxelDynamicNavGridRuntime.SolidCell;
        private bool _threatVoxelDataValid;
        private int _staticObstacleCacheCount;
        private JobHandle _foveatedSimulationHandle;
        private bool _foveatedSimulationScheduled;
        private bool _sleepVelocityWritePending;
        private JobHandle _leviathanNodeBuildHandle;
        private bool _leviathanNodeBuildScheduled;
        private SimulationLodTier _lastSimulationLodTier = SimulationLodTier.Full;

        /// <summary>
        /// Current active boid count.
        /// </summary>
        public int BoidCount => boidCount;

        /// <summary>
        /// Current world-budgeted boid count dispatched and rendered this frame.
        /// </summary>
        public int ActiveBoidCount => _activeBoidCount;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            _computeDispatchDisabled = false;
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            SanitizeSettings();
            ResolveDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayload();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, ResolveCameraDistanceSq());
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            _computeDispatchDisabled = false;
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            ResolveDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayload();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, ResolveCameraDistanceSq());
            SargassumGlobalDragManager.OnMassiveDisplacement += HandleMassiveDisplacement;
            FlashlightEvents.OnToggled += HandleFlashlightToggled;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            SargassumGlobalDragManager.OnMassiveDisplacement -= HandleMassiveDisplacement;
            FlashlightEvents.OnToggled -= HandleFlashlightToggled;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            HectonFloatingOrigin.UnregisterListener(this);
            _headlightPanicTimer = 0f;
            _debugHeadlightPanic01 = 0f;
            _flashlightOn = false;
            _parasiteModeActive = false;
            _formationModeActive = false;
            _reportedLatchedDroneCount = 0;
            _debugParasiteModeActive = false;
            _debugParasiteAggression01 = 0f;
            _debugLatchedDroneCount = 0;
            _debugParasiteCenterOfMassLS = Vector3.zero;
            _debugParasiteHarvesterPullWS = Vector3.zero;
            _debugHibernation01 = 0f;
            _debugFormationModeActive = false;
            _debugFormationBeaconCount = 0;
            _debugFormationObstacleCount = 0;
            _debugLeviathanModeActive = false;
            _debugLeviathanNodeCount = 0;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = Vector3.zero;
            _debugFragmentation01 = 0f;
            _debugSonarScatter01 = 0f;
            _parasiteLatchReadbackTimer = 0f;
            _parasiteLatchReadbackPending = false;
            _reportedParasiteCenterOfMassLS = Vector3.zero;
            _reportedParasiteHarvesterPullWS = Vector3.zero;
            _reportedWakeFleeCount = 0;
            _reportedWakeCenterWS = Vector3.zero;
            _reportedWakeFlowDirectionWS = Vector3.zero;
            _leviathanModeActive = false;
            _leviathanThreatLevel = 0f;
            _leviathanHotspotWS = Vector3.zero;
            _leviathanPathNodeCount = 0;
            _leviathanHeadPositionWS = Vector3.zero;
            _leviathanHeadForwardWS = Vector3.forward;
            _leviathanHeadVelocityWS = Vector3.zero;
            _leviathanHeadRadiusWS = 1f;
            _leviathanHeadValid = false;
            _leviathanStrikeCooldownTimer = 0f;
            _leviathanShockwaveCooldownTimer = 0f;
            _leviathanModeBlend = 0f;
            _fragmentationExpireTime = float.NegativeInfinity;
            _fragmentationStartTime = float.NegativeInfinity;
            _sonarScatterExpireTime = float.NegativeInfinity;
            _sonarScatterWaveFrontWS = 0f;
            _sonarScatterStrength01 = 0f;
            ResetThreatVoxelSnapshot();
            _lastDeepLeviathanMode = false;
            TryUnregister();
            CompletePendingReadbackAndReleaseBuffers();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            SargassumGlobalDragManager.OnMassiveDisplacement -= HandleMassiveDisplacement;
            FlashlightEvents.OnToggled -= HandleFlashlightToggled;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            CompletePendingReadbackAndReleaseBuffers();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            if (shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToSwarmData(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Runs GPU flocking and issues one indirect draw call when the field is valid.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            if (!_hasSpawnData || boidMaterial == null || boidMesh == null)
                return;

            ResolveDependencies();
            if (!_threatVoxelDataValid && _mapMagicVegetationBridge != null)
                RefreshThreatVoxelPayload();
            float cameraDistanceSq = ResolveCameraDistanceSq();
            if (boidCompute == null || _computeDispatchDisabled)
            {
                RenderStaticFallback(cameraDistanceSq, hibernation01: 1f);
                return;
            }

            if (!EnsureComputeKernelBindings())
            {
                RenderStaticFallback(cameraDistanceSq, hibernation01: 1f);
                return;
            }

            _deepModeActive = IsDeepModeActive();
            _parasiteModeActive = IsParasiteModeActive();
            _leviathanModeActive = IsLeviathanModeActive();
            if (_leviathanModeActive)
                _parasiteModeActive = false;
            _formationModeActive = IsFormationModeActive();
            float deltaTime = Mathf.Max(0f, dt);
            float leviathanBlendTarget = _leviathanModeActive ? 1f : 0f;
            float leviathanBlendT = 1f - Mathf.Exp(-Mathf.Max(leviathanModeBlendSharpness, 0.01f) * deltaTime);
            _leviathanModeBlend = Mathf.Lerp(_leviathanModeBlend, leviathanBlendTarget, leviathanBlendT);
            if (_headlightPanicTimer > 0f)
            {
                _headlightPanicTimer -= deltaTime;
                if (_headlightPanicTimer < 0f)
                    _headlightPanicTimer = 0f;
            }

            Vector3 currentDriftOffset = !_deepModeActive && dragManager != null ? dragManager.GlobalDriftOffset : Vector3.zero;
            Vector3 driftDelta = currentDriftOffset - _previousDriftOffset;
            _previousDriftOffset = currentDriftOffset;
            if (driftDelta.sqrMagnitude > 0.000001f)
            {
                _fieldCenter += driftDelta;
                _renderBounds.center += driftDelta;
                _debugRenderBounds = _renderBounds;
            }

            _simulationTime += deltaTime;
            WrapSimulationPhase();
            CompletePendingLeviathanNodeBuild(forceComplete: false);
            UpdateMassiveThreats();
            UpdateParasiteLatchReadback(deltaTime);
            float hibernation01 = 0f;
            bool dispatchedSimulation = TryConsumeSimulationStep(
                deltaTime,
                cameraDistanceSq,
                out float simulationDeltaTime,
                out hibernation01,
                out SimulationLodTier simulationLodTier);
            bool shouldDispatchSleepVelocityWrite = simulationLodTier == SimulationLodTier.Sleep && _sleepVelocityWritePending;
            bool shouldRender = ShouldRenderSwarm(cameraDistanceSq);
            if (dispatchedSimulation || shouldDispatchSleepVelocityWrite)
            {
                if (simulationLodTier == SimulationLodTier.Full)
                    UpdateSpatialGridLayout();

                if (BindSimulationUniforms(simulationDeltaTime, currentDriftOffset, driftDelta, hibernation01, simulationLodTier))
                {
                    try
                    {
                        DispatchClearLatchStats();
                        if (simulationLodTier == SimulationLodTier.Full)
                        {
                            DispatchClearSpatialGrid();
                            DispatchClearPbdCorrections();
                            boidCompute.Dispatch(_buildSpatialGridKernelIndex, _dispatchGroupCount, 1, 1);
                            boidCompute.Dispatch(_pbdSolveKernelIndex, _dispatchGroupCount, 1, 1);
                        }

                        boidCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
                        if (simulationLodTier == SimulationLodTier.Sleep)
                            _sleepVelocityWritePending = false;
                        if (simulationLodTier == SimulationLodTier.Full)
                            TryRequestParasiteLatchReadback();

                        _frameParity ^= 1;
                    }
                    catch (Exception exception)
                    {
                        DisableComputeDispatch($"Compute dispatch failure on '{boidCompute.name}'. {exception.Message}");
                    }
                }
            }

            _debugVisible = shouldRender;
            _debugHibernation01 = hibernation01;
            if (shouldRender)
                RenderCurrentBuffer();

            _debugDriftOffset = currentDriftOffset;
            _debugDeepModeActive = _deepModeActive;
            _debugHeadlightPanic01 = ResolveHeadlightPanic01();
            _debugParasiteModeActive = _parasiteModeActive;
            _debugFormationModeActive = _formationModeActive;
            _debugLeviathanModeActive = _leviathanModeActive;
        }

        /// <summary>
        /// Rebuilds the spawn set whenever the sargassum field topology changes.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            RefreshThreatVoxelPayload();
            bool populationBudgetChanged = RefreshActiveBoidCount();
            RefreshSpawnData(force: populationBudgetChanged);
        }

        /// <summary>
        /// Applies fixed-step leviathan strikes and shockwave pushes using the cached head pose resolved during Tick.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed delta supplied by GameTickManager.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = Mathf.Max(0f, fixedDeltaTime);
            if (_leviathanStrikeCooldownTimer > 0f)
            {
                _leviathanStrikeCooldownTimer -= safeFixedDeltaTime;
                if (_leviathanStrikeCooldownTimer < 0f)
                    _leviathanStrikeCooldownTimer = 0f;
            }

            if (_leviathanShockwaveCooldownTimer > 0f)
            {
                _leviathanShockwaveCooldownTimer -= safeFixedDeltaTime;
                if (_leviathanShockwaveCooldownTimer < 0f)
                    _leviathanShockwaveCooldownTimer = 0f;
            }

            UpdateLeviathanPhysicalState(Mathf.Max(safeFixedDeltaTime, 0.0001f));
            ApplyParasiteHullStress();
            ApplyParasiteEnvironmentalDrag();
            if (!_leviathanModeActive || !_leviathanHeadValid || _leviathanModeBlend < 0.5f)
                return;

            ApplyLeviathanPhysicalStrike();
            ApplyLeviathanShockwave();
        }

        private void ResolveDependencies()
        {
            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (biolumManager == null)
                biolumManager = HectonBiolumManager.Instance;

            if (dragManager == null)
                dragManager = SargassumGlobalDragManager.Instance;

            if (cutManager == null)
                cutManager = SargassumCutManager.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerContext != null && playerContext.IsInitialized)
            {
                playerTransform ??= playerContext.PlayerTransform;
                _playerRigidbody ??= playerContext.PlayerRigidbody;
                _playerMovement ??= playerContext.PlayerMovement;
                _playerFlashlight ??= playerContext.Flashlight;
            }

            if (_playerRigidbody == null && playerTransform != null)
                _playerRigidbody = playerTransform.GetComponent<Rigidbody>();

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (_playerTransportCoordinator == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerHealth == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerHealth);

            if (_playerFlashlight == null && playerTransform != null)
                _playerFlashlight = playerTransform.GetComponent<PlayerFlashlight>();

            if (_worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (_mapMagicVegetationBridge == null)
                _mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (viewCamera == null && playerTransform != null)
                viewCamera = playerContext != null && playerContext.PlayerCamera != null ? playerContext.PlayerCamera : playerTransform.GetComponent<Camera>();

            if (_playerFlashlight != null)
                _flashlightOn = _playerFlashlight.IsOn;
        }

        private void SanitizeSettings()
        {
            boidCount = Mathf.Clamp(boidCount, 128, 2048);
            boidCount = VRAMEnforcer.ApplyBoidPopulationBudget(boidCount, 128, 2048);
            maxSpawnAttempts = Mathf.Clamp(maxSpawnAttempts, 4, 32);
            densityThreshold = Mathf.Clamp01(densityThreshold);
            windowThreshold = Mathf.Clamp(windowThreshold, 0f, 0.75f);
            cruiseSpeed = Mathf.Max(0.1f, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
            panicSpeedBoost = Mathf.Max(0f, panicSpeedBoost);
            perceptionRadius = Mathf.Max(0.25f, perceptionRadius);
            separationRadius = Mathf.Clamp(separationRadius, 0.1f, perceptionRadius);
            boidBodyRadius = Mathf.Clamp(boidBodyRadius, 0.02f, separationRadius * 0.5f);
            consumedCollapseSpeed = Mathf.Clamp(consumedCollapseSpeed, 2f, 24f);
            gradientWorldStep = Mathf.Max(0.05f, gradientWorldStep);
            waterLevel = Mathf.Max(0f, waterLevel);
            minDepthBelowSurface = Mathf.Max(0.1f, minDepthBelowSurface);
            maxDepthBelowSurface = Mathf.Max(minDepthBelowSurface + 0.1f, maxDepthBelowSurface);
            panicThreshold = Mathf.Clamp01(panicThreshold);
            panicDecay = Mathf.Max(0.1f, panicDecay);
            grazingAnchorCount = Mathf.Clamp(grazingAnchorCount, 4, 96);
            grazingRadius = Mathf.Clamp(grazingRadius, 0.25f, 6f);
            grazingWeight = Mathf.Clamp(grazingWeight, 0f, 4f);
            canopyAffinityWeight = Mathf.Clamp(canopyAffinityWeight, 0f, 4f);
            grazingDensityThreshold = Mathf.Clamp01(grazingDensityThreshold);
            grazingRestSpeedScale = Mathf.Clamp(grazingRestSpeedScale, 0.05f, 0.6f);
            grazingRestHoldThreshold = Mathf.Clamp01(grazingRestHoldThreshold);
            panicPlayerSpeedThreshold = Mathf.Clamp(panicPlayerSpeedThreshold, 0.5f, 8f);
            panicPlayerRadius = Mathf.Clamp(panicPlayerRadius, 0.5f, 12f);
            cameraAvoidRadius = Mathf.Clamp(cameraAvoidRadius, 0.25f, 3f);
            cameraAvoidWeight = Mathf.Clamp(cameraAvoidWeight, 0f, 8f);
            voxelAvoidanceLookAheadDistance = Mathf.Clamp(voxelAvoidanceLookAheadDistance, 0.25f, 12f);
            voxelAvoidanceWeight = Mathf.Clamp(voxelAvoidanceWeight, 0f, 16f);
            maxMassiveThreatCount = Mathf.Clamp(maxMassiveThreatCount, 1, 8);
            massiveThreatPanicRadius = Mathf.Clamp(massiveThreatPanicRadius, 50f, 96f);
            massiveThreatWeight = Mathf.Clamp(massiveThreatWeight, 0f, 12f);
            deepBiolumAnchorCapacity = Mathf.Clamp(deepBiolumAnchorCapacity, 1, 16);
            deepBiolumSearchRadius = Mathf.Clamp(deepBiolumSearchRadius, 10f, 250f);
            deepBaitBallRadius = Mathf.Clamp(deepBaitBallRadius, 0.5f, 12f);
            deepBaitBallHeight = Mathf.Clamp(deepBaitBallHeight, 0.25f, 8f);
            deepClusterWeight = Mathf.Clamp(deepClusterWeight, 0f, 8f);
            deepHeadlightPanicDuration = Mathf.Clamp(deepHeadlightPanicDuration, 0.1f, 10f);
            deepHeadlightPanicRadiusScale = Mathf.Clamp(deepHeadlightPanicRadiusScale, 1f, 6f);
            boidVatFrameCount = Mathf.Max(1, boidVatFrameCount);
            boidVatPlaybackSpeed = Mathf.Max(0f, boidVatPlaybackSpeed);
            boidVatInstancePhaseScale = Mathf.Max(0f, boidVatInstancePhaseScale);
            boidVatPositionScale = Mathf.Max(0.0001f, boidVatPositionScale);
            boidVatNormalBlend = Mathf.Clamp01(boidVatNormalBlend);
            parasiteDroneWorldYThreshold = Mathf.Clamp(parasiteDroneWorldYThreshold, -4000f, -1000f);
            parasiteAffinityWeight = Mathf.Clamp(parasiteAffinityWeight, 0f, 12f);
            parasiteHullStressIntensity = Mathf.Clamp01(parasiteHullStressIntensity);
            parasiteHullStressLightBoost = Mathf.Clamp01(parasiteHullStressLightBoost);
            parasiteLatchRadius = Mathf.Clamp(parasiteLatchRadius, 0.5f, 8f);
            parasiteMaxLatchedDronesForFullDrag = Mathf.Clamp(parasiteMaxLatchedDronesForFullDrag, 1, 96);
            parasiteMaxEnvironmentalDragMultiplier = Mathf.Clamp(parasiteMaxEnvironmentalDragMultiplier, 1f, 4f);
            parasiteLatchReadbackInterval = Mathf.Clamp(parasiteLatchReadbackInterval, 0.05f, 0.5f);
            parasiteHarvesterLatchThreshold = Mathf.Clamp(parasiteHarvesterLatchThreshold, 1, 32);
            parasiteHarvesterFullLatchCount = Mathf.Clamp(parasiteHarvesterFullLatchCount, parasiteHarvesterLatchThreshold, 96);
            formationBeaconCapacity = Mathf.Clamp(formationBeaconCapacity, 1, 8);
            formationBeaconSearchRadius = Mathf.Clamp(formationBeaconSearchRadius, 8f, 250f);
            formationWeight = Mathf.Clamp(formationWeight, 0f, 8f);
            formationRingThickness = Mathf.Clamp(formationRingThickness, 0.1f, 12f);
            formationPulseAmplitude = Mathf.Clamp(formationPulseAmplitude, 0f, 2f);
            formationPulseSpeed = Mathf.Clamp(formationPulseSpeed, 0.1f, 4f);
            formationBreakPanicThreshold = Mathf.Clamp01(formationBreakPanicThreshold);
            formationObstacleCapacity = Mathf.Clamp(formationObstacleCapacity, 1, 16);
            formationObstacleSearchRadius = Mathf.Clamp(formationObstacleSearchRadius, 4f, 80f);
            formationObstacleWeight = Mathf.Clamp(formationObstacleWeight, 0f, 8f);
            leviathanNodeCapacity = Mathf.Clamp(leviathanNodeCapacity, 8, 64);
            leviathanThreatThreshold = Mathf.Clamp01(leviathanThreatThreshold);
            leviathanHotspotMinDistance = Mathf.Clamp(leviathanHotspotMinDistance, 10f, 200f);
            leviathanHotspotMaxDistance = Mathf.Clamp(leviathanHotspotMaxDistance, leviathanHotspotMinDistance, 400f);
            leviathanBodyWeight = Mathf.Clamp(leviathanBodyWeight, 0f, 8f);
            leviathanForwardWeight = Mathf.Clamp(leviathanForwardWeight, 0f, 8f);
            leviathanBodyRadius = Mathf.Clamp(leviathanBodyRadius, 0.5f, 12f);
            leviathanWaveAmplitude = Mathf.Clamp(leviathanWaveAmplitude, 0f, 2f);
            leviathanWaveFrequency = Mathf.Clamp(leviathanWaveFrequency, 0.1f, 6f);
            leviathanSurroundThreatThreshold = Mathf.Clamp(leviathanSurroundThreatThreshold, 0.6f, 1f);
            leviathanSurroundRadius = Mathf.Clamp(leviathanSurroundRadius, 4f, 48f);
            leviathanSurroundWeight = Mathf.Clamp(leviathanSurroundWeight, 0f, 8f);
            leviathanSurroundSpinSpeed = Mathf.Clamp(leviathanSurroundSpinSpeed, 0.1f, 4f);
            leviathanModeBlendSharpness = Mathf.Clamp(leviathanModeBlendSharpness, 0.1f, 12f);
            simulationCullDistance = SleepSimulationDistanceMeters;
            hibernationStartDistance = FullSimulationDistanceMeters;
            hibernationMaxStepSeconds = Mathf.Clamp(hibernationMaxStepSeconds, 1f / 60f, 0.5f);
            hibernationMinTimeScale = Mathf.Clamp(hibernationMinTimeScale, 0.1f, 1f);
            leviathanStrikeRadius = Mathf.Clamp(leviathanStrikeRadius, 1f, 24f);
            leviathanStrikeTraumaWeight = Mathf.Clamp01(leviathanStrikeTraumaWeight);
            leviathanStrikeImpulse = Mathf.Clamp(leviathanStrikeImpulse, 1f, 120f);
            leviathanStrikeDamage = Mathf.Clamp(leviathanStrikeDamage, 0.1f, 100f);
            leviathanStrikeCooldown = Mathf.Clamp(leviathanStrikeCooldown, 0.05f, 2f);
            leviathanShockwaveSpeedThreshold = Mathf.Clamp(leviathanShockwaveSpeedThreshold, 2f, 40f);
            leviathanShockwaveRadius = Mathf.Clamp(leviathanShockwaveRadius, 2f, 32f);
            leviathanShockwaveImpulse = Mathf.Clamp(leviathanShockwaveImpulse, 2f, 96f);
            leviathanShockwaveVerticalLift = Mathf.Clamp(leviathanShockwaveVerticalLift, 0f, 2f);
            leviathanShockwaveCadence = Mathf.Clamp(leviathanShockwaveCadence, 0.05f, 1.5f);
            leviathanShockwaveHitCapacity = Mathf.Clamp(leviathanShockwaveHitCapacity, 4, 32);
            _activeBoidCount = Mathf.Clamp(_activeBoidCount <= 0 ? boidCount : _activeBoidCount, 128, boidCount);
        }

        private void EnsureBuffers()
        {
            if (_spawnData == null || _spawnData.Length != boidCount)
            {
                // COLD ALLOC: BoidData[boidCount] - CPU staging array for deterministic spawn uploads - owner: SargassumMicroFaunaBoids
                _spawnData = new BoidData[boidCount];
            }

            if (_grazingAnchors == null || _grazingAnchors.Length != grazingAnchorCount)
            {
                // COLD ALLOC: GrazingAnchorData[grazingAnchorCount] - CPU staging array for deterministic grazing anchors - owner: SargassumMicroFaunaBoids
                _grazingAnchors = new GrazingAnchorData[grazingAnchorCount];
            }

            if (_massiveThreats == null || _massiveThreats.Length != maxMassiveThreatCount)
            {
                // COLD ALLOC: MassiveThreatData[maxMassiveThreatCount] - CPU staging array for leviathan panic threats - owner: SargassumMicroFaunaBoids
                _massiveThreats = new MassiveThreatData[maxMassiveThreatCount];
                _activeMassiveThreatCount = 0;
                _debugMassiveThreatCount = 0;
            }

            if (_deepBiolumZones == null || _deepBiolumZones.Length != deepBiolumAnchorCapacity)
            {
                // COLD ALLOC: HectonBiolumZone[deepBiolumAnchorCapacity] - deep-sea biolum anchor cache for bait-ball rebuilds - owner: SargassumMicroFaunaBoids
                _deepBiolumZones = new HectonBiolumZone[deepBiolumAnchorCapacity];
            }

            if (_deepBiolumZoneScores == null || _deepBiolumZoneScores.Length != deepBiolumAnchorCapacity)
            {
                // COLD ALLOC: float[deepBiolumAnchorCapacity] - deep-sea biolum anchor strength cache paired with zone refs - owner: SargassumMicroFaunaBoids
                _deepBiolumZoneScores = new float[deepBiolumAnchorCapacity];
            }

            if (_formationBeacons == null || _formationBeacons.Length != formationBeaconCapacity)
            {
                // COLD ALLOC: FormationBeaconData[formationBeaconCapacity] - GPU formation anchor staging for abyss beacon rings - owner: SargassumMicroFaunaBoids
                _formationBeacons = new FormationBeaconData[formationBeaconCapacity];
            }

            if (_formationObstacles == null || _formationObstacles.Length != formationObstacleCapacity)
            {
                // COLD ALLOC: FormationObstacleData[formationObstacleCapacity] - GPU rock obstacle proxy staging for formation deformation - owner: SargassumMicroFaunaBoids
                _formationObstacles = new FormationObstacleData[formationObstacleCapacity];
            }

            if (_formationBeaconSnapshots == null || _formationBeaconSnapshots.Length != 24)
            {
                // COLD ALLOC: BeaconSnapshot[24] - nearby abyss beacon copy buffer for hive-mind formation - owner: SargassumMicroFaunaBoids
                _formationBeaconSnapshots = new BeaconNetworkSystem.BeaconSnapshot[24];
            }

            if (_formationObstacleColliders == null || _formationObstacleColliders.Length != formationObstacleCapacity * 2)
            {
                // COLD ALLOC: Collider[32] - non-alloc overlap buffer for nearby formation obstacle harvesting - owner: SargassumMicroFaunaBoids
                _formationObstacleColliders = new Collider[Mathf.Max(2, formationObstacleCapacity * 2)];
            }

            if (_leviathanShockwaveSpatialHits == null || _leviathanShockwaveSpatialHits.Length != leviathanShockwaveHitCapacity)
            {
                // COLD ALLOC: SpatialQueryHit[leviathanShockwaveHitCapacity] - vegetation spatial-hash hit cache for leviathan shockwave debris pushes - owner: SargassumMicroFaunaBoids
                _leviathanShockwaveSpatialHits = new SpatialQueryHit[leviathanShockwaveHitCapacity];
            }

            if (_leviathanShockwaveColliders == null || _leviathanShockwaveColliders.Length != leviathanShockwaveHitCapacity)
            {
                // COLD ALLOC: Collider[leviathanShockwaveHitCapacity] - fallback overlap buffer for leviathan shockwave rigidbody pushes - owner: SargassumMicroFaunaBoids
                _leviathanShockwaveColliders = new Collider[leviathanShockwaveHitCapacity];
            }

            if (_leviathanShockwaveRigidbodies == null || _leviathanShockwaveRigidbodies.Length != leviathanShockwaveHitCapacity)
            {
                // COLD ALLOC: Rigidbody[leviathanShockwaveHitCapacity] - deduplicated rigidbody targets processed by leviathan shockwaves - owner: SargassumMicroFaunaBoids
                _leviathanShockwaveRigidbodies = new Rigidbody[leviathanShockwaveHitCapacity];
            }

            EnsureBuffer(ref _boidsBufferA, boidCount, BoidStride);
            EnsureBuffer(ref _boidsBufferB, boidCount, BoidStride);
            EnsureBuffer(ref _grazingAnchorBuffer, grazingAnchorCount, GrazingAnchorStride);
            EnsureBuffer(ref _massiveThreatBuffer, maxMassiveThreatCount, MassiveThreatStride);
            EnsureBuffer(ref _formationBeaconBuffer, formationBeaconCapacity, FormationBeaconStride);
            EnsureBuffer(ref _formationObstacleBuffer, formationObstacleCapacity, FormationObstacleStride);
            EnsureBuffer(ref _leviathanNodeBuffer, leviathanNodeCapacity, LeviathanNodeStride);
            EnsureBuffer(ref _latchStatsBuffer, LatchStatsElementCount, LatchStatsStride);
            EnsureBuffer(ref _pbdCorrectionBuffer, boidCount, PbdCorrectionStride);
            EnsureBuffer(ref _threatGridBuffer, Mathf.Max(1, _threatGridCellCount), ThreatGridStride);
            EnsureBuffer(ref _threatVoxelBuffer, Mathf.Max(1, _threatVoxelCellCount), ThreatVoxelStride);
            EnsureBuffer(ref _spatialGridCountBuffer, SpatialGridMaxCellCount, SpatialGridCountStride);
            EnsureBuffer(ref _spatialGridCellBuffer, SpatialGridMaxCellCount * SpatialGridMaxBoidsPerCell, SpatialGridCellEntryStride);
            EnsureBuffer(ref _simulationFrameBuffer, 1, SimulationFrameConstantsStride);
            EnsureNativeArrayCapacity(ref _staticObstacleCache, Mathf.Max(formationObstacleCapacity * 8, formationObstacleCapacity));
            EnsureNativeArrayCapacity(ref _leviathanNodeFrontNative, leviathanNodeCapacity);
            EnsureNativeArrayCapacity(ref _leviathanNodeBackNative, leviathanNodeCapacity);
            EnsureNativeArrayCapacity(ref _leviathanNodeCountNative, 1);
            EnsureNativeArrayCapacity(ref _foveatedSimulationInputNative, 1);
            EnsureNativeArrayCapacity(ref _foveatedSimulationFrontNative, 1);
            EnsureNativeArrayCapacity(ref _foveatedSimulationBackNative, 1);
            EnsureNativeArrayCapacity(ref _simulationFrameNative, 1);

            if (!ValidateGpuStructLayouts())
                return;

            if (boidCompute == null)
            {
                ResetComputeKernelBindings();
                return;
            }

            EnsureComputeKernelBindings();
        }

        private bool RefreshActiveBoidCount()
        {
            int previousActiveBoidCount = _activeBoidCount;
            if (TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount))
            {
                float populationBudgetScale = ResolvePopulationBudgetScale();
                int budgetCap = Mathf.Clamp(Mathf.RoundToInt(boidCount * populationBudgetScale), 0, boidCount);
                _activeBoidCount = Mathf.Clamp(Mathf.Min(ecosystemPopulationCount, budgetCap), 0, boidCount);
                _debugPopulationBudgetScale = boidCount > 0 ? (_activeBoidCount / (float)boidCount) : 0f;
            }
            else
            {
                float populationBudgetScale = ResolvePopulationBudgetScale();
                _activeBoidCount = Mathf.Clamp(Mathf.RoundToInt(boidCount * populationBudgetScale), 128, boidCount);
                _debugPopulationBudgetScale = populationBudgetScale;
            }

            _debugActiveBoidCount = _activeBoidCount;
            RefreshDispatchGroupCount();
            return previousActiveBoidCount != _activeBoidCount;
        }

        private bool TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount)
        {
            ecosystemPopulationCount = 0;
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized || playerTransform == null)
            {
                _ecosystemFitness = 0f;
                _ecosystemSpeedMultiplier = 1f;
                _ecosystemCamouflageIndex = 0f;
                return false;
            }

            if (!ecosystemDirector.TryGetSectorPopulation(playerTransform.position, out EcosystemSectorPopulationSample sample))
            {
                _ecosystemFitness = 0f;
                _ecosystemSpeedMultiplier = 1f;
                _ecosystemCamouflageIndex = 0f;
                return false;
            }

            ecosystemPopulationCount = Mathf.Max(0, sample.PreyPopulation);
            _ecosystemFitness = Mathf.Clamp01(sample.Fitness);
            _ecosystemSpeedMultiplier = Mathf.Max(0.25f, sample.SpeedMultiplier);
            _ecosystemCamouflageIndex = Mathf.Clamp01(sample.CamouflageIndex);
            _debugEcosystemFitness = _ecosystemFitness;
            return true;
        }

        private float ResolvePopulationBudgetScale()
        {
            WorldProceduralScatterDirector scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (scatterDirector == null)
                return 1f;

            float spawnBudgetScale = scatterDirector.CurrentSpawnBudgetScale;
            float faunaActivationScale = scatterDirector.CurrentFaunaActivationScale;
            return Mathf.Clamp(spawnBudgetScale * faunaActivationScale, MinimumPopulationBudgetScale, 1f);
        }

        private void RefreshDispatchGroupCount()
        {
            _dispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(_activeBoidCount / Mathf.Max(1f, _threadGroupSizeX)));
            _clearSpatialGridDispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(SpatialGridMaxCellCount / (float)SpatialGridClearThreadGroupSize));
            _debugDispatchGroups = _dispatchGroupCount;
        }

        private void ResetThreatVoxelSnapshot()
        {
            _threatVoxelCellCount = 0;
            _threatVoxelDimensions = Vector3Int.zero;
            _threatVoxelOriginWS = Vector3.zero;
            _threatVoxelCellSizeWS = Vector3.one;
            _threatVoxelSolidThreshold = VoxelDynamicNavGridRuntime.SolidCell;
            _threatVoxelDataValid = false;
        }

        private void ResetThreatGridSnapshot()
        {
            _threatGridCellCount = 0;
            _threatGridResolution = 0;
            _threatGridCenterWS = Vector3.zero;
            _threatGridCellSizeWS = 1f;
            _threatGridDataValid = false;
        }

        private void RefreshThreatVoxelPayload()
        {
            RefreshThreatGridPayload();

            NativeArray<byte> threatVoxels = default;
            Vector3Int gridDimensions = Vector3Int.zero;
            Vector3 gridOrigin = Vector3.zero;
            Vector3 voxelCellSize = Vector3.one;
            int threatVoxelSolidThreshold = VoxelDynamicNavGridRuntime.SolidCell;
            HectonCaveVoxelLightingVolume caveVoxelVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveVoxelVolume != null &&
                caveVoxelVolume.TryGetPublishedSignedDistanceVoxelPayload(
                    out NativeArray<byte> signedDistanceVoxels,
                    out Vector3Int caveGridDimensions,
                    out Vector3 caveGridOrigin,
                    out Vector3 caveVoxelCellSize))
            {
                threatVoxels = signedDistanceVoxels;
                gridDimensions = caveGridDimensions;
                gridOrigin = caveGridOrigin;
                voxelCellSize = caveVoxelCellSize;
                threatVoxelSolidThreshold = 128;
            }

            bool resolvedPassability = VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                new float3(_fieldCenter.x, _fieldCenter.y, _fieldCenter.z),
                out NativeArray<byte> navPassability,
                out int3 navDimensions,
                out float3 navOrigin,
                out float navCellSize);
            if (!threatVoxels.IsCreated && resolvedPassability)
            {
                threatVoxels = navPassability;
                gridDimensions = new Vector3Int(navDimensions.x, navDimensions.y, navDimensions.z);
                gridOrigin = new Vector3(navOrigin.x, navOrigin.y, navOrigin.z);
                voxelCellSize = new Vector3(navCellSize, navCellSize, navCellSize);
                threatVoxelSolidThreshold = VoxelDynamicNavGridRuntime.SolidCell;
            }
            else if (!threatVoxels.IsCreated &&
                     _mapMagicVegetationBridge != null &&
                     _mapMagicVegetationBridge.TryGetEcosystemThreatVoxelPayload(
                         out NativeArray<byte> fallbackThreatVoxels,
                         out Vector3Int fallbackGridDimensions,
                         out Vector3 fallbackGridOrigin,
                         out Vector3 fallbackVoxelCellSize))
            {
                threatVoxels = fallbackThreatVoxels;
                gridDimensions = fallbackGridDimensions;
                gridOrigin = fallbackGridOrigin;
                voxelCellSize = fallbackVoxelCellSize;
                threatVoxelSolidThreshold = VoxelDynamicNavGridRuntime.SolidCell;
            }
            else
            {
                ResetThreatVoxelSnapshot();
                return;
            }

            long cellCountLong = (long)gridDimensions.x * gridDimensions.y * gridDimensions.z;
            if (!threatVoxels.IsCreated ||
                gridDimensions.x <= 0 ||
                gridDimensions.y <= 0 ||
                gridDimensions.z <= 0 ||
                cellCountLong <= 0L ||
                cellCountLong > int.MaxValue ||
                threatVoxels.Length < cellCountLong)
            {
                ResetThreatVoxelSnapshot();
                return;
            }

            int cellCount = (int)cellCountLong;
            EnsureBuffer(ref _threatVoxelBuffer, cellCount, ThreatVoxelStride);
            EnsureNativeArrayCapacity(ref _threatVoxelUploadNative, cellCount);

            // Expand the byte-compressed voxel payload into uint lanes to match the structured buffer contract.
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                _threatVoxelUploadNative[cellIndex] = threatVoxels[cellIndex];

            GraphicsBufferUploadUtility.UploadNativeArray(_threatVoxelBuffer, _threatVoxelUploadNative, cellCount);
            _threatVoxelCellCount = cellCount;
            _threatVoxelDimensions = gridDimensions;
            _threatVoxelOriginWS = gridOrigin;
            _threatVoxelCellSizeWS = new Vector3(
                Mathf.Max(voxelCellSize.x, ThreatVoxelCellEpsilon),
                Mathf.Max(voxelCellSize.y, ThreatVoxelCellEpsilon),
                Mathf.Max(voxelCellSize.z, ThreatVoxelCellEpsilon));
            _threatVoxelSolidThreshold = Mathf.Clamp(threatVoxelSolidThreshold, 1, 255);
            _threatVoxelDataValid = true;
        }

        private void RefreshThreatGridPayload()
        {
            if (_mapMagicVegetationBridge == null ||
                !_mapMagicVegetationBridge.TryGetCompressedEcosystemThreatGridPayload(
                    out NativeArray<byte> threatGrid,
                    out int gridResolution,
                    out Vector3 gridCenter,
                    out float cellSize))
            {
                ResetThreatGridSnapshot();
                return;
            }

            long cellCountLong = (long)gridResolution * gridResolution;
            if (!threatGrid.IsCreated ||
                gridResolution <= 0 ||
                cellCountLong <= 0L ||
                cellCountLong > int.MaxValue ||
                threatGrid.Length < cellCountLong)
            {
                ResetThreatGridSnapshot();
                return;
            }

            int cellCount = (int)cellCountLong;
            EnsureBuffer(ref _threatGridBuffer, cellCount, ThreatGridStride);
            EnsureNativeArrayCapacity(ref _threatGridUploadNative, cellCount);

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                _threatGridUploadNative[cellIndex] = threatGrid[cellIndex];

            GraphicsBufferUploadUtility.UploadNativeArray(_threatGridBuffer, _threatGridUploadNative, cellCount);
            _threatGridCellCount = cellCount;
            _threatGridResolution = gridResolution;
            _threatGridCenterWS = gridCenter;
            _threatGridCellSizeWS = Mathf.Max(cellSize, ThreatVoxelCellEpsilon);
            _threatGridDataValid = true;
        }

        private static void EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return;

            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }

            // COLD ALLOC: GraphicsBuffer[count] - persistent GPU boid simulation/storage buffer - owner: SargassumMicroFaunaBoids
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
        }

        private void RefreshSpawnData(bool force)
        {
            if (boidCompute == null || boidMaterial == null || boidMesh == null || !EnsureComputeKernelBindings())
            {
                _hasSpawnData = false;
                return;
            }

            force |= RefreshActiveBoidCount();

            _deepModeActive = IsDeepModeActive();
            _debugDeepModeActive = _deepModeActive;

            if (_deepModeActive)
            {
                BuildLeviathanData();
                bool leviathanSpawnMode = _leviathanPathNodeCount > 1 && _leviathanThreatLevel >= leviathanThreatThreshold;
                if (!force && _lastSpawnModeDeep && _hasSpawnData)
                {
                    if (leviathanSpawnMode)
                    {
                        HarvestFormationObstacles(_fieldCenter);
                    }
                    else
                    {
                        BuildFormationData();
                    }

                    if (_formationBeacons != null)
                        GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);
                    if (_formationObstacles != null)
                        GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
                    UploadActiveLeviathanSnapshot();
                    _hasSpawnData = true;
                    _lastDeepLeviathanMode = leviathanSpawnMode;
                    return;
                }

                if (!BuildDeepSpawnData())
                {
                    _hasSpawnData = false;
                    return;
                }

                GraphicsBufferUploadUtility.UploadArray(_boidsBufferA, _spawnData, boidCount);
                GraphicsBufferUploadUtility.UploadArray(_boidsBufferB, _spawnData, boidCount);
                GraphicsBufferUploadUtility.UploadArray(_grazingAnchorBuffer, _grazingAnchors, _activeGrazingAnchorCount);
                if (_formationBeacons != null)
                    GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);
                if (_formationObstacles != null)
                    GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
                UploadActiveLeviathanSnapshot();
                _frameParity = 0;
                _previousDriftOffset = Vector3.zero;
                _lastFieldRevision = -1;
                _debugFieldRevision = -1;
                _hasSpawnData = true;
                _lastSpawnModeDeep = true;
                _lastDeepLeviathanMode = leviathanSpawnMode;
                return;
            }

            _lastSpawnModeDeep = false;
            _lastDeepLeviathanMode = false;
            _debugFormationBeaconCount = 0;
            _debugFormationObstacleCount = 0;
            _debugLeviathanNodeCount = 0;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = Vector3.zero;
            if (dragManager == null || !dragManager.TryGetDensityFieldTexture(out _, out Vector4 densityWorldRect))
            {
                _hasSpawnData = false;
                return;
            }

            if (!force && dragManager.FieldRevision == _lastFieldRevision)
                return;

            _densityWorldRect = densityWorldRect;
            BuildSpawnSet(densityWorldRect, dragManager.GlobalDriftOffset);
            GraphicsBufferUploadUtility.UploadArray(_boidsBufferA, _spawnData, boidCount);
            GraphicsBufferUploadUtility.UploadArray(_boidsBufferB, _spawnData, boidCount);
            BuildGrazingAnchors(densityWorldRect, dragManager.GlobalDriftOffset);
            GraphicsBufferUploadUtility.UploadArray(_grazingAnchorBuffer, _grazingAnchors, _activeGrazingAnchorCount);
            if (_formationBeacons != null)
                GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);
            if (_formationObstacles != null)
                GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
            UploadActiveLeviathanSnapshot();
            _frameParity = 0;
            _previousDriftOffset = dragManager.GlobalDriftOffset;
            _lastFieldRevision = dragManager.FieldRevision;
            _debugFieldRevision = _lastFieldRevision;
            _hasSpawnData = true;
        }

        private bool BuildDeepSpawnData()
        {
            if (biolumManager == null || playerTransform == null || _deepBiolumZones == null || _deepBiolumZoneScores == null)
                return false;

            System.Array.Clear(_deepBiolumZones, 0, _deepBiolumZones.Length);
            System.Array.Clear(_deepBiolumZoneScores, 0, _deepBiolumZoneScores.Length);
            int zoneCount = biolumManager.CopyNearbyZonesNonAlloc(
                playerTransform.position,
                deepBiolumSearchRadius,
                _deepBiolumZones,
                _deepBiolumZoneScores);
            if (zoneCount <= 0)
                return false;

            _densityWorldRect = Vector4.zero;
            BuildLeviathanData();
            if (_leviathanPathNodeCount > 1 && _leviathanThreatLevel >= leviathanThreatThreshold)
            {
                BuildLeviathanSpawnSet();
                BuildDeepGrazingAnchors(zoneCount);
                HarvestFormationObstacles(_fieldCenter);
            }
            else
            {
                BuildDeepSpawnSet(zoneCount);
                BuildDeepGrazingAnchors(zoneCount);
                BuildFormationData();
            }

            return true;
        }

        private bool IsDeepModeActive()
        {
            return playerTransform != null && playerTransform.position.y <= deepSeaWorldYThreshold;
        }

        private bool IsParasiteModeActive()
        {
            if (playerTransform == null || playerTransform.position.y > parasiteDroneWorldYThreshold)
                return false;

            if (_worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (_worldZoneDirector == null || _biomeMatrixDirector == null || _biomeMatrixDirector.CurrentDepthMeters < 2000f)
                return false;

            WorldZoneAnchor primaryZone = _worldZoneDirector.CurrentZone;
            WorldZoneAnchor secondaryZone = _worldZoneDirector.SecondaryZone;
            return IsSyntheticAbyssZone(primaryZone) || IsSyntheticAbyssZone(secondaryZone);
        }

        private bool IsLeviathanModeActive()
        {
            return _deepModeActive &&
                   _leviathanPathNodeCount > 1 &&
                   _leviathanThreatLevel >= leviathanThreatThreshold;
        }

        private static bool IsSyntheticAbyssZone(WorldZoneAnchor zone)
        {
            if (zone == null)
                return false;

            return zone.Kind == WorldZoneAnchor.ZoneKind.Service ||
                   zone.Kind == WorldZoneAnchor.ZoneKind.Power ||
                   zone.Kind == WorldZoneAnchor.ZoneKind.Construction;
        }

        private float ResolveParasiteAggression01()
        {
            if (!_parasiteModeActive || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return 0f;

            return Mathf.Clamp01(Mathf.Max(_flashlightOn ? 1f : 0f, ResolveHeadlightPanic01()));
        }

        private bool IsFormationModeActive()
        {
            return _deepModeActive && !_parasiteModeActive && !_leviathanModeActive && _debugFormationBeaconCount > 0;
        }

        private void BuildFormationData()
        {
            _debugFormationBeaconCount = 0;
            _debugFormationObstacleCount = 0;
            if (_formationBeacons == null || _formationObstacles == null)
                return;

            for (int i = 0; i < _formationBeacons.Length; i++)
                _formationBeacons[i] = default;

            for (int i = 0; i < _formationObstacles.Length; i++)
                _formationObstacles[i] = default;

            if (!_deepModeActive || playerTransform == null)
                return;

            BeaconNetworkSystem beaconNetwork = BeaconNetworkSystem.Instance;
            if (beaconNetwork == null || _formationBeaconSnapshots == null)
                return;

            int snapshotCount = beaconNetwork.CopySnapshots(_formationBeaconSnapshots);
            if (snapshotCount <= 0)
                return;

            Vector3 origin = playerTransform.position;
            int formationCount = 0;
            for (int i = 0; i < snapshotCount && formationCount < _formationBeacons.Length; i++)
            {
                BeaconNetworkSystem.BeaconSnapshot snapshot = _formationBeaconSnapshots[i];
                Vector3 beaconPosition = snapshot.Position;
                if ((beaconPosition - origin).sqrMagnitude > formationBeaconSearchRadius * formationBeaconSearchRadius)
                    continue;

                float beaconRadius = Mathf.Clamp(snapshot.LightRange * 2.2f, 4f, formationBeaconSearchRadius * 0.35f);
                _formationBeacons[formationCount] = new FormationBeaconData
                {
                    Position = beaconPosition,
                    Radius = beaconRadius,
                    Strength = 1f,
                    Phase = HashToFloat01((uint)i, 0u, 0x55A1F13Du),
                    Padding = Vector2.zero
                };
                formationCount++;
            }

            _debugFormationBeaconCount = formationCount;
            if (_formationBeaconBuffer != null)
                GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);

            RefreshStaticObstacleCache();
            HarvestFormationObstacles(origin);
        }

        private void RefreshStaticObstacleCache()
        {
            _staticObstacleCacheCount = 0;
            if (_mapMagicVegetationBridge == null || !_staticObstacleCache.IsCreated)
                return;

            if (!_mapMagicVegetationBridge.TryGetActiveUnderwaterNativePayload(
                    out NativeArray<Matrix4x4> matrices,
                    out NativeArray<HectonVegetationInstanceData> metadata,
                    out _,
                    out int count) ||
                !_mapMagicVegetationBridge.TryGetActiveUnderwaterSemanticPayload(out NativeArray<int> semanticTypes, out _, out _))
            {
                return;
            }

            int safeCount = Mathf.Min(count, Mathf.Min(matrices.Length, Mathf.Min(metadata.Length, semanticTypes.Length)));
            for (int i = 0; i < safeCount && _staticObstacleCacheCount < _staticObstacleCache.Length; i++)
            {
                HectonMapMagicVegetationBridge.VegetationSemanticType semanticType =
                    (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticTypes[i];
                if (!IsStaticFormationObstacleSemantic(semanticType))
                    continue;

                Matrix4x4 matrix = matrices[i];
                Vector3 axisX = matrix.GetColumn(0);
                Vector3 axisY = matrix.GetColumn(1);
                Vector3 axisZ = matrix.GetColumn(2);
                Vector3 extents = new Vector3(axisX.magnitude, axisY.magnitude, axisZ.magnitude);
                float radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
                if (radius <= 0.1f)
                    continue;

                _staticObstacleCache[_staticObstacleCacheCount] = new StaticObstacleData(
                    new float3(matrix.m03, matrix.m13, matrix.m23),
                    new float3(extents.x, extents.y, extents.z),
                    radius);
                _staticObstacleCacheCount++;
            }
        }

        private void HarvestFormationObstacles(Vector3 origin)
        {
            if (_formationObstacles == null || !_staticObstacleCache.IsCreated)
                return;

            int obstacleCount = 0;
            float3 origin3 = origin;
            for (int i = 0; i < _staticObstacleCacheCount && obstacleCount < _formationObstacles.Length; i++)
            {
                StaticObstacleData obstacle = _staticObstacleCache[i];
                float radius = Mathf.Max(0.1f, obstacle.Radius);
                float maxDistance = formationObstacleSearchRadius + radius;
                if (math.lengthsq(obstacle.Center - origin3) > maxDistance * maxDistance)
                    continue;

                _formationObstacles[obstacleCount] = new FormationObstacleData
                {
                    Position = new Vector3(obstacle.Center.x, obstacle.Center.y, obstacle.Center.z),
                    Radius = radius,
                    Weight = 1f,
                    Padding = Vector3.zero
                };
                obstacleCount++;
            }

            _debugFormationObstacleCount = obstacleCount;
            if (_formationObstacleBuffer != null)
                GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
        }

        private void BuildLeviathanData()
        {
            _leviathanThreatLevel = 0f;
            _leviathanHotspotWS = playerTransform != null ? playerTransform.position : Vector3.zero;
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = _leviathanHotspotWS;
            if (!_leviathanNodeFrontNative.IsCreated || !_leviathanNodeBackNative.IsCreated || _mapMagicVegetationBridge == null || playerTransform == null)
            {
                ClearLeviathanSnapshot();
                return;
            }

            if (!_mapMagicVegetationBridge.TryGetThreatHotspot(
                    leviathanThreatThreshold,
                    leviathanHotspotMinDistance,
                    leviathanHotspotMaxDistance,
                    out Vector3 hotspotPosition,
                    out float hotspotThreat))
            {
                ClearLeviathanSnapshot();
                return;
            }

            _leviathanHotspotWS = hotspotPosition;
            _leviathanThreatLevel = hotspotThreat;
            _debugLeviathanThreatLevel = hotspotThreat;
            _debugLeviathanHotspotWS = hotspotPosition;

            if (_mapMagicVegetationBridge.TryGetLatestAbyssalPathPayload(out NativeArray<Vector3> path, out int pathCount) &&
                pathCount > 1)
            {
                ScheduleLeviathanNodeBuild(path, pathCount);
            }

            _mapMagicVegetationBridge.TryScheduleAbyssalPath(playerTransform.position, hotspotPosition, out _);
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
        }

        private void ScheduleLeviathanNodeBuild(NativeArray<Vector3> path, int pathCount)
        {
            int safePathCount = Mathf.Min(pathCount, path.Length);
            if (safePathCount < 2 || !_leviathanNodeBackNative.IsCreated || _leviathanNodeBackNative.Length <= 0)
                return;

            CompletePendingLeviathanNodeBuild(forceComplete: false);
            if (_leviathanNodeBuildScheduled)
                return;

            EnsureNativeArrayCapacity(ref _leviathanPathScratchNative, safePathCount);
            for (int i = 0; i < safePathCount; i++)
                _leviathanPathScratchNative[i] = path[i];

            var job = new BuildLeviathanNodeJob
            {
                SourcePath = _leviathanPathScratchNative,
                SourceCount = safePathCount,
                OutputNodes = _leviathanNodeBackNative,
                OutputCount = _leviathanNodeCountNative,
                BodyRadius = Mathf.Max(0.5f, leviathanBodyRadius)
            };

            _leviathanNodeBuildHandle = job.Schedule();
            _leviathanNodeBuildScheduled = true;
        }

        private bool TrySampleLeviathanPath(float distance01, out Vector3 positionWS, out Vector3 tangentWS, out float radiusWS)
        {
            positionWS = _fieldCenter;
            tangentWS = Vector3.forward;
            radiusWS = Mathf.Max(0.5f, leviathanBodyRadius);
            if (!_leviathanNodeFrontNative.IsCreated || _leviathanPathNodeCount < 2)
                return false;

            int safeCount = Mathf.Min(_leviathanPathNodeCount, _leviathanNodeFrontNative.Length);
            LeviathanNodeData previousNode = _leviathanNodeFrontNative[0];
            for (int i = 1; i < safeCount; i++)
            {
                LeviathanNodeData currentNode = _leviathanNodeFrontNative[i];
                if (distance01 > currentNode.Distance01 && i < safeCount - 1)
                {
                    previousNode = currentNode;
                    continue;
                }

                float segmentLength01 = Mathf.Max(0.0001f, currentNode.Distance01 - previousNode.Distance01);
                float segmentT = Mathf.Clamp01((distance01 - previousNode.Distance01) / segmentLength01);
                positionWS = Vector3.Lerp(ToVector3(previousNode.Position), ToVector3(currentNode.Position), segmentT);
                tangentWS = Vector3.Slerp(ToVector3(previousNode.Tangent), ToVector3(currentNode.Tangent), segmentT).normalized;
                radiusWS = Mathf.Lerp(previousNode.Radius, currentNode.Radius, segmentT);
                return true;
            }

            LeviathanNodeData tailNode = _leviathanNodeFrontNative[safeCount - 1];
            positionWS = ToVector3(tailNode.Position);
            Vector3 tailTangent = ToVector3(tailNode.Tangent);
            tangentWS = tailTangent.sqrMagnitude > 0.0001f ? tailTangent : Vector3.forward;
            radiusWS = tailNode.Radius;
            return true;
        }

        private void UpdateLeviathanPhysicalState(float dt)
        {
            if (!_leviathanModeActive ||
                !TryResolveLeviathanHeadPose(out Vector3 headPositionWS, out Vector3 headForwardWS, out float headRadiusWS) ||
                !TryResolveLeviathanCourseVelocity(dt, out Vector3 courseVelocityWS, out Vector3 courseForwardWS))
            {
                _leviathanHeadValid = false;
                _leviathanHeadVelocityWS = Vector3.zero;
                _leviathanHeadRadiusWS = Mathf.Max(0.5f, leviathanBodyRadius);
                return;
            }

            _leviathanHeadPositionWS = headPositionWS;
            _leviathanHeadForwardWS = courseForwardWS.sqrMagnitude > 0.0001f ? courseForwardWS : headForwardWS;
            _leviathanHeadRadiusWS = headRadiusWS;
            _leviathanHeadVelocityWS = courseVelocityWS;
            _leviathanHeadValid = true;
        }

        private bool TryResolveLeviathanHeadPose(out Vector3 headPositionWS, out Vector3 headForwardWS, out float headRadiusWS)
        {
            headPositionWS = _fieldCenter;
            headForwardWS = Vector3.forward;
            headRadiusWS = Mathf.Max(0.5f, leviathanBodyRadius);
            if (!TrySampleLeviathanPath(0f, out Vector3 splinePosition, out Vector3 splineTangent, out float bodyRadius))
                return false;

            Vector3 safeTangent = splineTangent.sqrMagnitude > 0.0001f ? splineTangent.normalized : Vector3.forward;
            Vector3 lateral = Vector3.Cross(Vector3.up, safeTangent);
            if (lateral.sqrMagnitude <= 0.0001f)
                lateral = Vector3.Cross(Vector3.right, safeTangent);
            if (lateral.sqrMagnitude <= 0.0001f)
                lateral = Vector3.forward;
            lateral.Normalize();

            Vector3 vertical = Vector3.Cross(safeTangent, lateral);
            if (vertical.sqrMagnitude <= 0.0001f)
                vertical = Vector3.up;
            else
                vertical.Normalize();

            float simulationPhaseTime = GetAbsoluteSimulationTime();
            float surroundAttack = Mathf.Clamp01((_leviathanThreatLevel - leviathanSurroundThreatThreshold) / Mathf.Max(1f - leviathanSurroundThreatThreshold, 0.001f));
            float wavePhase = simulationPhaseTime * leviathanWaveFrequency;
            float lateralWave = Mathf.Sin(wavePhase) * (bodyRadius * leviathanWaveAmplitude);
            float verticalWaveOffset = Mathf.Cos(wavePhase * 0.63f) * (bodyRadius * leviathanWaveAmplitude * 0.35f);
            Vector3 leviathanTarget = splinePosition + lateral * lateralWave + vertical * verticalWaveOffset;

            Vector3 ringTarget = leviathanTarget;
            if (playerTransform != null && surroundAttack > 0f)
            {
                float ringRadius = Mathf.Max(leviathanSurroundRadius, bodyRadius * 2.4f);
                float ringPulse = Mathf.Sin(simulationPhaseTime * (leviathanWaveFrequency * 0.7f));
                float ringAngle = simulationPhaseTime * leviathanSurroundSpinSpeed;
                Vector3 ringOffset = new Vector3(
                    Mathf.Cos(ringAngle),
                    ringPulse * (bodyRadius * 0.18f),
                    Mathf.Sin(ringAngle)) * (ringRadius + ringPulse * bodyRadius * 0.22f);
                ringTarget = playerTransform.position + ringOffset;
            }

            headPositionWS = Vector3.Lerp(leviathanTarget, ringTarget, surroundAttack) + safeTangent * Mathf.Max(bodyRadius * 0.55f, 0.6f);
            headForwardWS = safeTangent;
            headRadiusWS = bodyRadius;
            return true;
        }

        private bool TryResolveLeviathanCourseVelocity(float dt, out Vector3 courseVelocityWS, out Vector3 courseForwardWS)
        {
            courseVelocityWS = Vector3.zero;
            courseForwardWS = Vector3.forward;
            if (!TrySampleLeviathanPath(0f, out Vector3 currentSplinePoint, out Vector3 currentSplineTangent, out _) || _leviathanPathNodeCount < 2)
                return false;

            float sampleStep = 1f / Mathf.Max(1, _leviathanPathNodeCount - 1);
            float nextDistance01 = Mathf.Clamp01(sampleStep);
            if (!TrySampleLeviathanPath(nextDistance01, out Vector3 nextSplinePoint, out Vector3 nextSplineTangent, out _))
                nextSplinePoint = currentSplinePoint + currentSplineTangent;

            Vector3 splineDelta = nextSplinePoint - currentSplinePoint;
            if (splineDelta.sqrMagnitude <= 0.000001f)
                splineDelta = currentSplineTangent.sqrMagnitude > 0.0001f ? currentSplineTangent.normalized : Vector3.forward;

            courseForwardWS = splineDelta.sqrMagnitude > 0.000001f ? splineDelta.normalized : nextSplineTangent.normalized;
            courseVelocityWS = courseForwardWS * (splineDelta.magnitude / Mathf.Max(dt, 0.0001f));
            return true;
        }

        private void BuildLeviathanSpawnSet()
        {
            if (_leviathanPathNodeCount < 2 || !_leviathanNodeFrontNative.IsCreated)
                return;

            Vector3 boundsMin = ToVector3(_leviathanNodeFrontNative[0].Position);
            Vector3 boundsMax = boundsMin;
            float radiusPadding = Mathf.Max(1f, leviathanBodyRadius * (1f + leviathanWaveAmplitude));
            for (int i = 0; i < _leviathanPathNodeCount; i++)
            {
                Vector3 nodePosition = ToVector3(_leviathanNodeFrontNative[i].Position);
                Vector3 nodeExtents = new Vector3(radiusPadding, radiusPadding, radiusPadding);
                boundsMin = Vector3.Min(boundsMin, nodePosition - nodeExtents);
                boundsMax = Vector3.Max(boundsMax, nodePosition + nodeExtents);
            }

            _fieldCenter = (boundsMin + boundsMax) * 0.5f;
            _fieldExtents = Vector3.Max((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 2f, 2f));
            _renderBounds = new Bounds(_fieldCenter, Vector3.Max(boundsMax - boundsMin, new Vector3(4f, 4f, 4f)));
            _debugRenderBounds = _renderBounds;

            for (int i = 0; i < boidCount; i++)
            {
                float bodyT = boidCount > 1 ? i / (float)(boidCount - 1) : 0f;
                if (!TrySampleLeviathanPath(bodyT, out Vector3 centerlinePosition, out Vector3 tangentWS, out float bodyRadius))
                {
                    centerlinePosition = _fieldCenter;
                    tangentWS = Vector3.forward;
                    bodyRadius = leviathanBodyRadius;
                }

                Vector3 normalWS = Vector3.Cross(Vector3.up, tangentWS);
                if (normalWS.sqrMagnitude <= 0.0001f)
                    normalWS = Vector3.Cross(Vector3.forward, tangentWS);
                normalWS.Normalize();
                Vector3 binormalWS = Vector3.Cross(tangentWS, normalWS).normalized;
                float angle = HashToFloat01((uint)i, 0u, 0x6A09E667u) * Mathf.PI * 2f;
                float radialT = Mathf.Sqrt(HashToFloat01((uint)i, 0u, 0xBB67AE85u));
                float spawnSeed = HashToFloat01((uint)i, 0u, 0x94D049BBu);
                float lateralWave = Mathf.Sin(bodyT * 15.7f + spawnSeed * 6.2831853f) * (bodyRadius * leviathanWaveAmplitude * 0.45f);
                float radialDistance = bodyRadius * radialT * 0.78f;
                Vector3 spawnOffset =
                    normalWS * (Mathf.Cos(angle) * radialDistance + lateralWave) +
                    binormalWS * (Mathf.Sin(angle) * radialDistance * 0.55f);
                Vector3 spawnPosition = centerlinePosition + spawnOffset;

                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = tangentWS * cruiseSpeed,
                    Panic = 0f,
                    StateFlags = DefaultBoidStateFlags
                };
            }
        }

        private void BuildDeepSpawnSet(int zoneCount)
        {
            HectonBiolumZone primaryZone = _deepBiolumZones[0];
            Vector3 primaryPosition = primaryZone != null ? primaryZone.GetZonePosition() : Vector3.zero;
            Vector3 boundsMin = primaryPosition;
            Vector3 boundsMax = primaryPosition;
            Vector3 weightedCenter = Vector3.zero;
            float weightSum = 0f;

            for (int i = 0; i < zoneCount; i++)
            {
                HectonBiolumZone zone = _deepBiolumZones[i];
                if (zone == null)
                    continue;

                float score = Mathf.Max(0.0001f, _deepBiolumZoneScores[i]);
                Vector3 zonePosition = zone.GetZonePosition();
                weightedCenter += zonePosition * score;
                weightSum += score;

                Vector3 extents = new Vector3(deepBaitBallRadius, deepBaitBallHeight, deepBaitBallRadius);
                boundsMin = Vector3.Min(boundsMin, zonePosition - extents);
                boundsMax = Vector3.Max(boundsMax, zonePosition + extents);
            }

            _fieldCenter = weightSum > 0.0001f ? weightedCenter / weightSum : primaryPosition;
            _fieldExtents = Vector3.Max((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 1f, 2f));
            _renderBounds = new Bounds(_fieldCenter, Vector3.Max(boundsMax - boundsMin, new Vector3(4f, 2f, 4f)));
            _debugRenderBounds = _renderBounds;

            for (int i = 0; i < boidCount; i++)
            {
                int zoneIndex = i % zoneCount;
                HectonBiolumZone zone = _deepBiolumZones[zoneIndex];
                Vector3 anchorPosition = zone != null ? zone.GetZonePosition() : _fieldCenter;
                float radiusT = Mathf.Sqrt(HashToFloat01((uint)i, 0u, 0xA2F98A1Du));
                float angle = HashToFloat01((uint)i, 0u, 0x3C6EF372u) * Mathf.PI * 2f;
                float verticalT = HashToFloat01((uint)i, 0u, 0x1BF5C7D5u) * 2f - 1f;
                Vector3 spawnPosition = anchorPosition;
                spawnPosition.x += Mathf.Cos(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.z += Mathf.Sin(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.y += verticalT * deepBaitBallHeight;

                Vector3 toCenter = anchorPosition - spawnPosition;
                if (toCenter.sqrMagnitude <= 0.0001f)
                    toCenter = BuildInitialVelocity(i);
                else
                    toCenter.Normalize();

                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = toCenter * cruiseSpeed,
                    Panic = 0f,
                    StateFlags = DefaultBoidStateFlags
                };
            }
        }

        private void BuildDeepGrazingAnchors(int zoneCount)
        {
            _activeGrazingAnchorCount = 0;
            Vector3 fallbackPosition = _fieldCenter;
            for (int i = 0; i < zoneCount && _activeGrazingAnchorCount < grazingAnchorCount; i++)
            {
                HectonBiolumZone zone = _deepBiolumZones[i];
                if (zone == null)
                    continue;

                _grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
                {
                    Position = zone.GetZonePosition(),
                    Radius = deepBaitBallRadius,
                    Strength = Mathf.Lerp(1.2f, 1.8f, Mathf.Clamp01(_deepBiolumZoneScores[i])),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            for (int i = _activeGrazingAnchorCount; i < grazingAnchorCount; i++)
            {
                _grazingAnchors[i] = new GrazingAnchorData
                {
                    Position = fallbackPosition,
                    Radius = deepBaitBallRadius,
                    Strength = 0f,
                    Phase = 0f,
                    Padding = Vector2.zero
                };
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private void BuildSpawnSet(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            float sizeX = 1f / Mathf.Max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / Mathf.Max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackCenter = new Vector3(minX + sizeX * 0.5f + driftOffset.x, (minY + maxY) * 0.5f, minZ + sizeZ * 0.5f + driftOffset.z);

            _fieldCenter = fallbackCenter;
            _fieldExtents = new Vector3(sizeX * 0.5f, Mathf.Max(1f, maxDepthBelowSurface), sizeZ * 0.5f);
            _renderBounds = new Bounds(_fieldCenter, new Vector3(sizeX, Mathf.Max(2f, maxDepthBelowSurface + 2f), sizeZ));
            _debugRenderBounds = _renderBounds;

            for (int i = 0; i < boidCount; i++)
            {
                Vector3 spawnPosition = fallbackCenter;
                SargassumGlobalDragManager.SargassumFieldSample fieldSample = default;
                bool found = false;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    float u = HashToFloat01((uint)i, (uint)attempt, 0xA2F98A1Du);
                    float v = HashToFloat01((uint)i, (uint)attempt, 0x3C6EF372u);
                    float w = HashToFloat01((uint)i, (uint)attempt, 0x1BF5C7D5u);

                    spawnPosition.x = minX + u * sizeX + driftOffset.x;
                    spawnPosition.y = Mathf.Lerp(minY, maxY, w);
                    spawnPosition.z = minZ + v * sizeZ + driftOffset.z;

                    if (!dragManager.SampleDetailedInfluence(spawnPosition, 0.45f, cruiseSpeed, out fieldSample))
                        continue;

                    if (fieldSample.Density01 < densityThreshold || fieldSample.Window01 > windowThreshold)
                        continue;

                    found = true;
                    break;
                }

                if (!found)
                {
                    spawnPosition = fallbackCenter;
                }

                Vector3 velocity = BuildInitialVelocity(i);
                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = velocity,
                    Panic = 0f,
                    StateFlags = DefaultBoidStateFlags
                };
            }
        }

        private void BuildGrazingAnchors(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            float sizeX = 1f / Mathf.Max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / Mathf.Max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackPosition = new Vector3(minX + sizeX * 0.5f + driftOffset.x, Mathf.Lerp(minY, maxY, 0.32f), minZ + sizeZ * 0.5f + driftOffset.z);

            _activeGrazingAnchorCount = 0;
            for (int i = 0; i < grazingAnchorCount; i++)
            {
                Vector3 anchorPosition = fallbackPosition;
                SargassumGlobalDragManager.SargassumFieldSample fieldSample = default;
                bool found = false;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    float u = HashToFloat01((uint)i, (uint)attempt, 0x1F123BB5u);
                    float v = HashToFloat01((uint)i, (uint)attempt, 0x6B8B4567u);
                    float w = HashToFloat01((uint)i, (uint)attempt, 0x327B23C6u);

                    anchorPosition.x = minX + u * sizeX + driftOffset.x;
                    anchorPosition.y = Mathf.Lerp(minY, maxY, Mathf.Lerp(0.18f, 0.58f, w));
                    anchorPosition.z = minZ + v * sizeZ + driftOffset.z;

                    if (!dragManager.SampleDetailedInfluence(anchorPosition, grazingRadius * 0.35f, cruiseSpeed, out fieldSample))
                        continue;

                    if (fieldSample.Density01 < grazingDensityThreshold || fieldSample.Window01 > windowThreshold)
                        continue;

                    anchorPosition = fieldSample.AnchorWS;
                    found = true;
                    break;
                }

                if (!found)
                    continue;

                _grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
                {
                    Position = anchorPosition,
                    Radius = grazingRadius,
                    Strength = Mathf.Lerp(0.8f, 1.25f, fieldSample.Density01),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            for (int i = _activeGrazingAnchorCount; i < grazingAnchorCount; i++)
            {
                _grazingAnchors[i] = new GrazingAnchorData
                {
                    Position = fallbackPosition,
                    Radius = grazingRadius,
                    Strength = 0f,
                    Phase = 0f,
                    Padding = Vector2.zero
                };
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private Vector3 BuildInitialVelocity(int index)
        {
            float angle = HashToFloat01((uint)index, 0u, 0xDEADBEEFu) * Mathf.PI * 2f;
            float vertical = Mathf.Lerp(-0.15f, 0.15f, HashToFloat01((uint)index, 0u, 0x165667B1u));
            Vector3 direction = new Vector3(Mathf.Cos(angle), vertical, Mathf.Sin(angle));
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();
            return direction * cruiseSpeed;
        }

        private bool BindSimulationUniforms(
            float simulationDt,
            Vector3 driftOffset,
            Vector3 driftDelta,
            float hibernation01,
            SimulationLodTier simulationLodTier)
        {
            if (!_simulationFrameNative.IsCreated || _simulationFrameBuffer == null)
                return false;

            GraphicsBuffer readBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _boidsBufferB : _boidsBufferA;

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : _fieldCenter;
            Vector3 playerVelocity = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            Vector3 playerRight = playerTransform != null ? playerTransform.right : Vector3.right;
            Vector3 playerUp = playerTransform != null ? playerTransform.up : Vector3.up;
            Vector3 playerForward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            float playerSpeed = playerVelocity.magnitude;
            float headlightPanic01 = ResolveHeadlightPanic01();
            float parasiteAggression01 = ResolveParasiteAggression01();
            float panicPlayerRadiusScale =
                _playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive()
                    ? HectonVegetationConstants.BoidScooterPanicRadiusMultiplier
                    : 1f;
            if (headlightPanic01 > 0f)
                panicPlayerRadiusScale = Mathf.Max(panicPlayerRadiusScale, Mathf.Lerp(1f, deepHeadlightPanicRadiusScale, headlightPanic01));

            RenderTexture cutMaskTexture = null;
            Vector4 cutMaskWorldRect = Vector4.zero;
            bool cutMaskActive = !_deepModeActive &&
                                 cutManager != null &&
                                 cutManager.TryGetCutMask(out cutMaskTexture, out cutMaskWorldRect);
            Texture densityTexture = !_deepModeActive && dragManager != null ? dragManager.DensityFieldTexture : Texture2D.blackTexture;
            Vector3 cameraPosition = viewCamera != null ? viewCamera.transform.position : playerPosition;
            float transportCapsuleRadius = 0f;
            float transportCapsuleHalfLength = 0f;
            if (_playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive())
            {
                transportCapsuleRadius = Mathf.Max(boidBodyRadius * 6f, panicPlayerRadius * panicPlayerRadiusScale);
                transportCapsuleHalfLength = Mathf.Max(transportCapsuleRadius, playerSpeed * 0.35f);
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            UpdateFragmentationState(playerPosition, playerVelocity, playerForward, playerSpeed, absoluteSimulationTime);
            UpdateSonarScatterState(simulationDt, absoluteSimulationTime);
            float fragmentation01 = ResolveFragmentationStrength01(absoluteSimulationTime);
            float sonarScatterStrength01 = absoluteSimulationTime < _sonarScatterExpireTime
                ? Mathf.Clamp01(_sonarScatterStrength01)
                : 0f;

            SimulationFrameConstants frameConstants = default;
            frameConstants.Simulation0 = new float4(simulationDt, waterLevel, minDepthBelowSurface, maxDepthBelowSurface);
            frameConstants.Motion0 = new float4(cruiseSpeed, maxSpeed, panicSpeedBoost, 0f);
            frameConstants.Neighbor0 = new float4(perceptionRadius, separationRadius, boidBodyRadius, consumedCollapseSpeed);
            frameConstants.Flocking0 = new float4(separationWeight, alignmentWeight, cohesionWeight, containmentWeight);
            frameConstants.Flocking1 = new float4(panicWeight, noiseWeight, densityThreshold, windowThreshold);
            frameConstants.Flocking2 = new float4(gradientWorldStep, panicThreshold, panicDecay, 0f);
            frameConstants.Grazing0 = new float4(grazingWeight, grazingRadius, grazingRestSpeedScale, grazingRestHoldThreshold);
            frameConstants.Time0 = new float4(_simulationTime, _simulationPhaseOffset, massiveThreatWeight, canopyAffinityWeight);
            frameConstants.FieldCenter = new float4(_fieldCenter.x, _fieldCenter.y, _fieldCenter.z, hibernation01);
            frameConstants.FieldExtents = new float4(_fieldExtents.x, _fieldExtents.y, _fieldExtents.z, _spatialGridCellSizeWS);
            frameConstants.SpatialGridOrigin = new float4(
                _spatialGridOriginWS.x,
                _spatialGridOriginWS.y,
                _spatialGridOriginWS.z,
                cutMaskActive ? 1f : 0f);
            frameConstants.SpatialGridMeta = new int4(
                _spatialGridResolution.x,
                _spatialGridResolution.y,
                _spatialGridResolution.z,
                SpatialGridMaxBoidsPerCell);
            frameConstants.Counts0 = new int4(
                _activeBoidCount,
                _activeGrazingAnchorCount,
                _massiveThreats != null ? _massiveThreats.Length : 0,
                _debugFormationBeaconCount);
            frameConstants.Counts1 = new int4(_debugFormationObstacleCount, _leviathanPathNodeCount, (int)simulationLodTier, 0);
            frameConstants.DensityWorldRect = new float4(_densityWorldRect.x, _densityWorldRect.y, _densityWorldRect.z, _densityWorldRect.w);
            frameConstants.CutMaskWorldRect = new float4(cutMaskWorldRect.x, cutMaskWorldRect.y, cutMaskWorldRect.z, cutMaskWorldRect.w);
            frameConstants.DriftOffset = new float4(driftOffset.x, driftOffset.y, driftOffset.z, _deepModeActive ? 1f : 0f);
            frameConstants.DriftDelta = new float4(driftDelta.x, driftDelta.y, driftDelta.z, _deepModeActive ? deepClusterWeight : 0f);
            frameConstants.PlayerPosition = new float4(playerPosition.x, playerPosition.y, playerPosition.z, playerSpeed);
            frameConstants.PlayerVelocity = new float4(playerVelocity.x, playerVelocity.y, playerVelocity.z, panicPlayerSpeedThreshold);
            frameConstants.PlayerRight = new float4(playerRight.x, playerRight.y, playerRight.z, panicPlayerRadius);
            frameConstants.PlayerUp = new float4(playerUp.x, playerUp.y, playerUp.z, panicPlayerRadiusScale);
            frameConstants.PlayerForward = new float4(playerForward.x, playerForward.y, playerForward.z, headlightPanic01);
            frameConstants.CameraAvoidPosition = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, cameraAvoidRadius);
            frameConstants.CameraAvoidData = new float4(
                cameraAvoidWeight,
                _parasiteModeActive ? 1f : 0f,
                _parasiteModeActive ? parasiteAffinityWeight : 0f,
                parasiteAggression01);
            frameConstants.ParasiteAndFormation0 = new float4(
                parasiteLatchRadius,
                _formationModeActive ? 1f : 0f,
                formationWeight,
                formationRingThickness);
            frameConstants.Formation1 = new float4(
                formationPulseAmplitude,
                formationPulseSpeed,
                formationBreakPanicThreshold,
                formationObstacleWeight);
            frameConstants.Leviathan0 = new float4(
                _leviathanModeActive ? 1f : 0f,
                leviathanBodyWeight,
                leviathanForwardWeight,
                leviathanWaveAmplitude);
            frameConstants.Leviathan1 = new float4(
                leviathanWaveFrequency,
                _leviathanThreatLevel,
                leviathanSurroundThreatThreshold,
                leviathanSurroundRadius);
            frameConstants.Leviathan2 = new float4(
                leviathanSurroundWeight,
                leviathanSurroundSpinSpeed,
                _leviathanModeBlend,
                hibernationMinTimeScale);
            frameConstants.CameraPosition = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f);
            frameConstants.ThreatGridMeta = new int4(
                _threatGridResolution,
                _threatGridDataValid ? 1 : 0,
                0,
                0);
            frameConstants.ThreatGridCenter = new float4(
                _threatGridCenterWS.x,
                _threatGridCenterWS.y,
                _threatGridCenterWS.z,
                _threatGridCellSizeWS);
            frameConstants.ThreatVoxelMeta = new int4(
                _threatVoxelDimensions.x,
                _threatVoxelDimensions.y,
                _threatVoxelDimensions.z,
                _threatVoxelSolidThreshold);
            float ecosystemSpeedScale = Mathf.Max(0.25f, _ecosystemSpeedMultiplier);
            float ecosystemCamouflageScale = Mathf.Lerp(1f, ecosystemCamouflageWeight, _ecosystemCamouflageIndex);
            float ecosystemFitnessScale = Mathf.Lerp(1f, 1.15f, _ecosystemFitness);
            frameConstants.ThreatVoxelOrigin = new float4(
                _threatVoxelOriginWS.x,
                _threatVoxelOriginWS.y,
                _threatVoxelOriginWS.z,
                voxelAvoidanceLookAheadDistance * Mathf.Lerp(1f, ecosystemSpeedScale, 0.5f));
            frameConstants.ThreatVoxelCellSize = new float4(
                _threatVoxelCellSizeWS.x,
                _threatVoxelCellSizeWS.y,
                _threatVoxelCellSizeWS.z,
                voxelAvoidanceWeight * ecosystemCamouflageScale);
            frameConstants.TransportCapsule0 = new float4(
                playerPosition.x,
                playerPosition.y,
                playerPosition.z,
                transportCapsuleRadius);
            frameConstants.TransportCapsule1 = new float4(
                playerVelocity.x,
                playerVelocity.y,
                playerVelocity.z,
                transportCapsuleHalfLength);
            frameConstants.Ecosystem0 = new float4(
                fragmentationWeight * ecosystemFitnessScale,
                sonarScatterStrength01,
                activeSonarWaveBandWidth,
                activeSonarScatterImpulse + activeSonarScatterWeight);
            frameConstants.Fragmentation0 = new float4(
                _fragmentationCenterAWS.x,
                _fragmentationCenterAWS.y,
                _fragmentationCenterAWS.z,
                fragmentation01);
            frameConstants.Fragmentation1 = new float4(
                _fragmentationCenterBWS.x,
                _fragmentationCenterBWS.y,
                _fragmentationCenterBWS.z,
                Mathf.Max(1f, Vector3.Distance(_fragmentationCenterAWS, _fragmentationCenterBWS) * 0.5f));
            frameConstants.SonarScatter0 = new float4(
                _sonarScatterOriginWS.x,
                _sonarScatterOriginWS.y,
                _sonarScatterOriginWS.z,
                _sonarScatterWaveFrontWS);

            try
            {
                _simulationFrameNative[0] = frameConstants;
                GraphicsBufferUploadUtility.UploadNativeArray(_simulationFrameBuffer, _simulationFrameNative, 1);
                boidCompute.SetBuffer(_kernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferReadId, readBuffer);
                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferWriteId, writeBuffer);
                boidCompute.SetBuffer(_kernelIndex, _PbdCorrectionsId, _pbdCorrectionBuffer);
                boidCompute.SetBuffer(_kernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
                boidCompute.SetBuffer(_kernelIndex, _SpatialGridCellsId, _spatialGridCellBuffer);
                boidCompute.SetBuffer(_kernelIndex, _GrazingAnchorsId, _grazingAnchorBuffer);
                boidCompute.SetBuffer(_kernelIndex, _FormationBeaconsId, _formationBeaconBuffer);
                boidCompute.SetBuffer(_kernelIndex, _FormationObstaclesId, _formationObstacleBuffer);
                boidCompute.SetBuffer(_kernelIndex, _LeviathanNodesId, _leviathanNodeBuffer);
                boidCompute.SetBuffer(_kernelIndex, _MassiveThreatsId, _massiveThreatBuffer);
                boidCompute.SetBuffer(_kernelIndex, _ThreatGridId, _threatGridBuffer);
                boidCompute.SetBuffer(_kernelIndex, _ThreatVoxelGridId, _threatVoxelBuffer);
                if (_latchStatsBuffer != null)
                    boidCompute.SetBuffer(_kernelIndex, _LatchStatsId, _latchStatsBuffer);

                boidCompute.SetTexture(_kernelIndex, _DensityTexId, densityTexture);
                boidCompute.SetTexture(_kernelIndex, _CutMaskTexId, cutMaskActive ? cutMaskTexture : Texture2D.blackTexture);

                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _BoidsBufferReadId, readBuffer);
                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SpatialGridCellsId, _spatialGridCellBuffer);

                boidCompute.SetBuffer(_pbdSolveKernelIndex, _BoidsBufferReadId, readBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SpatialGridCellsId, _spatialGridCellBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _PbdCorrectionsId, _pbdCorrectionBuffer);

                boidCompute.SetBuffer(_clearPbdCorrectionsKernelIndex, _PbdCorrectionsId, _pbdCorrectionBuffer);
                boidCompute.SetBuffer(_clearPbdCorrectionsKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_clearSpatialGridKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_clearSpatialGridKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
            }
            catch (Exception exception)
            {
                DisableComputeDispatch($"Compute binding failure on '{boidCompute.name}'. {exception.Message}");
                return false;
            }

            _debugPlayerSpeed = playerSpeed;
            _debugPlayerPanicRadiusScale = panicPlayerRadiusScale;
            _debugParasiteAggression01 = parasiteAggression01;
            _debugMassiveThreatCount = _activeMassiveThreatCount;
            _debugFragmentation01 = fragmentation01;
            _debugSonarScatter01 = sonarScatterStrength01;
            return true;
        }

        private void HandleMassiveDisplacement(SargassumGlobalDragManager.MassiveDisplacementSignal signal)
        {
            if (_massiveThreats == null || _massiveThreats.Length == 0)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float panicRadius = Mathf.Max(massiveThreatPanicRadius, Mathf.Max(signal.ExtremePanicRadiusWS, signal.RadiusWS * 3f));
            int targetIndex = -1;
            float weakestEndTime = float.MaxValue;
            Vector3 inferredDirectionWS = Vector3.zero;

            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                MassiveThreatData threat = _massiveThreats[i];
                if (threat.EndTime <= absoluteSimulationTime)
                {
                    targetIndex = i;
                    break;
                }

                float planarDistanceSq = (new Vector2(threat.Position.x, threat.Position.z) - new Vector2(signal.PositionWS.x, signal.PositionWS.z)).sqrMagnitude;
                float mergeDistance = Mathf.Max(threat.PanicRadius, panicRadius) * 0.4f;
                if (planarDistanceSq <= mergeDistance * mergeDistance)
                {
                    targetIndex = i;
                    Vector3 delta = signal.PositionWS - threat.Position;
                    if (delta.sqrMagnitude > 0.0001f)
                        inferredDirectionWS = delta.normalized;
                    else if (threat.DirectionWS.sqrMagnitude > 0.0001f)
                        inferredDirectionWS = threat.DirectionWS.normalized;
                    break;
                }

                if (threat.EndTime < weakestEndTime)
                {
                    weakestEndTime = threat.EndTime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            if (inferredDirectionWS.sqrMagnitude <= 0.0001f &&
                playerTransform != null &&
                playerTransform.gameObject.activeInHierarchy)
            {
                Vector3 playerDelta = signal.PositionWS - playerTransform.position;
                if (playerDelta.sqrMagnitude <= panicRadius * panicRadius &&
                    playerTransform.TryGetComponent(out Rigidbody playerRigidbody) &&
                    playerRigidbody.linearVelocity.sqrMagnitude > 0.0001f)
                {
                    inferredDirectionWS = playerRigidbody.linearVelocity.normalized;
                }
            }

            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = signal.PositionWS,
                InnerRadius = Mathf.Max(0.5f, signal.RadiusWS),
                PanicRadius = panicRadius,
                Strength = 1f,
                EndTime = absoluteSimulationTime + Mathf.Max(0.25f, signal.Duration),
                DirectionWS = inferredDirectionWS,
                ThreatFlags = (uint)MassiveThreatFlags.None
            };

            RecalculateMassiveThreatCount();
            if (_massiveThreatBuffer != null)
                GraphicsBufferUploadUtility.UploadArray(_massiveThreatBuffer, _massiveThreats, _activeMassiveThreatCount);

            if ((_deepModeActive || _parasiteModeActive || _formationModeActive || _leviathanModeActive) && AbyssalFluidDecalManager.Instance != null)
            {
                float ruptureScale = Mathf.Clamp01(signal.RadiusWS / Mathf.Max(1f, deepBaitBallRadius * 2f));
                AbyssalFluidDecalManager.Instance.RegisterRuptureFluid(signal.PositionWS, ruptureScale);
            }

            Vector3 displacementDirection = _leviathanHeadVelocityWS.sqrMagnitude > 0.0001f
                ? _leviathanHeadVelocityWS
                : (signal.PositionWS - _fieldCenter);
            TriggerFragmentation(signal.PositionWS, displacementDirection, signal.ExtremePanicRadiusWS, absoluteSimulationTime);
        }

        internal void RegisterLeviathanThreatPulse(
            Vector3 positionWS,
            Vector3 directionWS,
            float panicRadiusWS,
            float durationSeconds)
        {
            if (_massiveThreats == null || _massiveThreats.Length == 0)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            int targetIndex = -1;
            float weakestEndTime = float.MaxValue;
            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                MassiveThreatData threat = _massiveThreats[i];
                if (threat.EndTime <= absoluteSimulationTime)
                {
                    targetIndex = i;
                    break;
                }

                if ((threat.ThreatFlags & (uint)MassiveThreatFlags.LeviathanHuntPulse) == 0u)
                {
                    if (threat.EndTime < weakestEndTime)
                    {
                        weakestEndTime = threat.EndTime;
                        targetIndex = i;
                    }

                    continue;
                }

                float mergeDistance = Mathf.Max(threat.PanicRadius, panicRadiusWS) * 0.35f;
                if ((threat.Position - positionWS).sqrMagnitude <= mergeDistance * mergeDistance)
                {
                    targetIndex = i;
                    break;
                }

                if (threat.EndTime < weakestEndTime)
                {
                    weakestEndTime = threat.EndTime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            Vector3 resolvedDirection = directionWS.sqrMagnitude > 0.0001f ? directionWS.normalized : Vector3.forward;
            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = positionWS,
                InnerRadius = Mathf.Max(0.5f, boidBodyRadius * 2f),
                PanicRadius = Mathf.Max(4f, panicRadiusWS),
                Strength = 1f,
                EndTime = absoluteSimulationTime + Mathf.Max(0.15f, durationSeconds),
                DirectionWS = resolvedDirection,
                ThreatFlags = (uint)MassiveThreatFlags.LeviathanHuntPulse
            };

            RecalculateMassiveThreatCount();
            if (_massiveThreatBuffer != null)
                GraphicsBufferUploadUtility.UploadArray(_massiveThreatBuffer, _massiveThreats, _activeMassiveThreatCount);
        }

        private void UpdateFragmentationState(
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 playerForward,
            float playerSpeed,
            float absoluteSimulationTime)
        {
            if (_playerTransportCoordinator != null &&
                _playerTransportCoordinator.IsTransportActive() &&
                playerSpeed >= panicPlayerSpeedThreshold)
            {
                Vector3 dashDirection = playerVelocity.sqrMagnitude > 0.0001f ? playerVelocity : playerForward;
                TriggerFragmentation(playerPosition, dashDirection, Mathf.Max(panicPlayerRadius, boidBodyRadius * 6f), absoluteSimulationTime);
            }

            if (_leviathanHeadValid &&
                _leviathanHeadVelocityWS.magnitude >= leviathanShockwaveSpeedThreshold)
            {
                TriggerFragmentation(
                    _leviathanHeadPositionWS,
                    _leviathanHeadVelocityWS,
                    Mathf.Max(_leviathanHeadRadiusWS * 2.5f, leviathanShockwaveRadius * 0.45f),
                    absoluteSimulationTime);
            }

            if (absoluteSimulationTime >= _fragmentationExpireTime)
            {
                _fragmentationStartTime = float.NegativeInfinity;
                _fragmentationExpireTime = float.NegativeInfinity;
                _debugFragmentation01 = 0f;
            }
        }

        private void UpdateSonarScatterState(float simulationDt, float absoluteSimulationTime)
        {
            if (_sonarScatterStrength01 <= 0f || absoluteSimulationTime >= _sonarScatterExpireTime)
            {
                _sonarScatterStrength01 = 0f;
                _sonarScatterWaveFrontWS = 0f;
                _debugSonarScatter01 = 0f;
                return;
            }

            _sonarScatterWaveFrontWS += Mathf.Max(0f, simulationDt) * Mathf.Max(0.1f, activeSonarWaveSpeed);
        }

        private float ResolveFragmentationStrength01(float absoluteSimulationTime)
        {
            if (absoluteSimulationTime >= _fragmentationExpireTime ||
                !float.IsFinite(_fragmentationExpireTime) ||
                !float.IsFinite(_fragmentationStartTime))
                return 0f;

            float duration = Mathf.Max(0.1f, _fragmentationExpireTime - _fragmentationStartTime);
            float timeRemaining = Mathf.Max(0f, _fragmentationExpireTime - absoluteSimulationTime);
            return Mathf.Clamp01(timeRemaining / Mathf.Max(0.1f, duration));
        }

        private void TriggerFragmentation(Vector3 originWS, Vector3 dashVectorWS, float baseRadiusWS, float absoluteSimulationTime)
        {
            Vector3 dashDirection = dashVectorWS.sqrMagnitude > 0.0001f ? dashVectorWS.normalized : Vector3.forward;
            Vector3 splitAxis = Vector3.Cross(Vector3.up, dashDirection);
            if (splitAxis.sqrMagnitude <= 0.0001f)
                splitAxis = Vector3.Cross(Vector3.right, dashDirection);
            if (splitAxis.sqrMagnitude <= 0.0001f)
                splitAxis = Vector3.forward;
            else
                splitAxis.Normalize();

            float offsetDistance = Mathf.Max(1f, baseRadiusWS * Mathf.Max(0.5f, fragmentationOffsetScale));
            _fragmentationCenterAWS = originWS + splitAxis * offsetDistance;
            _fragmentationCenterBWS = originWS - splitAxis * offsetDistance;
            float safeMinDuration = Mathf.Max(5f, fragmentationMinDurationSeconds);
            float safeMaxDuration = Mathf.Max(safeMinDuration, fragmentationMaxDurationSeconds);
            float duration01 = Mathf.Clamp01(dashVectorWS.magnitude / Mathf.Max(0.1f, panicPlayerSpeedThreshold));
            _fragmentationStartTime = absoluteSimulationTime;
            _fragmentationExpireTime = absoluteSimulationTime + Mathf.Lerp(safeMinDuration, safeMaxDuration, duration01);
            _debugFragmentation01 = 1f;
        }

        private void HandleFlashlightToggled(bool isOn)
        {
            _flashlightOn = isOn;
            if (!isOn || !IsDeepModeActive() || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            _headlightPanicTimer = deepHeadlightPanicDuration;
            _debugHeadlightPanic01 = 1f;
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0f)
            {
                _sonarScatterStrength01 = 0f;
                _debugSonarScatter01 = 0f;
                return;
            }

            Vector3 originWS = playerTransform != null ? playerTransform.position : _fieldCenter;
            float maxFieldExtent = Mathf.Max(_fieldExtents.x, Mathf.Max(_fieldExtents.y, _fieldExtents.z));
            float safeWaveSpeed = Mathf.Max(0.1f, activeSonarWaveSpeed);
            float travelDistance = (maxFieldExtent * 2f) + Mathf.Max(0.25f, activeSonarWaveBandWidth);

            _sonarScatterOriginWS = originWS;
            _sonarScatterWaveFrontWS = 0f;
            _sonarScatterStrength01 = clampedIntensity;
            _sonarScatterExpireTime = GetAbsoluteSimulationTime() + (travelDistance / safeWaveSpeed);
            _debugSonarScatter01 = clampedIntensity;
        }

        private float ResolveHeadlightPanic01()
        {
            if (!_deepModeActive || deepHeadlightPanicDuration <= 0.0001f)
                return 0f;

            return Mathf.Clamp01(_headlightPanicTimer / deepHeadlightPanicDuration);
        }

        private void ApplyParasiteHullStress()
        {
            if (_playerMovement == null || !_parasiteModeActive)
                return;

            float aggression01 = ResolveParasiteAggression01();
            if (aggression01 <= 0f)
                return;

            float requestedStress = Mathf.Clamp01(Mathf.Lerp(parasiteHullStressIntensity, parasiteHullStressIntensity + parasiteHullStressLightBoost, aggression01));
            if (requestedStress <= 0.0001f)
                return;

            _playerMovement.RequestExternalHullStress(requestedStress);
        }

        private void UpdateParasiteLatchReadback(float dt)
        {
            if (_parasiteLatchReadbackPending)
            {
                if (!_parasiteLatchReadbackRequest.done)
                    return;

                _parasiteLatchReadbackPending = false;
                if (!_parasiteLatchReadbackRequest.hasError)
                {
                    var latchData = _parasiteLatchReadbackRequest.GetData<int>();
                    _reportedLatchedDroneCount = latchData.Length > LatchStatsLatchedCountIndex
                        ? Mathf.Clamp(latchData[LatchStatsLatchedCountIndex], 0, _activeBoidCount)
                        : 0;
                    if (_reportedLatchedDroneCount > 0 && latchData.Length >= LatchStatsElementCount)
                    {
                        float divisor = LatchStatsQuantize * Mathf.Max(1, _reportedLatchedDroneCount);
                        _reportedParasiteCenterOfMassLS = new Vector3(
                            latchData[LatchStatsLatchedSumXIndex] / divisor,
                            latchData[LatchStatsLatchedSumYIndex] / divisor,
                            latchData[LatchStatsLatchedSumZIndex] / divisor);
                    }
                    else
                    {
                        _reportedParasiteCenterOfMassLS = Vector3.zero;
                    }

                    if (_reportedLatchedDroneCount >= parasiteHarvesterLatchThreshold &&
                        TryResolveNearestHarvesterAnchor(playerTransform != null ? playerTransform.position : _fieldCenter, out Vector3 harvesterAnchorWS))
                    {
                        _reportedParasiteHarvesterPullWS = (harvesterAnchorWS - (playerTransform != null ? playerTransform.position : _fieldCenter)).normalized;
                    }
                    else
                    {
                        _reportedParasiteHarvesterPullWS = Vector3.zero;
                    }

                    _debugLatchedDroneCount = _reportedLatchedDroneCount;
                    _debugParasiteCenterOfMassLS = _reportedParasiteCenterOfMassLS;
                    _debugParasiteHarvesterPullWS = _reportedParasiteHarvesterPullWS;

                    _reportedWakeFleeCount = latchData.Length > LatchStatsWakeCountIndex
                        ? Mathf.Clamp(latchData[LatchStatsWakeCountIndex], 0, _activeBoidCount)
                        : 0;
                    if (_reportedWakeFleeCount >= WakeMinimumFleeBoids)
                    {
                        float wakeDivisor = WakeStatsQuantize * Mathf.Max(1, _reportedWakeFleeCount);
                        _reportedWakeCenterWS = new Vector3(
                            latchData[LatchStatsWakePosXIndex] / wakeDivisor,
                            latchData[LatchStatsWakePosYIndex] / wakeDivisor,
                            latchData[LatchStatsWakePosZIndex] / wakeDivisor);
                        Vector3 averageWakeVelocity = new Vector3(
                            latchData[LatchStatsWakeVelXIndex] / wakeDivisor,
                            latchData[LatchStatsWakeVelYIndex] / wakeDivisor,
                            latchData[LatchStatsWakeVelZIndex] / wakeDivisor);
                        _reportedWakeFlowDirectionWS = averageWakeVelocity.sqrMagnitude > 0.0001f
                            ? averageWakeVelocity.normalized
                            : Vector3.zero;

                        if (_reportedWakeFlowDirectionWS.sqrMagnitude > 0.0001f)
                        {
                            if (_mapMagicVegetationBridge == null)
                                _mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

                            if (_mapMagicVegetationBridge != null)
                            {
                                _mapMagicVegetationBridge.RegisterSwarmWakeImpulse(
                                    _reportedWakeCenterWS,
                                    _reportedWakeFlowDirectionWS * WakeFlowStrength,
                                    WakeFlowRadius,
                                    WakeFlowLifetimeSeconds);
                            }
                        }
                    }
                    else
                    {
                        _reportedWakeCenterWS = Vector3.zero;
                        _reportedWakeFlowDirectionWS = Vector3.zero;
                    }
                }
                else
                {
                    _reportedLatchedDroneCount = 0;
                    _reportedParasiteCenterOfMassLS = Vector3.zero;
                    _reportedParasiteHarvesterPullWS = Vector3.zero;
                    _reportedWakeFleeCount = 0;
                    _reportedWakeCenterWS = Vector3.zero;
                    _reportedWakeFlowDirectionWS = Vector3.zero;
                    _debugLatchedDroneCount = 0;
                    _debugParasiteCenterOfMassLS = Vector3.zero;
                    _debugParasiteHarvesterPullWS = Vector3.zero;
                }

                return;
            }

            if (_latchStatsBuffer == null)
            {
                _reportedLatchedDroneCount = 0;
                _reportedParasiteCenterOfMassLS = Vector3.zero;
                _reportedParasiteHarvesterPullWS = Vector3.zero;
                _reportedWakeFleeCount = 0;
                _reportedWakeCenterWS = Vector3.zero;
                _reportedWakeFlowDirectionWS = Vector3.zero;
                _debugLatchedDroneCount = 0;
                _debugParasiteCenterOfMassLS = Vector3.zero;
                _debugParasiteHarvesterPullWS = Vector3.zero;
                _parasiteLatchReadbackTimer = 0f;
                return;
            }

            if (!_parasiteModeActive)
            {
                _reportedLatchedDroneCount = 0;
                _reportedParasiteCenterOfMassLS = Vector3.zero;
                _reportedParasiteHarvesterPullWS = Vector3.zero;
                _debugLatchedDroneCount = 0;
                _debugParasiteCenterOfMassLS = Vector3.zero;
                _debugParasiteHarvesterPullWS = Vector3.zero;
            }

            _parasiteLatchReadbackTimer -= Mathf.Max(0f, dt);
        }

        private void TryRequestParasiteLatchReadback()
        {
            if (_parasiteLatchReadbackPending ||
                _latchStatsBuffer == null ||
                _parasiteLatchReadbackTimer > 0f)
            {
                return;
            }

            _parasiteLatchReadbackRequest = AsyncGPUReadback.Request(_latchStatsBuffer);
            _parasiteLatchReadbackPending = true;
            _parasiteLatchReadbackTimer = parasiteLatchReadbackInterval;
        }

        private void ApplyParasiteEnvironmentalDrag()
        {
            if (_playerMovement == null || !_parasiteModeActive || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            float latch01 = Mathf.Clamp01(_reportedLatchedDroneCount / Mathf.Max(1f, parasiteMaxLatchedDronesForFullDrag));
            _playerMovement.ApplyParasiteLatchInfluence(
                _reportedLatchedDroneCount,
                _reportedParasiteCenterOfMassLS,
                _reportedParasiteHarvesterPullWS);
            if (latch01 <= 0.0001f)
                return;

            float aggression01 = ResolveParasiteAggression01();
            float dragWeight = Mathf.Clamp01(latch01 * Mathf.Lerp(0.65f, 1f, aggression01));
            float requestedDragMultiplier = Mathf.Lerp(1f, parasiteMaxEnvironmentalDragMultiplier, dragWeight);
            if (requestedDragMultiplier <= 1.0001f)
                return;

            _playerMovement.ApplyEnvironmentalDrag(requestedDragMultiplier);
        }

        private void ApplyLeviathanPhysicalStrike()
        {
            if ((_playerMovement == null && _playerHealth == null) || playerTransform == null || _leviathanStrikeCooldownTimer > 0f)
                return;

            Vector3 toPlayer = playerTransform.position - _leviathanHeadPositionWS;
            if (toPlayer.sqrMagnitude > leviathanStrikeRadius * leviathanStrikeRadius)
                return;

            Vector3 strikeDirection = _leviathanHeadVelocityWS.sqrMagnitude > 0.0001f
                ? _leviathanHeadVelocityWS.normalized
                : _leviathanHeadForwardWS;
            if (strikeDirection.sqrMagnitude <= 0.0001f)
                strikeDirection = Vector3.forward;

            float speed01 = Mathf.Clamp01(_leviathanHeadVelocityWS.magnitude / Mathf.Max(0.1f, leviathanShockwaveSpeedThreshold));
            Vector3 traumaImpulse = strikeDirection * (leviathanStrikeImpulse * Mathf.Lerp(0.8f, 1.35f, speed01));
            if (_playerMovement != null)
                _playerMovement.ApplyPhysicalTrauma(traumaImpulse, Mathf.Lerp(leviathanStrikeTraumaWeight * 0.65f, leviathanStrikeTraumaWeight, speed01));

            if (_playerHealth != null)
                _playerHealth.TakeDamage(leviathanStrikeDamage);

            _leviathanStrikeCooldownTimer = leviathanStrikeCooldown;
        }

        private void ApplyLeviathanShockwave()
        {
            if (_leviathanShockwaveCooldownTimer > 0f ||
                _leviathanHeadVelocityWS.magnitude < leviathanShockwaveSpeedThreshold ||
                _leviathanShockwaveRigidbodies == null)
            {
                return;
            }

            int rigidbodyCount = 0;
            if (_leviathanShockwaveSpatialHits != null)
            {
                const SpatialTargetKind shockwaveKinds = SpatialTargetKind.Resource | SpatialTargetKind.Pickup | SpatialTargetKind.Module | SpatialTargetKind.Signal;
                int spatialHitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                    _leviathanHeadPositionWS,
                    leviathanShockwaveRadius,
                    shockwaveKinds,
                    _leviathanShockwaveSpatialHits);
                for (int i = 0; i < spatialHitCount; i++)
                {
                    Transform candidateTransform = _leviathanShockwaveSpatialHits[i].Transform;
                    if (candidateTransform == null || candidateTransform == playerTransform)
                        continue;

                    if (candidateTransform.TryGetComponent(out Rigidbody candidateBody))
                        TryAppendShockwaveBody(candidateBody, ref rigidbodyCount);
                }
            }

            if (_leviathanShockwaveColliders != null)
            {
                int colliderCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                    _leviathanHeadPositionWS,
                    leviathanShockwaveRadius,
                    _leviathanShockwaveColliders,
                    leviathanShockwaveLayers,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < colliderCount; i++)
                {
                    Collider hitCollider = _leviathanShockwaveColliders[i];
                    if (hitCollider == null)
                        continue;

                    Rigidbody candidateBody = hitCollider.attachedRigidbody;
                    if (candidateBody == null || candidateBody == _playerRigidbody)
                        continue;

                    TryAppendShockwaveBody(candidateBody, ref rigidbodyCount);
                }
            }

            if (rigidbodyCount <= 0)
                return;

            float originDensity01 = 0f;
            if (dragManager != null && dragManager.SampleInfluence(_leviathanHeadPositionWS, _leviathanHeadRadiusWS, out _, out _, out float sampledOriginDensity))
                originDensity01 = sampledOriginDensity;

            Vector3 headDirection = _leviathanHeadVelocityWS.sqrMagnitude > 0.0001f
                ? _leviathanHeadVelocityWS.normalized
                : _leviathanHeadForwardWS;
            float shockwaveSpeed01 = Mathf.Clamp01(_leviathanHeadVelocityWS.magnitude / Mathf.Max(leviathanShockwaveSpeedThreshold, 0.001f));
            for (int i = 0; i < rigidbodyCount; i++)
            {
                Rigidbody targetBody = _leviathanShockwaveRigidbodies[i];
                _leviathanShockwaveRigidbodies[i] = null;
                if (targetBody == null || targetBody == _playerRigidbody || targetBody.isKinematic)
                    continue;

                Vector3 bodyCenter = targetBody.worldCenterOfMass;
                Vector3 radialDirection = bodyCenter - _leviathanHeadPositionWS;
                float radialDistance = radialDirection.magnitude;
                if (radialDistance <= 0.0001f)
                    radialDirection = headDirection;
                else
                    radialDirection /= radialDistance;

                float distance01 = Mathf.Clamp01(1f - radialDistance / Mathf.Max(leviathanShockwaveRadius, 0.001f));
                if (distance01 <= 0.0001f)
                    continue;

                float density01 = originDensity01;
                if (dragManager != null && dragManager.SampleInfluence(bodyCenter, 0.75f, out _, out _, out float sampledDensity))
                    density01 = Mathf.Max(density01, sampledDensity);

                Vector3 impulseDirection = Vector3.Lerp(radialDirection, headDirection, 0.35f);
                impulseDirection.y += leviathanShockwaveVerticalLift;
                if (impulseDirection.sqrMagnitude <= 0.0001f)
                    impulseDirection = Vector3.up;
                else
                    impulseDirection.Normalize();

                float impulseMagnitude = leviathanShockwaveImpulse *
                                         Mathf.Lerp(0.7f, 1.35f, shockwaveSpeed01) *
                                         Mathf.Lerp(0.8f, 1.25f, density01) *
                                         distance01;
                PhysicsForceRouter.QueueForce(
                    targetBody,
                    impulseDirection * impulseMagnitude,
                    ForceMode.Impulse);
            }

            _leviathanShockwaveCooldownTimer = leviathanShockwaveCadence;
        }

        private void TryAppendShockwaveBody(Rigidbody candidateBody, ref int rigidbodyCount)
        {
            if (candidateBody == null || _leviathanShockwaveRigidbodies == null)
                return;

            int capacity = Mathf.Min(_leviathanShockwaveRigidbodies.Length, leviathanShockwaveHitCapacity);
            for (int i = 0; i < rigidbodyCount; i++)
            {
                if (_leviathanShockwaveRigidbodies[i] == candidateBody)
                    return;
            }

            if (rigidbodyCount >= capacity)
                return;

            _leviathanShockwaveRigidbodies[rigidbodyCount] = candidateBody;
            rigidbodyCount++;
        }

        private bool TryResolveNearestHarvesterAnchor(Vector3 origin, out Vector3 anchorWS)
        {
            anchorWS = origin;
            if (_mapMagicVegetationBridge == null)
                _mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_mapMagicVegetationBridge == null)
                return false;

            Vector3[] anchors = _mapMagicVegetationBridge.ActiveAbyssalAnchors;
            int anchorCount = _mapMagicVegetationBridge.ActiveAbyssalAnchorCount;
            if (anchors == null || anchorCount <= 0)
                return false;

            float nearestDistanceSq = float.PositiveInfinity;
            int cappedCount = Mathf.Min(anchorCount, anchors.Length);
            for (int i = 0; i < cappedCount; i++)
            {
                Vector3 candidate = anchors[i];
                float distanceSq = (candidate - origin).sqrMagnitude;
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                anchorWS = candidate;
            }

            return !float.IsPositiveInfinity(nearestDistanceSq);
        }

        private void UpdateMassiveThreats()
        {
            if (_massiveThreats == null)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            _activeMassiveThreatCount = 0;
            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                if (_massiveThreats[i].EndTime > absoluteSimulationTime)
                    _activeMassiveThreatCount++;
            }
            _debugMassiveThreatCount = _activeMassiveThreatCount;
        }

        private void DispatchClearLatchStats()
        {
            if (_latchStatsBuffer == null || boidCompute == null || _clearStatsKernelIndex < 0)
                return;

            boidCompute.SetBuffer(_clearStatsKernelIndex, _LatchStatsId, _latchStatsBuffer);
            boidCompute.Dispatch(_clearStatsKernelIndex, 1, 1, 1);
        }

        private void UpdateSpatialGridLayout()
        {
            float baseCellSize = Mathf.Max(0.5f, Mathf.Max(perceptionRadius, separationRadius));
            Vector3 fieldSize = Vector3.Max(_fieldExtents * 2f, Vector3.one * baseCellSize);
            float axisClampCellSize = Mathf.Max(
                fieldSize.x / SpatialGridMaxAxisResolution,
                Mathf.Max(fieldSize.y / SpatialGridMaxAxisResolution, fieldSize.z / SpatialGridMaxAxisResolution));
            _spatialGridCellSizeWS = Mathf.Max(baseCellSize, axisClampCellSize);

            Vector3 fieldMin = _fieldCenter - _fieldExtents;
            Vector3 fieldMax = _fieldCenter + _fieldExtents;
            // Negative-space-safe bounds_min anchor. The compute shader subtracts this origin before cell division.
            _spatialGridOriginWS = new Vector3(
                FloorToMultiple(fieldMin.x, _spatialGridCellSizeWS),
                FloorToMultiple(fieldMin.y, _spatialGridCellSizeWS),
                FloorToMultiple(fieldMin.z, _spatialGridCellSizeWS));

            int resolutionX = Mathf.Clamp(Mathf.CeilToInt((fieldMax.x - _spatialGridOriginWS.x) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            int resolutionY = Mathf.Clamp(Mathf.CeilToInt((fieldMax.y - _spatialGridOriginWS.y) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            int resolutionZ = Mathf.Clamp(Mathf.CeilToInt((fieldMax.z - _spatialGridOriginWS.z) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            _spatialGridResolution = new Vector3Int(resolutionX, resolutionY, resolutionZ);
            int cellCount = resolutionX * resolutionY * resolutionZ;
            _clearSpatialGridDispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(cellCount / (float)SpatialGridClearThreadGroupSize));
        }

        private void DispatchClearSpatialGrid()
        {
            if (boidCompute == null || _clearSpatialGridKernelIndex < 0 || _spatialGridCountBuffer == null)
                return;

            boidCompute.Dispatch(_clearSpatialGridKernelIndex, _clearSpatialGridDispatchGroupCount, 1, 1);
        }

        private void DispatchClearPbdCorrections()
        {
            if (boidCompute == null || _clearPbdCorrectionsKernelIndex < 0 || _pbdCorrectionBuffer == null)
                return;

            boidCompute.Dispatch(_clearPbdCorrectionsKernelIndex, _dispatchGroupCount, 1, 1);
        }

        private float ResolveCameraDistanceSq()
        {
            if (viewCamera == null)
            {
                ResolveDependencies();
                if (viewCamera == null)
                    return 0f;
            }

            Vector3 cameraPosition = viewCamera.transform.position;
            return (_renderBounds.center - cameraPosition).sqrMagnitude;
        }

        private bool TryConsumeSimulationStep(
            float frameDeltaTime,
            float cameraDistanceSq,
            out float simulationDeltaTime,
            out float hibernation01,
            out SimulationLodTier simulationLodTier)
        {
            CompletePendingFoveatedSimulationDecision(forceComplete: false);

            FoveatedSimulationDecision decision = default;
            if (_foveatedSimulationFrontNative.IsCreated && _foveatedSimulationFrontNative.Length > 0)
                decision = _foveatedSimulationFrontNative[0];

            simulationDeltaTime = Mathf.Max(0f, decision.SimulationDeltaTime);
            hibernation01 = Mathf.Clamp01(decision.Hibernation01);
            simulationLodTier = (SimulationLodTier)Mathf.Clamp(decision.Tier, (int)SimulationLodTier.Full, (int)SimulationLodTier.Sleep);
            _sleepVelocityWritePending = simulationLodTier == SimulationLodTier.Sleep && _lastSimulationLodTier != SimulationLodTier.Sleep;
            _lastSimulationLodTier = simulationLodTier;
            ScheduleFoveatedSimulationDecision(frameDeltaTime, cameraDistanceSq, decision.Accumulator);
            return decision.DispatchSimulation != 0 && simulationDeltaTime > 0f;
        }

        private bool ShouldRenderSwarm(float cameraDistanceSq)
        {
            if (viewCamera == null)
                return true;

            float maxDistanceSq = simulationCullDistance * simulationCullDistance;
            if (cameraDistanceSq > maxDistanceSq)
                return false;

            return CheckFrustumVisibility();
        }

        private void RecalculateMassiveThreatCount()
        {
            _activeMassiveThreatCount = 0;
            if (_massiveThreats == null)
            {
                _debugMassiveThreatCount = 0;
                return;
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                if (_massiveThreats[i].EndTime > absoluteSimulationTime)
                    _activeMassiveThreatCount++;
            }

            _debugMassiveThreatCount = _activeMassiveThreatCount;
        }

        private void RenderCurrentBuffer()
        {
            GraphicsBuffer currentBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            bool vatEnabled = boidVatPositionTexture != null &&
                              boidVatNormalTexture != null &&
                              boidVatFrameCount > 1;
            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetBuffer(_BoidsBufferId, currentBuffer);
            _materialPropertyBlock.SetFloat(_ParasiteModeId, _parasiteModeActive ? 1f : 0f);
            _materialPropertyBlock.SetFloat(_ParasiteAggressionId, _debugParasiteAggression01);
            _materialPropertyBlock.SetFloat(_VelocitySleepScaleId, _debugHibernation01 >= 0.999f ? 0f : 1f);
            _materialPropertyBlock.SetFloat(_VatEnabledId, vatEnabled ? 1f : 0f);
            _materialPropertyBlock.SetFloat(_VatFrameCountId, vatEnabled ? boidVatFrameCount : 1f);
            _materialPropertyBlock.SetFloat(_VatVertexCountId, boidMesh != null ? boidMesh.vertexCount : 0f);
            _materialPropertyBlock.SetFloat(_VatPlaybackSpeedId, boidVatPlaybackSpeed);
            _materialPropertyBlock.SetFloat(_VatInstancePhaseScaleId, boidVatInstancePhaseScale);
            _materialPropertyBlock.SetFloat(_VatPositionScaleId, boidVatPositionScale);
            _materialPropertyBlock.SetFloat(_VatNormalBlendId, boidVatNormalBlend);
            if (vatEnabled)
            {
                _materialPropertyBlock.SetTexture(_VatPositionTexId, boidVatPositionTexture);
                _materialPropertyBlock.SetTexture(_VatNormalTexId, boidVatNormalTexture);
            }

            int targetLayer = useGameObjectLayer ? gameObject.layer : 0;
            RenderParams renderParams = new RenderParams(boidMaterial)
            {
                worldBounds = _renderBounds,
                matProps = _materialPropertyBlock,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                layer = targetLayer,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMeshPrimitives(renderParams, boidMesh, 0, _activeBoidCount);
        }

        private bool CheckFrustumVisibility()
        {
            GeometryUtility.CalculateFrustumPlanes(viewCamera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _renderBounds);
        }

        private void TryRegister()
        {

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (!_registeredFixedTick)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _boidsBufferA);
            ReleaseBuffer(ref _boidsBufferB);
            ReleaseBuffer(ref _grazingAnchorBuffer);
            ReleaseBuffer(ref _massiveThreatBuffer);
            ReleaseBuffer(ref _formationBeaconBuffer);
            ReleaseBuffer(ref _formationObstacleBuffer);
            ReleaseBuffer(ref _leviathanNodeBuffer);
            ReleaseBuffer(ref _latchStatsBuffer);
            ReleaseBuffer(ref _pbdCorrectionBuffer);
            ReleaseBuffer(ref _threatGridBuffer);
            ReleaseBuffer(ref _threatVoxelBuffer);
            ReleaseBuffer(ref _spatialGridCountBuffer);
            ReleaseBuffer(ref _spatialGridCellBuffer);
            ReleaseBuffer(ref _simulationFrameBuffer);
        }

        private void CompletePendingReadbackAndReleaseBuffers()
        {
            CompletePendingLeviathanNodeBuild(forceComplete: true);
            if (_parasiteLatchReadbackPending)
            {
                _parasiteLatchReadbackPending = false;
                _parasiteLatchReadbackRequest = default;
            }

            _parasiteLatchReadbackTimer = 0f;
            ReleaseBuffers();
            ResetComputeKernelBindings();
            _boundBoidCompute = null;
            DisposeNativeArray(ref _staticObstacleCache);
            DisposeNativeArray(ref _leviathanPathScratchNative);
            DisposeNativeArray(ref _leviathanNodeFrontNative);
            DisposeNativeArray(ref _leviathanNodeBackNative);
            DisposeNativeArray(ref _leviathanNodeCountNative);
            DisposeFoveatedSimulationBuffers();
            DisposeNativeArray(ref _threatGridUploadNative);
            DisposeNativeArray(ref _threatVoxelUploadNative);
            ResetThreatGridSnapshot();
            ResetThreatVoxelSnapshot();
            DisposeNativeArray(ref _simulationFrameNative);
        }

        private void PrimeFoveatedSimulationDecision(float frameDeltaTime, float cameraDistanceSq)
        {
            EnsureNativeArrayCapacity(ref _foveatedSimulationInputNative, 1);
            EnsureNativeArrayCapacity(ref _foveatedSimulationFrontNative, 1);
            EnsureNativeArrayCapacity(ref _foveatedSimulationBackNative, 1);
            PopulateFoveatedSimulationInput(frameDeltaTime, cameraDistanceSq, previousAccumulator: 0f);
            var primeJob = new EvaluateSimulationLodJob
            {
                Input = _foveatedSimulationInputNative,
                Output = _foveatedSimulationBackNative
            };

            // COLD SYNC JOB: prime the foveated LOD front buffer before the first runtime Tick so tier selection stays Burst-authored from frame 0.
            JobHandle primeHandle = primeJob.Schedule();
            primeHandle.Complete();
            (_foveatedSimulationFrontNative, _foveatedSimulationBackNative) = (_foveatedSimulationBackNative, _foveatedSimulationFrontNative);
            _foveatedSimulationScheduled = false;
        }

        private void PopulateFoveatedSimulationInput(float frameDeltaTime, float cameraDistanceSq, float previousAccumulator)
        {
            if (!_foveatedSimulationInputNative.IsCreated || _foveatedSimulationInputNative.Length <= 0)
                return;

            _foveatedSimulationInputNative[0] = new FoveatedSimulationInput
            {
                FrameDeltaTime = Mathf.Max(0f, frameDeltaTime),
                CameraDistanceSq = Mathf.Max(0f, cameraDistanceSq),
                FullDistanceMeters = hibernationStartDistance,
                SleepDistanceMeters = simulationCullDistance,
                MaxStepSeconds = hibernationMaxStepSeconds,
                MinTimeScale = hibernationMinTimeScale,
                PreviousAccumulator = Mathf.Max(0f, previousAccumulator),
                Padding = 0f
            };
        }

        private void ScheduleFoveatedSimulationDecision(float frameDeltaTime, float cameraDistanceSq, float previousAccumulator)
        {
            if (_foveatedSimulationScheduled)
                return;

            if (!_foveatedSimulationInputNative.IsCreated ||
                !_foveatedSimulationBackNative.IsCreated ||
                _foveatedSimulationInputNative.Length <= 0 ||
                _foveatedSimulationBackNative.Length <= 0)
            {
                return;
            }

            PopulateFoveatedSimulationInput(frameDeltaTime, cameraDistanceSq, previousAccumulator);
            var job = new EvaluateSimulationLodJob
            {
                Input = _foveatedSimulationInputNative,
                Output = _foveatedSimulationBackNative
            };
            _foveatedSimulationHandle = job.Schedule();
            _foveatedSimulationScheduled = true;
        }

        private void CompletePendingFoveatedSimulationDecision(bool forceComplete)
        {
            if (!_foveatedSimulationScheduled)
                return;

            if (!forceComplete && !_foveatedSimulationHandle.IsCompleted)
                return;

            _foveatedSimulationHandle.Complete();
            _foveatedSimulationScheduled = false;
            (_foveatedSimulationFrontNative, _foveatedSimulationBackNative) = (_foveatedSimulationBackNative, _foveatedSimulationFrontNative);
        }

        private void DisposeFoveatedSimulationBuffers()
        {
            if (_foveatedSimulationScheduled)
            {
                DisposeNativeArrayDeferred(ref _foveatedSimulationInputNative, _foveatedSimulationHandle);
                DisposeNativeArrayDeferred(ref _foveatedSimulationFrontNative, _foveatedSimulationHandle);
                DisposeNativeArrayDeferred(ref _foveatedSimulationBackNative, _foveatedSimulationHandle);
                _foveatedSimulationScheduled = false;
                return;
            }

            DisposeNativeArray(ref _foveatedSimulationInputNative);
            DisposeNativeArray(ref _foveatedSimulationFrontNative);
            DisposeNativeArray(ref _foveatedSimulationBackNative);
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static float FloorToMultiple(float value, float multiple)
        {
            float safeMultiple = Mathf.Max(0.0001f, multiple);
            return Mathf.Floor(value / safeMultiple) * safeMultiple;
        }

        private static float HashToFloat01(uint index, uint iteration, uint salt)
        {
            uint value = index * 374761393u + iteration * 668265263u + salt + HashSeed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private float GetAbsoluteSimulationTime()
        {
            return _simulationPhaseOffset + _simulationTime;
        }

        private void WrapSimulationPhase()
        {
            if (_simulationTime < SimulationPhaseWrapSeconds)
                return;

            float wrappedDuration = Mathf.Floor(_simulationTime / SimulationPhaseWrapSeconds) * SimulationPhaseWrapSeconds;
            _simulationTime -= wrappedDuration;
            _simulationPhaseOffset += wrappedDuration;
        }

        private void CompletePendingLeviathanNodeBuild(bool forceComplete)
        {
            if (!_leviathanNodeBuildScheduled)
                return;

            if (!forceComplete && !_leviathanNodeBuildHandle.IsCompleted)
                return;

            _leviathanNodeBuildHandle.Complete();
            _leviathanNodeBuildScheduled = false;

            int safeCount = (_leviathanNodeCountNative.IsCreated && _leviathanNodeCountNative.Length > 0)
                ? Mathf.Clamp(_leviathanNodeCountNative[0], 0, _leviathanNodeBackNative.IsCreated ? _leviathanNodeBackNative.Length : 0)
                : 0;

            (_leviathanNodeFrontNative, _leviathanNodeBackNative) = (_leviathanNodeBackNative, _leviathanNodeFrontNative);
            _leviathanPathNodeCount = safeCount;
            _debugLeviathanNodeCount = safeCount;
            UploadActiveLeviathanSnapshot();
        }

        private void UploadActiveLeviathanSnapshot()
        {
            if (_leviathanNodeBuffer == null || !_leviathanNodeFrontNative.IsCreated || _leviathanPathNodeCount <= 0)
                return;

            int safeCount = Mathf.Clamp(_leviathanPathNodeCount, 0, _leviathanNodeFrontNative.Length);
            if (safeCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(_leviathanNodeBuffer, _leviathanNodeFrontNative, safeCount);
        }

        private void ClearLeviathanSnapshot()
        {
            _leviathanPathNodeCount = 0;
            _debugLeviathanNodeCount = 0;
        }

        private static void EnsureNativeArrayCapacity<T>(ref NativeArray<T> array, int requiredLength) where T : struct
        {
            if (requiredLength <= 0)
                return;

            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<T>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArrayDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose(dependency);
            array = default;
        }

        private bool ValidateGpuStructLayouts()
        {
            if (UnsafeUtility.SizeOf<BoidData>() != BoidDataStrideBytes ||
                UnsafeUtility.AlignOf<BoidData>() != BoidDataAlignmentBytes ||
                Marshal.OffsetOf<BoidData>(nameof(BoidData.Position)).ToInt32() != BoidDataPositionOffsetBytes ||
                Marshal.OffsetOf<BoidData>(nameof(BoidData.Velocity)).ToInt32() != BoidDataVelocityOffsetBytes ||
                Marshal.OffsetOf<BoidData>(nameof(BoidData.Panic)).ToInt32() != BoidDataPanicOffsetBytes ||
                Marshal.OffsetOf<BoidData>(nameof(BoidData.StateFlags)).ToInt32() != BoidDataStateFlagsOffsetBytes)
            {
                DisableComputeDispatch(
                    $"BoidData layout mismatch. Expected stride {BoidDataStrideBytes}, align {BoidDataAlignmentBytes}, offsets Position={BoidDataPositionOffsetBytes}, Velocity={BoidDataVelocityOffsetBytes}, Panic={BoidDataPanicOffsetBytes}, StateFlags={BoidDataStateFlagsOffsetBytes}.");
                return false;
            }

            if (UnsafeUtility.SizeOf<SimulationFrameConstants>() != SimulationFrameConstantsStride)
            {
                DisableComputeDispatch($"SimulationFrameConstants layout mismatch. Expected stride {SimulationFrameConstantsStride} bytes.");
                return false;
            }

            if (UnsafeUtility.SizeOf<GrazingAnchorData>() != GrazingAnchorStride ||
                UnsafeUtility.SizeOf<MassiveThreatData>() != MassiveThreatStride ||
                UnsafeUtility.SizeOf<FormationBeaconData>() != FormationBeaconStride ||
                UnsafeUtility.SizeOf<FormationObstacleData>() != FormationObstacleStride ||
                UnsafeUtility.SizeOf<LeviathanNodeData>() != LeviathanNodeStride)
            {
                DisableComputeDispatch("Ancillary GPU buffer layout mismatch. Expected explicit 4-byte packed strides for grazing anchors, massive threats, formation data, and leviathan nodes.");
                return false;
            }

            return true;
        }

        private static bool IsStaticFormationObstacleSemantic(HectonMapMagicVegetationBridge.VegetationSemanticType semanticType)
        {
            return semanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyCable ||
                   semanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyHullPlating ||
                   semanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam ||
                   semanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.DeadZoneMassiveStructure;
        }

        private bool EnsureComputeKernelBindings()
        {
            if (_computeDispatchDisabled)
                return false;

            if (boidCompute == null)
            {
                ResetComputeKernelBindings();
                return false;
            }

            if (!ReferenceEquals(_boundBoidCompute, boidCompute))
            {
                _computeDispatchDisabled = false;
                ResetComputeKernelBindings();
                _boundBoidCompute = boidCompute;
            }

            if (_computeKernelBindingsValid)
                return true;

            AssignManualKernelIndices();

            if (!TryValidateKernel(MainKernelName, _kernelIndex) ||
                !TryValidateKernel(ClearLatchStatsKernelName, _clearStatsKernelIndex) ||
                !TryValidateKernel(ClearSpatialGridKernelName, _clearSpatialGridKernelIndex) ||
                !TryValidateKernel(BuildSpatialGridKernelName, _buildSpatialGridKernelIndex) ||
                !TryValidateKernel(ClearPbdCorrectionsKernelName, _clearPbdCorrectionsKernelIndex) ||
                !TryValidateKernel(PbdSolveKernelName, _pbdSolveKernelIndex) ||
                !TryValidateKernel(ApplyOriginShiftKernelName, _applyOriginShiftKernelIndex))
            {
                _hasSpawnData = false;
                return false;
            }

            _threadGroupSizeX = ComputeThreadGroupSizeX;
            RefreshDispatchGroupCount();
            _computeKernelBindingsValid = true;
            return true;
        }

        private bool TryValidateKernel(string kernelName, int kernelIndex)
        {
            if (!boidCompute.HasKernel(kernelName))
            {
                DisableComputeDispatch($"Missing kernel '{kernelName}' on '{boidCompute.name}'.");
                return false;
            }

            try
            {
                boidCompute.GetKernelThreadGroupSizes(kernelIndex, out uint groupSizeX, out _, out _);
                if (groupSizeX == 0u)
                {
                    DisableComputeDispatch($"Kernel '{kernelName}' on '{boidCompute.name}' manual index {kernelIndex} reported thread group size 0.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                DisableComputeDispatch($"Kernel '{kernelName}' on '{boidCompute.name}' manual index {kernelIndex} failed validation. {exception.Message}");
                return false;
            }
        }

        private void AssignManualKernelIndices()
        {
            _kernelIndex = MainKernelIndex;
            _clearStatsKernelIndex = ClearLatchStatsKernelIndex;
            _clearSpatialGridKernelIndex = ClearSpatialGridKernelIndex;
            _buildSpatialGridKernelIndex = BuildSpatialGridKernelIndex;
            _clearPbdCorrectionsKernelIndex = ClearPbdCorrectionsKernelIndex;
            _pbdSolveKernelIndex = PbdSolveKernelIndex;
            _applyOriginShiftKernelIndex = ApplyOriginShiftKernelIndex;
        }

        private void ResetComputeKernelBindings()
        {
            _kernelIndex = -1;
            _clearStatsKernelIndex = -1;
            _clearPbdCorrectionsKernelIndex = -1;
            _pbdSolveKernelIndex = -1;
            _clearSpatialGridKernelIndex = -1;
            _buildSpatialGridKernelIndex = -1;
            _applyOriginShiftKernelIndex = -1;
            _threadGroupSizeX = ComputeThreadGroupSizeX;
            RefreshDispatchGroupCount();
            _computeKernelBindingsValid = false;
        }

        private void DisableComputeDispatch(string message)
        {
            if (_computeDispatchDisabled)
                return;

            _computeDispatchDisabled = true;
            ResetComputeKernelBindings();
            Debug.LogError($"SargassumMicroFaunaBoids compute dispatch disabled: {message}", this);
        }

        private void RenderStaticFallback(float cameraDistanceSq, float hibernation01)
        {
            bool shouldRender = ShouldRenderSwarm(cameraDistanceSq);
            _debugVisible = shouldRender;
            _debugHibernation01 = hibernation01;
            if (shouldRender)
                RenderCurrentBuffer();
        }

        private void DispatchOriginShiftToLiveBoidBuffers(Vector3 runtimeOffset)
        {
            if (boidCount <= 0 ||
                boidCompute == null ||
                _simulationFrameBuffer == null ||
                _boidsBufferA == null ||
                _boidsBufferB == null ||
                _computeDispatchDisabled ||
                !EnsureComputeKernelBindings())
            {
                return;
            }

            Vector4 shiftVector = new Vector4(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z, 0f);
            int dispatchGroups = Mathf.Max(1, Mathf.CeilToInt(boidCount / (float)Mathf.Max(1u, _threadGroupSizeX)));

            try
            {
                boidCompute.SetVector(_OriginShiftDeltaId, shiftVector);
                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferA);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferB);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);
            }
            catch (Exception exception)
            {
                DisableComputeDispatch($"Origin-shift dispatch failure on '{boidCompute.name}'. {exception.Message}");
            }
        }

        private void ApplyRuntimeOffsetToSwarmData(Vector3 runtimeOffset)
        {
            _fieldCenter += runtimeOffset;
            _renderBounds.center += runtimeOffset;
            _debugRenderBounds = _renderBounds;
            _threatGridCenterWS += runtimeOffset;
            _threatVoxelOriginWS += runtimeOffset;
            _leviathanHotspotWS += runtimeOffset;
            _debugLeviathanHotspotWS += runtimeOffset;
            _leviathanHeadPositionWS += runtimeOffset;
            _reportedWakeCenterWS += runtimeOffset;

            DispatchOriginShiftToLiveBoidBuffers(runtimeOffset);

            if (_grazingAnchors != null)
            {
                for (int i = 0; i < _activeGrazingAnchorCount; i++)
                    _grazingAnchors[i].Position += runtimeOffset;

                GraphicsBufferUploadUtility.UploadArray(_grazingAnchorBuffer, _grazingAnchors, _activeGrazingAnchorCount);
            }

            if (_massiveThreats != null)
            {
                for (int i = 0; i < _massiveThreats.Length; i++)
                    _massiveThreats[i].Position += runtimeOffset;

                GraphicsBufferUploadUtility.UploadArray(_massiveThreatBuffer, _massiveThreats, _activeMassiveThreatCount);
            }

            if (_formationBeacons != null)
            {
                for (int i = 0; i < _debugFormationBeaconCount; i++)
                    _formationBeacons[i].Position += runtimeOffset;

                GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);
            }

            if (_formationObstacles != null)
            {
                for (int i = 0; i < _debugFormationObstacleCount; i++)
                    _formationObstacles[i].Position += runtimeOffset;

                GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
            }

            if (_leviathanNodeFrontNative.IsCreated)
            {
                int frontCount = Mathf.Clamp(_leviathanPathNodeCount, 0, _leviathanNodeFrontNative.Length);
                for (int i = 0; i < frontCount; i++)
                {
                    LeviathanNodeData node = _leviathanNodeFrontNative[i];
                    node.Position += (float3)runtimeOffset;
                    _leviathanNodeFrontNative[i] = node;
                }

                GraphicsBufferUploadUtility.UploadNativeArray(_leviathanNodeBuffer, _leviathanNodeFrontNative, frontCount);
            }

            if (_leviathanNodeBackNative.IsCreated)
            {
                int backCount = Mathf.Clamp(_leviathanPathNodeCount, 0, _leviathanNodeBackNative.Length);
                for (int i = 0; i < backCount; i++)
                {
                    LeviathanNodeData node = _leviathanNodeBackNative[i];
                    node.Position += (float3)runtimeOffset;
                    _leviathanNodeBackNative[i] = node;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _editorValidateDepth++;
            if (_editorValidateDepth > MaxEditorValidateDepth)
            {
                _editorValidateDepth--;
                Debug.LogError("SargassumMicroFaunaBoids editor validation watchdog tripped.", this);
                return;
            }

            try
            {
                SanitizeSettings();
                ResetComputeKernelBindings();
                RefreshActiveBoidCount();
            }
            finally
            {
                _editorValidateDepth--;
            }
        }
#endif
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Ecosystem;
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
    public sealed class SargassumMicroFaunaBoids : MonoBehaviour, ITickable, IFixedTickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, Hecton8.Gameplay.IFlashlightEventListener, ISargassumGlobalDragEventListener, ISonarPingEventListener
    {
        private const int MaxLeviathanNodePathIterations = 4096;
        private const int WhileLoopWatchdogLimit = 10000;
        private const float FullSimulationDistanceMeters = 50f;
        private const float SleepSimulationDistanceMeters = 200f;
        private const float StatisticalDematerializeDistanceMeters = 200f;
        private const float StatisticalRematerializeDistanceMeters = 180f;
        private const float StatisticalDematerializeDistanceSq = StatisticalDematerializeDistanceMeters * StatisticalDematerializeDistanceMeters;
        private const float StatisticalRematerializeDistanceSq = StatisticalRematerializeDistanceMeters * StatisticalRematerializeDistanceMeters;
        private const int StatisticalMigrationKeepAliveSlowTickStride = 10;
        private const float StatisticalFibonacciGoldenAngle = 2.39996323f;
        private const float StatisticalTwoPi = 6.28318530718f;
        private const int PopulationDensityCellSizeMeters = 32;
        private const int PopulationDensityMinRadiusMeters = 4;
        private const int InactiveStatisticalSwarmRingCapacity = 16;
        private const int TargetBoidsPerGrazingAnchor = 48;
        private const float MinimumPopulationBudgetScale = 0.35f;
        private const string NativeMemoryOwner = nameof(SargassumMicroFaunaBoids);
        private const int ComputeDisableReasonDispatchFailure = 1;
        private const int ComputeDisableReasonBindingFailure = 2;
        private const int ComputeDisableReasonBoidLayoutMismatch = 3;
        private const int ComputeDisableReasonFrameLayoutMismatch = 4;
        private const int ComputeDisableReasonAncillaryLayoutMismatch = 5;
        private const int ComputeDisableReasonMissingKernel = 6;
        private const int ComputeDisableReasonZeroThreadGroup = 7;
        private const int ComputeDisableReasonKernelValidationFailure = 8;
        private const int ComputeDisableReasonOriginShiftFailure = 9;
#if UNITY_EDITOR
        private const int MaxEditorValidateDepth = 4;
        private static int _editorValidateDepth;
#endif

        internal static SargassumMicroFaunaBoids ActiveRuntimeInstance => GlobalRegistry.SargassumMicroFauna;

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

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
        internal struct BoidKillSignal
        {
            public float3 KillPositionWS;
            public float3 PredatorPositionWS;
            public int BoidId;
            public uint PredatorId;
            public float FearRadiusMeters;
            public float FearAmount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct FoodChainTelemetryEntry
        {
            public uint FrameIndex;
            public uint StateHash;
            public uint SourceHash;
            public uint Flags;
            public int ActiveBoidCount;
            public int ConsumedBoidCount;
            public int PendingKillJob;
            public int LodTier;
            public float3 FieldCenterWS;
            public float3 EventPositionWS;
            public uint AnomalyHash;
            public float SimulationTime;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
        internal struct PopulationDensityPoint
        {
            public int CenterCellId;
            public ushort Count;
            public byte Species;
            public byte RadiusMeters;
        }

        private struct NativeRingBuffer<T> : IDisposable where T : struct
        {
            private NativeArray<T> _items;
            private int _head;
            private int _count;

            public int Count => _count;
            public bool IsCreated => _items.IsCreated;

            public void EnsureCapacity(int capacity, string label)
            {
                if (capacity <= 0)
                    return;

                if (_items.IsCreated && _items.Length == capacity)
                    return;

                Dispose();
                _items = new NativeArray<T>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<T>[capacity] - persistent statistical swarm ring buffer - owner: SargassumMicroFaunaBoids
                NativeMemorySentinel.RegisterNativeArray(_items, NativeMemoryOwner, label, NativeAllocationLifetime.Scene);
                _head = 0;
                _count = 0;
            }

            public void PushOverwrite(in T value)
            {
                if (!_items.IsCreated || _items.Length <= 0)
                    return;

                _items[_head] = value;
                _head++;
                if (_head >= _items.Length)
                    _head = 0;
                _count = math.min(_count + 1, _items.Length);
            }

            public void Clear()
            {
                _head = 0;
                _count = 0;
            }

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                if (!_items.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(_items);
                _items.Dispose(dependency);
                _items = default;
                _head = 0;
                _count = 0;
            }
        }

        [Flags]
        private enum BoidStateFlags : uint
        {
            None = 0u,
            Active = 1u << 0,
            Hunting = 1u << 1,
            Fleeing = 1u << 2,
            Consumed = 1u << 3,
            AggressiveMutation = 1u << 4,
            VisualMutationResolved = 1u << 5
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct PredatorBoidConsumptionJob : IJob
        {
            [ReadOnly] public NativeArray<BoidData> Boids;
            public NativeQueue<BoidKillSignal>.ParallelWriter KillSignals;
            public float3 PredatorPositionWS;
            public float3 BiteCenterWS;
            public float BiteRangeSq;
            public float FearRadiusMeters;
            public float FearAmount;
            public uint PredatorId;
            public int ActiveBoidCount;
            public int MaxKills;

            public void Execute()
            {
                int safeCount = math.clamp(ActiveBoidCount, 0, Boids.Length);
                int emitted = 0;
                for (int i = 0; i < safeCount && emitted < MaxKills; i++)
                {
                    BoidData boid = Boids[i];
                    if ((boid.StateFlags & ConsumedBoidStateFlag) != 0u)
                        continue;

                    float3 boidPosition = new float3(boid.Position.x, boid.Position.y, boid.Position.z);
                    float3 delta = boidPosition - BiteCenterWS;
                    if (math.lengthsq(delta) > BiteRangeSq)
                        continue;

                    KillSignals.Enqueue(new BoidKillSignal
                    {
                        KillPositionWS = boidPosition,
                        PredatorPositionWS = PredatorPositionWS,
                        BoidId = i,
                        PredatorId = PredatorId,
                        FearRadiusMeters = FearRadiusMeters,
                        FearAmount = FearAmount
                    });
                    emitted++;
                }
            }
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
            public float CameraDistanceSq;
            public float Padding0;
            public float Padding1;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                float cameraDistanceSq = math.max(0f, input.CameraDistanceSq);
                float fullDistanceSq = fullDistanceMeters * fullDistanceMeters;
                float sleepDistanceSq = sleepDistanceMeters * sleepDistanceMeters;
                float safeMaxStepSeconds = math.max(1f / 60f, input.MaxStepSeconds);
                float safeMinTimeScale = math.clamp(input.MinTimeScale, 0.1f, 1f);
                float previousAccumulator = math.max(0f, input.PreviousAccumulator);
                decision.CameraDistanceSq = cameraDistanceSq;

                if (cameraDistanceSq > sleepDistanceSq)
                {
                    decision.Hibernation01 = 1f;
                    decision.Tier = (int)SimulationLodTier.Sleep;
                    Output[0] = decision;
                    return;
                }

                if (cameraDistanceSq <= fullDistanceSq)
                {
                    decision.Tier = (int)SimulationLodTier.Full;
                    decision.SimulationDeltaTime = safeFrameDeltaTime;
                    decision.DispatchSimulation = safeFrameDeltaTime > 0f ? 1 : 0;
                    Output[0] = decision;
                    return;
                }

                decision.Tier = (int)SimulationLodTier.Simplified;
                decision.Hibernation01 = math.saturate((cameraDistanceSq - fullDistanceSq) / math.max(0.01f, sleepDistanceSq - fullDistanceSq));
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

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 28)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildLeviathanNodeJob : IJob
        {
            [ReadOnly] public NativeArray<float3> SourcePath;
            public int SourceCount;
            public NativeArray<LeviathanNodeData> OutputNodes;
            public NativeArray<int> OutputCount;
            public float BodyRadius;

            private static float ApproxVisualSegmentLength(float3 a, float3 b)
            {
                float3 delta = math.abs(b - a);
                float maxAxis = math.cmax(delta);
                float minAxis = math.cmin(delta);
                float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
                return maxAxis + midAxis * 0.5f + minAxis * 0.25f;
            }

            private static float3 FastSafeDirection(float3 delta, float3 fallback)
            {
                return CheapNormalizeL1(delta, fallback);
            }

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
                    totalLength += ApproxVisualSegmentLength(SourcePath[i - 1], SourcePath[i]);

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
                        float segmentLength = ApproxVisualSegmentLength(SourcePath[pathCursor - 1], SourcePath[pathCursor]);
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
                        cumulativeDistance += ApproxVisualSegmentLength(OutputNodes[nodeIndex - 1].Position, nodePosition);

                    float3 tangent;
                    if (nodeIndex < targetCount - 1)
                        tangent = FastSafeDirection(OutputNodes[nodeIndex + 1].Position - nodePosition, new float3(0f, 0f, 1f));
                    else
                        tangent = FastSafeDirection(nodePosition - OutputNodes[math.max(0, nodeIndex - 1)].Position, new float3(0f, 0f, 1f));

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

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 768)]
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
            public float4 SubmarineWake0;
            public float4 SubmarineWake1;
            public float4 Ecosystem0;
            public float4 Fragmentation0;
            public float4 Fragmentation1;
            public float4 SonarScatter0;
            public float4 AcousticPanic0;
            public float4 AcousticPanic1;
            public float4 AbyssalFlowWeatherCurrent;
            public float4 PlayerDirection;
        }

        private const int BoidStride = 32;
        private const int GrazingAnchorStride = 32;
        private const int MassiveThreatStride = 48;
        private const int FormationBeaconStride = 32;
        private const int FormationObstacleStride = 32;
        private const int LeviathanNodeStride = 32;
        private const int PbdCorrectionScalarCount = 4;
        private const int PbdCorrectionRawStride = sizeof(int);
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
        private const uint ConsumedBoidStateFlag = (uint)BoidStateFlags.Consumed;
        private const uint BoidVisualMutationMask = (uint)(BoidStateFlags.AggressiveMutation | BoidStateFlags.VisualMutationResolved);
        private const int PredatorKillSignalDrainLimit = 8;
        private const float PredatorKillDefaultFearRadiusMeters = 10f;
        private const float PredatorKillFearDurationSeconds = 0.55f;
        private const float PredatorKillFearAmount = 100f;
        private const byte PredatorKillBloodDebrisKind = 2;
        private const byte PredatorKillDebrisFlags = 1;
        private const float PredatorKillFluidDecalRadiusScale = 0.28f;
        private const float PredatorKillSignalDrainLimitInv = 1f / PredatorKillSignalDrainLimit;
        private const float FeedingFrenzyWindowSeconds = 1f;
        private const int FeedingFrenzyKillThreshold = 5;
        private const byte FeedingFrenzyAcousticChannel = 5;
        private const byte FeedingFrenzyAcousticFlags = 1;
        private const float FeedingFrenzyAcousticRadiusMeters = 36f;
        private const int WhaleFallScavengerVisualCount = 96;
        private const float WhaleFallScavengerRadiusMeters = 14f;
        private const float WhaleFallScavengerGroundOffsetMeters = 0.08f;
        private const float WhaleFallScavengerTangentSpeedMetersPerSecond = 0.65f;
        private const float WhaleFallScavengerRadiusHashInv = 1f / 1023f;
        private const float WhaleFallScavengerAngleHashInv = 1f / 255f;
        private const int FoodChainTelemetryCapacity = 300;
        private const int FoodChainTelemetryEntrySizeBytes = 64;
        private const uint FoodChainTelemetryMagicLow = 0x48454354u;
        private const uint FoodChainTelemetryMagicHigh = 0x4643484Eu;
        private const uint FoodChainTelemetryFlagTick = 1u << 0;
        private const uint FoodChainTelemetryFlagKillJobScheduled = 1u << 1;
        private const uint FoodChainTelemetryFlagKillJobCompleted = 1u << 2;
        private const uint FoodChainTelemetryFlagKillDrained = 1u << 3;
        private const uint FoodChainTelemetryFlagWhaleFall = 1u << 4;
        private const uint FoodChainTelemetryFlagNonFinite = 1u << 31;
        private const uint FoodChainTelemetryAnomalyNonFinite = 0xEFC00001u;
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
        private const float SubmarineWakeMinimumSpeedMetersPerSecond = 1.5f;
        private const float SubmarineWakeBaseRadiusMeters = 14f;
        private const float SubmarineWakeMaxRadiusMeters = 34f;
        private const float SubmarineWakeRadiusSpeedScale = 0.85f;
        private const float SubmarineWakeBaseHalfLengthMeters = 18f;
        private const float SubmarineWakeMaxHalfLengthMeters = 55f;
        private const float SubmarineWakeHalfLengthSpeedScale = 1.6f;
        private const float LodDitherHibernationStart01 = 0.82f;
        private const float LodDitherHibernationInvRange = 1f / (1f - LodDitherHibernationStart01);
        private const uint HashSeed = 0x9E3779B9u;
        private const float SimulationPhaseWrapSeconds = 60f;
        private const int SimulationFrameConstantsStride = 768;
        private const int BoidDataPositionOffsetBytes = 0;
        private const int BoidDataVelocityOffsetBytes = 12;
        private const int BoidDataPanicOffsetBytes = 24;
        private const int BoidDataStrideBytes = 32;
        private const int BoidDataStateFlagsOffsetBytes = 28;
        private const int BoidDataAlignmentBytes = 4;
        private const uint ComputeThreadGroupSizeX = 64;
        private const float BoundsCubeSphereRadiusScale = 1.7320508f;
        private const float RenderConeCullNearDistanceSq = FullSimulationDistanceMeters * FullSimulationDistanceMeters;
        private const float RenderConeCullDotThreshold = -0.2f;
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
        private static readonly int _HitFlashStartTimeId = Shader.PropertyToID("_HitFlashStartTime");
        private static readonly int _HitFlashDurationId = Shader.PropertyToID("_HitFlashDuration");
        private static readonly int _HitFlashIntensityId = Shader.PropertyToID("_HitFlashIntensity");
        private static readonly int _HitFlashRadiusId = Shader.PropertyToID("_HitFlashRadius");
        private static readonly int _HitFlashBloatId = Shader.PropertyToID("_HitFlashBloat");
        private static readonly int _HitFlashOriginWSId = Shader.PropertyToID("_HitFlashOriginWS");
        private static readonly int _HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
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
        private static readonly int _LodDitherKeep01Id = Shader.PropertyToID("_LodDitherKeep01");
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
        private static readonly int _AbyssalFlowFieldTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
        private static readonly int _AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
        private static readonly int _AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
        private static readonly int _AbyssalFlowActiveId = Shader.PropertyToID("_AbyssalFlowActive");
        private static readonly int _AbyssalFlowWeightId = Shader.PropertyToID("_AbyssalFlowWeight");

        [Header("â”€â”€ Runtime Wiring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField]
        [Tooltip("Compute shader that simulates the micro-fauna flock.")]
        private ComputeShader boidCompute;

        [SerializeField]
        [Tooltip("Instanced mesh rendered for each micro-fauna boid.")]
        private Mesh boidMesh;

        [SerializeField]
        [Tooltip("Instanced material used by RenderMeshIndirect.")]
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

        [Header("── GPU Hit Reaction ────────────────")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds the GPU-only hit flash remains visible after a registered impact.")]
        private float hitFlashDurationSeconds = 0.1f;

        [SerializeField, Min(0f)]
        [Tooltip("World radius affected by a registered VAT hit reaction. Zero means the whole rendered school flashes.")]
        private float hitFlashRadiusMeters = 6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default intensity for GPU-only VAT hit flashes.")]
        private float hitFlashIntensity = 1f;

        [SerializeField, Range(0f, 0.12f)]
        [Tooltip("Local-normal bloat distance applied in the vertex shader while the hit flash is active.")]
        private float hitFlashBloatMeters = 0.035f;

        [SerializeField]
        [Tooltip("Color blended by the fragment shader while the hit flash is active.")]
        private Color hitFlashColor = new Color(1f, 0.08f, 0.04f, 1f);

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
        [Tooltip("Legacy voxel look-ahead retained for serialized scenes. Boid wall collision is purged for MX350-tier ghosting.")]
        private float voxelAvoidanceLookAheadDistance = 3.5f;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Legacy voxel avoidance weight retained for serialized scenes. Runtime clamps this to zero.")]
        private float voxelAvoidanceWeight = 0f;

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
        private LayerMask formationObstacleLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

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
        [Tooltip("Radius used when the leviathan emits a visual shockwave into the boid compute threat field.")]
        private float leviathanShockwaveRadius = 15f;

        [SerializeField, Range(2f, 96f)]
        [Tooltip("Visual shockwave intensity scalar. This never applies Rigidbody impulses.")]
        private float leviathanShockwaveImpulse = 18f;

        [SerializeField, Range(0.05f, 1.5f)]
        [Tooltip("Cooldown between consecutive visual shockwave pulses while the leviathan keeps sprinting.")]
        private float leviathanShockwaveCadence = 0.18f;

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
        [Tooltip("Current render bounds used by RenderMeshIndirect.")]
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
        [Tooltip("True when the ecosystem sector is running the apex-presence presentation fake.")]
        private bool _debugApexInSector;

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

        [SerializeField]
        [Tooltip("CPU-side consumed GPU boid count emitted by predator bite jobs this session.")]
        private int _debugConsumedBoidCount;

        private MaterialPropertyBlock _materialPropertyBlock;

        private BoidData[] _spawnData;
        private BoidData[] _singleBoidUpload;
        private GrazingAnchorData[] _grazingAnchors;
        private MassiveThreatData[] _massiveThreats;
        private FormationBeaconData[] _formationBeacons;
        private FormationObstacleData[] _formationObstacles;
        private HectonBiolumZone[] _deepBiolumZones;
        private float[] _deepBiolumZoneScores;
        private BeaconNetworkSystem.BeaconSnapshot[] _formationBeaconSnapshots;
        private Collider[] _formationObstacleColliders;
        private NativeArray<StaticObstacleData> _staticObstacleCache;
        private NativeArray<BoidData> _boidStateNative;
        private NativeQueue<BoidKillSignal> _killSignals;
        private NativeArray<FoodChainTelemetryEntry> _foodChainTelemetryRing;
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
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _boidIndirectArgsUpload =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1]; // COLD ALLOC: IndirectDrawIndexedArgs[1] - VAT micro-fauna indirect draw upload cache - owner: SargassumMicroFaunaBoids
        private GraphicsBuffer _boidsBufferA;
        private GraphicsBuffer _boidsBufferB;
        private GraphicsBuffer _boidIndirectArgsBuffer;
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
        private bool _latchStatsBufferRawTarget;
        private bool _pbdCorrectionBufferRawTarget;
        private bool _spatialGridCountBufferRawTarget;
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
        private Mesh _boidIndirectArgsMesh;
        private int _boidIndirectArgsInstanceCount = -1;
        private int _frameParity;
        private int _lastFieldRevision = -1;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _serviceRegistered;
        private bool _hasSpawnData;
        private bool _computeKernelBindingsValid;
        private bool _computeStaticBuffersBound;
        private bool _computeDispatchDisabled;
        private Texture _boundComputeDensityTexture;
        private Texture _boundComputeCutMaskTexture;
        private Texture _boundAbyssalFlowTexture;
        private Texture3D _fallbackAbyssalFlowTexture;
        private Vector3 _fieldCenter;
        private Vector3 _fieldExtents;
        private Vector3 _previousDriftOffset;
        private float _headlightPanicTimer;
        private bool _deepModeActive;
        private bool _lastSpawnModeDeep;
        private bool _lastDeepLeviathanMode;
        private float _simulationTime;
        private float _simulationPhaseOffset;
        private float _cachedVatSwayAmplitudeScale = 1f;
        private float _feedingFrenzyWindowStartTime = -1f;
        private int _feedingFrenzyKillCount;
        private JobHandle _predatorConsumptionHandle;
        private bool _predatorConsumptionJobPending;
        private bool _foodChainTelemetryDumped;
        private float _pendingPredatorConsumptionTimeSeconds;
        private int _foodChainTelemetryCursor;
        private Vector3 _hitFlashOriginWS;
        private float _hitFlashStartTime = -1000f;
        private float _hitFlashRuntimeRadius;
        private float _hitFlashRuntimeIntensity;
        private bool _hitFlashPropertiesDirty = true;
        private bool _renderPropertiesDirty = true;
        private GraphicsBuffer _renderPropertiesBoidBuffer;
        private Texture _renderPropertiesVatPositionTexture;
        private Texture _renderPropertiesVatNormalTexture;
        private float _renderPropertiesParasiteMode;
        private float _renderPropertiesParasiteAggression;
        private float _renderPropertiesVelocitySleepScale;
        private float _renderPropertiesLodDitherKeep01;
        private float _renderPropertiesVatEnabled;
        private float _renderPropertiesVatFrameCount;
        private float _renderPropertiesVatVertexCount;
        private float _renderPropertiesVatPlaybackSpeed;
        private float _renderPropertiesVatInstancePhaseScale;
        private float _renderPropertiesVatPositionScale;
        private float _renderPropertiesVatNormalBlend;
        private float _spatialGridCellSizeWS = 1f;
        private Vector3 _spatialGridOriginWS = Vector3.zero;
        private Vector3Int _spatialGridResolution = Vector3Int.one;
        private int _activeBoidCount;
        private int _migrationPopulationCount;
        private int _lastRequestedMigrationPopulationCount;
        private AbsoluteUniversePosition _registeredMigrationPopulationCenterAup;
        private int3 _registeredMigrationPopulationAupCell;
        private byte _registeredMigrationPopulationSpecies;
        private bool _registeredMigrationPopulationValid;
        private int _boidMeshVertexCount;
        private int _cachedRenderLayer;
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
        private bool _ecosystemApexInSector;
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
        private float _fragmentationHalfDistanceWS;
        private float _fragmentationStartTime = float.NegativeInfinity;
        private float _fragmentationExpireTime = float.NegativeInfinity;
        private Vector3 _sonarScatterOriginWS;
        private float _sonarScatterWaveFrontWS;
        private float _sonarScatterStrength01;
        private float _sonarScatterExpireTime = float.NegativeInfinity;
        private Vector3 _acousticPanicOriginWS;
        private float _acousticPanicRadiusWS;
        private float _acousticPanicStrength01;
        private float _acousticPanicExpireTime = float.NegativeInfinity;
        private uint _acousticPanicSeed;
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
        private int _viewPoseCacheFrame = -1;
        private bool _viewPoseCacheValid;
        private Vector3 _viewPoseCachePosition;
        private Vector3 _viewPoseCacheForward;
        private int _playerRuntimeSnapshotCacheFrame = -1;
        private bool _playerRuntimeSnapshotCacheValid;
        private PlayerMovementRuntimeState _playerRuntimeSnapshotMovement;
        private PlayerLookState _playerRuntimeSnapshotLook;
        private int _playerMotionCacheFrame = -1;
        private bool _playerMotionCacheValid;
        private Vector3 _playerMotionCachePosition;
        private Vector3 _playerMotionCacheVelocity;
        private bool _playerTransformProbeAttempted;
        private bool _viewCameraProbeAttempted;
        private bool _runtimeServiceProbeAttempted;
        private PopulationDensityPoint _statisticalPopulationPoint;
        private AbsoluteUniversePosition _statisticalPopulationCenterAup;
        private bool _statisticalPopulationActive;
        private int _statisticalPopulationBaseCount;
        private int _statisticalMigrationKeepAliveTickCountdown;
        private NativeRingBuffer<PopulationDensityPoint> _inactiveStatisticalSwarmRing;
        private NativeRingBuffer<AbsoluteUniversePosition> _inactiveStatisticalSwarmCenterRing;

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
            _computeDispatchDisabled = false;
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            _hitFlashPropertiesDirty = true;
            SanitizeSettings();
            RefreshRenderLayerCache();
            RefreshRenderScaleCache();
            ResetDependencyProbeCache();
            ResolveDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayload();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, ResolveCameraDistanceSq());
        }

        private void OnEnable()
        {
            _computeDispatchDisabled = false;
            InvalidateViewPoseCache();
            ResetDependencyProbeCache();
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            _hitFlashPropertiesDirty = true;
            RefreshRenderLayerCache();
            RefreshRenderScaleCache();
            ResolveDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayload();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, ResolveCameraDistanceSq());
            SargassumGlobalDragManager.Register(this);
            FlashlightEvents.Register(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            InvalidateViewPoseCache();
            ResetDependencyProbeCache();
            TryUnregisterService();
            SargassumGlobalDragManager.Unregister(this);
            FlashlightEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
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
            ClearStatisticalPopulationPoint();
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
            _acousticPanicExpireTime = float.NegativeInfinity;
            _acousticPanicRadiusWS = 0f;
            _acousticPanicStrength01 = 0f;
            _acousticPanicOriginWS = Vector3.zero;
            _acousticPanicSeed = 0u;
            ResetThreatVoxelSnapshot();
            _lastDeepLeviathanMode = false;
            TryUnregister();
            ClearStatisticalPopulationPoint();
            CompletePendingReadbackAndReleaseBuffers();
        }

        private void OnDestroy()
        {
            ResetDependencyProbeCache();
            TryUnregisterService();
            SargassumGlobalDragManager.Unregister(this);
            FlashlightEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ClearStatisticalPopulationPoint();
            CompletePendingReadbackAndReleaseBuffers();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            if (shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            InvalidateViewPoseCache();
            ApplyRuntimeOffsetToSwarmData(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Runs GPU flocking and issues one indirect draw call when the field is valid.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            RecordFoodChainTelemetry(FoodChainTelemetryFlagTick, _fieldCenter, 0u, 0u);

            if (_statisticalPopulationActive)
            {
                _debugVisible = false;
                _debugHibernation01 = 1f;
                return;
            }

            ResolveDependencies();

            if (!_hasSpawnData || boidMaterial == null || boidMesh == null)
                return;

            if (_activeBoidCount <= 0)
            {
                _debugVisible = false;
                _debugHibernation01 = 1f;
                return;
            }

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
            float deltaTime = math.max(0f, dt);
            float leviathanBlendTarget = _leviathanModeActive ? 1f : 0f;
            float leviathanBlendT = ResolveDecayBlend(math.max(leviathanModeBlendSharpness, 0.01f), deltaTime);
            _leviathanModeBlend = math.lerp(_leviathanModeBlend, leviathanBlendTarget, leviathanBlendT);
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
            UpdateMassiveThreats();
            UpdateParasiteLatchReadback(deltaTime);
            float hibernation01 = 0f;
            bool shouldRender = ShouldRenderSwarm(cameraDistanceSq);
            bool dispatchedSimulation = TryConsumeSimulationStep(
                deltaTime,
                cameraDistanceSq,
                out float simulationDeltaTime,
                out hibernation01,
                out SimulationLodTier simulationLodTier);
            bool shouldDispatchSleepVelocityWrite = simulationLodTier == SimulationLodTier.Sleep && _sleepVelocityWritePending;
            bool shouldDispatchSimulation = dispatchedSimulation && (shouldRender || ShouldMaintainOffscreenBoidSimulation(simulationLodTier));
            bool leaderFollowerSchooling = _formationModeActive && !_parasiteModeActive && _leviathanModeBlend < 0.001f;
            bool shouldCollectLatchStats = ShouldCollectLatchStats(simulationLodTier, leaderFollowerSchooling);
            if (shouldDispatchSimulation || shouldDispatchSleepVelocityWrite)
            {
                if (simulationLodTier == SimulationLodTier.Full && !leaderFollowerSchooling)
                    UpdateSpatialGridLayout();

                if (BindSimulationUniforms(simulationDeltaTime, currentDriftOffset, driftDelta, hibernation01, simulationLodTier, shouldRender))
                {
                    try
                    {
                        if (shouldCollectLatchStats)
                            DispatchClearLatchStats();

                        if (simulationLodTier == SimulationLodTier.Full && !leaderFollowerSchooling)
                        {
                            DispatchClearSpatialGrid();
                            DispatchClearPbdCorrections();
                            boidCompute.Dispatch(_buildSpatialGridKernelIndex, _dispatchGroupCount, 1, 1);
                            boidCompute.Dispatch(_pbdSolveKernelIndex, _dispatchGroupCount, 1, 1);
                        }

                        boidCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
                        if (simulationLodTier == SimulationLodTier.Sleep)
                            _sleepVelocityWritePending = false;
                        if (shouldCollectLatchStats)
                            TryRequestParasiteLatchReadback();

                        _frameParity ^= 1;
                    }
                    catch (Exception)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
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
            RefreshRenderLayerCache();
            RefreshRenderScaleCache();
            if (_statisticalPopulationActive)
            {
                float statisticalCameraDistanceSq = ResolveCameraDistanceSq();
                RefreshStatisticalMigrationPopulation(force: false);
                TryRematerializeStatisticalPopulation(statisticalCameraDistanceSq);
                return;
            }

            ResetDependencyProbeCache();
            ResolveDependencies();

            float cameraDistanceSq = ResolveCameraDistanceSq();
            if (TryDematerializeStatisticalPopulation(cameraDistanceSq))
                return;

            RefreshThreatVoxelPayload();
            bool populationBudgetChanged = RefreshActiveBoidCount();
            RefreshSpawnData(force: populationBudgetChanged);
        }

        /// <summary>
        /// Publishes completed CPU-side simulation decision jobs in the dispatcher-owned late-frame window.
        /// </summary>
        public void LateFrameTick()
        {
            CompletePendingLeviathanNodeBuild(forceComplete: false);
            CompletePendingFoveatedSimulationDecision(forceComplete: false);
            CompletePendingPredatorConsumption(forceComplete: false);
        }

        /// <summary>
        /// Applies fixed-step leviathan strikes and shockwave pushes using the cached head pose resolved during Tick.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed delta supplied by GameTickManager.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.max(0f, fixedDeltaTime);
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

                UpdateLeviathanPhysicalState(math.max(safeFixedDeltaTime, 0.0001f));
            ApplyParasiteHullStress();
            ApplyParasiteEnvironmentalDrag();
            if (!_leviathanModeActive || !_leviathanHeadValid || _leviathanModeBlend < 0.5f)
                return;

            ApplyLeviathanPhysicalStrike();
            ApplyLeviathanShockwave();
        }

        private void ResolveDependencies()
        {
            bool missingRuntimeServices = biolumManager == null ||
                                          dragManager == null ||
                                          cutManager == null ||
                                          _worldZoneDirector == null ||
                                          _biomeMatrixDirector == null ||
                                          _mapMagicVegetationBridge == null;
            if (!_runtimeServiceProbeAttempted && missingRuntimeServices)
            {
                if (biolumManager == null)
                    biolumManager = Hecton8.Core.GlobalRegistry.BiolumManager;

                if (dragManager == null)
                    dragManager = Hecton8.Core.GlobalRegistry.SargassumDrag;

                if (cutManager == null)
                    cutManager = Hecton8.Core.GlobalRegistry.SargassumCut;

                if (_worldZoneDirector == null)
                    _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

                if (_biomeMatrixDirector == null)
                    _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

                if (_mapMagicVegetationBridge == null)
                    _mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

                _runtimeServiceProbeAttempted = true;
            }
            else if (!missingRuntimeServices)
            {
                _runtimeServiceProbeAttempted = true;
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.IsBound)
            {
                playerTransform ??= runtimeContext.PlayerTransform;
                _playerRigidbody ??= runtimeContext.PlayerRigidbody;
                _playerMovement ??= runtimeContext.PlayerMovement;
                _playerTransportCoordinator ??= runtimeContext.PlayerTransportCoordinator;
                _playerHealth ??= runtimeContext.PlayerHealth;
                _playerFlashlight ??= runtimeContext.Flashlight;
            }
            else
            {
                IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
                if (playerContext != null && playerContext.IsInitialized)
                {
                    playerTransform ??= playerContext.PlayerTransform;
                    _playerRigidbody ??= playerContext.PlayerRigidbody;
                    _playerMovement ??= playerContext.PlayerMovement;
                    _playerTransportCoordinator ??= playerContext.PlayerTransportCoordinator;
                    _playerHealth ??= playerContext.PlayerHealth;
                    _playerFlashlight ??= playerContext.Flashlight;
                }
            }

            if (playerTransform == null && !_playerTransformProbeAttempted)
            {
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
                _playerTransformProbeAttempted = true;
            }
            else if (playerTransform != null)
            {
                _playerTransformProbeAttempted = true;
            }

            if (viewCamera == null && playerTransform != null && !_viewCameraProbeAttempted)
            {
                viewCamera = ComponentReferenceUtility.ResolveOwnedComponent<Camera>(playerTransform);
                _viewCameraProbeAttempted = true;
            }
            else if (viewCamera != null)
            {
                _viewCameraProbeAttempted = true;
            }

            if (_playerFlashlight != null)
                _flashlightOn = _playerFlashlight.IsOn;
        }

        private void ResetDependencyProbeCache()
        {
            _playerTransformProbeAttempted = false;
            _viewCameraProbeAttempted = false;
            _runtimeServiceProbeAttempted = false;
        }

        private void SanitizeSettings()
        {
            boidCount = math.clamp(boidCount, 128, 2048);
            boidCount = VRAMEnforcer.ApplyBoidPopulationBudget(boidCount, 128, 2048);
            maxSpawnAttempts = math.clamp(maxSpawnAttempts, 4, 32);
            densityThreshold = math.saturate(densityThreshold);
            windowThreshold = math.clamp(windowThreshold, 0f, 0.75f);
            cruiseSpeed = math.max(0.1f, cruiseSpeed);
            maxSpeed = math.max(cruiseSpeed, maxSpeed);
            panicSpeedBoost = math.max(0f, panicSpeedBoost);
            perceptionRadius = math.max(0.25f, perceptionRadius);
            separationRadius = math.clamp(separationRadius, 0.1f, perceptionRadius);
            boidBodyRadius = math.clamp(boidBodyRadius, 0.02f, separationRadius * 0.5f);
            consumedCollapseSpeed = math.clamp(consumedCollapseSpeed, 2f, 24f);
            gradientWorldStep = math.max(0.05f, gradientWorldStep);
            waterLevel = math.max(0f, waterLevel);
            minDepthBelowSurface = math.max(0.1f, minDepthBelowSurface);
            maxDepthBelowSurface = math.max(minDepthBelowSurface + 0.1f, maxDepthBelowSurface);
            panicThreshold = math.saturate(panicThreshold);
            panicDecay = math.max(0.1f, panicDecay);
            grazingAnchorCount = math.clamp(grazingAnchorCount, 4, 96);
            grazingRadius = math.clamp(grazingRadius, 0.25f, 6f);
            grazingWeight = math.clamp(grazingWeight, 0f, 4f);
            canopyAffinityWeight = math.clamp(canopyAffinityWeight, 0f, 4f);
            grazingDensityThreshold = math.saturate(grazingDensityThreshold);
            grazingRestSpeedScale = math.clamp(grazingRestSpeedScale, 0.05f, 0.6f);
            grazingRestHoldThreshold = math.saturate(grazingRestHoldThreshold);
            panicPlayerSpeedThreshold = math.clamp(panicPlayerSpeedThreshold, 0.5f, 8f);
            panicPlayerRadius = math.clamp(panicPlayerRadius, 0.5f, 12f);
            cameraAvoidRadius = math.clamp(cameraAvoidRadius, 0.25f, 3f);
            cameraAvoidWeight = math.clamp(cameraAvoidWeight, 0f, 8f);
            voxelAvoidanceLookAheadDistance = math.clamp(voxelAvoidanceLookAheadDistance, 0.25f, 12f);
            voxelAvoidanceWeight = 0f;
            maxMassiveThreatCount = math.clamp(maxMassiveThreatCount, 1, 8);
            massiveThreatPanicRadius = math.clamp(massiveThreatPanicRadius, 50f, 96f);
            massiveThreatWeight = math.clamp(massiveThreatWeight, 0f, 12f);
            deepBiolumAnchorCapacity = math.clamp(deepBiolumAnchorCapacity, 1, 16);
            deepBiolumSearchRadius = math.clamp(deepBiolumSearchRadius, 10f, 250f);
            deepBaitBallRadius = math.clamp(deepBaitBallRadius, 0.5f, 12f);
            deepBaitBallHeight = math.clamp(deepBaitBallHeight, 0.25f, 8f);
            deepClusterWeight = math.clamp(deepClusterWeight, 0f, 8f);
            deepHeadlightPanicDuration = math.clamp(deepHeadlightPanicDuration, 0.1f, 10f);
            deepHeadlightPanicRadiusScale = math.clamp(deepHeadlightPanicRadiusScale, 1f, 6f);
            boidVatFrameCount = math.max(1, boidVatFrameCount);
            boidVatPlaybackSpeed = math.max(0f, boidVatPlaybackSpeed);
            boidVatInstancePhaseScale = math.max(0f, boidVatInstancePhaseScale);
            boidVatPositionScale = math.max(0.0001f, boidVatPositionScale);
            boidVatNormalBlend = math.saturate(boidVatNormalBlend);
            hitFlashDurationSeconds = math.max(0.01f, hitFlashDurationSeconds);
            hitFlashRadiusMeters = math.max(0f, hitFlashRadiusMeters);
            hitFlashIntensity = math.saturate(hitFlashIntensity);
            hitFlashBloatMeters = math.clamp(hitFlashBloatMeters, 0f, 0.12f);
            parasiteDroneWorldYThreshold = math.clamp(parasiteDroneWorldYThreshold, -4000f, -1000f);
            parasiteAffinityWeight = math.clamp(parasiteAffinityWeight, 0f, 12f);
            parasiteHullStressIntensity = math.saturate(parasiteHullStressIntensity);
            parasiteHullStressLightBoost = math.saturate(parasiteHullStressLightBoost);
            parasiteLatchRadius = math.clamp(parasiteLatchRadius, 0.5f, 8f);
            parasiteMaxLatchedDronesForFullDrag = math.clamp(parasiteMaxLatchedDronesForFullDrag, 1, 96);
            parasiteMaxEnvironmentalDragMultiplier = math.clamp(parasiteMaxEnvironmentalDragMultiplier, 1f, 4f);
            parasiteLatchReadbackInterval = math.clamp(parasiteLatchReadbackInterval, 0.05f, 0.5f);
            parasiteHarvesterLatchThreshold = math.clamp(parasiteHarvesterLatchThreshold, 1, 32);
            parasiteHarvesterFullLatchCount = math.clamp(parasiteHarvesterFullLatchCount, parasiteHarvesterLatchThreshold, 96);
            formationBeaconCapacity = math.clamp(formationBeaconCapacity, 1, 8);
            formationBeaconSearchRadius = math.clamp(formationBeaconSearchRadius, 8f, 250f);
            formationWeight = math.clamp(formationWeight, 0f, 8f);
            formationRingThickness = math.clamp(formationRingThickness, 0.1f, 12f);
            formationPulseAmplitude = math.clamp(formationPulseAmplitude, 0f, 2f);
            formationPulseSpeed = math.clamp(formationPulseSpeed, 0.1f, 4f);
            formationBreakPanicThreshold = math.saturate(formationBreakPanicThreshold);
            formationObstacleCapacity = math.clamp(formationObstacleCapacity, 1, 16);
            formationObstacleSearchRadius = math.clamp(formationObstacleSearchRadius, 4f, 80f);
            formationObstacleWeight = math.clamp(formationObstacleWeight, 0f, 8f);
            leviathanNodeCapacity = math.clamp(leviathanNodeCapacity, 8, 64);
            leviathanThreatThreshold = math.saturate(leviathanThreatThreshold);
            leviathanHotspotMinDistance = math.clamp(leviathanHotspotMinDistance, 10f, 200f);
            leviathanHotspotMaxDistance = math.clamp(leviathanHotspotMaxDistance, leviathanHotspotMinDistance, 400f);
            leviathanBodyWeight = math.clamp(leviathanBodyWeight, 0f, 8f);
            leviathanForwardWeight = math.clamp(leviathanForwardWeight, 0f, 8f);
            leviathanBodyRadius = math.clamp(leviathanBodyRadius, 0.5f, 12f);
            leviathanWaveAmplitude = math.clamp(leviathanWaveAmplitude, 0f, 2f);
            leviathanWaveFrequency = math.clamp(leviathanWaveFrequency, 0.1f, 6f);
            leviathanSurroundThreatThreshold = math.clamp(leviathanSurroundThreatThreshold, 0.6f, 1f);
            leviathanSurroundRadius = math.clamp(leviathanSurroundRadius, 4f, 48f);
            leviathanSurroundWeight = math.clamp(leviathanSurroundWeight, 0f, 8f);
            leviathanSurroundSpinSpeed = math.clamp(leviathanSurroundSpinSpeed, 0.1f, 4f);
            leviathanModeBlendSharpness = math.clamp(leviathanModeBlendSharpness, 0.1f, 12f);
            simulationCullDistance = SleepSimulationDistanceMeters;
            hibernationStartDistance = FullSimulationDistanceMeters;
            hibernationMaxStepSeconds = math.clamp(hibernationMaxStepSeconds, 1f / 60f, 0.5f);
            hibernationMinTimeScale = math.clamp(hibernationMinTimeScale, 0.1f, 1f);
            leviathanStrikeRadius = math.clamp(leviathanStrikeRadius, 1f, 24f);
            leviathanStrikeTraumaWeight = math.saturate(leviathanStrikeTraumaWeight);
            leviathanStrikeImpulse = math.clamp(leviathanStrikeImpulse, 1f, 120f);
            leviathanStrikeDamage = math.clamp(leviathanStrikeDamage, 0.1f, 100f);
            leviathanStrikeCooldown = math.clamp(leviathanStrikeCooldown, 0.05f, 2f);
            leviathanShockwaveSpeedThreshold = math.clamp(leviathanShockwaveSpeedThreshold, 2f, 40f);
            leviathanShockwaveRadius = math.clamp(leviathanShockwaveRadius, 2f, 32f);
            leviathanShockwaveImpulse = math.clamp(leviathanShockwaveImpulse, 2f, 96f);
            leviathanShockwaveCadence = math.clamp(leviathanShockwaveCadence, 0.05f, 1.5f);
            _activeBoidCount = math.clamp(_activeBoidCount <= 0 ? boidCount : _activeBoidCount, 128, boidCount);
        }

        private void EnsureBuffers()
        {
            _boidMeshVertexCount = boidMesh != null ? boidMesh.vertexCount : 0;

            if (_spawnData == null || _spawnData.Length != boidCount)
            {
                // COLD ALLOC: BoidData[boidCount] - CPU staging array for deterministic spawn uploads - owner: SargassumMicroFaunaBoids
                _spawnData = new BoidData[boidCount];
            }

            if (_singleBoidUpload == null || _singleBoidUpload.Length != 1)
            {
                // COLD ALLOC: BoidData[1] - single-index consumed boid GPU patch upload cache - owner: SargassumMicroFaunaBoids
                _singleBoidUpload = new BoidData[1];
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
                _formationObstacleColliders = new Collider[math.max(2, formationObstacleCapacity * 2)];
            }

            bool buffersChanged = false;
            buffersChanged |= EnsureBuffer(ref _boidsBufferA, boidCount, BoidStride);
            buffersChanged |= EnsureBuffer(ref _boidsBufferB, boidCount, BoidStride);
            buffersChanged |= EnsureBuffer(ref _grazingAnchorBuffer, grazingAnchorCount, GrazingAnchorStride);
            buffersChanged |= EnsureBuffer(ref _massiveThreatBuffer, maxMassiveThreatCount, MassiveThreatStride);
            buffersChanged |= EnsureBuffer(ref _formationBeaconBuffer, formationBeaconCapacity, FormationBeaconStride);
            buffersChanged |= EnsureBuffer(ref _formationObstacleBuffer, formationObstacleCapacity, FormationObstacleStride);
            buffersChanged |= EnsureBuffer(ref _leviathanNodeBuffer, leviathanNodeCapacity, LeviathanNodeStride);
            buffersChanged |= EnsureRawBuffer(ref _latchStatsBuffer, ref _latchStatsBufferRawTarget, LatchStatsElementCount, LatchStatsStride);
            buffersChanged |= EnsureRawBuffer(ref _pbdCorrectionBuffer, ref _pbdCorrectionBufferRawTarget, boidCount * PbdCorrectionScalarCount, PbdCorrectionRawStride);
            buffersChanged |= EnsureBufferCapacity(ref _threatGridBuffer, math.max(1, _threatGridCellCount), ThreatGridStride);
            buffersChanged |= EnsureBuffer(ref _threatVoxelBuffer, math.max(1, _threatVoxelCellCount), ThreatVoxelStride);
            buffersChanged |= EnsureRawBuffer(ref _spatialGridCountBuffer, ref _spatialGridCountBufferRawTarget, SpatialGridMaxCellCount, SpatialGridCountStride);
            buffersChanged |= EnsureBuffer(ref _spatialGridCellBuffer, SpatialGridMaxCellCount * SpatialGridMaxBoidsPerCell, SpatialGridCellEntryStride);
            buffersChanged |= EnsureBuffer(ref _simulationFrameBuffer, 1, SimulationFrameConstantsStride);
            EnsureFallbackAbyssalFlowTexture();
            if (buffersChanged)
                _computeStaticBuffersBound = false;
            EnsureNativeArrayCapacity(ref _staticObstacleCache, math.max(formationObstacleCapacity * 8, formationObstacleCapacity), nameof(_staticObstacleCache));
            EnsureNativeArrayCapacity(ref _boidStateNative, boidCount, nameof(_boidStateNative));
            EnsureNativeArrayCapacity(ref _leviathanNodeFrontNative, leviathanNodeCapacity, nameof(_leviathanNodeFrontNative));
            EnsureNativeArrayCapacity(ref _leviathanNodeBackNative, leviathanNodeCapacity, nameof(_leviathanNodeBackNative));
            EnsureNativeArrayCapacity(ref _leviathanNodeCountNative, 1, nameof(_leviathanNodeCountNative));
            EnsureNativeArrayCapacity(ref _foveatedSimulationInputNative, 1, nameof(_foveatedSimulationInputNative));
            EnsureNativeArrayCapacity(ref _foveatedSimulationFrontNative, 1, nameof(_foveatedSimulationFrontNative));
            EnsureNativeArrayCapacity(ref _foveatedSimulationBackNative, 1, nameof(_foveatedSimulationBackNative));
            EnsureNativeArrayCapacity(ref _simulationFrameNative, 1, nameof(_simulationFrameNative));
            EnsureNativeArrayCapacity(ref _foodChainTelemetryRing, FoodChainTelemetryCapacity, nameof(_foodChainTelemetryRing));
            _inactiveStatisticalSwarmRing.EnsureCapacity(InactiveStatisticalSwarmRingCapacity, nameof(_inactiveStatisticalSwarmRing));
            _inactiveStatisticalSwarmCenterRing.EnsureCapacity(InactiveStatisticalSwarmRingCapacity, nameof(_inactiveStatisticalSwarmCenterRing));
            if (!_killSignals.IsCreated)
            {
                _killSignals = new NativeQueue<BoidKillSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BoidKillSignal>[8] - predator bite job lane drained in late-frame swap - owner: SargassumMicroFaunaBoids
                NativeMemorySentinel.RegisterNativeQueue(
                    _killSignals,
                    PredatorKillSignalDrainLimit,
                    NativeMemoryOwner,
                    nameof(_killSignals),
                    NativeAllocationLifetime.Scene);
                PrewarmQueue(ref _killSignals, PredatorKillSignalDrainLimit);
            }

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
            int previousMigrationPopulationCount = _migrationPopulationCount;
            if (TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount))
            {
                float populationBudgetScale = ResolvePopulationBudgetScale();
                int budgetCap = math.clamp(RoundToIntPositive(boidCount * populationBudgetScale), 0, boidCount);
                _lastRequestedMigrationPopulationCount = math.max(0, ecosystemPopulationCount);
                byte species = ResolvePopulationSpeciesByte();
                int migrationPopulationCount = RegisterMigrationPopulationAndTrack(species, _fieldCenter, ecosystemPopulationCount);
                int visibleBoidCount = MigrationDirector.ResolveVisibleBoidCountFromMigrationPopulation(migrationPopulationCount);
                _migrationPopulationCount = math.max(0, migrationPopulationCount);
                _activeBoidCount = math.clamp(math.min(visibleBoidCount, budgetCap), 0, boidCount);
                _debugPopulationBudgetScale = boidCount > 0 ? (_activeBoidCount / (float)boidCount) : 0f;
            }
            else
            {
                float populationBudgetScale = ResolvePopulationBudgetScale();
                int budgetPopulationCount = RoundToIntPositive(boidCount * populationBudgetScale);
                _lastRequestedMigrationPopulationCount = math.max(0, budgetPopulationCount);
                byte species = ResolvePopulationSpeciesByte();
                int migrationPopulationCount = RegisterMigrationPopulationAndTrack(species, _fieldCenter, budgetPopulationCount);
                int visibleBoidCount = MigrationDirector.ResolveVisibleBoidCountFromMigrationPopulation(migrationPopulationCount);
                _migrationPopulationCount = math.max(0, migrationPopulationCount);
                _activeBoidCount = math.clamp(visibleBoidCount, 0, boidCount);
                _debugPopulationBudgetScale = boidCount > 0 ? (_activeBoidCount / (float)boidCount) : 0f;
            }

            _debugActiveBoidCount = _activeBoidCount;
            RefreshDispatchGroupCount();
            return previousActiveBoidCount != _activeBoidCount ||
                   previousMigrationPopulationCount != _migrationPopulationCount;
        }

        private int ResolveActiveBoidUploadCount()
        {
            return math.clamp(_activeBoidCount, 0, boidCount);
        }

        private void UploadSpawnDataToBoidBuffers(int uploadCount)
        {
            int safeUploadCount = math.clamp(uploadCount, 0, boidCount);
            if (safeUploadCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadArray(_boidsBufferA, _spawnData, safeUploadCount);
            GraphicsBufferUploadUtility.UploadArray(_boidsBufferB, _spawnData, safeUploadCount);
            SyncBoidStateNativeFromSpawnData(safeUploadCount);
        }

        private void SyncBoidStateNativeFromSpawnData(int uploadCount)
        {
            if (!_boidStateNative.IsCreated || _spawnData == null)
                return;

            int safeUploadCount = math.clamp(uploadCount, 0, math.min(_spawnData.Length, _boidStateNative.Length));
            for (int i = 0; i < safeUploadCount; i++)
                _boidStateNative[i] = _spawnData[i];

            _debugConsumedBoidCount = 0;
            _feedingFrenzyWindowStartTime = -1f;
            _feedingFrenzyKillCount = 0;
        }

        private int ResolveTargetGrazingAnchorCount(int boidPopulation)
        {
            int safePopulation = math.clamp(boidPopulation, 0, boidCount);
            if (safePopulation <= 0 || grazingAnchorCount <= 0)
                return 0;

            return math.clamp(CeilDivPositive(safePopulation, TargetBoidsPerGrazingAnchor), 1, grazingAnchorCount);
        }

        private void UploadGrazingAnchors()
        {
            _activeGrazingAnchorCount = math.clamp(_activeGrazingAnchorCount, 0, grazingAnchorCount);
            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
            if (_activeGrazingAnchorCount <= 0 || _grazingAnchorBuffer == null || _grazingAnchors == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(_grazingAnchorBuffer, _grazingAnchors, _activeGrazingAnchorCount);
        }

        private void UploadFormationBeacons()
        {
            _debugFormationBeaconCount = math.clamp(_debugFormationBeaconCount, 0, formationBeaconCapacity);
            if (_debugFormationBeaconCount <= 0 || _formationBeaconBuffer == null || _formationBeacons == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(_formationBeaconBuffer, _formationBeacons, _debugFormationBeaconCount);
        }

        private void UploadFormationObstacles()
        {
            _debugFormationObstacleCount = math.clamp(_debugFormationObstacleCount, 0, formationObstacleCapacity);
            if (_debugFormationObstacleCount <= 0 || _formationObstacleBuffer == null || _formationObstacles == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(_formationObstacleBuffer, _formationObstacles, _debugFormationObstacleCount);
        }

        private void UploadMassiveThreats()
        {
            _activeMassiveThreatCount = math.clamp(_activeMassiveThreatCount, 0, maxMassiveThreatCount);
            _debugMassiveThreatCount = _activeMassiveThreatCount;
            if (_activeMassiveThreatCount <= 0 || _massiveThreatBuffer == null || _massiveThreats == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(_massiveThreatBuffer, _massiveThreats, _activeMassiveThreatCount);
        }

        private bool TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount)
        {
            ecosystemPopulationCount = 0;
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized)
            {
                _ecosystemFitness = 0f;
                _ecosystemSpeedMultiplier = 1f;
                _ecosystemCamouflageIndex = 0f;
                _ecosystemApexInSector = false;
                _debugApexInSector = false;
                return false;
            }

            if (!ecosystemDirector.TryGetSectorPopulation(_fieldCenter, out EcosystemSectorPopulationSample sample))
            {
                _ecosystemFitness = 0f;
                _ecosystemSpeedMultiplier = 1f;
                _ecosystemCamouflageIndex = 0f;
                _ecosystemApexInSector = false;
                _debugApexInSector = false;
                return false;
            }

            _ecosystemApexInSector = sample.ApexInSector;
            ecosystemPopulationCount = math.max(0, sample.ApexInSector ? sample.PreyPopulation >> 2 : sample.PreyPopulation);
            _ecosystemFitness = math.saturate(sample.Fitness);
            _ecosystemSpeedMultiplier = math.max(0.25f, sample.SpeedMultiplier) * (sample.ApexInSector ? 1.25f : 1f);
            _ecosystemCamouflageIndex = math.saturate(sample.CamouflageIndex + (sample.ApexInSector ? 0.35f : 0f));
            _debugEcosystemFitness = _ecosystemFitness;
            _debugEcosystemCamouflageIndex = _ecosystemCamouflageIndex;
            _debugApexInSector = _ecosystemApexInSector;
            return true;
        }

        private float ResolvePopulationBudgetScale()
        {
            WorldProceduralScatterDirector scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (scatterDirector == null)
                return 1f;

            float spawnBudgetScale = scatterDirector.CurrentSpawnBudgetScale;
            float faunaActivationScale = scatterDirector.CurrentFaunaActivationScale;
            return math.clamp(spawnBudgetScale * faunaActivationScale, MinimumPopulationBudgetScale, 1f);
        }

        private void RefreshDispatchGroupCount()
        {
            int safeThreadGroupSize = math.max(1, (int)_threadGroupSizeX);
            _dispatchGroupCount = math.max(1, CeilDivPositive(_activeBoidCount, safeThreadGroupSize));
            _clearSpatialGridDispatchGroupCount = math.max(1, CeilDivPositive(SpatialGridMaxCellCount, SpatialGridClearThreadGroupSize));
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
            ResetThreatVoxelSnapshot();
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
            if (EnsureBufferCapacity(ref _threatGridBuffer, cellCount, ThreatGridStride))
                _computeStaticBuffersBound = false;
            EnsureNativeArrayCapacity(ref _threatGridUploadNative, cellCount, nameof(_threatGridUploadNative));

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                _threatGridUploadNative[cellIndex] = threatGrid[cellIndex];

            GraphicsBufferUploadUtility.UploadNativeArray(_threatGridBuffer, _threatGridUploadNative, cellCount);
            _threatGridCellCount = cellCount;
            _threatGridResolution = gridResolution;
            _threatGridCenterWS = gridCenter;
            _threatGridCellSizeWS = math.max(cellSize, ThreatVoxelCellEpsilon);
            _threatGridDataValid = true;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

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
            return true;
        }

        private static bool EnsureRawBuffer(ref GraphicsBuffer buffer, ref bool rawTargetValid, int count, int stride)
        {
            if (buffer != null && rawTargetValid && buffer.count == count && buffer.stride == stride)
                return false;

            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }

            // COLD ALLOC: Raw GraphicsBuffer[count] - byte-addressed GPU atomics for counters/corrections - owner: SargassumMicroFaunaBoids
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
            rawTargetValid = true;
            return true;
        }

        private static bool EnsureBufferCapacity(ref GraphicsBuffer buffer, int minimumCount, int stride)
        {
            int safeMinimumCount = math.max(1, minimumCount);
            if (buffer != null && buffer.stride == stride && buffer.count >= safeMinimumCount)
                return false;

            return EnsureBuffer(ref buffer, safeMinimumCount, stride);
        }

        private void RefreshSpawnData(bool force)
        {
            if (_statisticalPopulationActive)
            {
                _hasSpawnData = false;
                return;
            }

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

                    UploadActiveLeviathanSnapshot();
                    _hasSpawnData = true;
                    _lastDeepLeviathanMode = leviathanSpawnMode;
                    return;
                }

                int deepSpawnUploadCount = ResolveActiveBoidUploadCount();
                if (!BuildDeepSpawnData(deepSpawnUploadCount))
                {
                    _hasSpawnData = false;
                    return;
                }

                UploadSpawnDataToBoidBuffers(deepSpawnUploadCount);
                UploadGrazingAnchors();
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
            int spawnUploadCount = ResolveActiveBoidUploadCount();
            BuildSpawnSet(densityWorldRect, dragManager.GlobalDriftOffset, spawnUploadCount);
            UploadSpawnDataToBoidBuffers(spawnUploadCount);
            BuildGrazingAnchors(densityWorldRect, dragManager.GlobalDriftOffset);
            UploadGrazingAnchors();
            UploadActiveLeviathanSnapshot();
            _frameParity = 0;
            _previousDriftOffset = dragManager.GlobalDriftOffset;
            _lastFieldRevision = dragManager.FieldRevision;
            _debugFieldRevision = _lastFieldRevision;
            _hasSpawnData = true;
        }

        private bool TryDematerializeStatisticalPopulation(float cameraDistanceSq)
        {
            if (_statisticalPopulationActive ||
                _activeBoidCount <= 0 ||
                cameraDistanceSq <= StatisticalDematerializeDistanceSq)
            {
                return false;
            }

            _statisticalPopulationCenterAup = AbsoluteUniversePosition.FromRuntimePosition(_fieldCenter);
            _statisticalPopulationBaseCount = ResolveStatisticalMigrationBaseCount();
            int statisticalMigrationPopulationCount = ResolveStatisticalMigrationPopulationCount();
            _statisticalPopulationPoint = new PopulationDensityPoint
            {
                CenterCellId = BuildPopulationDensityCellId(in _statisticalPopulationCenterAup),
                Count = (ushort)math.clamp(statisticalMigrationPopulationCount, 0, ushort.MaxValue),
                Species = ResolvePopulationSpeciesByte(),
                RadiusMeters = (byte)math.clamp(
                    CeilToIntPositive(math.max(_fieldExtents.x, math.max(_fieldExtents.y, _fieldExtents.z))),
                    PopulationDensityMinRadiusMeters,
                    byte.MaxValue)
            };

            _statisticalPopulationActive = true;
            RefreshStatisticalMigrationPopulation(force: true);
            PushInactiveStatisticalSwarm(in _statisticalPopulationPoint, in _statisticalPopulationCenterAup);
            RecycleStatisticalActiveBoidStorage();
            _hasSpawnData = false;
            _debugVisible = false;
            _debugActiveBoidCount = 0;
            return true;
        }

        private bool TryRematerializeStatisticalPopulation(float cameraDistanceSq)
        {
            if (!_statisticalPopulationActive)
                return true;

            if (cameraDistanceSq > StatisticalRematerializeDistanceSq)
                return false;

            if (boidCompute == null || boidMaterial == null || boidMesh == null)
                return false;

            _computeDispatchDisabled = false;
            EnsureBuffers();
            if (!EnsureComputeKernelBindings())
                return false;

            int rematerializedCount = ResolveStatisticalRematerializedCount();
            if (rematerializedCount <= 0)
            {
                ClearStatisticalPopulationPoint();
                _hasSpawnData = false;
                return false;
            }

            BuildStatisticalRematerializedSpawnSet(rematerializedCount);
            UploadSpawnDataToBoidBuffers(rematerializedCount);
            UploadGrazingAnchors();
            UploadActiveLeviathanSnapshot();

            _activeBoidCount = rematerializedCount;
            _debugActiveBoidCount = rematerializedCount;
            RefreshDispatchGroupCount();
            _frameParity = 0;
            _previousDriftOffset = dragManager != null ? dragManager.GlobalDriftOffset : Vector3.zero;
            _sleepVelocityWritePending = true;
            _lastSimulationLodTier = SimulationLodTier.Sleep;
            _statisticalPopulationActive = false;
            _statisticalMigrationKeepAliveTickCountdown = 0;
            _hasSpawnData = true;
            PrimeFoveatedSimulationDecision(0f, cameraDistanceSq);
            return true;
        }

        private void RecycleStatisticalActiveBoidStorage()
        {
            if (_parasiteLatchReadbackPending)
            {
                _parasiteLatchReadbackPending = false;
                _parasiteLatchReadbackRequest = default;
            }

            _activeBoidCount = 0;
            _debugActiveBoidCount = 0;
            _dispatchGroupCount = 1;
            _debugDispatchGroups = 1;
            _activeGrazingAnchorCount = 0;
            _debugGrazingAnchorCount = 0;
            _activeMassiveThreatCount = 0;
            _debugMassiveThreatCount = 0;
            _debugFormationBeaconCount = 0;
            _debugFormationObstacleCount = 0;
            ClearLeviathanSnapshot();
            _sleepVelocityWritePending = false;
            _parasiteLatchReadbackTimer = 0f;
        }

        private int ResolveStatisticalMigrationBaseCount()
        {
            if (_lastRequestedMigrationPopulationCount > 0)
                return _lastRequestedMigrationPopulationCount;

            if (_migrationPopulationCount > 0)
                return _migrationPopulationCount;

            return math.max(0, _activeBoidCount);
        }

        private int ResolveStatisticalMigrationPopulationCount()
        {
            int count = _migrationPopulationCount > 0 ? _migrationPopulationCount : _activeBoidCount;
            return math.clamp(count, 0, ushort.MaxValue);
        }

        private int ResolveStatisticalRematerializedCount()
        {
            int baseCount = _statisticalPopulationBaseCount > 0
                ? _statisticalPopulationBaseCount
                : _statisticalPopulationPoint.Count;
            Vector3 center = ToVector3(_statisticalPopulationCenterAup.ToRuntimeFloat3());
            _fieldCenter = center;
            if (TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount))
                baseCount = ecosystemPopulationCount;

            _lastRequestedMigrationPopulationCount = math.max(0, baseCount);
            int migrationPopulationCount = RegisterMigrationPopulationAndTrack(
                _statisticalPopulationPoint.Species,
                center,
                baseCount);
            _migrationPopulationCount = math.max(0, migrationPopulationCount);
            int visibleBoidCount = MigrationDirector.ResolveVisibleBoidCountFromMigrationPopulation(migrationPopulationCount);
            int budgetCap = math.clamp(RoundToIntPositive(boidCount * ResolvePopulationBudgetScale()), 0, boidCount);
            return math.clamp(math.min(visibleBoidCount, budgetCap), 0, boidCount);
        }

        private void PushInactiveStatisticalSwarm(in PopulationDensityPoint point, in AbsoluteUniversePosition centerAup)
        {
            _inactiveStatisticalSwarmRing.PushOverwrite(in point);
            _inactiveStatisticalSwarmCenterRing.PushOverwrite(in centerAup);
        }

        private void RefreshStatisticalMigrationPopulation(bool force)
        {
            if (!_statisticalPopulationActive)
                return;

            if (!force && _statisticalMigrationKeepAliveTickCountdown > 0)
            {
                _statisticalMigrationKeepAliveTickCountdown--;
                return;
            }

            _statisticalMigrationKeepAliveTickCountdown = StatisticalMigrationKeepAliveSlowTickStride;
            Vector3 center = ToVector3(_statisticalPopulationCenterAup.ToRuntimeFloat3());
            _fieldCenter = center;
            _renderBounds.center = center;
            _debugRenderBounds = _renderBounds;
            int baseCount = _statisticalPopulationBaseCount > 0 ? _statisticalPopulationBaseCount : _statisticalPopulationPoint.Count;
            if (TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount))
                baseCount = ecosystemPopulationCount;

            baseCount = math.max(0, baseCount);
            _statisticalPopulationBaseCount = baseCount;
            _lastRequestedMigrationPopulationCount = math.max(0, baseCount);
            int migrationPopulationCount = RegisterMigrationPopulationAndTrack(
                _statisticalPopulationPoint.Species,
                center,
                baseCount);
            _migrationPopulationCount = math.max(0, migrationPopulationCount);
            _statisticalPopulationPoint.Count = (ushort)math.clamp(migrationPopulationCount, 0, ushort.MaxValue);
        }

        private void ClearStatisticalMigrationPopulation()
        {
            if (!_statisticalPopulationActive && !_registeredMigrationPopulationValid)
                return;

            ClearRegisteredMigrationPopulation();
        }

        private int RegisterMigrationPopulationAndTrack(byte species, Vector3 center, int basePopulationCount)
        {
            int safeBasePopulationCount = math.max(0, basePopulationCount);
            if (safeBasePopulationCount <= 0)
            {
                ClearRegisteredMigrationPopulation();
                return 0;
            }

            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromRuntimePosition(center);
            int3 aupCell = MigrationDirector.ResolveMigrationPopulationAupCell(center);
            ClearRegisteredMigrationPopulationIfChanged(species, aupCell);
            int migrationPopulationCount = MigrationDirector.RegisterStatisticalSwarmPopulationAndResolveCount(
                species,
                center,
                safeBasePopulationCount);

            if (migrationPopulationCount > 0)
            {
                _registeredMigrationPopulationCenterAup = centerAup;
                _registeredMigrationPopulationAupCell = aupCell;
                _registeredMigrationPopulationSpecies = species;
                _registeredMigrationPopulationValid = true;
            }
            else
            {
                ClearRegisteredMigrationPopulation();
            }

            return migrationPopulationCount;
        }

        private void ClearRegisteredMigrationPopulationIfChanged(byte species, in int3 aupCell)
        {
            if (!_registeredMigrationPopulationValid ||
                (_registeredMigrationPopulationSpecies == species &&
                 _registeredMigrationPopulationAupCell.x == aupCell.x &&
                 _registeredMigrationPopulationAupCell.y == aupCell.y &&
                 _registeredMigrationPopulationAupCell.z == aupCell.z))
            {
                return;
            }

            ClearRegisteredMigrationPopulation();
        }

        private void ClearRegisteredMigrationPopulation()
        {
            if (_registeredMigrationPopulationValid)
            {
                Vector3 center = ToVector3(_registeredMigrationPopulationCenterAup.ToRuntimeFloat3());
                MigrationDirector.RegisterStatisticalSwarmPopulation(
                    _registeredMigrationPopulationSpecies,
                    center,
                    0);
            }

            _registeredMigrationPopulationCenterAup = default;
            _registeredMigrationPopulationAupCell = default;
            _registeredMigrationPopulationSpecies = 0;
            _registeredMigrationPopulationValid = false;
            _migrationPopulationCount = 0;
        }

        private void BuildStatisticalRematerializedSpawnSet(int rematerializedCount)
        {
            Vector3 center = ToVector3(_statisticalPopulationCenterAup.ToRuntimeFloat3());
            float radius = math.max(PopulationDensityMinRadiusMeters, _statisticalPopulationPoint.RadiusMeters);
            _fieldCenter = center;
            _fieldExtents = new Vector3(radius, radius * 0.45f, radius);
            _renderBounds = new Bounds(_fieldCenter, _fieldExtents * 2f);
            _debugRenderBounds = _renderBounds;
            _densityWorldRect = Vector4.zero;

            for (int i = 0; i < rematerializedCount; i++)
            {
                Vector3 offset = BuildSphericalFibonacciOffset(i, rematerializedCount, radius, _statisticalPopulationPoint.CenterCellId);
                Vector3 spawnPosition = center + offset;
                Vector3 velocity = BuildStatisticalTangentVelocity(offset, i);

                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = velocity,
                    Panic = 0f,
                    StateFlags = DefaultBoidStateFlags
                };
            }

            BuildStatisticalGrazingAnchors(center, radius, rematerializedCount);
        }

        private void BuildStatisticalGrazingAnchors(Vector3 center, float radius, int rematerializedCount)
        {
            _activeGrazingAnchorCount = math.min(ResolveTargetGrazingAnchorCount(rematerializedCount), 8);
            float anchorRadius = math.max(grazingRadius, radius * 0.2f);
            for (int i = 0; i < _activeGrazingAnchorCount; i++)
            {
                Vector3 offset = BuildSphericalFibonacciOffset(i, _activeGrazingAnchorCount, math.max(1f, radius * 0.35f), _statisticalPopulationPoint.CenterCellId ^ 0x6C8E9CF5);
                _grazingAnchors[i] = new GrazingAnchorData
                {
                    Position = center + offset,
                    Radius = anchorRadius,
                    Strength = 0.85f,
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private static Vector3 BuildSphericalFibonacciOffset(int index, int count, float radius)
        {
            return BuildSphericalFibonacciOffset(index, count, radius, 0);
        }

        private static Vector3 BuildSphericalFibonacciOffset(int index, int count, float radius, int centerCellSeed)
        {
            float safeCount = math.max(1, count);
            float y = 1f - (2f * (index + 0.5f) / safeCount);
            float ringRadius = math.max(0f, 1f - y * y);
            uint seed = unchecked((uint)centerCellSeed);
            float seedRotation = (seed & 0x0000FFFFu) * (StatisticalTwoPi / 65536f);
            float theta = index * StatisticalFibonacciGoldenAngle + seedRotation;
            return new Vector3(CheapCosSigned(theta) * ringRadius, y * 0.45f, CheapSinSigned(theta) * ringRadius) * radius;
        }

        private static int BuildPopulationDensityCellId(in AbsoluteUniversePosition centerAup)
        {
            int localX = FloorToInt(centerAup.LocalX / PopulationDensityCellSizeMeters);
            int localY = FloorToInt(centerAup.LocalY / PopulationDensityCellSizeMeters);
            int localZ = FloorToInt(centerAup.LocalZ / PopulationDensityCellSizeMeters);
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridX));
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridY));
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridZ));
                hash = HashPopulationDensityComponent(hash, (uint)localX);
                hash = HashPopulationDensityComponent(hash, (uint)localY);
                hash = HashPopulationDensityComponent(hash, (uint)localZ);
                return (int)(hash & 0x7FFFFFFFu);
            }
        }

        private static uint FoldLongToUInt(long value)
        {
            ulong bits = unchecked((ulong)value);
            return (uint)(bits ^ (bits >> 32));
        }

        private static uint HashPopulationDensityComponent(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private byte ResolvePopulationSpeciesByte()
        {
            if (_leviathanModeActive)
                return 3;

            if (_deepModeActive)
                return 2;

            return 1;
        }

        private void ClearStatisticalPopulationPoint()
        {
            ClearStatisticalMigrationPopulation();
            _statisticalPopulationPoint = default;
            _statisticalPopulationCenterAup = default;
            _statisticalPopulationActive = false;
            _statisticalPopulationBaseCount = 0;
            _lastRequestedMigrationPopulationCount = 0;
            _statisticalMigrationKeepAliveTickCountdown = 0;
            _inactiveStatisticalSwarmRing.Clear();
            _inactiveStatisticalSwarmCenterRing.Clear();
        }

        private bool BuildDeepSpawnData(int spawnCount)
        {
            if (biolumManager == null || !TryResolvePlayerRuntimePosition(out Vector3 playerPosition) || _deepBiolumZones == null || _deepBiolumZoneScores == null)
                return false;

            int zoneCount = biolumManager.CopyNearbyZonesNonAlloc(
                playerPosition,
                deepBiolumSearchRadius,
                _deepBiolumZones,
                _deepBiolumZoneScores);
            zoneCount = math.clamp(zoneCount, 0, math.min(_deepBiolumZones.Length, _deepBiolumZoneScores.Length));
            if (zoneCount <= 0)
                return false;

            _densityWorldRect = Vector4.zero;
            BuildLeviathanData();
            if (_leviathanPathNodeCount > 1 && _leviathanThreatLevel >= leviathanThreatThreshold)
            {
                BuildLeviathanSpawnSet(spawnCount);
                BuildDeepGrazingAnchors(zoneCount);
                HarvestFormationObstacles(_fieldCenter);
            }
            else
            {
                BuildDeepSpawnSet(zoneCount, spawnCount);
                BuildDeepGrazingAnchors(zoneCount);
                BuildFormationData();
            }

            return true;
        }

        private bool IsDeepModeActive()
        {
            return TryResolvePlayerRuntimePosition(out Vector3 playerPosition) && playerPosition.y <= deepSeaWorldYThreshold;
        }

        private bool IsParasiteModeActive()
        {
            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition) || playerPosition.y > parasiteDroneWorldYThreshold)
                return false;

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

            return math.saturate(math.max(_flashlightOn ? 1f : 0f, ResolveHeadlightPanic01()));
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

            if (!_deepModeActive)
                return;

            BeaconNetworkSystem beaconNetwork = Hecton8.Core.GlobalRegistry.BeaconNetwork;
            if (beaconNetwork == null || _formationBeaconSnapshots == null)
                return;

            int snapshotCount = beaconNetwork.CopySnapshots(_formationBeaconSnapshots);
            snapshotCount = math.clamp(snapshotCount, 0, _formationBeaconSnapshots.Length);
            if (snapshotCount <= 0)
                return;

            if (!TryResolvePlayerRuntimePosition(out Vector3 origin))
                return;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            HectonFluidEngine fluidRuntime = GlobalRegistry.Fluid;
            int formationCount = 0;
            for (int i = 0; i < snapshotCount && formationCount < _formationBeacons.Length; i++)
            {
                BeaconNetworkSystem.BeaconSnapshot snapshot = _formationBeaconSnapshots[i];
                Vector3 beaconPosition = snapshot.Position;
                AbsoluteUniversePosition beaconAup = AbsoluteUniversePosition.FromRuntimePosition(beaconPosition);
                if (AbsoluteUniversePosition.DistanceSq(in beaconAup, in originAup) > (double)formationBeaconSearchRadius * formationBeaconSearchRadius)
                    continue;

                float beaconRadius = math.clamp(snapshot.LightRange * 2.2f, 4f, formationBeaconSearchRadius * 0.35f);
                Vector2 leaderFlowXZ = Vector2.zero;
                if (fluidRuntime != null &&
                    fluidRuntime.TrySampleModAbyssalFlow(beaconPosition, out float3 resolvedLeaderFlow))
                {
                    leaderFlowXZ = new Vector2(resolvedLeaderFlow.x, resolvedLeaderFlow.z);
                }

                _formationBeacons[formationCount] = new FormationBeaconData
                {
                    Position = beaconPosition,
                    Radius = beaconRadius,
                    Strength = 1f,
                    Phase = HashToFloat01((uint)i, 0u, 0x55A1F13Du),
                    Padding = leaderFlowXZ
                };
                formationCount++;
            }

            _debugFormationBeaconCount = formationCount;
            UploadFormationBeacons();
            if (formationCount <= 0)
                return;

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

            int safeCount = math.min(count, math.min(matrices.Length, math.min(metadata.Length, semanticTypes.Length)));
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
                Vector3 extents = new Vector3(
                    math.abs(axisX.x) + math.abs(axisX.y) + math.abs(axisX.z),
                    math.abs(axisY.x) + math.abs(axisY.y) + math.abs(axisY.z),
                    math.abs(axisZ.x) + math.abs(axisZ.y) + math.abs(axisZ.z));
                float radius = math.max(extents.x, math.max(extents.y, extents.z));
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
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            for (int i = 0; i < _staticObstacleCacheCount && obstacleCount < _formationObstacles.Length; i++)
            {
                StaticObstacleData obstacle = _staticObstacleCache[i];
                float radius = math.max(0.1f, obstacle.Radius);
                float maxDistance = formationObstacleSearchRadius + radius;
                Vector3 obstaclePosition = new Vector3(obstacle.Center.x, obstacle.Center.y, obstacle.Center.z);
                AbsoluteUniversePosition obstacleAup = AbsoluteUniversePosition.FromRuntimePosition(obstaclePosition);
                if (AbsoluteUniversePosition.DistanceSq(in obstacleAup, in originAup) > (double)maxDistance * maxDistance)
                    continue;

                _formationObstacles[obstacleCount] = new FormationObstacleData
                {
                    Position = obstaclePosition,
                    Radius = radius,
                    Weight = 1f,
                    Padding = Vector3.zero
                };
                obstacleCount++;
            }

            _debugFormationObstacleCount = obstacleCount;
            UploadFormationObstacles();
        }

        private void BuildLeviathanData()
        {
            _leviathanThreatLevel = 0f;
            bool hasPlayerPosition = TryResolvePlayerRuntimePosition(out Vector3 playerPosition);
            _leviathanHotspotWS = hasPlayerPosition ? playerPosition : Vector3.zero;
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = _leviathanHotspotWS;
            if (!_leviathanNodeFrontNative.IsCreated || !_leviathanNodeBackNative.IsCreated || _mapMagicVegetationBridge == null || !hasPlayerPosition)
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

            _mapMagicVegetationBridge.TryScheduleAbyssalPath(playerPosition, hotspotPosition, out _);
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
        }

        private void ScheduleLeviathanNodeBuild(NativeArray<Vector3> path, int pathCount)
        {
            int safePathCount = math.min(pathCount, path.Length);
            if (safePathCount < 2 || !_leviathanNodeBackNative.IsCreated || _leviathanNodeBackNative.Length <= 0)
                return;

            if (_leviathanNodeBuildScheduled)
                return;

            EnsureNativeArrayCapacity(ref _leviathanPathScratchNative, safePathCount, nameof(_leviathanPathScratchNative));
            for (int i = 0; i < safePathCount; i++)
                _leviathanPathScratchNative[i] = path[i];

            var job = new BuildLeviathanNodeJob
            {
                SourcePath = _leviathanPathScratchNative,
                SourceCount = safePathCount,
                OutputNodes = _leviathanNodeBackNative,
                OutputCount = _leviathanNodeCountNative,
                BodyRadius = math.max(0.5f, leviathanBodyRadius)
            };

            _leviathanNodeBuildHandle = job.Schedule();
            _leviathanNodeBuildScheduled = true;
        }

        private bool TrySampleLeviathanPath(float distance01, out Vector3 positionWS, out Vector3 tangentWS, out float radiusWS)
        {
            positionWS = _fieldCenter;
            tangentWS = Vector3.forward;
            radiusWS = math.max(0.5f, leviathanBodyRadius);
            if (!_leviathanNodeFrontNative.IsCreated || _leviathanPathNodeCount < 2)
                return false;

            int safeCount = math.min(_leviathanPathNodeCount, _leviathanNodeFrontNative.Length);
            LeviathanNodeData previousNode = _leviathanNodeFrontNative[0];
            for (int i = 1; i < safeCount; i++)
            {
                LeviathanNodeData currentNode = _leviathanNodeFrontNative[i];
                if (distance01 > currentNode.Distance01 && i < safeCount - 1)
                {
                    previousNode = currentNode;
                    continue;
                }

                float segmentLength01 = math.max(0.0001f, currentNode.Distance01 - previousNode.Distance01);
                float segmentT = math.saturate((distance01 - previousNode.Distance01) / segmentLength01);
                positionWS = ToVector3(math.lerp(previousNode.Position, currentNode.Position, segmentT));
                tangentWS = FastNormalizeVector3(ToVector3(math.lerp(previousNode.Tangent, currentNode.Tangent, segmentT)), Vector3.forward);
                radiusWS = math.lerp(previousNode.Radius, currentNode.Radius, segmentT);
                return true;
            }

            LeviathanNodeData tailNode = _leviathanNodeFrontNative[safeCount - 1];
            positionWS = ToVector3(tailNode.Position);
            Vector3 tailTangent = ToVector3(tailNode.Tangent);
            tangentWS = FastNormalizeVector3(tailTangent, Vector3.forward);
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
                _leviathanHeadRadiusWS = math.max(0.5f, leviathanBodyRadius);
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
            headRadiusWS = math.max(0.5f, leviathanBodyRadius);
            if (!TrySampleLeviathanPath(0f, out Vector3 splinePosition, out Vector3 splineTangent, out float bodyRadius))
                return false;

            Vector3 safeTangent = FastNormalizeVector3(splineTangent, Vector3.forward);
            Vector3 lateral = ResolveApproxRight(safeTangent);
            Vector3 vertical = ResolveApproxUp(safeTangent, lateral);

            float simulationPhaseTime = GetAbsoluteSimulationTime();
            float surroundAttack = math.saturate((_leviathanThreatLevel - leviathanSurroundThreatThreshold) / math.max(1f - leviathanSurroundThreatThreshold, 0.001f));
            float wavePhase = simulationPhaseTime * leviathanWaveFrequency;
            float lateralWave = CheapSinSigned(wavePhase) * (bodyRadius * leviathanWaveAmplitude);
            float verticalWaveOffset = CheapCosSigned(wavePhase * 0.63f) * (bodyRadius * leviathanWaveAmplitude * 0.35f);
            Vector3 leviathanTarget = splinePosition + lateral * lateralWave + vertical * verticalWaveOffset;

            Vector3 ringTarget = leviathanTarget;
            if (surroundAttack > 0f && TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
            {
                float ringRadius = math.max(leviathanSurroundRadius, bodyRadius * 2.4f);
                float ringPulse = CheapSinSigned(simulationPhaseTime * (leviathanWaveFrequency * 0.7f));
                float ringAngle = simulationPhaseTime * leviathanSurroundSpinSpeed;
                Vector3 ringOffset = new Vector3(
                    CheapCosSigned(ringAngle),
                    ringPulse * (bodyRadius * 0.18f),
                    CheapSinSigned(ringAngle)) * (ringRadius + ringPulse * bodyRadius * 0.22f);
                ringTarget = playerPosition + ringOffset;
            }

            headPositionWS = leviathanTarget + (ringTarget - leviathanTarget) * surroundAttack + safeTangent * math.max(bodyRadius * 0.55f, 0.6f);
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

            float sampleStep = 1f / math.max(1, _leviathanPathNodeCount - 1);
            float nextDistance01 = math.saturate(sampleStep);
            if (!TrySampleLeviathanPath(nextDistance01, out Vector3 nextSplinePoint, out Vector3 nextSplineTangent, out _))
                nextSplinePoint = currentSplinePoint + currentSplineTangent;

            Vector3 splineDelta = nextSplinePoint - currentSplinePoint;
            if (splineDelta.sqrMagnitude <= 0.000001f)
                splineDelta = currentSplineTangent.sqrMagnitude > 0.0001f
                    ? FastNormalizeVector3(currentSplineTangent, Vector3.forward)
                    : Vector3.forward;

            courseForwardWS = FastNormalizeVector3(splineDelta, FastNormalizeVector3(nextSplineTangent, Vector3.forward));
            courseVelocityWS = splineDelta / math.max(dt, 0.0001f);
            return true;
        }

        private void BuildLeviathanSpawnSet(int spawnCount)
        {
            if (_leviathanPathNodeCount < 2 || !_leviathanNodeFrontNative.IsCreated)
                return;

            Vector3 boundsMin = ToVector3(_leviathanNodeFrontNative[0].Position);
            Vector3 boundsMax = boundsMin;
            float radiusPadding = math.max(1f, leviathanBodyRadius * (1f + leviathanWaveAmplitude));
            for (int i = 0; i < _leviathanPathNodeCount; i++)
            {
                Vector3 nodePosition = ToVector3(_leviathanNodeFrontNative[i].Position);
                Vector3 nodeExtents = new Vector3(radiusPadding, radiusPadding, radiusPadding);
                boundsMin = MinVector3(boundsMin, nodePosition - nodeExtents);
                boundsMax = MaxVector3(boundsMax, nodePosition + nodeExtents);
            }

            _fieldCenter = (boundsMin + boundsMax) * 0.5f;
            _fieldExtents = MaxVector3((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 2f, 2f));
            _renderBounds = new Bounds(_fieldCenter, MaxVector3(boundsMax - boundsMin, new Vector3(4f, 4f, 4f)));
            _debugRenderBounds = _renderBounds;

            int safeSpawnCount = math.clamp(spawnCount, 0, boidCount);
            for (int i = 0; i < safeSpawnCount; i++)
            {
                float bodyT = safeSpawnCount > 1 ? i / (float)(safeSpawnCount - 1) : 0f;
                if (!TrySampleLeviathanPath(bodyT, out Vector3 centerlinePosition, out Vector3 tangentWS, out float bodyRadius))
                {
                    centerlinePosition = _fieldCenter;
                    tangentWS = Vector3.forward;
                    bodyRadius = leviathanBodyRadius;
                }

                Vector3 normalWS = ResolveApproxRight(tangentWS);
                Vector3 binormalWS = ResolveApproxUp(tangentWS, normalWS);
                float angle = HashToFloat01((uint)i, 0u, 0x6A09E667u) * StatisticalTwoPi;
                float radialT = HashToFloat01((uint)i, 0u, 0xBB67AE85u);
                float spawnSeed = HashToFloat01((uint)i, 0u, 0x94D049BBu);
                float lateralWave = CheapSinSigned(bodyT * 15.7f + spawnSeed * StatisticalTwoPi) * (bodyRadius * leviathanWaveAmplitude * 0.45f);
                float radialDistance = bodyRadius * radialT * 0.78f;
                Vector3 spawnOffset =
                    normalWS * (CheapCosSigned(angle) * radialDistance + lateralWave) +
                    binormalWS * (CheapSinSigned(angle) * radialDistance * 0.55f);
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

        private void BuildDeepSpawnSet(int zoneCount, int spawnCount)
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

                float score = math.max(0.0001f, _deepBiolumZoneScores[i]);
                Vector3 zonePosition = zone.GetZonePosition();
                weightedCenter += zonePosition * score;
                weightSum += score;

                Vector3 extents = new Vector3(deepBaitBallRadius, deepBaitBallHeight, deepBaitBallRadius);
                boundsMin = MinVector3(boundsMin, zonePosition - extents);
                boundsMax = MaxVector3(boundsMax, zonePosition + extents);
            }

            _fieldCenter = weightSum > 0.0001f ? weightedCenter / weightSum : primaryPosition;
            _fieldExtents = MaxVector3((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 1f, 2f));
            _renderBounds = new Bounds(_fieldCenter, MaxVector3(boundsMax - boundsMin, new Vector3(4f, 2f, 4f)));
            _debugRenderBounds = _renderBounds;

            int safeSpawnCount = math.clamp(spawnCount, 0, boidCount);
            for (int i = 0; i < safeSpawnCount; i++)
            {
                int zoneIndex = i % zoneCount;
                HectonBiolumZone zone = _deepBiolumZones[zoneIndex];
                Vector3 anchorPosition = zone != null ? zone.GetZonePosition() : _fieldCenter;
                float radiusT = HashToFloat01((uint)i, 0u, 0xA2F98A1Du);
                float angle = HashToFloat01((uint)i, 0u, 0x3C6EF372u) * StatisticalTwoPi;
                float verticalT = HashToFloat01((uint)i, 0u, 0x1BF5C7D5u) * 2f - 1f;
                Vector3 spawnPosition = anchorPosition;
                spawnPosition.x += CheapCosSigned(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.z += CheapSinSigned(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.y += verticalT * deepBaitBallHeight;

                Vector3 toCenter = anchorPosition - spawnPosition;
                if (toCenter.sqrMagnitude <= 0.0001f)
                    toCenter = BuildInitialVelocity(i);
                else
                    toCenter = FastNormalizeVector3(toCenter, Vector3.forward);

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
            int targetAnchorCount = math.min(zoneCount, ResolveTargetGrazingAnchorCount(ResolveActiveBoidUploadCount()));
            for (int i = 0; i < zoneCount && _activeGrazingAnchorCount < targetAnchorCount; i++)
            {
                HectonBiolumZone zone = _deepBiolumZones[i];
                if (zone == null)
                    continue;

                _grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
                {
                    Position = zone.GetZonePosition(),
                    Radius = deepBaitBallRadius,
                    Strength = math.lerp(1.2f, 1.8f, math.saturate(_deepBiolumZoneScores[i])),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private void BuildSpawnSet(Vector4 densityWorldRect, Vector3 driftOffset, int spawnCount)
        {
            float sizeX = 1f / math.max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / math.max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackCenter = new Vector3(minX + sizeX * 0.5f + driftOffset.x, (minY + maxY) * 0.5f, minZ + sizeZ * 0.5f + driftOffset.z);

            _fieldCenter = fallbackCenter;
            _fieldExtents = new Vector3(sizeX * 0.5f, math.max(1f, maxDepthBelowSurface), sizeZ * 0.5f);
            _renderBounds = new Bounds(_fieldCenter, new Vector3(sizeX, math.max(2f, maxDepthBelowSurface + 2f), sizeZ));
            _debugRenderBounds = _renderBounds;

            int safeSpawnCount = math.clamp(spawnCount, 0, boidCount);
            for (int i = 0; i < safeSpawnCount; i++)
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
                    spawnPosition.y = math.lerp(minY, maxY, w);
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
            int targetAnchorCount = ResolveTargetGrazingAnchorCount(ResolveActiveBoidUploadCount());
            if (targetAnchorCount <= 0)
            {
                _activeGrazingAnchorCount = 0;
                _debugGrazingAnchorCount = 0;
                return;
            }

            float sizeX = 1f / math.max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / math.max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackPosition = new Vector3(minX + sizeX * 0.5f + driftOffset.x, math.lerp(minY, maxY, 0.32f), minZ + sizeZ * 0.5f + driftOffset.z);

            _activeGrazingAnchorCount = 0;
            for (int i = 0; i < grazingAnchorCount && _activeGrazingAnchorCount < targetAnchorCount; i++)
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
                    anchorPosition.y = math.lerp(minY, maxY, math.lerp(0.18f, 0.58f, w));
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
                    Strength = math.lerp(0.8f, 1.25f, fieldSample.Density01),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private Vector3 BuildInitialVelocity(int index)
        {
            int lane = (int)math.min(7f, HashToFloat01((uint)index, 0u, 0xDEADBEEFu) * 8f);
            float vertical = math.lerp(-0.08f, 0.08f, HashToFloat01((uint)index, 0u, 0x165667B1u));
            Vector3 direction;
            switch (lane)
            {
                case 0:
                    direction = new Vector3(1f, vertical, 0f);
                    break;
                case 1:
                    direction = new Vector3(0.70710678f, vertical, 0.70710678f);
                    break;
                case 2:
                    direction = new Vector3(0f, vertical, 1f);
                    break;
                case 3:
                    direction = new Vector3(-0.70710678f, vertical, 0.70710678f);
                    break;
                case 4:
                    direction = new Vector3(-1f, vertical, 0f);
                    break;
                case 5:
                    direction = new Vector3(-0.70710678f, vertical, -0.70710678f);
                    break;
                case 6:
                    direction = new Vector3(0f, vertical, -1f);
                    break;
                default:
                    direction = new Vector3(0.70710678f, vertical, -0.70710678f);
                    break;
            }

            return direction * cruiseSpeed;
        }

        private Vector3 BuildStatisticalTangentVelocity(Vector3 offset, int index)
        {
            float tangentX = offset.z;
            float tangentZ = -offset.x;
            float absX = math.abs(tangentX);
            float absZ = math.abs(tangentZ);
            if (math.max(absX, absZ) <= 0.0001f)
                return BuildInitialVelocity(index);

            float vertical = math.lerp(-0.08f, 0.08f, HashToFloat01((uint)index, 0u, 0x165667B1u));
            if (absX >= absZ)
                return new Vector3(tangentX < 0f ? -1f : 1f, vertical, 0f) * cruiseSpeed;

            return new Vector3(0f, vertical, tangentZ < 0f ? -1f : 1f) * cruiseSpeed;
        }

        private bool EnsureStaticComputeBufferBindings()
        {
            if (_computeStaticBuffersBound)
                return true;

            if (boidCompute == null ||
                _kernelIndex < 0 ||
                _buildSpatialGridKernelIndex < 0 ||
                _pbdSolveKernelIndex < 0 ||
                _clearPbdCorrectionsKernelIndex < 0 ||
                _clearSpatialGridKernelIndex < 0 ||
                _simulationFrameBuffer == null ||
                _pbdCorrectionBuffer == null ||
                _spatialGridCountBuffer == null ||
                _spatialGridCellBuffer == null ||
                _grazingAnchorBuffer == null ||
                _formationBeaconBuffer == null ||
                _formationObstacleBuffer == null ||
                _leviathanNodeBuffer == null ||
                _massiveThreatBuffer == null ||
                _threatGridBuffer == null ||
                _threatVoxelBuffer == null)
            {
                return false;
            }

            try
            {
                boidCompute.SetBuffer(_kernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
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

                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _SpatialGridCellsId, _spatialGridCellBuffer);

                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _SpatialGridCellsId, _spatialGridCellBuffer);
                boidCompute.SetBuffer(_pbdSolveKernelIndex, _PbdCorrectionsId, _pbdCorrectionBuffer);

                boidCompute.SetBuffer(_clearPbdCorrectionsKernelIndex, _PbdCorrectionsId, _pbdCorrectionBuffer);
                boidCompute.SetBuffer(_clearPbdCorrectionsKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_clearSpatialGridKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);
                boidCompute.SetBuffer(_clearSpatialGridKernelIndex, _SpatialGridCountsId, _spatialGridCountBuffer);

                if (_latchStatsBuffer != null)
                {
                    boidCompute.SetBuffer(_kernelIndex, _LatchStatsId, _latchStatsBuffer);
                    if (_clearStatsKernelIndex >= 0)
                        boidCompute.SetBuffer(_clearStatsKernelIndex, _LatchStatsId, _latchStatsBuffer);
                }
            }
            catch (Exception)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }

            _computeStaticBuffersBound = true;
            return true;
        }

        private void SetMainKernelTextureIfChanged(int propertyId, Texture texture, ref Texture boundTexture)
        {
            if (ReferenceEquals(boundTexture, texture))
                return;

            boidCompute.SetTexture(_kernelIndex, propertyId, texture);
            boundTexture = texture;
        }

        private void EnsureFallbackAbyssalFlowTexture()
        {
            if (_fallbackAbyssalFlowTexture != null)
                return;

            _fallbackAbyssalFlowTexture = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false)
            {
                name = "__HectonSargassumEmptyAbyssalFlow",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            }; // COLD ALLOC: Texture3D[1] - zero fallback abyssal-flow volume for swarm compute binding - owner: SargassumMicroFaunaBoids
            _fallbackAbyssalFlowTexture.SetPixel(0, 0, 0, Color.clear);
            _fallbackAbyssalFlowTexture.Apply(false, true);
        }

        private bool BindSimulationUniforms(
            float simulationDt,
            Vector3 driftOffset,
            Vector3 driftDelta,
            float hibernation01,
            SimulationLodTier simulationLodTier,
            bool shouldRender)
        {
            if (!_simulationFrameNative.IsCreated || _simulationFrameBuffer == null)
                return false;

            GraphicsBuffer readBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _boidsBufferB : _boidsBufferA;

            ResolvePlayerGpuFrame(
                out Vector3 playerPosition,
                out Vector3 playerVelocity,
                out Vector3 playerRight,
                out Vector3 playerUp,
                out Vector3 playerForward,
                out Vector3 cameraPosition);
            Vector3 playerDirection = playerPosition - _fieldCenter;
            if (playerDirection.sqrMagnitude <= 0.0001f)
                playerDirection = playerForward;
            playerDirection = FastNormalizeVector3(playerDirection, playerForward);
            float playerSpeedSq = playerVelocity.sqrMagnitude;
            float playerSpeed = math.min(
                playerSpeedSq / math.max(0.001f, panicPlayerSpeedThreshold),
                panicPlayerSpeedThreshold * 2f);
            float headlightPanic01 = ResolveHeadlightPanic01();
            float parasiteAggression01 = ResolveParasiteAggression01();
            float panicPlayerRadiusScale =
                _playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive()
                    ? HectonVegetationConstants.BoidScooterPanicRadiusMultiplier
                    : 1f;
            if (headlightPanic01 > 0f)
                panicPlayerRadiusScale = math.max(panicPlayerRadiusScale, math.lerp(1f, deepHeadlightPanicRadiusScale, headlightPanic01));

            RenderTexture cutMaskTexture = null;
            Vector4 cutMaskWorldRect = Vector4.zero;
            bool cutMaskActive = !_deepModeActive &&
                                 cutManager != null &&
                                 cutManager.TryGetCutMask(out cutMaskTexture, out cutMaskWorldRect);
            Texture densityTexture = !_deepModeActive && dragManager != null ? dragManager.DensityFieldTexture : Texture2D.blackTexture;
            if (densityTexture == null)
                densityTexture = Texture2D.blackTexture;
            Texture activeCutMaskTexture = cutMaskActive && cutMaskTexture != null ? cutMaskTexture : Texture2D.blackTexture;
            Vector3 abyssalFlowWeatherCurrent = Vector3.zero;
            HectonFluidEngine fluidRuntime = GlobalRegistry.Fluid;
            if (fluidRuntime != null &&
                fluidRuntime.TrySampleModAbyssalFlow(_fieldCenter, out float3 resolvedAbyssalFlow))
            {
                abyssalFlowWeatherCurrent = new Vector3(resolvedAbyssalFlow.x, resolvedAbyssalFlow.y, resolvedAbyssalFlow.z);
            }

            Texture abyssalFlowTexture = _fallbackAbyssalFlowTexture;
            Vector4 abyssalFlowCenter = Vector4.zero;
            Vector4 abyssalFlowSpacing = Vector4.zero;
            float abyssalFlowActive = 0f;
            if (fluidRuntime != null &&
                fluidRuntime.TryGetGpuAbyssalFlowFieldTexture(
                    out Texture publishedAbyssalFlowTexture,
                    out _,
                    out Vector4 publishedAbyssalFlowCenter,
                    out Vector4 publishedAbyssalFlowSpacing))
            {
                abyssalFlowTexture = publishedAbyssalFlowTexture;
                abyssalFlowCenter = publishedAbyssalFlowCenter;
                abyssalFlowSpacing = publishedAbyssalFlowSpacing;
                abyssalFlowActive = 1f;
            }

            float transportCapsuleRadius = 0f;
            float transportCapsuleHalfLength = 0f;
            if (_playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive())
            {
                transportCapsuleRadius = math.max(boidBodyRadius * 6f, panicPlayerRadius * panicPlayerRadiusScale);
                transportCapsuleHalfLength = math.max(transportCapsuleRadius, playerSpeed * 0.35f);
            }

            Vector3 submarineWakePosition = Vector3.zero;
            Vector3 submarineWakeVelocity = Vector3.zero;
            float submarineWakeRadius = 0f;
            float submarineWakeHalfLength = 0f;
            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Rigidbody submarineHull = submarine != null ? submarine.HullRigidbody : null;
            if (submarineHull != null)
            {
                submarineWakeVelocity = submarineHull.linearVelocity;
                float submarineSpeedSq = submarineWakeVelocity.sqrMagnitude;
                if (submarineSpeedSq > SubmarineWakeMinimumSpeedMetersPerSecond * SubmarineWakeMinimumSpeedMetersPerSecond)
                {
                    submarineWakePosition = submarineHull.worldCenterOfMass;
                    float wakeRadiusSpeed = (SubmarineWakeMaxRadiusMeters - SubmarineWakeBaseRadiusMeters) / math.max(0.001f, SubmarineWakeRadiusSpeedScale);
                    float wakeHalfLengthSpeed = (SubmarineWakeMaxHalfLengthMeters - SubmarineWakeBaseHalfLengthMeters) / math.max(0.001f, SubmarineWakeHalfLengthSpeedScale);
                    float wakeRadius01 = math.saturate(submarineSpeedSq / math.max(0.001f, wakeRadiusSpeed * wakeRadiusSpeed));
                    float wakeHalfLength01 = math.saturate(submarineSpeedSq / math.max(0.001f, wakeHalfLengthSpeed * wakeHalfLengthSpeed));
                    submarineWakeRadius = math.lerp(SubmarineWakeBaseRadiusMeters, SubmarineWakeMaxRadiusMeters, wakeRadius01);
                    submarineWakeHalfLength = math.lerp(SubmarineWakeBaseHalfLengthMeters, SubmarineWakeMaxHalfLengthMeters, wakeHalfLength01);
                }
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            UpdateFragmentationState(playerPosition, playerVelocity, playerForward, playerSpeed, absoluteSimulationTime);
            UpdateSonarScatterState(simulationDt, absoluteSimulationTime);
            float fragmentation01 = ResolveFragmentationStrength01(absoluteSimulationTime);
            float fragmentationHalfDistance = _fragmentationHalfDistanceWS;
            float sonarScatterStrength01 = absoluteSimulationTime < _sonarScatterExpireTime
                ? math.saturate(_sonarScatterStrength01)
                : 0f;
            float acousticPanicStrength01 = absoluteSimulationTime < _acousticPanicExpireTime
                ? math.saturate(_acousticPanicStrength01)
                : 0f;
            if (acousticPanicStrength01 <= 0f)
            {
                _acousticPanicRadiusWS = 0f;
                _acousticPanicStrength01 = 0f;
            }
            float acousticPanicTimeRemaining = math.max(0f, _acousticPanicExpireTime - absoluteSimulationTime);

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
                _activeMassiveThreatCount,
                _debugFormationBeaconCount);
            int gridBoundRange = math.max(math.max(_spatialGridResolution.x, math.max(_spatialGridResolution.y, _spatialGridResolution.z)) - 1, 0);
            int pbdNeighbourCellRange = math.min(
                4,
                math.min(
                    gridBoundRange,
                    math.max(1, CeilToIntPositive(math.max(boidBodyRadius * 2f, 0.001f) / math.max(_spatialGridCellSizeWS, 0.001f)))));
            frameConstants.Counts1 = new int4(_debugFormationObstacleCount, _leviathanPathNodeCount, (int)simulationLodTier, pbdNeighbourCellRange);
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
            frameConstants.CameraPosition = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, shouldRender ? 1f : -1f);
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
            float ecosystemSpeedScale = math.max(0.25f, _ecosystemSpeedMultiplier);
            float ecosystemCamouflageScale = math.lerp(1f, ecosystemCamouflageWeight, _ecosystemCamouflageIndex);
            float ecosystemFitnessScale = math.lerp(1f, 1.15f, _ecosystemFitness);
            frameConstants.ThreatVoxelOrigin = new float4(
                _threatVoxelOriginWS.x,
                _threatVoxelOriginWS.y,
                _threatVoxelOriginWS.z,
                voxelAvoidanceLookAheadDistance * math.lerp(1f, ecosystemSpeedScale, 0.5f));
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
            frameConstants.SubmarineWake0 = new float4(
                submarineWakePosition.x,
                submarineWakePosition.y,
                submarineWakePosition.z,
                submarineWakeRadius);
            frameConstants.SubmarineWake1 = new float4(
                submarineWakeVelocity.x,
                submarineWakeVelocity.y,
                submarineWakeVelocity.z,
                submarineWakeHalfLength);
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
                math.max(1f, fragmentationHalfDistance));
            frameConstants.SonarScatter0 = new float4(
                _sonarScatterOriginWS.x,
                _sonarScatterOriginWS.y,
                _sonarScatterOriginWS.z,
                _sonarScatterWaveFrontWS);
            frameConstants.AcousticPanic0 = new float4(
                _acousticPanicOriginWS.x,
                _acousticPanicOriginWS.y,
                _acousticPanicOriginWS.z,
                acousticPanicStrength01 > 0f ? _acousticPanicRadiusWS : 0f);
            frameConstants.AcousticPanic1 = new float4(
                _acousticPanicSeed,
                acousticPanicStrength01,
                acousticPanicTimeRemaining,
                0f);
            frameConstants.AbyssalFlowWeatherCurrent = new float4(abyssalFlowWeatherCurrent.x, abyssalFlowWeatherCurrent.y, abyssalFlowWeatherCurrent.z, 0f);
            frameConstants.PlayerDirection = new float4(playerDirection.x, playerDirection.y, playerDirection.z, 0f);

            try
            {
                _simulationFrameNative[0] = frameConstants;
                GraphicsBufferUploadUtility.UploadNativeArray(_simulationFrameBuffer, _simulationFrameNative, 1);
                if (!EnsureStaticComputeBufferBindings())
                    return false;

                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferReadId, readBuffer);
                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferWriteId, writeBuffer);

                SetMainKernelTextureIfChanged(_DensityTexId, densityTexture, ref _boundComputeDensityTexture);
                SetMainKernelTextureIfChanged(_CutMaskTexId, activeCutMaskTexture, ref _boundComputeCutMaskTexture);
                SetMainKernelTextureIfChanged(_AbyssalFlowFieldTextureId, abyssalFlowTexture, ref _boundAbyssalFlowTexture);
                boidCompute.SetVector(_AbyssalFlowCenterId, abyssalFlowCenter);
                boidCompute.SetVector(_AbyssalFlowSpacingId, abyssalFlowSpacing);
                boidCompute.SetFloat(_AbyssalFlowActiveId, abyssalFlowActive);
                boidCompute.SetFloat(_AbyssalFlowWeightId, 1f);

                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _BoidsBufferReadId, readBuffer);

                boidCompute.SetBuffer(_pbdSolveKernelIndex, _BoidsBufferReadId, readBuffer);
            }
            catch (Exception)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
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

        void ISargassumGlobalDragEventListener.OnSargassumEntanglementStrain(in SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
        }

        private void ResolvePlayerGpuFrame(
            out Vector3 playerPosition,
            out Vector3 playerVelocity,
            out Vector3 playerRight,
            out Vector3 playerUp,
            out Vector3 playerForward,
            out Vector3 cameraPosition)
        {
            playerPosition = _fieldCenter;
            playerVelocity = Vector3.zero;
            playerForward = Vector3.forward;
            playerUp = Vector3.up;
            playerRight = Vector3.right;
            cameraPosition = _fieldCenter;

            if (!TryResolvePlayerRuntimeSnapshot(
                    out PlayerMovementRuntimeState movementState,
                    out PlayerLookState lookState))
            {
                return;
            }

            bool hasMovementRoot = (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            bool hasLookRoot = (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            if (!hasMovementRoot && !hasLookRoot)
                return;

            if (hasLookRoot)
                playerPosition = ToVector3(lookState.EyePosition);
            else
                playerPosition = ToVector3(movementState.WorldPosition);

            if (hasMovementRoot)
                playerVelocity = ToVector3(movementState.Velocity);

            Vector3 runtimeForward = hasLookRoot ? ToVector3(lookState.AimForward) : Vector3.zero;
            if (runtimeForward.sqrMagnitude <= 0.0001f && hasMovementRoot)
                runtimeForward = ToVector3(movementState.CameraForward);
            if (runtimeForward.sqrMagnitude <= 0.0001f && hasMovementRoot)
                runtimeForward = ToVector3(movementState.Forward);

            playerForward = FastNormalizeVector3(runtimeForward, Vector3.forward);
            playerRight = ResolveApproxRight(playerForward);
            cameraPosition = hasLookRoot ? ToVector3(lookState.EyePosition) : playerPosition;
            if (cameraPosition.sqrMagnitude <= 0.0001f)
                cameraPosition = playerPosition;
        }

        private bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            return TryResolvePlayerRuntimeMotion(out playerPosition, out _);
        }

        private bool TryResolvePlayerRuntimeMotion(out Vector3 playerPosition, out Vector3 playerVelocity)
        {
            int currentFrame = Time.frameCount;
            if (_playerMotionCacheFrame != currentFrame)
            {
                _playerMotionCacheFrame = currentFrame;
                _playerMotionCacheValid = TryResolvePlayerRuntimeMotionUncached(
                    out _playerMotionCachePosition,
                    out _playerMotionCacheVelocity);
            }

            playerPosition = _playerMotionCachePosition;
            playerVelocity = _playerMotionCacheVelocity;
            return _playerMotionCacheValid;
        }

        private bool TryResolvePlayerRuntimeMotionUncached(out Vector3 playerPosition, out Vector3 playerVelocity)
        {
            playerPosition = Vector3.zero;
            playerVelocity = Vector3.zero;
            if (!TryResolvePlayerRuntimeSnapshot(
                    out PlayerMovementRuntimeState movementState,
                    out PlayerLookState lookState))
            {
                return false;
            }

            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                playerPosition = ToVector3(movementState.WorldPosition);
                playerVelocity = ToVector3(movementState.Velocity);
                return true;
            }

            if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
                return false;

            playerPosition = ToVector3(lookState.EyePosition);
            return true;
        }

        void ISargassumGlobalDragEventListener.OnSargassumMassiveDisplacement(in SargassumGlobalDragManager.MassiveDisplacementSignal signal)
        {
            HandleMassiveDisplacement(in signal);
        }

        private void HandleMassiveDisplacement(in SargassumGlobalDragManager.MassiveDisplacementSignal signal)
        {
            if (_massiveThreats == null || _massiveThreats.Length == 0)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float panicRadius = math.max(massiveThreatPanicRadius, math.max(signal.ExtremePanicRadiusWS, signal.RadiusWS * 3f));
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

                float planarDeltaX = threat.Position.x - signal.PositionWS.x;
                float planarDeltaZ = threat.Position.z - signal.PositionWS.z;
                float planarDistanceSq = (planarDeltaX * planarDeltaX) + (planarDeltaZ * planarDeltaZ);
                float mergeDistance = math.max(threat.PanicRadius, panicRadius) * 0.4f;
                if (planarDistanceSq <= mergeDistance * mergeDistance)
                {
                    targetIndex = i;
                    Vector3 delta = signal.PositionWS - threat.Position;
                    if (delta.sqrMagnitude > 0.0001f)
                        inferredDirectionWS = FastNormalizeVector3(delta, Vector3.forward);
                    else if (threat.DirectionWS.sqrMagnitude > 0.0001f)
                        inferredDirectionWS = FastNormalizeVector3(threat.DirectionWS, Vector3.forward);
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
                TryResolvePlayerRuntimeMotion(out Vector3 playerPosition, out Vector3 playerVelocity))
            {
                AbsoluteUniversePosition signalAup = AbsoluteUniversePosition.FromRuntimePosition(signal.PositionWS);
                AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
                if (AbsoluteUniversePosition.DistanceSq(in signalAup, in playerAup) <= (double)panicRadius * panicRadius &&
                    playerVelocity.sqrMagnitude > 0.0001f)
                {
                    inferredDirectionWS = FastNormalizeVector3(playerVelocity, Vector3.forward);
                }
            }

            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = signal.PositionWS,
                InnerRadius = math.max(0.5f, signal.RadiusWS),
                PanicRadius = panicRadius,
                Strength = 1f,
                EndTime = absoluteSimulationTime + math.max(0.25f, signal.Duration),
                DirectionWS = inferredDirectionWS,
                ThreatFlags = (uint)MassiveThreatFlags.None
            };

            RecalculateMassiveThreatCount();
            UploadMassiveThreats();

            AbyssalFluidDecalManager fluidDecals = Hecton8.Core.GlobalRegistry.AbyssalFluidDecals;
            if ((_deepModeActive || _parasiteModeActive || _formationModeActive || _leviathanModeActive) && fluidDecals != null)
            {
                float ruptureScale = math.saturate(signal.RadiusWS / math.max(1f, deepBaitBallRadius * 2f));
                fluidDecals.RegisterRuptureFluid(signal.PositionWS, ruptureScale);
            }

            float headVelocitySq = _leviathanHeadVelocityWS.sqrMagnitude;
            Vector3 displacementDirection = headVelocitySq > 0.0001f
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

                float mergeDistance = math.max(threat.PanicRadius, panicRadiusWS) * 0.35f;
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

            Vector3 resolvedDirection = FastNormalizeVector3(directionWS, Vector3.forward);
            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = positionWS,
                InnerRadius = math.max(0.5f, boidBodyRadius * 2f),
                PanicRadius = math.max(4f, panicRadiusWS),
                Strength = 1f,
                EndTime = absoluteSimulationTime + math.max(0.15f, durationSeconds),
                DirectionWS = resolvedDirection,
                ThreatFlags = (uint)MassiveThreatFlags.LeviathanHuntPulse
            };

            RecalculateMassiveThreatCount();
            UploadMassiveThreats();
        }

        internal void RegisterPredatorFearBurst(
            Vector3 positionWS,
            Vector3 directionWS,
            float panicRadiusWS,
            float durationSeconds,
            float strength01)
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

                if ((threat.ThreatFlags & (uint)MassiveThreatFlags.LeviathanHuntPulse) != 0u)
                    continue;

                float mergeDistance = math.max(threat.PanicRadius, panicRadiusWS) * 0.45f;
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

            Vector3 resolvedDirection = FastNormalizeVector3(directionWS, Vector3.forward);
            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = positionWS,
                InnerRadius = math.max(0.5f, boidBodyRadius * 1.5f),
                PanicRadius = math.max(3f, panicRadiusWS),
                Strength = math.saturate(strength01),
                EndTime = absoluteSimulationTime + math.max(0.15f, durationSeconds),
                DirectionWS = resolvedDirection,
                ThreatFlags = 0u
            };

            RecalculateMassiveThreatCount();
            UploadMassiveThreats();
        }

        internal int RegisterPredatorConsumptionBurst(
            Vector3 predatorPositionWS,
            Vector3 biteCenterWS,
            float biteRangeMeters,
            uint predatorId,
            float currentTimeSeconds)
        {
            if (!_boidStateNative.IsCreated ||
                !_killSignals.IsCreated ||
                _spawnData == null ||
                _singleBoidUpload == null ||
                _activeBoidCount <= 0)
            {
                return 0;
            }

            if (_predatorConsumptionJobPending)
            {
                if (DispatcherJobSwap.TryFinalizeCompleted(ref _predatorConsumptionHandle))
                {
                    _predatorConsumptionJobPending = false;
                    int finalizedDrainCount = DrainPredatorKillSignals(_pendingPredatorConsumptionTimeSeconds);
                    RecordFoodChainTelemetry(
                        FoodChainTelemetryFlagKillJobCompleted | (finalizedDrainCount > 0 ? FoodChainTelemetryFlagKillDrained : 0u),
                        biteCenterWS,
                        predatorId,
                        0u);
                }
                else
                {
                    return 0;
                }
            }

            float safeBiteRange = math.max(0.05f, biteRangeMeters);
            while (_killSignals.TryDequeue(out _))
            {
            }

            var killJob = new PredatorBoidConsumptionJob
            {
                Boids = _boidStateNative,
                KillSignals = _killSignals.AsParallelWriter(),
                PredatorPositionWS = new float3(predatorPositionWS.x, predatorPositionWS.y, predatorPositionWS.z),
                BiteCenterWS = new float3(biteCenterWS.x, biteCenterWS.y, biteCenterWS.z),
                BiteRangeSq = safeBiteRange * safeBiteRange,
                FearRadiusMeters = PredatorKillDefaultFearRadiusMeters,
                FearAmount = PredatorKillFearAmount,
                PredatorId = predatorId,
                ActiveBoidCount = _activeBoidCount,
                MaxKills = PredatorKillSignalDrainLimit
            };

            _predatorConsumptionHandle = killJob.Schedule();
            _predatorConsumptionJobPending = true;
            _pendingPredatorConsumptionTimeSeconds = currentTimeSeconds;
            JobHandle.ScheduleBatchedJobs();
            RecordFoodChainTelemetry(
                FoodChainTelemetryFlagKillJobScheduled,
                biteCenterWS,
                predatorId,
                0u);
            return 0;
        }

        private int CompletePendingPredatorConsumption(bool forceComplete)
        {
            if (!_predatorConsumptionJobPending)
                return 0;

            if (!DispatcherJobSwap.TryComplete(ref _predatorConsumptionHandle, forceComplete))
                return 0;

            _predatorConsumptionJobPending = false;
            int drainedCount = DrainPredatorKillSignals(_pendingPredatorConsumptionTimeSeconds);
            RecordFoodChainTelemetry(
                FoodChainTelemetryFlagKillJobCompleted | (drainedCount > 0 ? FoodChainTelemetryFlagKillDrained : 0u),
                _fieldCenter,
                unchecked((uint)math.max(0, drainedCount)),
                0u);
            return drainedCount;
        }

        private int DrainPredatorKillSignals(float currentTimeSeconds)
        {
            int drainedCount = 0;
            Vector3 frenzyCentroid = Vector3.zero;
            while (_killSignals.TryDequeue(out BoidKillSignal killSignal))
            {
                int boidId = killSignal.BoidId;
                if (boidId < 0 ||
                    boidId >= _activeBoidCount ||
                    boidId >= _boidStateNative.Length ||
                    boidId >= _spawnData.Length)
                {
                    continue;
                }

                BoidData boid = _boidStateNative[boidId];
                if ((boid.StateFlags & ConsumedBoidStateFlag) != 0u)
                    continue;

                Vector3 killPositionWS = new Vector3(killSignal.KillPositionWS.x, killSignal.KillPositionWS.y, killSignal.KillPositionWS.z);
                if (!float.IsFinite(killPositionWS.x) ||
                    !float.IsFinite(killPositionWS.y) ||
                    !float.IsFinite(killPositionWS.z))
                {
                    continue;
                }

                boid.Panic = 0f;
                boid.Velocity = Vector3.zero;
                boid.StateFlags = (boid.StateFlags & BoidVisualMutationMask) | ConsumedBoidStateFlag;
                _boidStateNative[boidId] = boid;
                _spawnData[boidId] = boid;
                _singleBoidUpload[0] = boid;
                UploadSingleBoidToLiveBuffers(boidId);

                PublishPredatorKillDebris(in killSignal, killPositionWS, boidId);
                RecordFoodChainTelemetry(
                    FoodChainTelemetryFlagKillDrained,
                    killPositionWS,
                    killSignal.PredatorId,
                    0u);
                RegisterPredatorFearBurst(
                    killPositionWS,
                    killPositionWS - new Vector3(killSignal.PredatorPositionWS.x, killSignal.PredatorPositionWS.y, killSignal.PredatorPositionWS.z),
                    math.max(3f, killSignal.FearRadiusMeters),
                    PredatorKillFearDurationSeconds,
                    math.saturate(killSignal.FearAmount * 0.01f));

                frenzyCentroid += killPositionWS;
                drainedCount++;
                _debugConsumedBoidCount++;
            }

            if (drainedCount > 0)
                TryPublishFeedingFrenzyAcousticPing(frenzyCentroid * math.rcp((float)drainedCount), currentTimeSeconds, drainedCount);

            return drainedCount;
        }

        private static void PublishPredatorKillDebris(in BoidKillSignal killSignal, Vector3 killPositionWS, int boidId)
        {
            uint sourceId = killSignal.PredatorId != 0u
                ? killSignal.PredatorId
                : (uint)math.hash(new int2(boidId, (int)Time.frameCount));
            DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(killPositionWS),
                SpeciesHash = (uint)math.hash(new int2(boidId, (int)(sourceId & 0x7FFFFFFFu))),
                SourceEntityId = sourceId,
                Intensity01 = 1f,
                DebrisKind = PredatorKillBloodDebrisKind,
                Flags = PredatorKillDebrisFlags
            };
            GlobalSignals.Publish(in debrisSignal);

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals != null)
                fluidDecals.RegisterRuptureFluid(killPositionWS, PredatorKillFluidDecalRadiusScale);
        }

        private void TryPublishFeedingFrenzyAcousticPing(Vector3 centroidWS, float currentTimeSeconds, int killCount)
        {
            float safeTime = math.max(0f, currentTimeSeconds);
            if (_feedingFrenzyWindowStartTime < 0f ||
                safeTime - _feedingFrenzyWindowStartTime > FeedingFrenzyWindowSeconds)
            {
                _feedingFrenzyWindowStartTime = safeTime;
                _feedingFrenzyKillCount = 0;
            }

            _feedingFrenzyKillCount += math.max(0, killCount);
            if (_feedingFrenzyKillCount <= FeedingFrenzyKillThreshold)
                return;

            AcousticPingSignal acousticPingSignal = new AcousticPingSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(centroidWS),
                RadiusMeters = FeedingFrenzyAcousticRadiusMeters,
                Intensity01 = math.saturate(_feedingFrenzyKillCount * PredatorKillSignalDrainLimitInv),
                SourceId = math.hash(new float3(centroidWS.x, centroidWS.y, centroidWS.z)),
                Channel = FeedingFrenzyAcousticChannel,
                Flags = FeedingFrenzyAcousticFlags
            };
            GlobalSignals.Publish(in acousticPingSignal);
            _feedingFrenzyKillCount = 0;
            _feedingFrenzyWindowStartTime = safeTime;
        }

        internal int RegisterWhaleFallScavengerBurst(Vector3 centerWS, uint sourceId, float currentTimeSeconds)
        {
            if (!_boidStateNative.IsCreated ||
                _spawnData == null ||
                _singleBoidUpload == null ||
                _activeBoidCount <= 0 ||
                _lastSimulationLodTier != SimulationLodTier.Full)
            {
                RegisterPredatorFearBurst(centerWS, Vector3.forward, WhaleFallScavengerRadiusMeters, 4f, 0.35f);
                RecordFoodChainTelemetry(FoodChainTelemetryFlagWhaleFall, centerWS, sourceId, 0u);
                return 0;
            }

            int safeActiveCount = math.min(math.min(_activeBoidCount, _boidStateNative.Length), _spawnData.Length);
            int visualCount = math.clamp(WhaleFallScavengerVisualCount, 0, safeActiveCount);
            if (visualCount <= 0)
                return 0;

            uint safeSourceId = sourceId != 0u ? sourceId : math.hash(new float3(centerWS.x, centerWS.y, centerWS.z));
            int startIndex = (int)(safeSourceId % (uint)safeActiveCount);
            for (int i = 0; i < visualCount; i++)
            {
                int boidId = (startIndex + i) % safeActiveCount;
                uint ringHash = math.hash(new int2((int)(safeSourceId & 0x7FFFFFFFu), i));
                float radius01 = ((ringHash >> 8) & 1023u) * WhaleFallScavengerRadiusHashInv;
                float angle = (i + ((ringHash & 255u) * WhaleFallScavengerAngleHashInv)) * StatisticalFibonacciGoldenAngle;
                float radius = math.lerp(2f, WhaleFallScavengerRadiusMeters, radius01 * radius01);
                Vector3 radial = new Vector3(CheapCosSigned(angle), 0f, CheapSinSigned(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 positionWS = centerWS + radial * radius;
                positionWS = ResolveWhaleFallGroundHuggingPosition(positionWS);

                BoidData boid = _boidStateNative[boidId];
                boid.Position = positionWS;
                boid.Velocity = tangent * WhaleFallScavengerTangentSpeedMetersPerSecond;
                boid.Panic = 0f;
                boid.StateFlags = (boid.StateFlags & BoidVisualMutationMask) | DefaultBoidStateFlags;
                _boidStateNative[boidId] = boid;
                _spawnData[boidId] = boid;
                _singleBoidUpload[0] = boid;
                UploadSingleBoidToLiveBuffers(boidId);
            }

            _fieldCenter = centerWS;
            _fieldExtents = new Vector3(WhaleFallScavengerRadiusMeters * 1.35f, 2f, WhaleFallScavengerRadiusMeters * 1.35f);
            _renderBounds = new Bounds(_fieldCenter, _fieldExtents * 2f);
            _debugRenderBounds = _renderBounds;
            RegisterPredatorFearBurst(centerWS, Vector3.forward, WhaleFallScavengerRadiusMeters, 2f, 0.2f);
            RecordFoodChainTelemetry(FoodChainTelemetryFlagWhaleFall, centerWS, safeSourceId, 0u);
            return visualCount;
        }

        private Vector3 ResolveWhaleFallGroundHuggingPosition(Vector3 positionWS)
        {
            HectonMapMagicVegetationBridge vegetationBridge = _mapMagicVegetationBridge != null
                ? _mapMagicVegetationBridge
                : HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null &&
                vegetationBridge.TryGetCachedTerrainHeight(positionWS.x, positionWS.z, out float terrainHeight))
            {
                positionWS.y = terrainHeight + WhaleFallScavengerGroundOffsetMeters;
                _mapMagicVegetationBridge = vegetationBridge;
                return positionWS;
            }

            positionWS.y += WhaleFallScavengerGroundOffsetMeters;
            return positionWS;
        }

        private void UploadSingleBoidToLiveBuffers(int boidId)
        {
            if (_singleBoidUpload == null || boidId < 0)
                return;

            UploadSingleBoidToBuffer(_boidsBufferA, _singleBoidUpload, boidId);
            UploadSingleBoidToBuffer(_boidsBufferB, _singleBoidUpload, boidId);
        }

        private static void UploadSingleBoidToBuffer(GraphicsBuffer buffer, BoidData[] source, int boidId)
        {
            if (buffer == null ||
                source == null ||
                source.Length <= 0 ||
                boidId < 0 ||
                boidId >= buffer.count)
            {
                return;
            }

            NativeArray<BoidData> mapped = buffer.LockBufferForWrite<BoidData>(boidId, 1);
            mapped[0] = source[0];
            buffer.UnlockBufferAfterWrite<BoidData>(1);
        }

        private void RecordFoodChainTelemetry(uint flags, Vector3 eventPositionWS, uint sourceHash, uint anomalyHash)
        {
            if (!_foodChainTelemetryRing.IsCreated)
                return;

            Vector3 safeFieldCenter = _fieldCenter;
            Vector3 safeEventPosition = eventPositionWS;
            if (!IsFiniteVector3(safeFieldCenter))
            {
                safeFieldCenter = Vector3.zero;
                anomalyHash = FoodChainTelemetryAnomalyNonFinite;
                flags |= FoodChainTelemetryFlagNonFinite;
            }

            if (!IsFiniteVector3(safeEventPosition))
            {
                safeEventPosition = safeFieldCenter;
                anomalyHash = FoodChainTelemetryAnomalyNonFinite;
                flags |= FoodChainTelemetryFlagNonFinite;
            }

            int writeIndex = _foodChainTelemetryCursor;
            int nextCursor = writeIndex + 1;
            if (nextCursor >= FoodChainTelemetryCapacity)
                nextCursor = 0;

            _foodChainTelemetryRing[writeIndex] = new FoodChainTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                StateHash = math.hash(new uint4(
                    unchecked((uint)math.max(0, _activeBoidCount)),
                    unchecked((uint)math.max(0, _debugConsumedBoidCount)),
                    unchecked((uint)_lastSimulationLodTier),
                    flags)),
                SourceHash = sourceHash,
                Flags = flags,
                ActiveBoidCount = _activeBoidCount,
                ConsumedBoidCount = _debugConsumedBoidCount,
                PendingKillJob = _predatorConsumptionJobPending ? 1 : 0,
                LodTier = (int)_lastSimulationLodTier,
                FieldCenterWS = new float3(safeFieldCenter.x, safeFieldCenter.y, safeFieldCenter.z),
                EventPositionWS = new float3(safeEventPosition.x, safeEventPosition.y, safeEventPosition.z),
                AnomalyHash = anomalyHash,
                SimulationTime = _simulationTime
            };
            _foodChainTelemetryCursor = nextCursor;

            if (anomalyHash != 0u)
                TryDumpFoodChainTelemetry(anomalyHash);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void TryDumpFoodChainTelemetry(uint anomalyHash)
        {
            if (_foodChainTelemetryDumped || !_foodChainTelemetryRing.IsCreated)
                return;

            _foodChainTelemetryDumped = true;
            string dumpPath = Path.Combine(
                Application.dataPath,
                "..",
                "Docs",
                "AgentLogs",
                "Dump_ECOSYSTEM_FOOD_CHAIN.bin");
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(FoodChainTelemetryMagicLow);
                writer.Write(FoodChainTelemetryMagicHigh);
                writer.Write((uint)FoodChainTelemetryCapacity);
                writer.Write((uint)FoodChainTelemetryEntrySizeBytes);
                writer.Write((uint)_foodChainTelemetryCursor);
                writer.Write(anomalyHash);

                for (int i = 0; i < FoodChainTelemetryCapacity; i++)
                    WriteFoodChainTelemetryEntry(writer, _foodChainTelemetryRing[i]);
            }
        }

        private static void WriteFoodChainTelemetryEntry(BinaryWriter writer, in FoodChainTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.StateHash);
            writer.Write(entry.SourceHash);
            writer.Write(entry.Flags);
            writer.Write(entry.ActiveBoidCount);
            writer.Write(entry.ConsumedBoidCount);
            writer.Write(entry.PendingKillJob);
            writer.Write(entry.LodTier);
            writer.Write(entry.FieldCenterWS.x);
            writer.Write(entry.FieldCenterWS.y);
            writer.Write(entry.FieldCenterWS.z);
            writer.Write(entry.EventPositionWS.x);
            writer.Write(entry.EventPositionWS.y);
            writer.Write(entry.EventPositionWS.z);
            writer.Write(entry.AnomalyHash);
            writer.Write(entry.SimulationTime);
        }

        /// <summary>
        /// Registers a GPU-only VAT hit reaction. Rendering owns the timestamp; no boid buffer mutation or CPU animation path is used.
        /// </summary>
        internal void RegisterVatHitReaction(Vector3 originWS, float radiusMeters, float intensity01)
        {
            float clampedIntensity = math.saturate(intensity01) * math.saturate(hitFlashIntensity);
            if (clampedIntensity <= 0.0001f ||
                !float.IsFinite(originWS.x) ||
                !float.IsFinite(originWS.y) ||
                !float.IsFinite(originWS.z))
            {
                return;
            }

            _hitFlashOriginWS = originWS;
            _hitFlashRuntimeRadius = math.max(0f, radiusMeters > 0.0001f ? radiusMeters : hitFlashRadiusMeters);
            _hitFlashRuntimeIntensity = clampedIntensity;
            _hitFlashStartTime = Time.time;
            _hitFlashPropertiesDirty = true;
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
                float playerVelocitySq = playerVelocity.sqrMagnitude;
                Vector3 dashDirection = playerVelocitySq > 0.0001f ? playerVelocity : playerForward;
                TriggerFragmentation(playerPosition, dashDirection, math.max(panicPlayerRadius, boidBodyRadius * 6f), absoluteSimulationTime);
            }

            float leviathanHeadVelocitySq = _leviathanHeadVelocityWS.sqrMagnitude;
            float leviathanShockwaveThresholdSq = leviathanShockwaveSpeedThreshold * leviathanShockwaveSpeedThreshold;
            if (_leviathanHeadValid &&
                leviathanHeadVelocitySq >= leviathanShockwaveThresholdSq)
            {
                TriggerFragmentation(
                    _leviathanHeadPositionWS,
                    _leviathanHeadVelocityWS,
                    math.max(_leviathanHeadRadiusWS * 2.5f, leviathanShockwaveRadius * 0.45f),
                    absoluteSimulationTime);
            }

            if (absoluteSimulationTime >= _fragmentationExpireTime)
            {
                _fragmentationStartTime = float.NegativeInfinity;
                _fragmentationExpireTime = float.NegativeInfinity;
                _fragmentationHalfDistanceWS = 0f;
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

            _sonarScatterWaveFrontWS += math.max(0f, simulationDt) * math.max(0.1f, activeSonarWaveSpeed);
        }

        private float ResolveFragmentationStrength01(float absoluteSimulationTime)
        {
            if (absoluteSimulationTime >= _fragmentationExpireTime ||
                !float.IsFinite(_fragmentationExpireTime) ||
                !float.IsFinite(_fragmentationStartTime))
                return 0f;

            float duration = math.max(0.1f, _fragmentationExpireTime - _fragmentationStartTime);
            float timeRemaining = math.max(0f, _fragmentationExpireTime - absoluteSimulationTime);
            return math.saturate(timeRemaining / math.max(0.1f, duration));
        }

        private void TriggerFragmentation(Vector3 originWS, Vector3 dashVectorWS, float baseRadiusWS, float absoluteSimulationTime)
        {
            float dashVectorSq = dashVectorWS.sqrMagnitude;
            Vector3 dashDirection = dashVectorSq > 0.0001f ? FastNormalizeVector3(dashVectorWS, Vector3.forward) : Vector3.forward;
            Vector3 splitAxis = ResolveApproxRight(dashDirection);

            float offsetDistance = math.max(1f, baseRadiusWS * math.max(0.5f, fragmentationOffsetScale));
            _fragmentationCenterAWS = originWS + splitAxis * offsetDistance;
            _fragmentationCenterBWS = originWS - splitAxis * offsetDistance;
            _fragmentationHalfDistanceWS = offsetDistance;
            float safeMinDuration = math.max(5f, fragmentationMinDurationSeconds);
            float safeMaxDuration = math.max(safeMinDuration, fragmentationMaxDurationSeconds);
            float panicSpeedSq = math.max(0.01f, panicPlayerSpeedThreshold * panicPlayerSpeedThreshold);
            float duration01 = math.saturate(dashVectorSq / panicSpeedSq);
            _fragmentationStartTime = absoluteSimulationTime;
            _fragmentationExpireTime = absoluteSimulationTime + math.lerp(safeMinDuration, safeMaxDuration, duration01);
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

        /// <inheritdoc />
        public void OnFlashlightEvent(in Hecton8.Gameplay.FlashlightEventPayload payload)
        {
            switch ((Hecton8.Gameplay.FlashlightEventType)payload.EventType)
            {
                case Hecton8.Gameplay.FlashlightEventType.Toggled:
                case Hecton8.Gameplay.FlashlightEventType.FlickerStart:
                    HandleFlashlightToggled(payload.IsOn);
                    break;
                case Hecton8.Gameplay.FlashlightEventType.BatteryDepleted:
                case Hecton8.Gameplay.FlashlightEventType.Overheat:
                    HandleFlashlightToggled(false);
                    break;
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity <= 0f)
            {
                _sonarScatterStrength01 = 0f;
                _debugSonarScatter01 = 0f;
                return;
            }

            Vector3 originWS = TryResolvePlayerRuntimePosition(out Vector3 playerPosition) ? playerPosition : _fieldCenter;
            float maxFieldExtent = math.max(_fieldExtents.x, math.max(_fieldExtents.y, _fieldExtents.z));
            float safeWaveSpeed = math.max(0.1f, activeSonarWaveSpeed);
            float travelDistance = (maxFieldExtent * 2f) + math.max(0.25f, activeSonarWaveBandWidth);

            _sonarScatterOriginWS = originWS;
            _sonarScatterWaveFrontWS = 0f;
            _sonarScatterStrength01 = clampedIntensity;
            _sonarScatterExpireTime = GetAbsoluteSimulationTime() + (travelDistance / safeWaveSpeed);
            _debugSonarScatter01 = clampedIntensity;
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        internal void RegisterAcousticPanicBurst(
            Vector3 originWS,
            float radiusWS,
            float durationSeconds,
            float strength01,
            uint seed)
        {
            float clampedStrength = math.saturate(strength01);
            if (radiusWS <= 0.001f || durationSeconds <= 0.001f || clampedStrength <= 0.0001f)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            _acousticPanicOriginWS = originWS;
            _acousticPanicRadiusWS = math.max(1f, radiusWS);
            _acousticPanicStrength01 = math.max(_acousticPanicStrength01, clampedStrength);
            _acousticPanicExpireTime = math.max(
                _acousticPanicExpireTime,
                absoluteSimulationTime + math.max(0.1f, durationSeconds));
            _acousticPanicSeed = seed != 0u ? seed : 0x9E3779B9u;
        }

        private float ResolveHeadlightPanic01()
        {
            if (!_deepModeActive || deepHeadlightPanicDuration <= 0.0001f)
                return 0f;

            return math.saturate(_headlightPanicTimer / deepHeadlightPanicDuration);
        }

        private void ApplyParasiteHullStress()
        {
            if (_playerMovement == null || !_parasiteModeActive)
                return;

            float aggression01 = ResolveParasiteAggression01();
            if (aggression01 <= 0f)
                return;

            float requestedStress = math.saturate(math.lerp(parasiteHullStressIntensity, parasiteHullStressIntensity + parasiteHullStressLightBoost, aggression01));
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
                        ? math.clamp(latchData[LatchStatsLatchedCountIndex], 0, _activeBoidCount)
                        : 0;
                    if (_reportedLatchedDroneCount > 0 && latchData.Length >= LatchStatsElementCount)
                    {
                        float divisor = LatchStatsQuantize * math.max(1, _reportedLatchedDroneCount);
                        _reportedParasiteCenterOfMassLS = new Vector3(
                            latchData[LatchStatsLatchedSumXIndex] / divisor,
                            latchData[LatchStatsLatchedSumYIndex] / divisor,
                            latchData[LatchStatsLatchedSumZIndex] / divisor);
                    }
                    else
                    {
                        _reportedParasiteCenterOfMassLS = Vector3.zero;
                    }

                    Vector3 playerPosition = TryResolvePlayerRuntimePosition(out Vector3 resolvedPlayerPosition) ? resolvedPlayerPosition : _fieldCenter;
                    if (_reportedLatchedDroneCount >= parasiteHarvesterLatchThreshold &&
                        TryResolveNearestHarvesterAnchor(playerPosition, out Vector3 harvesterAnchorWS))
                    {
                        _reportedParasiteHarvesterPullWS = FastNormalizeVector3(harvesterAnchorWS - playerPosition, Vector3.zero);
                    }
                    else
                    {
                        _reportedParasiteHarvesterPullWS = Vector3.zero;
                    }

                    _debugLatchedDroneCount = _reportedLatchedDroneCount;
                    _debugParasiteCenterOfMassLS = _reportedParasiteCenterOfMassLS;
                    _debugParasiteHarvesterPullWS = _reportedParasiteHarvesterPullWS;

                    _reportedWakeFleeCount = latchData.Length > LatchStatsWakeCountIndex
                        ? math.clamp(latchData[LatchStatsWakeCountIndex], 0, _activeBoidCount)
                        : 0;
                    if (_reportedWakeFleeCount >= WakeMinimumFleeBoids)
                    {
                        float wakeDivisor = WakeStatsQuantize * math.max(1, _reportedWakeFleeCount);
                        _reportedWakeCenterWS = new Vector3(
                            latchData[LatchStatsWakePosXIndex] / wakeDivisor,
                            latchData[LatchStatsWakePosYIndex] / wakeDivisor,
                            latchData[LatchStatsWakePosZIndex] / wakeDivisor);
                        Vector3 averageWakeVelocity = new Vector3(
                            latchData[LatchStatsWakeVelXIndex] / wakeDivisor,
                            latchData[LatchStatsWakeVelYIndex] / wakeDivisor,
                            latchData[LatchStatsWakeVelZIndex] / wakeDivisor);
                        _reportedWakeFlowDirectionWS = FastNormalizeVector3(averageWakeVelocity, Vector3.zero);

                        if (_reportedWakeFlowDirectionWS.sqrMagnitude > 0.0001f && _mapMagicVegetationBridge != null)
                        {
                            _mapMagicVegetationBridge.RegisterSwarmWakeImpulse(
                                _reportedWakeCenterWS,
                                _reportedWakeFlowDirectionWS * WakeFlowStrength,
                                WakeFlowRadius,
                                WakeFlowLifetimeSeconds);
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

            _parasiteLatchReadbackTimer -= math.max(0f, dt);
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

            float latch01 = math.saturate(_reportedLatchedDroneCount / math.max(1f, parasiteMaxLatchedDronesForFullDrag));
            _playerMovement.ApplyParasiteLatchInfluence(
                _reportedLatchedDroneCount,
                _reportedParasiteCenterOfMassLS,
                _reportedParasiteHarvesterPullWS);
            if (latch01 <= 0.0001f)
                return;

            float aggression01 = ResolveParasiteAggression01();
            float dragWeight = math.saturate(latch01 * math.lerp(0.65f, 1f, aggression01));
            float requestedDragMultiplier = math.lerp(1f, parasiteMaxEnvironmentalDragMultiplier, dragWeight);
            if (requestedDragMultiplier <= 1.0001f)
                return;

            _playerMovement.ApplyEnvironmentalDrag(requestedDragMultiplier);
        }

        private void ApplyLeviathanPhysicalStrike()
        {
            if ((_playerMovement == null && _playerHealth == null) || _leviathanStrikeCooldownTimer > 0f || !TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
                return;

            Vector3 toPlayer = playerPosition - _leviathanHeadPositionWS;
            if (toPlayer.sqrMagnitude > leviathanStrikeRadius * leviathanStrikeRadius)
                return;

            float leviathanHeadSpeedSq = _leviathanHeadVelocityWS.sqrMagnitude;
            Vector3 strikeDirection = leviathanHeadSpeedSq > 0.0001f
                ? FastNormalizeVector3(_leviathanHeadVelocityWS, _leviathanHeadForwardWS)
                : _leviathanHeadForwardWS;
            if (strikeDirection.sqrMagnitude <= 0.0001f)
                strikeDirection = Vector3.forward;

            float leviathanShockwaveSpeedThresholdSq = math.max(0.01f, leviathanShockwaveSpeedThreshold * leviathanShockwaveSpeedThreshold);
            float speed01 = math.saturate(leviathanHeadSpeedSq / leviathanShockwaveSpeedThresholdSq);
            Vector3 traumaImpulse = strikeDirection * (leviathanStrikeImpulse * math.lerp(0.8f, 1.35f, speed01));
            if (_playerMovement != null)
                _playerMovement.ApplyPhysicalTrauma(traumaImpulse, math.lerp(leviathanStrikeTraumaWeight * 0.65f, leviathanStrikeTraumaWeight, speed01));

            if (_playerHealth != null)
                _playerHealth.TakeLeviathanDamage(leviathanStrikeDamage);

            _leviathanStrikeCooldownTimer = leviathanStrikeCooldown;
        }

        private void ApplyLeviathanShockwave()
        {
            float speedSq = _leviathanHeadVelocityWS.sqrMagnitude;
            float thresholdSq = leviathanShockwaveSpeedThreshold * leviathanShockwaveSpeedThreshold;
            if (_leviathanShockwaveCooldownTimer > 0f || speedSq < thresholdSq)
                return;

            Vector3 headDirection = speedSq > 0.0001f
                ? FastNormalizeVector3(_leviathanHeadVelocityWS, _leviathanHeadForwardWS)
                : _leviathanHeadForwardWS;
            float speed01 = math.saturate(speedSq / math.max(0.001f, thresholdSq));
            float visualRadius = math.max(2f, leviathanShockwaveRadius * math.lerp(0.75f, 1.25f, speed01));
            float visualDuration = math.max(0.15f, leviathanShockwaveCadence + (leviathanShockwaveImpulse * 0.005f));
            RegisterLeviathanThreatPulse(_leviathanHeadPositionWS, headDirection, visualRadius, visualDuration);
            _leviathanShockwaveCooldownTimer = leviathanShockwaveCadence;
        }

        private bool TryResolveNearestHarvesterAnchor(Vector3 origin, out Vector3 anchorWS)
        {
            anchorWS = origin;
            if (_mapMagicVegetationBridge == null)
                return false;

            NativeArray<Vector3> anchors = _mapMagicVegetationBridge.ActiveAbyssalAnchorsNative;
            int anchorCount = _mapMagicVegetationBridge.ActiveAbyssalAnchorCount;
            if (!anchors.IsCreated || anchorCount <= 0)
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            double nearestDistanceSq = double.PositiveInfinity;
            int cappedCount = math.min(anchorCount, anchors.Length);
            for (int i = 0; i < cappedCount; i++)
            {
                Vector3 candidate = anchors[i];
                AbsoluteUniversePosition candidateAup = AbsoluteUniversePosition.FromRuntimePosition(candidate);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                anchorWS = candidate;
            }

            return !double.IsPositiveInfinity(nearestDistanceSq);
        }

        private void UpdateMassiveThreats()
        {
            if (_massiveThreats == null)
                return;

            int previousActiveThreatCount = _activeMassiveThreatCount;
            RecalculateMassiveThreatCount();
            if (previousActiveThreatCount != _activeMassiveThreatCount)
                UploadMassiveThreats();
        }

        private void DispatchClearLatchStats()
        {
            if (_latchStatsBuffer == null || boidCompute == null || _clearStatsKernelIndex < 0)
                return;

            boidCompute.Dispatch(_clearStatsKernelIndex, 1, 1, 1);
        }

        private bool ShouldCollectLatchStats(SimulationLodTier simulationLodTier, bool leaderFollowerSchooling)
        {
            return simulationLodTier == SimulationLodTier.Full &&
                   !leaderFollowerSchooling &&
                   _latchStatsBuffer != null;
        }

        private void UpdateSpatialGridLayout()
        {
            float baseCellSize = 2f;
            Vector3 doubledExtents = _fieldExtents * 2f;
            Vector3 fieldSize = new Vector3(
                math.max(doubledExtents.x, baseCellSize),
                math.max(doubledExtents.y, baseCellSize),
                math.max(doubledExtents.z, baseCellSize));
            float axisClampCellSize = math.max(
                fieldSize.x / SpatialGridMaxAxisResolution,
                math.max(fieldSize.y / SpatialGridMaxAxisResolution, fieldSize.z / SpatialGridMaxAxisResolution));
            _spatialGridCellSizeWS = math.max(baseCellSize, axisClampCellSize);

            Vector3 fieldMin = _fieldCenter - _fieldExtents;
            Vector3 fieldMax = _fieldCenter + _fieldExtents;
            // Negative-space-safe bounds_min anchor. The compute shader subtracts this origin before cell division.
            _spatialGridOriginWS = new Vector3(
                FloorToMultiple(fieldMin.x, _spatialGridCellSizeWS),
                FloorToMultiple(fieldMin.y, _spatialGridCellSizeWS),
                FloorToMultiple(fieldMin.z, _spatialGridCellSizeWS));

            int resolutionX = math.clamp(CeilToIntPositive((fieldMax.x - _spatialGridOriginWS.x) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            int resolutionY = math.clamp(CeilToIntPositive((fieldMax.y - _spatialGridOriginWS.y) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            int resolutionZ = math.clamp(CeilToIntPositive((fieldMax.z - _spatialGridOriginWS.z) / _spatialGridCellSizeWS), 1, SpatialGridMaxAxisResolution);
            _spatialGridResolution = new Vector3Int(resolutionX, resolutionY, resolutionZ);
            int cellCount = resolutionX * resolutionY * resolutionZ;
            _clearSpatialGridDispatchGroupCount = math.max(1, CeilDivPositive(cellCount, SpatialGridClearThreadGroupSize));
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
            if (!TryResolveApproxViewPose(out Vector3 cameraPosition, out _))
                return 0f;

            AbsoluteUniversePosition boundsAup = AbsoluteUniversePosition.FromRuntimePosition(_renderBounds.center);
            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromRuntimePosition(cameraPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in boundsAup, in cameraAup);
            return distanceSq >= float.MaxValue ? float.MaxValue : (float)math.max(0d, distanceSq);
        }

        private bool TryConsumeSimulationStep(
            float frameDeltaTime,
            float cameraDistanceSq,
            out float simulationDeltaTime,
            out float hibernation01,
            out SimulationLodTier simulationLodTier)
        {
            FoveatedSimulationDecision decision = default;
            if (_foveatedSimulationFrontNative.IsCreated && _foveatedSimulationFrontNative.Length > 0)
                decision = _foveatedSimulationFrontNative[0];

            simulationDeltaTime = math.max(0f, decision.SimulationDeltaTime);
            hibernation01 = math.saturate(decision.Hibernation01);
            simulationLodTier = (SimulationLodTier)math.clamp(decision.Tier, (int)SimulationLodTier.Full, (int)SimulationLodTier.Sleep);
            _sleepVelocityWritePending = simulationLodTier == SimulationLodTier.Sleep && _lastSimulationLodTier != SimulationLodTier.Sleep;
            _lastSimulationLodTier = simulationLodTier;
            ScheduleFoveatedSimulationDecision(frameDeltaTime, cameraDistanceSq, decision.Accumulator);
            return decision.DispatchSimulation != 0 && simulationDeltaTime > 0f;
        }

        private bool ShouldRenderSwarm(float cameraDistanceSq)
        {
            if (_activeBoidCount <= 0)
                return false;

            if (!TryResolveApproxViewPose(out _, out _))
                return true;

            float maxDistanceSq = simulationCullDistance * simulationCullDistance;
            if (cameraDistanceSq > maxDistanceSq)
                return false;

            if (cameraDistanceSq <= RenderConeCullNearDistanceSq)
                return true;

            return IsRenderBoundsInsideApproxViewCone();
        }

        private bool ShouldMaintainOffscreenBoidSimulation(SimulationLodTier simulationLodTier)
        {
            if (simulationLodTier == SimulationLodTier.Sleep)
                return true;

            return _leviathanModeBlend > 0.001f ||
                   _parasiteModeActive ||
                   _formationModeActive ||
                   _headlightPanicTimer > 0f;
        }

        private bool IsRenderBoundsInsideApproxViewCone()
        {
            if (!TryResolveApproxViewPose(out Vector3 cameraPosition, out Vector3 cameraForward))
                return true;

            Vector3 toCenter = _renderBounds.center - cameraPosition;
            float distanceSq = math.max(0.0001f, toCenter.sqrMagnitude);
            if (distanceSq <= RenderConeCullNearDistanceSq)
                return true;

            Vector3 safeForward = FastNormalizeVector3(cameraForward, Vector3.forward);
            float approxDistance = math.abs(toCenter.x) + math.abs(toCenter.y) + math.abs(toCenter.z);
            float inverseDistance = math.rcp(math.max(0.0001f, approxDistance));
            float dot = ((toCenter.x * safeForward.x) + (toCenter.y * safeForward.y) + (toCenter.z * safeForward.z)) * inverseDistance;
            Vector3 extents = _renderBounds.extents;
            float radius = math.max(extents.x, math.max(extents.y, extents.z)) * BoundsCubeSphereRadiusScale;
            float radiusPadding = radius * inverseDistance;
            return dot + radiusPadding >= RenderConeCullDotThreshold;
        }

        private bool TryResolveApproxViewPose(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            int currentFrame = Time.frameCount;
            if (_viewPoseCacheFrame == currentFrame)
            {
                cameraPosition = _viewPoseCachePosition;
                cameraForward = _viewPoseCacheForward;
                return _viewPoseCacheValid;
            }

            _viewPoseCacheFrame = currentFrame;
            _viewPoseCacheValid = TryResolveApproxViewPoseUncached(out _viewPoseCachePosition, out _viewPoseCacheForward);
            cameraPosition = _viewPoseCachePosition;
            cameraForward = _viewPoseCacheForward;
            return _viewPoseCacheValid;
        }

        private bool TryResolveApproxViewPoseUncached(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            if (TryResolvePlayerRuntimeSnapshot(
                    out PlayerMovementRuntimeState movementState,
                    out PlayerLookState lookState) &&
                (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                cameraPosition = ToVector3(lookState.EyePosition);
                cameraForward = ToVector3(lookState.AimForward);
                if (cameraForward.sqrMagnitude <= 0.0001f)
                    cameraForward = ToVector3(movementState.CameraForward);
                if (cameraForward.sqrMagnitude <= 0.0001f)
                    cameraForward = ToVector3(movementState.Forward);

                return cameraForward.sqrMagnitude > 0.0001f;
            }

            if (viewCamera != null)
            {
                Matrix4x4 cameraToWorld = viewCamera.cameraToWorldMatrix;
                Vector4 positionColumn = cameraToWorld.GetColumn(3);
                Vector4 forwardColumn = cameraToWorld.GetColumn(2);
                cameraPosition = new Vector3(positionColumn.x, positionColumn.y, positionColumn.z);
                cameraForward = new Vector3(forwardColumn.x, forwardColumn.y, forwardColumn.z);
                return cameraForward.sqrMagnitude > 0.0001f;
            }

            cameraPosition = default;
            cameraForward = default;
            return false;
        }

        private bool TryResolvePlayerRuntimeSnapshot(
            out PlayerMovementRuntimeState movementState,
            out PlayerLookState lookState)
        {
            int currentFrame = Time.frameCount;
            if (_playerRuntimeSnapshotCacheFrame != currentFrame)
            {
                _playerRuntimeSnapshotCacheFrame = currentFrame;
                if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                    runtimeContext != null)
                {
                    _playerRuntimeSnapshotMovement = runtimeContext.MovementState;
                    _playerRuntimeSnapshotLook = runtimeContext.LookState;
                    _playerRuntimeSnapshotCacheValid = true;
                }
                else
                {
                    _playerRuntimeSnapshotMovement = default;
                    _playerRuntimeSnapshotLook = default;
                    _playerRuntimeSnapshotCacheValid = false;
                }
            }

            movementState = _playerRuntimeSnapshotMovement;
            lookState = _playerRuntimeSnapshotLook;
            return _playerRuntimeSnapshotCacheValid;
        }

        private void InvalidateViewPoseCache()
        {
            _viewPoseCacheFrame = -1;
            _viewPoseCacheValid = false;
            _viewPoseCachePosition = default;
            _viewPoseCacheForward = default;
            _playerRuntimeSnapshotCacheFrame = -1;
            _playerRuntimeSnapshotCacheValid = false;
            _playerRuntimeSnapshotMovement = default;
            _playerRuntimeSnapshotLook = default;
            _playerMotionCacheFrame = -1;
            _playerMotionCacheValid = false;
            _playerMotionCachePosition = default;
            _playerMotionCacheVelocity = default;
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
            int writeIndex = 0;
            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                MassiveThreatData threat = _massiveThreats[i];
                if (threat.EndTime <= absoluteSimulationTime)
                    continue;

                if (writeIndex != i)
                    _massiveThreats[writeIndex] = threat;
                writeIndex++;
            }

            for (int i = writeIndex; i < _massiveThreats.Length; i++)
                _massiveThreats[i] = default;

            _activeMassiveThreatCount = writeIndex;
            _debugMassiveThreatCount = _activeMassiveThreatCount;
        }

        private void RefreshRenderScaleCache()
        {
            _cachedVatSwayAmplitudeScale = MigrationDirector.ResolveVatSwayAmplitudeScale();
        }

        private void RefreshRenderLayerCache()
        {
            _cachedRenderLayer = gameObject.layer;
        }

        private bool EnsureBoidIndirectArgsBuffer()
        {
            if (_boidIndirectArgsBuffer != null)
                return true;

            _boidIndirectArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - VAT micro-fauna indirect draw args - owner: SargassumMicroFaunaBoids
            _boidIndirectArgsMesh = null;
            _boidIndirectArgsInstanceCount = -1;
            return _boidIndirectArgsBuffer != null;
        }

        private bool UploadBoidIndirectArgs(Mesh mesh, int instanceCount)
        {
            if (mesh == null || instanceCount <= 0 || !EnsureBoidIndirectArgsBuffer())
                return false;

            if (_boidIndirectArgsMesh == mesh && _boidIndirectArgsInstanceCount == instanceCount)
                return true;

            _boidIndirectArgsUpload[0].indexCountPerInstance = mesh.GetIndexCount(0);
            _boidIndirectArgsUpload[0].instanceCount = (uint)instanceCount;
            _boidIndirectArgsUpload[0].startIndex = mesh.GetIndexStart(0);
            _boidIndirectArgsUpload[0].baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0));
            _boidIndirectArgsUpload[0].startInstance = 0u;
            GraphicsBufferUploadUtility.UploadArray(_boidIndirectArgsBuffer, _boidIndirectArgsUpload, 1);
            _boidIndirectArgsMesh = mesh;
            _boidIndirectArgsInstanceCount = instanceCount;
            return true;
        }

        private void UploadHitFlashPropertiesIfNeeded()
        {
            if (!_hitFlashPropertiesDirty || _materialPropertyBlock == null)
                return;

            _materialPropertyBlock.SetFloat(_HitFlashStartTimeId, _hitFlashStartTime);
            _materialPropertyBlock.SetFloat(_HitFlashDurationId, hitFlashDurationSeconds);
            _materialPropertyBlock.SetFloat(_HitFlashIntensityId, _hitFlashRuntimeIntensity);
            _materialPropertyBlock.SetFloat(_HitFlashRadiusId, _hitFlashRuntimeRadius);
            _materialPropertyBlock.SetFloat(_HitFlashBloatId, hitFlashBloatMeters);
            float hitFlashInvRadiusSq = _hitFlashRuntimeRadius > 0.0001f ? 1f / (_hitFlashRuntimeRadius * _hitFlashRuntimeRadius) : 0f;
            _materialPropertyBlock.SetVector(_HitFlashOriginWSId, new Vector4(_hitFlashOriginWS.x, _hitFlashOriginWS.y, _hitFlashOriginWS.z, hitFlashInvRadiusSq));
            _materialPropertyBlock.SetColor(_HitFlashColorId, hitFlashColor);
            _hitFlashPropertiesDirty = false;
        }

        private bool ShouldUploadRenderFloat(float value, ref float cachedValue)
        {
            if (!_renderPropertiesDirty && cachedValue == value)
                return false;

            cachedValue = value;
            return true;
        }

        private void UploadBoidRenderPropertiesIfNeeded(GraphicsBuffer currentBuffer, bool vatEnabled)
        {
            if (_renderPropertiesDirty || !ReferenceEquals(_renderPropertiesBoidBuffer, currentBuffer))
            {
                _materialPropertyBlock.SetBuffer(_BoidsBufferId, currentBuffer);
                _renderPropertiesBoidBuffer = currentBuffer;
            }

            float parasiteMode = _parasiteModeActive ? 1f : 0f;
            float parasiteAggression = _debugParasiteAggression01;
            float velocitySleepScale = _debugHibernation01 >= 0.999f ? 0f : 1f;
            float lodDitherKeep01 = ResolveLodDitherKeep01(_debugHibernation01);
            float vatEnabledFloat = vatEnabled ? 1f : 0f;
            float vatFrameCount = vatEnabled ? boidVatFrameCount : 1f;
            float vatVertexCount = _boidMeshVertexCount;
            float vatPositionScale = boidVatPositionScale * _cachedVatSwayAmplitudeScale;

            if (ShouldUploadRenderFloat(parasiteMode, ref _renderPropertiesParasiteMode))
                _materialPropertyBlock.SetFloat(_ParasiteModeId, parasiteMode);
            if (ShouldUploadRenderFloat(parasiteAggression, ref _renderPropertiesParasiteAggression))
                _materialPropertyBlock.SetFloat(_ParasiteAggressionId, parasiteAggression);
            if (ShouldUploadRenderFloat(velocitySleepScale, ref _renderPropertiesVelocitySleepScale))
                _materialPropertyBlock.SetFloat(_VelocitySleepScaleId, velocitySleepScale);
            if (ShouldUploadRenderFloat(lodDitherKeep01, ref _renderPropertiesLodDitherKeep01))
                _materialPropertyBlock.SetFloat(_LodDitherKeep01Id, lodDitherKeep01);
            if (ShouldUploadRenderFloat(vatEnabledFloat, ref _renderPropertiesVatEnabled))
                _materialPropertyBlock.SetFloat(_VatEnabledId, vatEnabledFloat);
            if (ShouldUploadRenderFloat(vatFrameCount, ref _renderPropertiesVatFrameCount))
                _materialPropertyBlock.SetFloat(_VatFrameCountId, vatFrameCount);
            if (ShouldUploadRenderFloat(vatVertexCount, ref _renderPropertiesVatVertexCount))
                _materialPropertyBlock.SetFloat(_VatVertexCountId, vatVertexCount);
            if (ShouldUploadRenderFloat(boidVatPlaybackSpeed, ref _renderPropertiesVatPlaybackSpeed))
                _materialPropertyBlock.SetFloat(_VatPlaybackSpeedId, boidVatPlaybackSpeed);
            if (ShouldUploadRenderFloat(boidVatInstancePhaseScale, ref _renderPropertiesVatInstancePhaseScale))
                _materialPropertyBlock.SetFloat(_VatInstancePhaseScaleId, boidVatInstancePhaseScale);
            if (ShouldUploadRenderFloat(vatPositionScale, ref _renderPropertiesVatPositionScale))
                _materialPropertyBlock.SetFloat(_VatPositionScaleId, vatPositionScale);
            if (ShouldUploadRenderFloat(boidVatNormalBlend, ref _renderPropertiesVatNormalBlend))
                _materialPropertyBlock.SetFloat(_VatNormalBlendId, boidVatNormalBlend);

            if (vatEnabled)
            {
                if (_renderPropertiesDirty || _renderPropertiesVatPositionTexture != boidVatPositionTexture)
                {
                    _materialPropertyBlock.SetTexture(_VatPositionTexId, boidVatPositionTexture);
                    _renderPropertiesVatPositionTexture = boidVatPositionTexture;
                }

                if (_renderPropertiesDirty || _renderPropertiesVatNormalTexture != boidVatNormalTexture)
                {
                    _materialPropertyBlock.SetTexture(_VatNormalTexId, boidVatNormalTexture);
                    _renderPropertiesVatNormalTexture = boidVatNormalTexture;
                }
            }

            _renderPropertiesDirty = false;
        }

        private void RenderCurrentBuffer()
        {
            GraphicsBuffer currentBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            if (_activeBoidCount <= 0 ||
                currentBuffer == null ||
                boidMesh == null ||
                boidMaterial == null ||
                _materialPropertyBlock == null)
            {
                return;
            }

            bool vatEnabled = boidVatPositionTexture != null &&
                              boidVatNormalTexture != null &&
                              boidVatFrameCount > 1;
            UploadBoidRenderPropertiesIfNeeded(currentBuffer, vatEnabled);
            UploadHitFlashPropertiesIfNeeded();

            if (!UploadBoidIndirectArgs(boidMesh, _activeBoidCount))
                return;

            int targetLayer = useGameObjectLayer ? _cachedRenderLayer : 0;
            RenderParams renderParams = new RenderParams(boidMaterial)
            {
                worldBounds = _renderBounds,
                matProps = _materialPropertyBlock,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                layer = targetLayer,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMeshIndirect(renderParams, boidMesh, _boidIndirectArgsBuffer, 1, 0);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            GlobalRegistry.RegisterSargassumMicroFaunaRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumMicroFauna, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(this);
            _serviceRegistered = false;
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

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _boidsBufferA);
            ReleaseBuffer(ref _boidsBufferB);
            ReleaseBuffer(ref _boidIndirectArgsBuffer);
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
            _latchStatsBufferRawTarget = false;
            _pbdCorrectionBufferRawTarget = false;
            _spatialGridCountBufferRawTarget = false;
            _computeStaticBuffersBound = false;
            _boidIndirectArgsMesh = null;
            _boidIndirectArgsInstanceCount = -1;
            _renderPropertiesBoidBuffer = null;
            _renderPropertiesVatPositionTexture = null;
            _renderPropertiesVatNormalTexture = null;
            if (_fallbackAbyssalFlowTexture != null)
            {
                Destroy(_fallbackAbyssalFlowTexture);
                _fallbackAbyssalFlowTexture = null;
                _boundAbyssalFlowTexture = null;
            }
            _renderPropertiesDirty = true;
            _hitFlashPropertiesDirty = true;
        }

        private void CompletePendingReadbackAndReleaseBuffers()
        {
            CompletePendingPredatorConsumption(forceComplete: true);
            JobHandle disposeDependency = CancelPendingLeviathanNodeBuildForDispose();
            if (_parasiteLatchReadbackPending)
            {
                _parasiteLatchReadbackPending = false;
                _parasiteLatchReadbackRequest = default;
            }

            _parasiteLatchReadbackTimer = 0f;
            ReleaseBuffers();
            ResetComputeKernelBindings();
            _boundBoidCompute = null;
            DisposeNativeArrayDeferred(ref _staticObstacleCache, disposeDependency);
            DisposeNativeArrayDeferred(ref _boidStateNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _leviathanPathScratchNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _leviathanNodeFrontNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _leviathanNodeBackNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _leviathanNodeCountNative, disposeDependency);
            DisposeFoveatedSimulationBuffers(disposeDependency);
            DisposeNativeArrayDeferred(ref _threatGridUploadNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _threatVoxelUploadNative, disposeDependency);
            ResetThreatGridSnapshot();
            ResetThreatVoxelSnapshot();
            DisposeNativeArrayDeferred(ref _simulationFrameNative, disposeDependency);
            DisposeNativeArrayDeferred(ref _foodChainTelemetryRing, disposeDependency);
            _inactiveStatisticalSwarmRing.Dispose(disposeDependency);
            _inactiveStatisticalSwarmCenterRing.Dispose(disposeDependency);
            if (_killSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_killSignals));
                _killSignals.Dispose();
                _killSignals = default;
            }

            _singleBoidUpload = null;
            _feedingFrenzyWindowStartTime = -1f;
            _feedingFrenzyKillCount = 0;
            _foodChainTelemetryCursor = 0;
            _foodChainTelemetryDumped = false;
            _pendingPredatorConsumptionTimeSeconds = 0f;
            _debugConsumedBoidCount = 0;
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle CancelPendingLeviathanNodeBuildForDispose()
        {
            if (!_leviathanNodeBuildScheduled)
                return default;

            JobHandle disposeDependency = _leviathanNodeBuildHandle;
            _leviathanNodeBuildHandle = default;
            _leviathanNodeBuildScheduled = false;
            ClearLeviathanSnapshot();
            return disposeDependency;
        }

        private void PrimeFoveatedSimulationDecision(float frameDeltaTime, float cameraDistanceSq)
        {
            EnsureNativeArrayCapacity(ref _foveatedSimulationInputNative, 1, nameof(_foveatedSimulationInputNative));
            EnsureNativeArrayCapacity(ref _foveatedSimulationFrontNative, 1, nameof(_foveatedSimulationFrontNative));
            EnsureNativeArrayCapacity(ref _foveatedSimulationBackNative, 1, nameof(_foveatedSimulationBackNative));
            PopulateFoveatedSimulationInput(frameDeltaTime, cameraDistanceSq, previousAccumulator: 0f);
            var primeJob = new EvaluateSimulationLodJob
            {
                Input = _foveatedSimulationInputNative,
                Output = _foveatedSimulationBackNative
            };

            // COLD DIRECT KERNEL: prime the foveated LOD front buffer before the first runtime Tick without a synchronous job-dispatch barrier.
            primeJob.Execute();
            (_foveatedSimulationFrontNative, _foveatedSimulationBackNative) = (_foveatedSimulationBackNative, _foveatedSimulationFrontNative);
            _foveatedSimulationScheduled = false;
        }

        private void PopulateFoveatedSimulationInput(float frameDeltaTime, float cameraDistanceSq, float previousAccumulator)
        {
            if (!_foveatedSimulationInputNative.IsCreated || _foveatedSimulationInputNative.Length <= 0)
                return;

            _foveatedSimulationInputNative[0] = new FoveatedSimulationInput
            {
                FrameDeltaTime = math.max(0f, frameDeltaTime),
                CameraDistanceSq = math.max(0f, cameraDistanceSq),
                FullDistanceMeters = hibernationStartDistance,
                SleepDistanceMeters = simulationCullDistance,
                MaxStepSeconds = hibernationMaxStepSeconds,
                MinTimeScale = hibernationMinTimeScale,
                PreviousAccumulator = math.max(0f, previousAccumulator),
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

            if (!DispatcherJobSwap.TryComplete(ref _foveatedSimulationHandle, forceComplete))
                return;

            _foveatedSimulationScheduled = false;
            (_foveatedSimulationFrontNative, _foveatedSimulationBackNative) = (_foveatedSimulationBackNative, _foveatedSimulationFrontNative);
        }

        private void DisposeFoveatedSimulationBuffers(JobHandle externalDependency)
        {
            if (_foveatedSimulationScheduled)
            {
                JobHandle disposeDependency = JobHandle.CombineDependencies(externalDependency, _foveatedSimulationHandle);
                DisposeNativeArrayDeferred(ref _foveatedSimulationInputNative, disposeDependency);
                DisposeNativeArrayDeferred(ref _foveatedSimulationFrontNative, disposeDependency);
                DisposeNativeArrayDeferred(ref _foveatedSimulationBackNative, disposeDependency);
                _foveatedSimulationHandle = default;
                _foveatedSimulationScheduled = false;
                return;
            }

            DisposeNativeArrayDeferred(ref _foveatedSimulationInputNative, externalDependency);
            DisposeNativeArrayDeferred(ref _foveatedSimulationFrontNative, externalDependency);
            DisposeNativeArrayDeferred(ref _foveatedSimulationBackNative, externalDependency);
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

        private static Vector3 MinVector3(Vector3 a, Vector3 b)
        {
            return new Vector3(math.min(a.x, b.x), math.min(a.y, b.y), math.min(a.z, b.z));
        }

        private static Vector3 MaxVector3(Vector3 a, Vector3 b)
        {
            return new Vector3(math.max(a.x, b.x), math.max(a.y, b.y), math.max(a.z, b.z));
        }

        private static Vector3 FastNormalizeVector3(Vector3 value, Vector3 fallback)
        {
            float lengthL1 = math.abs(value.x) + math.abs(value.y) + math.abs(value.z);
            return lengthL1 > 0.0001f
                ? new Vector3(value.x / lengthL1, value.y / lengthL1, value.z / lengthL1)
                : fallback;
        }

        private static float ResolveLodDitherKeep01(float hibernation01)
        {
            float fade01 = math.saturate((math.saturate(hibernation01) - LodDitherHibernationStart01) * LodDitherHibernationInvRange);
            return 1f - fade01;
        }

        private static Vector3 ResolveApproxRight(Vector3 forward)
        {
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return FastNormalizeVector3(right, Vector3.right);
        }

        private static Vector3 ResolveApproxUp(Vector3 forward, Vector3 right)
        {
            Vector3 up = new Vector3(
                -forward.y * right.z,
                forward.z * right.x - forward.x * right.z,
                -forward.y * right.x);
            return FastNormalizeVector3(up, Vector3.up);
        }

        private static float3 CheapNormalizeL1(float3 value, float3 fallback)
        {
            float lengthL1 = math.abs(value.x) + math.abs(value.y) + math.abs(value.z);
            return lengthL1 > 0.0001f ? value / lengthL1 : fallback;
        }

        private static float CheapSinSigned(float radians)
        {
            return -CheapTriangleWaveSigned(radians - 1.57079632679f);
        }

        private static float CheapCosSigned(float radians)
        {
            return -CheapTriangleWaveSigned(radians);
        }

        private static float CheapTriangleWaveSigned(float radians)
        {
            float cycle = radians * (1f / StatisticalTwoPi);
            cycle -= math.floor(cycle);
            return 1f - 4f * math.abs(cycle - 0.5f);
        }

        private static int CeilDivPositive(int numerator, int denominator)
        {
            if (numerator <= 0)
                return 0;

            int safeDenominator = math.max(1, denominator);
            return 1 + ((numerator - 1) / safeDenominator);
        }

        private static int CeilToIntPositive(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return value >= int.MaxValue ? int.MaxValue : (int)math.ceil(value);
        }

        private static int FloorToInt(float value)
        {
            if (!math.isfinite(value))
                return 0;

            return value <= int.MinValue ? int.MinValue : value >= int.MaxValue ? int.MaxValue : (int)math.floor(value);
        }

        private static int RoundToIntPositive(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return value >= int.MaxValue ? int.MaxValue : (int)(value + 0.5f);
        }

        private static float FloorToMultiple(float value, float multiple)
        {
            float safeMultiple = math.max(0.0001f, multiple);
            return math.floor(value / safeMultiple) * safeMultiple;
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

            float wrappedDuration = math.floor(_simulationTime / SimulationPhaseWrapSeconds) * SimulationPhaseWrapSeconds;
            _simulationTime -= wrappedDuration;
            _simulationPhaseOffset += wrappedDuration;
        }

        private void CompletePendingLeviathanNodeBuild(bool forceComplete)
        {
            if (!_leviathanNodeBuildScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _leviathanNodeBuildHandle, forceComplete))
                return;

            _leviathanNodeBuildScheduled = false;

            int safeCount = (_leviathanNodeCountNative.IsCreated && _leviathanNodeCountNative.Length > 0)
                ? math.clamp(_leviathanNodeCountNative[0], 0, _leviathanNodeBackNative.IsCreated ? _leviathanNodeBackNative.Length : 0)
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

            int safeCount = math.clamp(_leviathanPathNodeCount, 0, _leviathanNodeFrontNative.Length);
            if (safeCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(_leviathanNodeBuffer, _leviathanNodeFrontNative, safeCount);
        }

        private void ClearLeviathanSnapshot()
        {
            _leviathanPathNodeCount = 0;
            _debugLeviathanNodeCount = 0;
        }

        private static void EnsureNativeArrayCapacity<T>(ref NativeArray<T> array, int requiredLength, string label) where T : struct
        {
            if (requiredLength <= 0)
                return;

            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<T>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<T>[requiredLength] - persistent sargassum job/GPU staging buffer - owner: SargassumMicroFaunaBoids
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Scene);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArrayDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
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
                DisableComputeDispatch(ComputeDisableReasonBoidLayoutMismatch);
                return false;
            }

            if (UnsafeUtility.SizeOf<SimulationFrameConstants>() != SimulationFrameConstantsStride)
            {
                DisableComputeDispatch(ComputeDisableReasonFrameLayoutMismatch);
                return false;
            }

            if (UnsafeUtility.SizeOf<GrazingAnchorData>() != GrazingAnchorStride ||
                UnsafeUtility.SizeOf<MassiveThreatData>() != MassiveThreatStride ||
                UnsafeUtility.SizeOf<FormationBeaconData>() != FormationBeaconStride ||
                UnsafeUtility.SizeOf<FormationObstacleData>() != FormationObstacleStride ||
                UnsafeUtility.SizeOf<LeviathanNodeData>() != LeviathanNodeStride)
            {
                DisableComputeDispatch(ComputeDisableReasonAncillaryLayoutMismatch);
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
                DisableComputeDispatch(ComputeDisableReasonMissingKernel);
                return false;
            }

            try
            {
                boidCompute.GetKernelThreadGroupSizes(kernelIndex, out uint groupSizeX, out _, out _);
                if (groupSizeX == 0u)
                {
                    DisableComputeDispatch(ComputeDisableReasonZeroThreadGroup);
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
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
            _computeStaticBuffersBound = false;
            _boundComputeDensityTexture = null;
            _boundComputeCutMaskTexture = null;
            _boundAbyssalFlowTexture = null;
        }

        private void DisableComputeDispatch(int reasonCode)
        {
            if (_computeDispatchDisabled)
                return;

            _computeDispatchDisabled = true;
            ResetComputeKernelBindings();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogComputeDispatchDisabled(ResolveComputeDisableReasonMessage(reasonCode), this);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ResolveComputeDisableReasonMessage(int reasonCode)
        {
            switch (reasonCode)
            {
                case ComputeDisableReasonDispatchFailure:
                    return "Compute dispatch failure.";
                case ComputeDisableReasonBindingFailure:
                    return "Compute binding failure.";
                case ComputeDisableReasonBoidLayoutMismatch:
                    return "BoidData layout mismatch.";
                case ComputeDisableReasonFrameLayoutMismatch:
                    return "SimulationFrameConstants layout mismatch.";
                case ComputeDisableReasonAncillaryLayoutMismatch:
                    return "Ancillary GPU buffer layout mismatch. Expected explicit 4-byte packed strides for grazing anchors, massive threats, formation data, and leviathan nodes.";
                case ComputeDisableReasonMissingKernel:
                    return "Missing compute kernel.";
                case ComputeDisableReasonZeroThreadGroup:
                    return "Compute kernel reported thread group size 0.";
                case ComputeDisableReasonKernelValidationFailure:
                    return "Compute kernel validation failure.";
                case ComputeDisableReasonOriginShiftFailure:
                    return "Origin-shift dispatch failure.";
                default:
                    return "Unknown compute dispatch failure.";
            }
        }

        private static void LogComputeDispatchDisabled(string message, UnityEngine.Object context)
        {
            Debug.LogError(message, context);
        }
#endif

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
            int boidShiftCount = ResolveActiveBoidUploadCount();
            if (boidShiftCount <= 0 ||
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
            int safeThreadGroupSize = math.max(1, (int)_threadGroupSizeX);
            int dispatchGroups = math.max(1, (boidShiftCount + safeThreadGroupSize - 1) / safeThreadGroupSize);

            try
            {
                boidCompute.SetVector(_OriginShiftDeltaId, shiftVector);
                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferA);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferB);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);
            }
            catch (Exception)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
        }

        private void ApplyRuntimeOffsetToSwarmData(Vector3 runtimeOffset)
        {
            if (_statisticalPopulationActive)
            {
                _fieldCenter = ToVector3(_statisticalPopulationCenterAup.ToRuntimeFloat3());
                _renderBounds.center = _fieldCenter;
                _debugRenderBounds = _renderBounds;
                ResetThreatGridSnapshot();
                ResetThreatVoxelSnapshot();
                return;
            }

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

                UploadGrazingAnchors();
            }

            if (_massiveThreats != null)
            {
                for (int i = 0; i < _activeMassiveThreatCount; i++)
                    _massiveThreats[i].Position += runtimeOffset;

                UploadMassiveThreats();
            }

            if (_formationBeacons != null)
            {
                for (int i = 0; i < _debugFormationBeaconCount; i++)
                    _formationBeacons[i].Position += runtimeOffset;

                UploadFormationBeacons();
            }

            if (_formationObstacles != null)
            {
                for (int i = 0; i < _debugFormationObstacleCount; i++)
                    _formationObstacles[i].Position += runtimeOffset;

                UploadFormationObstacles();
            }

            if (_leviathanNodeFrontNative.IsCreated)
            {
                int frontCount = math.clamp(_leviathanPathNodeCount, 0, _leviathanNodeFrontNative.Length);
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
                int backCount = math.clamp(_leviathanPathNodeCount, 0, _leviathanNodeBackNative.Length);
                for (int i = 0; i < backCount; i++)
                {
                    LeviathanNodeData node = _leviathanNodeBackNative[i];
                    node.Position += (float3)runtimeOffset;
                    _leviathanNodeBackNative[i] = node;
                }
            }
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
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
                RefreshRenderLayerCache();
                _renderPropertiesDirty = true;
                _hitFlashPropertiesDirty = true;
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

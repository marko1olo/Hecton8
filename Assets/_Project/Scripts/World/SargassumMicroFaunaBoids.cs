using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Ecosystem;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Visor;
using Hecton8.Biolum;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
    public sealed class SargassumMicroFaunaBoids : MonoBehaviour, IFixedTickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, Hecton8.Gameplay.IFlashlightEventListener, ISargassumGlobalDragEventListener, ISonarPingEventListener, IMicroFaunaPresentationPulseSink, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SargassumMicroFaunaBoidsSignalPushDropCount;
        private static SargassumMicroFaunaBoids s_activeRuntimeInstance;
        private static bool s_duplicateRuntimeOwnerLogged;
        private const int MaxLeviathanNodePathIterations = 4096;
        private const int WhileLoopWatchdogLimit = 10000;
        private const float FullSimulationDistanceMeters = 50f;
        private const float SleepSimulationDistanceMeters = 200f;
        private const float StatisticalDematerializeDistanceMeters = 200f;
        private const float StatisticalRematerializeDistanceMeters = 180f;
        private const float StatisticalDematerializeDistanceSq = StatisticalDematerializeDistanceMeters * StatisticalDematerializeDistanceMeters;
        private const float StatisticalRematerializeDistanceSq = StatisticalRematerializeDistanceMeters * StatisticalRematerializeDistanceMeters;
        private const int StatisticalMigrationKeepAliveSlowTickStride = 10;
        private const float ParasiteModeMinDepthMeters = 2000f;
        private const float StatisticalFibonacciGoldenAngle = 2.39996323f;
        private const float StatisticalTwoPi = 6.28318530718f;
        private const int PopulationDensityCellSizeMeters = 32;
        private const int PopulationDensityMinRadiusMeters = 4;
        private const int InactiveStatisticalSwarmRingCapacity = 16;
        private const int TargetBoidsPerGrazingAnchor = 48;
        private const float MinimumPopulationBudgetScale = 0.35f;
        private const float MaximumEcosystemSpeedMultiplier = 4f;
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
        private const int ComputeDisableReasonOversizedThreadGroup = 10;
        private const int ComputeDisableReasonUnsupportedCompute = 11;
        private const int ComputeDisableReasonDispatchGroupLimit = 12;
        private const uint PortableThreadGroupMaxSize = 256u;
        private const int PortableMaxDispatchGroupsPerDimension = 65535;
        private const int FaunaSimulationBucketMask = SimulationBucketConstants.StandardSlowBucketMask;
        private const float FaunaSimulationBucketInvCount = 1f / SimulationBucketConstants.StandardSlowBucketCount;
        private const float DefaultWaterLevel = 14.02f;
        private const uint FaunaAmbientDriftKillSwitchMask = GlobalRegistry.SystemKillSwitchLane4VfxMask;
        private const uint FaunaBucketedSimulationCostHash = 0x46534255u; // FSBU
        private static uint _systemKillSwitchMaskSnapshot;
        private static int _systemKillSwitchSnapshotFrame = -1;
#if UNITY_EDITOR
        private const int MaxEditorValidateDepth = 4;
        private static int _editorValidateDepth;
#endif

        public static SargassumMicroFaunaBoids Instance => s_activeRuntimeInstance;
        internal static SargassumMicroFaunaBoids ActiveRuntimeInstance => Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_x001SargassumMicroFaunaBoidsSignalPushDropCount = 0;
            s_activeRuntimeInstance = null;
            s_duplicateRuntimeOwnerLogged = false;
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL scalar packing; ValidateGpuStructLayouts gates stride.
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        internal struct BoidData
        {
            // Byte layout proof vs HLSL StructuredBuffer<BoidData>:
            // Position   -> offset  0, size 12
            // Velocity   -> offset 12, size 12
            // Panic      -> offset 24, size  4
            // StateFlags -> offset 28, size  4
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public Vector3 Velocity;
            [FieldOffset(24)]
            public float Panic;
            [FieldOffset(28)]
            public uint StateFlags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct BoidKillSignal
        {
            [FieldOffset(0)] public float3 KillPositionWS;
            [FieldOffset(12)] public float3 PredatorPositionWS;
            [FieldOffset(24)] public int BoidId;
            [FieldOffset(28)] public uint PredatorId;
            [FieldOffset(32)] public float FearRadiusMeters;
            [FieldOffset(36)] public float FearAmount;
            [FieldOffset(40)] private ulong _pad0;
            [FieldOffset(48)] private ulong _pad1;
            [FieldOffset(56)] private ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FoodChainTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint StateHash;
            [FieldOffset(8)] public uint SourceHash;
            [FieldOffset(12)] public uint Flags;
            [FieldOffset(16)] public int ActiveBoidCount;
            [FieldOffset(20)] public int ConsumedBoidCount;
            [FieldOffset(24)] public int PendingKillJob;
            [FieldOffset(28)] public int LodTier;
            [FieldOffset(32)] public float3 FieldCenterWS;
            [FieldOffset(44)] public float3 EventPositionWS;
            [FieldOffset(56)] public uint AnomalyHash;
            [FieldOffset(60)] public float SimulationTime;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct BoidSensoryBlackBoxEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint StateHash;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public int ActiveThreatCount;
            [FieldOffset(16)] public float4 SubmarineThreat;
            [FieldOffset(32)] public float4 FlashlightThreat;
            [FieldOffset(48)] public float4 AcousticPingRadii;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal struct PopulationDensityPoint
        {
            [FieldOffset(0)] public int CenterCellId;
            [FieldOffset(4)] public ushort Count;
            [FieldOffset(6)] public byte Species;
            [FieldOffset(7)] public byte RadiusMeters;
            [FieldOffset(8)] private ulong _pad0;
        }

        private struct NativeRingBuffer<T> : IDisposable where T : struct
        {
            private VaultGenerationHandle<T> _itemsHandle;
            private IDataVault _vault;
            private int _head;
            private int _count;

            public int Count => _count;
            public bool IsCreated => _itemsHandle.Generation != 0u;

            public void EnsureCapacity(IDataVault vault, BufferID bufferId, int capacity, string label)
            {
                if (capacity <= 0 || vault == null || bufferId == BufferID.Unknown)
                    return;

                if (TryReadOnlySargassumVaultArray(vault, in _itemsHandle, bufferId, capacity, out NativeArray<T>.ReadOnly _))
                    return;

                Dispose();
                _vault = vault;
                TryEnsureSargassumVaultArray(vault, ref _itemsHandle, bufferId, capacity, NativeArrayOptions.ClearMemory, out _);
                _head = 0;
                _count = 0;
            }

            public void PushOverwrite(in T value)
            {
                IDataVault vault = _vault;
                if (vault == null ||
                    _itemsHandle.Generation == 0u)
                {
                    return;
                }

                NativeArray<T> items = default;
                bool locked = false;
                try
                {
                    if (!vault.TryAcquireWriteLock(in _itemsHandle, SystemID.WorldSargassum, out items))
                        return;

                    locked = true;
                    if (!items.IsCreated || items.Length <= 0)
                        return;

                    items[_head] = value;
                    _head++;
                    if (_head >= items.Length)
                        _head = 0;
                    _count = math.min(_count + 1, items.Length);
                }
                finally
                {
                    if (locked)
                        vault.ReleaseWriteLock(in _itemsHandle, SystemID.WorldSargassum);
                }
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
                _itemsHandle = default;
                _vault = null;
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
            VisualMutationResolved = 1u << 5,
            LightStimulus = 1u << 6
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct PredatorBoidConsumptionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<BoidData> Boids;
            [NoAlias] public NativeArray<BoidKillSignal> KillSignals;
            [NoAlias] public NativeArray<int> KillSignalCount;
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

                    if (emitted >= KillSignals.Length)
                        break;

                    KillSignals[emitted] = new BoidKillSignal
                    {
                        KillPositionWS = boidPosition,
                        PredatorPositionWS = PredatorPositionWS,
                        BoidId = i,
                        PredatorId = PredatorId,
                        FearRadiusMeters = FearRadiusMeters,
                        FearAmount = FearAmount
                    };
                    emitted++;
                }

                if (KillSignalCount.IsCreated && KillSignalCount.Length > 0)
                    KillSignalCount[0] = emitted;
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FoveatedSimulationInput
        {
            [FieldOffset(0)] public float FrameDeltaTime;
            [FieldOffset(4)] public float CameraDistanceSq;
            [FieldOffset(8)] public float FullDistanceMeters;
            [FieldOffset(12)] public float SleepDistanceMeters;
            [FieldOffset(16)] public float MaxStepSeconds;
            [FieldOffset(20)] public float MinTimeScale;
            [FieldOffset(24)] public float PreviousAccumulator;
            [FieldOffset(28)] public int PreviousTier;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FoveatedSimulationDecision
        {
            [FieldOffset(0)] public float SimulationDeltaTime;
            [FieldOffset(4)] public float Hibernation01;
            [FieldOffset(8)] public float Accumulator;
            [FieldOffset(12)] public int Tier;
            [FieldOffset(16)] public int DispatchSimulation;
            [FieldOffset(20)] public float CameraDistanceSq;
            [FieldOffset(24)] public float Padding0;
            [FieldOffset(28)] public float Padding1;
        }

        private static FoveatedSimulationDecision EvaluateFoveatedSimulationDecision(in FoveatedSimulationInput input)
        {
            FoveatedSimulationDecision decision = default;
            float safeFrameDeltaTime = math.isfinite(input.FrameDeltaTime) ? math.max(0f, input.FrameDeltaTime) : 0f;
            float fullDistanceMeters = math.isfinite(input.FullDistanceMeters) ? math.max(0f, input.FullDistanceMeters) : FullSimulationDistanceMeters;
            float sleepDistanceMeters = math.isfinite(input.SleepDistanceMeters) ? math.max(fullDistanceMeters + 0.01f, input.SleepDistanceMeters) : SleepSimulationDistanceMeters;
            float cameraDistanceSq = math.isfinite(input.CameraDistanceSq) ? math.max(0f, input.CameraDistanceSq) : 0f;
            int previousTier = math.clamp(input.PreviousTier, (int)SimulationLodTier.Full, (int)SimulationLodTier.Sleep);
            float fullEnterMeters = math.max(0f, fullDistanceMeters - SimulationLodHysteresisMeters);
            float fullExitMeters = fullDistanceMeters + SimulationLodHysteresisMeters;
            float sleepEnterMeters = sleepDistanceMeters + SimulationLodHysteresisMeters;
            float sleepExitMeters = math.max(fullDistanceMeters + 0.01f, sleepDistanceMeters - SimulationLodHysteresisMeters);
            float fullDistanceSq = fullDistanceMeters * fullDistanceMeters;
            float sleepDistanceSq = sleepDistanceMeters * sleepDistanceMeters;
            float fullEnterSq = fullEnterMeters * fullEnterMeters;
            float fullExitSq = fullExitMeters * fullExitMeters;
            float sleepEnterSq = sleepEnterMeters * sleepEnterMeters;
            float sleepExitSq = sleepExitMeters * sleepExitMeters;
            float safeMaxStepSeconds = math.isfinite(input.MaxStepSeconds) ? math.max(1f / 60f, input.MaxStepSeconds) : 1f / 30f;
            float safeMinTimeScale = math.isfinite(input.MinTimeScale) ? math.clamp(input.MinTimeScale, 0.1f, 1f) : 1f;
            float previousAccumulator = math.isfinite(input.PreviousAccumulator) ? math.max(0f, input.PreviousAccumulator) : 0f;
            decision.CameraDistanceSq = cameraDistanceSq;
            bool stayFull = previousTier == (int)SimulationLodTier.Full && cameraDistanceSq <= fullExitSq;
            bool enterFull = previousTier != (int)SimulationLodTier.Full && cameraDistanceSq <= fullEnterSq;
            bool staySleep = previousTier == (int)SimulationLodTier.Sleep && cameraDistanceSq > sleepExitSq;
            bool enterSleep = previousTier != (int)SimulationLodTier.Sleep && cameraDistanceSq > sleepEnterSq;

            if (staySleep || enterSleep)
            {
                decision.Hibernation01 = 1f;
                decision.Tier = (int)SimulationLodTier.Sleep;
                return decision;
            }

            if (stayFull || enterFull)
            {
                decision.Tier = (int)SimulationLodTier.Full;
                decision.SimulationDeltaTime = safeFrameDeltaTime;
                decision.DispatchSimulation = safeFrameDeltaTime > 0f ? 1 : 0;
                return decision;
            }

            decision.Tier = (int)SimulationLodTier.Simplified;
            decision.Hibernation01 = math.saturate((cameraDistanceSq - fullDistanceSq) / math.max(0.01f, sleepDistanceSq - fullDistanceSq));
            decision.Accumulator = previousAccumulator + safeFrameDeltaTime;
            if (decision.Accumulator + 0.0001f < safeMaxStepSeconds)
                return decision;

            decision.SimulationDeltaTime = decision.Accumulator * math.lerp(1f, safeMinTimeScale, decision.Hibernation01);
            decision.Accumulator = 0f;
            decision.DispatchSimulation = decision.SimulationDeltaTime > 0f ? 1 : 0;
            return decision;
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL float/uint field packing.
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct GrazingAnchorData
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float Radius;
            [FieldOffset(16)]
            public float Strength;
            [FieldOffset(20)]
            public float Phase;
            [FieldOffset(24)]
            public Vector2 Padding;
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL float/uint field packing.
        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct MassiveThreatData
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float InnerRadius;
            [FieldOffset(16)]
            public float PanicRadius;
            [FieldOffset(20)]
            public float Strength;
            [FieldOffset(24)]
            public float EndTime;
            [FieldOffset(28)]
            public Vector3 DirectionWS;
            [FieldOffset(40)]
            public uint ThreatFlags;
            [FieldOffset(44)]
            private uint _pad0;
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL float/uint field packing.
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FormationBeaconData
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float Radius;
            [FieldOffset(16)]
            public float Strength;
            [FieldOffset(20)]
            public float Phase;
            [FieldOffset(24)]
            public Vector2 Padding;
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL float/uint field packing.
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FormationObstacleData
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float Radius;
            [FieldOffset(16)]
            public float Weight;
            [FieldOffset(20)]
            public Vector3 Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private readonly struct StaticObstacleData
        {
            [FieldOffset(0)] public readonly float3 Center;
            [FieldOffset(12)] public readonly float3 Extents;
            [FieldOffset(24)] public readonly float Radius;
            [FieldOffset(28)] private readonly uint _pad0;

            public StaticObstacleData(float3 center, float3 extents, float radius)
            {
                Center = center;
                Extents = extents;
                Radius = radius;
                _pad0 = 0u;
            }
        }

        // GPU StructuredBuffer interop: explicit offsets preserve HLSL float/uint field packing.
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct LeviathanNodeData
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public float Distance01;
            [FieldOffset(16)]
            public float3 Tangent;
            [FieldOffset(28)]
            public float Radius;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildLeviathanNodeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float3> SourcePath;
            public int SourceCount;
            [NoAlias] public NativeArray<LeviathanNodeData> OutputNodes;
            [NoAlias] public NativeArray<int> OutputCount;
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

        // GPU frame packet interop: explicit 16-byte lanes preserve int4/float4 HLSL stride.
        [StructLayout(LayoutKind.Explicit, Size = 768)]
        private struct SimulationFrameConstants
        {
            [FieldOffset(0)]
            public float4 Simulation0;
            [FieldOffset(16)]
            public float4 Motion0;
            [FieldOffset(32)]
            public float4 Neighbor0;
            [FieldOffset(48)]
            public float4 Flocking0;
            [FieldOffset(64)]
            public float4 Flocking1;
            [FieldOffset(80)]
            public float4 Flocking2;
            [FieldOffset(96)]
            public float4 Grazing0;
            [FieldOffset(112)]
            public float4 Time0;
            [FieldOffset(128)]
            public float4 FieldCenter;
            [FieldOffset(144)]
            public float4 FieldExtents;
            [FieldOffset(160)]
            public float4 SpatialGridOrigin;
            [FieldOffset(176)]
            public int4 SpatialGridMeta;
            [FieldOffset(192)]
            public int4 Counts0;
            [FieldOffset(208)]
            public int4 Counts1;
            [FieldOffset(224)]
            public float4 DensityWorldRect;
            [FieldOffset(240)]
            public float4 CutMaskWorldRect;
            [FieldOffset(256)]
            public float4 DriftOffset;
            [FieldOffset(272)]
            public float4 DriftDelta;
            [FieldOffset(288)]
            public float4 PlayerPosition;
            [FieldOffset(304)]
            public float4 PlayerVelocity;
            [FieldOffset(320)]
            public float4 PlayerRight;
            [FieldOffset(336)]
            public float4 PlayerUp;
            [FieldOffset(352)]
            public float4 PlayerForward;
            [FieldOffset(368)]
            public float4 CameraAvoidPosition;
            [FieldOffset(384)]
            public float4 CameraAvoidData;
            [FieldOffset(400)]
            public float4 ParasiteAndFormation0;
            [FieldOffset(416)]
            public float4 Formation1;
            [FieldOffset(432)]
            public float4 Leviathan0;
            [FieldOffset(448)]
            public float4 Leviathan1;
            [FieldOffset(464)]
            public float4 Leviathan2;
            [FieldOffset(480)]
            public float4 CameraPosition;
            [FieldOffset(496)]
            public int4 ThreatGridMeta;
            [FieldOffset(512)]
            public float4 ThreatGridCenter;
            [FieldOffset(528)]
            public int4 ThreatVoxelMeta;
            [FieldOffset(544)]
            public float4 ThreatVoxelOrigin;
            [FieldOffset(560)]
            public float4 ThreatVoxelCellSize;
            [FieldOffset(576)]
            public float4 TransportCapsule0;
            [FieldOffset(592)]
            public float4 TransportCapsule1;
            [FieldOffset(608)]
            public float4 SubmarineWake0;
            [FieldOffset(624)]
            public float4 SubmarineWake1;
            [FieldOffset(640)]
            public float4 Ecosystem0;
            [FieldOffset(656)]
            public float4 Fragmentation0;
            [FieldOffset(672)]
            public float4 Fragmentation1;
            [FieldOffset(688)]
            public float4 SonarScatter0;
            [FieldOffset(704)]
            public float4 AcousticPanic0;
            [FieldOffset(720)]
            public float4 AcousticPanic1;
            [FieldOffset(736)]
            public float4 AbyssalFlowWeatherCurrent;
            [FieldOffset(752)]
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
        private const int SpatialGridMaxBoidsPerCell = 32;
        private const float ThreatVoxelCellEpsilon = 0.001f;
        private const int ThreatGridMaxResolution = 257;
        private const int ThreatGridMaxCellCount = ThreatGridMaxResolution * ThreatGridMaxResolution;
        private const uint DefaultBoidStateFlags = (uint)(BoidStateFlags.Active | BoidStateFlags.Hunting);
        private const uint ConsumedBoidStateFlag = (uint)BoidStateFlags.Consumed;
        private const uint BoidVisualMutationMask = (uint)(BoidStateFlags.AggressiveMutation | BoidStateFlags.VisualMutationResolved);
        private const int PredatorKillSignalDrainLimit = 8;
        private const int BoidKillSignalSizeBytes = 64;
        private const int FoveatedSimulationInputSizeBytes = 32;
        private const int FoveatedSimulationDecisionSizeBytes = 32;
        private const float SimulationLodHysteresisMeters = 6f;
        private const int StaticObstacleDataSizeBytes = 32;
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
        private const float WhaleFallScavengerMinimumWake01 = 0.125f;
        private const float WhaleFallScavengerRadiusMeters = 14f;
        private const float WhaleFallScavengerGroundOffsetMeters = 0.08f;
        private const float WhaleFallScavengerTangentSpeedMetersPerSecond = 0.65f;
        private const float WhaleFallScavengerRadiusHashInv = 1f / 1023f;
        private const float WhaleFallScavengerAngleHashInv = 1f / 255f;
        private const float WhaleFallDormantFearDurationSeconds = 4f;
        private const float WhaleFallActiveFearDurationSeconds = 2f;
        private const float WhaleFallDormantFearAmount = 0.35f;
        private const float WhaleFallActiveFearAmount = 0.2f;
        private const int FoodChainTelemetryCapacity = 300;
        private const int FoodChainTelemetryEntrySizeBytes = 64;
        private const string FoodChainTelemetryDumpPath = "Docs/AgentLogs/Dump_SARGASSUM_FOOD_CHAIN.bin";
        private const string FoodChainTelemetryDumpPayloadLabel = "sargassumFoodChainTelemetryDumpPayload";
        private const uint FoodChainTelemetryMagicLow = 0x48454354u;
        private const uint FoodChainTelemetryMagicHigh = 0x4643484Eu;
        private const uint FoodChainTelemetryFlagTick = 1u << 0;
        private const uint FoodChainTelemetryFlagKillJobScheduled = 1u << 1;
        private const uint FoodChainTelemetryFlagKillJobCompleted = 1u << 2;
        private const uint FoodChainTelemetryFlagKillDrained = 1u << 3;
        private const uint FoodChainTelemetryFlagWhaleFall = 1u << 4;
        private const uint FoodChainTelemetryFlagBoidsScattered = 1u << 5;
        private const uint FoodChainTelemetryFlagNonFinite = 1u << 31;
        private const uint FoodChainTelemetryAnomalyNonFinite = 0xEFC00001u;
        private const int BoidSensoryBlackBoxCapacity = 300;
        private const int BoidSensoryBlackBoxEntrySizeBytes = 64;
        private const string BoidSensoryBlackBoxDumpPath = "Docs/AgentLogs/Dump_SARGASSUM_BOID_SENSORY.bin";
        private const string BoidSensoryBlackBoxDumpPayloadLabel = "sargassumBoidSensoryBlackBoxDumpPayload";
        private const uint BoidSensoryBlackBoxMagicLow = 0x424F4944u;
        private const uint BoidSensoryBlackBoxMagicHigh = 0x53454E53u;
        private const uint BoidSensoryBlackBoxFlagTick = 1u << 0;
        private const uint BoidSensoryBlackBoxFlagLightActive = 1u << 1;
        private const uint BoidSensoryBlackBoxFlagPingActive = 1u << 2;
        private const uint BoidSensoryBlackBoxFlagCapsule = 1u << 3;
        private const uint BoidSensoryBlackBoxFlagNonFinite = 1u << 31;
        private const uint BoidSensoryBlackBoxAnomalyNonFinite = 0xB01D0001u;
        private const int PredatorAupBufferCapacity = 16;
        private const int PredatorAupStride = sizeof(float) * 4;
        private const int PredatorAupLowTierThreatLoopCap = 4;
        private const int SensoryThreatSlotSubmarine = 0;
        private const int SensoryThreatSlotFlashlight = 1;
        private const int SensoryThreatFirstPingSlot = 2;
        private const int SensoryThreatLastPingSlot = 4;
        private const int SensoryThreatReservedSlots = SensoryThreatLastPingSlot + 1;
        private const float SensoryThreatMinRadiusMeters = 0.1f;
        private const float SensorySubmarineThreatRadiusMeters = 32f;
        private const float SensoryFlashlightDefaultRangeMeters = 24f;
        private const float SensoryFlashlightEndpointScale = 0.72f;
        private const float SensoryFlashlightRadiusScale = 0.28f;
        private const float SensoryFlashlightGrowMetersPerSecond = 42f;
        private const float SensoryFlashlightShrinkMetersPerSecond = 56f;
        private const float SensoryAcousticPingDecayMetersPerSecond = 34f;
        private const float SensoryAcousticPingMinRadiusMeters = 8f;
        private const float SensoryAcousticPingMaxRadiusMeters = 120f;
        private const int SensorySubmarineLightSignalConsumeLimit = 8;
        private const uint SensoryThreatFlagFlashlightCapsule = 1u << 0;
        private const int SwarmAcousticSignalConsumeLimit = 4;
        private const int SwarmMovementSignalConsumeLimit = 8;
        private const float SwarmAcousticShockDurationSeconds = 1f / 60f;
        private const float SwarmMovementPanicDurationSeconds = 0.08f;
        private const float SwarmDispersedSignalCooldownSeconds = 0.08f;
        private const float SwarmDispersedMinimumIntensity = 0.25f;
        private const float MaelstromThreatRefreshSeconds = 0.22f;
        private const float MaelstromThreatDurationSeconds = 0.45f;
        private const float MassiveThreatMaxRadiusMeters = 256f;
        private const float MassiveThreatMaxDurationSeconds = 8f;
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
        private const int LatchStatsReadbackByteCount = LatchStatsElementCount * LatchStatsStride;
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
        private static readonly int _PredatorAUPBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int _EncounterPredatorAUPBufferId = Shader.PropertyToID("_EncounterPredatorAUPBuffer");
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
        private static readonly int _SimulationBucketIndexId = Shader.PropertyToID("_SimulationBucketIndex");
        private static readonly int _SimulationBucketMaskId = Shader.PropertyToID("_SimulationBucketMask");
        private static readonly int _SimulationInterpolationAlphaId = Shader.PropertyToID("_SimulationInterpolationAlpha");

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

        [SerializeField]
        [Tooltip("Authored zero-flow Texture3D bound when no abyssal flow field is published. Runtime Texture3D fallback generation is forbidden.")]
        private Texture3D neutralAbyssalFlowTexture;

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
        private float waterLevel = DefaultWaterLevel;

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

        [SerializeField]
        [Tooltip("Diagnostics-only GPU latch-count readback. Gameplay drag uses a deterministic CPU estimate by default.")]
        private bool enableParasiteLatchGpuReadback;

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

        [SerializeField, Range(4f, 80f)]
        [Tooltip("AUP distance radius used when harvesting static MapMagic rock proxies for formation avoidance.")]
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
        [Tooltip("Active predator/player AUP threat loop count uploaded to the compute shader.")]
        private int _debugPredatorAupThreatCount;

        [SerializeField]
        [Tooltip("Active fixed-slot sensory threat count uploaded to the boid compute shader.")]
        private int _debugBoidSensoryThreatCount;

        [SerializeField]
        [Tooltip("Current flashlight sensory threat radius in the fixed-slot threat buffer.")]
        private float _debugBoidFlashlightThreatRadius;

        [SerializeField]
        [Tooltip("CPU-side consumed GPU boid count emitted by predator bite jobs this session.")]
        private int _debugConsumedBoidCount;

        private Material _boidRenderMaterialSource;

        private HectonBiolumZone[] _deepBiolumZones;
        private float[] _deepBiolumZoneScores;
        private BeaconNetworkSnapshot[] _formationBeaconSnapshots;
        private FormationBeaconData[] _formationBeaconStaging;
        private StaticObstacleData[] _staticObstacleCacheStaging;
        private FormationObstacleData[] _formationObstacleStaging;
        private VaultGenerationHandle<GrazingAnchorData> _grazingAnchorsHandle;
        private VaultGenerationHandle<MassiveThreatData> _massiveThreatsHandle;
        private VaultGenerationHandle<FormationBeaconData> _formationBeaconsHandle;
        private VaultGenerationHandle<FormationObstacleData> _formationObstaclesHandle;
        private VaultGenerationHandle<StaticObstacleData> _staticObstacleCacheHandle;
        private VaultGenerationHandle<BoidData> _boidStateHandle;
        private VaultGenerationHandle<BoidKillSignal> _killSignalHandle;
        private VaultGenerationHandle<int> _killSignalCountHandle;
        private VaultGenerationHandle<FoodChainTelemetryEntry> _foodChainTelemetryRingHandle;
        private VaultGenerationHandle<float3> _leviathanPathScratchHandle;
        private VaultGenerationHandle<LeviathanNodeData> _leviathanNodeFrontHandle;
        private VaultGenerationHandle<LeviathanNodeData> _leviathanNodeBackHandle;
        private VaultGenerationHandle<int> _leviathanNodeCountHandle;
        private VaultGenerationHandle<FoveatedSimulationInput> _foveatedSimulationInputHandle;
        private VaultGenerationHandle<FoveatedSimulationDecision> _foveatedSimulationFrontHandle;
        private VaultGenerationHandle<FoveatedSimulationDecision> _foveatedSimulationBackHandle;
        private VaultGenerationHandle<SimulationFrameConstants> _simulationFrameHandle;
        private VaultGenerationHandle<float4> _boidSensoryThreatsHandle;
        private VaultGenerationHandle<BoidSensoryBlackBoxEntry> _boidSensoryBlackBoxHandle;
        private VaultGenerationHandle<uint> _threatGridUploadHandle;
        private uint[] _threatGridUploadSnapshot;
        private GraphicsBuffer _boidsBufferA;
        private GraphicsBuffer _boidsBufferB;
        private GraphicsBuffer _boidIndirectArgsBuffer;
        private GraphicsBuffer _grazingAnchorBuffer;
        private GraphicsBuffer _massiveThreatBuffer;
        private GraphicsBuffer _formationBeaconBuffer;
        private GraphicsBuffer _formationObstacleBuffer;
        private GraphicsBuffer _leviathanNodeBuffer;
        private GraphicsBuffer _latchStatsBuffer;
        private GraphicsBuffer _parasiteLatchHeldStatsBuffer;
        private GraphicsBuffer _pbdCorrectionBuffer;
        private GraphicsBuffer _threatGridBuffer;
        private GraphicsBuffer _threatVoxelBuffer;
        private GraphicsBuffer _spatialGridCountBuffer;
        private GraphicsBuffer _spatialGridCellBuffer;
        private GraphicsBuffer _simulationFrameBuffer;
        private GraphicsBuffer _predatorAupFallbackBuffer;
        private GraphicsBuffer _boidSensoryThreatBufferA;
        private GraphicsBuffer _boidSensoryThreatBufferB;
        private uint _boidSensoryThreatUploadHashA;
        private uint _boidSensoryThreatUploadHashB;
        private bool _boidSensoryThreatUploadValidA;
        private bool _boidSensoryThreatUploadValidB;
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
        private uint _threadGroupSizeX;
        private uint _clearStatsThreadGroupSizeX;
        private uint _clearSpatialGridThreadGroupSizeX;
        private uint _buildSpatialGridThreadGroupSizeX;
        private uint _clearPbdCorrectionsThreadGroupSizeX;
        private uint _pbdSolveThreadGroupSizeX;
        private uint _applyOriginShiftThreadGroupSizeX;
        private int _dispatchGroupCount = 1;
        private int _clearStatsDispatchGroupCount = 1;
        private int _clearSpatialGridDispatchGroupCount = 1;
        private int _buildSpatialGridDispatchGroupCount = 1;
        private int _clearPbdCorrectionsDispatchGroupCount = 1;
        private int _pbdSolveDispatchGroupCount = 1;
        private ComputeShader _boundBoidCompute;
        private Mesh _boidIndirectArgsMesh;
        private int _boidIndirectArgsInstanceCount = -1;
        private int _frameParity;
        private ISimulationBucketer _simulationBucketer;
        private bool _simulationBucketerProbeAttempted;
        private int _lastFieldRevision = -1;
        private float _simulationInterpolationAlpha = 1f;
        private bool _registeredFixedTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _serviceRegistered;
        private bool _runtimeRoutesRetiredAfterOwnershipLoss;
        private bool _hasSpawnData;
        private bool _renderCurrentBufferRequested;
        private bool _insideMicroFaunaVisualSync;
        private bool _spawnBufferUploadRequested;
        private bool _grazingAnchorUploadRequested;
        private bool _massiveThreatUploadRequested;
        private bool _formationBeaconUploadRequested;
        private bool _formationObstacleUploadRequested;
        private bool _activeLeviathanUploadRequested;
        private int _queuedSpawnBufferUploadCount;
        private bool _threatVoxelPayloadRefreshRequested;
        private bool _originShiftGpuDispatchRequested;
        private Vector3 _queuedOriginShiftGpuDelta;
        private bool _computeKernelBindingsValid;
        private bool _computeStaticBuffersBound;
        private bool _computeDispatchDisabled;
        private bool _coldSupportsComputeShaders;
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
        private bool _foodChainTelemetryDumped;
        private bool _foodChainTelemetryDumpSourceUnavailableLogged;
        private bool _foodChainTelemetryDumpFailureLogged;
        private bool _boidSensoryBlackBoxDumped;
        private bool _boidSensoryBlackBoxDumpSourceUnavailableLogged;
        private bool _boidSensoryBlackBoxDumpFailureLogged;
        private float _pendingPredatorConsumptionTimeSeconds;
        private int _foodChainTelemetryCursor;
        private int _boidSensoryBlackBoxCursor;
        private Vector3 _hitFlashOriginWS;
        private float _hitFlashStartTime = -1000f;
        private float _hitFlashRuntimeRadius;
        private float _hitFlashRuntimeIntensity;
        private float _spatialGridCellSizeWS = 1f;
        private double3 _spatialGridOriginWSD = double3.zero;
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
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerHealth _playerHealth;
        private PlayerFlashlight _playerFlashlight;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private HectonMapMagicVegetationBridge _mapMagicVegetationBridge;
        private MapMagicBridge _mapMagicRuntime;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private WorldProceduralScatterDirector _proceduralScatterDirector;
        private IDataVault _dataVault;
        private IAbyssalFlowGpuReadModel _fluidEngine;
        private ISubmarineRuntimeContext _submarineRuntime;
        private IEncounterDirectorService _encounterDirector;
        private IEcosystemDirectorService _ecosystemDirector;
        private IBeaconNetworkService _beaconNetworkRuntime;
        private IFluidDecalPresentationSink _abyssalFluidDecals;
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
        private bool _parasiteLatchReadbackRepairRequested;
        private bool _parasiteLatchReadbackDisposeAfterCompletion;
        private bool _parasiteLatchReleaseStatsBufferAfterCompletion;
        private AsyncGPUReadbackRequest _parasiteLatchReadbackRequest;
        private Action<AsyncGPUReadbackRequest> _parasiteLatchReadbackCompletion;
        private ParasiteLatchReadbackOwner _parasiteLatchReadback;
        private float _leviathanThreatLevel;
        private Vector3 _leviathanHotspotWS;
        private int _leviathanPathNodeCount;

        private struct ParasiteLatchReadbackOwner
        {
            public NativeArray<int> Data;
        }
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
        private int _activeBoidSensoryThreatCount;
        private uint _boidSensoryPingWriteCursor;
        private float _boidFlashlightThreatRadiusWS;
        private float _boidFlashlightThreatTargetRadiusWS;
        private float _boidFlashlightThreatRangeWS = SensoryFlashlightDefaultRangeMeters;
        private float _boidFlashlightThreatIntensity01;
        private Vector3 _boidFlashlightThreatOriginWS;
        private Vector3 _boidFlashlightThreatForwardWS = Vector3.forward;
        private float _lastSwarmDispersedSignalTime = float.NegativeInfinity;
        private uint _swarmDispersedSequence;
        private uint _lastMaelstromThreatHash;
        private float _nextMaelstromThreatRefreshTime = float.NegativeInfinity;
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
        private SimulationLodTier _lastSimulationLodTier = SimulationLodTier.Full;
        private float _lastSimulationHibernation01;
        private int _viewPoseCacheFrame = -1;
        private bool _viewPoseCacheValid;
        private Vector3 _viewPoseCachePosition;
        private Vector3 _viewPoseCacheForward;
        private bool _playerRuntimeContextProbeAttempted;
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

        public float SimulationInterpolationAlpha => _simulationInterpolationAlpha;

        private void Awake()
        {
            if (Application.isPlaying && TryAbortForUsableExistingRuntime())
                return;

            CacheGraphicsCapabilitiesCold();
            _computeDispatchDisabled = false;
            EnsureBoidMaterialBindingReady();
            SanitizeSettings();
            RefreshRenderLayerCache();
            RefreshRenderScaleCache();
            ResetDependencyProbeCache();
            _simulationBucketerProbeAttempted = false;
            RefreshColdRegistryDependencies();
            RefreshDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayloadVisualSync();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, RefreshCameraDistanceSq());
        }

        private void OnEnable()
        {
            if (Application.isPlaying && TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (Application.isPlaying && !_serviceRegistered)
                return;

            CacheGraphicsCapabilitiesCold();
            _computeDispatchDisabled = false;
            InvalidateViewPoseCache();
            ResetDependencyProbeCache();
            EnsureBoidMaterialBindingReady();
            RefreshRenderLayerCache();
            RefreshRenderScaleCache();
            _simulationBucketerProbeAttempted = false;
            TryRegisterHotSwapListener();
            RefreshColdRegistryDependencies();
            RefreshDependencies();
            EnsureBuffers();
            RefreshThreatVoxelPayloadVisualSync();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, RefreshCameraDistanceSq());
            SargassumGlobalDragManager.Register(this);
            FlashlightEvents.Register(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
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
            _lastSimulationHibernation01 = 0f;
            _debugFormationModeActive = false;
            _debugFormationBeaconCount = 0;
            _debugFormationObstacleCount = 0;
            _debugLeviathanModeActive = false;
            _debugLeviathanNodeCount = 0;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = Vector3.zero;
            _debugFragmentation01 = 0f;
            _debugSonarScatter01 = 0f;
            _debugPredatorAupThreatCount = 0;
            _debugBoidSensoryThreatCount = 0;
            _debugBoidFlashlightThreatRadius = 0f;
            _parasiteLatchReadbackTimer = 0f;
            _parasiteLatchReadbackPending = false;
            _parasiteLatchReadbackRepairRequested = false;
            _parasiteLatchReadbackDisposeAfterCompletion = false;
            _parasiteLatchReleaseStatsBufferAfterCompletion = false;
            _parasiteLatchHeldStatsBuffer = null;
            _reportedParasiteCenterOfMassLS = Vector3.zero;
            _reportedParasiteHarvesterPullWS = Vector3.zero;
            _reportedWakeFleeCount = 0;
            _reportedWakeCenterWS = Vector3.zero;
            _reportedWakeFlowDirectionWS = Vector3.zero;
            _fluidEngine = null;
            _submarineRuntime = null;
            _oceanKinematicsService = null;
            _encounterDirector = null;
            _ecosystemDirector = null;
            _beaconNetworkRuntime = null;
            _abyssalFluidDecals = null;
            _simulationBucketer = null;
            _simulationBucketerProbeAttempted = false;
            _simulationInterpolationAlpha = 1f;
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
            _activeBoidSensoryThreatCount = 0;
            _boidSensoryPingWriteCursor = 0;
            _boidFlashlightThreatRadiusWS = 0f;
            _boidFlashlightThreatTargetRadiusWS = 0f;
            _boidFlashlightThreatRangeWS = SensoryFlashlightDefaultRangeMeters;
            _boidFlashlightThreatIntensity01 = 0f;
            _boidFlashlightThreatOriginWS = Vector3.zero;
            _boidFlashlightThreatForwardWS = Vector3.forward;
            _foodChainTelemetryDumped = false;
            _foodChainTelemetryDumpSourceUnavailableLogged = false;
            _foodChainTelemetryDumpFailureLogged = false;
            _boidSensoryBlackBoxCursor = 0;
            _boidSensoryBlackBoxDumped = false;
            _boidSensoryBlackBoxDumpSourceUnavailableLogged = false;
            _boidSensoryBlackBoxDumpFailureLogged = false;
            _lastSwarmDispersedSignalTime = float.NegativeInfinity;
            _swarmDispersedSequence = 0u;
            ResetThreatVoxelSnapshot();
            _lastDeepLeviathanMode = false;
            TryUnregister();
            ClearStatisticalPopulationPoint();
            CompletePendingReadbackAndReleaseBuffers();
            ReleaseBoidMaterialBinding();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
        }

        private void OnDestroy()
        {
            ResetDependencyProbeCache();
            _fluidEngine = null;
            _submarineRuntime = null;
            _oceanKinematicsService = null;
            _encounterDirector = null;
            _ecosystemDirector = null;
            _beaconNetworkRuntime = null;
            _abyssalFluidDecals = null;
            _simulationBucketer = null;
            _simulationBucketerProbeAttempted = false;
            TryUnregisterService();
            SargassumGlobalDragManager.Unregister(this);
            FlashlightEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ClearStatisticalPopulationPoint();
            CompletePendingReadbackAndReleaseBuffers();
            ReleaseBoidMaterialBinding();
        }

        private bool EnsureBoidMaterialBindingReady()
        {
            Material source = boidMaterial;
            if (source == null)
            {
                ReleaseBoidMaterialBinding();
                return false;
            }

            if (ReferenceEquals(_boidRenderMaterialSource, source))
                return true;

            _boidRenderMaterialSource = source;
            return true;
        }

        private void ReleaseBoidMaterialBinding()
        {
            _boidRenderMaterialSource = null;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector3(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            InvalidateViewPoseCache();
            ApplyRuntimeOffsetToSwarmData(-shiftOffset);
        }

        private void RunMicroFaunaVisualSync(float dt)
        {
            RecordFoodChainTelemetry(FoodChainTelemetryFlagTick, _fieldCenter, 0u, 0u);

            if (_statisticalPopulationActive)
            {
                _debugVisible = false;
                _debugHibernation01 = 1f;
                _lastSimulationHibernation01 = 1f;
                return;
            }

            RefreshCachedRuntimeInputState();

            if (!_hasSpawnData || boidMaterial == null || boidMesh == null)
            {
                _lastSimulationHibernation01 = 1f;
                return;
            }

            if (_activeBoidCount <= 0)
            {
                _debugVisible = false;
                _debugHibernation01 = 1f;
                _lastSimulationHibernation01 = 1f;
                return;
            }

            ConsumeSwarmThreatSignals(dt);

            if (!_threatVoxelDataValid && _mapMagicVegetationBridge != null)
                RefreshThreatVoxelPayloadVisualSync();
            float cameraDistanceSq = RefreshCameraDistanceSq();
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
            RefreshMaelstromThreats();
            UpdateMassiveThreats();
            UpdateParasiteLatchReadback(deltaTime);
            float hibernation01 = 0f;
            bool shouldRender = ShouldRenderSwarm(cameraDistanceSq);
            bool dispatchedSimulation = ConsumeSimulationStep(
                deltaTime,
                cameraDistanceSq,
                out float simulationDeltaTime,
                out hibernation01,
                out SimulationLodTier simulationLodTier);
            bool shouldDispatchSleepVelocityWrite = simulationLodTier == SimulationLodTier.Sleep && _sleepVelocityWritePending;
            bool shouldDispatchSimulation = dispatchedSimulation && (shouldRender || ShouldMaintainOffscreenBoidSimulation(simulationLodTier));
            bool leaderFollowerSchooling = _formationModeActive && !_parasiteModeActive && _leviathanModeBlend < 0.001f;
            bool shouldCollectLatchStats = ShouldCollectLatchStats(simulationLodTier, leaderFollowerSchooling, shouldRender);
            if (IsFaunaAmbientDriftKillSwitchActive())
            {
                shouldDispatchSimulation = false;
                shouldDispatchSleepVelocityWrite = false;
                hibernation01 = math.max(hibernation01, 1f);
            }

            if (shouldDispatchSimulation || shouldDispatchSleepVelocityWrite)
            {
                bool runFullGridSolve = simulationLodTier == SimulationLodTier.Full && !leaderFollowerSchooling;
                if (runFullGridSolve)
                    UpdateSpatialGridLayout();

                if (_dispatchGroupCount <= 0 ||
                    (runFullGridSolve &&
                        (_clearSpatialGridDispatchGroupCount <= 0 ||
                         _buildSpatialGridDispatchGroupCount <= 0 ||
                         _clearPbdCorrectionsDispatchGroupCount <= 0 ||
                         _pbdSolveDispatchGroupCount <= 0)))
                {
                    DisableComputeDispatch(ComputeDisableReasonDispatchGroupLimit);
                    RenderStaticFallback(cameraDistanceSq, hibernation01: 1f);
                    return;
                }

                if (BindSimulationUniforms(simulationDeltaTime, currentDriftOffset, driftDelta, hibernation01, simulationLodTier, shouldRender, shouldCollectLatchStats))
                {
                    long watchdogStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        if (shouldCollectLatchStats)
                            DispatchClearLatchStats();

                        if (runFullGridSolve)
                        {
                            DispatchClearSpatialGrid();
                            DispatchClearPbdCorrections();
                            boidCompute.Dispatch(_buildSpatialGridKernelIndex, _buildSpatialGridDispatchGroupCount, 1, 1);
                            boidCompute.Dispatch(_pbdSolveKernelIndex, _pbdSolveDispatchGroupCount, 1, 1);
                        }

                        boidCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
                        if (simulationLodTier == SimulationLodTier.Sleep)
                            _sleepVelocityWritePending = false;
                        if (shouldCollectLatchStats)
                            TryRequestParasiteLatchReadback(hibernation01);

                        _frameParity ^= 1;
                    }
                    catch (ObjectDisposedException)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
                    }
                    catch (InvalidOperationException)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
                    }
                    catch (ArgumentException)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
                    }
                    catch (MissingReferenceException)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
                    }
                    catch (UnityException)
                    {
                        DisableComputeDispatch(ComputeDisableReasonDispatchFailure);
                    }

                    ReportWatchdogCost(FaunaBucketedSimulationCostHash, watchdogStart);
                }
            }

            _debugVisible = shouldRender;
            _debugHibernation01 = hibernation01;
            _lastSimulationHibernation01 = hibernation01;
            if (shouldRender)
                QueueRenderCurrentBuffer();

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
            FlushParasiteLatchReadbackRepairSlow();
            if (_statisticalPopulationActive)
            {
                float statisticalCameraDistanceSq = RefreshCameraDistanceSq();
                RefreshStatisticalMigrationPopulation(force: false);
                TryRematerializeStatisticalPopulation(statisticalCameraDistanceSq);
                return;
            }

            RefreshCachedRuntimeInputState();

            float cameraDistanceSq = RefreshCameraDistanceSq();
            if (TryDematerializeStatisticalPopulation(cameraDistanceSq))
                return;

            QueueThreatVoxelPayloadRefresh();
            bool populationBudgetChanged = RefreshActiveBoidCount();
            RefreshSpawnData(force: populationBudgetChanged);
        }

        /// <summary>
        /// Publishes completed CPU-side simulation decision jobs in the dispatcher-owned late-frame window.
        /// </summary>
        public void LateFrameTick()
        {
            _insideMicroFaunaVisualSync = true;
            try
            {
                FlushThreatVoxelPayloadRefreshVisualSync();
                RunMicroFaunaVisualSync(SystemDispatcher.CurrentFrameDeltaTime);
                CompletePendingFoveatedSimulationDecision(forceComplete: false);
                CompletePendingPredatorConsumption(forceComplete: false);
                FlushQueuedMicroFaunaGpuUploads();
                if (_renderCurrentBufferRequested)
                {
                    RenderCurrentBuffer();
                    _renderCurrentBufferRequested = false;
                }
            }
            finally
            {
                _insideMicroFaunaVisualSync = false;
            }
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.WorldGen:
                    _proceduralScatterDirector = currentService as WorldProceduralScatterDirector;
                    WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref _proceduralScatterDirector);
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    _biomeMatrixDirector = currentService as BiomeMatrixDirector;
                    WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _mapMagicVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);
                    _threatVoxelPayloadRefreshRequested = _mapMagicVegetationBridge != null;
                    SyncWaterSurfaceLevelFromRuntime();
                    break;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:
                    ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_mapMagicRuntime, previousService))
                        _mapMagicRuntime = null;
                    MapMagicBridge currentMapMagic = currentService as MapMagicBridge;
                    if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref currentMapMagic))
                        _mapMagicRuntime = currentMapMagic;
                    else
                        _mapMagicRuntime = null;
                    SyncWaterSurfaceLevelFromRuntime();
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    SyncWaterSurfaceLevelFromRuntime();
                    break;
                case GlobalRegistryServiceSlot.BiolumManagerRuntime:
                    biolumManager = currentService as HectonBiolumManager;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    dragManager = currentService as SargassumGlobalDragManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref dragManager);
                    break;
                case GlobalRegistryServiceSlot.SargassumCutRuntime:
                    cutManager = currentService as SargassumCutManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _fluidEngine = currentService as IAbyssalFlowGpuReadModel;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntime = currentService as ISubmarineRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.EncounterDirector:
                    _encounterDirector = currentService as IEncounterDirectorService;
                    break;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    EcosystemDirector ecosystemDirector = currentService as EcosystemDirector;
                    if (WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector))
                        _ecosystemDirector = ecosystemDirector;
                    else
                        _ecosystemDirector = null;
                    break;
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime:
                    _beaconNetworkRuntime = currentService as IBeaconNetworkService;
                    break;
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime:
                    _abyssalFluidDecals = currentService as IFluidDecalPresentationSink;
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    _simulationBucketerProbeAttempted = true;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _playerRuntimeContextProbeAttempted = true;
                    RefreshPlayerReferencesFromCachedContext(_playerRuntimeContext);
                    InvalidateViewPoseCache();
                    break;
            }
        }

        private void RefreshColdRegistryDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
            if (biolumManager == null)
                biolumManager = GlobalRegistry.BiolumManager;

            WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref dragManager);

            WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);

            if (_fluidEngine == null)
                _fluidEngine = GlobalRegistry.AbyssalFlowGpu;

            if (_submarineRuntime == null)
                _submarineRuntime = GlobalRegistry.Submarine;

            if (_encounterDirector == null)
                _encounterDirector = GlobalRegistry.EncounterDirector;

            if (_ecosystemDirector == null)
            {
                EcosystemDirector ecosystemDirector = null;
                if (WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector))
                    _ecosystemDirector = ecosystemDirector;
            }

            if (_beaconNetworkRuntime == null)
                _beaconNetworkRuntime = GlobalRegistry.BeaconNetworkService;

            if (_abyssalFluidDecals == null)
                _abyssalFluidDecals = GlobalRegistry.FluidDecalPresentation;

            if (_proceduralScatterDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref _proceduralScatterDirector);

            if (_biomeMatrixDirector == null || !_biomeMatrixDirector.isActiveAndEnabled)
            {
                _biomeMatrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
            }

            if (_mapMagicVegetationBridge == null)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);

            if (_mapMagicRuntime == null || !_mapMagicRuntime.isActiveAndEnabled)
            {
                _mapMagicRuntime = null;
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);
            }
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            SyncWaterSurfaceLevelFromRuntime();

            _playerRuntimeContext = GlobalRegistry.Player;
            _playerRuntimeContextProbeAttempted = true;
            RefreshPlayerReferencesFromCachedContext(_playerRuntimeContext);

            if (_simulationBucketer == null)
                _simulationBucketer = GlobalRegistry.SimulationBucketer;

            _simulationBucketerProbeAttempted = true;
        }

        private void SyncWaterSurfaceLevelFromRuntime()
        {
            if (TryResolveOceanWaterLevel(out float oceanWaterLevel))
            {
                waterLevel = oceanWaterLevel;
                return;
            }

            MapMagicBridge bridge = _mapMagicRuntime;
            if (!WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge))
                return;

            _mapMagicRuntime = bridge;
            float bridgedWaterLevel = bridge.WaterSurfaceLevel;
            if (TryResolveWaterLevel(bridgedWaterLevel, out float resolvedWaterLevel))
                waterLevel = resolvedWaterLevel;
        }

        private bool TryResolveOceanWaterLevel(out float resolvedWaterLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterLevel(oceanKinematics.SeaLevel, out resolvedWaterLevel))
            {
                return true;
            }

            resolvedWaterLevel = DefaultWaterLevel;
            return false;
        }

        private static bool TryResolveOceanWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            // [BLOCKING_SYNC_POINT] DataVault owner replacement invalidates native handles; fence writers before rebinding.
            CompletePendingPredatorConsumption(forceComplete: true);
            CompletePendingFoveatedSimulationDecision(forceComplete: true);
            JobHandle disposeDependency = CancelPendingLeviathanNodeBuildForDispose();
            ClearStatisticalPopulationPoint();
            _hasSpawnData = false;
            _debugVisible = false;
            _debugActiveBoidCount = 0;
            _queuedSpawnBufferUploadCount = 0;
            _computeStaticBuffersBound = false;
            _threatVoxelPayloadRefreshRequested = false;
            ResetThreatGridSnapshot();
            ResetThreatVoxelSnapshot();
            ClearVaultHandles(disposeDependency);
            _dataVault = currentVault;
            if (_dataVault == null || !isActiveAndEnabled)
            {
                _computeDispatchDisabled = true;
                return;
            }

            _computeDispatchDisabled = false;
            EnsureBuffers();
            RefreshThreatVoxelPayloadVisualSync();
            RefreshSpawnData(force: true);
            PrimeFoveatedSimulationDecision(0f, RefreshCameraDistanceSq());
        }

        private void RefreshDependencies()
        {
            RefreshColdRegistryDependencies();
            if (!_runtimeServiceProbeAttempted && (_worldZoneDirector == null || !_worldZoneDirector.isActiveAndEnabled))
            {
                _worldZoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);
            }

            _runtimeServiceProbeAttempted = true;
        }

        private void RefreshCachedRuntimeInputState()
        {
            RefreshPlayerReferencesFromCachedContext(_playerRuntimeContext);
        }

        private void RefreshPlayerReferencesFromCachedContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext != null && playerContext.IsInitialized)
            {
                playerTransform ??= playerContext.PlayerTransform;
                _playerMovement ??= playerContext.PlayerMovement;
                _playerTransportCoordinator ??= playerContext.PlayerTransportCoordinator;
                _playerHealth ??= playerContext.PlayerHealth;
                _playerFlashlight ??= playerContext.Flashlight;
                if (viewCamera == null)
                    viewCamera = playerContext.PlayerCamera;

                _playerTransformProbeAttempted = true;
                _viewCameraProbeAttempted = viewCamera != null;
            }

            if (_playerFlashlight != null)
                _flashlightOn = _playerFlashlight.IsOn;
        }

        private void ResetDependencyProbeCache()
        {
            _playerTransformProbeAttempted = false;
            _viewCameraProbeAttempted = false;
            _runtimeServiceProbeAttempted = false;
            _playerRuntimeContextProbeAttempted = false;
        }

        private void SanitizeSettings()
        {
            boidCount = math.clamp(boidCount, 128, 2048);
            boidCount = VRAMEnforcer.ApplyBoidPopulationBudget(boidCount, 128, 2048);
            maxSpawnAttempts = math.clamp(maxSpawnAttempts, 4, 32);
            densityThreshold = SaturateFinite01(densityThreshold);
            windowThreshold = ClampFinite(windowThreshold, 0f, 0.75f);
            cruiseSpeed = ClampMinFinite(cruiseSpeed, 0.1f);
            maxSpeed = ClampMinFinite(maxSpeed, cruiseSpeed);
            panicSpeedBoost = ClampMinFinite(panicSpeedBoost, 0f);
            perceptionRadius = ClampMinFinite(perceptionRadius, 0.25f);
            separationRadius = ClampFinite(separationRadius, 0.1f, perceptionRadius);
            boidBodyRadius = ClampFinite(boidBodyRadius, 0.02f, separationRadius * 0.5f);
            consumedCollapseSpeed = ClampFinite(consumedCollapseSpeed, 2f, 24f);
            gradientWorldStep = ClampMinFinite(gradientWorldStep, 0.05f);
            waterLevel = TryResolveWaterLevel(waterLevel, out float resolvedWaterLevel)
                ? resolvedWaterLevel
                : DefaultWaterLevel;
            minDepthBelowSurface = ClampMinFinite(minDepthBelowSurface, 0.1f);
            maxDepthBelowSurface = ClampMinFinite(maxDepthBelowSurface, minDepthBelowSurface + 0.1f);
            panicThreshold = SaturateFinite01(panicThreshold);
            panicDecay = ClampMinFinite(panicDecay, 0.1f);
            grazingAnchorCount = math.clamp(grazingAnchorCount, 4, 96);
            grazingRadius = ClampFinite(grazingRadius, 0.25f, 6f);
            grazingWeight = ClampFinite(grazingWeight, 0f, 4f);
            canopyAffinityWeight = ClampFinite(canopyAffinityWeight, 0f, 4f);
            grazingDensityThreshold = SaturateFinite01(grazingDensityThreshold);
            grazingRestSpeedScale = ClampFinite(grazingRestSpeedScale, 0.05f, 0.6f);
            grazingRestHoldThreshold = SaturateFinite01(grazingRestHoldThreshold);
            panicPlayerSpeedThreshold = ClampFinite(panicPlayerSpeedThreshold, 0.5f, 8f);
            panicPlayerRadius = ClampFinite(panicPlayerRadius, 0.5f, 12f);
            cameraAvoidRadius = ClampFinite(cameraAvoidRadius, 0.25f, 3f);
            cameraAvoidWeight = ClampFinite(cameraAvoidWeight, 0f, 8f);
            voxelAvoidanceLookAheadDistance = ClampFinite(voxelAvoidanceLookAheadDistance, 0.25f, 12f);
            voxelAvoidanceWeight = 0f;
            fragmentationWeight = ClampFinite(fragmentationWeight, 0f, 12f);
            fragmentationOffsetScale = ClampFinite(fragmentationOffsetScale, 0.5f, 3f);
            fragmentationMinDurationSeconds = ClampFinite(fragmentationMinDurationSeconds, 1f, MassiveThreatMaxDurationSeconds);
            fragmentationMaxDurationSeconds = ClampFinite(fragmentationMaxDurationSeconds, fragmentationMinDurationSeconds, MassiveThreatMaxDurationSeconds);
            activeSonarWaveSpeed = ClampFinite(activeSonarWaveSpeed, 0.1f, 200f);
            activeSonarWaveBandWidth = ClampFinite(activeSonarWaveBandWidth, 0.25f, 16f);
            activeSonarScatterImpulse = ClampFinite(activeSonarScatterImpulse, 0f, 64f);
            activeSonarScatterWeight = ClampFinite(activeSonarScatterWeight, 0f, 16f);
            maxMassiveThreatCount = math.clamp(maxMassiveThreatCount, 1, 8);
            massiveThreatPanicRadius = ClampFinite(massiveThreatPanicRadius, 50f, 96f);
            massiveThreatWeight = ClampFinite(massiveThreatWeight, 0f, 12f);
            deepBiolumAnchorCapacity = math.clamp(deepBiolumAnchorCapacity, 1, 16);
            deepBiolumSearchRadius = ClampFinite(deepBiolumSearchRadius, 10f, 250f);
            deepBaitBallRadius = ClampFinite(deepBaitBallRadius, 0.5f, 12f);
            deepBaitBallHeight = ClampFinite(deepBaitBallHeight, 0.25f, 8f);
            deepClusterWeight = ClampFinite(deepClusterWeight, 0f, 8f);
            deepHeadlightPanicDuration = ClampFinite(deepHeadlightPanicDuration, 0.1f, 10f);
            deepHeadlightPanicRadiusScale = ClampFinite(deepHeadlightPanicRadiusScale, 1f, 6f);
            boidVatFrameCount = math.max(1, boidVatFrameCount);
            boidVatPlaybackSpeed = ClampMinFinite(boidVatPlaybackSpeed, 0f);
            boidVatInstancePhaseScale = ClampMinFinite(boidVatInstancePhaseScale, 0f);
            boidVatPositionScale = ClampMinFinite(boidVatPositionScale, 0.0001f);
            boidVatNormalBlend = SaturateFinite01(boidVatNormalBlend);
            hitFlashDurationSeconds = ClampMinFinite(hitFlashDurationSeconds, 0.01f);
            hitFlashRadiusMeters = ClampMinFinite(hitFlashRadiusMeters, 0f);
            hitFlashIntensity = SaturateFinite01(hitFlashIntensity);
            hitFlashBloatMeters = ClampFinite(hitFlashBloatMeters, 0f, 0.12f);
            parasiteDroneWorldYThreshold = ClampFinite(parasiteDroneWorldYThreshold, -4000f, -1000f);
            parasiteAffinityWeight = ClampFinite(parasiteAffinityWeight, 0f, 12f);
            parasiteHullStressIntensity = SaturateFinite01(parasiteHullStressIntensity);
            parasiteHullStressLightBoost = SaturateFinite01(parasiteHullStressLightBoost);
            parasiteLatchRadius = ClampFinite(parasiteLatchRadius, 0.5f, 8f);
            parasiteMaxLatchedDronesForFullDrag = math.clamp(parasiteMaxLatchedDronesForFullDrag, 1, 96);
            parasiteMaxEnvironmentalDragMultiplier = ClampFinite(parasiteMaxEnvironmentalDragMultiplier, 1f, 4f);
            parasiteLatchReadbackInterval = ClampFinite(parasiteLatchReadbackInterval, 0.05f, 0.5f);
            parasiteHarvesterLatchThreshold = math.clamp(parasiteHarvesterLatchThreshold, 1, 32);
            parasiteHarvesterFullLatchCount = math.clamp(parasiteHarvesterFullLatchCount, parasiteHarvesterLatchThreshold, 96);
            formationBeaconCapacity = math.clamp(formationBeaconCapacity, 1, 8);
            formationBeaconSearchRadius = ClampFinite(formationBeaconSearchRadius, 8f, 250f);
            formationWeight = ClampFinite(formationWeight, 0f, 8f);
            formationRingThickness = ClampFinite(formationRingThickness, 0.1f, 12f);
            formationPulseAmplitude = ClampFinite(formationPulseAmplitude, 0f, 2f);
            formationPulseSpeed = ClampFinite(formationPulseSpeed, 0.1f, 4f);
            formationBreakPanicThreshold = SaturateFinite01(formationBreakPanicThreshold);
            formationObstacleCapacity = math.clamp(formationObstacleCapacity, 1, 16);
            formationObstacleSearchRadius = ClampFinite(formationObstacleSearchRadius, 4f, 80f);
            formationObstacleWeight = ClampFinite(formationObstacleWeight, 0f, 8f);
            leviathanNodeCapacity = math.clamp(leviathanNodeCapacity, 8, 64);
            leviathanThreatThreshold = SaturateFinite01(leviathanThreatThreshold);
            leviathanHotspotMinDistance = ClampFinite(leviathanHotspotMinDistance, 10f, 200f);
            leviathanHotspotMaxDistance = ClampFinite(leviathanHotspotMaxDistance, leviathanHotspotMinDistance, 400f);
            leviathanBodyWeight = ClampFinite(leviathanBodyWeight, 0f, 8f);
            leviathanForwardWeight = ClampFinite(leviathanForwardWeight, 0f, 8f);
            leviathanBodyRadius = ClampFinite(leviathanBodyRadius, 0.5f, 12f);
            leviathanWaveAmplitude = ClampFinite(leviathanWaveAmplitude, 0f, 2f);
            leviathanWaveFrequency = ClampFinite(leviathanWaveFrequency, 0.1f, 6f);
            leviathanSurroundThreatThreshold = ClampFinite(leviathanSurroundThreatThreshold, 0.6f, 1f);
            leviathanSurroundRadius = ClampFinite(leviathanSurroundRadius, 4f, 48f);
            leviathanSurroundWeight = ClampFinite(leviathanSurroundWeight, 0f, 8f);
            leviathanSurroundSpinSpeed = ClampFinite(leviathanSurroundSpinSpeed, 0.1f, 4f);
            leviathanModeBlendSharpness = ClampFinite(leviathanModeBlendSharpness, 0.1f, 12f);
            simulationCullDistance = SleepSimulationDistanceMeters;
            hibernationStartDistance = FullSimulationDistanceMeters;
            hibernationMaxStepSeconds = ClampFinite(hibernationMaxStepSeconds, 1f / 60f, 0.5f);
            hibernationMinTimeScale = ClampFinite(hibernationMinTimeScale, 0.1f, 1f);
            leviathanStrikeRadius = ClampFinite(leviathanStrikeRadius, 1f, 24f);
            leviathanStrikeTraumaWeight = SaturateFinite01(leviathanStrikeTraumaWeight);
            leviathanStrikeImpulse = ClampFinite(leviathanStrikeImpulse, 1f, 120f);
            leviathanStrikeDamage = ClampFinite(leviathanStrikeDamage, 0.1f, 100f);
            leviathanStrikeCooldown = ClampFinite(leviathanStrikeCooldown, 0.05f, 2f);
            leviathanShockwaveSpeedThreshold = ClampFinite(leviathanShockwaveSpeedThreshold, 2f, 40f);
            leviathanShockwaveRadius = ClampFinite(leviathanShockwaveRadius, 2f, 32f);
            leviathanShockwaveImpulse = ClampFinite(leviathanShockwaveImpulse, 2f, 96f);
            leviathanShockwaveCadence = ClampFinite(leviathanShockwaveCadence, 0.05f, 1.5f);
            _activeBoidCount = math.clamp(_activeBoidCount <= 0 ? boidCount : _activeBoidCount, 128, boidCount);
        }

        private void EnsureBuffers()
        {
            _boidMeshVertexCount = boidMesh != null ? boidMesh.vertexCount : 0;

            if (neutralAbyssalFlowTexture == null)
            {
                Hecton8.Core.H8Debug.LogError("[SargassumMicroFaunaBoids] Missing authored neutral abyssal-flow Texture3D. Runtime texture fallback generation is forbidden.", this);
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return;
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

            if (_formationBeaconSnapshots == null || _formationBeaconSnapshots.Length != 24)
            {
                // COLD ALLOC: BeaconSnapshot[24] - nearby abyss beacon copy buffer for hive-mind formation - owner: SargassumMicroFaunaBoids
                _formationBeaconSnapshots = new BeaconNetworkSnapshot[24];
            }

            if (_formationBeaconStaging == null || _formationBeaconStaging.Length != formationBeaconCapacity)
            {
                // COLD ALLOC: FormationBeaconData[formationBeaconCapacity] - pre-lock formation beacon staging - owner: SargassumMicroFaunaBoids
                _formationBeaconStaging = new FormationBeaconData[formationBeaconCapacity];
            }

            int staticObstacleCapacity = math.max(formationObstacleCapacity * 8, formationObstacleCapacity);
            if (_staticObstacleCacheStaging == null || _staticObstacleCacheStaging.Length != staticObstacleCapacity)
            {
                // COLD ALLOC: StaticObstacleData[formationObstacleCapacity*8] - pre-lock static formation obstacle staging - owner: SargassumMicroFaunaBoids
                _staticObstacleCacheStaging = new StaticObstacleData[staticObstacleCapacity];
            }

            if (_formationObstacleStaging == null || _formationObstacleStaging.Length != formationObstacleCapacity)
            {
                // COLD ALLOC: FormationObstacleData[formationObstacleCapacity] - pre-lock formation obstacle staging - owner: SargassumMicroFaunaBoids
                _formationObstacleStaging = new FormationObstacleData[formationObstacleCapacity];
            }

            bool buffersChanged = false;
            buffersChanged |= EnsureGpuWriteBuffer(ref _boidsBufferA, boidCount, BoidStride);
            buffersChanged |= EnsureGpuWriteBuffer(ref _boidsBufferB, boidCount, BoidStride);
            buffersChanged |= EnsureBuffer(ref _grazingAnchorBuffer, grazingAnchorCount, GrazingAnchorStride);
            buffersChanged |= EnsureBuffer(ref _massiveThreatBuffer, maxMassiveThreatCount, MassiveThreatStride);
            buffersChanged |= EnsureBuffer(ref _formationBeaconBuffer, formationBeaconCapacity, FormationBeaconStride);
            buffersChanged |= EnsureBuffer(ref _formationObstacleBuffer, formationObstacleCapacity, FormationObstacleStride);
            buffersChanged |= EnsureBuffer(ref _leviathanNodeBuffer, leviathanNodeCapacity, LeviathanNodeStride);
            buffersChanged |= EnsureGpuWriteRawBuffer(ref _latchStatsBuffer, ref _latchStatsBufferRawTarget, LatchStatsElementCount, LatchStatsStride);
            buffersChanged |= EnsureGpuWriteRawBuffer(ref _pbdCorrectionBuffer, ref _pbdCorrectionBufferRawTarget, boidCount * PbdCorrectionScalarCount, PbdCorrectionRawStride);
            buffersChanged |= EnsureBuffer(ref _threatGridBuffer, ThreatGridMaxCellCount, ThreatGridStride);
            EnsureThreatGridUploadSnapshotCold();
            buffersChanged |= EnsureBuffer(ref _threatVoxelBuffer, 1, ThreatVoxelStride);
            buffersChanged |= EnsureGpuWriteRawBuffer(ref _spatialGridCountBuffer, ref _spatialGridCountBufferRawTarget, SpatialGridMaxCellCount, SpatialGridCountStride);
            buffersChanged |= EnsureGpuWriteBuffer(ref _spatialGridCellBuffer, SpatialGridMaxCellCount * SpatialGridMaxBoidsPerCell, SpatialGridCellEntryStride);
            buffersChanged |= EnsureBuffer(ref _simulationFrameBuffer, 1, SimulationFrameConstantsStride);
            buffersChanged |= EnsurePredatorAupFallbackBuffer();
            buffersChanged |= TryEnsureBoidIndirectArgsBufferCold();
            bool sensoryBuffersChanged = false;
            sensoryBuffersChanged |= EnsureBuffer(ref _boidSensoryThreatBufferA, PredatorAupBufferCapacity, PredatorAupStride);
            sensoryBuffersChanged |= EnsureBuffer(ref _boidSensoryThreatBufferB, PredatorAupBufferCapacity, PredatorAupStride);
            if (sensoryBuffersChanged)
                ResetBoidSensoryThreatUploadCache();
            buffersChanged |= sensoryBuffersChanged;
            EnsureFallbackAbyssalFlowTexture();
            if (buffersChanged)
                _computeStaticBuffersBound = false;
            IDataVault vault = _dataVault;
            EnsureSargassumVaultGenerationHandle(vault, ref _grazingAnchorsHandle, BufferID.SargassumGrazingAnchors, grazingAnchorCount);
            EnsureSargassumVaultGenerationHandle(vault, ref _massiveThreatsHandle, BufferID.SargassumMassiveThreats, maxMassiveThreatCount);
            EnsureSargassumVaultGenerationHandle(vault, ref _formationBeaconsHandle, BufferID.SargassumFormationBeacons, formationBeaconCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _formationObstaclesHandle, BufferID.SargassumFormationObstacles, formationObstacleCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _staticObstacleCacheHandle, BufferID.SargassumStaticObstacleCache, staticObstacleCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _boidStateHandle, BufferID.SargassumBoidState, boidCount);
            EnsureSargassumVaultGenerationHandle(vault, ref _leviathanNodeFrontHandle, BufferID.SargassumLeviathanNodeFront, leviathanNodeCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _leviathanNodeBackHandle, BufferID.SargassumLeviathanNodeBack, leviathanNodeCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _leviathanNodeCountHandle, BufferID.SargassumLeviathanNodeCount, 1);
            EnsureSargassumVaultGenerationHandle(vault, ref _leviathanPathScratchHandle, BufferID.SargassumLeviathanPathScratch, leviathanNodeCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _foveatedSimulationInputHandle, BufferID.SargassumFoveatedSimulationInput, 1);
            EnsureSargassumVaultGenerationHandle(vault, ref _foveatedSimulationFrontHandle, BufferID.SargassumFoveatedSimulationFront, 1);
            EnsureSargassumVaultGenerationHandle(vault, ref _foveatedSimulationBackHandle, BufferID.SargassumFoveatedSimulationBack, 1);
            EnsureSargassumVaultGenerationHandle(vault, ref _simulationFrameHandle, BufferID.SargassumSimulationFrame, 1);
            EnsureSargassumVaultGenerationHandle(vault, ref _boidSensoryThreatsHandle, BufferID.SargassumBoidSensoryThreats, PredatorAupBufferCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _boidSensoryBlackBoxHandle, BufferID.SargassumBoidSensoryBlackBox, BoidSensoryBlackBoxCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _threatGridUploadHandle, BufferID.SargassumThreatGridUpload, ThreatGridMaxCellCount);
            EnsureSargassumVaultGenerationHandle(vault, ref _foodChainTelemetryRingHandle, BufferID.SargassumFoodChainTelemetryRing, FoodChainTelemetryCapacity);
            EnsureSargassumVaultGenerationHandle(vault, ref _killSignalHandle, BufferID.SargassumKillSignals, PredatorKillSignalDrainLimit);
            EnsureSargassumVaultGenerationHandle(vault, ref _killSignalCountHandle, BufferID.SargassumKillSignalCount, 1);
            _inactiveStatisticalSwarmRing.EnsureCapacity(vault, BufferID.SargassumInactiveSwarmRing, InactiveStatisticalSwarmRingCapacity, nameof(_inactiveStatisticalSwarmRing));
            _inactiveStatisticalSwarmCenterRing.EnsureCapacity(vault, BufferID.SargassumInactiveSwarmCenterRing, InactiveStatisticalSwarmRingCapacity, nameof(_inactiveStatisticalSwarmCenterRing));

            if (!ValidateGpuStructLayouts())
                return;

            if (boidCompute == null)
            {
                ResetComputeKernelBindings();
                return;
            }

            EnsureComputeKernelBindings();
        }

        private bool HasRequiredMicroFaunaStorage()
        {
            return HasSargassumReadOnlyStorage(in _boidStateHandle, BufferID.SargassumBoidState, boidCount) &&
                   HasSargassumReadOnlyStorage(in _grazingAnchorsHandle, BufferID.SargassumGrazingAnchors, grazingAnchorCount) &&
                   HasSargassumReadOnlyStorage(in _massiveThreatsHandle, BufferID.SargassumMassiveThreats, maxMassiveThreatCount) &&
                   HasSargassumReadOnlyStorage(in _formationBeaconsHandle, BufferID.SargassumFormationBeacons, formationBeaconCapacity) &&
                   HasSargassumReadOnlyStorage(in _formationObstaclesHandle, BufferID.SargassumFormationObstacles, formationObstacleCapacity) &&
                   HasSargassumReadOnlyStorage(in _staticObstacleCacheHandle, BufferID.SargassumStaticObstacleCache, math.max(formationObstacleCapacity * 8, formationObstacleCapacity)) &&
                   HasSargassumReadOnlyStorage(in _leviathanNodeFrontHandle, BufferID.SargassumLeviathanNodeFront, leviathanNodeCapacity) &&
                   HasSargassumReadOnlyStorage(in _leviathanNodeBackHandle, BufferID.SargassumLeviathanNodeBack, leviathanNodeCapacity) &&
                   HasSargassumReadOnlyStorage(in _leviathanNodeCountHandle, BufferID.SargassumLeviathanNodeCount, 1) &&
                   HasSargassumReadOnlyStorage(in _leviathanPathScratchHandle, BufferID.SargassumLeviathanPathScratch, leviathanNodeCapacity) &&
                   HasSargassumReadOnlyStorage(in _foveatedSimulationInputHandle, BufferID.SargassumFoveatedSimulationInput, 1) &&
                   HasSargassumReadOnlyStorage(in _foveatedSimulationFrontHandle, BufferID.SargassumFoveatedSimulationFront, 1) &&
                   HasSargassumReadOnlyStorage(in _foveatedSimulationBackHandle, BufferID.SargassumFoveatedSimulationBack, 1) &&
                   HasSargassumReadOnlyStorage(in _simulationFrameHandle, BufferID.SargassumSimulationFrame, 1);
        }

        private bool HasSargassumReadOnlyStorage<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryReadOnlySargassumVaultArray(
                in handle,
                bufferId,
                requiredLength,
                out NativeArray<T>.ReadOnly _);
        }

        private bool HasRequiredMicroFaunaGpuState()
        {
            return _boidsBufferA != null &&
                   _boidsBufferB != null &&
                   _grazingAnchorBuffer != null &&
                   _massiveThreatBuffer != null &&
                   _formationBeaconBuffer != null &&
                   _formationObstacleBuffer != null &&
                   _leviathanNodeBuffer != null &&
                   _latchStatsBuffer != null &&
                   _pbdCorrectionBuffer != null &&
                   _threatGridBuffer != null &&
                   _threatVoxelBuffer != null &&
                   _spatialGridCountBuffer != null &&
                   _spatialGridCellBuffer != null &&
                   _simulationFrameBuffer != null &&
                   _predatorAupFallbackBuffer != null &&
                   _boidSensoryThreatBufferA != null &&
                   _boidSensoryThreatBufferB != null &&
                   _computeKernelBindingsValid;
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
            _queuedSpawnBufferUploadCount = math.max(_queuedSpawnBufferUploadCount, math.max(0, uploadCount));
            _spawnBufferUploadRequested = true;
        }

        private void UploadSpawnDataToBoidBuffersVisualSync(int uploadCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
                return;

            try
            {
                int safeUploadCount = math.clamp(uploadCount, 0, math.min(boidCount, boidState.Length));
                if (safeUploadCount <= 0)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_boidsBufferA, boidState, safeUploadCount);
                GraphicsBufferUploadUtility.UploadNativeArray(_boidsBufferB, boidState, safeUploadCount);
                _debugConsumedBoidCount = 0;
                _feedingFrenzyWindowStartTime = -1f;
                _feedingFrenzyKillCount = 0;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }
        }

        private int ResolveTargetGrazingAnchorCount(int boidPopulation)
        {
            int safePopulation = math.clamp(boidPopulation, 0, boidCount);
            if (safePopulation <= 0 || grazingAnchorCount <= 0)
                return 0;

            return math.clamp(CeilDivPositive(safePopulation, TargetBoidsPerGrazingAnchor), 1, grazingAnchorCount);
        }

        private NativeArray<BoidData> ResolveBoidState()
        {
            return ResolveSargassumVaultArray(in _boidStateHandle, BufferID.SargassumBoidState, boidCount);
        }

        private void UploadGrazingAnchors()
        {
            _grazingAnchorUploadRequested = true;
        }

        private void UploadGrazingAnchorsVisualSync()
        {
            if (!TryAcquireSargassumWriteLock(
                    in _grazingAnchorsHandle,
                    BufferID.SargassumGrazingAnchors,
                    grazingAnchorCount,
                    out NativeArray<GrazingAnchorData> grazingAnchors))
                return;

            try
            {
                _activeGrazingAnchorCount = math.clamp(_activeGrazingAnchorCount, 0, math.min(grazingAnchorCount, grazingAnchors.Length));
                _debugGrazingAnchorCount = _activeGrazingAnchorCount;
                if (_activeGrazingAnchorCount <= 0 || _grazingAnchorBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_grazingAnchorBuffer, grazingAnchors, _activeGrazingAnchorCount);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _grazingAnchorsHandle);
            }
        }

        private void UploadFormationBeacons()
        {
            _formationBeaconUploadRequested = true;
        }

        private void UploadFormationBeaconsVisualSync()
        {
            if (!TryAcquireSargassumWriteLock(
                    in _formationBeaconsHandle,
                    BufferID.SargassumFormationBeacons,
                    formationBeaconCapacity,
                    out NativeArray<FormationBeaconData> formationBeacons))
                return;

            try
            {
                _debugFormationBeaconCount = math.clamp(_debugFormationBeaconCount, 0, math.min(formationBeaconCapacity, formationBeacons.Length));
                if (_debugFormationBeaconCount <= 0 || _formationBeaconBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_formationBeaconBuffer, formationBeacons, _debugFormationBeaconCount);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _formationBeaconsHandle);
            }
        }

        private void UploadFormationObstacles()
        {
            _formationObstacleUploadRequested = true;
        }

        private void UploadFormationObstaclesVisualSync()
        {
            if (!TryAcquireSargassumWriteLock(
                    in _formationObstaclesHandle,
                    BufferID.SargassumFormationObstacles,
                    formationObstacleCapacity,
                    out NativeArray<FormationObstacleData> formationObstacles))
                return;

            try
            {
                _debugFormationObstacleCount = math.clamp(_debugFormationObstacleCount, 0, math.min(formationObstacleCapacity, formationObstacles.Length));
                if (_debugFormationObstacleCount <= 0 || _formationObstacleBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_formationObstacleBuffer, formationObstacles, _debugFormationObstacleCount);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _formationObstaclesHandle);
            }
        }

        private void UploadMassiveThreats()
        {
            _massiveThreatUploadRequested = true;
        }

        private void UploadMassiveThreatsVisualSync()
        {
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
                return;

            try
            {
                _activeMassiveThreatCount = math.clamp(_activeMassiveThreatCount, 0, math.min(maxMassiveThreatCount, massiveThreats.Length));
                _debugMassiveThreatCount = _activeMassiveThreatCount;
                if (_activeMassiveThreatCount <= 0 || _massiveThreatBuffer == null)
                    return;

                RecalculateMassiveThreatCount(massiveThreats);
                if (_activeMassiveThreatCount <= 0)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_massiveThreatBuffer, massiveThreats, _activeMassiveThreatCount);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        private bool TryResolveEcosystemPopulationCount(out int ecosystemPopulationCount)
        {
            ecosystemPopulationCount = 0;
            IEcosystemDirectorService ecosystemDirector = _ecosystemDirector;
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized || !IsFiniteVector3(_fieldCenter))
            {
                ClearEcosystemBudgetState();
                return false;
            }

            if (!ecosystemDirector.TryGetSectorPopulation(_fieldCenter, out EcosystemSectorPopulationSample sample))
            {
                ClearEcosystemBudgetState();
                return false;
            }

            bool apexInSector = sample.ApexInSector != 0;
            _ecosystemApexInSector = apexInSector;
            int safePreyPopulation = math.max(0, sample.PreyPopulation);
            ecosystemPopulationCount = apexInSector ? safePreyPopulation >> 2 : safePreyPopulation;
            _ecosystemFitness = SaturateFinite01(sample.Fitness);
            float speedMultiplier = ClampFinite(sample.SpeedMultiplier, 0.25f, MaximumEcosystemSpeedMultiplier);
            _ecosystemSpeedMultiplier = ClampFinite(speedMultiplier * (apexInSector ? 1.25f : 1f), 0.25f, MaximumEcosystemSpeedMultiplier);
            _ecosystemCamouflageIndex = SaturateFinite01(sample.CamouflageIndex + (apexInSector ? 0.35f : 0f));
            _debugEcosystemFitness = _ecosystemFitness;
            _debugEcosystemCamouflageIndex = _ecosystemCamouflageIndex;
            _debugApexInSector = _ecosystemApexInSector;
            return true;
        }

        private void ClearEcosystemBudgetState()
        {
            _ecosystemFitness = 0f;
            _ecosystemSpeedMultiplier = 1f;
            _ecosystemCamouflageIndex = 0f;
            _ecosystemApexInSector = false;
            _debugApexInSector = false;
        }

        private float ResolvePopulationBudgetScale()
        {
            WorldProceduralScatterDirector scatterDirector = _proceduralScatterDirector;
            if (scatterDirector == null)
                return 1f;

            float spawnBudgetScale = ClampFinite(scatterDirector.CurrentSpawnBudgetScale, MinimumPopulationBudgetScale, 1f);
            float faunaActivationScale = ClampFinite(scatterDirector.CurrentFaunaActivationScale, 0f, 1.45f);
            return ClampFinite(spawnBudgetScale * faunaActivationScale, MinimumPopulationBudgetScale, 1f);
        }

        private void RefreshDispatchGroupCount()
        {
            _dispatchGroupCount = CeilDivPositive(_activeBoidCount, (int)_threadGroupSizeX);
            _clearStatsDispatchGroupCount = CeilDivPositive(1, (int)_clearStatsThreadGroupSizeX);
            _clearSpatialGridDispatchGroupCount = CeilDivPositive(SpatialGridMaxCellCount, (int)_clearSpatialGridThreadGroupSizeX);
            _buildSpatialGridDispatchGroupCount = CeilDivPositive(_activeBoidCount, (int)_buildSpatialGridThreadGroupSizeX);
            _clearPbdCorrectionsDispatchGroupCount = CeilDivPositive(_activeBoidCount, (int)_clearPbdCorrectionsThreadGroupSizeX);
            _pbdSolveDispatchGroupCount = CeilDivPositive(_activeBoidCount, (int)_pbdSolveThreadGroupSizeX);
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

        private void QueueThreatVoxelPayloadRefresh()
        {
            ResetThreatVoxelSnapshot();
            _threatVoxelPayloadRefreshRequested = true;
        }

        private void FlushThreatVoxelPayloadRefreshVisualSync()
        {
            if (!_threatVoxelPayloadRefreshRequested)
                return;

            _threatVoxelPayloadRefreshRequested = false;
            RefreshThreatVoxelPayloadVisualSync();
        }

        private void RefreshThreatVoxelPayloadVisualSync()
        {
            RefreshThreatGridPayloadVisualSync();
            ResetThreatVoxelSnapshot();
        }

        private void RefreshThreatGridPayloadVisualSync()
        {
            if (_mapMagicVegetationBridge == null ||
                !_mapMagicVegetationBridge.TryGetCompressedEcosystemThreatGridPayload(
                    out NativeArray<byte>.ReadOnly threatGrid,
                    out int gridResolution,
                    out Vector3 gridCenter,
                    out float cellSize))
            {
                ResetThreatGridSnapshot();
                return;
            }

            long cellCountLong = (long)gridResolution * gridResolution;
            if (threatGrid.Length <= 0 ||
                gridResolution <= 0 ||
                cellCountLong <= 0L ||
                cellCountLong > ThreatGridMaxCellCount ||
                threatGrid.Length < cellCountLong ||
                !IsFiniteVector3(gridCenter) ||
                !float.IsFinite(cellSize) ||
                cellSize <= 0f)
            {
                ResetThreatGridSnapshot();
                return;
            }

            int cellCount = (int)cellCountLong;
            if (_threatGridBuffer == null ||
                _threatGridBuffer.count < ThreatGridMaxCellCount ||
                _threatGridBuffer.stride != ThreatGridStride)
            {
                ResetThreatGridSnapshot();
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsSargassumVaultHandle(in _threatGridUploadHandle, BufferID.SargassumThreatGridUpload))
            {
                ResetThreatGridSnapshot();
                return;
            }

            bool uploadLocked = false;
            bool uploadReady = false;
            uint[] uploadSnapshot = _threatGridUploadSnapshot;
            if (uploadSnapshot == null || uploadSnapshot.Length < cellCount)
            {
                ResetThreatGridSnapshot();
                return;
            }

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                uploadSnapshot[cellIndex] = threatGrid[cellIndex];

            try
            {
                if (!vault.TryAcquireWriteLock(in _threatGridUploadHandle, SystemID.WorldSargassum, out NativeArray<uint> threatGridUpload))
                {
                    ResetThreatGridSnapshot();
                    return;
                }

                uploadLocked = true;
                if (!threatGridUpload.IsCreated ||
                    threatGridUpload.Length < cellCount)
                {
                    ResetThreatGridSnapshot();
                    return;
                }

                for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                    threatGridUpload[cellIndex] = uploadSnapshot[cellIndex];

                uploadReady = true;
            }
            finally
            {
                if (uploadLocked)
                    vault.ReleaseWriteLock(in _threatGridUploadHandle, SystemID.WorldSargassum);
            }

            if (!uploadReady)
                return;

            GraphicsBufferUploadUtility.UploadArray(_threatGridBuffer, uploadSnapshot, cellCount);
            _threatGridCellCount = cellCount;
            _threatGridResolution = gridResolution;
            _threatGridCenterWS = gridCenter;
            _threatGridCellSizeWS = math.max(cellSize, ThreatVoxelCellEpsilon);
            _threatGridDataValid = true;
        }

        private void EnsureThreatGridUploadSnapshotCold()
        {
            if (_threatGridUploadSnapshot != null && _threatGridUploadSnapshot.Length >= ThreatGridMaxCellCount)
                return;

            // COLD ALLOC: uint[ThreatGridMaxCellCount] - threat-grid GPU upload snapshot copied under DataVault lock and consumed after release - owner: SargassumMicroFaunaBoids
            _threatGridUploadSnapshot = new uint[ThreatGridMaxCellCount];
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

        private static bool EnsureGpuWriteBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }

            // COLD ALLOC: GraphicsBuffer[count] - GPU-written structured UAV buffer - owner: SargassumMicroFaunaBoids
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
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

        private static bool EnsureGpuWriteRawBuffer(ref GraphicsBuffer buffer, ref bool rawTargetValid, int count, int stride)
        {
            if (buffer != null && rawTargetValid && buffer.count == count && buffer.stride == stride)
                return false;

            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }

            // COLD ALLOC: Raw GraphicsBuffer[count] - GPU-written byte-addressed UAV buffer - owner: SargassumMicroFaunaBoids
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
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

            if (boidCompute == null || boidMaterial == null || boidMesh == null)
            {
                _hasSpawnData = false;
                return;
            }

            if (!HasRequiredMicroFaunaStorage())
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

            if (!TryResolveAupFromRuntimeOrigin(_fieldCenter, out _statisticalPopulationCenterAup))
                return false;
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
            if (!HasRequiredMicroFaunaStorage() ||
                !EnsureComputeKernelBindings() ||
                !HasRequiredMicroFaunaGpuState())
            {
                return false;
            }

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
            _lastSimulationHibernation01 = 1f;
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
            _parasiteLatchReadbackRepairRequested = false;

            _activeBoidCount = 0;
            _debugActiveBoidCount = 0;
            _dispatchGroupCount = 0;
            _debugDispatchGroups = 0;
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
            if (!TryResolveRuntimePosition(in _statisticalPopulationCenterAup, out Vector3 center))
            {
                ClearStatisticalPopulationPoint();
                return 0;
            }

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
            if (!TryResolveRuntimePosition(in _statisticalPopulationCenterAup, out Vector3 center))
            {
                ClearStatisticalPopulationPoint();
                return;
            }

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

            if (!TryResolveAupFromRuntimeOrigin(center, out AbsoluteUniversePosition centerAup))
            {
                ClearRegisteredMigrationPopulation();
                return 0;
            }

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
                if (TryResolveRuntimePosition(in _registeredMigrationPopulationCenterAup, out Vector3 center))
                {
                    MigrationDirector.RegisterStatisticalSwarmPopulation(
                        _registeredMigrationPopulationSpecies,
                        center,
                        0);
                }
            }

            _registeredMigrationPopulationCenterAup = default;
            _registeredMigrationPopulationAupCell = default;
            _registeredMigrationPopulationSpecies = 0;
            _registeredMigrationPopulationValid = false;
            _migrationPopulationCount = 0;
        }

        private void BuildStatisticalRematerializedSpawnSet(int rematerializedCount)
        {
            bool buildGrazingAnchors = false;
            Vector3 grazingCenter = Vector3.zero;
            float grazingRadiusMeters = 0f;
            int grazingRematerializedCount = 0;

            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
            {
                return;
            }

            try
            {
                if (!TryResolveRuntimePosition(in _statisticalPopulationCenterAup, out Vector3 center))
                {
                    ClearStatisticalPopulationPoint();
                    _hasSpawnData = false;
                    return;
                }

                float radius = math.max(PopulationDensityMinRadiusMeters, _statisticalPopulationPoint.RadiusMeters);
                _fieldCenter = center;
                _fieldExtents = new Vector3(radius, radius * 0.45f, radius);
                _renderBounds = new Bounds(_fieldCenter, _fieldExtents * 2f);
                _debugRenderBounds = _renderBounds;
                _densityWorldRect = Vector4.zero;

                int safeRematerializedCount = math.clamp(rematerializedCount, 0, math.min(boidCount, boidState.Length));
                for (int i = 0; i < safeRematerializedCount; i++)
                {
                    Vector3 offset = BuildSphericalFibonacciOffset(i, safeRematerializedCount, radius, _statisticalPopulationPoint.CenterCellId);
                    Vector3 spawnPosition = center + offset;
                    Vector3 velocity = BuildStatisticalTangentVelocity(offset, i);

                    boidState[i] = new BoidData
                    {
                        Position = spawnPosition,
                        Velocity = velocity,
                        Panic = 0f,
                        StateFlags = DefaultBoidStateFlags
                    };
                }

                grazingCenter = center;
                grazingRadiusMeters = radius;
                grazingRematerializedCount = safeRematerializedCount;
                buildGrazingAnchors = true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }

            if (buildGrazingAnchors)
                BuildStatisticalGrazingAnchors(grazingCenter, grazingRadiusMeters, grazingRematerializedCount);
        }

        private void BuildStatisticalGrazingAnchors(Vector3 center, float radius, int rematerializedCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _grazingAnchorsHandle,
                    BufferID.SargassumGrazingAnchors,
                    grazingAnchorCount,
                    out NativeArray<GrazingAnchorData> grazingAnchors))
            {
                _activeGrazingAnchorCount = 0;
                _debugGrazingAnchorCount = 0;
                return;
            }

            try
            {
                _activeGrazingAnchorCount = math.min(math.min(ResolveTargetGrazingAnchorCount(rematerializedCount), 8), grazingAnchors.Length);
                float anchorRadius = math.max(grazingRadius, radius * 0.2f);
                for (int i = 0; i < _activeGrazingAnchorCount; i++)
                {
                    Vector3 offset = BuildSphericalFibonacciOffset(i, _activeGrazingAnchorCount, math.max(1f, radius * 0.35f), _statisticalPopulationPoint.CenterCellId ^ 0x6C8E9CF5);
                    grazingAnchors[i] = new GrazingAnchorData
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
            finally
            {
                ReleaseSargassumWriteLock(in _grazingAnchorsHandle);
            }
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
            double3 absD = centerAup.ToAbsoluteDouble3();
            long cellX = (long)math.floor(absD.x / PopulationDensityCellSizeMeters);
            long cellY = (long)math.floor(absD.y / PopulationDensityCellSizeMeters);
            long cellZ = (long)math.floor(absD.z / PopulationDensityCellSizeMeters);
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridX));
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridY));
                hash = HashPopulationDensityComponent(hash, FoldLongToUInt(centerAup.GridZ));
                hash = HashPopulationDensityComponent(hash, (uint)(cellX & 0xFFFFFFFFL));
                hash = HashPopulationDensityComponent(hash, (uint)(cellY & 0xFFFFFFFFL));
                hash = HashPopulationDensityComponent(hash, (uint)(cellZ & 0xFFFFFFFFL));
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
            if (biolumManager == null || !RefreshPlayerRuntimePosition(out Vector3 playerPosition) || _deepBiolumZones == null || _deepBiolumZoneScores == null)
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
            return RefreshPlayerRuntimePosition(out Vector3 playerPosition) && playerPosition.y <= deepSeaWorldYThreshold;
        }

        private bool IsParasiteModeActive()
        {
            if (!RefreshPlayerRuntimePosition(out Vector3 playerPosition) || playerPosition.y > parasiteDroneWorldYThreshold)
                return false;

            if (!IsParasiteDepthGateSatisfied())
                return false;

            if (!TryResolveParasiteModeZones(out WorldZoneAnchor primaryZone, out WorldZoneAnchor secondaryZone))
                return false;

            return IsSyntheticAbyssZone(primaryZone) || IsSyntheticAbyssZone(secondaryZone);
        }

        private bool IsParasiteDepthGateSatisfied()
        {
            if (TryResolvePlayerDepthMeters(out float playerDepthMeters) &&
                playerDepthMeters >= ParasiteModeMinDepthMeters)
            {
                return true;
            }

            BiomeMatrixDirector matrixDirector = _biomeMatrixDirector;
            if (matrixDirector == null || !matrixDirector.isActiveAndEnabled)
            {
                matrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref matrixDirector);
                _biomeMatrixDirector = matrixDirector;
            }

            if (matrixDirector != null &&
                matrixDirector.isActiveAndEnabled &&
                math.isfinite(matrixDirector.CurrentDepthMeters))
            {
                return matrixDirector.CurrentDepthMeters >= ParasiteModeMinDepthMeters;
            }

            return false;
        }

        private bool TryResolveParasiteModeZones(out WorldZoneAnchor primaryZone, out WorldZoneAnchor secondaryZone)
        {
            WorldZoneDirector zoneDirector = _worldZoneDirector;
            if (zoneDirector == null || !zoneDirector.isActiveAndEnabled)
            {
                zoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref zoneDirector);
                _worldZoneDirector = zoneDirector;
            }

            if (zoneDirector == null || !zoneDirector.isActiveAndEnabled)
            {
                primaryZone = null;
                secondaryZone = null;
                return false;
            }

            primaryZone = zoneDirector.CurrentZone;
            secondaryZone = zoneDirector.SecondaryZone;
            return true;
        }

        private bool TryResolvePlayerDepthMeters(out float depthMeters)
        {
            if (RefreshPlayerRuntimeSnapshotCache(
                    out PlayerMovementRuntimeState movementState,
                    out PlayerLookState _) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depthMeters = math.max(0f, movementState.DepthMeters);
                return true;
            }

            if (_playerRuntimeContext != null)
            {
                depthMeters = 0f;
                return false;
            }

            HectonPlayerMovement movement = _playerMovement;
            if (movement != null && math.isfinite(movement.CurrentDepth))
            {
                depthMeters = math.max(0f, movement.CurrentDepth);
                return true;
            }

            depthMeters = 0f;
            return false;
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
            FormationBeaconData[] stagedBeacons = _formationBeaconStaging;
            if (stagedBeacons == null)
                return;

            int formationBeaconLimit = math.min(formationBeaconCapacity, stagedBeacons.Length);
            if (formationBeaconLimit <= 0 || !HasSargassumReadOnlyStorage(in _formationObstaclesHandle, BufferID.SargassumFormationObstacles, formationObstacleCapacity))
                return;

            if (!_deepModeActive)
                return;

            IBeaconNetworkService beaconNetwork = _beaconNetworkRuntime;
            if (beaconNetwork == null || _formationBeaconSnapshots == null)
                return;

            int snapshotCount = beaconNetwork.CopySnapshots(_formationBeaconSnapshots);
            snapshotCount = math.clamp(snapshotCount, 0, _formationBeaconSnapshots.Length);
            if (snapshotCount <= 0)
                return;

            if (!RefreshPlayerRuntimePosition(out Vector3 origin))
                return;

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return;

            IAbyssalFlowGpuReadModel fluidRuntime = _fluidEngine;
            double searchRadiusSq = (double)formationBeaconSearchRadius * formationBeaconSearchRadius;
            int formationCount = 0;
            for (int i = 0; i < snapshotCount && formationCount < formationBeaconLimit; i++)
            {
                BeaconNetworkSnapshot snapshot = _formationBeaconSnapshots[i];
                Vector3 beaconPosition = snapshot.Position;
                if (!TryResolveAupFromRuntimeOrigin(beaconPosition, out AbsoluteUniversePosition beaconAup))
                    continue;

                if (AbsoluteUniversePosition.DistanceSq(in beaconAup, in originAup) > searchRadiusSq)
                    continue;

                float beaconRadius = math.clamp(snapshot.LightRange * 2.2f, 4f, formationBeaconSearchRadius * 0.35f);
                Vector2 leaderFlowXZ = Vector2.zero;
                if (fluidRuntime != null &&
                    fluidRuntime.TrySampleModAbyssalFlow(beaconPosition, out float3 resolvedLeaderFlow))
                {
                    leaderFlowXZ = new Vector2(resolvedLeaderFlow.x, resolvedLeaderFlow.z);
                }

                stagedBeacons[formationCount] = new FormationBeaconData
                {
                    Position = beaconPosition,
                    Radius = beaconRadius,
                    Strength = 1f,
                    Phase = HashToFloat01((uint)i, 0u, 0x55A1F13Du),
                    Padding = leaderFlowXZ
                };
                formationCount++;
            }

            if (!PublishFormationBeacons(stagedBeacons, formationCount))
                return;

            UploadFormationBeacons();
            if (formationCount <= 0)
                return;

            RefreshStaticObstacleCache();
            HarvestFormationObstacles(origin);
        }

        private void RefreshStaticObstacleCache()
        {
            _staticObstacleCacheCount = 0;
            StaticObstacleData[] stagedObstacles = _staticObstacleCacheStaging;
            if (stagedObstacles == null)
                return;

            int staticObstacleCapacity = math.max(formationObstacleCapacity * 8, formationObstacleCapacity);
            int staticObstacleLimit = math.min(staticObstacleCapacity, stagedObstacles.Length);
            if (staticObstacleLimit <= 0 || _mapMagicVegetationBridge == null)
                return;

            if (!_mapMagicVegetationBridge.TryGetActiveUnderwaterNativePayload(
                    out NativeArray<Matrix4x4> matrices,
                    out NativeArray<HectonVegetationInstanceData> metadata,
                    out _,
                    out int count) ||
                !_mapMagicVegetationBridge.TryGetActiveUnderwaterSemanticPayload(out NativeArray<int>.ReadOnly semanticTypes, out _, out _))
            {
                return;
            }

            int safeCount = math.min(count, math.min(matrices.Length, math.min(metadata.Length, semanticTypes.Length)));
            int stagedCount = 0;
            for (int i = 0; i < safeCount && stagedCount < staticObstacleLimit; i++)
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

                stagedObstacles[stagedCount] = new StaticObstacleData(
                    new float3(matrix.m03, matrix.m13, matrix.m23),
                    new float3(extents.x, extents.y, extents.z),
                    radius);
                stagedCount++;
            }

            PublishStaticObstacleCache(stagedObstacles, stagedCount);
        }

        private bool PublishStaticObstacleCache(StaticObstacleData[] stagedObstacles, int stagedCount)
        {
            int staticObstacleCapacity = math.max(formationObstacleCapacity * 8, formationObstacleCapacity);
            if (!TryAcquireSargassumWriteLock(
                    in _staticObstacleCacheHandle,
                    BufferID.SargassumStaticObstacleCache,
                    staticObstacleCapacity,
                    out NativeArray<StaticObstacleData> staticObstacleCache))
                return false;

            try
            {
                int safeCount = math.clamp(stagedCount, 0, math.min(staticObstacleCache.Length, stagedObstacles.Length));
                for (int i = 0; i < safeCount; i++)
                {
                    staticObstacleCache[i] = stagedObstacles[i];
                }

                _staticObstacleCacheCount = safeCount;
                return true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _staticObstacleCacheHandle);
            }
        }

        private void HarvestFormationObstacles(Vector3 origin)
        {
            if (!TryReadOnlySargassumVaultArray(
                    in _staticObstacleCacheHandle,
                    BufferID.SargassumStaticObstacleCache,
                    math.max(formationObstacleCapacity * 8, formationObstacleCapacity),
                    out NativeArray<StaticObstacleData>.ReadOnly staticObstacleCache))
                return;

            FormationObstacleData[] stagedObstacles = _formationObstacleStaging;
            if (stagedObstacles == null)
                return;

            int formationObstacleLimit = math.min(formationObstacleCapacity, stagedObstacles.Length);
            if (formationObstacleLimit <= 0)
                return;

            int obstacleCount = 0;
            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return;

            int staticObstacleCount = math.min(_staticObstacleCacheCount, staticObstacleCache.Length);
            for (int i = 0; i < staticObstacleCount && obstacleCount < formationObstacleLimit; i++)
            {
                StaticObstacleData obstacle = staticObstacleCache[i];
                float radius = math.max(0.1f, obstacle.Radius);
                float maxDistance = formationObstacleSearchRadius + radius;
                Vector3 obstaclePosition = new Vector3(obstacle.Center.x, obstacle.Center.y, obstacle.Center.z);
                if (!TryResolveAupFromRuntimeOrigin(obstaclePosition, out AbsoluteUniversePosition obstacleAup))
                    continue;

                if (AbsoluteUniversePosition.DistanceSq(in obstacleAup, in originAup) > (double)maxDistance * maxDistance)
                    continue;

                stagedObstacles[obstacleCount] = new FormationObstacleData
                {
                    Position = obstaclePosition,
                    Radius = radius,
                    Weight = 1f,
                    Padding = Vector3.zero
                };
                obstacleCount++;
            }

            if (PublishFormationObstacles(stagedObstacles, obstacleCount))
            {
                UploadFormationObstacles();
            }
        }

        private bool PublishFormationBeacons(FormationBeaconData[] stagedBeacons, int stagedCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _formationBeaconsHandle,
                    BufferID.SargassumFormationBeacons,
                    formationBeaconCapacity,
                    out NativeArray<FormationBeaconData> formationBeacons))
                return false;

            try
            {
                int safeCount = math.clamp(stagedCount, 0, math.min(formationBeaconCapacity, math.min(formationBeacons.Length, stagedBeacons.Length)));
                for (int i = 0; i < safeCount; i++)
                {
                    formationBeacons[i] = stagedBeacons[i];
                }

                _debugFormationBeaconCount = safeCount;
                return true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _formationBeaconsHandle);
            }
        }

        private bool PublishFormationObstacles(FormationObstacleData[] stagedObstacles, int stagedCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _formationObstaclesHandle,
                    BufferID.SargassumFormationObstacles,
                    formationObstacleCapacity,
                    out NativeArray<FormationObstacleData> formationObstacles))
                return false;

            try
            {
                int safeCount = math.clamp(stagedCount, 0, math.min(formationObstacleCapacity, math.min(formationObstacles.Length, stagedObstacles.Length)));
                for (int i = 0; i < safeCount; i++)
                {
                    formationObstacles[i] = stagedObstacles[i];
                }

                _debugFormationObstacleCount = safeCount;
                return true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _formationObstaclesHandle);
            }
        }

        private void BuildLeviathanData()
        {
            _leviathanThreatLevel = 0f;
            bool hasPlayerPosition = RefreshPlayerRuntimePosition(out Vector3 playerPosition);
            _leviathanHotspotWS = hasPlayerPosition ? playerPosition : Vector3.zero;
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
            _debugLeviathanThreatLevel = 0f;
            _debugLeviathanHotspotWS = _leviathanHotspotWS;
            if (_mapMagicVegetationBridge == null || !hasPlayerPosition)
            {
                ClearLeviathanSnapshot();
                return;
            }

            if (!HasSargassumReadOnlyStorage(in _leviathanNodeFrontHandle, BufferID.SargassumLeviathanNodeFront, leviathanNodeCapacity) ||
                !HasSargassumReadOnlyStorage(in _leviathanNodeBackHandle, BufferID.SargassumLeviathanNodeBack, leviathanNodeCapacity))
            {
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

            if (_mapMagicVegetationBridge.TryGetLatestAbyssalPathPayload(out NativeArray<Vector3>.ReadOnly path, out int pathCount) &&
                pathCount > 1)
            {
                ScheduleLeviathanNodeBuild(path, pathCount);
            }

            _mapMagicVegetationBridge.TryScheduleAbyssalPath(playerPosition, hotspotPosition, out _);
            _debugLeviathanNodeCount = _leviathanPathNodeCount;
        }

        private void ScheduleLeviathanNodeBuild(NativeArray<Vector3>.ReadOnly path, int pathCount)
        {
            int safePathCount = math.min(math.min(pathCount, path.Length), leviathanNodeCapacity);
            if (safePathCount < 2)
                return;

            if (!TryAcquireSargassumWriteLock(
                    in _leviathanNodeBackHandle,
                    BufferID.SargassumLeviathanNodeBack,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData> leviathanNodeBack))
            {
                return;
            }

            int builtCount = 0;
            try
            {
                builtCount = BuildLeviathanNodesInline(
                    path,
                    safePathCount,
                    leviathanNodeBack,
                    math.max(0.5f, leviathanBodyRadius));
            }
            finally
            {
                ReleaseSargassumWriteLock(in _leviathanNodeBackHandle);
            }

            if (!WriteLeviathanNodeCount(builtCount))
                return;

            (_leviathanNodeFrontHandle, _leviathanNodeBackHandle) = (_leviathanNodeBackHandle, _leviathanNodeFrontHandle);
            _leviathanPathNodeCount = builtCount;
            _debugLeviathanNodeCount = builtCount;
            UploadActiveLeviathanSnapshot();
        }

        private static int BuildLeviathanNodesInline(
            NativeArray<Vector3>.ReadOnly sourcePath,
            int sourceCount,
            NativeArray<LeviathanNodeData> outputNodes,
            float bodyRadius)
        {
            int safePathCount = math.min(sourceCount, sourcePath.Length);
            if (!outputNodes.IsCreated || outputNodes.Length <= 0 || safePathCount < 2)
                return 0;

            float totalLength = 0f;
            for (int i = 1; i < safePathCount; i++)
                totalLength += ApproxLeviathanSegmentLength((float3)(sourcePath[i - 1]), (float3)(sourcePath[i]));

            if (totalLength <= 0.001f)
                return 0;

            int targetCount = math.min(outputNodes.Length, safePathCount);
            float distanceStep = totalLength / math.max(1, targetCount - 1);
            int pathCursor = 1;
            float traversed = 0f;
            float3 previousPoint = (float3)(sourcePath[0]);

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
                    float3 previousPathPoint = (float3)(sourcePath[pathCursor - 1]);
                    float3 currentPathPoint = (float3)(sourcePath[pathCursor]);
                    float segmentLength = ApproxLeviathanSegmentLength(previousPathPoint, currentPathPoint);
                    if (traversed + segmentLength >= targetDistance || pathCursor >= safePathCount - 1)
                    {
                        float segmentT = segmentLength > 0.0001f
                            ? math.saturate((targetDistance - traversed) / segmentLength)
                            : 0f;
                        previousPoint = math.lerp(previousPathPoint, currentPathPoint, segmentT);
                        break;
                    }

                    traversed += segmentLength;
                    pathCursor++;
                }

                if (pathCursor >= safePathCount)
                    previousPoint = (float3)(sourcePath[safePathCount - 1]);

                outputNodes[nodeIndex] = new LeviathanNodeData
                {
                    Position = previousPoint,
                    Distance01 = 0f,
                    Tangent = new float3(0f, 0f, 1f),
                    Radius = bodyRadius
                };
            }

            float cumulativeDistance = 0f;
            for (int nodeIndex = 0; nodeIndex < targetCount; nodeIndex++)
            {
                float3 nodePosition = outputNodes[nodeIndex].Position;
                if (nodeIndex > 0)
                    cumulativeDistance += ApproxLeviathanSegmentLength(outputNodes[nodeIndex - 1].Position, nodePosition);

                float3 tangent = nodeIndex < targetCount - 1
                    ? CheapNormalizeL1(outputNodes[nodeIndex + 1].Position - nodePosition, new float3(0f, 0f, 1f))
                    : CheapNormalizeL1(nodePosition - outputNodes[math.max(0, nodeIndex - 1)].Position, new float3(0f, 0f, 1f));
                float distance01 = totalLength > 0.0001f ? math.saturate(cumulativeDistance / totalLength) : 0f;
                float resolvedBodyRadius = math.lerp(bodyRadius, math.max(0.5f, bodyRadius * 0.18f), distance01);
                outputNodes[nodeIndex] = new LeviathanNodeData
                {
                    Position = nodePosition,
                    Distance01 = distance01,
                    Tangent = tangent,
                    Radius = resolvedBodyRadius
                };
            }

            return targetCount;
        }

        private static float ApproxLeviathanSegmentLength(float3 a, float3 b)
        {
            float3 delta = math.abs(b - a);
            float maxAxis = math.cmax(delta);
            float minAxis = math.cmin(delta);
            float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.5f + minAxis * 0.25f;
        }

        private bool WriteLeviathanNodeCount(int nodeCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _leviathanNodeCountHandle,
                    BufferID.SargassumLeviathanNodeCount,
                    1,
                    out NativeArray<int> leviathanNodeCount))
            {
                return false;
            }

            try
            {
                leviathanNodeCount[0] = math.clamp(nodeCount, 0, leviathanNodeCapacity);
                return true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _leviathanNodeCountHandle);
            }
        }

        private bool TrySampleLeviathanPath(float distance01, out Vector3 positionWS, out Vector3 tangentWS, out float radiusWS)
        {
            if (!TryReadOnlySargassumVaultArray(
                    in _leviathanNodeFrontHandle,
                    BufferID.SargassumLeviathanNodeFront,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData>.ReadOnly leviathanNodeFront))
            {
                positionWS = _fieldCenter;
                tangentWS = Vector3.forward;
                radiusWS = math.max(0.5f, leviathanBodyRadius);
                return false;
            }

            return TrySampleLeviathanPath(leviathanNodeFront, distance01, out positionWS, out tangentWS, out radiusWS);
        }

        private bool TrySampleLeviathanPath(
            NativeArray<LeviathanNodeData>.ReadOnly leviathanNodeFront,
            float distance01,
            out Vector3 positionWS,
            out Vector3 tangentWS,
            out float radiusWS)
        {
            positionWS = _fieldCenter;
            tangentWS = Vector3.forward;
            radiusWS = math.max(0.5f, leviathanBodyRadius);
            if (!leviathanNodeFront.IsCreated || _leviathanPathNodeCount < 2)
                return false;

            int safeCount = math.min(_leviathanPathNodeCount, leviathanNodeFront.Length);
            LeviathanNodeData previousNode = leviathanNodeFront[0];
            for (int i = 1; i < safeCount; i++)
            {
                LeviathanNodeData currentNode = leviathanNodeFront[i];
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

            LeviathanNodeData tailNode = leviathanNodeFront[safeCount - 1];
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
            if (!TryReadOnlySargassumVaultArray(
                    in _leviathanNodeFrontHandle,
                    BufferID.SargassumLeviathanNodeFront,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData>.ReadOnly leviathanNodeFront) ||
                !TrySampleLeviathanPath(leviathanNodeFront, 0f, out Vector3 splinePosition, out Vector3 splineTangent, out float bodyRadius))
            {
                return false;
            }

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
            if (surroundAttack > 0f && RefreshPlayerRuntimePosition(out Vector3 playerPosition))
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
            if (!TryReadOnlySargassumVaultArray(
                    in _leviathanNodeFrontHandle,
                    BufferID.SargassumLeviathanNodeFront,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData>.ReadOnly leviathanNodeFront) ||
                !TrySampleLeviathanPath(leviathanNodeFront, 0f, out Vector3 currentSplinePoint, out Vector3 currentSplineTangent, out _) ||
                _leviathanPathNodeCount < 2)
            {
                return false;
            }

            float sampleStep = 1f / math.max(1, _leviathanPathNodeCount - 1);
            float nextDistance01 = math.saturate(sampleStep);
            if (!TrySampleLeviathanPath(leviathanNodeFront, nextDistance01, out Vector3 nextSplinePoint, out Vector3 nextSplineTangent, out _))
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
            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
            {
                return;
            }

            try
            {
                if (!TryReadOnlySargassumVaultArray(
                        in _leviathanNodeFrontHandle,
                        BufferID.SargassumLeviathanNodeFront,
                        leviathanNodeCapacity,
                        out NativeArray<LeviathanNodeData>.ReadOnly leviathanNodeFront) ||
                    _leviathanPathNodeCount < 2)
                {
                    return;
                }

                Vector3 boundsMin = ToVector3(leviathanNodeFront[0].Position);
                Vector3 boundsMax = boundsMin;
                float radiusPadding = math.max(1f, leviathanBodyRadius * (1f + leviathanWaveAmplitude));
                int safeNodeCount = math.clamp(_leviathanPathNodeCount, 0, leviathanNodeFront.Length);
                for (int i = 0; i < safeNodeCount; i++)
                {
                    Vector3 nodePosition = ToVector3(leviathanNodeFront[i].Position);
                    Vector3 nodeExtents = new Vector3(radiusPadding, radiusPadding, radiusPadding);
                    boundsMin = MinVector3(boundsMin, nodePosition - nodeExtents);
                    boundsMax = MaxVector3(boundsMax, nodePosition + nodeExtents);
                }

                _fieldCenter = (boundsMin + boundsMax) * 0.5f;
                _fieldExtents = MaxVector3((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 2f, 2f));
                _renderBounds = new Bounds(_fieldCenter, MaxVector3(boundsMax - boundsMin, new Vector3(4f, 4f, 4f)));
                _debugRenderBounds = _renderBounds;

                int safeSpawnCount = math.clamp(spawnCount, 0, math.min(boidCount, boidState.Length));
                for (int i = 0; i < safeSpawnCount; i++)
                {
                    float bodyT = safeSpawnCount > 1 ? i / (float)(safeSpawnCount - 1) : 0f;
                    if (!TrySampleLeviathanPath(leviathanNodeFront, bodyT, out Vector3 centerlinePosition, out Vector3 tangentWS, out float bodyRadius))
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

                    boidState[i] = new BoidData
                    {
                        Position = spawnPosition,
                        Velocity = tangentWS * cruiseSpeed,
                        Panic = 0f,
                        StateFlags = DefaultBoidStateFlags
                    };
                }
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }
        }

        private void BuildDeepSpawnSet(int zoneCount, int spawnCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
                return;

            try
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

                int safeSpawnCount = math.clamp(spawnCount, 0, math.min(boidCount, boidState.Length));
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

                    boidState[i] = new BoidData
                    {
                        Position = spawnPosition,
                        Velocity = toCenter * cruiseSpeed,
                        Panic = 0f,
                        StateFlags = DefaultBoidStateFlags
                    };
                }
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }
        }

        private void BuildDeepGrazingAnchors(int zoneCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _grazingAnchorsHandle,
                    BufferID.SargassumGrazingAnchors,
                    grazingAnchorCount,
                    out NativeArray<GrazingAnchorData> grazingAnchors))
            {
                _activeGrazingAnchorCount = 0;
                _debugGrazingAnchorCount = 0;
                return;
            }

            try
            {
                _activeGrazingAnchorCount = 0;
                int targetAnchorCount = math.min(math.min(zoneCount, ResolveTargetGrazingAnchorCount(ResolveActiveBoidUploadCount())), grazingAnchors.Length);
                for (int i = 0; i < zoneCount && _activeGrazingAnchorCount < targetAnchorCount; i++)
                {
                    HectonBiolumZone zone = _deepBiolumZones[i];
                    if (zone == null)
                        continue;

                    grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
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
            finally
            {
                ReleaseSargassumWriteLock(in _grazingAnchorsHandle);
            }
        }

        private void BuildSpawnSet(Vector4 densityWorldRect, Vector3 driftOffset, int spawnCount)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
                return;

            try
            {
                double sizeXd = 1d / math.max((double)densityWorldRect.z, 0.0001d);
                double sizeZd = 1d / math.max((double)densityWorldRect.w, 0.0001d);
                double minXd = (double)densityWorldRect.x;
                double minZd = (double)densityWorldRect.y;
                double minYd = (double)waterLevel - (double)maxDepthBelowSurface;
                double maxYd = (double)waterLevel - (double)minDepthBelowSurface;
                double3 driftOffsetD = new double3(driftOffset.x, driftOffset.y, driftOffset.z);
                double3 fallbackCenterD = new double3(minXd + sizeXd * 0.5d + driftOffsetD.x, (minYd + maxYd) * 0.5d, minZd + sizeZd * 0.5d + driftOffsetD.z);

                _fieldCenter = new Vector3((float)fallbackCenterD.x, (float)fallbackCenterD.y, (float)fallbackCenterD.z);
                _fieldExtents = new Vector3((float)(sizeXd * 0.5d), math.max(1f, maxDepthBelowSurface), (float)(sizeZd * 0.5d));
                _renderBounds = new Bounds(_fieldCenter, new Vector3((float)sizeXd, math.max(2f, maxDepthBelowSurface + 2f), (float)sizeZd));
                _debugRenderBounds = _renderBounds;

                int safeSpawnCount = math.clamp(spawnCount, 0, math.min(boidCount, boidState.Length));
                for (int i = 0; i < safeSpawnCount; i++)
                {
                    double3 spawnPositionD = fallbackCenterD;
                    SargassumGlobalDragManager.SargassumFieldSample fieldSample = default;
                    bool found = false;

                    for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                    {
                        float u = HashToFloat01((uint)i, (uint)attempt, 0xA2F98A1Du);
                        float v = HashToFloat01((uint)i, (uint)attempt, 0x3C6EF372u);
                        float w = HashToFloat01((uint)i, (uint)attempt, 0x1BF5C7D5u);

                        spawnPositionD.x = minXd + (double)u * sizeXd + driftOffsetD.x;
                        spawnPositionD.y = math.lerp(minYd, maxYd, (double)w);
                        spawnPositionD.z = minZd + (double)v * sizeZd + driftOffsetD.z;

                        Vector3 samplePos = new Vector3((float)spawnPositionD.x, (float)spawnPositionD.y, (float)spawnPositionD.z);
                        if (!dragManager.SampleDetailedInfluence(samplePos, 0.45f, cruiseSpeed, out fieldSample))
                            continue;

                        if (fieldSample.Density01 < densityThreshold || fieldSample.Window01 > windowThreshold)
                            continue;

                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        spawnPositionD = fallbackCenterD;
                    }

                    Vector3 velocity = BuildInitialVelocity(i);
                    boidState[i] = new BoidData
                    {
                        Position = new Vector3((float)spawnPositionD.x, (float)spawnPositionD.y, (float)spawnPositionD.z),
                        Velocity = velocity,
                        Panic = 0f,
                        StateFlags = DefaultBoidStateFlags
                    };
                }
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }
        }

        private void BuildGrazingAnchors(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _grazingAnchorsHandle,
                    BufferID.SargassumGrazingAnchors,
                    grazingAnchorCount,
                    out NativeArray<GrazingAnchorData> grazingAnchors))
            {
                _activeGrazingAnchorCount = 0;
                _debugGrazingAnchorCount = 0;
                return;
            }

            try
            {
                int targetAnchorCount = ResolveTargetGrazingAnchorCount(ResolveActiveBoidUploadCount());
                if (targetAnchorCount <= 0)
                {
                    _activeGrazingAnchorCount = 0;
                    _debugGrazingAnchorCount = 0;
                    return;
                }

            targetAnchorCount = math.min(targetAnchorCount, grazingAnchors.Length);
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

                grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
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
            finally
            {
                ReleaseSargassumWriteLock(in _grazingAnchorsHandle);
            }
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
                _predatorAupFallbackBuffer == null ||
                _boidSensoryThreatBufferA == null ||
                _boidSensoryThreatBufferB == null ||
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
                boidCompute.SetBuffer(_kernelIndex, _PredatorAUPBufferId, _boidSensoryThreatBufferA);
                boidCompute.SetBuffer(_kernelIndex, _EncounterPredatorAUPBufferId, _predatorAupFallbackBuffer);
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
            catch (ObjectDisposedException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (InvalidOperationException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (ArgumentException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (MissingReferenceException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (UnityException)
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

            _fallbackAbyssalFlowTexture = neutralAbyssalFlowTexture;
        }

        private static int ResolvePredatorAupThreatLoopCap(int predatorAupCount, float hibernation01)
        {
            int safeCount = math.clamp(predatorAupCount, 0, PredatorAupBufferCapacity);
            if (safeCount <= 0)
                return 0;

            float quality01 = 1f - math.saturate(hibernation01);
            int scaledCount = (int)math.ceil(math.lerp(PredatorAupLowTierThreatLoopCap, safeCount, quality01));
            return math.clamp(scaledCount, 0, safeCount);
        }

        private static int ResolveBoidSensoryThreatFlags(float hibernation01)
        {
            return math.saturate(hibernation01) < 0.85f
                ? unchecked((int)SensoryThreatFlagFlashlightCapsule)
                : 0;
        }

        private int UpdateBoidSensoryThreats(
            float simulationDt,
            Vector3 playerPosition,
            Vector3 playerForward,
            Vector3 submarineThreatPosition,
            float submarineThreatRadius,
            SimulationLodTier simulationLodTier,
            float hibernation01)
        {
            GraphicsBuffer sensoryThreatWriteBuffer = ResolveBoidSensoryThreatWriteBuffer();
            if (sensoryThreatWriteBuffer == null)
            {
                _activeBoidSensoryThreatCount = 0;
                return 0;
            }

            if (!TryAcquireSargassumWriteLock(
                    in _boidSensoryThreatsHandle,
                    BufferID.SargassumBoidSensoryThreats,
                    PredatorAupBufferCapacity,
                    out NativeArray<float4> boidSensoryThreats))
            {
                _activeBoidSensoryThreatCount = 0;
                return 0;
            }

            int resultThreatCount = 0;
            bool recordBlackBox = false;
            float4 submarineThreatSnapshot = float4.zero;
            float4 flashlightThreatSnapshot = float4.zero;
            float4 pingThreatASnapshot = float4.zero;
            float4 pingThreatBSnapshot = float4.zero;
            float4 pingThreatCSnapshot = float4.zero;
            uint sensoryAnomalyHash = 0u;
            int sensoryThreatFlags = ResolveBoidSensoryThreatFlags(hibernation01);

            try
            {
                ClearBoidSensoryStaticThreatSlots(boidSensoryThreats);
                DecayBoidSensoryAcousticPingThreats(boidSensoryThreats, simulationDt);
                Vector3 playerAupPosition = RefreshPlayerAupRuntimePosition(playerPosition);
                WriteBoidSensoryThreatSlot(
                    boidSensoryThreats,
                    SensoryThreatSlotSubmarine,
                    submarineThreatPosition,
                    math.max(submarineThreatRadius, SensorySubmarineThreatRadiusMeters));
                ConsumeSubmarineLightSignals(playerAupPosition, playerForward);
                UpdateFlashlightSensoryThreat(boidSensoryThreats, simulationDt, playerAupPosition, playerForward, hibernation01);
                ConsumeBoidSensoryAcousticPingSignals(boidSensoryThreats);
                sensoryAnomalyHash = SanitizeBoidSensoryThreatSlots(boidSensoryThreats);

                int activeThreatCount = ResolveActiveBoidSensoryThreatCount(boidSensoryThreats);
                _activeBoidSensoryThreatCount = activeThreatCount;
                resultThreatCount = activeThreatCount;
                uint uploadHash = HashBoidSensoryThreatUpload(boidSensoryThreats);
                if (MarkBoidSensoryThreatUploadDirty(uploadHash))
                {
                    GraphicsBufferUploadUtility.UploadNativeArray(
                        sensoryThreatWriteBuffer,
                        boidSensoryThreats,
                        PredatorAupBufferCapacity);
                }

                submarineThreatSnapshot = ReadBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatSlotSubmarine);
                flashlightThreatSnapshot = ReadBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatSlotFlashlight);
                pingThreatASnapshot = ReadBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatFirstPingSlot);
                pingThreatBSnapshot = ReadBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatFirstPingSlot + 1);
                pingThreatCSnapshot = ReadBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatLastPingSlot);
                recordBlackBox = true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidSensoryThreatsHandle);
            }

            if (recordBlackBox)
            {
                RecordBoidSensoryBlackBox(
                    submarineThreatSnapshot,
                    flashlightThreatSnapshot,
                    pingThreatASnapshot,
                    pingThreatBSnapshot,
                    pingThreatCSnapshot,
                    resultThreatCount,
                    simulationLodTier,
                    sensoryThreatFlags,
                    sensoryAnomalyHash);
            }

            return resultThreatCount;
        }

        private void ClearBoidSensoryStaticThreatSlots(NativeArray<float4> boidSensoryThreats)
        {
            boidSensoryThreats[SensoryThreatSlotSubmarine] = float4.zero;
            boidSensoryThreats[SensoryThreatSlotFlashlight] = float4.zero;
            for (int i = SensoryThreatReservedSlots; i < PredatorAupBufferCapacity; i++)
                boidSensoryThreats[i] = float4.zero;
        }

        private Vector3 RefreshPlayerAupRuntimePosition(Vector3 fallbackPosition)
        {
            if (!RefreshPlayerRuntimeSnapshotCache(
                    out PlayerMovementRuntimeState movementState,
                    out PlayerLookState _))
            {
                return fallbackPosition;
            }

            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
                return fallbackPosition;

            return TryResolveRuntimePosition(in movementState.PredictedAup, out Vector3 runtimePosition)
                ? runtimePosition
                : fallbackPosition;
        }

        private void ConsumeSubmarineLightSignals(Vector3 fallbackOriginWS, Vector3 fallbackForwardWS)
        {
            ReadOnlySpan<SubmarineLightsChangedSignal> lightSignals = SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot();
            int signalStart = math.max(0, lightSignals.Length - SensorySubmarineLightSignalConsumeLimit);
            float bestSignalScore = -1f;
            Vector3 bestOriginWS = fallbackOriginWS;
            Vector3 bestForwardWS = fallbackForwardWS;
            float bestRangeMeters = 0f;
            float bestIntensity01 = 0f;
            bool shrinkRequested = false;

            for (int i = signalStart; i < lightSignals.Length; i++)
            {
                SubmarineLightsChangedSignal signal = lightSignals[i];
                bool powered = (signal.Flags & SubmarineLightsChangedSignalFlags.Powered) != 0 &&
                               (signal.Flags & SubmarineLightsChangedSignalFlags.BrownoutSuppressed) == 0;
                if (signal.Operation == SubmarineLightsChangedSignalOperations.Remove ||
                    signal.Operation == SubmarineLightsChangedSignalOperations.ClearSource ||
                    !powered)
                {
                    shrinkRequested = true;
                    continue;
                }

                float intensity01 = SaturateFinite01(signal.Intensity);
                if (intensity01 <= 0.001f)
                    continue;

                float rangeMeters = math.clamp(
                    math.isfinite(signal.RangeMeters) ? signal.RangeMeters : SensoryFlashlightDefaultRangeMeters,
                    SensoryThreatMinRadiusMeters,
                    SensoryAcousticPingMaxRadiusMeters);
                if (!TryResolveRuntimePosition(in signal.PositionAup, out Vector3 runtimePosition))
                    continue;

                float3 signalForward = signal.Forward;
                Vector3 forwardWS = math.lengthsq(signalForward) > 0.0001f && math.all(math.isfinite(signalForward))
                    ? FastNormalizeVector3(ToVector3(signalForward), fallbackForwardWS)
                    : fallbackForwardWS;
                float signalScore = rangeMeters * intensity01;
                if (signalScore <= bestSignalScore)
                    continue;

                bestSignalScore = signalScore;
                bestOriginWS = runtimePosition;
                bestForwardWS = forwardWS;
                bestRangeMeters = rangeMeters;
                bestIntensity01 = intensity01;
            }

            if (bestSignalScore > 0f)
            {
                _boidFlashlightThreatOriginWS = bestOriginWS;
                _boidFlashlightThreatForwardWS = FastNormalizeVector3(bestForwardWS, fallbackForwardWS);
                _boidFlashlightThreatRangeWS = bestRangeMeters;
                _boidFlashlightThreatIntensity01 = bestIntensity01;
                return;
            }

            if (shrinkRequested)
                _boidFlashlightThreatIntensity01 = 0f;
        }

        private void UpdateFlashlightSensoryThreat(
            NativeArray<float4> boidSensoryThreats,
            float simulationDt,
            Vector3 playerPosition,
            Vector3 playerForward,
            float hibernation01)
        {
            float signalLightIntensity01 = SaturateFinite01(_boidFlashlightThreatIntensity01);
            bool hasSignalLight = signalLightIntensity01 > 0.001f;
            float playerLightIntensity01 = _flashlightOn ? math.max(0.35f, ResolveHeadlightPanic01()) : 0f;
            float effectiveIntensity01 = math.max(signalLightIntensity01, playerLightIntensity01);
            if (effectiveIntensity01 <= 0.001f)
            {
                _boidFlashlightThreatTargetRadiusWS = 0f;
            }
            else
            {
                float rangeMeters = hasSignalLight
                    ? ClampFinite(_boidFlashlightThreatRangeWS, SensoryThreatMinRadiusMeters, SensoryAcousticPingMaxRadiusMeters)
                    : SensoryFlashlightDefaultRangeMeters;
                float targetRadius = math.max(
                    SensoryThreatMinRadiusMeters,
                    rangeMeters * SensoryFlashlightRadiusScale * effectiveIntensity01);
                _boidFlashlightThreatTargetRadiusWS = targetRadius;
            }

            float maxDelta = (_boidFlashlightThreatTargetRadiusWS > _boidFlashlightThreatRadiusWS
                    ? SensoryFlashlightGrowMetersPerSecond
                    : SensoryFlashlightShrinkMetersPerSecond) *
                math.max(0f, simulationDt);
            _boidFlashlightThreatRadiusWS = MoveTowardsFinite(
                _boidFlashlightThreatRadiusWS,
                _boidFlashlightThreatTargetRadiusWS,
                maxDelta);

            if (_boidFlashlightThreatRadiusWS <= SensoryThreatMinRadiusMeters &&
                _boidFlashlightThreatTargetRadiusWS <= SensoryThreatMinRadiusMeters)
            {
                ClearBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatSlotFlashlight);
                return;
            }

            Vector3 originWS = hasSignalLight ? _boidFlashlightThreatOriginWS : playerPosition;
            Vector3 forwardWS = hasSignalLight ? _boidFlashlightThreatForwardWS : playerForward;
            if (!IsFiniteVector3(originWS))
                originWS = playerPosition;
            forwardWS = FastNormalizeVector3(forwardWS, playerForward);
            float range = hasSignalLight
                ? ClampFinite(_boidFlashlightThreatRangeWS, SensoryThreatMinRadiusMeters, SensoryAcousticPingMaxRadiusMeters)
                : SensoryFlashlightDefaultRangeMeters;
            float endpointScale = math.lerp(1f, SensoryFlashlightEndpointScale, math.saturate(hibernation01));
            Vector3 endpointWS = originWS + forwardWS * math.max(2f, range * endpointScale);
            WriteBoidSensoryThreatSlot(boidSensoryThreats, SensoryThreatSlotFlashlight, endpointWS, _boidFlashlightThreatRadiusWS);
        }

        private void DecayBoidSensoryAcousticPingThreats(NativeArray<float4> boidSensoryThreats, float simulationDt)
        {
            float decay = math.max(0f, simulationDt) * SensoryAcousticPingDecayMetersPerSecond;
            for (int slot = SensoryThreatFirstPingSlot; slot <= SensoryThreatLastPingSlot; slot++)
            {
                float4 threat = boidSensoryThreats[slot];
                if (threat.w <= 0f)
                    continue;

                threat.w = math.max(0f, threat.w - decay);
                boidSensoryThreats[slot] = threat.w >= SensoryThreatMinRadiusMeters ? threat : float4.zero;
            }
        }

        private void ConsumeBoidSensoryAcousticPingSignals(NativeArray<float4> boidSensoryThreats)
        {
            ReadOnlySpan<AcousticPingSignal> pingSignals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int signalStart = math.max(0, pingSignals.Length - SwarmAcousticSignalConsumeLimit);
            for (int i = signalStart; i < pingSignals.Length; i++)
            {
                AcousticPingSignal signal = pingSignals[i];
                float intensity01 = SaturateFinite01(signal.Intensity01);
                if (intensity01 <= 0.001f)
                    continue;

                if (!TryResolveRuntimePosition(in signal.PositionAup, out Vector3 runtimePosition))
                    continue;

                float radius = math.clamp(
                    math.isfinite(signal.RadiusMeters) ? signal.RadiusMeters : 0f,
                    SensoryAcousticPingMinRadiusMeters,
                    SensoryAcousticPingMaxRadiusMeters);
                radius *= math.lerp(0.35f, 1f, intensity01);
                uint pingSlotCount = (uint)(SensoryThreatLastPingSlot - SensoryThreatFirstPingSlot + 1);
                int slot = SensoryThreatFirstPingSlot + (int)(_boidSensoryPingWriteCursor % pingSlotCount);
                _boidSensoryPingWriteCursor++;
                WriteBoidSensoryThreatSlot(boidSensoryThreats, slot, runtimePosition, radius);
            }
        }

        private int ResolveActiveBoidSensoryThreatCount(NativeArray<float4> boidSensoryThreats)
        {
            int lastActiveSlot = -1;
            for (int i = 0; i < PredatorAupBufferCapacity; i++)
            {
                float4 threat = boidSensoryThreats[i];
                if (threat.w >= SensoryThreatMinRadiusMeters && math.all(math.isfinite(threat)))
                    lastActiveSlot = i;
            }

            return math.max(0, lastActiveSlot + 1);
        }

        private static uint SanitizeBoidSensoryThreatSlots(NativeArray<float4> boidSensoryThreats)
        {
            if (!boidSensoryThreats.IsCreated)
                return 0u;

            uint anomalyHash = 0u;
            int slotCount = math.min(boidSensoryThreats.Length, PredatorAupBufferCapacity);
            for (int slot = 0; slot < slotCount; slot++)
            {
                float4 threat = boidSensoryThreats[slot];
                if (!math.all(math.isfinite(threat)))
                {
                    anomalyHash = math.hash(new uint4(
                        anomalyHash,
                        HashThreatFloat4(threat),
                        unchecked((uint)slot),
                        BoidSensoryBlackBoxAnomalyNonFinite));
                    boidSensoryThreats[slot] = float4.zero;
                    continue;
                }

                if (threat.w > 0f && threat.w < SensoryThreatMinRadiusMeters)
                {
                    threat.w = SensoryThreatMinRadiusMeters;
                    boidSensoryThreats[slot] = threat;
                    continue;
                }

                if (threat.w <= 0f)
                    boidSensoryThreats[slot] = float4.zero;
            }

            return anomalyHash;
        }

        private bool WriteBoidSensoryThreatSlot(NativeArray<float4> boidSensoryThreats, int slot, Vector3 runtimePosition, float radius)
        {
            if ((uint)slot >= (uint)PredatorAupBufferCapacity)
                return false;

            if (!IsFiniteVector3(runtimePosition) || !float.IsFinite(radius) || radius <= 0f)
            {
                boidSensoryThreats[slot] = float4.zero;
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition threatAup))
            {
                boidSensoryThreats[slot] = float4.zero;
                return false;
            }

            if (!TryResolveRuntimePosition(in threatAup, out Vector3 shiftedRuntime))
            {
                boidSensoryThreats[slot] = float4.zero;
                return false;
            }

            float safeRadius = ClampFinite(radius, SensoryThreatMinRadiusMeters, SensoryAcousticPingMaxRadiusMeters);
            boidSensoryThreats[slot] = new float4(
                shiftedRuntime.x,
                shiftedRuntime.y,
                shiftedRuntime.z,
                safeRadius);
            return true;
        }

        private void ClearBoidSensoryThreatSlot(NativeArray<float4> boidSensoryThreats, int slot)
        {
            if ((uint)slot < (uint)PredatorAupBufferCapacity)
                boidSensoryThreats[slot] = float4.zero;
        }

        private static float MoveTowardsFinite(float current, float target, float maxDelta)
        {
            current = float.IsFinite(current) ? current : 0f;
            target = float.IsFinite(target) ? target : 0f;
            maxDelta = math.max(0f, float.IsFinite(maxDelta) ? maxDelta : 0f);
            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;

            return current + math.sign(delta) * maxDelta;
        }

        private bool EnsurePredatorAupFallbackBuffer()
        {
            // COLD ALLOC: GraphicsBuffer[16 float4] - zero predator AUP fallback binding until EncounterDirector publishes active threats.
            bool changed = EnsureBuffer(ref _predatorAupFallbackBuffer, PredatorAupBufferCapacity, PredatorAupStride);
            if (!changed || _predatorAupFallbackBuffer == null)
                return changed;

            var mapped = _predatorAupFallbackBuffer.LockBufferForWrite<float4>(0, PredatorAupBufferCapacity);
            try
            {
                for (int i = 0; i < PredatorAupBufferCapacity; i++)
                    mapped[i] = float4.zero;
            }
            finally
            {
                _predatorAupFallbackBuffer.UnlockBufferAfterWrite<float4>(PredatorAupBufferCapacity);
            }
            return true;
        }

        private bool BindSimulationUniforms(
            float simulationDt,
            Vector3 driftOffset,
            Vector3 driftDelta,
            float hibernation01,
            SimulationLodTier simulationLodTier,
            bool shouldRender,
            bool shouldCollectLatchStats)
        {
            if (_simulationFrameBuffer == null)
                return false;

            ResolveSimulationBucketUniforms(out int simulationBucketIndex, out int simulationBucketMask);
            GraphicsBuffer readBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _boidsBufferB : _boidsBufferA;

            RefreshPlayerGpuFrame(
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
            Texture activeCutMaskTexture = cutMaskActive && cutMaskTexture != null ? (Texture)cutMaskTexture : Texture2D.blackTexture;
            Vector3 abyssalFlowWeatherCurrent = Vector3.zero;
            IAbyssalFlowGpuReadModel fluidRuntime = _fluidEngine;
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
                    out Vector4 publishedAbyssalFlowSpacing) &&
                publishedAbyssalFlowTexture != null)
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
            ISubmarineRuntimeContext submarine = _submarineRuntime;
            Rigidbody submarineHull = submarine != null ? submarine.HullRigidbody : null;
            Vector3 submarineThreatPosition = playerPosition;
            float submarineThreatRadius = math.max(SensorySubmarineThreatRadiusMeters, panicPlayerRadius * panicPlayerRadiusScale);
            if (submarineHull != null)
            {
                Vector3 submarineCenter = submarineHull.worldCenterOfMass;
                if (IsFiniteVector3(submarineCenter))
                    submarineThreatPosition = submarineCenter;
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

            GraphicsBuffer predatorAupBuffer = null;
            int predatorAupCount = 0;
            IEncounterDirectorService encounterDirector = _encounterDirector;
            if (encounterDirector != null &&
                encounterDirector.IsInitialized &&
                encounterDirector.TryGetPredatorAupGpuBuffer(out GraphicsBuffer publishedPredatorAupBuffer, out int publishedPredatorAupCount))
            {
                predatorAupBuffer = publishedPredatorAupBuffer;
                predatorAupCount = math.clamp(publishedPredatorAupCount, 0, PredatorAupBufferCapacity);
            }
            int predatorAupThreatLoopCap = ResolvePredatorAupThreatLoopCap(predatorAupCount, hibernation01);

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            UpdateFragmentationState(playerPosition, playerVelocity, playerForward, playerSpeed, absoluteSimulationTime);
            UpdateSonarScatterState(simulationDt, absoluteSimulationTime);
            float fragmentation01 = ResolveFragmentationStrength01(absoluteSimulationTime);
            float fragmentationHalfDistance = ClampFinite(_fragmentationHalfDistanceWS, 1f, MassiveThreatMaxRadiusMeters * 3f);
            bool absoluteSimulationTimeFinite = float.IsFinite(absoluteSimulationTime);
            float sonarScatterStrength01 = absoluteSimulationTimeFinite && absoluteSimulationTime < _sonarScatterExpireTime
                ? SaturateFinite01(_sonarScatterStrength01)
                : 0f;
            float acousticPanicStrength01 = absoluteSimulationTimeFinite && absoluteSimulationTime < _acousticPanicExpireTime
                ? SaturateFinite01(_acousticPanicStrength01)
                : 0f;
            if (acousticPanicStrength01 <= 0f)
            {
                _acousticPanicRadiusWS = 0f;
                _acousticPanicStrength01 = 0f;
            }
            float acousticPanicTimeRemaining = absoluteSimulationTimeFinite && float.IsFinite(_acousticPanicExpireTime)
                ? ClampMinFinite(_acousticPanicExpireTime - absoluteSimulationTime, 0f)
                : 0f;
            Vector3 fragmentationCenterA = IsFiniteVector3(_fragmentationCenterAWS) && fragmentation01 > 0f
                ? _fragmentationCenterAWS
                : Vector3.zero;
            Vector3 fragmentationCenterB = IsFiniteVector3(_fragmentationCenterBWS) && fragmentation01 > 0f
                ? _fragmentationCenterBWS
                : Vector3.zero;
            if (fragmentation01 <= 0f)
                fragmentationHalfDistance = 1f;

            Vector3 sonarScatterOrigin = IsFiniteVector3(_sonarScatterOriginWS) && sonarScatterStrength01 > 0f
                ? _sonarScatterOriginWS
                : Vector3.zero;
            if (sonarScatterStrength01 <= 0f)
                _sonarScatterWaveFrontWS = 0f;

            Vector3 acousticPanicOrigin = IsFiniteVector3(_acousticPanicOriginWS) && acousticPanicStrength01 > 0f
                ? _acousticPanicOriginWS
                : Vector3.zero;
            int boidSensoryThreatCount = UpdateBoidSensoryThreats(
                simulationDt,
                playerPosition,
                playerForward,
                submarineThreatPosition,
                submarineThreatRadius,
                simulationLodTier,
                hibernation01);
            int boidSensoryThreatFlags = ResolveBoidSensoryThreatFlags(hibernation01);

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
                (float)_spatialGridOriginWSD.x,
                (float)_spatialGridOriginWSD.y,
                (float)_spatialGridOriginWSD.z,
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
                boidSensoryThreatCount,
                boidSensoryThreatFlags);
            Vector3 threatGridCenterWS = IsFiniteVector3(_threatGridCenterWS) ? _threatGridCenterWS : Vector3.zero;
            float threatGridCellSizeWS = ClampFinite(_threatGridCellSizeWS, ThreatVoxelCellEpsilon, MassiveThreatMaxRadiusMeters);
            Vector3 threatVoxelOriginWS = IsFiniteVector3(_threatVoxelOriginWS) ? _threatVoxelOriginWS : Vector3.zero;
            frameConstants.ThreatGridCenter = new float4(
                threatGridCenterWS.x,
                threatGridCenterWS.y,
                threatGridCenterWS.z,
                threatGridCellSizeWS);
            frameConstants.ThreatVoxelMeta = new int4(
                _threatVoxelDimensions.x,
                _threatVoxelDimensions.y,
                _threatVoxelDimensions.z,
                _threatVoxelSolidThreshold);
            float ecosystemSpeedScale = ClampFinite(_ecosystemSpeedMultiplier, 0.25f, MaximumEcosystemSpeedMultiplier);
            float ecosystemCamouflageScale = math.lerp(1f, ecosystemCamouflageWeight, SaturateFinite01(_ecosystemCamouflageIndex));
            float ecosystemFitnessScale = math.lerp(1f, 1.15f, SaturateFinite01(_ecosystemFitness));
            float safeVoxelLookAheadDistance = ClampFinite(voxelAvoidanceLookAheadDistance, 0f, 12f);
            frameConstants.ThreatVoxelOrigin = new float4(
                threatVoxelOriginWS.x,
                threatVoxelOriginWS.y,
                threatVoxelOriginWS.z,
                safeVoxelLookAheadDistance * math.lerp(1f, ecosystemSpeedScale, 0.5f));
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
                ClampFinite(fragmentationWeight, 0f, 12f) * ecosystemFitnessScale,
                sonarScatterStrength01,
                ClampFinite(activeSonarWaveBandWidth, 0.25f, 16f),
                ClampFinite(activeSonarScatterImpulse, 0f, 64f) + ClampFinite(activeSonarScatterWeight, 0f, 16f));
            frameConstants.Fragmentation0 = new float4(
                fragmentationCenterA.x,
                fragmentationCenterA.y,
                fragmentationCenterA.z,
                fragmentation01);
            frameConstants.Fragmentation1 = new float4(
                fragmentationCenterB.x,
                fragmentationCenterB.y,
                fragmentationCenterB.z,
                math.max(1f, fragmentationHalfDistance));
            frameConstants.SonarScatter0 = new float4(
                sonarScatterOrigin.x,
                sonarScatterOrigin.y,
                sonarScatterOrigin.z,
                _sonarScatterWaveFrontWS);
            frameConstants.AcousticPanic0 = new float4(
                acousticPanicOrigin.x,
                acousticPanicOrigin.y,
                acousticPanicOrigin.z,
                acousticPanicStrength01 > 0f ? ClampFinite(_acousticPanicRadiusWS, 1f, SensoryAcousticPingMaxRadiusMeters) : 0f);
            frameConstants.AcousticPanic1 = new float4(
                _acousticPanicSeed,
                acousticPanicStrength01,
                acousticPanicTimeRemaining,
                shouldCollectLatchStats ? 1f : 0f);
            frameConstants.AbyssalFlowWeatherCurrent = new float4(
                abyssalFlowWeatherCurrent.x,
                abyssalFlowWeatherCurrent.y,
                abyssalFlowWeatherCurrent.z,
                predatorAupThreatLoopCap);
            frameConstants.PlayerDirection = new float4(
                playerDirection.x,
                playerDirection.y,
                playerDirection.z,
                predatorAupCount);

            try
            {
                if (!TryAcquireSargassumWriteLock(
                        in _simulationFrameHandle,
                        BufferID.SargassumSimulationFrame,
                        1,
                        out NativeArray<SimulationFrameConstants> simulationFrame))
                {
                    return false;
                }

                try
                {
                    simulationFrame[0] = frameConstants;
                    GraphicsBufferUploadUtility.UploadNativeArray(_simulationFrameBuffer, simulationFrame, 1);
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _simulationFrameHandle);
                }

                if (!EnsureStaticComputeBufferBindings())
                    return false;

                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferReadId, readBuffer);
                boidCompute.SetBuffer(_kernelIndex, _BoidsBufferWriteId, writeBuffer);
                boidCompute.SetBuffer(_kernelIndex, _PredatorAUPBufferId, ResolveBoidSensoryThreatReadBuffer());
                if (predatorAupBuffer != null && predatorAupThreatLoopCap > 0)
                    boidCompute.SetBuffer(_kernelIndex, _EncounterPredatorAUPBufferId, predatorAupBuffer);
                else
                    boidCompute.SetBuffer(_kernelIndex, _EncounterPredatorAUPBufferId, _predatorAupFallbackBuffer);

                SetMainKernelTextureIfChanged(_DensityTexId, densityTexture, ref _boundComputeDensityTexture);
                SetMainKernelTextureIfChanged(_CutMaskTexId, activeCutMaskTexture, ref _boundComputeCutMaskTexture);
                SetMainKernelTextureIfChanged(_AbyssalFlowFieldTextureId, abyssalFlowTexture, ref _boundAbyssalFlowTexture);
                boidCompute.SetVector(_AbyssalFlowCenterId, abyssalFlowCenter);
                boidCompute.SetVector(_AbyssalFlowSpacingId, abyssalFlowSpacing);
                boidCompute.SetFloat(_AbyssalFlowActiveId, abyssalFlowActive);
                boidCompute.SetFloat(_AbyssalFlowWeightId, 1f);
                boidCompute.SetInt(_SimulationBucketIndexId, simulationBucketIndex);
                boidCompute.SetInt(_SimulationBucketMaskId, simulationBucketMask);

                boidCompute.SetBuffer(_buildSpatialGridKernelIndex, _BoidsBufferReadId, readBuffer);

                boidCompute.SetBuffer(_pbdSolveKernelIndex, _BoidsBufferReadId, readBuffer);
            }
            catch (ObjectDisposedException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (InvalidOperationException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (ArgumentException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (MissingReferenceException)
            {
                DisableComputeDispatch(ComputeDisableReasonBindingFailure);
                return false;
            }
            catch (UnityException)
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
            _debugPredatorAupThreatCount = predatorAupThreatLoopCap;
            _debugBoidSensoryThreatCount = boidSensoryThreatCount;
            _debugBoidFlashlightThreatRadius = _boidFlashlightThreatRadiusWS;
            return true;
        }

        private void ResolveSimulationBucketUniforms(out int simulationBucketIndex, out int simulationBucketMask)
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer != null && bucketer.IsInitialized)
            {
                simulationBucketMask = bucketer.SlowBucketMask;
                simulationBucketIndex = bucketer.ActiveSlowBucket;
                _simulationInterpolationAlpha = math.saturate(bucketer.SimulationBucketInterpolationAlpha);
                return;
            }

            int frameCount = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            simulationBucketMask = FaunaSimulationBucketMask;
            simulationBucketIndex = frameCount & simulationBucketMask;
            _simulationInterpolationAlpha = (simulationBucketIndex + 1) * FaunaSimulationBucketInvCount;
        }

        private static bool IsFaunaAmbientDriftKillSwitchActive()
        {
            RefreshSystemKillSwitchBitsSnapshot();
            return (_systemKillSwitchMaskSnapshot & FaunaAmbientDriftKillSwitchMask) != 0u;
        }

        private static void RefreshSystemKillSwitchBitsSnapshot()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_systemKillSwitchSnapshotFrame == frame)
                return;

            _systemKillSwitchSnapshotFrame = frame;
            ReadOnlySpan<SystemKillSwitchBitsSignal> signals = SignalBus<SystemKillSwitchBitsSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                _systemKillSwitchMaskSnapshot = signals[i].CurrentMask;
        }

        private static void ReportWatchdogCost(uint subsystemHash, long startTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0L)
                return;

            float elapsedMilliseconds = (float)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            RuntimeWatchdog.ReportSubsystemCost(subsystemHash, elapsedMilliseconds);
        }

        void ISargassumGlobalDragEventListener.OnSargassumEntanglementStrain(in SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
        }

        private void RefreshPlayerGpuFrame(
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

            if (!RefreshPlayerRuntimeSnapshotCache(
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

        private bool RefreshPlayerRuntimePosition(out Vector3 playerPosition)
        {
            return RefreshPlayerRuntimeMotion(out playerPosition, out _);
        }

        private bool RefreshPlayerRuntimeMotion(out Vector3 playerPosition, out Vector3 playerVelocity)
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_playerMotionCacheFrame != currentFrame)
            {
                _playerMotionCacheFrame = currentFrame;
                _playerMotionCacheValid = BuildPlayerRuntimeMotionUncached(
                    out _playerMotionCachePosition,
                    out _playerMotionCacheVelocity);
            }

            playerPosition = _playerMotionCachePosition;
            playerVelocity = _playerMotionCacheVelocity;
            return _playerMotionCacheValid;
        }

        private bool BuildPlayerRuntimeMotionUncached(out Vector3 playerPosition, out Vector3 playerVelocity)
        {
            playerPosition = Vector3.zero;
            playerVelocity = Vector3.zero;
            if (!RefreshPlayerRuntimeSnapshotCache(
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
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
            {
                return;
            }

            try
            {
            int threatCapacity = math.min(maxMassiveThreatCount, massiveThreats.Length);
            if (threatCapacity == 0 ||
                !IsFiniteVector3(signal.PositionWS) ||
                !float.IsFinite(signal.RadiusWS) ||
                !float.IsFinite(signal.ExtremePanicRadiusWS) ||
                !float.IsFinite(signal.Duration) ||
                signal.RadiusWS <= 0.001f ||
                signal.Duration <= 0.001f)
            {
                return;
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float safeSignalRadius = ClampFinite(signal.RadiusWS, 0.5f, MassiveThreatMaxRadiusMeters);
            float safeExtremePanicRadius = ClampFinite(signal.ExtremePanicRadiusWS, safeSignalRadius, MassiveThreatMaxRadiusMeters);
            float safeDuration = ClampFinite(signal.Duration, 0.25f, MassiveThreatMaxDurationSeconds);
            float panicRadius = ClampFinite(
                math.max(massiveThreatPanicRadius, math.max(safeExtremePanicRadius, safeSignalRadius * 3f)),
                massiveThreatPanicRadius,
                MassiveThreatMaxRadiusMeters);
            int targetIndex = -1;
            float weakestEndTime = float.MaxValue;
            Vector3 inferredDirectionWS = Vector3.zero;

            for (int i = 0; i < threatCapacity; i++)
            {
                MassiveThreatData threat = massiveThreats[i];
                if (!TrySanitizeActiveMassiveThreat(in threat, absoluteSimulationTime, out threat))
                {
                    targetIndex = i;
                    break;
                }

                massiveThreats[i] = threat;
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
                RefreshPlayerRuntimeMotion(out Vector3 playerPosition, out Vector3 playerVelocity))
            {
                if (TryResolveAupFromRuntimeOrigin(signal.PositionWS, out AbsoluteUniversePosition signalAup) &&
                    TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup) &&
                    AbsoluteUniversePosition.DistanceSq(in signalAup, in playerAup) <= (double)panicRadius * panicRadius &&
                    playerVelocity.sqrMagnitude > 0.0001f)
                {
                    inferredDirectionWS = FastNormalizeVector3(playerVelocity, Vector3.forward);
                }
            }

            massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = signal.PositionWS,
                InnerRadius = safeSignalRadius,
                PanicRadius = panicRadius,
                Strength = 1f,
                EndTime = absoluteSimulationTime + safeDuration,
                DirectionWS = inferredDirectionWS,
                ThreatFlags = (uint)MassiveThreatFlags.None
            };

            RecalculateMassiveThreatCount(massiveThreats);
            UploadMassiveThreats();

            IFluidDecalPresentationSink fluidDecals = _abyssalFluidDecals;
            if ((_deepModeActive || _parasiteModeActive || _formationModeActive || _leviathanModeActive) && fluidDecals != null)
            {
                float ruptureScale = SaturateFinite01(safeSignalRadius * math.rcp(math.max(1f, deepBaitBallRadius * 2f)));
                fluidDecals.RegisterRuptureFluid(signal.PositionWS, ruptureScale);
            }

            float headVelocitySq = _leviathanHeadVelocityWS.sqrMagnitude;
            Vector3 displacementDirection = headVelocitySq > 0.0001f
                ? _leviathanHeadVelocityWS
                : (signal.PositionWS - _fieldCenter);
            TriggerFragmentation(signal.PositionWS, displacementDirection, safeExtremePanicRadius, absoluteSimulationTime);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        public void RegisterLeviathanThreatPulse(
            Vector3 positionWS,
            Vector3 directionWS,
            float panicRadiusWS,
            float durationSeconds)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
                return;

            try
            {
                int threatCapacity = math.min(maxMassiveThreatCount, massiveThreats.Length);
                if (threatCapacity == 0)
                    return;

                if (!IsFiniteVector3(positionWS) ||
                    !float.IsFinite(panicRadiusWS) ||
                    !float.IsFinite(durationSeconds) ||
                    panicRadiusWS <= 0.001f ||
                    durationSeconds <= 0.001f)
                {
                    return;
                }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float safePanicRadius = ClampFinite(panicRadiusWS, 4f, MassiveThreatMaxRadiusMeters);
            float safeDuration = ClampFinite(durationSeconds, 0.15f, MassiveThreatMaxDurationSeconds);
            int targetIndex = -1;
            float weakestEndTime = float.MaxValue;
            for (int i = 0; i < threatCapacity; i++)
            {
                MassiveThreatData threat = massiveThreats[i];
                if (!TrySanitizeActiveMassiveThreat(in threat, absoluteSimulationTime, out threat))
                {
                    targetIndex = i;
                    break;
                }

                massiveThreats[i] = threat;
                if ((threat.ThreatFlags & (uint)MassiveThreatFlags.LeviathanHuntPulse) == 0u)
                {
                    if (threat.EndTime < weakestEndTime)
                    {
                        weakestEndTime = threat.EndTime;
                        targetIndex = i;
                    }

                    continue;
                }

                float mergeDistance = math.max(threat.PanicRadius, safePanicRadius) * 0.35f;
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
            massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = positionWS,
                InnerRadius = math.max(0.5f, boidBodyRadius * 2f),
                PanicRadius = safePanicRadius,
                Strength = 1f,
                EndTime = absoluteSimulationTime + safeDuration,
                DirectionWS = resolvedDirection,
                ThreatFlags = (uint)MassiveThreatFlags.LeviathanHuntPulse
            };

                RecalculateMassiveThreatCount(massiveThreats);
                UploadMassiveThreats();
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        public void RegisterPredatorFearBurst(
            Vector3 positionWS,
            Vector3 directionWS,
            float panicRadiusWS,
            float durationSeconds,
            float strength01)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
            {
                return;
            }

            try
            {
                int threatCapacity = math.min(maxMassiveThreatCount, massiveThreats.Length);
                if (threatCapacity == 0 ||
                    !IsFiniteVector3(positionWS) ||
                    !float.IsFinite(panicRadiusWS) ||
                    !float.IsFinite(durationSeconds) ||
                    !float.IsFinite(strength01))
                {
                    return;
                }

                float clampedStrength01 = SaturateFinite01(strength01);
                if (panicRadiusWS <= 0.001f || durationSeconds <= 0.001f || clampedStrength01 <= 0.0001f)
                    return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float safePanicRadius = ClampFinite(panicRadiusWS, 3f, MassiveThreatMaxRadiusMeters);
            float safeDuration = ClampFinite(durationSeconds, SwarmAcousticShockDurationSeconds, MassiveThreatMaxDurationSeconds);
            int targetIndex = -1;
            float weakestEndTime = float.MaxValue;
            for (int i = 0; i < threatCapacity; i++)
            {
                MassiveThreatData threat = massiveThreats[i];
                if (!TrySanitizeActiveMassiveThreat(in threat, absoluteSimulationTime, out threat))
                {
                    targetIndex = i;
                    break;
                }

                massiveThreats[i] = threat;
                if ((threat.ThreatFlags & (uint)MassiveThreatFlags.LeviathanHuntPulse) != 0u)
                    continue;

                float mergeDistance = math.max(threat.PanicRadius, safePanicRadius) * 0.45f;
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
            massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = positionWS,
                InnerRadius = math.max(0.5f, boidBodyRadius * 1.5f),
                PanicRadius = safePanicRadius,
                Strength = clampedStrength01,
                EndTime = absoluteSimulationTime + safeDuration,
                DirectionWS = resolvedDirection,
                ThreatFlags = 0u
            };

                RecalculateMassiveThreatCount(massiveThreats);
                UploadMassiveThreats();
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        public int RegisterPredatorConsumptionBurst(
            Vector3 predatorPositionWS,
            Vector3 biteCenterWS,
            float biteRangeMeters,
            uint predatorId,
            float currentTimeSeconds)
        {
            Vector3 safeTelemetryCenterWS = IsFiniteVector3(biteCenterWS)
                ? biteCenterWS
                : IsFiniteVector3(_fieldCenter)
                    ? _fieldCenter
                    : Vector3.zero;

            if (!IsFiniteVector3(predatorPositionWS) ||
                !IsFiniteVector3(biteCenterWS) ||
                !float.IsFinite(biteRangeMeters) ||
                !float.IsFinite(currentTimeSeconds) ||
                biteRangeMeters <= 0.001f)
            {
                return 0;
            }

            int emittedCount = EmitPredatorKillSignals(
                predatorPositionWS,
                biteCenterWS,
                biteRangeMeters,
                predatorId);
            if (emittedCount <= 0)
                return 0;

            RecordFoodChainTelemetry(
                FoodChainTelemetryFlagKillJobScheduled,
                safeTelemetryCenterWS,
                predatorId,
                0u);

            int drainedCount = DrainPredatorKillSignals(currentTimeSeconds);
            RecordFoodChainTelemetry(
                FoodChainTelemetryFlagKillJobCompleted | (drainedCount > 0 ? FoodChainTelemetryFlagKillDrained : 0u),
                safeTelemetryCenterWS,
                predatorId,
                0u);
            return drainedCount;
        }

        private int CompletePendingPredatorConsumption(bool forceComplete)
        {
            return 0;
        }

        private int EmitPredatorKillSignals(
            Vector3 predatorPositionWS,
            Vector3 biteCenterWS,
            float biteRangeMeters,
            uint predatorId)
        {
            if (!TryReadOnlySargassumVaultArray(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData>.ReadOnly boidState))
            {
                WritePredatorKillSignalCount(0);
                return 0;
            }

            if (!WritePredatorKillSignalCount(0) ||
                !TryAcquireSargassumWriteLock(
                    in _killSignalHandle,
                    BufferID.SargassumKillSignals,
                    PredatorKillSignalDrainLimit,
                    out NativeArray<BoidKillSignal> killSignals))
            {
                return 0;
            }

            int emitted = 0;
            try
            {
                int safeCount = math.clamp(_activeBoidCount, 0, boidState.Length);
                float safeBiteRange = ClampFinite(biteRangeMeters, 0.05f, MassiveThreatMaxRadiusMeters);
                float biteRangeSq = safeBiteRange * safeBiteRange;
                float3 biteCenter = (float3)(biteCenterWS);
                float3 predatorPosition = (float3)(predatorPositionWS);
                for (int i = 0; i < safeCount && emitted < PredatorKillSignalDrainLimit; i++)
                {
                    BoidData boid = boidState[i];
                    if ((boid.StateFlags & ConsumedBoidStateFlag) != 0u)
                        continue;

                    float3 boidPosition = (float3)(boid.Position);
                    if (math.lengthsq(boidPosition - biteCenter) > biteRangeSq)
                        continue;

                    killSignals[emitted] = new BoidKillSignal
                    {
                        KillPositionWS = boidPosition,
                        PredatorPositionWS = predatorPosition,
                        BoidId = i,
                        PredatorId = predatorId,
                        FearRadiusMeters = PredatorKillDefaultFearRadiusMeters,
                        FearAmount = PredatorKillFearAmount
                    };
                    emitted++;
                }
            }
            finally
            {
                ReleaseSargassumWriteLock(in _killSignalHandle);
            }

            return WritePredatorKillSignalCount(emitted) ? emitted : 0;
        }

        private bool WritePredatorKillSignalCount(int emitted)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _killSignalCountHandle,
                    BufferID.SargassumKillSignalCount,
                    1,
                    out NativeArray<int> killSignalCount))
            {
                return false;
            }

            try
            {
                killSignalCount[0] = math.clamp(emitted, 0, PredatorKillSignalDrainLimit);
                return true;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _killSignalCountHandle);
            }
        }

        private int DrainPredatorKillSignals(float currentTimeSeconds)
        {
            var killSignals = ResolveSargassumVaultArray(in _killSignalHandle, BufferID.SargassumKillSignals, PredatorKillSignalDrainLimit);
            var killSignalCount = ResolveSargassumVaultArray(in _killSignalCountHandle, BufferID.SargassumKillSignalCount, 1);
            if (!killSignals.IsCreated || !killSignalCount.IsCreated || killSignalCount.Length <= 0)
                return 0;

            int drainedCount = 0;
            uint drainedMask = 0u;
            Vector3 frenzyCentroid = Vector3.zero;
            int signalCount = math.clamp(killSignalCount[0], 0, math.min(killSignals.Length, PredatorKillSignalDrainLimit));
            if (signalCount > 0 &&
                TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
            {
                try
                {
                    for (int signalIndex = 0; signalIndex < signalCount; signalIndex++)
                    {
                        BoidKillSignal killSignal = killSignals[signalIndex];
                        int boidId = killSignal.BoidId;
                        if (boidId < 0 ||
                            boidId >= _activeBoidCount ||
                            boidId >= boidState.Length)
                        {
                            continue;
                        }

                        BoidData boid = boidState[boidId];
                        if ((boid.StateFlags & ConsumedBoidStateFlag) != 0u)
                            continue;

                        Vector3 killPositionWS = ToVector3(killSignal.KillPositionWS);
                        if (!float.IsFinite(killPositionWS.x) ||
                            !float.IsFinite(killPositionWS.y) ||
                            !float.IsFinite(killPositionWS.z))
                        {
                            continue;
                        }

                        boid.Panic = 0f;
                        boid.Velocity = Vector3.zero;
                        boid.StateFlags = (boid.StateFlags & BoidVisualMutationMask) | ConsumedBoidStateFlag;
                        boidState[boidId] = boid;
                        UploadSingleBoidToLiveBuffers(boidState, boidId);

                        drainedMask |= 1u << signalIndex;
                        frenzyCentroid += killPositionWS;
                        drainedCount++;
                        _debugConsumedBoidCount++;
                    }
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _boidStateHandle);
                }
            }

            for (int signalIndex = 0; signalIndex < signalCount; signalIndex++)
            {
                if ((drainedMask & (1u << signalIndex)) == 0u)
                    continue;

                BoidKillSignal killSignal = killSignals[signalIndex];
                int boidId = killSignal.BoidId;
                Vector3 killPositionWS = ToVector3(killSignal.KillPositionWS);
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
            }

            if (drainedCount > 0)
                TryPublishFeedingFrenzyAcousticPing(frenzyCentroid * math.rcp((float)drainedCount), currentTimeSeconds, drainedCount);

            WritePredatorKillSignalCount(0);
            return drainedCount;
        }

        private void PublishPredatorKillDebris(in BoidKillSignal killSignal, Vector3 killPositionWS, int boidId)
        {
            uint sourceId = killSignal.PredatorId != 0u
                ? killSignal.PredatorId
                : (uint)math.hash(new int2(boidId, Hecton8.Core.SystemDispatcher.CurrentFrameIndex));
            if (TryResolveAupFromRuntimeOrigin(killPositionWS, out AbsoluteUniversePosition killAup))
            {
                DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
                {
                    PositionAup = killAup,
                    SpeciesHash = (uint)math.hash(new int2(boidId, (int)(sourceId & 0x7FFFFFFFu))),
                    SourceEntityId = sourceId,
                    Intensity01 = 1f,
                    DebrisKind = PredatorKillBloodDebrisKind,
                    Flags = PredatorKillDebrisFlags
                };
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debrisSignal, ref s_x001SargassumMicroFaunaBoidsSignalPushDropCount);
            }

            IFluidDecalPresentationSink fluidDecals = _abyssalFluidDecals;
            if (fluidDecals != null)
                fluidDecals.RegisterRuptureFluid(killPositionWS, PredatorKillFluidDecalRadiusScale);
        }

        private void TryPublishFeedingFrenzyAcousticPing(Vector3 centroidWS, float currentTimeSeconds, int killCount)
        {
            if (!IsFiniteVector3(centroidWS) ||
                !float.IsFinite(currentTimeSeconds))
            {
                return;
            }

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

            if (!TryResolveAupFromRuntimeOrigin(centroidWS, out AbsoluteUniversePosition centroidAup))
                return;

            AcousticPingSignal acousticPingSignal = new AcousticPingSignal
            {
                PositionAup = centroidAup,
                RadiusMeters = FeedingFrenzyAcousticRadiusMeters,
                Intensity01 = math.saturate(_feedingFrenzyKillCount * PredatorKillSignalDrainLimitInv),
                SourceId = math.hash(new float3(centroidWS.x, centroidWS.y, centroidWS.z)),
                Channel = FeedingFrenzyAcousticChannel,
                Flags = FeedingFrenzyAcousticFlags
            };
            SignalBus<AcousticPingSignal>.TryPushTracked(in acousticPingSignal, ref s_x001SargassumMicroFaunaBoidsSignalPushDropCount);
            _feedingFrenzyKillCount = 0;
            _feedingFrenzyWindowStartTime = safeTime;
        }

        internal int RegisterWhaleFallScavengerBurst(Vector3 centerWS, uint sourceId, float currentTimeSeconds)
        {
            if (!IsFiniteVector3(centerWS) ||
                !float.IsFinite(currentTimeSeconds))
            {
                return 0;
            }

            float hibernation01 = math.saturate(_lastSimulationHibernation01);
            if (!TryAcquireSargassumWriteLock(
                    in _boidStateHandle,
                    BufferID.SargassumBoidState,
                    boidCount,
                    out NativeArray<BoidData> boidState))
            {
                RegisterPredatorFearBurst(
                    centerWS,
                    Vector3.forward,
                    WhaleFallScavengerRadiusMeters,
                    ResolveWhaleFallScavengerFearDuration(hibernation01),
                    ResolveWhaleFallScavengerFearAmount(hibernation01));
                RecordFoodChainTelemetry(FoodChainTelemetryFlagWhaleFall, centerWS, sourceId, 0u);
                return 0;
            }

            bool emitFallbackAfterRelease = false;
            uint telemetrySourceAfterRelease = sourceId;
            int resultCount = 0;
            try
            {
                if (_activeBoidCount <= 0)
                {
                    emitFallbackAfterRelease = true;
                }
                else
                {
                    int safeActiveCount = math.min(_activeBoidCount, boidState.Length);
                    int visualCount = ResolveWhaleFallScavengerVisualCount(safeActiveCount, hibernation01);
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

                        BoidData boid = boidState[boidId];
                        boid.Position = positionWS;
                        boid.Velocity = tangent * WhaleFallScavengerTangentSpeedMetersPerSecond;
                        boid.Panic = 0f;
                        boid.StateFlags = (boid.StateFlags & BoidVisualMutationMask) | DefaultBoidStateFlags;
                        boidState[boidId] = boid;
                        UploadSingleBoidToLiveBuffers(boidState, boidId);
                    }

                    _fieldCenter = centerWS;
                    _fieldExtents = new Vector3(WhaleFallScavengerRadiusMeters * 1.35f, 2f, WhaleFallScavengerRadiusMeters * 1.35f);
                    _renderBounds = new Bounds(_fieldCenter, _fieldExtents * 2f);
                    _debugRenderBounds = _renderBounds;
                    emitFallbackAfterRelease = true;
                    telemetrySourceAfterRelease = safeSourceId;
                    resultCount = visualCount;
                }
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidStateHandle);
            }

            if (emitFallbackAfterRelease)
            {
                RegisterPredatorFearBurst(
                    centerWS,
                    Vector3.forward,
                        WhaleFallScavengerRadiusMeters,
                        ResolveWhaleFallScavengerFearDuration(hibernation01),
                        ResolveWhaleFallScavengerFearAmount(hibernation01));
                RecordFoodChainTelemetry(FoodChainTelemetryFlagWhaleFall, centerWS, telemetrySourceAfterRelease, 0u);
            }

            return resultCount;
        }

        private static int ResolveWhaleFallScavengerVisualCount(int safeActiveCount, float hibernation01)
        {
            if (safeActiveCount <= 0)
                return 0;

            float wake01 = math.lerp(
                WhaleFallScavengerMinimumWake01,
                1f,
                ResolveWhaleFallScavengerActivity01(hibernation01));
            int requestedCount = (int)math.round(WhaleFallScavengerVisualCount * wake01);
            return math.clamp(requestedCount, 1, safeActiveCount);
        }

        private static float ResolveWhaleFallScavengerFearDuration(float hibernation01)
        {
            float activity01 = ResolveWhaleFallScavengerActivity01(hibernation01);
            return math.lerp(WhaleFallDormantFearDurationSeconds, WhaleFallActiveFearDurationSeconds, activity01);
        }

        private static float ResolveWhaleFallScavengerFearAmount(float hibernation01)
        {
            float activity01 = ResolveWhaleFallScavengerActivity01(hibernation01);
            return math.lerp(WhaleFallDormantFearAmount, WhaleFallActiveFearAmount, activity01);
        }

        private static float ResolveWhaleFallScavengerActivity01(float hibernation01)
        {
            float active01 = 1f - math.saturate(hibernation01);
            return active01 * active01;
        }

        private Vector3 ResolveWhaleFallGroundHuggingPosition(Vector3 positionWS)
        {
            HectonMapMagicVegetationBridge vegetationBridge = _mapMagicVegetationBridge;
            if (vegetationBridge != null &&
                vegetationBridge.TryGetCachedTerrainHeight(positionWS.x, positionWS.z, out float terrainHeight))
            {
                positionWS.y = terrainHeight + WhaleFallScavengerGroundOffsetMeters;
                return positionWS;
            }

            positionWS.y += WhaleFallScavengerGroundOffsetMeters;
            return positionWS;
        }

        private void UploadSingleBoidToLiveBuffers(NativeArray<BoidData> boidState, int boidId)
        {
            if (boidId < 0)
                return;

            UploadSingleBoidToBuffer(_boidsBufferA, boidState, boidId);
            UploadSingleBoidToBuffer(_boidsBufferB, boidState, boidId);
        }

        private static void UploadSingleBoidToBuffer(GraphicsBuffer buffer, NativeArray<BoidData> source, int boidId)
        {
            if (buffer == null ||
                !source.IsCreated ||
                boidId < 0 ||
                boidId >= source.Length ||
                boidId >= buffer.count)
            {
                return;
            }

            NativeArray<BoidData> mapped = buffer.LockBufferForWrite<BoidData>(boidId, 1);
            try
            {
                unsafe
                {
                    int stride = UnsafeUtility.SizeOf<BoidData>();
                    void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + ((long)boidId * stride);
                    void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, stride, sourcePtr, stride))
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SargassumMicroFaunaBoids));
                }
            }
            finally
            {
                buffer.UnlockBufferAfterWrite<BoidData>(1);
            }
        }

        private void RecordFoodChainTelemetry(uint flags, Vector3 eventPositionWS, uint sourceHash, uint anomalyHash)
        {
            int pendingKillJob = ResolvePendingPredatorKillSignalCountForTelemetry();
            if (pendingKillJob > 0)
                flags |= FoodChainTelemetryFlagKillJobScheduled;

            if (!TryAcquireSargassumWriteLock(
                    in _foodChainTelemetryRingHandle,
                    BufferID.SargassumFoodChainTelemetryRing,
                    FoodChainTelemetryCapacity,
                    out NativeArray<FoodChainTelemetryEntry> foodChainTelemetryRing))
            {
                return;
            }

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
            if ((uint)writeIndex >= (uint)FoodChainTelemetryCapacity)
                writeIndex = 0;

            int nextCursor = writeIndex + 1;
            if (nextCursor >= FoodChainTelemetryCapacity)
                nextCursor = 0;

            try
            {
                foodChainTelemetryRing[writeIndex] = new FoodChainTelemetryEntry
                {
                    FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    StateHash = math.hash(new uint4(
                        unchecked((uint)math.max(0, _activeBoidCount)),
                        unchecked((uint)math.max(0, _debugConsumedBoidCount)),
                        unchecked((uint)_lastSimulationLodTier),
                        flags ^ unchecked((uint)pendingKillJob))),
                    SourceHash = sourceHash,
                    Flags = flags,
                    ActiveBoidCount = _activeBoidCount,
                    ConsumedBoidCount = _debugConsumedBoidCount,
                    PendingKillJob = pendingKillJob,
                    LodTier = (int)_lastSimulationLodTier,
                    FieldCenterWS = new float3(safeFieldCenter.x, safeFieldCenter.y, safeFieldCenter.z),
                    EventPositionWS = new float3(safeEventPosition.x, safeEventPosition.y, safeEventPosition.z),
                    AnomalyHash = anomalyHash,
                    SimulationTime = _simulationTime
                };
                _foodChainTelemetryCursor = nextCursor;
            }
            finally
            {
                ReleaseSargassumWriteLock(in _foodChainTelemetryRingHandle);
            }

            if (anomalyHash != 0u)
                TryDumpFoodChainTelemetry(anomalyHash);
        }

        private int ResolvePendingPredatorKillSignalCountForTelemetry()
        {
            if (!TryReadOnlySargassumVaultArray(
                    in _killSignalCountHandle,
                    BufferID.SargassumKillSignalCount,
                    1,
                    out NativeArray<int>.ReadOnly killSignalCount) ||
                killSignalCount.Length <= 0)
            {
                return 0;
            }

            return math.clamp(killSignalCount[0], 0, PredatorKillSignalDrainLimit);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector3(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!math.isfinite(originAup.LocalX) ||
                !math.isfinite(originAup.LocalY) ||
                !math.isfinite(originAup.LocalZ))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.isfinite(positionAup.LocalX) &&
                math.isfinite(positionAup.LocalY) &&
                math.isfinite(positionAup.LocalZ);
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!AbsoluteUniversePosition.IsFinite(in positionAup))
                return false;

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 runtimeFloat = AUPMath.ResolveCameraRelative(in positionAup, in runtimeOriginAup);
            if (!math.all(math.isfinite(runtimeFloat)))
                return false;

            runtimePosition = ToVector3(runtimeFloat);
            return IsFiniteVector3(runtimePosition);
        }

        private unsafe void TryDumpFoodChainTelemetry(uint anomalyHash)
        {
            if (_foodChainTelemetryDumped)
                return;

            if (!TryReadOnlySargassumVaultArray(
                    in _foodChainTelemetryRingHandle,
                    BufferID.SargassumFoodChainTelemetryRing,
                    FoodChainTelemetryCapacity,
                    out NativeArray<FoodChainTelemetryEntry>.ReadOnly foodChainTelemetryRing))
            {
                if (!_foodChainTelemetryDumpSourceUnavailableLogged)
                {
                    _foodChainTelemetryDumpSourceUnavailableLogged = true;
                    Hecton8.Core.H8Debug.LogError(
                        "[SargassumMicroFaunaBoids] Food-chain telemetry dump source unavailable. path=" +
                        FoodChainTelemetryDumpPath +
                        " anomaly=0x" +
                        anomalyHash.ToString("X8"));
                }

                return;
            }

            bool wrote = TryWriteFoodChainTelemetryDump(foodChainTelemetryRing, anomalyHash);
            _foodChainTelemetryDumped = wrote;
            if (!wrote && !_foodChainTelemetryDumpFailureLogged)
            {
                _foodChainTelemetryDumpFailureLogged = true;
                Hecton8.Core.H8Debug.LogError(
                    "[SargassumMicroFaunaBoids] Food-chain telemetry dump failed. path=" +
                    FoodChainTelemetryDumpPath +
                    " anomaly=0x" +
                    anomalyHash.ToString("X8"));
            }
        }

        private unsafe bool TryWriteFoodChainTelemetryDump(
            NativeArray<FoodChainTelemetryEntry>.ReadOnly foodChainTelemetryRing,
            uint anomalyHash)
        {
            if (!foodChainTelemetryRing.IsCreated)
                return false;

            int capacity = math.min(foodChainTelemetryRing.Length, FoodChainTelemetryCapacity);
            if (capacity <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<FoodChainTelemetryEntry>();
            const int headerBytes = (sizeof(uint) * 3) + (sizeof(int) * 3);
            int byteCount = headerBytes + capacity * entrySize;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(SargassumMicroFaunaBoids),
                    FoodChainTelemetryDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.WriteArrayElement<uint>(destination, 0, FoodChainTelemetryMagicLow);
                UnsafeUtility.WriteArrayElement<uint>(destination + sizeof(uint), 0, FoodChainTelemetryMagicHigh);
                UnsafeUtility.WriteArrayElement<int>(destination + sizeof(uint) * 2, 0, entrySize);
                UnsafeUtility.WriteArrayElement<int>(destination + (sizeof(uint) * 2) + sizeof(int), 0, capacity);
                UnsafeUtility.WriteArrayElement<int>(destination + (sizeof(uint) * 2) + (sizeof(int) * 2), 0, _foodChainTelemetryCursor);
                UnsafeUtility.WriteArrayElement<uint>(destination + (sizeof(uint) * 2) + (sizeof(int) * 3), 0, anomalyHash);

                byte* rows = destination + headerBytes;
                for (int i = 0; i < capacity; i++)
                {
                    FoodChainTelemetryEntry entry = foodChainTelemetryRing[i];
                    UnsafeUtility.CopyStructureToPtr(ref entry, rows + i * entrySize);
                }

                return NativeFaultDumpWriter.TryWriteAll(FoodChainTelemetryDumpPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SargassumMicroFaunaBoids),
                    FoodChainTelemetryDumpPayloadLabel);
            }
        }

        private void RecordBoidSensoryBlackBox(
            float4 submarineThreat,
            float4 flashlightThreat,
            float4 pingThreatA,
            float4 pingThreatB,
            float4 pingThreatC,
            int activeThreatCount,
            SimulationLodTier simulationLodTier,
            int sensoryThreatFlags,
            uint preUploadAnomalyHash)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _boidSensoryBlackBoxHandle,
                    BufferID.SargassumBoidSensoryBlackBox,
                    BoidSensoryBlackBoxCapacity,
                    out NativeArray<BoidSensoryBlackBoxEntry> boidSensoryBlackBox))
                return;

            try
            {
                float4 acousticPingRadii = new float4(
                    pingThreatA.w,
                    pingThreatB.w,
                    pingThreatC.w,
                    (float)simulationLodTier);

            uint flags = BoidSensoryBlackBoxFlagTick;
            if (flashlightThreat.w >= SensoryThreatMinRadiusMeters)
                flags |= BoidSensoryBlackBoxFlagLightActive;
            if (pingThreatA.w >= SensoryThreatMinRadiusMeters ||
                pingThreatB.w >= SensoryThreatMinRadiusMeters ||
                pingThreatC.w >= SensoryThreatMinRadiusMeters)
            {
                flags |= BoidSensoryBlackBoxFlagPingActive;
            }

            if ((sensoryThreatFlags & unchecked((int)SensoryThreatFlagFlashlightCapsule)) != 0)
                flags |= BoidSensoryBlackBoxFlagCapsule;

            bool hasInvalidBlackBoxState =
                !IsFiniteFloat4(submarineThreat) ||
                !IsFiniteFloat4(flashlightThreat) ||
                !IsFiniteFloat4(pingThreatA) ||
                !IsFiniteFloat4(pingThreatB) ||
                !IsFiniteFloat4(pingThreatC) ||
                activeThreatCount < 0 ||
                activeThreatCount > PredatorAupBufferCapacity;

            uint anomalyHash = preUploadAnomalyHash;
            if (hasInvalidBlackBoxState)
            {
                uint invalidHash = math.hash(new uint4(
                    anomalyHash,
                    HashThreatFloat4(submarineThreat) ^ HashThreatFloat4(flashlightThreat),
                    HashThreatFloat4(pingThreatA) ^ HashThreatFloat4(pingThreatB) ^ HashThreatFloat4(pingThreatC),
                    BoidSensoryBlackBoxAnomalyNonFinite ^ unchecked((uint)activeThreatCount)));
                anomalyHash = invalidHash != 0u ? invalidHash : BoidSensoryBlackBoxAnomalyNonFinite;
            }

            if (anomalyHash != 0u)
                flags |= BoidSensoryBlackBoxFlagNonFinite;

            int ringLength = math.min(boidSensoryBlackBox.Length, BoidSensoryBlackBoxCapacity);
            int writeIndex = _boidSensoryBlackBoxCursor;
            if ((uint)writeIndex >= (uint)ringLength)
                writeIndex = 0;

            int nextCursor = writeIndex + 1;
            if (nextCursor >= ringLength)
                nextCursor = 0;

            uint pingHash = math.hash(new uint4(
                HashThreatFloat4(pingThreatA),
                HashThreatFloat4(pingThreatB),
                HashThreatFloat4(pingThreatC),
                unchecked((uint)(int)simulationLodTier)));

                boidSensoryBlackBox[writeIndex] = new BoidSensoryBlackBoxEntry
                {
                    FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    StateHash = math.hash(new uint4(
                        HashThreatFloat4(submarineThreat),
                        HashThreatFloat4(flashlightThreat),
                        pingHash,
                        flags ^ unchecked((uint)math.max(0, activeThreatCount)))),
                    Flags = flags,
                    ActiveThreatCount = activeThreatCount,
                    SubmarineThreat = submarineThreat,
                    FlashlightThreat = flashlightThreat,
                    AcousticPingRadii = acousticPingRadii
                };
                _boidSensoryBlackBoxCursor = nextCursor;

                if (anomalyHash != 0u)
                    TryDumpBoidSensoryBlackBox(boidSensoryBlackBox, anomalyHash);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _boidSensoryBlackBoxHandle);
            }
        }

        private float4 ReadBoidSensoryThreatSlot(NativeArray<float4> boidSensoryThreats, int slot)
        {
            return boidSensoryThreats.IsCreated && (uint)slot < (uint)boidSensoryThreats.Length
                ? boidSensoryThreats[slot]
                : float4.zero;
        }

        private static bool IsFiniteFloat4(float4 value)
        {
            return math.all(math.isfinite(value));
        }

        private static uint HashThreatFloat4(float4 value)
        {
            return math.hash(math.asuint(value));
        }

        private unsafe void TryDumpBoidSensoryBlackBox(
            NativeArray<BoidSensoryBlackBoxEntry> boidSensoryBlackBox,
            uint anomalyHash)
        {
            if (_boidSensoryBlackBoxDumped)
                return;

            if (!boidSensoryBlackBox.IsCreated)
            {
                if (!_boidSensoryBlackBoxDumpSourceUnavailableLogged)
                {
                    _boidSensoryBlackBoxDumpSourceUnavailableLogged = true;
                    Hecton8.Core.H8Debug.LogError(
                        "[SargassumMicroFaunaBoids] Boid sensory blackbox dump source unavailable. path=" +
                        BoidSensoryBlackBoxDumpPath +
                        " anomaly=0x" +
                        anomalyHash.ToString("X8"));
                }

                return;
            }

            bool wrote = TryWriteBoidSensoryBlackBoxDump(boidSensoryBlackBox, anomalyHash);
            _boidSensoryBlackBoxDumped = wrote;
            if (!wrote && !_boidSensoryBlackBoxDumpFailureLogged)
            {
                _boidSensoryBlackBoxDumpFailureLogged = true;
                Hecton8.Core.H8Debug.LogError(
                    "[SargassumMicroFaunaBoids] Boid sensory blackbox dump failed. path=" +
                    BoidSensoryBlackBoxDumpPath +
                    " anomaly=0x" +
                    anomalyHash.ToString("X8"));
            }
        }

        private unsafe bool TryWriteBoidSensoryBlackBoxDump(
            NativeArray<BoidSensoryBlackBoxEntry> boidSensoryBlackBox,
            uint anomalyHash)
        {
            if (!boidSensoryBlackBox.IsCreated)
                return false;

            int capacity = math.min(boidSensoryBlackBox.Length, BoidSensoryBlackBoxCapacity);
            if (capacity <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<BoidSensoryBlackBoxEntry>();
            const int headerBytes = (sizeof(uint) * 3) + (sizeof(int) * 3);
            int byteCount = headerBytes + capacity * entrySize;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(SargassumMicroFaunaBoids),
                    BoidSensoryBlackBoxDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.WriteArrayElement<uint>(destination, 0, BoidSensoryBlackBoxMagicLow);
                UnsafeUtility.WriteArrayElement<uint>(destination + sizeof(uint), 0, BoidSensoryBlackBoxMagicHigh);
                UnsafeUtility.WriteArrayElement<int>(destination + sizeof(uint) * 2, 0, entrySize);
                UnsafeUtility.WriteArrayElement<int>(destination + (sizeof(uint) * 2) + sizeof(int), 0, capacity);
                UnsafeUtility.WriteArrayElement<int>(destination + (sizeof(uint) * 2) + (sizeof(int) * 2), 0, _boidSensoryBlackBoxCursor);
                UnsafeUtility.WriteArrayElement<uint>(destination + (sizeof(uint) * 2) + (sizeof(int) * 3), 0, anomalyHash);

                byte* rows = destination + headerBytes;
                for (int i = 0; i < capacity; i++)
                {
                    BoidSensoryBlackBoxEntry entry = boidSensoryBlackBox[i];
                    UnsafeUtility.CopyStructureToPtr(ref entry, rows + i * entrySize);
                }

                return NativeFaultDumpWriter.TryWriteAll(BoidSensoryBlackBoxDumpPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SargassumMicroFaunaBoids),
                    BoidSensoryBlackBoxDumpPayloadLabel);
            }
        }

        /// <summary>
        /// Registers a GPU-only VAT hit reaction. Rendering owns the timestamp; no boid buffer mutation or CPU animation path is used.
        /// </summary>
        public void RegisterVatHitReaction(Vector3 originWS, float radiusMeters, float intensity01)
        {
            float clampedIntensity = SaturateFinite01(intensity01) * SaturateFinite01(hitFlashIntensity);
            if (clampedIntensity <= 0.0001f ||
                !IsFiniteVector3(originWS) ||
                !float.IsFinite(radiusMeters))
            {
                return;
            }

            _hitFlashOriginWS = originWS;
            _hitFlashRuntimeRadius = radiusMeters > 0.0001f
                ? ClampFinite(radiusMeters, 0f, MassiveThreatMaxRadiusMeters)
                : ClampFinite(hitFlashRadiusMeters, 0f, MassiveThreatMaxRadiusMeters);
            _hitFlashRuntimeIntensity = clampedIntensity;
            _hitFlashStartTime = ResolveHitFlashShaderClockSeconds();
        }

        private static float ResolveHitFlashShaderClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private void UpdateFragmentationState(
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 playerForward,
            float playerSpeed,
            float absoluteSimulationTime)
        {
            if (!float.IsFinite(absoluteSimulationTime))
            {
                _fragmentationStartTime = float.NegativeInfinity;
                _fragmentationExpireTime = float.NegativeInfinity;
                _fragmentationHalfDistanceWS = 0f;
                _debugFragmentation01 = 0f;
                return;
            }

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
            if (_sonarScatterStrength01 <= 0f ||
                !float.IsFinite(absoluteSimulationTime) ||
                absoluteSimulationTime >= _sonarScatterExpireTime)
            {
                _sonarScatterStrength01 = 0f;
                _sonarScatterWaveFrontWS = 0f;
                _debugSonarScatter01 = 0f;
                return;
            }

            _sonarScatterWaveFrontWS += ClampMinFinite(simulationDt, 0f) * ClampFinite(activeSonarWaveSpeed, 0.1f, 200f);
            if (!float.IsFinite(_sonarScatterWaveFrontWS))
                _sonarScatterWaveFrontWS = 0f;
        }

        private float ResolveFragmentationStrength01(float absoluteSimulationTime)
        {
            if (!float.IsFinite(absoluteSimulationTime) ||
                absoluteSimulationTime >= _fragmentationExpireTime ||
                !float.IsFinite(_fragmentationExpireTime) ||
                !float.IsFinite(_fragmentationStartTime))
                return 0f;

            float duration = math.max(0.1f, _fragmentationExpireTime - _fragmentationStartTime);
            float timeRemaining = math.max(0f, _fragmentationExpireTime - absoluteSimulationTime);
            return math.saturate(timeRemaining / math.max(0.1f, duration));
        }

        private void TriggerFragmentation(Vector3 originWS, Vector3 dashVectorWS, float baseRadiusWS, float absoluteSimulationTime)
        {
            if (!IsFiniteVector3(originWS) ||
                !float.IsFinite(baseRadiusWS) ||
                !float.IsFinite(absoluteSimulationTime) ||
                baseRadiusWS <= 0.001f)
            {
                return;
            }

            Vector3 safeDashVectorWS = IsFiniteVector3(dashVectorWS) ? dashVectorWS : Vector3.forward;
            float dashVectorSq = safeDashVectorWS.sqrMagnitude;
            Vector3 dashDirection = dashVectorSq > 0.0001f ? FastNormalizeVector3(safeDashVectorWS, Vector3.forward) : Vector3.forward;
            Vector3 splitAxis = ResolveApproxRight(dashDirection);

            float safeBaseRadius = ClampFinite(baseRadiusWS, 1f, MassiveThreatMaxRadiusMeters);
            float offsetDistance = math.max(1f, safeBaseRadius * math.max(0.5f, fragmentationOffsetScale));
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
                    HandleFlashlightToggled(FlashlightEventPayload.IsOn(in payload));
                    break;
                case Hecton8.Gameplay.FlashlightEventType.BatteryDepleted:
                case Hecton8.Gameplay.FlashlightEventType.Overheat:
                    HandleFlashlightToggled(false);
                    break;
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = SaturateFinite01(intensity);
            if (clampedIntensity <= 0f)
            {
                _sonarScatterStrength01 = 0f;
                _debugSonarScatter01 = 0f;
                return;
            }

            Vector3 originWS = RefreshPlayerRuntimePosition(out Vector3 playerPosition) ? playerPosition : _fieldCenter;
            if (!IsFiniteVector3(originWS))
                return;

            float maxFieldExtent = ClampFinite(math.max(_fieldExtents.x, math.max(_fieldExtents.y, _fieldExtents.z)), 1f, MassiveThreatMaxRadiusMeters);
            float safeWaveSpeed = ClampFinite(activeSonarWaveSpeed, 0.1f, 200f);
            float safeBandWidth = ClampFinite(activeSonarWaveBandWidth, 0.25f, 16f);
            float travelDistance = (maxFieldExtent * 2f) + safeBandWidth;

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

        public void RegisterAcousticPanicBurst(
            Vector3 originWS,
            float radiusWS,
            float durationSeconds,
            float strength01,
            uint seed)
        {
            if (!IsFiniteVector3(originWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(durationSeconds) ||
                !math.isfinite(strength01))
            {
                return;
            }

            float clampedStrength = SaturateFinite01(strength01);
            if (radiusWS <= 0.001f || durationSeconds <= 0.001f || clampedStrength <= 0.0001f)
                return;

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            float safeRadius = ClampFinite(radiusWS, 1f, SensoryAcousticPingMaxRadiusMeters);
            float safeDuration = ClampFinite(durationSeconds, 0.1f, MassiveThreatMaxDurationSeconds);
            float previousStrength = SaturateFinite01(_acousticPanicStrength01);
            float previousExpireTime = float.IsFinite(_acousticPanicExpireTime) ? _acousticPanicExpireTime : 0f;
            _acousticPanicOriginWS = originWS;
            _acousticPanicRadiusWS = safeRadius;
            _acousticPanicStrength01 = math.max(previousStrength, clampedStrength);
            _acousticPanicExpireTime = math.max(
                previousExpireTime,
                absoluteSimulationTime + safeDuration);
            _acousticPanicSeed = seed != 0u ? seed : 0x9E3779B9u;
        }

        private void ConsumeSwarmThreatSignals(float simulationDt)
        {
            ConsumeMovementAcousticSignals();
            ConsumeAcousticPingSignals(simulationDt);
        }

        private void RefreshMaelstromThreats()
        {
            if (_fluidEngine == null ||
                !_fluidEngine.TryGetActiveMaelstroms(
                    out NativeArray<float4>.ReadOnly maelstroms,
                    out int maelstromCount,
                    out Vector4 maelstromMeta))
            {
                _lastMaelstromThreatHash = 0u;
                return;
            }

            int maelstromCapacity = _fluidEngine != null
                ? _fluidEngine.MaxActiveMaelstromCapacity
                : FluidAnalyticalContractConstants.MaxActiveMaelstromCount;
            int count = math.clamp(maelstromCount, 0, math.min(maelstromCapacity, maelstroms.Length));
            if (count <= 0)
            {
                _lastMaelstromThreatHash = 0u;
                return;
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            uint hash = 2166136261u;
            for (int i = 0; i < count; i++)
            {
                float4 maelstrom = maelstroms[i];
                hash = HashMaelstromThreat(hash, QuantizeThreatCoord(maelstrom.x));
                hash = HashMaelstromThreat(hash, QuantizeThreatCoord(maelstrom.y));
                hash = HashMaelstromThreat(hash, QuantizeThreatCoord(maelstrom.z));
                hash = HashMaelstromThreat(hash, QuantizeThreatCoord(maelstrom.w));
            }

            if (hash == _lastMaelstromThreatHash && absoluteSimulationTime < _nextMaelstromThreatRefreshTime)
                return;

            _lastMaelstromThreatHash = hash;
            _nextMaelstromThreatRefreshTime = absoluteSimulationTime + MaelstromThreatRefreshSeconds;
            float radius = ClampFinite(maelstromMeta.y, 8f, 160f);
            for (int i = 0; i < count; i++)
            {
                float4 maelstrom = maelstroms[i];
                Vector3 originWS = new Vector3(maelstrom.x, maelstrom.y, maelstrom.z);
                if (!IsFiniteVector3(originWS))
                    continue;

                float intensity01 = SaturateFinite01(maelstrom.w * radius * 0.04f);
                RegisterPredatorFearBurst(
                    originWS,
                    Vector3.forward,
                    radius,
                    MaelstromThreatDurationSeconds,
                    math.max(0.35f, intensity01));
            }
        }

        private static uint HashMaelstromThreat(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private static uint QuantizeThreatCoord(float value)
        {
            if (!math.isfinite(value))
                return 0xffffffffu;

            return unchecked((uint)(int)math.clamp(math.round(value * 8f), int.MinValue + 1f, int.MaxValue - 1f));
        }

        private void ConsumeMovementAcousticSignals()
        {
            ReadOnlySpan<MovementAcousticSignal> movementSignals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            int signalStart = math.max(0, movementSignals.Length - SwarmMovementSignalConsumeLimit);
            for (int i = signalStart; i < movementSignals.Length; i++)
            {
                MovementAcousticSignal signal = movementSignals[i];
                float volume01 = SaturateFinite01(signal.Volume);
                if (volume01 <= 0.01f)
                    continue;

                if (!TryResolveRuntimePosition(in signal.PositionAup, out Vector3 runtimePosition))
                    continue;

                float velocitySq = math.isfinite(signal.VelocitySq) ? signal.VelocitySq : 0f;
                float velocityGate = SaturateFinite01(velocitySq * (1f / 144f));
                float radius = math.lerp(10f, 42f, velocityGate);
                RegisterAcousticPanicBurst(
                    runtimePosition,
                    radius,
                    SwarmMovementPanicDurationSeconds,
                    volume01,
                    signal.SourceId);
            }
        }

        private void ConsumeAcousticPingSignals(float simulationDt)
        {
            ReadOnlySpan<AcousticPingSignal> pingSignals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int signalStart = math.max(0, pingSignals.Length - SwarmAcousticSignalConsumeLimit);
            float shockDuration = ClampFinite(simulationDt, SwarmAcousticShockDurationSeconds, 0.5f);
            for (int i = signalStart; i < pingSignals.Length; i++)
            {
                AcousticPingSignal signal = pingSignals[i];
                float intensity01 = SaturateFinite01(signal.Intensity01);
                if (intensity01 <= 0.001f)
                    continue;

                if (!TryResolveRuntimePosition(in signal.PositionAup, out Vector3 originWS))
                    continue;

                float radius = math.clamp(math.isfinite(signal.RadiusMeters) ? signal.RadiusMeters : 0f, 12f, 120f);
                RegisterPredatorFearBurst(originWS, Vector3.forward, radius, shockDuration, intensity01);
                RegisterAcousticPanicBurst(originWS, radius, shockDuration, intensity01, signal.SourceId);
                TryPublishSwarmDispersedSignal(originWS, radius, intensity01, signal.SourceId);
            }
        }

        private void TryPublishSwarmDispersedSignal(Vector3 originWS, float radiusWS, float intensity01, uint sourceId)
        {
            if (!IsFiniteVector3(originWS) ||
                !float.IsFinite(radiusWS) ||
                !float.IsFinite(intensity01))
            {
                return;
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            if (intensity01 < SwarmDispersedMinimumIntensity ||
                absoluteSimulationTime < _lastSwarmDispersedSignalTime + SwarmDispersedSignalCooldownSeconds)
            {
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(originWS, out AbsoluteUniversePosition originAup))
                return;

            _lastSwarmDispersedSignalTime = absoluteSimulationTime;
            _swarmDispersedSequence++;
            uint resolvedSourceId = sourceId != 0u ? sourceId : _swarmDispersedSequence;
            SwarmDispersedSignal signal = new SwarmDispersedSignal
            {
                PositionAup = originAup,
                RadiusMeters = ClampFinite(radiusWS, 1f, SensoryAcousticPingMaxRadiusMeters),
                Intensity01 = SaturateFinite01(intensity01),
                SourceId = resolvedSourceId,
                EstimatedBoidCount = (ushort)math.clamp(_activeBoidCount, 0, (int)ushort.MaxValue),
                Flags = 1,
                QualityTier = ResolveSignalQualityWeightByte()
            };

            SignalBus<SwarmDispersedSignal>.TryPushTracked(in signal, ref s_x001SargassumMicroFaunaBoidsSignalPushDropCount);
            RecordFoodChainTelemetry(FoodChainTelemetryFlagBoidsScattered, originWS, resolvedSourceId, 0u);
        }

        private static byte ResolveSignalQualityWeightByte()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            float safeQuality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return (byte)math.clamp((int)math.round(safeQuality * 255f), 0, 255);
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
                    NativeArray<int> latchData = _parasiteLatchReadback.Data;
                    bool hasCompleteLatchStats = latchData.Length >= LatchStatsElementCount;
                    _reportedLatchedDroneCount = latchData.Length > LatchStatsLatchedCountIndex
                        ? math.clamp(latchData[LatchStatsLatchedCountIndex], 0, _activeBoidCount)
                        : 0;
                    if (_reportedLatchedDroneCount > 0 && hasCompleteLatchStats)
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

                    Vector3 playerPosition = RefreshPlayerRuntimePosition(out Vector3 resolvedPlayerPosition) ? resolvedPlayerPosition : _fieldCenter;
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

                    _reportedWakeFleeCount = hasCompleteLatchStats
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

        private void TryRequestParasiteLatchReadback(float hibernation01)
        {
            if (!enableParasiteLatchGpuReadback ||
                _parasiteLatchReadbackPending ||
                _parasiteLatchReadbackDisposeAfterCompletion ||
                _latchStatsBuffer == null ||
                _parasiteLatchReadbackTimer > 0f)
            {
                return;
            }

            if (!HasParasiteLatchReadbackData())
            {
                QueueParasiteLatchReadbackRepair();
                return;
            }

            _parasiteLatchReadbackRequest = AsyncGPUReadback.RequestIntoNativeArray(
                ref _parasiteLatchReadback.Data,
                _latchStatsBuffer,
                LatchStatsReadbackByteCount,
                0,
                ResolveParasiteLatchReadbackCompletion());
            _parasiteLatchReadbackPending = !_parasiteLatchReadbackRequest.hasError;
            _parasiteLatchReadbackTimer = ResolveLatchStatsReadbackInterval(hibernation01);
            if (!_parasiteLatchReadbackPending)
                _parasiteLatchReadbackRequest = default;
        }

        private Action<AsyncGPUReadbackRequest> ResolveParasiteLatchReadbackCompletion()
        {
            if (_parasiteLatchReadbackCompletion == null)
                _parasiteLatchReadbackCompletion = OnParasiteLatchReadbackComplete;

            return _parasiteLatchReadbackCompletion;
        }

        private void OnParasiteLatchReadbackComplete(AsyncGPUReadbackRequest request)
        {
            if (!_parasiteLatchReadbackDisposeAfterCompletion)
                return;

            _parasiteLatchReadbackPending = false;
            _parasiteLatchReadbackRequest = default;
            _parasiteLatchReadbackDisposeAfterCompletion = false;
            bool releaseStatsBuffer = _parasiteLatchReleaseStatsBufferAfterCompletion;
            _parasiteLatchReleaseStatsBufferAfterCompletion = false;
            ReleaseParasiteLatchReadbackNativeData();
            if (releaseStatsBuffer)
            {
                ReleaseBuffer(ref _parasiteLatchHeldStatsBuffer);
                _latchStatsBufferRawTarget = false;
            }
            else
            {
                _parasiteLatchHeldStatsBuffer = null;
            }
        }

        private bool EnsureParasiteLatchReadbackData()
        {
            if (_parasiteLatchReadbackDisposeAfterCompletion)
                return false;

            if (HasParasiteLatchReadbackData())
                return true;

            if (_parasiteLatchReadbackPending)
                return false;

            DisposeParasiteLatchReadbackData();
            _parasiteLatchReadback.Data = H8Memory.Allocate<int>(
                LatchStatsElementCount,
                SystemID.WorldSargassum,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            if (!_parasiteLatchReadback.Data.IsCreated)
                throw new InvalidOperationException("H8Memory allocation failed for parasite latch readback data.");

            _parasiteLatchReadbackRepairRequested = false;
            return true;
        }

        private bool HasParasiteLatchReadbackData()
        {
            return _parasiteLatchReadback.Data.IsCreated &&
                   _parasiteLatchReadback.Data.Length >= LatchStatsElementCount;
        }

        private void QueueParasiteLatchReadbackRepair()
        {
            _parasiteLatchReadbackRepairRequested = true;
        }

        private void FlushParasiteLatchReadbackRepairSlow()
        {
            if (_parasiteLatchReadbackDisposeAfterCompletion)
                return;

            if (!enableParasiteLatchGpuReadback)
            {
                _parasiteLatchReadbackRepairRequested = false;
                return;
            }

            if (!_parasiteLatchReadbackRepairRequested && HasParasiteLatchReadbackData())
                return;

            if (_latchStatsBuffer == null || _parasiteLatchReadbackPending)
                return;

            EnsureParasiteLatchReadbackData();
        }

        private void DisposeParasiteLatchReadbackData()
        {
            _parasiteLatchReadbackRepairRequested = false;
            if (_parasiteLatchReadbackDisposeAfterCompletion)
                return;

            ReleaseParasiteLatchReadbackNativeData();
        }

        private void ReleaseParasiteLatchReadbackNativeData()
        {
            if (_parasiteLatchReadback.Data.IsCreated)
                H8Memory.Release(ref _parasiteLatchReadback.Data, SystemID.WorldSargassum);
        }

        private void ApplyParasiteEnvironmentalDrag()
        {
            if (_playerMovement == null || !_parasiteModeActive || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            if (!enableParasiteLatchGpuReadback)
                UpdateParasiteLatchAnalyticalEstimate();

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

        private void UpdateParasiteLatchAnalyticalEstimate()
        {
            float aggression01 = ResolveParasiteAggression01();
            if (aggression01 <= 0.0001f || _activeBoidCount <= 0)
            {
                _reportedLatchedDroneCount = 0;
                _reportedParasiteCenterOfMassLS = Vector3.zero;
                _reportedParasiteHarvesterPullWS = Vector3.zero;
                _debugLatchedDroneCount = 0;
                _debugParasiteCenterOfMassLS = Vector3.zero;
                _debugParasiteHarvesterPullWS = Vector3.zero;
                return;
            }

            float populationScale = math.saturate(_activeBoidCount / math.max(1f, boidCount));
            float latchFraction = math.lerp(0.015f, 0.085f, aggression01) * math.lerp(0.65f, 1f, populationScale);
            _reportedLatchedDroneCount = math.clamp(
                RoundToIntPositive(_activeBoidCount * latchFraction),
                0,
                math.max(1, parasiteMaxLatchedDronesForFullDrag));
            _reportedParasiteCenterOfMassLS = Vector3.zero;

            if (_reportedLatchedDroneCount >= parasiteHarvesterLatchThreshold &&
                RefreshPlayerRuntimePosition(out Vector3 playerPosition) &&
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
        }

        private void ApplyLeviathanPhysicalStrike()
        {
            if ((_playerMovement == null && _playerHealth == null) || _leviathanStrikeCooldownTimer > 0f || !RefreshPlayerRuntimePosition(out Vector3 playerPosition))
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
            float impulseMagnitude = leviathanStrikeImpulse * math.lerp(0.8f, 1.35f, speed01);
            Vector3 traumaImpulse = strikeDirection * impulseMagnitude;
            if (_playerMovement != null)
                _playerMovement.ApplyPhysicalTrauma(traumaImpulse, math.lerp(leviathanStrikeTraumaWeight * 0.65f, leviathanStrikeTraumaWeight, speed01));

            if (_playerHealth != null &&
                !TryQueueLeviathanStrikeDamage(_playerHealth, leviathanStrikeDamage, playerPosition, strikeDirection, impulseMagnitude))
            {
                ApplyLeviathanStrikeOwnerFallbackDamage(_playerHealth, leviathanStrikeDamage, playerPosition);
            }

            _leviathanStrikeCooldownTimer = leviathanStrikeCooldown;
        }

        private static bool TryQueueLeviathanStrikeDamage(
            HectonPlayerHealth playerHealth,
            float damage,
            Vector3 impactPoint,
            Vector3 strikeDirection,
            float impulseMagnitude)
        {
            if (playerHealth == null || !(damage > 0f) || !math.isfinite(damage))
                return false;

            int targetId = CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Transform targetTransform = playerHealth.transform;
            Vector3 safeImpactPoint = IsFiniteVector3(impactPoint)
                ? impactPoint
                : targetTransform != null && IsFiniteVector3(targetTransform.position)
                    ? targetTransform.position
                    : Vector3.zero;
            Vector3 safeDirection = FastNormalizeVector3(strikeDirection, Vector3.forward);
            float3 direction3 = new float3(safeDirection.x, safeDirection.y, safeDirection.z);
            direction3 = math.all(math.isfinite(direction3)) ? direction3 : new float3(0f, 0f, 1f);

            CombatDamageRequest signal = new CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = DamageSourceIds.FaunaLeviathanBite,
                Amount = damage,
                ImpulseMagnitude = math.max(0f, math.isfinite(impulseMagnitude) ? impulseMagnitude : 0f),
                Direction = direction3,
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    CombatDamageTypes.Impact,
                    0u,
                    CombatWeakspotTier.None)
            };

            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = ResolveLeviathanStrikeLocalPoint(targetTransform, safeImpactPoint),
                ArmorNormal = -direction3,
                LocalTemperatureCelsius = 20f,
                StatusDurationSeconds = 0f
            };

            double3 impactAup = double3.zero;
            if (TryResolveAupFromRuntimeOrigin(safeImpactPoint, out AbsoluteUniversePosition impactPointAup) &&
                impactPointAup.IsFinite())
            {
                double3 resolvedAup = impactPointAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolvedAup)))
                    impactAup = resolvedAup;
            }

            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
            return true;
        }

        private static void ApplyLeviathanStrikeOwnerFallbackDamage(
            HectonPlayerHealth playerHealth,
            float damage,
            Vector3 impactPoint)
        {
            if (playerHealth == null || !(damage > 0f) || !math.isfinite(damage))
                return;

            Transform targetTransform = playerHealth.transform;
            Vector3 safeImpactPoint = IsFiniteVector3(impactPoint)
                ? impactPoint
                : targetTransform != null && IsFiniteVector3(targetTransform.position)
                    ? targetTransform.position
                    : Vector3.zero;

            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = damage,
                LocalPoint = ResolveLeviathanStrikeLocalPoint(targetTransform, safeImpactPoint),
                DamageType = CombatDamageTypes.Impact,
                IntegrityDelta = 0,
                Depth = 0f,
                SourceId = DamageSourceIds.FaunaLeviathanBite,
                TraumaLevel = 0
            };
            playerHealth.ReceiveDamage(in packet);
        }

        private static float3 ResolveLeviathanStrikeLocalPoint(Transform targetTransform, Vector3 impactPoint)
        {
            if (targetTransform == null || !IsFiniteVector3(impactPoint))
                return float3.zero;

            Vector3 localPoint = targetTransform.InverseTransformPoint(impactPoint);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            return math.all(math.isfinite(localPoint3)) ? localPoint3 : float3.zero;
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

            var anchors = _mapMagicVegetationBridge.ActiveAbyssalAnchorsNative;
            int anchorCount = _mapMagicVegetationBridge.ActiveAbyssalAnchorCount;
            if (anchors.Length <= 0 || anchorCount <= 0)
                return false;

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return false;

            double nearestDistanceSq = double.PositiveInfinity;
            int cappedCount = math.min(anchorCount, anchors.Length);
            for (int i = 0; i < cappedCount; i++)
            {
                Vector3 candidate = anchors[i];
                if (!TryResolveAupFromRuntimeOrigin(candidate, out AbsoluteUniversePosition candidateAup))
                    continue;

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
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
                return;

            try
            {
                int previousActiveThreatCount = _activeMassiveThreatCount;
                RecalculateMassiveThreatCount(massiveThreats);
                if (previousActiveThreatCount != _activeMassiveThreatCount)
                    UploadMassiveThreats();
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        private void DispatchClearLatchStats()
        {
            if (_latchStatsBuffer == null || boidCompute == null || _clearStatsKernelIndex < 0)
                return;
            if (_clearStatsDispatchGroupCount <= 0)
            {
                DisableComputeDispatch(ComputeDisableReasonDispatchGroupLimit);
                return;
            }

            boidCompute.Dispatch(_clearStatsKernelIndex, _clearStatsDispatchGroupCount, 1, 1);
        }

        private bool ShouldCollectLatchStats(SimulationLodTier simulationLodTier, bool leaderFollowerSchooling, bool shouldRender)
        {
            return enableParasiteLatchGpuReadback &&
                   simulationLodTier != SimulationLodTier.Sleep &&
                   !leaderFollowerSchooling &&
                   _latchStatsBuffer != null &&
                   !_parasiteLatchReadbackPending &&
                   _parasiteLatchReadbackTimer <= 0f &&
                   (shouldRender || _parasiteModeActive);
        }

        private float ResolveLatchStatsReadbackInterval(float hibernation01)
        {
            float baseInterval = math.max(0.05f, parasiteLatchReadbackInterval);
            float hibernation = math.saturate(hibernation01);
            return baseInterval * math.lerp(1f, 4f, hibernation * hibernation);
        }

        private void UpdateSpatialGridLayout()
        {
            double baseCellSizeD = 2.0d;
            double3 doubledExtentsD = new double3(_fieldExtents.x * 2.0f, _fieldExtents.y * 2.0f, _fieldExtents.z * 2.0f);
            double3 fieldSizeD = new double3(
                math.max(doubledExtentsD.x, baseCellSizeD),
                math.max(doubledExtentsD.y, baseCellSizeD),
                math.max(doubledExtentsD.z, baseCellSizeD));
            double axisClampCellSizeD = math.max(
                fieldSizeD.x / SpatialGridMaxAxisResolution,
                math.max(fieldSizeD.y / SpatialGridMaxAxisResolution, fieldSizeD.z / SpatialGridMaxAxisResolution));
            double cellSizeD = math.max(baseCellSizeD, axisClampCellSizeD);
            _spatialGridCellSizeWS = (float)cellSizeD;

            double3 centerD = new double3(_fieldCenter.x, _fieldCenter.y, _fieldCenter.z);
            double3 extentsD = new double3(_fieldExtents.x, _fieldExtents.y, _fieldExtents.z);
            double3 fieldMinD = centerD - extentsD;
            double3 fieldMaxD = centerD + extentsD;

            double originXD = FloorToMultiple64(fieldMinD.x, cellSizeD);
            double originYD = FloorToMultiple64(fieldMinD.y, cellSizeD);
            double originZD = FloorToMultiple64(fieldMinD.z, cellSizeD);
            _spatialGridOriginWSD = new double3(originXD, originYD, originZD);

            int resolutionX = math.clamp((int)math.ceil((fieldMaxD.x - originXD) / cellSizeD), 1, SpatialGridMaxAxisResolution);
            int resolutionY = math.clamp((int)math.ceil((fieldMaxD.y - originYD) / cellSizeD), 1, SpatialGridMaxAxisResolution);
            int resolutionZ = math.clamp((int)math.ceil((fieldMaxD.z - originZD) / cellSizeD), 1, SpatialGridMaxAxisResolution);
            _spatialGridResolution = new Vector3Int(resolutionX, resolutionY, resolutionZ);
            int cellCount = resolutionX * resolutionY * resolutionZ;
            _clearSpatialGridDispatchGroupCount = CeilDivPositive(cellCount, (int)_clearSpatialGridThreadGroupSizeX);
        }

        private void DispatchClearSpatialGrid()
        {
            if (boidCompute == null || _clearSpatialGridKernelIndex < 0 || _spatialGridCountBuffer == null || _clearSpatialGridDispatchGroupCount <= 0)
                return;

            boidCompute.Dispatch(_clearSpatialGridKernelIndex, _clearSpatialGridDispatchGroupCount, 1, 1);
        }

        private void DispatchClearPbdCorrections()
        {
            if (boidCompute == null || _clearPbdCorrectionsKernelIndex < 0 || _pbdCorrectionBuffer == null || _clearPbdCorrectionsDispatchGroupCount <= 0)
                return;

            boidCompute.Dispatch(_clearPbdCorrectionsKernelIndex, _clearPbdCorrectionsDispatchGroupCount, 1, 1);
        }

        private float RefreshCameraDistanceSq()
        {
            if (!RefreshApproxViewPoseCache(out Vector3 cameraPosition, out _))
                return 0f;

            if (!TryResolveAupFromRuntimeOrigin(_renderBounds.center, out AbsoluteUniversePosition boundsAup) ||
                !TryResolveAupFromRuntimeOrigin(cameraPosition, out AbsoluteUniversePosition cameraAup))
            {
                return 0f;
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in boundsAup, in cameraAup);
            if (!double.IsFinite(distanceSq) || distanceSq <= 0d)
                return 0f;

            return distanceSq >= float.MaxValue ? float.MaxValue : (float)distanceSq;
        }

        private bool ConsumeSimulationStep(
            float frameDeltaTime,
            float cameraDistanceSq,
            out float simulationDeltaTime,
            out float hibernation01,
            out SimulationLodTier simulationLodTier)
        {
            FoveatedSimulationDecision decision = default;
            if (TryReadOnlySargassumVaultArray(
                    in _foveatedSimulationFrontHandle,
                    BufferID.SargassumFoveatedSimulationFront,
                    1,
                    out NativeArray<FoveatedSimulationDecision>.ReadOnly foveatedSimulationFront))
            {
                decision = foveatedSimulationFront[0];
            }

            float safeMaxStepSeconds = ClampFinite(hibernationMaxStepSeconds, 1f / 60f, 0.5f);
            simulationDeltaTime = ClampFinite(decision.SimulationDeltaTime, 0f, safeMaxStepSeconds);
            hibernation01 = SaturateFinite01(decision.Hibernation01);
            simulationLodTier = (SimulationLodTier)math.clamp(decision.Tier, (int)SimulationLodTier.Full, (int)SimulationLodTier.Sleep);
            _sleepVelocityWritePending = simulationLodTier == SimulationLodTier.Sleep && _lastSimulationLodTier != SimulationLodTier.Sleep;
            _lastSimulationLodTier = simulationLodTier;
            _lastSimulationHibernation01 = hibernation01;
            float safePreviousAccumulator = ClampFinite(decision.Accumulator, 0f, safeMaxStepSeconds);
            ScheduleFoveatedSimulationDecision(frameDeltaTime, cameraDistanceSq, safePreviousAccumulator);
            return decision.DispatchSimulation != 0 && simulationDeltaTime > 0f;
        }

        private bool ShouldRenderSwarm(float cameraDistanceSq)
        {
            if (_activeBoidCount <= 0)
                return false;

            if (!RefreshApproxViewPoseCache(out _, out _))
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
            if (!RefreshApproxViewPoseCache(out Vector3 cameraPosition, out Vector3 cameraForward))
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

        private bool RefreshApproxViewPoseCache(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_viewPoseCacheFrame == currentFrame)
            {
                cameraPosition = _viewPoseCachePosition;
                cameraForward = _viewPoseCacheForward;
                return _viewPoseCacheValid;
            }

            _viewPoseCacheFrame = currentFrame;
            _viewPoseCacheValid = BuildApproxViewPoseUncached(out _viewPoseCachePosition, out _viewPoseCacheForward);
            cameraPosition = _viewPoseCachePosition;
            cameraForward = _viewPoseCacheForward;
            return _viewPoseCacheValid;
        }

        private bool BuildApproxViewPoseUncached(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            if (RefreshPlayerRuntimeSnapshotCache(
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

        private bool RefreshPlayerRuntimeSnapshotCache(
            out PlayerMovementRuntimeState movementState,
            out PlayerLookState lookState)
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_playerRuntimeSnapshotCacheFrame != currentFrame)
            {
                _playerRuntimeSnapshotCacheFrame = currentFrame;
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                bool hasMovement = playerContext != null &&
                                   playerContext.TryGetMovementRuntimeState(out _playerRuntimeSnapshotMovement);
                bool hasLook = playerContext != null &&
                               playerContext.TryGetLookRuntimeState(out _playerRuntimeSnapshotLook);
                if (hasMovement || hasLook)
                {
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
            if (!TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
                return;

            try
            {
                RecalculateMassiveThreatCount(massiveThreats);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _massiveThreatsHandle);
            }
        }

        private void RecalculateMassiveThreatCount(NativeArray<MassiveThreatData> massiveThreats)
        {
            _activeMassiveThreatCount = 0;
            int threatCapacity = massiveThreats.IsCreated ? math.min(maxMassiveThreatCount, massiveThreats.Length) : 0;
            if (threatCapacity <= 0)
            {
                _debugMassiveThreatCount = 0;
                return;
            }

            float absoluteSimulationTime = GetAbsoluteSimulationTime();
            int writeIndex = 0;
            for (int i = 0; i < threatCapacity; i++)
            {
                MassiveThreatData threat = massiveThreats[i];
                if (!TrySanitizeActiveMassiveThreat(in threat, absoluteSimulationTime, out MassiveThreatData safeThreat))
                    continue;

                if (writeIndex != i)
                    massiveThreats[writeIndex] = safeThreat;
                else
                    massiveThreats[i] = safeThreat;

                writeIndex++;
            }

            for (int i = writeIndex; i < threatCapacity; i++)
                massiveThreats[i] = default;

            _activeMassiveThreatCount = writeIndex;
            _debugMassiveThreatCount = _activeMassiveThreatCount;
        }

        private static bool TrySanitizeActiveMassiveThreat(
            in MassiveThreatData source,
            float absoluteSimulationTime,
            out MassiveThreatData safeThreat)
        {
            safeThreat = default;
            if (!float.IsFinite(absoluteSimulationTime) ||
                !IsFiniteVector3(source.Position) ||
                !float.IsFinite(source.InnerRadius) ||
                !float.IsFinite(source.PanicRadius) ||
                !float.IsFinite(source.Strength) ||
                !float.IsFinite(source.EndTime))
            {
                return false;
            }

            float remainingSeconds = source.EndTime - absoluteSimulationTime;
            if (!float.IsFinite(remainingSeconds) || remainingSeconds <= 0.001f)
                return false;

            float strength01 = SaturateFinite01(source.Strength);
            if (strength01 <= 0.0001f)
                return false;

            float innerRadius = ClampFinite(source.InnerRadius, 0.5f, MassiveThreatMaxRadiusMeters);
            float panicRadius = ClampFinite(source.PanicRadius, innerRadius, MassiveThreatMaxRadiusMeters);
            safeThreat = source;
            safeThreat.InnerRadius = innerRadius;
            safeThreat.PanicRadius = panicRadius;
            safeThreat.Strength = strength01;
            safeThreat.EndTime = absoluteSimulationTime + ClampFinite(remainingSeconds, 0.001f, MassiveThreatMaxDurationSeconds);
            safeThreat.DirectionWS = FastNormalizeVector3(source.DirectionWS, Vector3.forward);
            return true;
        }

        private void RefreshRenderScaleCache()
        {
            _cachedVatSwayAmplitudeScale = MigrationDirector.ResolveVatSwayAmplitudeScale();
        }

        private void RefreshRenderLayerCache()
        {
            _cachedRenderLayer = gameObject.layer;
        }

        private bool TryEnsureBoidIndirectArgsBufferCold()
        {
            if (_boidIndirectArgsBuffer != null)
                return false;

            _boidIndirectArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - VAT micro-fauna indirect draw args - owner: SargassumMicroFaunaBoids
            _boidIndirectArgsMesh = null;
            _boidIndirectArgsInstanceCount = -1;
            return _boidIndirectArgsBuffer != null;
        }

        private bool HasBoidIndirectArgsBufferReady()
        {
            return _boidIndirectArgsBuffer != null;
        }

        private bool UploadBoidIndirectArgs(Mesh mesh, int instanceCount)
        {
            if (mesh == null || instanceCount <= 0 || !HasBoidIndirectArgsBufferReady())
                return false;

            if (_boidIndirectArgsMesh == mesh && _boidIndirectArgsInstanceCount == instanceCount)
                return true;

            var mappedArgs =
                _boidIndirectArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                mappedArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(0),
                    instanceCount = (uint)instanceCount,
                    startIndex = mesh.GetIndexStart(0),
                    baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                    startInstance = 0u
                };
            }
            finally
            {
                _boidIndirectArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _boidIndirectArgsMesh = mesh;
            _boidIndirectArgsInstanceCount = instanceCount;
            return true;
        }

        private void BindBoidMaterialProperties(Material renderMaterial, GraphicsBuffer currentBuffer, bool vatEnabled)
        {
            float parasiteMode = _parasiteModeActive ? 1f : 0f;
            float parasiteAggression = _debugParasiteAggression01;
            float velocitySleepScale = _debugHibernation01 >= 0.999f ? 0f : 1f;
            float lodDitherKeep01 = ResolveLodDitherKeep01(_debugHibernation01);
            float vatEnabledFloat = vatEnabled ? 1f : 0f;
            float vatFrameCount = vatEnabled ? boidVatFrameCount : 1f;
            float vatVertexCount = _boidMeshVertexCount;
            float vatPositionScale = boidVatPositionScale * _cachedVatSwayAmplitudeScale;
            float hitFlashInvRadiusSq = _hitFlashRuntimeRadius > 0.0001f ? 1f / (_hitFlashRuntimeRadius * _hitFlashRuntimeRadius) : 0f;

            renderMaterial.SetBuffer(_BoidsBufferId, currentBuffer);
            renderMaterial.SetFloat(_ParasiteModeId, parasiteMode);
            renderMaterial.SetFloat(_ParasiteAggressionId, parasiteAggression);
            renderMaterial.SetFloat(_VelocitySleepScaleId, velocitySleepScale);
            renderMaterial.SetFloat(_LodDitherKeep01Id, lodDitherKeep01);
            renderMaterial.SetFloat(_VatEnabledId, vatEnabledFloat);
            renderMaterial.SetFloat(_VatFrameCountId, vatFrameCount);
            renderMaterial.SetFloat(_VatVertexCountId, vatVertexCount);
            renderMaterial.SetFloat(_VatPlaybackSpeedId, boidVatPlaybackSpeed);
            renderMaterial.SetFloat(_VatInstancePhaseScaleId, boidVatInstancePhaseScale);
            renderMaterial.SetFloat(_VatPositionScaleId, vatPositionScale);
            renderMaterial.SetFloat(_VatNormalBlendId, boidVatNormalBlend);
            renderMaterial.SetFloat(_SimulationInterpolationAlphaId, _simulationInterpolationAlpha);
            renderMaterial.SetFloat(_HitFlashStartTimeId, _hitFlashStartTime);
            renderMaterial.SetFloat(_HitFlashDurationId, hitFlashDurationSeconds);
            renderMaterial.SetFloat(_HitFlashIntensityId, _hitFlashRuntimeIntensity);
            renderMaterial.SetFloat(_HitFlashRadiusId, _hitFlashRuntimeRadius);
            renderMaterial.SetFloat(_HitFlashBloatId, hitFlashBloatMeters);
            renderMaterial.SetVector(_HitFlashOriginWSId, new Vector4(_hitFlashOriginWS.x, _hitFlashOriginWS.y, _hitFlashOriginWS.z, hitFlashInvRadiusSq));
            renderMaterial.SetColor(_HitFlashColorId, hitFlashColor);

            if (vatEnabled)
            {
                renderMaterial.SetTexture(_VatPositionTexId, boidVatPositionTexture);
                renderMaterial.SetTexture(_VatNormalTexId, boidVatNormalTexture);
            }

        }

        private void RenderCurrentBuffer()
        {
            GraphicsBuffer currentBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            Material renderMaterial = boidMaterial;
            if (_activeBoidCount <= 0 ||
                currentBuffer == null ||
                boidMesh == null ||
                renderMaterial == null ||
                !EnsureBoidMaterialBindingReady())
            {
                return;
            }

            bool vatEnabled = boidVatPositionTexture != null &&
                              boidVatNormalTexture != null &&
                              boidVatFrameCount > 1;
            BindBoidMaterialProperties(renderMaterial, currentBuffer, vatEnabled);

            if (!UploadBoidIndirectArgs(boidMesh, _activeBoidCount))
                return;

            int targetLayer = useGameObjectLayer ? _cachedRenderLayer : 0;
            RenderParams renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = _renderBounds,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                layer = targetLayer,
                lightProbeUsage = LightProbeUsage.Off
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, boidMesh, _boidIndirectArgsBuffer, 1, 0);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || !_serviceRegistered)
                return;

            TryRegisterHotSwapListener();

            if (GlobalRegistry.Dispatcher == null)
                return;

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
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterSargassumMicroFaunaRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumMicroFauna, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;

            if (ReferenceEquals(GlobalRegistry.SargassumMicroFauna, this))
                GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SargassumMicroFaunaBoids active = s_activeRuntimeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsSargassumMicroFaunaRuntimeUsable(active))
                {
                    LogDuplicateRuntimeOwnerDetected(active);
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;

                if (ReferenceEquals(GlobalRegistry.SargassumMicroFauna, active))
                    GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(active);
            }

            SargassumMicroFaunaBoids registered = GlobalRegistry.SargassumMicroFauna;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsSargassumMicroFaunaRuntimeUsable(registered))
            {
                s_activeRuntimeInstance = registered;
                LogDuplicateRuntimeOwnerDetected(registered);
                Destroy(gameObject);
                return true;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, registered))
                s_activeRuntimeInstance = null;

            GlobalRegistry.UnregisterSargassumMicroFaunaRuntime(registered);
            return false;
        }

        private static bool IsSargassumMicroFaunaRuntimeUsable(SargassumMicroFaunaBoids runtime)
        {
            return runtime != null && runtime._serviceRegistered && runtime.isActiveAndEnabled;
        }

        private void ReconcileRuntimeOwnerFromRegistryReplacement(object previousService, object currentService)
        {
            if (currentService is SargassumMicroFaunaBoids currentRuntime)
            {
                s_activeRuntimeInstance = currentRuntime;
                bool ownsRuntime = ReferenceEquals(currentRuntime, this);
                _serviceRegistered = ownsRuntime;
                if (ownsRuntime)
                {
                    if (_runtimeRoutesRetiredAfterOwnershipLoss)
                        RestoreRuntimeRoutesAfterOwnershipGain();
                    return;
                }

                if (ReferenceEquals(previousService, this))
                    RetireRuntimeRoutesAfterOwnershipLoss();
                return;
            }

            if (ReferenceEquals(previousService, this))
            {
                _serviceRegistered = false;
                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
                RetireRuntimeRoutesAfterOwnershipLoss();
            }
        }

        private void RetireRuntimeRoutesAfterOwnershipLoss()
        {
            if (_runtimeRoutesRetiredAfterOwnershipLoss)
                return;

            SargassumGlobalDragManager.Unregister(this);
            FlashlightEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            HectonFloatingOrigin.UnregisterListener(this);

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

            _runtimeRoutesRetiredAfterOwnershipLoss = true;
        }

        private void RestoreRuntimeRoutesAfterOwnershipGain()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            RefreshColdRegistryDependencies();
            RefreshDependencies();
            SargassumGlobalDragManager.Register(this);
            FlashlightEvents.Register(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
        }

        private void LogDuplicateRuntimeOwnerDetected(SargassumMicroFaunaBoids keptRuntime)
        {
            if (s_duplicateRuntimeOwnerLogged)
                return;

            s_duplicateRuntimeOwnerLogged = true;
            string keptName = keptRuntime != null ? keptRuntime.name : "<null>";
            H8Debug.LogError(
                "[SargassumMicroFaunaBoids] Duplicate runtime owner detected. Keeping " +
                keptName +
                " and destroying duplicate " +
                name +
                " before service/tick registration.",
                this);
        }

        private void TryUnregister()
        {

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

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (!Application.isPlaying || _registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void ReleaseBuffers(bool keepLatchStatsBuffer = false)
        {
            ReleaseBuffer(ref _boidsBufferA);
            ReleaseBuffer(ref _boidsBufferB);
            ReleaseBuffer(ref _boidIndirectArgsBuffer);
            ReleaseBuffer(ref _grazingAnchorBuffer);
            ReleaseBuffer(ref _massiveThreatBuffer);
            ReleaseBuffer(ref _formationBeaconBuffer);
            ReleaseBuffer(ref _formationObstacleBuffer);
            ReleaseBuffer(ref _leviathanNodeBuffer);
            if (!keepLatchStatsBuffer)
            {
                ReleaseBuffer(ref _latchStatsBuffer);
                _parasiteLatchHeldStatsBuffer = null;
            }
            else
            {
                _latchStatsBuffer = null;
            }

            ReleaseBuffer(ref _pbdCorrectionBuffer);
            ReleaseBuffer(ref _threatGridBuffer);
            _threatGridUploadSnapshot = null;
            ReleaseBuffer(ref _threatVoxelBuffer);
            ReleaseBuffer(ref _spatialGridCountBuffer);
            ReleaseBuffer(ref _spatialGridCellBuffer);
            ReleaseBuffer(ref _simulationFrameBuffer);
            ReleaseBuffer(ref _predatorAupFallbackBuffer);
            ReleaseBuffer(ref _boidSensoryThreatBufferA);
            ReleaseBuffer(ref _boidSensoryThreatBufferB);
            ResetBoidSensoryThreatUploadCache();
            _latchStatsBufferRawTarget = false;
            _pbdCorrectionBufferRawTarget = false;
            _spatialGridCountBufferRawTarget = false;
            _computeStaticBuffersBound = false;
            _boidIndirectArgsMesh = null;
            _boidIndirectArgsInstanceCount = -1;
            _foodChainTelemetryCursor = 0;
            _foodChainTelemetryDumped = false;
            _foodChainTelemetryDumpSourceUnavailableLogged = false;
            _foodChainTelemetryDumpFailureLogged = false;
            _boidSensoryBlackBoxCursor = 0;
            _boidSensoryBlackBoxDumped = false;
            _boidSensoryBlackBoxDumpSourceUnavailableLogged = false;
            _boidSensoryBlackBoxDumpFailureLogged = false;
            if (_fallbackAbyssalFlowTexture != null)
            {
                _fallbackAbyssalFlowTexture = null;
                _boundAbyssalFlowTexture = null;
            }
        }

        private void CompletePendingReadbackAndReleaseBuffers()
        {
            CompletePendingPredatorConsumption(forceComplete: true);
            JobHandle disposeDependency = CancelPendingLeviathanNodeBuildForDispose();
            bool keepLatchStatsBuffer = _parasiteLatchReadbackDisposeAfterCompletion &&
                                        _parasiteLatchReleaseStatsBufferAfterCompletion;
            if (_parasiteLatchReadbackPending)
            {
                if (_parasiteLatchReadbackRequest.done)
                {
                    _parasiteLatchReadbackPending = false;
                    _parasiteLatchReadbackRequest = default;
                }
                else
                {
                    _parasiteLatchReadbackDisposeAfterCompletion = true;
                    _parasiteLatchReleaseStatsBufferAfterCompletion = _latchStatsBuffer != null;
                    _parasiteLatchHeldStatsBuffer = _latchStatsBuffer;
                    _parasiteLatchReadbackPending = false;
                    keepLatchStatsBuffer = _parasiteLatchReleaseStatsBufferAfterCompletion;
                }
            }

            _parasiteLatchReadbackTimer = 0f;
            DisposeParasiteLatchReadbackData();
            ReleaseBuffers(keepLatchStatsBuffer);
            ResetComputeKernelBindings();
            _boundBoidCompute = null;
            ResetThreatGridSnapshot();
            ResetThreatVoxelSnapshot();
            ClearVaultHandles(disposeDependency);

            _feedingFrenzyWindowStartTime = -1f;
            _feedingFrenzyKillCount = 0;
            _pendingPredatorConsumptionTimeSeconds = 0f;
            _debugConsumedBoidCount = 0;
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle CancelPendingLeviathanNodeBuildForDispose()
        {
            ClearLeviathanSnapshot();
            return default;
        }

        private void ClearVaultHandles(JobHandle disposeDependency)
        {
            TryCompleteSargassumJobInPostSimulationWindow(ref disposeDependency, forceComplete: true);
            _grazingAnchorsHandle = default;
            _massiveThreatsHandle = default;
            _formationBeaconsHandle = default;
            _formationObstaclesHandle = default;
            _staticObstacleCacheHandle = default;
            _boidStateHandle = default;
            _killSignalHandle = default;
            _killSignalCountHandle = default;
            _foodChainTelemetryRingHandle = default;
            _leviathanPathScratchHandle = default;
            _leviathanNodeFrontHandle = default;
            _leviathanNodeBackHandle = default;
            _leviathanNodeCountHandle = default;
            _simulationFrameHandle = default;
            _boidSensoryThreatsHandle = default;
            _boidSensoryBlackBoxHandle = default;
            _threatGridUploadHandle = default;
            DisposeFoveatedSimulationBuffers(disposeDependency);
            _inactiveStatisticalSwarmRing.Dispose(disposeDependency);
            _inactiveStatisticalSwarmCenterRing.Dispose(disposeDependency);
        }

        private void PrimeFoveatedSimulationDecision(float frameDeltaTime, float cameraDistanceSq)
        {
            PopulateFoveatedSimulationInput(frameDeltaTime, cameraDistanceSq, previousAccumulator: 0f);
            VaultGenerationHandle<FoveatedSimulationDecision> foveatedSimulationBackHandle = _foveatedSimulationBackHandle;
            if (!TryReadOnlySargassumVaultArray(
                    in _foveatedSimulationInputHandle,
                    BufferID.SargassumFoveatedSimulationInput,
                    1,
                    out NativeArray<FoveatedSimulationInput>.ReadOnly foveatedSimulationInput))
            {
                return;
            }

            if (!TryAcquireSargassumWriteLock(
                    in foveatedSimulationBackHandle,
                    BufferID.SargassumFoveatedSimulationBack,
                    1,
                    out NativeArray<FoveatedSimulationDecision> foveatedSimulationBack))
            {
                return;
            }

            try
            {
                FoveatedSimulationInput input = foveatedSimulationInput[0];
                FoveatedSimulationDecision decision = EvaluateFoveatedSimulationDecision(in input);
                foveatedSimulationBack[0] = decision;
                _lastSimulationHibernation01 = SaturateFinite01(decision.Hibernation01);
                (_foveatedSimulationFrontHandle, _foveatedSimulationBackHandle) = (_foveatedSimulationBackHandle, _foveatedSimulationFrontHandle);
                _foveatedSimulationHandle = default;
                _foveatedSimulationScheduled = false;
            }
            finally
            {
                ReleaseSargassumWriteLock(in foveatedSimulationBackHandle);
            }
        }

        private void PopulateFoveatedSimulationInput(float frameDeltaTime, float cameraDistanceSq, float previousAccumulator)
        {
            if (!TryAcquireSargassumWriteLock(
                    in _foveatedSimulationInputHandle,
                    BufferID.SargassumFoveatedSimulationInput,
                    1,
                    out NativeArray<FoveatedSimulationInput> foveatedSimulationInput))
                return;

            try
            {
                foveatedSimulationInput[0] = new FoveatedSimulationInput
                {
                    FrameDeltaTime = ClampMinFinite(frameDeltaTime, 0f),
                    CameraDistanceSq = ClampMinFinite(cameraDistanceSq, 0f),
                    FullDistanceMeters = ClampFinite(hibernationStartDistance, FullSimulationDistanceMeters, SleepSimulationDistanceMeters),
                    SleepDistanceMeters = ClampFinite(simulationCullDistance, FullSimulationDistanceMeters + 0.01f, SleepSimulationDistanceMeters * 4f),
                    MaxStepSeconds = ClampFinite(hibernationMaxStepSeconds, 1f / 60f, 0.5f),
                    MinTimeScale = ClampFinite(hibernationMinTimeScale, 0.1f, 1f),
                    PreviousAccumulator = ClampFinite(previousAccumulator, 0f, 0.5f),
                    PreviousTier = (int)_lastSimulationLodTier
                };
            }
            finally
            {
                ReleaseSargassumWriteLock(in _foveatedSimulationInputHandle);
            }
        }

        private void ScheduleFoveatedSimulationDecision(float frameDeltaTime, float cameraDistanceSq, float previousAccumulator)
        {
            if (_foveatedSimulationScheduled)
                return;

            PopulateFoveatedSimulationInput(frameDeltaTime, cameraDistanceSq, previousAccumulator);
            VaultGenerationHandle<FoveatedSimulationDecision> foveatedSimulationBackHandle = _foveatedSimulationBackHandle;
            if (!TryReadOnlySargassumVaultArray(
                    in _foveatedSimulationInputHandle,
                    BufferID.SargassumFoveatedSimulationInput,
                    1,
                    out NativeArray<FoveatedSimulationInput>.ReadOnly foveatedSimulationInput))
            {
                return;
            }

            if (!TryAcquireSargassumWriteLock(
                    in foveatedSimulationBackHandle,
                    BufferID.SargassumFoveatedSimulationBack,
                    1,
                    out NativeArray<FoveatedSimulationDecision> foveatedSimulationBack))
            {
                return;
            }

            try
            {
                FoveatedSimulationInput input = foveatedSimulationInput[0];
                foveatedSimulationBack[0] = EvaluateFoveatedSimulationDecision(in input);
                (_foveatedSimulationFrontHandle, _foveatedSimulationBackHandle) = (_foveatedSimulationBackHandle, _foveatedSimulationFrontHandle);
                _foveatedSimulationHandle = default;
                _foveatedSimulationScheduled = false;
            }
            finally
            {
                ReleaseSargassumWriteLock(in foveatedSimulationBackHandle);
            }
        }

        private static bool TryCompleteSargassumJobInPostSimulationWindow(ref JobHandle handle, bool forceComplete)
        {
            if (!forceComplete)
                return DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private void CompletePendingFoveatedSimulationDecision(bool forceComplete)
        {
            if (!_foveatedSimulationScheduled)
                return;

            if (!TryCompleteSargassumJobInPostSimulationWindow(ref _foveatedSimulationHandle, forceComplete))
                return;

            _foveatedSimulationScheduled = false;
            (_foveatedSimulationFrontHandle, _foveatedSimulationBackHandle) = (_foveatedSimulationBackHandle, _foveatedSimulationFrontHandle);
        }

        private void DisposeFoveatedSimulationBuffers(JobHandle externalDependency)
        {
            JobHandle disposeDependency = externalDependency;
            if (_foveatedSimulationScheduled)
            {
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _foveatedSimulationHandle);
                _foveatedSimulationScheduled = false;
            }

            TryCompleteSargassumJobInPostSimulationWindow(ref disposeDependency, forceComplete: true);
            _foveatedSimulationInputHandle = default;
            _foveatedSimulationFrontHandle = default;
            _foveatedSimulationBackHandle = default;
            _foveatedSimulationHandle = default;
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
            if (math.isfinite(lengthL1) && lengthL1 > 0.0001f)
            {
                float invLength = math.rcp(lengthL1);
                return new Vector3(value.x * invLength, value.y * invLength, value.z * invLength);
            }

            float fallbackLengthL1 = math.abs(fallback.x) + math.abs(fallback.y) + math.abs(fallback.z);
            if (math.isfinite(fallbackLengthL1) && fallbackLengthL1 > 0.0001f)
            {
                float invFallbackLength = math.rcp(fallbackLengthL1);
                return new Vector3(
                    fallback.x * invFallbackLength,
                    fallback.y * invFallbackLength,
                    fallback.z * invFallbackLength);
            }

            return Vector3.zero;
        }

        private static float SaturateFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ClampFinite(float value, float min, float max)
        {
            float safeMin = math.isfinite(min) ? min : 0f;
            float safeMax = math.isfinite(max) ? max : safeMin;
            if (safeMax < safeMin)
                safeMax = safeMin;

            return math.isfinite(value) ? math.clamp(value, safeMin, safeMax) : safeMin;
        }

        private static float ClampMinFinite(float value, float min)
        {
            float safeMin = math.isfinite(min) ? min : 0f;
            return math.isfinite(value) && value > safeMin ? value : safeMin;
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
            if (math.isfinite(lengthL1) && lengthL1 > 0.0001f)
                return value * math.rcp(lengthL1);

            float fallbackLengthL1 = math.abs(fallback.x) + math.abs(fallback.y) + math.abs(fallback.z);
            return math.isfinite(fallbackLengthL1) && fallbackLengthL1 > 0.0001f
                ? fallback * math.rcp(fallbackLengthL1)
                : float3.zero;
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
            if (numerator <= 0 || denominator <= 0)
                return 0;

            long groups = ((long)numerator + denominator - 1L) / denominator;
            return groups <= PortableMaxDispatchGroupsPerDimension ? (int)groups : 0;
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

        private static double FloorToMultiple64(double value, double multiple)
        {
            double safeMultiple = math.max(0.0001d, multiple);
            return math.floor(value / safeMultiple) * safeMultiple;
        }

        private static float FloorToMultiple(float value, float multiple)
        {
            float safeMultiple = math.max(0.0001f, multiple);
            return math.floor(value / safeMultiple) * safeMultiple;
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(uint boidSeed, uint salt)
        {
            uint state = math.max(1u, boidSeed ^ salt ^ HashSeed);
            return new Unity.Mathematics.Random(state);
        }

        private static float HashToFloat01(uint index, uint iteration, uint salt)
        {
            uint hash = (index + 1u) * 374761393u + iteration * 668265263u + salt + HashSeed;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
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

        private void UploadActiveLeviathanSnapshot()
        {
            _activeLeviathanUploadRequested = true;
        }

        private void UploadActiveLeviathanSnapshotVisualSync()
        {
            if (_leviathanNodeBuffer == null || _leviathanPathNodeCount <= 0)
                return;

            if (!TryAcquireSargassumWriteLock(
                    in _leviathanNodeFrontHandle,
                    BufferID.SargassumLeviathanNodeFront,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData> leviathanNodeFront))
                return;

            try
            {
                int safeCount = math.clamp(_leviathanPathNodeCount, 0, leviathanNodeFront.Length);
                if (safeCount <= 0)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_leviathanNodeBuffer, leviathanNodeFront, safeCount);
            }
            finally
            {
                ReleaseSargassumWriteLock(in _leviathanNodeFrontHandle);
            }
        }

        private void FlushQueuedMicroFaunaGpuUploads()
        {
            if (_originShiftGpuDispatchRequested)
            {
                Vector3 runtimeOffset = _queuedOriginShiftGpuDelta;
                _queuedOriginShiftGpuDelta = Vector3.zero;
                _originShiftGpuDispatchRequested = false;
                DispatchOriginShiftToLiveBoidBuffersVisualSync(runtimeOffset);
            }

            if (_spawnBufferUploadRequested)
            {
                int uploadCount = _queuedSpawnBufferUploadCount;
                _queuedSpawnBufferUploadCount = 0;
                _spawnBufferUploadRequested = false;
                UploadSpawnDataToBoidBuffersVisualSync(uploadCount);
            }

            if (_grazingAnchorUploadRequested)
            {
                _grazingAnchorUploadRequested = false;
                UploadGrazingAnchorsVisualSync();
            }

            if (_massiveThreatUploadRequested)
            {
                _massiveThreatUploadRequested = false;
                UploadMassiveThreatsVisualSync();
            }

            if (_formationBeaconUploadRequested)
            {
                _formationBeaconUploadRequested = false;
                UploadFormationBeaconsVisualSync();
            }

            if (_formationObstacleUploadRequested)
            {
                _formationObstacleUploadRequested = false;
                UploadFormationObstaclesVisualSync();
            }

            if (_activeLeviathanUploadRequested)
            {
                _activeLeviathanUploadRequested = false;
                UploadActiveLeviathanSnapshotVisualSync();
            }
        }

        private void ClearLeviathanSnapshot()
        {
            _leviathanPathNodeCount = 0;
            _debugLeviathanNodeCount = 0;
        }

        private GraphicsBuffer ResolveBoidSensoryThreatWriteBuffer()
        {
            return (_frameParity & 1) == 0
                ? _boidSensoryThreatBufferA
                : _boidSensoryThreatBufferB;
        }

        private GraphicsBuffer ResolveBoidSensoryThreatReadBuffer()
        {
            return ResolveBoidSensoryThreatWriteBuffer();
        }

        private static uint HashBoidSensoryThreatUpload(NativeArray<float4> boidSensoryThreats)
        {
            uint hash = 0xB01D5EEDu;
            if (!boidSensoryThreats.IsCreated)
                return hash;

            int count = math.min(boidSensoryThreats.Length, PredatorAupBufferCapacity);
            for (int i = 0; i < count; i++)
            {
                uint slotHash = math.hash(math.asuint(boidSensoryThreats[i]));
                hash = math.hash(new uint4(hash, slotHash, unchecked((uint)i), 0x9E3779B9u));
            }

            return hash;
        }

        private bool MarkBoidSensoryThreatUploadDirty(uint uploadHash)
        {
            if ((_frameParity & 1) == 0)
            {
                if (_boidSensoryThreatUploadValidA && _boidSensoryThreatUploadHashA == uploadHash)
                    return false;

                _boidSensoryThreatUploadHashA = uploadHash;
                _boidSensoryThreatUploadValidA = true;
                return true;
            }

            if (_boidSensoryThreatUploadValidB && _boidSensoryThreatUploadHashB == uploadHash)
                return false;

            _boidSensoryThreatUploadHashB = uploadHash;
            _boidSensoryThreatUploadValidB = true;
            return true;
        }

        private void ResetBoidSensoryThreatUploadCache()
        {
            _boidSensoryThreatUploadHashA = 0u;
            _boidSensoryThreatUploadHashB = 0u;
            _boidSensoryThreatUploadValidA = false;
            _boidSensoryThreatUploadValidB = false;
        }

        private NativeArray<T> ResolveSargassumVaultArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryResolveSargassumVaultArray(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static void EnsureSargassumVaultGenerationHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            if (!TryEnsureSargassumVaultArray(
                    vault,
                    ref handle,
                    bufferId,
                    requiredLength,
                    NativeArrayOptions.ClearMemory,
                    out _))
            {
                handle = default;
            }
        }

        private static bool TryEnsureSargassumVaultArray<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!TryResolveSargassumVaultArray(vault, in handle, bufferId, requiredLength, out buffer))
            {
                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.WorldSargassum,
                    options);
            }

            if (!TryResolveSargassumVaultArray(vault, in handle, bufferId, requiredLength, out buffer))
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryResolveSargassumVaultArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !IsSargassumVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireSargassumWriteLock<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                requiredLength <= 0 ||
                !IsSargassumVaultHandle(in handle, bufferId))
            {
                return false;
            }

            bool acquired = false;
            bool keepLock = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.WorldSargassum, out buffer))
                {
                    buffer = default;
                    return false;
                }

                acquired = true;
                if (!buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                keepLock = true;
                return true;
            }
            finally
            {
                if (acquired && !keepLock)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.WorldSargassum);
                    buffer = default;
                }
            }
        }

        private void ReleaseSargassumWriteLock<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null &&
                IsSargassumVaultHandle(in handle, (BufferID)handle.BufferID))
            {
                vault.ReleaseWriteLock(in handle, SystemID.WorldSargassum);
            }
        }

        private bool TryReadOnlySargassumVaultArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            return TryReadOnlySargassumVaultArray(_dataVault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryReadOnlySargassumVaultArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !IsSargassumVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsSargassumVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.WorldSargassum &&
                   handle.Generation != 0u;
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

            if (UnsafeUtility.SizeOf<BoidSensoryBlackBoxEntry>() != BoidSensoryBlackBoxEntrySizeBytes)
            {
                DisableComputeDispatch(ComputeDisableReasonAncillaryLayoutMismatch);
                return false;
            }

            if (UnsafeUtility.SizeOf<BoidKillSignal>() != BoidKillSignalSizeBytes ||
                UnsafeUtility.SizeOf<FoodChainTelemetryEntry>() != FoodChainTelemetryEntrySizeBytes ||
                UnsafeUtility.SizeOf<FoveatedSimulationInput>() != FoveatedSimulationInputSizeBytes ||
                Marshal.OffsetOf<FoveatedSimulationInput>(nameof(FoveatedSimulationInput.PreviousTier)).ToInt32() != 28 ||
                UnsafeUtility.SizeOf<FoveatedSimulationDecision>() != FoveatedSimulationDecisionSizeBytes ||
                UnsafeUtility.SizeOf<StaticObstacleData>() != StaticObstacleDataSizeBytes)
            {
                DisableComputeDispatch(ComputeDisableReasonAncillaryLayoutMismatch);
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

            if (boidCompute == null || !_coldSupportsComputeShaders)
            {
                if (boidCompute != null)
                    DisableComputeDispatch(ComputeDisableReasonUnsupportedCompute);

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

            if (!TryValidateKernel(MainKernelName, out int mainKernelIndex, out uint mainThreadGroupSizeX) ||
                !TryValidateKernel(ClearLatchStatsKernelName, out int clearStatsKernelIndex, out uint clearStatsThreadGroupSizeX) ||
                !TryValidateKernel(ClearSpatialGridKernelName, out int clearSpatialGridKernelIndex, out uint clearSpatialGridThreadGroupSizeX) ||
                !TryValidateKernel(BuildSpatialGridKernelName, out int buildSpatialGridKernelIndex, out uint buildSpatialGridThreadGroupSizeX) ||
                !TryValidateKernel(ClearPbdCorrectionsKernelName, out int clearPbdCorrectionsKernelIndex, out uint clearPbdCorrectionsThreadGroupSizeX) ||
                !TryValidateKernel(PbdSolveKernelName, out int pbdSolveKernelIndex, out uint pbdSolveThreadGroupSizeX) ||
                !TryValidateKernel(ApplyOriginShiftKernelName, out int applyOriginShiftKernelIndex, out uint applyOriginShiftThreadGroupSizeX))
            {
                _hasSpawnData = false;
                return false;
            }

            _kernelIndex = mainKernelIndex;
            _clearStatsKernelIndex = clearStatsKernelIndex;
            _clearSpatialGridKernelIndex = clearSpatialGridKernelIndex;
            _buildSpatialGridKernelIndex = buildSpatialGridKernelIndex;
            _clearPbdCorrectionsKernelIndex = clearPbdCorrectionsKernelIndex;
            _pbdSolveKernelIndex = pbdSolveKernelIndex;
            _applyOriginShiftKernelIndex = applyOriginShiftKernelIndex;
            _threadGroupSizeX = mainThreadGroupSizeX;
            _clearStatsThreadGroupSizeX = clearStatsThreadGroupSizeX;
            _clearSpatialGridThreadGroupSizeX = clearSpatialGridThreadGroupSizeX;
            _buildSpatialGridThreadGroupSizeX = buildSpatialGridThreadGroupSizeX;
            _clearPbdCorrectionsThreadGroupSizeX = clearPbdCorrectionsThreadGroupSizeX;
            _pbdSolveThreadGroupSizeX = pbdSolveThreadGroupSizeX;
            _applyOriginShiftThreadGroupSizeX = applyOriginShiftThreadGroupSizeX;
            RefreshDispatchGroupCount();
            _computeKernelBindingsValid = true;
            return true;
        }

        private bool TryValidateKernel(string kernelName, out int kernelIndex, out uint groupSizeX)
        {
            kernelIndex = -1;
            groupSizeX = 0u;

            try
            {
                if (!boidCompute.HasKernel(kernelName))
                {
                    DisableComputeDispatch(ComputeDisableReasonMissingKernel);
                    return false;
                }

                kernelIndex = boidCompute.FindKernel(kernelName);
                if (kernelIndex < 0)
                {
                    DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                    return false;
                }

                if (!boidCompute.IsSupported(kernelIndex))
                {
                    DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                    return false;
                }

                boidCompute.GetKernelThreadGroupSizes(kernelIndex, out groupSizeX, out uint groupSizeY, out uint groupSizeZ);
                ulong totalThreads = (ulong)groupSizeX * groupSizeY * groupSizeZ;
                if (groupSizeX == 0u || groupSizeY == 0u || groupSizeZ == 0u)
                {
                    DisableComputeDispatch(ComputeDisableReasonZeroThreadGroup);
                    return false;
                }

                if (groupSizeY != 1u || groupSizeZ != 1u)
                {
                    DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                    return false;
                }

                if (totalThreads > PortableThreadGroupMaxSize)
                {
                    DisableComputeDispatch(ComputeDisableReasonOversizedThreadGroup);
                    return false;
                }

                return true;
            }
            catch (ObjectDisposedException)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                return false;
            }
            catch (InvalidOperationException)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                return false;
            }
            catch (ArgumentException)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                return false;
            }
            catch (MissingReferenceException)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                return false;
            }
            catch (UnityException)
            {
                DisableComputeDispatch(ComputeDisableReasonKernelValidationFailure);
                return false;
            }
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
            _threadGroupSizeX = 0u;
            _clearStatsThreadGroupSizeX = 0u;
            _clearSpatialGridThreadGroupSizeX = 0u;
            _buildSpatialGridThreadGroupSizeX = 0u;
            _clearPbdCorrectionsThreadGroupSizeX = 0u;
            _pbdSolveThreadGroupSizeX = 0u;
            _applyOriginShiftThreadGroupSizeX = 0u;
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
                case ComputeDisableReasonOversizedThreadGroup:
                    return "Compute kernel exceeds portable 256-thread group ceiling.";
                case ComputeDisableReasonUnsupportedCompute:
                    return "Compute shaders unsupported by active platform.";
                case ComputeDisableReasonDispatchGroupLimit:
                    return "Compute dispatch group count exceeds portable per-dimension ceiling.";
                default:
                    return "Unknown compute dispatch failure.";
            }
        }

        private static void LogComputeDispatchDisabled(string message, UnityEngine.Object context)
        {
            Hecton8.Core.H8Debug.LogError(message, context);
        }
#endif

        private void RenderStaticFallback(float cameraDistanceSq, float hibernation01)
        {
            bool shouldRender = ShouldRenderSwarm(cameraDistanceSq);
            _debugVisible = shouldRender;
            _debugHibernation01 = hibernation01;
            _lastSimulationHibernation01 = hibernation01;
            if (shouldRender)
                QueueRenderCurrentBuffer();
        }

        private void QueueRenderCurrentBuffer()
        {
            _renderCurrentBufferRequested = true;
        }

        private void QueueOriginShiftGpuDispatch(Vector3 runtimeOffset)
        {
            if (!IsFiniteVector3(runtimeOffset))
                return;

            _queuedOriginShiftGpuDelta += runtimeOffset;
            if (!IsFiniteVector3(_queuedOriginShiftGpuDelta))
            {
                _queuedOriginShiftGpuDelta = Vector3.zero;
                _originShiftGpuDispatchRequested = false;
                return;
            }

            _originShiftGpuDispatchRequested = true;
        }

        private void DispatchOriginShiftToLiveBoidBuffersVisualSync(Vector3 runtimeOffset)
        {
            if (!IsFiniteVector3(runtimeOffset))
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
                return;
            }

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
            int dispatchGroups = CeilDivPositive(boidShiftCount, (int)_applyOriginShiftThreadGroupSizeX);
            if (dispatchGroups <= 0)
            {
                DisableComputeDispatch(ComputeDisableReasonDispatchGroupLimit);
                return;
            }

            try
            {
                boidCompute.SetVector(_OriginShiftDeltaId, shiftVector);
                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _SimulationFrameBufferId, _simulationFrameBuffer);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferA);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);

                boidCompute.SetBuffer(_applyOriginShiftKernelIndex, _BoidsBufferWriteId, _boidsBufferB);
                boidCompute.Dispatch(_applyOriginShiftKernelIndex, dispatchGroups, 1, 1);
            }
            catch (ObjectDisposedException)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
            catch (InvalidOperationException)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
            catch (ArgumentException)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
            catch (MissingReferenceException)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
            catch (UnityException)
            {
                DisableComputeDispatch(ComputeDisableReasonOriginShiftFailure);
            }
        }

        private void ApplyRuntimeOffsetToSwarmData(Vector3 runtimeOffset)
        {
            if (!IsFiniteVector3(runtimeOffset))
                return;

            if (_statisticalPopulationActive)
            {
                if (!TryResolveRuntimePosition(in _statisticalPopulationCenterAup, out _fieldCenter))
                {
                    ClearStatisticalPopulationPoint();
                    _hasSpawnData = false;
                    _activeBoidCount = 0;
                    _debugActiveBoidCount = 0;
                    ResetThreatGridSnapshot();
                    ResetThreatVoxelSnapshot();
                    return;
                }

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

            QueueOriginShiftGpuDispatch(runtimeOffset);
            ApplyRuntimeOffsetToGrazingAnchors(runtimeOffset);
            ApplyRuntimeOffsetToMassiveThreats(runtimeOffset);
            ApplyRuntimeOffsetToFormationBeacons(runtimeOffset);
            ApplyRuntimeOffsetToFormationObstacles(runtimeOffset);
            ApplyRuntimeOffsetToLeviathanFrontNodes(runtimeOffset);
            ApplyRuntimeOffsetToLeviathanBackNodes(runtimeOffset);
        }

        private void ApplyRuntimeOffsetToGrazingAnchors(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _grazingAnchorsHandle,
                    BufferID.SargassumGrazingAnchors,
                    grazingAnchorCount,
                    out NativeArray<GrazingAnchorData> grazingAnchors))
            {
                try
                {
                    int count = math.min(_activeGrazingAnchorCount, grazingAnchors.Length);
                    for (int i = 0; i < count; i++)
                        grazingAnchors[i] = OffsetGrazingAnchor(grazingAnchors[i], runtimeOffset);

                    UploadGrazingAnchors();
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _grazingAnchorsHandle);
                }
            }
        }

        private void ApplyRuntimeOffsetToMassiveThreats(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _massiveThreatsHandle,
                    BufferID.SargassumMassiveThreats,
                    maxMassiveThreatCount,
                    out NativeArray<MassiveThreatData> massiveThreats))
            {
                try
                {
                    int count = math.min(_activeMassiveThreatCount, massiveThreats.Length);
                    for (int i = 0; i < count; i++)
                        massiveThreats[i] = OffsetMassiveThreat(massiveThreats[i], runtimeOffset);

                    UploadMassiveThreats();
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _massiveThreatsHandle);
                }
            }
        }

        private void ApplyRuntimeOffsetToFormationBeacons(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _formationBeaconsHandle,
                    BufferID.SargassumFormationBeacons,
                    formationBeaconCapacity,
                    out NativeArray<FormationBeaconData> formationBeacons))
            {
                try
                {
                    int count = math.min(_debugFormationBeaconCount, formationBeacons.Length);
                    for (int i = 0; i < count; i++)
                        formationBeacons[i] = OffsetFormationBeacon(formationBeacons[i], runtimeOffset);

                    UploadFormationBeacons();
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _formationBeaconsHandle);
                }
            }
        }

        private void ApplyRuntimeOffsetToFormationObstacles(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _formationObstaclesHandle,
                    BufferID.SargassumFormationObstacles,
                    formationObstacleCapacity,
                    out NativeArray<FormationObstacleData> formationObstacles))
            {
                try
                {
                    int count = math.min(_debugFormationObstacleCount, formationObstacles.Length);
                    for (int i = 0; i < count; i++)
                        formationObstacles[i] = OffsetFormationObstacle(formationObstacles[i], runtimeOffset);

                    UploadFormationObstacles();
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _formationObstaclesHandle);
                }
            }
        }

        private void ApplyRuntimeOffsetToLeviathanFrontNodes(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _leviathanNodeFrontHandle,
                    BufferID.SargassumLeviathanNodeFront,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData> leviathanNodeFront))
            {
                try
                {
                    double3 offsetD = new double3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                    int frontCount = math.clamp(_leviathanPathNodeCount, 0, leviathanNodeFront.Length);
                    for (int i = 0; i < frontCount; i++)
                    {
                        LeviathanNodeData node = leviathanNodeFront[i];
                        double3 posD = new double3(node.Position.x, node.Position.y, node.Position.z) + offsetD;
                        node.Position = new float3((float)posD.x, (float)posD.y, (float)posD.z);
                        leviathanNodeFront[i] = node;
                    }

                    UploadActiveLeviathanSnapshot();
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _leviathanNodeFrontHandle);
                }
            }
        }

        private void ApplyRuntimeOffsetToLeviathanBackNodes(Vector3 runtimeOffset)
        {
            if (TryAcquireSargassumWriteLock(
                    in _leviathanNodeBackHandle,
                    BufferID.SargassumLeviathanNodeBack,
                    leviathanNodeCapacity,
                    out NativeArray<LeviathanNodeData> leviathanNodeBack))
            {
                try
                {
                    double3 offsetD = new double3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                    int backCount = math.clamp(_leviathanPathNodeCount, 0, leviathanNodeBack.Length);
                    for (int i = 0; i < backCount; i++)
                    {
                        LeviathanNodeData node = leviathanNodeBack[i];
                        double3 posD = new double3(node.Position.x, node.Position.y, node.Position.z) + offsetD;
                        node.Position = new float3((float)posD.x, (float)posD.y, (float)posD.z);
                        leviathanNodeBack[i] = node;
                    }
                }
                finally
                {
                    ReleaseSargassumWriteLock(in _leviathanNodeBackHandle);
                }
            }
        }

        private static GrazingAnchorData OffsetGrazingAnchor(GrazingAnchorData anchor, Vector3 runtimeOffset)
        {
            anchor.Position += runtimeOffset;
            return anchor;
        }

        private static MassiveThreatData OffsetMassiveThreat(MassiveThreatData threat, Vector3 runtimeOffset)
        {
            threat.Position += runtimeOffset;
            return threat;
        }

        private static FormationBeaconData OffsetFormationBeacon(FormationBeaconData beacon, Vector3 runtimeOffset)
        {
            beacon.Position += runtimeOffset;
            return beacon;
        }

        private static FormationObstacleData OffsetFormationObstacle(FormationObstacleData obstacle, Vector3 runtimeOffset)
        {
            obstacle.Position += runtimeOffset;
            return obstacle;
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
                Hecton8.Core.H8Debug.LogError("SargassumMicroFaunaBoids editor validation watchdog tripped.", this);
                return;
            }

            try
            {
                SanitizeSettings();
                RefreshRenderLayerCache();
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

using System.Runtime.InteropServices;
#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
#endif
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class TetherVerletJobLayout
    {
        public const int TelemetryEntryStrideBytes = 64;
        public const int MaxSolverIterations = 10;
    }

    internal static class TetherVerletFaultFlags
    {
        public const int None = 0;
        public const int NonFiniteNode = 1;
        public const int ConstraintNonFinite = 2;
        public const int VaultResolveFailed = 4;
        public const int VaultLockFailed = 8;
        public const int VaultMetadataMismatch = 16;
        public const int VaultFailureDumpRequested = 32;
        public const int BufferBoundsMismatch = 64;
    }

    [StructLayout(LayoutKind.Explicit, Size = TetherVerletJobLayout.TelemetryEntryStrideBytes)]
    internal struct TetherVerletTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int NodeCount;
        [FieldOffset(8)] public int IterationCount;
        [FieldOffset(12)] public float PeakConstraintDelta;
        [FieldOffset(16)] public float PeakCableTension;
        [FieldOffset(20)] public float3 AnchorPosition;
        [FieldOffset(32)] public float3 PayloadPosition;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint BufferId;
        [FieldOffset(52)] public uint Generation;
        [FieldOffset(56)] public uint FailureCode;
        [FieldOffset(60)] public uint Reserved0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletIntegrationJob : IJobParallelFor
    {
        private const byte PinnedNodeMask = 1;

        [NoAlias] public NativeArray<float3> Positions;
        [NoAlias] public NativeArray<float3> PreviousPositions;
        [NoAlias] public NativeArray<float3> Velocities;
        [NoAlias] public NativeArray<byte> NodeFaultFlags;

        [ReadOnly, NoAlias] public NativeArray<float3> PinnedPositions;
        [ReadOnly, NoAlias] public NativeArray<byte> PinnedMask;

        public float3 Acceleration;
        public float DeltaTimeSq;
        public float VelocityDamping;
        public float MaxCableVelocity;
        public float FloorY;
        public float NodeRadius;
        public MockWorldSampler WorldSampler;
        public float RockFriction01;
        public int WorldSamplerEnabled;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Positions.Length)
                return;

            if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                NodeFaultFlags[index] = 0;

            if (!PreviousPositions.IsCreated || (uint)index >= (uint)PreviousPositions.Length)
            {
                WriteNodeFault(index, TetherVerletFaultFlags.BufferBoundsMismatch);
                return;
            }

            if (PinnedMask.IsCreated && index < PinnedMask.Length && (PinnedMask[index] & PinnedNodeMask) != 0)
            {
                float3 pinned = ResolvePinnedPosition(index);
                Positions[index] = pinned;
                PreviousPositions[index] = pinned;
                if (Velocities.IsCreated && index < Velocities.Length)
                    Velocities[index] = float3.zero;
                return;
            }

            float3 position = Positions[index];
            float3 previous = PreviousPositions[index];
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(previous)))
            {
                float3 recovered = math.all(math.isfinite(position)) ? position : float3.zero;
                Positions[index] = recovered;
                PreviousPositions[index] = recovered;
                if (Velocities.IsCreated && index < Velocities.Length)
                    Velocities[index] = float3.zero;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
                return;
            }

            float velocityDamping = SanitizeNonNegative(VelocityDamping, 0f);
            float3 velocity = (position - previous) * velocityDamping;
            float maxVelocity = math.select(0f, math.max(0f, MaxCableVelocity), math.isfinite(MaxCableVelocity));
            float velocityLengthSq = math.lengthsq(velocity);
            if (!math.isfinite(velocityLengthSq))
            {
                velocity = float3.zero;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            float maxVelocitySq = maxVelocity * maxVelocity;
            float clampMask = math.step(0.000001f, maxVelocity) * math.step(maxVelocitySq, velocityLengthSq);
            float3 clampedVelocity = velocity * (maxVelocity * math.rsqrt(math.max(velocityLengthSq, 0.000001f)));
            velocity = math.select(velocity, clampedVelocity, clampMask > 0f);

            if (!math.all(math.isfinite(velocity)))
            {
                velocity = float3.zero;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            if (Velocities.IsCreated && index < Velocities.Length)
                Velocities[index] = velocity;

            float3 acceleration = SanitizeFloat3(Acceleration, float3.zero);
            float3 sampledAcceleration = SanitizeFloat3(acceleration + WorldSampler.SampleFlowAcceleration(position), acceleration);
            acceleration = math.select(acceleration, sampledAcceleration, WorldSamplerEnabled != 0);
            float safeDeltaTimeSq = math.select(0f, DeltaTimeSq, math.isfinite(DeltaTimeSq) & DeltaTimeSq >= 0f);
            float nodeRadius = SanitizeNonNegative(NodeRadius, 0f);
            float floor = SanitizeFinite(FloorY, 0f) + nodeRadius;
            float3 next = position + velocity + (acceleration * safeDeltaTimeSq);
            if (next.y < floor)
                next.y = floor;

            float3 previousForNextStep = position;
            if (WorldSamplerEnabled != 0)
            {
                SdfSampleDTO sample = WorldSampler.Sample(next);
                if (sample.Distance < nodeRadius)
                {
                    float3 up = default;
                    up.y = 1f;
                    float3 normal = MockSDFSampler.SafeNormal(sample.Normal, up);
                    next += normal * (nodeRadius - sample.Distance);
                    float3 impactVelocity = next - position;
                    float3 normalVelocity = normal * math.dot(impactVelocity, normal);
                    float3 tangentVelocity = impactVelocity - normalVelocity;
                    float roughness = math.saturate(SanitizeFinite(RockFriction01, 0f));
                    previousForNextStep = next - tangentVelocity * (1f - roughness);
                }
            }

            if (!math.all(math.isfinite(next)))
            {
                next = position;
                previousForNextStep = previous;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            PreviousPositions[index] = previousForNextStep;
            Positions[index] = next;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private float3 ResolvePinnedPosition(int index)
        {
            if (PinnedPositions.IsCreated && (uint)index < (uint)PinnedPositions.Length)
                return SanitizeFloat3(PinnedPositions[index], float3.zero);

            WriteNodeFault(index, TetherVerletFaultFlags.BufferBoundsMismatch);
            return float3.zero;
        }

        private void WriteNodeFault(int index, int faultFlag)
        {
            if (NodeFaultFlags.IsCreated && (uint)index < (uint)NodeFaultFlags.Length)
                NodeFaultFlags[index] = (byte)faultFlag;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.select(fallback, math.max(0f, value), math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct VerletCableSolverJob : IJob
    {
        private const float MinLengthSq = 0.000001f;
        private const byte PinnedNodeMask = 1;

        [NoAlias] public NativeArray<float3> Positions;
        [NoAlias] public NativeArray<float3> Corrections;
        [NoAlias] public NativeArray<float> CorrectionWeights;
        [NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<float> SolverStats;
        [NoAlias] public NativeArray<int> SolverFlags;

        [ReadOnly, NoAlias] public NativeArray<float> SegmentRestLengths;
        [ReadOnly, NoAlias] public NativeArray<float3> PinnedPositions;
        [ReadOnly, NoAlias] public NativeArray<byte> PinnedMask;
        [ReadOnly, NoAlias] public NativeArray<byte> NodeFaultFlags;

        public int NodeCount;
        public int IterationCount;
        public float FloorY;
        public float NodeRadius;

        public void Execute()
        {
            if (!Positions.IsCreated ||
                !Corrections.IsCreated ||
                !CorrectionWeights.IsCreated ||
                !SegmentRestLengths.IsCreated ||
                !SegmentTensions.IsCreated)
            {
                WriteStats(0f, TetherVerletFaultFlags.BufferBoundsMismatch);
                return;
            }

            int nodeCapacity = math.min(Positions.Length, math.min(Corrections.Length, CorrectionWeights.Length));
            int nodeCount = math.clamp(NodeCount, 0, nodeCapacity);
            int faultFlags = nodeCount != NodeCount
                ? TetherVerletFaultFlags.BufferBoundsMismatch
                : TetherVerletFaultFlags.None;
            if (nodeCount < 2)
            {
                WriteStats(0f, faultFlags);
                return;
            }

            int segmentCapacity = math.min(SegmentRestLengths.Length, SegmentTensions.Length);
            int segmentCount = math.min(nodeCount - 1, segmentCapacity);
            if (segmentCount < nodeCount - 1)
                faultFlags |= TetherVerletFaultFlags.BufferBoundsMismatch;

            int iterations = math.clamp(IterationCount, 1, TetherVerletJobLayout.MaxSolverIterations);
            if (iterations != IterationCount)
                faultFlags |= TetherVerletFaultFlags.BufferBoundsMismatch;

            float floor = SanitizeFinite(FloorY, 0f) + SanitizeNonNegative(NodeRadius, 0f);
            float peakDelta = 0f;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    Corrections[nodeIndex] = float3.zero;
                    CorrectionWeights[nodeIndex] = 0f;
                    if (NodeFaultFlags.IsCreated && nodeIndex < NodeFaultFlags.Length && NodeFaultFlags[nodeIndex] != 0)
                        faultFlags |= NodeFaultFlags[nodeIndex];

                    if (IsPinned(nodeIndex))
                    {
                        Positions[nodeIndex] = ResolvePinnedPosition(nodeIndex, ref faultFlags);
                        continue;
                    }

                    if (!math.all(math.isfinite(Positions[nodeIndex])))
                    {
                        Positions[nodeIndex] = float3.zero;
                        faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
                    }
                }

                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    int a = segmentIndex;
                    int b = segmentIndex + 1;
                    float3 p1 = Positions[a];
                    float3 p2 = Positions[b];
                    float3 dir = p1 - p2;
                    float lenSq = math.lengthsq(dir);
                    if (!math.isfinite(lenSq))
                    {
                        SegmentTensions[segmentIndex] = 0f;
                        faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
                        continue;
                    }

                    if (lenSq <= MinLengthSq)
                    {
                        SegmentTensions[segmentIndex] = 0f;
                        continue;
                    }

                    float invLength = math.rsqrt(math.max(lenSq, 0.0001f));
                    float distance = lenSq * invLength;
                    if (!math.isfinite(distance))
                    {
                        SegmentTensions[segmentIndex] = 0f;
                        faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
                        continue;
                    }

                    float rawRestLength = SegmentRestLengths[segmentIndex];
                    float restLength = math.select(0.0001f, math.max(0.0001f, rawRestLength), math.isfinite(rawRestLength));
                    float delta = distance - restLength;
                    float stretch = math.max(0f, delta);
                    peakDelta = math.max(peakDelta, stretch);
                    SegmentTensions[segmentIndex] = stretch;
                    if (stretch <= 0f)
                        continue;

                    float3 offset = dir * (stretch * invLength * 0.5f);
                    bool pinA = IsPinned(a);
                    bool pinB = IsPinned(b);
                    if (pinA && pinB)
                        continue;

                    if (pinA)
                    {
                        Corrections[b] += offset + offset;
                        CorrectionWeights[b] += 1f;
                    }
                    else if (pinB)
                    {
                        Corrections[a] -= offset + offset;
                        CorrectionWeights[a] += 1f;
                    }
                    else
                    {
                        Corrections[a] -= offset;
                        Corrections[b] += offset;
                        CorrectionWeights[a] += 1f;
                        CorrectionWeights[b] += 1f;
                    }
                }

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    if (IsPinned(nodeIndex))
                    {
                        Positions[nodeIndex] = ResolvePinnedPosition(nodeIndex, ref faultFlags);
                        continue;
                    }

                    float weight = CorrectionWeights[nodeIndex];
                    if (weight > 0f)
                        Positions[nodeIndex] += Corrections[nodeIndex] * math.rcp(math.max(weight, 0.000001f));

                    float3 constrained = Positions[nodeIndex];
                    if (constrained.y < floor)
                        constrained.y = floor;

                    if (!math.all(math.isfinite(constrained)))
                    {
                        constrained = float3.zero;
                        faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
                    }

                    Positions[nodeIndex] = constrained;
                }
            }

            WriteStats(peakDelta, faultFlags);
        }

        private bool IsPinned(int index)
        {
            return PinnedMask.IsCreated && index < PinnedMask.Length && (PinnedMask[index] & PinnedNodeMask) != 0;
        }

        private float3 ResolvePinnedPosition(int index, ref int faultFlags)
        {
            if (PinnedPositions.IsCreated && (uint)index < (uint)PinnedPositions.Length)
                return SanitizeFloat3(PinnedPositions[index], float3.zero, ref faultFlags);

            faultFlags |= TetherVerletFaultFlags.BufferBoundsMismatch;
            return float3.zero;
        }

        private void WriteStats(float peakDelta, int faultFlags)
        {
            if (SolverStats.IsCreated && SolverStats.Length > 0)
                SolverStats[0] = math.select(0f, math.max(0f, peakDelta), math.isfinite(peakDelta));

            if (SolverFlags.IsCreated && SolverFlags.Length > 0)
                SolverFlags[0] = faultFlags;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback, ref int faultFlags)
        {
            if (math.all(math.isfinite(value)))
                return value;

            faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
            return fallback;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.select(fallback, math.max(0f, value), math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletOriginShiftJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> Positions;
        [NoAlias] public NativeArray<float3> PreviousPositions;
        [NoAlias] public NativeArray<float3> PinnedPositions;
        public float3 ShiftOffset;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Positions.Length)
                return;

            float3 safeShiftOffset = math.select(float3.zero, ShiftOffset, math.isfinite(ShiftOffset));
            Positions[index] = SanitizeFloat3(Positions[index] - safeShiftOffset);

            if (index < PreviousPositions.Length)
                PreviousPositions[index] = SanitizeFloat3(PreviousPositions[index] - safeShiftOffset);

            if (PinnedPositions.IsCreated && index < PinnedPositions.Length)
                PinnedPositions[index] = SanitizeFloat3(PinnedPositions[index] - safeShiftOffset);
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockTetherLoadJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> Positions;
        [NoAlias] public NativeArray<float3> PreviousPositions;
        [NoAlias] public NativeArray<float3> Velocities;
        [NoAlias] public NativeArray<byte> NodeFaultFlags;
        [NoAlias] public NativeArray<float> SegmentRestLengths;

        public uint Seed;
        public float ExtremeSpanMeters;
        public float VelocityScale;

        public void Execute(int index)
        {
            uint hash = Hash(index, Seed);
            float span = math.select(1024f, math.max(1f, ExtremeSpanMeters), math.isfinite(ExtremeSpanMeters));
            float velocityScale = math.select(64f, math.max(0f, VelocityScale), math.isfinite(VelocityScale));
            float u = (index & 1023) * math.rcp(1023f);
            float lane = ((int)((hash >> 16) & 15u) - 7.5f) * 0.125f;
            float3 position = default;
            position.x = (u - 0.5f) * span;
            position.y = lane * span * 0.03125f;
            position.z = ((int)(hash & 255u) - 127.5f) * 0.015625f;

            float3 velocity = default;
            velocity.x = ((int)((hash >> 8) & 255u) - 127.5f) * 0.0078125f;
            velocity.y = ((int)((hash >> 24) & 255u) - 127.5f) * 0.0078125f;
            velocity.z = ((int)(hash & 127u) - 63f) * 0.015625f;
            velocity *= velocityScale;

            if ((uint)index < (uint)Positions.Length)
                Positions[index] = position;
            if ((uint)index < (uint)PreviousPositions.Length)
                PreviousPositions[index] = position - velocity;
            if ((uint)index < (uint)Velocities.Length)
                Velocities[index] = velocity;
            if ((uint)index < (uint)NodeFaultFlags.Length)
                NodeFaultFlags[index] = 0;
            if ((uint)index < (uint)SegmentRestLengths.Length)
            {
                float segmentCapacity = (float)math.max(1, SegmentRestLengths.Length);
                SegmentRestLengths[index] = math.max(0.025f, span * math.rcp(segmentCapacity));
            }
        }

        private static uint Hash(int index, uint seed)
        {
            uint value = (uint)index + 0x9E3779B9u + (seed << 6) + (seed >> 2);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletTelemetryJob : IJob
    {
        [NoAlias] public NativeArray<TetherVerletTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryHead;

        [ReadOnly, NoAlias] public NativeArray<float> SolverStats;
        [ReadOnly, NoAlias] public NativeArray<int> SolverFlags;

        public uint FrameIndex;
        public int NodeCount;
        public int IterationCount;
        public float PeakCableTension;
        public float TensionScale;
        public float3 AnchorPosition;
        public float3 PayloadPosition;
        public uint Flags;
        public int TelemetryOffset;
        public int TelemetryCapacity;
        public int TelemetryHeadOffset;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || !TelemetryHead.IsCreated || TelemetryRing.Length == 0 || TelemetryHead.Length == 0)
                return;

            if ((uint)TelemetryOffset >= (uint)TelemetryRing.Length || (uint)TelemetryHeadOffset >= (uint)TelemetryHead.Length)
                return;

            int capacity = TelemetryCapacity > 0
                ? math.min(TelemetryCapacity, TelemetryRing.Length - TelemetryOffset)
                : TelemetryRing.Length - TelemetryOffset;
            if (capacity <= 0)
                return;

            int head = TelemetryHead[TelemetryHeadOffset];
            int localIndex = head >= 0 && head < capacity ? head : 0;
            int index = TelemetryOffset + localIndex;
            uint solverFlags = SolverFlags.IsCreated && SolverFlags.Length > 0 ? (uint)SolverFlags[0] : 0u;
            float peakConstraintDelta = SanitizeNonNegative(SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f);
            float safeTensionScale = SanitizeNonNegative(TensionScale);
            float peakCableTension = math.isfinite(PeakCableTension) && PeakCableTension > 0f
                ? math.max(0f, PeakCableTension)
                : peakConstraintDelta * safeTensionScale;
            TetherVerletTelemetryEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.NodeCount = NodeCount;
            entry.IterationCount = IterationCount;
            entry.PeakConstraintDelta = peakConstraintDelta;
            entry.PeakCableTension = peakCableTension;
            entry.AnchorPosition = SanitizeFloat3(AnchorPosition);
            entry.PayloadPosition = SanitizeFloat3(PayloadPosition);
            entry.Flags = Flags | solverFlags | ResolveTelemetryFaultFlags(AnchorPosition, PayloadPosition, peakCableTension);
            entry.BufferId = 0u;
            entry.Generation = 0u;
            entry.FailureCode = 0u;
            entry.Reserved0 = 0u;
            TelemetryRing[index] = entry;

            TelemetryHead[TelemetryHeadOffset] = (localIndex + 1) % capacity;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(0f, math.max(0f, value), math.isfinite(value));
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }

        private static uint ResolveTelemetryFaultFlags(float3 anchorPosition, float3 payloadPosition, float peakCableTension)
        {
            bool nonFinite = !math.all(math.isfinite(anchorPosition)) ||
                             !math.all(math.isfinite(payloadPosition)) ||
                             !math.isfinite(peakCableTension);
            return nonFinite ? (uint)TetherVerletFaultFlags.NonFiniteNode : 0u;
        }
    }

#if UNITY_EDITOR
    internal static class TetherMemorySovereigntyValidator1303
    {
        private const int StressNodeCount = 4096;
        private const int StressSegmentCount = StressNodeCount - 1;
        private const int StressIterations = 64;
        private const int StressWorkerJoinMilliseconds = 3000;
        private const uint StressSeed = 0x13031303u;
        private const uint FailureHandle = 1u << 0;
        private const uint FailureLock = 1u << 1;
        private const uint FailureJob = 1u << 2;
        private const uint FailureDefrag = 1u << 3;
        private const uint FailureThread = 1u << 4;
        private const ulong StressMutationGuardMask =
            (1UL << (((int)BufferID.TetherVerletPositions) & 63)) |
            (1UL << (((int)BufferID.TetherVerletPreviousPositions) & 63)) |
            (1UL << (((int)BufferID.TetherVerletVelocities) & 63)) |
            (1UL << (((int)BufferID.TetherVerletPinnedPositions) & 63)) |
            (1UL << (((int)BufferID.TetherVerletPinnedMask) & 63)) |
            (1UL << (((int)BufferID.TetherVerletSegmentRestLengths) & 63)) |
            (1UL << (((int)BufferID.TetherVerletSegmentTensions) & 63)) |
            (1UL << (((int)BufferID.TetherVerletCorrections) & 63)) |
            (1UL << (((int)BufferID.TetherVerletCorrectionWeights) & 63)) |
            (1UL << (((int)BufferID.TetherVerletSolverStats) & 63)) |
            (1UL << (((int)BufferID.TetherVerletSolverFlags) & 63)) |
            (1UL << (((int)BufferID.TetherVerletNodeFaultFlags) & 63));

        [UnityEditor.MenuItem("Hecton8/Physics/Run Tether Memory Sovereignty Validator 1303")]
        public static void RunMenu()
        {
            bool passed = RunDefragRaceFuzzer(out uint failureFlags);
            if (passed)
                UnityEngine.Debug.Log("[1303] Tether memory sovereignty validator passed.");
            else
                UnityEngine.Debug.LogError("[1303] Tether memory sovereignty validator failed.");
        }

        public static bool RunDefragRaceFuzzer(out uint failureFlags)
        {
            failureFlags = 0u;
            int stop = 0;
            int compactionTicks = 0;
            int workerFailures = 0;
            System.Threading.Thread worker = null;

            using GlobalDataVault vault = GlobalDataVault.Create(256, 32L * 1024L * 1024L);
            VaultGenerationHandle<float3> positionsHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletPositions,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float3> previousHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletPreviousPositions,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float3> velocitiesHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletVelocities,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float3> pinnedPositionsHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletPinnedPositions,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<byte> pinnedMaskHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.TetherVerletPinnedMask,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<float> restLengthsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.TetherVerletSegmentRestLengths,
                StressSegmentCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float> tensionsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.TetherVerletSegmentTensions,
                StressSegmentCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float3> correctionsHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletCorrections,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float> weightsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.TetherVerletCorrectionWeights,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float> statsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.TetherVerletSolverStats,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> flagsHandle = vault.EnsureGenerationHandle<int>(
                BufferID.TetherVerletSolverFlags,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<byte> nodeFaultsHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.TetherVerletNodeFaultFlags,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);

            if (!ValidateHandle(in positionsHandle, BufferID.TetherVerletPositions) ||
                !ValidateHandle(in previousHandle, BufferID.TetherVerletPreviousPositions) ||
                !ValidateHandle(in velocitiesHandle, BufferID.TetherVerletVelocities) ||
                !ValidateHandle(in pinnedPositionsHandle, BufferID.TetherVerletPinnedPositions) ||
                !ValidateHandle(in pinnedMaskHandle, BufferID.TetherVerletPinnedMask) ||
                !ValidateHandle(in restLengthsHandle, BufferID.TetherVerletSegmentRestLengths) ||
                !ValidateHandle(in tensionsHandle, BufferID.TetherVerletSegmentTensions) ||
                !ValidateHandle(in correctionsHandle, BufferID.TetherVerletCorrections) ||
                !ValidateHandle(in weightsHandle, BufferID.TetherVerletCorrectionWeights) ||
                !ValidateHandle(in statsHandle, BufferID.TetherVerletSolverStats) ||
                !ValidateHandle(in flagsHandle, BufferID.TetherVerletSolverFlags) ||
                !ValidateHandle(in nodeFaultsHandle, BufferID.TetherVerletNodeFaultFlags))
            {
                failureFlags |= FailureHandle;
                return false;
            }

            bool mutationGuardHeld = false;

            try
            {
                if (!vault.TryAcquireMutationGuard(StressMutationGuardMask))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                mutationGuardHeld = true;
                if (!TryResolveStressBuffer(vault, in positionsHandle, BufferID.TetherVerletPositions, StressNodeCount, out NativeArray<float3> positions) ||
                    !TryResolveStressBuffer(vault, in previousHandle, BufferID.TetherVerletPreviousPositions, StressNodeCount, out NativeArray<float3> previousPositions) ||
                    !TryResolveStressBuffer(vault, in velocitiesHandle, BufferID.TetherVerletVelocities, StressNodeCount, out NativeArray<float3> velocities) ||
                    !TryResolveStressBuffer(vault, in pinnedPositionsHandle, BufferID.TetherVerletPinnedPositions, StressNodeCount, out NativeArray<float3> pinnedPositions) ||
                    !TryResolveStressBuffer(vault, in pinnedMaskHandle, BufferID.TetherVerletPinnedMask, StressNodeCount, out NativeArray<byte> pinnedMask) ||
                    !TryResolveStressBuffer(vault, in restLengthsHandle, BufferID.TetherVerletSegmentRestLengths, StressSegmentCount, out NativeArray<float> restLengths) ||
                    !TryResolveStressBuffer(vault, in tensionsHandle, BufferID.TetherVerletSegmentTensions, StressSegmentCount, out NativeArray<float> tensions) ||
                    !TryResolveStressBuffer(vault, in correctionsHandle, BufferID.TetherVerletCorrections, StressNodeCount, out NativeArray<float3> corrections) ||
                    !TryResolveStressBuffer(vault, in weightsHandle, BufferID.TetherVerletCorrectionWeights, StressNodeCount, out NativeArray<float> weights) ||
                    !TryResolveStressBuffer(vault, in statsHandle, BufferID.TetherVerletSolverStats, 1, out NativeArray<float> stats) ||
                    !TryResolveStressBuffer(vault, in flagsHandle, BufferID.TetherVerletSolverFlags, 1, out NativeArray<int> flags) ||
                    !TryResolveStressBuffer(vault, in nodeFaultsHandle, BufferID.TetherVerletNodeFaultFlags, StressNodeCount, out NativeArray<byte> nodeFaults))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                worker = new System.Threading.Thread(
                    () => RunCompactionWorker(vault, ref stop, ref compactionTicks, ref workerFailures))
                {
                    IsBackground = true,
                    Name = "H8.Tether1303.DefragFuzzer"
                };
                if (!TryStartStressWorkerNoThrow(worker))
                {
                    failureFlags |= FailureThread;
                    return false;
                }

                for (int pass = 0; pass < StressIterations; pass++)
                {
                    GenerateMockTetherLoadJob loadJob = default;
                    loadJob.Positions = positions;
                    loadJob.PreviousPositions = previousPositions;
                    loadJob.Velocities = velocities;
                    loadJob.NodeFaultFlags = nodeFaults;
                    loadJob.SegmentRestLengths = restLengths;
                    loadJob.Seed = StressSeed + (uint)pass;
                    loadJob.ExtremeSpanMeters = 2048f;
                    loadJob.VelocityScale = 96f;
                    JobHandle loadHandle = loadJob.Schedule(StressNodeCount, 64);
                    System.Threading.Thread.SpinWait(4096);
                    DispatcherJobFence.TryComplete(ref loadHandle, forceComplete: true);

                    TetherVerletIntegrationJob integrationJob = default;
                    integrationJob.Positions = positions;
                    integrationJob.PreviousPositions = previousPositions;
                    integrationJob.Velocities = velocities;
                    integrationJob.NodeFaultFlags = nodeFaults;
                    integrationJob.PinnedPositions = pinnedPositions;
                    integrationJob.PinnedMask = pinnedMask;
                    integrationJob.Acceleration = default;
                    integrationJob.DeltaTimeSq = 0.0002777778f;
                    integrationJob.VelocityDamping = 0.995f;
                    integrationJob.MaxCableVelocity = 96f;
                    integrationJob.FloorY = -4096f;
                    integrationJob.NodeRadius = 0.05f;
                    integrationJob.WorldSampler = default;
                    integrationJob.RockFriction01 = 0.5f;
                    integrationJob.WorldSamplerEnabled = 0;
                    JobHandle integrationHandle = integrationJob.Schedule(StressNodeCount, 64);
                    System.Threading.Thread.SpinWait(4096);
                    DispatcherJobFence.TryComplete(ref integrationHandle, forceComplete: true);

                    VerletCableSolverJob solverJob = default;
                    solverJob.Positions = positions;
                    solverJob.Corrections = corrections;
                    solverJob.CorrectionWeights = weights;
                    solverJob.SegmentTensions = tensions;
                    solverJob.SolverStats = stats;
                    solverJob.SolverFlags = flags;
                    solverJob.SegmentRestLengths = restLengths;
                    solverJob.PinnedPositions = pinnedPositions;
                    solverJob.PinnedMask = pinnedMask;
                    solverJob.NodeFaultFlags = nodeFaults;
                    solverJob.NodeCount = StressNodeCount;
                    solverJob.IterationCount = 3;
                    solverJob.FloorY = -4096f;
                    solverJob.NodeRadius = 0.05f;
                    JobHandle solverHandle = solverJob.Schedule();
                    System.Threading.Thread.SpinWait(4096);
                    DispatcherJobFence.TryComplete(ref solverHandle, forceComplete: true);

                    if (!ValidateStressResult(positions, stats, flags))
                    {
                        failureFlags |= FailureJob;
                        return false;
                    }
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref stop, 1);
                if (!TryJoinStressWorkerNoThrow(worker, StressWorkerJoinMilliseconds))
                    failureFlags |= FailureThread;

                if (mutationGuardHeld)
                    vault.ReleaseMutationGuard(StressMutationGuardMask);
            }

            bool relocated = vault.GenerateMockVaultRelocationForValidation(
                StressSeed,
                StressNodeCount,
                MemoryDefragPhase.PreSimulation,
                vault.ActiveBurstLockMask);
            positionsHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.TetherVerletPositions,
                StressNodeCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory);
            if (!relocated ||
                !vault.TryReadOnlyHandle(in positionsHandle, out NativeArray<float3>.ReadOnly refreshedPositions) ||
                refreshedPositions.Length < StressNodeCount ||
                System.Threading.Volatile.Read(ref compactionTicks) <= 0 ||
                System.Threading.Volatile.Read(ref workerFailures) != 0)
            {
                failureFlags |= FailureDefrag;
            }

            return failureFlags == 0u;
        }

        private static void RunCompactionWorker(
            GlobalDataVault vault,
            ref int stop,
            ref int compactionTicks,
            ref int workerFailures)
        {
            try
            {
                while (System.Threading.Volatile.Read(ref stop) == 0)
                {
                    vault.RequestEditorForceDefragmentation();
                    vault.FrostTickDefrag(
                        1f / 60f,
                        0f,
                        MemoryDefragPhase.PreSimulation,
                        vault.ActiveBurstLockMask);
                    System.Threading.Interlocked.Increment(ref compactionTicks);
                    System.Threading.Thread.SpinWait(1024);
                }
            }
            catch (System.InvalidOperationException)
            {
                System.Threading.Interlocked.Increment(ref workerFailures);
                System.Threading.Volatile.Write(ref stop, 1);
            }
            catch (System.ArgumentException)
            {
                System.Threading.Interlocked.Increment(ref workerFailures);
                System.Threading.Volatile.Write(ref stop, 1);
            }
        }

        private static bool TryStartStressWorkerNoThrow(System.Threading.Thread worker)
        {
            if (worker == null)
                return false;

            try
            {
                worker.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryJoinStressWorkerNoThrow(System.Threading.Thread worker, int timeoutMilliseconds)
        {
            if (worker == null || !worker.IsAlive)
                return true;

            if (System.Threading.Thread.CurrentThread.ManagedThreadId == worker.ManagedThreadId)
                return false;

            try
            {
                worker.Join(timeoutMilliseconds);
                return !worker.IsAlive;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateStressResult(
            NativeArray<float3> positions,
            NativeArray<float> stats,
            NativeArray<int> flags)
        {
            if (!positions.IsCreated ||
                positions.Length < StressNodeCount ||
                !stats.IsCreated ||
                stats.Length == 0 ||
                !flags.IsCreated ||
                flags.Length == 0)
            {
                return false;
            }

            float3 first = positions[0];
            float3 middle = positions[StressNodeCount >> 1];
            float3 last = positions[StressNodeCount - 1];
            return math.all(math.isfinite(first)) &&
                   math.all(math.isfinite(middle)) &&
                   math.all(math.isfinite(last)) &&
                   math.isfinite(stats[0]) &&
                   flags[0] == TetherVerletFaultFlags.None;
        }

        private static bool ValidateHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == unchecked((uint)SystemID.Physics) &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveStressBuffer<T>(
            GlobalDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int minLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   minLength >= 0 &&
                   ValidateHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= minLength;
        }
    }
#endif
}

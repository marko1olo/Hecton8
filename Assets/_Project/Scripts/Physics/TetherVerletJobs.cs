using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class TetherVerletFaultFlags
    {
        public const int None = 0;
        public const int NonFiniteNode = 1;
        public const int ConstraintNonFinite = 2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct TetherVerletTelemetryEntry
    {
        public uint FrameIndex;
        public int NodeCount;
        public int IterationCount;
        public float PeakConstraintDelta;
        public float PeakCableTension;
        public float3 AnchorPosition;
        public float3 PayloadPosition;
        public uint Flags;
        public uint Pad0;
        public uint Pad1;
        public uint Pad2;
        public uint Pad3;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletIntegrationJob : IJobParallelFor
    {
        private const byte PinnedNodeMask = 1;

        public NativeArray<float3> Positions;
        public NativeArray<float3> PreviousPositions;
        public NativeArray<float3> Velocities;
        public NativeArray<byte> NodeFaultFlags;

        [ReadOnly] public NativeArray<float3> PinnedPositions;
        [ReadOnly] public NativeArray<byte> PinnedMask;

        public float3 Acceleration;
        public float DeltaTimeSq;
        public float VelocityDamping;
        public float MaxCableVelocity;
        public float FloorY;
        public float NodeRadius;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Positions.Length)
                return;

            if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                NodeFaultFlags[index] = 0;

            if (PinnedMask.IsCreated && index < PinnedMask.Length && (PinnedMask[index] & PinnedNodeMask) != 0)
            {
                float3 pinned = SanitizeFloat3(PinnedPositions[index], float3.zero);
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

            float3 velocity = (position - previous) * VelocityDamping;
            float maxVelocity = math.isfinite(MaxCableVelocity) ? math.max(0f, MaxCableVelocity) : 0f;
            float velocityLengthSq = math.lengthsq(velocity);
            if (!math.isfinite(velocityLengthSq))
            {
                velocity = float3.zero;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            float maxVelocitySq = maxVelocity * maxVelocity;
            if (maxVelocity > 0f && velocityLengthSq > maxVelocitySq)
                velocity *= maxVelocity * math.rsqrt(math.max(velocityLengthSq, 0.000001f));

            if (!math.all(math.isfinite(velocity)))
            {
                velocity = float3.zero;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            if (Velocities.IsCreated && index < Velocities.Length)
                Velocities[index] = velocity;

            float3 acceleration = SanitizeFloat3(Acceleration, float3.zero);
            float safeDeltaTimeSq = math.isfinite(DeltaTimeSq) && DeltaTimeSq >= 0f ? DeltaTimeSq : 0f;
            float3 next = position + velocity + (acceleration * safeDeltaTimeSq);
            float floor = FloorY + math.max(0f, NodeRadius);
            if (next.y < floor)
                next.y = floor;

            if (!math.all(math.isfinite(next)))
            {
                next = position;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
            }

            PreviousPositions[index] = position;
            Positions[index] = next;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VerletCableSolverJob : IJob
    {
        private const float MinLengthSq = 0.000001f;
        private const byte PinnedNodeMask = 1;

        public NativeArray<float3> Positions;
        public NativeArray<float3> Corrections;
        public NativeArray<float> CorrectionWeights;
        public NativeArray<float> SegmentTensions;
        public NativeArray<float> SolverStats;
        public NativeArray<int> SolverFlags;

        [ReadOnly] public NativeArray<float> SegmentRestLengths;
        [ReadOnly] public NativeArray<float3> PinnedPositions;
        [ReadOnly] public NativeArray<byte> PinnedMask;
        [ReadOnly] public NativeArray<byte> NodeFaultFlags;

        public int NodeCount;
        public int IterationCount;
        public float FloorY;
        public float NodeRadius;

        public void Execute()
        {
            int nodeCount = math.clamp(NodeCount, 0, Positions.Length);
            if (nodeCount < 2)
            {
                WriteStats(0f, TetherVerletFaultFlags.None);
                return;
            }

            int segmentCount = math.min(nodeCount - 1, SegmentRestLengths.Length);
            int iterations = math.max(1, IterationCount);
            float peakDelta = 0f;
            int faultFlags = TetherVerletFaultFlags.None;

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
                        Positions[nodeIndex] = SanitizeFloat3(PinnedPositions[nodeIndex], float3.zero, ref faultFlags);
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

                    float invLength = math.rsqrt(lenSq);
                    float distance = lenSq * invLength;
                    if (!math.isfinite(distance))
                    {
                        SegmentTensions[segmentIndex] = 0f;
                        faultFlags |= TetherVerletFaultFlags.ConstraintNonFinite;
                        continue;
                    }

                    float rawRestLength = SegmentRestLengths[segmentIndex];
                    float restLength = math.isfinite(rawRestLength) ? math.max(0.0001f, rawRestLength) : 0.0001f;
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
                        Positions[nodeIndex] = SanitizeFloat3(PinnedPositions[nodeIndex], float3.zero, ref faultFlags);
                        continue;
                    }

                    float weight = CorrectionWeights[nodeIndex];
                    if (weight > 0f)
                        Positions[nodeIndex] += Corrections[nodeIndex] * math.rcp(math.max(weight, 0.000001f));

                    float3 constrained = Positions[nodeIndex];
                    float floor = FloorY + math.max(0f, NodeRadius);
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

        private void WriteStats(float peakDelta, int faultFlags)
        {
            if (SolverStats.IsCreated && SolverStats.Length > 0)
                SolverStats[0] = math.isfinite(peakDelta) ? math.max(0f, peakDelta) : 0f;

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
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletOriginShiftJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> PreviousPositions;
        public NativeArray<float3> PinnedPositions;
        public float3 ShiftOffset;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Positions.Length)
                return;

            float3 safeShiftOffset = math.all(math.isfinite(ShiftOffset)) ? ShiftOffset : float3.zero;
            Positions[index] = SanitizeFloat3(Positions[index] - safeShiftOffset);

            if (index < PreviousPositions.Length)
                PreviousPositions[index] = SanitizeFloat3(PreviousPositions[index] - safeShiftOffset);

            if (PinnedPositions.IsCreated && index < PinnedPositions.Length)
                PinnedPositions[index] = SanitizeFloat3(PinnedPositions[index] - safeShiftOffset);
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletTelemetryJob : IJob
    {
        public NativeArray<TetherVerletTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryHead;

        [ReadOnly] public NativeArray<float> SolverStats;
        [ReadOnly] public NativeArray<int> SolverFlags;

        public uint FrameIndex;
        public int NodeCount;
        public int IterationCount;
        public float PeakCableTension;
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
            TelemetryRing[index] = new TetherVerletTelemetryEntry
            {
                FrameIndex = FrameIndex,
                NodeCount = NodeCount,
                IterationCount = IterationCount,
                PeakConstraintDelta = SanitizeNonNegative(SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f),
                PeakCableTension = math.isfinite(PeakCableTension) ? math.max(0f, PeakCableTension) : 0f,
                AnchorPosition = SanitizeFloat3(AnchorPosition),
                PayloadPosition = SanitizeFloat3(PayloadPosition),
                Flags = Flags | solverFlags | ResolveTelemetryFaultFlags(AnchorPosition, PayloadPosition, PeakCableTension),
                Pad0 = 0u,
                Pad1 = 0u,
                Pad2 = 0u,
                Pad3 = 0u
            };

            TelemetryHead[TelemetryHeadOffset] = (localIndex + 1) % capacity;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        private static uint ResolveTelemetryFaultFlags(float3 anchorPosition, float3 payloadPosition, float peakCableTension)
        {
            bool nonFinite = !math.all(math.isfinite(anchorPosition)) ||
                             !math.all(math.isfinite(payloadPosition)) ||
                             !math.isfinite(peakCableTension);
            return nonFinite ? (uint)TetherVerletFaultFlags.NonFiniteNode : 0u;
        }
    }
}

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

    internal struct TetherVerletTelemetryEntry
    {
        public uint FrameIndex;
        public int NodeCount;
        public int IterationCount;
        public float PeakConstraintDelta;
        public float3 AnchorPosition;
        public float3 PayloadPosition;
        public uint Flags;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletIntegrationJob : IJobParallelFor
    {
        private const byte PinnedNodeMask = 1;

        public NativeArray<float3> Positions;
        public NativeArray<float3> PreviousPositions;
        public NativeArray<byte> NodeFaultFlags;

        [ReadOnly] public NativeArray<float3> PinnedPositions;
        [ReadOnly] public NativeArray<byte> PinnedMask;

        public float3 Acceleration;
        public float DeltaTimeSq;
        public float VelocityDamping;
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
                float3 pinned = PinnedPositions[index];
                Positions[index] = pinned;
                PreviousPositions[index] = pinned;
                return;
            }

            float3 position = Positions[index];
            float3 previous = PreviousPositions[index];
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(previous)))
            {
                float3 recovered = math.all(math.isfinite(position)) ? position : float3.zero;
                Positions[index] = recovered;
                PreviousPositions[index] = recovered;
                if (NodeFaultFlags.IsCreated && index < NodeFaultFlags.Length)
                    NodeFaultFlags[index] = (byte)TetherVerletFaultFlags.NonFiniteNode;
                return;
            }

            float3 velocity = (position - previous) * VelocityDamping;
            float3 next = position + velocity + (Acceleration * DeltaTimeSq);
            float floor = FloorY + math.max(0f, NodeRadius);
            if (next.y < floor)
                next.y = floor;

            PreviousPositions[index] = position;
            Positions[index] = next;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct TetherVerletJacobiConstraintJob : IJob
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
                        Positions[nodeIndex] = PinnedPositions[nodeIndex];
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
                    float lenSq = math.max(math.lengthsq(dir), MinLengthSq);
                    float invLength = math.rsqrt(lenSq);
                    float distance = lenSq * invLength;
                    float restLength = math.max(0.0001f, SegmentRestLengths[segmentIndex]);
                    float delta = distance - restLength;
                    float absDelta = math.abs(delta);
                    peakDelta = math.max(peakDelta, absDelta);
                    SegmentTensions[segmentIndex] = absDelta;

                    float3 offset = dir * (delta * invLength * 0.5f);
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
                        Positions[nodeIndex] = PinnedPositions[nodeIndex];
                        continue;
                    }

                    float weight = CorrectionWeights[nodeIndex];
                    if (weight > 0f)
                        Positions[nodeIndex] += Corrections[nodeIndex] * math.rcp(weight);

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
                SolverStats[0] = peakDelta;

            if (SolverFlags.IsCreated && SolverFlags.Length > 0)
                SolverFlags[0] = faultFlags;
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

            Positions[index] -= ShiftOffset;

            if (index < PreviousPositions.Length)
                PreviousPositions[index] -= ShiftOffset;

            if (PinnedPositions.IsCreated && index < PinnedPositions.Length)
                PinnedPositions[index] -= ShiftOffset;
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
        public float3 AnchorPosition;
        public float3 PayloadPosition;
        public uint Flags;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || !TelemetryHead.IsCreated || TelemetryRing.Length == 0 || TelemetryHead.Length == 0)
                return;

            int index = TelemetryHead[0] % TelemetryRing.Length;
            uint solverFlags = SolverFlags.IsCreated && SolverFlags.Length > 0 ? (uint)SolverFlags[0] : 0u;
            TelemetryRing[index] = new TetherVerletTelemetryEntry
            {
                FrameIndex = FrameIndex,
                NodeCount = NodeCount,
                IterationCount = IterationCount,
                PeakConstraintDelta = SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f,
                AnchorPosition = AnchorPosition,
                PayloadPosition = PayloadPosition,
                Flags = Flags | solverFlags
            };

            TelemetryHead[0] = (index + 1) % TelemetryRing.Length;
        }
    }
}

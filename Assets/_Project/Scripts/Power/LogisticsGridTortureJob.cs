using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Power
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LogisticsGridTortureResult
    {
        [FieldOffset(0)] public int NodeCount;
        [FieldOffset(4)] public int EdgeCount;
        [FieldOffset(8)] public int PassCount;
        [FieldOffset(12)] public int BrownoutCount;
        [FieldOffset(16)] public float MinPotential;
        [FieldOffset(20)] public float MaxPotential;
        [FieldOffset(24)] public float AveragePotential;
        [FieldOffset(28)] public uint FaultFlags;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public int ShortCircuitCount;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LogisticsGridTortureJob : IJob
    {
        public const int RequiredNodeCount = 2000;
        public const int RequiredEdgeCount = 6000;
        public const int RequiredShortCircuitCount = 384;
        public const int FixedPassCount = 2;
        private const uint NodeFlagActive = 1u << 0;
        private const uint NodeFlagSource = 1u << 1;
        private const uint NodeFlagBrownout = 1u << 5;
        private const uint EdgeFlagDamaged = 1u << 1;
        private const uint EdgeFlagShortCircuit = 1u << 2;
        private const float BrownoutThreshold01 = 0.20f;

        [NoAlias] public NativeArray<PowerNodeDTO> Nodes;
        [NoAlias] public NativeArray<PowerGridEdgeDTO> Edges;
        [NoAlias] public NativeArray<int> CsrOffsets;
        [NoAlias] public NativeArray<int> EdgeDestinations;
        [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> PotentialFront;
        [NoAlias] public NativeArray<float> PotentialBack;
        [NoAlias] public NativeArray<LogisticsGridTortureResult> Result;

        public void Execute()
        {
            int nodeCount = math.min(RequiredNodeCount, math.min(Nodes.Length, math.min(PotentialFront.Length, PotentialBack.Length)));
            nodeCount = math.min(nodeCount, CsrOffsets.Length - 1);
            int edgeCount = math.min(RequiredEdgeCount, math.min(Edges.Length, math.min(EdgeDestinations.Length, EdgeConductance.Length)));
            if (nodeCount <= 0 || edgeCount <= 0)
            {
                WriteResult(0, 0, 0, 0, 0f, 0f, 0f, 1u, 0u);
                return;
            }

            BuildGraph(nodeCount, edgeCount);
            RunDeltaPass(nodeCount, PotentialFront, PotentialBack, 0.92f);
            RunDeltaPass(nodeCount, PotentialBack, PotentialFront, 0.55f);
            Summarize(nodeCount, edgeCount);
        }

        private void BuildGraph(int nodeCount, int edgeCount)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                bool source = (nodeIndex % 97) == 0;
                PowerNodeDTO node = default;
                node.NodeHash = Hash(nodeIndex, 0x50305244u);
                node.Potential = source ? 1f : 0f;
                node.MaxCapacity = source ? 1000f : 100f;
                node.Flags = NodeFlagActive | (source ? NodeFlagSource : 0u);
                node.InternalResistance = source ? 0.05f : 0.35f;
                Nodes[nodeIndex] = node;
                PotentialFront[nodeIndex] = node.Potential;
                PotentialBack[nodeIndex] = node.Potential;
                CsrOffsets[nodeIndex] = 0;
            }

            CsrOffsets[nodeCount] = 0;
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                int source = edgeIndex % nodeCount;
                CsrOffsets[source + 1] = CsrOffsets[source + 1] + 1;
            }

            int prefix = 0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                int count = CsrOffsets[nodeIndex + 1];
                CsrOffsets[nodeIndex] = prefix;
                prefix += count;
            }
            CsrOffsets[nodeCount] = prefix;

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                int source = edgeIndex % nodeCount;
                int destination = (source + 1 + ((edgeIndex * 37) % math.max(1, nodeCount - 1))) % nodeCount;
                if (destination == source)
                    destination = (destination + 1) % nodeCount;

                PowerGridEdgeDTO edge = default;
                edge.SourceNodeIndex = source;
                edge.DestinationNodeIndex = destination;
                edge.SourceNodeHash = Hash(source, 0x50305244u);
                edge.DestinationNodeHash = Hash(destination, 0x50305244u);
                bool shortCircuit = IsShortCircuitEdge(edgeIndex, edgeCount);
                edge.Flags = shortCircuit
                    ? EdgeFlagShortCircuit | EdgeFlagDamaged
                    : 0u;
                edge.Conductance = shortCircuit ? 0f : 0.2f + ((edgeIndex & 31) * 0.01875f);
                edge.Capacity = shortCircuit ? 0f : 1000f;
                Edges[edgeIndex] = edge;
                EdgeDestinations[edgeIndex] = destination;
                EdgeConductance[edgeIndex] = edge.Conductance;
            }
        }

        private void RunDeltaPass(int nodeCount, NativeArray<float> input, NativeArray<float> output, float gain)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNodeDTO node = Nodes[nodeIndex];
                if ((node.Flags & NodeFlagSource) != 0u)
                {
                    output[nodeIndex] = 1f;
                    continue;
                }

                int start = CsrOffsets[nodeIndex];
                int end = CsrOffsets[nodeIndex + 1];
                float weightedPotential = 0f;
                float conductanceSum = 0f;
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    PowerGridEdgeDTO edge = Edges[edgeIndex];
                    if ((edge.Flags & (EdgeFlagShortCircuit | EdgeFlagDamaged)) != 0u)
                        continue;

                    int destination = EdgeDestinations[edgeIndex];
                    if ((uint)destination >= (uint)nodeCount)
                        continue;

                    float conductance = math.max(0f, EdgeConductance[edgeIndex]);
                    weightedPotential += conductance * math.saturate(input[destination]);
                    conductanceSum += conductance;
                }

                float current = math.saturate(input[nodeIndex]);
                float target = conductanceSum > 0.000001f ? weightedPotential * math.rcp(conductanceSum) : current;
                output[nodeIndex] = math.saturate(current + (target - current) * gain);
            }
        }

        private void Summarize(int nodeCount, int edgeCount)
        {
            int brownoutCount = 0;
            float minPotential = 1f;
            float maxPotential = 0f;
            float sumPotential = 0f;
            uint stateHash = 2166136261u;
            uint faultFlags = 0u;
            int shortCircuitCount = 0;
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                if ((Edges[edgeIndex].Flags & EdgeFlagShortCircuit) != 0u)
                    shortCircuitCount++;
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                float potential = PotentialFront[nodeIndex];
                if (!math.isfinite(potential))
                {
                    potential = 0f;
                    faultFlags |= 1u;
                }

                potential = math.saturate(potential);
                PowerNodeDTO node = Nodes[nodeIndex];
                node.Potential = potential;
                if (potential < BrownoutThreshold01)
                {
                    node.Flags |= NodeFlagBrownout;
                    brownoutCount++;
                }
                else
                {
                    node.Flags &= ~NodeFlagBrownout;
                }

                Nodes[nodeIndex] = node;
                minPotential = math.min(minPotential, potential);
                maxPotential = math.max(maxPotential, potential);
                sumPotential += potential;
                stateHash = Hash(stateHash, math.asuint(potential));
            }

            WriteResult(
                nodeCount,
                edgeCount,
                brownoutCount,
                shortCircuitCount,
                minPotential,
                maxPotential,
                nodeCount > 0 ? sumPotential * math.rcp(nodeCount) : 0f,
                faultFlags,
                stateHash);
        }

        private void WriteResult(
            int nodeCount,
            int edgeCount,
            int brownoutCount,
            int shortCircuitCount,
            float minPotential,
            float maxPotential,
            float averagePotential,
            uint faultFlags,
            uint stateHash)
        {
            if (!Result.IsCreated || Result.Length <= 0)
                return;

            Result[0] = new LogisticsGridTortureResult
            {
                NodeCount = nodeCount,
                EdgeCount = edgeCount,
                PassCount = FixedPassCount,
                BrownoutCount = brownoutCount,
                ShortCircuitCount = shortCircuitCount,
                MinPotential = minPotential,
                MaxPotential = maxPotential,
                AveragePotential = averagePotential,
                FaultFlags = faultFlags,
                StateHash = stateHash
            };
        }

        private static bool IsShortCircuitEdge(int edgeIndex, int edgeCount)
        {
            int shortBudget = math.clamp(RequiredShortCircuitCount, 0, edgeCount);
            return ((edgeIndex * 37) % math.max(1, edgeCount)) < shortBudget;
        }

        private static uint Hash(int index, uint salt)
        {
            return Hash((uint)index, salt);
        }

        private static uint Hash(uint value, uint salt)
        {
            uint hash = 2166136261u;
            hash = (hash ^ salt) * 16777619u;
            hash = (hash ^ value) * 16777619u;
            return hash;
        }
    }
}

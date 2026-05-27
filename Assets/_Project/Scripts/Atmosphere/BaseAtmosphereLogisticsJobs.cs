// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsJobs.cs
// Burst CSR graph build, Jacobi diffusion, breathing, leak injection, telemetry.
// ============================================================================

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    internal static partial class AtmosphereLogisticsLayout
    {
        private const int AtmosphereCellStrideBytes = 32;
        private const int AtmosphereDeltaLaneStrideBytes = 64;

        internal static bool ValidateAtmosphereCellLayout()
        {
            return UnsafeUtility.SizeOf<AtmosphereCellDTO>() == AtmosphereCellStrideBytes &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.NodeHash)) == 0 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.Oxygen01)) == 4 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.CarbonDioxide01)) == 8 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.Nitrogen01)) == 12 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.Toxin01)) == 16 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.Temperature)) == 20 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO.Flags)) == 24 &&
                   OffsetOf<AtmosphereCellDTO>(nameof(AtmosphereCellDTO._pad0)) == 28;
        }

        internal static bool ValidateAtmosphereDeltaLaneLayout()
        {
            return UnsafeUtility.SizeOf<AtmosphereDeltaLane64>() == AtmosphereDeltaLaneStrideBytes &&
                   OffsetOf<AtmosphereDeltaLane64>(nameof(AtmosphereDeltaLane64.Units)) == 0 &&
                   OffsetOf<AtmosphereDeltaLane64>(nameof(AtmosphereDeltaLane64.Flags)) == 4 &&
                   OffsetOf<AtmosphereDeltaLane64>(nameof(AtmosphereDeltaLane64._pad0)) == 8 &&
                   OffsetOf<AtmosphereDeltaLane64>(nameof(AtmosphereDeltaLane64._pad6)) == 56;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
    }

    internal static unsafe class AtmosphereDeltaLaneAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref int Units(AtmosphereDeltaLane64* lanes, int index)
        {
            return ref UnsafeUtility.AsRef<AtmosphereDeltaLane64>(lanes + index).Units;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Clear(AtmosphereDeltaLane64* lanes, int index)
        {
            UnsafeUtility.AsRef<AtmosphereDeltaLane64>(lanes + index) = default;
        }
    }

    internal static class AtmosphereLogisticsAupMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 LocalNodeDeltaClamped(double3 targetAup, double3 observerAup)
        {
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(targetAup, observerAup);
            return AupPrecisionMath.DowncastLocalDeltaClamped(
                localDelta,
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                AupPrecisionMath.CreateOutOfBoundsSentinel());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AtmosphereMockTopologyJob : IJob
    {
        [NoAlias] public NativeArray<AtmosphereNodeDTO> Nodes;
        [NoAlias] public NativeArray<AtmosphereConnectionDTO> Connections;
        [NoAlias] public NativeArray<AtmosphereCellDTO> FrontCells;
        [NoAlias] public NativeArray<AtmosphereCellDTO> BackCells;
        [NoAlias] public NativeArray<AtmosphereConsumerDTO> Consumers;
        [NoAlias] public NativeArray<AtmosphereToxicSourceDTO> Sources;
        [NoAlias] public NativeArray<AtmosphereVentDTO> Vents;
        [NoAlias] public NativeArray<AtmosphereGraphCountersDTO> Counters;
        [NoAlias] public NativeArray<AtmosphereTuningDTO> Tuning;
        public double3 GridOriginAup;

        public void Execute()
        {
            int nodeCapacity = math.min(AtmosphereLogisticsConstants.MaxMockNodes, Nodes.Length);
            int connectionCapacity = math.min(AtmosphereLogisticsConstants.MaxMockConnections, Connections.Length);
            if (nodeCapacity <= 0 || connectionCapacity <= 0 || FrontCells.Length < nodeCapacity || BackCells.Length < nodeCapacity)
            {
                WriteCounters(0, 0, 0, 0, 0, AtmosphereFaultFlags.EmptyGraph);
                return;
            }

            AtmosphereTuningDTO tuning = Tuning.Length > 0 ? Tuning[0] : DefaultTuning();
            float cellSize = math.clamp(FiniteOr(tuning.CellSizeMeters, 2f), AtmosphereLogisticsConstants.MinimumCellSizeMeters, AtmosphereLogisticsConstants.MaximumCellSizeMeters);
            int nodeCount = math.min(1000, nodeCapacity);
            int connectionCount = 0;
            const int Axis = 10;

            for (int i = 0; i < nodeCount; i++)
            {
                int x = i % Axis;
                int y = (i / Axis) % Axis;
                int z = i / (Axis * Axis);
                double3 aup = GridOriginAup + new double3(
                    (x - 4.5d) * cellSize,
                    (y - 4.5d) * cellSize,
                    (z - 4.5d) * cellSize);
                uint nodeHash = Hash3(x, y, z);

                Nodes[i] = new AtmosphereNodeDTO
                {
                    Aup = aup,
                    NodeHash = nodeHash,
                    Flags = AtmosphereCellFlags.Walkable | AtmosphereCellFlags.Sealed
                };

                AtmosphereCellDTO cell = new AtmosphereCellDTO
                {
                    NodeHash = nodeHash,
                    Oxygen01 = AtmosphereLogisticsConstants.DefaultOxygen01,
                    CarbonDioxide01 = AtmosphereLogisticsConstants.DefaultCarbonDioxide01,
                    Nitrogen01 = AtmosphereLogisticsConstants.DefaultNitrogen01,
                    Toxin01 = 0f,
                    Temperature = FiniteOr(tuning.AmbientTemperatureCelsius, AtmosphereLogisticsConstants.DefaultTemperatureCelsius),
                    Flags = AtmosphereCellFlags.Walkable | AtmosphereCellFlags.Sealed,
                    _pad0 = 0u
                };
                FrontCells[i] = cell;
                BackCells[i] = cell;
            }

            for (int i = nodeCount; i < Nodes.Length; i++)
                Nodes[i] = default;
            for (int i = nodeCount; i < FrontCells.Length; i++)
                FrontCells[i] = default;
            for (int i = nodeCount; i < BackCells.Length; i++)
                BackCells[i] = default;

            for (int z = 0; z < Axis && connectionCount < connectionCapacity; z++)
            for (int y = 0; y < Axis && connectionCount < connectionCapacity; y++)
            for (int x = 0; x < Axis && connectionCount < connectionCapacity; x++)
            {
                int node = x + y * Axis + z * Axis * Axis;
                if (x + 1 < Axis)
                    WriteConnection(ref connectionCount, connectionCapacity, node, node + 1, 1f);
                if (y + 1 < Axis)
                    WriteConnection(ref connectionCount, connectionCapacity, node, node + Axis, 0.92f);
                if (z + 1 < Axis)
                    WriteConnection(ref connectionCount, connectionCapacity, node, node + Axis * Axis, 0.82f);
            }

            for (int i = connectionCount; i < Connections.Length; i++)
                Connections[i] = default;

            int consumerCount = 0;
            if (Consumers.Length > 0)
            {
                consumerCount = 1;
                Consumers[0] = new AtmosphereConsumerDTO
                {
                    Aup = Nodes[nodeCount / 2].Aup,
                    OxygenPerSecond01 = 0.000015f,
                    CarbonDioxidePerSecond01 = 0.000014f,
                    RadiusMeters = cellSize * 1.5f,
                    HeatPerSecond = 0.0015f,
                    EntityHash = 0x5348494Eu,
                    Flags = 1u,
                    LastNodeHash = Nodes[nodeCount / 2].NodeHash,
                    LastNodeIndex = nodeCount / 2
                };
                for (int i = 1; i < Consumers.Length; i++)
                    Consumers[i] = default;
            }

            int sourceCount = 0;
            if (Sources.Length > 0)
            {
                sourceCount = 1;
                int sourceNode = math.min(nodeCount - 1, Axis * Axis - 1);
                Sources[0] = new AtmosphereToxicSourceDTO
                {
                    Aup = Nodes[sourceNode].Aup,
                    ToxinPerSecond01 = 0.00001f,
                    CarbonDioxidePerSecond01 = 0.000004f,
                    OxygenDrainPerSecond01 = 0.000003f,
                    HeatPerSecond = 0.01f,
                    SourceHash = 0x52454143u,
                    Flags = AtmosphereCellFlags.ReactorLeak,
                    LastNodeIndex = sourceNode,
                    RadiusMeters = cellSize * 2f
                };
                for (int i = 1; i < Sources.Length; i++)
                    Sources[i] = default;
            }

            int ventCount = 0;
            if (Vents.Length > 0)
            {
                ventCount = 1;
                Vents[0] = new AtmosphereVentDTO
                {
                    Aup = Nodes[0].Aup,
                    RadiusMeters = cellSize * 1.25f,
                    LeakOxygenPerSecond01 = 0.000006f,
                    LeakNitrogenPerSecond01 = 0.000006f,
                    ToxinIngressPerSecond01 = 0.000002f,
                    VentHash = Nodes[0].NodeHash,
                    Flags = AtmosphereCellFlags.Vent | AtmosphereCellFlags.Breached,
                    LastNodeIndex = 0
                };
                for (int i = 1; i < Vents.Length; i++)
                    Vents[i] = default;
            }

            WriteCounters(nodeCount, connectionCount, consumerCount, sourceCount, ventCount,
                AtmosphereLogisticsConstants.GraphInitializedFlag | AtmosphereLogisticsConstants.MockTopologyFlag);
        }

        private void WriteConnection(ref int count, int capacity, int from, int to, float conductance)
        {
            if (count >= capacity)
                return;

            Connections[count++] = new AtmosphereConnectionDTO
            {
                FromNode = from,
                ToNode = to,
                Conductance = conductance,
                Flags = 1u
            };
        }

        private void WriteCounters(int nodeCount, int connectionCount, int consumerCount, int sourceCount, int ventCount, uint flags)
        {
            if (Counters.Length == 0)
                return;

            Counters[0] = new AtmosphereGraphCountersDTO
            {
                NodeCount = nodeCount,
                ConnectionCount = connectionCount,
                CsrEdgeCount = 0,
                ConsumerCount = consumerCount,
                SourceCount = sourceCount,
                VentCount = ventCount,
                TelemetryCursor = 0,
                Flags = flags
            };
        }

        private static AtmosphereTuningDTO DefaultTuning()
        {
            return new AtmosphereTuningDTO
            {
                BaseDiffusionRate = 0.35f,
                InhalationMultiplier = 1f,
                ToxinDissipationSpeed = 0.005f,
                GlobalQualityWeight = 0.5f,
                CellSizeMeters = 2f,
                AmbientTemperatureCelsius = AtmosphereLogisticsConstants.DefaultTemperatureCelsius,
                LeakDrainMultiplier = 1f,
                Flags = 0u
            };
        }

        private static uint Hash3(int x, int y, int z)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)y) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AtmosphereCsrBuildJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AtmosphereConnectionDTO> Connections;
        [NoAlias] public NativeArray<int> EdgeOffsets;
        [NoAlias] public NativeArray<int> EdgeDestinations;
        [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<int> EdgeWriteCursor;
        [NoAlias] public NativeArray<AtmosphereGraphCountersDTO> Counters;

        public void Execute()
        {
            if (Counters.Length == 0)
                return;

            AtmosphereGraphCountersDTO counters = Counters[0];
            int nodeCount = math.clamp(counters.NodeCount, 0, EdgeOffsets.Length - 1);
            int connectionCount = math.clamp(counters.ConnectionCount, 0, Connections.Length);
            if (nodeCount <= 0)
            {
                counters.CsrEdgeCount = 0;
                counters.Flags |= AtmosphereFaultFlags.EmptyGraph;
                Counters[0] = counters;
                return;
            }

            for (int i = 0; i <= nodeCount; i++)
                EdgeOffsets[i] = 0;
            for (int i = 0; i < math.min(EdgeWriteCursor.Length, nodeCount); i++)
                EdgeWriteCursor[i] = 0;

            for (int i = 0; i < connectionCount; i++)
            {
                AtmosphereConnectionDTO edge = Connections[i];
                if (!IsValidConnection(in edge, nodeCount))
                    continue;

                EdgeOffsets[edge.FromNode + 1]++;
                EdgeOffsets[edge.ToNode + 1]++;
            }

            int running = 0;
            EdgeOffsets[0] = 0;
            for (int i = 1; i <= nodeCount; i++)
            {
                int count = EdgeOffsets[i];
                running += count;
                EdgeOffsets[i] = running;
            }

            int edgeCapacity = math.min(EdgeDestinations.Length, EdgeConductance.Length);
            if (running > edgeCapacity)
            {
                counters.Flags |= AtmosphereFaultFlags.CsrOverflow;
                running = edgeCapacity;
            }

            for (int i = 0; i < nodeCount; i++)
                EdgeWriteCursor[i] = EdgeOffsets[i];

            for (int i = 0; i < connectionCount; i++)
            {
                AtmosphereConnectionDTO edge = Connections[i];
                if (!IsValidConnection(in edge, nodeCount))
                    continue;

                WriteEdge(edge.FromNode, edge.ToNode, edge.Conductance, edgeCapacity);
                WriteEdge(edge.ToNode, edge.FromNode, edge.Conductance, edgeCapacity);
            }

            counters.CsrEdgeCount = running;
            counters.Flags |= AtmosphereLogisticsConstants.GraphInitializedFlag;
            counters.Flags &= ~AtmosphereLogisticsConstants.GraphDirtyFlag;
            Counters[0] = counters;
        }

        private void WriteEdge(int from, int to, float conductance, int edgeCapacity)
        {
            int cursor = EdgeWriteCursor[from];
            if ((uint)cursor >= (uint)edgeCapacity)
                return;

            EdgeDestinations[cursor] = to;
            EdgeConductance[cursor] = math.clamp(math.isfinite(conductance) ? conductance : 0f, 0f, 8f);
            EdgeWriteCursor[from] = cursor + 1;
        }

        private static bool IsValidConnection(in AtmosphereConnectionDTO edge, int nodeCount)
        {
            return (uint)edge.FromNode < (uint)nodeCount &&
                   (uint)edge.ToNode < (uint)nodeCount &&
                   edge.FromNode != edge.ToNode &&
                   math.isfinite(edge.Conductance) &&
                   edge.Conductance > 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereClearDeltaJob : IJobParallelFor
    {
        // Safety: these pointers are resolved from Vault NativeArray views immediately before scheduling and never stored beyond the job payload.
        // The runtime locks the owning BufferID values for the job window, so the Vault cannot relocate or resize these arrays while workers run.
        // All pointers address distinct 64-byte delta lanes with length >= scheduled node count; disposal is owned by GlobalDataVault session lifetime.
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* CarbonDioxideDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* NitrogenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* ToxinDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* TemperatureDeltaMilli;

        public void Execute(int index)
        {
            AtmosphereDeltaLaneAccess.Clear(OxygenDeltaUnits, index);
            AtmosphereDeltaLaneAccess.Clear(CarbonDioxideDeltaUnits, index);
            AtmosphereDeltaLaneAccess.Clear(NitrogenDeltaUnits, index);
            AtmosphereDeltaLaneAccess.Clear(ToxinDeltaUnits, index);
            AtmosphereDeltaLaneAccess.Clear(TemperatureDeltaMilli, index);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereConsumerBreathingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AtmosphereNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<AtmosphereConsumerDTO> Consumers;
        // Safety: delta pointers are phase-local Vault views, taken after dispatcher buffer locks and released only in PostSimulation.
        // Parallel writers use Interlocked.Add on 64-byte delta lanes; no float atomics or structural mutation occurs inside the worker loop.
        // The producer bounds every index through NodeCount/ConsumerCount before touching raw memory.
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* CarbonDioxideDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* TemperatureDeltaMilli;
        public int NodeCount;
        public int ConsumerCount;
        public float DeltaTime;
        public float InhalationMultiplier;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ConsumerCount || (uint)index >= (uint)Consumers.Length || NodeCount <= 0)
                return;

            AtmosphereConsumerDTO consumer = Consumers[index];
            if ((consumer.Flags & 1u) == 0u)
                return;

            int node = ResolveNearestNode(consumer.Aup, consumer.LastNodeIndex, consumer.RadiusMeters);
            if ((uint)node >= (uint)NodeCount)
                return;

            float dt = math.max(0f, DeltaTime);
            float inhalation = math.max(0f, math.isfinite(InhalationMultiplier) ? InhalationMultiplier : 1f);
            int o2Units = ToUnits(consumer.OxygenPerSecond01 * inhalation * dt);
            int co2Units = ToUnits(consumer.CarbonDioxidePerSecond01 * inhalation * dt);
            int tempMilli = ToMilli(consumer.HeatPerSecond * dt);

            if (o2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(OxygenDeltaUnits, node), -o2Units);
            if (co2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(CarbonDioxideDeltaUnits, node), co2Units);
            if (tempMilli != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(TemperatureDeltaMilli, node), tempMilli);
        }

        private int ResolveNearestNode(double3 aup, int hint, float radius)
        {
            float radiusSq = math.max(0.01f, radius * radius);
            int bestIndex = -1;
            float bestSq = float.MaxValue;

            if ((uint)hint < (uint)NodeCount)
            {
                float3 delta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[hint].Aup, aup);
                float sq = math.lengthsq(delta);
                if (sq <= radiusSq)
                    return hint;
            }

            for (int i = 0; i < NodeCount; i++)
            {
                float3 delta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[i].Aup, aup);
                float sq = math.lengthsq(delta);
                bool better = sq < bestSq;
                bestSq = math.select(bestSq, sq, better);
                bestIndex = math.select(bestIndex, i, better);
            }

            return bestIndex;
        }

        private static int ToUnits(float value01)
        {
            float safe = math.max(0f, math.isfinite(value01) ? value01 : 0f);
            return (int)math.round(safe * AtmosphereLogisticsConstants.GasUnitScale);
        }

        private static int ToMilli(float value)
        {
            float safe = math.isfinite(value) ? value : 0f;
            return (int)math.round(safe * 1000f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereToxicSourceInjectionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AtmosphereNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<AtmosphereToxicSourceDTO> Sources;
        // Safety: source jobs only receive raw pointers to fixed-size 64-byte delta buffers owned by the atmosphere runtime.
        // The arrays are cleared before source injection, atomically accumulated, then consumed by the diffusion job in the same dependency chain.
        // Node lookup clamps to NodeCount and never dereferences signal-owned or managed memory.
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* CarbonDioxideDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* ToxinDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* TemperatureDeltaMilli;
        public int NodeCount;
        public int SourceCount;
        public float DeltaTime;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SourceCount || (uint)index >= (uint)Sources.Length || NodeCount <= 0)
                return;

            AtmosphereToxicSourceDTO source = Sources[index];
            if (source.ToxinPerSecond01 <= 0f && source.CarbonDioxidePerSecond01 <= 0f && source.OxygenDrainPerSecond01 <= 0f)
                return;

            int node = ResolveNearestNode(source.Aup, source.LastNodeIndex, source.RadiusMeters);
            if ((uint)node >= (uint)NodeCount)
                return;

            float dt = math.max(0f, DeltaTime);
            int toxinUnits = ToUnits(source.ToxinPerSecond01 * dt);
            int co2Units = ToUnits(source.CarbonDioxidePerSecond01 * dt);
            int o2Units = ToUnits(source.OxygenDrainPerSecond01 * dt);
            int tempMilli = ToMilli(source.HeatPerSecond * dt);

            if (toxinUnits != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(ToxinDeltaUnits, node), toxinUnits);
            if (co2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(CarbonDioxideDeltaUnits, node), co2Units);
            if (o2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(OxygenDeltaUnits, node), -o2Units);
            if (tempMilli != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(TemperatureDeltaMilli, node), tempMilli);
        }

        private int ResolveNearestNode(double3 aup, int hint, float radius)
        {
            float radiusSq = math.max(0.01f, radius * radius);
            if ((uint)hint < (uint)NodeCount)
            {
                float3 hintDelta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[hint].Aup, aup);
                if (math.lengthsq(hintDelta) <= radiusSq)
                    return hint;
            }

            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < NodeCount; i++)
            {
                float3 delta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[i].Aup, aup);
                float sq = math.lengthsq(delta);
                bool better = sq < bestSq;
                bestSq = math.select(bestSq, sq, better);
                best = math.select(best, i, better);
            }

            return best;
        }

        private static int ToUnits(float value01)
        {
            float safe = math.max(0f, math.isfinite(value01) ? value01 : 0f);
            return (int)math.round(safe * AtmosphereLogisticsConstants.GasUnitScale);
        }

        private static int ToMilli(float value)
        {
            float safe = math.isfinite(value) ? value : 0f;
            return (int)math.round(safe * 1000f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereVentLeakJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AtmosphereNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<AtmosphereVentDTO> Vents;
        // Safety: vent leak writes are restricted to 64-byte delta lanes protected by dispatcher buffer locks.
        // Each write uses Interlocked.Add because multiple vents can resolve to the same atmosphere node.
        // AUP-to-node math subtracts in double precision before the localized float3 distance calculation.
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* NitrogenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* ToxinDeltaUnits;
        public int NodeCount;
        public int VentCount;
        public float DeltaTime;
        public float LeakDrainMultiplier;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VentCount || (uint)index >= (uint)Vents.Length || NodeCount <= 0)
                return;

            AtmosphereVentDTO vent = Vents[index];
            if ((vent.Flags & AtmosphereCellFlags.Breached) == 0u)
                return;

            int node = ResolveNearestNode(vent.Aup, vent.LastNodeIndex, vent.RadiusMeters);
            if ((uint)node >= (uint)NodeCount)
                return;

            float dt = math.max(0f, DeltaTime);
            float leak = math.max(0f, math.isfinite(LeakDrainMultiplier) ? LeakDrainMultiplier : 1f);
            int o2Units = ToUnits(vent.LeakOxygenPerSecond01 * leak * dt);
            int n2Units = ToUnits(vent.LeakNitrogenPerSecond01 * leak * dt);
            int toxinUnits = ToUnits(vent.ToxinIngressPerSecond01 * dt);

            if (o2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(OxygenDeltaUnits, node), -o2Units);
            if (n2Units != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(NitrogenDeltaUnits, node), -n2Units);
            if (toxinUnits != 0)
                Interlocked.Add(ref AtmosphereDeltaLaneAccess.Units(ToxinDeltaUnits, node), toxinUnits);
        }

        private int ResolveNearestNode(double3 aup, int hint, float radius)
        {
            float radiusSq = math.max(0.01f, radius * radius);
            if ((uint)hint < (uint)NodeCount)
            {
                float3 hintDelta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[hint].Aup, aup);
                if (math.lengthsq(hintDelta) <= radiusSq)
                    return hint;
            }

            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < NodeCount; i++)
            {
                float3 delta = AtmosphereLogisticsAupMath.LocalNodeDeltaClamped(Nodes[i].Aup, aup);
                float sq = math.lengthsq(delta);
                bool better = sq < bestSq;
                bestSq = math.select(bestSq, sq, better);
                best = math.select(best, i, better);
            }

            return best;
        }

        private static int ToUnits(float value01)
        {
            float safe = math.max(0f, math.isfinite(value01) ? value01 : 0f);
            return (int)math.round(safe * AtmosphereLogisticsConstants.GasUnitScale);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereDiffusionSolverJob : IJobParallelFor
    {
        // Safety: Front and Back are generation-checked Vault views resolved in the Simulation phase and locked until PostSimulation.
        // Front is read-only and Back is written one cell per Execute index; the runtime rejects aliasing by using two distinct BufferIDs.
        // The raw pointers are not cached outside the job payload, and bounds are enforced with NodeCount/EdgeCount on every access.
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereCellDTO* Front;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereCellDTO* Back;
        [ReadOnly, NoAlias] public NativeArray<int> EdgeOffsets;
        [ReadOnly, NoAlias] public NativeArray<int> EdgeDestinations;
        [ReadOnly, NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* CarbonDioxideDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* NitrogenDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* ToxinDeltaUnits;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* TemperatureDeltaMilli;
        public int NodeCount;
        public int EdgeCount;
        public float DeltaTime;
        public float BaseDiffusionRate;
        public float ToxinDissipationSpeed;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount)
                return;

            ref AtmosphereCellDTO source = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Front + index);
            int safeEdgeCount = math.max(0, EdgeCount);
            int start = math.clamp(EdgeOffsets[index], 0, safeEdgeCount);
            int end = math.clamp(EdgeOffsets[index + 1], start, safeEdgeCount);

            float o2 = Sanitize01(source.Oxygen01);
            float co2 = Sanitize01(source.CarbonDioxide01);
            float n2 = Sanitize01(source.Nitrogen01);
            float toxin = Sanitize01(source.Toxin01);
            float temp = math.isfinite(source.Temperature) ? source.Temperature : AtmosphereLogisticsConstants.DefaultTemperatureCelsius;
            float totalWeight = 0f;
            float4 neighborGas = float4.zero;
            float neighborTemp = 0f;

            for (int edge = start; edge < end; edge++)
            {
                int dest = EdgeDestinations[edge];
                if ((uint)dest >= (uint)NodeCount)
                    continue;

                float conductance = math.clamp(EdgeConductance[edge], 0f, 8f);
                ref AtmosphereCellDTO neighbor = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Front + dest);
                neighborGas += new float4(
                    Sanitize01(neighbor.Oxygen01),
                    Sanitize01(neighbor.CarbonDioxide01),
                    Sanitize01(neighbor.Nitrogen01),
                    Sanitize01(neighbor.Toxin01)) * conductance;
                neighborTemp += (math.isfinite(neighbor.Temperature) ? neighbor.Temperature : temp) * conductance;
                totalWeight += conductance;
            }

            float dt = math.max(0f, DeltaTime);
            float alpha = math.saturate(math.max(0f, BaseDiffusionRate) * dt);
            if (totalWeight > 0.000001f)
            {
                float denominator = math.max(totalWeight + 1f, 0.0001f);
                float invDenominator = math.rcp(denominator);
                float4 currentGas = new float4(o2, co2, n2, toxin);
                float4 relaxedGas = (neighborGas + currentGas) * invDenominator;
                o2 = math.lerp(o2, relaxedGas.x, alpha);
                co2 = math.lerp(co2, relaxedGas.y, alpha);
                n2 = math.lerp(n2, relaxedGas.z, alpha);
                toxin = math.lerp(toxin, relaxedGas.w, alpha);
                temp = math.lerp(temp, (neighborTemp + temp) * invDenominator, alpha * 0.35f);
            }

            o2 += AtmosphereDeltaLaneAccess.Units(OxygenDeltaUnits, index) * (1f / AtmosphereLogisticsConstants.GasUnitScale);
            co2 += AtmosphereDeltaLaneAccess.Units(CarbonDioxideDeltaUnits, index) * (1f / AtmosphereLogisticsConstants.GasUnitScale);
            n2 += AtmosphereDeltaLaneAccess.Units(NitrogenDeltaUnits, index) * (1f / AtmosphereLogisticsConstants.GasUnitScale);
            toxin += AtmosphereDeltaLaneAccess.Units(ToxinDeltaUnits, index) * (1f / AtmosphereLogisticsConstants.GasUnitScale);
            temp += AtmosphereDeltaLaneAccess.Units(TemperatureDeltaMilli, index) * 0.001f;
            toxin = math.max(0f, toxin - math.max(0f, ToxinDissipationSpeed) * dt);

            ref AtmosphereCellDTO target = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Back + index);
            target.NodeHash = source.NodeHash;
            target.Oxygen01 = math.saturate(o2);
            target.CarbonDioxide01 = math.saturate(co2);
            target.Nitrogen01 = math.saturate(n2);
            target.Toxin01 = math.saturate(toxin);
            target.Temperature = math.clamp(math.isfinite(temp) ? temp : AtmosphereLogisticsConstants.DefaultTemperatureCelsius, -80f, 250f);
            target.Flags = source.Flags;
            target._pad0 = 0u;
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AtmosphereQuantizeGasJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AtmosphereCellDTO> Cells;
        [NoAlias] public NativeArray<AtmosphereGasRemainderDTO> Remainders;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Cells.Length || (uint)index >= (uint)Remainders.Length)
                return;

            AtmosphereCellDTO cell = Cells[index];
            AtmosphereGasRemainderDTO rem = Remainders[index];
            cell.Oxygen01 = Quantize(cell.Oxygen01, ref rem.Oxygen);
            cell.CarbonDioxide01 = Quantize(cell.CarbonDioxide01, ref rem.CarbonDioxide);
            cell.Nitrogen01 = Quantize(cell.Nitrogen01, ref rem.Nitrogen);
            cell.Toxin01 = Quantize(cell.Toxin01, ref rem.Toxin);
            Cells[index] = cell;
            Remainders[index] = rem;
        }

        private static float Quantize(float value, ref float remainder)
        {
            float scaled = math.saturate(math.isfinite(value) ? value : 0f) * AtmosphereLogisticsConstants.GasUnitScale + remainder;
            int units = math.clamp((int)math.floor(scaled), 0, AtmosphereLogisticsConstants.GasUnitScale);
            remainder = math.clamp(scaled - units, -1f, 1f);
            return units * (1f / AtmosphereLogisticsConstants.GasUnitScale);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AtmosphereConservationCorrectionJob : IJob
    {
        // Safety: this single job runs after the parallel diffusion/quantization jobs in the dependency chain and before buffer unlock.
        // It reads Front, applies bounded residual correction across already-quantized Back cells, and reads immutable delta lanes produced earlier in the same frame.
        // All buffers are fixed Vault allocations sized to NodeCount; no pointer survives beyond this scheduled job struct.
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereCellDTO* Front;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereCellDTO* Back;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* OxygenDeltaUnits;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* CarbonDioxideDeltaUnits;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* NitrogenDeltaUnits;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereDeltaLane64* ToxinDeltaUnits;
        public int NodeCount;

        public void Execute()
        {
            if (NodeCount <= 0)
                return;

            long expectedO2 = 0L;
            long expectedCo2 = 0L;
            long expectedN2 = 0L;
            long expectedToxin = 0L;
            long actualO2 = 0L;
            long actualCo2 = 0L;
            long actualN2 = 0L;
            long actualToxin = 0L;

            for (int i = 0; i < NodeCount; i++)
            {
                ref AtmosphereCellDTO src = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Front + i);
                ref AtmosphereCellDTO dst = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Back + i);
                expectedO2 += ToUnits(src.Oxygen01) + AtmosphereDeltaLaneAccess.Units(OxygenDeltaUnits, i);
                expectedCo2 += ToUnits(src.CarbonDioxide01) + AtmosphereDeltaLaneAccess.Units(CarbonDioxideDeltaUnits, i);
                expectedN2 += ToUnits(src.Nitrogen01) + AtmosphereDeltaLaneAccess.Units(NitrogenDeltaUnits, i);
                expectedToxin += ToUnits(src.Toxin01) + AtmosphereDeltaLaneAccess.Units(ToxinDeltaUnits, i);
                actualO2 += ToUnits(dst.Oxygen01);
                actualCo2 += ToUnits(dst.CarbonDioxide01);
                actualN2 += ToUnits(dst.Nitrogen01);
                actualToxin += ToUnits(dst.Toxin01);
            }

            ApplyDistributedCorrection(0, expectedO2 - actualO2);
            ApplyDistributedCorrection(1, expectedCo2 - actualCo2);
            ApplyDistributedCorrection(2, expectedN2 - actualN2);
            ApplyDistributedCorrection(3, expectedToxin - actualToxin);
        }

        private static int ToUnits(float value)
        {
            return math.clamp((int)math.round(math.saturate(math.isfinite(value) ? value : 0f) * AtmosphereLogisticsConstants.GasUnitScale), 0, AtmosphereLogisticsConstants.GasUnitScale);
        }

        private void ApplyDistributedCorrection(int gasIndex, long deltaUnits)
        {
            long remaining = deltaUnits;
            for (int i = 0; i < NodeCount && remaining != 0L; i++)
            {
                ref AtmosphereCellDTO cell = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(Back + i);
                int current = GetGasUnits(in cell, gasIndex);
                if (remaining > 0L)
                {
                    int capacity = AtmosphereLogisticsConstants.GasUnitScale - current;
                    if (capacity <= 0)
                        continue;

                    int step = remaining > capacity ? capacity : (int)remaining;
                    SetGasUnits(ref cell, gasIndex, current + step);
                    remaining -= step;
                }
                else
                {
                    if (current <= 0)
                        continue;

                    long need = -remaining;
                    int step = need > current ? current : (int)need;
                    SetGasUnits(ref cell, gasIndex, current - step);
                    remaining += step;
                }
            }
        }

        private static int GetGasUnits(in AtmosphereCellDTO cell, int gasIndex)
        {
            switch (gasIndex)
            {
                case 0: return ToUnits(cell.Oxygen01);
                case 1: return ToUnits(cell.CarbonDioxide01);
                case 2: return ToUnits(cell.Nitrogen01);
                default: return ToUnits(cell.Toxin01);
            }
        }

        private static void SetGasUnits(ref AtmosphereCellDTO cell, int gasIndex, int units)
        {
            float value = math.clamp(units, 0, AtmosphereLogisticsConstants.GasUnitScale) * (1f / AtmosphereLogisticsConstants.GasUnitScale);
            switch (gasIndex)
            {
                case 0:
                    cell.Oxygen01 = value;
                    break;
                case 1:
                    cell.CarbonDioxide01 = value;
                    break;
                case 2:
                    cell.Nitrogen01 = value;
                    break;
                default:
                    cell.Toxin01 = value;
                    break;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AtmosphereTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AtmosphereCellDTO> Cells;
        [NoAlias] public NativeArray<AtmosphereTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<AtmosphereGraphCountersDTO> Counters;
        [NoAlias] public NativeArray<AtmosphereShaderPayloadDTO> ShaderPayload;
        public int NodeCount;
        public int SolverMicros;
        public int JacobiIterations;
        public int FrameIndex;

        public void Execute()
        {
            if (Counters.Length == 0 || Telemetry.Length == 0)
                return;

            AtmosphereGraphCountersDTO counters = Counters[0];
            int count = math.clamp(NodeCount, 0, math.min(Cells.Length, AtmosphereLogisticsConstants.MaxMockNodes));
            if (count <= 0)
            {
                counters.Flags |= AtmosphereFaultFlags.EmptyGraph;
                Counters[0] = counters;
                return;
            }

            float o2 = 0f;
            float maxCo2 = 0f;
            float n2 = 0f;
            float maxToxin = 0f;
            float temp = 0f;
            uint faultFlags = counters.Flags;
            ulong hash = 14695981039346656037UL;
            uint totalGasUnits = 0u;

            for (int i = 0; i < count; i++)
            {
                AtmosphereCellDTO cell = Cells[i];
                float cellO2 = Sanitize01(cell.Oxygen01, ref faultFlags);
                float cellCo2 = Sanitize01(cell.CarbonDioxide01, ref faultFlags);
                float cellN2 = Sanitize01(cell.Nitrogen01, ref faultFlags);
                float cellToxin = Sanitize01(cell.Toxin01, ref faultFlags);
                float cellTemp = math.isfinite(cell.Temperature) ? cell.Temperature : AtmosphereLogisticsConstants.DefaultTemperatureCelsius;

                o2 += cellO2;
                maxCo2 = math.max(maxCo2, cellCo2);
                n2 += cellN2;
                maxToxin = math.max(maxToxin, cellToxin);
                temp += cellTemp;
                totalGasUnits += (uint)math.clamp((int)math.round((cellO2 + cellCo2 + cellN2 + cellToxin) * AtmosphereLogisticsConstants.GasUnitScale), 0, int.MaxValue);

                hash = Mix(hash, cell.NodeHash);
                hash = Mix(hash, (uint)math.round(cellO2 * AtmosphereLogisticsConstants.GasUnitScale));
                hash = Mix(hash, (uint)math.round(cellCo2 * AtmosphereLogisticsConstants.GasUnitScale));
                hash = Mix(hash, (uint)math.round(cellN2 * AtmosphereLogisticsConstants.GasUnitScale));
                hash = Mix(hash, (uint)math.round(cellToxin * AtmosphereLogisticsConstants.GasUnitScale));
            }

            float inv = math.rcp(count);
            int cursor = counters.TelemetryCursor;
            int write = PositiveMod(cursor, Telemetry.Length);
            AtmosphereTelemetryEntry entry = new AtmosphereTelemetryEntry
            {
                StateHash = hash,
                AverageOxygen01 = o2 * inv,
                MaxCarbonDioxide01 = maxCo2,
                AverageNitrogen01 = n2 * inv,
                MaxToxin01 = maxToxin,
                AverageTemperature = temp * inv,
                FrameIndex = FrameIndex,
                NodeCount = count,
                EdgeCount = counters.CsrEdgeCount,
                ConsumerCount = counters.ConsumerCount,
                SourceCount = counters.SourceCount,
                SolverMicros = SolverMicros,
                JacobiIterations = JacobiIterations,
                FaultFlags = faultFlags,
                TotalGasUnits = totalGasUnits
            };

            Telemetry[write] = entry;
            counters.TelemetryCursor = cursor == int.MaxValue ? 0 : cursor + 1;
            counters.Flags = faultFlags;
            Counters[0] = counters;

            if (ShaderPayload.Length > 0)
            {
                ShaderPayload[0] = new AtmosphereShaderPayloadDTO
                {
                    Oxygen01 = entry.AverageOxygen01,
                    CarbonDioxide01 = entry.MaxCarbonDioxide01,
                    Toxin01 = entry.MaxToxin01,
                    Flow01 = math.saturate(entry.JacobiIterations * 0.125f)
                };
            }
        }

        private static float Sanitize01(float value, ref uint faultFlags)
        {
            if (!math.isfinite(value))
            {
                faultFlags |= AtmosphereFaultFlags.NonFiniteGas | AtmosphereFaultFlags.NaNDetected;
                return 0f;
            }

            return math.saturate(value);
        }

        private static ulong Mix(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        private static int PositiveMod(int value, int divisor)
        {
            int mod = value % divisor;
            return mod < 0 ? mod + divisor : mod;
        }
    }
}

using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Physics;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct DrainageMockNetworkJob : IJob
    {
        [NoAlias] public NativeArray<PumpNodeDTO> PumpNodes;
        [NoAlias] public NativeArray<PipeEdgeDTO> PipeEdges;
        [NoAlias] public NativeArray<double3> NodeAup;
        [NoAlias] public NativeArray<int> PumpRoomIndices;
        [NoAlias] public NativeArray<float> PowerPotential;
        [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public NativeArray<DrainageTuningDTO> Tuning;
        public int RequestedNodeCount;
        public int RequestedEdgeCount;
        public float BaseConductance;
        public float MaxPumpRate;
        public float PumpPowerDraw;

        public void Execute()
        {
            int safeNodeCount = math.min(math.max(1, RequestedNodeCount), PumpNodes.Length);
            safeNodeCount = math.min(safeNodeCount, NodeAup.Length);
            safeNodeCount = math.min(safeNodeCount, PumpRoomIndices.Length);
            safeNodeCount = math.min(safeNodeCount, PowerPotential.Length);
            int safeEdgeCount = math.min(math.max(0, RequestedEdgeCount), PipeEdges.Length);
            int activePumps = 0;
            float safeConductance = math.max(0.0001f, BaseConductance);
            float safePumpRate = math.max(0.0001f, MaxPumpRate);
            float safePowerDraw = math.max(0f, PumpPowerDraw);

            for (int i = 0; i < safeNodeCount; i++)
            {
                uint nodeHash = HashIndex(i, 0x50323232u);
                bool isPump = (i % 7) == 0 || i == 0;
                float pumpScale = 0.55f + ((i & 15) * 0.035f);
                PumpNodes[i] = new PumpNodeDTO
                {
                    NodeHash = nodeHash,
                    IngressRate = 0f,
                    MaxPumpRate = safePumpRate * pumpScale,
                    CurrentEvacuationRate = 0f,
                    Flags = SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Mock | (isPump ? SumpPumpNodeFlags.Pump : 0u),
                    PowerDraw = safePowerDraw
                };

                if (isPump)
                    activePumps++;

                int laneX = i & 31;
                int laneZ = i >> 5;
                double y = -((i * 13) % 11) * 0.22;
                NodeAup[i] = new double3(laneX * 4.0, y, laneZ * 4.0);
                PumpRoomIndices[i] = i;
                PowerPotential[i] = 0.64f + ((i & 7) * 0.045f);
            }

            for (int i = safeNodeCount; i < PumpNodes.Length; i++)
                PumpNodes[i] = default;
            for (int i = safeNodeCount; i < NodeAup.Length; i++)
                NodeAup[i] = default;
            for (int i = safeNodeCount; i < PumpRoomIndices.Length; i++)
                PumpRoomIndices[i] = -1;
            for (int i = safeNodeCount; i < PowerPotential.Length; i++)
                PowerPotential[i] = 0f;

            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
            {
                int source = edgeIndex % safeNodeCount;
                int stride = 1 + ((edgeIndex * 17) % math.max(1, safeNodeCount - 1));
                int destination = (source + stride) % safeNodeCount;
                uint edgeHash = HashIndex(edgeIndex, 0x45444745u);
                PipeEdges[edgeIndex] = new PipeEdgeDTO
                {
                    SourceNodeIndex = source,
                    DestinationNodeIndex = destination,
                    Conductance = safeConductance * (0.70f + ((edgeIndex & 7) * 0.055f)),
                    CurrentFlow = 0f,
                    Flags = SumpPipeEdgeFlags.Active | SumpPipeEdgeFlags.Mock,
                    PowerPotential = 1f,
                    FractionalRemainderM3 = 0f,
                    DownhillScalar = 0f,
                    EdgeHash = edgeHash,
                    SourceNodeHash = PumpNodes[source].NodeHash,
                    DestinationNodeHash = PumpNodes[destination].NodeHash
                };
            }

            for (int edgeIndex = safeEdgeCount; edgeIndex < PipeEdges.Length; edgeIndex++)
                PipeEdges[edgeIndex] = default;

            if (Counters.IsCreated && Counters.Length >= SumpPumpPipeGridConstants.CounterCount)
            {
                Counters[SumpPumpPipeGridConstants.CounterNodeCount] = safeNodeCount;
                Counters[SumpPumpPipeGridConstants.CounterEdgeCount] = safeEdgeCount;
                Counters[SumpPumpPipeGridConstants.CounterActivePumps] = activePumps;
                Counters[SumpPumpPipeGridConstants.CounterTopologyVersion] = Counters[SumpPumpPipeGridConstants.CounterTopologyVersion] + 1;
            }

            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                DrainageTuningDTO tuning = Tuning[0];
                tuning.BasePipeConductance = safeConductance;
                tuning.PumpPowerDraw = safePowerDraw;
                tuning.JacobiSmoothingFactor = SumpPumpPipeGridConstants.DefaultJacobiSmoothingFactor;
                tuning.MaxPumpRateScale = 1f;
                tuning.VisualFlowGain = SumpPumpPipeGridConstants.DefaultVisualFlowGain;
                tuning.MassQuantumM3 = SumpPumpPipeGridConstants.DefaultMassQuantumM3;
                tuning.NodeCount = (ushort)math.min(ushort.MaxValue, safeNodeCount);
                tuning.EdgeCount = (ushort)math.min(ushort.MaxValue, safeEdgeCount);
                tuning.ActivePumpCount = (ushort)math.min(ushort.MaxValue, activePumps);
                Tuning[0] = tuning;
            }
        }

        private static uint HashIndex(int index, uint salt)
        {
            uint hash = SumpPumpPipeGridConstants.FnvOffset;
            hash = SumpPumpPipeGridValidation.MixHash(hash, salt);
            hash = SumpPumpPipeGridValidation.MixHash(hash, (uint)index);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCsrPipeGraphJob : IJob
    {
        [NoAlias] public NativeArray<PipeEdgeDTO> PipeEdges;
        [NoAlias, ReadOnly] public NativeArray<double3> NodeAup;
        [NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias] public NativeArray<int> EdgeDestinations;
        [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        [NoAlias] public NativeArray<int> CsrFlatEdgeIndex;
        [NoAlias] public NativeArray<int> EdgeWriteCursor;
        [NoAlias] public NativeArray<int> Counters;
        public int NodeCount;
        public int EdgeCount;
        public float BasePipeConductance;

        public void Execute()
        {
            int safeNodeCount = ResolveSafeNodeCount();
            int safeEdgeCount = math.min(math.max(0, EdgeCount), PipeEdges.Length);
            int edgeCapacity = math.min(EdgeDestinations.Length, EdgeConductance.Length);
            edgeCapacity = math.min(edgeCapacity, EdgeCurrentFlow.Length);
            edgeCapacity = math.min(edgeCapacity, CsrFlatEdgeIndex.Length);
            int validEdgeCount = 0;

            for (int i = 0; i <= safeNodeCount; i++)
            {
                NodeEdgeOffsets[i] = 0;
                if (i < EdgeWriteCursor.Length)
                    EdgeWriteCursor[i] = 0;
            }

            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
            {
                PipeEdgeDTO edge = PipeEdges[edgeIndex];
                if (!IsValidEdge(in edge, safeNodeCount))
                    continue;

                NodeEdgeOffsets[edge.SourceNodeIndex + 1] = NodeEdgeOffsets[edge.SourceNodeIndex + 1] + 1;
                validEdgeCount++;
            }

            int prefix = 0;
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                int count = NodeEdgeOffsets[nodeIndex + 1];
                int remaining = math.max(0, edgeCapacity - prefix);
                int cappedCount = math.min(count, remaining);
                NodeEdgeOffsets[nodeIndex] = prefix;
                EdgeWriteCursor[nodeIndex] = prefix;
                prefix += cappedCount;
            }
            NodeEdgeOffsets[safeNodeCount] = prefix;
            validEdgeCount = prefix;

            for (int i = 0; i < validEdgeCount; i++)
            {
                EdgeDestinations[i] = -1;
                EdgeConductance[i] = 0f;
                EdgeCurrentFlow[i] = 0f;
                CsrFlatEdgeIndex[i] = -1;
            }

            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
            {
                PipeEdgeDTO edge = PipeEdges[edgeIndex];
                if (!IsValidEdge(in edge, safeNodeCount))
                    continue;

                int source = edge.SourceNodeIndex;
                int slot = EdgeWriteCursor[source];
                int nodeEnd = NodeEdgeOffsets[source + 1];
                if ((uint)slot >= (uint)nodeEnd || (uint)slot >= (uint)validEdgeCount)
                    continue;

                EdgeWriteCursor[source] = slot + 1;
                float downhill = ResolveDownhillScalar(source, edge.DestinationNodeIndex);
                float conductance = ResolveConductance(edge.Conductance, downhill);
                edge.DownhillScalar = downhill;
                edge.Conductance = conductance;
                edge.Flags = downhill > 0f ? edge.Flags | SumpPipeEdgeFlags.DownhillBoosted : edge.Flags & ~SumpPipeEdgeFlags.DownhillBoosted;
                PipeEdges[edgeIndex] = edge;

                EdgeDestinations[slot] = edge.DestinationNodeIndex;
                EdgeConductance[slot] = conductance;
                CsrFlatEdgeIndex[slot] = edgeIndex;
            }

            if (Counters.IsCreated && Counters.Length > SumpPumpPipeGridConstants.CounterValidCsrEdges)
                Counters[SumpPumpPipeGridConstants.CounterValidCsrEdges] = validEdgeCount;
        }

        private int ResolveSafeNodeCount()
        {
            int safeNodeCount = math.max(0, NodeCount);
            safeNodeCount = math.min(safeNodeCount, NodeAup.Length);
            safeNodeCount = math.min(safeNodeCount, NodeEdgeOffsets.Length - 1);
            safeNodeCount = math.min(safeNodeCount, EdgeWriteCursor.Length);
            return safeNodeCount;
        }

        private static bool IsValidEdge(in PipeEdgeDTO edge, int safeNodeCount)
        {
            return (edge.Flags & SumpPipeEdgeFlags.Active) != 0u &&
                   (edge.Flags & SumpPipeEdgeFlags.Sealed) == 0u &&
                   (uint)edge.SourceNodeIndex < (uint)safeNodeCount &&
                   (uint)edge.DestinationNodeIndex < (uint)safeNodeCount &&
                   edge.SourceNodeIndex != edge.DestinationNodeIndex;
        }

        private float ResolveDownhillScalar(int sourceIndex, int destinationIndex)
        {
            double3 source = NodeAup[sourceIndex];
            double3 destination = NodeAup[destinationIndex];
            double3 deltaDouble = destination - source;
            float3 delta = new float3(
                (float)math.clamp(deltaDouble.x, -100000d, 100000d),
                (float)math.clamp(deltaDouble.y, -100000d, 100000d),
                (float)math.clamp(deltaDouble.z, -100000d, 100000d));
            float lengthSq = math.lengthsq(delta);
            if (lengthSq <= 0.000001f || !math.isfinite(lengthSq))
                return 0f;

            float3 direction = delta * math.rsqrt(lengthSq);
            return math.saturate(math.dot(direction, new float3(0f, -1f, 0f)));
        }

        private float ResolveConductance(float edgeConductance, float downhill)
        {
            float baseConductance = math.max(0.000001f, BasePipeConductance);
            float conductance = math.max(baseConductance, math.isfinite(edgeConductance) ? edgeConductance : baseConductance);
            return conductance * (1f + (downhill * 0.35f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PipePressureSolverJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public PumpNodeDTO* PumpNodes;
        [NoAlias, ReadOnly] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<float> EdgeConductance;
        [NoAlias, ReadOnly] public NativeArray<float> PressureFront;
        [NoAlias, ReadOnly] public NativeArray<float> PowerPotential;
        [NoAlias] public NativeArray<float> PressureBack;
        public int NodeCount;
        public float JacobiSmoothingFactor;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount || PumpNodes == null)
                return;

            ref PumpNodeDTO pump = ref UnsafeUtility.AsRef<PumpNodeDTO>(PumpNodes + index);
            float oldPressure = ReadFloat(PressureFront, index, 0f);
            if ((pump.Flags & SumpPumpNodeFlags.Active) == 0u)
            {
                PressureBack[index] = 0f;
                pump.CurrentEvacuationRate = 0f;
                return;
            }

            int start = math.clamp(NodeEdgeOffsets[index], 0, EdgeDestinations.Length);
            int end = math.clamp(NodeEdgeOffsets[index + 1], start, EdgeDestinations.Length);
            float weightedPressure = 0f;
            float conductanceSum = 0f;
            for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                int destination = EdgeDestinations[edgeIndex];
                if ((uint)destination >= (uint)NodeCount)
                    continue;

                float conductance = math.max(0f, ReadFloat(EdgeConductance, edgeIndex, 0f));
                weightedPressure += conductance * ReadFloat(PressureFront, destination, 0f);
                conductanceSum += conductance;
            }

            float power = SumpPumpPipeGridValidation.Sanitize01(ReadFloat(PowerPotential, index, 0f), 0f);
            float pumpRate = ((pump.Flags & SumpPumpNodeFlags.Pump) != 0u)
                ? math.max(0f, pump.MaxPumpRate) * power
                : 0f;
            float solvedPressure = (weightedPressure + pumpRate) * math.rcp(math.max(0.000001f, conductanceSum + 1f));
            float smoothing = math.saturate(math.isfinite(JacobiSmoothingFactor) ? JacobiSmoothingFactor : SumpPumpPipeGridConstants.DefaultJacobiSmoothingFactor);
            float nextPressure = math.lerp(oldPressure, solvedPressure, smoothing);
            if (!math.isfinite(nextPressure))
            {
                nextPressure = 0f;
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
            }

            if (power <= 0.0001f)
                pump.Flags |= SumpPumpNodeFlags.PowerStarved;
            else
                pump.Flags &= ~SumpPumpNodeFlags.PowerStarved;

            pump.CurrentEvacuationRate = math.isfinite(pumpRate) ? pumpRate : 0f;
            PressureBack[index] = nextPressure;
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            if (!array.IsCreated || (uint)index >= (uint)array.Length)
                return fallback;

            float value = array[index];
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PipeEdgeFlowJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PipeEdgeDTO> PipeEdges;
        [NoAlias, ReadOnly] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<int> CsrFlatEdgeIndex;
        [NoAlias, ReadOnly] public NativeArray<float> EdgeConductance;
        [NoAlias, ReadOnly] public NativeArray<float> Pressure;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        [NoAlias] public NativeArray<DrainagePipeFlowGpuDTO> FlowGpu;
        public int NodeCount;
        public float VisualFlowGain;

        public void Execute(int sourceIndex)
        {
            if ((uint)sourceIndex >= (uint)NodeCount)
                return;

            float sourcePressure = ReadFloat(Pressure, sourceIndex, 0f);
            int start = math.clamp(NodeEdgeOffsets[sourceIndex], 0, EdgeDestinations.Length);
            int end = math.clamp(NodeEdgeOffsets[sourceIndex + 1], start, EdgeDestinations.Length);
            float visualGain = math.max(0.001f, math.isfinite(VisualFlowGain) ? VisualFlowGain : SumpPumpPipeGridConstants.DefaultVisualFlowGain);
            for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                int destination = EdgeDestinations[edgeIndex];
                float destinationPressure = ReadFloat(Pressure, destination, 0f);
                float conductance = ReadFloat(EdgeConductance, edgeIndex, 0f);
                float flow = (sourcePressure - destinationPressure) * conductance;
                if (!math.isfinite(flow))
                    flow = 0f;

                EdgeCurrentFlow[edgeIndex] = flow;
                int flatEdgeIndex = CsrFlatEdgeIndex[edgeIndex];
                uint edgeHash = 0u;
                uint flags = 0u;
                if ((uint)flatEdgeIndex < (uint)PipeEdges.Length)
                {
                    PipeEdgeDTO edge = PipeEdges[flatEdgeIndex];
                    edge.CurrentFlow = flow;
                    PipeEdges[flatEdgeIndex] = edge;
                    edgeHash = edge.EdgeHash;
                    flags = edge.Flags;
                }

                if (FlowGpu.IsCreated && (uint)edgeIndex < (uint)FlowGpu.Length)
                {
                    FlowGpu[edgeIndex] = new DrainagePipeFlowGpuDTO
                    {
                        Flow01 = math.saturate(math.abs(flow) * visualGain),
                        PressureDelta01 = math.saturate(math.abs(sourcePressure - destinationPressure)),
                        EdgeHash = edgeHash,
                        Flags = flags
                    };
                }
            }
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            if (!array.IsCreated || (uint)index >= (uint)array.Length)
                return fallback;

            float value = array[index];
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearDrainageRoomLocksJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DrainageRoomDrainLock64> RoomDrainLocks;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)RoomDrainLocks.Length)
                return;

            RoomDrainLocks[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvacuateWaterVolumeJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public PumpNodeDTO* PumpNodes;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* FrontCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* BackCompartments;
        [NoAlias, ReadOnly] public NativeArray<int> PumpRoomIndices;
        [NoAlias] public NativeArray<float> PumpRemainderM3;
        [NoAlias] public NativeArray<float> PumpMassErrorM3;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DrainageRoomDrainLock64* RoomDrainLocks;
        public int NodeCount;
        public int CompartmentCount;
        public int RoomDrainLockCount;
        public float DeltaTime;
        public float MassQuantumM3;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount || PumpNodes == null)
                return;

            ref PumpNodeDTO pump = ref UnsafeUtility.AsRef<PumpNodeDTO>(PumpNodes + index);
            float deltaTime = math.isfinite(DeltaTime) ? math.max(0f, DeltaTime) : 0f;
            if ((pump.Flags & (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump)) != (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump))
            {
                pump.CurrentEvacuationRate = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            int roomIndex = ReadInt(PumpRoomIndices, index, -1);
            if ((uint)roomIndex >= (uint)CompartmentCount || FrontCompartments == null || BackCompartments == null || RoomDrainLocks == null || (uint)roomIndex >= (uint)RoomDrainLockCount)
            {
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
                pump.CurrentEvacuationRate = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            float quantum = math.max(0.000001f, math.isfinite(MassQuantumM3) ? MassQuantumM3 : SumpPumpPipeGridConstants.DefaultMassQuantumM3);
            float evacuationRate = math.isfinite(pump.CurrentEvacuationRate) ? math.max(0f, pump.CurrentEvacuationRate) : 0f;
            float oldRemainder = ReadFloat(PumpRemainderM3, index, 0f);
            float requested = evacuationRate * deltaTime;
            requested += oldRemainder;
            if (!math.isfinite(requested))
            {
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
                WriteFloat(PumpRemainderM3, index, 0f);
                pump.CurrentEvacuationRate = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            float quantizedUnitsFloat = math.floor(requested * math.rcp(quantum));
            bool clippedQuantizedUnits = quantizedUnitsFloat > SumpPumpPipeGridConstants.MaxQuantizedDrainUnitsPerPump;
            if (!math.isfinite(quantizedUnitsFloat))
            {
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
                quantizedUnitsFloat = SumpPumpPipeGridConstants.MaxQuantizedDrainUnitsPerPump;
                clippedQuantizedUnits = true;
            }

            quantizedUnitsFloat = math.clamp(quantizedUnitsFloat, 0f, SumpPumpPipeGridConstants.MaxQuantizedDrainUnitsPerPump);
            int quantizedUnits = (int)quantizedUnitsFloat;
            if (quantizedUnits <= 0)
            {
                WriteFloat(PumpRemainderM3, index, math.max(0f, requested));
                pump.CurrentEvacuationRate = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            float quantizedM3 = quantizedUnits * quantum;
            float remainder = clippedQuantizedUnits ? 0f : math.max(0f, requested - quantizedM3);
            if (!TryAcquireRoomLock(roomIndex))
            {
                WriteFloat(PumpRemainderM3, index, oldRemainder);
                pump.CurrentEvacuationRate = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            ref FluidCompartmentDTO front = ref FluidCompartmentPointerUtility.ElementRef(FrontCompartments, roomIndex);
            ref FluidCompartmentDTO back = ref FluidCompartmentPointerUtility.ElementRef(BackCompartments, roomIndex);
            float frontWater = SanitizeCompartmentWater(ref front);
            float backWater = SanitizeCompartmentWater(ref back);
            float availableWater = math.min(frontWater, backWater);
            float actualDrained = math.min(availableWater, quantizedM3);
            if (actualDrained > 0f)
            {
                front.CurrentWaterVolume = math.max(0f, frontWater - actualDrained);
                back.CurrentWaterVolume = math.max(0f, backWater - actualDrained);
            }

            ReleaseRoomLock(roomIndex);
            WriteFloat(PumpRemainderM3, index, remainder);
            pump.CurrentEvacuationRate = deltaTime > 0f ? actualDrained * math.rcp(deltaTime) : 0f;

            float conservativeError = math.abs(front.CurrentWaterVolume - back.CurrentWaterVolume);
            WriteFloat(PumpMassErrorM3, index, conservativeError);
        }

        private bool TryAcquireRoomLock(int roomIndex)
        {
            if (RoomDrainLocks == null || (uint)roomIndex >= (uint)RoomDrainLockCount)
                return false;

            ref int state = ref UnsafeUtility.AsRef<int>(&RoomDrainLocks[roomIndex].LockState);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                if (Interlocked.CompareExchange(ref state, 1, 0) == 0)
                    return true;
            }

            return false;
        }

        private void ReleaseRoomLock(int roomIndex)
        {
            ref int state = ref UnsafeUtility.AsRef<int>(&RoomDrainLocks[roomIndex].LockState);
            Interlocked.Exchange(ref state, 0);
        }

        private static float SanitizeCompartmentWater(ref FluidCompartmentDTO dto)
        {
            float water = dto.CurrentWaterVolume;
            if (!math.isfinite(water))
            {
                dto.Flags |= FluidCompartmentFlags.NonFinite;
                dto.CurrentWaterVolume = 0f;
                return 0f;
            }

            float maxVolume = dto.MaxVolume;
            if (!math.isfinite(maxVolume) || maxVolume <= 0f)
            {
                dto.Flags |= FluidCompartmentFlags.NonFinite;
                dto.CurrentWaterVolume = 0f;
                return 0f;
            }

            water = math.max(0f, water);
            water = math.min(water, maxVolume);
            dto.CurrentWaterVolume = water;
            return water;
        }

        private static int ReadInt(NativeArray<int> array, int index, int fallback)
        {
            return array.IsCreated && (uint)index < (uint)array.Length ? array[index] : fallback;
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            if (!array.IsCreated || (uint)index >= (uint)array.Length)
                return fallback;

            float value = array[index];
            return math.isfinite(value) ? value : fallback;
        }

        private static void WriteFloat(NativeArray<float> array, int index, float value)
        {
            if (array.IsCreated && (uint)index < (uint)array.Length)
                array[index] = math.isfinite(value) ? math.max(0f, value) : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct DrainageTelemetryRecorderJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public PumpNodeDTO* PumpNodes;
        [NoAlias, ReadOnly] public NativeArray<float> Pressure;
        [NoAlias, ReadOnly] public NativeArray<float> PumpMassErrorM3;
        [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public NativeArray<DrainageTuningDTO> Tuning;
        [NoAlias] public NativeArray<DrainageTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<DrainageTelemetryEntry> FrameSummary;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int NodeCount;
        public int EdgeCount;
        public int SolverIterations;
        public uint FrameIndex;
        public float GlobalQualityWeight;
        public uint InputFlags;

        public void Execute()
        {
            int safeNodeCount = math.min(math.max(0, NodeCount), Pressure.Length);
            float deltaTime = 0f;
            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                DrainageTuningDTO activeTuning = Tuning[0];
                deltaTime = math.max(0f, math.isfinite(activeTuning.DeltaTimeSeconds) ? activeTuning.DeltaTimeSeconds : 0f);
            }

            float pressureTotal = 0f;
            float maxPressure = 0f;
            float frameEvacuated = 0f;
            float powerDraw = 0f;
            float massError = 0f;
            uint activePumpCount = 0u;
            uint nanCount = ReadCounter(SumpPumpPipeGridConstants.CounterNanCount);
            bool evacuationApplied = (InputFlags & SumpDrainageTelemetryFlags.MissingFluidVault) == 0u;
            uint stateHash = SumpPumpPipeGridConstants.FnvOffset;
            for (int i = 0; i < safeNodeCount; i++)
            {
                float pressure = Pressure[i];
                if (!math.isfinite(pressure))
                {
                    pressure = 0f;
                    nanCount++;
                }

                pressureTotal += pressure;
                maxPressure = math.max(maxPressure, pressure);
                stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, (uint)i);
                stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, math.asuint(pressure));
                if (PumpNodes != null)
                {
                    PumpNodeDTO pump = UnsafeUtility.AsRef<PumpNodeDTO>(PumpNodes + i);
                    float evacuationRate = math.isfinite(pump.CurrentEvacuationRate) ? math.max(0f, pump.CurrentEvacuationRate) : 0f;
                    if ((pump.Flags & SumpPumpNodeFlags.NonFinite) != 0u || !math.isfinite(pump.CurrentEvacuationRate))
                        nanCount++;
                    if (evacuationApplied &&
                        (pump.Flags & (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump)) == (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump) &&
                        evacuationRate > 0f)
                    {
                        activePumpCount++;
                        frameEvacuated += evacuationRate * deltaTime;
                        float maxRate = math.max(0f, math.isfinite(pump.MaxPumpRate) ? pump.MaxPumpRate : 0f);
                        float utilization = maxRate > 0.000001f ? math.saturate(evacuationRate * math.rcp(maxRate)) : 0f;
                        powerDraw += math.max(0f, math.isfinite(pump.PowerDraw) ? pump.PowerDraw : 0f) * utilization;
                    }

                    float pumpMassError = ReadFloat(PumpMassErrorM3, i, 0f);
                    if (evacuationApplied)
                        massError += pumpMassError;
                    stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, pump.NodeHash);
                    stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, math.asuint(evacuationRate));
                    stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, math.asuint(pumpMassError));
                    stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, pump.Flags);
                }
            }

            float averagePressure = safeNodeCount > 0 ? pressureTotal * math.rcp(safeNodeCount) : 0f;
            frameEvacuated = math.isfinite(frameEvacuated) ? math.max(0f, frameEvacuated) : 0f;
            powerDraw = math.isfinite(powerDraw) ? math.max(0f, powerDraw) : 0f;
            DrainageTelemetryEntry previous = FrameSummary.IsCreated && FrameSummary.Length > 0 ? FrameSummary[0] : default;
            float previousTotal = math.isfinite(previous.TotalEvacuatedM3) ? math.max(0f, previous.TotalEvacuatedM3) : 0f;
            uint flags = InputFlags | (nanCount > 0u ? SumpDrainageTelemetryFlags.NonFinite : 0u);
            DrainageTelemetryEntry entry = new DrainageTelemetryEntry
            {
                FrameIndex = FrameIndex,
                StateHash = stateHash,
                FrameEvacuatedM3 = frameEvacuated,
                TotalEvacuatedM3 = previousTotal + frameEvacuated,
                AveragePressure = math.isfinite(averagePressure) ? averagePressure : 0f,
                MaxPressure = math.isfinite(maxPressure) ? maxPressure : 0f,
                GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f),
                TotalPowerDrawWatts = powerDraw,
                ActivePumpCount = activePumpCount,
                NanCount = nanCount,
                NodeCount = (uint)math.max(0, NodeCount),
                EdgeCount = (uint)math.max(0, EdgeCount),
                Flags = flags,
                ConservativeMassErrorMilli = QuantizeMilli(massError)
            };

            if (FrameSummary.IsCreated && FrameSummary.Length > 0)
                FrameSummary[0] = entry;

            if (TelemetryRing.IsCreated && TelemetryRing.Length > 0 && TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                int cursor = TelemetryCursor[0];
                int capacity = math.min(TelemetryRing.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
                int index = cursor % capacity;
                if (index < 0)
                    index += capacity;
                TelemetryRing[index] = entry;
                TelemetryCursor[0] = cursor + 1;
            }

            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                DrainageTuningDTO tuning = Tuning[0];
                tuning.LastEvacuatedM3 = frameEvacuated;
                tuning.FrameIndex = FrameIndex;
                tuning.StateHash = stateHash;
                tuning.NodeCount = (ushort)math.min(ushort.MaxValue, math.max(0, NodeCount));
                tuning.EdgeCount = (ushort)math.min(ushort.MaxValue, math.max(0, EdgeCount));
                tuning.ActivePumpCount = (ushort)math.min(ushort.MaxValue, entry.ActivePumpCount);
                tuning.SolverIterations = (ushort)math.min(ushort.MaxValue, math.max(0, SolverIterations));
                tuning.Flags = flags;
                Tuning[0] = tuning;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadCounter(int index)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return 0u;

            int value = Counters[index];
            return value <= 0 ? 0u : (uint)value;
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            if (!array.IsCreated || (uint)index >= (uint)array.Length)
                return fallback;

            float value = array[index];
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static uint QuantizeMilli(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0u;

            float milli = math.round(value * 1000f);
            return milli >= 4294967040f ? uint.MaxValue : (uint)milli;
        }
    }
}

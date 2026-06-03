using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts.Physics;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
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
                float conductance = ResolveConductance(edge.Conductance);
                edge.Conductance = conductance;
                float downhill = ResolveDownhillScalar(source, edge.DestinationNodeIndex);
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

        private float ResolveConductance(float edgeConductance)
        {
            float baseConductance = math.max(0.000001f, BasePipeConductance);
            return math.max(baseConductance, math.isfinite(edgeConductance) ? edgeConductance : baseConductance);
        }
    }

    // NativeDisableUnsafePtrRestriction safety proof:
    // The DrainageNodeDTO pointer is a Vault view pinned by SumpPumpPipeGridRuntime before the job chain is scheduled.
    // The runtime keeps the involved Vault lanes locked until DispatcherJobFence reports completion, so compaction cannot
    // relocate the pointer while Burst workers are active.
    //
    // Rejected alternative: NativeArray<DrainageNodeDTO> copy/writeback per job. NativeArray indexers do not expose a ref
    // to the row field, so direct field mutation becomes copy-modify-store traffic and reintroduces CS1612 pressure on the
    // hottest pump rows. The pointer is intentionally paired with UnsafeUtility.AsRef for in-place 32-byte row edits.
    //
    // Scheduling invariant: each job writes disjoint row ranges by IJobParallelFor index, shared scalar lanes carry [NoAlias],
    // and external Power/Fluid inputs are either read-only snapshots or the single back-buffer mutation route guarded by
    // DrainageRoomDrainLock64. No same-frame job reads these pointers after the drainage mutation guard is released.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyPumpPowerConstraintJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public DrainageNodeDTO* PumpNodes;
        [NoAlias, ReadOnly] public NativeArray<float> PumpBaseMaxRate;
        [NoAlias, ReadOnly] public NativeArray<uint> PumpPowerNodeHashes;
        [NoAlias, ReadOnly] public NativeArray<Hecton8.Power.PowerNodeDTO> PowerNodes;
        [NoAlias, ReadOnly] public NativeArray<float> PowerPotentialFront;
        [NoAlias] public NativeArray<float> PowerPotential;
        public int NodeCount;
        public float MaxPumpThroughputM3PerSecond;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount || PumpNodes == null)
                return;

            ref DrainageNodeDTO node = ref UnsafeUtility.AsRef<DrainageNodeDTO>(PumpNodes + index);
            float baseRate = ReadFloat(PumpBaseMaxRate, index, node.MaxPumpRate);
            float throughputLimit = math.max(0.000001f, math.isfinite(MaxPumpThroughputM3PerSecond) ? MaxPumpThroughputM3PerSecond : SumpPumpPipeGridConstants.DefaultMaxPumpRateM3PerSecond);
            baseRate = math.min(math.max(0f, baseRate), throughputLimit);
            uint expectedPowerHash = ReadUInt(PumpPowerNodeHashes, index, node.NodeHashID);
            float potential = ResolvePowerPotential(index, expectedPowerHash);
            node.MaxPumpRate = baseRate * potential;
            if (potential <= 0.0001f)
                node.Flags |= SumpPumpNodeFlags.PowerStarved;
            else
                node.Flags &= ~SumpPumpNodeFlags.PowerStarved;

            if (PowerPotential.IsCreated && (uint)index < (uint)PowerPotential.Length)
                PowerPotential[index] = potential;
        }

        private float ResolvePowerPotential(int index, uint expectedPowerHash)
        {
            if (!PowerPotentialFront.IsCreated || (uint)index >= (uint)PowerPotentialFront.Length)
                return 0f;

            if (PowerNodes.IsCreated)
            {
                if ((uint)index >= (uint)PowerNodes.Length)
                    return 0f;

                Hecton8.Power.PowerNodeDTO powerNode = PowerNodes[index];
                if (expectedPowerHash != 0u && powerNode.NodeHash != expectedPowerHash)
                    return 0f;
            }

            return SumpPumpPipeGridValidation.Sanitize01(PowerPotentialFront[index], 0f);
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            if (!array.IsCreated || (uint)index >= (uint)array.Length)
                return fallback;

            float value = array[index];
            return math.isfinite(value) ? value : fallback;
        }

        private static uint ReadUInt(NativeArray<uint> array, int index, uint fallback)
        {
            return array.IsCreated && (uint)index < (uint)array.Length ? array[index] : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluatePipePressureDeltaPassJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public DrainageNodeDTO* PumpNodes;
        [NoAlias, ReadOnly] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<float> EdgeConductance;
        [NoAlias, ReadOnly] public NativeArray<double3> NodeAup;
        [NoAlias, ReadOnly] public NativeArray<float> PressureFront;
        [NoAlias, ReadOnly] public NativeArray<float> PowerPotential;
        [NoAlias] public NativeArray<float> PressureBack;
        public int NodeCount;
        public float DeltaSmoothingFactor;
        public float GravityAssistScalar;
        public float GravityResistanceScalar;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount || PumpNodes == null)
                return;

            ref DrainageNodeDTO pump = ref UnsafeUtility.AsRef<DrainageNodeDTO>(PumpNodes + index);
            float oldPressure = ReadFloat(PressureFront, index, 0f);
            if ((pump.Flags & SumpPumpNodeFlags.Active) == 0u)
            {
                PressureBack[index] = 0f;
                pump.HydraulicPressure = 0f;
                pump.CurrentFlow = 0f;
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

                float conductance = ResolveGravityConductance(index, destination, ReadFloat(EdgeConductance, edgeIndex, 0f));
                weightedPressure += conductance * ReadFloat(PressureFront, destination, 0f);
                conductanceSum += conductance;
            }

            float power = SumpPumpPipeGridValidation.Sanitize01(ReadFloat(PowerPotential, index, 0f), 0f);
            float pumpRate = ((pump.Flags & SumpPumpNodeFlags.Pump) != 0u)
                ? math.max(0f, pump.MaxPumpRate)
                : 0f;
            float solvedPressure = (weightedPressure + pumpRate) * math.rcp(math.max(0.000001f, conductanceSum + 1f));
            float smoothing = math.saturate(math.isfinite(DeltaSmoothingFactor) ? DeltaSmoothingFactor : SumpPumpPipeGridConstants.DefaultDeltaSmoothingFactor);
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

            pump.HydraulicPressure = nextPressure;
            pump.CurrentFlow = math.isfinite(pumpRate) ? pumpRate : 0f;
            PressureBack[index] = nextPressure;
        }

        private float ResolveGravityConductance(int sourceIndex, int destinationIndex, float baseConductance)
        {
            float conductance = math.max(0f, math.isfinite(baseConductance) ? baseConductance : 0f);
            if (!NodeAup.IsCreated ||
                (uint)sourceIndex >= (uint)NodeAup.Length ||
                (uint)destinationIndex >= (uint)NodeAup.Length)
            {
                return conductance;
            }

            double3 source = NodeAup[sourceIndex];
            double3 destination = NodeAup[destinationIndex];
            bool destinationLower = destination.y < source.y;
            bool destinationHigher = destination.y > source.y;
            double3 high = destinationLower ? source : destination;
            double3 low = destinationLower ? destination : source;
            double3 stableDelta = high - low;
            float verticalMeters = (float)math.clamp(stableDelta.y, 0d, 100000d);
            float gravityWeight = math.saturate(verticalMeters * 0.25f);
            float assist = math.max(0f, math.isfinite(GravityAssistScalar) ? GravityAssistScalar : SumpPumpPipeGridConstants.DefaultGravityAssistScalar);
            float resistance = math.max(0f, math.isfinite(GravityResistanceScalar) ? GravityResistanceScalar : SumpPumpPipeGridConstants.DefaultGravityResistanceScalar);
            float multiplier = math.select(1f, math.lerp(1f, assist, gravityWeight), destinationLower);
            multiplier = math.select(multiplier, math.lerp(1f, resistance, gravityWeight), destinationHigher);
            return conductance * multiplier;
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
    public unsafe struct ExecuteWaterEvacuationJob : IJobParallelFor
    {
        // Pointer safety: runtime locks all Vault-backed lanes before scheduling and releases them only after
        // the dispatcher completion window. FrontCompartments is a read-only owner snapshot; this job mutates
        // only BackCompartments, guarded by 64-byte room locks to prevent cross-thread water writes.
        //
        // Rejected alternative: copying FluidCompartmentDTO rows into a Construction-owned shadow lane would
        // break GlobalDataVault type identity and make Physics rollback snapshots stale. NativeArray copy/writeback
        // would also widen the contended path from one atomic field update to full 64-byte row stores.
        //
        // Scheduling invariant: each pump indexes one room through PumpRoomIndices; out-of-range rooms fail closed,
        // lock acquisition is bounded, and CompareExchange mutates only CurrentWaterVolume. The job never writes
        // FrontCompartments and never touches adjacent lock rows because DrainageRoomDrainLock64 is cache-line sized.
        [NoAlias, NativeDisableUnsafePtrRestriction] public DrainageNodeDTO* PumpNodes;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public FluidCompartmentDTO* FrontCompartments;
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

            ref DrainageNodeDTO pump = ref UnsafeUtility.AsRef<DrainageNodeDTO>(PumpNodes + index);
            float deltaTime = math.isfinite(DeltaTime) ? math.max(0f, DeltaTime) : 0f;
            if ((pump.Flags & (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump)) != (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump))
            {
                pump.CurrentFlow = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            int roomIndex = ReadInt(PumpRoomIndices, index, -1);
            if ((uint)roomIndex >= (uint)CompartmentCount || FrontCompartments == null || BackCompartments == null || RoomDrainLocks == null || (uint)roomIndex >= (uint)RoomDrainLockCount)
            {
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
                pump.CurrentFlow = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            float quantum = math.max(0.000001f, math.isfinite(MassQuantumM3) ? MassQuantumM3 : SumpPumpPipeGridConstants.DefaultMassQuantumM3);
            float evacuationRate = math.isfinite(pump.CurrentFlow) ? math.max(0f, pump.CurrentFlow) : 0f;
            float oldRemainder = ReadFloat(PumpRemainderM3, index, 0f);
            float requested = evacuationRate * deltaTime;
            requested += oldRemainder;
            if (!math.isfinite(requested))
            {
                pump.Flags |= SumpPumpNodeFlags.NonFinite;
                WriteFloat(PumpRemainderM3, index, 0f);
                pump.CurrentFlow = 0f;
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
                pump.CurrentFlow = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            float quantizedM3 = quantizedUnits * quantum;
            float remainder = clippedQuantizedUnits ? 0f : math.max(0f, requested - quantizedM3);
            if (!TryAcquireRoomLock(roomIndex))
            {
                WriteFloat(PumpRemainderM3, index, oldRemainder);
                pump.CurrentFlow = 0f;
                WriteFloat(PumpMassErrorM3, index, 0f);
                return;
            }

            ref FluidCompartmentDTO front = ref FluidCompartmentPointerUtility.ElementRef(FrontCompartments, roomIndex);
            ref FluidCompartmentDTO back = ref FluidCompartmentPointerUtility.ElementRef(BackCompartments, roomIndex);
            float frontWater = ReadCompartmentWater(ref front);
            float backWater = ReadCompartmentWater(ref back);
            float availableWater = math.min(frontWater, backWater);
            float actualDrained = math.min(availableWater, quantizedM3);
            if (actualDrained > 0f)
            {
                if (!TryDeductWaterAtomic(ref back, actualDrained, out float backDrained))
                {
                    ReleaseRoomLock(roomIndex);
                    WriteFloat(PumpRemainderM3, index, oldRemainder);
                    pump.Flags |= SumpPumpNodeFlags.NonFinite;
                    pump.CurrentFlow = 0f;
                    WriteFloat(PumpMassErrorM3, index, actualDrained);
                    return;
                }

                actualDrained = backDrained;
            }

            ReleaseRoomLock(roomIndex);
            WriteFloat(PumpRemainderM3, index, remainder);
            pump.CurrentFlow = deltaTime > 0f ? actualDrained * math.rcp(deltaTime) : 0f;

            float conservativeError = math.abs(frontWater - back.CurrentWaterVolume);
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

        private static float ReadCompartmentWater(ref FluidCompartmentDTO dto)
        {
            float water = dto.CurrentWaterVolume;
            if (!math.isfinite(water))
            {
                return 0f;
            }

            float maxVolume = dto.MaxWaterVolume;
            if (!math.isfinite(maxVolume) || maxVolume <= 0f)
            {
                return 0f;
            }

            return math.clamp(water, 0f, maxVolume);
        }

        private static bool TryDeductWaterAtomic(ref FluidCompartmentDTO dto, float requested, out float drained)
        {
            drained = 0f;
            float maxVolume = dto.MaxWaterVolume;
            if (!math.isfinite(maxVolume) || maxVolume <= 0f || requested <= 0f || !math.isfinite(requested))
                return false;

            unsafe
            {
                int* bits = (int*)UnsafeUtility.AddressOf(ref dto.CurrentWaterVolume);
                int oldBits = *bits;
                float oldWater = math.asfloat(oldBits);
                if (!math.isfinite(oldWater))
                {
                    dto.Flags |= FluidCompartmentFlags.NonFinite;
                    return false;
                }

                oldWater = math.clamp(oldWater, 0f, maxVolume);
                float nextWater = math.max(0f, oldWater - requested);
                int nextBits = math.asint(nextWater);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, oldBits) != oldBits)
                    return false;

                drained = oldWater - nextWater;
                dto.WaterLevelHeight01 = ResolveFill01(nextWater, maxVolume);
                return true;
            }
        }

        private static float ResolveFill01(float volume, float maxVolume)
        {
            return maxVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(volume * math.rcp(maxVolume))
                : 0f;
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
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public DrainageNodeDTO* PumpNodes;
        [NoAlias, ReadOnly] public NativeArray<float> Pressure;
        [NoAlias, ReadOnly] public NativeArray<float> PumpMassErrorM3;
        [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public NativeArray<DrainageTuningDTO> Tuning;
        [NoAlias] public NativeArray<DrainageTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<DrainageTelemetryEntry> FrameSummary;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int NodeCount;
        public int EdgeCount;
        public int DeltaPassCount;
        public uint FrameIndex;
        public float GlobalQualityWeight;
        public float PumpPowerDrawWatts;
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
                    DrainageNodeDTO pump = UnsafeUtility.AsRef<DrainageNodeDTO>(PumpNodes + i);
                    float evacuationRate = math.isfinite(pump.CurrentFlow) ? math.max(0f, pump.CurrentFlow) : 0f;
                    if ((pump.Flags & SumpPumpNodeFlags.NonFinite) != 0u || !math.isfinite(pump.CurrentFlow))
                        nanCount++;
                    if (evacuationApplied &&
                        (pump.Flags & (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump)) == (SumpPumpNodeFlags.Active | SumpPumpNodeFlags.Pump) &&
                        evacuationRate > 0f)
                    {
                        activePumpCount++;
                        frameEvacuated += evacuationRate * deltaTime;
                        float maxRate = math.max(0f, math.isfinite(pump.MaxPumpRate) ? pump.MaxPumpRate : 0f);
                        float utilization = maxRate > 0.000001f ? math.saturate(evacuationRate * math.rcp(maxRate)) : 0f;
                        powerDraw += math.max(0f, math.isfinite(PumpPowerDrawWatts) ? PumpPowerDrawWatts : 0f) * utilization;
                    }

                    float pumpMassError = ReadFloat(PumpMassErrorM3, i, 0f);
                    if (evacuationApplied)
                        massError += pumpMassError;
                    stateHash = SumpPumpPipeGridValidation.MixHash(stateHash, pump.NodeHashID);
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
                tuning.DeltaPassCount = (ushort)math.min(ushort.MaxValue, math.max(0, DeltaPassCount));
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

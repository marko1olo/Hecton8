using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class HabitatFluidIncursionMath
    {
        public const float AuthoritativeQualityWeight = 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveIngressVolume(
            float currentVolume,
            float maxVolume,
            float breachAreaSquareMeters,
            float depthMeters,
            float fixedDeltaTime,
            float dischargeCoefficient,
            float maximumIngressPerSecondNormalized,
            float gravityMetersPerSecondSquared,
            float epsilon)
        {
            float safeMaxVolume = math.max(0f, maxVolume);
            float safeCurrentVolume = math.clamp(currentVolume, 0f, safeMaxVolume);
            float remainingCapacity = safeMaxVolume - safeCurrentVolume;
            if (breachAreaSquareMeters <= epsilon || remainingCapacity <= epsilon)
                return safeCurrentVolume;

            float ingressVelocity = ResolveTorricelliIngressVelocity(depthMeters, gravityMetersPerSecondSquared);
            float cd = math.clamp(dischargeCoefficient, HectonPhysicsContract.FluidDischargeCoefficientMin, 1f);
            float deltaVolume = ingressVelocity * breachAreaSquareMeters * cd * math.max(0f, fixedDeltaTime);
            if (!math.isfinite(deltaVolume))
                deltaVolume = 0f;

            float maxIngressScale = math.max(HectonPhysicsContract.FluidMaximumIngressScaleMin, maximumIngressPerSecondNormalized) * math.max(0f, fixedDeltaTime);
            float maxIngressThisStep = safeMaxVolume * maxIngressScale;
            deltaVolume = math.clamp(deltaVolume, 0f, math.min(remainingCapacity, maxIngressThisStep));
            return safeCurrentVolume + deltaVolume;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeCubeRoot(float value)
        {
            float safeValue = math.max(0f, value);
            if (safeValue <= 0f)
                return 0f;

            float estimate = math.asfloat((math.asint(safeValue) / 3) + HectonPhysicsContract.CubeRootMagicBias);
            float estimateSq = math.max(estimate * estimate, HectonPhysicsContract.FluidSqrtEpsilon);
            estimate = ((estimate + estimate) + safeValue * math.rcp(estimateSq)) * HectonPhysicsContract.CubeRootNewtonOneThird;
            return math.isfinite(estimate) ? estimate : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveTorricelliIngressVelocity(float depthMeters, float gravityMetersPerSecondSquared)
        {
            float safeDepth = math.max(0f, depthMeters);
            float safeGravity = math.max(0f, gravityMetersPerSecondSquared);
            float velocity = ApproximateSqrtPositive(2f * safeGravity * safeDepth);
            return math.isfinite(velocity) ? velocity : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateSqrtPositive(float value)
        {
            float safeValue = math.max(0f, value);
            if (safeValue <= 0f)
                return 0f;

            float magnitude = safeValue * math.rsqrt(math.max(safeValue, HectonPhysicsContract.FluidSqrtEpsilon));
            return math.isfinite(magnitude) ? magnitude : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ResolveAupMeters(in AbsoluteUniversePositionBlit origin, float3 localOffset)
        {
            return new double3(
                ((double)origin.GridX * HabitatFluidIncursionConstants.AupCellSizeMeters) + origin.Local.x + localOffset.x,
                ((double)origin.GridY * HabitatFluidIncursionConstants.AupCellSizeMeters) + origin.Local.y + localOffset.y,
                ((double)origin.GridZ * HabitatFluidIncursionConstants.AupCellSizeMeters) + origin.Local.z + localOffset.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ResolveAupYMeters(in AbsoluteUniversePositionBlit aup)
        {
            return ((double)aup.GridY * HabitatFluidIncursionConstants.AupCellSizeMeters) + aup.Local.y;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidCompartmentClearJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Compartments and Integrity are owner-resolved contiguous lanes. The job validates index < ActiveCount before
        // writing each row, while NativeArray side lanes carry their own safety handles.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Managed compartment objects were rejected because the solver is Burst/rollback-facing. A temporary clear buffer
        // was rejected because it adds a copyback pass for data this job can overwrite deterministically.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one compartment/integrity row per worker index; no reader is scheduled until this clear handle is fenced.
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* Compartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public IntegrityStateDTO* Integrity;
        [NoAlias] public NativeArray<float3> LocalCentroids;
        [NoAlias] public NativeArray<FluidWaterlineShaderDTO> Waterlines;
        public AbsoluteUniversePositionBlit OriginAup;
        public uint NodeHashSeed;
        public float DefaultVolumeM3;
        public float DefaultFloorHeightLocal;
        public int ActiveCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveCount)
                return;

            uint nodeHash = NodeHashSeed + (uint)(index * 2654435761u);
            float laneX = (index & 7) * 3.2f;
            float laneZ = (index >> 3) * 3.2f;
            float3 centroid = new float3(laneX, DefaultFloorHeightLocal, laneZ);

            ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, index);
            dto.LocalCenterOfMass = HabitatFluidIncursionMath.ResolveAupMeters(in OriginAup, centroid);
            dto.NodeHashID = nodeHash;
            dto.MaxWaterVolume = math.max(0.001f, DefaultVolumeM3);
            dto.CurrentWaterVolume = 0f;
            dto.WaterLevelHeight01 = 0f;
            dto.Flags = 0u;

            LocalCentroids[index] = centroid;
            Waterlines[index] = new FluidWaterlineShaderDTO
            {
                Fill01 = 0f,
                WaterlineLocalY = centroid.y,
                Wobble01 = 0f,
                NodeHash = nodeHash
            };

            IntegrityStateDTO integrity = default;
            integrity.CenterAup = OriginAup;
            integrity.CenterAup.Local.x += centroid.x;
            integrity.CenterAup.Local.y += centroid.y;
            integrity.CenterAup.Local.z += centroid.z;
            integrity.NodeHash = nodeHash;
            integrity.Integrity01 = 1f;
            integrity.BreachAreaM2 = 0f;
            integrity.Flags = 0u;
            Integrity[index] = integrity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MockHullBreachJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Mock breach mutates exactly one compartment and one integrity row selected by clamped BreachIndex. Raw pointers
        // are required because the director owns the active compartment buffers outside this job.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A managed mock event was rejected because it would allocate and delay deterministic seeding. Duplicating the
        // active buffers was rejected because it creates shadow state.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is a single writer job for the selected mock breach row before ingress/equalization jobs are scheduled.
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* Compartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public IntegrityStateDTO* Integrity;
        public int CompartmentCount;
        public int BreachIndex;
        public float BreachAreaM2;
        public float IngressRateM3PerSecond;

        public void Execute()
        {
            if (CompartmentCount <= 0)
                return;

            int index = math.clamp(BreachIndex, 0, CompartmentCount - 1);
            ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, index);
            dto.Flags |= FluidCompartmentFlags.Breached | FluidCompartmentFlags.MockBreach;

            IntegrityStateDTO integrity = Integrity[index];
            integrity.Flags |= IntegrityStateDTO.FlagBreached | IntegrityStateDTO.FlagMockSource;
            integrity.Integrity01 = math.min(integrity.Integrity01, 0.35f);
            integrity.BreachAreaM2 = math.max(0.0001f, BreachAreaM2);
            Integrity[index] = integrity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockFloodIncursionJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* Compartments;
        public int CompartmentCount;
        public int TargetNodeIndex;
        public float AddedWaterM3;

        public void Execute()
        {
            if (CompartmentCount <= 0)
                return;

            int index = math.clamp(TargetNodeIndex, 0, CompartmentCount - 1);
            ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, index);
            float maxVolume = math.max(HabitatFluidIncursionConstants.WaterEpsilonM3, dto.MaxWaterVolume);
            float current = math.isfinite(dto.CurrentWaterVolume) ? math.clamp(dto.CurrentWaterVolume, 0f, maxVolume) : 0f;
            float next = math.min(maxVolume, current + math.max(0f, AddedWaterM3));
            dto.CurrentWaterVolume = next;
            dto.WaterLevelHeight01 = math.saturate(next * math.rcp(maxVolume));
            dto.Flags |= FluidCompartmentFlags.MockBreach;
            if (dto.WaterLevelHeight01 >= 0.995f)
                dto.Flags |= FluidCompartmentFlags.Flooded;
            else
                dto.Flags &= ~FluidCompartmentFlags.Flooded;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidIngressJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // ReadCompartments, WriteCompartments, and Integrity are owner-provided non-overlapping lanes. The job validates
        // index and writes only the matching compartment output row.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // In-place ingress was rejected because pressure equalization needs a stable read buffer. Managed event expansion
        // was rejected because this is a Burst hot path.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is double-buffered compartment ownership: ReadCompartments is immutable, WriteCompartments[index]
        // is exclusively written by worker index N, and Integrity is read-only during this stage.
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public FluidCompartmentDTO* ReadCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* WriteCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public IntegrityStateDTO* Integrity;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // IncursionWriter is the only FluidIngressJob output queue lane. Execute only enqueues
        // FluidIncursionSignal payloads and never drains, resizes, disposes, or reads the queue.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // HabitatFluidIncursionDirector schedules FluidIngressJob first, then chains BFS, mass,
        // and telemetry jobs into _simulationHandle. PostFixedTick swaps buffers only after
        // DispatcherJobFence.TryFinalizeCompleted observes that handle as finished.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Authoritative topology/CSV/mock writes call CompleteScheduledSimulationForAuthoritativeWrite
        // before mutating Vault lanes. TryLockJobBuffers keeps front/back buffers locked until the
        // chained handle is fenced, so no second writer mutates the SignalBus lane or backing buffers.
        [NoAlias, NativeDisableContainerSafetyRestriction]
        public global::Hecton8.Core.MpscSignalRingBuffer<FluidIncursionSignal>.ParallelWriter IncursionWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> IncursionWriterBudget;
        public int CompartmentCount;
        public float DeltaTime;
        public float GlobalQualityWeight;
        public float DischargeCoefficient;
        public float MaxIngressPerSecondNormalized;
        public AbsoluteUniversePositionBlit ExternalWaterlineAup;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)CompartmentCount)
                return;

            FluidCompartmentDTO dto = FluidCompartmentPointerUtility.ElementRef(ReadCompartments, index);
            IntegrityStateDTO integrity = Integrity[index];
            float currentWater = SanitizeWater(dto.CurrentWaterVolume, dto.MaxWaterVolume, ref dto);
            dto.CurrentWaterVolume = currentWater;
            dto.WaterLevelHeight01 = ResolveFill01(currentWater, dto.MaxWaterVolume);

            bool breached =
                (dto.Flags & FluidCompartmentFlags.Breached) != 0u ||
                (integrity.Flags & IntegrityStateDTO.FlagBreached) != 0u;
            bool isSealed = (dto.Flags & FluidCompartmentFlags.Isolated) != 0u ||
                            (integrity.Flags & IntegrityStateDTO.FlagSealed) != 0u;
            float breachArea = math.max(0f, integrity.BreachAreaM2);
            if (breached && !isSealed && breachArea > HabitatFluidIncursionConstants.WaterEpsilonM3)
            {
                float depthMeters = math.max(0f, ResolveWaterlineDepthMeters(in dto));
                float maxIngress = math.max(0.08f, MaxIngressPerSecondNormalized);
                float nextWater = FluidMathCore.ResolveIngressVolume(
                    currentWater,
                    dto.MaxWaterVolume,
                    breachArea,
                    depthMeters,
                    DeltaTime,
                    DischargeCoefficient,
                    maxIngress,
                    HabitatFluidIncursionConstants.GravityMetersPerSecondSq,
                    HabitatFluidIncursionConstants.WaterEpsilonM3);

                float delta = math.max(0f, nextWater - currentWater);
                dto.CurrentWaterVolume = nextWater;
                dto.WaterLevelHeight01 = ResolveFill01(nextWater, dto.MaxWaterVolume);
                dto.Flags |= FluidCompartmentFlags.Breached;
                if (delta > HabitatFluidIncursionConstants.WaterEpsilonM3 &&
                    !PublishIncursion(in integrity, in dto, delta))
                {
                    dto.Flags |= FluidCompartmentFlags.SignalOverflow;
                }
            }

            if (dto.CurrentWaterVolume > dto.MaxWaterVolume - HabitatFluidIncursionConstants.WaterEpsilonM3)
                dto.Flags |= FluidCompartmentFlags.Flooded;
            else
                dto.Flags &= ~FluidCompartmentFlags.Flooded;

            FluidCompartmentPointerUtility.ElementRef(WriteCompartments, index) = dto;
        }

        private bool PublishIncursion(in IntegrityStateDTO integrity, in FluidCompartmentDTO dto, float deltaM3)
        {
            FluidIncursionSignal signal = default;
            signal.LeakAup = integrity.CenterAup.ToAup();
            signal.CompartmentId = dto.NodeHashID;
            signal.FloodLevel01 = dto.MaxWaterVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(dto.CurrentWaterVolume * math.rcp(dto.MaxWaterVolume))
                : 0f;
            signal.FlowRate01 = math.saturate(deltaM3 * math.rcp(math.max(HabitatFluidIncursionConstants.WaterEpsilonM3, dto.MaxWaterVolume)));
            signal.Flags = (byte)((dto.Flags & FluidCompartmentFlags.MockBreach) != 0u ? 1 : 0);
            return SignalBus<FluidIncursionSignal>.TryEnqueueBounded(IncursionWriter, IncursionWriterBudget, signal);
        }

        private static float SanitizeWater(float value, float maxVolume, ref FluidCompartmentDTO dto)
        {
            if (!math.isfinite(value) || !math.isfinite(maxVolume) || maxVolume <= 0f)
            {
                dto.Flags |= FluidCompartmentFlags.NonFinite;
                return 0f;
            }

            if (value > maxVolume)
            {
                dto.Flags |= FluidCompartmentFlags.OverflowClamped;
                return maxVolume;
            }

            return math.max(0f, value);
        }

        private float ResolveWaterlineDepthMeters(in FluidCompartmentDTO dto)
        {
            double externalY = HabitatFluidIncursionMath.ResolveAupYMeters(in ExternalWaterlineAup);
            double depth = externalY - dto.LocalCenterOfMass.y;
            return (float)math.clamp(depth, -100000d, 100000d);
        }

        private static float ResolveFill01(float volume, float maxVolume)
        {
            return maxVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(volume * math.rcp(maxVolume))
                : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidBfsPressureEqualizationJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Compartments is the active pressure graph lane and Integrity is read-only breach metadata. Raw pointer access is
        // bounded by the graph counts and BFS queue limits owned by the director.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A recursive managed graph traversal was rejected for stack/GC risk. Per-edge job fanout was rejected because the
        // graph is small enough that extra scheduling would cost more than the solve.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is a single BFS writer over Compartments for the frame; no other compartment writer runs until
        // this job's handle completes.
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* Compartments;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public IntegrityStateDTO* Integrity;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<byte> EdgeFlags;
        [NoAlias, ReadOnly] public NativeArray<float> EdgeConductivity;
        [NoAlias] public NativeArray<int> BfsQueue;
        [NoAlias] public NativeArray<byte> BfsVisited;
        [NoAlias] public NativeArray<float> DeltaVolumes;
        [NoAlias] public NativeArray<float> TransferRemainders;
        public int CompartmentCount;
        public int EdgeCount;
        public int SolverIterations;
        public int MaxVisitedNodes;
        public float DeltaTime;
        public float TransferRate01PerSecond;
        public float MaxTransferPerNodeM3;
        [NoAlias] public NativeArray<FluidIncursionFrameSummaryDTO> Summary;

        public void Execute()
        {
            int safeCount = math.min(CompartmentCount, BfsQueue.Length);
            safeCount = math.min(safeCount, math.min(BfsVisited.Length, DeltaVolumes.Length));
            int safeEdgeCount = math.min(EdgeCount, math.min(EdgeDestinations.Length, EdgeFlags.Length));
            if (EdgeConductivity.IsCreated)
                safeEdgeCount = math.min(safeEdgeCount, EdgeConductivity.Length);
            if (TransferRemainders.IsCreated)
                safeEdgeCount = math.min(safeEdgeCount, TransferRemainders.Length);
            if (safeCount <= 0 ||
                safeEdgeCount <= 0 ||
                !EdgeOffsets.IsCreated ||
                !EdgeConductivity.IsCreated ||
                !TransferRemainders.IsCreated ||
                EdgeOffsets.Length < safeCount + 1)
            {
                return;
            }

            int iterations = math.clamp(SolverIterations, HabitatFluidIncursionConstants.MinSolverIterations, HabitatFluidIncursionConstants.MaxSolverIterations);
            EdgeCount = safeEdgeCount;
            ushort sealedEdgeCount = 0;
            ushort invalidCount = 0;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int nodeIndex = 0; nodeIndex < safeCount; nodeIndex++)
                {
                    BfsVisited[nodeIndex] = 0;
                    DeltaVolumes[nodeIndex] = 0f;
                }

                for (int seed = 0; seed < safeCount; seed++)
                {
                    if (BfsVisited[seed] != 0)
                        continue;

                    int componentCount = BuildComponent(seed, safeCount, ref sealedEdgeCount, ref invalidCount);
                    AccumulateComponentTransfers(componentCount, safeCount, ref sealedEdgeCount, ref invalidCount);
                    ApplyComponentDeltas(componentCount);
                }
            }

            if (Summary.IsCreated && Summary.Length > 0)
            {
                FluidIncursionFrameSummaryDTO summary = Summary[0];
                summary.SealedEdgeCount = sealedEdgeCount;
                summary.InvalidCount = (ushort)math.min(ushort.MaxValue, summary.InvalidCount + invalidCount);
                Summary[0] = summary;
            }
        }

        private int BuildComponent(int seed, int safeCount, ref ushort sealedEdgeCount, ref ushort invalidCount)
        {
            int head = 0;
            int tail = 0;
            BfsQueue[tail++] = seed;
            BfsVisited[seed] = 1;

            while (head < tail)
            {
                int node = BfsQueue[head++];
                ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, node);
                if ((dto.Flags & FluidCompartmentFlags.Isolated) != 0u)
                    continue;

                int start = math.clamp(EdgeOffsets[node], 0, EdgeCount);
                int end = math.clamp(EdgeOffsets[node + 1], start, EdgeCount);
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    if (IsSealedEdge(edgeIndex))
                    {
                        sealedEdgeCount = SaturatingInc(sealedEdgeCount);
                        continue;
                    }

                    int destination = EdgeDestinations[edgeIndex];
                    if ((uint)destination >= (uint)safeCount)
                    {
                        invalidCount = SaturatingInc(invalidCount);
                        continue;
                    }

                    ref FluidCompartmentDTO destinationDto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, destination);
                    if ((destinationDto.Flags & FluidCompartmentFlags.Isolated) != 0u)
                        continue;

                    if (BfsVisited[destination] != 0 || tail >= BfsQueue.Length)
                        continue;
                    if (tail >= ResolveVisitLimit(safeCount))
                        continue;

                    BfsVisited[destination] = 1;
                    BfsQueue[tail++] = destination;
                }
            }

            return tail;
        }

        private void AccumulateComponentTransfers(int componentCount, int safeCount, ref ushort sealedEdgeCount, ref ushort invalidCount)
        {
            for (int queueIndex = 0; queueIndex < componentCount; queueIndex++)
            {
                int node = BfsQueue[queueIndex];
                ref FluidCompartmentDTO source = ref FluidCompartmentPointerUtility.ElementRef(Compartments, node);
                if ((source.Flags & FluidCompartmentFlags.Isolated) != 0u)
                    continue;

                int start = math.clamp(EdgeOffsets[node], 0, EdgeCount);
                int end = math.clamp(EdgeOffsets[node + 1], start, EdgeCount);
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    float conductivity = ResolveEdgeConductivity(edgeIndex);
                    if (conductivity <= HabitatFluidIncursionConstants.WaterEpsilonM3)
                    {
                        sealedEdgeCount = SaturatingInc(sealedEdgeCount);
                        continue;
                    }

                    int destination = EdgeDestinations[edgeIndex];
                    if ((uint)destination >= (uint)safeCount || node > destination)
                    {
                        if ((uint)destination >= (uint)safeCount)
                            invalidCount = SaturatingInc(invalidCount);
                        continue;
                    }

                    ref FluidCompartmentDTO destinationDto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, destination);
                    float maxTransfer = math.max(
                        HabitatFluidIncursionConstants.WaterEpsilonM3,
                        MaxTransferPerNodeM3 * math.max(0f, DeltaTime));
                    float headDifferenceMeters = ResolveSurfaceHeadDifferenceMeters(
                        in source,
                        in destinationDto);
                    float delta = ResolvePotentialTransferDelta(
                        source.CurrentWaterVolume,
                        destinationDto.CurrentWaterVolume,
                        source.MaxWaterVolume,
                        destinationDto.MaxWaterVolume,
                        headDifferenceMeters,
                        DeltaTime,
                        TransferRate01PerSecond * conductivity,
                        maxTransfer,
                        0.74f,
                        0.02f,
                        HabitatFluidIncursionConstants.GravityMetersPerSecondSq,
                        HabitatFluidIncursionConstants.WaterEpsilonM3);

                    if (delta == 0f)
                        continue;

                    int transferMilliliters = QuantizeTransferMilliliters(
                        edgeIndex,
                        delta,
                        node,
                        destination,
                        in source,
                        in destinationDto);
                    if (transferMilliliters == 0)
                        continue;

                    DeltaVolumes[node] -= transferMilliliters;
                    DeltaVolumes[destination] += transferMilliliters;
                }
            }
        }

        private void ApplyComponentDeltas(int componentCount)
        {
            for (int queueIndex = 0; queueIndex < componentCount; queueIndex++)
            {
                int node = BfsQueue[queueIndex];
                ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, node);
                float nextWater = dto.CurrentWaterVolume + (DeltaVolumes[node] * HabitatFluidIncursionConstants.CubicMetersPerMilliliter);
                if (!math.isfinite(nextWater))
                {
                    dto.Flags |= FluidCompartmentFlags.NonFinite;
                    dto.CurrentWaterVolume = 0f;
                    dto.WaterLevelHeight01 = 0f;
                    continue;
                }

                float maxVolume = math.max(0f, dto.MaxWaterVolume);
                dto.CurrentWaterVolume = math.clamp(nextWater, 0f, maxVolume);
                dto.WaterLevelHeight01 = ResolveFill01(dto.CurrentWaterVolume, maxVolume);
                if (dto.CurrentWaterVolume >= maxVolume - HabitatFluidIncursionConstants.WaterEpsilonM3)
                    dto.Flags |= FluidCompartmentFlags.Flooded;
                else
                    dto.Flags &= ~FluidCompartmentFlags.Flooded;
            }
        }

        private bool IsSealedEdge(int edgeIndex)
        {
            return (uint)edgeIndex >= (uint)EdgeFlags.Length ||
                   (EdgeFlags[edgeIndex] & FluidEdgeFlags.Sealed) != 0;
        }

        private float ResolveEdgeConductivity(int edgeIndex)
        {
            if (IsSealedEdge(edgeIndex) || (uint)edgeIndex >= (uint)EdgeConductivity.Length)
                return 0f;

            float value = EdgeConductivity[edgeIndex];
            return math.isfinite(value) ? math.clamp(value, 0f, 1f) : 0f;
        }

        private int ResolveVisitLimit(int safeCount)
        {
            int requested = MaxVisitedNodes <= 0 ? safeCount : MaxVisitedNodes;
            return math.clamp(requested, 1, math.min(safeCount, BfsQueue.Length));
        }

        private int QuantizeTransferMilliliters(
            int edgeIndex,
            float deltaM3,
            int sourceNode,
            int destinationNode,
            in FluidCompartmentDTO source,
            in FluidCompartmentDTO destination)
        {
            if ((uint)edgeIndex >= (uint)TransferRemainders.Length || !math.isfinite(deltaM3))
                return 0;

            float desiredMilliliters = (deltaM3 * HabitatFluidIncursionConstants.MillilitersPerCubicMeter) + TransferRemainders[edgeIndex];
            int wholeMilliliters = desiredMilliliters >= 0f ? (int)math.floor(desiredMilliliters) : (int)math.ceil(desiredMilliliters);
            TransferRemainders[edgeIndex] = desiredMilliliters - wholeMilliliters;
            if (wholeMilliliters > 0)
            {
                int available = math.max(0, (int)math.floor(source.CurrentWaterVolume * HabitatFluidIncursionConstants.MillilitersPerCubicMeter) + (int)DeltaVolumes[sourceNode]);
                int capacity = math.max(0, (int)math.floor((destination.MaxWaterVolume - destination.CurrentWaterVolume) * HabitatFluidIncursionConstants.MillilitersPerCubicMeter) - (int)DeltaVolumes[destinationNode]);
                return math.min(wholeMilliliters, math.min(available, capacity));
            }

            if (wholeMilliliters < 0)
            {
                int available = math.max(0, (int)math.floor(destination.CurrentWaterVolume * HabitatFluidIncursionConstants.MillilitersPerCubicMeter) + (int)DeltaVolumes[destinationNode]);
                int capacity = math.max(0, (int)math.floor((source.MaxWaterVolume - source.CurrentWaterVolume) * HabitatFluidIncursionConstants.MillilitersPerCubicMeter) - (int)DeltaVolumes[sourceNode]);
                return -math.min(-wholeMilliliters, math.min(available, capacity));
            }

            return 0;
        }

        private static ushort SaturatingInc(ushort value)
        {
            return value == ushort.MaxValue ? value : (ushort)(value + 1);
        }

        private static float ResolveSurfaceHeadDifferenceMeters(
            in FluidCompartmentDTO source,
            in FluidCompartmentDTO destination)
        {
            float sourceFill = ResolveFill01(source.CurrentWaterVolume, source.MaxWaterVolume);
            float destinationFill = ResolveFill01(destination.CurrentWaterVolume, destination.MaxWaterVolume);
            float sourceHeight = math.max(0.25f, FluidMathCore.SafeCubeRoot(source.MaxWaterVolume));
            float destinationHeight = math.max(0.25f, FluidMathCore.SafeCubeRoot(destination.MaxWaterVolume));
            double centerDeltaY = source.LocalCenterOfMass.y - destination.LocalCenterOfMass.y;
            double surfaceDeltaY = (sourceFill * sourceHeight) - (destinationFill * destinationHeight);
            return (float)math.clamp(centerDeltaY + surfaceDeltaY, -100000d, 100000d);
        }

        private static float ResolvePotentialTransferDelta(
            float sourceVolume,
            float destinationVolume,
            float sourceMaxVolume,
            float destinationMaxVolume,
            float headDifferenceMeters,
            float fixedDeltaTime,
            float bulkheadFlowCoefficient,
            float maxTransferPerTick,
            float dischargeCoefficient,
            float nearZeroHeadDampingMeters,
            float gravityMetersPerSecondSquared,
            float epsilon)
        {
            if (!math.isfinite(headDifferenceMeters))
                return 0f;

            float absHeadDifferenceMeters = math.abs(headDifferenceMeters);
            float dampingFactor = math.smoothstep(0f, math.max(epsilon, nearZeroHeadDampingMeters), absHeadDifferenceMeters);
            if (dampingFactor <= epsilon)
                return 0f;

            float velocityMetersPerSecond = HabitatFluidIncursionMath.ApproximateSqrtPositive(
                2f * math.max(0f, gravityMetersPerSecondSquared) * absHeadDifferenceMeters);
            if (!math.isfinite(velocityMetersPerSecond))
                return 0f;

            float signedDeltaVolume =
                math.sign(headDifferenceMeters) *
                math.max(epsilon, dischargeCoefficient) *
                velocityMetersPerSecond *
                math.max(0f, bulkheadFlowCoefficient) *
                math.max(0f, fixedDeltaTime) *
                dampingFactor;
            float deltaVolume = math.clamp(signedDeltaVolume, -math.max(0.01f, maxTransferPerTick), math.max(0.01f, maxTransferPerTick));

            if (deltaVolume > 0f)
                deltaVolume = math.min(deltaVolume, math.min(math.max(0f, sourceVolume), math.max(0f, destinationMaxVolume - destinationVolume)));
            else if (deltaVolume < 0f)
                deltaVolume = -math.min(-deltaVolume, math.min(math.max(0f, destinationVolume), math.max(0f, sourceMaxVolume - sourceVolume)));

            return math.abs(deltaVolume) <= epsilon || !math.isfinite(deltaVolume) ? 0f : deltaVolume;
        }

        private static float ResolveFill01(float volume, float maxVolume)
        {
            if (!math.isfinite(volume) || !math.isfinite(maxVolume) || maxVolume <= HabitatFluidIncursionConstants.WaterEpsilonM3)
                return 0f;

            return math.saturate(volume * math.rcp(maxVolume));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidWaterlineMassSummaryJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Compartments is read-only mass input while Waterlines, CompartmentTelemetry, and MassState are separate NativeArray
        // outputs. The raw compartment pointer is bounded by the caller-provided active counts.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Copying compartment rows into a NativeArray facade was rejected because it duplicates bandwidth. Managed summary
        // objects were rejected because the telemetry row must remain blittable and Burst-friendly.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is read-only compartment traversal in this summary stage; all mutable output lanes are distinct
        // NativeArrays and are fenced by this returned handle.
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public FluidCompartmentDTO* Compartments;
        [NoAlias, ReadOnly] public NativeArray<float3> LocalCentroids;
        [NoAlias] public NativeArray<FluidWaterlineShaderDTO> Waterlines;
        [NoAlias] public NativeArray<FluidCompartmentTelemetryDTO> CompartmentTelemetry;
        [NoAlias] public NativeArray<FluidMassStateDTO> MassState;
        [NoAlias] public NativeArray<FluidIncursionFrameSummaryDTO> Summary;
        public int CompartmentCount;
        public int EdgeCount;
        public uint Frame;
        public uint SourceBodyId;
        public float BaseMassKg;
        public float WaterDensityKgPerM3;
        public float GlobalQualityWeight;
        public float VisualWobbleScalar;
        public byte MathLod;

        public void Execute()
        {
            int safeCount = math.min(CompartmentCount, LocalCentroids.Length);
            safeCount = math.min(safeCount, Waterlines.Length);
            float totalWaterM3 = 0f;
            float totalCapacityM3 = 0f;
            float maxFill01 = 0f;
            float peakIngressRate = 0f;
            float3 weightedWaterCenter = float3.zero;
            ushort floodedCount = 0;
            ushort breachedCount = 0;
            ushort invalidCount = 0;
            uint signalOverflowCount = 0u;
            uint stateHash = 2166136261u;

            for (int index = 0; index < safeCount; index++)
            {
                FluidCompartmentDTO dto = FluidCompartmentPointerUtility.ElementRef(Compartments, index);
                float maxVolume = math.max(HabitatFluidIncursionConstants.WaterEpsilonM3, dto.MaxWaterVolume);
                float water = dto.CurrentWaterVolume;
                if (!math.isfinite(water) || !math.isfinite(maxVolume))
                {
                    water = 0f;
                    invalidCount = SaturatingInc(invalidCount);
                }

                water = math.clamp(water, 0f, maxVolume);
                float fill01 = ResolveFill01(water, maxVolume);
                float height = math.max(0.25f, FluidMathCore.SafeCubeRoot(maxVolume));
                float wobble = math.saturate(fill01 * VisualWobbleScalar * math.saturate(GlobalQualityWeight));

                Waterlines[index] = new FluidWaterlineShaderDTO
                {
                    Fill01 = fill01,
                    WaterlineLocalY = LocalCentroids[index].y + (height * fill01),
                    Wobble01 = wobble,
                    NodeHash = dto.NodeHashID
                };

                if (CompartmentTelemetry.IsCreated && index < CompartmentTelemetry.Length)
                {
                    CompartmentTelemetry[index] = new FluidCompartmentTelemetryDTO
                    {
                        NodeHash = dto.NodeHashID,
                        CurrentWaterM3 = water,
                        MaxVolumeM3 = maxVolume,
                        Fill01 = fill01,
                        IngressRateM3PerSecond = 0f,
                        Flags = dto.Flags,
                        Frame = Frame,
                        CompartmentIndex = (ushort)math.min(ushort.MaxValue, index)
                    };
                }

                totalWaterM3 += water;
                totalCapacityM3 += maxVolume;
                maxFill01 = math.max(maxFill01, fill01);
                weightedWaterCenter += LocalCentroids[index] * water;
                if ((dto.Flags & FluidCompartmentFlags.Flooded) != 0u)
                    floodedCount = SaturatingInc(floodedCount);
                if ((dto.Flags & FluidCompartmentFlags.Breached) != 0u)
                    breachedCount = SaturatingInc(breachedCount);
                if ((dto.Flags & FluidCompartmentFlags.SignalOverflow) != 0u)
                    signalOverflowCount++;

                stateHash = MixHash(stateHash, dto.NodeHashID);
                stateHash = MixHash(stateHash, (uint)math.round(fill01 * 65535f));
                stateHash = MixHash(stateHash, dto.Flags);
            }

            float3 center = totalWaterM3 > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? weightedWaterCenter * math.rcp(totalWaterM3)
                : float3.zero;
            float waterMassKg = totalWaterM3 * math.max(1f, WaterDensityKgPerM3);
            float fillRatio = totalCapacityM3 > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(totalWaterM3 * math.rcp(totalCapacityM3))
                : 0f;
            float angularDragMultiplier = 1f + (fillRatio * 0.95f);

            if (MassState.IsCreated && MassState.Length > 0)
            {
                MassState[0] = new FluidMassStateDTO
                {
                    DynamicCenterOfMassLocal = center,
                    DynamicCenterOfMassOffsetLocal = center,
                    TotalWaterMassKg = waterMassKg,
                    BaseMassKg = math.max(0f, BaseMassKg),
                    FillRatio01 = fillRatio,
                    AngularDragMultiplier = angularDragMultiplier,
                    SourceBodyId = SourceBodyId,
                    Frame = Frame,
                    CompartmentCount = (ushort)math.min(ushort.MaxValue, safeCount),
                    MathLod = MathLod,
                    Flags = invalidCount > 0 ? (byte)1 : (byte)0
                };
            }

            if (Summary.IsCreated && Summary.Length > 0)
            {
                FluidIncursionFrameSummaryDTO summary = Summary[0];
                summary.Frame = Frame;
                summary.StateHash = stateHash;
                summary.TotalWaterM3 = totalWaterM3;
                summary.TotalWaterMassKg = waterMassKg;
                summary.MaxFill01 = maxFill01;
                summary.AverageFill01 = fillRatio;
                summary.PeakIngressRate = peakIngressRate;
                summary.FloodedCount = floodedCount;
                summary.BreachedCount = breachedCount;
                summary.InvalidCount = (ushort)math.min(ushort.MaxValue, summary.InvalidCount + invalidCount);
                summary.Flags = (invalidCount > 0 ? 1u : 0u) |
                                (signalOverflowCount > 0u ? 2u : 0u);
                summary.CenterOfMassLocal = center;
                summary.AcousticFloodIntensity01 = math.saturate(maxFill01 * 0.65f + fillRatio * 0.35f);
                summary.MathLod = MathLod;
                Summary[0] = summary;
            }
        }

        private static uint MixHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static ushort SaturatingInc(ushort value)
        {
            return value == ushort.MaxValue ? value : (ushort)(value + 1);
        }

        private static float ResolveFill01(float volume, float maxVolume)
        {
            return maxVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(volume * math.rcp(maxVolume))
                : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FluidTelemetryRecorderJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<FluidIncursionFrameSummaryDTO> Summary;
        [NoAlias] public NativeArray<FluidIncursionTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int TelemetryCapacity;
        public int CompartmentCount;
        public int EdgeCount;

        public void Execute()
        {
            if (!Summary.IsCreated ||
                !TelemetryRing.IsCreated ||
                !TelemetryCursor.IsCreated ||
                Summary.Length <= 0 ||
                TelemetryRing.Length <= 0 ||
                TelemetryCursor.Length <= 0)
            {
                return;
            }

            int capacity = math.min(math.max(1, TelemetryCapacity), TelemetryRing.Length);
            int cursor = TelemetryCursor[0];
            int writeIndex = cursor % capacity;
            if (writeIndex < 0)
                writeIndex += capacity;

            FluidIncursionFrameSummaryDTO summary = Summary[0];
            TelemetryRing[writeIndex] = new FluidIncursionTelemetryEntry
            {
                Frame = summary.Frame,
                StateHash = summary.StateHash,
                TotalWaterM3 = summary.TotalWaterM3,
                TotalWaterMassKg = summary.TotalWaterMassKg,
                MaxFill01 = summary.MaxFill01,
                AverageFill01 = summary.AverageFill01,
                PeakIngressRate = summary.PeakIngressRate,
                CompartmentCount = (ushort)math.min(ushort.MaxValue, CompartmentCount),
                FloodedCount = summary.FloodedCount,
                BreachedCount = summary.BreachedCount,
                EdgeCount = (ushort)math.min(ushort.MaxValue, EdgeCount),
                Flags = summary.Flags,
                CenterOfMassLocal = summary.CenterOfMassLocal,
                InvalidCount = summary.InvalidCount,
                SolverWallMicroseconds = summary.SolverWallMicroseconds
            };

            TelemetryCursor[0] = cursor + 1;
        }
    }
}

using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidCompartmentClearJob : IJobParallelFor
    {
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
            float3 centroid = new float3(laneX, 0f, laneZ);

            ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, index);
            dto.NodeHash = nodeHash;
            dto.MaxVolume = math.max(0.001f, DefaultVolumeM3);
            dto.CurrentWaterVolume = 0f;
            dto.FloorHeightLocal = DefaultFloorHeightLocal;
            dto.Flags = 0u;
            dto.IngressRate = 0f;
            dto._pad0 = 0;
            dto._pad1 = 0;
            dto._pad2 = 0;
            dto._pad3 = 0;
            dto._pad4 = 0;
            dto._pad5 = 0;
            dto._pad6 = 0;
            dto._pad7 = 0;

            LocalCentroids[index] = centroid;
            Waterlines[index] = new FluidWaterlineShaderDTO
            {
                Fill01 = 0f,
                WaterlineLocalY = DefaultFloorHeightLocal,
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
            dto.IngressRate = math.max(0f, IngressRateM3PerSecond);

            IntegrityStateDTO integrity = Integrity[index];
            integrity.Flags |= IntegrityStateDTO.FlagBreached | IntegrityStateDTO.FlagMockSource;
            integrity.Integrity01 = math.min(integrity.Integrity01, 0.35f);
            integrity.BreachAreaM2 = math.max(0.0001f, BreachAreaM2);
            Integrity[index] = integrity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidIngressJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public FluidCompartmentDTO* ReadCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* WriteCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public IntegrityStateDTO* Integrity;
        public NativeQueue<FluidIncursionSignal>.ParallelWriter IncursionWriter;
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
            float currentWater = SanitizeWater(dto.CurrentWaterVolume, dto.MaxVolume, ref dto);
            dto.CurrentWaterVolume = currentWater;
            dto.IngressRate = 0f;

            bool breached =
                (dto.Flags & FluidCompartmentFlags.Breached) != 0u ||
                (integrity.Flags & IntegrityStateDTO.FlagBreached) != 0u;
            bool sealed = (dto.Flags & FluidCompartmentFlags.Isolated) != 0u ||
                          (integrity.Flags & IntegrityStateDTO.FlagSealed) != 0u;
            float breachArea = math.max(0f, integrity.BreachAreaM2);
            if (breached && !sealed && breachArea > HabitatFluidIncursionConstants.WaterEpsilonM3)
            {
                float depthMeters = math.max(0f, ResolveWaterlineDepthMeters(in integrity, dto.FloorHeightLocal));
                float maxIngress = math.lerp(0.08f, math.max(0.08f, MaxIngressPerSecondNormalized), math.saturate(GlobalQualityWeight));
                float nextWater = FluidMathCore.ResolveIngressVolume(
                    currentWater,
                    dto.MaxVolume,
                    breachArea,
                    depthMeters,
                    DeltaTime,
                    DischargeCoefficient,
                    maxIngress,
                    HabitatFluidIncursionConstants.GravityMetersPerSecondSq,
                    HabitatFluidIncursionConstants.WaterEpsilonM3);

                float delta = math.max(0f, nextWater - currentWater);
                dto.CurrentWaterVolume = nextWater;
                dto.IngressRate = DeltaTime > 0f ? delta * math.rcp(DeltaTime) : 0f;
                dto.Flags |= FluidCompartmentFlags.Breached;
                if (delta > HabitatFluidIncursionConstants.WaterEpsilonM3)
                    PublishIncursion(in integrity, in dto, delta);
            }

            if (dto.CurrentWaterVolume > dto.MaxVolume - HabitatFluidIncursionConstants.WaterEpsilonM3)
                dto.Flags |= FluidCompartmentFlags.Flooded;
            else
                dto.Flags &= ~FluidCompartmentFlags.Flooded;

            FluidCompartmentPointerUtility.ElementRef(WriteCompartments, index) = dto;
        }

        private void PublishIncursion(in IntegrityStateDTO integrity, in FluidCompartmentDTO dto, float deltaM3)
        {
            FluidIncursionSignal signal = default;
            signal.LeakAup = integrity.CenterAup.ToAup();
            signal.CompartmentId = dto.NodeHash;
            signal.FloodLevel01 = dto.MaxVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(dto.CurrentWaterVolume * math.rcp(dto.MaxVolume))
                : 0f;
            signal.FlowRate01 = math.saturate(deltaM3 * math.rcp(math.max(HabitatFluidIncursionConstants.WaterEpsilonM3, dto.MaxVolume)));
            signal.Flags = (byte)((dto.Flags & FluidCompartmentFlags.MockBreach) != 0u ? 1 : 0);
            IncursionWriter.Enqueue(signal);
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

        private float ResolveWaterlineDepthMeters(in IntegrityStateDTO integrity, float floorHeightLocal)
        {
            double gridDeltaY = (ExternalWaterlineAup.GridY - integrity.CenterAup.GridY) *
                                HabitatFluidIncursionConstants.AupCellSizeMeters;
            double localDeltaY = (double)ExternalWaterlineAup.Local.y - integrity.CenterAup.Local.y;
            double depth = gridDeltaY + localDeltaY - floorHeightLocal;
            return (float)math.clamp(depth, -100000d, 100000d);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FluidBfsPressureEqualizationJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* Compartments;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public IntegrityStateDTO* Integrity;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<byte> EdgeFlags;
        [NoAlias] public NativeArray<int> BfsQueue;
        [NoAlias] public NativeArray<byte> BfsVisited;
        [NoAlias] public NativeArray<float> DeltaVolumes;
        public int CompartmentCount;
        public int EdgeCount;
        public int SolverIterations;
        public float DeltaTime;
        public float TransferRate01PerSecond;
        public float MaxTransferPerNodeM3;
        [NoAlias] public NativeArray<FluidIncursionFrameSummaryDTO> Summary;

        public void Execute()
        {
            int safeCount = math.min(CompartmentCount, BfsQueue.Length);
            safeCount = math.min(safeCount, math.min(BfsVisited.Length, DeltaVolumes.Length));
            if (safeCount <= 0 || !EdgeOffsets.IsCreated || EdgeOffsets.Length < safeCount + 1)
                return;

            int iterations = math.clamp(SolverIterations, HabitatFluidIncursionConstants.MinSolverIterations, HabitatFluidIncursionConstants.MaxSolverIterations);
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
                    if (IsSealedEdge(edgeIndex))
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
                    IntegrityStateDTO sourceIntegrity = Integrity[node];
                    IntegrityStateDTO destinationIntegrity = Integrity[destination];
                    float headDifferenceMeters = ResolveSurfaceHeadDifferenceMeters(
                        in source,
                        in destinationDto,
                        in sourceIntegrity,
                        in destinationIntegrity);
                    float delta = ResolvePotentialTransferDelta(
                        source.CurrentWaterVolume,
                        destinationDto.CurrentWaterVolume,
                        source.MaxVolume,
                        destinationDto.MaxVolume,
                        headDifferenceMeters,
                        DeltaTime,
                        TransferRate01PerSecond,
                        maxTransfer,
                        0.74f,
                        0.02f,
                        HabitatFluidIncursionConstants.GravityMetersPerSecondSq,
                        HabitatFluidIncursionConstants.WaterEpsilonM3);

                    if (delta == 0f)
                        continue;

                    DeltaVolumes[node] -= delta;
                    DeltaVolumes[destination] += delta;
                }
            }
        }

        private void ApplyComponentDeltas(int componentCount)
        {
            for (int queueIndex = 0; queueIndex < componentCount; queueIndex++)
            {
                int node = BfsQueue[queueIndex];
                ref FluidCompartmentDTO dto = ref FluidCompartmentPointerUtility.ElementRef(Compartments, node);
                float nextWater = dto.CurrentWaterVolume + DeltaVolumes[node];
                if (!math.isfinite(nextWater))
                {
                    dto.Flags |= FluidCompartmentFlags.NonFinite;
                    dto.CurrentWaterVolume = 0f;
                    continue;
                }

                dto.CurrentWaterVolume = math.clamp(nextWater, 0f, math.max(0f, dto.MaxVolume));
                if (dto.CurrentWaterVolume >= dto.MaxVolume - HabitatFluidIncursionConstants.WaterEpsilonM3)
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

        private static ushort SaturatingInc(ushort value)
        {
            return value == ushort.MaxValue ? value : (ushort)(value + 1);
        }

        private static float ResolveSurfaceHeadDifferenceMeters(
            in FluidCompartmentDTO source,
            in FluidCompartmentDTO destination,
            in IntegrityStateDTO sourceIntegrity,
            in IntegrityStateDTO destinationIntegrity)
        {
            float sourceFill = ResolveFill01(source.CurrentWaterVolume, source.MaxVolume);
            float destinationFill = ResolveFill01(destination.CurrentWaterVolume, destination.MaxVolume);
            float sourceHeight = math.max(0.25f, FluidMathCore.SafeCubeRoot(source.MaxVolume));
            float destinationHeight = math.max(0.25f, FluidMathCore.SafeCubeRoot(destination.MaxVolume));
            double gridDeltaY = (sourceIntegrity.CenterAup.GridY - destinationIntegrity.CenterAup.GridY) *
                                HabitatFluidIncursionConstants.AupCellSizeMeters;
            double localDeltaY = (double)sourceIntegrity.CenterAup.Local.y - destinationIntegrity.CenterAup.Local.y;
            double floorDeltaY = source.FloorHeightLocal - destination.FloorHeightLocal;
            double surfaceDeltaY = (sourceFill * sourceHeight) - (destinationFill * destinationHeight);
            return (float)math.clamp(gridDeltaY + localDeltaY + floorDeltaY + surfaceDeltaY, -100000d, 100000d);
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

            float velocityMetersPerSecond = math.sqrt(math.max(0f, 2f * math.max(0f, gravityMetersPerSecondSquared) * absHeadDifferenceMeters));
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
            uint stateHash = 2166136261u;

            for (int index = 0; index < safeCount; index++)
            {
                FluidCompartmentDTO dto = FluidCompartmentPointerUtility.ElementRef(Compartments, index);
                float maxVolume = math.max(HabitatFluidIncursionConstants.WaterEpsilonM3, dto.MaxVolume);
                float water = dto.CurrentWaterVolume;
                if (!math.isfinite(water) || !math.isfinite(maxVolume))
                {
                    water = 0f;
                    invalidCount = SaturatingInc(invalidCount);
                }

                water = math.clamp(water, 0f, maxVolume);
                float fill01 = math.saturate(water * math.rcp(maxVolume));
                float height = math.max(0.25f, FluidMathCore.SafeCubeRoot(maxVolume));
                float wobble = math.saturate((dto.IngressRate * 0.04f) + (fill01 * VisualWobbleScalar * math.saturate(GlobalQualityWeight)));

                Waterlines[index] = new FluidWaterlineShaderDTO
                {
                    Fill01 = fill01,
                    WaterlineLocalY = dto.FloorHeightLocal + (height * fill01),
                    Wobble01 = wobble,
                    NodeHash = dto.NodeHash
                };

                if (CompartmentTelemetry.IsCreated && index < CompartmentTelemetry.Length)
                {
                    CompartmentTelemetry[index] = new FluidCompartmentTelemetryDTO
                    {
                        NodeHash = dto.NodeHash,
                        CurrentWaterM3 = water,
                        MaxVolumeM3 = maxVolume,
                        Fill01 = fill01,
                        IngressRateM3PerSecond = math.max(0f, dto.IngressRate),
                        Flags = dto.Flags,
                        Frame = Frame,
                        CompartmentIndex = (ushort)math.min(ushort.MaxValue, index)
                    };
                }

                totalWaterM3 += water;
                totalCapacityM3 += maxVolume;
                maxFill01 = math.max(maxFill01, fill01);
                peakIngressRate = math.max(peakIngressRate, math.max(0f, dto.IngressRate));
                weightedWaterCenter += LocalCentroids[index] * water;
                if ((dto.Flags & FluidCompartmentFlags.Flooded) != 0u)
                    floodedCount = SaturatingInc(floodedCount);
                if ((dto.Flags & FluidCompartmentFlags.Breached) != 0u)
                    breachedCount = SaturatingInc(breachedCount);

                stateHash = MixHash(stateHash, dto.NodeHash);
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
            float angularDragMultiplier = 1f + (fillRatio * math.lerp(0.35f, 0.95f, math.saturate(GlobalQualityWeight)));

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
                summary.Flags = invalidCount > 0 ? 1u : 0u;
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

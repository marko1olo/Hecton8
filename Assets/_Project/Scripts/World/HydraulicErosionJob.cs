using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    internal static class HydraulicErosionJobLayout
    {
        public const int HydraulicErosionHeightDeltaStrideBytes = 16;
    }

    /// <summary>
    /// Blittable hydraulic erosion height/silt delta emitted by droplet slices.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HydraulicErosionJobLayout.HydraulicErosionHeightDeltaStrideBytes)]
    public struct HydraulicErosionHeightDelta
    {
        /// <summary>Linear heightmap cell index.</summary>
        [FieldOffset(0)] public int Index;

        /// <summary>Signed normalized height delta.</summary>
        [FieldOffset(4)] public float HeightDelta01;

        /// <summary>Positive normalized sediment deposition delta.</summary>
        [FieldOffset(8)] public float SedimentDelta01;

        /// <summary>Positive normalized erosion-depth delta.</summary>
        [FieldOffset(12)] public float ErosionDepthDelta01;
    }

    /// <summary>
    /// Schedules hydraulic erosion as four color-coded sub-grid phases.
    /// </summary>
    public static class HydraulicErosionScheduler
    {
        private const int MinDefaultDeltasPerApply = 1024;
        private const int MaxDefaultDeltasPerApply = 8192;
        private const int MaxDeltaApplyPassesPerDropletSlice = 128;

        /// <summary>Default telemetry capacity for worst-case sliced height-delta queues.</summary>
        public const int RecommendedMaxTrackedHeightDeltaQueueCapacity = 8 * 1024 * 1024;

        /// <summary>
        /// Maximum queued height-delta writes one droplet step can emit:
        /// DepositFlatSediment may run four 3x3 fill passes plus four bilinear cells,
        /// then terminal water/speed loss can still spread sediment across a 5x5 flat.
        /// </summary>
        public const int MaxHeightDeltaWritesPerDropletStep = 65;

        /// <summary>
        /// Schedules red-black style XY parity phases so same-phase sub-grids never share a write boundary.
        /// </summary>
        /// <param name="job">Caller-owned erosion job configuration.</param>
        /// <param name="innerLoopBatchCount">Batch count for sub-grid jobs.</param>
        /// <param name="dependency">Upstream job dependency.</param>
        /// <returns>Combined handle for all four erosion phases.</returns>
        public static JobHandle ScheduleFourPhase(
            ref HydraulicErosionJob job,
            int innerLoopBatchCount,
            JobHandle dependency)
        {
            int safeSubGridSize = math.max(8, job.SubGridSize);
            int safeCoreWidth = math.max(1, job.CoreWidth);
            int safeCoreHeight = math.max(1, job.CoreHeight);
            int subGridCountX = math.max(1, (safeCoreWidth + safeSubGridSize - 1) / safeSubGridSize);
            int subGridCountZ = math.max(1, (safeCoreHeight + safeSubGridSize - 1) / safeSubGridSize);
            int subGridCount = subGridCountX * subGridCountZ;
            int batchCount = math.max(1, innerLoopBatchCount);

            job.SubGridSize = safeSubGridSize;
            job.SubGridCountX = subGridCountX;
            job.SubGridCountZ = subGridCountZ;

            JobHandle handle = dependency;
            for (int phaseZ = 0; phaseZ < 2; phaseZ++)
            {
                for (int phaseX = 0; phaseX < 2; phaseX++)
                {
                    job.PhaseX = phaseX;
                    job.PhaseZ = phaseZ;
                    handle = job.Schedule(subGridCount, batchCount, handle);
                }
            }

            return handle;
        }

        /// <summary>
        /// Schedules deterministic droplet batches so one erosion kernel never owns an unbounded droplet budget.
        /// </summary>
        /// <param name="job">Caller-owned erosion job configuration.</param>
        /// <param name="dropletsPerSlice">Maximum droplets assigned to one four-phase pass.</param>
        /// <param name="innerLoopBatchCount">Batch count for sub-grid jobs.</param>
        /// <param name="dependency">Upstream job dependency.</param>
        /// <returns>Combined handle for all scheduled slices.</returns>
        public static JobHandle ScheduleFourPhaseSliced(
            ref HydraulicErosionJob job,
            int dropletsPerSlice,
            int innerLoopBatchCount,
            JobHandle dependency)
        {
            int originalDropletCount = math.max(0, job.DropletCount);
            int originalDropletIndexOffset = job.DropletIndexOffset;
            int safeDropletsPerSlice = math.max(1, dropletsPerSlice);
            if (originalDropletCount <= safeDropletsPerSlice)
                return ScheduleFourPhase(ref job, innerLoopBatchCount, dependency);

            JobHandle handle = dependency;
            for (int dropletOffset = 0; dropletOffset < originalDropletCount; dropletOffset += safeDropletsPerSlice)
            {
                job.DropletCount = math.min(safeDropletsPerSlice, originalDropletCount - dropletOffset);
                job.DropletIndexOffset = originalDropletIndexOffset + dropletOffset;
                handle = ScheduleFourPhase(ref job, innerLoopBatchCount, handle);
            }

            job.DropletCount = originalDropletCount;
            job.DropletIndexOffset = originalDropletIndexOffset;
            return handle;
        }

        /// <summary>
        /// Schedules droplet slices and applies their queued height deltas after each slice.
        /// </summary>
        /// <param name="job">Caller-owned erosion job configuration.</param>
        /// <param name="dropletsPerSlice">Maximum droplets assigned to one four-phase pass.</param>
        /// <param name="innerLoopBatchCount">Batch count for sub-grid jobs.</param>
        /// <param name="heightDeltas">Parallel droplet producer queue consumed by one delta-apply job.</param>
        /// <param name="maxDeltasPerApply">Preferred queued-delta drain cap per apply job. Values below one use a bounded estimate.</param>
        /// <param name="dependency">Upstream job dependency.</param>
        /// <returns>Combined handle for all scheduled slices and their delta-apply passes.</returns>
        public static JobHandle ScheduleFourPhaseSlicedWithDeltaApply(
            ref HydraulicErosionJob job,
            int dropletsPerSlice,
            int innerLoopBatchCount,
            NativeQueue<HydraulicErosionHeightDelta> heightDeltas,
            NativeArray<int> heightDeltaBudget,
            int maxDeltasPerApply,
            JobHandle dependency)
        {
            if (!heightDeltas.IsCreated || !heightDeltaBudget.IsCreated || heightDeltaBudget.Length < 2)
                return ScheduleFourPhaseSliced(ref job, dropletsPerSlice, innerLoopBatchCount, dependency);

            int originalDropletCount = math.max(0, job.DropletCount);
            int originalDropletIndexOffset = job.DropletIndexOffset;
            byte originalQueueHeightDeltas = job.QueueHeightDeltas;
            byte originalDeferHeightDeltaApplication = job.DeferHeightDeltaApplication;
            NativeQueue<HydraulicErosionHeightDelta>.ParallelWriter originalHeightDeltaQueue = job.HeightDeltaQueue;
            NativeArray<int> originalHeightDeltaBudget = job.HeightDeltaBudget;
            int safeDropletsPerSlice = math.max(1, dropletsPerSlice);
            int safeMaxDeltasPerApply = ResolvePreferredMaxDeltasPerApply(
                maxDeltasPerApply,
                safeDropletsPerSlice,
                job.MaxLifetime);

            if (originalDropletCount <= 0)
                return dependency;

            job.QueueHeightDeltas = 1;
            job.DeferHeightDeltaApplication = 1;
            job.HeightDeltaQueue = heightDeltas.AsParallelWriter();
            job.HeightDeltaBudget = heightDeltaBudget;

            JobHandle handle = dependency;
            for (int dropletOffset = 0; dropletOffset < originalDropletCount; dropletOffset += safeDropletsPerSlice)
            {
                job.DropletCount = math.min(safeDropletsPerSlice, originalDropletCount - dropletOffset);
                job.DropletIndexOffset = originalDropletIndexOffset + dropletOffset;
                int applyBudget = safeMaxDeltasPerApply;
                int applyPassCount = ResolveHeightDeltaApplyPlan(job.DropletCount, job.MaxLifetime, safeMaxDeltasPerApply, out applyBudget);
                ResetHeightDeltaBudget(heightDeltaBudget, applyBudget, applyPassCount);
                handle = ScheduleFourPhase(ref job, innerLoopBatchCount, handle);
                for (int applyPass = 0; applyPass < applyPassCount; applyPass++)
                {
                    handle = new HydraulicErosionDeltaApplyJob
                    {
                        HeightDeltas = heightDeltas,
                        Heightmap = job.Heightmap,
                        SedimentMask = job.SedimentMask,
                        ErosionDepthMask = job.ErosionDepthMask,
                        MaxDeltas = applyBudget,
                        ApplyQueuedDeltas = job.DeferHeightDeltaApplication
                    }.Schedule(handle);
                }
            }

            job.DropletCount = originalDropletCount;
            job.DropletIndexOffset = originalDropletIndexOffset;
            job.QueueHeightDeltas = originalQueueHeightDeltas;
            job.DeferHeightDeltaApplication = originalDeferHeightDeltaApplication;
            job.HeightDeltaQueue = originalHeightDeltaQueue;
            job.HeightDeltaBudget = originalHeightDeltaBudget;
            return handle;
        }

        private static void ResetHeightDeltaBudget(NativeArray<int> heightDeltaBudget, int applyBudget, int applyPassCount)
        {
            if (!heightDeltaBudget.IsCreated || heightDeltaBudget.Length < 2)
                return;

            long budget = (long)math.max(1, applyBudget) * math.max(1, applyPassCount);
            heightDeltaBudget[0] = budget > int.MaxValue ? int.MaxValue : (int)budget;
            heightDeltaBudget[1] = 0;
        }

        private static int ResolvePreferredMaxDeltasPerApply(int requestedMaxDeltasPerApply, int dropletsPerSlice, int maxLifetime)
        {
            if (requestedMaxDeltasPerApply > 0)
                return requestedMaxDeltasPerApply;

            long estimated = EstimateMaxHeightDeltaWrites(dropletsPerSlice, maxLifetime);
            long budget = estimated / MaxDeltaApplyPassesPerDropletSlice;
            if (budget < MinDefaultDeltasPerApply)
                return MinDefaultDeltasPerApply;
            if (budget > MaxDefaultDeltasPerApply)
                return MaxDefaultDeltasPerApply;

            return (int)budget;
        }

        private static int ResolveHeightDeltaApplyPlan(
            int dropletCount,
            int maxLifetime,
            int maxDeltasPerApply,
            out int resolvedMaxDeltasPerApply)
        {
            int safeBudget = math.max(1, maxDeltasPerApply);
            resolvedMaxDeltasPerApply = safeBudget;
            long estimated = EstimateMaxHeightDeltaWrites(dropletCount, maxLifetime);
            if (estimated <= safeBudget)
                return 1;

            long passes = (estimated + safeBudget - 1L) / safeBudget;
            if (passes <= MaxDeltaApplyPassesPerDropletSlice)
                return (int)passes;

            long raisedBudget = (estimated + MaxDeltaApplyPassesPerDropletSlice - 1L) / MaxDeltaApplyPassesPerDropletSlice;
            resolvedMaxDeltasPerApply = raisedBudget > int.MaxValue ? int.MaxValue : (int)raisedBudget;
            return MaxDeltaApplyPassesPerDropletSlice;
        }

        /// <summary>
        /// Resolves a sentinel/telemetry capacity for the queued delta buffer using the same worst-case model as the scheduler.
        /// </summary>
        public static int ResolveTrackedHeightDeltaQueueCapacity(
            int dropletCount,
            int maxLifetime,
            int minCapacity,
            int maxTrackedCapacity)
        {
            int safeMinCapacity = math.max(1, minCapacity);
            int safeMaxCapacity = math.max(safeMinCapacity, maxTrackedCapacity);
            long estimated = EstimateMaxHeightDeltaWrites(dropletCount, maxLifetime);
            if (estimated < safeMinCapacity)
                return safeMinCapacity;
            if (estimated > safeMaxCapacity)
                return safeMaxCapacity;

            return (int)estimated;
        }

        /// <summary>Estimates the maximum height-delta writes emitted by one sliced droplet pass.</summary>
        public static long EstimateMaxHeightDeltaWrites(int dropletCount, int maxLifetime)
        {
            return (long)math.max(0, dropletCount) *
                   math.max(1, maxLifetime) *
                   MaxHeightDeltaWritesPerDropletStep;
        }
    }

    /// <summary>
    /// Deterministic hydraulic erosion kernel for heightmap buffers.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct HydraulicErosionJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's parallel-for safety handle cannot infer that every Execute index owns a disjoint
        // height write rectangle. The scheduler filters each phase by X/Z parity, so same-phase
        // workers mutate only non-adjacent sub-grids and cannot touch the same height cell.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives rejected: duplicating a full heightmap per sub-grid would multiply terrain
        // memory and merge cost beyond the MX350 budget; serial IJob execution was the previous
        // bottleneck and cannot satisfy the required droplet counts. Per-cell atomics are not
        // available for float terrain edits and would destroy cache locality.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: ScheduleFourPhase writes PhaseX/PhaseZ before each Schedule call, and Execute
        // returns unless its sub-grid parity matches that phase. Each active job clamps erosion and
        // deposition to [writeMin, writeMax) inside its own sub-grid, with a one-cell movement inset.
        [NativeDisableParallelForRestriction, NoAlias]
        public NativeArray<float> Heightmap;

        /// <summary>Mutable normalized-source sediment accumulation lane. Normalize after the job.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // The sediment lane shares the same write footprint as Heightmap. Unity sees the NativeArray
        // as globally mutable from multiple Execute calls, but same-phase workers can only write
        // their own non-adjacent sub-grid windows selected by the four-phase parity filter.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives rejected: per-thread sediment buffers require an additional full-map reduction
        // pass and extra TempJob memory; scalar scheduling prevents useful parallel erosion; locking
        // or managed synchronization is illegal inside Burst and would violate the zero-GC mandate.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: every sediment write uses the same writeMin/writeMax bounds passed to the
        // droplet simulation by Execute. The scheduler never runs adjacent sub-grid parities in the
        // same phase, so no two live workers can update one sediment cell.
        [NativeDisableParallelForRestriction, NoAlias]
        public NativeArray<float> SedimentMask;

        /// <summary>Mutable raw erosion-depth lane used later by vegetation/scatter masks.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // ErosionDepthMask is intentionally mutable because channel bias must read previously carved
        // depth while the active sub-grid accumulates new erosion. Unity cannot prove this partition
        // is safe, but same-phase writes are spatially isolated by sub-grid parity.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives rejected: a read-only stale channel mask loses dendritic reinforcement inside
        // a phase; a full copy-on-write mask doubles memory bandwidth; NativeStream/NativeList event
        // accumulation would add variable-size native containers and a reduction pass.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: all erosion-depth writes occur through ErodeBrush using the Execute-owned
        // write bounds. Channel sampling may read nearby cells, but write ownership remains exclusive
        // for the active phase and later phases are chained by JobHandle dependency.
        [NativeDisableParallelForRestriction, NoAlias]
        public NativeArray<float> ErosionDepthMask;

        /// <summary>Optional queue writer for deferred terrain deltas.</summary>
        public NativeQueue<HydraulicErosionHeightDelta>.ParallelWriter HeightDeltaQueue;

        /// <summary>Two-int writer budget: remaining delta slots, dropped delta count.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // HeightDeltaBudget is an optional queued-delta limiter. Direct erosion scheduling keeps
        // QueueHeightDeltas and DeferHeightDeltaApplication at zero, so Execute never reads this
        // NativeArray in the production MapMagic path, but Unity job validation still rejects a
        // default optional container before the flag can gate access.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Allocating a synthetic TempJob budget for every direct erosion schedule would add owner
        // lifetime and Sentinel cleanup debt to the hot editor-generation route. Splitting the
        // job into duplicate direct/queued structs was rejected here because the queued path is
        // already quarantined from MapMagic and this patch only removes validation of unused state.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: the only budget read is inside TryEnqueueHeightDeltaBounded, which first checks
        // IsCreated and Length. If QueueHeightDeltas is zero, the method is never called; if it is
        // non-zero with an invalid budget, enqueue is refused before HeightDeltaQueue is touched.
        [NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> HeightDeltaBudget;

        /// <summary>Non-zero emits every terrain mutation into <see cref="HeightDeltaQueue"/>.</summary>
        public byte QueueHeightDeltas;

        /// <summary>Non-zero prevents immediate heightmap mutation; queued deltas are applied by a later job.</summary>
        public byte DeferHeightDeltaApplication;

        /// <summary>Heightmap width, including any overlap margin.</summary>
        public int Width;

        /// <summary>Heightmap height, including any overlap margin.</summary>
        public int Height;

        /// <summary>Core chunk X offset inside the overlapped buffer.</summary>
        public int CoreOffsetX;

        /// <summary>Core chunk Z offset inside the overlapped buffer.</summary>
        public int CoreOffsetZ;

        /// <summary>Core chunk width excluding margin pixels.</summary>
        public int CoreWidth;

        /// <summary>Core chunk height excluding margin pixels.</summary>
        public int CoreHeight;

        /// <summary>Sub-grid edge size in cells.</summary>
        public int SubGridSize;

        /// <summary>Number of sub-grids on X. Filled by the scheduler.</summary>
        public int SubGridCountX;

        /// <summary>Number of sub-grids on Z. Filled by the scheduler.</summary>
        public int SubGridCountZ;

        /// <summary>Active phase X parity, 0 or 1.</summary>
        public int PhaseX;

        /// <summary>Active phase Z parity, 0 or 1.</summary>
        public int PhaseZ;

        /// <summary>Number of simulated droplets across all sub-grids.</summary>
        public int DropletCount;

        /// <summary>Deterministic global droplet id offset for sliced scheduling.</summary>
        public int DropletIndexOffset;

        /// <summary>Maximum per-droplet integration steps.</summary>
        public int MaxLifetime;

        /// <summary>Deterministic seed.</summary>
        public uint Seed;

        /// <summary>Direction inertia. Higher values keep channels straighter.</summary>
        public float Inertia;

        /// <summary>Sediment capacity multiplier.</summary>
        public float CapacityFactor;

        /// <summary>Minimum sediment capacity even on shallow slopes.</summary>
        public float MinCapacity;

        /// <summary>Rock removal rate when sediment capacity exceeds carried sediment.</summary>
        public float ErosionRate;

        /// <summary>Sediment drop rate when capacity falls below carried sediment.</summary>
        public float DepositRate;

        /// <summary>Per-step water evaporation ratio.</summary>
        public float EvaporationRate;

        /// <summary>Velocity gain from downhill movement.</summary>
        public float Gravity;

        /// <summary>Initial droplet water volume.</summary>
        public float InitialWater;

        /// <summary>Initial droplet speed.</summary>
        public float InitialSpeed;

        /// <summary>Local flat-fill strength used for sandy depression plains.</summary>
        public float DepressionFillStrength;

        /// <summary>Spawn score multiplier for cells lower than their neighborhood.</summary>
        public float DepressionSpawnBias;

        /// <summary>Spawn score multiplier for already carved cells.</summary>
        public float ChannelSpawnBias;

        /// <summary>Directional pull toward existing erosion-depth channels.</summary>
        public float ChannelFlowBias;

        /// <summary>World cell size in meters for slope-angle conversion.</summary>
        public float CellSizeMeters;

        /// <summary>World vertical scale represented by height 0..1.</summary>
        public float HeightScaleMeters;

        /// <summary>Slope threshold below which droplets dump all sediment.</summary>
        public float SedimentaryFlatSlopeDegrees;

        /// <summary>Number of deterministic spawn candidates tested per droplet.</summary>
        public int SpawnCandidateCount;

        /// <summary>Minimum water volume before final deposition and termination.</summary>
        public float MinWater;

        /// <inheritdoc />
        public void Execute(int subGridIndex)
        {
            if (Width < 8 || Height < 8 || !Heightmap.IsCreated)
                return;

            int safeSubGridCountX = math.max(1, SubGridCountX);
            int safeSubGridCountZ = math.max(1, SubGridCountZ);
            int totalSubGrids = safeSubGridCountX * safeSubGridCountZ;
            if (subGridIndex < 0 || subGridIndex >= totalSubGrids)
                return;

            int subGridX = subGridIndex % safeSubGridCountX;
            int subGridZ = subGridIndex / safeSubGridCountX;
            if ((subGridX & 1) != (PhaseX & 1) || (subGridZ & 1) != (PhaseZ & 1))
                return;

            int safeSubGridSize = math.max(8, SubGridSize);
            int coreMinX = math.clamp(CoreOffsetX, 1, math.max(1, Width - 2));
            int coreMinZ = math.clamp(CoreOffsetZ, 1, math.max(1, Height - 2));
            int coreMaxX = math.clamp(CoreOffsetX + math.max(1, CoreWidth), coreMinX + 1, math.max(coreMinX + 1, Width - 1));
            int coreMaxZ = math.clamp(CoreOffsetZ + math.max(1, CoreHeight), coreMinZ + 1, math.max(coreMinZ + 1, Height - 1));
            int writeMinX = coreMinX + subGridX * safeSubGridSize;
            int writeMinZ = coreMinZ + subGridZ * safeSubGridSize;
            int writeMaxX = math.min(coreMaxX, writeMinX + safeSubGridSize);
            int writeMaxZ = math.min(coreMaxZ, writeMinZ + safeSubGridSize);

            if (writeMaxX - writeMinX < 5 || writeMaxZ - writeMinZ < 5)
                return;

            // Motion bounds are extended well beyond the write window so droplets can read and traverse
            // terrain across the full heightmap. Without this, droplets terminate exactly at the sub-grid
            // boundary, creating a hard erosion seam every SubGridSize pixels (the 32px checkerboard).
            // Writes are still clamped to [writeMin, writeMax) by ErodeBrush and DepositBrush.
            int motionOverlap = math.max(MaxLifetime, safeSubGridSize * 2);
            int motionMinX = math.max(1, writeMinX - motionOverlap);
            int motionMinZ = math.max(1, writeMinZ - motionOverlap);
            int motionMaxX = math.min(Width - 1, writeMaxX + motionOverlap);
            int motionMaxZ = math.min(Height - 1, writeMaxZ + motionOverlap);
            int safeDropletCount = math.max(0, DropletCount);
            int baseDroplets = totalSubGrids > 0 ? safeDropletCount / totalSubGrids : 0;
            int remainderDroplets = totalSubGrids > 0 ? safeDropletCount - baseDroplets * totalSubGrids : 0;
            int dropletsForSubGrid = baseDroplets + (subGridIndex < remainderDroplets ? 1 : 0);
            int dropletStart = subGridIndex * baseDroplets + math.min(subGridIndex, remainderDroplets);

            for (int localDroplet = 0; localDroplet < dropletsForSubGrid; localDroplet++)
            {
                SimulateDroplet(
                    DropletIndexOffset + dropletStart + localDroplet,
                    motionMinX,
                    motionMinZ,
                    motionMaxX,
                    motionMaxZ,
                    writeMinX,
                    writeMinZ,
                    writeMaxX,
                    writeMaxZ);
            }
        }

        /// <summary>
        /// Calculates droplet sediment capacity from downhill slope, velocity, and water.
        /// </summary>
        /// <param name="heightDelta">New height minus old height.</param>
        /// <param name="speed">Current droplet speed.</param>
        /// <param name="water">Current droplet water volume.</param>
        /// <param name="capacityFactor">Capacity multiplier.</param>
        /// <param name="minCapacity">Minimum allowed capacity.</param>
        /// <returns>Maximum sediment the droplet can carry.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateSedimentCapacity(
            float heightDelta,
            float speed,
            float water,
            float capacityFactor,
            float minCapacity)
        {
            float downhillSlope = math.max(-heightDelta, 0.001f);
            float velocityTerm = math.max(speed, 0.01f);
            float waterTerm = math.max(water, 0.01f);
            float rawCapacity = downhillSlope * velocityTerm * waterTerm * math.max(0f, capacityFactor);
            return math.max(rawCapacity, math.max(0f, minCapacity));
        }

        private void SimulateDroplet(
            int dropletIndex,
            int motionMinX,
            int motionMinZ,
            int motionMaxX,
            int motionMaxZ,
            int writeMinX,
            int writeMinZ,
            int writeMaxX,
            int writeMaxZ)
        {
            int safeLifetime = math.max(1, MaxLifetime);
            float safeInertia = math.saturate(Inertia);
            float safeErosionRate = math.max(0f, ErosionRate);
            float safeDepositRate = math.max(0f, DepositRate);
            float safeEvaporation = math.saturate(EvaporationRate);
            float safeGravity = math.max(0.001f, Gravity);
            float safeInitialWater = math.max(0.001f, InitialWater);
            float safeInitialSpeed = math.max(0.001f, InitialSpeed);
            float safeMinWater = math.max(0.000001f, MinWater);
            float flatSlopeDegrees = math.max(0f, SedimentaryFlatSlopeDegrees);
            float2 position = ResolveSpawnPosition(dropletIndex, motionMinX, motionMinZ, motionMaxX, motionMaxZ);
            float2 direction = float2.zero;
            float speed = safeInitialSpeed;
            float water = safeInitialWater;
            float sediment = 0f;

            for (int step = 0; step < safeLifetime; step++)
            {
                if (!IsInsideDropletBounds(position, motionMinX, motionMinZ, motionMaxX, motionMaxZ))
                    break;

                float height = SampleHeight(position);
                float2 gradient = SampleGradient(position);
                float slopeDegrees = ResolveSlopeDegrees(gradient);
                if (sediment > 0f && slopeDegrees <= flatSlopeDegrees)
                {
                    float deposited = DepositSedimentaryFlat(position, sediment, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                    sediment -= deposited;
                    break;
                }

                float2 channelGradient = SampleAccumulationGradient(ErosionDepthMask, position);
                float2 hydraulicForce = -gradient + channelGradient * math.max(0f, ChannelFlowBias);
                direction = direction * safeInertia + hydraulicForce * (1f - safeInertia);
                float directionLengthSq = math.lengthsq(direction);
                if (directionLengthSq <= 0.0000001f)
                    direction = HashDirection(dropletIndex, step);
                else
                    direction *= math.rsqrt(directionLengthSq);

                float2 nextPosition = position + direction;
                if (!IsInsideDropletBounds(nextPosition, motionMinX, motionMinZ, motionMaxX, motionMaxZ))
                {
                    float deposited = DepositSedimentaryFlat(position, sediment, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                    sediment -= deposited;
                    break;
                }

                float nextHeight = SampleHeight(nextPosition);
                float heightDelta = nextHeight - height;
                float capacity = CalculateSedimentCapacity(
                    heightDelta,
                    speed,
                    water,
                    CapacityFactor,
                    MinCapacity);

                if (heightDelta > 0f || sediment > capacity)
                {
                    float excessSediment = math.max(0f, sediment - capacity);
                    float depositAmount = heightDelta > 0f
                        ? math.min(sediment, heightDelta + excessSediment * safeDepositRate)
                        : excessSediment * safeDepositRate;

                    if (depositAmount > 0f)
                    {
                        float targetHeight = heightDelta > 0f
                            ? nextHeight
                            : height + depositAmount * math.max(0f, DepressionFillStrength);
                        float deposited = DepositFlatSediment(position, depositAmount, targetHeight, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                        sediment -= deposited;
                    }
                }
                else
                {
                    float erodeAmount = math.min((capacity - sediment) * safeErosionRate, math.max(0f, -heightDelta));
                    if (erodeAmount > 0f)
                        sediment += ErodeBrush(position, erodeAmount, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                }

                float speedSquared = speed * speed + (-heightDelta) * safeGravity;
                if (speedSquared <= 0.000001f)
                {
                    float deposited = DepositSedimentaryFlat(position, sediment, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                    sediment -= deposited;
                    break;
                }

                speed = math.min(FastSpeedMagnitude(speedSquared), 24.0f);
                water *= 1f - safeEvaporation;
                position = nextPosition;

                if (water <= safeMinWater)
                {
                    float deposited = DepositSedimentaryFlat(position, sediment, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                    sediment -= deposited;
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastSpeedMagnitude(float speedSquared)
        {
            float x = math.max(0f, speedSquared);
            float safe = math.max(x, 0.000000000001f);
            int estimateBits = (math.asint(safe) >> 1) + 0x1FBD1DF5;
            float estimate = math.asfloat(estimateBits);
            return math.select(0f, 0.5f * (estimate + safe / math.max(estimate, 0.000000000001f)), x > 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 ResolveSpawnPosition(int dropletIndex, int minX, int minZ, int maxX, int maxZ)
        {
            int spawnMinX = math.clamp(minX, 1, math.max(1, Width - 2));
            int spawnMinZ = math.clamp(minZ, 1, math.max(1, Height - 2));
            int spawnMaxX = math.clamp(maxX - 1, spawnMinX, math.max(spawnMinX, Width - 2));
            int spawnMaxZ = math.clamp(maxZ - 1, spawnMinZ, math.max(spawnMinZ, Height - 2));
            int candidates = math.max(1, SpawnCandidateCount);

            uint state = Hash((uint)dropletIndex ^ Seed ^ 0xA511E9B3u);
            int bestX = spawnMinX;
            int bestZ = spawnMinZ;
            float bestScore = -1f;

            for (int i = 0; i < candidates; i++)
            {
                state = Hash(state + (uint)i * 0x9E3779B9u);
                int x = spawnMinX + (int)(state % (uint)math.max(1, spawnMaxX - spawnMinX + 1));
                state = Hash(state ^ 0xB5297A4Du);
                int z = spawnMinZ + (int)(state % (uint)math.max(1, spawnMaxZ - spawnMinZ + 1));

                int index = z * Width + x;
                float depression = CalculateLocalDepression(x, z);
                float channel = ErosionDepthMask.IsCreated ? math.saturate(ErosionDepthMask[index] * 32f) : 0f;
                float jitter = Hash01(state ^ 0x68E31DA4u);
                float score = jitter +
                              depression * math.max(0f, DepressionSpawnBias) +
                              channel * math.max(0f, ChannelSpawnBias);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestZ = z;
                }
            }

            state = Hash(state ^ 0x1B56C4E9u);
            float jitterX = Hash01(state) - 0.5f;
            state = Hash(state ^ 0x92D68CA2u);
            float jitterZ = Hash01(state) - 0.5f;
            return new float2(bestX + 0.5f + jitterX, bestZ + 0.5f + jitterZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateLocalDepression(int x, int z)
        {
            int index = z * Width + x;
            float center = math.saturate(Heightmap[index]);
            float neighborSum = 0f;
            int neighborCount = 0;

            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oz == 0)
                        continue;

                    int nx = math.clamp(x + ox, 0, Width - 1);
                    int nz = math.clamp(z + oz, 0, Height - 1);
                    neighborSum += math.saturate(Heightmap[nz * Width + nx]);
                    neighborCount++;
                }
            }

            float neighborAverage = neighborSum / math.max(1, neighborCount);
            return math.max(0f, neighborAverage - center);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInsideDropletBounds(float2 position, int minX, int minZ, int maxX, int maxZ)
        {
            return position.x >= minX &&
                   position.y >= minZ &&
                   position.x < maxX &&
                   position.y < maxZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleHeight(float2 position)
        {
            int x = math.clamp((int)math.floor(position.x), 0, Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Height - 2);
            float fx = position.x - x;
            float fz = position.y - z;

            float h00 = math.saturate(Heightmap[z * Width + x]);
            float h10 = math.saturate(Heightmap[z * Width + x + 1]);
            float h01 = math.saturate(Heightmap[(z + 1) * Width + x]);
            float h11 = math.saturate(Heightmap[(z + 1) * Width + x + 1]);

            return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleAccumulation(NativeArray<float> mask, float2 position)
        {
            if (!mask.IsCreated)
                return 0f;

            int x = math.clamp((int)math.floor(position.x), 0, Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Height - 2);
            float fx = position.x - x;
            float fz = position.y - z;

            float h00 = math.saturate(mask[z * Width + x] * 32f);
            float h10 = math.saturate(mask[z * Width + x + 1] * 32f);
            float h01 = math.saturate(mask[(z + 1) * Width + x] * 32f);
            float h11 = math.saturate(mask[(z + 1) * Width + x + 1] * 32f);

            return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 SampleGradient(float2 position)
        {
            float left = SampleHeight(new float2(position.x - 1f, position.y));
            float right = SampleHeight(new float2(position.x + 1f, position.y));
            float down = SampleHeight(new float2(position.x, position.y - 1f));
            float up = SampleHeight(new float2(position.x, position.y + 1f));
            return new float2((right - left) * 0.5f, (up - down) * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 SampleAccumulationGradient(NativeArray<float> mask, float2 position)
        {
            if (!mask.IsCreated)
                return float2.zero;

            float left = SampleAccumulation(mask, new float2(position.x - 1f, position.y));
            float right = SampleAccumulation(mask, new float2(position.x + 1f, position.y));
            float down = SampleAccumulation(mask, new float2(position.x, position.y - 1f));
            float up = SampleAccumulation(mask, new float2(position.x, position.y + 1f));
            return new float2((right - left) * 0.5f, (up - down) * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSlopeDegrees(float2 gradient01)
        {
            float worldGradient = FastSpeedMagnitude(math.lengthsq(gradient01)) *
                                  math.max(0.001f, HeightScaleMeters) /
                                  math.max(0.001f, CellSizeMeters);
            return FastAtanDegreesPositive(worldGradient);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastAtanDegreesPositive(float value)
        {
            float x = math.max(0f, value);
            float reciprocal = 1f / math.max(x, 0.000001f);
            bool useReciprocal = x > 1f;
            float y = math.select(x, reciprocal, useReciprocal);
            float radians = y / (1f + 0.280872f * y * y);
            radians = math.select(radians, 1.5707964f - radians, useReciprocal);
            return radians * 57.29578f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ErodeBrush(float2 position, float amount, int writeMinX, int writeMinZ, int writeMaxX, int writeMaxZ)
        {
            int centerX = math.clamp((int)math.floor(position.x), writeMinX + 1, writeMaxX - 2);
            int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 1, writeMaxZ - 2);
            float totalWeight = 0f;

            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    float2 cellCenter = new float2(centerX + ox + 0.5f, centerZ + oz + 0.5f);
                    float distance = FastSpeedMagnitude(math.lengthsq(cellCenter - position));
                    totalWeight += math.saturate(1f - distance * 0.6666667f);
                }
            }

            if (totalWeight <= 0.000001f)
                return 0f;

            float removed = 0f;
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = centerX + ox;
                    int z = centerZ + oz;
                    int index = z * Width + x;
                    float2 cellCenter = new float2(x + 0.5f, z + 0.5f);
                    float weight = math.saturate(1f - FastSpeedMagnitude(math.lengthsq(cellCenter - position)) * 0.6666667f) / totalWeight;
                    float requested = amount * weight;
                    float current = math.saturate(Heightmap[index]);
                    float actual = math.min(current, requested);
                    ApplyHeightDelta(index, -actual, 0f, actual);
                    removed += actual;
                }
            }

            return removed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositSedimentaryFlat(float2 position, float amount, int writeMinX, int writeMinZ, int writeMaxX, int writeMaxZ)
        {
            if (amount <= 0f)
                return 0f;

            int centerX = math.clamp((int)math.floor(position.x), writeMinX + 2, writeMaxX - 3);
            int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 2, writeMaxZ - 3);
            float totalWeight = 0f;

            for (int oz = -2; oz <= 2; oz++)
            {
                for (int ox = -2; ox <= 2; ox++)
                {
                    float2 cellCenter = new float2(centerX + ox + 0.5f, centerZ + oz + 0.5f);
                    float distance = FastSpeedMagnitude(math.lengthsq(cellCenter - position));
                    totalWeight += math.saturate(1f - distance * 0.28f);
                }
            }

            if (totalWeight <= 0.000001f)
                return DepositBilinear(position, amount, writeMinX, writeMinZ, writeMaxX, writeMaxZ);

            for (int oz = -2; oz <= 2; oz++)
            {
                for (int ox = -2; ox <= 2; ox++)
                {
                    int index = (centerZ + oz) * Width + centerX + ox;
                    float2 cellCenter = new float2(centerX + ox + 0.5f, centerZ + oz + 0.5f);
                    float weight = math.saturate(1f - FastSpeedMagnitude(math.lengthsq(cellCenter - position)) * 0.28f) / totalWeight;
                    float deposit = amount * weight;
                    ApplyHeightDelta(index, deposit, deposit, 0f);
                }
            }

            return amount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositFlatSediment(float2 position, float amount, float targetHeight, int writeMinX, int writeMinZ, int writeMaxX, int writeMaxZ)
        {
            if (amount <= 0f)
                return 0f;

            int centerX = math.clamp((int)math.floor(position.x), writeMinX + 1, writeMaxX - 2);
            int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 1, writeMaxZ - 2);
            float remaining = amount;
            float safeTargetHeight = math.saturate(targetHeight);

            for (int pass = 0; pass < 4; pass++)
            {
                float lowest = 2f;
                float nextLowest = 2f;
                int lowCount = 0;

                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float h = math.saturate(Heightmap[(centerZ + oz) * Width + centerX + ox]);
                        if (h < lowest - 0.000001f)
                        {
                            nextLowest = lowest;
                            lowest = h;
                            lowCount = 1;
                        }
                        else if (math.abs(h - lowest) <= 0.000001f)
                        {
                            lowCount++;
                        }
                        else if (h < nextLowest)
                        {
                            nextLowest = h;
                        }
                    }
                }

                if (lowCount <= 0 || lowest >= safeTargetHeight)
                    break;

                float fillHeight = math.min(safeTargetHeight, nextLowest < 1.5f ? nextLowest : safeTargetHeight);
                float capacity = math.max(0f, fillHeight - lowest) * lowCount;
                if (capacity <= 0.000001f)
                    break;

                float fillAmount = math.min(remaining, capacity);
                float raise = fillAmount / lowCount;

                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int index = (centerZ + oz) * Width + centerX + ox;
                        float h = math.saturate(Heightmap[index]);
                        if (math.abs(h - lowest) > 0.000001f)
                            continue;

                        ApplyHeightDelta(index, raise, raise, 0f);
                    }
                }

                remaining -= fillAmount;
                if (remaining <= 0.000001f)
                    return amount;
            }

            if (remaining > 0.000001f)
            {
                float deposited = DepositBilinear(position, remaining, writeMinX, writeMinZ, writeMaxX, writeMaxZ);
                remaining -= deposited;
            }

            return amount - math.max(0f, remaining);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositBilinear(float2 position, float amount, int writeMinX, int writeMinZ, int writeMaxX, int writeMaxZ)
        {
            // Clamp to write window to prevent cross-sub-grid writes when droplets travel
            // beyond their spawn sub-grid under the expanded motion bounds.
            int x = math.clamp((int)math.floor(position.x), writeMinX, writeMaxX - 2);
            int z = math.clamp((int)math.floor(position.y), writeMinZ, writeMaxZ - 2);
            float fx = position.x - x;
            float fz = position.y - z;

            float w00 = (1f - fx) * (1f - fz);
            float w10 = fx * (1f - fz);
            float w01 = (1f - fx) * fz;
            float w11 = fx * fz;

            DepositAtIndex(z * Width + x, amount * w00);
            DepositAtIndex(z * Width + x + 1, amount * w10);
            DepositAtIndex((z + 1) * Width + x, amount * w01);
            DepositAtIndex((z + 1) * Width + x + 1, amount * w11);
            return amount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositAtIndex(int index, float amount)
        {
            float actual = math.max(0f, amount);
            return ApplyHeightDelta(index, actual, actual, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ApplyHeightDelta(int index, float heightDelta01, float sedimentDelta01, float erosionDepthDelta01)
        {
            if (heightDelta01 == 0f && sedimentDelta01 == 0f && erosionDepthDelta01 == 0f)
                return 0f;

            if (QueueHeightDeltas != 0)
            {
                HydraulicErosionHeightDelta delta = new HydraulicErosionHeightDelta
                {
                    Index = index,
                    HeightDelta01 = heightDelta01,
                    SedimentDelta01 = sedimentDelta01,
                    ErosionDepthDelta01 = erosionDepthDelta01
                };
                TryEnqueueHeightDeltaBounded(HeightDeltaQueue, HeightDeltaBudget, in delta);
                return heightDelta01;
            }

            float oldH = Heightmap[index];
            float newH = math.saturate(oldH + heightDelta01);
            float actualDelta = newH - oldH;
            Heightmap[index] = newH;

            if (sedimentDelta01 != 0f && SedimentMask.IsCreated)
                SedimentMask[index] = math.saturate(SedimentMask[index] + sedimentDelta01);

            if (erosionDepthDelta01 != 0f && ErosionDepthMask.IsCreated)
                ErosionDepthMask[index] = math.saturate(ErosionDepthMask[index] + erosionDepthDelta01);

            return actualDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool TryEnqueueHeightDeltaBounded(
            NativeQueue<HydraulicErosionHeightDelta>.ParallelWriter writer,
            NativeArray<int> writerBudget,
            in HydraulicErosionHeightDelta delta)
        {
            if (!writerBudget.IsCreated || writerBudget.Length < 2)
                return false;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writerBudget);
            int remainingAfterClaim = Interlocked.Decrement(ref budget[0]);
            if (remainingAfterClaim < 0)
            {
                Interlocked.Increment(ref budget[1]);
                return false;
            }

            writer.Enqueue(delta);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 HashDirection(int dropletIndex, int step)
        {
            uint hash = Hash((uint)dropletIndex * 0x9E3779B9u ^ (uint)step * 0x85EBCA6Bu);
            const float diagonal = 0.70710678118f;
            switch ((int)(hash & 7u))
            {
                case 0: return new float2(1f, 0f);
                case 1: return new float2(diagonal, diagonal);
                case 2: return new float2(0f, 1f);
                case 3: return new float2(-diagonal, diagonal);
                case 4: return new float2(-1f, 0f);
                case 5: return new float2(-diagonal, -diagonal);
                case 6: return new float2(0f, -1f);
                default: return new float2(diagonal, -diagonal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    /// <summary>
    /// Applies queued hydraulic erosion deltas after a sliced droplet pass.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct HydraulicErosionDeltaApplyJob : IJob
    {
        /// <summary>Queued signed height and mask deltas emitted by the droplet job.</summary>
        public NativeQueue<HydraulicErosionHeightDelta> HeightDeltas;

        /// <summary>Mutable normalized heightmap target.</summary>
        [NoAlias] public NativeArray<float> Heightmap;

        /// <summary>Mutable normalized sediment accumulation target.</summary>
        [NoAlias] public NativeArray<float> SedimentMask;

        /// <summary>Mutable normalized erosion-depth target.</summary>
        [NoAlias] public NativeArray<float> ErosionDepthMask;

        /// <summary>Maximum deltas consumed in this apply pass. Values below one are clamped to one to preserve bounded slicing.</summary>
        public int MaxDeltas;

        /// <summary>Non-zero applies queued deltas; zero drains already-applied telemetry deltas.</summary>
        public byte ApplyQueuedDeltas;

        /// <inheritdoc />
        public void Execute()
        {
            if (!HeightDeltas.IsCreated || !Heightmap.IsCreated)
                return;

            int budget = math.max(1, MaxDeltas);
            int count = math.min(budget, HeightDeltas.Count);
            if (count <= 0)
                return;

            NativeArray<HydraulicErosionHeightDelta> batch = new NativeArray<HydraulicErosionHeightDelta>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int actualCount = 0;
            while (actualCount < count && HeightDeltas.TryDequeue(out HydraulicErosionHeightDelta delta))
            {
                batch[actualCount++] = delta;
            }

            if (actualCount > 0)
            {
                // Deterministic Order: Sort batch entries by cell Index before applying to eliminate thread dequeue ordering desync
                NativeSortExtension.Sort(batch.GetSubArray(0, actualCount), new HeightDeltaIndexComparer());

                for (int i = 0; i < actualCount; i++)
                {
                    HydraulicErosionHeightDelta delta = batch[i];
                    if ((uint)delta.Index >= (uint)Heightmap.Length)
                        continue;

                    if (ApplyQueuedDeltas != 0)
                    {
                        Heightmap[delta.Index] = math.saturate(Heightmap[delta.Index] + delta.HeightDelta01);
                        if (delta.SedimentDelta01 != 0f && SedimentMask.IsCreated && (uint)delta.Index < (uint)SedimentMask.Length)
                            SedimentMask[delta.Index] = math.saturate(SedimentMask[delta.Index] + delta.SedimentDelta01);
                        if (delta.ErosionDepthDelta01 != 0f && ErosionDepthMask.IsCreated && (uint)delta.Index < (uint)ErosionDepthMask.Length)
                            ErosionDepthMask[delta.Index] = math.saturate(ErosionDepthMask[delta.Index] + delta.ErosionDepthDelta01);
                    }
                }
            }

            batch.Dispose();
        }

        private struct HeightDeltaIndexComparer : System.Collections.Generic.IComparer<HydraulicErosionHeightDelta>
        {
            public int Compare(HydraulicErosionHeightDelta x, HydraulicErosionHeightDelta y)
            {
                return x.Index.CompareTo(y.Index);
            }
        }
    }

    /// <summary>
    /// Smooths deposition cells into flat sedimentary plains after droplets dump payloads.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SedimentaryFlatSmoothingJob : IJobParallelFor
    {
        /// <summary>Read-only height source.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;

        /// <summary>Read-only sediment accumulation mask.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> SedimentMask;

        /// <summary>Write-only smoothed output.</summary>
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;

        /// <summary>Heightmap width.</summary>
        public int Width;

        /// <summary>Heightmap height.</summary>
        public int Height;

        /// <summary>Cell size in meters.</summary>
        public float CellSizeMeters;

        /// <summary>Vertical height scale in meters.</summary>
        public float HeightScaleMeters;

        /// <summary>Maximum slope angle allowed for forced flat deposition smoothing.</summary>
        public float MaxSlopeDegrees;

        /// <summary>Minimum sediment accumulator required to classify a cell as deposition flat.</summary>
        public float SedimentThreshold;

        /// <summary>Smoothing strength.</summary>
        public float Strength;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            float center = math.saturate(InputHeights01[index]);
            if (safeWidth < 3 || safeHeight < 3 || x <= 0 || z <= 0 || x >= safeWidth - 1 || z >= safeHeight - 1)
            {
                OutputHeights01[index] = center;
                return;
            }

            float sediment = SedimentMask.IsCreated ? SedimentMask[index] : 0f;
            if (sediment <= math.max(0f, SedimentThreshold))
            {
                OutputHeights01[index] = center;
                return;
            }

            float slopeDegrees = ResolveSlopeDegrees(index);
            if (slopeDegrees > math.max(0.001f, MaxSlopeDegrees) * 2f)
            {
                OutputHeights01[index] = center;
                return;
            }

            float weightedHeight = 0f;
            float totalWeight = 0f;
            float sedimentScale = 1f / math.max(0.000001f, sediment);
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int sampleIndex = (z + oz) * Width + x + ox;
                    float sampleSediment = SedimentMask.IsCreated ? SedimentMask[sampleIndex] : 0f;
                    float weight = 1f + math.saturate(sampleSediment * sedimentScale) * 8f;
                    weightedHeight += math.saturate(InputHeights01[sampleIndex]) * weight;
                    totalWeight += weight;
                }
            }

            float target = weightedHeight / math.max(0.000001f, totalWeight);
            OutputHeights01[index] = math.saturate(math.lerp(center, target, math.saturate(Strength)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSlopeDegrees(int index)
        {
            float left = math.saturate(InputHeights01[index - 1]);
            float right = math.saturate(InputHeights01[index + 1]);
            float back = math.saturate(InputHeights01[index - Width]);
            float forward = math.saturate(InputHeights01[index + Width]);
            float2 gradient = new float2((right - left) * 0.5f, (forward - back) * 0.5f);
            float worldGradient = FastMagnitude(math.lengthsq(gradient)) *
                                  math.max(0.001f, HeightScaleMeters) /
                                  math.max(0.001f, CellSizeMeters);
            return FastAtanDegreesPositive(worldGradient);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastAtanDegreesPositive(float value)
        {
            float x = math.max(0f, value);
            float reciprocal = 1f / math.max(x, 0.000001f);
            bool useReciprocal = x > 1f;
            float y = math.select(x, reciprocal, useReciprocal);
            float radians = y / (1f + 0.280872f * y * y);
            radians = math.select(radians, 1.5707964f - radians, useReciprocal);
            return radians * 57.29578f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastMagnitude(float magnitudeSq)
        {
            float x = math.max(0f, magnitudeSq);
            float safe = math.max(x, 0.000000000001f);
            int estimateBits = (math.asint(safe) >> 1) + 0x1FBD1DF5;
            float estimate = math.asfloat(estimateBits);
            return math.select(0f, 0.5f * (estimate + safe / math.max(estimate, 0.000000000001f)), x > 0f);
        }
    }

    /// <summary>
    /// Raises immediate banks around deep erosion channels to sharpen canyon walls.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CanyonWallSteepeningJob : IJobParallelFor
    {
        /// <summary>Read-only height source.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;

        /// <summary>Read-only erosion-depth mask.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> ErosionDepthMask;

        /// <summary>Write-only bank-steepened output.</summary>
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;

        /// <summary>Heightmap width.</summary>
        public int Width;

        /// <summary>Heightmap height.</summary>
        public int Height;

        /// <summary>Minimum neighboring erosion depth before wall lift applies.</summary>
        public float DepthThreshold;

        /// <summary>Bank lift strength from neighboring channel depth.</summary>
        public float Strength;

        /// <summary>Maximum normalized lift per cell.</summary>
        public float MaxLift01;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            float center = math.saturate(InputHeights01[index]);
            if (safeWidth < 3 || safeHeight < 3 || x <= 0 || z <= 0 || x >= safeWidth - 1 || z >= safeHeight - 1 || !ErosionDepthMask.IsCreated)
            {
                OutputHeights01[index] = center;
                return;
            }

            float centerDepth = ErosionDepthMask[index];
            float maxNeighborDepth = 0f;
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oz == 0)
                        continue;

                    maxNeighborDepth = math.max(maxNeighborDepth, ErosionDepthMask[(z + oz) * Width + x + ox]);
                }
            }

            float threshold = math.max(0f, DepthThreshold);
            float channelDelta = maxNeighborDepth - math.max(centerDepth, threshold);
            if (channelDelta <= 0f)
            {
                OutputHeights01[index] = center;
                return;
            }

            float lift = math.min(math.max(0f, MaxLift01), channelDelta * math.max(0f, Strength));
            OutputHeights01[index] = math.saturate(center + lift);
        }
    }

    /// <summary>
    /// Builds a bottom-only silt paint mask from hydraulic deposit and carved-channel evidence.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ErodedChannelSiltMaskJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights01;
        [ReadOnly, NoAlias] public NativeArray<float> Sediment01;
        [ReadOnly, NoAlias] public NativeArray<float> Wear01;
        [WriteOnly, NoAlias] public NativeArray<float> SiltMask01;

        public int Width;
        public int Height;
        public float DepressionStrength;
        public float SedimentStrength;
        public float WearStrength;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            if ((uint)index >= (uint)SiltMask01.Length || (uint)index >= (uint)(safeWidth * safeHeight))
                return;

            int x = index % safeWidth;
            int z = index / safeWidth;
            int maxX = safeWidth - 1;
            int maxZ = safeHeight - 1;

            float center = ReadHeight(index);
            float west = ReadHeight(math.max(0, x - 1) + z * safeWidth);
            float east = ReadHeight(math.min(maxX, x + 1) + z * safeWidth);
            float south = ReadHeight(x + math.max(0, z - 1) * safeWidth);
            float north = ReadHeight(x + math.min(maxZ, z + 1) * safeWidth);
            float neighborAverage = (west + east + south + north) * 0.25f;
            float depression = math.saturate((neighborAverage - center) * math.max(1f, DepressionStrength));
            float channelBottom = math.smoothstep(0.012f, 0.18f, depression);
            float deposit = math.saturate(ReadOptional(Sediment01, index) * math.max(0f, SedimentStrength));
            float wear = math.saturate(ReadOptional(Wear01, index) * math.max(0f, WearStrength));
            float carved = math.smoothstep(0.035f, 0.44f, wear);
            SiltMask01[index] = math.saturate(deposit * channelBottom * carved);
        }

        private float ReadHeight(int index)
        {
            if ((uint)index >= (uint)Heights01.Length)
                return 0f;

            return math.saturate(Heights01[index]);
        }

        private static float ReadOptional(NativeArray<float> source, int index)
        {
            if (!source.IsCreated || (uint)index >= (uint)source.Length)
                return 0f;

            return math.saturate(source[index]);
        }
    }

    /// <summary>
    /// Normalizes a mask in-place on a worker thread before managed matrix publication.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct NormalizeMaskInPlaceJob : IJob
    {
        /// <summary>Mutable mask buffer.</summary>
        [NoAlias] public NativeArray<float> Mask;

        /// <summary>Number of cells to scan and normalize.</summary>
        public int Count;

        /// <inheritdoc />
        public void Execute()
        {
            int count = math.clamp(Count, 0, Mask.IsCreated ? Mask.Length : 0);
            float maxValue = 0f;
            for (int i = 0; i < count; i++)
                maxValue = math.max(maxValue, math.max(0f, Mask[i]));

            float invMax = maxValue > 0.000001f ? 1f / maxValue : 0f;
            for (int i = 0; i < count; i++)
                Mask[i] = math.saturate(Mask[i] * invMax);
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using Den.Tools.Matrices;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace MapMagic.Nodes.MatrixGenerators
{
    /// <summary>
    /// MapMagic 2 custom generator wrapping the HECTON-8 Burst erosion kernels.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Hydraulic Erosion Burst",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonHydraulicErosionMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonHydraulicErosionMapMagicNode);
        private const string HeightALabel = "heightA";
        private const string HeightBLabel = "heightB";
        private const string SedimentLabel = "sediment";
        private const string SiltLabel = "silt";
        private const string WearLabel = "wear";
        private const int FullDropletTelemetryThreshold = 1000000;
        private const int DraftDropletTelemetryThreshold = 250000;
        private const int MinDropletsPerScheduleSlice = 100;
        private const int MaxDropletsPerScheduleSlice = 1000;
        private const int CellCountTelemetryThreshold = 1048576;
        private const float BarrierStallTelemetryThresholdMs = 25f;
        private const uint DropletBudgetWarningHash = 0x48594544u;
        private const uint CellBudgetWarningHash = 0x48594543u;
        private const uint BarrierStallWarningHash = 0x48594542u;
        private const uint HydraulicErosionNodeContextHash = 0x4859454Eu;
        private static readonly ProfilerMarker ErosionScheduleProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.ScheduleFourPhase");
        private static readonly ProfilerMarker SedimentaryFlatProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.SedimentaryFlatSchedule");
        private static readonly ProfilerMarker ThermalSlumpProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.ThermalSlumpSchedule");
        private static readonly ProfilerMarker CanyonWallProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.CanyonWallSchedule");
        private static readonly ProfilerMarker MaskNormalizeProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.MaskNormalizeSchedule");
        private static readonly ProfilerMarker PublishBarrierProfilerMarker = new ProfilerMarker("H8/World/HydraulicErosion.PublishBarrier");

        /// <summary>Input heightmap matrix.</summary>
        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        /// <summary>Eroded heightmap output.</summary>
        [Den.Tools.GUI.ValAttribute("Eroded Height", "Outlet")]
        public readonly Outlet<MatrixWorld> erodedHeightOut = new Outlet<MatrixWorld>();

        /// <summary>Strictly normalized bottom-channel silt deposition output.</summary>
        [Den.Tools.GUI.ValAttribute("Silt Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> sedimentMaskOut = new Outlet<MatrixWorld>();

        /// <summary>Strictly normalized hydraulic erosion-depth output.</summary>
        [Den.Tools.GUI.ValAttribute("Erosion Depth Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> wearMaskOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        /// <summary>Total droplet count. Draft generation uses a reduced count.</summary>
        [Den.Tools.GUI.ValAttribute("Droplets")]
        public int dropletCount = 1000000;

        /// <summary>Maximum droplet steps.</summary>
        [Den.Tools.GUI.ValAttribute("Lifetime")]
        public int maxLifetime = 64;

        /// <summary>Deterministic seed.</summary>
        [Den.Tools.GUI.ValAttribute("Seed")]
        public int seed = 190863;

        /// <summary>Boundary overlap processed around chunk core.</summary>
        [Den.Tools.GUI.ValAttribute("Margin")]
        public int marginPixels = 4;

        /// <summary>Number of weighted spawn candidates per droplet.</summary>
        [Den.Tools.GUI.ValAttribute("Spawn Candidates")]
        public int spawnCandidateCount = 12;

        /// <summary>Independent erosion partition edge size in pixels.</summary>
        [Den.Tools.GUI.ValAttribute("Sub Grid")]
        public int subGridSize = 32;

        /// <summary>Scheduler operation budget used to derive droplets per sliced four-phase pass.</summary>
        [Den.Tools.GUI.ValAttribute("Max Ops/Slice")]
        public int maxOperationsPerSlice = 1000;

        /// <summary>Spawn bias for slight depressions.</summary>
        [Den.Tools.GUI.ValAttribute("Depression Spawn")]
        public float depressionSpawnBias = 12f;

        /// <summary>Spawn bias for existing channels.</summary>
        [Den.Tools.GUI.ValAttribute("Channel Spawn")]
        public float channelSpawnBias = 24f;

        /// <summary>Directional pull into existing carved channels.</summary>
        [Den.Tools.GUI.ValAttribute("Channel Flow")]
        public float channelFlowBias = 2.75f;

        /// <summary>Direction inertia.</summary>
        [Den.Tools.GUI.ValAttribute("Inertia")]
        public float inertia = 0.86f;

        /// <summary>Sediment capacity multiplier.</summary>
        [Den.Tools.GUI.ValAttribute("Capacity")]
        public float capacityFactor = 4f;

        /// <summary>Minimum sediment capacity.</summary>
        [Den.Tools.GUI.ValAttribute("Min Capacity")]
        public float minCapacity = 0.0001f;

        /// <summary>Erosion rate.</summary>
        [Den.Tools.GUI.ValAttribute("Erosion")]
        public float erosionRate = 0.35f;

        /// <summary>Deposition rate.</summary>
        [Den.Tools.GUI.ValAttribute("Deposition")]
        public float depositRate = 0.18f;

        /// <summary>Evaporation rate.</summary>
        [Den.Tools.GUI.ValAttribute("Evaporation")]
        public float evaporationRate = 0.015f;

        /// <summary>Droplet gravity.</summary>
        [Den.Tools.GUI.ValAttribute("Gravity")]
        public float gravity = 4f;

        /// <summary>Local flat fill strength for sandy plains.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Fill")]
        public float depressionFillStrength = 0.85f;

        /// <summary>Slope cutoff for full-payload sediment dumping.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Slope")]
        public float sedimentaryFlatSlopeDegrees = 2f;

        /// <summary>Smoothing iterations for sedimentary plains.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Smooth Iter")]
        public int sedimentaryFlatSmoothingIterations = 2;

        /// <summary>Smoothing strength for sedimentary plains.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Smooth")]
        public float sedimentaryFlatSmoothingStrength = 0.95f;

        /// <summary>Raw sediment threshold for flat smoothing classification.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Sediment Min")]
        public float sedimentaryFlatSedimentThreshold = 0.00001f;

        /// <summary>Raw erosion-depth threshold for canyon bank sharpening.</summary>
        [Den.Tools.GUI.ValAttribute("Canyon Depth")]
        public float canyonWallDepthThreshold = 0.0002f;

        /// <summary>Wall lift strength around deep channels.</summary>
        [Den.Tools.GUI.ValAttribute("Canyon Wall")]
        public float canyonWallStrength = 4f;

        /// <summary>Maximum normalized wall lift per pass.</summary>
        [Den.Tools.GUI.ValAttribute("Canyon Max Lift")]
        public float canyonWallMaxLift01 = 0.02f;

        /// <summary>Enables thermal slumping after hydraulic erosion.</summary>
        [Den.Tools.GUI.ValAttribute("Thermal Slump")]
        public bool enableThermalSlumping = true;

        /// <summary>Thermal slumping iterations.</summary>
        [Den.Tools.GUI.ValAttribute("Slump Iterations")]
        public int thermalIterations = 2;

        /// <summary>Critical talus angle in degrees.</summary>
        [Den.Tools.GUI.ValAttribute("Talus Angle")]
        public float talusAngleDegrees = 45f;

        /// <summary>Thermal slumping strength.</summary>
        [Den.Tools.GUI.ValAttribute("Slump Strength")]
        public float thermalStrength = 0.32f;

        /// <inheritdoc />
        public float Complexity =>
            math.max(1, dropletCount / 50000f) +
            math.max(0, thermalIterations) +
            math.max(0, sedimentaryFlatSmoothingIterations) +
            (canyonWallStrength > 0f && canyonWallMaxLift01 > 0f ? 1f : 0f);

        /// <inheritdoc />
        public float Progress(TileData data) => data.GetProgress(this);

        /// <inheritdoc />
        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[1] - MapMagic port enumeration cache - owner: HectonHydraulicErosionMapMagicNode
                _inletCache = new IInlet<object>[1];
                _inletCache[0] = heightIn;
            }

            return _inletCache;
        }

        /// <inheritdoc />
        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[3] - MapMagic port enumeration cache - owner: HectonHydraulicErosionMapMagicNode
                _outletCache = new IOutlet<object>[3];
                _outletCache[0] = erodedHeightOut;
                _outletCache[1] = sedimentMaskOut;
                _outletCache[2] = wearMaskOut;
            }

            return _outletCache;
        }

        /// <inheritdoc />
        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        /// <inheritdoc />
        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            MatrixWorld eroded = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            MatrixWorld sedimentMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            MatrixWorld wearMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);

            if (!enabled)
            {
                CopyMatrix(src.arr, eroded.arr);
                data.StoreProduct(erodedHeightOut, eroded);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
                return;
            }

            int cellCount = src.arr != null ? src.arr.Length : 0;
            int width = math.max(1, src.rect.size.x);
            int height = math.max(1, src.rect.size.z);
            if (cellCount <= 0 || width * height > cellCount)
            {
                data.StoreProduct(erodedHeightOut, src);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
                return;
            }

            NativeArray<float> heightA = default;
            NativeArray<float> heightB = default;
            NativeArray<float> sediment = default;
            NativeArray<float> silt = default;
            NativeArray<float> wear = default;
            int heightARegistrationId = 0;
            int heightBRegistrationId = 0;
            int sedimentRegistrationId = 0;
            int siltRegistrationId = 0;
            int wearRegistrationId = 0;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                heightA = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightB = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                silt = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                wear = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobBuffers(
                    heightA,
                    heightB,
                    sediment,
                    silt,
                    wear,
                    out heightARegistrationId,
                    out heightBRegistrationId,
                    out sedimentRegistrationId,
                    out siltRegistrationId,
                    out wearRegistrationId);

                for (int i = 0; i < cellCount; i++)
                    heightA[i] = math.saturate(src.arr[i]);

                int safeMargin = math.clamp(marginPixels, 0, math.max(0, math.min(width, height) / 4));
                int coreWidth = math.max(1, width - safeMargin * 2);
                int coreHeight = math.max(1, height - safeMargin * 2);
                int resolvedDroplets = data.isDraft ? math.max(1, dropletCount / 4) : math.max(1, dropletCount);
                PublishColdPathBudgetWarnings(cellCount, resolvedDroplets, data.isDraft);
                float cellSizeMeters = ResolveCellSizeMeters(src);
                float heightScaleMeters = math.max(0.001f, src.worldSize.y > 0f ? src.worldSize.y : data.globals.height);

                var erosionJob = new HydraulicErosionJob
                {
                    Heightmap = heightA,
                    SedimentMask = sediment,
                    ErosionDepthMask = wear,
                    Width = width,
                    Height = height,
                    CoreOffsetX = safeMargin,
                    CoreOffsetZ = safeMargin,
                    CoreWidth = coreWidth,
                    CoreHeight = coreHeight,
                    SubGridSize = subGridSize,
                    DropletCount = resolvedDroplets,
                    MaxLifetime = math.max(1, maxLifetime),
                    Seed = unchecked((uint)seed),
                    Inertia = math.max(0.72f, inertia),
                    CapacityFactor = capacityFactor,
                    MinCapacity = minCapacity,
                    ErosionRate = erosionRate,
                    DepositRate = depositRate,
                    EvaporationRate = evaporationRate,
                    Gravity = gravity,
                    InitialWater = 1f,
                    InitialSpeed = 1f,
                    DepressionFillStrength = depressionFillStrength,
                    DepressionSpawnBias = depressionSpawnBias,
                    ChannelSpawnBias = math.max(12f, channelSpawnBias),
                    ChannelFlowBias = math.max(1.5f, channelFlowBias),
                    CellSizeMeters = cellSizeMeters,
                    HeightScaleMeters = heightScaleMeters,
                    SedimentaryFlatSlopeDegrees = sedimentaryFlatSlopeDegrees,
                    SpawnCandidateCount = math.max(12, spawnCandidateCount),
                    MinWater = 0.01f,
                    DropletIndexOffset = 0
                };

                using (ErosionScheduleProfilerMarker.Auto())
                {
                    int currentOperations = ResolveCurrentOperations(
                        cellCount,
                        sedimentaryFlatSmoothingIterations,
                        enableThermalSlumping ? thermalIterations : 0,
                        canyonWallStrength > 0f && canyonWallMaxLift01 > 0f);
                    int dropletsPerSlice = ResolveDropletsPerSlice(maxOperationsPerSlice, currentOperations);
                    // COLD SYNC JOB: MapMagic worker threads must return fully owned NativeArray state.
                    // The queued delta-apply path currently exposes a safety handle that can outlive the
                    // returned dependency in editor generation; direct four-phase scheduling keeps the
                    // published handle as the single owner of all height writes.
                    handle = HydraulicErosionScheduler.ScheduleFourPhaseSliced(
                        ref erosionJob,
                        dropletsPerSlice,
                        1,
                        handle);
                    handleScheduled = true;
                }
                NativeArray<float> current = heightA;
                NativeArray<float> next = heightB;

                if (width > 2 && height > 2)
                {
                    using (SedimentaryFlatProfilerMarker.Auto())
                    {
                        int flatIterations = math.max(0, sedimentaryFlatSmoothingIterations);
                        for (int i = 0; i < flatIterations; i++)
                        {
                            if (stop != null && stop.stop)
                                break;

                            var flatJob = new SedimentaryFlatSmoothingJob
                            {
                                InputHeights01 = current,
                                OutputHeights01 = next,
                                SedimentMask = sediment,
                                Width = width,
                                Height = height,
                                CellSizeMeters = cellSizeMeters,
                                HeightScaleMeters = heightScaleMeters,
                                MaxSlopeDegrees = sedimentaryFlatSlopeDegrees,
                                SedimentThreshold = sedimentaryFlatSedimentThreshold,
                                Strength = sedimentaryFlatSmoothingStrength
                            };

                            handle = flatJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                            Swap(ref current, ref next);
                        }
                    }
                }

                if (enableThermalSlumping && width > 2 && height > 2)
                {
                    using (ThermalSlumpProfilerMarker.Auto())
                    {
                        int iterations = math.max(0, thermalIterations);
                        for (int i = 0; i < iterations; i++)
                        {
                            if (stop != null && stop.stop)
                                break;

                            var slumpJob = new ThermalSlumpingJob
                            {
                                InputHeights01 = current,
                                OutputHeights01 = next,
                                WearMask = wear,
                                Width = width,
                                Height = height,
                                CellSizeMeters = cellSizeMeters,
                                HeightScaleMeters = heightScaleMeters,
                                TalusAngleDegrees = talusAngleDegrees,
                                Strength = thermalStrength,
                                WriteWearMaskFlag = 0
                            };

                            handle = slumpJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                            Swap(ref current, ref next);
                        }
                    }
                }

                if ((stop == null || !stop.stop) &&
                    width > 2 &&
                    height > 2 &&
                    canyonWallStrength > 0f &&
                    canyonWallMaxLift01 > 0f)
                {
                    using (CanyonWallProfilerMarker.Auto())
                    {
                        var canyonJob = new CanyonWallSteepeningJob
                        {
                            InputHeights01 = current,
                            OutputHeights01 = next,
                            ErosionDepthMask = wear,
                            Width = width,
                            Height = height,
                            DepthThreshold = canyonWallDepthThreshold,
                            Strength = canyonWallStrength,
                            MaxLift01 = canyonWallMaxLift01
                        };

                        handle = canyonJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                        Swap(ref current, ref next);
                    }
                }

                if (stop == null || !stop.stop)
                {
                    handle = new ErodedChannelSiltMaskJob
                    {
                        Heights01 = current,
                        Sediment01 = sediment,
                        Wear01 = wear,
                        SiltMask01 = silt,
                        Width = width,
                        Height = height,
                        DepressionStrength = 192f,
                        SedimentStrength = 1f,
                        WearStrength = 1f
                    }.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                }

                if (stop == null || !stop.stop)
                {
                    using (MaskNormalizeProfilerMarker.Auto())
                    {
                        handle = new NormalizeMaskInPlaceJob
                        {
                            Mask = sediment,
                            Count = cellCount
                        }.Schedule(handle);

                        handle = new NormalizeMaskInPlaceJob
                        {
                            Mask = wear,
                            Count = cellCount
                        }.Schedule(handle);

                        handle = new NormalizeMaskInPlaceJob
                        {
                            Mask = silt,
                            Count = cellCount
                        }.Schedule(handle);
                    }
                }

                // COLD SYNC JOB: MapMagic Generate must publish concrete matrix products before returning to the graph.
                long barrierStartTicks = Stopwatch.GetTimestamp();
                using (PublishBarrierProfilerMarker.Auto())
                {
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                    handleScheduled = false;
                }
                PublishBarrierWarning(ElapsedMilliseconds(barrierStartTicks, Stopwatch.GetTimestamp()));

                if (stop != null && stop.stop)
                    return;

                CopyNativeToMatrix(current, eroded.arr);
                CopyNativeToMatrix(silt, sedimentMask.arr);
                CopyNativeToMatrix(wear, wearMask.arr);

                data.SetProgress(this, Complexity);
                data.StoreProduct(erodedHeightOut, eroded);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref heightA, ref heightARegistrationId);
                DisposeTracked(ref heightB, ref heightBRegistrationId);
                DisposeTracked(ref sediment, ref sedimentRegistrationId);
                DisposeTracked(ref silt, ref siltRegistrationId);
                DisposeTracked(ref wear, ref wearRegistrationId);
            }
        }

        private static float ElapsedMilliseconds(long startTicks, long endTicks)
        {
            long rawDeltaTicks = endTicks - startTicks;
            long deltaTicks = rawDeltaTicks > 0L ? rawDeltaTicks : 0L;
            return (float)((deltaTicks * 1000.0) / Stopwatch.Frequency);
        }

        private static int ResolveDropletsPerSlice(int maxOperations, int currentOperations)
        {
            int maxOps = math.max(MinDropletsPerScheduleSlice, maxOperations);
            int currentOps = math.max(0, currentOperations);
            return math.clamp(maxOps - currentOps, MinDropletsPerScheduleSlice, MaxDropletsPerScheduleSlice);
        }

        private static int ResolveCurrentOperations(
            int cellCount,
            int flatIterations,
            int thermalSlumpIterations,
            bool canyonWallPass)
        {
            int cellDebt = math.clamp(cellCount / 4096, 0, 96);
            int flatDebt = math.max(0, flatIterations) * 64;
            int thermalDebt = math.max(0, thermalSlumpIterations) * 96;
            int canyonDebt = canyonWallPass ? 128 : 0;
            return cellDebt + flatDebt + thermalDebt + canyonDebt;
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> heightA,
            NativeArray<float> heightB,
            NativeArray<float> sediment,
            NativeArray<float> silt,
            NativeArray<float> wear,
            out int heightARegistrationId,
            out int heightBRegistrationId,
            out int sedimentRegistrationId,
            out int siltRegistrationId,
            out int wearRegistrationId)
        {
            heightARegistrationId = RegisterTempJobArray(heightA, HeightALabel);
            heightBRegistrationId = RegisterTempJobArray(heightB, HeightBLabel);
            sedimentRegistrationId = RegisterTempJobArray(sediment, SedimentLabel);
            siltRegistrationId = RegisterTempJobArray(silt, SiltLabel);
            wearRegistrationId = RegisterTempJobArray(wear, WearLabel);
        }

        private static int RegisterTempJobArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            if (registrationId <= 0)
                throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");

            return registrationId;
        }

        private static void PublishColdPathBudgetWarnings(int cellCount, int resolvedDroplets, bool isDraft)
        {
            int dropletThreshold = isDraft ? DraftDropletTelemetryThreshold : FullDropletTelemetryThreshold;
            if (resolvedDroplets >= dropletThreshold)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    DropletBudgetWarningHash,
                    HydraulicErosionNodeContextHash,
                    resolvedDroplets);
            }

            if (cellCount >= CellCountTelemetryThreshold)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    CellBudgetWarningHash,
                    HydraulicErosionNodeContextHash,
                    cellCount);
            }
        }

        private static void PublishBarrierWarning(float barrierMilliseconds)
        {
            if (barrierMilliseconds < BarrierStallTelemetryThresholdMs)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                BarrierStallWarningHash,
                HydraulicErosionNodeContextHash,
                barrierMilliseconds);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array, ref int registrationId) where T : struct
        {
            System.Exception cleanupException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (System.Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (cleanupException != null)
                throw cleanupException;
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        private static void Swap(ref NativeArray<float> current, ref NativeArray<float> next)
        {
            NativeArray<float> swap = current;
            current = next;
            next = swap;
        }

        private static void CopyMatrix(float[] source, float[] destination)
        {
            int count = math.min(source != null ? source.Length : 0, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static void CopyNativeToMatrix(NativeArray<float> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i]);
        }

    }
}

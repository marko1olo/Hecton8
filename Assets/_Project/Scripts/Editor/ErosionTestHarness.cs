using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only standalone harness for validating the isolated erosion jobs.
    /// </summary>
    public static class ErosionTestHarness
    {
        private const int Resolution = 512;
        private const int PixelCount = Resolution * Resolution;
        private const string OutputFolder = "CodexArtifacts";
        private const string NativeMemoryOwner = nameof(ErosionTestHarness);
        private const string BeforeLabel = "before";
        private const string HeightALabel = "heightA";
        private const string HeightBLabel = "heightB";
        private const string SedimentLabel = "sediment";
        private const string WearLabel = "wear";
        private const string HeightDeltaQueueLabel = "heightDeltas";
        private const string HeightDeltaBudgetLabel = "heightDeltaBudget";
        private const string MetricsLabel = "metrics";
        private const string ShelfRawLabel = "shelfRaw";
        private const string ShelfQuantizedLabel = "shelfQuantized";
        private const string HeightPixelsLabel = "heightPixels";
        private const string NormalPixelsLabel = "normalPixels";
        private const string MaskPixelsLabel = "maskPixels";
        private const string MaskMaxLabel = "maskMax";
        private const double ShelfPreviewOriginMeters = -16000.0;
        private const double ShelfPreviewCellSizeMeters = 64.0;
        private const double ShelfAupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
        // Vertical extent has ONE owner: WorldVerticalExtentMath
        // (Scripts/World/WorldVerticalExtentContracts.cs). These were hand-copied duplicates of the
        // HectonSandboxAbyssalShelfMapMagicNode field initialisers; same values, so the PNG/normal-map
        // artifacts this harness writes are byte-identical.
        private const float ShelfHighWorldY = WorldVerticalExtentMath.DefaultHighWorldY;
        private const float ShelfLowWorldY = WorldVerticalExtentMath.DefaultLowWorldY;
        private const float ErosionHeightScaleMeters = 160f;
        private const int ErosionSubGridSize = 32;
        private const float ErosionInertia = 0.86f;
        private const float ErosionChannelSpawnBias = 24f;
        private const float ErosionChannelFlowBias = 2.75f;
        private const float SedimentaryFlatSlopeDegrees = 2f;
        private const float SedimentaryFlatSmoothingStrength = 0.95f;
        private const float SedimentaryFlatSedimentThreshold = 0.00001f;
        private const float CanyonDepthThreshold = 0.0002f;
        private const float CanyonWallStrength = 4f;
        private const float CanyonMaxLift01 = 0.02f;
        private const int MinDropletsPerScheduleSlice = 100;
        private const int MaxDropletsPerScheduleSlice = 1000;
        private const int MaxErosionOperationsPerSlice = 1000;
        private const int MaxTrackedHeightDeltaQueueCapacity = HydraulicErosionScheduler.RecommendedMaxTrackedHeightDeltaQueueCapacity;
        private const int MaxHeightDeltaApplyPerJob = 8192;
        private const int ErosionDropletCount = 300000;
        private const int ErosionMaxLifetime = 72;
        private const int SedimentaryFlatIterations = 2;
        private const int ThermalSlumpIterations = 3;
        private const bool RunCanyonWallPass = true;
        private static readonly UTF8Encoding JsonEncoding = new UTF8Encoding(false); // COLD ALLOC: UTF8Encoding[1] - editor smoke JSON artifact writer - owner: ErosionTestHarness

        /// <summary>
        /// Generates fractal terrain, runs erosion and slumping, and writes PNG artifacts.
        /// </summary>
        [MenuItem("Tools/Hecton/Dev/Terrain/Run Erosion Test Harness")]
        public static void Run()
        {
            NativeArray<float> before = default;
            NativeArray<float> heightA = default;
            NativeArray<float> heightB = default;
            NativeArray<float> sediment = default;
            NativeArray<float> wear = default;
            NativeQueue<HydraulicErosionHeightDelta> heightDeltas = default;
            NativeArray<int> heightDeltaBudget = default;
            NativeArray<ErosionSmokeMetrics> metrics = default;
            int beforeRegistrationId = 0;
            int heightARegistrationId = 0;
            int heightBRegistrationId = 0;
            int sedimentRegistrationId = 0;
            int wearRegistrationId = 0;
            int heightDeltasRegistrationId = 0;
            int heightDeltaBudgetRegistrationId = 0;
            int metricsRegistrationId = 0;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                before = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.UninitializedMemory, BeforeLabel, out beforeRegistrationId);
                heightA = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.UninitializedMemory, HeightALabel, out heightARegistrationId);
                heightB = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.UninitializedMemory, HeightBLabel, out heightBRegistrationId);
                sediment = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.ClearMemory, SedimentLabel, out sedimentRegistrationId);
                wear = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.ClearMemory, WearLabel, out wearRegistrationId);
                int dropletsPerSlice = ResolveDropletsPerSlice(
                    MaxErosionOperationsPerSlice,
                    ResolveCurrentErosionOperations(PixelCount, SedimentaryFlatIterations, ThermalSlumpIterations, RunCanyonWallPass));
                heightDeltas = AllocateTrackedHeightDeltaQueue(ResolveHeightDeltaQueueCapacity(dropletsPerSlice, ErosionMaxLifetime), out heightDeltasRegistrationId); // COLD ALLOC: NativeQueue<HydraulicErosionHeightDelta>[tracked cap 8388608 entries, ~128 MiB payload upper-bound] - sliced editor erosion deltas; harness mirrors MapMagic queue budget for proof artifacts - owner: ErosionTestHarness
                heightDeltaBudget = AllocateTrackedTempJobArray<int>(2, NativeArrayOptions.ClearMemory, HeightDeltaBudgetLabel, out heightDeltaBudgetRegistrationId);
                metrics = AllocateTrackedTempJobArray<ErosionSmokeMetrics>(1, NativeArrayOptions.ClearMemory, MetricsLabel, out metricsRegistrationId);

                handle = new ErosionFractalHeightmapJob
                {
                    Before = before,
                    Height = heightA,
                    Resolution = Resolution,
                    PrimarySeed = 0xC001CAFEu,
                    RidgeSeed = 0x6C8E9CF5u
                }.Schedule(PixelCount, 64);
                handleScheduled = true;

                var erosionJob = new HydraulicErosionJob
                {
                    Heightmap = heightA,
                    SedimentMask = sediment,
                    ErosionDepthMask = wear,
                    Width = Resolution,
                    Height = Resolution,
                    CoreOffsetX = 4,
                    CoreOffsetZ = 4,
                    CoreWidth = Resolution - 8,
                    CoreHeight = Resolution - 8,
                    SubGridSize = ErosionSubGridSize,
                    DropletCount = ErosionDropletCount,
                    MaxLifetime = ErosionMaxLifetime,
                    Seed = 347239u,
                    Inertia = ErosionInertia,
                    CapacityFactor = 4f,
                    MinCapacity = 0.0001f,
                    ErosionRate = 0.35f,
                    DepositRate = 0.18f,
                    EvaporationRate = 0.015f,
                    Gravity = 4f,
                    InitialWater = 1f,
                    InitialSpeed = 1f,
                    DepressionFillStrength = 0.85f,
                    DepressionSpawnBias = 12f,
                    ChannelSpawnBias = ErosionChannelSpawnBias,
                    ChannelFlowBias = ErosionChannelFlowBias,
                    CellSizeMeters = 1f,
                    HeightScaleMeters = ErosionHeightScaleMeters,
                    SedimentaryFlatSlopeDegrees = SedimentaryFlatSlopeDegrees,
                    SpawnCandidateCount = 12,
                    MinWater = 0.01f
                };

                // QUEUED_DELTA_APPLY_QUARANTINED: editor-only proof route. Production MapMagic
                // terrain generation must stay on direct ScheduleFourPhaseSliced until this
                // queue writer/budget/apply lifecycle has fresh Unity proof.
                handle = HydraulicErosionScheduler.ScheduleFourPhaseSlicedWithDeltaApply(
                    ref erosionJob,
                    dropletsPerSlice,
                    1,
                    heightDeltas,
                    heightDeltaBudget,
                    ResolveHeightDeltaApplyBudget(dropletsPerSlice, ErosionMaxLifetime),
                    handle);
                NativeArray<float> current = heightA;
                NativeArray<float> next = heightB;

                for (int i = 0; i < SedimentaryFlatIterations; i++)
                {
                    var flatJob = new SedimentaryFlatSmoothingJob
                    {
                        InputHeights01 = current,
                        OutputHeights01 = next,
                        SedimentMask = sediment,
                        Width = Resolution,
                        Height = Resolution,
                        CellSizeMeters = 1f,
                        HeightScaleMeters = ErosionHeightScaleMeters,
                        MaxSlopeDegrees = SedimentaryFlatSlopeDegrees,
                        SedimentThreshold = SedimentaryFlatSedimentThreshold,
                        Strength = SedimentaryFlatSmoothingStrength
                    };

                    handle = flatJob.Schedule(PixelCount, 64, handle);
                    Swap(ref current, ref next);
                }

                for (int i = 0; i < ThermalSlumpIterations; i++)
                {
                    var slumpJob = new ThermalSlumpingJob
                    {
                        InputHeights01 = current,
                        OutputHeights01 = next,
                        WearMask = wear,
                        Width = Resolution,
                        Height = Resolution,
                        CellSizeMeters = 1f,
                        HeightScaleMeters = ErosionHeightScaleMeters,
                        TalusAngleDegrees = 45f,
                        Strength = 0.32f,
                        WriteWearMaskFlag = 0
                    };

                    handle = slumpJob.Schedule(PixelCount, 64, handle);
                    Swap(ref current, ref next);
                }

                if (RunCanyonWallPass)
                {
                    var canyonJob = new CanyonWallSteepeningJob
                    {
                        InputHeights01 = current,
                        OutputHeights01 = next,
                        ErosionDepthMask = wear,
                        Width = Resolution,
                        Height = Resolution,
                        DepthThreshold = CanyonDepthThreshold,
                        Strength = CanyonWallStrength,
                        MaxLift01 = CanyonMaxLift01
                    };

                    handle = canyonJob.Schedule(PixelCount, 64, handle);
                    Swap(ref current, ref next);
                }

                handle = new ErosionSmokeMetricsJob
                {
                    Before = before,
                    After = current,
                    Sediment = sediment,
                    Wear = wear,
                    Metrics = metrics
                }.Schedule(handle);

                // COLD SYNC JOB: editor harness must block to write deterministic PNG artifacts.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                handleScheduled = false;

                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
                Directory.CreateDirectory(folder);
                WriteHeightPng(before, Path.Combine(folder, "ErosionTestHarness_Before.png"));
                WriteHeightPng(current, Path.Combine(folder, "ErosionTestHarness_After.png"));
                WriteNormalPng(current, Path.Combine(folder, "ErosionTestHarness_After_Normal.png"), ErosionHeightScaleMeters, 1f);
                WriteMaskPng(sediment, Path.Combine(folder, "ErosionTestHarness_SedimentMask.png"));
                WriteMaskPng(wear, Path.Combine(folder, "ErosionTestHarness_ErosionDepthMask.png"));
                WriteMetricsJson(metrics[0], Path.Combine(folder, "ErosionTestHarness_Metrics.json"));
                WriteMacroShelfPreviewArtifacts(folder);

                AssetDatabase.Refresh();
                H8Debug.Log("[ErosionTestHarness] Wrote erosion PNG artifacts to " + folder);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref before, ref beforeRegistrationId);
                DisposeTracked(ref heightA, ref heightARegistrationId);
                DisposeTracked(ref heightB, ref heightBRegistrationId);
                DisposeTracked(ref sediment, ref sedimentRegistrationId);
                DisposeTracked(ref wear, ref wearRegistrationId);
                DisposeTrackedQueue(ref heightDeltas, ref heightDeltasRegistrationId);
                DisposeTracked(ref heightDeltaBudget, ref heightDeltaBudgetRegistrationId);
                DisposeTracked(ref metrics, ref metricsRegistrationId);
            }
        }

        private static void WriteMacroShelfPreviewArtifacts(string folder)
        {
            NativeArray<float> raw = default;
            NativeArray<float> quantized = default;
            int rawRegistrationId = 0;
            int quantizedRegistrationId = 0;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                raw = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.UninitializedMemory, ShelfRawLabel, out rawRegistrationId);
                quantized = AllocateTrackedTempJobArray<float>(PixelCount, NativeArrayOptions.UninitializedMemory, ShelfQuantizedLabel, out quantizedRegistrationId);

                HectonSandboxAbyssalShelfParams parameters = CreateMacroShelfParameters();
                AbsoluteUniversePosition previewOriginAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                    ShelfPreviewOriginMeters,
                    ShelfPreviewOriginMeters,
                    ShelfAupCellSizeMeters);
                int presampledWidth = Resolution + 2;
                var presampledNodes = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.TempJob);
                var presampleJob = new HectonSandboxAbyssalShelfPresampleJob
                {
                    PresampledNodes = presampledNodes,
                    Parameters = parameters,
                    PresampledWidth = presampledWidth,
                    WorldOriginAup = previewOriginAup,
                    CellSizeMeters = ShelfPreviewCellSizeMeters
                };
                var presampleHandle = presampleJob.Schedule(presampledWidth * presampledWidth, 64);
                handle = new HectonSandboxAbyssalShelfDifferentialJob
                {
                    PresampledNodes = presampledNodes,
                    OutputHeights01 = raw,
                    Parameters = parameters,
                    Width = Resolution,
                    PresampledWidth = presampledWidth,
                    WorldOriginAup = previewOriginAup,
                    CellSizeMeters = ShelfPreviewCellSizeMeters
                }.Schedule(PixelCount, 64, presampleHandle);
                presampledNodes.Dispose(handle);
                handleScheduled = true;

                const float plateauSourceAngle = 15f;
                const float plateauTargetAngle = 3.5f;
                const float cliffSourceAngle = 45f;
                double previewCenterX = previewOriginAup.GridX * (double)ShelfAupCellSizeMeters +
                    previewOriginAup.LocalX +
                    Resolution * ShelfPreviewCellSizeMeters * 0.5;
                double previewCenterZ = previewOriginAup.GridZ * (double)ShelfAupCellSizeMeters +
                    previewOriginAup.LocalZ +
                    Resolution * ShelfPreviewCellSizeMeters * 0.5;
                float cliffTargetAngle = HectonSandboxAbyssalShelfMath.EvaluateSlopeTargetAngleDegrees(
                    new double2(previewCenterX, previewCenterZ),
                    in parameters);
                const float cliffRampEndAngle = 62f;
                handle = new HectonSandboxSlopeQuantizationJob
                {
                    InputHeights01 = raw,
                    OutputHeights01 = quantized,
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = (float)ShelfPreviewCellSizeMeters,
                    LowWorldY = ShelfLowWorldY,
                    HighWorldY = ShelfHighWorldY,
                    PlateauSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauSourceAngle),
                    PlateauTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauTargetAngle),
                    CliffSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffSourceAngle),
                    CliffRampEndGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffRampEndAngle),
                    CliffTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffTargetAngle),
                    Strength = 1f
                }.Schedule(PixelCount, 64, handle);

                // COLD SYNC JOB: editor harness blocks to write deterministic macro shelf PNG artifacts.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                handleScheduled = false;

                WriteHeightPng(quantized, Path.Combine(folder, "ErosionTestHarness_MacroShelf.png"));
                WriteNormalPng(
                    quantized,
                    Path.Combine(folder, "ErosionTestHarness_MacroShelf_Normal.png"),
                    // Identical to the former (ShelfHighWorldY - ShelfLowWorldY): the span const is
                    // defined as DefaultHighWorldY - DefaultLowWorldY, so this is the same 7000f.
                    WorldVerticalExtentMath.DefaultVerticalSpanMeters,
                    (float)ShelfPreviewCellSizeMeters);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref raw, ref rawRegistrationId);
                DisposeTracked(ref quantized, ref quantizedRegistrationId);
            }
        }

        private static HectonSandboxAbyssalShelfParams CreateMacroShelfParameters()
        {
            return new HectonSandboxAbyssalShelfParams
            {
                AupCellSizeMeters = ShelfAupCellSizeMeters,
                DescentRadiusMeters = 15000.0,
                PlateCellSizeMeters = 4200.0,
                HighWorldY = ShelfHighWorldY,
                LowWorldY = ShelfLowWorldY,
                RidgeHeightMeters = 700f,
                RidgeMultiplier = 0.08f,
                RidgeWidthMeters = 1450f,
                JunctionWidthMeters = 2800f,
                PlateUniformity = 0.78f,
                DomainWarpMeters = 1450f,
                DomainWarpFrequency = 0.00011f,
                SlopeNoiseFrequency = 0.00003125f,
                MacroExponentialFalloff = 3.1f,
                ShelfRunMeters = 15000f,
                ShelfTargetSlopeDegrees = 30f,
                TrenchDepthMeters = 5000f,
                TrenchWidthMeters = 780f,
                TrenchSharpness = 2.4f,
                IslandCenterRadiusMeters = 2600f,
                IslandJunctionThreshold = 0.58f,
                Seed = HectonSandboxAbyssalShelfMath.CombineWorldSeed(880031u, 0),
                MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
            };
        }

        private static int ResolveDropletsPerSlice(int maxOperations, int currentOperations)
        {
            int maxOps = math.max(MinDropletsPerScheduleSlice, maxOperations);
            int currentOps = math.max(0, currentOperations);
            return math.clamp(maxOps - currentOps, MinDropletsPerScheduleSlice, MaxDropletsPerScheduleSlice);
        }

        private static int ResolveCurrentErosionOperations(
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

        private static int ResolveHeightDeltaQueueCapacity(int dropletsPerSlice, int maxLifetime)
        {
            return HydraulicErosionScheduler.ResolveTrackedHeightDeltaQueueCapacity(
                dropletsPerSlice,
                maxLifetime,
                1024,
                MaxTrackedHeightDeltaQueueCapacity);
        }

        private static int ResolveHeightDeltaApplyBudget(int dropletsPerSlice, int maxLifetime)
        {
            int queueCapacity = ResolveHeightDeltaQueueCapacity(dropletsPerSlice, maxLifetime);
            return math.clamp(queueCapacity / 16, 1024, MaxHeightDeltaApplyPerJob);
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, NativeArrayOptions options, string label, out int registrationId) where T : struct
        {
            registrationId = 0;
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            if (!array.IsCreated)
                throw new System.InvalidOperationException("[ErosionTestHarness] NativeArray allocation failed for " + label + ".");

            try
            {
                registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (registrationId <= 0)
                    throw new System.InvalidOperationException("[ErosionTestHarness] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                registrationId = 0;
                throw;
            }

            return array;
        }

        private static NativeQueue<HydraulicErosionHeightDelta> AllocateTrackedHeightDeltaQueue(int heightDeltaQueueCapacity, out int registrationId)
        {
            registrationId = 0;
            NativeQueue<HydraulicErosionHeightDelta> queue = new NativeQueue<HydraulicErosionHeightDelta>(Allocator.TempJob);
            if (!queue.IsCreated)
                throw new System.InvalidOperationException("[ErosionTestHarness] NativeQueue allocation failed for " + HeightDeltaQueueLabel + ".");

            try
            {
                registrationId = NativeMemorySentinel.RegisterNativeQueueInstance(queue, heightDeltaQueueCapacity, NativeMemoryOwner, HeightDeltaQueueLabel, NativeAllocationLifetime.TempJob);
                if (registrationId <= 0)
                    throw new System.InvalidOperationException("[ErosionTestHarness] NativeMemorySentinel rejected NativeQueue registration for " + HeightDeltaQueueLabel + ".");
            }
            catch
            {
                System.Exception nativeSentinelCleanupException0 = null;

                if (registrationId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(registrationId);
                    }
                    catch (System.Exception nativeSentinelException0)
                    {
                        nativeSentinelCleanupException0 = nativeSentinelException0;
                    }
                    finally
                    {
                        registrationId = 0;
                    }
                }

                try
                {
                    queue.Dispose();
                }
                catch (System.Exception nativeSentinelException0)
                {
                    if (nativeSentinelCleanupException0 == null)
                        nativeSentinelCleanupException0 = nativeSentinelException0;
                }

                if (nativeSentinelCleanupException0 != null)
                    throw nativeSentinelCleanupException0;

                throw;
            }

            return queue;
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array, ref int registrationId) where T : struct
        {
            System.Exception firstException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (System.Exception exception)
                {
                    firstException = exception;
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
                    if (firstException == null)
                        firstException = exception;
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

            if (firstException != null)
                throw firstException;
        }

        private static void DisposeTrackedQueue(ref NativeQueue<HydraulicErosionHeightDelta> queue, ref int registrationId)
        {
            System.Exception firstException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (System.Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void WriteHeightPng(NativeArray<float> heights, string path)
        {
            NativeArray<Color32> pixels = default;
            int pixelsRegistrationId = 0;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                pixels = AllocateTrackedTempJobArray<Color32>(PixelCount, NativeArrayOptions.UninitializedMemory, HeightPixelsLabel, out pixelsRegistrationId);

                handle = new ErosionGrayscalePngBakeJob
                {
                    Values = heights,
                    Pixels = pixels
                }.Schedule(PixelCount, 64);
                handleScheduled = true;

                // COLD SYNC JOB: editor harness blocks to write deterministic grayscale PNG artifacts.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                handleScheduled = false;

                WritePng(pixels, path);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref pixels, ref pixelsRegistrationId);
            }
        }

        private static void WriteNormalPng(NativeArray<float> heights, string path, float heightScaleMeters, float cellSizeMeters)
        {
            NativeArray<Color32> pixels = default;
            int pixelsRegistrationId = 0;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                pixels = AllocateTrackedTempJobArray<Color32>(PixelCount, NativeArrayOptions.UninitializedMemory, NormalPixelsLabel, out pixelsRegistrationId);

                handle = new ErosionNormalMapBakeJob
                {
                    Heights = heights,
                    Pixels = pixels,
                    Width = Resolution,
                    Height = Resolution,
                    HeightScaleMeters = math.max(0.001f, heightScaleMeters),
                    CellSizeMeters = math.max(0.001f, cellSizeMeters)
                }.Schedule(PixelCount, 64);
                handleScheduled = true;

                // COLD SYNC JOB: editor harness blocks to write deterministic normal-map PNG artifacts.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                handleScheduled = false;

                WritePng(pixels, path);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref pixels, ref pixelsRegistrationId);
            }
        }

        private static void WriteMaskPng(NativeArray<float> mask, string path)
        {
            NativeArray<Color32> pixels = default;
            NativeArray<float> maxValue = default;
            int pixelsRegistrationId = 0;
            int maxValueRegistrationId = 0;
            JobHandle maxHandle = default;
            JobHandle bakeHandle = default;
            bool maxHandleScheduled = false;
            bool bakeHandleScheduled = false;

            try
            {
                pixels = AllocateTrackedTempJobArray<Color32>(PixelCount, NativeArrayOptions.UninitializedMemory, MaskPixelsLabel, out pixelsRegistrationId);
                maxValue = AllocateTrackedTempJobArray<float>(1, NativeArrayOptions.ClearMemory, MaskMaxLabel, out maxValueRegistrationId);

                maxHandle = new ErosionMaskMaxJob
                {
                    Values = mask,
                    MaxValue = maxValue
                }.Schedule();
                maxHandleScheduled = true;

                bakeHandle = new ErosionMaskPngBakeJob
                {
                    Values = mask,
                    MaxValue = maxValue,
                    Pixels = pixels
                }.Schedule(PixelCount, 64, maxHandle);
                bakeHandleScheduled = true;
                maxHandleScheduled = false;

                // COLD SYNC JOB: editor harness blocks to write deterministic mask PNG artifacts.
                DispatcherJobSwap.TryComplete(ref bakeHandle, forceComplete: true);
                bakeHandleScheduled = false;

                WritePng(pixels, path);
            }
            finally
            {
                if (bakeHandleScheduled)
                    DispatcherJobSwap.TryComplete(ref bakeHandle, forceComplete: true);
                else if (maxHandleScheduled)
                    DispatcherJobSwap.TryComplete(ref maxHandle, forceComplete: true);

                DisposeTracked(ref maxValue, ref maxValueRegistrationId);
                DisposeTracked(ref pixels, ref pixelsRegistrationId);
            }
        }

        private static void WriteMetricsJson(ErosionSmokeMetrics metrics, string path)
        {
            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - editor smoke JSON artifact buffer - owner: ErosionTestHarness
            builder.Append("{\n");
            AppendJsonProperty(builder, "schema", "hecton8.erosion_smoke_metrics.v1", true);
            AppendJsonProperty(builder, "resolution", Resolution, true);
            AppendJsonProperty(builder, "dropletCount", ErosionDropletCount, true);
            AppendJsonProperty(builder, "thermalIterations", ThermalSlumpIterations, true);
            AppendJsonProperty(builder, "minBefore", metrics.MinBefore, true);
            AppendJsonProperty(builder, "maxBefore", metrics.MaxBefore, true);
            AppendJsonProperty(builder, "minAfter", metrics.MinAfter, true);
            AppendJsonProperty(builder, "maxAfter", metrics.MaxAfter, true);
            AppendJsonProperty(builder, "maxSediment", metrics.MaxSediment, true);
            AppendJsonProperty(builder, "maxWear", metrics.MaxWear, true);
            AppendJsonProperty(builder, "meanAbsoluteDelta", metrics.MeanAbsoluteDelta, true);
            AppendJsonProperty(builder, "changedCellCount", metrics.ChangedCellCount, true);
            AppendJsonProperty(builder, "nonFiniteCellCount", metrics.NonFiniteCellCount, false);
            builder.Append("\n}\n");
            File.WriteAllText(path, builder.ToString(), JsonEncoding);
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool comma)
        {
            AppendJsonName(builder, name);
            builder.Append('"');
            builder.Append(value);
            builder.Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, int value, bool comma)
        {
            AppendJsonName(builder, name);
            builder.Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, float value, bool comma)
        {
            AppendJsonName(builder, name);
            builder.Append(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJsonName(StringBuilder builder, string name)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
        }

        private static byte ToByte(float value)
        {
            return (byte)math.round(math.saturate(value) * 255f);
        }

        private static void WritePng(NativeArray<Color32> pixels, string path)
        {
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true);
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            byte[] pngBytes = texture.EncodeToPNG(); // COLD ALLOC: byte[] - editor-only PNG encode output - owner: ErosionTestHarness
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(pngBytes, 0, pngBytes.Length);
                stream.Flush(true);
            }

            Object.DestroyImmediate(texture);
        }

        private static void Swap(ref NativeArray<float> current, ref NativeArray<float> next)
        {
            NativeArray<float> swap = current;
            current = next;
            next = swap;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ErosionGrayscalePngBakeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Values;
            [WriteOnly] public NativeArray<Color32> Pixels;

            public void Execute(int index)
            {
                byte value = ToByte(Values[index]);
                Pixels[index] = new Color32(value, value, value, 255);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ErosionNormalMapBakeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Heights;
            [WriteOnly] public NativeArray<Color32> Pixels;
            public int Width;
            public int Height;
            public float HeightScaleMeters;
            public float CellSizeMeters;

            public void Execute(int index)
            {
                int width = math.max(1, Width);
                int height = math.max(1, Height);
                int x = index % width;
                int z = index / width;
                int xLeft = math.max(0, x - 1);
                int xRight = math.min(width - 1, x + 1);
                int zBack = math.max(0, z - 1);
                int zForward = math.min(height - 1, z + 1);
                float safeHeightScale = math.max(0.001f, HeightScaleMeters);
                float invCellSize = 0.5f / math.max(0.001f, CellSizeMeters);
                float left = Heights[z * width + xLeft] * safeHeightScale;
                float right = Heights[z * width + xRight] * safeHeightScale;
                float back = Heights[zBack * width + x] * safeHeightScale;
                float forward = Heights[zForward * width + x] * safeHeightScale;
                float dx = (right - left) * invCellSize;
                float dz = (forward - back) * invCellSize;
                float3 normal = math.normalize(new float3(-dx, 1f, -dz));
                Pixels[index] = new Color32(
                    ToByte(normal.x * 0.5f + 0.5f),
                    ToByte(normal.y * 0.5f + 0.5f),
                    ToByte(normal.z * 0.5f + 0.5f),
                    255);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ErosionMaskMaxJob : IJob
        {
            [ReadOnly] public NativeArray<float> Values;
            [WriteOnly] public NativeArray<float> MaxValue;

            public void Execute()
            {
                float maxValue = 0f;
                int count = Values.Length;
                for (int i = 0; i < count; i++)
                    maxValue = math.max(maxValue, Values[i]);

                MaxValue[0] = maxValue;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ErosionMaskPngBakeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Values;
            [ReadOnly] public NativeArray<float> MaxValue;
            [WriteOnly] public NativeArray<Color32> Pixels;

            public void Execute(int index)
            {
                float maxValue = MaxValue[0];
                float invMax = maxValue > 0.000001f ? 1f / maxValue : 0f;
                byte value = ToByte(Values[index] * invMax);
                Pixels[index] = new Color32(value, value, value, 255);
            }
        }
    }
}






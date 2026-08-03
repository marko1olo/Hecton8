using Den.Tools.Matrices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapMagic.Nodes.MatrixGenerators
{
    /// <summary>
    /// MapMagic base height adapter for the HECTON macro geology source fields.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Macro Geology Base",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSandboxAbyssalShelfMapMagicNode : Generator, IOutlet<MatrixWorld>
    {
        // L19 hop2 LIVE: MapMagic batch flag - IsHeadlessBatchProbe() is main-thread only;
        // Generate() runs on MapMagic worker threads so cache via command line.
        static bool IsHeadlessBatchProbe()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-batchmode", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private const string NativeMemoryOwner = nameof(HectonSandboxAbyssalShelfMapMagicNode);
        private const double SyncCompletionWarningMilliseconds = 0.2;
        private static readonly uint _invalidMatrixWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.InvalidMatrix"));
        private static readonly uint _syncCompletionWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SyncCompletionMs"));
        private static readonly uint _mapMagicContextHash =
            unchecked((uint)LocHash.Compute("MapMagic.SandboxAbyssalShelf"));

        /// <summary>
        /// Last reported terrain-height-vs-geology-span mismatch, hashed from the value PAIR. Written
        /// only through <see cref="System.Threading.Interlocked.Exchange(ref int, int)"/> because
        /// Generate runs on MapMagic worker threads. See the mismatch check in Generate for why this is
        /// keyed on values rather than being a one-shot bool.
        /// </summary>
        private static int _heightMismatchReportKey;

        // Vertical extent lives in ONE place: WorldVerticalExtentMath
        // (Scripts/World/WorldVerticalExtentContracts.cs). These initialisers were the de facto source of
        // vertical truth and had been hand-copied into ErosionTestHarness, HectonSandboxAbyssalShelfSmokeTester
        // and ProceduralWreckGenerator. Same numbers, one owner - no generated geometry changed, and the
        // serialised values already in HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset are untouched either way.
        [Den.Tools.GUI.ValAttribute("High Y m")] public float highWorldY = WorldVerticalExtentMath.DefaultHighWorldY;
        [Den.Tools.GUI.ValAttribute("Low Y m")] public float lowWorldY = WorldVerticalExtentMath.DefaultLowWorldY;
        [Den.Tools.GUI.ValAttribute("Descent Radius m")] public float descentRadiusMeters = 15000f;
        [Den.Tools.GUI.ValAttribute("Exponential Falloff")] public float macroExponentialFalloff = 3.1f;
        [Den.Tools.GUI.ValAttribute("Shelf Run m")] public float shelfRunMeters = 15000f;
        [Den.Tools.GUI.ValAttribute("Shelf Slope deg")] public float shelfTargetSlopeDegrees = 30f;

        [Den.Tools.GUI.ValAttribute("Plate Cell m")] public float plateCellSizeMeters = 4200f;
        [Den.Tools.GUI.ValAttribute("Ridge Height m")] public float ridgeHeightMeters = 700f;
        [Den.Tools.GUI.ValAttribute("Ridge Multiplier")] public float ridgeMultiplier = 0.08f;
        [Den.Tools.GUI.ValAttribute("Ridge Width m")] public float ridgeWidthMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Junction Width m")] public float junctionWidthMeters = 2800f;
        [Den.Tools.GUI.ValAttribute("Plate Uniformity")] public float plateUniformity = 0.78f;
        [Den.Tools.GUI.ValAttribute("Warp m")] public float domainWarpMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Warp Frequency")] public float domainWarpFrequency = 0.00011f;
        [Den.Tools.GUI.ValAttribute("Slope Noise Frequency")] public float slopeNoiseFrequency = 0.00003125f;
        [Den.Tools.GUI.ValAttribute("Trench Depth m")] public float trenchDepthMeters = 5000f;
        [Den.Tools.GUI.ValAttribute("Trench Width m")] public float trenchWidthMeters = 780f;
        [Den.Tools.GUI.ValAttribute("Trench Sharpness")] public float trenchSharpness = 2.4f;
        [Den.Tools.GUI.ValAttribute("Island Radius m")] public float islandCenterRadiusMeters = 2600f;
        [Den.Tools.GUI.ValAttribute("Island Junction")] public float islandJunctionThreshold = 0.58f;
        [Den.Tools.GUI.ValAttribute("Reef Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> reefMaskOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Playa Lake Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> lakeMaskOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Steep Rock Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> steepRockOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Continentality", "Outlet")]
        public readonly Outlet<MatrixWorld> continentalityOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Ledge Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> ledgeMaskOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Cave Entrance Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> caveEntranceOut = new Outlet<MatrixWorld>();
        [Den.Tools.GUI.ValAttribute("Brine Pool Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> brinePoolOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = WorldMacroGeologyFields.DefaultAuthoringSeed;

        [Den.Tools.GUI.ValAttribute("Quantize Slopes")] public bool enableSlopeQuantization = false;
        [Den.Tools.GUI.ValAttribute("Plateau Source deg")] public float plateauSourceAngleDegrees = 8f;
        [Den.Tools.GUI.ValAttribute("Plateau Target deg")] public float plateauTargetAngleDegrees = 2.5f;
        [Den.Tools.GUI.ValAttribute("Cliff Source deg")] public float cliffSourceAngleDegrees = 45f;
        [Den.Tools.GUI.ValAttribute("Cliff Target deg")] public float cliffTargetAngleDegrees = 52f;
        [Den.Tools.GUI.ValAttribute("Quantize Strength")] public float slopeQuantizationStrength = 1f;

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop)
                return;

            if (!enabled)
            {
                data.RemoveProduct(this);
                return;
            }

            MatrixWorld dst = new MatrixWorld(
                data.area.full.rect,
                data.area.full.worldPos,
                data.area.full.worldSize,
                data.globals.height);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float expectedHeight = math.max(highWorldY, lowWorldY + 1f) - lowWorldY;
            float globalsHeight = data.globals.height;
            if (math.abs(globalsHeight - expectedHeight) > 1f)
            {
                // This compares two GLOBAL configuration values - one pair of graph-asset fields and one
                // MapMagicObject scene field - neither of which can vary per tile. Generate runs once per
                // tile-generation task on MapMagic worker threads (TerrainTile.cs:550 ->
                // ThreadManager.cs:221), and draft and main are separate passes, so it re-tested and
                // re-reported the same global mismatch 52-53 times per headless run: 52 in
                // omega_route18, 53 in omega_route19, 52 in omega_route20. That volume buried the log.
                //
                // Keyed on the VALUE PAIR rather than a one-shot bool on purpose. A bool would report the
                // first mismatch and then stay permanently silent, so if someone later fixed the height
                // and broke it again to a different value, the project would never hear about it - and
                // this warning is the only signal that authored relief is being rendered into a smaller
                // terrain box, which collapses every slope, cliff, shelf-break and trench in the world by
                // the ratio of the two numbers. Keying on the values means each DISTINCT mismatch reports
                // exactly once and a genuine later change still speaks up.
                //
                // NUMBERS, kept current on purpose - the two quoted here when this guard was written
                // (12000m authored relief into 250m of terrain, a 48x collapse) are both historical and no
                // longer describe the tree. Authored window is now
                // WorldVerticalExtentMath.DefaultVerticalSpanMeters = 7000m
                // (Scripts/World/WorldVerticalExtentContracts.cs). MapMagic's own untouched default for
                // globals.height is 250m (Assets/MapMagic/Core/MapMagicObject.cs:512), and the shipped
                // sandbox scene builder writes 4000m
                // (WorldVerticalExtentMath.SandboxV2AuthoredTerrainHeightMeters, from
                // Scripts/Editor/CreateSandboxV2.cs) - so 020_RENDER_SANDBOX_V2 trips this warning at a
                // 1.75x collapse, and any scene left on the MapMagic default trips it at 28x.
                //
                // Interlocked, not a plain field: this executes on MapMagic worker threads, and several
                // tiles hit it concurrently. The golden-ratio multiply is the usual 32-bit mix; unchecked
                // is explicit for the uint->int cast, and key 0 is remapped because 0 is the initial
                // field value and would otherwise suppress the first genuine report.
                int reportKey = unchecked((int)(math.asuint(globalsHeight) ^ (math.asuint(expectedHeight) * 0x9E3779B9u)));
                if (reportKey == 0)
                    reportKey = 1;

                if (System.Threading.Interlocked.Exchange(ref _heightMismatchReportKey, reportKey) != reportKey)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[HectonMacroGeology] TerrainData.size.y ({globalsHeight:F1}m) != geology Y-span ({expectedHeight:F1}m). " +
                        $"Graph terrain height should equal HighWorldY - LowWorldY = {expectedHeight:F1}m. " +
                        "Run Hecton8/World/MapMagic/Sync Terrain Height To Geology Span - REPORT ONLY to see " +
                        "what a repair would change. Reported once per distinct value pair, not per tile.");
                }
            }
#endif

            float[] target = dst.arr;
            int cellCount = target != null ? target.Length : 0;
            int width = math.max(1, dst.rect.size.x);
            int height = math.max(1, dst.rect.size.z);
            if (cellCount <= 0 || width * height > cellCount)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _invalidMatrixWarningHash,
                    _mapMagicContextHash,
                    cellCount);
                data.RemoveProduct(this);
                return;
            }

            NativeArray<float> rawHeights = default;
            NativeArray<float> quantizedHeights = default;
            NativeArray<float> reefArr = default;
            NativeArray<float> lakeArr = default;
            NativeArray<float> steepRockArr = default;
            NativeArray<float> continentalityArr = default;
            NativeArray<float> ledgeArr = default;
            NativeArray<float> caveEntranceArr = default;
            NativeArray<float> brinePoolArr = default;

            int rawHeightsRegistrationId = 0;
            int quantizedHeightsRegistrationId = 0;
            int reefRegId = 0;
            int lakeRegId = 0;
            int steepRockRegId = 0;
            int continentalityRegId = 0;
            int ledgeRegId = 0;
            int caveEntranceRegId = 0;
            int brinePoolRegId = 0;

            JobHandle generationHandle = default;
            bool generationHandleScheduled = false;
            try
            {
                rawHeights = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                quantizedHeights = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                reefArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                lakeArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                steepRockArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                continentalityArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                ledgeArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                caveEntranceArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                brinePoolArr = new NativeArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                rawHeightsRegistrationId = RegisterTempJobArray(rawHeights, nameof(rawHeights));
                quantizedHeightsRegistrationId = RegisterTempJobArray(quantizedHeights, nameof(quantizedHeights));
                reefRegId = RegisterTempJobArray(reefArr, nameof(reefArr));
                lakeRegId = RegisterTempJobArray(lakeArr, nameof(lakeArr));
                steepRockRegId = RegisterTempJobArray(steepRockArr, nameof(steepRockArr));
                continentalityRegId = RegisterTempJobArray(continentalityArr, nameof(continentalityArr));
                ledgeRegId = RegisterTempJobArray(ledgeArr, nameof(ledgeArr));
                caveEntranceRegId = RegisterTempJobArray(caveEntranceArr, nameof(caveEntranceArr));
                brinePoolRegId = RegisterTempJobArray(brinePoolArr, nameof(brinePoolArr));

                double sampleCellSizeMeters = ResolveCellSizeMeters(dst);
                AbsoluteUniversePosition worldOriginAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                    dst.worldPos.x,
                    dst.worldPos.z,
                    AbsoluteUniversePosition.CellSizeMeters);
                uint worldSeed = WorldMacroGeologyFields.CombineWorldSeed(
                    unchecked((uint)seed),
                    ResolveRuntimeWorldSeed());
                var parameters = new HectonSandboxAbyssalShelfParams
                {
                    AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                    DescentRadiusMeters = math.max(15000.0, shelfRunMeters > 0f ? shelfRunMeters : descentRadiusMeters),
                    PlateCellSizeMeters = math.max(1f, plateCellSizeMeters),
                    HighWorldY = math.max(highWorldY, lowWorldY + 1f),
                    LowWorldY = lowWorldY,
                    RidgeHeightMeters = math.max(0f, ridgeHeightMeters),
                    RidgeMultiplier = math.max(0f, ridgeMultiplier),
                    RidgeWidthMeters = math.max(0.001f, ridgeWidthMeters),
                    JunctionWidthMeters = math.max(0.001f, junctionWidthMeters),
                    PlateUniformity = math.saturate(plateUniformity),
                    DomainWarpMeters = math.max(0f, domainWarpMeters),
                    DomainWarpFrequency = math.max(0.000001f, domainWarpFrequency),
                    SlopeNoiseFrequency = math.max(0.000001f, slopeNoiseFrequency),
                    MacroExponentialFalloff = math.max(0.1f, macroExponentialFalloff),
                    ShelfRunMeters = math.max(1f, shelfRunMeters),
                    ShelfTargetSlopeDegrees = math.clamp(shelfTargetSlopeDegrees, 1f, 75f),
                    TrenchDepthMeters = math.max(0f, trenchDepthMeters),
                    TrenchWidthMeters = math.max(1f, trenchWidthMeters),
                    TrenchSharpness = math.max(0.35f, trenchSharpness),
                    IslandCenterRadiusMeters = math.max(1f, islandCenterRadiusMeters),
                    IslandJunctionThreshold = math.saturate(islandJunctionThreshold),
                    Seed = worldSeed,
                    MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
                };

                int presampledWidth = width + 2;
                int presampledCount = presampledWidth * presampledWidth;
                NativeArray<PresampledMacroNode> presampledNodes = new NativeArray<PresampledMacroNode>(
                    presampledCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                NativeArray<float> finalHeights;
                double completeMilliseconds;

                // L19 hop2 LIVE: ScheduleParallelFor(PresampleJob) has produced mono_jit_info_table AV
                // under headless batch probes. Skip Unity job scheduling and emit a flat mid-shelf so
                // MapMagic can continue without ParallelFor JIT on this path.
                if (UnityEngine.IsHeadlessBatchProbe())
                {
                    for (int i = 0; i < cellCount; i++)
                    {
                        rawHeights[i] = 0.5f;
                        reefArr[i] = 0f;
                        lakeArr[i] = 0f;
                        steepRockArr[i] = 0f;
                        continentalityArr[i] = 0f;
                        ledgeArr[i] = 0f;
                        caveEntranceArr[i] = 0f;
                        brinePoolArr[i] = 0f;
                    }

                    if (presampledNodes.IsCreated)
                        presampledNodes.Dispose();
                    generationHandleScheduled = false;
                    finalHeights = rawHeights;
                    completeMilliseconds = 0.0;
                }
                else
                {
                    var presampleJob = new HectonSandboxAbyssalShelfPresampleJob
                    {
                        PresampledNodes = presampledNodes,
                        Parameters = parameters,
                        PresampledWidth = presampledWidth,
                        WorldOriginAup = worldOriginAup,
                        CellSizeMeters = sampleCellSizeMeters
                    };
                
                    JobHandle presampleHandle = presampleJob.Schedule(presampledCount, ResolveBatchCount(presampledCount));

                    var differentialJob = new HectonSandboxAbyssalShelfDifferentialJob
                    {
                        PresampledNodes = presampledNodes,
                        OutputHeights01 = rawHeights,
                        OutputReef = reefArr,
                        OutputLake = lakeArr,
                        OutputSteepRock = steepRockArr,
                        OutputContinentality = continentalityArr,
                        OutputLedge = ledgeArr,
                        OutputCaveEntrance = caveEntranceArr,
                        OutputBrinePool = brinePoolArr,
                        Parameters = parameters,
                        Width = width,
                        PresampledWidth = presampledWidth,
                        WorldOriginAup = worldOriginAup,
                        CellSizeMeters = sampleCellSizeMeters
                    };

                    generationHandle = differentialJob.Schedule(cellCount, ResolveBatchCount(cellCount), presampleHandle);
                
                    // Dispose of the temp array after the generation handle completes
                    generationHandle = presampledNodes.Dispose(generationHandle);
                    generationHandleScheduled = true;
                    finalHeights = rawHeights;

                    if (enableSlopeQuantization && width > 2 && height > 2)
                    {
                        float plateauTargetAngle = math.clamp(plateauTargetAngleDegrees, 1f, 60f);
                        float plateauSourceAngle = math.clamp(plateauSourceAngleDegrees, plateauTargetAngle + 0.001f, 45f);
                        float cliffSourceAngle = math.clamp(cliffSourceAngleDegrees, plateauSourceAngle + 0.001f, 62f);
                        double centerAupX = worldOriginAup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters +
                            worldOriginAup.LocalX +
                            dst.worldSize.x * 0.5;
                        double centerAupZ = worldOriginAup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters +
                            worldOriginAup.LocalZ +
                            dst.worldSize.z * 0.5;
                        float noisyCliffTargetAngle = HectonSandboxAbyssalShelfMath.EvaluateSlopeTargetAngleDegrees(
                            new double2(centerAupX, centerAupZ),
                            in parameters);
                        float cliffTargetLimit = math.min(math.max(22f, cliffTargetAngleDegrees), 62f);
                        float cliffTargetAngle = math.clamp(math.min(noisyCliffTargetAngle, cliffTargetLimit), 22f, 38f);
                        float cliffRampEndAngle = math.min(89f, cliffSourceAngle + math.max(1f, 62f - cliffSourceAngle));
                        var quantizeJob = new HectonSandboxSlopeQuantizationJob
                        {
                            InputHeights01 = rawHeights,
                            OutputHeights01 = quantizedHeights,
                            Width = width,
                            Height = height,
                            CellSizeMeters = (float)math.max(0.001, sampleCellSizeMeters),
                            LowWorldY = parameters.LowWorldY,
                            HighWorldY = parameters.HighWorldY,
                            PlateauSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauSourceAngle),
                            PlateauTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauTargetAngle),
                            CliffSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffSourceAngle),
                            CliffRampEndGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffRampEndAngle),
                            CliffTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffTargetAngle),
                            Strength = slopeQuantizationStrength
                        };

                        generationHandle = quantizeJob.Schedule(cellCount, ResolveBatchCount(cellCount), generationHandle);
                        finalHeights = quantizedHeights;
                    }

                    long completeStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    DispatcherJobSwap.ForceCompleteFromWorkerThread(ref generationHandle);
                    generationHandleScheduled = false;
                    completeMilliseconds =
                        (System.Diagnostics.Stopwatch.GetTimestamp() - completeStartTimestamp) *
                        1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                }
                if (completeMilliseconds > SyncCompletionWarningMilliseconds)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _syncCompletionWarningHash,
                        _mapMagicContextHash,
                        (float)completeMilliseconds);
                }

                if (stop != null && stop.stop)
                    return;

                NativeArray<float>.Copy(finalHeights, target, cellCount);

                data.StoreProduct(this, dst);

                // Store products for connected gameplay & geology outlets
                if (reefMaskOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(reefArr, mat.arr, cellCount);
                    data.StoreProduct(reefMaskOut, mat);
                }
                if (lakeMaskOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(lakeArr, mat.arr, cellCount);
                    data.StoreProduct(lakeMaskOut, mat);
                }
                if (steepRockOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(steepRockArr, mat.arr, cellCount);
                    data.StoreProduct(steepRockOut, mat);
                }
                if (continentalityOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(continentalityArr, mat.arr, cellCount);
                    data.StoreProduct(continentalityOut, mat);
                }
                if (ledgeMaskOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(ledgeArr, mat.arr, cellCount);
                    data.StoreProduct(ledgeMaskOut, mat);
                }
                if (caveEntranceOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(caveEntranceArr, mat.arr, cellCount);
                    data.StoreProduct(caveEntranceOut, mat);
                }
                if (brinePoolOut != null)
                {
                    MatrixWorld mat = new MatrixWorld(dst.rect, dst.worldPos, dst.worldSize);
                    NativeArray<float>.Copy(brinePoolArr, mat.arr, cellCount);
                    data.StoreProduct(brinePoolOut, mat);
                }
            }
            finally
            {
                if (generationHandleScheduled)
                    DispatcherJobSwap.ForceCompleteFromWorkerThread(ref generationHandle);

                DisposeTracked(ref rawHeights, ref rawHeightsRegistrationId);
                DisposeTracked(ref quantizedHeights, ref quantizedHeightsRegistrationId);
                DisposeTracked(ref reefArr, ref reefRegId);
                DisposeTracked(ref lakeArr, ref lakeRegId);
                DisposeTracked(ref steepRockArr, ref steepRockRegId);
                DisposeTracked(ref continentalityArr, ref continentalityRegId);
                DisposeTracked(ref ledgeArr, ref ledgeRegId);
                DisposeTracked(ref caveEntranceArr, ref caveEntranceRegId);
                DisposeTracked(ref brinePoolArr, ref brinePoolRegId);
            }
        }

        private static int RegisterTempJobArray(NativeArray<float> array, string label)
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeAllocationLifetime.TransientArena);
            if (registrationId <= 0)
                throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");

            return registrationId;
        }

        private static void DisposeTracked(ref NativeArray<float> array, ref int registrationId)
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

        private static double ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001, matrix.worldSize.x / safeWidth);
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        private static int ResolveRuntimeWorldSeed()
        {
            return global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)
                ? runtimeWorldSeed
                : 0;
        }
    }
}

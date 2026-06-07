#if UNITY_EDITOR
using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.GeologyForge
{
    internal static class TopographyForgeGenerator
    {
        private const int AsyncWriteChunkBytes = 1024 * 1024;
        private const string TempOutputSuffix = ".tmp";
        private const string BackupOutputSuffix = ".bak";
        private const string NativeMemoryOwner = nameof(TopographyForgeGenerator);
        private static bool _isBaking;
        private static bool _cancelRequested;

        static TopographyForgeGenerator()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelAsyncBake;
        }

        public static TopographyBakeSettings DefaultSettings()
        {
            TopographyBakeSettings settings = default;
            settings.SectorResolution = TopographyForgeConstants.DefaultSectorResolution;
            settings.SectorSizeMeters = TopographyForgeConstants.DefaultSectorSizeMeters;
            settings.SectorCountX = TopographyForgeConstants.DefaultWorldSizeMeters / TopographyForgeConstants.DefaultSectorSizeMeters;
            settings.SectorCountZ = TopographyForgeConstants.DefaultWorldSizeMeters / TopographyForgeConstants.DefaultSectorSizeMeters;
            settings.MacroResolution = TopographyForgeConstants.DefaultMacroResolution;
            settings.HeightMinMeters = -5200f;
            settings.HeightMaxMeters = 1800f;
            settings.SeaFloorBiasMeters = -2000f;
            settings.RidgeFrequency = 0.00032f;
            settings.RidgeAmplitude = 1f;
            settings.RidgeLacunarity = 2.04f;
            settings.RidgePersistence = 0.54f;
            settings.RidgeOctaves = 7;
            settings.WarpFrequency = 0.000086f;
            settings.WarpStrengthMeters = 860f;
            settings.TerraceSteps = 18f;
            settings.TerraceStrength = 0.28f;
            settings.RiftDepthMeters = 5000f;
            settings.RiftWidthMeters = 2200f;
            settings.WorldSeed = 0x53483234u;
            settings.GlobalQualityWeight = 1f;
            settings.WorldOriginAup = double3.zero;
            return settings;
        }

        public static bool BakeGlobalHeightmapsAsync(TopographyBakeSettings settings, Action<float> progress)
        {
            if (_isBaking)
                return false;

            _isBaking = true;
            _cancelRequested = false;
            _ = RunBakeAsync(SanitizeSettings(settings), progress);
            return true;
        }

        public static void CancelAsyncBake()
        {
            _cancelRequested = true;
        }

        public static TopographyBakeMetrics RunMockSectorBenchmark(TopographyBakeSettings settings)
        {
            settings = SanitizeSettings(settings);
            settings.SectorResolution = TopographyForgeConstants.MockSectorResolution;
            int cellCount = settings.SectorResolution * settings.SectorResolution;
            NativeArray<TopographyBakeRunStateDTO> state = default;
            NativeArray<float> heights = default;
            try
            {
                state = NewRunState(Allocator.TempJob);
                heights = AllocateTopographyArray<float>(
                    cellCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                TopographyBakeConfigDTO config = BuildSectorConfig(settings, 0, 0);
                config.Width = settings.SectorResolution;
                config.Height = settings.SectorResolution;
                Stopwatch stopwatch = Stopwatch.StartNew();
                new GenerateMockSectorJob
                {
                    HeightsMeters = heights,
                    Config = config,
                    Ridge = BuildDefaultRidge(settings)
                }.Schedule(cellCount, ResolveBatchCount(cellCount, settings.GlobalQualityWeight)).Complete();
                stopwatch.Stop();
                AddMockSectorMilliseconds(state, stopwatch.Elapsed.TotalMilliseconds);
                unsafe
                {
                    AnalyzeHeights(heights, config, state, out _, out _, out _);
                }
                SetMockSectorCounts(state);
                TopographyBakeMetrics metrics = SnapshotMetrics(state);
                WriteBakeReport(metrics, settings, 0, "mock_sector");
                return metrics;
            }
            finally
            {
                ReleaseTopographyArray(ref state);
                ReleaseTopographyArray(ref heights);
            }
        }

        private static async Awaitable RunBakeAsync(TopographyBakeSettings settings, Action<float> progress)
        {
            NativeArray<TopographyBakeTelemetryEntry> blackBox = default;
            NativeArray<TopographyBakeRunStateDTO> state = default;
            try
            {
                state = NewRunState(Allocator.Persistent);
                blackBox = AllocateTopographyArray<TopographyBakeTelemetryEntry>(
                    TopographyForgeConstants.BlackBoxFrameCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                ClearBlackBox(blackBox);
                TopographyBakeMetrics metrics = await BakeGlobalHeightmapsInternalAsync(settings, progress, blackBox, state);
                WriteBakeReport(metrics, settings, metrics.RecipeCount, "global_heightmap");
                AssetDatabase.Refresh();
                Debug.Log("[TopographyForge] Baked terrain heightmaps: sectors=" + metrics.CompletedSectors + "/" + metrics.SectorCount + ", min=" + metrics.MinHeightMeters.ToString("F2", CultureInfo.InvariantCulture) + ", max=" + metrics.MaxHeightMeters.ToString("F2", CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception ex)
            {
                unsafe
                {
                    uint reason = TopographyForgeConstants.WarningAsyncWriteFailed;
                    if (state.IsCreated && state.Length > 0)
                    {
                        reason |= SnapshotMetrics(state).WarningFlags & (
                            TopographyForgeConstants.WarningNaNHeight |
                            TopographyForgeConstants.WarningInvalidBiomeMask |
                            TopographyForgeConstants.WarningBiomeMaskRecipeOverflow);
                    }

                    DumpBlackBox(blackBox, SnapshotBlackBoxCursor(state), reason);
                }
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ReleaseTopographyArray(ref state);
                ReleaseTopographyArray(ref blackBox);
                _isBaking = false;
                _cancelRequested = false;
                progress?.Invoke(0f);
            }
        }

        private static async Awaitable<TopographyBakeMetrics> BakeGlobalHeightmapsInternalAsync(
            TopographyBakeSettings settings,
            Action<float> progress,
            NativeArray<TopographyBakeTelemetryEntry> blackBox,
            NativeArray<TopographyBakeRunStateDTO> state)
        {
            EnsureFolders();
            NativeArray<TopographyBiomeKernelDTO> recipes = default;
            NativeArray<TectonicRiftSegmentDTO> rifts = default;
            try
            {
                int recipeCount;
                recipes = LoadKernelRecipes(out recipeCount);
                ResetMetrics(state);
                SetSectorAndRecipeCounts(state, settings.SectorCountX * settings.SectorCountZ, recipeCount);
                rifts = BuildDefaultRifts(settings);
                FractalParamsDTO ridge = BuildDefaultRidge(settings);
                DomainWarpParamsDTO warp = BuildDefaultWarp(settings);
                int totalWorkUnits = math.max(1, SnapshotMetrics(state).SectorCount + 1);
                int completedWorkUnits = 0;

                for (int z = 0; z < settings.SectorCountZ; z++)
                {
                    for (int x = 0; x < settings.SectorCountX; x++)
                    {
                        if (_cancelRequested)
                            return SnapshotMetrics(state);

                        TopographyBakeConfigDTO config = BuildSectorConfig(settings, x, z);
                        config.RiftCount = rifts.Length;
                        await BakeSectorAsync(settings, config, ridge, warp, recipes, rifts, state, blackBox);
                        completedWorkUnits++;
                        float p = completedWorkUnits * math.rcp((float)totalWorkUnits);
                        progress?.Invoke(p);
                        EditorUtility.DisplayProgressBar("Global Topography Forge", "Baking sector " + x + "," + z, p);
                        await Awaitable.NextFrameAsync();
                    }
                }

                if (!_cancelRequested)
                {
                    await BakeMacroHeightmapAsync(settings, ridge, warp, recipes, rifts, state, blackBox);
                    completedWorkUnits++;
                    progress?.Invoke(completedWorkUnits * math.rcp((float)totalWorkUnits));
                }
            }
            finally
            {
                ReleaseTopographyArray(ref recipes);
                ReleaseTopographyArray(ref rifts);
            }

            return SnapshotMetrics(state);
        }

        private static async Awaitable BakeSectorAsync(
            TopographyBakeSettings settings,
            TopographyBakeConfigDTO config,
            FractalParamsDTO ridge,
            DomainWarpParamsDTO warp,
            NativeArray<TopographyBiomeKernelDTO> recipes,
            NativeArray<TectonicRiftSegmentDTO> rifts,
            NativeArray<TopographyBakeRunStateDTO> state,
            NativeArray<TopographyBakeTelemetryEntry> blackBox)
        {
            int cellCount = config.Width * config.Height;
            NativeArray<double2> warped = default;
            NativeArray<float> raw = default;
            NativeArray<float> terraced = default;
            NativeArray<float> final = default;
            NativeArray<float4> biomeMask = default;
            try
            {
                RecordTelemetry(blackBox, state, config, 1u, 0f, config.HeightMinMeters, config.HeightMaxMeters, 0u);
                warped = AllocateTopographyArray<double2>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                raw = AllocateTopographyArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                terraced = AllocateTopographyArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                final = AllocateTopographyArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                biomeMask = AllocateTopographyArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                int batchCount = ResolveBatchCount(cellCount, settings.GlobalQualityWeight);
                Stopwatch stage = Stopwatch.StartNew();
                JobHandle biomeMaskHandle = new GenerateBiomeMaskJob
                {
                    BiomeMaskWeights = biomeMask,
                    Recipes = recipes,
                    Config = config
                }.Schedule(cellCount, batchCount);
                JobHandle warpHandle = new ApplyDomainWarpingJob
                {
                    WarpedAupXZ = warped,
                    Recipes = recipes,
                    Config = config,
                    Warp = warp
                }.Schedule(cellCount, batchCount);
                JobHandle ridgeHandle = new EvaluateMountainRidgesJob
                {
                    WarpedAupXZ = warped,
                    Recipes = recipes,
                    HeightsMeters = raw,
                    Config = config,
                    Ridge = ridge
                }.Schedule(cellCount, batchCount, warpHandle);
                JobHandle terraceHandle = new ApplyStrataTerracingJob
                {
                    InputHeightsMeters = raw,
                    OutputHeightsMeters = terraced,
                    Config = config
                }.Schedule(cellCount, batchCount, ridgeHandle);
                JobHandle riftHandle = new ApplyTectonicRiftsJob
                {
                    InputHeightsMeters = terraced,
                    Rifts = rifts,
                    OutputHeightsMeters = final,
                    Config = config
                }.Schedule(cellCount, batchCount, terraceHandle);
                JobHandle.CombineDependencies(riftHandle, biomeMaskHandle).Complete();
                stage.Stop();
                AddPipelineMilliseconds(state, stage.Elapsed.TotalMilliseconds);

                uint heightWarnings;
                float minHeight;
                float maxHeight;
                uint checksum;
                unsafe
                {
                    heightWarnings = AnalyzeHeights(final, config, state, out minHeight, out maxHeight, out checksum);
                }
                uint biomeChecksum = AnalyzeBiomeMask(biomeMask, state, out uint biomeWarnings);
                uint recipeWarnings = ResolveBiomeMaskRecipeWarnings(recipes);
                AddWarningFlags(state, recipeWarnings);
                uint warningFlags = heightWarnings | biomeWarnings | recipeWarnings;
                RecordTelemetry(blackBox, state, config, 4u, (float)stage.Elapsed.TotalMilliseconds, minHeight, maxHeight, warningFlags);
                uint fatalWarnings = warningFlags & (TopographyForgeConstants.WarningNaNHeight | TopographyForgeConstants.WarningInvalidBiomeMask);
                if (fatalWarnings != 0u)
                {
                    unsafe
                    {
                        DumpBlackBox(blackBox, SnapshotBlackBoxCursor(state), fatalWarnings);
                    }
                }

                HeightmapFileHeaderDTO header = BuildHeader(config, minHeight, maxHeight, checksum, final.Length);
                BiomeMaskFileHeaderDTO biomeHeader = BuildBiomeMaskHeader(config, biomeChecksum, biomeMask.Length, recipes.IsCreated ? recipes.Length : 0);
                Stopwatch write = Stopwatch.StartNew();
                await WriteHeightmapAsync(BuildSectorPath(config.SectorX, config.SectorZ), header, final);
                await WriteBiomeMaskAsync(BuildSectorBiomeMaskPath(config.SectorX, config.SectorZ), biomeHeader, biomeMask);
                write.Stop();
                AddSerializationMilliseconds(state, write.Elapsed.TotalMilliseconds);
                IncrementCompletedSectors(state);
            }
            finally
            {
                ReleaseTopographyArray(ref warped);
                ReleaseTopographyArray(ref raw);
                ReleaseTopographyArray(ref terraced);
                ReleaseTopographyArray(ref final);
                ReleaseTopographyArray(ref biomeMask);
            }
        }

        private static async Awaitable BakeMacroHeightmapAsync(
            TopographyBakeSettings settings,
            FractalParamsDTO ridge,
            DomainWarpParamsDTO warp,
            NativeArray<TopographyBiomeKernelDTO> recipes,
            NativeArray<TectonicRiftSegmentDTO> rifts,
            NativeArray<TopographyBakeRunStateDTO> state,
            NativeArray<TopographyBakeTelemetryEntry> blackBox)
        {
            int resolution = math.max(64, settings.MacroResolution);
            int cellCount = resolution * resolution;
            NativeArray<float> macro = default;
            NativeArray<float4> macroBiomeMask = default;
            try
            {
                TopographyBakeConfigDTO config = BuildMacroConfig(settings, resolution, rifts.Length);
                RecordTelemetry(blackBox, state, config, 2u, 0f, config.HeightMinMeters, config.HeightMaxMeters, 0u);
                macro = AllocateTopographyArray<float>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                macroBiomeMask = AllocateTopographyArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Stopwatch stopwatch = Stopwatch.StartNew();
                double2 worldSize = new double2(settings.SectorCountX * settings.SectorSizeMeters, settings.SectorCountZ * settings.SectorSizeMeters);
                JobHandle macroHandle = new GenerateMacroHeightmapJob
                {
                    MacroHeightsMeters = macro,
                    Rifts = rifts,
                    Recipes = recipes,
                    Config = config,
                    Ridge = ridge,
                    Warp = warp,
                    WorldSizeMeters = worldSize
                }.Schedule(cellCount, ResolveBatchCount(cellCount, settings.GlobalQualityWeight));
                JobHandle maskHandle = new GenerateMacroBiomeMaskJob
                {
                    BiomeMaskWeights = macroBiomeMask,
                    Recipes = recipes,
                    Config = config,
                    WorldSizeMeters = worldSize
                }.Schedule(cellCount, ResolveBatchCount(cellCount, settings.GlobalQualityWeight));
                JobHandle.CombineDependencies(macroHandle, maskHandle).Complete();
                stopwatch.Stop();
                AddMacroMilliseconds(state, stopwatch.Elapsed.TotalMilliseconds);
                uint heightWarnings;
                float minHeight;
                float maxHeight;
                uint checksum;
                unsafe
                {
                    heightWarnings = AnalyzeHeights(macro, config, state, out minHeight, out maxHeight, out checksum);
                }
                uint biomeChecksum = AnalyzeBiomeMask(macroBiomeMask, state, out uint biomeWarnings);
                uint recipeWarnings = ResolveBiomeMaskRecipeWarnings(recipes);
                AddWarningFlags(state, recipeWarnings);
                uint warnings = heightWarnings | biomeWarnings | recipeWarnings;
                RecordTelemetry(blackBox, state, config, 5u, (float)stopwatch.Elapsed.TotalMilliseconds, minHeight, maxHeight, warnings);
                uint fatalWarnings = warnings & (TopographyForgeConstants.WarningNaNHeight | TopographyForgeConstants.WarningInvalidBiomeMask);
                if (fatalWarnings != 0u)
                {
                    unsafe
                    {
                        DumpBlackBox(blackBox, SnapshotBlackBoxCursor(state), fatalWarnings);
                    }
                }

                HeightmapFileHeaderDTO header = BuildHeader(config, minHeight, maxHeight, checksum, macro.Length);
                BiomeMaskFileHeaderDTO biomeHeader = BuildBiomeMaskHeader(config, biomeChecksum, macroBiomeMask.Length, recipes.IsCreated ? recipes.Length : 0);
                Stopwatch write = Stopwatch.StartNew();
                await WriteHeightmapAsync(TopographyForgeConstants.MacroOutputPath, header, macro);
                await WriteBiomeMaskAsync(TopographyForgeConstants.MacroBiomeMaskOutputPath, biomeHeader, macroBiomeMask);
                write.Stop();
                AddSerializationMilliseconds(state, write.Elapsed.TotalMilliseconds);
            }
            finally
            {
                ReleaseTopographyArray(ref macro);
                ReleaseTopographyArray(ref macroBiomeMask);
            }
        }

        private static TopographyBakeSettings SanitizeSettings(TopographyBakeSettings settings)
        {
            settings.SectorSizeMeters = FiniteOrDefault(settings.SectorSizeMeters, TopographyForgeConstants.DefaultSectorSizeMeters);
            settings.HeightMinMeters = FiniteOrDefault(settings.HeightMinMeters, -5200f);
            settings.HeightMaxMeters = FiniteOrDefault(settings.HeightMaxMeters, 1800f);
            settings.SeaFloorBiasMeters = FiniteOrDefault(settings.SeaFloorBiasMeters, -2000f);
            settings.RidgeFrequency = FiniteOrDefault(settings.RidgeFrequency, 0.00032f);
            settings.RidgeAmplitude = FiniteOrDefault(settings.RidgeAmplitude, 1f);
            settings.RidgeLacunarity = FiniteOrDefault(settings.RidgeLacunarity, 2.04f);
            settings.RidgePersistence = FiniteOrDefault(settings.RidgePersistence, 0.54f);
            settings.WarpFrequency = FiniteOrDefault(settings.WarpFrequency, 0.000086f);
            settings.WarpStrengthMeters = FiniteOrDefault(settings.WarpStrengthMeters, 860f);
            settings.TerraceSteps = FiniteOrDefault(settings.TerraceSteps, 18f);
            settings.TerraceStrength = FiniteOrDefault(settings.TerraceStrength, 0.28f);
            settings.RiftDepthMeters = FiniteOrDefault(settings.RiftDepthMeters, 5000f);
            settings.RiftWidthMeters = FiniteOrDefault(settings.RiftWidthMeters, 2200f);
            settings.GlobalQualityWeight = FiniteOrDefault(settings.GlobalQualityWeight, 1f);
            if (!math.all(math.isfinite(settings.WorldOriginAup)))
                settings.WorldOriginAup = double3.zero;

            if (settings.SectorSizeMeters <= 0f)
                settings.SectorSizeMeters = TopographyForgeConstants.DefaultSectorSizeMeters;
            settings.SectorSizeMeters = math.max(1f, settings.SectorSizeMeters);
            if (settings.SectorResolution <= 1)
                settings.SectorResolution = TopographyForgeConstants.DefaultSectorResolution;
            settings.SectorResolution = math.clamp(settings.SectorResolution, 16, 4096);
            if (settings.SectorCountX <= 0)
                settings.SectorCountX = TopographyForgeConstants.DefaultWorldSizeMeters / (int)settings.SectorSizeMeters;
            if (settings.SectorCountZ <= 0)
                settings.SectorCountZ = TopographyForgeConstants.DefaultWorldSizeMeters / (int)settings.SectorSizeMeters;
            settings.SectorCountX = math.clamp(settings.SectorCountX, 1, 512);
            settings.SectorCountZ = math.clamp(settings.SectorCountZ, 1, 512);
            if (settings.MacroResolution <= 0)
                settings.MacroResolution = TopographyForgeConstants.DefaultMacroResolution;
            settings.MacroResolution = math.clamp(settings.MacroResolution, 64, 4096);
            settings.RidgeFrequency = math.max(0.0000001f, settings.RidgeFrequency);
            settings.RidgeAmplitude = math.max(0f, settings.RidgeAmplitude);
            settings.RidgeLacunarity = math.max(1.0001f, settings.RidgeLacunarity);
            settings.RidgePersistence = math.saturate(settings.RidgePersistence);
            settings.RidgeOctaves = math.clamp(settings.RidgeOctaves, 1, 12);
            settings.WarpFrequency = math.max(0.0000001f, settings.WarpFrequency);
            settings.WarpStrengthMeters = math.max(0f, settings.WarpStrengthMeters);
            settings.TerraceSteps = math.max(1f, settings.TerraceSteps);
            settings.TerraceStrength = math.saturate(settings.TerraceStrength);
            settings.RiftDepthMeters = math.max(0f, settings.RiftDepthMeters);
            settings.RiftWidthMeters = math.max(1f, settings.RiftWidthMeters);
            settings.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            if (settings.WorldSeed == 0u)
                settings.WorldSeed = 0x53483234u;
            if (settings.HeightMaxMeters <= settings.HeightMinMeters)
            {
                settings.HeightMinMeters = -5200f;
                settings.HeightMaxMeters = 1800f;
            }

            return settings;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static TopographyBakeMetrics NewMetrics()
        {
            TopographyBakeMetrics metrics = default;
            metrics.MinHeightMeters = float.PositiveInfinity;
            metrics.MaxHeightMeters = float.NegativeInfinity;
            return metrics;
        }

        private static NativeArray<TopographyBakeRunStateDTO> NewRunState(Allocator allocator)
        {
            NativeArray<TopographyBakeRunStateDTO> state = AllocateTopographyArray<TopographyBakeRunStateDTO>(
                1,
                allocator,
                NativeArrayOptions.UninitializedMemory);
            ResetState(state);
            return state;
        }

        private static NativeArray<T> AllocateTopographyArray<T>(int length, Allocator allocator, NativeArrayOptions options) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("Topography Forge native allocation failed.");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(
                    array,
                    NativeMemoryOwner,
                    typeof(T).Name,
                    ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException($"Native memory sentinel registration failed for {typeof(T).Name}.");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static void ReleaseTopographyArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
                array = default;
            }
        }

        private static NativeAllocationLifetime ResolveNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }

        private static unsafe ref TopographyBakeRunStateDTO RunStateRef(NativeArray<TopographyBakeRunStateDTO> state)
        {
            return ref UnsafeUtility.AsRef<TopographyBakeRunStateDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(state));
        }

        private static void ResetState(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                ref TopographyBakeRunStateDTO run = ref RunStateRef(state);
                run = default;
                run.Metrics = NewMetrics();
            }
        }

        private static void ResetMetrics(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                ref TopographyBakeRunStateDTO run = ref RunStateRef(state);
                run.Metrics = NewMetrics();
            }
        }

        private static TopographyBakeMetrics SnapshotMetrics(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return NewMetrics();

            unsafe
            {
                return RunStateRef(state).Metrics;
            }
        }

        private static uint SnapshotBlackBoxCursor(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return 0u;

            unsafe
            {
                return RunStateRef(state).BlackBoxCursor;
            }
        }

        private static void SetSectorAndRecipeCounts(NativeArray<TopographyBakeRunStateDTO> state, int sectorCount, int recipeCount)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                ref TopographyBakeRunStateDTO run = ref RunStateRef(state);
                run.Metrics.SectorCount = sectorCount;
                run.Metrics.RecipeCount = recipeCount;
            }
        }

        private static void SetMockSectorCounts(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                ref TopographyBakeRunStateDTO run = ref RunStateRef(state);
                run.Metrics.CompletedSectors = 1;
                run.Metrics.SectorCount = 1;
            }
        }

        private static void AddMockSectorMilliseconds(NativeArray<TopographyBakeRunStateDTO> state, double milliseconds)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.MockSectorMilliseconds = milliseconds;
            }
        }

        private static void AddPipelineMilliseconds(NativeArray<TopographyBakeRunStateDTO> state, double milliseconds)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.PipelineMilliseconds += milliseconds;
            }
        }

        private static void AddMacroMilliseconds(NativeArray<TopographyBakeRunStateDTO> state, double milliseconds)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.MacroMilliseconds += milliseconds;
            }
        }

        private static void AddSerializationMilliseconds(NativeArray<TopographyBakeRunStateDTO> state, double milliseconds)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.SerializationMilliseconds += milliseconds;
            }
        }

        private static void IncrementCompletedSectors(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.CompletedSectors++;
            }
        }

        private static void IncrementNanSectors(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.NaNSectors++;
            }
        }

        private static void AccumulateHeightAnalysis(NativeArray<TopographyBakeRunStateDTO> state, float minHeight, float maxHeight, uint warnings)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                ref TopographyBakeRunStateDTO run = ref RunStateRef(state);
                run.Metrics.MinHeightMeters = math.min(run.Metrics.MinHeightMeters, minHeight);
                run.Metrics.MaxHeightMeters = math.max(run.Metrics.MaxHeightMeters, maxHeight);
                run.Metrics.WarningFlags |= warnings;
            }
        }

        private static void AddWarningFlags(NativeArray<TopographyBakeRunStateDTO> state, uint warnings)
        {
            if (!state.IsCreated || state.Length == 0 || warnings == 0u)
                return;

            unsafe
            {
                RunStateRef(state).Metrics.WarningFlags |= warnings;
            }
        }

        private static void AdvanceBlackBoxCursor(NativeArray<TopographyBakeRunStateDTO> state)
        {
            if (!state.IsCreated || state.Length == 0)
                return;

            unsafe
            {
                RunStateRef(state).BlackBoxCursor++;
            }
        }

        private static FractalParamsDTO BuildDefaultRidge(TopographyBakeSettings settings)
        {
            FractalParamsDTO ridge = default;
            ridge.Frequency = settings.RidgeFrequency;
            ridge.Amplitude = settings.RidgeAmplitude;
            ridge.Lacunarity = settings.RidgeLacunarity;
            ridge.Persistence = settings.RidgePersistence;
            ridge.Octaves = settings.RidgeOctaves;
            ridge.SeedHash = settings.WorldSeed ^ 0x52494447u;
            return ridge;
        }

        private static DomainWarpParamsDTO BuildDefaultWarp(TopographyBakeSettings settings)
        {
            DomainWarpParamsDTO warp = default;
            warp.Frequency = settings.WarpFrequency;
            warp.StrengthMeters = settings.WarpStrengthMeters;
            warp.Lacunarity = 1.92f;
            warp.Persistence = 0.58f;
            warp.Octaves = 4;
            warp.SeedHash = settings.WorldSeed ^ 0x57415250u;
            return warp;
        }

        private static TopographyBakeConfigDTO BuildSectorConfig(TopographyBakeSettings settings, int sectorX, int sectorZ)
        {
            TopographyBakeConfigDTO config = default;
            double worldWidth = settings.SectorCountX * (double)settings.SectorSizeMeters;
            double worldDepth = settings.SectorCountZ * (double)settings.SectorSizeMeters;
            double startX = settings.WorldOriginAup.x - (worldWidth * 0.5);
            double startZ = settings.WorldOriginAup.z - (worldDepth * 0.5);
            config.SectorAup = new double3(
                startX + (sectorX * (double)settings.SectorSizeMeters),
                settings.WorldOriginAup.y,
                startZ + (sectorZ * (double)settings.SectorSizeMeters));
            config.PixelSizeMeters = settings.SectorSizeMeters / (double)(settings.SectorResolution - 1);
            config.Width = settings.SectorResolution;
            config.Height = settings.SectorResolution;
            config.HeightMinMeters = settings.HeightMinMeters;
            config.HeightMaxMeters = settings.HeightMaxMeters;
            config.SeaFloorBiasMeters = settings.SeaFloorBiasMeters;
            config.RidgeBlend = 1f;
            config.TerraceSteps = settings.TerraceSteps;
            config.TerraceStrength = settings.TerraceStrength;
            config.TerraceSlopeStart = 0.025f;
            config.TerraceSlopeEnd = 0.22f;
            config.RiftDepthMeters = settings.RiftDepthMeters;
            config.RiftWidthMeters = settings.RiftWidthMeters;
            config.WorldSeed = settings.WorldSeed;
            config.SectorX = sectorX;
            config.SectorZ = sectorZ;
            config.GlobalQualityWeight = 1f;
            config.HeightScaleMeters = 1f;
            config.Flags = TopographyForgeConstants.RollbackExcludedFlag;
            return config;
        }

        private static TopographyBakeConfigDTO BuildMacroConfig(TopographyBakeSettings settings, int resolution, int riftCount)
        {
            TopographyBakeConfigDTO config = BuildSectorConfig(settings, -1, -1);
            double worldWidth = settings.SectorCountX * (double)settings.SectorSizeMeters;
            double worldDepth = settings.SectorCountZ * (double)settings.SectorSizeMeters;
            config.SectorAup = new double3(
                settings.WorldOriginAup.x - (worldWidth * 0.5),
                settings.WorldOriginAup.y,
                settings.WorldOriginAup.z - (worldDepth * 0.5));
            config.Width = resolution;
            config.Height = resolution;
            config.PixelSizeMeters = worldWidth / math.max(1.0, resolution - 1.0);
            config.RiftCount = riftCount;
            return config;
        }

        private static NativeArray<TopographyBiomeKernelDTO> LoadKernelRecipes(out int recipeCount)
        {
            recipeCount = 0;
            TopographyBiomeRecipeStore recipeList = default;
            try
            {
                recipeList = TopographyBiomeCsv.LoadRecipes(Allocator.Temp);
                recipeCount = recipeList.Length;
                return CopyRecipes(recipeList);
            }
            finally
            {
                if (recipeList.IsCreated)
                    recipeList.Dispose();
            }
        }

        private static NativeArray<TopographyBiomeKernelDTO> CopyRecipes(TopographyBiomeRecipeStore recipes)
        {
            if (recipes.Length <= 0)
                return default;

            NativeArray<TopographyBiomeKernelDTO> output = AllocateTopographyArray<TopographyBiomeKernelDTO>(
                recipes.Length,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < recipes.Length; i++)
                output[i] = ToKernelRecipe(recipes[i]);
            return output;
        }

        private static NativeArray<TectonicRiftSegmentDTO> BuildDefaultRifts(TopographyBakeSettings settings)
        {
            NativeArray<TectonicRiftSegmentDTO> rifts = AllocateTopographyArray<TectonicRiftSegmentDTO>(4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            double worldWidth = settings.SectorCountX * (double)settings.SectorSizeMeters;
            double worldDepth = settings.SectorCountZ * (double)settings.SectorSizeMeters;
            double2 min = new double2(settings.WorldOriginAup.x - worldWidth * 0.5, settings.WorldOriginAup.z - worldDepth * 0.5);
            double2 max = new double2(settings.WorldOriginAup.x + worldWidth * 0.5, settings.WorldOriginAup.z + worldDepth * 0.5);
            rifts[0] = Rift(new double2(min.x + worldWidth * 0.08, min.y + worldDepth * 0.18), new double2(max.x - worldWidth * 0.12, max.y - worldDepth * 0.24), settings, 0xA2400001u);
            rifts[1] = Rift(new double2(min.x + worldWidth * 0.18, max.y - worldDepth * 0.16), new double2(max.x - worldWidth * 0.18, min.y + worldDepth * 0.22), settings, 0xA2400002u);
            rifts[2] = Rift(new double2(min.x + worldWidth * 0.46, min.y), new double2(min.x + worldWidth * 0.61, max.y), settings, 0xA2400003u);
            rifts[3] = Rift(new double2(min.x, min.y + worldDepth * 0.56), new double2(max.x, min.y + worldDepth * 0.43), settings, 0xA2400004u);
            return rifts;
        }

        private static TopographyBiomeKernelDTO ToKernelRecipe(TopographyBiomeRecipeDTO recipe)
        {
            TopographyBiomeKernelDTO kernel = default;
            kernel.CenterAupXZ = recipe.CenterAupXZ;
            kernel.RadiusMeters = math.max(1f, recipe.RadiusMeters);
            kernel.InvRadiusMeters = math.rcp(kernel.RadiusMeters);
            kernel.InvRadiusSqMeters = math.rcp(kernel.RadiusMeters * kernel.RadiusMeters);
            kernel.TerraceSteps = math.max(1f, recipe.TerraceSteps);
            kernel.TerraceStrength = math.saturate(recipe.TerraceStrength);
            kernel.RidgeBlend = math.saturate(recipe.RidgeBlend);
            kernel.RiftDepthMeters = math.max(0f, recipe.RiftDepthMeters);
            kernel.SeedHash = recipe.SeedHash;
            kernel.Ridge = recipe.Ridge;
            kernel.Warp = recipe.Warp;
            return kernel;
        }

        private static TectonicRiftSegmentDTO Rift(double2 start, double2 end, TopographyBakeSettings settings, uint seed)
        {
            TectonicRiftSegmentDTO rift = default;
            rift.StartAupXZ = start;
            rift.EndAupXZ = end;
            rift.WidthMeters = settings.RiftWidthMeters;
            rift.DepthMeters = settings.RiftDepthMeters;
            rift.EdgeSharpness = 1f;
            rift.FalloffPower = 2.35f;
            rift.SeedHash = seed ^ settings.WorldSeed;
            return rift;
        }

        private static int ResolveBatchCount(int cellCount, float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            int maxBatch = (int)math.round(math.lerp(32f, 128f, q));
            return math.max(1, math.min(maxBatch, math.max(1, cellCount / 64)));
        }

        private static unsafe uint AnalyzeHeights(
            NativeArray<float> heights,
            TopographyBakeConfigDTO config,
            NativeArray<TopographyBakeRunStateDTO> state,
            out float minHeight,
            out float maxHeight,
            out uint checksum)
        {
            minHeight = float.PositiveInfinity;
            maxHeight = float.NegativeInfinity;
            uint warnings = 0u;
            checksum = 2166136261u;
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(heights);
            byte* bytePtr = (byte*)ptr;
            int payloadBytes = heights.Length * UnsafeUtility.SizeOf<float>();
            bool sectorContainsNaN = false;
            for (int b = 0; b < payloadBytes; b++)
            {
                checksum ^= bytePtr[b];
                checksum *= 16777619u;
            }

            for (int i = 0; i < heights.Length; i++)
            {
                float h = ptr[i];
                if (!math.isfinite(h))
                {
                    warnings |= TopographyForgeConstants.WarningNaNHeight;
                    sectorContainsNaN = true;
                    continue;
                }

                minHeight = math.min(minHeight, h);
                maxHeight = math.max(maxHeight, h);
                if (h < config.HeightMinMeters || h > config.HeightMaxMeters)
                    warnings |= TopographyForgeConstants.WarningHeightClamped;
            }

            if (!math.isfinite(minHeight))
                minHeight = config.HeightMinMeters;
            if (!math.isfinite(maxHeight))
                maxHeight = config.HeightMinMeters;

            if (sectorContainsNaN)
                IncrementNanSectors(state);

            AccumulateHeightAnalysis(state, minHeight, maxHeight, warnings);
            return warnings;
        }

        private static unsafe uint AnalyzeBiomeMask(NativeArray<float4> mask, NativeArray<TopographyBakeRunStateDTO> state, out uint warnings)
        {
            uint checksum = 2166136261u;
            warnings = 0u;
            float4* ptr = (float4*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mask);
            byte* bytePtr = (byte*)ptr;
            int payloadBytes = mask.Length * UnsafeUtility.SizeOf<float4>();
            for (int b = 0; b < payloadBytes; b++)
            {
                checksum ^= bytePtr[b];
                checksum *= 16777619u;
            }

            for (int i = 0; i < mask.Length; i++)
            {
                float4 value = ptr[i];
                float sum = math.csum(value);
                if (!math.all(math.isfinite(value)) || math.any(value < new float4(-0.0001f)) || math.any(value > new float4(1.0001f)) || math.abs(sum - 1f) > 0.01f)
                    warnings |= TopographyForgeConstants.WarningInvalidBiomeMask;
            }

            AddWarningFlags(state, warnings);
            return checksum;
        }

        private static uint ResolveBiomeMaskRecipeWarnings(NativeArray<TopographyBiomeKernelDTO> recipes)
        {
            if (recipes.IsCreated && recipes.Length > TopographyForgeConstants.BiomeMaskChannels)
                return TopographyForgeConstants.WarningBiomeMaskRecipeOverflow;

            return 0u;
        }

        private static HeightmapFileHeaderDTO BuildHeader(TopographyBakeConfigDTO config, float minHeight, float maxHeight, uint checksum, int elementCount)
        {
            HeightmapFileHeaderDTO header = default;
            header.Magic = TopographyForgeConstants.HeightmapMagic;
            header.Version = TopographyForgeConstants.HeightmapVersion;
            header.HeaderBytes = TopographyForgeConstants.HeightmapHeaderBytes;
            header.Flags = config.Flags | TopographyForgeConstants.RollbackExcludedFlag;
            header.Width = config.Width;
            header.Height = config.Height;
            header.SectorX = config.SectorX;
            header.SectorZ = config.SectorZ;
            header.SectorAup = config.SectorAup;
            header.PixelSizeMeters = config.PixelSizeMeters;
            header.MinHeightMeters = minHeight;
            header.MaxHeightMeters = maxHeight;
            header.HeightMinContractMeters = config.HeightMinMeters;
            header.HeightMaxContractMeters = config.HeightMaxMeters;
            header.WorldSeed = config.WorldSeed;
            header.DataChecksum = checksum;
            header.PayloadBytes = (uint)(elementCount * UnsafeUtility.SizeOf<float>());
            header.ElementStrideBytes = (uint)UnsafeUtility.SizeOf<float>();
            header.EndianMarker = TopographyForgeConstants.HeightmapEndianMarker;
            header.SchemaHash = TopographyForgeConstants.HeightmapSchemaHash;
            return header;
        }

        private static BiomeMaskFileHeaderDTO BuildBiomeMaskHeader(TopographyBakeConfigDTO config, uint checksum, int elementCount, int recipeCount)
        {
            BiomeMaskFileHeaderDTO header = default;
            header.Magic = TopographyForgeConstants.BiomeMaskMagic;
            header.Version = TopographyForgeConstants.HeightmapVersion;
            header.HeaderBytes = TopographyForgeConstants.BiomeMaskHeaderBytes;
            header.Flags = config.Flags | TopographyForgeConstants.RollbackExcludedFlag;
            header.Width = config.Width;
            header.Height = config.Height;
            header.SectorX = config.SectorX;
            header.SectorZ = config.SectorZ;
            header.SectorAup = config.SectorAup;
            header.PixelSizeMeters = config.PixelSizeMeters;
            header.WorldSeed = config.WorldSeed;
            header.DataChecksum = checksum;
            header.PayloadBytes = (uint)(elementCount * UnsafeUtility.SizeOf<float4>());
            header.ElementStrideBytes = (uint)UnsafeUtility.SizeOf<float4>();
            header.ChannelCount = TopographyForgeConstants.BiomeMaskChannels;
            header.RecipeCount = (uint)math.clamp(recipeCount, 0, TopographyForgeConstants.BiomeMaskChannels);
            header.EndianMarker = TopographyForgeConstants.HeightmapEndianMarker;
            header.SchemaHash = TopographyForgeConstants.BiomeMaskSchemaHash;
            header.SemanticsHash = TopographyForgeConstants.BiomeMaskSemanticsHash;
            return header;
        }

        private static async Awaitable WriteHeightmapAsync(string assetPath, HeightmapFileHeaderDTO header, NativeArray<float> heights)
        {
            EnsureLittleEndianHost();
            string path = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string tempPath = path + TempOutputSuffix;
            string backupPath = path + BackupOutputSuffix;
            TryDeleteFile(tempPath);

            Exception failure = null;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                WriteHeightmapTempBlocking(tempPath, header, heights);
                if (!TopographyForgeSelfAudit.TryValidateHeightmapFile(tempPath, out string validationError))
                {
                    TryDeleteFile(tempPath);
                    throw new InvalidDataException(validationError);
                }

                PromoteTempFileWithBackup(tempPath, path, backupPath);

                if (!TopographyForgeSelfAudit.TryValidateHeightmapFile(path, out validationError))
                {
                    try
                    {
                        RestorePromotedFileFromBackup(path, backupPath);
                    }
                    catch (Exception restoreException)
                    {
                        throw new InvalidDataException(validationError, restoreException);
                    }

                    throw new InvalidDataException(validationError);
                }
                RetirePreviousBackup(backupPath);
            }
            catch (Exception ex)
            {
                failure = ex;
                TryDeleteFile(tempPath);
            }

            await Awaitable.MainThreadAsync();
            if (failure != null)
                throw failure;
        }

        private static async Awaitable WriteBiomeMaskAsync(string assetPath, BiomeMaskFileHeaderDTO header, NativeArray<float4> mask)
        {
            EnsureLittleEndianHost();
            string path = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string tempPath = path + TempOutputSuffix;
            string backupPath = path + BackupOutputSuffix;
            TryDeleteFile(tempPath);

            Exception failure = null;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                WriteBiomeMaskTempBlocking(tempPath, header, mask);
                if (!TopographyForgeSelfAudit.TryValidateBiomeMaskFile(tempPath, out string validationError))
                {
                    TryDeleteFile(tempPath);
                    throw new InvalidDataException(validationError);
                }

                PromoteTempFileWithBackup(tempPath, path, backupPath);

                if (!TopographyForgeSelfAudit.TryValidateBiomeMaskFile(path, out validationError))
                {
                    try
                    {
                        RestorePromotedFileFromBackup(path, backupPath);
                    }
                    catch (Exception restoreException)
                    {
                        throw new InvalidDataException(validationError, restoreException);
                    }

                    throw new InvalidDataException(validationError);
                }
                RetirePreviousBackup(backupPath);
            }
            catch (Exception ex)
            {
                failure = ex;
                TryDeleteFile(tempPath);
            }

            await Awaitable.MainThreadAsync();
            if (failure != null)
                throw failure;
        }

        private static void WriteHeightmapTempBlocking(string tempPath, HeightmapFileHeaderDTO header, NativeArray<float> heights)
        {
            byte[] headerBytes = null;
            byte[] chunk = null;
            try
            {
                headerBytes = ArrayPool<byte>.Shared.Rent(TopographyForgeConstants.HeightmapHeaderBytes);
                chunk = ArrayPool<byte>.Shared.Rent(AsyncWriteChunkBytes);
                unsafe
                {
                    CopyHeaderToBytes(header, headerBytes);
                }

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, AsyncWriteChunkBytes, FileOptions.WriteThrough))
                {
                    stream.Write(headerBytes, 0, TopographyForgeConstants.HeightmapHeaderBytes);
                    int remainingFloats = heights.Length;
                    int floatOffset = 0;
                    int chunkFloats = AsyncWriteChunkBytes / UnsafeUtility.SizeOf<float>();
                    while (remainingFloats > 0)
                    {
                        int count = math.min(chunkFloats, remainingFloats);
                        int bytes = count * UnsafeUtility.SizeOf<float>();
                        unsafe
                        {
                            CopyNativeFloatChunkToBytes(heights, floatOffset, count, chunk);
                        }
                        stream.Write(chunk, 0, bytes);
                        floatOffset += count;
                        remainingFloats -= count;
                    }

                    stream.Flush(true);
                }
            }
            finally
            {
                if (chunk != null)
                    ArrayPool<byte>.Shared.Return(chunk);
                if (headerBytes != null)
                    ArrayPool<byte>.Shared.Return(headerBytes);
            }
        }

        private static void WriteBiomeMaskTempBlocking(string tempPath, BiomeMaskFileHeaderDTO header, NativeArray<float4> mask)
        {
            byte[] headerBytes = null;
            byte[] chunk = null;
            try
            {
                headerBytes = ArrayPool<byte>.Shared.Rent(TopographyForgeConstants.BiomeMaskHeaderBytes);
                chunk = ArrayPool<byte>.Shared.Rent(AsyncWriteChunkBytes);
                unsafe
                {
                    CopyBiomeMaskHeaderToBytes(header, headerBytes);
                }

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, AsyncWriteChunkBytes, FileOptions.WriteThrough))
                {
                    stream.Write(headerBytes, 0, TopographyForgeConstants.BiomeMaskHeaderBytes);
                    int remaining = mask.Length;
                    int offset = 0;
                    int chunkElements = AsyncWriteChunkBytes / UnsafeUtility.SizeOf<float4>();
                    while (remaining > 0)
                    {
                        int count = math.min(chunkElements, remaining);
                        int bytes = count * UnsafeUtility.SizeOf<float4>();
                        unsafe
                        {
                            CopyNativeFloat4ChunkToBytes(mask, offset, count, chunk);
                        }
                        stream.Write(chunk, 0, bytes);
                        offset += count;
                        remaining -= count;
                    }

                    stream.Flush(true);
                }
            }
            finally
            {
                if (chunk != null)
                    ArrayPool<byte>.Shared.Return(chunk);
                if (headerBytes != null)
                    ArrayPool<byte>.Shared.Return(headerBytes);
            }
        }

        private static void EnsureLittleEndianHost()
        {
            if (!BitConverter.IsLittleEndian)
                throw new InvalidDataException("Topography h8bin writer requires little-endian host byte order.");
        }

        private static unsafe void CopyHeaderToBytes(HeightmapFileHeaderDTO header, byte[] headerBytes)
        {
            fixed (byte* headerDst = headerBytes)
            {
                HeightmapFileHeaderDTO* headerPtr = &header;
                UnsafeUtility.MemCpy(headerDst, headerPtr, TopographyForgeConstants.HeightmapHeaderBytes);
            }
        }

        private static unsafe void CopyBiomeMaskHeaderToBytes(BiomeMaskFileHeaderDTO header, byte[] headerBytes)
        {
            fixed (byte* headerDst = headerBytes)
            {
                BiomeMaskFileHeaderDTO* headerPtr = &header;
                UnsafeUtility.MemCpy(headerDst, headerPtr, TopographyForgeConstants.BiomeMaskHeaderBytes);
            }
        }

        private static unsafe void CopyNativeFloatChunkToBytes(NativeArray<float> heights, int floatOffset, int count, byte[] chunk)
        {
            float* source = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(heights);
            fixed (byte* chunkPtr = chunk)
                UnsafeUtility.MemCpy(chunkPtr, source + floatOffset, count * UnsafeUtility.SizeOf<float>());
        }

        private static unsafe void CopyNativeFloat4ChunkToBytes(NativeArray<float4> mask, int elementOffset, int count, byte[] chunk)
        {
            float4* source = (float4*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mask);
            fixed (byte* chunkPtr = chunk)
                UnsafeUtility.MemCpy(chunkPtr, source + elementOffset, count * UnsafeUtility.SizeOf<float4>());
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void PromoteTempFileWithBackup(string tempPath, string path, string backupPath)
        {
            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            string previousBackupPath = backupPath + ".prev";
            TryDeleteFile(previousBackupPath);
            bool movedPreviousBackup = false;
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, previousBackupPath);
                movedPreviousBackup = true;
            }

            try
            {
                File.Replace(tempPath, path, backupPath, true);
            }
            catch
            {
                TryRestorePreviousBackup(backupPath, previousBackupPath, movedPreviousBackup);
                throw;
            }
        }

        private static void RetirePreviousBackup(string backupPath)
        {
            TryDeleteFile(backupPath + ".prev");
        }

        private static void RestorePromotedFileFromBackup(string path, string backupPath)
        {
            string failedPath = PrepareFailedArtifactPath(path + ".failed");
            try
            {
                if (File.Exists(backupPath))
                {
                    RestoreBackupToActivePath(path, backupPath, failedPath);
                    TryRestorePreviousBackup(backupPath, backupPath + ".prev", true);
                    return;
                }

                if (File.Exists(path))
                {
                    if (failedPath != null)
                        File.Move(path, failedPath);
                    else
                        File.Delete(path);
                }
            }
            catch (IOException ex)
            {
                throw new IOException("Topography promoted artifact restore failed for active path: " + path, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("Topography promoted artifact restore denied for active path: " + path, ex);
            }
        }

        private static void RestoreBackupToActivePath(string path, string backupPath, string failedPath)
        {
            if (!File.Exists(path))
            {
                File.Move(backupPath, path);
                return;
            }

            if (failedPath != null)
            {
                try
                {
                    File.Replace(backupPath, path, failedPath, true);
                    return;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            File.Delete(path);
            File.Move(backupPath, path);
        }

        private static string PrepareFailedArtifactPath(string failedPath)
        {
            string previousFailedPath = failedPath + ".prev";
            TryDeleteFile(previousFailedPath);
            if (File.Exists(failedPath))
            {
                try
                {
                    File.Move(failedPath, previousFailedPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return File.Exists(failedPath) ? null : failedPath;
        }

        private static void TryRestorePreviousBackup(string backupPath, string previousBackupPath, bool movedPreviousBackup)
        {
            if (!movedPreviousBackup || File.Exists(backupPath) || !File.Exists(previousBackupPath))
                return;

            try
            {
                File.Move(previousBackupPath, backupPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string BuildSectorPath(int sectorX, int sectorZ)
        {
            return Path.Combine(
                TopographyForgeConstants.SectorOutputFolder,
                "terrain_sx_" + sectorX.ToString("D3", CultureInfo.InvariantCulture) + "_sz_" + sectorZ.ToString("D3", CultureInfo.InvariantCulture) + ".h8bin");
        }

        private static string BuildSectorBiomeMaskPath(int sectorX, int sectorZ)
        {
            return Path.Combine(
                TopographyForgeConstants.SectorOutputFolder,
                "terrain_sx_" + sectorX.ToString("D3", CultureInfo.InvariantCulture) + "_sz_" + sectorZ.ToString("D3", CultureInfo.InvariantCulture) + "_biome_mask.h8bin");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(TopographyForgeConstants.SectorOutputFolder);
            Directory.CreateDirectory("Docs/Reports");
            Directory.CreateDirectory("Docs/AgentLogs");
        }

        private static void RecordTelemetry(
            NativeArray<TopographyBakeTelemetryEntry> blackBox,
            NativeArray<TopographyBakeRunStateDTO> state,
            TopographyBakeConfigDTO config,
            uint stage,
            float milliseconds,
            float minHeight,
            float maxHeight,
            uint warningFlags)
        {
            if (!blackBox.IsCreated || blackBox.Length == 0)
                return;

            uint cursor = SnapshotBlackBoxCursor(state);
            int index = (int)(cursor % TopographyForgeConstants.BlackBoxFrameCount);
            TopographyBakeTelemetryEntry entry = default;
            entry.SectorAup = config.SectorAup;
            entry.Frame = cursor;
            entry.Stage = stage;
            entry.MinHeightMeters = minHeight;
            entry.MaxHeightMeters = maxHeight;
            entry.StageMilliseconds = milliseconds;
            entry.SectorX = config.SectorX;
            entry.SectorZ = config.SectorZ;
            entry.WarningFlags = warningFlags;
            entry.StateHash = TopographyNoiseMath.HashAup(config.SectorAup, config.WorldSeed) ^ warningFlags ^ stage;
            entry.DumpReason = 0u;
            blackBox[index] = entry;
            AdvanceBlackBoxCursor(state);
        }

        private static void ClearBlackBox(NativeArray<TopographyBakeTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated)
                return;

            TopographyBakeTelemetryEntry empty = default;
            for (int i = 0; i < blackBox.Length; i++)
                blackBox[i] = empty;
        }

        internal static unsafe void DumpBlackBox(NativeArray<TopographyBakeTelemetryEntry> blackBox, uint cursor, uint reason)
        {
            if (!blackBox.IsCreated || blackBox.Length <= 0)
                return;

            EnsureFolders();
            TopographyBakeDumpHeader header = default;
            header.Magic = TopographyForgeConstants.DumpMagic;
            header.EntryCount = (uint)blackBox.Length;
            int entrySize = UnsafeUtility.SizeOf<TopographyBakeTelemetryEntry>();
            header.EntrySize = (uint)entrySize;
            header.Cursor = cursor;
            header.Reason = reason;
            int headerLength = UnsafeUtility.SizeOf<TopographyBakeDumpHeader>();
            int entryLength = blackBox.Length * entrySize;
            byte[] headerBytes = null;
            byte[] entryBytes = null;
            try
            {
                headerBytes = ArrayPool<byte>.Shared.Rent(headerLength); // COLD POOL: crash dump header scratch - owner: SHINOBU_240
                entryBytes = ArrayPool<byte>.Shared.Rent(entryLength); // COLD POOL: 300-frame crash dump scratch - owner: SHINOBU_240

                fixed (byte* headerDst = headerBytes)
                {
                    TopographyBakeDumpHeader* headerPtr = &header;
                    UnsafeUtility.MemCpy(headerDst, headerPtr, headerLength);
                }

                fixed (byte* entryDst = entryBytes)
                {
                    TopographyBakeTelemetryEntry* src = (TopographyBakeTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                    int count = blackBox.Length;
                    int start = cursor >= (uint)count ? (int)(cursor % (uint)count) : 0;
                    for (int i = 0; i < count; i++)
                    {
                        int sourceIndex = (start + i) % count;
                        UnsafeUtility.MemCpy(entryDst + (i * entrySize), src + sourceIndex, entrySize);
                    }
                }

                using (FileStream stream = new FileStream(TopographyForgeConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(headerBytes, 0, headerLength);
                    stream.Write(entryBytes, 0, entryLength);
                }
            }
            finally
            {
                if (headerBytes != null)
                    ArrayPool<byte>.Shared.Return(headerBytes);
                if (entryBytes != null)
                    ArrayPool<byte>.Shared.Return(entryBytes);
            }
        }

        internal static void WriteBakeReport(TopographyBakeMetrics metrics, TopographyBakeSettings settings, int recipeCount, string mode)
        {
            EnsureFolders();
            StringBuilder builder = new StringBuilder(2048); // COLD ALLOC: JSON report builder - owner: SHINOBU_240
            builder.AppendLine("{");
            AppendJson(builder, "agent", "SHINOBU_240", true);
            AppendJson(builder, "mode", mode, true);
            AppendJson(builder, "sector_count", metrics.SectorCount, true);
            AppendJson(builder, "completed_sectors", metrics.CompletedSectors, true);
            AppendJson(builder, "recipe_count", recipeCount, true);
            AppendJson(builder, "sector_resolution", settings.SectorResolution, true);
            AppendJson(builder, "macro_resolution", settings.MacroResolution, true);
            AppendJson(builder, "global_quality_weight", settings.GlobalQualityWeight, true);
            AppendJson(builder, "payload_math_quality_weight", 1f, true);
            AppendJson(builder, "quality_weight_affects_payload_truth", false, true);
            AppendJson(builder, "quality_weight_affects_scheduler", true, true);
            AppendJson(builder, "min_height_meters", metrics.MinHeightMeters, true);
            AppendJson(builder, "max_height_meters", metrics.MaxHeightMeters, true);
            AppendJson(builder, "nan_sectors", metrics.NaNSectors, true);
            AppendJson(builder, "warning_flags", metrics.WarningFlags, true);
            AppendJson(builder, "ridge_ms", metrics.RidgeMilliseconds, true);
            AppendJson(builder, "warp_ms", metrics.WarpMilliseconds, true);
            AppendJson(builder, "terrace_ms", metrics.TerraceMilliseconds, true);
            AppendJson(builder, "rift_ms", metrics.RiftMilliseconds, true);
            AppendJson(builder, "pipeline_ms", metrics.PipelineMilliseconds, true);
            AppendJson(builder, "stage_timing_note", "Sector jobs are scheduled as one dependency chain and completed once at terminal readback; per-stage fields are retained for legacy report schema and remain zero unless a dedicated profiler pass is added.", true);
            AppendJson(builder, "serialization_ms", metrics.SerializationMilliseconds, true);
            AppendJson(builder, "macro_ms", metrics.MacroMilliseconds, true);
            AppendJson(builder, "mock_sector_ms", metrics.MockSectorMilliseconds, true);
            AppendJson(builder, "biome_mask_channels", TopographyForgeConstants.BiomeMaskChannels, true);
            AppendJson(builder, "biome_mask_payload", mode != "mock_sector", true);
            AppendJson(builder, "biome_mask_invalid", (metrics.WarningFlags & TopographyForgeConstants.WarningInvalidBiomeMask) != 0u, true);
            AppendJson(builder, "biome_mask_recipe_overflow", (metrics.WarningFlags & TopographyForgeConstants.WarningBiomeMaskRecipeOverflow) != 0u, true);
            AppendJson(builder, "rollback_excluded", true, true);
            AppendJson(builder, "critical_warning", (metrics.WarningFlags & (TopographyForgeConstants.WarningNaNHeight | TopographyForgeConstants.WarningInvalidBiomeMask)) != 0u, false);
            builder.AppendLine("}");
            File.WriteAllText(TopographyForgeConstants.BakeReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(value).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, uint value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, float value, bool comma)
        {
            float finite = math.isfinite(value) ? value : 0f;
            builder.Append("  \"").Append(name).Append("\": ").Append(finite.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, double value, bool comma)
        {
            double finite = double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
            builder.Append("  \"").Append(name).Append("\": ").Append(finite.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : string.Empty);
        }

    }
}
#endif

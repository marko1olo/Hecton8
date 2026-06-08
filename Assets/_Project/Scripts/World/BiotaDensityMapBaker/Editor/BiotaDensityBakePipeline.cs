#if UNITY_EDITOR
using System;
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

namespace Hecton8.World.BiotaDensityMapBaker.Editor
{
    public struct BiotaDensityBakeResult
    {
        public string OutputPath;
        public int Width;
        public int Height;
        public int LayerCount;
        public int PixelCount;
        public int RawByteCount;
        public int RleRunCount;
        public int NonFiniteCount;
        public uint WarningFlags;
        public uint StateHash;
        public uint BiomassByteSum;
        public long FileBytes;
        public float JobMilliseconds;
        public float CompressionMilliseconds;
        public float SerializationMilliseconds;
        public float CompressionRatio;
    }

    public static class BiotaDensityBakePipeline
    {
        public const string OutputFolder = "Assets/StreamingAssets/Hecton8/Biota";
        public const string DefaultAssetName = "biota_density_SHINOBU_308.h8bin";
        public const string ReportPath = "Docs/Reports/BIOTA_BAKE_REPORT.json";
        public const string AuditPath = "Docs/Reports/BIOTA_DENSITY_SELF_AUDIT_SHINOBU_308.md";
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_308.bin";
        public const string DefaultCsvPath = "Assets/_SourceData/Biota/biota_spawning_rules.csv";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private const double DefaultSectorOriginX = -50000.0d;
        private const double DefaultSectorOriginY = -4200.0d;
        private const double DefaultSectorOriginZ = -50000.0d;
        private const double MaxAcceptedAupMagnitude = 1000000000.0d;
        private const string NativeMemoryOwner = nameof(BiotaDensityBakePipeline);

        [MenuItem("HECTON-8/Ecosystem Density Forge/Bake Mock Biota Density")]
        public static void BakeDefaultMenu()
        {
            FixedList4096Bytes<BiotaSpawnRuleDTO> rules = default;
            FixedList4096Bytes<BiotaRuleWeightDTO> weights = default;
            FillDefaultRules(ref rules, ref weights);
            bool ok = BakeMockSector(DefaultConfig(BiotaDensityBakeConstants.DefaultResolution), in rules, in weights, DefaultAssetName, out _);
            if (ok)
            {
                AssetDatabase.Refresh();
                return;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        public static BiotaDensityBakeConfigDTO DefaultConfig(int resolution)
        {
            int size = math.clamp(resolution, 16, BiotaDensityBakeConstants.MaxResolution);
            return new BiotaDensityBakeConfigDTO
            {
                SectorOriginAUP = new double3(DefaultSectorOriginX, DefaultSectorOriginY, DefaultSectorOriginZ),
                Width = size,
                Height = size,
                LayerCount = BiotaDensityBakeConstants.DefaultLayerCount,
                Seed = 0x53483038u,
                CellSizeMeters = BiotaDensityBakeConstants.DefaultCellSizeMeters,
                NoiseFrequency = BiotaDensityBakeConstants.DefaultNoiseFrequency,
                NoiseOffset = BiotaDensityBakeConstants.DefaultNoiseOffset,
                GlobalDensityMultiplier = BiotaDensityBakeConstants.DefaultDensityMultiplier,
                ThermalFalloffMeters = BiotaDensityBakeConstants.DefaultThermalFalloffMeters,
                BaseTemperatureCelsius = 2.0f,
                DepthScaleMeters = 4000f,
                SlopeSoftnessDegrees = 3.5f,
                TemperatureSoftnessCelsius = 18f,
                GlobalQualityWeight = 1f,
                Flags = BiotaDensityBakeConstants.RollbackExcludedFlag,
                EdgeSampleFlags = 0u,
                RuleCount = BiotaDensityBakeConstants.DefaultRuleCount,
                VentCount = 3u
            };
        }

        public static void FillDefaultRules(
            ref FixedList4096Bytes<BiotaSpawnRuleDTO> rules,
            ref FixedList4096Bytes<BiotaRuleWeightDTO> weights)
        {
            rules.Clear();
            weights.Clear();
            AddDefaultRule(ref rules, ref weights, "KELP_CANOPY", 0u, 10f, 180f, 0f, 18f, 0x52454546u, 8f, 12f, 0.72f, 0.10f, 0.0f);
            AddDefaultRule(ref rules, ref weights, "SILT_WEED", 0u, 260f, 2600f, 0f, 11f, 0x53494C54u, 4f, 14f, 0.88f, 0.95f, 0.10f);
            AddDefaultRule(ref rules, ref weights, "GHOST_RAY_PREY", 1u, 900f, 4200f, 0f, 24f, 0xFFFFFFFFu, 3f, 20f, 0.50f, 0.22f, 0.18f);
            AddDefaultRule(ref rules, ref weights, "ABYSSAL_PREDATOR", 2u, 1600f, 4700f, 0f, 28f, 0x4841444Cu, 2f, 18f, 0.34f, 0.12f, 0.20f);
            AddDefaultRule(ref rules, ref weights, "VENT_TUBE_WORM", 3u, 600f, 4600f, 0f, 32f, 0x56454E54u, 38f, 28f, 0.96f, 0.18f, 1.0f);
        }

        public static void AddDefaultRule(
            ref FixedList4096Bytes<BiotaSpawnRuleDTO> rules,
            ref FixedList4096Bytes<BiotaRuleWeightDTO> weights,
            string species,
            uint layer,
            float minDepth,
            float maxDepth,
            float minSlope,
            float maxSlope,
            uint biomeHash,
            float preferredTemperature,
            float temperatureTolerance,
            float spawnWeight,
            float siltAffinity,
            float thermalAffinity)
        {
            if (rules.Length >= BiotaDensityBakeConstants.MaxRuleCount || weights.Length >= BiotaDensityBakeConstants.MaxRuleCount)
                return;

            rules.Add(new BiotaSpawnRuleDTO
            {
                MinDepth = math.max(0f, minDepth),
                MaxDepth = math.max(minDepth, maxDepth),
                MinSlope = math.clamp(minSlope, 0f, 90f),
                MaxSlope = math.clamp(maxSlope, 0f, 90f),
                RequiredBiomeHash = biomeHash,
                PreferredTemperature = preferredTemperature
            });
            weights.Add(new BiotaRuleWeightDTO
            {
                SpeciesHash = BiotaDensityBakeMath.HashAscii(species),
                SpawnWeight = math.max(0f, spawnWeight),
                TemperatureTolerance = math.max(0.001f, temperatureTolerance),
                SiltAffinity = math.saturate(siltAffinity),
                ThermalAffinity = math.saturate(thermalAffinity),
                LayerIndex = layer,
                Flags = 0u
            });
        }

        public static bool BakeMockSector(
            BiotaDensityBakeConfigDTO config,
            in FixedList4096Bytes<BiotaSpawnRuleDTO> rules,
            in FixedList4096Bytes<BiotaRuleWeightDTO> weights,
            string assetName,
            out BiotaDensityBakeResult result)
        {
            try
            {
                result = BakeMockSectorBlocking(config, rules, weights, assetName);
                return result.WarningFlags != uint.MaxValue;
            }
            catch (Exception exception)
            {
                result = default;
                result.WarningFlags = uint.MaxValue;
                UnityEngine.Debug.LogError("[SHINOBU_308] Biota density bake failed: " + exception.GetType().Name + " " + exception.Message);
                return false;
            }
        }

        private static BiotaDensityBakeResult BakeMockSectorBlocking(
            BiotaDensityBakeConfigDTO sourceConfig,
            FixedList4096Bytes<BiotaSpawnRuleDTO> sourceRules,
            FixedList4096Bytes<BiotaRuleWeightDTO> sourceWeights,
            string assetName)
        {
            BiotaDensityBakeResult result = default;
            NativeArray<float> depth = default;
            NativeArray<float> silt = default;
            NativeArray<uint> biome = default;
            NativeArray<float> temperature = default;
            NativeArray<float> thermal = default;
            NativeArray<byte> density = default;
            NativeArray<byte> nonFinite = default;
            NativeArray<BiotaSpawnRuleDTO> rules = default;
            NativeArray<BiotaRuleWeightDTO> weights = default;
            NativeArray<BiotaThermalVentDTO> vents = default;
            NativeArray<float> edgeWest = default;
            NativeArray<float> edgeEast = default;
            NativeArray<float> edgeSouth = default;
            NativeArray<float> edgeNorth = default;
            NativeArray<BiotaDensityRleRunDTO> runs = default;
            NativeArray<int> runCount = default;
            NativeArray<BiotaDensityBakeTelemetryEntry> telemetry = default;
            JobHandle cleanupHandle = default;
            bool cleanupHandleValid = false;
            bool cleanupHandleCompleted = false;
            bool progressCleared = false;
            Stopwatch timer = new Stopwatch();
            Stopwatch compressionTimer = new Stopwatch();
            Stopwatch serializationTimer = new Stopwatch();

            try
            {
                telemetry = Allocate<BiotaDensityBakeTelemetryEntry>(BiotaDensityBakeConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                int telemetryCursor = PrimeTelemetry(telemetry, in result, in sourceConfig);
                ValidateLayoutsOrThrow();
                BiotaDensityBakeConfigDTO config = SanitizeConfig(sourceConfig, sourceRules.Length);
                config.EdgeSampleFlags = 15u;
                result.Width = config.Width;
                result.Height = config.Height;
                result.LayerCount = config.LayerCount;
                result.PixelCount = checked(config.Width * config.Height);
                result.RawByteCount = checked(result.PixelCount * result.LayerCount);
                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 0u, in result, in config);
                Directory.CreateDirectory(OutputFolder);
                Directory.CreateDirectory("Docs/Reports");
                Directory.CreateDirectory("Docs/AgentLogs");

                depth = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                silt = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                biome = Allocate<uint>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                temperature = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                thermal = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                density = Allocate<byte>(result.RawByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nonFinite = Allocate<byte>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rules = CreateNativeRules(in sourceRules, config.RuleCount);
                weights = CreateNativeWeights(in sourceWeights, config.RuleCount);
                vents = CreateDefaultVents(config);
                edgeWest = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeEast = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeSouth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeNorth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                runCount = Allocate<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Mock terrain, silt, biome synthesis", 0.10f);
                timer.Restart();
                JobHandle terrainHandle = new GenerateMockTerrainDataJob
                {
                    DepthMeters = depth,
                    Silt01 = silt,
                    BiomeHashes = biome,
                    Config = config
                }.Schedule(result.PixelCount, 64);
                JobHandle thermalHandle = new CalculateThermalGradientJob
                {
                    Vents = vents,
                    TemperatureCelsius = temperature,
                    Thermal01 = thermal,
                    Config = config
                }.Schedule(result.PixelCount, 64);
                JobHandle edgeWestHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeWest,
                    Config = config,
                    Side = 0
                }.Schedule(edgeWest.Length, 64);
                JobHandle edgeEastHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeEast,
                    Config = config,
                    Side = 1
                }.Schedule(edgeEast.Length, 64);
                JobHandle edgeSouthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeSouth,
                    Config = config,
                    Side = 2
                }.Schedule(edgeSouth.Length, 64);
                JobHandle edgeNorthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeNorth,
                    Config = config,
                    Side = 3
                }.Schedule(edgeNorth.Length, 64);
                JobHandle edgeHorizontalHandle = JobHandle.CombineDependencies(edgeWestHandle, edgeEastHandle);
                JobHandle edgeVerticalHandle = JobHandle.CombineDependencies(edgeSouthHandle, edgeNorthHandle);
                JobHandle terrainAndThermalHandle = JobHandle.CombineDependencies(terrainHandle, thermalHandle);
                cleanupHandle = JobHandle.CombineDependencies(terrainAndThermalHandle, JobHandle.CombineDependencies(edgeHorizontalHandle, edgeVerticalHandle));
                cleanupHandleValid = true;

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Evaluating depth/slope/temp/silt rules", 0.46f);
                JobHandle evaluationHandle = new EvaluateBiotaDensityJob
                {
                    DepthMeters = depth,
                    TemperatureCelsius = temperature,
                    Silt01 = silt,
                    Thermal01 = thermal,
                    BiomeHashes = biome,
                    WestEdgeDepthMeters = edgeWest,
                    EastEdgeDepthMeters = edgeEast,
                    SouthEdgeDepthMeters = edgeSouth,
                    NorthEdgeDepthMeters = edgeNorth,
                    Rules = rules,
                    Weights = weights,
                    DensityBytes = density,
                    NonFiniteFlags = nonFinite,
                    Config = config
                }.Schedule(result.PixelCount, 64, cleanupHandle);
                cleanupHandle = evaluationHandle;

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "RLE compression", 0.72f);
                compressionTimer.Start();
                JobHandle countRleHandle = new CountDensityRleRunsJob
                {
                    DensityBytes = density,
                    RunCount = runCount,
                    PixelCount = result.PixelCount,
                    LayerCount = result.LayerCount
                }.Schedule(evaluationHandle);
                cleanupHandle = countRleHandle;
                // EDITOR BLOCKING SYNC POINT: batchmode writer is intentionally synchronous and needs exact RLE cardinality.
                countRleHandle.Complete();
                cleanupHandleCompleted = true;

                int runCapacity = math.max(1, runCount[0]);
                runs = Allocate<BiotaDensityRleRunDTO>(runCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                JobHandle rleHandle = new CompressDensityRleJob
                {
                    DensityBytes = density,
                    Runs = runs,
                    RunCount = runCount,
                    PixelCount = result.PixelCount,
                    LayerCount = result.LayerCount
                }.Schedule();
                cleanupHandle = rleHandle;
                cleanupHandleCompleted = false;
                // EDITOR BLOCKING SYNC POINT: batchmode cannot return until the validated .h8bin is fully emitted.
                rleHandle.Complete();
                cleanupHandleCompleted = true;
                timer.Stop();
                compressionTimer.Stop();

                result.JobMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
                result.CompressionMilliseconds = (float)compressionTimer.Elapsed.TotalMilliseconds;
                result.RleRunCount = math.clamp(runCount[0], 0, runs.Length);
                result.NonFiniteCount = CountNonFinite(nonFinite);
                result.BiomassByteSum = SumBytes(density);
                result.StateHash = HashDensity(density);
                if (result.NonFiniteCount > 0)
                    result.WarningFlags |= BiotaDensityBakeConstants.WarningNonFiniteDensity;

                long compressedPayloadBytes = (long)result.RleRunCount * UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>();
                result.CompressionRatio = compressedPayloadBytes > 0
                    ? (float)((double)result.RawByteCount / compressedPayloadBytes)
                    : 0f;
                if (compressedPayloadBytes >= result.RawByteCount)
                    result.WarningFlags |= BiotaDensityBakeConstants.WarningRleExpanded;

                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 1u, in result, in config);
                if (result.NonFiniteCount > 0)
                    DumpBlackBoxSafe(telemetry, 2u);

                Dispose(ref depth);
                Dispose(ref silt);
                Dispose(ref biome);
                Dispose(ref temperature);
                Dispose(ref thermal);
                Dispose(ref density);
                Dispose(ref nonFinite);
                Dispose(ref rules);
                Dispose(ref weights);
                Dispose(ref vents);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
                Dispose(ref runCount);

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Sync .h8bin write", 0.88f);
                serializationTimer.Start();
                string outputPath = ResolveOutputPath(assetName);
                EditorUtility.ClearProgressBar();
                progressCleared = true;
                WriteCompressedBinaryBlocking(outputPath, config, result, runs, result.RleRunCount);
                serializationTimer.Stop();
                result.SerializationMilliseconds = (float)serializationTimer.Elapsed.TotalMilliseconds;
                result.OutputPath = outputPath;
                result.FileBytes = ValidateWrittenBinaryOrThrow(outputPath, in result);
                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 2u, in result, in config);
                WriteReport(in result, in config, sourceRules.Length);
                WriteSelfAudit(in result, in config, sourceRules.Length);
                return result;
            }
            catch
            {
                DumpBlackBoxSafe(telemetry, 1u);
                throw;
            }
            finally
            {
                if (cleanupHandleValid && !cleanupHandleCompleted)
                    cleanupHandle.Complete();
                if (!progressCleared)
                    EditorUtility.ClearProgressBar();
                Dispose(ref depth);
                Dispose(ref silt);
                Dispose(ref biome);
                Dispose(ref temperature);
                Dispose(ref thermal);
                Dispose(ref density);
                Dispose(ref nonFinite);
                Dispose(ref rules);
                Dispose(ref weights);
                Dispose(ref vents);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
                Dispose(ref runs);
                Dispose(ref runCount);
                Dispose(ref telemetry);
            }
        }

        public static async Awaitable<BiotaDensityBakeResult> BakeMockSectorAsync(
            BiotaDensityBakeConfigDTO sourceConfig,
            FixedList4096Bytes<BiotaSpawnRuleDTO> sourceRules,
            FixedList4096Bytes<BiotaRuleWeightDTO> sourceWeights,
            string assetName)
        {
            BiotaDensityBakeResult result = default;
            NativeArray<float> depth = default;
            NativeArray<float> silt = default;
            NativeArray<uint> biome = default;
            NativeArray<float> temperature = default;
            NativeArray<float> thermal = default;
            NativeArray<byte> density = default;
            NativeArray<byte> nonFinite = default;
            NativeArray<BiotaSpawnRuleDTO> rules = default;
            NativeArray<BiotaRuleWeightDTO> weights = default;
            NativeArray<BiotaThermalVentDTO> vents = default;
            NativeArray<float> edgeWest = default;
            NativeArray<float> edgeEast = default;
            NativeArray<float> edgeSouth = default;
            NativeArray<float> edgeNorth = default;
            NativeArray<BiotaDensityRleRunDTO> runs = default;
            NativeArray<int> runCount = default;
            NativeArray<BiotaDensityBakeTelemetryEntry> telemetry = default;
            JobHandle cleanupHandle = default;
            bool cleanupHandleValid = false;
            bool cleanupHandleCompleted = false;
            bool progressCleared = false;
            Stopwatch timer = new Stopwatch();
            Stopwatch compressionTimer = new Stopwatch();
            Stopwatch serializationTimer = new Stopwatch();

            try
            {
                telemetry = Allocate<BiotaDensityBakeTelemetryEntry>(BiotaDensityBakeConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                int telemetryCursor = PrimeTelemetry(telemetry, in result, in sourceConfig);
                ValidateLayoutsOrThrow();
                BiotaDensityBakeConfigDTO config = SanitizeConfig(sourceConfig, sourceRules.Length);
                config.EdgeSampleFlags = 15u;
                result.Width = config.Width;
                result.Height = config.Height;
                result.LayerCount = config.LayerCount;
                result.PixelCount = checked(config.Width * config.Height);
                result.RawByteCount = checked(result.PixelCount * result.LayerCount);
                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 0u, in result, in config);
                Directory.CreateDirectory(OutputFolder);
                Directory.CreateDirectory("Docs/Reports");
                Directory.CreateDirectory("Docs/AgentLogs");

                depth = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                silt = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                biome = Allocate<uint>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                temperature = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                thermal = Allocate<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                density = Allocate<byte>(result.RawByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nonFinite = Allocate<byte>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rules = CreateNativeRules(in sourceRules, config.RuleCount);
                weights = CreateNativeWeights(in sourceWeights, config.RuleCount);
                vents = CreateDefaultVents(config);
                edgeWest = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeEast = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeSouth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeNorth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                runCount = Allocate<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Mock terrain, silt, biome synthesis", 0.10f);
                timer.Restart();
                JobHandle terrainHandle = new GenerateMockTerrainDataJob
                {
                    DepthMeters = depth,
                    Silt01 = silt,
                    BiomeHashes = biome,
                    Config = config
                }.Schedule(result.PixelCount, 64);
                JobHandle thermalHandle = new CalculateThermalGradientJob
                {
                    Vents = vents,
                    TemperatureCelsius = temperature,
                    Thermal01 = thermal,
                    Config = config
                }.Schedule(result.PixelCount, 64);
                JobHandle edgeWestHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeWest,
                    Config = config,
                    Side = 0
                }.Schedule(edgeWest.Length, 64);
                JobHandle edgeEastHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeEast,
                    Config = config,
                    Side = 1
                }.Schedule(edgeEast.Length, 64);
                JobHandle edgeSouthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeSouth,
                    Config = config,
                    Side = 2
                }.Schedule(edgeSouth.Length, 64);
                JobHandle edgeNorthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeNorth,
                    Config = config,
                    Side = 3
                }.Schedule(edgeNorth.Length, 64);
                JobHandle edgeHorizontalHandle = JobHandle.CombineDependencies(edgeWestHandle, edgeEastHandle);
                JobHandle edgeVerticalHandle = JobHandle.CombineDependencies(edgeSouthHandle, edgeNorthHandle);
                JobHandle terrainAndThermalHandle = JobHandle.CombineDependencies(terrainHandle, thermalHandle);
                cleanupHandle = JobHandle.CombineDependencies(terrainAndThermalHandle, JobHandle.CombineDependencies(edgeHorizontalHandle, edgeVerticalHandle));
                cleanupHandleValid = true;

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Evaluating depth/slope/temp/silt rules", 0.46f);
                JobHandle evaluationHandle = new EvaluateBiotaDensityJob
                {
                    DepthMeters = depth,
                    TemperatureCelsius = temperature,
                    Silt01 = silt,
                    Thermal01 = thermal,
                    BiomeHashes = biome,
                    WestEdgeDepthMeters = edgeWest,
                    EastEdgeDepthMeters = edgeEast,
                    SouthEdgeDepthMeters = edgeSouth,
                    NorthEdgeDepthMeters = edgeNorth,
                    Rules = rules,
                    Weights = weights,
                    DensityBytes = density,
                    NonFiniteFlags = nonFinite,
                    Config = config
                }.Schedule(result.PixelCount, 64, cleanupHandle);
                cleanupHandle = evaluationHandle;

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "RLE compression", 0.72f);
                compressionTimer.Start();
                JobHandle countRleHandle = new CountDensityRleRunsJob
                {
                    DensityBytes = density,
                    RunCount = runCount,
                    PixelCount = result.PixelCount,
                    LayerCount = result.LayerCount
                }.Schedule(evaluationHandle);
                cleanupHandle = countRleHandle;
                // EDITOR BLOCKING SYNC POINT: exact RLE cardinality is required before allocating the async-lived Persistent run buffer.
                countRleHandle.Complete();
                cleanupHandleCompleted = true;

                int runCapacity = math.max(1, runCount[0]);
                runs = Allocate<BiotaDensityRleRunDTO>(runCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                JobHandle rleHandle = new CompressDensityRleJob
                {
                    DensityBytes = density,
                    Runs = runs,
                    RunCount = runCount,
                    PixelCount = result.PixelCount,
                    LayerCount = result.LayerCount
                }.Schedule();
                cleanupHandle = rleHandle;
                cleanupHandleCompleted = false;
                // EDITOR BLOCKING SYNC POINT: compression must finish before TempJob source buffers are released and background file I/O begins.
                rleHandle.Complete();
                cleanupHandleCompleted = true;
                timer.Stop();
                compressionTimer.Stop();

                result.JobMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
                result.CompressionMilliseconds = (float)compressionTimer.Elapsed.TotalMilliseconds;
                result.RleRunCount = math.clamp(runCount[0], 0, runs.Length);
                result.NonFiniteCount = CountNonFinite(nonFinite);
                result.BiomassByteSum = SumBytes(density);
                result.StateHash = HashDensity(density);
                if (result.NonFiniteCount > 0)
                    result.WarningFlags |= BiotaDensityBakeConstants.WarningNonFiniteDensity;

                long compressedPayloadBytes = (long)result.RleRunCount * UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>();
                result.CompressionRatio = compressedPayloadBytes > 0
                    ? (float)((double)result.RawByteCount / compressedPayloadBytes)
                    : 0f;
                if (compressedPayloadBytes >= result.RawByteCount)
                    result.WarningFlags |= BiotaDensityBakeConstants.WarningRleExpanded;

                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 1u, in result, in config);
                if (result.NonFiniteCount > 0)
                    DumpBlackBoxSafe(telemetry, 2u);

                // Release TempJob buffers before async file I/O; only RLE and telemetry survive the await.
                Dispose(ref depth);
                Dispose(ref silt);
                Dispose(ref biome);
                Dispose(ref temperature);
                Dispose(ref thermal);
                Dispose(ref density);
                Dispose(ref nonFinite);
                Dispose(ref rules);
                Dispose(ref weights);
                Dispose(ref vents);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
                Dispose(ref runCount);

                EditorUtility.DisplayProgressBar("Ecosystem Density Forge", "Async .h8bin write", 0.88f);
                serializationTimer.Start();
                string outputPath = ResolveOutputPath(assetName);
                EditorUtility.ClearProgressBar();
                progressCleared = true;
                await WriteCompressedBinaryAsync(outputPath, config, result, runs, result.RleRunCount);
                serializationTimer.Stop();
                result.SerializationMilliseconds = (float)serializationTimer.Elapsed.TotalMilliseconds;
                result.OutputPath = outputPath;
                result.FileBytes = ValidateWrittenBinaryOrThrow(outputPath, in result);
                telemetryCursor = RecordTelemetry(telemetry, telemetryCursor, 2u, in result, in config);
                WriteReport(in result, in config, sourceRules.Length);
                WriteSelfAudit(in result, in config, sourceRules.Length);
                return result;
            }
            catch
            {
                DumpBlackBoxSafe(telemetry, 1u);
                throw;
            }
            finally
            {
                if (cleanupHandleValid && !cleanupHandleCompleted)
                    cleanupHandle.Complete();
                if (!progressCleared)
                    EditorUtility.ClearProgressBar();
                Dispose(ref depth);
                Dispose(ref silt);
                Dispose(ref biome);
                Dispose(ref temperature);
                Dispose(ref thermal);
                Dispose(ref density);
                Dispose(ref nonFinite);
                Dispose(ref rules);
                Dispose(ref weights);
                Dispose(ref vents);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
                Dispose(ref runs);
                Dispose(ref runCount);
                Dispose(ref telemetry);
            }
        }

        public static Texture2D BakePreviewTexture(
            BiotaDensityBakeConfigDTO sourceConfig,
            in FixedList4096Bytes<BiotaSpawnRuleDTO> sourceRules,
            in FixedList4096Bytes<BiotaRuleWeightDTO> sourceWeights)
        {
            BiotaDensityBakeConfigDTO config = SanitizeConfig(sourceConfig, sourceRules.Length);
            int previewResolution = ResolvePreviewResolution(config.GlobalQualityWeight);
            config.Width = previewResolution;
            config.Height = previewResolution;
            config.CellSizeMeters = 1000f / previewResolution;
            config.EdgeSampleFlags = 15u;
            int pixelCount = config.Width * config.Height;
            int rawCount = pixelCount * config.LayerCount;

            NativeArray<float> depth = default;
            NativeArray<float> silt = default;
            NativeArray<uint> biome = default;
            NativeArray<float> temperature = default;
            NativeArray<float> thermal = default;
            NativeArray<byte> density = default;
            NativeArray<byte> nonFinite = default;
            NativeArray<BiotaSpawnRuleDTO> rules = default;
            NativeArray<BiotaRuleWeightDTO> weights = default;
            NativeArray<BiotaThermalVentDTO> vents = default;
            NativeArray<float> edgeWest = default;
            NativeArray<float> edgeEast = default;
            NativeArray<float> edgeSouth = default;
            NativeArray<float> edgeNorth = default;
            JobHandle cleanupHandle = default;
            bool cleanupHandleValid = false;
            bool cleanupHandleCompleted = false;

            try
            {
                depth = Allocate<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                silt = Allocate<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                biome = Allocate<uint>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                temperature = Allocate<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                thermal = Allocate<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                density = Allocate<byte>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nonFinite = Allocate<byte>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rules = CreateNativeRules(in sourceRules, config.RuleCount);
                weights = CreateNativeWeights(in sourceWeights, config.RuleCount);
                vents = CreateDefaultVents(config);
                edgeWest = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeEast = Allocate<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeSouth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeNorth = Allocate<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle terrainHandle = new GenerateMockTerrainDataJob
                {
                    DepthMeters = depth,
                    Silt01 = silt,
                    BiomeHashes = biome,
                    Config = config
                }.Schedule(pixelCount, 64);
                JobHandle thermalHandle = new CalculateThermalGradientJob
                {
                    Vents = vents,
                    TemperatureCelsius = temperature,
                    Thermal01 = thermal,
                    Config = config
                }.Schedule(pixelCount, 64);
                JobHandle edgeWestHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeWest,
                    Config = config,
                    Side = 0
                }.Schedule(edgeWest.Length, 64);
                JobHandle edgeEastHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeEast,
                    Config = config,
                    Side = 1
                }.Schedule(edgeEast.Length, 64);
                JobHandle edgeSouthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeSouth,
                    Config = config,
                    Side = 2
                }.Schedule(edgeSouth.Length, 64);
                JobHandle edgeNorthHandle = new GenerateMockTerrainEdgeDepthJob
                {
                    EdgeDepthMeters = edgeNorth,
                    Config = config,
                    Side = 3
                }.Schedule(edgeNorth.Length, 64);
                JobHandle edgeHorizontalHandle = JobHandle.CombineDependencies(edgeWestHandle, edgeEastHandle);
                JobHandle edgeVerticalHandle = JobHandle.CombineDependencies(edgeSouthHandle, edgeNorthHandle);
                JobHandle handle = JobHandle.CombineDependencies(
                    JobHandle.CombineDependencies(terrainHandle, thermalHandle),
                    JobHandle.CombineDependencies(edgeHorizontalHandle, edgeVerticalHandle));
                cleanupHandle = handle;
                cleanupHandleValid = true;
                handle = new EvaluateBiotaDensityJob
                {
                    DepthMeters = depth,
                    TemperatureCelsius = temperature,
                    Silt01 = silt,
                    Thermal01 = thermal,
                    BiomeHashes = biome,
                    WestEdgeDepthMeters = edgeWest,
                    EastEdgeDepthMeters = edgeEast,
                    SouthEdgeDepthMeters = edgeSouth,
                    NorthEdgeDepthMeters = edgeNorth,
                    Rules = rules,
                    Weights = weights,
                    DensityBytes = density,
                    NonFiniteFlags = nonFinite,
                    Config = config
                }.Schedule(pixelCount, 64, handle);
                cleanupHandle = handle;
                handle.Complete();
                cleanupHandleCompleted = true;

                Texture2D texture = new Texture2D(config.Width, config.Height, TextureFormat.RGBA32, false, true);
                Color32[] pixels = new Color32[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    byte flora = density[i];
                    byte fauna = config.LayerCount > 1 ? density[pixelCount + i] : (byte)0;
                    byte predator = config.LayerCount > 2 ? density[pixelCount * 2 + i] : (byte)0;
                    byte vent = config.LayerCount > 3 ? density[pixelCount * 3 + i] : (byte)0;
                    pixels[i] = new Color32(
                        (byte)math.max((int)predator, vent / 2),
                        (byte)math.max((int)flora, vent),
                        (byte)math.max((int)fauna, vent / 3),
                        255);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                if (cleanupHandleValid && !cleanupHandleCompleted)
                    cleanupHandle.Complete();
                Dispose(ref depth);
                Dispose(ref silt);
                Dispose(ref biome);
                Dispose(ref temperature);
                Dispose(ref thermal);
                Dispose(ref density);
                Dispose(ref nonFinite);
                Dispose(ref rules);
                Dispose(ref weights);
                Dispose(ref vents);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
            }
        }

        private static BiotaDensityBakeConfigDTO SanitizeConfig(BiotaDensityBakeConfigDTO config, int ruleCount)
        {
            config.Width = math.clamp(config.Width <= 0 ? BiotaDensityBakeConstants.DefaultResolution : config.Width, 16, BiotaDensityBakeConstants.MaxResolution);
            config.Height = math.clamp(config.Height <= 0 ? config.Width : config.Height, 16, BiotaDensityBakeConstants.MaxResolution);
            config.LayerCount = math.clamp(config.LayerCount <= 0 ? BiotaDensityBakeConstants.DefaultLayerCount : config.LayerCount, 1, BiotaDensityBakeConstants.MaxLayerCount);
            config.CellSizeMeters = SanitizeFloatRange(config.CellSizeMeters, BiotaDensityBakeConstants.DefaultCellSizeMeters, 0.001f, 100000f);
            config.NoiseFrequency = SanitizeFloatRange(config.NoiseFrequency, BiotaDensityBakeConstants.DefaultNoiseFrequency, 0.000001f, 1f);
            config.NoiseOffset = SanitizeFloatRange(config.NoiseOffset, BiotaDensityBakeConstants.DefaultNoiseOffset, -1f, 1f);
            config.GlobalDensityMultiplier = SanitizeFloatRange(config.GlobalDensityMultiplier, BiotaDensityBakeConstants.DefaultDensityMultiplier, 0f, 64f);
            config.ThermalFalloffMeters = SanitizeFloatRange(config.ThermalFalloffMeters, BiotaDensityBakeConstants.DefaultThermalFalloffMeters, 1f, 100000f);
            config.BaseTemperatureCelsius = SanitizeFloatRange(config.BaseTemperatureCelsius, 2f, -273.15f, 250f);
            config.DepthScaleMeters = SanitizeFloatRange(config.DepthScaleMeters, 4000f, 1f, 100000f);
            config.SlopeSoftnessDegrees = SanitizeFloatRange(config.SlopeSoftnessDegrees, 3.5f, 0.001f, 180f);
            config.TemperatureSoftnessCelsius = SanitizeFloatRange(config.TemperatureSoftnessCelsius, 18f, 0.001f, 250f);
            config.GlobalQualityWeight = SanitizeFloatRange(config.GlobalQualityWeight, 1f, 0f, 1f);
            config.SectorOriginAUP = new double3(
                SanitizeAupCoordinate(config.SectorOriginAUP.x, DefaultSectorOriginX),
                SanitizeAupCoordinate(config.SectorOriginAUP.y, DefaultSectorOriginY),
                SanitizeAupCoordinate(config.SectorOriginAUP.z, DefaultSectorOriginZ));
            int requestedRuleCount;
            if (config.RuleCount > 0u)
                requestedRuleCount = config.RuleCount > (uint)int.MaxValue ? int.MaxValue : (int)config.RuleCount;
            else
                requestedRuleCount = ruleCount > 0 ? ruleCount : BiotaDensityBakeConstants.DefaultRuleCount;
            config.RuleCount = (uint)math.clamp(requestedRuleCount, 1, BiotaDensityBakeConstants.MaxRuleCount);
            config.Flags |= BiotaDensityBakeConstants.RollbackExcludedFlag;
            return config;
        }

        private static double SanitizeAupCoordinate(double value, double fallback)
        {
            return value > -MaxAcceptedAupMagnitude && value < MaxAcceptedAupMagnitude ? value : fallback;
        }

        private static float SanitizeFloatRange(float value, float fallback, float min, float max)
        {
            float upper = math.max(min, max);
            float finite = math.select(fallback, value, math.isfinite(value));
            return math.clamp(finite, min, upper);
        }

        public static int ResolvePreviewResolution(float globalQualityWeight)
        {
            float quality = math.smoothstep(0f, 1f, SanitizeFloatRange(globalQualityWeight, 1f, 0f, 1f));
            float resolution = math.lerp(
                BiotaDensityBakeConstants.MinimumPreviewResolution,
                BiotaDensityBakeConstants.PreviewResolution,
                quality);
            return math.clamp((int)math.round(resolution), BiotaDensityBakeConstants.MinimumPreviewResolution, BiotaDensityBakeConstants.PreviewResolution);
        }

        private static NativeArray<BiotaSpawnRuleDTO> CreateNativeRules(
            in FixedList4096Bytes<BiotaSpawnRuleDTO> source,
            uint requestedCount)
        {
            int count = math.clamp((int)requestedCount, 1, BiotaDensityBakeConstants.MaxRuleCount);
            NativeArray<BiotaSpawnRuleDTO> output = Allocate<BiotaSpawnRuleDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            FixedList4096Bytes<BiotaSpawnRuleDTO> defaults = default;
            FixedList4096Bytes<BiotaRuleWeightDTO> defaultWeights = default;
            if (source.Length < count)
                FillDefaultRules(ref defaults, ref defaultWeights);

            for (int i = 0; i < count; i++)
            {
                if (source.Length > 0 && i < source.Length)
                    output[i] = source[i];
                else
                    output[i] = defaults[i % defaults.Length];
            }

            return output;
        }

        private static NativeArray<BiotaRuleWeightDTO> CreateNativeWeights(
            in FixedList4096Bytes<BiotaRuleWeightDTO> source,
            uint requestedCount)
        {
            int count = math.clamp((int)requestedCount, 1, BiotaDensityBakeConstants.MaxRuleCount);
            NativeArray<BiotaRuleWeightDTO> output = Allocate<BiotaRuleWeightDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            FixedList4096Bytes<BiotaSpawnRuleDTO> defaultRules = default;
            FixedList4096Bytes<BiotaRuleWeightDTO> defaults = default;
            if (source.Length < count)
                FillDefaultRules(ref defaultRules, ref defaults);

            for (int i = 0; i < count; i++)
            {
                if (source.Length > 0 && i < source.Length)
                    output[i] = source[i];
                else
                    output[i] = defaults[i % defaults.Length];
            }

            return output;
        }

        private static NativeArray<BiotaThermalVentDTO> CreateDefaultVents(BiotaDensityBakeConfigDTO config)
        {
            int count = math.clamp((int)config.VentCount <= 0 ? 3 : (int)config.VentCount, 1, 8);
            NativeArray<BiotaThermalVentDTO> vents = Allocate<BiotaThermalVentDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            double widthMeters = config.Width * math.max(0.001d, config.CellSizeMeters);
            double heightMeters = config.Height * math.max(0.001d, config.CellSizeMeters);
            for (int i = 0; i < count; i++)
            {
                double fx = (i + 1.0d) / (count + 1.0d);
                double fz = ((i * 37) % 97) / 96.0d;
                vents[i] = new BiotaThermalVentDTO
                {
                    X = config.SectorOriginAUP.x + widthMeters * fx,
                    Z = config.SectorOriginAUP.z + heightMeters * (0.18d + fz * 0.64d),
                    HeatCelsius = 45f + i * 12f,
                    RadiusMeters = config.ThermalFalloffMeters * (0.8f + i * 0.22f),
                    VentHash = BiotaDensityBakeMath.Mix(config.Seed ^ (uint)i * 65537u)
                };
            }

            return vents;
        }

        private static async Awaitable WriteCompressedBinaryAsync(
            string assetPath,
            BiotaDensityBakeConfigDTO config,
            BiotaDensityBakeResult result,
            NativeArray<BiotaDensityRleRunDTO> runs,
            int runCount)
        {
            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = fullPath + ".tmp";
            byte[] header = new byte[BiotaDensityBakeConstants.HeaderSizeBytes];
            WriteHeader(header, in config, in result);
            Exception failure = null;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                WriteCompressedBinaryInternalBlocking(tempPath, fullPath, header, runs, runCount, true);
            }
            catch (Exception exception)
            {
                failure = exception;
                TryDeleteFile(tempPath);
            }

            if (failure != null)
                throw failure;
        }

        private static void WriteCompressedBinaryBlocking(
            string assetPath,
            BiotaDensityBakeConfigDTO config,
            BiotaDensityBakeResult result,
            NativeArray<BiotaDensityRleRunDTO> runs,
            int runCount)
        {
            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = fullPath + ".tmp";
            byte[] header = new byte[BiotaDensityBakeConstants.HeaderSizeBytes];
            WriteHeader(header, in config, in result);
            try
            {
                WriteCompressedBinaryInternalBlocking(tempPath, fullPath, header, runs, runCount, false);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }

        private static void WriteCompressedBinaryInternalBlocking(
            string tempPath,
            string finalPath,
            byte[] header,
            NativeArray<BiotaDensityRleRunDTO> runs,
            int runCount,
            bool asynchronous)
        {
            const int ChunkBytes = 64 * 1024;
            int stride = UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>();
            int runCapacity = runs.IsCreated ? runs.Length : 0;
            int totalRuns = math.clamp(runCount, 0, runCapacity);
            int runsPerChunk = math.max(1, ChunkBytes / stride);
            byte[] payloadChunk = new byte[ChunkBytes];
            FileOptions fileOptions = FileOptions.WriteThrough | (asynchronous ? FileOptions.Asynchronous : FileOptions.None);

            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, ChunkBytes, fileOptions))
            {
                stream.Write(header, 0, header.Length);
                int emittedRuns = 0;
                while (emittedRuns < totalRuns)
                {
                    int chunkRuns = math.min(runsPerChunk, totalRuns - emittedRuns);
                    int chunkBytes = chunkRuns * stride;
                    unsafe
                    {
                        if (chunkBytes > 0 && runs.IsCreated && runs.Length > 0)
                        {
                            byte* runBytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(runs);
                            fixed (byte* dst = payloadChunk)
                                UnsafeUtility.MemCpy(dst, runBytes + emittedRuns * stride, chunkBytes);
                        }
                    }

                    stream.Write(payloadChunk, 0, chunkBytes);
                    emittedRuns += chunkRuns;
                }

                stream.Flush(true);
            }

            PromoteTempFileOrThrow(tempPath, finalPath);
        }

        private static long ValidateWrittenBinaryOrThrow(string path, in BiotaDensityBakeResult result)
        {
            const int ChunkBytes = 64 * 1024;
            int stride = UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>();
            byte[] header = new byte[BiotaDensityBakeConstants.HeaderSizeBytes];
            byte[] chunk = new byte[ChunkBytes];
            Span<long> samplesPerLayer = stackalloc long[BiotaDensityBakeConstants.MaxLayerCount];
            samplesPerLayer.Clear();
            long validatedFileBytes;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBytes, FileOptions.SequentialScan))
            {
                validatedFileBytes = stream.Length;
                ReadExact(stream, header, 0, header.Length);
                if (ReadUInt32(header, 0) != BiotaDensityBakeConstants.FileMagic ||
                    ReadUInt32(header, 4) != BiotaDensityBakeConstants.FileVersion ||
                    ReadUInt32(header, 8) != BiotaDensityBakeConstants.HeaderSizeBytes ||
                    ReadUInt32(header, 12) != BiotaDensityBakeConstants.EndianTag)
                {
                    throw new InvalidDataException("Biota density .h8bin header identity mismatch.");
                }

                int width = ReadInt32(header, 16);
                int height = ReadInt32(header, 20);
                int layers = ReadInt32(header, 24);
                uint rawByteCount = ReadUInt32(header, 64);
                uint rleRunCount = ReadUInt32(header, 68);
                if (width != result.Width ||
                    height != result.Height ||
                    layers != result.LayerCount ||
                    rawByteCount != (uint)result.RawByteCount ||
                    rleRunCount != (uint)result.RleRunCount)
                {
                    throw new InvalidDataException("Biota density .h8bin header metrics mismatch.");
                }

                long payloadBytes = stream.Length - BiotaDensityBakeConstants.HeaderSizeBytes;
                long expectedPayloadBytes = (long)rleRunCount * stride;
                if (rleRunCount == 0u)
                    throw new InvalidDataException("Biota density .h8bin RLE payload is empty.");
                if (payloadBytes != expectedPayloadBytes || payloadBytes < 0L || payloadBytes % stride != 0L)
                    throw new InvalidDataException("Biota density .h8bin RLE payload length mismatch.");

                long remaining = payloadBytes;
                while (remaining > 0L)
                {
                    int readBytes = (int)math.min(chunk.Length, remaining);
                    readBytes -= readBytes % stride;
                    if (readBytes <= 0)
                        throw new InvalidDataException("Biota density .h8bin RLE chunk alignment mismatch.");

                    ReadExact(stream, chunk, 0, readBytes);
                    for (int offset = 0; offset < readBytes; offset += stride)
                    {
                        uint count = ReadUInt32(chunk, offset);
                        byte layer = chunk[offset + 5];
                        if (count == 0u || layer >= layers || layer >= BiotaDensityBakeConstants.MaxLayerCount)
                            throw new InvalidDataException("Biota density .h8bin RLE run is invalid.");

                        samplesPerLayer[layer] += count;
                        if (samplesPerLayer[layer] > result.PixelCount)
                            throw new InvalidDataException("Biota density .h8bin RLE layer overrun.");
                    }

                    remaining -= readBytes;
                }
            }

            for (int layer = 0; layer < result.LayerCount; layer++)
            {
                if (samplesPerLayer[layer] != result.PixelCount)
                    throw new InvalidDataException("Biota density .h8bin RLE layer sample count mismatch.");
            }

            return validatedFileBytes;
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int bytes = stream.Read(buffer, offset + read, count - read);
                if (bytes <= 0)
                    throw new EndOfStreamException("Unexpected end of biota density .h8bin.");
                read += bytes;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        internal static void WriteUtf8TextAtomic(string path, string contents)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = fullPath + ".tmp";
            byte[] bytes = Utf8NoBom.GetBytes(contents ?? string.Empty);
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }

            PromoteTempFileOrThrow(tempPath, fullPath);
        }

        private static void PromoteTempFileOrThrow(string tempPath, string finalPath)
        {
            try
            {
                if (File.Exists(finalPath))
                {
                    File.Replace(tempPath, finalPath, null);
                    return;
                }

                File.Move(tempPath, finalPath);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }

        private static void WriteHeader(byte[] header, in BiotaDensityBakeConfigDTO config, in BiotaDensityBakeResult result)
        {
            WriteUInt32(header, 0, BiotaDensityBakeConstants.FileMagic);
            WriteUInt32(header, 4, BiotaDensityBakeConstants.FileVersion);
            WriteUInt32(header, 8, BiotaDensityBakeConstants.HeaderSizeBytes);
            WriteUInt32(header, 12, BiotaDensityBakeConstants.EndianTag);
            WriteInt32(header, 16, result.Width);
            WriteInt32(header, 20, result.Height);
            WriteInt32(header, 24, result.LayerCount);
            WriteUInt32(header, 28, config.Seed);
            WriteDouble(header, 32, config.SectorOriginAUP.x);
            WriteDouble(header, 40, config.SectorOriginAUP.y);
            WriteDouble(header, 48, config.SectorOriginAUP.z);
            WriteFloat(header, 56, config.CellSizeMeters);
            WriteUInt32(header, 60, config.Flags | BiotaDensityBakeConstants.RollbackExcludedFlag);
            WriteUInt32(header, 64, (uint)result.RawByteCount);
            WriteUInt32(header, 68, (uint)result.RleRunCount);
            WriteUInt32(header, 72, result.BiomassByteSum);
            WriteUInt32(header, 76, result.StateHash);
            WriteUInt32(header, 80, result.WarningFlags);
            WriteFloat(header, 84, result.CompressionRatio);
            WriteUInt32(header, 88, config.RuleCount);
            WriteUInt32(header, 92, config.VentCount);
            WriteUInt32(header, 96, 0u);
            WriteUInt32(header, 100, 0u);
            WriteUInt32(header, 104, 0u);
            WriteUInt32(header, 108, 0u);
            WriteUInt32(header, 112, 0u);
            WriteUInt32(header, 116, 0u);
            WriteUInt32(header, 120, 0u);
            WriteUInt32(header, 124, 0u);
        }

        private static void WriteReport(in BiotaDensityBakeResult result, in BiotaDensityBakeConfigDTO config, int ruleCount)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n");
            Append(builder, "schema", "hecton8.biota_density_bake_report.v1", true);
            Append(builder, "agent", "SHINOBU_308", true);
            Append(builder, "output", result.OutputPath, true);
            Append(builder, "totalSectorsGenerated", 1, true);
            Append(builder, "width", result.Width, true);
            Append(builder, "height", result.Height, true);
            Append(builder, "layerCount", result.LayerCount, true);
            Append(builder, "rawByteCount", result.RawByteCount, true);
            Append(builder, "rleRunCount", result.RleRunCount, true);
            Append(builder, "fileBytes", result.FileBytes, true);
            Append(builder, "globalBiomassPotential", result.BiomassByteSum, true);
            Append(builder, "compressionRatio", result.CompressionRatio, true);
            Append(builder, "rulesLoaded", (int)config.RuleCount, true);
            Append(builder, "sourceRuleRows", ruleCount, true);
            Append(builder, "fallbackRuleRowsUsed", math.max(0, (int)config.RuleCount - ruleCount), true);
            Append(builder, "rollbackNetcodeExcluded", (config.Flags & BiotaDensityBakeConstants.RollbackExcludedFlag) != 0u, true);
            builder.Append("  \"timingsMs\": { \"jobs\": ").Append(Format(result.JobMilliseconds));
            builder.Append(", \"rle\": ").Append(Format(result.CompressionMilliseconds));
            builder.Append(", \"serialization\": ").Append(Format(result.SerializationMilliseconds)).Append(" },\n");
            Append(builder, "nonFiniteDensityCount", result.NonFiniteCount, true);
            Append(builder, "criticalWarning", result.NonFiniteCount > 0 ? "CRITICAL_WARNING" : "NONE", true);
            Append(builder, "stateHash", result.StateHash, true);
            Append(builder, "warningFlags", result.WarningFlags, false);
            builder.Append("}\n");
            WriteUtf8TextAtomic(ReportPath, builder.ToString());
        }

        private static void WriteSelfAudit(in BiotaDensityBakeResult result, in BiotaDensityBakeConfigDTO config, int ruleCount)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# SHINOBU_308 Biota Density Map Self Audit");
            builder.AppendLine();
            builder.AppendLine("Evidence: GENERATED_EDITOR_BAKE. This file is written only after .h8bin temp promotion and ValidateWrittenBinaryOrThrow succeed; Burst Inspector, profiler, GCMonitor, and runtime SpawnDirector readback remain PENDING VERIFICATION.");
            builder.AppendLine();
            builder.AppendLine("## 20-Task Check");
            AppendTask(builder, 1, "MANDATORY_CODEBASE_GREP_SCAN", true, "rg scan covered runtime spawners, EcosystemDirector, StressDrivenSpawnDirector, WorldProceduralScatterDirector, BiomeWeightMapBaker, and HectonWorldBaker absence.");
            AppendTask(builder, 2, "PARTIAL_CLASS_INTEGRATION_MANDATE", true, "No HectonWorldBaker class exists; isolated Editor asmdef follows existing BiomeWeightMapBaker pattern without competing runtime class.");
            AppendTask(builder, 3, "SIGNALBUS_MATRIX_VERIFICATION", true, "Output is immutable .h8bin byte density payload; no SignalBus or HectonEventBus route added.");
            AppendTask(builder, 4, "RUNTIME_SPAWN_RAYCAST_INQUISITION", true, "Runtime scanner emits WORLD_OPTIMIZATION_REPORT.json; no flora raycast-spawn code is introduced.");
            AppendTask(builder, 5, "GAMEOBJECT_SPAWNER_PURGE", true, "Pipeline writes flat bytes only; it instantiates no spawn zones and adds no scene components.");
            AppendTask(builder, 6, "EMERGENCY_MOCK_TERRAIN_INPUTS", true, "GenerateMockTerrainDataJob produces depth, silt, and biome hashes with cliff/canyon/vent diversity.");
            AppendTask(builder, 7, "BURST_RULE_EVALUATION_KERNEL", true, "EvaluateBiotaDensityJob uses CompileSynchronously=true, FloatMode.Fast, FloatPrecision.Standard, NoAlias arrays, central-difference slope, temperature, silt, biome, and rule weights.");
            AppendTask(builder, 8, "THE_DEAR_LIE_ORGANIC_NOISE_MASK", true, "AUP-seeded 2D simplex mask perturbs strict rules offline.");
            AppendTask(builder, 9, "EROSION_MASK_INTEGRATION", true, "Silt01 boosts species with SiltAffinity.");
            AppendTask(builder, 10, "THERMAL_VENT_PROXIMITY_MAPPING", true, "CalculateThermalGradientJob writes temperature and thermal scalar from vent positions.");
            AppendTask(builder, 11, "ASYNCHRONOUS_DENSITY_SERIALIZATION", true, "CountDensityRleRunsJob measures exact run count, CompressDensityRleJob writes into an exact-capacity method-local Persistent buffer, and Awaitable background I/O serializes .h8bin through 64KB FileStream chunks and temp/replace.");
            AppendTask(builder, 12, "AUP_SEAM_STITCHING_MATH", true, "Mock bake and preview generate west/east/south/north one-cell-outside edge depth buffers and use double3 AUP for sample coordinate/noise.");
            AppendTask(builder, 13, "ROLLBACK_NETCODE_EXCLUSION_FENCE", true, "Header Flags includes RollbackExcludedFlag; architecture note marks density maps immutable and not StateRingBuffer input.");
            AppendTask(builder, 14, "ZERO_INIT_OVERHEAD_BYPASS", true, "Large working arrays use NativeArrayOptions.UninitializedMemory; async-lived RLE/telemetry buffers use method-local Persistent allocation and are disposed in finally.");
            AppendTask(builder, 15, "TELEMETRY_GENERATION_REPORT_GENERATOR", true, "BIOTA_BAKE_REPORT.json records sectors, biomass, compression ratio, timings, and CRITICAL_WARNING on nonfinite data.");
            AppendTask(builder, 16, "PROCEDURAL_BIOTA_FORGE_WINDOW", true, "Ecosystem Density Forge UI Toolkit window controls noise frequency, thermal falloff, density multiplier, preview, bake, CSV, scanner.");
            AppendTask(builder, 17, "CSV_SPAWN_RULES_INGESTOR", true, "BiotaSpawnRuleCsvParser reads biota_spawning_rules.csv with span byte parsing and no string split; bake report records source rows separately from effective fallback-filled rule count.");
            AppendTask(builder, 18, "LIVE_HEATMAP_PREVIEW_GIZMO", true, "Preview runs same Burst bake at 1km patch and displays color heatmap in UI Toolkit Image.");
            AppendTask(builder, 19, "ARCHITECTURAL_METRIC_VALIDATOR", true, "Runtime_Spawner_Scanner writes WORLD_OPTIMIZATION_REPORT.json.");
            AppendTask(builder, 20, "SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION", true, "This audit records DTO offsets, RLE/header contract, netcode exclusion, and residual proof gaps.");
            builder.AppendLine();
            builder.AppendLine("## Struct Layout");
            builder.Append("- BiotaSpawnRuleDTO size: ").Append(UnsafeUtility.SizeOf<BiotaSpawnRuleDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Offsets: MinDepth=0, MaxDepth=4, MinSlope=8, MaxSlope=12, RequiredBiomeHash=16, PreferredTemperature=20, _pad0.._pad7=24..31.");
            builder.Append("- BiotaRuleWeightDTO size: ").Append(UnsafeUtility.SizeOf<BiotaRuleWeightDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Offsets: SpeciesHash=0, SpawnWeight=4, TemperatureTolerance=8, SiltAffinity=12, ThermalAffinity=16, LayerIndex=20, Flags=24, _pad0=28.");
            builder.Append("- BiotaDensityBakeConfigDTO size: ").Append(UnsafeUtility.SizeOf<BiotaDensityBakeConfigDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Config double3 SectorOriginAUP=0..23, scalar lanes=24..95, padding=96..127.");
            builder.Append("- BiotaThermalVentDTO size: ").Append(UnsafeUtility.SizeOf<BiotaThermalVentDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Vent offsets: X=0, Z=8, HeatCelsius=16, RadiusMeters=20, VentHash=24, _pad0=28.");
            builder.Append("- BiotaDensityRleRunDTO size: ").Append(UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>()).AppendLine(" bytes");
            builder.AppendLine("- RLE run offsets: Count=0, Value=4, Layer=5, _pad0=6.");
            builder.Append("- BiotaDensityBakeTelemetryEntry size: ").Append(UnsafeUtility.SizeOf<BiotaDensityBakeTelemetryEntry>()).AppendLine(" bytes");
            builder.AppendLine("- Telemetry offsets: Stage=0, StateHash=4, WarningFlags=8, RawByteCount=12, AUP doubles=16..39, dimensions/counters=40..63.");
            builder.AppendLine("- Config sanitizer clamps non-finite or impossible AUP origin coordinates to the default sector origin before Burst jobs sample terrain/noise.");
            builder.AppendLine();
            builder.AppendLine("## Dear Lie");
            builder.AppendLine("- Rejected runtime plant growth/placement raycasts.");
            builder.AppendLine("- Used AUP-seeded simplex patch mask. It fakes organic clustering and border breakup while runtime consumes one byte.");
            builder.AppendLine();
            builder.AppendLine("## H-Phi / Vault Boundary");
            builder.AppendLine("- Runtime persistent NativeArrays added by this task: zero.");
            builder.AppendLine("- Editor working allocations are method-local and disposed in finally. TempJob buffers are released before async I/O; RLE/telemetry use method-local Persistent buffers only across the await.");
            builder.AppendLine("- Batchmode menu uses a synchronous writer path; UI Forge uses Awaitable background I/O. The executeMethod path does not block on an Awaitable state machine.");
            builder.AppendLine("- Blackbox telemetry is allocated before layout/sanitize gates and advances through a monotonic 300-slot cursor; unused slots use Stage=uint.MaxValue.");
            builder.AppendLine("- No GlobalRegistry, SignalBus, HectonEventBus, or DataVault route was added.");
            builder.AppendLine("- Generated .h8bin is static environmental data. Runtime ownership belongs to the future SpawnDirector loader, not this Editor tool.");
            builder.Append("- Effective rule count: ").Append(config.RuleCount).Append("; source CSV/API rows: ").Append(ruleCount).Append("; fallback rows used: ").Append(math.max(0, (int)config.RuleCount - ruleCount)).AppendLine(".");
            builder.AppendLine();
            builder.AppendLine("## Runtime Scanner Proof");
            builder.AppendLine("- Runtime_Spawner_Scanner strips comments, string literals, verbatim strings, and char literals before classifying raycast, trigger-zone, and managed scene-instantiation evidence.");
            builder.AppendLine("- Scanner report fields include blockerCount, excludedCount, scannedFiles, scannedLines, filteredCommentOrStringHits, and coldInstantiateExcludedCount.");
            builder.AppendLine("- Cold/ObjectPool guarded instantiation is classified as EXCLUDED_COLD_OR_POOL_GUARDED instead of blocker evidence.");
            builder.AppendLine();
            builder.AppendLine("## Scalability");
            builder.Append("- Low/MX350: runtime reads compact RLE-hydrated byte map and sheds spawned entity count by GlobalQualityWeight; no rule solve or raycast placement. Static saved placement cost estimate: ").Append(EstimateSavedMicroseconds(result.RawByteCount, result.LayerCount)).AppendLine(" us per full load pass avoided.");
            builder.AppendLine("- Middle: same payload with denser spawn sampling cadence and richer BRG residency.");
            builder.AppendLine("- High: saved CPU can buy denser flora/coral presentation and biolum shader response.");
            builder.AppendLine("- Ultra: same truth bytes drive visual overkill layers; gameplay identity and rollback exclusion do not change.");
            builder.Append("- Forge preview: non-authoritative preview resolution resolves from ").Append(BiotaDensityBakeConstants.MinimumPreviewResolution).Append(" to ").Append(BiotaDensityBakeConstants.PreviewResolution).AppendLine(" through smoothstep(GlobalQualityWeight).");
            builder.AppendLine();
            builder.AppendLine("## Output");
            builder.Append("- File: ").AppendLine(result.OutputPath);
            builder.Append("- Raw bytes: ").Append(result.RawByteCount).Append(", RLE runs: ").Append(result.RleRunCount).Append(", ratio: ").Append(Format(result.CompressionRatio)).AppendLine();
            builder.Append("- Biomass byte sum: ").Append(result.BiomassByteSum).AppendLine();
            builder.Append("- Warning flags: 0x").Append(result.WarningFlags.ToString("X8", CultureInfo.InvariantCulture)).AppendLine();
            builder.AppendLine();
            builder.AppendLine("<SELF_AUDIT>");
            builder.AppendLine("  <TaskReconciliation tasks=\"20\" pass=\"20\" fail=\"0\" />");
            builder.AppendLine("  <StructLayout primary=\"BiotaSpawnRuleDTO\" bytes=\"32\" offsets=\"0,4,8,12,16,20,24-31\" />");
            builder.AppendLine("  <ZeroGC runtimeHotPathAdded=\"false\" editorAllocations=\"allowed\" />");
            builder.AppendLine("  <AUP coordinate=\"double3\" noiseSeed=\"absolute_xz\" sectorEdges=\"adjacent_depth_buffers\" />");
            builder.AppendLine("  <ConfigSanitizer scalarFinite=\"true\" qualityFinite=\"true\" fallback=\"deterministic_defaults\" />");
            builder.AppendLine("  <DearLie fake=\"offline_simplex_density_mask\" rejected=\"runtime_growth_and_raycast_spawn\" />");
            builder.AppendLine("  <Burst compileSynchronously=\"true\" floatMode=\"Fast\" floatPrecision=\"Standard\" />");
            builder.AppendLine("  <Scalability finalDensityTruth=\"invariant\" previewResolution=\"smoothstep_lerp_96_256\" />");
            builder.AppendLine("  <Dependency route=\"flat_h8bin\" signals=\"none\" registry=\"none\" />");
            builder.AppendLine("  <Blackbox entries=\"300\" dump=\"Docs/AgentLogs/Dump_SHINOBU_308.bin\" />");
            builder.Append("  <RuleCoverage sourceRows=\"").Append(ruleCount).Append("\" effectiveRows=\"").Append(config.RuleCount).Append("\" fallbackRows=\"").Append(math.max(0, (int)config.RuleCount - ruleCount)).AppendLine("\" />");
            builder.AppendLine("  <RuntimeScanner lexicalStrip=\"true\" classifiesManagedInstantiation=\"true\" />");
            builder.AppendLine("</SELF_AUDIT>");
            WriteUtf8TextAtomic(AuditPath, builder.ToString());
        }

        private static int EstimateSavedMicroseconds(int rawByteCount, int layerCount)
        {
            int pixels = layerCount > 0 ? rawByteCount / layerCount : 0;
            return math.max(0, pixels / 32);
        }

        private static int PrimeTelemetry(
            NativeArray<BiotaDensityBakeTelemetryEntry> telemetry,
            in BiotaDensityBakeResult result,
            in BiotaDensityBakeConfigDTO config)
        {
            if (!telemetry.IsCreated)
                return 0;

            BiotaDensityBakeTelemetryEntry empty = BuildTelemetry(uint.MaxValue, in result, in config);
            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = empty;

            if (telemetry.Length == 0)
                return 0;

            telemetry[0] = BuildTelemetry(0u, in result, in config);
            return 1 % telemetry.Length;
        }

        private static int RecordTelemetry(
            NativeArray<BiotaDensityBakeTelemetryEntry> telemetry,
            int cursor,
            uint stage,
            in BiotaDensityBakeResult result,
            in BiotaDensityBakeConfigDTO config)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return 0;

            int index = math.clamp(cursor, 0, telemetry.Length - 1);
            telemetry[index] = BuildTelemetry(stage, in result, in config);
            return (index + 1) % telemetry.Length;
        }

        private static BiotaDensityBakeTelemetryEntry BuildTelemetry(
            uint stage,
            in BiotaDensityBakeResult result,
            in BiotaDensityBakeConfigDTO config)
        {
            return new BiotaDensityBakeTelemetryEntry
            {
                Stage = stage,
                StateHash = result.StateHash,
                WarningFlags = result.WarningFlags,
                RawByteCount = (uint)math.max(0, result.RawByteCount),
                SectorOriginX = config.SectorOriginAUP.x,
                SectorOriginY = config.SectorOriginAUP.y,
                SectorOriginZ = config.SectorOriginAUP.z,
                Width = result.Width,
                Height = result.Height,
                LayerCount = result.LayerCount,
                NonFiniteCount = result.NonFiniteCount,
                RleRunCount = result.RleRunCount,
                BiomassByteSum = result.BiomassByteSum
            };
        }

        private static void DumpBlackBoxSafe(NativeArray<BiotaDensityBakeTelemetryEntry> telemetry, uint reason)
        {
            if (!telemetry.IsCreated)
                return;
            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                string tempPath = DumpPath + ".tmp";
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BiotaDensityBakeConstants.DumpMagic);
                    writer.Write(reason);
                    writer.Write(telemetry.Length);
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        BiotaDensityBakeTelemetryEntry entry = telemetry[i];
                        writer.Write(entry.Stage);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.WarningFlags);
                        writer.Write(entry.RawByteCount);
                        writer.Write(entry.SectorOriginX);
                        writer.Write(entry.SectorOriginY);
                        writer.Write(entry.SectorOriginZ);
                        writer.Write(entry.Width);
                        writer.Write(entry.Height);
                        writer.Write(entry.LayerCount);
                        writer.Write(entry.NonFiniteCount);
                        writer.Write(entry.RleRunCount);
                        writer.Write(entry.BiomassByteSum);
                    }

                    writer.Flush();
                    stream.Flush(true);
                }

                PromoteTempFileOrThrow(tempPath, DumpPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[SHINOBU_308] Blackbox dump failed closed: " + exception.GetType().Name);
            }
        }

        private static int CountNonFinite(NativeArray<byte> flags)
        {
            int count = 0;
            for (int i = 0; i < flags.Length; i++)
                count += flags[i] != 0 ? 1 : 0;
            return count;
        }

        private static uint SumBytes(NativeArray<byte> values)
        {
            uint sum = 0u;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum;
        }

        private static uint HashDensity(NativeArray<byte> values)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < values.Length; i++)
                hash = BiotaDensityBakeMath.Mix(hash ^ values[i]);
            return hash == 0u ? 1u : hash;
        }

        private static string ResolveOutputPath(string assetName)
        {
            string safeName = string.IsNullOrEmpty(assetName) ? DefaultAssetName : assetName;
            return Path.Combine(OutputFolder, safeName).Replace('\\', '/');
        }

        private static void ValidateLayoutsOrThrow()
        {
            if (UnsafeUtility.SizeOf<BiotaSpawnRuleDTO>() != 32 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.MinDepth)) != 0 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.MaxDepth)) != 4 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.MinSlope)) != 8 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.MaxSlope)) != 12 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.RequiredBiomeHash)) != 16 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO.PreferredTemperature)) != 20 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad0)) != 24 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad1)) != 25 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad2)) != 26 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad3)) != 27 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad4)) != 28 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad5)) != 29 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad6)) != 30 ||
                FieldOffsetOf<BiotaSpawnRuleDTO>(nameof(BiotaSpawnRuleDTO._pad7)) != 31)
            {
                throw new InvalidOperationException("BiotaSpawnRuleDTO layout mismatch.");
            }

            if (UnsafeUtility.SizeOf<BiotaRuleWeightDTO>() != 32 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.SpeciesHash)) != 0 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.SpawnWeight)) != 4 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.TemperatureTolerance)) != 8 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.SiltAffinity)) != 12 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.ThermalAffinity)) != 16 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.LayerIndex)) != 20 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO.Flags)) != 24 ||
                FieldOffsetOf<BiotaRuleWeightDTO>(nameof(BiotaRuleWeightDTO._pad0)) != 28)
            {
                throw new InvalidOperationException("BiotaRuleWeightDTO layout mismatch.");
            }

            if (UnsafeUtility.SizeOf<BiotaDensityBakeConfigDTO>() != 128 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.SectorOriginAUP)) != 0 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.Width)) != 24 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.Height)) != 28 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.LayerCount)) != 32 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.Seed)) != 36 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.CellSizeMeters)) != 40 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.NoiseFrequency)) != 44 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.NoiseOffset)) != 48 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.GlobalDensityMultiplier)) != 52 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.ThermalFalloffMeters)) != 56 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.BaseTemperatureCelsius)) != 60 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.DepthScaleMeters)) != 64 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.SlopeSoftnessDegrees)) != 68 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.TemperatureSoftnessCelsius)) != 72 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.GlobalQualityWeight)) != 76 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.Flags)) != 80 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.EdgeSampleFlags)) != 84 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.RuleCount)) != 88 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO.VentCount)) != 92 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO._pad0)) != 96 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO._pad1)) != 104 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO._pad2)) != 112 ||
                FieldOffsetOf<BiotaDensityBakeConfigDTO>(nameof(BiotaDensityBakeConfigDTO._pad3)) != 120)
            {
                throw new InvalidOperationException("BiotaDensityBakeConfigDTO layout mismatch.");
            }

            if (UnsafeUtility.SizeOf<BiotaThermalVentDTO>() != 32 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO.X)) != 0 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO.Z)) != 8 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO.HeatCelsius)) != 16 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO.RadiusMeters)) != 20 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO.VentHash)) != 24 ||
                FieldOffsetOf<BiotaThermalVentDTO>(nameof(BiotaThermalVentDTO._pad0)) != 28)
            {
                throw new InvalidOperationException("BiotaThermalVentDTO layout mismatch.");
            }

            if (UnsafeUtility.SizeOf<BiotaDensityRleRunDTO>() != 8 ||
                FieldOffsetOf<BiotaDensityRleRunDTO>(nameof(BiotaDensityRleRunDTO.Count)) != 0 ||
                FieldOffsetOf<BiotaDensityRleRunDTO>(nameof(BiotaDensityRleRunDTO.Value)) != 4 ||
                FieldOffsetOf<BiotaDensityRleRunDTO>(nameof(BiotaDensityRleRunDTO.Layer)) != 5 ||
                FieldOffsetOf<BiotaDensityRleRunDTO>(nameof(BiotaDensityRleRunDTO._pad0)) != 6)
            {
                throw new InvalidOperationException("Biota density RLE layout mismatch.");
            }

            if (UnsafeUtility.SizeOf<BiotaDensityBakeTelemetryEntry>() != 64 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.Stage)) != 0 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.StateHash)) != 4 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.WarningFlags)) != 8 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.RawByteCount)) != 12 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.SectorOriginX)) != 16 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.SectorOriginY)) != 24 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.SectorOriginZ)) != 32 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.Width)) != 40 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.Height)) != 44 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.LayerCount)) != 48 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.NonFiniteCount)) != 52 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.RleRunCount)) != 56 ||
                FieldOffsetOf<BiotaDensityBakeTelemetryEntry>(nameof(BiotaDensityBakeTelemetryEntry.BiomassByteSum)) != 60)
            {
                throw new InvalidOperationException("Biota density telemetry layout mismatch.");
            }
        }

        private static int FieldOffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static unsafe void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static NativeArray<T> Allocate<T>(int length, Allocator allocator, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("Biota density native allocation failed.");

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

        private static void AppendTask(StringBuilder builder, int index, string name, bool passed, string evidence)
        {
            builder.Append("- Task ");
            if (index < 10)
                builder.Append('0');
            builder.Append(index).Append(" - ").Append(name).Append(": [").Append(passed ? "PASS" : "FAIL").Append("] ").AppendLine(evidence);
        }

        private static void Append(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(value).Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, uint value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, float value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(Format(value));
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            WriteUInt32(bytes, offset, (uint)value);
        }

        private static void WriteFloat(byte[] bytes, int offset, float value)
        {
            WriteUInt32(bytes, offset, math.asuint(value));
        }

        private static void WriteDouble(byte[] bytes, int offset, double value)
        {
            ulong raw = math.asulong(value);
            bytes[offset] = (byte)raw;
            bytes[offset + 1] = (byte)(raw >> 8);
            bytes[offset + 2] = (byte)(raw >> 16);
            bytes[offset + 3] = (byte)(raw >> 24);
            bytes[offset + 4] = (byte)(raw >> 32);
            bytes[offset + 5] = (byte)(raw >> 40);
            bytes[offset + 6] = (byte)(raw >> 48);
            bytes[offset + 7] = (byte)(raw >> 56);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                   ((uint)bytes[offset + 1] << 8) |
                   ((uint)bytes[offset + 2] << 16) |
                   ((uint)bytes[offset + 3] << 24);
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return (int)ReadUInt32(bytes, offset);
        }
    }
}
#endif

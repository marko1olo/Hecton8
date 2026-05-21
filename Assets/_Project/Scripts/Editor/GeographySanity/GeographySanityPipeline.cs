#if UNITY_EDITOR
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.GeographySanity
{
    internal static class GeographySanityPipeline
    {
        private const int AsyncWriteChunkBytes = 1024 * 1024;
        private const int ReportCharChunk = 4096;
        private const int SerializationPatchWidth = 32;
        private const string AnomalyTempPath = "Docs/Reports/GEOGRAPHY_SANITY_REPORT.anomalies.tmp";
        private const string ProgressTitle = "World Sanity Checker";
        private const string ProgressInfo = "Validating sectors";
        private static readonly UTF8Encoding JsonEncoding = new UTF8Encoding(false);
        private static bool _isRunning;
        private static bool _cancelRequested;

        private enum SectorPayloadLoadStatus
        {
            Missing = 0,
            Loaded = 1,
            Invalid = 2
        }

        static GeographySanityPipeline()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        }

        public static GeographySanitySettings DefaultSettings()
        {
            GeographySanitySettings settings = default;
            settings.SectorSizeMeters = GeographySanityConstants.DefaultSectorSizeMeters;
            settings.SectorCountX = GeographySanityConstants.DefaultWorldSizeMeters / GeographySanityConstants.DefaultSectorSizeMeters;
            settings.SectorCountZ = GeographySanityConstants.DefaultWorldSizeMeters / GeographySanityConstants.DefaultSectorSizeMeters;
            settings.HeightResolution = GeographySanityConstants.DefaultHeightResolution;
            settings.SdfResolution = GeographySanityConstants.DefaultSdfResolution;
            settings.EntitiesPerSector = GeographySanityConstants.DefaultEntitiesPerSector;
            settings.NavigationRequestsPerSector = GeographySanityConstants.DefaultNavigationRequestsPerSector;
            settings.ConnectivityResolution = GeographySanityConstants.DefaultConnectivityResolution;
            settings.MaxFloatingDistance = 0.5f;
            settings.VerticalProbeStepMeters = 1.0f;
            settings.VerticalProbeSteps = 32;
            settings.GlobalQualityWeight = 1.0f;
            settings.WorldSeed = 0x53483247u;
            settings.WorldOriginAup = double3.zero;
            settings.CheckFloating = true;
            settings.CheckBuried = true;
            settings.CheckCrushDepth = true;
            settings.CheckConnectivity = true;
            settings.UseMockDataWhenSectorFilesMissing = true;
            settings.ForceMockData = false;
            settings.SanitizedNonFiniteInput = false;
            return settings;
        }

        public static bool ValidateEntireWorldAsync(GeographySanitySettings settings, Action<float> progress)
        {
            if (_isRunning)
                return false;

            _isRunning = true;
            _cancelRequested = false;
            _ = RunValidationAsync(Sanitize(settings), progress);
            return true;
        }

        public static void Cancel()
        {
            _cancelRequested = true;
        }

        public static GeographySanityMetricsDTO RunMockBenchmark()
        {
            GeographySanitySettings settings = Sanitize(DefaultSettings());
            settings.SectorCountX = 1;
            settings.SectorCountZ = 1;
            settings.EntitiesPerSector = math.max(1024, settings.EntitiesPerSector);
            settings.NavigationRequestsPerSector = math.max(16, settings.NavigationRequestsPerSector);
            settings = Sanitize(settings);
            settings.ForceMockData = true;
            NativeArray<GeographySanityTelemetryEntry> blackBox = default;
            NativeList<SanityProfileDTO> profiles = default;
            StringBuilder anomalies = new StringBuilder(16384);
            try
            {
                EnsureFolders();
                GeographySanityLayoutAssertion.AssertAll();
                profiles = GeographySanityProfileCsv.LoadProfiles(Allocator.TempJob, out _, out _);
                blackBox = AllocateNativeArray<GeographySanityTelemetryEntry>(
                    GeographySanityConstants.BlackBoxFrameCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                InitializeBlackBox(blackBox);
                GeographySanityMetricsDTO metrics = default;
                metrics.SectorCount = 1;
                ApplySettingsWarnings(settings, ref metrics);
                bool first = true;
                Stopwatch total = Stopwatch.StartNew();
                RunSector(settings, 0, 0, profiles.AsArray(), profiles.Length, blackBox, ref metrics, anomalies, ref first);
                metrics.CompletedSectors = 1;
                if (metrics.FatalMathCount > 0)
                    DumpBlackBox(blackBox, GeographySanityConstants.ResultFatalMath);
                total.Stop();
                metrics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                metrics.MockMilliseconds = metrics.BurstMilliseconds;
                metrics.SerializationMilliseconds = WriteReportSync(settings, metrics, anomalies, "mock_benchmark");
                WriteDiagnosticLog(settings, metrics, "mock_benchmark");
                return metrics;
            }
            finally
            {
                if (profiles.IsCreated)
                    profiles.Dispose();
                ReleaseNativeArray(ref blackBox);
            }
        }

        private static async Awaitable RunValidationAsync(GeographySanitySettings settings, Action<float> progress)
        {
            NativeArray<GeographySanityTelemetryEntry> blackBox = default;
            NativeList<SanityProfileDTO> profiles = default;
            StringBuilder sectorAnomalies = new StringBuilder(16384);
            Stopwatch total = Stopwatch.StartNew();
            GeographySanityMetricsDTO metrics = default;
            bool firstAnomaly = true;
            string anomalyTempPath = Path.Combine(ResolveProjectRoot(), AnomalyTempPath);
            try
            {
                EnsureFolders();
                string anomalyTempDirectory = Path.GetDirectoryName(anomalyTempPath);
                if (!string.IsNullOrEmpty(anomalyTempDirectory))
                    Directory.CreateDirectory(anomalyTempDirectory);
                GeographySanityLayoutAssertion.AssertAll();
                profiles = GeographySanityProfileCsv.LoadProfiles(Allocator.Persistent, out int profileRows, out int profileErrors);
                if (profileErrors > 0)
                    Debug.LogWarning("World Sanity Checker profile CSV errors: " + profileErrors + ". Valid rows: " + profileRows);
                blackBox = AllocateNativeArray<GeographySanityTelemetryEntry>(
                    GeographySanityConstants.BlackBoxFrameCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                InitializeBlackBox(blackBox);
                int sectorCount = settings.SectorCountX * settings.SectorCountZ;
                metrics.SectorCount = sectorCount;
                ApplySettingsWarnings(settings, ref metrics);
                int completed = 0;
                using (FileStream anomalyStream = new FileStream(anomalyTempPath, FileMode.Create, FileAccess.Write, FileShare.Read, AsyncWriteChunkBytes, FileOptions.SequentialScan))
                using (StreamWriter anomalyWriter = new StreamWriter(anomalyStream, JsonEncoding))
                {
                    for (int z = 0; z < settings.SectorCountZ; z++)
                    {
                        for (int x = 0; x < settings.SectorCountX; x++)
                        {
                            if (_cancelRequested)
                                break;

                            RunSector(settings, x, z, profiles.AsArray(), profiles.Length, blackBox, ref metrics, sectorAnomalies, ref firstAnomaly);
                            if (sectorAnomalies.Length > 0)
                            {
                                WriteStringBuilder(anomalyWriter, sectorAnomalies);
                                sectorAnomalies.Length = 0;
                            }

                            completed++;
                            metrics.CompletedSectors = completed;
                            float p = completed * math.rcp(math.max(1, sectorCount));
                            progress?.Invoke(p);
                            EditorUtility.DisplayProgressBar(ProgressTitle, ProgressInfo, p);
                            if (metrics.FatalMathCount > 0)
                            {
                                DumpBlackBox(blackBox, GeographySanityConstants.ResultFatalMath);
                                _cancelRequested = true;
                                break;
                            }

                            await Awaitable.NextFrameAsync();
                        }

                        if (_cancelRequested)
                            break;
                    }

                    anomalyWriter.Flush();
                }

                MarkIncompleteSweepIfNeeded(ref metrics);
                total.Stop();
                metrics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                metrics.SerializationMilliseconds = await WriteReportAsync(settings, metrics, anomalyTempPath, "sector_stream");
                WriteDiagnosticLog(settings, metrics, "sector_stream");
                AssetDatabase.Refresh();
                Debug.Log("World Sanity Checker pass ended. Floating=" + metrics.FloatingCount + ", Buried=" + metrics.BuriedCount + ", Crush=" + metrics.CrushDepthCount + ", NavTrap=" + metrics.NavigationTrapCount + ". STATUS: PENDING VERIFICATION.");
            }
            catch (Exception ex)
            {
                if (blackBox.IsCreated)
                    DumpBlackBox(blackBox, GeographySanityConstants.ResultFatalMath);
                MarkPipelineException(settings, total, ref metrics);
                try
                {
                    metrics.SerializationMilliseconds = await WriteReportAsync(settings, metrics, anomalyTempPath, "sector_stream_exception");
                    WriteDiagnosticLog(settings, metrics, "sector_stream_exception");
                    AssetDatabase.Refresh();
                }
                catch (Exception reportEx)
                {
                    Debug.LogException(reportEx);
                }

                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ReleaseNativeArray(ref blackBox);
                if (profiles.IsCreated)
                    profiles.Dispose();
                TryDeleteFile(anomalyTempPath);
                _isRunning = false;
                _cancelRequested = false;
                progress?.Invoke(0f);
            }
        }

        private static unsafe void RunSector(
            GeographySanitySettings settings,
            int sectorX,
            int sectorZ,
            NativeArray<SanityProfileDTO> profiles,
            int profileCount,
            NativeArray<GeographySanityTelemetryEntry> blackBox,
            ref GeographySanityMetricsDTO metrics,
            StringBuilder anomalyRows,
            ref bool firstAnomaly)
        {
            int heightCount = settings.HeightResolution * settings.HeightResolution;
            int sdfResolution = settings.SdfResolution;
            int sdfCount = sdfResolution * sdfResolution * sdfResolution;
            int entityCount = settings.EntitiesPerSector;
            int ruleCount = entityCount;
            int navCount = settings.CheckConnectivity ? settings.NavigationRequestsPerSector : 0;
            int materialCount = 4;
            int effectiveConnectivityResolution = ResolveConnectivityResolution(settings.ConnectivityResolution, settings.GlobalQualityWeight);
            int connectivityCells = effectiveConnectivityResolution * effectiveConnectivityResolution * effectiveConnectivityResolution;
            int scratchIntsPerRequest = math.max(1, connectivityCells * 2);
            NativeArray<float> heights = default;
            NativeArray<float> sdf = default;
            NativeArray<SpatialEntityDTO> entities = default;
            NativeArray<SpatialAnomalyRuleDTO> rules = default;
            NativeArray<NavigationRequestDTO> navRequests = default;
            NativeArray<CrushDepthMaterialDTO> materials = default;
            NativeArray<SpatialAnomalyResultDTO> entityResults = default;
            NativeArray<SpatialAnomalyResultDTO> navResults = default;
            NativeArray<int> connectivityScratch = default;
            try
            {
                heights = new NativeArray<float>(heightCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sdf = new NativeArray<float>(sdfCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                entities = new NativeArray<SpatialEntityDTO>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rules = new NativeArray<SpatialAnomalyRuleDTO>(ruleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                navRequests = new NativeArray<NavigationRequestDTO>(math.max(1, navCount), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                materials = new NativeArray<CrushDepthMaterialDTO>(materialCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                entityResults = new NativeArray<SpatialAnomalyResultDTO>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                navResults = new NativeArray<SpatialAnomalyResultDTO>(math.max(1, navCount), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                connectivityScratch = new NativeArray<int>(math.max(1, navCount * scratchIntsPerRequest), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                FillMaterialTable(materials);

                GeographySectorDTO sector = BuildSector(settings, sectorX, sectorZ);
                long burstStartTicks = Stopwatch.GetTimestamp();
                int workCount = math.max(math.max(heightCount, sdfCount), math.max(entityCount, navCount));
                int batch = ResolveBatchCount(workCount, settings.GlobalQualityWeight);
                float* heightPtr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(heights);
                float* sdfPtr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(sdf);
                SpatialEntityDTO* entityPtr = (SpatialEntityDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(entities);
                SpatialAnomalyRuleDTO* rulePtr = (SpatialAnomalyRuleDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(rules);
                NavigationRequestDTO* navPtr = (NavigationRequestDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(navRequests);
                CrushDepthMaterialDTO* materialPtr = (CrushDepthMaterialDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(materials);
                SanityProfileDTO* profilePtr = profileCount > 0 ? (SanityProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(profiles) : null;
                SpatialAnomalyResultDTO* entityResultPtr = (SpatialAnomalyResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(entityResults);
                SpatialAnomalyResultDTO* navResultPtr = (SpatialAnomalyResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(navResults);
                int* scratchPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(connectivityScratch);

                int activeEntityCount = entityCount;
                int activeNavCount = navCount;
                SectorPayloadLoadStatus payloadStatus = settings.ForceMockData
                    ? SectorPayloadLoadStatus.Missing
                    : TryLoadSectorPayload(
                        settings,
                        sectorX,
                        sectorZ,
                        heights,
                        sdf,
                        entities,
                        rules,
                        navRequests,
                        entityResults,
                        navResults,
                        sector,
                        out activeEntityCount,
                        out activeNavCount);
                if (payloadStatus == SectorPayloadLoadStatus.Invalid)
                {
                    metrics.WarningFlags |= GeographySanityConstants.WarningInvalidSectorPayload;
                    metrics.FatalMathCount++;
                    AppendInvalidSectorPayloadResult(anomalyRows, ref firstAnomaly, sector);
                    WriteBlackBox(blackBox, sector, 0, metrics, (float)ElapsedMillisecondsSince(burstStartTicks));
                    return;
                }

                if (payloadStatus == SectorPayloadLoadStatus.Missing && settings.UseMockDataWhenSectorFilesMissing)
                {
                    activeEntityCount = entityCount;
                    activeNavCount = navCount;
                    metrics.WarningFlags |= GeographySanityConstants.WarningMockFallbackUsed;
                }
                else if (payloadStatus == SectorPayloadLoadStatus.Missing)
                {
                    metrics.WarningFlags |= GeographySanityConstants.WarningMissingSectorPayload;
                    WriteBlackBox(blackBox, sector, 0, metrics, (float)ElapsedMillisecondsSince(burstStartTicks));
                    return;
                }

                JobHandle seed = payloadStatus == SectorPayloadLoadStatus.Loaded
                    ? default
                    : new GenerateMockSpatialAnomaliesJob
                    {
                        HeightSamples = heightPtr,
                        SdfSamples = sdfPtr,
                        Entities = entityPtr,
                        Rules = rulePtr,
                        NavigationRequests = navPtr,
                        EntityResults = entityResultPtr,
                        NavigationResults = navResultPtr,
                        Sector = sector,
                        HeightSampleCount = heightCount,
                        SdfSampleCount = sdfCount,
                        EntityCount = entityCount,
                        RuleCount = ruleCount,
                        NavigationRequestCount = navCount
                    }.Schedule(workCount, batch);

                int scheduledEntityCount = activeEntityCount;
                int scheduledNavCount = activeNavCount;
                int entityBatch = ResolveBatchCount(math.max(1, scheduledEntityCount), settings.GlobalQualityWeight);

                JobHandle profiled = seed;
                if (profileCount > 0 && scheduledEntityCount > 0)
                {
                    profiled = new ApplySanityProfilesJob
                    {
                        Entities = entityPtr,
                        Profiles = profilePtr,
                        Rules = rulePtr,
                        EntityCount = scheduledEntityCount,
                        ProfileCount = profileCount
                    }.Schedule(scheduledEntityCount, entityBatch, seed);
                }

                JobHandle floating = profiled;
                if (settings.CheckFloating && scheduledEntityCount > 0)
                {
                    floating = new EvaluateFloatingAnomaliesJob
                    {
                        Entities = entityPtr,
                        HeightSamples = heightPtr,
                        SdfSamples = sdfPtr,
                        Results = entityResultPtr,
                        Sector = sector,
                        EntityCount = scheduledEntityCount
                    }.Schedule(scheduledEntityCount, entityBatch, profiled);
                }

                JobHandle buried = floating;
                if (settings.CheckBuried && scheduledEntityCount > 0)
                {
                    buried = new EvaluateBuriedAnomaliesJob
                    {
                        Entities = entityPtr,
                        SdfSamples = sdfPtr,
                        Results = entityResultPtr,
                        Sector = sector,
                        EntityCount = scheduledEntityCount
                    }.Schedule(scheduledEntityCount, entityBatch, floating);
                }

                JobHandle crush = buried;
                if (settings.CheckCrushDepth && scheduledEntityCount > 0)
                {
                    crush = new ValidateCrushDepthLimitsJob
                    {
                        Entities = entityPtr,
                        Materials = materialPtr,
                        Results = entityResultPtr,
                        EntityCount = scheduledEntityCount,
                        MaterialCount = materialCount
                    }.Schedule(scheduledEntityCount, entityBatch, buried);
                }

                JobHandle connectivity = seed;
                if (settings.CheckConnectivity && scheduledNavCount > 0)
                {
                    connectivity = new EvaluateNavigationalConnectivityJob
                    {
                        Requests = navPtr,
                        SdfSamples = sdfPtr,
                        Scratch = scratchPtr,
                        Results = navResultPtr,
                        Sector = sector,
                        RequestCount = scheduledNavCount,
                        GridResolution = effectiveConnectivityResolution,
                        ScratchIntsPerRequest = scratchIntsPerRequest
                    }.Schedule(scheduledNavCount, 1, seed);
                }

                JobHandle finalHandle = JobHandle.CombineDependencies(crush, connectivity);
                finalHandle.Complete();
                double burstMilliseconds = ElapsedMillisecondsSince(burstStartTicks);
                metrics.BurstMilliseconds += burstMilliseconds;
                metrics.EntityCount += activeEntityCount;
                metrics.NavigationRequestCount += activeNavCount;
                AppendResults(entityResults, activeEntityCount, anomalyRows, ref firstAnomaly, ref metrics);
                AppendResults(navResults, activeNavCount, anomalyRows, ref firstAnomaly, ref metrics);
                WriteBlackBox(blackBox, sector, activeEntityCount, metrics, (float)burstMilliseconds);
            }
            finally
            {
                if (connectivityScratch.IsCreated) connectivityScratch.Dispose();
                if (navResults.IsCreated) navResults.Dispose();
                if (entityResults.IsCreated) entityResults.Dispose();
                if (materials.IsCreated) materials.Dispose();
                if (navRequests.IsCreated) navRequests.Dispose();
                if (rules.IsCreated) rules.Dispose();
                if (entities.IsCreated) entities.Dispose();
                if (sdf.IsCreated) sdf.Dispose();
                if (heights.IsCreated) heights.Dispose();
            }
        }

        private static GeographySectorDTO BuildSector(GeographySanitySettings settings, int sectorX, int sectorZ)
        {
            GeographySectorDTO sector = default;
            float sectorSize = math.max(1f, settings.SectorSizeMeters);
            sector.SectorOriginAup = settings.WorldOriginAup + new double3(sectorX * (double)sectorSize, 0.0, sectorZ * (double)sectorSize);
            sector.SectorSizeMeters = sectorSize;
            sector.HeightResolution = settings.HeightResolution;
            sector.SdfResolutionX = settings.SdfResolution;
            sector.SdfResolutionY = settings.SdfResolution;
            sector.SdfResolutionZ = settings.SdfResolution;
            sector.SdfVoxelSizeMeters = sectorSize * math.rcp(math.max(1, settings.SdfResolution - 1));
            sector.SdfMinYLocalMeters = -4096f;
            sector.SdfSizeYMeters = 4608f;
            sector.SectorX = sectorX;
            sector.SectorZ = sectorZ;
            sector.MaxFloatingDistance = settings.MaxFloatingDistance;
            sector.VerticalProbeStepMeters = settings.VerticalProbeStepMeters;
            sector.VerticalProbeSteps = ResolveVerticalProbeSteps(settings.VerticalProbeSteps, settings.GlobalQualityWeight);
            sector.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            sector.WorldSeed = settings.WorldSeed ^ (uint)(sectorX * 73856093) ^ (uint)(sectorZ * 19349663);
            sector.Flags = ResolveRuleFlags(settings);
            return sector;
        }

        private static SectorPayloadLoadStatus TryLoadSectorPayload(
            GeographySanitySettings settings,
            int sectorX,
            int sectorZ,
            NativeArray<float> heights,
            NativeArray<float> sdf,
            NativeArray<SpatialEntityDTO> entities,
            NativeArray<SpatialAnomalyRuleDTO> rules,
            NativeArray<NavigationRequestDTO> navRequests,
            NativeArray<SpatialAnomalyResultDTO> entityResults,
            NativeArray<SpatialAnomalyResultDTO> navResults,
            GeographySectorDTO sector,
            out int loadedEntityCount,
            out int loadedNavigationRequestCount)
        {
            loadedEntityCount = 0;
            loadedNavigationRequestCount = 0;
            string fileName = BuildSectorFileName(sectorX, sectorZ);
            string path = Path.Combine(ResolveProjectRoot(), GeographySanityConstants.SectorInputFolder, fileName);
            if (!File.Exists(path))
                return SectorPayloadLoadStatus.Missing;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    uint rawMagic = reader.ReadUInt32();
                    bool swapEndian = rawMagic == ReverseBytes32(GeographySanityConstants.SectorFileMagic);
                    uint magic = swapEndian ? ReverseBytes32(rawMagic) : rawMagic;
                    uint version = ReadUInt32Endian(reader, swapEndian);
                    int heightResolution = ReadInt32Endian(reader, swapEndian);
                    int sdfResolution = ReadInt32Endian(reader, swapEndian);
                    int entityCount = ReadInt32Endian(reader, swapEndian);
                    int navCount = ReadInt32Endian(reader, swapEndian);
                    int maxNavCount = math.max(0, settings.NavigationRequestsPerSector);
                    if (magic != GeographySanityConstants.SectorFileMagic ||
                        version != GeographySanityConstants.SectorFileVersion ||
                        heightResolution != settings.HeightResolution ||
                        sdfResolution != settings.SdfResolution ||
                        entityCount < 0 ||
                        entityCount > entities.Length ||
                        navCount < 0 ||
                        navCount > maxNavCount)
                    {
                        return SectorPayloadLoadStatus.Invalid;
                    }

                    double3 origin;
                    origin.x = ReadDoubleEndian(reader, swapEndian);
                    origin.y = ReadDoubleEndian(reader, swapEndian);
                    origin.z = ReadDoubleEndian(reader, swapEndian);
                    if (!IsSectorOriginCompatible(origin, sector.SectorOriginAup))
                        return SectorPayloadLoadStatus.Invalid;

                    int heightCount = heightResolution * heightResolution;
                    int sdfCount = sdfResolution * sdfResolution * sdfResolution;
                    if (heightCount > heights.Length || sdfCount > sdf.Length)
                        return SectorPayloadLoadStatus.Invalid;

                    for (int i = 0; i < heightCount; i++)
                    {
                        float height = ReadSingleEndian(reader, swapEndian);
                        if (!GeographySanitySampling.IsFinite(height))
                            return SectorPayloadLoadStatus.Invalid;

                        heights[i] = height;
                    }

                    for (int i = 0; i < sdfCount; i++)
                    {
                        float distance = ReadSingleEndian(reader, swapEndian);
                        if (!GeographySanitySampling.IsFinite(distance))
                            return SectorPayloadLoadStatus.Invalid;

                        sdf[i] = distance;
                    }

                    for (int i = 0; i < entityCount; i++)
                    {
                        SpatialEntityDTO entity = default;
                        entity.TargetAUP = new double3(ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian));
                        entity.RadiusMeters = ReadSingleEndian(reader, swapEndian);
                        entity.RequiredClearance = ReadSingleEndian(reader, swapEndian);
                        entity.MaxFloatingDistance = ReadSingleEndian(reader, swapEndian);
                        entity.RecoverableEpsilon = ReadSingleEndian(reader, swapEndian);
                        entity.EntityHash = ReadUInt32Endian(reader, swapEndian);
                        entity.ObjectTypeHash = ReadUInt32Endian(reader, swapEndian);
                        entity.HullMaterialHash = ReadUInt32Endian(reader, swapEndian);
                        entity.RuleFlags = ReadUInt32Endian(reader, swapEndian);
                        entity.SourceFlags = 2u;
                        if (!IsValidEntityPayload(in entity))
                            return SectorPayloadLoadStatus.Invalid;

                        entities[i] = entity;

                        SpatialAnomalyRuleDTO rule = default;
                        rule.TargetAUP = entity.TargetAUP;
                        rule.RequiredClearance = entity.RequiredClearance;
                        rule.RuleFlags = entity.RuleFlags;
                        rules[i] = rule;

                        SpatialAnomalyResultDTO result = default;
                        result.TargetAUP = entity.TargetAUP;
                        result.EntityHash = entity.EntityHash;
                        result.ObjectTypeHash = entity.ObjectTypeHash;
                        result.HullMaterialHash = entity.HullMaterialHash;
                        result.SectorX = sector.SectorX;
                        result.SectorZ = sector.SectorZ;
                        entityResults[i] = result;
                    }

                    for (int i = entityCount; i < entities.Length; i++)
                    {
                        SpatialEntityDTO entity = default;
                        entity.TargetAUP = sector.SectorOriginAup;
                        entity.RuleFlags = 0u;
                        entities[i] = entity;
                        entityResults[i] = default;
                        rules[i] = default;
                    }

                    int storedNavCount = settings.CheckConnectivity ? math.min(navCount, navRequests.Length) : 0;
                    for (int i = 0; i < navCount; i++)
                    {
                        NavigationRequestDTO request = default;
                        request.StartAUP = new double3(ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian));
                        request.EndAUP = new double3(ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian), ReadDoubleEndian(reader, swapEndian));
                        request.VehicleRadiusMeters = ReadSingleEndian(reader, swapEndian);
                        request.RequiredClearance = ReadSingleEndian(reader, swapEndian);
                        request.RequestHash = ReadUInt32Endian(reader, swapEndian);
                        request.RuleFlags = GeographySanityConstants.RuleCheckConnectivity;
                        if (!IsValidNavigationPayload(in request))
                            return SectorPayloadLoadStatus.Invalid;

                        if (i < storedNavCount)
                        {
                            navRequests[i] = request;

                            SpatialAnomalyResultDTO result = default;
                            result.TargetAUP = request.StartAUP;
                            result.RequestHash = request.RequestHash;
                            result.EntityHash = request.RequestHash;
                            result.SectorX = sector.SectorX;
                            result.SectorZ = sector.SectorZ;
                            navResults[i] = result;
                        }
                    }

                    for (int i = storedNavCount; i < navRequests.Length; i++)
                    {
                        navRequests[i] = default;
                        navResults[i] = default;
                    }

                    if (stream.Position != stream.Length)
                        return SectorPayloadLoadStatus.Invalid;

                    loadedEntityCount = entityCount;
                    loadedNavigationRequestCount = storedNavCount;
                    return SectorPayloadLoadStatus.Loaded;
                }
            }
            catch (EndOfStreamException)
            {
                return SectorPayloadLoadStatus.Invalid;
            }
            catch (IOException)
            {
                return SectorPayloadLoadStatus.Invalid;
            }
            catch (UnauthorizedAccessException)
            {
                return SectorPayloadLoadStatus.Invalid;
            }
        }

        private static string BuildSectorFileName(int sectorX, int sectorZ)
        {
            Span<char> buffer = stackalloc char[64];
            int offset = 0;
            AppendFileNameText(buffer, ref offset, "sector_".AsSpan());
            AppendFileNameInt(buffer, ref offset, sectorX);
            AppendFileNameText(buffer, ref offset, "_".AsSpan());
            AppendFileNameInt(buffer, ref offset, sectorZ);
            AppendFileNameText(buffer, ref offset, ".h8bin".AsSpan());
            return new string(buffer.Slice(0, offset));
        }

        private static void AppendFileNameText(Span<char> buffer, ref int offset, ReadOnlySpan<char> value)
        {
            value.CopyTo(buffer.Slice(offset));
            offset += value.Length;
        }

        private static void AppendFileNameInt(Span<char> buffer, ref int offset, int value)
        {
            if (!value.TryFormat(buffer.Slice(offset), out int written, default, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Sector filename buffer is too small.");

            offset += written;
        }

        private static uint ReadUInt32Endian(BinaryReader reader, bool swapEndian)
        {
            uint value = reader.ReadUInt32();
            return swapEndian ? ReverseBytes32(value) : value;
        }

        private static int ReadInt32Endian(BinaryReader reader, bool swapEndian)
        {
            return unchecked((int)ReadUInt32Endian(reader, swapEndian));
        }

        private static ulong ReadUInt64Endian(BinaryReader reader, bool swapEndian)
        {
            ulong value = reader.ReadUInt64();
            return swapEndian ? ReverseBytes64(value) : value;
        }

        private static float ReadSingleEndian(BinaryReader reader, bool swapEndian)
        {
            return math.asfloat(ReadUInt32Endian(reader, swapEndian));
        }

        private static double ReadDoubleEndian(BinaryReader reader, bool swapEndian)
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64Endian(reader, swapEndian)));
        }

        private static bool IsSectorOriginCompatible(double3 payloadOrigin, double3 expectedOrigin)
        {
            const double OriginToleranceMeters = 0.001;
            if (!GeographySanitySampling.IsFinite(payloadOrigin) || !GeographySanitySampling.IsFinite(expectedOrigin))
                return false;

            return math.abs(payloadOrigin.x - expectedOrigin.x) <= OriginToleranceMeters &&
                   math.abs(payloadOrigin.y - expectedOrigin.y) <= OriginToleranceMeters &&
                   math.abs(payloadOrigin.z - expectedOrigin.z) <= OriginToleranceMeters;
        }

        private static bool IsValidEntityPayload(in SpatialEntityDTO entity)
        {
            return GeographySanitySampling.IsFinite(entity.TargetAUP) &&
                IsFinitePositive(entity.RadiusMeters) &&
                IsFiniteNonNegative(entity.RequiredClearance) &&
                IsFiniteNonNegative(entity.MaxFloatingDistance) &&
                IsFiniteNonNegative(entity.RecoverableEpsilon) &&
                IsSupportedEntityRuleMask(entity.RuleFlags);
        }

        private static bool IsValidNavigationPayload(in NavigationRequestDTO request)
        {
            return GeographySanitySampling.IsFinite(request.StartAUP) &&
                GeographySanitySampling.IsFinite(request.EndAUP) &&
                IsFinitePositive(request.VehicleRadiusMeters) &&
                IsFiniteNonNegative(request.RequiredClearance);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return GeographySanitySampling.IsFinite(value) && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return GeographySanitySampling.IsFinite(value) && value > 0f;
        }

        private static bool IsSupportedEntityRuleMask(uint flags)
        {
            const uint supported = GeographySanityConstants.RuleCheckFloating |
                                   GeographySanityConstants.RuleCheckBuried |
                                   GeographySanityConstants.RuleCheckCrushDepth;
            return flags != 0u && (flags & ~supported) == 0u;
        }

        private static uint ReverseBytes32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static ulong ReverseBytes64(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) |
                   ((value & 0x000000000000FF00UL) << 40) |
                   ((value & 0x0000000000FF0000UL) << 24) |
                   ((value & 0x00000000FF000000UL) << 8) |
                   ((value & 0x000000FF00000000UL) >> 8) |
                   ((value & 0x0000FF0000000000UL) >> 24) |
                   ((value & 0x00FF000000000000UL) >> 40) |
                   ((value & 0xFF00000000000000UL) >> 56);
        }

        private static void FillMaterialTable(NativeArray<CrushDepthMaterialDTO> materials)
        {
            CrushDepthMaterialDTO glass = default;
            glass.HullMaterialHash = 0x474C4153u;
            glass.CrushDepthMeters = 600f;
            materials[0] = glass;

            CrushDepthMaterialDTO titanium = default;
            titanium.HullMaterialHash = 0xC0FFEE01u;
            titanium.CrushDepthMeters = 1800f;
            materials[1] = titanium;

            CrushDepthMaterialDTO reinforced = default;
            reinforced.HullMaterialHash = 0x53544545u;
            reinforced.CrushDepthMeters = 3400f;
            materials[2] = reinforced;

            CrushDepthMaterialDTO abyssal = default;
            abyssal.HullMaterialHash = 0x41425953u;
            abyssal.CrushDepthMeters = 6500f;
            materials[3] = abyssal;
        }

        private static void AppendResults(
            NativeArray<SpatialAnomalyResultDTO> results,
            int count,
            StringBuilder builder,
            ref bool first,
            ref GeographySanityMetricsDTO metrics)
        {
            for (int i = 0; i < count; i++)
            {
                SpatialAnomalyResultDTO result = results[i];
                if (result.ErrorFlags == 0u)
                    continue;

                if ((result.ErrorFlags & GeographySanityConstants.ResultFloating) != 0u) metrics.FloatingCount++;
                if ((result.ErrorFlags & GeographySanityConstants.ResultBuried) != 0u) metrics.BuriedCount++;
                if ((result.ErrorFlags & GeographySanityConstants.ResultCrushDepth) != 0u) metrics.CrushDepthCount++;
                if ((result.ErrorFlags & GeographySanityConstants.ResultNavigationTrap) != 0u) metrics.NavigationTrapCount++;
                if ((result.ErrorFlags & GeographySanityConstants.ResultFatalMath) != 0u) metrics.FatalMathCount++;

                if (!first)
                    builder.Append(",\n");
                first = false;
                AppendResultJson(builder, result);
            }
        }

        private static void AppendInvalidSectorPayloadResult(StringBuilder builder, ref bool first, GeographySectorDTO sector)
        {
            SpatialAnomalyResultDTO result = default;
            result.TargetAUP = sector.SectorOriginAup;
            result.ErrorFlags = GeographySanityConstants.ResultFatalMath;
            result.EntityHash = GeographySanityConstants.SectorFileMagic;
            result.ObjectTypeHash = GeographySanityConstants.SectorFileVersion;
            result.SectorX = sector.SectorX;
            result.SectorZ = sector.SectorZ;
            if (!first)
                builder.Append(",\n");
            first = false;
            AppendResultJson(builder, result);
        }

        private static void AppendResultJson(StringBuilder builder, SpatialAnomalyResultDTO result)
        {
            builder.Append("    {\n");
            AppendJson(builder, "type", ResolveType(result.ErrorFlags), 3).Append(",\n");
            builder.Append("      \"flags\": ").Append(result.ErrorFlags).Append(",\n");
            builder.Append("      \"sectorX\": ").Append(result.SectorX).Append(",\n");
            builder.Append("      \"sectorZ\": ").Append(result.SectorZ).Append(",\n");
            builder.Append("      \"entityHash\": ").Append(result.EntityHash).Append(",\n");
            builder.Append("      \"requestHash\": ").Append(result.RequestHash).Append(",\n");
            builder.Append("      \"aup\": { \"x\": ");
            AppendDouble(builder, result.TargetAUP.x).Append(", \"y\": ");
            AppendDouble(builder, result.TargetAUP.y).Append(", \"z\": ");
            AppendDouble(builder, result.TargetAUP.z).Append(" },\n");
            builder.Append("      \"suggestedCorrectionMeters\": { \"x\": ");
            AppendFloat(builder, result.SuggestedCorrectionMeters.x).Append(", \"y\": ");
            AppendFloat(builder, result.SuggestedCorrectionMeters.y).Append(", \"z\": ");
            AppendFloat(builder, result.SuggestedCorrectionMeters.z).Append(" },\n");
            builder.Append("      \"sdfMeters\": ");
            AppendFloat(builder, result.SdfMeters).Append(",\n");
            builder.Append("      \"heightMeters\": ");
            AppendFloat(builder, result.HeightMeters).Append(",\n");
            builder.Append("      \"clearanceMeters\": ");
            AppendFloat(builder, result.ClearanceMeters).Append(",\n");
            builder.Append("      \"actualDepthMeters\": ");
            AppendFloat(builder, result.ActualDepthMeters).Append(",\n");
            builder.Append("      \"crushDepthLimitMeters\": ");
            AppendFloat(builder, result.CrushDepthLimitMeters).Append("\n");
            builder.Append("    }");
        }

        private static async Awaitable<double> WriteReportAsync(
            GeographySanitySettings settings,
            GeographySanityMetricsDTO metrics,
            string anomalyTempPath,
            string mode)
        {
            string absolutePath = Path.Combine(ResolveProjectRoot(), GeographySanityConstants.ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            Exception failure = null;
            double serializationMilliseconds = 0.0;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                serializationMilliseconds = WriteReportBlocking(absolutePath, settings, metrics, anomalyTempPath, mode);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            await Awaitable.MainThreadAsync();
            if (failure != null)
                throw failure;

            return serializationMilliseconds;
        }

        private static double WriteReportBlocking(
            string absolutePath,
            GeographySanitySettings settings,
            GeographySanityMetricsDTO metrics,
            string anomalyTempPath,
            string mode)
        {
            using (FileStream output = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read, AsyncWriteChunkBytes, FileOptions.WriteThrough))
            {
                Stopwatch serialization = Stopwatch.StartNew();
                long serializationPatchOffset = WriteReportHeader(output, settings, metrics, mode);
                if (File.Exists(anomalyTempPath))
                {
                    using (FileStream input = new FileStream(anomalyTempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, AsyncWriteChunkBytes, FileOptions.SequentialScan))
                    {
                        CopyStreamPooled(input, output);
                    }
                }

                WriteUtf8StringPooled(output, "\n  ]\n}\n");
                output.Flush();
                serialization.Stop();
                PatchSerializationMilliseconds(output, serializationPatchOffset, serialization.Elapsed.TotalMilliseconds);
                output.Flush();
                return serialization.Elapsed.TotalMilliseconds;
            }
        }

        private static double WriteReportSync(
            GeographySanitySettings settings,
            GeographySanityMetricsDTO metrics,
            StringBuilder anomalyRows,
            string mode)
        {
            string absolutePath = Path.Combine(ResolveProjectRoot(), GeographySanityConstants.ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream output = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read, AsyncWriteChunkBytes, FileOptions.WriteThrough))
            {
                Stopwatch serialization = Stopwatch.StartNew();
                long serializationPatchOffset = WriteReportHeader(output, settings, metrics, mode);
                WriteStringBuilderUtf8Pooled(output, anomalyRows);
                WriteUtf8StringPooled(output, "\n  ]\n}\n");
                output.Flush();
                serialization.Stop();
                PatchSerializationMilliseconds(output, serializationPatchOffset, serialization.Elapsed.TotalMilliseconds);
                output.Flush();
                return serialization.Elapsed.TotalMilliseconds;
            }
        }

        private static long WriteReportHeader(
            FileStream output,
            GeographySanitySettings settings,
            GeographySanityMetricsDTO metrics,
            string mode)
        {
            string header = BuildReportHeader(settings, metrics, mode, out int serializationPatchCharOffset);
            long serializationPatchOffset = output.Position + serializationPatchCharOffset;
            WriteUtf8StringPooled(output, header);
            return serializationPatchOffset;
        }

        private static string BuildReportHeader(GeographySanitySettings settings, GeographySanityMetricsDTO metrics, string mode, out int serializationPatchCharOffset)
        {
            StringBuilder builder = new StringBuilder(4096);
            serializationPatchCharOffset = -1;
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.geography_sanity_report.v1", 1).Append(",\n");
            AppendJson(builder, "agent", GeographySanityConstants.AgentId, 1).Append(",\n");
            AppendJson(builder, "mode", mode, 1).Append(",\n");
            AppendJson(builder, "status", metrics.FatalMathCount > 0 ? "FATAL_MATH_ERROR" : "PENDING_VERIFICATION", 1).Append(",\n");
            AppendJson(builder, "proofGrade", ResolveProofGrade(settings, metrics), 1).Append(",\n");
            builder.Append("  \"certificationEligible\": ").Append(IsCertificationEligible(settings, metrics) ? "true" : "false").Append(",\n");
            builder.Append("  \"sectorCount\": ").Append(metrics.SectorCount).Append(",\n");
            builder.Append("  \"completedSectors\": ").Append(metrics.CompletedSectors).Append(",\n");
            builder.Append("  \"entityCount\": ").Append(metrics.EntityCount).Append(",\n");
            builder.Append("  \"navigationRequestCount\": ").Append(metrics.NavigationRequestCount).Append(",\n");
            builder.Append("  \"floatingCount\": ").Append(metrics.FloatingCount).Append(",\n");
            builder.Append("  \"buriedCount\": ").Append(metrics.BuriedCount).Append(",\n");
            builder.Append("  \"crushDepthCount\": ").Append(metrics.CrushDepthCount).Append(",\n");
            builder.Append("  \"navigationTrapCount\": ").Append(metrics.NavigationTrapCount).Append(",\n");
            builder.Append("  \"fatalMathCount\": ").Append(metrics.FatalMathCount).Append(",\n");
            builder.Append("  \"warningFlags\": ").Append(metrics.WarningFlags).Append(",\n");
            builder.Append("  \"burstMilliseconds\": ");
            AppendDouble(builder, metrics.BurstMilliseconds).Append(",\n");
            builder.Append("  \"serializationMilliseconds\": ");
            serializationPatchCharOffset = builder.Length;
            AppendSerializationPlaceholder(builder).Append(",\n");
            builder.Append("  \"totalMilliseconds\": ");
            AppendDouble(builder, metrics.TotalMilliseconds).Append(",\n");
            builder.Append("  \"settings\": {\n");
            builder.Append("    \"sectorSizeMeters\": ");
            AppendFloat(builder, settings.SectorSizeMeters).Append(",\n");
            builder.Append("    \"heightResolution\": ").Append(settings.HeightResolution).Append(",\n");
            builder.Append("    \"sdfResolution\": ").Append(settings.SdfResolution).Append(",\n");
            builder.Append("    \"connectivityResolution\": ").Append(settings.ConnectivityResolution).Append(",\n");
            builder.Append("    \"verticalProbeSteps\": ").Append(settings.VerticalProbeSteps).Append(",\n");
            builder.Append("    \"effectiveConnectivityResolution\": ").Append(ResolveConnectivityResolution(settings.ConnectivityResolution, settings.GlobalQualityWeight)).Append(",\n");
            builder.Append("    \"effectiveVerticalProbeSteps\": ").Append(ResolveVerticalProbeSteps(settings.VerticalProbeSteps, settings.GlobalQualityWeight)).Append(",\n");
            builder.Append("    \"globalQualityWeight\": ");
            AppendFloat(builder, settings.GlobalQualityWeight).Append("\n");
            builder.Append("  },\n");
            builder.Append("  \"rollbackNetcodeExcluded\": true,\n");
            builder.Append("  \"runtimeAuthorityMutation\": false,\n");
            builder.Append("  \"anomalies\": [\n");
            return builder.ToString();
        }

        private static StringBuilder AppendSerializationPlaceholder(StringBuilder builder)
        {
            builder.Append('0');
            for (int i = 1; i < SerializationPatchWidth; i++)
                builder.Append(' ');
            return builder;
        }

        private static void PatchSerializationMilliseconds(FileStream output, long offset, double value)
        {
            Span<char> text = stackalloc char[SerializationPatchWidth];
            int textLength = 1;
            text[0] = '0';
            if (!double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value.TryFormat(text, out int written, "R", CultureInfo.InvariantCulture) &&
                written <= SerializationPatchWidth)
            {
                textLength = written;
            }

            byte[] bytes = ArrayPool<byte>.Shared.Rent(SerializationPatchWidth);
            try
            {
                for (int i = 0; i < SerializationPatchWidth; i++)
                    bytes[i] = 32;

                for (int i = 0; i < textLength; i++)
                    bytes[i] = (byte)text[i];

                output.Position = offset;
                output.Write(bytes, 0, SerializationPatchWidth);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private static void WriteStringBuilder(StreamWriter writer, StringBuilder builder)
        {
            int length = builder.Length;
            if (length <= 0)
                return;

            char[] chunk = ArrayPool<char>.Shared.Rent(math.min(ReportCharChunk, length));
            try
            {
                int offset = 0;
                while (offset < length)
                {
                    int count = math.min(chunk.Length, length - offset);
                    builder.CopyTo(offset, chunk, 0, count);
                    writer.Write(chunk, 0, count);
                    offset += count;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chunk);
            }
        }

        private static void WriteStringBuilderUtf8Pooled(FileStream output, StringBuilder builder)
        {
            int length = builder.Length;
            if (length <= 0)
                return;

            char[] chars = ArrayPool<char>.Shared.Rent(math.min(ReportCharChunk, length));
            byte[] bytes = ArrayPool<byte>.Shared.Rent(JsonEncoding.GetMaxByteCount(chars.Length));
            try
            {
                int offset = 0;
                while (offset < length)
                {
                    int count = math.min(chars.Length, length - offset);
                    builder.CopyTo(offset, chars, 0, count);
                    int byteCount = JsonEncoding.GetBytes(chars, 0, count, bytes, 0);
                    output.Write(bytes, 0, byteCount);
                    offset += count;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
                ArrayPool<char>.Shared.Return(chars);
            }
        }

        private static void CopyStreamPooled(FileStream input, FileStream output)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AsyncWriteChunkBytes);
            try
            {
                while (true)
                {
                    int read = input.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        break;

                    output.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void WriteUtf8StringPooled(FileStream stream, string content)
        {
            int byteCount = JsonEncoding.GetByteCount(content);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(math.max(1, byteCount));
            try
            {
                int written = JsonEncoding.GetBytes(content, 0, content.Length, bytes, 0);
                stream.Write(bytes, 0, written);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private static void WriteDiagnosticLog(GeographySanitySettings settings, GeographySanityMetricsDTO metrics, string mode)
        {
            string projectRoot = ResolveProjectRoot();
            string path = Path.Combine(projectRoot, GeographySanityConstants.DiagnosticLogPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(1024);
            AppendDiagnosticValue(builder, "agent", GeographySanityConstants.AgentId);
            AppendDiagnosticValue(builder, "mode", mode);
            AppendDiagnosticValue(builder, "status", metrics.FatalMathCount > 0 ? "FATAL_MATH_ERROR" : "PENDING_VERIFICATION");
            AppendDiagnosticValue(builder, "proofGrade", ResolveProofGrade(settings, metrics));
            AppendDiagnosticValue(builder, "certificationEligible", IsCertificationEligible(settings, metrics) ? "true" : "false");
            builder.Append("sectors=").Append(metrics.CompletedSectors).Append('/').Append(metrics.SectorCount).Append('\n');
            AppendDiagnosticValue(builder, "entities", metrics.EntityCount);
            AppendDiagnosticValue(builder, "navRequests", metrics.NavigationRequestCount);
            AppendDiagnosticValue(builder, "floating", metrics.FloatingCount);
            AppendDiagnosticValue(builder, "buried", metrics.BuriedCount);
            AppendDiagnosticValue(builder, "crushDepth", metrics.CrushDepthCount);
            AppendDiagnosticValue(builder, "navigationTraps", metrics.NavigationTrapCount);
            AppendDiagnosticValue(builder, "fatalMath", metrics.FatalMathCount);
            AppendDiagnosticValue(builder, "warningFlags", metrics.WarningFlags);
            AppendDiagnosticValue(builder, "burstMs", metrics.BurstMilliseconds);
            AppendDiagnosticValue(builder, "serializationMs", metrics.SerializationMilliseconds);
            AppendDiagnosticValue(builder, "configuredConnectivityResolution", settings.ConnectivityResolution);
            AppendDiagnosticValue(builder, "effectiveConnectivityResolution", ResolveConnectivityResolution(settings.ConnectivityResolution, settings.GlobalQualityWeight));
            AppendDiagnosticValue(builder, "configuredVerticalProbeSteps", settings.VerticalProbeSteps);
            AppendDiagnosticValue(builder, "effectiveVerticalProbeSteps", ResolveVerticalProbeSteps(settings.VerticalProbeSteps, settings.GlobalQualityWeight));
            AppendDiagnosticValue(builder, "globalQualityWeight", settings.GlobalQualityWeight);
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendDiagnosticValue(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        private static void AppendDiagnosticValue(StringBuilder builder, string key, int value)
        {
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        private static void AppendDiagnosticValue(StringBuilder builder, string key, uint value)
        {
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        private static void AppendDiagnosticValue(StringBuilder builder, string key, float value)
        {
            builder.Append(key).Append('=');
            AppendFloat(builder, value).Append('\n');
        }

        private static void AppendDiagnosticValue(StringBuilder builder, string key, double value)
        {
            builder.Append(key).Append('=');
            AppendDouble(builder, value).Append('\n');
        }

        private static unsafe void WriteBlackBox(
            NativeArray<GeographySanityTelemetryEntry> blackBox,
            GeographySectorDTO sector,
            int entityCount,
            GeographySanityMetricsDTO metrics,
            float stageMs)
        {
            if (!blackBox.IsCreated || blackBox.Length == 0)
                return;

            int index = math.abs(metrics.CompletedSectors) % blackBox.Length;
            GeographySanityTelemetryEntry entry = default;
            entry.SectorAup = sector.SectorOriginAup;
            entry.Frame = (uint)math.max(1, metrics.CompletedSectors + 1);
            entry.Stage = 1u;
            entry.StateHash = sector.WorldSeed;
            entry.ErrorFlags = metrics.FatalMathCount > 0 ? GeographySanityConstants.ResultFatalMath : 0u;
            entry.SectorX = sector.SectorX;
            entry.SectorZ = sector.SectorZ;
            entry.EntityCount = entityCount;
            entry.ErrorCount = metrics.FloatingCount + metrics.BuriedCount + metrics.CrushDepthCount + metrics.NavigationTrapCount;
            entry.StageMilliseconds = stageMs;
            entry.DumpReason = 0u;
            blackBox[index] = entry;
        }

        private static double ElapsedMillisecondsSince(long startTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            return elapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        private static void InitializeBlackBox(NativeArray<GeographySanityTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated)
                return;

            for (int i = 0; i < blackBox.Length; i++)
                blackBox[i] = default;
        }

        private static NativeArray<T> AllocateNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("World Sanity Checker native allocation failed.");

            return array;
        }

        private static void ReleaseNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
        }

        private static unsafe void DumpBlackBox(NativeArray<GeographySanityTelemetryEntry> blackBox, uint reason)
        {
            if (!blackBox.IsCreated)
                return;

            string path = Path.Combine(ResolveProjectRoot(), GeographySanityConstants.DumpPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                GeographySanityDumpHeaderDTO header = default;
                header.Magic = GeographySanityConstants.DumpMagic;
                header.EntryCount = (uint)blackBox.Length;
                header.EntrySize = (uint)UnsafeUtility.SizeOf<GeographySanityTelemetryEntry>();
                header.Cursor = ComputeBlackBoxCursor(blackBox);
                header.Reason = reason;
                WriteDumpHeader(stream, header);
                for (int i = 0; i < blackBox.Length; i++)
                {
                    GeographySanityTelemetryEntry entry = blackBox[i];
                    WriteTelemetryEntry(stream, entry);
                }
            }
        }

        private static uint ComputeBlackBoxCursor(NativeArray<GeographySanityTelemetryEntry> blackBox)
        {
            uint maxFrame = 0u;
            for (int i = 0; i < blackBox.Length; i++)
            {
                uint frame = blackBox[i].Frame;
                if (frame > maxFrame)
                    maxFrame = frame;
            }

            return blackBox.Length > 0 ? maxFrame % (uint)blackBox.Length : 0u;
        }

        private static void WriteDumpHeader(FileStream stream, GeographySanityDumpHeaderDTO header)
        {
            Span<byte> bytes = stackalloc byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(0, 4), header.Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(4, 4), header.EntryCount);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(8, 4), header.EntrySize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(12, 4), header.Cursor);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(16, 4), header.Reason);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(20, 4), 0u);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(24, 8), 0UL);
            stream.Write(bytes);
        }

        private static void WriteTelemetryEntry(FileStream stream, GeographySanityTelemetryEntry entry)
        {
            Span<byte> bytes = stackalloc byte[64];
            WriteDoubleLittleEndian(bytes.Slice(0, 8), entry.SectorAup.x);
            WriteDoubleLittleEndian(bytes.Slice(8, 8), entry.SectorAup.y);
            WriteDoubleLittleEndian(bytes.Slice(16, 8), entry.SectorAup.z);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(24, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(28, 4), entry.Stage);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(32, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(36, 4), entry.ErrorFlags);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(40, 4), entry.SectorX);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(44, 4), entry.SectorZ);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(48, 4), entry.EntityCount);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(52, 4), entry.ErrorCount);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(56, 4), math.asuint(entry.StageMilliseconds));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(60, 4), entry.DumpReason);
            stream.Write(bytes);
        }

        private static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        private static GeographySanitySettings Sanitize(GeographySanitySettings settings)
        {
            bool sanitized = settings.SanitizedNonFiniteInput;
            settings.SectorSizeMeters = RequireFinite(settings.SectorSizeMeters, GeographySanityConstants.DefaultSectorSizeMeters, ref sanitized);
            settings.MaxFloatingDistance = RequireFinite(settings.MaxFloatingDistance, 0.5f, ref sanitized);
            settings.VerticalProbeStepMeters = RequireFinite(settings.VerticalProbeStepMeters, 1.0f, ref sanitized);
            settings.GlobalQualityWeight = RequireFinite(settings.GlobalQualityWeight, 0f, ref sanitized);
            settings.WorldOriginAup = RequireFiniteAup(settings.WorldOriginAup, double3.zero, ref sanitized);
            settings.SectorSizeMeters = math.max(1f, settings.SectorSizeMeters);
            settings.SectorCountX = math.clamp(settings.SectorCountX, 1, GeographySanityConstants.MaximumSectorCountAxis);
            settings.SectorCountZ = math.clamp(settings.SectorCountZ, 1, GeographySanityConstants.MaximumSectorCountAxis);
            settings.HeightResolution = math.clamp(settings.HeightResolution, 2, GeographySanityConstants.MaximumHeightResolution);
            settings.SdfResolution = math.clamp(settings.SdfResolution, 4, GeographySanityConstants.MaximumSdfResolution);
            settings.EntitiesPerSector = math.clamp(settings.EntitiesPerSector, 1, GeographySanityConstants.MaximumEntitiesPerSector);
            settings.NavigationRequestsPerSector = math.clamp(settings.NavigationRequestsPerSector, 0, GeographySanityConstants.MaximumNavigationRequestsPerSector);
            settings.ConnectivityResolution = math.clamp(settings.ConnectivityResolution, 4, GeographySanityConstants.MaximumConnectivityResolution);
            settings.MaxFloatingDistance = math.max(0.01f, settings.MaxFloatingDistance);
            settings.VerticalProbeStepMeters = math.max(0.05f, settings.VerticalProbeStepMeters);
            settings.VerticalProbeSteps = math.clamp(settings.VerticalProbeSteps, 1, GeographySanityConstants.MaximumVerticalProbeSteps);
            settings.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            settings.SanitizedNonFiniteInput = sanitized;
            return settings;
        }

        private static float RequireFinite(float value, float fallback, ref bool sanitized)
        {
            if (GeographySanitySampling.IsFinite(value))
                return value;

            sanitized = true;
            return fallback;
        }

        private static double3 RequireFiniteAup(double3 value, double3 fallback, ref bool sanitized)
        {
            if (GeographySanitySampling.IsFinite(value))
                return value;

            sanitized = true;
            return fallback;
        }

        private static void ApplySettingsWarnings(GeographySanitySettings settings, ref GeographySanityMetricsDTO metrics)
        {
            if (!IsCertificationQuality(settings.GlobalQualityWeight))
                metrics.WarningFlags |= GeographySanityConstants.WarningReducedQualityTriage;
            if (!HasFullCheckMask(settings))
                metrics.WarningFlags |= GeographySanityConstants.WarningPartialCheckMask;
            if (settings.SanitizedNonFiniteInput)
                metrics.WarningFlags |= GeographySanityConstants.WarningSanitizedSettings;
        }

        private static void MarkIncompleteSweepIfNeeded(ref GeographySanityMetricsDTO metrics)
        {
            if (metrics.SectorCount > 0 && metrics.CompletedSectors < metrics.SectorCount)
                metrics.WarningFlags |= GeographySanityConstants.WarningIncompleteSweep;
        }

        private static void MarkPipelineException(GeographySanitySettings settings, Stopwatch total, ref GeographySanityMetricsDTO metrics)
        {
            if (metrics.SectorCount <= 0)
            {
                metrics.SectorCount = math.max(1, settings.SectorCountX * settings.SectorCountZ);
                ApplySettingsWarnings(settings, ref metrics);
            }

            if (total.IsRunning)
                total.Stop();

            metrics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            metrics.FatalMathCount = math.max(1, metrics.FatalMathCount);
            metrics.WarningFlags |= GeographySanityConstants.WarningPipelineException;
            MarkIncompleteSweepIfNeeded(ref metrics);
        }

        private static bool IsCertificationQuality(float globalQualityWeight)
        {
            return globalQualityWeight >= GeographySanityConstants.CertificationQualityWeight;
        }

        private static bool HasFullCheckMask(GeographySanitySettings settings)
        {
            return settings.CheckFloating &&
                settings.CheckBuried &&
                settings.CheckCrushDepth &&
                settings.CheckConnectivity;
        }

        private static bool IsCertificationEligible(GeographySanitySettings settings, GeographySanityMetricsDTO metrics)
        {
            const uint blockerWarnings =
                GeographySanityConstants.WarningMissingSectorPayload |
                GeographySanityConstants.WarningInvalidSectorPayload |
                GeographySanityConstants.WarningReducedQualityTriage |
                GeographySanityConstants.WarningPartialCheckMask |
                GeographySanityConstants.WarningMockFallbackUsed |
                GeographySanityConstants.WarningIncompleteSweep |
                GeographySanityConstants.WarningPipelineException |
                GeographySanityConstants.WarningSanitizedSettings;
            return metrics.FatalMathCount == 0 &&
                metrics.SectorCount > 0 &&
                metrics.CompletedSectors == metrics.SectorCount &&
                IsCertificationQuality(settings.GlobalQualityWeight) &&
                HasFullCheckMask(settings) &&
                (metrics.WarningFlags & blockerWarnings) == 0u;
        }

        private static string ResolveProofGrade(GeographySanitySettings settings, GeographySanityMetricsDTO metrics)
        {
            if (metrics.FatalMathCount > 0)
                return "FATAL_INPUT";
            if ((metrics.WarningFlags & GeographySanityConstants.WarningSanitizedSettings) != 0u)
                return "INVALID_SETTINGS";
            if ((metrics.WarningFlags & GeographySanityConstants.WarningInvalidSectorPayload) != 0u)
                return "INVALID_MASTER_DATA";
            if ((metrics.WarningFlags & GeographySanityConstants.WarningMissingSectorPayload) != 0u)
                return "INCOMPLETE_MISSING_INPUT";
            if ((metrics.WarningFlags & GeographySanityConstants.WarningIncompleteSweep) != 0u ||
                metrics.CompletedSectors < metrics.SectorCount)
                return "INCOMPLETE_SWEEP";
            if ((metrics.WarningFlags & GeographySanityConstants.WarningMockFallbackUsed) != 0u)
                return "TRIAGE_MOCK_DATA";
            if (!IsCertificationQuality(settings.GlobalQualityWeight) ||
                (metrics.WarningFlags & GeographySanityConstants.WarningReducedQualityTriage) != 0u)
                return "TRIAGE_REDUCED_QUALITY";
            if (!HasFullCheckMask(settings) ||
                (metrics.WarningFlags & GeographySanityConstants.WarningPartialCheckMask) != 0u)
                return "TRIAGE_PARTIAL_CHECKS";
            return "CERTIFICATION_CANDIDATE_STATIC_SOURCE";
        }

        private static int ResolveBatchCount(int count, float globalQualityWeight)
        {
            float w = math.saturate(globalQualityWeight);
            int batch = (int)math.round(math.lerp(16f, 128f, w));
            return math.clamp(batch, 1, math.max(1, count));
        }

        private static int ResolveConnectivityResolution(int configuredResolution, float globalQualityWeight)
        {
            float w = math.smoothstep(0.2f, 0.95f, math.saturate(globalQualityWeight));
            int resolved = (int)math.round(math.lerp(4f, configuredResolution, w));
            return math.clamp(resolved, 4, configuredResolution);
        }

        private static int ResolveVerticalProbeSteps(int configuredSteps, float globalQualityWeight)
        {
            float w = math.smoothstep(0.2f, 0.95f, math.saturate(globalQualityWeight));
            int resolved = (int)math.round(math.lerp(1f, configuredSteps, w));
            return math.clamp(resolved, 1, configuredSteps);
        }

        private static uint ResolveRuleFlags(GeographySanitySettings settings)
        {
            uint flags = 0u;
            if (settings.CheckFloating) flags |= GeographySanityConstants.RuleCheckFloating;
            if (settings.CheckBuried) flags |= GeographySanityConstants.RuleCheckBuried;
            if (settings.CheckCrushDepth) flags |= GeographySanityConstants.RuleCheckCrushDepth;
            if (settings.CheckConnectivity) flags |= GeographySanityConstants.RuleCheckConnectivity;
            return flags;
        }

        private static string ResolveType(uint flags)
        {
            if ((flags & GeographySanityConstants.ResultFatalMath) != 0u) return "FATAL_MATH_ERROR";
            if ((flags & GeographySanityConstants.ResultNavigationTrap) != 0u) return "Navigational Trap";
            if ((flags & GeographySanityConstants.ResultCrushDepth) != 0u) return "Crush Depth Violation";
            if ((flags & GeographySanityConstants.ResultBuried) != 0u) return "Buried";
            if ((flags & GeographySanityConstants.ResultFloating) != 0u) return "Floating";
            return "Unknown";
        }

        private static StringBuilder AppendJson(StringBuilder builder, string key, string value, int indent)
        {
            builder.Append(' ', indent * 2);
            AppendJsonString(builder, key).Append(": ");
            AppendJsonString(builder, value);
            return builder;
        }

        private static StringBuilder AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '\\' || c == '"')
                        builder.Append('\\').Append(c);
                    else if (c == '\n' || c == '\r' || c == '\t')
                        builder.Append(' ');
                    else if (c < 32 || c > 126)
                        AppendUnicodeEscape(builder, c);
                    else
                        builder.Append(c);
                }
            }

            builder.Append('"');
            return builder;
        }

        private static StringBuilder AppendUnicodeEscape(StringBuilder builder, char value)
        {
            builder.Append("\\u");
            int scalar = value;
            builder.Append(ToHexNibble((scalar >> 12) & 0xF));
            builder.Append(ToHexNibble((scalar >> 8) & 0xF));
            builder.Append(ToHexNibble((scalar >> 4) & 0xF));
            builder.Append(ToHexNibble(scalar & 0xF));
            return builder;
        }

        private static char ToHexNibble(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        private static StringBuilder AppendFloat(StringBuilder builder, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return builder.Append("null");

            Span<char> buffer = stackalloc char[32];
            return value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture)
                ? AppendChars(builder, buffer.Slice(0, written))
                : builder.Append("null");
        }

        private static StringBuilder AppendDouble(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return builder.Append("null");

            Span<char> buffer = stackalloc char[32];
            return value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture)
                ? AppendChars(builder, buffer.Slice(0, written))
                : builder.Append("null");
        }

        private static StringBuilder AppendChars(StringBuilder builder, ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
                builder.Append(value[i]);

            return builder;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.Combine(ResolveProjectRoot(), "Docs", "Reports"));
            Directory.CreateDirectory(Path.Combine(ResolveProjectRoot(), "Docs", "AgentLogs"));
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

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }
    }
}
#endif

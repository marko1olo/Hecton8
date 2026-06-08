using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only smoke tester for hydraulic erosion native lifetime, deterministic bounds, and edge-case terrain inputs.
    /// </summary>
    public static class HydraulicErosionSmokeTester
    {
        private const int BlockSize = 256;
        private const int ScenarioCount = 4;
        private const string OutputFolder = "CodexArtifacts";
        private const string OutputFile = "HydraulicErosionSmokeTester.json";
        private const string NativeMemoryOwner = nameof(HydraulicErosionSmokeTester);
        private const string BeforeLabel = "before";
        private const string HeightALabel = "heightA";
        private const string HeightBLabel = "heightB";
        private const string SedimentLabel = "sediment";
        private const string WearLabel = "wear";
        private const string MetricBlocksLabel = "metricBlocks";
        private const string MetricSummaryLabel = "metricSummary";
        private const uint SmokeFailureWarningHash = 0x48594553u;
        private const uint NativeLeakContextHash = 0x48594E4Cu;
        private const int ErosionSubGridSize = 32;
        private const float ErosionHeightScaleMeters = 160f;
        private const float ErosionInertia = 0.86f;
        private const float ErosionChannelSpawnBias = 24f;
        private const float ErosionChannelFlowBias = 2.75f;
        private const float SedimentaryFlatSlopeDegrees = 2f;
        private const float SedimentaryFlatSmoothingStrength = 0.95f;
        private const float SedimentaryFlatSedimentThreshold = 0.00001f;
        private const float CanyonDepthThreshold = 0.0002f;
        private const float CanyonWallStrength = 4f;
        private const float CanyonMaxLift01 = 0.02f;

        private struct ScenarioConfig
        {
            public string Name;
            public int Resolution;
            public int Droplets;
            public int Lifetime;
            public int Margin;
            public int SlumpIterations;
            public float SlumpStrength;
            public float TalusAngle;
            public uint Seed;
        }

        private struct ScenarioResult
        {
            public string Name;
            public bool Passed;
            public int Resolution;
            public int Droplets;
            public int Lifetime;
            public int SlumpIterations;
            public int NanCount;
            public int SentinelDelta;
            public long TrackedByteDelta;
            public float MinHeight;
            public float MaxHeight;
            public float MeanHeight;
            public float SumSediment;
            public float SumWear;
            public float MaxSediment;
            public float MaxWear;
            public float MaxBoundaryHeightDelta;
            public float MaxBoundarySediment;
            public float MaxBoundaryWear;
            public float Milliseconds;
            public int BoundarySampleCount;
            public int BoundaryNanCount;
        }

        /// <summary>
        /// Runs all smoke scenarios and writes a JSON artifact under CodexArtifacts.
        /// </summary>
        [MenuItem("Tools/Hecton/Dev/Terrain/Run Hydraulic Erosion Smoke Tester")]
        public static void RunMenu()
        {
            string path = RunAndWriteJson();
            Debug.Log("[HydraulicErosionSmokeTester] Wrote JSON artifact to " + path);
        }

        /// <summary>
        /// Executes smoke scenarios and returns the JSON artifact path.
        /// </summary>
        public static string RunAndWriteJson()
        {
            // COLD ALLOC: ScenarioResult[4] - editor smoke result staging - owner: HydraulicErosionSmokeTester
            ScenarioResult[] results = new ScenarioResult[ScenarioCount];
            int passCount = 0;

            for (int i = 0; i < ScenarioCount; i++)
            {
                results[i] = RunScenario(GetScenarioConfig(i));
                if (results[i].Passed)
                    passCount++;
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, OutputFile);
            File.WriteAllText(path, BuildJson(results, passCount), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return path;
        }

        private static ScenarioConfig GetScenarioConfig(int index)
        {
            switch (index)
            {
                case 0:
                    return new ScenarioConfig
                    {
                        Name = "dry_zero_power",
                        Resolution = 8,
                        Droplets = 0,
                        Lifetime = 1,
                        Margin = 1,
                        SlumpIterations = 0,
                        SlumpStrength = 0f,
                        TalusAngle = 45f,
                        Seed = 0xA110CA7Eu
                    };
                case 1:
                    return new ScenarioConfig
                    {
                        Name = "tiny_margin_clamp",
                        Resolution = 16,
                        Droplets = 128,
                        Lifetime = 16,
                        Margin = 4,
                        SlumpIterations = 1,
                        SlumpStrength = 0.32f,
                        TalusAngle = 45f,
                        Seed = 0xBADC0DEu
                    };
                case 2:
                    return new ScenarioConfig
                    {
                        Name = "draft_tile",
                        Resolution = 64,
                        Droplets = 4096,
                        Lifetime = 32,
                        Margin = 4,
                        SlumpIterations = 2,
                        SlumpStrength = 0.32f,
                        TalusAngle = 45f,
                        Seed = 0xC001CAFEu
                    };
                default:
                    return new ScenarioConfig
                    {
                        Name = "thermal_stress",
                        Resolution = 96,
                        Droplets = 2048,
                        Lifetime = 24,
                        Margin = 4,
                        SlumpIterations = 4,
                        SlumpStrength = 0.75f,
                        TalusAngle = 25f,
                        Seed = 0xE80510A5u
                    };
            }
        }

        private static ScenarioResult RunScenario(in ScenarioConfig config)
        {
            int pixelCount = config.Resolution * config.Resolution;
            int blockCount = (pixelCount + BlockSize - 1) / BlockSize;
            int sentinelBefore = NativeMemorySentinel.ActiveAllocationCount;
            long trackedBytesBefore = NativeMemorySentinel.TrackedBytes;
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            NativeArray<float> before = default;
            NativeArray<float> heightA = default;
            NativeArray<float> heightB = default;
            NativeArray<float> sediment = default;
            NativeArray<float> wear = default;
            NativeArray<HydraulicErosionMetricBlock> metricBlocks = default;
            NativeArray<HydraulicErosionMetricBlock> metricSummary = default;
            JobHandle handle = default;
            bool handleScheduled = false;
            var result = new ScenarioResult
            {
                Name = config.Name,
                Resolution = config.Resolution,
                Droplets = config.Droplets,
                Lifetime = config.Lifetime,
                SlumpIterations = config.SlumpIterations,
                MinHeight = 1f
            };

            try
            {
                before = AllocateTrackedTempJobArray<float>(pixelCount, NativeArrayOptions.UninitializedMemory, BeforeLabel);
                heightA = AllocateTrackedTempJobArray<float>(pixelCount, NativeArrayOptions.UninitializedMemory, HeightALabel);
                heightB = AllocateTrackedTempJobArray<float>(pixelCount, NativeArrayOptions.UninitializedMemory, HeightBLabel);
                sediment = AllocateTrackedTempJobArray<float>(pixelCount, NativeArrayOptions.ClearMemory, SedimentLabel);
                wear = AllocateTrackedTempJobArray<float>(pixelCount, NativeArrayOptions.ClearMemory, WearLabel);
                metricBlocks = AllocateTrackedTempJobArray<HydraulicErosionMetricBlock>(blockCount, NativeArrayOptions.UninitializedMemory, MetricBlocksLabel);
                metricSummary = AllocateTrackedTempJobArray<HydraulicErosionMetricBlock>(1, NativeArrayOptions.UninitializedMemory, MetricSummaryLabel);

                handle = new ErosionFractalHeightmapJob
                {
                    Before = before,
                    Height = heightA,
                    Resolution = config.Resolution,
                    PrimarySeed = config.Seed,
                    RidgeSeed = config.Seed ^ 0x9E3779B9u
                }.Schedule(pixelCount, 64);
                handleScheduled = true;

                handle = ScheduleErosion(config, heightA, heightB, sediment, wear, handle, out NativeArray<float> current);
                handle = new HydraulicErosionMetricsJob
                {
                    Heightmap = current,
                    SedimentMask = sediment,
                    WearMask = wear,
                    Blocks = metricBlocks,
                    SampleCount = pixelCount,
                    BlockSize = BlockSize,
                    Width = config.Resolution,
                    Height = config.Resolution,
                    BoundaryMargin = config.Margin
                }.Schedule(blockCount, 1, handle);
                handle = new HydraulicErosionMetricReductionJob
                {
                    Blocks = metricBlocks,
                    Summary = metricSummary,
                    BlockCount = blockCount
                }.Schedule(handle);

                // COLD SYNC JOB: editor smoke tester must block to inspect deterministic result bounds.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                handleScheduled = false;

                ApplyMetrics(metricSummary[0], ref result);
                result.SentinelDelta = NativeMemorySentinel.ActiveAllocationCount - sentinelBefore;
                result.TrackedByteDelta = NativeMemorySentinel.TrackedBytes - trackedBytesBefore;
                result.Passed =
                    result.NanCount == 0 &&
                    result.BoundaryNanCount == 0 &&
                    result.SentinelDelta == 7 &&
                    result.MinHeight >= -0.0001f &&
                    result.MaxHeight <= 1.0001f;
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref metricBlocks);
                DisposeTracked(ref metricSummary);
                DisposeTracked(ref before);
                DisposeTracked(ref heightA);
                DisposeTracked(ref heightB);
                DisposeTracked(ref sediment);
                DisposeTracked(ref wear);
            }

            result.SentinelDelta = NativeMemorySentinel.ActiveAllocationCount - sentinelBefore;
            result.TrackedByteDelta = NativeMemorySentinel.TrackedBytes - trackedBytesBefore;
            result.Passed &= result.SentinelDelta == 0 && result.TrackedByteDelta == 0L;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            result.Milliseconds = (float)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

            if (!result.Passed && Application.isPlaying)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    SmokeFailureWarningHash,
                    NativeLeakContextHash,
                    result.SentinelDelta);
            }

            return result;
        }

        private static JobHandle ScheduleErosion(
            in ScenarioConfig config,
            NativeArray<float> heightA,
            NativeArray<float> heightB,
            NativeArray<float> sediment,
            NativeArray<float> wear,
            JobHandle dependency,
            out NativeArray<float> current)
        {
            int safeMargin = math.clamp(config.Margin, 0, math.max(0, config.Resolution / 4));
            var erosionJob = new HydraulicErosionJob
            {
                Heightmap = heightA,
                SedimentMask = sediment,
                ErosionDepthMask = wear,
                Width = config.Resolution,
                Height = config.Resolution,
                CoreOffsetX = safeMargin,
                CoreOffsetZ = safeMargin,
                CoreWidth = math.max(1, config.Resolution - safeMargin * 2),
                CoreHeight = math.max(1, config.Resolution - safeMargin * 2),
                SubGridSize = ErosionSubGridSize,
                DropletCount = math.max(0, config.Droplets),
                MaxLifetime = math.max(1, config.Lifetime),
                Seed = config.Seed,
                Inertia = ErosionInertia,
                CapacityFactor = 4f,
                MinCapacity = 0.0001f,
                ErosionRate = 0.35f,
                DepositRate = 0.18f,
                EvaporationRate = 0.015f,
                Gravity = 4f,
                InitialWater = config.Droplets <= 0 ? 0f : 1f,
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

            JobHandle handle = HydraulicErosionScheduler.ScheduleFourPhase(ref erosionJob, 1, dependency);
            current = heightA;
            NativeArray<float> next = heightB;

            int cellCount = config.Resolution * config.Resolution;
            for (int i = 0; i < 2; i++)
            {
                var flatJob = new SedimentaryFlatSmoothingJob
                {
                    InputHeights01 = current,
                    OutputHeights01 = next,
                    SedimentMask = sediment,
                    Width = config.Resolution,
                    Height = config.Resolution,
                    CellSizeMeters = 1f,
                    HeightScaleMeters = ErosionHeightScaleMeters,
                    MaxSlopeDegrees = SedimentaryFlatSlopeDegrees,
                    SedimentThreshold = SedimentaryFlatSedimentThreshold,
                    Strength = SedimentaryFlatSmoothingStrength
                };

                handle = flatJob.Schedule(cellCount, 64, handle);
                Swap(ref current, ref next);
            }

            for (int i = 0; i < config.SlumpIterations; i++)
            {
                var slumpJob = new ThermalSlumpingJob
                {
                    InputHeights01 = current,
                    OutputHeights01 = next,
                    WearMask = wear,
                    Width = config.Resolution,
                    Height = config.Resolution,
                    CellSizeMeters = 1f,
                    HeightScaleMeters = ErosionHeightScaleMeters,
                    TalusAngleDegrees = config.TalusAngle,
                    Strength = config.SlumpStrength,
                    WriteWearMaskFlag = 0
                };

                handle = slumpJob.Schedule(cellCount, 64, handle);
                Swap(ref current, ref next);
            }

            var canyonJob = new CanyonWallSteepeningJob
            {
                InputHeights01 = current,
                OutputHeights01 = next,
                ErosionDepthMask = wear,
                Width = config.Resolution,
                Height = config.Resolution,
                DepthThreshold = CanyonDepthThreshold,
                Strength = CanyonWallStrength,
                MaxLift01 = CanyonMaxLift01
            };

            handle = canyonJob.Schedule(cellCount, 64, handle);
            Swap(ref current, ref next);

            return handle;
        }

        private static void ApplyMetrics(in HydraulicErosionMetricBlock summary, ref ScenarioResult result)
        {
            result.MinHeight = summary.MinHeight;
            result.MaxHeight = summary.MaxHeight;
            result.MeanHeight = summary.SampleCount > 0 ? summary.SumHeight / summary.SampleCount : 0f;
            result.SumSediment = summary.SumSediment;
            result.SumWear = summary.SumWear;
            result.MaxSediment = summary.MaxSediment;
            result.MaxWear = summary.MaxWear;
            result.NanCount = summary.NanCount;
            result.MaxBoundaryHeightDelta = summary.MaxBoundaryHeightDelta;
            result.MaxBoundarySediment = summary.MaxBoundarySediment;
            result.MaxBoundaryWear = summary.MaxBoundaryWear;
            result.BoundarySampleCount = summary.BoundarySampleCount;
            result.BoundaryNanCount = summary.BoundaryNanCount;
        }

        private static string BuildJson(ScenarioResult[] results, int passCount)
        {
            // COLD ALLOC: StringBuilder[2048] - editor smoke JSON report - owner: HydraulicErosionSmokeTester
            var builder = new StringBuilder(2048);
            builder.Append("{\n");
            builder.Append("  \"status\":\"PENDING VERIFICATION\",\n");
            builder.Append("  \"tester\":\"HydraulicErosionSmokeTester\",\n");
            builder.Append("  \"scenarioCount\":").Append(results.Length).Append(",\n");
            builder.Append("  \"passCount\":").Append(passCount).Append(",\n");
            builder.Append("  \"scenarios\":[\n");

            for (int i = 0; i < results.Length; i++)
            {
                AppendScenarioJson(builder, results[i]);
                builder.Append(i == results.Length - 1 ? "\n" : ",\n");
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendScenarioJson(StringBuilder builder, in ScenarioResult result)
        {
            builder.Append("    {");
            builder.Append("\"name\":\"").Append(result.Name).Append("\",");
            builder.Append("\"passed\":").Append(result.Passed ? "true" : "false").Append(',');
            builder.Append("\"resolution\":").Append(result.Resolution).Append(',');
            builder.Append("\"droplets\":").Append(result.Droplets).Append(',');
            builder.Append("\"lifetime\":").Append(result.Lifetime).Append(',');
            builder.Append("\"slumpIterations\":").Append(result.SlumpIterations).Append(',');
            builder.Append("\"nanCount\":").Append(result.NanCount).Append(',');
            builder.Append("\"boundarySampleCount\":").Append(result.BoundarySampleCount).Append(',');
            builder.Append("\"boundaryNanCount\":").Append(result.BoundaryNanCount).Append(',');
            builder.Append("\"sentinelDelta\":").Append(result.SentinelDelta).Append(',');
            builder.Append("\"trackedByteDelta\":").Append(result.TrackedByteDelta).Append(',');
            builder.Append("\"minHeight\":");
            AppendFloat(builder, result.MinHeight);
            builder.Append(",\"maxHeight\":");
            AppendFloat(builder, result.MaxHeight);
            builder.Append(",\"meanHeight\":");
            AppendFloat(builder, result.MeanHeight);
            builder.Append(",\"sumSediment\":");
            AppendFloat(builder, result.SumSediment);
            builder.Append(",\"sumWear\":");
            AppendFloat(builder, result.SumWear);
            builder.Append(",\"maxSediment\":");
            AppendFloat(builder, result.MaxSediment);
            builder.Append(",\"maxWear\":");
            AppendFloat(builder, result.MaxWear);
            builder.Append(",\"maxBoundaryHeightDelta\":");
            AppendFloat(builder, result.MaxBoundaryHeightDelta);
            builder.Append(",\"maxBoundarySediment\":");
            AppendFloat(builder, result.MaxBoundarySediment);
            builder.Append(",\"maxBoundaryWear\":");
            AppendFloat(builder, result.MaxBoundaryWear);
            builder.Append(",\"milliseconds\":");
            AppendFloat(builder, result.Milliseconds);
            builder.Append('}');
        }

        private static void AppendFloat(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, NativeArrayOptions options, string label) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[HydraulicErosionSmokeTester] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[HydraulicErosionSmokeTester] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
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

        private static void Swap(ref NativeArray<float> current, ref NativeArray<float> next)
        {
            NativeArray<float> swap = current;
            current = next;
            next = swap;
        }
    }
}

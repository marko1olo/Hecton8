using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.World;
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
        private const string MetricsLabel = "metrics";
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
            NativeArray<ErosionSmokeMetrics> metrics = default;

            try
            {
                before = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightA = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightB = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                wear = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                metrics = new NativeArray<ErosionSmokeMetrics>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobBuffers(before, heightA, heightB, sediment, wear, metrics);

                JobHandle handle = new ErosionFractalHeightmapJob
                {
                    Before = before,
                    Height = heightA,
                    Resolution = Resolution,
                    PrimarySeed = 0xC001CAFEu,
                    RidgeSeed = 0x6C8E9CF5u
                }.Schedule(PixelCount, 64);

                var erosionJob = new HydraulicErosionJob
                {
                    Heightmap = heightA,
                    SedimentMask = sediment,
                    WearMask = wear,
                    Width = Resolution,
                    Height = Resolution,
                    CoreOffsetX = 4,
                    CoreOffsetZ = 4,
                    CoreWidth = Resolution - 8,
                    CoreHeight = Resolution - 8,
                    DropletCount = 300000,
                    MaxLifetime = 72,
                    Seed = 347239u,
                    Inertia = 0.05f,
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
                    ChannelSpawnBias = 4f,
                    SpawnCandidateCount = 8,
                    MinWater = 0.01f
                };

                handle = erosionJob.Schedule(handle);
                NativeArray<float> current = heightA;
                NativeArray<float> next = heightB;

                for (int i = 0; i < 3; i++)
                {
                    var slumpJob = new ThermalSlumpingJob
                    {
                        InputHeights01 = current,
                        OutputHeights01 = next,
                        WearMask = wear,
                        Width = Resolution,
                        Height = Resolution,
                        CellSizeMeters = 1f,
                        HeightScaleMeters = 160f,
                        TalusAngleDegrees = 45f,
                        Strength = 0.32f,
                        WriteWearMask = true
                    };

                    handle = slumpJob.Schedule(PixelCount, 64, handle);
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
                handle.Complete();

                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
                Directory.CreateDirectory(folder);
                WriteHeightPng(before, Path.Combine(folder, "ErosionTestHarness_Before.png"));
                WriteHeightPng(current, Path.Combine(folder, "ErosionTestHarness_After.png"));
                WriteMaskPng(sediment, Path.Combine(folder, "ErosionTestHarness_SedimentMask.png"));
                WriteMaskPng(wear, Path.Combine(folder, "ErosionTestHarness_WearMask.png"));
                WriteMetricsJson(metrics[0], Path.Combine(folder, "ErosionTestHarness_Metrics.json"));

                AssetDatabase.Refresh();
                Debug.Log("[ErosionTestHarness] Wrote erosion PNG artifacts to " + folder);
            }
            finally
            {
                DisposeTracked(ref before);
                DisposeTracked(ref heightA);
                DisposeTracked(ref heightB);
                DisposeTracked(ref sediment);
                DisposeTracked(ref wear);
                DisposeTracked(ref metrics);
            }
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> before,
            NativeArray<float> heightA,
            NativeArray<float> heightB,
            NativeArray<float> sediment,
            NativeArray<float> wear,
            NativeArray<ErosionSmokeMetrics> metrics)
        {
            NativeMemorySentinel.RegisterNativeArray(before, NativeMemoryOwner, BeforeLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(heightA, NativeMemoryOwner, HeightALabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(heightB, NativeMemoryOwner, HeightBLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(sediment, NativeMemoryOwner, SedimentLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(wear, NativeMemoryOwner, WearLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(metrics, NativeMemoryOwner, MetricsLabel, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void WriteHeightPng(NativeArray<float> heights, string path)
        {
            Color32[] pixels = new Color32[PixelCount];
            for (int i = 0; i < PixelCount; i++)
            {
                byte value = (byte)math.round(math.saturate(heights[i]) * 255f);
                pixels[i] = new Color32(value, value, value, 255);
            }

            WritePng(pixels, path);
        }

        private static void WriteMaskPng(NativeArray<float> mask, string path)
        {
            float maxValue = 0f;
            for (int i = 0; i < PixelCount; i++)
                maxValue = math.max(maxValue, mask[i]);

            float invMax = maxValue > 0.000001f ? 1f / maxValue : 0f;
            Color32[] pixels = new Color32[PixelCount];
            for (int i = 0; i < PixelCount; i++)
            {
                byte value = (byte)math.round(math.saturate(mask[i] * invMax) * 255f);
                pixels[i] = new Color32(value, value, value, 255);
            }

            WritePng(pixels, path);
        }

        private static void WriteMetricsJson(ErosionSmokeMetrics metrics, string path)
        {
            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - editor smoke JSON artifact buffer - owner: ErosionTestHarness
            builder.Append("{\n");
            AppendJsonProperty(builder, "schema", "hecton8.erosion_smoke_metrics.v1", true);
            AppendJsonProperty(builder, "resolution", Resolution, true);
            AppendJsonProperty(builder, "dropletCount", 300000, true);
            AppendJsonProperty(builder, "thermalIterations", 3, true);
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

        private static void WritePng(Color32[] pixels, string path)
        {
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void Swap(ref NativeArray<float> current, ref NativeArray<float> next)
        {
            NativeArray<float> swap = current;
            current = next;
            next = swap;
        }
    }
}

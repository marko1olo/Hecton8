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
        private const string MetricsLabel = "metrics";
        private const string ShelfRawLabel = "shelfRaw";
        private const string ShelfQuantizedLabel = "shelfQuantized";
        private const string HeightPixelsLabel = "heightPixels";
        private const string NormalPixelsLabel = "normalPixels";
        private const string MaskPixelsLabel = "maskPixels";
        private const string MaskMaxLabel = "maskMax";
        private const double ShelfPreviewOriginMeters = -16000.0;
        private const double ShelfPreviewCellSizeMeters = 64.0;
        private const double ShelfAupCellSizeMeters = 5000.0;
        private const float ShelfHighWorldY = 2000f;
        private const float ShelfLowWorldY = -5000f;
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
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                before = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightA = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightB = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                wear = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                metrics = new NativeArray<ErosionSmokeMetrics>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                RegisterTempJobBuffers(before, heightA, heightB, sediment, wear, metrics);

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
                    DropletCount = 300000,
                    MaxLifetime = 72,
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

                handle = HydraulicErosionScheduler.ScheduleFourPhase(ref erosionJob, 1, handle);
                NativeArray<float> current = heightA;
                NativeArray<float> next = heightB;

                for (int i = 0; i < 2; i++)
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
                        WriteWearMask = false
                    };

                    handle = slumpJob.Schedule(PixelCount, 64, handle);
                    Swap(ref current, ref next);
                }

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
                Debug.Log("[ErosionTestHarness] Wrote erosion PNG artifacts to " + folder);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref before);
                DisposeTracked(ref heightA);
                DisposeTracked(ref heightB);
                DisposeTracked(ref sediment);
                DisposeTracked(ref wear);
                DisposeTracked(ref metrics);
            }
        }

        private static void WriteMacroShelfPreviewArtifacts(string folder)
        {
            NativeArray<float> raw = default;
            NativeArray<float> quantized = default;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                raw = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                quantized = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(raw, NativeMemoryOwner, ShelfRawLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(quantized, NativeMemoryOwner, ShelfQuantizedLabel, NativeAllocationLifetime.TempJob);

                HectonSandboxAbyssalShelfParams parameters = CreateMacroShelfParameters();
                handle = new HectonSandboxAbyssalShelfBaseJob
                {
                    OutputHeights01 = raw,
                    Parameters = parameters,
                    Width = Resolution,
                    WorldOriginAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                        ShelfPreviewOriginMeters,
                        ShelfPreviewOriginMeters,
                        ShelfAupCellSizeMeters),
                    CellSizeMeters = ShelfPreviewCellSizeMeters
                }.Schedule(PixelCount, 64);
                handleScheduled = true;

                const float plateauSourceAngle = 15f;
                const float plateauTargetAngle = 3.5f;
                const float cliffSourceAngle = 45f;
                const float cliffTargetAngle = 52f;
                const float cliffRampEndAngle = cliffSourceAngle + (cliffTargetAngle - cliffSourceAngle) * 0.25f;
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
                    ShelfHighWorldY - ShelfLowWorldY,
                    (float)ShelfPreviewCellSizeMeters);
            }
            finally
            {
                if (handleScheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref raw);
                DisposeTracked(ref quantized);
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
                MacroExponentialFalloff = 3.1f,
                ShelfRunMeters = 15000f,
                ShelfTargetSlopeDegrees = 30f,
                TrenchDepthMeters = 5000f,
                TrenchWidthMeters = 780f,
                TrenchSharpness = 2.4f,
                IslandCenterRadiusMeters = 2600f,
                IslandJunctionThreshold = 0.58f,
                Seed = HectonSandboxAbyssalShelfMath.CombineWorldSeed(880031u, 0)
            };
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
            NativeArray<Color32> pixels = default;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                pixels = new NativeArray<Color32>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(pixels, NativeMemoryOwner, HeightPixelsLabel, NativeAllocationLifetime.TempJob);

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

                DisposeTracked(ref pixels);
            }
        }

        private static void WriteNormalPng(NativeArray<float> heights, string path, float heightScaleMeters, float cellSizeMeters)
        {
            NativeArray<Color32> pixels = default;
            JobHandle handle = default;
            bool handleScheduled = false;

            try
            {
                pixels = new NativeArray<Color32>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(pixels, NativeMemoryOwner, NormalPixelsLabel, NativeAllocationLifetime.TempJob);

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

                DisposeTracked(ref pixels);
            }
        }

        private static void WriteMaskPng(NativeArray<float> mask, string path)
        {
            NativeArray<Color32> pixels = default;
            NativeArray<float> maxValue = default;
            JobHandle maxHandle = default;
            JobHandle bakeHandle = default;
            bool maxHandleScheduled = false;
            bool bakeHandleScheduled = false;

            try
            {
                pixels = new NativeArray<Color32>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                maxValue = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(pixels, NativeMemoryOwner, MaskPixelsLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(maxValue, NativeMemoryOwner, MaskMaxLabel, NativeAllocationLifetime.TempJob);

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

                DisposeTracked(ref maxValue);
                DisposeTracked(ref pixels);
            }
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

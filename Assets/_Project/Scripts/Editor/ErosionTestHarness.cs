using System.IO;
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

            try
            {
                before = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightA = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightB = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                wear = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                GenerateFractalHeightmap(before, heightA);

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

                JobHandle handle = erosionJob.Schedule();
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

                // COLD SYNC JOB: editor harness must block to write deterministic PNG artifacts.
                handle.Complete();

                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
                Directory.CreateDirectory(folder);
                WriteHeightPng(before, Path.Combine(folder, "ErosionTestHarness_Before.png"));
                WriteHeightPng(current, Path.Combine(folder, "ErosionTestHarness_After.png"));
                WriteMaskPng(sediment, Path.Combine(folder, "ErosionTestHarness_SedimentMask.png"));
                WriteMaskPng(wear, Path.Combine(folder, "ErosionTestHarness_WearMask.png"));

                AssetDatabase.Refresh();
                Debug.Log("[ErosionTestHarness] Wrote erosion PNG artifacts to " + folder);
            }
            finally
            {
                if (before.IsCreated)
                    before.Dispose();
                if (heightA.IsCreated)
                    heightA.Dispose();
                if (heightB.IsCreated)
                    heightB.Dispose();
                if (sediment.IsCreated)
                    sediment.Dispose();
                if (wear.IsCreated)
                    wear.Dispose();
            }
        }

        private static void GenerateFractalHeightmap(NativeArray<float> before, NativeArray<float> height)
        {
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float2 uv = new float2(x, z) * (1f / Resolution);
                    float n = FractalValueNoise(uv * 7.5f, 0xC001CAFEu);
                    float ridge = 1f - math.abs(FractalValueNoise(uv * 3.25f + new float2(19.3f, -7.1f), 0x6C8E9CF5u) * 2f - 1f);
                    float basin = math.smoothstep(0.2f, 0.95f, n);
                    float h = math.saturate(basin * 0.72f + math.pow(ridge, 3.2f) * 0.28f);
                    int index = z * Resolution + x;
                    before[index] = h;
                    height[index] = h;
                }
            }
        }

        private static float FractalValueNoise(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < 6; octave++)
            {
                total += ValueNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= 0.52f;
                frequency *= 2.03f;
            }

            return total / math.max(0.0001f, normalization);
        }

        private static float ValueNoise(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * (3f - 2f * local);

            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);

            return math.lerp(
                math.lerp(a, b, smooth.x),
                math.lerp(c, d, smooth.x),
                smooth.y);
        }

        private static float Hash01(int x, int y, uint seed)
        {
            uint hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
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

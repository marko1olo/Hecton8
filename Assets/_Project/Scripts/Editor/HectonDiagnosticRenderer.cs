using System;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using Hecton8.World;
using Unity.Burst;

public static class HectonDiagnosticRenderer
{
    private static readonly string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

    [MenuItem("Tools/Hecton/Run Terrain Diagnostics")]
    public static void RunDiagnostics()
    {
        Debug.Log("[DIAGNOSTICS] Starting Phase A & B...");

        try
        {
            float centerX = 4000f; // Edge of the shelf
            float centerZ = 4000f;
            int res = 1024;
            
            // Phase A: Scales
            float[] scales = { 4096f, 1024f, 256f, 64f, 16f };
            foreach (float scale in scales)
            {
                RenderPhaseA(centerX, centerZ, scale, res);
            }

            // Phase B: Debug Maps at 1024m scale
            RenderPhaseB(centerX, centerZ, 1024f, res);

            Debug.Log("[DIAGNOSTICS] Done.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DIAGNOSTICS_ERROR] {ex}");
            EditorApplication.Exit(1);
        }
    }

    private static void RenderPhaseA(float cx, float cz, float size, int res)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(12345);

        var heights = new NativeArray<float>(res * res, Allocator.TempJob);
        var job = new HeightMapJob {
            Heights = heights,
            Params = p,
            Width = res,
            CellSize = cellSize,
            StartX = cx - size * 0.5f,
            StartZ = cz - size * 0.5f,
            IncludeMeso = true
        };
        job.Schedule(res * res, 64).Complete();

        // Hillshade
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        var hsJob = new HillshadeJob {
            Heights = heights,
            Colors = colors,
            Width = res,
            CellSize = cellSize,
            SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
        };
        hsJob.Schedule(res * res, 64).Complete();

        SavePNG(colors, res, res, $"PhaseA_{size}m_cell{cellSize:F2}.png");

        heights.Dispose();
        colors.Dispose();
    }

    private static void RenderPhaseB(float cx, float cz, float size, int res)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(12345);

        var heightsMacro = new NativeArray<float>(res * res, Allocator.TempJob);
        var heightsFull = new NativeArray<float>(res * res, Allocator.TempJob);
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        
        var j1 = new HeightMapJob { Heights = heightsMacro, Params = p, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = false };
        var j2 = new HeightMapJob { Heights = heightsFull, Params = p, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = true };
        
        j1.Schedule(res * res, 64).Complete();
        j2.Schedule(res * res, 64).Complete();

        // 1. Slope map
        var slopeJob = new SlopeMapJob { Heights = heightsFull, Colors = colors, Width = res, CellSize = cellSize };
        slopeJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, "PhaseB_Slope.png");

        // 2. Curvature map
        var curvJob = new CurvatureMapJob { Heights = heightsFull, Colors = colors, Width = res, CellSize = cellSize };
        curvJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, "PhaseB_Curvature.png");

        // 3. Detail Strength map
        var detailJob = new DetailStrengthMapJob { Params = p, Colors = colors, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f };
        detailJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, "PhaseB_DetailStrength.png");

        // 4. Meso Diff map
        var diffJob = new DiffMapJob { Heights1 = heightsMacro, Heights2 = heightsFull, Colors = colors };
        diffJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, "PhaseB_MesoDiff.png");

        // 5. Masks map
        var maskJob = new MaskMapJob { Params = p, Colors = colors, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f };
        maskJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, "PhaseB_FeatureMasks.png");

        heightsMacro.Dispose();
        heightsFull.Dispose();
        colors.Dispose();
    }

    private static void SavePNG(NativeArray<Color32> colors, int w, int h, string filename)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixelData(colors, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log($"Saved {filename}");
    }

    private static float CellSizeToDetailStrength(float cell)
    {
        if (cell <= 1f) return 1f;
        if (cell <= 2f) return math.lerp(1f, 0.5f, (cell - 1f));
        if (cell <= 4f) return math.lerp(0.5f, 0f, (cell - 2f) * 0.5f);
        return 0f;
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct HeightMapJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> Heights;
        public WorldMacroGeologyParams Params;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;
        public bool IncludeMeso;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float worldX = StartX + x * CellSize;
            float worldZ = StartZ + z * CellSize;
            
            WorldMacroGeologySample macro = WorldMacroGeologyFields.EvaluateSinglePass(worldX, worldZ, in Params);
            float h = macro.HeightMeters;
            
            if (IncludeMeso)
            {
                float detailStrength = CellSizeToDetailStrength(CellSize);
                if (detailStrength > 0.001f)
                {
                    WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(Params.Seed);
                    float baseBudget = math.lerp(45f, 70f, detailStrength);
                    mesoParams.MaxMesoDeltaMeters = math.max(1f, baseBudget);

                    WorldTerrainMesoDetailSample mesoSample = WorldTerrainMesoDetailFields.Evaluate(
                        in macro, worldX, worldZ, in mesoParams);

                    h += mesoSample.HeightDeltaMeters * detailStrength;
                }
            }
            Heights[i] = h;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct HillshadeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float3 SunDir;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float3 normal = math.normalize(new float3(hL - hR, 2f * CellSize, hD - hU));
            float ndotl = math.saturate(math.dot(normal, SunDir));
            float c = 0.1f + ndotl * 0.9f;
            byte b = (byte)math.clamp(c * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct SlopeMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float3 normal = math.normalize(new float3(hL - hR, 2f * CellSize, hD - hU));
            float slope01 = 1f - normal.y; // 0 = flat, 1 = vertical
            byte r = (byte)math.clamp(slope01 * 2f * 255f, 0, 255);
            Colors[i] = new Color32(r, r, r, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct CurvatureMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float laplacian = (hL + hR + hD + hU - 4f * hC) / CellSize;
            float c = laplacian * 0.5f + 0.5f;
            byte b = (byte)math.clamp(c * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DiffMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights1;
        [ReadOnly] public NativeArray<float> Heights2;
        [WriteOnly] public NativeArray<Color32> Colors;

        public void Execute(int i)
        {
            float diff = math.abs(Heights1[i] - Heights2[i]);
            byte r = (byte)math.clamp(diff * 10f * 255f, 0, 255);
            Colors[i] = new Color32(r, 0, 0, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DetailStrengthMapJob : IJobParallelFor
    {
        public WorldMacroGeologyParams Params;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float worldX = StartX + x * CellSize;
            float worldZ = StartZ + z * CellSize;

            float ds = CellSizeToDetailStrength(CellSize);
            byte b = (byte)math.clamp(ds * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct MaskMapJob : IJobParallelFor
    {
        public WorldMacroGeologyParams Params;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float worldX = StartX + x * CellSize;
            float worldZ = StartZ + z * CellSize;

            WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ, in Params, out var masks);
            
            // R = Terrace, G = Sediment (we will use Slump), B = Canyon
            byte r = (byte)math.clamp(masks.Terrace * 255f, 0, 255);
            byte g = (byte)math.clamp(masks.Slump * 255f, 0, 255);
            byte b = (byte)math.clamp(masks.Canyon * 255f, 0, 255);
            Colors[i] = new Color32(r, g, b, 255);
        }
    }
}

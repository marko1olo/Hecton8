using System;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using Hecton8.World;
using Unity.Burst;

public static class GraphOutputRenderer
{
    private static readonly string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

    public static void Execute()
    {
        Debug.Log("[GRAPH_RENDERER] Starting Task A...");
        try {
            float cx = 4000f;
            float cz = 4000f;

            // 1. Raw Function Renders
            RenderRaw(cx, cz, 1024f, 1024, "A_raw_1024");
            RenderRaw(cx, cz, 256f, 1024, "A_raw_256");

            // 2. Setup Scene for Graph Render
            SessionState.SetBool("UpdateSandboxSceneTaskRun", true); // Block the other task from messing with scenes
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");
            
            var mmObject = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject == null) {
                Debug.LogError("[GRAPH_RENDERER] MapMagicObject not found!");
                EditorApplication.Exit(1);
                return;
            }

            // Move MapMagic to generate at (4000, 4000)
            GameObject viewer = new GameObject("Viewer");
            viewer.transform.position = new Vector3(cx, 0, cz);
            viewer.tag = "MainCamera";
            viewer.AddComponent<Camera>();

            mmObject.tiles.generateInfinite = true;
            mmObject.tiles.generateRange = 2; // Enough to cover a few tiles
            mmObject.Refresh(true);
            
            EditorApplication.update += CheckGeneration;
            
        } catch (Exception ex) {
            Debug.LogError($"[GRAPH_RENDERER] {ex}");
            EditorApplication.Exit(1);
        }
    }

    private static double startTime = 0;
    private static void CheckGeneration()
    {
        if (startTime == 0) startTime = EditorApplication.timeSinceStartup;

        if (EditorApplication.timeSinceStartup - startTime > 300.0)
        {
            Debug.LogError("[GRAPH_RENDERER] Timeout waiting for generation.");
            EditorApplication.update -= CheckGeneration;
            EditorApplication.Exit(1);
            return;
        }

        var mmObject = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
        if (mmObject == null) return;

        bool isGenerating = mmObject.IsGenerating();
        bool hasTerrain = UnityEngine.Terrain.activeTerrains.Length > 0;
        
        if (!isGenerating && hasTerrain && EditorApplication.timeSinceStartup - startTime > 15.0)
        {
            Debug.Log("[GRAPH_RENDERER] Generation complete! Rendering Graph Output...");
            EditorApplication.update -= CheckGeneration;
            
            try {
                RenderGraphOutput(4000f, 4000f, 1024f, 1024, "B_graph_1024");
                RenderGraphOutput(4000f, 4000f, 256f, 1024, "B_graph_256");
            } catch (Exception ex) {
                Debug.LogError($"[GRAPH_RENDERER] Render Error: {ex}");
            }
            
            EditorApplication.Exit(0);
        }
    }

    private static void RenderRaw(float cx, float cz, float size, int res, string prefix)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(12345);

        var heights = new NativeArray<float>(res * res, Allocator.TempJob);
        var job = new HectonDiagnosticRenderer.HeightMapJob {
            Heights = heights, Params = p, Width = res, CellSize = cellSize,
            StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = false
        };
        job.Schedule(res * res, 64).Complete();

        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        var hsJob = new HectonDiagnosticRenderer.HillshadeJob {
            Heights = heights, Colors = colors, Width = res, CellSize = cellSize,
            SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
        };
        hsJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, $"{prefix}_beauty.png");

        var slopeJob = new HectonDiagnosticRenderer.SlopeMapJob {
            Heights = heights, Colors = colors, Width = res, CellSize = cellSize
        };
        slopeJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, $"{prefix}_slope.png");

        heights.Dispose();
        colors.Dispose();
    }

    private static void RenderGraphOutput(float cx, float cz, float size, int res, string prefix)
    {
        float cellSize = size / res;
        var heights = new NativeArray<float>(res * res, Allocator.Persistent);
        
        float startX = cx - size * 0.5f;
        float startZ = cz - size * 0.5f;

        // Sample terrains on main thread since Unity API is not thread safe
        for (int i = 0; i < res * res; i++) {
            int x = i % res;
            int z = i / res;
            float worldX = startX + x * cellSize;
            float worldZ = startZ + z * cellSize;
            
            float h = SampleTerrainHeight(worldX, worldZ);
            heights[i] = h;
        }

        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        var hsJob = new HectonDiagnosticRenderer.HillshadeJob {
            Heights = heights, Colors = colors, Width = res, CellSize = cellSize,
            SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
        };
        hsJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, $"{prefix}_beauty.png");

        var slopeJob = new HectonDiagnosticRenderer.SlopeMapJob {
            Heights = heights, Colors = colors, Width = res, CellSize = cellSize
        };
        slopeJob.Schedule(res * res, 64).Complete();
        SavePNG(colors, res, res, $"{prefix}_slope.png");

        heights.Dispose();
        colors.Dispose();
    }

    private static float SampleTerrainHeight(float worldX, float worldZ)
    {
        foreach (var t in UnityEngine.Terrain.activeTerrains) {
            if (t.terrainData == null) continue;
            Vector3 local = t.transform.InverseTransformPoint(new Vector3(worldX, 0, worldZ));
            if (local.x >= 0 && local.x <= t.terrainData.size.x && local.z >= 0 && local.z <= t.terrainData.size.z) {
                return t.SampleHeight(new Vector3(worldX, 0, worldZ)) + t.transform.position.y;
            }
        }
        return -4000f; // Default if no terrain found (abyssal depth)
    }

    private static void SavePNG(NativeArray<Color32> colors, int w, int h, string filename)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixelData(colors, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log($"[GRAPH_RENDERER] Saved {filename}");
    }
}

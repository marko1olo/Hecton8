using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MapMagic.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Diagnostics
{
    public static class HeadlessMatrixBenchmark
    {
        private static MapMagicObject mm;
        private static Queue<(int size, int res)> configQueue;
        private static (int size, int res) currentConfig;
        private static Stopwatch sw;
        private static StringBuilder sb;
        private static bool isGenerating = false;

        public static void Run()
        {
            UnityEngine.Debug.Log("[TASK_2] Starting Headless Matrix Benchmark...");

            mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
            if (mm == null)
            {
                UnityEngine.Debug.LogError("[TASK_2] MapMagicObject not found.");
                HeadlessRunAll.NextTask();
                return;
            }

            configQueue = new Queue<(int size, int res)>();
            configQueue.Enqueue((1000, 513));
            configQueue.Enqueue((1000, 1025));
            configQueue.Enqueue((500, 1025));

            sb = new StringBuilder();
            sb.AppendLine("=== RESOLUTION MATRIX BENCHMARK ===");
            sb.AppendLine("Config | m/vert | Gen Time | Memory/Tile | Tiles Around | Seams");
            sb.AppendLine("---|---|---|---|---|---");

            EditorApplication.update += BenchmarkUpdate;
            StartNextConfig();
        }

        private static void StartNextConfig()
        {
            if (configQueue.Count == 0)
            {
                EditorApplication.update -= BenchmarkUpdate;
                UnityEngine.Debug.Log("[TASK_2] Benchmark Complete!\n" + sb.ToString());
                HeadlessRunAll.NextTask();
                return;
            }

            currentConfig = configQueue.Dequeue();
            mm.tileSize = new Den.Tools.Vector2D(currentConfig.size, currentConfig.size);
            mm.tileResolution = (MapMagicObject.Resolution)currentConfig.res;

            UnityEngine.Debug.Log($"[TASK_2] Testing {currentConfig.size}@{currentConfig.res}...");

            HeadlessRunAll.ClearMapMagic(mm);
            mm.tiles.Pin(new Den.Tools.Coord(0, 0), false, mm);

            System.GC.Collect();
            sw = Stopwatch.StartNew();
            mm.StartGenerate();
            isGenerating = true;
        }

        private static void BenchmarkUpdate()
        {
            if (!isGenerating) return;

            if (!mm.IsGenerating())
            {
                sw.Stop();
                isGenerating = false;

                float mPerVert = (float)currentConfig.size / (currentConfig.res - 1);
                int range = 2000 / currentConfig.size;
                int tilesAround = (range * 2 + 1) * (range * 2 + 1);
                int seams = (range * 2 + 1) * 2 * (range * 2);
                
                long memBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                // Estimate just for the tile (the actual memory will be dirty from the editor)
                long estTileMem = (currentConfig.res * currentConfig.res * 4);
                float estMemMb = estTileMem / (1024f * 1024f);

                sb.AppendLine($"{currentConfig.size}m@{currentConfig.res} | {mPerVert:F2} | {sw.ElapsedMilliseconds}ms | {estMemMb:F2}MB | {tilesAround} | {seams}");

                StartNextConfig();
            }
        }
    }
}

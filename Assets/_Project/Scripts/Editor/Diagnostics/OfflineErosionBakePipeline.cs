using UnityEngine;
using UnityEditor;
using System.IO;
using MapMagic.Core;
using Unity.Collections;

namespace Hecton8.Diagnostics
{
    public static class OfflineErosionBakePipeline
    {
        private static MapMagicObject mm;
        private static bool isWaiting = false;
        private static Terrain targetTerrain = null;

        public static void BakeCenterTile()
        {
            Debug.Log("[TASK_3] Starting Offline Erosion Bake for Tile (0,0)...");

            mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
            if (mm == null)
            {
                Debug.LogError("[TASK_3] MapMagicObject not found.");
                HeadlessRunAll.NextTask();
                return;
            }

            Debug.Log("[TASK_3] Forcing generation of tile 0,0 for Bake...");
            HeadlessRunAll.ClearMapMagic(mm);
            mm.tiles.Pin(new Den.Tools.Coord(0, 0), false, mm);
            mm.StartGenerate();
            
            isWaiting = true;
            EditorApplication.update += CheckGeneration;
        }

        private static void CheckGeneration()
        {
            if (!isWaiting) return;
            if (mm.IsGenerating()) return;

            isWaiting = false;
            EditorApplication.update -= CheckGeneration;

            foreach (var tile in mm.tiles.AllActiveTerrains())
            {
                var tTile = mm.tiles.FindByTerrain(tile);
                if (tTile != null && tTile.coord.x == 0 && tTile.coord.z == 0)
                {
                    targetTerrain = tile;
                    break;
                }
            }

            if (targetTerrain == null)
            {
                Debug.LogError("[TASK_3] No generated Terrain found.");
                HeadlessRunAll.NextTask();
                return;
            }

            if (targetTerrain == null)
            {
                // Fallback: just grab any terrain as prototype
                targetTerrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            }

            if (targetTerrain == null)
            {
                Debug.LogError("[EROSION_BAKE] No generated Terrain found. Generate MapMagic first.");
                return;
            }

            int w = targetTerrain.terrainData.heightmapResolution;
            float[,] heights = targetTerrain.terrainData.GetHeights(0, 0, w, w);

            // Save BEFORE images
            SaveImages(heights, w, "Erosion_Before");

            // Flatten 2D array to 1D for Job
            NativeArray<float> heightBuffer = new NativeArray<float>(w * w, Allocator.TempJob);
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    heightBuffer[y * w + x] = heights[y, x];
                }
            }

            Debug.Log($"[EROSION_BAKE] Captured Heightmap {w}x{w}. Running HydraulicErosionJob (1M droplets)...");

            var sedimentMask = new NativeArray<float>(w * w, Allocator.TempJob);
            var erosionDepthMask = new NativeArray<float>(w * w, Allocator.TempJob);
            var dummyQueue = new Unity.Collections.NativeQueue<Hecton8.World.HydraulicErosionHeightDelta>(Allocator.TempJob);
            var dummyBudget = new NativeArray<int>(2, Allocator.TempJob);

            var job = new Hecton8.World.HydraulicErosionJob
            {
                Heightmap = heightBuffer,
                SedimentMask = sedimentMask,
                ErosionDepthMask = erosionDepthMask,
                HeightDeltaQueue = dummyQueue.AsParallelWriter(),
                HeightDeltaBudget = dummyBudget,
                Width = w,
                Height = w,
                CoreOffsetX = 0,
                CoreOffsetZ = 0,
                CoreWidth = w,
                CoreHeight = w,
                SubGridSize = 128,
                DropletCount = 1000000,
                MaxLifetime = 30,
                Seed = 12345,
                Inertia = 0.05f,
                CapacityFactor = 4f,
                MinCapacity = 0.01f,
                ErosionRate = 0.3f,
                DepositRate = 0.3f,
                EvaporationRate = 0.01f,
                Gravity = 4f,
                InitialWater = 1f,
                InitialSpeed = 1f,
                DepressionFillStrength = 0f,
                DepressionSpawnBias = 0f,
                ChannelSpawnBias = 0f,
                ChannelFlowBias = 0f,
                CellSizeMeters = targetTerrain.terrainData.size.x / w,
                HeightScaleMeters = targetTerrain.terrainData.size.y,
                SedimentaryFlatSlopeDegrees = 5f,
                SpawnCandidateCount = 1,
                MinWater = 0.01f
            };

            // Run Job safely using the Scheduler
            var handle = Hecton8.World.HydraulicErosionScheduler.ScheduleFourPhaseSliced(
                ref job,
                dropletsPerSlice: 50000,
                innerLoopBatchCount: 16,
                dependency: default
            );
            handle.Complete();

            Debug.Log("[EROSION_BAKE] HydraulicErosionJob complete! Serializing to Asset...");

            // Write back to 2D
            float[,] erodedHeights = new float[w, w];
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    erodedHeights[y, x] = heightBuffer[y * w + x];
                }
            }

            // Cleanup
            heightBuffer.Dispose();
            sedimentMask.Dispose();
            erosionDepthMask.Dispose();
            dummyQueue.Dispose();
            dummyBudget.Dispose();

            // Save to new TerrainData
            TerrainData bakedData = new TerrainData();
            bakedData.heightmapResolution = w;
            bakedData.size = targetTerrain.terrainData.size;
            bakedData.SetHeights(0, 0, erodedHeights);

            string outPath = "Assets/_Project/Data/World/Archive/ErodedTile_0_0.asset";
            Directory.CreateDirectory("Assets/_Project/Data/World/Archive");
            AssetDatabase.CreateAsset(bakedData, outPath);
            AssetDatabase.SaveAssets();

            // Save AFTER images
            SaveImages(erodedHeights, w, "Erosion_After");

            Debug.Log($"[TASK_3] Saved successfully to: {outPath}. Cost for Playmode Streaming is now 0ms.");
            HeadlessRunAll.NextTask();
        }

        private static void SaveImages(float[,] allHeights, int w, string suffix)
        {
            string outDir = Path.Combine(Application.dataPath, "../Logs/TerrainDiagnostics");
            Directory.CreateDirectory(outDir);

            Texture2D slopeTex = new Texture2D(w, w, TextureFormat.RGB24, false);
            Color32[] slopePixels = new Color32[w * w];
            Texture2D beautyTex = new Texture2D(w, w, TextureFormat.RGB24, false);
            Color32[] beautyPixels = new Color32[w * w];

            float sizeX = 1000f; // rough
            float sizeZ = 1000f;
            float sizeY = 600f; // rough height

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float curHeight = allHeights[y, x];
                    
                    float hL = x > 0 ? allHeights[y, x - 1] : curHeight;
                    float hR = x < w - 1 ? allHeights[y, x + 1] : curHeight;
                    float hD = y > 0 ? allHeights[y - 1, x] : curHeight;
                    float hU = y < w - 1 ? allHeights[y + 1, x] : curHeight;

                    float dx = (hR - hL) * sizeY / (2.0f * (sizeX / w));
                    float dz = (hU - hD) * sizeY / (2.0f * (sizeZ / w));
                    
                    Vector3 normal = new Vector3(-dx, 1, -dz).normalized;
                    float slopeAngle = Vector3.Angle(Vector3.up, normal);
                    byte slopeCol = (byte)Mathf.Clamp(slopeAngle / 45f * 255f, 0, 255);
                    slopePixels[idx] = new Color32(slopeCol, slopeCol, slopeCol, 255);

                    Vector3 lightDir = new Vector3(1, 1, 1).normalized;
                    float dot = Mathf.Max(0, Vector3.Dot(normal, lightDir));
                    byte shadeCol = (byte)(dot * 255f);
                    beautyPixels[idx] = new Color32(shadeCol, shadeCol, shadeCol, 255);
                }
            }

            slopeTex.SetPixels32(slopePixels);
            slopeTex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, $"Task3_Headless_{suffix}_Slope.png"), slopeTex.EncodeToPNG());

            beautyTex.SetPixels32(beautyPixels);
            beautyTex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, $"Task3_Headless_{suffix}_Hillshade.png"), beautyTex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(slopeTex);
            UnityEngine.Object.DestroyImmediate(beautyTex);
        }
    }
}

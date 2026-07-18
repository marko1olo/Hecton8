using System.IO;
using UnityEditor;
using UnityEngine;
using MapMagic.Core;

namespace Hecton8.Diagnostics
{
    public static class HeadlessTerrainDumper
    {
        private static MapMagicObject mm;
        private static Terrain targetTerrain;
        private static bool isWaiting = false;

        public static void Run()
        {
            Debug.Log("[TASK_1] Starting Headless Terrain Dumper...");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");

            mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
            if (mm == null)
            {
                Debug.LogError("[TASK_1] MapMagicObject not found.");
                HeadlessRunAll.NextTask();
                return;
            }

            Debug.Log("[TASK_1] Clearing and starting Generate...");
            // Force generate center tile
            HeadlessRunAll.ClearMapMagic(mm);
            mm.tiles.Pin(new Den.Tools.Coord(0, 0), false, mm);
            mm.StartGenerate();
            
            isWaiting = true;
            EditorApplication.update += CheckGeneration;
        }

        private static void CheckGeneration()
        {
            if (!isWaiting) return;
            
            if (mm.IsGenerating())
            {
                // still generating
                return;
            }

            // Generation complete!
            isWaiting = false;
            EditorApplication.update -= CheckGeneration;

            Debug.Log("[TASK_1] MapMagic Generation Complete.");

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
                Debug.LogError("[TASK_1] Generation finished but Terrain for tile 0,0 is null! Height std=0.");
                HeadlessRunAll.NextTask();
                return;
            }

            DumpTerrain(targetTerrain);
            HeadlessRunAll.NextTask();
        }

        private static void DumpTerrain(Terrain t)
        {
            int w = t.terrainData.heightmapResolution;
            float[,] heights = t.terrainData.GetHeights(0, 0, w, w);

            // Check if it's flat (std=0)
            float sum = 0f;
            float sumSq = 0f;
            int count = w * w;
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float val = heights[y, x];
                    sum += val;
                    sumSq += val * val;
                }
            }
            float mean = sum / count;
            float variance = (sumSq / count) - (mean * mean);
            float std = Mathf.Sqrt(Mathf.Max(0, variance));
            float sizeY = t.terrainData.size.y;

            Debug.Log($"[TASK_1] Heightmap Stats for {t.name}: Mean={mean:F4}, Std={std:F4}, SizeY={sizeY}m, RealStd={std * sizeY:F2}m");

            if (std < 0.0001f)
            {
                Debug.LogError("[TASK_1] Heightmap is flat! Draft generation failed in batchmode.");
            }
            else
            {
                Debug.Log("[TASK_1] Heightmap has variation. Generating PNGs...");
            }

            string outDir = Path.Combine(Application.dataPath, "../Logs/TerrainDiagnostics");
            Directory.CreateDirectory(outDir);

            // Generate full tile (1000m)
            SaveImages(heights, w, 0, 0, w, outDir, "Full1000m", t);

            // Generate 100m crop (roughly 1/10th of the tile, so w/10)
            int w100 = w / 10;
            int offset100 = (w - w100) / 2;
            SaveImages(heights, w, offset100, offset100, w100, outDir, "Crop100m", t);

            // Generate 10m crop (roughly 1/100th of the tile, so w/100)
            int w10 = w / 100;
            if (w10 < 10) w10 = 10;
            int offset10 = (w - w10) / 2;
            SaveImages(heights, w, offset10, offset10, w10, outDir, "Crop10m", t);
        }

        private static void SaveImages(float[,] allHeights, int totalW, int offsetX, int offsetY, int cropW, string outDir, string suffix, Terrain t)
        {
            Texture2D slopeTex = new Texture2D(cropW, cropW, TextureFormat.RGB24, false);
            Color32[] slopePixels = new Color32[cropW * cropW];
            Texture2D beautyTex = new Texture2D(cropW, cropW, TextureFormat.RGB24, false);
            Color32[] beautyPixels = new Color32[cropW * cropW];

            float sizeX = t.terrainData.size.x * ((float)cropW / totalW);
            float sizeZ = t.terrainData.size.z * ((float)cropW / totalW);
            float sizeY = t.terrainData.size.y;

            for (int y = 0; y < cropW; y++)
            {
                for (int x = 0; x < cropW; x++)
                {
                    int gy = offsetY + y;
                    int gx = offsetX + x;

                    int idx = y * cropW + x;
                    float curHeight = allHeights[gy, gx];
                    
                    float hL = gx > 0 ? allHeights[gy, gx - 1] : curHeight;
                    float hR = gx < totalW - 1 ? allHeights[gy, gx + 1] : curHeight;
                    float hD = gy > 0 ? allHeights[gy - 1, gx] : curHeight;
                    float hU = gy < totalW - 1 ? allHeights[gy + 1, gx] : curHeight;

                    float dx = (hR - hL) * sizeY / (2.0f * (sizeX / cropW));
                    float dz = (hU - hD) * sizeY / (2.0f * (sizeZ / cropW));
                    
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
            File.WriteAllBytes(Path.Combine(outDir, $"Task1_Headless_{suffix}_Slope.png"), slopeTex.EncodeToPNG());

            beautyTex.SetPixels32(beautyPixels);
            beautyTex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, $"Task1_Headless_{suffix}_Hillshade.png"), beautyTex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(slopeTex);
            UnityEngine.Object.DestroyImmediate(beautyTex);

            Debug.Log($"[TASK_1] Saved {suffix} captures.");
        }
    }
}

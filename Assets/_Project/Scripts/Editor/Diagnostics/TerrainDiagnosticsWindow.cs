using UnityEngine;
using UnityEditor;
using System.IO;
using MapMagic.Core;
using Hecton8.World;
using UnityEditor.SceneManagement;
using System.Text;

namespace Hecton8.Diagnostics
{
    public class TerrainDiagnosticsWindow : EditorWindow
    {
        [MenuItem("Tools/Hecton/Terrain Architect Dashboard")]
        public static void ShowWindow()
        {
            GetWindow<TerrainDiagnosticsWindow>("Terrain Architect");
        }

        private void OnGUI()
        {
            GUILayout.Label("Terrain Architect Dashboard", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // TASK 1
            GUILayout.Label("TASK 1: PlayMode Capture", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter PlayMode to capture actual streaming terrain.", MessageType.Info);
            }
            GUI.enabled = EditorApplication.isPlaying;
            if (GUILayout.Button("1. Capture Heights & Slope/Hillshade", GUILayout.Height(30)))
            {
                CaptureTerrains();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // TASK 2
            GUILayout.Label("TASK 2: Resolution Matrix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This will change MapMagic settings and measure generation time. Run in Edit Mode.", MessageType.Info);
            GUI.enabled = !EditorApplication.isPlaying;
            if (GUILayout.Button("2. Run Matrix Test (1000m, 500m, 250m)", GUILayout.Height(30)))
            {
                RunMatrixTest();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // TASK 3
            GUILayout.Label("TASK 3: Offline Erosion Bake", EditorStyles.boldLabel);
            GUI.enabled = !EditorApplication.isPlaying;
            if (GUILayout.Button("3. Bake Erosion For Center Tile", GUILayout.Height(30)))
            {
                BakeErosion();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // TASK 5, 6, 7
            GUILayout.Label("TASKS 5, 6, 7: Live Systems Check", EditorStyles.boldLabel);
            if (GUILayout.Button("4. Dump Live Systems Status", GUILayout.Height(30)))
            {
                DumpLiveSystems();
            }
        }

        private void CaptureTerrains()
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0) return;

            string outDir = Path.Combine(Application.dataPath, "../Logs/TerrainDiagnostics");
            Directory.CreateDirectory(outDir);

            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;
                int w = t.terrainData.heightmapResolution;
                float[,] heights = t.terrainData.GetHeights(0, 0, w, w);

                Texture2D slopeTex = new Texture2D(w, w, TextureFormat.RGB24, false);
                Color32[] slopePixels = new Color32[w * w];
                Texture2D beautyTex = new Texture2D(w, w, TextureFormat.RGB24, false);
                Color32[] beautyPixels = new Color32[w * w];

                float sizeX = t.terrainData.size.x;
                float sizeZ = t.terrainData.size.z;
                float sizeY = t.terrainData.size.y;

                for (int y = 0; y < w; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        float curHeight = heights[y, x];
                        float hL = x > 0 ? heights[y, x - 1] : curHeight;
                        float hR = x < w - 1 ? heights[y, x + 1] : curHeight;
                        float hD = y > 0 ? heights[y - 1, x] : curHeight;
                        float hU = y < w - 1 ? heights[y + 1, x] : curHeight;

                        float dx = (hR - hL) * sizeY / (2.0f * (sizeX / w));
                        float dz = (hU - hD) * sizeY / (2.0f * (sizeZ / w));
                        Vector3 normal = new Vector3(-dx, 1, -dz).normalized;
                        float slopeAngle = Vector3.Angle(Vector3.up, normal);
                        byte slopeCol = (byte)Mathf.Clamp(slopeAngle / 45f * 255f, 0, 255);
                        slopePixels[idx] = new Color32(slopeCol, slopeCol, slopeCol, 255);

                        float dot = Mathf.Max(0, Vector3.Dot(normal, new Vector3(1, 1, 1).normalized));
                        byte shadeCol = (byte)(dot * 255f);
                        beautyPixels[idx] = new Color32(shadeCol, shadeCol, shadeCol, 255);
                    }
                }

                slopeTex.SetPixels32(slopePixels);
                slopeTex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, $"Live_{t.name}_Slope.png"), slopeTex.EncodeToPNG());

                beautyTex.SetPixels32(beautyPixels);
                beautyTex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, $"Live_{t.name}_Hillshade.png"), beautyTex.EncodeToPNG());

                DestroyImmediate(slopeTex);
                DestroyImmediate(beautyTex);
            }
            Debug.Log($"[TASK 1] Captures saved to {outDir}");
        }

        private void RunMatrixTest()
        {
            Debug.Log("[TASK 2] Triggering Resolution Matrix...");
            HeadlessMatrixBenchmark.Run();
        }

        private void BakeErosion()
        {
            Debug.Log("[TASK 3] Triggering Offline Bake...");
            OfflineErosionBakePipeline.BakeCenterTile();
        }

        private void DumpLiveSystems()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[TASKS 5, 6, 7] Live Systems Report:");
            
            var scatter = FindAnyObjectByType<WorldProceduralScatterDirector>();
            sb.AppendLine($"ScatterDirector: {(scatter != null ? "FOUND" : "MISSING")}");

            var voxel = FindAnyObjectByType<HectonVoxelStreamingBridge>();
            sb.AppendLine($"VoxelStreamingBridge: {(voxel != null ? "FOUND" : "MISSING")}");

            var seam = FindAnyObjectByType<WorldGenerativeGeologyTerrainSeamApplier>();
            sb.AppendLine($"TerrainSeamApplier: {(seam != null ? "FOUND" : "MISSING")}");

            Debug.Log(sb.ToString());
        }
    }
}

using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton8.Diagnostics
{
    public class TerrainPlaymodeCaptureTool : EditorWindow
    {
        [MenuItem("Tools/Hecton/Playmode Terrain Diagnostic Capture")]
        public static void ShowWindow()
        {
            GetWindow<TerrainPlaymodeCaptureTool>("Terrain Capture");
        }

        private void OnGUI()
        {
            GUILayout.Label("Terrain Diagnostic Capture", EditorStyles.boldLabel);
            GUILayout.Label("Use this tool while in PlayMode to capture the generated Terrains.");

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("You must be in PlayMode to capture streamed terrain.", MessageType.Warning);
            }

            GUI.enabled = EditorApplication.isPlaying;
            if (GUILayout.Button("Capture Live Terrains (Heightmap & Slope)", GUILayout.Height(40)))
            {
                CaptureTerrains();
            }
            GUI.enabled = true;
        }

        private void CaptureTerrains()
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0)
            {
                Debug.LogWarning("[CAPTURE] No Terrains found in the scene.");
                return;
            }

            string outDir = Path.Combine(Application.dataPath, "../Logs/TerrainCaptures");
            Directory.CreateDirectory(outDir);

            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;
                
                int w = t.terrainData.heightmapResolution;
                int h = t.terrainData.heightmapResolution;
                float[,] heights = t.terrainData.GetHeights(0, 0, w, h);

                Texture2D slopeTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                Color32[] slopePixels = new Color32[w * h];

                Texture2D beautyTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                Color32[] beautyPixels = new Color32[w * h];

                float sizeX = t.terrainData.size.x;
                float sizeZ = t.terrainData.size.z;
                float sizeY = t.terrainData.size.y;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        float curHeight = heights[y, x];
                        
                        // Calculate slope
                        float hL = x > 0 ? heights[y, x - 1] : curHeight;
                        float hR = x < w - 1 ? heights[y, x + 1] : curHeight;
                        float hD = y > 0 ? heights[y - 1, x] : curHeight;
                        float hU = y < h - 1 ? heights[y + 1, x] : curHeight;

                        float dx = (hR - hL) * sizeY / (2.0f * (sizeX / w));
                        float dz = (hU - hD) * sizeY / (2.0f * (sizeZ / h));
                        
                        Vector3 normal = new Vector3(-dx, 1, -dz).normalized;
                        float slopeAngle = Vector3.Angle(Vector3.up, normal);
                        byte slopeCol = (byte)Mathf.Clamp(slopeAngle / 45f * 255f, 0, 255);
                        slopePixels[idx] = new Color32(slopeCol, slopeCol, slopeCol, 255);

                        // Calculate hillshade
                        Vector3 lightDir = new Vector3(1, 1, 1).normalized;
                        float dot = Mathf.Max(0, Vector3.Dot(normal, lightDir));
                        byte shadeCol = (byte)(dot * 255f);
                        beautyPixels[idx] = new Color32(shadeCol, shadeCol, shadeCol, 255);
                    }
                }

                slopeTex.SetPixels32(slopePixels);
                slopeTex.Apply();
                byte[] slopeBytes = slopeTex.EncodeToPNG();
                string slopePath = Path.Combine(outDir, $"Live_{t.name}_Slope.png");
                File.WriteAllBytes(slopePath, slopeBytes);

                beautyTex.SetPixels32(beautyPixels);
                beautyTex.Apply();
                byte[] beautyBytes = beautyTex.EncodeToPNG();
                string beautyPath = Path.Combine(outDir, $"Live_{t.name}_Hillshade.png");
                File.WriteAllBytes(beautyPath, beautyBytes);

                DestroyImmediate(slopeTex);
                DestroyImmediate(beautyTex);

                Debug.Log($"[CAPTURE] Saved {t.name} to {slopePath} and {beautyPath}");
            }

            Debug.Log($"[CAPTURE] All captures saved to {outDir}");
        }
    }
}

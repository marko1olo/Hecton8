using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using Hecton8.World;

namespace Hecton8.Editor
{
    public static class TerrainRenderTestGoal
    {
        private static string ArtifactDir = "./";
        private static MapMagicObject mmObject;

        [MenuItem("Hecton8/Tests/Terrain Render Test")]
        public static void Execute()
        {
            try
            {
                Debug.Log("Starting Terrain Render Test...");
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");

                mmObject = Object.FindAnyObjectByType<MapMagicObject>();
                if (mmObject == null)
                {
                    Debug.LogError("No MapMagicObject found in the scene.");
                    File.WriteAllText(ArtifactDir + "mcp_error.txt", "No MapMagicObject found.");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                Debug.Log("MapMagicObject found. Forcing generation...");
                mmObject.StartGenerate();

                EditorApplication.update += CheckGenerationComplete;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Exception in TerrainRenderTestGoal: " + ex);
                File.WriteAllText(ArtifactDir + "mcp_error.txt", ex.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void CheckGenerationComplete()
        {
            if (mmObject == null)
            {
                EditorApplication.update -= CheckGenerationComplete;
                return;
            }

            if (mmObject.IsGenerating())
            {
                return;
            }

            EditorApplication.update -= CheckGenerationComplete;
            Debug.Log("MapMagic Generation Complete. Taking screenshots...");

            try
            {
                CaptureScreenshots();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Screenshot failed: " + ex);
                File.WriteAllText(ArtifactDir + "mcp_error.txt", ex.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void CaptureScreenshots()
        {
            try {
                var terrains = UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsSortMode.None);
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                
                string outStr = $"Found {terrains.Length} terrains. BaseMat: {baseMat != null}\n";
                foreach(var t in terrains) {
                    if (baseMat != null) {
                        Material instanced = new Material(baseMat);
                        t.materialTemplate = instanced;
                        if (t.terrainData != null && t.terrainData.alphamapTextureCount > 0) {
                            Texture2D[] alphamaps = t.terrainData.alphamapTextures;
                            if (alphamaps.Length > 0 && alphamaps[0] != null) instanced.SetTexture("_Control1", alphamaps[0]);
                            if (alphamaps.Length > 1 && alphamaps[1] != null) instanced.SetTexture("_Control2", alphamaps[1]);
                            instanced.SetVector("_TerrainSize", new Vector4(t.terrainData.size.x, t.terrainData.size.y, t.terrainData.size.z, 0));
                        }
                    }

                    var mat = t.materialTemplate;
                    if (mat == null) outStr += $"Terrain {t.name}: NULL Material!\n";
                    else outStr += $"Terrain {t.name}: Material='{mat.name}', Shader='{mat.shader.name}'\n";
                }
                File.WriteAllText(ArtifactDir + "terrain_mat_dump.txt", outStr);
            } catch {}

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("TestCamera");
                cam = camObj.AddComponent<Camera>();
                camObj.transform.position = new Vector3(0, 100, 0);
                camObj.transform.rotation = Quaternion.Euler(45, 0, 0);
            }

            Vector3[] positions = new Vector3[]
            {
                new Vector3(0, 50, 0),
                new Vector3(100, 20, 100),
                new Vector3(-100, 80, -100)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                cam.transform.position = positions[i];
                cam.transform.LookAt(new Vector3(positions[i].x, 0, positions[i].z));
                TakeScreenshot(cam, ArtifactDir + "Terrain_View_" + i + ".png");
            }

            Debug.Log("Screenshots captured!");
            File.WriteAllText(ArtifactDir + "mcp_success.txt", "DONE");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void TakeScreenshot(Camera cam, string filename)
        {
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Texture2D screenShot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(filename, bytes);
        }
    }
}

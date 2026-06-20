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
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");

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
            UnityEngine.Terrain[] terrains = UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(UnityEngine.FindObjectsSortMode.None);
            try {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
                Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
                Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");
                
                string outStr = $"Found {terrains.Length} terrains. BaseMat: {baseMat != null}\n";
                foreach(var t in terrains) {
                    var inj = t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                    if (inj != null) inj.enabled = false;

                    Material instanced = t.materialTemplate;
                    if (instanced == null) {
                        instanced = new Material(baseMat);
                        t.materialTemplate = instanced;
                    }

                    if (albedo != null) instanced.SetTexture("_AlbedoArray", albedo);
                    if (normal != null) instanced.SetTexture("_NormalArray", normal);
                    if (mask != null) instanced.SetTexture("_MaskArray", mask);

                    instanced.SetFloat("_UVScale", 4.0f);
                    instanced.SetFloat("_TriplanarBlend", 4.0f);
                    instanced.SetFloat("_MinDepth", -4600f);
                    instanced.SetFloat("_MaxDepth", 500f);
                    
                    if (t.terrainData != null && t.terrainData.alphamapTextureCount > 0) {
                        Texture2D[] alphamaps = t.terrainData.alphamapTextures;
                        if (alphamaps.Length > 0 && alphamaps[0] != null) instanced.SetTexture("_Control", alphamaps[0]);
                        if (alphamaps.Length > 1 && alphamaps[1] != null) instanced.SetTexture("_Control1", alphamaps[1]);
                        instanced.SetVector("_TerrainSize", new Vector4(t.terrainData.size.x, t.terrainData.size.y, t.terrainData.size.z, 0));
                    }

                    var mat = t.materialTemplate;
                    if (mat == null) outStr += $"Terrain {t.name}: NULL Material!\n";
                    else outStr += $"Terrain {t.name}: Material='{mat.name}', Shader='{mat.shader.name}'\n";
                }
                File.WriteAllText(ArtifactDir + "terrain_mat_dump.txt", outStr);
            } catch (System.Exception ex) { Debug.LogException(ex); }

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("TestCamera");
                cam = camObj.AddComponent<Camera>();
            }
            cam.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 50000f;

            Vector3 center = Vector3.zero;
            if (terrains.Length > 0 && terrains[0].terrainData != null) {
                center = terrains[0].transform.position + new Vector3(terrains[0].terrainData.size.x / 2, 0, terrains[0].terrainData.size.z / 2);
                center.y = terrains[0].SampleHeight(center);
            }

            Vector3[] positions = new Vector3[]
            {
                center + new Vector3(0, 50, 0),
                center + new Vector3(100, 20, 100),
                center + new Vector3(-100, 80, -100)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                cam.transform.position = positions[i];
                cam.transform.LookAt(new Vector3(positions[i].x, center.y, positions[i].z));
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

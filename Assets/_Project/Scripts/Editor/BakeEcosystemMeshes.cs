using UnityEngine;
using UnityEditor;
using System.IO;
using Hecton8.Editor;
using System.Collections.Generic;

namespace Hecton8.Editor
{
    public static class BakeEcosystemMeshes
    {
        [MenuItem("Hecton8/Bake Ecosystem Meshes")]
        public static void BakeAll()
        {
            string exportDir = "Assets/_Project/Prefabs/GeneratedEcosystem";
            if (!AssetDatabase.IsValidFolder(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }

            Material coralMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/MAT_ProceduralCoral.mat");
            if (coralMat == null)
            {
                coralMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(coralMat, "Assets/_Project/Art/Materials/Terrain/MAT_ProceduralCoral.mat");
            }
            coralMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            coralMat.enableInstancing = true;
            coralMat.SetColor("_BaseColor", new Color(0.6f, 0.2f, 0.4f, 1)); // Deep red/purple

            // Setup Alpha Test
            coralMat.SetFloat("_AlphaClip", 1.0f);
            coralMat.EnableKeyword("_ALPHATEST_ON");
            coralMat.renderQueue = 2450;
            coralMat.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1));

            var cColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/color___family.coral.brittle.png");
            if (cColor != null) coralMat.SetTexture("_BaseMap", cColor);

            var cNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/normal___family.coral.brittle.png");
            if (cNormal != null) coralMat.SetTexture("_BumpMap", cNormal);

            Material kelpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/MAT_ProceduralKelp.mat");
            if (kelpMat == null)
            {
                kelpMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(kelpMat, "Assets/_Project/Art/Materials/Terrain/MAT_ProceduralKelp.mat");
            }
            kelpMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            kelpMat.enableInstancing = true;
            kelpMat.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.6f, 1)); // Deep ocean blue-green

            kelpMat.SetFloat("_AlphaClip", 1.0f);
            kelpMat.EnableKeyword("_ALPHATEST_ON");
            kelpMat.renderQueue = 2450;
            kelpMat.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1));

            var kColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/color___family.kelp.tall.png");
            if (kColor != null) kelpMat.SetTexture("_BaseMap", kColor);

            var kNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/normal___family.kelp.tall.png");
            if (kNormal != null) kelpMat.SetTexture("_BumpMap", kNormal);
            // FIX: Mark materials dirty so they save to disk
            EditorUtility.SetDirty(coralMat);
            EditorUtility.SetDirty(kelpMat);
            AssetDatabase.SaveAssets();

            string[] coralTokens = {
                "family_coral_brittle__lace", "family_coral_brittle__crown", "family_coral_brittle__thicket",
                "family_coral_brittle__halo", "family_coral_brittle__candelabra", "family_coral_brittle__wreath",
                "family_coral_brittle__cathedral"
            };

            List<GameObject> bakedCorals = new List<GameObject>();
            foreach (var token in coralTokens)
            {
                if (WorldProceduralCoralMeshBuilder.TryBuild(token, new Vector3(3, 3, 3), 0, out Mesh mesh))
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    System.IO.File.AppendAllText("C:/Users/danat/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/RenderLog.txt", $"[BAKE] Coral {token} vertexCount: {mesh.vertexCount}\n");
                    bakedCorals.Add(SavePrefab(mesh, token, coralMat, exportDir));
                }
            }

            string[] seaweedTokens = {
                "family_kelp_tall__stalk", "family_kelp_tall__lean", "family_kelp_tall__ribbon",
                "family_kelp_tall__lamina", "family_kelp_tall__rope", "family_kelp_tall__banner",
                "family_kelp_tall__lance"
            };

            List<GameObject> bakedKelp = new List<GameObject>();
            foreach (var token in seaweedTokens)
            {
                if (WorldProceduralSeaweedMeshBuilder.TryBuild(token, new Vector3(2, 6, 2), 0, out Mesh mesh))
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    System.IO.File.AppendAllText("C:/Users/danat/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/RenderLog.txt", $"[BAKE] Kelp {token} vertexCount: {mesh.vertexCount}\n");
                    bakedKelp.Add(SavePrefab(mesh, token, kelpMat, exportDir));
                }
            }

            Debug.Log($"[BAKE] Successfully baked {bakedCorals.Count} corals and {bakedKelp.Count} kelps.");
        }

        private static GameObject SavePrefab(Mesh mesh, string token, Material mat, string exportDir)
        {
            string meshPath = $"{exportDir}/{token}_Mesh.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject go = new GameObject(token);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            string prefabPath = $"{exportDir}/{token}_Prefab.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // We can also have an execute method for batch mode
        public static void ExecuteBatch()
        {
            BakeAll();
            EditorApplication.Exit(0);
        }
    }
}
